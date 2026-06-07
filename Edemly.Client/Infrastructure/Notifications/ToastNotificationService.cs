using CommunityToolkit.WinUI.Notifications;
namespace Edemly.Client.Infrastructure.Notifications
{
    public class ToastNotificationService
    {
        public async Task ShowMessageToastAsync(MessageDto content)
        {
            try
            {
                var sender = await App.ApiClients.Users.GetUserByIdAsync(content.SenderId);

                string senderName = sender?.Username ?? "Невідомий";
                string messageText = content.Text;
                string msg = messageText.Length > 100
                    ? senderName + ":" + messageText.Substring(0, 100) + "..."
                    : senderName + ":" + messageText;

                string title = "Нове повідомлення";

                var builder = new ToastContentBuilder()
                    .AddArgument("action", "viewChat")
                    .AddArgument("chatId", content.ChatId.ToString())
                    .AddText(title, AdaptiveTextStyle.Title)
                    .AddText(msg);

                builder.Show(toast =>
                {
                    toast.Tag = content.Id.ToString();
                    toast.Group = "chatNotifications";
                    toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(3);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка показу Toast: {ex}");
            }
        }
    }
}