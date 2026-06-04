namespace Edemly.Server.Configuration
{
    public class WorkerSettings
    {
        public int SessionCleanupIntervalMinutes { get; set; } = 60;

        public int ReminderCheckIntervalMinutes { get; set; } = 5;

        public int PaymentCheckIntervalMinutes { get; set; } = 10;

        public int SessionExpirationDays { get; set; } = 30;

        public int PaymentTimeoutHours { get; set; } = 24;

        public int StartupDelaySeconds { get; set; } = 10;

        public int BatchSize { get; set; } = 100;

        public TimeSpan SessionCleanupInterval => TimeSpan.FromMinutes(SessionCleanupIntervalMinutes);
        public TimeSpan ReminderCheckInterval => TimeSpan.FromMinutes(ReminderCheckIntervalMinutes);
        public TimeSpan PaymentCheckInterval => TimeSpan.FromMinutes(PaymentCheckIntervalMinutes);
        public TimeSpan SessionExpiration => TimeSpan.FromDays(SessionExpirationDays);
        public TimeSpan PaymentTimeout => TimeSpan.FromHours(PaymentTimeoutHours);
        public TimeSpan StartupDelay => TimeSpan.FromSeconds(StartupDelaySeconds);
    }
}