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

