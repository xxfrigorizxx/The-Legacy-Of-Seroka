using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Chunk_Client : Node3D
{
	private (ArrayMesh terrain, ArrayMesh eau) ConstruireMeshSection(int indexSection, float baseX, float baseZ)
	{
		int yDebut = indexSection * HAUTEUR_SECTION;
		int yFin = Math.Min(yDebut + HAUTEUR_SECTION, HauteurMax);
		int tailleY = yFin - yDebut + 1;
		int tc = TailleChunk;
		int tx = tc + 1, tz = tc + 1;

		// Rembourrage 17³ : tableau padded, aucune interrogation voisin. Lookup local pur.
		float DensitePourMesh(int x, int y, int z) => _densitiesFlat[Idx(x, yDebut + y, z)];

		if (_valsRecyclables == null) _valsRecyclables = new float[8];
		if (_vertsRecyclables == null) _vertsRecyclables = new Vector3[8];
		if (_vertListRecyclables == null) _vertListRecyclables = new Vector3[12];

		var bufferDensities = ArrayPool<float>.Shared.Rent(TAILLE_MAX_SECTION);
		var bufferMaterials = ArrayPool<byte>.Shared.Rent(TAILLE_MAX_SECTION);
		float[] bufferEau = _densitiesEauFlat != null ? ArrayPool<float>.Shared.Rent(TAILLE_MAX_SECTION) : null;
		ArrayMesh meshTerrain = null;
		ArrayMesh meshEau = null;
		try
		{
		int stride = tailleY * tz;
		for (int x = 0; x < tx; x++)
			for (int y = 0; y < tailleY; y++)
				for (int z = 0; z < tz; z++)
				{
					int idx = x * stride + y * tz + z;
					bufferDensities[idx] = DensitePourMesh(x, y, z);
					bufferMaterials[idx] = _materialsFlat[Idx(x, yDebut + y, z)];
					if (bufferEau != null) bufferEau[idx] = _densitiesEauFlat[Idx(x, yDebut + y, z)];
				}

		float ValD(int x, int y, int z) => bufferDensities[x * stride + y * tz + z];
		byte MatD(int x, int y, int z) => bufferMaterials[x * stride + y * tz + z];
		float EauD(int x, int y, int z) => bufferEau[x * stride + y * tz + z];

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		float[] vals = _valsRecyclables;
		Vector3[] verts = _vertsRecyclables;
		var edgeTable = ConstantesMarchingCubes.EdgeTable;
		var triTable = ConstantesMarchingCubes.TriTable;

		for (int x = 0; x < tc; x++)
			for (int y = 0; y < yFin - yDebut; y++)
			{
				int yG = yDebut + y;
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

					vals[0] = ValD(x, y, z);
					vals[1] = ValD(x + 1, y, z);
					vals[2] = ValD(x + 1, y + 1, z);
					vals[3] = ValD(x, y + 1, z);
					vals[4] = ValD(x, y, z + 1);
					vals[5] = ValD(x + 1, y, z + 1);
					vals[6] = ValD(x + 1, y + 1, z + 1);
					vals[7] = ValD(x, y + 1, z + 1);

					int cubeIndex = 0;
					for (int i = 0; i < 8; i++)
						if (vals[i] > Isolevel) cubeIndex |= 1 << i;
					if (edgeTable[cubeIndex] == 0) continue;

					Vector3[] vertList = _vertListRecyclables;
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
					byte[] mats = new byte[8];
					mats[0] = MatD(x, y, z); mats[1] = MatD(x + 1, y, z);
					mats[2] = MatD(x + 1, y + 1, z); mats[3] = MatD(x, y + 1, z);
					mats[4] = MatD(x, y, z + 1); mats[5] = MatD(x + 1, y, z + 1);
					mats[6] = MatD(x + 1, y + 1, z + 1); mats[7] = MatD(x, y + 1, z + 1);
					byte idMat = 0;
					for (int i = 0; i < 8; i++)
					{
						if (vals[i] > Isolevel && mats[i] > 0)
						{
							idMat = mats[i];
							break;
						}
					}
					if (idMat == 0) idMat = 2;

					// Espace GLOBAL du monde — évite le tiling biomique (couleurs temp/hum).
					float xGlobal = baseX + x;
					float zGlobal = baseZ + z;
					float temp = _noiseTemperature?.GetNoise2D(xGlobal, zGlobal) ?? 0f;
					float hum = _noiseHumidite != null ? CalculerHumiditeGlobale(xGlobal, zGlobal) : 0f;
					Color couleurId = new Color(idMat / 255f, (temp + 1f) * 0.5f, (hum + 1f) * 0.5f, 1f);

					for (int i = 0; triTable[cubeIndex, i] != -1; i += 3)
					{
						Vector3 v0 = vertList[triTable[cubeIndex, i]];
						Vector3 v1 = vertList[triTable[cubeIndex, i + 1]];
						Vector3 v2 = vertList[triTable[cubeIndex, i + 2]];
						Vector3 n = (v2 - v0).Cross(v1 - v0).Normalized();
						st.SetNormal(n);
						st.SetColor(couleurId);
						st.AddVertex(v0);
						st.SetNormal(n);
						st.SetColor(couleurId);
						st.AddVertex(v1);
						st.SetNormal(n);
						st.SetColor(couleurId);
						st.AddVertex(v2);
					}
				}
			}

		st.GenerateNormals();
		meshTerrain = st.Commit();

		if (bufferEau != null)
		{
			if (_valsEauRecyclables == null) _valsEauRecyclables = new float[8];
			if (_vertsEauRecyclables == null) _vertsEauRecyclables = new Vector3[8];
			if (_vertListEauRecyclables == null) _vertListEauRecyclables = new Vector3[12];

			var stEau = new SurfaceTool();
			stEau.Begin(Mesh.PrimitiveType.Triangles);
			float[] valsEau = _valsEauRecyclables;
			Vector3[] vertsEau = _vertsEauRecyclables;
			for (int x = 0; x < tc; x++)
				for (int y = 0; y < yFin - yDebut; y++)
				{
					int yG = yDebut + y;
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
						valsEau[0] = EauD(x, y, z);
						valsEau[1] = EauD(x + 1, y, z);
						valsEau[2] = EauD(x + 1, y + 1, z);
						valsEau[3] = EauD(x, y + 1, z);
						valsEau[4] = EauD(x, y, z + 1);
						valsEau[5] = EauD(x + 1, y, z + 1);
						valsEau[6] = EauD(x + 1, y + 1, z + 1);
						valsEau[7] = EauD(x, y + 1, z + 1);
						int ci = 0;
						for (int i = 0; i < 8; i++)
							if (valsEau[i] > Isolevel) ci |= 1 << i;
						if (edgeTable[ci] == 0) continue;
						Vector3[] vl = _vertListEauRecyclables;
						vl[0] = Interp(vertsEau[0], vertsEau[1], valsEau[0], valsEau[1]);
						vl[1] = Interp(vertsEau[1], vertsEau[2], valsEau[1], valsEau[2]);
						vl[2] = Interp(vertsEau[2], vertsEau[3], valsEau[2], valsEau[3]);
						vl[3] = Interp(vertsEau[3], vertsEau[0], valsEau[3], valsEau[0]);
						vl[4] = Interp(vertsEau[4], vertsEau[5], valsEau[4], valsEau[5]);
						vl[5] = Interp(vertsEau[5], vertsEau[6], valsEau[5], valsEau[6]);
						vl[6] = Interp(vertsEau[6], vertsEau[7], valsEau[6], valsEau[7]);
						vl[7] = Interp(vertsEau[7], vertsEau[4], valsEau[7], valsEau[4]);
						vl[8] = Interp(vertsEau[0], vertsEau[4], valsEau[0], valsEau[4]);
						vl[9] = Interp(vertsEau[1], vertsEau[5], valsEau[1], valsEau[5]);
						vl[10] = Interp(vertsEau[2], vertsEau[6], valsEau[2], valsEau[6]);
						vl[11] = Interp(vertsEau[3], vertsEau[7], valsEau[3], valsEau[7]);
						for (int i = 0; triTable[ci, i] != -1; i += 3)
						{
							Vector3 v0 = vl[triTable[ci, i]], v1 = vl[triTable[ci, i + 1]], v2 = vl[triTable[ci, i + 2]];
							Vector3 n = (v2 - v0).Cross(v1 - v0).Normalized();
							stEau.SetNormal(n);
							stEau.AddVertex(v0);
							stEau.SetNormal(n);
							stEau.AddVertex(v1);
							stEau.SetNormal(n);
							stEau.AddVertex(v2);
						}
					}
				}
			stEau.GenerateNormals();
			meshEau = stEau.Commit();
		}
		}
		finally
		{
			ArrayPool<float>.Shared.Return(bufferDensities);
			ArrayPool<byte>.Shared.Return(bufferMaterials);
			if (bufferEau != null) ArrayPool<float>.Shared.Return(bufferEau);
		}
		return (meshTerrain, meshEau);
	}

	/// <summary>Construit les données de mesh/collision en arrière-plan sans aucune ressource Godot (listes C# uniquement). Consommé par le Main Thread via CreerMeshesDepuisPayload.</summary>
	private SectionPayload ConstruireSectionPayloadEnBackground(int indexSection, float baseX, float baseZ)
	{
		int yDebut = indexSection * HAUTEUR_SECTION;
		int yFin = Math.Min(yDebut + HAUTEUR_SECTION, HauteurMax);
		int tailleY = yFin - yDebut + 1;
		int tc = TailleChunk;
		int tx = tc + 1, tz = tc + 1;

		float DensitePourMesh(int x, int y, int z) => _densitiesFlat[Idx(x, yDebut + y, z)];

		if (_valsRecyclables == null) _valsRecyclables = new float[8];
		if (_vertsRecyclables == null) _vertsRecyclables = new Vector3[8];
		if (_vertListRecyclables == null) _vertListRecyclables = new Vector3[12];

		var bufferDensities = ArrayPool<float>.Shared.Rent(TAILLE_MAX_SECTION);
		var bufferMaterials = ArrayPool<byte>.Shared.Rent(TAILLE_MAX_SECTION);
		float[] bufferEau = _densitiesEauFlat != null ? ArrayPool<float>.Shared.Rent(TAILLE_MAX_SECTION) : null;

		var vertsT = new List<Vector3>(8192);
		var normsT = new List<Vector3>(8192);
		var colsT = new List<Color>(8192);
		List<Vector3> vertsE = bufferEau != null ? new List<Vector3>(4096) : null;
		List<Vector3> normsE = bufferEau != null ? new List<Vector3>(4096) : null;

		try
		{
			int stride = tailleY * tz;
			int nbVoxels = stride * tz;
			for (int x = 0; x < tx; x++)
				for (int y = 0; y < tailleY; y++)
					for (int z = 0; z < tz; z++)
					{
						int idx = x * stride + y * tz + z;
						bufferDensities[idx] = DensitePourMesh(x, y, z);
						bufferMaterials[idx] = _materialsFlat[Idx(x, yDebut + y, z)];
						if (bufferEau != null) bufferEau[idx] = _densitiesEauFlat[Idx(x, yDebut + y, z)];
					}

			bool sectionVide = true;
			for (int i = 0; i < nbVoxels; i++)
			{
				if (bufferDensities[i] > Isolevel) { sectionVide = false; break; }
			}
			if (sectionVide && bufferEau != null)
			{
				for (int i = 0; i < nbVoxels; i++)
				{
					if (bufferEau[i] > Isolevel) { sectionVide = false; break; }
				}
			}
			if (sectionVide)
				return new SectionPayload();

			float ValD(int x, int y, int z) => bufferDensities[x * stride + y * tz + z];
			byte MatD(int x, int y, int z) => bufferMaterials[x * stride + y * tz + z];
			float EauD(int x, int y, int z) => bufferEau[x * stride + y * tz + z];

			float[] vals = _valsRecyclables;
			Vector3[] verts = _vertsRecyclables;
			if (_matsRecyclables == null) _matsRecyclables = new byte[8];
			byte[] mats = _matsRecyclables;
			var edgeTable = ConstantesMarchingCubes.EdgeTable;
			var triTable = ConstantesMarchingCubes.TriTable;

			for (int x = 0; x < tc; x++)
				for (int y = 0; y < yFin - yDebut; y++)
				{
					int yG = yDebut + y;
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

						vals[0] = ValD(x, y, z);
						vals[1] = ValD(x + 1, y, z);
						vals[2] = ValD(x + 1, y + 1, z);
						vals[3] = ValD(x, y + 1, z);
						vals[4] = ValD(x, y, z + 1);
						vals[5] = ValD(x + 1, y, z + 1);
						vals[6] = ValD(x + 1, y + 1, z + 1);
						vals[7] = ValD(x, y + 1, z + 1);

						int cubeIndex = 0;
						for (int i = 0; i < 8; i++)
							if (vals[i] > Isolevel) cubeIndex |= 1 << i;
						if (edgeTable[cubeIndex] == 0) continue;

						Vector3[] vertList = _vertListRecyclables;
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
							if (vals[i] > Isolevel && mats[i] > 0)
							{
								idMat = mats[i];
								break;
							}
						}
						if (idMat == 0) idMat = 2;

						float xGlobal = baseX + x;
						float zGlobal = baseZ + z;
						float temp = _noiseTemperature?.GetNoise2D(xGlobal, zGlobal) ?? 0f;
						float hum = _noiseHumidite != null ? CalculerHumiditeGlobale(xGlobal, zGlobal) : 0f;
						Color couleurId = new Color(idMat / 255f, (temp + 1f) * 0.5f, (hum + 1f) * 0.5f, 1f);

						for (int i = 0; triTable[cubeIndex, i] != -1; i += 3)
						{
							Vector3 v0 = vertList[triTable[cubeIndex, i]];
							Vector3 v1 = vertList[triTable[cubeIndex, i + 1]];
							Vector3 v2 = vertList[triTable[cubeIndex, i + 2]];
							Vector3 n = (v2 - v0).Cross(v1 - v0).Normalized();
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
				if (_vertListEauRecyclables == null) _vertListEauRecyclables = new Vector3[12];

				float[] valsEau = _valsEauRecyclables;
				Vector3[] vertsEau = _vertsEauRecyclables;
				for (int x = 0; x < tc; x++)
					for (int y = 0; y < yFin - yDebut; y++)
					{
						int yG = yDebut + y;
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
							valsEau[0] = EauD(x, y, z);
							valsEau[1] = EauD(x + 1, y, z);
							valsEau[2] = EauD(x + 1, y + 1, z);
							valsEau[3] = EauD(x, y + 1, z);
							valsEau[4] = EauD(x, y, z + 1);
							valsEau[5] = EauD(x + 1, y, z + 1);
							valsEau[6] = EauD(x + 1, y + 1, z + 1);
							valsEau[7] = EauD(x, y + 1, z + 1);
							int ci = 0;
							for (int i = 0; i < 8; i++)
								if (valsEau[i] > Isolevel) ci |= 1 << i;
							if (edgeTable[ci] == 0) continue;
							Vector3[] vl = _vertListEauRecyclables;
							vl[0] = Interp(vertsEau[0], vertsEau[1], valsEau[0], valsEau[1]);
							vl[1] = Interp(vertsEau[1], vertsEau[2], valsEau[1], valsEau[2]);
							vl[2] = Interp(vertsEau[2], vertsEau[3], valsEau[2], valsEau[3]);
							vl[3] = Interp(vertsEau[3], vertsEau[0], valsEau[3], valsEau[0]);
							vl[4] = Interp(vertsEau[4], vertsEau[5], valsEau[4], valsEau[5]);
							vl[5] = Interp(vertsEau[5], vertsEau[6], valsEau[5], valsEau[6]);
							vl[6] = Interp(vertsEau[6], vertsEau[7], valsEau[6], valsEau[7]);
							vl[7] = Interp(vertsEau[7], vertsEau[4], valsEau[7], valsEau[4]);
							vl[8] = Interp(vertsEau[0], vertsEau[4], valsEau[0], valsEau[4]);
							vl[9] = Interp(vertsEau[1], vertsEau[5], valsEau[1], valsEau[5]);
							vl[10] = Interp(vertsEau[2], vertsEau[6], valsEau[2], valsEau[6]);
							vl[11] = Interp(vertsEau[3], vertsEau[7], valsEau[3], valsEau[7]);
							for (int i = 0; triTable[ci, i] != -1; i += 3)
							{
								Vector3 v0 = vl[triTable[ci, i]], v1 = vl[triTable[ci, i + 1]], v2 = vl[triTable[ci, i + 2]];
								Vector3 n = (v2 - v0).Cross(v1 - v0).Normalized();
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
}
