using CommunityToolkit.WinUI.Notifications;

namespace Edemly.Client.Realtime.Notifications
{
    public class ReminderNotificationService
    {
        public Task ShowReminderToastAsync(int remindingId)
        {
            try
            {
                string title = "Reminding deadline reached!";
                string msg = "Please check your tasks!";

                var builder = new ToastContentBuilder()
                    .AddArgument("action", "viewReminding")
                    .AddArgument("remindingId", remindingId.ToString())
                    .AddText(title, AdaptiveTextStyle.Title)
                    .AddText(msg);

                builder.Show(toast =>
                {
                    toast.Tag = remindingId.ToString();
                    toast.Group = "reminderNotifications";
                    toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(3);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show reminder toast: {ex}");
            }

            return Task.CompletedTask;
        }
    }
}