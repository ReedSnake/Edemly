#nullable enable

using Edemly.Client.Api;
using Edemly.Client.Caching;
using Edemly.Client.Models;
using Edemly.Client.Realtime;
using Edemly.Client.UI.Helpers;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client
{
    public partial class ChatManager : IDisposable
    {
        private readonly StackPanel _messagesPanel;
        private readonly ScrollViewer? _messagesScrollViewer;
        private readonly StackPanel _chatsPanel;
        private readonly TextBlock _chatHeaderText;

        private Action<Contact?>? _updateChatHeaderCallback;

        private readonly Dictionary<int, Contact> _contacts = new Dictionary<int, Contact>();
        private readonly Dictionary<int, List<MessageDto>> _chatHistory = new Dictionary<int, List<MessageDto>>();
        private readonly Dictionary<int, int> _chatToUserMap = new Dictionary<int, int>();
        private readonly Dictionary<int, DateTime?> _lastMessageDate = new Dictionary<int, DateTime?>();
        private readonly Dictionary<int, DateTime> _chatLastMessageTime = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, MessageDto> _chatLastMessage = new Dictionary<int, MessageDto>();
        private readonly HashSet<int> _chatsWithUnreadMessages = new HashSet<int>();

        private readonly Dictionary<int, int> _chatTypes = new Dictionary<int, int>();

        private readonly Dictionary<int, Contact> _groupContacts = new Dictionary<int, Contact>();

        private readonly Dictionary<int, string> _userNamesCache = new Dictionary<int, string>();

        private readonly Dictionary<int, int> _chatLoadedPages = new Dictionary<int, int>();
        private readonly HashSet<int> _loadingOlderChats = new HashSet<int>();
        private readonly HashSet<int> _noMoreOlderMessages = new HashSet<int>();

        private readonly IHubService _hubService;
        private readonly IApiService _apiService;

        private readonly ChatLoader _chatLoader;
        private readonly MessageRenderer _messageRenderer;
        private readonly ChatUIBuilder _uiBuilder;
        private readonly UserSearchHandler _searchHandler;

        private readonly ChatCache _cache;
        private System.Threading.Timer? _cacheCleanupTimer;
        private System.Threading.Timer? _presenceTimer;

        private readonly Dictionary<int, (bool IsOnline, DateTime? LastSeenUtc, DateTime ExpiresAtUtc)> _userStatusCache = new();
        private readonly object _statusCacheLock = new object();
        private readonly TimeSpan _statusTtl = TimeSpan.FromSeconds(60);

        private System.Threading.Timer? _sortDebounceTimer;
        private readonly object _sortLock = new object();
        private readonly TimeSpan _sortDebouncePeriod = TimeSpan.FromMilliseconds(250);

        private const string DEFAULT_AVATAR_PATH = "pack://application:,,,/Assets/Avatars/default-avatar.png";
        private const int MAX_PARALLEL_LOADS = 5;
        private const int INITIAL_MESSAGE_COUNT = 10;

        public Contact? CurrentChatContact { get; private set; }
        public int CurrentChatId { get; set; } = -1;
        public int CurrentUserId { get; private set; }

        public ChatManager(
            StackPanel messagesPanel,
            ScrollViewer? messagesScrollViewer,
            StackPanel chatsPanel,
            TextBlock chatHeaderText,
            int currentUserId,
            Action<Contact?>? updateChatHeaderCallback = null)
        {
            _messagesPanel = messagesPanel;
            _messagesScrollViewer = messagesScrollViewer;
            _chatsPanel = chatsPanel;
            _chatHeaderText = chatHeaderText;
            CurrentUserId = currentUserId;
            _updateChatHeaderCallback = updateChatHeaderCallback;

            _hubService = App.HubService;
            _apiService = App.ApiService;
            _cache = App.GlobalChatCache;

            _chatLoader = new ChatLoader(_cache);
            _messageRenderer = new MessageRenderer(_messagesPanel, CurrentUserId);
            _uiBuilder = new ChatUIBuilder();
            _searchHandler = new UserSearchHandler(CurrentUserId);

            _hubService.MessageReceived += OnMessageReceived;
            _hubService.MessageUpdated += OnMessageUpdated;
            _hubService.MessageDeleted += OnMessageDeleted;
            _hubService.ConnectionStateChanged += OnConnectionStateChanged;

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

        private async Task PresenceTimerTickAsync()
        {
            try
            {
                if (_hubService == null) return;

                await RefreshAllPrivateChatStatusesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] PresenceTimerTickAsync error: {ex.Message}");
            }
        }

        private void RequestSortAllChatsDebounced()
        {
            try
            {
                lock (_sortLock)
                {
                    _sortDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _sortDebounceTimer = new System.Threading.Timer(_ =>
                    {
                        try { Application.Current?.Dispatcher?.Invoke(() => SortAllChats()); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] SortAllChats invoke failed: {ex}"); }
                    }, null, _sortDebouncePeriod, Timeout.InfiniteTimeSpan);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RequestSortAllChatsDebounced failed: {ex}"); }
        }

        private bool TryGetCachedStatus(int userId, out bool isOnline, out DateTime? lastSeenUtc)
        {
            isOnline = false;
            lastSeenUtc = null;
            try
            {
                lock (_statusCacheLock)
                {
                    if (_userStatusCache.TryGetValue(userId, out var entry))
                    {
                        if (DateTime.UtcNow <= entry.ExpiresAtUtc)
                        {
                            isOnline = entry.IsOnline;
                            lastSeenUtc = entry.LastSeenUtc;
                            return true;
                        }
                        else
                        {
                            _userStatusCache.Remove(userId);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] TryGetCachedStatus failed: {ex}"); }
            return false;
        }

        private void UpdateStatusCache(int userId, bool isOnline, DateTime? lastSeenUtc)
        {
            try
            {
                var expires = DateTime.UtcNow.Add(_statusTtl);
                lock (_statusCacheLock)
                {
                    _userStatusCache[userId] = (isOnline, lastSeenUtc, expires);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateStatusCache failed: {ex}"); }
        }

        public bool TryGetCachedUserStatus(int userId, out bool isOnline, out DateTime? lastSeenUtc)
        {
            return TryGetCachedStatus(userId, out isOnline, out lastSeenUtc);
        }

        public async Task<(bool Found, bool IsOnline, DateTime? LastSeenUtc)> RefreshUserStatusAsync(int userId)
        {
            try
            {
                var statusObj = await _hubService.QueryUserStatusAsync(userId);
                if (statusObj == null)
                {
                    return (false, false, null);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(statusObj);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var status = System.Text.Json.JsonSerializer.Deserialize<UserStatusDto>(json, options);
                if (status == null)
                {
                    return (false, false, null);
                }

                UpdateStatusCache(userId, status.IsOnline, status.LastSeen);

                var chatIds = _chatToUserMap
                    .Where(kv => kv.Value == userId)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var chatId in chatIds)
                {
                    UpdateChatButtonOnline(chatId, status.IsOnline);
                }

                return (true, status.IsOnline, status.LastSeen);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RefreshUserStatusAsync failed for user {userId}: {ex.Message}");
                return (false, false, null);
            }
        }

        private bool GetCachedOnlineForChat(int chatId)
        {
            if (_chatToUserMap.TryGetValue(chatId, out var userId) &&
                userId > 0 &&
                TryGetCachedStatus(userId, out var isOnline, out _))
            {
                return isOnline;
            }

            return false;
        }

        #region Public Methods

        public async Task LoadExistingChatsAsync()
        {
            try
            {
                var chats = await _apiService.GetMyChatsAsync();

                if (chats == null || chats.Count == 0)
                {
                    return;
                }

                var newChats = chats.Where(c => !_chatToUserMap.ContainsKey(c.Id)).ToList();

                if (newChats.Count == 0)
                {
                    foreach (var chat in chats.Where(c => _chatToUserMap.ContainsKey(c.Id)))
                    {
                        if (chat.LastMessageTime.HasValue)
                        {
                            _chatLastMessageTime[chat.Id] = chat.LastMessageTime.Value;
                        }
                    }
                    SortAllChats();
                    return;
                }

                foreach (var chat in chats)
                {
                    if (chat.LastMessageTime.HasValue)
                    {
                        _chatLastMessageTime[chat.Id] = chat.LastMessageTime.Value;
                    }
                    else
                    {
                        _chatLastMessageTime[chat.Id] = DateTime.UtcNow;
                    }
                    _chatTypes[chat.Id] = chat.Type;
                }

                var privateChats = newChats.Where(c => c.Type == 0).ToList();
                var groupChats = newChats.Where(c => c.Type == 1 || c.Type == 2).ToList();

                var privateChatIds = privateChats.Select(c => c.Id).ToList();
                Dictionary<int, List<ChatMemberDto>>? chatMembersMap = null;

                if (privateChatIds.Count > 0)
                {
                    chatMembersMap = await _chatLoader.LoadChatMembersBatchAsync(privateChatIds);
                }

                var userIds = new List<int>();
                if (chatMembersMap != null)
                {
                    foreach (var members in chatMembersMap.Values)
                    {
                        userIds.AddRange(members.Where(m => m.UserId != CurrentUserId).Select(m => m.UserId));
                    }
                }

                Dictionary<int, UserDto>? usersMap = null;
                if (userIds.Count > 0)
                {
                    usersMap = await _chatLoader.LoadUsersBatchAsync(userIds);
                }

                var privateChatTasks = privateChats.Select(chat =>
                    LoadAndAddPrivateChatOptimizedAsync(chat, chatMembersMap, usersMap)
                ).ToList();

                var groupChatTasks = groupChats.Select(chat =>
                    LoadAndAddGroupChatAsync(chat)
                ).ToList();

                var semaphore = new SemaphoreSlim(MAX_PARALLEL_LOADS);
                var allTasks = privateChatTasks.Concat(groupChatTasks).Select(async task =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await task;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(allTasks);

                SortAllChats();

                await RefreshAllPrivateChatStatusesAsync();

                await LoadLastMessageTextsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadExistingChatsAsync error: {ex.Message}");
            }
        }

        private async Task LoadLastMessageTextsAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    var visibleChatIds = _chatToUserMap.Keys
                        .OrderByDescending(chatId =>
                        {
                            if (_chatLastMessage.TryGetValue(chatId, out var lastMessage))
                            {
                                return lastMessage.SentAt;
                            }
                            else if (_chatLastMessageTime.TryGetValue(chatId, out var time))
                            {
                                return time;
                            }
                            else
                            {
                                return DateTime.MinValue;
                            }
                        })
                        .Take(15)
                        .ToList();

                    var semaphore = new SemaphoreSlim(5);
                    var tasks = visibleChatIds.Select(async chatId =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var messages = await _apiService.GetChatMessagesAsync(chatId, page: 1, pageSize: 1);
                            if (messages.Count > 0)
                            {
                                var lastMessage = messages[0];
                                _chatLastMessage[chatId] = lastMessage;

                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    UpdateChatButtonIfExists(chatId);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadLastMessageTextsAsync error: {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] LoadLastMessageTextsAsync outer error: {ex.Message}");
                }
            });
        }

        private void UpdateChatButtonIfExists(int chatId)
        {
            var chatButton = _chatsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is int id && id == chatId);

            if (chatButton != null)
            {
                UpdateChatButton(chatId);
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
                    Edemly.Client.Pages.MessageBox.Show("Failed to send message", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                TryPlayNotificationSound();
            }
            catch (Exception ex)
            {
                Edemly.Client.Pages.MessageBox.Show($"Failed to send message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CreateDefaultChat()
        {
        }

        public void UpdateUIElements(
            StackPanel messagesPanel,
            ScrollViewer messagesScrollViewer,
            StackPanel chatsPanel,
            TextBlock chatHeaderText)
        {
            typeof(ChatManager).GetField("_messagesPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, messagesPanel);
            typeof(ChatManager).GetField("_messagesScrollViewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, messagesScrollViewer);
            typeof(ChatManager).GetField("_chatsPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, chatsPanel);
            typeof(ChatManager).GetField("_chatHeaderText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, chatHeaderText);

            _messageRenderer.UpdateMessagesPanel(messagesPanel);
        }

        public async Task RestoreUIAsync()
        {
            _chatsPanel.Children.Clear();
            SortAllChats();

            if (CurrentChatId >= 0 && CurrentChatContact != null)
            {
                _chatHeaderText.Text = CurrentChatContact.Name;
                _updateChatHeaderCallback?.Invoke(CurrentChatContact);

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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MakeTextLinksClickable(FindBorderByMessageId(message.Id), message.Text);
                    });
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

                System.Diagnostics.Debug.WriteLine($"[SWITCH CHAT] Switching to chat {chatId}, Previous: {previousChatId}, CurrentChatId is now: {CurrentChatId}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _chatHeaderText.Text = contact.Name;
                    _updateChatHeaderCallback?.Invoke(contact);

                    SortAllChats();
                });

                if (_chatsWithUnreadMessages.Contains(chatId))
                {
                    _chatsWithUnreadMessages.Remove(chatId);
                }

                Application.Current.Dispatcher.Invoke(() => _messagesPanel.Children.Clear());

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

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var sep = _uiBuilder.CreateDateSeparator(sentAt);
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

        private DateTime GetChatLastActivity(int chatId)
        {
            try
            {
                DateTime last = DateTime.MinValue;

                if (_chatLastMessage.TryGetValue(chatId, out var lastMessage) && lastMessage != null)
                {
                    last = lastMessage.SentAt;
                }

                if (_chatLastMessageTime.TryGetValue(chatId, out var time))
                {
                    if (time > last) last = time;
                }

                return last;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] GetChatLastActivity error: {ex.Message}");
                return DateTime.MinValue;
            }
        }

        private void SortAllChats()
        {
            var sortedChatIds = _chatToUserMap.Keys
                .OrderByDescending(chatId => GetChatLastActivity(chatId))
                .ThenBy(chatId => chatId) // deterministic tie-breaker
                .ToList();

            _chatsPanel.Children.Clear();

            foreach (var chatId in sortedChatIds)
            {
                var userId = _chatToUserMap[chatId];

                Contact? contact;
                if (userId < 0)
                {
                    if (!_groupContacts.TryGetValue(chatId, out contact))
                        continue;
                }
                else
                {
                    if (!_contacts.TryGetValue(userId, out contact))
                        continue;
                }

                string? lastMessageText = null;
                string? lastMessageSender = null;
                DateTime? lastMessageTime = null;
                bool hasUnread = _chatsWithUnreadMessages.Contains(chatId);

                if (_chatLastMessage.TryGetValue(chatId, out var lastMessage))
                {
                    if (lastMessage.Type == 1)
                    {
                        lastMessageText = "Voice Message";
                    }
                    else if (lastMessage.Type == 3)
                    {
                        lastMessageText = "Photo";
                    }
                    else if (lastMessage.Type == 4 || lastMessage.Type == 5)
                    {
                        lastMessageText = "File";
                    }
                    else
                    {
                        lastMessageText = lastMessage.Text;
                    }

                    if (lastMessage.SenderId == CurrentUserId)
                    {
                        lastMessageSender = "You";
                    }
                    else if (_chatTypes.TryGetValue(chatId, out var chatType) && chatType != 0)
                    {
                        lastMessageSender = null;
                    }
                    else
                    {
                        lastMessageSender = contact.Name;
                    }

                    lastMessageTime = lastMessage.SentAt;
                }
                else if (_chatLastMessageTime.TryGetValue(chatId, out var time))
                {
                    lastMessageTime = time;
                }

                var isActive = (chatId == CurrentChatId);
                var isOnline = GetCachedOnlineForChat(chatId);
                var chatButton = _uiBuilder.CreateChatButton(
                    contact,
                    chatId,
                    SwitchToChatAsync,
                    lastMessageText,
                    lastMessageSender,
                    hasUnread,
                    isOnline,
                    isActive,
                    lastMessageTime // ✅ ДОДАНО: передаємо час
                );
                _chatsPanel.Children.Add(chatButton);
            }
        }

        private void SortChatsAndMoveToTop(int chatId)
        {
            if (!_chatToUserMap.ContainsKey(chatId))
            {
                return;
            }

            RequestSortAllChatsDebounced();
        }

        private Task LoadAndAddPrivateChatOptimizedAsync(
            ChatDto chat,
            Dictionary<int, List<ChatMemberDto>>? chatMembersMap,
            Dictionary<int, UserDto>? usersMap)
        {
            try
            {
                if (_chatToUserMap.ContainsKey(chat.Id))
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

                var photoPath = string.IsNullOrEmpty(user.PfpUrl) ? DEFAULT_AVATAR_PATH : user.PfpUrl;
                var contact = new Models.Contact(
                    user.Id,
                    user.Username,
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty,
                    photoPath
                );

                lock (_contacts)
                {
                    _contacts[contact.UserId] = contact;
                }

                lock (_chatToUserMap)
                {
                    _chatToUserMap[chat.Id] = contact.UserId;
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
                if (_chatToUserMap.ContainsKey(chat.Id))
                {
                    return Task.CompletedTask;
                }

                var photoPath = string.IsNullOrEmpty(chat.IconUrl) ? DEFAULT_AVATAR_PATH : chat.IconUrl;

                string groupName = string.IsNullOrWhiteSpace(chat.Name)
                    ? $"Group {chat.Id}"
                    : chat.Name;

                var contact = new Models.Contact(
                    chat.Id,
                    groupName,
                    "",
                    "",
                    photoPath
                );

                lock (_groupContacts)
                {
                    _groupContacts[chat.Id] = contact;
                }

                lock (_chatToUserMap)
                {
                    _chatToUserMap[chat.Id] = -chat.Id;
                }

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
                        var photoPath = string.IsNullOrEmpty(user.PfpUrl) ? DEFAULT_AVATAR_PATH : user.PfpUrl;
                        var contact = new Models.Contact(
                            user.Id,
                            user.Username,
                            user.Email ?? "",
                            user.PhoneNumber ?? "",
                            photoPath
                        );

                        _contacts[user.Id] = contact;
                        _chatToUserMap[chatId] = userId;

                        _chatTypes[chatId] = 0;

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddChatToList(contact, chatId);
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
                    var photoPath = string.IsNullOrEmpty(user.PfpUrl) ? DEFAULT_AVATAR_PATH : user.PfpUrl;
                    contact = new Models.Contact(
                        user.Id,
                        user.Username,
                        user.Email ?? string.Empty,
                        user.PhoneNumber ?? string.Empty,
                        photoPath
                    );
                    _contacts[user.Id] = contact;
                }

                var chat = await _apiService.CreateOrGetPrivateChatAsync(user.Id);

                if (chat == null)
                {
                    Edemly.Client.Pages.MessageBox.Show("Failed to create chat", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int chatId = chat.Id;

                if (!_chatToUserMap.ContainsKey(chatId))
                {
                    _chatToUserMap[chatId] = user.Id;
                    _chatLastMessageTime[chatId] = DateTime.UtcNow;
                    _chatTypes[chatId] = chat.Type;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddChatToList(contact, chatId);
                        SortChatsAndMoveToTop(chatId);
                    });
                }

                await SwitchToChatAsync(contact, chatId);
            }
            catch (Exception ex)
            {
                Edemly.Client.Pages.MessageBox.Show($"Failed to create chat: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddChatToList(Models.Contact contact, int chatId)
        {
            var existingButton = _chatsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is int id && id == chatId);

            if (existingButton != null)
            {
                return;
            }

            string? lastMessageText = null;
            string? lastMessageSender = null;
            DateTime? lastMessageTime = null;
            bool hasUnread = _chatsWithUnreadMessages.Contains(chatId);

            if (_chatLastMessage.TryGetValue(chatId, out var lastMessage))
            {
                if (lastMessage.Type == 1)
                {
                    lastMessageText = "Voice Message";
                }
                else if (lastMessage.Type == 3)
                {
                    lastMessageText = "Photo";
                }
                else if (lastMessage.Type == 4 || lastMessage.Type == 5)
                {
                    lastMessageText = "File";
                }
                else
                {
                    lastMessageText = lastMessage.Text;
                }

                lastMessageSender = lastMessage.SenderId == CurrentUserId ? "You" : contact.Name;
                lastMessageTime = lastMessage.SentAt;
            }
            else if (_chatLastMessageTime.TryGetValue(chatId, out var time))
            {
                lastMessageTime = time;
            }

            var isActive = (chatId == CurrentChatId);
            var isOnline = GetCachedOnlineForChat(chatId);
            var chatButton = _uiBuilder.CreateChatButton(
                contact,
                chatId,
                SwitchToChatAsync,
                lastMessageText,
                lastMessageSender,
                hasUnread,
                isOnline,
                isActive,
                lastMessageTime // ✅ ДОДАНО: передаємо час
            );

            _chatsPanel.Children.Add(chatButton);
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
                    Application.Current.Dispatcher.Invoke(() => { _messagesPanel.Dispatcher.Invoke(() => { }); });

                    await Application.Current.Dispatcher.InvokeAsync(() => RefreshCurrentChatMessagesAsync());

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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MakeTextLinksClickable(FindBorderByMessageId(message.Id), message.Text);
                });
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
                var user = await _apiService.GetUserByIdAsync(userId);
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

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AddChatToList(groupContact, chatId);
                    SortChatsAndMoveToTop(chatId);
                });

                await SwitchToChatAsync(groupContact, chatId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] AddGroupChatAndSwitchAsync error: {ex.Message}");
            }
        }

        public void UpdateChatButtonName(int chatId, string newName)
        {
            if (!_chatToUserMap.TryGetValue(chatId, out var userId))
            {
                return;
            }

            Contact? contact;
            if (userId < 0)
            {
                if (!_groupContacts.TryGetValue(chatId, out contact))
                {
                    return;
                }
            }
            else
            {
                if (!_contacts.TryGetValue(userId, out contact))
                {
                    return;
                }
            }

            contact.Name = newName;

            UpdateChatButton(chatId);
        }

        public void Dispose()
        {
            _cacheCleanupTimer?.Dispose();
            _cacheCleanupTimer = null;

            _hubService.MessageReceived -= OnMessageReceived;
            _hubService.MessageUpdated -= OnMessageUpdated;
            _hubService.MessageDeleted -= OnMessageDeleted;
            _hubService.ConnectionStateChanged -= OnConnectionStateChanged;
            _hubService.GroupCreated -= OnGroupCreated;
            _hubService.GroupUpdated -= OnGroupUpdated;
            _hubService.ProfileUpdated -= OnProfileUpdated;
            _hubService.UserStatusChanged -= OnHubUserStatusChanged;

            _presenceTimer?.Dispose();
            _presenceTimer = null;
        }

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
                        Application.Current.Dispatcher.Invoke(() => _messagesPanel.Children.Remove(border));
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
                    var resInfo = Application.GetResourceStream(packUri);
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
                    var asm = Assembly.GetExecutingAssembly();
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