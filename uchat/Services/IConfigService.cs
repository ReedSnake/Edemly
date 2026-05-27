namespace uchat.Services
{
    public interface IConfigService
    {
        string Language { get; set; }
        string Theme { get; set; }
        bool SaveCredentials { get; set; }

        // Added: installation metadata
        bool IsInstalled { get; set; }
        string Company { get; set; }
        bool CreateDesktopShortcut { get; set; }
        string ExePath { get; set; }

        // Background image path for wallpaper
        string BackgroundImagePath { get; set; }

        void Load();
        void Save();
        T GetValue<T>(string key, T defaultValue);
        void SetValue<T>(string key, T value);
    }
}
