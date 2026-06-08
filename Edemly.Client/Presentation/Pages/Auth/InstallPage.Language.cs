#nullable enable

using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class InstallPage
    {
        private void InitializeLanguageOptions()
        {
            LanguageComboBox.Items.Clear();
            LanguageComboBox.Items.Add(CreateLanguageItem("English", "en"));
            LanguageComboBox.Items.Add(CreateLanguageItem("Українська", "uk"));
        }

        private static ComboBoxItem CreateLanguageItem(string displayName, string languageTag)
        {
            return new ComboBoxItem
            {
                Content = displayName,
                Tag = languageTag
            };
        }

        private static string ResolveInitialLanguage()
        {
            var savedLanguage = ConfigService.Instance?.Language;
            if (!string.IsNullOrWhiteSpace(savedLanguage))
            {
                return savedLanguage;
            }

            var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? "en";
            return systemLanguage is "uk" or "ru" ? "uk" : "en";
        }

        private void SelectLanguage(string languageTag)
        {
            LanguageComboBox.SelectedItem = LanguageComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, languageTag, StringComparison.OrdinalIgnoreCase));

            TryLoadLanguage(languageTag, "[InstallPage] LoadLanguage failed");
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var languageTag = GetSelectedLanguageTag();
            SaveLanguageSelection(languageTag);
            TryLoadLanguage(languageTag, "[InstallPage] LoadLanguage on selection failed");
            ApplyLanguage();
        }

        private string GetSelectedLanguageTag()
        {
            return (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "en";
        }

        private static void SaveLanguageSelection(string languageTag)
        {
            try
            {
                ConfigService.Instance.Language = languageTag;
                ConfigService.Instance.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstallPage] Save config language failed: {ex}");
            }
        }

        private static void TryLoadLanguage(string languageTag, string errorPrefix)
        {
            try
            {
                LanguageService.Instance.LoadLanguage(languageTag);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{errorPrefix}: {ex}");
            }
        }
    }
}
