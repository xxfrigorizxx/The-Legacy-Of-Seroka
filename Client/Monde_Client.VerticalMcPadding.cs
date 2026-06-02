using Godot;

/// <summary>Padding MC vertical (tranches 100 m) et corridor de solidification / marche.</summary>
public partial class Monde_Client : Node3D
{
	/// <summary>Échantillonne un voxel local ou sur la tranche voisine (ly±1 hors limites).</summary>
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

		int h = data.HauteurMax;
		Vector2I coord = data.Coordonnees;
		int cy = data.CoordChunkY;
		ChunkData source = data;
		int lyLecture = ly;

		int yMonde = ConstantesProfondeurVerticale.MondeYDepuisLocal(cy, h, ly);
		const int niveauMer = ConstantesProfondeurVerticale.NiveauEauMondeAlpha;

		if (ly > h)
		{
			if (TryGetChunkDataPourCoordY(coord, cy + 1, out source) && source?.DensitiesFlat != null)
				lyLecture = 0;
			else
			{
				// Tranche du dessus pas encore chargée : prolonger ly=h (évite déchirure MC à Y=100).
				source = data;
				lyLecture = h;
				LireVoxelLocal(source, lx, lyLecture, lz, ref densite, ref eau, ref mat);
				if (densite <= 0f && ConstantesProfondeurVerticale.EstSousNiveauMer(yMonde, niveauMer)
					&& (cy + 1) * h <= niveauMer)
				{
					densite = -10f;
					eau = 1f;
					mat = 4;
				}
				return true;
			}
		}
		else if (ly < 0)
		{
			if (TryGetChunkDataPourCoordY(coord, cy - 1, out source) && source?.DensitiesFlat != null)
				lyLecture = h;
			else
			{
				source = data;
				lyLecture = 0;
				LireVoxelLocal(source, lx, lyLecture, lz, ref densite, ref eau, ref mat);
				if (densite <= 0f && ConstantesProfondeurVerticale.EstSousNiveauMer(yMonde, niveauMer))
				{
					densite = -10f;
					eau = 1f;
					mat = 4;
				}
				return true;
			}
		}

		if (lx < 0 || lx > source.TailleChunk || lyLecture < 0 || lyLecture > source.HauteurMax || lz < 0 || lz > source.TailleChunk)
			return true;

		LireVoxelLocal(source, lx, lyLecture, lz, ref densite, ref eau, ref mat);
		if (ly < 0 && eau <= 0f && lyLecture > 0
			&& source.DensitiesEauFlat != null
			&& source.DensitiesEauFlat[source.Idx(lx, lyLecture - 1, lz)] > 0f)
			LireVoxelLocal(source, lx, lyLecture - 1, lz, ref densite, ref eau, ref mat);
		return true;
	}

	private static void LireVoxelLocal(ChunkData data, int lx, int ly, int lz, ref float densite, ref float eau, ref byte mat)
	{
		densite = data.DensitiesFlat[data.Idx(lx, ly, lz)];
		mat = data.MaterialsFlat[data.Idx(lx, ly, lz)];
		if (data.DensitiesEauFlat != null)
			eau = data.DensitiesEauFlat[data.Idx(lx, ly, lz)];
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

	/// <summary>Chunks visibles dans le corridor sans corps physique (solidification en retard).</summary>
	public bool CorridorSolidificationEnRetard(Vector3 posJoueur, Vector3 velXZ)
	{
		if (!EssayerObtenirJoueurDansArbre(out _))
			return false;

		Vector2I cJoueur = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cyJoueur = ModeProfondeurTranchesActif()
			? CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y))
			: 0;
		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur + 1, 2, 4);

		for (int dx = -rayon; dx <= rayon; dx++)
		{
			for (int dz = -rayon; dz <= rayon; dz++)
			{
				Vector2I cc = new Vector2I(cJoueur.X + dx, cJoueur.Y + dz);
				int cy = ModeProfondeurTranchesActif() ? cyJoueur : 0;
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

	/// <summary>Mesh visible sans collision dans la fenêtre ±2 tranches (ex. fond d'étang après la chute).</summary>
	internal void SolidifierVolumesVisiblesAutourJoueur(Vector3 posJoueur)
	{
		if (!ModeProfondeurTranchesActif() || !EssayerObtenirJoueurDansArbre(out _))
			return;
		World3D world = GetWorld3D();
		if (world == null) return;

		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(posJoueur, TailleChunk);
		int cy = CoordYDepuisMondeY((int)Mathf.Floor(posJoueur.Y));
		int demiY = ConstantesProfondeurVerticale.DemiFenetreTranches;
		int rayon = Mathf.Clamp(RayonPrioriteCollisionJoueur + 1, 2, 3);
		int traites = 0;
		const int maxParFrame = 5;

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
