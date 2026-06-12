using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Détient les Chunk_Client (MeshInstance3D, collision). Reçoit des données et les transforme en triangles. Pas de génération de bruit.</summary>
public partial class Monde_Client : Node3D
{
	[Export] public int TailleChunk = 16;
	[Export] public int HauteurMax = 720;  // Montagnes jusqu'à 700
	/// <summary>Profondeur étendue : tranches verticales de 100 m (voir <see cref="ConstantesProfondeurVerticale"/>).</summary>
	[Export] public bool ActiverProfondeurEtendue = true;
	[Export] public int ProfondeurMaxMetres = 1000;
	/// <summary>Plafond horizontal (demi-côté chunks) en mode tranches 100 m — évite RenderDistance×5 tranches = milliers de meshes.</summary>
	[Export(PropertyHint.Range, "2,24,1")] public int PlafondRayonChargementProfondeurChunks = 10;
	[Export] public int RenderDistance = 200;
	[Export] public int RenderDistanceDetailChunks = 15;
	[Export] public int RayonQualiteMaxChunks = 4;
	[Export] public int RayonGazonVisibleChunks = 6;
	[Export] public int RayonBuissonsVisibleChunks = 12;
	[Export] public bool ProfilLodCinematiqueUltraSmooth = true;
	[Export] public int LODTextureEtapes = 12;
	[Export] public int MaxChunksParFrame = 9;
	/// <summary>Nombre d'entrées inspectées max pour choisir un job maths (évite un scan O(n) complet à chaque worker).</summary>
	[Export] public int FenetreSelectionTravailMaths = 56;
	[Export] public int RayonInitialRequetesChunks = 10;
	[Export] public float IntervalleExpansionRequetesSec = 0.30f;
	[Export] public int SeuilBacklogHaut = 80;
	[Export] public int SeuilBacklogBas = 24;
	[Export] public float IntervalleProgressionForceeRayonSec = 1.6f;
	[Export] public bool ModeAutoDiagnosticAdaptatif = true;
	[Export] public int FpsCibleAutoDiagnostic = 60;
	[Export] public float RatioChargeMinimumAuto = 0.10f;
	[Export] public bool ActiverAntiSpikeFrameTime = true;
	[Export] public float SeuilSpikeFrameMs = 18f;
	[Export] public float DureeFreinSpikeSec = 0.45f;
	[Export] public bool ActiverHorizonLod = true;
	[Export] public int RayonHorizonChunks = 72;
	[Export] public float PasHorizonMetres = 20f;
	[Export] public float FrequenceMajHorizonSec = 1.2f;
	[Export] public bool ActiverCullingCameraChunks = true;
	[Export] public float AngleCullingCameraDeg = 135f;
	[Export] public int MargeChunksToujoursVisibles = 12;
	/// <summary>Plafond du demi-côté du disque « toujours visible » côté culling (évite de tout dessiner à R=200 tout en suivant <see cref="RenderDistance"/>).</summary>
	[Export(PropertyHint.Range, "8,96,1")] public int PlafondDisqueToujoursVisibleChunks = 28;
	/// <summary>Écart minimal (chunks) entre <see cref="RayonChargementChunksActif"/> et <c>_rayonRequetesActuel</c> pour ne pas plafonner à 1 requête/frame en mode survie FPS.</summary>
	[Export(PropertyHint.Range, "4,24,1")] public int SeuilGapRequetesMin = 8;
	[Export] public int MaxBasculesCullingParPasse = 96;
	[Export] public int MaxChunksEvaluesCullingParPasse = 240;
	[Export] public float IntervalleDormanceSec = 0.06f;
	/// <summary>Rayon (en chunks) autour du joueur où les collisions sont actives. Tout dans ce rayon doit être dynamique (réveil immédiat). Au-delà, physique en dormance. 5 chunks ≈ 80 m (évite trous de collision en bordure, allège énormément Jolt : 121 chunks × 45 sections = 5445 shapes au lieu de 13005 à R=8).</summary>
	[Export] public int RayonDormancePhysique = 5;
	/// <summary>Demi-côté (chunks) pour lever l’overlay « Chargement du monde » : 1 = grille 3×3. Ne pas exiger tout le rayon de dormance (17×17) au démarrage sinon chargement quasi infini.</summary>
	[Export] public int RayonGrilleMinSpawnPret = 1;
	/// <summary>Chunks demandés en plus du rayon physique (file prioritaire). Le sol doit être chargé avant que tu n’entres dans la grille ChunkSousPiedsAPret.</summary>
	[Export] public int MargePreloadChunks = 6;
	/// <summary>Anticipation du déplacement (s) : une 2ᵉ zone de priorité autour de la position future pour marches longues dans une direction.</summary>
	[Export] public float SecondesAnticipationChargement = 3.0f;
	[Export] public float IntervalleRafraichissementRadarImmobile = 0.45f;
	/// <summary>Intégrations mesh/collision par frame quand le spawn est déjà prêt (exploration). Augmente si le sol met du temps à « se réveiller ».</summary>
	[Export] public int MaxIntegrationsParFrameExploration = 1;
	/// <summary>Intégrations mesh/collision par frame pendant le chargement initial (anti-pic CPU/GPU).</summary>
	[Export] public int MaxIntegrationsParFrameChargement = 3;
	/// <summary>Budget de vertices intégrés par frame (exploration). Lisse l'arrivée des triangles.</summary>
	[Export] public int BudgetVerticesIntegrationParFrameExploration = 16000;
	/// <summary>Budget de vertices intégrés par frame au chargement initial (plus généreux).</summary>
	[Export] public int BudgetVerticesIntegrationParFrameChargement = 70000;
	/// <summary>Solidifications BodySetSpace par frame en exploration (hors chargement initial). Bas = moins de CreateTrimeshShape / frame (Jolt).</summary>
	[Export] public int MaxSolidificationsParFrameExploration = 2;
	/// <summary>Budget minimal de solidifications quand le joueur se déplace vite (anti-traversée du sol).</summary>
	[Export] public int MaxSolidificationsPrioriteJoueur = 6;
	/// <summary>Nombre max d'entrées inspectées pour choisir un chunk à solidifier (évite un scan complet de la file à chaque tick).</summary>
	[Export] public int FenetreSelectionSolidification = 64;
	/// <summary>Rayon (chunks) à réveiller en urgence autour de la position courante / anticipée du joueur.</summary>
	[Export] public int RayonPrioriteCollisionJoueur = 2;
	/// <summary>Anticipation (secondes) pour pré-réveiller les collisions devant le joueur.</summary>
	[Export] public float SecondesAnticipationCollision = 0.85f;
	/// <summary>Nombre max de chunks de flore (gazon/buissons) construits par frame en exploration.</summary>
	[Export] public int MaxFloreParFrameExploration = 3;
	/// <summary>Nombre max de chunks de flore construits par frame pendant le chargement initial.</summary>
	[Export] public int MaxFloreParFrameChargement = 4;
	[ExportGroup("Performance streaming")]
	/// <summary>Si désactivé, <c>_rayonRequetesActuel</c> suit immédiatement <see cref="RenderDistance"/> (convergence en une frame, voir <see cref="AjusterFenetreRequetes"/>).</summary>
	[Export] public bool ModeSurvieFpsAgressif = true;
	[Export(PropertyHint.Range, "20,59,1")] public int SeuilFpsUrgenceForte = 42;
	[Export(PropertyHint.Range, "15,45,1")] public int SeuilFpsUrgenceCritique = 30;
	[Export(PropertyHint.Range, "10,35,1")] public int SeuilFpsUrgenceExtreme = 24;
	[Export(PropertyHint.Range, "40,90,1")] public int SeuilFpsSortieUrgenceExtreme = 56;
	[ExportGroup("Streaming si moteur calme")]
	/// <summary>Si activé en mode « Sauver les FPS », élargit légèrement le radar et l’expansion du rayon de requêtes quand les FPS moyens et le backlog restent stables (sans dépasser RenderDistance).</summary>
	[Export] public bool ActiverElargissementRadarSiFpsStable = true;
	[Export(PropertyHint.Range, "45,90,1")] public int SeuilFpsMoyenPourElargirRadar = 56;
	[Export(PropertyHint.Range, "0.5,8,0.25")] public float SecondesFpsStablesPourElargirRadar = 2.25f;
	[Export(PropertyHint.Range, "1,10,1")] public int MargeRadarSupplementaireChunksSiCalme = 3;
	[Export(PropertyHint.Range, "0,4,1")] public int PasExpansionRequetesSupplementaireSiCalme = 1;
	[ExportGroup("Fenêtre requêtes vs RenderDistance")]
	/// <summary>Diviseur du « gap » (cible − fenêtre) pour accélérer les pas d’expansion quand FPS/backlog sont sains (mode survie on).</summary>
	[Export(PropertyHint.Range, "2,16,1")] public int DiviseurGapPourPasExpansion = 3;
	/// <summary>Plafond de chunks ajoutés par tick d’expansion quand le gap est grand (évite un pic si RenderDistance est énorme).</summary>
	[Export(PropertyHint.Range, "2,16,1")] public int PasExpansionMaxSiGapLarge = 5;
	/// <summary>Part du gap refermée d’un coup après une hausse de <see cref="RenderDistance"/> (mode survie on), ex. 0,45 ≈ 45 %.</summary>
	[Export(PropertyHint.Range, "0.15,0.9,0.05")] public float FractionImpulsionHausseRenderDistance = 0.45f;
	/// <summary>Plafond relatif à <see cref="RenderDistance"/> sous urgence FPS extrême (niveau 3).</summary>
	[Export(PropertyHint.Range, "0.22,0.55,0.01")] public float FractionRayonMaxUrgenceExtreme = 0.38f;
	/// <summary>Plafond relatif sous urgence critique (niveau 2).</summary>
	[Export(PropertyHint.Range, "0.35,0.70,0.01")] public float FractionRayonMaxUrgenceCritique = 0.52f;
	/// <summary>Plafond relatif sous urgence forte (niveau 1).</summary>
	[Export(PropertyHint.Range, "0.45,0.85,0.01")] public float FractionRayonMaxUrgenceForte = 0.68f;

	// =========================================================================
	// GATE FPS STRICT : gèle tout nouveau chargement tant que FPS < seuil.
	// Pendant le gel, SEULE la zone ultra-proche joueur continue (anti-chute).
	// Ramp-up 1-par-1 à la reprise (un élément/frame au début, puis augmente).
	// =========================================================================
	[Export] public bool ActiverGateFpsStrict = true;
	/// <summary>FPS en dessous duquel on gèle le streaming non-critique. Éviter 50+ si beaucoup de configs restent en 45–55 FPS (gel = 0 requête chunk → vide noir).</summary>
	[Export(PropertyHint.Range, "30,70,1")] public float SeuilFpsGateStrict = 42f;
	/// <summary>FPS au-dessus duquel on sort du gel (hystérésis anti-pompage).</summary>
	[Export(PropertyHint.Range, "40,90,1")] public float SeuilFpsGateReprise = 57f;
	/// <summary>Durée de stabilité au-dessus du seuil de reprise avant de dégeler.</summary>
	[Export(PropertyHint.Range, "0.1,2.0,0.05")] public float DureeStabiliteReprise = 0.20f;
	/// <summary>Durée du ramp-up après dégel (1 élément → budget normal). Évite un pic juste après reprise.</summary>
	[Export(PropertyHint.Range, "0.2,3.0,0.1")] public float DureeRampUpPostDegel = 0.55f;
	/// <summary>Temps minimal en état "gelé" avant d'autoriser un dégel (anti-clignotement).</summary>
	[Export(PropertyHint.Range, "0.05,2.0,0.05")] public float DureeMinEtatGeleSec = 0.15f;
	/// <summary>Temps minimal en état "ouvert" avant de pouvoir re-geler (anti-oscillation).</summary>
	[Export(PropertyHint.Range, "0.05,2.0,0.05")] public float DureeMinEtatOuvertSec = 0.45f;
	/// <summary>Après un nouveau monde : pas de gel streaming (chunks doivent se générer même en mode « Sauver les FPS »).</summary>
	[Export(PropertyHint.Range, "5,120,1")] public float DureeGraceStreamingBootstrapNouveauMondeSec = 50f;

	private bool _gateStreamingGele = false;
	private Vector2I _dernierChunkReservePrioritaire = new Vector2I(int.MinValue, int.MinValue);
	private float _cooldownReservePrioritaireSec;
	private float _timerGraceStreamingBootstrap;
	/// <summary>Frame courante : le monde a encore besoin de chunks (priorité sur le gel FPS).</summary>
	private bool _streamingChunksPrioritaireCetteFrame;
	private float _tempsFpsStableHaut = 0f;
	private float _tempsDepuisDegel = 99f;
	private float _tempsEtatGate = 99f;
	[Export(PropertyHint.Range, "8,25,0.1")] public float BudgetFrameCibleMs = 16.2f;
	[Export(PropertyHint.Range, "0.1,4,0.1")] public float MargeBudgetUrgenceMs = 1.0f;
	[Export(PropertyHint.Range, "0.2,8,0.1")] public float BudgetMsIntegrationsMainThread = 2.0f;
	[Export(PropertyHint.Range, "0.2,8,0.1")] public float BudgetMsSolidificationMainThread = 1.8f;
	[Export(PropertyHint.Range, "0.1,4,0.1")] public float BudgetMsLancementWorkersMainThread = 0.8f;
	[Export(PropertyHint.Range, "0.05,0.8,0.01")] public float IntervalleServicesLointainsUrgenceSec = 0.22f;
	[Export(PropertyHint.Range, "8,512,1")] public int FenetreSelectionRequetes = 96;
	[Export(PropertyHint.Range, "1,8,1")] public int MaxLancementsTravailleursParTick = 1;
	/// <summary>Anti micro-freeze : hors zone critique joueur, exécute les charges lourdes en série (intégrations, puis solidifications, puis workers) au lieu de les cumuler sur une même frame.</summary>
	[Export] public bool ForcerOrdonnancementSerieAntiFreeze = true;
	[Export(PropertyHint.Range, "0.01,0.5,0.01")] public float IntervalleMinRebuildRadarSec = 0.10f;
	[Export(PropertyHint.Range, "0,128,1")] public int SeuilBacklogBootstrapStable = 6;
	[Export] public bool ExigerSolidificationVidePourBootstrap = false;
	[ExportGroup("Diagnostic performance")]
	[Export] public bool ActiverProfilagePerfMondeClient = true;
	[Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleLogProfilageSec = 2.0f;
	[Export] public bool ActiverDiagnosticCollisionAbysse = false;

	private ConcurrentQueue<Action> _misesAJourMainThread = new ConcurrentQueue<Action>();
	public ConcurrentQueue<Action> _misesAJourUrgentes = new ConcurrentQueue<Action>();

	private readonly struct TacheIntegration
	{
		public readonly Action Action;
		public readonly int CoutVerticesEstime;
		public TacheIntegration(Action action, int coutVerticesEstime)
		{
			Action = action;
			CoutVerticesEstime = coutVerticesEstime;
		}
	}
	/// <summary>File d'attente d'intégration (main thread) avec coût estimé pour étaler les triangles sur plusieurs frames.</summary>
	private ConcurrentQueue<TacheIntegration> _fileIntegrationMainThread = new ConcurrentQueue<TacheIntegration>();

	/// <summary>Forge restreinte : file des chunks en attente de calcul (maths). Au plus MaxTravailleursCalcul Task.Run actifs.</summary>
	private readonly object _lockFileAttenteMaths = new object();
	private List<(ChunkData data, DonneesChunk donnees)> _fileAttenteMathsData = new List<(ChunkData, DonneesChunk)>();
	private int _chunksEnCoursDeCalcul = 0;
	[Export] public int MaxTravailleursCalcul = 4;

	/// <summary>Chunks au format Data-Oriented (RID). Plus de Node pour le terrain.</summary>
	private Dictionary<Vector2I, ChunkData> _chunksData = new Dictionary<Vector2I, ChunkData>();
	private readonly Dictionary<Vector3I, ChunkData> _chunksDataAbysse3D = new Dictionary<Vector3I, ChunkData>();
	private readonly Dictionary<Vector3I, ChunkData> _chunksDataProfondeur3D = new Dictionary<Vector3I, ChunkData>();
	/// <summary>File d'attente de solidification physique : un chunk par frame pour éviter les pics PhysicsServer3D (dilution physique).</summary>
	private List<ChunkData> _fileAttenteSolidification = new List<ChunkData>();
	/// <summary>Présence O(1) de la file standard pour éviter les Contains/Remove inutiles.</summary>
	private readonly HashSet<ChunkData> _setSolidificationNormale = new HashSet<ChunkData>();
	/// <summary>File urgente de collision autour du joueur (priorité absolue sécurité gameplay).</summary>
	private readonly List<ChunkData> _fileAttenteSolidificationUrgente = new List<ChunkData>();
	/// <summary>Miroir O(1) de la file urgente (évite Contains O(n) dans les anneaux autour du joueur).</summary>
	private readonly HashSet<ChunkData> _setSolidificationUrgente = new HashSet<ChunkData>();

	private List<Vector2I> _chunksACharger = new List<Vector2I>();

	/// <summary>Animation d'émergence : fondu d'apparition d'un chunk (anti pop-in brutal). Seul le visuel est animé, la physique est immédiate.</summary>
	private struct AnimEmergenceChunk
	{
		public Rid VisualRid;
		public Rid WaterRid;
		public Rid FloreNodeRid;
		public float TempsEcoule;
		public float Duree;
	}
	private readonly List<AnimEmergenceChunk> _animsEmergence = new List<AnimEmergenceChunk>();
	/// <summary>Durée (s) du fondu d'apparition du terrain. 0 = désactivé (mis à 0 par défaut : le shader TerrainVoxel n'expose pas de canal de transparence — tenter un fade masque simplement les textures).</summary>
	[Export(PropertyHint.Range, "0,1.5,0.05")] public float DureeFonduEmergenceChunk = 0.0f;

	private readonly HashSet<Vector2I> _prioritaireSetTemp = new HashSet<Vector2I>();
	private readonly List<Vector2I> _prioritaireListTemp = new List<Vector2I>();
	private readonly HashSet<Vector3I> _chunksUniquesTemp = new HashSet<Vector3I>();
	private readonly HashSet<Vector3I> _chunksTraitesRemeshTemp = new HashSet<Vector3I>();
	private readonly Dictionary<Vector3I, HashSet<int>> _sectionsParChunkRemeshTemp = new Dictionary<Vector3I, HashSet<int>>();
	private readonly HashSet<(int cx, int coordY, int cz, int section)> _sectionsRemeshTraiteesTemp = new HashSet<(int, int, int, int)>();
	private readonly List<Vector3I> _voisinsRemeshMinageTemp = new List<Vector3I>(8);
	private readonly List<Vector3I> _remeshOrdreChunksTemp = new List<Vector3I>(32);
	private readonly List<Vector2I> _chunksATuerTemp = new List<Vector2I>();
	private readonly List<Vector3I> _clesChunksAbysseARetirerTemp = new List<Vector3I>();
	private bool _radarEnCours;
	private HashSet<(int cx, int coordY, int cz, int section)> _sectionsAReconstruire = new HashSet<(int, int, int, int)>();
	private CharacterBody3D _joueur;
	/// <summary>CoordY joueur lue sur le thread principal (_PhysicsProcess) — safe pour workers MC.</summary>
	private volatile int _coordYJoueurProfondeurCache = int.MinValue;
	private volatile bool _joueurPresentPourWorkers;
	private Vector2I _ancienChunkJoueur = new Vector2I(-99999, -99999);
	private int _ancienCoordYJoueur = int.MinValue;
	private bool _modificationEnCours;
	/// <summary>Mutations voxel reçues avant que la tranche/chunk soit en RAM client (minage sans trou visuel).</summary>
	private readonly Dictionary<Vector3I, byte> _voxelsModifiesEnAttente = new Dictionary<Vector3I, byte>();
	private readonly List<Vector3I> _voxelsEnAttenteBuffer = new List<Vector3I>();
	/// <summary>Sections MC synchrones max par frame au minage (évite spike si 50 RPC d'un coup).</summary>
	private int _remeshMinageSyncRestantFrame = 0;
	private bool _mondeClientSortieEnCours;
	private MeshInstance3D _horizonLodMesh;
	private static StandardMaterial3D _cacheMatHorizon;
	private Vector2I _centreHorizonCell = new Vector2I(int.MinValue, int.MinValue);
	private float _timerMajHorizon = 0f;
	private float _timerCullingCamera = 0f;
	/// <summary>Nombre de passes culling « boost » après une rotation caméra (évite trous / herbe figée hors cône).</summary>
	private int _framesBoostCullingRotationRestantes;
	/// <summary>Clé (chunkX, coordChunkY, chunkZ) : en Abysse plusieurs couches partagent le même couple (X,Z).</summary>
	private readonly List<Vector3I> _fileFloreDifferee = new List<Vector3I>();
	private readonly HashSet<Vector3I> _setFloreDifferee = new HashSet<Vector3I>();
	private readonly Dictionary<Vector3I, ulong> _frameEnqueueFlore = new Dictionary<Vector3I, ulong>();
	private int _rayonRequetesActuel;
	private float _timerExpansionRequetes;
	private float _timerProgressionForceeRayon = 0f;
	private float _timerRafraichissementRadarImmobile = 0f;
	private Vector3 _derniereDirectionRadar = Vector3.Forward;
	private const int EpaisseurAnneauRadar = 3;
	[Export] public int MaxAjoutsRadarParPasse = 520;
	private float _fpsMoyenneAuto = 60f;
	private float _cooldownLogPerfFps;
	private float _ratioChargeAuto = 1f;
	private int _maxAjoutsRadarParPasseDyn = 520;
	private int _maxRequetesDyn = 12;
	private int _maxTravailleursDyn = 8;
	private int _maxTransitionsDormanceDyn = 64;
	private int _maxBasculesCullingDyn = 96;
	private float _intervalleCullingDyn = 0.03f;
	private float _intervalleRadarImmobileDyn = 0.55f;
	private float _facteurMouvementAuto = 1f;
	private float _timerFreinSpike = 0f;
	private int _niveauUrgencePerf = 0; // 0=normal, 1=forte, 2=critique
	/// <summary>Temps cumulé (s) où les conditions « moteur calme » sont réunies pour autoriser un radar un peu plus ambitieux.</summary>
	private float _accumulateurFpsStablePourRadar = 0f;

	/// <summary>Après « Appliquer » dans le panneau graphismes : évite que les plafonds d’urgence FPS annulent la distance de rendu choisie (ex. 30 chunks) tout en gardant une montée progressive du radar.</summary>
	private float _timerGraceStreamingReglageUtilisateur;
	private const float DureeGraceStreamingReglageUtilisateurSec = 75f;
	private float _cooldownServicesLointains = 0f;
	private Vector2I _obsChunkDormance = new Vector2I(-99999, -99999);
	private float _timerDormance = 0f;
	private readonly List<Vector2I> _cacheCoordsChunks = new List<Vector2I>();
	private int _cacheCoordsChunksCount = -1;
	private int _indexCullingScan = 0;
	private int _indexDormanceScan = 0;
	private int _curseurSelectionSolidification = 0;
	private int _curseurSelectionRequetes = 0;
	private int _phaseOrdonnancementSerie = 0; // 0=integrations, 1=solidifications, 2=workers
	private ulong _frameDernierRebuildCacheChunks = 0;
	private float _cooldownRebuildRadar = 0f;
	private bool _rebuildRadarEnAttente;
	private Vector3 _positionRadarEnAttente = Vector3.Zero;
	private float _cooldownDrainProfilage = 0f;
	private Camera3D _cameraObservationCache;
	private ulong _frameCameraObservationCache = ulong.MaxValue;

	private Action<Vector2I> _enregistrerDemandeChunk;
	private Action<Vector3, float, float> _demanderDestruction;
	private Action<Vector3, Vector3, float, int> _demanderCreation;
	private int _seedTerrain;
	private NetworkManager _networkManager;
	private int _dimensionReseauActive = (int)DimensionJeu.Alpha;
	private readonly HashSet<int> _coordYActifsAbysseTravail = new HashSet<int>();
	private readonly HashSet<int> _coordYCollisionAbysseTravail = new HashSet<int>();
	private readonly List<int> _coordYActifsAbysseListeTravail = new List<int>(8);
	private readonly HashSet<int> _coordYActifsProfondeurTravail = new HashSet<int>();
	private readonly List<int> _coordYActifsProfondeurListeTravail = new List<int>(8);
	private readonly Dictionary<Vector3I, ulong> _demandesAbysseFrameDerniereEmission = new Dictionary<Vector3I, ulong>();
	private readonly List<Vector3I> _clesDemandesAbysseExpireesTemp = new List<Vector3I>();
	private readonly Dictionary<Vector3I, ulong> _demandesProfondeurFrameDerniereEmission = new Dictionary<Vector3I, ulong>();
	/// <summary>Dernière émission réseau par couche (anti-spam temporel, complète le garde-fou par frame).</summary>
	private readonly Dictionary<Vector3I, double> _demandesChunkTempsDerniereEmission = new Dictionary<Vector3I, double>();
	private readonly List<Vector3I> _clesDemandesProfondeurExpireesTemp = new List<Vector3I>();
	private const double IntervalleRedemandeChunkUrgentSec = 0.18;
	private const double IntervalleRedemandeChunkProcheSec = 0.42;
	private const double IntervalleRedemandeChunkDormanceSec = 0.30;
	private const double IntervalleRedemandeChunkLointainSec = 1.15;
	private float _timerTrimAbysse = 0f;
	private const float IntervalleTrimAbysseSec = 0.50f;
	private float _cooldownDiagCoherenceAbysse = 0f;
	private const float IntervalleDiagCoherenceAbysseSec = 1.25f;
	private float _cooldownLogDiagnosticCollisionAbysse = 0f;
	private const float IntervalleDiagnosticCollisionAbysseSec = 0.80f;
	private const int MaxFileDemandesChunksAbysse = 1400;
	/// <summary>Plafond file maths (marching cubes) : au-delà, on retire les chunks les plus lointains (évite lag croissant en exploration).</summary>
	private const int MaxFileAttenteMathsChunks = 200;
	/// <summary>Plafond intégrations mesh en attente (FIFO si dépassement).</summary>
	private const int MaxFileIntegrationEnAttente = 320;
	private const int MaxChunksAChargerAlpha = 900;
	private float _timerEpurationBacklog = 0f;
	private const float IntervalleEpurationBacklogSec = 0.22f;

	private Camera3D ObtenirCameraObservation()
	{
		ulong frame = Engine.GetProcessFrames();
		if (_frameCameraObservationCache == frame)
		{
			if (_cameraObservationCache != null
				&& GodotObject.IsInstanceValid(_cameraObservationCache)
				&& _cameraObservationCache.IsInsideTree())
			return _cameraObservationCache;
			_cameraObservationCache = null;
			return null;
		}
		_frameCameraObservationCache = frame;
		Viewport viewport = GetViewport();
		Camera3D camera = viewport?.GetCamera3D();
		if (camera != null && GodotObject.IsInstanceValid(camera) && camera.IsInsideTree())
		{
			_cameraObservationCache = camera;
			return _cameraObservationCache;
		}
		_cameraObservationCache = null;
		return null;
	}

	private bool EssayerObtenirJoueurDansArbre(out CharacterBody3D joueur)
	{
		joueur = _joueur;
		return joueur != null && GodotObject.IsInstanceValid(joueur) && joueur.IsInsideTree();
	}

	private bool JoueurEnModeVolCreatif()
	{
		if (_joueur is Joueur joueur)
			return joueur.ModeCreatifActif || joueur.NoclipAdminActif;
		return false;
	}

	/// <summary>
	/// Vol créatif en surface (maillage sous les pieds) : budgets réseau réduits.
	/// Sous terre / grotte / vide : streaming normal pour explorer (spectateur noclip).
	/// </summary>
	private bool VolCreatifStreamingReduit()
	{
		if (!JoueurEnModeVolCreatif())
			return false;
		if (!EssayerObtenirJoueurDansArbre(out _))
			return false;
		Vector3 obs = ObtenirPositionObservation();
		Vector2I chunkObs = Gestionnaire_Monde.WorldToChunkCoord(obs, TailleChunk);
		if (!ChunkDisponiblePourObservation(chunkObs, obs))
			return false;
		return ChunkMeshGrilleSousPiedsPret();
	}

	private int ObtenirRayonSecuriteSolActif()
	{
		int rayon = Mathf.Max(1, RayonDormancePhysique);
		bool joueurValide = EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef);
		Vector3 posJoueur = joueurValide ? joueurRef.GlobalPosition : Vector3.Zero;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			if (VolCreatifStreamingReduit())
				return 2;
			if (joueurValide && ConstantesDimensionAbysse.EstDansTrouNoirXZ(posJoueur.X, posJoueur.Z))
				return Mathf.Clamp(rayon, 2, 3);
			if (joueurValide)
			{
				Vector3 v = joueurRef.Velocity;
				float vitesseXZ = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
				bool localPret = AbysseCollisionLocaleActive(posJoueur);
				if (!localPret || vitesseXZ >= 4.0f || v.Y < -0.4f)
					rayon = Mathf.Max(5, rayon);
				else
					rayon = Mathf.Clamp(rayon, 3, 4);
			}
			else
			{
				rayon = Mathf.Max(4, rayon);
			}
		}
		return rayon;
	}

	private int ObtenirRayonUrgenceCollisionActif()
	{
		int rayon = Mathf.Max(1, RayonPrioriteCollisionJoueur);
		bool joueurValide = EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef);
		Vector3 posJoueur = joueurValide ? joueurRef.GlobalPosition : Vector3.Zero;
		if (_dimensionReseauActive != (int)DimensionJeu.Abysse && ModeProfondeurTranchesActif() && joueurValide)
		{
			if (!ChunkSousPiedsAPret())
				rayon = Mathf.Max(rayon, 3);
			else
			{
				Vector3 v = joueurRef.Velocity;
				float vitesseXZ = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
				if (vitesseXZ >= 2.5f || v.Y < -0.35f)
					rayon = Mathf.Max(rayon, 3);
			}
		}
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			if (VolCreatifStreamingReduit())
				return 2;
			if (joueurValide && ConstantesDimensionAbysse.EstDansTrouNoirXZ(posJoueur.X, posJoueur.Z))
				return Mathf.Clamp(rayon, 2, 3);
			if (joueurValide)
			{
				Vector3 v = joueurRef.Velocity;
				float vitesseXZ = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
				bool localPret = AbysseCollisionLocaleActive(posJoueur);
				if (!localPret || vitesseXZ >= 4.0f || v.Y < -0.4f)
					rayon = Mathf.Max(5, rayon);
				else
					rayon = Mathf.Clamp(rayon, 3, 4);
			}
			else
			{
				rayon = Mathf.Max(4, rayon);
			}
		}
		return rayon;
	}

	// Références vers l'UI
	private Panel _slotGauche;
	private Panel _slotDroite;

	public override void _Ready()
	{
		// Profilage perf : actif uniquement en éditeur (F5). Dans une version exportée (launcher/joueurs),
		// OS.HasFeature("editor") est faux → profileur auto-désactivé (pas de spam log chez les joueurs).
		ActiverProfilagePerfMondeClient = ActiverProfilagePerfMondeClient && OS.HasFeature("editor");
		// Assure-toi que les chemins correspondent à ton arborescence exacte
		_slotGauche = GetNode<Panel>("../HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Gauche");
		_slotDroite = GetNode<Panel>("../HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Droite");
		InitialiserHorizonLointain();
		InitialiserOcclusionVisuelle();
	}

	public override void _ExitTree()
	{
		_mondeClientSortieEnCours = true;
		base._ExitTree();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree() || _mondeClientSortieEnCours) return; // GARROT SPATIAL : pas de manipulation de chunks si l'arbre s'effondre.
		float dt = (float)delta;
		ulong debutFramePerfUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownRebuildRadar = Mathf.Max(0f, _cooldownRebuildRadar - dt);
		_cooldownServicesLointains = Mathf.Max(0f, _cooldownServicesLointains - dt);
		_cooldownDiagCoherenceAbysse = Mathf.Max(0f, _cooldownDiagCoherenceAbysse - dt);
		_cooldownLogDiagnosticCollisionAbysse = Mathf.Max(0f, _cooldownLogDiagnosticCollisionAbysse - dt);
		_cooldownDrainProfilage += dt;
		TraiterAnimationsEmergence(dt);
		Camera3D cameraActive = ObtenirCameraObservation();
		bool joueurValide = EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef);
		Vector3 positionJoueurSecurisee = joueurValide ? joueurRef.GlobalPosition : Vector3.Zero;
		_joueurPresentPourWorkers = joueurValide;
		_coordYJoueurProfondeurCache = joueurValide
			? CoordYDepuisMondeY((int)Mathf.Floor(positionJoueurSecurisee.Y))
			: int.MinValue;
		bool coordYJoueurChange = joueurValide
			&& _coordYJoueurProfondeurCache != _ancienCoordYJoueur;
		if (coordYJoueurChange)
		{
			_ancienCoordYJoueur = _coordYJoueurProfondeurCache;
			if (ModeProfondeurTranchesActif())
			{
				DemanderFenetreVerticaleUrgenteAutourPosition(
					positionJoueurSecurisee,
					Mathf.Max(1, RayonGrilleMinSpawnPret));
				_tempsDepuisNettoyage = IntervalleNettoyageChunks;
			}
		}
		Vector3 directionJoueurSecurisee = joueurValide ? (-joueurRef.GlobalTransform.Basis.Z).Normalized() : Vector3.Forward;
		Vector3 positionObservation = joueurValide ? positionJoueurSecurisee : Vector3.Zero;
		Vector3 positionJoueur = joueurValide ? positionJoueurSecurisee : positionObservation;
		Vector3 directionObservation = directionJoueurSecurisee;
		if (cameraActive != null)
		{
			try
			{
				if (cameraActive.IsInsideTree())
				{
					positionObservation = cameraActive.GlobalPosition;
					directionObservation = (-cameraActive.GlobalTransform.Basis.Z).Normalized();
				}
			}
			catch (ObjectDisposedException)
			{
				// Caméra détruite pendant la frame: fallback sur joueur/zero.
			}
		}
		Vector2I chunkObservationActuel = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			_timerTrimAbysse -= dt;
			if (_timerTrimAbysse <= 0f)
			{
				_timerTrimAbysse = IntervalleTrimAbysseSec;
				PurgerChunksAbysseHorsFenetre(positionObservation, positionJoueur);
			}
			if (ActiverDiagnosticCollisionAbysse && joueurValide && _cooldownLogDiagnosticCollisionAbysse <= 0f)
			{
				JournaliserDiagnosticCollisionAbysse(positionObservation);
				_cooldownLogDiagnosticCollisionAbysse = IntervalleDiagnosticCollisionAbysseSec;
			}
		}
		_cooldownReservePrioritaireSec = Mathf.Max(0f, _cooldownReservePrioritaireSec - dt);
		MettreAJourAutoDiagnostic(dt);
		_remeshMinageSyncRestantFrame = _modificationEnCours ? 14 : 4;
		if (joueurValide && ModeProfondeurTranchesActif()
			&& ConstantesProfondeurVerticale.EstProcheJonctionTrancheMonde(positionJoueurSecurisee.Y))
			_remeshMinageSyncRestantFrame = Mathf.Max(_remeshMinageSyncRestantFrame, _modificationEnCours ? 18 : 8);
		int niveauUrgence = _niveauUrgencePerf;
		float budgetFrameMs = Mathf.Clamp(BudgetFrameCibleMs, 8f, 25f);
		if (niveauUrgence >= 3) budgetFrameMs -= Mathf.Clamp(MargeBudgetUrgenceMs, 0.1f, 4f);
		else if (niveauUrgence >= 2) budgetFrameMs -= Mathf.Clamp(MargeBudgetUrgenceMs * 0.6f, 0.1f, 4f);
		budgetFrameMs = Mathf.Clamp(budgetFrameMs, 7.2f, 25f);
		ulong budgetFrameUs = (ulong)Mathf.Max(1000f, budgetFrameMs * 1000f);
		bool BudgetFrameDepasse() => PerfBudgetMonitor.Begin() - debutFramePerfUs >= budgetFrameUs;
		float vitesseJoueurXZ = 0f;
		if (joueurValide)
		{
			Vector3 vv = joueurRef.Velocity;
			vitesseJoueurXZ = Mathf.Sqrt(vv.X * vv.X + vv.Z * vv.Z);
		}
		bool prioriteJoueur = vitesseJoueurXZ >= SeuilVitessePrioriteJoueur;
		Vector3 velXZJoueur = joueurValide
			? new Vector3(joueurRef.Velocity.X, 0f, joueurRef.Velocity.Z)
			: Vector3.Zero;
		bool meshGrilleSousPieds = joueurValide && ChunkMeshGrilleSousPiedsPret();
		bool collisionPret = ChunkSousPiedsAPret();
		bool joueurEnChute = joueurValide && joueurRef.Velocity.Y < -0.5f;
		bool enChargement = ModeProfondeurTranchesActif()
			? (_timerGraceStreamingBootstrap > 0f ? !collisionPret : (!collisionPret && !meshGrilleSousPieds))
			: !collisionPret;
		bool enVideAttenduAbyssePrecoce = _dimensionReseauActive == (int)DimensionJeu.Abysse
			&& joueurValide
			&& EstVideAbysseAttendu(positionJoueurSecurisee);
		if (enVideAttenduAbyssePrecoce)
			enChargement = false;
		// Sol sûr : mesh + collision sous les pieds, pas en chute — le rattrapage lointain ne doit pas voler le budget fluidité.
		bool solSecuriseSousPieds = meshGrilleSousPieds && collisionPret && !joueurEnChute && !enChargement;
		bool corridorStreamingEnRetard = joueurValide && CorridorStreamingEnRetard(positionJoueurSecurisee, velXZJoueur);
		bool corridorEnRetard = joueurValide && (CorridorSolidificationEnRetard(positionJoueurSecurisee, velXZJoueur)
			|| corridorStreamingEnRetard);
		if (joueurValide)
		{
			int rayonUrgenceCollision = ObtenirRayonUrgenceCollisionActif();
			bool solidifUrgenteNecessaire = !solSecuriseSousPieds || _fpsMoyenneAuto >= 54f;
			if (solidifUrgenteNecessaire)
				EnfilerSolidificationUrgenteAutour(positionJoueurSecurisee, rayonUrgenceCollision);
			else
				EnfilerSolidificationUrgenteAutour(positionJoueurSecurisee, Mathf.Min(1, rayonUrgenceCollision));
			if (ModeProfondeurTranchesActif())
				MaintenirJonctionsTranchesAutourJoueur(positionJoueurSecurisee, dt);
			if (prioriteJoueur && !solSecuriseSousPieds)
			{
				Vector3 vel = joueurRef.Velocity;
				Vector3 velXZ = new Vector3(vel.X, 0f, vel.Z);
				if (velXZ.LengthSquared() > 0.25f)
				{
					Vector3 pointAnticipe = positionJoueurSecurisee + velXZ * Mathf.Max(0.35f, SecondesAnticipationCollision);
					EnfilerSolidificationUrgenteAutour(pointAnticipe, rayonUrgenceCollision);
				}
			}
		}
		_timerEpurationBacklog -= dt;
		if (_timerEpurationBacklog <= 0f)
		{
			_timerEpurationBacklog = IntervalleEpurationBacklogSec;
			EpurerBacklogsChunkLointains(positionObservation);
		}
		int backlogCharge = CompterBacklog();
		float facteurAntiSpikeBacklog = 1f;
		if (ModeSurvieFpsAgressif)
		{
			if (backlogCharge > SeuilBacklogHaut) facteurAntiSpikeBacklog *= 0.82f;
			if (backlogCharge > SeuilBacklogHaut + 28) facteurAntiSpikeBacklog *= 0.72f;
			if (backlogCharge > SeuilBacklogHaut + 64) facteurAntiSpikeBacklog *= 0.62f;
		}
		bool urgencePerfExtreme = niveauUrgence >= 3;
		bool urgencePerfCritique = niveauUrgence >= 2;
		bool urgencePerfForte = niveauUrgence >= 1;
		float budgetIntegrationsMs = Mathf.Clamp(BudgetMsIntegrationsMainThread, 0.2f, 8f);
		float budgetSolidificationMs = Mathf.Clamp(BudgetMsSolidificationMainThread, 0.2f, 8f);
		float budgetWorkersMainMs = Mathf.Clamp(BudgetMsLancementWorkersMainThread, 0.1f, 4f);
		if (ModeSurvieFpsAgressif)
		{
			if (urgencePerfExtreme)
			{
				budgetIntegrationsMs *= 0.68f;
				budgetSolidificationMs *= 0.70f;
				budgetWorkersMainMs *= 0.65f;
			}
			else if (urgencePerfCritique)
			{
				budgetIntegrationsMs *= 0.78f;
				budgetSolidificationMs *= 0.80f;
				budgetWorkersMainMs *= 0.78f;
			}
			else if (urgencePerfForte)
			{
				budgetIntegrationsMs *= 0.90f;
				budgetSolidificationMs *= 0.92f;
				budgetWorkersMainMs *= 0.90f;
			}
		}

		// 2) Intégrations : chargement initial agressif ; exploration : plusieurs par frame pour suivre un monde infini.
		bool enVideAttenduAbysse = enVideAttenduAbyssePrecoce;
		// Anti-chute strict ; corridor en retard = rattrapage doux si le sol local est déjà sûr (préserve ~60 FPS en marche).
		bool doitGarantirProcheJoueur = enChargement || joueurEnChute
			|| (prioriteJoueur && !meshGrilleSousPieds)
			|| (!solSecuriseSousPieds && corridorEnRetard);
		_streamingChunksPrioritaireCetteFrame = EstStreamingChunksPrioritaire(enChargement, doitGarantirProcheJoueur);
		if (solSecuriseSousPieds && _fpsMoyenneAuto < 58f)
			_streamingChunksPrioritaireCetteFrame = enChargement || doitGarantirProcheJoueur;
		if (enVideAttenduAbysse)
		{
			float distanceCentre = Mathf.Sqrt((positionJoueurSecurisee.X * positionJoueurSecurisee.X) + (positionJoueurSecurisee.Z * positionJoueurSecurisee.Z));
			// Marge élargie pour éviter les décrochages de solidification en descente proche paroi.
			bool procheParoiTrou = Mathf.Abs(distanceCentre - ConstantesDimensionAbysse.RayonTrouNoir) <= 220f;
			doitGarantirProcheJoueur = procheParoiTrou && (joueurEnChute || prioriteJoueur);
		}
		int baseIntegrations = enChargement
			? Mathf.Max(1, MaxIntegrationsParFrameChargement)
			: Mathf.Max(1, MaxIntegrationsParFrameExploration);
		int maxIntegrations = Mathf.Clamp(Mathf.RoundToInt(baseIntegrations * Mathf.Lerp(0.60f, 1.15f, _ratioChargeAuto) * facteurAntiSpikeBacklog), 1, Mathf.Max(1, baseIntegrations + 2));
		int budgetVerticesBase = enChargement
			? Mathf.Max(25000, BudgetVerticesIntegrationParFrameChargement)
			: Mathf.Max(18000, BudgetVerticesIntegrationParFrameExploration);
		float ratioVertices = Mathf.Lerp(0.58f, 1.25f, _ratioChargeAuto) * facteurAntiSpikeBacklog;
		if (_timerFreinSpike > 0f) ratioVertices *= 0.70f;
		int budgetVerticesDyn = Mathf.Clamp(Mathf.RoundToInt(budgetVerticesBase * ratioVertices), 12000, Mathf.Max(12000, budgetVerticesBase + 55000));
		// Mode « Sauver les FPS » : en exploration, 1 mesh lourd/frame max (évite le lag à chaque chunk).
		if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && !corridorStreamingEnRetard && !enChargement)
		{
			maxIntegrations = 1;
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, BudgetVerticesIntegrationParFrameExploration);
		}
		else if (ModeSurvieFpsAgressif && prioriteJoueur && !corridorStreamingEnRetard && enChargement)
		{
			maxIntegrations = Mathf.Max(2, Mathf.Min(maxIntegrations, 3));
			budgetVerticesDyn = Mathf.Max(18000, Mathf.RoundToInt(budgetVerticesDyn * 0.78f));
		}
		// Les restrictions d'urgence ne s'appliquent JAMAIS si le sol proche joueur n'est pas prêt (sinon chute dans le vide).
		if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && !corridorStreamingEnRetard && urgencePerfExtreme)
		{
			maxIntegrations = 1;
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 12000);
		}
		else if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && !corridorStreamingEnRetard && urgencePerfCritique)
		{
			maxIntegrations = 1;
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 12000);
		}
		else if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && !corridorStreamingEnRetard && urgencePerfForte)
		{
			maxIntegrations = Mathf.Min(maxIntegrations, 2);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 18000);
		}
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse && !doitGarantirProcheJoueur)
		{
			maxIntegrations = Mathf.Min(maxIntegrations, 1);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 14000);
		}
		if (VolCreatifStreamingReduit())
		{
			maxIntegrations = Mathf.Min(maxIntegrations, 1);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 10000);
		}
		// Plancher anti-chute : si le sol manque, accélérer le chargement local (sans burst permanent en exploration).
		if (doitGarantirProcheJoueur)
		{
			bool solVisuelPret = meshGrilleSousPieds && !corridorEnRetard && !enChargement;
			maxIntegrations = Mathf.Max(maxIntegrations, solVisuelPret ? 2 : 3);
			int plafondVerticesAntiChute = solVisuelPret
				? (ModeSurvieFpsAgressif ? 10000 : 14000)
				: 22000;
			budgetVerticesDyn = Mathf.Max(budgetVerticesDyn, plafondVerticesAntiChute);
			budgetIntegrationsMs = Mathf.Max(budgetIntegrationsMs, 3.2f);
			budgetSolidificationMs = Mathf.Max(budgetSolidificationMs, 3.0f);
			budgetWorkersMainMs = Mathf.Max(budgetWorkersMainMs, 1.0f);
			if (ModeProfondeurTranchesActif())
			{
				budgetSolidificationMs = meshGrilleSousPieds && !corridorEnRetard
					? Mathf.Max(budgetSolidificationMs, 2.8f)
					: Mathf.Max(budgetSolidificationMs, 5.5f);
			}
			if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			{
				// APISARA (muraille comprise) : 1 mesh lourd/frame max hors urgence anti-chute.
				int plafondIntegAbysse = (doitGarantirProcheJoueur && (joueurEnChute || vitesseJoueurXZ >= 3.5f)) ? 2 : 1;
				maxIntegrations = Mathf.Clamp(maxIntegrations, 1, plafondIntegAbysse);
				budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 14000);
			}
		}
		if (corridorStreamingEnRetard && !solSecuriseSousPieds)
		{
			int integCorridor = ModeSurvieFpsAgressif ? (enChargement ? 3 : 2) : (enChargement ? 4 : 3);
			maxIntegrations = Mathf.Max(maxIntegrations, integCorridor);
			int plafondCorridor = ModeSurvieFpsAgressif
				? (enChargement ? 16000 : 12000)
				: (enChargement ? 28000 : 22000);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, plafondCorridor);
		}
		// GATE FPS STRICT : hors zone critique, gel total si FPS < seuil, puis ramp-up 1→budget.
		maxIntegrations = AppliquerGateEtRampUp(maxIntegrations, doitGarantirProcheJoueur, 1);
		// Sous gel : drain minimal (1 mesh léger/frame) — ne PAS forcer 22k vertices (spirale 10 FPS).
		if (!doitGarantirProcheJoueur && _gateStreamingGele)
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, ModeSurvieFpsAgressif ? 6000 : 9000);
		else if (!doitGarantirProcheJoueur && _tempsDepuisDegel < DureeRampUpPostDegel)
		{
			float tRamp = Mathf.Clamp(_tempsDepuisDegel / Mathf.Max(0.01f, DureeRampUpPostDegel), 0f, 1f);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, Mathf.RoundToInt(Mathf.Lerp(8000, budgetVerticesDyn, tRamp)));
		}
		// File d’intégration non vide : garder un flux minimal même sous gate, sans forcer un burst qui freeze.
		if (!doitGarantirProcheJoueur && _fileIntegrationMainThread.TryPeek(out _)
			&& (_gateStreamingGele || maxIntegrations <= 1))
		{
			maxIntegrations = Mathf.Max(maxIntegrations, 1);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, _gateStreamingGele ? 7000 : 10000);
		}
		bool autoriserIntegrations = true;
		bool autoriserSolidifications = true;
		bool autoriserWorkers = true;
		// Hors zone critique joueur, sérielle stricte : une seule famille de charge lourde par frame.
		if (ForcerOrdonnancementSerieAntiFreeze && !doitGarantirProcheJoueur && !corridorEnRetard && !corridorStreamingEnRetard)
		{
			bool pendingIntegrations = _fileIntegrationMainThread.TryPeek(out _);
			bool pendingSolidifications = _fileAttenteSolidificationUrgente.Count > 0 || _fileAttenteSolidification.Count > 0;
			bool pendingWorkers;
			lock (_lockFileAttenteMaths)
				pendingWorkers = _fileAttenteMathsData.Count > 0;
			if (pendingIntegrations || pendingSolidifications || pendingWorkers)
			{
				for (int i = 0; i < 3; i++)
				{
					int candidate = (_phaseOrdonnancementSerie + i) % 3;
					bool pendingCandidate = candidate switch
					{
						0 => pendingIntegrations,
						1 => pendingSolidifications,
						_ => pendingWorkers
					};
					if (!pendingCandidate) continue;
					autoriserIntegrations = candidate == 0;
					autoriserSolidifications = candidate == 1;
					autoriserWorkers = candidate == 2;
					_phaseOrdonnancementSerie = (candidate + 1) % 3;
					break;
				}
			}
		}
		if (!autoriserIntegrations)
			maxIntegrations = 0;
		int integrationsBacklog = _fileIntegrationMainThread.Count;
		if (ModeSurvieFpsAgressif && integrationsBacklog >= 2 && !doitGarantirProcheJoueur)
			maxIntegrations = Mathf.Min(maxIntegrations, 1);
		int integrations = 0;
		int verticesIntegres = 0;
		ulong debutIntegrationsUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		ulong budgetIntegrationsUs = (ulong)Mathf.Max(300UL, budgetIntegrationsMs * 1000f);
		while (integrations < maxIntegrations && _fileIntegrationMainThread.TryDequeue(out var integration))
		{
			if (integrations > 0 && PerfBudgetMonitor.Begin() - debutIntegrationsUs >= budgetIntegrationsUs)
			{
				_fileIntegrationMainThread.Enqueue(integration);
				break;
			}
			int cout = Mathf.Max(1, integration.CoutVerticesEstime);
			if (integrations > 0 && verticesIntegres + cout > budgetVerticesDyn)
			{
				_fileIntegrationMainThread.Enqueue(integration);
				break;
			}
			try { integration.Action.Invoke(); }
			catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
			catch (System.Exception ex) { JournalErreursZeroK.Erreur("Monde_Client intégration: " + ex.Message, forcerConsole: true); }
			verticesIntegres += cout;
			integrations++;
			if (integrations > 0 && PerfBudgetMonitor.Begin() - debutIntegrationsUs >= budgetIntegrationsUs)
				break;
		}
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/Integrations", debutIntegrationsUs);

		// 3) Solidification physique lissée : collisions urgentes (autour joueur) puis fond.
		ulong debutSolidificationUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		int solidificationsEffectuees = 0;
		if (autoriserSolidifications && (_fileAttenteSolidificationUrgente.Count > 0 || _fileAttenteSolidification.Count > 0))
		{
			Vector2I coordObsSolidif = chunkObservationActuel;
			if (_dimensionReseauActive == (int)DimensionJeu.Abysse && joueurValide)
				coordObsSolidif = Gestionnaire_Monde.WorldToChunkCoord(positionJoueurSecurisee, TailleChunk);
			int baseSolidifications = enChargement
				? (ModeProfondeurTranchesActif() ? 6 : 3)
				: Mathf.Max(1, MaxSolidificationsParFrameExploration);
			int maxSolidifications = Mathf.Clamp(Mathf.RoundToInt(baseSolidifications * Mathf.Lerp(0.60f, 1.12f, _ratioChargeAuto) * facteurAntiSpikeBacklog), 1, Mathf.Max(1, baseSolidifications + 2));
			if (prioriteJoueur && !solSecuriseSousPieds)
				maxSolidifications = Mathf.Max(maxSolidifications, Mathf.Max(3, MaxSolidificationsPrioriteJoueur));
			if (ModeProfondeurTranchesActif() && (doitGarantirProcheJoueur || (prioriteJoueur && !solSecuriseSousPieds)))
			{
				int plafondProf = meshGrilleSousPieds && !corridorEnRetard && !enChargement ? 4 : (enChargement ? 10 : 7);
				maxSolidifications = Mathf.Max(maxSolidifications, plafondProf);
			}
			if (_fileAttenteSolidificationUrgente.Count > 0)
				maxSolidifications = Mathf.Max(maxSolidifications, Mathf.Min(8, 3 + _fileAttenteSolidificationUrgente.Count / 6));
			if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && urgencePerfExtreme)
				maxSolidifications = Mathf.Min(maxSolidifications, 1);
			else if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && urgencePerfCritique)
				maxSolidifications = Mathf.Min(maxSolidifications, 2);
			else if (ModeSurvieFpsAgressif && !doitGarantirProcheJoueur && urgencePerfForte)
				maxSolidifications = Mathf.Min(maxSolidifications, 3);
			// Plancher anti-chute : si le sol proche joueur n'est pas prêt, garantir quelques solidifications par frame.
			if (doitGarantirProcheJoueur)
			{
				bool solVisuelPret = meshGrilleSousPieds && !corridorEnRetard && !enChargement;
				int plancherSol = ModeProfondeurTranchesActif()
					? (solVisuelPret ? 2 : (enChargement ? 5 : 4))
					: 4;
				maxSolidifications = Mathf.Max(maxSolidifications, plancherSol);
				if (ModeSurvieFpsAgressif && solVisuelPret && !joueurEnChute && vitesseJoueurXZ < 3f)
					maxSolidifications = Mathf.Min(maxSolidifications, 2);
				if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
				{
					// CreateTrimeshShape + BodySetSpace : un burst élevé gèle l’image. Plancher modéré pour le sol,
					// plafond serré ; on monte seulement en chute / déplacement rapide.
					maxSolidifications = Mathf.Clamp(Mathf.Max(maxSolidifications, 3), 1, 4);
					if (joueurEnChute || vitesseJoueurXZ >= 4.5f)
						maxSolidifications = Mathf.Clamp(maxSolidifications + 2, 1, 6);
				}
			}
			// GATE FPS STRICT : hors zone critique, 0 si gelé, puis ramp-up.
			maxSolidifications = AppliquerGateEtRampUp(maxSolidifications, doitGarantirProcheJoueur, 1);
			// Plafond CreateTrimeshShape / frame (mémoire temporaire Jolt) hors zone critique déjà couverte par doitGarantirProcheJoueur.
			if (!doitGarantirProcheJoueur && ModeSurvieFpsAgressif)
			{
				int plafondJolt = _niveauUrgencePerf >= 3 ? 1 : (_niveauUrgencePerf >= 2 ? 2 : (_niveauUrgencePerf == 1 ? 3 : 4));
				if (StreamingPeutElargirTranquillement())
					plafondJolt = Mathf.Min(5, plafondJolt + 1);
				maxSolidifications = Mathf.Min(maxSolidifications, plafondJolt);
			}
			int efforts = 0;
			World3D w = GetWorld3D();
			ulong budgetSolidificationUs = (ulong)Mathf.Max(300UL, budgetSolidificationMs * 1000f);
			Vector3 posSolidifUrgente = joueurValide ? positionJoueurSecurisee : positionObservation;
			while (_fileAttenteSolidificationUrgente.Count > 0 && efforts < maxSolidifications && w != null)
			{
				if (efforts > 0 && PerfBudgetMonitor.Begin() - debutSolidificationUs >= budgetSolidificationUs)
					break;
				if (!PreleverSolidificationUrgenteProche(posSolidifUrgente, out ChunkData urgent) || urgent == null)
					break;
				AssurerCorpsPhysiqueChunk(urgent);
				if (urgent.PhysicsBodyRID.IsValid)
				{
					PhysicsServer3D.Singleton.BodySetSpace(urgent.PhysicsBodyRID, w.Space);
					urgent.EstEnFileSolidification = false;
					SynchroniserFloreDesQueCollisionChunkActive(urgent);
				}
				else if (urgent.EstVideIntegral && _dimensionReseauActive == (int)DimensionJeu.Abysse)
				{
					urgent.EstEnFileSolidification = false;
				}
				efforts++;
				if (efforts > 0 && PerfBudgetMonitor.Begin() - debutSolidificationUs >= budgetSolidificationUs)
					break;
			}
			while (_fileAttenteSolidification.Count > 0 && efforts < maxSolidifications)
			{
				if (efforts > 0 && PerfBudgetMonitor.Begin() - debutSolidificationUs >= budgetSolidificationUs)
					break;
				int idxProche = ExtraireIndexSolidificationProche(coordObsSolidif);
				ChunkData chunkASolidifier = _fileAttenteSolidification[idxProche];
				int dx = Mathf.Abs(chunkASolidifier.Coordonnees.X - coordObsSolidif.X);
				int dz = Mathf.Abs(chunkASolidifier.Coordonnees.Y - coordObsSolidif.Y);
				if (dx <= RayonDormancePhysique && dz <= RayonDormancePhysique && w != null)
				{
					AssurerCorpsPhysiqueChunk(chunkASolidifier);
					if (chunkASolidifier.PhysicsBodyRID.IsValid)
					{
						_fileAttenteSolidification.RemoveAt(idxProche);
						_setSolidificationNormale.Remove(chunkASolidifier);
						chunkASolidifier.EstEnFileSolidification = false;
						PhysicsServer3D.Singleton.BodySetSpace(chunkASolidifier.PhysicsBodyRID, w.Space);
						SynchroniserFloreDesQueCollisionChunkActive(chunkASolidifier);
					}
					else
					{
						_fileAttenteSolidification.RemoveAt(idxProche);
						_setSolidificationNormale.Remove(chunkASolidifier);
						if (chunkASolidifier.EstVideIntegral && _dimensionReseauActive == (int)DimensionJeu.Abysse)
							chunkASolidifier.EstEnFileSolidification = false;
						else
							AjouterEnFileSolidification(chunkASolidifier);
					}
				}
				else
				{
					_fileAttenteSolidification.RemoveAt(idxProche);
					_setSolidificationNormale.Remove(chunkASolidifier);
					if (chunkASolidifier.EstVideIntegral && _dimensionReseauActive == (int)DimensionJeu.Abysse)
						chunkASolidifier.EstEnFileSolidification = false;
					else
						AjouterEnFileSolidification(chunkASolidifier);
				}
				efforts++;
				if (efforts > 0 && PerfBudgetMonitor.Begin() - debutSolidificationUs >= budgetSolidificationUs)
					break;
			}
			solidificationsEffectuees = efforts;
		}
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/Solidification", debutSolidificationUs);

		// FORGE RESTREINTE : lancer au plus MaxTravailleurs calculs en arrière-plan (tri par distance au joueur).
		Vector2I obsChunk = chunkObservationActuel;
		int maxTravailleurs = _maxTravailleursDyn;
		int budgetLancementsWorkers = Mathf.Clamp(MaxLancementsTravailleursParTick, 1, 8);
		float ratioWorkers = Mathf.Lerp(0.55f, 1.15f, _ratioChargeAuto) * facteurAntiSpikeBacklog;
		if (_timerFreinSpike > 0f) ratioWorkers *= 0.72f;
		budgetLancementsWorkers = Mathf.Clamp(Mathf.RoundToInt(budgetLancementsWorkers * ratioWorkers), 1, 8);
		if (ModeSurvieFpsAgressif && urgencePerfExtreme && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = 1;
			budgetLancementsWorkers = 1;
		}
		else if (ModeSurvieFpsAgressif && urgencePerfCritique && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = Mathf.Min(maxTravailleurs, 1);
			budgetLancementsWorkers = 1;
		}
		else if (ModeSurvieFpsAgressif && urgencePerfForte && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = Mathf.Min(maxTravailleurs, 2);
			budgetLancementsWorkers = Mathf.Min(budgetLancementsWorkers, 1);
		}
		// Plancher dur : si le sol manque ou joueur en chute, garantir au moins 2 workers pour que les maths sortent rapidement.
		if (doitGarantirProcheJoueur)
		{
			maxTravailleurs = Mathf.Max(maxTravailleurs, Mathf.Min(3, Mathf.Max(2, System.Environment.ProcessorCount / 2)));
			budgetLancementsWorkers = Mathf.Max(budgetLancementsWorkers, 2);
		}
		// GATE FPS STRICT : hors zone critique, zero worker lancé si gelé, puis ramp-up.
		maxTravailleurs = AppliquerGateEtRampUp(maxTravailleurs, doitGarantirProcheJoueur, 1);
		budgetLancementsWorkers = AppliquerGateEtRampUp(budgetLancementsWorkers, doitGarantirProcheJoueur, 1);
		// Évite rafales : si des meshes attendent le thread principal, ne pas lancer de nouveaux MC en parallèle.
		int integrationBacklogWorkers = _fileIntegrationMainThread.Count;
		if (ModeSurvieFpsAgressif && integrationBacklogWorkers >= 2 && !doitGarantirProcheJoueur)
		{
			budgetLancementsWorkers = Mathf.Min(budgetLancementsWorkers, 1);
			maxTravailleurs = Mathf.Min(maxTravailleurs, Mathf.Max(1, Thread.VolatileRead(ref _chunksEnCoursDeCalcul)));
		}
		if (ModeSurvieFpsAgressif && integrationBacklogWorkers >= 5 && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = 0;
			budgetLancementsWorkers = 0;
		}
		if (ModeSurvieFpsAgressif && _sectionsAReconstruire.Count > 14 && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = 0;
			budgetLancementsWorkers = 0;
		}
		if (!autoriserWorkers)
		{
			maxTravailleurs = 0;
			budgetLancementsWorkers = 0;
		}
		int workersLancesTick = 0;
		ulong debutWorkersUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		ulong budgetWorkersUs = (ulong)Mathf.Max(200UL, budgetWorkersMainMs * 1000f);
		while (Thread.VolatileRead(ref _chunksEnCoursDeCalcul) < maxTravailleurs && workersLancesTick < budgetLancementsWorkers)
		{
			if (workersLancesTick > 0 && PerfBudgetMonitor.Begin() - debutWorkersUs >= budgetWorkersUs)
				break;
			ChunkData chunkData = null;
			DonneesChunk donnees = null;
			lock (_lockFileAttenteMaths)
			{
				if (_fileAttenteMathsData.Count == 0) break;
				int best = 0;
				float bestD = float.MaxValue;
				int fenetreSelection = Mathf.Clamp(FenetreSelectionTravailMaths, 4, 256);
				int limiteScan = Mathf.Min(_fileAttenteMathsData.Count, fenetreSelection);
				bool profondeurMultiCouche = ModeProfondeurTranchesActif();
				int coordYObs = profondeurMultiCouche
					? CoordYDepuisMondeY((int)Mathf.Floor(positionObservation.Y))
					: 0;
				for (int i = 0; i < limiteScan; i++)
				{
					var jobData = _fileAttenteMathsData[i].data;
					var c = jobData.Coordonnees;
					float d = (c.X - obsChunk.X) * (c.X - obsChunk.X) + (c.Y - obsChunk.Y) * (c.Y - obsChunk.Y);
					if (profondeurMultiCouche)
					{
						int dy = jobData.CoordChunkY - coordYObs;
						d += dy * dy * 0.5f;
					}
					if (d < bestD) { bestD = d; best = i; }
				}
				var job = _fileAttenteMathsData[best];
				_fileAttenteMathsData.RemoveAt(best);
				chunkData = job.data;
				donnees = job.donnees;
			}
			if (chunkData == null || donnees == null) break;
			Interlocked.Increment(ref _chunksEnCoursDeCalcul);
			var mondeRef = this;
			var enqueueIntegration = EnqueueIntegration;
			// Pré-construire la collision dans le worker seulement pour les chunks proches qui vont être
			// solidifiés (anti-pic streaming), sans gaspiller mémoire/CPU sur les chunks lointains.
			bool prochePhysique =
				Mathf.Abs(chunkData.Coordonnees.X - obsChunk.X) <= RayonDormancePhysique + 1
				&& Mathf.Abs(chunkData.Coordonnees.Y - obsChunk.Y) <= RayonDormancePhysique + 1
				&& (!ModeProfondeurTranchesActif()
					|| (_coordYJoueurProfondeurCache != int.MinValue
						&& Mathf.Abs(chunkData.CoordChunkY - _coordYJoueurProfondeurCache)
							<= ConstantesProfondeurVerticale.DemiFenetrePhysiqueTranches));
			Task.Run(() =>
			{
				try
				{
					Chunk_Client.RemplirDonneesVoxelDepuisServeur(chunkData, donnees);
					if (ModeProfondeurTranchesActif())
						mondeRef.SynchroniserFrontieresVerticalesProfondeurClient(chunkData, postTraitementLegermachine: true);
					var payloads = Chunk_Client.ReconstruirePayloadsDepuisData(
						chunkData, mondeRef.TryEchantillonnerVoxelProfondeur);
					if (payloads != null)
					{
						int coutVertices = 0;
						for (int i = 0; i < payloads.Count; i++)
						{
							var p = payloads[i];
							if (p?.SommetsVisuels != null) coutVertices += p.SommetsVisuels.Length;
							if (p?.SommetsEau != null) coutVertices += p.SommetsEau.Length;
						}
						if (prochePhysique)
						{
							try { chunkData.ShapeCollisionPrecalc = Chunk_Client.ConstruireShapeCollisionDepuisPayloads(payloads); }
							catch { chunkData.ShapeCollisionPrecalc = null; }
						}
						enqueueIntegration(() => mondeRef.IntegrerChunkDataRIDs(chunkData, payloads), coutVertices);
					}
				}
				finally
				{
					Interlocked.Decrement(ref _chunksEnCoursDeCalcul);
				}
			});
			workersLancesTick++;
			if (workersLancesTick > 0 && PerfBudgetMonitor.Begin() - debutWorkersUs >= budgetWorkersUs)
				break;
		}
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/LancementWorkers", debutWorkersUs);
		ReinitialiserCompteurSolidificationCorridorFrame();
		if (joueurValide && ModeProfondeurTranchesActif()
			&& !solSecuriseSousPieds
			&& (corridorEnRetard || enChargement || _fpsMoyenneAuto >= 50f))
			SolidifierVolumesVisiblesAutourJoueur(positionJoueurSecurisee, _fpsMoyenneAuto);
		bool frameChargeeStreaming = integrations > 0 || solidificationsEffectuees > 0 || workersLancesTick > 0;
		// « Sauver les FPS » : économise culling/radar lointain si budget serré, mais ne bloque pas la génération de chunks.
		bool economiserServicesFondLointains = ModeSurvieFpsAgressif
			&& BudgetFrameDepasse()
			&& !_streamingChunksPrioritaireCetteFrame;
		bool phaseFondAutorisee = !economiserServicesFondLointains
			&& (!urgencePerfCritique || !frameChargeeStreaming || _streamingChunksPrioritaireCetteFrame);

		// Voxels RPC reçus avant chargement tranche : appliquer AVANT le remesh (sinon déchirure 1–N frames).
		AppliquerVoxelsEnAttente();

		bool hadModifications = _sectionsAReconstruire.Count > 0;

		// PRIORITÉ ABSOLUE : remesh partiel (sections) au minage — synchrone près du joueur.
		if (hadModifications)
		{
			_sectionsParChunkRemeshTemp.Clear();
			foreach (var cible in _sectionsAReconstruire)
			{
				var cleChunk = new Vector3I(cible.cx, cible.coordY, cible.cz);
				if (!_sectionsParChunkRemeshTemp.TryGetValue(cleChunk, out HashSet<int> secs))
				{
					secs = new HashSet<int>();
					_sectionsParChunkRemeshTemp[cleChunk] = secs;
				}
				secs.Add(cible.section);
			}
			int budgetSectionsRemesh = enChargement ? 3 : 6;
			if (prioriteJoueur && !enChargement)
				budgetSectionsRemesh = Mathf.Min(budgetSectionsRemesh, 3);
			if (_modificationEnCours)
				budgetSectionsRemesh = Mathf.Max(budgetSectionsRemesh, ModeSurvieFpsAgressif ? 12 : 18);
			else if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 45f)
				budgetSectionsRemesh = 2;
			else if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 55f)
				budgetSectionsRemesh = Mathf.Max(2, budgetSectionsRemesh / 2);
			if (!_modificationEnCours && _sectionsAReconstruire.Count > 24)
				budgetSectionsRemesh = 1;
			if (_niveauUrgencePerf >= 2 && !_modificationEnCours)
				budgetSectionsRemesh = Mathf.Max(1, budgetSectionsRemesh / 2);
			int sectionsRemeshues = 0;
			_sectionsRemeshTraiteesTemp.Clear();
			_remeshOrdreChunksTemp.Clear();
			_remeshOrdreChunksTemp.AddRange(_sectionsParChunkRemeshTemp.Keys);
			if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRemesh))
			{
				Vector3 posJ = joueurRemesh.GlobalPosition;
				_remeshOrdreChunksTemp.Sort((a, b) =>
				{
					float da = ObtenirCentreMondeChunkApprox(a).DistanceSquaredTo(posJ);
					float db = ObtenirCentreMondeChunkApprox(b).DistanceSquaredTo(posJ);
					return da.CompareTo(db);
				});
			}
			foreach (Vector3I cleChunk in _remeshOrdreChunksTemp)
			{
				if (sectionsRemeshues >= budgetSectionsRemesh)
					break;
				if (!_sectionsParChunkRemeshTemp.TryGetValue(cleChunk, out HashSet<int> secsChunk) || secsChunk.Count == 0)
					continue;
				int reste = budgetSectionsRemesh - sectionsRemeshues;
				var batch = new HashSet<int>();
				foreach (int sec in secsChunk)
				{
					if (batch.Count >= reste)
						break;
					batch.Add(sec);
				}
				if (batch.Count == 0)
					continue;
				if (EstRemeshPrioritaireMinage(cleChunk) || _modificationEnCours)
					ExecuterReconstructionPrioritaire(cleChunk, batch);
				else
					EnfilerRemeshSectionsEnArrierePlan(cleChunk, batch);
				foreach (int sec in batch)
				{
					_sectionsRemeshTraiteesTemp.Add((cleChunk.X, cleChunk.Y, cleChunk.Z, sec));
					sectionsRemeshues++;
				}
			}
			if (_sectionsRemeshTraiteesTemp.Count > 0)
			{
				_sectionsAReconstruire.RemoveWhere(s =>
					_sectionsRemeshTraiteesTemp.Contains((s.cx, s.coordY, s.cz, s.section)));
			}
		}
		if (_sectionsAReconstruire.Count == 0)
			_modificationEnCours = false;

		// En Abysse : requêtes proches si collision locale pas prête ou déplacement rapide.
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse && joueurValide)
		{
			Vector3 posJoueur = positionJoueurSecurisee;
			bool enVideAttendu = EstVideAbysseAttendu(posJoueur);
			Vector3 v = joueurRef.Velocity;
			float vitesseXZ = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
			bool localPret = AbysseCollisionLocaleActive(posJoueur);
			if ((!localPret && !enVideAttendu) || vitesseXZ >= 2.5f || v.Y < -0.5f)
			{
				Vector2I chunkJoueur = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
				GarantirRequetesChunksProcheJoueur(posJoueur, chunkJoueur);
			}
		}

		Vector2I chunkPieds = chunkObservationActuel;
		if (!phaseFondAutorisee)
		{
			// Flore de secours: même quand on coupe les services de fond, on garde un filet visuel
			// pour éviter l'effet "plus d'herbe/buissons" en mouvement.
			int budgetFloreSecours = Mathf.Min(1, CalculerBudgetFloreDynamique(enChargement, prioriteJoueur));
			if (budgetFloreSecours > 0 && _fileFloreDifferee.Count > 0)
			{
				ulong debutFloreSecoursUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
				TraiterFloreDifferee(positionObservation, budgetFloreSecours);
				if (ActiverProfilagePerfMondeClient)
					PerfBudgetMonitor.End("MondeClient/FloreSecours", debutFloreSecoursUs);
			}

			GarantirRequetesChunksProcheJoueur(positionObservation, chunkObservationActuel);
		}
		else
		{
		// 2. Tâches de fond : dépiler l'affichage des nouveaux Chunks
		int actionsVisuelles = 0;
		int budgetVisuelDyn = enChargement ? 3 : MaxMeshesParFrameVisuelles;
		if (_niveauUrgencePerf >= 3) budgetVisuelDyn = 0;
		else if (_niveauUrgencePerf >= 2) budgetVisuelDyn = 1;
		else if (_niveauUrgencePerf == 1) budgetVisuelDyn = Mathf.Max(1, MaxMeshesParFrameVisuelles - 1);
		while (actionsVisuelles < budgetVisuelDyn && _misesAJourUrgentes.TryDequeue(out var urgente))
		{
			try { urgente.Invoke(); } catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
			actionsVisuelles++;
		}
		while (actionsVisuelles < budgetVisuelDyn && _misesAJourMainThread.TryDequeue(out var action))
		{
			try { action.Invoke(); } catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
			actionsVisuelles++;
		}

		// Position d'observation : déjà résolue en tête de frame (caméra / joueur).
		bool chunkObservationChange = chunkObservationActuel != _obsChunkDormance;
		_obsChunkDormance = chunkObservationActuel;
		AjusterFenetreRequetes(dt);
		float tFpsServices = ModeSurvieFpsAgressif
			? Mathf.Clamp((Mathf.Clamp(_fpsMoyenneAuto, 20f, 120f) - 45f) / 20f, 0f, 1f) // 45 FPS -> 0, 65 FPS -> 1
			: 1f;
		float intervalleServicesLointainsDyn = Mathf.Lerp(0.14f, 0.03f, tFpsServices);
		if (_niveauUrgencePerf >= 2) intervalleServicesLointainsDyn *= 1.30f;
		else if (_niveauUrgencePerf == 1) intervalleServicesLointainsDyn *= 1.12f;
		if (_timerFreinSpike > 0f) intervalleServicesLointainsDyn *= 1.30f;
		intervalleServicesLointainsDyn = Mathf.Clamp(intervalleServicesLointainsDyn, 0.025f, 0.26f);
		bool executerServicesLointains = _cooldownServicesLointains <= 0f;
		if (executerServicesLointains)
		{
			ulong debutHorizonUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
			MettreAJourHorizonLointain(positionObservation, dt);
			if (ActiverProfilagePerfMondeClient)
				PerfBudgetMonitor.End("MondeClient/Horizon", debutHorizonUs);

			ulong debutCullingUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
			AppliquerCullingCameraChunks(positionObservation, directionObservation, dt);
			if (ActiverOcclusionVisuelle)
				MettreAJourOcclusionVisuelle(positionObservation, directionObservation, dt);
			if (ActiverProfilagePerfMondeClient)
				PerfBudgetMonitor.End("MondeClient/Culling", debutCullingUs);

			_cooldownServicesLointains = intervalleServicesLointainsDyn;
		}
		if (chunkObservationChange) _timerDormance = 0f;
		_timerDormance -= dt;
		if (_timerDormance <= 0f)
		{
			ulong debutDormanceUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
			ActualiserDormanceChunks(_obsChunkDormance.X, _obsChunkDormance.Y, _maxTransitionsDormanceDyn);
			if (ActiverProfilagePerfMondeClient)
				PerfBudgetMonitor.End("MondeClient/Dormance", debutDormanceUs);
			float facteurDormance = _niveauUrgencePerf >= 2 ? 1.7f : (_niveauUrgencePerf == 1 ? 1.35f : 1f);
			if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 56f) facteurDormance *= 1.2f;
			_timerDormance = Mathf.Clamp(IntervalleDormanceSec * facteurDormance, 0.02f, 0.34f);
		}

		if (chunkObservationActuel != _ancienChunkJoueur || coordYJoueurChange)
		{
			if (chunkObservationActuel != _ancienChunkJoueur)
			{
				_ancienChunkJoueur = chunkObservationActuel;
				// Replanifie les flores des chunks proches : sinon l'herbe générée quand le joueur était loin reste vide/pauvre.
				ReplanifierFloreAutourJoueur(chunkObservationActuel);
			}
			float facteurPressionPerf = 1f;
			if (ModeSurvieFpsAgressif)
			{
				float fpsReference = Mathf.Clamp(_fpsMoyenneAuto, 20f, 120f);
				facteurPressionPerf = Mathf.Clamp(60f / fpsReference, 1f, 3.2f);
				if (urgencePerfExtreme)
					facteurPressionPerf = Mathf.Max(facteurPressionPerf, 4.5f);
			}
			float cooldownRadar = coordYJoueurChange && chunkObservationActuel == _ancienChunkJoueur
				? 0.03f
				: Mathf.Clamp(
					Mathf.Max(0.03f, IntervalleMinRebuildRadarSec)
					* (ModeSurvieFpsAgressif && backlogCharge >= SeuilBacklogBas ? 1.6f : 1f)
					* (prioriteJoueur ? 1.25f : 1f)
					* facteurPressionPerf,
					Mathf.Max(0.03f, IntervalleMinRebuildRadarSec),
					1.4f);
			DemanderRafraichissementRadar(positionObservation, cooldownRadar);
			_derniereDirectionRadar = directionObservation;
			_timerRafraichissementRadarImmobile = _intervalleRadarImmobileDyn;
		}
		else
		{
			// Reste immobile: continue de préparer la map par vagues.
			_timerRafraichissementRadarImmobile -= dt;
			float dot = _derniereDirectionRadar.Normalized().Dot(directionObservation.Normalized());
			bool rotationImportante = dot < 0.86f;
			if (rotationImportante && ActiverCullingCameraChunks && _framesBoostCullingRotationRestantes == 0)
			{
				ReinitialiserCullingVisibleDisqueObservation(positionObservation);
				_framesBoostCullingRotationRestantes = 12;
				_timerCullingCamera = 0f;
			}
			if (!urgencePerfExtreme && (_timerRafraichissementRadarImmobile <= 0f || rotationImportante) && !_radarEnCours)
			{
				float facteurPressionPerf = ModeSurvieFpsAgressif
					? Mathf.Clamp(60f / Mathf.Max(20f, _fpsMoyenneAuto), 1f, 3.2f)
					: 1f;
				float cooldownRadar = Mathf.Clamp(
					Mathf.Max(0.03f, IntervalleMinRebuildRadarSec)
					* (rotationImportante ? 0.7f : 1.2f)
					* facteurPressionPerf,
					Mathf.Max(0.03f, IntervalleMinRebuildRadarSec),
					1.4f);
				DemanderRafraichissementRadar(positionObservation, cooldownRadar);
				_derniereDirectionRadar = directionObservation;
				_timerRafraichissementRadarImmobile = rotationImportante
					? Mathf.Max(0.12f, _intervalleRadarImmobileDyn * 0.45f)
					: _intervalleRadarImmobileDyn;
			}
		}
		if (_rebuildRadarEnAttente && !_radarEnCours && _cooldownRebuildRadar <= 0f)
		{
			_rebuildRadarEnAttente = false;
			_cooldownRebuildRadar = Mathf.Max(0.03f, IntervalleMinRebuildRadarSec);
			// Même en urgence extrême : ne pas jeter le rebuild (sinon la file ne reprend jamais les trous / lointain).
			ActualiserVisibiliteEtTriChunks(_positionRadarEnAttente);
		}

		// Etale la flore sur plusieurs frames + garde-fou pour éviter une disparition visuelle prolongée.
		int budgetFlore = CalculerBudgetFloreDynamique(enChargement, prioriteJoueur);
		ulong debutFloreUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		TraiterFloreDifferee(positionObservation, budgetFlore);
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/Flore", debutFloreUs);

		// Priorité catch-up : suit RenderDistance en mode FPS bas (ex. R=3 ne doit pas charger 11 chunks).
		int rayonPriorite = ObtenirRayonPrioriteCatchUp();
		if (ModeSurvieFpsAgressif && urgencePerfExtreme)
			rayonPriorite = Mathf.Max(RayonGrilleMinSpawnPret + 1, rayonPriorite - 1);
		else if (ModeSurvieFpsAgressif && urgencePerfCritique)
			rayonPriorite = Mathf.Max(RayonGrilleMinSpawnPret + 1, rayonPriorite);
		_prioritaireSetTemp.Clear();
		void AjouterAnneauManquant(Vector2I centre, int rayonDemi)
		{
			for (int dx = -rayonDemi; dx <= rayonDemi; dx++)
				for (int dz = -rayonDemi; dz <= rayonDemi; dz++)
				{
					var v = new Vector2I(centre.X + dx, centre.Y + dz);
					if (!ChunkDisponiblePourObservation(v, positionObservation)) _prioritaireSetTemp.Add(v);
				}
		}
		AjouterAnneauManquant(chunkPieds, rayonPriorite);
		// Anticipation de trajectoire : même en urgence extrême on demande les chunks devant le joueur
		// (rayon réduit mais jamais nul) pour éviter les chutes quand le joueur avance vite.
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRefAnticipation) && SecondesAnticipationChargement > 0.01f)
		{
			Vector3 vel = joueurRefAnticipation.Velocity;
			Vector3 decalAnticipation = new Vector3(vel.X, 0f, vel.Z) * SecondesAnticipationChargement;
			if (decalAnticipation.LengthSquared() > 1f)
			{
				Vector3 posFutur = positionObservation + decalAnticipation;
				Vector2I chunkFutur = Gestionnaire_Monde.WorldToChunkCoord(posFutur, TailleChunk);
				int rayonAvant;
				if (ModeSurvieFpsAgressif && urgencePerfExtreme)
					rayonAvant = Mathf.Max(1, RayonGrilleMinSpawnPret);
				else if (ModeSurvieFpsAgressif && urgencePerfCritique)
					rayonAvant = Mathf.Max(2, RayonDormancePhysique);
				else
					rayonAvant = Mathf.Max(RayonDormancePhysique + 1, rayonPriorite - 1);
				AjouterAnneauManquant(chunkFutur, rayonAvant);
			}
		}
		if (_prioritaireSetTemp.Count > 0)
		{
			RetirerChunksDeLaFile(_prioritaireSetTemp);
			_prioritaireListTemp.Clear();
			_prioritaireListTemp.AddRange(_prioritaireSetTemp);
			_chunksACharger.AddRange(_prioritaireListTemp);
			TrierFileChunksAChargerParDistance(positionObservation);
		}
		}

		// 3. Requêtes : extraction radiale + purge obsolètes. Si le chunk sous les pieds n'est pas chargé, on demande plus de chunks (catch-up côté client).
		PurgerChunksObsolètesDeLaFile(positionObservation);
		bool chunkPiedsManquant = !ChunkDisponiblePourObservation(chunkPieds, positionObservation);
		int backlog = CompterBacklog();
		int rayonChargementCibleChunks = RayonChargementChunksActif();
		int nbRequetes = chunkPiedsManquant ? Mathf.Min(_maxRequetesDyn * 2, 20) : _maxRequetesDyn;
		// Pendant minage/remesh : réduire le débit mais ne pas couper le streaming (sinon trous aux frontières).
		if (_modificationEnCours)
			nbRequetes = Mathf.Max(chunkPiedsManquant ? 4 : 2, nbRequetes / 2);
		if (ModeSurvieFpsAgressif)
		{
			if (backlog >= SeuilBacklogHaut) nbRequetes = Mathf.Max(1, nbRequetes / 3);
			else if (backlog >= SeuilBacklogBas) nbRequetes = Mathf.Max(1, nbRequetes / 2);
		}
		// Les coupures d'urgence ne s'appliquent pas si le sol proche joueur n'est pas prêt (anti-chute dans le vide).
		// Sous grâce « Appliquer » graphismes : on ne force pas 1 requête/frame sinon la distance 30 chunks met une éternité.
		if (_timerGraceStreamingReglageUtilisateur <= 0f && !_streamingChunksPrioritaireCetteFrame)
		{
			if (ModeSurvieFpsAgressif && urgencePerfExtreme && !doitGarantirProcheJoueur)
				nbRequetes = chunkPiedsManquant ? 2 : 1;
			else if (ModeSurvieFpsAgressif && urgencePerfCritique && !doitGarantirProcheJoueur)
				nbRequetes = Mathf.Min(nbRequetes, chunkPiedsManquant ? 3 : 1);
			if (ModeSurvieFpsAgressif && frameChargeeStreaming && !doitGarantirProcheJoueur)
			{
				int gapFenetre = Mathf.Max(0, rayonChargementCibleChunks - _rayonRequetesActuel);
				if (gapFenetre >= SeuilGapRequetesMin)
					nbRequetes = Mathf.Min(Mathf.Max(nbRequetes, chunkPiedsManquant ? 3 : 2), 3);
				else
					nbRequetes = Mathf.Min(nbRequetes, chunkPiedsManquant ? 2 : 1);
			}
		}
		else if (_streamingChunksPrioritaireCetteFrame && ModeSurvieFpsAgressif)
		{
			nbRequetes = Mathf.Max(nbRequetes, chunkPiedsManquant ? 8 : 4);
		}
		if (corridorStreamingEnRetard && !VolCreatifStreamingReduit())
		{
			if (solSecuriseSousPieds)
				nbRequetes = Mathf.Max(nbRequetes, _fpsMoyenneAuto < 50f ? 1 : 2);
			else
				nbRequetes = Mathf.Max(nbRequetes, chunkPiedsManquant ? 10 : 6);
		}
		else if (corridorStreamingEnRetard && VolCreatifStreamingReduit())
			nbRequetes = Mathf.Min(nbRequetes, chunkPiedsManquant ? 2 : 1);
		if (VolCreatifStreamingReduit())
			nbRequetes = Mathf.Min(nbRequetes, chunkPiedsManquant ? 2 : 1);
		// Plancher dur : en situation de risque de chute / exploration souterraine, garantir au moins 4 requêtes par frame.
		if (doitGarantirProcheJoueur && !VolCreatifStreamingReduit())
			nbRequetes = Mathf.Max(nbRequetes, 4);
		// GATE FPS STRICT : aucune nouvelle requête hors zone critique si gelé, ramp-up ensuite.
		nbRequetes = AppliquerGateEtRampUp(nbRequetes, doitGarantirProcheJoueur, 1);
		int rayonDetailStreaming = rayonChargementCibleChunks;
		// Hors « Sauver les FPS » : la fenêtre de requête réseau suit la distance du panneau (évite un plafond implicite ~RayonInitialRequetesChunks).
		int rayonFenetreDemandeChunks = ModeSurvieFpsAgressif ? _rayonRequetesActuel : rayonChargementCibleChunks;
		float rayonMaxCarreDemande = (rayonFenetreDemandeChunks + 1f) * (rayonFenetreDemandeChunks + 1f);
		// Sous gel, AppliquerGateEtRampUp renvoie 0 : plus aucun chunk distant ne part → halo minuscule malgré RenderDistance.
		// Garde-fou analogue au budget flore : 1 requête/frame si la fenêtre est en retard et la file n'est pas vide.
		if (nbRequetes <= 0 && !doitGarantirProcheJoueur && _chunksACharger.Count > 0 && ActiverGateFpsStrict
			&& _timerGraceStreamingReglageUtilisateur <= 0f)
		{
			int gapAntiVide = Mathf.Max(0, rayonDetailStreaming - _rayonRequetesActuel);
			// Sous gel : déjà couvert par gap ; hors gel (ex. rampe post-dégel à 0) : garder un flux si la file attend encore.
			bool besoinFlux = _gateStreamingGele
				? gapAntiVide >= 1 || backlog >= 1
				: gapAntiVide > 0 || backlog >= SeuilBacklogBas;
			if (besoinFlux)
				nbRequetes = 1;
		}
		int requetesEmises = 0;
		int minRequetesForcees = doitGarantirProcheJoueur ? 3 : 0;
		for (int n = 0; n < nbRequetes && _chunksACharger.Count > 0; n++)
		{
			// On ne coupe sur le budget frame QUE si on a déjà émis les requêtes minimales de sécurité.
			if (requetesEmises >= minRequetesForcees && BudgetFrameDepasse())
				break;
			Vector2I chunkCible = ExtraireChunkLePlusProche(_chunksACharger, positionObservation, directionObservation);
			float distCarree = DistanceCarreeAuJoueur(chunkCible, positionObservation);
			if (distCarree > rayonMaxCarreDemande)
			{
				// CRITIQUE : ExtraireChunkLePlusProche retire déjà l’entrée ; sans replacer, on « perd » le chunk.
				_chunksACharger.Add(chunkCible);
				if (ModeSurvieFpsAgressif)
				{
					// Si le plus proche en file est encore hors fenêtre, agrandir la fenêtre jusqu’à l’inclure (sinon on
					// brûle nbRequetes itérations sans jamais appeler DemanderChunk → monde bloqué à quelques chunks malgré R=64).
					int minRayonPourChunk = Mathf.Max(0, Mathf.CeilToInt(Mathf.Sqrt(distCarree)) - 1);
					int minRayon = ObtenirRayonMinRequetesReseau();
					if (minRayonPourChunk > _rayonRequetesActuel)
					{
						int plafond = Mathf.Max(minRayon, rayonDetailStreaming);
						_rayonRequetesActuel = Mathf.Clamp(minRayonPourChunk, minRayon, plafond);
					}
					continue;
				}
				// Hors survie : la file est censée respecter RenderDistance ; si un voisin dépasse quand même, on envoie pour éviter une boucle morte.
				DemanderChunk(chunkCible);
				requetesEmises++;
				continue;
			}
			DemanderChunk(chunkCible);
			requetesEmises++;
		}

		_tempsDepuisNettoyage += dt;
		float intervalleNettoyage = backlogCharge > SeuilBacklogHaut
			? IntervalleNettoyageChunks * 0.5f
			: IntervalleNettoyageChunks;
		if (_tempsDepuisNettoyage >= intervalleNettoyage)
		{
			_tempsDepuisNettoyage = 0f;
			NettoyerChunksObsoles(positionObservation);
		}
		if (ActiverProfilagePerfMondeClient)
		{
			PerfBudgetMonitor.End("MondeClient/Frame", debutFramePerfUs);
			if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageSec))
			{
				_cooldownDrainProfilage = 0f;
				PerfBudgetMonitor.FlushSiEchu("MondeClient", IntervalleLogProfilageSec);
			}
		}
	}






}
