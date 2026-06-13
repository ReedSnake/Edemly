namespace Edemly.Client.Infrastructure.Storage
{
    public interface IConfigService
    {
        string Language { get; set; }
        string Theme { get; set; }
        bool SaveCredentials { get; set; }

        bool IsInstalled { get; set; }
        string Company { get; set; }
        string ExePath { get; }
        string ServerUrl { get; set; }
        string HubServerUrl { get; set; }
        string ClientConfigUrl { get; set; }
        string UpdateFeedUrl { get; set; }

        string BackgroundImagePath { get; set; }

        void Load();

        void Save();

        T GetValue<T>(string key, T defaultValue);

        void SetValue<T>(string key, T value);
    }
}
