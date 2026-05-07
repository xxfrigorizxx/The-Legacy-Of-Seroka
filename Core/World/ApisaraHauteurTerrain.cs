using Godot;
using System.Collections.Generic;

/// <summary>
/// Surface monde APISARA : mêmes formules et bruits que <see cref="Chunk_Serveur"/> (génération abysse).
/// (distance profil + bandes sanctuaire / muraille / plaine extérieure / sable / océan).
/// Sert au placement des portails et au fallback sol quand le raycast n’a pas encore de collision.
/// </summary>
public static class ApisaraHauteurTerrain
{
	private const float AbyssRayonTrouNoir = 500f;
	private const float AbyssRayonX = 900f;
	private const float AbyssRayonY = 1100f;
	private const float AbyssRayonZ = 1450f;
	private const float AbyssRayonW = 1600f;
	private const float AbyssFondAbsolu = ConstantesDimensionAbysse.FondAbsolu;
	private const float AbyssAltitudeSanctuaire = 20f;

	private static readonly Dictionary<int, BruitsApisara> _bruitsParSeed = new();

	private sealed class BruitsApisara
	{
		internal readonly FastNoiseLite NoiseSurface;
		internal readonly FastNoiseLite NoiseErosion;
		internal readonly FastNoiseLite NoiseCavernes;
		internal readonly FastNoiseLite NoiseRivieres;
		internal readonly FastNoiseLite NoiseAbysseChaos3D;
		internal readonly FastNoiseLite NoiseAbysseChaosDetail3D;

		internal BruitsApisara(int seed)
		{
			NoiseSurface = new FastNoiseLite();
			NoiseSurface.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			NoiseSurface.Seed = seed;
			NoiseSurface.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
			NoiseSurface.FractalOctaves = 5;
			NoiseSurface.Frequency = 0.002f;

			NoiseErosion = new FastNoiseLite();
			NoiseErosion.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			NoiseErosion.Seed = seed + 1;
			NoiseErosion.FractalOctaves = 5;
			NoiseErosion.Frequency = 0.002f;

			NoiseCavernes = new FastNoiseLite();
			NoiseCavernes.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
			NoiseCavernes.Seed = seed + 4;
			NoiseCavernes.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
			NoiseCavernes.FractalOctaves = 3;
			NoiseCavernes.Frequency = 0.015f;

			NoiseRivieres = new FastNoiseLite();
			NoiseRivieres.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			NoiseRivieres.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
			NoiseRivieres.Frequency = 0.003f;
			NoiseRivieres.Seed = seed + 5;

			NoiseAbysseChaos3D = new FastNoiseLite();
			NoiseAbysseChaos3D.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
			NoiseAbysseChaos3D.Seed = seed + 9241;
			NoiseAbysseChaos3D.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
			NoiseAbysseChaos3D.FractalOctaves = 4;
			NoiseAbysseChaos3D.Frequency = 0.0135f;

			NoiseAbysseChaosDetail3D = new FastNoiseLite();
			NoiseAbysseChaosDetail3D.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
			NoiseAbysseChaosDetail3D.Seed = seed + 9242;
			NoiseAbysseChaosDetail3D.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
			NoiseAbysseChaosDetail3D.FractalOctaves = 3;
			NoiseAbysseChaosDetail3D.Frequency = 0.026f;
		}
	}

	/// <summary>Hauteur Y monde du voxel de surface (identique au serveur de chunks APISARA).</summary>
	public static int ObtenirHauteurSolMonde(int xGlobal, int zGlobal, int seedTerrain)
	{
		if (!_bruitsParSeed.TryGetValue(seedTerrain, out BruitsApisara b))
		{
			b = new BruitsApisara(seedTerrain);
			_bruitsParSeed[seedTerrain] = b;
		}

		return CalculerSurfaceMonde(
			xGlobal, zGlobal,
			b.NoiseSurface, b.NoiseErosion, b.NoiseCavernes, b.NoiseRivieres,
			b.NoiseAbysseChaos3D, b.NoiseAbysseChaosDetail3D);
	}

	/// <summary>Appel direct avec les mêmes instances de bruit que le chunk courant (zéro duplication de cache).</summary>
	public static int CalculerSurfaceMonde(
		int xGlobal, int zGlobal,
		FastNoiseLite noiseSurface,
		FastNoiseLite noiseErosion,
		FastNoiseLite noiseCavernes,
		FastNoiseLite noiseRivieres,
		FastNoiseLite noiseAbysseChaos3D,
		FastNoiseLite noiseAbysseChaosDetail3D)
	{
		const float largeurTransition = 120f;
		float distanceProfil = CalculerDistanceProfilAbysse(xGlobal, zGlobal, noiseSurface, noiseErosion);
		float distanceBrute = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);

		if (distanceBrute <= AbyssRayonTrouNoir)
			return (int)AbyssFondAbsolu;

		float HauteurSanctuaire()
		{
			float tSanctuaire = Mathf.Clamp((distanceProfil - AbyssRayonTrouNoir) / Mathf.Max(1f, AbyssRayonX - AbyssRayonTrouNoir), 0f, 1f);
			float baseSanctuaire = AbyssAltitudeSanctuaire + (tSanctuaire * 8f);
			float bruitMacro = noiseErosion.GetNoise2D(xGlobal * 0.0042f + 4200f, zGlobal * 0.0042f + 4200f) * 5.5f;
			float bruitMicro = noiseSurface.GetNoise2D(xGlobal * 0.013f + 8900f, zGlobal * 0.013f + 8900f) * 2.4f;
			int hauteurSanctuaire = Mathf.RoundToInt(baseSanctuaire + bruitMacro + bruitMicro);
			return Mathf.Max(20f, hauteurSanctuaire);
		}

		float HauteurMuraille()
		{
			float shiftRadialMur = noiseAbysseChaos3D.GetNoise2D(xGlobal * 0.00155f + 5500f, zGlobal * 0.00155f - 5500f) * 118f;
			float tMur = Mathf.Clamp((distanceProfil + shiftRadialMur - AbyssRayonX) / Mathf.Max(1f, AbyssRayonY - AbyssRayonX), 0f, 1f);
			float sCurve = tMur * tMur * (3f - 2f * tMur);
			float ondulationBase = noiseAbysseChaosDetail3D.GetNoise2D(xGlobal * 0.0024f + 7200f, zGlobal * 0.0024f - 7200f) * 42f;
			float baseMur = Mathf.Lerp(108f, 352f, sCurve) + ondulationBase;

			float bruitMacro = noiseSurface.GetNoise2D(xGlobal * 0.005f + 6100f, zGlobal * 0.005f + 6100f) * 72f;
			float bruitMicro = noiseErosion.GetNoise2D(xGlobal * 0.022f + 9100f, zGlobal * 0.022f + 9100f) * 34f;
			float chaosRelief = Mathf.Abs(noiseAbysseChaos3D.GetNoise2D(xGlobal * 0.0038f + 1800f, zGlobal * 0.0038f - 1800f)) * 68f;
			float cretes = Mathf.Abs(noiseCavernes.GetNoise2D(xGlobal * 0.034f + 13000f, zGlobal * 0.034f + 13000f)) * 108f;
			float picsAigusBrut = (noiseCavernes.GetNoise2D(xGlobal * 0.061f + 31000f, zGlobal * 0.061f + 31000f) + 1f) * 0.5f;
			float picsAigus = Mathf.Pow(Mathf.Clamp(picsAigusBrut, 0f, 1f), 3.15f) * 88f;

			float bruitFalaises = noiseRivieres.GetNoise2D(xGlobal * 0.011f + 17000f, zGlobal * 0.011f + 17000f);
			float masqueFalaises = Mathf.Clamp((bruitFalaises - 0.08f) * 2.35f, 0f, 1f);
			float zoneFalaises = Mathf.Clamp(1f - (Mathf.Abs(tMur - 0.46f) / 0.42f), 0f, 1f);
			zoneFalaises *= zoneFalaises;
			float falaises = masqueFalaises * zoneFalaises * 110f;

			float bruitEntaille = noiseRivieres.GetNoise2D(xGlobal * 0.0065f + 21000f, zGlobal * 0.0065f + 21000f);
			float masqueEntaille = Mathf.Clamp((0.14f - bruitEntaille) * 2.7f, 0f, 1f);
			float entaille = masqueEntaille * (60f + (1f - sCurve) * 70f);

			float attenuationSortie = Mathf.Clamp((tMur - 0.72f) / 0.28f, 0f, 1f);
			float sortieRampe = Mathf.Lerp(0f, 150f, attenuationSortie * attenuationSortie);
			float reductionSortie = sortieRampe * (0.45f + (1f - masqueFalaises) * 0.55f);

			float hauteurMur = baseMur + bruitMacro + bruitMicro + chaosRelief + cretes + picsAigus + falaises - reductionSortie - entaille;
			return Mathf.Clamp(hauteurMur, 88f, 485f);
		}

		float HauteurPlaineExterieure()
		{
			float t = Mathf.Clamp((distanceProfil - AbyssRayonY) / Mathf.Max(1f, AbyssRayonZ - AbyssRayonY), 0f, 1f);
			float basePlaine = Mathf.Lerp(145f, 34f, t);
			float vallons = noiseErosion.GetNoise2D(xGlobal * 0.0048f + 7000f, zGlobal * 0.0048f + 7000f) * 12f;
			float reliefFin = noiseSurface.GetNoise2D(xGlobal * 0.011f + 10100f, zGlobal * 0.011f + 10100f) * 6f;
			return Mathf.Max(20f, basePlaine + vallons + reliefFin);
		}

		float HauteurFrontiereSable()
		{
			float t = Mathf.Clamp((distanceProfil - AbyssRayonZ) / Mathf.Max(1f, AbyssRayonW - AbyssRayonZ), 0f, 1f);
			float baseFrontiere = Mathf.Lerp(28f, 0f, t);
			float bruit = noiseErosion.GetNoise2D(xGlobal * 0.009f + 11000f, zGlobal * 0.009f + 11000f) * 2.5f;
			return baseFrontiere + bruit;
		}

		float HauteurOcean()
		{
			float t = (distanceProfil - AbyssRayonW) / 1100f;
			float chute = Mathf.Clamp(t * t, 0f, 1f) * 180f;
			float bruit = noiseRivieres.GetNoise2D(xGlobal * 0.003f + 15000f, zGlobal * 0.003f + 15000f) * 4.2f;
			return -8f - chute + bruit;
		}

		float Blend(float a, float b, float centre)
		{
			float debut = centre - largeurTransition;
			float fin = centre + largeurTransition;
			float t = Mathf.Clamp((distanceProfil - debut) / Mathf.Max(1f, fin - debut), 0f, 1f);
			float s = t * t * (3f - 2f * t);
			return Mathf.Lerp(a, b, s);
		}

		float h;
		if (distanceProfil < AbyssRayonX - largeurTransition)
			h = HauteurSanctuaire();
		else if (distanceProfil < AbyssRayonX + largeurTransition)
			h = Blend(HauteurSanctuaire(), HauteurMuraille(), AbyssRayonX);
		else if (distanceProfil < AbyssRayonY - largeurTransition)
			h = HauteurMuraille();
		else if (distanceProfil < AbyssRayonY + largeurTransition)
			h = Blend(HauteurMuraille(), HauteurPlaineExterieure(), AbyssRayonY);
		else if (distanceProfil < AbyssRayonZ - largeurTransition)
			h = HauteurPlaineExterieure();
		else if (distanceProfil < AbyssRayonZ + largeurTransition)
			h = Blend(HauteurPlaineExterieure(), HauteurFrontiereSable(), AbyssRayonZ);
		else if (distanceProfil < AbyssRayonW - largeurTransition)
			h = HauteurFrontiereSable();
		else if (distanceProfil < AbyssRayonW + largeurTransition)
			h = Blend(HauteurFrontiereSable(), HauteurOcean(), AbyssRayonW);
		else
			h = HauteurOcean();

		return Mathf.RoundToInt(h);
	}

	private static float CalculerDistanceProfilAbysse(
		float xGlobal, float zGlobal,
		FastNoiseLite noiseSurface, FastNoiseLite noiseErosion)
	{
		float distance = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);
		float angle = Mathf.Atan2(zGlobal, xGlobal);

		float modulationAngulaire =
			Mathf.Sin(angle * 3.1f + 0.8f) * 38f +
			Mathf.Sin(angle * 6.7f - 1.4f) * 22f;
		float bruitMacroAnneau = noiseSurface.GetNoise2D(xGlobal * 0.0018f + 2600f, zGlobal * 0.0018f + 2600f) * 54f;
		float bruitWarpAnneau = noiseErosion.GetNoise2D(xGlobal * 0.0034f - 3700f, zGlobal * 0.0034f - 3700f) * 28f;
		return distance + modulationAngulaire + bruitMacroAnneau + bruitWarpAnneau;
	}
}
