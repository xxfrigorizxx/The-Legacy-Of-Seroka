using Godot;
using System;
using System.Collections.Generic;

    /// <summary>Slot d'inventaire avec ADN morphologique (forme) et chimique (composition).</summary>
    public struct SlotInventaire
    {
        public int ID;
        public int IndexMorphologique;
        /// <summary>Roche : indice minÃ©ral. BÃ¢ton (32) : 0 = branche brute, 1 = bÃ¢ton de chÃªne faÃ§onnÃ© (craft, teinte plus pÃ¢le).</summary>
        public int IndexChimique;
    /// <summary>Grosseur roche matiÃ¨re (40â€“49) : 0=Mini, 1=Petite, 2=Moyenne, 3=Grosse, 4=Ã‰norme.</summary>
    public int IndexTaille;
    /// <summary>True si le slot contient un Ã©clat de fracture (mesh dynamique, pas dans le cache).</summary>
    public bool EstUnEclat;
    /// <summary>Mesh sauvegardÃ© pour les Ã©clats (sinon null).</summary>
    public Mesh MeshEclat;
    /// <summary>Nombre de fractures subies (0 = intact). ConservÃ© au ramassage/lancer pour poudre au-delÃ  de 5.</summary>
    public int NiveauFracture;
    /// <summary>Ã‰chelle de l'Ã©clat au ramassage (Ã©vite qu'il grossisse au relancer).</summary>
    public Vector3 ScaleEclat;
    /// <summary>Essence de bois (0 = chÃªne pour l'instant). UtilisÃ© pour ID 30 (bÃ»che) et 32 (bÃ¢ton). En prÃ©vision des futurs arbres.</summary>
    public byte IndexBotanique;
    /// <summary>IdentitÃ© dâ€™assemblage CAO (sÃ©quence piÃ¨ces / poses). Vide si non forgÃ© ou hÃ©ritage sans donnÃ©e.</summary>
    public string GenomeAssemblage;
    /// <summary>Dague primitive (105) : durabilitÃ© max (lame minÃ©rale + manche corde). 0 = non initialisÃ©.</summary>
    public float DurabiliteOutilMax;
    /// <summary>Dague primitive : points restants. Ã€ 0 lâ€™arme est cassÃ©e.</summary>
    public float DurabiliteOutilActuelle;
    /// <summary>Dague 105 : taille de la roche en pointe (0â€“4) utilisÃ©e au craft â€” Ã©chelle visuelle de la lame. DÃ©faut 2 si absent.</summary>
    public int IndexTailleLameRoche;
    /// <summary>QuantitÃ© stackÃ©e dans le slot (base 1).</summary>
    public int Quantite;
    /// <summary>ClÃ© de conteneur persistante (ex: sac tier 0) pour mÃ©moriser son contenu mÃªme dÃ©sÃ©quipÃ©.</summary>
    public string CleConteneur;

    public SlotInventaire()
    {
        ID = 0;
        IndexMorphologique = 0;
        IndexChimique = 0;
        IndexTaille = 2;
        EstUnEclat = false;
        MeshEclat = null;
        NiveauFracture = 0;
        ScaleEclat = Vector3.One;
        IndexBotanique = LSystem_Botanique.IndexChene;
        GenomeAssemblage = "";
        DurabiliteOutilMax = 0f;
        DurabiliteOutilActuelle = 0f;
        IndexTailleLameRoche = 2;
        Quantite = 0;
        CleConteneur = "";
    }

    public bool EstVide => ID == 0;
}

public partial class Joueur : CharacterBody3D
{
    public readonly struct SectionSanteCorps
    {
        public readonly string Cle;
        public readonly string Nom;
        public readonly string Matiere;
        public readonly string Os;
        public readonly float PointsVie;
        public readonly float PointsVieMax;
        public readonly float PointsVieMaxBrut;
        public readonly float PointsVieBrulureBloquee;
        public readonly float IntegriteOs;
        public readonly float IntegriteOsMax;

        public SectionSanteCorps(
            string cle,
            string nom,
            string matiere,
            string os,
            float pointsVie,
            float pointsVieMax,
            float pointsVieMaxBrut,
            float pointsVieBrulureBloquee,
            float integriteOs,
            float integriteOsMax)
        {
            Cle = cle;
            Nom = nom;
            Matiere = matiere;
            Os = os;
            PointsVie = pointsVie;
            PointsVieMax = pointsVieMax;
            PointsVieMaxBrut = pointsVieMaxBrut;
            PointsVieBrulureBloquee = pointsVieBrulureBloquee;
            IntegriteOs = integriteOs;
            IntegriteOsMax = integriteOsMax;
        }
    }

    public const ulong NiveauMaxFutureState = 10_000_000_000_000_000_000UL;
    public const float CapacitePoidsBaseHumainKg = 20.0f;
    private static readonly UInt128 XpHybrideCoefLineaire = (UInt128)10;
    private static readonly UInt128 XpHybrideDivQuadratique = (UInt128)1000;
    /// <summary>MÃ©ta et slots : mÃªme clÃ© pour lâ€™Ã©tabli CAO et les corps posÃ©s.</summary>
    public const string MetaGenomeAssemblage = "GenomeAssemblage";
    /// <summary>Prévisualisation main : cuir bovin (variante texture/genome).</summary>
    public const string MetaSignatureLootCuir117 = "SigLootCuir117";
    /// <summary>Prévisualisation GLB simple (steak, charbon, os…) — signature = ID objet.</summary>
    public const string MetaSignatureGlbLootSimple = "SigGlbLootSimple";
    /// <summary>ItemPhysique dague (105) : durabilitÃ© synchronisÃ©e inventaire / sol.</summary>
    public const string MetaDurabiliteOutilMax = "DurOutilMax";
    public const string MetaDurabiliteOutilActuelle = "DurOutilAct";
    public const string MetaTailleLameRoche = "TailleLameRoche";
    /// <summary>Sac tier 0 Ã©quipable (slot dos) : 1 case de stockage persistante.</summary>
    public const int IdObjetSacTier0 = 101;
    /// <summary>Tag botanique rÃ©servÃ© aux variantes liane (pochette/sac) pour les rÃ¨gles gameplay.</summary>
    public const byte TagVarianteLiane = 16;
    /// <summary>Tag botanique rÃ©servÃ© aux variantes corde d'herbe solide (pochette/sac/ceinture).</summary>
    public const byte TagVarianteHerbeSolide = 17;
    /// <summary>Tag réservé à la filière intestin (corde/tissu/pochette/sac/ceinture).</summary>
    public const byte TagVarianteIntestin = 18;
    /// <summary>Tag réservé à la filière intestin solide (slots identiques, pile x2).</summary>
    public const byte TagVarianteIntestinSolide = 19;
    /// <summary>Tag corde / bandage mixte : intestin nettoyé + fibre (herbe, liane ou boyau).</summary>
    public const byte TagVarianteCordeIntestinMixe = 20;
    /// <summary>Alias historique (= <see cref="IdObjetSacTier0"/>).</summary>
    public const int IdObjetSacDos = 101;
    /// <summary>Ceinture tissÃ©e (102) : slot corps uniquement, sans stockage.</summary>
    public const int IdObjetCeinturePoches = 102;
    /// <summary>Pochette tier 0 (matÃ©riau) : craft atelier, mÃªme rendu corde/tissu que la ceinture.</summary>
    public const int IdObjetPochetteTier0 = 103;
    /// <summary>Ceinture Ã  sacoches (104) : slot corps ; 4 cases de stockage persistantes.</summary>
    public const int IdObjetCeintureSacoches = 104;
    /// <summary>Pelle en pierre tier 0.</summary>
    public const int IdObjetPellePierreTier0 = 107;
    /// <summary>Pioche en pierre tier 0.</summary>
    public const int IdObjetPiochePierreTier0 = 108;
    /// <summary>Lance en pierre tier 0 (arme d'attaque/lancer uniquement).</summary>
    public const int IdObjetLancePierreTier0 = 111;
    /// <summary>Faux primitive en pierre (visuel épée tier0 + craft établi 3×3).</summary>
    public const int IdObjetFauxPierreTier0 = 112;
    /// <summary>Coffre en bois tier 0 (10 slots stockage, craft 3×3).</summary>
    public const int IdObjetCoffreBoisTier0 = 113;
    /// <summary>Carnet du savoir dédié (slot UI exclusif + pages éditables).</summary>
    public const int IdObjetCarnetSavoir = 114;
    /// <summary>Steak cru (loot dépeçage bovin).</summary>
    public const int IdObjetSteakCru = 115;
    /// <summary>Steak cuit (obtenu par cuisson au pit roche).</summary>
    public const int IdObjetSteakCuit = 123;
    /// <summary>Os (loot dépeçage bovin).</summary>
    public const int IdObjetOsBoeuf = 116;
    /// <summary>Cuir de bœuf (loot dépeçage ; texture via <see cref="SlotInventaire.GenomeAssemblage"/> préfixe PEAU:).</summary>
    public const int IdObjetCuirBoeuf = 117;
    /// <summary>Intestin de bœuf (loot dépeçage).</summary>
    public const int IdObjetIntestinBoeuf = 118;
    /// <summary>Intestin de bœuf nettoyé (obtenu par immersion dans l'eau).</summary>
    public const int IdObjetIntestinBoeufNettoye = 119;
    /// <summary>Pit à feu (structure bois posable, sans allumage dans cette étape).</summary>
    public const int IdObjetPitFeu = 120;
    /// <summary>Allume-feu préhistorique (silex + marcassite/pyrite).</summary>
    public const int IdObjetAllumeFeu = 121;
    /// <summary>Pit à feu roche (pit à feu renforcé avec couronne de roches).</summary>
    public const int IdObjetPitFeuRoche = 122;
    /// <summary>Fondation bois (demi-bûches standard en 3x3).</summary>
    public const int IdObjetFondationBois = 124;
    /// <summary>Fondation roche (3x3 roches matière homogènes).</summary>
    public const int IdObjetFondationRoche = 125;
    /// <summary>Fondation mixte : base bois, sole roche.</summary>
    public const int IdObjetFondationBoisSoleRoche = 126;
    /// <summary>Fondation mixte : base roche, sole bois.</summary>
    public const int IdObjetFondationRocheSoleBois = 127;
    /// <summary>Plancher bois (3 demi-bûches standard côte à côte) posé sur une fondation.</summary>
    public const int IdObjetSolBois = 136;
    /// <summary>Plancher roche (3 roches moyennes côte à côte, même type) posé sur une fondation.</summary>
    public const int IdObjetSolRoche = 137;
    /// <summary>Muret bois (4 m de long, 1 m de haut) à fixer sur le côté des fondations.</summary>
    public const int IdObjetMuretBois = 138;
    /// <summary>Muret pierre (4 m de long, 1 m de haut) à fixer sur le côté des fondations.</summary>
    public const int IdObjetMuretPierre = 139;
    /// <summary>Mur bois (4 m de large, 3 m de haut) à poser sur les murets.</summary>
    public const int IdObjetMurBois = 140;
    /// <summary>Mur bois avec fenêtre (4 m de large, 3 m de haut), double essence (mur + fenêtre).</summary>
    public const int IdObjetMurBoisFenetre = 141;
    /// <summary>Mur bois cadre de porte (4 m de large, 3 m de haut), essence unique.</summary>
    public const int IdObjetMurBoisCadrePorte = 142;
    /// <summary>Porte bois (ouvrable/fermable avec E), à poser dans un mur cadre de porte.</summary>
    public const int IdObjetPorteBois = 143;
    /// <summary>Toit chaume modulaire (solo/long/L/carré selon voisins), variante via liage.</summary>
    public const int IdObjetToitChaume = 144;
    /// <summary>Torche posable (sol/mur), allumable à l'allume-feu.</summary>
    public const int IdObjetTorche = 145;
    /// <summary>Fenêtre bois craftable (composant), base pour mur fenêtré.</summary>
    public const int IdObjetFenetreBois = 146;
    /// <summary>Table en bois décorative (meuble posé, non-artisanal).</summary>
    public const int IdObjetTableBoisDecorative = 147;
    /// <summary>Table artisanat structures tier 1 (station dédiée structures).</summary>
    public const int IdObjetTableArtisanaTier1 = 148;
    /// <summary>Aloe vera récoltable/replantable (objet dédié, mesh procédural spécifique).</summary>
    public const int IdObjetAloeVera = 149;
    /// <summary>Morceau de charbon — basse qualité (Y proche surface).</summary>
    public const int IdObjetCharbonBasseQualite = 150;
    /// <summary>Morceau de charbon — qualité moyenne.</summary>
    public const int IdObjetCharbonMoyenneQualite = 151;
    /// <summary>Morceau de charbon — bonne qualité.</summary>
    public const int IdObjetCharbonBonneQualite = 152;
    /// <summary>Morceau de charbon — anthracite (profondeur, léger reflet).</summary>
    public const int IdObjetCharbonAntracite = 153;
    /// <summary>Bol en bois rempli d'eau (obtenu en remplissant un bol vide dans l'eau). Garde l'essence du bois.</summary>
    public const int IdObjetBolEau = 154;
    /// <summary>Argile humidifiée (craft : bol d'eau + voxel argile). Matériau intermédiaire, stack 20.</summary>
    public const int IdObjetArgileHumidifiee = 155;
    /// <summary>Torchie (craft : argile humidifiée + brin d'herbe + voxel boue). Matériau intermédiaire, stack 24.</summary>
    public const int IdObjetTorchie = 156;
    /// <summary>Four en torchie (craft 3×3 torchies, posable au sol direct uniquement). Stack 1.</summary>
    public const int IdObjetFourTorchie = 157;
    /// <summary>Bol en argile (craft établi 3×3, argile humidifiée). Matériau intermédiaire, stack 10.</summary>
    public const int IdObjetBolArgile = 158;
    /// <summary>Bol en céramique (four 500–700 °C, 2 min 30). Chaud après cuisson : refroidir 1 min au soleil.</summary>
    public const int IdObjetBolCeramique = 159;
    /// <summary>Pince en os vide (craft établi 3×3, 4 os). Outil, stack 1.</summary>
    public const int IdObjetPinceOs = 160;
    /// <summary>Moule modelé en argile (à cuire au four).</summary>
    public const int IdObjetMouleArgile = 161;
    /// <summary>Moule en céramique (chaud après cuisson, refroidissement au soleil).</summary>
    public const int IdObjetMouleCeramique = 162;
    /// <summary>Chamotte — céramique sur-cuite (échec four argile trop chaud).</summary>
    public const int IdObjetChamotte = 163;
    /// <summary>Morceau de quartz miné (drop ~90 % du voxel 19).</summary>
    public const int IdObjetQuartz = 164;
    /// <summary>Quartz pur — variante rare (~10 % du voxel 19).</summary>
    public const int IdObjetQuartzPur = 165;
    /// <summary>Morceau de minerai d'étain miné (drop du voxel 37).</summary>
    public const int IdObjetEtain = 166;
    /// <summary>Taille monde du four posé au sol (plus grande dimension du mesh normalisé).</summary>
    public const float TailleFourTorchiePoseMetres = 3.0f;
    /// <summary>Incrémenter pour régénérer texture / réinstancier les modèles torchie (GLB sans UVs).</summary>
    public const int RevisionRenduTorchie = 4;
    /// <summary>Incrémenter après changement shader/textures quartz miné.</summary>
    public const int RevisionRenduQuartz = 2;
    /// <summary>Incrémenter après changement texture/matériau étain miné.</summary>
    public const int RevisionRenduEtain = 1;

    public static bool EstIdCharbonRecolte(int id) =>
        id == IdObjetCharbonBasseQualite
        || id == IdObjetCharbonMoyenneQualite
        || id == IdObjetCharbonBonneQualite
        || id == IdObjetCharbonAntracite;

    public static bool EstIdQuartzRecolte(int id) =>
        id == IdObjetQuartz || id == IdObjetQuartzPur;

    public static bool EstIdEtainRecolte(int id) => id == IdObjetEtain;

    private const int SeuilHauteurMontagneCharbonRecolte = 150;
    private const int IdVoxelMineraiCharbon = 10;
    private const int IdVoxelMineraiQuartz = 19;
    private const int IdVoxelMineraiEtain = 37;

    /// <summary>Loot charbon miné (voxel 10) selon Y monde et biome montagne (aligné filons serveur).</summary>
    public static int ObtenirIdObjetCharbonRecolteDepuisPositionMonde(Vector3 positionMonde, int seedTerrain)
    {
        int gx = Mathf.FloorToInt(positionMonde.X);
        int gy = Mathf.FloorToInt(positionMonde.Y);
        int gz = Mathf.FloorToInt(positionMonde.Z);
        int hSurf = Generateur_Voxel.ObtenirHauteurTerrainMonde(gx, gz, seedTerrain);
        if (hSurf >= SeuilHauteurMontagneCharbonRecolte)
        {
            float r = RandDeterministeUnitaireCharbon(gx, gy, gz, seedTerrain);
            return r < 0.05f ? IdObjetCharbonAntracite : IdObjetCharbonBonneQualite;
        }
        if (gy <= 0)
            return IdObjetCharbonAntracite;
        if (gy <= 30)
            return IdObjetCharbonBonneQualite;
        if (gy <= 49)
            return IdObjetCharbonMoyenneQualite;
        if (gy <= 95)
            return IdObjetCharbonBasseQualite;
        return IdObjetCharbonBasseQualite;
    }

    /// <summary>Loot quartz miné (voxel 19) : ~90 % quartz, ~10 % quartz pur (déterministe par position).</summary>
    public static int ObtenirIdObjetQuartzRecolteDepuisPositionMonde(Vector3 positionMonde, int seedTerrain)
    {
        int gx = Mathf.FloorToInt(positionMonde.X);
        int gy = Mathf.FloorToInt(positionMonde.Y);
        int gz = Mathf.FloorToInt(positionMonde.Z);
        float r = RandDeterministeUnitaireCharbon(gx, gy, gz, seedTerrain ^ 0x51A7C919);
        return r < 0.10f ? IdObjetQuartzPur : IdObjetQuartz;
    }

    public static SlotInventaire ConstruireSlotLootMineraiVoxel(int idVoxelMinerai, Vector3 positionMonde, int seedTerrain)
    {
        if (idVoxelMinerai == IdVoxelMineraiCharbon)
        {
            return new SlotInventaire
            {
                ID = ObtenirIdObjetCharbonRecolteDepuisPositionMonde(positionMonde, seedTerrain),
                Quantite = 1
            };
        }
        if (idVoxelMinerai == IdVoxelMineraiQuartz)
        {
            return new SlotInventaire
            {
                ID = ObtenirIdObjetQuartzRecolteDepuisPositionMonde(positionMonde, seedTerrain),
                Quantite = 1
            };
        }
        if (idVoxelMinerai == IdVoxelMineraiEtain)
        {
            return new SlotInventaire
            {
                ID = IdObjetEtain,
                Quantite = 1
            };
        }
        return new SlotInventaire
        {
            ID = 2,
            GenomeAssemblage = $"VOXEL_TERRAIN:{idVoxelMinerai}",
            Quantite = 1
        };
    }

    private static float RandDeterministeUnitaireCharbon(int x, int y, int z, int seed)
    {
        uint h = (uint)(seed ^ (x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
        h ^= h >> 16;
        h *= 0x7feb352d;
        h ^= h >> 15;
        h *= 0x846ca68b;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
    /// <summary>Emprise horizontale des planchers posés (carré X×Z, léger débord sur fondation 4 m).</summary>
    public const float PlancherEmpriseMetres = 4.1f;
    /// <summary>Épaisseur des planchers bois / roche.</summary>
    public const float PlancherEpaisseurMetres = 0.08f;
    /// <summary>Maillet / pilon en bois (établi 3×3 : rondin court fendu en 8).</summary>
    public const int IdObjetMailletBois = 128;
    /// <summary>Bol en bois (établi 3×3 : bûche la plus courte + dague pour sculpter).</summary>
    public const int IdObjetBolBois = 129;
    /// <summary>Mortier avec pilon (assemblage bol + pilon, essences séparées par mesh).</summary>
    public const int IdObjetMortierPilonBois = 130;
    /// <summary>Table d'analyse tier 1 (station avancée, 8 slots analyse).</summary>
    public const int IdObjetTableAnalyseTier1 = 131;
    /// <summary>Hache en pierre (déblocage analyse tier 1).</summary>
    public const int IdObjetHachePierreTier1 = 132;
    /// <summary>Atelle de jambe (artisanat de base).</summary>
    public const int IdObjetAtelleJambe = 133;
    /// <summary>Atelle de bras (artisanat de base).</summary>
    public const int IdObjetAtelleBras = 134;
    /// <summary>Bandage tier 1 (ligatures, texture héritée du liage crafté).</summary>
    public const int IdObjetBandageTier1 = 135;
    /// <summary>Objet posé au sol : quantité dans l’inventaire au ramassage (>1).</summary>
    public const string MetaQuantiteObjetPose = "QuantiteObjetPose";
    /// <summary>Rack Ã  bÃ¢tons (stockage dÃ©diÃ©).</summary>
    public const int IdObjetRackBatons = 109;
    /// <summary>Rack Ã  bÃ»ches (stockage dÃ©diÃ©).</summary>
    public const int IdObjetRackBuches = 110;
    /// <summary>Petite baie rÃ©coltable sur buisson (palette couleur via IndexChimique).</summary>
    public const int IdObjetBaie = 35;
    /// <summary>Nombre de teintes de baie (IndexChimique valide : 0 inclus à <see cref="BaieNombreCouleurs"/> exclus).</summary>
    public const int BaieNombreCouleurs = 9;

    /// <summary>Index couleur baie (0–8) pour l’inventaire et le rendu.</summary>
    public static int ClampIndexCouleurBaie(int indexChimique)
    {
        if (indexChimique < 0) return 0;
        if (indexChimique >= BaieNombreCouleurs) return BaieNombreCouleurs - 1;
        return indexChimique;
    }

    /// <summary>Teinte affichée (mesh, inventaire) : 8 = cyan APISARA ; les autres variantes bouclent sur 0..7 comme l’ancien <c>% 8</c>.</summary>
    public static int IndexCouleurBaieDepuisVariante(byte variante)
    {
        if (variante == 8) return 8;
        return ClampIndexCouleurBaie(variante % 8);
    }

    private static bool EstIdPitFeu(int id) => id == IdObjetPitFeu || id == IdObjetPitFeuRoche;
    private static bool EstIdFourTorchie(int id) => id == IdObjetFourTorchie;
    private static bool EstIdFondation(int id) => id == IdObjetFondationBois
        || id == IdObjetFondationRoche
        || id == IdObjetFondationBoisSoleRoche
        || id == IdObjetFondationRocheSoleBois;

    private static bool EstIdSolBois(int id) => id == IdObjetSolBois;
    private static bool EstIdSolRoche(int id) => id == IdObjetSolRoche;
    private static bool EstIdPlancher(int id) => EstIdSolBois(id) || EstIdSolRoche(id);
    private static bool EstIdMuretBois(int id) => id == IdObjetMuretBois;
    private static bool EstIdMuretPierre(int id) => id == IdObjetMuretPierre;
    private static bool EstIdMuret(int id) => EstIdMuretBois(id) || EstIdMuretPierre(id);
    private static bool EstIdMurBoisSimple(int id) => id == IdObjetMurBois;
    private static bool EstIdMurBoisFenetre(int id) => id == IdObjetMurBoisFenetre;
    private static bool EstIdMurBoisCadrePorte(int id) => id == IdObjetMurBoisCadrePorte;
    private static bool EstIdMurBois(int id) => EstIdMurBoisSimple(id) || EstIdMurBoisFenetre(id) || EstIdMurBoisCadrePorte(id);
    private static bool EstIdPorteBois(int id) => id == IdObjetPorteBois;
    private static bool EstIdToitChaume(int id) => id == IdObjetToitChaume;
    private static bool EstIdTorche(int id) => id == IdObjetTorche;
    private static bool EstIdTableBoisDecorative(int id) => id == IdObjetTableBoisDecorative;
    private static bool EstIdTableArtisanaTier1(int id) => id == IdObjetTableArtisanaTier1;

    /// <summary>Albedo procédural (main, sol, GLB teinté).</summary>
    public static Color ObtenirCouleurAlbedoBaie(int indexChimique)
    {
        return ClampIndexCouleurBaie(indexChimique) switch
        {
            1 => new Color(0.82f, 0.24f, 0.64f),
            2 => new Color(0.95f, 0.62f, 0.12f),
            3 => new Color(0.18f, 0.38f, 0.92f),
            4 => new Color(0.98f, 0.88f, 0.12f),
            5 => new Color(0.22f, 0.72f, 0.28f),
            6 => new Color(0.18f, 0.08f, 0.22f),
            7 => new Color(0.98f, 0.45f, 0.72f),
            8 => new Color(0.15f, 0.95f, 0.98f),
            _ => new Color(0.90f, 0.14f, 0.14f),
        };
    }

    /// <summary>Lexème féminin singulier pour « Petite baie … » ; pluriel « Petites baies …s » via suffixe s (oranges, roses, etc.).</summary>
    public static string ObtenirLexemeCouleurBaiePourNomInventaire(int indexChimique)
    {
        return ClampIndexCouleurBaie(indexChimique) switch
        {
            1 => "violette",
            2 => "orange",
            3 => "bleue",
            4 => "jaune",
            5 => "verte",
            6 => "noire",
            7 => "rose",
            8 => "cyan fluorescente",
            _ => "rouge",
        };
    }

    /// <summary>Accord avec « baie(s) » au ramassage (ex. bleue / bleues).</summary>
    public static string ObtenirAdjectifBaieAccorde(int indexChimique, bool pluriel)
    {
        return ClampIndexCouleurBaie(indexChimique) switch
        {
            1 => pluriel ? "violettes" : "violette",
            2 => pluriel ? "oranges" : "orange",
            3 => pluriel ? "bleues" : "bleue",
            4 => pluriel ? "jaunes" : "jaune",
            5 => pluriel ? "vertes" : "verte",
            6 => pluriel ? "noires" : "noire",
            7 => pluriel ? "roses" : "rose",
            8 => pluriel ? "cyan fluorescentes" : "cyan fluorescente",
            _ => pluriel ? "rouges" : "rouge",
        };
    }

    /// <summary>True si cet ID est un contenant portÃ© qui ouvre la grille Â« sac Â» dans lâ€™UI.</summary>
    public static bool EstObjetQuiDebloqueGrilleSac(int id) => id == IdObjetSacDos;

    /// <summary>Stats des outils forgÃ©s (CAO) : clÃ© = <see cref="HashGenomeStable"/> du genome si prÃ©sent, sinon GetHashCode du mesh (hÃ©ritage).</summary>
    public struct StatsOutilForge
    {
        public float Masse;
        public float EpaisseurLameBase;
        public Vector3 AxeTranchantLocal;
        public string Nom;
    }

    public static Dictionary<int, StatsOutilForge> RegistreOutilsForges = new Dictionary<int, StatsOutilForge>();

    /// <summary>ClÃ© dÃ©terministe pour <see cref="RegistreOutilsForges"/> (mÃªme chaÃ®ne â†’ mÃªme int, toute session).</summary>
    public static int HashGenomeStable(string genome)
    {
        if (string.IsNullOrEmpty(genome)) return 0;
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in genome)
            {
                h ^= c;
                h *= 16777619u;
            }
            int r = (int)h;
            return r == 0 ? 1 : r;
        }
    }

    /// <summary>ClÃ© registre outil forgÃ© : genome prioritaire, sinon mesh en mÃ©moire.</summary>
    public static int ClefRegistreOutilForge(SlotInventaire mainActive)
    {
        if (mainActive.ID != 100 || !mainActive.EstUnEclat || mainActive.MeshEclat == null) return 0;
        if (!string.IsNullOrEmpty(mainActive.GenomeAssemblage))
            return HashGenomeStable(mainActive.GenomeAssemblage);
        return mainActive.MeshEclat.GetHashCode();
    }

    public enum TypeMouvementFrappe { Estoc, DeHautEnBas, DeBasEnHaut, GaucheADroite, DroiteAGauche }

    public const float Speed = 2.5f;
    public const float JumpVelocity = 5.15f;
    public const float MasseCorporelleBaseHumainKg = 95.0f;
    public const float MasseCorporelleBaseOrcKg = 110.0f;

    // SensibilitÃ© chirurgicale de la souris
    public const float MouseSensitivity = 0.003f;
    /// <summary>Offset pitch souris (rad) : limite haute rÃ©aliste (Ã©vite de regarder "derriÃ¨re" en levant).</summary>
    private const float PitchSourisMaxDeg = 82f;
    /// <summary>Offset pitch souris (rad) : autorise Ã  baisser la tÃªte vers le sol en FPS.</summary>
    private const float PitchSourisMinDeg = -72f;

    /// <summary>Rayon du pinceau de sculpture (minage ET pose). SymÃ©trie absolue.</summary>
    private const float RAYON_SCULPTURE = 1.0f;

    /// <summary>Rayon de fauchage de la flore (gazon) avant pose de lâ€™atelier primitif â€” mÃªme ordre dâ€™idÃ©e que la lame sur le sol.</summary>
    private const float RayonFauchagePoseAtelier200 = 2.75f;

    /// <summary>Mains avec ADN morphologique : la pierre conserve sa forme exacte.</summary>
    public SlotInventaire MainGauche = new SlotInventaire();
    public SlotInventaire MainDroite = new SlotInventaire();
    /// <summary>True = main gauche active, false = main droite.</summary>
    // FIX CRITIQUE : La main droite est la main dominante par dÃ©faut (logique humaine standard)
    public bool MainGaucheEstActive = false;

    /// <summary>Sac au dos Ã©quipÃ© (slot dÃ©diÃ©, pas les mains). Assigner via <see cref="AssignerEquipementSacDos"/>.</summary>
    public SlotInventaire EquipementSacDos = new SlotInventaire();
    /// <summary>Ceinture Ã  poches Ã©quipÃ©e.</summary>
    public SlotInventaire EquipementCeinture = new SlotInventaire();
    /// <summary>Carnet du savoir équipé dans son slot dédié (hors barre basse).</summary>
    public SlotInventaire EquipementCarnet = new SlotInventaire();

    /// <summary>Craft 2Ã—2 dans le menu inventaire (Q) â€” jamais mÃ©langÃ© avec la grille de lâ€™Ã©tabli posÃ©.</summary>
    public SlotInventaire[] GrilleCraftPoche = new SlotInventaire[4];
    /// <summary>Stockage sac (1 case). Le contenu vit dans l'objet sac via <see cref="CleConteneur"/>.</summary>
    public SlotInventaire[] GrilleSacStockage = new SlotInventaire[1];
    /// <summary>Stockage ceinture Ã  sacoches (104) : 4 cases, mÃªme dictionnaire de mÃ©moire que le sac.</summary>
    public SlotInventaire[] GrilleCeintureStockage = new SlotInventaire[4];
    private readonly Dictionary<string, SlotInventaire[]> _memoireStockageSacs = new Dictionary<string, SlotInventaire[]>();

    /// <summary>Atelier (ItemPhysique 200) dont le plan 3Ã—3 est affichÃ© ; null en mode poche.</summary>
    public ItemPhysique AtelierPlanTravailOuvert;
    /// <summary>Rack Ã  bÃ¢tons (ItemPhysique 109) dont le stockage 3Ã—3 est affichÃ© ; null hors mode rack.</summary>
    public ItemPhysique RackBatonsOuvert;
    /// <summary>Coffre en bois (113) ouvert : stockage 10 cases dans le menu Q.</summary>
    public ItemPhysique CoffreOuvert;
    /// <summary>Four en torchie (157) ouvert : interface cuisson dans le menu Q.</summary>
    public ItemPhysique FourTorchieOuvert;

    /// <summary>True si le menu a Ã©tÃ© ouvert depuis lâ€™atelier posÃ© : recettes et UI en 3Ã—3. False aprÃ¨s Q ou fermeture du menu.</summary>
    public bool CraftGrille3x3AuTable { get; set; }
    /// <summary>ID de la station de craft 3×3 ouverte (0 = craft poche / aucune station).</summary>
    public int IdStationCraftOuverte { get; set; }
    /// <summary>True si la grille 3Ã—3 sert de stockage rack bÃ¢tons (pas de recettes).</summary>
    public bool StockageRackBatonsOuvert { get; set; }
    /// <summary>True si le panneau stockage 10 slots du coffre en bois est actif.</summary>
    public bool StockageCoffreOuvert { get; set; }
    /// <summary>True si le panneau four en torchie (combustible / cuisson / rÃ©sultats) est actif.</summary>
    public bool StockageFourTorchieOuvert { get; set; }

    /// <summary>Slot contenant le rÃ©sultat d'une recette valide.</summary>
    public SlotInventaire SlotResultatCraft = new SlotInventaire();

    private Camera3D _camera;
    private RayCast3D _rayon;
    private Camera3D _cameraFps;
    private Camera3D _cameraTps;
    private Vector3 _positionLocaleBaseCameraFps = new Vector3(0f, 0.56f, -0.07f);
    private RayCast3D _rayonFps;
    private RayCast3D _rayonTps;
    /// <summary>Limite les appels à <see cref="MenuAnatomie.RafraichirMenu"/> depuis le HUD (évite coût SubViewport par frame).</summary>
    private ulong _msDernierRafraichirMenuCompletHud;
    private Node3D _pivotCameraTps;
    private SpringArm3D _brasCameraTps;
    private bool _vueTroisiemePersonne;
    /// <summary>Pitch relatif (rad) autour de lâ€™axe X local de la camÃ©ra, clampÃ© ; ajoutÃ© Ã  <see cref="_pitchCameraBaseRad"/>.</summary>
    private float _pitchCamera;
    /// <summary>Pitch absolu de rÃ©fÃ©rence (rad) sur X : 0 sous CharacterBody, âˆ’Ï€/2 sur BoneAttachment tÃªte/cou (vue âˆ’Z).</summary>
    private float _pitchCameraBaseRad;
    /// <summary>Sur lâ€™os cou/tÃªte Mixamo la camÃ©ra peut viser lâ€™arriÃ¨re du crÃ¢ne : rotation Y locale Ï€ pour regarder devant.</summary>
    private float _yawCorrectionCameraFpsRad;
    private Node3D _rigHumain;
    private Vector3 _positionRigHumainVisible = Vector3.Zero;
    private bool _positionRigHumainVisibleInitialisee;
    private AnimationPlayer _animationHumain;
    private Skeleton3D _squeletteHumain;
    private int _osBrasDroit = -1;
    private int _osAvantBrasDroit = -1;
    private int _osMainDroite = -1;
    private int _osEpauleDroite = -1;
    private SkeletonIK3D _ikBrasDroitFps;
    private Marker3D _aimantIkMainDroite;
    private float _ikBlendMainDroite;
    private float _impulsionIkFrappePoids;
    private Vector3 _impulsionIkFrappeLocal;
    private BoneAttachment3D _attacheMainDroiteTps;
    private BoneAttachment3D _attacheMainGaucheTps;
    private BoneAttachment3D _attacheCameraFps;
    private BoneAttachment3D _attacheCeintureCorps;
    private BoneAttachment3D _attacheDosCorps;
    private Node3D _supportVisuelCeinture;
    private Node3D _supportVisuelSacDos;
    private int _signatureVisuelleCeintureEquipe = int.MinValue;
    private int _signatureVisuelleSacDosEquipe = int.MinValue;
    /// <summary>Calque 1 : corps + dÃ©cor (la camÃ©ra FPS ne rend que ce calque pour ne pas voir lâ€™intÃ©rieur de la tÃªte).</summary>
    private const uint CalqueRenduCorpsEtMondeFps = 1u;
    /// <summary>Calque 2 : uniquement tÃªte / cou / cheveux â€” masquÃ© pour la camÃ©ra FPS.</summary>
    private const uint CalqueRenduTeteFpsCachee = 2u;
    private float _solCapsuleLocalY = -0.95f;
    private int _essaisLiaisonPlaybackAnimationTree;
    private int _tentativesLecturePlaybackArbreLocomotion;
    /// <summary>AprÃ¨s avoir quittÃ© le sol : encore considÃ©rÃ© Â« au sol Â» pour lâ€™anim (Ã©vite Idle/Marche/Saut qui clignotent).</summary>
    private float _bufferSolCoyoteAnim;
    /// <summary>Coyote jump un peu plus long que lâ€™anim : le sol Â« procÃ©dural Â» clignote souvent une frame.</summary>
    private float _bufferCoyoteSaut;
    /// <summary>Saut appuyÃ© un peu avant dâ€™atterrir : consommÃ© dÃ¨s que le sol redevient valide (jump buffer).</summary>
    private float _tamponSautRestant;
    private string _clipIdleHumain = "";
    private string _clipWalkHumain = "";
    private string _clipRunHumain = "";
    private string _clipJumpHumain = "";
    private bool _fallbackAnimProcedural;
    /// <summary>BibliothÃ¨que oÃ¹ sont fusionnÃ©es les clips FBX (Idle / Marche / Saut) â€” Ã©quivalent Ã©diteur des .res externes.</summary>
    private static readonly StringName BibliothequeLocomotionMixamo = "locomotion";
    /// <summary>Lecteur unique pour les clips scriptÃ©s : mÃªme parent que le GLB, chemins de pistes cohÃ©rents avec lâ€™inspecteur Godot.</summary>
    private const string NomNoeudAnimationPlayerLocomotion = "AnimationPlayerLocomotion";
    private AnimationTree _animationTreeHumain;
    private AnimationNodeStateMachinePlayback _playbackLocomotion;
    private string _dernierEtatLocomotionTree = "";
    private bool _animationTreeContientSaut;
    /// <summary>Â« Au sol Â» frame prÃ©cÃ©dente pour dÃ©tecter le dÃ©collage (dÃ©clenche lâ€™Ã©tat Saut dans lâ€™AnimationTree).</summary>
    private bool _etaitAuSolAnimPrecedent = true;
    /// <summary>Locomotion sol : <see cref="AnimationNodeBlendSpace1D"/> Idleâ†”Marche via <c>blend_position</c> (Ã©vite le patinage Idle/Marche).</summary>
    private bool _animationTreeUtiliseBlendDeplacement;
    /// <summary>Blend 1D avec 3 points (Idle / Marche / Run) si le clip Run est fusionnÃ©.</summary>
    private bool _locomotionBlendTroisPoints;
    private const string NomEtatDeplacementBlend = "Deplacement";
    private const string NomEtatSautLocomotion = "Saut";
    private static readonly StringName NomEtatDeplacementBlendString = new StringName(NomEtatDeplacementBlend);
    private static readonly StringName NomEtatSautLocomotionString = new StringName(NomEtatSautLocomotion);
    private const string ParamBlendDeplacementLocomotion = "parameters/Deplacement/blend_position";
    /// <summary>Point blend max pour la marche quand un clip Run existe (0 = Idle, Run = 1).</summary>
    private const float BlendLocomotionMarcheMaxAvecCourse = 0.42f;
    private float _dernierBlendLocomotion = float.NaN;
    private float _derniereVitesseAnimationHumain = float.NaN;
    private const float DureeTamponSautSecondes = 0.28f;
    private const ulong NiveauxParSautAdditionnelAgiliter = 250UL;
    private int _sautsAeriensEffectues;
    /// <summary>Capsule de rÃ©fÃ©rence dans la scÃ¨ne (souvent dÃ©sactivÃ©e) : bas local utilisÃ© pour aligner les pieds du mesh.</summary>
    private const string NomCollisionReferencePieds = "CollisionShape3D";
    [Export] public Vector3 OffsetAimantMainDroiteFpsLocal { get; set; } = new Vector3(0.42f, -0.25f, -0.26f);
    [Export] public Color CouleurPeauHumain { get; set; } = new Color(0.84f, 0.69f, 0.58f, 1f);
    [Export] public Texture2D TexturePeauHumain { get; set; }
    [Export] public Color CouleurSousVetementHumain { get; set; } = new Color(0.19f, 0.22f, 0.27f, 1f);
    [Export] public Texture2D TextureSousVetementHumain { get; set; }
    // 0 = camera sur l'axe du corps (derriere la capsule HitboxCorps r=0.19 m) : evite de voir a travers
    // les murs quand on s'y colle. L'ancienne avancee (0.25 m) sortait la camera de la tete ; devenu inutile
    // car tete/cou/cheveux sont masques pour la camera FPS (CalqueRenduTeteFpsCachee).
    [Export] public float AvanceCameraFpsMetres { get; set; } = 0f;
    [Export] public Vector3 OffsetCeintureEquipeLocal { get; set; } = new Vector3(0f, -0.04f, -0.01f);
    [Export] public Vector3 RotationCeintureEquipeDeg { get; set; } = Vector3.Zero;
    [Export] public Vector3 OffsetSacDosEquipeLocal { get; set; } = new Vector3(0f, 0.18f, -0.22f);
    [Export] public Vector3 RotationSacDosEquipeDeg { get; set; } = new Vector3(0f, 90f, 0f);
    private static readonly Vector3 PositionObjetViewmodelFps = new Vector3(0.30f, -0.22f, -0.86f);
    private static readonly Vector3 RotationObjetViewmodelFpsDeg = new Vector3(10f, 154f, -10f);
    private static readonly Vector3 PositionObjetMainDefaut = new Vector3(0.035f, -0.01f, 0.065f);
    private static readonly Vector3 RotationObjetMainDefautDeg = new Vector3(8f, 92f, -16f);
    /// <summary>Orientation Mixamo -> Godot : correction latÃ©rale standard.</summary>
    public const float YawRigMixamoVersGodotDeg = 180f;

    /// <summary>Méta sur <c>HumainRigRoot</c> : chemin <c>res://</c> du GLB instancié (recréation si race ou sexe change).</summary>
    public static readonly StringName MetaCheminCorpsJoueurZk = new StringName("zk_chemin_corps");

    /// <summary>Chemin du GLB corps joueur (race + sexe), identique menu et partie.</summary>
    public static string ObtenirCheminGlbCorpsJoueur(RaceJoueur race, SexeJoueur sexe)
    {
        if (race == RaceJoueur.Orc)
        {
            return sexe == SexeJoueur.Feminin
                ? "res://Modeles/Entites/Orc/Orcesse.glb"
                : "res://Modeles/Entites/Orc/Orc.glb";
        }
        return sexe == SexeJoueur.Feminin
            ? "res://Modeles/Entites/Humain/Humaine.glb"
            : "res://Modeles/Entites/Humain/humain.glb";
    }

    /// <summary>Échelle du rig humain/orc identique en jeu et dans l’aperçu menu (assistant création).</summary>
    public static void AppliquerEchelleRigSelonRace(Node3D rig, RaceJoueur race)
    {
        if (rig == null || !GodotObject.IsInstanceValid(rig)) return;
        const float scaleHumainUniforme = 1.3f;
        if (race == RaceJoueur.Orc)
        {
            const float refH = 1.7f;
            const float refW = 0.45f;
            rig.Scale = new Vector3(
                scaleHumainUniforme * (refW + 0.1f) / refW,
                scaleHumainUniforme * (refH + 0.3f) / refH,
                scaleHumainUniforme * (refW + 0.1f) / refW);
        }
        else
            rig.Scale = Vector3.One * scaleHumainUniforme;
    }
    /// <summary>DÃ©calage Y supplÃ©mentaire du rig (pieds / sol), ajoutÃ© au bas de la capsule.</summary>
    [Export] public float DecalageYRigHumain { get; set; }
    /// <summary>Si non-NaN, remplace le bas collision utilisÃ© uniquement pour <see cref="InitialiserModeleHumainJoueur"/> (pieds du mesh), en mÃ¨tres espace local joueur.</summary>
    [Export] public float ForcerBasCollisionLocalPourAlignementPieds { get; set; } = float.NaN;
    /// <summary>Distance verticale du pivot racine du GLB (souvent hanches Mixamo) jusquâ€™aux pieds, en mÃ¨tres **avant** <see cref="Node3D.Scale"/> du rig. 0 si le pivot est dÃ©jÃ  au niveau du sol entre les pieds.</summary>
    [Export] public float HauteurPiedsSousPivotRigMixamo { get; set; } = 0.96f;
    /// <summary>Face supÃ©rieure approximative du voxel de surface : <see cref="Generateur_Voxel.ObtenirHauteurTerrainMonde"/> + cette marge (pieds posÃ©s au-dessus du bloc).</summary>
    private const float MargeSurfaceVoxelAuDessusH = 1.02f;
    /// <summary>Petit dÃ©calage pour Ã©viter le clipping pieds / sol.</summary>
    private const float MargeEpsilonPiedsSurSol = 0.07f;
    /// <summary>Euler additionnel sur le nÅ“ud racine du GLB (ajustement fin aprÃ¨s le yaw Mixamo).</summary>
    [Export] public Vector3 CorrectionManuelleEulerRigHumainDeg { get; set; }
    [ExportGroup("Deplacement - Enjambement")]
    [Export] public bool ActiverEnjambementObstacle = true;
    [Export(PropertyHint.Range, "0.08,0.8,0.01")] public float HauteurMaxEnjambementObstacle = 0.42f;
    [Export(PropertyHint.Range, "0.2,1.5,0.01")] public float DistanceAvantEnjambementObstacle = 0.56f;
    [Export(PropertyHint.Range, "0.05,4,0.01")] public float VitesseMinEnjambementObstacle = 0.32f;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float NormalYMinSolEnjambementObstacle = 0.5f;
    [Export(PropertyHint.Range, "-1,0.9,0.01")] public float NormalYMaxObstacleEnjambement = 0.34f;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float CooldownEnjambementObstacleSec = 0.08f;
    private Texture2D _texturePeauProcedurale;
    private Texture2D _textureSousVetementProcedurale;
    private Gestionnaire_Monde _gestionnaireMonde;
    private bool _modeCreatifAdmin;
    private bool _noclipAdmin;
    private uint _collisionLayerParDefaut = 1u;
    private uint _collisionMaskParDefaut = 1u;
    [Export] public float VitesseVolCreatifBase = 12f;
    [Export] public float VitesseVolCreatifVerticale = 8f;
    [Export] public float CapVitesseVolCreatif = 16f;
    [Export] public float AccelerationVolCreatif = 28f;
    private Panel _slotGauche;
    private Panel _slotDroite;
    private Label _lblHudNomMainG;
    private Label _lblHudNomMainD;
    private MarginContainer _hudStatsSurvie;
    private ProgressBar _barreFaim;
    private ProgressBar _barreEndurance;
    private Label _labelFaim;
    private Label _labelEndurance;
    private PanelContainer _panneauSelectionAtelleJambe;
    private Label _labelSelectionAtelleJambe;
    private PanelContainer _panneauSelectionAtelleBras;
    private Label _labelSelectionAtelleBras;
    private ColorRect _overlayDegatsRouge;
    private ShaderMaterial _materiauOverlayDegatsRouge;
    private Tween _tweenOverlayDegatsRouge;
    private ColorRect _overlayVisionTete;
    private ShaderMaterial _materiauOverlayVisionTete;
    private const float IntensiteMaxFlouVisionTete = 2.8f;
    private const float IntensiteMaxObscurcissementVisionTete = 0.62f;
    [ExportGroup("Diagnostic performance")]
    [Export] public bool ActiverProfilagePerfJoueur = false;
    [Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleLogProfilageJoueurSec = 2.0f;
    [ExportGroup("Diagnostic visuels FPS")]
    [Export] public bool ActiverDiagnosticVisuelsFpsAuto = false;
    [Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleDiagnosticVisuelsFpsSec = 1.0f;
    private float _cooldownDrainProfilageJoueur = 0f;
    private float _cooldownDiagnosticVisuelsFps = 0f;
    private float _cooldownDiagnosticAnomalieVisuelleFps = 0f;
    private int _dernierPourcentageFaimHud = -1;
    private int _dernierPourcentageEnduranceHud = -1;
    private float _derniereValeurBarreFaimHud = float.NaN;
    private float _derniereValeurBarreEnduranceHud = float.NaN;
    private readonly Dictionary<int, StyleBoxFlat> _cacheStyleSlotsHud = new Dictionary<int, StyleBoxFlat>();
    private MeshInstance3D _objetEnMain;
    /// <summary>Dernière identité (main active + slot) affichée sur _objetEnMain — invalide le cache visuel si changement.</summary>
    private int _derniereSignatureGlobaleObjetTenu = int.MinValue;
    private readonly HashSet<ulong> _instanceIdsVisuelsMasquesFps = new();
    private const string MetaSignatureDague105 = "SigDague105";
    private const string MetaSignatureHachette106 = "SigHachette106";
    private const string MetaSignaturePelle107 = "SigPelle107";
    private const string MetaSignaturePioche108 = "SigPioche108";
    private const string MetaSignatureLance111 = "SigLance111";
    private const string MetaSignatureFaux112 = "SigFaux112";
    private const string MetaSignatureAtelier200 = "SigAtelier200";
    private const string MetaSignatureCorde20 = "SigCorde20";
    private const string MetaSignatureTissu21 = "SigTissu21";
    private const string MetaSignatureCeinture102 = "SigCeinture102";
    private const string MetaSignatureCeinture104 = "SigCeinture104";
    private const string MetaSignaturePochette103 = "SigPochette103";
    private const string MetaSignatureSac101 = "SigSac101";
    private const string MetaSignatureRack109 = "SigRack109";
    private const string MetaSignatureCoffre113 = "SigCoffre113";
    private const string MetaSignaturePitFeu120 = "SigPitFeu120";
    private const string MetaSignaturePitFeuRoche122 = "SigPitFeuRoche122";
    private const string MetaSignatureFondation = "SigFondation";
    private const string MetaSignatureSolBois136 = "SigSolBois136";
    private const string MetaSignatureSolRoche137 = "SigSolRoche137";
    private const string MetaSignatureAllumeFeu121 = "SigAllumeFeu121";
    private const string MetaSignatureMailletBois128 = "SigMailletBois128";
    private const string MetaSignatureBolBois129 = "SigBolBois129";
    private const string MetaSignatureBolEau154 = "SigBolEau154";
    private const string MetaSignatureArgileHumid155 = "SigArgileHumid155";
    private const string MetaSignatureTorchie156 = "SigTorchie156";
    private const string MetaSignatureFourTorchie157 = "SigFourTorchie157";
    private const string MetaSignatureBolArgile158 = "SigBolArgile158";
    private const string MetaSignatureBolCeramique159 = "SigBolCeramique159";
    private const string MetaSignaturePinceOs160 = "SigPinceOs160";
    private const string MetaSignatureMouleArgile161 = "SigMouleArgile161";
    private const string MetaSignatureMouleCeramique162 = "SigMouleCeramique162";
    private const string MetaSignatureChamotte163 = "SigChamotte163";
    private const string MetaSignatureMortierPilon130 = "SigMortierPilon130";
    private const string MetaSignatureFenetreBois146 = "SigFenetreBois146";
    private const string MetaSignatureTableAnalyse131 = "SigTableAnalyse131";
    private const string MetaSignatureAtelleJambe133 = "SigAtelleJambe133";
    private const string MetaSignatureAtelleBras134 = "SigAtelleBras134";
    private const string MetaSignatureBandageTier1135 = "SigBandageTier1135";
    private const string MetaSignatureCarnet114 = "SigCarnet114";
    private const string MetaSignatureBaie35 = "SigBaie35";
    private SubViewportContainer _viewportSlotGauche;
    private SubViewportContainer _viewportSlotDroite;
    private MeshInstance3D _meshPreviewGauche;
    private MeshInstance3D _meshPreviewDroite;

    private float _forceLancer;
    private const float VitesseChargeBras = 1.8f;

    private float _rotationManuelleY = 0f;
    private float _rotationManuelleX = 0f;
    private float _rotationManuelleZ = 0f;
    /// <summary>Étages supplémentaires en mode pose fondation (molette / Page Haut-Bas), pas de limite basse.</summary>
    private int _offsetEtagesFondationManuel = 0;
    /// <summary>Mode snap muret manuel (0:auto, 1:fondation, 2:muret, 3:terrain).</summary>
    private int _modeSnapMuretManuel = 0;
    private bool _modePlacementStructureActif;
    private bool _modePlacementLancerShiftActif;
    private Node3D _ghostPlacementStructure;
    private bool _ghostPlacementValide;
    private int _ghostPlacementId = -1;
    private Color _ghostPlacementCouleur = Colors.Transparent;
    /// <summary>Clic gauche : maintien pour enregistrer le swipe avant relÃ¢chement.</summary>
    private bool _gaucheMaintenu = false;
    private Vector2 _mouvementSourisCumule = Vector2.Zero;
    private Tween _tweenFrappe;
    private AudioStreamPlayer3D _audioCoupeArbre;
    private Modelisateur_UI _modelisateur;
    private FutureState_UI _menuFutureState;
    private MenuAnatomie _menuAnatomie;
    private Control _racineMenuAnatomieViewport;
    private const string SectionCorpsTete = "tete";
    private const string SectionCorpsTorse = "torse";
    private const string SectionCorpsBrasGauche = "bras_gauche";
    private const string SectionCorpsBrasDroit = "bras_droit";
    private const string SectionCorpsJambeGauche = "jambe_gauche";
    private const string SectionCorpsJambeDroite = "jambe_droite";
    private static readonly string[] SectionsCorpsToutes =
    {
        SectionCorpsTete,
        SectionCorpsTorse,
        SectionCorpsBrasGauche,
        SectionCorpsBrasDroit,
        SectionCorpsJambeGauche,
        SectionCorpsJambeDroite
    };
    private float _pvTete;
    private float _pvTorse;
    private float _pvBrasGauche;
    private float _pvBrasDroit;
    private float _pvJambeGauche;
    private float _pvJambeDroite;
    private float _integriteOsTete;
    private float _integriteOsTorse;
    private float _integriteOsBrasGauche;
    private float _integriteOsBrasDroit;
    private float _integriteOsJambeGauche;
    private float _integriteOsJambeDroite;
    private float _timerAtelleJambeGaucheRestant;
    private float _timerAtelleJambeDroiteRestant;
    private bool _selectionAtelleJambeEnCours;
    private float _timerAtelleBrasGaucheRestant;
    private float _timerAtelleBrasDroitRestant;
    private bool _selectionAtelleBrasEnCours;
    private bool _etatAuSolPrecedent;
    private float _sommetYChuteCourante;
    private readonly SectionSanteCorps[] _cacheSanteCorps = new SectionSanteCorps[6];
    private readonly Dictionary<string, ulong> _futureStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Force"] = 0UL,
        ["Constitution"] = 0UL,
        ["Dextiriter"] = 0UL,
        ["Agiliter"] = 0UL,
        ["Metaboliste"] = 0UL,
        ["Intelligence"] = 0UL
    };
    private readonly Dictionary<string, UInt128> _futureStateXp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Force"] = 0UL,
        ["Constitution"] = 0UL,
        ["Dextiriter"] = 0UL,
        ["Agiliter"] = 0UL,
        ["Metaboliste"] = 0UL,
        ["Intelligence"] = 0UL
    };
    private readonly Dictionary<string, ulong> _metiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bucheron"] = 0UL,
        ["Traisage"] = 0UL,
        ["Artisana"] = 0UL,
        ["Batisseur"] = 0UL,
        ["Mineur"] = 0UL,
        ["Forgeron"] = 0UL,
        ["Terrassier"] = 0UL,
        ["Cuisinier"] = 0UL,
        ["Boucher"] = 0UL,
        ["Chasseur"] = 0UL
    };
    private readonly Dictionary<string, UInt128> _metierXp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bucheron"] = 0UL,
        ["Traisage"] = 0UL,
        ["Artisana"] = 0UL,
        ["Batisseur"] = 0UL,
        ["Mineur"] = 0UL,
        ["Forgeron"] = 0UL,
        ["Terrassier"] = 0UL,
        ["Cuisinier"] = 0UL,
        ["Boucher"] = 0UL,
        ["Chasseur"] = 0UL
    };

    private static PhysicsMaterial _physMatRocheRonde;
    private static PhysicsMaterial _physMatRochePlate;
    private static PhysicsMaterial _physMatRocheOvale;
    private static PhysicsMaterial _physMatRochePointe;
    private static PhysicsMaterial _physMatBois;
    private static PhysicsMaterial _physMatFibre;
    private static PhysicsMaterial _physMatCorde;
    private static PhysicsMaterial _physMatVegetalLache;
    private static PhysicsMaterial _physMatMetalForge;
    private static PhysicsMaterial _physMatDefautObjet;
    private const float DistanceParXpMetabolisteMetres = 10f;
    private const float BonusVitesseMetabolisteParNiveau = 0.00001f; // +0,001%
    private const int ValeurNeutreStat = 10;
    private const float BonusGameplayParPointStat = 0.0001f; // +0,01%
    private const float BonusChargeKgParPointForce = 0.01f; // +0,01 kg par point
    private const float BonusPvParPointConstitution = 0.01f; // +0,01 PV par section
    private const int DegatsParPointXpConstitution = 10;
    private ulong _degatsCumulesConstitution;
    private const float ChanceAnalyseBase = 0.50f;
    private const float ChanceAnalyseMin = 0.05f;
    private const float ChanceAnalyseMax = 0.95f;
    private bool _positionReferenceMetabolisteInitialisee;
    private Vector3 _positionReferenceMetaboliste;
    private float _distanceCumuleeMetabolisteMetres;
    private const float FaimMaxJoueur = 100f;
    private const float EnduranceMaxJoueur = 100f;
    private const float DrainFaimPassifParSeconde = 0.018f;
    private const float DrainFaimEffortParSeconde = 0.075f;
    private const float DrainFaimSprintParSeconde = 0.055f;
    private const float DrainEnduranceActionParSeconde = 1f;
    private const float DrainEnduranceSprintParSeconde = 2.2f;
    private const float RegenEnduranceParSeconde = 10f;
    private const float CoutFaimParPointEndurance = 0.0045f;
    /// <summary>Multiplicateur sur la perte de faim (passif, effort, sprint, coût lié à la régénération d'énergie).</summary>
    private const float FacteurRalentissementDrainFaim = 0.5f;
    /// <summary>Dégâts par seconde sur le torse lorsque la faim est épuisée (affamer).</summary>
    private const float DegatsTorseParSecondeFaimNulle = 2f;
    private const float MultiplicateurVitesseSprint = 1.65f;
    /// <summary>Au sol : vitesse physique ×1,05 ; <see cref="Speed"/> reste la référence des blends d’animation.</summary>
    private const float FacteurVitesseMouvementAuSol = 1.05f;
    private const float GainFaimClicDroitMainVide = 12f;
    private const float CooldownGainFaimClicDroitSec = 0.22f;
    private const float DureeBuffVitesseBaieNoireSec = 5f;
    private const float DureeBuffSautBaieOrangeSec = 5f;
    private const float DureeBuffReductionDegatsBaieBleueSec = 3f;
    private const float MultiplicateurVitesseBaieNoire = 1.25f;
    private const float MultiplicateurSautBaieOrange = 2f;
    private const float MultiplicateurDegatsBaieBleue = 0.5f;
    private const float DegatsTotalPoisonBaieRose = 100f;
    private const float DureePoisonBaieRoseSec = 24f * 60f * 60f;
    private const float MultiplicateurPoisonMin = 0.0001f;
    private const float IntervalleDegatsBrulureFeuSec = 0.35f;
    private const float PertePvMaxBrulureParImpact = 1f;
    private float _faimJoueur = FaimMaxJoueur;
    private float _enduranceJoueur = EnduranceMaxJoueur;
    private float _cooldownGainFaimClicDroit;
    private float _timerBuffVitesseBaieNoireRestant;
    private float _timerBuffSautBaieOrangeRestant;
    private float _timerBuffReductionDegatsBaieBleueRestant;
    private string _sectionPoisonBaieRose = SectionCorpsTorse;
    private float _degatsPoisonBaieRoseRestants;
    private float _dureePoisonBaieRoseRestanteSec;
    private float _accumulateurDegatsPoisonBaieRose;
    private float _multiplicateurPoisonBaieRose = 1f;
    private float _cooldownDegatsBrulureFeuRestant;
    private float _malusPvMaxBrulureTete;
    private float _malusPvMaxBrulureTorse;
    private float _malusPvMaxBrulureBrasGauche;
    private float _malusPvMaxBrulureBrasDroit;
    private float _malusPvMaxBrulureJambeGauche;
    private float _malusPvMaxBrulureJambeDroite;
    private float _cooldownEnjambementObstacle;
    private bool _mortJoueurEnCours;
    private CanvasLayer _layerMortRecreation;
    private Control _panneauMortCitation;
    private Control _panneauMortChoix;
    private Control _panneauMortCreation;
    private LineEdit _lineNomMortRecreation;
    private Label _labelErreurMortRecreation;
    private Label _labelRaceMortRecreation;
    private Label _labelSexeMortRecreation;
    private RaceJoueur _raceMortRecreation = RaceJoueur.Humain;
    private SexeJoueur _sexeMortRecreation = SexeJoueur.Masculin;
    private const string CitationMortNature =
        "La nature ne pleure pas les espèces stériles. Elle les remplace.";

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        InitialiserSanteCorps();
        ReinitialiserEffetsConsommationBaies();
        ReinitialiserBruluresFeu();
        _etatAuSolPrecedent = IsOnFloor();
        _sommetYChuteCourante = GlobalPosition.Y;

        _physMatRocheRonde = new PhysicsMaterial { Friction = 0.18f, Bounce = 0.48f };
        _physMatRochePlate = new PhysicsMaterial { Friction = 0.94f, Bounce = 0.07f };
        _physMatRocheOvale = new PhysicsMaterial { Friction = 0.52f, Bounce = 0.16f };
        _physMatRochePointe = new PhysicsMaterial { Friction = 0.86f, Bounce = 0.05f };
        _physMatBois = new PhysicsMaterial { Friction = 0.78f, Bounce = 0.18f };
        _physMatFibre = new PhysicsMaterial { Friction = 0.86f, Bounce = 0.19f };
        _physMatCorde = new PhysicsMaterial { Friction = 0.84f, Bounce = 0.11f };
        _physMatVegetalLache = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.12f };
        _physMatMetalForge = new PhysicsMaterial { Friction = 0.48f, Bounce = 0.04f };
        _physMatDefautObjet = new PhysicsMaterial { Friction = 0.65f, Bounce = 0.1f };

        _cameraFps = GetNode<Camera3D>("Camera3D");
        _rayonFps = GetNode<RayCast3D>("Camera3D/RayCast3D");
        _positionLocaleBaseCameraFps = _cameraFps.Position;
        _camera = _cameraFps;
        _rayon = _rayonFps;
        _rayonFps.TargetPosition = new Vector3(0f, 0f, -12f);
        _rayonFps.CollisionMask = 0xFFFFFFFF; // Toutes les couches (sol AAA = layer 1, objets, eauâ€¦)
        _rayonFps.AddException(this); // Ne pas toucher le joueur (sinon le "minage" ne vise pas le sol)
        // Sol = calque 1 ; cadre portail Nexus = calque 2 (<see cref="Portail.CalqueCollisionCadrePortail"/>) pour ne pas bloquer les raycasts sol (masque 1).
        CollisionLayer = 1u;
        CollisionMask = 1u | Portail.CalqueCollisionCadrePortail;
        _collisionLayerParDefaut = CollisionLayer;
        _collisionMaskParDefaut = CollisionMask;
        // Sol voxel irrÃ©gulier : snap modÃ©rÃ© + marge rÃ©duite pour Ã©viter le pompage vertical.
        FloorSnapLength = 0.32f;
        SafeMargin = 0.06f;
        FloorMaxAngle = Mathf.DegToRad(52f);
        ConstruireHitboxesCompositeJoueur();
        RedimensionnerHitboxesSiOrc();
        ConstruireRigCameraTps();
        InitialiserModeleHumainJoueur();
        Callable.From(RetryLierPlaybackAnimationTreeHumain).CallDeferred();
        _pitchCamera = 0f;
        if (_cameraFps != null)
            _cameraFps.Rotation = new Vector3(_pitchCameraBaseRad + _pitchCamera, _yawCorrectionCameraFpsRad, 0f);
        if (_pivotCameraTps != null)
            _pivotCameraTps.Rotation = new Vector3((_pitchCameraBaseRad + _pitchCamera) * 0.82f, 0f, 0f);
        ConfigurerModeCamera(false);
        _gestionnaireMonde = GetParent().GetNode<Gestionnaire_Monde>("Gestionnaire_Monde");
        _slotGauche = GetParent().GetNode<Panel>("Gestionnaire_Monde/HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Gauche");
        _slotDroite = GetParent().GetNode<Panel>("Gestionnaire_Monde/HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Droite");
        ResoudreSlotCarnetHud();
        InsererNomsAuDessusSlotsHud();
        AssurerOverlayDegatsRouge();

        CreerPreviewsInventaire3D();
        InitialiserCarnetSavoirSysteme();

        _modelisateur = new Modelisateur_UI();
        // Le parent (Monde_Zero) est encore en _Ready : add_child direct Ã©choue â†’ diffÃ©rÃ©.
        CallDeferred(nameof(BrancherModelisateurCAO));

        _menuFutureState = new FutureState_UI();
        CallDeferred(nameof(BrancherMenuFutureState));

        RafraichirHUD();

        PackedScene sceneMenu = GD.Load<PackedScene>("res://Scenes/UI/MenuAnatomie.tscn");
        if (sceneMenu != null)
        {
            _menuAnatomie = sceneMenu.Instantiate<MenuAnatomie>();
            // Calque 100 : le menu pause du Gestionnaire est en 101 pour sâ€™afficher par-dessus lâ€™inventaire.
            var layerAnatomie = new CanvasLayer { Layer = 100, Name = "LayerAnatomie", ProcessMode = ProcessModeEnum.Always };
            // Un Control sous CanvasLayer sans parent Control nâ€™obtient pas la taille du viewport â†’ UI rÃ©duite Ã  un coin.
            var racineViewport = new Control { Name = "RacineMenuAnatomieViewport" };
            racineViewport.MouseFilter = Control.MouseFilterEnum.Ignore;
            _racineMenuAnatomieViewport = racineViewport;
            AddChild(layerAnatomie);
            layerAnatomie.AddChild(racineViewport);
            // Le menu sâ€™initialise en _Ready : si le parent est encore 0Ã—0, tout lâ€™UI reste coincÃ© au coin.
            AjusterRacineMenuAnatomieViewport();
            racineViewport.AddChild(_menuAnatomie);
            _menuAnatomie.Initialiser(this);
            CreerHudStatsSurvie();
            CallDeferred(nameof(AjusterRacineMenuAnatomieViewport));
            if (GetViewport() != null)
                GetViewport().SizeChanged += OnViewportTailleMenuAnatomie;
        }
        else
        {
            CreerHudStatsSurvie();
        }
        InitialiserChatInGame();
    }







    public override void _Process(double delta)
    {
        float dt = (float)delta;
        AppliquerVisibiliteCorpsLocalSelonVue();
        MettreAJourFiltreEauImmersion(dt);
        if (ActiverDiagnosticVisuelsFpsAuto && !_vueTroisiemePersonne)
        {
            _cooldownDiagnosticVisuelsFps -= dt;
            if (_cooldownDiagnosticVisuelsFps <= 0f)
            {
                _cooldownDiagnosticVisuelsFps = Mathf.Max(0.2f, IntervalleDiagnosticVisuelsFpsSec);
                DiagnostiquerVisuelsFpsRuntime();
            }
        }
        if (!_vueTroisiemePersonne && !DoitAfficherCorpsLocal())
        {
            _cooldownDiagnosticAnomalieVisuelleFps -= dt;
            if (_cooldownDiagnosticAnomalieVisuelleFps <= 0f && DetecterAnomalieVisuelleCorpsEnFps(out var detailsAnomalie))
            {
                _cooldownDiagnosticAnomalieVisuelleFps = 0.75f;
                GD.Print($"ZERO-K [DiagFPS-Alerte] visuels corps rendus en FPS ({detailsAnomalie.Count}).");
                foreach (string ligne in detailsAnomalie)
                    GD.Print(ligne);
            }
        }
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (EstModePlacementGhostActif())
        {
            if (EstUiJoueurBloquanteOuverte() || !EstModePlacementGhostActifPourSlot(mainActive))
                AnnulerModePlacementStructure(reinitialiserRotation: false);
            else
                MettreAJourGhostPlacementStructure(mainActive);
        }
        if (_cooldownMessageCarnet > 0f)
            _cooldownMessageCarnet = Mathf.Max(0f, _cooldownMessageCarnet - dt);
        if (_aimantIkMainDroite == null || !GodotObject.IsInstanceValid(_aimantIkMainDroite) || _ikBrasDroitFps == null || !GodotObject.IsInstanceValid(_ikBrasDroitFps))
            return;

        bool activerIk = !_vueTroisiemePersonne && !MainGaucheEstActive && !mainActive.EstVide;
        float cibleBlend = activerIk ? 1f : 0f;
        _ikBlendMainDroite = Mathf.MoveToward(_ikBlendMainDroite, cibleBlend, dt * 9.5f);
        _impulsionIkFrappePoids = Mathf.MoveToward(_impulsionIkFrappePoids, 0f, dt * 8.0f);

        Vector3 offsetFrappe = _impulsionIkFrappeLocal * _impulsionIkFrappePoids;
        _aimantIkMainDroite.Position = OffsetAimantMainDroiteFpsLocal + offsetFrappe;
        _ikBrasDroitFps.Influence = _ikBlendMainDroite;
    }



    private static bool EstToggleFutureState(InputEvent e)
    {
        return e is InputEventKey k
            && k.Pressed
            && !k.Echo
            && (k.Keycode == Key.K || k.PhysicalKeycode == Key.K);
    }

    private static bool SlotEstAllumeFeu(SlotInventaire slot) => !slot.EstVide && slot.ID == IdObjetAllumeFeu;

    private bool ConsommerUsageAllumeFeu(ref SlotInventaire slot)
    {
        if (!SlotEstAllumeFeu(slot))
            return false;
        Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slot);
        slot.DurabiliteOutilActuelle = Mathf.Max(0f, slot.DurabiliteOutilActuelle - 1f);
        if (slot.DurabiliteOutilActuelle <= 0.001f)
        {
            GD.Print("ZERO-K : L'allume-feu s'est brisé.");
            slot = new SlotInventaire();
        }
        return true;
    }

    private bool EssayerConsommerAllumeFeuDisponiblePourTorche()
    {
        if (MainGaucheEstActive)
        {
            if (SlotEstAllumeFeu(MainDroite))
            {
                var s = MainDroite;
                bool ok = ConsommerUsageAllumeFeu(ref s);
                MainDroite = s;
                return ok;
            }
        }
        else if (SlotEstAllumeFeu(MainGauche))
        {
            var s = MainGauche;
            bool ok = ConsommerUsageAllumeFeu(ref s);
            MainGauche = s;
            return ok;
        }

        for (int i = 0; i < GrilleCeintureStockage.Length; i++)
        {
            if (!SlotEstAllumeFeu(GrilleCeintureStockage[i]))
                continue;
            var s = GrilleCeintureStockage[i];
            bool ok = ConsommerUsageAllumeFeu(ref s);
            GrilleCeintureStockage[i] = s;
            return ok;
        }

        for (int i = 0; i < GrilleSacStockage.Length; i++)
        {
            if (!SlotEstAllumeFeu(GrilleSacStockage[i]))
                continue;
            var s = GrilleSacStockage[i];
            bool ok = ConsommerUsageAllumeFeu(ref s);
            GrilleSacStockage[i] = s;
            return ok;
        }

        return false;
    }

    private bool EssayerAllumerTorcheEnMain(ref SlotInventaire mainActive)
    {
        if (mainActive.EstVide || !EstIdTorche(mainActive.ID))
            return false;
        if ((mainActive.GenomeAssemblage ?? "").StartsWith("TORCHE:1", StringComparison.Ordinal))
        {
            GD.Print("ZERO-K : La torche en main est déjà allumée.");
            return false;
        }
        if (!EssayerConsommerAllumeFeuDisponiblePourTorche())
        {
            GD.Print("ZERO-K : Allume-feu requis (autre main ou inventaire) pour allumer la torche.");
            return false;
        }

        mainActive.GenomeAssemblage = "TORCHE:1";
        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
        GD.Print("ZERO-K : Torche allumée.");
        return true;
    }



    /// <summary>Masque le mesh Â« en main Â» devant la camÃ©ra quand le menu CAO est ouvert (Ã©vite la confusion avec le transit UI).</summary>
    public void DefinirVisibiliteObjetMainCamera(bool visible)
    {
        if (_objetEnMain != null)
            _objetEnMain.Visible = visible;
    }

    /// <summary>True si le menu inventaire (Q) est ouvert â€” utilisÃ© par le gestionnaire pour Ã‰chap â†’ pause sans fermer lâ€™UI.</summary>
    public bool MenuAnatomieOuvert() => _menuAnatomie != null && _menuAnatomie.EstOuvert;
    public bool ModeCreatifActif => _modeCreatifAdmin;
    public bool NoclipAdminActif => _modeCreatifAdmin && _noclipAdmin;


    /// <summary>True si une UI joueur bloque le contrôle déplacement/saut.</summary>
    private bool EstUiJoueurBloquanteOuverte()
    {
        return (_modelisateur != null && _modelisateur.EstOuvert)
            || (_menuFutureState != null && _menuFutureState.EstOuvert)
            || (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            || CarnetSavoirOuvert()
            || ChatInGameOuvert();
    }

    /// <summary>Ferme les UI joueur (inventaire/craft ou CAO) via Ã‰chap et remet le contrÃ´le jeu.</summary>
    public bool FermerUIJoueurSiOuverte()
    {
        if (_menuFutureState != null && _menuFutureState.EstOuvert)
        {
            _menuFutureState.BasculerVisibilite();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return true;
        }
        if (CarnetSavoirOuvert())
        {
            FermerCarnetSavoirUI();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return true;
        }
        if (ChatInGameOuvert())
        {
            FermerChatInGame();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return true;
        }
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            CraftGrille3x3AuTable = false;
            IdStationCraftOuverte = 0;
            AtelierPlanTravailOuvert = null;
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
            StockageCoffreOuvert = false;
            CoffreOuvert = null;
            StockageFourTorchieOuvert = false;
            FourTorchieOuvert = null;
            _menuAnatomie.BasculerVisibilite();
            RafraichirHUD();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return true;
        }
        if (_modelisateur != null && _modelisateur.EstOuvert && !_modelisateur.SaisieTexteEnCours)
        {
            _modelisateur.BasculerVisibilite();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return true;
        }
        return false;
    }

    public IReadOnlyDictionary<string, ulong> ObtenirFutureStates() => _futureStates;

    public IReadOnlyDictionary<string, UInt128> ObtenirFutureStatesXp() => _futureStateXp;

    public readonly struct FicheStatutPersonnage
    {
        public readonly string NomRace;
        public readonly ulong NiveauGlobalFutureStates;
        public readonly int PointsVieActuels;
        public readonly int PointsVieMax;
        public readonly int Force;
        public readonly int Constitution;
        public readonly int Agilite;
        public readonly int Intelligence;
        public readonly int Metabolisme;
        public readonly int Defense;

        public FicheStatutPersonnage(
            string nomRace,
            ulong niveauGlobalFutureStates,
            int pointsVieActuels,
            int pointsVieMax,
            int force,
            int constitution,
            int agilite,
            int intelligence,
            int metabolisme,
            int defense)
        {
            NomRace = nomRace;
            NiveauGlobalFutureStates = niveauGlobalFutureStates;
            PointsVieActuels = pointsVieActuels;
            PointsVieMax = pointsVieMax;
            Force = force;
            Constitution = constitution;
            Agilite = agilite;
            Intelligence = intelligence;
            Metabolisme = metabolisme;
            Defense = defense;
        }
    }




    public void RafraichirHUD()
    {
        AssurerDurabiliteOutilsSurLesMains();
        if (EstModePlacementGhostActif())
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!EstModePlacementGhostActifPourSlot(mainActive))
                AnnulerModePlacementStructure(reinitialiserRotation: false);
        }
        MettreAJourSlotUI(_slotGauche, MainGauche, MainGaucheEstActive);
        MettreAJourSlotUI(_slotDroite, MainDroite, !MainGaucheEstActive);
        MettreAJourSlotUI(_slotCarnet, EquipementCarnet, false);
        MettreAJourLibellesNomsHud();
        MettreAJourHudStatsSurvie();
        MettreAJourObjetEnMain();
        RafraichirVisuelsEquipementsCorps();
        MettreAJourPreviewsSlots();
        MettreAJourVisibilitePreviews();
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            ulong intervalleMenuHudMs = _modeCreatifAdmin ? 150UL : 50UL;
            ulong maintenant = Time.GetTicksMsec();
            if (maintenant - _msDernierRafraichirMenuCompletHud >= intervalleMenuHudMs
                || _msDernierRafraichirMenuCompletHud == 0UL)
            {
                _msDernierRafraichirMenuCompletHud = maintenant;
                _menuAnatomie.RafraichirMenu();
            }
        }
        else
            _msDernierRafraichirMenuCompletHud = 0UL;
    }

    /// <summary>Assigne le Mesh exact de la main active au MeshInstance3D devant la camÃ©ra.</summary>
    private static bool EstObjetProcedural(int id) => ItemPhysique.EstIdRocheMatiere(id);

    private static bool PeutUtiliserFrappe(SlotInventaire s)
    {
        if (s.EstVide) return false;
        if (EstObjetProcedural(s.ID)) return true;
        if (s.ID == 105 || s.ID == 106 || s.ID == IdObjetHachePierreTier1 || s.ID == IdObjetPellePierreTier0 || s.ID == IdObjetPiochePierreTier0 || s.ID == IdObjetLancePierreTier0 || s.ID == IdObjetFauxPierreTier0) return true;
        return s.ID == 100 && s.EstUnEclat && s.MeshEclat != null;
    }

    private void AssurerDurabiliteOutilsSurLesMains()
    {
        if (MainGauche.ID == 105 || MainGauche.ID == 106 || MainGauche.ID == IdObjetHachePierreTier1 || MainGauche.ID == IdObjetPellePierreTier0 || MainGauche.ID == IdObjetPiochePierreTier0 || MainGauche.ID == IdObjetLancePierreTier0 || MainGauche.ID == IdObjetFauxPierreTier0)
        {
            var m = MainGauche;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref m);
            MainGauche = m;
        }
        if (MainDroite.ID == 105 || MainDroite.ID == 106 || MainDroite.ID == IdObjetHachePierreTier1 || MainDroite.ID == IdObjetPellePierreTier0 || MainDroite.ID == IdObjetPiochePierreTier0 || MainDroite.ID == IdObjetLancePierreTier0 || MainDroite.ID == IdObjetFauxPierreTier0)
        {
            var m = MainDroite;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref m);
            MainDroite = m;
        }
    }

    /// <summary>Usure dague / hachette main active aprÃ¨s un usage rÃ©ussi (fauchage, coupe, frappe roche, fente boisâ€¦). Retourne l'ID de l'outil cassÃ© (0 si rien cassÃ©).</summary>
    private int AppliquerUsureOutilMainActive(float cout)
    {
        if (cout <= 0f) return 0;
        bool casse = false;
        int idOutilCasse = 0;
        if (MainGaucheEstActive)
        {
            if (MainGauche.ID != 105 && MainGauche.ID != 106 && MainGauche.ID != IdObjetHachePierreTier1 && MainGauche.ID != IdObjetPellePierreTier0 && MainGauche.ID != IdObjetPiochePierreTier0 && MainGauche.ID != IdObjetLancePierreTier0 && MainGauche.ID != IdObjetFauxPierreTier0) return 0;
            var m = MainGauche;
            int idOutil = m.ID;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref m);
            m.DurabiliteOutilActuelle -= cout;
            if (m.DurabiliteOutilActuelle <= 0f)
            {
                idOutilCasse = idOutil;
                MainGauche = default;
                casse = true;
            }
            else
                MainGauche = m;
        }
        else
        {
            if (MainDroite.ID != 105 && MainDroite.ID != 106 && MainDroite.ID != IdObjetHachePierreTier1 && MainDroite.ID != IdObjetPellePierreTier0 && MainDroite.ID != IdObjetPiochePierreTier0 && MainDroite.ID != IdObjetLancePierreTier0 && MainDroite.ID != IdObjetFauxPierreTier0) return 0;
            var m = MainDroite;
            int idOutil = m.ID;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref m);
            m.DurabiliteOutilActuelle -= cout;
            if (m.DurabiliteOutilActuelle <= 0f)
            {
                idOutilCasse = idOutil;
                MainDroite = default;
                casse = true;
            }
            else
                MainDroite = m;
        }
        if (casse)
        {
            if (idOutilCasse == 105)
                GD.Print("ZERO-K : La dague primitive se brise â€” lame ou manche a cÃ©dÃ©. Il vous faudra une nouvelle lame et une corde.");
            else if (idOutilCasse == IdObjetFauxPierreTier0)
                GD.Print("ZERO-K : La faux primitive se brise — il faut refaire l’outil (roche pointue, ligature, manche et bâtons en T).");
            else if (idOutilCasse == 106)
                GD.Print("ZERO-K : La hachette primitive se brise â€” lame ou manche a cÃ©dÃ©. Il vous faudra refaire lâ€™outil.");
            else if (idOutilCasse == IdObjetHachePierreTier1)
                GD.Print("ZERO-K : La hache en pierre se brise — il faut reforger l'outil.");
            else if (idOutilCasse == IdObjetPellePierreTier0)
                GD.Print("ZERO-K : La pelle en pierre se brise â€” il faut reforger lâ€™outil.");
            else if (idOutilCasse == IdObjetLancePierreTier0)
                GD.Print("ZERO-K : La lance en pierre se brise â€” il faut la reforger.");
            else
                GD.Print("ZERO-K : La pioche en pierre se brise â€” il faut reforger lâ€™outil.");
        }
        RafraichirHUD();
        return idOutilCasse;
    }

    private static void RemplirDurabiliteOutilDepuisItemPhysique(ref SlotInventaire slot, ItemPhysique item)
    {
        if ((slot.ID != 105 && slot.ID != 106 && slot.ID != IdObjetHachePierreTier1 && slot.ID != IdObjetPellePierreTier0 && slot.ID != IdObjetPiochePierreTier0 && slot.ID != IdObjetLancePierreTier0 && slot.ID != IdObjetFauxPierreTier0) || item == null) return;
        if (item.HasMeta(MetaDurabiliteOutilMax))
        {
            slot.DurabiliteOutilMax = (float)item.GetMeta(MetaDurabiliteOutilMax).AsDouble();
            slot.DurabiliteOutilActuelle = item.HasMeta(MetaDurabiliteOutilActuelle)
                ? (float)item.GetMeta(MetaDurabiliteOutilActuelle).AsDouble()
                : slot.DurabiliteOutilMax;
        }
        else
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slot);
        if (item.HasMeta(MetaTailleLameRoche) && (slot.ID == 105 || slot.ID == IdObjetFauxPierreTier0))
            slot.IndexTailleLameRoche = (int)item.GetMeta(MetaTailleLameRoche).AsInt32();
    }


    private static ArbreVivant ObtenirArbreDepuisCollider(Node col)
    {
        for (Node n = col; n != null; n = n.GetParent())
            if (n is ArbreVivant a) return a;
        return null;
    }

    /// <summary>Le raycast renvoie souvent la <see cref="CollisionShape3D"/> enfant, pas le corps â€” sinon la frappe retombe sans effet ni log.</summary>
    private static RigidBody3D ResoudreRigidBodyDepuisCollider(Node col)
    {
        if (col == null) return null;
        if (col is RigidBody3D rb0) return rb0;
        for (Node n = col; n != null; n = n.GetParent())
            if (n is RigidBody3D rb) return rb;
        return null;
    }

    /// <summary>Cadavre d'arbre abattu (<c>ArbreMort</c>) depuis un collider enfant (feuillage, tronc, hitbox).</summary>
    private static RigidBody3D ResoudreCadavreArbreDepuisCollider(Node col)
    {
        for (Node n = col; n != null; n = n.GetParent())
        {
            if (n is RigidBody3D rb && (rb.Name.ToString().Contains("ArbreMort") || rb.IsInGroup("CadavreArbre")))
                return rb;
        }
        return null;
    }

    /// <summary>
    /// Résout le cadavre visé : collider direct, puis le long du rayon caméra, puis proximité filtrée par la visée.
    /// Évite de frapper le mauvais arbre quand plusieurs cadavres sont proches.
    /// </summary>
    private RigidBody3D ResoudreCadavreArbreCible(Node objetTouche, Vector3 pointImpact)
    {
        RigidBody3D direct = ResoudreCadavreArbreDepuisCollider(objetTouche);
        if (direct != null && GodotObject.IsInstanceValid(direct))
            return direct;
        RigidBody3D leLongVisée = ChercherCadavreArbreLeLongVisée(pointImpact);
        if (leLongVisée != null)
            return leLongVisée;
        return ChercherCadavreArbreProchePointImpact(pointImpact);
    }

    /// <summary>Parcourt le rayon caméra→impact en ignorant sol/objets jusqu'à trouver un <c>ArbreMort</c>.</summary>
    private RigidBody3D ChercherCadavreArbreLeLongVisée(Vector3 pointImpact, float margeApresImpactMetres = 1.6f)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null || _rayon == null)
            return null;

        Vector3 origine = _rayon.GlobalPosition;
        Vector3 versImpact = pointImpact - origine;
        if (versImpact.LengthSquared() < 1e-8f)
            return null;
        Vector3 direction = versImpact.Normalized();
        float longueur = versImpact.Length() + margeApresImpactMetres;
        Vector3 destination = origine + direction * longueur;

        var excludes = new Godot.Collections.Array<Rid>();
        if (this is CollisionObject3D coJoueur)
            excludes.Add(coJoueur.GetRid());

        const int maxPasses = 10;
        for (int passe = 0; passe < maxPasses; passe++)
        {
            var q = PhysicsRayQueryParameters3D.Create(origine, destination);
            q.CollisionMask = 0xFFFFFFFF;
            q.CollideWithAreas = false;
            q.CollideWithBodies = true;
            q.Exclude = excludes;
            Godot.Collections.Dictionary hit = space.IntersectRay(q);
            if (hit.Count == 0 || !hit.ContainsKey("collider"))
                break;

            Node col = NoeudDepuisColliderRaycast(hit["collider"].AsGodotObject());
            RigidBody3D cadavre = ResoudreCadavreArbreDepuisCollider(col);
            if (cadavre != null && GodotObject.IsInstanceValid(cadavre) && cadavre.IsInsideTree())
                return cadavre;

            if (hit.ContainsKey("rid"))
                excludes.Add((Rid)hit["rid"]);
            else
                break;
        }
        return null;
    }

    /// <summary>Après abattage, la visée peut toucher le sol sans collider cadavre : on cherche dans un tube autour du rayon caméra.</summary>
    private RigidBody3D ChercherCadavreArbreProchePointImpact(Vector3 pointMonde, float rayonMetres = 2.35f)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return null;

        Vector3 origineRayon = _rayon != null ? _rayon.GlobalPosition : pointMonde + Vector3.Up * 1.6f;
        Vector3 dirRayon = pointMonde - origineRayon;
        if (dirRayon.LengthSquared() < 1e-8f && _camera != null)
            dirRayon = -_camera.GlobalTransform.Basis.Z;
        if (dirRayon.LengthSquared() < 1e-8f)
            dirRayon = Vector3.Forward;
        dirRayon = dirRayon.Normalized();

        float rayonSq = rayonMetres * rayonMetres;
        const float maxEcartRayonMetres = 1.05f;
        float maxEcartRayonSq = maxEcartRayonMetres * maxEcartRayonMetres;
        var ppq = new PhysicsPointQueryParameters3D
        {
            Position = pointMonde,
            CollisionMask = 0xFFFFFFFF,
            CollideWithAreas = false,
            CollideWithBodies = true
        };
        Godot.Collections.Array<Godot.Collections.Dictionary> results = space.IntersectPoint(ppq, 48);
        RigidBody3D meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < results.Count; i++)
        {
            if (!results[i].TryGetValue("collider", out Variant vCol)) continue;
            var colObj = vCol.AsGodotObject();
            Node noeud = colObj is CollisionShape3D sh ? sh.GetParent() as Node : colObj as Node;
            RigidBody3D rb = ResoudreCadavreArbreDepuisCollider(noeud);
            if (rb == null || !GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree()) continue;

            Vector3 auCorps = rb.GlobalPosition - origineRayon;
            float leLong = auCorps.Dot(dirRayon);
            if (leLong < -0.35f || leLong > rayonMetres + 2.5f)
                continue;
            Vector3 perp = auCorps - dirRayon * leLong;
            if (perp.LengthSquared() > maxEcartRayonSq)
                continue;

            float distImpactSq = rb.GlobalPosition.DistanceSquaredTo(pointMonde);
            if (distImpactSq > rayonSq)
                continue;

            // Priorité : alignement visée, puis proximité du point d'impact.
            float score = perp.LengthSquared() * 6f + distImpactSq;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = rb;
            }
        }
        return meilleur;
    }

    /// <summary>Surface dâ€™appui uniquement ROCHE (sol ID 2, cailloux matiÃ¨re 40â€“49). Le bois posÃ© nâ€™est pas une enclume.</summary>
    private bool EstSurfaceSupportAffutage(Node objetTouche, Vector3 pointMonde)
    {
        if (objetTouche == null) return false;
        if (ObtenirArbreDepuisCollider(objetTouche) != null) return false;

        for (Node n = objetTouche; n != null; n = n.GetParent())
        {
            if (n.Name.ToString().Contains("ArbreMort")) return false;
        }

        for (Node n = objetTouche; n != null; n = n.GetParent())
        {
            if (n is ItemPhysique ip)
            {
                if (ip.ID_Objet == 15 || ip.ID_Objet == 20 || ip.ID_Objet == 21 || ip.ID_Objet == 34)
                    return false;
                if (ItemPhysique.EstIdRocheMatiere(ip.ID_Objet))
                    return true;
            }
        }

        string nm = objetTouche.Name.ToString();
        if (nm.Contains("TerrainSection") || nm.Contains("CollisionSection"))
        {
            int id = _gestionnaireMonde?.ObtenirMatiereExacte(pointMonde - new Vector3(0f, 0.22f, 0f)) ?? 1;
            return id == 2;
        }

        return false;
    }

    private void JouerSonEtEffetCoupeArbre(Vector3 pos)
    {
        if (_audioCoupeArbre == null)
        {
            _audioCoupeArbre = new AudioStreamPlayer3D { Bus = "Master", VolumeDb = -3f, MaxDistance = 25f };
            var wav = new AudioStreamWav { MixRate = 22050, Stereo = false, Format = AudioStreamWav.FormatEnum.Format16Bits };
            const int samples = 2205;
            var data = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                short s = (short)(16000 * Mathf.Exp(-t * 8) * Mathf.Sin(t * 80) * (0.5f + GD.Randf() * 0.5f));
                data[i * 2] = (byte)(s & 0xFF);
                data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            wav.Data = data;
            _audioCoupeArbre.Stream = wav;
            GetTree().CurrentScene.AddChild(_audioCoupeArbre);
        }
        _audioCoupeArbre.GlobalPosition = pos;
        _audioCoupeArbre.Play();

        var container = new Node3D { Name = "EffetCoupeArbre" };
        GetTree().CurrentScene.AddChild(container);
        container.GlobalPosition = pos;

        var matCopeaux = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.28f, 0.1f), Roughness = 0.9f };
        for (int i = 0; i < 8; i++)
        {
            var mi = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.03f, 0.04f) * (0.7f + GD.Randf() * 0.6f) },
                MaterialOverride = matCopeaux,
                Position = new Vector3((float)(GD.Randf() - 0.5f) * 0.2f, (float)GD.Randf() * 0.1f, (float)(GD.Randf() - 0.5f) * 0.2f)
            };
            container.AddChild(mi);
        }
        var timer = container.GetTree().CreateTimer(0.35);
        timer.Timeout += () => container.QueueFree();
    }


}

