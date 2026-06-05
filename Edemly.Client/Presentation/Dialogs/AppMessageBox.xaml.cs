using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Dialogs
{
    public partial class AppMessageBox : ThemedWindow
    {
        public MessageBoxResult Result { get; private set; }

        private AppMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();

            txtTitle.Text = title;
            txtMessage.Text = message;

            ConfigureIcon(icon);
            ConfigureButtons(button);
        }

        private void ConfigureIcon(MessageBoxImage icon)
        {
            var headerGrid = txtTitle.Parent as Grid;
            var headerBorder = headerGrid?.Parent as Border;
            var mainBorder = Content as Border;
            var accentBrush = ResolveAccentBrush(icon);

            txtIcon.Text = icon switch
            {
                MessageBoxImage.Warning => "вљ пёЏ",
                MessageBoxImage.Error => "вќЊ",
                MessageBoxImage.Question => "вќ“",
                _ => "в„№пёЏ"
            };

            if (headerBorder != null)
                headerBorder.Background = accentBrush;

            if (mainBorder != null)
                mainBorder.BorderBrush = accentBrush;
        }

        private Brush ResolveAccentBrush(MessageBoxImage icon)
        {
            string resourceKey = icon switch
            {
                MessageBoxImage.Warning => "ThemeWarningBrush",
                MessageBoxImage.Error => "ThemeDangerBrush",
                MessageBoxImage.Question => "ThemeSuccessBrush",
                _ => "ThemeInfoBrush"
            };

            return TryFindResource(resourceKey) as Brush
                ?? new SolidColorBrush(Color.FromRgb(33, 150, 243));
        }

        private void ConfigureButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Collapsed;
                    btnOk.Content = "OK";
                    break;

                case MessageBoxButton.OKCancel:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnOk.Content = "OK";
                    btnCancel.Content = "Cancel";
                    break;

                case MessageBoxButton.YesNo:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnOk.Content = "Yes";
                    btnCancel.Content = "No";
                    break;

                case MessageBoxButton.YesNoCancel:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnOk.Content = "Yes";
                    btnCancel.Content = "No";
                    break;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = btnOk.Content.ToString() == "Yes" ? MessageBoxResult.Yes : MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = btnCancel.Content.ToString() == "No" ? MessageBoxResult.No : MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        #region Static Show Methods

        public static void Show(string message, string title = "Message", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            var messageBox = new AppMessageBox(message, title, button, icon);
            messageBox.ShowDialog();
        }

        public static MessageBoxResult ShowQuestion(string message, string title = "Question")
        {
            var messageBox = new AppMessageBox(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            messageBox.ShowDialog();
            return messageBox.Result;
        }

        public static void ShowInfo(string message, string title = "Information")
        {
            var messageBox = new AppMessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            messageBox.ShowDialog();
        }

        public static void ShowError(string message, string title = "Error")
        {
            var messageBox = new AppMessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            messageBox.ShowDialog();
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            var messageBox = new AppMessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            messageBox.ShowDialog();
        }

        #endregion Static Show Methods
    }
}
