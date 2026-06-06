using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	private void AssurerCorpsPhysiqueChunk(ChunkData data)
	{
		if (data == null || data.PhysicsBodyRID.IsValid || data._meshRef == null) return;
		World3D world = GetWorld3D();
		if (world == null) return;

		Shape3D shape = null;
		// Shape pré-construite (BVH déjà bâti sur le thread de fond) : évite le pic CreateTrimeshShape ici.
		if (data.ShapeCollisionPrecalc != null)
		{
			shape = data.ShapeCollisionPrecalc;
			data.ShapeCollisionPrecalc = null;
		}
		if (shape == null)
		{
			try { shape = data._meshRef.CreateTrimeshShape(); }
			catch (Exception) { shape = null; }
		}
		if (shape == null) return;

		Transform3D transformChunk = new Transform3D(
			Basis.Identity,
			GlobalPosition + data.ObtenirOrigineMonde(TailleChunk));

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
		float farRadiusCible = Mathf.Max(nearRadius + TailleChunk * 10f, RayonHorizonChunks * TailleChunk);
		// Horizon borné par RenderDistance (+ marge) : évite des silhouettes à des distances non demandées par le joueur.
		float limiteRender = Mathf.Max(nearRadius + TailleChunk * 2f, (Mathf.Max(6, RenderDistance) + 2) * TailleChunk);
		float farRadius = Mathf.Min(farRadiusCible, limiteRender);
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

	/// <summary>Applique la visibilité culling terrain/eau/flore. Si passage à visible, peut ré-enfiler la flore (densité vue de près).</summary>
	private void AppliquerVisibiliteCullingSurChunk(ChunkData data, bool visible, Vector3 positionObservation, bool replanifierFloreSiVisible)
	{
		if (data == null || data.CullingVisible == visible) return;
		bool etaitVisible = data.CullingVisible;
		data.CullingVisible = visible;
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

	/// <summary>Remet tout le terrain déjà chargé en visible (anti-trous après changement d’options / désactivation « Sauver les FPS »).</summary>
	private void ReinitialiserVisibiliteCullingTousLesChunksCharges(Vector3 positionObservation)
	{
		foreach (var kv in _chunksData)
		{
			ChunkData data = kv.Value;
			if (data == null) continue;
			AppliquerVisibiliteCullingSurChunk(data, true, positionObservation, replanifierFloreSiVisible: false);
		}
		_timerCullingCamera = 0f;
	}

	/// <summary>Rétablit la visibilité dans un disque autour du point d’observation (marge + dormance) après rotation caméra.</summary>
	private void ReinitialiserCullingVisibleDisqueObservation(Vector3 positionObservation)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		int r = DisqueToujoursVisibleChunksCulling();
		for (int dx = -r; dx <= r; dx++)
		for (int dz = -r; dz <= r; dz++)
		{
			Vector2I c = new Vector2I(obs.X + dx, obs.Y + dz);
			if (!_chunksData.TryGetValue(c, out ChunkData data)) continue;
			AppliquerVisibiliteCullingSurChunk(data, true, positionObservation, replanifierFloreSiVisible: true);
		}
	}

	private void AppliquerCullingCameraChunks(Vector3 positionObservation, Vector3 directionObservation, float dt)
	{
		if (!ActiverCullingCameraChunks) return;
		_timerCullingCamera -= dt;
		if (_timerCullingCamera > 0f) return;
		_timerCullingCamera = _intervalleCullingDyn;
		AssurerCacheCoordsChunks();
		if (_cacheCoordsChunks.Count == 0)
		{
			if (_framesBoostCullingRotationRestantes > 0)
				_framesBoostCullingRotationRestantes--;
			return;
		}
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
		if (_framesBoostCullingRotationRestantes > 0)
		{
			basculesRestantes = Mathf.Max(basculesRestantes, Mathf.Max(48, MaxBasculesCullingParPasse));
			chunksAEvaluer = Mathf.Min(chunksAEvaluerBase, Mathf.RoundToInt(chunksAEvaluer * 2.2f));
		}

		float cosHalf = Mathf.Cos(Mathf.DegToRad(Mathf.Clamp(AngleCullingCameraDeg, 80f, 175f) * 0.5f));
		float rayonToujoursVisible = DisqueToujoursVisibleChunksCulling() * TailleChunk;
		float rayonToujoursVisibleCarre = rayonToujoursVisible * rayonToujoursVisible;

		int total = _cacheCoordsChunks.Count;
		// En mode « Sauver les FPS », le budget de bascules peut laisser des chunks dans un état visible=false obsolète → trous noirs.
		// Hors ce mode : on évalue tout le cache chaque passe (coût assumé par l’utilisateur qui veut le contrôle panneau).
		if (!ModeSurvieFpsAgressif && total > 0)
		{
			chunksAEvaluer = total;
			basculesRestantes = Mathf.Max(basculesRestantes, total);
		}
		int evalues = 0;
		while (evalues < chunksAEvaluer)
		{
			total = _cacheCoordsChunks.Count;
			if (total <= 0) break;
			if (_indexCullingScan >= total) _indexCullingScan = 0;
			if ((uint)_indexCullingScan >= (uint)_cacheCoordsChunks.Count) break;
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

			if (data.CullingVisible == visible)
				continue;
			if (basculesRestantes <= 0) break;
			AppliquerVisibiliteCullingSurChunk(data, visible, positionObservation, replanifierFloreSiVisible: true);
			basculesRestantes--;
			if (basculesRestantes <= 0) break;
		}
		if (_framesBoostCullingRotationRestantes > 0)
			_framesBoostCullingRotationRestantes--;
	}

	private bool EstChunkProche(Vector2I coordChunk, Vector3 positionObservation, int rayonChunks)
	{
		Vector2I obs = Gestionnaire_Monde.WorldToChunkCoord(positionObservation, TailleChunk);
		return Mathf.Abs(coordChunk.X - obs.X) <= rayonChunks && Mathf.Abs(coordChunk.Y - obs.Y) <= rayonChunks;
	}

	/// <summary>Dès que le corps statique du chunk est dans l'espace (collision réelle), applique la flore sans attendre <see cref="TraiterFloreDifferee"/>.</summary>
	private void SynchroniserFloreDesQueCollisionChunkActive(ChunkData data)
	{
		if (data == null) return;
		if (data.InventaireFlore == null || data.InventaireFlore.Count == 0) return;
		if (!data.PhysicsBodyRID.IsValid || data.Dormant) return;
		ConstruireFloreChunk(data, ObtenirPositionInteractionFlore());
	}

	private static Vector3I CleFlorePourChunkData(ChunkData data)
		=> new Vector3I(data.Coordonnees.X, data.CoordChunkY, data.Coordonnees.Y);

	private bool ChunkFloreEncoreCharge(ChunkData data)
	{
		if (data == null)
			return false;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			Vector3I cle = CleFlorePourChunkData(data);
			return _chunksDataAbysse3D.TryGetValue(cle, out ChunkData d) && ReferenceEquals(d, data);
		}
		if (ModeProfondeurTranchesActif())
		{
			Vector3I cle = CleFlorePourChunkData(data);
			return _chunksDataProfondeur3D.TryGetValue(cle, out ChunkData dProf) && ReferenceEquals(dProf, data);
		}
		return _chunksData.TryGetValue(data.Coordonnees, out ChunkData d2) && ReferenceEquals(d2, data);
	}

	private void RetirerFloreDiffereePourChunk(ChunkData data)
	{
		if (data == null) return;
		Vector3I cle = CleFlorePourChunkData(data);
		_setFloreDifferee.Remove(cle);
		_fileFloreDifferee.Remove(cle);
		_frameEnqueueFlore.Remove(cle);
	}

	private void EnfilerFloreChunk(ChunkData data, Vector3 positionObservation)
	{
		if (data == null || data.InventaireFlore == null || data.InventaireFlore.Count == 0) return;
		if (data._nodeFlore is Node3D existant && GodotObject.IsInstanceValid(existant))
		{
			Chunk_Client.MettreAJourFlorePourChunkData(data, positionObservation, existant);
			existant.Visible = data.CullingVisible;
			RetirerFloreDiffereePourChunk(data);
			return;
		}
		Vector3I cle = CleFlorePourChunkData(data);
		if (_setFloreDifferee.Add(cle))
		{
			_fileFloreDifferee.Add(cle);
			_frameEnqueueFlore[cle] = Engine.GetPhysicsFrames();
		}
	}

	private void ConstruireFloreChunk(ChunkData data, Vector3 positionObservation)
	{
		if (data == null || !ChunkFloreEncoreCharge(data))
			return;
		Vector3I cleFlore = CleFlorePourChunkData(data);
		_setFloreDifferee.Remove(cleFlore);
		_fileFloreDifferee.Remove(cleFlore);
		_frameEnqueueFlore.Remove(cleFlore);
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
		Vector3 posObs = ObtenirPositionObservation();
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			foreach (var kv in _chunksDataAbysse3D)
			{
				Vector3I key = kv.Key;
				int ddx = Mathf.Abs(key.X - chunkCentre.X);
				int ddz = Mathf.Abs(key.Z - chunkCentre.Y);
				if (ddx > rayon || ddz > rayon)
					continue;
				if (!EstCoordYDansFenetrePaliersAbysse(key.Y, posObs))
					continue;
				ChunkData data = kv.Value;
				if (data?.InventaireFlore == null || data.InventaireFlore.Count == 0)
					continue;
				EnfilerFloreChunk(data, posObs);
			}
			return;
		}
		if (ModeProfondeurTranchesActif())
		{
			int cyObs = CoordYDepuisMondeY((int)Mathf.Floor(posObs.Y));
			int demiFenetre = ConstantesProfondeurVerticale.DemiFenetreTranches;
			foreach (var kv in _chunksDataProfondeur3D)
			{
				Vector3I key = kv.Key;
				int ddx = Mathf.Abs(key.X - chunkCentre.X);
				int ddz = Mathf.Abs(key.Z - chunkCentre.Y);
				if (ddx > rayon || ddz > rayon)
					continue;
				if (Mathf.Abs(key.Y - cyObs) > demiFenetre)
					continue;
				ChunkData data = kv.Value;
				if (data?.InventaireFlore == null || data.InventaireFlore.Count == 0)
					continue;
				EnfilerFloreChunk(data, posObs);
			}
			return;
		}
		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I coord = new Vector2I(chunkCentre.X + dx, chunkCentre.Y + dz);
				if (!_chunksData.TryGetValue(coord, out var data)) continue;
				if (data.InventaireFlore == null || data.InventaireFlore.Count == 0) continue;
				EnfilerFloreChunk(data, posObs);
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
			Vector3I coord = ExtraireCleFloreLaPlusProche(_fileFloreDifferee, positionObservation);
			if (_frameEnqueueFlore.TryGetValue(coord, out ulong frameAjout) && frameAjout >= frameCourante)
			{
				// Laisse au moins 1 frame entre l’intégration du chunk et la création de sa flore.
				_fileFloreDifferee.Add(coord);
				continue;
			}
			_setFloreDifferee.Remove(coord);
			_frameEnqueueFlore.Remove(coord);
			ChunkData data = null;
			if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
				_chunksDataAbysse3D.TryGetValue(coord, out data);
			else if (ModeProfondeurTranchesActif())
				_chunksDataProfondeur3D.TryGetValue(coord, out data);
			else if (_chunksData.TryGetValue(new Vector2I(coord.X, coord.Z), out var dAlpha) && dAlpha.CoordChunkY == coord.Y)
				data = dAlpha;
			if (data == null)
				continue;
			ConstruireFloreChunk(data, positionObservation);
			traites++;
		}
	}
}
