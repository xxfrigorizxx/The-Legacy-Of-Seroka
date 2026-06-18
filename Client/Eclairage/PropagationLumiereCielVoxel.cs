using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Propagation de lumière ciel (skylight) sur la grille voxel — style Minecraft / jeux AAA voxel.
/// Indépendante de la caméra : la lumière dépend uniquement de la géométrie du monde.
///
/// DORMANT : non appelé actuellement. L'éclairage du terrain utilise le PBR natif de Godot
/// (soleil DirectionalLight3D + ombres shadow map). Conservé pour une future modulation de
/// l'AMBIANCE en grotte (occlusion d'ambiance) ou la lumière de bloc des torches — surtout
/// PAS pour bloquer le soleil direct (ce qui rendait l'éclairage fragile et dépendant du voxel).
/// </summary>
public static class PropagationLumiereCielVoxel
{
	public const int NiveauMax = 15;
	private const float Isolevel = 0f;
	private const float DecalageSondeSurface = 0.62f;

	[ThreadStatic] private static Queue<int> _filePropagation;
	[ThreadStatic] private static bool[] _visitePropagation;

	/// <summary>Recalcule <see cref="ChunkData.SkylightFlat"/> (0..15) pour un chunk.</summary>
	public static void Recalculer(ChunkData data, Chunk_Client.EchantillonnerVoxelChunkDelegate echantillonner = null)
	{
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null)
			return;

		int tx = data.Tx > 0 ? data.Tx : data.TailleChunk + 1;
		int ty = data.Ty > 0 ? data.Ty : data.HauteurMax + 1;
		int tz = data.Tz > 0 ? data.Tz : data.TailleChunk + 1;
		int total = tx * ty * tz;
		if (total <= 0)
			return;

		if (data.SkylightFlat == null || data.SkylightFlat.Length != total)
			data.SkylightFlat = new byte[total];
		else
			Array.Clear(data.SkylightFlat, 0, total);

		// Phase 1 — colonnes : lumière directe du ciel (descente verticale, cellules air seulement).
		for (int x = 0; x < tx; x++)
		{
			for (int z = 0; z < tz; z++)
			{
				int niveauColonne = CalculerNiveauCielEntrant(data, x, z, echantillonner);
				for (int y = data.HauteurMax; y >= 0; y--)
				{
					int idx = data.Idx(x, y, z);
					if (EstOpaque(data, x, y, z, echantillonner))
					{
						niveauColonne = 0;
						continue;
					}
					data.SkylightFlat[idx] = (byte)Mathf.Clamp(niveauColonne, 0, NiveauMax);
				}
			}
		}

		// Phase 2 — propagation horizontale (entrée de lumière dans les grottes).
		_filePropagation ??= new Queue<int>(4096);
		int capacite = Math.Max(total, 4096);
		if (_visitePropagation == null || _visitePropagation.Length < capacite)
			_visitePropagation = new bool[capacite];
		else
			Array.Clear(_visitePropagation, 0, capacite);

		Queue<int> file = _filePropagation;
		file.Clear();

		for (int i = 0; i < total; i++)
		{
			if (data.SkylightFlat[i] <= 1)
				continue;
			int z = i % tz;
			int y = (i / tz) % ty;
			int x = i / (ty * tz);
			if (EstOpaque(data, x, y, z, echantillonner))
				continue;
			file.Enqueue(i);
			_visitePropagation[i] = true;
		}

		ReadOnlySpan<int> dx = stackalloc int[] { 1, -1, 0, 0, 0, 0 };
		ReadOnlySpan<int> dy = stackalloc int[] { 0, 0, 1, -1, 0, 0 };
		ReadOnlySpan<int> dz = stackalloc int[] { 0, 0, 0, 0, 1, -1 };

		while (file.Count > 0)
		{
			int idx = file.Dequeue();
			int z = idx % tz;
			int y = (idx / tz) % ty;
			int x = idx / (ty * tz);
			int niveau = data.SkylightFlat[idx];
			if (niveau <= 1)
				continue;

			for (int d = 0; d < 6; d++)
			{
				int nx = x + dx[d];
				int ny = y + dy[d];
				int nz = z + dz[d];
				if (EstOpaque(data, nx, ny, nz, echantillonner))
					continue;
				if (nx < 0 || nx >= tx || ny < 0 || ny >= ty || nz < 0 || nz >= tz)
					continue;

				int nIdx = data.Idx(nx, ny, nz);
				int nouveau = niveau - 1;
				if (nouveau <= data.SkylightFlat[nIdx])
					continue;

				data.SkylightFlat[nIdx] = (byte)nouveau;
				if (!_visitePropagation[nIdx])
				{
					_visitePropagation[nIdx] = true;
					file.Enqueue(nIdx);
				}
			}
		}
	}

	/// <summary>Skylight [0,1] à une cellule air de la grille.</summary>
	public static float EchantillonnerSkylightNormalise(ChunkData data, float lx, float ly, float lz)
	{
		if (data?.SkylightFlat == null)
			return 0f;
		int x = Mathf.Clamp(Mathf.RoundToInt(lx), 0, data.TailleChunk);
		int y = Mathf.Clamp(Mathf.RoundToInt(ly), 0, data.HauteurMax);
		int z = Mathf.Clamp(Mathf.RoundToInt(lz), 0, data.TailleChunk);
		return data.SkylightFlat[data.Idx(x, y, z)] / (float)NiveauMax;
	}

	/// <summary>
	/// Skylight pour un sommet de mesh MC (sur la frontière solide/air).
	/// Sonde du côté air le long de la normale — pas dans la roche.
	/// </summary>
	public static float EchantillonnerSkylightSurface(ChunkData data, Vector3 posLocal, Vector3 normalLocal)
	{
		if (data?.SkylightFlat == null)
			return 0f;

		Vector3 n = normalLocal.LengthSquared() > 1e-6f ? normalLocal.Normalized() : Vector3.Up;
		Vector3 probe = posLocal + n * DecalageSondeSurface;
		float sky = EchantillonnerSkylightNormalise(data, probe.X, probe.Y, probe.Z);

		// Paroi de grotte : si la sonde tombe encore dans la roche, essayer les 6 directions.
		if (sky > 0.02f)
			return sky;

		float best = 0f;
		ReadOnlySpan<Vector3> axes = stackalloc Vector3[]
		{
			Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Forward, Vector3.Back
		};
		for (int i = 0; i < axes.Length; i++)
		{
			Vector3 p = posLocal + axes[i] * DecalageSondeSurface;
			best = Mathf.Max(best, EchantillonnerSkylightNormalise(data, p.X, p.Y, p.Z));
		}
		return best;
	}

	private static int CalculerNiveauCielEntrant(
		ChunkData data, int x, int z, Chunk_Client.EchantillonnerVoxelChunkDelegate echantillonner)
	{
		if (!EstOpaque(data, x, data.HauteurMax, z, echantillonner))
			return NiveauMax;

		if (echantillonner == null)
			return 0;

		int ly = data.HauteurMax + 1;
		const int limiteRechercheCiel = 320;
		for (int i = 0; i < limiteRechercheCiel; i++, ly++)
		{
			if (EstOpaque(data, x, ly, z, echantillonner))
				return 0;
		}
		return NiveauMax;
	}

	/// <summary>Voxel solide = bloque la lumière. Hors monde / hors chunk sans voisin = bloqué (conservateur).</summary>
	private static bool EstOpaque(
		ChunkData data, int lx, int ly, int lz, Chunk_Client.EchantillonnerVoxelChunkDelegate echantillonner)
	{
		if (echantillonner != null)
		{
			if (echantillonner(data, lx, ly, lz, out float densite, out _, out _))
				return densite > Isolevel;
			return true;
		}

		int tc = data.TailleChunk;
		int h = data.HauteurMax;
		if (lx < 0 || lz < 0 || lx > tc || lz > tc || ly < 0 || ly > h)
			return true;

		return data.DensitiesFlat[data.Idx(lx, ly, lz)] > Isolevel;
	}
}
