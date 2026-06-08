#nullable disable

using Edemly;
using Edemly.Client.Infrastructure.Storage;
using System.Windows;
using System.Windows.Media;
namespace Edemly.Client.Application.Theme
{
    public class ThemeService
    {
        private static ThemeService _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        private string _currentTheme = "Default";
        public string CurrentTheme => _currentTheme;

        public event Action<string> ThemeChanged;

        private static readonly Dictionary<string, ThemePalette> ThemePalettes = new()
        {
            {
                "Default", new ThemePalette
                {
                    Name = "Default",
                    Primary = Color.FromRgb(0x05, 0x72, 0x72),
                    PrimaryLight = Color.FromRgb(0x00, 0xB8, 0xB8),
                    Secondary = Color.FromRgb(0x0B, 0x45, 0x39),
                    Background = Color.FromRgb(0xF6, 0xFF, 0xFC),
                    BackgroundDark = Color.FromRgb(0x00, 0x40, 0x40),
                    TextPrimary = Color.FromRgb(0x03, 0x1C, 0x1C),
                    TextSecondary = Color.FromRgb(0x66, 0x66, 0x66),
                    Border = Color.FromRgb(0xE0, 0xE0, 0xE0),
                    BorderLight = Color.FromRgb(0xC3, 0xF0, 0xE1)
                }
            },
            {
                "Blue", new ThemePalette
                {
                    Name = "Blue",
                    Primary = Color.FromRgb(0x0D, 0x48, 0x9D),
                    PrimaryLight = Color.FromRgb(0x42, 0xA5, 0xF5),
                    Secondary = Color.FromRgb(0x1A, 0x23, 0x7E),
                    Background = Color.FromRgb(0xF3, 0xF5, 0xFD),
                    BackgroundDark = Color.FromRgb(0x00, 0x0E, 0x2E),
                    TextPrimary = Color.FromRgb(0x0D, 0x1B, 0x4E),
                    TextSecondary = Color.FromRgb(0x55, 0x66, 0xCC),
                    Border = Color.FromRgb(0xBB, 0xDE, 0xFB),
                    BorderLight = Color.FromRgb(0xC5, 0xCA, 0xE9)
                }
            },
            {
                "Pink", new ThemePalette
                {
                    Name = "Pink",
                    Primary = Color.FromRgb(0xFF, 0x88, 0xB1),
                    PrimaryLight = Color.FromRgb(0xFF, 0xB6, 0xC1),
                    Secondary = Color.FromRgb(0x6F, 0x00, 0x27),
                    Background = Color.FromRgb(0xF8, 0xE8, 0xEE),
                    BackgroundDark = Color.FromRgb(0x4A, 0x00, 0x1A),
                    TextPrimary = Color.FromRgb(0x6F, 0x00, 0x27),
                    TextSecondary = Color.FromRgb(0xB8, 0x5C, 0x7A),
                    Border = Color.FromRgb(0xF5, 0xC6, 0xD6),
                    BorderLight = Color.FromRgb(0xFC, 0xE4, 0xEC)
                }
            },
            {
                "Orange", new ThemePalette
                {
                    Name = "Orange",
                    Primary = Color.FromRgb(0xFF, 0x66, 0x00),
                    PrimaryLight = Color.FromRgb(0xFF, 0x98, 0x00),
                    Secondary = Color.FromRgb(0x73, 0x31, 0x06),
                    Background = Color.FromRgb(0xF7, 0xEB, 0xE3),
                    BackgroundDark = Color.FromRgb(0x4E, 0x21, 0x04),
                    TextPrimary = Color.FromRgb(0x73, 0x31, 0x06),
                    TextSecondary = Color.FromRgb(0xBF, 0x66, 0x00),
                    Border = Color.FromRgb(0xFF, 0xCC, 0x80),
                    BorderLight = Color.FromRgb(0xFF, 0xE0, 0xB2)
                }
            },
            {
                "Purple", new ThemePalette
                {
                    Name = "Purple",
                    Primary = Color.FromRgb(0xD2, 0x91, 0xFF),
                    PrimaryLight = Color.FromRgb(0xE1, 0xBE, 0xE7),
                    Secondary = Color.FromRgb(0x55, 0x00, 0x91),
                    Background = Color.FromRgb(0xF2, 0xE8, 0xF9),
                    BackgroundDark = Color.FromRgb(0x38, 0x00, 0x61),
                    TextPrimary = Color.FromRgb(0x55, 0x00, 0x91),
                    TextSecondary = Color.FromRgb(0x9C, 0x27, 0xB0),
                    Border = Color.FromRgb(0xCE, 0x93, 0xD8),
                    BorderLight = Color.FromRgb(0xE1, 0xBE, 0xE7)
                }
            },
            {
                "Red", new ThemePalette
                {
                    Name = "Red",
                    Primary = Color.FromRgb(0xE4, 0x39, 0x26),
                    PrimaryLight = Color.FromRgb(0xEF, 0x5B, 0x5B),
                    Secondary = Color.FromRgb(0x54, 0x09, 0x01),
                    Background = Color.FromRgb(0xFF, 0xED, 0xEC),
                    BackgroundDark = Color.FromRgb(0x3B, 0x06, 0x01),
                    TextPrimary = Color.FromRgb(0x54, 0x09, 0x01),
                    TextSecondary = Color.FromRgb(0xC6, 0x28, 0x28),
                    Border = Color.FromRgb(0xEF, 0x9A, 0x9A),
                    BorderLight = Color.FromRgb(0xFF, 0xCD, 0xD2)
                }
            }
        };

        public ThemeService()
        {
            var savedTheme = ConfigService.Instance?.Theme ?? "Default";
            if (!ThemePalettes.ContainsKey(savedTheme))
                savedTheme = "Default";

            _currentTheme = savedTheme;
        }

        public ThemePalette GetCurrentPalette() => ThemePalettes[_currentTheme];

        public ThemePalette GetPalette(string themeName)
        {
            return ThemePalettes.TryGetValue(themeName, out var palette)
                ? palette
                : ThemePalettes["Default"];
        }

        public IEnumerable<string> GetAvailableThemes() => ThemePalettes.Keys;

        public void SetTheme(string themeName)
        {
            if (!ThemePalettes.ContainsKey(themeName))
                return;

            _currentTheme = themeName;

            ConfigService.Instance.Theme = themeName;
            ConfigService.Instance.Save();

            ApplyThemeToApplication();

            ThemeChanged?.Invoke(themeName);
        }

        private void ApplyThemeToApplication()
        {
            try
            {
                var palette = GetCurrentPalette();
                var app = System.Windows.Application.Current;

                if (app?.Resources == null) return;

                var surfaceColor = Blend(palette.Background, palette.BorderLight, 0.32);
                var surfaceAltColor = Blend(palette.Background, palette.BorderLight, 0.62);
                var inputBackgroundColor = Blend(surfaceColor, Colors.White, 0.18);
                var overlayColor = WithAlpha(palette.BackgroundDark, 0x99);
                var onPrimaryTextColor = Colors.White;
                var onSecondaryTextColor = Colors.White;
                var disabledTextColor = Blend(palette.TextSecondary, palette.Background, 0.18);
                var dangerColor = Color.FromRgb(0xE5, 0x39, 0x35);
                var successColor = Color.FromRgb(0x2E, 0x7D, 0x32);
                var warningColor = Color.FromRgb(0xF5, 0x9E, 0x0B);
                var infoColor = Color.FromRgb(0x21, 0x96, 0xF3);
                var onlineColor = Color.FromRgb(0x22, 0xC5, 0x5E);

                app.Resources["ThemePrimaryColor"] = palette.Primary;
                app.Resources["ThemePrimaryLightColor"] = palette.PrimaryLight;
                app.Resources["ThemeSecondaryColor"] = palette.Secondary;
                app.Resources["ThemeBackgroundColor"] = palette.Background;
                app.Resources["ThemeBackgroundDarkColor"] = palette.BackgroundDark;
                app.Resources["ThemeTextPrimaryColor"] = palette.TextPrimary;
                app.Resources["ThemeTextSecondaryColor"] = palette.TextSecondary;
                app.Resources["ThemeBorderColor"] = palette.Border;
                app.Resources["ThemeBorderLightColor"] = palette.BorderLight;
                app.Resources["ThemeSurfaceColor"] = surfaceColor;
                app.Resources["ThemeSurfaceAltColor"] = surfaceAltColor;
                app.Resources["ThemeInputBackgroundColor"] = inputBackgroundColor;
                app.Resources["ThemeOverlayColor"] = overlayColor;
                app.Resources["ThemeOnPrimaryTextColor"] = onPrimaryTextColor;
                app.Resources["ThemeOnSecondaryTextColor"] = onSecondaryTextColor;
                app.Resources["ThemeDisabledTextColor"] = disabledTextColor;
                app.Resources["ThemeDangerColor"] = dangerColor;
                app.Resources["ThemeSuccessColor"] = successColor;
                app.Resources["ThemeWarningColor"] = warningColor;
                app.Resources["ThemeInfoColor"] = infoColor;
                app.Resources["ThemeOnlineColor"] = onlineColor;

                app.Resources["ThemePrimaryBrush"] = CreateBrush(palette.Primary);
                app.Resources["ThemePrimaryLightBrush"] = CreateBrush(palette.PrimaryLight);
                app.Resources["ThemeSecondaryBrush"] = CreateBrush(palette.Secondary);
                app.Resources["ThemeBackgroundBrush"] = CreateBrush(palette.Background);
                app.Resources["ThemeBackgroundDarkBrush"] = CreateBrush(palette.BackgroundDark);
                app.Resources["ThemeTextPrimaryBrush"] = CreateBrush(palette.TextPrimary);
                app.Resources["ThemeTextSecondaryBrush"] = CreateBrush(palette.TextSecondary);
                app.Resources["ThemeBorderBrush"] = CreateBrush(palette.Border);
                app.Resources["ThemeBorderLightBrush"] = CreateBrush(palette.BorderLight);
                app.Resources["ThemeSurfaceBrush"] = CreateBrush(surfaceColor);
                app.Resources["ThemeSurfaceAltBrush"] = CreateBrush(surfaceAltColor);
                app.Resources["ThemeInputBackgroundBrush"] = CreateBrush(inputBackgroundColor);
                app.Resources["ThemeOverlayBrush"] = CreateBrush(overlayColor);
                app.Resources["ThemeOnPrimaryTextBrush"] = CreateBrush(onPrimaryTextColor);
                app.Resources["ThemeOnSecondaryTextBrush"] = CreateBrush(onSecondaryTextColor);
                app.Resources["ThemeDisabledTextBrush"] = CreateBrush(disabledTextColor);
                app.Resources["ThemeDangerBrush"] = CreateBrush(dangerColor);
                app.Resources["ThemeSuccessBrush"] = CreateBrush(successColor);
                app.Resources["ThemeWarningBrush"] = CreateBrush(warningColor);
                app.Resources["ThemeInfoBrush"] = CreateBrush(infoColor);
                app.Resources["ThemeOnlineBrush"] = CreateBrush(onlineColor);

                app.Resources["ThemeGradientBrush"] = CreateTwoStopGradientBrush(
                    palette.Primary,
                    0.0,
                    palette.BackgroundDark,
                    0.7,
                    new Point(1, 1),
                    new Point(0, 0));

                app.Resources["PageBackgroundBrush"] = CreateTwoStopGradientBrush(
                    palette.Primary,
                    0.0,
                    palette.BackgroundDark,
                    0.7,
                    new Point(1, 1),
                    new Point(0, 0));

                app.Resources["AuthPageBackgroundBrush"] = CreateGradientBrush(
                    palette.BackgroundDark,
                    palette.Primary,
                    new Point(0, 0),
                    new Point(1, 1),
                    palette.Secondary,
                    0.55);

                System.Diagnostics.Debug.WriteLine($"[THEME] Theme '{_currentTheme}' applied to application resources");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[THEME] Error applying theme: {ex.Message}");
            }
        }

        public void LoadAndApplyTheme()
        {
            var savedTheme = ConfigService.Instance?.Theme ?? "Default";
            if (!ThemePalettes.ContainsKey(savedTheme))
                savedTheme = "Default";

            _currentTheme = savedTheme;
            ApplyThemeToApplication();
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private static LinearGradientBrush CreateGradientBrush(
            Color startColor,
            Color endColor,
            Point startPoint,
            Point endPoint,
            Color? middleColor = null,
            double middleOffset = 0.5)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = startPoint,
                EndPoint = endPoint
            };

            brush.GradientStops.Add(new GradientStop(startColor, 0.0));

            if (middleColor.HasValue)
            {
                brush.GradientStops.Add(new GradientStop(middleColor.Value, middleOffset));
            }

            brush.GradientStops.Add(new GradientStop(endColor, 1.0));

            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private static LinearGradientBrush CreateTwoStopGradientBrush(
            Color firstColor,
            double firstOffset,
            Color secondColor,
            double secondOffset,
            Point startPoint,
            Point endPoint)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = startPoint,
                EndPoint = endPoint
            };

            brush.GradientStops.Add(new GradientStop(firstColor, firstOffset));
            brush.GradientStops.Add(new GradientStop(secondColor, secondOffset));

            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static Color Blend(Color first, Color second, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));

            return Color.FromRgb(
                (byte)Math.Round(first.R + ((second.R - first.R) * amount)),
                (byte)Math.Round(first.G + ((second.G - first.G) * amount)),
                (byte)Math.Round(first.B + ((second.B - first.B) * amount)));
        }
    }

    public class ThemePalette
    {
        public string Name { get; set; }
        public Color Primary { get; set; }
        public Color PrimaryLight { get; set; }
        public Color Secondary { get; set; }
        public Color Background { get; set; }
        public Color BackgroundDark { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color Border { get; set; }
        public Color BorderLight { get; set; }
    }
}
