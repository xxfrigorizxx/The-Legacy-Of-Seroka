# Notes — nage joueur APISARA vs autres dimensions

À traiter dans une future mise à jour (analyse juin 2026).

## Constat

- **Même code joueur** partout : `Core/Movement/PlayerMovementWater.cs` (pas de branche APISARA).
- APISARA **semble** beaucoup plus fluide surtout à cause du **monde + streaming**, pas d’une physique dédiée.

## Différences clés

| Sujet | APISARA | Alpha / Beta / Omega / Delta |
|-------|---------|------------------------------|
| Niveau mer (génération) | Y = **19** (`AbyssNiveauEau`) | Y = **103** (`NiveauEau`) |
| Tranches 100 m | **Non** (`ModeProfondeurTranchesActif` = false) | **Oui** (`ActiverProfondeurEtendue`) |
| Eau volumétrique | Colonnes 2D classiques | Corps + chapeau 3D (`InitialiserEauVolumetrique`) |
| Frein « corridor mesh » | **Absent** | **Actif** si mesh visible sans collision (`EstFreinCorridorMeshSansCollisionActif`) |
| Animation nage joueur | Aucune (idle/marche) | Idem |

## Fichiers de référence

- Joueur : `Core/Movement/PlayerMovementWater.cs`
- Surface locale : `EssayerTrouverSurfaceEauY` ; défaut global `Gestionnaire_Monde.ObtenirNiveauSurfaceEau()` = 103.35 (toutes dimensions).
- Génération eau : `Serveur/Chunk_Serveur.cs` → `InitialiserEauVolumetrique`
- Tranches : `Core/World/ConstantesProfondeurVerticale.cs`, `Client/Monde_Client.ChunkAvailabilityAbysse.cs`
- Frein corridor : `Client/Monde_Client.VerticalMcPadding.cs` → `CorridorMarcheBloque`
- Référence faune (nage + anim) : `Faune/BoeufSauvage.Natation.cs`

## Pistes correctifs (par impact probable)

1. **Ne pas freiner** le mouvement horizontal en nage (`freinCorridorMesh` si `estDansEau`).
2. **Fiabiliser l’eau** aux jonctions de tranches 100 m (coordY 0 ↔ 1, Y ≈ 100–103).
3. **`ObtenirNiveauSurfaceEau()` par dimension** (19 APISARA, 103 Alpha-like).
4. **Animation nage** joueur (comme bovins).
5. **Brancher** `DetecterBordBergeSortieEau` (déjà écrit, jamais appelé).

## Tests manuels suggérés

- Mer Alpha : nager en ligne droite vers bord de chunk (frein / saccades).
- APISARA : anneau d’eau Y≈19 vs prairie centrale.
- Remontée espace maintenu : surface locale trouvée vs repli 103.35.
