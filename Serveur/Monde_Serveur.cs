using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FileAccess = Godot.FileAccess;

/// <summary>Détient les chunks serveur (données voxel), la génération, la simulation d'eau. Aucun MeshInstance3D.</summary>
public partial class Monde_Serveur : Node
{
	[Export] public int TailleChunk = 16;
	[Export] public int HauteurMax = 720;  // Montagnes jusqu'à 700
	[Export] public int SeedTerrain = 19847;
	[Export] public int RayonMondeChunks = 1000;
	[Export] public int RenderDistance = 200;
	[Export] public string NomDimension = "ARAPA";
	[Export] public bool ActiverGenerationAbysse = false;

	/// <summary>Matériel du terrain pour les débris (BlocChutant). Assigné par Gestionnaire_Monde.</summary>
	public Material MaterielTerrain;

	/// <summary>Fuseau horaire de la dimension en heures. Monde 1 = 0, Monde 2 = +6, Monde 3 = +12, Monde 4 = +18.</summary>
	[Export] public double FuseauHoraireHeures = 0.0;

	private Dictionary<Vector2I, Chunk_Serveur> _chunks = new Dictionary<Vector2I, Chunk_Serveur>();
	private readonly Dictionary<int, Dictionary<Vector2I, Chunk_Serveur>> _chunksAbysseParStage2D = new Dictionary<int, Dictionary<Vector2I, Chunk_Serveur>>();
	private Queue<Vector3I> _fileEau = new Queue<Vector3I>();
	private HashSet<Vector3I> _eauActive = new HashSet<Vector3I>();
	private readonly Dictionary<Vector3I, (Vector3I retourInterdit, int tickExpiration)> _antiRetourEau = new Dictionary<Vector3I, (Vector3I, int)>();
	private int _tickEauCourant;
	private const int MaxEauParTick = 24;
	private const int DureeBlocageRetourEauTicks = 5;
	private static readonly Vector3I[] DirEauHoriz = { new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, -1), new Vector3I(0, 0, 1) };
	private static readonly Vector3I[] DirVoisins = { new Vector3I(0, 1, 0), new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, -1), new Vector3I(0, 0, 1) };
	private static readonly Vector3I[] DirReveil = { new Vector3I(0, 1, 0), new Vector3I(0, -1, 0), new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0), new Vector3I(0, 0, 1), new Vector3I(0, 0, -1) };

	private Node _parentPourBlocsChutants;
	private Node _parentPourArbres;
	private Action<Vector2I, List<int>> _onChunkModifie;
	private Action<Vector2I, DonneesChunk> _onEnvoyerChunk;
	private Action<Vector2I, int, Dictionary<Vector3I, byte>> _onFloreModifie;
	private Action<Vector3I, byte> _onVoxelModifie;
	private Action<Vector2I> _onOrdonnerDestructionChunk;
	private Func<Vector3> _obtenirPositionJoueur;
	private Func<int> _obtenirDimensionActive;
	private int _dimensionServeurId = (int)DimensionJeu.Alpha;
	/// <summary>Dimension non visitée : pas de génération, eau, arbres ni décharge (chunks déjà sur disque).</summary>
	private bool _simulationSuspendue;
	private int _jourAbsoluMemorisePourArbresSuspension = -1;
	private long _unixMemorisePourArbresSuspension;
	private const int BudgetChunksDirtyAvantSuspension = 128;
	private readonly HashSet<long> _adminPeerIds = new HashSet<long>();
	private readonly Dictionary<long, bool> _modeCreatifParPeer = new Dictionary<long, bool>();
	private readonly Dictionary<long, bool> _noclipParPeer = new Dictionary<long, bool>();
	private const string CheminAdminWhitelist = "user://admin_whitelist.json";
	private const string PrefixeCommandeBootstrapAdmin = "/ADAMINISATATORA";

	private readonly List<DemandeChunk> _chunksEnAttenteEnvoi = new List<DemandeChunk>();
	private readonly List<Vector3I> _clesChunksAbysseARetirerTemp = new List<Vector3I>();
	private readonly HashSet<Vector3I> _demandesForceesSansPurge = new HashSet<Vector3I>();
	private readonly HashSet<Vector3I> _demandesEnAttenteSet = new HashSet<Vector3I>();
	private Queue<ColisChunk> _fileEnvoiReseau = new Queue<ColisChunk>();
	private readonly HashSet<Vector3I> _chunksEnCoursGeneration = new HashSet<Vector3I>();
	private int _chunksEnGenerationActive;
	private static readonly int MaxThreadsGeneration = 4;
	[Export] public int MultiplicateurCharge = 2; // Réduit la tempête de tâches pour éviter l'overload CPU.
	private int LancerMaxTaches => MaxThreadsGeneration * MultiplicateurCharge;
	[Export] public int MaxArbresSpawnParTick = 2;
	/// <summary>Budget CPU de spawn arbres par tick (ms). Sécurité anti micro-freeze MMO.</summary>
	[Export] public float BudgetMsSpawnArbresParTick = 0.90f;
	/// <summary>Budget CPU de spawn pierres par tick (ms). Sécurité anti micro-freeze MMO.</summary>
	[Export] public float BudgetMsSpawnPierresParTick = 0.70f;
	[Export] public bool ModeAntiMicroFreezeStrict = true;
	[Export] public float FacteurSpawnStrict = 0.72f;
	[Export] public int RayonSecuriteTerrainReveilPierres = 1;
	[Export] public bool ModeEssencesPartoutTemporaire = false;
	[Export] public float RatioJungleModeTest = 0.35f;
	/// <summary>Budget anti micro-freeze : limite de chunks workers intégrés par frame.</summary>
	[Export] public int MaxIntegrationsWorkersParTick = 2;
	/// <summary>Budget anti micro-freeze : limite de demandes chunks traitées par frame.</summary>
	[Export] public int MaxDemandesChunksParTick = 2;
	[Export] public int MaxDemandesChunksAbysseParTick = 4;
	[Export] public int MaxDemandesAbysseEnFile = 2400;
	/// <summary>Budget anti micro-freeze : limite de chargements disque synchrones par frame.</summary>
	private const int MaxChargesDisqueParTick = 1;
	[Export] public int MaxChunksEnvoiParTick = 8;
	[Export] public bool ActiverDiagnosticBaselineServeur = false;
	[Export(PropertyHint.Range, "0.5,20,0.1")] public float IntervalleDiagnosticBaselineServeurSec = 2.0f;
	private bool _modificationEnCours;
	private readonly object _verrouGeneration = new object();
	private readonly ConcurrentQueue<(Vector2I coord, int coordY, Chunk_Serveur chunk, DonneesChunk donnees)> _chunksGeneres = new ConcurrentQueue<(Vector2I, int, Chunk_Serveur, DonneesChunk)>();
	private float _cooldownDiagnosticBaselineServeur;

	private struct SnapshotTickServeur
	{
		public int IntegrationsWorkers;
		public int DemandesTraitees;
		public int ChargesDisque;
		public int EnvoisReseau;
		public int ArbresSpawns;
		public int PierresSpawns;
		public int FileDemandesRestantes;
		public int FileEnvoisRestants;
		public int FileEauRestante;
	}
	private ServerTickOrchestrator _serverTickOrchestrator;
	private ChunkPersistenceService _chunkPersistenceService;
	private FloraPersistenceService _floraPersistenceService;
	private ArbrePersistenceService _arbrePersistenceService;
	private PierrePersistenceService _pierrePersistenceService;
	private ChunkGenerationKernel _chunkGenerationKernel;
	private ChunkGenerationScheduler _chunkGenerationScheduler;
	private WaterSimulationService _waterSimulationService;
	private SpawnPipelineService _spawnPipelineService;

	private struct ColisChunk
	{
		public Vector2I Coord;
		public DonneesChunk Donnees;
	}

	private struct DemandeChunk
	{
		public Vector2I Coord;
		public int CoordY;
		public Vector3 Observation;
		public bool EstAbysse;

		public Vector3I Cle3D => new Vector3I(Coord.X, CoordY, Coord.Y);
	}

	/// <summary>Pierres chargées depuis disque → instanciation goutte-à-goutte (quand chunk dessiné à l'écran).</summary>
	private Queue<(Vector3 pos, int id, int indexCache, int indexChimique)> _filePierresAInstancier = new Queue<(Vector3, int, int, int)>();
	/// <summary>Chambre de stase : roches par coord de chunk. Aucune poussière avant que la croûte (chunk) soit scellée — libérées seulement à l'envoi du chunk.</summary>
	private Dictionary<Vector2I, List<(Vector3 pos, int id, int indexCache, int indexChimique)>> _rochesEnStase = new Dictionary<Vector2I, List<(Vector3, int, int, int)>>();
	/// <summary>Micro-dosage : au plus 3 cailloux par frame pour éviter pics CPU / sync BVH Jolt (AddChild lourd).</summary>
	private const int MaxPierresParFrame = 3;

	/// <summary>Pools de roches par taille (ID 10–14). Limite 50 par catégorie. Plus loin du joueur → formes plus cassées (2e moitié du cache).</summary>
	private Dictionary<int, List<RigidBody3D>> _poolsRochesParTaille = new Dictionary<int, List<RigidBody3D>>();
	private const int TaillePoolParType = 50;
	/// <summary>En deçà de cette distance au niveau d'eau (Y=103) : formes douces. Au-delà (hautes montagnes ou profondeur) : formes plus cassées.</summary>
	private const float SeuilDistanceEauFormesCassées = 25f;

	private float _tempsDepuisVerifDecharge;
	private const float IntervalleEvaluationTectonique = 0.5f;
	/// <summary>Tapis roulant décharge : au plus N chunks sauvegardés/déchargés par frame (évite lag).</summary>
	[Export] public int MaxChunksDechargeParTick = 2;
	[Export] public float BudgetMsDechargeParTick = 0.80f;
	private readonly Queue<Vector2I> _chunksEnAttenteDecharge = new Queue<Vector2I>();
	private readonly HashSet<Vector2I> _chunksEnAttenteDechargeSet = new HashSet<Vector2I>();
	private FastNoiseLite _noiseTemperatureArbres;
	private int _noiseTemperatureArbresSeed = int.MinValue;
	private FastNoiseLite _noiseHumiditeArbres;
	private int _noiseHumiditeArbresSeed = int.MinValue;
	private FastNoiseLite _noiseBiomeForetArbres;
	private int _noiseBiomeForetArbresSeed = int.MinValue;
	private readonly Queue<(Vector2I coord, Vector3 pos, int age, uint seed, byte indexBotanique, int joursRattrapage)> _fileSpawnArbres = new Queue<(Vector2I, Vector3, int, uint, byte, int)>();
	private const int MagicArbresV2 = 0x5A4B3251;
	private const int MagicArbresV3 = 0x5A4B3252;
	private const int MagicArbresV4 = 0x5A4B3253;
	private const int MagicArbresV5 = 0x5A4B3254;
	private const int MagicArbresV6 = 0x5A4B3255;
	private readonly List<Vector2I> _cycleAutosaveChunks = new List<Vector2I>();
	private int _indexCycleAutosaveChunks;
	private readonly List<Vector3I> _cycleAutosaveChunksAbysse = new List<Vector3I>();
	private int _indexCycleAutosaveChunksAbysse;
	private readonly Queue<Vector2I> _fileChunksDirtyAutosave = new Queue<Vector2I>();
	private readonly HashSet<Vector2I> _setChunksDirtyAutosave = new HashSet<Vector2I>();
	[Export] public int MultiplicateurScanDirtyAutosave = 6;

	/// <summary>
	/// Budget d’objets traités par tick (arbres, pierres, décharge chunks). Indépendant du FPS pour que le
	/// débit soit identique entre machines ; le plafond CPU par tick reste assuré par les budgets temps (µs)
	/// dans les boucles <c>while</c> qui consomment ces files.
	/// </summary>
	private int CalculerBudgetSpawnAdaptatif(int budgetBase)
	{
		return Mathf.Max(1, budgetBase);
	}

	/// <summary>
	/// Réduit le débit quand les files de spawn sont longues (et mode strict). Sans lecture du FPS : parité entre PC.
	/// </summary>
	private float CalculerFacteurPressionSpawn()
	{
		float facteur = 1f;

		int chargeObjets = _fileSpawnArbres.Count + _filePierresAInstancier.Count;
		if (chargeObjets > 220) facteur *= 0.84f;
		if (chargeObjets > 520) facteur *= 0.72f;
		if (chargeObjets > 900) facteur *= 0.62f;

		if (ModeAntiMicroFreezeStrict)
			facteur *= Mathf.Clamp(FacteurSpawnStrict, 0.45f, 1f);

		return Mathf.Clamp(facteur, 0.22f, 1f);
	}
	private const int PoolVariantesArbreParAge = 50;
	private const int PoolAgesPregenArbre = 5;
	private const int PoolEspecesArbre = 7; // chene, bouleau, pin, sapin, jungle, chene mort, bouleau mort
	private readonly uint[,,] _poolSeedsArbres = new uint[PoolEspecesArbre, PoolAgesPregenArbre, PoolVariantesArbreParAge];
	private int _seedPoolArbres = int.MinValue;

	public void Initialiser(Node parentPourBlocsChutants, Node parentPourArbres, Action<Vector2I, List<int>> onChunkModifie, Action<Vector2I, DonneesChunk> onEnvoyerChunk = null, Action<Vector2I, int, Dictionary<Vector3I, byte>> onFloreModifie = null, Action<Vector3I, byte> onVoxelModifie = null, Action<Vector2I> onOrdonnerDestructionChunk = null, Func<Vector3> obtenirPositionJoueur = null, Func<int> obtenirDimensionActive = null, int dimensionServeurId = (int)DimensionJeu.Alpha)
	{
		_parentPourBlocsChutants = parentPourBlocsChutants;
		_parentPourArbres = parentPourArbres;
		_onChunkModifie = onChunkModifie;
		_onEnvoyerChunk = onEnvoyerChunk;
		_onFloreModifie = onFloreModifie;
		_onVoxelModifie = onVoxelModifie;
		_onOrdonnerDestructionChunk = onOrdonnerDestructionChunk;
		_obtenirPositionJoueur = obtenirPositionJoueur;
		_obtenirDimensionActive = obtenirDimensionActive;
		_dimensionServeurId = dimensionServeurId;
		DirAccess.MakeDirRecursiveAbsolute(ObtenirDossierChunksRelatif());
		GD.Print($"ZERO-K : Dossier chunks actif = {ObtenirDossierChunksRelatif()}/ (lecture ET écriture) [{NomDimension}]");
		ChargerAdminWhitelist();
		AssurerPoolSeedsArbresPregen();
		CreerPoolsRochesParTaille();
		_chunkPersistenceService = new ChunkPersistenceService(this);
		_floraPersistenceService = new FloraPersistenceService(this);
		_arbrePersistenceService = new ArbrePersistenceService(this);
		_pierrePersistenceService = new PierrePersistenceService(this);
		_chunkGenerationKernel = new ChunkGenerationKernel(this);
		_chunkGenerationScheduler = new ChunkGenerationScheduler(this, _chunkGenerationKernel);
		_waterSimulationService = new WaterSimulationService(this);
		_spawnPipelineService = new SpawnPipelineService(this);
		_serverTickOrchestrator = new ServerTickOrchestrator(this);
	}

	private Vector3 InvokerPositionJoueurStreaming()
	{
		if (_obtenirPositionJoueur == null)
			return Vector3.Zero;
		try
		{
			return _obtenirPositionJoueur();
		}
		catch (ObjectDisposedException)
		{
			return Vector3.Zero;
		}
	}

	public bool EstSimulationSuspendue => _simulationSuspendue;

	/// <summary>Suspend ou reprend la simulation serveur de cette dimension (génération, eau, pierres, décharge).</summary>
	public void DefinirSimulationSuspendue(bool suspendue)
	{
		if (_simulationSuspendue == suspendue)
			return;

		if (suspendue)
		{
			int sauves = ForcerSauvegardeChunksDirty(BudgetChunksDirtyAvantSuspension);
			MemoriserHorlogePourRattrapageArbres();
			ViderFilesStreamingPourSuspension();
			GelCorpsDynamiquesDimension();
			_simulationSuspendue = true;
			SetPhysicsProcess(false);
			if (sauves > 0)
				GD.Print($"ZERO-K [{NomDimension}] : simulation suspendue ({sauves} chunk(s) modifié(s) gravé(s)).");
			else
				GD.Print($"ZERO-K [{NomDimension}] : simulation suspendue.");
		}
		else
		{
			_simulationSuspendue = false;
			RattraperArbresApresRepriseSimulation();
			SetPhysicsProcess(true);
			GD.Print($"ZERO-K [{NomDimension}] : simulation reprise.");
		}
	}

	private void MemoriserHorlogePourRattrapageArbres()
	{
		_jourAbsoluMemorisePourArbresSuspension = GameState.Instance != null ? GameState.Instance.JourAbsolu : 0;
		_unixMemorisePourArbresSuspension = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	/// <summary>Arbres encore en scène (chunk non déchargé) : même logique que <see cref="ChargerArbresChunk"/> au reload disque.</summary>
	private void RattraperArbresApresRepriseSimulation()
	{
		if (_parentPourArbres == null || _jourAbsoluMemorisePourArbresSuspension < 0)
			return;
		int jours = CalculerJoursRattrapageArbres(_jourAbsoluMemorisePourArbresSuspension, _unixMemorisePourArbresSuspension);
		_jourAbsoluMemorisePourArbresSuspension = -1;
		_unixMemorisePourArbresSuspension = 0L;
		if (jours <= 0)
			return;

		int count = 0;
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is not ArbreVivant arbre || n is not Node3D n3)
				continue;
			arbre.RattraperCroissance(jours, n3.GlobalPosition);
			count++;
		}
		if (count > 0)
			GD.Print($"ZERO-K [{NomDimension}] : rattrapage croissance arbres ({jours} j) sur {count} arbre(s) en scène.");
	}

	private void ViderFilesStreamingPourSuspension()
	{
		_chunksEnAttenteEnvoi.Clear();
		_demandesEnAttenteSet.Clear();
		_demandesForceesSansPurge.Clear();
		_fileEnvoiReseau.Clear();
	}

	private void GelCorpsDynamiquesDimension()
	{
		if (_parentPourBlocsChutants == null)
			return;
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			if (child is not RigidBody3D rb)
				continue;
			if (rb.HasMeta("DimensionId") && rb.GetMeta("DimensionId").AsInt32() != _dimensionServeurId)
				continue;
			else if (_dimensionServeurId != (int)DimensionJeu.Alpha && !ActiverGenerationAbysse)
				continue;

			int id = 0;
			if (rb is ItemPhysique item)
				id = item.ID_Objet;
			else if (rb.HasMeta("ID_Matiere"))
				id = rb.GetMeta("ID_Matiere").AsInt32();
			bool structureFixe = id == 200 || id == Joueur.IdObjetTableAnalyseTier1 || id == Joueur.IdObjetRackBatons
				|| id == Joueur.IdObjetRackBuches || id == Joueur.IdObjetCoffreBoisTier0;
			if (structureFixe)
				continue;
			rb.LinearVelocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.Sleeping = true;
			rb.Freeze = true;
		}
	}

	private void ChargerAdminWhitelist()
	{
		_adminPeerIds.Clear();
		if (!FileAccess.FileExists(CheminAdminWhitelist))
		{
			GD.Print($"ZERO-K ADMIN : fichier absent ({CheminAdminWhitelist}). Aucun admin persiste ; bootstrap via /ADAMINISATATORA <NomPersonnage>.");
			return;
		}

		try
		{
			using var file = FileAccess.Open(CheminAdminWhitelist, FileAccess.ModeFlags.Read);
			if (file == null) return;
			string contenu = file.GetAsText();
			if (string.IsNullOrWhiteSpace(contenu)) return;
			using var doc = JsonDocument.Parse(contenu);
			if (doc.RootElement.TryGetProperty("admin_peer_ids", out JsonElement admins) && admins.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement e in admins.EnumerateArray())
				{
					if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out long id) && id > 0)
						_adminPeerIds.Add(id);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K ADMIN : lecture whitelist impossible ({CheminAdminWhitelist}) -> {ex.Message}");
		}
	}

	/// <summary>Ajoute un peer admin (idempotent). Utilisé après bootstrap pour synchroniser toutes les dimensions.</summary>
	public void AjouterPeerAdmin(long peerId)
	{
		if (peerId > 0)
			_adminPeerIds.Add(peerId);
	}

	/// <summary>Persiste les IDs admin sur disque (la whitelist sert juste à se souvenir des peers déjà promus).</summary>
	public void PersisterWhitelistAdmin()
	{
		try
		{
			var liste = new List<long>(_adminPeerIds);
			liste.Sort();
			string json = JsonSerializer.Serialize(new
			{
				admin_peer_ids = liste
			});
			using var file = FileAccess.Open(CheminAdminWhitelist, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				GD.PrintErr($"ZERO-K ADMIN : impossible d’écrire {CheminAdminWhitelist}.");
				return;
			}
			file.StoreString(json);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K ADMIN : écriture whitelist impossible -> {ex.Message}");
		}
	}

	/// <summary>
	/// Traite <c>/ADAMINISATATORA &lt;NomPersonnage&gt;</c> : compare l’argument au nom du personnage donné à la
	/// création du monde (<see cref="GameState.NomPersonnageJoue"/>). Comparaison insensible à la casse et aux
	/// espaces de bord. Le secret historique (<c>bootstrap_secret</c>) n’est plus utilisé.
	/// </summary>
	/// <returns>Vrai si la chaîne est bien cette commande (succès ou échec).</returns>
	public bool EssayerBootstrapAdmin(long peerId, string commandeBrute, out bool succes, out string messageServeur)
	{
		succes = false;
		messageServeur = "";
		string commande = (commandeBrute ?? "").Trim();
		if (!commande.StartsWith(PrefixeCommandeBootstrapAdmin, StringComparison.OrdinalIgnoreCase))
			return false;

		string argumentNom = commande.Length > PrefixeCommandeBootstrapAdmin.Length
			? commande.Substring(PrefixeCommandeBootstrapAdmin.Length).Trim()
			: "";

		string nomPersonnageAttendu = (GameState.Instance?.NomPersonnageJoue ?? "").Trim();
		if (string.IsNullOrEmpty(nomPersonnageAttendu))
		{
			GD.PrintErr("ZERO-K ADMIN : NomPersonnageJoue indisponible (aucun monde chargé ?).");
			messageServeur = "Configuration administrateur indisponible.";
			return true;
		}

		if (string.IsNullOrEmpty(argumentNom))
		{
			messageServeur = "Refus : précisez le nom du personnage (/ADAMINISATATORA <NomPersonnage>).";
			return true;
		}

		if (!string.Equals(argumentNom, nomPersonnageAttendu, StringComparison.OrdinalIgnoreCase))
		{
			messageServeur = "Nom de personnage invalide.";
			return true;
		}

		succes = true;
		if (_adminPeerIds.Contains(peerId))
			messageServeur = "Droits administrateur déjà accordés.";
		else
		{
			AjouterPeerAdmin(peerId);
			messageServeur = $"Droits administrateur accordés à « {nomPersonnageAttendu} ».";
			GD.Print($"ZERO-K ADMIN : bootstrap réussi peer={peerId} nom='{nomPersonnageAttendu}'.");
		}
		return true;
	}

	public bool EstPeerAdmin(long peerId) => peerId > 0 && _adminPeerIds.Contains(peerId);

	public bool EstModeCreatifPeer(long peerId) =>
		_modeCreatifParPeer.TryGetValue(peerId, out bool actif) && actif;

	public bool EstNoclipPeer(long peerId) =>
		_noclipParPeer.TryGetValue(peerId, out bool actif) && actif;

	public bool EssayerTraiterCommandeAdmin(long peerId, string commandeBrute, out bool modeCreatif, out bool noclip, out string messageServeur)
	{
		modeCreatif = EstModeCreatifPeer(peerId);
		noclip = EstNoclipPeer(peerId);
		messageServeur = "Commande admin invalide.";

		string commande = (commandeBrute ?? "").Trim();
		if (string.IsNullOrEmpty(commande)) return false;
		if (!EstPeerAdmin(peerId))
		{
			GD.PrintErr($"ZERO-K ADMIN SECURITE : tentative non autorisée peer={peerId} cmd='{commande}'.");
			messageServeur = "Accès refusé: vous n'êtes pas admin.";
			return false;
		}

		string[] t = commande.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (t.Length != 3 || !string.Equals(t[0], "/MODUSA", StringComparison.OrdinalIgnoreCase))
			return false;
		if (string.Equals(t[1], "RUDI", StringComparison.OrdinalIgnoreCase))
		{
			if (!int.TryParse(t[2], out int niveauRudi) || (niveauRudi != 0 && niveauRudi != 1 && niveauRudi != 3))
				return false;

			bool creatifActif = niveauRudi != 0;
			bool noclipActif = niveauRudi == 3;
			_modeCreatifParPeer[peerId] = creatifActif;
			_noclipParPeer[peerId] = noclipActif;
			modeCreatif = creatifActif;
			noclip = noclipActif;
			if (niveauRudi == 0)
				messageServeur = "Mode creatif desactive.";
			else if (niveauRudi == 1)
				messageServeur = "Mode creatif active.";
			else
				messageServeur = "Mode creatif + noclip admin active.";
			GD.Print($"ZERO-K ADMIN : peer={peerId} -> RUDI={niveauRudi} (creatif={(modeCreatif ? 1 : 0)}, noclip={(noclip ? 1 : 0)}).");
			return true;
		}
		if (string.Equals(t[1], "NOCLIP", StringComparison.OrdinalIgnoreCase))
		{
			if (!int.TryParse(t[2], out int etatInt) || (etatInt != 0 && etatInt != 1))
				return false;
			bool etat = etatInt == 1;
			if (!EstModeCreatifPeer(peerId) && etat)
			{
				messageServeur = "Noclip nécessite d'abord /MODUSA RUDI 1.";
				return true;
			}
			_noclipParPeer[peerId] = etat;
			modeCreatif = EstModeCreatifPeer(peerId);
			noclip = etat && modeCreatif;
			messageServeur = noclip ? "Noclip admin active." : "Noclip admin desactive.";
			GD.Print($"ZERO-K ADMIN : peer={peerId} -> noclip={(noclip ? 1 : 0)}.");
			return true;
		}

		return false;
	}

	public bool EssayerConstruireSlotInjectionCreatif(long peerId, int id, int indexMorphologique, int indexChimique, int indexTaille, int indexBotanique, out SlotInventaire slot, out string messageServeur)
	{
		slot = new SlotInventaire();
		messageServeur = "Injection creatif refusée.";
		if (!EstPeerAdmin(peerId))
		{
			GD.PrintErr($"ZERO-K ADMIN SECURITE : injection non autorisée peer={peerId} id={id}.");
			messageServeur = "Accès refusé: admin requis.";
			return false;
		}
		if (!EstModeCreatifPeer(peerId))
		{
			messageServeur = "Mode creatif requis (/MODUSA RUDI 1).";
			return false;
		}
		if (id <= 0)
		{
			messageServeur = "ID objet invalide.";
			return false;
		}

		slot.ID = id;
		slot.IndexMorphologique = Mathf.Clamp(indexMorphologique, 0, 255);
		slot.IndexChimique = Mathf.Clamp(indexChimique, 0, Mathf.Max(0, ItemPhysique.TableGeologique.Length - 1));
		slot.IndexTaille = Mathf.Clamp(indexTaille, 0, 4);
		slot.IndexBotanique = (byte)Mathf.Clamp(indexBotanique, 0, 255);

		// Roches matières : la chimie est imposée par l'ID objet (40-51).
		if (ItemPhysique.EstIdRocheMatiere(id))
			slot.IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(id);

		Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slot);
		slot.Quantite = Mathf.Max(1, Joueur.ObtenirPileMax(slot));
		messageServeur = $"Injection autorisée: {Atlas_Matiere.ObtenirNomObjet(slot)} x{slot.Quantite}.";
		return true;
	}

	private static uint MelangerPool(uint x)
	{
		x ^= x >> 16;
		x *= 0x7FEB352Du;
		x ^= x >> 15;
		x *= 0x846CA68Bu;
		x ^= x >> 16;
		return x;
	}

	/// <summary>Construit le pool de variantes d'arbres (50 par essence x âge 1..5) à la création du monde.</summary>
	private void AssurerPoolSeedsArbresPregen()
	{
		if (_seedPoolArbres == SeedTerrain) return;
		for (int espece = 0; espece < PoolEspecesArbre; espece++)
		for (int ageIdx = 0; ageIdx < PoolAgesPregenArbre; ageIdx++)
		for (int variante = 0; variante < PoolVariantesArbreParAge; variante++)
		{
			uint baseH = (uint)(
				SeedTerrain * 2654435761u
				^ (uint)(espece * 374761393)
				^ (uint)((ageIdx + 1) * 668265263)
				^ (uint)(variante * 2246822519u));
			uint seed = MelangerPool(baseH);
			if (seed == 0u) seed = (uint)(12345 + espece * 97 + ageIdx * 31 + variante * 7);
			_poolSeedsArbres[espece, ageIdx, variante] = seed;
		}
		_seedPoolArbres = SeedTerrain;
	}

	/// <summary>Retourne une seed du pool pour les âges 1..5 (nouveaux chunks), sinon conserve la seed dynamique.</summary>
	private uint SelectionnerSeedArbreDepuisPool(byte indexBotanique, int age, int gx, int gz, uint seedOriginale)
	{
		if (age < 1 || age > PoolAgesPregenArbre) return seedOriginale;
		int espece = Mathf.Clamp(indexBotanique, 0, PoolEspecesArbre - 1);
		uint h = (uint)(gx * 73856093) ^ (uint)(gz * 19349663) ^ seedOriginale;
		int variante = (int)(h % PoolVariantesArbreParAge);
		return _poolSeedsArbres[espece, age - 1, variante];
	}

	// Fenêtre anti-doublon : empêche 3-4 "Râle d'Agonie" en cascade (save manuel + WMCloseRequest + _ExitTree parent + _ExitTree serveur).
	private ulong _derniereSauvegardeMondeEntierTickMs;
	private const ulong FenetreDedoublonageSauvegardeMs = 2000UL;

	/// <summary>Sauvegarde d'urgence : sauvegarde tous les chunks chargés (robuste même si un drapeau EstModifie a été raté).</summary>
	public void SauvegarderMondeEntier(bool ignorerDedoublonage = false, string contexte = null)
	{
		ulong maintenantMs = Godot.Time.GetTicksMsec();
		if (!ignorerDedoublonage
			&& _derniereSauvegardeMondeEntierTickMs != 0UL
			&& maintenantMs - _derniereSauvegardeMondeEntierTickMs < FenetreDedoublonageSauvegardeMs)
		{
			// Déjà sauvegardé il y a moins de 2s → on ignore pour éviter les cascades en cascade sur shutdown.
			return;
		}
		_derniereSauvegardeMondeEntierTickMs = maintenantMs;

		string contexteInfo = string.IsNullOrWhiteSpace(contexte) ? "générique" : contexte;
		GD.Print($"ZERO-K : Lancement du Râle d'Agonie ({contexteInfo}, ignorerDedoublonage={ignorerDedoublonage}). Sauvegarde des Chunks modifiés...");
		ForcerInstanciationArbresEnAttente();
		int chunksSauves = 0;
		if (ActiverGenerationAbysse)
		{
			foreach (var kvStage in _chunksAbysseParStage2D)
			{
				foreach (var kvp in kvStage.Value)
				{
					if (SauvegarderChunkCoordEtCouche(kvp.Key, kvp.Value.ChunkOffsetY, kvp.Value, uniquementSiModifie: false))
						chunksSauves++;
				}
			}
		}
		else
		{
			foreach (var kvp in _chunks)
			{
				Vector2I coord = kvp.Key;
				Chunk_Serveur chunk = kvp.Value;
				if (SauvegarderChunkCoordEtCouche(coord, 0, chunk, uniquementSiModifie: false))
					chunksSauves++;
			}
		}
		GD.Print($"ZERO-K : Râle d'Agonie terminé. {chunksSauves} Chunks gravés sur le disque.");
	}

	/// <summary>Flush critique à la sortie/menu : force dirty + full save, sans fenêtre anti-doublon.</summary>
	public void SauvegarderCritiqueAvantSortie(string contexte)
	{
		int dirty = ForcerSauvegardeChunksDirty();
		GD.Print($"ZERO-K : Flush critique '{contexte}' -> chunks dirty sauvés={dirty}.");
		SauvegarderMondeEntier(ignorerDedoublonage: true, contexte: contexte);
	}

	/// <summary>
	/// Autosauvegarde progressive anti-crash : grave un sous-ensemble de chunks actifs à chaque appel.
	/// Le cycle reprend au chunk suivant pour éviter un gros pic CPU/I/O.
	/// </summary>
	public int SauvegarderChunksActifsProgressif(int maxChunks)
	{
		if (maxChunks <= 0) return 0;
		if (ActiverGenerationAbysse)
		{
			if (_chunksAbysseParStage2D.Count == 0)
				return 0;
			ReconstruireCycleAutosaveAbysseSiNecessaire();
			if (_cycleAutosaveChunksAbysse.Count == 0)
				return 0;
			int sauvegardes = 0;
			int scans = Mathf.Max(maxChunks * Mathf.Max(2, MultiplicateurScanDirtyAutosave), 8);
			int total = _cycleAutosaveChunksAbysse.Count;
			scans = Mathf.Clamp(scans, 1, total);
			for (int i = 0; i < scans && sauvegardes < maxChunks; i++)
			{
				if (_indexCycleAutosaveChunksAbysse >= total)
					_indexCycleAutosaveChunksAbysse = 0;
				Vector3I cle = _cycleAutosaveChunksAbysse[_indexCycleAutosaveChunksAbysse++];
				if (!_chunksAbysseParStage2D.TryGetValue(ConstantesDimensionAbysse.ObtenirIndexStageDepuisCoordYChunk(cle.Y, HauteurMax), out var stage))
					continue;
				if (!stage.TryGetValue(new Vector2I(cle.X, cle.Z), out var chunk) || chunk == null || !chunk.EstModifie)
					continue;
				if (SauvegarderChunkCoordEtCouche(new Vector2I(cle.X, cle.Z), cle.Y, chunk, uniquementSiModifie: true))
					sauvegardes++;
			}
			return sauvegardes;
		}
		if (_chunks.Count == 0) return 0;
		int scansAlpha = Mathf.Max(maxChunks * Mathf.Max(2, MultiplicateurScanDirtyAutosave), 8);
		AlimenterFileChunksDirtyAutosave(scansAlpha);
		if (_fileChunksDirtyAutosave.Count == 0) return 0;

		int sauvegardesAlpha = 0;
		int tentative = _fileChunksDirtyAutosave.Count;
		while (sauvegardesAlpha < maxChunks && tentative > 0 && _fileChunksDirtyAutosave.Count > 0)
		{
			tentative--;
			Vector2I coord = _fileChunksDirtyAutosave.Dequeue();
			_setChunksDirtyAutosave.Remove(coord);
			if (SauvegarderChunkCoord(coord, uniquementSiModifie: true))
				sauvegardesAlpha++;
		}
		return sauvegardesAlpha;
	}

	/// <summary>Retourne un backlog compact pour diagnostics autosave/décharge (profil perf).</summary>
	public (int DirtyAutosave, int Decharge) ObtenirBacklogsPersistance()
	{
		return (_fileChunksDirtyAutosave.Count, _chunksEnAttenteDecharge.Count);
	}

	/// <summary>Flush explicite des chunks modifiés (quitter/sauvegarde forcée) avec budget optionnel.</summary>
	public int ForcerSauvegardeChunksDirty(int maxChunks = int.MaxValue)
	{
		if (ActiverGenerationAbysse)
		{
			int budget = maxChunks <= 0 ? int.MaxValue : maxChunks;
			int sauvegardes = 0;
			foreach (var kvStage in _chunksAbysseParStage2D)
			{
				foreach (var kvp in kvStage.Value)
				{
					if (sauvegardes >= budget)
						return sauvegardes;
					Chunk_Serveur chunk = kvp.Value;
					if (chunk == null || !chunk.EstModifie)
						continue;
					if (SauvegarderChunkCoordEtCouche(kvp.Key, chunk.ChunkOffsetY, chunk, uniquementSiModifie: true))
						sauvegardes++;
				}
			}
			return sauvegardes;
		}
		if (_chunks.Count == 0) return 0;
		AlimenterFileChunksDirtyAutosave(_chunks.Count);
		int budgetAlpha = maxChunks <= 0 ? int.MaxValue : maxChunks;
		int sauvegardesAlpha = 0;
		int tentative = _fileChunksDirtyAutosave.Count;
		while (sauvegardesAlpha < budgetAlpha && tentative > 0 && _fileChunksDirtyAutosave.Count > 0)
		{
			tentative--;
			Vector2I coord = _fileChunksDirtyAutosave.Dequeue();
			_setChunksDirtyAutosave.Remove(coord);
			if (SauvegarderChunkCoord(coord, uniquementSiModifie: true))
				sauvegardesAlpha++;
		}
		return sauvegardesAlpha;
	}

	private void AlimenterFileChunksDirtyAutosave(int budgetScan)
	{
		if (_chunks.Count == 0 || budgetScan <= 0) return;
		if (_cycleAutosaveChunks.Count != _chunks.Count)
		{
			_cycleAutosaveChunks.Clear();
			foreach (var coord in _chunks.Keys)
				_cycleAutosaveChunks.Add(coord);
			_indexCycleAutosaveChunks = 0;
		}
		int total = _cycleAutosaveChunks.Count;
		if (total == 0) return;
		int scans = Mathf.Clamp(budgetScan, 1, total);
		for (int i = 0; i < scans; i++)
		{
			if (_indexCycleAutosaveChunks >= total)
				_indexCycleAutosaveChunks = 0;
			Vector2I coord = _cycleAutosaveChunks[_indexCycleAutosaveChunks++];
			if (_setChunksDirtyAutosave.Contains(coord)) continue;
			if (!_chunks.TryGetValue(coord, out var chunk) || chunk == null) continue;
			if (!chunk.EstModifie) continue;
			_setChunksDirtyAutosave.Add(coord);
			_fileChunksDirtyAutosave.Enqueue(coord);
		}
	}

	private bool SauvegarderChunkCoord(Vector2I coord, bool uniquementSiModifie)
	{
		if (!_chunks.TryGetValue(coord, out var chunk) || chunk == null)
			return false;
		int coordYSauvegarde = ActiverGenerationAbysse ? chunk.ChunkOffsetY : 0;
		return SauvegarderChunkCoordEtCouche(coord, coordYSauvegarde, chunk, uniquementSiModifie);
	}

	private bool SauvegarderChunkCoordEtCouche(Vector2I coord, int coordY, Chunk_Serveur chunk, bool uniquementSiModifie)
	{
		if (chunk == null)
			return false;
		if (uniquementSiModifie && !chunk.EstModifie)
			return false;
		ForcerInstanciationArbresEnAttente(coord);
		_chunkPersistenceService.SauvegarderChunkSurDisque(coord, chunk);
		_floraPersistenceService.SauvegarderFloreChunk(coord, chunk);
		_pierrePersistenceService.SauvegarderPierresChunk(coord, coordY);
		_arbrePersistenceService.SauvegarderArbresChunk(coord, chunk);
		return true;
	}

	private void ReconstruireCycleAutosaveAbysseSiNecessaire()
	{
		int total = 0;
		foreach (var stage in _chunksAbysseParStage2D.Values)
			total += stage.Count;
		if (_cycleAutosaveChunksAbysse.Count == total)
			return;
		_cycleAutosaveChunksAbysse.Clear();
		foreach (var kvStage in _chunksAbysseParStage2D)
		{
			foreach (var coord in kvStage.Value.Keys)
				_cycleAutosaveChunksAbysse.Add(new Vector3I(coord.X, ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(kvStage.Key, HauteurMax), coord.Y));
		}
		_indexCycleAutosaveChunksAbysse = 0;
	}

	private bool ColonneAbysseExiste(Vector2I coord)
	{
		foreach (var stage in _chunksAbysseParStage2D.Values)
		{
			if (stage.ContainsKey(coord))
				return true;
		}
		return false;
	}

	private int SauvegarderColonneAbysse(Vector2I coord, bool uniquementSiModifie)
	{
		int sauves = 0;
		foreach (var stage in _chunksAbysseParStage2D.Values)
		{
			if (!stage.TryGetValue(coord, out var chunk) || chunk == null)
				continue;
			if (SauvegarderChunkCoordEtCouche(coord, chunk.ChunkOffsetY, chunk, uniquementSiModifie))
				sauves++;
		}
		return sauves;
	}

	public override void _ExitTree()
	{
		SauvegarderMondeEntier(ignorerDedoublonage: true, contexte: "Monde_Serveur._ExitTree");
	}

	public override void _Notification(int what)
	{
		// Utilisation stricte de Node.NotificationWMCloseRequest (WM en majuscules)
		if (what == Node.NotificationWMCloseRequest)
		{
			SauvegarderMondeEntier(ignorerDedoublonage: true, contexte: "Monde_Serveur.NotificationWMCloseRequest");
			GetTree().Quit();
		}
	}

	private static int CoordYDepuisMondeY(float yMonde, int hauteurMax)
	{
		int h = Mathf.Max(1, hauteurMax);
		return Mathf.FloorToInt(yMonde / h);
	}

	private int ObtenirIndexPalierAbysse(float yMonde)
	{
		return ConstantesDimensionAbysse.ObtenirIndexStageDepuisYMonde(yMonde);
	}

	private void ObtenirPlageCoordYPalierAbysse(int indexPalier, out int coordYMin, out int coordYMax)
	{
		ConstantesDimensionAbysse.ObtenirPlageCoordYChunkDuStage(indexPalier, HauteurMax, out coordYMin, out coordYMax);
	}

	private bool EstCoordYDansFenetrePaliersAbysse(int coordY, Vector3 observation)
	{
		float hauteur = Mathf.Max(1f, HauteurMax);
		float centreChunkY = coordY * hauteur + hauteur * 0.5f;
		int palierChunk = ObtenirIndexPalierAbysse(centreChunkY);
		int palierObservation = ObtenirIndexPalierAbysse(observation.Y);
		int ecart = Mathf.Abs(palierChunk - palierObservation);
		int demiFenetre = ConstantesDimensionAbysse.ObtenirDemiFenetrePaliersActifs(observation.X, observation.Z);
		return ecart <= Mathf.Max(0, demiFenetre);
	}

	private static int LocalYDepuisMondeY(int yMonde, int hauteurMax)
	{
		int h = Mathf.Max(1, hauteurMax);
		int local = yMonde % h;
		if (local < 0) local += h;
		return local;
	}

	private static Vector3I ConstruireCleChunk3D(Vector2I coord, int coordY) => new Vector3I(coord.X, coordY, coord.Y);

	private int NormaliserCoordYAbysse(int coordY)
	{
		int indexStage = ConstantesDimensionAbysse.ObtenirIndexStageDepuisCoordYChunk(coordY, HauteurMax);
		return ConstantesDimensionAbysse.ObtenirCoordYChunkRepresentatifDuStage(indexStage, HauteurMax);
	}

	private void RemplirCoordYImpactesParRayonAbysse(float yCentreMonde, float rayon, HashSet<int> sortie)
	{
		if (sortie == null)
			return;
		sortie.Clear();
		int coordYMin = CoordYDepuisMondeY(yCentreMonde - rayon, HauteurMax);
		int coordYMax = CoordYDepuisMondeY(yCentreMonde + rayon, HauteurMax);
		if (coordYMax < coordYMin)
		{
			int tmp = coordYMin;
			coordYMin = coordYMax;
			coordYMax = tmp;
		}
		for (int coordY = coordYMin; coordY <= coordYMax; coordY++)
			sortie.Add(NormaliserCoordYAbysse(coordY));
		if (sortie.Count == 0)
			sortie.Add(NormaliserCoordYAbysse(CoordYDepuisMondeY(yCentreMonde, HauteurMax)));
	}

	private Dictionary<Vector2I, Chunk_Serveur> ObtenirOuCreerStageAbysse(int indexStage)
	{
		if (!_chunksAbysseParStage2D.TryGetValue(indexStage, out var stage))
		{
			stage = new Dictionary<Vector2I, Chunk_Serveur>();
			_chunksAbysseParStage2D[indexStage] = stage;
		}
		return stage;
	}

	private bool TryGetChunkRuntime(Vector2I coord, int coordY, out Chunk_Serveur chunk)
	{
		if (ActiverGenerationAbysse)
		{
			int coordYNormalise = NormaliserCoordYAbysse(coordY);
			int indexStage = ConstantesDimensionAbysse.ObtenirIndexStageDepuisCoordYChunk(coordYNormalise, HauteurMax);
			if (_chunksAbysseParStage2D.TryGetValue(indexStage, out var stage))
				return stage.TryGetValue(coord, out chunk);
			chunk = null;
			return false;
		}
		return _chunks.TryGetValue(coord, out chunk);
	}

	private void DefinirChunkRuntime(Vector2I coord, int coordY, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		if (ActiverGenerationAbysse)
		{
			int coordYNormalise = NormaliserCoordYAbysse(coordY);
			int indexStage = ConstantesDimensionAbysse.ObtenirIndexStageDepuisCoordYChunk(coordYNormalise, HauteurMax);
			ObtenirOuCreerStageAbysse(indexStage)[coord] = chunk;
			if (!_chunks.ContainsKey(coord))
				_chunks[coord] = chunk; // proxy colonne pour les systèmes 2D existants (décharge/radar)
			return;
		}
		_chunks[coord] = chunk;
	}

	/// <summary>Enregistre une demande de chunk. En Abysse, la coordonnée Y est réellement exploitée.</summary>
	public void EnregistrerDemandeChunk(Vector2I coord, int coordY = 0, Vector3? observation = null)
	{
		if (_simulationSuspendue)
			return;
		bool estAbysse = ActiverGenerationAbysse;
		int cibleY = estAbysse ? NormaliserCoordYAbysse(coordY) : 0;
		Vector3 obs = observation ?? InvokerPositionJoueurStreaming();
		if (estAbysse && !EstCoordYDansFenetrePaliersAbysse(cibleY, obs))
			return;
		Vector3I cle = new Vector3I(coord.X, cibleY, coord.Y);
		if (estAbysse
			&& !_demandesEnAttenteSet.Contains(cle)
			&& _chunksEnAttenteEnvoi.Count >= Mathf.Max(512, MaxDemandesAbysseEnFile))
		{
			// Garde-fou anti-file infinie: on refuse de grossir la file quand elle est saturée.
			return;
		}
		var demande = new DemandeChunk
		{
			Coord = coord,
			CoordY = cibleY,
			Observation = obs,
			EstAbysse = estAbysse
		};
		cle = demande.Cle3D;
		if (_demandesEnAttenteSet.Add(cle))
			_chunksEnAttenteEnvoi.Add(demande);
		_demandesForceesSansPurge.Add(cle);
	}

	public bool ChunkEstCharge(Vector2I coord)
	{
		if (ActiverGenerationAbysse)
		{
			foreach (var stage in _chunksAbysseParStage2D.Values)
			{
				if (stage.ContainsKey(coord))
					return true;
			}
		}
		return _chunks.ContainsKey(coord);
	}

	public Chunk_Serveur ObtenirOuCreerChunk(Vector2I coord)
	{
		return ObtenirOuCreerChunk(coord, 0);
	}

	public Chunk_Serveur ObtenirOuCreerChunk(Vector2I coord, int coordY)
	{
		int coordYLocal = ActiverGenerationAbysse ? NormaliserCoordYAbysse(coordY) : 0;
		if (TryGetChunkRuntime(coord, coordYLocal, out var c)) return c;

		Chunk_Serveur chunkActuel = null;
		bool fichierExistant = FichierChunkExiste(coord, coordYLocal);
		// BRANCHE 1 : RÉSURRECTION — AUCUNE génération.
		if (fichierExistant)
			chunkActuel = ChargerChunkDepuisDisque(coord, coordYLocal);
		// BRANCHE 2 : CRÉATION PROCÉDURALE — TOUTES les passes ici.
		if (chunkActuel == null)
		{
			if (fichierExistant)
				GD.PrintErr($"ZERO-K DIAG : ObtenirOuCreerChunk fallback procédural pour {coord} (fichier présent mais lecture invalide).");
			chunkActuel = CreerChunkServeur(coord, coordYLocal);
			chunkActuel.GenererDonneesVoxel(); // GenererTerrainDeBase, Surface, Eau — UNIQUEMENT pour chunks ex nihilo.
		}
		DefinirChunkRuntime(coord, coordYLocal, chunkActuel);
		SynchroniserFrontieresAvecVoisinsCharges(coord, chunkActuel);
		RepousserBorduresChunkDisqueVersVoisinsProceduraux(coord, chunkActuel);
		return chunkActuel;
	}

	/// <summary>Quand un chunk arrive après ses voisins, aligne ses bordures sur les chunks déjà chargés pour éviter les coutures visuelles.</summary>
	/// <remarks>Ne copie jamais une bordure procédurale sur un chunk ressuscité du disque : sinon trous sauvegardés rebouchés au reload.</remarks>
	private void SynchroniserFrontieresAvecVoisinsCharges(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		if (ActiverGenerationAbysse)
			return; // En Abysse multi-couches, cette synchro 2D peut recoller des tranches Y incohérentes.

		void SynchroniserBordureDepuisVoisin(Vector2I coordVoisin, int voisinX, int chunkX, int voisinZ, int chunkZ, bool axeX)
		{
			if (!_chunks.TryGetValue(coordVoisin, out var voisin) || voisin == null) return;
			if (chunk.EstChargeDepuisDisque && !voisin.EstChargeDepuisDisque)
			{
				if (OS.IsDebugBuild())
					GD.Print($"ZERO-K DIAG : Synchro frontière ignorée ({coord} <- {coordVoisin}) pour préserver un chunk disque.");
				return;
			}
			for (int y = 0; y <= HauteurMax; y++)
			{
				if (axeX)
				{
					for (int z = 0; z <= TailleChunk; z++)
					{
						byte id = voisin.LireVoxelLocalBrut(voisinX, y, z);
						chunk.SetVoxelLocal(chunkX, y, z, id, false);
					}
				}
				else
				{
					for (int x = 0; x <= TailleChunk; x++)
					{
						byte id = voisin.LireVoxelLocalBrut(x, y, voisinZ);
						chunk.SetVoxelLocal(x, y, chunkZ, id, false);
					}
				}
			}
		}

		// 4 côtés cardinaux.
		SynchroniserBordureDepuisVoisin(new Vector2I(coord.X - 1, coord.Y), TailleChunk, 0, 0, 0, true);          // Ouest
		SynchroniserBordureDepuisVoisin(new Vector2I(coord.X + 1, coord.Y), 0, TailleChunk, 0, 0, true);          // Est
		SynchroniserBordureDepuisVoisin(new Vector2I(coord.X, coord.Y - 1), 0, 0, TailleChunk, 0, false);         // Nord
		SynchroniserBordureDepuisVoisin(new Vector2I(coord.X, coord.Y + 1), 0, 0, 0, TailleChunk, false);         // Sud

		// 4 coins (Marching Cubes lit aussi les coins partagés).
		if (_chunks.TryGetValue(new Vector2I(coord.X - 1, coord.Y - 1), out var nordOuest) && !(chunk.EstChargeDepuisDisque && !nordOuest.EstChargeDepuisDisque))
			for (int y = 0; y <= HauteurMax; y++)
				chunk.SetVoxelLocal(0, y, 0, nordOuest.LireVoxelLocalBrut(TailleChunk, y, TailleChunk), false);
		if (_chunks.TryGetValue(new Vector2I(coord.X + 1, coord.Y - 1), out var nordEst) && !(chunk.EstChargeDepuisDisque && !nordEst.EstChargeDepuisDisque))
			for (int y = 0; y <= HauteurMax; y++)
				chunk.SetVoxelLocal(TailleChunk, y, 0, nordEst.LireVoxelLocalBrut(0, y, TailleChunk), false);
		if (_chunks.TryGetValue(new Vector2I(coord.X - 1, coord.Y + 1), out var sudOuest) && !(chunk.EstChargeDepuisDisque && !sudOuest.EstChargeDepuisDisque))
			for (int y = 0; y <= HauteurMax; y++)
				chunk.SetVoxelLocal(0, y, TailleChunk, sudOuest.LireVoxelLocalBrut(TailleChunk, y, 0), false);
		if (_chunks.TryGetValue(new Vector2I(coord.X + 1, coord.Y + 1), out var sudEst) && !(chunk.EstChargeDepuisDisque && !sudEst.EstChargeDepuisDisque))
			for (int y = 0; y <= HauteurMax; y++)
				chunk.SetVoxelLocal(TailleChunk, y, TailleChunk, sudEst.LireVoxelLocalBrut(0, y, 0), false);
	}

	/// <summary>Après résurrection disque : recopie la bordure sauvegardée vers les voisins procéduraux déjà en RAM, puis ré-enfile leur envoi client.</summary>
	private void RepousserBorduresChunkDisqueVersVoisinsProceduraux(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null || !chunk.EstChargeDepuisDisque) return;
		if (ActiverGenerationAbysse)
			return; // Évite de pousser des bordures 2D sur des couches verticales différentes.

		void PousserBordureVersVoisin(Vector2I coordVoisin, int chunkX, int voisinX, int chunkZ, int voisinZ, bool axeX)
		{
			if (!_chunks.TryGetValue(coordVoisin, out var voisin) || voisin == null || voisin.EstChargeDepuisDisque) return;
			for (int y = 0; y <= HauteurMax; y++)
			{
				if (axeX)
				{
					for (int z = 0; z <= TailleChunk; z++)
					{
						byte id = chunk.LireVoxelLocalBrut(chunkX, y, z);
						voisin.SetVoxelLocal(voisinX, y, z, id);
					}
				}
				else
				{
					for (int x = 0; x <= TailleChunk; x++)
					{
						byte id = chunk.LireVoxelLocalBrut(x, y, chunkZ);
						voisin.SetVoxelLocal(x, y, voisinZ, id);
					}
				}
			}
			EnfileEnvoiCompletChunkAuClient(coordVoisin);
		}

		// Miroir de SynchroniserBordureDepuisVoisin : bord du chunk disque → padding du voisin procédural.
		PousserBordureVersVoisin(new Vector2I(coord.X - 1, coord.Y), 0, TailleChunk, 0, 0, true);   // face Ouest du chunk → colonne Est du voisin Ouest
		PousserBordureVersVoisin(new Vector2I(coord.X + 1, coord.Y), TailleChunk, 0, 0, 0, true);
		PousserBordureVersVoisin(new Vector2I(coord.X, coord.Y - 1), 0, 0, 0, TailleChunk, false);
		PousserBordureVersVoisin(new Vector2I(coord.X, coord.Y + 1), 0, 0, TailleChunk, 0, false);

		// Coins diagonaux : voxel partagé (évite décalage MC sur voisin procédural déjà envoyé).
		if (_chunks.TryGetValue(new Vector2I(coord.X - 1, coord.Y - 1), out var no) && !no.EstChargeDepuisDisque)
		{
			for (int y = 0; y <= HauteurMax; y++)
				no.SetVoxelLocal(TailleChunk, y, TailleChunk, chunk.LireVoxelLocalBrut(0, y, 0));
			EnfileEnvoiCompletChunkAuClient(new Vector2I(coord.X - 1, coord.Y - 1));
		}
		if (_chunks.TryGetValue(new Vector2I(coord.X + 1, coord.Y - 1), out var ne) && !ne.EstChargeDepuisDisque)
		{
			for (int y = 0; y <= HauteurMax; y++)
				ne.SetVoxelLocal(0, y, TailleChunk, chunk.LireVoxelLocalBrut(TailleChunk, y, 0));
			EnfileEnvoiCompletChunkAuClient(new Vector2I(coord.X + 1, coord.Y - 1));
		}
		if (_chunks.TryGetValue(new Vector2I(coord.X - 1, coord.Y + 1), out var so) && !so.EstChargeDepuisDisque)
		{
			for (int y = 0; y <= HauteurMax; y++)
				so.SetVoxelLocal(TailleChunk, y, 0, chunk.LireVoxelLocalBrut(0, y, TailleChunk));
			EnfileEnvoiCompletChunkAuClient(new Vector2I(coord.X - 1, coord.Y + 1));
		}
		if (_chunks.TryGetValue(new Vector2I(coord.X + 1, coord.Y + 1), out var se) && !se.EstChargeDepuisDisque)
		{
			for (int y = 0; y <= HauteurMax; y++)
				se.SetVoxelLocal(0, y, 0, chunk.LireVoxelLocalBrut(TailleChunk, y, TailleChunk));
			EnfileEnvoiCompletChunkAuClient(new Vector2I(coord.X + 1, coord.Y + 1));
		}
	}

	/// <summary>Ré-enfile un colis complet pour que le client remplace le maillage après mutation serveur.</summary>
	private void EnfileEnvoiCompletChunkAuClient(Vector2I coord)
	{
		if (!_chunks.TryGetValue(coord, out var ch) || ch == null) return;
		_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() });
	}

	private Chunk_Serveur CreerChunkServeur(Vector2I coord, int coordY = 0)
	{
		var chunk = new Chunk_Serveur(
			coord.X, coordY, coord.Y, TailleChunk, HauteurMax, SeedTerrain,
			(pos, mat, brancheTailléeBuisson, indexCouleurBaie) => { SpawnBlocChutant(pos, mat, brancheTailléeBuisson, indexCouleurBaie); },
			ChunkEstCharge,
			ReveillerEauAdjacente,
			ActiverGenerationAbysse,
			ObtenirDossierChunksRelatif()
		);
		chunk.SetOnVoxelModifie((pos, id) => _onVoxelModifie?.Invoke(pos, id));
		chunk.SetOnFlorePurgée((c, coordChunkY, inventaire) =>
		{
			_onFloreModifie?.Invoke(c, coordChunkY, inventaire);
			// La fauche et les interactions buissons ne touchent pas aux voxels : sans gravure immédiate,
			// l’état flore ne part sur disque qu’au prochain passage de l’autosauvegarde progressive (potentiellement très tard).
			SauvegarderFloreChunk(c, chunk);
		});
		return chunk;
	}

	/// <summary>Sauvegarde les roches matière (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>) : morph dans index, taille dans chimique (octet).</summary>
	internal void SauvegarderPierresChunk(Vector2I coord, int coordY)
	{
		if (_parentPourBlocsChutants == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var pierres = new List<(Vector3 pos, int id, int index, int chimique)>();
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			var item = child as ItemPhysique ?? child.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			if (item == null) continue;
			if (item.EstEclatFracture) continue; // Éclats de fracture : pas sauvegardés (créés à l'instant, supprimés quand chunk déchargé).
			int id = item.ID_Objet;
			if (!ItemPhysique.EstIdRocheMatiere(id)) continue;
			if (child is not Node3D n3 || !TryGetPositionMonde(n3, out Vector3 pos)) continue;
			if (pos.X >= xMin && pos.X < xMax && pos.Z >= zMin && pos.Z < zMax)
				pierres.Add((pos, id, Mathf.Clamp(item.IndexCacheMemoire, 0, 3), Mathf.Clamp(item.IndexTailleRoche, 0, 4)));
		}
		string dossier = ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif() + "/");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coordY}_{coord.Y}_items.bin");
		try
		{
			using (var w = new BinaryWriter(File.Open(chemin, FileMode.Create)))
			{
				w.Write(0x5A4B324A); // Magic v3 = IndexCacheMemoire + IndexChimique
				w.Write(pierres.Count);
				foreach (var (pos, id, index, chimique) in pierres)
				{
					w.Write(pos.X); w.Write(pos.Y); w.Write(pos.Z);
					w.Write((byte)id);
					w.Write((byte)index);
					w.Write((byte)chimique);
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde pierres chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Charge et enfile les pierres sur le tapis roulant (ordre spatial logique X,Z,Y). v1/v2/v3.</summary>
	internal bool ChargerEtSpawnerPierresChunk(Vector2I coord, int coordY)
	{
		if (_parentPourBlocsChutants == null) return false;
		string dossier = ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif());
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coordY}_{coord.Y}_items.bin");
		if (!File.Exists(chemin))
		{
			// Fallback legacy: les versions précédentes forçaient coordY=0.
			chemin = Path.Combine(dossier, $"chunk_{coord.X}_0_{coord.Y}_items.bin");
		}
		if (!File.Exists(chemin)) return false;
		try
		{
			var pierres = new List<(Vector3 pos, int id, int indexCache, int indexChimique)>();
			using (var stream = File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
			using (var r = new BinaryReader(stream))
			{
				int magicOrCount = r.ReadInt32();
				bool formatV3 = (magicOrCount == 0x5A4B324A);
				bool formatV2 = (magicOrCount == 0x5A4B3249) || formatV3;
				int count = formatV2 || formatV3 ? r.ReadInt32() : magicOrCount;
				for (int i = 0; i < count; i++)
				{
					float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
					int id = r.ReadByte();
					int indexCache = formatV2 || formatV3 ? r.ReadByte() : -1;
					int indexChimique = formatV3 ? r.ReadByte() : -1;
					if (id >= 10 && id <= 14)
					{
						int chim = Mathf.Clamp(indexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
						if (id == 11) chim = ItemPhysique.IndexChimiqueSilex;
						int tailleMigr = id switch { 10 => 1, 11 => 1, 12 => 2, 13 => 3, 14 => 4, _ => 2 };
						id = ItemPhysique.IdRocheMatiereMin + chim;
						indexChimique = tailleMigr;
						if (indexCache >= 0) indexCache %= 4;
					}
					if (ItemPhysique.EstIdRocheMatiere(id))
						pierres.Add((new Vector3(x, y, z), id, indexCache, indexChimique));
				}
			}
			MettreRochesEnStase(coord, pierres);
			return true;
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur chargement pierres chunk {coord} : {ex.Message}"); return false; }
	}

	/// <summary>Sauvegarde les ArbreVivant dans ce chunk. Fichier chunk_X_Y_arbres.bin.</summary>
	internal void SauvegarderArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		// Pendant la fermeture, cette méthode peut être rappelée alors que le parent d'arbres
		// n'est plus dans l'arbre de scène. Dans cet état, la collecte retourne 0 et
		// écrase les fichiers *_arbres.bin avec un inventaire vide.
		if (_parentPourArbres == null
			|| !GodotObject.IsInstanceValid(_parentPourArbres)
			|| !_parentPourArbres.IsInsideTree())
			return;
		ForcerInstanciationArbresEnAttente(coord);
		int coordY = chunk?.ChunkOffsetY ?? 0;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		// En Abysse, plusieurs paliers Y partagent le même XZ : on doit scinder les arbres par couche.
		// En Alpha, ChunkOffsetY = 0 -> bornes [0, HauteurMax] qui couvrent tout (compatibilité préservée).
		float yMin = coordY * HauteurMax;
		float yMax = (coordY + 1) * HauteurMax;
		var arbres = new List<(Vector3 pos, int age, byte indexBotanique, uint seed)>();
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is not ArbreVivant arbre) continue;
			if (!TryGetPositionMonde(arbre, out Vector3 p)) continue;
			if (p.X >= xMin && p.X < xMax && p.Z >= zMin && p.Z < zMax && p.Y >= yMin && p.Y < yMax)
				arbres.Add((p, arbre.AgeEnJours, arbre.IndexBotanique, arbre.Seed));
		}
		string dossier = ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif() + "/");
		Directory.CreateDirectory(dossier);
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coordY}_{coord.Y}_arbres.bin");
		try
		{
			using (var w = new BinaryWriter(File.Open(chemin, FileMode.Create)))
			{
				w.Write(MagicArbresV6); // MAGIC V6 = V5 + correction Y racine (plus de tronc enterré au reload)
				int jourActuel = GameState.Instance != null ? GameState.Instance.JourAbsolu : 0;
				long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				w.Write(jourActuel);
				w.Write(unixNow);
				w.Write(arbres.Count);
				foreach (var (pos, age, indexBotanique, seed) in arbres)
				{
					int gx = Mathf.FloorToInt(pos.X);
					int gz = Mathf.FloorToInt(pos.Z);
					// ArbreVivant est instancié à (racineY - 0.5). On sauvegarde la racine entière pour éviter la dérive.
					int gyRacine = Mathf.RoundToInt(pos.Y + 0.5f);
					w.Write(gx); w.Write(gyRacine); w.Write(gz);
					w.Write(age); // Âge brut (int, croissance infinie)
					w.Write(indexBotanique);
					w.Write(seed); // Seed exacte de forme pour réinstanciation identique.
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde arbres chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Croissance des arbres 3D : VieillirUnJour sur chaque ArbreVivant. Appelé au changement de jour (minuit).</summary>
	public void FairePousserArbresDuJour()
	{
		if (_simulationSuspendue || _parentPourArbres == null)
			return;
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is ArbreVivant arbre)
				arbre.VieillirUnJour();
		}
		GD.Print("ZERO-K : Croissance des arbres du jour appliquée.");
	}

	public DonneesChunk ObtenirDonneesChunkPourClient(Vector2I coord)
	{
		var chunk = ObtenirOuCreerChunk(coord);
		return chunk.ObtenirDonneesPourClient();
	}

	/// <summary>
	/// Surface monde (face supérieure du voxel de surface : <c>gy + 1</c>) à partir des densités serveur,
	/// même convention que <see cref="Monde_Client.EssayerObtenirYSurfaceMondeDepuisDonneesVoxel"/>.
	/// Alpha-like uniquement : retourne <c>false</c> si génération Abysse active.
	/// </summary>
	public bool EssayerObtenirYSurfaceMondeDepuisVoxels(int gx, int gz, out float ySurface)
	{
		ySurface = 0f;
		if (ActiverGenerationAbysse)
			return false;

		Gestionnaire_Monde.WorldToChunkAndLocal(gx, gz, TailleChunk, out Vector2I coordChunk, out int lx, out int lz);
		if (lx < 0 || lx >= TailleChunk || lz < 0 || lz >= TailleChunk)
			return false;

		ObtenirOuCreerChunk(coordChunk, 0);

		const int yMondeMin = 3;
		int hProc = Generateur_Voxel.ObtenirHauteurTerrainMonde(gx, gz, SeedTerrain);
		// Fenêtre autour de la hauteur « carte » : évite de prendre un plafon de grotte / surplomb très au-dessus du sol jouable.
		int yHaut = Mathf.Min(HauteurMax - 1, hProc + 72);
		int yBas = Mathf.Max(yMondeMin, hProc - 160);
		for (int gy = yHaut; gy >= yBas; gy--)
		{
			var pos = new Vector3I(gx, gy, gz);
			if (EstVoxelAir(pos))
				continue;
			bool videAuDessus = gy + 1 >= HauteurMax || EstVoxelAir(new Vector3I(gx, gy + 1, gz));
			if (!videAuDessus)
				continue;
			ySurface = gy + 1f;
			return true;
		}

		for (int gy = HauteurMax - 1; gy >= yMondeMin; gy--)
		{
			var pos = new Vector3I(gx, gy, gz);
			if (EstVoxelAir(pos))
				continue;
			bool videAuDessus = gy + 1 >= HauteurMax || EstVoxelAir(new Vector3I(gx, gy + 1, gz));
			if (!videAuDessus)
				continue;
			ySurface = gy + 1f;
			return true;
		}

		return false;
	}

	private (Chunk_Serveur chunk, Vector3I local)? ObtenirChunkEtLocal(Vector3I pos)
	{
		Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk) return null;
		int coordYLocal = CoordYDepuisMondeY(pos.Y, HauteurMax);
		Vector2I coord = new Vector2I(c.X, c.Y);
		if (!TryGetChunkRuntime(coord, coordYLocal, out var ch))
			return null;
		int localY = LocalYDepuisMondeY(pos.Y, HauteurMax);
		return (ch, new Vector3I(lx, localY, lz));
	}

	private bool EstVoxelEau(Vector3I pos)
	{
		var r = ObtenirChunkEtLocal(pos);
		return r.HasValue && r.Value.chunk.EstVoxelEau(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	/// <summary>Vérifie un petit voisinage 3³ : bûche/bâton peuvent chevaucher plusieurs voxels.</summary>
	public bool EstPointDansEau(Vector3 positionGlobale)
	{
		int gx = Mathf.FloorToInt(positionGlobale.X);
		int gy = Mathf.FloorToInt(positionGlobale.Y);
		int gz = Mathf.FloorToInt(positionGlobale.Z);
		for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
				for (int dz = -1; dz <= 1; dz++)
					if (EstVoxelEau(new Vector3I(gx + dx, gy + dy, gz + dz)))
						return true;
		return false;
	}

	private bool EstVoxelAir(Vector3I pos)
	{
		var r = ObtenirChunkEtLocal(pos);
		return r.HasValue && r.Value.chunk.EstVoxelAir(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
	}

	private void DefinirVoxel(Vector3I pos, byte id)
	{
		var r = ObtenirChunkEtLocal(pos);
		if (!r.HasValue) return;
		if (id == 4) r.Value.chunk.DefinirVoxelEau(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
		else if (id == 0) r.Value.chunk.DefinirVoxelAir(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
		_onVoxelModifie?.Invoke(pos, id);
	}

	/// <summary>Réplique la modification sur le padding des chunks voisins (évite déchirures quand chunk envoyé plus tard).</summary>
	public void RepliquerPaddingVoisins(Vector3I posGlobal, byte id)
	{
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int localX, out int localZ);
		int cx = c.X;
		int cz = c.Y;

		int chunkY = CoordYDepuisMondeY(posGlobal.Y, HauteurMax);
		int localY = LocalYDepuisMondeY(posGlobal.Y, HauteurMax);

		Chunk_Serveur ObtenirVoisinPadding(int chunkX, int chunkZ)
		{
			var coordVoisine = new Vector2I(chunkX, chunkZ);
			if (TryGetChunkRuntime(coordVoisine, chunkY, out var voisin) && voisin != null)
				return voisin;
			// Garantit la persistance des bords : si le voisin n'était pas chargé, on le crée
			// et on réplique quand même le padding pour éviter les coutures au chargement tardif.
			return ObtenirOuCreerChunk(coordVoisine, chunkY);
		}

		if (localX == 0)
			ObtenirVoisinPadding(cx - 1, cz)?.SetVoxelLocal(TailleChunk, localY, localZ, id);
		if (localX == TailleChunk - 1)
			ObtenirVoisinPadding(cx + 1, cz)?.SetVoxelLocal(0, localY, localZ, id);
		if (localZ == 0)
			ObtenirVoisinPadding(cx, cz - 1)?.SetVoxelLocal(localX, localY, TailleChunk, id);
		if (localZ == TailleChunk - 1)
			ObtenirVoisinPadding(cx, cz + 1)?.SetVoxelLocal(localX, localY, 0, id);
		if (localX == 0 && localZ == 0)
			ObtenirVoisinPadding(cx - 1, cz - 1)?.SetVoxelLocal(TailleChunk, localY, TailleChunk, id);
		if (localX == TailleChunk - 1 && localZ == 0)
			ObtenirVoisinPadding(cx + 1, cz - 1)?.SetVoxelLocal(0, localY, TailleChunk, id);
		if (localX == 0 && localZ == TailleChunk - 1)
			ObtenirVoisinPadding(cx - 1, cz + 1)?.SetVoxelLocal(TailleChunk, localY, 0, id);
		if (localX == TailleChunk - 1 && localZ == TailleChunk - 1)
			ObtenirVoisinPadding(cx + 1, cz + 1)?.SetVoxelLocal(0, localY, 0, id);
	}

	private void DemanderMiseAJourMesh(Vector3I pos)
	{
		var r = ObtenirChunkEtLocal(pos);
		if (!r.HasValue) return;
		Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		int cx = c.X;
		int cz = c.Y;
		int localY = LocalYDepuisMondeY(pos.Y, HauteurMax);
		int sec = Mathf.Clamp(Mathf.FloorToInt(localY / 16f), 0, 44);  // section locale dans la tranche verticale active
		_onChunkModifie?.Invoke(new Vector2I(cx, cz), new List<int> { sec });
		if (lx == 0) _onChunkModifie?.Invoke(new Vector2I(cx - 1, cz), new List<int> { sec });
		if (lx == TailleChunk - 1) _onChunkModifie?.Invoke(new Vector2I(cx + 1, cz), new List<int> { sec });
		if (lz == 0) _onChunkModifie?.Invoke(new Vector2I(cx, cz - 1), new List<int> { sec });
		if (lz == TailleChunk - 1) _onChunkModifie?.Invoke(new Vector2I(cx, cz + 1), new List<int> { sec });
	}

	public static int ObtenirHauteurTerrainMonde(int worldX, int worldZ, int seed)
	{
		return Generateur_Voxel.ObtenirHauteurTerrainMonde(worldX, worldZ, seed);
	}

	/// <summary>Oracle géologique : sonde les 8 coins du cube Marching Cubes pour isoler la matière solide (évite fallback gazon quand on lit l'air).</summary>
	public int ObtenirMatiereExacte(Vector3 positionGlobale)
	{
		int gx = Mathf.FloorToInt(positionGlobale.X);
		int gy = Mathf.FloorToInt(positionGlobale.Y);
		int gz = Mathf.FloorToInt(positionGlobale.Z);

		int matiereTrouvee = 1;
		bool trouveSolide = false;

		for (int dx = 0; dx <= 1; dx++)
		{
			for (int dy = 0; dy <= 1; dy++)
			{
				for (int dz = 0; dz <= 1; dz++)
				{
					var r = ObtenirChunkEtLocal(new Vector3I(gx + dx, gy + dy, gz + dz));
					if (r.HasValue && r.Value.chunk.EstVoxelSolide(r.Value.local.X, r.Value.local.Y, r.Value.local.Z))
					{
						byte mat = r.Value.chunk.ObtenirMatiereAtLocal(r.Value.local.X, r.Value.local.Y, r.Value.local.Z);
						if (mat > 0)
						{
							matiereTrouvee = mat;
							trouveSolide = true;
							if (mat != 1) return mat;
						}
					}
				}
			}
		}
		return trouveSolide ? matiereTrouvee : 1;
	}

	private float DistanceCarreeAuJoueur(DemandeChunk chunk, Vector3 posObservation)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(posObservation, TailleChunk);
		int dx = chunk.Coord.X - obs.X;
		int dz = chunk.Coord.Y - obs.Y;
		if (!chunk.EstAbysse)
			return dx * dx + dz * dz;
		int dy = chunk.CoordY - CoordYDepuisMondeY(posObservation.Y, HauteurMax);
		return dx * dx + dz * dz + (dy * dy);
	}

	/// <summary>Extraction radiale : le chunk à distance minimale de l'épicentre. DistanceSquaredTo évite la racine carrée.</summary>
	private DemandeChunk ExtraireChunkLePlusProche(List<DemandeChunk> liste, Vector3 positionObservation)
	{
		if (liste.Count == 0) return default;
		Vector2 posObsV2 = new Vector2(positionObservation.X / (float)TailleChunk, positionObservation.Z / (float)TailleChunk);
		DemandeChunk chunkCible = liste[0];
		float distanceMin = float.MaxValue;
		int indexASupprimer = 0;
		for (int i = 0; i < liste.Count; i++)
		{
			DemandeChunk entree = liste[i];
			Vector2 posChunk = new Vector2(entree.Coord.X, entree.Coord.Y);
			float dist = posObsV2.DistanceSquaredTo(posChunk);
			if (entree.EstAbysse)
			{
				int dy = entree.CoordY - CoordYDepuisMondeY(positionObservation.Y, HauteurMax);
				dist += dy * dy;
			}
			if (dist < distanceMin)
			{
				distanceMin = dist;
				chunkCible = entree;
				indexASupprimer = i;
			}
		}
		liste.RemoveAt(indexASupprimer);
		return chunkCible;
	}

	private void PurgerRuntimeAbysseHorsFenetre(Vector3 positionObservation)
	{
		if (!ActiverGenerationAbysse || _chunksAbysseParStage2D.Count == 0)
			return;

		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		float seuilDistCarree = (RenderDistance + 2) * (RenderDistance + 2);
		int stageObservation = ConstantesDimensionAbysse.ObtenirIndexStageDepuisYMonde(positionObservation.Y);
		int demiFenetre = ConstantesDimensionAbysse.ObtenirDemiFenetrePaliersActifs(positionObservation.X, positionObservation.Z);
		int stageMin = stageObservation - Mathf.Max(0, demiFenetre);
		int stageMax = stageObservation + Mathf.Max(0, demiFenetre);
		var stagesASupprimer = new List<int>();
		foreach (var kvStage in _chunksAbysseParStage2D)
		{
			int stage = kvStage.Key;
			if (stage < stageMin || stage > stageMax)
			{
				stagesASupprimer.Add(stage);
				continue;
			}
			var coordsASupprimer = new List<Vector2I>();
			foreach (var kvChunk in kvStage.Value)
			{
				int dx = kvChunk.Key.X - obs.X;
				int dz = kvChunk.Key.Y - obs.Y;
				float dist2 = dx * dx + dz * dz;
				if (dist2 > seuilDistCarree)
					coordsASupprimer.Add(kvChunk.Key);
			}
			foreach (var coord in coordsASupprimer)
			{
				if (!kvStage.Value.TryGetValue(coord, out var chunk)) continue;
				if (chunk != null && chunk.EstModifie)
					SauvegarderChunkCoordEtCouche(coord, chunk.ChunkOffsetY, chunk, uniquementSiModifie: false);
				kvStage.Value.Remove(coord);
				if (_chunks.TryGetValue(coord, out var proxy) && ReferenceEquals(proxy, chunk))
					_chunks.Remove(coord);
			}
			if (kvStage.Value.Count == 0)
				stagesASupprimer.Add(stage);
		}
		foreach (int stage in stagesASupprimer)
		{
			if (!_chunksAbysseParStage2D.TryGetValue(stage, out var stageDict))
				continue;
			var clesEtage = new List<Vector2I>(stageDict.Keys);
			foreach (var coord in clesEtage)
			{
				if (!stageDict.TryGetValue(coord, out var ch) || ch == null)
					continue;
				if (ch.EstModifie)
					SauvegarderChunkCoordEtCouche(coord, ch.ChunkOffsetY, ch, uniquementSiModifie: false);
				if (_chunks.TryGetValue(coord, out var proxy) && ReferenceEquals(proxy, ch))
					_chunks.Remove(coord);
			}
			_chunksAbysseParStage2D.Remove(stage);
		}
	}

	private void EvaluerDechargementChunks()
	{
		if (_obtenirPositionJoueur == null || _onOrdonnerDestructionChunk == null) return;
		Vector3 posJoueur = InvokerPositionJoueurStreaming();
		Vector2I cj = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cjX = cj.X;
		int cjZ = cj.Y;
		foreach (var kv in _chunks)
		{
			int dx = Mathf.Abs(kv.Key.X - cjX);
			int dz = Mathf.Abs(kv.Key.Y - cjZ);
			if (dx <= RenderDistance && dz <= RenderDistance)
				continue;
			if (_chunksEnAttenteDechargeSet.Add(kv.Key))
				_chunksEnAttenteDecharge.Enqueue(kv.Key);
		}
	}

	/// <summary>Traite au plus MaxChunksDechargeParTick chunks : sauvegarde (voxels + pierres) puis décharge (retrait pierres, Remove chunk, notif client).</summary>
	private void ProcesserDechargeProgressive()
	{
		if (_chunksEnAttenteDecharge.Count == 0 || _onOrdonnerDestructionChunk == null) return;
		Vector3 posJoueur = InvokerPositionJoueurStreaming();
		Vector2I cj = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		float facteurPression = CalculerFacteurPressionSpawn();
		int budgetChunks = Mathf.Max(1, Mathf.RoundToInt(CalculerBudgetSpawnAdaptatif(Mathf.Max(1, MaxChunksDechargeParTick)) * Mathf.Clamp(facteurPression * 0.9f, 0.2f, 1f)));
		ulong t0 = Time.GetTicksUsec();
		ulong budgetUs = (ulong)Mathf.Max(140f, BudgetMsDechargeParTick * 1000f * Mathf.Clamp(facteurPression, 0.2f, 1f));
		int traites = 0;
		while (traites < budgetChunks && _chunksEnAttenteDecharge.Count > 0)
		{
			if (Time.GetTicksUsec() - t0 >= budgetUs) break;
			Vector2I coord = _chunksEnAttenteDecharge.Dequeue();
			_chunksEnAttenteDechargeSet.Remove(coord);
			int dxJoueur = Mathf.Abs(coord.X - cj.X);
			int dzJoueur = Mathf.Abs(coord.Y - cj.Y);
			if (dxJoueur <= RenderDistance && dzJoueur <= RenderDistance)
				continue;
			bool colonneChargee = _chunks.ContainsKey(coord) || (ActiverGenerationAbysse && ColonneAbysseExiste(coord));
			if (colonneChargee)
			{
				if (ActiverGenerationAbysse)
					SauvegarderColonneAbysse(coord, uniquementSiModifie: false);
				else
					SauvegarderChunkCoord(coord, uniquementSiModifie: false);
				RetirerPierresChunk(coord);
				RetirerArbresChunk(coord);
				if (ActiverGenerationAbysse)
				{
					foreach (var stage in _chunksAbysseParStage2D.Values)
					{
						stage.Remove(coord);
					}
				}
				_chunks.Remove(coord);
				_onOrdonnerDestructionChunk(coord);
				traites++;
			}
		}
	}
}