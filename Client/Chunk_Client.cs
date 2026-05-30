using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Paquet pour une section de chunk : uniquement des données C# pures (aucune ressource Godot). Produit par le Task.Run, consommé par le Main Thread.</summary>
public class SectionPayload
{
	public Vector3[] SommetsVisuels;
	public Vector3[] NormalsVisuels;
	public Color[] CouleursVisuels;
	public Vector3[] SommetsEau;
	public Vector3[] NormalsEau;

	public bool EstGeometrieVide() =>
		(SommetsVisuels == null || SommetsVisuels.Length == 0)
		&& (SommetsEau == null || SommetsEau.Length == 0);
}

/// <summary>Paquet flore précalculé dans le Task.Run : positions et couleurs pour un seul passage MultiMesh (évite AddChild désynchronisé).</summary>
public class ChunkFlorePayload
{
	public List<(Transform3D T, Color C, Vector3 PosMonde)> Gazon;
	/// <summary>Buissons avec baies : transform + index couleur baie (0..8, voir <see cref="Joueur.BaieNombreCouleurs"/>).</summary>
	public List<(Transform3D T, int CouleurIdx)> BuissonPlein;
	public List<Transform3D> BuissonVide;
	public List<Transform3D> AloeVera;
}

/// <summary>Paquet pour cailloux physiques : positions précalculées, spawn dilué (1-2 par frame) sur le Main Thread.</summary>
public class ChunkCaillouxPayload
{
	public List<Transform3D> Positions = new List<Transform3D>();
}

/// <summary>Détient MeshInstance3D, CollisionShape3D. Reçoit des données et les transforme en triangles. Aucun bruit fractal.</summary>
public partial class Chunk_Client : Node3D
{
	[ThreadStatic] private static float[] _valsRecyclables;
	[ThreadStatic] private static Vector3[] _vertsRecyclables;
	[ThreadStatic] private static Vector3[] _vertListRecyclables;
	[ThreadStatic] private static byte[] _matsRecyclables;
	[ThreadStatic] private static float[] _valsEauRecyclables;
	[ThreadStatic] private static Vector3[] _vertsEauRecyclables;
	[ThreadStatic] private static Vector3[] _vertListEauRecyclables;

	private const int TAILLE_MAX_SECTION = 17 * 17 * 17;

	public int ChunkOffsetX { get; set; }
	public int ChunkOffsetZ { get; set; }
	public int TailleChunk { get; set; }
	public int HauteurMax { get; set; }

	private const int HAUTEUR_SECTION = 16;
	private const int NB_SECTIONS = 45;  // 45×16 = 720 (HauteurMax) — avant: 16 = 256 uniquement
	private const float Isolevel = 0.0f;

	private MeshInstance3D[] _sectionsTerrain;
	private MeshInstance3D[] _sectionsEau;
	private CollisionShape3D[] _sectionsPhysiques;
	private MultiMeshInstance3D _mmGazon;
	/// <summary>Un MultiMesh par teinte de baies (draw calls regroupés par mesh procédural).</summary>
	private MultiMeshInstance3D[] _mmBuissonPleinParCouleur;
	private MultiMeshInstance3D _mmBuissonVide;
	private MultiMeshInstance3D _mmAloeVera;

	/// <summary>Échelle du gazon (grass.glb) partout sur ID 1. Ajustable pour uniformiser la taille.</summary>
	public static float EchelleGazon = 2f;
	/// <summary>Zone proche en chunks: conserve la densité maximale de la flore.</summary>
	public static int RayonQualiteMaxChunks = 7;
	/// <summary>Distance max d'affichage du gazon en chunks (au-delà: supprimé).</summary>
	public static int RayonVisibiliteGazonChunks = 12;
	/// <summary>Distance max d'affichage des buissons en chunks (LOD lointain possible).</summary>
	public static int RayonVisibiliteBuissonsChunks = 24;

	[Export] public Material MaterielTerre;
	private Material _materielTerrainRuntime;

	private float[] _densitiesFlat;
	private byte[] _materialsFlat;
	private float[] _densitiesEauFlat;
	/// <summary>Dimensions des tableaux plats : tx = TailleChunk+1, ty = HauteurMax+1, tz = TailleChunk+1.</summary>
	private int _tx, _ty, _tz;

	/// <summary>Index 1D aligné sur DonneesChunk (serveur) : x*Ty*Tz + y*Tz + z. Cohérent avec CompresserDensitesPourReseau / DecompresserDensitesFlat.</summary>
	private int Idx(int x, int y, int z) => x * _ty * _tz + y * _tz + z;
	private FastNoiseLite _noiseTemperature;
	private FastNoiseLite _noiseHumidite;
	private FastNoiseLite _noiseHumiditeDetail;
	private Dictionary<Vector3I, byte> _inventaireFloreEnAttente;
	private Dictionary<Vector3I, byte> _inventaireFloreCache;
	private ChunkFlorePayload _payloadFloreCache;
	private int _frameFlore;
	private readonly List<(Transform3D T, int CouleurIdx)> _tamponPleins = new List<(Transform3D T, int CouleurIdx)>(64);
	private readonly List<Transform3D> _tamponVides = new List<Transform3D>(64);
	private readonly List<Transform3D> _tamponAloe = new List<Transform3D>(64);
	private readonly List<(Transform3D t, Color c)> _tamponGazon = new List<(Transform3D t, Color c)>(512);
	private readonly List<Transform3D>[] _tamponBuissonsParCouleur = new List<Transform3D>[Joueur.BaieNombreCouleurs];
	private MultiMesh _cacheMultiMeshGazon;
	private MultiMesh _cacheMultiMeshBuissonVide;
	private MultiMesh _cacheMultiMeshAloeVera;
	private readonly MultiMesh[] _cacheMultiMeshBuissonPleinParCouleur = new MultiMesh[Joueur.BaieNombreCouleurs];
	/// <summary>Rayon en chunks : seul le gazon (grass.glb) est visible dans cette zone autour du joueur. Les buissons restent visibles partout.</summary>
	private const int RAYON_GAZON_CHUNKS = 1;
	/// <summary>Au-delà de ce rayon (en chunks), le chunk reste affiché mais est "dormant" : pas de physique ni collision.</summary>
	private const int RAYON_CHUNK_ACTIF_CHUNKS = 2;

	private bool _dormant;

	public override void _Ready()
	{
		SetProcess(true);
		SetPhysicsProcess(false);
		_materielTerrainRuntime = ConstruireMaterielTerrainRuntime();
		for (int i = 0; i < _tamponBuissonsParCouleur.Length; i++)
			_tamponBuissonsParCouleur[i] = new List<Transform3D>(32);

		_sectionsTerrain = new MeshInstance3D[NB_SECTIONS];
		_sectionsEau = new MeshInstance3D[NB_SECTIONS];
		_sectionsPhysiques = new CollisionShape3D[NB_SECTIONS];

		var shaderEau = GD.Load<Shader>("res://EauTriplanar.gdshader");
		var matEau = new ShaderMaterial();
		matEau.Shader = shaderEau;
		matEau.SetShaderParameter("albedo_color", new Color(0.1f, 0.3f, 0.6f, 0.6f));

		for (int i = 0; i < NB_SECTIONS; i++)
		{
			var miTerrain = new MeshInstance3D { Name = $"TerrainSection_{i}" };
			AddChild(miTerrain);
			_sectionsTerrain[i] = miTerrain;

			var corps = new StaticBody3D { Name = $"CollisionSection_{i}" };
			var collisionShape = new CollisionShape3D();
			corps.AddChild(collisionShape);
			miTerrain.AddChild(corps);
			_sectionsPhysiques[i] = collisionShape;

			var miEau = new MeshInstance3D { Name = $"EauSection_{i}", MaterialOverride = matEau };
			AddChild(miEau);
			_sectionsEau[i] = miEau;
		}
		_mmGazon = new MultiMeshInstance3D { Name = "Gazon" };
		var racineBuissonsPleins = new Node3D { Name = "BuissonPleinParCouleur" };
		_mmBuissonPleinParCouleur = new MultiMeshInstance3D[Joueur.BaieNombreCouleurs];
		for (int i = 0; i < Joueur.BaieNombreCouleurs; i++)
		{
			_mmBuissonPleinParCouleur[i] = new MultiMeshInstance3D { Name = $"BuissonPlein_c{i}" };
			racineBuissonsPleins.AddChild(_mmBuissonPleinParCouleur[i]);
		}
		_mmBuissonVide = new MultiMeshInstance3D { Name = "BuissonVide" };
		_mmAloeVera = new MultiMeshInstance3D { Name = "AloeVera" };
		AddChild(_mmGazon);
		AddChild(racineBuissonsPleins);
		AddChild(_mmBuissonVide);
		AddChild(_mmAloeVera);
	}

	public override void _Process(double delta)
	{
		// Interaction dynamique désactivée : conserve le rendu visuel des brins sans coût CPU de scan.
		if (_inventaireFloreCache == null || _inventaireFloreCache.Count == 0) return;
		_frameFlore++;
		if (_frameFlore % 12 == 0)
		{
			ActualiserFloreAvecDistance();
		}
	}

	public void ConfigurerBruitClimat(int seed)
	{
		// Aligné avec serveur : Fbm + octaves = transitions lentes pour couleurs cohérentes
		_noiseTemperature = new FastNoiseLite();
		_noiseTemperature.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseTemperature.Seed = seed + 2;
		_noiseTemperature.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseTemperature.FractalOctaves = 4;
		_noiseTemperature.Frequency = 0.0005f;

		_noiseHumidite = new FastNoiseLite();
		_noiseHumidite.Seed = seed + 3;
		_noiseHumidite.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseHumidite.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseHumidite.FractalOctaves = 4;
		_noiseHumidite.Frequency = 0.0006f;

		_noiseHumiditeDetail = new FastNoiseLite();
		_noiseHumiditeDetail.Seed = seed + 33;
		_noiseHumiditeDetail.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseHumiditeDetail.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseHumiditeDetail.FractalOctaves = 2;
		_noiseHumiditeDetail.Frequency = 0.0065f;
	}

	/// <summary>Exécute le calcul lourd (décompression + flore + 45 sections) dans le worker. Appelé par Monde_Client depuis Task.Run. Une seule tâche = un chunk entier (pas de sous-tasks).</summary>
	public void ExecuterCalculChunk(DonneesChunk donnees, Action<Action> enqueueIntegration)
	{
		if (donnees.MaterialsFlat == null) return;
		bool formatQuantifie = donnees.DensitiesQuantifiees != null;
		int tx = donnees.TailleChunk + 1, ty = donnees.HauteurMax + 1, tz = donnees.TailleChunk + 1;
		float baseX = donnees.CoordChunk.X * (float)donnees.TailleChunk;
		float baseZ = donnees.CoordChunk.Y * (float)donnees.TailleChunk;

		if (formatQuantifie)
		{
			_densitiesFlat = DonneesChunk.DecompresserDensitesFlat(donnees.DensitiesQuantifiees, tx, ty, tz);
			_densitiesEauFlat = donnees.DensitiesEauQuantifiees != null
				? DonneesChunk.DecompresserDensitesFlat(donnees.DensitiesEauQuantifiees, tx, ty, tz)
				: null;
		}
		else
		{
			_densitiesFlat = (float[])donnees.DensitiesFlat.Clone();
			_densitiesEauFlat = donnees.DensitiesEauFlat != null ? (float[])donnees.DensitiesEauFlat.Clone() : null;
		}
		_tx = tx; _ty = ty; _tz = tz;
		_materialsFlat = (byte[])donnees.MaterialsFlat.Clone();

		var invFlore = donnees.InventaireFlore;
		var chunkRef = this;
		if (invFlore != null && invFlore.Count > 0)
		{
			var payload = ConstruirePayloadFloreEnBackground(invFlore, (float)(donnees.CoordChunk.X * donnees.TailleChunk), (float)(donnees.CoordChunk.Y * donnees.TailleChunk));
			enqueueIntegration?.Invoke(() =>
			{
				// Sans cache, _Process ne rappelle pas ActualiserFloreAvecDistance : l’herbe reste figée au filtrage « joueur loin ».
				chunkRef._inventaireFloreCache = invFlore;
				chunkRef.AppliquerPayloadFlore(payload);
			});
		}
		else
			enqueueIntegration?.Invoke(() =>
			{
				chunkRef._inventaireFloreCache = null;
				chunkRef.AppliquerPayloadFlore(null);
			});

		// 45 sections en séquence dans ce worker (pas de Task.Run par section) — sections vides ignorées (muraille / ciel).
		for (int idxSec = 0; idxSec < NB_SECTIONS; idxSec++)
		{
			SectionPayload payload = ConstruireSectionPayloadEnBackground(idxSec, baseX, baseZ);
			if (payload.EstGeometrieVide()) continue;
			int sec = idxSec;
			enqueueIntegration?.Invoke(() => chunkRef.IntegrerSectionPayload(sec, payload));
		}
	}

	/// <summary>Reçoit les données du serveur. Les travaux lourds sont délégués à la Forge restreinte (file d'attente + MaxTravailleurs). Ne lance plus de Task.Run ici.</summary>
	public void RecevoirDonneesChunk(DonneesChunk donnees, Action<Action> enqueueIntegration)
	{
		// Si le monde utilise la Forge restreinte, il appelle EnqueueChunkGeneration au lieu de ceci. Conservé pour appel direct (ex. tests).
		ExecuterCalculChunk(donnees, enqueueIntegration);
	}

	/// <summary>Met à jour un voxel aux coordonnées locales (pour réplication du padding des voisins).</summary>
	public void SetVoxelLocal(int lx, int ly, int lz, byte id)
	{
		if (_densitiesFlat == null || lx < 0 || lx > TailleChunk || ly < 0 || ly > HauteurMax || lz < 0 || lz > TailleChunk) return;
		if (id == 0)
		{
			_densitiesFlat[Idx(lx, ly, lz)] = -10f;
			_materialsFlat[Idx(lx, ly, lz)] = 0;
			if (_densitiesEauFlat != null) _densitiesEauFlat[Idx(lx, ly, lz)] = -1f;
			// Purge flore locale : modèle 3D disparaît immédiatement quand le bloc sous lui est détruit
			var posGlobale = new Vector3I(ChunkOffsetX * TailleChunk + lx, ly, ChunkOffsetZ * TailleChunk + lz);
			if (_inventaireFloreCache != null && _inventaireFloreCache.Remove(posGlobale))
			{
				_payloadFloreCache = null; // Sinon ActualiserFlore réapplique l’ancien paquet figé.
				ActualiserFloreAvecDistance();
			}
		}
		else if (id == 4)
		{
			_densitiesFlat[Idx(lx, ly, lz)] = -10f;
			_materialsFlat[Idx(lx, ly, lz)] = 4;
			if (_densitiesEauFlat != null) _densitiesEauFlat[Idx(lx, ly, lz)] = 1f;
		}
		else
		{
			_densitiesFlat[Idx(lx, ly, lz)] = 10f;
			_materialsFlat[Idx(lx, ly, lz)] = id;
			if (_densitiesEauFlat != null) _densitiesEauFlat[Idx(lx, ly, lz)] = -1f;
		}
	}

	/// <summary>Applique une mise à jour voxel unique (eau/air/solide) depuis le serveur. Met à jour les données et lève le dirty flag — AUCUN Marching Cubes ici.</summary>
	public void AppliquerVoxelGlobal(Vector3I posGlobal, byte id)
	{
		if (_densitiesFlat == null) return;
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		if (c.X != ChunkOffsetX || c.Y != ChunkOffsetZ) return;
		int ly = posGlobal.Y;
		if (lx < 0 || lx > TailleChunk || ly < 0 || ly > HauteurMax || lz < 0 || lz > TailleChunk)
			return;
		if (id == 0)
		{
			_densitiesFlat[Idx(lx, ly, lz)] = -10f;
			_materialsFlat[Idx(lx, ly, lz)] = 0;
			if (_densitiesEauFlat != null) _densitiesEauFlat[Idx(lx, ly, lz)] = -1f;
			// Purge flore : gazon et buissons disparaissent quand le bloc ID 1 (herbe) est détruit
			bool floreModifiee = false;
			if (_inventaireFloreCache != null)
			{
				floreModifiee |= _inventaireFloreCache.Remove(posGlobal);
				// Sensibilité : aussi retirer la flore sur le bloc au-dessus (gazon sur surface)
				var posAuDessus = new Vector3I(posGlobal.X, posGlobal.Y + 1, posGlobal.Z);
				floreModifiee |= _inventaireFloreCache.Remove(posAuDessus);
			}
			if (floreModifiee)
			{
				_payloadFloreCache = null;
				ActualiserFloreAvecDistance();
			}
		}
		else if (id == 4)
		{
			_densitiesFlat[Idx(lx, ly, lz)] = -10f;
			_materialsFlat[Idx(lx, ly, lz)] = 4;
			if (_densitiesEauFlat != null) _densitiesEauFlat[Idx(lx, ly, lz)] = 1f;
		}
		else
		{
			_densitiesFlat[Idx(lx, ly, lz)] = 10f;
			_materialsFlat[Idx(lx, ly, lz)] = id;
			if (_densitiesEauFlat != null) _densitiesEauFlat[Idx(lx, ly, lz)] = -1f;
		}
	}

	/// <summary>Appelé par Monde_Client. Reconstruit une section en Task.Run (données pures) puis forge sur le Main Thread.</summary>
	public void DeclencherReconstructionSection(int indexSection)
	{
		if (_densitiesFlat == null) return;
		var monde = GetParent() as Monde_Client;
		if (monde == null) return;
		float baseX = ChunkOffsetX * TailleChunk;
		float baseZ = ChunkOffsetZ * TailleChunk;
		int idx = indexSection;
		var chunkRef = this;
		var enqueue = monde.EnqueueIntegration;
		Task.Run(() =>
		{
			SectionPayload payload = ConstruireSectionPayloadEnBackground(idx, baseX, baseZ);
			enqueue.Invoke(() => chunkRef.IntegrerSectionPayload(idx, payload));
		});
	}

	/// <summary>Reconstruction synchrone sur le Main Thread (Coupe-File VIP). Évite la ThreadPool Starvation.</summary>
	public void ReconstruireSectionSynchrone(int indexSection)
	{
		if (_densitiesFlat == null) return;
		float baseX = ChunkOffsetX * TailleChunk;
		float baseZ = ChunkOffsetZ * TailleChunk;
		var (meshTerrain, meshEau) = ConstruireMeshSection(indexSection, baseX, baseZ);
		AppliquerMeshSection(indexSection, meshTerrain, meshEau);
	}

	/// <summary>Retourne la densité aux coordonnées locales. -10 si hors bornes (pour suture MC aux frontières).</summary>
	public float ObtenirDensiteLocale(int lx, int ly, int lz)
	{
		if (_densitiesFlat == null || lx < 0 || lx > TailleChunk || ly < 0 || ly > HauteurMax || lz < 0 || lz > TailleChunk)
			return -10f;
		return _densitiesFlat[Idx(lx, ly, lz)];
	}

	/// <summary>Section prête si son CollisionShape3D est construit. Utilisé pour suspendre la gravité au spawn.</summary>
	public bool SectionAPret(int section)
	{
		if (_sectionsPhysiques == null || section < 0 || section >= NB_SECTIONS) return false;
		return _sectionsPhysiques[section]?.Shape != null;
	}

	/// <summary>Active ou désactive la physique du terrain selon la distance au joueur (obsChunkX/Z). Au-delà de RAYON_CHUNK_ACTIF_CHUNKS, le chunk est "dormant" : visuel seul. Protocole d'éveil : réactive Visible, ProcessMode et CollisionShape.Disabled.</summary>
	public void MettreAJourDormance(int obsChunkX, int obsChunkZ)
	{
		int dx = Mathf.Abs(ChunkOffsetX - obsChunkX);
		int dz = Mathf.Abs(ChunkOffsetZ - obsChunkZ);
		bool presDuJoueur = dx <= RAYON_CHUNK_ACTIF_CHUNKS && dz <= RAYON_CHUNK_ACTIF_CHUNKS;
		bool presPortailNexusVersApisara = false;
		if (GetParent() is Monde_Client mc && mc.EstDimensionActiveeAvecPortailNexusAuChunkOrigine())
		{
			int dx0 = Mathf.Abs(ChunkOffsetX);
			int dz0 = Mathf.Abs(ChunkOffsetZ);
			presPortailNexusVersApisara = dx0 <= RAYON_CHUNK_ACTIF_CHUNKS && dz0 <= RAYON_CHUNK_ACTIF_CHUNKS;
		}

		bool dormant = !presDuJoueur && !presPortailNexusVersApisara;
		if (dormant == _dormant) return;
		_dormant = dormant;

		if (dormant)
		{
			// Ne pas masquer le mesh : la dormance coupe seulement la physique (cf. docstring).
			// Visible = false gelait tout le monde à ~5×5 chunks (RAYON_CHUNK_ACTIF_CHUNKS=2), ignorant RenderDistance.
			Visible = true;
			ProcessMode = ProcessModeEnum.Disabled;
			if (_sectionsPhysiques != null)
			{
				for (int i = 0; i < _sectionsPhysiques.Length; i++)
				{
					var col = _sectionsPhysiques[i];
					if (col != null) col.Disabled = true;
					var corps = col?.GetParent() as StaticBody3D;
					if (corps != null)
					{
						corps.SetCollisionLayerValue(1, false);
						corps.SetCollisionMaskValue(1, false);
					}
				}
			}
		}
		else
		{
			Visible = true;
			ProcessMode = ProcessModeEnum.Inherit;
			SetProcess(true);
			if (_sectionsPhysiques != null)
			{
				for (int i = 0; i < _sectionsPhysiques.Length; i++)
				{
					var col = _sectionsPhysiques[i];
					if (col != null) col.Disabled = false;
					var corps = col?.GetParent() as StaticBody3D;
					if (corps != null)
					{
						corps.SetCollisionLayerValue(1, true);
						corps.SetCollisionMaskValue(1, true);
					}
				}
			}
		}
	}

	/// <summary>Applique le mesh visuel ET le CollisionShape3D. Hitbox créée sur le Main Thread (CreateTrimeshShape) pour éviter la dépendance à PackedVector3Array.</summary>
	private void AppliquerMeshSection(int idx, ArrayMesh meshTerrain, ArrayMesh meshEau)
	{
		try
		{
			if (!IsInsideTree()) return;
			_sectionsTerrain[idx].Mesh = meshTerrain;
			_sectionsTerrain[idx].MaterialOverride = _materielTerrainRuntime ?? MaterielTerre ?? GD.Load<Material>("res://Manteau_Planetaire.tres");

			var collisionShape = _sectionsPhysiques[idx];
			if (collisionShape != null && meshTerrain != null)
			{
				Shape3D nouveauShape = meshTerrain.GetFaces().Length > 0 ? meshTerrain.CreateTrimeshShape() : null;
				// Appelé depuis la file d'intégration (Main Thread) : pas de CallDeferred.
				if (IsInsideTree() && collisionShape != null)
				{
					if (collisionShape.Shape != null)
					{
						collisionShape.Shape.Dispose();
						collisionShape.Shape = null;
					}
					collisionShape.Shape = nouveauShape;
				}
			}

			if (_densitiesEauFlat != null && meshEau != null)
				_sectionsEau[idx].Mesh = meshEau;
		}
		catch (ObjectDisposedException) { /* Chunk déjà supprimé, ignorer */ }
		catch (System.Exception) when (IsChunkDisposeException()) { /* Godot/natif : objet libéré */ }
	}

	/// <summary>Forge sur le Main Thread : transforme un SectionPayload (données pures) en ArrayMesh + shape, puis applique à la section.</summary>
	private void IntegrerSectionPayload(int idx, SectionPayload payload)
	{
		if (payload == null) return;
		(ArrayMesh meshTerrain, ArrayMesh meshEau) = CreerMeshesDepuisPayload(payload);
		AppliquerMeshSection(idx, meshTerrain, meshEau);
	}

	/// <summary>Construit ArrayMesh terrain et eau à partir des tableaux du payload (à appeler uniquement sur le Main Thread).</summary>
	private static (ArrayMesh terrain, ArrayMesh eau) CreerMeshesDepuisPayload(SectionPayload p)
	{
		ArrayMesh meshTerrain = null;
		ArrayMesh meshEau = null;

		if (p.SommetsVisuels != null && p.SommetsVisuels.Length > 0)
		{
			var st = new SurfaceTool();
			st.Begin(Mesh.PrimitiveType.Triangles);
			for (int i = 0; i < p.SommetsVisuels.Length; i++)
			{
				st.SetNormal(p.NormalsVisuels != null && i < p.NormalsVisuels.Length ? p.NormalsVisuels[i] : Vector3.Up);
				st.SetColor(p.CouleursVisuels != null && i < p.CouleursVisuels.Length ? p.CouleursVisuels[i] : Colors.White);
				st.AddVertex(p.SommetsVisuels[i]);
			}
			st.GenerateNormals();
			meshTerrain = st.Commit();
		}

		if (p.SommetsEau != null && p.SommetsEau.Length > 0)
		{
			var stEau = new SurfaceTool();
			stEau.Begin(Mesh.PrimitiveType.Triangles);
			for (int i = 0; i < p.SommetsEau.Length; i++)
			{
				stEau.SetNormal(p.NormalsEau != null && i < p.NormalsEau.Length ? p.NormalsEau[i] : Vector3.Up);
				stEau.AddVertex(p.SommetsEau[i]);
			}
			stEau.GenerateNormals();
			meshEau = stEau.Commit();
		}

		return (meshTerrain, meshEau);
	}

	private static bool IsChunkDisposeException() => true; // Placeholder pour filtre when

	/// <summary>
	/// Durcissement runtime launcher : si la ressource triplanaire n'a pas sa texture array, on la recolle
	/// explicitement pour éviter le rendu magenta (shader sans sampler valide).
	/// </summary>
	private Material ConstruireMaterielTerrainRuntime()
	{
		return TerrainMaterialFactory.ObtenirMaterielTerrainRobuste(MaterielTerre);
	}

	/// <summary>Version différée sans paramètre — lit _inventaireFloreEnAttente pour éviter Variant/CallDeferred.</summary>
	private void AppliquerInventaireFloreEnAttente()
	{
		try
		{
			var inv = _inventaireFloreEnAttente;
			_inventaireFloreEnAttente = null;
			if (inv != null) MettreAJourRenduFlore(inv);
		}
		catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
	}

	/// <summary>Construit le paquet flore (transforms + couleurs) dans le Task.Run. N'appelle aucun nœud Godot.</summary>
	private ChunkFlorePayload ConstruirePayloadFloreEnBackground(Dictionary<Vector3I, byte> inventaire, float originX, float originZ)
	{
		var payload = new ChunkFlorePayload
		{
			Gazon = new List<(Transform3D T, Color C, Vector3 PosMonde)>(),
			BuissonPlein = new List<(Transform3D T, int CouleurIdx)>(),
			BuissonVide = new List<Transform3D>(),
			AloeVera = new List<Transform3D>()
		};
		Vector3 chunkOrigin = new Vector3(originX, 0, originZ);
		foreach (var kv in inventaire)
		{
			Vector3 positionLocale = new Vector3(kv.Key.X, kv.Key.Y + 0.5f, kv.Key.Z) - chunkOrigin + new Vector3(0.5f, 0f, 0.5f);
			float angle = (float)((kv.Key.X * 73856093 ^ kv.Key.Z * 19349663) % 10000) / 10000f * Mathf.Tau;
			Vector3 posMonde = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);

			if (kv.Value == 0)
			{
				Color couleurSol = ObtenirCouleurTerrainApproxThreadSafe(kv.Key.X, kv.Key.Y, kv.Key.Z);
				Color couleurHerbe = couleurSol.Lerp(new Color(0.22f, 0.32f, 0.20f, 1f), 0.08f);
				uint hashBase = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663);
				float humidite = CalculerHumiditeGlobale(kv.Key.X, kv.Key.Z);
				float facteurHum = Mathf.Clamp((humidite + 1f) * 0.5f, 0f, 1f);
				float facteurHauteur = Mathf.Lerp(1.0f, 1.32f, facteurHum);
				float facteurLargeur = Mathf.Lerp(0.92f, 1.12f, facteurHum);
				int densiteMax = ConstantesDimensionAbysse.EstDansTrouNoirXZ(kv.Key.X, kv.Key.Z) ? 11 : 19;
				int densiteGazon = CalculerDensiteGazonSelonHumidite(humidite, densiteMax);
				for (int i = 0; i < densiteGazon; i++)
				{
					CalculerVariationBrin(hashBase, i, densiteGazon, out float offsetX, out float offsetZ, out float echelleAlea, out float angleBrin);
					var tGazon = Transform3D.Identity;
					tGazon.Origin = positionLocale + new Vector3(offsetX, 0, offsetZ);
					float baseEchelle = EchelleGazon * echelleAlea;
					tGazon.Basis = Basis.Identity.Scaled(new Vector3(baseEchelle * facteurLargeur, baseEchelle * facteurHauteur, baseEchelle * facteurLargeur)).Rotated(Vector3.Up, angleBrin);
					payload.Gazon.Add((tGazon, couleurHerbe, posMonde + new Vector3(offsetX, 0, offsetZ)));
				}
			}

			if (Chunk_Serveur.EstTypeBuisson(kv.Value))
			{
				uint h = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663) ^ (uint)(kv.Key.Y * 83492791);
				float echelleBuis = 0.018f + (h % 500) / 500f * 0.007f;
				var tBuis = Transform3D.Identity;
				tBuis.Origin = positionLocale + new Vector3(0f, -0.04f, 0f);
				tBuis.Basis = Basis.Identity.Scaled(new Vector3(echelleBuis, echelleBuis, echelleBuis)).Rotated(Vector3.Up, angle);
				if (Chunk_Serveur.EstTypeAloeVera(kv.Value))
				{
					payload.AloeVera.Add(tBuis);
					continue;
				}
				if (Chunk_Serveur.EstBuissonPlein(kv.Value))
				{
					int idxCouleur = Joueur.IndexCouleurBaieDepuisVariante(Chunk_Serveur.ObtenirVarianteBuisson(kv.Value));
					payload.BuissonPlein.Add((tBuis, idxCouleur));
				}
				else payload.BuissonVide.Add(tBuis);
			}
		}
		return payload;
	}

	/// <summary>Version thread-safe de la couleur terrain (utilisée dans Task.Run, pas d'accès nœud).</summary>
	private Color ObtenirCouleurTerrainApproxThreadSafe(int xGlobal, int yGlobal, int zGlobal)
	{
		if (_materialsFlat == null || _noiseTemperature == null || _noiseHumidite == null) return new Color(0.5f, 0.6f, 0.5f);
		int lx = xGlobal - ChunkOffsetX * TailleChunk;
		int lz = zGlobal - ChunkOffsetZ * TailleChunk;
		if (lx < 0 || lx > TailleChunk || yGlobal < 0 || yGlobal > HauteurMax || lz < 0 || lz > TailleChunk)
			return new Color(0.5f, 0.6f, 0.5f);
		byte idMat = _materialsFlat[Idx(lx, yGlobal, lz)];
		float temp = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
		float hum = CalculerHumiditeGlobale(xGlobal, zGlobal);
		float facteurHum = Mathf.Clamp((hum + 1f) * 0.5f, 0f, 1f);
		if (idMat != 1) return new Color(0.5f, 0.6f, 0.5f);
		return CalculerCouleurHerbeBiome(temp, facteurHum);
	}

	/// <summary>Densité du gazon pilotée par l'humidité locale : max inchangé en zone humide, réduit en zone sèche.</summary>
	private int CalculerDensiteGazonSelonHumidite(float humiditeGlobale, int densiteMax)
	{
		float facteurHum = Mathf.Clamp((humiditeGlobale + 1f) * 0.5f, 0f, 1f);
		// Zone sèche plus couverte pour limiter les trous visuels.
		float multiplicateur = Mathf.Lerp(0.90f, 1.0f, facteurHum);
		return Mathf.Clamp(Mathf.RoundToInt(densiteMax * multiplicateur), 1, densiteMax);
	}

	private static void CalculerVariationBrin(uint hashBase, int index, int densiteGazon, out float offsetX, out float offsetZ, out float echelleAlea, out float angleBrin)
	{
		// Distribution phyllotaxique + jitter déterministe: casse les rangées "en cube" sans coût mémoire.
		uint h = hashBase ^ (uint)(index * 83492791);
		float angleTouffe = ((hashBase & 1023u) / 1023f) * Mathf.Tau;
		float t = (index + 0.5f) / Mathf.Max(1f, densiteGazon);
		float rayon = Mathf.Sqrt(t) * 0.56f;
		float jitterR = ((((h >> 8) & 255u) / 255f) - 0.5f) * 0.10f;
		rayon = Mathf.Clamp(rayon + jitterR, 0f, 0.64f);
		float jitterA = (((h & 255u) / 255f) - 0.5f) * 0.65f;
		float anglePos = index * 2.39996323f + angleTouffe + jitterA;
		float decalTouffeX = ((((hashBase >> 10) & 255u) / 255f) - 0.5f) * 0.18f;
		float decalTouffeZ = ((((hashBase >> 18) & 255u) / 255f) - 0.5f) * 0.18f;
		offsetX = Mathf.Cos(anglePos) * rayon + decalTouffeX;
		offsetZ = Mathf.Sin(anglePos) * rayon + decalTouffeZ;
		echelleAlea = 0.74f + (((h >> 16) & 255u) / 255f) * 0.52f;
		angleBrin = anglePos + ((((h >> 24) & 255u) / 255f) - 0.5f) * 0.70f;
	}

	/// <summary>Applique le paquet flore sur le Main Thread : un seul passage MultiMesh (1 Draw Call pour toute la végétation du chunk). Filtre le gazon par distance.</summary>
	private void AppliquerPayloadFlore(ChunkFlorePayload payload)
	{
		try
		{
			_payloadFloreCache = payload;
			if (!IsInsideTree())
			{
				if (_mmGazon != null) _mmGazon.Multimesh = null;
				ViderMultimeshBuissonsPleinsClient();
				if (_mmBuissonVide != null) _mmBuissonVide.Multimesh = null;
				return;
			}
			if (payload == null || (payload.Gazon.Count == 0 && payload.BuissonPlein.Count == 0 && payload.BuissonVide.Count == 0 && payload.AloeVera.Count == 0))
			{
				if (_mmGazon != null) _mmGazon.Multimesh = null;
				ViderMultimeshBuissonsPleinsClient();
				if (_mmBuissonVide != null) _mmBuissonVide.Multimesh = null;
				if (_mmAloeVera != null) _mmAloeVera.Multimesh = null;
				return;
			}
			Vector3 posObs = (GetParent() as Monde_Client)?.ObtenirPositionObservation() ?? GlobalPosition;
			// On utilise le rayon public configurable (RayonVisibiliteGazonChunks) au lieu d'un const serré : permet de voir l'herbe loin.
			int rayonChunksGazon = Mathf.Max(1, RayonVisibiliteGazonChunks);
			float rayonCarre = (rayonChunksGazon * TailleChunk) * (rayonChunksGazon * TailleChunk);
			var gazonFiltre = new List<(Transform3D t, Color c)>();
			foreach (var item in payload.Gazon)
			{
				if (item.PosMonde.DistanceSquaredTo(posObs) <= rayonCarre)
					gazonFiltre.Add((item.T, item.C));
			}
			RemplirMultiMeshGazon(gazonFiltre);
			RemplirMultiMeshBuissons(payload.BuissonPlein, payload.BuissonVide, payload.AloeVera);
		}
		catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
	}

	/// <summary>
	/// Le moteur attend des listes de sommets multiples de 3 ; un reste peut provoquer un plantage natif (PagedArray / tampons triangle).
	/// </summary>
	private static void TronquerSommetsSiResteNonTriplet(List<Vector3> sommets, List<Vector3> normales, List<Color> couleurs)
	{
		if (sommets == null || sommets.Count == 0) return;
		int r = sommets.Count % 3;
		if (r == 0) return;
		int n = sommets.Count - r;
		GD.PrintErr($"ZERO-K : troncature {r} sommet(s) hors triplet terrain (était {sommets.Count}).");
		sommets.RemoveRange(n, r);
		if (normales != null && normales.Count >= n + r) normales.RemoveRange(n, r);
		if (couleurs != null && couleurs.Count >= n + r) couleurs.RemoveRange(n, r);
	}

	private static void TronquerEauSiResteNonTriplet(List<Vector3> sommets, List<Vector3> normales)
	{
		if (sommets == null || sommets.Count == 0) return;
		int r = sommets.Count % 3;
		if (r == 0) return;
		int n = sommets.Count - r;
		GD.PrintErr($"ZERO-K : troncature {r} sommet(s) hors triplet eau (était {sommets.Count}).");
		sommets.RemoveRange(n, r);
		if (normales != null && normales.Count >= n + r) normales.RemoveRange(n, r);
	}

	/// <summary>Godot exige une AABB explicite pour le culling spatial des instances ; une AABB vide peut déstabiliser le rendu instancié.</summary>
	private static Aabb CalculerAabbFusionneMultimesh(Mesh meshPrototype, IReadOnlyList<Transform3D> transforms)
	{
		if (meshPrototype == null || transforms == null || transforms.Count == 0)
			return new Aabb(Vector3.Zero, new Vector3(1f, 0.25f, 1f));
		Aabb local = meshPrototype.GetAabb();
		if (local.Size.LengthSquared() < 1e-16f)
			return new Aabb(Vector3.Zero, new Vector3(1f, 0.25f, 1f));
		Vector3 p0 = local.Position;
		Vector3 s = local.Size;
		Span<Vector3> corners = stackalloc Vector3[8];
		corners[0] = p0;
		corners[1] = p0 + new Vector3(s.X, 0f, 0f);
		corners[2] = p0 + new Vector3(0f, s.Y, 0f);
		corners[3] = p0 + new Vector3(0f, 0f, s.Z);
		corners[4] = p0 + new Vector3(s.X, s.Y, 0f);
		corners[5] = p0 + new Vector3(s.X, 0f, s.Z);
		corners[6] = p0 + new Vector3(0f, s.Y, s.Z);
		corners[7] = p0 + s;
		bool first = true;
		Aabb merged = default;
		for (int i = 0; i < transforms.Count; i++)
		{
			Transform3D t = transforms[i];
			for (int c = 0; c < 8; c++)
			{
				Vector3 wp = t * corners[c];
				if (first)
				{
					merged = new Aabb(wp, Vector3.Zero);
					first = false;
				}
				else
					merged = merged.Expand(wp);
			}
		}
		return merged.Grow(0.04f);
	}

	private static Aabb CalculerAabbFusionneMultimeshGazon(Mesh meshPrototype, List<(Transform3D t, Color c)> instances)
	{
		if (meshPrototype == null || instances == null || instances.Count == 0)
			return new Aabb(Vector3.Zero, new Vector3(1f, 0.25f, 1f));
		Aabb local = meshPrototype.GetAabb();
		if (local.Size.LengthSquared() < 1e-16f)
			return new Aabb(Vector3.Zero, new Vector3(1f, 0.25f, 1f));
		Vector3 p0 = local.Position;
		Vector3 s = local.Size;
		Span<Vector3> corners = stackalloc Vector3[8];
		corners[0] = p0;
		corners[1] = p0 + new Vector3(s.X, 0f, 0f);
		corners[2] = p0 + new Vector3(0f, s.Y, 0f);
		corners[3] = p0 + new Vector3(0f, 0f, s.Z);
		corners[4] = p0 + new Vector3(s.X, s.Y, 0f);
		corners[5] = p0 + new Vector3(s.X, 0f, s.Z);
		corners[6] = p0 + new Vector3(0f, s.Y, s.Z);
		corners[7] = p0 + s;
		bool first = true;
		Aabb merged = default;
		for (int i = 0; i < instances.Count; i++)
		{
			Transform3D t = instances[i].t;
			for (int c = 0; c < 8; c++)
			{
				Vector3 wp = t * corners[c];
				if (first)
				{
					merged = new Aabb(wp, Vector3.Zero);
					first = false;
				}
				else
					merged = merged.Expand(wp);
			}
		}
		return merged.Grow(0.04f);
	}

	/// <summary>
	/// Ordre Godot : <see cref="MultiMesh.TransformFormat"/> → <see cref="MultiMesh.UseColors"/> → <see cref="MultiMesh.Mesh"/> → <see cref="MultiMesh.InstanceCount"/>
	/// (voir doc <c>instance_count</c> : les drapeaux/format ne s’appliquent plus après allocation ; <c>mesh</c> avant le count évite tampons désalignés / crash <c>paged_array</c> index==count).
	/// </summary>
	private static void ConfigurerMultiMeshGazonAvecInstances(MultiMesh mm, Mesh meshGazon, List<(Transform3D t, Color c)> instances)
	{
		if (mm == null || meshGazon == null || instances == null) return;
		int n = instances.Count;
		if (n <= 0) return;
		mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		mm.UseColors = true;
		mm.Mesh = meshGazon;
		mm.InstanceCount = n;
		int instancesAllouees = Mathf.Clamp(mm.InstanceCount, 0, n);
		for (int i = 0; i < instancesAllouees; i++)
		{
			mm.SetInstanceTransform(i, instances[i].t);
			mm.SetInstanceColor(i, instances[i].c);
		}
		mm.CustomAabb = CalculerAabbFusionneMultimeshGazon(meshGazon, instances);
	}

	/// <summary>Buissons : pas de couleur par instance — même séquence sûre que le gazon (sans canal couleur).</summary>
	private static void ConfigurerMultiMeshBuissonAvecTransforms(MultiMesh mm, Mesh meshBuisson, IReadOnlyList<Transform3D> transforms)
	{
		if (mm == null || meshBuisson == null || transforms == null) return;
		int n = transforms.Count;
		if (n <= 0) return;
		mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		mm.UseColors = false;
		mm.Mesh = meshBuisson;
		mm.InstanceCount = n;
		int instancesAllouees = Mathf.Clamp(mm.InstanceCount, 0, n);
		for (int i = 0; i < instancesAllouees; i++)
			mm.SetInstanceTransform(i, transforms[i]);
	}

	private void RemplirMultiMeshGazon(List<(Transform3D t, Color c)> gazonInstances)
	{
		if (gazonInstances == null || gazonInstances.Count == 0)
		{
			if (_mmGazon != null) _mmGazon.Multimesh = null;
			return;
		}

		// FORÇAGE ABSOLU : On utilise le générateur C#, on ignore tout fichier externe.
		if (_cacheMeshGazon == null)
		{
			_cacheMeshGazon = GenererMeshGazonProcedural();
		}

		Mesh meshGazon = _cacheMeshGazon;

		_cacheMultiMeshGazon ??= new MultiMesh();
		var mm = _cacheMultiMeshGazon;
		ConfigurerMultiMeshGazonAvecInstances(mm, meshGazon, gazonInstances);
		if (mm.InstanceCount <= 0)
		{
			_mmGazon.Multimesh = null;
			return;
		}

		_mmGazon.Multimesh = mm;
		_mmGazon.MaterialOverride = ObtenirMaterielGazonSymbiotique();
		_mmGazon.Visible = true;
	}

	private void ViderMultimeshBuissonsPleinsClient()
	{
		if (_mmBuissonPleinParCouleur == null) return;
		for (int i = 0; i < _mmBuissonPleinParCouleur.Length; i++)
		{
			if (_mmBuissonPleinParCouleur[i] != null)
				_mmBuissonPleinParCouleur[i].Multimesh = null;
		}
	}

	private void RemplirMultiMeshBuissons(List<(Transform3D T, int CouleurIdx)> pleinsColores, List<Transform3D> vides, List<Transform3D> aloes)
	{
		if (_cacheMeshVide == null) _cacheMeshVide = GenererMeshBuissonProcedural(false, 0);
		if (_cacheMeshAloeVera == null) _cacheMeshAloeVera = GenererMeshAloeVeraProcedural();
		Material matBuisson = ObtenirMaterielBuissonProcedural();
		ViderMultimeshBuissonsPleinsClient();
		if (_mmBuissonPleinParCouleur != null && pleinsColores != null && pleinsColores.Count > 0)
		{
			for (int i = 0; i < _tamponBuissonsParCouleur.Length; i++)
				_tamponBuissonsParCouleur[i].Clear();
			foreach (var p in pleinsColores)
				_tamponBuissonsParCouleur[Joueur.ClampIndexCouleurBaie(p.CouleurIdx)].Add(p.T);
			for (int c = 0; c < _mmBuissonPleinParCouleur.Length; c++)
			{
				Mesh meshPlein = ObtenirMeshBuissonProcedural(true, c);
				List<Transform3D> instancesCouleur = _tamponBuissonsParCouleur[c];
				if (meshPlein == null || instancesCouleur.Count <= 0)
					continue;
				_cacheMultiMeshBuissonPleinParCouleur[c] ??= new MultiMesh();
				var mm = _cacheMultiMeshBuissonPleinParCouleur[c];
				ConfigurerMultiMeshBuissonAvecTransforms(mm, meshPlein, instancesCouleur);
				mm.CustomAabb = CalculerAabbFusionneMultimesh(meshPlein, instancesCouleur);
				_mmBuissonPleinParCouleur[c].Multimesh = mm;
				_mmBuissonPleinParCouleur[c].MaterialOverride = matBuisson;
				_mmBuissonPleinParCouleur[c].Visible = true;
			}
		}
		Mesh meshVide = _cacheMeshVide;
		if (meshVide != null && vides != null && vides.Count > 0)
		{
			_cacheMultiMeshBuissonVide ??= new MultiMesh();
			var mm = _cacheMultiMeshBuissonVide;
			ConfigurerMultiMeshBuissonAvecTransforms(mm, meshVide, vides);
			mm.CustomAabb = CalculerAabbFusionneMultimesh(meshVide, vides);
			_mmBuissonVide.Multimesh = mm;
			_mmBuissonVide.MaterialOverride = matBuisson;
			_mmBuissonVide.Visible = true;
		}
		else _mmBuissonVide.Multimesh = null;

		Mesh meshAloe = _cacheMeshAloeVera;
		if (meshAloe != null && aloes != null && aloes.Count > 0)
		{
			_cacheMultiMeshAloeVera ??= new MultiMesh();
			var mmAloe = _cacheMultiMeshAloeVera;
			ConfigurerMultiMeshBuissonAvecTransforms(mmAloe, meshAloe, aloes);
			mmAloe.CustomAabb = CalculerAabbFusionneMultimesh(meshAloe, aloes);
			_mmAloeVera.Multimesh = mmAloe;
			_mmAloeVera.MaterialOverride = matBuisson;
			_mmAloeVera.Visible = true;
		}
		else if (_mmAloeVera != null)
			_mmAloeVera.Multimesh = null;
	}

	/// <summary>Met à jour UNIQUEMENT les MultiMesh (buissons). N'appelle JAMAIS ConstruireMeshSection — isolement absolu du terrain.</summary>
	public void MettreAJourRenduFlore(Dictionary<Vector3I, byte> inventaire)
	{
		_inventaireFloreCache = inventaire;
		_payloadFloreCache = null; // Force recalcul depuis inventaire si pas encore de payload
		ActualiserFloreAvecDistance();
	}

	/// <summary>Lissage temporel : ajoute les cailloux physiques 1-2 par frame pour éviter le Main Thread Blocking (pas de boucle AddChild massive).</summary>
	private async void AppliquerCaillouxPhysiques(List<Transform3D> positions)
	{
		if (positions == null || positions.Count == 0 || !IsInsideTree()) return;
		const int caillouxParFrame = 2;
		int compteur = 0;
		foreach (var pos in positions)
		{
			GenererCaillouPhysique(pos);
			compteur++;
			if (compteur >= caillouxParFrame)
			{
				compteur = 0;
				await ToSignal(GetTree(), "process_frame");
				if (!IsInsideTree()) return;
			}
		}
	}

	/// <summary>À surcharger ou appeler depuis le monde : crée un RigidBody3D (caillou/silex) à la position. Par défaut no-op (côté client les pierres sont gérées par le serveur).</summary>
	protected virtual void GenererCaillouPhysique(Transform3D pos)
	{
		// Côté client : les cailloux sont répliqués par le serveur. Pour du spawn local, override ou utiliser un callback depuis Monde_Client.
	}

	private void ActualiserFloreAvecDistance()
	{
		try
		{
			if (!IsInsideTree()) return;
			if (_payloadFloreCache != null)
			{
				AppliquerPayloadFlore(_payloadFloreCache);
				return;
			}
			var inventaire = _inventaireFloreCache;
			if (inventaire == null || inventaire.Count == 0)
			{
				if (_mmGazon != null) _mmGazon.Multimesh = null;
				ViderMultimeshBuissonsPleinsClient();
				if (_mmBuissonVide != null) _mmBuissonVide.Multimesh = null;
				if (_mmAloeVera != null) _mmAloeVera.Multimesh = null;
				return;
			}
		Vector3 chunkOrigin = GlobalPosition;
		_tamponPleins.Clear();
		_tamponVides.Clear();
		_tamponAloe.Clear();
		_tamponGazon.Clear();
		Vector3 posObs = (GetParent() as Monde_Client)?.ObtenirPositionObservation() ?? chunkOrigin;
		// Utilise le rayon public configurable pour que l'herbe reste visible jusqu'à la distance paramétrée.
		int rayonChunksGazon = Mathf.Max(1, RayonVisibiliteGazonChunks);
		float rayonCarre = (rayonChunksGazon * TailleChunk) * (rayonChunksGazon * TailleChunk);

		foreach (var kv in inventaire)
		{
			Vector3 positionLocale = new Vector3(kv.Key.X, kv.Key.Y + 0.5f, kv.Key.Z) - chunkOrigin + new Vector3(0.5f, 0f, 0.5f);
			float angle = (float)((kv.Key.X * 73856093 ^ kv.Key.Z * 19349663) % 10000) / 10000f * Mathf.Tau;
			Vector3 posMonde = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);

			if (kv.Value == 0 && posMonde.DistanceSquaredTo(posObs) <= rayonCarre)
			{
				Color couleurSol = ObtenirCouleurTerrainApprox(kv.Key.X, kv.Key.Y, kv.Key.Z);
				Color couleurHerbe = couleurSol.Lerp(new Color(0.22f, 0.32f, 0.20f, 1f), 0.08f);
				uint hashBase = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663);
				float humidite = CalculerHumiditeGlobale(kv.Key.X, kv.Key.Z);
				float facteurHum = Mathf.Clamp((humidite + 1f) * 0.5f, 0f, 1f);
				float facteurHauteur = Mathf.Lerp(1.0f, 1.32f, facteurHum);
				float facteurLargeur = Mathf.Lerp(0.92f, 1.12f, facteurHum);
				int densiteMax = ConstantesDimensionAbysse.EstDansTrouNoirXZ(kv.Key.X, kv.Key.Z) ? 11 : 19;
				int densiteGazon = CalculerDensiteGazonSelonHumidite(humidite, densiteMax);
				for (int i = 0; i < densiteGazon; i++)
				{
					CalculerVariationBrin(hashBase, i, densiteGazon, out float offsetX, out float offsetZ, out float echelleAlea, out float angleBrin);
					var tGazon = Transform3D.Identity;
					tGazon.Origin = positionLocale + new Vector3(offsetX, 0, offsetZ);
					float baseEchelle = EchelleGazon * echelleAlea;
					tGazon.Basis = Basis.Identity.Scaled(new Vector3(baseEchelle * facteurLargeur, baseEchelle * facteurHauteur, baseEchelle * facteurLargeur)).Rotated(Vector3.Up, angleBrin);
					_tamponGazon.Add((tGazon, couleurHerbe));
				}
			}
			if (Chunk_Serveur.EstTypeBuisson(kv.Value))
			{
				uint h = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663) ^ (uint)(kv.Key.Y * 83492791);
				float echelleBuis = 0.018f + (h % 500) / 500f * 0.007f;
				var tBuis = Transform3D.Identity;
				tBuis.Origin = positionLocale + new Vector3(0f, -0.04f, 0f);
				tBuis.Basis = Basis.Identity.Scaled(new Vector3(echelleBuis, echelleBuis, echelleBuis)).Rotated(Vector3.Up, angle);
				if (Chunk_Serveur.EstTypeAloeVera(kv.Value))
				{
					_tamponAloe.Add(tBuis);
					continue;
				}
				if (Chunk_Serveur.EstBuissonPlein(kv.Value))
				{
					int idxCouleur = Joueur.IndexCouleurBaieDepuisVariante(Chunk_Serveur.ObtenirVarianteBuisson(kv.Value));
					_tamponPleins.Add((tBuis, idxCouleur));
				}
				else _tamponVides.Add(tBuis);
			}
		}
		RemplirMultiMeshGazon(_tamponGazon);
		RemplirMultiMeshBuissons(_tamponPleins, _tamponVides, _tamponAloe);
		}
		catch (ObjectDisposedException) { /* Chunk déjà supprimé */ }
	}

	private static Mesh _cacheMeshGazon;
	private static readonly Mesh[] _cacheMeshPleinParCouleurCache = new Mesh[Joueur.BaieNombreCouleurs];
	private static Mesh _cacheMeshVide;
	private static Material _cacheMaterielGazonSymbiotique;
	private static Material _cacheMaterielBuissonProcedural;
private static Texture2D _cacheTextureFeuilleBuisson;
	private static Mesh _cacheMeshAloeVera;
	private static Mesh _cacheMeshLamelleAloeObjet;
	private const int MAX_CONTACTS_GAZON = 6;
	private const int MAX_CONTACTS_RIGIDES_GAZON_SCAN = 24;
	private const int MAX_TRACES_CONTACT_GAZON = 18;
	private const float RAYON_REVEIL_INTERACTION_GAZON = 56f;
	private const float BONUS_RAYON_REVEIL_SI_JOUEUR_RAPIDE = 34f;
	private const float VITESSE_OBSERVATION_REVEIL_MAX = 18f;
	private const float FREINAGE_GAZON_MIN = 0.28f;
	private const float FREINAGE_GAZON_MAX = 1.35f;
	private const float DECROISSANCE_TRACE_PAR_SECONDE = 0.16f;
	private static readonly Vector3[] _contactsGazonMonde = new Vector3[MAX_CONTACTS_GAZON];
	private static readonly float[] _contactsGazonIntensite = new float[MAX_CONTACTS_GAZON];
	private static readonly Dictionary<RigidBody3D, float> _dampBaseRigides = new Dictionary<RigidBody3D, float>();
	private static readonly HashSet<RigidBody3D> _rigidesActifsCeScan = new HashSet<RigidBody3D>();
	private struct TraceContactGazon
	{
		public Vector3 PosMonde;
		public float Intensite;
	}
	private static readonly List<TraceContactGazon> _tracesContactsGazon = new List<TraceContactGazon>();
	private static Vector3 _dernierePositionObservation = new Vector3(float.NaN, float.NaN, float.NaN);
	private static ulong _frameDerniereObservation = ulong.MaxValue;
	private static ulong _frameDerniereMajTraces = ulong.MaxValue;
	private static ulong _frameDerniereApplicationShader = ulong.MaxValue;
	private static ulong _frameDernierScanContactsGazon = ulong.MaxValue;

	/// <summary>Couleur approximative du terrain à (x,y,z) — même formule que TerrainVoxel (temp/hum). Pour herbe symbiotique.</summary>
	private Color ObtenirCouleurTerrainApprox(int xGlobal, int yGlobal, int zGlobal)
	{
		if (_materialsFlat == null || _noiseTemperature == null || _noiseHumidite == null) return new Color(0.5f, 0.6f, 0.5f);
		int lx = xGlobal - ChunkOffsetX * TailleChunk;
		int lz = zGlobal - ChunkOffsetZ * TailleChunk;
		if (lx < 0 || lx > TailleChunk || yGlobal < 0 || yGlobal > HauteurMax || lz < 0 || lz > TailleChunk)
			return new Color(0.5f, 0.6f, 0.5f);
		byte idMat = _materialsFlat[Idx(lx, yGlobal, lz)];
		float temp = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
		float hum = CalculerHumiditeGlobale(xGlobal, zGlobal);
		float facteurHum = Mathf.Clamp((hum + 1f) * 0.5f, 0f, 1f);
		if (idMat != 1) return new Color(0.5f, 0.6f, 0.5f);
		return CalculerCouleurHerbeBiome(temp, facteurHum);
	}

	private static Color CalculerCouleurHerbeBiome(float temperature, float facteurHum)
	{
		float h = Mathf.Clamp(facteurHum, 0f, 1f);
		float t = Mathf.Clamp((temperature + 1f) * 0.5f, 0f, 1f);
		float jungle = Mathf.SmoothStep(0.58f, 0.92f, t) * Mathf.SmoothStep(0.55f, 0.95f, h);
		Color sec = new Color(0.74f, 0.69f, 0.36f);
		Color prairie = new Color(0.34f, 0.59f, 0.28f);
		Color jungleVif = new Color(0.22f, 0.67f, 0.30f);
		Color humide = prairie.Lerp(jungleVif, jungle);
		return sec.Lerp(humide, h);
	}

	private float CalculerHumiditeGlobale(float xGlobal, float zGlobal)
	{
		float macro = _noiseHumidite.GetNoise2D(xGlobal, zGlobal);
		float micro = _noiseHumiditeDetail != null ? _noiseHumiditeDetail.GetNoise2D(xGlobal, zGlobal) : 0f;
		return Mathf.Clamp(macro * 0.85f + micro * 0.15f, -1f, 1f);
	}

	/// <summary>ShaderMaterial procédural : gazon mat et organique, sans vent aléatoire ; réaction de contact avec le joueur.</summary>
	private static Material ObtenirMaterielGazonSymbiotique()
	{
		if (_cacheMaterielGazonSymbiotique != null) return _cacheMaterielGazonSymbiotique;
		var shader = new Shader();
		shader.Code = @"
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform vec3 couleur_pointe = vec3(0.38, 0.52, 0.18);
uniform vec3 contact_pos_0 = vec3(0.0, -99999.0, 0.0);
uniform vec3 contact_pos_1 = vec3(0.0, -99999.0, 0.0);
uniform vec3 contact_pos_2 = vec3(0.0, -99999.0, 0.0);
uniform vec3 contact_pos_3 = vec3(0.0, -99999.0, 0.0);
uniform vec3 contact_pos_4 = vec3(0.0, -99999.0, 0.0);
uniform vec3 contact_pos_5 = vec3(0.0, -99999.0, 0.0);
uniform float contact_pow_0 = 0.0;
uniform float contact_pow_1 = 0.0;
uniform float contact_pow_2 = 0.0;
uniform float contact_pow_3 = 0.0;
uniform float contact_pow_4 = 0.0;
uniform float contact_pow_5 = 0.0;
uniform float rayon_contact = 2.8;
uniform float force_contact = 1.25;
varying vec3 v_pos_monde;

float contact_influence(vec3 pos_monde, vec3 contact_pos) {
	vec2 delta = pos_monde.xz - contact_pos.xz;
	float dist = length(delta);
	return 1.0 - smoothstep(0.0, rayon_contact, dist);
}

vec2 contact_dir(vec3 pos_monde, vec3 contact_pos) {
	vec2 delta = pos_monde.xz - contact_pos.xz;
	float dist = length(delta);
	return dist > 0.0001 ? normalize(delta) : vec2(0.0, 0.0);
}

void vertex() {
	vec3 pos_monde = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_pos_monde = pos_monde;
	float influence = pow(1.0 - UV.y, 1.45);
	vec2 dir_total = vec2(0.0, 0.0);
	float contact_total = 0.0;

	float c0 = contact_influence(pos_monde, contact_pos_0) * contact_pow_0;
	dir_total += contact_dir(pos_monde, contact_pos_0) * c0;
	contact_total += c0;
	float c1 = contact_influence(pos_monde, contact_pos_1) * contact_pow_1;
	dir_total += contact_dir(pos_monde, contact_pos_1) * c1;
	contact_total += c1;
	float c2 = contact_influence(pos_monde, contact_pos_2) * contact_pow_2;
	dir_total += contact_dir(pos_monde, contact_pos_2) * c2;
	contact_total += c2;
	float c3 = contact_influence(pos_monde, contact_pos_3) * contact_pow_3;
	dir_total += contact_dir(pos_monde, contact_pos_3) * c3;
	contact_total += c3;
	float c4 = contact_influence(pos_monde, contact_pos_4) * contact_pow_4;
	dir_total += contact_dir(pos_monde, contact_pos_4) * c4;
	contact_total += c4;
	float c5 = contact_influence(pos_monde, contact_pos_5) * contact_pow_5;
	dir_total += contact_dir(pos_monde, contact_pos_5) * c5;
	contact_total += c5;

	if (contact_total > 0.0001) {
		vec2 dir = normalize(dir_total);
		float contact = pow(clamp(contact_total, 0.0, 1.45), 1.15);
		float bend = force_contact * contact * influence;
		vec3 bend_world = vec3(dir.x, 0.0, dir.y) * bend;
		vec3 bend_local = (inverse(MODEL_MATRIX) * vec4(bend_world, 0.0)).xyz;
		VERTEX += bend_local;
		VERTEX.y -= bend * 0.22;
	}
}

void fragment() {
	vec3 couleur_base = COLOR.rgb;
	float h = 1.0 - UV.y; // 0 = pointe, 1 = base
	float centre = 1.0 - abs(UV.x * 2.0 - 1.0); // 0 = bord, 1 = nervure centrale
	float nervure = pow(centre, 1.8);

	// Base plus sombre + pointe légèrement plus lumineuse.
	vec3 couleur_racine = couleur_base * 0.48;
	vec3 couleur_sommet = couleur_pointe * couleur_base * 1.30;
	float mix_hauteur = pow(h, 1.3);
	vec3 couleur_finale = mix(couleur_racine, couleur_sommet, mix_hauteur);

	// Nervure centrale plus claire, bords un peu assombris.
	couleur_finale *= mix(0.84, 1.08, nervure);

	// Texture procédurale : stries verticales + micro-variation monde stable.
	float strie = sin(UV.y * 90.0 + nervure * 6.0);
	float stries = 0.95 + 0.05 * strie;
	float bruitMonde = fract(sin(dot(v_pos_monde.xz, vec2(12.9898, 78.233))) * 43758.5453);
	float bruitFin = fract(sin(dot(v_pos_monde.xz + UV.xy, vec2(41.23, 17.77))) * 12471.137);
	couleur_finale *= stries * mix(0.92, 1.08, bruitMonde) * mix(0.97, 1.03, bruitFin);

	// Racine ombrée pour un effet tapis plus dense.
	couleur_finale *= mix(0.78, 1.0, pow(mix_hauteur, 0.75));
	ALBEDO = couleur_finale;
	ROUGHNESS = mix(0.96, 0.90, nervure);
	SPECULAR = 0.0;
	BACKLIGHT = couleur_finale * 0.20;
}
";
		var mat = new ShaderMaterial { Shader = shader };
		_cacheMaterielGazonSymbiotique = mat;
		return mat;
	}





}
