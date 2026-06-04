using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Services
{
    public class TenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly IConfiguration _configuration;

        public TenantDbContextFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public CompanyDbContext CreateCompanyDbContext(Company company)
        {
            var defaultConn = _configuration.GetConnectionString("DefaultConnection");

            var dbName = company.DbName;
            if (string.IsNullOrWhiteSpace(dbName))
            {
                var name = (company.Name ?? string.Empty).ToLowerInvariant();
                var sb = new System.Text.StringBuilder();
                foreach (var ch in name)
                {
                    if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                    else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '.') sb.Append('_');
                }
                var sanitized = sb.ToString().Trim('_');
                if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "company";
                dbName = $"edemly_company_{sanitized}";
            }

            var tenantConn = new MySqlConnector.MySqlConnectionStringBuilder(defaultConn!) { Database = dbName }.ToString();

            var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
            optionsBuilder.UseMySql(tenantConn, ServerVersion.AutoDetect(tenantConn), mysqlOptions => { mysqlOptions.MigrationsAssembly("Edemly.Server"); });

            return new CompanyDbContext(optionsBuilder.Options);
        }
    }
}