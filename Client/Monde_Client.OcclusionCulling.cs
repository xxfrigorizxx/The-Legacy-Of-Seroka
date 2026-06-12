using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Occlusion visuelle façon Minecraft : masque le rendu (pas la sauvegarde / collision) quand un objet
/// est caché derrière le terrain ou un meuble statique. Combine occludeurs Godot (Embree) + raycasts physiques.
/// </summary>
public partial class Monde_Client : Node3D
{
	[ExportGroup("Occlusion visuelle")]
	/// <summary>Désactivé par défaut : la version précédente masquait tout le monde (Embree + Visible=false).</summary>
	[Export] public bool ActiverOcclusionVisuelle = false;
	/// <summary>Rayons physiques max par frame pour objets + chunks RenderingServer.</summary>
	[Export(PropertyHint.Range, "8,256,4")] public int BudgetRayonsOcclusionParFrame = 96;
	[Export(PropertyHint.Range, "8,128,4")] public int BudgetRayonsOcclusionFpsBas = 40;
	[Export] public float IntervalleOcclusionSec = 0.05f;
	/// <summary>Distance max (m) pour tester l'occlusion (au-delà : visible par défaut).</summary>
	[Export] public float DistanceMaxTestOcclusionMetres = 220f;
	/// <summary>Marge (m) sur l'impact rayon : évite les clignotements sur les bords.</summary>
	[Export] public float MargeImpactOcclusionMetres = 0.38f;
	/// <summary>Faces voxel détaillées pour Embree (coûteux à l'intégration) ; sinon boîte AABB du mesh.</summary>
	[Export] public bool OccludeursTerrainVoxelDetailles = false;

	private const uint MasqueCollisionOcclusion = 1u;
	private float _timerOcclusion;
	private int _indexScanOcclusionChunks;
	private int _indexScanOcclusionObjets;
	private readonly List<ChunkData> _listeChunksOcclusionTravail = new List<ChunkData>(512);
	private readonly List<ItemPhysique> _listeObjetsOcclusionTravail = new List<ItemPhysique>(512);
	private readonly List<ItemPhysique> _objetsPosesOcclusion = new List<ItemPhysique>(512);
	private readonly HashSet<ItemPhysique> _setObjetsPosesOcclusion = new HashSet<ItemPhysique>();
	private static readonly Vector3[] _echantillonsBoite = new Vector3[5];

	public bool EstOcclusionVisuelleActivee => ActiverOcclusionVisuelle;

	private void InitialiserOcclusionVisuelle()
	{
		AddToGroup("MondeClient");
		// Hotfix : version expérimentale — masquait tout + lag ; réactivation manuelle plus tard.
		ActiverOcclusionVisuelle = false;
		Callable.From(FinaliserInitialisationOcclusionVisuelle).CallDeferred();
	}

	private void FinaliserInitialisationOcclusionVisuelle()
	{
		Viewport vp = GetViewport();
		if (!ActiverOcclusionVisuelle)
		{
			if (vp != null)
				vp.UseOcclusionCulling = false;
			RestaurerVisibiliteForceeApresOcclusion();
			return;
		}
		if (vp != null)
			vp.UseOcclusionCulling = true;
		EnregistrerTousObjetsPosesOcclusionExistants();
	}

	/// <summary>Annule les effets d'occlusion (objets + terrain) — appelé au boot et si l'option est coupée.</summary>
	internal void RestaurerVisibiliteForceeApresOcclusion()
	{
		RestaurerVisibiliteForceeObjetsPoses();
		PurgerOccludeursTerrainTousChunks();
		Vector3 pos = ObtenirPositionObservation();
		void ResetChunk(ChunkData data)
		{
			if (data == null) return;
			LibererOccludeurTerrainChunk(data);
			data.OcclusionVisible = true;
			AppliquerVisibiliteRenduFinaleChunk(data, pos, replanifierFloreSiVisible: false);
		}
		foreach (var kv in _chunksData) ResetChunk(kv.Value);
		foreach (var kv in _chunksDataProfondeur3D) ResetChunk(kv.Value);
		foreach (var kv in _chunksDataAbysse3D) ResetChunk(kv.Value);
		_timerOcclusion = 0f;
		_objetsPosesOcclusion.Clear();
		_setObjetsPosesOcclusion.Clear();
	}

	private void RestaurerVisibiliteForceeObjetsPoses()
	{
		var arbre = GetTree();
		if (arbre == null)
			return;
		foreach (Node n in arbre.GetNodesInGroup("BlocsPoses"))
		{
			if (n is ItemPhysique item)
				item.ForcerVisibiliteRenduComplete();
			else if (n is Node3D n3d)
				n3d.Visible = true;
		}
	}

	private void PurgerOccludeursTerrainTousChunks()
	{
		void Purger(ChunkData d) => LibererOccludeurTerrainChunk(d);
		foreach (var kv in _chunksData) Purger(kv.Value);
		foreach (var kv in _chunksDataProfondeur3D) Purger(kv.Value);
		foreach (var kv in _chunksDataAbysse3D) Purger(kv.Value);
	}

	private void EnregistrerTousObjetsPosesOcclusionExistants()
	{
		var arbre = GetTree();
		if (arbre == null)
			return;
		foreach (Node n in arbre.GetNodesInGroup("BlocsPoses"))
		{
			if (n is ItemPhysique item)
				EnregistrerObjetPoseOcclusion(item);
		}
	}

	internal void EnregistrerObjetPoseOcclusion(ItemPhysique item)
	{
		if (item == null || !GodotObject.IsInstanceValid(item))
			return;
		if (!ActiverOcclusionVisuelle)
		{
			item.ForcerVisibiliteRenduComplete();
			return;
		}
		if (!_setObjetsPosesOcclusion.Add(item))
			return;
		_objetsPosesOcclusion.Add(item);
		item.OcclusionVisible = true;
		item.AppliquerVisibiliteRenduObjetPose();
	}

	internal void RetirerObjetPoseOcclusion(ItemPhysique item)
	{
		if (item == null)
			return;
		if (_setObjetsPosesOcclusion.Remove(item))
			_objetsPosesOcclusion.Remove(item);
	}

	/// <summary>Crée / met à jour l'occludeur Embree (faces extérieures voxel) pour le culling moteur des Node3D.</summary>
	internal void ConstruireOuMettreAJourOccludeurTerrainChunk(ChunkData data, Vector3 origineMondeLocale)
	{
		if (!ActiverOcclusionVisuelle || data == null)
			return;
		LibererOccludeurTerrainChunk(data);
		Aabb boite = data.BoiteMondeRendu;
		if (boite.Size.LengthSquared() < 0.01f)
			return;
		Occluder3D occluderRes = OccludeursTerrainVoxelDetailles
			? ConstruireOccludeurVoxelFacesExterieures(data)
			: null;
		if (occluderRes == null)
			occluderRes = new BoxOccluder3D { Size = boite.Size.Max(Vector3.One * 0.5f) };
		var noeud = new OccluderInstance3D
		{
			Name = $"OccluderChunk_{data.Coordonnees.X}_{data.CoordChunkY}_{data.Coordonnees.Y}",
			Occluder = occluderRes,
			Position = origineMondeLocale + boite.GetCenter(),
			Visible = true
		};
		AddChild(noeud);
		data.OccludeurTerrain = noeud;
	}

	internal void LibererOccludeurTerrainChunk(ChunkData data)
	{
		if (data?.OccludeurTerrain == null)
			return;
		if (GodotObject.IsInstanceValid(data.OccludeurTerrain))
			data.OccludeurTerrain.QueueFree();
		data.OccludeurTerrain = null;
	}

	internal static Aabb CalculerBoiteLocaleDepuisSommets(Vector3[] sommets)
	{
		if (sommets == null || sommets.Length == 0)
			return new Aabb();
		Vector3 min = sommets[0];
		Vector3 max = sommets[0];
		for (int i = 1; i < sommets.Length; i++)
		{
			Vector3 v = sommets[i];
			min = min.Min(v);
			max = max.Max(v);
		}
		return new Aabb(min, max - min);
	}

	private ArrayOccluder3D ConstruireOccludeurVoxelFacesExterieures(ChunkData data)
	{
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null)
			return null;
		int tc = data.TailleChunk;
		int h = data.HauteurMax;
		var verts = new List<Vector3>(2048);
		var ind = new List<int>(4096);
		bool Solide(int x, int y, int z)
		{
			if (x < 0 || x > tc || y < 0 || y > h || z < 0 || z > tc)
				return false;
			return data.DensitiesFlat[data.Idx(x, y, z)] > 0f && data.MaterialsFlat[data.Idx(x, y, z)] != 0;
		}
		void AjouterQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
		{
			int baseIdx = verts.Count;
			verts.Add(a);
			verts.Add(b);
			verts.Add(c);
			verts.Add(d);
			ind.Add(baseIdx);
			ind.Add(baseIdx + 1);
			ind.Add(baseIdx + 2);
			ind.Add(baseIdx);
			ind.Add(baseIdx + 2);
			ind.Add(baseIdx + 3);
		}
		for (int x = 0; x <= tc; x++)
		{
			for (int y = 0; y <= h; y++)
			{
				for (int z = 0; z <= tc; z++)
				{
					if (!Solide(x, y, z))
						continue;
					if (!Solide(x - 1, y, z))
						AjouterQuad(new Vector3(x, y, z + 1), new Vector3(x, y, z), new Vector3(x, y + 1, z), new Vector3(x, y + 1, z + 1));
					if (!Solide(x + 1, y, z))
						AjouterQuad(new Vector3(x + 1, y, z), new Vector3(x + 1, y, z + 1), new Vector3(x + 1, y + 1, z + 1), new Vector3(x + 1, y + 1, z));
					if (!Solide(x, y - 1, z))
						AjouterQuad(new Vector3(x, y, z), new Vector3(x + 1, y, z), new Vector3(x + 1, y, z + 1), new Vector3(x, y, z + 1));
					if (!Solide(x, y + 1, z))
						AjouterQuad(new Vector3(x, y + 1, z + 1), new Vector3(x + 1, y + 1, z + 1), new Vector3(x + 1, y + 1, z), new Vector3(x, y + 1, z));
					if (!Solide(x, y, z - 1))
						AjouterQuad(new Vector3(x + 1, y, z), new Vector3(x, y, z), new Vector3(x, y + 1, z), new Vector3(x + 1, y + 1, z));
					if (!Solide(x, y, z + 1))
						AjouterQuad(new Vector3(x, y, z + 1), new Vector3(x + 1, y, z + 1), new Vector3(x + 1, y + 1, z + 1), new Vector3(x, y + 1, z + 1));
				}
			}
		}
		if (verts.Count < 4)
			return null;
		var arr = new ArrayOccluder3D();
		arr.Vertices = verts.ToArray();
		arr.Indices = ind.ToArray();
		return arr;
	}

	private void MettreAJourOcclusionVisuelle(Vector3 positionObservation, Vector3 directionObservation, float dt)
	{
		if (!ActiverOcclusionVisuelle || !IsInsideTree())
			return;
		Camera3D camera = ObtenirCameraObservation();
		if (camera == null)
			return;
		_timerOcclusion -= dt;
		if (_timerOcclusion > 0f)
			return;
		float facteur = ModeSurvieFpsAgressif && _fpsMoyenneAuto < 52f ? 1.45f : 1f;
		if (_niveauUrgencePerf >= 2)
			facteur *= 1.25f;
		_timerOcclusion = IntervalleOcclusionSec * facteur;

		World3D world = GetWorld3D();
		PhysicsDirectSpaceState3D espace = world?.DirectSpaceState;
		if (espace == null)
			return;

		Vector3 origineRay = camera.GlobalPosition;
		Vector3 dirCam = directionObservation.Normalized();
		float distMax = Mathf.Max(32f, DistanceMaxTestOcclusionMetres);
		float distMax2 = distMax * distMax;
		int budget = ModeSurvieFpsAgressif && _fpsMoyenneAuto < 50f
			? BudgetRayonsOcclusionFpsBas
			: BudgetRayonsOcclusionParFrame;
		if (_niveauUrgencePerf >= 3)
			budget = Mathf.Max(8, budget / 2);
		int rayonsRestants = budget;

		RemplirListeChunksOcclusionUnique();
		int nChunks = _listeChunksOcclusionTravail.Count;
		if (nChunks > 0)
		{
			if (_indexScanOcclusionChunks >= nChunks)
				_indexScanOcclusionChunks = 0;
			int scansChunk = Mathf.Min(nChunks, Mathf.Max(rayonsRestants, 8));
			for (int s = 0; s < scansChunk && rayonsRestants > 0; s++)
			{
				if (_indexScanOcclusionChunks >= nChunks)
					_indexScanOcclusionChunks = 0;
				ChunkData data = _listeChunksOcclusionTravail[_indexScanOcclusionChunks++];
				if (data == null || !data.VisualInstanceRID.IsValid)
					continue;
				if (!data.CullingVisible)
				{
					if (!data.OcclusionVisible)
					{
						data.OcclusionVisible = true;
						AppliquerVisibiliteRenduFinaleChunk(data, positionObservation, replanifierFloreSiVisible: false);
					}
					continue;
				}
				Aabb boiteMonde = BoiteMondeDepuisChunkRendu(data);
				if (!BoitePassePreTestOcclusion(boiteMonde, origineRay, dirCam, distMax2))
				{
					DefinirOcclusionChunk(data, true, positionObservation);
					continue;
				}
				bool occlus = EstBoiteOccludee(espace, origineRay, boiteMonde, data.PhysicsBodyRID);
				DefinirOcclusionChunk(data, !occlus, positionObservation);
				rayonsRestants--;
			}
		}

		NettoyerObjetsOcclusionInvalides();
		int nObj = _objetsPosesOcclusion.Count;
		if (nObj > 0 && rayonsRestants > 0)
		{
			if (_indexScanOcclusionObjets >= nObj)
				_indexScanOcclusionObjets = 0;
			int scansObj = Mathf.Min(nObj, rayonsRestants);
			for (int s = 0; s < scansObj && rayonsRestants > 0; s++)
			{
				if (_indexScanOcclusionObjets >= nObj)
					_indexScanOcclusionObjets = 0;
				ItemPhysique item = _objetsPosesOcclusion[_indexScanOcclusionObjets++];
				if (!EstObjetPoseOcclusionValide(item))
					continue;
				Aabb boiteMonde = BoiteMondeDepuisItem(item);
				if (!BoitePassePreTestOcclusion(boiteMonde, origineRay, dirCam, distMax2))
				{
					DefinirOcclusionObjet(item, true);
					continue;
				}
				Rid exclure = item.GetRid();
				bool occlus = EstBoiteOccludee(espace, origineRay, boiteMonde, exclure);
				DefinirOcclusionObjet(item, !occlus);
				rayonsRestants--;
			}
		}
	}

	private void RemplirListeChunksOcclusionUnique()
	{
		_listeChunksOcclusionTravail.Clear();
		var vus = new HashSet<ChunkData>();
		void Ajouter(ChunkData d)
		{
			if (d != null && d.VisualInstanceRID.IsValid && vus.Add(d))
				_listeChunksOcclusionTravail.Add(d);
		}
		foreach (var kv in _chunksData)
			Ajouter(kv.Value);
		foreach (var kv in _chunksDataProfondeur3D)
			Ajouter(kv.Value);
		foreach (var kv in _chunksDataAbysse3D)
			Ajouter(kv.Value);
	}

	private Aabb BoiteMondeDepuisChunkRendu(ChunkData data)
	{
		Vector3 origine = GlobalPosition + data.ObtenirOrigineMonde(TailleChunk);
		Aabb boite = data.BoiteMondeRendu;
		if (boite.Size.LengthSquared() < 0.01f)
			boite = new Aabb(Vector3.Zero, Vector3.One * TailleChunk);
		return new Aabb(origine + boite.Position, boite.Size);
	}

	private static Aabb BoiteMondeDepuisItem(ItemPhysique item)
	{
		Aabb local = item.OcclusionBoiteLocale;
		if (local.Size.LengthSquared() < 0.0001f)
			local = new Aabb(Vector3.Zero, Vector3.One * 0.35f);
		return item.GlobalTransform * local;
	}

	private static bool BoitePassePreTestOcclusion(Aabb boiteMonde, Vector3 origineRay, Vector3 dirCam, float distMax2)
	{
		Vector3 centre = boiteMonde.GetCenter();
		Vector3 vers = centre - origineRay;
		float d2 = vers.LengthSquared();
		if (d2 > distMax2)
			return false;
		if (d2 > 0.25f && vers.Normalized().Dot(dirCam) < -0.05f)
			return false;
		return true;
	}

	private bool EstBoiteOccludee(PhysicsDirectSpaceState3D espace, Vector3 origineRay, Aabb boiteMonde, Rid exclureCorps)
	{
		RemplirEchantillonsBoite(boiteMonde);
		int bloques = 0;
		int testes = 0;
		for (int i = 0; i < _echantillonsBoite.Length; i++)
		{
			Vector3 cible = _echantillonsBoite[i];
			Vector3 delta = cible - origineRay;
			float dist = delta.Length();
			if (dist < 0.35f)
				return false;
			var requete = PhysicsRayQueryParameters3D.Create(origineRay, cible);
			requete.CollisionMask = MasqueCollisionOcclusion;
			requete.CollideWithAreas = false;
			if (exclureCorps.IsValid)
				requete.Exclude = new Godot.Collections.Array<Rid> { exclureCorps };
			var impact = espace.IntersectRay(requete);
			testes++;
			if (impact.Count == 0 || !impact.ContainsKey("position"))
				continue;
			float distImpact = origineRay.DistanceTo((Vector3)impact["position"]);
			if (distImpact < dist - MargeImpactOcclusionMetres)
				bloques++;
		}
		if (testes == 0)
			return false;
		return bloques >= 3 || bloques >= testes - 1;
	}

	private static void RemplirEchantillonsBoite(Aabb boiteMonde)
	{
		Vector3 c = boiteMonde.GetCenter();
		Vector3 e = boiteMonde.Size * 0.5f;
		_echantillonsBoite[0] = c;
		_echantillonsBoite[1] = c + new Vector3(-e.X, e.Y, -e.Z);
		_echantillonsBoite[2] = c + new Vector3(e.X, e.Y, -e.Z);
		_echantillonsBoite[3] = c + new Vector3(-e.X, e.Y, e.Z);
		_echantillonsBoite[4] = c + new Vector3(e.X, e.Y, e.Z);
	}

	private void DefinirOcclusionChunk(ChunkData data, bool visible, Vector3 positionObservation)
	{
		if (data.OcclusionVisible == visible)
			return;
		data.OcclusionVisible = visible;
		AppliquerVisibiliteRenduFinaleChunk(data, positionObservation, replanifierFloreSiVisible: visible);
	}

	private void DefinirOcclusionObjet(ItemPhysique item, bool visible)
	{
		if (item.OcclusionVisible == visible)
			return;
		item.OcclusionVisible = visible;
		item.AppliquerVisibiliteRenduObjetPose();
	}

	private void NettoyerObjetsOcclusionInvalides()
	{
		for (int i = _objetsPosesOcclusion.Count - 1; i >= 0; i--)
		{
			ItemPhysique item = _objetsPosesOcclusion[i];
			if (EstObjetPoseOcclusionValide(item))
				continue;
			_setObjetsPosesOcclusion.Remove(item);
			_objetsPosesOcclusion.RemoveAt(i);
		}
	}

	private static bool EstObjetPoseOcclusionValide(ItemPhysique item)
		=> item != null && GodotObject.IsInstanceValid(item) && item.IsInsideTree() && item.IsInGroup("BlocsPoses");

	/// <summary>Applique CullingVisible × OcclusionVisible sur terrain / eau / flore.</summary>
	private void AppliquerVisibiliteRenduFinaleChunk(ChunkData data, Vector3 positionObservation, bool replanifierFloreSiVisible)
	{
		if (data == null)
			return;
		bool visible = data.CullingVisible && data.OcclusionVisible;
		if (data.RenduVisibleEffectif == visible)
			return;
		bool etaitVisible = data.RenduVisibleEffectif;
		data.RenduVisibleEffectif = visible;
		if (data.VisualInstanceRID.IsValid)
			RenderingServer.Singleton.InstanceSetVisible(data.VisualInstanceRID, visible);
		if (data.WaterInstanceRID.IsValid)
			RenderingServer.Singleton.InstanceSetVisible(data.WaterInstanceRID, visible);
		if (data._nodeFlore is Node3D flore && flore.Visible != visible)
			flore.Visible = visible;
		if (replanifierFloreSiVisible && visible && !etaitVisible && data.InventaireFlore != null && data.InventaireFlore.Count > 0)
		{
			if (data._nodeFlore is Node3D floreVisible && GodotObject.IsInstanceValid(floreVisible))
				Chunk_Client.MettreAJourFlorePourChunkData(data, positionObservation, floreVisible);
			else
				EnfilerFloreChunk(data, positionObservation);
		}
	}
}
