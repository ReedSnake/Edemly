using Edemly.Client.Application.Localization;
using Edemly.Client.Infrastructure.Startup;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class InstallPage: ThemedPage
    {
        private const string ShortcutFileName = "Edemly.lnk";

        private readonly ICompanyCatalogLoader _companyCatalogLoader;
        private readonly IDesktopShortcutService _desktopShortcutService;

        public InstallPage()
            : this(new CompanyCatalogLoader(), new DesktopShortcutService())
        {
        }

        internal InstallPage(ICompanyCatalogLoader companyCatalogLoader, IDesktopShortcutService desktopShortcutService)
        {
            _companyCatalogLoader = companyCatalogLoader ?? throw new ArgumentNullException(nameof(companyCatalogLoader));
            _desktopShortcutService = desktopShortcutService ?? throw new ArgumentNullException(nameof(desktopShortcutService));

            InitializeComponent();
            Loaded += Page_install_Loaded;
        }

        private async void Page_install_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeLanguageOptions();

            var initialLanguage = ResolveInitialLanguage();
            SelectLanguage(initialLanguage);

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
                {
                    return;
                }

                if (CompanyComboBox.Items[0] is not ComboBoxItem firstItem)
                {
                    return;
                }

                var personalLabel = DefaultLanguage.PersonalLabel;
                firstItem.Content = personalLabel;
                firstItem.ToolTip = personalLabel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_INSTALL] UpdateCompanyPersonalLabel failed: {ex}");
            }
        }
    }
}
