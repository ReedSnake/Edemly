using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Theme;
using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Infrastructure.Startup
{
    public static class AppEnvironmentInitializer
    {
        public static void ApplySavedPreferences(System.Windows.Application application, IConfigService? config)
        {
            try
            {
                var savedLang = config?.Language;
                if (string.IsNullOrWhiteSpace(savedLang))
                {
                    savedLang = "en";
                }

                ApplyLanguage(savedLang);
                ApplyTheme();
                ApplyCulture(savedLang);
                ApplyBackgroundImage(application, config?.BackgroundImagePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ENV] Startup preferences failed: {ex}");
            }
        }

        private static void ApplyLanguage(string savedLang)
        {
            try
            {
                LanguageService.Instance.LoadLanguage(savedLang);
                Debug.WriteLine($"[APP ENV] Language set to: {LanguageService.Instance.CurrentLanguage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ENV] LoadLanguage failed: {ex}");
            }
        }

        private static void ApplyTheme()
        {
            try
            {
                ThemeService.Instance.LoadAndApplyTheme();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ENV] LoadAndApplyTheme failed: {ex}");
            }
        }

        private static void ApplyCulture(string savedLang)
        {
            try
            {
                CultureInfo cultureInfo;
                if (string.Equals(savedLang, "uk", StringComparison.OrdinalIgnoreCase))
                {
                    cultureInfo = new CultureInfo("uk-UA");
                }
                else if (string.Equals(savedLang, "en", StringComparison.OrdinalIgnoreCase))
                {
                    cultureInfo = new CultureInfo("en-US");
                }
                else
                {
                    cultureInfo = new CultureInfo(savedLang);
                }

                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ENV] Set culture failed: {ex}");
            }
        }

        private static void ApplyBackgroundImage(System.Windows.Application application, string? backgroundImagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(backgroundImagePath))
                {
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(backgroundImagePath, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                application.Resources["BackgroundImage"] = bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP ENV] Failed to load BackgroundImage '{backgroundImagePath}': {ex.Message}");
                application.Resources["BackgroundImage"] = null;
            }
        }
    }
}
