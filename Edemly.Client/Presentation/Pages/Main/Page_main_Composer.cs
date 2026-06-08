#nullable disable

using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private void SetMessagePlaceholder()
        {
            if (MessageTextBox == null)
                return;

            ApplyTextInputPlaceholderStyle(MessageTextBox, DefaultLanguage.TypeMessage);
        }

        private void RestoreMessageInputText(string text)
        {
            if (MessageTextBox == null)
                return;

            if (string.IsNullOrWhiteSpace(text) || PageMainInputHelper.IsPlaceholderText(text))
            {
                SetMessagePlaceholder();
                return;
            }

            ApplyTextInputActiveStyle(MessageTextBox, text);
        }

        private void ResetSendButtonForCurrentMessageInput()
        {
            if (SendButton == null || MessageTextBox == null)
                return;

            SendButton.IsEnabled = true;
            SendButton.Background = Brushes.Transparent;
            SendButton.ToolTip = null;

            if (PageMainInputHelper.IsPlaceholderText(MessageTextBox.Text))
            {
                SendButton.Content = PageMainGlyphs.Microphone;
                SendButton.Tag = "voice";
            }
            else
            {
                SendButton.Content = PageMainGlyphs.Send;
                SendButton.Tag = "send";
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var tag = SendButton?.Tag?.ToString();

            if (tag == "voice")
            {
                await HandleVoiceRecordingAsync();
                return;
            }

            if (tag == "recording" || _isRecording)
            {
                await HandleVoiceRecordingAsync();
                return;
            }

            string message = MessageTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(message) && !PageMainInputHelper.IsPlaceholderText(message))
            {
                if (_chatController.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat via search", "Error");
                    return;
                }

                await _chatController.SendMessageAsync(message);
                SetMessagePlaceholder();
                MessageTextBox.Focus();
            }
        }

        private async void MessageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_isRecording)
                {
                    e.Handled = true;
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    return;
                }

                e.Handled = true;

                if (_chatController.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat", "Error");
                    return;
                }

                string message = MessageTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(message) && !PageMainInputHelper.IsPlaceholderText(message))
                {
                    await _chatController.SendMessageAsync(message);
                    SetMessagePlaceholder();
                }
            }
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isRecording || SendButton == null)
                return;

            ResetSendButtonForCurrentMessageInput();
        }
    }
}
