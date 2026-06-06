#nullable enable

using Edemly.Client.Application.Attachments;
using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                var clipboardData = Clipboard.GetDataObject();

                if (PageMainAttachmentInputHelper.HasFiles(clipboardData))
                {
                    e.CancelCommand();
                    _ = RunAttachmentSelectionAsync(PageMainAttachmentInputHelper.ExtractFiles(clipboardData));
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    e.CancelCommand();
                    _ = HandleClipboardImageAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] OnPaste error: {ex.Message}");
            }
        }

        private void OnPasteCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var clipboardData = Clipboard.GetDataObject();

                if (PageMainAttachmentInputHelper.HasFiles(clipboardData))
                {
                    _ = RunAttachmentSelectionAsync(PageMainAttachmentInputHelper.ExtractFiles(clipboardData));
                    e.Handled = true;
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    _ = HandleClipboardImageAsync();
                    e.Handled = true;
                    return;
                }

                var clipboardText = PageMainAttachmentInputHelper.ExtractText(clipboardData);
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    InsertTextIntoMessageBox(clipboardText, sender as TextBox);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] OnPasteCommandExecuted error: {ex.Message}");
            }
        }

        private void OnCanPasteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void MessageTextBox_PreviewKeyDownForPaste(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key != Key.V || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    return;
                }

                var clipboardData = Clipboard.GetDataObject();

                if (PageMainAttachmentInputHelper.HasFiles(clipboardData))
                {
                    _ = RunAttachmentSelectionAsync(PageMainAttachmentInputHelper.ExtractFiles(clipboardData));
                    e.Handled = true;
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    _ = HandleClipboardImageAsync();
                    e.Handled = true;
                    return;
                }

                var clipboardText = PageMainAttachmentInputHelper.ExtractText(clipboardData);
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    InsertTextIntoMessageBox(clipboardText, sender as TextBox);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] MessageTextBox_PreviewKeyDownForPaste error: {ex.Message}");
            }
        }

        private async Task HandleClipboardImageAsync()
        {
            string? tempPath = null;

            try
            {
                if (!EnsureAttachmentChatSelected())
                {
                    return;
                }

                var source = Clipboard.GetImage();
                if (source == null)
                {
                    return;
                }

                tempPath = _clipboardImageTempFileStore.SaveToTemporaryPng(source);
                if (string.IsNullOrWhiteSpace(tempPath))
                {
                    return;
                }

                await RunAttachmentSelectionAsync([tempPath]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] HandleClipboardImageAsync error: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
            finally
            {
                _clipboardImageTempFileStore.Delete(tempPath);
            }
        }

        private void InsertTextIntoMessageBox(string text, TextBox? textBox = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var target = textBox ?? MessageTextBox;
            if (target == null)
            {
                return;
            }

            if (IsPlaceholderText(target.Text))
            {
                ApplyTextInputActiveStyle(target, string.Empty);
            }

            var selectionStart = target.SelectionStart;
            target.Text = target.Text.Insert(selectionStart, text);
            target.SelectionStart = selectionStart + text.Length;
        }
    }
}
