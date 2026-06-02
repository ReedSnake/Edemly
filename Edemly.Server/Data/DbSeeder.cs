using Microsoft.EntityFrameworkCore;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Data
{
    /// <summary>
    /// Сервіс для ініціалізації бази даних тестовими даними
    /// </summary>
    public static class DbSeeder
    {
        /// <summary>
        /// Створює адміністратора, якщо його ще немає
        /// </summary>
        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            try
            {
                // Перевіряємо, чи є вже адмін
                var adminEmail = configuration["AdminEmail"] ?? "admin@edemly.local";
                var existingAdmin = await context.LoginInfos
                    .FirstOrDefaultAsync(l => l.Email == adminEmail);

                if (existingAdmin != null)
                {
                    logger.LogInformation("Admin user already exists");
                    return;
                }

                // Створюємо LoginInfo
                var loginInfo = new LoginInfo
                {
                    Email = adminEmail,
                    IsEmailVerified = true
                };
                context.LoginInfos.Add(loginInfo);
                await context.SaveChangesAsync();

                // Створюємо User з усіма деталями
                var adminUser = new User
                {
                    Username = "Admin",
                    LoginInfoId = loginInfo.Id,
                    FirstName = "System",
                    LastName = "Admin",
                    PhoneNumber = null,
                    SubscriptionStatus = SubscriptionStatus.Vip,
                    SubscriptionExpiration = DateTime.UtcNow.AddYears(100), // Безстрокова VIP підписка
                    PfpUrl = null,
                    LastOnline = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                logger.LogInformation("Admin user created successfully: {Email}", adminEmail);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error seeding admin user");
                throw;
            }
        }
    }
}
