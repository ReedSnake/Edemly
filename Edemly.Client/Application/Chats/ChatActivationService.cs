using Edemly.Client.Api;
using Edemly.Client.Presentation.Controllers.Chats;
using System.Diagnostics;
using System.Windows;
using Edemly.Client.Presentation.Windows;
namespace Edemly.Client.Application.Chats
{
    public sealed class ChatActivationService
    {
        private readonly Func<IApiService> _apiServiceProvider;
        private readonly Func<ChatWorkspaceController?> _chatControllerProvider;
        private readonly Func<int?> _currentUserIdProvider;
        private readonly Func<MainWindow> _ensureMainWindow;
        private readonly Dictionary<int, (ChatDto chat, List<ChatMemberDto> members)> _chatDataCache = new();
        private readonly object _chatCacheLock = new();

        public ChatActivationService(
            Func<IApiService> apiServiceProvider,
            Func<ChatWorkspaceController?> chatControllerProvider,
            Func<int?> currentUserIdProvider,
            Func<MainWindow> ensureMainWindow)
        {
            _apiServiceProvider = apiServiceProvider ?? throw new ArgumentNullException(nameof(apiServiceProvider));
            _chatControllerProvider = chatControllerProvider ?? throw new ArgumentNullException(nameof(chatControllerProvider));
            _currentUserIdProvider = currentUserIdProvider ?? throw new ArgumentNullException(nameof(currentUserIdProvider));
            _ensureMainWindow = ensureMainWindow ?? throw new ArgumentNullException(nameof(ensureMainWindow));
        }

        public void ClearCache()
        {
            lock (_chatCacheLock)
            {
                _chatDataCache.Clear();
            }
        }

        public async Task OpenChatWindowAsync(int chatId)
        {
            try
            {
                var mainWindow = _ensureMainWindow();
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }

                mainWindow.Activate();
                mainWindow.Focus();

                var chatController = await WaitForChatControllerAsync();
                if (chatController == null)
                {
                    Debug.WriteLine("[CHAT ACTIVATION] Chat controller not ready after waiting");
                    return;
                }

                var (chat, members) = await GetOrLoadChatDataAsync(chatId);
                if (chat == null)
                {
                    Debug.WriteLine($"[CHAT ACTIVATION] Chat {chatId} not found");
                    return;
                }

                var contact = await BuildContactAsync(chat, members);
                if (contact == null)
                {
                    Debug.WriteLine($"[CHAT ACTIVATION] Could not build contact for chat {chatId}");
                    return;
                }

                await SwitchToChatDirectAsync(chatController, contact, chatId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT ACTIVATION] OpenChatWindowAsync failed: {ex}");
            }
        }

        private async Task<ChatWorkspaceController?> WaitForChatControllerAsync()
        {
            int waitTime = 0;
            while (_chatControllerProvider() == null && waitTime < 2000)
            {
                await Task.Delay(100);
                waitTime += 100;
            }

            return _chatControllerProvider();
        }

        private async Task<(ChatDto? chat, List<ChatMemberDto> members)> GetOrLoadChatDataAsync(int chatId)
        {
            lock (_chatCacheLock)
            {
                if (_chatDataCache.TryGetValue(chatId, out var cachedData))
                {
                    return (cachedData.chat, cachedData.members);
                }
            }

            var apiService = _apiServiceProvider();
            var chatsTask = apiService.GetMyChatsAsync();
            var membersTask = apiService.GetChatMembersAsync(chatId);

            await Task.WhenAll(chatsTask, membersTask);

            var chats = await chatsTask;
            var members = await membersTask ?? new List<ChatMemberDto>();
            var chat = chats.FirstOrDefault(c => c.Id == chatId);

            if (chat != null)
            {
                lock (_chatCacheLock)
                {
                    _chatDataCache[chatId] = (chat, members);
                }
            }

            return (chat, members);
        }

        private async Task<Models.Contact?> BuildContactAsync(ChatDto chat, List<ChatMemberDto> members)
        {
            if (chat.Type == 0)
            {
                var currentUserId = _currentUserIdProvider();
                var otherMember = members.FirstOrDefault(member => member.UserId != currentUserId);
                if (otherMember == null)
                {
                    return null;
                }

                var user = await _apiServiceProvider().GetUserByIdAsync(otherMember.UserId);
                if (user == null)
                {
                    return null;
                }

                return Models.Contact.FromUserDto(user);
            }

            var groupPhotoPath = string.IsNullOrEmpty(chat.IconUrl)
                ? Models.Contact.DefaultAvatarPath
                : chat.IconUrl;

            var groupName = string.IsNullOrWhiteSpace(chat.Name)
                ? $"Group {chat.Id}"
                : chat.Name;

            return Models.Contact.CreateGroup(chat.Id, groupName, groupPhotoPath);
        }

        private static async Task SwitchToChatDirectAsync(ChatWorkspaceController chatController, Models.Contact contact, int chatId)
        {
            try
            {
                await chatController.SwitchToChatPublicAsync(contact, chatId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT ACTIVATION] SwitchToChatDirectAsync failed: {ex.Message}");
            }
        }
    }
}
