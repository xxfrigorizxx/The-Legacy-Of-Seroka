using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Orchestre Monde_Serveur (données) et Monde_Client (visuel). Support Solo (Host local) et MMORPG.</summary>
public partial class Gestionnaire_Monde : Node3D
{
	[Export] public int TailleChunk = 16;
	[Export] public int HauteurMax = 720;  // Montagnes jusqu'à 700
	[Export] public int SeedTerrain = 19847;
	/// <summary>Profondeur étendue des dimensions alpha-like (Alpha/Beta/Omega/Delta) : le sous-sol descend jusqu'à -<see cref="ProfondeurMaxMetres"/> sans changer la surface. Mettre à false pour revenir au socle Y=0.</summary>
	[Export] public bool ActiverProfondeurEtendue = true;
	[Export] public int ProfondeurMaxMetres = 1000;
	[Export] public int RenderDistance = 14;
	[Export] public int RenderDistanceDetailChunks = 10;
	[Export] public int RayonQualiteProcheChunks = 5;
	[Export] public int RayonGazonVisibleChunks = 9;
	[Export] public int RayonBuissonsVisibleChunks = 16;
	[Export] public bool ActiverHorizonLod = false;
	[Export] public int RayonHorizonChunks = 72;
	[Export] public float PasHorizonMetres = 20f;
	[Export] public bool ActiverCullingCameraChunks = true;
	[Export] public float AngleCullingCameraDeg = 135f;
	[Export] public int MargeChunksToujoursVisibles = 8;
	/// <summary>Requêtes réseau / chargement par frame côté client. Monde gigantesque : 4 est trop lent pour que le sol et les collisions suivent la marche.</summary>
	[Export] public int MaxChunksParFrame = 14;
	/// <summary>Si vrai et aucune position sauvegardée : alignement raycast au sol. Avec session / player.dat, la hauteur sauvegardée est conservée.</summary>
	[Export] public bool ForcerAlignementSolAuChargement = true;
	/// <summary>Fuseau horaire du Monde 1. Québec = -5, Paris = +1, UTC = 0.</summary>
	[Export] public double FuseauHoraireHeures = -5;
	[Export] public bool PreGenererAuDemarrage = false;
	[Export] public int RayonPreGeneration = 2;
	[Export] public bool ModeEssencesPartoutTemporaire = false;
	[Export] public float RatioJungleModeTest = 0.30f;
	[Export] public bool ActiverAutosauvegarde = true;
	/// <summary>Moins d’écart entre deux gravures disque en cas de crash (rechargement = monde identique si sauvegarde récente).</summary>
	[Export] public float IntervalleAutosauvegardeSecondes = 25f;
	[Export] public int MaxChunksAutosauvegardeParCycle = 4;
	[Export] public bool ExigerBootstrapClientStableAvantMasquerOverlay = true;
	[Export(PropertyHint.Range, "0,120,1")] public float DureeMaxAttenteBootstrapClientSec = 18f;
	[ExportGroup("Profil matériel auto")]
	[Export] public bool ActiverProfilMaterielAuto = true;
	[Export] public bool ForcerProfilGTX1060i710700F = false;
	[ExportGroup("Warmup shaders")]
	[Export] public bool ActiverWarmupShadersProgressif = true;
	[Export(PropertyHint.Range, "1,8,1")] public int WarmupMateriauxParFrame = 1;
	[Export(PropertyHint.Range, "0.02,1,0.01")] public float IntervalleWarmupShadersSec = 0.12f;
	[ExportGroup("Stabilité runtime")]
	[Export] public bool ActiverSurveillanceOrphans = true;
	[Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleSurveillanceOrphansSec = 2.0f;
	[Export(PropertyHint.Range, "0.2,5,0.1")] public float IntervalleRefreshCacheDormanceSec = 0.8f;
	[ExportGroup("Diagnostic performance")]
	[Export] public bool ActiverProfilagePerfGestionnaire = false;
	[Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleLogProfilageSec = 2.0f;
	[Export] public Material MaterielTerrain;
	/// <summary>Matériau eau (océan). Créé automatiquement dans _Ready à partir de EauTriplanar.gdshader. Non exposé à l'éditeur.</summary>
	public Material MaterielEau;
	/// <summary>Échelle du gazon (grass.glb) sur ID 1. Modifier pour ajuster la taille partout.</summary>
	[Export] public float EchelleGazon = 2f;
	public int RayonMondeChunks = 1000;

	/// <summary>Si true, utilise Monde_Serveur + Monde_Client (Solo/MMORPG). Si false, legacy Generateur_Voxel.</summary>
	[Export] public bool UseArchitectureReseau = true;

	/// <summary>Si true : au lancement on ignore <c>user://options_graphics.cfg</c> et on garde les [Export] de la scène (sinon le fichier réécrit tout et l’inspecteur semble « ne rien faire »).</summary>
	[ExportGroup("Options graphiques (fichier)")]
	[Export] public bool IgnorerFichierOptionsGraphiquesAuDemarrage = false;

	// Files pour le mode legacy (Generateur_Voxel)
	private ConcurrentQueue<System.Action> _misesAJourMainThread = new ConcurrentQueue<System.Action>();
	public ConcurrentQueue<System.Action> _misesAJourUrgentes = new ConcurrentQueue<System.Action>();

	private CharacterBody3D _joueur;
	private Monde_Serveur _mondeServeur;
	private Monde_Serveur _mondeServeurAlpha;
	private Monde_Serveur _mondeServeurAbysse;
	private Monde_Client _mondeClient;
	private NetworkManager _networkManager;
	private readonly Dictionary<int, Monde_Serveur> _serveurParDimension = new Dictionary<int, Monde_Serveur>();
	private readonly Dictionary<long, int> _dimensionParPeer = new Dictionary<long, int>();
	private readonly Dictionary<int, Dictionary<Vector3I, HashSet<long>>> _attenteChunksParDimension = new Dictionary<int, Dictionary<Vector3I, HashSet<long>>>();
	private int _dimensionLocaleActive = (int)DimensionJeu.Alpha;
	/// <summary>Environment Alpha/autres dimensions avant bascule APISARA — restauré à la sortie pour ne pas perdre la ressource de scène.</summary>
	private Godot.Environment _environnementSauvegardeHorsApisara;
	private const string FichierSessionJoueur = "player_session.dat";
	/// <summary>Racine de scène par dimension (id → Node3D). Remplace les ex-champs Alpha/Abysse pour supporter Beta/Omega/Delta.</summary>
	private readonly Dictionary<int, Node3D> _racineParDimension = new Dictionary<int, Node3D>();
	/// <summary>Conteneur d'arbres scéniques par dimension (id → Node3D). Toggle de visibilité par dimension active.</summary>
	private readonly Dictionary<int, Node3D> _arbresParDimension = new Dictionary<int, Node3D>();
	/// <summary>XZ du portail « vers APISARA » par dimension Alpha-like (cible de retour depuis APISARA).</summary>
	private readonly Dictionary<int, Vector2> _xzPortailVersApisaraParDimension = new Dictionary<int, Vector2>();
	/// <summary>Position joueur mémorisée par dimension (clé = id de dimension). Permet de revenir « exactement où j'étais » dans chaque monde.</summary>
	private readonly Dictionary<int, Vector3> _positionsSauvegardeesParDimension = new Dictionary<int, Vector3>();
	private Label _labelCoords;
	private Label _labelHeureDimension;
	private CanvasLayer _repereCentreLayer;
	/// <summary>Overlay "Chargement du monde..." affiché tant que la collision du chunk de spawn n'est pas prête.</summary>
	private CanvasLayer _overlayChargement;
	private Label _labelChargementPrincipal;
	/// <summary>Nom canonique du phénomène qui provoque les effets de remontée par palier (APISARA). Ce n'est ni une « maladie du vide », ni une malédiction générique : c'est l'EMERUKEDESI PAROTAROMA.</summary>
	public const string EmerukedesiParotaroma = "EMERUKEDESI PAROTAROMA";

	/// <summary>Overlay visuel : manifestation palier 1 de l'<see cref="EmerukedesiParotaroma"/> (flou perceptif, pas un simple « mal de l'air »).</summary>
	private CanvasLayer _overlayEmerukedesiParotaromaStage1;
	private ShaderMaterial _materiauEmerukedesiParotaromaStage1;
	private double _secondesOverlayChargement;
	private float _dernierYRemonteeAbysse = float.NaN;
	private float _yDepartMonteeAbysse = float.NaN;
	private bool _monteeAbysseContinue;
	private bool _emerukedesiParotaromaStage1Actif;
	private bool _emerukedesiParotaromaStage1FonduSortieActif;
	private double _emerukedesiParotaromaStage1TempsFonduRestant;
	/// <summary>Gain vertical minimal (m) cumulés pour déclencher la manifestation, sans contrainte de vitesse/temps.</summary>
	private const float SeuilDeclenchementRemonteeAbysseMetres = 2f;
	/// <summary>Progression verticale minimale par frame (m) pour considérer une remontée effective (ignore le bruit physique).</summary>
	private const float SeuilProgressionMonteeAbysseMetres = 0.01f;
	/// <summary>Descente cumulée par frame (m) considérée comme « vraie redescente » pour repartir la base de cumul.</summary>
	private const float SeuilRedescenteNetteAbysseMetres = 0.20f;
	/// <summary>Délai anti-yoyo avant lancement du fondu quand la remontée cesse (immobile/descente légère).</summary>
	private const double DelaiArretMonteeAvantFonduParotaromaSec = 0.30;
	private double _secondesSansMonteeAbysse;
	/// <summary>Durée du fondu quand la remontée cesse, pour l'effet palier 1 de l'<see cref="EmerukedesiParotaroma"/>.</summary>
	private const double DureeFonduEmerukedesiParotaromaStage1Sec = 15.0;
	private bool _chargementAbysseEnCours;
	private double _secondesStabiliteAbyssePret;
	private const double DureeStabiliteAbyssePretSec = 0.22;
	private double _secondesVerrouAbysse;
	private const double DureeMaxVerrouAbysseSec = 6.0;
	private const double DureeTimeoutDurVerrouAbysseSec = 35.0;
	private double _cooldownRearmementVerrouAbysse;
	private const double CooldownRearmementVerrouAbysseSec = 10.0;
	private bool _gateTpDimensionActif;
	/// <summary>Position cible pendant un TP dimension (streaming chunks tant que le joueur est hors arbre).</summary>
	private Vector3 _positionReferenceTransfertDimension;
	private double _secondesGateTpDimension;
	private const double DureeMaxGateTpDimensionSec = 8.0;
	private bool _portailsNexusPlaces;
	/// <summary>Assombrissement plein écran avant TP portail (client).</summary>
	private CanvasLayer _overlayPortailTransition;
	private ColorRect _rectAssombrissementPortail;
	private ColorRect _rectEffetVitessePortail;
	private ShaderMaterial _materiauEffetVitessePortail;
	private Tween _tweenTransitionPortail;
	private double _cooldownPulseReveilPierresTp;
	private const double IntervallePulseReveilPierresTpSec = 0.30;
	private bool _verrouMarcheAbysseActif;
	private double _secondesVerrouMarcheAbysse;
	private double _secondesStabiliteMarcheAbysse;
	private const double DureeMaxVerrouMarcheAbysseSec = 2.5;
	private const double DureeStabiliteSortieVerrouMarcheAbysseSec = 0.15;
	private Vector3 _spawnInitialEnAttente;
	private bool _spawnDoitEtreAligneAuSol;
	private bool _spawnAligneAuSol;
	private bool _ajusterPiedsJoueurSurSurfaceApresRestauration;
	/// <summary>Phase A (inventaire, progression, carnet) déjà restaurée depuis le disque.</summary>
	private bool _restaurationPersistantPhaseJoueurFaite;
	/// <summary>Phase B (objets posés, blocs chutants persistants, faune) déjà exécutée.</summary>
	private bool _restaurationPersistantObjetsSolFaite;
	/// <summary>Une sauvegarde complète après la 1re restauration sol pour aligner disque ↔ scène (tables, inventaire, chunks).</summary>
	private bool _synchronisationDisquePostRestaurationSolEffectuee;
	/// <summary>RigidBody restaurés gelés jusqu’à ce que le chunk ait une collision terrain (évite chute dans le vide au reload).</summary>
	private readonly List<RigidBody3D> _rigidBodiesAttenteCollisionSolRestauration = new List<RigidBody3D>();
	private double _secondesDormanceObjets;
	private const int RayonDormanceObjetsChunks = 5;
	[Export] public int BudgetDormanceObjetsParCycle = 120;
	[Export] public int RayonSecuriteTerrainObjetsChunks = 1;
	[Export] public bool ActiverFiletSecuriteObjetsDynamiques = true;
	[Export] public int BudgetFiletSecuriteObjetsParCycle = 72;
	[Export] public float SeuilEnfouissementObjetsMetres = 1.2f;
	[Export] public float MargeRemonteeObjetsMetres = 0.15f;
	[Export] public bool ActiverDiagnosticCollisionAbysse = false;
	private double _cooldownDiagnosticCollisionAbysse;
	private const double IntervalleDiagnosticCollisionAbysseSec = 0.9;
	private const float NiveauEauOcean = 103f;
	private Area3D _oceanPhysique;
	private Node3D _conteneurEffetsEau;
	private readonly HashSet<ulong> _corpsDansOcean = new HashSet<ulong>();
	private StandardMaterial3D _materielEclaboussureEau;
	private readonly Dictionary<ulong, Node3D> _corpsSuiviRemous = new Dictionary<ulong, Node3D>();
	private readonly Dictionary<ulong, GpuParticles3D> _effetsRemousParCorps = new Dictionary<ulong, GpuParticles3D>();
	private readonly List<ulong> _tmpRemousASupprimer = new List<ulong>();
	private bool _chargementCycleSolaire;
	private double _secondesDepuisAutosauvegarde;
	private int _indexDormanceBlocsPoses;
	private int _indexDormanceObjetsDyn;
	private Vector3 _dernieresCoordsAffichees = new Vector3(float.NaN, float.NaN, float.NaN);
	private string _dernierTexteHeureDimension = "";
	private float _cooldownDrainProfilage = 0f;
	private float _cooldownLogAutosaveDiag = 0f;
	private readonly Dictionary<string, List<RigidBody3D>> _cacheRigidBodiesDormance = new Dictionary<string, List<RigidBody3D>>();

	private readonly struct SessionJoueurSauvegardee
	{
		public readonly int DimensionId;
		public readonly Vector3 Position;
		/// <summary>Dictionnaire des positions mémorisées par dimension (id → position). En lecture v1 : ne contient que la dimension active.</summary>
		public readonly Dictionary<int, Vector3> PositionsParDimension;
		public SessionJoueurSauvegardee(int dimensionId, Vector3 position, Dictionary<int, Vector3> positionsParDimension)
		{
			DimensionId = dimensionId;
			Position = position;
			PositionsParDimension = positionsParDimension ?? new Dictionary<int, Vector3>();
		}
	}
	private float _cooldownRefreshCacheDormance;
	private float _cooldownSurveillanceOrphans;
	private int _dernierOrphanNodes = -1;
	private readonly List<Material> _fileWarmupMateriaux = new List<Material>();
	private int _indexWarmupMateriau;
	private float _cooldownWarmupShaders;
	private MeshInstance3D _meshWarmupShaders;
	private string _nomCpuDetecte = "";
	private string _nomGpuDetecte = "";
	private DimensionCoordinator _dimensionCoordinator;
	private WorldLifecycleBootstrap _worldLifecycleBootstrap;
	private readonly WorldUiFacade _worldUiFacade = new WorldUiFacade();

	// Legacy
	private List<Vector2I> _chunksACharger = new List<Vector2I>();
	private bool _radarLegacyEnCours;
	private Dictionary<Vector2I, Node3D> _chunks = new Dictionary<Vector2I, Node3D>();
	private PackedScene _sceneChunk;
	private Vector2I _ancienChunkJoueur = new Vector2I(-99999, -99999);

	public void EnqueueMiseAJourMainThread(System.Action action) => _misesAJourMainThread.Enqueue(action);
	public void EnqueueMiseAJourUrgente(System.Action action) => _misesAJourUrgentes.Enqueue(action);
	public float ObtenirNiveauSurfaceEau() => NiveauEauOcean + 0.35f;

	/// <summary>Conversion monde → chunk avec arrondi géométrique (Floor). OBLIGATOIRE pour coordonnées négatives : (int)(x/TailleChunk) tronque vers zéro et casse la zone de spawn.</summary>
	public static Vector2I WorldToChunkCoord(float worldX, float worldZ, int tailleChunk)
	{
		return new Vector2I(
			Mathf.FloorToInt(worldX / (float)tailleChunk),
			Mathf.FloorToInt(worldZ / (float)tailleChunk));
	}

	/// <summary>Conversion monde → chunk (Vector3). Utilise WorldToChunkCoord pour éviter la division entière C#.</summary>
	public static Vector2I WorldToChunkCoord(Vector3 worldPos, int tailleChunk)
		=> WorldToChunkCoord(worldPos.X, worldPos.Z, tailleChunk);

	/// <summary>Règle d'or : coordonnée locale par soustraction euclidienne, JAMAIS par modulo (en C#, -5 % 16 = -5). Garantit local dans [0, TailleChunk] pour monde négatif.</summary>
	public static void WorldToChunkAndLocal(float worldX, float worldZ, int tailleChunk, out Vector2I coordChunk, out int localX, out int localZ)
	{
		coordChunk = WorldToChunkCoord(worldX, worldZ, tailleChunk);
		int worldCellX = Mathf.FloorToInt(worldX);
		int worldCellZ = Mathf.FloorToInt(worldZ);
		localX = worldCellX - coordChunk.X * tailleChunk;
		localZ = worldCellZ - coordChunk.Y * tailleChunk;
	}

	private Monde_Serveur ObtenirServeurDimension(int dimensionId)
	{
		_serveurParDimension.TryGetValue(dimensionId, out Monde_Serveur serveur);
		return serveur;
	}

	private int ObtenirDimensionPeer(long peerId)
	{
		if (_dimensionParPeer.TryGetValue(peerId, out int dimension))
			return dimension;
		return (int)DimensionJeu.Alpha;
	}

	private void DefinirDimensionPeer(long peerId, int dimensionId)
	{
		_dimensionParPeer[peerId] = dimensionId;
	}

	/// <summary>Reparente sous la racine de dimension (toujours différé : sûr pendant <c>_Ready</c> et changements d'arbre).</summary>
	private void ReparenterNoeudDansDimension(Node3D noeud, int dimensionId, Vector3? positionApresReparent = null)
	{
		if (noeud == null || !GodotObject.IsInstanceValid(noeud))
			return;
		Vector3 pos = positionApresReparent ?? noeud.GlobalPosition;
		CallDeferred(nameof(ReparenterNoeudDansDimensionDiffere), noeud, dimensionId, pos);
	}

	private void ReparenterNoeudDansDimensionDiffere(Node3D noeud, int dimensionId, Vector3 positionFinale)
	{
		if (noeud == null || !GodotObject.IsInstanceValid(noeud))
			return;
		if (!_racineParDimension.TryGetValue(dimensionId, out Node3D cible))
			_racineParDimension.TryGetValue((int)DimensionJeu.Alpha, out cible);
		if (cible == null || !GodotObject.IsInstanceValid(cible))
			return;
		if (noeud.GetParent() != cible)
			noeud.Reparent(cible, true);
		noeud.GlobalPosition = positionFinale;
	}

	/// <summary>Vrai si le nœud joueur existe encore (évite <see cref="ObjectDisposedException"/> après mort / transition).</summary>
	public bool JoueurReferenceValide()
	{
		if (_joueur == null)
			return false;
		try
		{
			if (!GodotObject.IsInstanceValid(_joueur))
			{
				_joueur = null;
				return false;
			}
			if (!_joueur.IsInsideTree())
				return false;
			return true;
		}
		catch (ObjectDisposedException)
		{
			_joueur = null;
			return false;
		}
	}

	public CharacterBody3D ObtenirJoueurSiValide()
	{
		return JoueurReferenceValide() ? _joueur : null;
	}

	public Vector3 ObtenirPositionJoueurOuSpawn()
	{
		if (_gateTpDimensionActif)
			return _positionReferenceTransfertDimension;
		if (!JoueurReferenceValide())
			return _spawnInitialEnAttente;
		try
		{
			return _joueur.GlobalPosition;
		}
		catch (ObjectDisposedException)
		{
			_joueur = null;
			return _spawnInitialEnAttente;
		}
	}

	/// <summary>Vrai si le chunk sous les pieds du joueur a sa collision construite (évite chute libre au spawn).</summary>
	public bool EstSpawnPret()
	{
		if (!JoueurReferenceValide()) return false;
		Vector3 pos = ObtenirPointReferenceSpawn();
		if (UseArchitectureReseau)
		{
			if (_mondeClient == null) return false;
			if (_dimensionLocaleActive == (int)DimensionJeu.Abysse)
				return _mondeClient.AbyssePretPourDeplacement(pos);
			Vector2I cReseau = WorldToChunkCoord(pos, TailleChunk);
			return _mondeClient.ChunkCollisionActive(cReseau);
		}
		Vector2I c = WorldToChunkCoord(pos, TailleChunk);
		if (!_chunks.TryGetValue(c, out var n)) return false;
		var ch = n as Generateur_Voxel;
		if (ch == null) return false;
		int sec = Mathf.FloorToInt(pos.Y / 16f);
		return ch.SectionAPret(sec);
	}

	/// <summary>Vrai si la collision du chunk contenant ce point monde est prête (réseau ou legacy).</summary>
	public bool EstCollisionTerrainChunkPretPourPoint(Vector3 monde)
	{
		Vector2I c = WorldToChunkCoord(monde, TailleChunk);
		if (UseArchitectureReseau)
			return _mondeClient != null && _mondeClient.ChunkCollisionActive(c);
		if (!_chunks.TryGetValue(c, out var n)) return false;
		var ch = n as Generateur_Voxel;
		if (ch == null) return false;
		int sec = Mathf.FloorToInt(monde.Y / 16f);
		return ch.SectionAPret(sec);
	}

	/// <summary>
	/// Portail Nexus vers APISARA : le terrain du chunk à ce XZ est streamé pour la dimension active
	/// (collision + voxels), prêt pour un raycast sol fidèle à la génération (seed).
	/// </summary>
	public bool EstTerrainClientPretPourPortailVersApisara(float mondeX, float mondeZ, int dimensionIdPortail)
	{
		if (dimensionIdPortail == (int)DimensionJeu.Abysse)
			return false;
		if (!UseArchitectureReseau || _mondeClient == null)
		{
			float y = EstimerAltitudeTerrainPortail(mondeX, mondeZ, dimensionIdPortail);
			return EstCollisionTerrainChunkPretPourPoint(new Vector3(mondeX, y, mondeZ));
		}
		if (dimensionIdPortail != _dimensionLocaleActive)
			return false;
		return _mondeClient.ChunkTerrainPretAvecVoxelsPourCoordMonde(mondeX, mondeZ);
	}

	private string ObtenirCheminSessionJoueur()
	{
		return System.IO.Path.Combine(ProjectSettings.GlobalizePath($"user://saves/{GameState.Instance?.NomMondeActuel}"), FichierSessionJoueur);
	}

	/// <summary>Sauvegarde la session joueur (v2) : dimension active + dernière position connue dans chaque dimension visitée.
	/// Met à jour automatiquement <see cref="_positionsSauvegardeesParDimension"/>[dimensionId] = position avant d'écrire le fichier.</summary>
	private void SauvegarderSessionJoueur(int dimensionId, Vector3 position)
	{
		if (GameState.Instance == null || string.IsNullOrWhiteSpace(GameState.Instance.NomMondeActuel))
			return;
		_positionsSauvegardeesParDimension[dimensionId] = position;
		try
		{
			string dossier = ProjectSettings.GlobalizePath($"user://saves/{GameState.Instance.NomMondeActuel}");
			System.IO.Directory.CreateDirectory(dossier);
			string chemin = ObtenirCheminSessionJoueur();
			using var w = new System.IO.BinaryWriter(System.IO.File.Open(chemin, System.IO.FileMode.Create));
			w.Write(2); // version (v2 = positions par dimension)
			w.Write(dimensionId);
			w.Write(position.X);
			w.Write(position.Y);
			w.Write(position.Z);
			w.Write(_positionsSauvegardeesParDimension.Count);
			foreach (var kv in _positionsSauvegardeesParDimension)
			{
				w.Write(kv.Key);
				w.Write(kv.Value.X);
				w.Write(kv.Value.Y);
				w.Write(kv.Value.Z);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur sauvegarde session joueur ({dimensionId}) : {ex.Message}");
		}
	}

	/// <summary>Lit le fichier de session. Compatible v1 (un seul (dim, pos)) ET v2 (dim active + dictionnaire de positions par dim).
	/// Pour v1, on synthétise un dictionnaire à une seule entrée. Met à jour <see cref="_positionsSauvegardeesParDimension"/>.</summary>
	private SessionJoueurSauvegardee? ChargerSessionJoueur()
	{
		if (GameState.Instance == null || string.IsNullOrWhiteSpace(GameState.Instance.NomMondeActuel))
			return null;
		string chemin = ObtenirCheminSessionJoueur();
		if (!System.IO.File.Exists(chemin))
			return null;
		try
		{
			using var r = new System.IO.BinaryReader(System.IO.File.Open(chemin, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read));
			int version = r.ReadInt32();
			if (version != 1 && version != 2)
				return null;
			int dimensionId = r.ReadInt32();
			float x = r.ReadSingle();
			float y = r.ReadSingle();
			float z = r.ReadSingle();
			Vector3 position = new Vector3(x, y, z);
			var positions = new Dictionary<int, Vector3>();
			if (version == 1)
			{
				positions[dimensionId] = position;
			}
			else
			{
				int count = r.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					int dimId = r.ReadInt32();
					float px = r.ReadSingle();
					float py = r.ReadSingle();
					float pz = r.ReadSingle();
					positions[dimId] = new Vector3(px, py, pz);
				}
			}
			_positionsSauvegardeesParDimension.Clear();
			foreach (var kv in positions)
				_positionsSauvegardeesParDimension[kv.Key] = kv.Value;
			return new SessionJoueurSauvegardee(dimensionId, position, positions);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur lecture session joueur : {ex.Message}");
			return null;
		}
	}

	/// <summary>Collision terrain + sol physique réel sous le corps (évite dégel prématuré au reload).</summary>
	private bool EstSolPhysiquePretSousRigidBody(RigidBody3D rb)
	{
		if (rb == null || !GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree())
			return false;
		if (!EstCollisionTerrainChunkPretPourPoint(rb.GlobalPosition))
			return false;
		if (UseArchitectureReseau && _mondeClient != null
			&& !_mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, 1))
			return false;

		PhysicsDirectSpaceState3D espace = rb.GetWorld3D()?.DirectSpaceState;
		if (espace == null)
			return true;

		Vector3 origine = rb.GlobalPosition + Vector3.Up * 0.35f;
		Vector3 fin = rb.GlobalPosition + Vector3.Down * 2.5f;
		var requete = PhysicsRayQueryParameters3D.Create(origine, fin);
		requete.CollisionMask = 1;
		requete.CollideWithAreas = false;
		requete.Exclude = new Godot.Collections.Array<Rid> { rb.GetRid() };
		var impact = espace.IntersectRay(requete);
		if (impact.Count == 0 || !impact.ContainsKey("position"))
			return false;

		float ySol = ((Vector3)impact["position"]).Y;
		float ecart = rb.GlobalPosition.Y - ySol;
		return ecart <= 1.35f && ecart >= -0.45f;
	}

	private void AjouterRigidBodyFileAttenteRestaurationSol(RigidBody3D rb)
	{
		if (_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
			return;
		rb.Freeze = true;
		rb.LinearVelocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		rb.Sleeping = true;
		_rigidBodiesAttenteCollisionSolRestauration.Add(rb);
	}

	/// <summary>Gèle le corps jusqu’à ce que le terrain soit streamé sous lui, puis dégel progressif dans <see cref="TraiterDepgelRigidBodiesRestaurationSol"/>.</summary>
	public void EnregistrerRigidBodyRestaurationSolSiCollisionManquante(RigidBody3D rb)
	{
		if (rb == null || !GodotObject.IsInstanceValid(rb)) return;
		if (rb is ItemPhysique ipMeuble && ItemPhysique.EstMeublePoseStatique(ipMeuble.ID_Objet))
			return;
		if (EstSolPhysiquePretSousRigidBody(rb)) return;
		AjouterRigidBodyFileAttenteRestaurationSol(rb);
	}

	/// <summary>Au chargement de sauvegarde : toujours geler les corps dynamiques jusqu’à sol physique prêt (positions sauvegardées).</summary>
	public void EnregistrerRigidBodyRestaurationSolAuChargement(RigidBody3D rb)
	{
		if (rb == null || !GodotObject.IsInstanceValid(rb)) return;
		if (rb is ItemPhysique ipMeuble && ItemPhysique.EstMeublePoseStatique(ipMeuble.ID_Objet))
			return;
		AjouterRigidBodyFileAttenteRestaurationSol(rb);
	}

	private void TraiterDepgelRigidBodiesRestaurationSol(int maxParFrame)
	{
		int budget = Mathf.Max(1, maxParFrame);
		for (int i = _rigidBodiesAttenteCollisionSolRestauration.Count - 1; i >= 0 && budget > 0; i--)
		{
			RigidBody3D rb = _rigidBodiesAttenteCollisionSolRestauration[i];
			if (!GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree())
			{
				_rigidBodiesAttenteCollisionSolRestauration.RemoveAt(i);
				budget--;
				continue;
			}
			if (EstSolPhysiquePretSousRigidBody(rb))
			{
				if (rb is ItemPhysique ipFigé && ItemPhysique.EstMeublePoseStatique(ipFigé.ID_Objet))
				{
					ipFigé.LinearVelocity = Vector3.Zero;
					ipFigé.AngularVelocity = Vector3.Zero;
					ipFigé.Freeze = true;
					ipFigé.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
					ipFigé.Sleeping = true;
				}
				else
				{
					rb.Freeze = false;
					rb.Sleeping = false;
				}
				_rigidBodiesAttenteCollisionSolRestauration.RemoveAt(i);
			}
			budget--;
		}
	}

	private Vector3 ObtenirPointReferenceSpawn()
	{
		if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
			return _spawnInitialEnAttente;
		return ObtenirPositionJoueurOuSpawn();
	}

	private bool ChunkEtVoisinsCardinauxPretsAuPoint(Vector3 point)
	{
		if (!UseArchitectureReseau) return true;
		if (_mondeClient == null) return false;
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse)
			return _mondeClient.AbyssePretPourDeplacement(point);
		Vector2I c = WorldToChunkCoord(point, TailleChunk);
		if (!_mondeClient.ChunkCollisionActive(c)) return false;
		if (!_mondeClient.ChunkCollisionActive(new Vector2I(c.X - 1, c.Y))) return false;
		if (!_mondeClient.ChunkCollisionActive(new Vector2I(c.X + 1, c.Y))) return false;
		if (!_mondeClient.ChunkCollisionActive(new Vector2I(c.X, c.Y - 1))) return false;
		if (!_mondeClient.ChunkCollisionActive(new Vector2I(c.X, c.Y + 1))) return false;
		return true;
	}

	/// <summary>Nouvelle partie : le joueur ne doit bouger en physique qu’après <see cref="FinaliserSpawnInitialAuSol"/> (sinon il tombe depuis Y=h+10 avant le raycast et peut traverser le sol).</summary>
	public bool EstAlignementSpawnTermine() => !_spawnDoitEtreAligneAuSol || _spawnAligneAuSol;

	/// <summary>Vrai si le verrou anti-chute APISARA force actuellement l'arrêt du mouvement joueur.</summary>
	public bool EstVerrouSecuriteAbysseActif() => _gateTpDimensionActif;

	public bool EstDimensionLocaleAbysse() => _dimensionLocaleActive == (int)DimensionJeu.Abysse;
	public int ObtenirDimensionLocaleActiveId() => _dimensionLocaleActive;

	/// <summary>Racine 3D de la dimension (ARAPA, BETA, …) — parent des objets posés / joueur reparenté.</summary>
	public Node3D ObtenirRacineDimension(int dimensionId)
	{
		if (_racineParDimension.TryGetValue(dimensionId, out Node3D racine) && racine != null && GodotObject.IsInstanceValid(racine))
			return racine;
		return null;
	}

	/// <summary>Vrai si la zone locale du joueur est prête pour un déplacement physique sûr.</summary>
	public bool EstDeplacementLocalPret()
	{
		if (!UseArchitectureReseau || _mondeClient == null || !JoueurReferenceValide())
			return true;
		Vector3 posJoueur = _joueur.GlobalPosition;
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse)
			return _mondeClient.AbysseCollisionLocaleActive(posJoueur);
		Vector2I c = WorldToChunkCoord(posJoueur, TailleChunk);
		return _mondeClient.ChunkCollisionActive(c);
	}

	private void JournaliserDiagnosticCollisionAbysse()
	{
		if (!ActiverDiagnosticCollisionAbysse || _dimensionLocaleActive != (int)DimensionJeu.Abysse || !JoueurReferenceValide() || _mondeClient == null)
			return;
		Vector3 posJoueur = _joueur.GlobalPosition;
		bool spawnPret = EstSpawnPret();
		bool deplacementPret = EstDeplacementLocalPret();
		bool collisionLocale = _mondeClient.AbysseCollisionLocaleActive(posJoueur);
		bool gateTp = _gateTpDimensionActif;
		Vector2I chunk = WorldToChunkCoord(posJoueur, TailleChunk);
		GD.Print($"ZERO-K ABYSSE DIAG MONDE: chunk={chunk} spawnPret={spawnPret} deplacementPret={deplacementPret} collisionLocale={collisionLocale} gateTp={gateTp} overlay={(_overlayChargement?.Visible ?? false)}");
	}

	private bool CollisionLocalePretePourTpDimension()
	{
		if (!UseArchitectureReseau || _mondeClient == null)
			return true;
		Vector3 posJoueur = ObtenirPositionJoueurOuSpawn();
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse)
			return _mondeClient.AbysseCollisionLocaleActive(posJoueur);
		Vector2I c = WorldToChunkCoord(posJoueur, TailleChunk);
		return _mondeClient.ChunkCollisionActive(c);
	}

	/// <summary>Utilisé par Generateur_Voxel (legacy) et Monde_Serveur.</summary>
	public bool ChunkEstCharge(Vector2I coord)
	{
		if (UseArchitectureReseau) return _mondeServeur?.ChunkEstCharge(coord) ?? false;
		return _chunks.ContainsKey(coord);
	}

	/// <summary>Utilisé par Generateur_Voxel (legacy). En mode réseau, Monde_Serveur gère l'eau.</summary>
	public void ReveillerEauAdjacente(Vector3 pointGlobal)
	{
		if (UseArchitectureReseau) { _mondeServeur?.ReveillerEauAdjacente(pointGlobal); return; }
		ReveillerEauAdjacenteLegacy(pointGlobal);
	}

	private Queue<Vector3I> _fileEau = new Queue<Vector3I>();
	private HashSet<Vector3I> _eauActive = new HashSet<Vector3I>();
	private readonly Dictionary<Vector3I, (Vector3I retourInterdit, int tickExpiration)> _antiRetourEauLegacy = new Dictionary<Vector3I, (Vector3I, int)>();
	private int _tickEauLegacy;
	private const int MaxEauParTick = 24;
	private const int DureeBlocageRetourEauLegacyTicks = 5;
	private static readonly Vector3I[] DirReveilEau = { new Vector3I(0, 1, 0), new Vector3I(0, -1, 0), new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, 1), new Vector3I(0, 0, -1) };
	private static readonly Vector3I[] DirEauHorizLegacy = { new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, -1), new Vector3I(0, 0, 1) };

	private static uint MelangerSeed(uint v)
	{
		v ^= v >> 16;
		v *= 0x7feb352du;
		v ^= v >> 15;
		v *= 0x846ca68bu;
		v ^= v >> 16;
		return v;
	}

	private static int EtendreDansPlage(uint h, int minInclusif, int maxInclusif)
	{
		if (maxInclusif <= minInclusif) return minInclusif;
		uint amplitude = (uint)(maxInclusif - minInclusif + 1);
		return minInclusif + (int)(h % amplitude);
	}

	private int EvaluerCandidatSpawn(int x, int z)
	{
		int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, SeedTerrain);
		if (h < 103 || h > 230) return int.MinValue;

		// Rejette les zones trop abruptes (rive abrupte, pente forte, bord de canyon).
		int hE = Generateur_Voxel.ObtenirHauteurTerrainMonde(x + 8, z, SeedTerrain);
		int hW = Generateur_Voxel.ObtenirHauteurTerrainMonde(x - 8, z, SeedTerrain);
		int hN = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z - 8, SeedTerrain);
		int hS = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z + 8, SeedTerrain);
		int pente = Mathf.Abs(h - hE) + Mathf.Abs(h - hW) + Mathf.Abs(h - hN) + Mathf.Abs(h - hS);
		if (pente > 40) return int.MinValue;

		// Préférence pour un plateau "jouable" (ni sous l'eau, ni en haute montagne).
		int scoreAltitude = 600 - Mathf.Abs(h - 118) * 8;
		int scorePente = 220 - pente * 4;
		return scoreAltitude + scorePente;
	}

	private Vector3 CalculerSpawnInitialDepuisSeed()
	{
		uint s0 = MelangerSeed((uint)SeedTerrain ^ 0x9E3779B9u);
		uint s1 = MelangerSeed(s0 ^ 0x85EBCA6Bu);
		int baseX = EtendreDansPlage(s0, -4096, 4096);
		int baseZ = EtendreDansPlage(s1, -4096, 4096);

		const int pas = 24;
		const int rayonAnneaux = 24;
		int meilleurX = baseX;
		int meilleurZ = baseZ;
		int meilleurScore = int.MinValue;

		for (int anneau = 0; anneau <= rayonAnneaux; anneau++)
		{
			for (int dx = -anneau; dx <= anneau; dx++)
			{
				for (int dz = -anneau; dz <= anneau; dz++)
				{
					if (anneau > 0 && Mathf.Abs(dx) != anneau && Mathf.Abs(dz) != anneau) continue;
					int x = baseX + dx * pas;
					int z = baseZ + dz * pas;
					int score = EvaluerCandidatSpawn(x, z);
					if (score > meilleurScore)
					{
						meilleurScore = score;
						meilleurX = x;
						meilleurZ = z;
					}
				}
			}
			if (meilleurScore > 500) break;
		}

		int hauteurTerrain = Generateur_Voxel.ObtenirHauteurTerrainMonde(meilleurX, meilleurZ, SeedTerrain);
		// Quelques mètres au-dessus du relief : le raycast final pose les pieds. +40 laissait le corps dans le ciel si le sol tardait ou si le raycast échouait.
		float ySpawn = hauteurTerrain + 10f;
		if (hauteurTerrain < 103) ySpawn = Mathf.Max(ySpawn, 142f);
		return new Vector3(meilleurX + 0.5f, ySpawn, meilleurZ + 0.5f);
	}

	private static double CalculerDistanceHoraireCirculaire(double heureA, double heureB)
	{
		double delta = Math.Abs(heureA - heureB) % 24.0;
		return delta > 12.0 ? 24.0 - delta : delta;
	}

	private static int ObtenirPrioriteDimensionAlphaLike(int dimensionId)
	{
		if (dimensionId == (int)DimensionJeu.Alpha) return 0;
		if (dimensionId == (int)DimensionJeu.Beta) return 1;
		if (dimensionId == (int)DimensionJeu.Omega) return 2;
		if (dimensionId == (int)DimensionJeu.Delta) return 3;
		return 99;
	}

	private int SelectionnerDimensionInitialeParFuseauReel(out double offsetLocalHeures, out double distanceHeures)
	{
		offsetLocalHeures = DateTimeOffset.Now.Offset.TotalHours;
		double heureLocaleJoueur = DateTime.UtcNow.AddHours(offsetLocalHeures).TimeOfDay.TotalHours;
		int dimensionChoisie = (int)DimensionJeu.Alpha;
		double meilleurScore = double.MaxValue;
		int meilleurePriorite = int.MaxValue;

		foreach (var info in ConstantesDimensions.ToutesAlphaLike())
		{
			double heureDimension = DateTime.UtcNow.AddHours(FuseauHoraireHeures + info.FuseauOffsetHeures).TimeOfDay.TotalHours;
			double score = CalculerDistanceHoraireCirculaire(heureLocaleJoueur, heureDimension);
			int priorite = ObtenirPrioriteDimensionAlphaLike(info.Id);
			if (score < meilleurScore || (Math.Abs(score - meilleurScore) < 0.0001 && priorite < meilleurePriorite))
			{
				meilleurScore = score;
				meilleurePriorite = priorite;
				dimensionChoisie = info.Id;
			}
		}

		distanceHeures = meilleurScore;
		return dimensionChoisie;
	}

	/// <summary>Garantit un spawn au-dessus du terrain local pour éviter un joueur sous la map. Si <paramref name="conserverHauteurSauvegardee"/>, ne rabaisse pas un Y élevé (fondations / étages).</summary>
	private Vector3 AssurerSpawnAuDessusDuSol(Vector3 pos, bool conserverHauteurSauvegardee = false)
	{
		int hauteurTerrain = Generateur_Voxel.ObtenirHauteurTerrainMonde((int)pos.X, (int)pos.Z, SeedTerrain);
		float ySurfaceApprox = hauteurTerrain + 1.02f;
		float yCibleAuSol = _joueur is Joueur jo
			? jo.CalculerYOriginePourPiedsSurSurface(ySurfaceApprox)
			: hauteurTerrain + 2.85f;

		if (!conserverHauteurSauvegardee && pos.Y > hauteurTerrain + 18f)
		{
			GD.Print($"ZERO-K : Spawn abaissé (trop haut par rapport au terrain ~{hauteurTerrain}) {pos.Y:0.0} -> {yCibleAuSol:0.0}");
			pos.Y = yCibleAuSol;
		}

		float yMinSecurise = _joueur is Joueur jo2
			? jo2.CalculerYOriginePourPiedsSurSurface(hauteurTerrain + 0.25f)
			: hauteurTerrain + 2.2f;
		if (pos.Y < yMinSecurise)
		{
			GD.Print($"ZERO-K : Spawn corrigé (anti sous-map) {pos.Y:0.00} -> {yMinSecurise:0.00}");
			pos.Y = yMinSecurise;
		}
		return pos;
	}

	/// <summary>Raycast court sous la position sauvegardée : pieds sur fondation / plancher / sol proche, sans ramener au terrain lointain.</summary>
	private bool EssayerAjusterPiedsJoueurSurSurfaceProche(Vector3 positionApprox, out Vector3 positionAjustee)
	{
		positionAjustee = positionApprox;
		if (!EssayerTrouverSolParRaycastCourt(positionApprox, 5.5f, 9f, out Vector3 pointSol))
			return false;
		if (_joueur is Joueur jo)
			positionAjustee = new Vector3(positionApprox.X, jo.CalculerYOriginePourPiedsSurSurface(pointSol.Y), positionApprox.Z);
		else
			positionAjustee = new Vector3(positionApprox.X, pointSol.Y + 1.2f, positionApprox.Z);
		return true;
	}

	private void AjusterJoueurPositionRestaureeSurSurfaceProche()
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return;
		Vector3 avant = _joueur.GlobalPosition;
		if (!EssayerAjusterPiedsJoueurSurSurfaceProche(avant, out Vector3 apres))
			return;
		_joueur.GlobalPosition = apres;
		_joueur.Velocity = Vector3.Zero;
		GD.Print($"ZERO-K : Position restaurée ajustée sur surface proche {avant} -> {apres}");
	}

	private bool EssayerTrouverSolParRaycastCourt(Vector3 positionApprox, float hauteurAuDessus, float profondeurMax, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null)
			return false;
		Vector3 debut = positionApprox + Vector3.Up * hauteurAuDessus;
		Vector3 fin = positionApprox - Vector3.Up * profondeurMax;
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollisionMask = 1;
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		if (_joueur != null && _joueur.GetRid().IsValid)
			query.Exclude = new Godot.Collections.Array<Rid> { _joueur.GetRid() };
		Godot.Collections.Array<Rid> excludes = query.Exclude ?? new Godot.Collections.Array<Rid>();
		const int maxEssais = 8;
		for (int essai = 0; essai < maxEssais; essai++)
		{
			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0 || !hit.ContainsKey("position"))
				return false;
			if (!EstImpactToitChaume(hit))
			{
				pointSol = (Vector3)hit["position"];
				return true;
			}
			if (hit.ContainsKey("rid"))
			{
				excludes.Add((Rid)hit["rid"]);
				query.Exclude = excludes;
			}
		}
		return false;
	}

	/// <summary>Raycast vertical vers le terrain/collision du monde. Retourne true si un point sol est trouvé.</summary>
	private bool EssayerTrouverSolParRaycast(Vector3 positionApprox, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;
		Vector3 debut = positionApprox + Vector3.Up * 900f;
		Vector3 fin = positionApprox + Vector3.Down * 900f;
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollisionMask = 1;
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		if (_joueur != null && _joueur.GetRid().IsValid)
		{
			var excludes = new Godot.Collections.Array<Rid> { _joueur.GetRid() };
			query.Exclude = excludes;
		}
		Godot.Collections.Array<Rid> excludesRay = query.Exclude ?? new Godot.Collections.Array<Rid>();
		const int maxEssais = 10;
		for (int essai = 0; essai < maxEssais; essai++)
		{
			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0 || !hit.ContainsKey("position"))
				return false;
			if (!EstImpactToitChaume(hit))
			{
				pointSol = (Vector3)hit["position"];
				return true;
			}
			if (hit.ContainsKey("rid"))
			{
				excludesRay.Add((Rid)hit["rid"]);
				query.Exclude = excludesRay;
			}
		}
		return false;
	}

	private static bool EstImpactToitChaume(Godot.Collections.Dictionary hit)
	{
		if (hit == null || !hit.ContainsKey("collider"))
			return false;
		GodotObject obj = hit["collider"].AsGodotObject();
		if (obj is not Node n)
			return false;
		for (Node cur = n; cur != null; cur = cur.GetParent())
		{
			if (cur is ItemPhysique ip && ip.IsInGroup("BlocsPoses"))
				return ip.ID_Objet == Joueur.IdObjetToitChaume;
		}
		return false;
	}

	/// <summary>
	/// Finalise le spawn du nouveau monde : la map/collision doit être prête, puis raycast vertical au point de spawn.
	/// Tant que le raycast n'a pas de hit valide, on ne finalise PAS (évite spawn sous la map).
	/// </summary>
	/// <summary>Après mort : nouveau personnage à la dernière position connue (même hauteur), sinon spawn seed.</summary>
	public void RepositionnerJoueurApresMortNouveauPersonnage()
	{
		if (_joueur == null)
			return;
		Vector3 posSpawn = Vector3.Zero;
		int dimMort = _dimensionLocaleActive;
		bool poseMortConnue = GameState.Instance != null
			&& GameState.Instance.EssayerChargerDernierePoseMort(out dimMort, out posSpawn);
		if (poseMortConnue && _serveurParDimension.ContainsKey(dimMort) && dimMort != _dimensionLocaleActive)
			_dimensionCoordinator.AppliquerChangementDimensionLocale(dimMort, posSpawn, "Retour au lieu du décès.", rechargerPersistanceDimension: false);
		if (!poseMortConnue)
		{
			posSpawn = CalculerSpawnInitialDepuisSeed();
			posSpawn = AssurerSpawnAuDessusDuSol(posSpawn);
			_spawnDoitEtreAligneAuSol = true;
			_spawnAligneAuSol = false;
		}
		else
		{
			posSpawn = AssurerSpawnAuDessusDuSol(posSpawn, conserverHauteurSauvegardee: true);
			_spawnDoitEtreAligneAuSol = false;
			_spawnAligneAuSol = true;
			_ajusterPiedsJoueurSurSurfaceApresRestauration = true;
		}
		_spawnInitialEnAttente = posSpawn;
		_joueur.GlobalPosition = posSpawn;
		_joueur.Velocity = Vector3.Zero;
		if (_spawnDoitEtreAligneAuSol)
		{
			_joueur.Visible = false;
			if (!FinaliserSpawnInitialAuSol(autoriserFallbackSansRaycast: true) && _joueur is Joueur jo)
				jo.Visible = true;
		}
		else
		{
			_joueur.Visible = true;
			Callable.From(AjusterJoueurPositionRestaureeSurSurfaceProche).CallDeferred();
		}
	}

	private bool FinaliserSpawnInitialAuSol(bool autoriserFallbackSansRaycast = false)
	{
		if (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol || _joueur == null) return true;

		if (EssayerTrouverSolParRaycast(_spawnInitialEnAttente, out Vector3 pointSol))
		{
			Vector3 posFinale = _spawnInitialEnAttente;
			if (_joueur is Joueur jo)
				posFinale.Y = jo.CalculerYOriginePourPiedsSurSurface(pointSol.Y);
			else
				posFinale = pointSol + Vector3.Up * 1.2f;

			_joueur.GlobalPosition = posFinale;
			_joueur.Velocity = Vector3.Zero;
			_joueur.Visible = true;
			_spawnAligneAuSol = true;
			GD.Print($"ZERO-K : Spawn finalisé au sol (raycast) -> {posFinale}");
			return true;
		}

		if (!autoriserFallbackSansRaycast)
			return false;

		// Fallback ultime (timeout long uniquement).
		Vector3 posFallback = AssurerSpawnAuDessusDuSol(_spawnInitialEnAttente);
		_joueur.GlobalPosition = posFallback;
		_joueur.Velocity = Vector3.Zero;
		_joueur.Visible = true;
		_spawnAligneAuSol = true;
		GD.PrintErr($"ZERO-K : Spawn finalisé en fallback sans raycast -> {posFallback}");
		return true;
	}

	private void ActiverEauLegacy(Vector3I pos) { if (_eauActive.Add(pos)) _fileEau.Enqueue(pos); }

	private bool PeutCoulerVersLegacy(Vector3I source, Vector3I destination)
	{
		if (!_antiRetourEauLegacy.TryGetValue(source, out var blocage)) return true;
		if (blocage.tickExpiration <= _tickEauLegacy)
		{
			_antiRetourEauLegacy.Remove(source);
			return true;
		}
		return blocage.retourInterdit != destination;
	}

	private void MemoriserFluxEauLegacy(Vector3I source, Vector3I destination)
	{
		// Évite le va-et-vient immédiat destination -> source.
		_antiRetourEauLegacy[destination] = (source, _tickEauLegacy + DureeBlocageRetourEauLegacyTicks);
		if (_antiRetourEauLegacy.Count > 20000)
			_antiRetourEauLegacy.Clear();
	}

	private void ReveillerEauAdjacenteLegacy(Vector3 pointGlobal)
	{
		int gx = Mathf.FloorToInt(pointGlobal.X), gy = Mathf.FloorToInt(pointGlobal.Y), gz = Mathf.FloorToInt(pointGlobal.Z);
		var basePos = new Vector3I(gx, gy, gz);
		foreach (var d in DirReveilEau)
			if (EstVoxelEauLegacy(basePos + d)) ActiverEauLegacy(basePos + d);
	}

	private (Generateur_Voxel chunk, Vector3I local)? ObtenirChunkEtLocalLegacy(Vector3I pos)
	{
		if (pos.Y < 0 || pos.Y > HauteurMax) return null;
		Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		if (!_chunks.TryGetValue(c, out var n)) return null;
		if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk) return null;
		var ch = n as Generateur_Voxel;
		return ch != null ? (ch, new Vector3I(lx, pos.Y, lz)) : null;
	}
	private bool EstVoxelEauLegacy(Vector3I pos)
	{
		var r = ObtenirChunkEtLocalLegacy(pos);
		return r.HasValue && r.Value.chunk.EstVoxelEau(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	private bool EstVoxelAirLegacy(Vector3I pos)
	{
		var r = ObtenirChunkEtLocalLegacy(pos);
		return r.HasValue && r.Value.chunk.EstVoxelAir(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	private void DefinirVoxelLegacy(Vector3I pos, byte id)
	{
		var r = ObtenirChunkEtLocalLegacy(pos);
		if (!r.HasValue) return;
		if (id == 4) r.Value.chunk.DefinirVoxelEau(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
		else if (id == 0) r.Value.chunk.DefinirVoxelAir(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	private void DemanderMiseAJourMeshLegacy(Vector3I pos)
	{
		var r = ObtenirChunkEtLocalLegacy(pos);
		if (!r.HasValue) return;
		r.Value.chunk.ActualiserMesh();
		Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		if (lx == 0 && _chunks.TryGetValue(new Vector2I(c.X - 1, c.Y), out var vx)) (vx as Generateur_Voxel)?.ActualiserMesh();
		if (lx == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(c.X + 1, c.Y), out var vxp)) (vxp as Generateur_Voxel)?.ActualiserMesh();
		if (lz == 0 && _chunks.TryGetValue(new Vector2I(c.X, c.Y - 1), out var vz)) (vz as Generateur_Voxel)?.ActualiserMesh();
		if (lz == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(c.X, c.Y + 1), out var vzp)) (vzp as Generateur_Voxel)?.ActualiserMesh();
	}

	/// <summary>
	/// Crée HUD_Inventaire (mains) et HUD_Carnet (slot carnet) : chemins attendus par le joueur et Monde_Client.
	/// Doit être appelé avant l’ajout de <see cref="Monde_Client"/> (son <c>_Ready</c> résout les panels).
	/// </summary>
	private void AssurerCalquesHudInventaireEtCarnet()
	{
		if (GetNodeOrNull("HUD_Inventaire") != null)
			return;

		var hudInv = new CanvasLayer { Name = "HUD_Inventaire", Layer = 15 };
		var conteneur = new Control
		{
			Name = "Conteneur_Ancrage",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		conteneur.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		conteneur.OffsetRight = 0f;
		conteneur.OffsetBottom = 0f;

		var boite = new HBoxContainer
		{
			Name = "Boite_Slots",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		boite.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		boite.OffsetLeft = -200f;
		boite.OffsetRight = 200f;
		boite.OffsetTop = -120f;
		boite.OffsetBottom = -20f;
		boite.AddThemeConstantOverride("separation", 28);

		var slotG = new Panel
		{
			Name = "Slot_Main_Gauche",
			CustomMinimumSize = new Vector2(72f, 72f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		var slotD = new Panel
		{
			Name = "Slot_Main_Droite",
			CustomMinimumSize = new Vector2(72f, 72f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		boite.AddChild(slotG);
		boite.AddChild(slotD);
		conteneur.AddChild(boite);
		hudInv.AddChild(conteneur);
		AddChild(hudInv);

		var hudCarnet = new CanvasLayer { Name = "HUD_Carnet", Layer = 16 };
		var slotCarnet = new Panel
		{
			Name = "Slot_Carnet_Savoir",
			CustomMinimumSize = new Vector2(64f, 64f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		slotCarnet.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		slotCarnet.OffsetLeft = -88f;
		slotCarnet.OffsetRight = -16f;
		slotCarnet.OffsetTop = 52f;
		slotCarnet.OffsetBottom = 124f;
		hudCarnet.AddChild(slotCarnet);
		AddChild(hudCarnet);
	}

	private void RestaurerEtatPersistantMonde()
	{
		if (_restaurationPersistantPhaseJoueurFaite) return;
		_restaurationPersistantPhaseJoueurFaite = true;
		if (_joueur is Joueur j)
			j.ChargerEtatPersistantPhaseJoueur();
	}

	/// <summary>Phase B : objets posés / blocs persistants / faune — après collision minimale au spawn (évite chute dans le vide).</summary>
	/// <returns>True si la restauration vient d’être exécutée cette frame (première fois).</returns>
	private bool EssayerRestaurerObjetsPersistantsPhaseSol(bool spawnPretEtAligne)
	{
		if (_restaurationPersistantObjetsSolFaite) return false;
		if (!spawnPretEtAligne || _joueur is not Joueur j) return false;
		j.ChargerEtatPersistantPhaseObjetsAuSolEtFaune();
		j.PlanifierRecalculAssemblageToitsChaume();
		_restaurationPersistantObjetsSolFaite = true;
		return true;
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel"))
			return;
		if (_pauseVisible && _panelGraphismes != null && _panelGraphismes.Visible)
		{
			_panelGraphismes.Visible = false;
			_editionGraphiqueEnDirect = false;
			ForcerCycleSolaireActif();
			RafraichirIndicateurModeEditionGraphique();
			if (_panelPause != null)
				_panelPause.Visible = true;
			GetTree().Paused = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetViewport().SetInputAsHandled();
			return;
		}
		if (_joueur is Joueur jo && jo.FermerUIJoueurSiOuverte())
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		ToggleMenuPause();
		GetViewport().SetInputAsHandled();
	}

	/// <summary>Ouvre le menu pause et met le jeu en pause, sans fermer l’inventaire (Échap pendant Q).</summary>
	public void ForcerOuvertureMenuPause()
	{
		if (_panelPause == null) CreerMenuPause();
		if (_pauseVisible) return;
		_pauseVisible = true;
		_panelPause.Visible = true;
		if (_panelGraphismes != null)
			_panelGraphismes.Visible = false;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Notification(int what)
	{
		if (!Engine.IsEditorHint() && what == Node.NotificationWMCloseRequest)
			SauvegarderManuelDepuisMenu("WMCloseRequest");
		base._Notification(what);
	}

	/// <summary>Filet avant destruction de la scène (BlocsPoses encore dans l’arbre ; le <see cref="Joueur._ExitTree"/> ne sauve pas les objets posés).</summary>
	private void EssayerSauvegardeCompleteAvantSortieScene()
	{
		if (_joueur is not Joueur j || !IsInsideTree())
			return;
		if (!_restaurationPersistantPhaseJoueurFaite || !_restaurationPersistantObjetsSolFaite)
			return;
		j.SauvegarderEtatPersistantMonde(GetTree());
		GD.Print("ZERO-K : Sauvegarde monde avant sortie de scène (objets posés toutes dimensions).");
	}

	public override void _ExitTree()
	{
		if (_meshWarmupShaders != null && GodotObject.IsInstanceValid(_meshWarmupShaders))
			_meshWarmupShaders.QueueFree();
		_meshWarmupShaders = null;
		_fileWarmupMateriaux.Clear();
		foreach (var kv in _effetsRemousParCorps)
			if (kv.Value != null && GodotObject.IsInstanceValid(kv.Value))
				kv.Value.QueueFree();
		_effetsRemousParCorps.Clear();
		_corpsSuiviRemous.Clear();
		_corpsDansOcean.Clear();
		// Inventaire / objets : Joueur._ExitTree si encore dans l’arbre ; chunks pour toutes les dimensions ici.
		if (IsInsideTree())
			SauvegarderChunksVoxelToutesDimensions("Gestionnaire_Monde._ExitTree");
		else if (UseArchitectureReseau)
		{
			foreach (var kv in _serveurParDimension)
				kv.Value?.SauvegarderCritiqueAvantSortie("Gestionnaire_Monde._ExitTree");
		}
		else
		{
			foreach (var kv in _chunks)
				(kv.Value as Generateur_Voxel)?.Sauvegarder(kv.Key);
		}
		BoeufSauvage.ViderCachesBibliothequesExternesPourDechargementMonde();
		base._ExitTree();
	}

	private Panel _panelPause;
	private Panel _panelGraphismes;
	private bool _pauseVisible;
	private OptionButton _optionPresetGraphique;
	private HSlider _sliderRenderDistance;
	private Label _labelRenderDistanceValeur;
	private HSlider _sliderRayonQualiteProche;
	private Label _labelRayonQualiteProcheValeur;
	private HSlider _sliderDetailChunks;
	private Label _labelDetailChunksValeur;
	private HSlider _sliderRayonGazon;
	private Label _labelRayonGazonValeur;
	private HSlider _sliderRayonBuissons;
	private Label _labelRayonBuissonsValeur;
	private HSlider _sliderRayonHorizon;
	private Label _labelRayonHorizonValeur;
	private HSlider _sliderPasHorizon;
	private Label _labelPasHorizonValeur;
	private HSlider _sliderAngleCulling;
	private Label _labelAngleCullingValeur;
	private HSlider _sliderMargeToujoursVisible;
	private Label _labelMargeToujoursVisibleValeur;
	private HSlider _sliderMaxChunksFrame;
	private Label _labelMaxChunksFrameValeur;
	private HSlider _sliderLodEtapes;
	private Label _labelLodEtapesValeur;
	private CheckBox _checkActiverHorizon;
	private CheckBox _checkActiverCulling;
	private CheckBox _checkLodUltraSmooth;
	private CheckBox _checkModeSurvieAgressif;
	private Label _labelModeEditionGraphique;
	private Label _labelAutoHybride;
	private GraphicsOptionsData _optionsGraphiquesActuelles;
	private GraphicsOptionsData _optionsGraphiquesDefautProjet;
	private bool _optionsGraphiquesChargeesUtilisateur;
	private bool _synchronisationUiGraphiqueEnCours;
	private bool _editionGraphiqueEnDirect;
	private bool _verrouProfilMaterielUtilisateur;
	private bool _autoHybrideActif;
	private float _timerSessionAutoHybride;
	private float _timerAjustementAutoHybride;
	private float _fpsMinSessionAutoHybride = float.MaxValue;

	private void LancerSynchronisationDisquePostRestaurationSolDifferee()
	{
		_ = ExecuterSynchronisationDisquePostRestaurationApresDelaiAsync();
	}

	private async Task ExecuterSynchronisationDisquePostRestaurationApresDelaiAsync()
	{
		for (int i = 0; i < 2; i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInsideTree()) return;
		}
		SauvegarderPersistanceCompleteMonde("post-restauration", sauverObjetsPoses: false);
		GD.Print("ZERO-K : Synchronisation disque post-restauration (chunks uniquement — objets posés déjà chargés depuis le fichier).");
	}

	/// <summary>Même logique que le bouton Sauvegarder du menu pause et de l’inventaire (position + monde / chunks).</summary>
	/// <summary>Grave le terrain modifié pour toutes les dimensions (ARAPA, APISARA, PETA, OMEGA, DERATA).</summary>
	public void SauvegarderChunksVoxelToutesDimensions(string contexte = "persist")
	{
		if (UseArchitectureReseau)
		{
			foreach (var info in ConstantesDimensions.ToutesAvecPersistanceModifications())
			{
				if (!_serveurParDimension.TryGetValue(info.Id, out Monde_Serveur serveur) || serveur == null)
					continue;
				serveur.SauvegarderCritiqueAvantSortie($"{contexte}/{info.NomCanonique}");
			}
			return;
		}
		foreach (var kv in _chunks)
			(kv.Value as Generateur_Voxel)?.Sauvegarder(kv.Key);
	}

	/// <summary>Constructions, objets, faune et chunks pour chaque dimension (génération APISARA distincte, modifications persistées).</summary>
	public void SauvegarderPersistanceCompleteMonde(string contexte = "persist", bool sauverObjetsPoses = true)
	{
		if (_joueur != null)
		{
			GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
			SauvegarderSessionJoueur(_dimensionLocaleActive, _joueur.GlobalPosition);
		}
		if (_joueur is Joueur j)
		{
			if (_restaurationPersistantPhaseJoueurFaite)
			{
				if (_restaurationPersistantObjetsSolFaite && sauverObjetsPoses)
					j.SauvegarderEtatPersistantMonde(GetTree());
				else if (_restaurationPersistantObjetsSolFaite && !sauverObjetsPoses)
					j.SauvegarderEtatPersistantJoueurSeulement();
				else
					j.SauvegarderEtatPersistantJoueurSeulement();
			}
			else
				GD.Print("ZERO-K : Sauvegarde joueur différée (phase restauration joueur pas encore exécutée).");
		}
		SauvegarderChunksVoxelToutesDimensions(contexte);
		GD.Print($"ZERO-K : Persistance complète monde ({contexte}).");
	}

	public void SauvegarderManuelDepuisMenu(string contexte = "menu")
	{
		SauvegarderPersistanceCompleteMonde($"menu:{contexte}");
	}

	/// <summary>Évite <c>Contains("1060")</c> qui peut matcher d’autres GPU (ex. chaînes contenant « 1060 » hors GTX 1060).</summary>
	private static bool GpuSembleEtreGeforceGtx1060(string nomGpuLower)
	{
		if (string.IsNullOrEmpty(nomGpuLower)) return false;
		return nomGpuLower.Contains("gtx 1060")
			|| nomGpuLower.Contains("geforce gtx 1060")
			|| nomGpuLower.Contains("1060 3gb")
			|| nomGpuLower.Contains("1060 6gb");
	}

	private void DetecterProfilMaterielEtAjuster()
	{
		if (_verrouProfilMaterielUtilisateur)
			return;
		if (!ActiverProfilMaterielAuto && !ForcerProfilGTX1060i710700F)
			return;
		MettreAJourInfosMaterielDetecte();
		if (_optionsGraphiquesChargeesUtilisateur)
			return;
		bool profilCible = ForcerProfilGTX1060i710700F
			|| ((_nomCpuDetecte.Contains("i7-10700f") || _nomCpuDetecte.Contains("10700f"))
				&& GpuSembleEtreGeforceGtx1060(_nomGpuDetecte));
		if (!profilCible)
			return;
		// Profil conservateur pour éviter les rafales CPU sur ce couple CPU/GPU.
		MaxChunksParFrame = Mathf.Min(MaxChunksParFrame, 10);
		BudgetDormanceObjetsParCycle = Mathf.Min(BudgetDormanceObjetsParCycle, 84);
	}

	private void ConfigurerProfilMondeClientSelonMateriel()
	{
		if (_mondeClient == null) return;
		if (_verrouProfilMaterielUtilisateur) return;
		if (_optionsGraphiquesChargeesUtilisateur) return;
		bool profilCible = ForcerProfilGTX1060i710700F
			|| ((_nomCpuDetecte.Contains("10700f")) && GpuSembleEtreGeforceGtx1060(_nomGpuDetecte));
		if (!profilCible)
			return;
		_mondeClient.FpsCibleAutoDiagnostic = 60;
		_mondeClient.ModeSurvieFpsAgressif = true;
		_mondeClient.SeuilFpsUrgenceForte = Mathf.Min(_mondeClient.SeuilFpsUrgenceForte, 45);
		_mondeClient.SeuilFpsUrgenceCritique = Mathf.Min(_mondeClient.SeuilFpsUrgenceCritique, 33);
		_mondeClient.SeuilFpsUrgenceExtreme = Mathf.Min(_mondeClient.SeuilFpsUrgenceExtreme, 26);
		_mondeClient.SeuilFpsSortieUrgenceExtreme = Mathf.Max(_mondeClient.SeuilFpsSortieUrgenceExtreme, 56);
		_mondeClient.MaxLancementsTravailleursParTick = Mathf.Min(_mondeClient.MaxLancementsTravailleursParTick, 1);
		_mondeClient.BudgetFrameCibleMs = Mathf.Min(_mondeClient.BudgetFrameCibleMs, 16.2f);
		_mondeClient.SeuilFpsGateStrict = Mathf.Min(_mondeClient.SeuilFpsGateStrict, 40f);
		_mondeClient.SeuilFpsGateReprise = Mathf.Min(_mondeClient.SeuilFpsGateReprise, 52f);
		_mondeClient.DureeStabiliteReprise = Mathf.Min(_mondeClient.DureeStabiliteReprise, 0.20f);
		_mondeClient.DureeRampUpPostDegel = Mathf.Min(_mondeClient.DureeRampUpPostDegel, 0.55f);
		_mondeClient.DureeMinEtatGeleSec = Mathf.Min(_mondeClient.DureeMinEtatGeleSec, 0.15f);
		_mondeClient.DureeMinEtatOuvertSec = Mathf.Max(_mondeClient.DureeMinEtatOuvertSec, 0.45f);
		_mondeClient.MaxChunksEvaluesCullingParPasse = Mathf.Min(_mondeClient.MaxChunksEvaluesCullingParPasse, 180);
		_mondeClient.MaxBasculesCullingParPasse = Mathf.Min(_mondeClient.MaxBasculesCullingParPasse, 72);
	}

	private void InitialiserWarmupShadersProgressif()
	{
		if (!ActiverWarmupShadersProgressif || !UseArchitectureReseau)
			return;
		_fileWarmupMateriaux.Clear();
		if (MaterielTerrain != null) _fileWarmupMateriaux.Add(MaterielTerrain);
		if (MaterielEau != null) _fileWarmupMateriaux.Add(MaterielEau);
		if (_fileWarmupMateriaux.Count == 0)
			return;
		if (_meshWarmupShaders == null || !GodotObject.IsInstanceValid(_meshWarmupShaders))
		{
			_meshWarmupShaders = new MeshInstance3D
			{
				Name = "WarmupShadersRuntime",
				Mesh = new QuadMesh { Size = new Vector2(0.04f, 0.04f) },
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Visible = true
			};
			AddChild(_meshWarmupShaders);
		}
		_meshWarmupShaders.GlobalPosition = ObtenirPositionJoueurOuSpawn() + new Vector3(0f, -0.4f, 0f);
		_meshWarmupShaders.Scale = new Vector3(0.001f, 0.001f, 0.001f);
		_indexWarmupMateriau = 0;
		_cooldownWarmupShaders = 0f;
	}

	private void TraiterWarmupShadersProgressif(float dt)
	{
		if (!ActiverWarmupShadersProgressif || _fileWarmupMateriaux.Count == 0 || _meshWarmupShaders == null || !GodotObject.IsInstanceValid(_meshWarmupShaders))
			return;
		_cooldownWarmupShaders -= dt;
		if (_cooldownWarmupShaders > 0f)
			return;
		_cooldownWarmupShaders = Mathf.Max(0.02f, IntervalleWarmupShadersSec);
		int budget = Mathf.Clamp(WarmupMateriauxParFrame, 1, 8);
		for (int i = 0; i < budget && _indexWarmupMateriau < _fileWarmupMateriaux.Count; i++)
		{
			Material mat = _fileWarmupMateriaux[_indexWarmupMateriau++];
			if (mat == null) continue;
			_meshWarmupShaders.MaterialOverride = mat;
		}
		if (_indexWarmupMateriau >= _fileWarmupMateriaux.Count)
		{
			_fileWarmupMateriaux.Clear();
			if (_meshWarmupShaders != null && GodotObject.IsInstanceValid(_meshWarmupShaders))
				_meshWarmupShaders.QueueFree();
			_meshWarmupShaders = null;
		}
	}

	private void RafraichirCacheDormanceGroupes(float dt, bool force = false)
	{
		if (!force)
		{
			_cooldownRefreshCacheDormance -= dt;
			if (_cooldownRefreshCacheDormance > 0f)
				return;
		}
		_cooldownRefreshCacheDormance = Mathf.Max(0.2f, IntervalleRefreshCacheDormanceSec);
		void Remplir(string nomGroupe)
		{
			if (!_cacheRigidBodiesDormance.TryGetValue(nomGroupe, out List<RigidBody3D> liste))
			{
				liste = new List<RigidBody3D>(256);
				_cacheRigidBodiesDormance[nomGroupe] = liste;
			}
			liste.Clear();
			var noeuds = GetTree().GetNodesInGroup(nomGroupe);
			for (int i = 0; i < noeuds.Count; i++)
			{
				if (noeuds[i] is RigidBody3D rb && rb.IsInsideTree() && GodotObject.IsInstanceValid(rb))
					liste.Add(rb);
			}
		}
		Remplir("BlocsPoses");
		Remplir("ObjetsDormantsDynamiques");
	}

	private void SurveillerDeriveRuntime(float dt)
	{
		if (!ActiverSurveillanceOrphans)
			return;
		_cooldownSurveillanceOrphans -= dt;
		if (_cooldownSurveillanceOrphans > 0f)
			return;
		_cooldownSurveillanceOrphans = Mathf.Max(0.2f, IntervalleSurveillanceOrphansSec);
		int orphanNodes = (int)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
		if (_dernierOrphanNodes >= 0 && orphanNodes > _dernierOrphanNodes + 96)
		{
			GD.PrintErr($"ZERO-K PERF: hausse orphans détectée {_dernierOrphanNodes} -> {orphanNodes}. Vérifier créations temporaires non libérées.");
		}
		_dernierOrphanNodes = orphanNodes;
	}

	private void MettreAJourEtatCycleSolaire(bool chargementActif)
	{
		if (_chargementCycleSolaire == chargementActif) return;
		_chargementCycleSolaire = chargementActif;
		var cycle = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		cycle?.DefinirChargementMondeActif(chargementActif);
	}

	private void DemarrerLegacy()
	{
		_sceneChunk = GD.Load<PackedScene>("res://Generateur_Voxel.tscn");
		ActualiserVisibiliteEtTriChunksLegacy();
	}

	public void AppliquerDestructionGlobale(Vector3 pointImpact, float rayon, float forceDegats = 5.0f)
	{
		if (UseArchitectureReseau)
			_mondeClient?.AppliquerDestructionGlobale(pointImpact, rayon, forceDegats);
		else
		{
			foreach (var kv in _chunks)
			{
				var g = kv.Value as Generateur_Voxel;
				g?.DetruireVoxel(pointImpact, rayon, forceDegats);
			}
		}
	}

	public void AppliquerCreationGlobale(Vector3 pointImpact, Vector3 normale, float rayon, int idMatiere = 1)
	{
		if (UseArchitectureReseau)
			_mondeClient?.AppliquerCreationGlobale(pointImpact, normale, rayon, idMatiere);
		else
		{
			Vector3 cible = pointImpact + (normale * 1.5f);
			foreach (var kv in _chunks)
			{
				var g = kv.Value as Generateur_Voxel;
				g?.CreerMatiere(cible, rayon, idMatiere);
			}
		}
	}

	/// <summary>Relaie vers <see cref="Monde_Serveur.AppliquerFauchageGlobal"/> : chaque <see cref="Chunk_Serveur.FaucherFlore"/> retire la flore dans le rayon, appelle le callback pour spawner fibres (15) / buissons, puis notifie le client pour les meshes gazon.</summary>
	public void AppliquerFauchageGlobal(Vector3 pointImpact, float rayon)
	{
		if (UseArchitectureReseau)
			_mondeServeur?.AppliquerFauchageGlobal(pointImpact, rayon);
		else
			GD.Print("ZERO-K : Le fauchage (gazon, fibres) n'existe qu'en mode chunks serveur. Réactivez UseArchitectureReseau sur le Gestionnaire_Monde.");
	}

	/// <summary>Fauchage faune: retire le gazon dans le rayon sans creer de loot au sol. Retourne vrai si au moins un mesh de flore a ete supprime.</summary>
	public bool AppliquerFauchageFauneGlobal(Vector3 pointImpact, float rayon)
	{
		if (UseArchitectureReseau)
			return _mondeServeur?.AppliquerFauchageFauneGlobal(pointImpact, rayon) ?? false;
		return false;
	}

	/// <summary>Retourne vrai si un mesh de gazon 3D existe dans le rayon autour du point.</summary>
	public bool ExisteGazonFauneGlobal(Vector3 pointImpact, float rayon)
	{
		if (UseArchitectureReseau)
			return _mondeServeur?.ExisteGazonFauneGlobal(pointImpact, rayon) ?? false;
		return false;
	}

	/// <summary>Récolte ciblée d’un buisson : 0=hachette (branche), 1=dague (coupe), 2=pelle (déracinage replantable), 3=dague aloe (sans branche).</summary>
	public bool RecolterBuissonGlobal(Vector3 pointImpact, float rayon, byte modeRecolte)
	{
		if (UseArchitectureReseau && _mondeServeur != null)
			return _mondeServeur.RecolterBuissonGlobal(pointImpact, rayon, modeRecolte);
		return false;
	}

	/// <summary>Détecte un buisson sous la visée sans le modifier.</summary>
	public bool EssayerDetecterBuissonSousPoint(Vector3 pointImpact, float rayon, out Vector3 posBuisson, out byte typeFlore)
	{
		posBuisson = Vector3.Zero;
		typeFlore = 0;
		if (UseArchitectureReseau && _mondeServeur != null)
			return _mondeServeur.EssayerDetecterBuissonGlobal(pointImpact, rayon, out posBuisson, out typeFlore);
		return false;
	}

	/// <summary>Plante un buisson (type 1/2) au sol selon le point visé.</summary>
	public bool PlanterBuissonGlobal(Vector3 pointImpact, Vector3 normaleImpact, byte typeFlore)
	{
		if (!(UseArchitectureReseau && _mondeServeur != null)) return false;
		Vector3 cible = pointImpact + (normaleImpact * 0.08f);
		return _mondeServeur.PlanterBuissonGlobal(cible, typeFlore);
	}

	/// <summary>Récolte les baies d’un buisson plein sous la visée. Retourne la quantité et l’index couleur récoltés.</summary>
	public bool RecolterBaiesBuissonSousPoint(Vector3 pointImpact, float rayon, out int quantiteBaies, out byte indexCouleurBaie)
	{
		quantiteBaies = 0;
		indexCouleurBaie = 0;
		if (!(UseArchitectureReseau && _mondeServeur != null)) return false;
		return _mondeServeur.RecolterBaiesBuissonGlobal(pointImpact, rayon, out quantiteBaies, out indexCouleurBaie);
	}

	/// <summary>Oracle géologique : lecture directe de l'ADN (_materials) depuis le Serveur. Évite la dissonance visuelle (mine terre → reçoit pierre).</summary>
	public int ObtenirMatiereExacte(Vector3 positionGlobale)
	{
		if (UseArchitectureReseau && _mondeServeur != null)
			return _mondeServeur.ObtenirMatiereExacte(positionGlobale);
		return AnalyserMatiereAuPoint(positionGlobale, Vector3.Up);
	}

	/// <summary>True si l’eau voxel est présente près du point (bûche de chêne : flotter seulement ici, pas sur la terre ferme).</summary>
	public bool EstPointDansEau(Vector3 positionGlobale)
	{
		if (UseArchitectureReseau && _mondeServeur != null)
			return _mondeServeur.EstPointDansEau(positionGlobale);
		int gx = Mathf.FloorToInt(positionGlobale.X);
		int gy = Mathf.FloorToInt(positionGlobale.Y);
		int gz = Mathf.FloorToInt(positionGlobale.Z);
		for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
				for (int dz = -1; dz <= 1; dz++)
					if (EstVoxelEauLegacy(new Vector3I(gx + dx, gy + dy, gz + dz)))
						return true;
		return false;
	}

	/// <summary>True si le point touche l'eau par voisinage voxel ou matière eau exacte.</summary>
	public bool EstPointImmergeEau(Vector3 positionGlobale)
	{
		return EstPointDansEau(positionGlobale) || ObtenirMatiereExacte(positionGlobale) == 4;
	}

	/// <summary>Calcule le ratio d'immersion d'un corps via des offsets locaux (0..1).</summary>
	public float CalculerRatioImmersion(Vector3 origineGlobale, Vector3[] offsetsLocaux)
	{
		if (offsetsLocaux == null || offsetsLocaux.Length == 0)
			return 0f;
		int pointsImmerges = 0;
		for (int i = 0; i < offsetsLocaux.Length; i++)
		{
			if (EstPointImmergeEau(origineGlobale + offsetsLocaux[i]))
				pointsImmerges++;
		}
		return pointsImmerges / (float)offsetsLocaux.Length;
	}

	/// <summary>True si le ratio d'immersion est au moins égal au seuil donné.</summary>
	public bool EstMajoritairementImmerge(Vector3 origineGlobale, Vector3[] offsetsLocaux, float seuil = 0.5f)
	{
		return CalculerRatioImmersion(origineGlobale, offsetsLocaux) >= seuil;
	}

	/// <summary>Appelé quand un arbre est coupé : spawn branches et bûches qui tombent au sol.</summary>
	public void DemanderSpawnDebrisArbre(Vector3 baseArbre, int ageEnJours, uint seed)
	{
		_mondeServeur?.SpawnDebrisArbre(baseArbre, ageEnJours, seed);
	}

	/// <summary>Oracle géologique (legacy) : déduit l'ID depuis altitude/normale. Utiliser ObtenirMatiereExacte en mode réseau.</summary>
	public int AnalyserMatiereAuPoint(Vector3 positionGlobale, Vector3 normaleSurface)
	{
		float altitude = positionGlobale.Y;

		// Règle 1 : La pente absolue (La Roche) — mur vertical ou falaise
		if (normaleSurface.Y < 0.6f)
			return 2; // ID 2 = Roche

		// Règle 2 : Le niveau de la mer (Le Sable)
		const float NIVEAU_EAU = 103f; // Aligné avec NiveauPlage du terrain (+1 m)
		if (altitude < NIVEAU_EAU + 2.0f && altitude >= NIVEAU_EAU - 5.0f)
			return 3; // ID 3 = Sable

		// Règle 3 : Les hauts sommets (La Neige) — 245-255 (bruit)
		int bruit = (int)((positionGlobale.X * 73856093 + positionGlobale.Z * 19349663) % 37) - 18;
		float seuilNeige = 250f + bruit * 0.3f;
		if (altitude > seuilNeige)
			return 4; // ID 4 = Neige (atlas livre)

		// Par défaut : plat et altitude moyenne
		return 1; // ID 1 = Terre
	}

	// --- Legacy ---
	private bool FichierSauvegardeExiste(Vector2I coord)
		=> FileAccess.FileExists(Generateur_Voxel.ObtenirCheminChunk(coord));

	public async Task PreGenererMonde(int rayonChunks)
	{
		GD.Print($"DÉBUT BAKING : Rayon {rayonChunks} chunks...");
		for (int x = -rayonChunks; x <= rayonChunks; x++)
			for (int z = -rayonChunks; z <= rayonChunks; z++)
			{
				Vector2I coord = new Vector2I(x, z);
				if (FichierSauvegardeExiste(coord)) continue;
				var (d, m) = Generateur_Voxel.GenererDonneesVoxelBrut(coord, SeedTerrain, TailleChunk, HauteurMax);
				Generateur_Voxel.SauvegarderDonneesBrutes(coord, d, m, TailleChunk, HauteurMax);
				await Task.Delay(0);
			}
		GD.Print("FIN BAKING.");
	}

	private void ActualiserVisibiliteEtTriChunksLegacy()
	{
		if (_joueur == null) return;
		if (_radarLegacyEnCours) return;

		_radarLegacyEnCours = true;
		Vector2I chunkJoueur = ObtenirCoordonneesChunkJoueur();
		int cjX = chunkJoueur.X;
		int cjZ = chunkJoueur.Y;
		HashSet<Vector2I> chunksCharges = new HashSet<Vector2I>(_chunks.Keys);
		List<Vector2I> copieChunksACharger = new List<Vector2I>(_chunksACharger);

		Task.Run(() =>
		{
			HashSet<Vector2I> dejaVu = new HashSet<Vector2I>(copieChunksACharger);
			foreach (var c in chunksCharges) dejaVu.Add(c);

			for (int dx = -RenderDistance; dx <= RenderDistance; dx++)
				for (int dz = -RenderDistance; dz <= RenderDistance; dz++)
				{
					Vector2I coord = new Vector2I(cjX + dx, cjZ + dz);
					if (Mathf.Abs(coord.X) > RayonMondeChunks || Mathf.Abs(coord.Y) > RayonMondeChunks) continue;
					if (dejaVu.Add(coord))
						copieChunksACharger.Add(coord);
				}

			copieChunksACharger.Sort((a, b) => a.DistanceSquaredTo(chunkJoueur).CompareTo(b.DistanceSquaredTo(chunkJoueur)));

			Callable.From(() => AppliquerNouveauTriRadarLegacy(copieChunksACharger)).CallDeferred();
		});
	}

	private void AppliquerNouveauTriRadarLegacy(List<Vector2I> nouvelleListeTriee)
	{
		if (nouvelleListeTriee == null)
		{
			_chunksACharger.Clear();
			_radarLegacyEnCours = false;
			return;
		}
		_chunksACharger.Clear();
		_chunksACharger.AddRange(nouvelleListeTriee);
		_radarLegacyEnCours = false;

		Vector2I chunkJoueur = ObtenirCoordonneesChunkJoueur();
		int cjX = chunkJoueur.X;
		int cjZ = chunkJoueur.Y;
		var sup = new List<Vector2I>();
		foreach (var kv in _chunks)
		{
			if (Mathf.Abs(kv.Key.X - cjX) > RenderDistance || Mathf.Abs(kv.Key.Y - cjZ) > RenderDistance)
			{
				(kv.Value as Generateur_Voxel)?.Sauvegarder(kv.Key);
				kv.Value.QueueFree();
				sup.Add(kv.Key);
			}
		}
		foreach (var k in sup) _chunks.Remove(k);
	}

	private void LancerGenerationChunk(int cx, int cz)
	{
		if (!IsInsideTree()) return; // GARROT SPATIAL : pas d'ajout de chunk si l'arbre s'effondre.
		Vector2I coord = new Vector2I(cx, cz);
		if (_chunks.ContainsKey(coord)) return;
		var chunk = _sceneChunk.Instantiate<Node3D>();
		var g = chunk as Generateur_Voxel;
		g.SeedTerrain = SeedTerrain;
		g.ChunkOffsetX = coord.X;
		g.ChunkOffsetZ = coord.Y;
		chunk.Position = new Vector3(coord.X * TailleChunk, 0, coord.Y * TailleChunk);
		AddChild(chunk);
		g.DemarrerGenerationChunk(coord);
		_chunks[coord] = chunk;
	}
}
