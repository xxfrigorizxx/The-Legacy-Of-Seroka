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
			AppliquerChangementDimensionLocale(dimMort, posSpawn, "Retour au lieu du décès.", rechargerPersistanceDimension: false);
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

	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
			TreeExiting += EssayerSauvegardeCompleteAvantSortieScene;
		DirAccess.MakeDirRecursiveAbsolute("user://chunks");
		_joueur = GetParent().GetNode<CharacterBody3D>("Joueur");
		// F5 / lancement direct : GameState reste sur « MonMonde » par défaut alors que les sauvegardes sont dans le dernier monde du menu.
		GameState.Instance?.AppliquerDernierMondeJoueSiChargementDirectVersMondeZero();
		// Aligner la seed exportée de la scène sur le monde chargé (évite spawn / outils basés sur 19847 alors que le terrain utilise GameState).
		if (GameState.Instance != null)
			SeedTerrain = GameState.Instance.SeedTerrainActuel;
		// Dernier monde joué = celui dont on charge les sauvegardes (évite F5 / reprise sur le mauvais dossier).
		GameState.Instance?.PublierMondeActuelCommeDernierJoueSurDisque();
		AssurerCalquesHudInventaireEtCarnet();
		Chunk_Client.EchelleGazon = EchelleGazon;
		_optionsGraphiquesDefautProjet = CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise);
		ChargerOptionsGraphiquesAuDemarrage();

		// Affichage des coordonnées en haut au centre
		var canvas = new CanvasLayer { Layer = 10 };
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop, false);
		panel.OffsetLeft = -70;
		panel.OffsetTop = 8;
		panel.OffsetRight = 70;
		panel.OffsetBottom = 36;
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0, 0, 0, 0.6f);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(6);
		panel.AddThemeStyleboxOverride("panel", style);
		_labelCoords = new Label();
		_labelCoords.AddThemeFontSizeOverride("font_size", 14);
		_labelCoords.HorizontalAlignment = HorizontalAlignment.Center;
		panel.AddChild(_labelCoords);

		// Horloge dimension active en haut à droite (diagnostic temps 1:1 / fuseaux).
		var panelHeure = new PanelContainer();
		panelHeure.SetAnchorsPreset(Control.LayoutPreset.TopRight, false);
		panelHeure.OffsetLeft = -240f;
		panelHeure.OffsetTop = 8f;
		panelHeure.OffsetRight = -12f;
		panelHeure.OffsetBottom = 36f;
		var styleHeure = new StyleBoxFlat();
		styleHeure.BgColor = new Color(0f, 0f, 0f, 0.6f);
		styleHeure.SetCornerRadiusAll(4);
		styleHeure.SetContentMarginAll(6);
		panelHeure.AddThemeStyleboxOverride("panel", styleHeure);
		_labelHeureDimension = new Label();
		_labelHeureDimension.AddThemeFontSizeOverride("font_size", 14);
		_labelHeureDimension.HorizontalAlignment = HorizontalAlignment.Right;
		panelHeure.AddChild(_labelHeureDimension);
		AddChild(canvas);
		canvas.AddChild(panel);
		canvas.AddChild(panelHeure);
		CreerRepereCentreEcran();

		// Position : chargée si monde existant, sinon spawn par défaut (terrain généré → joueur déposé)
		Vector3 posSpawn = _joueur.GlobalPosition;
		int dimensionReconnexion = (int)DimensionJeu.Alpha;
		var sessionSauvegardee = ChargerSessionJoueur();
		if (sessionSauvegardee.HasValue)
		{
			dimensionReconnexion = sessionSauvegardee.Value.DimensionId;
			posSpawn = sessionSauvegardee.Value.Position;
			GD.Print($"ZERO-K : Session joueur restaurée dimension={dimensionReconnexion} pos={posSpawn}");
		}
		var posSauvegardee = GameState.Instance?.ObtenirPositionJoueurSauvegardee();
		bool positionPersistanteConnue = sessionSauvegardee.HasValue || posSauvegardee.HasValue;
		_spawnDoitEtreAligneAuSol = !positionPersistanteConnue && ForcerAlignementSolAuChargement;
		_spawnAligneAuSol = !_spawnDoitEtreAligneAuSol;
		_ajusterPiedsJoueurSurSurfaceApresRestauration = positionPersistanteConnue;
		if (sessionSauvegardee.HasValue)
		{
			GD.Print($"ZERO-K : Reconnexion joueur à {posSpawn} (dimension {dimensionReconnexion})");
		}
		else if (posSauvegardee.HasValue)
		{
			posSpawn = posSauvegardee.Value;
			GD.Print($"ZERO-K : Joueur reconnecté à {posSpawn}");
		}
		else
		{
			// Nouveau monde: spawn déterministe basé sur la seed (et pas uniquement la position fixe de la scène).
			double offsetLocal;
			double distanceHeures;
			dimensionReconnexion = SelectionnerDimensionInitialeParFuseauReel(out offsetLocal, out distanceHeures);
			_dimensionLocaleActive = dimensionReconnexion;
			DefinirDimensionPeer(Multiplayer.GetUniqueId(), _dimensionLocaleActive);
			string nomDimension = ConstantesDimensions.ObtenirNomCanonique(dimensionReconnexion);
			GD.Print($"ZERO-K : Spawn initial dimension={nomDimension} (id={dimensionReconnexion}) offsetLocal={offsetLocal:0.##}h ecart={distanceHeures:0.##}h");
			posSpawn = CalculerSpawnInitialDepuisSeed();
			GD.Print($"ZERO-K : Spawn initial seed={SeedTerrain} -> {posSpawn}");
		}
		posSpawn = AssurerSpawnAuDessusDuSol(posSpawn, conserverHauteurSauvegardee: positionPersistanteConnue);
		_joueur.GlobalPosition = posSpawn;
		_spawnInitialEnAttente = posSpawn;
		if (_spawnDoitEtreAligneAuSol)
			_joueur.Visible = false; // Apparaît seulement après alignement raycast sur le sol.

		if (UseArchitectureReseau)
		{
			if (sessionSauvegardee.HasValue)
				_dimensionLocaleActive = dimensionReconnexion;
			DemarrerArchitectureReseau();
			// Reconnexion : si la dernière dimension active n'est pas Alpha (déjà l'état par défaut au boot)
			// et qu'elle existe bien dans nos serveurs, on bascule dessus à la même position. Couvre Abysse + Beta/Omega/Delta.
			if (sessionSauvegardee.HasValue
				&& dimensionReconnexion != (int)DimensionJeu.Alpha
				&& _serveurParDimension.ContainsKey(dimensionReconnexion))
			{
				string nomCanonique = ConstantesDimensions.ObtenirNomCanonique(dimensionReconnexion);
				// Ne pas recharger placed_objects ici : Gestionnaire_Monde._Ready s'exécute avant Joueur._Ready,
				// le terrain n'est pas prêt, et RechargerEtatPersistantDimensionActive poserait _persistantObjetsSolCharges
				// ce qui empêche la phase B (EssayerRestaurerObjetsPersistantsPhaseSol) de respawner les constructions.
				AppliquerChangementDimensionLocale(dimensionReconnexion, posSpawn, $"Reconnexion en {nomCanonique}.", rechargerPersistanceDimension: false);
			}
		}
		else
		{
			DemarrerLegacy();
		}

		if (PreGenererAuDemarrage)
			_ = PreGenererMonde(RayonPreGeneration);

		CreerMenuPause();

		// Overlay "Chargement du monde..." — empêche de traverser le sol avant que la collision soit prête
		_overlayChargement = new CanvasLayer { Layer = 50 };
		var panelChargement = new PanelContainer();
		panelChargement.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var styleChargement = new StyleBoxFlat();
		styleChargement.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
		styleChargement.SetCornerRadiusAll(8);
		styleChargement.SetContentMarginAll(24);
		panelChargement.AddThemeStyleboxOverride("panel", styleChargement);
		var lblChargement = new Label { Text = "Chargement du monde...", HorizontalAlignment = HorizontalAlignment.Center };
		lblChargement.AddThemeFontSizeOverride("font_size", 22);
		_labelChargementPrincipal = lblChargement;
		panelChargement.AddChild(lblChargement);
		_overlayChargement.AddChild(panelChargement);
		AddChild(_overlayChargement);
		_secondesOverlayChargement = 0;
		CreerOverlayEmerukedesiParotaromaStage1();
		AssurerOverlayPortailTransition();

		// Forge automatique du matériau eau (bypass de l'éditeur) — sanctuarisation : le GC ne le détruira pas car lié au nœud.
		var shaderEau = GD.Load<Shader>("res://EauTriplanar.gdshader");
		if (shaderEau != null)
		{
			var matEau = new ShaderMaterial();
			matEau.Shader = shaderEau;
			matEau.SetShaderParameter("albedo_color", new Color(0.1f, 0.3f, 0.6f, 0.6f));
			MaterielEau = matEau;
		}
		if (UseArchitectureReseau)
			InitialiserWarmupShadersProgressif();

		CallDeferred(nameof(RestaurerEtatPersistantMonde));
	}

	private void CreerRepereCentreEcran()
	{
		if (_repereCentreLayer != null && GodotObject.IsInstanceValid(_repereCentreLayer)) return;

		_repereCentreLayer = new CanvasLayer { Layer = 12 };
		AddChild(_repereCentreLayer);

		var root = new Control
		{
			Name = "RepereCentre",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.SetAnchorsPreset(Control.LayoutPreset.Center);
		root.CustomMinimumSize = new Vector2(22, 22);
		root.Size = root.CustomMinimumSize;
		root.Position = -root.Size * 0.5f;
		_repereCentreLayer.AddChild(root);

		var h = new ColorRect
		{
			Name = "LigneHorizontale",
			Color = new Color(1f, 1f, 1f, 0.9f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		h.SetAnchorsPreset(Control.LayoutPreset.Center);
		h.CustomMinimumSize = new Vector2(18, 2);
		h.Size = h.CustomMinimumSize;
		h.Position = -h.Size * 0.5f;
		root.AddChild(h);

		var v = new ColorRect
		{
			Name = "LigneVerticale",
			Color = new Color(1f, 1f, 1f, 0.9f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		v.SetAnchorsPreset(Control.LayoutPreset.Center);
		v.CustomMinimumSize = new Vector2(2, 18);
		v.Size = v.CustomMinimumSize;
		v.Position = -v.Size * 0.5f;
		root.AddChild(v);
	}

	private void CreerOverlayEmerukedesiParotaromaStage1()
	{
		if (_overlayEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_overlayEmerukedesiParotaromaStage1))
			return;

		_overlayEmerukedesiParotaromaStage1 = new CanvasLayer { Name = "Overlay_EmerukedesiParotaroma_Stage1", Layer = 49 };
		var rect = new ColorRect
		{
			Name = "EmerukedesiParotaromaRect",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rect.OffsetLeft = 0f;
		rect.OffsetTop = 0f;
		rect.OffsetRight = 0f;
		rect.OffsetBottom = 0f;
		rect.Color = Colors.White;

		var shader = new Shader();
		shader.Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec2 uv = SCREEN_UV;
	vec4 base = texture(screen_tex, uv);
	vec2 p = SCREEN_PIXEL_SIZE * mix(0.0, 5.0, clamp(strength, 0.0, 1.0));

	vec4 blur = base * 0.30;
	blur += texture(screen_tex, uv + vec2( p.x,  0.0)) * 0.14;
	blur += texture(screen_tex, uv + vec2(-p.x,  0.0)) * 0.14;
	blur += texture(screen_tex, uv + vec2( 0.0,  p.y)) * 0.14;
	blur += texture(screen_tex, uv + vec2( 0.0, -p.y)) * 0.14;
	blur += texture(screen_tex, uv + vec2( p.x,  p.y)) * 0.07;
	blur += texture(screen_tex, uv + vec2(-p.x,  p.y)) * 0.07;

	COLOR = mix(base, blur, clamp(strength, 0.0, 1.0));
}";

		_materiauEmerukedesiParotaromaStage1 = new ShaderMaterial { Shader = shader };
		_materiauEmerukedesiParotaromaStage1.SetShaderParameter("strength", 0.0f);
		rect.Material = _materiauEmerukedesiParotaromaStage1;
		_overlayEmerukedesiParotaromaStage1.AddChild(rect);
		_overlayEmerukedesiParotaromaStage1.Visible = false;
		AddChild(_overlayEmerukedesiParotaromaStage1);
	}

	private void AssurerOverlayPortailTransition()
	{
		if (_overlayPortailTransition != null && GodotObject.IsInstanceValid(_overlayPortailTransition)) return;
		_overlayPortailTransition = new CanvasLayer { Name = "Overlay_Portail_Transition", Layer = 51 };
		_rectAssombrissementPortail = new ColorRect
		{
			Name = "RectAssombrissementPortail",
			MouseFilter = Control.MouseFilterEnum.Stop,
			Color = new Color(0f, 0f, 0f, 0f)
		};
		_rectAssombrissementPortail.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_rectAssombrissementPortail.OffsetLeft = 0f;
		_rectAssombrissementPortail.OffsetTop = 0f;
		_rectAssombrissementPortail.OffsetRight = 0f;
		_rectAssombrissementPortail.OffsetBottom = 0f;
		_overlayPortailTransition.AddChild(_rectAssombrissementPortail);
		_rectEffetVitessePortail = new ColorRect
		{
			Name = "RectEffetVitessePortail",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color = new Color(1f, 1f, 1f, 0f),
			Modulate = new Color(1f, 1f, 1f, 0f)
		};
		_rectEffetVitessePortail.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_rectEffetVitessePortail.OffsetLeft = 0f;
		_rectEffetVitessePortail.OffsetTop = 0f;
		_rectEffetVitessePortail.OffsetRight = 0f;
		_rectEffetVitessePortail.OffsetBottom = 0f;
		var shaderVitesse = new Shader();
		shaderVitesse.Code = @"
shader_type canvas_item;
uniform float warp_strength : hint_range(0.0, 1.0) = 0.0;
uniform float line_density : hint_range(40.0, 380.0) = 165.0;
uniform float speed : hint_range(4.0, 90.0) = 38.0;

float hash1(float x) { return fract(sin(x * 127.1) * 43758.5453); }

void fragment()
{
	vec2 uv = UV;
	float rows = max(12.0, line_density);
	float row = floor(uv.y * rows);
	float seed = hash1(row + floor(TIME * 4.0));
	float xCenter = fract(seed + TIME * speed * 0.045);
	float width = mix(0.0018, 0.020, warp_strength);
	float dist = abs(uv.x - xCenter);
	float line = smoothstep(width, 0.0, dist);
	float fadeEdges = smoothstep(0.05, 0.40, uv.x) * smoothstep(1.0, 0.72, uv.x);
	float sparkle = 0.55 + 0.45 * hash1(row * 0.91 + floor(TIME * 18.0));
	float alpha = line * fadeEdges * sparkle * warp_strength;
	COLOR = vec4(vec3(1.0), alpha);
}
";
		_materiauEffetVitessePortail = new ShaderMaterial { Shader = shaderVitesse };
		_materiauEffetVitessePortail.SetShaderParameter("warp_strength", 0.0f);
		_rectEffetVitessePortail.Material = _materiauEffetVitessePortail;
		_overlayPortailTransition.AddChild(_rectEffetVitessePortail);
		_overlayPortailTransition.Visible = false;
		AddChild(_overlayPortailTransition);
	}

	private void CalculerPhasesTransitionPortail(float dureeTotaleSec, out float fadeIn, out float phaseVitesse, out float fadeOut)
	{
		float d = Mathf.Max(0.35f, dureeTotaleSec);
		fadeIn = Mathf.Clamp(d * 0.30f, 0.22f, 1.0f);
		fadeOut = Mathf.Clamp(d * 0.26f, 0.20f, 0.85f);
		phaseVitesse = Mathf.Max(0.10f, d - fadeIn - fadeOut);
	}

	/// <summary>Transition immersive portail : noir progressif, lignes de vitesse blanches, puis éclaircissement.</summary>
	public void AfficherAssombrissementPortailTransition(float dureeTotaleSec)
	{
		AssurerOverlayPortailTransition();
		if (_rectAssombrissementPortail == null || _rectEffetVitessePortail == null) return;
		if (_tweenTransitionPortail != null && GodotObject.IsInstanceValid(_tweenTransitionPortail))
			_tweenTransitionPortail.Kill();
		_overlayPortailTransition.Visible = true;
		_rectAssombrissementPortail.Color = new Color(0f, 0f, 0f, 0f);
		_rectEffetVitessePortail.Modulate = new Color(1f, 1f, 1f, 0f);
		_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", 0.0f);
		CalculerPhasesTransitionPortail(dureeTotaleSec, out float fadeIn, out float phaseVitesse, out float fadeOut);
		float demiVitesse = Mathf.Max(0.05f, phaseVitesse * 0.5f);
		Tween tween = CreateTween();
		_tweenTransitionPortail = tween;
		tween.TweenProperty(_rectAssombrissementPortail, "color", new Color(0f, 0f, 0f, 0.98f), fadeIn);
		tween.Parallel().TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0.14f), fadeIn);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 0.0f, 0.50f, fadeIn);
		tween.TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0.48f), demiVitesse);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 0.50f, 1.00f, demiVitesse);
		tween.TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0.24f), demiVitesse);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 1.00f, 0.35f, demiVitesse);
		tween.TweenProperty(_rectAssombrissementPortail, "color", new Color(0f, 0f, 0f, 0f), fadeOut);
		tween.Parallel().TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0f), fadeOut);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 0.35f, 0.0f, fadeOut);
		tween.TweenCallback(Callable.From(() =>
		{
			if (_overlayPortailTransition != null && GodotObject.IsInstanceValid(_overlayPortailTransition))
				_overlayPortailTransition.Visible = false;
			if (_rectEffetVitessePortail != null && GodotObject.IsInstanceValid(_rectEffetVitessePortail))
				_rectEffetVitessePortail.Modulate = new Color(1f, 1f, 1f, 0f);
		}));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RpcRecevoirAssombrissementPortail(float dureeSec)
	{
		AfficherAssombrissementPortailTransition(dureeSec);
	}

	/// <summary>Serveur : envoie l’effet d’assombrissement au peer cible (et localement si c’est l’hôte).</summary>
	public void DiffuserAssombrissementPortailAuxClients(long peerId, float dureeSec)
	{
		if (Multiplayer.HasMultiplayerPeer())
		{
			if (!Multiplayer.IsServer()) return;
			// Godot interdit RpcId vers soi-même quand CallLocal=false.
			if (peerId != Multiplayer.GetUniqueId())
				RpcId((int)peerId, nameof(RpcRecevoirAssombrissementPortail), dureeSec);
			else
				AfficherAssombrissementPortailTransition(dureeSec);
		}
		else
			AfficherAssombrissementPortailTransition(dureeSec);
	}

	private void ReinitialiserEmerukedesiParotaromaStage1()
	{
		_dernierYRemonteeAbysse = float.NaN;
		_yDepartMonteeAbysse = float.NaN;
		_monteeAbysseContinue = false;
		_secondesSansMonteeAbysse = 0.0;
		_emerukedesiParotaromaStage1Actif = false;
		_emerukedesiParotaromaStage1FonduSortieActif = false;
		_emerukedesiParotaromaStage1TempsFonduRestant = 0.0;
		if (_materiauEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_materiauEmerukedesiParotaromaStage1))
			_materiauEmerukedesiParotaromaStage1.SetShaderParameter("strength", 0.0f);
		if (_overlayEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_overlayEmerukedesiParotaromaStage1))
			_overlayEmerukedesiParotaromaStage1.Visible = false;
	}

	/// <summary>Mise à jour de la manifestation palier 1 de l'<see cref="EmerukedesiParotaroma"/> (remontée en zone négative uniquement).</summary>
	private void MettreAJourEmerukedesiParotaromaStage1(double delta)
	{
		if (_joueur == null || _dimensionLocaleActive != (int)DimensionJeu.Abysse)
		{
			ReinitialiserEmerukedesiParotaromaStage1();
			return;
		}

		float yActuel = _joueur.GlobalPosition.Y;
		if (yActuel >= 0f)
		{
			ReinitialiserEmerukedesiParotaromaStage1();
			_dernierYRemonteeAbysse = yActuel;
			return;
		}

		if (float.IsNaN(_dernierYRemonteeAbysse))
			_dernierYRemonteeAbysse = yActuel;

		float deltaY = yActuel - _dernierYRemonteeAbysse;
		bool remonteeEffective = deltaY > SeuilProgressionMonteeAbysseMetres;
		bool redescenteNette = deltaY < -SeuilRedescenteNetteAbysseMetres;

		if (float.IsNaN(_yDepartMonteeAbysse))
			_yDepartMonteeAbysse = yActuel;

		float intensite = 0f;
		if (remonteeEffective)
		{
			_monteeAbysseContinue = true;
			_secondesSansMonteeAbysse = 0.0;

			bool gainSuffisant = !float.IsNaN(_yDepartMonteeAbysse)
				&& (yActuel - _yDepartMonteeAbysse) >= SeuilDeclenchementRemonteeAbysseMetres;
			if (!_emerukedesiParotaromaStage1Actif && gainSuffisant)
				_emerukedesiParotaromaStage1Actif = true;

			if (_emerukedesiParotaromaStage1Actif)
			{
				_emerukedesiParotaromaStage1FonduSortieActif = false;
				_emerukedesiParotaromaStage1TempsFonduRestant = DureeFonduEmerukedesiParotaromaStage1Sec;
				intensite = 1f;
			}
		}
		else
		{
			_monteeAbysseContinue = false;
			_secondesSansMonteeAbysse += delta;

			if (redescenteNette)
				_yDepartMonteeAbysse = yActuel;

			if (_emerukedesiParotaromaStage1Actif
				&& !_emerukedesiParotaromaStage1FonduSortieActif
				&& _secondesSansMonteeAbysse >= DelaiArretMonteeAvantFonduParotaromaSec)
			{
				_emerukedesiParotaromaStage1FonduSortieActif = true;
				_emerukedesiParotaromaStage1TempsFonduRestant = DureeFonduEmerukedesiParotaromaStage1Sec;
			}

			if (_emerukedesiParotaromaStage1FonduSortieActif)
			{
				_emerukedesiParotaromaStage1TempsFonduRestant = Math.Max(0.0, _emerukedesiParotaromaStage1TempsFonduRestant - delta);
				intensite = (float)Mathf.Clamp((float)(_emerukedesiParotaromaStage1TempsFonduRestant / DureeFonduEmerukedesiParotaromaStage1Sec), 0f, 1f);
				if (_emerukedesiParotaromaStage1TempsFonduRestant <= 0.0)
				{
					_emerukedesiParotaromaStage1FonduSortieActif = false;
					_emerukedesiParotaromaStage1Actif = false;
					_yDepartMonteeAbysse = yActuel;
					intensite = 0f;
				}
			}
			else if (_emerukedesiParotaromaStage1Actif)
			{
				// Pendant le délai anti-yoyo, on conserve l'intensité pleine pour éviter une coupure visuelle brutale.
				intensite = 1f;
			}
		}

		if (_materiauEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_materiauEmerukedesiParotaromaStage1))
			_materiauEmerukedesiParotaromaStage1.SetShaderParameter("strength", intensite);
		if (_overlayEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_overlayEmerukedesiParotaromaStage1))
			_overlayEmerukedesiParotaromaStage1.Visible = intensite > 0.001f;
		_dernierYRemonteeAbysse = yActuel;
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

	private void ChargerOptionsGraphiquesAuDemarrage()
	{
		var defaut = (_optionsGraphiquesDefautProjet ?? CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise)).Clone();
		if (IgnorerFichierOptionsGraphiquesAuDemarrage)
		{
			_optionsGraphiquesChargeesUtilisateur = false;
			AppliquerOptionsGraphiques(defaut, sauvegarder: false, synchroniserUi: false);
			return;
		}
		_optionsGraphiquesChargeesUtilisateur = FileAccess.FileExists("user://options_graphics.cfg");
		GraphicsOptionsData chargees = GraphicsOptionsService.ChargerOuDefaut(defaut);
		AppliquerOptionsGraphiques(chargees, sauvegarder: false, synchroniserUi: false);
	}

	private GraphicsOptionsData CapturerOptionsGraphiquesCourantes(PresetGraphique preset)
	{
		return GraphicsOptionsService.Normaliser(new GraphicsOptionsData
		{
			Preset = preset,
			RenderDistance = RenderDistance,
			RenderDistanceDetailChunks = RenderDistanceDetailChunks,
			RayonQualiteProcheChunks = RayonQualiteProcheChunks,
			RayonGazonVisibleChunks = RayonGazonVisibleChunks,
			RayonBuissonsVisibleChunks = RayonBuissonsVisibleChunks,
			ActiverHorizonLod = ActiverHorizonLod,
			RayonHorizonChunks = RayonHorizonChunks,
			PasHorizonMetres = PasHorizonMetres,
			ActiverCullingCameraChunks = ActiverCullingCameraChunks,
			AngleCullingCameraDeg = AngleCullingCameraDeg,
			MargeChunksToujoursVisibles = MargeChunksToujoursVisibles,
			MaxChunksParFrame = MaxChunksParFrame,
			LODTextureEtapes = _mondeClient?.LODTextureEtapes ?? 12,
			ProfilLodCinematiqueUltraSmooth = _mondeClient?.ProfilLodCinematiqueUltraSmooth ?? true,
			ModeSurvieFpsAgressif = _mondeClient?.ModeSurvieFpsAgressif ?? true,
			FpsCibleAutoDiagnostic = _mondeClient?.FpsCibleAutoDiagnostic ?? 60,
			SeuilFpsUrgenceForte = _mondeClient?.SeuilFpsUrgenceForte ?? 42,
			SeuilFpsUrgenceCritique = _mondeClient?.SeuilFpsUrgenceCritique ?? 30,
			SeuilFpsUrgenceExtreme = _mondeClient?.SeuilFpsUrgenceExtreme ?? 24,
			SeuilFpsSortieUrgenceExtreme = _mondeClient?.SeuilFpsSortieUrgenceExtreme ?? 56
		});
	}

	private void RestaurerParametresMondeClientNonExposesUtilisateur(bool modeProtectionFps)
	{
		if (_mondeClient == null)
			return;
		// On rétablit ces paramètres même après un ancien profil matériel.
		_mondeClient.MaxLancementsTravailleursParTick = modeProtectionFps ? 2 : 6;
		_mondeClient.BudgetFrameCibleMs = modeProtectionFps ? 16.2f : 22f;
		// 50 FPS de gel était trop agressif (beaucoup de configs restent 45–55) ; hors survie : gate désactivé via ForcerModeStreaming.
		_mondeClient.SeuilFpsGateStrict = modeProtectionFps ? 40f : 28f;
		_mondeClient.SeuilFpsGateReprise = modeProtectionFps ? 52f : 34f;
		_mondeClient.DureeStabiliteReprise = modeProtectionFps ? 0.20f : 0.12f;
		_mondeClient.DureeRampUpPostDegel = modeProtectionFps ? 0.55f : 0.18f;
		_mondeClient.DureeMinEtatGeleSec = modeProtectionFps ? 0.15f : 0.08f;
		_mondeClient.DureeMinEtatOuvertSec = modeProtectionFps ? 0.45f : 0.10f;
		_mondeClient.MaxChunksEvaluesCullingParPasse = modeProtectionFps ? 240 : 900;
		_mondeClient.MaxBasculesCullingParPasse = modeProtectionFps ? 96 : 300;
	}

	private void AppliquerOptionsGraphiques(GraphicsOptionsData options, bool sauvegarder, bool synchroniserUi, bool prioriteChargementStreamApresReglageManuel = false)
	{
		GraphicsOptionsData o = GraphicsOptionsService.Normaliser(options?.Clone() ?? new GraphicsOptionsData());
		int ancienRenderDistance = RenderDistance;
		RenderDistance = o.RenderDistance;
		RenderDistanceDetailChunks = o.RenderDistanceDetailChunks;
		RayonQualiteProcheChunks = o.RayonQualiteProcheChunks;
		RayonGazonVisibleChunks = o.RayonGazonVisibleChunks;
		RayonBuissonsVisibleChunks = o.RayonBuissonsVisibleChunks;
		ActiverHorizonLod = o.ActiverHorizonLod;
		RayonHorizonChunks = o.RayonHorizonChunks;
		PasHorizonMetres = o.PasHorizonMetres;
		ActiverCullingCameraChunks = o.ActiverCullingCameraChunks;
		AngleCullingCameraDeg = o.AngleCullingCameraDeg;
		MargeChunksToujoursVisibles = o.MargeChunksToujoursVisibles;
		MaxChunksParFrame = o.MaxChunksParFrame;

		if (_serveurParDimension.Count > 0)
		{
			bool modeProtectionFps = o.ModeSurvieFpsAgressif;
			foreach (var kv in _serveurParDimension)
			{
				Monde_Serveur serveur = kv.Value;
				if (serveur == null) continue;
				serveur.RenderDistance = RenderDistance;
				if (modeProtectionFps)
				{
					serveur.MultiplicateurCharge = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 18f), 1, 3);
					serveur.MaxDemandesChunksParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 12f), 2, 10);
					serveur.MaxIntegrationsWorkersParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 18f), 2, 6);
					serveur.MaxChunksEnvoiParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 8f), 8, 20);
				}
				else
				{
					// Priorité joueur : laisse les grosses distances pousser réellement le streaming.
					serveur.MultiplicateurCharge = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 10f), 2, 8);
					serveur.MaxDemandesChunksParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 2.5f), 8, 48);
					serveur.MaxIntegrationsWorkersParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 6f), 4, 16);
					serveur.MaxChunksEnvoiParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 1.5f), 12, 80);
				}
			}
		}

		if (_mondeClient != null)
		{
			_mondeClient.RenderDistance = RenderDistance;
			_mondeClient.RenderDistanceDetailChunks = RenderDistanceDetailChunks;
			_mondeClient.RayonQualiteMaxChunks = RayonQualiteProcheChunks;
			_mondeClient.RayonGazonVisibleChunks = RayonGazonVisibleChunks;
			_mondeClient.RayonBuissonsVisibleChunks = RayonBuissonsVisibleChunks;
			_mondeClient.ActiverHorizonLod = ActiverHorizonLod;
			_mondeClient.RayonHorizonChunks = RayonHorizonChunks;
			_mondeClient.PasHorizonMetres = PasHorizonMetres;
			_mondeClient.ActiverCullingCameraChunks = ActiverCullingCameraChunks;
			_mondeClient.AngleCullingCameraDeg = AngleCullingCameraDeg;
			_mondeClient.MargeChunksToujoursVisibles = MargeChunksToujoursVisibles;
			_mondeClient.MaxChunksParFrame = MaxChunksParFrame;
			_mondeClient.LODTextureEtapes = o.LODTextureEtapes;
			_mondeClient.ProfilLodCinematiqueUltraSmooth = o.ProfilLodCinematiqueUltraSmooth;
			_mondeClient.ModeSurvieFpsAgressif = o.ModeSurvieFpsAgressif;
			_mondeClient.FpsCibleAutoDiagnostic = o.FpsCibleAutoDiagnostic;
			_mondeClient.SeuilFpsUrgenceForte = o.SeuilFpsUrgenceForte;
			_mondeClient.SeuilFpsUrgenceCritique = o.SeuilFpsUrgenceCritique;
			_mondeClient.SeuilFpsUrgenceExtreme = o.SeuilFpsUrgenceExtreme;
			_mondeClient.SeuilFpsSortieUrgenceExtreme = o.SeuilFpsSortieUrgenceExtreme;
			_mondeClient.MaxAjoutsRadarParPasse = o.ModeSurvieFpsAgressif
				? Mathf.Clamp(480 + RenderDistance * 8, 520, 2000)
				: Mathf.Clamp(1200 + RenderDistance * 40, 1600, 8000);
			// D’abord aligner gate / diagnostic / rayon requêtes sur le choix utilisateur, puis réglages dérivés et horizon.
			// Avant : Reappliquer puis Forcer → une frame pouvait laisser le gel actif alors que « Sauver les FPS » était décoché.
			_mondeClient.ForcerModeStreamingUtilisateur(o.ModeSurvieFpsAgressif);
			RestaurerParametresMondeClientNonExposesUtilisateur(o.ModeSurvieFpsAgressif);
			_mondeClient.ReappliquerReglagesGraphiquesRuntime();
			_mondeClient.ForcerRafraichissementStreamingGraphique(microReload: true);
			if (_joueur != null && RenderDistance > ancienRenderDistance)
			{
				Vector2I chunkActuel = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
				_mondeClient.ReserverChunkSpawnPrioritaire(chunkActuel);
				_mondeClient.ImpulserConvergenceVersRenderDistance();
			}
			// Décocher « Sauver les FPS » : grâce streaming pour débloquer tout de suite la distance mesurée (même sans bouton Appliquer dédié).
			if (!o.ModeSurvieFpsAgressif || prioriteChargementStreamApresReglageManuel)
				_mondeClient.SignalerGraceStreamingApresReglageManuel();
		}

		if (_joueur is Joueur joueurHumain)
			joueurHumain.ConfigurerFarClipPourRenderDistance(RenderDistance, TailleChunk);

		var cycleSolaire = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		cycleSolaire?.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
		if (_mondeServeurAbysse is Gestionnaire_Abysse gestionnaireAbysseDistance)
			gestionnaireAbysseDistance.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);

		// Mode legacy : pas de Monde_Client — il faut rafraîchir la grille de chunks sinon RenderDistance ne bouge jamais tant qu’on ne change pas de chunk.
		if (!UseArchitectureReseau)
			ActualiserVisibiliteEtTriChunksLegacy();

		_optionsGraphiquesActuelles = o.Clone();
		if (synchroniserUi)
			SynchroniserPanelGraphiqueDepuisOptions(_optionsGraphiquesActuelles);
		if (sauvegarder)
		{
			GraphicsOptionsService.Sauvegarder(_optionsGraphiquesActuelles);
			_optionsGraphiquesChargeesUtilisateur = true;
		}
	}

	private void LancerAutoHybrideGraphique()
	{
		ForcerControleUtilisateurSurGraphismes();
		MettreAJourInfosMaterielDetecte();
		GraphicsOptionsData baseOptions = CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise);
		GraphicsOptionsData seed = GraphicsOptionsService.GenererBaseAutoMateriel(_nomCpuDetecte, _nomGpuDetecte, baseOptions);
		AppliquerOptionsGraphiques(seed, sauvegarder: true, synchroniserUi: true);
		_autoHybrideActif = true;
		_timerSessionAutoHybride = 0f;
		_timerAjustementAutoHybride = 0f;
		_fpsMinSessionAutoHybride = float.MaxValue;
		if (_labelAutoHybride != null)
			_labelAutoHybride.Text = "Auto hybride: analyse en cours...";
	}

	private void TraiterAutoHybrideGraphique(float dt)
	{
		if (!_autoHybrideActif || _mondeClient == null)
			return;
		if (_pauseVisible)
			return;

		_timerSessionAutoHybride += dt;
		_timerAjustementAutoHybride += dt;
		float fpsMoyen = _mondeClient.LireFpsMoyenAutoDiagnostic();
		_fpsMinSessionAutoHybride = Mathf.Min(_fpsMinSessionAutoHybride, fpsMoyen);

		const float intervalleAjustement = 4f;
		const float dureeSession = 18f;
		if (_timerAjustementAutoHybride >= intervalleAjustement)
		{
			_timerAjustementAutoHybride = 0f;
			GraphicsOptionsData ajuste = GraphicsOptionsService.AjusterSelonFps(
				CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise),
				fpsMoyen,
				_fpsMinSessionAutoHybride);
			AppliquerOptionsGraphiques(ajuste, sauvegarder: true, synchroniserUi: true);
		}

		if (_timerSessionAutoHybride >= dureeSession)
		{
			_autoHybrideActif = false;
			if (_labelAutoHybride != null)
				_labelAutoHybride.Text = $"Auto hybride termine (FPS moyen {fpsMoyen:0}).";
		}
	}

	private void MettreAJourInfosMaterielDetecte()
	{
		_nomCpuDetecte = OS.GetProcessorName()?.ToLowerInvariant() ?? "";
		try
		{
			_nomGpuDetecte = RenderingServer.GetVideoAdapterName().ToLowerInvariant();
		}
		catch
		{
			_nomGpuDetecte = "";
		}
	}

	private void RafraichirIndicateurModeEditionGraphique()
	{
		if (_labelModeEditionGraphique == null)
			return;
		if (_editionGraphiqueEnDirect)
			_labelModeEditionGraphique.Text = "Mode: LIVE (application en direct)";
		else if (_pauseVisible)
			_labelModeEditionGraphique.Text = "Mode: PAUSE";
		else
			_labelModeEditionGraphique.Text = "Mode: JEU";
	}

	private void ForcerMicroReloadGraphiqueMaintenant()
	{
		if (_mondeClient == null)
			return;
		_mondeClient.ForcerRafraichissementStreamingGraphique(microReload: true);
		if (_joueur != null)
		{
			Vector2I chunkActuel = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
			_mondeClient.ReserverChunkSpawnPrioritaire(chunkActuel);
		}
		ForcerCycleSolaireActif();
	}

	private void ForcerControleUtilisateurSurGraphismes()
	{
		_verrouProfilMaterielUtilisateur = true;
		_optionsGraphiquesChargeesUtilisateur = true;
		ActiverProfilMaterielAuto = false;
		ForcerProfilGTX1060i710700F = false;
	}

	private void ForcerCycleSolaireActif()
	{
		var cycle = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		cycle?.DefinirChargementMondeActif(false);
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

	private void CreerMenuPause()
	{
		// Au-dessus de l’inventaire (calque 100 sur le joueur).
		var layer = new CanvasLayer { Layer = 101, ProcessMode = ProcessModeEnum.Always };
		AddChild(layer);
		_panelPause = new Panel();
		_panelPause.SetAnchorsPreset(Control.LayoutPreset.Center);
		_panelPause.OffsetLeft = -100;
		_panelPause.OffsetTop = -80;
		_panelPause.OffsetRight = 100;
		_panelPause.OffsetBottom = 80;
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = 20;
		vbox.OffsetTop = 20;
		vbox.OffsetRight = -20;
		vbox.OffsetBottom = -20;
		vbox.AddThemeConstantOverride("separation", 10);
		_panelPause.AddChild(vbox);
		var lbl = new Label { Text = "Pause", HorizontalAlignment = HorizontalAlignment.Center };
		vbox.AddChild(lbl);
		var btnResume = new Button { Text = "Reprendre" };
		btnResume.Pressed += () => { ToggleMenuPause(); };
		vbox.AddChild(btnResume);
		var btnSave = new Button { Text = "Sauvegarder" };
		btnSave.Pressed += () => SauvegarderManuelDepuisMenu("BoutonPause");
		vbox.AddChild(btnSave);
		var btnGraphismes = new Button { Text = "Graphismes" };
		btnGraphismes.Pressed += () =>
		{
			if (_panelGraphismes != null)
			{
				SynchroniserPanelGraphiqueDepuisOptions(CapturerOptionsGraphiquesCourantes(_optionsGraphiquesActuelles?.Preset ?? PresetGraphique.Personnalise));
				_panelGraphismes.Visible = true;
				_editionGraphiqueEnDirect = true;
				ForcerCycleSolaireActif();
				RafraichirIndicateurModeEditionGraphique();
				// Edition en direct : on laisse le monde tourner pendant les ajustements.
				_panelPause.Visible = false;
				GetTree().Paused = false;
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		};
		vbox.AddChild(btnGraphismes);
		var btnMenu = new Button { Text = "Menu principal" };
		btnMenu.Pressed += () =>
		{
			ToggleMenuPause();
			GetTree().Paused = false;
			SauvegarderManuelDepuisMenu();
			GetTree().ChangeSceneToFile("res://menu_principal.tscn");
		};
		vbox.AddChild(btnMenu);
		var btnQuit = new Button { Text = "Quitter le jeu" };
		btnQuit.Pressed += () =>
		{
			SauvegarderManuelDepuisMenu();
			GetTree().Quit();
		};
		vbox.AddChild(btnQuit);
		layer.AddChild(_panelPause);
		CreerPanelGraphismes(layer);
		_panelPause.Visible = false;
	}

	private (HSlider slider, Label valeur) CreerLigneSlider(Control parent, string texte, float min, float max, float pas)
	{
		var ligne = new HBoxContainer();
		ligne.AddThemeConstantOverride("separation", 8);
		parent.AddChild(ligne);
		var label = new Label
		{
			Text = texte,
			CustomMinimumSize = new Vector2(230, 0),
			SizeFlagsHorizontal = Control.SizeFlags.Fill
		};
		ligne.AddChild(label);
		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = pas,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		var btnMoins = new Button { Text = "-", CustomMinimumSize = new Vector2(28, 0) };
		btnMoins.Pressed += () => slider.Value = Mathf.Max(slider.MinValue, slider.Value - slider.Step);
		ligne.AddChild(btnMoins);
		ligne.AddChild(slider);
		var btnPlus = new Button { Text = "+", CustomMinimumSize = new Vector2(28, 0) };
		btnPlus.Pressed += () => slider.Value = Mathf.Min(slider.MaxValue, slider.Value + slider.Step);
		ligne.AddChild(btnPlus);
		var valeur = new Label
		{
			Text = "-",
			HorizontalAlignment = HorizontalAlignment.Right,
			CustomMinimumSize = new Vector2(70, 0)
		};
		ligne.AddChild(valeur);
		return (slider, valeur);
	}

	private void CreerPanelGraphismes(CanvasLayer layer)
	{
		_panelGraphismes = new Panel
		{
			Visible = false
		};
		_panelGraphismes.SetAnchorsPreset(Control.LayoutPreset.Center);
		_panelGraphismes.OffsetLeft = -360;
		_panelGraphismes.OffsetTop = -270;
		_panelGraphismes.OffsetRight = 360;
		_panelGraphismes.OffsetBottom = 270;

		var marge = new MarginContainer();
		marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		marge.AddThemeConstantOverride("margin_left", 16);
		marge.AddThemeConstantOverride("margin_top", 16);
		marge.AddThemeConstantOverride("margin_right", 16);
		marge.AddThemeConstantOverride("margin_bottom", 16);
		_panelGraphismes.AddChild(marge);

		var racine = new VBoxContainer();
		racine.AddThemeConstantOverride("separation", 8);
		marge.AddChild(racine);

		racine.AddChild(new Label
		{
			Text = "Reglages graphiques avances",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		_optionPresetGraphique = new OptionButton();
		_optionPresetGraphique.AddItem("Faible", (int)PresetGraphique.Faible);
		_optionPresetGraphique.AddItem("Moyen", (int)PresetGraphique.Moyen);
		_optionPresetGraphique.AddItem("Eleve", (int)PresetGraphique.Eleve);
		_optionPresetGraphique.AddItem("Ultra", (int)PresetGraphique.Ultra);
		_optionPresetGraphique.AddItem("Personnalise", (int)PresetGraphique.Personnalise);
		_optionPresetGraphique.ItemSelected += (_) => AppliquerPresetDepuisUI();
		racine.AddChild(_optionPresetGraphique);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		racine.AddChild(scroll);

		var contenu = new VBoxContainer();
		contenu.AddThemeConstantOverride("separation", 5);
		scroll.AddChild(contenu);

		(_sliderRenderDistance, _labelRenderDistanceValeur) = CreerLigneSlider(contenu, "Distance de rendu (chunks)", 6, 64, 1);
		(_sliderRayonQualiteProche, _labelRayonQualiteProcheValeur) = CreerLigneSlider(contenu, "Qualite proche chunks", 1, 24, 1);
		(_sliderDetailChunks, _labelDetailChunksValeur) = CreerLigneSlider(contenu, "Distance detail (chunks)", 6, 64, 1);
		(_sliderRayonGazon, _labelRayonGazonValeur) = CreerLigneSlider(contenu, "Visibilite gazon", 1, 24, 1);
		(_sliderRayonBuissons, _labelRayonBuissonsValeur) = CreerLigneSlider(contenu, "Visibilite buissons", 2, 32, 1);
		(_sliderRayonHorizon, _labelRayonHorizonValeur) = CreerLigneSlider(contenu, "Rayon horizon LOD", 24, 240, 1);
		(_sliderPasHorizon, _labelPasHorizonValeur) = CreerLigneSlider(contenu, "Pas horizon (metres)", 12, 80, 1);
		(_sliderAngleCulling, _labelAngleCullingValeur) = CreerLigneSlider(contenu, "Angle culling camera", 80, 175, 1);
		(_sliderMargeToujoursVisible, _labelMargeToujoursVisibleValeur) = CreerLigneSlider(contenu, "Marge toujours visible", 1, 32, 1);
		(_sliderMaxChunksFrame, _labelMaxChunksFrameValeur) = CreerLigneSlider(contenu, "Max chunks / frame", 2, 40, 1);
		(_sliderLodEtapes, _labelLodEtapesValeur) = CreerLigneSlider(contenu, "Etapes LOD texture", 8, 24, 1);

		_checkActiverHorizon = new CheckBox { Text = "Activer horizon lointain simplifie" };
		_checkActiverCulling = new CheckBox { Text = "Activer culling camera des chunks" };
		_checkLodUltraSmooth = new CheckBox { Text = "LOD texture ultra smooth" };
		_checkModeSurvieAgressif = new CheckBox
		{
			Text = "Sauver les FPS (gel streaming + plafonds; décoche = distance de rendu pleine, gate FPS désactivé)"
		};
		contenu.AddChild(_checkActiverHorizon);
		contenu.AddChild(_checkActiverCulling);
		contenu.AddChild(_checkLodUltraSmooth);
		contenu.AddChild(_checkModeSurvieAgressif);

		_sliderRenderDistance.ValueChanged += (_) =>
		{
			_sliderDetailChunks.MaxValue = _sliderRenderDistance.Value;
			if (_sliderDetailChunks.Value > _sliderDetailChunks.MaxValue)
				_sliderDetailChunks.Value = _sliderDetailChunks.MaxValue;
		};

		_sliderRenderDistance.ValueChanged += (_) => _labelRenderDistanceValeur.Text = $"{_sliderRenderDistance.Value:0}";
		_sliderRayonQualiteProche.ValueChanged += (_) => _labelRayonQualiteProcheValeur.Text = $"{_sliderRayonQualiteProche.Value:0}";
		_sliderDetailChunks.ValueChanged += (_) => _labelDetailChunksValeur.Text = $"{_sliderDetailChunks.Value:0}";
		_sliderRayonGazon.ValueChanged += (_) => _labelRayonGazonValeur.Text = $"{_sliderRayonGazon.Value:0}";
		_sliderRayonBuissons.ValueChanged += (_) => _labelRayonBuissonsValeur.Text = $"{_sliderRayonBuissons.Value:0}";
		_sliderRayonHorizon.ValueChanged += (_) => _labelRayonHorizonValeur.Text = $"{_sliderRayonHorizon.Value:0}";
		_sliderPasHorizon.ValueChanged += (_) => _labelPasHorizonValeur.Text = $"{_sliderPasHorizon.Value:0}m";
		_sliderAngleCulling.ValueChanged += (_) => _labelAngleCullingValeur.Text = $"{_sliderAngleCulling.Value:0}deg";
		_sliderMargeToujoursVisible.ValueChanged += (_) => _labelMargeToujoursVisibleValeur.Text = $"{_sliderMargeToujoursVisible.Value:0}";
		_sliderMaxChunksFrame.ValueChanged += (_) => _labelMaxChunksFrameValeur.Text = $"{_sliderMaxChunksFrame.Value:0}";
		_sliderLodEtapes.ValueChanged += (_) => _labelLodEtapesValeur.Text = $"{_sliderLodEtapes.Value:0}";
		_sliderRenderDistance.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonQualiteProche.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderDetailChunks.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonGazon.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonBuissons.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonHorizon.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderPasHorizon.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderAngleCulling.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderMargeToujoursVisible.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderMaxChunksFrame.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderLodEtapes.ValueChanged += (_) => SurControleGraphiqueModifie();
		_checkActiverHorizon.Toggled += (_) => SurControleGraphiqueModifie();
		_checkActiverCulling.Toggled += (_) => SurControleGraphiqueModifie();
		_checkLodUltraSmooth.Toggled += (_) => SurControleGraphiqueModifie();
		_checkModeSurvieAgressif.Toggled += (_) => SurControleGraphiqueModifie();

		_labelModeEditionGraphique = new Label { Text = "Mode: PAUSE" };
		_labelAutoHybride = new Label { Text = "Auto hybride inactif." };
		racine.AddChild(_labelModeEditionGraphique);
		racine.AddChild(_labelAutoHybride);

		var boutons = new HBoxContainer();
		boutons.AddThemeConstantOverride("separation", 8);
		var btnAuto = new Button { Text = "Auto hybride" };
		btnAuto.Pressed += LancerAutoHybrideGraphique;
		var btnAppliquer = new Button { Text = "Appliquer" };
		btnAppliquer.Pressed += () =>
		{
			_autoHybrideActif = false;
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData lus = LireOptionsDepuisPanel();
			AppliquerOptionsGraphiques(lus, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			_labelAutoHybride.Text = "Reglages appliques.";
		};
		var btnAppliquerMicroReload = new Button { Text = "Appliquer + micro reload" };
		btnAppliquerMicroReload.Pressed += () =>
		{
			_autoHybrideActif = false;
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData lus = LireOptionsDepuisPanel();
			AppliquerOptionsGraphiques(lus, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			ForcerMicroReloadGraphiqueMaintenant();
			_labelAutoHybride.Text = "Reglages appliques + micro reload force.";
		};
		var btnReset = new Button { Text = "Reset (Moyen)" };
		btnReset.Pressed += () =>
		{
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData preset = GraphicsOptionsService.ConstruirePreset(PresetGraphique.Moyen, CapturerOptionsGraphiquesCourantes(PresetGraphique.Moyen));
			AppliquerOptionsGraphiques(preset, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			_labelAutoHybride.Text = "Preset moyen applique.";
		};
		var btnResetComplet = new Button { Text = "Reset complet (defaut projet)" };
		btnResetComplet.Pressed += () =>
		{
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData defautProjet = (_optionsGraphiquesDefautProjet ?? CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise)).Clone();
			defautProjet.Preset = PresetGraphique.Personnalise;
			AppliquerOptionsGraphiques(defautProjet, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			ForcerMicroReloadGraphiqueMaintenant();
			_labelAutoHybride.Text = "Reset complet applique (defaut projet).";
		};
		var btnFermer = new Button { Text = "Fermer" };
		btnFermer.Pressed += () =>
		{
			_panelGraphismes.Visible = false;
			_editionGraphiqueEnDirect = false;
			ForcerCycleSolaireActif();
			RafraichirIndicateurModeEditionGraphique();
			if (_panelPause != null)
				_panelPause.Visible = true;
			GetTree().Paused = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
		};
		boutons.AddChild(btnAuto);
		boutons.AddChild(btnAppliquer);
		boutons.AddChild(btnAppliquerMicroReload);
		boutons.AddChild(btnReset);
		boutons.AddChild(btnResetComplet);
		boutons.AddChild(btnFermer);
		racine.AddChild(boutons);

		layer.AddChild(_panelGraphismes);
		SynchroniserPanelGraphiqueDepuisOptions(CapturerOptionsGraphiquesCourantes(_optionsGraphiquesActuelles?.Preset ?? PresetGraphique.Personnalise));
		RafraichirIndicateurModeEditionGraphique();
	}

	private void AppliquerPresetDepuisUI()
	{
		if (_synchronisationUiGraphiqueEnCours)
			return;
		if (_optionPresetGraphique == null || _optionPresetGraphique.Selected < 0)
			return;
		PresetGraphique preset = (PresetGraphique)_optionPresetGraphique.GetItemId(_optionPresetGraphique.Selected);
		if (preset == PresetGraphique.Personnalise)
			return;
		ForcerControleUtilisateurSurGraphismes();
		GraphicsOptionsData baseOptions = CapturerOptionsGraphiquesCourantes(preset);
		GraphicsOptionsData p = GraphicsOptionsService.ConstruirePreset(preset, baseOptions);
		AppliquerOptionsGraphiques(p, sauvegarder: false, synchroniserUi: true);
		if (_mondeClient != null)
			_mondeClient.SignalerGraceStreamingApresReglageManuel();
		if (_labelAutoHybride != null)
			_labelAutoHybride.Text = $"Preset {preset} previsualise. Clique Appliquer pour sauvegarder.";
	}

	private void SurControleGraphiqueModifie()
	{
		if (_synchronisationUiGraphiqueEnCours)
			return;
		ForcerControleUtilisateurSurGraphismes();
		ForcerCycleSolaireActif();
		GraphicsOptionsData previsualisation = LireOptionsDepuisPanel();
		AppliquerOptionsGraphiques(previsualisation, sauvegarder: false, synchroniserUi: false);
		// Même hors mode LIVE : sans grâce streaming, ModeSurvieFpsAgressif plafonnait le radar et la distance de rendu « ne marchait pas ».
		if (_mondeClient != null)
			_mondeClient.SignalerGraceStreamingApresReglageManuel();
		if (_optionPresetGraphique != null)
		{
			int idx = _optionPresetGraphique.GetItemIndex((int)PresetGraphique.Personnalise);
			if (idx >= 0)
				_optionPresetGraphique.Select(idx);
		}
		if (_labelAutoHybride != null)
			_labelAutoHybride.Text = "Previsualisation active (non sauvegardee).";
	}

	private GraphicsOptionsData LireOptionsDepuisPanel()
	{
		return GraphicsOptionsService.Normaliser(new GraphicsOptionsData
		{
			Preset = PresetGraphique.Personnalise,
			RenderDistance = Mathf.RoundToInt((float)_sliderRenderDistance.Value),
			RenderDistanceDetailChunks = Mathf.RoundToInt((float)_sliderDetailChunks.Value),
			RayonQualiteProcheChunks = Mathf.RoundToInt((float)_sliderRayonQualiteProche.Value),
			RayonGazonVisibleChunks = Mathf.RoundToInt((float)_sliderRayonGazon.Value),
			RayonBuissonsVisibleChunks = Mathf.RoundToInt((float)_sliderRayonBuissons.Value),
			ActiverHorizonLod = _checkActiverHorizon.ButtonPressed,
			RayonHorizonChunks = Mathf.RoundToInt((float)_sliderRayonHorizon.Value),
			PasHorizonMetres = (float)_sliderPasHorizon.Value,
			ActiverCullingCameraChunks = _checkActiverCulling.ButtonPressed,
			AngleCullingCameraDeg = (float)_sliderAngleCulling.Value,
			MargeChunksToujoursVisibles = Mathf.RoundToInt((float)_sliderMargeToujoursVisible.Value),
			MaxChunksParFrame = Mathf.RoundToInt((float)_sliderMaxChunksFrame.Value),
			LODTextureEtapes = Mathf.RoundToInt((float)_sliderLodEtapes.Value),
			ProfilLodCinematiqueUltraSmooth = _checkLodUltraSmooth.ButtonPressed,
			ModeSurvieFpsAgressif = _checkModeSurvieAgressif.ButtonPressed,
			FpsCibleAutoDiagnostic = _optionsGraphiquesActuelles?.FpsCibleAutoDiagnostic ?? 60,
			SeuilFpsUrgenceForte = _optionsGraphiquesActuelles?.SeuilFpsUrgenceForte ?? 42,
			SeuilFpsUrgenceCritique = _optionsGraphiquesActuelles?.SeuilFpsUrgenceCritique ?? 30,
			SeuilFpsUrgenceExtreme = _optionsGraphiquesActuelles?.SeuilFpsUrgenceExtreme ?? 24,
			SeuilFpsSortieUrgenceExtreme = _optionsGraphiquesActuelles?.SeuilFpsSortieUrgenceExtreme ?? 56
		});
	}

	private void SynchroniserPanelGraphiqueDepuisOptions(GraphicsOptionsData options)
	{
		if (_panelGraphismes == null)
			return;
		GraphicsOptionsData o = GraphicsOptionsService.Normaliser(options?.Clone() ?? CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise));
		_synchronisationUiGraphiqueEnCours = true;
		if (_optionPresetGraphique != null)
		{
			int idx = _optionPresetGraphique.GetItemIndex((int)o.Preset);
			int idxSel = idx >= 0 ? idx : _optionPresetGraphique.GetItemIndex((int)PresetGraphique.Personnalise);
			// Select() émet ItemSelected (souvent en différé) : sans blocage, AppliquerPresetDepuisUI réécrit tout le monde avec le preset et annule les curseurs.
			_optionPresetGraphique.SetBlockSignals(true);
			_optionPresetGraphique.Select(idxSel);
			_optionPresetGraphique.SetBlockSignals(false);
		}
		_sliderRenderDistance.SetValueNoSignal(o.RenderDistance);
		_sliderRayonQualiteProche.SetValueNoSignal(o.RayonQualiteProcheChunks);
		_sliderDetailChunks.MaxValue = o.RenderDistance;
		_sliderDetailChunks.SetValueNoSignal(Mathf.Clamp(o.RenderDistanceDetailChunks, 6, o.RenderDistance));
		_sliderRayonGazon.SetValueNoSignal(o.RayonGazonVisibleChunks);
		_sliderRayonBuissons.SetValueNoSignal(o.RayonBuissonsVisibleChunks);
		_sliderRayonHorizon.SetValueNoSignal(o.RayonHorizonChunks);
		_sliderPasHorizon.SetValueNoSignal(o.PasHorizonMetres);
		_sliderAngleCulling.SetValueNoSignal(o.AngleCullingCameraDeg);
		_sliderMargeToujoursVisible.SetValueNoSignal(o.MargeChunksToujoursVisibles);
		_sliderMaxChunksFrame.SetValueNoSignal(o.MaxChunksParFrame);
		_sliderLodEtapes.SetValueNoSignal(o.LODTextureEtapes);
		_checkActiverHorizon.ButtonPressed = o.ActiverHorizonLod;
		_checkActiverCulling.ButtonPressed = o.ActiverCullingCameraChunks;
		_checkLodUltraSmooth.ButtonPressed = o.ProfilLodCinematiqueUltraSmooth;
		_checkModeSurvieAgressif.ButtonPressed = o.ModeSurvieFpsAgressif;

		_labelRenderDistanceValeur.Text = $"{o.RenderDistance}";
		_labelRayonQualiteProcheValeur.Text = $"{o.RayonQualiteProcheChunks}";
		_labelDetailChunksValeur.Text = $"{o.RenderDistanceDetailChunks}";
		_labelRayonGazonValeur.Text = $"{o.RayonGazonVisibleChunks}";
		_labelRayonBuissonsValeur.Text = $"{o.RayonBuissonsVisibleChunks}";
		_labelRayonHorizonValeur.Text = $"{o.RayonHorizonChunks}";
		_labelPasHorizonValeur.Text = $"{o.PasHorizonMetres:0}m";
		_labelAngleCullingValeur.Text = $"{o.AngleCullingCameraDeg:0}deg";
		_labelMargeToujoursVisibleValeur.Text = $"{o.MargeChunksToujoursVisibles}";
		_labelMaxChunksFrameValeur.Text = $"{o.MaxChunksParFrame}";
		_labelLodEtapesValeur.Text = $"{o.LODTextureEtapes}";
		_synchronisationUiGraphiqueEnCours = false;
	}

	private void ToggleMenuPause()
	{
		if (_panelPause == null) CreerMenuPause();
		_pauseVisible = !_pauseVisible;
		_panelPause.Visible = _pauseVisible;
		if (!_pauseVisible && _panelGraphismes != null)
		{
			_panelGraphismes.Visible = false;
			_editionGraphiqueEnDirect = false;
		}
		RafraichirIndicateurModeEditionGraphique();
		GetTree().Paused = _pauseVisible;
		Input.MouseMode = _pauseVisible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}

	private void DemarrerArchitectureReseau()
	{
		DetecterProfilMaterielEtAjuster();
		_networkManager = new NetworkManager();
		AddChild(_networkManager);
		_networkManager.DemarrerHostSolo();
		_networkManager.CommandeAdminDemandee += SurCommandeAdminDemandee;
		_networkManager.InjectionItemCreatifDemandee += SurInjectionItemCreatifDemandee;
		_networkManager.DemandeChunkDimensionDemandee += SurDemandeChunkDimensionDemandee;

		_serveurParDimension.Clear();
		_attenteChunksParDimension.Clear();
		_dimensionParPeer.Clear();
		_racineParDimension.Clear();
		_arbresParDimension.Clear();

		// Crée une racine de scène par dimension (Alpha + Abysse + Beta + Omega + Delta) avant d'instancier les serveurs.
		foreach (var info in ConstantesDimensions.Toutes())
		{
			var racine = new Node3D { Name = info.NomCanonique };
			AddChild(racine);
			_racineParDimension[info.Id] = racine;
		}

		int seedAlpha = GetNode<GameState>("/root/GameState").SeedTerrainActuel;
		Material materielTerrainResolu = TerrainMaterialFactory.ObtenirMaterielTerrainRobuste(MaterielTerrain);

		// Alpha + clones (Beta/Omega/Delta) : même seed, même algorithme, fuseaux décalés de 0/+6/+12/+18 h.
		foreach (var info in ConstantesDimensions.ToutesAlphaLike())
		{
			var serveur = new Monde_Serveur
			{
				NomDimension = info.NomCanonique,
				ActiverGenerationAbysse = false,
				TailleChunk = TailleChunk,
				HauteurMax = HauteurMax,
				SeedTerrain = seedAlpha,
				RenderDistance = RenderDistance,
				FuseauHoraireHeures = FuseauHoraireHeures + info.FuseauOffsetHeures,
				ModeEssencesPartoutTemporaire = ModeEssencesPartoutTemporaire,
				RatioJungleModeTest = RatioJungleModeTest,
				MaterielTerrain = materielTerrainResolu
			};
			_serveurParDimension[info.Id] = serveur;
			_attenteChunksParDimension[info.Id] = new Dictionary<Vector3I, HashSet<long>>();
			if (info.Id == (int)DimensionJeu.Alpha)
				_mondeServeurAlpha = serveur;
		}

		// APISARA : génération abyssale dédiée, seed décalée +9137 (bruits Abysse historiques), heure forcée 13h30.
		_mondeServeurAbysse = new Gestionnaire_Abysse
		{
			NomDimension = ConstantesDimensionAbysse.Apisara,
			ActiverGenerationAbysse = true,
			TailleChunk = TailleChunk,
			HauteurMax = HauteurMax,
			SeedTerrain = seedAlpha + 9137,
			RenderDistance = RenderDistance,
			FuseauHoraireHeures = FuseauHoraireHeures + ConstantesDimensions.ObtenirInfoOuAlpha((int)DimensionJeu.Abysse).FuseauOffsetHeures,
			ModeEssencesPartoutTemporaire = ModeEssencesPartoutTemporaire,
			RatioJungleModeTest = RatioJungleModeTest,
			MaterielTerrain = materielTerrainResolu
		};
		_serveurParDimension[(int)DimensionJeu.Abysse] = _mondeServeurAbysse;
		_attenteChunksParDimension[(int)DimensionJeu.Abysse] = new Dictionary<Vector3I, HashSet<long>>();

		_mondeServeur = _mondeServeurAlpha;
		if (!_serveurParDimension.ContainsKey(_dimensionLocaleActive))
			_dimensionLocaleActive = (int)DimensionJeu.Alpha;
		DefinirDimensionPeer(Multiplayer.GetUniqueId(), _dimensionLocaleActive);

		_mondeClient = new Monde_Client();
		_mondeClient.TailleChunk = TailleChunk;
		_mondeClient.HauteurMax = HauteurMax;
		_mondeClient.RenderDistance = RenderDistance;
		_mondeClient.RenderDistanceDetailChunks = RenderDistanceDetailChunks;
		_mondeClient.RayonQualiteMaxChunks = RayonQualiteProcheChunks;
		_mondeClient.RayonGazonVisibleChunks = RayonGazonVisibleChunks;
		_mondeClient.RayonBuissonsVisibleChunks = RayonBuissonsVisibleChunks;
		_mondeClient.ActiverHorizonLod = ActiverHorizonLod;
		_mondeClient.RayonHorizonChunks = RayonHorizonChunks;
		_mondeClient.PasHorizonMetres = PasHorizonMetres;
		_mondeClient.ActiverCullingCameraChunks = ActiverCullingCameraChunks;
		_mondeClient.AngleCullingCameraDeg = AngleCullingCameraDeg;
		_mondeClient.MargeChunksToujoursVisibles = MargeChunksToujoursVisibles;
		_mondeClient.MaxChunksParFrame = MaxChunksParFrame;
		_mondeClient.MaterielTerrain = TerrainMaterialFactory.ObtenirMaterielTerrainRobuste(MaterielTerrain);
		ConfigurerProfilMondeClientSelonMateriel();
		_mondeClient.Initialiser(
			_joueur,
			GetNode<GameState>("/root/GameState").SeedTerrainActuel,
			coord =>
			{
				Vector3 posJ = ObtenirPositionJoueurOuSpawn();
				int coordY = Mathf.FloorToInt(posJ.Y / Mathf.Max(1f, _mondeServeur?.HauteurMax ?? 1));
				_mondeServeur?.EnregistrerDemandeChunk(coord, coordY, posJ);
			},
			(pointImpact, rayon, forceDegats) => _mondeServeur.AppliquerDestructionGlobale(pointImpact, rayon, forceDegats),
			(pointImpact, normale, rayon, idMatiere) => _mondeServeur.AppliquerCreationGlobale(pointImpact, normale, rayon, idMatiere)
		);
		_mondeClient.ConfigurerReseauChunks(_networkManager, _dimensionLocaleActive);
		AppliquerOptionsGraphiques(CapturerOptionsGraphiquesCourantes(_optionsGraphiquesActuelles?.Preset ?? PresetGraphique.Personnalise), sauvegarder: false, synchroniserUi: false);

		// Initialise et reparente chaque serveur sous sa racine dédiée (Alpha, Beta, Omega, Delta, Abysse).
		foreach (var kv in _serveurParDimension)
		{
			InitialiserDimensionServeur(kv.Value, kv.Key);
			if (_racineParDimension.TryGetValue(kv.Key, out Node3D racine) && racine != null && kv.Value != null)
				racine.AddChild(kv.Value);
		}
		MettreAJourVisibiliteArbresParDimension(_dimensionLocaleActive);
		AddChild(_mondeClient);
		ReparenterNoeudDansDimension(_joueur, (int)DimensionJeu.Alpha);
		MettreAJourAtmosphereAbysseLocale(_dimensionLocaleActive);
		MettreAJourSuspensionServeursDimensions(_dimensionLocaleActive);

		// Croissance des arbres + jour absolu au passage minuit
		var cycleSolaire = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (cycleSolaire != null)
		{
			cycleSolaire.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
			if (_mondeServeurAbysse is Gestionnaire_Abysse gestionnaireAbysseDistance)
				gestionnaireAbysseDistance.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
			cycleSolaire.Connect("NouveauJour", Callable.From(() =>
			{
				GameState.Instance?.IncrementerJourAbsolu();
				foreach (var kv in _serveurParDimension)
				{
					if (kv.Value == null || kv.Value.EstSimulationSuspendue)
						continue;
					kv.Value.FairePousserArbresDuJour();
				}
			}));
		}

		// Matrice visqueuse : Area3D océan (Y < 103) impose damp + gravité réduite (Archimède)
		CreerAreaOcean();

		// Lier le chunk de spawn en priorité pour éviter chute libre (comme les 2 fois précédentes)
		Vector3 pos = _joueur.GlobalPosition;
		Vector2I chunkSpawn = WorldToChunkCoord(pos, TailleChunk);
		_mondeClient.ReserverChunkSpawnPrioritaire(chunkSpawn);

		// Envoyer le fuseau horaire de la dimension au client (spawn / portail)
		EnvoyerFuseauHoraireAuPeer(1); // Peer 1 = hôte local en Solo
		Multiplayer.PeerConnected += EnvoyerFuseauHoraireAuPeer;
		Multiplayer.PeerConnected += SurPeerConnecteDimensions;
		Multiplayer.PeerDisconnected += SurPeerDeconnecteDimensions;

		Callable.From(InitialiserPortailsNexusSiNecessaire).CallDeferred();
	}

	/// <summary>
	/// Modèle <c>Portaille.glb</c> : un portail à l’origine <c>(0, surface, 0)</c> par monde Alpha / Beta / Omega / Delta (vers APISARA) ;
	/// quatre portails sur la prairie extérieure APISARA (axes N, E, S, O ~1280 m). Liaisons fixes : Nord↔Alpha, Est↔Beta, Sud↔Omega, Ouest↔Delta (voir <see cref="NexusPortailsApisara"/>).
	/// </summary>
	private void InitialiserPortailsNexusSiNecessaire()
	{
		if (_portailsNexusPlaces || !UseArchitectureReseau) return;
		_portailsNexusPlaces = true;
		const string cheminPortaille = "res://Modeles/structure/portaille/Portaille.glb";
		var scene = GD.Load<PackedScene>("res://Scenes/PortailNexus.tscn");
		if (scene == null)
		{
			GD.PrintErr("ZERO-K : impossible de charger res://Scenes/PortailNexus.tscn.");
			return;
		}

		foreach (var info in ConstantesDimensions.ToutesAlphaLike())
		{
			if (!_racineParDimension.TryGetValue(info.Id, out Node3D racine) || racine == null) continue;
			var p = scene.Instantiate() as Portail;
			if (p == null) continue;
			p.Name = $"PortailVersApisara_{info.NomCanonique}";
			p.CheminScenePortaille = cheminPortaille;
			p.Liaison = NexusPortailsApisara.ObtenirCardinalPourDimensionAlphaLike(info.Id);
			p.AncreSurApisara = false;
			p.IdDimensionConteneur = info.Id;
			p.ForcerAttenteAffichageJusquaSolConfirmeVersApisara();
			racine.AddChild(p);
			var xz = ObtenirMeilleurXZPortailOrigineAlphaLike(info.Id, SeedTerrain);
			_xzPortailVersApisaraParDimension[info.Id] = xz;
			// Hauteur procédurale tout de suite (évite une frame à Y=0) ; au prochain idle : raycast vers le sol pour coller au mesh.
			float yInit = EstimerAltitudeTerrainPortail(xz.X, xz.Y, info.Id);
			float enf = Mathf.Max(0f, p.EnfoncementBaseAuSolMetres);
			p.GlobalPosition = new Vector3(xz.X, yInit - enf, xz.Y);
			Vector2 xzCapture = xz;
			int idDimCapture = info.Id;
			Callable.From(() => AffinerPortailVersApisaraSolParRaycast(p, xzCapture, idDimCapture)).CallDeferred();
		}

		if (_racineParDimension.TryGetValue((int)DimensionJeu.Abysse, out Node3D racineAb) && racineAb != null)
		{
			foreach (PointCardinal c in Enum.GetValues(typeof(PointCardinal)))
			{
				var p = scene.Instantiate() as Portail;
				if (p == null) continue;
				p.Name = $"PortailDepuisApisara_{c}";
				p.CheminScenePortaille = cheminPortaille;
				p.Liaison = c;
				p.AncreSurApisara = true;
				p.IdDimensionConteneur = (int)DimensionJeu.Abysse;
				racineAb.AddChild(p);
				var a = NexusCoords.ObtenirAncreApisara(c);
				float y = EstimerAltitudeTerrainPortail(a.X, a.Z, (int)DimensionJeu.Abysse);
				p.Position = new Vector3(a.X, y - Mathf.Max(0f, p.EnfoncementBaseAuSolMetres), a.Z);
				Callable.From(() => p.AlignerPortailSurSurface()).CallDeferred();
			}
		}

		PrioriserChunksClientAutourPortailsDimension(_dimensionLocaleActive);
		MettreAJourVisibilitePortailsParDimension(_dimensionLocaleActive);
		GD.Print("ZERO-K : portails Nexus (Portaille.glb) — 4 mondes à (0,0) + 4 sur plaine extérieure APISARA (N,E,S,O).");
		Callable.From(DiffuserSolPortailsNexusVersApisaraApresInitDepuisVoxelsServeur).CallDeferred();
	}

	/// <summary>Ordre fixe : Alpha, Beta, Omega, Delta — aligné sur <see cref="ConstantesDimensions.ToutesAlphaLike"/> usuel.</summary>
	private static readonly int[] _ordreDimensionsPortailVersApisara =
	{
		(int)DimensionJeu.Alpha,
		(int)DimensionJeu.Beta,
		(int)DimensionJeu.Omega,
		(int)DimensionJeu.Delta
	};

	/// <summary>
	/// Serveur ou solo : lit la surface voxel à (0,0) par dimension, applique aux portails, envoie aux clients distants.
	/// Les portails APISARA (<see cref="Portail.AncreSurApisara"/>) ne sont pas concernés.
	/// </summary>
	private void DiffuserSolPortailsNexusVersApisaraApresInitDepuisVoxelsServeur()
	{
		if (!UseArchitectureReseau)
			return;
		bool serveurOuSolo = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
		float yAlpha = -1f, yBeta = -1f, yOmega = -1f, yDelta = -1f;
		if (serveurOuSolo)
		{
			for (int i = 0; i < _ordreDimensionsPortailVersApisara.Length; i++)
			{
				int dim = _ordreDimensionsPortailVersApisara[i];
				Monde_Serveur srv = ObtenirServeurDimension(dim);
				float y = -1f;
				if (srv != null && srv.EssayerObtenirYSurfaceMondeDepuisVoxels(0, 0, out float ySurf))
					y = ySurf;
				switch (i)
				{
					case 0: yAlpha = y; break;
					case 1: yBeta = y; break;
					case 2: yOmega = y; break;
					case 3: yDelta = y; break;
				}
			}
			AppliquerYSolPortailsNexusVersApisaraAuxInstances(yAlpha, yBeta, yOmega, yDelta);
			if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
			{
				foreach (long peerId in Multiplayer.GetPeers())
					RpcId((int)peerId, nameof(RpcRecevoirYSolPortailsNexusVersApisara), yAlpha, yBeta, yOmega, yDelta);
			}
		}
	}

	private void AppliquerYSolPortailsNexusVersApisaraAuxInstances(float yAlpha, float yBeta, float yOmega, float yDelta)
	{
		float[] ys = { yAlpha, yBeta, yOmega, yDelta };
		for (int i = 0; i < _ordreDimensionsPortailVersApisara.Length; i++)
		{
			if (ys[i] < 0f)
				continue;
			int dim = _ordreDimensionsPortailVersApisara[i];
			if (!_racineParDimension.TryGetValue(dim, out Node3D racine) || racine == null) continue;
			foreach (Node enfant in racine.GetChildren())
			{
				if (enfant is not Portail portail || portail.AncreSurApisara)
					continue;
				if (!portail.Name.ToString().StartsWith("PortailVersApisara_", StringComparison.Ordinal))
					continue;
				portail.AppliquerSurfaceSolAutoritaireServeur(ys[i]);
				break;
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RpcRecevoirYSolPortailsNexusVersApisara(float yAlpha, float yBeta, float yOmega, float yDelta)
	{
		AppliquerYSolPortailsNexusVersApisaraAuxInstances(yAlpha, yBeta, yOmega, yDelta);
	}

	/// <summary>
	/// Après chargement : raycast vertical (ciel → fond) au XZ du portail « vers APISARA » pour obtenir la vraie hauteur du terrain mesh ;
	/// sinon on garde l’estimé procédural. Repositionne le nœud puis réaligne trigger / remblai.
	/// </summary>
	private void AffinerPortailVersApisaraSolParRaycast(Portail p, Vector2 xz, int dimensionId)
	{
		if (p == null || !GodotObject.IsInstanceValid(p) || p.AncreSurApisara) return;
		// Placement réel : <see cref="Portail.AlignerPortailSurSurface"/> (attente chunk + raycast ciel→sol).
		float enf = Mathf.Max(0f, p.EnfoncementBaseAuSolMetres);
		float yProc = EstimerAltitudeTerrainPortail(xz.X, xz.Y, dimensionId);
		p.GlobalPosition = new Vector3(xz.X, yProc - enf, xz.Y);
		p.AlignerPortailSurSurface();
	}

	/// <summary>Raycast monde vers le bas (même principe que <see cref="Portail.AlignerPortailSurSurface"/>), sans exclure de corps.</summary>
	private static bool EssayerObtenirAltitudeSolParRaycastXZ(Node3D noeudReferenceMonde, float x, float z, int dimensionId, out float ySol)
	{
		ySol = 0f;
		World3D world = noeudReferenceMonde.GetWorld3D();
		if (world?.DirectSpaceState == null) return false;

		float yRef = EstimerAltitudeTerrainPortail(x, z, dimensionId);
		float debutY = Mathf.Max(3200f, yRef + 500f);
		Vector3 debut = new Vector3(x, debutY, z);
		Vector3 fin = new Vector3(x, ConstantesDimensionAbysse.FondAbsolu, z);
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollisionMask = 1u;
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0 && hit.ContainsKey("position"))
		{
			ySol = ((Vector3)hit["position"]).Y;
			return true;
		}

		return false;
	}

	/// <summary>Surface à partir des voxels déjà chargés côté client (comme la flore) ; uniquement si <paramref name="dimensionIdPortail"/> est la dimension <b>localement</b> affichée.</summary>
	public bool EssayerObtenirYSurfaceTerrainDepuisVoxelsChunk(float mondeX, float mondeZ, int dimensionIdPortail, out float ySurface)
	{
		ySurface = 0f;
		if (_mondeClient == null || dimensionIdPortail != _dimensionLocaleActive) return false;
		return _mondeClient.EssayerObtenirYSurfaceMondeDepuisDonneesVoxel(mondeX, mondeZ, out ySurface);
	}

	/// <summary>Altitude monde approximative du sol (bruit procédural), même logique que le placement initial des portails.</summary>
	public static float EstimerAltitudeTerrainPortail(float x, float z, int dimensionId)
	{
		int seed = GameState.Instance?.SeedTerrainActuel ?? 19847;
		if (dimensionId == (int)DimensionJeu.Abysse)
		{
			int h = ApisaraHauteurTerrain.ObtenirHauteurSolMonde(Mathf.FloorToInt(x), Mathf.FloorToInt(z), seed);
			return h + 1f;
		}

		// Même ordre de grandeur qu’APISARA (+1) : la face du voxel surface est à h ; +10 plaçait le portail dans le ciel avant raycast.
		int hAlpha = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(x), Mathf.FloorToInt(z), seed);
		return hAlpha + 1f;
	}

	/// <summary>Repère XZ du portail « vers APISARA » : toujours <b>(0,0)</b> monde (chunk 0,0 chargé en priorité — alignement sol / raycast fiables).</summary>
	public static Vector2 ObtenirMeilleurXZPortailOrigineAlphaLike(int dimensionId, int seedTerrain)
	{
		_ = dimensionId;
		_ = seedTerrain;
		return Vector2.Zero;
	}

	/// <summary>Position XZ du portail vers APISARA pour une dimension Alpha-like (retour depuis l’ancre APISARA).</summary>
	public Vector2 ObtenirXZPortailVersApisaraPourDimension(int dimensionId)
	{
		if (_xzPortailVersApisaraParDimension.TryGetValue(dimensionId, out Vector2 v))
			return v;
		return ObtenirMeilleurXZPortailOrigineAlphaLike(dimensionId, SeedTerrain);
	}

	/// <summary>Delai de TP pendant la transition immersive portail (noir + vitesse), aligné avec l'orchestration visuelle client.</summary>
	public float ObtenirDelaiTeleportPendantTransitionPortail(float dureeTotaleSec)
	{
		float d = Mathf.Max(0.35f, dureeTotaleSec);
		float fadeIn = Mathf.Clamp(d * 0.30f, 0.22f, 1.0f);
		float fadeOut = Mathf.Clamp(d * 0.26f, 0.20f, 0.85f);
		float phaseVitesse = Mathf.Max(0.10f, d - fadeIn - fadeOut);
		return Mathf.Clamp(fadeIn + phaseVitesse * 0.50f, 0.20f, d);
	}

	/// <summary>Retourne un point d'arrivée à distance fixe devant l’ouverture du portail (membrane), sur le plan horizontal — évite un spawn sous l’arche ou « dans » le cadre.</summary>
	public bool EssayerObtenirPointArriveeDevantPortailNexus(int dimensionIdCible, PointCardinal liaison, bool versApisara, float distanceMetres, out Vector3 pointMonde)
	{
		pointMonde = Vector3.Zero;
		Portail portailCible = TrouverPortailNexusDimension(dimensionIdCible, liaison, versApisara);
		if (portailCible == null)
			return false;

		Transform3D gt = portailCible.GlobalTransform;
		// Pivot à l’ouverture (aligné trigger / visuel), pas seulement l’origine au sol du nœud.
		Vector3 pivotMembrane = gt * portailCible.PositionLocaleMembrane;
		Vector3 basisZ = gt.Basis.Z;
		// Côté « monde » après traversée : opposé à la direction d’entrée (−Z local = regard à travers le portail). Sortie = +Z local → projection horizontale de +Basis.Z (évite −Z qui replaçait sous l’arche / vers Apisara).
		Vector3 dirHoriz = new Vector3(basisZ.X, 0f, basisZ.Z);
		if (dirHoriz.LengthSquared() < 1e-8f)
		{
			Vector3 dir3 = basisZ;
			if (dir3.LengthSquared() < 1e-8f)
				dir3 = Vector3.Back;
			dirHoriz = dir3.Normalized();
		}
		else
			dirHoriz = dirHoriz.Normalized();

		// Sécurité globale Nexus: jamais moins de 20 m devant la membrane pour éviter une réapparition dans/près du portail.
		float distance = Mathf.Max(20f, distanceMetres);
		Vector3 cible = pivotMembrane + dirHoriz * distance;
		pointMonde = new Vector3(cible.X, pivotMembrane.Y, cible.Z);
		return true;
	}

	/// <summary>Applique un cooldown sur le portail de destination pour éviter les boucles TP immédiates.</summary>
	public void ArmerCooldownPortailNexus(int dimensionIdCible, PointCardinal liaison, bool versApisara, float cooldownSec)
	{
		Portail portailCible = TrouverPortailNexusDimension(dimensionIdCible, liaison, versApisara);
		if (portailCible != null)
			portailCible.ArmerCooldownPortailArrivee(cooldownSec);
	}

	private Portail TrouverPortailNexusDimension(int dimensionIdCible, PointCardinal liaison, bool versApisara)
	{
		if (!_racineParDimension.TryGetValue(dimensionIdCible, out Node3D racine) || racine == null)
			return null;
		Portail meilleur = null;
		float meilleureDistance2 = float.MaxValue;
		Vector3 cibleCanonique = Vector3.Zero;
		bool cibleCanoniqueValide = false;
		if (versApisara)
		{
			cibleCanonique = NexusCoords.ObtenirAncreApisara(liaison);
			cibleCanoniqueValide = true;
		}
		else if (ConstantesDimensions.EssayerObtenirInfo(dimensionIdCible, out var infoDim) && infoDim.EstAlphaLike)
		{
			Vector2 xz = ObtenirXZPortailVersApisaraPourDimension(dimensionIdCible);
			cibleCanonique = new Vector3(xz.X, 0f, xz.Y);
			cibleCanoniqueValide = true;
		}
		foreach (Node enfant in racine.GetChildren())
		{
			if (enfant is not Portail p || !GodotObject.IsInstanceValid(p))
				continue;
			if (versApisara)
			{
				if (!p.AncreSurApisara || p.Liaison != liaison)
					continue;
			}
			else
			{
				if (p.AncreSurApisara || p.Liaison != liaison)
					continue;
			}
			if (cibleCanoniqueValide)
			{
				Vector3 pp = p.GlobalPosition;
				Vector3 pc = new Vector3(pp.X, 0f, pp.Z);
				Vector3 cc = new Vector3(cibleCanonique.X, 0f, cibleCanonique.Z);
				float d2 = pc.DistanceSquaredTo(cc);
				if (d2 < meilleureDistance2)
				{
					meilleureDistance2 = d2;
					meilleur = p;
				}
				continue;
			}
			return p;
		}
		return meilleur;
	}

	private void MettreAJourVisibilitePortailsParDimension(int dimensionIdActif)
	{
		if (!UseArchitectureReseau || _racineParDimension.Count == 0) return;
		foreach (var kv in _racineParDimension)
		{
			if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value)) continue;
			foreach (Node enfant in kv.Value.GetChildren())
			{
				if (enfant is Portail portail)
					portail.DefinirVisibiliteSelonDimensionActive(kv.Key == dimensionIdActif);
			}
		}
	}

	private void MarquerPortailsDimensionPourRealignementSol(int dimensionId)
	{
		if (!_racineParDimension.TryGetValue(dimensionId, out Node3D racine) || racine == null) return;
		foreach (Node enfant in racine.GetChildren())
		{
			if (enfant is Portail p)
				p.MarquerAttenteNouveauRaycastSol();
		}
	}

	/// <summary>Chunks du client : priorité au sol sous les portails Nexus de la dimension (collision raycast).</summary>
	private void PrioriserChunksClientAutourPortailsDimension(int dimensionId)
	{
		if (_mondeClient == null || TailleChunk <= 0) return;
		if (dimensionId == (int)DimensionJeu.Abysse)
		{
			foreach (PointCardinal c in Enum.GetValues(typeof(PointCardinal)))
			{
				Vector3 a = NexusCoords.ObtenirAncreApisara(c);
				_mondeClient.ReserverChunkSpawnPrioritaire(WorldToChunkCoord(a.X, a.Z, TailleChunk));
			}
			return;
		}

		Vector2 xz = ObtenirXZPortailVersApisaraPourDimension(dimensionId);
		_mondeClient.ReserverChunkSpawnPrioritaire(WorldToChunkCoord(xz.X, xz.Y, TailleChunk));
	}

	private void InitialiserDimensionServeur(Monde_Serveur serveur, int dimensionId)
	{
		if (serveur == null) return;
		var nodeArbres = new Node3D { Name = $"Arbres_{dimensionId}" };
		AddChild(nodeArbres);
		_arbresParDimension[dimensionId] = nodeArbres;
		serveur.Initialiser(
			this,
			nodeArbres,
			(coord, sections) =>
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient.RecevoirChunkModifie(coord, sections);
			},
			(coord, donnees) => DistribuerChunkDimensionAuxPeers(dimensionId, coord, donnees),
			(coord, coordChunkY, inventaireFlore) =>
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient.RecevoirFloreModifie(coord, coordChunkY, inventaireFlore);
			},
			(pos, id) =>
			{
				serveur.RepliquerPaddingVoisins(pos, id);
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient.AppliquerVoxel(pos, id);
				if (Multiplayer.IsServer())
					DiffuserVoxelDimension(dimensionId, pos, id);
			},
			(coord) =>
			{
				if (Multiplayer.IsServer())
					DiffuserDestructionChunkDimension(dimensionId, coord);
			},
			ObtenirPositionJoueurOuSpawn,
			() => _dimensionLocaleActive,
			dimensionId
		);
	}

	private void MettreAJourVisibiliteArbresParDimension(int dimensionIdActif)
	{
		foreach (var kv in _arbresParDimension)
		{
			if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value))
				continue;
			kv.Value.Visible = (kv.Key == dimensionIdActif);
		}
	}

	/// <summary>Seule la dimension visitée simule terrain / eau / décharge ; les autres restent sur disque jusqu'au retour.</summary>
	private void MettreAJourSuspensionServeursDimensions(int dimensionActiveId)
	{
		foreach (var kv in _serveurParDimension)
		{
			if (kv.Value == null || !GodotObject.IsInstanceValid(kv.Value))
				continue;
			kv.Value.DefinirSimulationSuspendue(kv.Key != dimensionActiveId);
		}
	}

	private void SurPeerConnecteDimensions(long peerId)
	{
		DefinirDimensionPeer(peerId, (int)DimensionJeu.Alpha);
	}

	private void SurPeerDeconnecteDimensions(long peerId)
	{
		_dimensionParPeer.Remove(peerId);
		foreach (var kv in _attenteChunksParDimension)
		{
			foreach (var entree in kv.Value)
				entree.Value.Remove(peerId);
		}
	}

	private void SurDemandeChunkDimensionDemandee(int coordX, int coordY, int coordZ, int dimensionId, float obsX, float obsY, float obsZ, long peerId)
	{
		if (!UseArchitectureReseau || !Multiplayer.IsServer()) return;
		Monde_Serveur serveur = ObtenirServeurDimension(dimensionId);
		if (serveur == null) return;
		DefinirDimensionPeer(peerId, dimensionId);
		Vector2I coord = new Vector2I(coordX, coordZ);
		Vector3I coord3D = new Vector3I(coordX, coordY, coordZ);
		if (!_attenteChunksParDimension.TryGetValue(dimensionId, out var attentes))
		{
			attentes = new Dictionary<Vector3I, HashSet<long>>();
			_attenteChunksParDimension[dimensionId] = attentes;
		}
		if (!attentes.TryGetValue(coord3D, out var peers))
		{
			peers = new HashSet<long>();
			attentes[coord3D] = peers;
		}
		peers.Add(peerId);
		serveur.EnregistrerDemandeChunk(coord, coordY, new Vector3(obsX, obsY, obsZ));
	}

	private void DistribuerChunkDimensionAuxPeers(int dimensionId, Vector2I coord, DonneesChunk donnees)
	{
		if (!_attenteChunksParDimension.TryGetValue(dimensionId, out var attentes)) return;
		Vector3I cleExacte = new Vector3I(coord.X, donnees?.CoordChunkY ?? 0, coord.Y);
		HashSet<long> peers = null;
		if (!attentes.TryGetValue(cleExacte, out peers))
			return;
		if (peers == null || peers.Count == 0) return;
		var destinataires = new List<long>(peers);
		attentes.Remove(cleExacte);
		foreach (long peerId in destinataires)
		{
			if (ObtenirDimensionPeer(peerId) != dimensionId)
				continue;
			if (peerId == Multiplayer.GetUniqueId())
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient?.RecevoirDonneesChunk(coord, donnees);
				continue;
			}
			RpcId((int)peerId, nameof(RecevoirChunkDimensionRPC), dimensionId,
				coord.X, donnees?.CoordChunkY ?? 0, coord.Y, donnees.TailleChunk, donnees.HauteurMax,
				donnees.DensitiesQuantifiees ?? Array.Empty<byte>(),
				donnees.MaterialsFlat ?? Array.Empty<byte>(),
				donnees.DensitiesEauQuantifiees ?? Array.Empty<byte>(),
				donnees?.EstVideIntegral ?? false);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirChunkDimensionRPC(int dimensionId, int coordX, int coordY, int coordZ, int tailleChunk, int hauteurMax, byte[] densitiesPlates, byte[] materialsFlat, byte[] densitiesEauPlates, bool estVideIntegral)
	{
		if (_dimensionLocaleActive != dimensionId || _mondeClient == null) return;
		_mondeClient.RecevoirChunkDuServeurRPC(coordX, coordY, coordZ, tailleChunk, hauteurMax, densitiesPlates, materialsFlat, densitiesEauPlates, estVideIntegral);
	}

	private void DiffuserVoxelDimension(int dimensionId, Vector3I pos, byte id)
	{
		foreach (var kv in _dimensionParPeer)
		{
			long peerId = kv.Key;
			if (kv.Value != dimensionId) continue;
			if (peerId == Multiplayer.GetUniqueId())
				continue;
			_mondeClient?.RpcId((int)peerId, nameof(Monde_Client.AppliquerVoxelRPC), pos.X, pos.Y, pos.Z, (int)id);
		}
	}

	private void DiffuserDestructionChunkDimension(int dimensionId, Vector2I coord)
	{
		foreach (var kv in _dimensionParPeer)
		{
			long peerId = kv.Key;
			if (kv.Value != dimensionId) continue;
			if (peerId == Multiplayer.GetUniqueId())
			{
				if (_dimensionLocaleActive == dimensionId)
					_mondeClient?.OrdonnerDestructionChunkRPC(coord.X, coord.Y);
				continue;
			}
			_mondeClient?.RpcId((int)peerId, nameof(Monde_Client.OrdonnerDestructionChunkRPC), coord.X, coord.Y);
		}
	}

	private Vector3 ObtenirPointTeleportDimension(int dimensionId)
	{
		return ConstantesDimensions.ObtenirInfoOuAlpha(dimensionId).PointTeleportDefaut;
	}

	/// <summary>Retourne la position où réapparaître dans la dimension cible : si une position y a été mémorisée
	/// (visite précédente sauvegardée dans <see cref="_positionsSauvegardeesParDimension"/>), on la réutilise ;
	/// sinon on tombe sur le point canonique de téléportation.</summary>
	private Vector3 ObtenirPointTeleportAvecMemoireDimension(int dimensionId)
	{
		if (_positionsSauvegardeesParDimension.TryGetValue(dimensionId, out Vector3 positionMemorisee))
			return positionMemorisee;
		return ObtenirPointTeleportDimension(dimensionId);
	}

	public bool EnvoyerCommandeAdminChat(string commande)
	{
		if (!UseArchitectureReseau || _networkManager == null) return false;
		string cmd = (commande ?? "").Trim();
		if (string.IsNullOrEmpty(cmd)) return false;
		_networkManager.EnvoyerCommandeAdminAuServeur(cmd);
		return true;
	}

	public bool DemanderInjectionItemCreatif(SlotInventaire slot)
	{
		if (!UseArchitectureReseau || _networkManager == null || slot.EstVide) return false;
		_networkManager.EnvoyerDemandeInjectionItemCreatif(slot);
		return true;
	}

	private void SurCommandeAdminDemandee(string commande, long peerId)
	{
		if (!UseArchitectureReseau || !Multiplayer.IsServer()) return;
		string cmd = (commande ?? "").Trim();
		Monde_Serveur serveurCourant = ObtenirServeurDimension(ObtenirDimensionPeer(peerId)) ?? _mondeServeur;
		if (serveurCourant == null) return;

		if (serveurCourant.EssayerBootstrapAdmin(peerId, cmd, out bool succesBootstrap, out string msgBootstrap))
		{
			if (succesBootstrap)
			{
				SynchroniserPeerAdminToutesDimensions(peerId);
				Monde_Serveur pourPersist = _mondeServeurAlpha ?? serveurCourant;
				pourPersist.PersisterWhitelistAdmin();
			}
			EnvoyerMessageAdminAuPeer(peerId, msgBootstrap);
			return;
		}

		if (cmd.StartsWith("/DIMANASIO", StringComparison.OrdinalIgnoreCase))
		{
			if (!serveurCourant.EstPeerAdmin(peerId))
			{
				EnvoyerMessageAdminAuPeer(peerId, "Accès refusé: vous n'êtes pas admin.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO APISARA", StringComparison.OrdinalIgnoreCase))
			{
				TransfererPeerVersDimension(peerId, (int)DimensionJeu.Abysse, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Abysse), $"Transfert vers {ConstantesDimensionAbysse.Apisara}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO ARAPA", StringComparison.OrdinalIgnoreCase))
			{
				TransfererPeerVersDimension(peerId, (int)DimensionJeu.Alpha, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Alpha), $"Retour vers {ConstantesDimensions.NomAlpha}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO PETA", StringComparison.OrdinalIgnoreCase))
			{
				TransfererPeerVersDimension(peerId, (int)DimensionJeu.Beta, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Beta), $"Transfert vers {ConstantesDimensions.NomBeta}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO OMEGA", StringComparison.OrdinalIgnoreCase))
			{
				TransfererPeerVersDimension(peerId, (int)DimensionJeu.Omega, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Omega), $"Transfert vers {ConstantesDimensions.NomOmega}.");
				return;
			}
			if (string.Equals(cmd, "/DIMANASIO DERATA", StringComparison.OrdinalIgnoreCase))
			{
				TransfererPeerVersDimension(peerId, (int)DimensionJeu.Delta, ObtenirPointTeleportAvecMemoireDimension((int)DimensionJeu.Delta), $"Transfert vers {ConstantesDimensions.NomDelta}.");
				return;
			}
			EnvoyerMessageAdminAuPeer(peerId, "Commande dimension inconnue.");
			return;
		}

		if (!serveurCourant.EssayerTraiterCommandeAdmin(peerId, commande, out bool modeCreatif, out bool noclip, out string messageServeur))
		{
			if (!string.IsNullOrWhiteSpace(messageServeur))
				EnvoyerMessageAdminAuPeer(peerId, messageServeur);
			return;
		}

		if (peerId == Multiplayer.GetUniqueId())
			AppliquerEtatModeCreatifLocal(modeCreatif, noclip, messageServeur);
		else
		{
			RpcId((int)peerId, nameof(RecevoirEtatModeCreatifRPC), modeCreatif ? 1 : 0, noclip ? 1 : 0, messageServeur ?? "");
		}
	}

	/// <summary>Réplique l’ID admin sur chaque <see cref="Monde_Serveur"/> (dimensions distinctes, même fichier whitelist).</summary>
	private void SynchroniserPeerAdminToutesDimensions(long peerId)
	{
		foreach (var kv in _serveurParDimension)
			kv.Value?.AjouterPeerAdmin(peerId);
	}

	private void EnvoyerMessageAdminAuPeer(long peerId, string message)
	{
		if (string.IsNullOrWhiteSpace(message)) return;
		if (peerId == Multiplayer.GetUniqueId())
			Joueur.AlerteSqueletteBoiteNoire(message);
		else
			RpcId((int)peerId, nameof(RecevoirMessageChatAdminRPC), message ?? "");
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirMessageChatAdminRPC(string message)
	{
		if (!string.IsNullOrWhiteSpace(message))
			Joueur.AlerteSqueletteBoiteNoire(message);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirEtatModeCreatifRPC(int modeCreatif, int noclip, string messageServeur)
	{
		AppliquerEtatModeCreatifLocal(modeCreatif != 0, noclip != 0, messageServeur);
	}

	private void AppliquerEtatModeCreatifLocal(bool actif, bool noclip, string messageServeur)
	{
		if (_joueur is Joueur j)
			j.DefinirModeCreatifDepuisServeur(actif, noclip);
		if (!string.IsNullOrWhiteSpace(messageServeur))
			Joueur.AlerteSqueletteBoiteNoire(messageServeur);
	}

	private void SurInjectionItemCreatifDemandee(int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, long peerId)
	{
		if (!UseArchitectureReseau || !Multiplayer.IsServer()) return;
		Monde_Serveur serveurCourant = ObtenirServeurDimension(ObtenirDimensionPeer(peerId)) ?? _mondeServeur;
		if (serveurCourant == null) return;
		if (!serveurCourant.EssayerConstruireSlotInjectionCreatif(peerId, id, indexMorphologique, indexChimique, indexTaille, indexBotanique, out SlotInventaire slot, out string messageServeur))
		{
			if (!string.IsNullOrWhiteSpace(messageServeur))
				EnvoyerMessageAdminAuPeer(peerId, messageServeur);
			return;
		}

		if (peerId == Multiplayer.GetUniqueId())
			AppliquerInjectionItemCreatifLocale(slot, messageServeur);
		else
		{
			RpcId((int)peerId, nameof(RecevoirInjectionItemCreatifRPC),
				slot.ID, slot.IndexMorphologique, slot.IndexChimique, slot.IndexTaille, (int)slot.IndexBotanique,
				slot.IndexTailleLameRoche, slot.Quantite, messageServeur ?? "");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirInjectionItemCreatifRPC(int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, int indexTailleLameRoche, int quantite, string messageServeur)
	{
		SlotInventaire slot = new SlotInventaire
		{
			ID = id,
			IndexMorphologique = indexMorphologique,
			IndexChimique = indexChimique,
			IndexTaille = indexTaille,
			IndexBotanique = (byte)Mathf.Clamp(indexBotanique, 0, 255),
			IndexTailleLameRoche = indexTailleLameRoche,
			Quantite = quantite
		};
		AppliquerInjectionItemCreatifLocale(slot, messageServeur);
	}

	private void AppliquerInjectionItemCreatifLocale(SlotInventaire slot, string messageServeur)
	{
		if (_joueur is Joueur j)
			j.InjecterSlotCreatifAdmin(slot);
		if (!string.IsNullOrWhiteSpace(messageServeur))
			Joueur.AlerteSqueletteBoiteNoire(messageServeur);
	}

	private void TransfererPeerVersDimension(long peerId, int dimensionCible, Vector3 positionCible, string messageServeur)
	{
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
		int dimensionActuelle = ObtenirDimensionPeer(peerId);
		if (peerId == Multiplayer.GetUniqueId())
		{
			Vector3 positionAvantTp = JoueurReferenceValide() ? _joueur.GlobalPosition : positionCible;
			GameState.Instance?.SauvegarderPositionJoueur(positionAvantTp);
			// Mémorise la position actuelle dans la dim qu'on quitte (clé = dimensionActuelle).
			_positionsSauvegardeesParDimension[dimensionActuelle] = positionAvantTp;
			SauvegarderSessionJoueur(dimensionActuelle, positionAvantTp);
			if (_joueur is Joueur j && ConstantesDimensions.EssayerObtenirInfo(dimensionActuelle, out var infoCourante))
				SauvegarderPersistanceCompleteMonde($"TransfererPeer.quit.{infoCourante.NomCanonique}");
			else if (_joueur is Joueur jFallback)
				jFallback.SauvegarderEtatPersistantJoueurSeulement();
		}
		DefinirDimensionPeer(peerId, dimensionCible);
		if (peerId == Multiplayer.GetUniqueId())
		{
			AppliquerChangementDimensionLocale(dimensionCible, positionCible, messageServeur);
			return;
		}
		RpcId((int)peerId, nameof(RecevoirTransfertDimensionRPC), dimensionCible, positionCible.X, positionCible.Y, positionCible.Z, messageServeur ?? "");
	}

	/// <summary>Peer réseau associé au nœud joueur (autorité), ou l’identifiant local en solo.</summary>
	public long ObtenirPeerIdPourNoeudJoueur(Joueur j)
	{
		if (j == null) return 1;
		if (!Multiplayer.HasMultiplayerPeer())
			return Multiplayer.GetUniqueId();
		int auth = j.GetMultiplayerAuthority();
		if (auth >= 0)
			return auth;
		return Multiplayer.GetUniqueId();
	}

	/// <summary>Remblai voxel sous un portail (serveur / solo) : uniquement l’air entre le sol existant et les pieds, sur une profondeur max (pas de colonne pleine).</summary>
	public void DemanderRemplissageSocleSousPortail(Vector3 centrePortailMonde, int dimensionId, float ySurfaceTerrain, int rayonDemiCoteVoxels, int profondeurMaxVersLeBas)
	{
		if (!UseArchitectureReseau)
			return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
			return;
		Monde_Serveur serveur = ObtenirServeurDimension(dimensionId);
		serveur?.RemplirSocleSousPortail(centrePortailMonde, ySurfaceTerrain, rayonDemiCoteVoxels, profondeurMaxVersLeBas);
	}

	/// <summary>Transfert déclenché par un <see cref="Portail"/> : dimension cible + XZ logique (Y affiné par raycast vertical après coup).</summary>
	public void TransfererJoueurViaPortail(Node3D joueur, int dimensionIdCible, Vector3 positionCibleXZ, string messageServeur = null)
	{
		if (joueur is not Joueur j || !GodotObject.IsInstanceValid(j)) return;
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;

		float yRef = ConstantesDimensions.ObtenirInfoOuAlpha(dimensionIdCible).PointTeleportDefaut.Y;
		var posInitiale = new Vector3(positionCibleXZ.X, yRef, positionCibleXZ.Z);
		long peerId = ObtenirPeerIdPourNoeudJoueur(j);
		TransfererPeerVersDimension(peerId, dimensionIdCible, posInitiale, messageServeur ?? "Transit dimensionnel.");
		if (peerId == Multiplayer.GetUniqueId())
		{
			float ax = positionCibleXZ.X;
			float az = positionCibleXZ.Z;
			Callable.From(() => AlignerJoueurPortailSurSolDeferred(ax, az)).CallDeferred();
		}
	}

	private void AlignerJoueurPortailSurSolDeferred(float mondeX, float mondeZ, int tentative = 0)
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur)) return;
		var approx = new Vector3(mondeX, 0f, mondeZ);
		if (EssayerTrouverSolParRaycast(approx, out Vector3 pointSol))
		{
			// Même règle que <see cref="FinaliserSpawnInitialAuSol"/> : pieds sur la surface du raycast, sans décalage arbitraire.
			if (_joueur is Joueur jo)
				_joueur.GlobalPosition = new Vector3(mondeX, jo.CalculerYOriginePourPiedsSurSurface(pointSol.Y), mondeZ);
			else
				_joueur.GlobalPosition = pointSol + Vector3.Up * 1.2f;
			_joueur.Velocity = Vector3.Zero;
			FinaliserSortiePortailApresAlignement();
			return;
		}
		// Tant que la collision n’est pas prête : même repli hauteur voxel qu’au spawn initial (évite rester sous le portail / dans le vide).
		Vector3 repliTerrain = AssurerSpawnAuDessusDuSol(new Vector3(mondeX, ConstantesDimensions.ObtenirInfoOuAlpha(_dimensionLocaleActive).PointTeleportDefaut.Y, mondeZ));
		_joueur.GlobalPosition = repliTerrain;
		_joueur.Velocity = Vector3.Zero;
		if (tentative < 18 && GetTree() != null)
		{
			float delai = 0.12f + tentative * 0.07f;
			GetTree().CreateTimer(delai).Timeout += () => AlignerJoueurPortailSurSolDeferred(mondeX, mondeZ, tentative + 1);
			return;
		}
		GD.PushWarning("ZERO-K Portail : raycast sol sans impact après attente, position hauteur voxel conservée.");
	}

	private void FinaliserSortiePortailApresAlignement()
	{
		// Dès qu'un alignement sol est validé, on relâche immédiatement le verrou de TP.
		_gateTpDimensionActif = false;
		_secondesGateTpDimension = 0.0;
		EjecterJoueurHorsMembranePortailSiNecessaire();
	}

	private void EjecterJoueurHorsMembranePortailSiNecessaire()
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return;
		if (!_racineParDimension.TryGetValue(_dimensionLocaleActive, out Node3D racine) || racine == null)
			return;

		Portail portailProche = null;
		float meilleureDistance2 = float.MaxValue;
		foreach (Node enfant in racine.GetChildren())
		{
			if (enfant is not Portail p || !GodotObject.IsInstanceValid(p))
				continue;
			Vector3 centreMembrane = p.GlobalTransform * p.PositionLocaleMembrane;
			float d2 = centreMembrane.DistanceSquaredTo(_joueur.GlobalPosition);
			if (d2 < meilleureDistance2)
			{
				meilleureDistance2 = d2;
				portailProche = p;
			}
		}

		if (portailProche == null)
			return;

		Vector3 centre = portailProche.GlobalTransform * portailProche.PositionLocaleMembrane;
		Vector2 deltaJoueur = new Vector2(_joueur.GlobalPosition.X - centre.X, _joueur.GlobalPosition.Z - centre.Z);
		float rayonSecurite = Mathf.Max(6f, portailProche.RayonTriggerMetres * 0.95f);
		if (deltaJoueur.LengthSquared() > rayonSecurite * rayonSecurite)
			return;

		Vector3 axeSortie3 = portailProche.GlobalTransform.Basis.Z;
		Vector2 axeSortie = new Vector2(axeSortie3.X, axeSortie3.Z);
		if (axeSortie.LengthSquared() < 1e-6f)
			axeSortie = Vector2.Right;
		else
			axeSortie = axeSortie.Normalized();

		float signe = Mathf.Sign(deltaJoueur.Dot(axeSortie));
		if (Mathf.IsZeroApprox(signe))
			signe = 1f;

		float distanceSortie = Mathf.Max(22f, Mathf.Max(portailProche.DistanceApparitionDevantPortailMetres, portailProche.RayonTriggerMetres + 6f));
		Vector3 xzCible = new Vector3(
			centre.X + axeSortie.X * distanceSortie * signe,
			_joueur.GlobalPosition.Y,
			centre.Z + axeSortie.Y * distanceSortie * signe);

		if (EssayerTrouverSolParRaycast(new Vector3(xzCible.X, 0f, xzCible.Z), out Vector3 pointSol))
		{
			if (_joueur is Joueur jo)
				_joueur.GlobalPosition = new Vector3(xzCible.X, jo.CalculerYOriginePourPiedsSurSurface(pointSol.Y), xzCible.Z);
			else
				_joueur.GlobalPosition = pointSol + Vector3.Up * 1.2f;
		}
		else
		{
			_joueur.GlobalPosition = AssurerSpawnAuDessusDuSol(new Vector3(xzCible.X, _joueur.GlobalPosition.Y, xzCible.Z));
		}

		_joueur.Velocity = Vector3.Zero;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RecevoirTransfertDimensionRPC(int dimensionId, float posX, float posY, float posZ, string messageServeur)
	{
		AppliquerChangementDimensionLocale(dimensionId, new Vector3(posX, posY, posZ), messageServeur);
		Callable.From(() => AlignerJoueurPortailSurSolDeferred(posX, posZ)).CallDeferred();
	}

	private void AppliquerChangementDimensionLocale(int dimensionId, Vector3 positionCible, string messageServeur, bool rechargerPersistanceDimension = true)
	{
		_dimensionLocaleActive = dimensionId;
		DefinirDimensionPeer(Multiplayer.GetUniqueId(), dimensionId);
		_mondeServeur = ObtenirServeurDimension(dimensionId) ?? _mondeServeurAlpha;
		MettreAJourSuspensionServeursDimensions(dimensionId);
		_mondeServeur?.ForcerPulseReveilPierres();
		_mondeClient?.DefinirDimensionReseauActive(dimensionId);
		_positionReferenceTransfertDimension = positionCible;
		_gateTpDimensionActif = true;
		_secondesGateTpDimension = 0.0;
		_cooldownPulseReveilPierresTp = 0.0;
		MarquerPortailsDimensionPourRealignementSol(dimensionId);
		PrioriserChunksClientAutourPortailsDimension(dimensionId);
		ReinitialiserEmerukedesiParotaromaStage1();
		MettreAJourVisibiliteArbresParDimension(dimensionId);
		MettreAJourVisibilitePortailsParDimension(dimensionId);
		if (_joueur != null && GodotObject.IsInstanceValid(_joueur))
		{
			ReparenterNoeudDansDimension(_joueur, dimensionId, positionCible);
			_joueur.Velocity = Vector3.Zero;
		}
		_mondeClient?.ReinitialiserTousLesChunksLocaux();
		// Après reset des chunks : respawn objets/faune (portail). Au boot hors Alpha, la phase B le fait quand le sol est prêt.
		if (rechargerPersistanceDimension && _joueur is Joueur jDiffere)
			jDiffere.CallDeferred(Joueur.NomMethodeRechargerPersistanceDimensionDifferee);
		_chargementAbysseEnCours = dimensionId == (int)DimensionJeu.Abysse;
		_chargementAbysseEnCours = false; // Abysse suit le chargement Alpha (pas de verrou dédié).
		_secondesStabiliteAbyssePret = 0.0;
		_secondesVerrouAbysse = 0.0;
		_cooldownRearmementVerrouAbysse = 0.0;
		_verrouMarcheAbysseActif = false;
		_secondesVerrouMarcheAbysse = 0.0;
		_secondesStabiliteMarcheAbysse = 0.0;
		if (_overlayChargement != null)
		{
			_overlayChargement.Visible = true;
			_secondesOverlayChargement = 0.0;
		}
		if (_labelChargementPrincipal != null)
			_labelChargementPrincipal.Text = "Chargement du monde...";
		if (_mondeClient != null)
		{
			Vector2I chunkSpawn = WorldToChunkCoord(positionCible, TailleChunk);
			_mondeClient.ReserverChunkSpawnPrioritaire(chunkSpawn);
		}
		EnvoyerFuseauHoraireAuPeer(Multiplayer.GetUniqueId());
		MettreAJourAtmosphereAbysseLocale(dimensionId);
		if (!string.IsNullOrWhiteSpace(messageServeur))
			Joueur.AlerteSqueletteBoiteNoire(messageServeur);
	}

	private void MettreAJourAtmosphereAbysseLocale(int dimensionIdActif)
	{
		if (_mondeServeurAbysse is not Gestionnaire_Abysse gestionnaireAbysse)
			return;

		bool apisara = dimensionIdActif == (int)DimensionJeu.Abysse;
		gestionnaireAbysse.DefinirAtmosphereAbysseActive(apisara);

		var we = GetParent()?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (we == null)
			return;

		Godot.Environment envApisara = gestionnaireAbysse.ObtenirEnvironmentAbysse();
		if (apisara)
		{
			if (_environnementSauvegardeHorsApisara == null && we.Environment != null && !ReferenceEquals(we.Environment, envApisara))
				_environnementSauvegardeHorsApisara = we.Environment;
			we.Environment = envApisara;
		}
		else if (_environnementSauvegardeHorsApisara != null)
		{
			we.Environment = _environnementSauvegardeHorsApisara;
		}
	}

	/// <summary>Volume océan dédié à la détection (remous/éclaboussures), sans override physique global.</summary>
	private void CreerAreaOcean()
	{
		float demiRayon = RayonMondeChunks * TailleChunk;
		float hauteurZone = NiveauEauOcean + 500f; // Couvre jusqu'en profondeur -500
		var ocean = new Area3D { Name = "Ocean_Physique" };
		// IMPORTANT : pas d'effet physique global ici.
		// Les forces eau sont gérées par chaque corps selon son ratio d'immersion.
		ocean.GravitySpaceOverride = Area3D.SpaceOverride.Disabled;
		ocean.Gravity = 0f;
		ocean.GravityDirection = new Vector3(0, -1, 0);
		ocean.GravityPoint = false;
		ocean.LinearDamp = 0f;
		ocean.LinearDampSpaceOverride = Area3D.SpaceOverride.Disabled;
		ocean.AngularDamp = 0f;
		ocean.AngularDampSpaceOverride = Area3D.SpaceOverride.Disabled;
		ocean.Priority = 100; // Priorité haute sur le monde par défaut

		var col = new CollisionShape3D();
		col.Shape = new BoxShape3D { Size = new Vector3(demiRayon * 2f, hauteurZone, demiRayon * 2f) };
		ocean.AddChild(col);
		ocean.Position = new Vector3(0, (NiveauEauOcean - 500f) / 2f, 0); // Centre du volume
		ocean.BodyEntered += SurCorpsEntreOcean;
		ocean.BodyExited += SurCorpsSortOcean;
		AddChild(ocean);
		_oceanPhysique = ocean;
	}

	private void SurCorpsEntreOcean(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return;
		if (!CorpsAuContactEauVoxel(corps)) return;

		ulong id = corps.GetInstanceId();
		if (!_corpsDansOcean.Add(id)) return;

		if (corps is CharacterBody3D or RigidBody3D)
			AssurerEffetRemousSuiviPour(corps, id);

		if (corps is RigidBody3D rb)
		{
			// Seulement un objet qui tombe (vitesse verticale descendante suffisante).
			float vitesseChute = -rb.LinearVelocity.Y;
			if (vitesseChute < 2.0f) return;
			float intensite = Mathf.Clamp(vitesseChute / 18f, 0.35f, 1.35f);
			Vector3 impactSurface = rb.GlobalPosition;
			impactSurface.Y = NiveauEauOcean + 0.04f;
			CreerEclaboussureSurface(impactSurface, intensite);
		}
	}

	private void SurCorpsSortOcean(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return;
		ulong id = corps.GetInstanceId();
		_corpsDansOcean.Remove(id);
		RetirerEffetRemousSuivi(id);
	}

	private void AssurerEffetRemousSuiviPour(Node3D corps, ulong id)
	{
		if (_effetsRemousParCorps.ContainsKey(id)) return;
		AssurerConteneurEffetsEau();
		if (_conteneurEffetsEau == null || !GodotObject.IsInstanceValid(_conteneurEffetsEau)) return;

		var p = new GpuParticles3D
		{
			Name = $"Remous_{id}",
			Amount = 10,
			Lifetime = 0.50f,
			OneShot = false,
			Emitting = false
		};
		var mat = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, 1f, 0f),
			Spread = 24f,
			InitialVelocityMin = 0.18f,
			InitialVelocityMax = 0.58f,
			Gravity = new Vector3(0f, -1.2f, 0f),
			ScaleMin = 0.08f,
			ScaleMax = 0.15f,
			DampingMin = 0.7f,
			DampingMax = 1.2f
		};
		p.ProcessMaterial = mat;
		p.DrawPass1 = new QuadMesh { Size = new Vector2(0.06f, 0.06f) };
		p.MaterialOverride = ObtenirMaterielEclaboussureEau();
		_conteneurEffetsEau.AddChild(p);
		if (p.IsInsideTree())
			p.GlobalPosition = new Vector3(corps.GlobalPosition.X, ObtenirNiveauSurfaceEau(), corps.GlobalPosition.Z);
		_corpsSuiviRemous[id] = corps;
		_effetsRemousParCorps[id] = p;
	}

	private void RetirerEffetRemousSuivi(ulong id)
	{
		_corpsSuiviRemous.Remove(id);
		if (_effetsRemousParCorps.TryGetValue(id, out var p))
		{
			_effetsRemousParCorps.Remove(id);
			if (p != null && GodotObject.IsInstanceValid(p))
				p.QueueFree();
		}
	}

	private void MettreAJourEffetsRemousSuivis()
	{
		if (_effetsRemousParCorps.Count == 0) return;
		float ySurface = ObtenirNiveauSurfaceEau() + 0.03f;
		_tmpRemousASupprimer.Clear();
		foreach (var kv in _effetsRemousParCorps)
		{
			ulong id = kv.Key;
			GpuParticles3D p = kv.Value;
			if (p == null || !GodotObject.IsInstanceValid(p))
			{
				_tmpRemousASupprimer.Add(id);
				continue;
			}
			if (!_corpsSuiviRemous.TryGetValue(id, out Node3D corps) || corps == null || !GodotObject.IsInstanceValid(corps) || !_corpsDansOcean.Contains(id))
			{
				_tmpRemousASupprimer.Add(id);
				continue;
			}
			if (!corps.IsInsideTree() || !p.IsInsideTree())
			{
				_tmpRemousASupprimer.Add(id);
				continue;
			}

			float vitesseHoriz = 0f;
			if (corps is CharacterBody3D cb)
			{
				Vector3 v = cb.Velocity;
				vitesseHoriz = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
			}
			else if (corps is RigidBody3D rb)
			{
				Vector3 v = rb.LinearVelocity;
				vitesseHoriz = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
			}

			bool auContactEau = CorpsAuContactEauVoxel(corps);
			bool actif = vitesseHoriz > 0.45f && auContactEau;
			p.GlobalPosition = new Vector3(corps.GlobalPosition.X, ySurface, corps.GlobalPosition.Z);
			p.AmountRatio = actif ? Mathf.Clamp((vitesseHoriz - 0.45f) / 3.8f, 0.08f, 0.72f) : 0f;
			p.Emitting = actif;
		}

		for (int i = 0; i < _tmpRemousASupprimer.Count; i++)
			RetirerEffetRemousSuivi(_tmpRemousASupprimer[i]);
	}

	/// <summary>Vérité gameplay : le corps est dans l'eau uniquement si ses voxels de contact détectent l'eau.</summary>
	private bool CorpsAuContactEauVoxel(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return false;
		Vector3 pos = corps.GlobalPosition;
		// Échantillons pieds + centre bas pour éviter les faux positifs en grottes sèches sous le niveau de mer.
		return EstPointDansEau(pos + new Vector3(0f, -0.95f, 0f))
			|| EstPointDansEau(pos + new Vector3(0f, -0.55f, 0f))
			|| EstPointDansEau(pos + new Vector3(0f, -0.15f, 0f));
	}

	private StandardMaterial3D ObtenirMaterielEclaboussureEau()
	{
		if (_materielEclaboussureEau != null) return _materielEclaboussureEau;
		_materielEclaboussureEau = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(0.82f, 0.93f, 1f, 0.82f),
			NoDepthTest = false,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
		};
		return _materielEclaboussureEau;
	}

	private void AssurerConteneurEffetsEau()
	{
		if (_conteneurEffetsEau != null && GodotObject.IsInstanceValid(_conteneurEffetsEau)) return;
		_conteneurEffetsEau = new Node3D { Name = "Effets_Eau" };
		AddChild(_conteneurEffetsEau);
	}

	private void CreerEclaboussureSurface(Vector3 centre, float intensite)
	{
		AssurerConteneurEffetsEau();
		if (_conteneurEffetsEau == null || !GodotObject.IsInstanceValid(_conteneurEffetsEau)) return;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Engine.GetPhysicsFrames() * 73856093u + (uint)Mathf.Abs((int)centre.X * 19349663) + (uint)Mathf.Abs((int)centre.Z * 83492791));
		int nbGouttes = Mathf.Clamp(Mathf.RoundToInt(10 + 18 * intensite), 10, 34);
		Material mat = ObtenirMaterielEclaboussureEau();

		for (int i = 0; i < nbGouttes; i++)
		{
			float angle = rng.RandfRange(0f, Mathf.Tau);
			float rayon = rng.RandfRange(0.06f, 0.18f + 0.22f * intensite);
			float montee = rng.RandfRange(0.08f, 0.32f + 0.25f * intensite);
			float dureeMontee = rng.RandfRange(0.10f, 0.18f);
			float dureeDescente = rng.RandfRange(0.12f, 0.24f);
			float taille = rng.RandfRange(0.028f, 0.05f + 0.03f * intensite);

			var goutte = new MeshInstance3D
			{
				Mesh = new QuadMesh { Size = new Vector2(taille, taille) },
				MaterialOverride = mat
			};
			_conteneurEffetsEau.AddChild(goutte);
			goutte.GlobalPosition = centre + new Vector3(rng.RandfRange(-0.04f, 0.04f), 0f, rng.RandfRange(-0.04f, 0.04f));

			Vector3 cibleMontee = centre + new Vector3(Mathf.Cos(angle) * rayon * 0.55f, montee, Mathf.Sin(angle) * rayon * 0.55f);
			Vector3 cibleDescente = centre + new Vector3(Mathf.Cos(angle) * rayon, rng.RandfRange(0.0f, 0.03f), Mathf.Sin(angle) * rayon);

			// Tween rattaché à la goutte : évite tweens orphelins sous Gestionnaire_Monde si la scène change avant la fin.
			var tw = goutte.CreateTween();
			tw.TweenProperty(goutte, "global_position", cibleMontee, dureeMontee).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(goutte, "global_position", cibleDescente, dureeDescente).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.Parallel().TweenProperty(goutte, "scale", Vector3.Zero, dureeMontee + dureeDescente).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(goutte))
					goutte.QueueFree();
			};
		}
	}

	private void EnvoyerFuseauHoraireAuPeer(long peerId)
	{
		if (!Multiplayer.IsServer()) return;
		var soleil = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (soleil == null) return;
		int dimension = ObtenirDimensionPeer(peerId);
		double offset = ObtenirServeurDimension(dimension)?.FuseauHoraireHeures ?? 0.0;
		soleil.RpcId(peerId, nameof(Cycle_Solaire.DefinirDecalageHoraire), offset);
		bool forcerJour = dimension == (int)DimensionJeu.Abysse;
		soleil.RpcId(peerId, nameof(Cycle_Solaire.ConfigurerHeureFixeDimension), forcerJour ? 1 : 0, 13.5);
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

	public override void _Process(double delta)
	{
		ulong debutProcessUs = ActiverProfilagePerfGestionnaire ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownLogAutosaveDiag = Mathf.Max(0f, _cooldownLogAutosaveDiag - (float)delta);
		_cooldownDiagnosticCollisionAbysse = Math.Max(0.0, _cooldownDiagnosticCollisionAbysse - delta);
		_cooldownDrainProfilage += (float)delta;
		TraiterWarmupShadersProgressif((float)delta);
		SurveillerDeriveRuntime((float)delta);
		TraiterAutoHybrideGraphique((float)delta);
		MettreAJourEffetsRemousSuivis();
		if (ActiverAutosauvegarde && IntervalleAutosauvegardeSecondes > 0f)
		{
			_secondesDepuisAutosauvegarde += delta;
			if (_secondesDepuisAutosauvegarde >= IntervalleAutosauvegardeSecondes)
			{
				_secondesDepuisAutosauvegarde = 0;
				ExecuterAutosauvegardeProgressive();
			}
		}

		// Verrou anti-chute : tant que le spawn n'est pas aligné au sol, on ancre le joueur au point de spawn.
		if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol && _joueur != null)
		{
			_joueur.GlobalPosition = _spawnInitialEnAttente;
			_joueur.Velocity = Vector3.Zero;
			_joueur.Visible = false;
		}

		// Garde-fou profondeur extrême Abysse : stabilise l'état physique au fond absolu.
		if (_joueur != null && _dimensionLocaleActive == (int)DimensionJeu.Abysse)
		{
			const float fondAbsolu = ConstantesDimensionAbysse.FondAbsolu;
			float y = _joueur.GlobalPosition.Y;
			// Amorti avant le plancher : évite une décélération infinie « écrasement » sur un seul tick.
			if (y < fondAbsolu + 42f && y > fondAbsolu && _joueur.Velocity.Y < -8f)
				_joueur.Velocity = new Vector3(_joueur.Velocity.X, Mathf.Max(_joueur.Velocity.Y, -22f), _joueur.Velocity.Z);
			if (y <= fondAbsolu)
			{
				_joueur.GlobalPosition = new Vector3(_joueur.GlobalPosition.X, fondAbsolu, _joueur.GlobalPosition.Z);
				_joueur.Velocity = new Vector3(_joueur.Velocity.X * 0.35f, 0f, _joueur.Velocity.Z * 0.35f);
			}
		}
		MettreAJourEmerukedesiParotaromaStage1(delta);

		bool spawnPretActuel = EstSpawnPret();
		bool spawnPretEtAligneActuel = spawnPretActuel && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
		Vector3 pointRefSpawn = ObtenirPointReferenceSpawn();
		bool cardinauxPrets = ChunkEtVoisinsCardinauxPretsAuPoint(pointRefSpawn);
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse && _cooldownDiagnosticCollisionAbysse <= 0.0)
		{
			JournaliserDiagnosticCollisionAbysse();
			_cooldownDiagnosticCollisionAbysse = IntervalleDiagnosticCollisionAbysseSec;
		}
		if (_gateTpDimensionActif)
		{
			_secondesGateTpDimension += delta;
			_cooldownPulseReveilPierresTp = Math.Max(0.0, _cooldownPulseReveilPierresTp - delta);
			if (_cooldownPulseReveilPierresTp <= 0.0)
			{
				_mondeServeur?.ForcerPulseReveilPierres();
				_cooldownPulseReveilPierresTp = IntervallePulseReveilPierresTpSec;
			}
			if (CollisionLocalePretePourTpDimension() || _secondesGateTpDimension >= DureeMaxGateTpDimensionSec)
			{
				_gateTpDimensionActif = false;
				_secondesGateTpDimension = 0.0;
			}
			else if (_overlayChargement != null)
			{
				_overlayChargement.Visible = true;
			}
		}
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse && UseArchitectureReseau && _joueur != null && _mondeClient != null && !_gateTpDimensionActif)
		{
			bool pretMarcheAbysse = _mondeClient.AbyssePretPourDeplacement(_joueur.GlobalPosition);
			if (!pretMarcheAbysse)
			{
				_verrouMarcheAbysseActif = true;
				_secondesStabiliteMarcheAbysse = 0.0;
				_secondesVerrouMarcheAbysse += delta;
				if (_secondesVerrouMarcheAbysse >= DureeMaxVerrouMarcheAbysseSec)
				{
					// Filet anti-soft-lock: on relâche même si la croix n'est pas encore prête.
					_verrouMarcheAbysseActif = false;
					_secondesVerrouMarcheAbysse = 0.0;
					_secondesStabiliteMarcheAbysse = 0.0;
				}
			}
			else if (_verrouMarcheAbysseActif)
			{
				_secondesVerrouMarcheAbysse += delta;
				_secondesStabiliteMarcheAbysse += delta;
				if (_secondesStabiliteMarcheAbysse >= DureeStabiliteSortieVerrouMarcheAbysseSec
					|| _secondesVerrouMarcheAbysse >= DureeMaxVerrouMarcheAbysseSec)
				{
					_verrouMarcheAbysseActif = false;
					_secondesVerrouMarcheAbysse = 0.0;
					_secondesStabiliteMarcheAbysse = 0.0;
				}
			}
			else
			{
				_secondesVerrouMarcheAbysse = 0.0;
				_secondesStabiliteMarcheAbysse = 0.0;
			}
		}
		else
		{
			_verrouMarcheAbysseActif = false;
			_secondesVerrouMarcheAbysse = 0.0;
			_secondesStabiliteMarcheAbysse = 0.0;
		}
		_chargementAbysseEnCours = false;
		_secondesStabiliteAbyssePret = 0.0;
		_secondesVerrouAbysse = 0.0;
		_cooldownRearmementVerrouAbysse = 0.0;
		// Le cycle solaire ne doit être neutralisé que pendant le bootstrap strict du spawn.
		// IMPORTANT: ne pas lier le ciel aux cardinaux, sinon le cycle peut rester figé alors que le joueur est déjà jouable.
		bool chargementVisuelActif = _overlayChargement != null
			&& _overlayChargement.Visible
			&& (!spawnPretEtAligneActuel || _gateTpDimensionActif);
		MettreAJourEtatCycleSolaire(chargementVisuelActif);

		// Masquer l'overlay quand le sol minimal sous les pieds est prêt, ou après timeout (évite chargement infini si file / grille trop large).
		if (_overlayChargement != null && _overlayChargement.Visible)
		{
			if (_labelChargementPrincipal != null && _labelChargementPrincipal.Text != "Chargement du monde...")
				_labelChargementPrincipal.Text = "Chargement du monde...";
			if (_gateTpDimensionActif)
			{
				_secondesOverlayChargement += delta;
				goto FinBlocOverlay;
			}
			_secondesOverlayChargement += delta;
			bool spawnPret = spawnPretActuel;
			// Nouveau monde: on attend que la zone soit réellement prête avant raycast de pose au sol.
			if (spawnPret && cardinauxPrets && _spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
				FinaliserSpawnInitialAuSol();
			bool spawnPretEtAligne = spawnPret && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
			// Fallback UX: si le critère strict reste bloqué mais que le chunk local est bien actif,
			// on masque l'overlay pour ne pas laisser un "chargement infini" à l'écran.
			if (!spawnPretEtAligne && _joueur != null && _secondesOverlayChargement >= 6.0 && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol))
			{
				Vector2I chunkJoueur = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
				bool chunkLocalPret = !UseArchitectureReseau || (_mondeClient?.ChunkCollisionActive(chunkJoueur) ?? false);
				if (chunkLocalPret)
				{
					if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
						FinaliserSpawnInitialAuSol();
					spawnPretEtAligne = true;
				}
			}
			if (spawnPretEtAligne || _secondesOverlayChargement >= 90.0)
			{
				bool bootstrapClientStable = !UseArchitectureReseau
					|| _mondeClient == null
					|| _mondeClient.BootstrapInitialStabilise()
					|| !ExigerBootstrapClientStableAvantMasquerOverlay
					|| _secondesOverlayChargement >= Math.Max(0.0f, DureeMaxAttenteBootstrapClientSec);
				if (!bootstrapClientStable)
				{
					// On garde l’overlay un peu plus longtemps pour préchauffer collision/files et lisser les premières secondes de déplacement.
					goto FinBlocOverlay;
				}
				if (!spawnPretEtAligne && _secondesOverlayChargement >= 90.0)
					GD.PrintErr("ZERO-K : Timeout chargement monde (>90 s) — overlay masqué. Vérifiez réseau / Monde_Client si le sol manque.");
				if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
					FinaliserSpawnInitialAuSol(autoriserFallbackSansRaycast: _secondesOverlayChargement >= 90.0);
				_overlayChargement.Visible = false;
				if (_ajusterPiedsJoueurSurSurfaceApresRestauration)
				{
					_ajusterPiedsJoueurSurSurfaceApresRestauration = false;
					Callable.From(AjusterJoueurPositionRestaureeSurSurfaceProche).CallDeferred();
				}
			}
		}
FinBlocOverlay:

		bool spawnPretEtAlignePourRestauration = EstSpawnPret() && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
		bool restaurationSolVientDeTourner = EssayerRestaurerObjetsPersistantsPhaseSol(spawnPretEtAlignePourRestauration);
		// Réécrit inventaire + placed_objects + chunks après reload. Décalé de quelques frames : une sauvegarde
		// immédiate dans la même frame que la restauration sol a rarement provoqué un plantage moteur (PagedArray hors limites).
		if (restaurationSolVientDeTourner && !_synchronisationDisquePostRestaurationSolEffectuee)
		{
			_synchronisationDisquePostRestaurationSolEffectuee = true;
			CallDeferred(nameof(LancerSynchronisationDisquePostRestaurationSolDifferee));
		}
		int budgetDepgelSol = Mathf.Clamp(64 + _rigidBodiesAttenteCollisionSolRestauration.Count / 2, 64, 256);
		TraiterDepgelRigidBodiesRestaurationSol(budgetDepgelSol);

		// Mise à jour des coordonnées affichées en haut à droite
		if (_labelCoords != null && _joueur != null && _joueur.IsInsideTree())
		{
			Vector3 p = _joueur.GlobalPosition;
			Vector3 pArrondi = new Vector3(
				Mathf.Round(p.X * 10f) * 0.1f,
				Mathf.Round(p.Y * 10f) * 0.1f,
				Mathf.Round(p.Z * 10f) * 0.1f);
			if (pArrondi != _dernieresCoordsAffichees)
			{
				_dernieresCoordsAffichees = pArrondi;
				_labelCoords.Text = $"X: {pArrondi.X:F1}  Y: {pArrondi.Y:F1}  Z: {pArrondi.Z:F1}";
			}
		}
		if (_labelHeureDimension != null)
		{
			var infoDimension = ConstantesDimensions.ObtenirInfoOuAlpha(_dimensionLocaleActive);
			string heureAffichee;
			if (infoDimension.HeureFiguree)
			{
				// APISARA: même valeur que celle forcée via Cycle_Solaire.ConfigurerHeureFixeDimension(..., 13.5).
				heureAffichee = "13:30:00";
			}
			else
			{
				double offset = ObtenirServeurDimension(_dimensionLocaleActive)?.FuseauHoraireHeures
					?? (FuseauHoraireHeures + infoDimension.FuseauOffsetHeures);
				heureAffichee = DateTime.UtcNow.AddHours(offset).ToString("HH:mm:ss");
			}
			string texteHeure = $"{infoDimension.NomCanonique}  {heureAffichee}";
			if (!string.Equals(texteHeure, _dernierTexteHeureDimension, StringComparison.Ordinal))
			{
				_dernierTexteHeureDimension = texteHeure;
				_labelHeureDimension.Text = texteHeure;
			}
		}

		if (UseArchitectureReseau)
		{
			_secondesDormanceObjets += delta;
			if (_secondesDormanceObjets >= 0.4)
			{
				_secondesDormanceObjets = 0;
				ulong debutDormanceUs = ActiverProfilagePerfGestionnaire ? PerfBudgetMonitor.Begin() : 0UL;
				MettreAJourDormanceObjetsPoses((float)delta);
				if (ActiverProfilagePerfGestionnaire)
					PerfBudgetMonitor.End("GestionnaireMonde/DormanceObjets", debutDormanceUs);
			}
			// Monde_Client gère son propre _Process
			if (ActiverProfilagePerfGestionnaire)
			{
				PerfBudgetMonitor.End("GestionnaireMonde/Process", debutProcessUs);
				if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageSec))
				{
					_cooldownDrainProfilage = 0f;
					PerfBudgetMonitor.FlushSiEchu("GestionnaireMonde", IntervalleLogProfilageSec);
				}
			}
			return;
		}

		// Legacy : goutte-à-goutte visuel (1 mesh/frame max, évite Upload Stall VRAM)
		const int MaxMeshesParFrame = 2;
		int actionsExecutees = 0;
		while (actionsExecutees < MaxMeshesParFrame && _misesAJourUrgentes.TryDequeue(out var a))
		{
			a.Invoke();
			actionsExecutees++;
		}
		while (actionsExecutees < MaxMeshesParFrame && _misesAJourMainThread.TryDequeue(out var a))
		{
			a.Invoke();
			actionsExecutees++;
		}

		Vector2I cj = ObtenirCoordonneesChunkJoueur();
		bool chunkChange = cj != _ancienChunkJoueur;
		if (chunkChange) _ancienChunkJoueur = cj;

		// Radar strict : uniquement quand le joueur change de chunk (zéro alloc quand immobile)
		if (chunkChange)
			ActualiserVisibiliteEtTriChunksLegacy();

		int n = 0;
		while (_chunksACharger.Count > 0 && n < MaxChunksParFrame)
		{
			Vector2I c = _chunksACharger[0];
			_chunksACharger.RemoveAt(0);
			LancerGenerationChunk(c.X, c.Y);
			n++;
		}

		// Eau runtime purement événementielle (legacy) : uniquement file des voxels réveillés.
		if (_fileEau.Count > 0)
		{
			_tickEauLegacy++;
			int eauCount = Math.Min(_fileEau.Count, MaxEauParTick);
			for (int i = 0; i < eauCount; i++)
			{
				Vector3I pos = _fileEau.Dequeue();
				_eauActive.Remove(pos);
				if (!EstVoxelEauLegacy(pos)) continue;
				Vector3I posBas = pos + new Vector3I(0, -1, 0);
				if (posBas.Y < 0) { DefinirVoxelLegacy(pos, 0); DemanderMiseAJourMeshLegacy(pos); continue; }
				if (EstVoxelAirLegacy(posBas))
				{
					DefinirVoxelLegacy(posBas, 4);
					DefinirVoxelLegacy(pos, 0);
					MemoriserFluxEauLegacy(pos, posBas);
					ActiverEauLegacy(posBas);
					DemanderMiseAJourMeshLegacy(pos);
					DemanderMiseAJourMeshLegacy(posBas);
					ReveillerEauAdjacenteLegacy(new Vector3(pos.X, pos.Y, pos.Z));
					continue;
				}
				bool aPression = EstVoxelEauLegacy(pos + new Vector3I(0, 1, 0));
				foreach (var d in DirEauHorizLegacy)
				{
					Vector3I pc = pos + d, pcb = pc + new Vector3I(0, -1, 0);
					if (!EstVoxelAirLegacy(pc)) continue;
					if (!PeutCoulerVersLegacy(pos, pc)) continue;
					if (aPression || EstVoxelAirLegacy(pcb))
					{
						DefinirVoxelLegacy(pc, 4);
						DefinirVoxelLegacy(pos, 0);
						MemoriserFluxEauLegacy(pos, pc);
						ActiverEauLegacy(pc);
						DemanderMiseAJourMeshLegacy(pos);
						DemanderMiseAJourMeshLegacy(pc);
						ReveillerEauAdjacenteLegacy(new Vector3(pos.X, pos.Y, pos.Z));
						break;
					}
				}
			}
		}
		if (ActiverProfilagePerfGestionnaire)
		{
			PerfBudgetMonitor.End("GestionnaireMonde/Process", debutProcessUs);
			if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageSec))
			{
				_cooldownDrainProfilage = 0f;
				PerfBudgetMonitor.FlushSiEchu("GestionnaireMonde", IntervalleLogProfilageSec);
			}
		}
	}

	/// <summary>
	/// Filet de sécurité anti-crash : sauvegarde régulière du joueur et d'un lot de chunks actifs.
	/// La sauvegarde complète reste assurée par le bouton manuel, _Notification et _ExitTree.
	/// </summary>
	private void ExecuterAutosauvegardeProgressive()
	{
		ulong debutAutosaveUs = ActiverProfilagePerfGestionnaire ? PerfBudgetMonitor.Begin() : 0UL;
		if (_joueur != null)
		{
			GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
			SauvegarderSessionJoueur(_dimensionLocaleActive, _joueur.GlobalPosition);
		}
		if (_joueur is Joueur j)
		{
			if (_restaurationPersistantPhaseJoueurFaite)
			{
				if (_restaurationPersistantObjetsSolFaite)
					j.SauvegarderEtatPersistantMonde(GetTree());
				else
					j.SauvegarderEtatPersistantJoueurSeulement();
			}
		}

		if (UseArchitectureReseau)
		{
			int budget = Mathf.Max(1, MaxChunksAutosauvegardeParCycle);
			int n = 0;
			int backlogDirty = 0;
			int backlogDecharge = 0;
			foreach (var kv in _serveurParDimension)
			{
				n += kv.Value?.SauvegarderChunksActifsProgressif(budget) ?? 0;
				var b = kv.Value?.ObtenirBacklogsPersistance() ?? (0, 0);
				backlogDirty += b.Item1;
				backlogDecharge += b.Item2;
			}
			if (n > 0 || (_cooldownLogAutosaveDiag <= 0f && (backlogDirty > 0 || backlogDecharge > 0)))
			{
				GD.Print($"ZERO-K : Autosauvegarde progressive ({n} chunk(s)).");
				GD.Print($"ZERO-K PERF: backlog persistance dirty={backlogDirty} decharge={backlogDecharge} budget={budget}.");
				_cooldownLogAutosaveDiag = 15f;
			}
		}
		if (ActiverProfilagePerfGestionnaire)
			PerfBudgetMonitor.End("GestionnaireMonde/Autosave", debutAutosaveUs);
	}

	private void MettreAJourDormanceObjetsPoses(float dt)
	{
		if (_joueur == null) return;
		RafraichirCacheDormanceGroupes(dt);
		Vector2I chunkJoueur = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
		int rayon = RayonDormanceObjetsChunks;
		bool useGardeTerrain = UseArchitectureReseau && _mondeClient != null;
		int rayonSecuriteTerrain = Mathf.Clamp(RayonSecuriteTerrainObjetsChunks, 0, 2);

		int budgetTotal = Mathf.Max(16, BudgetDormanceObjetsParCycle);
		int budgetBlocs = Mathf.Max(1, Mathf.RoundToInt(budgetTotal * 0.65f));
		int budgetDyn = Mathf.Max(1, budgetTotal - budgetBlocs);
		int budgetFiletSecurite = ActiverFiletSecuriteObjetsDynamiques
			? Mathf.Clamp(BudgetFiletSecuriteObjetsParCycle, 1, budgetTotal)
			: 0;
		TraiterDormanceGroupe("BlocsPoses", ref _indexDormanceBlocsPoses, budgetBlocs, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ignorerRacks: true, ref budgetFiletSecurite);
		TraiterDormanceGroupe("ObjetsDormantsDynamiques", ref _indexDormanceObjetsDyn, budgetDyn, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ignorerRacks: false, ref budgetFiletSecurite);
	}

	private void TraiterDormanceGroupe(string nomGroupe, ref int indexCurseur, int budget, Vector2I chunkJoueur, int rayon, bool useGardeTerrain, int rayonSecuriteTerrain, bool ignorerRacks, ref int budgetFiletSecurite)
	{
		if (!_cacheRigidBodiesDormance.TryGetValue(nomGroupe, out List<RigidBody3D> noeuds))
		{
			RafraichirCacheDormanceGroupes(0f, force: true);
			if (!_cacheRigidBodiesDormance.TryGetValue(nomGroupe, out noeuds))
				return;
		}
		int total = noeuds.Count;
		if (total == 0) { indexCurseur = 0; return; }
		if (indexCurseur >= total) indexCurseur = 0;
		int iterations = Math.Min(Mathf.Max(1, budget), total);
		for (int i = 0; i < iterations; i++)
		{
			total = noeuds.Count;
			if (total <= 0) { indexCurseur = 0; return; }
			if (indexCurseur >= total) indexCurseur = 0;
			if ((uint)indexCurseur >= (uint)noeuds.Count) break;
			RigidBody3D rb = noeuds[indexCurseur++];
			if (rb == null || !GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree())
			{
				int idxSuppr = Mathf.Clamp(indexCurseur - 1, 0, noeuds.Count - 1);
				if (idxSuppr >= 0 && idxSuppr < noeuds.Count) noeuds.RemoveAt(idxSuppr);
				total = noeuds.Count;
				indexCurseur = Mathf.Clamp(indexCurseur - 1, 0, Math.Max(0, total - 1));
				if (total == 0) { indexCurseur = 0; return; }
				continue;
			}
			if (ignorerRacks && rb is ItemPhysique ip && ItemPhysique.EstMeublePoseStatique(ip.ID_Objet))
				continue;
			AppliquerDormanceRigidBody(rb, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ref budgetFiletSecurite);
		}
	}

	private void AppliquerDormanceRigidBody(RigidBody3D rb, Vector2I chunkJoueur, int rayon, bool useGardeTerrain, int rayonSecuriteTerrain, ref int budgetFiletSecurite)
	{
		Vector2I c = WorldToChunkCoord(rb.GlobalPosition, TailleChunk);
		bool dansRayon = Mathf.Abs(c.X - chunkJoueur.X) <= rayon && Mathf.Abs(c.Y - chunkJoueur.Y) <= rayon;
		bool terrainPret = !useGardeTerrain || _mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, rayonSecuriteTerrain);
		bool itemLegerPetit = ItemPhysique.EstRigidBodyLegerEtPetitReactif(rb);
		bool structureStatique = rb is ItemPhysique ipStatique && ItemPhysique.EstMeublePoseStatique(ipStatique.ID_Objet);

		if (!structureStatique && budgetFiletSecurite > 0
			&& !_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
		{
			budgetFiletSecurite--;
			EssayerRecalerRigidBodySousSol(rb, terrainPret);
		}

		if (itemLegerPetit && _joueur != null && GodotObject.IsInstanceValid(_joueur))
		{
			float dist2 = rb.GlobalPosition.DistanceSquaredTo(_joueur.GlobalPosition);
			if (dist2 <= 6f * 6f)
			{
				if (!terrainPret)
				{
					EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
					return;
				}
				if (_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
					return;
				// Dégeler seulement la dormance — ne pas réveiller un objet déjà au repos sur le sol.
				if (rb.Freeze) rb.Freeze = false;
				return;
			}
		}

		// Priorité gameplay: un objet proche du joueur ne doit jamais rester figé en l'air.
		if (dansRayon)
		{
			if (!terrainPret)
			{
				EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
				return;
			}
			if (_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
				return;
			if (rb.Freeze) rb.Freeze = false;
			return;
		}

		if (itemLegerPetit && terrainPret)
		{
			if (rb.Freeze) rb.Freeze = false;
			return;
		}
		// Lointain : figer seulement si encore actif (évite de casser le repos naturel Sleeping au sol).
		if (!terrainPret || rb.Freeze || !rb.Sleeping)
		{
			if (!terrainPret || rb.LinearVelocity.LengthSquared() > 0.08f || rb.AngularVelocity.LengthSquared() > 0.08f || rb.Freeze)
				FigerRigidBodyDormance(rb);
		}
	}

	private static void FigerRigidBodyDormance(RigidBody3D rb)
	{
		rb.LinearVelocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		rb.Sleeping = true;
		rb.Freeze = true;
	}

	private void EssayerRecalerRigidBodySousSol(RigidBody3D rb, bool terrainPret)
	{
		if (!terrainPret || !GodotObject.IsInstanceValid(rb))
			return;

		// Objet au repos : le filet ne doit pas le téléporter (cause principale du « saut » après pose).
		if (rb.Sleeping
			&& rb.LinearVelocity.LengthSquared() < 0.06f
			&& rb.AngularVelocity.LengthSquared() < 0.06f)
			return;

		Vector3 pos = rb.GlobalPosition;
		PhysicsDirectSpaceState3D espace = rb.GetWorld3D()?.DirectSpaceState;
		if (espace != null)
		{
			var requete = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 0.35f, pos + Vector3.Down * 8f);
			requete.CollisionMask = 1;
			requete.CollideWithAreas = false;
			requete.Exclude = new Godot.Collections.Array<Rid> { rb.GetRid() };
			var impact = espace.IntersectRay(requete);
			if (impact.Count > 0 && impact.ContainsKey("position"))
			{
				float ySol = ((Vector3)impact["position"]).Y;
				float ecart = pos.Y - ySol;
				// Déjà posé sur le mesh collision réel : ne pas remonter vers la hauteur procédurale.
				if (ecart >= -0.3f)
					return;
				// Enfoui sous le mesh seulement : petit recal au contact, sans filet de dégel.
				if (ecart >= -1.5f)
				{
					float yCorrige = ySol + Mathf.Max(0.02f, MargeRemonteeObjetsMetres);
					if (Mathf.Abs(yCorrige - pos.Y) < 0.02f)
						return;
					rb.GlobalPosition = new Vector3(pos.X, yCorrige, pos.Z);
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					return;
				}
			}
		}

		// Chute dans le vide (pas de sol raycast) : dernier recours procédural, puis attente collision.
		int x = Mathf.FloorToInt(pos.X);
		int z = Mathf.FloorToInt(pos.Z);
		int h = _dimensionLocaleActive == (int)DimensionJeu.Abysse
			? ApisaraHauteurTerrain.ObtenirHauteurSolMonde(x, z, SeedTerrain)
			: Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, SeedTerrain);
		float ySurface = h + 1.0f;
		if (pos.Y >= ySurface - 0.6f)
			return;

		float yCorrigeProc = ySurface + Mathf.Max(0.02f, MargeRemonteeObjetsMetres);
		rb.GlobalPosition = new Vector3(pos.X, yCorrigeProc, pos.Z);
		rb.LinearVelocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
	}

	private Vector2I ObtenirCoordonneesChunkJoueur()
	{
		if (_joueur == null) return Vector2I.Zero;
		return WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
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

	/// <summary>Récolte ciblée d’un buisson : 0=hachette (branche), 1=dague (coupe), 2=pelle (déracinage replantable).</summary>
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
