using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Services
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

        public async Task<Company> CreateCompanyAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));

            var safeName = MakeSafeName(name);

            var exists = await _serverDb.Companies.AnyAsync(c => c.Name == safeName, cancellationToken);
            if (exists) throw new InvalidOperationException("Company already exists");

            var dbName = GetTenantDbName(safeName);

            var company = new Company { Name = safeName, DbName = dbName };
            _serverDb.Companies.Add(company);
            await _serverDb.SaveChangesAsync(cancellationToken);

            var defaultConn = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing");

            try
            {
                var connBuilder = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn);

                connBuilder.Database = string.Empty;

                var serverConnString = connBuilder.ToString();

                using var serverConn = new MySqlConnector.MySqlConnection(serverConnString);
                await serverConn.OpenAsync(cancellationToken);

                _logger.LogInformation("Checking existence of tenant database '{DbName}'", dbName);

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
                            createCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;";
                            await createCmd.ExecuteNonQueryAsync(cancellationToken);

                            _logger.LogInformation("Database '{DbName}' created successfully (or already existed)", dbName);
                        }
                        catch (Exception exCreate)
                        {
                            _logger.LogError(exCreate, "Failed to create database '{DbName}'. Host={Host}, Port={Port}", dbName, connBuilder.Server, connBuilder.Port);

                            throw;
                        }

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
                _logger.LogError(ex, "Error while creating physical database for company {Company}. Company record saved with DbName {DbName}", safeName, dbName);
                throw;
            }

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
                var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn) { Database = dbName }.ToString();

                optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("Edemly.Server");
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
            return $"edemly_company_{safeName}";
        }

        public async Task<List<Company>> ListCompaniesAsync(CancellationToken cancellationToken = default)
        {
            return await _serverDb.Companies.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        }

        public async Task AddEmailsToTenantAsync(int companyId, List<string> emails, CancellationToken cancellationToken = default)
        {
            if (emails == null || emails.Count == 0)
                throw new ArgumentException("At least one email is required", nameof(emails));

            var company = await _serverDb.Companies.FindAsync(
                new object[] { companyId },
                cancellationToken);

            if (company == null)
                throw new InvalidOperationException("Company not found");

            var defaultConn = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing");

            var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn)
            {
                Database = company.DbName
            }.ToString();

            var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
            optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
            {
                mysqlOptions.MigrationsAssembly("Edemly.Server");
            });

            using var tenantCtx = new CompanyDbContext(optionsBuilder.Options);

            var emailEntities = emails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => new Data.Entities.Email
                {
                    EmailAddress = e.Trim()
                })
                .ToList();

            if (emailEntities.Count == 0)
                throw new ArgumentException("At least one valid email is required", nameof(emails));

            tenantCtx.Emails.AddRange(emailEntities);
            await tenantCtx.SaveChangesAsync(cancellationToken);
        }

        public async Task EnsureTenantDatabaseAsync(Company company, CancellationToken cancellationToken = default)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));

            var dbName = company.DbName;

            var defaultConn = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection missing");

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

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
                var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn) { Database = dbName }.ToString();

                optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("Edemly.Server");
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