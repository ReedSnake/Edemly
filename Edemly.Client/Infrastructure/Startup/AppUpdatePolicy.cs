using System.Reflection;

namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class AppUpdatePolicy
    {
        public static AppUpdatePolicy Optional { get; } = new(
            latestVersion: string.Empty,
            minimumRequiredVersion: string.Empty,
            mandatory: false,
            installerUrl: string.Empty);

        public AppUpdatePolicy(
            string latestVersion,
            string minimumRequiredVersion,
            bool mandatory,
            string installerUrl)
        {
            LatestVersion = latestVersion;
            MinimumRequiredVersion = minimumRequiredVersion;
            Mandatory = mandatory;
            InstallerUrl = installerUrl;
        }

        public string LatestVersion { get; }
        public string MinimumRequiredVersion { get; }
        public bool Mandatory { get; }
        public string InstallerUrl { get; }

        public bool IsMandatoryForCurrentVersion()
        {
            if (Mandatory)
            {
                return true;
            }

            if (!TryParseVersion(MinimumRequiredVersion, out var requiredVersion))
            {
                return false;
            }

            var currentVersionText = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!TryParseVersion(currentVersionText, out var currentVersion))
            {
                currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            }

            return currentVersion < requiredVersion;
        }

        private static bool TryParseVersion(string? raw, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var normalized = raw.Trim();
            var metadataStart = normalized.IndexOfAny(new[] { '-', '+' });
            if (metadataStart >= 0)
            {
                normalized = normalized[..metadataStart];
            }

            return Version.TryParse(normalized, out version!);
        }
    }
}
