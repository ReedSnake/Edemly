#nullable enable

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class MainPage
    {
        private async Task RefreshActiveChatThemeAsync()
        {
            try
            {
                if (_chatController == null)
                {
                    return;
                }

                await _chatController.RefreshCurrentChatAppearanceAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] RefreshActiveChatThemeAsync error: {ex.Message}");
            }
        }
    }
}
