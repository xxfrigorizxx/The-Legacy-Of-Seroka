# Changelog Agents

Ce dossier sert de journal des modifications faites dans le projet.

## Regle d'ecriture (pour tous les agents)

- Ajouter une nouvelle entree en haut de la section `## Entrees`.
- Garder le format suivant:
  - Date
  - Agent
  - Fichiers modifies
  - Resume court
  - Impact gameplay/technique
- Ne pas supprimer les anciennes entrees.

## Entrees

### 2026-04-11 - Agent Codex

- **Fichiers modifies**
  - `Joueur.cs`
  - `Core/Gameplay/PlayerPersistenceService.cs`
  - `Core/Gameplay/CraftWorkflowService.cs`
  - `Core/Combat/ToolCombatService.cs`
  - `Core/World/WorldInteractionService.cs`
  - `FutureState_UI.cs`
  - `MenuAnatomie.cs`
- **Resume**
  - Refonte des progressions longues: niveau max global porte a `10_000_000_000_000_000_000`, XP Future States/Metiers migrees en `UInt128`, courbe XP hybride appliquee et persistance versionnee 128-bit (compatibilite lecture des anciennes saves).
  - Ajout/extension gameplay des states et metiers: `Dextiriter` (XP via fauchage gazon + craft fibres/lianes), `Traisage` (XP craft sacs/ceintures + chance de double craft tissu/corde), `Metaboliste` (XP tous les 10m et bonus vitesse +0.001%/niveau).
  - Systeme de charge revise: depassement autorise au-dela de la capacite max avec ralentissement progressif selon surcharge; capacite de charge liee explicitement a la Force (+0.01%/niveau).
  - UI Future States/Menu Q enrichie: affichage multiplicateurs (Force), charge actuelle/max, poids sous apercu joueur, et ligne bonus Metaboliste.
  - Ajustements iteratifs de la camera d'apercu anatomie (face joueur, cadrage portrait, corrections hauteur/zoom/clip) pour eviter le dos, les halos lumineux et le chevauchement avec les barres de vie.
- **Impact**
  - Progression ultra-longue stable en memoire et en sauvegarde, sans saturation XP sur les grands niveaux.
  - Nouvelles boucles de progression actives en deplacement, recolte et craft, avec meilleure lisibilite des bonus.
  - Experience inventaire/anatomie plus claire: informations de charge et etat du personnage visibles en continu.
  - Cadrage d'apercu joueur plus exploitable en UI (moins d'artefacts, meilleure lecture du corps).

### 2026-04-11 - Agent Codex

- **Fichiers modifies**
  - `Client/Monde_Client.cs`
  - `Gestionnaire_Monde.cs`
  - `Cycle_Solaire.cs`
  - `MenuAnatomie.cs`
  - `monde_zero.tscn`
- **Resume**
  - Renforcement du streaming/collisions autour du joueur pour reduire les traversées du sol en mouvement: file urgente de solidification, anticipation de collision devant le joueur, et budget de solidification augmente en priorite joueur.
  - Stabilisation du spawn nouveau monde: chargement du terrain local en priorite, alignement au sol via raycast avant apparition, et verrou anti-chute tant que l'alignement n'est pas valide.
  - Correction de dormance physique des objets poses (dont roches) pour eviter les cas de gel en l'air / flottement proche du joueur.
  - Correction de l'anomalie "boule blanche" dans le ciel en neutralisant une lumiere d'aperçu anatomie qui partageait le `World3D` principal.
  - Ajustements ciel/astre pour conserver le cycle jour/nuit existant avec `Soleil`/`Lune` sans artefact visuel parasite.
- **Impact**
  - Moins de chutes a travers le terrain pendant la marche/vol et meilleure reactivite des collisions proches.
  - Spawn plus fiable sur nouveau monde (plus de spawn sous la map lors du chargement initial).
  - Physique des roches/objets plus coherente pres du joueur.
  - Suppression du second astre blanc parasite, tout en conservant le soleil gameplay existant.

### 2026-04-11 - Agent Codex

- **Fichiers modifies**
  - `Serveur/Chunk_Serveur.cs`
  - `Generateur_Voxel.cs`
  - `TerrainVoxel.gdshader`
  - `Client/Chunk_Client.cs`
  - `Serveur/Monde_Serveur.cs`
- **Resume**
  - Ajustement biome jungle pour rester majoritairement sur `ID 1` (herbe), avec maintien de petites poches d'argile (`ID 8`) proches de l'eau et tres rarement au fond des zones aquatiques.
  - Refonte de la coloration herbe terrain pour textures N/B: meilleure fusion avec le relief de texture et transitions climat plus naturelles (jungle humide -> tempere -> sec jaune).
  - Alignement de la couleur du gazon 3D sur la meme logique biome que le sol `ID 1` pour coherence visuelle entre terrain et brins.
  - Correction du choix d'essence des arbres: les variantes mortes apparaissent uniquement sur sol aride (`ID 6`), plus sur herbe (`ID 1`).
- **Impact**
  - Jungle plus lisible et plus coherente gameplay (gazon conserve car support `ID 1`).
  - Ressource argile conservee mais plus rare et contextualisee (berges/eau), au lieu d'etre dominante.
  - Rendu herbe moins "filtre colle", details de texture mieux preserves.
  - Spawn des arbres morts plus coherent avec le biome aride et suppression des cas visuels incoherents sur prairie.

### 2026-04-10 - Agent Codex

- **Fichiers modifies**
  - `Serveur/Monde_Serveur.cs`
  - `Gestionnaire_Monde.cs`
  - `LSystem_Botanique.cs`
  - `Serveur/Chunk_Serveur.cs`
  - `ArbreVivant.cs`
  - `Atlas_Matiere.cs`
  - `Core/Combat/ToolCombatService.cs`
- **Resume**
  - Correction de persistance des arbres au reload (save/load plus fiable, seeds d'arbres persistees, correction Y pour eviter arbres enterres).
  - Correction eau dynamique: anti ping-pong (va-et-vient) pour stabiliser l'ecoulement.
  - Ajout des arbres morts en zones arides (chene mort/bouleau mort), sans feuilles, coupables a la roche plate.
  - Les branches issues des arbres morts restent craftables, mais avec durabilite bois reduite (x0.5).
- **Impact**
  - Moins de disparition/changement visuel des arbres au rechargement.
  - Moins d'oscillation infinie de l'eau.
  - Nouveau gameplay aride coherent avec ressources bois "mort".

