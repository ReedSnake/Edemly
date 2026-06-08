#nullable enable

using Edemly.Contracts.Messages;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageContextMenuOptions
    {
        public bool AllowCopyText { get; init; }
        public bool AllowEdit { get; init; }
        public bool AllowDelete { get; init; } = true;

        public static MessageContextMenuOptions ForMessage(MessageDto message)
        {
            return new MessageContextMenuOptions
            {
                AllowCopyText = message.Type == 0 && !string.IsNullOrWhiteSpace(message.Text),
                AllowEdit = message.Type == 0,
                AllowDelete = true
            };
        }

        public static MessageContextMenuOptions ForVoice()
        {
            return new MessageContextMenuOptions
            {
                AllowCopyText = false,
                AllowEdit = false,
                AllowDelete = true
            };
        }
    }
}
