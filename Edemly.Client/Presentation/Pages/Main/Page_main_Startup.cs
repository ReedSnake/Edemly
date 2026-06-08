#nullable enable

using Edemly.Client.Presentation.Controllers.Chats;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private void InitializeChatWorkspaceIfAvailable()
        {
            if (App.CurrentUserId is int currentUserId)
            {
                InitializeChatWorkspace(currentUserId);
            }
        }

        private void InitializeChatWorkspace(int currentUserId)
        {
            if (_isRuntimeInitialized)
            {
                return;
            }

            if (App.GlobalChatController != null)
            {
                _chatController = App.GlobalChatController;
                _chatController.UpdateUiBindings(CreateChatWorkspaceBindings());
                _isFirstLoad = false;
            }
            else
            {
                _chatController = new ChatWorkspaceController(CreateChatWorkspaceBindings(), currentUserId);
                App.GlobalChatController = _chatController;
                _isFirstLoad = true;
            }

            _isRuntimeInitialized = true;
            UpdateChatHeader(null);
        }

        private async void Page_main_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                ConfigureAttachmentInputBindings();
                ApplyLocalization();
                RefreshPlaceholders();

                await EnsurePageReadyAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOAD] Page_main_Loaded error: {ex.Message}");
            }
        }

        private void ConfigureAttachmentInputBindings()
        {
            CommandBinding? pasteBinding = null;

            if (MessageTextBox != null)
            {
                MessageTextBox.AllowDrop = true;

                MessageTextBox.PreviewDragOver -= MessageTextBox_PreviewDragOver;
                MessageTextBox.PreviewDrop -= MessageTextBox_PreviewDrop;
                MessageTextBox.PreviewDragOver += MessageTextBox_PreviewDragOver;
                MessageTextBox.PreviewDrop += MessageTextBox_PreviewDrop;

                DataObject.RemovePastingHandler(MessageTextBox, new DataObjectPastingEventHandler(OnPaste));
                DataObject.AddPastingHandler(MessageTextBox, new DataObjectPastingEventHandler(OnPaste));

                var existingBinding = MessageTextBox.CommandBindings
                    .OfType<CommandBinding>()
                    .FirstOrDefault(binding => binding.Command == ApplicationCommands.Paste);

                if (existingBinding != null)
                {
                    MessageTextBox.CommandBindings.Remove(existingBinding);
                }

                pasteBinding = new CommandBinding(ApplicationCommands.Paste, OnPasteCommandExecuted, OnCanPasteCommand);
                MessageTextBox.CommandBindings.Add(pasteBinding);

                MessageTextBox.PreviewKeyDown -= MessageTextBox_PreviewKeyDownForPaste;
                MessageTextBox.PreviewKeyDown += MessageTextBox_PreviewKeyDownForPaste;
            }

            try
            {
                AllowDrop = true;
                PreviewDragOver -= MessageTextBox_PreviewDragOver;
                PreviewDrop -= MessageTextBox_PreviewDrop;
                PreviewDragOver += MessageTextBox_PreviewDragOver;
                PreviewDrop += MessageTextBox_PreviewDrop;

                if (FindName("MainGrid") is UIElement mainGrid)
                {
                    mainGrid.AllowDrop = true;
                    mainGrid.PreviewDragOver -= MessageTextBox_PreviewDragOver;
                    mainGrid.PreviewDrop -= MessageTextBox_PreviewDrop;
                    mainGrid.PreviewDragOver += MessageTextBox_PreviewDragOver;
                    mainGrid.PreviewDrop += MessageTextBox_PreviewDrop;
                }

                var window = Window.GetWindow(this);
                if (window == null)
                {
                    return;
                }

                var existingWindowBinding = window.CommandBindings
                    .OfType<CommandBinding>()
                    .FirstOrDefault(binding => binding.Command == ApplicationCommands.Paste);

                if (existingWindowBinding != null)
                {
                    window.CommandBindings.Remove(existingWindowBinding);
                }

                if (pasteBinding != null)
                {
                    window.CommandBindings.Add(pasteBinding);
                }

                AddHandler(UIElement.DropEvent, new DragEventHandler(MessageTextBox_PreviewDrop), true);
                AddHandler(UIElement.DragOverEvent, new DragEventHandler(MessageTextBox_PreviewDragOver), true);

                window.AddHandler(UIElement.DropEvent, new DragEventHandler(MessageTextBox_PreviewDrop), true);
                window.AddHandler(UIElement.DragOverEvent, new DragEventHandler(MessageTextBox_PreviewDragOver), true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] ConfigureAttachmentInputBindings failed: {ex}");
            }
        }

        private async Task<bool> EnsurePageReadyAsync()
        {
            if (App.CurrentUserId is not int currentUserId || string.IsNullOrWhiteSpace(App.AuthToken))
            {
                RedirectToLogin();
                return false;
            }

            if (!_isRuntimeInitialized)
            {
                InitializeChatWorkspace(currentUserId);
            }

            if (_hasStartedAsyncInitialization)
            {
                return true;
            }

            _hasStartedAsyncInitialization = true;
            await InitializeAsync(showConnectionWarning: false);
            return true;
        }

        private void RedirectToLogin()
        {
            void NavigateToLogin()
            {
                if (NavigationService != null)
                {
                    NavigationService.Navigate(new LoginPage());
                }
            }

            if (NavigationService != null)
            {
                NavigateToLogin();
                return;
            }

            _ = Dispatcher.BeginInvoke((Action)NavigateToLogin);
        }

        private async Task InitializeAsync(bool showConnectionWarning)
        {
            try
            {
                if (!App.HubService.IsConnected)
                {
                    await ConnectToHubAsync(showConnectionWarning);
                }

                App.HubService.UserStatusChanged -= OnUserStatusChanged;
                App.HubService.ProfileUpdated -= OnProfileUpdated;
                App.HubService.UserStatusChanged += OnUserStatusChanged;
                App.HubService.ProfileUpdated += OnProfileUpdated;

                if (_isFirstLoad)
                {
                    await LoadChatsAsync();
                    return;
                }

                if (_chatController != null)
                {
                    await _chatController.RestoreUIAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorOccurred + ": {0}", ex.Message), DefaultLanguage.ErrorTitle);
            }
        }

        private async Task ConnectToHubAsync(bool showConnectionWarning)
        {
            if (string.IsNullOrWhiteSpace(App.AuthToken))
            {
                RedirectToLogin();
                return;
            }

            try
            {
                var connected = await App.HubService.ConnectAsync(App.AuthToken);
                if (!connected)
                {
                    if (showConnectionWarning)
                    {
                        MessageBox.ShowWarning(DefaultLanguage.ConnectionLost, DefaultLanguage.ErrorTitle);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Hub connection was not established during page startup.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (showConnectionWarning)
                {
                    MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Startup hub connect failed: {ex.Message}");
                }
            }
        }

        private async Task LoadChatsAsync()
        {
            try
            {
                if (_chatController == null)
                {
                    return;
                }

                await _chatController.LoadExistingChatsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private void Page_main_Unloaded(object sender, RoutedEventArgs e)
        {
            App.HubService.UserStatusChanged -= OnUserStatusChanged;
            App.HubService.ProfileUpdated -= OnProfileUpdated;

            DisposeCancellationTokenSourceSafely();
        }

        private void DisposeCancellationTokenSourceSafely()
        {
            var cancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, null);
            if (cancellationTokenSource == null)
            {
                return;
            }

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
            }

            try
            {
                cancellationTokenSource.Dispose();
            }
            catch
            {
            }
        }
    }
}
