using Edemly.Client.Lang;
using Edemly.Client.Realtime;
using Edemly.Client.Services;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Edemly.Client.Pages
{
    public partial class Page_install : Page
    {
        private const string ShortcutFileName = "Edemly.lnk";

        public Page_install()
        {
            InitializeComponent();

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            Loaded += Page_install_Loaded;
        }

        private void OnThemeChanged()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[PAGE_INSTALL] Theme changed");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] OnThemeChanged failed: {ex}"); }
        }

        private async void Page_install_Loaded(object sender, RoutedEventArgs e)
        {
            LanguageComboBox.Items.Clear();
            var enItem = new ComboBoxItem { Content = "English", Tag = "en" };
            var ukItem = new ComboBoxItem { Content = "Українська", Tag = "uk" };
            LanguageComboBox.Items.Add(enItem);
            LanguageComboBox.Items.Add(ukItem);

            var savedLang = ConfigService.Instance?.Language;
            string initial;
            if (!string.IsNullOrWhiteSpace(savedLang))
            {
                initial = savedLang;
            }
            else
            {
                var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? "en";
                initial = (sys == "uk" || sys == "ru") ? "uk" : "en";
            }

            LanguageComboBox.SelectedItem = LanguageComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Tag == initial);

            try
            {
                LanguageService.Instance.LoadLanguage(initial);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] LoadLanguage failed: {ex}"); }

            ApplyLanguage(initial);

            await LoadCompaniesAsync();
        }

        private void ApplyLanguage(string langTag)
        {
            TitleText.Text = DefaultLanguage.InstallTitle;
            DescriptionText.Text = DefaultLanguage.InstallDescription;

            LanguageLabelText.Text = DefaultLanguage.LanguageLabel;
            LanguageDescText.Text = DefaultLanguage.LanguageDesc;

            CompanyLabelText.Text = DefaultLanguage.CompanyLabel;
            CompanyDescText.Text = DefaultLanguage.CompanyDesc;

            DesktopShortcutLabel.Text = DefaultLanguage.DesktopShortcutLabel;

            NoteTextBlock.Text = DefaultLanguage.NoteInitial;

            CancelButton.Content = DefaultLanguage.CancelButton;
            ContinueButton.Content = DefaultLanguage.ContinueButton;

            UpdateCompanyPersonalLabel();
        }

        private void UpdateCompanyPersonalLabel()
        {
            try
            {
                if (CompanyComboBox == null) return;

                if (CompanyComboBox.Items.Count > 0)
                {
                    if (CompanyComboBox.Items[0] is ComboBoxItem first)
                    {
                        var personalLabel = DefaultLanguage.PersonalLabel;
                        var tag = first.Tag;
                        first.Content = personalLabel;
                        first.Tag = tag;
                        first.ToolTip = personalLabel;
                    }
                }
            }
            catch { }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                try
                {
                    ConfigService.Instance.Language = tag;
                    ConfigService.Instance.Save();
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Save config language failed: {ex}"); }

                try
                {
                    LanguageService.Instance.LoadLanguage(tag);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] LoadLanguage on selection failed: {ex}"); }

                ApplyLanguage(tag);

                UpdateCompanyPersonalLabel();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new Edemly.Client.Page_login());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Cancel navigation failed: {ex}");
                try { if (NavigationService?.CanGoBack == true) NavigationService.GoBack(); } catch (Exception ex2) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] GoBack failed: {ex2}"); }
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sel = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "en";

                try
                {
                    ConfigService.Instance.Language = sel;
                    ConfigService.Instance.Save();
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Save language on continue failed: {ex}"); }

                try
                {
                    LanguageService.Instance.LoadLanguage(sel);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] LoadLanguage on continue failed: {ex}");
                    try { ConfigService.Instance.Language = sel; } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Save fallback language failed: {innerEx}"); }
                }

                ApplyLanguage(sel);

                var selectedCompanyTag = (CompanyComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "personal";
                if (string.Equals(selectedCompanyTag, "personal", StringComparison.OrdinalIgnoreCase))
                    selectedCompanyTag = string.Empty;

                App.SetCompanyAndApply(selectedCompanyTag, markInstalled: true);

                var baseUrl = App.BaseServerUrlNoCompany?.TrimEnd('/') ?? string.Empty;
                string shortcutArg = string.IsNullOrEmpty(baseUrl) ? string.Empty :
                    (string.IsNullOrEmpty(selectedCompanyTag) ? baseUrl : $"{baseUrl}/{selectedCompanyTag.Trim().Trim('/')}");

                if (DesktopShortcutCheckBox.IsChecked == true)
                {
                    ConfigService.Instance.CreateDesktopShortcut = true;
                    var created = CreateOrReplaceDesktopShortcut(shortcutArg);
                    if (!created)
                    {
                        MessageBox.Show(DefaultLanguage.ShortcutCreateFailed, "Edemly", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    ConfigService.Instance.CreateDesktopShortcut = false;
                }

                await Task.Delay(80);
                NavigationService?.Navigate(new Edemly.Client.Page_login());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Edemly", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCompaniesAsync()
        {
            try
            {
                CompanyComboBox.IsEnabled = false;
                CompanyComboBox.Items.Clear();

                var personalLabel = DefaultLanguage.PersonalLabel;
                var personalItem = new ComboBoxItem { Content = personalLabel, Tag = "personal" };
                CompanyComboBox.Items.Add(personalItem);

                NoteTextBlock.Text = DefaultLanguage.LoadingCompanies;

                var baseUrl = App.BaseServerUrlNoCompany?.TrimEnd('/');
                if (string.IsNullOrEmpty(baseUrl))
                {
                    CompanyComboBox.SelectedIndex = 0;
                    NoteTextBlock.Text = DefaultLanguage.ServerNotProvided;
                    CompanyComboBox.IsEnabled = true;
                    return;
                }

                using var http = new HttpClient { Timeout = HubSettings.ShortOperationTimeout };
                var url = baseUrl + "/api/admin/companies";

                HttpResponseMessage resp;
                try
                {
                    resp = await http.GetAsync(url);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Get companies failed: {ex}");
                    resp = null;
                }

                if (resp == null || !resp.IsSuccessStatusCode)
                {
                    CompanyComboBox.SelectedIndex = 0;
                    NoteTextBlock.Text = DefaultLanguage.CompaniesLoadFailed;
                    CompanyComboBox.IsEnabled = true;
                    return;
                }

                var json = await resp.Content.ReadAsStringAsync();
                var companies = JsonSerializer.Deserialize<List<SimpleCompany>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                               ?? new List<SimpleCompany>();

                foreach (var c in companies.OrderBy(c => c.Name))
                {
                    if (string.IsNullOrWhiteSpace(c.Name)) continue;
                    var display = c.Name.Replace('_', ' ');
                    var item = new ComboBoxItem { Content = display, Tag = c.Name, ToolTip = display };
                    CompanyComboBox.Items.Add(item);
                }

                var saved = ConfigService.Instance?.Company;
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    var match = CompanyComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => string.Equals((string?)i.Tag, saved, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        CompanyComboBox.SelectedItem = match;
                    else
                        CompanyComboBox.SelectedIndex = 0;
                }
                else
                {
                    CompanyComboBox.SelectedIndex = 0;
                }

                NoteTextBlock.Text = DefaultLanguage.SelectCompanyNote;
                CompanyComboBox.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] LoadCompaniesAsync failed: {ex}");
                CompanyComboBox.Items.Clear();
                var personalLabel = DefaultLanguage.PersonalLabel;
                CompanyComboBox.Items.Add(new ComboBoxItem { Content = personalLabel, Tag = "personal" });
                CompanyComboBox.SelectedIndex = 0;
                NoteTextBlock.Text = DefaultLanguage.CompaniesErrorFallback;
                CompanyComboBox.IsEnabled = true;
            }
        }

        private bool CreateOrReplaceDesktopShortcut(string combinedServerArg)
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shortcutPath = Path.Combine(desktop, ShortcutFileName);

                try
                {
                    if (File.Exists(shortcutPath))
                        File.Delete(shortcutPath);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Delete shortcut failed: {ex}"); }

                string exePath = GetExecutablePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return false;

                string args = string.IsNullOrWhiteSpace(combinedServerArg) ? string.Empty : $"\"{combinedServerArg}\"";

                var wshType = Type.GetTypeFromProgID("WScript.Shell");
                if (wshType == null) return false;
                var wsh = Activator.CreateInstance(wshType);
                var shortcut = wshType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, wsh, new object[] { shortcutPath });

                var scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { args });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exePath) ?? "" });
                scType.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 1 });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Edemly" });

                try
                {
                    scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exePath + ",0" });
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Set IconLocation failed: {ex}"); }

                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                return File.Exists(shortcutPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] CreateOrReplaceDesktopShortcut failed: {ex}");
                return false;
            }
        }

        private string GetExecutablePath()
        {
            try
            {
                try
                {
                    var cfgPath = ConfigService.Instance?.ExePath;
                    if (!string.IsNullOrWhiteSpace(cfgPath) && File.Exists(cfgPath))
                        return cfgPath;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Read config exe path failed: {ex}"); }

                var entry = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrWhiteSpace(entry) && File.Exists(entry))
                    return entry;

                try
                {
                    var procPath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(procPath) && File.Exists(procPath))
                        return procPath;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Get process main module failed: {ex}"); }

                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                var candidates = new List<string> { "Edemly.exe", "Edemly.Client.exe" };

                var asmName = Assembly.GetExecutingAssembly().GetName().Name;
                try
                {
                    if (!string.IsNullOrWhiteSpace(asmName))
                        candidates.Add(asmName + ".exe");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Assembly name retrieval failed: {ex}"); }

                DirectoryInfo di = new DirectoryInfo(baseDir);
                for (int depth = 0; di != null && depth < 4; depth++)
                {
                    foreach (var name in candidates)
                    {
                        var p = Path.Combine(di.FullName, name);
                        if (File.Exists(p)) return p;
                    }
                    di = di.Parent;
                }

                var prog = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var progX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                try
                {
                    foreach (var pf in new[] { prog, progX86 }.Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        foreach (var folder in new[] { "Edemly" })
                        {
                            foreach (var candidate in candidates)
                            {
                                var p = Path.Combine(pf, folder, candidate);
                                if (File.Exists(p)) return p;
                            }
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] Searching program files failed: {ex}"); }

                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] GetExecutablePath failed: {ex}");
                return string.Empty;
            }
        }

        private class SimpleCompany
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }
    }
}