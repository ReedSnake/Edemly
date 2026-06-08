#nullable enable

using Edemly.Client.Application.Attachments;
using Edemly.Client.Infrastructure.Attachments;
using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Controllers.Chats;
using Edemly.Client.Presentation.Dialogs.Attachments;
using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main : ThemedPage
    {
        private bool isMenuOpen;
        private bool isContactInfoOpen;
        private bool isGroupInfoOpen;
        private bool _isRuntimeInitialized;
        private bool _hasStartedAsyncInitialization;
        private bool _isFirstLoad = true;
        private int _contactInfoLoadVersion;
        private ChatWorkspaceController? _chatController;
        private CancellationTokenSource? _cancellationTokenSource;
        private IAttachmentFilePicker _attachmentFilePicker = null!;
        private IClipboardImageTempFileStore _clipboardImageTempFileStore = null!;
        private IAttachmentWorkflowCoordinator _attachmentWorkflowCoordinator = null!;
        private readonly HashSet<int> _selectedParticipants = new();
        private VoiceRecorder? _voiceRecorder;
        private bool _isRecording;
        private string _messageTextBeforeRecording = string.Empty;

        public Page_main()
        {
            InitializeComponent();

            InitializeServices();
            InitializeChatWorkspaceIfAvailable();
            InitializeStaticUi();

            Loaded += Page_main_Loaded;
            Unloaded += Page_main_Unloaded;

            LoadStickers();
        }

        private void InitializeServices()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _attachmentFilePicker = new AttachmentFilePicker();
            _clipboardImageTempFileStore = new ClipboardImageTempFileStore();
            _attachmentWorkflowCoordinator = new AttachmentWorkflowCoordinator(
                new AttachmentDescriptorFactory(),
                new AttachmentPreviewDialogService(),
                new ChatAttachmentSender(App.ApiClients, App.HubService));
        }

        private void InitializeStaticUi()
        {
            UpdateChatHeader(null);
            ApplyLocalization();
            InitializeComposerInputBindings();
            InitializeInputPlaceholders();
        }

        private void InitializeComposerInputBindings()
        {
            MessageTextBox.KeyDown += MessageTextBox_PreviewKeyDown;

            MessageTextBox.GotFocus += (_, _) =>
            {
                if (PageMainInputHelper.IsPlaceholderText(MessageTextBox.Text))
                {
                    ApplyTextInputActiveStyle(MessageTextBox, string.Empty);
                }
            };

            MessageTextBox.LostFocus += (_, _) =>
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
        }

        private void InitializeInputPlaceholders()
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text) || PageMainInputHelper.IsPlaceholderText(MessageTextBox.Text))
            {
                SetMessagePlaceholder();
            }

            if (SendButton != null && PageMainInputHelper.IsPlaceholderText(MessageTextBox.Text))
            {
                SendButton.Content = PageMainGlyphs.Microphone;
                SendButton.Tag = "voice";
            }

            if (ParticipantSearchTextBox != null)
            {
                ApplyTextInputPlaceholderStyle(ParticipantSearchTextBox, DefaultLanguage.SearchUsers);
            }
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
                        SendButton.Content = PageMainGlyphs.Stop;
                        SendButton.Tag = "recording";
                        SetThemeResource(SendButton, Control.BackgroundProperty, "ThemeDangerBrush");
                    }
                }
                else
                {
                    ResetSendButtonForCurrentMessageInput();
                }

                if (_chatController?.CurrentChatContact != null && !_chatController.IsCurrentChatGroup())
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

                _ = RefreshActiveChatThemeAsync();

                System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Theme applied");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] ApplyTheme error: {ex.Message}");
            }
        }

        private void InnerScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                DependencyObject? original = e.OriginalSource as DependencyObject;
                ScrollViewer? target = FindAncestor<ScrollViewer>(original) ?? sender as ScrollViewer;

                if (target == null || target.ScrollableHeight <= 0)
                {
                    return;
                }

                var newOffset = target.VerticalOffset - e.Delta;
                newOffset = Math.Max(0, Math.Min(target.ScrollableHeight, newOffset));
                target.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] InnerScrollViewer_PreviewMouseWheel failed: {ex}");
            }
        }

        private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
        {
            try
            {
                while (child != null)
                {
                    if (child is T found)
                    {
                        return found;
                    }

                    child = VisualTreeHelper.GetParent(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] FindAncestor failed: {ex}");
            }

            return null;
        }

        private void MessagesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (MessagesScrollViewer == null || MessagesScrollViewer.ScrollableHeight <= 0)
                {
                    return;
                }

                var newOffset = MessagesScrollViewer.VerticalOffset - e.Delta;
                newOffset = Math.Max(0, Math.Min(MessagesScrollViewer.ScrollableHeight, newOffset));
                MessagesScrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] MessagesScrollViewer_PreviewMouseWheel failed: {ex}");
            }
        }

        private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                var element = Mouse.DirectlyOver as DependencyObject;
                var scrollViewer = FindAncestor<ScrollViewer>(element) ?? MessagesScrollViewer;

                if (scrollViewer == MessagesScrollViewer && MessagesScrollViewer?.ScrollableHeight > 0)
                {
                    var newOffset = MessagesScrollViewer.VerticalOffset - e.Delta;
                    newOffset = Math.Max(0, Math.Min(MessagesScrollViewer.ScrollableHeight, newOffset));
                    MessagesScrollViewer.ScrollToVerticalOffset(newOffset);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Page_PreviewMouseWheel failed: {ex}");
            }
        }
    }
}
