using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Common;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace Edemly.Client.Pages.Payments
{
    public partial class Page_premium : ThemedPage
    {
        private const decimal MonthlyAmount = 79.90m;
        private const decimal YearlyAmount = 790.00m;

        public Page_premium()
        {
            InitializeComponent();

            LoadTexts();
        }

        protected override void ApplyTheme()
        {
            try
            {
                if (Content is Grid rootGrid)
                {
                    rootGrid.SetResourceReference(Panel.BackgroundProperty, "PageBackgroundBrush");
                }

                System.Diagnostics.Debug.WriteLine("[PAGE_PREMIUM] Theme applied");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_PREMIUM] ApplyTheme failed: {ex}"); }
        }

        private void LoadTexts()
        {
            try
            {
                this.Title = DefaultLanguage.PremiumTitle;

                MainTitleText.Text = DefaultLanguage.PremiumMainTitle;
                DescriptionText.Text = DefaultLanguage.PremiumDescription;
                WhatIncludedText.Text = DefaultLanguage.PremiumWhatIncluded;

                Feature1TitleText.Text = DefaultLanguage.PremiumFeature1Title;
                Feature1DescText.Text = DefaultLanguage.PremiumFeature1Desc;

                Feature2TitleText.Text = DefaultLanguage.PremiumFeature2Title;
                Feature2DescText.Text = DefaultLanguage.PremiumFeature2Desc;

                Feature3TitleText.Text = DefaultLanguage.PremiumFeature3Title;
                Feature3DescText.Text = DefaultLanguage.PremiumFeature3Desc;

                Feature4TitleText.Text = DefaultLanguage.PremiumFeature4Title;
                Feature4DescText.Text = DefaultLanguage.PremiumFeature4Desc;

                NoteText.Text = DefaultLanguage.PremiumNote;

                MonthlyButton.Content = DefaultLanguage.PremiumMonthlyButton;
                YearlyButton.Content = DefaultLanguage.PremiumYearlyButton;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PREMIUM] Error loading texts: {ex.Message}");
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Page_main());
        }

        private async void MonthlyButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPaymentFlowAsync(MonthlyAmount, "Monthly");
        }

        private async void YearlyButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPaymentFlowAsync(YearlyAmount, "Yearly");
        }

        private async Task StartPaymentFlowAsync(decimal amount, string planName)
        {
            try
            {
                var apiConcrete = App.ApiService as Edemly.Client.Api.ApiService;
                if (apiConcrete == null)
                {
                    MessageBox.ShowError(DefaultLanguage.PremiumApiError, DefaultLanguage.PremiumPaymentError);
                    return;
                }

                var res = await apiConcrete.InitiatePaymentAsync(amount);
                if (!res.Success || string.IsNullOrEmpty(res.Html))
                {
                    MessageBox.ShowError(res.Error ?? DefaultLanguage.PremiumPaymentFailed, DefaultLanguage.PremiumPaymentError);
                    return;
                }

                var tmp = Path.Combine(Path.GetTempPath(), $"edemly_payment_{Guid.NewGuid():N}.html");
                await File.WriteAllTextAsync(tmp, res.Html, Encoding.UTF8);

                try
                {
                    Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true });
                }
                catch (Exception)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{tmp}\"") { CreateNoWindow = true });
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.ShowError(string.Format(DefaultLanguage.PremiumOpenPageError, ex2.Message), DefaultLanguage.PremiumPaymentError);
                        System.Diagnostics.Debug.WriteLine($"[PAGE_PREMIUM] Failed to open payment HTML: {ex2}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError("Error: " + ex.Message, DefaultLanguage.PremiumPaymentError);
            }
        }
    }
}
