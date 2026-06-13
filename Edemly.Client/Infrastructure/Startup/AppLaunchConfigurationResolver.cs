using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;

namespace Edemly.Client.Infrastructure.Startup
{
    public static class AppLaunchConfigurationResolver
    {
        public static AppLaunchConfiguration? Resolve(string[] commandLineArgs, IConfigService? config)
        {
            try
            {
                if (!TryParseArguments(commandLineArgs, out var serverUrl, out var tenantArg, out var hubServerArg) ||
                    string.IsNullOrWhiteSpace(serverUrl))
                {
                    return null;
                }

                ApplyTenantOverride(config, tenantArg);
                ApplyHubServerOverride(config, hubServerArg);

                var company = ResolveCompany(config);
                var apiBaseUrl = string.IsNullOrWhiteSpace(company)
                    ? serverUrl
                    : serverUrl.TrimEnd('/') + "/" + company.Trim().Trim('/');
                var hubServerUrl = ResolveHubServerUrl(config, hubServerArg, serverUrl);
                var cacheScope = string.IsNullOrWhiteSpace(company) ? "personal" : company.Trim();

                return new AppLaunchConfiguration(serverUrl, apiBaseUrl, hubServerUrl, cacheScope);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP LAUNCH] Resolve failed: {ex}");
                return null;
            }
        }

        private static bool TryParseArguments(
            string[] commandLineArgs,
            out string? serverUrl,
            out string? tenantArg,
            out string? hubServerArg)
        {
            serverUrl = null;
            tenantArg = null;
            hubServerArg = null;

            if (commandLineArgs.Length <= 1)
            {
                return false;
            }

            for (int i = 1; i < commandLineArgs.Length; i++)
            {
                var raw = commandLineArgs[i].Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }

                if (raw.StartsWith("--") || raw.StartsWith("-"))
                {
                    if (TryReadOptionValue(
                            commandLineArgs,
                            ref i,
                            raw,
                            new[] { "--tenant", "--company" },
                            out var tenantValue))
                    {
                        tenantArg = tenantValue;
                    }
                    else if (TryReadOptionValue(
                                 commandLineArgs,
                                 ref i,
                                 raw,
                                 new[] { "--hub-server", "--hub-url", "--hubs" },
                                 out var hubValue))
                    {
                        hubServerArg = hubValue;
                    }

                    continue;
                }

                if (serverUrl != null)
                {
                    continue;
                }

                if (TryNormalizeServerUrl(raw, out var parsed))
                {
                    serverUrl = parsed;
                }
            }

            return !string.IsNullOrWhiteSpace(serverUrl);
        }

        private static bool TryReadOptionValue(
            string[] args,
            ref int index,
            string raw,
            string[] optionNames,
            out string? value)
        {
            value = null;

            foreach (var optionName in optionNames)
            {
                if (!raw.Equals(optionName, StringComparison.OrdinalIgnoreCase) &&
                    !raw.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = raw.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    value = parts[1].Trim().Trim('"');
                    return !string.IsNullOrWhiteSpace(value);
                }

                if (index + 1 < args.Length)
                {
                    value = args[index + 1].Trim().Trim('"');
                    index++;
                    return !string.IsNullOrWhiteSpace(value);
                }

                return false;
            }

            return false;
        }

        private static void ApplyTenantOverride(IConfigService? config, string? tenantArg)
        {
            if (config == null ||
                string.IsNullOrWhiteSpace(tenantArg) ||
                string.Equals(tenantArg, "Personal", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            config.Company = tenantArg.Trim();
            config.IsInstalled = true;
            config.Save();
        }

        private static void ApplyHubServerOverride(IConfigService? config, string? hubServerArg)
        {
            if (config == null ||
                string.IsNullOrWhiteSpace(hubServerArg) ||
                !TryNormalizeServerUrl(hubServerArg, out var hubServerUrl))
            {
                return;
            }

            config.HubServerUrl = hubServerUrl;
            config.Save();
        }

        private static string? ResolveCompany(IConfigService? config)
        {
            if (config == null ||
                !config.IsInstalled ||
                string.IsNullOrWhiteSpace(config.Company) ||
                string.Equals(config.Company, "Personal", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return config.Company.Trim();
        }

        private static string ResolveHubServerUrl(IConfigService? config, string? hubServerArg, string serverUrl)
        {
            if (!string.IsNullOrWhiteSpace(hubServerArg) &&
                TryNormalizeServerUrl(hubServerArg, out var hubServerUrl))
            {
                return hubServerUrl;
            }

            if (!string.IsNullOrWhiteSpace(config?.HubServerUrl) &&
                TryNormalizeServerUrl(config.HubServerUrl, out var configuredHubServerUrl))
            {
                return configuredHubServerUrl;
            }

            return serverUrl;
        }

        private static bool TryNormalizeServerUrl(string raw, out string normalized)
        {
            normalized = string.Empty;
            var candidate = raw.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            if (!candidate.Contains("://"))
            {
                candidate = "https://" + candidate;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
            {
                return false;
            }

            normalized = parsed.ToString().TrimEnd('/');
            return true;
        }
    }
}
