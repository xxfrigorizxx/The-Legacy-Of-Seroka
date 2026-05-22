using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

/// <summary>Données voxel et logique de génération pour un chunk. Aucun MeshInstance3D.</summary>
public partial class Chunk_Serveur : RefCounted
{
	public int TailleChunk { get; }
	public int HauteurMax { get; }
	public int ChunkOffsetX { get; }
	public int ChunkOffsetY { get; }
	public int ChunkOffsetZ { get; }
	public Vector3 PositionMonde { get; }

	private float[,,] _densities;
	private float[,,] _densitiesEau;
	private byte[,,] _materials;
	private readonly object _verrouVoxel = new object();
	private readonly bool _generationAbysseActive;
	private readonly string _dossierChunksSauvegarde;

	private FastNoiseLite _noiseSurface;
	private FastNoiseLite _noiseErosion;
	private FastNoiseLite _noiseTemperature;
	private FastNoiseLite _noiseHumidite;
	private FastNoiseLite _noiseHumiditeDetail;
	private FastNoiseLite _noiseCavernes;
	private FastNoiseLite _noiseRivieres;
	private FastNoiseLite _noiseNeige;
	private FastNoiseLite _noiseBiomeForet;
	private FastNoiseLite _noiseAbysseSpirale3D;
	private FastNoiseLite _noiseAbysseChaos3D;
	private FastNoiseLite _noiseAbysseChaosDetail3D;

	private const float Isolevel = 0.0f;
	private const int NiveauEau = 103;  // +1 m
	private const int ProfondeurBase = 104;
	private const int AmplitudeMontagne = 250;  // Montagnes adoucies (environ moitié de la hauteur précédente)
	private const int NiveauPlage = 102;  // Sable jusqu'à 102, herbe à 103-104 (niveau eau inchangé)
	private const int SeuilNeigeBase = 250;   // Neige 245-255 (bruit ±5)
	private const int SeuilMontagneRoche = 207; // Roche 200-215 (bruit ±8)
	private static readonly Vector3I[] DirPropagationEauInitiale = {
		new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0),
		new Vector3I(0, 1, 0), new Vector3I(0, -1, 0),
		new Vector3I(0, 0, 1), new Vector3I(0, 0, -1)
	};
	private static readonly Vector2I[] DirCardinales2D = {
		new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1)
	};
	/// <summary>Limites altitude flore. Inclut la zone de spawn (herbe haute).</summary>
	private const float NIVEAU_MIN_FLORE = 5f;
	private const float NIVEAU_MAX_FLORE = 260f;
	private const float AbyssRayonTrouNoir = 500f;
	// Profil radial "atoll" calé visuellement sur la carte de référence:
	// trou central large, plaine intérieure, muraille courte/violente,
	// plaine extérieure, liseré sable, puis océan.
	private const float AbyssRayonX = 900f;
	private const float AbyssRayonY = 1100f;
	private const float AbyssRayonZ = 1450f;
	private const float AbyssRayonW = 1600f;
	private const float AbyssFondAbsolu = ConstantesDimensionAbysse.FondAbsolu;
	private const float AbyssAltitudeSanctuaire = 20f;
	private const int AbyssNiveauEau = 19;
	private const float AbyssYSpiraleTop = 20f;
	private const float AbyssYSpiraleBottom = -450f;
	private const float AbyssYAnneauTransitionBottom = -510f;
	private const float AbyssRayonSpiraleMin = 400f;
	private const float AbyssRayonSpiraleMax = 500f;
	private const float AbyssExtrusionMin = 6f;
	private const float AbyssExtrusionMax = 72f;
	private const float AbyssChaosExtrusionMin = 18f;
	private const float AbyssChaosExtrusionMax = 375f;
	private const float AbyssChaosYScale = 0.30f;
	/// <summary>Nombre de pics angulaires (période par tour) dans l'anneau [-510,-450[ — élevé = champ dense après l'arrêt de la spirale.</summary>
	private const float AbyssPicAnneauNombre = 54f;
	/// <summary>Pics beaucoup plus rares dans la zone spirale (supplément hors rampe).</summary>
	private const float AbyssPicSpiraleNombre = 14f;
	/// <summary>Petit affleurement praticable à l'angle de sortie de spirale (m, profondeur max) — sans dalle large.</summary>
	private const float AbyssPicLipSortieProfondeurMax = 22f;
	/// <summary>Plateau du fond du goufre (herbe + baies cyan) : rayon autour de l'origine monde XZ.</summary>
	private const float AbyssRayonPlateauCentralFlore = 115f;
	private const float AbyssChanceBuissonFluo = 0.022f;
	private const float AbyssChanceBuissonFluoCentre = 0.11f;
	/// <summary>Extrusion 3D (spirale, pics) : uniquement près du goufre — hors muraille (900–1100 m) inutile de scanner.</summary>
	private const float AbyssRayonProfilExtrusionMax = AbyssRayonTrouNoir + 110f;

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

	private Action<Vector3, byte, bool, byte> _callbackBlocChutant;
	private Func<Vector2I, bool> _chunkEstCharge;
	private Action<Vector3> _reveillerEau;
	private Action<Vector3I, byte> _onVoxelModifie;
	private Action<Vector2I, int, Dictionary<Vector3I, byte>> _onFlorePurgée;

	/// <summary>Drapeau de souillure : true dès qu'un voxel persistant (sol/eau/air) change réellement.</summary>
	private bool _estModifie;
	/// <summary>True si chargé depuis disque. AUCUNE passe de génération ne doit jamais s'exécuter sur ce chunk.</summary>
	private bool _chargeDepuisDisque;

	public bool EstModifie => _estModifie;
	public bool EstChargeDepuisDisque => _chargeDepuisDisque;
	internal void MarquerModifie() => _estModifie = true;

	/// <summary>Repeuple <see cref="InventaireArbres"/> en rejouant uniquement la passe procédurale d'arbres sur les voxels actuels.
	/// Utilisé en migration sur saves Abysse antérieures à la persistance des arbres : ne touche pas <c>_densities/_materials</c>.</summary>
	public void RegenererInventaireArbresProcedural()
	{
		if (_densities == null) return;
		InventaireArbres.Clear();
		InjecterArbresLSystem();
	}

	public void SetOnVoxelModifie(Action<Vector3I, byte> callback) => _onVoxelModifie = callback;
	public void SetOnFlorePurgée(Action<Vector2I, int, Dictionary<Vector3I, byte>> callback) => _onFlorePurgée = callback;

	public Chunk_Serveur(int chunkOffsetX, int chunkOffsetY, int chunkOffsetZ, int tailleChunk, int hauteurMax, int seed,
		Action<Vector3, byte, bool, byte> callbackBlocChutant, Func<Vector2I, bool> chunkEstCharge, Action<Vector3> reveillerEau,
		bool generationAbysse = false, string dossierChunksSauvegarde = "")
	{
		ChunkOffsetX = chunkOffsetX;
		ChunkOffsetY = chunkOffsetY;
		ChunkOffsetZ = chunkOffsetZ;
		TailleChunk = tailleChunk;
		HauteurMax = hauteurMax;
		PositionMonde = new Vector3(chunkOffsetX * tailleChunk, chunkOffsetY * hauteurMax, chunkOffsetZ * tailleChunk);
		_callbackBlocChutant = callbackBlocChutant;
		_chunkEstCharge = chunkEstCharge;
		_reveillerEau = reveillerEau;
		_generationAbysseActive = generationAbysse;
		_dossierChunksSauvegarde = dossierChunksSauvegarde ?? "";

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

		// Macro-biomes forestiers tempérés (zones: sans arbres, bouleaux, chênes, mixte).
		_noiseBiomeForet = new FastNoiseLite();
		_noiseBiomeForet.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseBiomeForet.Seed = seed + 77;
		_noiseBiomeForet.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseBiomeForet.FractalOctaves = 3;
		_noiseBiomeForet.Frequency = 0.00028f;

		_noiseAbysseSpirale3D = new FastNoiseLite();
		_noiseAbysseSpirale3D.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
		_noiseAbysseSpirale3D.Seed = seed + 9137;
		_noiseAbysseSpirale3D.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
		_noiseAbysseSpirale3D.FractalOctaves = 3;
		_noiseAbysseSpirale3D.Frequency = 0.028f;

		_noiseAbysseChaos3D = new FastNoiseLite();
		_noiseAbysseChaos3D.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
		_noiseAbysseChaos3D.Seed = seed + 9241;
		_noiseAbysseChaos3D.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
		_noiseAbysseChaos3D.FractalOctaves = 4;
		_noiseAbysseChaos3D.Frequency = 0.0135f;

		_noiseAbysseChaosDetail3D = new FastNoiseLite();
		_noiseAbysseChaosDetail3D.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noiseAbysseChaosDetail3D.Seed = seed + 9242;
		_noiseAbysseChaosDetail3D.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noiseAbysseChaosDetail3D.FractalOctaves = 3;
		_noiseAbysseChaosDetail3D.Frequency = 0.026f;
	}

	public bool EstPret => _densities != null;

	/// <summary>TOUTES les passes procédurales (terrain, surface, herbe, eau). NE DOIT JAMAIS s'exécuter sur un chunk chargé du disque.</summary>
	public void GenererDonneesVoxel()
	{
		if (_chargeDepuisDisque) return; // GARDE ABSOLUE : chunk ressuscité du disque — aucune modification mathématique.
		lock (_verrouVoxel)
		{
			if (_generationAbysseActive)
				InventaireFlore.Clear();
			_densities = new float[TailleChunk + 1, HauteurMax + 1, TailleChunk + 1];
			_materials = new byte[TailleChunk + 1, HauteurMax + 1, TailleChunk + 1];
			_densitiesEau = new float[TailleChunk + 1, HauteurMax + 1, TailleChunk + 1];
			int taille = TailleChunk + 1;
			int[,] sommetSolide = new int[taille, taille];
			bool activerGrottes = !_generationAbysseActive;
			for (int x = 0; x < taille; x++)
				for (int z = 0; z < taille; z++)
					sommetSolide[x, z] = -1;

			// Hauteur / climat par colonne (x,z) : évite ~700× recalculs bruit 2D/3D par chunk en Abysse.
			var hauteurColonne = new int[taille, taille];
			var temperatureColonne = new float[taille, taille];
			var humiditeColonne = new float[taille, taille];
			bool[,] dansTrouNoirColonne = _generationAbysseActive ? new bool[taille, taille] : null;
			for (int x = 0; x < taille; x++)
			{
				for (int z = 0; z < taille; z++)
				{
					float xGlobal = ChunkOffsetX * TailleChunk + x;
					float zGlobal = ChunkOffsetZ * TailleChunk + z;
					int xInt = (int)xGlobal;
					int zInt = (int)zGlobal;
					hauteurColonne[x, z] = CalculerHauteurTerrain(xInt, zInt);
					if (_generationAbysseActive)
					{
						float distanceRadiale = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
						dansTrouNoirColonne[x, z] = distanceRadiale <= AbyssRayonTrouNoir;
						temperatureColonne[x, z] = 0f;
						humiditeColonne[x, z] = 0f;
					}
					else
					{
						temperatureColonne[x, z] = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
						humiditeColonne[x, z] = CalculerHumiditeGlobale(xGlobal, zGlobal);
					}
				}
			}

			const float yMinBandeExtrusionAbysse = AbyssYAnneauTransitionBottom - 1f;
			const float yMaxBandeExtrusionAbysse = AbyssYSpiraleTop + 1f;

			for (int x = 0; x < taille; x++)
			{
				for (int z = 0; z < taille; z++)
				{
					float xGlobal = ChunkOffsetX * TailleChunk + x;
					float zGlobal = ChunkOffsetZ * TailleChunk + z;
					int hauteurSurface = hauteurColonne[x, z];
					float temperature = temperatureColonne[x, z];
					float humidite = humiditeColonne[x, z];
					bool dansTrouNoirCol = _generationAbysseActive && dansTrouNoirColonne[x, z];

					bool colonneExtrusionTrou = _generationAbysseActive && ColonneDansBandeExtrusionTrou(xGlobal, zGlobal);
					int yDebutCol = 0;
					int yFinCol = HauteurMax;
					if (_generationAbysseActive)
					{
						if (dansTrouNoirCol || colonneExtrusionTrou)
							ObtenirPlageIndiceYMonde(yMinBandeExtrusionAbysse, yMaxBandeExtrusionAbysse, out yDebutCol, out yFinCol);
						else if (hauteurSurface >= ChunkOffsetY * HauteurMax - 2
							&& hauteurSurface <= ChunkOffsetY * HauteurMax + HauteurMax + 2)
							ObtenirPlageIndiceYMonde(hauteurSurface - 10f, hauteurSurface + 14f, out yDebutCol, out yFinCol);
						else
						{
							yDebutCol = 1;
							yFinCol = 0;
						}
					}

					for (int y = yDebutCol; y <= yFinCol; y++)
					{
						float globalY = ChunkOffsetY * HauteurMax + y;
						bool extrusionParoiAbysse = false;
						bool extrusionAnneauAbysse = false;
						bool picSupplementSpiraleAbysse = false;
						if (colonneExtrusionTrou
							&& globalY >= yMinBandeExtrusionAbysse
							&& globalY <= yMaxBandeExtrusionAbysse)
						{
							extrusionParoiAbysse = EvaluerExtrusionParoiAbysse(xGlobal, globalY, zGlobal, out _);
							if (!extrusionParoiAbysse)
								extrusionAnneauAbysse = EvaluerExtrusionAnneauAbysse(xGlobal, globalY, zGlobal, out _);
							if (!extrusionParoiAbysse && !extrusionAnneauAbysse)
								picSupplementSpiraleAbysse = EvaluerPicSupplementSpiraleAbysse(xGlobal, globalY, zGlobal, out _);
						}

						if (extrusionParoiAbysse || extrusionAnneauAbysse || picSupplementSpiraleAbysse)
						{
							_densities[x, y, z] = 10.0f;
							_materials[x, y, z] = 2;
							_densitiesEau[x, y, z] = -1.0f;
							sommetSolide[x, z] = y;
							continue;
						}

						_densitiesEau[x, y, z] = -1.0f;

						bool socleZeroMonde = globalY >= 0f
							&& globalY <= 2f
							&& !dansTrouNoirCol;
						if (socleZeroMonde)
						{
							_densities[x, y, z] = 1000.0f;
							_materials[x, y, z] = 2;
							sommetSolide[x, z] = y;
						}
						else if (globalY == hauteurSurface)
						{
							byte mat = DeterminerMateriauCroûte((int)xGlobal, (int)zGlobal, (int)globalY, hauteurSurface, temperature, humidite);
							_materials[x, y, z] = mat;
							_densities[x, y, z] = 10.0f;
							sommetSolide[x, z] = y;
							// Gazon sur voxel herbe (ID 1), terrain plat — Alpha classique ou deux plaines APISARA (jungle).
							bool floreAlpha = !_generationAbysseActive
								&& EstMateriauSupportGazon(mat)
								&& TerrainAssezPlat((int)xGlobal, (int)zGlobal)
								&& TerrainAvecMargeBord((int)xGlobal, (int)zGlobal);
							bool florePlaineJungleAbysse = _generationAbysseActive
								&& EstPlaineJungleAbysse(xGlobal, zGlobal)
								&& EstMateriauSupportGazon(mat)
								&& TerrainAssezPlat((int)xGlobal, (int)zGlobal)
								&& TerrainAvecMargeBord((int)xGlobal, (int)zGlobal);
							if (floreAlpha || florePlaineJungleAbysse)
							{
								float altitudeFlore = globalY;
								if (altitudeFlore > NIVEAU_MIN_FLORE && altitudeFlore < NIVEAU_MAX_FLORE)
								{
									var posGlobale = new Vector3I((int)xGlobal, (int)globalY, (int)zGlobal);
									InventaireFlore[posGlobale] = FloreTypeGazon;
									EssayerPromouvoirGazonEnBuisson(posGlobale, xGlobal, zGlobal);
								}
							}
						}
						else if (globalY < hauteurSurface && globalY >= hauteurSurface - 4)
						{
							float valeurGrotte = activerGrottes ? _noiseCavernes.GetNoise3D(xGlobal, globalY, zGlobal) : -1f;
							if (activerGrottes && valeurGrotte > 0.75f)
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
								sommetSolide[x, z] = y;
							}
						}
						else if (globalY < hauteurSurface - 4)
						{
							float valeurGrotte = activerGrottes ? _noiseCavernes.GetNoise3D(xGlobal, globalY, zGlobal) : -1f;
							if (activerGrottes && valeurGrotte > 0.55f)
							{
								_densities[x, y, z] = -10.0f;
								_materials[x, y, z] = 0;
							}
							else
							{
								_densities[x, y, z] = 10.0f;
								_materials[x, y, z] = 2;
								sommetSolide[x, z] = y;
							}
						}
						else
						{
							_densities[x, y, z] = -10.0f;
							_materials[x, y, z] = 0;
						}
					}
				}
			}
			AppliquerBiomeParasiteCornichesAbysse();
			AppliquerEnsemencementFloreTrouAbysse(notifierClient: false);
			InitialiserEauVolumetrique(sommetSolide);

			// Pass L-System : injection des Chênes (voxels bois ID 30, feuilles ID 31)
			InjecterArbresLSystem();
			// Garantie de lisibilité gameplay: au moins un buisson si le chunk contient du gazon.
			AssurerBuissonMinimalDansChunk();
			// RÈGLE : Chunk procédural non touché par le joueur → jamais sauvegardé (régénération à la demande).
		}
	}

	/// <summary>
	/// Injection initiale d'eau volumétrique (une seule fois à la génération du chunk).
	/// Un voxel devient eau uniquement s'il est connecté à une colonne ouverte au ciel sous le niveau d'eau.
	/// </summary>
	private void InitialiserEauVolumetrique(int[,] sommetSolide)
	{
		if (_densities == null || _densitiesEau == null || _materials == null) return;
		int yMaxEau = Math.Min(ObtenirNiveauEauActif(), HauteurMax);
		if (yMaxEau <= 2) return;

		var file = new Queue<Vector3I>();
		for (int x = 0; x <= TailleChunk; x++)
		{
			for (int z = 0; z <= TailleChunk; z++)
			{
				float xGlobal = ChunkOffsetX * TailleChunk + x;
				float zGlobal = ChunkOffsetZ * TailleChunk + z;
				// Le cœur abyssal doit rester un vide absolu, pas un puits rempli d'eau.
				if (EstDansTrouNoirAbysseMonde(xGlobal, zGlobal))
					continue;
				int yDebut = Mathf.Clamp(sommetSolide[x, z] + 1, 3, yMaxEau);
				for (int y = yDebut; y <= yMaxEau; y++)
				{
					if (!EstVoxelAirSansVerrou(x, y, z)) continue;
					DefinirEauSansVerrou(x, y, z);
					file.Enqueue(new Vector3I(x, y, z));
				}
			}
		}

		while (file.Count > 0)
		{
			Vector3I pos = file.Dequeue();
			foreach (var d in DirPropagationEauInitiale)
			{
				int nx = pos.X + d.X;
				int ny = pos.Y + d.Y;
				int nz = pos.Z + d.Z;
				if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk) continue;
				if (ny <= 2 || ny > yMaxEau) continue;
				if (!EstVoxelAirSansVerrou(nx, ny, nz)) continue;
				DefinirEauSansVerrou(nx, ny, nz);
				file.Enqueue(new Vector3I(nx, ny, nz));
			}
		}
	}

	private int ObtenirNiveauEauActif()
	{
		int niveauMonde = _generationAbysseActive ? AbyssNiveauEau : NiveauEau;
		return niveauMonde - (ChunkOffsetY * HauteurMax);
	}

	private bool EstVoxelAirSansVerrou(int x, int y, int z)
	{
		bool sol = _densities[x, y, z] > Isolevel;
		bool eau = _densitiesEau[x, y, z] > Isolevel;
		return !sol && !eau;
	}

	private void DefinirEauSansVerrou(int x, int y, int z)
	{
		_densities[x, y, z] = -10.0f;
		_materials[x, y, z] = 4;
		_densitiesEau[x, y, z] = 1.0f;
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

			int hauteurSurface = CalculerHauteurTerrain(xGlobal, zGlobal);
			if (hauteurSurface < 0 || hauteurSurface >= HauteurMax - 1) continue;
			if (hauteurSurface <= 2) continue;

			byte matSurface;
			lock (_verrouVoxel)
			{
				matSurface = _materials[x, hauteurSurface, z];
			}

			// Tempéré: herbe (1). Froid/enneigé: neige (5) et glace (9). Aride: terre aride (6).
			bool solTempere = matSurface == 1;
			bool solFroid = matSurface == 5 || matSurface == 9;
			bool solAride = matSurface == 6;
			if (!solTempere && !solFroid && !solAride) continue;

			float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
			float humidite = CalculerHumiditeGlobale(xGlobal, zGlobal);
			float humiditeNorm = (humidite + 1f) * 0.5f;
			bool estJungle = temperature > 0.22f && humiditeNorm > 0.78f;

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
				if (solAride)
				{
					// Zone aride sans herbe: uniquement arbres morts feuillus (chêne/bouleau) très clairsemés.
					if (temperature < 0.12f) continue;
					if (humiditeNorm > 0.48f) continue;
					float tSec = Mathf.Clamp((0.48f - humiditeNorm) / 0.40f, 0f, 1f);
					chanceLocale = Mathf.Lerp(0.006f, 0.022f, tSec);
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
			bool solAride = matSurface == 6;
			if (!solTempere && !solFroid && !solAride) continue;

			float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
			float humiditeNorm = (CalculerHumiditeGlobale(xGlobal, zGlobal) + 1f) * 0.5f;
			bool estJungle = temperature > 0.22f && humiditeNorm > 0.78f;
			if (temperature < -0.15f)
			{
				if (!solFroid || humiditeNorm < 0.08f) continue;
			}
			else
			{
				if (solAride)
				{
					if (temperature < 0.12f || humiditeNorm > 0.48f) continue;
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
			int hauteurSurface = CalculerHauteurTerrain(xGlobal, zGlobal);
			if (hauteurSurface < 0 || hauteurSurface >= HauteurMax - 1) continue;
			if (hauteurSurface <= 2) continue;
			byte matSurface;
			lock (_verrouVoxel)
				matSurface = _materials[x, hauteurSurface, z];
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
			int hauteurSurface = CalculerHauteurTerrain(xGlobal, zGlobal);
			if (hauteurSurface < 3 || hauteurSurface >= HauteurMax - 1) continue;
			byte matSurface;
			lock (_verrouVoxel)
				matSurface = _materials[x, hauteurSurface, z];
			if (matSurface != 1) continue;
			var racine = new Vector3I(xGlobal, hauteurSurface + 1, zGlobal);
			uint seedArbre = (uint)((xGlobal * 73856093) ^ (zGlobal * 19349663));
			InventaireArbres[racine] = new DonneesArbre { Stage = (byte)(seedArbre % 10), Seed = seedArbre };
			return;
		}
	}

	/// <summary>0=sans arbres, 1=bouleau seul, 2=mixte, 3=chêne seul (tempéré uniquement).</summary>
	private int DeterminerBiomeForetTempere(int xGlobal, int zGlobal)
	{
		float n = _noiseBiomeForet?.GetNoise2D(xGlobal, zGlobal) ?? 0f;
		if (n < -0.44f) return 0;
		if (n < -0.08f) return 1;
		if (n < 0.28f) return 2;
		return 3;
	}

	private bool EstZoneJungle(float xGlobal, float zGlobal)
	{
		float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
		float humiditeNorm = (CalculerHumiditeGlobale(xGlobal, zGlobal) + 1f) * 0.5f;
		return temperature > 0.22f && humiditeNorm > 0.72f;
	}

	private bool PeutPlacerBuissonAvecEspacement(Vector3I pos, int rayonCases)
	{
		for (int dx = -rayonCases; dx <= rayonCases; dx++)
		for (int dz = -rayonCases; dz <= rayonCases; dz++)
		{
			if (dx == 0 && dz == 0) continue;
			Vector3I voisin = new Vector3I(pos.X + dx, pos.Y, pos.Z + dz);
			if (InventaireFlore.TryGetValue(voisin, out byte typeVoisin) && EstTypeBuisson(typeVoisin))
				return false;
		}
		return true;
	}

	private byte DeterminerVarianteBuisson(float xGlobal, float zGlobal, bool estJungle)
	{
		if (!estJungle)
		{
			// Tempéré : 8 teintes (0..7) ; la variante 8 est réservée aux buissons APISARA du trou noir.
			float r = DeterministicRand(xGlobal * 0.31f + 5f, zGlobal * 0.47f + 7f);
			return (byte)Mathf.Clamp((int)(r * 8f), 0, 7);
		}
		// Jungle: pool ouvert 0..120 (future-proof pour nouvelles variantes).
		float rJ = DeterministicRand(xGlobal * 0.77f + 11f, zGlobal * 1.13f + 23f);
		int variante = Mathf.Clamp((int)(rJ * 121f), 0, 120);
		// Réserve la variante 8 au trou (baie cyan fluorescente) — évite un buisson jungle « fluo » par hasard.
		if (variante == 8) variante = 9;
		return (byte)variante;
	}

	private void EssayerPromouvoirGazonEnBuisson(Vector3I posGlobale, float xGlobal, float zGlobal)
	{
		if (_generationAbysseActive && !EstPlaineJungleAbysse(xGlobal, zGlobal))
			return;
		float chanceDePousse = CalculerChanceBuisson(xGlobal, zGlobal);
		if (chanceDePousse <= 0f || DeterministicRand(xGlobal, zGlobal) >= chanceDePousse) return;
		bool estJungle = EstZoneJungle(xGlobal, zGlobal)
			|| (_generationAbysseActive && EstPlaineJungleAbysse(xGlobal, zGlobal));
		int rayonEspacement = estJungle ? 2 : 1;
		if (!PeutPlacerBuissonAvecEspacement(posGlobale, rayonEspacement)) return;
		byte variante = DeterminerVarianteBuisson(xGlobal, zGlobal, estJungle);
		// Plus de buissons à baies qu’à vide pour remplir le monde de cueillettes.
		bool plein = DeterministicRand(xGlobal + 17f, zGlobal) < 0.68f;
		InventaireFlore[posGlobale] = ConstruireTypeBuisson(variante, plein);
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
			if (_generationAbysseActive && EstDansTrouNoirAbysseMonde(kv.Key.X, kv.Key.Z)) continue;
			uint h = (uint)(kv.Key.X * 73856093) ^ (uint)(kv.Key.Z * 19349663) ^ (uint)(kv.Key.Y * 83492791);
			if (!trouve || h < hashMin)
			{
				hashMin = h;
				candidat = kv.Key;
				trouve = true;
			}
		}
		if (!trouve) return;
		bool estJungle = EstZoneJungle(candidat.X, candidat.Z)
			|| (_generationAbysseActive && EstPlaineJungleAbysse(candidat.X, candidat.Z));
		byte variante = DeterminerVarianteBuisson(candidat.X, candidat.Z, estJungle);
		InventaireFlore[candidat] = ConstruireTypeBuisson(variante, true);
	}

	private int CalculerHauteurTerrain(int xGlobal, int zGlobal)
	{
		if (_generationAbysseActive)
			return CalculerHauteurTerrainAbysse(xGlobal, zGlobal);

		float bruitBrut = _noiseSurface.GetNoise2D(xGlobal, zGlobal);
		float bruitNormalise = (bruitBrut + 1.0f) / 2.0f;
		float relief = Mathf.Pow(bruitNormalise, 2.3f);  // Relief plus progressif : moins de pics agressifs
		float humiditeNorm = (CalculerHumiditeGlobale(xGlobal, zGlobal) + 1f) * 0.5f;

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
		float hTier2 = tTier2 * tTier2 * 42f;
		float tMontLisse = tMont * tMont * (3f - 2f * tMont);
		float hMontagnes = tMontLisse * tMontLisse * AmplitudeMontagne;

		// Transition progressive base → tier2+montagnes (blend 0.05 → 0.20)
		float poidsBase = 1f - Mathf.Clamp((relief - 0.05f) / 0.15f, 0f, 1f);
		poidsBase = poidsBase * poidsBase * (3f - 2f * poidsBase);
		float hauteurHaut = 118f + hTier2 + hMontagnes;
		int hauteurBase = (int)(rampBase * poidsBase + hauteurHaut * (1f - poidsBase));

		// Macro-océan rare: grandes cuvettes marines avec quelques îles éparses.
		float oceanMacro = _noiseErosion.GetNoise2D(xGlobal * 0.000085f + 12000f, zGlobal * 0.000085f + 12000f);
		if (oceanMacro > 0.62f)
		{
			float tOcean = Mathf.Clamp((oceanMacro - 0.62f) / 0.38f, 0f, 1f);
			float oceanSmooth = tOcean * tOcean * (3f - 2f * tOcean);
			hauteurBase -= (int)(oceanSmooth * 48f);

			// Îles rares dans l'océan (archipels sporadiques).
			float ileMacro = _noiseSurface.GetNoise2D(xGlobal * 0.00042f + 21000f, zGlobal * 0.00042f + 21000f);
			float ileDetail = _noiseErosion.GetNoise2D(xGlobal * 0.0018f + 26000f, zGlobal * 0.0018f + 26000f);
			if (ileMacro > 0.70f && ileDetail > 0.78f)
			{
				float tIle = Mathf.Clamp((Mathf.Min(ileMacro, ileDetail) - 0.70f) / 0.30f, 0f, 1f);
				float boostIle = 8f + (tIle * tIle * 30f);
				hauteurBase += (int)boostIle;
			}
		}

		float crevasseBrute = _noiseRivieres.GetNoise2D(xGlobal, zGlobal);
		int profondeurEau = 0;
		// Zones boueuses/humides: plus de rivières (seuil abaissé + creusement un peu plus fort).
		float tHumide = Mathf.Clamp((humiditeNorm - 0.56f) / 0.44f, 0f, 1f);
		float seuilRiviere = Mathf.Lerp(0.12f, 0.045f, tHumide);
		if (crevasseBrute > seuilRiviere)
		{
			float intensiteRiviera = (crevasseBrute - seuilRiviere) / Mathf.Max(0.05f, 1f - seuilRiviere);
			float tSmooth = intensiteRiviera * intensiteRiviera * (3f - 2f * intensiteRiviera);  // Descente très douce vers l'eau
			float profondeurMax = Mathf.Lerp(22f, 30f, tHumide);
			profondeurEau = (int)(tSmooth * profondeurMax);
		}
		return hauteurBase - profondeurEau;
	}

	/// <summary>Distance radiale warping APISARA (plaines / muraille) — partagée hauteur + masques biome.</summary>
	private float CalculerDistanceProfilAbysse(float xGlobal, float zGlobal)
	{
		float distance = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);
		float angle = Mathf.Atan2(zGlobal, xGlobal);

		// Décale radialement les frontières de zones pour casser l'anneau parfait
		// et obtenir une muraille plus organique (bosses, creux, pointes irrégulières).
		float modulationAngulaire =
			Mathf.Sin(angle * 3.1f + 0.8f) * 38f +
			Mathf.Sin(angle * 6.7f - 1.4f) * 22f;
		float bruitMacroAnneau = _noiseSurface.GetNoise2D(xGlobal * 0.0018f + 2600f, zGlobal * 0.0018f + 2600f) * 54f;
		float bruitWarpAnneau = _noiseErosion.GetNoise2D(xGlobal * 0.0034f - 3700f, zGlobal * 0.0034f - 3700f) * 28f;
		return distance + modulationAngulaire + bruitMacroAnneau + bruitWarpAnneau;
	}

	/// <summary>Les deux plaines herbe (sanctuaire intérieur + plaine extérieure), hors trou et hors muraille.</summary>
	private bool EstPlaineJungleAbysse(float xGlobal, float zGlobal)
	{
		if (!_generationAbysseActive) return false;
		float dp = CalculerDistanceProfilAbysse(xGlobal, zGlobal);
		if (dp <= AbyssRayonTrouNoir) return false;
		if (dp > AbyssRayonTrouNoir && dp <= AbyssRayonX) return true;
		if (dp >= AbyssRayonY && dp <= AbyssRayonZ) return true;
		return false;
	}

	private int CalculerHauteurTerrainAbysse(int xGlobal, int zGlobal) =>
		ApisaraHauteurTerrain.CalculerSurfaceMonde(
			xGlobal, zGlobal,
			_noiseSurface, _noiseErosion, _noiseCavernes, _noiseRivieres,
			_noiseAbysseChaos3D, _noiseAbysseChaosDetail3D);

	/// <summary>Même masque que la rampe spirale ciselée dans le trou ; 0 = hors bande. Sert aussi à ne pas poser de gazon sur la descente.</summary>
	private float CalculerIntensiteSpiraleDescenteAbysse(float xGlobal, float yGlobal, float zGlobal)
	{
		if (!_generationAbysseActive) return 0f;
		if (yGlobal > AbyssYSpiraleTop || yGlobal < AbyssYSpiraleBottom) return 0f;
		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance < AbyssRayonSpiraleMin || distance > AbyssRayonSpiraleMax) return 0f;

		float angle = Mathf.Atan2(zGlobal, xGlobal);
		float warpHelicoidal = _noiseErosion.GetNoise3D(xGlobal * 0.0062f + 4100f, yGlobal * 0.0093f - 1700f, zGlobal * 0.0062f - 2600f) * 0.85f;
		float phase = angle * 6.2f + yGlobal * 0.018f + warpHelicoidal;
		float signalSpirale = 0.5f + (0.5f * Mathf.Cos(phase));
		float masqueSpirale = Mathf.SmoothStep(0.58f, 0.92f, signalSpirale);

		float n3dBrut = _noiseAbysseSpirale3D.GetNoise3D(xGlobal, yGlobal, zGlobal);
		float n3d = Mathf.Pow(Mathf.Clamp((n3dBrut + 1f) * 0.5f, 0f, 1f), 1.25f);

		float attenuationRayon = Mathf.Clamp((distance - AbyssRayonSpiraleMin) / Mathf.Max(1f, AbyssRayonTrouNoir - AbyssRayonSpiraleMin), 0f, 1f);
		attenuationRayon *= attenuationRayon;

		float tDescente = Mathf.Clamp((AbyssYSpiraleTop - yGlobal) / Mathf.Max(1f, AbyssYSpiraleTop - AbyssYSpiraleBottom), 0f, 1f);
		float gainDescente = Mathf.Lerp(0.85f, 1.22f, tDescente);

		float varianteOrg = Mathf.Lerp(0.72f, 1.28f, Mathf.Clamp((_noiseAbysseChaosDetail3D.GetNoise3D(xGlobal * 0.31f, yGlobal * 0.41f, zGlobal * 0.31f) + 1f) * 0.5f, 0f, 1f));
		return masqueSpirale * n3d * attenuationRayon * gainDescente * varianteOrg;
	}

	private bool EvaluerExtrusionParoiAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		float intensite = CalculerIntensiteSpiraleDescenteAbysse(xGlobal, yGlobal, zGlobal);
		if (intensite <= 0.02f) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		profondeurInward = Mathf.Lerp(AbyssExtrusionMin, AbyssExtrusionMax, Mathf.Clamp(intensite, 0f, 1f));
		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	/// <summary>
	/// Anneau [-510,-450[ : pics pierre triangulaires (vue de côté) ancrés au mur, herbe en passe séparée.
	/// Profondeur maximale vers Y=-450 (base du pic côté mur), pointe vers Y=-510.
	/// </summary>
	private bool EvaluerExtrusionAnneauAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		if (!_generationAbysseActive) return false;
		if (yGlobal >= AbyssYSpiraleBottom || yGlobal < AbyssYAnneauTransitionBottom) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance > AbyssRayonTrouNoir) return false;

		float angle = Mathf.Atan2(zGlobal, xGlobal);
		float angleNorm = Mathf.PosMod(angle + Mathf.Pi, Mathf.Tau) / Mathf.Tau;
		float xMur = Mathf.Cos(angle) * AbyssRayonTrouNoir;
		float zMur = Mathf.Sin(angle) * AbyssRayonTrouNoir;
		float yAniso = yGlobal * AbyssChaosYScale;

		float warpAng = _noiseAbysseChaosDetail3D.GetNoise3D(xMur * 0.019f + 120f, yAniso, zMur * 0.019f - 120f) * 0.075f;
		float densitePic = AbyssPicAnneauNombre * Mathf.Lerp(0.78f, 1.22f, Mathf.Clamp((_noiseAbysseChaos3D.GetNoise2D(xMur * 0.0022f + 900f, zMur * 0.0022f - 900f) + 1f) * 0.5f, 0f, 1f));
		float scaled = (angleNorm + warpAng) * densitePic;
		float frac = scaled - Mathf.Floor(scaled);
		float distCreneau = Mathf.Min(frac, 1f - frac);
		float porteAngulaire = 1f - Mathf.SmoothStep(0.035f, 0.13f, distCreneau);

		float tVert = (yGlobal - AbyssYAnneauTransitionBottom) / Mathf.Max(1e-3f, AbyssYSpiraleBottom - AbyssYAnneauTransitionBottom);
		tVert = Mathf.Clamp(tVert, 0f, 1f);
		tVert = tVert * tVert * (3f - 2f * tVert);

		float bruitMacro = Mathf.Clamp((_noiseAbysseChaos3D.GetNoise3D(xMur, yAniso, zMur) + 1f) * 0.5f, 0f, 1f);
		float bruitDetail = Mathf.Clamp((_noiseAbysseChaosDetail3D.GetNoise3D(xMur * 1.75f + 700f, yAniso * 0.8f - 220f, zMur * 1.75f - 700f) + 1f) * 0.5f, 0f, 1f);
		float amplitude = Mathf.Lerp(0.42f, 1f, bruitMacro) * (0.72f + 0.28f * bruitDetail);

		float profondeurBase = Mathf.Lerp(AbyssChaosExtrusionMin + 24f, AbyssChaosExtrusionMax, amplitude);
		profondeurBase *= tVert * porteAngulaire;

		float angleSortieSpirale = ObtenirAngleSortieSpirale();
		float ecartAngle = ObtenirEcartAngulaireAbsolu(angle, angleSortieSpirale);
		float lipAngle = 1f - Mathf.SmoothStep(0.07f, 0.24f, ecartAngle);
		float lipAlt = 1f - Mathf.SmoothStep(10f, 26f, Mathf.Abs(yGlobal - AbyssYSpiraleBottom));
		float lip = lipAngle * lipAngle * (3f - 2f * lipAngle) * lipAlt * lipAlt * (3f - 2f * lipAlt);
		profondeurBase = Mathf.Max(profondeurBase, lip * AbyssPicLipSortieProfondeurMax);

		profondeurInward = Mathf.Clamp(profondeurBase, 0f, AbyssChaosExtrusionMax);
		if (profondeurInward < 1.5f) return false;

		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	/// <summary>
	/// Hors rampe spirale : pics supplémentaires plus rares entre Y=-450 et le haut de la spirale (Y=20).
	/// </summary>
	private bool EvaluerPicSupplementSpiraleAbysse(float xGlobal, float yGlobal, float zGlobal, out float profondeurInward)
	{
		profondeurInward = 0f;
		if (!_generationAbysseActive) return false;
		if (yGlobal <= AbyssYSpiraleBottom || yGlobal > AbyssYSpiraleTop) return false;

		float distance = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
		if (distance > AbyssRayonTrouNoir) return false;

		float angle = Mathf.Atan2(zGlobal, xGlobal);
		float angleNorm = Mathf.PosMod(angle + Mathf.Pi, Mathf.Tau) / Mathf.Tau;
		float xMur = Mathf.Cos(angle) * AbyssRayonTrouNoir;
		float zMur = Mathf.Sin(angle) * AbyssRayonTrouNoir;
		float yAniso = yGlobal * AbyssChaosYScale;

		float tProximiteFinSpirale = 1f - (yGlobal - AbyssYSpiraleBottom) / Mathf.Max(1e-3f, AbyssYSpiraleTop - AbyssYSpiraleBottom);
		tProximiteFinSpirale = Mathf.Clamp(tProximiteFinSpirale, 0f, 1f);
		tProximiteFinSpirale = tProximiteFinSpirale * tProximiteFinSpirale * (3f - 2f * tProximiteFinSpirale);
		if (tProximiteFinSpirale < 0.06f) return false;

		float warpAng = _noiseAbysseChaosDetail3D.GetNoise3D(xMur * 0.017f - 900f, yAniso * 1.1f, zMur * 0.017f + 900f) * 0.09f;
		float scaled = (angleNorm + warpAng) * AbyssPicSpiraleNombre;
		float frac = scaled - Mathf.Floor(scaled);
		float distCreneau = Mathf.Min(frac, 1f - frac);
		float porteAngulaire = 1f - Mathf.SmoothStep(0.05f, 0.19f, distCreneau);

		float bruit = Mathf.Clamp((_noiseAbysseChaos3D.GetNoise3D(xMur * 0.35f, yAniso + 80f, zMur * 0.35f) + 1f) * 0.5f, 0f, 1f);
		float profondeurMax = Mathf.Lerp(32f, 138f, bruit) * tProximiteFinSpirale * porteAngulaire;

		profondeurInward = Mathf.Clamp(profondeurMax, 0f, 165f);
		if (profondeurInward < 2f) return false;

		float seuilInterieur = AbyssRayonTrouNoir - profondeurInward;
		return distance >= seuilInterieur && distance <= AbyssRayonTrouNoir;
	}

	private static float ObtenirAngleSortieSpirale()
	{
		float angle = (-AbyssYSpiraleBottom * 0.018f) / 6.2f;
		return Mathf.PosMod(angle, Mathf.Tau);
	}

	private static float ObtenirEcartAngulaireAbsolu(float a, float b)
	{
		float d = Mathf.PosMod(a - b + Mathf.Pi, Mathf.Tau) - Mathf.Pi;
		return Mathf.Abs(d);
	}

	private void AppliquerBiomeParasiteCornichesAbysse()
	{
		if (!_generationAbysseActive || _densities == null || _materials == null) return;
		if (!EstChunkAvecGeometrieExtrusionTrou()) return;
		ObtenirPlageIndiceYMonde(AbyssYAnneauTransitionBottom - 8f, AbyssYSpiraleTop + 32f, out int yDebut, out int yFin);
		if (yDebut > yFin) return;
		for (int x = 0; x <= TailleChunk; x++)
		{
			for (int y = yDebut; y <= yFin; y++)
			{
				float globalY = ChunkOffsetY * HauteurMax + y;

				for (int z = 0; z <= TailleChunk; z++)
				{
					if (_densities[x, y, z] <= Isolevel) continue;

					float xGlobal = ChunkOffsetX * TailleChunk + x;
					float zGlobal = ChunkOffsetZ * TailleChunk + z;
					if (!ColonneDansBandeExtrusionTrou(xGlobal, zGlobal)) continue;
					bool appartientSpirale = EvaluerExtrusionParoiAbysse(xGlobal, globalY, zGlobal, out _);
					bool appartientAnneau = !appartientSpirale && EvaluerExtrusionAnneauAbysse(xGlobal, globalY, zGlobal, out _);
					bool appartientPicSupplement = !appartientSpirale && !appartientAnneau && EvaluerPicSupplementSpiraleAbysse(xGlobal, globalY, zGlobal, out _);
					if (!appartientSpirale && !appartientAnneau && !appartientPicSupplement) continue;

					bool airAuDessus = y >= HauteurMax
						|| (_densities[x, y + 1, z] <= Isolevel && (_densitiesEau == null || _densitiesEau[x, y + 1, z] <= Isolevel));
					if (!airAuDessus)
					{
						_materials[x, y, z] = 2;
						continue;
					}

					bool zonePic = appartientAnneau || appartientPicSupplement;
					bool praticable = EstSurfacePraticableBiomeParasite(x, y, z)
						|| (zonePic && EstSurfacePraticablePicAbysseRelaxee(x, y, z));
					_materials[x, y, z] = praticable ? (byte)1 : (byte)2;
				}
			}
		}
	}

	/// <summary>APISARA : buissons à baies cyan fluorescentes (variante 8) uniquement dans le trou, Y monde [-500, 0], sur gazon existant avec espacement.</summary>
	private void EnsemencerBuissonsFluoTrouAbysse()
	{
		if (!_generationAbysseActive) return;
		float yMin = ConstantesDimensionAbysse.LimiteInferieureHerbeTrouMonde;
		var gazonATraiter = new List<Vector3I>(64);
		foreach (var kv in InventaireFlore)
		{
			if (kv.Value != FloreTypeGazon) continue;
			float yM = kv.Key.Y;
			if (yM < yMin || yM > 0f) continue;
			if (!EstDansTrouNoirAbysseMonde(kv.Key.X, kv.Key.Z)) continue;
			if (!PositionFloreDansChunk(kv.Key)) continue;
			gazonATraiter.Add(kv.Key);
		}
		for (int i = 0; i < gazonATraiter.Count; i++)
		{
			Vector3I pos = gazonATraiter[i];
			float xG = pos.X;
			float zG = pos.Z;
			float distCentre = Mathf.Sqrt((xG * xG) + (zG * zG));
			float chanceBuissonFluo = distCentre <= AbyssRayonPlateauCentralFlore
				? AbyssChanceBuissonFluoCentre
				: AbyssChanceBuissonFluo;
			if (DeterministicRand(xG * 1.03f + pos.Y * 0.019f, zG * 0.97f) > chanceBuissonFluo) continue;
			if (!PeutPlacerBuissonAvecEspacement(pos, 3)) continue;
			InventaireFlore[pos] = ConstruireTypeBuisson(8, true);
		}
	}

	private bool PositionFloreDansChunk(Vector3I posGlobale)
	{
		int lx = posGlobale.X - ChunkOffsetX * TailleChunk;
		int lz = posGlobale.Z - ChunkOffsetZ * TailleChunk;
		return lx >= 0 && lx <= TailleChunk && lz >= 0 && lz <= TailleChunk;
	}

	/// <summary>APISARA : gazon seul sur replats du trou (spirale/pics), sans buissons ni arbres.</summary>
	private void EnsemencerFloreTrouAbysseReplats()
	{
		if (!_generationAbysseActive || _densities == null || _materials == null) return;
		float yMin = ConstantesDimensionAbysse.LimiteInferieureHerbeTrouMonde;
		float yMax = AbyssYSpiraleTop + 28f;
		ObtenirPlageIndiceYMonde(yMin, yMax, out int yIndiceMin, out int yIndiceMax);
		if (yIndiceMin > yIndiceMax) return;
		for (int x = 0; x <= TailleChunk; x++)
		for (int z = 0; z <= TailleChunk; z++)
		{
			float xG = ChunkOffsetX * TailleChunk + x;
			float zG = ChunkOffsetZ * TailleChunk + z;
			if (!EstDansTrouNoirAbysseMonde(xG, zG)) continue;
			float distCentre = Mathf.Sqrt((xG * xG) + (zG * zG));
			bool plateauCentral = distCentre <= AbyssRayonPlateauCentralFlore;
			int ySurface = -1;
			for (int y = yIndiceMax; y >= yIndiceMin; y--)
			{
				if (_densities[x, y, z] <= Isolevel) continue;
				bool airDessus = y >= HauteurMax
					|| (_densities[x, y + 1, z] <= Isolevel && (_densitiesEau == null || _densitiesEau[x, y + 1, z] <= Isolevel));
				if (!airDessus) continue;
				ySurface = y;
				break;
			}
			if (ySurface < 0) continue;
			float globalY = ChunkOffsetY * HauteurMax + ySurface;
			bool surfaceHerbe = _materials[x, ySurface, z] == 1;
			bool surfacePlateauCentral = plateauCentral
				&& EstSurfacePraticablePicAbysseRelaxee(x, ySurface, z);
			if (!surfaceHerbe && !surfacePlateauCentral) continue;
			if (!AbysseReplatreHerbeTrouPlat(x, ySurface, z, xG, zG)) continue;
			if (CalculerIntensiteSpiraleDescenteAbysse(xG, globalY, zG) > 0.42f && !surfacePlateauCentral) continue;
			var pos = new Vector3I((int)xG, (int)globalY, (int)zG);
			InventaireFlore[pos] = FloreTypeGazon;
		}
	}

	/// <summary>Replats horizontaux dans le puits : voisins en 8 dans le chunk ; hors grille ignorés. Surface strictement plate (même Y que le centre) pour éviter l’herbe sur les marches.</summary>
	private bool AbysseReplatreHerbeTrouPlat(int lx, int ly, int lz, float xGlobal, float zGlobal)
	{
		if (_densities == null) return false;
		int h0 = ly;
		bool plateauCentral = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal)) <= AbyssRayonPlateauCentralFlore;
		int voisinsExaminés = 0;
		int ecartMax = 0;
		for (int dz = -1; dz <= 1; dz++)
		for (int dx = -1; dx <= 1; dx++)
		{
			if (dx == 0 && dz == 0) continue;
			int nx = lx + dx;
			int nz = lz + dz;
			if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk) continue;
			voisinsExaminés++;
			int hy = ObtenirHauteurSurfaceLocale(nx, nz);
			if (hy < 0) return false;
			ecartMax = Mathf.Max(ecartMax, Mathf.Abs(hy - h0));
			if (!plateauCentral && hy != h0) return false;
		}
		if (plateauCentral)
			return ecartMax <= 1;
		return voisinsExaminés > 0;
	}

	private bool EstSurfacePraticableBiomeParasite(int x, int y, int z)
	{
		if (_densities == null) return false;
		if (y < 0 || y > HauteurMax) return false;

		int voisinsTestes = 0;
		int voisinsStables = 0;
		for (int i = 0; i < DirCardinales2D.Length; i++)
		{
			int nx = x + DirCardinales2D[i].X;
			int nz = z + DirCardinales2D[i].Y;
			if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk)
				continue;

			voisinsTestes++;
			bool voisinSolide = _densities[nx, y, nz] > Isolevel;
			bool voisinAirAuDessus = y >= HauteurMax
				|| (_densities[nx, y + 1, nz] <= Isolevel && (_densitiesEau == null || _densitiesEau[nx, y + 1, nz] <= Isolevel));
			if (voisinSolide && voisinAirAuDessus)
				voisinsStables++;
		}

		return voisinsTestes >= 3 && voisinsStables >= 2;
	}

	/// <summary>Sommets de pics APISARA souvent trop étroits pour 2/3 voisins stables — critère assoupli sans toucher au reste.</summary>
	private bool EstSurfacePraticablePicAbysseRelaxee(int x, int y, int z)
	{
		if (_densities == null) return false;
		if (y < 0 || y > HauteurMax) return false;

		int voisinsTestes = 0;
		int voisinsStables = 0;
		for (int i = 0; i < DirCardinales2D.Length; i++)
		{
			int nx = x + DirCardinales2D[i].X;
			int nz = z + DirCardinales2D[i].Y;
			if (nx < 0 || nx > TailleChunk || nz < 0 || nz > TailleChunk)
				continue;

			voisinsTestes++;
			bool voisinSolide = _densities[nx, y, nz] > Isolevel;
			bool voisinAirAuDessus = y >= HauteurMax
				|| (_densities[nx, y + 1, nz] <= Isolevel && (_densitiesEau == null || _densitiesEau[nx, y + 1, nz] <= Isolevel));
			if (voisinSolide && voisinAirAuDessus)
				voisinsStables++;
		}

		return voisinsTestes >= 2 && voisinsStables >= 1;
	}

	private bool EstDansTrouNoirAbysseMonde(float xGlobal, float zGlobal)
	{
		if (!_generationAbysseActive) return false;
		float distance = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);
		return distance <= AbyssRayonTrouNoir;
	}

	private static float DeterministicRand(float x, float z)
	{
		// Hash cross-platform: évite les conversions float->uint hors plage (comportement non défini).
		uint hx = unchecked((uint)BitConverter.SingleToInt32Bits(x));
		uint hz = unchecked((uint)BitConverter.SingleToInt32Bits(z));
		uint h = hx * 73856093u ^ hz * 19349663u ^ 0x9E3779B9u;
		h ^= h >> 16;
		h *= 0x7FEB352Du;
		h ^= h >> 15;
		h *= 0x846CA68Bu;
		h ^= h >> 16;
		// 24 bits utiles -> [0,1), distribution plus lisse que mod 10000.
		return (h & 0x00FFFFFFu) / 16777216f;
	}

	private float CalculerHumiditeGlobale(float xGlobal, float zGlobal)
	{
		if (_generationAbysseActive)
			return 0f;
		float macro = _noiseHumidite.GetNoise2D(xGlobal, zGlobal);
		float micro = _noiseHumiditeDetail != null ? _noiseHumiditeDetail.GetNoise2D(xGlobal, zGlobal) : 0f;
		return Mathf.Clamp(macro * 0.85f + micro * 0.15f, -1f, 1f);
	}

	/// <summary>Probabilité de transformer un gazon en buisson selon humidité + biome tempéré/jungle.</summary>
	private float CalculerChanceBuisson(float xGlobal, float zGlobal)
	{
		if (_generationAbysseActive && EstPlaineJungleAbysse(xGlobal, zGlobal))
			return Mathf.Clamp(0.048f + DeterministicRand(xGlobal * 0.21f + 3f, zGlobal * 0.19f + 5f) * 0.052f, 0.042f, 0.10f);
		float humiditeBrute = CalculerHumiditeGlobale(xGlobal, zGlobal);
		float humiditeNorm = (humiditeBrute + 1f) * 0.5f;
		// Seuil bas abaissé : des buissons même en zones plus sèches (baies visibles « partout »).
		if (humiditeNorm <= 0.14f) return 0f;
		float t = (humiditeNorm - 0.14f) / 0.86f;
		float chance = 0.010f + t * 0.085f;
		// Jungle: plus de buissons (espacement appliqué séparément).
		if (EstZoneJungle(xGlobal, zGlobal))
			chance *= 1.65f;
		// Tempéré uniquement: distribution des baies par biome forêt.
		float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
		if (temperature >= -0.15f)
		{
			int biome = DeterminerBiomeForetTempere((int)xGlobal, (int)zGlobal);
			if (biome == 2) chance *= 1.35f;      // Mixte (bouleau + chêne): plus forte densité de baies.
			else if (biome == 0) chance *= 0.82f; // Plaine / peu d’arbres : reste couvrant.
		}
		return Mathf.Clamp(chance, 0f, 0.11f);
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
		if (_generationAbysseActive)
		{
			float distance = Mathf.Sqrt(xGlobal * xGlobal + zGlobal * zGlobal);
			if (distance <= AbyssRayonTrouNoir) return 2;      // Néant / roche sombre
			if (distance <= AbyssRayonX) return 1;             // Plaine sanctuaire
			if (distance <= AbyssRayonY) return 2;             // Muraille montagne
			if (distance <= AbyssRayonZ) return 1;             // Plaine exterieure
			if (distance <= AbyssRayonW) return 3;             // Frontiere sable
			return 4;                                           // Ocean
		}

		float bruitNeige = _noiseNeige.GetNoise2D(xGlobal, zGlobal);
		float bruitRoche = _noiseNeige.GetNoise2D(xGlobal + 500f, zGlobal);
		float bruitDesert = _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.9f + 17000f, zGlobal * 1.9f + 17000f);
		// Poches d'argile : uniquement en climat jungle, surtout en bord d'eau, très rare au fond.
		float bruitArgileRive = _noiseHumiditeDetail.GetNoise2D(xGlobal * 2.15f + 3100f, zGlobal * 2.15f - 2700f);
		float bruitArgileFond = _noiseHumiditeDetail.GetNoise2D(xGlobal * 3.6f - 9300f, zGlobal * 3.6f + 4800f);
		int seuilNeigeLocal = SeuilNeigeBase + (int)(bruitNeige * 5f);   // 245-255
		int seuilRocheLocal = SeuilMontagneRoche + (int)(bruitRoche * 8f); // 200-215
		if (globalY >= seuilNeigeLocal) return 5;  // NEIGE
		if (globalY >= seuilRocheLocal) return 2;   // Roche nue
		bool climatJungleArgile = temperature > 0.22f && humidite > 0.34f;
		bool bordEau = hauteurSurface >= NiveauEau - 1 && hauteurSurface <= NiveauEau + 2;
		bool fondEau = hauteurSurface <= NiveauEau - 1;
		if (climatJungleArgile && bordEau && bruitArgileRive > 0.83f) return 8;
		if (climatJungleArgile && fondEau && bruitArgileFond > 0.965f) return 8;
		if (globalY <= NiveauPlage) return (humidite > 0.2f) ? (byte)7 : (byte)3;  // Plage : seuil doux
		// Sable UNIQUEMENT quand très sec ET très chaud (temp + humidité liés logiquement)
		if (temperature > 0.5f && humidite < -0.5f) return 3;  // Désert : sable
		bool desertSableFort = temperature > 0.36f && humidite < -0.28f && bruitDesert > -0.05f;
		bool desertSableModere = temperature > 0.26f && humidite < -0.22f && bruitDesert > 0.26f;
		if (desertSableFort || desertSableModere) return 3;
		// Plusieurs stades temp/hum avec seuils progressifs (transitions lentes)
		if (temperature > 0.4f)  // Très chaud
		{
			// Jungle: chaud + très humide => herbe dominante (boue conservée par taches).
			if (humidite > 0.62f)
				return _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.7f + 400f, zGlobal * 1.7f + 400f) > 0.42f ? (byte)7 : (byte)1;
			if (humidite > 0.4f)
				return _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.9f + 760f, zGlobal * 1.9f + 760f) > 0.64f ? (byte)7 : (byte)1;
			if (humidite < -0.18f && bruitDesert > -0.18f) return 3; // Désert chaud: sable dominant.
			if (humidite > 0.1f) return 1;   // Chaud humide : herbe tropicale (ID 1)
			return 1;   // Sec mais pas assez pour sable → herbe jaunâtre (shader)
		}
		if (temperature > 0.15f)  // Chaud
		{
			if (humidite > 0.60f)
				return _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.6f + 900f, zGlobal * 1.6f + 900f) > 0.46f ? (byte)7 : (byte)1;
			if (humidite > 0.35f)
				return _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.45f + 1280f, zGlobal * 1.45f + 1280f) > 0.70f ? (byte)7 : (byte)1;
			if (humidite < -0.30f && bruitDesert > 0.08f) return 3; // Désert tempéré chaud (sable en nappes).
			if (humidite > 0.0f) return 1;
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
		Span<byte> destination = bytes.AsSpan();
		lock (_verrouVoxel)
		{
			int idx = 0;
			for (int x = 0; x < tx; x++)
				for (int y = 0; y < ty; y++)
					for (int z = 0; z < tz; z++)
					{
						BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(idx, 4), _densities[x, y, z]); idx += 4;
						bytes[idx++] = _materials[x, y, z];
						BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(idx, 4), _densitiesEau[x, y, z]); idx += 4;
					}
		}
		return bytes;
	}

	/// <summary>Sauvegarde binaire sur disque.</summary>
	public void SauvegarderChunkSurDisque()
	{
		string dossierSave = string.IsNullOrWhiteSpace(_dossierChunksSauvegarde)
			? ProjectSettings.GlobalizePath($"user://saves/{GameState.Instance?.NomMondeActuel ?? "MonMonde"}/chunks/")
			: ProjectSettings.GlobalizePath(_dossierChunksSauvegarde.EndsWith("/") ? _dossierChunksSauvegarde : _dossierChunksSauvegarde + "/");
		Directory.CreateDirectory(dossierSave);
		string cheminFichier = Path.Combine(dossierSave, $"chunk_{ChunkOffsetX}_{ChunkOffsetY}_{ChunkOffsetZ}.bin");
		byte[] donnees = ObtenirTableauBytes();
		using (var writer = new BinaryWriter(File.Open(cheminFichier, FileMode.Create)))
		{
			writer.Write((byte)1);
			writer.Write(donnees.Length);
			writer.Write(donnees);
		}
		if (OS.IsDebugBuild())
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
		AppliquerEnsemencementFloreTrouAbysse(notifierClient: false);
	}

	/// <summary>Répare les parois / rebords du goufre sur chunks disque générés avant l'optimisation Y (extrusion absente).</summary>
	public void ReparerGeometrieExtrusionAbysseSiChargee()
	{
		if (!_generationAbysseActive || !_chargeDepuisDisque || _densities == null || _materials == null)
			return;
		if (!EstChunkAvecGeometrieExtrusionTrou())
			return;

		const float yMinBande = AbyssYAnneauTransitionBottom - 1f;
		const float yMaxBande = AbyssYSpiraleTop + 1f;
		bool modifie = false;
		lock (_verrouVoxel)
		{
			int taille = TailleChunk + 1;
			ObtenirPlageIndiceYMonde(yMinBande, yMaxBande, out int yDebut, out int yFin);
			if (yDebut > yFin) return;

			for (int x = 0; x < taille; x++)
			for (int z = 0; z < taille; z++)
			{
				float xGlobal = ChunkOffsetX * TailleChunk + x;
				float zGlobal = ChunkOffsetZ * TailleChunk + z;
				float distXZ = Mathf.Sqrt((xGlobal * xGlobal) + (zGlobal * zGlobal));
				bool dansTrou = distXZ <= AbyssRayonTrouNoir;
				if (!dansTrou && !ColonneDansBandeExtrusionTrou(xGlobal, zGlobal))
					continue;

				for (int y = yDebut; y <= yFin; y++)
				{
					float globalY = ChunkOffsetY * HauteurMax + y;
					if (globalY < yMinBande || globalY > yMaxBande) continue;

					bool extrusionParoi = EvaluerExtrusionParoiAbysse(xGlobal, globalY, zGlobal, out _);
					bool extrusionAnneau = !extrusionParoi && EvaluerExtrusionAnneauAbysse(xGlobal, globalY, zGlobal, out _);
					bool extrusionPic = !extrusionParoi && !extrusionAnneau && EvaluerPicSupplementSpiraleAbysse(xGlobal, globalY, zGlobal, out _);
					if (!extrusionParoi && !extrusionAnneau && !extrusionPic) continue;

					if (_densities[x, y, z] > Isolevel && _materials[x, y, z] > 0)
						continue;

					_densities[x, y, z] = 10.0f;
					_materials[x, y, z] = 2;
					if (_densitiesEau != null)
						_densitiesEau[x, y, z] = -1.0f;
					modifie = true;
				}
			}

			if (modifie)
			{
				AppliquerBiomeParasiteCornichesAbysse();
				_estModifie = true;
			}
		}

		if (modifie)
			AppliquerEnsemencementFloreTrouAbysse(notifierClient: false);
	}

	/// <summary>Herbe sur replats + baies cyan (variante 8) dans le goufre APISARA. Idempotent ; génération procédurale uniquement (pas à chaque chargement disque).</summary>
	public void AppliquerEnsemencementFloreTrouAbysse(bool notifierClient = true)
	{
		if (!_generationAbysseActive || _densities == null || !ChunkColonneIntersecteTrouNoirAbysse()) return;
		int avant = InventaireFlore.Count;
		EnsemencerFloreTrouAbysseReplats();
		EnsemencerBuissonsFluoTrouAbysse();
		if (notifierClient && InventaireFlore.Count != avant)
			_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));
	}

	/// <summary>Le chunk (XZ) touche le disque du goufre (rayon 500 m) — évite un scan voxel complet hors zone.</summary>
	private bool ChunkColonneIntersecteTrouNoirAbysse()
	{
		if (!_generationAbysseActive) return false;
		float minX = ChunkOffsetX * TailleChunk;
		float maxX = minX + TailleChunk;
		float minZ = ChunkOffsetZ * TailleChunk;
		float maxZ = minZ + TailleChunk;
		float closestX = Mathf.Clamp(0f, minX, maxX);
		float closestZ = Mathf.Clamp(0f, minZ, maxZ);
		float dist2 = (closestX * closestX) + (closestZ * closestZ);
		return dist2 <= AbyssRayonTrouNoir * AbyssRayonTrouNoir;
	}

	private bool ColonneDansBandeExtrusionTrou(float xGlobal, float zGlobal) =>
		CalculerDistanceProfilAbysse(xGlobal, zGlobal) <= AbyssRayonProfilExtrusionMax;

	private bool EstChunkAvecGeometrieExtrusionTrou()
	{
		if (!_generationAbysseActive) return false;
		float minX = ChunkOffsetX * TailleChunk;
		float maxX = minX + TailleChunk;
		float minZ = ChunkOffsetZ * TailleChunk;
		float maxZ = minZ + TailleChunk;
		return ColonneDansBandeExtrusionTrou(minX, minZ)
			|| ColonneDansBandeExtrusionTrou(maxX, minZ)
			|| ColonneDansBandeExtrusionTrou(minX, maxZ)
			|| ColonneDansBandeExtrusionTrou(maxX, maxZ);
	}

	private void ObtenirPlageIndiceYMonde(float yMondeMin, float yMondeMax, out int yIndiceMin, out int yIndiceMax)
	{
		float baseYMonde = ChunkOffsetY * HauteurMax;
		yIndiceMin = Mathf.Clamp(Mathf.CeilToInt(yMondeMin - baseYMonde), 0, HauteurMax);
		yIndiceMax = Mathf.Clamp(Mathf.FloorToInt(yMondeMax - baseYMonde), 0, HauteurMax);
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
			EssayerPromouvoirGazonEnBuisson(pos, xGlobal, zGlobal);
		}
		AssurerBuissonMinimalDansChunk();
	}

	/// <summary>Scanne la surface chargée et remplit InventaireFlore (chunks du disque). Gazon partout sur ID 1 ; Abysse : plaines jungle uniquement, clés en Y monde.</summary>
	private void GenererInventaireFloreDepuisSurface()
	{
		for (int x = 0; x < TailleChunk; x++)
			for (int z = 0; z < TailleChunk; z++)
			{
				float xGlobal = ChunkOffsetX * TailleChunk + x;
				float zGlobal = ChunkOffsetZ * TailleChunk + z;
				if (_generationAbysseActive && !EstPlaineJungleAbysse(xGlobal, zGlobal))
					continue;
				int ySurface = -1;
				for (int y = HauteurMax - 1; y >= 2; y--)
					if (_densities[x, y, z] > Isolevel && (y + 1 >= HauteurMax + 1 || _densities[x, y + 1, z] <= Isolevel))
					{ ySurface = y; break; }
				if (ySurface < 0) continue;
				byte mat = _materials[x, ySurface, z];
				if (!EstMateriauSupportGazon(mat)) continue;
				if (!TerrainAssezPlatDepuisDonnees(x, z)) continue;
				if (!TerrainAvecMargeBordDepuisDonnees(x, z)) continue;
				int yMonde = ChunkOffsetY * HauteurMax + ySurface;
				if (yMonde <= NIVEAU_MIN_FLORE || yMonde >= NIVEAU_MAX_FLORE) continue;
				var posGlobale = new Vector3I((int)xGlobal, yMonde, (int)zGlobal);
				InventaireFlore[posGlobale] = FloreTypeGazon;
				EssayerPromouvoirGazonEnBuisson(posGlobale, xGlobal, zGlobal);
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
				int ly = posGlobale.Y - ChunkOffsetY * HauteurMax;
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
				_callbackBlocChutant?.Invoke(posSpawn, idItem, false, 0);
			}
			InventaireFlore.Remove(mort);
		}
		_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));
	}

	/// <summary>Copie les données du chunk pour envoi au client. Quantification byte[] pour RPC (divise poids par 4).</summary>
	public DonneesChunk ObtenirDonneesPourClient()
	{
		lock (_verrouVoxel)
		{
			int tx = TailleChunk + 1, ty = HauteurMax + 1, tz = TailleChunk + 1;
			bool estVideIntegral = true;
			var d = new DonneesChunk
			{
				CoordChunk = new Vector2I(ChunkOffsetX, ChunkOffsetZ),
				CoordChunkY = ChunkOffsetY,
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
					{
						byte mat = _materials[x, y, z];
						d.MaterialsFlat[idx++] = mat;
						if (estVideIntegral && mat > 0 && _densities[x, y, z] > 0f)
							estVideIntegral = false;
					}
			d.EstVideIntegral = estVideIntegral;
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
	byte couleurBaie = (byte)Joueur.IndexCouleurBaieDepuisVariante(ObtenirVarianteBuisson(typeFlore));
	var rng = new RandomNumberGenerator { Seed = unchecked((ulong)(uint)((posFlore.X * 911) ^ (posFlore.Z * 353) ^ 0xBEE35)) };
	Vector3 basePos = new Vector3(posFlore.X + 0.5f, posFlore.Y + 0.72f, posFlore.Z + 0.5f);
	for (int i = 0; i < q; i++)
	{
		Vector3 offset = new Vector3(rng.RandfRange(-0.22f, 0.22f), rng.RandfRange(0.02f, 0.14f), rng.RandfRange(-0.22f, 0.22f));
		_callbackBlocChutant?.Invoke(basePos + offset, ID_ITEM_BAIE, false, couleurBaie);
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
			_callbackBlocChutant?.Invoke(posSpawn + new Vector3(0f, 0.08f, 0f), ID_ITEM_BRANCHE_BUISSON, true, 0);
			if (EstBuissonPlein(typeFlore)) InventaireFlore[posFlore] = TypeBuissonSansBaies(typeFlore);
			else InventaireFlore.Remove(posFlore);
			break;

		case 1: // Dague maintenue: coupe après 3s -> 1 branche + 1 baie (si buisson plein), sans drop buisson.
			FaireTomberBaiesAuSolSiPlein(posFlore, typeFlore, 1);
			_callbackBlocChutant?.Invoke(posSpawn + new Vector3(0f, 0.08f, 0f), ID_ITEM_BRANCHE_BUISSON, true, 0);
			InventaireFlore.Remove(posFlore);
			break;

		case 2: // Pelle maintenue: déracine et récupère la plante replantable.
			FaireTomberBaiesAuSolSiPlein(posFlore, typeFlore);
			InventaireFlore.Remove(posFlore);
			// Pelle: on récupère la plante "sans baies"; les baies d'un buisson plein tombent déjà au sol juste au-dessus.
			_callbackBlocChutant?.Invoke(posSpawn + new Vector3(0f, 0.06f, 0f), ID_ITEM_BUISSON_VIDE, false, 0);
			break;

		default:
			return false;
	}

	_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));
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
	_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));
	return true;
}

/// <summary>Récolte des baies via interaction: uniquement buisson plein (type 1), qui devient ensuite buisson vide (type 2).</summary>
public bool RecolterBaiesBuisson(Vector3 pointImpactGlobal, float rayon, out int quantiteBaies, out byte indexCouleurBaie)
{
	quantiteBaies = 0;
	indexCouleurBaie = 0;
	if (!EssayerTrouverBuissonLePlusProche(pointImpactGlobal, rayon, out Vector3I posFlore, out byte typeFlore))
		return false;
	if (!EstBuissonPlein(typeFlore)) // Buisson déjà vide: aucune baie à ramasser.
		return false;

	byte variante = ObtenirVarianteBuisson(typeFlore);
	indexCouleurBaie = (byte)Joueur.IndexCouleurBaieDepuisVariante(variante);

	InventaireFlore[posFlore] = TypeBuissonSansBaies(typeFlore);
	_onFlorePurgée?.Invoke(new Vector2I(ChunkOffsetX, ChunkOffsetZ), ChunkOffsetY, new Dictionary<Vector3I, byte>(InventaireFlore));

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
						if (y <= 2) continue;
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
						if (y <= 2) continue;
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
		if (!EstDansLimitesChunk(x, y, z) || y <= 2) return;
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
				_estModifie = true;
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
				_estModifie = true;
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
			AuditerGraviteFlore();
		}
	}

	/// <summary>Met à jour un voxel local ET notifie le client (croissance arbres).</summary>
	public void ModifierVoxelEtNotifier(int lx, int ly, int lz, byte id)
	{
		SetVoxelLocal(lx, ly, lz, id);
		_estModifie = true;
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
