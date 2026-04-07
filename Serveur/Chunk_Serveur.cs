using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>Données voxel et logique de génération pour un chunk. Aucun MeshInstance3D.</summary>
public partial class Chunk_Serveur : RefCounted
{
	public int TailleChunk { get; }
	public int HauteurMax { get; }
	public int ChunkOffsetX { get; }
	public int ChunkOffsetZ { get; }
	public Vector3 PositionMonde { get; }

	private float[,,] _densities;
	private float[,,] _densitiesEau;
	private byte[,,] _materials;
	private readonly object _verrouVoxel = new object();

	private FastNoiseLite _noiseSurface;
	private FastNoiseLite _noiseErosion;
	private FastNoiseLite _noiseTemperature;
	private FastNoiseLite _noiseHumidite;
	private FastNoiseLite _noiseHumiditeDetail;
	private FastNoiseLite _noiseCavernes;
	private FastNoiseLite _noiseRivieres;
	private FastNoiseLite _noiseNeige;

	private const float Isolevel = 0.0f;
	private const int NiveauEau = 103;  // +1 m
	private const int ProfondeurBase = 104;
	private const int AmplitudeMontagne = 396;  // Max ~500 (très rare en haut)
	private const int NiveauPlage = 102;  // Sable jusqu'à 102, herbe à 103-104 (niveau eau inchangé)
	private const int SeuilNeigeBase = 250;   // Neige 245-255 (bruit ±5)
	private const int SeuilMontagneRoche = 207; // Roche 200-215 (bruit ±8)
	/// <summary>Limites altitude flore. Inclut la zone de spawn (herbe haute).</summary>
	private const float NIVEAU_MIN_FLORE = 5f;
	private const float NIVEAU_MAX_FLORE = 260f;

	/// <summary>Registre flore: 0=gazon, puis couples buisson (impair=plein, pair=vide) pour variantes futures.</summary>
	public Dictionary<Vector3I, byte> InventaireFlore { get; } = new Dictionary<Vector3I, byte>();

	public const byte FloreTypeGazon = 0;
	public const byte FloreTypeBuissonRougePlein = 1;
	public const byte FloreTypeBuissonRougeVide = 2;
	public const byte VarianteCouleurBuissonRouge = 0;
	private const byte FloreTypeBuissonDebut = FloreTypeBuissonRougePlein;

	public static bool EstTypeBuisson(byte typeFlore) => typeFlore >= FloreTypeBuissonDebut;
	public static bool EstBuissonPlein(byte typeFlore) => EstTypeBuisson(typeFlore) && (((typeFlore - FloreTypeBuissonDebut) & 1) == 0);
	public static bool EstBuissonVide(byte typeFlore) => EstTypeBuisson(typeFlore) && (((typeFlore - FloreTypeBuissonDebut) & 1) == 1);
	public static byte ObtenirVarianteBuisson(byte typeFlore) => EstTypeBuisson(typeFlore) ? (byte)((typeFlore - FloreTypeBuissonDebut) / 2) : (byte)255;
	public static byte ConstruireTypeBuisson(byte varianteCouleur, bool plein)
	{
		int v = varianteCouleur;
		if (v < 0) v = 0;
		if (v > 120) v = 120;
		return (byte)(FloreTypeBuissonDebut + v * 2 + (plein ? 0 : 1));
	}
	public static byte TypeBuissonSansBaies(byte typeFlore) => EstTypeBuisson(typeFlore) ? ConstruireTypeBuisson(ObtenirVarianteBuisson(typeFlore), false) : typeFlore;

	/// <summary>Registre d'arbres L-System : racine (position base) → (stade 0-3, seed). Croissance 1 stade/jour.</summary>
	public Dictionary<Vector3I, DonneesArbre> InventaireArbres { get; } = new Dictionary<Vector3I, DonneesArbre>();

	private Action<Vector3, byte> _callbackBlocChutant;
	private Func<Vector2I, bool> _chunkEstCharge;
	private Action<Vector3> _reveillerEau;
	private Action<Vector3I, byte> _onVoxelModifie;
	private Action<Vector2I, Dictionary<Vector3I, byte>> _onFlorePurgée;

	/// <summary>Drapeau de souillure : true UNIQUEMENT quand DetruireVoxel ou CreerMatiere sont appelés. On ne sauvegarde JAMAIS un chunk intact.</summary>
	private bool _estModifie;
	/// <summary>True si chargé depuis disque. AUCUNE passe de génération ne doit jamais s'exécuter sur ce chunk.</summary>
	private bool _chargeDepuisDisque;

	public bool EstModifie => _estModifie;
	public bool EstChargeDepuisDisque => _chargeDepuisDisque;

	public void SetOnVoxelModifie(Action<Vector3I, byte> callback) => _onVoxelModifie = callback;
	public void SetOnFlorePurgée(Action<Vector2I, Dictionary<Vector3I, byte>> callback) => _onFlorePurgée = callback;

	public Chunk_Serveur(int chunkOffsetX, int chunkOffsetZ, int tailleChunk, int hauteurMax, int seed,
		Action<Vector3, byte> callbackBlocChutant, Func<Vector2I, bool> chunkEstCharge, Action<Vector3> reveillerEau)
	{
		ChunkOffsetX = chunkOffsetX;
		ChunkOffsetZ = chunkOffsetZ;
		TailleChunk = tailleChunk;
		HauteurMax = hauteurMax;
		PositionMonde = new Vector3(chunkOffsetX * tailleChunk, 0, chunkOffsetZ * tailleChunk);
		_callbackBlocChutant = callbackBlocChutant;
		_chunkEstCharge = chunkEstCharge;
		_reveillerEau = reveillerEau;

		ConfigurerBruit(seed);
	}

	private void ConfigurerBruit(int seed)
	{
		_noiseSurface = new FastNoiseLite();
		_noiseSurface.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_noiseSurface.Seed = seed;
		_noiseSurface.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseSurface.FractalOctaves = 5;
		_noiseSurface.Frequency = 0.002f;

		_noiseErosion = new FastNoiseLite();
		_noiseErosion.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_noiseErosion.Seed = seed + 1;
		_noiseErosion.FractalOctaves = 5;
		_noiseErosion.Frequency = 0.002f;

		// Température : Fbm + octaves = transitions lentes, zones climatiques étendues
		_noiseTemperature = new FastNoiseLite();
		_noiseTemperature.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseTemperature.Seed = seed + 2;
		_noiseTemperature.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseTemperature.FractalOctaves = 4;
		_noiseTemperature.Frequency = 0.0005f;  // Zones larges = transitions douces

		// Humidité : idem, plusieurs stades avec variation progressive
		_noiseHumidite = new FastNoiseLite();
		_noiseHumidite.Seed = seed + 3;
		_noiseHumidite.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseHumidite.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseHumidite.FractalOctaves = 4;
		_noiseHumidite.Frequency = 0.0006f;  // Légèrement différent de temp = biomes variés

		// Micro-variation globale continue (sans effet "patch par chunk").
		_noiseHumiditeDetail = new FastNoiseLite();
		_noiseHumiditeDetail.Seed = seed + 33;
		_noiseHumiditeDetail.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseHumiditeDetail.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseHumiditeDetail.FractalOctaves = 2;
		_noiseHumiditeDetail.Frequency = 0.0065f;

		_noiseCavernes = new FastNoiseLite();
		_noiseCavernes.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
		_noiseCavernes.Seed = seed + 4;
		_noiseCavernes.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
		_noiseCavernes.FractalOctaves = 3;
		_noiseCavernes.Frequency = 0.015f;

		_noiseRivieres = new FastNoiseLite();
		_noiseRivieres.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_noiseRivieres.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
		_noiseRivieres.Frequency = 0.003f;
		_noiseRivieres.Seed = seed + 5;

		_noiseNeige = new FastNoiseLite();
		_noiseNeige.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseNeige.Seed = seed + 10;
		_noiseNeige.Frequency = 0.008f;  // Variation locale naturelle de la limite des neiges
	}

	public bool EstPret => _densities != null;

	/// <summary>TOUTES les passes procédurales (terrain, surface, herbe, eau). NE DOIT JAMAIS s'exécuter sur un chunk chargé du disque.</summary>
	public void GenererDonneesVoxel()
	{
		if (_chargeDepuisDisque) return; // GARDE ABSOLUE : chunk ressuscité du disque — aucune modification mathématique.
		lock (_verrouVoxel)
		{
			_densities = new float[TailleChunk + 1, HauteurMax + 1, TailleChunk + 1];
			_materials = new byte[TailleChunk + 1, HauteurMax + 1, TailleChunk + 1];
			_densitiesEau = new float[TailleChunk + 1, HauteurMax + 1, TailleChunk + 1];

			for (int x = 0; x <= TailleChunk; x++)
			{
				for (int y = 0; y <= HauteurMax; y++)
				{
					for (int z = 0; z <= TailleChunk; z++)
					{
						// Espace GLOBAL du monde — évite le tiling biomique (chaleur/humidité fracturée).
						float xGlobal = ChunkOffsetX * TailleChunk + x;
						float zGlobal = ChunkOffsetZ * TailleChunk + z;
						float globalY = y;

						int hauteurSurface = CalculerHauteurTerrain((int)xGlobal, (int)zGlobal);
						float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
						float humidite = CalculerHumiditeGlobale(xGlobal, zGlobal);

						_densitiesEau[x, y, z] = -1.0f;

						if (y <= 2)
						{
							_densities[x, y, z] = 1000.0f;
							_materials[x, y, z] = 2;
						}
						else if (globalY == hauteurSurface)
						{
							byte mat = DeterminerMateriauCroûte((int)xGlobal, (int)zGlobal, (int)globalY, hauteurSurface, temperature, humidite);
							_materials[x, y, z] = mat;
							_densities[x, y, z] = 10.0f;
							// Gazon uniquement sur voxel herbe (ID 1), uniquement sur terrain plat
							if (EstMateriauSupportGazon(mat)
								&& TerrainAssezPlat((int)xGlobal, (int)zGlobal)
								&& TerrainAvecMargeBord((int)xGlobal, (int)zGlobal))
							{
								float altitudeFlore = globalY;
								if (altitudeFlore > NIVEAU_MIN_FLORE && altitudeFlore < NIVEAU_MAX_FLORE)
								{
									var posGlobale = new Vector3I((int)xGlobal, (int)globalY, (int)zGlobal);
									InventaireFlore[posGlobale] = FloreTypeGazon; // Gazon seul par défaut
									float chanceDePousse = CalculerChanceBuisson(xGlobal, zGlobal);
									if (chanceDePousse > 0f && DeterministicRand(xGlobal, zGlobal) < chanceDePousse)
										InventaireFlore[posGlobale] = ConstruireTypeBuisson(VarianteCouleurBuissonRouge, DeterministicRand(xGlobal + 17f, zGlobal) < 0.5f);
								}
							}
						}
						else if (globalY < hauteurSurface && globalY >= hauteurSurface - 4)
						{
							float valeurGrotte = _noiseCavernes.GetNoise3D(xGlobal, globalY, zGlobal);
							if (valeurGrotte > 0.75f)
							{
								_densities[x, y, z] = -10.0f;
								_materials[x, y, z] = 0;
							}
							else
							{
								_densities[x, y, z] = 10.0f;
								float bruitN = _noiseNeige.GetNoise2D(xGlobal, zGlobal);
								int seuilRocheLocal = SeuilMontagneRoche + (int)(_noiseNeige.GetNoise2D(xGlobal + 500f, zGlobal) * 8f);
								int seuilNeigeLocal = SeuilNeigeBase + (int)(bruitN * 5f);
								_materials[x, y, z] = (hauteurSurface >= seuilRocheLocal || hauteurSurface >= seuilNeigeLocal) ? (byte)2 : (humidite > 0.3f ? (byte)7 : (byte)6);
							}
						}
						else if (globalY < hauteurSurface - 4)
						{
							float valeurGrotte = _noiseCavernes.GetNoise3D(xGlobal, globalY, zGlobal);
							if (valeurGrotte > 0.55f)
							{
								_densities[x, y, z] = -10.0f;
								_materials[x, y, z] = 0;
							}
							else
							{
								_densities[x, y, z] = 10.0f;
								_materials[x, y, z] = 2;
							}
						}
						else if (globalY > hauteurSurface && globalY <= NiveauEau)
						{
							_densities[x, y, z] = -10.0f;
							_materials[x, y, z] = 0;
							_densitiesEau[x, y, z] = (NiveauEau + 1.0f) - y;
						}
						else
						{
							_densities[x, y, z] = -10.0f;
							_materials[x, y, z] = 0;
						}
					}
				}
			}

			// Pass L-System : injection des Chênes (voxels bois ID 30, feuilles ID 31)
			InjecterArbresLSystem();
			// Garantie de lisibilité gameplay: au moins un buisson si le chunk contient du gazon.
			AssurerBuissonMinimalDansChunk();
			// RÈGLE : Chunk procédural non touché par le joueur → jamais sauvegardé (régénération à la demande).
		}
	}

	/// <summary>Enregistre les positions d'arbres (ArbreVivant 3D) — sans injection voxel. Monde_Serveur les instancie.</summary>
	private void InjecterArbresLSystem()
	{
		const float chanceArbre = 0.06f;
		const int espacementMin = 4;
		bool arbreAjoute = false;
		for (int x = 2; x < TailleChunk - 2; x += espacementMin)
		for (int z = 2; z < TailleChunk - 2; z += espacementMin)
		{
			int xGlobal = ChunkOffsetX * TailleChunk + x;
			int zGlobal = ChunkOffsetZ * TailleChunk + z;
			if (!TerrainAssezPlat(xGlobal, zGlobal)) continue;
			if (!TerrainAvecMargeBord(xGlobal, zGlobal)) continue;

			int hauteurSurface = CalculerHauteurTerrain(xGlobal, zGlobal);
			if (hauteurSurface < 0 || hauteurSurface >= HauteurMax - 1) continue;
			if (hauteurSurface <= 2) continue;

			byte matSurface;
			lock (_verrouVoxel)
			{
				matSurface = _materials[x, hauteurSurface, z];
			}

			// Tempéré: herbe (1). Froid/enneigé: neige (5) et glace de surface (9) autorisées (pins).
			bool solTempere = matSurface == 1;
			bool solFroid = matSurface == 5 || matSurface == 9;
			if (!solTempere && !solFroid) continue;

			float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
			float humidite = CalculerHumiditeGlobale(xGlobal, zGlobal);
			float humiditeNorm = (humidite + 1f) * 0.5f;

			float chanceLocale = chanceArbre;
			if (temperature < -0.15f)
			{
				if (!solFroid) continue;
				if (humiditeNorm < 0.08f) continue;
				// Zone neige/pin: sec -> peu d'arbres, et plus il fait froid plus la densité monte.
				// Plafond conservé à 0.085 (comme avant la dernière modification).
				float tHumideNeige = Mathf.Clamp((humiditeNorm - 0.08f) / 0.50f, 0f, 1f);
				float tFroidNeige = Mathf.Clamp((-temperature - 0.15f) / 0.55f, 0f, 1f);
				float facteurNeige = tHumideNeige * Mathf.Lerp(0.45f, 1.0f, tFroidNeige);
				chanceLocale = Mathf.Lerp(0.012f, 0.085f, facteurNeige);
			}
			else
			{
				if (!solTempere) continue;
				if (humiditeNorm < 0.2f) continue;
				// Prairie tempérée: sec -> clairsemé, humide -> densité actuelle.
				float tHumideTempere = Mathf.Clamp((humiditeNorm - 0.2f) / 0.45f, 0f, 1f);
				chanceLocale = Mathf.Lerp(0.018f, chanceArbre, tHumideTempere);
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

		// Fallback machine/biome : si le tirage standard n'a rien donné, on force un arbre
		// sur un point viable (herbe OU neige), en gardant une cohérence humide minimale.
		for (int x = 2; x < TailleChunk - 2; x += 2)
		for (int z = 2; z < TailleChunk - 2; z += 2)
		{
			int xGlobal = ChunkOffsetX * TailleChunk + x;
			int zGlobal = ChunkOffsetZ * TailleChunk + z;
			if (!TerrainAssezPlat(xGlobal, zGlobal)) continue;
			if (!TerrainAvecMargeBord(xGlobal, zGlobal)) continue;

			int hauteurSurface = CalculerHauteurTerrain(xGlobal, zGlobal);
			if (hauteurSurface < 0 || hauteurSurface >= HauteurMax - 1) continue;
			if (hauteurSurface <= 2) continue;

			byte matSurface;
			lock (_verrouVoxel)
			{
				matSurface = _materials[x, hauteurSurface, z];
			}
			bool solTempere = matSurface == 1;
			bool solFroid = matSurface == 5 || matSurface == 9;
			if (!solTempere && !solFroid) continue;

			float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
			float humiditeNorm = (CalculerHumiditeGlobale(xGlobal, zGlobal) + 1f) * 0.5f;
			if (temperature < -0.15f)
			{
				if (!solFroid || humiditeNorm < 0.08f) continue;
			}
			else
			{
				if (!solTempere || humiditeNorm < 0.2f) continue;
			}

			var racine = new Vector3I(xGlobal, hauteurSurface + 1, zGlobal);
			uint seedArbre = (uint)((xGlobal * 73856093) ^ (zGlobal * 19349663));
			int stage = (int)(seedArbre % 10);
			InventaireArbres[racine] = new DonneesArbre { Stage = (byte)stage, Seed = seedArbre };
			return;
		}
	}

	/// <summary>Assure un minimum visuel : au moins un buisson s'il existe du gazon dans le chunk.</summary>
	private void AssurerBuissonMinimalDansChunk()
	{
		if (InventaireFlore.Count == 0) return;
		foreach (var kv in InventaireFlore)
			if (EstTypeBuisson(kv.Value))
				return;

		bool trouve = false;
		Vector3I candidat = default;
		uint hashMin = uint.MaxValue;
		foreach (var kv in InventaireFlore)
		{
			if (kv.Value != FloreTypeGazon) continue;
			uint h = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663) ^ (uint)(kv.Key.Y * 83492791);
			if (!trouve || h < hashMin)
			{
				hashMin = h;
				candidat = kv.Key;
				trouve = true;
			}
		}
		if (!trouve) return;
		InventaireFlore[candidat] = ConstruireTypeBuisson(VarianteCouleurBuissonRouge, true);
	}

	private int CalculerHauteurTerrain(int xGlobal, int zGlobal)
	{
		float bruitBrut = _noiseSurface.GetNoise2D(xGlobal, zGlobal);
		float bruitNormalise = (bruitBrut + 1.0f) / 2.0f;
		float relief = Mathf.Pow(bruitNormalise, 3.0f);  // Exposant 3 : plaine/collines/montagnes

		// Plaine : plaines basses 103-105 (biais fort vers 103) + plaine principale 105-118
		float bruitPlaine = _noiseErosion.GetNoise2D(xGlobal * 0.0003f, zGlobal * 0.0003f);
		float bruitVague = _noiseErosion.GetNoise2D(xGlobal * 0.0012f + 3000f, zGlobal * 0.0012f + 3000f);
		float bruitMicro = _noiseErosion.GetNoise2D(xGlobal * 0.005f + 5000f, zGlobal * 0.005f + 5000f);
		float mix = (bruitPlaine + 1f) * 0.5f * 0.4f + (bruitVague + 1f) * 0.5f * 0.35f + (bruitMicro + 1f) * 0.5f * 0.25f;
		mix = Mathf.Clamp(mix, 0f, 1f);
		float rampBase;
		if (mix < 0.6f) {
			float t = mix / 0.6f;
			rampBase = 103f + t * t * t * 2f;  // t³ : plus de temps à 103, peu à 105
		} else {
			float t = (mix - 0.6f) / 0.4f;
			rampBase = 105f + t * 13f;  // Plaine principale 105 → 118
		}

		// Tier 2 + Montagnes : plaine ~45%, tier2 ~30%, montagnes ~25%
		float tTier2 = Mathf.Clamp((relief - 0.09f) / 0.33f, 0f, 1f);
		float tMont = Mathf.Clamp((relief - 0.42f) / 0.58f, 0f, 1f);
		float hTier2 = tTier2 * tTier2 * 82f;
		float hMontagnes = tMont * tMont * 500f;  // Montagnes jusqu'à 700

		// Transition progressive base → tier2+montagnes (blend 0.05 → 0.20)
		float poidsBase = 1f - Mathf.Clamp((relief - 0.05f) / 0.15f, 0f, 1f);
		poidsBase = poidsBase * poidsBase * (3f - 2f * poidsBase);
		float hauteurHaut = 118f + hTier2 + hMontagnes;
		int hauteurBase = (int)(rampBase * poidsBase + hauteurHaut * (1f - poidsBase));
		float crevasseBrute = _noiseRivieres.GetNoise2D(xGlobal, zGlobal);
		int profondeurEau = 0;
		if (crevasseBrute > 0.12f)
		{
			float intensiteRiviera = (crevasseBrute - 0.12f) / 0.88f;
			float tSmooth = intensiteRiviera * intensiteRiviera * (3f - 2f * intensiteRiviera);  // Descente très douce vers l'eau
			profondeurEau = (int)(tSmooth * 22.0f);
		}
		return hauteurBase - profondeurEau;
	}

	private static float DeterministicRand(float x, float z)
	{
		uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
		return ((h % 10000) / 10000f);
	}

	private float CalculerHumiditeGlobale(float xGlobal, float zGlobal)
	{
		float macro = _noiseHumidite.GetNoise2D(xGlobal, zGlobal);
		float micro = _noiseHumiditeDetail != null ? _noiseHumiditeDetail.GetNoise2D(xGlobal, zGlobal) : 0f;
		return Mathf.Clamp(macro * 0.85f + micro * 0.15f, -1f, 1f);
	}

	/// <summary>Probabilité de transformer un gazon en buisson selon l'humidité locale.</summary>
	private float CalculerChanceBuisson(float xGlobal, float zGlobal)
	{
		float humiditeBrute = CalculerHumiditeGlobale(xGlobal, zGlobal);
		float humiditeNorm = (humiditeBrute + 1f) * 0.5f;
		if (humiditeNorm <= 0.28f) return 0f;
		float t = (humiditeNorm - 0.28f) / 0.72f;
		float chance = 0.003f + t * 0.045f;
		return Mathf.Clamp(chance, 0f, 0.05f);
	}

	/// <summary>Seuil de pente max (m) : si la hauteur varie de plus sur 1 m, pas de flore (évite lévitation sur bords).</summary>
	private const float SEUIL_PENTE_MAX = 0.12f;
	private const float MARGE_BORD_FLORE_METRES = 0.20f;

	/// <summary>Loi de l'inclinaison : vrai si le terrain est assez plat pour la flore.</summary>
	private bool TerrainAssezPlat(int xGlobal, int zGlobal)
	{
		float h0 = CalculerHauteurTerrain(xGlobal, zGlobal);
		float hauteurNord = CalculerHauteurTerrain(xGlobal, zGlobal + 1);
		float hauteurSud = CalculerHauteurTerrain(xGlobal, zGlobal - 1);
		float hauteurEst = CalculerHauteurTerrain(xGlobal + 1, zGlobal);
		float hauteurOuest = CalculerHauteurTerrain(xGlobal - 1, zGlobal);
		float diffMax = Mathf.Max(
			Mathf.Max(Mathf.Abs(hauteurNord - h0), Mathf.Abs(hauteurSud - h0)),
			Mathf.Max(Mathf.Abs(hauteurEst - h0), Mathf.Abs(hauteurOuest - h0))
		);
		return diffMax < SEUIL_PENTE_MAX;
	}

	private bool TerrainAvecMargeBord(int xGlobal, int zGlobal)
	{
		float h0 = CalculerHauteurTerrain(xGlobal, zGlobal);
		float hN = CalculerHauteurTerrain(xGlobal, zGlobal + 1);
		float hS = CalculerHauteurTerrain(xGlobal, zGlobal - 1);
		float hE = CalculerHauteurTerrain(xGlobal + 1, zGlobal);
		float hO = CalculerHauteurTerrain(xGlobal - 1, zGlobal);
		// Si on est à moins de 20 cm d'un "bord" (marche), on interdit le spawn.
		return Mathf.Abs(hN - h0) <= MARGE_BORD_FLORE_METRES
			&& Mathf.Abs(hS - h0) <= MARGE_BORD_FLORE_METRES
			&& Mathf.Abs(hE - h0) <= MARGE_BORD_FLORE_METRES
			&& Mathf.Abs(hO - h0) <= MARGE_BORD_FLORE_METRES;
	}

	/// <summary>Retourne (hauteur surface, matériau) pour ensemencement. (-1, 0) si pas de sol.</summary>
	public (int ySurface, byte mat) ObtenirSurfaceEtMateriau(int lx, int lz)
	{
		int y = ObtenirHauteurSurfaceLocale(lx, lz);
		if (y < 0) return (-1, 0);
		lock (_verrouVoxel)
		{
			byte mat = _materials[lx, y, lz];
			if (mat == 4) mat = 3; // Cécité hydrique : eau → sable
			return (y, mat);
		}
	}

	/// <summary>Hauteur de surface depuis les données chargées (chunks disque). -1 si hors limites ou pas de sol.</summary>
	private int ObtenirHauteurSurfaceLocale(int lx, int lz)
	{
		if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk || _densities == null) return -1;
		for (int y = HauteurMax - 1; y >= 2; y--)
			if (_densities[lx, y, lz] > Isolevel && (y + 1 >= HauteurMax + 1 || _densities[lx, y + 1, lz] <= Isolevel))
				return y;
		return -1;
	}

	/// <summary>Loi de l'inclinaison (chunks disque) : vrai si le terrain chargé est assez plat à (lx, lz).</summary>
	private bool TerrainAssezPlatDepuisDonnees(int lx, int lz)
	{
		int h0 = ObtenirHauteurSurfaceLocale(lx, lz);
		if (h0 < 0) return false;
		int hx1 = ObtenirHauteurSurfaceLocale(lx + 1, lz);
		int hx2 = ObtenirHauteurSurfaceLocale(lx - 1, lz);
		int hz1 = ObtenirHauteurSurfaceLocale(lx, lz + 1);
		int hz2 = ObtenirHauteurSurfaceLocale(lx, lz - 1);
		float d1 = hx1 >= 0 ? Mathf.Abs(hx1 - h0) : 0f;
		float d2 = hx2 >= 0 ? Mathf.Abs(hx2 - h0) : 0f;
		float d3 = hz1 >= 0 ? Mathf.Abs(hz1 - h0) : 0f;
		float d4 = hz2 >= 0 ? Mathf.Abs(hz2 - h0) : 0f;
		float diffMax = Mathf.Max(Mathf.Max(d1, d2), Mathf.Max(d3, d4));
		return diffMax < SEUIL_PENTE_MAX;
	}

	private bool TerrainAvecMargeBordDepuisDonnees(int lx, int lz)
	{
		int h0 = ObtenirHauteurSurfaceLocale(lx, lz);
		if (h0 < 0) return false;
		int hx1 = ObtenirHauteurSurfaceLocale(lx + 1, lz);
		int hx2 = ObtenirHauteurSurfaceLocale(lx - 1, lz);
		int hz1 = ObtenirHauteurSurfaceLocale(lx, lz + 1);
		int hz2 = ObtenirHauteurSurfaceLocale(lx, lz - 1);
		if (hx1 < 0 || hx2 < 0 || hz1 < 0 || hz2 < 0) return false;
		return Mathf.Abs(hx1 - h0) <= MARGE_BORD_FLORE_METRES
			&& Mathf.Abs(hx2 - h0) <= MARGE_BORD_FLORE_METRES
			&& Mathf.Abs(hz1 - h0) <= MARGE_BORD_FLORE_METRES
			&& Mathf.Abs(hz2 - h0) <= MARGE_BORD_FLORE_METRES;
	}

	private byte DeterminerMateriauCroûte(int xGlobal, int zGlobal, int globalY, int hauteurSurface, float temperature, float humidite)
	{
		float bruitNeige = _noiseNeige.GetNoise2D(xGlobal, zGlobal);
		float bruitRoche = _noiseNeige.GetNoise2D(xGlobal + 500f, zGlobal);
		int seuilNeigeLocal = SeuilNeigeBase + (int)(bruitNeige * 5f);   // 245-255
		int seuilRocheLocal = SeuilMontagneRoche + (int)(bruitRoche * 8f); // 200-215
		if (globalY >= seuilNeigeLocal) return 5;  // NEIGE
		if (globalY >= seuilRocheLocal) return 2;   // Roche nue
		if (globalY <= NiveauPlage) return (humidite > 0.2f) ? (byte)7 : (byte)3;  // Plage : seuil doux
		// Sable UNIQUEMENT quand très sec ET très chaud (temp + humidité liés logiquement)
		if (temperature > 0.5f && humidite < -0.5f) return 3;  // Désert : sable
		// Plusieurs stades temp/hum avec seuils progressifs (transitions lentes)
		if (temperature > 0.4f)  // Très chaud
		{
			if (humidite > 0.4f) return 8;   // Argile humide
			if (humidite > 0.1f) return 6;   // Terre aride
			return 1;   // Sec mais pas assez pour sable → herbe jaunâtre (shader)
		}
		if (temperature > 0.15f)  // Chaud
		{
			if (humidite > 0.35f) return 8;
			if (humidite > 0.0f) return 6;
			return 1;   // Sec → herbe (shader jaunâtre)
		}
		if (temperature < -0.4f) return 5;  // Très froid = toujours neige
		if (temperature < -0.15f)  // Froid
			return (humidite > 0.2f) ? (byte)9 : (byte)5;  // Glace si humide, neige sinon
		// Tempéré / Frais : humide → boue, sec → herbe (shader jaunâtre), entre-deux → herbe
		if (humidite < -0.35f) return 1;   // Très sec : herbe jaunâtre (shader)
		if (humidite < -0.15f) return 1;   // Sec : herbe
		if (humidite > 0.4f) return 7;     // Très humide : boue
		if (humidite > 0.2f) return 7;    // Humide : boue
		if (humidite > 0.05f) return 1;   // Légèrement humide : herbe
		return 1;  // Herbe par défaut
	}

	/// <summary>Tableau C# byte[] pour sauvegarde binaire. Format: densities (4×N) + materials (1×N) + densitiesEau (4×N).</summary>
	public byte[] ObtenirTableauBytes()
	{
		int tx = TailleChunk + 1, ty = HauteurMax + 1, tz = TailleChunk + 1;
		int voxelCount = tx * ty * tz;
		var bytes = new byte[voxelCount * 9];
		lock (_verrouVoxel)
		{
			int idx = 0;
			for (int x = 0; x < tx; x++)
				for (int y = 0; y < ty; y++)
					for (int z = 0; z < tz; z++)
					{
						Buffer.BlockCopy(BitConverter.GetBytes(_densities[x, y, z]), 0, bytes, idx, 4); idx += 4;
						bytes[idx++] = _materials[x, y, z];
						Buffer.BlockCopy(BitConverter.GetBytes(_densitiesEau[x, y, z]), 0, bytes, idx, 4); idx += 4;
					}
		}
		return bytes;
	}

	/// <summary>Sauvegarde binaire sur disque.</summary>
	public void SauvegarderChunkSurDisque()
	{
		string nom = GameState.Instance?.NomMondeActuel ?? "MonMonde";
		string dossierSave = ProjectSettings.GlobalizePath($"user://saves/{nom}/chunks/");
		Directory.CreateDirectory(dossierSave);
		string cheminFichier = Path.Combine(dossierSave, $"chunk_{ChunkOffsetX}_{ChunkOffsetZ}.bin");
		byte[] donnees = ObtenirTableauBytes();
		using (var writer = new BinaryWriter(File.Open(cheminFichier, FileMode.Create)))
		{
			writer.Write((byte)1);
			writer.Write(donnees.Length);
			writer.Write(donnees);
		}
		GD.Print($"ZERO-K : Cicatrice mémorisée. Chunk {ChunkOffsetX}_{ChunkOffsetZ} gravé sur le disque.");
		_estModifie = false;
	}

	/// <summary>Désérialise depuis byte[] (GetBuffer). Chunk chargé = pas modifié.</summary>
	public bool AppliquerTableauBytes(byte[] donnees)
	{
		int tx = TailleChunk + 1, ty = HauteurMax + 1, tz = TailleChunk + 1;
		int voxelCount = tx * ty * tz;
		int tailleAttendue = voxelCount * 9;
		if (donnees == null || donnees.Length != tailleAttendue) return false;

		lock (_verrouVoxel)
		{
			_densities = new float[tx, ty, tz];
			_materials = new byte[tx, ty, tz];
			_densitiesEau = new float[tx, ty, tz];
			int idx = 0;
			for (int x = 0; x < tx; x++)
				for (int y = 0; y < ty; y++)
					for (int z = 0; z < tz; z++)
					{
						_densities[x, y, z] = BitConverter.ToSingle(donnees, idx); idx += 4;
						_materials[x, y, z] = donnees[idx++];
						_densitiesEau[x, y, z] = BitConverter.ToSingle(donnees, idx); idx += 4;
					}
		}
		_estModifie = false;
		_chargeDepuisDisque = true; // MARQUER : ce chunk vient du disque — GenererDonneesVoxel ne doit JAMAIS le toucher.
		return true;
	}

	/// <summary>Flore fallback pour rétrocompatibilité si aucun fichier flore n’existe encore.</summary>
	public void RegenererInventaireFloreDepuisSurface()
	{
		InventaireFlore.Clear();
		GenererInventaireFloreDepuisSurface();
	}

	/// <summary>Migration douce: anciens chunks avec gazon seul -> injecte des buissons sans recréer toute la flore.</summary>
	public void EnrichirBuissonsDepuisInventaireSiAbsents()
	{
		if (InventaireFlore.Count == 0) return;
		foreach (var kv in InventaireFlore)
			if (EstTypeBuisson(kv.Value))
				return;

		var positions = new List<Vector3I>(InventaireFlore.Keys);
		foreach (var pos in positions)
		{
			if (!InventaireFlore.TryGetValue(pos, out byte typeFlore) || typeFlore != FloreTypeGazon) continue;
			float xGlobal = pos.X;
			float zGlobal = pos.Z;
			float chanceDePousse = CalculerChanceBuisson(xGlobal, zGlobal);
			if (chanceDePousse > 0f && DeterministicRand(xGlobal, zGlobal) < chanceDePousse)
				InventaireFlore[pos] = ConstruireTypeBuisson(VarianteCouleurBuissonRouge, DeterministicRand(xGlobal + 17f, zGlobal) < 0.5f);
		}
		AssurerBuissonMinimalDansChunk();
	}

	/// <summary>Scanne la surface chargée et remplit InventaireFlore (chunks du disque). Gazon partout sur ID 1.</summary>
	private void GenererInventaireFloreDepuisSurface()
	{
		for (int x = 0; x < TailleChunk; x++)
			for (int z = 0; z < TailleChunk; z++)
			{
				int ySurface = -1;
				for (int y = HauteurMax - 1; y >= 2; y--)
					if (_densities[x, y, z] > Isolevel && (y + 1 >= HauteurMax + 1 || _densities[x, y + 1, z] <= Isolevel))
					{ ySurface = y; break; }
				if (ySurface < 0) continue;
				byte mat = _materials[x, ySurface, z];
				if (!EstMateriauSupportGazon(mat)) continue;
				if (!TerrainAssezPlatDepuisDonnees(x, z)) continue;
				if (!TerrainAvecMargeBordDepuisDonnees(x, z)) continue;
				float xGlobal = ChunkOffsetX * TailleChunk + x;
				float zGlobal = ChunkOffsetZ * TailleChunk + z;
				float altitudeFlore = ySurface;
				if (altitudeFlore <= NIVEAU_MIN_FLORE || altitudeFlore >= NIVEAU_MAX_FLORE) continue;
				var posGlobale = new Vector3I((int)xGlobal, ySurface, (int)zGlobal);
				InventaireFlore[posGlobale] = FloreTypeGazon;
				float chanceDePousse = CalculerChanceBuisson(xGlobal, zGlobal);
				if (chanceDePousse > 0f && DeterministicRand(xGlobal, zGlobal) < chanceDePousse)
					InventaireFlore[posGlobale] = ConstruireTypeBuisson(VarianteCouleurBuissonRouge, DeterministicRand(xGlobal + 17f, zGlobal) < 0.5f);
			}
		AssurerBuissonMinimalDansChunk();
	}

	private static bool EstMateriauSupportGazon(byte mat)
	{
		// Gazon uniquement sur voxel herbe (ID 1).
		return mat == 1;
	}

	/// <summary>Crible gravitationnel : purger les buissons dont le bloc support a été miné (évite lévitation).</summary>
	public void AuditerGraviteFlore()
	{
		if (InventaireFlore.Count == 0) return;
		var floreMorte = new List<Vector3I>();
		lock (_verrouVoxel)
		{
			foreach (var kv in InventaireFlore)
			{
				Vector3I posGlobale = kv.Key;
				int lx = posGlobale.X - ChunkOffsetX * TailleChunk;
				int ly = posGlobale.Y;
				int lz = posGlobale.Z - ChunkOffsetZ * TailleChunk;
				if (!EstDansLimitesChunk(lx, ly, lz)) continue;
				if (_densities[lx, ly, lz] <= Isolevel) floreMorte.Add(posGlobale);
			}
		}
		if (floreMorte.Count == 0) return;
		foreach (var mort in floreMorte)
		{
			if (InventaireFlore.TryGetValue(mort, out byte typeFlore))
			{
				Vector3 posSpawn = new Vector3(mort.X + 0.5f, mort.Y + 0.5f, mort.Z + 0.5f);
				byte idItem = typeFlore == FloreTypeGazon ? (byte)15 : (byte)(EstBuissonPlein(typeFlore) ? ID_ITEM_BUISSON_PLEIN : ID_ITEM_BUISSON_VIDE);
				_callbackBlocChutant?.Invoke(posSpawn, idItem);
			}
			InventaireFlore.Remove(mort);
		}
		_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), new Dictionary<Vector3I, byte>(InventaireFlore));
	}

	/// <summary>Copie les données du chunk pour envoi au client. Quantification byte[] pour RPC (divise poids par 4).</summary>
	public DonneesChunk ObtenirDonneesPourClient()
	{
		lock (_verrouVoxel)
		{
			int tx = TailleChunk + 1, ty = HauteurMax + 1, tz = TailleChunk + 1;
			var d = new DonneesChunk
			{
				CoordChunk = new Vector2I(ChunkOffsetX, ChunkOffsetZ),
				TailleChunk = TailleChunk,
				HauteurMax = HauteurMax,
				DensitiesQuantifiees = DonneesChunk.CompresserDensitesPourReseau(_densities, tx, ty, tz),
				DensitiesEauQuantifiees = DonneesChunk.CompresserDensitesPourReseau(_densitiesEau, tx, ty, tz),
				MaterialsFlat = new byte[tx * ty * tz],
				InventaireFlore = new Dictionary<Vector3I, byte>(InventaireFlore)
			};
			int idx = 0;
			for (int x = 0; x < tx; x++)
				for (int y = 0; y < ty; y++)
					for (int z = 0; z < tz; z++)
						d.MaterialsFlat[idx++] = _materials[x, y, z];
			return d;
		}
	}

	private const byte ID_ITEM_BUISSON_PLEIN = 10;
	private const byte ID_ITEM_BUISSON_VIDE = 11;
private const byte ID_ITEM_BRANCHE_BUISSON = BlocChutant.ID_BRANCHE;
private const byte ID_ITEM_BAIE = Joueur.IdObjetBaie;

private int TirerQuantiteBaiesDepuisSeed(int seed)
{
	var rng = new RandomNumberGenerator { Seed = unchecked((ulong)(uint)seed) };
	int roll = rng.RandiRange(1, 100);
	if (roll <= 60) return 1;
	if (roll <= 85) return 2;
	if (roll <= 95) return 3;
	return 4;
}

private void FaireTomberBaiesAuSolSiPlein(Vector3I posFlore, byte typeFlore, int quantiteForcee = -1)
{
	if (!EstBuissonPlein(typeFlore)) return;
	int q = quantiteForcee > 0
		? quantiteForcee
		: TirerQuantiteBaiesDepuisSeed((posFlore.X * 73856093) ^ (posFlore.Y * 19349663) ^ (posFlore.Z * 83492791) ^ 0x6B35);
	var rng = new RandomNumberGenerator { Seed = unchecked((ulong)(uint)((posFlore.X * 911) ^ (posFlore.Z * 353) ^ 0xBEE35)) };
	Vector3 basePos = new Vector3(posFlore.X + 0.5f, posFlore.Y + 0.72f, posFlore.Z + 0.5f);
	for (int i = 0; i < q; i++)
	{
		Vector3 offset = new Vector3(rng.RandfRange(-0.22f, 0.22f), rng.RandfRange(0.02f, 0.14f), rng.RandfRange(-0.22f, 0.22f));
		_callbackBlocChutant?.Invoke(basePos + offset, ID_ITEM_BAIE);
	}
}

private bool EssayerTrouverBuissonLePlusProche(Vector3 pointImpactGlobal, float rayon, out Vector3I posFlore, out byte typeFlore)
{
	posFlore = default;
	typeFlore = 0;
	float rayon2 = rayon * rayon;
	const float demiEpaisseurVerticale = 5f;
	float meilleureDist2 = float.MaxValue;
	bool trouve = false;
	foreach (var kv in InventaireFlore)
	{
		if (!EstTypeBuisson(kv.Value)) continue;
		float dx = (kv.Key.X + 0.5f) - pointImpactGlobal.X;
		float dz = (kv.Key.Z + 0.5f) - pointImpactGlobal.Z;
		float d2 = dx * dx + dz * dz;
		if (d2 > rayon2) continue;
		float dy = Mathf.Abs((kv.Key.Y + 0.5f) - pointImpactGlobal.Y);
		if (dy > demiEpaisseurVerticale) continue;
		if (!trouve || d2 < meilleureDist2)
		{
			trouve = true;
			meilleureDist2 = d2;
			posFlore = kv.Key;
			typeFlore = kv.Value;
		}
	}
	return trouve;
}

/// <summary>Détection locale d’un buisson sous la visée (sans le récolter).</summary>
public bool EssayerDetecterBuisson(Vector3 pointImpactGlobal, float rayon, out Vector3 posBuisson, out byte typeFlore)
{
	posBuisson = Vector3.Zero;
	typeFlore = 0;
	if (!EssayerTrouverBuissonLePlusProche(pointImpactGlobal, rayon, out Vector3I pos, out byte type))
		return false;
	posBuisson = new Vector3(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f);
	typeFlore = type;
	return true;
}

/// <summary>Récolte ciblée buisson: 0=hachette (branche), 1=dague (coupe), 2=pelle (plante replantable).</summary>
public bool RecolterBuisson(Vector3 pointImpactGlobal, float rayon, byte modeRecolte)
{
	if (!EssayerTrouverBuissonLePlusProche(pointImpactGlobal, rayon, out Vector3I posFlore, out byte typeFlore))
		return false;

	Vector3 posSpawn = new Vector3(posFlore.X + 0.5f, posFlore.Y + 0.5f, posFlore.Z + 0.5f);
	switch (modeRecolte)
	{
		case 0: // Hachette: coupe de branche, le buisson plein devient buisson vide.
			FaireTomberBaiesAuSolSiPlein(posFlore, typeFlore);
			_callbackBlocChutant?.Invoke(posSpawn + new Vector3(0f, 0.08f, 0f), ID_ITEM_BRANCHE_BUISSON);
			if (EstBuissonPlein(typeFlore)) InventaireFlore[posFlore] = TypeBuissonSansBaies(typeFlore);
			else InventaireFlore.Remove(posFlore);
			break;

		case 1: // Dague maintenue: coupe après 3s -> 1 branche + 1 baie (si buisson plein), sans drop buisson.
			FaireTomberBaiesAuSolSiPlein(posFlore, typeFlore, 1);
			_callbackBlocChutant?.Invoke(posSpawn + new Vector3(0f, 0.08f, 0f), ID_ITEM_BRANCHE_BUISSON);
			InventaireFlore.Remove(posFlore);
			break;

		case 2: // Pelle maintenue: déracine et récupère la plante replantable.
			FaireTomberBaiesAuSolSiPlein(posFlore, typeFlore);
			InventaireFlore.Remove(posFlore);
			// Pelle: on récupère la plante "sans baies"; les baies d'un buisson plein tombent déjà au sol juste au-dessus.
			_callbackBlocChutant?.Invoke(posSpawn + new Vector3(0f, 0.06f, 0f), ID_ITEM_BUISSON_VIDE);
			break;

		default:
			return false;
	}

	_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), new Dictionary<Vector3I, byte>(InventaireFlore));
	return true;
}

/// <summary>Plante un buisson (1 plein, 2 vide) sur la surface locale du chunk.</summary>
public bool PlanterBuisson(Vector3 pointImpactGlobal, byte typeFlore)
{
	if (!EstTypeBuisson(typeFlore)) return false;
	Gestionnaire_Monde.WorldToChunkAndLocal(pointImpactGlobal.X, pointImpactGlobal.Z, TailleChunk, out Vector2I c, out int lx, out int lz);
	if (c.X != ChunkOffsetX || c.Y != ChunkOffsetZ) return false;
	if (lx < 0 || lx >= TailleChunk || lz < 0 || lz >= TailleChunk) return false;
	int ySurface = ObtenirHauteurSurfaceLocale(lx, lz);
	if (ySurface < 0) return false;
	lock (_verrouVoxel)
	{
		if (!EstMateriauSupportGazon(_materials[lx, ySurface, lz])) return false;
	}
	if (!TerrainAssezPlatDepuisDonnees(lx, lz)) return false;
	if (!TerrainAvecMargeBordDepuisDonnees(lx, lz)) return false;
	var posGlobale = new Vector3I(ChunkOffsetX * TailleChunk + lx, ySurface, ChunkOffsetZ * TailleChunk + lz);
	InventaireFlore[posGlobale] = typeFlore;
	_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), new Dictionary<Vector3I, byte>(InventaireFlore));
	return true;
}

/// <summary>Récolte des baies via interaction: uniquement buisson plein (type 1), qui devient ensuite buisson vide (type 2).</summary>
public bool RecolterBaiesBuisson(Vector3 pointImpactGlobal, float rayon, out int quantiteBaies, out byte indexCouleurBaie)
{
	quantiteBaies = 0;
	indexCouleurBaie = 0; // 0 = rouge (palette future extensible)
	if (!EssayerTrouverBuissonLePlusProche(pointImpactGlobal, rayon, out Vector3I posFlore, out byte typeFlore))
		return false;
	if (!EstBuissonPlein(typeFlore)) // Buisson déjà vide: aucune baie à ramasser.
		return false;

	InventaireFlore[posFlore] = TypeBuissonSansBaies(typeFlore);
	_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), new Dictionary<Vector3I, byte>(InventaireFlore));

	quantiteBaies = TirerQuantiteBaiesDepuisSeed((posFlore.X * 73856093) ^ (posFlore.Y * 19349663) ^ (posFlore.Z * 83492791) ^ unchecked((int)Time.GetTicksUsec()));
	return true;
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
			_callbackBlocChutant?.Invoke(posSpawn, idItem);
		}
		if (floreDetruite.Count > 0)
			_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), new Dictionary<Vector3I, byte>(InventaireFlore));

		lock (_verrouVoxel)
		{
			float rayon2 = rayonExplosion * rayonExplosion;
			bool modifie = false;

			for (int x = 0; x <= TailleChunk; x++)
				for (int y = 0; y <= HauteurMax; y++)
					for (int z = 0; z <= TailleChunk; z++)
					{
						if (y <= 2) continue;
						float dx = pointLocal.X - x, dy = pointLocal.Y - y, dz = pointLocal.Z - z;
						if (dx * dx + dy * dy + dz * dz <= rayon2)
						{
							bool etaitSolide = _densities[x, y, z] > Isolevel;
							_densities[x, y, z] = Mathf.Max(_densities[x, y, z] - forceDegats, -1.0f); // Plancher absolu : le voxel ne peut pas être "plus que vide"
							modifie = true;
							if (etaitSolide) positionsDetruites.Add(new Vector3I(x, y, z));
						}
					}
			if (!modifie) return;
			_estModifie = true; // Joueur a miné → sauvegarde obligatoire au déchargement.
			foreach (var pos in positionsDetruites) VerifierStabilite(pos);
		}

		int baseX = ChunkOffsetX * TailleChunk;
		int baseZ = ChunkOffsetZ * TailleChunk;

		foreach (var pos in positionsDetruites)
		{
			_reveillerEau?.Invoke(PositionMonde + new Vector3(pos.X, pos.Y, pos.Z));
			int gx = baseX + pos.X;
			int gz = baseZ + pos.Z;
			var posGlobal = new Vector3I(gx, pos.Y, gz);
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
			for (int x = 0; x <= TailleChunk; x++)
				for (int y = 0; y <= HauteurMax; y++)
					for (int z = 0; z <= TailleChunk; z++)
					{
						if (y <= 2) continue;
						float dx = pointLocal.X - x, dy = pointLocal.Y - y, dz = pointLocal.Z - z;
						if (dx * dx + dy * dy + dz * dz <= rayon2)
						{
							_densities[x, y, z] = Mathf.Min(_densities[x, y, z] + 5.0f, 1.0f); // Plafond absolu : le voxel ne peut pas être "plus que plein"
							_materials[x, y, z] = idMatiere; // Injection couleur : le Shader lit ce tableau
							positionsModifiees.Add(new Vector3I(x, y, z));
						}
					}
			if (positionsModifiees.Count == 0) return;
			_estModifie = true; // Joueur a placé des blocs → sauvegarde obligatoire.
		}

		foreach (var pos in positionsModifiees)
		{
			_reveillerEau?.Invoke(PositionMonde + new Vector3(pos.X, pos.Y, pos.Z));
			int gx = Mathf.FloorToInt(PositionMonde.X) + pos.X;
			int gz = Mathf.FloorToInt(PositionMonde.Z) + pos.Z;
			var posGlobal = new Vector3I(gx, pos.Y, gz);
			_onVoxelModifie?.Invoke(posGlobal, idMatiere);
		}
		AuditerGraviteFlore();
	}

	private List<int> ObtenirSectionsAffectees(List<Vector3I> positions)
	{
		const int HAUTEUR_SECTION = 16, NB_SECTIONS = 45;  // 45×16 = 720 (HauteurMax)
		var sections = new HashSet<int>();
		foreach (var pos in positions)
		{
			int idx = Mathf.FloorToInt(pos.Y / (float)HAUTEUR_SECTION);
			if (idx >= 0 && idx < NB_SECTIONS) sections.Add(idx);
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
		_callbackBlocChutant?.Invoke(PositionMonde + new Vector3(xu + 0.5f, yu + 0.5f, zu + 0.5f), mat);

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

	public void DefinirVoxelEau(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z) || y <= 2) return;
		lock (_verrouVoxel)
		{
			_densities[x, y, z] = -10.0f;
			_materials[x, y, z] = 4;
			if (_densitiesEau != null) _densitiesEau[x, y, z] = 1.0f;
		}
		AuditerGraviteFlore();
	}

	public void DefinirVoxelAir(int x, int y, int z)
	{
		if (!EstDansLimitesChunk(x, y, z)) return;
		lock (_verrouVoxel)
		{
			_densities[x, y, z] = -10.0f;
			_materials[x, y, z] = 0;
			if (_densitiesEau != null) _densitiesEau[x, y, z] = -1.0f;
		}
		AuditerGraviteFlore();
	}

	/// <summary>Met à jour un voxel aux coords locales (réplication du padding des voisins).</summary>
	public void SetVoxelLocal(int lx, int ly, int lz, byte id)
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
		_estModifie = true;
		AuditerGraviteFlore();
	}

	/// <summary>Met à jour un voxel local ET notifie le client (croissance arbres).</summary>
	public void ModifierVoxelEtNotifier(int lx, int ly, int lz, byte id)
	{
		SetVoxelLocal(lx, ly, lz, id);
		_estModifie = true;
		var posGlobal = new Vector3I(ChunkOffsetX * TailleChunk + lx, ly, ChunkOffsetZ * TailleChunk + lz);
		_onVoxelModifie?.Invoke(posGlobal, id);
	}

	public void FaucherFlore(Vector3 pointImpactGlobal, float rayon)
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
		if (floreDetruite.Count == 0) return;

		foreach (var kv in floreDetruite)
		{
			InventaireFlore.Remove(kv.Key);
			Vector3 posSpawn = new Vector3(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Key.Z + 0.5f);
			_callbackBlocChutant?.Invoke(posSpawn, 15);
		}
		_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), new Dictionary<Vector3I, byte>(InventaireFlore));
	}
}
