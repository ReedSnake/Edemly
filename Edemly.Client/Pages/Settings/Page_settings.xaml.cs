#nullable disable

using Edemly.Client.Api;
using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Services;
using Edemly.Client.Presentation.Common;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings : ThemedPage
    {
        private readonly IApiService _apiService;
        private bool _hasUnsavedChanges = false;
        private string _originalName = string.Empty;
        private string _originalPhone = string.Empty;
        private string _originalAbout = string.Empty;
        private string _originalAvatarPath = string.Empty;
        private bool _isInitialized = false;

        public Page_settings()
        {
            InitializeComponent();
            _apiService = App.ApiService;
            this.Unloaded += Page_settings_Unloaded;

            try { App.HubService.ProfileUpdated += OnProfileUpdated; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Failed to subscribe ProfileUpdated: {ex}"); }
            try
            {
                AvatarImage.SizeChanged += AvatarImage_SizeChanged;
                AvatarBorder.SizeChanged += AvatarBorder_SizeChanged;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Failed to attach avatar size handlers: {ex}"); }

            try
            {
                var lang = LanguageService.Instance.CurrentLanguage;
                EnglishRadioButton.IsChecked = lang == "en";
                UkrainianRadioButton.IsChecked = lang == "uk";

                EnglishRadioButton.Content = DefaultLanguage.LanguageEnglishName;
                UkrainianRadioButton.Content = DefaultLanguage.LanguageUkrainianName;

                var selectLabel = this.FindName("SelectLanguageLabel") as TextBlock;
                if (selectLabel != null) selectLabel.Text = DefaultLanguage.SelectLanguageLabel;

                var themeLabel = this.FindName("ThemeSettingsLabel") as TextBlock;
                if (themeLabel != null) themeLabel.Text = DefaultLanguage.ThemeSettings;

                var themeColorLabel = this.FindName("ThemeColorLabel") as TextBlock;
                if (themeColorLabel != null) themeColorLabel.Text = DefaultLanguage.ThemeColor;

                var changePhotoBtn = this.FindName("ChangePhotoButton") as Button;
                if (changePhotoBtn != null) changePhotoBtn.Content = DefaultLanguage.ChangePhoto;

                var saveBtn = this.FindName("SaveButton") as Button;
                if (saveBtn != null) saveBtn.Content = DefaultLanguage.SaveButton;

                UpdateThemePreviewSelection(ThemeService.Instance.CurrentTheme);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Init UI failed: {ex}"); }

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

        private async void OnProfileUpdated(int userId, string newPfpUrl)
        {
            try
            {
                if (App.CurrentUserId.HasValue && userId == App.CurrentUserId.Value)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(newPfpUrl))
                            {
                                App.GlobalProfilePictureCache.InvalidateCache(newPfpUrl);
                                var bmp = await App.GlobalProfilePictureCache.ForceDownloadAsync(newPfpUrl);
                                if (bmp != null)
                                {
                                    AvatarImage.Source = bmp;
                                    AvatarImage.Opacity = 1;
                                    AvatarInitials.Visibility = Visibility.Collapsed;
                                    UpdateAvatarClip();
                                }
                            }

                            _originalAvatarPath = newPfpUrl ?? string.Empty;
                            App.CurrentUserPhotoUrl = newPfpUrl;
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] OnProfileUpdated inner failed: {ex}"); }
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] OnProfileUpdated failed: {ex}"); }
        }

        private void Page_settings_Unloaded(object sender, RoutedEventArgs e)
        {
            try { App.HubService.ProfileUpdated -= OnProfileUpdated; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Unsubscribe failed: {ex}"); }
        }

        private async Task LoadUserDataAsync()
        {
            await Task.CompletedTask;
            if (_isInitialized) return;
            _isInitialized = true;

            try
            {
                if (_apiService == null) return;

                var userInfo = await _apiService.GetUserInfoAsync();
                if (userInfo == null) return;

                var fullName = (userInfo.FirstName + " " + userInfo.LastName).Trim();
                NameTextBox.Text = fullName;
                UsernameTextBox.Text = userInfo.Username ?? string.Empty;
                EmailTextBox.Text = userInfo.Email ?? string.Empty;
                PhoneNumberTextBox.Text = userInfo.PhoneNumber ?? string.Empty;

                try
                {
                    var aboutTb = this.FindName("AboutTextBox") as TextBox;
                    if (aboutTb != null)
                    {
                        aboutTb.Text = userInfo.Description ?? string.Empty;
                        _originalAbout = aboutTb.Text.Trim();
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Setting about text failed: {ex}"); }

                _originalName = NameTextBox.Text.Trim();
                _originalPhone = PhoneNumberTextBox.Text.Trim();

                _originalAvatarPath = userInfo.PfpUrl ?? string.Empty;

                if (!string.IsNullOrEmpty(_originalAvatarPath))
                    await LoadAvatarFromUrlAsync(_originalAvatarPath);
                else
                    ShowInitials();

                CheckForChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] LoadUserDataAsync error: {ex.Message}");
            }
        }

        private async Task LoadAvatarFromUrlAsync(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) { ShowInitials(); return; }

                var bmp = await App.GlobalProfilePictureCache.GetOrDownloadAsync(url);
                if (bmp != null)
                {
                    AvatarImage.Source = bmp;
                    AvatarImage.Opacity = 1;
                    AvatarInitials.Visibility = Visibility.Collapsed;
                    UpdateAvatarClip();
                }
                else
                {
                    ShowInitials();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] LoadAvatarFromUrlAsync error: {ex.Message}");
                ShowInitials();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService?.CanGoBack == true)
                this.NavigationService.GoBack();
        }

        private void ShowInitials()
        {
            try
            {
                AvatarImage.Source = null;
                AvatarImage.Opacity = 0;
                AvatarImage.Clip = null;
                var txt = NameTextBox.Text ?? string.Empty;
                var initials = "";
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    var parts = txt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    initials = parts.Length >= 2 ? (parts[0][0].ToString() + parts[1][0].ToString()) : txt.Substring(0, Math.Min(2, txt.Length));
                    initials = initials.ToUpper();
                }
                else if (!string.IsNullOrEmpty(UsernameTextBox.Text))
                {
                    initials = UsernameTextBox.Text.Substring(0, Math.Min(2, UsernameTextBox.Text.Length)).ToUpper();
                }

                AvatarInitials.Text = initials;
                AvatarInitials.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ShowInitials failed: {ex}"); }
        }

        private async void ChangePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            await ChangePhotoAsync();
        }

        private async Task ChangePhotoAsync()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = DefaultLanguage.SelectProfilePhotoTitle,
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                    Multiselect = false
                };

                if (dlg.ShowDialog() != true) return;

                var file = dlg.FileName;
                if (!File.Exists(file)) return;

                var localBmp = await App.GlobalProfilePictureCache.CacheLocalFileAsync(file);
                if (localBmp != null)
                {
                    AvatarImage.Source = localBmp;
                    AvatarImage.Opacity = 1;
                    AvatarInitials.Visibility = Visibility.Collapsed;
                    UpdateAvatarClip();
                }

                ChangePhotoButton.IsEnabled = false;
                ChangePhotoButton.Content = DefaultLanguage.Uploading;

                var upload = await _apiService.UploadProfilePictureAsync(file);

                if (!upload.Success || string.IsNullOrEmpty(upload.Url))
                {
                    MessageBox.ShowError(string.Format(DefaultLanguage.PhotoUploadFailed, upload.Error), DefaultLanguage.ErrorTitle);
                    return;
                }

                var newUrl = upload.Url;

                var phone = PhoneNumberTextBox.Text?.Trim();
                var aboutTb = this.FindName("AboutTextBox") as TextBox;
                var about = aboutTb?.Text?.Trim();
                var name = NameTextBox.Text?.Trim();

                bool updated = false;
                try { updated = await _apiService.UpdateUserInfoAsync(phone, about, newUrl, name); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateUserInfoAsync failed: {ex}"); }

                if (!updated)
                    MessageBox.ShowWarning(DefaultLanguage.PhotoUploadedButUpdateFailed, DefaultLanguage.WarningTitle);

                try { App.GlobalProfilePictureCache.InvalidateCache(_originalAvatarPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] InvalidateCache failed: {ex}"); }
                try { await App.GlobalProfilePictureCache.ForceDownloadAsync(newUrl); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ForceDownloadAsync failed: {ex}"); }

                _originalAvatarPath = newUrl;
                App.CurrentUserPhotoUrl = newUrl;

                await LoadAvatarFromUrlAsync(newUrl);

                try
                {
                    var currentUserId = App.CurrentUserId ?? 0;
                    if (currentUserId > 0) await App.HubService.NotifyProfileUpdateAsync(currentUserId, newUrl);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] NotifyProfileUpdate failed: {ex}"); }

                MessageBox.Show(DefaultLanguage.ProfilePhotoUpdated, DefaultLanguage.SuccessTitle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] ChangePhotoAsync error: {ex.Message}");
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorSavingSettings, ex.Message), DefaultLanguage.ErrorTitle);
            }
            finally
            {
                ChangePhotoButton.IsEnabled = true;
                ChangePhotoButton.Content = DefaultLanguage.ChangePhoto;
            }
        }

        private void AvatarBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _ = ChangePhotoAsync();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveUserDataAsync();
        }

        private async Task SaveUserDataAsync()
        {
            try
            {
                var name = NameTextBox.Text?.Trim();
                var phone = PhoneNumberTextBox.Text?.Trim();
                var aboutTb = this.FindName("AboutTextBox") as TextBox;
                var about = aboutTb?.Text?.Trim();

                if (!IsValidPhone(phone))
                {
                    MessageBox.ShowWarning(DefaultLanguage.PleaseEnterValidPhone, DefaultLanguage.WarningTitle);
                    return;
                }

                bool success = await _apiService.UpdateUserInfoAsync(phone, about, _originalAvatarPath, name);

                if (!success)
                {
                    MessageBox.ShowError(DefaultLanguage.FailedSaveUserSettings, DefaultLanguage.ErrorTitle);
                    return;
                }

                _originalName = name ?? string.Empty;
                _originalPhone = phone ?? string.Empty;
                _originalAbout = about ?? string.Empty;

                var saveBtn = this.FindName("SaveButton") as Button;
                if (saveBtn != null) saveBtn.Visibility = Visibility.Collapsed;

                MessageBox.Show(DefaultLanguage.SettingsSaved, DefaultLanguage.SuccessTitle);

                App.CurrentUserName = name;

                try
                {
                    var currentUserId = App.CurrentUserId ?? 0;
                    if (currentUserId > 0 && !string.IsNullOrEmpty(_originalAvatarPath))
                        await App.HubService.NotifyProfileUpdateAsync(currentUserId, _originalAvatarPath);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] NotifyProfileUpdate failed in Save: {ex}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] SaveUserDataAsync error: {ex.Message}");
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorSavingSettings, ex.Message), DefaultLanguage.ErrorTitle);
            }
        }

        private bool IsValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length >= 2;

        private bool IsValidPhone(string phone)
        { if (string.IsNullOrWhiteSpace(phone)) return true; return Regex.IsMatch(phone, @"^\+?[0-9\s\-\(\)]+$"); }

        private void PhoneNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) => CheckForChanges();

        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        { UpdateInitials(); CheckForChanges(); }

        private void AboutTextBox_TextChanged(object sender, TextChangedEventArgs e) => CheckForChanges();

        private void CheckForChanges()
        {
            try
            {
                var name = (this.FindName("NameTextBox") as TextBox)?.Text?.Trim() ?? string.Empty;
                var phone = (this.FindName("PhoneNumberTextBox") as TextBox)?.Text?.Trim() ?? string.Empty;
                var about = (this.FindName("AboutTextBox") as TextBox)?.Text?.Trim() ?? string.Empty;

                _hasUnsavedChanges = name != _originalName || phone != _originalPhone || about != _originalAbout;

                var saveBtn = this.FindName("SaveButton") as Button;
                if (saveBtn != null) saveBtn.Visibility = _hasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] CheckForChanges failed: {ex}"); }
        }

        private void UpdateInitials()
        {
            try
            {
                var txt = (this.FindName("NameTextBox") as TextBox)?.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    var parts = txt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var initials = parts.Length >= 2 ? (parts[0][0].ToString() + parts[1][0].ToString()) : txt.Substring(0, Math.Min(2, txt.Length));
                    var initialsBlock = this.FindName("AvatarInitials") as TextBlock;
                    if (initialsBlock != null) initialsBlock.Text = initials.ToUpper();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateInitials failed: {ex}"); }
        }

        private void EnglishRadioButton_Checked(object sender, RoutedEventArgs e) => ChangeLanguage("en");

        private void UkrainianRadioButton_Checked(object sender, RoutedEventArgs e) => ChangeLanguage("uk");

        private void ChangeLanguage(string languageCode)
        {
            try
            {
                ConfigService.Instance.Language = languageCode;
                ConfigService.Instance.Save();

                try
                {
                    CultureInfo culture = languageCode == "uk" ? new CultureInfo("uk-UA") : new CultureInfo("en-US");
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Set culture failed: {ex}"); }

                LanguageService.Instance.LoadLanguage(languageCode);

                EnglishRadioButton.Content = DefaultLanguage.LanguageEnglishName;
                UkrainianRadioButton.Content = DefaultLanguage.LanguageUkrainianName;

                var selectLabel = this.FindName("SelectLanguageLabel") as TextBlock;
                if (selectLabel != null) selectLabel.Text = DefaultLanguage.SelectLanguageLabel;

                var themeLabel = this.FindName("ThemeSettingsLabel") as TextBlock;
                if (themeLabel != null) themeLabel.Text = DefaultLanguage.ThemeSettings;

                var themeColorLabel = this.FindName("ThemeColorLabel") as TextBlock;
                if (themeColorLabel != null) themeColorLabel.Text = DefaultLanguage.ThemeColor;

                var changePhotoBtn = this.FindName("ChangePhotoButton") as Button;
                if (changePhotoBtn != null) changePhotoBtn.Content = DefaultLanguage.ChangePhoto;

                var saveBtn = this.FindName("SaveButton") as Button;
                if (saveBtn != null) saveBtn.Content = DefaultLanguage.SaveButton;

                var nav = this.NavigationService;
                if (nav != null)
                {
                    nav.Navigate(new Page_settings());
                }
                else
                {
                    System.Windows.Application.Current.MainWindow.Title = DefaultLanguage.AppTitle;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ChangeLanguage failed: {ex}"); }
        }

        private void ThemeColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var themeName = element.Tag as string;

            if (string.IsNullOrWhiteSpace(themeName))
            {
                return;
            }

            ChangeTheme(themeName);
        }

        private void ChangeTheme(string themeName)
        {
            try
            {
                ThemeService.Instance.SetTheme(themeName);

                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Theme changed to: {themeName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] ChangeTheme error: {ex}");
            }
        }

        private void UpdateThemePreviewSelection(string activeTheme)
        {
            try
            {
                var themePreviews = new[]
                {
                    (Preview: DefaultThemeColor, ThemeName: "Default"),
                    (Preview: BlueThemeColor, ThemeName: "Blue"),
                    (Preview: PinkThemeColor, ThemeName: "Pink"),
                    (Preview: OrangeThemeColor, ThemeName: "Orange"),
                    (Preview: PurpleThemeColor, ThemeName: "Purple"),
                    (Preview: RedThemeColor, ThemeName: "Red")
                };

                foreach (var (preview, themeName) in themePreviews)
                {
                    if (preview == null)
                    {
                        continue;
                    }

                    bool isActive = string.Equals(activeTheme, themeName, StringComparison.OrdinalIgnoreCase);
                    preview.BorderThickness = isActive ? new Thickness(3) : new Thickness(1);
                    preview.SetResourceReference(Border.BorderBrushProperty, isActive ? "ThemePrimaryBrush" : "ThemeBorderBrush");
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateThemePreviewSelection failed: {ex}"); }
        }

        private void SetAppBackgroundImage(string packUriOrNull)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packUriOrNull))
                {
                    System.Windows.Application.Current.Resources["BackgroundImage"] = null;
                    try { ConfigService.Instance.BackgroundImagePath = string.Empty; } catch { }
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(packUriOrNull, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                System.Windows.Application.Current.Resources["BackgroundImage"] = bmp;

                try
                {
                    ConfigService.Instance.BackgroundImagePath = packUriOrNull;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Set background image path failed: {ex}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] SetAppBackgroundImage error: {ex.Message}");
                System.Windows.Application.Current.Resources["BackgroundImage"] = null;
                try { ConfigService.Instance.BackgroundImagePath = string.Empty; } catch { }
            }
        }

        private void Wallpaper_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var backgroundPath = element.Tag as string;

            SetAppBackgroundImage(backgroundPath);
        }


        private static readonly Regex PhoneInputRegex = new(@"^[0-9+\-\s()]+$");

        private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !PhoneInputRegex.IsMatch(e.Text);
        }

        private void PhoneNumberTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = false;
            }
        }

        private void PhoneNumberTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;

            if (string.IsNullOrWhiteSpace(text) || !PhoneInputRegex.IsMatch(text))
            {
                e.CancelCommand();
            }
        }

        private void WallpapersScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void AvatarImage_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateAvatarClip();

        private void AvatarBorder_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateAvatarClip();

        private void UpdateAvatarClip()
        {
            try
            {
                if (AvatarImage == null || AvatarBorder == null) return;

                double w = AvatarBorder.ActualWidth;
                double h = AvatarBorder.ActualHeight;
                if (w <= 0 || h <= 0) return;

                var center = new Point(w / 2.0, h / 2.0);
                var radiusX = w / 2.0;
                var radiusY = h / 2.0;

                AvatarImage.Clip = new EllipseGeometry(center, radiusX, radiusY);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateAvatarClip failed: {ex}"); }
        }
    }
}
