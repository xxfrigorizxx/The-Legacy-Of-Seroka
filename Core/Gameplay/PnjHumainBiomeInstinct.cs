using Godot;
using System;

/// <summary>
/// « Instinct de survie » des PNJ : lecture silencieuse des cartes de bruit globales (humidité / température / macro-biome)
/// via la seed du monde — sans générer de chunk. Sert au pathfinding de migration longue distance.
/// </summary>
public static class PnjHumainBiomeInstinct
{
	private const float RayonRechercheDefautM = 800f;
	private const float PasGrilleDefautM = 60f;
	private const int MaxCellulesGrilleParRecherche = 80;
	private const int HauteurMinPraticable = 103;
	private const int HauteurMaxPraticable = 230;
	private const int PenteMaxPraticable = 56;
	private const int PenteRaide = 90;
	private const float ScoreMinViable = 1.0f;
	/// <summary>Distance minimale pour quitter un biome hostile — sauf si le candidat est nettement meilleur.</summary>
	private const float DistanceMinSortieBiomeHostileM = 70f;
	/// <summary>Score minimal pour un site de campement (forêt / plaine humide).</summary>
	private const float ScoreMinCampement = 1.6f;

	private static int _seedCache = int.MinValue;
	private static FastNoiseLite _noiseTemperature;
	private static FastNoiseLite _noiseHumidite;
	private static FastNoiseLite _noiseErosion;

	private static void AssurerBruit(int seed)
	{
		if (_seedCache == seed && _noiseHumidite != null)
			return;
		_seedCache = seed;

		_noiseTemperature = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Seed = seed + 2,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 4,
			Frequency = 0.0006f
		};
		_noiseHumidite = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Seed = seed + 3,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 4,
			Frequency = 0.0006f
		};
		_noiseErosion = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
			Seed = seed + 1,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 4,
			Frequency = 0.002f
		};
	}

	/// <summary>
	/// Balaye par anneaux croissants autour de l'origine et renvoie le biome favorable le plus proche
	/// (score forage + pente + fort biais distance). Lecture seule sur le bruit procédural.
	/// </summary>
	public static bool EssayerTrouverBiomeFavorable(int seed, Vector2 origine, out Vector2 cibleXZ,
		float rayonM = RayonRechercheDefautM, float pasM = PasGrilleDefautM, bool favoriserDescente = true)
	{
		return BalayerBiomesFavorables(seed, origine, out cibleXZ, rayonM, pasM, favoriserDescente,
			exigerCampement: false, biaisDistance: 220f, distanceMinHostile: DistanceMinSortieBiomeHostileM);
	}

	/// <summary>Biome propice au campement le plus proche — priorité distance, pas de bonus « descente ».</summary>
	public static bool EssayerTrouverBiomePourCampement(int seed, Vector2 origine, out Vector2 cibleXZ,
		float rayonM = 280f, float pasM = 24f)
	{
		return BalayerBiomesFavorables(seed, origine, out cibleXZ, rayonM, pasM, favoriserDescente: false,
			exigerCampement: true, biaisDistance: 95f, distanceMinHostile: 20f);
	}

	/// <summary>Secours : balaye 16 directions × plusieurs distances pour éviter une marche aléatoire en neige.</summary>
	public static bool EssayerTrouverMeilleureDirectionCampement(int seed, Vector2 origine, out Vector2 cibleXZ, float rayonMaxM = 280f)
	{
		cibleXZ = origine;
		AssurerBruit(seed);
		int ox = Mathf.FloorToInt(origine.X);
		int oz = Mathf.FloorToInt(origine.Y);
		float scoreOrigine = EvaluerScoreBiomeRapide(ox, oz, seed);
		bool origineHostile = EstZoneHostileRapide(ox, oz, seed);

		float meilleur = float.MinValue;
		bool trouve = false;
		int steps = 16;
		float[] distances = { 40f, 72f, 104f, 136f, 168f, 200f, 240f, 280f };
		foreach (float dist in distances)
		{
			if (dist > rayonMaxM)
				break;
			for (int s = 0; s < steps; s++)
			{
				float angle = (s / (float)steps) * Mathf.Tau;
				float wx = origine.X + Mathf.Cos(angle) * dist;
				float wz = origine.Y + Mathf.Sin(angle) * dist;
				int ix = Mathf.FloorToInt(wx);
				int iz = Mathf.FloorToInt(wz);
				if (!EstBiomeFavorablePourCampement(ix, iz, seed))
					continue;
				float score = EvaluerScoreForage(ix, iz, seed, dist);
				if (score <= float.MinValue + 1f)
					continue;
				if (origineHostile && score <= scoreOrigine + 0.4f)
					continue;
				float composite = score - dist / 90f;
				if (composite <= meilleur)
					continue;
				meilleur = composite;
				cibleXZ = new Vector2(wx, wz);
				trouve = true;
			}
		}
		return trouve;
	}

	private static bool BalayerBiomesFavorables(int seed, Vector2 origine, out Vector2 cibleXZ,
		float rayonM, float pasM, bool favoriserDescente, bool exigerCampement, float biaisDistance, float distanceMinHostile)
	{
		cibleXZ = origine;
		AssurerBruit(seed);
		rayonM = Mathf.Clamp(rayonM, 80f, 1200f);
		pasM = Mathf.Clamp(pasM, 25f, 100f);

		int ox = Mathf.FloorToInt(origine.X);
		int oz = Mathf.FloorToInt(origine.Y);
		bool origineHostile = EstZoneHostileRapide(ox, oz, seed);
		float scoreOrigine = EvaluerScoreBiomeRapide(ox, oz, seed);
		int hOrigine = Generateur_Voxel.ObtenirHauteurTerrainMonde(ox, oz, seed);

		float meilleurComposite = float.MinValue;
		float meilleurSecours = float.MinValue;
		Vector2 secoursXZ = origine;
		bool trouve = false;
		bool trouveSecours = false;
		int cellulesEvaluees = 0;
		int maxAnneaux = Mathf.CeilToInt(rayonM / pasM);

		// Anneaux concentriques : on évalue d'abord les zones proches (plaine sous la pente, etc.).
		for (int ring = 1; ring <= maxAnneaux && cellulesEvaluees < MaxCellulesGrilleParRecherche; ring++)
		{
			float distAnneau = ring * pasM;
			if (distAnneau > rayonM)
				break;
			int steps = Mathf.Clamp(ring * 6, 8, 24);
			for (int s = 0; s < steps && cellulesEvaluees < MaxCellulesGrilleParRecherche; s++)
			{
				float angle = (s / (float)steps) * Mathf.Tau;
				float wx = origine.X + Mathf.Cos(angle) * distAnneau;
				float wz = origine.Y + Mathf.Sin(angle) * distAnneau;
				int ix = Mathf.FloorToInt(wx);
				int iz = Mathf.FloorToInt(wz);
				cellulesEvaluees++;

				float score = EvaluerScoreForage(ix, iz, seed, distAnneau);
				if (score <= float.MinValue + 1f)
					continue;

				int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(ix, iz, seed);
				if (favoriserDescente && h < hOrigine - 2)
					score += 1.2f + Mathf.Min(2f, (hOrigine - h) * 0.08f);

				float composite = score - distAnneau / biaisDistance;

				if (composite > meilleurSecours)
				{
					meilleurSecours = composite;
					secoursXZ = new Vector2(wx, wz);
					trouveSecours = true;
				}

				if (exigerCampement)
				{
					if (!EstBiomeFavorablePourCampement(ix, iz, seed))
						continue;
				}
				else if (score < ScoreMinViable)
					continue;

				if (origineHostile && distAnneau < distanceMinHostile && score < scoreOrigine + (exigerCampement ? 0.5f : 1.2f))
					continue;

				if (composite <= meilleurComposite)
					continue;
				meilleurComposite = composite;
				cibleXZ = new Vector2(wx, wz);
				trouve = true;
			}
		}

		if (trouve)
			return true;

		if (trouveSecours && (meilleurSecours > scoreOrigine + 0.3f || origineHostile))
		{
			int sx = Mathf.FloorToInt(secoursXZ.X);
			int sz = Mathf.FloorToInt(secoursXZ.Y);
			if (!exigerCampement || EstBiomeFavorablePourCampement(sx, sz, seed))
			{
				cibleXZ = secoursXZ;
				return true;
			}
		}
		return false;
	}

	/// <summary>Score macro-biome via bruit seulement — léger, safe à appeler souvent.</summary>
	public static float EvaluerScoreBiomeRapide(int worldX, int worldZ, int seed)
	{
		AssurerBruit(seed);
		float humidite = _noiseHumidite.GetNoise2D(worldX, worldZ);
		float temperature = _noiseTemperature.GetNoise2D(worldX, worldZ);
		float macroBiome = (_noiseErosion.GetNoise2D(worldX * 0.60f + 9100f, worldZ * 0.60f - 9100f) + 1f) * 0.5f;

		float scoreBio = 0f;
		if (macroBiome >= 0.5f)
			scoreBio = 3.5f + humidite * 1.2f;
		else if (macroBiome >= 0.416666f)
			scoreBio = 2.8f + humidite * 1.5f;
		else if (macroBiome >= 0.25f)
			scoreBio = 2.2f + humidite * 1.0f;
		else if (macroBiome < 0.083333f)
			scoreBio = -2.5f + humidite * 0.3f;
		else if (macroBiome < 0.166666f)
			scoreBio = -3.0f + temperature * 0.5f;
		else if (macroBiome < 0.5f)
			scoreBio = -1.5f + humidite * 0.4f;

		scoreBio += Mathf.Clamp(temperature, -0.5f, 0.8f) * 0.6f;
		return scoreBio;
	}

	/// <summary>Score élevé = zone propice aux baies (humide, tempérée, plate, pas sous l'eau).</summary>
	public static float EvaluerScoreForage(int worldX, int worldZ, int seed, float distanceOrigine = 0f)
	{
		float scoreBio = EvaluerScoreBiomeRapide(worldX, worldZ, seed);
		int pente = CalculerPenteTerrain(worldX, worldZ, seed, out int hauteur);
		if (hauteur < HauteurMinPraticable || hauteur > HauteurMaxPraticable)
			return float.MinValue;
		if (pente > PenteRaide)
			scoreBio -= 4f;
		else if (pente > PenteMaxPraticable)
			scoreBio -= 2f;

		if (distanceOrigine > 0.01f)
			scoreBio -= distanceOrigine / 350f; // biais vers le biome favorable le plus proche
		return scoreBio;
	}

	/// <summary>Test hostile léger (bruit seulement) — pour l'IA en temps réel.</summary>
	public static bool EstZoneHostileRapide(int worldX, int worldZ, int seed)
		=> EvaluerScoreBiomeRapide(worldX, worldZ, seed) < ScoreMinViable;

	/// <summary>Biome assez bon pour s'installer (forêt / plaine humide) — pas une expédition.</summary>
	public static bool EstBiomeFavorablePourCampement(int worldX, int worldZ, int seed)
	{
		if (EstZoneHostileRapide(worldX, worldZ, seed))
			return false;
		return EvaluerScoreBiomeRapide(worldX, worldZ, seed) >= 1.6f;
	}

	/// <summary>True si la position actuelle est impropre au forage (neige, sable, aride…).</summary>
	public static bool EstZoneHostilePourForage(int worldX, int worldZ, int seed)
		=> EstZoneHostileRapide(worldX, worldZ, seed);

	/// <summary>Neige, sable, terre aride : creusable à main nue par le PNJ.</summary>
	public static bool EstMatiereCrevassableMainNu(int worldX, int worldZ, int seed)
	{
		AssurerBruit(seed);
		float macroBiome = (_noiseErosion.GetNoise2D(worldX * 0.60f + 9100f, worldZ * 0.60f - 9100f) + 1f) * 0.5f;
		if (macroBiome < 0.166666f)
			return true; // neige / sable
		if (macroBiome < 0.5f)
			return true; // aride (et argile — plus mou que la roche)
		return false;
	}

	/// <summary>Pente locale (somme des dénivelés ±5 m) — disponible hors chunks chargés.</summary>
	public static int CalculerPenteTerrain(int x, int z, int seed, out int hauteur)
	{
		hauteur = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, seed);
		int hE = Generateur_Voxel.ObtenirHauteurTerrainMonde(x + 5, z, seed);
		int hW = Generateur_Voxel.ObtenirHauteurTerrainMonde(x - 5, z, seed);
		int hN = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z - 5, seed);
		int hS = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z + 5, seed);
		return Mathf.Abs(hauteur - hE) + Mathf.Abs(hauteur - hW) + Mathf.Abs(hauteur - hN) + Mathf.Abs(hauteur - hS);
	}

	/// <summary>Facteur de vitesse virtuelle selon la pente (montagne = ralentissement drastique).</summary>
	public static float FacteurVitesseSelonPente(int pente)
	{
		if (pente > PenteRaide) return 0.08f;
		if (pente > PenteMaxPraticable) return 0.25f;
		if (pente > 35) return 0.55f;
		return 1f;
	}

	/// <summary>Drain stamina / seconde selon la pente (montée raide = épuisement).</summary>
	public static float DrainStaminaPenteParSeconde(int pente)
	{
		if (pente > PenteRaide) return 12f;
		if (pente > PenteMaxPraticable) return 5f;
		if (pente > 35) return 1.5f;
		return 0f;
	}

	public static float HauteurSolMonde(float x, float z, int seed)
	{
		int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(x), Mathf.FloorToInt(z), seed);
		return h + PnjHumain.DecalageYOrigineDepuisHauteurTerrainVoxel;
	}
}
