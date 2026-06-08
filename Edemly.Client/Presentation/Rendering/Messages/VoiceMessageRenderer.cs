#nullable disable

using Edemly.Contracts.Messages;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class VoiceMessageRenderer
    {
        private readonly IMessageContextMenuFactory _contextMenuFactory;

        public VoiceMessageRenderer(IMessageContextMenuFactory contextMenuFactory)
        {
            _contextMenuFactory = contextMenuFactory;
        }

        public void Render(MessageDto message, MessageRenderContext context, bool isHistorical)
        {
            if (context.IsMine(message))
            {
                VoiceMessageHelper.AddMyVoiceMessage(
                    message,
                    context,
                    isHistorical,
                    _contextMenuFactory);
                return;
            }

            VoiceMessageHelper.AddFriendVoiceMessage(
                message,
                context,
                isHistorical,
                context.SenderName,
                _contextMenuFactory);
        }
    }
}
