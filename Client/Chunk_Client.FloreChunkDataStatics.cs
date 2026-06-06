using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Chunk_Client : Node3D
{
	/// <summary>Remplit ChunkData depuis DonneesChunk et construit les 45 SectionPayload (pour architecture AAA / RID). Appelé depuis le worker.</summary>
	public static List<SectionPayload> RemplirEtConstruirePayloads(ChunkData data, DonneesChunk donnees)
	{
		if (donnees?.MaterialsFlat == null) return null;
		bool formatQuantifie = donnees.DensitiesQuantifiees != null;
		int tx = donnees.TailleChunk + 1, ty = donnees.HauteurMax + 1, tz = donnees.TailleChunk + 1;
		float baseX = donnees.CoordChunk.X * (float)donnees.TailleChunk;
		float baseZ = donnees.CoordChunk.Y * (float)donnees.TailleChunk;

		if (formatQuantifie)
		{
			data.DensitiesFlat = DonneesChunk.DecompresserDensitesFlat(donnees.DensitiesQuantifiees, tx, ty, tz);
			data.DensitiesEauFlat = donnees.DensitiesEauQuantifiees != null
				? DonneesChunk.DecompresserDensitesFlat(donnees.DensitiesEauQuantifiees, tx, ty, tz)
				: null;
		}
		else
		{
			data.DensitiesFlat = (float[])donnees.DensitiesFlat.Clone();
			data.DensitiesEauFlat = donnees.DensitiesEauFlat != null ? (float[])donnees.DensitiesEauFlat.Clone() : null;
		}
		data.Tx = tx; data.Ty = ty; data.Tz = tz;
		data.MaterialsFlat = (byte[])donnees.MaterialsFlat.Clone();
		data.TailleChunk = donnees.TailleChunk;
		data.HauteurMax = donnees.HauteurMax;

		// Flore : l’état serveur (persisté disque) est la source de vérité absolue.
		// IMPORTANT : un inventaire vide est un état valide (zone totalement fauchée),
		// il ne faut jamais régénérer automatiquement côté client.
		if (donnees.InventaireFlore != null)
			data.InventaireFlore = new Dictionary<Vector3I, byte>(donnees.InventaireFlore);
		else if (data.InventaireFlore == null)
			data.InventaireFlore = GenererInventaireFloreDepuisSurface(data); // Fallback legacy uniquement.

		int nbSections = ObtenirNbSectionsEffectif(data.HauteurMax);
		var payloads = new List<SectionPayload>(nbSections);
		for (int i = 0; i < nbSections; i++)
			payloads.Add(ConstruireSectionPayloadEnBackgroundFromData(data, i, baseX, baseZ));
		return payloads;
	}

	/// <summary>Copie densités/matériaux serveur en RAM sans lancer Marching Cubes (couche profonde en attente de maillage).</summary>
	public static void RemplirDonneesVoxelDepuisServeur(ChunkData data, DonneesChunk donnees)
	{
		if (data == null || donnees?.MaterialsFlat == null) return;
		bool formatQuantifie = donnees.DensitiesQuantifiees != null;
		int tx = donnees.TailleChunk + 1, ty = donnees.HauteurMax + 1, tz = donnees.TailleChunk + 1;
		if (formatQuantifie)
		{
			data.DensitiesFlat = DonneesChunk.DecompresserDensitesFlat(donnees.DensitiesQuantifiees, tx, ty, tz);
			data.DensitiesEauFlat = donnees.DensitiesEauQuantifiees != null
				? DonneesChunk.DecompresserDensitesFlat(donnees.DensitiesEauQuantifiees, tx, ty, tz)
				: null;
		}
		else
		{
			data.DensitiesFlat = (float[])donnees.DensitiesFlat.Clone();
			data.DensitiesEauFlat = donnees.DensitiesEauFlat != null ? (float[])donnees.DensitiesEauFlat.Clone() : null;
		}
		data.Tx = tx;
		data.Ty = ty;
		data.Tz = tz;
		data.MaterialsFlat = (byte[])donnees.MaterialsFlat.Clone();
		data.TailleChunk = donnees.TailleChunk;
		data.HauteurMax = donnees.HauteurMax;
		if (donnees.InventaireFlore != null)
			data.InventaireFlore = new Dictionary<Vector3I, byte>(donnees.InventaireFlore);
	}

	/// <summary>Crée le nœud MultiMeshInstance3D de gazon pour un ChunkData (architecture AAA). À ajouter au monde et à libérer dans data.LibérerRids.</summary>
	public static MultiMeshInstance3D CreerNoeudGazonPourChunkData(ChunkData data, Vector3 positionObservation, int tailleChunk)
	{
		var instances = ConstruireGazonInstancesPourChunkData(data, positionObservation);
		if (instances == null || instances.Count == 0) return null;
		if (_cacheMeshGazon == null) _cacheMeshGazon = GenererMeshGazonProcedural();
		Mesh meshGazon = _cacheMeshGazon;
		if (meshGazon == null) return null;
		var mm = CreerMultiMeshGazon(instances, meshGazon);
		var node = new MultiMeshInstance3D { Name = "Gazon" };
		node.Multimesh = mm;
		node.MaterialOverride = ObtenirMaterielGazonSymbiotique();
		node.Position = new Vector3(data.Coordonnees.X * tailleChunk, data.ObtenirOffsetYMonde(), data.Coordonnees.Y * tailleChunk);
		node.Visible = true;
		return node;
	}

	private static void ConstruireListesTransformBuissonsDepuisChunkData(ChunkData data, Vector3 positionObservation, List<(Transform3D T, int CouleurIdx)> pleinsColores, List<Transform3D> vides, List<Transform3D> aloes)
	{
		pleinsColores.Clear();
		vides.Clear();
		aloes.Clear();
		if (data?.InventaireFlore == null) return;
		float originX = data.Coordonnees.X * (float)data.TailleChunk;
		float originZ = data.Coordonnees.Y * (float)data.TailleChunk;
		float originY = data.ObtenirOffsetYMonde();
		Vector3 chunkOrigin = new Vector3(originX, originY, originZ);
		float rayonBuissons = Mathf.Max(2, RayonVisibiliteBuissonsChunks) * data.TailleChunk;
		float rayonBuissonsCarre = rayonBuissons * rayonBuissons;
		foreach (var kv in data.InventaireFlore)
		{
			if (!Chunk_Serveur.EstTypeBuisson(kv.Value)) continue;
			Vector3 posMonde = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);
			float distCarree = posMonde.DistanceSquaredTo(positionObservation);
			if (distCarree > rayonBuissonsCarre) continue;
			Vector3 positionLocale = new Vector3(kv.Key.X, kv.Key.Y + 0.5f, kv.Key.Z) - chunkOrigin + new Vector3(0.5f, 0f, 0.5f);
			float angle = (float)((kv.Key.X * 73856093 ^ kv.Key.Z * 19349663) % 10000) / 10000f * Mathf.Tau;
			uint h = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663) ^ (uint)(kv.Key.Y * 83492791);
			float echelleBuis = 0.018f + (h % 500) / 500f * 0.007f;
			var tBuis = Transform3D.Identity;
			tBuis.Origin = positionLocale + new Vector3(0f, -0.04f, 0f);
			tBuis.Basis = Basis.Identity.Scaled(new Vector3(echelleBuis, echelleBuis, echelleBuis)).Rotated(Vector3.Up, angle);
			if (Chunk_Serveur.EstTypeAloeVera(kv.Value))
			{
				aloes.Add(tBuis);
				continue;
			}
			if (Chunk_Serveur.EstBuissonPlein(kv.Value))
			{
				int idxCouleur = Joueur.IndexCouleurBaieDepuisVariante(Chunk_Serveur.ObtenirVarianteBuisson(kv.Value));
				pleinsColores.Add((tBuis, idxCouleur));
			}
			else vides.Add(tBuis);
		}
	}

	/// <summary>Remplit jusqu’à 8 MultiMesh (une teinte de baie par mesh procédural) sous la racine flore.</summary>
	private static void AppliquerBuissonsPleinsGroupesSurRacineFlore(Node3D root, List<(Transform3D T, int CouleurIdx)> pleinsColores, Material matBuisson)
	{
		if (root == null) return;
		if (pleinsColores == null || pleinsColores.Count == 0)
		{
			var groupe0 = root.GetNodeOrNull<Node3D>("BuissonPleinGroup");
			if (groupe0 != null)
			{
				foreach (Node n in groupe0.GetChildren())
				{
					if (n is MultiMeshInstance3D m)
					{
						m.Multimesh = null;
						m.Visible = false;
					}
				}
			}
			var leg0 = root.GetNodeOrNull<MultiMeshInstance3D>("BuissonPlein");
			if (leg0 != null)
			{
				leg0.Multimesh = null;
				leg0.Visible = false;
			}
			return;
		}
		var leg = root.GetNodeOrNull<MultiMeshInstance3D>("BuissonPlein");
		leg?.QueueFree();
		var groupe = root.GetNodeOrNull<Node3D>("BuissonPleinGroup");
		if (groupe == null)
		{
			groupe = new Node3D { Name = "BuissonPleinGroup" };
			for (int i = 0; i < Joueur.BaieNombreCouleurs; i++)
				groupe.AddChild(new MultiMeshInstance3D { Name = $"c{i}" });
			root.AddChild(groupe);
		}
		var parCouleur = new List<Transform3D>[Joueur.BaieNombreCouleurs];
		for (int i = 0; i < parCouleur.Length; i++)
			parCouleur[i] = new List<Transform3D>();
		foreach (var p in pleinsColores)
			parCouleur[Joueur.ClampIndexCouleurBaie(p.CouleurIdx)].Add(p.T);
		for (int c = 0; c < Joueur.BaieNombreCouleurs; c++)
		{
			var mmi = groupe.GetNodeOrNull<MultiMeshInstance3D>($"c{c}");
			if (mmi == null) continue;
			Mesh meshPlein = ObtenirMeshBuissonProcedural(true, c);
			AppliquerMultiMeshBuissonsSurNoeud(mmi, parCouleur[c], meshPlein, matBuisson);
		}
	}

	private static void AppliquerMultiMeshBuissonsSurNoeud(MultiMeshInstance3D mmi, List<Transform3D> transforms, Mesh mesh, Material mat)
	{
		if (mmi == null) return;
		if (transforms == null || transforms.Count == 0)
		{
			mmi.Multimesh = null;
			mmi.Visible = false;
			return;
		}
		var mm = new MultiMesh();
		ConfigurerMultiMeshBuissonAvecTransforms(mm, mesh, transforms);
		mm.CustomAabb = CalculerAabbFusionneMultimesh(mesh, transforms);
		mmi.Multimesh = mm;
		mmi.MaterialOverride = mat;
		mmi.Visible = true;
	}

	/// <summary>Architecture AAA (RID) : gazon + buissons procéduraux sous un Node3D à la position du chunk.</summary>
	public static Node3D CreerNoeudFlorePourChunkData(ChunkData data, Vector3 positionObservation, int tailleChunk)
	{
		if (data?.InventaireFlore == null || data.InventaireFlore.Count == 0) return null;
		Vector3 chunkPos = data.ObtenirOrigineMonde(tailleChunk);
		var root = new Node3D { Name = "Flore", Position = chunkPos };

		var pleinsColores = new List<(Transform3D T, int CouleurIdx)>();
		var vides = new List<Transform3D>();
		var aloes = new List<Transform3D>();
		ConstruireListesTransformBuissonsDepuisChunkData(data, positionObservation, pleinsColores, vides, aloes);

		if (_cacheMeshVide == null) _cacheMeshVide = GenererMeshBuissonProcedural(false, 0);
		Material matBuisson = ObtenirMaterielBuissonProcedural();

		MultiMeshInstance3D nodeGazon = CreerNoeudGazonPourChunkData(data, positionObservation, tailleChunk);
		bool aGazon = nodeGazon != null;
		if (aGazon)
		{
			nodeGazon.Position = Vector3.Zero;
			root.AddChild(nodeGazon);
		}

		AppliquerBuissonsPleinsGroupesSurRacineFlore(root, pleinsColores, matBuisson);
		if (vides.Count > 0)
		{
			var mmi = new MultiMeshInstance3D { Name = "BuissonVide" };
			AppliquerMultiMeshBuissonsSurNoeud(mmi, vides, _cacheMeshVide, matBuisson);
			root.AddChild(mmi);
		}
		if (aloes.Count > 0)
		{
			if (_cacheMeshAloeVera == null) _cacheMeshAloeVera = GenererMeshAloeVeraProcedural();
			var mmiAloe = new MultiMeshInstance3D { Name = "AloeVera" };
			AppliquerMultiMeshBuissonsSurNoeud(mmiAloe, aloes, _cacheMeshAloeVera, matBuisson);
			root.AddChild(mmiAloe);
		}

		if (!aGazon && pleinsColores.Count == 0 && vides.Count == 0 && aloes.Count == 0) return null;
		return root;
	}

	/// <summary>Mise à jour flore complète (gazon + buissons) pour le nœud créé par <see cref="CreerNoeudFlorePourChunkData"/>.</summary>
	public static void MettreAJourFlorePourChunkData(ChunkData data, Vector3 positionObservation, Node3D nodeFlore)
	{
		if (data == null || nodeFlore == null) return;

		var gazon = nodeFlore.GetNodeOrNull<MultiMeshInstance3D>("Gazon");
		if (gazon != null)
			MettreAJourGazonPourChunkData(data, positionObservation, gazon);

		if (_cacheMeshVide == null) _cacheMeshVide = GenererMeshBuissonProcedural(false, 0);
		Material matBuisson = ObtenirMaterielBuissonProcedural();

		var pleinsColores = new List<(Transform3D T, int CouleurIdx)>();
		var vides = new List<Transform3D>();
		var aloes = new List<Transform3D>();
		ConstruireListesTransformBuissonsDepuisChunkData(data, positionObservation, pleinsColores, vides, aloes);

		AppliquerBuissonsPleinsGroupesSurRacineFlore(nodeFlore, pleinsColores, matBuisson);

		var mmiVide = nodeFlore.GetNodeOrNull<MultiMeshInstance3D>("BuissonVide");
		if (vides.Count > 0)
		{
			if (mmiVide == null)
			{
				mmiVide = new MultiMeshInstance3D { Name = "BuissonVide" };
				nodeFlore.AddChild(mmiVide);
			}
			AppliquerMultiMeshBuissonsSurNoeud(mmiVide, vides, _cacheMeshVide, matBuisson);
		}
		else if (mmiVide != null)
		{
			mmiVide.Multimesh = null;
			mmiVide.Visible = false;
		}

		var mmiAloe = nodeFlore.GetNodeOrNull<MultiMeshInstance3D>("AloeVera");
		if (aloes.Count > 0)
		{
			if (_cacheMeshAloeVera == null) _cacheMeshAloeVera = GenererMeshAloeVeraProcedural();
			if (mmiAloe == null)
			{
				mmiAloe = new MultiMeshInstance3D { Name = "AloeVera" };
				nodeFlore.AddChild(mmiAloe);
			}
			AppliquerMultiMeshBuissonsSurNoeud(mmiAloe, aloes, _cacheMeshAloeVera, matBuisson);
		}
		else if (mmiAloe != null)
		{
			mmiAloe.Multimesh = null;
			mmiAloe.Visible = false;
		}
	}

	/// <summary>Met à jour le MultiMesh du nœud gazon quand la flore a été purgée côté serveur (minage, gravité, fauchage). Les brins disparaissent visuellement.</summary>
	public static void MettreAJourGazonPourChunkData(ChunkData data, Vector3 positionObservation, MultiMeshInstance3D nodeGazon)
	{
		if (data == null || nodeGazon == null) return;
		var instances = ConstruireGazonInstancesPourChunkData(data, positionObservation);
		if (_cacheMeshGazon == null) _cacheMeshGazon = GenererMeshGazonProcedural();
		Mesh meshGazon = _cacheMeshGazon;
		if (meshGazon == null) return;
		if (instances.Count == 0)
		{
			nodeGazon.Multimesh = null;
			nodeGazon.Visible = false;
			return;
		}
		nodeGazon.Visible = true;
		nodeGazon.Multimesh = CreerMultiMeshGazon(instances, meshGazon);
	}

	private static MultiMesh CreerMultiMeshGazon(List<(Transform3D t, Color c)> instances, Mesh meshGazon)
	{
		var mm = new MultiMesh();
		ConfigurerMultiMeshGazonAvecInstances(mm, meshGazon, instances);
		return mm;
	}

	/// <summary>Construit la liste (transform, couleur) pour le gazon d'un ChunkData. Tout le gazon du chunk est ajouté dès l'intégration (pas de filtre distance).</summary>
	public static List<(Transform3D t, Color c)> ConstruireGazonInstancesPourChunkData(ChunkData data, Vector3 positionObservation)
	{
		var liste = new List<(Transform3D t, Color c)>();
		if (data?.InventaireFlore == null || data.InventaireFlore.Count == 0) return liste;
		float rayonGazon = Mathf.Max(1, RayonVisibiliteGazonChunks) * data.TailleChunk;
		float rayonGazonCarre = rayonGazon * rayonGazon;
		float rayonQualite = Mathf.Max(1, RayonQualiteMaxChunks) * data.TailleChunk;
		float rayonQualiteCarre = rayonQualite * rayonQualite;
		float originX = data.Coordonnees.X * (float)data.TailleChunk;
		float originZ = data.Coordonnees.Y * (float)data.TailleChunk;
		float originY = data.ObtenirOffsetYMonde();
		Vector3 chunkOrigin = new Vector3(originX, originY, originZ);
		foreach (var kv in data.InventaireFlore)
		{
			if (kv.Value != 0) continue; // gazon uniquement
			Vector3 posMonde = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);
			float distCarree = posMonde.DistanceSquaredTo(positionObservation);
			if (distCarree > rayonGazonCarre) continue;
			Vector3 positionLocale = new Vector3(kv.Key.X, kv.Key.Y + 0.5f, kv.Key.Z) - chunkOrigin + new Vector3(0.5f, 0f, 0.5f);
			Color couleurSol = ObtenirCouleurTerrainDepuisChunkData(data, kv.Key.X, kv.Key.Y, kv.Key.Z);
			Color couleurHerbe = couleurSol.Lerp(new Color(0.22f, 0.32f, 0.20f, 1f), 0.08f);
			uint hashBase = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663);
			int densiteBase = ConstantesDimensionAbysse.EstDansTrouNoirXZ(kv.Key.X, kv.Key.Z)
				? 7
				: (distCarree <= rayonQualiteCarre ? 9 : (distCarree <= rayonQualiteCarre * 2.6f ? 6 : 3));
			float humidite = CalculerHumiditeGlobaleDepuisChunkData(data, kv.Key.X, kv.Key.Z);
			float facteurHum = Mathf.Clamp((humidite + 1f) * 0.5f, 0f, 1f);
			float facteurHauteur = Mathf.Lerp(1.0f, 1.32f, facteurHum);
			float facteurLargeur = Mathf.Lerp(0.92f, 1.12f, facteurHum);
			int densiteGazon = CalculerDensiteGazonSelonHumiditeChunkData(humidite, densiteBase);
			for (int i = 0; i < densiteGazon; i++)
			{
				CalculerVariationBrin(hashBase, i, densiteGazon, out float offsetX, out float offsetZ, out float echelleAlea, out float angleBrin);
				var t = Transform3D.Identity;
				t.Origin = positionLocale + new Vector3(offsetX, 0, offsetZ);
				float baseEchelle = EchelleGazon * echelleAlea;
				t.Basis = Basis.Identity.Scaled(new Vector3(baseEchelle * facteurLargeur, baseEchelle * facteurHauteur, baseEchelle * facteurLargeur)).Rotated(Vector3.Up, angleBrin);
				liste.Add((t, couleurHerbe));
			}
		}
		return liste;
	}

	private static bool EssayerLireMateriauDepuisChunkData(ChunkData data, int lx, int ly, int lz, out byte idMateriau)
	{
		idMateriau = 0;
		if (data?.MaterialsFlat == null) return false;
		int tx = data.Tx > 0 ? data.Tx : data.TailleChunk + 1;
		int ty = data.Ty > 0 ? data.Ty : data.HauteurMax + 1;
		int tz = data.Tz > 0 ? data.Tz : data.TailleChunk + 1;
		if (lx < 0 || lx >= tx || ly < 0 || ly >= ty || lz < 0 || lz >= tz)
			return false;
		int index = lx * ty * tz + ly * tz + lz;
		if ((uint)index >= (uint)data.MaterialsFlat.Length)
			return false;
		idMateriau = data.MaterialsFlat[index];
		return true;
	}

	private static Color ObtenirCouleurTerrainDepuisChunkData(ChunkData data, int xGlobal, int yGlobal, int zGlobal)
	{
		if (data?.MaterialsFlat == null || data.NoiseTemperature == null || data.NoiseHumidite == null) return new Color(0.5f, 0.6f, 0.5f);
		int lx = xGlobal - data.Coordonnees.X * data.TailleChunk;
		int lz = zGlobal - data.Coordonnees.Y * data.TailleChunk;
		int ly = yGlobal - (int)data.ObtenirOffsetYMonde();
		if (!EssayerLireMateriauDepuisChunkData(data, lx, ly, lz, out byte idMat))
			return new Color(0.5f, 0.6f, 0.5f);
		float temp = data.NoiseTemperature.GetNoise2D(xGlobal, zGlobal);
		float hum = CalculerHumiditeGlobaleDepuisChunkData(data, xGlobal, zGlobal);
		float facteurHum = Mathf.Clamp((hum + 1f) * 0.5f, 0f, 1f);
		if (idMat != 1) return new Color(0.5f, 0.6f, 0.5f);
		return CalculerCouleurHerbeBiome(temp, facteurHum);
	}

	private static float CalculerHumiditeGlobaleDepuisChunkData(ChunkData data, float xGlobal, float zGlobal)
	{
		float macro = data.NoiseHumidite.GetNoise2D(xGlobal, zGlobal);
		float micro = data.NoiseHumiditeDetail != null ? data.NoiseHumiditeDetail.GetNoise2D(xGlobal, zGlobal) : 0f;
		return Mathf.Clamp(macro * 0.85f + micro * 0.15f, -1f, 1f);
	}

	/// <summary>Version statique pour ChunkData : max inchangé en humide, réduit en sec.</summary>
	private static int CalculerDensiteGazonSelonHumiditeChunkData(float humiditeGlobale, int densiteMax)
	{
		float facteurHum = Mathf.Clamp((humiditeGlobale + 1f) * 0.5f, 0f, 1f);
		float multiplicateur = Mathf.Lerp(0.90f, 1.0f, facteurHum);
		return Mathf.Clamp(Mathf.RoundToInt(densiteMax * multiplicateur), 1, densiteMax);
	}

	private static bool EstMateriauSupportGazon(byte mat)
	{
		// Gazon uniquement sur voxel herbe (ID 1).
		return mat == 1;
	}

	/// <summary>Génère l'inventaire flore (gazon) à partir de la surface du chunk. Appelé au chargement pour afficher l'herbe.</summary>
	private static Dictionary<Vector3I, byte> GenererInventaireFloreDepuisSurface(ChunkData data)
	{
		var inv = new Dictionary<Vector3I, byte>();
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null || data.TailleChunk <= 0 || data.HauteurMax <= 0) return inv;
		const float isolevel = 0.0f;
		int tc = data.TailleChunk;
		int ox = data.Coordonnees.X * tc;
		int oz = data.Coordonnees.Y * tc;
		for (int lx = 0; lx < tc; lx++)
			for (int lz = 0; lz < tc; lz++)
			{
				int ySurface = -1;
				for (int y = data.HauteurMax - 1; y >= 0; y--)
				{
					float d = data.DensitiesFlat[data.Idx(lx, y, lz)];
					if (d <= isolevel) continue;
					bool videAuDessus = y + 1 > data.HauteurMax || data.DensitiesFlat[data.Idx(lx, y + 1, lz)] <= isolevel;
					if (videAuDessus) { ySurface = y; break; }
				}
				if (ySurface < 2) continue;
				byte mat = data.MaterialsFlat[data.Idx(lx, ySurface, lz)];
				if (!EstMateriauSupportGazon(mat)) continue;
				float dy = data.DensitiesFlat[data.Idx(lx, Math.Min(ySurface + 1, data.HauteurMax), lz)] - data.DensitiesFlat[data.Idx(lx, Math.Max(0, ySurface - 1), lz)];
				float dx = data.DensitiesFlat[data.Idx(Math.Min(lx + 1, tc), ySurface, lz)] - data.DensitiesFlat[data.Idx(Math.Max(0, lx - 1), ySurface, lz)];
				float dz = data.DensitiesFlat[data.Idx(lx, ySurface, Math.Min(lz + 1, tc))] - data.DensitiesFlat[data.Idx(lx, ySurface, Math.Max(0, lz - 1))];
				Vector3 grad = new Vector3(dx, dy, dz);
				if (grad.LengthSquared() < 0.0001f) continue;
				Vector3 normal = (-grad).Normalized();
				if (normal.Y < 0.82f) continue; // bloque la flore sur fortes pentes/côtés
				var posGlobale = new Vector3I(ox + lx, (int)data.ObtenirOffsetYMonde() + ySurface, oz + lz);
				inv[posGlobale] = 0; // gazon
			}
		return inv;
	}
}
