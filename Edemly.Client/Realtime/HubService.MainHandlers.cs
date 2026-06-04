using Edemly.Client.Pages.Settings;
using Edemly.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Realtime
{
    public partial class HubService
    {
        private void RegisterHandlers(HubConnection? conn)
        {
            if (conn == null) return;

            lock (_stateLock)
            {
                if (_handlersRegisteredSet.Contains(conn)) return;
                _handlersRegisteredSet.Add(conn);
            }

            conn.On<MessageDto>(HubMethods.ReceiveMessage, message =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageReceived?.Invoke(message);

                    try
                    {
                        var isFromMe = App.CurrentUserId.HasValue &&
                                       App.CurrentUserId.Value == message.SenderId;

                        var currentChat = MyInfo.currentChatIdNotification;

                        if (!isFromMe && message.ChatId != currentChat)
                        {
                            _ = _toastNotificationService.ShowMessageToastAsync(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[HUB SERVICE] Error in ReceiveMessage handler: {ex.Message}");
                    }
                });
            });

            conn.On<MessageDto>(HubMethods.ReceiveMessageUpdate, message =>
            {
                HubEventDispatcher.Dispatch(() =>
                    MessageUpdated?.Invoke(message));
            });

            conn.On<int>(HubMethods.SendNotifyReminder, async reminderId =>
            {
                try
                {
                    if (reminderId != 0)
                    {
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await _reminderNotificationService.ShowReminderToastAsync(reminderId);
                        });

                        await conn.InvokeAsync(HubMethods.ConfirmRemindingReceived, reminderId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to handle reminder notification: {ex}");
                }
            });

            conn.On<int, int>(HubMethods.ReceiveMessageDelete, (messageId, chatId) =>
            {
                HubEventDispatcher.Dispatch(() =>
                    MessageDeleted?.Invoke(messageId, chatId));
            });

            conn.On<object>(HubMethods.GroupCreated, data =>
            {
                var groupData = HubPayloadParser.Deserialize<GroupChatCreatedDto>(
                    data,
                    HubMethods.GroupCreated);

                if (groupData != null)
                {
                    HubEventDispatcher.Dispatch(() =>
                        GroupCreated?.Invoke(groupData.ChatId));
                }
            });

            conn.On<object>(HubMethods.GroupUpdated, data =>
            {
                if (!HubPayloadParser.TryDeserialize<GroupUpdatedEventDto>(
                        data,
                        HubMethods.GroupUpdated,
                        out var payload) ||
                    payload == null ||
                    payload.ChatId == 0)
                {
                    return;
                }

                Debug.WriteLine(
                    $"[HUB PARSED] GroupUpdated -> chatId: {payload.ChatId}, name: {payload.Name}, iconUrl: {payload.IconUrl}");

                HubEventDispatcher.Dispatch(() =>
                    GroupUpdated?.Invoke(
                        payload.ChatId,
                        payload.Name,
                        payload.Description,
                        payload.IconUrl));
            });

            conn.On<object>(HubMethods.UserStatusChanged, data =>
            {
                var statusData = HubPayloadParser.Deserialize<UserStatusDto>(
                    data,
                    HubMethods.UserStatusChanged);

                if (statusData != null)
                {
                    Debug.WriteLine(
                        $"[HUB PARSED] UserStatusChanged -> userId: {statusData.UserId}, isOnline: {statusData.IsOnline}, lastSeen: {statusData.LastSeen}");

                    HubEventDispatcher.Dispatch(() =>
                        UserStatusChanged?.Invoke(
                            statusData.UserId,
                            statusData.IsOnline,
                            statusData.LastSeen));
                }
            });

            conn.On<object>(HubMethods.ProfileUpdated, data =>
            {
                if (!HubPayloadParser.TryDeserialize<ProfileUpdatedEventDto>(
                        data,
                        HubMethods.ProfileUpdated,
                        out var payload) ||
                    payload == null ||
                    payload.UserId == 0)
                {
                    return;
                }

                Debug.WriteLine(
                    $"[HUB PARSED] ProfileUpdated -> userId: {payload.UserId}, pfp: {payload.PfpUrl}");

                if (string.IsNullOrWhiteSpace(payload.PfpUrl))
                {
                    return;
                }

                HubEventDispatcher.Dispatch(() =>
                    ProfileUpdated?.Invoke(payload.UserId, payload.PfpUrl));
            });

            conn.Closed += async ex => await OnConnectionClosedInternalAsync(conn, ex);
            conn.Reconnecting += ex => OnReconnectingInternal(conn, ex);
            conn.Reconnected += id => OnReconnectedInternal(conn, id);
        }

        private void UnregisterHandlers(HubConnection? conn)
        {
            if (conn == null) return;

            try
            {
                conn.Remove(HubMethods.ReceiveMessage);
                conn.Remove(HubMethods.ReceiveMessageUpdate);
                conn.Remove(HubMethods.SendNotifyReminder);
                conn.Remove(HubMethods.ReceiveMessageDelete);
                conn.Remove(HubMethods.GroupCreated);
                conn.Remove(HubMethods.GroupUpdated);
                conn.Remove(HubMethods.UserStatusChanged);
                conn.Remove(HubMethods.ProfileUpdated);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HUB] Failed to unregister handlers: {ex}");
            }

            lock (_stateLock)
            {
                try
                {
                    _handlersRegisteredSet.Remove(conn);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HUB] Failed to remove connection from registered handlers: {ex}");
                }
            }
        }
    }
}