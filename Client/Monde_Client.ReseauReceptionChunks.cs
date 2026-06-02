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
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobal.X, posGlobal.Z, TailleChunk, out Vector2I c, out int localX, out int localZ);
		int cx = c.X;
		int cz = c.Y;
		int coordY = CoordYDepuisMondeY(posGlobal.Y);
		int localVoxelY = LocalYDepuisMondeY(posGlobal.Y);
		int sec = Mathf.FloorToInt(localVoxelY / 16f);
		int localYSection = localVoxelY - sec * 16;

		if (!TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY, out var data))
		{
			if (_dimensionReseauActive != (int)DimensionJeu.Abysse)
				DemanderChunkCouche(new Vector2I(cx, cz), coordY, urgent: true);
			return;
		}
		int nbSec = Chunk_Client.ObtenirNbSectionsEffectif(data.HauteurMax);
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
		RepliquerPaddingVoisinsVerticauxProfondeur(cx, cz, coordY, localX, localZ, localVoxelY, data.HauteurMax, id, data);

		void MarquerSectionsChunk(int chunkX, int chunkZ)
			=> MarquerSectionsChunkSiPresent(chunkX, chunkZ, coordY, sec, localYSection, localVoxelY, nbSec);

		MarquerSectionsChunk(cx, cz);
		if (localX == 0) MarquerSectionsChunk(cx - 1, cz);
		if (localZ == 0) MarquerSectionsChunk(cx, cz - 1);
		if (localX == 0 && localZ == 0) MarquerSectionsChunk(cx - 1, cz - 1);

		if (_dimensionReseauActive != (int)DimensionJeu.Abysse)
		{
			MarquerSectionsJonctionTrancheVerticale(cx, cz, coordY, localVoxelY, data.HauteurMax);
			EnsurerTranchesVoisinesChargeesPourMinage(cx, cz, coordY, localVoxelY, data.HauteurMax);
		}
	}

	/// <summary>Jonction Y=0, ±100… : demander la tranche voisine avant de miner si elle manque encore en RAM.</summary>
	private void EnsurerTranchesVoisinesChargeesPourMinage(int chunkX, int chunkZ, int coordY, int localY, int hauteurTranche)
	{
		if (!ModeProfondeurTranchesActif() || hauteurTranche <= 0)
			return;
		var coord = new Vector2I(chunkX, chunkZ);
		if (localY <= 3 && coordY > ConstantesProfondeurVerticale.CoordYDepuisMondeY(-ProfondeurMaxMetres))
		{
			if (!TryGetChunkDataPourCoordY(coord, coordY - 1, out _))
				DemanderChunkCouche(coord, coordY - 1, urgent: true);
		}
		if (localY >= hauteurTranche - 4)
		{
			if (!TryGetChunkDataPourCoordY(coord, coordY + 1, out _))
				DemanderChunkCouche(coord, coordY + 1, urgent: true);
		}
	}

	/// <summary>Remesh léger des 2 sections bas/haut après sync voxel (évite trou à l'entrée en profondeur Y&lt;0).</summary>
	private void MarquerSectionsBordTranchePourRemesh(ChunkData chunk, ChunkData voisin)
	{
		if (chunk == null) return;
		int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(chunk.HauteurMax);
		Vector2I c = chunk.Coordonnees;
		int cy = chunk.CoordChunkY;
		if (chunk.VisualInstanceRID.IsValid)
		{
			for (int s = 0; s <= 1 && s < nbSec; s++)
				_sectionsAReconstruire.Add((c.X, cy, c.Y, s));
			for (int s = nbSec - 2; s < nbSec; s++)
				if (s >= 0)
					_sectionsAReconstruire.Add((c.X, cy, c.Y, s));
		}
		if (voisin == null || !voisin.VisualInstanceRID.IsValid)
			return;
		int nbSecV = ConstantesProfondeurVerticale.ObtenirNbSections(voisin.HauteurMax);
		Vector2I cv = voisin.Coordonnees;
		int cyv = voisin.CoordChunkY;
		for (int s = 0; s <= 1 && s < nbSecV; s++)
			_sectionsAReconstruire.Add((cv.X, cyv, cv.Y, s));
		for (int s = nbSecV - 2; s < nbSecV; s++)
			if (s >= 0)
				_sectionsAReconstruire.Add((cv.X, cyv, cv.Y, s));
	}

	/// <summary>Couture verticale au minage : sections bas/haut uniquement si la modification touche la frontière Y.</summary>
	private void MarquerSectionsJonctionTrancheVerticale(int chunkX, int chunkZ, int coordY, int localY, int hauteurTranche)
	{
		if (!ModeProfondeurTranchesActif() || hauteurTranche <= 0 || localY < 0)
			return;
		int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(hauteurTranche);
		bool presBas = localY <= 3;
		bool presHaut = localY >= hauteurTranche - 4;
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

	/// <summary>Réplique une modification sur le padding vertical partagé entre tranches coordY±1 (100 m).</summary>
	private void RepliquerPaddingVoisinsVerticauxProfondeur(int cx, int cz, int coordY, int localX, int localZ, int localY, int hauteurTranche, byte id, ChunkData chunkCourant)
	{
		if (!ModeProfondeurTranchesActif() || hauteurTranche <= 0 || chunkCourant == null) return;
		if (localY == 0 && TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY - 1, out var sous))
		{
			sous.SetVoxelLocal(localX, hauteurTranche, localZ, id);
			int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(hauteurTranche);
			for (int s = nbSec - 2; s < nbSec; s++)
				if (s >= 0)
					_sectionsAReconstruire.Add((cx, sous.CoordChunkY, cz, s));
		}
		if (localY == hauteurTranche - 1)
		{
			chunkCourant.SetVoxelLocal(localX, hauteurTranche, localZ, id);
			if (TryGetChunkDataPourCoordY(new Vector2I(cx, cz), coordY + 1, out var sur))
			{
				sur.SetVoxelLocal(localX, 0, localZ, id);
				int nbSec = ConstantesProfondeurVerticale.ObtenirNbSections(hauteurTranche);
				for (int s = 0; s <= 1 && s < nbSec; s++)
					_sectionsAReconstruire.Add((cx, sur.CoordChunkY, cz, s));
			}
		}
	}

	/// <summary>Aligne ly=0 / ly=h avec les tranches voisines déjà en RAM (évite les trous à Y=100, 200…).</summary>
	internal void SynchroniserFrontieresVerticalesProfondeurClient(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.DensitiesFlat == null || chunk.MaterialsFlat == null)
			return;
		Vector2I coord = chunk.Coordonnees;
		int h = chunk.HauteurMax;
		int cy = chunk.CoordChunkY;

		void CopierFaceDepuis(ChunkData voisin, int sourceLy, int destLy)
		{
			if (voisin?.DensitiesFlat == null || voisin.MaterialsFlat == null) return;
			for (int x = 0; x <= chunk.TailleChunk; x++)
				for (int z = 0; z <= chunk.TailleChunk; z++)
				{
					byte mat = voisin.MaterialsFlat[voisin.Idx(x, sourceLy, z)];
					chunk.SetVoxelLocal(x, destLy, z, mat);
				}
		}

		bool coutureDessous = false;
		bool coutureDessus = false;
		if (TryGetChunkDataPourCoordY(coord, cy - 1, out var dessous))
		{
			CopierFaceDepuis(dessous, h, 0);
			RecopierFaceVersVoisin(dessous, 0, h, chunk);
			coutureDessous = true;
		}
		if (TryGetChunkDataPourCoordY(coord, cy + 1, out var dessus))
		{
			CopierFaceDepuis(dessus, 0, h);
			RecopierFaceVersVoisin(dessus, h, 0, chunk);
			coutureDessus = true;
		}
		AppliquerEauMerVerticale3DClient(chunk);
		if (coutureDessus && TryGetChunkDataPourCoordY(coord, cy + 1, out var dessusHarmonise))
			AppliquerEauMerVerticale3DClient(dessusHarmonise);
		if (coutureDessous && TryGetChunkDataPourCoordY(coord, cy - 1, out var dessousHarmonise))
			AppliquerEauMerVerticale3DClient(dessousHarmonise);
		RecollerTerrainJonctionTrancheSup(chunk);

		// Remesh bord après voxels eau 3D (évite double surface à Y=100 tant que la tranche voisine arrive).
		if (CompterBacklog() < SeuilBacklogBas)
		{
			if (coutureDessous)
				MarquerSectionsBordTranchePourRemesh(chunk, dessous);
			if (coutureDessus)
				MarquerSectionsBordTranchePourRemesh(chunk, dessus);
		}

		void RecopierFaceVersVoisin(ChunkData voisin, int sourceLy, int destLy, ChunkData sourceChunk)
		{
			if (voisin?.MaterialsFlat == null) return;
			for (int x = 0; x <= chunk.TailleChunk; x++)
				for (int z = 0; z <= chunk.TailleChunk; z++)
					voisin.SetVoxelLocal(x, destLy, z, sourceChunk.MaterialsFlat[sourceChunk.Idx(x, sourceLy, z)]);
		}
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

	/// <summary>Harmonise voxels eau + nettoyage jonctions (après sync verticale 3D).</summary>
	internal void AppliquerEauMerVerticale3DClient(ChunkData chunk)
	{
		HarmoniserEauVerticaleProfondeurClient(chunk);
		NettoyerEauAuDessusNiveauMerClient(chunk);
		FusionnerJonctionEauMerVerticaleClient(chunk);
	}

	/// <summary>Propage l'eau depuis la tranche inférieure (jonction 100 m) pour éviter une surface d'eau au milieu d'une colonne.</summary>
	internal void HarmoniserEauVerticaleProfondeurClient(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.MaterialsFlat == null || chunk.DensitiesFlat == null)
			return;
		Vector2I coord = chunk.Coordonnees;
		int h = chunk.HauteurMax;
		int cy = chunk.CoordChunkY;
		int yBaseMonde = cy * h;
		const int niveauEauMonde = ConstantesProfondeurVerticale.NiveauEauMondeAlpha;
		int yMaxEauLocal = ConstantesProfondeurVerticale.ObtenirYMaxEauLocalTranche(cy, h);
		if (yMaxEauLocal <= 0)
			return;

		if (TryGetChunkDataPourCoordY(coord, cy - 1, out var dessous) && dessous?.MaterialsFlat != null)
		{
			for (int x = 0; x <= chunk.TailleChunk; x++)
			{
				for (int z = 0; z <= chunk.TailleChunk; z++)
				{
					int lySource = h;
					if (dessous.MaterialsFlat[dessous.Idx(x, lySource, z)] != 4 && lySource > 0
						&& dessous.MaterialsFlat[dessous.Idx(x, lySource - 1, z)] == 4)
						lySource = lySource - 1;
					if (dessous.MaterialsFlat[dessous.Idx(x, lySource, z)] != 4)
						continue;
					for (int y = 0; y <= yMaxEauLocal; y++)
					{
						if (chunk.MaterialsFlat[chunk.Idx(x, y, z)] != 0) break;
						chunk.SetVoxelLocal(x, y, z, 4);
					}
				}
			}
		}

		var role = ConstantesProfondeurVerticale.ObtenirRoleTrancheEauMer(cy, h, niveauEauMonde);
		if (role == ConstantesProfondeurVerticale.RoleTrancheEauMer.Chapeau
			|| role == ConstantesProfondeurVerticale.RoleTrancheEauMer.Corps)
			RemplirEauVolumeMer3DClient(chunk, yBaseMonde, yMaxEauLocal, niveauEauMonde);

		FusionnerJonctionEauMerVerticaleClient(chunk);
	}

	/// <summary>Remplit l'air sous la mer (tranche corps ou chapeau) — logique 3D, pas test ciel 2.5D.</summary>
	private static void RemplirEauVolumeMer3DClient(ChunkData chunk, int yBaseMonde, int yMaxEauLocal, int niveauEauMonde)
	{
		int h = chunk.HauteurMax;
		for (int x = 0; x <= chunk.TailleChunk; x++)
		{
			for (int z = 0; z <= chunk.TailleChunk; z++)
			{
				int sommetSolide = -1;
				for (int ly = h; ly >= 0; ly--)
				{
					if (chunk.DensitiesFlat[chunk.Idx(x, ly, z)] > 0f)
					{
						sommetSolide = ly;
						break;
					}
				}
				int yDebut = Mathf.Clamp(sommetSolide + 1, 0, yMaxEauLocal);
				for (int y = yDebut; y <= yMaxEauLocal; y++)
				{
					if (yBaseMonde + y > niveauEauMonde) continue;
					if (chunk.MaterialsFlat[chunk.Idx(x, y, z)] != 0) continue;
					chunk.SetVoxelLocal(x, y, z, 4);
				}
			}
		}
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

	/// <summary>
	/// Copie la face partagée vers les tranches voisines déjà chargées (données voxel / future collision).
	/// Pas de remaillage ici : le padding MC lit le voisin au maillage ; un remesh complet via
	/// <see cref="_sectionsAReconstruire"/> libérait tous les RIDs et bloquait le streaming (trous + void).
	/// </summary>
	internal void MarquerRemeshVoisinsVerticalDejaMailes(ChunkData chunk)
	{
		if (!ModeProfondeurTranchesActif() || chunk?.MaterialsFlat == null)
			return;
		Vector2I coord = chunk.Coordonnees;
		int h = chunk.HauteurMax;
		int cy = chunk.CoordChunkY;

		void PousserFaceVers(ChunkData voisin, int sourceLy, int destLy)
		{
			if (voisin?.MaterialsFlat == null || !voisin.VisualInstanceRID.IsValid)
				return;
			for (int x = 0; x <= chunk.TailleChunk; x++)
				for (int z = 0; z <= chunk.TailleChunk; z++)
					voisin.SetVoxelLocal(x, destLy, z, chunk.MaterialsFlat[chunk.Idx(x, sourceLy, z)]);
		}

		if (TryGetChunkDataPourCoordY(coord, cy - 1, out var dessous))
			PousserFaceVers(dessous, 0, h);
		if (TryGetChunkDataPourCoordY(coord, cy + 1, out var dessus))
			PousserFaceVers(dessus, h, 0);
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
		if (!modeAbysse && ModeProfondeurTranchesActif())
		{
			_demandesProfondeurFrameDerniereEmission.Remove(cle3D);
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
