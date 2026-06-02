#nullable disable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Edemly.Client.Services
{
    /// <summary>
    /// Сервіс для управління темами додатку
    /// </summary>
    public class ThemeService
    {
        private static ThemeService _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        private string _currentTheme = "Default";
        public string CurrentTheme => _currentTheme;

        public event Action<string> ThemeChanged;

        /// <summary>
        /// Визначення палітри кольорів для кожної теми
        /// </summary>
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

        /// <summary>
        /// Отримати палітру для поточної теми
        /// </summary>
        public ThemePalette GetCurrentPalette() => ThemePalettes[_currentTheme];

        /// <summary>
        /// Отримати палітру для конкретної теми
        /// </summary>
        public ThemePalette GetPalette(string themeName)
        {
            return ThemePalettes.TryGetValue(themeName, out var palette) 
                ? palette 
                : ThemePalettes["Default"];
        }

        /// <summary>
        /// Отримати список доступних тем
        /// </summary>
        public IEnumerable<string> GetAvailableThemes() => ThemePalettes.Keys;

        /// <summary>
        /// Переключитися на нову тему і застосувати її глобально
        /// </summary>
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

        /// <summary>
        /// Застосувати тему до глобальних ресурсів додатку
        /// </summary>
        private void ApplyThemeToApplication()
        {
            try
            {
                var palette = GetCurrentPalette();
                var app = Application.Current;

                if (app?.Resources == null) return;

                app.Resources["ThemePrimaryBrush"] = new SolidColorBrush(palette.Primary);
                app.Resources["ThemePrimaryLightBrush"] = new SolidColorBrush(palette.PrimaryLight);
                app.Resources["ThemeSecondaryBrush"] = new SolidColorBrush(palette.Secondary);
                app.Resources["ThemeBackgroundBrush"] = new SolidColorBrush(palette.Background);
                app.Resources["ThemeBackgroundDarkBrush"] = new SolidColorBrush(palette.BackgroundDark);
                app.Resources["ThemeTextPrimaryBrush"] = new SolidColorBrush(palette.TextPrimary);
                app.Resources["ThemeTextSecondaryBrush"] = new SolidColorBrush(palette.TextSecondary);
                app.Resources["ThemeBorderBrush"] = new SolidColorBrush(palette.Border);
                app.Resources["ThemeBorderLightBrush"] = new SolidColorBrush(palette.BorderLight);

                var gradientBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(1, 1),
                    EndPoint = new Point(0, 0)
                };
                gradientBrush.GradientStops.Add(new GradientStop(palette.BackgroundDark, 0.7));
                gradientBrush.GradientStops.Add(new GradientStop(palette.Primary, 0.0));
                app.Resources["ThemeGradientBrush"] = gradientBrush;

                System.Diagnostics.Debug.WriteLine($"[THEME] Theme '{_currentTheme}' applied to application resources");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[THEME] Error applying theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузити та застосувати тему при запуску
        /// </summary>
        public void LoadAndApplyTheme()
        {
            var savedTheme = ConfigService.Instance?.Theme ?? "Default";
            if (!ThemePalettes.ContainsKey(savedTheme))
                savedTheme = "Default";

            _currentTheme = savedTheme;
            ApplyThemeToApplication();
        }
    }

    /// <summary>
    /// Палітра кольорів теми
    /// </summary>
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
