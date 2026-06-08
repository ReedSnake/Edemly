#nullable enable

using Edemly.Client.Api;
using Edemly.Client.Application.Services;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;
using Edemly.Client.Api.Users;  
namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class SettingsPage : ThemedPage
    {
        private readonly IApiClients _apiClient;
        private readonly SettingsProfileEditorState _profileState = new();

        public SettingsPage()
        {
            InitializeComponent();
            _apiClient = App.ApiClients;
            BackButton.Content = PageNavigationGlyphs.Back;
            Unloaded += SettingsPage_Unloaded;

            try { App.HubService.ProfileUpdated += OnProfileUpdated; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to subscribe ProfileUpdated: {ex}"); }
            try
            {
                AvatarImage.SizeChanged += AvatarImage_SizeChanged;
                AvatarBorder.SizeChanged += AvatarBorder_SizeChanged;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to attach avatar size handlers: {ex}"); }

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
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SettingsPage] ApplyTheme error: {ex}"); }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try { App.HubService.ProfileUpdated -= OnProfileUpdated; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SettingsPage] Unsubscribe failed: {ex}"); }
        }
    }
}
