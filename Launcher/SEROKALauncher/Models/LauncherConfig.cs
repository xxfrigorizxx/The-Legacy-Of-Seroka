using System.Text.Json;

namespace SEROKALauncher.Models;

public sealed class LauncherConfig
{
    public string Channel { get; init; } = "alpha";
    public string Platform { get; init; } = "windows-x64";
    public string ManifestUrl { get; init; } = "";
    public string EntryExecutable { get; init; } = "SEROKAFrozenLegacy.exe";
    public string GameDirectoryName { get; init; } = "game";
    public bool AllowPrerelease { get; init; } = true;
    public string? LocalSourceManifestPath { get; init; }
    public int NetworkTimeoutSeconds { get; init; } = 15;
    public int NetworkRetryCount { get; init; } = 2;

    public static LauncherConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Configuration launcher introuvable: {path}");

        using FileStream stream = File.OpenRead(path);
        LauncherConfig? config = JsonSerializer.Deserialize<LauncherConfig>(stream, JsonOptions);
        if (config is null)
            throw new InvalidDataException("launcher-config.json invalide (deserialization nulle).");
        return config;
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
