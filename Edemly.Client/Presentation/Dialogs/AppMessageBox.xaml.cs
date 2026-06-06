using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Dialogs
{
    public partial class AppMessageBox : ThemedWindow
    {
        private readonly MessageBoxImage _currentIcon;
        private MessageBoxResult _okResult = MessageBoxResult.OK;
        private MessageBoxResult _cancelResult = MessageBoxResult.Cancel;
        private string _accentResourceKey = "ThemeInfoBrush";

        public MessageBoxResult Result { get; private set; }

        private AppMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();

            _currentIcon = icon;
            txtTitle.Text = string.IsNullOrWhiteSpace(title) ? DefaultLanguage.Information : title;
            txtMessage.Text = message;

            ConfigureIcon(icon);
            ConfigureButtons(button);
        }

        private void ConfigureIcon(MessageBoxImage icon)
        {
            var headerGrid = txtTitle.Parent as Grid;
            var headerBorder = headerGrid?.Parent as Border;
            var mainBorder = Content as Border;

            _accentResourceKey = ResolveAccentResourceKey(icon);
            txtIcon.Text = ResolveIconGlyph(icon);

            if (headerBorder != null)
            {
                headerBorder.SetResourceReference(Border.BackgroundProperty, _accentResourceKey);
            }

            if (mainBorder != null)
            {
                mainBorder.SetResourceReference(Border.BorderBrushProperty, _accentResourceKey);
            }
        }

        private static string ResolveAccentResourceKey(MessageBoxImage icon)
        {
            return icon switch
            {
                MessageBoxImage.Warning => "ThemeWarningBrush",
                MessageBoxImage.Error => "ThemeDangerBrush",
                MessageBoxImage.Question => "ThemeSuccessBrush",
                _ => "ThemeInfoBrush"
            };
        }

        private static string ResolveIconGlyph(MessageBoxImage icon)
        {
            return icon switch
            {
                MessageBoxImage.Warning => "!",
                MessageBoxImage.Error => "x",
                MessageBoxImage.Question => "?",
                _ => "i"
            };
        }

        private void ConfigureButtons(MessageBoxButton button)
        {
            switch (button)
            {
                case MessageBoxButton.OK:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Collapsed;
                    btnOk.Content = DefaultLanguage.Ok;
                    _okResult = MessageBoxResult.OK;
                    _cancelResult = MessageBoxResult.Cancel;
                    break;

                case MessageBoxButton.OKCancel:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnOk.Content = DefaultLanguage.Ok;
                    btnCancel.Content = DefaultLanguage.Cancel;
                    _okResult = MessageBoxResult.OK;
                    _cancelResult = MessageBoxResult.Cancel;
                    break;

                case MessageBoxButton.YesNo:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnOk.Content = DefaultLanguage.Yes;
                    btnCancel.Content = DefaultLanguage.No;
                    _okResult = MessageBoxResult.Yes;
                    _cancelResult = MessageBoxResult.No;
                    break;

                case MessageBoxButton.YesNoCancel:
                    btnOk.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnOk.Content = DefaultLanguage.Yes;
                    btnCancel.Content = DefaultLanguage.No;
                    _okResult = MessageBoxResult.Yes;
                    _cancelResult = MessageBoxResult.No;
                    break;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = _okResult;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = _cancelResult;
            DialogResult = false;
            Close();
        }

        protected override void ApplyTheme()
        {
            ConfigureIcon(_currentIcon);
        }

        #region Static Show Methods

        public static void Show(string message, string? title = null, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            var messageBox = new AppMessageBox(message, title ?? DefaultLanguage.Information, button, icon);
            messageBox.ShowDialog();
        }

        public static MessageBoxResult ShowQuestion(string message, string? title = null)
        {
            var messageBox = new AppMessageBox(message, title ?? DefaultLanguage.Warning, MessageBoxButton.YesNo, MessageBoxImage.Question);
            messageBox.ShowDialog();
            return messageBox.Result;
        }

        public static void ShowInfo(string message, string? title = null)
        {
            var messageBox = new AppMessageBox(message, title ?? DefaultLanguage.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            messageBox.ShowDialog();
        }

        public static void ShowError(string message, string? title = null)
        {
            var messageBox = new AppMessageBox(message, title ?? DefaultLanguage.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            messageBox.ShowDialog();
        }

        public static void ShowWarning(string message, string? title = null)
        {
            var messageBox = new AppMessageBox(message, title ?? DefaultLanguage.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
            messageBox.ShowDialog();
        }

        #endregion Static Show Methods
    }
}
