# SEROKALauncher

Launcher Windows pour `SEROKAFrozenLegacy.exe`.

## Capacites implementees

- Lecture de `launcher-config.json`
- Lecture manifest local (`manifests/local-manifest.json`)
- Lecture manifest source local **ou** distante (`manifestUrl`)
- Verification fichiers (`size` + `sha256`)
- Mise a jour atomique (`.tmp` -> remplacement)
- Retry unique en cas d'echec hash/taille
- Lancement de `game/SEROKAFrozenLegacy.exe`

## Arguments CLI

- `--install-root="C:\\Chemin\\SEROKAFrozenLegacy"`: force la racine d'installation
- `--config="C:\\Chemin\\launcher-config.json"`: force la config launcher
- `--check-only`: verifie manifests/fichiers sans lancer le jeu

## Arborescence attendue

```text
SEROKAFrozenLegacy/
  launcher/
    SEROKALauncher.exe
    launcher-config.json
  game/
    SEROKAFrozenLegacy.exe
    SEROKAFrozenLegacy.pck
  manifests/
    local-manifest.json
```

## Config rapide (offline/local source)

Voir `examples/launcher-config.local.example.json`.

## Config rapide (API distante)

Voir `examples/launcher-config.remote.example.json`.
