using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace Edemly.Client.Infrastructure.Startup
{
    public static class AppLaunchConfigurationResolver
    {
        private const string DefaultClientConfigUrl = "http://localhost:8080/client.json";

        public static AppLaunchConfiguration? Resolve(string[] commandLineArgs, IConfigService? config)
        {
            return ResolveAsync(commandLineArgs, config).GetAwaiter().GetResult();
        }

        public static async Task<AppLaunchConfiguration?> ResolveAsync(
            string[] commandLineArgs,
            IConfigService? config,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var args = ParseArguments(commandLineArgs);

                if (!string.IsNullOrWhiteSpace(args.ServerUrl))
                {
                    return BuildConfiguration(
                        config,
                        args.ServerUrl,
                        discoveredHubServerUrl: null,
                        selectedServerName: "command-line",
                        clientConfigUrl: ResolveClientConfigUrl(args, config),
                        updateFeedUrl: ResolveUpdateFeedUrl(args.UpdateFeedUrl, config?.UpdateFeedUrl, null),
                        updatePolicy: AppUpdatePolicy.Optional,
                        tenantArg: args.TenantArg,
                        hubServerArg: args.HubServerArg);
                }

                var clientConfigUrl = ResolveClientConfigUrl(args, config);
                var bootstrap = await TryLoadBootstrapConfigAsync(clientConfigUrl, cancellationToken);
                if (bootstrap != null)
                {
                    var selectedServer = await SelectServerAsync(bootstrap, cancellationToken);
                    if (selectedServer != null && TryNormalizeServerUrl(selectedServer.ApiBaseUrl, out var apiBaseUrl))
                    {
                        var updateFeedUrl = ResolveUpdateFeedUrl(
                            args.UpdateFeedUrl,
                            config?.UpdateFeedUrl,
                            bootstrap.Updates?.WindowsStableUrl);

                        return BuildConfiguration(
                            config,
                            apiBaseUrl,
                            discoveredHubServerUrl: selectedServer.HubBaseUrl,
                            selectedServerName: selectedServer.Name ?? "static",
                            clientConfigUrl: clientConfigUrl,
                            updateFeedUrl: updateFeedUrl,
                            updatePolicy: BuildUpdatePolicy(bootstrap.Updates),
                            tenantArg: args.TenantArg,
                            hubServerArg: args.HubServerArg);
                    }
                }

                if (!string.IsNullOrWhiteSpace(config?.ServerUrl) &&
                    TryNormalizeServerUrl(config.ServerUrl, out var savedServerUrl))
                {
                    return BuildConfiguration(
                        config,
                        savedServerUrl,
                        discoveredHubServerUrl: config.HubServerUrl,
                        selectedServerName: "saved",
                        clientConfigUrl: clientConfigUrl,
                        updateFeedUrl: ResolveUpdateFeedUrl(args.UpdateFeedUrl, config.UpdateFeedUrl, null),
                        updatePolicy: AppUpdatePolicy.Optional,
                        tenantArg: args.TenantArg,
                        hubServerArg: args.HubServerArg);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP LAUNCH] Resolve failed: {ex}");
                return null;
            }
        }

        private static AppLaunchConfiguration BuildConfiguration(
            IConfigService? config,
            string serverUrl,
            string? discoveredHubServerUrl,
            string selectedServerName,
            string clientConfigUrl,
            string updateFeedUrl,
            AppUpdatePolicy? updatePolicy,
            string? tenantArg,
            string? hubServerArg)
        {
            ApplyTenantOverride(config, tenantArg);

            var company = ResolveCompany(config);
            var apiBaseUrl = string.IsNullOrWhiteSpace(company)
                ? serverUrl
                : serverUrl.TrimEnd('/') + "/" + company.Trim().Trim('/');
            var hubServerUrl = ResolveHubServerUrl(config, hubServerArg, discoveredHubServerUrl, serverUrl);
            var cacheScope = string.IsNullOrWhiteSpace(company) ? "personal" : company.Trim();

            PersistConfiguration(config, serverUrl, hubServerUrl, clientConfigUrl, updateFeedUrl);

            return new AppLaunchConfiguration(
                serverUrl,
                apiBaseUrl,
                hubServerUrl,
                cacheScope,
                updateFeedUrl,
                clientConfigUrl,
                selectedServerName,
                updatePolicy ?? AppUpdatePolicy.Optional);
        }

        private static LaunchArguments ParseArguments(string[] commandLineArgs)
        {
            var parsed = new LaunchArguments();

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
                        parsed.TenantArg = tenantValue;
                    }
                    else if (TryReadOptionValue(
                                 commandLineArgs,
                                 ref i,
                                 raw,
                                 new[] { "--hub-server", "--hub-url", "--hubs" },
                                 out var hubValue))
                    {
                        parsed.HubServerArg = hubValue;
                    }
                    else if (TryReadOptionValue(
                                 commandLineArgs,
                                 ref i,
                                 raw,
                                 new[] { "--config-url", "--client-config", "--bootstrap-url" },
                                 out var configUrl))
                    {
                        parsed.ClientConfigUrl = configUrl;
                    }
                    else if (TryReadOptionValue(
                                 commandLineArgs,
                                 ref i,
                                 raw,
                                 new[] { "--update-url", "--updates-url", "--update-feed" },
                                 out var updateUrl))
                    {
                        parsed.UpdateFeedUrl = updateUrl;
                    }
                    else if (TryReadOptionValue(
                                 commandLineArgs,
                                 ref i,
                                 raw,
                                 new[] { "--server", "--server-url", "--api-server" },
                                 out var serverValue) &&
                             TryNormalizeServerUrl(serverValue, out var optionServerUrl))
                    {
                        parsed.ServerUrl = optionServerUrl;
                    }

                    continue;
                }

                if (parsed.ServerUrl != null)
                {
                    continue;
                }

                if (TryNormalizeServerUrl(raw, out var positionalServerUrl))
                {
                    parsed.ServerUrl = positionalServerUrl;
                }
            }

            return parsed;
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

        private static string ResolveClientConfigUrl(LaunchArguments args, IConfigService? config)
        {
            if (TryNormalizeAbsoluteUrl(args.ClientConfigUrl, out var argConfigUrl))
            {
                return argConfigUrl;
            }

            var envConfigUrl = Environment.GetEnvironmentVariable("EDEMLY_CLIENT_CONFIG_URL");
            if (TryNormalizeAbsoluteUrl(envConfigUrl, out var envUrl))
            {
                return envUrl;
            }

            if (TryNormalizeAbsoluteUrl(config?.ClientConfigUrl, out var savedUrl))
            {
                return savedUrl;
            }

            return DefaultClientConfigUrl;
        }

        private static string ResolveUpdateFeedUrl(string? argValue, string? savedValue, string? staticValue)
        {
            if (TryNormalizeAbsoluteUrl(argValue, out var argUrl))
            {
                return argUrl;
            }

            var envUpdateUrl = Environment.GetEnvironmentVariable("EDEMLY_UPDATE_FEED_URL");
            if (TryNormalizeAbsoluteUrl(envUpdateUrl, out var envUrl))
            {
                return envUrl;
            }

            if (TryNormalizeAbsoluteUrl(staticValue, out var staticUrl))
            {
                return staticUrl;
            }

            if (TryNormalizeAbsoluteUrl(savedValue, out var savedUrl))
            {
                return savedUrl;
            }

            return string.Empty;
        }

        private static AppUpdatePolicy BuildUpdatePolicy(ClientBootstrapUpdates? updates)
        {
            if (updates == null)
            {
                return AppUpdatePolicy.Optional;
            }

            return new AppUpdatePolicy(
                updates.LatestVersion ?? string.Empty,
                updates.MinimumRequiredVersion ?? string.Empty,
                updates.Mandatory,
                updates.InstallerUrl ?? string.Empty);
        }

        private static async Task<ClientBootstrapConfig?> TryLoadBootstrapConfigAsync(
            string clientConfigUrl,
            CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };

                var response = await httpClient.GetAsync(clientConfigUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[APP LAUNCH] Static client config request failed: {response.StatusCode} {clientConfigUrl}");
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonSerializer.DeserializeAsync<ClientBootstrapConfig>(
                    stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP LAUNCH] Failed to load static client config '{clientConfigUrl}': {ex.Message}");
                return null;
            }
        }

        private static async Task<ClientBootstrapServer?> SelectServerAsync(
            ClientBootstrapConfig bootstrap,
            CancellationToken cancellationToken)
        {
            var servers = bootstrap.Servers
                .Where(server => server.Enabled)
                .OrderBy(server => server.Priority)
                .ThenBy(server => server.Name)
                .ToList();

            foreach (var server in servers)
            {
                if (!TryNormalizeServerUrl(server.ApiBaseUrl, out var apiBaseUrl))
                {
                    continue;
                }

                if (await IsServerHealthyAsync(apiBaseUrl, cancellationToken))
                {
                    server.ApiBaseUrl = apiBaseUrl;
                    return server;
                }
            }

            var fallback = servers.FirstOrDefault(server => TryNormalizeServerUrl(server.ApiBaseUrl, out _));
            if (fallback != null)
            {
                TryNormalizeServerUrl(fallback.ApiBaseUrl, out var apiBaseUrl);
                fallback.ApiBaseUrl = apiBaseUrl;
                Debug.WriteLine($"[APP LAUNCH] No healthy server found. Falling back to '{fallback.Name ?? fallback.ApiBaseUrl}'.");
            }

            return fallback;
        }

        private static async Task<bool> IsServerHealthyAsync(string apiBaseUrl, CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2)
                };

                using var response = await httpClient.GetAsync(apiBaseUrl.TrimEnd('/') + "/health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP LAUNCH] Health check failed for '{apiBaseUrl}': {ex.Message}");
                return false;
            }
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

        private static string ResolveHubServerUrl(
            IConfigService? config,
            string? hubServerArg,
            string? discoveredHubServerUrl,
            string serverUrl)
        {
            if (!string.IsNullOrWhiteSpace(hubServerArg) &&
                TryNormalizeServerUrl(hubServerArg, out var hubServerUrl))
            {
                return hubServerUrl;
            }

            if (!string.IsNullOrWhiteSpace(discoveredHubServerUrl) &&
                TryNormalizeServerUrl(discoveredHubServerUrl, out var discoveredHubUrl))
            {
                return discoveredHubUrl;
            }

            if (!string.IsNullOrWhiteSpace(config?.HubServerUrl) &&
                TryNormalizeServerUrl(config.HubServerUrl, out var configuredHubServerUrl))
            {
                return configuredHubServerUrl;
            }

            return serverUrl;
        }

        private static void PersistConfiguration(
            IConfigService? config,
            string serverUrl,
            string hubServerUrl,
            string clientConfigUrl,
            string updateFeedUrl)
        {
            if (config == null)
            {
                return;
            }

            config.ServerUrl = serverUrl;
            config.HubServerUrl = hubServerUrl;
            config.ClientConfigUrl = clientConfigUrl;
            config.UpdateFeedUrl = updateFeedUrl;
            config.Save();
        }

        private static bool TryNormalizeServerUrl(string? raw, out string normalized)
        {
            normalized = string.Empty;
            if (!TryNormalizeAbsoluteUrl(raw, out var absoluteUrl))
            {
                return false;
            }

            normalized = absoluteUrl.TrimEnd('/');
            return true;
        }

        private static bool TryNormalizeAbsoluteUrl(string? raw, out string normalized)
        {
            normalized = string.Empty;
            var candidate = raw?.Trim();
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

        private sealed class LaunchArguments
        {
            public string? ServerUrl { get; set; }
            public string? TenantArg { get; set; }
            public string? HubServerArg { get; set; }
            public string? ClientConfigUrl { get; set; }
            public string? UpdateFeedUrl { get; set; }
        }

        private sealed class ClientBootstrapConfig
        {
            public int SchemaVersion { get; set; }
            public string? Environment { get; set; }
            public List<ClientBootstrapServer> Servers { get; set; } = new();
            public ClientBootstrapUpdates? Updates { get; set; }
        }

        private sealed class ClientBootstrapServer
        {
            public string? Name { get; set; }
            public string ApiBaseUrl { get; set; } = string.Empty;
            public string? HubBaseUrl { get; set; }
            public string? PaymentBaseUrl { get; set; }
            public int Priority { get; set; }
            public bool Enabled { get; set; } = true;
        }

        private sealed class ClientBootstrapUpdates
        {
            public string? WindowsStableUrl { get; set; }
            public string? LatestVersion { get; set; }
            public string? MinimumRequiredVersion { get; set; }
            public bool Mandatory { get; set; }
            public string? InstallerUrl { get; set; }
        }
    }
}
