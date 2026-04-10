# Changelog

## 2026-04-10

### Ajouts gameplay et contenu
- Ajout du rack a batons (`Rack_Batons_Tier0`) avec son modele et son integration en jeu.
- Ajout et integration de variantes d'equipement textile: sacs, ceintures et pochettes.
- Ajout des animations locomotion du personnage (idle, marche, saut) et de leurs ressources.

### Systeme joueur (FPS/TPS)
- Ajustements du controleur joueur (`Joueur`) pour la camera FPS/TPS, le cadrage visage et l'orientation du rig.
- Stabilisation de l'affichage et de la rotation des objets tenus en main (incluant les armes comme la hachette).
- Amelioration de la coherence entre rendu local, animations et interactions de combat/craft.

### Scenes et integration
- Mise a jour des scenes et ressources (`Joueur.tscn`, `monde_zero.tscn`, `project.godot`) pour aligner les nouveaux objets et comportements.
- Synchronisation des services client/serveur touches par les nouveaux flux d'inventaire, rendu et interaction monde.
