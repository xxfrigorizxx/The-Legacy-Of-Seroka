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
	[Export] public string NomDimension = "Dimension_Alpha";
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
	private readonly HashSet<long> _adminPeerIds = new HashSet<long>();
	private readonly Dictionary<long, bool> _modeCreatifParPeer = new Dictionary<long, bool>();
	private readonly Dictionary<long, bool> _noclipParPeer = new Dictionary<long, bool>();
	private const string CheminAdminWhitelist = "user://admin_whitelist.json";

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
	private bool _modificationEnCours;
	private readonly object _verrouGeneration = new object();
	private readonly ConcurrentQueue<(Vector2I coord, int coordY, Chunk_Serveur chunk, DonneesChunk donnees)> _chunksGeneres = new ConcurrentQueue<(Vector2I, int, Chunk_Serveur, DonneesChunk)>();

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
	}

	private void ChargerAdminWhitelist()
	{
		_adminPeerIds.Clear();
		// Hôte local autorisé par défaut pour dev/administration.
		_adminPeerIds.Add(1L);
		if (!FileAccess.FileExists(CheminAdminWhitelist))
		{
			GD.Print($"ZERO-K ADMIN : whitelist absente ({CheminAdminWhitelist}), fallback hôte local (peer 1).");
			return;
		}

		try
		{
			using var file = FileAccess.Open(CheminAdminWhitelist, FileAccess.ModeFlags.Read);
			if (file == null) return;
			string contenu = file.GetAsText();
			if (string.IsNullOrWhiteSpace(contenu)) return;
			using var doc = JsonDocument.Parse(contenu);
			if (!doc.RootElement.TryGetProperty("admin_peer_ids", out JsonElement admins) || admins.ValueKind != JsonValueKind.Array)
				return;
			foreach (JsonElement e in admins.EnumerateArray())
			{
				if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out long id) && id > 0)
					_adminPeerIds.Add(id);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K ADMIN : lecture whitelist impossible ({CheminAdminWhitelist}) -> {ex.Message}");
		}
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
		chunk.SauvegarderChunkSurDisque();
		SauvegarderFloreChunk(coord, chunk);
		SauvegarderPierresChunk(coord, coordY);
		SauvegarderArbresChunk(coord, chunk);
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
		return ecart <= Mathf.Max(0, ConstantesDimensionAbysse.DemiFenetrePaliersActifs);
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
		bool estAbysse = ActiverGenerationAbysse;
		int cibleY = estAbysse ? NormaliserCoordYAbysse(coordY) : 0;
		Vector3 obs = observation ?? (_obtenirPositionJoueur?.Invoke() ?? Vector3.Zero);
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

	public override void _PhysicsProcess(double delta)
	{
		bool hadModifications = _modificationEnCours;
		_modificationEnCours = false;

		// Récupérer les chunks générés par les workers (Main Thread uniquement)
		// SÉGRÉGATION : ne JAMAIS écraser un chunk chargé depuis le disque avec un chunk procédural.
		int integrationsWorkers = 0;
		while (integrationsWorkers < MaxIntegrationsWorkersParTick && _chunksGeneres.TryDequeue(out var result))
		{
			var cleGeneree = new Vector3I(result.coord.X, result.coordY, result.coord.Y);
			_chunksEnCoursGeneration.Remove(cleGeneree);
			_chunksEnGenerationActive--;
			if (TryGetChunkRuntime(result.coord, result.coordY, out var existant) && existant.EstChargeDepuisDisque)
				continue; // Chunk déjà ressuscité du disque — ignorer le résultat procédural.
			// Toujours intégrer la tranche générée : en Abysse, les couches Y doivent pouvoir se remplacer proprement
			// autour de la position courante sans rester bloquées sur une ancienne couche.
			DefinirChunkRuntime(result.coord, result.coordY, result.chunk);
			SynchroniserFrontieresAvecVoisinsCharges(result.coord, result.chunk);
			SpawnerArbresChunkAvecPrioriteSauvegarde(result.coord, result.chunk);
			if (ActiverGenerationAbysse)
			{
				// En Abysse, l'affichage du terrain est prioritaire: on envoie le chunk immédiatement
				// et on laisse l'ensemencement des roches suivre en arrière-plan.
				_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = result.coord, Donnees = result.chunk.ObtenirDonneesPourClient() });
				DeclencherEnsemencement(result.coord, result.chunk, TailleChunk, (coord, ch) => LibererRochesChunk(coord));
			}
			else
			{
				// Envoi client uniquement APRÈS stase remplie, sinon LibererRochesChunk trouve une liste vide
				DeclencherEnsemencement(result.coord, result.chunk, TailleChunk, (coord, ch) =>
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() }));
			}
			integrationsWorkers++;
		}

		// Manufacture parallèle : purge des obsolètes puis extraction radiale
		if (!hadModifications)
		{
			Vector3 posObs = _obtenirPositionJoueur?.Invoke() ?? Vector3.Zero;
			if (ActiverGenerationAbysse)
				PurgerRuntimeAbysseHorsFenetre(posObs);
			float rayonMaxCarrePurge = (RenderDistance + 1) * (RenderDistance + 1);
			_chunksEnAttenteEnvoi.RemoveAll(c =>
			{
				if (c.EstAbysse && !EstCoordYDansFenetrePaliersAbysse(c.CoordY, posObs))
					return true;
				if (_demandesForceesSansPurge.Contains(c.Cle3D))
					return false;
				float d2 = DistanceCarreeAuJoueur(c, posObs);
				return d2 > rayonMaxCarrePurge;
			});
			Vector3 posObservation = posObs;
			int demandesTraitees = 0;
			int chargesDisque = 0;
			int budgetChargesDisque = ActiverGenerationAbysse ? Mathf.Max(4, MaxChargesDisqueParTick * 5) : MaxChargesDisqueParTick;
			int budgetDemandes = ActiverGenerationAbysse
				? Mathf.Max(2, MaxDemandesChunksAbysseParTick + 2)
				: Mathf.Max(1, MaxDemandesChunksParTick);
			while (_chunksEnAttenteEnvoi.Count > 0 && _chunksEnGenerationActive < LancerMaxTaches && demandesTraitees < budgetDemandes)
			{
				demandesTraitees++;
				DemandeChunk demande = ExtraireChunkLePlusProche(_chunksEnAttenteEnvoi, posObservation);
				Vector2I chunkCible = demande.Coord;
				int coordYCible = demande.CoordY;
				Vector3I cleDemande = demande.Cle3D;
				_demandesEnAttenteSet.Remove(cleDemande);
				bool demandeForcee = _demandesForceesSansPurge.Remove(cleDemande);

				float distCarree = DistanceCarreeAuJoueur(demande, posObservation);
				float rayonMaxCarre = (RenderDistance + 1) * (RenderDistance + 1);
				if (!demandeForcee && distCarree > rayonMaxCarre)
				{
					// Même logique que le client : ExtraireChunkLePlusProche a retiré l’entrée — la remettre en file
					// sinon la demande disparaît à jamais (trou / monde qui n’atteint jamais RenderDistance).
					_chunksEnAttenteEnvoi.Add(demande);
					_demandesEnAttenteSet.Add(cleDemande);
					continue;
				}

				if (TryGetChunkRuntime(chunkCible, coordYCible, out var existant))
				{
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = existant.ObtenirDonneesPourClient() });
					continue;
				}

				Chunk_Serveur chunkActuel = null;

				// BRANCHE 1 : RÉSURRECTION PURE — AUCUN appel de génération. Le chunk part directement au Mesh.
				if (FichierChunkExiste(chunkCible, coordYCible))
				{
					if (chargesDisque >= budgetChargesDisque)
					{
						// On refile la demande pour la frame suivante afin d'éviter un pic I/O + désérialisation.
						_chunksEnAttenteEnvoi.Add(demande);
						_demandesEnAttenteSet.Add(cleDemande);
						continue;
					}
					chunkActuel = ChargerChunkDepuisDisque(chunkCible, coordYCible);
					if (chunkActuel == null)
						GD.PrintErr($"ZERO-K DIAG : Fallback procédural pour {chunkCible} après échec de chargement disque.");
					chargesDisque++;
					// RÈGLE D'ARCHITECTURE : GenererTerrainDeBase, GenererCoucheSurface, GenererEau, GenererArbres
					// ne sont JAMAIS appelés ici. Le chunk chargé est final.
				}

				// BRANCHE 2 : CRÉATION PROCÉDURALE — TOUTES les passes (terrain, surface, eau) UNIQUEMENT ici.
				if (chunkActuel == null)
				{
					lock (_verrouGeneration)
					{
						if (!_chunksEnCoursGeneration.Add(cleDemande))
							continue;
						_chunksEnGenerationActive++;
					}
					Vector2I coord = chunkCible;
					int yChunk = coordYCible;
					Task.Run(() =>
					{
						var chunk = CreerChunkServeur(coord, yChunk);
						// TOUTES les passes : GenererTerrainDeBase, GenererCoucheSurface, GenererEau — encapsulées dans GenererDonneesVoxel.
						chunk.GenererDonneesVoxel();
						var donnees = chunk.ObtenirDonneesPourClient();
						_chunksGeneres.Enqueue((coord, yChunk, chunk, donnees));
					});
					continue;
				}

				// BRANCHE COMMUNE : Chunk ressuscité. Pierres + Arbres. Spawn quand chunk demandé (visible écran).
				DefinirChunkRuntime(chunkCible, coordYCible, chunkActuel);
				SynchroniserFrontieresAvecVoisinsCharges(chunkCible, chunkActuel);
				RepousserBorduresChunkDisqueVersVoisinsProceduraux(chunkCible, chunkActuel);
				SpawnerArbresChunkAvecPrioriteSauvegarde(chunkCible, chunkActuel);
				if (!ChargerEtSpawnerPierresChunk(chunkCible, coordYCible))
				{
					if (ActiverGenerationAbysse)
					{
						// Reconnexion Abysse: ne pas bloquer l'envoi du chunk par l'ensemencement.
						_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = chunkActuel.ObtenirDonneesPourClient() });
						DeclencherEnsemencement(chunkCible, chunkActuel, TailleChunk, (coord, ch) => LibererRochesChunk(coord));
					}
					else
					{
						// Attendre que l'ensemencement asynchrone finisse AVANT d'envoyer le chunk au réseau
						DeclencherEnsemencement(chunkCible, chunkActuel, TailleChunk, (coord, ch) =>
							_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() }));
					}
				}
				else
				{
					// Si chargé depuis le disque, on envoie directement
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = chunkActuel.ObtenirDonneesPourClient() });
				}
			}
		}

		// Tapis roulant : 1 envoi au client par Tick (60 TPS)
		int envoisCeTick = 0;
		while (_fileEnvoiReseau.Count > 0 && envoisCeTick < MaxChunksEnvoiParTick)
		{
			ColisChunk colis = _fileEnvoiReseau.Dequeue();
			_onEnvoyerChunk?.Invoke(colis.Coord, colis.Donnees);
			// Verrou chronologique : la croûte est scellée (chunk envoyé) → on libère les roches de ce chunk vers la file de micro-dosage.
			LibererRochesChunk(colis.Coord);
			envoisCeTick++;
		}

		// Réveil des pierres dormantes : quand joueur dans 2 chunks, le terrain est chargé → on dégèle
		ReveillerPierresDansRayon();
		float facteurPressionSpawn = CalculerFacteurPressionSpawn();

		// Spawn progressif des ArbreVivant pour éviter les gros spikes au premier chargement.
		int nArbres = 0;
		int budgetArbresTick = Mathf.Max(1, Mathf.RoundToInt(CalculerBudgetSpawnAdaptatif(MaxArbresSpawnParTick) * facteurPressionSpawn));
		ulong t0Arbres = Time.GetTicksUsec();
		ulong budgetUsArbres = (ulong)Mathf.Max(110f, BudgetMsSpawnArbresParTick * 1000f * facteurPressionSpawn);
		while (nArbres < budgetArbresTick && _fileSpawnArbres.Count > 0)
		{
			if (Time.GetTicksUsec() - t0Arbres >= budgetUsArbres) break;
			var a = _fileSpawnArbres.Dequeue();
			if (!_chunks.ContainsKey(a.coord)) continue; // Chunk déjà déchargé entre-temps
			InstancierArbreVivant(a.pos, a.age, a.seed, a.indexBotanique, a.joursRattrapage);
			nArbres++;
		}

		// Goutte-à-goutte : pierres chargées depuis disque, instanciées quand chunk dessiné à l'écran
		int nPierres = 0;
		int budgetPierresTick = Mathf.Max(1, Mathf.RoundToInt(CalculerBudgetSpawnAdaptatif(MaxPierresParFrame) * Mathf.Clamp(facteurPressionSpawn * 0.95f, 0.2f, 1f)));
		ulong t0Pierres = Time.GetTicksUsec();
		ulong budgetUsPierres = (ulong)Mathf.Max(95f, BudgetMsSpawnPierresParTick * 1000f * Mathf.Clamp(facteurPressionSpawn * 0.9f, 0.2f, 1f));
		while (nPierres < budgetPierresTick && _filePierresAInstancier.Count > 0)
		{
			if (Time.GetTicksUsec() - t0Pierres >= budgetUsPierres) break;
			var (pos, id, idx, chim) = _filePierresAInstancier.Dequeue();
			// Plus la roche est loin du niveau d'eau (Y=103), plus elle peut prendre une forme cassée (2e moitié du cache)
			if (idx < 0)
			{
				float distEau = Mathf.Abs(pos.Y - NIVEAU_EAU);
				bool formesCassées = distEau > SeuilDistanceEauFormesCassées;
				idx = formesCassées ? -2 : -1;
			}
			GenererItemPhysique(pos, id, idx, chim);
			nPierres++;
		}

		_tempsDepuisVerifDecharge += (float)delta;
		if (_tempsDepuisVerifDecharge >= IntervalleEvaluationTectonique)
		{
			_tempsDepuisVerifDecharge = 0f;
			EvaluerDechargementChunks();
		}

		// Tapis roulant décharge : N chunks par frame (sauvegarde + décharge progressifs)
		ProcesserDechargeProgressive();

		// Eau runtime purement événementielle : on ne traite que la file des voxels réveillés par modification locale.
		if (_fileEau.Count == 0) return;
		_tickEauCourant++;
		int n = Math.Min(_fileEau.Count, MaxEauParTick);
		for (int i = 0; i < n; i++)
		{
			Vector3I pos = _fileEau.Dequeue();
			_eauActive.Remove(pos);
			if (!EstVoxelEau(pos)) continue;

			Vector3I posBas = pos + new Vector3I(0, -1, 0);
			if (posBas.Y < 0) { DefinirVoxel(pos, 0); continue; }

			if (EstVoxelAir(posBas))
			{
				DefinirVoxel(posBas, 4);
				DefinirVoxel(pos, 0);
				MemoriserFluxEau(pos, posBas);
				ActiverEau(posBas);
				ReveillerVoisins(pos);
				continue;
			}

			bool aPression = EstVoxelEau(pos + new Vector3I(0, 1, 0));
			foreach (var d in DirEauHoriz)
			{
				Vector3I pc = pos + d, pcb = pc + new Vector3I(0, -1, 0);
				if (!EstVoxelAir(pc)) continue;
				if (!PeutCoulerVers(pos, pc)) continue;
				bool auBord = EstVoxelAir(pcb);
				if (aPression || auBord)
				{
					DefinirVoxel(pc, 4);
					DefinirVoxel(pos, 0);
					MemoriserFluxEau(pos, pc);
					ActiverEau(pc);
					ReveillerVoisins(pos);
					break;
				}
			}
		}
	}

	private void ActiverEau(Vector3I pos)
	{
		if (_eauActive.Add(pos)) _fileEau.Enqueue(pos);
	}

	private bool PeutCoulerVers(Vector3I source, Vector3I destination)
	{
		if (!_antiRetourEau.TryGetValue(source, out var blocage)) return true;
		if (blocage.tickExpiration <= _tickEauCourant)
		{
			_antiRetourEau.Remove(source);
			return true;
		}
		return blocage.retourInterdit != destination;
	}

	private void MemoriserFluxEau(Vector3I source, Vector3I destination)
	{
		// Évite l'oscillation immédiate destination -> source.
		_antiRetourEau[destination] = (source, _tickEauCourant + DureeBlocageRetourEauTicks);
		if (_antiRetourEau.Count > 20000)
			_antiRetourEau.Clear();
	}

	private void ReveillerVoisins(Vector3I pos)
	{
		foreach (var d in DirVoisins)
			if (EstVoxelEau(pos + d)) ActiverEau(pos + d);
	}

	public void ReveillerEauAdjacente(Vector3 pointGlobal)
	{
		int gx = Mathf.FloorToInt(pointGlobal.X), gy = Mathf.FloorToInt(pointGlobal.Y), gz = Mathf.FloorToInt(pointGlobal.Z);
		var basePos = new Vector3I(gx, gy, gz);
		foreach (var d in DirReveil)
			if (EstVoxelEau(basePos + d)) ActiverEau(basePos + d);
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

	private string ObtenirNomDimensionNormalise()
	{
		string brut = string.IsNullOrWhiteSpace(NomDimension) ? "Dimension_Alpha" : NomDimension.Trim();
		return brut.Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
	}

	private string ObtenirDossierChunksRelatif()
	{
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string suffixeDimension = ObtenirNomDimensionNormalise();
		return $"user://saves/{nom}/chunks_{suffixeDimension}";
	}

	private string ObtenirCheminChunkRelatif(Vector2I coord, int coordY)
		=> $"{ObtenirDossierChunksRelatif()}/chunk_{coord.X}_{coordY}_{coord.Y}.bin";

	private bool FichierChunkExiste(Vector2I coord, int coordY)
	{
		return File.Exists(ProjectSettings.GlobalizePath(ObtenirCheminChunkRelatif(coord, coordY)));
	}

	private string ObtenirCheminSauvegarde(Vector2I coord, int coordY) => ObtenirCheminChunkRelatif(coord, coordY);

	/// <summary>Délègue au chunk la sauvegarde binaire. NE sauvegarde QUE si EstModifie.</summary>
	private void SauvegarderChunkSurDisque(Vector2I coord, Chunk_Serveur chunk)
	{
		chunk.SauvegarderChunkSurDisque();
	}

	/// <summary>Résurrection : chargement binaire via BinaryReader. Si fichier absent ou corrompu → régénération procédurale.</summary>
	private Chunk_Serveur ChargerChunkDepuisDisque(Vector2I coord, int coordY)
	{
		GD.Print($"ZERO-K DIAG : Tentative chargement Chunk {coord}...");
		string cheminGodot = ObtenirCheminSauvegarde(coord, coordY);
		string cheminAbsolu = ProjectSettings.GlobalizePath(cheminGodot);
		if (!File.Exists(cheminAbsolu))
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — fichier inexistant ({cheminGodot}).");
			return null;
		}
		int voxelCount = (TailleChunk + 1) * (HauteurMax + 1) * (TailleChunk + 1);
		int tailleAttendue = voxelCount * 9;
		byte[] donneesVoxels;
		try
		{
			using (var reader = new BinaryReader(File.Open(cheminAbsolu, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				byte version = reader.ReadByte();
				if (version != 1)
				{
					GD.PrintErr($"ZERO-K REJET : Chunk {coord} — version {version} non supportée ({cheminGodot}).");
					return null;
				}
				int tailleLu = reader.ReadInt32();
				if (tailleLu != tailleAttendue)
				{
					GD.PrintErr($"ZERO-K REJET : Chunk {coord} corrompu (taille {tailleLu} ≠ {tailleAttendue}) ({cheminGodot}). Régénération forcée.");
					return null;
				}
				donneesVoxels = reader.ReadBytes(tailleLu);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — erreur lecture ({cheminGodot}) : {ex.Message}");
			return null;
		}
		if (donneesVoxels == null || donneesVoxels.Length != tailleAttendue)
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} refusé ! Taille lue : {donneesVoxels?.Length ?? 0} | Attendue : {tailleAttendue} ({cheminGodot}).");
			return null;
		}
		GD.Print($"ZERO-K SUCCÈS : Chunk {coord} chargé depuis le disque ({donneesVoxels.Length} bytes).");
		var chunk = CreerChunkServeur(coord, coordY);
		if (!chunk.AppliquerTableauBytes(donneesVoxels))
		{
			GD.PrintErr($"ZERO-K REJET : Chunk {coord} — AppliquerTableauBytes a échoué ({cheminGodot}). Régénération forcée.");
			return null;
		}
		ChargerFloreChunk(coord, chunk);
		return chunk;
	}

	/// <summary>Sauvegarde l’inventaire flore du chunk (herbe/buissons retirés ou repoussés).</summary>
	private void SauvegarderFloreChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		string dossier = ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif() + "/");
		Directory.CreateDirectory(dossier);
		int coordY = chunk?.ChunkOffsetY ?? 0;
		string chemin = Path.Combine(dossier, $"chunk_{coord.X}_{coordY}_{coord.Y}_flore.bin");
		try
		{
			using (var w = new BinaryWriter(File.Open(chemin, FileMode.Create)))
			{
				w.Write(0x5A4B3346); // ZK3F
				w.Write(chunk.InventaireFlore.Count);
				foreach (var kv in chunk.InventaireFlore)
				{
					w.Write(kv.Key.X);
					w.Write(kv.Key.Y);
					w.Write(kv.Key.Z);
					w.Write(kv.Value);
				}
			}
		}
		catch (Exception ex) { GD.PrintErr($"ZERO-K : Erreur sauvegarde flore chunk {coord} : {ex.Message}"); }
	}

	/// <summary>Charge l’inventaire flore; fallback procédural si fichier absent.</summary>
	private void ChargerFloreChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (chunk == null) return;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif()), $"chunk_{coord.X}_{chunk.ChunkOffsetY}_{coord.Y}_flore.bin");
		if (!File.Exists(chemin))
		{
			chunk.RegenererInventaireFloreDepuisSurface();
			return;
		}
		try
		{
			chunk.InventaireFlore.Clear();
			using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				int magic = r.ReadInt32();
				if (magic != 0x5A4B3346)
				{
					chunk.RegenererInventaireFloreDepuisSurface();
					return;
				}
				int count = r.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					var pos = new Vector3I(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
					byte etat = r.ReadByte();
					chunk.InventaireFlore[pos] = etat;
				}
			}
			chunk.EnrichirBuissonsDepuisInventaireSiAbsents();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur chargement flore chunk {coord} : {ex.Message}");
			chunk.RegenererInventaireFloreDepuisSurface();
		}
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

	private void SpawnBlocChutant(Vector3 pos, byte mat, bool brancheTailléeBuisson = false, byte indexCouleurBaie = 0)
	{
		if (_parentPourBlocsChutants == null) return;
		var matTerrain = MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
		var bloc = BlocChutant.Creer(pos, mat, matTerrain, brancheTailléeBuisson, indexCouleurBaie);
		_parentPourBlocsChutants.AddChild(bloc);
		// Fibres (fauchage) : léger décalage vers le haut pour éviter d’être coincées dans le sol / la collision.
		Vector3 posPose = mat == 15 ? pos + new Vector3(0f, 0.12f, 0f) : pos;
		bloc.GlobalPosition = posPose;
	}

	/// <summary>Spawn branches et bûches qui tombent au sol quand un arbre est coupé.</summary>
	public void SpawnDebrisArbre(Vector3 baseArbre, int ageEnJours, uint seed)
	{
		if (_parentPourBlocsChutants == null) return;
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Mathf.Abs(baseArbre.X) * 73856 + Mathf.Abs(baseArbre.Z) * 19349 + seed);
		int nbBranches = Mathf.Clamp(ageEnJours * 2 + (int)(rng.Randf() * 4), 2, 12);
		int nbBuches = Mathf.Clamp(ageEnJours / 2 + (int)(rng.Randf() * 2), 1, 6);
		float offsetRayon = 0.8f + ageEnJours * 0.1f;
		for (int i = 0; i < nbBranches; i++)
		{
			float angle = (float)i / nbBranches * Mathf.Tau + rng.Randf() * 0.5f;
			float r = offsetRayon * (0.5f + rng.Randf() * 0.5f);
			Vector3 pos = baseArbre + new Vector3(Mathf.Cos(angle) * r, 0.5f + rng.Randf() * 0.3f, Mathf.Sin(angle) * r);
			SpawnBlocChutant(pos, BlocChutant.ID_BRANCHE);
		}
		for (int i = 0; i < nbBuches; i++)
		{
			float angle = (float)i / nbBuches * Mathf.Tau + rng.Randf() * 0.8f;
			float r = offsetRayon * (0.3f + rng.Randf() * 0.4f);
			Vector3 pos = baseArbre + new Vector3(Mathf.Cos(angle) * r, 0.6f + rng.Randf() * 0.4f, Mathf.Sin(angle) * r);
			SpawnBlocChutant(pos, BlocChutant.ID_BOIS);
		}
	}

	private const float NIVEAU_EAU = 103f;  // +1 m
	private const float DECALAGE_SPAWN_VERTICAL = 1.2f; // Légèrement au-dessus du terrain à la génération, tombe quand réveillé
	/// <summary>Rayon en chunks : objets dynamiques gelés se réveillent dans cette zone autour du joueur.</summary>
	private const int RAYON_ACTIVATION_PIERRES_CHUNKS = 5;

	/// <summary>Délai de synchronisation : attend 2 frames physiques, puis enfile sur le tapis roulant (ordre spatial logique). Si onStasePrete est fourni (chunk procédural), on enqueue l'envoi client seulement après la stase → évite LibererRochesChunk à vide.</summary>
	private async void DeclencherEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk, Action<Vector2I, Chunk_Serveur> onStasePrete = null)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		var positionsFiltrees = CollecterPositionsEnsemencement(chunkCoord, chunk, tailleChunk);
		var aEnfiler = new List<(Vector3 pos, int id, int indexCache, int indexChimique)>();
		foreach (var p in positionsFiltrees)
			aEnfiler.Add((p.pos, p.idMat, p.idxMorph, p.taille));
		MettreRochesEnStase(chunkCoord, aEnfiler);
		onStasePrete?.Invoke(chunkCoord, chunk);
	}

	/// <summary>Pré-crée les pools par matière rocheuse (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>).</summary>
	private void CreerPoolsRochesParTaille()
	{
		if (_parentPourBlocsChutants == null) return;
		int n = 0;
		for (int id = ItemPhysique.IdRocheMatiereMin; id <= ItemPhysique.IdRocheMatiereMax; id++)
		{
			_poolsRochesParTaille[id] = new List<RigidBody3D>();
			for (int i = 0; i < TaillePoolParType; i++)
			{
				var rb = CreerNouvelleRoche(id, 0, 2);
				_poolsRochesParTaille[id].Add(rb);
			}
			n++;
		}
		GD.Print($"ZERO-K : Pools roches par matière créés ({n} x {TaillePoolParType}).");
	}

	/// <summary>Collecte positions, ID matière (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>), morph (-1 = tirage), taille (0–4).</summary>
	private List<(Vector3 pos, int idMat, int idxMorph, int taille)> CollecterPositionsEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk)
	{
		var liste = new List<(Vector3 pos, int idMat, int idxMorph, int taille)>();
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(chunkCoord.X * 73856093 + chunkCoord.Y * 19349663 + SeedTerrain);

		for (float x = 0; x < tailleChunk; x += 3f)
		{
			for (float z = 0; z < tailleChunk; z += 3f)
			{
				if (rng.Randf() > 0.02f) continue;
				int lx = Mathf.Clamp(Mathf.FloorToInt(x), 0, (int)tailleChunk);
				int lz = Mathf.Clamp(Mathf.FloorToInt(z), 0, (int)tailleChunk);
				var (ySurface, idMatiere) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
				if (ySurface < 0) continue;

				Vector3 pointImpact = new Vector3(
					chunkCoord.X * tailleChunk + x + 0.5f,
					ySurface + 0.5f,
					chunkCoord.Y * tailleChunk + z + 0.5f
				);
				Vector3 pointDeSpawnSecurise = pointImpact + new Vector3(0, DECALAGE_SPAWN_VERTICAL, 0);

				if (idMatiere == 3 && pointImpact.Y < NIVEAU_EAU)
				{
					liste.Add((pointDeSpawnSecurise, ItemPhysique.IdRocheMatiereMin + ItemPhysique.IndexChimiqueSilex, -1, 1));
					continue;
				}

				int tailleSpawn = 0;
				float proba = rng.Randf();
				if (idMatiere == 1 || idMatiere == 3) tailleSpawn = 1;
				else if (idMatiere == 7 || idMatiere == 8) tailleSpawn = (proba > 0.4f) ? 1 : 2;
				else if (idMatiere == 5 || idMatiere == 6) tailleSpawn = (proba > 0.5f) ? 1 : 2;
				else if (idMatiere == 2)
				{
					if (proba < 0.40f) tailleSpawn = 1;
					else if (proba < 0.70f) tailleSpawn = 2;
					else if (proba < 0.90f) tailleSpawn = 3;
					else tailleSpawn = 4;
				}

				if (tailleSpawn != 0)
				{
					int chimIdx = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
					liste.Add((pointDeSpawnSecurise, ItemPhysique.IdRocheMatiereMin + chimIdx, -1, tailleSpawn));
				}
			}
		}
		return liste;
	}

	/// <summary>Chambre de stase : les roches attendent leur sol. Pas de spawn tant que le chunk n'est pas scellé (envoyé).</summary>
	private void MettreRochesEnStase(Vector2I coordChunk, List<(Vector3 pos, int id, int indexCache, int indexChimique)> pierres)
	{
		if (pierres.Count == 0) return;
		pierres.Sort((a, b) =>
		{
			int cmpX = a.pos.X.CompareTo(b.pos.X);
			if (cmpX != 0) return cmpX;
			int cmpZ = a.pos.Z.CompareTo(b.pos.Z);
			if (cmpZ != 0) return cmpZ;
			return a.pos.Y.CompareTo(b.pos.Y);
		});
		_rochesEnStase[coordChunk] = pierres;
	}

	/// <summary>Signal de fondation : chunk scellé (envoyé au client) → on transfère ses roches vers la file de micro-dosage (3 par frame).</summary>
	private void LibererRochesChunk(Vector2I coordChunk)
	{
		if (!_rochesEnStase.TryGetValue(coordChunk, out var liste)) return;
		foreach (var p in liste)
			_filePierresAInstancier.Enqueue(p);
		_rochesEnStase.Remove(coordChunk);
	}

	/// <summary>Enfile cailloux et silex sur le tapis roulant en ordre spatial logique (X, Z, Y) : terrain cohérent.</summary>
	private void EnfilerPierresSurTapisRoulant(List<(Vector3 pos, int id, int indexCache, int indexChimique)> pierres)
	{
		if (pierres.Count == 0) return;
		pierres.Sort((a, b) =>
		{
			int cmpX = a.pos.X.CompareTo(b.pos.X);
			if (cmpX != 0) return cmpX;
			int cmpZ = a.pos.Z.CompareTo(b.pos.Z);
			if (cmpZ != 0) return cmpZ;
			return a.pos.Y.CompareTo(b.pos.Y);
		});
		foreach (var p in pierres)
			_filePierresAInstancier.Enqueue((p.pos, p.id, p.indexCache, p.indexChimique));
	}

	/// <summary>Roches matière (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>) : <paramref name="indexCache"/> = morph (-1/-2 = tirage), <paramref name="indexChimique"/> = <see cref="ItemPhysique.IndexTailleRoche"/> (0–4).</summary>
	private void GenererItemPhysique(Vector3 position, int idObjet, int indexCache = -1, int indexChimique = -1)
	{
		if (_parentPourBlocsChutants == null) return;
		ItemPhysique rb = null;
		if (_poolsRochesParTaille.TryGetValue(idObjet, out var pool) && pool.Count > 0)
		{
			rb = pool[pool.Count - 1] as ItemPhysique;
			pool.RemoveAt(pool.Count - 1);
			if (rb != null)
			{
				rb.ID_Objet = idObjet;
				if (indexCache == -2)
					rb.IndexCacheMemoire = GD.RandRange(2, 3);
				else if (indexCache < 0)
					rb.IndexCacheMemoire = GD.RandRange(0, 3);
				else
					rb.IndexCacheMemoire = Mathf.Clamp(indexCache, 0, 3);
				rb.IndexTailleRoche = indexChimique >= 0 ? Mathf.Clamp(indexChimique, 0, 4) : 2;
				rb.IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet);
				rb.ReappliquerApparence();
				rb.Freeze = true; // Stase : ReveillerPierresDansRayon dégèle à 2 chunks (terrain solide)
			}
		}
		else
			rb = CreerNouvelleRoche(idObjet, indexCache, indexChimique);
		try
		{
			_parentPourBlocsChutants.AddChild(rb);
			rb.GlobalPosition = position;
			rb.Freeze = true; // Dormance : gravité seulement à 2 chunks du joueur (évite chute dans le vide)
			rb.SetMeta("DimensionId", _dimensionServeurId);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K CRASH ÉVITÉ : Objet physique échoué à l'instanciation. {ex.Message}");
			rb?.QueueFree();
		}
	}

	/// <summary>Crée une roche neuve (ItemPhysique = RigidBody3D racine). N'est pas ajoutée au parent.</summary>
	private ItemPhysique CreerNouvelleRoche(int idObjet, int indexCache, int indexTailleOuChim)
	{
		int morph;
		if (indexCache == -2)
			morph = GD.RandRange(2, 3);
		else if (indexCache < 0)
			morph = GD.RandRange(0, 3);
		else
			morph = Mathf.Clamp(indexCache, 0, 3);
		int taille = indexTailleOuChim >= 0 ? Mathf.Clamp(indexTailleOuChim, 0, 4) : 2;
		float rayon = ItemPhysique.RayonBaseRochesJoueur(taille);
		var item = new ItemPhysique
		{
			ID_Objet = idObjet,
			IndexCacheMemoire = morph,
			IndexTailleRoche = taille,
			IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet),
			Name = "ItemPhysique",
			// Morphologie appliquée sur le MeshInstance3D dans ItemPhysique._Ready (Jolt : pas d’échelle non uniforme sur RigidBody3D).
			Scale = Vector3.One
		};
		item.Mass = 1.0f;
		// Friction / amortissement : ItemPhysique._Ready → AppliquerPhysiqueRochePortee (évite conflit avec matériau 0,6).
		item.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = rayon, Height = rayon * 2f } });
		item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = rayon } });
		return item;
	}

	/// <summary>Rayon en unités : pierres gelées se réveillent quand joueur entre (2 chunks = terrain chargé).</summary>
	private float RayonActivationPierres => RAYON_ACTIVATION_PIERRES_CHUNKS * TailleChunk;

	private bool TerrainChargeAutourPosition(Vector3 posMonde)
	{
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(posMonde, TailleChunk);
		int rayon = Mathf.Clamp(RayonSecuriteTerrainReveilPierres, 0, 2);
		for (int dx = -rayon; dx <= rayon; dx++)
			for (int dz = -rayon; dz <= rayon; dz++)
				if (!_chunks.ContainsKey(new Vector2I(c.X + dx, c.Y + dz)))
					return false;
		return true;
	}

	/// <summary>Réveille les objets dynamiques dans le rayon, endort les lointains (charge CPU réduite côté serveur).</summary>
	private void ReveillerPierresDansRayon()
	{
		if (_parentPourBlocsChutants == null || _obtenirPositionJoueur == null) return;
		Vector3 posJoueur = _obtenirPositionJoueur();
		int dimensionActive = _obtenirDimensionActive?.Invoke() ?? _dimensionServeurId;
		float rayonCarre = RayonActivationPierres * RayonActivationPierres;
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			if (child is not RigidBody3D rb) continue;
			if (rb.HasMeta("DimensionId"))
			{
				int dimRb = rb.GetMeta("DimensionId").AsInt32();
				if (dimRb != dimensionActive)
					continue;
			}
			int id = 0;
			if (rb is ItemPhysique item)
				id = item.ID_Objet;
			else if (rb.HasMeta("ID_Matiere"))
				id = rb.GetMeta("ID_Matiere").AsInt32();
			if (!TryGetPositionMonde(rb, out Vector3 posRb)) continue;
			float distCarre = posRb.DistanceSquaredTo(posJoueur);
			bool structureFixe = id == 200 || id == Joueur.IdObjetRackBatons || id == Joueur.IdObjetRackBuches || id == Joueur.IdObjetCoffreBoisTier0;
			if (distCarre <= rayonCarre)
			{
				bool terrainPret = TerrainChargeAutourPosition(posRb);
				if (!structureFixe && terrainPret)
				{
					rb.Freeze = false; // Réveiller : gravité + collisions
					rb.Sleeping = false;
				}
				else if (!structureFixe)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
			}
			else
			{
				rb.LinearVelocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				rb.Sleeping = true;
				if (!structureFixe)
					rb.Freeze = true;
			}
		}
	}

	public void ForcerPulseReveilPierres()
	{
		ReveillerPierresDansRayon();
	}

	/// <summary>Évite l’erreur Godot <c>!is_inside_tree()</c> sur GlobalPosition (ex. sauvegarde pendant <c>_ExitTree</c>).</summary>
	private static bool TryGetPositionMonde(Node3D node, out Vector3 worldPos)
	{
		worldPos = default;
		if (node == null) return false;
		if (node.IsInsideTree())
		{
			worldPos = node.GlobalPosition;
			return true;
		}
		if (node.GetParent() is Node3D parent && parent.IsInsideTree())
		{
			worldPos = parent.GlobalTransform * node.Position;
			return true;
		}
		return false;
	}

	/// <summary>Sauvegarde les roches matière (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>) : morph dans index, taille dans chimique (octet).</summary>
	private void SauvegarderPierresChunk(Vector2I coord, int coordY)
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
	private bool ChargerEtSpawnerPierresChunk(Vector2I coord, int coordY)
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
	private void SauvegarderArbresChunk(Vector2I coord, Chunk_Serveur chunk)
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

	private void AssurerNoiseTemperatureArbres()
	{
		if (_noiseTemperatureArbres != null && _noiseTemperatureArbresSeed == SeedTerrain) return;
		_noiseTemperatureArbres = new FastNoiseLite();
		_noiseTemperatureArbres.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseTemperatureArbres.Seed = SeedTerrain + 2;
		_noiseTemperatureArbres.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseTemperatureArbres.FractalOctaves = 4;
		_noiseTemperatureArbres.Frequency = 0.0005f;
		_noiseTemperatureArbresSeed = SeedTerrain;
	}

	private void AssurerNoiseBiomeForetArbres()
	{
		if (_noiseBiomeForetArbres != null && _noiseBiomeForetArbresSeed == SeedTerrain) return;
		_noiseBiomeForetArbres = new FastNoiseLite();
		_noiseBiomeForetArbres.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseBiomeForetArbres.Seed = SeedTerrain + 77;
		_noiseBiomeForetArbres.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseBiomeForetArbres.FractalOctaves = 3;
		_noiseBiomeForetArbres.Frequency = 0.00028f;
		_noiseBiomeForetArbresSeed = SeedTerrain;
	}

	private void AssurerNoiseHumiditeArbres()
	{
		if (_noiseHumiditeArbres != null && _noiseHumiditeArbresSeed == SeedTerrain) return;
		_noiseHumiditeArbres = new FastNoiseLite();
		_noiseHumiditeArbres.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseHumiditeArbres.Seed = SeedTerrain + 3;
		_noiseHumiditeArbres.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseHumiditeArbres.FractalOctaves = 4;
		_noiseHumiditeArbres.Frequency = 0.0006f;
		_noiseHumiditeArbresSeed = SeedTerrain;
	}

	/// <summary>0=sans arbres, 1=bouleau seul, 2=mixte, 3=chêne seul (tempéré uniquement).</summary>
	private int DeterminerBiomeForetTempere(int gx, int gz)
	{
		float n = _noiseBiomeForetArbres?.GetNoise2D(gx, gz) ?? 0f;
		if (n < -0.44f) return 0;
		if (n < -0.08f) return 1;
		if (n < 0.28f) return 2;
		return 3;
	}

	private byte DeterminerIndexBotaniqueArbre(uint seedArbre, int gx, int gz, byte matSurface)
	{
		// Choix déterministe: un arbre garde la même essence entre chargements.
		uint h = (seedArbre * 1664525u) + 1013904223u;
		float r = (h & 0x00FFFFFFu) / 16777216f;
		// Mode test: injecte beaucoup de jungle partout, sans supprimer totalement les autres essences.
		float ratioJungleTest = Mathf.Clamp(RatioJungleModeTest, 0f, 0.95f);
		if (ModeEssencesPartoutTemporaire && r < ratioJungleTest)
			return LSystem_Botanique.IndexJungle;
		// APISARA : le bruit tempéré (neige/bouleau) ne correspond pas au climat surface ; canopée jungle + chênes.
		if (ActiverGenerationAbysse)
			return (byte)(r < 0.55f ? LSystem_Botanique.IndexJungle : LSystem_Botanique.IndexChene);
		AssurerNoiseTemperatureArbres();
		float temp = _noiseTemperatureArbres?.GetNoise2D(gx, gz) ?? 0f;
		AssurerNoiseHumiditeArbres();
		float humidite = _noiseHumiditeArbres?.GetNoise2D(gx, gz) ?? 0f;
		float humiditeNorm = (humidite + 1f) * 0.5f;
		// Arbres morts uniquement sur terre aride (ID 6), jamais sur herbe (ID 1).
		if (matSurface == 6 && temp > 0.12f && humiditeNorm < 0.48f)
			return (byte)(r < 0.50f ? LSystem_Botanique.IndexCheneMort : LSystem_Botanique.IndexBouleauMort);
		// Zone froide/neige: sapin majoritaire en froid modere, pin plus frequent en grand froid.
		if (temp < -0.32f)
			return (byte)(r < 0.72f ? LSystem_Botanique.IndexPin : LSystem_Botanique.IndexSapin);
		if (temp < -0.15f)
			return (byte)(r < 0.76f ? LSystem_Botanique.IndexSapin : LSystem_Botanique.IndexPin);
		// Jungle: très humide + chaud (on garde les zones neigeuses inchangées).
		if (temp > 0.22f && humidite > 0.62f)
			return (byte)(r < 0.70f ? LSystem_Botanique.IndexJungle : LSystem_Botanique.IndexChene);
		AssurerNoiseBiomeForetArbres();
		int biome = DeterminerBiomeForetTempere(gx, gz);
		if (biome == 1) return LSystem_Botanique.IndexBouleau; // zone bouleaux
		if (biome == 3) return LSystem_Botanique.IndexChene;   // zone chênes
		// Mixte (et vieux saves en zone sans arbres): mélange local d'essences.
		return (byte)(r < 0.50f ? LSystem_Botanique.IndexBouleau : LSystem_Botanique.IndexChene);
	}

	/// <summary>Spawn les ArbreVivant 3D pour ce chunk (procédural ou chargé).</summary>
	private void SpawnerArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (_parentPourArbres == null || chunk.InventaireArbres.Count == 0) return;
		AssurerPoolSeedsArbresPregen();
		foreach (var kv in chunk.InventaireArbres)
		{
			// Base collée au sol (Y - 0.5 pour éviter troncs flottants)
			Vector3 pos = new Vector3(kv.Key.X + 0.5f, kv.Key.Y - 0.5f, kv.Key.Z + 0.5f);
			int age = Mathf.Max(1, kv.Value.Stage + 1);
			int lx = kv.Key.X - coord.X * chunk.TailleChunk;
			int lz = kv.Key.Z - coord.Y * chunk.TailleChunk;
			var (_, matSurface) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
			byte indexBotanique = DeterminerIndexBotaniqueArbre(kv.Value.Seed, kv.Key.X, kv.Key.Z, matSurface);
			uint seedPregen = SelectionnerSeedArbreDepuisPool(indexBotanique, age, kv.Key.X, kv.Key.Z, kv.Value.Seed);
			_fileSpawnArbres.Enqueue((coord, pos, age, seedPregen, indexBotanique, 0));
		}
	}

	/// <summary>Priorité au disque: si un save arbres existe, on le charge; sinon fallback procédural du chunk.</summary>
	private void SpawnerArbresChunkAvecPrioriteSauvegarde(Vector2I coord, Chunk_Serveur chunk)
	{
		if (ChargerArbresChunk(coord, chunk))
			return;
		// Migration : un chunk APISARA chargé du disque AVANT que les arbres soient persistés (ancien build) a un
		// InventaireArbres vide. On rejoue UNE fois la passe procédurale arbres (rules déterministes) pour récupérer
		// la canopée. Le drapeau EstModifie force l'écriture du fichier *_arbres.bin à la prochaine sauvegarde.
		if (ActiverGenerationAbysse && chunk != null && chunk.EstChargeDepuisDisque && chunk.InventaireArbres.Count == 0)
		{
			chunk.RegenererInventaireArbresProcedural();
			if (chunk.InventaireArbres.Count > 0)
				chunk.MarquerModifie();
		}
		SpawnerArbresChunk(coord, chunk);
	}

	private void InstancierArbreVivant(Vector3 pos, int age, uint seed, byte indexBotanique, int joursRattrapage)
	{
		if (_parentPourArbres == null) return;
		var arbre = new ArbreVivant
		{
			AgeEnJours = Mathf.Max(1, age),
			ResistanceActuelle = ArbreVivant.ResistanceMaxPourAge(Mathf.Max(1, age)),
			Seed = seed,
			IndexBotanique = indexBotanique
		};
		_parentPourArbres.AddChild(arbre);
		arbre.GlobalPosition = pos;
		if (joursRattrapage > 0)
			arbre.RattraperCroissance(joursRattrapage, pos);
	}

	/// <summary>Charge et spawn les arbres depuis disque. Rattrape la croissance du temps passé hors-ligne.</summary>
	private bool ChargerArbresChunk(Vector2I coord, Chunk_Serveur chunk)
	{
		if (_parentPourArbres == null) return false;
		int coordY = chunk?.ChunkOffsetY ?? 0;
		string chemin = Path.Combine(ProjectSettings.GlobalizePath(ObtenirDossierChunksRelatif()), $"chunk_{coord.X}_{coordY}_{coord.Y}_arbres.bin");
		if (!File.Exists(chemin)) return false;
		try
		{
			using (var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read)))
			{
				int magic = r.ReadInt32();
				int jourDeSauvegarde = 0;
				long unixSauvegarde = 0L;
				bool formatV3 = magic == MagicArbresV3;
				bool formatV4 = magic == MagicArbresV4;
				bool formatV5 = magic == MagicArbresV5;
				bool formatV6 = magic == MagicArbresV6;
				if (magic == MagicArbresV2 || formatV3 || formatV4 || formatV5 || formatV6) // V2+ avec jour de sauvegarde
					jourDeSauvegarde = r.ReadInt32();
				if (formatV4 || formatV5 || formatV6)
					unixSauvegarde = r.ReadInt64();
				else if (magic != 0x5A4B3250 && !formatV3 && magic != MagicArbresV2)
					return false; // Format inconnu

				int joursEcoules = CalculerJoursRattrapageArbres(jourDeSauvegarde, unixSauvegarde);
				int count = r.ReadInt32();

				for (int i = 0; i < count; i++)
				{
					int gx = r.ReadInt32(), gy = r.ReadInt32(), gz = r.ReadInt32();
					int ageSauvegarde;
					byte indexBotaniqueSauvegarde;
					uint seedSauvegarde = 0u;
					if (magic == MagicArbresV2 || formatV3 || formatV4 || formatV5 || formatV6)
					{
						ageSauvegarde = r.ReadInt32();
						indexBotaniqueSauvegarde = (formatV3 || formatV4 || formatV5 || formatV6) ? r.ReadByte() : LSystem_Botanique.IndexChene;
						if (formatV5 || formatV6)
							seedSauvegarde = r.ReadUInt32();
					}
					else
					{
						byte stage = r.ReadByte();
						seedSauvegarde = r.ReadUInt32(); // seed legacy (v1)
						ageSauvegarde = stage + 1; // Ancien format Stage 0-4 → age 1-5
						indexBotaniqueSauvegarde = LSystem_Botanique.IndexChene;
					}

					// Migration rétrocompatible:
					// formats <= V5 sauvegardaient Y avec un cast int sur (racineY - 0.5),
					// ce qui perdait 1 bloc. On corrige ici pour remonter les arbres.
					if (!formatV6)
						gy += 1;
					Vector3 pos = new Vector3(gx + 0.5f, gy - 0.5f, gz + 0.5f);
					uint seedHashPos = (uint)((gx * 73856093) ^ (gz * 19349663));
					uint seedArbre = seedSauvegarde != 0u ? seedSauvegarde : seedHashPos;
					int ageCharge = Mathf.Max(1, ageSauvegarde);
					int lx = gx - coord.X * chunk.TailleChunk;
					int lz = gz - coord.Y * chunk.TailleChunk;
					var (_, matSurface) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
					byte indexBotanique = (formatV3 || formatV4 || formatV5 || formatV6) ? indexBotaniqueSauvegarde : DeterminerIndexBotaniqueArbre(seedArbre, gx, gz, matSurface);
					_fileSpawnArbres.Enqueue((coord, pos, ageCharge, seedArbre, indexBotanique, joursEcoules));
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K : Erreur chargement arbres chunk {coord} : {ex.Message}");
			return false;
		}
	}

	private int CalculerJoursRattrapageArbres(int jourDeSauvegarde, long unixSauvegarde)
	{
		int jourActuel = GameState.Instance != null ? GameState.Instance.JourAbsolu : 0;
		int joursJeu = Mathf.Max(0, jourActuel - jourDeSauvegarde);
		if (unixSauvegarde <= 0L) return joursJeu;
		long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		long deltaSec = Math.Max(0L, unixNow - unixSauvegarde);
		int joursReels = (int)(deltaSec / 86400L);
		return Mathf.Max(joursJeu, joursReels);
	}

	/// <summary>Vide la file de spawn arbres avant sauvegarde pour éviter les fichiers vides lors d'un reload rapide.</summary>
	private void ForcerInstanciationArbresEnAttente(Vector2I? filtreCoord = null)
	{
		if (_fileSpawnArbres.Count == 0) return;
		var restant = new Queue<(Vector2I coord, Vector3 pos, int age, uint seed, byte indexBotanique, int joursRattrapage)>();
		while (_fileSpawnArbres.Count > 0)
		{
			var a = _fileSpawnArbres.Dequeue();
			bool coordOk = !filtreCoord.HasValue || a.coord == filtreCoord.Value;
			if (!coordOk)
			{
				restant.Enqueue(a);
				continue;
			}
			if (!_chunks.ContainsKey(a.coord)) continue;
			InstancierArbreVivant(a.pos, a.age, a.seed, a.indexBotanique, a.joursRattrapage);
		}
		while (restant.Count > 0)
			_fileSpawnArbres.Enqueue(restant.Dequeue());
	}

	/// <summary>Retire du monde les ArbreVivant dont la position est dans le chunk (décharge).</summary>
	private void RetirerArbresChunk(Vector2I coord)
	{
		if (_parentPourArbres == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var aRetirer = new List<Node>();
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is not ArbreVivant) continue;
			if (n is not Node3D n3 || !TryGetPositionMonde(n3, out Vector3 p)) continue;
			if (p.X >= xMin && p.X < xMax && p.Z >= zMin && p.Z < zMax)
				aRetirer.Add(n);
		}
		foreach (var n in aRetirer)
		{
			_parentPourArbres.RemoveChild(n);
			n.QueueFree();
		}
	}

	/// <summary>Retire du monde les pierres/silex dont la position est dans le chunk ; remet dans le pool de la taille si possible.</summary>
	private void RetirerPierresChunk(Vector2I coord)
	{
		if (_parentPourBlocsChutants == null) return;
		float xMin = coord.X * TailleChunk;
		float xMax = (coord.X + 1) * TailleChunk;
		float zMin = coord.Y * TailleChunk;
		float zMax = (coord.Y + 1) * TailleChunk;
		var aRetirer = new List<Node>();
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			var item = child as ItemPhysique ?? child.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			if (item == null) continue;
			if (!ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) continue;
			if (child is not Node3D n3p || !TryGetPositionMonde(n3p, out Vector3 pos)) continue;
			if (pos.X >= xMin && pos.X < xMax && pos.Z >= zMin && pos.Z < zMax)
				aRetirer.Add(child);
		}
		foreach (var n in aRetirer)
		{
			var item = n as ItemPhysique ?? n.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			int id = item?.ID_Objet ?? 0;
			_parentPourBlocsChutants.RemoveChild(n);
			// Les éclats de fracture sont créés à l'instant, jamais remis au pool (sinon roches infinies).
			if (item != null && item.EstEclatFracture)
			{
				n.QueueFree();
				continue;
			}
			if (n is RigidBody3D rb && ItemPhysique.EstIdRocheMatiere(id) && _poolsRochesParTaille.TryGetValue(id, out var pool) && pool.Count < TaillePoolParType)
			{
				rb.Freeze = true; // En pool = figé pour réutilisation ; dégelé à la sortie (GenererItemPhysique)
				pool.Add(rb);
			}
			else
				n.QueueFree();
		}
	}

	/// <summary>Croissance des arbres 3D : VieillirUnJour sur chaque ArbreVivant. Appelé au changement de jour (minuit).</summary>
	public void FairePousserArbresDuJour()
	{
		if (_parentPourArbres == null) return;
		foreach (Node n in _parentPourArbres.GetChildren())
		{
			if (n is ArbreVivant arbre)
				arbre.VieillirUnJour();
		}
		GD.Print("ZERO-K : Croissance des arbres du jour appliquée.");
	}

	public void AppliquerDestructionGlobale(Vector3 pointImpact, float rayon, float forceDegats = 5.0f, int peerDemandeur = -1)
	{
		_modificationEnCours = true;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.DetruireVoxel(pointImpact, rayon, forceDegats);
					}
				}
			return;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				Vector2I coord = new Vector2I(cx, cz);
				var chunk = ObtenirOuCreerChunk(coord);
				chunk.DetruireVoxel(pointImpact, rayon, forceDegats);
			}
	}

	public void AppliquerFauchageGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.FaucherFlore(pointImpact, rayon);
					}
				}
			return;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				chunk.FaucherFlore(pointImpact, rayon);
			}
	}

	public bool AppliquerFauchageFauneGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		bool aFauche = false;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (chunk.FaucherFloreSansLoot(pointImpact, rayon))
							aFauche = true;
					}
				}
			return aFauche;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				if (chunk.FaucherFloreSansLoot(pointImpact, rayon))
					aFauche = true;
			}
		return aFauche;
	}

	public bool ExisteGazonFauneGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (chunk.ExisteGazonDansRayon(pointImpact, rayon))
							return true;
					}
				}
			return false;
		}
		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				if (chunk.ExisteGazonDansRayon(pointImpact, rayon))
					return true;
			}
		return false;
	}

	/// <summary>Récolte ciblée de buisson : 0=hachette (branche), 1=dague, 2=pelle (replantable).</summary>
	public bool RecolterBuissonGlobal(Vector3 pointImpact, float rayon, byte modeRecolte)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (chunk.RecolterBuisson(pointImpact, rayon, modeRecolte))
							return true;
					}
				}
			return false;
		}
		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				if (chunk.RecolterBuisson(pointImpact, rayon, modeRecolte))
					return true;
			}
		return false;
	}

	/// <summary>Détection d’un buisson sous la visée (sans mutation), utile pour le minage maintenu.</summary>
	public bool EssayerDetecterBuissonGlobal(Vector3 pointImpact, float rayon, out Vector3 posBuisson, out byte typeFlore)
	{
		posBuisson = Vector3.Zero;
		typeFlore = 0;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		float meilleureDist2 = float.MaxValue;
		bool trouve = false;
		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte type))
							continue;
						float d2 = pos.DistanceSquaredTo(pointImpact);
						if (!trouve || d2 < meilleureDist2)
						{
							trouve = true;
							meilleureDist2 = d2;
							posBuisson = pos;
							typeFlore = type;
						}
					}
				}
		}
		else
		{
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
					if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte type))
						continue;
					float d2 = pos.DistanceSquaredTo(pointImpact);
					if (!trouve || d2 < meilleureDist2)
					{
						trouve = true;
						meilleureDist2 = d2;
						posBuisson = pos;
						typeFlore = type;
					}
				}
		}
		return trouve;
	}

	/// <summary>Plante un buisson au point visé (terre plate). Retourne false si la zone n'est pas valide.</summary>
	public bool PlanterBuissonGlobal(Vector3 pointImpact, byte typeFlore)
	{
		Vector2I coord = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z, TailleChunk);
		var chunk = ObtenirOuCreerChunk(coord);
		return chunk.PlanterBuisson(pointImpact, typeFlore);
	}

	/// <summary>Cueille les baies d’un buisson plein sous la visée (le buisson devient vide).</summary>
	public bool RecolterBaiesBuissonGlobal(Vector3 pointImpact, float rayon, out int quantiteBaies, out byte indexCouleurBaie)
	{
		quantiteBaies = 0;
		indexCouleurBaie = 0;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		Vector2I meilleurChunk = default;
		int meilleurCoordYChunk = 0;
		Vector3 meilleurePos = Vector3.Zero;
		float meilleureDist2 = float.MaxValue;
		bool trouve = false;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte typeFlore) || !Chunk_Serveur.EstBuissonPlein(typeFlore))
							continue;
						float d2 = pos.DistanceSquaredTo(pointImpact);
						if (!trouve || d2 < meilleureDist2)
						{
							trouve = true;
							meilleureDist2 = d2;
							meilleurePos = pos;
							meilleurChunk = coord;
							meilleurCoordYChunk = coordY;
						}
					}
				}
		}
		else
		{
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
					if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte typeFlore) || !Chunk_Serveur.EstBuissonPlein(typeFlore))
						continue;
					float d2 = pos.DistanceSquaredTo(pointImpact);
					if (!trouve || d2 < meilleureDist2)
					{
						trouve = true;
						meilleureDist2 = d2;
						meilleurePos = pos;
						meilleurChunk = new Vector2I(cx, cz);
						meilleurCoordYChunk = 0;
					}
				}
		}

		if (!trouve) return false;
		var cible = ActiverGenerationAbysse
			? ObtenirOuCreerChunk(meilleurChunk, meilleurCoordYChunk)
			: ObtenirOuCreerChunk(meilleurChunk);
		return cible.RecolterBaiesBuisson(meilleurePos, rayon, out quantiteBaies, out indexCouleurBaie);
	}

	public void AppliquerCreationGlobale(Vector3 pointImpact, Vector3 normale, float rayon, int idMatiere = 1)
	{
		_modificationEnCours = true;
		Vector3 pointCible = pointImpact + (normale * 0.1f); // Réduit pour éviter les blocs flottants
		byte matiere = (byte)Mathf.Clamp(idMatiere, 0, 255);
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X - rayon, pointCible.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X + rayon, pointCible.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X, pointCible.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X, pointCible.Z + rayon, TailleChunk).Y;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointCible.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
			{
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.CreerMatiere(pointCible, rayon, matiere);
					}
				}
			}
			return;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
		{
			for (int cz = czMin; cz <= czMax; cz++)
			{
				Vector2I coord = new Vector2I(cx, cz);
				var chunk = ObtenirOuCreerChunk(coord);
				chunk.CreerMatiere(pointCible, rayon, matiere);
			}
		}
	}

	/// <summary>
	/// Comble uniquement l’air entre le premier solide rencontré sous les pieds et la surface (disque XZ), sur une profondeur max.
	/// Sans solide dans la fenêtre : aucun remblai (évite une colonne pleine dans le ciel quand le portail était mal aligné).
	/// Réplication via <see cref="Chunk_Serveur.ModifierVoxelEtNotifier"/>.
	/// </summary>
	public void RemplirSocleSousPortail(Vector3 centreMondeXZ, float ySurfaceTerrain, int rayonDemiCoteVoxels, int profondeurMaxVersLeBas)
	{
		rayonDemiCoteVoxels = Mathf.Max(0, rayonDemiCoteVoxels);
		profondeurMaxVersLeBas = Mathf.Max(1, profondeurMaxVersLeBas);
		int gx0 = Mathf.FloorToInt(centreMondeXZ.X);
		int gz0 = Mathf.FloorToInt(centreMondeXZ.Z);
		int yTop = Mathf.FloorToInt(ySurfaceTerrain);
		int yScanMin = yTop - profondeurMaxVersLeBas;
		const int yMondeDepuis = 3;
		yScanMin = Mathf.Max(yMondeDepuis, yScanMin);

		int rCarre = rayonDemiCoteVoxels * rayonDemiCoteVoxels;
		const byte idTerre = 2;
		const byte idHerbe = 1;

		for (int dx = -rayonDemiCoteVoxels; dx <= rayonDemiCoteVoxels; dx++)
		{
			for (int dz = -rayonDemiCoteVoxels; dz <= rayonDemiCoteVoxels; dz++)
			{
				if (dx * dx + dz * dz > rCarre)
					continue;
				int gx = gx0 + dx;
				int gz = gz0 + dz;
				Gestionnaire_Monde.WorldToChunkAndLocal(gx, gz, TailleChunk, out Vector2I coordChunk, out int lx, out int lz);
				if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk)
					continue;

				int? ySolideSousPied = null;
				for (int gy = yTop; gy >= yScanMin; gy--)
				{
					int coordYSlice = CoordYDepuisMondeY(gy, HauteurMax);
					Chunk_Serveur chunkScan = ObtenirOuCreerChunk(coordChunk, coordYSlice);
					int lyScan = LocalYDepuisMondeY(gy, HauteurMax);
					if (!chunkScan.EstVoxelAir(lx, lyScan, lz))
					{
						ySolideSousPied = gy;
						break;
					}
				}

				if (!ySolideSousPied.HasValue)
					continue;

				for (int gy = ySolideSousPied.Value + 1; gy <= yTop; gy++)
				{
					int coordYSlice = CoordYDepuisMondeY(gy, HauteurMax);
					Chunk_Serveur chunk = ObtenirOuCreerChunk(coordChunk, coordYSlice);
					int ly = LocalYDepuisMondeY(gy, HauteurMax);
					if (!chunk.EstVoxelAir(lx, ly, lz))
						continue;
					byte id = gy == yTop ? idHerbe : idTerre;
					chunk.ModifierVoxelEtNotifier(lx, ly, lz, id);
				}
			}
		}
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
		if (localX == 0 && _chunks.TryGetValue(new Vector2I(cx - 1, cz), out var vx) && vx.ChunkOffsetY == chunkY)
			vx.SetVoxelLocal(TailleChunk, localY, localZ, id);
		if (localX == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx + 1, cz), out var vxp) && vxp.ChunkOffsetY == chunkY)
			vxp.SetVoxelLocal(0, localY, localZ, id);
		if (localZ == 0 && _chunks.TryGetValue(new Vector2I(cx, cz - 1), out var vz) && vz.ChunkOffsetY == chunkY)
			vz.SetVoxelLocal(localX, localY, TailleChunk, id);
		if (localZ == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx, cz + 1), out var vzp) && vzp.ChunkOffsetY == chunkY)
			vzp.SetVoxelLocal(localX, localY, 0, id);
		if (localX == 0 && localZ == 0 && _chunks.TryGetValue(new Vector2I(cx - 1, cz - 1), out var vxz) && vxz.ChunkOffsetY == chunkY)
			vxz.SetVoxelLocal(TailleChunk, localY, TailleChunk, id);
		if (localX == TailleChunk - 1 && localZ == 0 && _chunks.TryGetValue(new Vector2I(cx + 1, cz - 1), out var vxpz) && vxpz.ChunkOffsetY == chunkY)
			vxpz.SetVoxelLocal(0, localY, TailleChunk, id);
		if (localX == 0 && localZ == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx - 1, cz + 1), out var vxzp) && vxzp.ChunkOffsetY == chunkY)
			vxzp.SetVoxelLocal(TailleChunk, localY, 0, id);
		if (localX == TailleChunk - 1 && localZ == TailleChunk - 1 && _chunks.TryGetValue(new Vector2I(cx + 1, cz + 1), out var vxpzp) && vxpzp.ChunkOffsetY == chunkY)
			vxpzp.SetVoxelLocal(0, localY, 0, id);
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
		int stageMin = stageObservation - Mathf.Max(0, ConstantesDimensionAbysse.DemiFenetrePaliersActifs);
		int stageMax = stageObservation + Mathf.Max(0, ConstantesDimensionAbysse.DemiFenetrePaliersActifs);
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
		Vector3 posJoueur = _obtenirPositionJoueur();
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
		Vector3 posJoueur = _obtenirPositionJoueur?.Invoke() ?? Vector3.Zero;
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