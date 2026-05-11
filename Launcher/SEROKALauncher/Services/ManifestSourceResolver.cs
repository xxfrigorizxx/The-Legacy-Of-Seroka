using SEROKALauncher.Models;
using System.Text.Json;

namespace SEROKALauncher.Services;

public sealed class ManifestSourceResolver
{
    private readonly LauncherConfig _config;
    private readonly LauncherLogger _logger;
    private readonly HttpClient _httpClient;

    public ManifestSourceResolver(LauncherConfig config, LauncherLogger logger)
    {
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(3, _config.NetworkTimeoutSeconds))
        };
    }

    public async Task<(LauncherManifest Manifest, Uri? Origin)> ResolveRemoteOrLocalSourceAsync(string launcherDirectory, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_config.LocalSourceManifestPath))
        {
            string path = ResolvePath(launcherDirectory, _config.LocalSourceManifestPath);
            _logger.Info($"Source manifest locale: {path}");
            LauncherManifest localSource = LauncherManifest.LoadFromFile(path);
            ManifestValidator.Validate(localSource);
            return (localSource, new Uri(path));
        }

        if (string.IsNullOrWhiteSpace(_config.ManifestUrl))
            throw new InvalidOperationException("ManifestUrl vide et aucune source locale fournie.");

        int tries = Math.Max(1, _config.NetworkRetryCount);
        Exception? lastError = null;
        for (int attempt = 1; attempt <= tries; attempt++)
        {
            try
            {
                _logger.Info($"Recuperation manifest distant (tentative {attempt}/{tries})...");
                string json = await _httpClient.GetStringAsync(_config.ManifestUrl, ct);
                LauncherManifest? manifest = JsonSerializer.Deserialize<LauncherManifest>(json, LauncherConfig.JsonOptions);
                if (manifest is null)
                    throw new InvalidDataException("Manifest distant nul apres deserialization.");
                ManifestValidator.Validate(manifest);
                return (manifest, new Uri(_config.ManifestUrl));
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.Warn($"Echec lecture manifest distant: {ex.Message}");
                if (attempt < tries)
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }

        throw new InvalidOperationException("Manifest distant indisponible.", lastError);
    }

    private static string ResolvePath(string launcherDirectory, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;
        return Path.GetFullPath(Path.Combine(launcherDirectory, configuredPath));
    }
}
