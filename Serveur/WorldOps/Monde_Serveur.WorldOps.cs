using Godot;
using System.Collections.Generic;

/// <summary>
/// Opérations monde globales (destruction, fauchage, récolte, création, socle). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: mêmes règles d'impact rayon/abysse et mêmes mutations voxel que l'historique.
/// </summary>
public partial class Monde_Serveur : Node
{
	public void AppliquerDestructionGlobale(Vector3 pointImpact, float rayon, float forceDegats = 5.0f, int peerDemandeur = -1)
	{
		_modificationEnCours = true;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.DetruireVoxel(pointImpact, rayon, forceDegats);
					}
				}
			return;
		}

		if (ModeProfondeurActive)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesProfond(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.DetruireVoxel(pointImpact, rayon, forceDegats);
					}
				}
			return;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				Vector2I coord = new Vector2I(cx, cz);
				var chunk = ObtenirOuCreerChunk(coord);
				chunk.DetruireVoxel(pointImpact, rayon, forceDegats);
			}
	}

	public void AppliquerFauchageGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.FaucherFlore(pointImpact, rayon);
					}
				}
			return;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				chunk.FaucherFlore(pointImpact, rayon);
			}
	}

	public bool AppliquerFauchageFauneGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		bool aFauche = false;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (chunk.FaucherFloreSansLoot(pointImpact, rayon))
							aFauche = true;
					}
				}
			return aFauche;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				if (chunk.FaucherFloreSansLoot(pointImpact, rayon))
					aFauche = true;
			}
		return aFauche;
	}

	public bool ExisteGazonFauneGlobal(Vector3 pointImpact, float rayon)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (chunk.ExisteGazonDansRayon(pointImpact, rayon))
							return true;
					}
				}
			return false;
		}
		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				if (chunk.ExisteGazonDansRayon(pointImpact, rayon))
					return true;
			}
		return false;
	}

	/// <summary>Récolte ciblée de buisson : 0=hachette (branche), 1=dague, 2=pelle (replantable), 3=dague aloe (sans branche).</summary>
	public bool RecolterBuissonGlobal(Vector3 pointImpact, float rayon, byte modeRecolte)
	{
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (chunk.RecolterBuisson(pointImpact, rayon, modeRecolte))
							return true;
					}
				}
			return false;
		}
		for (int cx = cxMin; cx <= cxMax; cx++)
			for (int cz = czMin; cz <= czMax; cz++)
			{
				var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
				if (chunk.RecolterBuisson(pointImpact, rayon, modeRecolte))
					return true;
			}
		return false;
	}

	/// <summary>Détection d’un buisson sous la visée (sans mutation), utile pour le minage maintenu.</summary>
	public bool EssayerDetecterBuissonGlobal(Vector3 pointImpact, float rayon, out Vector3 posBuisson, out byte typeFlore)
	{
		posBuisson = Vector3.Zero;
		typeFlore = 0;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;
		float meilleureDist2 = float.MaxValue;
		bool trouve = false;
		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte type))
							continue;
						float d2 = pos.DistanceSquaredTo(pointImpact);
						if (!trouve || d2 < meilleureDist2)
						{
							trouve = true;
							meilleureDist2 = d2;
							posBuisson = pos;
							typeFlore = type;
						}
					}
				}
		}
		else
		{
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
					if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte type))
						continue;
					float d2 = pos.DistanceSquaredTo(pointImpact);
					if (!trouve || d2 < meilleureDist2)
					{
						trouve = true;
						meilleureDist2 = d2;
						posBuisson = pos;
						typeFlore = type;
					}
				}
		}
		return trouve;
	}

	/// <summary>Plante un buisson au point visé (terre plate). Retourne false si la zone n'est pas valide.</summary>
	public bool PlanterBuissonGlobal(Vector3 pointImpact, byte typeFlore)
	{
		Vector2I coord = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z, TailleChunk);
		var chunk = ObtenirOuCreerChunk(coord);
		return chunk.PlanterBuisson(pointImpact, typeFlore);
	}

	/// <summary>Cueille les baies d’un buisson plein sous la visée (le buisson devient vide).</summary>
	public bool RecolterBaiesBuissonGlobal(Vector3 pointImpact, float rayon, out int quantiteBaies, out byte indexCouleurBaie)
	{
		quantiteBaies = 0;
		indexCouleurBaie = 0;
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X - rayon, pointImpact.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X + rayon, pointImpact.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointImpact.X, pointImpact.Z + rayon, TailleChunk).Y;

		Vector2I meilleurChunk = default;
		int meilleurCoordYChunk = 0;
		Vector3 meilleurePos = Vector3.Zero;
		float meilleureDist2 = float.MaxValue;
		bool trouve = false;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointImpact.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte typeFlore) || !Chunk_Serveur.EstBuissonPlein(typeFlore))
							continue;
						float d2 = pos.DistanceSquaredTo(pointImpact);
						if (!trouve || d2 < meilleureDist2)
						{
							trouve = true;
							meilleureDist2 = d2;
							meilleurePos = pos;
							meilleurChunk = coord;
							meilleurCoordYChunk = coordY;
						}
					}
				}
		}
		else
		{
			for (int cx = cxMin; cx <= cxMax; cx++)
				for (int cz = czMin; cz <= czMax; cz++)
				{
					var chunk = ObtenirOuCreerChunk(new Vector2I(cx, cz));
					if (!chunk.EssayerDetecterBuisson(pointImpact, rayon, out Vector3 pos, out byte typeFlore) || !Chunk_Serveur.EstBuissonPlein(typeFlore))
						continue;
					float d2 = pos.DistanceSquaredTo(pointImpact);
					if (!trouve || d2 < meilleureDist2)
					{
						trouve = true;
						meilleureDist2 = d2;
						meilleurePos = pos;
						meilleurChunk = new Vector2I(cx, cz);
						meilleurCoordYChunk = 0;
					}
				}
		}

		if (!trouve) return false;
		var cible = ActiverGenerationAbysse
			? ObtenirOuCreerChunk(meilleurChunk, meilleurCoordYChunk)
			: ObtenirOuCreerChunk(meilleurChunk);
		return cible.RecolterBaiesBuisson(meilleurePos, rayon, out quantiteBaies, out indexCouleurBaie);
	}

	public void AppliquerCreationGlobale(Vector3 pointImpact, Vector3 normale, float rayon, int idMatiere = 1)
	{
		_modificationEnCours = true;
		Vector3 pointCible = pointImpact + (normale * 0.1f); // Réduit pour éviter les blocs flottants
		byte matiere = (byte)Mathf.Clamp(idMatiere, 0, 255);
		int cxMin = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X - rayon, pointCible.Z, TailleChunk).X;
		int cxMax = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X + rayon, pointCible.Z, TailleChunk).X;
		int czMin = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X, pointCible.Z - rayon, TailleChunk).Y;
		int czMax = Gestionnaire_Monde.WorldToChunkCoord(pointCible.X, pointCible.Z + rayon, TailleChunk).Y;

		if (ActiverGenerationAbysse)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesParRayonAbysse(pointCible.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
			{
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.CreerMatiere(pointCible, rayon, matiere);
					}
				}
			}
			return;
		}

		if (ModeProfondeurActive)
		{
			var coordYImpactes = new HashSet<int>();
			RemplirCoordYImpactesProfond(pointCible.Y, rayon, coordYImpactes);
			for (int cx = cxMin; cx <= cxMax; cx++)
			{
				for (int cz = czMin; cz <= czMax; cz++)
				{
					Vector2I coord = new Vector2I(cx, cz);
					foreach (int coordY in coordYImpactes)
					{
						var chunk = ObtenirOuCreerChunk(coord, coordY);
						chunk.CreerMatiere(pointCible, rayon, matiere);
					}
				}
			}
			return;
		}

		for (int cx = cxMin; cx <= cxMax; cx++)
		{
			for (int cz = czMin; cz <= czMax; cz++)
			{
				Vector2I coord = new Vector2I(cx, cz);
				var chunk = ObtenirOuCreerChunk(coord);
				chunk.CreerMatiere(pointCible, rayon, matiere);
			}
		}
	}

	/// <summary>
	/// Comble uniquement l’air entre le premier solide rencontré sous les pieds et la surface (disque XZ), sur une profondeur max.
	/// Sans solide dans la fenêtre : aucun remblai (évite une colonne pleine dans le ciel quand le portail était mal aligné).
	/// Réplication via <see cref="Chunk_Serveur.ModifierVoxelEtNotifier"/>.
	/// </summary>
	public void RemplirSocleSousPortail(Vector3 centreMondeXZ, float ySurfaceTerrain, int rayonDemiCoteVoxels, int profondeurMaxVersLeBas)
	{
		rayonDemiCoteVoxels = Mathf.Max(0, rayonDemiCoteVoxels);
		profondeurMaxVersLeBas = Mathf.Max(1, profondeurMaxVersLeBas);
		int gx0 = Mathf.FloorToInt(centreMondeXZ.X);
		int gz0 = Mathf.FloorToInt(centreMondeXZ.Z);
		int yTop = Mathf.FloorToInt(ySurfaceTerrain);
		int yScanMin = yTop - profondeurMaxVersLeBas;
		const int yMondeDepuis = 3;
		yScanMin = Mathf.Max(yMondeDepuis, yScanMin);

		int rCarre = rayonDemiCoteVoxels * rayonDemiCoteVoxels;
		const byte idTerre = 2;
		const byte idHerbe = 1;

		for (int dx = -rayonDemiCoteVoxels; dx <= rayonDemiCoteVoxels; dx++)
		{
			for (int dz = -rayonDemiCoteVoxels; dz <= rayonDemiCoteVoxels; dz++)
			{
				if (dx * dx + dz * dz > rCarre)
					continue;
				int gx = gx0 + dx;
				int gz = gz0 + dz;
				Gestionnaire_Monde.WorldToChunkAndLocal(gx, gz, TailleChunk, out Vector2I coordChunk, out int lx, out int lz);
				if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk)
					continue;

				int? ySolideSousPied = null;
				for (int gy = yTop; gy >= yScanMin; gy--)
				{
					int coordYSlice = CoordYDepuisMondeY(gy, HauteurMax);
					Chunk_Serveur chunkScan = ObtenirOuCreerChunk(coordChunk, coordYSlice);
					int lyScan = LocalYDepuisMondeY(gy, HauteurMax);
					if (!chunkScan.EstVoxelAir(lx, lyScan, lz))
					{
						ySolideSousPied = gy;
						break;
					}
				}

				if (!ySolideSousPied.HasValue)
					continue;

				for (int gy = ySolideSousPied.Value + 1; gy <= yTop; gy++)
				{
					int coordYSlice = CoordYDepuisMondeY(gy, HauteurMax);
					Chunk_Serveur chunk = ObtenirOuCreerChunk(coordChunk, coordYSlice);
					int ly = LocalYDepuisMondeY(gy, HauteurMax);
					if (!chunk.EstVoxelAir(lx, ly, lz))
						continue;
					byte id = gy == yTop ? idHerbe : idTerre;
					chunk.ModifierVoxelEtNotifier(lx, ly, lz, id);
				}
			}
		}
	}
}
