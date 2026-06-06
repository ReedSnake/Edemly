#nullable enable

using Edemly.Client.Presentation.Common;
using System.Diagnostics;

namespace Edemly.Client.Presentation.Pages.Payments
{
    public partial class Page_premium
    {
        private void LoadTexts()
        {
            try
            {
                Title = DefaultLanguage.PremiumTitle;

                if (BackButton != null)
                {
                    BackButton.Content = PageNavigationGlyphs.Back;
                }

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
    }
}
