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

	public void RecevoirChunkModifie(Vector2I coordChunk, int coordChunkY, List<int> sectionsAffectees)
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
					int nbSecAbysse = Chunk_Client.ObtenirNbSectionsEffectif(HauteurMax);
					if (sec >= 0 && sec < nbSecAbysse)
						_sectionsAReconstruire.Add((coordChunk.X, kv.Key.Y, coordChunk.Y, sec));
				}
			}
			if (!trouve)
				return;
			return;
		}
		int nbSec = HauteurMax;
		if (TryGetChunkDataPourCoordY(coordChunk, coordChunkY, out var dataChunk))
			nbSec = dataChunk.HauteurMax;
		int nbSecMax = Chunk_Client.ObtenirNbSectionsEffectif(nbSec);
		foreach (int sec in sectionsAffectees)
		{
			if (sec < 0 || sec >= nbSecMax) continue;
			_sectionsAReconstruire.Add((coordChunk.X, coordChunkY, coordChunk.Y, sec));
		}
	}

	private void MarquerSectionsChunkSiPresent(int chunkX, int chunkZ, int coordY, int sec, int localYSection, int localVoxelY, int nbSec)
	{
		if (!TryGetChunkDataPourCoordY(new Vector2I(chunkX, chunkZ), coordY, out var chunkCible))
			return;
		if (sec >= 0 && sec < nbSec)
			_sectionsAReconstruire.Add((chunkX, chunkCible.CoordChunkY, chunkZ, sec));
		if (localYSection == 0 && localVoxelY > 0 && sec - 1 >= 0)
			_sectionsAReconstruire.Add((chunkX, chunkCible.CoordChunkY, chunkZ, sec - 1));
		if (localYSection == 15 && sec + 1 < nbSec)
			_sectionsAReconstruire.Add((chunkX, chunkCible.CoordChunkY, chunkZ, sec + 1));
	}

	/// <summary>Micro-RPC : mise à jour voxel unique. Modifie le chunk principal ET la réplique sur le padding des voisins.</summary>
	public void AppliquerVoxel(Vector3I posGlobal, byte id)
	{
		_modificationEnCours = true;
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out _, out _);
		int coordY = CoordYDepuisMondeY(posGlobal.Y);
		if (!TryGetChunkDataPourCoordY(c, coordY, out _))
		{
			if (_voxelsModifiesEnAttente.Count < 8192)
				_voxelsModifiesEnAttente[posGlobal] = id;
			if (_dimensionReseauActive != (int)DimensionJeu.Abysse)
				DemanderChunkCouche(c, coordY, urgent: true);
			return;
		}
		AppliquerVoxelSurChunkCharge(posGlobal, id);
	}

	private void AppliquerVoxelSurChunkCharge(Vector3I posGlobal, byte id)
	{
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int localX, out int localZ);
		int cx = c.X;
		int cz = c.Y;
		int coordY = CoordYDepuisMondeY(posGlobal.Y);
		int localVoxelY = LocalYDepuisMondeY(posGlobal.Y);
		int sec = Mathf.FloorToInt(localVoxelY / 16f);
		int localYSection = localVoxelY - sec * 16;

		if (!TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY, out var data))
			return;
		int nbSec = Chunk_Client.ObtenirNbSectionsEffectif(data.HauteurMax);
		data.SetVoxelLocal(localX, localVoxelY, localZ, id);
		RepliquerPaddingCompletMinage(cx, cz, coordY, localX, localZ, localVoxelY, data.HauteurMax, id, data, sec, nbSec);

		void MarquerSectionsChunk(int chunkX, int chunkZ)
			=> MarquerSectionsChunkSiPresent(chunkX, chunkZ, coordY, sec, localYSection, localVoxelY, nbSec);

		MarquerSectionsChunk(cx, cz);
		if (localX == 0) MarquerSectionsChunk(cx - 1, cz);
		if (localZ == 0) MarquerSectionsChunk(cx, cz - 1);
		if (localX == 0 && localZ == 0) MarquerSectionsChunk(cx - 1, cz - 1);
		if (localX >= TailleChunk - 1) MarquerSectionsChunk(cx + 1, cz);
		if (localZ >= TailleChunk - 1) MarquerSectionsChunk(cx, cz + 1);
		if (localX >= TailleChunk - 1 && localZ >= TailleChunk - 1) MarquerSectionsChunk(cx + 1, cz + 1);
		if (localX == 0 && localZ >= TailleChunk - 1) MarquerSectionsChunk(cx - 1, cz + 1);
		if (localX >= TailleChunk - 1 && localZ == 0) MarquerSectionsChunk(cx + 1, cz - 1);

		if (_dimensionReseauActive != (int)DimensionJeu.Abysse)
		{
			MarquerSectionsJonctionTrancheVerticale(cx, cz, coordY, localVoxelY, data.HauteurMax);
			EnsurerTranchesVoisinesChargeesPourMinage(cx, cz, coordY, localVoxelY, data.HauteurMax);
			EnsurerTranchesVoisinesJonctionPourMinage(cx, cz, coordY, localX, localZ, localVoxelY, data.HauteurMax);
			EnsurerChunksVoisinsHorizontauxChargeesPourMinage(cx, cz, coordY, localX, localZ);
		}
		TenterRemeshMinageImmediat(cx, cz, coordY, localVoxelY, sec, nbSec, data.HauteurMax);
	}

	/// <summary>Remesh synchrone près du joueur : trou visible immédiatement (budget limité / frame).</summary>
	private void TenterRemeshMinageImmediat(int cx, int cz, int coordY, int localY, int secPrincipale, int nbSec, int hauteurTranche)
	{
		if (_remeshMinageSyncRestantFrame <= 0)
			return;
		RemeshMinageImmediatChunk(new Vector3I(cx, coordY, cz), secPrincipale, nbSec);
		if (ModeProfondeurTranchesActif() && hauteurTranche > 0)
		{
			int marge = ConstantesProfondeurVerticale.MargePaddingMinageVoxels;
			if (localY >= hauteurTranche - marge || localY == hauteurTranche - 1)
			{
				int nbSecHaut = nbSec;
				if (TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY + 1, out var sur))
					nbSecHaut = Chunk_Client.ObtenirNbSectionsEffectif(sur.HauteurMax);
				RemeshMinageImmediatChunk(new Vector3I(cx, coordY + 1, cz), 0, nbSecHaut);
				if (nbSecHaut > 1)
					RemeshMinageImmediatChunk(new Vector3I(cx, coordY + 1, cz), 1, nbSecHaut);
			}
			if ((localY <= marge || localY == 0)
				&& coordY > ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres))
			{
				int nbSecBas = nbSec;
				if (TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY - 1, out var sous))
					nbSecBas = Chunk_Client.ObtenirNbSectionsEffectif(sous.HauteurMax);
				if (nbSecBas > 0)
					RemeshMinageImmediatChunk(new Vector3I(cx, coordY - 1, cz), nbSecBas - 1, nbSecBas);
				if (nbSecBas > 1)
					RemeshMinageImmediatChunk(new Vector3I(cx, coordY - 1, cz), nbSecBas - 2, nbSecBas);
			}
			RemeshVoisinsCardinauxJonction(cx, cz, coordY, localY, hauteurTranche, nbSec);
		}
	}

	private static readonly int[][] _cardinauxJonction = { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } };

	private void RemeshVoisinsCardinauxJonction(int cx, int cz, int coordY, int localY, int hauteurTranche, int nbSec)
	{
		if (localY != 0 && localY != hauteurTranche - 1)
			return;
		foreach (var d in _cardinauxJonction)
		{
			int ncx = cx + d[0];
			int ncz = cz + d[1];
			if (localY == 0)
			{
				RemeshMinageImmediatChunk(new Vector3I(ncx, coordY, ncz), 0, nbSec);
				int cyMin = ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres);
				if (coordY > cyMin && TryGetChunkDataPourCoordY(new Vector2I(ncx, ncz), coordY - 1, out var sous))
				{
					int nbBas = Chunk_Client.ObtenirNbSectionsEffectif(sous.HauteurMax);
					if (nbBas > 0)
						RemeshMinageImmediatChunk(new Vector3I(ncx, coordY - 1, ncz), nbBas - 1, nbBas);
				}
			}
			if (localY == hauteurTranche - 1)
			{
				if (nbSec > 0)
					RemeshMinageImmediatChunk(new Vector3I(ncx, coordY, ncz), nbSec - 1, nbSec);
				if (TryGetChunkDataPourCoordY(new Vector2I(ncx, ncz), coordY + 1, out var sur))
				{
					int nbHaut = Chunk_Client.ObtenirNbSectionsEffectif(sur.HauteurMax);
					if (nbHaut > 0)
						RemeshMinageImmediatChunk(new Vector3I(ncx, coordY + 1, ncz), 0, nbHaut);
				}
			}
		}
	}

	private void RemeshMinageImmediatChunk(Vector3I cleChunk, int secPrincipale, int nbSec)
	{
		if (_mondeClientSortieEnCours || !IsInsideTree() || _remeshMinageSyncRestantFrame <= 0 || !EstRemeshPrioritaireMinage(cleChunk))
			return;
		if (!TryGetChunkDataPourCoordY(new Vector2I(cleChunk.X, cleChunk.Z), cleChunk.Y, out var data)
			|| data?.DensitiesFlat == null || !data.VisualInstanceRID.IsValid)
			return;
		var batch = new HashSet<int> { secPrincipale };
		if (secPrincipale > 0)
			batch.Add(secPrincipale - 1);
		if (secPrincipale + 1 < nbSec)
			batch.Add(secPrincipale + 1);
		int cout = batch.Count;
		if (cout > _remeshMinageSyncRestantFrame)
			cout = _remeshMinageSyncRestantFrame;
		if (cout <= 0)
			return;
		var batchLimite = new HashSet<int>();
		foreach (int s in batch)
		{
			if (batchLimite.Count >= cout)
				break;
			batchLimite.Add(s);
		}
		ExecuterReconstructionPrioritaire(cleChunk, batchLimite);
		_remeshMinageSyncRestantFrame -= batchLimite.Count;
		foreach (int s in batchLimite)
			_sectionsAReconstruire.Remove((cleChunk.X, cleChunk.Y, cleChunk.Z, s));
	}

	/// <summary>Rejoue les RPC voxel reçus pendant un chunk encore en chargement.</summary>
	private void AppliquerVoxelsEnAttente()
	{
		if (_voxelsModifiesEnAttente.Count == 0)
			return;
		_voxelsEnAttenteBuffer.Clear();
		foreach (var kv in _voxelsModifiesEnAttente)
			_voxelsEnAttenteBuffer.Add(kv.Key);
		Vector3 posJoueur = Vector3.Zero;
		bool triParDistance = EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef);
		if (triParDistance)
			posJoueur = joueurRef.GlobalPosition;
		if (triParDistance)
		{
			_voxelsEnAttenteBuffer.Sort((a, b) =>
			{
				float da = new Vector3(a.X + 0.5f, a.Y + 0.5f, a.Z + 0.5f).DistanceSquaredTo(posJoueur);
				float db = new Vector3(b.X + 0.5f, b.Y + 0.5f, b.Z + 0.5f).DistanceSquaredTo(posJoueur);
				return da.CompareTo(db);
			});
		}
		int budget = 96;
		if (_modificationEnCours)
			budget = 128;
		else if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 45f)
			budget = 32;
		else if (ModeSurvieFpsAgressif && _fpsMoyenneAuto < 55f)
			budget = 48;
		else if (_sectionsAReconstruire.Count > 20)
			budget = 24;
		for (int i = 0; i < _voxelsEnAttenteBuffer.Count && budget > 0; i++)
		{
			Vector3I pos = _voxelsEnAttenteBuffer[i];
			if (!_voxelsModifiesEnAttente.TryGetValue(pos, out byte id))
				continue;
			Gestionnaire_Monde.WorldToChunkAndLocal(pos.X, pos.Z, TailleChunk, out Vector2I c, out _, out _);
			int coordY = CoordYDepuisMondeY(pos.Y);
			if (!TryGetChunkDataPourCoordY(c, coordY, out _))
				continue;
			_voxelsModifiesEnAttente.Remove(pos);
			AppliquerVoxelSurChunkCharge(pos, id);
			budget--;
		}
	}

	/// <summary>Jonction Y=0, ±100… : demander la tranche voisine avant de miner si elle manque encore en RAM.</summary>
	private void EnsurerTranchesVoisinesChargeesPourMinage(int chunkX, int chunkZ, int coordY, int localY, int hauteurTranche)
	{
		if (!ModeProfondeurTranchesActif() || hauteurTranche <= 0)
			return;
		int marge = ConstantesProfondeurVerticale.MargePaddingMinageVoxels;
		int cyMin = ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres);
		bool presBas = localY <= marge || localY == 0;
		bool presHaut = localY >= hauteurTranche - marge || localY == hauteurTranche - 1;
		if (!presBas && !presHaut)
			return;
		var coord = new Vector2I(chunkX, chunkZ);
		if (presBas && coordY > cyMin)
		{
			if (!TryGetChunkDataPourCoordY(coord, coordY - 1, out _))
				DemanderChunkCouche(coord, coordY - 1, urgent: true);
		}
		if (presHaut)
		{
			if (!TryGetChunkDataPourCoordY(coord, coordY + 1, out _))
				DemanderChunkCouche(coord, coordY + 1, urgent: true);
		}
	}

	private void EnsurerTranchesVoisinesJonctionPourMinage(int chunkX, int chunkZ, int coordY, int localX, int localZ, int localY, int hauteurTranche)
	{
		if (!ModeProfondeurTranchesActif() || hauteurTranche <= 0)
			return;
		int cyMin = ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres);
		if (localY != 0 && localY != hauteurTranche - 1)
			return;
		const int margeXZ = 2;
		void DemanderSlice(int cx, int cz, int cy)
		{
			if (!TryGetChunkDataPourCoordY(new Vector2I(cx, cz), cy, out _))
				DemanderChunkCouche(new Vector2I(cx, cz), cy, urgent: true);
		}
		if (localY == 0 && coordY > cyMin)
		{
			DemanderSlice(chunkX, chunkZ, coordY - 1);
			if (localX <= margeXZ) DemanderSlice(chunkX - 1, chunkZ, coordY - 1);
			if (localX >= TailleChunk - margeXZ) DemanderSlice(chunkX + 1, chunkZ, coordY - 1);
			if (localZ <= margeXZ) DemanderSlice(chunkX, chunkZ - 1, coordY - 1);
			if (localZ >= TailleChunk - margeXZ) DemanderSlice(chunkX, chunkZ + 1, coordY - 1);
		}
		if (localY == hauteurTranche - 1)
		{
			DemanderSlice(chunkX, chunkZ, coordY + 1);
			if (localX <= margeXZ) DemanderSlice(chunkX - 1, chunkZ, coordY + 1);
			if (localX >= TailleChunk - margeXZ) DemanderSlice(chunkX + 1, chunkZ, coordY + 1);
			if (localZ <= margeXZ) DemanderSlice(chunkX, chunkZ - 1, coordY + 1);
			if (localZ >= TailleChunk - margeXZ) DemanderSlice(chunkX, chunkZ + 1, coordY + 1);
		}
	}

	/// <summary>Près d'une jonction Y=±100 : charge les tranches voisines et recoud le voile si les deux tranches sont en RAM.</summary>
	internal void MaintenirJonctionsTranchesAutourJoueur(Vector3 posJoueur, float dt)
	{
		if (!ModeProfondeurTranchesActif() || _networkManager == null)
			return;
		if (!ConstantesProfondeurVerticale.EstProcheJonctionTrancheMonde(posJoueur.Y))
			return;

		int ly = ConstantesProfondeurVerticale.LocalYDepuisMondeY((int)Mathf.Floor(posJoueur.Y));
		int h = ConstantesProfondeurVerticale.HauteurTrancheMetres;
		int coordY = CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y));
		int marge = ConstantesProfondeurVerticale.MargeJonctionTrancheVoxels;
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur + 1, 2, 3);
		ulong frame = Engine.GetPhysicsFrames();
		bool entretienVoile = frame % 120 == 0;

		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I cc = new Vector2I(c.X + dx, c.Y + dz);
				if (ly <= marge && coordY > ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres))
				{
					if (!TryGetChunkDataPourCoordY(cc, coordY - 1, out _))
						DemanderChunkCouche(cc, coordY - 1, urgent: true);
					else if (entretienVoile && TryGetChunkDataPourCoordY(cc, coordY, out var courant)
						&& TryGetChunkDataPourCoordY(cc, coordY - 1, out var dessous)
						&& courant?.VisualInstanceRID.IsValid == true
						&& dessous?.VisualInstanceRID.IsValid == true)
					{
						bool urgent = EstChunkProfondeurProcheJoueur(courant) || EstChunkProfondeurProcheJoueur(dessous);
						MarquerSectionsBordTranchePourRemesh(courant, dessous, immediate: urgent);
					}
				}
				if (ly >= h - marge)
				{
					if (!TryGetChunkDataPourCoordY(cc, coordY + 1, out _))
						DemanderChunkCouche(cc, coordY + 1, urgent: true);
					else if (entretienVoile && TryGetChunkDataPourCoordY(cc, coordY, out var courant)
						&& TryGetChunkDataPourCoordY(cc, coordY + 1, out var dessus)
						&& courant?.VisualInstanceRID.IsValid == true
						&& dessus?.VisualInstanceRID.IsValid == true)
					{
						bool urgent = EstChunkProfondeurProcheJoueur(courant) || EstChunkProfondeurProcheJoueur(dessus);
						MarquerSectionsBordTranchePourRemesh(courant, dessus, immediate: urgent);
					}
				}
			}
		}
	}

	private void EnsurerChunksVoisinsHorizontauxChargeesPourMinage(int chunkX, int chunkZ, int coordY, int localX, int localZ)
	{
		const int marge = 2;
		int last = TailleChunk - 1;
		if (localX > marge && localZ > marge && localX < last - marge && localZ < last - marge)
			return;
		void DemanderSiAbsent(int cx, int cz)
		{
			if (!TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY, out _))
				DemanderChunkCouche(new Vector2I(cx, cz), coordY, urgent: true);
		}
		if (localX <= marge)
		{
			DemanderSiAbsent(chunkX - 1, chunkZ);
			if (localZ <= marge) DemanderSiAbsent(chunkX - 1, chunkZ - 1);
			if (localZ >= last - marge) DemanderSiAbsent(chunkX - 1, chunkZ + 1);
		}
		if (localX >= last - marge)
		{
			DemanderSiAbsent(chunkX + 1, chunkZ);
			if (localZ <= marge) DemanderSiAbsent(chunkX + 1, chunkZ - 1);
			if (localZ >= last - marge) DemanderSiAbsent(chunkX + 1, chunkZ + 1);
		}
		if (localZ <= marge)
			DemanderSiAbsent(chunkX, chunkZ - 1);
		if (localZ >= last - marge)
			DemanderSiAbsent(chunkX, chunkZ + 1);
	}

	/// <summary>Après intégration d'une tranche : remesh bas/haut du voisin déjà affiché (voile déchiré à la jonction).</summary>
	internal void RecoudreVoisinsVerticalApresIntegration(ChunkData chunk)
	{
		if (chunk == null || !ModeProfondeurTranchesActif())
			return;
		Vector2I coord = chunk.Coordonnees;
		int cy = chunk.CoordChunkY;
		if (TryGetChunkDataPourCoordY(coord, cy - 1, out var dessous) && dessous?.VisualInstanceRID.IsValid == true)
		{
			bool urgent = EstChunkProfondeurProcheJoueur(chunk) || EstChunkProfondeurProcheJoueur(dessous);
			MarquerSectionsBordTranchePourRemesh(chunk, dessous, immediate: urgent);
		}
		if (TryGetChunkDataPourCoordY(coord, cy + 1, out var dessus) && dessus?.VisualInstanceRID.IsValid == true)
		{
			bool urgent = EstChunkProfondeurProcheJoueur(chunk) || EstChunkProfondeurProcheJoueur(dessus);
			MarquerSectionsBordTranchePourRemesh(chunk, dessus, immediate: urgent);
		}
	}

	private readonly Dictionary<Vector3I, ulong> _frameDernierRemeshBordVert = new Dictionary<Vector3I, ulong>();

	/// <summary>Une section bord (bas ou haut) max, avec anti-spam remesh (évite lag en cascade).</summary>
	private void MarquerSectionBordVerticaleLegere(ChunkData chunk, bool sectionBasse)
	{
		if (chunk == null || !chunk.VisualInstanceRID.IsValid)
			return;
		Vector3I cle = new Vector3I(chunk.Coordonnees.X, chunk.CoordChunkY, chunk.Coordonnees.Y);
		ulong frame = Engine.GetPhysicsFrames();
		if (_frameDernierRemeshBordVert.TryGetValue(cle, out ulong derniere) && frame - derniere < 12)
			return;
		_frameDernierRemeshBordVert[cle] = frame;
		int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(chunk.HauteurMax);
		if (nbSec <= 0) return;
		int s = sectionBasse ? 0 : nbSec - 1;
		_sectionsAReconstruire.Add((chunk.Coordonnees.X, chunk.CoordChunkY, chunk.Coordonnees.Y, s));
	}

	/// <summary>Remesh léger bas/haut après sync voxel (2 sections max par tranche).</summary>
	private void MarquerSectionsBordTranchePourRemesh(ChunkData chunk, ChunkData voisin, bool immediate = false)
	{
		void Marquer(ChunkData c)
		{
			if (c == null)
				return;
			int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(c.HauteurMax);
			if (nbSec <= 0)
				return;
			if (immediate && c.VisualInstanceRID.IsValid)
			{
				AjouterRemeshJonctionVertical((c.Coordonnees.X, c.CoordChunkY, c.Coordonnees.Y, 0));
				AjouterRemeshJonctionVertical((c.Coordonnees.X, c.CoordChunkY, c.Coordonnees.Y, nbSec - 1));
				return;
			}
			MarquerSectionBordVerticaleLegere(c, sectionBasse: true);
			MarquerSectionBordVerticaleLegere(c, sectionBasse: false);
		}
		Marquer(chunk);
		Marquer(voisin);
	}

	/// <summary>Couture verticale au minage : sections bas/haut uniquement si la modification touche la frontière Y.</summary>
	private void MarquerSectionsJonctionTrancheVerticale(int chunkX, int chunkZ, int coordY, int localY, int hauteurTranche)
	{
		if (!ModeProfondeurTranchesActif() || hauteurTranche <= 0 || localY < 0)
			return;
		int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(hauteurTranche);
		int marge = ConstantesProfondeurVerticale.MargePaddingMinageVoxels;
		bool presBas = localY <= marge;
		bool presHaut = localY >= hauteurTranche - marge;
		if (presBas)
		{
			if (TryGetChunkDataPourCoordY(new Vector2I(chunkX, chunkZ), coordY, out var cur))
			{
				for (int s = 0; s <= 1 && s < nbSec; s++)
					_sectionsAReconstruire.Add((chunkX, cur.CoordChunkY, chunkZ, s));
			}
			if (TryGetChunkDataPourCoordY(new Vector2I(chunkX, chunkZ), coordY - 1, out var sous))
			{
				for (int s = nbSec - 2; s < nbSec; s++)
					if (s >= 0)
						_sectionsAReconstruire.Add((chunkX, sous.CoordChunkY, chunkZ, s));
			}
		}
		if (presHaut)
		{
			if (TryGetChunkDataPourCoordY(new Vector2I(chunkX, chunkZ), coordY, out var cur))
			{
				for (int s = nbSec - 2; s < nbSec; s++)
					if (s >= 0)
						_sectionsAReconstruire.Add((chunkX, cur.CoordChunkY, chunkZ, s));
			}
			if (TryGetChunkDataPourCoordY(new Vector2I(chunkX, chunkZ), coordY + 1, out var sur))
			{
				for (int s = 0; s <= 1 && s < nbSec; s++)
					_sectionsAReconstruire.Add((chunkX, sur.CoordChunkY, chunkZ, s));
			}
		}
	}

	/// <summary>
	/// NE PAS écrire les colonnes/faces fantômes locales (indice tc / h) au minage interne.
	/// L'indice tc d'un chunk est le voxel 0 du voisin (recouvrement partagé) : le punir quand on mine
	/// localX/Z=15 ou ly=h-1 crée une déchirure d'1 voxel à la frontière (côté A vide, côté B plein).
	/// La colonne fantôme n'est mise à jour QUE par la réplication réelle du voisin (localX/Z==0) ou la
	/// couture verticale (échantillonneur). Conservé en no-op pour ne pas toucher les sites d'appel.
	/// </summary>
	private static void MettreAJourPaddingLocalChunk(ChunkData chunk, int lx, int ly, int lz, byte id, int tc, int h)
	{
	}

	/// <summary>Réplique padding XZ + tranche Y (Y=0, ±100…) y compris coins chunk×tranche — aligné serveur.</summary>
	private void RepliquerPaddingCompletMinage(int cx, int cz, int coordY, int localX, int localZ, int localY, int h,
		byte id, ChunkData chunkCourant, int sec, int nbSec)
	{
		int tc = TailleChunk;
		int last = tc - 1;

		void SetVoxel(int ncx, int ncz, int ncy, int lx, int ly, int lz, bool remeshBas = false, bool remeshHaut = false)
		{
			if (!TryGetChunkDataPourCoordY(new Vector2I(ncx, ncz), ncy, out var c) || c?.DensitiesFlat == null)
				return;
			c.SetVoxelLocal(lx, ly, lz, id);
			MettreAJourPaddingLocalChunk(c, lx, ly, lz, id, tc, c.HauteurMax);
			if (remeshBas)
				AjouterRemeshJonctionVertical((ncx, ncy, ncz, 0));
			if (remeshHaut)
			{
				int nb = ConstantesProfondeurVerticale.ObtenirNbSections(c.HauteurMax);
				if (nb > 0)
					AjouterRemeshJonctionVertical((ncx, ncy, ncz, nb - 1));
			}
			if (ncy == coordY && (ncx != cx || ncz != cz))
				_sectionsAReconstruire.Add((ncx, ncy, ncz, sec));
		}

		MettreAJourPaddingLocalChunk(chunkCourant, localX, localY, localZ, id, tc, h);

		// Ouest / nord (local == 0)
		if (localX == 0)
			SetVoxel(cx - 1, cz, coordY, tc, localY, localZ);
		if (localZ == 0)
			SetVoxel(cx, cz - 1, coordY, localX, localY, tc);
		if (localX == 0 && localZ == 0)
			SetVoxel(cx - 1, cz - 1, coordY, tc, localY, tc);

		// Est/sud : padding local uniquement (MettreAJourPaddingLocalChunk) — pas d'écriture voisin lx=0
		// (décalerait la matière d'un voxel, comme sur le serveur).
		if (localX == 0 && localZ == last)
			SetVoxel(cx - 1, cz + 1, coordY, tc, localY, 0);

		if (!ModeProfondeurTranchesActif() || h <= 0 || chunkCourant == null)
			return;
		int cyMin = ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres);

		void RepliquerJonctionBas()
		{
			SetVoxel(cx, cz, coordY - 1, localX, h, localZ, remeshHaut: true);
			if (localX == 0) SetVoxel(cx - 1, cz, coordY - 1, tc, h, localZ, remeshHaut: true);
			if (localX == last) SetVoxel(cx + 1, cz, coordY - 1, 0, h, localZ, remeshHaut: true);
			if (localZ == 0) SetVoxel(cx, cz - 1, coordY - 1, localX, h, tc, remeshHaut: true);
			if (localZ == last) SetVoxel(cx, cz + 1, coordY - 1, localX, h, 0, remeshHaut: true);
			if (localX == 0 && localZ == 0) SetVoxel(cx - 1, cz - 1, coordY - 1, tc, h, tc, remeshHaut: true);
			if (localX == last && localZ == last) SetVoxel(cx + 1, cz + 1, coordY - 1, 0, h, 0, remeshHaut: true);
			if (localX == 0 && localZ == last) SetVoxel(cx - 1, cz + 1, coordY - 1, tc, h, 0, remeshHaut: true);
			if (localX == last && localZ == 0) SetVoxel(cx + 1, cz - 1, coordY - 1, 0, h, tc, remeshHaut: true);
			AjouterRemeshJonctionVertical((cx, coordY, cz, 0));
		}

		void RepliquerJonctionHaut()
		{
			// Le voxel frontière (monde Y=(coordY+1)*h) est stocké en double : coordY+1 ly=0 ET coordY ly=h (padding miroir).
			// Vider les DEUX copies, sinon le plafond de la tranche courante reste fermé (moitié haute « inminable » à Y=100).
			chunkCourant.SetVoxelLocal(localX, h, localZ, id);
			SetVoxel(cx, cz, coordY + 1, localX, 0, localZ, remeshBas: true);
			if (localX == 0) SetVoxel(cx - 1, cz, coordY + 1, tc, 0, localZ, remeshBas: true);
			if (localX == last) SetVoxel(cx + 1, cz, coordY + 1, 0, 0, localZ, remeshBas: true);
			if (localZ == 0) SetVoxel(cx, cz - 1, coordY + 1, localX, 0, tc, remeshBas: true);
			if (localZ == last) SetVoxel(cx, cz + 1, coordY + 1, localX, 0, 0, remeshBas: true);
			if (localX == 0 && localZ == 0) SetVoxel(cx - 1, cz - 1, coordY + 1, tc, 0, tc, remeshBas: true);
			if (localX == last && localZ == last) SetVoxel(cx + 1, cz + 1, coordY + 1, 0, 0, 0, remeshBas: true);
			if (localX == 0 && localZ == last) SetVoxel(cx - 1, cz + 1, coordY + 1, tc, 0, 0, remeshBas: true);
			if (localX == last && localZ == 0) SetVoxel(cx + 1, cz - 1, coordY + 1, 0, 0, tc, remeshBas: true);
			if (nbSec > 0)
				AjouterRemeshJonctionVertical((cx, coordY, cz, nbSec - 1));
		}

		if (localY == 0 && coordY > cyMin)
			RepliquerJonctionBas();
		if (localY == h - 1)
			RepliquerJonctionHaut();
	}

	/// <summary>Ne pas reboucher un trou miné (air) avec du sol voisin lors de la couture verticale.</summary>
	private static bool DoitPreserverAirMinageSurCoutureClient(ChunkData cible, int lx, int ly, int lz, float densSource, byte matSource)
	{
		if (matSource == 0 || densSource <= 0f)
			return false;
		if (cible?.DensitiesFlat == null || cible.MaterialsFlat == null)
			return false;
		float densCible = cible.DensitiesFlat[cible.Idx(lx, ly, lz)];
		return densCible <= 0f && cible.MaterialsFlat[cible.Idx(lx, ly, lz)] == 0;
	}

	/// <summary>Remesh jonction sans debounce 12 frames (évite voile gris 1–2 s à Y=100).</summary>
	private void AjouterRemeshJonctionVertical((int cx, int coordY, int cz, int section) cle)
	{
		_sectionsAReconstruire.Add(cle);
		_frameDernierRemeshBordVert.Remove(new Vector3I(cle.cx, cle.coordY, cle.cz));
	}

	/// <summary>Aligne ly=0 / ly=h avec les tranches voisines déjà en RAM — recouture si une tranche voisine arrive après.</summary>
	internal void SynchroniserFrontieresVerticalesProfondeurClient(ChunkData chunk, bool premiereCoutureSeulement = true, bool postTraitementLegermachine = false)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.DensitiesFlat == null || chunk.MaterialsFlat == null)
			return;
		Vector2I coord = chunk.Coordonnees;
		int h = chunk.HauteurMax;
		int cy = chunk.CoordChunkY;
		bool voisinBasDispo = TryGetChunkDataPourCoordY(coord, cy - 1, out ChunkData dessous);
		bool voisinHautDispo = TryGetChunkDataPourCoordY(coord, cy + 1, out ChunkData dessus);
		if (premiereCoutureSeulement && chunk.CoutureVoxelAppliquee && !voisinBasDispo && !voisinHautDispo)
			return;

		void CopierVoxelExact(ChunkData cible, int x, int y, int z, ChunkData source, int sx, int sy, int sz)
		{
			int si = source.Idx(sx, sy, sz);
			float dens = source.DensitiesFlat[si];
			byte mat = source.MaterialsFlat[si];
			if (DoitPreserverAirMinageSurCoutureClient(cible, x, y, z, dens, mat))
				return;
			float eau = source.DensitiesEauFlat != null ? source.DensitiesEauFlat[si] : -1f;
			int di = cible.Idx(x, y, z);
			cible.DensitiesFlat[di] = dens;
			cible.MaterialsFlat[di] = mat;
			if (cible.DensitiesEauFlat != null)
				cible.DensitiesEauFlat[di] = eau;
		}

		void CopierFaceDepuis(ChunkData voisin, int sourceLy, int destLy)
		{
			if (voisin?.DensitiesFlat == null || voisin.MaterialsFlat == null) return;
			for (int x = 0; x <= chunk.TailleChunk; x++)
				for (int z = 0; z <= chunk.TailleChunk; z++)
					CopierVoxelExact(chunk, x, destLy, z, voisin, x, sourceLy, z);
		}

		void RepousserFaceVers(ChunkData voisin, int sourceLy, int destLy)
		{
			if (voisin?.DensitiesFlat == null || voisin.MaterialsFlat == null) return;
			for (int x = 0; x <= chunk.TailleChunk; x++)
				for (int z = 0; z <= chunk.TailleChunk; z++)
					CopierVoxelExact(voisin, x, destLy, z, chunk, x, sourceLy, z);
		}

		if (voisinBasDispo && dessous != null)
		{
			CopierFaceDepuis(dessous, h, 0);
			RepousserFaceVers(dessous, 0, h);
			if (!postTraitementLegermachine)
			{
				AppliquerEauMerVerticale3DClient(dessous);
				RecollerTerrainJonctionTrancheSup(dessous);
			}
			bool remeshUrgent = EstChunkProfondeurProcheJoueur(chunk) || EstChunkProfondeurProcheJoueur(dessous);
			if (postTraitementLegermachine && (ModeSurvieFpsAgressif || _fpsMoyenneAuto < 45f))
				remeshUrgent = false;
			MarquerSectionsBordTranchePourRemesh(chunk, dessous, immediate: remeshUrgent);
		}
		if (voisinHautDispo && dessus != null)
		{
			CopierFaceDepuis(dessus, 0, h);
			RepousserFaceVers(dessus, h, 0);
			if (!postTraitementLegermachine)
			{
				AppliquerEauMerVerticale3DClient(dessus);
				RecollerTerrainJonctionTrancheSup(chunk);
			}
			bool remeshUrgent = EstChunkProfondeurProcheJoueur(chunk) || EstChunkProfondeurProcheJoueur(dessus);
			if (postTraitementLegermachine && (ModeSurvieFpsAgressif || _fpsMoyenneAuto < 45f))
				remeshUrgent = false;
			MarquerSectionsBordTranchePourRemesh(chunk, dessus, immediate: remeshUrgent);
		}
		else if (!postTraitementLegermachine)
			RecollerTerrainJonctionTrancheSup(chunk);

		if (!postTraitementLegermachine)
			AppliquerEauMerVerticale3DClient(chunk);
		chunk.CoutureVoxelAppliquee = true;
	}

	/// <summary>Sync XZ désactivée côté client : le padding MC lit les voisins sans muter les voxels (préserve le minage).</summary>
	internal void SynchroniserFrontieresHorizontalesProfondeurClient(ChunkData chunk)
	{
	}

	private void MarquerSectionsBordTrancheHorizontalePourRemesh(ChunkData chunk)
	{
		if (chunk == null || !chunk.VisualInstanceRID.IsValid)
			return;
		MarquerSectionBordVerticaleLegere(chunk, sectionBasse: true);
		MarquerSectionBordVerticaleLegere(chunk, sectionBasse: false);
	}

	/// <summary>Retire l'eau au-dessus de Y=103 dans la tranche courante (ciel, pas océan).</summary>
	internal void NettoyerEauAuDessusNiveauMerClient(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.MaterialsFlat == null)
			return;
		int h = chunk.HauteurMax;
		int yBaseMonde = chunk.CoordChunkY * h;
		const int niveauEauMonde = ConstantesProfondeurVerticale.NiveauEauMondeAlpha;
		for (int x = 0; x <= chunk.TailleChunk; x++)
		{
			for (int z = 0; z <= chunk.TailleChunk; z++)
			{
				for (int y = 0; y <= h; y++)
				{
					if (yBaseMonde + y <= niveauEauMonde) continue;
					if (chunk.MaterialsFlat[chunk.Idx(x, y, z)] == 4)
						chunk.SetVoxelLocal(x, y, z, 0);
				}
			}
		}
	}

	/// <summary>
	/// Ajustements eau légers côté client (jonction Y=100 uniquement). Pas de remplissage volumique :
	/// le serveur envoie l'état réel ; un re-fill local inondait grottes et creusait des océans fantômes.
	/// </summary>
	internal void AppliquerEauMerVerticale3DClient(ChunkData chunk)
	{
		NettoyerEauAuDessusNiveauMerClient(chunk);
		NettoyerEauSousSurfaceTerrestreClient(chunk);
		FusionnerJonctionEauMerVerticaleClient(chunk);
	}

	/// <summary>Retire l'eau sous la surface du terrain (grottes) — le serveur corrigé ne devrait plus en envoyer.</summary>
	internal void NettoyerEauSousSurfaceTerrestreClient(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.MaterialsFlat == null)
			return;
		int h = chunk.HauteurMax;
		int yBaseMonde = chunk.CoordChunkY * h;
		Vector2I coord = chunk.Coordonnees;
		for (int x = 0; x <= chunk.TailleChunk; x++)
		{
			for (int z = 0; z <= chunk.TailleChunk; z++)
			{
				int xg = coord.X * chunk.TailleChunk + x;
				int zg = coord.Y * chunk.TailleChunk + z;
				int hSurf = Generateur_Voxel.ObtenirHauteurTerrainMonde(xg, zg, _seedTerrain);
				for (int y = 0; y <= h; y++)
				{
					if (yBaseMonde + y > hSurf) continue;
					if (chunk.MaterialsFlat[chunk.Idx(x, y, z)] == 4)
						chunk.SetVoxelLocal(x, y, z, 0);
				}
			}
		}
	}

	/// <summary>Tranche dans la fenêtre verticale ±1 autour du joueur (couture prioritaire).</summary>
	private bool EstChunkProfondeurProcheJoueur(ChunkData chunk)
	{
		if (chunk == null || !ModeProfondeurTranchesActif() || !_joueurPresentPourWorkers)
			return false;
		int cyJoueur = _coordYJoueurProfondeurCache;
		if (cyJoueur == int.MinValue)
			return false;
		return Mathf.Abs(chunk.CoordChunkY - cyJoueur) <= ConstantesProfondeurVerticale.DemiFenetreTranches;
	}

	/// <summary>Recopie la roche de la tranche du dessus sur ly=h après la passe eau (anti-déchirure Y=100).</summary>
	internal void RecollerTerrainJonctionTrancheSup(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.DensitiesFlat == null || chunk.MaterialsFlat == null)
			return;
		int h = chunk.HauteurMax;
		int cy = chunk.CoordChunkY;
		if (ConstantesProfondeurVerticale.ObtenirRoleTrancheEauMer(cy, h)
			!= ConstantesProfondeurVerticale.RoleTrancheEauMer.Corps)
			return;
		if (!TryGetChunkDataPourCoordY(chunk.Coordonnees, cy + 1, out var dessus)
			|| dessus?.DensitiesFlat == null || dessus.MaterialsFlat == null)
			return;
		for (int x = 0; x <= chunk.TailleChunk; x++)
		{
			for (int z = 0; z <= chunk.TailleChunk; z++)
			{
				if (dessus.DensitiesFlat[dessus.Idx(x, 0, z)] <= 0f)
					continue;
				if (chunk.DensitiesFlat[chunk.Idx(x, h, z)] <= 0f)
					continue;
				byte mat = dessus.MaterialsFlat[dessus.Idx(x, 0, z)];
				if (mat == 4)
					continue;
				chunk.SetVoxelLocal(x, h, z, mat);
			}
		}
	}

	/// <summary>
	/// Jonction Y=100,200… : prolonge l'eau sur ly=h (fusion avec le chapeau), sans percer la roche en air.
	/// </summary>
	internal void FusionnerJonctionEauMerVerticaleClient(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.MaterialsFlat == null)
			return;
		int h = chunk.HauteurMax;
		int cy = chunk.CoordChunkY;
		int yBaseMonde = cy * h;
		const int niveauEauMonde = ConstantesProfondeurVerticale.NiveauEauMondeAlpha;
		int yJonction = ConstantesProfondeurVerticale.MondeYJonctionTrancheSup(cy, h);
		if (niveauEauMonde < yJonction)
			return;
		if (ConstantesProfondeurVerticale.ObtenirRoleTrancheEauMer(cy, h, niveauEauMonde)
			!= ConstantesProfondeurVerticale.RoleTrancheEauMer.Corps)
			return;

		bool dessusCharge = TryGetChunkDataPourCoordY(chunk.Coordonnees, cy + 1, out var dessus)
			&& dessus?.MaterialsFlat != null;
		for (int x = 0; x <= chunk.TailleChunk; x++)
		{
			for (int z = 0; z <= chunk.TailleChunk; z++)
			{
				if (yJonction > niveauEauMonde)
					continue;
				// Ne jamais toucher la roche à la jonction (sinon déchirure horizontale à Y=100 partout).
				if (chunk.DensitiesFlat[chunk.Idx(x, h, z)] > 0f)
					continue;
				if (h > 0 && chunk.DensitiesFlat[chunk.Idx(x, h - 1, z)] > 0f)
					continue;
				bool eauJusteEnDessous = h > 0 && chunk.MaterialsFlat[chunk.Idx(x, h - 1, z)] == 4;
				if (dessusCharge && dessus.MaterialsFlat[dessus.Idx(x, 0, z)] == 4)
					chunk.SetVoxelLocal(x, h, z, 4);
				else if (eauJusteEnDessous)
					chunk.SetVoxelLocal(x, h, z, 4);
			}
		}
	}

	/// <summary>Recopie la face partagée vers les tranches voisines (legacy — désactivé, MC padding seulement).</summary>
	internal void MarquerRemeshVoisinsVerticalDejaMailes(ChunkData chunk)
	{
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
		if (_chunksDataProfondeur3D.Count > 0)
		{
			_clesChunksAbysseARetirerTemp.Clear();
			foreach (var kv in _chunksDataProfondeur3D)
			{
				if (kv.Key.X == coord.X && kv.Key.Z == coord.Y)
					_clesChunksAbysseARetirerTemp.Add(kv.Key);
			}
			for (int i = 0; i < _clesChunksAbysseARetirerTemp.Count; i++)
			{
				Vector3I cle = _clesChunksAbysseARetirerTemp[i];
				if (!_chunksDataProfondeur3D.TryGetValue(cle, out var dataProfond) || dataProfond == null)
					continue;
				_chunksDataProfondeur3D.Remove(cle);
				dataProfond.LibérerRids();
				dataProfond.LibererDonneesVoxel();
			}
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
		if (hauteurMax <= 0)
			hauteurMax = ModeProfondeurTranchesActif() ? ConstantesProfondeurVerticale.HauteurTrancheMetres : HauteurMax;
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
			RetirerAntiSpamDemandeCoucheChunk(cle3D);
			RetirerAntiSpamDemandeCoucheChunk(cle3DNormalisee);
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
		if (!modeAbysse && ModeProfondeurTranchesActif())
		{
			RetirerAntiSpamDemandeCoucheChunk(cle3D);
			RetirerAntiSpamDemandeCoucheChunk(cle3DNormalisee);
			IntegrerOuMettreAJourChunkProfondeur(coordChunk, donnees, coordY, coordYCle, cle3DNormalisee, empreinte);
			return;
		}
		if (!modeAbysse && _chunksData.TryGetValue(coordChunk, out var existing))
		{
			if (coordY != 0)
			{
				if (_chunksDataProfondeur3D.TryGetValue(cle3DNormalisee, out var existingProfond))
				{
					existingProfond.CoordChunkY = coordY;
					existingProfond.HauteurMax = ObtenirHauteurTrancheDonnees(donnees);
					if (existingProfond.VisualInstanceRID.IsValid && existingProfond.EmpreinteDonneesServeur == empreinte && empreinte != 0)
					{
						SynchroniserProxyChunkProfondeur(coordChunk);
						return;
					}
					EnqueueChunkGeneration(existingProfond, donnees);
					SynchroniserProxyChunkProfondeur(coordChunk);
					return;
				}
			}
			// Profondeur étendue : si la couche verticale change (descente/remontée), on repart propre.
			// Sans libérer les RIDs, le mesh/la collision resteraient figés à l'altitude de l'ancienne couche.
			if (existing.CoordChunkY != coordY)
			{
				if (coordY == 0)
			{
				existing.LibérerRids();
				existing.EmpreinteDonneesServeur = 0;
				existing.CoordChunkY = coordY;
				EnqueueChunkGeneration(existing, donnees);
				}
				else
				{
					var dataProfond = new ChunkData
					{
						Coordonnees = coordChunk,
						CoordChunkY = coordY,
						TailleChunk = TailleChunk,
						HauteurMax = ObtenirHauteurTrancheDonnees(donnees),
						EstVideIntegral = donnees?.EstVideIntegral ?? false
					};
					dataProfond.ConfigurerBruitClimat(_seedTerrain);
					_chunksDataProfondeur3D[cle3DNormalisee] = dataProfond;
					SynchroniserProxyChunkProfondeur(coordChunk);
					EnqueueChunkGeneration(dataProfond, donnees);
				}
				return;
			}
			existing.CoordChunkY = coordY;
			if (existing.VisualInstanceRID.IsValid && existing.EmpreinteDonneesServeur == empreinte && empreinte != 0)
				return;
			RetirerAntiSpamDemandeCoucheChunk(cle3DNormalisee);
			EnqueueChunkGeneration(existing, donnees);
			return;
		}

		var data = new ChunkData
		{
			Coordonnees = coordChunk,
			CoordChunkY = coordYCle,
			TailleChunk = TailleChunk,
			HauteurMax = ObtenirHauteurTrancheDonnees(donnees),
			EstVideIntegral = donnees?.EstVideIntegral ?? false
		};
		data.ConfigurerBruitClimat(_seedTerrain);
		_chunksData[coordChunk] = data;
		if (modeAbysse)
			_chunksDataAbysse3D[cle3DNormalisee] = data;
		else if (coordY != 0)
			_chunksDataProfondeur3D[cle3DNormalisee] = data;
		if (!modeAbysse && coordY != 0)
			SynchroniserProxyChunkProfondeur(coordChunk);
		RetirerAntiSpamDemandeCoucheChunk(cle3DNormalisee);
		EnqueueChunkGeneration(data, donnees);
	}

	private int ObtenirHauteurTrancheDonnees(DonneesChunk donnees)
	{
		if (donnees != null && donnees.HauteurMax > 0)
			return donnees.HauteurMax;
		if (ModeProfondeurTranchesActif())
			return ConstantesProfondeurVerticale.HauteurTrancheMetres;
		return HauteurMax;
	}

	private void IntegrerOuMettreAJourChunkProfondeur(Vector2I coordChunk, DonneesChunk donnees, int coordY, int coordYCle, Vector3I cle3DNormalisee, ulong empreinte)
	{
		int hauteurTranche = ObtenirHauteurTrancheDonnees(donnees);
		if (_chunksDataProfondeur3D.TryGetValue(cle3DNormalisee, out var existingProfond))
		{
			existingProfond.CoordChunkY = coordY;
			existingProfond.HauteurMax = hauteurTranche;
			existingProfond.EstVideIntegral = donnees?.EstVideIntegral ?? false;
			if (existingProfond.VisualInstanceRID.IsValid && existingProfond.EmpreinteDonneesServeur == empreinte && empreinte != 0)
			{
				SynchroniserProxyChunkProfondeur(coordChunk);
				return;
			}
			EnqueueChunkGeneration(existingProfond, donnees);
			SynchroniserProxyChunkProfondeur(coordChunk);
			return;
		}
		var dataProfond = new ChunkData
		{
			Coordonnees = coordChunk,
			CoordChunkY = coordYCle,
			TailleChunk = TailleChunk,
			HauteurMax = hauteurTranche,
			EstVideIntegral = donnees?.EstVideIntegral ?? false
		};
		dataProfond.ConfigurerBruitClimat(_seedTerrain);
		_chunksDataProfondeur3D[cle3DNormalisee] = dataProfond;
		if (coordYCle == 0)
			_chunksData[coordChunk] = dataProfond;
		EnqueueChunkGeneration(dataProfond, donnees);
		SynchroniserProxyChunkProfondeur(coordChunk);
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
