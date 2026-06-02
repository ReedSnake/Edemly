using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Edemly.Client.Lang;
using Edemly.Client.Services;
using MessageBox = Edemly.Client.Pages.MessageBox;

namespace Edemly.Client
{
    public partial class Page_premium : Page
    {
        private const decimal MonthlyAmount = 79.90m; 
        private const decimal YearlyAmount = 790.00m; 

        public Page_premium()
        {
            InitializeComponent();

            // Subscribe to theme changes
            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            // Apply current theme
            ApplyThemeToPage();

            LoadTexts();
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToPage();
                System.Diagnostics.Debug.WriteLine("[PAGE_PREMIUM] Theme changed");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_PREMIUM] OnThemeChanged failed: {ex}"); }
        }

        private void ApplyThemeToPage()
        {
            try
            {
                var palette = ThemeService.Instance.GetCurrentPalette();

                // Update page background gradient
                var grid = this.Content as Grid;
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

                // Update buttons
                if (MonthlyButton != null)
                {
                    MonthlyButton.Background = new SolidColorBrush(palette.Primary);
                }
                if (YearlyButton != null)
                {
                    YearlyButton.Background = new SolidColorBrush(palette.Secondary);
                }

                System.Diagnostics.Debug.WriteLine($"[PAGE_PREMIUM] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_PREMIUM] ApplyThemeToPage error: {ex.Message}");
            }
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

                // Initiate payment on server
                var res = await apiConcrete.InitiatePaymentAsync(amount);
                if (!res.Success || string.IsNullOrEmpty(res.Html))
                {
                    MessageBox.ShowError(res.Error ?? DefaultLanguage.PremiumPaymentFailed, DefaultLanguage.PremiumPaymentError);
                    return;
                }

                // Save HTML to temp file and open
                var tmp = Path.Combine(Path.GetTempPath(), $"edemly_payment_{Guid.NewGuid():N}.html");
                await File.WriteAllTextAsync(tmp, res.Html, Encoding.UTF8);

                try
                {
                    Process.Start(new ProcessStartInfo(tmp) { UseShellExecute = true });
                }
                catch (Exception ex)
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
