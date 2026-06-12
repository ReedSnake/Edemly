#nullable enable

using Edemly.Client.Application.Calls;
using Edemly.Client.Presentation.Windows.Calls;
using Edemly.Contracts.Calls;
using Edemly.Contracts.Realtime;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class MainPage
    {
        private void OnGroupCallUpdated(GroupCallEventDto groupCall)
        {
            if (groupCall == null || groupCall.ChatId <= 0)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                if (IsActiveGroupCall(groupCall))
                {
                    _activeGroupCallsByChatId[groupCall.ChatId] = groupCall;
                }
                else
                {
                    _activeGroupCallsByChatId.Remove(groupCall.ChatId);
                }

                RefreshActiveGroupCallBanner();
            });
        }

        private void RefreshActiveGroupCallBanner()
        {
            if (ActiveGroupCallBanner == null)
            {
                return;
            }

            var currentChatId = _chatController?.CurrentChatId ?? -1;
            if (currentChatId < 0
                || _chatController?.IsCurrentChatGroup() != true
                || !_activeGroupCallsByChatId.TryGetValue(currentChatId, out var groupCall)
                || !IsActiveGroupCall(groupCall))
            {
                ActiveGroupCallBanner.Visibility = Visibility.Collapsed;
                return;
            }

            var joinedCount = groupCall.Participants.Count(participant =>
                string.Equals(participant.Status, CallParticipantStatuses.Joined, StringComparison.OrdinalIgnoreCase));

            if (joinedCount == 0)
            {
                joinedCount = groupCall.Participants.Count;
            }

            var currentUserId = App.CurrentUserId;
            var currentUserJoined = currentUserId.HasValue
                && groupCall.Participants.Any(participant =>
                    participant.UserId == currentUserId.Value
                    && string.Equals(participant.Status, CallParticipantStatuses.Joined, StringComparison.OrdinalIgnoreCase));
            var currentSession = App.CallSessionState.Current;
            var currentSessionInThisCall = currentSession.CallId == groupCall.CallId
                && currentSession.Phase == CallSessionPhase.InCall;

            ActiveGroupCallTitleText.Text = "Group call";
            ActiveGroupCallMetaText.Text = $"{joinedCount} joined · started {groupCall.StartedAt.ToLocalTime():HH:mm}";
            ActiveGroupCallMetaText.Text = currentUserJoined || currentSessionInThisCall
                ? $"{joinedCount} joined - you are in this call"
                : $"{joinedCount} joined - started {NormalizeServerUtc(groupCall.StartedAt).ToLocalTime():HH:mm}";
            JoinActiveGroupCallButton.Content = currentUserJoined || currentSessionInThisCall
                ? "Open"
                : "Join";
            ActiveGroupCallBanner.Visibility = Visibility.Visible;
        }

        private async void JoinActiveGroupCallButton_Click(object sender, RoutedEventArgs e)
        {
            var currentChatId = _chatController?.CurrentChatId ?? -1;
            if (currentChatId < 0 || !_activeGroupCallsByChatId.TryGetValue(currentChatId, out var groupCall))
            {
                RefreshActiveGroupCallBanner();
                return;
            }

            try
            {
                var callWindow = GetOrCreateCallWindowForGroupJoin();
                await App.CallSessionController.JoinGroupCallAsync(groupCall);

                if (!callWindow.IsVisible)
                {
                    callWindow.Show();
                }

                callWindow.WindowState = WindowState.Normal;
                callWindow.Activate();
                callWindow.ShowCurrentSession();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.ShowWarning(ex.Message, DefaultLanguage.WarningTitle);
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"{DefaultLanguage.CallFailed}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private static bool IsActiveGroupCall(GroupCallEventDto groupCall)
        {
            return string.Equals(groupCall.Scope, CallScopes.Group, StringComparison.OrdinalIgnoreCase)
                && groupCall.EndedAt == null
                && (string.Equals(groupCall.Status, CallLifecycleStatuses.Active, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(groupCall.Status, CallLifecycleStatuses.Pending, StringComparison.OrdinalIgnoreCase));
        }

        private static DateTime NormalizeServerUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static CallWindow GetOrCreateCallWindowForGroupJoin()
        {
            var existing = System.Windows.Application.Current?.Windows.OfType<CallWindow>().FirstOrDefault();
            if (existing != null)
            {
                existing.RegisterHubHandlers();
                if (existing.Owner == null)
                {
                    existing.Owner = System.Windows.Application.Current?.MainWindow;
                }

                return existing;
            }

            var callWindow = new CallWindow(App.CallSessionController)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            callWindow.RegisterHubHandlers();
            return callWindow;
        }
    }
}
