using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	/// <summary>Enfile un chunk pour calcul en arrière-plan (Forge restreinte). Ne lance pas de Task.Run : le lancement est limité à MaxTravailleurs dans _PhysicsProcess.</summary>
	public void EnqueueChunkGeneration(ChunkData data, DonneesChunk donnees)
	{
		if (data == null || donnees == null) return;
		ulong empreinte = CalculerEmpreinteDonneesChunk(donnees);
		if (data.VisualInstanceRID.IsValid && data.EmpreinteDonneesServeur == empreinte && empreinte != 0)
			return;
		data.EmpreinteDonneesServeur = empreinte;
		if (empreinte != 0)
		{
			data.CoutureVoxelAppliquee = false;
			data.CachePayloadsSections = null;
		}
		lock (_lockFileAttenteMaths)
		{
			for (int i = _fileAttenteMathsData.Count - 1; i >= 0; i--)
			{
				if (ReferenceEquals(_fileAttenteMathsData[i].data, data))
					_fileAttenteMathsData.RemoveAt(i);
			}
			_fileAttenteMathsData.Add((data, donnees));
		}
	}

	/// <summary>Architecture AAA : fusionne les 45 SectionPayload en un mesh + shape, crée les RIDs RenderingServer/PhysicsServer3D, attache au monde. À appeler sur le Main Thread.</summary>
	internal void IntegrerChunkDataRIDs(ChunkData data, List<SectionPayload> payloads, bool recoudreVoisinsVertical = true)
	{
		if (data == null || payloads == null || payloads.Count == 0 || !IsInsideTree()) return;
		World3D world = GetWorld3D();
		if (world == null) return;
		bool remplacerVisuelExistant = data.VisualInstanceRID.IsValid;
		if (remplacerVisuelExistant)
		{
			LibererPhysiqueChunk(data);
			// Mesh remplacé (remesh/mining) : une shape pré-calculée du mesh précédent serait périmée.
			data.ShapeCollisionPrecalc = null;
		}

		// 1. Fusion des payloads en un seul ArrayMesh (terrain) sans SurfaceTool/GenerateNormals.
		int totalTerrainVertices = 0;
		foreach (var p in payloads)
			if (p?.SommetsVisuels != null)
				totalTerrainVertices += p.SommetsVisuels.Length;
		if (totalTerrainVertices <= 0)
		{
			if (remplacerVisuelExistant)
				return;
			return;
		}

		int sommetsCacheAvant = 0;
		if (remplacerVisuelExistant && data.CachePayloadsSections != null)
		{
			foreach (var p in data.CachePayloadsSections)
				if (p?.SommetsVisuels != null)
					sommetsCacheAvant += p.SommetsVisuels.Length;
		}
		if (sommetsCacheAvant >= 512 && totalTerrainVertices < sommetsCacheAvant / 4)
			return;

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

		if (terrainOffset != totalTerrainVertices)
		{
			JournalErreursZeroK.Erreur($"ZERO-K : fusion terrain incohérente offset={terrainOffset} attendu={totalTerrainVertices} chunk ({data.Coordonnees.X},{data.Coordonnees.Y}) cy={data.CoordChunkY}.");
			Array.Resize(ref terrainVertices, terrainOffset);
			Array.Resize(ref terrainNormals, terrainOffset);
			Array.Resize(ref terrainColors, terrainOffset);
		}
		int nSommetsTerrain = terrainVertices.Length;
		int resteTri = nSommetsTerrain % 3;
		if (resteTri != 0)
		{
			int nv = nSommetsTerrain - resteTri;
			JournalErreursZeroK.Erreur($"ZERO-K : sommets terrain non multiple de 3 (n={nSommetsTerrain}), troncature de {resteTri} — chunk ({data.Coordonnees.X},{data.Coordonnees.Y}) cy={data.CoordChunkY}.");
			Array.Resize(ref terrainVertices, nv);
			Array.Resize(ref terrainNormals, nv);
			Array.Resize(ref terrainColors, nv);
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
			_materielTerrainCache ??= TerrainMaterialFactory.ObtenirMaterielTerrainRobuste();
			matTerrain = _materielTerrainCache;
		}
		if (matTerrain != null)
			mergedMesh.SurfaceSetMaterial(0, matTerrain);

		// RÈGLE CAS B (espace local) : les sommets du mesh sont en [0, TailleChunk] x [0, HauteurMax] x [0, TailleChunk].
		// Une SEULE application du décalage chunk : position monde = origine parent + (coordChunk * TailleChunk).
		// Pas de double translation (ne pas ajouter d'offset si les vertices étaient déjà en monde).
		Vector3 origineChunkLocale = data.ObtenirOrigineMonde(TailleChunk);
		Vector3 positionVraie = GlobalPosition + origineChunkLocale;
		Transform3D transformChunk = new Transform3D(Basis.Identity, positionVraie);
		if (ActiverOcclusionVisuelle)
		{
			data.BoiteMondeRendu = CalculerBoiteLocaleDepuisSommets(terrainVertices);
			ConstruireOuMettreAJourOccludeurTerrainChunk(data, origineChunkLocale);
		}

		// 2. RenderingServer : remplacement in-place après minage (évite le « vide » d’une frame) ou nouvelle instance.
		Rid meshRid = mergedMesh.GetRid();
		if (remplacerVisuelExistant)
		{
			RenderingServer.Singleton.InstanceSetBase(data.VisualInstanceRID, meshRid);
			RenderingServer.Singleton.InstanceSetTransform(data.VisualInstanceRID, transformChunk);
			if (data._meshRef != null && !ReferenceEquals(data._meshRef, mergedMesh))
				data._meshRef.Dispose();
			data._meshRef = mergedMesh;
		}
		else
		{
			Rid instanceRid = RenderingServer.Singleton.InstanceCreate();
			RenderingServer.Singleton.InstanceSetBase(instanceRid, meshRid);
			RenderingServer.Singleton.InstanceSetScenario(instanceRid, world.Scenario);
			RenderingServer.Singleton.InstanceSetTransform(instanceRid, transformChunk);
			data.VisualInstanceRID = instanceRid;
			data._meshRef = mergedMesh;
		}
		data.PhysicsBodyRID = default;
		data.PhysicsShapeRID = default;
		data._shapeRef = null;

		bool solidifieCorridor = false;
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurCorridor))
		{
			Vector3 posC = joueurCorridor.GlobalPosition;
			Vector3 velC = new Vector3(joueurCorridor.Velocity.X, 0f, joueurCorridor.Velocity.Z);
			solidifieCorridor = EssayerSolidifierCorridorAIntegration(data, posC, velC);
		}

		// Flore : remesh terrain seul → conserver le nœud (évite flash herbe/buissons lors des recousures bord).
		Vector3 posObsFlore = ObtenirPositionObservation();
		if (data.InventaireFlore == null || data.InventaireFlore.Count == 0)
		{
			if (data._nodeFlore != null)
			{
				data._nodeFlore.QueueFree();
				data._nodeFlore = null;
			}
		}
		else if (data._nodeFlore is Node3D floreExistante && GodotObject.IsInstanceValid(floreExistante))
		{
			Chunk_Client.MettreAJourFlorePourChunkData(data, posObsFlore, floreExistante);
			floreExistante.Visible = data.CullingVisible && data.OcclusionVisible;
			RetirerFloreDiffereePourChunk(data);
		}
		else
		{
			// STREAMING UN-A-UN : file différée + budget par frame. Seul le chunk sous les pieds
			// est construit immédiatement (sol nu évité).
			if (EssayerObtenirJoueurDansArbre(out CharacterBody3D _))
			{
				Vector2I cJoueurFlore = Gestionnaire_Monde.WorldToChunkCoord(posObsFlore, TailleChunk);
				int ddx = Mathf.Abs(data.Coordonnees.X - cJoueurFlore.X);
				int ddz = Mathf.Abs(data.Coordonnees.Y - cJoueurFlore.Y);
				bool chunkSousPiedsXZ = ddx == 0 && ddz == 0;
				if (chunkSousPiedsXZ)
				{
					if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
						chunkSousPiedsXZ = data.CoordChunkY == CoordYStageAbysseDepuisYMonde(posObsFlore.Y);
					else if (ModeProfondeurTranchesActif())
						chunkSousPiedsXZ = data.CoordChunkY == CoordYDepuisMondeY((int)Mathf.Floor(posObsFlore.Y));
				}
				if (chunkSousPiedsXZ)
					ConstruireFloreChunk(data, posObsFlore);
				else
					EnfilerFloreChunk(data, posObsFlore);
			}
			else
			{
				EnfilerFloreChunk(data, posObsFlore);
			}
		}

		// Physique lazy : seulement les tranches Y proches du joueur (±1) ; le reste reste visuel sans Jolt.
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurSolidif))
		{
			Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(joueurSolidif.GlobalPosition, TailleChunk);
			int dx = Mathf.Abs(data.Coordonnees.X - cJoueur.X);
			int dz = Mathf.Abs(data.Coordonnees.Y - cJoueur.Y);
			bool solidifier = true;
			if (ModeProfondeurTranchesActif())
			{
				int cyJoueur = CoordYDepuisMondeY((int)Mathf.Floor(joueurSolidif.GlobalPosition.Y));
				solidifier = Mathf.Abs(data.CoordChunkY - cyJoueur) <= ConstantesProfondeurVerticale.DemiFenetrePhysiqueTranches;
			}
			if (solidifier && !solidifieCorridor)
			{
				if (dx <= 1 && dz <= 1)
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
			else if (data.PhysicsBodyRID.IsValid)
			{
				RetirerDeFileSolidification(data);
				data.Dormant = true;
				PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, default(Rid));
				data.EstEnFileSolidification = false;
			}
		}
		SolidifierCollisionPrioritaireSiProcheJoueur(data);
		// 4. Eau : SurfaceTool (matériau eau existant).
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
		bool eauPresente = meshEau != null && meshEau.GetSurfaceCount() > 0;
		if (eauPresente)
		{
			Rid meshEauRid = meshEau.GetRid();
			var gestionnaire = GetParent() as Gestionnaire_Monde;
			if (data.WaterInstanceRID.IsValid)
			{
				RenderingServer.Singleton.InstanceSetBase(data.WaterInstanceRID, meshEauRid);
				RenderingServer.Singleton.InstanceSetTransform(data.WaterInstanceRID, transformChunk);
				if (data._meshEauRef != null && !ReferenceEquals(data._meshEauRef, meshEau))
					data._meshEauRef.Dispose();
			}
			else
			{
				Rid waterRid = RenderingServer.Singleton.InstanceCreate();
				RenderingServer.Singleton.InstanceSetBase(waterRid, meshEauRid);
				RenderingServer.Singleton.InstanceSetScenario(waterRid, world.Scenario);
				RenderingServer.Singleton.InstanceSetTransform(waterRid, transformChunk);
				data.WaterInstanceRID = waterRid;
			}
			if (gestionnaire != null && gestionnaire.MaterielEau != null)
				RenderingServer.Singleton.InstanceGeometrySetMaterialOverride(data.WaterInstanceRID, gestionnaire.MaterielEau.GetRid());
			else
				GD.PrintErr("CRITIQUE: MaterielEau non assigné (Gestionnaire_Monde._Ready n'a pas créé le matériau ou parent absent).");
			data._meshEauRef = meshEau;
		}
		else if (data.WaterInstanceRID.IsValid)
		{
			RenderingServer.Singleton.FreeRid(data.WaterInstanceRID);
			data.WaterInstanceRID = default;
			data._meshEauRef?.Dispose();
			data._meshEauRef = null;
		}

		// Fade-in d'émergence (anti pop-in) : on démarre le chunk en transparence totale
		// et on l'anime vers opaque en DureeFonduEmergenceChunk secondes. Purement visuel.
		if (!remplacerVisuelExistant && DureeFonduEmergenceChunk > 0.01f && data.VisualInstanceRID.IsValid)
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

		SynchroniserProxyChunkProfondeur(data.Coordonnees);
		RestaurerCollisionImmediateSiSousJoueur(data);
		data.CachePayloadsSections = new List<SectionPayload>(payloads);
		if (recoudreVoisinsVertical && ModeProfondeurTranchesActif())
			RecoudreVoisinsVerticalApresIntegration(data);
		if (_voxelsModifiesEnAttente.Count > 0
			&& EstRemeshPrioritaireMinage(new Vector3I(data.Coordonnees.X, data.CoordChunkY, data.Coordonnees.Y)))
			AppliquerVoxelsEnAttente();
		if (ActiverOcclusionVisuelle)
			AppliquerVisibiliteRenduFinaleChunk(data, ObtenirPositionObservation(), replanifierFloreSiVisible: false);
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
		if (coordSpawn == _dernierChunkReservePrioritaire && _cooldownReservePrioritaireSec > 0f)
			return;
		_dernierChunkReservePrioritaire = coordSpawn;
		_cooldownReservePrioritaireSec = 2.5f;
		// Cap strict : au plus ce qu’il faut pour la dormance + marge ; le radar remplira le reste progressivement.
		int rayonSpawn = ModeProfondeurTranchesActif()
			? Mathf.Min(RayonChargementChunksActif(), RayonDormancePhysique + MargePreloadChunks + 2)
			: Mathf.Min(RayonChargementChunksActif(), Mathf.Max(RayonDormancePhysique + MargePreloadChunks + 8, 12));
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
		if (ModeProfondeurTranchesActif())
		{
			Vector3 posUrgence = EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurSpawn)
				? joueurSpawn.GlobalPosition
				: ObtenirPositionObservation();
			DemanderFenetreVerticaleUrgenteAutourPosition(posUrgence, rayonXZ: 1, demiFenetreY: DemiFenetreTranchesStreamingActif());
		}
	}

	/// <summary>Au spawn : force les tranches verticales autour du point (fenêtre réduite au boot pour limiter le pic FPS).</summary>
	private void DemanderFenetreVerticaleUrgenteAutourPosition(Vector3 posMonde, int rayonXZ, int demiFenetreY = -1)
	{
		if (_networkManager == null || !ModeProfondeurTranchesActif())
			return;
		int demiY = demiFenetreY >= 0 ? demiFenetreY : DemiFenetreTranchesStreamingActif();
		Vector2I centre = Gestionnaire_Monde.WorldToChunkCoord(posMonde, TailleChunk);
		ConstantesProfondeurVerticale.RemplirFenetreCoordYAutourJoueur(posMonde.Y, ProfondeurMaxMetres, _coordYActifsProfondeurTravail, demiY);
		for (int dx = -rayonXZ; dx <= rayonXZ; dx++)
		{
			for (int dz = -rayonXZ; dz <= rayonXZ; dz++)
			{
				Vector2I cc = new Vector2I(centre.X + dx, centre.Y + dz);
				foreach (int coordY in _coordYActifsProfondeurTravail)
					DemanderChunkCouche(cc, coordY, urgent: true);
			}
		}
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
		int rayonMax = _dimensionReseauActive == (int)DimensionJeu.Abysse ? 4 : 3;
		int rayon = Mathf.Clamp(rayonChunks, 0, rayonMax);
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pointMonde, TailleChunk);
		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I cc = new Vector2I(c.X + dx, c.Y + dz);
				if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
				{
					_coordYActifsAbysseTravail.Clear();
					_coordYActifsAbysseTravail.Add(CoordYStageAbysseDepuisYMonde(pointMonde.Y));
					foreach (int y in _coordYActifsAbysseTravail)
					{
						if (!_chunksDataAbysse3D.TryGetValue(new Vector3I(cc.X, y, cc.Y), out var data) || data == null)
							continue;
						if (data.EstVideIntegral)
						{
							data.EstEnFileSolidification = false;
							continue;
						}
						if (data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification) continue;
						if (data.EstEnFileSolidification)
							RetirerDeFileSolidification(data);
						else
							data.EstEnFileSolidification = true;
						EnfilerSolidificationUrgenteUnique(data);
					}
				}
				else if (ModeProfondeurTranchesActif())
				{
					ConstantesProfondeurVerticale.RemplirFenetreCoordYAutourJoueur(pointMonde.Y, ProfondeurMaxMetres, _coordYActifsProfondeurTravail, DemiFenetreTranchesStreamingActif());
					foreach (int coordY in _coordYActifsProfondeurTravail)
					{
						if (!TryGetChunkDataPourCoordY(cc, coordY, out var data) || data == null)
							continue;
						if (data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification) continue;
						if (data.EstEnFileSolidification)
							RetirerDeFileSolidification(data);
						else
							data.EstEnFileSolidification = true;
						EnfilerSolidificationUrgenteUnique(data);
					}
				}
				else
				{
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
	}

	/// <summary>Retire la collision sans détruire le mesh (remesh minage / couture verticale).</summary>
	private static void LibererPhysiqueChunk(ChunkData data)
	{
		if (data == null) return;
		if (data.PhysicsBodyRID.IsValid)
		{
			PhysicsServer3D.Singleton.BodyRemoveShape(data.PhysicsBodyRID, 0);
			PhysicsServer3D.Singleton.FreeRid(data.PhysicsBodyRID);
			data.PhysicsBodyRID = default;
		}
		if (data._shapeRef != null)
		{
			data._shapeRef.Dispose();
			data._shapeRef = null;
		}
		data.PhysicsShapeRID = default;
		data.EstEnFileSolidification = false;
	}
}
