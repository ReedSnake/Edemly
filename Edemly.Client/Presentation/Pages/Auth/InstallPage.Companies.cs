#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Services;
using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class InstallPage
    {
        private async Task LoadCompaniesAsync()
        {
            try
            {
                CompanyComboBox.IsEnabled = false;
                CompanyComboBox.Items.Clear();
                CompanyComboBox.Items.Add(CreatePersonalCompanyItem());

                NoteTextBlock.Text = DefaultLanguage.LoadingCompanies;

                var baseUrl = App.BaseServerUrlNoCompany?.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    CompanyComboBox.SelectedIndex = 0;
                    NoteTextBlock.Text = DefaultLanguage.ServerNotProvided;
                    return;
                }

                var companies = await _companyCatalogLoader.LoadAsync(baseUrl);
                foreach (var company in companies)
                {
                    CompanyComboBox.Items.Add(CreateCompanyItem(company.Name));
                }

                SelectSavedCompany();
                NoteTextBlock.Text = companies.Count > 0
                    ? DefaultLanguage.SelectCompanyNote
                    : DefaultLanguage.CompaniesLoadFailed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstallPage] LoadCompaniesAsync failed: {ex}");
                ResetCompaniesToFallback(DefaultLanguage.CompaniesErrorFallback);
            }
            finally
            {
                CompanyComboBox.IsEnabled = true;
            }
        }

        private ComboBoxItem CreatePersonalCompanyItem()
        {
            var personalLabel = DefaultLanguage.PersonalLabel;
            return new ComboBoxItem
            {
                Content = personalLabel,
                Tag = "personal",
                ToolTip = personalLabel
            };
        }

        private static ComboBoxItem CreateCompanyItem(string companyName)
        {
            var displayName = companyName.Replace('_', ' ');
            return new ComboBoxItem
            {
                Content = displayName,
                Tag = companyName,
                ToolTip = displayName
            };
        }

        private void SelectSavedCompany()
        {
            var savedCompany = ConfigService.Instance?.Company;
            if (string.IsNullOrWhiteSpace(savedCompany))
            {
                CompanyComboBox.SelectedIndex = 0;
                return;
            }

            var match = CompanyComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item =>
                    string.Equals(item.Tag as string, savedCompany, StringComparison.OrdinalIgnoreCase));

            CompanyComboBox.SelectedItem = match ?? CompanyComboBox.Items[0];
        }

        private void ResetCompaniesToFallback(string noteText)
        {
            CompanyComboBox.Items.Clear();
            CompanyComboBox.Items.Add(CreatePersonalCompanyItem());
            CompanyComboBox.SelectedIndex = 0;
            NoteTextBlock.Text = noteText;
        }

        private string GetSelectedCompanyTag()
        {
            var selectedCompanyTag = (CompanyComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "personal";
            return string.Equals(selectedCompanyTag, "personal", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : selectedCompanyTag;
        }
    }
}
