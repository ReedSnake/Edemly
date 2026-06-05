#nullable enable

using System;
using System.Windows.Controls;
using Edemly.Client.Models;

namespace Edemly.Client.Presentation.Controllers.Chats
{
    public sealed class ChatWorkspaceBindings
    {
        public ChatWorkspaceBindings(
            StackPanel messagesPanel,
            ScrollViewer? messagesScrollViewer,
            StackPanel chatsPanel,
            TextBlock chatHeaderText,
            Action<Contact?>? updateChatHeaderCallback)
        {
            MessagesPanel = messagesPanel;
            MessagesScrollViewer = messagesScrollViewer;
            ChatsPanel = chatsPanel;
            ChatHeaderText = chatHeaderText;
            UpdateChatHeaderCallback = updateChatHeaderCallback;
        }

        public StackPanel MessagesPanel { get; }
        public ScrollViewer? MessagesScrollViewer { get; }
        public StackPanel ChatsPanel { get; }
        public TextBlock ChatHeaderText { get; }
        public Action<Contact?>? UpdateChatHeaderCallback { get; }
    }
}
