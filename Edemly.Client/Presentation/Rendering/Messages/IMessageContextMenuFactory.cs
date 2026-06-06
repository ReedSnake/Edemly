#nullable enable

using System.Windows.Controls;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public interface IMessageContextMenuFactory
    {
        void Attach(
            Border messageBorder,
            MessageDto message,
            MessageRenderContext context,
            MessageContextMenuOptions? options = null);
    }
}
