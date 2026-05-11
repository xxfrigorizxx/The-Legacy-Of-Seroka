using System.Text.Json;

namespace SEROKALauncher.Models;

public sealed class LauncherManifest
{
    public int SchemaVersion { get; init; }
    public string Channel { get; init; } = "";
    public string Version { get; init; } = "";
    public string BuildId { get; init; } = "";
    public string PublishedAtUtc { get; init; } = "";
    public string EntryExecutable { get; init; } = "SEROKAFrozenLegacy.exe";
    public string? Notes { get; init; }
    public List<ManifestFileEntry> Files { get; init; } = new();

    public static LauncherManifest LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Manifest introuvable: {path}");
        using FileStream stream = File.OpenRead(path);
        LauncherManifest? manifest = JsonSerializer.Deserialize<LauncherManifest>(stream, LauncherConfig.JsonOptions);
        if (manifest is null)
            throw new InvalidDataException($"Manifest JSON invalide: {path}");
        return manifest;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, LauncherConfig.JsonOptions);
    }
}

public sealed class ManifestFileEntry
{
    public string Path { get; init; } = "";
    public long Size { get; init; }
    public string Sha256 { get; init; } = "";
    public string Url { get; init; } = "";
    public bool Required { get; init; }
}
