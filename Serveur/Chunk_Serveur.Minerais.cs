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
    private const bool SpawnMineraiQuartz = false;
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
    private const int SeuilHauteurMontagneCharbon = 150;
    private const int YMinFilonsMontagneCharbon = 120;

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
        new(19, SpawnMineraiQuartz, 6, 65, 0.018f, 0.79f),
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
        new(37, SpawnMineraiEtain, 8, 85, 0.020f, 0.86f),
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
