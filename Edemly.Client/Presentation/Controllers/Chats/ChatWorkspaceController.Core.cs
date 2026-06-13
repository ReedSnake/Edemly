#nullable enable

using Edemly.Client.Application.Chats;
using Edemly.Client.Application.Users.Profile;
using Edemly.Client.Models;
using Edemly.Client.Presentation.Rendering.Chats;
using Edemly.Client.Presentation.Rendering.Messages;
using System.Windows;
using System.Windows.Controls;
using Edemly.Client.Api;
using Edemly.Client.Infrastructure.Realtime;
using Edemly.Contracts.Messages;
using Edemly.Client.Infrastructure.Caching;
using Edemly.Contracts.ChatMembers;
using Edemly.Contracts.Users;
using Edemly.Contracts.Chats;
namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController : IDisposable
    {
        private StackPanel _messagesPanel;
        private ScrollViewer? _messagesScrollViewer;
        private StackPanel _chatsPanel;
        private TextBlock _chatHeaderText;

        private Action<Contact?>? _updateChatHeaderCallback;

        private readonly ChatWorkspaceState _runtimeState = new();
        private Dictionary<int, Contact> _contacts => _runtimeState.Contacts;
        private Dictionary<int, List<MessageDto>> _chatHistory => _runtimeState.ChatHistory;
        private Dictionary<int, int> _chatToUserMap => _runtimeState.ChatToUserMap;
        private Dictionary<int, DateTime?> _lastMessageDate => _runtimeState.LastMessageDate;
        private Dictionary<int, DateTime> _chatLastMessageTime => _runtimeState.ChatLastMessageTime;
        private Dictionary<int, MessageDto> _chatLastMessage => _runtimeState.ChatLastMessage;
        private Dictionary<int, Contact> _chatContactsByChatId => _runtimeState.ChatContactsByChatId;
        private HashSet<int> _chatsWithUnreadMessages => _runtimeState.ChatsWithUnreadMessages;
        private Dictionary<int, int> _chatTypes => _runtimeState.ChatTypes;
        private Dictionary<int, Contact> _groupContacts => _runtimeState.GroupContacts;
        private Dictionary<int, string> _userNamesCache => _runtimeState.UserNamesCache;
        private Dictionary<int, int> _chatLoadedPages => _runtimeState.ChatLoadedPages;
        private HashSet<int> _loadingOlderChats => _runtimeState.LoadingOlderChats;
        private HashSet<int> _noMoreOlderMessages => _runtimeState.NoMoreOlderMessages;

        private readonly IHubService _hubService;
        private readonly IApiClients _apiClient;

        private readonly ChatLoader _chatLoader;
        private readonly MessageRenderer _messageRenderer;
        private readonly ChatListItemBuilder _chatListItemBuilder;
        private readonly ChatListItemStateFactory _chatListItemStateFactory;
        private readonly UserSearchHandler _searchHandler;

        private readonly ChatCache _cache;
        private System.Threading.Timer? _cacheCleanupTimer;
        private System.Threading.Timer? _presenceTimer;

        private Dictionary<int, (bool IsOnline, DateTime? LastSeenUtc, DateTime ExpiresAtUtc)> _userStatusCache => _runtimeState.UserStatusCache;
        private object _statusCacheLock => _runtimeState.StatusCacheLock;
        private readonly TimeSpan _statusTtl = TimeSpan.FromSeconds(60);

        private System.Threading.Timer? _sortDebounceTimer;
        private readonly object _sortLock = new object();
        private readonly TimeSpan _sortDebouncePeriod = TimeSpan.FromMilliseconds(250);

        private const string DEFAULT_AVATAR_PATH = "pack://application:,,,/Assets/Avatars/default-avatar.png";
        private const int INITIAL_MESSAGE_COUNT = 10;

        public Contact? CurrentChatContact
        {
            get => _runtimeState.CurrentChatContact;
            private set => _runtimeState.CurrentChatContact = value;
        }

        public int CurrentChatId
        {
            get => _runtimeState.CurrentChatId;
            set => _runtimeState.CurrentChatId = value;
        }

        public int CurrentUserId { get; private set; }

        public ChatWorkspaceController(ChatWorkspaceBindings uiBindings, int currentUserId)
        {
            _messagesPanel = uiBindings.MessagesPanel;
            _messagesScrollViewer = uiBindings.MessagesScrollViewer;
            _chatsPanel = uiBindings.ChatsPanel;
            _chatHeaderText = uiBindings.ChatHeaderText;
            CurrentUserId = currentUserId;
            _updateChatHeaderCallback = uiBindings.UpdateChatHeaderCallback;

            _hubService = App.HubService;
            _cache = App.GlobalChatCache;
            _apiClient = App.ApiClients;

            _chatLoader = new ChatLoader(_apiClient, _cache);
            _messageRenderer = new MessageRenderer(_messagesPanel, CurrentUserId);
            _chatListItemBuilder = new ChatListItemBuilder();
            _chatListItemStateFactory = new ChatListItemStateFactory(_runtimeState, CurrentUserId);
            _searchHandler = new UserSearchHandler(_apiClient, CurrentUserId);

            _hubService.MessageReceived += OnMessageReceived;
            _hubService.MessageUpdated += OnMessageUpdated;
            _hubService.MessageDeleted += OnMessageDeleted;
            _hubService.GroupCreated += OnGroupCreated;
            _hubService.GroupUpdated += OnGroupUpdated;
            _hubService.ProfileUpdated += OnProfileUpdated;

            _hubService.UserStatusChanged += OnHubUserStatusChanged;

            try
            {
                if (_messagesScrollViewer != null)
                {
                    _messagesScrollViewer.ScrollChanged -= MessagesScrollViewer_ScrollChanged;
                    _messagesScrollViewer.ScrollChanged += MessagesScrollViewer_ScrollChanged;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to attach scroll handler: {ex}"); }

            if (_cacheCleanupTimer == null)
            {
                _cacheCleanupTimer = new System.Threading.Timer(
                    _ => _cache.CleanupExpiredEntries(),
                    null,
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5));
            }

            try
            {
                _presenceTimer = new System.Threading.Timer(async _ => await PresenceTimerTickAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to start presence timer: {ex.Message}");
            }
        }

        #region Public Methods

        public async Task LoadExistingChatsAsync()
        {
            try
            {
                var chats = await _apiClient.Chats.GetMyChatsAsync();

                if (chats == null || chats.Count == 0)
                {
                    return;
                }

                foreach (var chat in chats)
                {
                    ApplyChatSummary(chat);
                    EnsureChatListContact(chat);
                }

                SortAllChats();
                HydratePrivateChatsInBackground(chats.Where(c => c.Type == 0).ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadExistingChatsAsync error: {ex.Message}");
            }
        }

        public async Task SearchAndCreateChatAsync(string searchText, TextBox searchTextBox, StackPanel resultsPanel)
        {
            await _searchHandler.SearchAndDisplayResultsAsync(
                searchText,
                searchTextBox,
                resultsPanel,
                CreateChatWithUserAsync);
        }

        public async Task SendMessageAsync(string messageText)
        {
            if (CurrentChatId < 0 || string.IsNullOrWhiteSpace(messageText))
                return;

            try
            {
                var message = new CreateMessageDto
                {
                    ChatId = CurrentChatId,
                    Text = messageText,
                    Type = 0,
                    ContentUrl = null
                };

                bool success = await _hubService.SendMessageAsync(message);

                if (!success)
                {
                    MessageBox.Show("Failed to send message", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                TryPlayNotificationSound();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateUiBindings(ChatWorkspaceBindings uiBindings)
        {
            _messagesPanel = uiBindings.MessagesPanel;
            _messagesScrollViewer = uiBindings.MessagesScrollViewer;
            _chatsPanel = uiBindings.ChatsPanel;
            _chatHeaderText = uiBindings.ChatHeaderText;
            _updateChatHeaderCallback = uiBindings.UpdateChatHeaderCallback;

            _messageRenderer.UpdateMessagesPanel(_messagesPanel);
        }

        public async Task RestoreUIAsync()
        {
            _chatsPanel.Children.Clear();
            SortAllChats();

            if (CurrentChatId >= 0 && CurrentChatContact != null)
            {
                NotifyCurrentChatHeader();

                try
                {
                    if (_chatsWithUnreadMessages.Contains(CurrentChatId))
                    {
                        _chatsWithUnreadMessages.Remove(CurrentChatId);
                        UpdateChatButton(CurrentChatId);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RestoreUIAsync unread marker clear error: {ex.Message}"); }

                try
                {
                    if (!_chatHistory.TryGetValue(CurrentChatId, out var cached) || cached == null || cached.Count == 0)
                    {
                        await LoadChatMessagesAsync(CurrentChatId, pageSize: INITIAL_MESSAGE_COUNT);
                        _chatLoadedPages[CurrentChatId] = 1;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RestoreUIAsync: failed to load messages for chat {CurrentChatId}: {ex.Message}");
                }

                await RestoreChatMessagesAsync(CurrentChatId);
            }
            else
            {
                CurrentUserProfileState.CurrentChatIdNotification = -1;
                _updateChatHeaderCallback?.Invoke(null);
            }
        }

        private async Task RestoreChatMessagesAsync(int chatId)
        {
            _messagesPanel.Children.Clear();

            if (_chatHistory.TryGetValue(chatId, out var messages))
            {
                _lastMessageDate[chatId] = null;

                if (_chatTypes.TryGetValue(chatId, out var chatType) && chatType == 1)
                {
                    _messageRenderer.SetGroupChatMode(true);
                }
                else
                {
                    _messageRenderer.SetGroupChatMode(false);
                }

                foreach (var message in messages)
                {
                    AddDateSeparatorIfNeeded(chatId, message.SentAt);

                    string? senderName = null;
                    if (chatType == 1 && message.SenderId != CurrentUserId)
                    {
                        senderName = await GetUserNameAsync(message.SenderId);
                    }

                    _messageRenderer.RenderMessage(message, isHistorical: true, senderName);
                }

                _messagesScrollViewer?.ScrollToEnd();
            }
        }

        private async Task SwitchToChatAsync(Models.Contact contact, int chatId)
        {
            try
            {
                if (contact == null) return;

                var previousChatId = CurrentChatId;

                CurrentChatContact = contact;
                CurrentChatId = chatId;
                CurrentUserProfileState.CurrentChatIdNotification = chatId;

                System.Diagnostics.Debug.WriteLine($"[SWITCH CHAT] Switching to chat {chatId}, Previous: {previousChatId}, CurrentChatId is now: {CurrentChatId}");

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    NotifyCurrentChatHeader();
                    SortAllChats();
                });

                if (_chatsWithUnreadMessages.Contains(chatId))
                {
                    _chatsWithUnreadMessages.Remove(chatId);
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => _messagesPanel.Children.Clear());

                if (!_chatHistory.TryGetValue(chatId, out var local) || local == null || local.Count == 0)
                {
                    await LoadChatMessagesAsync(chatId, pageSize: INITIAL_MESSAGE_COUNT);
                    _chatLoadedPages[chatId] = 1;
                }

                if (_chatTypes.TryGetValue(chatId, out var ct) && ct == 1)
                    _messageRenderer.SetGroupChatMode(true);
                else
                    _messageRenderer.SetGroupChatMode(false);

                await RestoreChatMessagesAsync(chatId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] SwitchToChatAsync error: {ex.Message}");
            }
        }

        public async Task SwitchToChatPublicAsync(Models.Contact contact, int chatId)
        {
            await SwitchToChatAsync(contact, chatId);
        }

        private async Task LoadChatMessagesAsync(int chatId, int pageSize = INITIAL_MESSAGE_COUNT)
        {
            try
            {
                var messages = await _chatLoader.LoadChatMessagesAsync(chatId, page: 1, pageSize: pageSize);
                if (messages == null) messages = new List<MessageDto>();

                var ordered = messages.OrderBy(m => m.SentAt).ToList();

                _chatHistory[chatId] = ordered;

                try
                {
                    _cache.AddMessages(chatId, ordered);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadChatMessagesAsync cache error: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadChatMessagesAsync error: {ex.Message}");
            }
        }

        private void AddDateSeparatorIfNeeded(int chatId, DateTime sentAt)
        {
            try
            {
                DateTime? last = null;
                if (_lastMessageDate.TryGetValue(chatId, out var ld)) last = ld;

                if (last.HasValue && last.Value.Date == sentAt.Date)
                    return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var sep = _chatListItemBuilder.CreateDateSeparator(sentAt);
                    if (sep != null)
                    {
                        _messagesPanel.Children.Add(sep);
                    }
                });

                _lastMessageDate[chatId] = sentAt.Date;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] AddDateSeparatorIfNeeded error: {ex.Message}");
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void ApplyChatSummary(ChatDto chat)
        {
            _chatTypes[chat.Id] = chat.Type;
            _chatLastMessageTime[chat.Id] = chat.LastMessageTime ?? chat.CreatedAt;

            if (string.IsNullOrWhiteSpace(chat.LastMessageText) &&
                !chat.LastMessageSenderId.HasValue)
            {
                _chatLastMessage.Remove(chat.Id);
                return;
            }

            var lastMessageTime = chat.LastMessageTime ?? chat.CreatedAt;
            if (_chatLastMessage.TryGetValue(chat.Id, out var existing) &&
                existing.SentAt > lastMessageTime)
            {
                return;
            }

            _chatLastMessage[chat.Id] = new MessageDto
            {
                ChatId = chat.Id,
                SenderId = chat.LastMessageSenderId ?? 0,
                Text = chat.LastMessageText ?? string.Empty,
                SentAt = lastMessageTime,
                Type = MessageTypeCodes.Text
            };
        }

        private void EnsureChatListContact(ChatDto chat)
        {
            if (chat.Type == 0)
            {
                EnsureDirectChatSummaryContact(chat);
                return;
            }

            EnsureGroupChatSummaryContact(chat);
        }

        private void EnsureDirectChatSummaryContact(ChatDto chat)
        {
            var displayName = string.IsNullOrWhiteSpace(chat.Name)
                ? $"Chat {chat.Id}"
                : chat.Name;

            if (!_chatContactsByChatId.TryGetValue(chat.Id, out var contact))
            {
                contact = new Models.Contact(0, displayName, string.Empty, photoPath: DEFAULT_AVATAR_PATH);
                _chatContactsByChatId[chat.Id] = contact;
            }
            else if (!string.Equals(contact.Name, displayName, StringComparison.Ordinal))
            {
                contact.Name = displayName;
            }

            if (!_chatToUserMap.TryGetValue(chat.Id, out var userId) || userId <= 0)
            {
                _chatToUserMap[chat.Id] = 0;
            }
        }

        private void EnsureGroupChatSummaryContact(ChatDto chat)
        {
            var photoPath = string.IsNullOrEmpty(chat.IconUrl) ? DEFAULT_AVATAR_PATH : chat.IconUrl;
            var groupName = string.IsNullOrWhiteSpace(chat.Name)
                ? $"Group {chat.Id}"
                : chat.Name;

            if (!_groupContacts.TryGetValue(chat.Id, out var contact))
            {
                contact = Models.Contact.CreateGroup(chat.Id, groupName, photoPath);
                _groupContacts[chat.Id] = contact;
            }
            else
            {
                contact.Name = groupName;
                contact.PhotoPath = photoPath;
            }

            _chatContactsByChatId[chat.Id] = contact;
            _chatToUserMap[chat.Id] = -chat.Id;
        }

        private void HydratePrivateChatsInBackground(List<ChatDto> privateChats)
        {
            if (privateChats.Count == 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var privateChatIds = privateChats.Select(chat => chat.Id).ToList();
                    var chatMembersMap = await _chatLoader.LoadChatMembersBatchAsync(privateChatIds);

                    var userIds = chatMembersMap.Values
                        .SelectMany(members => members)
                        .Where(member => member.UserId != CurrentUserId)
                        .Select(member => member.UserId)
                        .Distinct()
                        .ToList();

                    var usersMap = userIds.Count == 0
                        ? new Dictionary<int, UserDto>()
                        : await _chatLoader.LoadUsersBatchAsync(userIds);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var chat in privateChats)
                        {
                            LoadAndAddPrivateChatOptimizedAsync(chat, chatMembersMap, usersMap).GetAwaiter().GetResult();
                            UpdateChatButtonIfExists(chat.Id);
                        }
                    });

                    await RefreshAllPrivateChatStatusesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] HydratePrivateChatsInBackground error: {ex.Message}");
                }
            });
        }

        private Task LoadAndAddPrivateChatOptimizedAsync(
            ChatDto chat,
            Dictionary<int, List<ChatMemberDto>>? chatMembersMap,
            Dictionary<int, UserDto>? usersMap)
        {
            try
            {
                if (_chatToUserMap.TryGetValue(chat.Id, out var existingUserId) &&
                    existingUserId > 0 &&
                    _contacts.ContainsKey(existingUserId))
                {
                    return Task.CompletedTask;
                }

                if (chatMembersMap == null || !chatMembersMap.TryGetValue(chat.Id, out var members) || members.Count == 0)
                {
                    return Task.CompletedTask;
                }

                var otherMember = members.FirstOrDefault(m => m.UserId != CurrentUserId);
                if (otherMember == null)
                {
                    return Task.CompletedTask;
                }

                if (usersMap == null || !usersMap.TryGetValue(otherMember.UserId, out var user))
                {
                    return Task.CompletedTask;
                }

                var contact = Models.Contact.FromUserDto(user);

                lock (_contacts)
                {
                    _contacts[contact.UserId] = contact;
                }

                lock (_chatToUserMap)
                {
                    _chatToUserMap[chat.Id] = contact.UserId;
                }

                lock (_chatContactsByChatId)
                {
                    _chatContactsByChatId[chat.Id] = contact;
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadAndAddPrivateChatOptimizedAsync error: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        private Task LoadAndAddGroupChatAsync(ChatDto chat)
        {
            try
            {
                EnsureGroupChatSummaryContact(chat);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadAndAddGroupChatAsync error: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        private Task EnsureChatExistsAsync(int chatId, int userId)
        {
            if (_chatToUserMap.ContainsKey(chatId))
                return Task.CompletedTask;

            return Task.Run(async () =>
            {
                try
                {
                    var user = await _chatLoader.GetUserWithCacheAsync(userId);
                    if (user != null)
                    {
                        var contact = Models.Contact.FromUserDto(user);

                        _contacts[user.Id] = contact;
                        _chatToUserMap[chatId] = userId;

                        _chatTypes[chatId] = 0;

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddChatToList(chatId);
                        });
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] EnsureChatExistsAsync error: {ex.Message}"); }
            });
        }

        private async Task CreateChatWithUserAsync(UserDto user)
        {
            try
            {
                var existingChatId = _chatToUserMap.FirstOrDefault(x => x.Value == user.Id).Key;

                Models.Contact contact;

                if (_contacts.ContainsKey(user.Id))
                {
                    contact = _contacts[user.Id];
                }
                else
                {
                    contact = Models.Contact.FromUserDto(user);
                    _contacts[user.Id] = contact;
                }

                var chat = await _apiClient.Chats.CreateOrGetPrivateChatAsync(user.Id);

                if (chat == null)
                {
                    MessageBox.Show("Failed to create chat", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int chatId = chat.Id;

                if (!_chatToUserMap.ContainsKey(chatId))
                {
                    _chatToUserMap[chatId] = user.Id;
                    _chatLastMessageTime[chatId] = DateTime.UtcNow;
                    _chatTypes[chatId] = chat.Type;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddChatToList(chatId);
                        SortChatsAndMoveToTop(chatId);
                    });
                }

                await SwitchToChatAsync(contact, chatId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create chat: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MessagesScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            try
            {
                if (e.VerticalOffset <= 1)
                {
                    var chatId = CurrentChatId;
                    if (chatId < 0) return;

                    if (_loadingOlderChats.Contains(chatId) || _noMoreOlderMessages.Contains(chatId)) return;

                    await LoadOlderMessagesAsync(chatId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] ScrollChanged error: {ex.Message}");
            }
        }

        private async Task LoadOlderMessagesAsync(int chatId)
        {
            if (chatId < 0) return;

            try
            {
                _loadingOlderChats.Add(chatId);

                int nextPage = 1;
                if (_chatLoadedPages.TryGetValue(chatId, out var loaded)) nextPage = loaded + 1;

                double oldOffset = 0;
                double oldExtent = 0;
                try {
                    if (_messagesScrollViewer != null)
                    {
                        oldOffset = _messagesScrollViewer.VerticalOffset;
                        oldExtent = _messagesScrollViewer.ExtentHeight;
                    }
                } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadOlderMessagesAsync offset measure error: {ex.Message}"); }

                var newMessages = await _chatLoader.LoadChatMessagesAsync(chatId, page: nextPage, pageSize: INITIAL_MESSAGE_COUNT);

                if (newMessages == null || newMessages.Count == 0)
                {
                    _noMoreOlderMessages.Add(chatId);
                    return;
                }

                var ordered = newMessages.OrderBy(m => m.SentAt).ToList();

                if (!_chatHistory.TryGetValue(chatId, out var existing))
                {
                    existing = new List<MessageDto>();
                    _chatHistory[chatId] = existing;
                }

                var toInsert = ordered.Where(m => !existing.Any(em => em.Id == m.Id)).ToList();
                if (toInsert.Count == 0)
                {
                    _chatLoadedPages[chatId] = nextPage; // still mark page advanced to avoid refetch loops
                    return;
                }

                existing.InsertRange(0, toInsert);

                _chatLoadedPages[chatId] = nextPage;

                if (chatId == CurrentChatId)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { _messagesPanel.Dispatcher.Invoke(() => { }); });

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => RefreshCurrentChatMessagesAsync());

                    await Task.Delay(10);
                    _messagesScrollViewer?.UpdateLayout();

                    try
                    {
                        if (_messagesScrollViewer != null)
                        {
                            double newExtent = _messagesScrollViewer.ExtentHeight;
                            double delta = newExtent - oldExtent;
                            var target = oldOffset + delta;

                            if (target < 0)
                                target = 0;

                            _messagesScrollViewer.ScrollToVerticalOffset(target);
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadOlderMessagesAsync scroll restore error: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadOlderMessagesAsync error: {ex.Message}");
            }
            finally
            {
                _loadingOlderChats.Remove(chatId);
            }
        }

        private async Task RefreshCurrentChatMessagesAsync()
        {
            if (CurrentChatId < 0 || !_chatHistory.ContainsKey(CurrentChatId))
                return;

            _messagesPanel.Children.Clear();
            _lastMessageDate[CurrentChatId] = null;

            var messages = _chatHistory[CurrentChatId];

            if (_chatTypes.TryGetValue(CurrentChatId, out var chatType) && chatType == 1)
            {
                _messageRenderer.SetGroupChatMode(true);
            }
            else
            {
                _messageRenderer.SetGroupChatMode(false);
            }

            foreach (var message in messages)
            {
                AddDateSeparatorIfNeeded(CurrentChatId, message.SentAt);

                string? senderName = null;
                if (chatType == 1 && message.SenderId != CurrentUserId)
                {
                    senderName = await GetUserNameAsync(message.SenderId);
                }

                _messageRenderer.RenderMessage(message, isHistorical: true, senderName);
            }

            _messagesScrollViewer?.ScrollToEnd();
        }

        private async Task<string> GetUserNameAsync(int userId)
        {
            if (_userNamesCache.TryGetValue(userId, out var cachedName))
            {
                return cachedName;
            }

            try
            {
                var user = await _apiClient.Users.GetUserByIdAsync(userId);
                if (user != null)
                {
                    _userNamesCache[userId] = user.Username;
                    return user.Username;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load username for {userId}: {ex.Message}");
            }

            return "Member";
        }

        #endregion Private Methods

        public async Task AddGroupChatAndSwitchAsync(Models.Contact groupContact, int chatId)
        {
            try
            {
                lock (_groupContacts)
                {
                    _groupContacts[chatId] = groupContact;
                }

                lock (_chatToUserMap)
                {
                    if (!_chatToUserMap.ContainsKey(chatId))
                        _chatToUserMap[chatId] = -chatId;
                }

                _chatTypes[chatId] = 1;

                if (!_chatLastMessageTime.ContainsKey(chatId))
                    _chatLastMessageTime[chatId] = DateTime.UtcNow;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AddChatToList(chatId);
                    SortChatsAndMoveToTop(chatId);
                });

                await SwitchToChatAsync(groupContact, chatId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] AddGroupChatAndSwitchAsync error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cacheCleanupTimer?.Dispose();
            _cacheCleanupTimer = null;

            _hubService.MessageReceived -= OnMessageReceived;
            _hubService.MessageUpdated -= OnMessageUpdated;
            _hubService.MessageDeleted -= OnMessageDeleted;
            _hubService.GroupCreated -= OnGroupCreated;
            _hubService.GroupUpdated -= OnGroupUpdated;
            _hubService.ProfileUpdated -= OnProfileUpdated;
            _hubService.UserStatusChanged -= OnHubUserStatusChanged;

            _presenceTimer?.Dispose();
            _presenceTimer = null;

            _sortDebounceTimer?.Dispose();
            _sortDebounceTimer = null;
        }

    }
}
