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
	/// <summary>Mode profondeur étendue (alpha-like) : le sol descend jusqu'à <see cref="_fondMondeY"/> au lieu de s'arrêter au socle Y=0.</summary>
	private readonly bool _profondeurEtendueActive;
	/// <summary>Y monde du socle dur (bedrock) en mode profondeur étendue (ex. -1000). Inutilisé si le mode est inactif.</summary>
	private readonly int _fondMondeY;

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

	/// <summary>Emplacements de gazon broutés (faune ou joueur), candidats à une repousse lente ~1×/jour. Runtime uniquement.</summary>
	private readonly HashSet<Vector3I> _gazonBroutePourRepousse = new HashSet<Vector3I>();

	public const byte FloreTypeGazon = 0;
	public const byte FloreTypeBuissonRougePlein = 1;
	public const byte FloreTypeBuissonRougeVide = 2;
	public const byte VarianteCouleurBuissonRouge = 0;
	public const byte VarianteBuissonAloeVera = 10;
	private const byte FloreTypeBuissonDebut = FloreTypeBuissonRougePlein;

	public static bool EstTypeBuisson(byte typeFlore) => typeFlore >= FloreTypeBuissonDebut;
	public static bool EstBuissonPlein(byte typeFlore) => EstTypeBuisson(typeFlore) && (((typeFlore - FloreTypeBuissonDebut) & 1) == 0);
	public static bool EstBuissonVide(byte typeFlore) => EstTypeBuisson(typeFlore) && (((typeFlore - FloreTypeBuissonDebut) & 1) == 1);
	public static byte ObtenirVarianteBuisson(byte typeFlore) => EstTypeBuisson(typeFlore) ? (byte)((typeFlore - FloreTypeBuissonDebut) / 2) : (byte)255;
	public static bool EstTypeAloeVera(byte typeFlore)
		=> EstTypeBuisson(typeFlore) && ObtenirVarianteBuisson(typeFlore) == VarianteBuissonAloeVera;
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
	private Action _demarrerBatchStabilite;
	private Action<Vector3I> _propagerStabiliteGlobal;
	private Func<Vector3I, bool> _estSolideGlobal;
	private Func<bool> _consommerBudgetStabilite;
	private Action<Vector3I, byte> _onVoxelModifie;
	private Action<Vector2I, int, Dictionary<Vector3I, byte>> _onFlorePurgée;

	/// <summary>Drapeau de souillure : true dès qu'un voxel persistant (sol/eau/air) change réellement.</summary>
	private bool _estModifie;
	/// <summary>True si chargé depuis disque. AUCUNE passe de génération ne doit jamais s'exécuter sur ce chunk.</summary>
	private bool _chargeDepuisDisque;
	/// <summary>Évite de rescanner tout le chunk à chaque ObtenirOuCreerChunk (coût CPU + renvoi réseau).</summary>
	private bool _reparationLegacyProfondeurFaite;

	public bool EstModifie => _estModifie;
	public bool EstChargeDepuisDisque => _chargeDepuisDisque;
	internal void MarquerModifie() { _estModifie = true; _contenuChangeDepuisEnvoiClient = true; }

	/// <summary>
	/// Anti-gaspillage réseau : true tant que le client n'a pas reçu la dernière version de CE chunk.
	/// Posé à true à la création (jamais envoyé) et à chaque mutation voxel ; remis à false après mise en file d'envoi.
	/// Évite de re-sérialiser (~260 Ko) et renvoyer en boucle un chunk inchangé que le client possède déjà.
	/// </summary>
	private bool _contenuChangeDepuisEnvoiClient = true;
	public bool ABesoinDeReenvoiClient() => _contenuChangeDepuisEnvoiClient;
	public void MarquerEnvoyeAuClient() => _contenuChangeDepuisEnvoiClient = false;
	internal void InvaliderCopieClient() => _contenuChangeDepuisEnvoiClient = true;

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

	public void ConfigurerStabiliteGlobale(
		Action demarrerBatch,
		Action<Vector3I> propagerGlobal,
		Func<Vector3I, bool> estSolideGlobal,
		Func<bool> consommerBudget)
	{
		_demarrerBatchStabilite = demarrerBatch;
		_propagerStabiliteGlobal = propagerGlobal;
		_estSolideGlobal = estSolideGlobal;
		_consommerBudgetStabilite = consommerBudget;
	}

	public Vector3I LocalVersGlobalVoxel(int lx, int ly, int lz) =>
		new Vector3I(ChunkOffsetX * TailleChunk + lx, ChunkOffsetY * HauteurMax + ly, ChunkOffsetZ * TailleChunk + lz);

	public Chunk_Serveur(int chunkOffsetX, int chunkOffsetY, int chunkOffsetZ, int tailleChunk, int hauteurMax, int seed,
		Action<Vector3, byte, bool, byte> callbackBlocChutant, Func<Vector2I, bool> chunkEstCharge, Action<Vector3> reveillerEau,
		bool generationAbysse = false, string dossierChunksSauvegarde = "",
		bool profondeurEtendueActive = false, int fondMondeY = 0)
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
		_profondeurEtendueActive = profondeurEtendueActive && !generationAbysse;
		_fondMondeY = fondMondeY;

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
		// Fréquence relevée: sur 1.5-2 km de marche, on rencontre plus souvent chaque macro-biome.
		_noiseBiomeForet.Frequency = 0.00074f;

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
			// IMPORTANT APISARA: certaines colonnes Y sont volontairement "skippées" pour la perf.
			// Si on laisse la densité par défaut à 0 (isolevel), on peut créer des fentes/artefacts de mesh.
			// On initialise tout en AIR explicite, puis les passes remplissent les zones solides/eau utiles.
			for (int x = 0; x <= TailleChunk; x++)
			{
				for (int y = 0; y <= HauteurMax; y++)
				{
					for (int z = 0; z <= TailleChunk; z++)
					{
						_densities[x, y, z] = -10.0f;
						_materials[x, y, z] = 0;
						_densitiesEau[x, y, z] = -1.0f;
					}
				}
			}
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
						bool noyauMurailleAbysse = false;
						if (colonneExtrusionTrou
							&& globalY >= yMinBandeExtrusionAbysse
							&& globalY <= yMaxBandeExtrusionAbysse)
						{
							noyauMurailleAbysse = EvaluerNoyauMurailleContinueAbysse(xGlobal, globalY, zGlobal, out _);
							extrusionParoiAbysse = EvaluerExtrusionParoiAbysse(xGlobal, globalY, zGlobal, out _);
							if (!extrusionParoiAbysse)
								extrusionAnneauAbysse = EvaluerExtrusionAnneauAbysse(xGlobal, globalY, zGlobal, out _);
							if (!extrusionParoiAbysse && !extrusionAnneauAbysse)
								picSupplementSpiraleAbysse = EvaluerPicSupplementSpiraleAbysse(xGlobal, globalY, zGlobal, out _);
						}

						if (noyauMurailleAbysse || extrusionParoiAbysse || extrusionAnneauAbysse || picSupplementSpiraleAbysse)
						{
							_densities[x, y, z] = 10.0f;
							_materials[x, y, z] = 2;
							_densitiesEau[x, y, z] = -1.0f;
							sommetSolide[x, z] = y;
							continue;
						}

						_densitiesEau[x, y, z] = -1.0f;

						// Mode profondeur étendue : socle dur (bedrock) descendu au fond du monde, rien dessous,
						// et plus de socle artificiel à Y=0 (la roche/les grottes descendent en continu jusqu'au fond).
						bool sousFondMondeProfond = _profondeurEtendueActive && globalY < _fondMondeY - 2f;
						bool socleFondMondeProfond = _profondeurEtendueActive
							&& globalY <= _fondMondeY
							&& globalY >= _fondMondeY - 2f
							&& !dansTrouNoirCol;
						bool socleZeroMonde = !_profondeurEtendueActive
							&& globalY >= 0f
							&& globalY <= 2f
							&& !dansTrouNoirCol;
						if (sousFondMondeProfond)
						{
							_densities[x, y, z] = -10.0f;
							_materials[x, y, z] = 0;
						}
						else if (socleFondMondeProfond || socleZeroMonde)
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
							float temperatureNormSurface = (temperature + 1f) * 0.5f;
							float humiditeNormSurface = (humidite + 1f) * 0.5f;
							bool boueTropicaleFlorable = mat == 7 && temperatureNormSurface > 0.78f && humiditeNormSurface > 0.70f;
							// Gazon sur voxel herbe (ID 1), terrain plat — Alpha classique ou deux plaines APISARA (jungle).
							bool floreAlpha = !_generationAbysseActive
								&& (EstMateriauSupportGazon(mat) || boueTropicaleFlorable)
								&& TerrainAssezPlat((int)xGlobal, (int)zGlobal)
								&& TerrainAvecMargeBord((int)xGlobal, (int)zGlobal);
							bool florePlaineJungleAbysse = _generationAbysseActive
								&& EstPlaineJungleAbysse(xGlobal, zGlobal)
								&& (EstMateriauSupportGazon(mat) || boueTropicaleFlorable)
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
							else if (!_generationAbysseActive
								&& EstMateriauSupportAloeVera(mat)
								&& TerrainAssezPlat((int)xGlobal, (int)zGlobal)
								&& TerrainAvecMargeBord((int)xGlobal, (int)zGlobal))
							{
								float altitudeFlore = globalY;
								if (altitudeFlore > NIVEAU_MIN_FLORE && altitudeFlore < NIVEAU_MAX_FLORE)
								{
									var posGlobale = new Vector3I((int)xGlobal, (int)globalY, (int)zGlobal);
									EssayerPlacerAloeVera(posGlobale, xGlobal, zGlobal);
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
							// Seuil abaissé (0.55 -> 0.50) : les grottes naturelles étaient quasi absentes auparavant.
							if (activerGrottes && valeurGrotte > 0.50f)
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
			// Veines de minerais: système pré-intégré, désactivé tant que les switches restent à false.
			AppliquerVeinesMinerais(hauteurColonne, temperatureColonne, humiditeColonne);
			AppliquerBiomeParasiteCornichesAbysse();
			AppliquerEnsemencementFloreTrouAbysse(notifierClient: false);
			// Couches profondes (sous Y=0) : pas d'eau de mer (aucun océan sous terre) ni d'arbres de surface.
			bool coucheProfondeSousSurface = _profondeurEtendueActive && ChunkOffsetY < 0;
			if (!coucheProfondeSousSurface)
				InitialiserEauVolumetrique(sommetSolide);

			// Pass L-System : injection des Chênes (voxels bois ID 30, feuilles ID 31)
			if (!coucheProfondeSousSurface)
				InjecterArbresLSystem();
			// Garantie de lisibilité gameplay: au moins un buisson si le chunk contient du gazon.
			AssurerBuissonMinimalDansChunk();
			// RÈGLE : Chunk procédural non touché par le joueur → jamais sauvegardé (régénération à la demande).
		}
	}

	/// <summary>
	/// Injection initiale d'eau volumétrique (une seule fois à la génération du chunk).
	/// Un voxel devient eau uniquement s'il est dans une colonne ouverte au ciel sous le niveau d'eau.
	/// On évite la propagation latérale systématique qui inondait toutes les grottes connectées.
	/// En profondeur (tranches 100 m), le plafond d'eau utilise Y monde (pas un yMax local à 3 sur coordY=1).
	/// </summary>
	private void InitialiserEauVolumetrique(int[,] sommetSolide)
	{
		if (_densities == null || _densitiesEau == null || _materials == null) return;
		int niveauEauMonde = _generationAbysseActive ? AbyssNiveauEau : NiveauEau;
		int yBaseMonde = ChunkOffsetY * HauteurMax;
		int yMaxLocal = _profondeurEtendueActive
			? ConstantesProfondeurVerticale.ObtenirYMaxEauLocalTranche(ChunkOffsetY, HauteurMax, niveauEauMonde)
			: Math.Min(ObtenirNiveauEauActif(), HauteurMax);
		if (yMaxLocal <= 2) return;
		var roleEau = _profondeurEtendueActive
			? ConstantesProfondeurVerticale.ObtenirRoleTrancheEauMer(ChunkOffsetY, HauteurMax, niveauEauMonde)
			: ConstantesProfondeurVerticale.RoleTrancheEauMer.Aucun;
		bool remplissageVolume3D = roleEau == ConstantesProfondeurVerticale.RoleTrancheEauMer.Chapeau
			|| roleEau == ConstantesProfondeurVerticale.RoleTrancheEauMer.Corps;
		for (int x = 0; x <= TailleChunk; x++)
		{
			for (int z = 0; z <= TailleChunk; z++)
			{
				float xGlobal = ChunkOffsetX * TailleChunk + x;
				float zGlobal = ChunkOffsetZ * TailleChunk + z;
				// Le cœur abyssal doit rester un vide absolu, pas un puits rempli d'eau.
				if (EstDansTrouNoirAbysseMonde(xGlobal, zGlobal))
					continue;
				int hauteurSurface = CalculerHauteurTerrain((int)xGlobal, (int)zGlobal);
				bool colonneSousNiveauMer = hauteurSurface < niveauEauMonde;
				int yDebut;
				if (remplissageVolume3D)
				{
					// Tranche 100 m : eau uniquement entre la surface monde (≈103) et la mer — pas dans les grottes sous Y=0.
					int yMondeDebutEau = hauteurSurface + 1;
					yDebut = Mathf.Clamp(yMondeDebutEau - yBaseMonde, 0, yMaxLocal);
					if (yDebut > yMaxLocal)
						continue;
				}
				else
					yDebut = Mathf.Clamp(sommetSolide[x, z] + 1, 0, yMaxLocal);
				for (int y = yDebut; y <= yMaxLocal; y++)
				{
					int yMonde = yBaseMonde + y;
					if (yMonde > niveauEauMonde) continue;
					if (yMonde <= hauteurSurface) continue;
					if (!EstVoxelAirSansVerrou(x, y, z)) continue;
					// Mares peu profondes (1 bloc) : pas de test « ciel ouvert » qui bloquait les cuvettes.
					if (!remplissageVolume3D && !colonneSousNiveauMer && !EstVoxelOuvertAuCielMonde(x, y, z, niveauEauMonde)) continue;
					DefinirEauSansVerrou(x, y, z);
				}
			}
		}
	}

	/// <summary>Colonne ouverte jusqu'au niveau de la mer (Y monde), pas seulement le haut de la tranche courante.</summary>
	private bool EstVoxelOuvertAuCielMonde(int x, int y, int z, int niveauEauMonde)
	{
		int yMonde = ChunkOffsetY * HauteurMax + y;
		for (int ny = y + 1; ny <= HauteurMax; ny++)
		{
			int yMondeN = ChunkOffsetY * HauteurMax + ny;
			if (yMondeN > niveauEauMonde)
				return true;
			if (_densities[x, ny, z] > Isolevel)
				return false;
		}
		return true;
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

	private bool EstVoxelOuvertAuCielSansVerrou(int x, int y, int z)
	{
		for (int ny = y + 1; ny <= HauteurMax; ny++)
		{
			if (_densities[x, ny, z] > Isolevel)
				return false;
		}
		return true;
	}

	private void DefinirEauSansVerrou(int x, int y, int z)
	{
		_densities[x, y, z] = -10.0f;
		_materials[x, y, z] = 4;
		_densitiesEau[x, y, z] = 1.0f;
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

	private void EssayerPlacerAloeVera(Vector3I posGlobale, float xGlobal, float zGlobal)
	{
		byte matSurface = ObtenirMateriauSurfaceMonde(posGlobale.X, posGlobale.Y, posGlobale.Z);
		if (!EstMateriauSupportAloeVera(matSurface))
			return;
		// Le sable/bio aride sous l'eau ne doit pas être traité comme désert exploitable.
		if (posGlobale.Y - 1 <= NiveauEau)
			return;
		float temperature = _noiseTemperature.GetNoise2D(xGlobal, zGlobal);
		float humiditeNorm = (CalculerHumiditeGlobale(xGlobal, zGlobal) + 1f) * 0.5f;
		if (temperature < 0.04f || humiditeNorm > 0.62f)
			return;
		float baseChance = Mathf.Lerp(0.028f, 0.085f, Mathf.Clamp((temperature - 0.04f) / 0.60f, 0f, 1f));
		float bonusSec = Mathf.Lerp(1.0f, 1.9f, Mathf.Clamp((0.62f - humiditeNorm) / 0.62f, 0f, 1f));
		float chance = Mathf.Clamp(baseChance * bonusSec, 0f, 0.17f);
		// Règle gameplay: sur terre aride (ID 6), l'aloe est deux fois moins fréquent que sur sable désert (ID 3).
		if (matSurface == 6)
			chance *= 0.5f;
		if (DeterministicRand(xGlobal * 1.37f + 19f, zGlobal * 1.91f + 31f) >= chance)
			return;
		if (!PeutPlacerBuissonAvecEspacement(posGlobale, 2))
			return;
		InventaireFlore[posGlobale] = ConstruireTypeBuisson(VarianteBuissonAloeVera, plein: false);
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
			profondeurEau = Mathf.Max(1, (int)(tSmooth * profondeurMax));
		}
		return hauteurBase - profondeurEau;
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

	/// <summary>Sol d'une grotte (cavité fermée) — évite le plafond détecté par <see cref="ObtenirHauteurSurfaceLocale"/>.</summary>
	public (int ySol, byte mat) ObtenirSolGrotteEtMateriau(int lx, int lz)
	{
		if (lx < 0 || lx > TailleChunk || lz < 0 || lz > TailleChunk || _densities == null)
			return (-1, 0);
		const int hauteurMinCavite = 4;
		lock (_verrouVoxel)
		{
			for (int y = 2; y < HauteurMax - 4; y++)
			{
				if (_densities[lx, y, lz] <= Isolevel) continue;
				if (_densities[lx, y + 1, lz] > Isolevel) continue;
				int yAir = y + 1;
				while (yAir < HauteurMax
					&& _densities[lx, yAir, lz] <= Isolevel
					&& (_densitiesEau == null || _densitiesEau[lx, yAir, lz] <= Isolevel))
					yAir++;
				if (yAir - (y + 1) < hauteurMinCavite) continue;
				if (yAir > HauteurMax - 2) continue;
				if (_densities[lx, yAir, lz] <= Isolevel) continue;
				byte mat = _materials[lx, y, lz];
				if (mat == 4) mat = 3;
				return (y, mat);
			}
		}
		return (-1, 0);
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

	private static float UniformiserSelectionMacroBiome(float macroBrut)
	{
		// Les bruits Perlin/Fbm sont centrés vers 0.5, ce qui rend les extrêmes rares.
		// On applique une CDF gaussienne approximative pour rétablir des tranches plus équitables.
		float z = (macroBrut - 0.5f) / 0.23f;
		float uniforme = 0.5f * (1f + ApproxErf(z * 0.70710677f));
		return Mathf.Clamp(uniforme, 0f, 1f);
	}

	private static float ApproxErf(float x)
	{
		// Abramowitz & Stegun 7.1.26 (erreur max ~1.5e-7).
		float signe = x < 0f ? -1f : 1f;
		float ax = Mathf.Abs(x);
		float t = 1f / (1f + 0.3275911f * ax);
		float poly = (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) * t;
		float y = 1f - poly * MathF.Exp(-ax * ax);
		return signe * y;
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
		float bruitSableQuartz = _noiseHumiditeDetail.GetNoise2D(xGlobal * 2.75f + 5100f, zGlobal * 2.75f - 3900f);
		// Seuils abaissés (0.86/0.93 étaient quasi inatteignables → le sable de quartz n'apparaissait jamais).
		if (fondEau && bruitSableQuartz > 0.55f) return Atlas_Matiere.IdVoxelSableQuartz;
		if (bordEau && bruitSableQuartz > 0.75f) return Atlas_Matiere.IdVoxelSableQuartz;
		if (globalY <= NiveauPlage) return (humidite > 0.2f) ? (byte)7 : (byte)3;  // Plage : seuil doux
		// Pilotage climat demandé (température × humidité), avec une légère variation organique locale.
		float detailHumide = _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.55f + 1400f, zGlobal * 1.55f + 1400f);
		float detailSec = _noiseHumiditeDetail.GetNoise2D(xGlobal * 1.9f + 17000f, zGlobal * 1.9f + 17000f);
		float bruitOrganique = (_noiseBiomeForet.GetNoise2D(xGlobal * 0.83f - 4200f, zGlobal * 0.83f + 4200f) + 1f) * 0.5f;
		float humiditeNorm = (humidite + 1f) * 0.5f;
		float temperatureNorm = (temperature + 1f) * 0.5f;
		float detailHumideNorm = (detailHumide + 1f) * 0.5f;
		float detailSecNorm = (detailSec + 1f) * 0.5f;

		// Très froid + très humide => gelé ; très froid + peu/pas humide => neige.
		if (temperatureNorm < 0.22f)
		{
			if (humiditeNorm > 0.74f && (bruitOrganique > 0.42f || detailHumideNorm > 0.58f))
				return 9; // Terre gelée
			return 5;    // Neige
		}

		// Froid (peu importe humidité) => neige.
		if (temperatureNorm < 0.42f) return 5;

		// Très chaud :
		// - très humide => boue (avec flore possible ensuite),
		// - peu humide => boue,
		// - sec => sable.
		if (temperatureNorm > 0.78f)
		{
			if (humiditeNorm > 0.70f) return 7; // Boue
			if (humiditeNorm > 0.36f) return 7; // Boue (peu humide)
			return 3; // Sable (sec)
		}

		// Chaud :
		// - très humide => argile,
		// - peu humide => aride,
		// - sec => aride + sable (poches).
		if (temperatureNorm > 0.62f)
		{
			if (humiditeNorm > 0.70f) return 8; // Argile
			if (humiditeNorm > 0.36f) return 6; // Aride
			return (detailSecNorm > 0.52f || bruitOrganique > 0.63f) ? (byte)3 : (byte)6; // Aride+sable
		}

		// Tempéré (humide / très humide / sec) => herbe.
		return 1;
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
			byte version = _profondeurEtendueActive
				? ConstantesProfondeurVerticale.VersionChunkProfondeur
				: (byte)1;
			writer.Write(version);
			if (_profondeurEtendueActive)
				writer.Write((ushort)HauteurMax);
			writer.Write(donnees.Length);
			writer.Write(donnees);
		}
		if (Monde_Serveur.JournaliserChunksVerbeux)
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

					bool noyauMuraille = EvaluerNoyauMurailleContinueAbysse(xGlobal, globalY, zGlobal, out _);
					bool extrusionParoi = EvaluerExtrusionParoiAbysse(xGlobal, globalY, zGlobal, out _);
					bool extrusionAnneau = !extrusionParoi && EvaluerExtrusionAnneauAbysse(xGlobal, globalY, zGlobal, out _);
					bool extrusionPic = !extrusionParoi && !extrusionAnneau && EvaluerPicSupplementSpiraleAbysse(xGlobal, globalY, zGlobal, out _);
					if (!noyauMuraille && !extrusionParoi && !extrusionAnneau && !extrusionPic) continue;

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

	/// <summary>
	/// Sauvegardes créées avant la profondeur étendue : vide ou bedrock artificiel sous Y≈0.
	/// Rebouche le sous-sol procédural (roche + grottes) sans toucher au ciel ni au bedrock du fond monde.
	/// </summary>
	/// <returns>True si des voxels ont été rebouchés (chunk à renvoyer au client).</returns>
	public bool ReparerSousSolProfondeurLegacySiChargee()
	{
		// Tranches 100 m (v2) : pas de rebouchage legacy 720 m.
		if (_profondeurEtendueActive)
			return false;
		if (_reparationLegacyProfondeurFaite)
			return false;
		_reparationLegacyProfondeurFaite = true;
		if (!_profondeurEtendueActive || _generationAbysseActive || !_chargeDepuisDisque)
			return false;
		if (_densities == null || _materials == null)
			return false;

		const bool activerGrottes = true;
		bool modifie = false;
		int taille = TailleChunk + 1;
		lock (_verrouVoxel)
		{
			for (int x = 0; x < taille; x++)
			for (int z = 0; z < taille; z++)
			{
				int xInt = ChunkOffsetX * TailleChunk + x;
				int zInt = ChunkOffsetZ * TailleChunk + z;
				int hauteurSurface = CalculerHauteurTerrain(xInt, zInt);
				float trancheBas = ChunkOffsetY * HauteurMax;
				float trancheHaut = trancheBas + HauteurMax;
				float globalYMin;
				float globalYMax;
				if (ChunkOffsetY == 0)
				{
					// Ancien socle Y=0 : reboucher seulement le sous-sol peu profond.
					globalYMin = Mathf.Max(_fondMondeY + 3f, trancheBas);
					globalYMax = Mathf.Min(hauteurSurface - 4f, trancheBas + 64f);
				}
				else
				{
					// Jonction entre tranches : haut de la couche (ex. Y -720..-650 si coordY=-1).
					globalYMax = Mathf.Min(hauteurSurface - 4f, trancheHaut);
					globalYMin = Mathf.Max(trancheBas, Mathf.Max(_fondMondeY + 3f, globalYMax - 96f));
				}
				if (globalYMin >= globalYMax)
					continue;
				ObtenirPlageIndiceYMonde(globalYMin, globalYMax, out int yDebut, out int yFin);
				if (yDebut > yFin)
					continue;

				for (int y = yDebut; y <= yFin; y++)
				{
					float globalY = ChunkOffsetY * HauteurMax + y;
					if (_densities[x, y, z] > Isolevel)
						continue;

					float valeurGrotte = activerGrottes
						? _noiseCavernes.GetNoise3D(xInt, globalY, zInt)
						: -1f;
					if (activerGrottes && valeurGrotte > 0.50f)
						continue;

					_densities[x, y, z] = 10.0f;
					_materials[x, y, z] = 2;
					if (_densitiesEau != null)
						_densitiesEau[x, y, z] = -1.0f;
					modifie = true;
				}
			}
		}

		if (modifie)
			_estModifie = true;
		return modifie;
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
				bool supportGazon = EstMateriauSupportGazon(mat);
				bool supportAloe = !_generationAbysseActive && EstMateriauSupportAloeVera(mat);
				if (!supportGazon && !supportAloe) continue;
				if (!TerrainAssezPlatDepuisDonnees(x, z)) continue;
				if (!TerrainAvecMargeBordDepuisDonnees(x, z)) continue;
				int yMonde = ChunkOffsetY * HauteurMax + ySurface;
				if (yMonde <= NIVEAU_MIN_FLORE || yMonde >= NIVEAU_MAX_FLORE) continue;
				var posGlobale = new Vector3I((int)xGlobal, yMonde, (int)zGlobal);
				if (supportGazon)
				{
					InventaireFlore[posGlobale] = FloreTypeGazon;
					EssayerPromouvoirGazonEnBuisson(posGlobale, xGlobal, zGlobal);
				}
				else
				{
					EssayerPlacerAloeVera(posGlobale, xGlobal, zGlobal);
				}
			}
		AssurerBuissonMinimalDansChunk();
	}

	private static bool EstMateriauSupportGazon(byte mat)
	{
		// Gazon uniquement sur voxel herbe (ID 1).
		return mat == 1;
	}

	private static bool EstMateriauSupportAloeVera(byte mat)
	{
		// Aloe vera: désert/aride.
		return mat == 3 || mat == 6;
	}

	private byte ObtenirMateriauSurfaceMonde(int xMonde, int yMonde, int zMonde)
	{
		int lx = xMonde - ChunkOffsetX * TailleChunk;
		int ly = yMonde - ChunkOffsetY * HauteurMax;
		int lz = zMonde - ChunkOffsetZ * TailleChunk;
		if (!EstDansLimitesChunk(lx, ly, lz))
			return 0;
		lock (_verrouVoxel)
			return _materials[lx, ly, lz];
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

private bool EssayerTrouverBuissonLePlusProche(Vector3 pointImpactGlobal, float rayon, out Vector3I posFlore, out byte typeFlore, bool pleinSeulement = false)
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
		if (pleinSeulement && !EstBuissonPlein(kv.Value)) continue;
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
public bool EssayerDetecterBuisson(Vector3 pointImpactGlobal, float rayon, out Vector3 posBuisson, out byte typeFlore, bool pleinSeulement = false)
{
	posBuisson = Vector3.Zero;
	typeFlore = 0;
	if (!EssayerTrouverBuissonLePlusProche(pointImpactGlobal, rayon, out Vector3I pos, out byte type, pleinSeulement))
		return false;
	posBuisson = new Vector3(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f);
	typeFlore = type;
	return true;
}

/// <summary>Récolte ciblée buisson: 0=hachette (branche), 1=dague (coupe), 2=pelle (plante replantable), 3=dague aloe (sans branche).</summary>
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

		case 3: // Dague + aloe: récolte dédiée, sans branche ni drop buisson vide au sol.
			InventaireFlore.Remove(posFlore);
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

}
