#nullable enable

using Edemly.Client.Infrastructure.Navigation;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Payments
{
    public partial class PremiumPage : ThemedPage
    {
        private const decimal MonthlyAmount = 79.90m;
        private const decimal YearlyAmount = 790.00m;

        private readonly IExternalNavigationLauncher _externalNavigationLauncher;

        public PremiumPage()
        {
            InitializeComponent();

            _externalNavigationLauncher = new ProcessExternalNavigationLauncher();

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

                System.Diagnostics.Debug.WriteLine("[PremiumPage] Theme applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PremiumPage] ApplyTheme failed: {ex}");
            }
        }
    }
}
