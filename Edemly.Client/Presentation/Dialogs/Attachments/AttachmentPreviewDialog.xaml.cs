#nullable enable

using Edemly.Client.Application.Attachments;
using Edemly.Client.Presentation.Common;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Presentation.Dialogs.Attachments
{
    public partial class AttachmentPreviewDialog : ThemedWindow
    {
        private readonly AttachmentDescriptor _descriptor;

        public AttachmentDialogResult Result { get; private set; } = AttachmentDialogResult.Cancelled;

        public AttachmentPreviewDialog(AttachmentDescriptor descriptor, string? initialCaption = null)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

            InitializeComponent();
            ConfigureContent(initialCaption);
            LoadPreview();
        }

        private void ConfigureContent(string? initialCaption)
        {
            DialogTitleText.Text = _descriptor.Kind == AttachmentKind.Image
                ? DefaultLanguage.AttachmentPreviewImageTitle
                : DefaultLanguage.AttachmentPreviewFileTitle;
            DialogHintText.Text = DefaultLanguage.AttachmentPreviewHint;
            PreviewIconText.Text = _descriptor.IconGlyph;
            PreviewUnavailableText.Text = DefaultLanguage.AttachmentPreviewUnavailable;
            FileNameText.Text = _descriptor.FileName;
            FileMetaText.Text = $"{AttachmentPresentationFormatter.GetKindLabel(_descriptor.Kind)} | {AttachmentPresentationFormatter.FormatSize(_descriptor.SizeBytes)}";
            CaptionLabelText.Text = DefaultLanguage.AttachmentCaptionLabel;
            RemoveButton.Content = DefaultLanguage.Remove;
            CancelButton.Content = DefaultLanguage.Cancel;
            SendButton.Content = DefaultLanguage.Send;
            CaptionTextBox.Text = initialCaption ?? string.Empty;

            RemoveButton.SetResourceReference(Control.BackgroundProperty, "ThemeDangerBrush");
            RemoveButton.SetResourceReference(Control.ForegroundProperty, "ThemeOnPrimaryTextBrush");
            RemoveButton.SetResourceReference(Control.BorderBrushProperty, "ThemeDangerBrush");
        }

        private void LoadPreview()
        {
            if (!_descriptor.CanPreviewImage || !File.Exists(_descriptor.FilePath))
            {
                ShowPreviewFallback();
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(_descriptor.FilePath);
                bitmap.DecodePixelWidth = 1024;
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewImage.Source = bitmap;
                PreviewImage.Visibility = Visibility.Visible;
                PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] Failed to load attachment preview: {ex.Message}");
                ShowPreviewFallback();
            }
        }

        private void ShowPreviewFallback()
        {
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewPlaceholderPanel.Visibility = Visibility.Visible;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            Result = new AttachmentDialogResult(AttachmentDialogAction.Remove, string.Empty);
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AttachmentDialogResult.Cancelled;
            DialogResult = false;
            Close();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            Result = new AttachmentDialogResult(AttachmentDialogAction.Send, CaptionTextBox.Text?.Trim() ?? string.Empty);
            DialogResult = true;
            Close();
        }
    }
}
