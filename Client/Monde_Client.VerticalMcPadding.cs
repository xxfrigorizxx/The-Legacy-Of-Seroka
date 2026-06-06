using Godot;

/// <summary>Padding MC vertical (tranches 100 m) et corridor de solidification / marche.</summary>
public partial class Monde_Client : Node3D
{
	/// <summary>
	/// Échantillonne un voxel en coordonnées monde (tranche voisine coordY±1, chunk voisin XZ, ou prolongement de bord).
	/// </summary>
	public bool TryEchantillonnerVoxelProfondeur(ChunkData data, int lx, int ly, int lz, out float densite, out float eau, out byte mat)
	{
		densite = -10f;
		eau = -1f;
		mat = 0;
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null)
			return false;

		if (!ModeProfondeurTranchesActif())
		{
			if (lx < 0 || lx > data.TailleChunk || ly < 0 || ly > data.HauteurMax || lz < 0 || lz > data.TailleChunk)
				return false;
			LireVoxelLocal(data, lx, ly, lz, ref densite, ref eau, ref mat);
			return true;
		}

		int tc = data.TailleChunk;
		int h = data.HauteurMax;
		int cy = data.CoordChunkY;
		Vector2I coord = data.Coordonnees;
		int xMonde = coord.X * tc + lx;
		int zMonde = coord.Y * tc + lz;
		int yMonde = cy * h + ly;

		Vector2I coordCible = Gestionnaire_Monde.WorldToChunkCoord(xMonde, zMonde, TailleChunk);
		int cyCible = ConstantesProfondeurVerticale.CoordYDepuisMondeY(yMonde);
		int lxC = xMonde - coordCible.X * tc;
		int lzC = zMonde - coordCible.Y * tc;
		int lyC = yMonde - cyCible * h;

		if (TryGetChunkDataPourCoordY(coordCible, cyCible, out var source)
			&& source?.DensitiesFlat != null && source.MaterialsFlat != null
			&& lxC >= 0 && lxC <= tc && lyC >= 0 && lyC <= source.HauteurMax && lzC >= 0 && lzC <= tc)
		{
			LireVoxelLocal(source, lxC, lyC, lzC, ref densite, ref eau, ref mat);
			return true;
		}

		// Voisin vertical absent : miroir sur la ligne de couture (ly=0 / ly=h) au lieu d'un clamp aveugle.
		int lxBord = Mathf.Clamp(lx, 0, tc);
		int lzBord = Mathf.Clamp(lz, 0, tc);
		if (cyCible == cy - 1 && ly < 0)
		{
			LireVoxelLocal(data, lxBord, 0, lzBord, ref densite, ref eau, ref mat);
			return true;
		}
		if (cyCible == cy + 1 && ly > h)
		{
			LireVoxelLocal(data, lxBord, h, lzBord, ref densite, ref eau, ref mat);
			return true;
		}

		int lyBord = ly;
		if (ly > h)
			lyBord = h;
		else if (ly < 0)
			lyBord = 0;

		// Voisin horizontal absent : prolonger le bord local (air/roche) — ne jamais inventer de l'eau ici.
		LireVoxelLocal(data, lxBord, lyBord, lzBord, ref densite, ref eau, ref mat);
		return true;
	}

	private static void LireVoxelLocal(ChunkData data, int lx, int ly, int lz, ref float densite, ref float eau, ref byte mat)
	{
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null)
		{
			densite = -10f;
			eau = -1f;
			mat = 0;
			return;
		}
		int tc = data.TailleChunk;
		int h = data.HauteurMax;
		if (lx < 0 || lz < 0 || lx > tc || lz > tc || ly < 0 || ly > h)
		{
			densite = -10f;
			eau = -1f;
			mat = 0;
			return;
		}
		int i = data.Idx(lx, ly, lz);
		densite = data.DensitiesFlat[i];
		mat = data.MaterialsFlat[i];
		if (data.DensitiesEauFlat != null)
			eau = data.DensitiesEauFlat[i];
	}

	/// <summary>Frein avant un mesh visible sans collision dans le cône de course (pas le ciel lointain).</summary>
	public bool CorridorMarcheBloque(Vector3 posJoueur, Vector3 velXZ)
	{
		if (!ModeProfondeurTranchesActif() || velXZ.LengthSquared() < 0.25f)
			return false;

		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cyJoueur = CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y));
		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur, 1, 2);

		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I cc = new Vector2I(cJoueur.X + dx, cJoueur.Y + dz);
				if (!TryGetChunkDataPourCoordY(cc, cyJoueur, out var data) || data == null)
					continue;
				if (!data.VisualInstanceRID.IsValid || !EstDansCorridorMarche(data, posJoueur, velXZ))
					continue;
				if (!data.PhysicsBodyRID.IsValid || data.Dormant || data.EstEnFileSolidification)
					return true;
			}
		}
		return false;
	}

	/// <summary>Chunks manquants (pas de mesh) dans le cône de course : le joueur marche vers du vide.</summary>
	public bool CorridorStreamingEnRetard(Vector3 posJoueur, Vector3 velXZ)
	{
		if (velXZ.LengthSquared() < 0.25f)
			return false;

		Vector2 dir = new Vector2(velXZ.X, velXZ.Z);
		if (dir.LengthSquared() < 1e-6f)
			return false;
		dir = dir.Normalized();

		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int profondeur = Mathf.Clamp(RayonPrioriteCollisionJoueur + 3, 3, 6);
		for (int i = 1; i <= profondeur; i++)
		{
			Vector2I cc = new Vector2I(
				cJoueur.X + Mathf.RoundToInt(dir.X * i),
				cJoueur.Y + Mathf.RoundToInt(dir.Y * i));
			if (!ChunkDisponiblePourObservation(cc, posJoueur))
				return true;
		}
		return false;
	}

	/// <summary>Chunks visibles dans le corridor sans corps physique (solidification en retard).</summary>
	public bool CorridorSolidificationEnRetard(Vector3 posJoueur, Vector3 velXZ)
	{
		if (!EssayerObtenirJoueurDansArbre(out _))
			return false;

		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cyJoueur = ModeProfondeurTranchesActif()
			? CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y))
			: 0;
		int lyJoueur = ModeProfondeurTranchesActif()
			? ConstantesProfondeurVerticale.LocalYDepuisMondeY((int)Mathf.Floor(posJoueur.Y))
			: 0;
		int h = ConstantesProfondeurVerticale.HauteurTrancheMetres;
		bool procheJonction = ModeProfondeurTranchesActif()
			&& ConstantesProfondeurVerticale.EstProcheJonctionTranche(lyJoueur, h);
		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur + 1, 2, 4);

		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I cc = new Vector2I(cJoueur.X + dx, cJoueur.Y + dz);
				int[] tranches = procheJonction
					? new[] { cyJoueur - 1, cyJoueur, cyJoueur + 1 }
					: new[] { cyJoueur };
				foreach (int cy in tranches)
				{
					if (!TryGetChunkDataPourCoordY(cc, cy, out var data) || data == null)
						continue;
					if (!data.VisualInstanceRID.IsValid)
						continue;
					if (!EstDansCorridorMarche(data, posJoueur, velXZ))
						continue;
					if (!data.PhysicsBodyRID.IsValid || data.EstEnFileSolidification)
						return true;
				}
			}
		}
		return false;
	}

	/// <summary>Chunk dans le rayon prioritaire ou le cône avant le joueur (XZ).</summary>
	public bool DoitSolidifierALIntegration(ChunkData data, Vector3 posJoueur, Vector3 velXZ)
	{
		if (data == null || data._meshRef == null)
			return false;

		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int dx = Mathf.Abs(data.Coordonnees.X - cJoueur.X);
		int dz = Mathf.Abs(data.Coordonnees.Y - cJoueur.Y);

		if (ModeProfondeurTranchesActif())
		{
			int cyJoueur = CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y));
			if (Mathf.Abs(data.CoordChunkY - cyJoueur) > ConstantesProfondeurVerticale.DemiFenetreTranches)
				return false;
		}

		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur, 1, 3);
		if (dx <= rayon && dz <= rayon)
			return true;

		return EstDansCorridorMarche(data, posJoueur, velXZ);
	}

	private bool EstDansCorridorMarche(ChunkData data, Vector3 posJoueur, Vector3 velXZ)
	{
		if (velXZ.LengthSquared() < 0.25f)
			return false;

		float tc = TailleChunk;
		Vector2 centreChunk = new Vector2(
			data.Coordonnees.X * tc + tc * 0.5f,
			data.Coordonnees.Y * tc + tc * 0.5f);
		Vector2 posXZ = new Vector2(posJoueur.X, posJoueur.Z);
		Vector2 versChunk = centreChunk - posXZ;
		float dist = versChunk.Length();
		float vitesse = velXZ.Length();
		float portee = SecondesAnticipationCollision * vitesse + tc;
		if (dist > portee)
			return false;

		Vector2 dir = new Vector2(velXZ.X, velXZ.Z).Normalized();
		if (dist > 0.01f)
		{
			float cos = dir.Dot(versChunk / dist);
			if (cos < 0.52f)
				return false;
		}
		return true;
	}

	private int _solidificationsCorridorCetteFrame;

	internal void ReinitialiserCompteurSolidificationCorridorFrame()
		=> _solidificationsCorridorCetteFrame = 0;

	private const int PlafondSolidificationsCorridorParFrame = 6;

	/// <summary>Solidification synchrone à l'intégration si dans le corridor et sous le plafond frame.</summary>
	internal bool EssayerSolidifierCorridorAIntegration(ChunkData data, Vector3 posJoueur, Vector3 velXZ)
	{
		if (!DoitSolidifierALIntegration(data, posJoueur, velXZ))
			return false;
		if (_solidificationsCorridorCetteFrame >= PlafondSolidificationsCorridorParFrame)
			return false;

		World3D world = GetWorld3D();
		if (world == null)
			return false;

		RetirerDeFileSolidification(data);
		_setSolidificationUrgente.Remove(data);
		data.EstEnFileSolidification = false;

		if (!data.PhysicsBodyRID.IsValid)
		{
			AssurerCorpsPhysiqueChunk(data);
			_solidificationsCorridorCetteFrame++;
		}

		if (data.PhysicsBodyRID.IsValid)
		{
			PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, world.Space);
			data.Dormant = false;
			return true;
		}
		return false;
	}

	/// <summary>Mesh visible sans collision dans la fenêtre physique (tranche courante ±1) — ex. fond d'étang après la chute.</summary>
	internal void SolidifierVolumesVisiblesAutourJoueur(Vector3 posJoueur, float fpsMoyen = 60f)
	{
		if (!ModeProfondeurTranchesActif() || !EssayerObtenirJoueurDansArbre(out _))
			return;
		World3D world = GetWorld3D();
		if (world == null) return;

		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cy = CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y));
		int demiY = ConstantesProfondeurVerticale.DemiFenetrePhysiqueTranches;
		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur + 1, 2, 3);
		int traites = 0;
		int maxParFrame = fpsMoyen < 32f ? 1 : (fpsMoyen < 45f ? 2 : 3);

		for (int dy = -demiY; dy <= demiY && traites < maxParFrame; dy++)
		{
			for (int dx = -rayon; dx <= rayon && traites < maxParFrame; dx++)
			{
				for (int dz = -rayon; dz <= rayon && traites < maxParFrame; dz++)
				{
					Vector2I cc = new Vector2I(c.X + dx, c.Y + dz);
					if (!TryGetChunkDataPourCoordY(cc, cy + dy, out var data) || data == null)
						continue;
					if (!data.VisualInstanceRID.IsValid || data._meshRef == null)
						continue;
					float yBase = data.CoordChunkY * data.HauteurMax;
					if (posJoueur.Y < yBase - 12f || posJoueur.Y > yBase + data.HauteurMax + 12f)
						continue;
					if (data.PhysicsBodyRID.IsValid && !data.Dormant && !data.EstEnFileSolidification)
						continue;

					bool proche = dx * dx + dz * dz <= 4 && Mathf.Abs(dy) <= 1;
					if (proche && !data.PhysicsBodyRID.IsValid)
					{
						RetirerDeFileSolidification(data);
						data.EstEnFileSolidification = false;
						AssurerCorpsPhysiqueChunk(data);
						if (data.PhysicsBodyRID.IsValid)
						{
							PhysicsServer3D.Singleton.BodySetSpace(data.PhysicsBodyRID, world.Space);
							data.Dormant = false;
							traites++;
							continue;
						}
					}
					if (!data.EstEnFileSolidification)
					{
						EnfilerSolidificationUrgenteUnique(data);
						data.EstEnFileSolidification = true;
						traites++;
					}
				}
			}
		}
	}
}
