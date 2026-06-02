namespace uchat_server.Configuration
{
    /// <summary>
    /// Конфігурація для фонового Worker Service
    /// </summary>
    public class WorkerSettings
    {
        /// <summary>
        /// Інтервал очищення застарілих сесій (у хвилинах)
        /// </summary>
        public int SessionCleanupIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Інтервал перевірки нагадувань (у хвилинах)
        /// </summary>
        public int ReminderCheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Інтервал перевірки платежів (у хвилинах)
        /// </summary>
        public int PaymentCheckIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// Період зберігання сесій після останнього доступу
        /// </summary>
        public int SessionExpirationDays { get; set; } = 30;

        /// <summary>
        /// Період очікування перед тим як Pending перетворить у Failed
        /// </summary>
        public int PaymentTimeoutHours { get; set; } = 24;

        /// <summary>
        /// Затримка перед першим запуском Worker (у секундах)
        /// </summary>
        public int StartupDelaySeconds { get; set; } = 10;

        /// <summary>
        /// Максимальна кількість записів для обробки за одну ітерацію
        /// </summary>
        public int BatchSize { get; set; } = 100;

        // Властивості-хелпери для TimeSpan
        public TimeSpan SessionCleanupInterval => TimeSpan.FromMinutes(SessionCleanupIntervalMinutes);
        public TimeSpan ReminderCheckInterval => TimeSpan.FromMinutes(ReminderCheckIntervalMinutes);
        public TimeSpan PaymentCheckInterval => TimeSpan.FromMinutes(PaymentCheckIntervalMinutes);
        public TimeSpan SessionExpiration => TimeSpan.FromDays(SessionExpirationDays);
        public TimeSpan PaymentTimeout => TimeSpan.FromHours(PaymentTimeoutHours);
        public TimeSpan StartupDelay => TimeSpan.FromSeconds(StartupDelaySeconds);
    }
}