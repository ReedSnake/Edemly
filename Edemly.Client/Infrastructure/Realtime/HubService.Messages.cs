using Microsoft.AspNetCore.SignalR.Client;
namespace Edemly.Client.Infrastructure.Realtime
{
    public partial class HubService
    {
        public async Task<bool> SendMessageAsync(CreateMessageDto message)
        {
            if (!IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[HUB] SendMessageAsync called while not connected");
                return false;
            }

            try
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(message);
                    System.Diagnostics.Debug.WriteLine($"[HUB] Invoking SendMessage. ConnectionState={_connection?.State}; Payload={json}");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to serialize message for debug output: {ex}"); }

                var cts = new CancellationTokenSource(HubSettings.StartConnectionTimeout);
                await _connection!.InvokeAsync(HubMethods.SendMessage, message, cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    var state = _connection?.State.ToString() ?? "<null>";
                    System.Diagnostics.Debug.WriteLine($"[HUB][ERROR] SendMessageAsync failed. ConnectionState={state}. Exception={ex}");
                }
                catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to log SendMessageAsync error: {logEx}"); }

                ShowError("Помилка надсилання повідомлення", ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateMessageAsync(UpdateMessageDto message)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await _connection!.InvokeAsync(HubMethods.UpdateMessage, message, cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Помилка оновлення повідомлення", ex.Message);
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(int messageId, int chatId)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                await _connection!.InvokeAsync(HubMethods.DeleteMessage, messageId, chatId, cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Помилка видалення повідомлення", ex.Message);
                return false;
            }
        }

        public async Task<bool> NotifyProfileUpdateAsync(int userId, string newPfpUrl)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                await _connection!.InvokeAsync(HubMethods.NotifyProfileUpdated, userId, newPfpUrl);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify profile update: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> NotifyGroupUpdateAsync(int chatId, string? name, string? description, string? iconUrl)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                await _connection!.InvokeAsync(HubMethods.NotifyGroupUpdated, chatId, name, description, iconUrl);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify group update: {ex.Message}");
                return false;
            }
        }

        public async Task<object?> QueryUserStatusAsync(int userId)
        {
            if (!IsConnected || _connection == null)
                return null;

            try
            {
                var cts = new CancellationTokenSource(HubSettings.ShortOperationTimeout);
                var result = await _connection.InvokeAsync<object>(HubMethods.GetUserStatus, userId, cts.Token);
                if (result == null) return null;

                var json = System.Text.Json.JsonSerializer.Serialize(result);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var statusData = System.Text.Json.JsonSerializer.Deserialize<UserStatusDto>(json, options);
                return statusData as object;
            }
            catch (Exception ex)
            {
                try
                {
                    var fullType = ex.GetType()?.FullName ?? ex.GetType()?.Name ?? "<unknown>";
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] QueryUserStatusAsync exception type={fullType}; message={ex.Message}");

                    if ((ex.GetType()?.Name ?? string.Empty).IndexOf("HubException", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[HUB SERVICE] Server-side HubException: " + ex.Message);
                    }

                    var msg = ex.Message ?? string.Empty;
                    if (msg.IndexOf("Connection closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("closed the connection", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _ = OnConnectionClosedAsync(ex);
                    }
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB] Failed to log QueryUserStatusAsync error: {logEx}");
                }

                return null;
            }
        }
    }
}