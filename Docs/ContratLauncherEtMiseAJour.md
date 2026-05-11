# Contrat Launcher et Mise a Jour (Etape 0)

Ce document fige les regles du launcher avant toute implementation reseau/API.
Objectif: eviter les migrations de structure dans 2 semaines.

## Identite produit (nom final)

- Nom jeu public: `SEROKAFrozenLegacy`
- Executable jeu Windows: `SEROKAFrozenLegacy.exe`
- Executable launcher Windows (propose): `SEROKALauncher.exe`

Le launcher doit toujours lancer `SEROKAFrozenLegacy.exe` (pas l'ancien nom).

## Canaux de release

- Canal actif initial: `alpha`
- Canaux reserves: `beta`, `stable`
- Un launcher est attache a un canal (config locale)

## Versioning

- Format recommande: SemVer avec suffixe prerelease
- Exemple initial: `0.1.0-alpha.1`
- Build id optionnel: `2026.05.10.01`

## Arborescence locale d'installation (Windows)

```text
SEROKAFrozenLegacy/
  launcher/
    SEROKALauncher.exe
    launcher-config.json
    launcher.log
  game/
    SEROKAFrozenLegacy.exe
    SEROKAFrozenLegacy.pck
    ...autres fichiers runtime du jeu...
  manifests/
    local-manifest.json
```

Regle: le jeu est contenu dans `game/`. Le launcher n'ecrit jamais dans `launcher/` sauf logs/config.

## Contrat manifest (minimum)

Champs obligatoires:

- `schemaVersion` (int)
- `channel` (string: `alpha|beta|stable`)
- `version` (string)
- `buildId` (string)
- `publishedAtUtc` (string ISO-8601 UTC)
- `entryExecutable` (string, ex: `SEROKAFrozenLegacy.exe`)
- `files` (liste)

Par fichier:

- `path` (chemin relatif a `game/`)
- `size` (octets)
- `sha256` (hex lowercase)
- `url` (https)
- `required` (bool)

## Regles de verification launcher

1. Lire le manifest local (si existe).
2. Lire le manifest distant (ou local pour l'etape 1 offline).
3. Comparer `version` + hashes par fichier.
4. Telecharger les fichiers differents.
5. Verifier `size` puis `sha256`.
6. Appliquer remplacement atomique (fichier temporaire puis rename).
7. Ecrire le nouveau `local-manifest.json`.
8. Lancer `game/SEROKAFrozenLegacy.exe`.

## Politique d'echec

- Hash invalide: supprimer le fichier telecharge et reessayer 1 fois.
- Echec persistant: ne pas lancer le jeu, afficher erreur explicite.
- Fichier `required=true` manquant: blocant.

## Rollback minimum

- Garder `current` et `previous` manifest cote distribution.
- Si version cassee, re-pointer `current` vers la precedente.

## API cible (future etape reseau)

Endpoint lecture manifest:

- `GET /launcher/manifest?channel=alpha&platform=windows-x64`

Reponse: JSON conforme au schema.

## Non-objectifs de cette etape 0

- Pas d'auth compte.
- Pas de patch delta.
- Pas de chiffrement des assets.

