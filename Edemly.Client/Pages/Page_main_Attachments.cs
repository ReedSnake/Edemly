#nullable disable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = Edemly.Client.Pages.MessageBox;
using Edemly.Client.DTOs;

namespace Edemly.Client
{
    public partial class Page_main : Page
    {
        private async void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (chatManager.CurrentChatId < 0)
            {
                MessageBox.ShowWarning("First select a contact to chat", "Error");
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "All files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Documents (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx",
                Multiselect = true,
                Title = "Select files to send"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filePath in openFileDialog.FileNames)
                {
                    string caption = ShowAttachmentCaptionDialog(filePath);
                    if (caption == null)
                    {
                        continue;
                    }

                    await SendFileAsync(filePath, caption);
                }
            }
        }

        private string ShowAttachmentCaptionDialog(string filePath)
        {
            var input = new Window
            {
                Title = "Send file",
                Width = 560,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                Owner = Application.Current.MainWindow
            };

            var grid = new Grid { Margin = new Thickness(12) };

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string ext = System.IO.Path.GetExtension(filePath)?.ToLower() ?? string.Empty;
            bool isImage = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp";

            if (isImage)
            {
                var img = new System.Windows.Controls.Image { Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Center };
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(filePath);
                    bi.DecodePixelWidth = 800;
                    bi.EndInit();
                    bi.Freeze();

                    img.Source = bi;
                    img.MaxHeight = 280;
                    Grid.SetRow(img, 0);
                    grid.Children.Add(img);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ATTACH-PREVIEW] Failed to load image preview: {ex.Message}");
                    var labelFallback = new TextBlock { Text = System.IO.Path.GetFileName(filePath), FontSize = 13, Margin = new Thickness(0, 0, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis };
                    Grid.SetRow(labelFallback, 0); grid.Children.Add(labelFallback);
                }
            }
            else
            {
                var label = new TextBlock { Text = System.IO.Path.GetFileName(filePath), FontSize = 13, Margin = new Thickness(0, 0, 0, 8), TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetRow(label, 0); grid.Children.Add(label);
            }

            var tb = new TextBox { Text = string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80, Padding = new Thickness(8) };
            Grid.SetRow(tb, 1); grid.Children.Add(tb);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(buttons, 2);
            var cancel = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (s, e) => input.DialogResult = false;
            var send = new Button { Content = "Send", Width = 80, Height = 30, Background = new SolidColorBrush(Color.FromRgb(5, 114, 114)), Foreground = Brushes.White };
            send.Click += (s, e) => input.DialogResult = true;
            buttons.Children.Add(cancel); buttons.Children.Add(send); grid.Children.Add(buttons);

            input.Content = grid;

            var result = input.ShowDialog();
            if (result == true)
            {
                return tb.Text?.Trim() ?? string.Empty;
            }

            return null;
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[PASTE_HANDLER] OnPaste invoked");
                if (Clipboard.ContainsFileDropList())
                {
                    var sc = Clipboard.GetFileDropList();
                    var files = sc.Cast<string>().ToList();
                    e.CancelCommand();

                    System.Diagnostics.Debug.WriteLine($"[PASTE_HANDLER] Files from clipboard: {files.Count}");

                    Dispatcher.InvokeAsync(async () => await ProcessDroppedFiles(files));
                }
                else if (Clipboard.ContainsImage())
                {
                    e.CancelCommand();
                    System.Diagnostics.Debug.WriteLine("[PASTE_HANDLER] Image found on clipboard");

                    Dispatcher.InvokeAsync(async () => await HandleClipboardImageAsync());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[PASTE_HANDLER] No file list on clipboard; letting text paste proceed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PASTE] Error handling paste: {ex.Message}");
            }
        }

        private void OnPasteCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[PASTE_CMD] OnPasteCommandExecuted invoked");
                if (Clipboard.ContainsFileDropList())
                {
                    var sc = Clipboard.GetFileDropList();
                    var files = sc.Cast<string>().ToList();

                    System.Diagnostics.Debug.WriteLine($"[PASTE_CMD] Files from clipboard: {files.Count}");

                    Dispatcher.InvokeAsync(async () => await ProcessDroppedFiles(files));

                    e.Handled = true;
                }
                else if (Clipboard.ContainsImage())
                {
                    System.Diagnostics.Debug.WriteLine("[PASTE_CMD] Image on clipboard; processing");
                    Dispatcher.InvokeAsync(async () => await HandleClipboardImageAsync());
                    e.Handled = true;
                }
                else if (Clipboard.ContainsText())
                {
                    System.Diagnostics.Debug.WriteLine("[PASTE_CMD] Clipboard contains text; inserting");
                    var tb = sender as TextBox ?? MessageTextBox;
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text) && tb != null)
                    {
                        var selStart = tb.SelectionStart;
                        tb.Text = tb.Text.Insert(selStart, text);
                        tb.SelectionStart = selStart + text.Length;
                    }

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PASTE-CMD] Error: {ex.Message}");
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
                if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    System.Diagnostics.Debug.WriteLine("[PASTE-KEY] Ctrl+V detected");

                    if (Clipboard.ContainsFileDropList())
                    {
                        var sc = Clipboard.GetFileDropList();
                        var files = sc.Cast<string>().ToList();
                        _ = ProcessDroppedFiles(files);
                        e.Handled = true;
                        return;
                    }

                    if (Clipboard.ContainsText())
                    {
                        var text = Clipboard.GetText();
                        var tb = sender as TextBox ?? MessageTextBox;
                        if (!string.IsNullOrEmpty(text) && tb != null)
                        {
                            var selStart = tb.SelectionStart;
                            tb.Text = tb.Text.Insert(selStart, text);
                            tb.SelectionStart = selStart + text.Length;
                        }
                        e.Handled = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PASTE-KEY] Error: {ex.Message}");
            }
        }

        private string SaveBitmapSourceToTempPng(BitmapSource source)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"edemly_clip_{Guid.NewGuid():N}.png");

                var encoder = new PngBitmapEncoder();

                var frame = BitmapFrame.Create(source);
                encoder.Frames.Add(frame);

                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    encoder.Save(fs);
                }

                System.Diagnostics.Debug.WriteLine($"[PASTE-IMG] Saved clipboard image to: {tempFile}");
                return tempFile;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PASTE-IMG] Failed to save clipboard image: {ex.Message}");
                return null;
            }
        }

        private string ShowImagePreviewAndCaptionDialog(string imagePath)
        {
            try
            {
                var input = new Window
                {
                    Title = "Send image",
                    Width = 560,
                    Height = 420,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.CanResize,
                    Owner = Application.Current.MainWindow
                };

                var grid = new Grid { Margin = new Thickness(12) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var img = new System.Windows.Controls.Image { Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 8) };
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(imagePath);
                    bi.EndInit();
                    img.Source = bi;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ATTACH-PREVIEW] Failed to load image preview (show dialog): {ex}"); }

                Grid.SetRow(img, 0); grid.Children.Add(img);

                var tb = new TextBox { Text = string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80, Padding = new Thickness(8) };
                Grid.SetRow(tb, 1); grid.Children.Add(tb);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
                Grid.SetRow(buttons, 2);
                var cancel = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
                cancel.Click += (s, e) => input.DialogResult = false;
                var send = new Button { Content = "Send", Width = 80, Height = 30, Background = new SolidColorBrush(Color.FromRgb(5, 114, 114)), Foreground = Brushes.White };
                send.Click += (s, e) => input.DialogResult = true;
                buttons.Children.Add(cancel); buttons.Children.Add(send); grid.Children.Add(buttons);

                input.Content = grid;

                var result = input.ShowDialog();
                if (result == true)
                {
                    return tb.Text?.Trim() ?? string.Empty;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PASTE-IMG] Preview dialog failed: {ex.Message}");
                return null;
            }
        }

        private async Task HandleClipboardImageAsync()
        {
            try
            {
                if (chatManager.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat", "Error");
                    return;
                }

                var source = Clipboard.GetImage();
                if (source == null)
                {
                    System.Diagnostics.Debug.WriteLine("[PASTE-IMG] Clipboard.GetImage returned null");
                    return;
                }

                var tempPath = SaveBitmapSourceToTempPng(source);
                if (string.IsNullOrEmpty(tempPath)) return;

                var caption = ShowImagePreviewAndCaptionDialog(tempPath);
                if (caption == null)
                {
                    try { File.Delete(tempPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PASTE-IMG] Failed to delete temp image on cancel: {ex}"); }
                    return;
                }

                await SendFileAsync(tempPath, caption);

                try { File.Delete(tempPath); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PASTE-IMG] Failed to delete temp image: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PASTE-IMG] Error handling clipboard image: {ex.Message}");
                MessageBox.ShowError($"Error processing image: {ex.Message}", "Error");
            }
        }

        private void MessageTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DRAG] PreviewDragOver invoked. Data formats: {string.Join(",", e.Data.GetFormats())}");
                if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DRAG] PreviewDragOver error: {ex.Message}");
            }
        }

        private void MessageTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DROP] PreviewDrop invoked. Formats: {string.Join(",", e.Data.GetFormats())}");
                if (chatManager.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat", "Error");
                    return;
                }

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    System.Diagnostics.Debug.WriteLine($"[DROP] FileDrop count: {files.Length}");
                    e.Handled = true;
                    _ = ProcessDroppedFiles(files);
                }
                else if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
                {
                    var text = e.Data.GetData(DataFormats.UnicodeText) as string ?? e.Data.GetData(DataFormats.Text) as string;
                    System.Diagnostics.Debug.WriteLine($"[DROP] TextDrop: {(text?.Length ?? 0)} chars");
                    if (!string.IsNullOrEmpty(text) && MessageTextBox != null)
                    {
                        var selStart = MessageTextBox.SelectionStart;
                        MessageTextBox.Text = MessageTextBox.Text.Insert(selStart, text);
                        MessageTextBox.SelectionStart = selStart + text.Length;
                    }
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DROP] Error handling drop: {ex.Message}");
            }
        }

        private async Task ProcessDroppedFiles(IEnumerable<string> files)
        {
            System.Diagnostics.Debug.WriteLine($"[FILES] ProcessDroppedFiles called with {files.Count()} items");
            if (chatManager.CurrentChatId < 0)
            {
                MessageBox.ShowWarning("First select a contact to chat", "Error");
                return;
            }

            foreach (var f in files)
            {
                try
                {
                    if (!File.Exists(f)) continue;

                    var caption = ShowAttachmentCaptionDialog(f);
                    if (caption == null) continue;

                    await SendFileAsync(f, caption);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FILES] Error processing file '{f}': {ex.Message}");
                }
            }
        }

        private async Task SendFileAsync(string filePath, string caption = "")
        {
            try
            {
                if (chatManager.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat", "Error");
                    return;
                }

                var fileInfo = new System.IO.FileInfo(filePath);
                if (fileInfo.Length > 50 * 1024 * 1024)
                {
                    MessageBox.ShowError("File size exceeds 50MB limit", "Error");
                    return;
                }

                AttachFileButton.IsEnabled = false;

                var uploadResult = await App.ApiService.UploadFileAsync(filePath);

                if (!uploadResult.Success)
                {
                    MessageBox.ShowError($"Failed to upload file: {uploadResult.Error}", "Error");
                    return;
                }

                var extension = System.IO.Path.GetExtension(filePath).ToLower();
                int messageType;

                if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif")
                {
                    messageType = 3;
                }
                else if (extension == ".pdf" || extension == ".doc" || extension == ".docx")
                {
                    messageType = 5;
                }
                else
                {
                    messageType = 4;
                }

                var message = new CreateMessageDto
                {
                    ChatId = chatManager.CurrentChatId,
                    Text = string.IsNullOrWhiteSpace(caption) ? string.Empty : caption,
                    Type = messageType,
                    ContentUrl = uploadResult.Url,
                    FileName = uploadResult.FileName
                };

                bool success = await App.HubService.SendMessageAsync(message);

                if (!success)
                {
                    MessageBox.ShowError("Failed to send file message", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"Error sending file: {ex.Message}", "Error");
            }
            finally
            {
                AttachFileButton.IsEnabled = true;
            }
        }
    }
}
