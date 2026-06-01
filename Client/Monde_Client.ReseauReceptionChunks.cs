using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	public void AppliquerDestructionGlobale(Vector3 pointImpact, float rayon, float forceDegats = 5.0f)
	{
		_demanderDestruction?.Invoke(pointImpact, rayon, forceDegats);
	}

	public void AppliquerCreationGlobale(Vector3 pointImpact, Vector3 normale, float rayon, int idMatiere = 1)
	{
		_demanderCreation?.Invoke(pointImpact, normale, rayon, idMatiere);
	}

	/// <summary>Mise à jour flore : le serveur a purgé du gazon (minage, gravité, fauchage). On met à jour l'inventaire et le rendu gazon pour que les brins disparaissent.</summary>
	public void RecevoirFloreModifie(Vector2I coordChunk, int coordChunkY, Dictionary<Vector3I, byte> inventaireFlore)
	{
		RecevoirFloreModifieAvecRetry(coordChunk, coordChunkY, inventaireFlore, 0);
	}

	private void RecevoirFloreModifieAvecRetry(Vector2I coordChunk, int coordChunkY, Dictionary<Vector3I, byte> inventaireFlore, int tentative)
	{
		ChunkData data = null;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			if (!TryGetChunkDataPourCoordY(coordChunk, coordChunkY, out data))
				data = null;
		}
		else if (!_chunksData.TryGetValue(coordChunk, out data))
			data = null;

		if (data == null)
		{
			if (tentative < 12)
				Callable.From(() => RecevoirFloreModifieAvecRetry(coordChunk, coordChunkY, inventaireFlore, tentative + 1)).CallDeferred();
			return;
		}

		data.InventaireFlore = inventaireFlore ?? new Dictionary<Vector3I, byte>();
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			_chunksData[coordChunk] = data;
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
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			bool trouve = false;
			foreach (var kv in _chunksDataAbysse3D)
			{
				if (kv.Key.X != coordChunk.X || kv.Key.Z != coordChunk.Y)
					continue;
				trouve = true;
				foreach (int sec in sectionsAffectees)
				{
					if (sec >= 0 && sec < 45)
						_sectionsAReconstruire.Add((coordChunk.X, kv.Key.Y, coordChunk.Y, sec));
				}
			}
			if (!trouve)
				return;
			return;
		}
		if (!_chunksData.TryGetValue(coordChunk, out _)) return;
		foreach (int sec in sectionsAffectees)
			if (sec >= 0 && sec < 45) _sectionsAReconstruire.Add((coordChunk.X, 0, coordChunk.Y, sec));
	}

	/// <summary>Micro-RPC : mise à jour voxel unique. Modifie le chunk principal ET la réplique sur le padding des voisins.</summary>
	public void AppliquerVoxel(Vector3I posGlobal, byte id)
	{
		_modificationEnCours = true;
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int localX, out int localZ);
		int cx = c.X;
		int cz = c.Y;
		int coordY = CoordYDepuisMondeY(posGlobal.Y);
		int localVoxelY = LocalYDepuisMondeY(posGlobal.Y);
		int sec = Mathf.FloorToInt(localVoxelY / 16f);
		int localYSection = localVoxelY - sec * 16;

		if (!TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY, out var data)) return;
		data.SetVoxelLocal(localX, localVoxelY, localZ, id);

		// Côté client, même règle que serveur: ne répliquer que les frontières partagées (local == 0).
		if (localX == 0 && TryGetChunkDataPourCoordY(new Vector2I(cx - 1, cz), coordY, out var vx))
		{
			vx.SetVoxelLocal(TailleChunk, localVoxelY, localZ, id);
			_sectionsAReconstruire.Add((cx - 1, vx.CoordChunkY, cz, sec));
		}
		if (localZ == 0 && TryGetChunkDataPourCoordY(new Vector2I(cx, cz - 1), coordY, out var vz))
		{
			vz.SetVoxelLocal(localX, localVoxelY, TailleChunk, id);
			_sectionsAReconstruire.Add((cx, vz.CoordChunkY, cz - 1, sec));
		}
		if (localX == 0 && localZ == 0 && TryGetChunkDataPourCoordY(new Vector2I(cx - 1, cz - 1), coordY, out var vxz))
		{
			vxz.SetVoxelLocal(TailleChunk, localVoxelY, TailleChunk, id);
			_sectionsAReconstruire.Add((cx - 1, vxz.CoordChunkY, cz - 1, sec));
		}

		void MarquerSectionsChunkSiPresent(int chunkX, int chunkZ)
		{
			if (!TryGetChunkDataPourCoordY(new Vector2I(chunkX, chunkZ), coordY, out var chunkCible))
				return;
			if (sec >= 0 && sec < 45)
				_sectionsAReconstruire.Add((chunkX, chunkCible.CoordChunkY, chunkZ, sec));
			if (localYSection == 0 && localVoxelY > 0 && sec - 1 >= 0)
				_sectionsAReconstruire.Add((chunkX, chunkCible.CoordChunkY, chunkZ, sec - 1));
			if (localYSection == 15 && sec + 1 < 45)
				_sectionsAReconstruire.Add((chunkX, chunkCible.CoordChunkY, chunkZ, sec + 1));
		}

		MarquerSectionsChunkSiPresent(cx, cz);
		if (localX == 0) MarquerSectionsChunkSiPresent(cx - 1, cz);
		if (localZ == 0) MarquerSectionsChunkSiPresent(cx, cz - 1);
		if (localX == 0 && localZ == 0) MarquerSectionsChunkSiPresent(cx - 1, cz - 1);
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
		bool modeAbysse = _dimensionReseauActive == (int)DimensionJeu.Abysse;
		if (modeAbysse && _chunksDataAbysse3D.Count > 0)
		{
			_clesChunksAbysseARetirerTemp.Clear();
			foreach (var kv in _chunksDataAbysse3D)
			{
				if (kv.Key.X == coord.X && kv.Key.Z == coord.Y)
					_clesChunksAbysseARetirerTemp.Add(kv.Key);
			}
			for (int i = 0; i < _clesChunksAbysseARetirerTemp.Count; i++)
			{
				Vector3I cle = _clesChunksAbysseARetirerTemp[i];
				if (_chunksDataAbysse3D.TryGetValue(cle, out var dataAbysse) && dataAbysse != null)
					RetirerChunkDataAbysse(cle, dataAbysse);
			}
		}
		if (_chunksData.TryGetValue(coord, out var data))
		{
			if (modeAbysse)
			{
				// En Abysse, la destruction est pilotée par RetirerChunkDataAbysse (source de vérité 3D).
				// Ici on purge seulement un éventuel résidu 2D sans relibérer un RID.
				if (TrouverCoucheAbysseColonne(coord) == null)
					_chunksData.Remove(coord);
			}
			else
			{
				_chunksData.Remove(coord);
				data.LibérerRids();
			}
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
	public void RecevoirChunkDuServeurRPC(int coordX, int coordY, int coordZ, int tailleChunk, int hauteurMax, byte[] densitiesPlates, byte[] materialsFlat, byte[] densitiesEauPlates, bool estVideIntegral = false)
	{
		var donnees = new DonneesChunk
		{
			CoordChunk = new Vector2I(coordX, coordZ),
			CoordChunkY = coordY,
			TailleChunk = tailleChunk,
			HauteurMax = hauteurMax,
			DensitiesQuantifiees = densitiesPlates,
			DensitiesEauQuantifiees = densitiesEauPlates,
			MaterialsFlat = materialsFlat,
			EstVideIntegral = estVideIntegral
		};
		RecevoirDonneesChunk(new Vector2I(coordX, coordZ), donnees);
	}

	public void RecevoirDonneesChunk(Vector2I coordChunk, DonneesChunk donnees)
	{
		int coordY = donnees?.CoordChunkY ?? 0;
		bool modeAbysse = _dimensionReseauActive == (int)DimensionJeu.Abysse;
		int coordYCle = modeAbysse ? NormaliserCoordYAbysse(coordY) : coordY;
		Vector3I cle3D = new Vector3I(coordChunk.X, coordY, coordChunk.Y);
		Vector3I cle3DNormalisee = new Vector3I(coordChunk.X, coordYCle, coordChunk.Y);
		if (modeAbysse)
		{
			_demandesAbysseFrameDerniereEmission.Remove(cle3D);
			_demandesAbysseFrameDerniereEmission.Remove(cle3DNormalisee);
		}
		// Architecture AAA : ChunkData (RID) uniquement, plus de Node.
		ulong empreinte = CalculerEmpreinteDonneesChunk(donnees);
		if (modeAbysse && _chunksDataAbysse3D.TryGetValue(cle3DNormalisee, out var existingAbysse))
		{
			existingAbysse.CoordChunkY = coordYCle;
			existingAbysse.EstVideIntegral = donnees?.EstVideIntegral ?? false;
			if (existingAbysse.VisualInstanceRID.IsValid && existingAbysse.EmpreinteDonneesServeur == empreinte && empreinte != 0)
			{
				_chunksData[coordChunk] = existingAbysse;
				return;
			}
			EnqueueChunkGeneration(existingAbysse, donnees);
			_chunksData[coordChunk] = existingAbysse;
			return;
		}
		if (!modeAbysse && _chunksData.TryGetValue(coordChunk, out var existing))
		{
			// Profondeur étendue : si la couche verticale change (descente/remontée), on repart propre.
			// Sans libérer les RIDs, le mesh/la collision resteraient figés à l'altitude de l'ancienne couche.
			if (existing.CoordChunkY != coordY)
			{
				existing.LibérerRids();
				existing.EmpreinteDonneesServeur = 0;
				existing.CoordChunkY = coordY;
				EnqueueChunkGeneration(existing, donnees);
				return;
			}
			existing.CoordChunkY = coordY;
			if (existing.VisualInstanceRID.IsValid && existing.EmpreinteDonneesServeur == empreinte && empreinte != 0)
				return;
			EnqueueChunkGeneration(existing, donnees);
			return;
		}

		var data = new ChunkData
		{
			Coordonnees = coordChunk,
			CoordChunkY = coordYCle,
			TailleChunk = TailleChunk,
			HauteurMax = HauteurMax,
			EstVideIntegral = donnees?.EstVideIntegral ?? false
		};
		data.ConfigurerBruitClimat(_seedTerrain);
		_chunksData[coordChunk] = data;
		if (modeAbysse)
			_chunksDataAbysse3D[cle3DNormalisee] = data;
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
}
