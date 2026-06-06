#nullable enable

namespace Edemly.Client.Infrastructure.Navigation
{
    public interface IExternalNavigationLauncher
    {
        void OpenFile(string path);

        void OpenUri(Uri uri);
    }
}
