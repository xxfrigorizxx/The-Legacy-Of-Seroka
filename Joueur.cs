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
        public readonly int PointsVie;
        public readonly int PointsVieMax;

        public SectionSanteCorps(string cle, string nom, string matiere, int pointsVie, int pointsVieMax)
        {
            Cle = cle;
            Nom = nom;
            Matiere = matiere;
            PointsVie = pointsVie;
            PointsVieMax = pointsVieMax;
        }
    }

    public const ulong NiveauMaxFutureState = 10_000_000_000_000_000_000UL;
    public const float CapacitePoidsBaseHumainKg = 20.0f;
    private static readonly UInt128 XpHybrideCoefLineaire = (UInt128)10;
    private static readonly UInt128 XpHybrideDivQuadratique = (UInt128)1000;
    /// <summary>MÃ©ta et slots : mÃªme clÃ© pour lâ€™Ã©tabli CAO et les corps posÃ©s.</summary>
    public const string MetaGenomeAssemblage = "GenomeAssemblage";
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
    /// <summary>Rack Ã  bÃ¢tons (stockage dÃ©diÃ©).</summary>
    public const int IdObjetRackBatons = 109;
    /// <summary>Rack Ã  bÃ»ches (stockage dÃ©diÃ©).</summary>
    public const int IdObjetRackBuches = 110;
    /// <summary>Petite baie rÃ©coltable sur buisson (palette couleur via IndexChimique).</summary>
    public const int IdObjetBaie = 35;

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

    public const float Speed = 5.0f;
    public const float JumpVelocity = 5.15f;

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

    /// <summary>True si le menu a Ã©tÃ© ouvert depuis lâ€™atelier posÃ© : recettes et UI en 3Ã—3. False aprÃ¨s Q ou fermeture du menu.</summary>
    public bool CraftGrille3x3AuTable { get; set; }
    /// <summary>True si la grille 3Ã—3 sert de stockage rack bÃ¢tons (pas de recettes).</summary>
    public bool StockageRackBatonsOuvert { get; set; }

    /// <summary>Slot contenant le rÃ©sultat d'une recette valide.</summary>
    public SlotInventaire SlotResultatCraft = new SlotInventaire();

    private Camera3D _camera;
    private RayCast3D _rayon;
    private Camera3D _cameraFps;
    private Camera3D _cameraTps;
    private RayCast3D _rayonFps;
    private RayCast3D _rayonTps;
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
    /// <summary>Locomotion sol : <see cref="AnimationNodeBlendSpace1D"/> Idleâ†”Marche via <c>blend_position</c> (Ã©vite le patinage Idle/Marche).</summary>
    private bool _animationTreeUtiliseBlendDeplacement;
    private const string NomEtatDeplacementBlend = "Deplacement";
    private const string ParamBlendDeplacementLocomotion = "parameters/Deplacement/blend_position";
    private const float DureeTamponSautSecondes = 0.28f;
    /// <summary>Capsule de rÃ©fÃ©rence dans la scÃ¨ne (souvent dÃ©sactivÃ©e) : bas local utilisÃ© pour aligner les pieds du mesh.</summary>
    private const string NomCollisionReferencePieds = "CollisionShape3D";
    [Export] public Vector3 OffsetAimantMainDroiteFpsLocal { get; set; } = new Vector3(0.42f, -0.25f, -0.26f);
    [Export] public Color CouleurPeauHumain { get; set; } = new Color(0.84f, 0.69f, 0.58f, 1f);
    [Export] public Texture2D TexturePeauHumain { get; set; }
    [Export] public Color CouleurSousVetementHumain { get; set; } = new Color(0.19f, 0.22f, 0.27f, 1f);
    [Export] public Texture2D TextureSousVetementHumain { get; set; }
    [Export] public float AvanceCameraFpsMetres { get; set; } = 0.20f;
    [Export] public Vector3 OffsetCeintureEquipeLocal { get; set; } = new Vector3(0f, -0.04f, -0.01f);
    [Export] public Vector3 RotationCeintureEquipeDeg { get; set; } = Vector3.Zero;
    [Export] public Vector3 OffsetSacDosEquipeLocal { get; set; } = new Vector3(0f, 0.18f, -0.22f);
    [Export] public Vector3 RotationSacDosEquipeDeg { get; set; } = new Vector3(0f, 90f, 0f);
    private static readonly Vector3 PositionObjetViewmodelFps = new Vector3(0.30f, -0.22f, -0.86f);
    private static readonly Vector3 RotationObjetViewmodelFpsDeg = new Vector3(10f, 154f, -10f);
    private static readonly Vector3 PositionObjetMainDefaut = new Vector3(0.035f, -0.01f, 0.065f);
    private static readonly Vector3 RotationObjetMainDefautDeg = new Vector3(8f, 92f, -16f);
    /// <summary>Orientation Mixamo -> Godot : correction latÃ©rale standard.</summary>
    private const float YawRigMixamoVersGodotDeg = 180f;
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
    private Texture2D _texturePeauProcedurale;
    private Texture2D _textureSousVetementProcedurale;
    private Gestionnaire_Monde _gestionnaireMonde;
    private Panel _slotGauche;
    private Panel _slotDroite;
    private Label _lblHudNomMainG;
    private Label _lblHudNomMainD;
    private MeshInstance3D _objetEnMain;
    private const string MetaSignatureDague105 = "SigDague105";
    private const string MetaSignatureHachette106 = "SigHachette106";
    private const string MetaSignaturePelle107 = "SigPelle107";
    private const string MetaSignaturePioche108 = "SigPioche108";
    private const string MetaSignatureAtelier200 = "SigAtelier200";
    private const string MetaSignatureCorde20 = "SigCorde20";
    private const string MetaSignatureTissu21 = "SigTissu21";
    private const string MetaSignatureCeinture102 = "SigCeinture102";
    private const string MetaSignatureCeinture104 = "SigCeinture104";
    private const string MetaSignaturePochette103 = "SigPochette103";
    private const string MetaSignatureSac101 = "SigSac101";
    private const string MetaSignatureRack109 = "SigRack109";
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
    private int _pvTete;
    private int _pvTorse;
    private int _pvBrasGauche;
    private int _pvBrasDroit;
    private int _pvJambeGauche;
    private int _pvJambeDroite;
    private readonly SectionSanteCorps[] _cacheSanteCorps = new SectionSanteCorps[6];
    private readonly Dictionary<string, ulong> _futureStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Force"] = 0UL,
        ["Dextiriter"] = 0UL
    };
    private readonly Dictionary<string, UInt128> _futureStateXp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Force"] = 0UL,
        ["Dextiriter"] = 0UL
    };
    private readonly Dictionary<string, ulong> _metiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bucheron"] = 0UL,
        ["Traisage"] = 0UL
    };
    private readonly Dictionary<string, UInt128> _metierXp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bucheron"] = 0UL,
        ["Traisage"] = 0UL
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

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        InitialiserSanteCorps();

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
        _camera = _cameraFps;
        _rayon = _rayonFps;
        _rayonFps.TargetPosition = new Vector3(0f, 0f, -12f);
        _rayonFps.CollisionMask = 0xFFFFFFFF; // Toutes les couches (sol AAA = layer 1, objets, eauâ€¦)
        _rayonFps.AddException(this); // Ne pas toucher le joueur (sinon le "minage" ne vise pas le sol)
        // MÃªme couche / masque que les corps statiques terrain (Monde_Client PhysicsServer3D layer 1).
        CollisionLayer = 1u;
        CollisionMask = 1u;
        // Sol voxel irrÃ©gulier : snap modÃ©rÃ© + marge rÃ©duite pour Ã©viter le pompage vertical.
        FloorSnapLength = 0.32f;
        SafeMargin = 0.06f;
        FloorMaxAngle = Mathf.DegToRad(52f);
        ConstruireHitboxesCompositeJoueur();
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
        InsererNomsAuDessusSlotsHud();

        CreerPreviewsInventaire3D();

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
            CallDeferred(nameof(AjusterRacineMenuAnatomieViewport));
            if (GetViewport() != null)
                GetViewport().SizeChanged += OnViewportTailleMenuAnatomie;
        }
    }

    private void InitialiserSanteCorps()
    {
        _pvTete = 80;
        _pvTorse = 180;
        _pvBrasGauche = 110;
        _pvBrasDroit = 110;
        _pvJambeGauche = 140;
        _pvJambeDroite = 140;
    }

    private static string NormaliserCleSectionCorps(string cleSection)
    {
        if (string.IsNullOrWhiteSpace(cleSection))
            return SectionCorpsTorse;

        string cle = cleSection.Trim().ToLowerInvariant();
        if (cle.Contains("hitboxtete") || cle.Contains("tete") || cle.Contains("head"))
            return SectionCorpsTete;
        if (cle.Contains("hitboxcorps") || cle.Contains("torse") || cle.Contains("corps") || cle.Contains("chest"))
            return SectionCorpsTorse;
        if (cle.Contains("hitboxbrasg") || cle.Contains("brasg") || cle.Contains("bras_g") || cle.Contains("leftarm"))
            return SectionCorpsBrasGauche;
        if (cle.Contains("hitboxbrasd") || cle.Contains("brasd") || cle.Contains("bras_d") || cle.Contains("rightarm"))
            return SectionCorpsBrasDroit;
        if (cle.Contains("hitboxjambeg") || cle.Contains("jambeg") || cle.Contains("jambe_g") || cle.Contains("leftleg"))
            return SectionCorpsJambeGauche;
        if (cle.Contains("hitboxjambed") || cle.Contains("jambed") || cle.Contains("jambe_d") || cle.Contains("rightleg"))
            return SectionCorpsJambeDroite;
        return SectionCorpsTorse;
    }

    private static int ObtenirPvMaxSectionCorps(string cleSection)
    {
        return cleSection switch
        {
            SectionCorpsTete => 80,
            SectionCorpsTorse => 180,
            SectionCorpsBrasGauche => 110,
            SectionCorpsBrasDroit => 110,
            SectionCorpsJambeGauche => 140,
            SectionCorpsJambeDroite => 140,
            _ => 180
        };
    }

    public void AppliquerDegatsSectionCorps(string cleSection, int degats)
    {
        if (degats <= 0)
            return;

        string section = NormaliserCleSectionCorps(cleSection);
        switch (section)
        {
            case SectionCorpsTete:
                _pvTete = Mathf.Max(0, _pvTete - degats);
                break;
            case SectionCorpsBrasGauche:
                _pvBrasGauche = Mathf.Max(0, _pvBrasGauche - degats);
                break;
            case SectionCorpsBrasDroit:
                _pvBrasDroit = Mathf.Max(0, _pvBrasDroit - degats);
                break;
            case SectionCorpsJambeGauche:
                _pvJambeGauche = Mathf.Max(0, _pvJambeGauche - degats);
                break;
            case SectionCorpsJambeDroite:
                _pvJambeDroite = Mathf.Max(0, _pvJambeDroite - degats);
                break;
            default:
                _pvTorse = Mathf.Max(0, _pvTorse - degats);
                break;
        }
        RafraichirHUD();
    }

    public void SoignerSectionCorps(string cleSection, int pointsSoin)
    {
        if (pointsSoin <= 0)
            return;

        string section = NormaliserCleSectionCorps(cleSection);
        switch (section)
        {
            case SectionCorpsTete:
                _pvTete = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvTete + pointsSoin);
                break;
            case SectionCorpsBrasGauche:
                _pvBrasGauche = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvBrasGauche + pointsSoin);
                break;
            case SectionCorpsBrasDroit:
                _pvBrasDroit = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvBrasDroit + pointsSoin);
                break;
            case SectionCorpsJambeGauche:
                _pvJambeGauche = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvJambeGauche + pointsSoin);
                break;
            case SectionCorpsJambeDroite:
                _pvJambeDroite = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvJambeDroite + pointsSoin);
                break;
            default:
                _pvTorse = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvTorse + pointsSoin);
                break;
        }
        RafraichirHUD();
    }

    public IReadOnlyList<SectionSanteCorps> ObtenirEtatSanteCorps()
    {
        _cacheSanteCorps[0] = new SectionSanteCorps(SectionCorpsTete, "Tete", "Os + chair", _pvTete, ObtenirPvMaxSectionCorps(SectionCorpsTete));
        _cacheSanteCorps[1] = new SectionSanteCorps(SectionCorpsTorse, "Torse", "Os + chair", _pvTorse, ObtenirPvMaxSectionCorps(SectionCorpsTorse));
        _cacheSanteCorps[2] = new SectionSanteCorps(SectionCorpsBrasGauche, "Bras gauche", "Os + chair", _pvBrasGauche, ObtenirPvMaxSectionCorps(SectionCorpsBrasGauche));
        _cacheSanteCorps[3] = new SectionSanteCorps(SectionCorpsBrasDroit, "Bras droit", "Os + chair", _pvBrasDroit, ObtenirPvMaxSectionCorps(SectionCorpsBrasDroit));
        _cacheSanteCorps[4] = new SectionSanteCorps(SectionCorpsJambeGauche, "Jambe gauche", "Os + chair", _pvJambeGauche, ObtenirPvMaxSectionCorps(SectionCorpsJambeGauche));
        _cacheSanteCorps[5] = new SectionSanteCorps(SectionCorpsJambeDroite, "Jambe droite", "Os + chair", _pvJambeDroite, ObtenirPvMaxSectionCorps(SectionCorpsJambeDroite));
        return _cacheSanteCorps;
    }

    public float ObtenirRatioSanteGlobaleCorps()
    {
        int pvActuels = _pvTete + _pvTorse + _pvBrasGauche + _pvBrasDroit + _pvJambeGauche + _pvJambeDroite;
        int pvMax = ObtenirPvMaxSectionCorps(SectionCorpsTete)
            + ObtenirPvMaxSectionCorps(SectionCorpsTorse)
            + ObtenirPvMaxSectionCorps(SectionCorpsBrasGauche)
            + ObtenirPvMaxSectionCorps(SectionCorpsBrasDroit)
            + ObtenirPvMaxSectionCorps(SectionCorpsJambeGauche)
            + ObtenirPvMaxSectionCorps(SectionCorpsJambeDroite);
        if (pvMax <= 0)
            return 0f;
        return pvActuels / (float)pvMax;
    }

    private void ConstruireRigCameraTps()
    {
        _pivotCameraTps = new Node3D
        {
            Name = "CameraPivotTPS",
            Position = new Vector3(0f, 1.55f, 0f),
            Rotation = new Vector3(_pitchCamera, 0f, 0f)
        };
        AddChild(_pivotCameraTps);

        _brasCameraTps = new SpringArm3D
        {
            Name = "SpringArmTPS",
            SpringLength = 3.35f,
            Margin = 0.08f,
            CollisionMask = 0xFFFFFFFF,
            Shape = new SphereShape3D { Radius = 0.2f }
        };
        _pivotCameraTps.AddChild(_brasCameraTps);

        _cameraTps = new Camera3D
        {
            Name = "CameraTPS",
            Current = false,
            Fov = 74f,
            Near = 0.03f,
            Far = 1600f
        };
        _brasCameraTps.AddChild(_cameraTps);

        _rayonTps = new RayCast3D
        {
            Name = "RayCastTPS",
            TargetPosition = new Vector3(0f, 0f, -14f),
            CollisionMask = 0xFFFFFFFF,
            Enabled = true
        };
        _cameraTps.AddChild(_rayonTps);
        _rayonTps.AddException(this);
    }

    private int TrouverOsParMotifs(Skeleton3D sk, params string[][] motifs)
    {
        if (sk == null) return -1;
        for (int m = 0; m < motifs.Length; m++)
        {
            string[] tokens = motifs[m];
            for (int i = 0; i < sk.GetBoneCount(); i++)
            {
                string nom = sk.GetBoneName(i).ToString().ToLowerInvariant();
                bool ok = true;
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (!nom.Contains(tokens[t])) { ok = false; break; }
                }
                if (ok) return i;
            }
        }
        return -1;
    }

    private int TrouverOsParNomsAlternatifs(Skeleton3D sk, params string[] motifsOuNoms)
    {
        if (sk == null) return -1;
        for (int i = 0; i < sk.GetBoneCount(); i++)
        {
            string nom = sk.GetBoneName(i).ToString().ToLowerInvariant();
            for (int m = 0; m < motifsOuNoms.Length; m++)
            {
                string p = motifsOuNoms[m].ToLowerInvariant();
                if (nom.Contains(p)) return i;
            }
        }
        return -1;
    }

    private int TrouverRacineIkDepuisMainDroite(int osMainDroite)
    {
        if (_squeletteHumain == null || osMainDroite < 0) return -1;
        int parent = _squeletteHumain.GetBoneParent(osMainDroite);
        int fallback = -1;
        while (parent >= 0)
        {
            if (parent < osMainDroite && fallback < 0)
                fallback = parent;
            if (parent < osMainDroite)
            {
                string nom = _squeletteHumain.GetBoneName(parent).ToString().ToLowerInvariant();
                if (nom.Contains("forearm") || nom.Contains("lowerarm") || nom.Contains("upperarm") || nom.Contains("arm") || nom.Contains("shoulder") || nom.Contains("clavicle"))
                    return parent;
            }
            parent = _squeletteHumain.GetBoneParent(parent);
        }
        return fallback;
    }

    private void InitialiserSqueletteHumain()
    {
        _squeletteHumain = TrouverPremierNoeudDeType<Skeleton3D>(_rigHumain);
        if (_squeletteHumain == null) return;

        _osBrasDroit = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "arm" }, new[] { "r", "upperarm" });
        _osAvantBrasDroit = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "forearm" }, new[] { "right", "lowerarm" }, new[] { "r", "forearm" });
        _osMainDroite = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "hand" }, new[] { "r", "hand" });
        _osEpauleDroite = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "shoulder" }, new[] { "r", "shoulder" }, new[] { "clavicle", "right" });
        int osMainD = TrouverOsParMotifs(_squeletteHumain, new[] { "right", "hand" }, new[] { "r", "hand" });
        int osMainG = TrouverOsParMotifs(_squeletteHumain, new[] { "left", "hand" }, new[] { "l", "hand" });

        if (osMainD >= 0)
        {
            _attacheMainDroiteTps = new BoneAttachment3D { Name = "AttacheMainDroiteTPS", BoneIdx = osMainD };
            _squeletteHumain.AddChild(_attacheMainDroiteTps);
        }
        if (osMainG >= 0)
        {
            _attacheMainGaucheTps = new BoneAttachment3D { Name = "AttacheMainGaucheTPS", BoneIdx = osMainG };
            _squeletteHumain.AddChild(_attacheMainGaucheTps);
        }

        InitialiserAttachesEquipementsCorps();

        Node3D attacheActive = _attacheMainDroiteTps ?? _attacheMainGaucheTps;
        if (attacheActive != null)
        {
            _objetEnMain = new MeshInstance3D
            {
                Name = "ObjetEnMain",
                Position = new Vector3(0.035f, -0.01f, 0.065f),
                RotationDegrees = new Vector3(8f, 92f, -16f),
                Scale = Vector3.One * 0.9f
            };
            attacheActive.AddChild(_objetEnMain);
        }

        if (_ikBrasDroitFps != null && GodotObject.IsInstanceValid(_ikBrasDroitFps))
        {
            _ikBrasDroitFps.Stop();
            _ikBrasDroitFps.QueueFree();
            _ikBrasDroitFps = null;
        }
        if (_aimantIkMainDroite != null && GodotObject.IsInstanceValid(_aimantIkMainDroite))
        {
            _aimantIkMainDroite.QueueFree();
            _aimantIkMainDroite = null;
        }

        RafraichirVisuelsEquipementsCorps();

        if (_cameraFps != null && _osMainDroite >= 0)
        {
            _aimantIkMainDroite = new Marker3D { Name = "AimantMainDroiteIK" };
            _cameraFps.AddChild(_aimantIkMainDroite);
            _aimantIkMainDroite.Position = OffsetAimantMainDroiteFpsLocal;

            int osRacineIk = TrouverRacineIkDepuisMainDroite(_osMainDroite);
            if (osRacineIk < 0 || osRacineIk >= _osMainDroite)
            {
                GD.PrintErr($"ZERO-K : IK bras droit ignorÃ© â€” chaÃ®ne invalide (root={osRacineIk}, tip={_osMainDroite}).");
                return;
            }

            _ikBrasDroitFps = new SkeletonIK3D { Name = "IK_BrasDroitFPS" };
            _ikBrasDroitFps.RootBone = _squeletteHumain.GetBoneName(osRacineIk);
            _ikBrasDroitFps.TipBone = _squeletteHumain.GetBoneName(_osMainDroite);
            _squeletteHumain.AddChild(_ikBrasDroitFps);
            _ikBrasDroitFps.TargetNode = _ikBrasDroitFps.GetPathTo(_aimantIkMainDroite);
            _ikBrasDroitFps.Influence = 0f;
            _ikBrasDroitFps.Start();
        }
    }

    private void InitialiserAttachesEquipementsCorps()
    {
        if (_squeletteHumain == null) return;

        if (_attacheCeintureCorps != null && GodotObject.IsInstanceValid(_attacheCeintureCorps))
            _attacheCeintureCorps.QueueFree();
        if (_attacheDosCorps != null && GodotObject.IsInstanceValid(_attacheDosCorps))
            _attacheDosCorps.QueueFree();
        _attacheCeintureCorps = null;
        _attacheDosCorps = null;
        _supportVisuelCeinture = null;
        _supportVisuelSacDos = null;
        _signatureVisuelleCeintureEquipe = int.MinValue;
        _signatureVisuelleSacDosEquipe = int.MinValue;

        int osCeinture = TrouverOsParNomsAlternatifs(_squeletteHumain, "hips", "pelvis", "hanche", "bassin", "spine");
        int osDos = TrouverOsParNomsAlternatifs(_squeletteHumain, "spine2", "spine1", "spine", "chest", "upperchest", "torso");
        if (osDos < 0) osDos = osCeinture;

        if (osCeinture >= 0)
        {
            _attacheCeintureCorps = new BoneAttachment3D { Name = "AttacheCeintureCorps", BoneIdx = osCeinture };
            _squeletteHumain.AddChild(_attacheCeintureCorps);
            _supportVisuelCeinture = new Node3D { Name = "SupportVisuelCeinture" };
            _attacheCeintureCorps.AddChild(_supportVisuelCeinture);
        }
        else if (_rigHumain != null)
        {
            // Fallback si le rig importé n'expose pas d'os pelvis/hips correctement nommé.
            _supportVisuelCeinture = new Node3D { Name = "SupportVisuelCeintureFallback", Position = new Vector3(0f, -0.72f, 0f) };
            _rigHumain.AddChild(_supportVisuelCeinture);
        }
        if (osDos >= 0)
        {
            _attacheDosCorps = new BoneAttachment3D { Name = "AttacheDosCorps", BoneIdx = osDos };
            _squeletteHumain.AddChild(_attacheDosCorps);
            _supportVisuelSacDos = new Node3D { Name = "SupportVisuelSacDos" };
            _attacheDosCorps.AddChild(_supportVisuelSacDos);
        }
        else if (_rigHumain != null)
        {
            // Fallback dos sans os explicite.
            _supportVisuelSacDos = new Node3D { Name = "SupportVisuelSacDosFallback", Position = new Vector3(0f, -0.45f, -0.16f) };
            _rigHumain.AddChild(_supportVisuelSacDos);
        }
    }

    private void RafraichirVisuelsEquipementsCorps()
    {
        if (_supportVisuelCeinture != null && GodotObject.IsInstanceValid(_supportVisuelCeinture))
        {
            _supportVisuelCeinture.Position = OffsetCeintureEquipeLocal;
            _supportVisuelCeinture.RotationDegrees = RotationCeintureEquipeDeg;
        }
        if (_supportVisuelSacDos != null && GodotObject.IsInstanceValid(_supportVisuelSacDos))
        {
            _supportVisuelSacDos.Position = OffsetSacDosEquipeLocal;
            _supportVisuelSacDos.RotationDegrees = RotationSacDosEquipeDeg;
        }

        RafraichirVisuelCeintureEquipe();
        RafraichirVisuelSacDosEquipe();
    }

    private void RafraichirVisuelCeintureEquipe()
    {
        if (_supportVisuelCeinture == null || !GodotObject.IsInstanceValid(_supportVisuelCeinture))
            return;

        SlotInventaire ceinture = EquipementCeinture;
        int sig = ceinture.ID switch
        {
            IdObjetCeinturePoches => SignatureSlotCeinture102(ceinture),
            IdObjetCeintureSacoches => SignatureSlotCeinture104(ceinture),
            IdObjetPochetteTier0 => SignatureSlotPochette103(ceinture),
            _ => -1
        };
        bool modelePresent = _supportVisuelCeinture.FindChild("ModeleArme", true, false) != null;
        if (sig < 0)
        {
            if (_signatureVisuelleCeintureEquipe != -1 || modelePresent)
                NettoyerModelesEnfants(_supportVisuelCeinture);
            _signatureVisuelleCeintureEquipe = -1;
            return;
        }
        if (sig == _signatureVisuelleCeintureEquipe && modelePresent)
            return;

        if (ceinture.ID == IdObjetCeintureSacoches)
            InstancierModeleCeintureSacoches(_supportVisuelCeinture, ceinture, 0.24f);
        else if (ceinture.ID == IdObjetPochetteTier0)
            InstancierModelePochetteTier0(_supportVisuelCeinture, ceinture, 0.18f);
        else
            InstancierModeleCeinturePoches(_supportVisuelCeinture, ceinture, 0.22f);
        _signatureVisuelleCeintureEquipe = sig;
    }

    private void RafraichirVisuelSacDosEquipe()
    {
        if (_supportVisuelSacDos == null || !GodotObject.IsInstanceValid(_supportVisuelSacDos))
            return;

        SlotInventaire sac = EquipementSacDos;
        int sig = sac.ID == IdObjetSacTier0 ? SignatureSlotSac101(sac) : -1;
        bool modelePresent = _supportVisuelSacDos.FindChild("ModeleArme", true, false) != null;
        if (sig < 0)
        {
            if (_signatureVisuelleSacDosEquipe != -1 || modelePresent)
                NettoyerModelesEnfants(_supportVisuelSacDos);
            _signatureVisuelleSacDosEquipe = -1;
            return;
        }
        if (sig == _signatureVisuelleSacDosEquipe && modelePresent)
            return;

        InstancierModeleSacTier0(_supportVisuelSacDos, sac, 0.26f);
        _signatureVisuelleSacDosEquipe = sig;
    }

    /// <summary>Cou puis tÃªte (sans HeadTop) : camÃ©ra FPS sur le mÃªme squelette que la vue TPS.</summary>
    private int TrouverOsSupportCameraFps()
    {
        if (_squeletteHumain == null) return -1;
        int cou = TrouverOsParMotifs(_squeletteHumain, new[] { "neck" });
        if (cou < 0) cou = TrouverOsParNomsAlternatifs(_squeletteHumain, "neck", "cou");
        if (cou >= 0) return cou;
        for (int i = 0; i < _squeletteHumain.GetBoneCount(); i++)
        {
            string nom = _squeletteHumain.GetBoneName(i).ToString().ToLowerInvariant();
            if (nom.Contains("headtop")) continue;
            if (nom.Contains("head") || nom.Contains("tete")) return i;
        }
        return -1;
    }

    private void BrancherCameraFpsSurSquelette()
    {
        if (_cameraFps == null || _squeletteHumain == null) return;
        if (_attacheCameraFps != null && GodotObject.IsInstanceValid(_attacheCameraFps))
        {
            _attacheCameraFps.QueueFree();
            _attacheCameraFps = null;
        }

        // CamÃ©ra FPS volontairement dÃ©solidarisÃ©e du squelette pour Ã©viter les secousses d'animation.
        // RÃ©fÃ©rence visage : lÃ©gÃ¨rement en avant et un peu sous la ligne des yeux (proche bouche).
        if (_cameraFps.GetParent() != this)
            _cameraFps.Reparent(this);
        _cameraFps.Position = new Vector3(0f, 0.56f, -0.07f - Mathf.Max(0f, AvanceCameraFpsMetres));
        _pitchCameraBaseRad = 0f;
        _yawCorrectionCameraFpsRad = 0f;
        _cameraFps.Rotation = new Vector3(_pitchCameraBaseRad + _pitchCamera, _yawCorrectionCameraFpsRad, 0f);
        _cameraFps.Near = 0.12f;
    }

    private static T TrouverPremierNoeudDeType<T>(Node racine) where T : Node
    {
        if (racine == null) return null;
        if (racine is T t) return t;
        foreach (Node enfant in racine.GetChildren())
        {
            T trouve = TrouverPremierNoeudDeType<T>(enfant);
            if (trouve != null) return trouve;
        }
        return null;
    }

    /// <summary>Le GLB peut contenir un AnimationPlayer interne : on le coupe pour que seul <see cref="NomNoeudAnimationPlayerLocomotion"/> pilote le rig.</summary>
    private static void DesactiverAutresAnimationPlayers(Node racine, AnimationPlayer garder)
    {
        if (racine == null) return;
        foreach (Node enfant in racine.GetChildren())
        {
            if (enfant is AnimationPlayer ap && ap != garder)
                ap.ProcessMode = ProcessModeEnum.Disabled;
            DesactiverAutresAnimationPlayers(enfant, garder);
        }
    }

    private void SecuriserMateriauxModeleHumain(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            Color peau = CouleurPeauHumain;
            string nom = mi.Name.ToString().ToLowerInvariant();
            if (AppliquerMateriauxParSurfaceHumain(mi, peau))
            {
                mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
                foreach (Node c in n.GetChildren())
                    SecuriserMateriauxModeleHumain(c);
                return;
            }

            bool estSousVetement = EstNomMailleSousVetementHumain(nom);
            bool estPeau = EstNomMaillePeauHumain(nom);
            if (estSousVetement)
            {
                mi.MaterialOverride = CreerMateriauHumain(TextureSousVetementHumain ?? ObtenirTextureSousVetementProcedurale(), CouleurSousVetementHumain, 0.92f);
            }
            else if (estPeau)
            {
                mi.MaterialOverride = CreerMateriauHumain(TexturePeauHumain ?? ObtenirTexturePeauProcedurale(), peau, 0.88f);
            }
            else if (nom.Contains("eye"))
            {
                mi.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.14f, 0.2f, 0.25f, 1f),
                    Roughness = 0.6f,
                    Metallic = 0f
                };
            }
            else if (nom.Contains("lip") || nom.Contains("mouth"))
            {
                mi.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.6f, 0.34f, 0.34f, 1f),
                    Roughness = 0.82f,
                    Metallic = 0f
                };
            }
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }
        foreach (Node c in n.GetChildren())
            SecuriserMateriauxModeleHumain(c);
    }

    /// <summary>Si le mesh contient plusieurs surfaces, on force la convention: 0=peau, 1=sous-vêtement, autres=peau.</summary>
    private bool AppliquerMateriauxParSurfaceHumain(MeshInstance3D mi, Color peau)
    {
        if (mi?.Mesh == null) return false;
        int surfaces = mi.Mesh.GetSurfaceCount();
        if (surfaces <= 1) return false;

        Texture2D texPeau = TexturePeauHumain ?? ObtenirTexturePeauProcedurale();
        Texture2D texSous = TextureSousVetementHumain ?? ObtenirTextureSousVetementProcedurale();
        for (int i = 0; i < surfaces; i++)
        {
            bool estSousVetement = i == 1;
            mi.SetSurfaceOverrideMaterial(i, CreerMateriauHumain(
                estSousVetement ? texSous : texPeau,
                estSousVetement ? CouleurSousVetementHumain : peau,
                estSousVetement ? 0.92f : 0.88f));
        }

        // Important: garder null sinon Godot écrase les matériaux de surface.
        mi.MaterialOverride = null;
        return true;
    }

    private static bool ContientUnMotCle(string nomLower, params string[] mots)
    {
        if (string.IsNullOrEmpty(nomLower)) return false;
        foreach (string mot in mots)
        {
            if (!string.IsNullOrEmpty(mot) && nomLower.Contains(mot))
                return true;
        }
        return false;
    }

    private static bool EstNomMailleSousVetementHumain(string nomLower)
    {
        return ContientUnMotCle(nomLower,
            "underwear", "under", "brief", "short", "panty", "culotte", "slip", "boxer", "sous", "vetement", "clothe", "cloth", "pants");
    }

    private static bool EstNomMaillePeauHumain(string nomLower)
    {
        return ContientUnMotCle(nomLower, "body", "skin", "head", "face", "arm", "leg", "hand", "foot", "torso", "neck", "ear", "nose");
    }

    private static StandardMaterial3D CreerMateriauHumain(Texture2D texture, Color couleur, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = couleur,
            Roughness = roughness,
            Metallic = 0f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
    }

    private Texture2D ObtenirTexturePeauProcedurale()
    {
        if (_texturePeauProcedurale != null) return _texturePeauProcedurale;
        Color c0 = CouleurPeauHumain;
        Color c1 = CouleurPeauHumain.Darkened(0.06f);
        Color c2 = CouleurPeauHumain.Lightened(0.05f);
        _texturePeauProcedurale = CreerTextureChecker4x4(c0, c1, c2, c1);
        return _texturePeauProcedurale;
    }

    private Texture2D ObtenirTextureSousVetementProcedurale()
    {
        if (_textureSousVetementProcedurale != null) return _textureSousVetementProcedurale;
        Color baseCol = CouleurSousVetementHumain;
        Color stripe = baseCol.Lightened(0.16f);
        _textureSousVetementProcedurale = CreerTextureChecker4x4(baseCol, stripe, baseCol.Darkened(0.1f), stripe.Darkened(0.08f));
        return _textureSousVetementProcedurale;
    }

    private static Texture2D CreerTextureChecker4x4(Color a, Color b, Color c, Color d)
    {
        Image img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                bool left = x < 2;
                bool top = y < 2;
                Color col = (left, top) switch
                {
                    (true, true) => a,
                    (false, true) => b,
                    (true, false) => c,
                    _ => d
                };
                img.SetPixel(x, y, col);
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private static bool EstNomMailleTeteOuCouPourFps(string nomLower)
    {
        if (string.IsNullOrEmpty(nomLower) || nomLower.Contains("headtop")) return false;
        if (nomLower.Contains("head") || nomLower.Contains("tete")) return true;
        if (nomLower.Contains("hair") || nomLower.Contains("scalp") || nomLower.Contains("cheveu")) return true;
        if (nomLower.Contains("face") || nomLower.Contains("visage")) return true;
        if (nomLower.Contains("skull") || nomLower.Contains("crane")) return true;
        if (nomLower.Contains("eye") || nomLower.Contains("oeil") || nomLower.Contains("lash") || nomLower.Contains("brow") || nomLower.Contains("tear"))
            return true;
        if (nomLower.Contains("teeth") || nomLower.Contains("tooth") || nomLower.Contains("dent") || nomLower.Contains("tongue") || nomLower.Contains("langue"))
            return true;
        if (nomLower.Contains("lip") || nomLower.Contains("mouth") || nomLower.Contains("bouche") || nomLower.Contains("gum")) return true;
        if (nomLower.Contains("ear") || nomLower.Contains("oreille")) return true;
        if (nomLower.Contains("nose") || nomLower.Contains("nez")) return true;
        if (nomLower.Contains("neck") || nomLower.Contains("cou") && !nomLower.Contains("accou")) return true;
        if (nomLower.Contains("beard") || nomLower.Contains("barbe") || nomLower.Contains("mustache") || nomLower.Contains("moustache"))
            return true;
        return false;
    }

    /// <summary>Place tÃªte/cou sur le calque 2 : la camÃ©ra FPS (cull 1) ne les dessine pas â€” Ã©vite lâ€™intÃ©rieur du crÃ¢ne / cheveux.</summary>
    private static void AssignerCalquesTetePourVueFps(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            string l = n.Name.ToString().ToLowerInvariant();
            if (EstNomMailleTeteOuCouPourFps(l))
                mi.Layers = CalqueRenduTeteFpsCachee;
        }
        foreach (Node c in n.GetChildren())
            AssignerCalquesTetePourVueFps(c);
    }

    private void AppliquerCullMasksCamerasJoueur()
    {
        if (_cameraFps != null)
            _cameraFps.CullMask = CalqueRenduCorpsEtMondeFps;
        if (_cameraTps != null)
            _cameraTps.CullMask = uint.MaxValue;
    }

    private void DetecterClipsAnimationHumain()
    {
        _animationHumain = _rigHumain?.GetNodeOrNull<AnimationPlayer>(NomNoeudAnimationPlayerLocomotion)
            ?? TrouverPremierNoeudDeType<AnimationPlayer>(_rigHumain);
        _clipIdleHumain = _clipWalkHumain = _clipRunHumain = _clipJumpHumain = "";
        _fallbackAnimProcedural = true;
        if (_animationHumain == null) return;

        var noms = _animationHumain.GetAnimationList();
        if (noms == null || noms.Length == 0)
            return;

        if (_animationHumain.HasAnimationLibrary(BibliothequeLocomotionMixamo))
        {
            var libLoc = _animationHumain.GetAnimationLibrary(BibliothequeLocomotionMixamo);
            string Pref(string clip) => $"{BibliothequeLocomotionMixamo}/{clip}";
            if (libLoc.HasAnimation("Idle"))
                _clipIdleHumain = Pref("Idle");
            if (libLoc.HasAnimation("Marche"))
                _clipWalkHumain = _clipRunHumain = Pref("Marche");
            if (libLoc.HasAnimation("Saut"))
                _clipJumpHumain = Pref("Saut");
        }

        for (int i = 0; i < noms.Length; i++)
        {
            string nom = noms[i];
            string l = nom.ToLowerInvariant();
            if (string.IsNullOrEmpty(_clipIdleHumain) && (l.Contains("idle") || l.Contains("attente") || l.Contains("stand") || l.Contains("breathing")))
                _clipIdleHumain = nom;
            if (string.IsNullOrEmpty(_clipWalkHumain) && (l.Contains("walk") || l.Contains("marche") || l.Contains("jog") || l.Contains("stride")))
                _clipWalkHumain = nom;
            if (string.IsNullOrEmpty(_clipRunHumain) && (l.Contains("run") || l.Contains("course") || l.Contains("sprint")))
                _clipRunHumain = nom;
            if (string.IsNullOrEmpty(_clipJumpHumain) && (l.Contains("jump") || l.Contains("saut") || l.Contains("fall") || l.Contains("air")))
                _clipJumpHumain = nom;
        }

        if (string.IsNullOrEmpty(_clipIdleHumain))
            _clipIdleHumain = noms[0];

        if (string.IsNullOrEmpty(_clipWalkHumain))
            _clipWalkHumain = !string.IsNullOrEmpty(_clipRunHumain) ? _clipRunHumain : _clipIdleHumain;
        if (string.IsNullOrEmpty(_clipRunHumain))
            _clipRunHumain = _clipWalkHumain;

        _fallbackAnimProcedural = false;
        if (_playbackLocomotion == null && !string.IsNullOrEmpty(_clipIdleHumain))
            _animationHumain.Play(_clipIdleHumain);
    }

    private static Animation ExtrairePremiereAnimationDepuisJoueur(AnimationPlayer ap)
    {
        if (ap == null) return null;
        foreach (StringName nomLib in ap.GetAnimationLibraryList())
        {
            AnimationLibrary lib = ap.GetAnimationLibrary(nomLib);
            if (lib == null) continue;
            foreach (StringName nomAnim in lib.GetAnimationList())
            {
                Animation source = lib.GetAnimation(nomAnim);
                if (source != null)
                    return (Animation)source.Duplicate();
            }
        }
        return null;
    }

    private static void RemapperCheminsAnimationVersSqueletteHumain(Animation anim, string prefixeSqueletteFbx, string prefixeSqueletteHumain)
    {
        if (anim == null || string.IsNullOrEmpty(prefixeSqueletteFbx) || prefixeSqueletteHumain == null) return;
        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            string s = anim.TrackGetPath(i).ToString();
            if (s.StartsWith(prefixeSqueletteFbx, StringComparison.Ordinal))
                anim.TrackSetPath(i, new NodePath(prefixeSqueletteHumain + s.Substring(prefixeSqueletteFbx.Length)));
        }
    }

    /// <summary>Si le prÃ©fixe FBX ne matche pas (autre hiÃ©rarchie), recolle tout ce qui suit Â« Skeleton3D Â» au chemin du squelette sur le rig joueur.</summary>
    private static void RemapperCheminsAnimationParMarqueurSquelette(Animation anim, string cheminNoeudSqueletteHumain)
    {
        if (anim == null || string.IsNullOrEmpty(cheminNoeudSqueletteHumain)) return;
        const string marqueur = "Skeleton3D";
        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            string s = anim.TrackGetPath(i).ToString();
            int idx = s.IndexOf(marqueur, StringComparison.Ordinal);
            if (idx < 0) continue;
            string queue = s.Substring(idx + marqueur.Length);
            anim.TrackSetPath(i, new NodePath(cheminNoeudSqueletteHumain + queue));
        }
    }

    /// <summary>Charge imobile / Marcher / Jump depuis les FBX et les enregistre dans la bibliothÃ¨que Â« locomotion Â» du rig (sans passage par lâ€™Ã©diteur Â« Save to File Â»).</summary>
    private void FusionnerAnimationsFbxVersRigHumain()
    {
        if (_rigHumain == null || _squeletteHumain == null) return;

        _animationHumain = _rigHumain.GetNodeOrNull<AnimationPlayer>(NomNoeudAnimationPlayerLocomotion);
        if (_animationHumain == null)
        {
            _animationHumain = new AnimationPlayer { Name = NomNoeudAnimationPlayerLocomotion };
            _rigHumain.AddChild(_animationHumain);
            _rigHumain.MoveChild(_animationHumain, 0);
        }

        // Les pistes sont remappÃ©es relativement au parent du lecteur (HumainRigRoot).
        _animationHumain.RootNode = new NodePath("..");
        _animationHumain.ProcessMode = ProcessModeEnum.Always;
        _animationHumain.Active = true;
        DesactiverAutresAnimationPlayers(_rigHumain, _animationHumain);

        if (!_animationHumain.HasAnimationLibrary(BibliothequeLocomotionMixamo))
            _animationHumain.AddAnimationLibrary(BibliothequeLocomotionMixamo, new AnimationLibrary());

        AnimationLibrary libLoc = _animationHumain.GetAnimationLibrary(BibliothequeLocomotionMixamo);
        if (libLoc == null) return;

        Node racineCheminsJoueur = _animationHumain.GetParent() ?? _rigHumain;
        string prefixHum = racineCheminsJoueur.GetPathTo(_squeletteHumain).ToString();
        GD.Print($"ZERO-K : AnimationPlayer Â« {NomNoeudAnimationPlayerLocomotion} Â» â€” pistes ciblent le squelette via Â« {prefixHum} Â» (parent lecteur = {racineCheminsJoueur.Name}).");

        void FusionnerUneSceneFbx(string cheminScene, StringName nomClip)
        {
            if (libLoc.HasAnimation(nomClip)) return;
            var sc = GD.Load<PackedScene>(cheminScene);
            if (sc == null)
            {
                GD.PrintErr($"ZERO-K : scÃ¨ne FBX introuvable : {cheminScene}");
                return;
            }
            Node temp = sc.Instantiate();
            var apFbx = TrouverPremierNoeudDeType<AnimationPlayer>(temp);
            Skeleton3D skFbx = TrouverPremierNoeudDeType<Skeleton3D>(temp);
            if (apFbx == null || skFbx == null)
            {
                GD.PrintErr($"ZERO-K : pas dâ€™AnimationPlayer ou Skeleton3D dans {cheminScene}");
                temp.QueueFree();
                return;
            }
            Node racineCheminsFbx = apFbx.GetParent() ?? temp;
            string prefixFbx = racineCheminsFbx.GetPathTo(skFbx).ToString();
            Animation anim = ExtrairePremiereAnimationDepuisJoueur(apFbx);
            temp.QueueFree();
            if (anim == null)
            {
                GD.PrintErr($"ZERO-K : aucune animation dans {cheminScene}");
                return;
            }
            // Les FBX Mixamo arrivent souvent sans boucle explicite : Idle/Marche doivent boucler en continu.
            if (nomClip == "Idle" || nomClip == "Marche")
                anim.LoopMode = Animation.LoopModeEnum.Linear;
            RemapperCheminsAnimationVersSqueletteHumain(anim, prefixFbx, prefixHum);
            RemapperCheminsAnimationParMarqueurSquelette(anim, prefixHum);
            libLoc.AddAnimation(nomClip, anim);
            GD.Print($"ZERO-K : clip Â« {nomClip} Â» fusionnÃ© ({cheminScene}) FBX:{prefixFbx} â†’ joueur:{prefixHum} â†’ {BibliothequeLocomotionMixamo}/{nomClip}.");
        }

        FusionnerUneSceneFbx("res://Modeles/Animations/imobile.fbx", "Idle");
        FusionnerUneSceneFbx("res://Modeles/Animations/Marcher.fbx", "Marche");
        FusionnerUneSceneFbx("res://Modeles/Animations/Jump.fbx", "Saut");
    }

    private void ConfigurerAnimationTreeLocomotionHumain()
    {
        if (_animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain))
        {
            _animationTreeHumain.Active = false;
            _animationTreeHumain.QueueFree();
            _animationTreeHumain = null;
        }
        _playbackLocomotion = null;
        _dernierEtatLocomotionTree = "";
        _animationTreeContientSaut = false;
        _animationTreeUtiliseBlendDeplacement = false;
        _tentativesLecturePlaybackArbreLocomotion = 0;

        if (_animationHumain == null || _fallbackAnimProcedural) return;
        if (!_animationHumain.HasAnimationLibrary(BibliothequeLocomotionMixamo)) return;

        AnimationLibrary libLoc = _animationHumain.GetAnimationLibrary(BibliothequeLocomotionMixamo);
        if (libLoc == null || !libLoc.HasAnimation("Idle") || !libLoc.HasAnimation("Marche"))
            return;

        // ðŸ“– FIX CRITIQUE : Amputation volontaire de l'animation de saut dÃ©fectueuse.
        // La physique (Velocity.Y) fonctionnera toujours, mais le visuel restera sur Marche/Idle.
        _animationTreeContientSaut = false;
        var nomIdle = new StringName($"{BibliothequeLocomotionMixamo}/Idle");
        var nomMarche = new StringName($"{BibliothequeLocomotionMixamo}/Marche");

        var blendIdle = new AnimationNodeAnimation { Animation = nomIdle };
        var blendMarche = new AnimationNodeAnimation { Animation = nomMarche };
        var blendDeplacement = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
        blendDeplacement.AddBlendPoint(blendIdle, 0f);
        blendDeplacement.AddBlendPoint(blendMarche, 1f);

        var machine = new AnimationNodeStateMachine();
        machine.AddNode(NomEtatDeplacementBlend, blendDeplacement, new Vector2(240f, 120f));

        if (_animationTreeContientSaut)
        {
            var noeudSaut = new AnimationNodeAnimation { Animation = new StringName($"{BibliothequeLocomotionMixamo}/Saut") };
            machine.AddNode("Saut", noeudSaut, new Vector2(240f, 280f));
        }

        const float XfadeLocomotion = 0.12f;
        var depuisStart = new AnimationNodeStateMachineTransition
        {
            XfadeTime = XfadeLocomotion,
            SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate
        };
        machine.AddTransition("Start", NomEtatDeplacementBlend, depuisStart);

        if (_animationTreeContientSaut)
        {
            var versSaut = new AnimationNodeStateMachineTransition { XfadeTime = 0.08f };
            machine.AddTransition(NomEtatDeplacementBlend, "Saut", versSaut);
            var retourSol = new AnimationNodeStateMachineTransition { XfadeTime = 0.1f };
            machine.AddTransition("Saut", NomEtatDeplacementBlend, retourSol);
        }

        _animationTreeUtiliseBlendDeplacement = true;
        _animationTreeHumain = new AnimationTree { Name = "AnimationTreeLocomotion", ProcessMode = ProcessModeEnum.Always };
        _rigHumain.AddChild(_animationTreeHumain);
        _animationTreeHumain.TreeRoot = machine;
        _animationTreeHumain.AnimPlayer = _animationTreeHumain.GetPathTo(_animationHumain);
        _animationTreeHumain.Active = true;
        _playbackLocomotion = null;
        _dernierEtatLocomotionTree = "";
        Callable.From(ApresAnimationTreePretLocomotion).CallDeferred();
    }

    private void ApresAnimationTreePretLocomotion()
    {
        if (_animationTreeHumain == null || !GodotObject.IsInstanceValid(_animationTreeHumain) || _animationHumain == null)
            return;

        _animationTreeHumain.Active = true;
        _playbackLocomotion = ExtrairePlaybackMachineEtatAnimationTree();
        if (_playbackLocomotion == null)
        {
            if (++_tentativesLecturePlaybackArbreLocomotion > 15)
            {
                GD.PrintErr("ZERO-K : AnimationTree â€” Â« parameters/playback Â» introuvable. Lecture directe Idle sur AnimationPlayer.");
                _animationTreeHumain.QueueFree();
                _animationTreeHumain = null;
                _playbackLocomotion = null;
                if (!string.IsNullOrEmpty(_clipIdleHumain))
                    _animationHumain.Play(_clipIdleHumain, 0.08f);
                return;
            }

            Callable.From(ApresAnimationTreePretLocomotion).CallDeferred();
            return;
        }

        _tentativesLecturePlaybackArbreLocomotion = 0;
        _playbackLocomotion.Start(new StringName(NomEtatDeplacementBlend));
        _dernierEtatLocomotionTree = NomEtatDeplacementBlend;
    }

    private AnimationNodeStateMachinePlayback ExtrairePlaybackMachineEtatAnimationTree()
    {
        if (_animationTreeHumain == null) return null;
        Variant v = _animationTreeHumain.Get("parameters/playback");
        if (v.VariantType == Variant.Type.Nil) return null;
        return v.AsGodotObject() as AnimationNodeStateMachinePlayback;
    }

    private void InitialiserModeleHumainJoueur()
    {
        var capsuleVisuelle = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (capsuleVisuelle != null)
            capsuleVisuelle.Visible = false;

        // PrÃ©fÃ©rer le nÅ“ud liÃ© dans la scÃ¨ne (Joueur.tscn / monde_zero.tscn) pour que lâ€™Ã©diteur montre le GLB.
        _rigHumain = GetNodeOrNull<Node3D>("HumainRigRoot");
        if (_rigHumain == null)
        {
            PackedScene sceneHumain = GD.Load<PackedScene>("res://Modeles/Entites/Humain/humain.glb");
            if (sceneHumain == null)
            {
                GD.PrintErr("ZERO-K : ModÃ¨le joueur introuvable : res://Modeles/Entites/Humain/humain.glb (ajoute un enfant HumainRigRoot depuis humain.glb).");
                return;
            }

            _rigHumain = sceneHumain.Instantiate<Node3D>();
            _rigHumain.Name = "HumainRigRoot";
            AddChild(_rigHumain);
        }

        _rigHumain.Scale = Vector3.One * 1.3f; // rÃ©glage final demandÃ©

        _solCapsuleLocalY = CalculerBasCollisionLocalJoueur();
        float basPourPieds = CalculerBasPourAlignementPiedsDuMesh();
        float yRig = basPourPieds + HauteurPiedsSousPivotRigMixamo * _rigHumain.Scale.Y + DecalageYRigHumain;
        _rigHumain.Position = new Vector3(0f, yRig, 0f);

        Vector3 man = CorrectionManuelleEulerRigHumainDeg;
        _rigHumain.RotationDegrees = new Vector3(man.X, YawRigMixamoVersGodotDeg + man.Y, man.Z);

        SecuriserMateriauxModeleHumain(_rigHumain);
        AssignerCalquesTetePourVueFps(_rigHumain);
        InitialiserSqueletteHumain();
        BrancherCameraFpsSurSquelette();
        FusionnerAnimationsFbxVersRigHumain();
        DetecterClipsAnimationHumain();
        ConfigurerAnimationTreeLocomotionHumain();
        Callable.From(ForcerLectureAnimLocomotionSiArbreMort).CallDeferred();
    }

    /// <summary>Si lâ€™AnimationTree nâ€™a pas pris le relais, au moins jouer Idle sur le lecteur (Ã©vite T-pose figÃ©e).</summary>
    private void ForcerLectureAnimLocomotionSiArbreMort()
    {
        if (_animationHumain == null || !GodotObject.IsInstanceValid(_animationHumain)) return;
        bool arbreOk = _animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain) && _animationTreeHumain.Active && _playbackLocomotion != null;
        if (arbreOk) return;
        if (_animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain))
            _animationTreeHumain.Active = false;
        if (!string.IsNullOrEmpty(_clipIdleHumain))
            _animationHumain.Play(_clipIdleHumain, 0.08f);
    }

    /// <summary>Y global du <see cref="CharacterBody3D"/> pour que le bas des hitboxes soit juste au-dessus du contact sol (raycast / mesh).</summary>
    public float CalculerYOriginePourPiedsSurSurface(float yContactSolWorld, float epsilon = 0f)
    {
        if (epsilon <= 0f) epsilon = MargeEpsilonPiedsSurSol;
        return yContactSolWorld - CalculerBasCollisionLocalJoueur() + epsilon;
    }

    private void ConfigurerModeCamera(bool activerTps)
    {
        _vueTroisiemePersonne = activerTps;
        if (_cameraFps != null) _cameraFps.Current = !activerTps;
        if (_cameraTps != null) _cameraTps.Current = activerTps;
        if (_rayonFps != null) _rayonFps.Enabled = !activerTps;
        if (_rayonTps != null) _rayonTps.Enabled = activerTps;

        _camera = activerTps ? _cameraTps : _cameraFps;
        _rayon = activerTps ? _rayonTps : _rayonFps;

        if (_rigHumain != null && GodotObject.IsInstanceValid(_rigHumain))
            _rigHumain.Visible = true;

        AppliquerCullMasksCamerasJoueur();
        MettreAJourObjetTenueTps();
    }

    private void BasculerModeCamera()
    {
        ConfigurerModeCamera(!_vueTroisiemePersonne);
        GD.Print(_vueTroisiemePersonne ? "ZERO-K : CamÃ©ra extÃ©rieure activÃ©e." : "ZERO-K : CamÃ©ra premiÃ¨re personne activÃ©e.");
    }

    private static bool EstToggleCameraF5(InputEvent e)
    {
        if (e == null) return false;
        if (e.IsActionPressed("toggle_camera_mode")) return true;
        if (e is InputEventKey k && k.Pressed && !k.Echo && (k.Keycode == Key.F5 || k.PhysicalKeycode == Key.F5))
            return true;
        return false;
    }

    private Node3D ObtenirAttacheMainActiveTps()
    {
        Node3D active = MainGaucheEstActive ? _attacheMainGaucheTps : _attacheMainDroiteTps;
        if (active == null) active = _attacheMainDroiteTps ?? _attacheMainGaucheTps;
        return active;
    }

    private void MettreAJourObjetTenueTps()
    {
        if (_objetEnMain == null || !GodotObject.IsInstanceValid(_objetEnMain)) return;
        bool vueFpsViewmodel = !_vueTroisiemePersonne && _cameraFps != null;
        Node3D parentCible = vueFpsViewmodel ? _cameraFps : ObtenirAttacheMainActiveTps();
        if (parentCible != null && _objetEnMain.GetParent() != parentCible)
        {
            _objetEnMain.Reparent(parentCible);
            _objetEnMain.Position = PositionObjetMainDefaut;
            _objetEnMain.RotationDegrees = RotationObjetMainDefautDeg;
            _objetEnMain.Scale = Vector3.One * 0.9f;
        }

        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        bool visible = !mainActive.EstVide && EstObjetAvecVisuel(mainActive.ID);
        bool frappeEnCours = _tweenFrappe != null && GodotObject.IsInstanceValid(_tweenFrappe) && _tweenFrappe.IsRunning();
        // IMPORTANT : MettreAJourObjetEnMain() recalcule rotation/scale selon le type d'objet.
        // Pendant le tween de frappe on Ã©vite de l'appeler pour ne pas Ã©craser la pose du coup.
        if (!frappeEnCours)
            MettreAJourObjetEnMain();
        _objetEnMain.Visible = visible;
        if (frappeEnCours)
            return;
        if (vueFpsViewmodel && visible)
        {
            // Viewmodel FPS : on garde la rotation dÃ©finie par MettreAJourObjetEnMain()
            // (inclut orientation par type + rotation manuelle X/Y/Z), sinon elle est Ã©crasÃ©e.
            _objetEnMain.Position = PositionObjetViewmodelFps;
        }
    }

    private void ImpulserPoseBrasFrappe(TypeMouvementFrappe type)
    {
        _impulsionIkFrappePoids = 1f;
        _impulsionIkFrappeLocal = type switch
        {
            TypeMouvementFrappe.Estoc => new Vector3(0f, 0.02f, -0.24f),
            TypeMouvementFrappe.DeHautEnBas => new Vector3(0f, -0.18f, -0.18f),
            TypeMouvementFrappe.DeBasEnHaut => new Vector3(0f, 0.17f, -0.12f),
            TypeMouvementFrappe.GaucheADroite => new Vector3(0.18f, 0.02f, -0.12f),
            TypeMouvementFrappe.DroiteAGauche => new Vector3(-0.18f, 0.02f, -0.12f),
            _ => Vector3.Zero
        };
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_aimantIkMainDroite == null || !GodotObject.IsInstanceValid(_aimantIkMainDroite) || _ikBrasDroitFps == null || !GodotObject.IsInstanceValid(_ikBrasDroitFps))
            return;

        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        bool activerIk = !_vueTroisiemePersonne && !MainGaucheEstActive && !mainActive.EstVide;
        float cibleBlend = activerIk ? 1f : 0f;
        _ikBlendMainDroite = Mathf.MoveToward(_ikBlendMainDroite, cibleBlend, dt * 9.5f);
        _impulsionIkFrappePoids = Mathf.MoveToward(_impulsionIkFrappePoids, 0f, dt * 8.0f);

        Vector3 offsetFrappe = _impulsionIkFrappeLocal * _impulsionIkFrappePoids;
        _aimantIkMainDroite.Position = OffsetAimantMainDroiteFpsLocal + offsetFrappe;
        _ikBrasDroitFps.Influence = _ikBlendMainDroite;
    }

    private void MettreAJourAnimationHumain(float dt, Vector3 vitesse, Vector2 entreeWasd, bool auSolPourAnim)
    {
        if (_rigHumain == null || !GodotObject.IsInstanceValid(_rigHumain)) return;
        if (_animationHumain == null || _fallbackAnimProcedural)
            return;

        float vitesseHoriz = new Vector2(vitesse.X, vitesse.Z).Length();
        bool veutMarcher = entreeWasd.LengthSquared() > 0.02f;
        string cibleClip = _clipIdleHumain;
        if (vitesseHoriz > Speed * 0.78f && !string.IsNullOrEmpty(_clipRunHumain))
            cibleClip = _clipRunHumain;
        else if ((veutMarcher || vitesseHoriz > 0.04f) && !string.IsNullOrEmpty(_clipWalkHumain))
            cibleClip = _clipWalkHumain;

        // AnimationTree actif pour Idle/Marche uniquement (aucun appel d'animation de saut).
        if (_playbackLocomotion != null && _animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain))
        {
            if (!_animationTreeHumain.Active)
                _animationTreeHumain.Active = true;

            if (_animationTreeUtiliseBlendDeplacement)
            {
                float blend = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.001f, Speed), 0f, 1f);
                _animationTreeHumain.Set(ParamBlendDeplacementLocomotion, blend);
            }

            string etatMachine = NomEtatDeplacementBlend;
            if (etatMachine != _dernierEtatLocomotionTree)
            {
                _playbackLocomotion.Travel(new StringName(etatMachine));
                _dernierEtatLocomotionTree = etatMachine;
            }
        }
        else
        {
            // Fallback sÃ©curitÃ© : si playback indisponible, lecture directe Idle/Marche.
            if (_animationTreeHumain != null && GodotObject.IsInstanceValid(_animationTreeHumain) && _animationTreeHumain.Active)
                _animationTreeHumain.Active = false;
            if (!string.IsNullOrEmpty(cibleClip) && (_animationHumain.CurrentAnimation != cibleClip || !_animationHumain.IsPlaying()))
                _animationHumain.Play(cibleClip, 0.12f);
        }

        bool arbrePilote = _playbackLocomotion != null && _animationTreeHumain != null && _animationTreeHumain.Active;
        _animationHumain.SpeedScale = arbrePilote
            ? 1f
            : Mathf.Lerp(0.92f, 1.35f, Mathf.Clamp(vitesseHoriz / Mathf.Max(0.001f, Speed), 0f, 1f));
    }

    /// <summary>Avance / strafe alignÃ©s sur la vue camÃ©ra (plan XZ) : Ã©vite W qui part Â« sur le cÃ´tÃ© Â» quand le mesh a un yaw Mixamo diffÃ©rent du corps.</summary>
    private Vector3 CalculerDirectionMouvementAuSol(Vector2 inputDir)
    {
        if (inputDir.LengthSquared() < 1e-6f)
            return Vector3.Zero;

        Camera3D cam = _camera;
        if (cam == null)
            return (Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized();

        Vector3 forward = -cam.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 1e-6f)
            forward = -GlobalTransform.Basis.Z;
        forward = forward.Normalized();

        Vector3 right = cam.GlobalTransform.Basis.X;
        right.Y = 0f;
        if (right.LengthSquared() < 1e-6f)
            right = GlobalTransform.Basis.X;
        right = right.Normalized();

        // GetVector : Y nÃ©gatif = avant (W / move_forward).
        Vector3 dir = forward * (-inputDir.Y) + right * inputDir.X;
        return dir.LengthSquared() < 1e-6f ? Vector3.Zero : dir.Normalized();
    }

    /// <summary>Hitboxes sÃ©parÃ©es : si elles sont dÃ©jÃ  dans la scÃ¨ne (<c>HitboxCorps</c>), on les garde (Ã©diteur + jeu identiques).</summary>
    private void ConstruireHitboxesCompositeJoueur()
    {
        if (GetNodeOrNull("HitboxCorps") is CollisionShape3D deja && deja.Shape != null)
            return;

        foreach (Node c in GetChildren())
        {
            if (c is CollisionShape3D ancien)
            {
                RemoveChild(ancien);
                ancien.Free();
            }
        }

        void Ajouter(string nom, Shape3D forme, Vector3 pos, Vector3 rotDeg)
        {
            var cs = new CollisionShape3D { Name = nom, Shape = forme, Position = pos, RotationDegrees = rotDeg };
            AddChild(cs);
        }

        Ajouter("HitboxJambeG", new CapsuleShape3D { Radius = 0.075f, Height = 0.56f }, new Vector3(-0.11f, -0.44f, 0f), Vector3.Zero);
        Ajouter("HitboxJambeD", new CapsuleShape3D { Radius = 0.075f, Height = 0.56f }, new Vector3(0.11f, -0.44f, 0f), Vector3.Zero);
        Ajouter("HitboxCorps", new CapsuleShape3D { Radius = 0.19f, Height = 0.4f }, new Vector3(0f, 0.12f, 0f), Vector3.Zero);
        Ajouter("HitboxTete", new SphereShape3D { Radius = 0.105f }, new Vector3(0f, 0.58f, 0f), Vector3.Zero);
        Ajouter("HitboxBrasG", new CapsuleShape3D { Radius = 0.055f, Height = 0.34f }, new Vector3(-0.27f, 0.05f, 0f), new Vector3(0f, 0f, 72f));
        Ajouter("HitboxBrasD", new CapsuleShape3D { Radius = 0.055f, Height = 0.34f }, new Vector3(0.27f, 0.05f, 0f), new Vector3(0f, 0f, -72f));
    }

    /// <summary>Point bas (Y local) dâ€™une forme sous sa transform ; <see cref="float.MaxValue"/> si non gÃ©rÃ©e.</summary>
    private static float CalculerBasYLocalPourCollisionShape(CollisionShape3D cs)
    {
        if (cs?.Shape == null) return float.MaxValue;
        Transform3D t = cs.Transform;
        switch (cs.Shape)
        {
            case CapsuleShape3D cap:
            {
                float half = cap.Height * 0.5f + cap.Radius;
                return (t * new Vector3(0f, -half, 0f)).Y;
            }
            case SphereShape3D sph:
                return (t * new Vector3(0f, -sph.Radius, 0f)).Y;
            case BoxShape3D box:
            {
                float minY = float.MaxValue;
                Vector3 e = box.Size * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    float sx = (i & 1) != 0 ? e.X : -e.X;
                    float sy = (i & 2) != 0 ? e.Y : -e.Y;
                    float sz = (i & 4) != 0 ? e.Z : -e.Z;
                    minY = Mathf.Min(minY, (t * new Vector3(sx, sy, sz)).Y);
                }
                return minY;
            }
            default:
                return float.MaxValue;
        }
    }

    /// <summary>Bas local pour poser les pieds du mesh : capsule <see cref="NomCollisionReferencePieds"/> si prÃ©sente (mÃªme dÃ©sactivÃ©e), sinon hitboxes actives.</summary>
    private float CalculerBasPourAlignementPiedsDuMesh()
    {
        if (!float.IsNaN(ForcerBasCollisionLocalPourAlignementPieds))
            return ForcerBasCollisionLocalPourAlignementPieds;
        var csRef = GetNodeOrNull<CollisionShape3D>(NomCollisionReferencePieds);
        if (csRef != null && csRef.Shape != null)
        {
            float y = CalculerBasYLocalPourCollisionShape(csRef);
            if (y != float.MaxValue) return y;
        }
        return CalculerBasCollisionLocalJoueur();
    }

    /// <summary>Point le plus bas (Y local) des <see cref="CollisionShape3D"/> activÃ©es â€” physique / snap sol.</summary>
    private float CalculerBasCollisionLocalJoueur()
    {
        float minY = float.MaxValue;
        foreach (Node c in GetChildren())
        {
            if (c is not CollisionShape3D cs || cs.Disabled || cs.Shape == null) continue;
            float y = CalculerBasYLocalPourCollisionShape(cs);
            if (y != float.MaxValue) minY = Mathf.Min(minY, y);
        }

        return minY == float.MaxValue ? -0.9f : minY;
    }

    private void RetryLierPlaybackAnimationTreeHumain()
    {
        if (_essaisLiaisonPlaybackAnimationTree++ > 8) return;
        if (_animationTreeHumain == null || !GodotObject.IsInstanceValid(_animationTreeHumain) || _animationHumain == null) return;
        if (_playbackLocomotion != null) return;
        ApresAnimationTreePretLocomotion();
        if (_playbackLocomotion == null)
            Callable.From(RetryLierPlaybackAnimationTreeHumain).CallDeferred();
    }

    /// <summary>Quand la collision terrain (RID) arrive aprÃ¨s le mesh, ou si le sol est encore Â« dormant Â», colle la capsule au sol dÃ©tectÃ© par raycast.</summary>
    private void EssayerCollerCapsuleAuSolTerrain(bool dansEau)
    {
        if (dansEau) return;
        if (IsOnFloor()) return;
        // Ne pas tirer vers le sol tant quâ€™on nâ€™est pas en chute nette : sinon le saut est mangÃ© dÃ¨s que Vy redescend sous 2.
        if (Velocity.Y > -0.55f) return;

        World3D w = GetWorld3D();
        if (w?.DirectSpaceState == null) return;

        float basLocalY = CalculerBasCollisionLocalJoueur();
        float origY = GlobalPosition.Y + basLocalY + 0.55f;
        Vector3 orig = new Vector3(GlobalPosition.X, origY, GlobalPosition.Z);
        var q = PhysicsRayQueryParameters3D.Create(orig, orig + new Vector3(0f, -520f, 0f));
        q.CollisionMask = 1;
        q.CollideWithAreas = false;
        q.CollideWithBodies = true;
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = w.DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0 || !hit.ContainsKey("position")) return;
        float solY = ((Vector3)hit["position"]).Y;
        float basCapsuleY = GlobalPosition.Y + basLocalY;
        float gap = basCapsuleY - solY;
        // Ignorer les micro-corrections : elles crÃ©ent un tremblement visible en vue FPS.
        if (gap <= 0.14f || gap >= 140f) return;

        GlobalPosition += new Vector3(0f, -(gap - 0.08f), 0f);
        if (Velocity.Y <= 0.2f)
            Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
    }

    /// <summary>Quand le sol collision nâ€™est pas encore actif, colle le corps au champ de hauteur procÃ©dural (Ã©vite de Â« voler Â» quelques mÃ¨tres au-dessus du terrain).</summary>
    private void AppliquerContrainteVerticaleHauteurTerrainMonde(bool estDansEau, bool ignorerSiMonteeSaut, float dt)
    {
        if (_gestionnaireMonde == null || estDansEau) return;
        // En jeu normal : pas de rabattement sur le bruit procÃ©dural (casse saut, pentes, rebonds). RÃ©servÃ© au chargement / spawn.
        if (ignorerSiMonteeSaut) return;
        if (IsOnFloor()) return;

        int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(
            Mathf.FloorToInt(GlobalPosition.X),
            Mathf.FloorToInt(GlobalPosition.Z),
            _gestionnaireMonde.SeedTerrain);
        float ySurface = h + MargeSurfaceVoxelAuDessusH;
        float yCible = CalculerYOriginePourPiedsSurSurface(ySurface, MargeEpsilonPiedsSurSol);

        float y = GlobalPosition.Y;
        if (y <= yCible + 0.42f) return;

        float ny = y > yCible + 14f
            ? yCible
            : Mathf.MoveToward(y, yCible, Mathf.Max(28f, 55f * (y - yCible)) * dt);
        GlobalPosition = new Vector3(GlobalPosition.X, ny, GlobalPosition.Z);
        if (ny <= yCible + 0.06f)
            Velocity = new Vector3(Velocity.X, Mathf.Min(Velocity.Y, 0f), Velocity.Z);
    }

    private void BrancherModelisateurCAO()
    {
        if (_modelisateur == null) return;
        Node parent = GetParent();
        if (parent == null) return;
        parent.AddChild(_modelisateur);
        _modelisateur.Initialiser(this);
    }

    private void BrancherMenuFutureState()
    {
        if (_menuFutureState == null) return;
        Node parent = GetParent();
        if (parent == null) return;
        parent.AddChild(_menuFutureState);
        _menuFutureState.Initialiser(this);
    }

    /// <summary>Le parent CanvasLayer nâ€™a pas de rectangle : sans Ã§a, ancres FullRect = 0Ã—0 et tout lâ€™UI part en coin.</summary>
    private void AjusterRacineMenuAnatomieViewport()
    {
        if (_racineMenuAnatomieViewport == null || !GodotObject.IsInstanceValid(_racineMenuAnatomieViewport) || GetViewport() == null)
            return;
        Rect2 vr = GetViewport().GetVisibleRect();
        if (vr.Size.X < 1f || vr.Size.Y < 1f)
            return;
        _racineMenuAnatomieViewport.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _racineMenuAnatomieViewport.Position = Vector2.Zero;
        _racineMenuAnatomieViewport.Size = vr.Size;
    }

    private void OnViewportTailleMenuAnatomie()
    {
        AjusterRacineMenuAnatomieViewport();
    }

    /// <summary>LibellÃ©s au-dessus de chaque slot (nom de lâ€™objet pour repÃ©rer les erreurs de donnÃ©es).</summary>
    private void InsererNomsAuDessusSlotsHud()
    {
        if (_slotGauche == null || _slotDroite == null) return;
        if (_slotGauche.GetParent() is not HBoxContainer hbox) return;
        hbox.RemoveChild(_slotDroite);
        hbox.RemoveChild(_slotGauche);

        _lblHudNomMainG = CreerLabelNomSlotHud("G");
        _lblHudNomMainD = CreerLabelNomSlotHud("D");

        var colG = new VBoxContainer
        {
            Name = "ColHudMainG",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var colD = new VBoxContainer
        {
            Name = "ColHudMainD",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        colG.AddChild(_lblHudNomMainG);
        colG.AddChild(_slotGauche);
        colD.AddChild(_lblHudNomMainD);
        colD.AddChild(_slotDroite);

        hbox.AddChild(colG);
        hbox.AddChild(colD);
    }

    private static Label CreerLabelNomSlotHud(string coteMain)
    {
        var lbl = new Label
        {
            Name = $"LabelNomHud{coteMain}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(72, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
        lbl.AddThemeConstantOverride("outline_size", 3);
        return lbl;
    }

    private void MettreAJourLibellesNomsHud()
    {
        if (_lblHudNomMainG != null)
        {
            string n = Atlas_Matiere.ObtenirNomObjet(MainGauche);
            _lblHudNomMainG.Text = MainGauche.EstVide ? "" : n;
            _lblHudNomMainG.Visible = !MainGauche.EstVide && !string.IsNullOrEmpty(n);
        }
        if (_lblHudNomMainD != null)
        {
            string n = Atlas_Matiere.ObtenirNomObjet(MainDroite);
            _lblHudNomMainD.Text = MainDroite.EstVide ? "" : n;
            _lblHudNomMainD.Visible = !MainDroite.EstVide && !string.IsNullOrEmpty(n);
        }
    }

    /// <summary>SubViewport + MeshInstance3D dans chaque slot pour afficher la pierre exacte en 2D.</summary>
    private void CreerPreviewsInventaire3D()
    {
        _viewportSlotGauche = CreerSubViewportPourSlot(_slotGauche, out _meshPreviewGauche);
        _viewportSlotDroite = CreerSubViewportPourSlot(_slotDroite, out _meshPreviewDroite);
    }

    private SubViewportContainer CreerSubViewportPourSlot(Panel slot, out MeshInstance3D meshPreview)
    {
        var container = new SubViewportContainer();
        container.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        container.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        container.Stretch = true;
        slot.AddChild(container);

        var viewport = new SubViewport();
        viewport.Size = new Vector2I(64, 64);
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible;
        viewport.World3D = new World3D();
        viewport.TransparentBg = true;
        container.AddChild(viewport);

        var cam = new Camera3D();
        cam.SetOrthogonal(0.5f, 0.01f, 10f);
        cam.Position = new Vector3(0, 0, 1.2f);
        viewport.AddChild(cam);

        var meshNode = new MeshInstance3D();
        meshNode.Position = Vector3.Zero;
        meshNode.RotationDegrees = new Vector3(-20, 25, 0);
        viewport.AddChild(meshNode);
        meshPreview = meshNode;

        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-45, 30, 0);
        light.Set("sky_mode", 1); // LightOnly : pas de disque dans le ciel (Ã©vite 2e soleil blanc dans SubViewport)
        viewport.AddChild(light);

        return container;
    }

    private void ReinitialiserRotationManuelle()
    {
        _rotationManuelleX = 0f;
        _rotationManuelleY = 0f;
        _rotationManuelleZ = 0f;
    }

    private static bool EstToggleFutureState(InputEvent e)
    {
        return e is InputEventKey k
            && k.Pressed
            && !k.Echo
            && (k.Keycode == Key.K || k.PhysicalKeycode == Key.K);
    }

    public override void _Input(InputEvent @event)
    {
        if (_menuFutureState != null && _menuFutureState.EstOuvert)
        {
            if (@event.IsActionPressed("ui_cancel")
                || (@event is InputEventKey ekEsc && ekEsc.Pressed && !ekEsc.Echo && ekEsc.Keycode == Key.Escape)
                || EstToggleFutureState(@event))
            {
                _menuFutureState.BasculerVisibilite();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
                return;
            if (@event is InputEventKey keBlocFuture && keBlocFuture.Pressed && !keBlocFuture.Echo)
                return;
            if (@event is InputEventAction actionFuture && actionFuture.Pressed)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (EstToggleFutureState(@event) && _menuFutureState != null)
        {
            if (_menuFutureState.EstOuvert)
                _menuFutureState.BasculerVisibilite();
            else
            {
                _menuFutureState.DefinirModeFutureStates();
                OuvrirFutureStateDepuisMenu();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_menuAnatomie != null && @event.IsActionPressed("inventaire"))
        {
            if (!_menuAnatomie.EstOuvert)
            {
                CraftGrille3x3AuTable = false;
                AtelierPlanTravailOuvert = null;
                StockageRackBatonsOuvert = false;
                RackBatonsOuvert = null;
            }
            _menuAnatomie.BasculerVisibilite();
            RafraichirHUD();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            // Ã‰chap / ui_cancel : fermer lâ€™UI et revenir en jeu immÃ©diatement.
            if (@event.IsActionPressed("ui_cancel") ||
                (@event is InputEventKey ekEsc && ekEsc.Pressed && !ekEsc.Echo && ekEsc.Keycode == Key.Escape))
            {
                FermerUIJoueurSiOuverte();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
                return;
            // Laisser Tab (changer_main) descendre jusquâ€™au handler ; bloquer le reste du clavier (minage, etc.).
            if (@event.IsActionPressed("changer_main"))
            {
                // no-op ici : traitÃ© plus bas
            }
            else if (@event is InputEventKey keBloc && keBloc.Pressed && !keBloc.Echo)
                return;
        }

        // Menu CAO (stub) : bloquer le jeu ; Ã‰chap ferme â€” plus de touche K.
        bool caoOuvert = _modelisateur != null && _modelisateur.EstOuvert;
        if (caoOuvert)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
            {
                if (_modelisateur == null || !_modelisateur.SaisieTexteEnCours)
                {
                    _modelisateur.BasculerVisibilite();
                    GetViewport().SetInputAsHandled();
                }
            }
            return;
        }

        if (@event.IsActionPressed("clic_gauche"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            _gaucheMaintenu = true;
            _mouvementSourisCumule = Vector2.Zero;
            if (mainActive.EstVide)
                ReinitialiserMinageMainNueProgression();
        }
        else if (@event.IsActionReleased("clic_gauche") && _gaucheMaintenu)
        {
            _gaucheMaintenu = false;
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (_bloquerActionClicGaucheApresMinageBuisson)
            {
                _bloquerActionClicGaucheApresMinageBuisson = false;
                ReinitialiserMinageMainNueProgression();
                return;
            }
            if (!mainActive.EstVide && PeutUtiliserFrappe(mainActive))
            {
                TypeMouvementFrappe mouv = TypeMouvementFrappe.Estoc;

                if (_mouvementSourisCumule.Length() > 40f)
                {
                    if (Mathf.Abs(_mouvementSourisCumule.X) > Mathf.Abs(_mouvementSourisCumule.Y))
                        mouv = _mouvementSourisCumule.X > 0 ? TypeMouvementFrappe.GaucheADroite : TypeMouvementFrappe.DroiteAGauche;
                    else
                        mouv = _mouvementSourisCumule.Y > 0 ? TypeMouvementFrappe.DeHautEnBas : TypeMouvementFrappe.DeBasEnHaut;
                }

                ExecuterAction(1.0f, mouv);
                JouerAnimationFrappe(mouv);
            }
            ReinitialiserMinageMainNueProgression();
        }
        else if (@event.IsActionPressed("clic_droit"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!mainActive.EstVide) _forceLancer = 0f; // MAIN PLEINE = DÃ‰BUT CHARGE LANCER/POSER
        }
        else if (@event.IsActionReleased("clic_droit"))
        {
            // PRIORITÃ‰ ABSOLUE : si la visÃ©e touche un atelier posÃ©, on ouvre le plan 3x3
            // avant toute logique de pose/lancer de l'objet en main.
            if (EssayerOuvrirAtelierSousVisee())
            {
                _forceLancer = 0f;
                return;
            }

            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!mainActive.EstVide)
            {
                // IDENTIFICATION DE LA MATIÃˆRE : Est-ce du terrain (Voxel) ?
                bool estTerrainVoxel = mainActive.ID >= 1 && mainActive.ID <= 9;
                bool estAtelierEnMain = mainActive.ID == 200;
                bool estRackBatonsEnMain = mainActive.ID == IdObjetRackBatons || mainActive.ID == IdObjetRackBuches;
                bool estBuissonEnMain = mainActive.ID == 10 || mainActive.ID == 11;
                // Clic bref = poser. Maintien du clic = lancer (seuil 0,5 s).
                // Atelier + rack (structures fixes) : jamais de lancer.
                if (estAtelierEnMain || estRackBatonsEnMain || estBuissonEnMain || estTerrainVoxel || _forceLancer < 0.5f)
                {
                    // Clic droit court + lame / roche plate / Ã©clat + sol : fauchage (mÃªme ressenti quâ€™un coup) â€” le gauche le fait aussi.
                    if (!estAtelierEnMain && !estRackBatonsEnMain && !estTerrainVoxel && _forceLancer < 0.5f && ExecuterFauchageSolPrioritaireClicDroit())
                    {
                        _forceLancer = 0f;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    ExecuterPlacement();
                }
                else
                {
                    ExecuterLancer(Mathf.Clamp(_forceLancer, 0.5f, 5.0f));
                }
                _forceLancer = 0f;
            }
            else
            {
                // Main vide : clic droit court sur atelier posÃ© => ouvrir.
                EssayerOuvrirAtelierSousVisee();
            }
        }
        else if (@event.IsActionPressed("interagir"))
        {
            // E : main pleine â†’ corde accrochÃ©e / dÃ©pÃ´t flexible ou rigide ; main vide â†’ ramasser
            ExecuterToucheInteragir();
        }
        else if (@event.IsActionPressed("changer_main"))
        {
            MainGaucheEstActive = !MainGaucheEstActive;
            ReinitialiserRotationManuelle();
            RafraichirHUD();
            _menuAnatomie?.RafraichirMenu();
            GD.Print(MainGaucheEstActive ? "ZERO-K : Main Gauche sÃ©lectionnÃ©e (Tab)." : "ZERO-K : Main Droite sÃ©lectionnÃ©e (Tab).");
        }
        else if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Pressed && !keyEvent.Echo)
            {
                if (keyEvent.Keycode == Key.R)
                {
                    if (keyEvent.CtrlPressed)
                    {
                        _rotationManuelleZ += 90f;
                        if (_rotationManuelleZ >= 360f) _rotationManuelleZ -= 360f;
                    }
                    else if (keyEvent.ShiftPressed)
                    {
                        _rotationManuelleX += 90f;
                        if (_rotationManuelleX >= 360f) _rotationManuelleX -= 360f;
                    }
                    else
                    {
                        _rotationManuelleY += 90f;
                        if (_rotationManuelleY >= 360f) _rotationManuelleY -= 360f;
                    }
                    MettreAJourObjetEnMain();
                    GD.Print($"ZERO-K : Rotation manuelle â€” Y (R) {_rotationManuelleY}Â°, X (Maj+R) {_rotationManuelleX}Â°, Z (Ctrl+R) {_rotationManuelleZ}Â°.");
                }
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (EstToggleCameraF5(@event))
        {
            BasculerModeCamera();
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (_modelisateur != null && _modelisateur.EstOuvert)
            return;

        if (_menuFutureState != null && _menuFutureState.EstOuvert)
            return;

        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            return;

        // Tampon saut : captÃ© ici pour ne pas perdre la frame si un autre nÅ“ud consomme lâ€™input avant _PhysicsProcess.
        if ((@event.IsActionPressed("jump") || @event.IsActionPressed("ui_accept"))
            && ((@event is InputEventKey k && k.Pressed && !k.Echo)
                || (@event is InputEventJoypadButton jb && jb.Pressed)
                || @event is InputEventAction))
            _tamponSautRestant = Mathf.Max(_tamponSautRestant, DureeTamponSautSecondes);

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (_gaucheMaintenu) _mouvementSourisCumule += mouseMotion.Relative;

            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _pitchCamera = Mathf.Clamp(
                _pitchCamera - mouseMotion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(PitchSourisMinDeg),
                Mathf.DegToRad(PitchSourisMaxDeg));
            float pitchAbsolu = _pitchCameraBaseRad + _pitchCamera;
            if (_cameraFps != null)
                _cameraFps.Rotation = new Vector3(pitchAbsolu, _yawCorrectionCameraFpsRad, 0f);
            if (_pivotCameraTps != null)
                _pivotCameraTps.Rotation = new Vector3(pitchAbsolu * 0.82f, 0f, 0f);
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void MettreAJourSlotUI(Panel slot, SlotInventaire slotData, bool selectionne)
    {
        if (slot == null || !GodotObject.IsInstanceValid(slot))
            return;

        int idMatiere = slotData.ID;
        var style = new StyleBoxFlat();
        if (idMatiere == 0)
            style.BgColor = new Color(0.2f, 0.2f, 0.2f);
        else if (idMatiere == 1)
            style.BgColor = new Color(0.5f, 0.3f, 0.1f); // Marron (Terre)
        else if (idMatiere == 2)
            style.BgColor = new Color(0.4f, 0.4f, 0.4f); // Gris foncÃ© (Roche)
        else if (idMatiere == 3)
            style.BgColor = new Color(0.9f, 0.8f, 0.5f); // Jaune pÃ¢le (Sable)
        else if (idMatiere == 4)
            style.BgColor = new Color(0.9f, 0.9f, 0.9f); // Blanc (Neige)
        else if (idMatiere == 5)
            style.BgColor = new Color(0.9f, 0.95f, 1f); // Blanc bleutÃ© (Neige/Glace)
        else if (idMatiere == 6)
            style.BgColor = new Color(0.6f, 0.45f, 0.25f); // Terre aride (Arid earth)
        else if (idMatiere == 7)
            style.BgColor = new Color(0.35f, 0.25f, 0.15f); // Boue (Mud)
        else if (idMatiere == 8)
            style.BgColor = new Color(0.3f, 0.5f, 0.2f); // Terre tropicale
        else if (idMatiere == 9)
            style.BgColor = new Color(0.7f, 0.75f, 0.8f); // Terre gelÃ©e
        else if (ItemPhysique.EstIdRocheMatiere(idMatiere))
        {
            int idx = ItemPhysique.IndexChimiqueDepuisIdRoche(idMatiere);
            style.BgColor = ItemPhysique.TableGeologique[idx].CouleurBase;
        }
        else if (idMatiere == 999)
            style.BgColor = new Color(0.1f, 0.8f, 0.2f); // Vert (Objet/Buisson)
        else if (idMatiere == 30)
            style.BgColor = new Color(0.4f, 0.25f, 0.15f); // Marron (BÃ»che)
        else if (idMatiere == 32)
            style.BgColor = new Color(0.5f, 0.35f, 0.2f); // Marron clair (BÃ¢ton)
        else if (idMatiere == 34)
            style.BgColor = new Color(0.2f, 0.55f, 0.15f); // Vert feuillage
        else if (idMatiere == 100)
            style.BgColor = new Color(0.85f, 0.65f, 0.2f); // Or (outil forgÃ© CAO)
        else if (idMatiere == 105)
        {
            int ir = Mathf.Clamp(slotData.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            style.BgColor = ItemPhysique.TableGeologique[ir].CouleurBase.Lerp(new Color(0.35f, 0.28f, 0.2f), 0.35f);
        }
        else if (idMatiere == 106)
        {
            int ir = Mathf.Clamp(slotData.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            style.BgColor = ItemPhysique.TableGeologique[ir].CouleurBase.Lerp(new Color(0.42f, 0.32f, 0.18f), 0.28f);
        }
        else
            style.BgColor = new Color(0.4f, 0.4f, 0.6f); // Violet (Autre)

        if (selectionne)
        {
            style.BorderColor = new Color(1f, 0.9f, 0.2f);
            style.SetBorderWidthAll(3);
        }

        slot.AddThemeStyleboxOverride("panel", style);
    }

    /// <summary>Masque le mesh Â« en main Â» devant la camÃ©ra quand le menu CAO est ouvert (Ã©vite la confusion avec le transit UI).</summary>
    public void DefinirVisibiliteObjetMainCamera(bool visible)
    {
        if (_objetEnMain != null)
            _objetEnMain.Visible = visible;
    }

    /// <summary>True si le menu inventaire (Q) est ouvert â€” utilisÃ© par le gestionnaire pour Ã‰chap â†’ pause sans fermer lâ€™UI.</summary>
    public bool MenuAnatomieOuvert() => _menuAnatomie != null && _menuAnatomie.EstOuvert;

    /// <summary>Ferme les UI joueur (inventaire/craft ou CAO) via Ã‰chap et remet le contrÃ´le jeu.</summary>
    public bool FermerUIJoueurSiOuverte()
    {
        if (_menuFutureState != null && _menuFutureState.EstOuvert)
        {
            _menuFutureState.BasculerVisibilite();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return true;
        }
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            CraftGrille3x3AuTable = false;
            AtelierPlanTravailOuvert = null;
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
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

    public ulong ObtenirNiveauFutureState(string nomStat)
    {
        if (string.IsNullOrWhiteSpace(nomStat))
            return 0UL;
        return _futureStates.TryGetValue(nomStat, out ulong niveau) ? niveau : 0UL;
    }

    public UInt128 ObtenirXpFutureState(string nomStat)
    {
        if (string.IsNullOrWhiteSpace(nomStat))
            return UInt128.Zero;
        return _futureStateXp.TryGetValue(nomStat, out UInt128 xp) ? xp : UInt128.Zero;
    }

    private static UInt128 CalculerXpNiveauSuivant(ulong niveau)
    {
        UInt128 prochainNiveau = (UInt128)niveau + 1u;
        UInt128 termeLineaire = XpHybrideCoefLineaire * prochainNiveau;
        UInt128 termeQuadratique = (prochainNiveau * prochainNiveau) / XpHybrideDivQuadratique;
        return termeLineaire + termeQuadratique;
    }

    public UInt128 ObtenirXpNecessaireProchainNiveauFutureState(string nomStat)
    {
        ulong niveau = ObtenirNiveauFutureState(nomStat);
        return CalculerXpNiveauSuivant(niveau);
    }

    public void AjouterXpFutureState(string nomStat, ulong xpGagne)
    {
        if (string.IsNullOrWhiteSpace(nomStat) || xpGagne == 0UL)
            return;
        AjouterFutureStateSiAbsent(nomStat, 0UL);
        UInt128 xpActuel = ObtenirXpFutureState(nomStat);
        UInt128 gain = xpGagne;
        UInt128 xpTotal = xpActuel > UInt128.MaxValue - gain ? UInt128.MaxValue : xpActuel + gain;
        ulong niveau = ObtenirNiveauFutureState(nomStat);
        while (niveau < NiveauMaxFutureState)
        {
            UInt128 cout = ObtenirXpNecessaireProchainNiveauFutureState(nomStat);
            if (xpTotal < cout || cout == UInt128.Zero || cout == UInt128.MaxValue)
                break;
            xpTotal -= cout;
            niveau++;
        }
        _futureStateXp[nomStat] = xpTotal;
        _futureStates[nomStat] = Math.Min(niveau, NiveauMaxFutureState);
        _menuFutureState?.Rafraichir();
    }

    public void DefinirNiveauFutureState(string nomStat, ulong niveau)
    {
        if (string.IsNullOrWhiteSpace(nomStat))
            return;
        _futureStates[nomStat] = Math.Min(niveau, NiveauMaxFutureState);
        _menuFutureState?.Rafraichir();
    }

    public void AjouterFutureStateSiAbsent(string nomStat, ulong niveauInitial = 0UL)
    {
        if (string.IsNullOrWhiteSpace(nomStat) || _futureStates.ContainsKey(nomStat))
            return;
        _futureStates[nomStat] = Math.Min(niveauInitial, NiveauMaxFutureState);
        _futureStateXp[nomStat] = UInt128.Zero;
        _menuFutureState?.Rafraichir();
    }

    public void OuvrirFutureStateDepuisMenu()
    {
        OuvrirFutureStateDepuisMenu(false);
    }

    public void OuvrirFutureStateDepuisMenu(bool ouvrirMetiers)
    {
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.BasculerVisibilite();
        if (_modelisateur != null && _modelisateur.EstOuvert && !_modelisateur.SaisieTexteEnCours)
            _modelisateur.BasculerVisibilite();
        if (_menuFutureState != null && !_menuFutureState.EstOuvert)
            _menuFutureState.BasculerVisibilite();
        if (ouvrirMetiers)
            _menuFutureState?.DefinirModeMetiers();
        else
            _menuFutureState?.DefinirModeFutureStates();
        _menuFutureState?.Rafraichir();
    }

    public void OuvrirMetiersDepuisMenu()
    {
        OuvrirFutureStateDepuisMenu(true);
    }

    public void OuvrirInventaireDepuisFutureState()
    {
        if (_menuFutureState != null && _menuFutureState.EstOuvert)
            _menuFutureState.BasculerVisibilite();
        if (_menuAnatomie != null && !_menuAnatomie.EstOuvert)
            _menuAnatomie.BasculerVisibilite();
        _menuAnatomie?.ForcerOngletInventaire();
        _menuAnatomie?.RafraichirMenu();
    }

    public IReadOnlyDictionary<string, ulong> ObtenirMetiers() => _metiers;

    public ulong ObtenirNiveauMetier(string nomMetier)
    {
        if (string.IsNullOrWhiteSpace(nomMetier))
            return 0UL;
        return _metiers.TryGetValue(nomMetier, out ulong niveau) ? niveau : 0UL;
    }

    public UInt128 ObtenirXpMetier(string nomMetier)
    {
        if (string.IsNullOrWhiteSpace(nomMetier))
            return UInt128.Zero;
        return _metierXp.TryGetValue(nomMetier, out UInt128 xp) ? xp : UInt128.Zero;
    }

    public UInt128 ObtenirXpNecessaireProchainNiveauMetier(string nomMetier)
    {
        ulong niveau = ObtenirNiveauMetier(nomMetier);
        return CalculerXpNiveauSuivant(niveau);
    }

    public void AjouterMetierSiAbsent(string nomMetier, ulong niveauInitial = 0UL)
    {
        if (string.IsNullOrWhiteSpace(nomMetier) || _metiers.ContainsKey(nomMetier))
            return;
        _metiers[nomMetier] = Math.Min(niveauInitial, NiveauMaxFutureState);
        _metierXp[nomMetier] = UInt128.Zero;
        _menuFutureState?.Rafraichir();
    }

    public void AjouterXpMetier(string nomMetier, ulong xpGagne)
    {
        if (string.IsNullOrWhiteSpace(nomMetier) || xpGagne == 0UL)
            return;
        AjouterMetierSiAbsent(nomMetier, 0UL);
        UInt128 xpActuel = ObtenirXpMetier(nomMetier);
        UInt128 gain = xpGagne;
        UInt128 xpTotal = xpActuel > UInt128.MaxValue - gain ? UInt128.MaxValue : xpActuel + gain;
        ulong niveau = ObtenirNiveauMetier(nomMetier);
        while (niveau < NiveauMaxFutureState)
        {
            UInt128 cout = ObtenirXpNecessaireProchainNiveauMetier(nomMetier);
            if (xpTotal < cout || cout == UInt128.Zero || cout == UInt128.MaxValue)
                break;
            xpTotal -= cout;
            niveau++;
        }
        _metierXp[nomMetier] = xpTotal;
        _metiers[nomMetier] = Math.Min(niveau, NiveauMaxFutureState);
        _menuFutureState?.Rafraichir();
    }

    public float ObtenirBonusDegatsArbreBucheron()
    {
        return ObtenirNiveauMetier("Bucheron") * 0.01f;
    }

    private const double BonusForceParNiveauPourcent = 0.0001d; // +0,01% / niveau

    public float ObtenirMultiplicateurDegatsForce()
    {
        ulong niveauForce = ObtenirNiveauFutureState("Force");
        double multiplicateur = 1d + (niveauForce * BonusForceParNiveauPourcent);
        if (double.IsNaN(multiplicateur) || double.IsInfinity(multiplicateur))
            return 1f;
        return (float)Mathf.Clamp((float)multiplicateur, 1f, 100000f);
    }

    public float ObtenirMultiplicateurCapaciteChargeForce()
    {
        ulong niveauForce = ObtenirNiveauFutureState("Force");
        double multiplicateur = 1d + (niveauForce * BonusForceParNiveauPourcent);
        if (double.IsNaN(multiplicateur) || double.IsInfinity(multiplicateur))
            return 1f;
        return (float)Mathf.Clamp((float)multiplicateur, 1f, 100000f);
    }

    private static float ObtenirMasseUnitaireRocheInventaireKg(int indexTaille) => Mathf.Clamp(indexTaille, 0, 4) switch
    {
        0 => 0.45f,
        1 => 1.10f,
        2 => 2.20f,
        3 => 4.60f,
        _ => 9.20f
    };

    private static float ObtenirMasseUnitaireSimpleParIdKg(int idObjet) => idObjet switch
    {
        15 => 0.08f,
        16 => 0.08f,
        17 => 0.08f,
        20 => 0.08f,
        21 => 0.10f,
        IdObjetCeinturePoches => 0.14f,
        IdObjetCeintureSacoches => 0.18f,
        IdObjetPochetteTier0 => 0.12f,
        IdObjetSacTier0 => 0.16f,
        105 => 0.32f,
        106 => 0.58f,
        IdObjetPellePierreTier0 => 0.62f,
        IdObjetPiochePierreTier0 => 0.66f,
        IdObjetBaie => 0.03f,
        34 => 0.04f,
        999 => 1.2f,
        200 => 12.0f,
        IdObjetRackBatons => 8.0f,
        IdObjetRackBuches => 8.0f,
        _ => 0.5f
    };

    private static float ObtenirMasseUnitaireBoisInventaireKg(SlotInventaire slot)
    {
        CalculerDimensionsBoisPose(slot.ID, slot.IndexMorphologique, slot.IndexTaille, out float baseRadius, out float baseLength, out float w, out float h);
        float longueur = baseLength;
        if (slot.ScaleEclat.Z > 0.01f)
            longueur *= slot.ScaleEclat.Z;
        float rayonX = Mathf.Max(0.001f, w * 0.5f);
        float rayonY = Mathf.Max(0.001f, h * 0.5f);
        float volume = Mathf.Pi * rayonX * rayonY * Mathf.Max(0.05f, longueur);
        return Mathf.Max(0.05f, volume * 520f);
    }

    public static float ObtenirMasseSlotInventaireKg(SlotInventaire slot)
    {
        if (slot.EstVide)
            return 0f;
        if (ItemPhysique.EstIdRocheMatiere(slot.ID))
            return ObtenirMasseUnitaireRocheInventaireKg(slot.IndexTaille);
        if (slot.ID == 30 || slot.ID == 32)
            return ObtenirMasseUnitaireBoisInventaireKg(slot);
        return ObtenirMasseUnitaireSimpleParIdKg(slot.ID);
    }

    private static float ObtenirMasseTotaleSlotInventaireKg(SlotInventaire slot)
    {
        if (slot.EstVide)
            return 0f;
        return ObtenirMasseSlotInventaireKg(slot) * Mathf.Max(1, ObtenirQuantiteSlot(slot));
    }

    public float ObtenirPoidsTotalPorteKg()
    {
        float total = 0f;
        total += ObtenirMasseTotaleSlotInventaireKg(MainGauche);
        total += ObtenirMasseTotaleSlotInventaireKg(MainDroite);
        total += ObtenirMasseTotaleSlotInventaireKg(EquipementSacDos);
        total += ObtenirMasseTotaleSlotInventaireKg(EquipementCeinture);
        for (int i = 0; i < GrilleCraftPoche.Length; i++)
            total += ObtenirMasseTotaleSlotInventaireKg(GrilleCraftPoche[i]);
        for (int i = 0; i < GrilleSacStockage.Length; i++)
            total += ObtenirMasseTotaleSlotInventaireKg(GrilleSacStockage[i]);
        for (int i = 0; i < GrilleCeintureStockage.Length; i++)
            total += ObtenirMasseTotaleSlotInventaireKg(GrilleCeintureStockage[i]);
        return total;
    }

    public float ObtenirCapacitePoidsMaxKg()
    {
        return CapacitePoidsBaseHumainKg * ObtenirMultiplicateurCapaciteChargeForce();
    }

    /// <summary>
    /// Au-delÃ  de la charge Â« confort Â», le joueur peut encore porter mais se dÃ©place plus lentement
    /// (plus la surcharge est grande, plus le facteur est bas).
    /// </summary>
    public float ObtenirFacteurVitesseSelonChargePortee()
    {
        float cap = ObtenirCapacitePoidsMaxKg();
        if (cap < 0.01f)
            return 1f;
        float poids = ObtenirPoidsTotalPorteKg();
        if (poids <= cap)
            return 1f;
        float surplusRelatif = (poids - cap) / cap;
        const float intensiteRalentissement = 1.15f;
        return Mathf.Clamp(1f / (1f + intensiteRalentissement * surplusRelatif), 0.1f, 1f);
    }

    public bool PeutPorterSlotSupplementaire(SlotInventaire slot)
    {
        _ = slot;
        return true;
    }

    public void RafraichirHUD()
    {
        AssurerDurabiliteOutilsSurLesMains();
        MettreAJourSlotUI(_slotGauche, MainGauche, MainGaucheEstActive);
        MettreAJourSlotUI(_slotDroite, MainDroite, !MainGaucheEstActive);
        MettreAJourLibellesNomsHud();
        MettreAJourObjetEnMain();
        RafraichirVisuelsEquipementsCorps();
        MettreAJourPreviewsSlots();
        MettreAJourVisibilitePreviews();
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }

    /// <summary>Assigne le Mesh exact de la main active au MeshInstance3D devant la camÃ©ra.</summary>
    private static bool EstObjetProcedural(int id) => ItemPhysique.EstIdRocheMatiere(id);

    private static bool PeutUtiliserFrappe(SlotInventaire s)
    {
        if (s.EstVide) return false;
        if (EstObjetProcedural(s.ID)) return true;
        if (s.ID == 105 || s.ID == 106 || s.ID == IdObjetPellePierreTier0 || s.ID == IdObjetPiochePierreTier0) return true;
        return s.ID == 100 && s.EstUnEclat && s.MeshEclat != null;
    }

    private void AssurerDurabiliteOutilsSurLesMains()
    {
        if (MainGauche.ID == 105 || MainGauche.ID == 106 || MainGauche.ID == IdObjetPellePierreTier0 || MainGauche.ID == IdObjetPiochePierreTier0)
        {
            var m = MainGauche;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref m);
            MainGauche = m;
        }
        if (MainDroite.ID == 105 || MainDroite.ID == 106 || MainDroite.ID == IdObjetPellePierreTier0 || MainDroite.ID == IdObjetPiochePierreTier0)
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
            if (MainGauche.ID != 105 && MainGauche.ID != 106 && MainGauche.ID != IdObjetPellePierreTier0 && MainGauche.ID != IdObjetPiochePierreTier0) return 0;
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
            if (MainDroite.ID != 105 && MainDroite.ID != 106 && MainDroite.ID != IdObjetPellePierreTier0 && MainDroite.ID != IdObjetPiochePierreTier0) return 0;
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
            else if (idOutilCasse == 106)
                GD.Print("ZERO-K : La hachette primitive se brise â€” lame ou manche a cÃ©dÃ©. Il vous faudra refaire lâ€™outil.");
            else if (idOutilCasse == IdObjetPellePierreTier0)
                GD.Print("ZERO-K : La pelle en pierre se brise â€” il faut reforger lâ€™outil.");
            else
                GD.Print("ZERO-K : La pioche en pierre se brise â€” il faut reforger lâ€™outil.");
        }
        RafraichirHUD();
        return idOutilCasse;
    }

    private static void RemplirDurabiliteOutilDepuisItemPhysique(ref SlotInventaire slot, ItemPhysique item)
    {
        if ((slot.ID != 105 && slot.ID != 106 && slot.ID != IdObjetPellePierreTier0 && slot.ID != IdObjetPiochePierreTier0) || item == null) return;
        if (item.HasMeta(MetaDurabiliteOutilMax))
        {
            slot.DurabiliteOutilMax = (float)item.GetMeta(MetaDurabiliteOutilMax).AsDouble();
            slot.DurabiliteOutilActuelle = item.HasMeta(MetaDurabiliteOutilActuelle)
                ? (float)item.GetMeta(MetaDurabiliteOutilActuelle).AsDouble()
                : slot.DurabiliteOutilMax;
        }
        else
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slot);
        if (item.HasMeta(MetaTailleLameRoche))
            slot.IndexTailleLameRoche = (int)item.GetMeta(MetaTailleLameRoche).AsInt32();
    }

    private static Mesh _cacheMeshCorde;
    /// <summary>Invalider le cache si la topologie du mesh corde change (Ã©vite un mesh cassÃ© gardÃ© en statique).</summary>
    private const int RevisionCacheMeshCorde = 1;
    private static int _revisionMeshCordeEnCache = -1;

    private static Mesh CreerMeshCordeTressee()
    {
        if (_cacheMeshCorde != null && _revisionMeshCordeEnCache == RevisionCacheMeshCorde)
            return _cacheMeshCorde;
        _cacheMeshCorde = null;
        const float rayonHelice = 0.026f;
        const float rayonTube = 0.012f;
        const float hauteur = 0.28f;
        const int nbTours = 3;
        const int ringsParStrand = 24;
        const int segsParRing = 6;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int strand = 0; strand < 3; strand++)
        {
            float phase = strand * Mathf.Tau / 3f;
            for (int r = 0; r < ringsParStrand; r++)
            {
                float t = r / (float)(ringsParStrand - 1);
                float angle = phase + t * nbTours * Mathf.Tau;
                Vector3 centre = new Vector3(rayonHelice * Mathf.Cos(angle), t * hauteur - hauteur * 0.5f, rayonHelice * Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), hauteur / (rayonHelice * nbTours * Mathf.Tau), Mathf.Cos(angle)).Normalized();
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 binormal = tangent.Cross(radial).Normalized();
                for (int s = 0; s < segsParRing; s++)
                {
                    float a = s * Mathf.Tau / segsParRing;
                    Vector3 offset = (radial * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * rayonTube;
                    st.AddVertex(centre + offset);
                }
            }
            // Quads entre deux anneaux : b = voisin latÃ©ral sur lâ€™anneau (wrap), pas v+s1 (cassait s=dernier â†’ index hors plage).
            for (int r = 0; r < ringsParStrand - 1; r++)
            {
                int v0 = strand * ringsParStrand * segsParRing + r * segsParRing;
                for (int s = 0; s < segsParRing; s++)
                {
                    int s1 = (s + 1) % segsParRing;
                    int a = v0 + s;
                    int b = v0 + s1;
                    int c = v0 + segsParRing + s;
                    int d = v0 + segsParRing + s1;
                    st.AddIndex(a); st.AddIndex(b); st.AddIndex(c);
                    st.AddIndex(b); st.AddIndex(d); st.AddIndex(c);
                }
            }
        }
        st.GenerateNormals();
        _cacheMeshCorde = st.Commit();
        _revisionMeshCordeEnCache = RevisionCacheMeshCorde;
        return _cacheMeshCorde;
    }

    public static Mesh ObtenirMeshDepuisCache(int id, int indexMorpho, int indexTaille = 2)
    {
        if (ItemPhysique.EstIdRocheMatiere(id))
        {
            float r = ItemPhysique.RayonBaseRochesJoueur(indexTaille);
            return new SphereMesh { Radius = r, Height = r * 2f };
        }
        else if (id == 10) return Chunk_Client.ObtenirMeshBuissonProcedural(true);
        else if (id == 11) return Chunk_Client.ObtenirMeshBuissonProcedural(false);
        else if (id == BlocChutant.ID_BRANCHE) return new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.6f, RadialSegments = 10, Rings = 1 };
        else if (id == 15 || id == 16) return new CapsuleMesh { Radius = 0.009f, Height = 0.34f };
        else if (id == 17) return new CapsuleMesh { Radius = 0.009f, Height = 0.38f };
        else if (id == 20) return null; // GLB res://Modeles/materials/traisagre_corde_tier0.glb via InstancierModeleCordeTier0Gazon
        else if (id == 21) return null; // GLB res://Modeles/materials/tissu_tier0.glb via InstancierModeleTissuTier0
        else if (id == IdObjetSacTier0) return null; // GLB res://Modeles/Equipable/Sac_Tiere0.glb via InstancierModeleSacTier0
        else if (id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches) return null; // GLB ceinture / ceinture+pochettes via instanciation dÃ©diÃ©e
        else if (id == IdObjetPochetteTier0) return null; // GLB res://Modeles/materials/Pochette_Tiere0.glb via InstancierModelePochetteTier0
        else if (id == IdObjetPellePierreTier0) return null; // GLB res://Modeles/Equipements/Pelle_Pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetPiochePierreTier0) return null; // GLB res://Modeles/Equipements/Pioche_pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetRackBatons || id == IdObjetRackBuches) return null; // GLB rack (bÃ¢tons / bÃ»ches) via instanciation dÃ©diÃ©e
        else if (id == 30 || id == 32)
        {
            CalculerDimensionsBoisPose(id, indexMorpho, indexTaille, out float br, out float bl, out _, out _);
            return GenererMeshBoisFendu(br, bl, indexMorpho);
        }
        else if (id == 34) return new QuadMesh { Size = new Vector2(0.12f, 0.18f) }; // Feuilles (mÃªme style que feuillage arbre)
        else if (id == IdObjetBaie) return new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 10, Rings = 6 };
        if (id >= 1 && id <= 9)
            return new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) };
        return null;
    }

    public static void AppliquerMaterielObjet(MeshInstance3D visuel, int idObjet, int indexChimique, int indexMorphologique = 0, int niveauTressage = 0, byte indexBotanique = LSystem_Botanique.IndexChene)
    {
        // FIX CRITIQUE : Ne JAMAIS Ã©craser le matÃ©riau d'un outil forgÃ© (il possÃ¨de ses propres surfaces cuites)
        if (idObjet == 100)
        {
            visuel.MaterialOverride = null;
            return;
        }
        if (idObjet == 15 || idObjet == 16 || idObjet == 17)
        {
            visuel.MaterialOverride = Atlas_Matiere.ObtenirProfilFlexible(idObjet, out var pf)
                ? new StandardMaterial3D { AlbedoColor = pf.CouleurCorde, Roughness = 0.9f, Metallic = 0f }
                : new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.55f, 0.15f), Roughness = 0.9f };
            return;
        }
        if (idObjet == 20 || idObjet == 21 || idObjet == IdObjetCeinturePoches || idObjet == IdObjetCeintureSacoches || idObjet == IdObjetPochetteTier0 || idObjet == IdObjetSacTier0)
        {
            bool varianteHerbeSolide = indexBotanique == TagVarianteHerbeSolide
                || (indexChimique == 15 && indexMorphologique == 15 && indexBotanique >= 2);
            bool varianteLiane = indexBotanique == TagVarianteLiane
                || (indexChimique == 16 && indexMorphologique == 16 && indexBotanique < 2);

            int matA = indexChimique;
            int matB = indexMorphologique;
            int niveauAspect = niveauTressage;

            if (varianteHerbeSolide)
            {
                // ForÃ§age visuel cohÃ©rent: toute variante herbe solide rend comme une ligature d'herbe solide tier 2.
                matA = 15;
                matB = 15;
                niveauAspect = Mathf.Max(niveauAspect, 2);
            }
            else if (varianteLiane)
            {
                matA = 16;
                matB = 16;
            }

            visuel.MaterialOverride = Atlas_Matiere.ObtenirMaterielCorde(matA, matB, niveauAspect);
            return;
        }
        if (idObjet == 30 || idObjet == 32)
        {
            visuel.MaterialOverride = idObjet == 32 && indexChimique == 1 && indexBotanique == LSystem_Botanique.IndexChene
                ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                : ArbreVivant.ObtenirMaterielBoisTriplanar(indexBotanique);
            return;
        }
        if (idObjet == 10 || idObjet == 11)
        {
            visuel.MaterialOverride = null; // Le mesh buisson porte dÃ©jÃ  son matÃ©riau procÃ©dural.
            return;
        }
        if (idObjet == BlocChutant.ID_BRANCHE)
        {
            visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.52f, 0.32f, 0.14f), Roughness = 0.9f, Metallic = 0.02f };
            return;
        }
        if (idObjet == 34) { visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.55f, 0.15f), Roughness = 0.95f, Metallic = 0f }; return; }
        if (idObjet == IdObjetBaie)
        {
            Color c = indexChimique switch
            {
                1 => new Color(0.82f, 0.24f, 0.64f), // futur violet
                2 => new Color(0.95f, 0.62f, 0.12f), // futur orange
                _ => new Color(0.90f, 0.14f, 0.14f)  // rouge par dÃ©faut
            };
            visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.34f, Metallic = 0f, EmissionEnabled = true, Emission = c * 0.06f };
            return;
        }
        if (idObjet >= 1 && idObjet <= 9)
        {
            visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.3f, 0.2f), Roughness = 1f, Metallic = 0f };
            return;
        }
        int chimique = Mathf.Clamp(indexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        if (ItemPhysique.EstIdRocheMatiere(idObjet))
            chimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet);
        visuel.MaterialOverride = ItemPhysique.CreerMaterielProcedural(ItemPhysique.EstMatiereSilexParIdObjet(idObjet), chimique);
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

    /// <summary>Phase 1 pure : minage du terrain Marching Cubes uniquement. Clic gauche.</summary>
    private void ExecuterMinageVoxel()
    {
        float multiplicateurForce = ObtenirMultiplicateurDegatsForce();
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        // ArbreVivant : seules roches brutes et Ã©clats â€” la dague (105) est trop fragile pour le bois.
        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = ItemPhysique.EstIdRocheMatiere(main.ID) || main.EstUnEclat || main.ID == 106;
            if (!outilTranchantPourArbre) return;

            float degatsArbre = 5.0f * multiplicateurForce + ObtenirBonusDegatsArbreBucheron();
            float epaisseurLame = 0.2f;
            if (!main.EstVide)
            {
                if (main.EstUnEclat && main.MeshEclat != null)
                {
                    Aabb boite = main.MeshEclat.GetAabb();
                    epaisseurLame = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));
                    degatsArbre *= Mathf.Clamp(0.2f / Mathf.Max(0.005f, epaisseurLame), 1.0f, 40.0f);
                }
                else if (ItemPhysique.EstMatiereSilexParIdObjet(main.ID)) { epaisseurLame = 0.05f; degatsArbre *= 2.5f; }
                else if (main.ID == 106) { epaisseurLame = 0.065f; degatsArbre *= 2.2f; }
            }
            Vector3 pointImpact = _rayon.GetCollisionPoint();
            Vector3 directionFrappe = -_rayon.GetCollisionNormal();
            if (directionFrappe.LengthSquared() < 0.1f)
                directionFrappe = -_camera.GlobalTransform.Basis.Z.Normalized();
            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, degatsArbre, epaisseurLame, main.ID == 106);
            if (resultatCoupe == 0) return;
            if (resultatCoupe == 2)
                AjouterXpMetier("Bucheron", 1UL);
            bool rochePlate = ItemPhysique.EstIdRocheMatiere(main.ID) && main.IndexMorphologique == 1;
            if (rochePlate && ObtenirNiveauFutureState("Force") < 15UL)
                AjouterXpFutureState("Force", 1UL);
            JouerSonEtEffetCoupeArbre(pointImpact);
            return;
        }
        // Si on touche un objet physique valide, on annule le minage (y compris CollisionShape3D sous RigidBody).
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses"))) return;

        // Si objetTouche est null, cela signifie qu'on a touchÃ© le terrain bas-niveau ! ON CONTINUE LE MINAGE.
        Vector3 pointImpactVoxel = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeSondage = pointImpactVoxel - (normaleImpact * 0.5f);

        int idExtrait = _gestionnaireMonde?.ObtenirMatiereExacte(pointDeSondage) ?? 1;
        // Toujours terrain (1-9) pour que la pose refusionne avec le sol ; jamais 10/11/12/999 (bloc vert).
        if (idExtrait < 1 || idExtrait > 9) idExtrait = 1;

        if (MainGaucheEstActive && !MainGauche.EstVide && !MainDroite.EstVide) return;
        if (!MainGaucheEstActive && !MainDroite.EstVide && !MainGauche.EstVide) return;

        float forceDegats = 5.0f * multiplicateurForce;
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        // THÃ‰ORÃˆME DE LA LAME : L'Ã©paisseur dicte le tranchant
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            Aabb boite = mainActive.MeshEclat.GetAabb();
            float epaisseur = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));

            float multiplicateur = 0.2f / Mathf.Max(0.005f, epaisseur);
            forceDegats *= Mathf.Clamp(multiplicateur, 1.0f, 40.0f);

            GD.Print($"ZERO-K : Lame dÃ©tectÃ©e. Ã‰paisseur: {epaisseur:F3}m | Tranchant: x{multiplicateur:F1}");
        }
        else if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            forceDegats *= 2.5f;

        _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpactVoxel, RAYON_SCULPTURE, forceDegats);

        var nouveauSlot = new SlotInventaire { ID = idExtrait, IndexMorphologique = 0, IndexChimique = 0 };
        if (MainGaucheEstActive)
        {
            if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else MainDroite = nouveauSlot;
        }
        else
        {
            if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else MainGauche = nouveauSlot;
        }
        RafraichirHUD();
    }

    /// <summary>Spawn du lancer au niveau du corps (main / torse), puis lÃ©ger recul si un mur bloque tout de suite devant.</summary>
    private Vector3 CalculerPointSpawnLancer(Vector3 direction)
    {
        direction = direction.Normalized();
        Vector3 offsetMain = _camera.GlobalTransform.Basis.X * 0.3f + _camera.GlobalTransform.Basis.Y * -0.2f;
        Vector3 orig = _camera.GlobalPosition + direction * 0.4f + offsetMain;

        var query = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition, orig + direction * 0.2f);
        query.CollisionMask = 1;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
            return (Vector3)hit["position"] - direction * 0.1f;
        return orig;
    }

    /// <summary>Vitesse cible (m/s) pour un lancer : indÃ©pendante de la masse (impulsion = mÃ—v).</summary>
    private static float ObtenirVitesseCibleLancer(float forceCharge)
    {
        float f = Mathf.Clamp(forceCharge, 0.5f, 5.0f);
        return Mathf.Lerp(8f, 24f, Mathf.InverseLerp(0.5f, 5f, f));
    }

    /// <summary>Clic court Â« poser Â» : petit Ã©lan vers la visÃ©e pour ne pas tomber comme un plomb.</summary>
    private void AppliquerImpulsionLacherDoux(Node3D nePose)
    {
        if (nePose is not RigidBody3D rb || _camera == null) return;
        rb.Sleeping = false;
        Vector3 dir = -_camera.GlobalTransform.Basis.Z;
        dir = new Vector3(dir.X, Mathf.Max(0.1f, dir.Y + 0.32f), dir.Z);
        if (dir.LengthSquared() < 1e-6f) return;
        dir = dir.Normalized();
        float m = Mathf.Max(0.012f, rb.Mass);
        float bonusLeger = Mathf.Clamp(1.22f - m * 0.028f, 0.9f, 1.28f);
        float v = 2.85f * bonusLeger;
        rb.ApplyCentralImpulse(dir * (m * v));
    }

    /// <summary>Lance lâ€™objet tenu : impulsion = masse Ã— vitesse cible (mÃªme sensation petit caillou / gros morceau).</summary>
    private void ExecuterLancer(float force)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide) return;

        Vector3 direction = -_camera.GlobalTransform.Basis.Z.Normalized();
        Vector3 pointDeSpawn = CalculerPointSpawnLancer(direction);

        // 2. On invoque le bloc
        Node3D corpsCree = CreerBlocPose(pointDeSpawn, mainActive);

        // 3. Impulsion massique : vitesse quasi constante quelle que soit la masse (les lourds partent vraiment).
        if (corpsCree is RigidBody3D rb)
        {
            rb.Sleeping = false;
            Vector3 dir = (direction + Vector3.Up * 0.15f).Normalized();
            float v = ObtenirVitesseCibleLancer(force);
            float m = Mathf.Max(0.012f, rb.Mass);
            float bonusLeger = Mathf.Clamp(1.18f - m * 0.022f, 0.9f, 1.22f);
            rb.ApplyCentralImpulse(dir * (m * v * bonusLeger));
            if (corpsCree is ItemPhysique ipLance && ItemPhysique.EstIdRocheMatiere(mainActive.ID))
                ipLance.ActiverGraceImpactAuLancer(24);
        }

        // 4. On vide la main
        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;
        RafraichirHUD();
        ReinitialiserRotationManuelle();
    }

    /// <summary>CrÃ©e un bloc physique posÃ© avec IndexCacheMemoire assignÃ© (forme exacte conservÃ©e au rejet). Retourne le nÅ“ud crÃ©Ã© (pour lancer avec impulsion). ItemPhysique est le RigidBody3D racine.</summary>
    private Node3D CreerBlocPose(Vector3 pointDeChute, SlotInventaire mainActive)
    {
        int id = mainActive.ID;
        Node3D corps;
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            bool boisSculpte = mainActive.ID == 30 || mainActive.ID == 32;
            Vector3 scaleInv = mainActive.ScaleEclat.LengthSquared() > 1e-8f ? mainActive.ScaleEclat : Vector3.One;
            Mesh meshPose = mainActive.MeshEclat;
            Vector3 scaleRb = scaleInv;
            bool meshBoisBake = false;
            // Bois taillÃ© : cuire ScaleEclat dans les sommets (comme les Ã©clats de roche). Ã‰vite scale non uniforme sur le RigidBody3D = visuel/collision faux en jeu.
            if (boisSculpte && (scaleInv - Vector3.One).LengthSquared() > 1e-8f)
            {
                ArrayMesh baked = ItemPhysique.DupliquerMeshBakeEchelle(mainActive.MeshEclat, scaleInv);
                if (baked != null)
                {
                    meshPose = baked;
                    scaleRb = Vector3.One;
                    meshBoisBake = true;
                }
            }
            var item = new ItemPhysique
            {
                ID_Objet = mainActive.ID,
                IndexChimique = mainActive.IndexChimique,
                EstUnEclat = true,
                NiveauFracture = mainActive.NiveauFracture,
                Scale = scaleRb,
                IndexBotanique = boisSculpte ? mainActive.IndexBotanique : (byte)0,
                Name = "ItemPhysique",
                GenomeAssemblage = mainActive.GenomeAssemblage ?? ""
            };
            if (meshBoisBake)
                item.SetMeta(ItemPhysique.MetaScaleEclatInventaire, scaleInv);
            if (!string.IsNullOrEmpty(item.GenomeAssemblage))
                item.SetMeta(MetaGenomeAssemblage, item.GenomeAssemblage);
            // FIX CRITIQUE : pas de matÃ©riau gris unique â€” l'ArrayMesh forgÃ© porte ses textures par surface
            Material matVisuel = null;
            if (mainActive.ID != 100)
            {
                int chimPourRoche = ItemPhysique.EstIdRocheMatiere(mainActive.ID)
                    ? ItemPhysique.IndexChimiqueDepuisIdRoche(mainActive.ID)
                    : Mathf.Clamp(mainActive.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
                matVisuel = boisSculpte
                    ? (mainActive.ID == 32 && mainActive.IndexChimique == 1 && mainActive.IndexBotanique == LSystem_Botanique.IndexChene
                        ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                        : ArbreVivant.ObtenirMaterielBoisTriplanar(mainActive.IndexBotanique))
                    : ItemPhysique.CreerMaterielProcedural(ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID), chimPourRoche);
            }
            item.AddChild(new MeshInstance3D { Name = "MeshInstance3D", Mesh = meshPose, MaterialOverride = matVisuel });
            item.AddChild(new CollisionShape3D { Name = "CollisionShape3D", Shape = ItemPhysique.CreerShapeCollisionConvexeRobuste(meshPose) });
            corps = item;
        }
        else if (id == 105)
        {
            SlotInventaire slotDague = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotDague);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotDague.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotDague.DurabiliteOutilActuelle);
            item.SetMeta(MetaTailleLameRoche, Mathf.Clamp(slotDague.IndexTailleLameRoche <= 0 ? 2 : slotDague.IndexTailleLameRoche, 0, 4));
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotDague, 0.625f, ObtenirFacteurEchelleLameDague(slotDague));
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D
            {
                Name = "CollisionShape3D",
                Shape = new CapsuleShape3D { Radius = 0.07f, Height = 0.46f }
            });
            corps = item;
        }
        else if (id == 106)
        {
            SlotInventaire slotHachette = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotHachette);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotHachette.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotHachette.DurabiliteOutilActuelle);
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotHachette, 0.625f, 1f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D
            {
                Name = "CollisionShape3D",
                Shape = new BoxShape3D { Size = new Vector3(0.1f, 0.45f, 0.2f) },
                Position = new Vector3(0, 0.22f, 0)
            });
            corps = item;
        }
        else if (id == IdObjetPellePierreTier0)
        {
            SlotInventaire slotPelle = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotPelle);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotPelle.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotPelle.DurabiliteOutilActuelle);
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotPelle, 0.64f, 1f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D
            {
                Name = "CollisionShape3D",
                Shape = new BoxShape3D { Size = new Vector3(0.12f, 0.52f, 0.22f) },
                Position = new Vector3(0, 0.24f, 0)
            });
            corps = item;
        }
        else if (id == IdObjetPiochePierreTier0)
        {
            SlotInventaire slotPioche = mainActive;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slotPioche);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = mainActive.IndexTaille,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            item.SetMeta(MetaDurabiliteOutilMax, slotPioche.DurabiliteOutilMax);
            item.SetMeta(MetaDurabiliteOutilActuelle, slotPioche.DurabiliteOutilActuelle);
            if (!string.IsNullOrEmpty(slotPioche.GenomeAssemblage))
            {
                item.GenomeAssemblage = slotPioche.GenomeAssemblage;
                item.SetMeta(MetaGenomeAssemblage, slotPioche.GenomeAssemblage);
            }
            var meshRoot = new MeshInstance3D { Name = "MeshInstance3D" };
            InstancierModeleArme(meshRoot, slotPioche, 0.65f, 1f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D
            {
                Name = "CollisionShape3D",
                Shape = new BoxShape3D { Size = new Vector3(0.13f, 0.54f, 0.22f) },
                Position = new Vector3(0, 0.24f, 0)
            });
            corps = item;
        }
        else if (id == IdObjetRackBatons || id == IdObjetRackBuches)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            if (!string.IsNullOrEmpty(mainActive.GenomeAssemblage))
            {
                item.GenomeAssemblage = mainActive.GenomeAssemblage;
                item.SetMeta(MetaGenomeAssemblage, mainActive.GenomeAssemblage);
            }
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (id == IdObjetRackBatons) InstancierModeleRackBatons(meshRoot, mainActive, 1.05f, true);
            else InstancierModeleRackBuches(meshRoot, mainActive, 1.05f, true);
            item.AddChild(meshRoot);
            // MÃªme logique que la table (200) : collisions exactes depuis les meshes pour Ã©viter la lÃ©vitation.
            var pileRack = new List<Node> { meshRoot };
            for (int i = 0; i < pileRack.Count; i++)
            {
                foreach (Node c in pileRack[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pileRack.Add(c);
                }
            }
            // Fallback sÃ©curitÃ© si jamais le GLB ne retourne aucune surface exploitable.
            if (item.GetChildCount() <= 1)
                item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.9f, 0.68f, 0.52f) }, Position = new Vector3(0f, 0.34f, 0f) });
            string cle = !string.IsNullOrEmpty(mainActive.CleConteneur) ? mainActive.CleConteneur : Guid.NewGuid().ToString("N");
            item.SetMeta("CleConteneur", cle);
            corps = item;
        }
        else if (id == 200)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                Freeze = true,
                FreezeMode = RigidBody3D.FreezeModeEnum.Static
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            // FIX CRITIQUE : point zÃ©ro aux pieds du meuble (ancrerBaseAuSol = true), ~1,2 m sur la plus grande dimension.
            InstancierModeleAtelierPrimitif(meshRoot, mainActive, 1.2f, true);
            item.AddChild(meshRoot);

            var pile = new List<Node> { meshRoot };
            for (int i = 0; i < pile.Count; i++)
            {
                foreach (Node c in pile[i].GetChildren())
                {
                    if (c is MeshInstance3D mi && mi.Mesh != null)
                    {
                        Shape3D shape = mi.Mesh.CreateTrimeshShape();
                        if (shape != null)
                        {
                            Transform3D t = mi.Transform;
                            Node parentNode = mi.GetParent();
                            while (parentNode != null && parentNode != item && parentNode is Node3D n3d)
                            {
                                t = n3d.Transform * t;
                                parentNode = parentNode.GetParent();
                            }
                            var colNode = new CollisionShape3D { Shape = shape, Transform = t };
                            item.AddChild(colNode);
                        }
                    }
                    pile.Add(c);
                }
            }
            corps = item;
        }
        else if (ItemPhysique.EstIdRocheMatiere(id))
        {
            float tailleBase = mainActive.IndexTaille switch { 0 => 0.08f, 1 => 0.15f, 2 => 0.25f, 3 => 0.40f, 4 => 0.65f, _ => 0.2f };

            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(id),
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = Mathf.Clamp(mainActive.IndexTaille, 0, 4),
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };

            Vector3 scaleForme = Vector3.One;
            if (mainActive.IndexMorphologique == 1) scaleForme = new Vector3(1f, 0.4f, 1f);
            else if (mainActive.IndexMorphologique == 2) scaleForme = new Vector3(1f, 0.7f, 1.4f);
            else if (mainActive.IndexMorphologique == 3) scaleForme = new Vector3(0.6f, 1.3f, 0.6f);

            var sphereMesh = new SphereMesh { Radius = tailleBase, Height = tailleBase * 2f };
            Mesh finalMesh = sphereMesh;
            Shape3D colShape;

            if (mainActive.IndexMorphologique == 0)
            {
                colShape = new SphereShape3D { Radius = tailleBase };
            }
            else
            {
                Godot.Collections.Array arrays = sphereMesh.GetMeshArrays();
                Vector3[] vertices = ((Variant)arrays[(int)Mesh.ArrayType.Vertex]).AsVector3Array();
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new Vector3(vertices[i].X * scaleForme.X, vertices[i].Y * scaleForme.Y, vertices[i].Z * scaleForme.Z);
                }
                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                var bakedMesh = new ArrayMesh();
                bakedMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                finalMesh = bakedMesh;
                colShape = bakedMesh.CreateConvexShape(true, true);
            }

            var meshNode = new MeshInstance3D { Name = "MeshInstance3D", Mesh = finalMesh };
            AppliquerMaterielObjet(meshNode, id, ItemPhysique.IndexChimiqueDepuisIdRoche(id), 0, 0);

            item.AddChild(meshNode);
            item.AddChild(new CollisionShape3D { Name = "CollisionShape3D", Shape = colShape });
            item.SetMeta(ItemPhysique.MetaRocheForgeeParJoueur, true);
            corps = item;
        }
        else if (id == 15 || id == 16 || id == 17) // Fibres flexibles : fagot de brins (teinte selon profil)
        {
            Color teinte = Atlas_Matiere.ObtenirProfilFlexible(id, out var profilF)
                ? profilF.CouleurCorde
                : new Color(0.35f, 0.55f, 0.15f);
            var item = new ItemPhysique { ID_Objet = id, Name = "ItemPhysique" };
            var matFibre = new StandardMaterial3D { AlbedoColor = teinte, Roughness = 0.9f, Metallic = 0f };
            float l = id == 17 ? 0.42f : 0.38f;
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * Mathf.Pi * 0.6f - 0.15f;
                float x = Mathf.Sin(a) * 0.025f; float z = Mathf.Cos(a) * 0.025f;
                var mi = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.01f, Height = l - 0.02f }, MaterialOverride = matFibre, Position = new Vector3(x, l * 0.5f, z), Rotation = new Vector3(0.08f * (i - 3), 0.1f * (i % 2 - 0.5f), 0.06f * (i - 2)) };
                item.AddChild(mi);
            }
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.12f, l, 0.12f) }, Position = new Vector3(0, l * 0.5f, 0) });
            corps = item;
        }
        else if (id == 20) // Tressage / corde tier 0 : modÃ¨le GLB + mÃªmes matÃ©riaux procÃ©duraux que lâ€™inventaire.
        {
            int idA = mainActive.IndexChimique, idB = mainActive.IndexMorphologique;
            var item = new ItemPhysique { ID_Objet = id, IndexChimique = idA, IndexCacheMemoire = idB, NiveauFracture = mainActive.NiveauFracture, Name = "ItemPhysique" };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCordeTier0Gazon(meshRoot, mainActive, 0.32f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.045f, Height = 0.28f } });
            corps = item;
        }
        else if (id == 21) // Tissu tier 0 : 4 cordes tissÃ©es â€” GLB + mÃªme matiÃ¨re plate que la corde.
        {
            int idA = mainActive.IndexChimique, idB = mainActive.IndexMorphologique;
            var item = new ItemPhysique { ID_Objet = id, IndexChimique = idA, IndexCacheMemoire = idB, NiveauFracture = mainActive.NiveauFracture, Name = "ItemPhysique" };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleTissuTier0(meshRoot, mainActive, 0.34f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.32f, 0.06f, 0.32f) } });
            corps = item;
        }
        else if (id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches) // 102 = ceinture seule ; 104 = GLB avec poches + stockage persistant.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            if (!string.IsNullOrEmpty(mainActive.CleConteneur))
                item.SetMeta("CleConteneur", mainActive.CleConteneur);
            if (!string.IsNullOrEmpty(mainActive.GenomeAssemblage))
            {
                item.GenomeAssemblage = mainActive.GenomeAssemblage;
                item.SetMeta(MetaGenomeAssemblage, mainActive.GenomeAssemblage);
            }
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (id == IdObjetCeintureSacoches)
                InstancierModeleCeintureSacoches(meshRoot, mainActive, 0.42f);
            else
                InstancierModeleCeinturePoches(meshRoot, mainActive, 0.4f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = id == IdObjetCeintureSacoches ? new Vector3(0.52f, 0.12f, 0.32f) : new Vector3(0.42f, 0.09f, 0.28f) } });
            corps = item;
        }
        else if (id == IdObjetPochetteTier0) // Pochette tier 0 : tissu + corde, mÃªme matiÃ¨re procÃ©durale que ceinture.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModelePochetteTier0(meshRoot, mainActive, 0.36f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.24f, 0.08f, 0.2f) } });
            corps = item;
        }
        else if (id == IdObjetSacTier0) // Sac tier 0 : modÃ¨le dÃ©diÃ© + matiÃ¨re corde/tissu.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexBotanique = mainActive.IndexBotanique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            if (!string.IsNullOrEmpty(mainActive.CleConteneur))
                item.SetMeta("CleConteneur", mainActive.CleConteneur);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleSacTier0(meshRoot, mainActive, 0.4f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.36f, 0.14f, 0.28f) } });
            corps = item;
        }
        else if (id == 30 || id == 32)
        {
            int f = Mathf.Clamp(mainActive.IndexMorphologique, 0, 3);
            CalculerDimensionsBoisPose(id, mainActive.IndexMorphologique, mainActive.IndexTaille, out float br, out float baseLengthCalc, out float w, out float hh);
            float bl = baseLengthCalc;
            if (mainActive.ScaleEclat.Z > 0.1f)
                bl = baseLengthCalc * mainActive.ScaleEclat.Z;
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexBotanique = mainActive.IndexBotanique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = Mathf.Clamp(mainActive.IndexTaille, 0, 4),
                IndexChimique = mainActive.IndexChimique,
                Name = "ItemPhysique",
                ContinuousCd = true,
                NiveauFracture = 0
            };
            item.SetMeta("ScaleLongueurBois", bl / Mathf.Max(0.001f, baseLengthCalc));
            Mesh meshObj = GenererMeshBoisFendu(br, bl, mainActive.IndexMorphologique);
            Shape3D colObj;
            if (f == 0)
            {
                colObj = new CylinderShape3D { Radius = br, Height = bl };
            }
            else
            {
                // FIX CRITIQUE : Une BoÃ®te statique est beaucoup plus stable qu'un ConvexShape pour Jolt.
                // Elle englobe le morceau coupÃ© et empÃªche le passage Ã  travers la terre.
                float wCol = br * 2f; float hCol = br;
                if (f == 2) { wCol = br; hCol = br; }
                else if (f >= 3) { wCol = br; hCol = br * 0.4f; }
                colObj = new BoxShape3D { Size = new Vector3(wCol, hCol, bl) };
            }
            var meshNode = new MeshInstance3D
            {
                Mesh = meshObj,
                MaterialOverride = id == 32 && mainActive.IndexChimique == 1 && mainActive.IndexBotanique == LSystem_Botanique.IndexChene
                    ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                    : ArbreVivant.ObtenirMaterielBoisTriplanar(mainActive.IndexBotanique)
            };
            meshNode.RotationDegrees = new Vector3(90f, 0f, 0f);
            var colNode = new CollisionShape3D { Shape = colObj };
            colNode.RotationDegrees = new Vector3(90f, 0f, 0f);
            item.AddChild(meshNode);
            item.AddChild(colNode);
            corps = item;
        }
        else if (id == 34) // Feuilles arrachÃ©es (mÃªme mesh que le feuillage d'arbre)
        {
            var matFeuilles = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.55f, 0.15f), Roughness = 0.95f, Metallic = 0f };
            var bloc = BlocChutant.CreerFeuillageArrache(pointDeChute, matFeuilles);
            corps = bloc;
        }
        else if (id == 10 || id == 11 || id == BlocChutant.ID_BRANCHE)
        {
            var mat = new StandardMaterial3D { AlbedoColor = new Color(0.38f, 0.46f, 0.2f), Roughness = 0.92f, Metallic = 0f };
            corps = BlocChutant.Creer(pointDeChute, (byte)id, mat);
        }
        else if (id == IdObjetBaie)
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = 0,
                Name = "ItemPhysique",
                ContinuousCd = true
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleBaie(meshRoot, mainActive, 0.22f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.08f } });
            corps = item;
        }
        else // 999 Buisson â€” RigidBody3D pour pouvoir le lancer comme les autres objets posÃ©s.
        {
            float cote = 0.85f;
            var rb = new RigidBody3D { Mass = cote * cote * cote * 190f, ContinuousCd = true };
            rb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(cote, cote, cote) } });
            rb.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(cote, cote, cote) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.8f, 0.2f) } });
            corps = rb;
        }
        corps.SetMeta("ID_Matiere", id);
        corps.AddToGroup("BlocsPoses");
        GetParent().AddChild(corps);
        // Placement pur : pas de translation Y supplÃ©mentaire (Ã©vite double offset / lÃ©vitation atelier).
        corps.GlobalPosition = pointDeChute;
        if (id == IdObjetRackBatons || id == IdObjetRackBuches)
        {
            // Snap sol robuste pour le rack: corrige les cas oÃ¹ le raycast vise une surface dÃ©calÃ©e.
            var espace = GetWorld3D()?.DirectSpaceState;
            if (espace != null)
            {
                Vector3 origine = corps.GlobalPosition + Vector3.Up * 4f;
                Vector3 dest = corps.GlobalPosition + Vector3.Down * 8f;
                var q = PhysicsRayQueryParameters3D.Create(origine, dest);
                if (corps is CollisionObject3D coRack)
                    q.Exclude = new Godot.Collections.Array<Rid> { coRack.GetRid() };
                q.CollideWithAreas = false;
                var hit = espace.IntersectRay(q);
                if (hit.Count > 0 && hit.ContainsKey("position"))
                {
                    Aabb? box = null;
                    AccumulerAabbMeshes(corps, Transform3D.Identity, ref box);
                    if (box.HasValue)
                    {
                        float minY = box.Value.Position.Y;
                        float hitY = ((Vector3)hit["position"]).Y;
                        corps.GlobalPosition += Vector3.Up * (hitY - minY + 0.005f);
                    }
                }
            }
        }
        // MÃªme calque que le terrain PhysicsServer3D / StaticBody (bit 1) : collision fiable au sol.
        if (corps is RigidBody3D rbPose)
        {
            rbPose.CollisionLayer = 1;
            rbPose.CollisionMask = 1;
            rbPose.ContinuousCd = true;

            if (ItemPhysique.EstIdRocheMatiere(id))
            {
                int morphR = Mathf.Clamp(mainActive.IndexMorphologique, 0, 3);
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                if (morphR == 0)
                {
                    rbPose.PhysicsMaterialOverride = _physMatRocheRonde;
                    rbPose.LinearDamp = 0.04f;
                    rbPose.AngularDamp = 0.04f;
                }
                else if (morphR == 1)
                {
                    rbPose.PhysicsMaterialOverride = _physMatRochePlate;
                    rbPose.LinearDamp = 0.38f;
                    rbPose.AngularDamp = 1.55f;
                }
                else if (morphR == 2)
                {
                    rbPose.PhysicsMaterialOverride = _physMatRocheOvale;
                    rbPose.LinearDamp = 0.11f;
                    rbPose.AngularDamp = 0.3f;
                }
                else
                {
                    rbPose.PhysicsMaterialOverride = _physMatRochePointe;
                    rbPose.LinearDamp = 0.2f;
                    rbPose.AngularDamp = 0.88f;
                }
            }
            else if (id == 30 || id == 32 || id == 200 || id == IdObjetRackBatons || id == IdObjetRackBuches)
            {
                rbPose.PhysicsMaterialOverride = _physMatBois;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.06f;
                rbPose.AngularDamp = 0.42f;
                if (id == 200)
                {
                    // TrÃ¨s lourd + pas de gravitÃ© : Ã©vite tout glissement / dÃ©rive si le moteur rÃ©veille le corps un instant.
                    rbPose.Mass = 2800f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
                else if (id == IdObjetRackBatons || id == IdObjetRackBuches)
                {
                    rbPose.Mass = 1200f;
                    rbPose.GravityScale = 0f;
                    rbPose.Sleeping = true;
                }
            }
            else if (id is >= 15 and <= 17)
            {
                rbPose.PhysicsMaterialOverride = _physMatFibre;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.42f;
                rbPose.AngularDamp = 1.0f;
            }
            else if (id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0)
            {
                rbPose.PhysicsMaterialOverride = _physMatCorde;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.32f;
                rbPose.AngularDamp = 0.95f;
            }
            else if (id == 999)
            {
                rbPose.PhysicsMaterialOverride = _physMatVegetalLache;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.28f;
                rbPose.AngularDamp = 0.75f;
            }
            else if (mainActive.EstUnEclat)
            {
                // Bois 30/32 et roches 40â€“49 sont dÃ©jÃ  couverts plus haut ; ici : outil forgÃ© (100) et autres Ã©clats.
                if (id == 100)
                {
                    rbPose.PhysicsMaterialOverride = _physMatMetalForge;
                    rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.LinearDamp = 0.12f;
                    rbPose.AngularDamp = 0.55f;
                }
                else
                {
                    rbPose.PhysicsMaterialOverride = _physMatDefautObjet;
                    rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                    rbPose.LinearDamp = 0.18f;
                    rbPose.AngularDamp = 0.65f;
                }
            }
            if (id == 105 && rbPose is ItemPhysique ipDague)
                ItemPhysique.AppliquerPhysiqueDague105(ipDague);
            else if (id == 106 && rbPose is ItemPhysique ipHachette)
                ItemPhysique.AppliquerPhysiqueHachette106(ipHachette);
            else if (id == IdObjetPellePierreTier0 && rbPose is ItemPhysique ipPelle)
                ItemPhysique.AppliquerPhysiquePelle107(ipPelle);
            else if (id == IdObjetPiochePierreTier0 && rbPose is ItemPhysique ipPioche)
                ItemPhysique.AppliquerPhysiquePioche108(ipPioche);
        }
        // Fibres / corde non Ã©lastiques : ne pas appliquer dâ€™Ã©chelle Â« Ã©tirÃ©e Â» (herbe, liane, corde boyau+herbe, etc.)
        bool estFlexOuCorde = id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(mainActive))
            corps.Scale = Vector3.One;
        else if (!ItemPhysique.EstIdRocheMatiere(id) && id != 30 && id != 32 && mainActive.ScaleEclat != Vector3.Zero)
            corps.Scale = mainActive.ScaleEclat;
        return corps;
    }

    private float _tempsAttenteSpawn;
    private bool _verrouSpawnActif = true;

    /// <summary>Recherche une couche d'eau dont la case au-dessus n'est pas de l'eau: donne la hauteur de surface (face haute voxel).</summary>
    private bool EssayerTrouverSurfaceEauY(Vector3 centreRecherche, out float surfaceY)
    {
        surfaceY = 0f;
        if (_gestionnaireMonde == null) return false;

        // Recherche locale verticale autour du joueur: robuste si le niveau d'eau varie lÃ©gÃ¨rement.
        for (int dy = 6; dy >= -8; dy--)
        {
            Vector3 p = centreRecherche + Vector3.Up * dy;
            int id = _gestionnaireMonde.ObtenirMatiereExacte(p);
            if (id != 4) continue;
            int idAuDessus = _gestionnaireMonde.ObtenirMatiereExacte(p + Vector3.Up);
            if (idAuDessus == 4) continue;
            surfaceY = Mathf.Floor(p.Y) + 1.0f;
            return true;
        }
        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        bool caoOuvert = _modelisateur != null && _modelisateur.EstOuvert;

        if (!caoOuvert)
        {
            if (!mainActive.EstVide && Input.IsActionPressed("clic_droit"))
                _forceLancer = Mathf.Min(5.0f, _forceLancer + (VitesseChargeBras * 2.5f) * dt);
            if (_gaucheMaintenu && (mainActive.EstVide || mainActive.ID == 105 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0))
                MettreAJourMinageMainNueOuAtelier(dt, mainActive);
            else
                ReinitialiserMinageMainNueProgression();
        }
        else
        {
            if (_gaucheMaintenu) _gaucheMaintenu = false;
            _forceLancer = 0f;
            ReinitialiserMinageMainNueProgression();
        }

        Vector3 velocity = Velocity;
        bool spawnPret = _gestionnaireMonde == null || _gestionnaireMonde.EstSpawnPret();
        bool spawnAligneAuSol = _gestionnaireMonde == null || _gestionnaireMonde.EstAlignementSpawnTermine();
        if (_verrouSpawnActif)
        {
            // Attendre aussi le raycast + pose au sol (Gestionnaire _Process), pas seulement la collision du chunk :
            // sinon une frame de physique avec Y Â« ciel Â» + gravitÃ© = traversÃ©e du mesh.
            if (!spawnPret || !spawnAligneAuSol)
            {
                _tempsAttenteSpawn += dt;
                // Anti soft-lock: si le sol/collision tarde trop, on rend le contrÃ´le au joueur.
                if (_tempsAttenteSpawn <= 8f)
                {
                    int idCorps = _gestionnaireMonde?.ObtenirMatiereExacte(GlobalPosition + Vector3.Up * 0.8f) ?? 1;
                    bool eauCorps = idCorps == 4;
                    velocity.X = 0f;
                    velocity.Y = 0f;
                    velocity.Z = 0f;
                    Velocity = velocity;
                    MoveAndSlide();
                    AppliquerContrainteVerticaleHauteurTerrainMonde(eauCorps, ignorerSiMonteeSaut: false, dt);
                    return;
                }
                GD.PrintErr("ZERO-K : DÃ©verrouillage dÃ©placement forcÃ© (spawn non prÃªt trop longtemps).");
                _verrouSpawnActif = false;
            }
            else
            {
                _verrouSpawnActif = false;
            }
            _tempsAttenteSpawn = 0f;
        }

        int idMilieu = _gestionnaireMonde?.ObtenirMatiereExacte(GlobalPosition + Vector3.Up * 0.8f) ?? 1;
        bool estDansEau = (idMilieu == 4);
        bool sautMaintenu = !caoOuvert && (Input.IsActionPressed("ui_accept") || Input.IsActionPressed("jump"));

        if (IsOnFloor())
        {
            _bufferSolCoyoteAnim = 0.18f;
            _bufferCoyoteSaut = 0.28f;
        }
        else
        {
            _bufferSolCoyoteAnim = Mathf.Max(0f, _bufferSolCoyoteAnim - dt);
            _bufferCoyoteSaut = Mathf.Max(0f, _bufferCoyoteSaut - dt);
        }

        _tamponSautRestant = Mathf.Max(0f, _tamponSautRestant - dt);
        if (!caoOuvert && (Input.IsActionJustPressed("jump") || Input.IsActionJustPressed("ui_accept")))
            _tamponSautRestant = Mathf.Max(_tamponSautRestant, DureeTamponSautSecondes);

        bool auSolPourAnim = IsOnFloor() || _bufferSolCoyoteAnim > 0f;
        bool solAccepteSaut = IsOnFloor() || (_bufferCoyoteSaut > 0f && velocity.Y <= 0.05f);

        if (estDansEau)
        {
            velocity.X *= 0.92f;
            velocity.Z *= 0.92f;
            velocity.Y *= 0.96f;

            if (sautMaintenu && EssayerTrouverSurfaceEauY(GlobalPosition + Vector3.Up * 0.3f, out float surfaceEau))
            {
                // Maintien stable Ã  la surface quand on garde saut: Ã©vite l'effet yo-yo vertical.
                float cibleY = surfaceEau - 0.28f;
                float erreur = cibleY - GlobalPosition.Y;
                float vitesseVerticaleCible = Mathf.Clamp(erreur * 5.0f, -1.15f, 2.1f);
                velocity.Y = Mathf.MoveToward(velocity.Y, vitesseVerticaleCible, 9.0f * dt);
            }
            else if (sautMaintenu)
                velocity.Y += JumpVelocity * 0.7f * dt;
            else
                velocity.Y -= 1.5f * dt;
        }
        else if (!IsOnFloor())
        {
            velocity += GetGravity() * dt;
        }

        bool sautDepuisSolStable = !estDansEau
            && _tamponSautRestant > 0f
            && solAccepteSaut;
        if (!caoOuvert && sautDepuisSolStable)
        {
            velocity.Y = JumpVelocity;
            _tamponSautRestant = 0f;
            _bufferCoyoteSaut = 0f;
        }

        Vector2 inputDir = caoOuvert ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        Vector3 direction = CalculerDirectionMouvementAuSol(inputDir);
        float vitesseMouvement = estDansEau ? Speed * (sautMaintenu ? 0.58f : 0.4f) : Speed;
        vitesseMouvement *= ObtenirFacteurVitesseSelonChargePortee();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * vitesseMouvement;
            velocity.Z = direction.Z * vitesseMouvement;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, vitesseMouvement);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, vitesseMouvement);
        }

        MettreAJourAnimationHumain(dt, velocity, inputDir, auSolPourAnim);
        MettreAJourObjetTenueTps();

        Velocity = velocity;
        MoveAndSlide();
        // DÃ©sactivÃ© en jeu normal : peut provoquer un "TP au sol" en retombÃ©e.
        if (_verrouSpawnActif)
            EssayerCollerCapsuleAuSolTerrain(estDansEau);
        AppliquerContrainteVerticaleHauteurTerrainMonde(estDansEau, ignorerSiMonteeSaut: true, dt);
    }
}

