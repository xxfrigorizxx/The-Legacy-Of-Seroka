using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Récupère les sauvegardes Godot laissées sous d’anciens noms de projet (<c>user://</c>)
/// avant le renommage applicatif <c>SEROKA</c> — cause fréquente de « perte totale » après passage launcher.
/// </summary>
public static class UserDataMigrationService
{
    private static bool _migrationTentee;

    /// <summary>Noms historiques de dossiers <c>%APPDATA%\Godot\app_userdata\</c>.</summary>
    private static readonly string[] NomsDossiersUserDataLegacy =
    {
        "Zero-K - Frozen Legacy",
        "Zero-K-Frozen-Legacy",
        "SEROKAFrozenLegacy"
    };

    public static void ExecuterMigrationAuDemarrageSiBesoin()
    {
        if (_migrationTentee || Engine.IsEditorHint())
            return;
        _migrationTentee = true;

        try
        {
            string cible = ProjectSettings.GlobalizePath("user://").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(cible) || !Directory.Exists(Path.GetDirectoryName(cible)))
                return;

            string racineGodot = ObtenirRacineAppUserdataGodot();
            if (string.IsNullOrEmpty(racineGodot))
                return;

            int fichiersFusionnes = 0;
            foreach (string nomLegacy in NomsDossiersUserDataLegacy)
            {
                string source = Path.Combine(racineGodot, nomLegacy);
                if (!Directory.Exists(source))
                    continue;
                if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(cible), StringComparison.OrdinalIgnoreCase))
                    continue;

                fichiersFusionnes += FusionnerArborescenceUserData(source, cible);
            }

            if (fichiersFusionnes > 0)
                GD.Print($"ZERO-K : Migration user:// — {fichiersFusionnes} fichier(s) récupéré(s) depuis un profil Godot legacy.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ZERO-K : Migration user:// échouée (sauvegardes inchangées) : {ex.Message}");
        }
    }

    private static string ObtenirRacineAppUserdataGodot()
    {
        string cible = ProjectSettings.GlobalizePath("user://");
        if (string.IsNullOrWhiteSpace(cible))
            return "";

        string parent = Directory.GetParent(cible.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
        if (parent != null && string.Equals(Path.GetFileName(parent), "app_userdata", StringComparison.OrdinalIgnoreCase))
            return parent;

        string appData = global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.ApplicationData);
        string fallback = Path.Combine(appData, "Godot", "app_userdata");
        return Directory.Exists(fallback) ? fallback : "";
    }

    private static int FusionnerArborescenceUserData(string source, string cible)
    {
        int ajoutes = 0;
        var aIgnorer = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "shader_cache", "vulkan", "logs", "objectdb_snapshots"
        };

        foreach (string fichierSource in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relatif = Path.GetRelativePath(source, fichierSource);
            string premierSegment = relatif.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (aIgnorer.Contains(premierSegment))
                continue;

            string fichierCible = Path.Combine(cible, relatif);
            if (FichierDoitEtreCopie(fichierSource, fichierCible))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fichierCible)!);
                File.Copy(fichierSource, fichierCible, overwrite: false);
                ajoutes++;
            }
        }

        return ajoutes;
    }

    private static bool FichierDoitEtreCopie(string source, string cible)
    {
        if (!File.Exists(cible))
            return true;
        try
        {
            DateTime src = File.GetLastWriteTimeUtc(source);
            DateTime dst = File.GetLastWriteTimeUtc(cible);
            return src > dst.AddSeconds(2);
        }
        catch
        {
            return false;
        }
    }
}
