#nullable enable

using Edemly.Contracts.Messages;
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
