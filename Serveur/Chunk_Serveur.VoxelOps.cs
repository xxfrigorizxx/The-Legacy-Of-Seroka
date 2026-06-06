using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

public partial class Chunk_Serveur : RefCounted
{
	private bool EstSocleIntouchableLocal(int yLocal)
	{
		int yMonde = ChunkOffsetY * HauteurMax + yLocal;
		if (_profondeurEtendueActive)
			return yMonde <= _fondMondeY && yMonde >= _fondMondeY - 2;
		return yMonde >= 0 && yMonde <= 2;
	}

	public void DetruireVoxel(Vector3 pointImpactGlobal, float rayonExplosion, float forceDegats = 5.0f, Action<List<int>> onSectionsAffectees = null)
	{
		Vector3 pointLocal = pointImpactGlobal - PositionMonde;
		var positionsDetruites = new List<Vector3I>();

		// Destruction radiale : flore dans le rayon de la pioche (2 m) — atomiser AVANT de modifier la densité
		const float rayonDestructionFlore = 2.0f;
		var floreDetruite = new List<KeyValuePair<Vector3I, byte>>();
		foreach (var kv in InventaireFlore)
		{
			Vector3 posFlore = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);
			if (posFlore.DistanceTo(pointImpactGlobal) <= rayonDestructionFlore) floreDetruite.Add(kv);
		}
		foreach (var kv in floreDetruite)
		{
			InventaireFlore.Remove(kv.Key);
			Vector3 posSpawn = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);
			// Gazon (0) lâche la Fibre (15). Buissons (1,2) : 10, 11.
			byte idItem = kv.Value == FloreTypeGazon ? (byte)15 : (byte)(EstBuissonPlein(kv.Value) ? ID_ITEM_BUISSON_PLEIN : ID_ITEM_BUISSON_VIDE);
			_callbackBlocChutant?.Invoke(posSpawn, idItem, false, 0);
		}
		if (floreDetruite.Count > 0)
			_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));

		lock (_verrouVoxel)
		{
			float rayon2 = rayonExplosion * rayonExplosion;
			bool modifie = false;

			// IMPORTANT: on modifie uniquement le volume "réel" du chunk.
			// La couche de padding (x/z==TailleChunk, y==HauteurMax) doit rester dérivée des voisins
			// via la réplication, sinon on crée des déchirures visuelles sur les bords.
			for (int x = 0; x < TailleChunk; x++)
				for (int y = 0; y < HauteurMax; y++)
					for (int z = 0; z < TailleChunk; z++)
					{
						if (EstSocleIntouchableLocal(y)) continue;
						float dx = pointLocal.X - x, dy = pointLocal.Y - y, dz = pointLocal.Z - z;
						if (dx * dx + dy * dy + dz * dz <= rayon2)
						{
							bool etaitSolide = _densities[x, y, z] > Isolevel;
							// Gameplay: un minage validé doit produire un trou immédiatement visible.
							_densities[x, y, z] = -10.0f;
							_materials[x, y, z] = 0;
							modifie = true;
							if (etaitSolide) positionsDetruites.Add(new Vector3I(x, y, z));
						}
					}
			if (!modifie) return;
			_estModifie = true; // Joueur a miné → sauvegarde obligatoire au déchargement.
			_contenuChangeDepuisEnvoiClient = true;
			foreach (var pos in positionsDetruites) VerifierStabilite(pos);
		}

		int baseX = ChunkOffsetX * TailleChunk;
		int baseZ = ChunkOffsetZ * TailleChunk;

		foreach (var pos in positionsDetruites)
		{
			_reveillerEau?.Invoke(PositionMonde + new Vector3(pos.X, pos.Y, pos.Z));
			int gx = baseX + pos.X;
			int gy = ChunkOffsetY * HauteurMax + pos.Y;
			int gz = baseZ + pos.Z;
			var posGlobal = new Vector3I(gx, gy, gz);
			_onVoxelModifie?.Invoke(posGlobal, 0);
		}
		AuditerGraviteFlore();
	}

	public void CreerMatiere(Vector3 pointCibleGlobal, float rayon, byte idMatiere = 1, Action<List<int>> onSectionsAffectees = null)
	{
		Vector3 pointLocal = pointCibleGlobal - PositionMonde;
		var positionsModifiees = new List<Vector3I>();

		lock (_verrouVoxel)
		{
			float rayon2 = rayon * rayon;
			// Même règle qu'en destruction: ne pas écrire dans le padding de bord.
			for (int x = 0; x < TailleChunk; x++)
				for (int y = 0; y < HauteurMax; y++)
					for (int z = 0; z < TailleChunk; z++)
					{
						if (EstSocleIntouchableLocal(y)) continue;
						float dx = pointLocal.X - x, dy = pointLocal.Y - y, dz = pointLocal.Z - z;
						if (dx * dx + dy * dy + dz * dz <= rayon2)
						{
							// Gameplay: une pose validée doit créer de la matière immédiatement visible.
							_densities[x, y, z] = 10.0f;
							_materials[x, y, z] = idMatiere; // Injection couleur : le Shader lit ce tableau
							positionsModifiees.Add(new Vector3I(x, y, z));
						}
					}
			if (positionsModifiees.Count == 0) return;
			_estModifie = true; // Joueur a placé des blocs → sauvegarde obligatoire.
			_contenuChangeDepuisEnvoiClient = true;
		}

		foreach (var pos in positionsModifiees)
		{
			_reveillerEau?.Invoke(PositionMonde + new Vector3(pos.X, pos.Y, pos.Z));
			int gx = Mathf.FloorToInt(PositionMonde.X) + pos.X;
			int gy = ChunkOffsetY * HauteurMax + pos.Y;
			int gz = Mathf.FloorToInt(PositionMonde.Z) + pos.Z;
			var posGlobal = new Vector3I(gx, gy, gz);
			_onVoxelModifie?.Invoke(posGlobal, idMatiere);
		}
		AuditerGraviteFlore();
	}

	private List<int> ObtenirSectionsAffectees(List<Vector3I> positions)
	{
		const int HAUTEUR_SECTION = 16;
		int nbSections = ConstantesProfondeurVerticale.ObtenirNbSections(HauteurMax);
		var sections = new HashSet<int>();
		foreach (var pos in positions)
		{
			int idx = Mathf.FloorToInt(pos.Y / (float)HAUTEUR_SECTION);
			if (idx >= 0 && idx < nbSections) sections.Add(idx);
			// Frontière section : pas de modulo (en C# pos.Y % 16 peut être négatif). Même logique par soustraction.
			if (pos.Y > 0 && idx > 0 && pos.Y == idx * HAUTEUR_SECTION) sections.Add(idx - 1);
		}
		return new List<int>(sections);
	}

	private bool EstDansLimitesChunk(int x, int y, int z) =>
		x >= 0 && x <= TailleChunk && y >= 0 && y <= HauteurMax && z >= 0 && z <= TailleChunk;

	private Vector2I? ObtenirChunkVoisinSiHorsLimites(int x, int y, int z)
	{
		if (y < 0 || y > HauteurMax) return null;
		if (x >= 0 && x <= TailleChunk && z >= 0 && z <= TailleChunk) return null;
		int dx = x < 0 ? -1 : (x > TailleChunk ? 1 : 0);
		int dz = z < 0 ? -1 : (z > TailleChunk ? 1 : 0);
		return new Vector2I(ChunkOffsetX + dx, ChunkOffsetZ + dz);
	}

	private bool EstSolide(int x, int y, int z) =>
		EstDansLimitesChunk(x, y, z) && _densities[x, y, z] > Isolevel;

	/// <summary>Lecture directe de l'ADN : retourne l'ID matière exact du voxel (aligné avec le Shader). 1 si air ou hors limites. CÉCITÉ HYDRIQUE : jamais 4 (eau).</summary>
	public byte ObtenirMatiereAtLocal(int lx, int ly, int lz)
	{
		if (!EstDansLimitesChunk(lx, ly, lz) || _densities == null) return 1;
		lock (_verrouVoxel)
		{
			if (_densities[lx, ly, lz] <= Isolevel) return 1; // Air : fallback terre
			byte mat = _materials[lx, ly, lz];
			// L'EAU (ID 4) est un fluide géré ailleurs. Le Marching Cubes sous l'eau = SABLE ou TERRE. Jamais retourner 4.
			if (mat == 4) return 3; // Fond marin = Sable
			return mat;
		}
	}

	private static int ObtenirResistanceMateriau(byte id)
	{
		if (id == 3) return 0;
		if (id == 2) return 2;
		return 1;
	}

	private bool AUnSupport(int bx, int by, int bz, byte mat)
	{
		int r = ObtenirResistanceMateriau(mat);
		if (r == 0) return EstSolide(bx, by - 1, bz);
		for (int x = -r; x <= r; x++)
			for (int z = -r; z <= r; z++)
				if (Mathf.Abs(x) + Mathf.Abs(z) <= r &&
					EstSolide(bx + x, by, bz + z) && EstSolide(bx + x, by - 1, bz + z))
					return true;
		return false;
	}

	private void VerifierStabilite(Vector3I pos)
	{
		int xu = pos.X, yu = pos.Y + 1, zu = pos.Z;
		if (yu < 0 || yu > HauteurMax) return;
		if (!EstDansLimitesChunk(xu, yu, zu))
		{
			var v = ObtenirChunkVoisinSiHorsLimites(xu, yu, zu);
			if (v == null || _chunkEstCharge == null || !_chunkEstCharge(v.Value)) return;
			return;
		}
		if (!EstSolide(xu, yu, zu)) return;
		byte mat = _materials[xu, yu, zu];
		if (mat == 0) mat = 2;
		if (AUnSupport(xu, yu, zu, mat)) return;

		lock (_verrouVoxel)
		{
			_densities[xu, yu, zu] = -10.0f;
			if (_densitiesEau != null) _densitiesEau[xu, yu, zu] = -1.0f;
		}

		_reveillerEau?.Invoke(PositionMonde + new Vector3(xu, yu, zu));
		_callbackBlocChutant?.Invoke(PositionMonde + new Vector3(xu + 0.5f, yu + 0.5f, zu + 0.5f), mat, false, 0);

		AuditerGraviteFlore();
		VerifierStabilite(new Vector3I(xu, yu, zu));
		VerifierStabilite(new Vector3I(xu - 1, yu - 1, zu));
		VerifierStabilite(new Vector3I(xu + 1, yu - 1, zu));
		VerifierStabilite(new Vector3I(xu, yu - 1, zu - 1));
		VerifierStabilite(new Vector3I(xu, yu - 1, zu + 1));
	}

	public bool EstVoxelEau(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z)) return false;
		lock (_verrouVoxel) return _densitiesEau != null && _densitiesEau[x, y, z] > Isolevel;
	}

	public bool EstVoxelAir(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z)) return false;
		lock (_verrouVoxel)
		{
			bool sol = _densities[x, y, z] > Isolevel;
			bool eau = _densitiesEau != null && _densitiesEau[x, y, z] > Isolevel;
			return !sol && !eau;
		}
	}

	public bool EstVoxelSolide(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z)) return false;
		lock (_verrouVoxel) return _densities[x, y, z] > Isolevel;
	}

	/// <summary>Lecture brute d'un voxel local pour synchroniser les frontières inter-chunks (0=air, 4=eau, sinon matière solide).</summary>
	public byte LireVoxelLocalBrut(int lx, int ly, int lz)
	{
		if (!EstDansLimitesChunk(lx, ly, lz)) return 0;
		lock (_verrouVoxel)
		{
			bool eau = _densitiesEau != null && _densitiesEau[lx, ly, lz] > Isolevel;
			if (eau) return 4;
			bool solide = _densities[lx, ly, lz] > Isolevel;
			if (!solide) return 0;
			byte mat = _materials[lx, ly, lz];
			return mat == 0 ? (byte)1 : mat;
		}
	}

	public void DefinirVoxelEau(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z) || EstSocleIntouchableLocal(y)) return;
		bool modifie = false;
		lock (_verrouVoxel)
		{
			bool etaitEau = _densitiesEau != null && _densitiesEau[x, y, z] > Isolevel;
			bool dejaEau = etaitEau && _materials[x, y, z] == 4 && _densities[x, y, z] <= Isolevel;
			_densities[x, y, z] = -10.0f;
			_materials[x, y, z] = 4;
			if (_densitiesEau != null) _densitiesEau[x, y, z] = 1.0f;
			modifie = !dejaEau;
			if (modifie)
			{
				_estModifie = true;
				_contenuChangeDepuisEnvoiClient = true;
			}
		}
		if (modifie)
			AuditerGraviteFlore();
	}

	public void DefinirVoxelAir(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z)) return;
		bool modifie = false;
		lock (_verrouVoxel)
		{
			bool etaitEau = _densitiesEau != null && _densitiesEau[x, y, z] > Isolevel;
			bool etaitSolide = _densities[x, y, z] > Isolevel;
			bool dejaAir = !etaitEau && !etaitSolide && _materials[x, y, z] == 0;
			_densities[x, y, z] = -10.0f;
			_materials[x, y, z] = 0;
			if (_densitiesEau != null) _densitiesEau[x, y, z] = -1.0f;
			modifie = !dejaAir;
			if (modifie)
			{
				_estModifie = true;
				_contenuChangeDepuisEnvoiClient = true;
			}
		}
		if (modifie)
			AuditerGraviteFlore();
	}

	/// <summary>Met à jour un voxel aux coords locales (réplication du padding des voisins).</summary>
	public void SetVoxelLocal(int lx, int ly, int lz, byte id, bool marquerChunkModifie = true)
	{
		if (!EstDansLimitesChunk(lx, ly, lz)) return;
		lock (_verrouVoxel)
		{
			if (id == 0)
			{
				_densities[lx, ly, lz] = -10.0f;
				_materials[lx, ly, lz] = 0;
				if (_densitiesEau != null) _densitiesEau[lx, ly, lz] = -1.0f;
			}
			else if (id == 4)
			{
				_densities[lx, ly, lz] = -10.0f;
				_materials[lx, ly, lz] = 4;
				if (_densitiesEau != null) _densitiesEau[lx, ly, lz] = 1.0f;
			}
			else
			{
				_densities[lx, ly, lz] = (id == 30) ? 50.0f : 10.0f; // Le bois (ID 30) a 50 HP !
				_materials[lx, ly, lz] = id;
				if (_densitiesEau != null) _densitiesEau[lx, ly, lz] = -1.0f;
			}
		}
		// Padding voisin répliqué = vraie mutation persistante (sinon trou bordure perdu au reload).
		if (marquerChunkModifie)
		{
			_estModifie = true;
			_contenuChangeDepuisEnvoiClient = true;
			AuditerGraviteFlore();
		}
	}

	/// <summary>Met à jour un voxel local ET notifie le client (croissance arbres).</summary>
	public void ModifierVoxelEtNotifier(int lx, int ly, int lz, byte id)
	{
		SetVoxelLocal(lx, ly, lz, id);
		_estModifie = true;
		_contenuChangeDepuisEnvoiClient = true;
		int gy = ChunkOffsetY * HauteurMax + ly;
		var posGlobal = new Vector3I(ChunkOffsetX * TailleChunk + lx, gy, ChunkOffsetZ * TailleChunk + lz);
		_onVoxelModifie?.Invoke(posGlobal, id);
	}

	public bool FaucherFlore(Vector3 pointImpactGlobal, float rayon)
	{
		return FaucherFloreInterne(pointImpactGlobal, rayon, true);
	}

	public bool FaucherFloreSansLoot(Vector3 pointImpactGlobal, float rayon)
	{
		return FaucherFloreInterne(pointImpactGlobal, rayon, false);
	}

	public bool ExisteGazonDansRayon(Vector3 pointImpactGlobal, float rayon)
	{
		float rayon2 = rayon * rayon;
		const float demiEpaisseurVerticale = 5f;
		foreach (var kv in InventaireFlore)
		{
			if (kv.Value != FloreTypeGazon)
				continue;
			float dx = (kv.Key.X + 0.5f) - pointImpactGlobal.X;
			float dz = (kv.Key.Z + 0.5f) - pointImpactGlobal.Z;
			if (dx * dx + dz * dz > rayon2)
				continue;
			float dy = Mathf.Abs((kv.Key.Y + 0.5f) - pointImpactGlobal.Y);
			if (dy > demiEpaisseurVerticale)
				continue;
			return true;
		}
		return false;
	}

	private bool FaucherFloreInterne(Vector3 pointImpactGlobal, float rayon, bool creerLoot)
	{
		float rayon2 = rayon * rayon;
		const float demiEpaisseurVerticale = 5f; // Le raycast sol peut avoir un Y légèrement différent du voxel surface → la 3D pure ratée trop souvent.
		var floreDetruite = new List<KeyValuePair<Vector3I, byte>>();

		foreach (var kv in InventaireFlore)
		{
			if (kv.Value != FloreTypeGazon)
				continue;
			float dx = (kv.Key.X + 0.5f) - pointImpactGlobal.X;
			float dz = (kv.Key.Z + 0.5f) - pointImpactGlobal.Z;
			if (dx * dx + dz * dz > rayon2)
				continue;
			float dy = Mathf.Abs((kv.Key.Y + 0.5f) - pointImpactGlobal.Y);
			if (dy > demiEpaisseurVerticale)
				continue;
			floreDetruite.Add(kv);
		}
		if (floreDetruite.Count == 0) return false;

		foreach (var kv in floreDetruite)
		{
			InventaireFlore.Remove(kv.Key);
			if (creerLoot)
			{
				Vector3 posSpawn = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);
				_callbackBlocChutant?.Invoke(posSpawn, 15, false, 0);
			}
		}
		_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));
		return true;
	}
}
