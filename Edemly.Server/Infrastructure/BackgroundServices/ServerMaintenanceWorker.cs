using Edemly.Server.Api.Hubs;
using Edemly.Server.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Edemly.Server
{
    public class ServerMaintenanceWorker : BackgroundService
    {
        private readonly ILogger<ServerMaintenanceWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly WorkerSettings _settings;
        private readonly IHubContext<MainHub> _hub;

        private DateTime _lastSessionCleanup = DateTime.MinValue;
        private DateTime _lastReminderCheck = DateTime.MinValue;
        private DateTime _lastPaymentCheck = DateTime.MinValue;

        public ServerMaintenanceWorker(
            ILogger<ServerMaintenanceWorker> logger,
            IServiceProvider serviceProvider,
            IOptions<WorkerSettings> settings,
            IHubContext<MainHub> hub)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _settings = settings.Value;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker Service запущено о {Time}", DateTimeOffset.Now);

            await Task.Delay(_settings.StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var tasks = new List<Task>();

                    if (now - _lastSessionCleanup >= _settings.SessionCleanupInterval)
                    {
                        tasks.Add(ExecuteTaskAsync(() => CleanupExpiredSessionsAsync(stoppingToken),
                            () => _lastSessionCleanup = now, "Cleanup sessions", stoppingToken));
                    }

                    if (now - _lastReminderCheck >= _settings.ReminderCheckInterval)
                    {
                        tasks.Add(ExecuteTaskAsync(() => CheckPendingRemindersAsync(stoppingToken),
                            () => _lastReminderCheck = now, "Check reminders", stoppingToken));
                    }

                    if (now - _lastPaymentCheck >= _settings.PaymentCheckInterval)
                    {
                        tasks.Add(ExecuteTaskAsync(() => CheckPendingPaymentsAsync(stoppingToken),
                            () => _lastPaymentCheck = now, "Check payments", stoppingToken));
                    }

                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks);
                    }

                    var checkInterval = TimeSpan.FromMinutes(1);
                    await Task.Delay(checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Worker Service зупиняється...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Помилка в Worker Service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Worker Service зупинено о {Time}", DateTimeOffset.Now);
        }

        private async Task ExecuteTaskAsync(
            Func<Task> taskFunc,
            Action updateLastRun,
            string taskName,
            CancellationToken stoppingToken)
        {
            try
            {
                await taskFunc();
                updateLastRun();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при виконанні задачі: {TaskName}", taskName);
            }
        }

        private async Task CleanupExpiredSessionsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            try
            {
                var sessionsToDelete = await context.Sessions.Where(s => s.ExpirationTime < DateTime.UtcNow).ToListAsync();
                context.RemoveRange(sessionsToDelete);
                context.SaveChanges();
                _logger.LogInformation($"Sessions deleted: {sessionsToDelete.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при очищенні застарілих сесій");
            }
        }

        private async Task CheckPendingRemindersAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            try
            {
                var dueReminders = await dbContext.Remindings
                    .Where(r => r.ShouldNotify && r.ShowTime && r.LastTime < DateTime.UtcNow)
                    .Include(r => r.User)
                    .GroupBy(r => r.UserId)
                    .Select(g => g.OrderBy(r => r.LastTime).First())
                    .ToListAsync(stoppingToken);

                if (dueReminders.Count > 0)
                {
                    _logger.LogInformation($"Знайдено {dueReminders.Count} нагадувань для обробки");

                    var reminderIds = new List<int>();

                    foreach (var reminder in dueReminders)
                    {
                        try
                        {
                            await _hub.Clients.User(reminder.UserId.ToString()).SendAsync("SendNotifyReminder", reminder.Id, cancellationToken: stoppingToken);

                            _logger.LogInformation($"Sent reminder to user with id {reminder.UserId}: {reminder.Name}");

                            reminderIds.Add(reminder.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Помилка при обробці нагадування {reminder.Id}");
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("Немає нагадувань для обробки");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перевірці нагадувань");
            }
        }

        private async Task CheckPendingPaymentsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            try
            {
                var timeoutDate = DateTime.UtcNow.Subtract(_settings.PaymentTimeout);

                var updatedCount = await dbContext.Payments
                    .Where(p => p.Status == PaymentStatus.Pending && p.UpdatedAt < timeoutDate)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Status, PaymentStatus.Failed)
                        .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                        stoppingToken);

                if (updatedCount > 0)
                {
                    _logger.LogWarning("Оновлено {Count} застарілих платежів до статусу Failed", updatedCount);
                }
                else
                {
                    _logger.LogDebug("Немає застарілих платежів");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перевірці платежів");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker Service ������� ������ �������");
            await base.StopAsync(stoppingToken);
        }
    }
}