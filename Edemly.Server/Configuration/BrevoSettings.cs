namespace Edemly.Server.Configuration
{
    public class BrevoSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string SenderEmail { get; set; } = "no-reply@edemly.me";

        public string SenderName { get; set; } = "Edemly Team";

        public string LogoUrl { get; set; } = "https://raw.githubusercontent.com/ReedSnake/SmartTravelPlanner/master/logo.png";

        public string ServiceName { get; set; } = "Edemly";

        public int CodeExpirationMinutes { get; set; } = 10;
    }
}