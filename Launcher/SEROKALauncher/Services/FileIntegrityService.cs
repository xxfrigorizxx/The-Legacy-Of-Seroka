using SEROKALauncher.Models;
using System.Security.Cryptography;

namespace SEROKALauncher.Services;

public static class FileIntegrityService
{
    public static string ComputeSha256Hex(string filePath)
    {
        using var sha = SHA256.Create();
        using FileStream stream = File.OpenRead(filePath);
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsMatching(string fullPath, ManifestFileEntry entry)
    {
        if (!File.Exists(fullPath))
            return false;

        FileInfo fi = new(fullPath);
        if (fi.Length != entry.Size)
            return false;

        string hash = ComputeSha256Hex(fullPath);
        return string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
