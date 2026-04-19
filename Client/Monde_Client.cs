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
	[Export] public int RenderDistance = 200;
	[Export] public int RenderDistanceDetailChunks = 15;
	[Export] public int RayonQualiteMaxChunks = 7;
	[Export] public int RayonGazonVisibleChunks = 12;
	[Export] public int RayonBuissonsVisibleChunks = 24;
	[Export] public bool ProfilLodCinematiqueUltraSmooth = true;
	[Export] public int LODTextureEtapes = 12;
	[Export] public int MaxChunksParFrame = 9;
	/// <summary>Nombre d'entrées inspectées max pour choisir un job maths (évite un scan O(n) complet à chaque worker).</summary>
	[Export] public int FenetreSelectionTravailMaths = 56;
	[Export] public int RayonInitialRequetesChunks = 10;
	[Export] public float IntervalleExpansionRequetesSec = 0.30f;
	[Export] public int SeuilBacklogHaut = 36;
	[Export] public int SeuilBacklogBas = 12;
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
	[Export] public int MaxBasculesCullingParPasse = 96;
	[Export] public int MaxChunksEvaluesCullingParPasse = 240;
	[Export] public float IntervalleDormanceSec = 0.06f;
	/// <summary>Rayon (en chunks) autour du joueur où les collisions sont actives. Tout dans ce rayon doit être dynamique (réveil immédiat). Au-delà, physique en dormance. 5 chunks ≈ 80 m (évite trous de collision en bordure, allège énormément Jolt : 121 chunks × 45 sections = 5445 shapes au lieu de 13005 à R=8).</summary>
	[Export] public int RayonDormancePhysique = 5;
	/// <summary>Demi-côté (chunks) pour lever l’overlay « Chargement du monde » : 2 = grille 5×5. Ne pas exiger tout le rayon de dormance (17×17) au démarrage sinon chargement quasi infini.</summary>
	[Export] public int RayonGrilleMinSpawnPret = 2;
	/// <summary>Chunks demandés en plus du rayon physique (file prioritaire). Le sol doit être chargé avant que tu n’entres dans la grille ChunkSousPiedsAPret.</summary>
	[Export] public int MargePreloadChunks = 6;
	/// <summary>Anticipation du déplacement (s) : une 2ᵉ zone de priorité autour de la position future pour marches longues dans une direction.</summary>
	[Export] public float SecondesAnticipationChargement = 3.0f;
	[Export] public float IntervalleRafraichissementRadarImmobile = 0.45f;
	/// <summary>Intégrations mesh/collision par frame quand le spawn est déjà prêt (exploration). Augmente si le sol met du temps à « se réveiller ».</summary>
	[Export] public int MaxIntegrationsParFrameExploration = 3;
	/// <summary>Intégrations mesh/collision par frame pendant le chargement initial (anti-pic CPU/GPU).</summary>
	[Export] public int MaxIntegrationsParFrameChargement = 5;
	/// <summary>Budget de vertices intégrés par frame (exploration). Lisse l'arrivée des triangles.</summary>
	[Export] public int BudgetVerticesIntegrationParFrameExploration = 65000;
	/// <summary>Budget de vertices intégrés par frame au chargement initial (plus généreux).</summary>
	[Export] public int BudgetVerticesIntegrationParFrameChargement = 100000;
	/// <summary>Solidifications BodySetSpace par frame en exploration (hors chargement initial).</summary>
	[Export] public int MaxSolidificationsParFrameExploration = 6;
	/// <summary>Budget minimal de solidifications quand le joueur se déplace vite (anti-traversée du sol).</summary>
	[Export] public int MaxSolidificationsPrioriteJoueur = 9;
	/// <summary>Nombre max d'entrées inspectées pour choisir un chunk à solidifier (évite un scan complet de la file à chaque tick).</summary>
	[Export] public int FenetreSelectionSolidification = 64;
	/// <summary>Rayon (chunks) à réveiller en urgence autour de la position courante / anticipée du joueur.</summary>
	[Export] public int RayonPrioriteCollisionJoueur = 2;
	/// <summary>Anticipation (secondes) pour pré-réveiller les collisions devant le joueur.</summary>
	[Export] public float SecondesAnticipationCollision = 0.85f;
	/// <summary>Nombre max de chunks de flore (gazon/buissons) construits par frame en exploration.</summary>
	[Export] public int MaxFloreParFrameExploration = 6;
	/// <summary>Nombre max de chunks de flore construits par frame pendant le chargement initial.</summary>
	[Export] public int MaxFloreParFrameChargement = 4;
	[ExportGroup("Performance streaming")]
	[Export] public bool ModeSurvieFpsAgressif = true;
	[Export(PropertyHint.Range, "20,59,1")] public int SeuilFpsUrgenceForte = 42;
	[Export(PropertyHint.Range, "15,45,1")] public int SeuilFpsUrgenceCritique = 30;
	[Export(PropertyHint.Range, "10,35,1")] public int SeuilFpsUrgenceExtreme = 24;
	[Export(PropertyHint.Range, "40,90,1")] public int SeuilFpsSortieUrgenceExtreme = 56;

	// =========================================================================
	// GATE FPS STRICT : gèle tout nouveau chargement tant que FPS < seuil.
	// Pendant le gel, SEULE la zone ultra-proche joueur continue (anti-chute).
	// Ramp-up 1-par-1 à la reprise (un élément/frame au début, puis augmente).
	// =========================================================================
	[Export] public bool ActiverGateFpsStrict = true;
	/// <summary>FPS en dessous duquel on gèle tout nouveau chargement non-critique.</summary>
	[Export(PropertyHint.Range, "30,70,1")] public float SeuilFpsGateStrict = 50f;
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

	private bool _gateStreamingGele = false;
	private float _tempsFpsStableHaut = 0f;
	private float _tempsDepuisDegel = 99f;
	private float _tempsEtatGate = 99f;
	[Export(PropertyHint.Range, "8,25,0.1")] public float BudgetFrameCibleMs = 16.2f;
	[Export(PropertyHint.Range, "0.1,4,0.1")] public float MargeBudgetUrgenceMs = 1.0f;
	[Export(PropertyHint.Range, "0.05,0.8,0.01")] public float IntervalleServicesLointainsUrgenceSec = 0.22f;
	[Export(PropertyHint.Range, "8,512,1")] public int FenetreSelectionRequetes = 96;
	[Export(PropertyHint.Range, "1,8,1")] public int MaxLancementsTravailleursParTick = 2;
	[Export(PropertyHint.Range, "0.01,0.5,0.01")] public float IntervalleMinRebuildRadarSec = 0.10f;
	[Export(PropertyHint.Range, "0,128,1")] public int SeuilBacklogBootstrapStable = 6;
	[Export] public bool ExigerSolidificationVidePourBootstrap = false;
	[ExportGroup("Diagnostic performance")]
	[Export] public bool ActiverProfilagePerfMondeClient = false;
	[Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleLogProfilageSec = 2.0f;

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
	private readonly HashSet<Vector2I> _chunksUniquesTemp = new HashSet<Vector2I>();
	private readonly List<Vector2I> _chunksATuerTemp = new List<Vector2I>();
	private bool _radarEnCours;
	private HashSet<(int cx, int cz, int section)> _sectionsAReconstruire = new HashSet<(int, int, int)>();
	private CharacterBody3D _joueur;
	private Vector2I _ancienChunkJoueur = new Vector2I(-99999, -99999);
	private bool _modificationEnCours;
	private MeshInstance3D _horizonLodMesh;
	private static StandardMaterial3D _cacheMatHorizon;
	private Vector2I _centreHorizonCell = new Vector2I(int.MinValue, int.MinValue);
	private float _timerMajHorizon = 0f;
	private float _timerCullingCamera = 0f;
	private readonly List<Vector2I> _fileFloreDifferee = new List<Vector2I>();
	private readonly HashSet<Vector2I> _setFloreDifferee = new HashSet<Vector2I>();
	private readonly Dictionary<Vector2I, ulong> _frameEnqueueFlore = new Dictionary<Vector2I, ulong>();
	private int _rayonRequetesActuel;
	private float _timerExpansionRequetes;
	private float _timerProgressionForceeRayon = 0f;
	private float _timerRafraichissementRadarImmobile = 0f;
	private Vector3 _derniereDirectionRadar = Vector3.Forward;
	private const int EpaisseurAnneauRadar = 3;
	[Export] public int MaxAjoutsRadarParPasse = 520;
	private float _fpsMoyenneAuto = 60f;
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

	private Camera3D ObtenirCameraObservation()
	{
		ulong frame = Engine.GetProcessFrames();
		if (_frameCameraObservationCache == frame)
			return _cameraObservationCache;
		_frameCameraObservationCache = frame;
		Viewport viewport = GetViewport();
		Camera3D camera = viewport?.GetCamera3D();
		if (camera != null && GodotObject.IsInstanceValid(camera))
		{
			_cameraObservationCache = camera;
			return _cameraObservationCache;
		}
		_cameraObservationCache = null;
		return null;
	}

	// Références vers l'UI
	private Panel _slotGauche;
	private Panel _slotDroite;

	public override void _Ready()
	{
		// Assure-toi que les chemins correspondent à ton arborescence exacte
		_slotGauche = GetNode<Panel>("../HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Gauche");
		_slotDroite = GetNode<Panel>("../HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Droite");
		InitialiserHorizonLointain();
	}

	private int RayonDetailChunksActif()
	{
		int max = Mathf.Max(6, RenderDistance);
		int detail = Mathf.Clamp(RenderDistanceDetailChunks, 6, max);
		return Mathf.Min(detail, max);
	}

	/// <summary>Rayon de chargement réseau/terrain réel. Indépendant du rayon de détail visuel.</summary>
	private int RayonChargementChunksActif()
	{
		int rendu = Mathf.Max(RayonDormancePhysique + 1, RenderDistance);
		return Mathf.Max(rendu, RayonDetailChunksActif());
	}

	/// <summary>Rayon radar préparé à l'avance: progresse avec la fenêtre de requêtes pour éviter les pics CPU à très grande distance.</summary>
	private int RayonRadarPreparationActif()
	{
		int minRadar = Mathf.Max(RayonDormancePhysique + 2, RayonInitialRequetesChunks);
		int cible = RayonChargementChunksActif();
		// Hors mode « Sauver les FPS », pas de fenêtre progressive imposée par le moteur : alignement direct sur la distance utilisateur.
		if (!ModeSurvieFpsAgressif)
			return Mathf.Max(minRadar, cible);
		// Réglage manuel validé : on ne réduit plus la cible du radar à ~dormance+3 sous urgence (sinon 30 chunks demandés ne chargent jamais).
		if (_timerGraceStreamingReglageUtilisateur > 0f)
		{
			int margeManuel = 10;
			int progressifManuel = Mathf.Max(minRadar, _rayonRequetesActuel + margeManuel);
			return Mathf.Clamp(progressifManuel, minRadar, cible);
		}
		int margeProgressive = _niveauUrgencePerf >= 3 ? 1 : (_niveauUrgencePerf >= 2 ? 2 : (_niveauUrgencePerf == 1 ? 4 : 6));
		int progressif = Mathf.Max(minRadar, _rayonRequetesActuel + margeProgressive);
		if (_niveauUrgencePerf >= 3)
			cible = Mathf.Min(cible, Mathf.Max(minRadar, RayonDormancePhysique + 3));
		else if (_niveauUrgencePerf >= 2)
			cible = Mathf.Min(cible, Mathf.Max(minRadar, RayonDormancePhysique + 4));
		else if (_niveauUrgencePerf == 1)
			cible = Mathf.Min(cible, Mathf.Max(minRadar, RayonDormancePhysique + 7));
		return Mathf.Clamp(progressif, minRadar, cible);
	}

	private void AppliquerParametresLodTextureTerrain()
	{
		if (!(MaterielTerrain is ShaderMaterial sm)) return;
		float detailMetres = RayonDetailChunksActif() * TailleChunk;
		float start = Mathf.Max(240f, Mathf.Max(15f, RayonDetailChunksActif() + 4f) * TailleChunk);
		if (ProfilLodCinematiqueUltraSmooth) start += TailleChunk * 4f;
		float end = start + Mathf.Max(1800f, Mathf.Max(RenderDistance, RayonHorizonChunks) * TailleChunk * 8f);
		float mip = ProfilLodCinematiqueUltraSmooth ? 2.8f : 3.4f;
		float blend = ProfilLodCinematiqueUltraSmooth ? 0.92f : 0.82f;
		float jitter = ProfilLodCinematiqueUltraSmooth ? 84f : 56f;
		float steps = Mathf.Clamp(LODTextureEtapes, 8, 24);

		sm.SetShaderParameter("lod_texture_start", start);
		sm.SetShaderParameter("lod_texture_end", end);
		sm.SetShaderParameter("lod_far_mip", mip);
		sm.SetShaderParameter("lod_steps", steps);
		sm.SetShaderParameter("lod_step_blend", blend);
		sm.SetShaderParameter("lod_start_jitter", jitter);
		_ = detailMetres; // garde explicite la base physique (chunks->mètres) pour future extension.
	}

	public void ReappliquerReglagesGraphiquesRuntime()
	{
		Chunk_Client.RayonQualiteMaxChunks = Mathf.Max(1, RayonQualiteMaxChunks);
		Chunk_Client.RayonVisibiliteGazonChunks = Mathf.Max(1, RayonGazonVisibleChunks);
		Chunk_Client.RayonVisibiliteBuissonsChunks = Mathf.Max(2, RayonBuissonsVisibleChunks);
		AppliquerParametresLodTextureTerrain();
		if (ActiverHorizonLod)
		{
			// InitialiserHorizonLointain() ne fait rien si le mesh existe déjà : sans recréation, PasHorizon / RayonHorizon ne changent jamais en jeu.
			if (_horizonLodMesh != null && GodotObject.IsInstanceValid(_horizonLodMesh))
			{
				if (_horizonLodMesh.IsInsideTree())
					RemoveChild(_horizonLodMesh);
				_horizonLodMesh.QueueFree();
				_horizonLodMesh = null;
			}
			_centreHorizonCell = new Vector2I(int.MinValue, int.MinValue);
			_timerMajHorizon = 0f;
			Callable.From(InitialiserHorizonLointain).CallDeferred();
		}
		else if (_horizonLodMesh != null && GodotObject.IsInstanceValid(_horizonLodMesh))
		{
			_horizonLodMesh.QueueFree();
			_horizonLodMesh = null;
			_centreHorizonCell = new Vector2I(int.MinValue, int.MinValue);
		}

		Vector2I centre = ObtenirCoordonneesChunkJoueur();
		ReplanifierFloreAutourJoueur(centre);

		// Si le culling est désactivé, il faut forcer la visibilité des chunks déjà cachés.
		if (!ActiverCullingCameraChunks)
		{
			foreach (var kv in _chunksData)
			{
				ChunkData data = kv.Value;
				if (data == null) continue;
				data.CullingVisible = true;
				if (data.VisualInstanceRID.IsValid)
					RenderingServer.Singleton.InstanceSetVisible(data.VisualInstanceRID, true);
				if (data.WaterInstanceRID.IsValid)
					RenderingServer.Singleton.InstanceSetVisible(data.WaterInstanceRID, true);
				if (data._nodeFlore is Node3D flore)
					flore.Visible = true;
			}
		}
		else
		{
			// Réévalue vite la visibilité après changement d'angle/marge.
			_timerCullingCamera = 0f;
		}
	}

	public void ForcerModeStreamingUtilisateur(bool activerProtectionsFps)
	{
		ModeAutoDiagnosticAdaptatif = activerProtectionsFps;
		ActiverGateFpsStrict = activerProtectionsFps;
		ActiverAntiSpikeFrameTime = activerProtectionsFps;
		if (!activerProtectionsFps)
		{
			_ratioChargeAuto = 1f;
			_facteurMouvementAuto = 1f;
			_niveauUrgencePerf = 0;
			_timerFreinSpike = 0f;
			_gateStreamingGele = false;
			_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
			_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
			int minRayon = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
			int cible = Mathf.Max(minRayon, RayonChargementChunksActif());
			_rayonRequetesActuel = Mathf.Clamp(Mathf.Max(_rayonRequetesActuel, cible - 1), minRayon, cible);
		}

		Vector3 posObs = ObtenirPositionObservation();
		_cooldownRebuildRadar = 0f;
		_rebuildRadarEnAttente = false;
		if (!_radarEnCours)
			ActualiserVisibiliteEtTriChunks(posObs);
	}

	/// <summary>À appeler quand le joueur valide explicitement les graphismes (bouton Appliquer) : laisse converger vers RenderDistance sans plafonds d’urgence immédiats.</summary>
	public void SignalerGraceStreamingApresReglageManuel()
	{
		_timerGraceStreamingReglageUtilisateur = DureeGraceStreamingReglageUtilisateurSec;
		_gateStreamingGele = false;
		_tempsFpsStableHaut = 0f;
		_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
		_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
	}

	public void ForcerRafraichissementStreamingGraphique(bool microReload)
	{
		Vector3 positionObservation = ObtenirPositionObservation();
		Camera3D cameraActive = ObtenirCameraObservation();
		Vector3 directionObservation = cameraActive != null
			? (-cameraActive.GlobalTransform.Basis.Z).Normalized()
			: (_joueur != null ? (-_joueur.GlobalTransform.Basis.Z).Normalized() : Vector3.Forward);

		int minRayon = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		int cible = Mathf.Max(minRayon, RayonChargementChunksActif());
		_rayonRequetesActuel = Mathf.Clamp(Mathf.Max(_rayonRequetesActuel, cible - 1), minRayon, cible);
		_timerExpansionRequetes = 0f;
		_timerProgressionForceeRayon = 0f;
		_timerRafraichissementRadarImmobile = 0f;
		_tempsDepuisNettoyage = IntervalleNettoyageChunks;
		_cooldownRebuildRadar = 0f;

		if (microReload)
			NettoyerChunksObsoles(positionObservation, directionObservation);
		else
			PurgerChunksObsolètesDeLaFile(positionObservation);

		_rebuildRadarEnAttente = false;
		if (!_radarEnCours)
			ActualiserVisibiliteEtTriChunks(positionObservation);
		else
			DemanderRafraichissementRadar(positionObservation, 0.01f);
	}

	public float LireFpsMoyenAutoDiagnostic() => _fpsMoyenneAuto;

	public int LireNiveauUrgencePerformance() => _niveauUrgencePerf;

	public void Initialiser(CharacterBody3D joueur, int seed, Action<Vector2I> enregistrerDemandeChunk,
		Action<Vector3, float, float> demanderDestruction, Action<Vector3, Vector3, float, int> demanderCreation)
	{
		_joueur = joueur;
		_seedTerrain = seed;
		_enregistrerDemandeChunk = enregistrerDemandeChunk;
		_demanderDestruction = demanderDestruction;
		_demanderCreation = demanderCreation;
		Chunk_Client.RayonQualiteMaxChunks = Mathf.Max(1, RayonQualiteMaxChunks);
		Chunk_Client.RayonVisibiliteGazonChunks = Mathf.Max(1, RayonGazonVisibleChunks);
		Chunk_Client.RayonVisibiliteBuissonsChunks = Mathf.Max(2, RayonBuissonsVisibleChunks);
		_rayonRequetesActuel = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		_timerExpansionRequetes = IntervalleExpansionRequetesSec;
		_timerProgressionForceeRayon = IntervalleProgressionForceeRayonSec;
		_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile;
		AppliquerParametresLodTextureTerrain();
	}

	/// <summary>
	/// Applique le gate FPS et le ramp-up post-dégel sur un budget de streaming.
	/// - Si la zone est critique (doitGarantirProcheJoueur=true), retourne budgetActuel inchangé (anti-chute).
	/// - Sinon, si gelé (FPS &lt; seuil), retourne 0 (arrêt net du streaming non-critique).
	/// - Sinon pendant le ramp-up, interpole de 1 vers budgetActuel sur DureeRampUpPostDegel secondes.
	/// </summary>
	private int AppliquerGateEtRampUp(int budgetActuel, bool doitGarantirProcheJoueur, int minSortieGel = 1)
	{
		if (doitGarantirProcheJoueur) return budgetActuel;
		// Grâce post-panneau graphismes : sinon le gate peut bloquer tout (0 requête chunk) malgré un RenderDistance élevé.
		if (_timerGraceStreamingReglageUtilisateur > 0f) return budgetActuel;
		if (!ActiverGateFpsStrict) return budgetActuel;
		if (_gateStreamingGele) return 0;
		if (_tempsDepuisDegel < DureeRampUpPostDegel)
		{
			float t = Mathf.Clamp(_tempsDepuisDegel / Mathf.Max(0.01f, DureeRampUpPostDegel), 0f, 1f);
			int plafond = Mathf.Max(minSortieGel, Mathf.RoundToInt(Mathf.Lerp(minSortieGel, Mathf.Max(minSortieGel, budgetActuel), t)));
			return Mathf.Min(budgetActuel, plafond);
		}
		return budgetActuel;
	}

	/// <summary>
	/// Budget flore dynamique avec garde-fou visuel:
	/// - applique urgence + gate/ramp-up;
	/// - évite un budget 0 prolongé en déplacement (herbe/buissons invisibles).
	/// </summary>
	private int CalculerBudgetFloreDynamique(bool enChargement, bool prioriteJoueur)
	{
		int budgetFlore = enChargement
			? Mathf.Max(1, MaxFloreParFrameChargement)
			: Mathf.Max(1, MaxFloreParFrameExploration);

		if (!enChargement && _niveauUrgencePerf >= 2)
			budgetFlore = Mathf.Max(1, Mathf.Min(budgetFlore, 1));
		else if (!enChargement && _niveauUrgencePerf == 1)
			budgetFlore = Mathf.Max(1, Mathf.Min(budgetFlore, 2));

		if (!enChargement)
			budgetFlore = AppliquerGateEtRampUp(budgetFlore, false, 1);

		// Même sous gate sévère, garder un flux minimal quand on se déplace et que la file n'est pas vide.
		if (!enChargement && budgetFlore <= 0 && _fileFloreDifferee.Count > 0 && (prioriteJoueur || _joueur != null))
			budgetFlore = 1;

		return Mathf.Max(0, budgetFlore);
	}

	private void MettreAJourAutoDiagnostic(float dt)
	{
		if (_timerFreinSpike > 0f)
			_timerFreinSpike = Mathf.Max(0f, _timerFreinSpike - dt);
		if (ActiverAntiSpikeFrameTime)
		{
			float frameMs = dt * 1000f;
			float seuilSpike = Mathf.Clamp(SeuilSpikeFrameMs, 14f, 45f);
			if (frameMs >= seuilSpike)
				_timerFreinSpike = Mathf.Max(_timerFreinSpike, Mathf.Clamp(DureeFreinSpikeSec, 0.08f, 1.2f));
		}

		if (!ModeAutoDiagnosticAdaptatif)
		{
			_ratioChargeAuto = 1f;
			_facteurMouvementAuto = 1f;
			_niveauUrgencePerf = 0;
			_maxAjoutsRadarParPasseDyn = MaxAjoutsRadarParPasse;
			_maxRequetesDyn = Mathf.Max(1, MaxChunksParFrame);
			_maxTravailleursDyn = Mathf.Clamp(MaxTravailleursCalcul, 2, 16);
			_maxTransitionsDormanceDyn = 64;
			_intervalleCullingDyn = 0.03f;
			_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile;
			_maxBasculesCullingDyn = Mathf.Max(8, MaxBasculesCullingParPasse);
			if (_timerFreinSpike > 0f)
			{
				_maxAjoutsRadarParPasseDyn = Mathf.Max(140, Mathf.RoundToInt(_maxAjoutsRadarParPasseDyn * 0.72f));
				_maxRequetesDyn = Mathf.Max(2, Mathf.RoundToInt(_maxRequetesDyn * 0.65f));
				_maxTransitionsDormanceDyn = Mathf.Max(10, Mathf.RoundToInt(_maxTransitionsDormanceDyn * 0.62f));
				_maxBasculesCullingDyn = Mathf.Max(8, Mathf.RoundToInt(_maxBasculesCullingDyn * 0.52f));
				_intervalleCullingDyn *= 1.35f;
			}
			return;
		}

		float fps = (float)Engine.GetFramesPerSecond();
		if (fps > 1f)
		{
			float alpha = Mathf.Clamp(dt * 2.0f, 0.04f, 0.22f);
			_fpsMoyenneAuto = Mathf.Lerp(_fpsMoyenneAuto, fps, alpha);
		}

		// === Gate FPS strict : gèle tout nouveau chargement tant que FPS < 60 (hystérésis). ===
		// Pendant la grâce « Appliquer » graphismes, on n’entre pas ici : le else force dégel (sinon aucun chunk ne part).
		if (ActiverGateFpsStrict && _timerGraceStreamingReglageUtilisateur <= 0f)
		{
			float fpsInstant = fps > 1f ? fps : _fpsMoyenneAuto;
			// Utilise à la fois FPS instantané et moyen pour une réaction rapide sans bruit.
			float fpsSignal = Mathf.Min(fpsInstant, _fpsMoyenneAuto);
			_tempsEtatGate += dt;
			if (!_gateStreamingGele)
			{
				if (fpsSignal < SeuilFpsGateStrict && _tempsEtatGate >= Mathf.Max(0.05f, DureeMinEtatOuvertSec))
				{
					_gateStreamingGele = true;
					_tempsFpsStableHaut = 0f;
					_tempsDepuisDegel = 0f;
					_tempsEtatGate = 0f;
				}
			}
			else
			{
				if (fpsSignal >= SeuilFpsGateReprise)
					_tempsFpsStableHaut += dt;
				else
					_tempsFpsStableHaut = 0f;
				if (_tempsFpsStableHaut >= DureeStabiliteReprise && _tempsEtatGate >= Mathf.Max(0.05f, DureeMinEtatGeleSec))
				{
					_gateStreamingGele = false;
					_tempsDepuisDegel = 0f;
					_tempsEtatGate = 0f;
				}
			}
			if (!_gateStreamingGele)
				_tempsDepuisDegel = Mathf.Min(_tempsDepuisDegel + dt, DureeRampUpPostDegel + 1f);
		}
		else
		{
			_gateStreamingGele = false;
			_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
			_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
		}

		float cible = Mathf.Clamp(FpsCibleAutoDiagnostic, 45, 240);
		float ratio = Mathf.Clamp(_fpsMoyenneAuto / cible, RatioChargeMinimumAuto, 1.15f);
		if (_fpsMoyenneAuto < 22f) ratio *= 0.35f;
		else if (_fpsMoyenneAuto < 30f) ratio *= 0.45f;
		else if (_fpsMoyenneAuto < 45f) ratio *= 0.60f;
		else if (_fpsMoyenneAuto < 55f) ratio *= 0.75f;
		else if (_fpsMoyenneAuto < 70f) ratio *= 0.88f;
		_ratioChargeAuto = Mathf.Clamp(ratio, RatioChargeMinimumAuto, 1.1f);
		int seuilForte = Mathf.Clamp(SeuilFpsUrgenceForte, 20, 59);
		int seuilCritique = Mathf.Clamp(SeuilFpsUrgenceCritique, 15, seuilForte - 1);
		int seuilExtreme = Mathf.Clamp(SeuilFpsUrgenceExtreme, 10, seuilCritique);
		int seuilSortieExtreme = Mathf.Clamp(SeuilFpsSortieUrgenceExtreme, seuilForte, 90);
		if (!ModeSurvieFpsAgressif)
			_niveauUrgencePerf = 0;
		else
		{
			// Hystérésis anti-pompage: une fois en mode extrême, on n'en sort qu'au-dessus d'un seuil plus haut.
			if (_niveauUrgencePerf >= 3)
			{
				if (_fpsMoyenneAuto >= seuilSortieExtreme) _niveauUrgencePerf = 1;
				else _niveauUrgencePerf = 3;
			}
			else if (_fpsMoyenneAuto <= seuilExtreme)
				_niveauUrgencePerf = 3;
			else if (_fpsMoyenneAuto <= seuilCritique)
				_niveauUrgencePerf = 2;
			else if (_fpsMoyenneAuto <= seuilForte)
				_niveauUrgencePerf = 1;
			else
				_niveauUrgencePerf = 0;
		}

		float vitesseXZ = 0f;
		if (_joueur != null)
		{
			Vector3 vel = _joueur.Velocity;
			vitesseXZ = Mathf.Sqrt(vel.X * vel.X + vel.Z * vel.Z);
		}
		float tMouvement = Mathf.Clamp((vitesseXZ - 0.6f) / 5.0f, 0f, 1f);
		_facteurMouvementAuto = Mathf.Lerp(1f, 0.54f, tMouvement);
		float ratioStable = Mathf.Clamp(_ratioChargeAuto * _facteurMouvementAuto, RatioChargeMinimumAuto, 1.05f);
		if (_timerFreinSpike > 0f)
			ratioStable = Mathf.Clamp(ratioStable * 0.64f, RatioChargeMinimumAuto, 1.05f);

		int cpuCount = Math.Max(1, System.Environment.ProcessorCount);
		_maxAjoutsRadarParPasseDyn = Mathf.Clamp(Mathf.RoundToInt(MaxAjoutsRadarParPasse * ratioStable), 24, MaxAjoutsRadarParPasse);
		_maxRequetesDyn = Mathf.Clamp(Mathf.RoundToInt(MaxChunksParFrame * Mathf.Lerp(0.35f, 1.20f, ratioStable)), 1, 56);
		_maxTravailleursDyn = Mathf.Clamp(
			Mathf.RoundToInt(MaxTravailleursCalcul * Mathf.Lerp(0.30f, 1.05f, ratioStable)),
			1,
			Mathf.Clamp(cpuCount - 1, 1, 12));
		_maxTransitionsDormanceDyn = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(6f, 96f, ratioStable)), 4, 120);
		_intervalleCullingDyn = Mathf.Lerp(0.14f, 0.02f, ratioStable);
		_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile * Mathf.Lerp(2.4f, 0.82f, ratioStable);
		_maxBasculesCullingDyn = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(18f, Mathf.Max(18, MaxBasculesCullingParPasse), ratioStable)), 12, Mathf.Max(12, MaxBasculesCullingParPasse));
		if (_niveauUrgencePerf >= 3)
		{
			_maxAjoutsRadarParPasseDyn = Mathf.Min(_maxAjoutsRadarParPasseDyn, 18);
			_maxRequetesDyn = Mathf.Min(_maxRequetesDyn, 1);
			_maxTravailleursDyn = 1;
			_maxTransitionsDormanceDyn = Mathf.Min(_maxTransitionsDormanceDyn, 6);
			_maxBasculesCullingDyn = Mathf.Min(_maxBasculesCullingDyn, 10);
			_intervalleCullingDyn = Mathf.Max(_intervalleCullingDyn, 0.18f);
			_intervalleRadarImmobileDyn = Mathf.Max(_intervalleRadarImmobileDyn, IntervalleRafraichissementRadarImmobile * 3.2f);
		}
		else if (_fpsMoyenneAuto < 30f)
		{
			_maxAjoutsRadarParPasseDyn = Mathf.Min(_maxAjoutsRadarParPasseDyn, 42);
			_maxRequetesDyn = Mathf.Min(_maxRequetesDyn, 2);
			_maxTravailleursDyn = 1;
			_maxTransitionsDormanceDyn = Mathf.Min(_maxTransitionsDormanceDyn, 10);
			_maxBasculesCullingDyn = Mathf.Min(_maxBasculesCullingDyn, 14);
			_intervalleCullingDyn = Mathf.Max(_intervalleCullingDyn, 0.14f);
			_intervalleRadarImmobileDyn = Mathf.Max(_intervalleRadarImmobileDyn, IntervalleRafraichissementRadarImmobile * 2.4f);
		}
		if (_timerFreinSpike > 0f)
		{
			_maxBasculesCullingDyn = Mathf.Max(8, Mathf.RoundToInt(_maxBasculesCullingDyn * 0.55f));
			_intervalleCullingDyn *= 1.25f;
		}
	}

	public void EnqueueMiseAJourMainThread(Action action) => _misesAJourMainThread.Enqueue(action);
	public void EnqueueMiseAJourUrgente(Action action) => _misesAJourUrgentes.Enqueue(action);

	/// <summary>Dépose un travail d'intégration (mesh, collision, flore) avec coût estimé pour respecter un budget de triangles par frame.</summary>
	public void EnqueueIntegration(Action action, int coutVerticesEstime = 12000)
	{
		if (action == null) return;
		_fileIntegrationMainThread.Enqueue(new TacheIntegration(action, Mathf.Max(1, coutVerticesEstime)));
	}

	private void AjouterEnFileSolidification(ChunkData data)
	{
		if (data == null || _setSolidificationNormale.Contains(data))
			return;
		_fileAttenteSolidification.Add(data);
		_setSolidificationNormale.Add(data);
		data.EstEnFileSolidification = true;
	}

	private void RetirerDeFileSolidification(ChunkData data)
	{
		if (data == null || !_setSolidificationNormale.Remove(data))
			return;
		_fileAttenteSolidification.Remove(data);
		data.EstEnFileSolidification = false;
	}

	private void DemanderRafraichissementRadar(Vector3 positionObservation, float cooldownSec)
	{
		_positionRadarEnAttente = positionObservation;
		_rebuildRadarEnAttente = true;
		if (_radarEnCours || _cooldownRebuildRadar > 0f)
			return;
		_rebuildRadarEnAttente = false;
		_cooldownRebuildRadar = Mathf.Max(0.01f, cooldownSec);
		ActualiserVisibiliteEtTriChunks(positionObservation);
	}

	public bool BootstrapInitialStabilise()
	{
		if (!ChunkSousPiedsAPret())
			return false;
		if (CompterBacklog() > Mathf.Max(0, SeuilBacklogBootstrapStable))
			return false;
		if (ExigerSolidificationVidePourBootstrap
			&& (_fileAttenteSolidificationUrgente.Count > 0 || _fileAttenteSolidification.Count > 0))
			return false;
		return true;
	}

	/// <summary>Enfile un chunk pour calcul en arrière-plan (Forge restreinte). Ne lance pas de Task.Run : le lancement est limité à MaxTravailleurs dans _PhysicsProcess.</summary>
	public void EnqueueChunkGeneration(ChunkData data, DonneesChunk donnees)
	{
		if (data == null || donnees == null) return;
		lock (_lockFileAttenteMaths)
			_fileAttenteMathsData.Add((data, donnees));
	}

	/// <summary>Architecture AAA : fusionne les 45 SectionPayload en un mesh + shape, crée les RIDs RenderingServer/PhysicsServer3D, attache au monde. À appeler sur le Main Thread.</summary>
	internal void IntegrerChunkDataRIDs(ChunkData data, List<SectionPayload> payloads)
	{
		if (data == null || payloads == null || payloads.Count == 0 || !IsInsideTree()) return;
		World3D world = GetWorld3D();
		if (world == null) return;

		// 1. Fusion des payloads en un seul ArrayMesh (terrain) sans SurfaceTool/GenerateNormals.
		int totalTerrainVertices = 0;
		foreach (var p in payloads)
			if (p?.SommetsVisuels != null)
				totalTerrainVertices += p.SommetsVisuels.Length;
		if (totalTerrainVertices <= 0) return;

		var terrainVertices = new Vector3[totalTerrainVertices];
		var terrainNormals = new Vector3[totalTerrainVertices];
		var terrainColors = new Color[totalTerrainVertices];
		int terrainOffset = 0;
		foreach (var p in payloads)
		{
			if (p?.SommetsVisuels == null || p.SommetsVisuels.Length == 0) continue;
			int count = p.SommetsVisuels.Length;
			Array.Copy(p.SommetsVisuels, 0, terrainVertices, terrainOffset, count);

			if (p.NormalsVisuels != null && p.NormalsVisuels.Length >= count)
				Array.Copy(p.NormalsVisuels, 0, terrainNormals, terrainOffset, count);
			else
				for (int i = 0; i < count; i++) terrainNormals[terrainOffset + i] = Vector3.Up;

			if (p.CouleursVisuels != null && p.CouleursVisuels.Length >= count)
				Array.Copy(p.CouleursVisuels, 0, terrainColors, terrainOffset, count);
			else
				for (int i = 0; i < count; i++) terrainColors[terrainOffset + i] = Colors.White;

			terrainOffset += count;
		}

		var terrainArrays = new Godot.Collections.Array();
		terrainArrays.Resize((int)Mesh.ArrayType.Max);
		terrainArrays[(int)Mesh.ArrayType.Vertex] = terrainVertices;
		terrainArrays[(int)Mesh.ArrayType.Normal] = terrainNormals;
		terrainArrays[(int)Mesh.ArrayType.Color] = terrainColors;

		var mergedMesh = new ArrayMesh();
		mergedMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, terrainArrays);

		Material matTerrain = MaterielTerrain;
		if (matTerrain == null)
		{
			_materielTerrainCache ??= GD.Load<Material>("res://Manteau_Planetaire.tres");
			matTerrain = _materielTerrainCache;
		}
		if (matTerrain != null)
			mergedMesh.SurfaceSetMaterial(0, matTerrain);

		// RÈGLE CAS B (espace local) : les sommets du mesh sont en [0, TailleChunk] x [0, HauteurMax] x [0, TailleChunk].
		// Une SEULE application du décalage chunk : position monde = origine parent + (coordChunk * TailleChunk).
		// Pas de double translation (ne pas ajouter d'offset si les vertices étaient déjà en monde).
		Vector3 positionVraie = GlobalPosition + new Vector3(data.Coordonnees.X * TailleChunk, 0, data.Coordonnees.Y * TailleChunk);
		Transform3D transformChunk = new Transform3D(Basis.Identity, positionVraie);

		// 2. RenderingServer : instance visuelle sans Node
		Rid meshRid = mergedMesh.GetRid();
		Rid instanceRid = RenderingServer.Singleton.InstanceCreate();
		RenderingServer.Singleton.InstanceSetBase(instanceRid, meshRid);
		RenderingServer.Singleton.InstanceSetScenario(instanceRid, world.Scenario);
		RenderingServer.Singleton.InstanceSetTransform(instanceRid, transformChunk);

		data.VisualInstanceRID = instanceRid;
		data._meshRef = mergedMesh;
		data.PhysicsBodyRID = default;
		data.PhysicsShapeRID = default;
		data._shapeRef = null;

		// Flore (gazon + buissons) : retirer l'ancien nœud si réintégration.
		if (data._nodeFlore != null)
		{
			data._nodeFlore.QueueFree();
			data._nodeFlore = null;
		}
		// STREAMING UN-A-UN : toute la flore passe par la file différée. Le budget par frame
		// (MaxFloreParFrame*) + le gate FPS + le ramp-up assurent une apparition séquentielle,
		// jamais « tout d'un coup ». Seul le chunk sous les pieds (risque visuel de sol nu immédiat)
		// est construit immédiatement.
		Vector3 posObsFlore = ObtenirPositionObservation();
		if (_joueur != null)
		{
			Vector2I cJoueurFlore = Gestionnaire_Monde.WorldToChunkCoord(posObsFlore, TailleChunk);
			int ddx = Mathf.Abs(data.Coordonnees.X - cJoueurFlore.X);
			int ddz = Mathf.Abs(data.Coordonnees.Y - cJoueurFlore.Y);
			if (ddx == 0 && ddz == 0)
				ConstruireFloreChunk(data, posObsFlore);
			else
				EnfilerFloreChunk(data, posObsFlore);
		}
		else
		{
			EnfilerFloreChunk(data, posObsFlore);
		}

		// Physique lazy stricte : collision montée en file pour amortir le coût.
		// Seule la zone ultra proche joueur passe par la file urgente.
		if (_joueur != null)
		{
			Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(ObtenirPositionObservation(), TailleChunk);
			int dx = Mathf.Abs(data.Coordonnees.X - cJoueur.X);
			int dz = Mathf.Abs(data.Coordonnees.Y - cJoueur.Y);
			if (dx == 0 && dz == 0)
			{
				AssurerCorpsPhysiqueChunk(data);
				if (data.PhysicsBodyRID.IsValid)
				{
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, world.Space);
					data.EstEnFileSolidification = false;
				}
			}
			else if (dx <= 1 && dz <= 1)
			{
				if (!data.EstEnFileSolidification)
				{
					RetirerDeFileSolidification(data);
					EnfilerSolidificationUrgenteUnique(data);
					data.EstEnFileSolidification = true;
				}
			}
			else if (!data.EstEnFileSolidification)
			{
				AjouterEnFileSolidification(data);
			}
		}

		// 4. Eau : on conserve SurfaceTool ici (chemin robuste visuellement avec le matériau eau existant).
		var stEau = new SurfaceTool();
		stEau.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var p in payloads)
		{
			if (p?.SommetsEau == null || p.SommetsEau.Length == 0) continue;
			for (int i = 0; i < p.SommetsEau.Length; i++)
			{
				stEau.SetNormal(p.NormalsEau != null && i < p.NormalsEau.Length ? p.NormalsEau[i] : Vector3.Up);
				stEau.AddVertex(p.SommetsEau[i]);
			}
		}
		stEau.GenerateNormals();
		ArrayMesh meshEau = stEau.Commit();
		if (meshEau != null && meshEau.GetSurfaceCount() > 0)
		{
			Rid waterRid = RenderingServer.Singleton.InstanceCreate();
			RenderingServer.Singleton.InstanceSetBase(waterRid, meshEau.GetRid());
			RenderingServer.Singleton.InstanceSetScenario(waterRid, world.Scenario);
			RenderingServer.Singleton.InstanceSetTransform(waterRid, transformChunk);
			var gestionnaire = GetParent() as Gestionnaire_Monde;
			if (gestionnaire != null && gestionnaire.MaterielEau != null)
				RenderingServer.Singleton.InstanceGeometrySetMaterialOverride(waterRid, gestionnaire.MaterielEau.GetRid());
			else
				GD.PrintErr("CRITIQUE: MaterielEau non assigné (Gestionnaire_Monde._Ready n'a pas créé le matériau ou parent absent).");
			data.WaterInstanceRID = waterRid;
			data._meshEauRef = meshEau;
		}

		// Fade-in d'émergence (anti pop-in) : on démarre le chunk en transparence totale
		// et on l'anime vers opaque en DureeFonduEmergenceChunk secondes. Purement visuel.
		if (DureeFonduEmergenceChunk > 0.01f && data.VisualInstanceRID.IsValid)
		{
			try
			{
				RenderingServer.Singleton.InstanceGeometrySetTransparency(data.VisualInstanceRID, 1f);
				if (data.WaterInstanceRID.IsValid)
					RenderingServer.Singleton.InstanceGeometrySetTransparency(data.WaterInstanceRID, 1f);
				_animsEmergence.Add(new AnimEmergenceChunk
				{
					VisualRid = data.VisualInstanceRID,
					WaterRid = data.WaterInstanceRID,
					FloreNodeRid = default,
					TempsEcoule = 0f,
					Duree = DureeFonduEmergenceChunk
				});
			}
			catch { /* InstanceGeometrySetTransparency requiert un material supportant la transparence ; si ça échoue, on laisse le chunk opaque direct (pas de pop-in au moins lissé par le streaming). */ }
		}
	}

	/// <summary>Avance les fondus d'émergence. Appelé 1×/frame depuis _PhysicsProcess. Retire les anims terminées.</summary>
	private void TraiterAnimationsEmergence(float dt)
	{
		if (_animsEmergence.Count == 0) return;
		for (int i = _animsEmergence.Count - 1; i >= 0; i--)
		{
			var anim = _animsEmergence[i];
			anim.TempsEcoule += dt;
			float t = Mathf.Clamp(anim.TempsEcoule / Mathf.Max(0.01f, anim.Duree), 0f, 1f);
			// Courbe ease-out : transparence 1 → 0 avec accélération en fin (apparition douce puis franche).
			float transparency = 1f - (t * t);
			if (anim.VisualRid.IsValid)
			{
				try { RenderingServer.Singleton.InstanceGeometrySetTransparency(anim.VisualRid, transparency); }
				catch { }
			}
			if (anim.WaterRid.IsValid)
			{
				try { RenderingServer.Singleton.InstanceGeometrySetTransparency(anim.WaterRid, transparency); }
				catch { }
			}
			if (t >= 1f)
			{
				if (anim.VisualRid.IsValid)
					try { RenderingServer.Singleton.InstanceGeometrySetTransparency(anim.VisualRid, 0f); } catch { }
				if (anim.WaterRid.IsValid)
					try { RenderingServer.Singleton.InstanceGeometrySetTransparency(anim.WaterRid, 0f); } catch { }
				_animsEmergence.RemoveAt(i);
			}
			else
			{
				_animsEmergence[i] = anim;
			}
		}
	}

	/// <summary>Réserve une fenêtre raisonnable autour du spawn (pas RenderDistance entier : 200² chunks = blocage / liste énorme / chargement infini).</summary>
	public void ReserverChunkSpawnPrioritaire(Vector2I coordSpawn)
	{
		// Cap strict : au plus ce qu’il faut pour la dormance + marge ; le radar remplira le reste progressivement.
		int rayonSpawn = Mathf.Min(RayonChargementChunksActif(), Mathf.Max(RayonDormancePhysique + MargePreloadChunks + 8, 12));
		var prioritaire = new List<Vector2I>();
		for (int dx = -rayonSpawn; dx <= rayonSpawn; dx++)
			for (int dz = -rayonSpawn; dz <= rayonSpawn; dz++)
				prioritaire.Add(new Vector2I(coordSpawn.X + dx, coordSpawn.Y + dz));
		Vector2 centre = new Vector2(coordSpawn.X, coordSpawn.Y);
		prioritaire.Sort((a, b) =>
		{
			float da = new Vector2(a.X, a.Y).DistanceSquaredTo(centre);
			float db = new Vector2(b.X, b.Y).DistanceSquaredTo(centre);
			return da.CompareTo(db);
		});
		_chunksACharger.InsertRange(0, prioritaire);
		_ancienChunkJoueur = coordSpawn;
	}

	private const int MaxMeshesParFrameVisuelles = 2;
	private const int MaxMeshesParFrameModification = 16;
	// Déclenche plus tôt la voie "priorité joueur" pour éviter que le gate bloque l'avant à vitesse modérée.
	private const float SeuilVitessePrioriteJoueur = 2.9f;
	private float _tempsDepuisNettoyage;
	private const float IntervalleNettoyageChunks = 1.5f;

	private void EnfilerSolidificationUrgenteUnique(ChunkData data)
	{
		if (data == null) return;
		if (_setSolidificationUrgente.Add(data))
			_fileAttenteSolidificationUrgente.Add(data);
	}

	private void EnfilerSolidificationUrgenteAutour(Vector3 pointMonde, int rayonChunks)
	{
		int rayon = Mathf.Clamp(rayonChunks, 0, 3);
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pointMonde, TailleChunk);
		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I cc = new Vector2I(c.X + dx, c.Y + dz);
				if (!_chunksData.TryGetValue(cc, out var data)) continue;
				if (data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification) continue;
				if (data.EstEnFileSolidification)
					RetirerDeFileSolidification(data);
				else
					data.EstEnFileSolidification = true;
				EnfilerSolidificationUrgenteUnique(data);
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree()) return; // GARROT SPATIAL : pas de manipulation de chunks si l'arbre s'effondre.
		float dt = (float)delta;
		ulong debutFramePerfUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownRebuildRadar = Mathf.Max(0f, _cooldownRebuildRadar - dt);
		_cooldownServicesLointains = Mathf.Max(0f, _cooldownServicesLointains - dt);
		_cooldownDrainProfilage += dt;
		TraiterAnimationsEmergence(dt);
		Camera3D cameraActive = ObtenirCameraObservation();
		Vector3 positionObservation = cameraActive != null ? cameraActive.GlobalPosition : (_joueur?.GlobalPosition ?? Vector3.Zero);
		Vector3 directionObservation = cameraActive != null ? (-cameraActive.GlobalTransform.Basis.Z).Normalized() :
			(_joueur != null ? (-_joueur.GlobalTransform.Basis.Z).Normalized() : Vector3.Forward);
		Vector2I chunkObservationActuel = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		MettreAJourAutoDiagnostic(dt);
		int niveauUrgence = _niveauUrgencePerf;
		float budgetFrameMs = Mathf.Clamp(BudgetFrameCibleMs, 8f, 25f);
		if (niveauUrgence >= 3) budgetFrameMs -= Mathf.Clamp(MargeBudgetUrgenceMs, 0.1f, 4f);
		else if (niveauUrgence >= 2) budgetFrameMs -= Mathf.Clamp(MargeBudgetUrgenceMs * 0.6f, 0.1f, 4f);
		budgetFrameMs = Mathf.Clamp(budgetFrameMs, 7.2f, 25f);
		ulong budgetFrameUs = (ulong)Mathf.Max(1000f, budgetFrameMs * 1000f);
		bool BudgetFrameDepasse() => PerfBudgetMonitor.Begin() - debutFramePerfUs >= budgetFrameUs;
		float vitesseJoueurXZ = 0f;
		if (_joueur != null)
		{
			Vector3 vv = _joueur.Velocity;
			vitesseJoueurXZ = Mathf.Sqrt(vv.X * vv.X + vv.Z * vv.Z);
		}
		bool prioriteJoueur = vitesseJoueurXZ >= SeuilVitessePrioriteJoueur;
		if (_joueur != null)
		{
			EnfilerSolidificationUrgenteAutour(_joueur.GlobalPosition, RayonPrioriteCollisionJoueur);
			if (prioriteJoueur)
			{
				Vector3 vel = _joueur.Velocity;
				Vector3 velXZ = new Vector3(vel.X, 0f, vel.Z);
				if (velXZ.LengthSquared() > 0.25f)
				{
					Vector3 pointAnticipe = _joueur.GlobalPosition + velXZ * Mathf.Max(0.35f, SecondesAnticipationCollision);
					EnfilerSolidificationUrgenteAutour(pointAnticipe, RayonPrioriteCollisionJoueur);
				}
			}
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

		// 2) Intégrations : chargement initial agressif ; exploration : plusieurs par frame pour suivre un monde infini.
		bool enChargement = !ChunkSousPiedsAPret();
		// GARANTIE SOL JOUEUR : dès que le sol proche manque ou que le joueur est en l'air, on refuse toute restriction sous les pieds.
		bool joueurEnChute = _joueur != null && _joueur.Velocity.Y < -0.5f;
		bool doitGarantirProcheJoueur = enChargement || joueurEnChute || prioriteJoueur;
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
		// Priorité input : limiter les gros bursts quand le joueur file vite.
		if (prioriteJoueur)
		{
			maxIntegrations = Mathf.Max(2, Mathf.Min(maxIntegrations, 3));
			budgetVerticesDyn = Mathf.Max(18000, Mathf.RoundToInt(budgetVerticesDyn * 0.78f));
		}
		// Les restrictions d'urgence ne s'appliquent JAMAIS si le sol proche joueur n'est pas prêt (sinon chute dans le vide).
		if (!doitGarantirProcheJoueur && urgencePerfExtreme)
		{
			maxIntegrations = 1;
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 12000);
		}
		else if (!doitGarantirProcheJoueur && urgencePerfCritique)
		{
			maxIntegrations = 1;
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 12000);
		}
		else if (!doitGarantirProcheJoueur && urgencePerfForte)
		{
			maxIntegrations = Mathf.Min(maxIntegrations, 2);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, 18000);
		}
		// Plancher dur : si le sol manque, on refuse toute limite < 3 intégrations et 22k vertices.
		if (doitGarantirProcheJoueur)
		{
			maxIntegrations = Mathf.Max(maxIntegrations, 3);
			budgetVerticesDyn = Mathf.Max(budgetVerticesDyn, 22000);
		}
		// GATE FPS STRICT : hors zone critique, gel total si FPS < seuil, puis ramp-up 1→budget.
		maxIntegrations = AppliquerGateEtRampUp(maxIntegrations, doitGarantirProcheJoueur, 1);
		if (!doitGarantirProcheJoueur && _gateStreamingGele) budgetVerticesDyn = 0;
		else if (!doitGarantirProcheJoueur && _tempsDepuisDegel < DureeRampUpPostDegel)
		{
			float tRamp = Mathf.Clamp(_tempsDepuisDegel / Mathf.Max(0.01f, DureeRampUpPostDegel), 0f, 1f);
			budgetVerticesDyn = Mathf.Min(budgetVerticesDyn, Mathf.RoundToInt(Mathf.Lerp(12000, budgetVerticesDyn, tRamp)));
		}
		int integrations = 0;
		int verticesIntegres = 0;
		ulong debutIntegrationsUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		while (integrations < maxIntegrations && _fileIntegrationMainThread.TryDequeue(out var integration))
		{
			int cout = Mathf.Max(1, integration.CoutVerticesEstime);
			if (integrations > 0 && verticesIntegres + cout > budgetVerticesDyn)
			{
				_fileIntegrationMainThread.Enqueue(integration);
				break;
			}
			try { integration.Action.Invoke(); }
			catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
			catch (System.Exception ex) { GD.PrintErr("Monde_Client intégration: ", ex.Message); }
			verticesIntegres += cout;
			integrations++;
		}
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/Integrations", debutIntegrationsUs);

		// 3) Solidification physique lissée : collisions urgentes (autour joueur) puis fond.
		ulong debutSolidificationUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		int solidificationsEffectuees = 0;
		if (_fileAttenteSolidificationUrgente.Count > 0 || _fileAttenteSolidification.Count > 0)
		{
			Vector2I coordObsSolidif = chunkObservationActuel;
			int baseSolidifications = enChargement ? 6 : Mathf.Max(1, MaxSolidificationsParFrameExploration);
			int maxSolidifications = Mathf.Clamp(Mathf.RoundToInt(baseSolidifications * Mathf.Lerp(0.60f, 1.12f, _ratioChargeAuto) * facteurAntiSpikeBacklog), 1, Mathf.Max(1, baseSolidifications + 2));
			if (prioriteJoueur)
				maxSolidifications = Mathf.Max(maxSolidifications, Mathf.Max(6, MaxSolidificationsPrioriteJoueur));
			if (_fileAttenteSolidificationUrgente.Count > 0)
				maxSolidifications = Mathf.Max(maxSolidifications, Mathf.Min(12, 4 + _fileAttenteSolidificationUrgente.Count / 4));
			if (!doitGarantirProcheJoueur && urgencePerfExtreme)
				maxSolidifications = Mathf.Min(maxSolidifications, 1);
			else if (!doitGarantirProcheJoueur && urgencePerfCritique)
				maxSolidifications = Mathf.Min(maxSolidifications, 2);
			else if (!doitGarantirProcheJoueur && urgencePerfForte)
				maxSolidifications = Mathf.Min(maxSolidifications, 3);
			// Plancher anti-chute : si le sol proche joueur n'est pas prêt, garantir au moins 4 solidifications par frame.
			if (doitGarantirProcheJoueur)
				maxSolidifications = Mathf.Max(maxSolidifications, 4);
			// GATE FPS STRICT : hors zone critique, 0 si gelé, puis ramp-up.
			maxSolidifications = AppliquerGateEtRampUp(maxSolidifications, doitGarantirProcheJoueur, 1);
			int efforts = 0;
			World3D w = GetWorld3D();
			while (_fileAttenteSolidificationUrgente.Count > 0 && efforts < maxSolidifications && w != null)
			{
				int idxUrgent = _fileAttenteSolidificationUrgente.Count - 1;
				ChunkData urgent = _fileAttenteSolidificationUrgente[idxUrgent];
				_fileAttenteSolidificationUrgente.RemoveAt(idxUrgent);
				_setSolidificationUrgente.Remove(urgent);
				if (urgent == null) continue;
				AssurerCorpsPhysiqueChunk(urgent);
				if (urgent.PhysicsBodyRID.IsValid)
				{
					PhysicsServer3D.Singleton.BodySetSpace(urgent.PhysicsBodyRID, w.Space);
					urgent.EstEnFileSolidification = false;
				}
				efforts++;
			}
			while (_fileAttenteSolidification.Count > 0 && efforts < maxSolidifications)
			{
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
					}
					else
					{
						_fileAttenteSolidification.RemoveAt(idxProche);
						_setSolidificationNormale.Remove(chunkASolidifier);
						AjouterEnFileSolidification(chunkASolidifier);
					}
				}
				else
				{
					_fileAttenteSolidification.RemoveAt(idxProche);
					_setSolidificationNormale.Remove(chunkASolidifier);
					AjouterEnFileSolidification(chunkASolidifier);
				}
				efforts++;
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
		if (urgencePerfExtreme && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = 1;
			budgetLancementsWorkers = 1;
		}
		else if (urgencePerfCritique && !doitGarantirProcheJoueur)
		{
			maxTravailleurs = Mathf.Min(maxTravailleurs, 1);
			budgetLancementsWorkers = 1;
		}
		else if (urgencePerfForte && !doitGarantirProcheJoueur)
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
		int workersLancesTick = 0;
		ulong debutWorkersUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		while (Thread.VolatileRead(ref _chunksEnCoursDeCalcul) < maxTravailleurs && workersLancesTick < budgetLancementsWorkers)
		{
			ChunkData chunkData = null;
			DonneesChunk donnees = null;
			lock (_lockFileAttenteMaths)
			{
				if (_fileAttenteMathsData.Count == 0) break;
				int best = 0;
				float bestD = float.MaxValue;
				int fenetreSelection = Mathf.Clamp(FenetreSelectionTravailMaths, 4, 256);
				int limiteScan = Mathf.Min(_fileAttenteMathsData.Count, fenetreSelection);
				for (int i = 0; i < limiteScan; i++)
				{
					var c = _fileAttenteMathsData[i].data.Coordonnees;
					float d = (c.X - obsChunk.X) * (c.X - obsChunk.X) + (c.Y - obsChunk.Y) * (c.Y - obsChunk.Y);
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
			Task.Run(() =>
			{
				try
				{
					var payloads = Chunk_Client.RemplirEtConstruirePayloads(chunkData, donnees);
					if (payloads != null)
					{
						int coutVertices = 0;
						for (int i = 0; i < payloads.Count; i++)
						{
							var p = payloads[i];
							if (p?.SommetsVisuels != null) coutVertices += p.SommetsVisuels.Length;
							if (p?.SommetsEau != null) coutVertices += p.SommetsEau.Length;
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
		}
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/LancementWorkers", debutWorkersUs);
		bool frameChargeeStreaming = integrations > 0 || solidificationsEffectuees > 0 || workersLancesTick > 0;
		bool phaseFondAutorisee = !BudgetFrameDepasse() && (!urgencePerfCritique || !frameChargeeStreaming);

		bool hadModifications = _sectionsAReconstruire.Count > 0;
		_modificationEnCours = false;

		// 1. PRIORITÉ ABSOLUE : Reconstruire les chunks modifiés (minage/pose) pour que le terrain se mette à jour
		if (hadModifications)
		{
			_chunksUniquesTemp.Clear();
			foreach (var cible in _sectionsAReconstruire)
				_chunksUniquesTemp.Add(new Vector2I(cible.cx, cible.cz));
			_sectionsAReconstruire.Clear();
			foreach (Vector2I coord in _chunksUniquesTemp)
				ExecuterReconstructionPrioritaire(coord);
			// Gel de Production : l'univers s'arrête de naître pendant cette frame.
			return;
		}
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

			// GARANTIE ANTI-CHUTE : même si le budget frame est dépassé, on DOIT demander les chunks autour du joueur
			// sinon le serveur ne génère jamais le terrain et le joueur tombe dans le vide.
			GarantirRequetesChunksProcheJoueur(positionObservation, chunkObservationActuel);
			if (ActiverProfilagePerfMondeClient)
			{
				PerfBudgetMonitor.End("MondeClient/Frame", debutFramePerfUs);
				if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageSec))
				{
					_cooldownDrainProfilage = 0f;
					PerfBudgetMonitor.FlushSiEchu("MondeClient", IntervalleLogProfilageSec);
				}
			}
			return;
		}

		// 2. Tâches de fond : dépiler l'affichage des nouveaux Chunks
		int actionsVisuelles = 0;
		int budgetVisuelDyn = MaxMeshesParFrameVisuelles;
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

		if (chunkObservationActuel != _ancienChunkJoueur)
		{
			_ancienChunkJoueur = chunkObservationActuel;
			// Replanifie les flores des chunks proches : sinon l'herbe générée quand le joueur était loin reste vide/pauvre.
			ReplanifierFloreAutourJoueur(chunkObservationActuel);
			float facteurPressionPerf = 1f;
			if (ModeSurvieFpsAgressif)
			{
				float fpsReference = Mathf.Clamp(_fpsMoyenneAuto, 20f, 120f);
				facteurPressionPerf = Mathf.Clamp(60f / fpsReference, 1f, 3.2f);
				if (urgencePerfExtreme)
					facteurPressionPerf = Mathf.Max(facteurPressionPerf, 4.5f);
			}
			float cooldownRadar = Mathf.Clamp(
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
			if (urgencePerfExtreme)
			{
				_rebuildRadarEnAttente = false;
			}
			else
			{
			_rebuildRadarEnAttente = false;
			_cooldownRebuildRadar = Mathf.Max(0.03f, IntervalleMinRebuildRadarSec);
			ActualiserVisibiliteEtTriChunks(_positionRadarEnAttente);
			}
		}

		// Etale la flore sur plusieurs frames + garde-fou pour éviter une disparition visuelle prolongée.
		int budgetFlore = CalculerBudgetFloreDynamique(enChargement, prioriteJoueur);
		ulong debutFloreUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		TraiterFloreDifferee(positionObservation, budgetFlore);
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/Flore", debutFloreUs);

		// Priorité : couvrir RayonDormancePhysique + marge (l’ancien 9×9 était trop petit vs grille 17×17 pour R=8).
		Vector2I chunkPieds = chunkObservationActuel;
		int rayonPriorite = RayonDormancePhysique + Mathf.Max(0, MargePreloadChunks);
		if (urgencePerfExtreme)
			rayonPriorite = Mathf.Max(RayonDormancePhysique + 1, rayonPriorite - 5);
		else if (urgencePerfCritique)
			rayonPriorite = Mathf.Max(RayonDormancePhysique + 2, rayonPriorite - 3);
		_prioritaireSetTemp.Clear();
		void AjouterAnneauManquant(Vector2I centre, int rayonDemi)
		{
			for (int dx = -rayonDemi; dx <= rayonDemi; dx++)
				for (int dz = -rayonDemi; dz <= rayonDemi; dz++)
				{
					var v = new Vector2I(centre.X + dx, centre.Y + dz);
					if (!_chunksData.ContainsKey(v)) _prioritaireSetTemp.Add(v);
				}
		}
		AjouterAnneauManquant(chunkPieds, rayonPriorite);
		// Anticipation de trajectoire : même en urgence extrême on demande les chunks devant le joueur
		// (rayon réduit mais jamais nul) pour éviter les chutes quand le joueur avance vite.
		if (_joueur != null && SecondesAnticipationChargement > 0.01f)
		{
			Vector3 vel = _joueur.Velocity;
			Vector3 decalAnticipation = new Vector3(vel.X, 0f, vel.Z) * SecondesAnticipationChargement;
			if (decalAnticipation.LengthSquared() > 1f)
			{
				Vector3 posFutur = positionObservation + decalAnticipation;
				Vector2I chunkFutur = Gestionnaire_Monde.WorldToChunkCoord(posFutur, TailleChunk);
				int rayonAvant;
				if (urgencePerfExtreme)
					rayonAvant = Mathf.Max(1, RayonGrilleMinSpawnPret);
				else if (urgencePerfCritique)
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
			_chunksACharger.InsertRange(0, _prioritaireListTemp);
		}

		if (_modificationEnCours) return;

		// 3. Requêtes : extraction radiale + purge obsolètes. Si le chunk sous les pieds n'est pas chargé, on demande plus de chunks (catch-up côté client).
		PurgerChunksObsolètesDeLaFile(positionObservation);
		bool chunkPiedsManquant = !_chunksData.ContainsKey(chunkPieds);
		int backlog = CompterBacklog();
		int nbRequetes = chunkPiedsManquant ? Mathf.Min(_maxRequetesDyn * 2, 20) : _maxRequetesDyn;
		if (ModeSurvieFpsAgressif)
		{
			if (backlog >= SeuilBacklogHaut) nbRequetes = Mathf.Max(1, nbRequetes / 3);
			else if (backlog >= SeuilBacklogBas) nbRequetes = Mathf.Max(1, nbRequetes / 2);
		}
		// Les coupures d'urgence ne s'appliquent pas si le sol proche joueur n'est pas prêt (anti-chute dans le vide).
		// Sous grâce « Appliquer » graphismes : on ne force pas 1 requête/frame sinon la distance 30 chunks met une éternité.
		if (_timerGraceStreamingReglageUtilisateur <= 0f)
		{
			if (urgencePerfExtreme && !doitGarantirProcheJoueur)
				nbRequetes = chunkPiedsManquant ? 2 : 1;
			else if (urgencePerfCritique && !doitGarantirProcheJoueur)
				nbRequetes = Mathf.Min(nbRequetes, chunkPiedsManquant ? 3 : 1);
			if (ModeSurvieFpsAgressif && frameChargeeStreaming && !doitGarantirProcheJoueur)
				nbRequetes = Mathf.Min(nbRequetes, chunkPiedsManquant ? 2 : 1);
		}
		// Plancher dur : en situation de risque de chute, garantir au moins 4 requêtes par frame.
		if (doitGarantirProcheJoueur)
			nbRequetes = Mathf.Max(nbRequetes, 4);
		// GATE FPS STRICT : aucune nouvelle requête hors zone critique si gelé, ramp-up ensuite.
		nbRequetes = AppliquerGateEtRampUp(nbRequetes, doitGarantirProcheJoueur, 1);
		int requetesEmises = 0;
		int minRequetesForcees = doitGarantirProcheJoueur ? 3 : 0;
		for (int n = 0; n < nbRequetes && _chunksACharger.Count > 0; n++)
		{
			// On ne coupe sur le budget frame QUE si on a déjà émis les requêtes minimales de sécurité.
			if (requetesEmises >= minRequetesForcees && BudgetFrameDepasse())
				break;
			Vector2I chunkCible = ExtraireChunkLePlusProche(_chunksACharger, positionObservation, directionObservation);
			float distCarree = DistanceCarreeAuJoueur(chunkCible, positionObservation);
			float rayonMaxCarre = (_rayonRequetesActuel + 1) * (_rayonRequetesActuel + 1);
			if (distCarree > rayonMaxCarre)
				continue;
			DemanderChunk(chunkCible);
			requetesEmises++;
		}

		_tempsDepuisNettoyage += dt;
		if (_tempsDepuisNettoyage >= IntervalleNettoyageChunks)
		{
			_tempsDepuisNettoyage = 0f;
			NettoyerChunksObsoles(positionObservation, directionObservation);
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

	private void AssurerCorpsPhysiqueChunk(ChunkData data)
	{
		if (data == null || data.PhysicsBodyRID.IsValid || data._meshRef == null) return;
		World3D world = GetWorld3D();
		if (world == null) return;

		Shape3D shape = null;
		try { shape = data._meshRef.CreateTrimeshShape(); }
		catch (Exception) { shape = null; }
		if (shape == null) return;

		Transform3D transformChunk = new Transform3D(
			Basis.Identity,
			GlobalPosition + new Vector3(data.Coordonnees.X * TailleChunk, 0, data.Coordonnees.Y * TailleChunk));

		Rid shapeRid = shape.GetRid();
		Rid bodyRid = PhysicsServer3D.Singleton.BodyCreate();
		PhysicsServer3D.Singleton.BodySetMode(bodyRid, PhysicsServer3D.BodyMode.Static);
		PhysicsServer3D.Singleton.BodySetSpace(bodyRid, default(Rid));
		PhysicsServer3D.Singleton.BodyAddShape(bodyRid, shapeRid);
		PhysicsServer3D.Singleton.BodySetState(bodyRid, PhysicsServer3D.BodyState.Transform, transformChunk);
		PhysicsServer3D.Singleton.BodySetCollisionLayer(bodyRid, 1);
		PhysicsServer3D.Singleton.BodySetCollisionMask(bodyRid, 1);

		data.PhysicsShapeRID = shapeRid;
		data.PhysicsBodyRID = bodyRid;
		data._shapeRef = shape;
	}

	private void InitialiserHorizonLointain()
	{
		if (!ActiverHorizonLod || _horizonLodMesh != null) return;
		_horizonLodMesh = new MeshInstance3D { Name = "HorizonLOD" };
		_horizonLodMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		if (_cacheMatHorizon == null)
		{
			_cacheMatHorizon = new StandardMaterial3D
			{
				VertexColorUseAsAlbedo = true,
				AlbedoColor = Colors.White,
				Roughness = 1f,
				Metallic = 0f,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
		}
		_horizonLodMesh.MaterialOverride = _cacheMatHorizon;
		AddChild(_horizonLodMesh);
	}

	private void MettreAJourHorizonLointain(Vector3 positionObservation, float dt)
	{
		if (!ActiverHorizonLod || _horizonLodMesh == null) return;
		_timerMajHorizon -= dt;
		float pas = Mathf.Max(16f, PasHorizonMetres);
		Vector2I celluleActuelle = new Vector2I(
			Mathf.FloorToInt(positionObservation.X / pas),
			Mathf.FloorToInt(positionObservation.Z / pas));
		if (_timerMajHorizon > 0f && celluleActuelle == _centreHorizonCell) return;

		float frequenceBase = Mathf.Max(0.4f, FrequenceMajHorizonSec);
		float facteurHorizon = 1f;
		if (ModeSurvieFpsAgressif)
		{
			if (_fpsMoyenneAuto < 58f) facteurHorizon = 1.35f;
			if (_fpsMoyenneAuto < 52f) facteurHorizon = 1.85f;
			if (_niveauUrgencePerf >= 2) facteurHorizon = Mathf.Max(facteurHorizon, 2.25f);
			else if (_niveauUrgencePerf == 1) facteurHorizon = Mathf.Max(facteurHorizon, 1.5f);
			if (_timerFreinSpike > 0f) facteurHorizon *= 1.28f;
		}
		_timerMajHorizon = Mathf.Clamp(frequenceBase * facteurHorizon, 0.4f, 3.2f);
		_centreHorizonCell = celluleActuelle;

		float nearRadius = (RayonDetailChunksActif() + 10) * TailleChunk;
		float farRadius = Mathf.Max(nearRadius + TailleChunk * 10f, RayonHorizonChunks * TailleChunk);
		if (farRadius <= nearRadius + pas) return;

		float cx = celluleActuelle.X * pas;
		float cz = celluleActuelle.Y * pas;
		int n = Mathf.Clamp(Mathf.CeilToInt((farRadius * 2f) / pas) + 1, 16, 200);
		float startX = cx - farRadius;
		float startZ = cz - farRadius;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		float[,] h = new float[n, n];
		for (int gz = 0; gz < n; gz++)
		{
			float wz = startZ + gz * pas;
			for (int gx = 0; gx < n; gx++)
			{
				float wx = startX + gx * pas;
				h[gx, gz] = Generateur_Voxel.ObtenirHauteurTerrainMonde((int)wx, (int)wz, _seedTerrain);
			}
		}

		for (int gz = 0; gz < n - 1; gz++)
		{
			for (int gx = 0; gx < n - 1; gx++)
			{
				float x0 = startX + gx * pas;
				float z0 = startZ + gz * pas;
				float x1 = x0 + pas;
				float z1 = z0 + pas;

				float d00 = new Vector2(x0 - positionObservation.X, z0 - positionObservation.Z).Length();
				float d10 = new Vector2(x1 - positionObservation.X, z0 - positionObservation.Z).Length();
				float d01 = new Vector2(x0 - positionObservation.X, z1 - positionObservation.Z).Length();
				float d11 = new Vector2(x1 - positionObservation.X, z1 - positionObservation.Z).Length();
				if (d00 < nearRadius && d10 < nearRadius && d01 < nearRadius && d11 < nearRadius) continue;

				Vector3 p00 = new Vector3(x0, h[gx, gz], z0);
				Vector3 p10 = new Vector3(x1, h[gx + 1, gz], z0);
				Vector3 p01 = new Vector3(x0, h[gx, gz + 1], z1);
				Vector3 p11 = new Vector3(x1, h[gx + 1, gz + 1], z1);

				Color c00 = CouleurHorizon(h[gx, gz], d00, nearRadius, farRadius);
				Color c10 = CouleurHorizon(h[gx + 1, gz], d10, nearRadius, farRadius);
				Color c01 = CouleurHorizon(h[gx, gz + 1], d01, nearRadius, farRadius);
				Color c11 = CouleurHorizon(h[gx + 1, gz + 1], d11, nearRadius, farRadius);

				st.SetNormal(Vector3.Up); st.SetColor(c00); st.AddVertex(p00);
				st.SetNormal(Vector3.Up); st.SetColor(c10); st.AddVertex(p10);
				st.SetNormal(Vector3.Up); st.SetColor(c11); st.AddVertex(p11);

				st.SetNormal(Vector3.Up); st.SetColor(c00); st.AddVertex(p00);
				st.SetNormal(Vector3.Up); st.SetColor(c11); st.AddVertex(p11);
				st.SetNormal(Vector3.Up); st.SetColor(c01); st.AddVertex(p01);

			}
		}

		ArrayMesh mesh = st.Commit();
		_horizonLodMesh.Mesh = mesh;
	}

	private static Color CouleurHorizon(float hauteur, float distance, float nearRadius, float farRadius)
	{
		float tAlt = Mathf.Clamp((hauteur - 90f) / 190f, 0f, 1f);
		Color baseC = new Color(0.24f, 0.36f, 0.22f).Lerp(new Color(0.45f, 0.48f, 0.37f), tAlt);
		float tDist = Mathf.Clamp((distance - nearRadius) / Mathf.Max(1f, farRadius - nearRadius), 0f, 1f);
		return baseC.Lerp(new Color(0.42f, 0.48f, 0.40f), tDist * 0.45f);
	}

	private void AssurerCacheCoordsChunks()
	{
		ulong frame = Engine.GetPhysicsFrames();
		bool refreshPeriodique = frame - _frameDernierRebuildCacheChunks >= 45;
		if (!refreshPeriodique && _cacheCoordsChunksCount == _chunksData.Count && _cacheCoordsChunks.Count > 0) return;
		_cacheCoordsChunks.Clear();
		foreach (var kv in _chunksData)
			_cacheCoordsChunks.Add(kv.Key);
		_cacheCoordsChunksCount = _chunksData.Count;
		_frameDernierRebuildCacheChunks = frame;
		if (_cacheCoordsChunks.Count == 0)
		{
			_indexCullingScan = 0;
			_indexDormanceScan = 0;
			return;
		}
		_indexCullingScan %= _cacheCoordsChunks.Count;
		_indexDormanceScan %= _cacheCoordsChunks.Count;
	}

	private void AppliquerCullingCameraChunks(Vector3 positionObservation, Vector3 directionObservation, float dt)
	{
		if (!ActiverCullingCameraChunks) return;
		_timerCullingCamera -= dt;
		if (_timerCullingCamera > 0f) return;
		_timerCullingCamera = _intervalleCullingDyn;
		AssurerCacheCoordsChunks();
		if (_cacheCoordsChunks.Count == 0) return;
		int basculesRestantes = Mathf.Max(8, _maxBasculesCullingDyn);
		int chunksAEvaluerBase = Mathf.Clamp(MaxChunksEvaluesCullingParPasse, 32, 4000);
		float facteurCulling = Mathf.Lerp(0.5f, 1f, _ratioChargeAuto);
		if (ModeSurvieFpsAgressif)
		{
			if (_niveauUrgencePerf >= 2) facteurCulling *= 0.55f;
			else if (_niveauUrgencePerf == 1) facteurCulling *= 0.78f;
			if (_fpsMoyenneAuto < 56f) facteurCulling *= 0.82f;
			if (_timerFreinSpike > 0f) facteurCulling *= 0.70f;
		}
		int chunksAEvaluer = Mathf.Clamp(Mathf.RoundToInt(chunksAEvaluerBase * facteurCulling), 24, chunksAEvaluerBase);
		if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 55f)
			basculesRestantes = Mathf.Max(6, Mathf.RoundToInt(basculesRestantes * 0.75f));

		float cosHalf = Mathf.Cos(Mathf.DegToRad(Mathf.Clamp(AngleCullingCameraDeg, 80f, 175f) * 0.5f));
		float rayonToujoursVisible = Mathf.Max(RayonDormancePhysique + 1, MargeChunksToujoursVisibles) * TailleChunk;
		float rayonToujoursVisibleCarre = rayonToujoursVisible * rayonToujoursVisible;

		int total = _cacheCoordsChunks.Count;
		int evalues = 0;
		while (evalues < chunksAEvaluer && total > 0)
		{
			if (_indexCullingScan >= total) _indexCullingScan = 0;
			Vector2I coord = _cacheCoordsChunks[_indexCullingScan];
			_indexCullingScan++;
			evalues++;
			if (!_chunksData.TryGetValue(coord, out ChunkData data)) continue;
			Vector3 centre = new Vector3((data.Coordonnees.X + 0.5f) * TailleChunk, positionObservation.Y, (data.Coordonnees.Y + 0.5f) * TailleChunk);
			Vector3 to = centre - positionObservation;
			float d2 = to.LengthSquared();
			bool visible = true;
			if (d2 > rayonToujoursVisibleCarre)
			{
				float len = Mathf.Sqrt(d2);
				Vector3 dir = to / Mathf.Max(0.0001f, len);
				float dot = directionObservation.Dot(dir);
				visible = dot >= cosHalf;
			}

			if (data.CullingVisible == visible) continue;
			if (basculesRestantes <= 0) break;
			data.CullingVisible = visible;
			if (data.VisualInstanceRID.IsValid)
				RenderingServer.Singleton.InstanceSetVisible(data.VisualInstanceRID, visible);
			if (data.WaterInstanceRID.IsValid)
				RenderingServer.Singleton.InstanceSetVisible(data.WaterInstanceRID, visible);
			if (data._nodeFlore is Node3D flore && flore.Visible != visible)
				flore.Visible = visible;
			basculesRestantes--;
			if (basculesRestantes <= 0) break;
		}
	}

	private bool EstChunkProche(Vector2I coordChunk, Vector3 positionObservation, int rayonChunks)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		return Mathf.Abs(coordChunk.X - obs.X) <= rayonChunks && Mathf.Abs(coordChunk.Y - obs.Y) <= rayonChunks;
	}

	private void EnfilerFloreChunk(ChunkData data, Vector3 positionObservation)
	{
		if (data == null || data.InventaireFlore == null || data.InventaireFlore.Count == 0) return;
		if (_setFloreDifferee.Add(data.Coordonnees))
		{
			_fileFloreDifferee.Add(data.Coordonnees);
			_frameEnqueueFlore[data.Coordonnees] = Engine.GetPhysicsFrames();
		}
	}

	private void ConstruireFloreChunk(ChunkData data, Vector3 positionObservation)
	{
		if (data == null || !_chunksData.ContainsKey(data.Coordonnees)) return;
		_setFloreDifferee.Remove(data.Coordonnees);
		_fileFloreDifferee.Remove(data.Coordonnees);
		_frameEnqueueFlore.Remove(data.Coordonnees);
		if (data.InventaireFlore == null || data.InventaireFlore.Count == 0) return;
		// Si un node flore existe déjà (généré quand le joueur était loin et souvent presque vide),
		// on le met à jour au lieu de retourner : rend l'herbe/buissons visibles dès que tu t'approches.
		if (data._nodeFlore is Node3D existant)
		{
			Chunk_Client.MettreAJourFlorePourChunkData(data, positionObservation, existant);
			return;
		}
		var node = Chunk_Client.CreerNoeudFlorePourChunkData(data, positionObservation, TailleChunk);
		if (node == null) return;
		node.Visible = data.CullingVisible;
		AddChild(node);
		data._nodeFlore = node;
	}

	/// <summary>
	/// Replanifie la reconstruction des flores dans le rayon de visibilité gazon, pour que l'herbe/buissons
	/// apparaissent quand tu approches de chunks générés trop tôt (alors que tu étais loin).
	/// </summary>
	private void ReplanifierFloreAutourJoueur(Vector2I chunkCentre)
	{
		int rayon = Mathf.Max(1, Mathf.Min(RayonGazonVisibleChunks, RayonBuissonsVisibleChunks));
		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I coord = new Vector2I(chunkCentre.X + dx, chunkCentre.Y + dz);
				if (!_chunksData.TryGetValue(coord, out var data)) continue;
				if (data.InventaireFlore == null || data.InventaireFlore.Count == 0) continue;
				if (_setFloreDifferee.Add(coord))
				{
					_fileFloreDifferee.Add(coord);
					_frameEnqueueFlore[coord] = Engine.GetPhysicsFrames();
				}
			}
		}
	}

	private void TraiterFloreDifferee(Vector3 positionObservation, int budgetParFrame)
	{
		int budget = Mathf.Max(0, budgetParFrame);
		ulong frameCourante = Engine.GetPhysicsFrames();
		int traites = 0;
		int tentatives = _fileFloreDifferee.Count;
		while (traites < budget && _fileFloreDifferee.Count > 0 && tentatives > 0)
		{
			tentatives--;
			Vector2I coord = ExtraireChunkLePlusProcheSimple(_fileFloreDifferee, positionObservation);
			if (_frameEnqueueFlore.TryGetValue(coord, out ulong frameAjout) && frameAjout >= frameCourante)
			{
				// Laisse au moins 1 frame entre l’intégration du chunk et la création de sa flore.
				_fileFloreDifferee.Add(coord);
				continue;
			}
			_setFloreDifferee.Remove(coord);
			_frameEnqueueFlore.Remove(coord);
			if (!_chunksData.TryGetValue(coord, out var data)) continue;
			ConstruireFloreChunk(data, positionObservation);
			traites++;
		}
	}

	private int CompterBacklog()
	{
		int pendingMaths;
		lock (_lockFileAttenteMaths)
			pendingMaths = _fileAttenteMathsData.Count;
		return pendingMaths
			+ _fileIntegrationMainThread.Count
			+ _fileAttenteSolidification.Count
			+ _fileFloreDifferee.Count
			+ Thread.VolatileRead(ref _chunksEnCoursDeCalcul);
	}

	private void AjusterFenetreRequetes(float dt)
	{
		if (_timerGraceStreamingReglageUtilisateur > 0f)
			_timerGraceStreamingReglageUtilisateur = Mathf.Max(0f, _timerGraceStreamingReglageUtilisateur - dt);
		int rayonDetail = RayonChargementChunksActif();
		int minRayonRequetes = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		// Hors « Sauver les FPS » : pas de réduction automatique du rayon ni throttling par backlog sur cette fenêtre.
		if (!ModeSurvieFpsAgressif)
		{
			_rayonRequetesActuel = Mathf.Clamp(rayonDetail, minRayonRequetes, rayonDetail);
			return;
		}
		if (_rayonRequetesActuel <= 0) _rayonRequetesActuel = minRayonRequetes;
		_rayonRequetesActuel = Mathf.Clamp(_rayonRequetesActuel, minRayonRequetes, rayonDetail);

		int backlog = CompterBacklog();
		_timerExpansionRequetes -= dt;
		_timerProgressionForceeRayon -= dt;
		if (backlog >= SeuilBacklogHaut)
		{
			_rayonRequetesActuel = Mathf.Max(Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks), _rayonRequetesActuel - 1);
			_timerExpansionRequetes = Mathf.Max(0.1f, IntervalleExpansionRequetesSec * 0.6f);
		}
		else if (_timerExpansionRequetes <= 0f && backlog <= SeuilBacklogBas)
		{
			int gap = Mathf.Max(0, rayonDetail - _rayonRequetesActuel);
			int pas = gap > 40 ? 2 : 1;
			if (_timerGraceStreamingReglageUtilisateur > 0f)
				pas = Mathf.Max(pas, Mathf.Clamp(gap / 6, 2, 5));
			_rayonRequetesActuel = Mathf.Min(rayonDetail, _rayonRequetesActuel + pas);
			_timerExpansionRequetes = Mathf.Max(0.1f, IntervalleExpansionRequetesSec);
		}
		if (_timerGraceStreamingReglageUtilisateur <= 0f)
		{
			if (_niveauUrgencePerf >= 3)
			{
				int capUrgence = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
				_rayonRequetesActuel = Mathf.Min(_rayonRequetesActuel, capUrgence);
				_timerExpansionRequetes = Mathf.Max(_timerExpansionRequetes, 0.32f);
				_timerProgressionForceeRayon = Mathf.Max(_timerProgressionForceeRayon, 0.60f);
			}
			else if (_niveauUrgencePerf >= 2)
			{
				int capUrgence = Mathf.Max(RayonDormancePhysique + 2, RayonInitialRequetesChunks);
				_rayonRequetesActuel = Mathf.Min(_rayonRequetesActuel, capUrgence);
				_timerExpansionRequetes = Mathf.Max(_timerExpansionRequetes, 0.25f);
				_timerProgressionForceeRayon = Mathf.Max(_timerProgressionForceeRayon, 0.45f);
			}
			else if (_niveauUrgencePerf == 1)
			{
				int capUrgence = Mathf.Max(RayonDormancePhysique + 4, RayonInitialRequetesChunks);
				_rayonRequetesActuel = Mathf.Min(_rayonRequetesActuel, capUrgence);
				_timerExpansionRequetes = Mathf.Max(_timerExpansionRequetes, 0.16f);
			}
		}

		// Même sous charge, le rayon avance lentement pour éviter un "blocage complet" du chargement lointain.
		if (_niveauUrgencePerf <= 0 && _timerProgressionForceeRayon <= 0f && _rayonRequetesActuel < rayonDetail)
		{
			_rayonRequetesActuel = Mathf.Min(rayonDetail, _rayonRequetesActuel + 1);
			_timerProgressionForceeRayon = Mathf.Max(0.5f, IntervalleProgressionForceeRayonSec);
		}
	}

	private Vector2I ExtraireChunkLePlusProcheSimple(List<Vector2I> liste, Vector3 positionObservation)
	{
		if (liste.Count == 0) return Vector2I.Zero;
		Vector2 posObsV2 = new Vector2(positionObservation.X / TailleChunk, positionObservation.Z / TailleChunk);
		int best = 0;
		float bestD = float.MaxValue;
		for (int i = 0; i < liste.Count; i++)
		{
			Vector2 c = new Vector2(liste[i].X, liste[i].Y);
			float d = c.DistanceSquaredTo(posObsV2);
			if (d < bestD) { bestD = d; best = i; }
		}
		Vector2I v = liste[best];
		liste.RemoveAt(best);
		return v;
	}

	private void ExecuterReconstructionPrioritaire(Vector2I coord)
	{
		if (!_chunksData.TryGetValue(coord, out var data)) return;
		if (data.DensitiesFlat == null || data.MaterialsFlat == null) return;
		// Libérer l'ancien mesh et la collision avant de recréer (sinon fuite RID)
		data.LibérerRids();
		var payloads = Chunk_Client.ReconstruirePayloadsDepuisData(data);
		if (payloads != null && payloads.Count > 0)
			IntegrerChunkDataRIDs(data, payloads);
	}

	private float DistanceCarreeAuJoueur(Vector2I chunk, Vector3 posObservation)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(posObservation, TailleChunk);
		int dx = chunk.X - obs.X, dz = chunk.Y - obs.Y;
		return dx * dx + dz * dz;
	}

	private void PurgerChunksObsolètesDeLaFile(Vector3 positionObservation)
	{
		// Garde la file alignée sur la vague radar active (pas tout le RenderDistance d'un coup).
		int rayonRadar = RayonRadarPreparationActif();
		float rayonMaxCarre = (rayonRadar + 2) * (rayonRadar + 2);
		for (int i = _chunksACharger.Count - 1; i >= 0; i--)
		{
			float d2 = DistanceCarreeAuJoueur(_chunksACharger[i], positionObservation);
			if (d2 > rayonMaxCarre)
				_chunksACharger.RemoveAt(i);
		}
	}

	/// <summary>Sénescence : retire de la mémoire les chunks au-delà du rayon + hystérésis. Libère les RIDs (RenderingServer/PhysicsServer3D).</summary>
	private void NettoyerChunksObsoles(Vector3 positionObservation, Vector3 directionObservation)
	{
		int rayonDetail = RayonChargementChunksActif();
		float seuilAvantCarree = (rayonDetail + 3) * (rayonDetail + 3);
		float seuilStandardCarree = (rayonDetail + 2) * (rayonDetail + 2);
		float seuilArriereCarree = (rayonDetail + 1) * (rayonDetail + 1);
		Vector2 dirObs = new Vector2(directionObservation.X, directionObservation.Z).Normalized();
		bool dirValide = dirObs.LengthSquared() > 0.0001f;
		_chunksATuerTemp.Clear();
		foreach (var kv in _chunksData)
		{
			float dist2 = DistanceCarreeAuJoueur(kv.Key, positionObservation);
			float seuilLocal = seuilStandardCarree;
			if (dirValide)
			{
				Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
				Vector2 to = new Vector2(kv.Key.X, kv.Key.Y) - posObsV2;
				float toLen = to.Length();
				if (toLen > 0.001f)
				{
					float dot = dirObs.Dot(to / toLen);
					if (dot > 0.25f) seuilLocal = seuilAvantCarree;
					else if (dot < -0.15f) seuilLocal = seuilArriereCarree;
				}
			}
			if (dist2 > seuilLocal)
				_chunksATuerTemp.Add(kv.Key);
		}
		foreach (Vector2I coord in _chunksATuerTemp)
		{
			if (_chunksData.TryGetValue(coord, out var data))
			{
				_chunksData.Remove(coord);
				RetirerDeFileSolidification(data);
				_setSolidificationUrgente.Remove(data);
				_setFloreDifferee.Remove(coord);
				_fileFloreDifferee.Remove(coord);
				_frameEnqueueFlore.Remove(coord);
				data.LibérerRids();
				NettoyerRegistreReconstruction(coord);
			}
		}
	}

	private void RetirerChunksDeLaFile(HashSet<Vector2I> aRetirer)
	{
		if (aRetirer == null || aRetirer.Count == 0 || _chunksACharger.Count == 0) return;
		for (int i = _chunksACharger.Count - 1; i >= 0; i--)
			if (aRetirer.Contains(_chunksACharger[i]))
				_chunksACharger.RemoveAt(i);
	}

	/// <summary>Extraction radiale : le chunk à distance minimale de l'épicentre (caméra/joueur). DistanceSquaredTo évite la racine.</summary>
	private Vector2I ExtraireChunkLePlusProche(List<Vector2I> liste, Vector3 positionObservation, Vector3 directionObservation)
	{
		if (liste.Count == 0) return Vector2I.Zero;
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		Vector2I chunkCible = liste[0];
		float scoreMin = float.MaxValue;
		int indexASupprimer = 0;
		float rayonNear = Mathf.Max(3, RayonDormancePhysique + 1);
		float rayonNearCarre = rayonNear * rayonNear;
		float vitesseXZ = 0f;
		if (_joueur != null)
		{
			Vector3 v = _joueur.Velocity;
			vitesseXZ = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
		}
		float facteurMouvement = Mathf.Clamp(vitesseXZ / 6f, 0f, 1f);
		float penaliteArriere = Mathf.Lerp(180f, 340f, facteurMouvement);
		float bonusAvant = Mathf.Lerp(10f, 72f, facteurMouvement);
		int count = liste.Count;
		int fenetre = Mathf.Clamp(FenetreSelectionRequetes, 8, 512);
		int scan = Mathf.Min(count, fenetre);
		if (_curseurSelectionRequetes >= count)
			_curseurSelectionRequetes = 0;
		for (int n = 0; n < scan; n++)
		{
			int i = (_curseurSelectionRequetes + n) % count;
			Vector2 posChunk = new Vector2(liste[i].X, liste[i].Y);
			Vector2 to = posChunk - posObsV2;
			float dist = to.LengthSquared();
			float score = dist;
			if (dist > rayonNearCarre)
			{
				float d = Mathf.Sqrt(Mathf.Max(0.0001f, dist));
				Vector3 dir = new Vector3(to.X / d, 0f, to.Y / d);
				float dot = directionObservation.Dot(dir);
				if (dot < 0f)
					score += (1f - dot) * penaliteArriere;
				else
					score -= dot * bonusAvant;
			}
			if (score < scoreMin)
			{
				scoreMin = score;
				chunkCible = liste[i];
				indexASupprimer = i;
			}
		}
		_curseurSelectionRequetes = (_curseurSelectionRequetes + 1) % count;
		liste.RemoveAt(indexASupprimer);
		return chunkCible;
	}

	private int ExtraireIndexSolidificationProche(Vector2I coordObservation)
	{
		int count = _fileAttenteSolidification.Count;
		if (count <= 1) return 0;
		int fenetre = Mathf.Clamp(FenetreSelectionSolidification, 4, 256);
		int scan = Mathf.Min(count, fenetre);
		if (_curseurSelectionSolidification >= count) _curseurSelectionSolidification = 0;
		int idxBest = _curseurSelectionSolidification;
		int dBest = int.MaxValue;
		for (int n = 0; n < scan; n++)
		{
			int idx = (_curseurSelectionSolidification + n) % count;
			ChunkData c = _fileAttenteSolidification[idx];
			if (c == null) continue;
			int ddx = c.Coordonnees.X - coordObservation.X;
			int ddz = c.Coordonnees.Y - coordObservation.Y;
			int d2 = ddx * ddx + ddz * ddz;
			if (d2 < dBest)
			{
				dBest = d2;
				idxBest = idx;
			}
		}
		_curseurSelectionSolidification = (_curseurSelectionSolidification + 1) % count;
		return idxBest;
	}

	private void DeclencherReconstructionSection((int cx, int cz, int section) cible)
	{
		var coord = new Vector2I(cible.cx, cible.cz);
		if (!_chunksData.TryGetValue(coord, out _)) return;
		// AAA : pas de reconstruction par section ; on pourrait re-demander le chunk.
	}

	public void AppliquerDestructionGlobale(Vector3 pointImpact, float rayon, float forceDegats = 5.0f)
	{
		_demanderDestruction?.Invoke(pointImpact, rayon, forceDegats);
	}

	public void AppliquerCreationGlobale(Vector3 pointImpact, Vector3 normale, float rayon, int idMatiere = 1)
	{
		_demanderCreation?.Invoke(pointImpact, normale, rayon, idMatiere);
	}

	/// <summary>Mise à jour flore : le serveur a purgé du gazon (minage, gravité, fauchage). On met à jour l'inventaire et le rendu gazon pour que les brins disparaissent.</summary>
	public void RecevoirFloreModifie(Vector2I coordChunk, Dictionary<Vector3I, byte> inventaireFlore)
	{
		RecevoirFloreModifieAvecRetry(coordChunk, inventaireFlore, 0);
	}

	private void RecevoirFloreModifieAvecRetry(Vector2I coordChunk, Dictionary<Vector3I, byte> inventaireFlore, int tentative)
	{
		if (!_chunksData.TryGetValue(coordChunk, out var data))
		{
			if (tentative < 12)
				Callable.From(() => RecevoirFloreModifieAvecRetry(coordChunk, inventaireFlore, tentative + 1)).CallDeferred();
			return;
		}

		data.InventaireFlore = inventaireFlore ?? new Dictionary<Vector3I, byte>();
		Vector3 posObs = ObtenirPositionObservation();

		if (data._nodeFlore is Node3D nodeFlore)
		{
			Chunk_Client.MettreAJourFlorePourChunkData(data, posObs, nodeFlore);
			return;
		}

		// Ancien monde : racine = seul MultiMesh gazon (sans buissons instanciés).
		if (data._nodeFlore is MultiMeshInstance3D legacyGazon)
		{
			Chunk_Client.MettreAJourGazonPourChunkData(data, posObs, legacyGazon);
			return;
		}

		if (data.InventaireFlore.Count > 0)
		{
			EnfilerFloreChunk(data, posObs);
		}
	}

	public void RecevoirChunkModifie(Vector2I coordChunk, List<int> sectionsAffectees)
	{
		_modificationEnCours = true;
		if (!_chunksData.TryGetValue(coordChunk, out _)) return;
		foreach (int sec in sectionsAffectees)
			if (sec >= 0 && sec < 45) _sectionsAReconstruire.Add((coordChunk.X, coordChunk.Y, sec));
	}

	/// <summary>Micro-RPC : mise à jour voxel unique. Modifie le chunk principal ET la réplique sur le padding des voisins.</summary>
	public void AppliquerVoxel(Vector3I posGlobal, byte id)
	{
		_modificationEnCours = true;
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int localX, out int localZ);
		int cx = c.X;
		int cz = c.Y;
		int sec = Mathf.FloorToInt(posGlobal.Y / 16f);
		int localY = posGlobal.Y - sec * 16;

		if (!_chunksData.TryGetValue(new Vector2I(cx, cz), out var data)) return;
		data.SetVoxelLocal(localX, (int)posGlobal.Y, localZ, id);

		if (localX == 0 && _chunksData.TryGetValue(new Vector2I(cx - 1, cz), out var vx))
		{
			vx.SetVoxelLocal(TailleChunk, (int)posGlobal.Y, localZ, id);
			_sectionsAReconstruire.Add((cx - 1, cz, sec));
		}
		if (localX == TailleChunk - 1 && _chunksData.TryGetValue(new Vector2I(cx + 1, cz), out var vxp))
		{
			vxp.SetVoxelLocal(0, (int)posGlobal.Y, localZ, id);
			_sectionsAReconstruire.Add((cx + 1, cz, sec));
		}
		if (localZ == 0 && _chunksData.TryGetValue(new Vector2I(cx, cz - 1), out var vz))
		{
			vz.SetVoxelLocal(localX, (int)posGlobal.Y, TailleChunk, id);
			_sectionsAReconstruire.Add((cx, cz - 1, sec));
		}
		if (localZ == TailleChunk - 1 && _chunksData.TryGetValue(new Vector2I(cx, cz + 1), out var vzp))
		{
			vzp.SetVoxelLocal(localX, (int)posGlobal.Y, 0, id);
			_sectionsAReconstruire.Add((cx, cz + 1, sec));
		}
		if (localX == 0 && localZ == 0 && _chunksData.TryGetValue(new Vector2I(cx - 1, cz - 1), out var vxz))
		{
			vxz.SetVoxelLocal(TailleChunk, (int)posGlobal.Y, TailleChunk, id);
			_sectionsAReconstruire.Add((cx - 1, cz - 1, sec));
		}
		if (localX == TailleChunk - 1 && localZ == 0 && _chunksData.TryGetValue(new Vector2I(cx + 1, cz - 1), out var vxpz))
		{
			vxpz.SetVoxelLocal(0, (int)posGlobal.Y, TailleChunk, id);
			_sectionsAReconstruire.Add((cx + 1, cz - 1, sec));
		}
		if (localX == 0 && localZ == TailleChunk - 1 && _chunksData.TryGetValue(new Vector2I(cx - 1, cz + 1), out var vxzp))
		{
			vxzp.SetVoxelLocal(TailleChunk, (int)posGlobal.Y, 0, id);
			_sectionsAReconstruire.Add((cx - 1, cz + 1, sec));
		}
		if (localX == TailleChunk - 1 && localZ == TailleChunk - 1 && _chunksData.TryGetValue(new Vector2I(cx + 1, cz + 1), out var vxpzp))
		{
			vxpzp.SetVoxelLocal(0, (int)posGlobal.Y, 0, id);
			_sectionsAReconstruire.Add((cx + 1, cz + 1, sec));
		}

		if (sec >= 0 && sec < 45) _sectionsAReconstruire.Add((cx, cz, sec));
		if (localY == 0 && posGlobal.Y > 0 && sec - 1 >= 0) _sectionsAReconstruire.Add((cx, cz, sec - 1));
		if (localY == 15 && sec + 1 < 45) _sectionsAReconstruire.Add((cx, cz, sec + 1));
		if (localX == TailleChunk - 1) _sectionsAReconstruire.Add((cx + 1, cz, sec));
		if (localZ == TailleChunk - 1) _sectionsAReconstruire.Add((cx, cz + 1, sec));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void AppliquerVoxelRPC(int x, int y, int z, int id)
	{
		AppliquerVoxel(new Vector3I(x, y, z), (byte)id);
	}

	/// <summary>RPC Serveur → Client : ordre de destruction. Le Client n'a pas le droit de discuter.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void OrdonnerDestructionChunkRPC(int coordX, int coordZ)
	{
		var coord = new Vector2I(coordX, coordZ);
		if (_chunksData.TryGetValue(coord, out var data))
		{
			_chunksData.Remove(coord);
			data.LibérerRids();
			NettoyerRegistreReconstruction(coord);
		}
	}

	private void NettoyerRegistreReconstruction(Vector2I coordChunk)
	{
		_sectionsAReconstruire.RemoveWhere(c => c.cx == coordChunk.X && c.cz == coordChunk.Y);
	}

	[Export] public Material MaterielTerrain;
	private Material _materielTerrainCache;

	/// <summary>RPC : le serveur envoie chunk en byte[] uniquement. Ne jamais lancer Marching Cubes ici — Task.Run immédiat.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void RecevoirChunkDuServeurRPC(int coordX, int coordZ, int tailleChunk, int hauteurMax, byte[] densitiesPlates, byte[] materialsFlat, byte[] densitiesEauPlates)
	{
		var donnees = new DonneesChunk
		{
			CoordChunk = new Vector2I(coordX, coordZ),
			TailleChunk = tailleChunk,
			HauteurMax = hauteurMax,
			DensitiesQuantifiees = densitiesPlates,
			DensitiesEauQuantifiees = densitiesEauPlates,
			MaterialsFlat = materialsFlat
		};
		RecevoirDonneesChunk(new Vector2I(coordX, coordZ), donnees);
	}

	public void RecevoirDonneesChunk(Vector2I coordChunk, DonneesChunk donnees)
	{
		// Architecture AAA : ChunkData (RID) uniquement, plus de Node.
		if (_chunksData.TryGetValue(coordChunk, out var existing))
		{
			EnqueueChunkGeneration(existing, donnees);
			return;
		}

		var data = new ChunkData
		{
			Coordonnees = coordChunk,
			TailleChunk = TailleChunk,
			HauteurMax = HauteurMax
		};
		data.ConfigurerBruitClimat(_seedTerrain);
		_chunksData[coordChunk] = data;
		EnqueueChunkGeneration(data, donnees);
	}

	private void AttacherEtPositionnerChunk(Chunk_Client chunkVisuel, Vector3 position)
	{
		if (!IsInsideTree()) return; // Si le jeu ferme, on annule.
		AddChild(chunkVisuel);
		chunkVisuel.Position = position;
		Vector2I obs = ObtenirCoordonneesChunkJoueur();
		chunkVisuel.MettreAJourDormance(obs.X, obs.Y);
	}

	/// <summary>Position d'observation (caméra ou joueur). Utilisée par le radar et par les chunks pour la visibilité du gazon.</summary>
	public Vector3 ObtenirPositionObservation()
	{
		Camera3D cam = ObtenirCameraObservation();
		return cam != null ? cam.GlobalPosition : (_joueur?.GlobalPosition ?? Vector3.Zero);
	}

	/// <summary>Position d'interaction flore : privilégie le corps joueur (contact sol), sinon fallback observation.</summary>
	public Vector3 ObtenirPositionInteractionFlore()
	{
		return _joueur?.GlobalPosition ?? ObtenirPositionObservation();
	}

	/// <summary>Position utilisée par le radar (chunk le plus proche). Utilise la caméra active si disponible (caméra libre), sinon le corps du joueur.</summary>
	private Vector2I ObtenirCoordonneesChunkJoueur()
	{
		Vector3 pos = ObtenirPositionObservation();
		return Gestionnaire_Monde.WorldToChunkCoord(pos, TailleChunk);
	}

	private void ActualiserVisibiliteEtTriChunks(Vector3 positionObservation)
	{
		if (_radarEnCours) return;

		_radarEnCours = true;
		ulong debutRadarUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		Vector2I chunkCentreRadar = Gestionnaire_Monde.WorldToChunkCoord(positionObservation.X, positionObservation.Z, TailleChunk);
		int cjX = chunkCentreRadar.X;
		int cjZ = chunkCentreRadar.Y;
		int rayonRadar = RayonRadarPreparationActif();
		HashSet<Vector2I> chunksCharges = new HashSet<Vector2I>(_chunksData.Keys);
		List<Vector2I> copieChunksACharger = new List<Vector2I>(_chunksACharger);

		Task.Run(() =>
		{
			HashSet<Vector2I> dejaVu = new HashSet<Vector2I>(copieChunksACharger);
			foreach (var c in chunksCharges) dejaVu.Add(c);
			int rayonInterieur = Mathf.Max(0, rayonRadar - EpaisseurAnneauRadar);
			int ajoutes = 0;
			for (int dx = -rayonRadar; dx <= rayonRadar && ajoutes < _maxAjoutsRadarParPasseDyn; dx++)
				for (int dz = -rayonRadar; dz <= rayonRadar && ajoutes < _maxAjoutsRadarParPasseDyn; dz++)
				{
					int adx = Mathf.Abs(dx);
					int adz = Mathf.Abs(dz);
					if (adx < rayonInterieur && adz < rayonInterieur) continue; // Remplit l'anneau courant uniquement.
					Vector2I coord = new Vector2I(cjX + dx, cjZ + dz);
					if (dejaVu.Add(coord))
					{
						copieChunksACharger.Add(coord);
						ajoutes++;
					}
				}

			// Tri radial strict : distance au carré (pas de new Vector2 par comparaison — évite des milliers d'allocations).
			float ox = posObsV2.X, oy = posObsV2.Y;
			copieChunksACharger.Sort((a, b) =>
			{
				float da = (a.X - ox) * (a.X - ox) + (a.Y - oy) * (a.Y - oy);
				float db = (b.X - ox) * (b.X - ox) + (b.Y - oy) * (b.Y - oy);
				return da.CompareTo(db);
			});

			Callable.From(() =>
			{
				AppliquerNouveauTriRadar(copieChunksACharger);
				if (ActiverProfilagePerfMondeClient)
					PerfBudgetMonitor.End("MondeClient/RadarBuild", debutRadarUs);
			}).CallDeferred();
		});
	}

	private void AppliquerNouveauTriRadar(List<Vector2I> nouvelleListeTriee)
	{
		ulong debutApplyRadarUs = ActiverProfilagePerfMondeClient ? PerfBudgetMonitor.Begin() : 0UL;
		if (nouvelleListeTriee == null || nouvelleListeTriee.Count == 0)
		{
			_chunksACharger.Clear();
			_radarEnCours = false;
			if (ActiverProfilagePerfMondeClient)
				PerfBudgetMonitor.End("MondeClient/RadarApply", debutApplyRadarUs);
			return;
		}
		int rayonRadar = RayonRadarPreparationActif();
		int cap = Mathf.Clamp((2 * rayonRadar + 1) * (2 * rayonRadar + 1), 256, 65536);
		int n = Mathf.Min(cap, nouvelleListeTriee.Count);
		_chunksACharger.Clear();
		if (_chunksACharger.Capacity < n)
			_chunksACharger.Capacity = n;
		for (int i = 0; i < n; i++)
			_chunksACharger.Add(nouvelleListeTriee[i]);
		_radarEnCours = false;
		if (ActiverProfilagePerfMondeClient)
			PerfBudgetMonitor.End("MondeClient/RadarApply", debutApplyRadarUs);
		// Le dépilage est fait dans _PhysicsProcess (usine en continu, 60 TPS)
	}

	/// <summary>Dormance physique progressive: limite les transitions BodySetSpace par frame pour supprimer les micro-spikes.</summary>
	private void ActualiserDormanceChunks(int obsChunkX, int obsChunkZ, int maxTransitions)
	{
		World3D world = GetWorld3D();
		if (world == null) return;
		Rid space = world.Space;

		// Indispensable : le budget « transitions » peut laisser le chunk sous les PIEDS du corps dormant
		// alors que la caméra TPS / radar a déjà réveillé le décor lointain → joueur qui « vole » au-dessus du voxel visible.
		if (_joueur != null)
		{
			Vector2I cp = Gestionnaire_Monde.WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dz = -1; dz <= 1; dz++)
				{
					var coord = new Vector2I(cp.X + dx, cp.Y + dz);
					if (!_chunksData.TryGetValue(coord, out ChunkData d)) continue;
					if (d.PhysicsBodyRID.IsValid)
					{
						if (d.Dormant)
						{
							d.Dormant = false;
							PhysicsServer3D.Singleton.BodySetSpace(d.PhysicsBodyRID, space);
							if (d.EstEnFileSolidification)
							{
								RetirerDeFileSolidification(d);
							}
						}
					}
					else if (!d.EstEnFileSolidification)
					{
						RetirerDeFileSolidification(d);
						EnfilerSolidificationUrgenteUnique(d);
						d.EstEnFileSolidification = true;
					}
				}
			}
		}

		int transitions = 0;
		int limite = Mathf.Max(1, maxTransitions);

		bool BasculerDormanceChunk(ChunkData data, bool dormantCible)
		{
			if (transitions >= limite) return false;
			data.Dormant = dormantCible;
			if (data.PhysicsBodyRID.IsValid)
			{
				if (dormantCible)
				{
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, default(Rid));
					transitions++;
					if (data.EstEnFileSolidification)
					{
						RetirerDeFileSolidification(data);
					}
				}
				else
				{
					// Réveil dynamique : activer les collisions tout de suite dans le rayon (pas de file).
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, space);
					transitions++;
					if (data.EstEnFileSolidification)
					{
						RetirerDeFileSolidification(data);
					}
				}
			}
			else if (!dormantCible)
			{
				// Corps non créé (lazy) : enfile pour création/activation progressive.
				if (!data.EstEnFileSolidification)
				{
					AjouterEnFileSolidification(data);
				}
			}
			return transitions < limite;
		}

		// PASSAGE A (priorité sécurité): réveille d'abord le rayon proche du joueur.
		int rayonReveil = Mathf.Max(1, RayonDormancePhysique);
		for (int dx = -rayonReveil; dx <= rayonReveil; dx++)
		{
			for (int dz = -rayonReveil; dz <= rayonReveil; dz++)
			{
				if (transitions >= limite) return;
				var coord = new Vector2I(obsChunkX + dx, obsChunkZ + dz);
				if (!_chunksData.TryGetValue(coord, out var data)) continue;

				if (data.Dormant)
				{
					if (!BasculerDormanceChunk(data, false)) return;
				}
				else if (!data.PhysicsBodyRID.IsValid && !data.EstEnFileSolidification)
				{
					// Garantit qu'un chunk proche sans body est solidifié rapidement.
					RetirerDeFileSolidification(data);
					EnfilerSolidificationUrgenteUnique(data);
					data.EstEnFileSolidification = true;
				}
			}
		}

		// PASSAGE B (secondaire): endort le lointain avec le budget restant.
		AssurerCacheCoordsChunks();
		int total = _cacheCoordsChunks.Count;
		if (total == 0) return;
		int evaluations = 0;
		int maxEvaluations = Mathf.Max(limite * 4, 96);
		if (_niveauUrgencePerf >= 2)
			maxEvaluations = Mathf.Max(limite * 2, 56);
		else if (_niveauUrgencePerf == 1)
			maxEvaluations = Mathf.Max(limite * 3, 72);
		if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 55f)
			maxEvaluations = Mathf.Max(48, Mathf.RoundToInt(maxEvaluations * 0.75f));
		while (evaluations < maxEvaluations && transitions < limite && total > 0)
		{
			if (_indexDormanceScan >= total) _indexDormanceScan = 0;
			Vector2I coord = _cacheCoordsChunks[_indexDormanceScan];
			_indexDormanceScan++;
			evaluations++;
			if (!_chunksData.TryGetValue(coord, out var data)) continue;
			int dx = Mathf.Abs(data.Coordonnees.X - obsChunkX);
			int dz = Mathf.Abs(data.Coordonnees.Y - obsChunkZ);
			bool doitDormir = dx > RayonDormancePhysique || dz > RayonDormancePhysique;
			if (!doitDormir || data.Dormant) continue;
			if (!BasculerDormanceChunk(data, true)) return;
		}
	}

	private void DemanderChunk(Vector2I coord)
	{
		_enregistrerDemandeChunk?.Invoke(coord);
	}

	/// <summary>
	/// Émet IMMÉDIATEMENT les requêtes pour les chunks manquants dans un petit rayon autour du joueur.
	/// Appelée même quand le budget frame est dépassé pour éviter les chutes dans le vide.
	/// </summary>
	private void GarantirRequetesChunksProcheJoueur(Vector3 positionObservation, Vector2I chunkObservationActuel)
	{
		// Rayon minimal : couvre au moins le RayonGrilleMinSpawnPret + anticipation courte dans la direction de déplacement.
		int rayonMin = Mathf.Clamp(RayonGrilleMinSpawnPret + 1, 1, RayonDormancePhysique);
		int budgetRequetesForce = 6; // jamais plus de 6 par frame : micro-burst contrôlé.
		int emises = 0;
		for (int dx = -rayonMin; dx <= rayonMin && emises < budgetRequetesForce; dx++)
		{
			for (int dz = -rayonMin; dz <= rayonMin && emises < budgetRequetesForce; dz++)
			{
				Vector2I cible = new Vector2I(chunkObservationActuel.X + dx, chunkObservationActuel.Y + dz);
				if (_chunksData.ContainsKey(cible)) continue;
				DemanderChunk(cible);
				emises++;
			}
		}
		// Anticipation chute : si le joueur tombe, pousser aussi le chunk sous sa trajectoire.
		if (_joueur != null && _joueur.Velocity.LengthSquared() > 1f && emises < budgetRequetesForce)
		{
			Vector3 cibleAnticipee = positionObservation + _joueur.Velocity.Normalized() * TailleChunk;
			Vector2I chunkAnticipe = Gestionnaire_Monde.WorldToChunkCoord(cibleAnticipee, TailleChunk);
			if (!_chunksData.ContainsKey(chunkAnticipe))
			{
				DemanderChunk(chunkAnticipe);
			}
		}
	}

	/// <summary>Interroge la densité à une position globale (chunk en RAM uniquement). Plus utilisé pour Marching Cubes (rembourrage 17³).</summary>
	public (float valeur, bool trouve) ObtenirDensiteGlobaleEx(Vector3I posGlobale)
	{
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobale.X, posGlobale.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		if (!_chunksData.TryGetValue(c, out var data)) return (-10f, false);
		return (data.ObtenirDensiteLocale(lx, posGlobale.Y, lz), true);
	}

	/// <summary>Vrai si une grille réduite sous les pieds a ses collisions actives (le rayon complet de dormance se remplit ensuite en jeu).</summary>
	public bool ChunkSousPiedsAPret()
	{
		if (_joueur == null) return false;
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
		int rg = Mathf.Clamp(RayonGrilleMinSpawnPret, 0, RayonDormancePhysique);
		for (int dx = -rg; dx <= rg; dx++)
			for (int dz = -rg; dz <= rg; dz++)
			{
				var v = new Vector2I(c.X + dx, c.Y + dz);
				if (!_chunksData.TryGetValue(v, out var data)) return false;
				if (!data.PhysicsBodyRID.IsValid || data.Dormant || data.EstEnFileSolidification) return false;
			}
		return true;
	}

	/// <summary>Vrai si le chunk a une collision active (body valide, non dormant, hors file de solidification).</summary>
	public bool ChunkCollisionActive(Vector2I coord)
	{
		if (!_chunksData.TryGetValue(coord, out var data)) return false;
		return data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification;
	}

	/// <summary>Vrai si le chunk sous les pieds et ses 4 voisins cardinaux ont une collision active (évite fissures de bord au démarrage).</summary>
	public bool ChunkSousPiedsEtVoisinsCardinauxPrets()
	{
		if (_joueur == null) return false;
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
		if (!ChunkCollisionActive(c)) return false;
		if (!ChunkCollisionActive(new Vector2I(c.X - 1, c.Y))) return false;
		if (!ChunkCollisionActive(new Vector2I(c.X + 1, c.Y))) return false;
		if (!ChunkCollisionActive(new Vector2I(c.X, c.Y - 1))) return false;
		if (!ChunkCollisionActive(new Vector2I(c.X, c.Y + 1))) return false;
		return true;
	}

	/// <summary>Vrai si la collision terrain est active autour d'un point monde (rayon en chunks).</summary>
	public bool CollisionTerrainActiveAutourPoint(Vector3 pointMonde, int rayonChunks = 0)
	{
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pointMonde, TailleChunk);
		int rayon = Mathf.Clamp(rayonChunks, 0, 2);
		for (int dx = -rayon; dx <= rayon; dx++)
			for (int dz = -rayon; dz <= rayon; dz++)
				if (!ChunkCollisionActive(new Vector2I(c.X + dx, c.Y + dz)))
					return false;
		return true;
	}
}
