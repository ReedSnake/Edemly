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
                if (!TryParseArguments(commandLineArgs, out var serverUrl, out var tenantArg) ||
                    string.IsNullOrWhiteSpace(serverUrl))
                {
                    return null;
                }

                ApplyTenantOverride(config, tenantArg);

                var company = ResolveCompany(config);
                var apiBaseUrl = string.IsNullOrWhiteSpace(company)
                    ? serverUrl
                    : serverUrl.TrimEnd('/') + "/" + company.Trim().Trim('/');
                var cacheScope = string.IsNullOrWhiteSpace(company) ? "personal" : company.Trim();

                return new AppLaunchConfiguration(serverUrl, apiBaseUrl, cacheScope);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP LAUNCH] Resolve failed: {ex}");
                return null;
            }
        }

        private static bool TryParseArguments(string[] commandLineArgs, out string? serverUrl, out string? tenantArg)
        {
            serverUrl = null;
            tenantArg = null;

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
                    if (raw.StartsWith("--tenant", StringComparison.OrdinalIgnoreCase) ||
                        raw.StartsWith("--company", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = raw.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            tenantArg = parts[1].Trim().Trim('"');
                        }
                        else if (i + 1 < commandLineArgs.Length)
                        {
                            tenantArg = commandLineArgs[i + 1].Trim().Trim('"');
                            i++;
                        }
                    }

                    continue;
                }

                if (serverUrl != null)
                {
                    continue;
                }

                var candidate = raw;
                if (!candidate.Contains("://"))
                {
                    candidate = "https://" + candidate;
                }

                if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
                {
                    serverUrl = parsed.ToString().TrimEnd('/');
                }
            }

            return !string.IsNullOrWhiteSpace(serverUrl);
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
    }
}
