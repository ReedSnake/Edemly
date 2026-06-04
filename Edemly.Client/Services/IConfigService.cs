namespace Edemly.Client.Services
{
    public interface IConfigService
    {
        string Language { get; set; }
        string Theme { get; set; }
        bool SaveCredentials { get; set; }

        bool IsInstalled { get; set; }
        string Company { get; set; }
        bool CreateDesktopShortcut { get; set; }
        string ExePath { get; set; }

        string BackgroundImagePath { get; set; }

        void Load();

        void Save();

        T GetValue<T>(string key, T defaultValue);

        void SetValue<T>(string key, T value);
    }
}