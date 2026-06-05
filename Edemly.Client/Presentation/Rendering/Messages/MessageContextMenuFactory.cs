#nullable disable

using Edemly.Client.Application.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageContextMenuFactory
    {
        private readonly MessageActions _actions;

        public MessageContextMenuFactory(MessageActions actions)
        {
            _actions = actions;
        }

        public void Attach(Border messageBorder, MessageDto message, MessageRenderContext context)
        {
            var contextMenu = new ContextMenu();

            if (message.Type == 0 && !string.IsNullOrEmpty(message.Text))
            {
                var copyItem = new MenuItem
                {
                    Header = DefaultLanguage.CopyMessage,
                    FontSize = 13
                };
                copyItem.Click += (s, e) => Clipboard.SetText(message.Text);
                contextMenu.Items.Add(copyItem);
            }

            if (message.SenderId == context.CurrentUserId)
            {
                if (contextMenu.Items.Count > 0)
                {
                    contextMenu.Items.Add(new Separator());
                }

                if (message.Type == 0)
                {
                    var editItem = new MenuItem
                    {
                        Header = DefaultLanguage.EditMessage,
                        FontSize = 13
                    };
                    editItem.Click += async (s, e) => await _actions.EditMessageAsync(message, context.CurrentUserId);
                    contextMenu.Items.Add(editItem);
                }

                var deleteItem = new MenuItem
                {
                    Header = DefaultLanguage.DeleteMessage,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69))
                };
                deleteItem.Click += async (s, e) => await _actions.DeleteMessageAsync(message);
                contextMenu.Items.Add(deleteItem);
            }

            if (contextMenu.Items.Count > 0)
            {
                messageBorder.ContextMenu = contextMenu;
            }
        }
    }
}
