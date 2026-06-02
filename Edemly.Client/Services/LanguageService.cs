using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Edemly.Client.Services
{
    internal class LanguageService : ILanguageService
    {
        private static LanguageService? _instance;
        private Dictionary<string, Dictionary<string, string>> _translations;
        private string _currentLanguage;

        public static LanguageService Instance => _instance ??= new LanguageService();

        private LanguageService()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>();

            // Завантажуємо збережену мову або використовуємо English за замовчуванням
            var savedLanguage = ConfigService.Instance.Language;
            _currentLanguage = savedLanguage;
            LoadLanguage(savedLanguage);
        }

        public bool LoadLanguage(string languageCode)
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Language", $"{languageCode}.json");

                if (!File.Exists(filePath))
                    return false;

                string jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
                var loadedTranslations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);

                if (loadedTranslations != null)
                {
                    _translations = loadedTranslations;
                    _currentLanguage = languageCode;

                    // Зберігаємо вибір мови через ConfigService
                    ConfigService.Instance.Language = languageCode;

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading language file: {ex.Message}");
                return false;
            }
        }

        public string GetText(string category, string key, string? fallback = null)
        {
            if (_translations.TryGetValue(category, out var categoryTexts))
            {
                if (categoryTexts.TryGetValue(key, out var text))
                {
                    return text;
                }
            }

            return fallback ?? $"[{category}.{key}]";
        }

        public string CurrentLanguage => _currentLanguage;
    }
}
