using Godot;

public partial class Chunk_Serveur
{
    private const bool ActiverSystemeMinerais = true;

    private const bool SpawnMineraiCharbon = true;
    private const bool SpawnMineraiJade = false;
    private const bool SpawnMineraiOpale = false;
    private const bool SpawnMineraiDiamant = false;
    private const bool SpawnMineraiTopaze = false;
    private const bool SpawnMineraiRubis = false;
    private const bool SpawnMineraiSaphir = false;
    private const bool SpawnMineraiEmeraude = false;
    private const bool SpawnMineraiAmethyste = false;
    private const bool SpawnMineraiQuartz = true;
    private const bool SpawnMineraiPalladium = false;
    private const bool SpawnMineraiPlatine = false;
    private const bool SpawnMineraiArgent = false;
    private const bool SpawnMineraiOr = false;
    private const bool SpawnMineraiBismuth = false;
    private const bool SpawnMineraiManganese = false;
    private const bool SpawnMineraiTitane = false;
    private const bool SpawnMineraiTungstene = false;
    private const bool SpawnMineraiCobalt = false;
    private const bool SpawnMineraiChrome = false;
    private const bool SpawnMineraiNickel = false;
    private const bool SpawnMineraiAluminium = false;
    private const bool SpawnMineraiFer = false;
    private const bool SpawnMineraiPlomb = false;
    private const bool SpawnMineraiZinc = false;
    private const bool SpawnMineraiEtain = false;
    private const bool SpawnMineraiCuivre = false;
    private const bool SpawnMineraiSoufre = false;
    private const bool SpawnMineraiSalpetre = false;
    private const bool SpawnMineraiUranium = false;
    private const bool SpawnMineraiThorium = false;
    private const bool SpawnMineraiPlutonium = false;
    private const bool SpawnMineraiSel = false;
    private const bool SpawnMineraiGraphite = false;
    private const bool SpawnMineraiCalcaire = false;
    private const bool SpawnMineraiGypse = false;
    private const bool SpawnMineraiObsidienne = false;

    private const byte IdMineraiCharbon = 10;
    private const byte IdMineraiQuartz = 19;
    private const byte IdMineraiEtain = 37;
    /// <summary>Part des voxels de filon quartz remplacés par de l'étain (ID 37) à la pose.</summary>
    private const float QuartzFractionEtainDansFilon = 0.10f;
    private const int SeuilHauteurMontagneCharbon = 150;
    private const int YMinFilonsMontagneCharbon = 120;

    /// <summary>Bande monde principale des filons quartz (Y global).</summary>
    private const int QuartzYMinPrincipal = -300;
    private const int QuartzYMaxPrincipal = -100;
    private const int QuartzSeuilHauteurMontagne = 150;
    private const float QuartzCelluleAnchorsXz = 28f;
    private const float QuartzSeuilPresencePrincipal = 0.38f;
    /// <summary>Montagne : mini-filons très rares au-dessus de Y=-100.</summary>
    private const float QuartzSeuilPresenceMontagne = 0.955f;

    private enum CategorieFilonsCharbon
    {
        Montagne,
        ArideFroid,
        TempereBoue,
    }

    /// <summary>
    /// Filon charbon = nappe horizontale (~1 m en Y), large en X/Z.
    /// Plusieurs nappes peuvent s'empiler (PasY) ; plus profond = patch X/Z plus large + plus rare.
    /// </summary>
    private readonly struct ParametresTierFilon
    {
        public ParametresTierFilon(
            int yMin, int yMax,
            float pasY, float freqXz,
            float seuilPresence,
            float epaisseurVerticaleMin, float epaisseurVerticaleMax,
            float tailleCelluleXzMetres)
        {
            YMin = yMin;
            YMax = yMax;
            PasY = pasY;
            FreqXz = freqXz;
            SeuilPresence = seuilPresence;
            EpaisseurVerticaleMin = epaisseurVerticaleMin;
            EpaisseurVerticaleMax = epaisseurVerticaleMax;
            TailleCelluleXzMetres = tailleCelluleXzMetres;
        }

        public int YMin { get; }
        public int YMax { get; }
        /// <summary>Distance entre centres de nappes empilables (m).</summary>
        public float PasY { get; }
        public float FreqXz { get; }
        /// <summary>Plus haut = filon plus rare.</summary>
        public float SeuilPresence { get; }
        /// <summary>Épaisseur verticale du filon (~1 m max — le filon ne « monte » pas en Y).</summary>
        public float EpaisseurVerticaleMin { get; }
        public float EpaisseurVerticaleMax { get; }
        /// <summary>Quantification horizontale : plus grand = filon plus large en X/Z (profondeur).</summary>
        public float TailleCelluleXzMetres { get; }
    }

    // Profondeur ↑ → seuil ↑ (rare), cellule X/Z ↑ (gros patch), même ~1 m d'épaisseur verticale.
    private static readonly ParametresTierFilon TierPetitCharbon = new(50, 95, 6f, 0.004f, 0.72f, 0.45f, 1.0f, 5f);
    private static readonly ParametresTierFilon TierMoyenCharbon = new(31, 49, 10f, 0.0025f, 0.80f, 0.45f, 1.0f, 10f);
    private static readonly ParametresTierFilon TierMegaCharbon = new(0, 30, 16f, 0.0015f, 0.86f, 0.45f, 1.0f, 18f);
    private static readonly ParametresTierFilon TierMontagneCharbon = new(YMinFilonsMontagneCharbon, int.MaxValue, 12f, 0.005f, 0.90f, 0.45f, 1.0f, 8f);

    private readonly struct RegleMinerai
    {
        public RegleMinerai(byte id, bool actif, int profondeurMin, int profondeurMax, float frequence, float seuil)
        {
            Id = id;
            Actif = actif;
            ProfondeurMin = profondeurMin;
            ProfondeurMax = profondeurMax;
            Frequence = frequence;
            Seuil = seuil;
        }

        public byte Id { get; }
        public bool Actif { get; }
        public int ProfondeurMin { get; }
        public int ProfondeurMax { get; }
        public float Frequence { get; }
        public float Seuil { get; }
    }

    private static readonly RegleMinerai[] ReglesMinerais =
    {
        new(11, SpawnMineraiJade, 8, 80, 0.020f, 0.86f),
        new(12, SpawnMineraiOpale, 14, 90, 0.021f, 0.87f),
        new(13, SpawnMineraiDiamant, 22, 120, 0.024f, 0.91f),
        new(14, SpawnMineraiTopaze, 12, 95, 0.022f, 0.88f),
        new(15, SpawnMineraiRubis, 16, 110, 0.024f, 0.90f),
        new(16, SpawnMineraiSaphir, 16, 110, 0.024f, 0.90f),
        new(17, SpawnMineraiEmeraude, 14, 105, 0.022f, 0.89f),
        new(18, SpawnMineraiAmethyste, 18, 125, 0.023f, 0.90f),
        new(19, false, 6, 65, 0.018f, 0.79f), // quartz : filons verticaux dédiés (AppliquerFilonsQuartz)
        new(20, SpawnMineraiPalladium, 20, 140, 0.025f, 0.92f),
        new(21, SpawnMineraiPlatine, 18, 130, 0.024f, 0.91f),
        new(22, SpawnMineraiArgent, 10, 95, 0.021f, 0.86f),
        new(23, SpawnMineraiOr, 14, 110, 0.023f, 0.89f),
        new(24, SpawnMineraiBismuth, 10, 85, 0.020f, 0.86f),
        new(25, SpawnMineraiManganese, 10, 95, 0.021f, 0.87f),
        new(26, SpawnMineraiTitane, 18, 130, 0.024f, 0.91f),
        new(27, SpawnMineraiTungstene, 22, 145, 0.025f, 0.92f),
        new(28, SpawnMineraiCobalt, 16, 120, 0.023f, 0.90f),
        new(29, SpawnMineraiChrome, 14, 112, 0.023f, 0.89f),
        new(32, SpawnMineraiNickel, 12, 104, 0.022f, 0.88f),
        new(33, SpawnMineraiAluminium, 6, 70, 0.019f, 0.83f),
        new(34, SpawnMineraiFer, 8, 90, 0.020f, 0.85f),
        new(35, SpawnMineraiPlomb, 12, 110, 0.022f, 0.88f),
        new(36, SpawnMineraiZinc, 10, 95, 0.021f, 0.87f),
        new(37, SpawnMineraiEtain, 8, 85, 0.020f, 0.86f), // étain : 10 % des filons quartz (pas veines génériques)
        new(38, SpawnMineraiCuivre, 8, 92, 0.020f, 0.86f),
        new(39, SpawnMineraiSoufre, 10, 100, 0.021f, 0.88f),
        new(40, SpawnMineraiSalpetre, 6, 75, 0.019f, 0.84f),
        new(41, SpawnMineraiUranium, 24, 170, 0.026f, 0.93f),
        new(42, SpawnMineraiThorium, 24, 170, 0.026f, 0.93f),
        new(43, SpawnMineraiPlutonium, 28, 185, 0.027f, 0.94f),
        new(44, SpawnMineraiSel, 4, 50, 0.018f, 0.80f),
        new(45, SpawnMineraiGraphite, 8, 88, 0.020f, 0.86f),
        new(46, SpawnMineraiCalcaire, 2, 45, 0.017f, 0.79f),
        new(47, SpawnMineraiGypse, 2, 42, 0.017f, 0.79f),
        new(48, SpawnMineraiObsidienne, 20, 150, 0.024f, 0.92f),
    };

    private static CategorieFilonsCharbon DeterminerCategorieFilonsCharbon(int hauteurSurface, byte materiauSurface)
    {
        if (hauteurSurface >= SeuilHauteurMontagneCharbon)
            return CategorieFilonsCharbon.Montagne;

        return materiauSurface switch
        {
            3 or 5 or 8 or 9 => CategorieFilonsCharbon.ArideFroid,
            _ => CategorieFilonsCharbon.TempereBoue,
        };
    }

    private static bool EstDansCoucheHorizontaleCharbon(
        float xGlobal, float zGlobal, float globalY,
        ParametresTierFilon tier)
    {
        if (globalY < tier.YMin || globalY > tier.YMax)
            return false;

        float pasCouche = Mathf.Max(1f, tier.PasY);
        float centreY = Mathf.Floor(globalY / pasCouche) * pasCouche + pasCouche * 0.5f;
        float distY = Mathf.Abs(globalY - centreY);
        if (distY > tier.EpaisseurVerticaleMax)
            return false;

        // Patch horizontal : même décision pour toute une cellule X/Z (gros filons en profondeur).
        float cell = Mathf.Max(1f, tier.TailleCelluleXzMetres);
        float qx = Mathf.Floor(xGlobal / cell);
        float qz = Mathf.Floor(zGlobal / cell);
        float presence = DeterministicRand(
            qx * tier.FreqXz,
            qz * tier.FreqXz + centreY * 0.003f);
        if (presence < tier.SeuilPresence)
            return false;

        float plageSeuil = 1f - tier.SeuilPresence;
        float facteur = plageSeuil > 0.0001f ? (presence - tier.SeuilPresence) / plageSeuil : 1f;
        float epaisseurVert = tier.EpaisseurVerticaleMin + facteur * (tier.EpaisseurVerticaleMax - tier.EpaisseurVerticaleMin);
        return distY <= epaisseurVert;
    }

    private static bool EstDansFilonCharbonCategorie(
        float xGlobal, float zGlobal, float globalY,
        CategorieFilonsCharbon categorie)
    {
        return categorie switch
        {
            CategorieFilonsCharbon.Montagne => globalY >= YMinFilonsMontagneCharbon
                && EstDansCoucheHorizontaleCharbon(xGlobal, zGlobal, globalY, TierMontagneCharbon),
            CategorieFilonsCharbon.ArideFroid => globalY >= TierMegaCharbon.YMin && globalY <= TierMegaCharbon.YMax
                && EstDansCoucheHorizontaleCharbon(xGlobal, zGlobal, globalY, TierMegaCharbon),
            CategorieFilonsCharbon.TempereBoue => globalY switch
            {
                >= 50 and <= 95 => EstDansCoucheHorizontaleCharbon(xGlobal, zGlobal, globalY, TierPetitCharbon),
                >= 31 and <= 49 => EstDansCoucheHorizontaleCharbon(xGlobal, zGlobal, globalY, TierMoyenCharbon),
                >= 0 and <= 30 => EstDansCoucheHorizontaleCharbon(xGlobal, zGlobal, globalY, TierMegaCharbon),
                _ => false
            },
            _ => false,
        };
    }

    private static void ObtenirPlageYLocalFilonsCharbon(
        CategorieFilonsCharbon categorie, int chunkOffsetY, int hauteurMax,
        out int yMinLocal, out int yMaxLocal)
    {
        int baseY = chunkOffsetY * hauteurMax;
        int globalYMin;
        int globalYMax;
        switch (categorie)
        {
            case CategorieFilonsCharbon.Montagne:
                globalYMin = YMinFilonsMontagneCharbon;
                globalYMax = baseY + hauteurMax;
                break;
            case CategorieFilonsCharbon.ArideFroid:
                globalYMin = TierMegaCharbon.YMin;
                globalYMax = TierMegaCharbon.YMax;
                break;
            default:
                globalYMin = TierMegaCharbon.YMin;
                globalYMax = TierPetitCharbon.YMax;
                break;
        }

        yMinLocal = Mathf.Clamp(globalYMin - baseY, 0, hauteurMax);
        yMaxLocal = Mathf.Clamp(globalYMax - baseY, 0, hauteurMax);
        if (yMinLocal > yMaxLocal)
        {
            yMinLocal = 0;
            yMaxLocal = -1;
        }
    }

    /// <summary>
    /// Filon quartz = veine quasi verticale (rarement parfaite), serpentin en X/Z,
    /// épaisseur variable 1–5 m, longueur courte à très longue.
    /// </summary>
    private readonly struct ParametresFilonQuartz
    {
        public ParametresFilonQuartz(
            float seuilPresence, float longueurMin, float longueurMax,
            float epaisseurMin, float epaisseurMax,
            float wanderMin, float wanderMax, float chanceVerticalPur)
        {
            SeuilPresence = seuilPresence;
            LongueurMin = longueurMin;
            LongueurMax = longueurMax;
            EpaisseurMin = epaisseurMin;
            EpaisseurMax = epaisseurMax;
            WanderMin = wanderMin;
            WanderMax = wanderMax;
            ChanceVerticalPur = chanceVerticalPur;
        }

        public float SeuilPresence { get; }
        public float LongueurMin { get; }
        public float LongueurMax { get; }
        public float EpaisseurMin { get; }
        public float EpaisseurMax { get; }
        public float WanderMin { get; }
        public float WanderMax { get; }
        public float ChanceVerticalPur { get; }
    }

    private static readonly ParametresFilonQuartz TierQuartzPrincipal = new(
        QuartzSeuilPresencePrincipal, 20f, 250f, 1f, 5f, 1.2f, 7f, 0.07f);
    private static readonly ParametresFilonQuartz TierQuartzMontagne = new(
        QuartzSeuilPresenceMontagne, 8f, 32f, 1f, 2f, 0.4f, 2.2f, 0.12f);

    private static void ObtenirPlageYLocalFilonsQuartz(
        int hauteurSurface, int chunkOffsetY, int hauteurMax,
        out int yMinLocal, out int yMaxLocal, out bool zoneMontagne)
    {
        int baseY = chunkOffsetY * hauteurMax;
        int globalYMaxChunk = baseY + hauteurMax;
        zoneMontagne = hauteurSurface >= QuartzSeuilHauteurMontagne;

        int globalYMin = QuartzYMinPrincipal;
        int globalYMax = QuartzYMaxPrincipal;
        if (zoneMontagne)
        {
            int yMaxMontagne = Mathf.Min(hauteurSurface - 4, globalYMaxChunk);
            if (yMaxMontagne > QuartzYMaxPrincipal)
                globalYMax = Mathf.Max(globalYMax, yMaxMontagne);
        }

        yMinLocal = Mathf.Clamp(globalYMin - baseY, 0, hauteurMax);
        yMaxLocal = Mathf.Clamp(globalYMax - baseY, 0, hauteurMax);
        if (yMinLocal > yMaxLocal)
        {
            yMinLocal = 0;
            yMaxLocal = -1;
        }
    }

    /// <summary>
    /// Longueur 20–250 m : distribution biaisée vers le court ; au-delà de ~120 m, seuil de rareté croissant.
    /// </summary>
    private static float ObtenirLongueurFilonQuartz(float seedC, float seedRareteLongueur, ParametresFilonQuartz tier)
    {
        float plage = tier.LongueurMax - tier.LongueurMin;
        float t = Mathf.Pow(Mathf.Clamp(seedC, 0f, 1f), 2.75f);
        float longueur = tier.LongueurMin + t * plage;

        if (longueur > 120f)
        {
            float ratio = (longueur - 120f) / Mathf.Max(1f, tier.LongueurMax - 120f);
            float seuil = 0.42f + ratio * ratio * 0.52f;
            if (seedRareteLongueur < seuil)
                longueur = tier.LongueurMin + seedRareteLongueur * Mathf.Min(100f, longueur - tier.LongueurMin);
        }

        return Mathf.Clamp(longueur, tier.LongueurMin, tier.LongueurMax);
    }

    private static bool EstDansFilonQuartzSerpentin(
        float xGlobal, float yGlobal, float zGlobal,
        int hauteurSurface,
        ParametresFilonQuartz tier,
        bool modeMontagne)
    {
        float cell = Mathf.Max(8f, QuartzCelluleAnchorsXz);
        int ax0 = Mathf.FloorToInt(xGlobal / cell);
        int az0 = Mathf.FloorToInt(zGlobal / cell);

        for (int dax = -1; dax <= 1; dax++)
        {
            for (int daz = -1; daz <= 1; daz++)
            {
                int ax = ax0 + dax;
                int az = az0 + daz;
                float presence = DeterministicRand(ax * 0.0043f + 19.7f, az * 0.0039f - 11.2f);
                if (presence < tier.SeuilPresence)
                    continue;

                int nbFilons = presence > 0.70f ? 3 : (presence > 0.50f ? 2 : 1);
                for (int vi = 0; vi < nbFilons; vi++)
                {
                    if (EstDansFilonQuartzSerpentinCellule(
                            xGlobal, yGlobal, zGlobal, hauteurSurface, ax, az, vi, tier, modeMontagne))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool EstDansFilonQuartzSerpentinCellule(
        float xGlobal, float yGlobal, float zGlobal,
        int hauteurSurface,
        int ax, int az, int indiceFilon,
        ParametresFilonQuartz tier,
        bool modeMontagne)
    {
        float cell = Mathf.Max(8f, QuartzCelluleAnchorsXz);
        float seedA = DeterministicRand(ax * 17.11f + indiceFilon * 3.7f, az * 13.07f - indiceFilon * 2.1f);
        float seedB = DeterministicRand(ax * 9.43f - indiceFilon, az * 11.29f + indiceFilon * 5.3f);
        float seedC = DeterministicRand(ax * 5.17f + indiceFilon * 7.9f, az * 6.83f - indiceFilon * 4.1f);

        float anchorX = ax * cell + cell * (0.22f + seedA * 0.56f);
        float anchorZ = az * cell + cell * (0.18f + seedB * 0.58f);
        float phase = seedA * 31.7f + seedB * 19.3f + indiceFilon * 11.1f;

        float longueur = ObtenirLongueurFilonQuartz(seedC, seedA, tier);
        float yStart;
        float yEnd;
        if (modeMontagne)
        {
            float bandeHaute = hauteurSurface - 4f;
            float bandeBasse = QuartzYMaxPrincipal + 2f;
            if (bandeHaute <= bandeBasse + 6f)
                return false;

            yEnd = bandeBasse + seedA * Mathf.Max(1f, bandeHaute - bandeBasse - 8f);
            yStart = yEnd - longueur;
            if (yStart <= bandeBasse)
                return false;
        }
        else
        {
            float plageY = QuartzYMaxPrincipal - QuartzYMinPrincipal - longueur;
            yStart = QuartzYMinPrincipal + seedB * Mathf.Max(1f, plageY);
            yEnd = yStart + longueur;
            if (yGlobal < QuartzYMinPrincipal || yGlobal > QuartzYMaxPrincipal)
                return false;
        }

        if (yGlobal < yStart - 0.5f || yGlobal > yEnd + 0.5f)
            return false;

        bool verticalPur = seedC < tier.ChanceVerticalPur;
        float wanderAmp = verticalPur
            ? 0.08f + seedA * 0.22f
            : tier.WanderMin + seedA * (tier.WanderMax - tier.WanderMin);

        // Inclinaison légère : quasi vertical, souvent en diagonale.
        float tiltX = (seedB - 0.5f) * (verticalPur ? 0.04f : 0.14f);
        float tiltZ = (seedC - 0.5f) * (verticalPur ? 0.04f : 0.14f);
        float relY = yGlobal - yStart;

        float serpentX = wanderAmp * (
            Mathf.Sin(yGlobal * 0.071f + phase) +
            0.42f * Mathf.Sin(yGlobal * 0.019f + phase * 1.63f));
        float serpentZ = wanderAmp * (
            Mathf.Cos(yGlobal * 0.067f + phase * 0.91f) +
            0.42f * Mathf.Cos(yGlobal * 0.023f + phase * 2.17f));

        float centreX = anchorX + serpentX + tiltX * relY;
        float centreZ = anchorZ + serpentZ + tiltZ * relY;

        float dx = xGlobal - centreX;
        float dz = zGlobal - centreZ;
        float distXz = Mathf.Sqrt(dx * dx + dz * dz);

        // Épaisseur non constante le long du filon (serpentin en largeur aussi).
        float ondulation = 0.5f + 0.5f * Mathf.Sin(yGlobal * 0.13f + phase * 0.47f);
        float bruitEpaisseur = DeterministicRand(
            anchorX * 0.31f + yGlobal * 0.17f + indiceFilon,
            anchorZ * 0.27f - yGlobal * 0.11f - indiceFilon);
        float rayon = 0.5f * (
            tier.EpaisseurMin +
            ondulation * bruitEpaisseur * (tier.EpaisseurMax - tier.EpaisseurMin));
        rayon = Mathf.Clamp(rayon, tier.EpaisseurMin * 0.5f, tier.EpaisseurMax * 0.5f);

        return distXz <= rayon;
    }

    /// <summary>Quartz (19) ou étain (37) dans un filon quartz — tirage déterministe par position monde.</summary>
    private static byte ObtenirMateriauDepuisFilonQuartz(float xGlobal, float yGlobal, float zGlobal)
    {
        float bruit = DeterministicRand(
            xGlobal * 0.0413f + 37.17f,
            zGlobal * 0.0371f - yGlobal * 0.0189f + 19.53f);
        return bruit < QuartzFractionEtainDansFilon ? IdMineraiEtain : IdMineraiQuartz;
    }

    /// <summary>
    /// Sauvegardes antérieures à l'activation des filons quartz : ré-injecte les veines dans la roche intacte (ID 2).
    /// </summary>
    public bool RetroAppliquerFilonsQuartzDepuisDisque()
    {
        if (!SpawnMineraiQuartz || _generationAbysseActive || _materials == null || _densities == null || _densitiesEau == null)
            return false;

        int baseY = ChunkOffsetY * HauteurMax;
        int globalYMax = baseY + HauteurMax;
        if (globalYMax < QuartzYMinPrincipal)
            return false;

        int taille = TailleChunk + 1;
        var hauteurColonne = new int[taille, taille];
        for (int x = 0; x < taille; x++)
        {
            for (int z = 0; z < taille; z++)
            {
                int xInt = ChunkOffsetX * TailleChunk + x;
                int zInt = ChunkOffsetZ * TailleChunk + z;
                hauteurColonne[x, z] = CalculerHauteurTerrain(xInt, zInt);
            }
        }

        int avant = CompterVoxelsMineraiQuartz();
        AppliquerFilonsQuartz(hauteurColonne);
        int apres = CompterVoxelsMineraiQuartz();
        if (apres > avant)
        {
            MarquerModifie();
            return true;
        }
        return false;
    }

    private int CompterVoxelsMineraiQuartz()
    {
        if (_materials == null)
            return 0;
        int compte = 0;
        int taille = TailleChunk + 1;
        for (int x = 0; x < taille; x++)
            for (int y = 0; y <= HauteurMax; y++)
                for (int z = 0; z < taille; z++)
                    if (_materials[x, y, z] == IdMineraiQuartz)
                        compte++;
        return compte;
    }

    private void AppliquerFilonsQuartz(int[,] hauteurColonne)
    {
        if (!SpawnMineraiQuartz || _materials == null || _densities == null || _densitiesEau == null)
            return;

        int taille = TailleChunk + 1;
        for (int x = 0; x < taille; x++)
        {
            for (int z = 0; z < taille; z++)
            {
                int hauteurSurface = hauteurColonne[x, z];
                ObtenirPlageYLocalFilonsQuartz(
                    hauteurSurface, ChunkOffsetY, HauteurMax,
                    out int yMinFilon, out int yMaxFilon, out bool zoneMontagne);
                if (yMaxFilon < yMinFilon)
                    continue;

                float xGlobal = ChunkOffsetX * TailleChunk + x;
                float zGlobal = ChunkOffsetZ * TailleChunk + z;

                for (int y = yMinFilon; y <= yMaxFilon; y++)
                {
                    if (_materials[x, y, z] != 2 || _densities[x, y, z] <= Isolevel || _densitiesEau[x, y, z] > Isolevel)
                        continue;

                    float globalY = ChunkOffsetY * HauteurMax + y;
                    bool modeMontagne = zoneMontagne && globalY > QuartzYMaxPrincipal;
                    ParametresFilonQuartz tier = modeMontagne ? TierQuartzMontagne : TierQuartzPrincipal;

                    if (EstDansFilonQuartzSerpentin(xGlobal, globalY, zGlobal, hauteurSurface, tier, modeMontagne))
                        _materials[x, y, z] = ObtenirMateriauDepuisFilonQuartz(xGlobal, globalY, zGlobal);
                }
            }
        }
    }

    private void AppliquerFilonsCharbon(
        int[,] hauteurColonne,
        float[,] temperatureColonne,
        float[,] humiditeColonne)
    {
        if (!SpawnMineraiCharbon || _materials == null || _densities == null || _densitiesEau == null)
            return;

        int taille = TailleChunk + 1;
        for (int x = 0; x < taille; x++)
        {
            for (int z = 0; z < taille; z++)
            {
                float xGlobal = ChunkOffsetX * TailleChunk + x;
                float zGlobal = ChunkOffsetZ * TailleChunk + z;
                int xInt = (int)xGlobal;
                int zInt = (int)zGlobal;
                int hauteurSurface = hauteurColonne[x, z];
                float temperature = temperatureColonne[x, z];
                float humidite = humiditeColonne[x, z];

                byte materiauSurface = DeterminerMateriauCroûte(
                    xInt, zInt, hauteurSurface, hauteurSurface, temperature, humidite);
                CategorieFilonsCharbon categorie = DeterminerCategorieFilonsCharbon(hauteurSurface, materiauSurface);
                ObtenirPlageYLocalFilonsCharbon(categorie, ChunkOffsetY, HauteurMax, out int yMinFilon, out int yMaxFilon);
                if (yMaxFilon < yMinFilon)
                    continue;

                for (int y = yMinFilon; y <= yMaxFilon; y++)
                {
                    if (_materials[x, y, z] != 2 || _densities[x, y, z] <= Isolevel || _densitiesEau[x, y, z] > Isolevel)
                        continue;

                    float globalY = ChunkOffsetY * HauteurMax + y;
                    if (EstDansFilonCharbonCategorie(xGlobal, zGlobal, globalY, categorie))
                        _materials[x, y, z] = IdMineraiCharbon;
                }
            }
        }
    }

    private void AppliquerVeinesMinerais(
        int[,] hauteurColonne,
        float[,] temperatureColonne,
        float[,] humiditeColonne)
    {
        if (!ActiverSystemeMinerais || _generationAbysseActive || _materials == null || _densities == null || _densitiesEau == null)
            return;

        if (SpawnMineraiCharbon)
            AppliquerFilonsCharbon(hauteurColonne, temperatureColonne, humiditeColonne);

        if (SpawnMineraiQuartz)
            AppliquerFilonsQuartz(hauteurColonne);

        bool auMoinsUnGeneriqueActif = false;
        for (int i = 0; i < ReglesMinerais.Length; i++)
        {
            if (ReglesMinerais[i].Actif)
            {
                auMoinsUnGeneriqueActif = true;
                break;
            }
        }
        if (!auMoinsUnGeneriqueActif)
            return;

        int taille = TailleChunk + 1;
        for (int x = 0; x < taille; x++)
        {
            for (int z = 0; z < taille; z++)
            {
                float xGlobal = ChunkOffsetX * TailleChunk + x;
                float zGlobal = ChunkOffsetZ * TailleChunk + z;
                int hauteurSurface = hauteurColonne[x, z];

                for (int y = 0; y <= HauteurMax; y++)
                {
                    if (_materials[x, y, z] != 2 || _densities[x, y, z] <= Isolevel || _densitiesEau[x, y, z] > Isolevel)
                        continue;

                    float globalYf = ChunkOffsetY * HauteurMax + y;
                    int profondeur = hauteurSurface - (int)globalYf;
                    if (profondeur < 0)
                        continue;

                    for (int i = 0; i < ReglesMinerais.Length; i++)
                    {
                        RegleMinerai regle = ReglesMinerais[i];
                        if (!regle.Actif)
                            continue;
                        if (profondeur < regle.ProfondeurMin || profondeur > regle.ProfondeurMax)
                            continue;

                        float bruitVeine = DeterministicRand(
                            xGlobal * regle.Frequence + globalYf * (regle.Frequence * 7.1f) + regle.Id * 17.3f,
                            zGlobal * regle.Frequence + globalYf * (regle.Frequence * 5.7f) - regle.Id * 11.9f);

                        if (bruitVeine >= regle.Seuil)
                        {
                            _materials[x, y, z] = regle.Id;
                            break;
                        }
                    }
                }
            }
        }
    }
}
