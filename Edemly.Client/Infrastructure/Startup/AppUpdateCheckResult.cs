using Velopack;

namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class AppUpdateCheckResult
    {
        private AppUpdateCheckResult(
            string feedUrl,
            bool isInstalled,
            UpdateInfo? updateInfo,
            VelopackAsset? pendingUpdate,
            AppUpdatePolicy policy,
            string statusMessage)
        {
            FeedUrl = feedUrl;
            IsInstalled = isInstalled;
            UpdateInfo = updateInfo;
            PendingUpdate = pendingUpdate;
            Policy = policy;
            StatusMessage = statusMessage;
        }

        public string FeedUrl { get; }
        public bool IsInstalled { get; }
        public bool HasUpdate => UpdateInfo != null || PendingUpdate != null;
        public bool IsPendingRestart => PendingUpdate != null;
        public bool IsMandatory => Policy.IsMandatoryForCurrentVersion();
        public string StatusMessage { get; }
        public AppUpdatePolicy Policy { get; }
        internal UpdateInfo? UpdateInfo { get; }
        internal VelopackAsset? PendingUpdate { get; }

        public string Version
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Policy.LatestVersion))
                {
                    return Policy.LatestVersion;
                }

                if (PendingUpdate?.Version != null)
                {
                    return PendingUpdate.Version.ToString();
                }

                return UpdateInfo?.TargetFullRelease.Version?.ToString() ?? string.Empty;
            }
        }

        public static AppUpdateCheckResult Skipped(string feedUrl, string message)
        {
            return new AppUpdateCheckResult(feedUrl, false, null, null, AppUpdatePolicy.Optional, message);
        }

        public static AppUpdateCheckResult NotInstalled(string feedUrl, AppUpdatePolicy policy)
        {
            return new AppUpdateCheckResult(feedUrl, false, null, null, policy, "Application is not installed by Velopack.");
        }

        public static AppUpdateCheckResult NoUpdate(string feedUrl, AppUpdatePolicy policy)
        {
            return new AppUpdateCheckResult(feedUrl, true, null, null, policy, "No updates available.");
        }

        public static AppUpdateCheckResult Available(string feedUrl, UpdateInfo updateInfo, AppUpdatePolicy policy)
        {
            return new AppUpdateCheckResult(feedUrl, true, updateInfo, null, policy, "Update available.");
        }

        public static AppUpdateCheckResult PendingRestart(string feedUrl, VelopackAsset pendingUpdate, AppUpdatePolicy policy)
        {
            return new AppUpdateCheckResult(feedUrl, true, null, pendingUpdate, policy, "Update pending restart.");
        }
    }
}
