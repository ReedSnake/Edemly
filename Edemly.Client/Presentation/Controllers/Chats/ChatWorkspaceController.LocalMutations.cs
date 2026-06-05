#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using Edemly.Client.Models;

namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController
    {
        public void UpdateMessageLocally(MessageDto updatedMessage)
        {
            try
            {
                if (_chatHistory.TryGetValue(updatedMessage.ChatId, out var messages))
                {
                    var idx = messages.FindIndex(m => m.Id == updatedMessage.Id);
                    if (idx >= 0)
                    {
                        messages[idx] = updatedMessage;
                    }
                }

                if (_chatLastMessage.ContainsKey(updatedMessage.ChatId) && _chatLastMessage[updatedMessage.ChatId].Id == updatedMessage.Id)
                {
                    _chatLastMessage[updatedMessage.ChatId] = updatedMessage;
                    UpdateChatButton(updatedMessage.ChatId);
                }

                if (updatedMessage.ChatId == CurrentChatId)
                {
                    _messageRenderer.UpdateMessageInUI(updatedMessage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateMessageLocally error: {ex.Message}");
            }
        }

        public void RemoveMessageLocally(int chatId, int messageId)
        {
            try
            {
                if (_chatHistory.TryGetValue(chatId, out var messages))
                {
                    messages.RemoveAll(m => m.Id == messageId);
                }

                if (_chatLastMessage.ContainsKey(chatId) && _chatLastMessage[chatId].Id == messageId)
                {
                    var lastMsg = _chatHistory.ContainsKey(chatId) ? _chatHistory[chatId].OrderByDescending(m => m.SentAt).FirstOrDefault() : null;
                    if (lastMsg != null)
                    {
                        _chatLastMessage[chatId] = lastMsg;
                    }
                    else
                    {
                        _chatLastMessage.Remove(chatId);
                    }
                    UpdateChatButton(chatId);
                }

                if (chatId == CurrentChatId)
                {
                    var border = FindBorderByMessageId(messageId);
                    if (border != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => _messagesPanel.Children.Remove(border));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RemoveMessageLocally error: {ex.Message}");
            }
        }

        private void TryPlayNotificationSound()
        {
            try
            {
                const string relativePath = "Assets\\Audio\\message-notification.wav";

                string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, relativePath);
                if (File.Exists(outPath))
                {
                    try
                    {
                        var player = new SoundPlayer(outPath);
                        player.Play();
                        return;
                    }
                    catch (Exception exFile)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Sound play from output file failed: {exFile.Message}");
                    }
                }

                try
                {
                    var packUri = new Uri("pack://application:,,,/Assets/Audio/message-notification.wav", UriKind.Absolute);
                    var resInfo = System.Windows.Application.GetResourceStream(packUri);
                    if (resInfo?.Stream != null)
                    {
                        string tmp = Path.Combine(Path.GetTempPath(), $"edemly_msg_spawn_{Guid.NewGuid():N}.wav");
                        using (var fs = File.Create(tmp))
                        {
                            resInfo.Stream.CopyTo(fs);
                        }

                        try
                        {
                            var player = new SoundPlayer(tmp);
                            player.Play();

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(10_000).ConfigureAwait(false);
                                    File.Delete(tmp);
                                }
                                catch (Exception exTmp) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Sound cleanup tmp file error: {exTmp.Message}"); }
                            });

                            return;
                        }
                        catch (Exception exTmp)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Sound play from temp file failed: {exTmp.Message}");
                        }
                    }
                }
                catch (Exception exPack)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Pack resource lookup failed: {exPack.Message}");
                }

                try
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    var resourceName = asm.GetManifestResourceNames()
                                      .FirstOrDefault(n => n.EndsWith("message-notification.wav", StringComparison.OrdinalIgnoreCase));
                    if (resourceName != null)
                    {
                        using (var s = asm.GetManifestResourceStream(resourceName))
                        {
                            if (s != null)
                            {
                                string tmp2 = Path.Combine(Path.GetTempPath(), $"edemly_msg_spawn_{Guid.NewGuid():N}.wav");
                                using (var fs = File.Create(tmp2))
                                {
                                    s.CopyTo(fs);
                                }

                                try
                                {
                                    var player = new SoundPlayer(tmp2);
                                    player.Play();

                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await Task.Delay(10_000).ConfigureAwait(false);
                                            File.Delete(tmp2);
                                        }
                                        catch (Exception exTmp2) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Sound cleanup embedded resource error: {exTmp2.Message}"); }
                                    });

                                    return;
                                }
                                catch (Exception exTmp2)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Sound play from embedded resource failed: {exTmp2.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception exAsm)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Embedded resource lookup failed: {exAsm.Message}");
                }

                System.Diagnostics.Debug.WriteLine("[CHAT MANAGER] Notification sound not found in output, pack URI, or embedded resources.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] TryPlayNotificationSound error: {ex.Message}");
            }
        }
    }
}
