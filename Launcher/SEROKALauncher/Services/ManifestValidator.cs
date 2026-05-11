using SEROKALauncher.Models;
using System.Text.RegularExpressions;

namespace SEROKALauncher.Services;

internal static partial class ManifestValidator
{
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.Compiled)]
    private static partial Regex ShaRegex();

    public static void Validate(LauncherManifest manifest)
    {
        if (manifest.SchemaVersion < 1)
            throw new InvalidDataException("schemaVersion doit etre >= 1.");
        if (manifest.Channel is not ("alpha" or "beta" or "stable"))
            throw new InvalidDataException("channel doit etre alpha|beta|stable.");
        if (string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("version est obligatoire.");
        if (string.IsNullOrWhiteSpace(manifest.BuildId))
            throw new InvalidDataException("buildId est obligatoire.");
        if (!DateTimeOffset.TryParse(manifest.PublishedAtUtc, out _))
            throw new InvalidDataException("publishedAtUtc doit etre ISO-8601.");
        if (!string.Equals(manifest.EntryExecutable, "SEROKAFrozenLegacy.exe", StringComparison.Ordinal))
            throw new InvalidDataException("entryExecutable doit etre SEROKAFrozenLegacy.exe.");
        if (manifest.Files.Count == 0)
            throw new InvalidDataException("files ne peut pas etre vide.");

        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ManifestFileEntry file in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Path))
                throw new InvalidDataException("Un fichier manifest a un path vide.");
            if (!dedupe.Add(file.Path))
                throw new InvalidDataException($"Chemin duplique dans manifest: {file.Path}");
            if (file.Size < 0)
                throw new InvalidDataException($"size invalide pour {file.Path}");
            if (!ShaRegex().IsMatch(file.Sha256 ?? ""))
                throw new InvalidDataException($"sha256 invalide pour {file.Path}");
            if (string.IsNullOrWhiteSpace(file.Url))
                throw new InvalidDataException($"url vide pour {file.Path}");
        }
    }
}
