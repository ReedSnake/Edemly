using Edemly.Client.Application.Localization;
using Edemly.Client.Infrastructure.Startup;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class InstallPage: ThemedPage
    {
        private readonly ICompanyCatalogLoader _companyCatalogLoader;

        public InstallPage()
            : this(new CompanyCatalogLoader())
        {
        }

        internal InstallPage(ICompanyCatalogLoader companyCatalogLoader)
        {
            _companyCatalogLoader = companyCatalogLoader ?? throw new ArgumentNullException(nameof(companyCatalogLoader));

            InitializeComponent();
            Loaded += InstallPage_Loaded;
        }

        private async void InstallPage_Loaded(object sender, RoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"[InstallPage] UpdateCompanyPersonalLabel failed: {ex}");
            }
        }
    }
}
