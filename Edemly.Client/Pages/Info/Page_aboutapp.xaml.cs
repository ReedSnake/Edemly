using Edemly.Client.Application.Services;
using Edemly.Client.Presentation.Common;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
namespace Edemly.Client.Pages.Info
{
    public partial class Page_aboutapp : ThemedPage
    {
        public Page_aboutapp()
        {
            InitializeComponent();

            LoadTexts();
        }

        private void LoadTexts()
        {
            try
            {
                var langService = LanguageService.Instance;

                this.Title = langService.GetText("page_about", "title", "About Edemly");

                GreetingText.Text = langService.GetText("page_about", "greeting", "Hi, dear user!");
                WelcomeText.Text = langService.GetText("page_about", "welcome", "We are the Edemly team, and we are happy to welcome you to our messenger!");
                GoalText.Text = langService.GetText("page_about", "goal", "Our goal is to create a space where communication and planning complement each other organically. We believe that modern people need not just another messenger, but a tool that helps them not only stay connected, but also manage their time effectively.");
                FaqTitleText.Text = langService.GetText("page_about", "faq_title", "FAQs:");

                FaqContactQuestionText.Text = langService.GetText("page_about", "faq_contact_question", "How do I add a new contact?");
                FaqContactAnswerText.Text = langService.GetText("page_about", "faq_contact_answer", "Use the search function in the main menu. Enter the user's name or email address, and you can start chatting.");

                FaqThemeQuestionText.Text = langService.GetText("page_about", "faq_theme_question", "How do I customize the theme?");
                FaqThemeAnswerText.Text = langService.GetText("page_about", "faq_theme_answer", "Go to the main menu and select 'Settings.' There you will find various design options to suit your taste.");

                FaqSchedulerQuestionText.Text = langService.GetText("page_about", "faq_scheduler_question", "How does the task scheduler work?");
                FaqSchedulerAnswerText.Text = langService.GetText("page_about", "faq_scheduler_answer", "You can create tasks and set deadlines. All tasks are synchronized with your calendar so you can see the full picture of your activities.");

                FaqSupportQuestionText.Text = langService.GetText("page_about", "faq_support_question", "How do I contact support?");
                SupportAnswerRun.Text = langService.GetText("page_about", "faq_support_answer", "If you have any questions or problems, please contact us using the form: ");
                SupportLinkRun.Text = langService.GetText("page_about", "faq_support_link", "Click here");

                ClosingText.Text = langService.GetText("page_about", "closing", "We create Edemly with you in mind. Every feature is designed to make your life easier and more organized.");
                ThanksText.Text = langService.GetText("page_about", "thanks", "Thank you for choosing us!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ABOUT] Error loading texts: {ex.Message}");
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
