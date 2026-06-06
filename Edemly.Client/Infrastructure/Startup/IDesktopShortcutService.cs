#nullable enable

namespace Edemly.Client.Infrastructure.Startup
{
    public interface IDesktopShortcutService
    {
        bool TryCreateOrReplaceShortcut(string shortcutFileName, string? preferredExecutablePath, string shortcutArgument);
    }
}
