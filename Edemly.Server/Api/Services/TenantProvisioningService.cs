using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using uchat_server.Data;
using uchat_server.Data.Entities;

namespace uchat_server.Api.Services
{
    public class TenantProvisioningService
    {
        private readonly ServerDbContext _serverDb;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantProvisioningService> _logger;

        public TenantProvisioningService(ServerDbContext serverDb, IConfiguration configuration, ILogger<TenantProvisioningService> logger)
        {
            _serverDb = serverDb;
            _configuration = configuration;
            _logger = logger;
        }

        // Create tenant: add to Companies and create physical database if missing
        public async Task<Company> CreateCompanyAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));

            var safeName = MakeSafeName(name);

            // check existing
            var exists = await _serverDb.Companies.AnyAsync(c => c.Name == safeName, cancellationToken);
            if (exists) throw new InvalidOperationException("Company already exists");

            // Build DB name
            var dbName = GetTenantDbName(safeName);

            // Save company record
            var company = new Company { Name = safeName, DbName = dbName };
            _serverDb.Companies.Add(company);
            await _serverDb.SaveChangesAsync(cancellationToken);

            // Create physical database if not exists
            var defaultConn = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing");

            try
            {
                // For MySQL - replace database in connection string
                var connBuilder = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn);

                // Do NOT force a specific database name for server-level connection; Aiven may not expose 'mysql' DB
                // Use empty Database so connection is at server level
                connBuilder.Database = string.Empty;

                // Use server connection to check/create database
                var serverConnString = connBuilder.ToString();

                using var serverConn = new MySqlConnector.MySqlConnection(serverConnString);
                await serverConn.OpenAsync(cancellationToken);

                _logger.LogInformation("Checking existence of tenant database '{DbName}'", dbName);

                // Check if database exists
                using (var cmd = serverConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @db";
                    var p = cmd.CreateParameter(); p.ParameterName = "@db"; p.Value = dbName; cmd.Parameters.Add(p);

                    var existsObj = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (existsObj == null)
                    {
                        _logger.LogInformation("Database '{DbName}' not found, attempting to create", dbName);

                        try
                        {
                            using var createCmd = serverConn.CreateCommand();
                            // Use IF NOT EXISTS to make creation idempotent and avoid race errors
                            createCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;";
                            await createCmd.ExecuteNonQueryAsync(cancellationToken);

                            _logger.LogInformation("Database '{DbName}' created successfully (or already existed)", dbName);
                        }
                        catch (Exception exCreate)
                        {
                            // Log detailed error - helps debugging Aiven permissions or SQL errors
                            _logger.LogError(exCreate, "Failed to create database '{DbName}'. Host={Host}, Port={Port}", dbName, connBuilder.Server, connBuilder.Port);

                            // rethrow so caller knows creation failed
                            throw;
                        }

                        // re-check existence
                        using (var verifyCmd = serverConn.CreateCommand())
                        {
                            verifyCmd.CommandText = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @db";
                            var pv = verifyCmd.CreateParameter(); pv.ParameterName = "@db"; pv.Value = dbName; verifyCmd.Parameters.Add(pv);

                            var verify = await verifyCmd.ExecuteScalarAsync(cancellationToken);
                            _logger.LogInformation("Post-create verification for '{DbName}': {Exists}", dbName, verify != null);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Database '{DbName}' already exists", dbName);
                    }
                }
            }
            catch (Exception ex)
            {
                // If DB creation failed, leave company record in master but log and propagate
                _logger.LogError(ex, "Error while creating physical database for company {Company}. Company record saved with DbName {DbName}", safeName, dbName);
                throw;
            }

            // Apply migrations to the newly created database
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
                var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn) { Database = dbName }.ToString();

                optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("uchat_server");
                });

                using (var tenantCtx = new CompanyDbContext(optionsBuilder.Options))
                {
                    _logger.LogInformation("Applying migrations to tenant database '{DbName}'", dbName);
                    await tenantCtx.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Migrations applied to '{DbName}'", dbName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply migrations to tenant database '{DbName}'", dbName);
                throw;
            }

            _logger.LogInformation("Company {Company} created with DB {Db}", safeName, dbName);

            return company;
        }

        private static string MakeSafeName(string name)
        {
            return name.Trim().ToLowerInvariant().Replace(' ', '_');
        }

        private static string GetTenantDbName(string safeName)
        {
            return $"uchat_company_{safeName}";
        }

        public async Task<List<Company>> ListCompaniesAsync(CancellationToken cancellationToken = default)
        {
            return await _serverDb.Companies.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        }

        // Add email into tenant database
        public async Task AddEmailToTenantAsync(int companyId, string email, CancellationToken cancellationToken = default)
        {
            var company = await _serverDb.Companies.FindAsync(new object[] { companyId }, cancellationToken);
            if (company == null) throw new InvalidOperationException("Company not found");

            var defaultConn = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing");

            var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn) { Database = company.DbName }.ToString();

            var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
            optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
            {
                mysqlOptions.MigrationsAssembly("uchat_server");
            });

            using var tenantCtx = new CompanyDbContext(optionsBuilder.Options);

            var e = new Data.Entities.Email { EmailAddress = email };
            tenantCtx.Emails.Add(e);
            await tenantCtx.SaveChangesAsync(cancellationToken);
        }

        // Ensure physical database exists and migrations applied for given company
        public async Task EnsureTenantDatabaseAsync(Company company, CancellationToken cancellationToken = default)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));

            var dbName = company.DbName;

            var defaultConn = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing");

            // Create DB if missing
            try
            {
                var connBuilder = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn)
                {
                    Database = string.Empty
                };

                using var serverConn = new MySqlConnector.MySqlConnection(connBuilder.ToString());
                await serverConn.OpenAsync(cancellationToken);

                using (var cmd = serverConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @db";
                    var p = cmd.CreateParameter(); p.ParameterName = "@db"; p.Value = dbName; cmd.Parameters.Add(p);

                    var existsObj = await cmd.ExecuteScalarAsync(cancellationToken);
                    if (existsObj == null)
                    {
                        _logger.LogInformation("Tenant DB {Db} missing ? creating", dbName);
                        using var createCmd = serverConn.CreateCommand();
                        // Use IF NOT EXISTS for idempotency
                        createCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;";
                        await createCmd.ExecuteNonQueryAsync(cancellationToken);
                        _logger.LogInformation("Tenant DB {Db} created", dbName);
                    }
                    else
                    {
                        _logger.LogInformation("Tenant DB {Db} already exists", dbName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure physical DB for tenant {Company} ({Db})", company.Name, dbName);
                throw;
            }

            // Apply migrations
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
                var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn) { Database = dbName }.ToString();

                optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("uchat_server");
                });

                using var tenantCtx = new CompanyDbContext(optionsBuilder.Options);
                await tenantCtx.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Applied migrations to tenant DB {Db}", dbName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply migrations to tenant DB {Db}", dbName);
                throw;
            }
        }
    }
}
