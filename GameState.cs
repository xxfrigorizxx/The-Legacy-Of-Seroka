using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>Race du personnage joueur (persistée par monde).</summary>
public enum RaceJoueur
{
	Humain = 0,
	Orc = 1
}

/// <summary>État global du jeu. Autoload pour passer monde/seed entre menu et jeu.</summary>
public partial class GameState : Node
{
	/// <summary>Instance statique pour accès fiable (Engine.HasSingleton peu fiable avec autoloads C#).</summary>
	public static GameState Instance { get; private set; }

	/// <summary>Nom du monde actuel (dossier dans user://saves/). TOUJOURS utilisé pour chunks.</summary>
	public string NomMondeActuel { get; private set; } = "MonMonde";

	/// <summary>Jour absolu du monde (incrémenté à minuit). Persisté dans world_time.dat.</summary>
	public int JourAbsolu { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		// Godot 4 : SceneTree n’expose pas le signal « tree_exiting » (Godot 3). Fermeture via fenêtre racine + notification WM.
		Window fenetre = GetWindow();
		if (fenetre != null)
			fenetre.CloseRequested += ExecuterSauvegardeFiletAvantFermetureApplication;
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		// Croix Windows / demande de fermeture (souvent avant la destruction de la scène de jeu).
		if (!Engine.IsEditorHint() && what == Node.NotificationWMCloseRequest)
			ExecuterSauvegardeFiletAvantFermetureApplication();
	}

	/// <summary>Filet si fermeture sans passer par le bouton Sauvegarder (croix fenêtre, etc.). <c>GetTree().Quit()</c> est déjà couvert par les boutons Quitter.</summary>
	private void ExecuterSauvegardeFiletAvantFermetureApplication()
	{
		if (Engine.IsEditorHint()) return;
		EssayerSauvegardeCompleteSiEnPartie();
	}

	/// <summary>
	/// La scène <c>monde_zero.tscn</c> a pour racine un nœud (ex. <c>Monde_Zero</c>) : le <see cref="Gestionnaire_Monde"/> est enfant, pas la racine <see cref="SceneTree.CurrentScene"/>.
	/// </summary>
	private static Gestionnaire_Monde ObtenirGestionnaireMondeDepuisSceneCourante(SceneTree tree)
	{
		Node scene = tree?.CurrentScene;
		if (scene == null) return null;
		if (scene is Gestionnaire_Monde g) return g;
		Gestionnaire_Monde enfant = scene.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
		if (enfant != null) return enfant;
		return scene.FindChild("Gestionnaire_Monde", recursive: true, owned: false) as Gestionnaire_Monde;
	}

	/// <summary>
	/// Sauvegarde joueur + objets + chunks (même logique que le bouton Sauvegarder) si la scène courante est le monde de jeu.
	/// Ne fait rien depuis le menu principal ou une autre scène.
	/// </summary>
	public static void EssayerSauvegardeCompleteSiEnPartie()
	{
		if (Engine.IsEditorHint()) return;
		SceneTree tree = Instance?.GetTree() ?? Engine.GetMainLoop() as SceneTree;
		ObtenirGestionnaireMondeDepuisSceneCourante(tree)?.SauvegarderManuelDepuisMenu();
	}

	/// <summary>Lit <c>user://last_played_world.txt</c> (nom de dossier sous <c>user://saves/</c>).</summary>
	public bool EssayerLireDernierMondeJoueSurDisque(out string nomMonde)
	{
		nomMonde = null;
		string p = ProjectSettings.GlobalizePath(FichierDernierMondeJoue);
		if (!File.Exists(p)) return false;
		try
		{
			nomMonde = File.ReadAllText(p).Trim();
			return !string.IsNullOrEmpty(nomMonde);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Lecture dernier monde joué : {ex.Message}");
			return false;
		}
	}

	/// <summary>Réécrit le fichier « dernier monde » pour F5 / reprise et éviter tout décalage avec <see cref="NomMondeActuel"/>.</summary>
	public void PublierMondeActuelCommeDernierJoueSurDisque()
	{
		if (string.IsNullOrWhiteSpace(NomMondeActuel)) return;
		EcrireDernierMondeJoue(NomMondeActuel);
	}

	/// <summary>Seed du terrain pour le monde actuel.</summary>
	public int SeedTerrainActuel { get; private set; } = 19847;

	/// <summary>Nom d’affichage du personnage pour ce monde.</summary>
	public string NomPersonnageJoue { get; private set; } = "";

	/// <summary>Race du personnage pour ce monde.</summary>
	public RaceJoueur RaceJoueurCourante { get; private set; } = RaceJoueur.Humain;

	private const int VersionFichierIdentiteJoueur = 1;
	private const string NomFichierIdentiteJoueur = "player_identity.dat";

	/// <summary>Brouillon assistant : étape monde validée, pas encore d’écriture disque.</summary>
	public bool CreationMondeBrouillonActif { get; private set; }

	/// <summary>Nom de dossier monde prêt après étape 1 (vide si pas de brouillon).</summary>
	public string NomMondeNouveauPret { get; private set; } = "";

	/// <summary>Seed terrain après étape 1.</summary>
	public int SeedTerrainNouveauPret { get; private set; }

	/// <summary>Valide nom + seed sans créer de fichiers (étape 1 de l’assistant).</summary>
	public bool EssayerValiderEtapeMondeNouveau(string nomBrut, string seedTexteBrut, out string erreur)
	{
		erreur = null;
		int seed = ResoudreSeedDepuisTexte(seedTexteBrut);
		string nom = NettoyerNomMonde(nomBrut);
		if (string.IsNullOrEmpty(nom))
			nom = $"Monde_{seed}";

		if (MondeExisteDejaSurDisque(nom))
		{
			erreur = "Un monde avec ce nom existe déjà.";
			return false;
		}

		CreationMondeBrouillonActif = true;
		NomMondeNouveauPret = nom;
		SeedTerrainNouveauPret = seed;
		return true;
	}

	/// <summary>Annule le brouillon (Retour depuis l’assistant ou abandon).</summary>
	public void AnnulerCreationMondeBrouillon()
	{
		CreationMondeBrouillonActif = false;
		NomMondeNouveauPret = "";
		SeedTerrainNouveauPret = 0;
	}

	/// <summary>Écrit le monde après l’étape personnage (nécessite un brouillon actif).</summary>
	public bool EssayerFinaliserNouveauMondeAvecPersonnage(string nomPersonnageBrut, RaceJoueur race, out string erreur)
	{
		erreur = null;
		if (!CreationMondeBrouillonActif || string.IsNullOrWhiteSpace(NomMondeNouveauPret))
		{
			erreur = "Aucune création de monde en cours.";
			return false;
		}

		string nom = NomMondeNouveauPret;
		int seed = SeedTerrainNouveauPret;
		if (MondeExisteDejaSurDisque(nom))
		{
			AnnulerCreationMondeBrouillon();
			erreur = "Un monde avec ce nom existe déjà.";
			return false;
		}

		if (!ExecuterEcritureNouveauMondeSurDisque(nom, seed, nomPersonnageBrut, race, out erreur))
			return false;

		AnnulerCreationMondeBrouillon();
		return true;
	}

	/// <summary>
	/// Crée un nouveau monde en une fois (API directe). Pour l’assistant, préférer <see cref="EssayerValiderEtapeMondeNouveau"/> puis <see cref="EssayerFinaliserNouveauMondeAvecPersonnage"/>.
	/// </summary>
	public bool EssayerCreerNouveauMonde(string nomBrut, string seedTexteBrut, string nomPersonnageBrut, RaceJoueur race, out string erreur)
	{
		erreur = null;
		int seed = ResoudreSeedDepuisTexte(seedTexteBrut);
		string nom = NettoyerNomMonde(nomBrut);
		if (string.IsNullOrEmpty(nom))
			nom = $"Monde_{seed}";

		if (MondeExisteDejaSurDisque(nom))
		{
			erreur = "Un monde avec ce nom existe déjà.";
			return false;
		}

		return ExecuterEcritureNouveauMondeSurDisque(nom, seed, nomPersonnageBrut, race, out erreur);
	}

	private bool ExecuterEcritureNouveauMondeSurDisque(string nom, int seed, string nomPersonnageBrut, RaceJoueur race, out string erreur)
	{
		erreur = null;
		try
		{
			NomMondeActuel = nom;
			SeedTerrainActuel = seed;
			string dossier = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks");
			Directory.CreateDirectory(dossier);
			SauvegarderMetadataMonde(nom, seed);
			string nomPerso = NettoyerNomPersonnage(nomPersonnageBrut);
			NomPersonnageJoue = nomPerso;
			RaceJoueurCourante = race;
			SauvegarderIdentiteJoueurSurDisque(nom, nomPerso, race);
			EcrireDernierMondeJoue(nom);
			GD.Print($"ZERO-K : Nouveau monde créé : {nom} (seed {seed}, perso « {nomPerso} », {race})");
			return true;
		}
		catch (Exception ex)
		{
			erreur = $"Impossible de créer le monde : {ex.Message}";
			GD.PrintErr($"ZERO-K : {erreur}");
			return false;
		}
	}

	/// <summary>Nom affichable ; vide → « Voyageur ». Pas un nom de dossier.</summary>
	public static string NettoyerNomPersonnage(string brut)
	{
		if (string.IsNullOrWhiteSpace(brut))
			return "Voyageur";
		var sb = new StringBuilder(Math.Min(brut.Trim().Length, 64));
		int n = 0;
		foreach (char c in brut.Trim())
		{
			if (n >= 64) break;
			if (char.IsControl(c)) continue;
			sb.Append(c);
			n++;
		}
		string s = sb.ToString().Trim();
		return string.IsNullOrEmpty(s) ? "Voyageur" : s;
	}

	private static void SauvegarderIdentiteJoueurSurDisque(string nomMonde, string nomPersonnage, RaceJoueur race)
	{
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, NomFichierIdentiteJoueur);
		using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
		w.Write(VersionFichierIdentiteJoueur);
		w.Write(nomPersonnage ?? "Voyageur");
		w.Write((byte)race);
	}

	private static bool EssayerLireIdentiteJoueurDepuisDisque(string nomMonde, out string nomPersonnage, out RaceJoueur race)
	{
		nomPersonnage = "Voyageur";
		race = RaceJoueur.Humain;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{nomMonde}"), NomFichierIdentiteJoueur);
		if (!File.Exists(chemin))
			return false;
		try
		{
			using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			int version = r.ReadInt32();
			if (version < 1 || version > VersionFichierIdentiteJoueur)
				return false;
			nomPersonnage = NettoyerNomPersonnage(r.ReadString());
			byte b = r.ReadByte();
			race = b == (byte)RaceJoueur.Orc ? RaceJoueur.Orc : RaceJoueur.Humain;
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Lecture identité joueur : {ex.Message}");
			return false;
		}
	}

	private static bool MondeExisteDejaSurDisque(string nom)
	{
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nom}");
		if (!Directory.Exists(dossier))
			return false;
		return File.Exists(Path.Combine(dossier, "world_meta.dat"))
			|| Directory.Exists(Path.Combine(dossier, "chunks"));
	}

	/// <summary>Retire les caractères interdits pour un nom de dossier. Null si rien d’utilisable.</summary>
	private static string NettoyerNomMonde(string brut)
	{
		if (string.IsNullOrWhiteSpace(brut))
			return null;
		var sb = new StringBuilder(brut.Trim().Length);
		foreach (char c in brut.Trim())
		{
			if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
				sb.Append('_');
			else
				sb.Append(c);
		}
		string s = sb.ToString().Trim().TrimEnd('.', ' ');
		if (string.IsNullOrWhiteSpace(s) || s == "." || s == "..")
			return null;
		return s;
	}

	private static int GenererSeedAleatoire()
	{
		int seed = (int)(DateTime.UtcNow.Ticks % 2147483647);
		if (seed < 0) seed = -seed;
		if (seed == 0) seed = 19847;
		return seed;
	}

	/// <summary>Vide → aléatoire ; entier → tel quel (0 remplacé par aléatoire) ; autre texte → hash stable.</summary>
	private static int ResoudreSeedDepuisTexte(string seedTexteBrut)
	{
		if (string.IsNullOrWhiteSpace(seedTexteBrut))
			return GenererSeedAleatoire();
		string t = seedTexteBrut.Trim();
		if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
			return n == 0 ? GenererSeedAleatoire() : n;
		return HasherTexteEnSeedPositif(t);
	}

	private static int HasherTexteEnSeedPositif(string s)
	{
		unchecked
		{
			uint h = 2166136261u;
			foreach (char c in s)
			{
				h ^= c;
				h *= 16777619u;
			}
			int v = (int)(h & 0x7FFFFFFF);
			return v == 0 ? 1337 : v;
		}
	}

	/// <summary>Charge un monde existant par son nom. Retourne true si trouvé. Rétrocompatibilité : MonMonde sans world_meta → seed 19847.</summary>
	public bool ChargerMonde(string nomMonde)
	{
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
		string cheminMeta = Path.Combine(dossier, "world_meta.dat");
		int seed = 19847;
		if (File.Exists(cheminMeta))
		{
			try
			{
				using var reader = new BinaryReader(File.Open(cheminMeta, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
				seed = reader.ReadInt32();
			}
			catch (Exception ex)
			{
				GD.PrintErr($"ZERO-K : Erreur lecture metadata : {ex.Message}");
			}
		}
		else if (!Directory.Exists(dossier))
		{
			GD.PrintErr($"ZERO-K : Monde '{nomMonde}' introuvable (dossier absent).");
			return false;
		}
		NomMondeActuel = nomMonde;
		SeedTerrainActuel = seed;
		if (EssayerLireIdentiteJoueurDepuisDisque(nomMonde, out string nomP, out RaceJoueur rc))
		{
			NomPersonnageJoue = nomP;
			RaceJoueurCourante = rc;
		}
		else
		{
			NomPersonnageJoue = "Voyageur";
			RaceJoueurCourante = RaceJoueur.Humain;
		}
		ChargerJourAbsolu(nomMonde);
		EcrireDernierMondeJoue(nomMonde);
		GD.Print($"ZERO-K : Monde chargé : {nomMonde} (seed {seed}, jour {JourAbsolu}, perso « {NomPersonnageJoue} », {RaceJoueurCourante})");
		return true;
	}

	private const string FichierDernierMondeJoue = "user://last_played_world.txt";

	private static void EcrireDernierMondeJoue(string nom)
	{
		if (string.IsNullOrWhiteSpace(nom)) return;
		try
		{
			string p = ProjectSettings.GlobalizePath(FichierDernierMondeJoue);
			File.WriteAllText(p, nom.Trim());
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Écriture dernier monde joué : {ex.Message}");
		}
	}

	/// <summary>
	/// Si <c>monde_zero</c> est lancé sans menu (F5, raccourci) alors que <see cref="NomMondeActuel"/> vaut encore <c>MonMonde</c>,
	/// réutilise le dernier monde enregistré (objets posés, chunks, inventaire) pour éviter d’écrire dans le mauvais dossier.
	/// </summary>
	public void AppliquerDernierMondeJoueSiChargementDirectVersMondeZero()
	{
		if (!string.Equals(NomMondeActuel, "MonMonde", StringComparison.Ordinal))
			return;
		if (!EssayerLireDernierMondeJoueSurDisque(out string nomFichier))
			return;
		if (nomFichier == NomMondeActuel)
			return;
		string dossierCible = ProjectSettings.GlobalizePath($"user://saves/{nomFichier}");
		if (!Directory.Exists(dossierCible))
			return;
		bool cibleValide = File.Exists(Path.Combine(dossierCible, "world_meta.dat"))
			|| Directory.Exists(Path.Combine(dossierCible, "chunks"));
		if (!cibleValide)
			return;
		if (!ChargerMonde(nomFichier))
			GD.PrintErr($"ZERO-K : Impossible de charger le dernier monde joué ({nomFichier}).");
	}

	/// <summary>Liste les noms des mondes sauvegardés. Inclut les dossiers avec chunks/ (rétrocompatibilité MonMonde).</summary>
	public List<string> ObtenirListeMondes()
	{
		var liste = new List<string>();
		string basePath = ProjectSettings.GlobalizePath("user://saves");
		if (!Directory.Exists(basePath)) return liste;
		foreach (string dir in Directory.GetDirectories(basePath))
		{
			string nom = Path.GetFileName(dir);
			if (File.Exists(Path.Combine(dir, "world_meta.dat")) || Directory.Exists(Path.Combine(dir, "chunks")))
				liste.Add(nom);
		}
		return liste;
	}

	private void SauvegarderMetadataMonde(string nom, int seed)
	{
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nom}");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, "world_meta.dat");
		using var writer = new BinaryWriter(File.Open(chemin, FileMode.Create));
		writer.Write(seed);
	}

	private void SauvegarderJourAbsolu(string nom)
	{
		try
		{
			string chemin = Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{nom}"), "world_time.dat");
			using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
			w.Write(JourAbsolu);
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde jour : {ex.Message}"); }
	}

	private void ChargerJourAbsolu(string nom)
	{
		string chemin = Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{nom}"), "world_time.dat");
		if (!File.Exists(chemin)) { JourAbsolu = 0; return; }
		try
		{
			using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			JourAbsolu = Mathf.Max(0, r.ReadInt32());
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur lecture jour : {ex.Message}"); JourAbsolu = 0; }
	}

	/// <summary>Incmente le jour absolu (appelé à minuit). Sauvegarde immédiate.</summary>
	public void IncrementerJourAbsolu()
	{
		JourAbsolu++;
		if (!string.IsNullOrEmpty(NomMondeActuel))
			SauvegarderJourAbsolu(NomMondeActuel);
	}

	/// <summary>Sauvegarde la position du joueur pour ce monde. Appelé à la déconnexion / quitter.</summary>
	public void SauvegarderPositionJoueur(Vector3 pos)
	{
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{NomMondeActuel}");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, "player.dat");
		try
		{
			using var writer = new BinaryWriter(File.Open(chemin, FileMode.Create));
			writer.Write(pos.X);
			writer.Write(pos.Y);
			writer.Write(pos.Z);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur sauvegarde position joueur : {ex.Message}");
		}
	}

	/// <summary>Charge la position du joueur sauvegardée. Null si aucun fichier (nouveau monde).</summary>
	public Vector3? ObtenirPositionJoueurSauvegardee()
	{
		string chemin = Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{NomMondeActuel}"), "player.dat");
		if (!File.Exists(chemin)) return null;
		try
		{
			using var reader = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			float x = reader.ReadSingle();
			float y = reader.ReadSingle();
			float z = reader.ReadSingle();
			return new Vector3(x, y, z);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur lecture position joueur : {ex.Message}");
			return null;
		}
	}
}
