using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	/// <summary>Même critère de surface que la flore au chargement du chunk : solide avec air au-dessus (densité &gt; 0). Chunk déjà en RAM pour la dimension active.</summary>
	public bool EssayerObtenirYSurfaceMondeDepuisDonneesVoxel(float worldX, float worldZ, out float ySurfaceMonde)
	{
		ySurfaceMonde = 0f;
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse) return false;
		Gestionnaire_Monde.WorldToChunkAndLocal(worldX, worldZ, TailleChunk, out Vector2I c, out int lx, out int lz);
		int hEstime = Generateur_Voxel.ObtenirHauteurTerrainMonde((int)worldX, (int)worldZ, _seedTerrain);
		ChunkData data = null;
		if (ModeProfondeurTranchesActif())
		{
			int coordY = CoordYDepuisMondeY(hEstime);
			for (int d = -ConstantesProfondeurVerticale.DemiFenetreTranches; d <= ConstantesProfondeurVerticale.DemiFenetreTranches; d++)
			{
				if (TryGetChunkDataPourCoordY(c, coordY + d, out data) && data?.DensitiesFlat != null)
					break;
			}
		}
		else if (!_chunksData.TryGetValue(c, out data) || data.DensitiesFlat == null)
			data = null;

		if (data == null || data.MaterialsFlat == null)
		{
			ySurfaceMonde = hEstime + 1f;
			return true;
		}
		if (lx < 0 || lx > data.TailleChunk || lz < 0 || lz > data.TailleChunk) return false;
		const float isolevel = 0f;
		int ySurface = -1;
		for (int y = data.HauteurMax - 1; y >= 0; y--)
		{
			float d = data.DensitiesFlat[data.Idx(lx, y, lz)];
			if (d <= isolevel) continue;
			bool videAuDessus = y + 1 > data.HauteurMax || data.DensitiesFlat[data.Idx(lx, y + 1, lz)] <= isolevel;
			if (videAuDessus)
			{
				ySurface = y;
				break;
			}
		}

		if (ySurface < 0)
		{
			ySurfaceMonde = hEstime + 1f;
			return true;
		}
		ySurfaceMonde = data.ObtenirOffsetYMonde() + ySurface + 1f;
		return true;
	}

	/// <summary>Interroge la densité à une position globale (chunk en RAM uniquement). Plus utilisé pour Marching Cubes (rembourrage 17³).</summary>
	public (float valeur, bool trouve) ObtenirDensiteGlobaleEx(Vector3I posGlobale)
	{
		Gestionnaire_Monde.WorldToChunkAndLocal(posGlobale.X, posGlobale.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
		int coordY = CoordYDepuisMondeY(posGlobale.Y);
		if (!TryGetChunkDataPourCoordY(c, coordY, out var data)) return (-10f, false);
		int localY = LocalYDepuisMondeY(posGlobale.Y);
		return (data.ObtenirDensiteLocale(lx, localY, lz), true);
	}

	/// <summary>Vrai si une grille réduite sous les pieds a ses collisions actives (le rayon complet de dormance se remplit ensuite en jeu).</summary>
	public bool ChunkSousPiedsAPret()
	{
		if (!EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef)) return false;
		Vector3 pos = joueurRef.GlobalPosition;
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pos, TailleChunk);
		int rg = Mathf.Clamp(RayonGrilleMinSpawnPret, 0, RayonDormancePhysique);
		for (int dx = -rg; dx <= rg; dx++)
			for (int dz = -rg; dz <= rg; dz++)
			{
				var v = new Vector2I(c.X + dx, c.Y + dz);
				if (!ChunkCollisionActivePourObservation(v, pos))
					return false;
			}
		return true;
	}

	/// <summary>Vrai si le chunk a une collision active (body valide, non dormant, hors file de solidification).</summary>
	public bool ChunkCollisionActive(Vector2I coord)
	{
		Vector3 obs = _joueur?.GlobalPosition ?? ObtenirPositionObservation();
		return ChunkCollisionActivePourObservation(coord, obs);
	}

	private bool ChunkCollisionActivePourObservation(Vector2I coord, Vector3 observation)
	{
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			return ChunkCollisionActiveAbyssePourObservation(coord, observation);
		if (ModeProfondeurTranchesActif())
		{
			int coordY = CoordYDepuisMondeY((int)Mathf.Floor(observation.Y));
			int localY = ConstantesProfondeurVerticale.LocalYDepuisMondeY((int)Mathf.Floor(observation.Y));
			if (CoucheCollisionActive(coord, coordY))
				return true;
			if (localY < 4 && CoucheCollisionActive(coord, coordY - 1))
				return true;
			return false;
		}
		int coordYSimple = CoordYDepuisMondeY((int)Mathf.Floor(observation.Y));
		return CoucheCollisionActive(coord, coordYSimple);
	}

	private bool CoucheCollisionActive(Vector2I coord, int coordY)
	{
		if (TryGetChunkDataPourCoordY(coord, coordY, out var data) && data != null)
			return data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification;
		if (_chunksData.TryGetValue(coord, out data) && data != null && data.CoordChunkY == coordY)
			return data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification;
		return false;
	}

	/// <summary>
	/// Alpha-like : le chunk horizontal contenant ce XZ a collision terrain active et des densités/voxels en RAM
	/// (monde généré pour la seed courante réellement connu côté client, comme la flore).
	/// </summary>
	public bool ChunkTerrainPretAvecVoxelsPourCoordMonde(float worldX, float worldZ)
	{
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			return false;
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(worldX, worldZ, TailleChunk);
		if (!ChunkCollisionActive(c))
			return false;
		if (ModeProfondeurTranchesActif())
		{
			Vector3 obs = ObtenirPositionObservation();
			int coordY = CoordYDepuisMondeY((int)Mathf.Floor(obs.Y));
			if (!TryGetChunkDataPourCoordY(c, coordY, out ChunkData dataProf))
				return false;
			return dataProf.DensitiesFlat != null && dataProf.MaterialsFlat != null;
		}
		if (!_chunksData.TryGetValue(c, out ChunkData data))
			return false;
		return data.DensitiesFlat != null && data.MaterialsFlat != null;
	}

	/// <summary>Vrai si le chunk sous les pieds et ses 4 voisins cardinaux ont une collision active (évite fissures de bord au démarrage).</summary>
	public bool ChunkSousPiedsEtVoisinsCardinauxPrets()
	{
		if (!EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef)) return false;
		Vector3 pos = joueurRef.GlobalPosition;
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pos, TailleChunk);
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			if (!ChunkCollisionActiveAbyssePourObservation(c, pos)) return false;
			if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X - 1, c.Y), pos)) return false;
			if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X + 1, c.Y), pos)) return false;
			if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X, c.Y - 1), pos)) return false;
			if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X, c.Y + 1), pos)) return false;
			return true;
		}
		if (!ChunkCollisionActivePourObservation(c, pos)) return false;
		if (!ChunkCollisionActivePourObservation(new Vector2I(c.X - 1, c.Y), pos)) return false;
		if (!ChunkCollisionActivePourObservation(new Vector2I(c.X + 1, c.Y), pos)) return false;
		if (!ChunkCollisionActivePourObservation(new Vector2I(c.X, c.Y - 1), pos)) return false;
		if (!ChunkCollisionActivePourObservation(new Vector2I(c.X, c.Y + 1), pos)) return false;
		return true;
	}

	/// <summary>Vrai si la collision terrain est active autour d'un point monde (rayon en chunks).</summary>
	public bool CollisionTerrainActiveAutourPoint(Vector3 pointMonde, int rayonChunks = 0)
	{
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pointMonde, TailleChunk);
		int rayonMax = _dimensionReseauActive == (int)DimensionJeu.Abysse ? 5 : 2;
		int rayon = Mathf.Clamp(rayonChunks, 0, rayonMax);
		for (int dx = -rayon; dx <= rayon; dx++)
			for (int dz = -rayon; dz <= rayon; dz++)
				if (!ChunkCollisionActivePourObservation(new Vector2I(c.X + dx, c.Y + dz), pointMonde))
					return false;
		return true;
	}

	private bool ChunkCollisionActiveAbyssePourObservation(Vector2I coord, Vector3 observation)
	{
		// Sécurité anti-chute: en priorité stricte, on valide la collision du stage courant
		// (pas un stage voisin), sinon on peut marcher sur une couche absente.
		int coordYCourant = CoordYStageAbysseDepuisYMonde(observation.Y);
		if (_chunksDataAbysse3D.TryGetValue(new Vector3I(coord.X, coordYCourant, coord.Y), out var dataCourant)
			&& dataCourant != null
			&& dataCourant.PhysicsBodyRID.IsValid
			&& !dataCourant.Dormant
			&& !dataCourant.EstEnFileSolidification)
		{
			return true;
		}
		// Chunk vide déjà en RAM : collision « prête » (trou réel, streaming) — évite filet joueur / boucle panique.
		if (dataCourant != null && dataCourant.EstVideIntegral)
			return true;
		return false;
	}

	/// <summary>
	/// Condition stricte anti-chute pour APISARA (<see cref="ConstantesDimensionAbysse.Apisara"/>): collision active sur le chunk courant
	/// et les 4 voisins cardinaux, dans la fenêtre de paliers active.
	/// </summary>
	public bool AbyssePretPourDeplacement(Vector3 pointMonde)
	{
		if (_dimensionReseauActive != (int)DimensionJeu.Abysse)
			return ChunkSousPiedsEtVoisinsCardinauxPrets();
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pointMonde, TailleChunk);
		if (!ChunkCollisionActiveAbyssePourObservation(c, pointMonde)) return false;
		if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X - 1, c.Y), pointMonde)) return false;
		if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X + 1, c.Y), pointMonde)) return false;
		if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X, c.Y - 1), pointMonde)) return false;
		if (!ChunkCollisionActiveAbyssePourObservation(new Vector2I(c.X, c.Y + 1), pointMonde)) return false;
		return true;
	}

	/// <summary>Prêt minimal local en Abysse: collision active sur le chunk courant (sans exiger les 4 voisins).</summary>
	public bool AbysseCollisionLocaleActive(Vector3 pointMonde)
	{
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(pointMonde, TailleChunk);
		return ChunkCollisionActivePourObservation(c, pointMonde);
	}
}
