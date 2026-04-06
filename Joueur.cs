using Godot;
using System;
using System.Collections.Generic;

    /// <summary>Slot d'inventaire avec ADN morphologique (forme) et chimique (composition).</summary>
    public struct SlotInventaire
    {
        public int ID;
        public int IndexMorphologique;
        /// <summary>Roche : indice minéral. Bâton (32) : 0 = branche brute, 1 = bâton de chêne façonné (craft, teinte plus pâle).</summary>
        public int IndexChimique;
    /// <summary>Grosseur roche matière (40–49) : 0=Mini, 1=Petite, 2=Moyenne, 3=Grosse, 4=Énorme.</summary>
    public int IndexTaille;
    /// <summary>True si le slot contient un éclat de fracture (mesh dynamique, pas dans le cache).</summary>
    public bool EstUnEclat;
    /// <summary>Mesh sauvegardé pour les éclats (sinon null).</summary>
    public Mesh MeshEclat;
    /// <summary>Nombre de fractures subies (0 = intact). Conservé au ramassage/lancer pour poudre au-delà de 5.</summary>
    public int NiveauFracture;
    /// <summary>Échelle de l'éclat au ramassage (évite qu'il grossisse au relancer).</summary>
    public Vector3 ScaleEclat;
    /// <summary>Essence de bois (0 = chêne pour l'instant). Utilisé pour ID 30 (bûche) et 32 (bâton). En prévision des futurs arbres.</summary>
    public byte IndexBotanique;
    /// <summary>Identité d’assemblage CAO (séquence pièces / poses). Vide si non forgé ou héritage sans donnée.</summary>
    public string GenomeAssemblage;
    /// <summary>Dague primitive (105) : durabilité max (lame minérale + manche corde). 0 = non initialisé.</summary>
    public float DurabiliteOutilMax;
    /// <summary>Dague primitive : points restants. À 0 l’arme est cassée.</summary>
    public float DurabiliteOutilActuelle;
    /// <summary>Dague 105 : taille de la roche en pointe (0–4) utilisée au craft — échelle visuelle de la lame. Défaut 2 si absent.</summary>
    public int IndexTailleLameRoche;
    /// <summary>Quantité stackée dans le slot (base 1).</summary>
    public int Quantite;
    /// <summary>Clé de conteneur persistante (ex: sac tier 0) pour mémoriser son contenu même déséquipé.</summary>
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
    /// <summary>Méta et slots : même clé pour l’établi CAO et les corps posés.</summary>
    public const string MetaGenomeAssemblage = "GenomeAssemblage";
    /// <summary>ItemPhysique dague (105) : durabilité synchronisée inventaire / sol.</summary>
    public const string MetaDurabiliteOutilMax = "DurOutilMax";
    public const string MetaDurabiliteOutilActuelle = "DurOutilAct";
    public const string MetaTailleLameRoche = "TailleLameRoche";
    /// <summary>Sac tier 0 équipable (slot dos) : 1 case de stockage persistante.</summary>
    public const int IdObjetSacTier0 = 101;
    /// <summary>Alias historique (= <see cref="IdObjetSacTier0"/>).</summary>
    public const int IdObjetSacDos = 101;
    /// <summary>Ceinture tissée (102) : slot corps uniquement, sans stockage.</summary>
    public const int IdObjetCeinturePoches = 102;
    /// <summary>Pochette tier 0 (matériau) : craft atelier, même rendu corde/tissu que la ceinture.</summary>
    public const int IdObjetPochetteTier0 = 103;
    /// <summary>Ceinture à sacoches (104) : slot corps ; 4 cases de stockage persistantes.</summary>
    public const int IdObjetCeintureSacoches = 104;
    /// <summary>Pelle en pierre tier 0.</summary>
    public const int IdObjetPellePierreTier0 = 107;
    /// <summary>Pioche en pierre tier 0.</summary>
    public const int IdObjetPiochePierreTier0 = 108;
    /// <summary>Petite baie récoltable sur buisson (palette couleur via IndexChimique).</summary>
    public const int IdObjetBaie = 35;

    /// <summary>True si cet ID est un contenant porté qui ouvre la grille « sac » dans l’UI.</summary>
    public static bool EstObjetQuiDebloqueGrilleSac(int id) => id == IdObjetSacDos;

    /// <summary>Stats des outils forgés (CAO) : clé = <see cref="HashGenomeStable"/> du genome si présent, sinon GetHashCode du mesh (héritage).</summary>
    public struct StatsOutilForge
    {
        public float Masse;
        public float EpaisseurLameBase;
        public Vector3 AxeTranchantLocal;
        public string Nom;
    }

    public static Dictionary<int, StatsOutilForge> RegistreOutilsForges = new Dictionary<int, StatsOutilForge>();

    /// <summary>Clé déterministe pour <see cref="RegistreOutilsForges"/> (même chaîne → même int, toute session).</summary>
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

    /// <summary>Clé registre outil forgé : genome prioritaire, sinon mesh en mémoire.</summary>
    public static int ClefRegistreOutilForge(SlotInventaire mainActive)
    {
        if (mainActive.ID != 100 || !mainActive.EstUnEclat || mainActive.MeshEclat == null) return 0;
        if (!string.IsNullOrEmpty(mainActive.GenomeAssemblage))
            return HashGenomeStable(mainActive.GenomeAssemblage);
        return mainActive.MeshEclat.GetHashCode();
    }

    public enum TypeMouvementFrappe { Estoc, DeHautEnBas, DeBasEnHaut, GaucheADroite, DroiteAGauche }

    public const float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;

    // Sensibilité chirurgicale de la souris
    public const float MouseSensitivity = 0.003f;

    /// <summary>Rayon du pinceau de sculpture (minage ET pose). Symétrie absolue.</summary>
    private const float RAYON_SCULPTURE = 1.0f;

    /// <summary>Rayon de fauchage de la flore (gazon) avant pose de l’atelier primitif — même ordre d’idée que la lame sur le sol.</summary>
    private const float RayonFauchagePoseAtelier200 = 2.75f;

    /// <summary>Mains avec ADN morphologique : la pierre conserve sa forme exacte.</summary>
    public SlotInventaire MainGauche = new SlotInventaire();
    public SlotInventaire MainDroite = new SlotInventaire();
    /// <summary>True = main gauche active, false = main droite.</summary>
    // FIX CRITIQUE : La main droite est la main dominante par défaut (logique humaine standard)
    public bool MainGaucheEstActive = false;

    /// <summary>Sac au dos équipé (slot dédié, pas les mains). Assigner via <see cref="AssignerEquipementSacDos"/>.</summary>
    public SlotInventaire EquipementSacDos = new SlotInventaire();
    /// <summary>Ceinture à poches équipée.</summary>
    public SlotInventaire EquipementCeinture = new SlotInventaire();

    /// <summary>Craft 2×2 dans le menu inventaire (Q) — jamais mélangé avec la grille de l’établi posé.</summary>
    public SlotInventaire[] GrilleCraftPoche = new SlotInventaire[4];
    /// <summary>Stockage sac (1 case). Le contenu vit dans l'objet sac via <see cref="CleConteneur"/>.</summary>
    public SlotInventaire[] GrilleSacStockage = new SlotInventaire[1];
    /// <summary>Stockage ceinture à sacoches (104) : 4 cases, même dictionnaire de mémoire que le sac.</summary>
    public SlotInventaire[] GrilleCeintureStockage = new SlotInventaire[4];
    private readonly Dictionary<string, SlotInventaire[]> _memoireStockageSacs = new Dictionary<string, SlotInventaire[]>();

    /// <summary>Atelier (ItemPhysique 200) dont le plan 3×3 est affiché ; null en mode poche.</summary>
    public ItemPhysique AtelierPlanTravailOuvert;

    /// <summary>True si le menu a été ouvert depuis l’atelier posé : recettes et UI en 3×3. False après Q ou fermeture du menu.</summary>
    public bool CraftGrille3x3AuTable { get; set; }

    /// <summary>Slot contenant le résultat d'une recette valide.</summary>
    public SlotInventaire SlotResultatCraft = new SlotInventaire();

    private Camera3D _camera;
    private RayCast3D _rayon;
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
    /// <summary>Clic gauche : maintien pour enregistrer le swipe avant relâchement.</summary>
    private bool _gaucheMaintenu = false;
    private Vector2 _mouvementSourisCumule = Vector2.Zero;
    private Tween _tweenFrappe;
    private AudioStreamPlayer3D _audioCoupeArbre;
    private Modelisateur_UI _modelisateur;
    private MenuAnatomie _menuAnatomie;
    private Control _racineMenuAnatomieViewport;

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

        _camera = GetNode<Camera3D>("Camera3D");
        _rayon = GetNode<RayCast3D>("Camera3D/RayCast3D");
        _rayon.TargetPosition = new Vector3(0f, 0f, -12f);
        _rayon.CollisionMask = 0xFFFFFFFF; // Toutes les couches (sol AAA = layer 1, objets, eau…)
        _rayon.AddException(this); // Ne pas toucher le joueur (sinon le "minage" ne vise pas le sol)
        _gestionnaireMonde = GetParent().GetNode<Gestionnaire_Monde>("Gestionnaire_Monde");
        _slotGauche = GetParent().GetNode<Panel>("Gestionnaire_Monde/HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Gauche");
        _slotDroite = GetParent().GetNode<Panel>("Gestionnaire_Monde/HUD_Inventaire/Conteneur_Ancrage/Boite_Slots/Slot_Main_Droite");
        InsererNomsAuDessusSlotsHud();

        CreerObjetEnMain3D();
        CreerPreviewsInventaire3D();

        _modelisateur = new Modelisateur_UI();
        // Le parent (Monde_Zero) est encore en _Ready : add_child direct échoue → différé.
        CallDeferred(nameof(BrancherModelisateurCAO));

        RafraichirHUD();

        PackedScene sceneMenu = GD.Load<PackedScene>("res://Scenes/UI/MenuAnatomie.tscn");
        if (sceneMenu != null)
        {
            _menuAnatomie = sceneMenu.Instantiate<MenuAnatomie>();
            // Calque 100 : le menu pause du Gestionnaire est en 101 pour s’afficher par-dessus l’inventaire.
            var layerAnatomie = new CanvasLayer { Layer = 100, Name = "LayerAnatomie", ProcessMode = ProcessModeEnum.Always };
            // Un Control sous CanvasLayer sans parent Control n’obtient pas la taille du viewport → UI réduite à un coin.
            var racineViewport = new Control { Name = "RacineMenuAnatomieViewport" };
            racineViewport.MouseFilter = Control.MouseFilterEnum.Ignore;
            _racineMenuAnatomieViewport = racineViewport;
            AddChild(layerAnatomie);
            layerAnatomie.AddChild(racineViewport);
            // Le menu s’initialise en _Ready : si le parent est encore 0×0, tout l’UI reste coincé au coin.
            AjusterRacineMenuAnatomieViewport();
            racineViewport.AddChild(_menuAnatomie);
            _menuAnatomie.Initialiser(this);
            CallDeferred(nameof(AjusterRacineMenuAnatomieViewport));
            if (GetViewport() != null)
                GetViewport().SizeChanged += OnViewportTailleMenuAnatomie;
        }
    }

    private void BrancherModelisateurCAO()
    {
        if (_modelisateur == null) return;
        Node parent = GetParent();
        if (parent == null) return;
        parent.AddChild(_modelisateur);
        _modelisateur.Initialiser(this);
    }

    /// <summary>Le parent CanvasLayer n’a pas de rectangle : sans ça, ancres FullRect = 0×0 et tout l’UI part en coin.</summary>
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

    /// <summary>MeshInstance3D attaché à la caméra pour afficher l'objet tenu en main (forme exacte).</summary>
    private void CreerObjetEnMain3D()
    {
        _objetEnMain = new MeshInstance3D();
        _objetEnMain.Position = new Vector3(0.3f, -0.25f, -0.8f);
        _objetEnMain.RotationDegrees = new Vector3(-15, 10, 5);
        _objetEnMain.Scale = Vector3.One * 0.5f;
        _camera.AddChild(_objetEnMain);
    }

    /// <summary>Libellés au-dessus de chaque slot (nom de l’objet pour repérer les erreurs de données).</summary>
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
        light.Set("sky_mode", 1); // LightOnly : pas de disque dans le ciel (évite 2e soleil blanc dans SubViewport)
        viewport.AddChild(light);

        return container;
    }

    private void ReinitialiserRotationManuelle()
    {
        _rotationManuelleX = 0f;
        _rotationManuelleY = 0f;
        _rotationManuelleZ = 0f;
    }

    public override void _Input(InputEvent @event)
    {
        if (_menuAnatomie != null && @event.IsActionPressed("inventaire"))
        {
            if (!_menuAnatomie.EstOuvert)
            {
                CraftGrille3x3AuTable = false;
                AtelierPlanTravailOuvert = null;
            }
            _menuAnatomie.BasculerVisibilite();
            RafraichirHUD();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
        {
            // Échap / ui_cancel : ouvrir le menu pause (bloque le jeu) — ne pas fermer l’inventaire.
            if (@event.IsActionPressed("ui_cancel") ||
                (@event is InputEventKey ekEsc && ekEsc.Pressed && !ekEsc.Echo && ekEsc.Keycode == Key.Escape))
            {
                GetParent()?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde")?.ForcerOuvertureMenuPause();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
                return;
            // Laisser Tab (changer_main) descendre jusqu’au handler ; bloquer le reste du clavier (minage, etc.).
            if (@event.IsActionPressed("changer_main"))
            {
                // no-op ici : traité plus bas
            }
            else if (@event is InputEventKey keBloc && keBloc.Pressed && !keBloc.Echo)
                return;
        }

        // Menu CAO (stub) : bloquer le jeu ; Échap ferme — plus de touche K.
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
            if (!mainActive.EstVide) _forceLancer = 0f; // MAIN PLEINE = DÉBUT CHARGE LANCER/POSER
        }
        else if (@event.IsActionReleased("clic_droit"))
        {
            // PRIORITÉ ABSOLUE : si la visée touche un atelier posé, on ouvre le plan 3x3
            // avant toute logique de pose/lancer de l'objet en main.
            if (EssayerOuvrirAtelierSousVisee())
            {
                _forceLancer = 0f;
                return;
            }

            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!mainActive.EstVide)
            {
                // IDENTIFICATION DE LA MATIÈRE : Est-ce du terrain (Voxel) ?
                bool estTerrainVoxel = mainActive.ID >= 1 && mainActive.ID <= 9;
                bool estAtelierEnMain = mainActive.ID == 200;
                bool estBuissonEnMain = mainActive.ID == 10 || mainActive.ID == 11;
                // Clic bref = poser. Maintien du clic = lancer (seuil 0,5 s). Atelier (meuble) : jamais de lancer.
                if (estAtelierEnMain || estBuissonEnMain || estTerrainVoxel || _forceLancer < 0.5f)
                {
                    // Clic droit court + lame / roche plate / éclat + sol : fauchage (même ressenti qu’un coup) — le gauche le fait aussi.
                    if (!estAtelierEnMain && !estTerrainVoxel && _forceLancer < 0.5f && ExecuterFauchageSolPrioritaireClicDroit())
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
                // Main vide : clic droit court sur atelier posé => ouvrir.
                EssayerOuvrirAtelierSousVisee();
            }
        }
        else if (@event.IsActionPressed("interagir"))
        {
            // E : main pleine → corde accrochée / dépôt flexible ou rigide ; main vide → ramasser
            ExecuterToucheInteragir();
        }
        else if (@event.IsActionPressed("changer_main"))
        {
            MainGaucheEstActive = !MainGaucheEstActive;
            ReinitialiserRotationManuelle();
            RafraichirHUD();
            _menuAnatomie?.RafraichirMenu();
            GD.Print(MainGaucheEstActive ? "ZERO-K : Main Gauche sélectionnée (Tab)." : "ZERO-K : Main Droite sélectionnée (Tab).");
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
                    GD.Print($"ZERO-K : Rotation manuelle — Y (R) {_rotationManuelleY}°, X (Maj+R) {_rotationManuelleX}°, Z (Ctrl+R) {_rotationManuelleZ}°.");
                }
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_modelisateur != null && _modelisateur.EstOuvert)
            return;

        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (_gaucheMaintenu) _mouvementSourisCumule += mouseMotion.Relative;

            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _camera.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
            Vector3 cameraRot = _camera.Rotation;
            cameraRot.X = Mathf.Clamp(cameraRot.X, Mathf.DegToRad(-80f), Mathf.DegToRad(80f));
            _camera.Rotation = cameraRot;
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void MettreAJourSlotUI(Panel slot, SlotInventaire slotData, bool selectionne)
    {
        int idMatiere = slotData.ID;
        var style = new StyleBoxFlat();
        if (idMatiere == 0)
            style.BgColor = new Color(0.2f, 0.2f, 0.2f);
        else if (idMatiere == 1)
            style.BgColor = new Color(0.5f, 0.3f, 0.1f); // Marron (Terre)
        else if (idMatiere == 2)
            style.BgColor = new Color(0.4f, 0.4f, 0.4f); // Gris foncé (Roche)
        else if (idMatiere == 3)
            style.BgColor = new Color(0.9f, 0.8f, 0.5f); // Jaune pâle (Sable)
        else if (idMatiere == 4)
            style.BgColor = new Color(0.9f, 0.9f, 0.9f); // Blanc (Neige)
        else if (idMatiere == 5)
            style.BgColor = new Color(0.9f, 0.95f, 1f); // Blanc bleuté (Neige/Glace)
        else if (idMatiere == 6)
            style.BgColor = new Color(0.6f, 0.45f, 0.25f); // Terre aride (Arid earth)
        else if (idMatiere == 7)
            style.BgColor = new Color(0.35f, 0.25f, 0.15f); // Boue (Mud)
        else if (idMatiere == 8)
            style.BgColor = new Color(0.3f, 0.5f, 0.2f); // Terre tropicale
        else if (idMatiere == 9)
            style.BgColor = new Color(0.7f, 0.75f, 0.8f); // Terre gelée
        else if (ItemPhysique.EstIdRocheMatiere(idMatiere))
        {
            int idx = ItemPhysique.IndexChimiqueDepuisIdRoche(idMatiere);
            style.BgColor = ItemPhysique.TableGeologique[idx].CouleurBase;
        }
        else if (idMatiere == 999)
            style.BgColor = new Color(0.1f, 0.8f, 0.2f); // Vert (Objet/Buisson)
        else if (idMatiere == 30)
            style.BgColor = new Color(0.4f, 0.25f, 0.15f); // Marron (Bûche)
        else if (idMatiere == 32)
            style.BgColor = new Color(0.5f, 0.35f, 0.2f); // Marron clair (Bâton)
        else if (idMatiere == 34)
            style.BgColor = new Color(0.2f, 0.55f, 0.15f); // Vert feuillage
        else if (idMatiere == 100)
            style.BgColor = new Color(0.85f, 0.65f, 0.2f); // Or (outil forgé CAO)
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

    /// <summary>Masque le mesh « en main » devant la caméra quand le menu CAO est ouvert (évite la confusion avec le transit UI).</summary>
    public void DefinirVisibiliteObjetMainCamera(bool visible)
    {
        if (_objetEnMain != null)
            _objetEnMain.Visible = visible;
    }

    /// <summary>True si le menu inventaire (Q) est ouvert — utilisé par le gestionnaire pour Échap → pause sans fermer l’UI.</summary>
    public bool MenuAnatomieOuvert() => _menuAnatomie != null && _menuAnatomie.EstOuvert;

    public void RafraichirHUD()
    {
        AssurerDurabiliteOutilsSurLesMains();
        MettreAJourSlotUI(_slotGauche, MainGauche, MainGaucheEstActive);
        MettreAJourSlotUI(_slotDroite, MainDroite, !MainGaucheEstActive);
        MettreAJourLibellesNomsHud();
        MettreAJourObjetEnMain();
        MettreAJourPreviewsSlots();
        MettreAJourVisibilitePreviews();
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }

    /// <summary>Assigne le Mesh exact de la main active au MeshInstance3D devant la caméra.</summary>
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

    /// <summary>Usure dague / hachette main active après un usage réussi (fauchage, coupe, frappe roche, fente bois…).</summary>
    private void AppliquerUsureOutilMainActive(float cout)
    {
        if (cout <= 0f) return;
        bool casse = false;
        int idOutilCasse = 0;
        if (MainGaucheEstActive)
        {
            if (MainGauche.ID != 105 && MainGauche.ID != 106 && MainGauche.ID != IdObjetPellePierreTier0 && MainGauche.ID != IdObjetPiochePierreTier0) return;
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
            if (MainDroite.ID != 105 && MainDroite.ID != 106 && MainDroite.ID != IdObjetPellePierreTier0 && MainDroite.ID != IdObjetPiochePierreTier0) return;
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
                GD.Print("ZERO-K : La dague primitive se brise — lame ou manche a cédé. Il vous faudra une nouvelle lame et une corde.");
            else if (idOutilCasse == 106)
                GD.Print("ZERO-K : La hachette primitive se brise — lame ou manche a cédé. Il vous faudra refaire l’outil.");
            else if (idOutilCasse == IdObjetPellePierreTier0)
                GD.Print("ZERO-K : La pelle en pierre se brise — il faut reforger l’outil.");
            else
                GD.Print("ZERO-K : La pioche en pierre se brise — il faut reforger l’outil.");
        }
        RafraichirHUD();
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
    /// <summary>Invalider le cache si la topologie du mesh corde change (évite un mesh cassé gardé en statique).</summary>
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
            // Quads entre deux anneaux : b = voisin latéral sur l’anneau (wrap), pas v+s1 (cassait s=dernier → index hors plage).
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
        else if (id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches) return null; // GLB ceinture / ceinture+pochettes via instanciation dédiée
        else if (id == IdObjetPochetteTier0) return null; // GLB res://Modeles/materials/Pochette_Tiere0.glb via InstancierModelePochetteTier0
        else if (id == IdObjetPellePierreTier0) return null; // GLB res://Modeles/Equipements/Pelle_Pierre_tier0.glb via InstancierModeleArme
        else if (id == IdObjetPiochePierreTier0) return null; // GLB res://Modeles/Equipements/Pioche_pierre_tier0.glb via InstancierModeleArme
        else if (id == 30 || id == 32)
        {
            CalculerDimensionsBoisPose(id, indexMorpho, indexTaille, out float br, out float bl, out _, out _);
            return GenererMeshBoisFendu(br, bl, indexMorpho);
        }
        else if (id == 34) return new QuadMesh { Size = new Vector2(0.12f, 0.18f) }; // Feuilles (même style que feuillage arbre)
        else if (id == IdObjetBaie) return new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 10, Rings = 6 };
        if (id >= 1 && id <= 9)
            return new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) };
        return null;
    }

    public static void AppliquerMaterielObjet(MeshInstance3D visuel, int idObjet, int indexChimique, int indexMorphologique = 0, int niveauTressage = 0, byte indexBotanique = LSystem_Botanique.IndexChene)
    {
        // FIX CRITIQUE : Ne JAMAIS écraser le matériau d'un outil forgé (il possède ses propres surfaces cuites)
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
        if (idObjet == 20 || idObjet == 21 || idObjet == IdObjetCeinturePoches || idObjet == IdObjetCeintureSacoches || idObjet == IdObjetPochetteTier0 || idObjet == IdObjetSacTier0) { visuel.MaterialOverride = Atlas_Matiere.ObtenirMaterielCorde(indexChimique, indexMorphologique, niveauTressage); return; }
        if (idObjet == 30 || idObjet == 32)
        {
            visuel.MaterialOverride = idObjet == 32 && indexChimique == 1
                ? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
                : ArbreVivant.ObtenirMaterielBoisTriplanar(indexBotanique);
            return;
        }
        if (idObjet == 10 || idObjet == 11)
        {
            visuel.MaterialOverride = null; // Le mesh buisson porte déjà son matériau procédural.
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
                _ => new Color(0.90f, 0.14f, 0.14f)  // rouge par défaut
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

    /// <summary>Le raycast renvoie souvent la <see cref="CollisionShape3D"/> enfant, pas le corps — sinon la frappe retombe sans effet ni log.</summary>
    private static RigidBody3D ResoudreRigidBodyDepuisCollider(Node col)
    {
        if (col == null) return null;
        if (col is RigidBody3D rb0) return rb0;
        for (Node n = col; n != null; n = n.GetParent())
            if (n is RigidBody3D rb) return rb;
        return null;
    }

    /// <summary>Surface d’appui uniquement ROCHE (sol ID 2, cailloux matière 40–49). Le bois posé n’est pas une enclume.</summary>
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
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        // ArbreVivant : seules roches brutes et éclats — la dague (105) est trop fragile pour le bois.
        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = ItemPhysique.EstIdRocheMatiere(main.ID) || main.EstUnEclat || main.ID == 106;
            if (!outilTranchantPourArbre) return;

            float degatsArbre = 5.0f;
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
            JouerSonEtEffetCoupeArbre(pointImpact);
            return;
        }
        // Si on touche un objet physique valide, on annule le minage (y compris CollisionShape3D sous RigidBody).
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses"))) return;

        // Si objetTouche est null, cela signifie qu'on a touché le terrain bas-niveau ! ON CONTINUE LE MINAGE.
        Vector3 pointImpactVoxel = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeSondage = pointImpactVoxel - (normaleImpact * 0.5f);

        int idExtrait = _gestionnaireMonde?.ObtenirMatiereExacte(pointDeSondage) ?? 1;
        // Toujours terrain (1-9) pour que la pose refusionne avec le sol ; jamais 10/11/12/999 (bloc vert).
        if (idExtrait < 1 || idExtrait > 9) idExtrait = 1;

        if (MainGaucheEstActive && !MainGauche.EstVide && !MainDroite.EstVide) return;
        if (!MainGaucheEstActive && !MainDroite.EstVide && !MainGauche.EstVide) return;

        float forceDegats = 5.0f;
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        // THÉORÈME DE LA LAME : L'épaisseur dicte le tranchant
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            Aabb boite = mainActive.MeshEclat.GetAabb();
            float epaisseur = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));

            float multiplicateur = 0.2f / Mathf.Max(0.005f, epaisseur);
            forceDegats *= Mathf.Clamp(multiplicateur, 1.0f, 40.0f);

            GD.Print($"ZERO-K : Lame détectée. Épaisseur: {epaisseur:F3}m | Tranchant: x{multiplicateur:F1}");
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

    /// <summary>Spawn du lancer au niveau du corps (main / torse), puis léger recul si un mur bloque tout de suite devant.</summary>
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

    /// <summary>Vitesse cible (m/s) pour un lancer : indépendante de la masse (impulsion = m×v).</summary>
    private static float ObtenirVitesseCibleLancer(float forceCharge)
    {
        float f = Mathf.Clamp(forceCharge, 0.5f, 5.0f);
        return Mathf.Lerp(8f, 24f, Mathf.InverseLerp(0.5f, 5f, f));
    }

    /// <summary>Clic court « poser » : petit élan vers la visée pour ne pas tomber comme un plomb.</summary>
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

    /// <summary>Lance l’objet tenu : impulsion = masse × vitesse cible (même sensation petit caillou / gros morceau).</summary>
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

    /// <summary>Crée un bloc physique posé avec IndexCacheMemoire assigné (forme exacte conservée au rejet). Retourne le nœud créé (pour lancer avec impulsion). ItemPhysique est le RigidBody3D racine.</summary>
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
            // Bois taillé : cuire ScaleEclat dans les sommets (comme les éclats de roche). Évite scale non uniforme sur le RigidBody3D = visuel/collision faux en jeu.
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
            // FIX CRITIQUE : pas de matériau gris unique — l'ArrayMesh forgé porte ses textures par surface
            Material matVisuel = null;
            if (mainActive.ID != 100)
            {
                int chimPourRoche = ItemPhysique.EstIdRocheMatiere(mainActive.ID)
                    ? ItemPhysique.IndexChimiqueDepuisIdRoche(mainActive.ID)
                    : Mathf.Clamp(mainActive.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
                matVisuel = boisSculpte
                    ? (mainActive.ID == 32 && mainActive.IndexChimique == 1
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
            // FIX CRITIQUE : point zéro aux pieds du meuble (ancrerBaseAuSol = true), ~1,2 m sur la plus grande dimension.
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
        else if (id == 20) // Tressage / corde tier 0 : modèle GLB + mêmes matériaux procéduraux que l’inventaire.
        {
            int idA = mainActive.IndexChimique, idB = mainActive.IndexMorphologique;
            var item = new ItemPhysique { ID_Objet = id, IndexChimique = idA, IndexCacheMemoire = idB, NiveauFracture = mainActive.NiveauFracture, Name = "ItemPhysique" };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModeleCordeTier0Gazon(meshRoot, mainActive, 0.32f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.045f, Height = 0.28f } });
            corps = item;
        }
        else if (id == 21) // Tissu tier 0 : 4 cordes tissées — GLB + même matière plate que la corde.
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
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            if (!string.IsNullOrEmpty(mainActive.CleConteneur))
                item.SetMeta("CleConteneur", mainActive.CleConteneur);
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            if (id == IdObjetCeintureSacoches)
                InstancierModeleCeintureSacoches(meshRoot, mainActive, 0.42f);
            else
                InstancierModeleCeinturePoches(meshRoot, mainActive, 0.4f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = id == IdObjetCeintureSacoches ? new Vector3(0.52f, 0.12f, 0.32f) : new Vector3(0.42f, 0.09f, 0.28f) } });
            corps = item;
        }
        else if (id == IdObjetPochetteTier0) // Pochette tier 0 : tissu + corde, même matière procédurale que ceinture.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
            var meshRoot = new Node3D { Name = "MeshInstance3D" };
            InstancierModelePochetteTier0(meshRoot, mainActive, 0.36f);
            item.AddChild(meshRoot);
            item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.24f, 0.08f, 0.2f) } });
            corps = item;
        }
        else if (id == IdObjetSacTier0) // Sac tier 0 : modèle dédié + matière corde/tissu.
        {
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = mainActive.IndexChimique,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique"
            };
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
                // FIX CRITIQUE : Une Boîte statique est beaucoup plus stable qu'un ConvexShape pour Jolt.
                // Elle englobe le morceau coupé et empêche le passage à travers la terre.
                float wCol = br * 2f; float hCol = br;
                if (f == 2) { wCol = br; hCol = br; }
                else if (f >= 3) { wCol = br; hCol = br * 0.4f; }
                colObj = new BoxShape3D { Size = new Vector3(wCol, hCol, bl) };
            }
            var meshNode = new MeshInstance3D
            {
                Mesh = meshObj,
                MaterialOverride = id == 32 && mainActive.IndexChimique == 1
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
        else if (id == 34) // Feuilles arrachées (même mesh que le feuillage d'arbre)
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
        else // 999 Buisson — RigidBody3D pour pouvoir le lancer comme les autres objets posés.
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
        // Placement pur : pas de translation Y supplémentaire (évite double offset / lévitation atelier).
        corps.GlobalPosition = pointDeChute;
        // Même calque que le terrain PhysicsServer3D / StaticBody (bit 1) : collision fiable au sol.
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
            else if (id == 30 || id == 32 || id == 200)
            {
                rbPose.PhysicsMaterialOverride = _physMatBois;
                rbPose.LinearDampMode = RigidBody3D.DampMode.Replace;
                rbPose.AngularDampMode = RigidBody3D.DampMode.Replace;
                rbPose.LinearDamp = 0.06f;
                rbPose.AngularDamp = 0.42f;
                if (id == 200)
                {
                    // Très lourd + pas de gravité : évite tout glissement / dérive si le moteur réveille le corps un instant.
                    rbPose.Mass = 2800f;
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
                // Bois 30/32 et roches 40–49 sont déjà couverts plus haut ; ici : outil forgé (100) et autres éclats.
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
        // Fibres / corde non élastiques : ne pas appliquer d’échelle « étirée » (herbe, liane, corde boyau+herbe, etc.)
        bool estFlexOuCorde = id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(mainActive))
            corps.Scale = Vector3.One;
        else if (!ItemPhysique.EstIdRocheMatiere(id) && id != 30 && id != 32 && mainActive.ScaleEclat != Vector3.Zero)
            corps.Scale = mainActive.ScaleEclat;
        return corps;
    }

    private float _tempsAttenteSpawn;

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

        if (!spawnPret)
        {
            _tempsAttenteSpawn += dt;
            velocity = Vector3.Zero;
            Velocity = velocity;
            MoveAndSlide();
            return;
        }
        _tempsAttenteSpawn = 0f;

        int idMilieu = _gestionnaireMonde?.ObtenirMatiereExacte(GlobalPosition + Vector3.Up * 0.8f) ?? 1;
        bool estDansEau = (idMilieu == 4);

        if (estDansEau)
        {
            velocity.X *= 0.90f;
            velocity.Z *= 0.90f;
            velocity.Y *= 0.95f;
            if (!caoOuvert && Input.IsActionPressed("ui_accept"))
                velocity.Y += JumpVelocity * 0.8f * dt;
            else
                velocity.Y -= 1.5f * dt;
        }
        else if (!IsOnFloor())
        {
            velocity += GetGravity() * dt;
        }

        if (!caoOuvert && !estDansEau && Input.IsActionJustPressed("ui_accept") && IsOnFloor())
            velocity.Y = JumpVelocity;

        Vector2 inputDir = caoOuvert ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float vitesseMouvement = estDansEau ? Speed * 0.4f : Speed;

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

        Velocity = velocity;
        MoveAndSlide();
    }
}
