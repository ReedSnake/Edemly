#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Rendering.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageContextMenuFactory : IMessageContextMenuFactory
    {
        private readonly MessageActions _actions;

        public MessageContextMenuFactory(MessageActions actions)
        {
            _actions = actions;
        }

        public void Attach(
            Border messageBorder,
            MessageDto message,
            MessageRenderContext context,
            MessageContextMenuOptions options = null)
        {
            options ??= MessageContextMenuOptions.ForMessage(message);

            var contextMenu = StyledContextMenu.Create();

            if (options.AllowCopyText && !string.IsNullOrWhiteSpace(message.Text))
            {
                StyledContextMenu.AddItem(
                    contextMenu,
                    MessageMenuGlyphs.Copy,
                    DefaultLanguage.CopyMessage,
                    () => Clipboard.SetText(message.Text));
            }

            if (message.SenderId == context.CurrentUserId)
            {
                if (contextMenu.Items.Count > 0)
                {
                    StyledContextMenu.AddSeparator(contextMenu);
                }

                if (options.AllowEdit)
                {
                    StyledContextMenu.AddItem(
                        contextMenu,
                        MessageMenuGlyphs.Edit,
                        DefaultLanguage.EditMessage,
                        () => _ = ExecuteMenuActionAsync(() => _actions.EditMessageAsync(message, context.CurrentUserId)));
                }

                if (options.AllowDelete)
                {
                    StyledContextMenu.AddItem(
                        contextMenu,
                        MessageMenuGlyphs.Delete,
                        DefaultLanguage.DeleteMessage,
                        () => _ = ExecuteMenuActionAsync(() => _actions.DeleteMessageAsync(message)),
                        isDanger: true);
                }
            }

            messageBorder.ContextMenu = contextMenu.Items.Count > 0
                ? contextMenu
                : null;
        }

        private static async Task ExecuteMenuActionAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MESSAGE MENU] Action failed: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }
    }
}
