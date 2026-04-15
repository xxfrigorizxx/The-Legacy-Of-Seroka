# Changelog — ajouts à venir

Ce fichier regroupe les **modifications prévues, en cours ou non encore fusionnées** dans une version datée. Le journal historique reste dans [`CHANGELOG.md`](CHANGELOG.md) ; les notes de release plus larges peuvent vivre dans [`CHANGELOG2.md`](CHANGELOG2.md).

## Règle d’écriture (pour tous les agents)

- Ajouter une **nouvelle entrée en haut** de la section `## Entrées`.
- Garder le format suivant :
  - Date (prévue ou du jour de la note)
  - Agent / auteur
  - Fichiers modifiés (chemins relatifs au dépôt)
  - Résumé court
  - Impact gameplay / technique
- Ne pas supprimer les anciennes entrées de ce fichier (on garde l’historique des intentions).
- Quand une entrée est **livrée en production**, la recopier si besoin dans `CHANGELOG.md` puis marquer ici la livraison (date + référence courte) ou la retirer si doublon assumé.

## Entrées

### 2026-04-14 — Cursor Agent

- **Fichiers modifiés**
  - `Atlas_Matiere.cs`
  - `Core/Combat/ToolCombatService.cs`
  - `Core/Rendering/HeldItemRenderService.cs`
  - `Core/Rendering/ModelInstantiationService.cs`
  - `Core/World/WorldInteractionService.cs`
  - `Faune/BoeufSauvage.cs`
  - `ItemPhysique.cs`
  - `Joueur.cs`
  - `Scenes/Faune/BoeufSauvage.tscn`
  - `Scenes/Faune/VacheSauvage.tscn`
  - `Modeles/Equipements/Epe_pierre_tier0.glb` (+ `.import`)
  - `changelog/CHANGELOG_3.md`
- **Résumé court**
  - **Faux primitive pierre (ID 112)** : recette atelier 3×3 (bâtons / corde / lame), durabilité, rendu `Epe_pierre_tier0.glb`, pose au sol, combat et persistance alignés sur les autres outils pierre.
  - **Objets lançables** : clic droit court dédié à la **pose** sous la visée (le fauchage gazon ne prend plus la priorité sur dague / faux / roche plate ou pointue) ; distance minimale de pose ramenée à **0,55 m** pour les lançables (pose au pied du curseur sans rejet silencieux à 1,4 m).
  - **Dague (105) au sol** : hitbox issue des **meshes du GLB** (enveloppes convexes composées, pas capsule seule) — compatible RigidBody dynamique (pas de trimesh concave).
  - **Minage** : le rayon ne cible plus le terrain « à travers » un **bœuf** sous le curseur (main nue et pioche) — aligné avec l’exclusion déjà faite pour ItemPhysique / rigides / arbres.
  - **Faune / tests** : ajustements mineurs sur `BoeufSauvage` et scènes de test associées.
- **Impact gameplay / technique**
  - Nouvel outil tranchant craftable et utilisable comme les outils pierre existants.
  - Pose au sol plus prévisible pour tous les objets lançables ; fauchage herbe inchangé au **clic gauche**.
  - Collisions dague posée plus fidèles au modèle 3D (convexes, pas maillage concave exact).
  - Minage cohérent face à la vache (moins d’extraction fantôme derrière l’animal).
  - Build vérifié : `dotnet build "Zero-K - Frozen Legacy.csproj"`.

### 2026-04-13 — Agent Codex

- **Fichiers modifiés**
  - `Client/Monde_Client.cs`
  - `Client/Chunk_Client.cs`
  - `Serveur/Chunk_Serveur.cs`
  - `Gestionnaire_Monde.cs`
  - `Faune/BoeufSauvage.cs`
  - `Tests/ExplorationAutoMondeRunner.cs`
- **Résumé court**
  - Optimisations CPU/GC orientées micro-freezes: réduction des coûts de solidification chunks, sérialisation chunk sans allocations intermédiaires, étalement de dormance objets posés, cache UI faune, et instrumentation frametime/GC du runner d’exploration.
- **Impact gameplay / technique**
  - Gameplay et visuel inchangés (aucune constante de combat/déplacement/rendu modifiée).
  - Build OK (`dotnet build`), smoke natation OK.
  - Nouveau rapport automatique `artifacts/exploration_perf_metrics.log` (run headless local): `p95=133.333ms`, `p99=146.475ms`, `max=150.000ms`, `spikes>=33ms=849`.
  - Le run exploration se termine avec un crash natif à la fermeture Godot (`AccessViolation`), après écriture du rapport de métriques.

