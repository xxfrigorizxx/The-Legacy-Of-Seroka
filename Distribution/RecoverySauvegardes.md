# Récupération de sauvegarde (SEROKA / Frozen Legacy)

Ce guide s’adresse aux joueurs qui semblent avoir **perdu tout** après une mise à jour du launcher (alpha.37 ou antérieure). Les sauvegardes ne sont **pas** dans le dossier d’installation du jeu ; elles sont gérées par Godot sous Windows.

## Où sont les vraies sauvegardes ?

| Emplacement | Contenu |
|-------------|---------|
| `%APPDATA%\Godot\app_userdata\SEROKA\saves\` | Profil actuel (launcher / build SEROKA) |
| `%APPDATA%\Godot\app_userdata\Zero-K - Frozen Legacy\saves\` | Ancien profil (éditeur ou builds avant renommage) |

Pour ouvrir rapidement : `Win + R`, coller `%APPDATA%\Godot\app_userdata`, Entrée.

Chaque **monde** est un sous-dossier dans `saves\` (ex. `Monde_1234567890\`) avec notamment :

- `player_inventory.dat` — inventaire, corps, faim
- `player_progression.dat` — progression
- `placed_objects.*.dat` — ateliers, coffres, objets posés
- `chunks\` — terrain modifié

## Depuis alpha.38

Au premier lancement, le jeu **fusionne automatiquement** les fichiers manquants ou plus anciens depuis les dossiers legacy vers `SEROKA`. Relancez le launcher une fois, chargez votre monde habituel.

Dans la console Godot (ou `logs\godot.log` sous `SEROKA\logs\`), cherchez :

`ZERO-K : Migration user:// — N fichier(s) récupéré(s) depuis un profil Godot legacy.`

## Si le monde n’apparaît toujours pas

1. **Ne supprimez rien** dans `app_userdata` tant que vous n’avez pas vérifié les deux dossiers ci-dessus.
2. Repérez le dossier de monde qui contient encore des fichiers **non vides** (taille > 0 octets pour `player_inventory.dat` ou des fichiers dans `chunks\`).
3. Si les données sont uniquement sous `Zero-K - Frozen Legacy\saves\MonNomDeMonde\`, copiez manuellement ce dossier vers :
   `%APPDATA%\Godot\app_userdata\SEROKA\saves\MonNomDeMonde\`
4. Relancez le jeu et chargez ce monde depuis le menu.

## Sauvegarde launcher (à partir d’alpha.38)

Avant chaque mise à jour, le launcher copie une sauvegarde complète de `SEROKA` vers :

`%LOCALAPPDATA%\SEROKAFrozenLegacy\backups\userdata_before_update_AAAAMMJJ_HHMMSS\`

En cas de problème après une MAJ, restaurez en copiant le contenu de `...\SEROKA\` depuis ce backup vers `%APPDATA%\Godot\app_userdata\SEROKA\` (jeu fermé).

## Ce que la mise à jour ne doit jamais effacer

Le launcher ne modifie que `%LOCALAPPDATA%\SEROKAFrozenLegacy\game\` (exe, pck, DLL). Il **ne touche pas** à `%APPDATA%\Godot\app_userdata\`.

## Support

Si la récupération échoue, envoyez (sans données personnelles sensibles) :

- Liste des dossiers sous `app_userdata\` (captures ou noms)
- Taille approximative du dossier `saves\MonMonde\` concerné
- Version affichée par le launcher (ex. `0.1.0-alpha.38`)
