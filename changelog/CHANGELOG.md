# Changelog

## 2026-04-13

### Faune bovine sauvage (nouveau dans le monde)

- **Système complet** : scripts `Faune/BoeufSauvage.cs`, `Faune/VacheSauvage.cs` (femelle, même logique que le taureau), `Faune/GestionnaireFauneBoeufs.cs` ; scènes `BoeufSauvage`, `VacheSauvage`, `VeauMaleSauvage`, `VeauFemelleSauvage` ; modèle 3D branché (ex. vache `Modeles/Entites/Boeufs/Quaternius/Cow.gltf`) avec `AnimationTree`, squelette de référence et pipeline d’animations (fusion GLB, scènes par action, `AnimationTree` locomotion ou repli lecture directe).
- **Animations** : `Faune/animation_registry_bovins.json` (classification des noms de clips idle / marche / course / broutage / mort, etc.) et sélection évolutive optionnelle pour varier le comportement visuel.
- **Spawn monde** : apparition de troupeaux selon le terrain (plaine / hors plaine), distance au joueur, densité configurable, option de premier troupeau garanti ; enregistrement des entités pour persistance.
- **Persistance** : sauvegarde / chargement `faune_boeufs.dat` (profil JSON par bête, femelle vs mâle) avec le monde.
- **Simulation** : faim (herbe visible, broutage, pénalités si affamé), stamina (course, attaque, saut, nage), vie et régénération sur le long terme ; **mort** et suppression du cadavre après délai.
- **Progression** : âge, cycles, **expérience et montée de niveau** (dont option « nouveau jour »), bonus de stats ; veaux avec maturation et taille réduite jusqu’à l’âge adulte.
- **Reproduction** : conception journalière / continue, gestation, naissance mâle ou femelle sous forme de veau configurable, cooldowns.
- **Comportement** : errance avec buts, fuite du joueur, broutage, **charge** (notamment taureau en protection des femelles), perception (cône de vision, ligne de vue, ouïe, mémoire courte), soutien de groupe.
- **Environnement** : IA terrain légère (marches, vide, sauts stratégiques / escalade, anti-coinçage, apprentissage de navigation), **natation** dans l’eau avec recherche de sortie.
- **Combat** : dégâts par impact entre entités / sources, plafonds et cooldowns ; entités **tuables** comme le reste des cibles de combat.
- **Évolution** : adaptation comportementale à l’environnement, gènes taille / vitesse et mutations, signal `EvolutionEvenement` pour brancher d’autres systèmes.
- **Debug UI** : barres / textes 3D faim, stamina et vie **désactivés par défaut** ; si l’affichage est coupé, suppression des nœuds `UI_Faim` / `UI_Stamina` / `UI_Vie` résiduels. Tout le gameplay ci-dessus reste actif ; l’inspecteur permet de réactiver l’UI pour le debug.

### Armes, craft et XP joueur

- Ajout de la `Lance en pierre` (ID 111) comme arme complète : propriétés d’objet, durabilité, rendu en main/preview, instanciation modèle, physique au sol/lancer, ramassage et compatibilité inventaire.
- Activation du craft atelier 3x3 de la lance avec patron demandé et version miroir :
  - `(X) (C) (R)`
  - `(X) (B) (C)`
  - `(B) (X) (X)`
- Configuration gameplay de la lance pour le combat uniquement : dégâts en mêlée/lancer activés, usage de creusage du sol bloqué.
- Ajout du gain d’XP `Force +2` quand des dégâts sont effectivement infligés à une entité via l’impact combat validé.

### Interface joueur (inventaire Q)

- **Faim et énergie** : plus d’affichage permanent sur l’écran de base (HUD bas) ; les barres et libellés sont dans le menu **inventaire (Q)**, colonne « Anatomie / Points de vie », **sous** la liste des barres de PV des membres (`MenuAnatomie.cs` : zone `BoiteFaimEnergieExterne` + `AttacherHudFaimEnergieJoueur`). Le métabolisme en jeu reste inchangé (`Joueur.cs`).
- Libellé **« Endurance »** remplacé par **« Énergie »** dans cette UI.
- Si la scène `MenuAnatomie` n’est pas chargée, repli sur l’ancien ancrage HUD (comportement dégradé).
