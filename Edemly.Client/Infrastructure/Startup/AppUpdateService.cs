using System.Diagnostics;
using Velopack;

namespace Edemly.Client.Infrastructure.Startup
{
    public static class AppUpdateService
    {
        public static async Task<AppUpdateCheckResult> CheckForUpdateAsync(
            string updateFeedUrl,
            AppUpdatePolicy updatePolicy,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(updateFeedUrl))
            {
                return AppUpdateCheckResult.Skipped(updateFeedUrl, "Update feed URL is empty.");
            }

            updatePolicy ??= AppUpdatePolicy.Optional;

            try
            {
                var manager = new UpdateManager(updateFeedUrl);
                if (!manager.IsInstalled)
                {
                    Debug.WriteLine("[APP UPDATE] Application is not installed by Velopack. Update check skipped.");
                    return AppUpdateCheckResult.NotInstalled(updateFeedUrl, updatePolicy);
                }

                var pendingUpdate = manager.UpdatePendingRestart;
                if (pendingUpdate != null)
                {
                    Debug.WriteLine($"[APP UPDATE] Pending update {pendingUpdate.Version} is ready to apply.");
                    return AppUpdateCheckResult.PendingRestart(updateFeedUrl, pendingUpdate, updatePolicy);
                }

                cancellationToken.ThrowIfCancellationRequested();

                var updateInfo = await manager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    Debug.WriteLine("[APP UPDATE] No updates available.");
                    return AppUpdateCheckResult.NoUpdate(updateFeedUrl, updatePolicy);
                }

                Debug.WriteLine($"[APP UPDATE] Update {updateInfo.TargetFullRelease.Version} is available.");
                return AppUpdateCheckResult.Available(updateFeedUrl, updateInfo, updatePolicy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP UPDATE] Update check failed: {ex}");
                return AppUpdateCheckResult.Skipped(updateFeedUrl, ex.Message);
            }
        }

        public static async Task DownloadAndApplyUpdateAsync(
            AppUpdateCheckResult update,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (update == null || string.IsNullOrWhiteSpace(update.FeedUrl) || !update.HasUpdate)
            {
                return;
            }

            var manager = new UpdateManager(update.FeedUrl);
            if (!manager.IsInstalled)
            {
                Debug.WriteLine("[APP UPDATE] Cannot apply update because the app is not installed by Velopack.");
                return;
            }

            if (update.PendingUpdate != null)
            {
                Debug.WriteLine($"[APP UPDATE] Applying pending update {update.PendingUpdate.Version}.");
                manager.ApplyUpdatesAndRestart(update.PendingUpdate);
                return;
            }

            if (update.UpdateInfo == null)
            {
                return;
            }

            Debug.WriteLine($"[APP UPDATE] Downloading update {update.UpdateInfo.TargetFullRelease.Version}.");
            await manager.DownloadUpdatesAsync(update.UpdateInfo, progress, cancellationToken);

            Debug.WriteLine($"[APP UPDATE] Applying update {update.UpdateInfo.TargetFullRelease.Version}.");
            manager.ApplyUpdatesAndRestart(update.UpdateInfo.TargetFullRelease);
        }
    }
}
