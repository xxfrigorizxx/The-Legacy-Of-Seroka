# Checklist release alpha (launcher)

## 0) Pipeline unique recommande (source de verite)

- Utiliser **une seule commande** pour build + export + synchronisation payload + manifest + upload Backblaze:
  - `powershell -ExecutionPolicy Bypass -File "Distribution/Publish-AlphaRelease.ps1" -Version "0.1.0-alpha.X"`
- Ne plus publier via etapes manuelles partielles (sinon risque de desynchronisation exe/pck/dll).
- Le script publie:
  - `SEROKAFrozenLegacy.exe`
  - `SEROKAFrozenLegacy.pck`
  - `data_Zero-K - Frozen Legacy_windows_x86_64/*`
  - `manifest.alpha.json`

## 1) Build launcher (self-contained)

- `dotnet publish "Launcher/SEROKALauncher/SEROKALauncher.csproj" -c Release`
- Verifier presence de `SEROKALauncher.exe` et des DLL runtime dans `Launcher/SEROKALauncher/bin/Release/net8.0/win-x64/publish/`

## 2) Build jeu

- Export Godot **Windows release** (pas debug):
  - `godot --path . --export-release "Windows Desktop" "..\\SEROKAFrozenLegacy.exe"`
- Verifier qu'aucun suffixe `(debug)` n'apparait dans la fenetre du jeu apres installation.
- Ne pas utiliser `--export-debug` pour le payload distribution.
- Renommer sortie jeu en:
  - `SEROKAFrozenLegacy.exe`
  - `SEROKAFrozenLegacy.pck`

## 3) Generer manifest

- Copier les fichiers jeu dans le dossier payload cible
- Generer le manifest:
  - `powershell -ExecutionPolicy Bypass -File "Distribution/New-LauncherManifest.ps1" -GameDirectory "<payload_game_dir>" -OutputManifestPath "Distribution/manifest.alpha.json" -Version "0.1.0-alpha.1" -BuildId "2026.05.10.01" -Channel "alpha" -BaseUrl "https://cdn.seroka.example/alpha"`

## 4) Publier distribution

- Upload payload jeu sur CDN/storage
- Publier `manifest.alpha.json` (et garder `previous`)
- Mettre a jour endpoint API manifest

## 5) Packaging setup

- Ouvrir `Distribution/Installer/SEROKALauncher.iss`
- Ajuster `AppVersion`, chemins source et noms de fichiers
- Compiler avec `ISCC.exe`
- Recuperer le setup dans `Distribution/Installer/Output/`
- Verifier que l'installation cible est en dossier utilisateur (`%LOCALAPPDATA%\\SEROKAFrozenLegacy`) pour autoriser logs et mises a jour sans elevation admin

## 6) Smoke tests obligatoires

- Install propre sur PC vierge
- Lancement via raccourci bureau (launcher)
- `--check-only` retourne succes
- Corrompre un fichier du jeu -> launcher repare
- Simuler nouvelle version -> telechargement + lancement OK
- Manifest invalide -> erreur claire, aucun remplacement partiel

## 7) Reinitialisation test (si ecarts editeur vs launcher)

- Reinitialiser caches editeur + rebuild C#:
  - `powershell -ExecutionPolicy Bypass -File "Distribution/Reset-EditorState.ps1" -ForceKillGodot`
- Reinitialiser les donnees `user://` de test (backup auto):
  - `powershell -ExecutionPolicy Bypass -File "Distribution/Sync-TestUserData.ps1"`
