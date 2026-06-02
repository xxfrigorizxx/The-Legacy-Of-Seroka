using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

public partial class Chunk_Serveur : RefCounted
{
	/// <summary>Résout surface arbre : Y monde, indice local dans la tranche, matériau. False si la surface n'est pas dans ce chunk.</summary>
	private bool EssayerObtenirSurfaceArbre(int lx, int lz, out int yMonde, out int lyLocal, out byte matSurface)
	{
		yMonde = 0;
		lyLocal = -1;
		matSurface = 0;
		int xGlobal = ChunkOffsetX * TailleChunk + lx;
		int zGlobal = ChunkOffsetZ * TailleChunk + lz;
		yMonde = CalculerHauteurTerrain(xGlobal, zGlobal);
		if (yMonde <= 2) return false;
		lyLocal = yMonde - ChunkOffsetY * HauteurMax;
		if (lyLocal < 0 || lyLocal >= HauteurMax - 1) return false;
		lock (_verrouVoxel)
		{
			if (_materials == null) return false;
			matSurface = _materials[lx, lyLocal, lz];
		}
		return true;
	}

	/// <summary>Enregistre les positions d'arbres (ArbreVivant 3D) — sans injection voxel. Monde_Serveur les instancie.</summary>
	private void InjecterArbresLSystem()
	{
		if (_generationAbysseActive)
		{
			InjecterArbresLSystemPlaineJungleAbysse();
			return;
		}
		const float chanceArbre = 0.06f;
		const int espacementMin = 4;
		int xCentre = ChunkOffsetX * TailleChunk + TailleChunk / 2;
		int zCentre = ChunkOffsetZ * TailleChunk + TailleChunk / 2;
		float tempCentre = _noiseTemperature.GetNoise2D(xCentre, zCentre);
		float humCentreNorm = (CalculerHumiditeGlobale(xCentre, zCentre) + 1f) * 0.5f;
		bool chunkJungleCentre = tempCentre > 0.22f && humCentreNorm > 0.78f;
		bool chunkSansArbresTempere = tempCentre >= -0.15f
			&& !chunkJungleCentre
			&& DeterminerBiomeForetTempere(xCentre, zCentre) == 0;
		bool arbreAjoute = false;
		for (int x = 2; x < TailleChunk - 2; x += espacementMin)
		for (int z = 2; z < TailleChunk - 2; z += espacementMin)
		{
			int xGlobal = ChunkOffsetX * TailleChunk + x;
			int zGlobal = ChunkOffsetZ * TailleChunk + z;
			if (!TerrainAssezPlat(xGlobal, zGlobal)) continue;
			if (!TerrainAvecMargeBord(xGlobal, zGlobal)) continue;

			if (!EssayerObtenirSurfaceArbre(x, z, out int hauteurSurface, out _, out byte matSurface)) continue;

			// Tempéré: herbe (1). Froid/enneigé: neige (5) et glace (9). Sec: terre aride (6) et désert sableux (3).
			bool solTempere = matSurface == 1;
			bool solFroid = matSurface == 5 || matSurface == 9;
			bool solAride = matSurface == 6;
			bool solDesert = matSurface == 3;
			if (!solTempere && !solFroid && !solAride && !solDesert) continue;

			float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
			float humidite = CalculerHumiditeGlobale(xGlobal, zGlobal);
			float humiditeNorm = (humidite + 1f) * 0.5f;
			bool estJungle = temperature > 0.22f && humiditeNorm > 0.78f;

			float chanceLocale = chanceArbre;
			if (solFroid || temperature < -0.15f)
			{
				if (!solFroid) continue;
				if (humiditeNorm < 0.04f) continue;
				// Zone neige/pin: sec -> peu d'arbres, et plus il fait froid plus la densité monte.
				// Plafond conservé à 0.085 (comme avant la dernière modification).
				float tHumideNeige = Mathf.Clamp((humiditeNorm - 0.04f) / 0.56f, 0f, 1f);
				float tFroidNeige = Mathf.Clamp((-temperature + 0.02f) / 0.72f, 0f, 1f);
				float facteurNeige = tHumideNeige * Mathf.Lerp(0.45f, 1.0f, tFroidNeige);
				chanceLocale = Mathf.Lerp(0.012f, 0.085f, facteurNeige);
			}
			else
			{
				if (solAride || solDesert)
				{
					// Pas d'arbres morts en désert immergé.
					if (hauteurSurface <= NiveauEau) continue;
					// Zones sèches: arbres morts uniquement, plus visibles qu'avant.
					if (solDesert)
					{
						if (temperature < 0.18f) continue;
						if (humiditeNorm > 0.42f) continue;
						float tSecDesert = Mathf.Clamp((0.42f - humiditeNorm) / 0.42f, 0f, 1f);
						chanceLocale = Mathf.Lerp(0.004f, 0.015f, tSecDesert);
					}
					else
					{
						if (temperature < 0.10f) continue;
						if (humiditeNorm > 0.56f) continue;
						float tSecAride = Mathf.Clamp((0.56f - humiditeNorm) / 0.50f, 0f, 1f);
						chanceLocale = Mathf.Lerp(0.012f, 0.040f, tSecAride);
					}
				}
				else
				{
					if (!solTempere) continue;
					if (humiditeNorm < 0.2f) continue;
					if (estJungle)
					{
						// Jungle chaude/humide: densité haute.
						float tJungle = Mathf.Clamp((humiditeNorm - 0.78f) / 0.22f, 0f, 1f);
						chanceLocale = Mathf.Lerp(0.078f, 0.145f, tJungle);
					}
					else
					{
						int biomeForet = DeterminerBiomeForetTempere(xGlobal, zGlobal);
						if (biomeForet == 0) continue; // biome "clairière/plaine" sans arbres
						// Prairie tempérée: sec -> clairsemé, humide -> densité actuelle.
						float tHumideTempere = Mathf.Clamp((humiditeNorm - 0.2f) / 0.45f, 0f, 1f);
						chanceLocale = Mathf.Lerp(0.018f, chanceArbre, tHumideTempere);
						if (biomeForet == 2) chanceLocale *= 0.92f; // mixte: un peu plus aéré
						else chanceLocale *= 1.08f; // monospécifique: un peu plus dense
					}
				}
			}

			if (DeterministicRand(xGlobal * 1.7f, zGlobal * 2.3f) >= chanceLocale) continue;

			var racine = new Vector3I(xGlobal, hauteurSurface + 1, zGlobal);
			uint seedArbre = (uint)((xGlobal * 73856093) ^ (zGlobal * 19349663));
			// Âges 1–10 à la génération (pas que des bébés)
			int stage = (int)(seedArbre % 10);
			InventaireArbres[racine] = new DonneesArbre { Stage = (byte)stage, Seed = seedArbre };
			arbreAjoute = true;
		}

		if (arbreAjoute || InventaireArbres.Count > 0) return;
		if (chunkSansArbresTempere) return;

		// Fallback machine/biome : si le tirage standard n'a rien donné, on force un arbre
		// sur un point viable (herbe OU neige OU aride), en gardant une cohérence humide minimale.
		for (int x = 2; x < TailleChunk - 2; x += 2)
		for (int z = 2; z < TailleChunk - 2; z += 2)
		{
			int xGlobal = ChunkOffsetX * TailleChunk + x;
			int zGlobal = ChunkOffsetZ * TailleChunk + z;
			if (!TerrainAssezPlat(xGlobal, zGlobal)) continue;
			if (!TerrainAvecMargeBord(xGlobal, zGlobal)) continue;

			if (!EssayerObtenirSurfaceArbre(x, z, out int hauteurSurface, out _, out byte matSurface)) continue;

			bool solTempere = matSurface == 1;
			bool solFroid = matSurface == 5 || matSurface == 9;
			bool solAride = matSurface == 6;
			bool solDesert = matSurface == 3;
			if (!solTempere && !solFroid && !solAride && !solDesert) continue;

			float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
			float humiditeNorm = (CalculerHumiditeGlobale(xGlobal, zGlobal) + 1f) * 0.5f;
			bool estJungle = temperature > 0.22f && humiditeNorm > 0.78f;
			if (solFroid || temperature < -0.15f)
			{
				if (!solFroid || humiditeNorm < 0.04f) continue;
			}
			else
			{
				if (solAride || solDesert)
				{
					// Pas d'arbres morts en désert immergé.
					if (hauteurSurface <= NiveauEau) continue;
					if (solDesert)
					{
						if (temperature < 0.18f || humiditeNorm > 0.42f) continue;
					}
					else
					{
						if (temperature < 0.10f || humiditeNorm > 0.56f) continue;
					}
				}
				else
				{
					if (!solTempere || humiditeNorm < 0.2f) continue;
					if (!estJungle && DeterminerBiomeForetTempere(xGlobal, zGlobal) == 0) continue;
				}
			}

			var racine = new Vector3I(xGlobal, hauteurSurface + 1, zGlobal);
			uint seedArbre = (uint)((xGlobal * 73856093) ^ (zGlobal * 19349663));
			int stage = (int)(seedArbre % 10);
			InventaireArbres[racine] = new DonneesArbre { Stage = (byte)stage, Seed = seedArbre };
			return;
		}
	}

	/// <summary>APISARA : arbres type jungle uniquement sur les deux plaines herbe (sanctuaire + plaine extérieure).</summary>
	private void InjecterArbresLSystemPlaineJungleAbysse()
	{
		const int espacementMin = 3;
		bool arbreAjoute = false;
		for (int x = 2; x < TailleChunk - 2; x += espacementMin)
		for (int z = 2; z < TailleChunk - 2; z += espacementMin)
		{
			int xGlobal = ChunkOffsetX * TailleChunk + x;
			int zGlobal = ChunkOffsetZ * TailleChunk + z;
			if (!EstPlaineJungleAbysse(xGlobal, zGlobal)) continue;
			if (!TerrainAssezPlat(xGlobal, zGlobal)) continue;
			if (!TerrainAvecMargeBord(xGlobal, zGlobal)) continue;
			if (!EssayerObtenirSurfaceArbre(x, z, out int hauteurSurface, out _, out byte matSurface)) continue;
			if (matSurface != 1) continue;
			float bruitDensite = DeterministicRand(xGlobal * 1.7f, zGlobal * 2.3f);
			float chanceLocale = Mathf.Lerp(0.068f, 0.132f, bruitDensite);
			if (DeterministicRand(xGlobal * 0.91f + 2f, zGlobal * 1.1f + 3f) >= chanceLocale) continue;
			var racine = new Vector3I(xGlobal, hauteurSurface + 1, zGlobal);
			uint seedArbre = (uint)((xGlobal * 73856093) ^ (zGlobal * 19349663));
			int stage = (int)(seedArbre % 10);
			InventaireArbres[racine] = new DonneesArbre { Stage = (byte)stage, Seed = seedArbre };
			arbreAjoute = true;
		}
		if (arbreAjoute || InventaireArbres.Count > 0) return;
		for (int x = 2; x < TailleChunk - 2; x += 2)
		for (int z = 2; z < TailleChunk - 2; z += 2)
		{
			int xGlobal = ChunkOffsetX * TailleChunk + x;
			int zGlobal = ChunkOffsetZ * TailleChunk + z;
			if (!EstPlaineJungleAbysse(xGlobal, zGlobal)) continue;
			if (!TerrainAssezPlat(xGlobal, zGlobal) || !TerrainAvecMargeBord(xGlobal, zGlobal)) continue;
			if (!EssayerObtenirSurfaceArbre(x, z, out int hauteurSurface, out _, out byte matSurface)) continue;
			if (hauteurSurface < 3) continue;
			if (matSurface != 1) continue;
			var racine = new Vector3I(xGlobal, hauteurSurface + 1, zGlobal);
			uint seedArbre = (uint)((xGlobal * 73856093) ^ (zGlobal * 19349663));
			InventaireArbres[racine] = new DonneesArbre { Stage = (byte)(seedArbre % 10), Seed = seedArbre };
			return;
		}
	}
}
