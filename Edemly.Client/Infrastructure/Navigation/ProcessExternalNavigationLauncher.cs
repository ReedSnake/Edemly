#nullable enable

using System.Diagnostics;

namespace Edemly.Client.Infrastructure.Navigation
{
    public sealed class ProcessExternalNavigationLauncher : IExternalNavigationLauncher
    {
        public void OpenFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{path}\"")
                {
                    CreateNoWindow = true
                });
            }
        }

        public void OpenUri(Uri uri)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
    }
}
