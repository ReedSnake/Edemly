using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using uchat.Services;

namespace uchat.Pages
{
    public partial class MessageBox : Window
    {
        public MessageBoxResult Result { get; private set; }

        private MessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();

            // Subscribe to theme changes
            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            txtTitle.Text = title;
            txtMessage.Text = message;

            ConfigureIcon(icon);
            ConfigureButtons(button);
        }

        private void OnThemeChanged()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MESSAGEBOX] Theme changed");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MESSAGEBOX] OnThemeChanged failed: {ex}"); }
        }

        private void ConfigureIcon(MessageBoxImage icon)
        {
            // Знаходимо Border з заголовком через візуальне дерево
            var headerGrid = txtTitle.Parent as Grid;
            var headerBorder = headerGrid?.Parent as Border;
            var mainBorder = this.Content as Border;

            switch (icon)
            {
                case MessageBoxImage.Information:
                    txtIcon.Text = "ℹ️";
                    if (headerBorder != null)
                        headerBorder.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // #2196F3 - Синій
                    if (mainBorder != null)
                        mainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;

                case MessageBoxImage.Warning:
                    txtIcon.Text = "⚠️";
                    if (headerBorder != null)
                        headerBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // #FF9800 - Помаранчевий
                    if (mainBorder != null)
                        mainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    break;

                case MessageBoxImage.Error:
                    txtIcon.Text = "❌";
                    if (headerBorder != null)
                        headerBorder.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // #F44336 - Червоний
                    if (mainBorder != null)
                        mainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    break;

                case MessageBoxImage.Question:
                    txtIcon.Text = "❓";
                    if (headerBorder != null)
                        headerBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // #4CAF50 - Зелений
                    if (mainBorder != null)
                        mainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;

                default:
                    txtIcon.Text = "ℹ️";
                    if (headerBorder != null)
                        headerBorder.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    if (mainBorder != null)
                        mainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;
            }
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
            var messageBox = new MessageBox(message, title, button, icon);
            messageBox.ShowDialog();
        }

        public static MessageBoxResult ShowQuestion(string message, string title = "Question")
        {
            var messageBox = new MessageBox(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            messageBox.ShowDialog();
            return messageBox.Result;
        }

        public static void ShowInfo(string message, string title = "Information")
        {
            var messageBox = new MessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            messageBox.ShowDialog();
        }

        public static void ShowError(string message, string title = "Error")
        {
            var messageBox = new MessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            messageBox.ShowDialog();
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            var messageBox = new MessageBox(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            messageBox.ShowDialog();
        }

        #endregion
    }
}