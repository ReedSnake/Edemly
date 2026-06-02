namespace Edemly.Server.Configuration
{
    /// <summary>
    /// Налаштування для Brevo Email Service
    /// </summary>
    public class BrevoSettings
    {
        /// <summary>
        /// API ключ Brevo
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Email відправника (має бути верифікований в Brevo)
        /// </summary>
        public string SenderEmail { get; set; } = "no-reply@edemly.me";

        /// <summary>
        /// Ім'я відправника
        /// </summary>
        public string SenderName { get; set; } = "Uchat Team";

        /// <summary>
        /// URL логотипу для email
        /// </summary>
        public string LogoUrl { get; set; } = "https://raw.githubusercontent.com/ReedSnake/SmartTravelPlanner/master/logo.png";

        /// <summary>
        /// Назва сервісу для відображення в email
        /// </summary>
        public string ServiceName { get; set; } = "Uchat";

        /// <summary>
        /// Час життя коду в хвилинах
        /// </summary>
        public int CodeExpirationMinutes { get; set; } = 10;
    }
}