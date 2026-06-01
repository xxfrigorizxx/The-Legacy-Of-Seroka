using Godot;

public partial class Chunk_Serveur
{
    // Switch maître: garde le comportement actuel tant qu'il est à false.
    private const bool ActiverSystemeMinerais = false;

    // Switches individuels: passe un minerai à true quand tu veux le faire apparaître.
    private const bool SpawnMineraiCharbon = false;
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
        new(10, SpawnMineraiCharbon, 4, 70, 0.021f, 0.80f),
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

    private void AppliquerVeinesMinerais(int[,] hauteurColonne)
    {
        if (!ActiverSystemeMinerais || _generationAbysseActive || _materials == null || _densities == null || _densitiesEau == null)
            return;

        bool auMoinsUnActif = false;
        for (int i = 0; i < ReglesMinerais.Length; i++)
        {
            if (ReglesMinerais[i].Actif)
            {
                auMoinsUnActif = true;
                break;
            }
        }
        if (!auMoinsUnActif)
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
