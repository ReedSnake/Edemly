#nullable enable

using Edemly.Client.Api;
using Edemly.Client.Application.Users.Profile;
using Edemly.Client.Application.Services;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings : ThemedPage
    {
        private readonly IApiService _apiService;
        private readonly SettingsProfileEditorState _profileState = new();

        public Page_settings()
        {
            InitializeComponent();
            _apiService = App.ApiService;
            Unloaded += Page_settings_Unloaded;

            try { App.HubService.ProfileUpdated += OnProfileUpdated; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Failed to subscribe ProfileUpdated: {ex}"); }
            try
            {
                AvatarImage.SizeChanged += AvatarImage_SizeChanged;
                AvatarBorder.SizeChanged += AvatarBorder_SizeChanged;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Failed to attach avatar size handlers: {ex}"); }

            InitializeLanguageControls();
            UpdateThemePreviewSelection(ThemeService.Instance.CurrentTheme);
            _ = LoadUserDataAsync();
        }

        protected override void ApplyTheme()
        {
            try
            {
                MainPageGrid?.SetResourceReference(Panel.BackgroundProperty, "PageBackgroundBrush");
                UpdateThemePreviewSelection(ThemeService.Instance.CurrentTheme);
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ApplyTheme error: {ex}"); }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
        }

        private void Page_settings_Unloaded(object sender, RoutedEventArgs e)
        {
            try { App.HubService.ProfileUpdated -= OnProfileUpdated; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Unsubscribe failed: {ex}"); }
        }
    }
}
