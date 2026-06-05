using System.IO;
using System.Text.Json;
namespace Edemly.Client.Infrastructure.Storage
{
    public class ConfigService : IConfigService
    {
        private static ConfigService? _instance;
        private Dictionary<string, object> _config;
        private readonly string _configFilePath;

        public static ConfigService Instance => _instance ??= new ConfigService();

        private ConfigService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Edemly");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            _configFilePath = Path.Combine(appFolder, "config.json");
            _config = new Dictionary<string, object>();

            Load();
        }

        public string Language
        {
            get => GetValue("Language", "en");
            set => SetValue("Language", value);
        }

        public string Theme
        {
            get => GetValue("Theme", "Default");
            set => SetValue("Theme", value);
        }

        public bool SaveCredentials
        {
            get => GetValue("SaveCredentials", false);
            set => SetValue("SaveCredentials", value);
        }

        public bool IsInstalled
        {
            get => GetValue("IsInstalled", false);
            set => SetValue("IsInstalled", value);
        }

        public string Company
        {
            get => GetValue("Company", string.Empty);
            set => SetValue("Company", value);
        }

        public bool CreateDesktopShortcut
        {
            get => GetValue("CreateDesktopShortcut", false);
            set => SetValue("CreateDesktopShortcut", value);
        }

        public string ExePath
        {
            get => GetValue("ExePath", System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty);
            set => SetValue("ExePath", value);
        }

        public string BackgroundImagePath
        {
            get => GetValue("BackgroundImagePath", string.Empty);
            set => SetValue("BackgroundImagePath", value);
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var loadedConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                    if (loadedConfig != null)
                    {
                        _config.Clear();
                        foreach (var kvp in loadedConfig)
                        {
                            _config[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        public T GetValue<T>(string key, T defaultValue)
        {
            if (_config.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                    }

                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            if (value != null)
            {
                _config[key] = value!;
                Save();
            }
            else
            {
                _config.Remove(key);
                Save();
            }
        }
    }
}