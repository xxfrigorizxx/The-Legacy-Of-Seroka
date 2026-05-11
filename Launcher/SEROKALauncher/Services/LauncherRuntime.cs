using SEROKALauncher.Models;
using System.Diagnostics;

namespace SEROKALauncher.Services;

public sealed class LauncherRuntime
{
    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            bool checkOnly = args.Any(a => string.Equals(a, "--check-only", StringComparison.OrdinalIgnoreCase));
            RuntimePaths paths = ResolveRuntimePaths(args);
            var logger = new LauncherLogger(paths.LogPath);
            logger.Info("Demarrage SEROKALauncher.");

            LauncherConfig config = LauncherConfig.Load(paths.ConfigPath);
            logger.Info($"Canal={config.Channel}, Plateforme={config.Platform}");
            string gameDirectory = Path.Combine(paths.InstallRoot, config.GameDirectoryName);

            LauncherManifest? localManifest = TryLoadManifest(paths.LocalManifestPath, logger);

            var sourceResolver = new ManifestSourceResolver(config, logger);
            LauncherManifest remoteManifest;
            Uri? remoteOrigin;
            try
            {
                (remoteManifest, remoteOrigin) = await sourceResolver.ResolveRemoteOrLocalSourceAsync(paths.LauncherDirectory, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.Warn($"Manifest source indisponible: {ex.Message}");
                if (localManifest is null)
                    throw new InvalidOperationException("Aucun manifest local exploitable.");
                remoteManifest = localManifest;
                remoteOrigin = null;
            }

            var updater = new UpdateService(logger);
            List<ManifestFileEntry> toUpdate = updater.ComputeFilesToUpdate(remoteManifest, gameDirectory);
            if (toUpdate.Count > 0)
            {
                logger.Info($"{toUpdate.Count} fichier(s) a mettre a jour.");
                await updater.ApplyUpdatesAsync(toUpdate, gameDirectory, remoteOrigin, CancellationToken.None);
                Directory.CreateDirectory(Path.GetDirectoryName(paths.LocalManifestPath)!);
                await File.WriteAllTextAsync(paths.LocalManifestPath, remoteManifest.ToJson());
                localManifest = remoteManifest;
            }
            else
            {
                logger.Info("Aucune mise a jour necessaire.");
                localManifest = remoteManifest;
                if (!File.Exists(paths.LocalManifestPath) || !string.Equals(File.ReadAllText(paths.LocalManifestPath), remoteManifest.ToJson(), StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(paths.LocalManifestPath)!);
                    await File.WriteAllTextAsync(paths.LocalManifestPath, remoteManifest.ToJson());
                    logger.Info("Manifest local synchronise avec le manifest distant.");
                }
            }

            ValidateRequiredFiles(localManifest, gameDirectory, logger);
            if (checkOnly)
            {
                logger.Info("Verification uniquement demandee (--check-only).");
                return 0;
            }
            LaunchGame(gameDirectory, localManifest.EntryExecutable, logger);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERREUR launcher: {ex.Message}");
            return 1;
        }
    }

    private static LauncherManifest? TryLoadManifest(string path, LauncherLogger logger)
    {
        if (!File.Exists(path))
        {
            logger.Warn($"Manifest local absent: {path}");
            return null;
        }
        try
        {
            LauncherManifest manifest = LauncherManifest.LoadFromFile(path);
            ManifestValidator.Validate(manifest);
            logger.Info($"Manifest local charge: {manifest.Version}");
            return manifest;
        }
        catch (Exception ex)
        {
            logger.Warn($"Manifest local invalide: {ex.Message}");
            return null;
        }
    }

    private static void ValidateRequiredFiles(LauncherManifest manifest, string gameDirectory, LauncherLogger logger)
    {
        foreach (ManifestFileEntry file in manifest.Files.Where(f => f.Required))
        {
            string fullPath = Path.GetFullPath(Path.Combine(gameDirectory, file.Path));
            if (!FileIntegrityService.IsMatching(fullPath, file))
                throw new IOException($"Fichier requis invalide ou absent: {file.Path}");
        }
        logger.Info("Validation fichiers requis OK.");
    }

    private static void LaunchGame(string gameDirectory, string executableName, LauncherLogger logger)
    {
        string gameExe = Path.Combine(gameDirectory, executableName);
        if (!File.Exists(gameExe))
            throw new FileNotFoundException($"Executable jeu introuvable: {gameExe}");

        var psi = new ProcessStartInfo
        {
            FileName = gameExe,
            WorkingDirectory = gameDirectory,
            UseShellExecute = true
        };
        Process.Start(psi);
        logger.Info($"Jeu lance: {gameExe}");
    }

    private static RuntimePaths ResolveRuntimePaths(string[] args)
    {
        string launcherDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string installRoot = Directory.GetParent(launcherDirectory)?.FullName ?? launcherDirectory;
        string configPath = Path.Combine(launcherDirectory, "launcher-config.json");

        foreach (string arg in args)
        {
            if (arg.StartsWith("--install-root=", StringComparison.OrdinalIgnoreCase))
            {
                installRoot = Path.GetFullPath(arg["--install-root=".Length..].Trim('"'));
            }
            else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
            {
                configPath = Path.GetFullPath(arg["--config=".Length..].Trim('"'));
            }
        }

        string launcherDirFromConfig = Path.GetDirectoryName(configPath) ?? launcherDirectory;
        if (!Directory.Exists(launcherDirFromConfig))
            Directory.CreateDirectory(launcherDirFromConfig);

        // Respecte le contrat etape 0.
        string gameDirectory = Path.Combine(installRoot, "game");
        string manifestsDirectory = Path.Combine(installRoot, "manifests");
        string appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SEROKAFrozenLegacy",
            "launcher");
        Directory.CreateDirectory(appDataRoot);
        return new RuntimePaths(
            launcherDirFromConfig,
            gameDirectory,
            Path.Combine(manifestsDirectory, "local-manifest.json"),
            configPath,
            Path.Combine(appDataRoot, "launcher.log"),
            installRoot);
    }

    private sealed record RuntimePaths(
        string LauncherDirectory,
        string GameDirectory,
        string LocalManifestPath,
        string ConfigPath,
        string LogPath,
        string InstallRoot);
}
