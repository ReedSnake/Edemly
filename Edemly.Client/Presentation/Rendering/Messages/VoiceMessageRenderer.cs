#nullable disable

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class VoiceMessageRenderer
    {
        public void Render(MessageDto message, MessageRenderContext context, bool isHistorical)
        {
            if (context.IsMine(message))
            {
                VoiceMessageHelper.AddMyVoiceMessage(
                    message,
                    context.MessagesPanel,
                    context.CurrentUserId,
                    isHistorical);
                return;
            }

            VoiceMessageHelper.AddFriendVoiceMessage(
                message,
                context.MessagesPanel,
                context.CurrentUserId,
                isHistorical,
                context.SenderName,
                context.IsGroupChat);
        }
    }
}
