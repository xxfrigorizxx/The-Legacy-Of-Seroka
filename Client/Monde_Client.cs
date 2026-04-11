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
	[Export] public int MaxChunksParFrame = 12;
	/// <summary>Nombre d'entrées inspectées max pour choisir un job maths (évite un scan O(n) complet à chaque worker).</summary>
	[Export] public int FenetreSelectionTravailMaths = 48;
	[Export] public int RayonInitialRequetesChunks = 8;
	[Export] public float IntervalleExpansionRequetesSec = 0.35f;
	[Export] public int SeuilBacklogHaut = 36;
	[Export] public int SeuilBacklogBas = 12;
	[Export] public float IntervalleProgressionForceeRayonSec = 1.6f;
	[Export] public bool ModeAutoDiagnosticAdaptatif = true;
	[Export] public int FpsCibleAutoDiagnostic = 60;
	[Export] public float RatioChargeMinimumAuto = 0.28f;
	[Export] public bool ActiverAntiSpikeFrameTime = true;
	[Export] public float SeuilSpikeFrameMs = 22f;
	[Export] public float DureeFreinSpikeSec = 0.35f;
	[Export] public bool ActiverHorizonLod = false;
	[Export] public int RayonHorizonChunks = 72;
	[Export] public float PasHorizonMetres = 20f;
	[Export] public float FrequenceMajHorizonSec = 1.2f;
	[Export] public bool ActiverCullingCameraChunks = true;
	[Export] public float AngleCullingCameraDeg = 135f;
	[Export] public int MargeChunksToujoursVisibles = 12;
	[Export] public int MaxBasculesCullingParPasse = 96;
	[Export] public int MaxChunksEvaluesCullingParPasse = 240;
	[Export] public float IntervalleDormanceSec = 0.06f;
	/// <summary>Rayon (en chunks) autour du joueur où les collisions sont actives. Tout dans ce rayon doit être dynamique (réveil immédiat). Au-delà, physique en dormance. 8 chunks ≈ 128 m (évite trous de collision en bordure).</summary>
	[Export] public int RayonDormancePhysique = 8;
	/// <summary>Demi-côté (chunks) pour lever l’overlay « Chargement du monde » : 2 = grille 5×5. Ne pas exiger tout le rayon de dormance (17×17) au démarrage sinon chargement quasi infini.</summary>
	[Export] public int RayonGrilleMinSpawnPret = 2;
	/// <summary>Chunks demandés en plus du rayon physique (file prioritaire). Le sol doit être chargé avant que tu n’entres dans la grille ChunkSousPiedsAPret.</summary>
	[Export] public int MargePreloadChunks = 4;
	/// <summary>Anticipation du déplacement (s) : une 2ᵉ zone de priorité autour de la position future pour marches longues dans une direction.</summary>
	[Export] public float SecondesAnticipationChargement = 2.5f;
	[Export] public float IntervalleRafraichissementRadarImmobile = 0.55f;
	/// <summary>Intégrations mesh/collision par frame quand le spawn est déjà prêt (exploration). Augmente si le sol met du temps à « se réveiller ».</summary>
	[Export] public int MaxIntegrationsParFrameExploration = 4;
	/// <summary>Intégrations mesh/collision par frame pendant le chargement initial (anti-pic CPU/GPU).</summary>
	[Export] public int MaxIntegrationsParFrameChargement = 8;
	/// <summary>Budget de vertices intégrés par frame (exploration). Lisse l'arrivée des triangles.</summary>
	[Export] public int BudgetVerticesIntegrationParFrameExploration = 130000;
	/// <summary>Budget de vertices intégrés par frame au chargement initial (plus généreux).</summary>
	[Export] public int BudgetVerticesIntegrationParFrameChargement = 190000;
	/// <summary>Solidifications BodySetSpace par frame en exploration (hors chargement initial).</summary>
	[Export] public int MaxSolidificationsParFrameExploration = 10;
	/// <summary>Nombre max de chunks de flore (gazon/buissons) construits par frame en exploration.</summary>
	[Export] public int MaxFloreParFrameExploration = 1;
	/// <summary>Nombre max de chunks de flore construits par frame pendant le chargement initial.</summary>
	[Export] public int MaxFloreParFrameChargement = 1;

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
	[Export] public int MaxTravailleursCalcul = 8;

	/// <summary>Chunks au format Data-Oriented (RID). Plus de Node pour le terrain.</summary>
	private Dictionary<Vector2I, ChunkData> _chunksData = new Dictionary<Vector2I, ChunkData>();
	/// <summary>File d'attente de solidification physique : un chunk par frame pour éviter les pics PhysicsServer3D (dilution physique).</summary>
	private List<ChunkData> _fileAttenteSolidification = new List<ChunkData>();

	private List<Vector2I> _chunksACharger = new List<Vector2I>();
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
	private const int MaxAjoutsRadarParPasse = 1400;
	private float _fpsMoyenneAuto = 60f;
	private float _ratioChargeAuto = 1f;
	private int _maxAjoutsRadarParPasseDyn = MaxAjoutsRadarParPasse;
	private int _maxRequetesDyn = 12;
	private int _maxTravailleursDyn = 8;
	private int _maxTransitionsDormanceDyn = 64;
	private int _maxBasculesCullingDyn = 96;
	private float _intervalleCullingDyn = 0.03f;
	private float _intervalleRadarImmobileDyn = 0.55f;
	private float _facteurMouvementAuto = 1f;
	private float _timerFreinSpike = 0f;
	private Vector2I _obsChunkDormance = new Vector2I(-99999, -99999);
	private float _timerDormance = 0f;
	private readonly List<Vector2I> _cacheCoordsChunks = new List<Vector2I>();
	private int _cacheCoordsChunksCount = -1;
	private int _indexCullingScan = 0;
	private int _indexDormanceScan = 0;
	private ulong _frameDernierRebuildCacheChunks = 0;

	private Action<Vector2I> _enregistrerDemandeChunk;
	private Action<Vector3, float, float> _demanderDestruction;
	private Action<Vector3, Vector3, float, int> _demanderCreation;
	private int _seedTerrain;

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
		int progressif = Mathf.Max(minRadar, _rayonRequetesActuel + 6);
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
			_maxAjoutsRadarParPasseDyn = MaxAjoutsRadarParPasse;
			_maxRequetesDyn = Mathf.Max(1, MaxChunksParFrame);
			_maxTravailleursDyn = Mathf.Clamp(MaxTravailleursCalcul, 2, 16);
			_maxTransitionsDormanceDyn = 64;
			_intervalleCullingDyn = 0.03f;
			_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile;
			_maxBasculesCullingDyn = Mathf.Max(8, MaxBasculesCullingParPasse);
			if (_timerFreinSpike > 0f)
			{
				_maxAjoutsRadarParPasseDyn = Mathf.Max(220, Mathf.RoundToInt(_maxAjoutsRadarParPasseDyn * 0.72f));
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

		float cible = Mathf.Clamp(FpsCibleAutoDiagnostic, 35, 240);
		float ratio = Mathf.Clamp(_fpsMoyenneAuto / cible, RatioChargeMinimumAuto, 1.15f);
		if (_fpsMoyenneAuto < 35f) ratio *= 0.82f;
		else if (_fpsMoyenneAuto < 45f) ratio *= 0.90f;
		_ratioChargeAuto = Mathf.Clamp(ratio, RatioChargeMinimumAuto, 1.1f);

		float vitesseXZ = 0f;
		if (_joueur != null)
		{
			Vector3 vel = _joueur.Velocity;
			vitesseXZ = new Vector2(vel.X, vel.Z).Length();
		}
		float tMouvement = Mathf.Clamp((vitesseXZ - 0.8f) / 5.5f, 0f, 1f);
		_facteurMouvementAuto = Mathf.Lerp(1f, 0.62f, tMouvement);
		float ratioStable = Mathf.Clamp(_ratioChargeAuto * _facteurMouvementAuto, RatioChargeMinimumAuto, 1.05f);
		if (_timerFreinSpike > 0f)
			ratioStable = Mathf.Clamp(ratioStable * 0.68f, RatioChargeMinimumAuto, 1.05f);

		_maxAjoutsRadarParPasseDyn = Mathf.Clamp(Mathf.RoundToInt(MaxAjoutsRadarParPasse * ratioStable), 220, MaxAjoutsRadarParPasse);
		_maxRequetesDyn = Mathf.Clamp(Mathf.RoundToInt(MaxChunksParFrame * Mathf.Lerp(0.55f, 1.45f, ratioStable)), 2, 56);
		_maxTravailleursDyn = Mathf.Clamp(Mathf.RoundToInt(MaxTravailleursCalcul * Mathf.Lerp(0.6f, 1.2f, ratioStable)), 2, 16);
		_maxTransitionsDormanceDyn = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(14f, 96f, ratioStable)), 12, 120);
		_intervalleCullingDyn = Mathf.Lerp(0.06f, 0.018f, ratioStable);
		_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile * Mathf.Lerp(1.6f, 0.78f, ratioStable);
		_maxBasculesCullingDyn = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(18f, Mathf.Max(18, MaxBasculesCullingParPasse), ratioStable)), 12, Mathf.Max(12, MaxBasculesCullingParPasse));
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

		// 1. Fusion des payloads en un seul ArrayMesh (terrain)
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var p in payloads)
		{
			if (p?.SommetsVisuels == null || p.SommetsVisuels.Length == 0) continue;
			for (int i = 0; i < p.SommetsVisuels.Length; i++)
			{
				st.SetNormal(p.NormalsVisuels != null && i < p.NormalsVisuels.Length ? p.NormalsVisuels[i] : Vector3.Up);
				st.SetColor(p.CouleursVisuels != null && i < p.CouleursVisuels.Length ? p.CouleursVisuels[i] : Colors.White);
				st.AddVertex(p.SommetsVisuels[i]);
			}
		}
		st.GenerateNormals();
		ArrayMesh mergedMesh = st.Commit();
		if (mergedMesh.GetSurfaceCount() == 0) return;

		Material matTerrain = MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
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
		EnfilerFloreChunk(data, ObtenirPositionObservation());

		// Physique lazy: créer la hitbox uniquement près du joueur, puis activer dans l'espace.
		if (_joueur != null)
		{
			Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(ObtenirPositionObservation(), TailleChunk);
			int dx = Mathf.Abs(data.Coordonnees.X - cJoueur.X);
			int dz = Mathf.Abs(data.Coordonnees.Y - cJoueur.Y);
			if (dx <= RayonDormancePhysique && dz <= RayonDormancePhysique)
			{
				AssurerCorpsPhysiqueChunk(data);
				if (data.PhysicsBodyRID.IsValid)
				{
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, world.Space);
					data.EstEnFileSolidification = false;
				}
			}
			else if (!data.EstEnFileSolidification)
			{
				_fileAttenteSolidification.Add(data);
				data.EstEnFileSolidification = true;
			}
		}

		// 4. Eau : fusion des SommetsEau/NormalsEau de toutes les sections, même transform que le terrain
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
		if (meshEau.GetFaces().Length > 0)
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

	private const int MaxMeshesParFrameVisuelles = 4;
	private const int MaxMeshesParFrameModification = 16;
	private float _tempsDepuisNettoyage;
	private const float IntervalleNettoyageChunks = 1.5f;

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInsideTree()) return; // GARROT SPATIAL : pas de manipulation de chunks si l'arbre s'effondre.
		float dt = (float)delta;
		MettreAJourAutoDiagnostic(dt);
		int backlogCharge = CompterBacklog();
		float facteurAntiSpikeBacklog = 1f;
		if (backlogCharge > SeuilBacklogHaut) facteurAntiSpikeBacklog *= 0.82f;
		if (backlogCharge > SeuilBacklogHaut + 28) facteurAntiSpikeBacklog *= 0.72f;
		if (backlogCharge > SeuilBacklogHaut + 64) facteurAntiSpikeBacklog *= 0.62f;

		// 2) Intégrations : chargement initial agressif ; exploration : plusieurs par frame pour suivre un monde infini.
		bool enChargement = !ChunkSousPiedsAPret();
		int baseIntegrations = enChargement
			? Mathf.Max(1, MaxIntegrationsParFrameChargement)
			: Mathf.Max(1, MaxIntegrationsParFrameExploration);
		int maxIntegrations = Mathf.Clamp(Mathf.RoundToInt(baseIntegrations * Mathf.Lerp(0.62f, 1.2f, _ratioChargeAuto) * facteurAntiSpikeBacklog), 1, Mathf.Max(1, baseIntegrations + 2));
		int budgetVerticesBase = enChargement
			? Mathf.Max(25000, BudgetVerticesIntegrationParFrameChargement)
			: Mathf.Max(18000, BudgetVerticesIntegrationParFrameExploration);
		float ratioVertices = Mathf.Lerp(0.58f, 1.25f, _ratioChargeAuto) * facteurAntiSpikeBacklog;
		if (_timerFreinSpike > 0f) ratioVertices *= 0.70f;
		int budgetVerticesDyn = Mathf.Clamp(Mathf.RoundToInt(budgetVerticesBase * ratioVertices), 12000, Mathf.Max(12000, budgetVerticesBase + 70000));
		int integrations = 0;
		int verticesIntegres = 0;
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

		// 3) Solidification physique lissée : crée/active les collisions proches en budget fixe.
		if (_fileAttenteSolidification.Count > 0)
		{
			Vector2I coordObsSolidif = ObtenirCoordonneesChunkJoueur();
			int baseSolidifications = enChargement ? 10 : Mathf.Max(1, MaxSolidificationsParFrameExploration);
			int maxSolidifications = Mathf.Clamp(Mathf.RoundToInt(baseSolidifications * Mathf.Lerp(0.65f, 1.2f, _ratioChargeAuto) * facteurAntiSpikeBacklog), 1, Mathf.Max(1, baseSolidifications + 2));
			int efforts = 0;
			World3D w = GetWorld3D();
			while (_fileAttenteSolidification.Count > 0 && efforts < maxSolidifications)
			{
				int idxProche = 0;
				int dBest = int.MaxValue;
				for (int i = 0; i < _fileAttenteSolidification.Count; i++)
				{
					ChunkData c = _fileAttenteSolidification[i];
					int ddx = c.Coordonnees.X - coordObsSolidif.X;
					int ddz = c.Coordonnees.Y - coordObsSolidif.Y;
					int d2 = ddx * ddx + ddz * ddz;
					if (d2 < dBest) { dBest = d2; idxProche = i; }
				}
				ChunkData chunkASolidifier = _fileAttenteSolidification[idxProche];
				int dx = Mathf.Abs(chunkASolidifier.Coordonnees.X - coordObsSolidif.X);
				int dz = Mathf.Abs(chunkASolidifier.Coordonnees.Y - coordObsSolidif.Y);
				if (dx <= RayonDormancePhysique && dz <= RayonDormancePhysique && w != null)
				{
					AssurerCorpsPhysiqueChunk(chunkASolidifier);
					if (chunkASolidifier.PhysicsBodyRID.IsValid)
					{
						_fileAttenteSolidification.RemoveAt(idxProche);
						chunkASolidifier.EstEnFileSolidification = false;
						PhysicsServer3D.Singleton.BodySetSpace(chunkASolidifier.PhysicsBodyRID, w.Space);
					}
					else
					{
						_fileAttenteSolidification.RemoveAt(idxProche);
						_fileAttenteSolidification.Add(chunkASolidifier);
					}
				}
				else
				{
					_fileAttenteSolidification.RemoveAt(idxProche);
					_fileAttenteSolidification.Add(chunkASolidifier);
				}
				efforts++;
			}
		}

		// FORGE RESTREINTE : lancer au plus MaxTravailleurs calculs en arrière-plan (tri par distance au joueur).
		Vector2I obsChunk = ObtenirCoordonneesChunkJoueur();
		int maxTravailleurs = _maxTravailleursDyn;
		while (Thread.VolatileRead(ref _chunksEnCoursDeCalcul) < maxTravailleurs)
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
		}

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

		// 2. Tâches de fond : dépiler l'affichage des nouveaux Chunks
		int actionsVisuelles = 0;
		while (actionsVisuelles < MaxMeshesParFrameVisuelles && _misesAJourUrgentes.TryDequeue(out var urgente))
		{
			try { urgente.Invoke(); } catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
			actionsVisuelles++;
		}
		while (actionsVisuelles < MaxMeshesParFrameVisuelles && _misesAJourMainThread.TryDequeue(out var action))
		{
			try { action.Invoke(); } catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
			actionsVisuelles++;
		}

		// Position d'observation : Caméra Active (caméra libre) ou corps du joueur — le verrou se base sur la caméra !
		Camera3D cameraActive = GetViewport()?.GetCamera3D();
		Vector3 positionObservation = cameraActive != null ? cameraActive.GlobalPosition : (_joueur?.GlobalPosition ?? Vector3.Zero);
		Vector3 directionObservation = cameraActive != null ? (-cameraActive.GlobalTransform.Basis.Z).Normalized() :
			(_joueur != null ? (-_joueur.GlobalTransform.Basis.Z).Normalized() : Vector3.Forward);
		Vector2I chunkObservationActuel = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		bool chunkObservationChange = chunkObservationActuel != _obsChunkDormance;
		_obsChunkDormance = chunkObservationActuel;
		AjusterFenetreRequetes(dt);
		MettreAJourHorizonLointain(positionObservation, dt);
		AppliquerCullingCameraChunks(positionObservation, directionObservation, dt);
		if (chunkObservationChange) _timerDormance = 0f;
		_timerDormance -= dt;
		if (_timerDormance <= 0f)
		{
			ActualiserDormanceChunks(_obsChunkDormance.X, _obsChunkDormance.Y, _maxTransitionsDormanceDyn);
			_timerDormance = Mathf.Clamp(IntervalleDormanceSec, 0.02f, 0.25f);
		}

		if (chunkObservationActuel != _ancienChunkJoueur)
		{
			_ancienChunkJoueur = chunkObservationActuel;
			ActualiserVisibiliteEtTriChunks(positionObservation);
			_derniereDirectionRadar = directionObservation;
			_timerRafraichissementRadarImmobile = _intervalleRadarImmobileDyn;
		}
		else
		{
			// Reste immobile: continue de préparer la map par vagues.
			_timerRafraichissementRadarImmobile -= dt;
			float dot = _derniereDirectionRadar.Normalized().Dot(directionObservation.Normalized());
			bool rotationImportante = dot < 0.86f;
			if ((_timerRafraichissementRadarImmobile <= 0f || rotationImportante) && !_radarEnCours)
			{
				ActualiserVisibiliteEtTriChunks(positionObservation);
				_derniereDirectionRadar = directionObservation;
				_timerRafraichissementRadarImmobile = rotationImportante
					? Mathf.Max(0.12f, _intervalleRadarImmobileDyn * 0.45f)
					: _intervalleRadarImmobileDyn;
			}
		}

		// Etale la flore sur plusieurs frames (supprime les gros spikes du premier chargement massif).
		int budgetFlore = enChargement
			? Mathf.Max(1, MaxFloreParFrameChargement)
			: Mathf.Max(1, MaxFloreParFrameExploration);
		TraiterFloreDifferee(positionObservation, budgetFlore);

		// Priorité : couvrir RayonDormancePhysique + marge (l’ancien 9×9 était trop petit vs grille 17×17 pour R=8).
		Vector2I chunkPieds = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		int rayonPriorite = RayonDormancePhysique + Mathf.Max(0, MargePreloadChunks);
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
		if (_joueur != null && SecondesAnticipationChargement > 0.01f)
		{
			Vector3 vel = _joueur.Velocity;
			Vector3 decalAnticipation = new Vector3(vel.X, 0f, vel.Z) * SecondesAnticipationChargement;
			if (decalAnticipation.LengthSquared() > 1f)
			{
				Vector3 posFutur = positionObservation + decalAnticipation;
				Vector2I chunkFutur = Gestionnaire_Monde.WorldToChunkCoord(posFutur, TailleChunk);
				int rayonAvant = Mathf.Max(RayonDormancePhysique + 1, rayonPriorite - 1);
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
		if (backlog >= SeuilBacklogHaut) nbRequetes = Mathf.Max(1, nbRequetes / 3);
		else if (backlog >= SeuilBacklogBas) nbRequetes = Mathf.Max(1, nbRequetes / 2);
		for (int n = 0; n < nbRequetes && _chunksACharger.Count > 0; n++)
		{
			Vector2I chunkCible = ExtraireChunkLePlusProche(_chunksACharger, positionObservation, directionObservation);
			float distCarree = DistanceCarreeAuJoueur(chunkCible, positionObservation);
			float rayonMaxCarre = (_rayonRequetesActuel + 1) * (_rayonRequetesActuel + 1);
			if (distCarree > rayonMaxCarre)
				continue;
			DemanderChunk(chunkCible);
		}

		_tempsDepuisNettoyage += dt;
		if (_tempsDepuisNettoyage >= IntervalleNettoyageChunks)
		{
			_tempsDepuisNettoyage = 0f;
			NettoyerChunksObsoles(positionObservation);
		}
	}

	private void AssurerCorpsPhysiqueChunk(ChunkData data)
	{
		if (data == null || data.PhysicsBodyRID.IsValid || data._meshRef == null) return;
		World3D world = GetWorld3D();
		if (world == null) return;
		Vector3[] faces = data._meshRef.GetFaces();
		if (faces == null || faces.Length == 0) return;

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

		_timerMajHorizon = Mathf.Max(0.4f, FrequenceMajHorizonSec);
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
		int chunksAEvaluer = Mathf.Clamp(MaxChunksEvaluesCullingParPasse, 32, 4000);
		if (_timerFreinSpike > 0f) chunksAEvaluer = Mathf.Max(24, Mathf.RoundToInt(chunksAEvaluer * 0.55f));

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
		if (data._nodeFlore != null || data.InventaireFlore == null || data.InventaireFlore.Count == 0) return;
		var node = Chunk_Client.CreerNoeudFlorePourChunkData(data, positionObservation, TailleChunk);
		if (node == null) return;
		node.Visible = data.CullingVisible;
		AddChild(node);
		data._nodeFlore = node;
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
		int rayonDetail = RayonChargementChunksActif();
		if (_rayonRequetesActuel <= 0) _rayonRequetesActuel = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		_rayonRequetesActuel = Mathf.Clamp(_rayonRequetesActuel, Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks), rayonDetail);

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
			_rayonRequetesActuel = Mathf.Min(rayonDetail, _rayonRequetesActuel + pas);
			_timerExpansionRequetes = Mathf.Max(0.1f, IntervalleExpansionRequetesSec);
		}

		// Même sous charge, le rayon avance lentement pour éviter un "blocage complet" du chargement lointain.
		if (_timerProgressionForceeRayon <= 0f && _rayonRequetesActuel < rayonDetail)
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
	private void NettoyerChunksObsoles(Vector3 positionObservation)
	{
		int rayonDetail = RayonChargementChunksActif();
		float seuilCarree = (rayonDetail + 2) * (rayonDetail + 2);
		_chunksATuerTemp.Clear();
		foreach (var kv in _chunksData)
		{
			if (DistanceCarreeAuJoueur(kv.Key, positionObservation) > seuilCarree)
				_chunksATuerTemp.Add(kv.Key);
		}
		foreach (Vector2I coord in _chunksATuerTemp)
		{
			if (_chunksData.TryGetValue(coord, out var data))
			{
				_chunksData.Remove(coord);
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
		for (int i = 0; i < liste.Count; i++)
		{
			Vector2 posChunk = new Vector2(liste[i].X, liste[i].Y);
			Vector2 to = posChunk - posObsV2;
			float dist = to.LengthSquared();
			float score = dist;
			float d = Mathf.Sqrt(Mathf.Max(0.0001f, dist));
			if (d > rayonNear)
			{
				Vector3 dir = new Vector3(to.X / d, 0f, to.Y / d);
				float dot = directionObservation.Dot(dir);
				if (dot < 0f) score += (1f - dot) * 200f;
			}
			if (score < scoreMin)
			{
				scoreMin = score;
				chunkCible = liste[i];
				indexASupprimer = i;
			}
		}
		liste.RemoveAt(indexASupprimer);
		return chunkCible;
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
		Camera3D cam = GetViewport()?.GetCamera3D();
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
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		int cjX = Gestionnaire_Monde.WorldToChunkCoord(positionObservation.X, positionObservation.Z, TailleChunk).X;
		int cjZ = Gestionnaire_Monde.WorldToChunkCoord(positionObservation.X, positionObservation.Z, TailleChunk).Y;
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

			// Tri radial strict : distance au carré depuis l'épicentre (évite racine carrée)
			Vector2 posObs = posObsV2;
			copieChunksACharger.Sort((a, b) =>
			{
				float da = new Vector2(a.X, a.Y).DistanceSquaredTo(posObs);
				float db = new Vector2(b.X, b.Y).DistanceSquaredTo(posObs);
				return da.CompareTo(db);
			});

			Callable.From(() => AppliquerNouveauTriRadar(copieChunksACharger.ToArray())).CallDeferred();
		});
	}

	private void AppliquerNouveauTriRadar(Vector2I[] nouvelleListeTriee)
	{
		if (nouvelleListeTriee == null || nouvelleListeTriee.Length == 0)
		{
			_chunksACharger.Clear();
			_radarEnCours = false;
			return;
		}
		int rayonRadar = RayonRadarPreparationActif();
		int cap = Mathf.Clamp((2 * rayonRadar + 1) * (2 * rayonRadar + 1), 256, 16000);
		int n = Mathf.Min(cap, nouvelleListeTriee.Length);
		var compacte = new List<Vector2I>(n);
		for (int i = 0; i < n; i++) compacte.Add(nouvelleListeTriee[i]);
		_chunksACharger = compacte;
		_radarEnCours = false;
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
								_fileAttenteSolidification.Remove(d);
								d.EstEnFileSolidification = false;
							}
						}
					}
					else if (!d.EstEnFileSolidification)
					{
						_fileAttenteSolidification.Add(d);
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
						_fileAttenteSolidification.Remove(data);
						data.EstEnFileSolidification = false;
					}
				}
				else
				{
					// Réveil dynamique : activer les collisions tout de suite dans le rayon (pas de file).
					PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, space);
					transitions++;
					if (data.EstEnFileSolidification)
					{
						_fileAttenteSolidification.Remove(data);
						data.EstEnFileSolidification = false;
					}
				}
			}
			else if (!dormantCible)
			{
				// Corps non créé (lazy) : enfile pour création/activation progressive.
				if (!data.EstEnFileSolidification)
				{
					_fileAttenteSolidification.Add(data);
					data.EstEnFileSolidification = true;
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
					_fileAttenteSolidification.Add(data);
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
