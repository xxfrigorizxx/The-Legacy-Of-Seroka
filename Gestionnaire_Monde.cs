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
	[Export] public int RenderDistance = 200;
	[Export] public int RenderDistanceDetailChunks = 15;
	[Export] public int RayonQualiteProcheChunks = 7;
	[Export] public int RayonGazonVisibleChunks = 12;
	[Export] public int RayonBuissonsVisibleChunks = 24;
	[Export] public bool ActiverHorizonLod = false;
	[Export] public int RayonHorizonChunks = 72;
	[Export] public float PasHorizonMetres = 20f;
	[Export] public bool ActiverCullingCameraChunks = true;
	[Export] public float AngleCullingCameraDeg = 135f;
	[Export] public int MargeChunksToujoursVisibles = 12;
	/// <summary>Requêtes réseau / chargement par frame côté client. Monde gigantesque : 4 est trop lent pour que le sol et les collisions suivent la marche.</summary>
	[Export] public int MaxChunksParFrame = 14;
	[Export] public bool ForcerAlignementSolAuChargement = true;
	/// <summary>Fuseau horaire du Monde 1. Québec = -5, Paris = +1, UTC = 0.</summary>
	[Export] public double FuseauHoraireHeures = -5;
	[Export] public bool PreGenererAuDemarrage = false;
	[Export] public int RayonPreGeneration = 2;
	[Export] public bool ModeEssencesPartoutTemporaire = false;
	[Export] public float RatioJungleModeTest = 0.30f;
	[Export] public bool ActiverAutosauvegarde = true;
	[Export] public float IntervalleAutosauvegardeSecondes = 45f;
	[Export] public int MaxChunksAutosauvegardeParCycle = 4;
	[Export] public Material MaterielTerrain;
	/// <summary>Matériau eau (océan). Créé automatiquement dans _Ready à partir de EauTriplanar.gdshader. Non exposé à l'éditeur.</summary>
	public Material MaterielEau;
	/// <summary>Échelle du gazon (grass.glb) sur ID 1. Modifier pour ajuster la taille partout.</summary>
	[Export] public float EchelleGazon = 2f;
	public int RayonMondeChunks = 1000;

	/// <summary>Si true, utilise Monde_Serveur + Monde_Client (Solo/MMORPG). Si false, legacy Generateur_Voxel.</summary>
	[Export] public bool UseArchitectureReseau = true;

	// Files pour le mode legacy (Generateur_Voxel)
	private ConcurrentQueue<System.Action> _misesAJourMainThread = new ConcurrentQueue<System.Action>();
	public ConcurrentQueue<System.Action> _misesAJourUrgentes = new ConcurrentQueue<System.Action>();

	private CharacterBody3D _joueur;
	private Monde_Serveur _mondeServeur;
	private Monde_Client _mondeClient;
	private NetworkManager _networkManager;
	private Label _labelCoords;
	private CanvasLayer _repereCentreLayer;
	/// <summary>Overlay "Chargement du monde..." affiché tant que la collision du chunk de spawn n'est pas prête.</summary>
	private CanvasLayer _overlayChargement;
	private double _secondesOverlayChargement;
	private Vector3 _spawnInitialEnAttente;
	private bool _spawnDoitEtreAligneAuSol;
	private bool _spawnAligneAuSol;
	private bool _etatPersistantRestaure;
	private double _secondesDormanceObjets;
	private const int RayonDormanceObjetsChunks = 5;
	[Export] public int RayonSecuriteTerrainObjetsChunks = 1;
	private const float NiveauEauOcean = 103f;
	private Area3D _oceanPhysique;
	private Node3D _conteneurEffetsEau;
	private readonly HashSet<ulong> _corpsDansOcean = new HashSet<ulong>();
	private StandardMaterial3D _materielEclaboussureEau;
	private bool _chargementCycleSolaire;
	private double _secondesDepuisAutosauvegarde;

	// Legacy
	private List<Vector2I> _chunksACharger = new List<Vector2I>();
	private bool _radarLegacyEnCours;
	private Dictionary<Vector2I, Node3D> _chunks = new Dictionary<Vector2I, Node3D>();
	private PackedScene _sceneChunk;
	private Vector2I _ancienChunkJoueur = new Vector2I(-99999, -99999);

	public void EnqueueMiseAJourMainThread(System.Action action) => _misesAJourMainThread.Enqueue(action);
	public void EnqueueMiseAJourUrgente(System.Action action) => _misesAJourUrgentes.Enqueue(action);

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

	/// <summary>Vrai si le chunk sous les pieds du joueur a sa collision construite (évite chute libre au spawn).</summary>
	public bool EstSpawnPret()
	{
		if (_joueur == null) return false;
		Vector3 pos = ObtenirPointReferenceSpawn();
		if (UseArchitectureReseau)
		{
			if (_mondeClient == null) return false;
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

	private Vector3 ObtenirPointReferenceSpawn()
	{
		if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
			return _spawnInitialEnAttente;
		return _joueur != null ? _joueur.GlobalPosition : _spawnInitialEnAttente;
	}

	private bool ChunkEtVoisinsCardinauxPretsAuPoint(Vector3 point)
	{
		if (!UseArchitectureReseau) return true;
		if (_mondeClient == null) return false;
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
	private float _tempsEcoulement;
	private const float TICK_EAU = 0.05f;
	private const int MaxEauParTick = 32;
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

	/// <summary>Garantit un spawn au-dessus du terrain local pour éviter un joueur sous la map, et ramène au voisinage du sol si la position est restée « dans le ciel ».</summary>
	private Vector3 AssurerSpawnAuDessusDuSol(Vector3 pos)
	{
		int hauteurTerrain = Generateur_Voxel.ObtenirHauteurTerrainMonde((int)pos.X, (int)pos.Z, SeedTerrain);
		float ySurfaceApprox = hauteurTerrain + 1.02f;
		float yCibleAuSol = _joueur is Joueur jo
			? jo.CalculerYOriginePourPiedsSurSurface(ySurfaceApprox)
			: hauteurTerrain + 2.85f;

		// Sauvegarde / fallback raycast : Y énorme → le personnage « vole » jusqu’à ce que la collision existe.
		if (pos.Y > hauteurTerrain + 18f)
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

	/// <summary>Raycast vertical vers le terrain/collision du monde. Retourne true si un point sol est trouvé.</summary>
	private bool EssayerTrouverSolParRaycast(Vector3 positionApprox, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;
		Vector3 debut = positionApprox + Vector3.Up * 900f;
		Vector3 fin = positionApprox + Vector3.Down * 900f;
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		if (_joueur != null && _joueur.GetRid().IsValid)
		{
			var excludes = new Godot.Collections.Array<Rid> { _joueur.GetRid() };
			query.Exclude = excludes;
		}

		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || !hit.ContainsKey("position")) return false;
		pointSol = (Vector3)hit["position"];
		return true;
	}

	/// <summary>
	/// Finalise le spawn du nouveau monde : la map/collision doit être prête, puis raycast vertical au point de spawn.
	/// Tant que le raycast n'a pas de hit valide, on ne finalise PAS (évite spawn sous la map).
	/// </summary>
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

	public override void _Ready()
	{
		DirAccess.MakeDirRecursiveAbsolute("user://chunks");
		_joueur = GetParent().GetNode<CharacterBody3D>("Joueur");
		Chunk_Client.EchelleGazon = EchelleGazon;

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
		AddChild(canvas);
		canvas.AddChild(panel);
		CreerRepereCentreEcran();

		// Position : chargée si monde existant, sinon spawn par défaut (terrain généré → joueur déposé)
		Vector3 posSpawn = _joueur.GlobalPosition;
		var posSauvegardee = GameState.Instance?.ObtenirPositionJoueurSauvegardee();
		_spawnDoitEtreAligneAuSol = !posSauvegardee.HasValue || ForcerAlignementSolAuChargement;
		_spawnAligneAuSol = !_spawnDoitEtreAligneAuSol;
		if (posSauvegardee.HasValue)
		{
			posSpawn = posSauvegardee.Value;
			GD.Print($"ZERO-K : Joueur reconnecté à {posSpawn}");
		}
		else
		{
			// Nouveau monde: spawn déterministe basé sur la seed (et pas uniquement la position fixe de la scène).
			posSpawn = CalculerSpawnInitialDepuisSeed();
			GD.Print($"ZERO-K : Spawn initial seed={SeedTerrain} -> {posSpawn}");
		}
		posSpawn = AssurerSpawnAuDessusDuSol(posSpawn);
		_joueur.GlobalPosition = posSpawn;
		_spawnInitialEnAttente = posSpawn;
		if (_spawnDoitEtreAligneAuSol)
			_joueur.Visible = false; // Apparaît seulement après alignement raycast sur le sol.

		if (UseArchitectureReseau)
		{
			DemarrerArchitectureReseau();
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
		panelChargement.AddChild(lblChargement);
		_overlayChargement.AddChild(panelChargement);
		AddChild(_overlayChargement);
		_secondesOverlayChargement = 0;

		// Forge automatique du matériau eau (bypass de l'éditeur) — sanctuarisation : le GC ne le détruira pas car lié au nœud.
		var shaderEau = GD.Load<Shader>("res://EauTriplanar.gdshader");
		if (shaderEau != null)
		{
			var matEau = new ShaderMaterial();
			matEau.Shader = shaderEau;
			matEau.SetShaderParameter("albedo_color", new Color(0.1f, 0.3f, 0.6f, 0.6f));
			MaterielEau = matEau;
		}

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

	private void RestaurerEtatPersistantMonde()
	{
		if (_etatPersistantRestaure) return;
		_etatPersistantRestaure = true;
		if (_joueur is Joueur j)
			j.ChargerEtatPersistantMonde();
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel"))
			return;
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
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Notification(int what)
	{
		if (what == Node.NotificationWMCloseRequest)
		{
			if (_joueur != null)
				GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
			if (_joueur is Joueur j)
				j.SauvegarderEtatPersistantMonde();
			if (UseArchitectureReseau)
				_mondeServeur?.SauvegarderMondeEntier();
			else
				foreach (var kv in _chunks)
					(kv.Value as Generateur_Voxel)?.Sauvegarder(kv.Key);
		}
		base._Notification(what);
	}

	public override void _ExitTree()
	{
		// Sauvegarde position joueur (reconnexion au même endroit) — uniquement si encore dans l'arbre.
		if (_joueur != null && _joueur.IsInsideTree())
			GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
		if (_joueur is Joueur j)
			j.SauvegarderEtatPersistantMonde();
		// RÈGLE ABSOLUE : sauvegarde des chunks modifiés AVANT destruction (parent _ExitTree avant enfants).
		if (UseArchitectureReseau)
			_mondeServeur?.SauvegarderMondeEntier();
		else
		{
			foreach (var kv in _chunks)
				(kv.Value as Generateur_Voxel)?.Sauvegarder(kv.Key);
		}
		base._ExitTree();
	}

	private Panel _panelPause;
	private bool _pauseVisible;

	/// <summary>Même logique que le bouton Sauvegarder du menu pause et de l’inventaire (position + monde / chunks).</summary>
	public void SauvegarderManuelDepuisMenu()
	{
		if (_joueur != null)
			GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
		if (_joueur is Joueur j)
			j.SauvegarderEtatPersistantMonde();
		if (UseArchitectureReseau)
			_mondeServeur?.SauvegarderMondeEntier();
		else
		{
			foreach (var kv in _chunks)
				(kv.Value as Generateur_Voxel)?.Sauvegarder(kv.Key);
		}
		GD.Print("ZERO-K : Sauvegarde manuelle effectuée.");
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
		btnSave.Pressed += SauvegarderManuelDepuisMenu;
		vbox.AddChild(btnSave);
		var btnMenu = new Button { Text = "Menu principal" };
		btnMenu.Pressed += () =>
		{
			ToggleMenuPause();
			GetTree().Paused = false;
			GetTree().ChangeSceneToFile("res://menu_principal.tscn");
		};
		vbox.AddChild(btnMenu);
		var btnQuit = new Button { Text = "Quitter le jeu" };
		btnQuit.Pressed += () => GetTree().Quit();
		vbox.AddChild(btnQuit);
		layer.AddChild(_panelPause);
		_panelPause.Visible = false;
	}

	private void ToggleMenuPause()
	{
		if (_panelPause == null) CreerMenuPause();
		_pauseVisible = !_pauseVisible;
		_panelPause.Visible = _pauseVisible;
		GetTree().Paused = _pauseVisible;
		Input.MouseMode = _pauseVisible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}

	private void DemarrerArchitectureReseau()
	{
		_networkManager = new NetworkManager();
		AddChild(_networkManager);
		_networkManager.DemarrerHostSolo();

		_mondeServeur = new Monde_Serveur();
		_mondeServeur.TailleChunk = TailleChunk;
		_mondeServeur.HauteurMax = HauteurMax;
		_mondeServeur.SeedTerrain = GetNode<GameState>("/root/GameState").SeedTerrainActuel;
		_mondeServeur.RenderDistance = RenderDistance;
		_mondeServeur.FuseauHoraireHeures = FuseauHoraireHeures;
		_mondeServeur.ModeEssencesPartoutTemporaire = ModeEssencesPartoutTemporaire;
		_mondeServeur.RatioJungleModeTest = RatioJungleModeTest;
		_mondeServeur.MaterielTerrain = MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");

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
		_mondeClient.MaterielTerrain = MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
		_mondeClient.Initialiser(
			_joueur,
			GetNode<GameState>("/root/GameState").SeedTerrainActuel,
			coord => _mondeServeur.EnregistrerDemandeChunk(coord),
			(pointImpact, rayon, forceDegats) => _mondeServeur.AppliquerDestructionGlobale(pointImpact, rayon, forceDegats),
			(pointImpact, normale, rayon, idMatiere) => _mondeServeur.AppliquerCreationGlobale(pointImpact, normale, rayon, idMatiere)
		);

		var nodeArbres = new Node3D { Name = "Arbres" };
		AddChild(nodeArbres);

		_mondeServeur.Initialiser(
			this,
			nodeArbres,
			(coord, sections) => _mondeClient.RecevoirChunkModifie(coord, sections),
			(coord, donnees) => _mondeClient.RecevoirDonneesChunk(coord, donnees),
			(coord, inventaireFlore) => _mondeClient.RecevoirFloreModifie(coord, inventaireFlore),
			(pos, id) =>
			{
				_mondeServeur.RepliquerPaddingVoisins(pos, id);
				_mondeClient.AppliquerVoxel(pos, id);
				if (Multiplayer.IsServer())
					_mondeClient.Rpc(nameof(Monde_Client.AppliquerVoxelRPC), pos.X, pos.Y, pos.Z, (int)id);
			},
			(coord) =>
			{
				if (Multiplayer.IsServer())
					_mondeClient.Rpc("OrdonnerDestructionChunkRPC", coord.X, coord.Y);
			},
			() => _joueur?.GlobalPosition ?? Vector3.Zero
		);
		AddChild(_mondeServeur);
		AddChild(_mondeClient);

		// Croissance des arbres + jour absolu au passage minuit
		var cycleSolaire = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (cycleSolaire != null)
		{
			cycleSolaire.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
			cycleSolaire.Connect("NouveauJour", Callable.From(() =>
			{
				GameState.Instance?.IncrementerJourAbsolu();
				_mondeServeur.FairePousserArbresDuJour();
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
	}

	/// <summary>Matrice visqueuse : océan physique couvrant Y &lt; 103. Linear/Angular Damp 4.0, gravité 4 (Archimède).</summary>
	private void CreerAreaOcean()
	{
		float demiRayon = RayonMondeChunks * TailleChunk;
		float hauteurZone = NiveauEauOcean + 500f; // Couvre jusqu'en profondeur -500
		var ocean = new Area3D { Name = "Ocean_Physique" };
		ocean.GravitySpaceOverride = Area3D.SpaceOverride.Replace;
		ocean.Gravity = 4.0f; // Poussée d'Archimède (réduit chute)
		ocean.GravityDirection = new Vector3(0, -1, 0);
		ocean.GravityPoint = false;
		ocean.LinearDamp = 4.0f;
		ocean.LinearDampSpaceOverride = Area3D.SpaceOverride.Replace;
		ocean.AngularDamp = 4.0f;
		ocean.AngularDampSpaceOverride = Area3D.SpaceOverride.Replace;
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
		if (corps == _joueur) return;
		if (corps is not RigidBody3D rb) return;

		ulong id = corps.GetInstanceId();
		if (!_corpsDansOcean.Add(id)) return;

		// Seulement un objet qui tombe (vitesse verticale descendante suffisante).
		float vitesseChute = -rb.LinearVelocity.Y;
		if (vitesseChute < 2.0f) return;

		float intensite = Mathf.Clamp(vitesseChute / 18f, 0.35f, 1.35f);
		Vector3 impactSurface = rb.GlobalPosition;
		impactSurface.Y = NiveauEauOcean + 0.04f;
		CreerEclaboussureSurface(impactSurface, intensite);
	}

	private void SurCorpsSortOcean(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return;
		_corpsDansOcean.Remove(corps.GetInstanceId());
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

			var tw = CreateTween();
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
		double offset = _mondeServeur?.FuseauHoraireHeures ?? 0.0;
		soleil.RpcId(peerId, nameof(Cycle_Solaire.DefinirDecalageHoraire), offset);
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

		bool spawnPretActuel = EstSpawnPret();
		bool spawnPretEtAligneActuel = spawnPretActuel && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
		Vector3 pointRefSpawn = ObtenirPointReferenceSpawn();
		bool cardinauxPrets = ChunkEtVoisinsCardinauxPretsAuPoint(pointRefSpawn);
		// Le cycle solaire ne doit être neutralisé que pendant le bootstrap initial (overlay visible),
		// sinon un chunk cardinal temporairement absent peut laisser le ciel bloqué en mode nuit.
		bool chargementVisuelActif = _overlayChargement != null
			&& _overlayChargement.Visible
			&& (!spawnPretEtAligneActuel || !cardinauxPrets);
		MettreAJourEtatCycleSolaire(chargementVisuelActif);

		// Masquer l'overlay quand le sol minimal sous les pieds est prêt, ou après timeout (évite chargement infini si file / grille trop large).
		if (_overlayChargement != null && _overlayChargement.Visible)
		{
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
				if (!spawnPretEtAligne && _secondesOverlayChargement >= 90.0)
					GD.PrintErr("ZERO-K : Timeout chargement monde (>90 s) — overlay masqué. Vérifiez réseau / Monde_Client si le sol manque.");
				if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
					FinaliserSpawnInitialAuSol(autoriserFallbackSansRaycast: _secondesOverlayChargement >= 90.0);
				_overlayChargement.Visible = false;
			}
		}

		// Mise à jour des coordonnées affichées en haut à droite
		if (_labelCoords != null && _joueur != null && _joueur.IsInsideTree())
		{
			Vector3 p = _joueur.GlobalPosition;
			_labelCoords.Text = $"X: {p.X:F1}  Y: {p.Y:F1}  Z: {p.Z:F1}";
		}

		if (UseArchitectureReseau)
		{
			_secondesDormanceObjets += delta;
			if (_secondesDormanceObjets >= 0.4)
			{
				_secondesDormanceObjets = 0;
				MettreAJourDormanceObjetsPoses();
			}
			// Monde_Client gère son propre _Process
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

		// Eau dynamique (legacy)
		_tempsEcoulement += (float)delta;
		if (_tempsEcoulement >= TICK_EAU)
		{
			_tempsEcoulement = 0;
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
	}

	/// <summary>
	/// Filet de sécurité anti-crash : sauvegarde régulière du joueur et d'un lot de chunks actifs.
	/// La sauvegarde complète reste assurée par le bouton manuel, _Notification et _ExitTree.
	/// </summary>
	private void ExecuterAutosauvegardeProgressive()
	{
		if (_joueur != null)
			GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
		if (_joueur is Joueur j)
			j.SauvegarderEtatPersistantMonde();

		if (UseArchitectureReseau)
		{
			int budget = Mathf.Max(1, MaxChunksAutosauvegardeParCycle);
			int n = _mondeServeur?.SauvegarderChunksActifsProgressif(budget) ?? 0;
			if (n > 0)
				GD.Print($"ZERO-K : Autosauvegarde progressive ({n} chunk(s)).");
		}
	}

	private void MettreAJourDormanceObjetsPoses()
	{
		if (_joueur == null) return;
		Vector2I chunkJoueur = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
		int rayon = RayonDormanceObjetsChunks;
		bool useGardeTerrain = UseArchitectureReseau && _mondeClient != null;
		int rayonSecuriteTerrain = Mathf.Clamp(RayonSecuriteTerrainObjetsChunks, 0, 2);
		foreach (Node n in GetTree().GetNodesInGroup("BlocsPoses"))
		{
			if (n is not RigidBody3D rb || !rb.IsInsideTree()) continue;
			if (rb is ItemPhysique ip && (ip.ID_Objet == 200 || ip.ID_Objet == Joueur.IdObjetRackBatons || ip.ID_Objet == Joueur.IdObjetRackBuches))
				continue;
			Vector2I c = WorldToChunkCoord(rb.GlobalPosition, TailleChunk);
			bool dansRayon = Mathf.Abs(c.X - chunkJoueur.X) <= rayon && Mathf.Abs(c.Y - chunkJoueur.Y) <= rayon;
			bool terrainPret = !useGardeTerrain || _mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, rayonSecuriteTerrain);
			// Priorité gameplay: un objet proche du joueur ne doit jamais rester figé en l'air.
			if (dansRayon)
			{
				if (rb.Freeze) rb.Freeze = false;
				if (rb.Sleeping) rb.Sleeping = false;
			}
			else if (!terrainPret)
			{
				if (!rb.Freeze || !rb.Sleeping)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
			}
			else
			{
				if (!rb.Freeze || !rb.Sleeping)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
			}
		}
		foreach (Node n in GetTree().GetNodesInGroup("ObjetsDormantsDynamiques"))
		{
			if (n is not RigidBody3D rb || !rb.IsInsideTree()) continue;
			Vector2I c = WorldToChunkCoord(rb.GlobalPosition, TailleChunk);
			bool dansRayon = Mathf.Abs(c.X - chunkJoueur.X) <= rayon && Mathf.Abs(c.Y - chunkJoueur.Y) <= rayon;
			bool terrainPret = !useGardeTerrain || _mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, rayonSecuriteTerrain);
			if (dansRayon)
			{
				if (rb.Freeze) rb.Freeze = false;
				if (rb.Sleeping) rb.Sleeping = false;
			}
			else if (!terrainPret)
			{
				if (!rb.Freeze || !rb.Sleeping)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
			}
			else
			{
				if (!rb.Freeze || !rb.Sleeping)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
			}
		}
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

			Callable.From(() => AppliquerNouveauTriRadarLegacy(copieChunksACharger.ToArray())).CallDeferred();
		});
	}

	private void AppliquerNouveauTriRadarLegacy(Vector2I[] nouvelleListeTriee)
	{
		_chunksACharger = new List<Vector2I>(nouvelleListeTriee);
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
