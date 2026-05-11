using SEROKALauncher.Models;

namespace SEROKALauncher.Services;

public sealed class UpdateService
{
    private readonly LauncherLogger _logger;
    private readonly HttpClient _httpClient = new();

    public UpdateService(LauncherLogger logger)
    {
        _logger = logger;
    }

    public List<ManifestFileEntry> ComputeFilesToUpdate(LauncherManifest targetManifest, string gameDirectory)
    {
        var files = new List<ManifestFileEntry>();
        foreach (ManifestFileEntry entry in targetManifest.Files)
        {
            string fullPath = Path.GetFullPath(Path.Combine(gameDirectory, entry.Path));
            if (!FileIntegrityService.IsMatching(fullPath, entry))
                files.Add(entry);
        }
        return files;
    }

    public async Task ApplyUpdatesAsync(
        IReadOnlyList<ManifestFileEntry> files,
        string gameDirectory,
        Uri? manifestOrigin,
        CancellationToken ct)
    {
        foreach (ManifestFileEntry entry in files)
        {
            ct.ThrowIfCancellationRequested();
            await DownloadValidateAndReplaceAsync(entry, gameDirectory, manifestOrigin, ct);
        }
    }

    private async Task DownloadValidateAndReplaceAsync(ManifestFileEntry entry, string gameDirectory, Uri? manifestOrigin, CancellationToken ct)
    {
        string finalPath = Path.GetFullPath(Path.Combine(gameDirectory, entry.Path));
        string tempPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            _logger.Info($"Telechargement {entry.Path} (tentative {attempt}/2)...");
            await DownloadToFileAsync(entry, manifestOrigin, tempPath, ct);

            FileInfo fi = new(tempPath);
            if (fi.Length != entry.Size)
            {
                _logger.Warn($"Taille invalide pour {entry.Path}. Attendu={entry.Size}, obtenu={fi.Length}");
                File.Delete(tempPath);
                continue;
            }

            string hash = FileIntegrityService.ComputeSha256Hex(tempPath);
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warn($"SHA-256 invalide pour {entry.Path}. Attendu={entry.Sha256}, obtenu={hash}");
                File.Delete(tempPath);
                continue;
            }

            if (File.Exists(finalPath))
            {
                File.Replace(tempPath, finalPath, backupPath, true);
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            else
            {
                File.Move(tempPath, finalPath);
            }
            _logger.Info($"Mise a jour appliquee: {entry.Path}");
            return;
        }

        throw new IOException($"Echec mise a jour apres retry hash/taille: {entry.Path}");
    }

    private async Task DownloadToFileAsync(ManifestFileEntry entry, Uri? manifestOrigin, string destinationPath, CancellationToken ct)
    {
        Uri uri = ResolveFileUri(entry, manifestOrigin);
        if (uri.IsFile)
        {
            string sourcePath = uri.LocalPath;
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source locale introuvable: {sourcePath}");
            File.Copy(sourcePath, destinationPath, true);
            return;
        }

        using HttpResponseMessage response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using Stream inStream = await response.Content.ReadAsStreamAsync(ct);
        await using FileStream outStream = File.Create(destinationPath);
        await inStream.CopyToAsync(outStream, ct);
    }

    private static Uri ResolveFileUri(ManifestFileEntry entry, Uri? manifestOrigin)
    {
        if (Uri.TryCreate(entry.Url, UriKind.Absolute, out Uri? absolute))
            return absolute;
        if (manifestOrigin is null)
            throw new InvalidOperationException($"URL relative sans origine manifest: {entry.Url}");
        if (manifestOrigin.IsFile)
        {
            string parent = Path.GetDirectoryName(manifestOrigin.LocalPath) ?? "";
            string candidate = Path.GetFullPath(Path.Combine(parent, entry.Url));
            return new Uri(candidate);
        }
        return new Uri(manifestOrigin, entry.Url);
    }
}
