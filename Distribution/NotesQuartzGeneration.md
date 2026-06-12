# Quartz — état et règles de génération

## Deux choses différentes dans le jeu

| Nom | ID terrain | Statut | Où |
|-----|------------|--------|-----|
| **Sable de quartz** (surface blanche) | **49** | **Actif** | Fond / bords d'eau |
| **Minerai quartz** (veine dans la roche) | **19** | **Actif** (`SpawnMineraiQuartz = true`) | Filons verticaux serpentins dédiés |

---

## Sable de quartz (ID 49) — règles actives

**Fichiers :** `Serveur/Chunk_Serveur.cs` (`DeterminerMateriauCroûte`), `Generateur_Voxel.cs` (miroir client/oracle), `TerrainVoxel.gdshader` (teinte blanche sur `03_sable.png`).

**Niveau mer de référence :** `NiveauEau = 103`, `NiveauPlage = 102`.

### Conditions de colonne (X, Z)

- `bordEau` : hauteur de surface entre **102 et 105** (`NiveauEau - 1` … `NiveauEau + 2`)
- `fondEau` : hauteur de surface **≤ 101** (`NiveauEau - 1`)

### Bruit procédural

```
bruitSableQuartz = noise2D(x * 2.75 + 5100, z * 2.75 - 3900)
```

(`_noiseHumiditeDetail` côté serveur, `_noiseHumidite` / `noiseNeige` côté générateur selon le fichier.)

### Règles de placement (après neige / roche / argile jungle)

| Zone | Condition bruit | Fréquence approx. |
|------|-----------------|-------------------|
| **Fond d'eau / rivière** | `fondEau` **et** `bruitSableQuartz > 0.86` | ~14 % des colonnes fond |
| **Bord d'eau** | `bordEau` **et** `bruitSableQuartz > 0.93` | ~3,5 % des colonnes bord |

Pas de filon 3D : c'est de la **croûte de surface** (comme sable / argile), pas une veine dans la pierre.

### Rendu

- Texture : même couche que le sable (`03_sable.png`)
- Shader : teinte blanc cassé selon la luminosité du grain (ID 49)

### Inventaire / créatif

- Tag : `VOXEL_TERRAIN:49` (proxy roche ID 2 en inventaire — évite collision roches matière 40–51)
- Validation : `Atlas_Matiere.EstGenomeVoxelTerrainValide`

### Où le voir

- **Nouveau monde** ou chunks **jamais générés** (chunks déjà sauvés gardent l'ancien terrain)
- Fond des rivières et berges : bandes blanches parmi le sable beige
- Créatif : « Voxel terrain: Sable de quartz (ID 49) »

---

## Minerai quartz (ID 19) — filons verticaux serpentins

**Fichier :** `Serveur/Chunk_Serveur.Minerais.cs` (`AppliquerFilonsQuartz`)

Système dédié (comme le charbon), **pas** les veines 3D génériques.

### Zone principale (sous-terrain profond)

| Paramètre | Valeur |
|-----------|--------|
| Y monde | **-300 à -100** |
| Orientation | Quasi **verticale** ; ~7 % des filons quasi purs ; sinon **diagonale** légère + serpentin X/Z |
| Épaisseur | **1 à 5 m** (variable le long du filon, pas constante) |
| Longueur | **20 à 250 m** — courts fréquents, très longs rares (distribution biaisée + seuil au-delà de ~120 m) |
| Forme | Centre serpentin (`sin`/`cos` multi-fréquences) — tortueux comme de la serpentine |
| Hôte | Roche **ID 2** solide, hors eau |
| Présence | Grille ~28 m en X/Z, seuil ~0,38 (1 à 3 filons par cellule) |

### Montagne (exception rare)

- Colonnes dont la surface ≥ **150** m (`QuartzSeuilHauteurMontagne`)
- **Mini-filons** entre **Y = -100** et **surface − 4 m** (au-dessus de la bande principale)
- Très rares (seuil ~0,955), épaisseur **1–2 m**, longueur **8–32 m**

### Général

- Pas en **APISARA** (`_generationAbysseActive`)
- **Nouveau monde** ou chunks vierges pour voir le terrain généré
- Les **sauvegardes déjà visitées** reçoivent les filons au rechargement (`RetroAppliquerFilonsQuartzDepuisDisque`) — relance le monde ou revisite la zone
- Smoke test : `godot --path . res://Tests/SmokeQuartzFilon.tscn --headless` → `artifacts/smoke_quartz_filon.log`
