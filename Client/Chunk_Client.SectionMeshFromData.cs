using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Chunk_Client : Node3D
{
	/// <summary>Échantillon voxel avec extension verticale (tranche voisine coordY±1).</summary>
	public delegate bool EchantillonnerVoxelChunkDelegate(ChunkData owner, int lx, int ly, int lz, out float densite, out float eau, out byte mat);

	private static int CompterSommetsSection(SectionPayload payload)
		=> payload?.SommetsVisuels?.Length ?? 0;

	/// <summary>
	/// Construit la shape de collision (trimesh + BVH) à partir des payloads, sur le thread de fond.
	/// Le coûteux build BVH (SetFaces) est ainsi sorti du thread principal (anti-pic streaming).
	/// Sommets en espace LOCAL (CAS B) — le body applique l'origine du chunk, comme le visuel.
	/// </summary>
	public static ConcavePolygonShape3D ConstruireShapeCollisionDepuisPayloads(List<SectionPayload> payloads)
	{
		if (payloads == null)
			return null;
		int total = 0;
		foreach (var p in payloads)
			if (p?.SommetsVisuels != null)
				total += p.SommetsVisuels.Length;
		total -= total % 3;
		if (total < 3)
			return null;
		var faces = new Vector3[total];
		int offset = 0;
		foreach (var p in payloads)
		{
			if (p?.SommetsVisuels == null || p.SommetsVisuels.Length == 0)
				continue;
			int count = p.SommetsVisuels.Length;
			if (offset + count > total)
				count = total - offset;
			if (count <= 0)
				break;
			Array.Copy(p.SommetsVisuels, 0, faces, offset, count);
			offset += count;
		}
		try
		{
			var shape = new ConcavePolygonShape3D();
			shape.SetFaces(faces); // Build BVH ici, sur le thread de fond.
			return shape;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Padding MC vertical (ly&lt;0 / ly&gt;h) : seulement sections bas/haut de tranche.</summary>
	private static bool SectionUtilisePaddingVertical(int indexSection, int nbSections)
		=> indexSection <= 0 || indexSection >= nbSections - 1;

	/// <summary>Reconstruit uniquement les sections listées ; réutilise le cache pour le reste.</summary>
	public static List<SectionPayload> ReconstruireSectionsDepuisData(
		ChunkData data,
		IReadOnlyCollection<int> sectionsAReconstruire,
		EchantillonnerVoxelChunkDelegate echantillonner = null)
	{
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null || sectionsAReconstruire == null || sectionsAReconstruire.Count == 0)
			return null;
		int nbSections = ObtenirNbSectionsEffectif(data.HauteurMax);
		List<SectionPayload> payloads;
		if (data.CachePayloadsSections != null && data.CachePayloadsSections.Count == nbSections)
			payloads = new List<SectionPayload>(data.CachePayloadsSections);
		else
			payloads = ReconstruirePayloadsDepuisData(data, echantillonner);
		if (payloads == null || payloads.Count != nbSections)
			return payloads;
		float baseX = data.Coordonnees.X * (float)data.TailleChunk;
		float baseZ = data.Coordonnees.Y * (float)data.TailleChunk;
		foreach (int sec in sectionsAReconstruire)
		{
			if (sec < 0 || sec >= nbSections) continue;
			var echSec = SectionUtilisePaddingVertical(sec, nbSections) ? echantillonner : null;
			var nouveau = ConstruireSectionPayloadEnBackgroundFromData(data, sec, baseX, baseZ, echSec);
			int sommetsAnciens = CompterSommetsSection(payloads[sec]);
			int sommetsNouveaux = CompterSommetsSection(nouveau);
			if (sommetsNouveaux == 0 && sommetsAnciens > 0)
				continue;
			// Effondrement massif (voisin absent) : conserver la section précédente.
			if (sommetsAnciens >= 256 && sommetsNouveaux < sommetsAnciens / 8)
				continue;
			payloads[sec] = nouveau;
		}
		return payloads;
	}

	/// <summary>Reconstruit les 45 SectionPayload à partir d'un ChunkData déjà rempli (minage/pose). Pour mise à jour visuelle après AppliquerVoxel.</summary>
	public static List<SectionPayload> ReconstruirePayloadsDepuisData(ChunkData data, EchantillonnerVoxelChunkDelegate echantillonner = null)
	{
		if (data?.DensitiesFlat == null || data.MaterialsFlat == null) return null;
		float baseX = data.Coordonnees.X * (float)data.TailleChunk;
		float baseZ = data.Coordonnees.Y * (float)data.TailleChunk;
		int nbSections = ObtenirNbSectionsEffectif(data.HauteurMax);
		var payloads = new List<SectionPayload>(nbSections);
		for (int i = 0; i < nbSections; i++)
		{
			var echSec = SectionUtilisePaddingVertical(i, nbSections) ? echantillonner : null;
			payloads.Add(ConstruireSectionPayloadEnBackgroundFromData(data, i, baseX, baseZ, echSec));
		}
		return payloads;
	}

	private const int TAILLE_MAX_SECTION_DATA = 17 * 17 * 17;
	private const float IsolevelData = 0.0f;

	/// <summary>Construit un SectionPayload à partir de ChunkData (sans Node). Utilise les tableaux plats et le bruit du data.</summary>
	private static SectionPayload ConstruireSectionPayloadEnBackgroundFromData(
		ChunkData data, int indexSection, float baseX, float baseZ, EchantillonnerVoxelChunkDelegate echantillonner = null)
	{
		// CAS B : tous les sommets sont en ESPACE LOCAL (x,z dans [0, TailleChunk]). Le placement
		// de l'instance (Monde_Client.IntegrerChunkDataRIDs) applique UNE SEULE FOIS (cx*TailleChunk, 0, cz*TailleChunk).
		const int hauteurSection = 16;
		int yDebut = indexSection * hauteurSection;
		int yFin = Math.Min(yDebut + hauteurSection, data.HauteurMax);
		int tailleY = yFin - yDebut + 1;
		// Padding MC vers tranche coordY±1 (jonction Y=100,200…) : indispensable pour ne pas déchirer le voile.
		bool padHautMc = echantillonner != null && yFin >= data.HauteurMax;
		bool padBasMc = echantillonner != null && yDebut == 0
			&& data.CoordChunkY > ConstantesProfondeurVerticale.CoordYDepuisMondeY(-1200);
		int padBas = padBasMc ? 1 : 0;
		int padHaut = padHautMc ? 1 : 0;
		int tailleYBuffer = tailleY + padBas + padHaut;
		int yDebutBuffer = yDebut - padBas;
		int nbCubesY = tailleYBuffer - 1;
		int tc = data.TailleChunk;
		int tx = tc + 1, tz = tc + 1;

		var bufferDensities = ArrayPool<float>.Shared.Rent(TAILLE_MAX_SECTION_DATA);
		var bufferMaterials = ArrayPool<byte>.Shared.Rent(TAILLE_MAX_SECTION_DATA);
		float[] bufferEau = data.DensitiesEauFlat != null ? ArrayPool<float>.Shared.Rent(TAILLE_MAX_SECTION_DATA) : null;

		void RemplirCelluleBuffer(int x, int yBuf, int z, int lyGlobale)
		{
			int idx = x * (tailleYBuffer * tz) + yBuf * tz + z;
			if (echantillonner != null && (lyGlobale < 0 || lyGlobale > data.HauteurMax))
			{
				echantillonner(data, x, lyGlobale, z, out float d, out float e, out byte m);
				bufferDensities[idx] = d;
				bufferMaterials[idx] = m;
				if (bufferEau != null) bufferEau[idx] = e;
				return;
			}
			if (lyGlobale < 0 || lyGlobale > data.HauteurMax)
			{
				bufferDensities[idx] = -10f;
				bufferMaterials[idx] = 0;
				if (bufferEau != null) bufferEau[idx] = -1f;
				return;
			}
			bufferDensities[idx] = data.DensitiesFlat[data.Idx(x, lyGlobale, z)];
			bufferMaterials[idx] = data.MaterialsFlat[data.Idx(x, lyGlobale, z)];
			if (bufferEau != null) bufferEau[idx] = data.DensitiesEauFlat[data.Idx(x, lyGlobale, z)];
		}
		var vertsT = new List<Vector3>(8192);
		var normsT = new List<Vector3>(8192);
		var colsT = new List<Color>(8192);
		List<Vector3> vertsE = bufferEau != null ? new List<Vector3>(4096) : null;
		List<Vector3> normsE = bufferEau != null ? new List<Vector3>(4096) : null;
		if (_valsRecyclables == null) _valsRecyclables = new float[8];
		if (_vertsRecyclables == null) _vertsRecyclables = new Vector3[8];
		if (_vertListRecyclables == null) _vertListRecyclables = new Vector3[12];
		if (_matsRecyclables == null) _matsRecyclables = new byte[8];
		float[] vals = _valsRecyclables;
		Vector3[] verts = _vertsRecyclables;
		Vector3[] vertList = _vertListRecyclables;

		try
		{
			int stride = tailleYBuffer * tz;
			int nbVoxels = stride * tx;
			for (int x = 0; x < tx; x++)
				for (int y = 0; y < tailleYBuffer; y++)
					for (int z = 0; z < tz; z++)
						RemplirCelluleBuffer(x, y, z, yDebutBuffer + y);

			bool sectionVide = true;
			for (int i = 0; i < nbVoxels; i++)
			{
				if (bufferDensities[i] > IsolevelData) { sectionVide = false; break; }
			}
			if (sectionVide && bufferEau != null)
			{
				for (int i = 0; i < nbVoxels; i++)
				{
					if (bufferEau[i] > IsolevelData) { sectionVide = false; break; }
				}
			}
			if (sectionVide)
				return new SectionPayload();

			float ValD(int x, int y, int z) => bufferDensities[x * stride + y * tz + z];
			byte MatD(int x, int y, int z) => bufferMaterials[x * stride + y * tz + z];
			float EauD(int x, int y, int z) => bufferEau[x * stride + y * tz + z];

			var edgeTable = ConstantesMarchingCubes.EdgeTable;
			var triTable = ConstantesMarchingCubes.TriTable;
			var noiseT = data.NoiseTemperature;
			byte[] mats = _matsRecyclables;

			for (int x = 0; x < tc; x++)
				for (int y = 0; y < nbCubesY; y++)
				{
					int yG = yDebutBuffer + y;
					for (int z = 0; z < tc; z++)
					{
						verts[0] = new Vector3(x, yG, z);
						verts[1] = new Vector3(x + 1, yG, z);
						verts[2] = new Vector3(x + 1, yG + 1, z);
						verts[3] = new Vector3(x, yG + 1, z);
						verts[4] = new Vector3(x, yG, z + 1);
						verts[5] = new Vector3(x + 1, yG, z + 1);
						verts[6] = new Vector3(x + 1, yG + 1, z + 1);
						verts[7] = new Vector3(x, yG + 1, z + 1);
						vals[0] = ValD(x, y, z); vals[1] = ValD(x + 1, y, z); vals[2] = ValD(x + 1, y + 1, z); vals[3] = ValD(x, y + 1, z);
						vals[4] = ValD(x, y, z + 1); vals[5] = ValD(x + 1, y, z + 1); vals[6] = ValD(x + 1, y + 1, z + 1); vals[7] = ValD(x, y + 1, z + 1);
						int cubeIndex = 0;
						for (int i = 0; i < 8; i++)
							if (vals[i] > IsolevelData) cubeIndex |= 1 << i;
						if (edgeTable[cubeIndex] == 0) continue;
						vertList[0] = Interp(verts[0], verts[1], vals[0], vals[1]);
						vertList[1] = Interp(verts[1], verts[2], vals[1], vals[2]);
						vertList[2] = Interp(verts[2], verts[3], vals[2], vals[3]);
						vertList[3] = Interp(verts[3], verts[0], vals[3], vals[0]);
						vertList[4] = Interp(verts[4], verts[5], vals[4], vals[5]);
						vertList[5] = Interp(verts[5], verts[6], vals[5], vals[6]);
						vertList[6] = Interp(verts[6], verts[7], vals[6], vals[7]);
						vertList[7] = Interp(verts[7], verts[4], vals[7], vals[4]);
						vertList[8] = Interp(verts[0], verts[4], vals[0], vals[4]);
						vertList[9] = Interp(verts[1], verts[5], vals[1], vals[5]);
						vertList[10] = Interp(verts[2], verts[6], vals[2], vals[6]);
						vertList[11] = Interp(verts[3], verts[7], vals[3], vals[7]);
						// LECTURE DES 8 ATOMES DU CUBE — texture du coin solide (évite herbe sur murs/arbres)
						mats[0] = MatD(x, y, z); mats[1] = MatD(x + 1, y, z);
						mats[2] = MatD(x + 1, y + 1, z); mats[3] = MatD(x, y + 1, z);
						mats[4] = MatD(x, y, z + 1); mats[5] = MatD(x + 1, y, z + 1);
						mats[6] = MatD(x + 1, y + 1, z + 1); mats[7] = MatD(x, y + 1, z + 1);
						byte idMat = 0;
						for (int i = 0; i < 8; i++)
						{
							if (vals[i] > IsolevelData && mats[i] > 0)
							{
								idMat = mats[i];
								break;
							}
						}
						if (idMat == 0) idMat = 2;
						float xGlobal = baseX + x, zGlobal = baseZ + z;
						float temp = noiseT?.GetNoise2D(xGlobal, zGlobal) ?? 0f;
						float hum = data.NoiseHumidite != null ? CalculerHumiditeGlobaleDepuisChunkData(data, xGlobal, zGlobal) : 0f;
						Color couleurId = new Color(idMat / 255f, (temp + 1f) * 0.5f, (hum + 1f) * 0.5f, 1f);
						for (int i = 0; triTable[cubeIndex, i] != -1; i += 3)
						{
							Vector3 v0 = vertList[triTable[cubeIndex, i]], v1 = vertList[triTable[cubeIndex, i + 1]], v2 = vertList[triTable[cubeIndex, i + 2]];
							Vector3 n = (v1 - v0).Cross(v2 - v0).Normalized();
							vertsT.Add(v0); vertsT.Add(v1); vertsT.Add(v2);
							normsT.Add(n); normsT.Add(n); normsT.Add(n);
							colsT.Add(couleurId); colsT.Add(couleurId); colsT.Add(couleurId);
						}
					}
				}

			if (bufferEau != null)
			{
				if (_valsEauRecyclables == null) _valsEauRecyclables = new float[8];
				if (_vertsEauRecyclables == null) _vertsEauRecyclables = new Vector3[8];
				float[] valsEau = _valsEauRecyclables;
				Vector3[] vertsEau = _vertsEauRecyclables;
				for (int x = 0; x < tc; x++)
					for (int y = 0; y < nbCubesY; y++)
					{
						int yG = yDebutBuffer + y;
						if (echantillonner != null
							&& ConstantesProfondeurVerticale.CorpsOmetMaillageEauALaJonction(
								data.CoordChunkY, data.HauteurMax, yG))
							continue;
						for (int z = 0; z < tc; z++)
						{
							vertsEau[0] = new Vector3(x, yG, z);
							vertsEau[1] = new Vector3(x + 1, yG, z);
							vertsEau[2] = new Vector3(x + 1, yG + 1, z);
							vertsEau[3] = new Vector3(x, yG + 1, z);
							vertsEau[4] = new Vector3(x, yG, z + 1);
							vertsEau[5] = new Vector3(x + 1, yG, z + 1);
							vertsEau[6] = new Vector3(x + 1, yG + 1, z + 1);
							vertsEau[7] = new Vector3(x, yG + 1, z + 1);
							valsEau[0] = EauD(x, y, z); valsEau[1] = EauD(x + 1, y, z); valsEau[2] = EauD(x + 1, y + 1, z); valsEau[3] = EauD(x, y + 1, z);
							valsEau[4] = EauD(x, y, z + 1); valsEau[5] = EauD(x + 1, y, z + 1); valsEau[6] = EauD(x + 1, y + 1, z + 1); valsEau[7] = EauD(x, y + 1, z + 1);
							int ci = 0;
							for (int i = 0; i < 8; i++)
								if (valsEau[i] > IsolevelData) ci |= 1 << i;
							if (edgeTable[ci] == 0) continue;
							vertList[0] = Interp(vertsEau[0], vertsEau[1], valsEau[0], valsEau[1]);
							vertList[1] = Interp(vertsEau[1], vertsEau[2], valsEau[1], valsEau[2]);
							vertList[2] = Interp(vertsEau[2], vertsEau[3], valsEau[2], valsEau[3]);
							vertList[3] = Interp(vertsEau[3], vertsEau[0], valsEau[3], valsEau[0]);
							vertList[4] = Interp(vertsEau[4], vertsEau[5], valsEau[4], valsEau[5]);
							vertList[5] = Interp(vertsEau[5], vertsEau[6], valsEau[5], valsEau[6]);
							vertList[6] = Interp(vertsEau[6], vertsEau[7], valsEau[6], valsEau[7]);
							vertList[7] = Interp(vertsEau[7], vertsEau[4], valsEau[7], valsEau[4]);
							vertList[8] = Interp(vertsEau[0], vertsEau[4], valsEau[0], valsEau[4]);
							vertList[9] = Interp(vertsEau[1], vertsEau[5], valsEau[1], valsEau[5]);
							vertList[10] = Interp(vertsEau[2], vertsEau[6], valsEau[2], valsEau[6]);
							vertList[11] = Interp(vertsEau[3], vertsEau[7], valsEau[3], valsEau[7]);
							for (int i = 0; triTable[ci, i] != -1; i += 3)
							{
								Vector3 v0 = vertList[triTable[ci, i]], v1 = vertList[triTable[ci, i + 1]], v2 = vertList[triTable[ci, i + 2]];
								Vector3 n = (v1 - v0).Cross(v2 - v0).Normalized();
								vertsE.Add(v0); vertsE.Add(v1); vertsE.Add(v2);
								normsE.Add(n); normsE.Add(n); normsE.Add(n);
							}
						}
					}
			}
		}
		finally
		{
			ArrayPool<float>.Shared.Return(bufferDensities);
			ArrayPool<byte>.Shared.Return(bufferMaterials);
			if (bufferEau != null) ArrayPool<float>.Shared.Return(bufferEau);
		}

		TronquerSommetsSiResteNonTriplet(vertsT, normsT, colsT);
		if (vertsE != null) TronquerEauSiResteNonTriplet(vertsE, normsE);
		return new SectionPayload
		{
			SommetsVisuels = vertsT.ToArray(),
			NormalsVisuels = normsT.ToArray(),
			CouleursVisuels = colsT.ToArray(),
			SommetsEau = vertsE?.Count > 0 ? vertsE.ToArray() : null,
			NormalsEau = normsE?.Count > 0 ? normsE.ToArray() : null
		};
	}

	private static Vector3 Interp(Vector3 p1, Vector3 p2, float v1, float v2)
	{
		if (Mathf.Abs(Isolevel - v1) < 0.00001f) return p1;
		if (Mathf.Abs(Isolevel - v2) < 0.00001f) return p2;
		if (Mathf.Abs(v1 - v2) < 0.00001f) return p1;
		float t = (Isolevel - v1) / (v2 - v1);
		return p1 + t * (p2 - p1);
	}
}
