using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

/// <summary>Race du personnage joueur (persistée par monde).</summary>
public enum RaceJoueur
{
	Humain = 0,
	Orc = 1
}

/// <summary>Sexe du personnage (mesh uniquement ; la race garde les règles gameplay).</summary>
public enum SexeJoueur
{
	Masculin = 0,
	Feminin = 1
}

/// <summary>État global du jeu. Autoload pour passer monde/seed entre menu et jeu.</summary>
public partial class GameState : Node
{
	/// <summary>Instance statique pour accès fiable (Engine.HasSingleton peu fiable avec autoloads C#).</summary>
	public static GameState Instance { get; private set; }
	private static bool _empreinteRuntimeJournalisee;

	// --- Chien de garde anti-gel (thread de fond) ---
	// Détecte un blocage du thread principal (boucle infinie / gel) et l'écrit dans user://logs/seroka_watchdog.log,
	// MÊME quand le jeu est totalement figé. Un crash natif (GPU/Jolt) tue tout le process → aucune ligne "GEL" = crash natif.
	private System.Threading.Thread _threadChienDeGarde;
	private volatile bool _chienDeGardeActif;
	private long _horodatageBattementMs;
	private volatile string _phasePrincipaleCourante = "demarrage";
	private int _gelDejaSignale;
	private string _cheminLogChienDeGarde;
	private readonly object _verrouLogChienDeGarde = new object();
	private static readonly System.Diagnostics.Stopwatch _chronoChienDeGarde = System.Diagnostics.Stopwatch.StartNew();
	private const long SeuilGelChienDeGardeMs = 5000;

	/// <summary>Marque la phase courante du thread principal (affichée dans le log si un gel est détecté). Coût négligeable.</summary>
	public static void MarquerPhasePrincipale(string phase)
	{
		if (Instance != null)
			Instance._phasePrincipaleCourante = phase ?? "";
	}

	/// <summary>Nom du monde actuel (dossier dans user://saves/). TOUJOURS utilisé pour chunks.</summary>
	public string NomMondeActuel { get; private set; } = "MonMonde";

	/// <summary>Jour absolu du monde (incrémenté à minuit). Persisté dans world_time.dat.</summary>
	public int JourAbsolu { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		DemarrerChienDeGardeAntiGel();
		UserDataMigrationService.ExecuterMigrationAuDemarrageSiBesoin();
		JournaliserEmpreinteRuntime();
		// Godot 4 : SceneTree n’expose pas le signal « tree_exiting » (Godot 3). Fermeture via fenêtre racine + notification WM.
		Window fenetre = GetWindow();
		if (fenetre != null)
			fenetre.CloseRequested += ExecuterSauvegardeFiletAvantFermetureApplication;
	}

	public override void _Process(double delta)
	{
		// Base par frame : si un gel survient hors d'une phase marquée, le log affichera "frame_normale"
		// (=> chercher côté boucle/streaming) plutôt qu'un marqueur périmé d'une opération roche.
		_phasePrincipaleCourante = "frame_normale";
		// Battement de cœur pour le chien de garde anti-gel (écriture atomique, coût négligeable).
		System.Threading.Interlocked.Exchange(ref _horodatageBattementMs, _chronoChienDeGarde.ElapsedMilliseconds);
	}

	public override void _ExitTree()
	{
		ArreterChienDeGarde();
		base._ExitTree();
	}

	private void DemarrerChienDeGardeAntiGel()
	{
		if (Engine.IsEditorHint())
			return;
		try
		{
			_cheminLogChienDeGarde = ProjectSettings.GlobalizePath("user://logs/seroka_watchdog.log");
			string dossier = Path.GetDirectoryName(_cheminLogChienDeGarde);
			if (!string.IsNullOrEmpty(dossier))
				Directory.CreateDirectory(dossier);
			System.Threading.Interlocked.Exchange(ref _horodatageBattementMs, _chronoChienDeGarde.ElapsedMilliseconds);
			EcrireLigneChienDeGarde($"=== Session demarree {DateTime.Now:yyyy-MM-dd HH:mm:ss} (dll={Assembly.GetExecutingAssembly().GetName().Version}) ===");
		}
		catch { /* le chien de garde ne doit JAMAIS casser le jeu */ }

		_chienDeGardeActif = true;
		_threadChienDeGarde = new System.Threading.Thread(BoucleChienDeGarde)
		{
			IsBackground = true,
			Name = "SerokaChienDeGarde"
		};
		_threadChienDeGarde.Start();
	}

	/// <summary>Thread de fond : surveille le battement de cœur du thread principal et journalise tout gel ≥ seuil.</summary>
	private void BoucleChienDeGarde()
	{
		while (_chienDeGardeActif)
		{
			System.Threading.Thread.Sleep(1000);
			long dernier = System.Threading.Interlocked.Read(ref _horodatageBattementMs);
			long ecart = _chronoChienDeGarde.ElapsedMilliseconds - dernier;
			if (ecart >= SeuilGelChienDeGardeMs)
			{
				if (System.Threading.Interlocked.Exchange(ref _gelDejaSignale, 1) == 0)
					EcrireLigneChienDeGarde(
						$"[{DateTime.Now:HH:mm:ss}] GEL DETECTE : thread principal bloque depuis {ecart} ms " +
						$"(derniere phase='{_phasePrincipaleCourante}'). Boucle infinie / gel cote code. " +
						"Si AUCUNE autre ligne ensuite et que le jeu s'est ferme => crash natif (GPU/SDFGI/physique).");
			}
			else
			{
				System.Threading.Interlocked.Exchange(ref _gelDejaSignale, 0);
			}
		}
	}

	private void EcrireLigneChienDeGarde(string ligne)
	{
		if (string.IsNullOrEmpty(_cheminLogChienDeGarde))
			return;
		try
		{
			lock (_verrouLogChienDeGarde)
				File.AppendAllText(_cheminLogChienDeGarde, ligne + System.Environment.NewLine);
		}
		catch { /* ignore : la journalisation ne doit jamais planter le jeu */ }
	}

	private void ArreterChienDeGarde()
	{
		_chienDeGardeActif = false;
		try { _threadChienDeGarde?.Join(200); } catch { }
		_threadChienDeGarde = null;
	}

	/// <summary>
	/// Trace une empreinte runtime (mode moteur, chemins, hash DLL/PCK, version manifest install) pour diagnostiquer
	/// immédiatement les désynchronisations entre Play éditeur et lancement via launcher.
	/// </summary>
	private void JournaliserEmpreinteRuntime()
	{
		if (_empreinteRuntimeJournalisee)
			return;
		_empreinteRuntimeJournalisee = true;

		string userDir = ProjectSettings.GlobalizePath("user://");
		string exePath = OS.GetExecutablePath();
		string baseDir = AppContext.BaseDirectory;
		string exeDir = string.IsNullOrWhiteSpace(exePath) ? baseDir : Path.GetDirectoryName(exePath) ?? baseDir;
		string assemblyPath = Assembly.GetExecutingAssembly().Location;
		if (string.IsNullOrWhiteSpace(assemblyPath))
		{
			string dllCandidate = Path.Combine(baseDir, "Zero-K - Frozen Legacy.dll");
			if (File.Exists(dllCandidate))
				assemblyPath = dllCandidate;
		}
		string pckPath = Path.Combine(exeDir, "SEROKAFrozenLegacy.pck");
		string manifestLocalPath = Path.GetFullPath(Path.Combine(baseDir, "..", "manifests", "local-manifest.json"));
		string manifestVersion = LireVersionManifestLocal(manifestLocalPath);

		string hashDll = CalculerSha256CourtSiFichierExistant(assemblyPath);
		string hashPck = CalculerSha256CourtSiFichierExistant(pckPath);

		GD.Print(
			$"SEROKA_RUNTIME_FINGERPRINT | " +
			$"editor={Engine.IsEditorHint()} debug={OS.IsDebugBuild()} " +
			$"userDir=\"{userDir}\" baseDir=\"{baseDir}\" exe=\"{exePath}\" " +
			$"dll=\"{assemblyPath}\" dllSha256={hashDll} pckSha256={hashPck} " +
			$"manifestVersion={manifestVersion}");
	}

	private static string CalculerSha256CourtSiFichierExistant(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			return "absent";
		try
		{
			using FileStream flux = File.OpenRead(path);
			using SHA256 sha = SHA256.Create();
			byte[] hash = sha.ComputeHash(flux);
			string hex = Convert.ToHexString(hash).ToLowerInvariant();
			return hex.Length > 12 ? hex[..12] : hex;
		}
		catch (Exception ex)
		{
			return $"erreur:{ex.GetType().Name}";
		}
	}

	private static string LireVersionManifestLocal(string manifestPath)
	{
		if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
			return "absent";
		try
		{
			string json = File.ReadAllText(manifestPath);
			const string cle = "\"version\"";
			int idxCle = json.IndexOf(cle, StringComparison.OrdinalIgnoreCase);
			if (idxCle < 0)
				return "invalide";
			int idxDeuxPoints = json.IndexOf(':', idxCle + cle.Length);
			if (idxDeuxPoints < 0)
				return "invalide";
			int idxGuillemetDebut = json.IndexOf('"', idxDeuxPoints + 1);
			if (idxGuillemetDebut < 0)
				return "invalide";
			int idxGuillemetFin = json.IndexOf('"', idxGuillemetDebut + 1);
			if (idxGuillemetFin < 0)
				return "invalide";
			string version = json.Substring(idxGuillemetDebut + 1, idxGuillemetFin - idxGuillemetDebut - 1).Trim();
			return string.IsNullOrWhiteSpace(version) ? "invalide" : version;
		}
		catch
		{
			return "invalide";
		}
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		// Croix Windows / demande de fermeture (souvent avant la destruction de la scène de jeu).
		if (!Engine.IsEditorHint() && what == Node.NotificationWMCloseRequest)
			ExecuterSauvegardeFiletAvantFermetureApplication();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint())
			return;
		Window fenetre = GetWindow();
		if (fenetre == null)
			return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo
			&& (key.Keycode == Key.F11 || key.PhysicalKeycode == Key.F11))
		{
			if (fenetre.Mode == Window.ModeEnum.Fullscreen || fenetre.Mode == Window.ModeEnum.ExclusiveFullscreen)
				fenetre.Mode = Window.ModeEnum.Windowed;
			else
				fenetre.Mode = Window.ModeEnum.Fullscreen;
			GetViewport()?.SetInputAsHandled();
		}
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

	/// <summary>Sexe du personnage pour ce monde (persisté avec l’identité).</summary>
	public SexeJoueur SexeJoueurCourante { get; private set; } = SexeJoueur.Masculin;

	/// <summary>Après mort : le menu affiche uniquement l’étape personnage pour le même <see cref="NomMondeActuel"/> (carte inchangée).</summary>
	public bool RecreationPersonnageMemeMondeEnAttente { get; private set; }

	private const int VersionFichierIdentiteJoueur = 2;
	private const int VersionFichierIdentiteJoueurMinLue = 1;
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
	public bool EssayerFinaliserNouveauMondeAvecPersonnage(string nomPersonnageBrut, RaceJoueur race, SexeJoueur sexe, out string erreur)
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

		if (!ExecuterEcritureNouveauMondeSurDisque(nom, seed, nomPersonnageBrut, race, sexe, out erreur))
			return false;

		AnnulerCreationMondeBrouillon();
		return true;
	}

	/// <summary>
	/// Crée un nouveau monde en une fois (API directe). Pour l’assistant, préférer <see cref="EssayerValiderEtapeMondeNouveau"/> puis <see cref="EssayerFinaliserNouveauMondeAvecPersonnage"/>.
	/// </summary>
	public bool EssayerCreerNouveauMonde(string nomBrut, string seedTexteBrut, string nomPersonnageBrut, RaceJoueur race, SexeJoueur sexe, out string erreur)
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

		return ExecuterEcritureNouveauMondeSurDisque(nom, seed, nomPersonnageBrut, race, sexe, out erreur);
	}

	private bool ExecuterEcritureNouveauMondeSurDisque(string nom, int seed, string nomPersonnageBrut, RaceJoueur race, SexeJoueur sexe, out string erreur)
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
			SexeJoueurCourante = sexe;
			SauvegarderIdentiteJoueurSurDisque(nom, nomPerso, race, sexe);
			EcrireDernierMondeJoue(nom);
			GD.Print($"ZERO-K : Nouveau monde créé : {nom} (seed {seed}, perso « {nomPerso} », {race}, {sexe})");
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

	private static void SauvegarderIdentiteJoueurSurDisque(string nomMonde, string nomPersonnage, RaceJoueur race, SexeJoueur sexe)
	{
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, NomFichierIdentiteJoueur);
		using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
		w.Write(VersionFichierIdentiteJoueur);
		w.Write(nomPersonnage ?? "Voyageur");
		w.Write((byte)race);
		w.Write((byte)sexe);
	}

	private static bool EssayerLireIdentiteJoueurDepuisDisque(string nomMonde, out string nomPersonnage, out RaceJoueur race, out SexeJoueur sexe)
	{
		nomPersonnage = "Voyageur";
		race = RaceJoueur.Humain;
		sexe = SexeJoueur.Masculin;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{nomMonde}"), NomFichierIdentiteJoueur);
		if (!File.Exists(chemin))
			return false;
		try
		{
			using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			int version = r.ReadInt32();
			if (version < VersionFichierIdentiteJoueurMinLue || version > VersionFichierIdentiteJoueur)
				return false;
			nomPersonnage = NettoyerNomPersonnage(r.ReadString());
			byte b = r.ReadByte();
			race = b == (byte)RaceJoueur.Orc ? RaceJoueur.Orc : RaceJoueur.Humain;
			if (version >= 2)
			{
				byte bs = r.ReadByte();
				sexe = bs == (byte)SexeJoueur.Feminin ? SexeJoueur.Feminin : SexeJoueur.Masculin;
			}
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
		if (EssayerLireIdentiteJoueurDepuisDisque(nomMonde, out string nomP, out RaceJoueur rc, out SexeJoueur sx))
		{
			NomPersonnageJoue = nomP;
			RaceJoueurCourante = rc;
			SexeJoueurCourante = sx;
		}
		else
		{
			NomPersonnageJoue = "Voyageur";
			RaceJoueurCourante = RaceJoueur.Humain;
			SexeJoueurCourante = SexeJoueur.Masculin;
		}
		ChargerJourAbsolu(nomMonde);
		EcrireDernierMondeJoue(nomMonde);
		GD.Print($"ZERO-K : Monde chargé : {nomMonde} (seed {seed}, jour {JourAbsolu}, perso « {NomPersonnageJoue} », {RaceJoueurCourante}, {SexeJoueurCourante})");
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

	private const string FichierDernierePoseMort = "player_last_pose.dat";

	/// <summary>Mémorise la position exacte au décès (non effacée avec la progression) pour réapparition au même endroit.</summary>
	public void SauvegarderDernierePoseMort(int dimensionId, Vector3 position)
	{
		if (string.IsNullOrWhiteSpace(NomMondeActuel))
			return;
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{NomMondeActuel}");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, FichierDernierePoseMort);
		try
		{
			using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
			w.Write(1);
			w.Write(dimensionId);
			w.Write(position.X);
			w.Write(position.Y);
			w.Write(position.Z);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur sauvegarde pose mort : {ex.Message}");
		}
	}

	/// <summary>Pose au moment de la dernière mort (même monde). Null si absente.</summary>
	public bool EssayerChargerDernierePoseMort(out int dimensionId, out Vector3 position)
	{
		dimensionId = (int)DimensionJeu.Alpha;
		position = Vector3.Zero;
		if (string.IsNullOrWhiteSpace(NomMondeActuel))
			return false;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{NomMondeActuel}"), FichierDernierePoseMort);
		if (!File.Exists(chemin))
			return false;
		try
		{
			using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			if (r.ReadInt32() != 1)
				return false;
			dimensionId = r.ReadInt32();
			position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur lecture pose mort : {ex.Message}");
			return false;
		}
	}

	/// <summary>Efface la progression perso après mort (carte / chunks inchangés). L’UI de recréation se fait en jeu.</summary>
	public void PreparerMortNouveauPersonnageMemeMonde()
	{
		if (string.IsNullOrWhiteSpace(NomMondeActuel))
			return;
		EffacerDonneesPersonnageMondeActuel();
		NomPersonnageJoue = "";
		RaceJoueurCourante = RaceJoueur.Humain;
		SexeJoueurCourante = SexeJoueur.Masculin;
		GD.Print($"ZERO-K : Mort — recréez un personnage pour le monde « {NomMondeActuel} » (carte conservée).");
	}

	public void AnnulerRecreationPersonnageMemeMondeEnAttente()
	{
		RecreationPersonnageMemeMondeEnAttente = false;
	}

	/// <summary>Supprime uniquement les fichiers de progression / inventaire / identité joueur (pas <c>chunks/</c>, <c>world_*</c>, objets posés, drops).</summary>
	public bool EffacerDonneesPersonnageMondeActuel()
	{
		if (string.IsNullOrWhiteSpace(NomMondeActuel)) return false;
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{NomMondeActuel}");
		if (!Directory.Exists(dossier)) return false;
		string[] fichiers =
		{
			"player_progression.dat",
			"player_inventory.dat",
			"player_carnet_savoir.json",
			"player_session.dat",
			"player.dat",
			NomFichierIdentiteJoueur
		};
		foreach (string f in fichiers)
		{
			try
			{
				string p = Path.Combine(dossier, f);
				if (File.Exists(p)) File.Delete(p);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"ZERO-K : Suppression sauvegarde personnage ({f}) : {ex.Message}");
			}
		}
		return true;
	}

	/// <summary>Valide le nouveau personnage sur le monde déjà chargé (après mort). Réécrit l’identité ; ne crée pas de nouveau dossier monde.</summary>
	public bool EssayerFinaliserRecreationPersonnageSurMondeExistant(string nomPersonnageBrut, RaceJoueur race, SexeJoueur sexe, out string erreur)
	{
		erreur = null;
		if (string.IsNullOrWhiteSpace(NomMondeActuel))
		{
			erreur = "Aucun monde actif.";
			return false;
		}
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{NomMondeActuel}");
		if (!Directory.Exists(dossier))
		{
			erreur = "Dossier de sauvegarde introuvable.";
			return false;
		}
		string nomPerso = NettoyerNomPersonnage(nomPersonnageBrut);
		NomPersonnageJoue = nomPerso;
		RaceJoueurCourante = race;
		SexeJoueurCourante = sexe;
		try
		{
			SauvegarderIdentiteJoueurSurDisque(NomMondeActuel, nomPerso, race, sexe);
		}
		catch (Exception ex)
		{
			erreur = ex.Message;
			return false;
		}
		RecreationPersonnageMemeMondeEnAttente = false;
		EcrireDernierMondeJoue(NomMondeActuel);
		GD.Print($"ZERO-K : Nouveau personnage sur monde existant « {NomMondeActuel} » : « {nomPerso} », {race}, {sexe}.");
		return true;
	}
}
