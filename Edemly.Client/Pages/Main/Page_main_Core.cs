#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Infrastructure.Audio;
using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Controllers.Chats;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
namespace Edemly.Client.Pages.Main
{
    public partial class Page_main : ThemedPage
    {
        private bool isMenuOpen = false;
        private bool isContactInfoOpen = false;
        private bool isGroupInfoOpen = false;
        private ChatWorkspaceController? _chatController;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isFirstLoad = true;

        private HashSet<int> _selectedParticipants = new HashSet<int>();

        private VoiceRecorder? _voiceRecorder;
        private bool _isRecording = false;
        private string _messageTextBeforeRecording = string.Empty;

        public Page_main()
        {
            InitializeComponent();

            if (App.CurrentUserId == null)
            {
                MessageBox.ShowWarning(DefaultLanguage.ErrorOccurred, DefaultLanguage.ErrorTitle);
                NavigationService.Navigate(new Page_login());
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            if (App.GlobalChatController != null)
            {
                _chatController = App.GlobalChatController;
                _chatController.UpdateUiBindings(CreateChatWorkspaceBindings());

                _isFirstLoad = false;
            }
            else
            {
                _chatController = new ChatWorkspaceController(CreateChatWorkspaceBindings(), App.CurrentUserId.Value);

                App.GlobalChatController = _chatController;
                _isFirstLoad = true;
            }

            UpdateChatHeader(null);

            ApplyLocalization();

            MessageTextBox.KeyDown += MessageTextBox_PreviewKeyDown;

            MessageTextBox.GotFocus += (s, e) =>
            {
                if (IsPlaceholderText(MessageTextBox.Text))
                {
                    MessageTextBox.Text = "";
                    ApplyTextInputActiveStyle(MessageTextBox);
                }
            };

            MessageTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
                {
                    SetMessagePlaceholder();
                }
            };

            SearchTextBox.GotFocus += SearchTextBox_GotFocus;
            SearchTextBox.LostFocus += SearchTextBox_LostFocus;
            SearchTextBox.KeyDown += SearchTextBox_KeyDown;
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;

            if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || IsPlaceholderText(MessageTextBox.Text))
            {
                SetMessagePlaceholder();
            }

            if (SendButton != null && IsPlaceholderText(MessageTextBox.Text))
            {
                SendButton.Content = "🎤";
                SendButton.Tag = "voice";
            }

            if (ParticipantSearchTextBox != null)
            {
                ApplyTextInputPlaceholderStyle(ParticipantSearchTextBox, DefaultLanguage.SearchUsers);
            }

            this.Loaded += Page_main_Loaded;
            this.Unloaded += Page_main_Unloaded;
            LoadStickers();
            InitializeAsync();
        }

        private ChatWorkspaceBindings CreateChatWorkspaceBindings()
        {
            return new ChatWorkspaceBindings(
                MessagesPanel,
                MessagesScrollViewer,
                ChatsPanel,
                ChatHeaderText,
                UpdateChatHeader);
        }

        private void SetThemeResource(FrameworkElement element, DependencyProperty property, string resourceKey)
        {
            element?.SetResourceReference(property, resourceKey);
        }

        private void ApplyTextInputPlaceholderStyle(TextBox textBox, string placeholder)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.Text = placeholder;
            SetThemeResource(textBox, Control.ForegroundProperty, "ThemeDisabledTextBrush");
            textBox.FontStyle = FontStyles.Italic;
        }

        private void ApplyTextInputActiveStyle(TextBox textBox, string? text = null)
        {
            if (textBox == null)
            {
                return;
            }

            if (text != null)
            {
                textBox.Text = text;
            }

            SetThemeResource(textBox, Control.ForegroundProperty, "ThemeTextPrimaryBrush");
            textBox.FontStyle = FontStyles.Normal;
        }

        private void ApplyLocalization()
        {
            try
            {
                var myPlannerText = this.FindName("MyPlannerButtonText") as TextBlock;
                if (myPlannerText != null) myPlannerText.Text = DefaultLanguage.MyPlanner;

                var chatsHeaderText = this.FindName("ChatsHeaderText") as TextBlock;
                if (chatsHeaderText != null) chatsHeaderText.Text = DefaultLanguage.Chats;

                if (CreateGroupButton != null) CreateGroupButton.ToolTip = DefaultLanguage.CreateGroupTooltip;

                var menuTitleText = this.FindName("MenuTitleText") as TextBlock;
                if (menuTitleText != null) menuTitleText.Text = DefaultLanguage.Menu;
                if (SettingsButton != null) SettingsButton.Content = DefaultLanguage.MenuSettings;
                if (PremiumButton != null) PremiumButton.Content = DefaultLanguage.MenuPremium;
                if (HelpButton != null) HelpButton.Content = DefaultLanguage.MenuAbout;
                if (LogoutButton != null) LogoutButton.Content = DefaultLanguage.MenuLogout;

                var contactInfoTitle = this.FindName("ContactInfoTitle") as TextBlock;
                if (contactInfoTitle != null) contactInfoTitle.Text = DefaultLanguage.ContactInfo;
                var nameLabelText = this.FindName("NameLabelText") as TextBlock;
                if (nameLabelText != null) nameLabelText.Text = DefaultLanguage.NameLabel;
                var emailLabelText = this.FindName("EmailLabelText") as TextBlock;
                if (emailLabelText != null) emailLabelText.Text = DefaultLanguage.EmailLabel;
                var phoneLabelText = this.FindName("PhoneLabelText") as TextBlock;
                if (phoneLabelText != null) phoneLabelText.Text = DefaultLanguage.PhoneLabel;
                var personalNotesTitle = this.FindName("PersonalNotesTitle") as TextBlock;
                if (personalNotesTitle != null) personalNotesTitle.Text = DefaultLanguage.PersonalNotes;
                var personalNotesPrivate = this.FindName("PersonalNotesPrivate") as TextBlock;
                if (personalNotesPrivate != null) personalNotesPrivate.Text = DefaultLanguage.NotesPrivate;
                if (NoNotesText != null) NoNotesText.Text = DefaultLanguage.NoNotes;
                var closeContactInfoButton = this.FindName("CloseContactInfoButton") as Button;
                if (closeContactInfoButton != null) closeContactInfoButton.Content = DefaultLanguage.Close;

                var groupInfoTitle = this.FindName("GroupInfoTitle") as TextBlock;
                if (groupInfoTitle != null) groupInfoTitle.Text = DefaultLanguage.GroupInfo;
                var groupNameLabelText = this.FindName("GroupNameLabelText") as TextBlock;
                if (groupNameLabelText != null) groupNameLabelText.Text = DefaultLanguage.GroupNameLabel;
                var descriptionLabelText = this.FindName("DescriptionLabelText") as TextBlock;
                if (descriptionLabelText != null) descriptionLabelText.Text = DefaultLanguage.DescriptionLabel;
                if (NoDescriptionText != null) NoDescriptionText.Text = DefaultLanguage.NoDescription;
                var membersLabelText = this.FindName("MembersLabelText") as TextBlock;
                if (membersLabelText != null) membersLabelText.Text = DefaultLanguage.MembersLabel;
                if (CloseGroupInfoButton != null) CloseGroupInfoButton.Content = DefaultLanguage.Close;
                if (GroupSettingsIconButton != null) GroupSettingsIconButton.ToolTip = DefaultLanguage.GroupSettings;

                var stickersTitle = this.FindName("StickersTitle") as TextBlock;
                if (stickersTitle != null) stickersTitle.Text = DefaultLanguage.Stickers;

                var searchResultsTitle = this.FindName("SearchResultsTitle") as TextBlock;
                if (searchResultsTitle != null) searchResultsTitle.Text = DefaultLanguage.SearchResults;

                var createGroupTitle = this.FindName("CreateGroupTitle") as TextBlock;
                if (createGroupTitle != null) createGroupTitle.Text = DefaultLanguage.CreateNewGroup;
                var groupNameInputLabel = this.FindName("GroupNameInputLabel") as TextBlock;
                if (groupNameInputLabel != null) groupNameInputLabel.Text = DefaultLanguage.GroupNameLabel;
                var addParticipantsLabel = this.FindName("AddParticipantsLabel") as TextBlock;
                if (addParticipantsLabel != null) addParticipantsLabel.Text = DefaultLanguage.AddParticipants;
                var cancelCreateGroupButton = this.FindName("CancelCreateGroupButton") as Button;
                if (cancelCreateGroupButton != null) cancelCreateGroupButton.Content = DefaultLanguage.Cancel;
                if (ConfirmCreateGroupButton != null) ConfirmCreateGroupButton.Content = DefaultLanguage.CreateGroup;

                if (CallButton != null) CallButton.ToolTip = DefaultLanguage.CallTooltip;
                if (AttachFileButton != null) AttachFileButton.ToolTip = DefaultLanguage.AttachFile;
                if (StickerButton != null) StickerButton.ToolTip = DefaultLanguage.SendSticker;

                if (SearchTextBox != null)
                {
                    if (string.IsNullOrWhiteSpace(SearchTextBox.Text) || SearchTextBox.Text == "Search...")
                    {
                        ApplyTextInputPlaceholderStyle(SearchTextBox, DefaultLanguage.SearchPlaceholder);
                    }
                }

                if (MessageTextBox != null)
                {
                    if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || IsPlaceholderText(MessageTextBox.Text))
                    {
                        SetMessagePlaceholder();
                    }
                }

                UpdateParticipantCountText();

                System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Localization applied successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Error applying localization: {ex.Message}");
            }
        }

        private void UpdateParticipantCountText()
        {
            if (ParticipantCountText != null)
            {
                ParticipantCountText.Text = string.Format(DefaultLanguage.ParticipantsSelected, _selectedParticipants.Count);
            }
        }

        private void RefreshPlaceholders()
        {
            try
            {
                if (MessageTextBox != null)
                {
                    if (_isRecording)
                    {
                        ApplyTextInputPlaceholderStyle(MessageTextBox, DefaultLanguage.Loading);
                    }
                    else
                    {
                        var currentText = MessageTextBox.Text?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(currentText) || IsPlaceholderText(currentText))
                        {
                            SetMessagePlaceholder();
                        }
                    }
                }

                if (SearchTextBox != null)
                {
                    var currentText = SearchTextBox.Text?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(currentText) ||
                        currentText == "Search..." ||
                        currentText == "Пошук...")
                    {
                        ApplyTextInputPlaceholderStyle(SearchTextBox, DefaultLanguage.SearchPlaceholder);
                    }
                }

                if (ParticipantSearchTextBox != null)
                {
                    var currentText = ParticipantSearchTextBox.Text?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(currentText) ||
                        currentText == "Search users..." ||
                        currentText == "Пошук користувачів...")
                    {
                        ApplyTextInputPlaceholderStyle(ParticipantSearchTextBox, DefaultLanguage.SearchUsers);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] RefreshPlaceholders error: {ex.Message}");
            }
        }

        private void Page_main_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                ApplyLocalization();

                RefreshPlaceholders();

                CommandBinding? pasteBinding = null;

                if (MessageTextBox != null)
                {
                    MessageTextBox.AllowDrop = true;

                    MessageTextBox.PreviewDragOver -= MessageTextBox_PreviewDragOver;
                    MessageTextBox.PreviewDrop -= MessageTextBox_PreviewDrop;
                    MessageTextBox.PreviewDragOver += MessageTextBox_PreviewDragOver;
                    MessageTextBox.PreviewDrop += MessageTextBox_PreviewDrop;

                    System.Windows.DataObject.RemovePastingHandler(MessageTextBox, new DataObjectPastingEventHandler(OnPaste));
                    System.Windows.DataObject.AddPastingHandler(MessageTextBox, new DataObjectPastingEventHandler(OnPaste));

                    var existing = MessageTextBox.CommandBindings.OfType<CommandBinding>().FirstOrDefault(cb => cb.Command == ApplicationCommands.Paste);
                    if (existing != null) MessageTextBox.CommandBindings.Remove(existing);
                    pasteBinding = new CommandBinding(ApplicationCommands.Paste, OnPasteCommandExecuted, OnCanPasteCommand);
                    MessageTextBox.CommandBindings.Add(pasteBinding);

                    MessageTextBox.PreviewKeyDown -= MessageTextBox_PreviewKeyDownForPaste;
                    MessageTextBox.PreviewKeyDown += MessageTextBox_PreviewKeyDownForPaste;
                }

                try
                {
                    this.AllowDrop = true;
                    this.PreviewDragOver -= MessageTextBox_PreviewDragOver;
                    this.PreviewDrop -= MessageTextBox_PreviewDrop;
                    this.PreviewDragOver += MessageTextBox_PreviewDragOver;
                    this.PreviewDrop += MessageTextBox_PreviewDrop;

                    var mainGrid = this.FindName("MainGrid") as UIElement;
                    if (mainGrid != null)
                    {
                        mainGrid.AllowDrop = true;
                        mainGrid.PreviewDragOver -= MessageTextBox_PreviewDragOver;
                        mainGrid.PreviewDrop -= MessageTextBox_PreviewDrop;
                        mainGrid.PreviewDragOver += MessageTextBox_PreviewDragOver;
                        mainGrid.PreviewDrop += MessageTextBox_PreviewDrop;
                    }

                    var wnd = Window.GetWindow(this);
                    if (wnd != null)
                    {
                        try
                        {
                            var existingWnd = wnd.CommandBindings.OfType<CommandBinding>().FirstOrDefault(cb => cb.Command == ApplicationCommands.Paste);
                            if (existingWnd != null) wnd.CommandBindings.Remove(existingWnd);
                            if (pasteBinding != null) wnd.CommandBindings.Add(pasteBinding);
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Attach paste binding failed: {ex}"); }

                        try
                        {
                            this.AddHandler(UIElement.DropEvent, new DragEventHandler(MessageTextBox_PreviewDrop), true);
                            this.AddHandler(UIElement.DragOverEvent, new DragEventHandler(MessageTextBox_PreviewDragOver), true);

                            wnd.AddHandler(UIElement.DropEvent, new DragEventHandler(MessageTextBox_PreviewDrop), true);
                            wnd.AddHandler(UIElement.DragOverEvent, new DragEventHandler(MessageTextBox_PreviewDragOver), true);
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] AddHandler for drag/drop failed: {ex}"); }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Page_main_Loaded inner failed: {ex}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD] Page_main_Loaded error: {ex.Message}");
            }
        }

        private async void InitializeAsync()
        {
            try
            {
                if (!App.HubService.IsConnected)
                {
                    await ConnectToHubAsync();
                }

                App.HubService.UserStatusChanged += OnUserStatusChanged;
                App.HubService.ProfileUpdated += OnProfileUpdated;

                if (_isFirstLoad)
                {
                    await LoadChatsAsync();
                }
                else
                {
                    if (_chatController == null)
                        return;

                    await _chatController.RestoreUIAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorOccurred + ": {0}", ex.Message), DefaultLanguage.ErrorTitle);
            }
        }

        private void Page_main_Unloaded(object sender, RoutedEventArgs e)
        {
            App.HubService.UserStatusChanged -= OnUserStatusChanged;
            App.HubService.ProfileUpdated -= OnProfileUpdated;

            DisposeCancellationTokenSourceSafely();
        }

        protected override void ApplyTheme()
        {
            try
            {
                MainGrid?.SetResourceReference(Panel.BackgroundProperty, "PageBackgroundBrush");
                RefreshPlaceholders();

                if (_isRecording)
                {
                    if (SendButton != null)
                    {
                        SendButton.Content = "⏹";
                        SendButton.Tag = "recording";
                        SetThemeResource(SendButton, Control.BackgroundProperty, "ThemeDangerBrush");
                    }
                }
                else
                {
                    ResetSendButtonForCurrentMessageInput();
                }

                if (_chatController?.CurrentChatContact != null && !(_chatController.IsCurrentChatGroup()))
                {
                    if (_chatController.TryGetCachedUserStatus(_chatController.CurrentChatContact.UserId, out var cachedOnline, out var cachedLastSeen))
                    {
                        UpdateOnlineStatus(cachedOnline, cachedLastSeen);
                    }
                    else
                    {
                        UpdateOnlineStatus(false, null);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Theme applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] ApplyTheme error: {ex.Message}");
            }
        }

        private async Task ConnectToHubAsync()
        {
            if (string.IsNullOrEmpty(App.AuthToken))
            {
                MessageBox.ShowError(DefaultLanguage.ErrorOccurred, DefaultLanguage.ErrorTitle);
                return;
            }

            try
            {
                bool connected = await App.HubService.ConnectAsync(App.AuthToken);
                if (!connected)
                {
                    MessageBox.ShowWarning(DefaultLanguage.ConnectionLost, DefaultLanguage.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private async Task LoadChatsAsync()
        {
            try
            {
                if (_chatController == null)
                    return;
                await _chatController.LoadExistingChatsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private void DisposeCancellationTokenSourceSafely()
        {
            var cts = Interlocked.Exchange(ref _cancellationTokenSource, null);
            if (cts == null) return;

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
            }

            try
            {
                cts.Dispose();
            }
            catch
            {
            }
        }

        private void InnerScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                DependencyObject? original = e.OriginalSource as DependencyObject;
                ScrollViewer? target = FindAncestor<ScrollViewer>(original) ?? (sender as ScrollViewer);

                if (target == null)
                    return;

                if (target.ScrollableHeight <= 0)
                    return;

                double newOffset = target.VerticalOffset - e.Delta;
                newOffset = Math.Max(0, Math.Min(target.ScrollableHeight, newOffset));
                target.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] InnerScrollViewer_PreviewMouseWheel failed: {ex}"); }
        }

        private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
        {
            try
            {
                while (child != null)
                {
                    if (child is T found) return found;
                    child = VisualTreeHelper.GetParent(child);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] FindAncestor failed: {ex}"); }
            return null;
        }

        private void MessagesScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                if (MessagesScrollViewer == null) return;

                if (MessagesScrollViewer.ScrollableHeight <= 0) return;

                double newOffset = MessagesScrollViewer.VerticalOffset - e.Delta;
                newOffset = Math.Max(0, Math.Min(MessagesScrollViewer.ScrollableHeight, newOffset));
                MessagesScrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] MessagesScrollViewer_PreviewMouseWheel failed: {ex}"); }
        }

        private void Page_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                var element = Mouse.DirectlyOver as DependencyObject;
                var sv = FindAncestor<ScrollViewer>(element);

                if (sv == null) sv = MessagesScrollViewer;

                if (sv != null && sv == MessagesScrollViewer)
                {
                    if (MessagesScrollViewer.ScrollableHeight <= 0) return;
                    double newOffset = MessagesScrollViewer.VerticalOffset - e.Delta;
                    newOffset = Math.Max(0, Math.Min(MessagesScrollViewer.ScrollableHeight, newOffset));
                    MessagesScrollViewer.ScrollToVerticalOffset(newOffset);
                    e.Handled = true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Page_PreviewMouseWheel failed: {ex}"); }
        }
    }
}
