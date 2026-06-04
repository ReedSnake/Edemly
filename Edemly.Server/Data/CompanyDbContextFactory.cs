using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Edemly.Server.Data
{
    internal class CompanyDbContextFactory : IDesignTimeDbContextFactory<CompanyDbContext>
    {
        public CompanyDbContext CreateDbContext(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();

            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                options => options.EnableRetryOnFailure()
                    .MigrationsAssembly("Edemly.Server")
            );

            return new CompanyDbContext(optionsBuilder.Options);
        }
    }
}