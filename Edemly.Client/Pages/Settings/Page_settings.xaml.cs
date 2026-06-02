#nullable disable
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Edemly.Client.Lang;
using Edemly.Client.Pages;
using Edemly.Client.Services;
using MessageBox = Edemly.Client.Pages.MessageBox;
using Edemly.Client.Api;

namespace Edemly.Client
{
    public partial class Page_settings : Page
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

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

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

                var currentTheme = ThemeService.Instance.CurrentTheme;
                UpdateThemeButtonsStyle(currentTheme);

                ApplyThemeToPage();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Init UI failed: {ex}"); }

            _ = LoadUserDataAsync();
        }

        private void OnThemeChanged()
        {
            try
            {
                var currentTheme = ThemeService.Instance.CurrentTheme;
                UpdateThemeButtonsStyle(currentTheme);
                ApplyThemeToPage();
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Theme changed to: {currentTheme}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] OnThemeChanged error: {ex}"); }
        }

        private void ApplyThemeToPage()
        {
            try
            {
                var palette = ThemeService.Instance.GetCurrentPalette();

                var grid = this.FindName("MainPageGrid") as Grid;
                if (grid != null)
                {
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(1, 1),
                        EndPoint = new Point(0, 0)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop(palette.BackgroundDark, 0.7));
                    gradientBrush.GradientStops.Add(new GradientStop(palette.Primary, 0.0));
                    grid.Background = gradientBrush;
                }

                if (AvatarBorder != null)
                {
                    AvatarBorder.Background = new SolidColorBrush(palette.Primary);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ApplyThemeToPage error: {ex.Message}");
            }
        }

        private async void OnProfileUpdated(int userId, string newPfpUrl)
        {
            try
            {
                if (App.CurrentUserId.HasValue && userId == App.CurrentUserId.Value)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
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

                var userInfo = await _apiService.GetUserInfo();
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
                    await LoadAvatarFromUrl(_originalAvatarPath);
                else
                    ShowInitials();

                CheckForChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] LoadUserDataAsync error: {ex.Message}");
            }
        }

        private async Task LoadAvatarFromUrl(string url)
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
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] LoadAvatarFromUrl error: {ex.Message}");
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
                try { updated = await _apiService.UpdateUserInfo(phone, about, newUrl, name); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateUserInfo failed: {ex}"); }

                if (!updated)
                    MessageBox.ShowWarning(DefaultLanguage.PhotoUploadedButUpdateFailed, DefaultLanguage.WarningTitle);

                try { App.GlobalProfilePictureCache.InvalidateCache(_originalAvatarPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] InvalidateCache failed: {ex}"); }
                try { await App.GlobalProfilePictureCache.ForceDownloadAsync(newUrl); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ForceDownloadAsync failed: {ex}"); }

                _originalAvatarPath = newUrl;
                App.CurrentUserPhotoUrl = newUrl;

                await LoadAvatarFromUrl(newUrl);

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

                bool success = await _apiService.UpdateUserInfo(phone, about, _originalAvatarPath, name);

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
        private bool IsValidPhone(string phone) { if (string.IsNullOrWhiteSpace(phone)) return true; return Regex.IsMatch(phone, @"^\+?[0-9\s\-\(\)]+$"); }

        private void PhoneNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) => CheckForChanges();
        private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdateInitials(); CheckForChanges(); }
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
                    Application.Current.MainWindow.Title = DefaultLanguage.AppTitle;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ChangeLanguage failed: {ex}"); }
        }

        private void DefaultThemeColor_MouseDown(object sender, MouseButtonEventArgs e) => ChangeTheme("Default");
        private void BlueThemeColor_MouseDown(object sender, MouseButtonEventArgs e) => ChangeTheme("Blue");
        private void PinkThemeColor_MouseDown(object sender, MouseButtonEventArgs e) => ChangeTheme("Pink");
        private void OrangeThemeColor_MouseDown(object sender, MouseButtonEventArgs e) => ChangeTheme("Orange");
        private void PurpleThemeColor_MouseDown(object sender, MouseButtonEventArgs e) => ChangeTheme("Purple");
        private void RedThemeColor_MouseDown(object sender, MouseButtonEventArgs e) => ChangeTheme("Red");

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

        private void UpdateThemeButtonsStyle(string activeTheme)
        {
            try
            {
                var defaultBtn = this.FindName("DefaultThemeButton") as Button;
                var blueBtn = this.FindName("BlueThemeButton") as Button;
                var pinkBtn = this.FindName("PinkThemeButton") as Button;
                var orangeBtn = this.FindName("OrangeThemeButton") as Button;
                var purpleBtn = this.FindName("PurpleThemeButton") as Button;
                var redBtn = this.FindName("RedThemeButton") as Button;

                if (defaultBtn != null)
                {
                    if (activeTheme == "Default")
                    {
                        defaultBtn.Background = new SolidColorBrush(Color.FromRgb(0x05, 0x72, 0x72));
                        defaultBtn.Foreground = Brushes.White;
                        defaultBtn.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        defaultBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                        defaultBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x72, 0x72));
                        defaultBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x05, 0x72, 0x72));
                        defaultBtn.BorderThickness = new Thickness(2);
                    }
                }

                if (blueBtn != null)
                {
                    if (activeTheme == "Blue")
                    {
                        blueBtn.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x48, 0x9D));
                        blueBtn.Foreground = Brushes.White;
                        blueBtn.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        blueBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                        blueBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x48, 0x9D));
                        blueBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x0D, 0x48, 0x9D));
                        blueBtn.BorderThickness = new Thickness(2);
                    }
                }

                if (pinkBtn != null)
                {
                    if (activeTheme == "Pink")
                    {
                        pinkBtn.Background = new SolidColorBrush(Color.FromRgb(0x6F, 0x00, 0x27));
                        pinkBtn.Foreground = Brushes.White;
                        pinkBtn.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        pinkBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                        pinkBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x00, 0x27));
                        pinkBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x00, 0x27));
                        pinkBtn.BorderThickness = new Thickness(2);
                    }
                }

                if (orangeBtn != null)
                {
                    if (activeTheme == "Orange")
                    {
                        orangeBtn.Background = new SolidColorBrush(Color.FromRgb(0x73, 0x31, 0x06));
                        orangeBtn.Foreground = Brushes.White;
                        orangeBtn.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        orangeBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                        orangeBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x73, 0x31, 0x06));
                        orangeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x73, 0x31, 0x06));
                        orangeBtn.BorderThickness = new Thickness(2);
                    }
                }

                if (purpleBtn != null)
                {
                    if (activeTheme == "Purple")
                    {
                        purpleBtn.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x00, 0x91));
                        purpleBtn.Foreground = Brushes.White;
                        purpleBtn.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        purpleBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                        purpleBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x00, 0x91));
                        purpleBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x00, 0x91));
                        purpleBtn.BorderThickness = new Thickness(2);
                    }
                }

                if (redBtn != null)
                {
                    if (activeTheme == "Red")
                    {
                        redBtn.Background = new SolidColorBrush(Color.FromRgb(0x54, 0x09, 0x01));
                        redBtn.Foreground = Brushes.White;
                        redBtn.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        redBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                        redBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x54, 0x09, 0x01));
                        redBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x54, 0x09, 0x01));
                        redBtn.BorderThickness = new Thickness(2);
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateThemeButtonsStyle failed: {ex}"); }
        }

        private void SetAppBackgroundImage(string packUriOrNull)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packUriOrNull))
                {
                    Application.Current.Resources["BackgroundImage"] = null;
                    try { ConfigService.Instance.BackgroundImagePath = string.Empty; } catch { }
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(packUriOrNull, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                Application.Current.Resources["BackgroundImage"] = bmp;

                try
                {
                    ConfigService.Instance.BackgroundImagePath = packUriOrNull;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Set background image path failed: {ex}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] SetAppBackgroundImage error: {ex.Message}");
                Application.Current.Resources["BackgroundImage"] = null;
                try { ConfigService.Instance.BackgroundImagePath = string.Empty; } catch { }
            }
        }

        private void Wallpaper1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage(null);
        }
        private void Wallpaper2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-blue.png");
        }
        private void Wallpaper3_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-pink.png");
        }
        private void Wallpaper4_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-orange.png");
        }
        private void Wallpaper5_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-green.png");
        }
        private void Wallpaper6_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-black.png");
        }
        private void Wallpaper7_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-violet.png");
        }
        private void Wallpaper8_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SetAppBackgroundImage("pack://application:,,,/Assets/Backgrounds/profile-red.png");
        }

        private void Color1_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Color2_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Color3_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Color4_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Color5_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Color6_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Color7_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void SelectThemeColor(string colorHex) { }
        private void ResetColorSelection() { }

        private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) { }
        private void PhoneNumberTextBox_PreviewKeyDown(object sender, KeyEventArgs e) { }
        private void PhoneNumberTextBox_Pasting(object sender, DataObjectPastingEventArgs e) { }
        private void WallpapersScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift || e.Delta > 0)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                else
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
            }
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
