using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Services;
using Edemly.Client.Infrastructure.Realtime;
using Edemly.Client.Presentation.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Pages.Auth
{
    public partial class Page_install : ThemedPage
    {
        private const string ShortcutFileName = "Edemly.lnk";

        public Page_install()
        {
            InitializeComponent();

            Loaded += Page_install_Loaded;
        }

        private async void Page_install_Loaded(object sender, RoutedEventArgs e)
        {
            LanguageComboBox.Items.Clear();

            var enItem = new ComboBoxItem
            {
                Content = "English",
                Tag = "en"
            };

            var ukItem = new ComboBoxItem
            {
                Content = "Українська",
                Tag = "uk"
            };

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
                string systemLanguage =
                    CultureInfo.CurrentUICulture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? "en";

                initial = systemLanguage is "uk" or "ru"
                    ? "uk"
                    : "en";
            }

            LanguageComboBox.SelectedItem = LanguageComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Tag == initial);

            try
            {
                LanguageService.Instance.LoadLanguage(initial);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] LoadLanguage failed: {ex}");
            }

            ApplyLanguage();

            await LoadCompaniesAsync();
        }

        private void ApplyLanguage()
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
                if (CompanyComboBox.Items.Count == 0)
                    return;

                if (CompanyComboBox.Items[0] is not ComboBoxItem firstItem)
                    return;

                string personalLabel = DefaultLanguage.PersonalLabel;

                firstItem.Content = personalLabel;
                firstItem.ToolTip = personalLabel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] UpdateCompanyPersonalLabel failed: {ex}");
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is not ComboBoxItem item)
                return;

            if (item.Tag is not string languageTag)
                return;

            try
            {
                ConfigService.Instance.Language = languageTag;
                ConfigService.Instance.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] Save config language failed: {ex}");
            }

            try
            {
                LanguageService.Instance.LoadLanguage(languageTag);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] LoadLanguage on selection failed: {ex}");
            }

            ApplyLanguage();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new Page_login());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] Cancel navigation failed: {ex}");

                try
                {
                    if (NavigationService?.CanGoBack == true)
                    {
                        NavigationService.GoBack();
                    }
                }
                catch (Exception goBackEx)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] GoBack failed: {goBackEx}");
                }
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedLanguage =
                    (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "en";

                try
                {
                    ConfigService.Instance.Language = selectedLanguage;
                    ConfigService.Instance.Save();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Save language on continue failed: {ex}");
                }

                try
                {
                    LanguageService.Instance.LoadLanguage(selectedLanguage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] LoadLanguage on continue failed: {ex}");

                    try
                    {
                        ConfigService.Instance.Language = selectedLanguage;
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"[PAGE_INSTALL] Save fallback language failed: {innerEx}");
                    }
                }

                ApplyLanguage();

                string selectedCompanyTag =
                    (CompanyComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "personal";

                if (string.Equals(selectedCompanyTag, "personal", StringComparison.OrdinalIgnoreCase))
                {
                    selectedCompanyTag = string.Empty;
                }

                App.SetCompanyAndApply(selectedCompanyTag, markInstalled: true);

                string baseUrl = App.BaseServerUrlNoCompany?.TrimEnd('/') ?? string.Empty;

                string shortcutArgument = string.IsNullOrEmpty(baseUrl)
                    ? string.Empty
                    : string.IsNullOrEmpty(selectedCompanyTag)
                        ? baseUrl
                        : $"{baseUrl}/{selectedCompanyTag.Trim().Trim('/')}";

                if (DesktopShortcutCheckBox.IsChecked == true)
                {
                    ConfigService.Instance.CreateDesktopShortcut = true;

                    bool created = CreateOrReplaceDesktopShortcut(shortcutArgument);

                    if (!created)
                    {
                        MessageBox.Show(
                            DefaultLanguage.ShortcutCreateFailed,
                            "Edemly",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    ConfigService.Instance.CreateDesktopShortcut = false;
                }

                await Task.Delay(80);

                NavigationService?.Navigate(new Page_login());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error: " + ex.Message,
                    "Edemly",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadCompaniesAsync()
        {
            try
            {
                CompanyComboBox.IsEnabled = false;
                CompanyComboBox.Items.Clear();

                string personalLabel = DefaultLanguage.PersonalLabel;

                var personalItem = new ComboBoxItem
                {
                    Content = personalLabel,
                    Tag = "personal",
                    ToolTip = personalLabel
                };

                CompanyComboBox.Items.Add(personalItem);

                NoteTextBlock.Text = DefaultLanguage.LoadingCompanies;

                string? baseUrl = App.BaseServerUrlNoCompany?.TrimEnd('/');

                if (string.IsNullOrEmpty(baseUrl))
                {
                    CompanyComboBox.SelectedIndex = 0;
                    NoteTextBlock.Text = DefaultLanguage.ServerNotProvided;
                    CompanyComboBox.IsEnabled = true;

                    return;
                }

                using var httpClient = new HttpClient
                {
                    Timeout = HubSettings.ShortOperationTimeout
                };

                string url = baseUrl + "/api/admin/companies";

                HttpResponseMessage? response;

                try
                {
                    response = await httpClient.GetAsync(url);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Get companies failed: {ex}");
                    response = null;
                }

                if (response is null || !response.IsSuccessStatusCode)
                {
                    CompanyComboBox.SelectedIndex = 0;
                    NoteTextBlock.Text = DefaultLanguage.CompaniesLoadFailed;
                    CompanyComboBox.IsEnabled = true;

                    return;
                }

                string json = await response.Content.ReadAsStringAsync();

                var companies = JsonSerializer.Deserialize<List<SimpleCompany>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<SimpleCompany>();

                foreach (var company in companies.OrderBy(company => company.Name))
                {
                    if (string.IsNullOrWhiteSpace(company.Name))
                        continue;

                    string displayName = company.Name.Replace('_', ' ');

                    var item = new ComboBoxItem
                    {
                        Content = displayName,
                        Tag = company.Name,
                        ToolTip = displayName
                    };

                    CompanyComboBox.Items.Add(item);
                }

                string? savedCompany = ConfigService.Instance?.Company;

                if (!string.IsNullOrWhiteSpace(savedCompany))
                {
                    var match = CompanyComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(item =>
                            string.Equals(
                                item.Tag as string,
                                savedCompany,
                                StringComparison.OrdinalIgnoreCase));

                    CompanyComboBox.SelectedItem = match ?? CompanyComboBox.Items[0];
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
                Debug.WriteLine($"[PAGE_INSTALL] LoadCompaniesAsync failed: {ex}");

                CompanyComboBox.Items.Clear();

                string personalLabel = DefaultLanguage.PersonalLabel;

                CompanyComboBox.Items.Add(new ComboBoxItem
                {
                    Content = personalLabel,
                    Tag = "personal",
                    ToolTip = personalLabel
                });

                CompanyComboBox.SelectedIndex = 0;
                NoteTextBlock.Text = DefaultLanguage.CompaniesErrorFallback;
                CompanyComboBox.IsEnabled = true;
            }
        }

        private bool CreateOrReplaceDesktopShortcut(string combinedServerArg)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktop, ShortcutFileName);

                try
                {
                    if (File.Exists(shortcutPath))
                    {
                        File.Delete(shortcutPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Delete shortcut failed: {ex}");
                }

                string exePath = GetExecutablePath();

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return false;

                string args = string.IsNullOrWhiteSpace(combinedServerArg)
                    ? string.Empty
                    : $"\"{combinedServerArg}\"";

                Type? wshType = Type.GetTypeFromProgID("WScript.Shell");

                if (wshType is null)
                    return false;

                object? wsh = Activator.CreateInstance(wshType);

                object? shortcut = wshType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    wsh,
                    new object[] { shortcutPath });

                if (shortcut is null)
                    return false;

                Type shortcutType = shortcut.GetType();

                shortcutType.InvokeMember(
                    "TargetPath",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { exePath });

                shortcutType.InvokeMember(
                    "Arguments",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { args });

                shortcutType.InvokeMember(
                    "WorkingDirectory",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { Path.GetDirectoryName(exePath) ?? string.Empty });

                shortcutType.InvokeMember(
                    "WindowStyle",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { 1 });

                shortcutType.InvokeMember(
                    "Description",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { "Edemly" });

                try
                {
                    shortcutType.InvokeMember(
                        "IconLocation",
                        BindingFlags.SetProperty,
                        null,
                        shortcut,
                        new object[] { exePath + ",0" });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Set IconLocation failed: {ex}");
                }

                shortcutType.InvokeMember(
                    "Save",
                    BindingFlags.InvokeMethod,
                    null,
                    shortcut,
                    null);

                return File.Exists(shortcutPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] CreateOrReplaceDesktopShortcut failed: {ex}");
                return false;
            }
        }

        private string GetExecutablePath()
        {
            try
            {
                try
                {
                    string? configPath = ConfigService.Instance?.ExePath;

                    if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
                        return configPath;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Read config exe path failed: {ex}");
                }

                string? entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;

                if (!string.IsNullOrWhiteSpace(entryAssemblyPath) && File.Exists(entryAssemblyPath))
                    return entryAssemblyPath;

                try
                {
                    string? processPath = Process.GetCurrentProcess().MainModule?.FileName;

                    if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
                        return processPath;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Get process main module failed: {ex}");
                }

                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory
                    ?? Directory.GetCurrentDirectory();

                var candidates = new List<string>
                {
                    "Edemly.exe",
                    "Edemly.Client.exe"
                };

                string? assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    candidates.Add(assemblyName + ".exe");
                }

                DirectoryInfo? directoryInfo = new(baseDirectory);

                for (int depth = 0; directoryInfo is not null && depth < 4; depth++)
                {
                    foreach (string candidate in candidates)
                    {
                        string path = Path.Combine(directoryInfo.FullName, candidate);

                        if (File.Exists(path))
                            return path;
                    }

                    directoryInfo = directoryInfo.Parent;
                }

                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                try
                {
                    foreach (string programFilesDirectory in new[] { programFiles, programFilesX86 }
                                 .Where(path => !string.IsNullOrWhiteSpace(path)))
                    {
                        foreach (string candidate in candidates)
                        {
                            string path = Path.Combine(
                                programFilesDirectory,
                                "Edemly",
                                candidate);

                            if (File.Exists(path))
                                return path;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] Searching program files failed: {ex}");
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] GetExecutablePath failed: {ex}");
                return string.Empty;
            }
        }

        private sealed class SimpleCompany
        {
            public int Id { get; set; }

            public string? Name { get; set; }
        }
    }
}