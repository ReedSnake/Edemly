#nullable enable

using Edemly.Client.Infrastructure.Navigation;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Info
{
    public partial class AboutAppPage : ThemedPage
    {
        private readonly IExternalNavigationLauncher _externalNavigationLauncher;

        public AboutAppPage()
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ABOUT] ApplyTheme failed: {ex.Message}");
            }
        }
    }
}
