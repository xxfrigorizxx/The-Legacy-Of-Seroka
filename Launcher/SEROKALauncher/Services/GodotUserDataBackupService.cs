namespace SEROKALauncher.Services;

/// <summary>Sauvegarde <c>%APPDATA%\Godot\app_userdata\SEROKA</c> avant toute mise à jour launcher (les binaires game/ ne contiennent pas les saves).</summary>
public static class GodotUserDataBackupService
{
    private const string NomProjetGodot = "SEROKA";

    public static void SauvegarderAvantMiseAJourSiPresent(LauncherLogger logger)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userData = Path.Combine(appData, "Godot", "app_userdata", NomProjetGodot);
            if (!Directory.Exists(userData))
            {
                logger.Info("Aucun dossier user:// SEROKA a sauvegarder avant mise a jour.");
                return;
            }

            string backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SEROKAFrozenLegacy",
                "backups",
                "userdata_before_update_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            string destination = Path.Combine(backupRoot, NomProjetGodot);
            CopierRepertoire(userData, destination);
            logger.Info($"Sauvegarde user:// avant mise a jour: {destination}");
        }
        catch (Exception ex)
        {
            logger.Warn($"Sauvegarde user:// avant mise a jour impossible: {ex.Message}");
        }
    }

    private static void CopierRepertoire(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string fichier in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relatif = Path.GetRelativePath(source, fichier);
            string cible = Path.Combine(destination, relatif);
            Directory.CreateDirectory(Path.GetDirectoryName(cible)!);
            File.Copy(fichier, cible, overwrite: true);
        }
    }
}
