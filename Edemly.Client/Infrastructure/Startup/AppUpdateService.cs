using System.Diagnostics;
using Velopack;

namespace Edemly.Client.Infrastructure.Startup
{
    public static class AppUpdateService
    {
        public static async Task CheckForUpdatesAsync(string updateFeedUrl)
        {
            if (string.IsNullOrWhiteSpace(updateFeedUrl))
            {
                return;
            }

            try
            {
                var manager = new UpdateManager(updateFeedUrl);
                if (!manager.IsInstalled)
                {
                    Debug.WriteLine("[APP UPDATE] Application is not installed by Velopack. Auto-update check skipped.");
                    return;
                }

                var pendingUpdate = manager.UpdatePendingRestart;
                if (pendingUpdate != null)
                {
                    Debug.WriteLine($"[APP UPDATE] Applying pending update {pendingUpdate.Version}.");
                    manager.ApplyUpdatesAndRestart(pendingUpdate);
                    return;
                }

                var updateInfo = await manager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    Debug.WriteLine("[APP UPDATE] No updates available.");
                    return;
                }

                Debug.WriteLine($"[APP UPDATE] Downloading update {updateInfo.TargetFullRelease.Version}.");
                await manager.DownloadUpdatesAsync(updateInfo);

                Debug.WriteLine($"[APP UPDATE] Applying update {updateInfo.TargetFullRelease.Version}.");
                manager.ApplyUpdatesAndRestart(updateInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP UPDATE] Auto-update check failed: {ex}");
            }
        }
    }
}
