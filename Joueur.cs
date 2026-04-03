using Godot;
using System;
using System.Collections.Generic;

/// <summary>Slot d'inventaire avec ADN morphologique (forme) et chimique (composition).</summary>
public struct SlotInventaire
{
    public int ID;
    public int IndexMorphologique;
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
    /// <summary>Dague 105 : taille de la roche plate (0–4) utilisée au craft — échelle visuelle de la lame. Défaut 2 si absent.</summary>
    public int IndexTailleLameRoche;

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
    /// <summary>Sac à dos équipable : débloque la grille inventaire du menu anatomie (<see cref="AStockageSacOuCeintureEquipe"/>).</summary>
    public const int IdObjetSacDos = 101;
    /// <summary>Ceinture à poches équipable : débloque la même grille.</summary>
    public const int IdObjetCeinturePoches = 102;

    /// <summary>True si cet ID est un contenant porté qui ouvre la grille « sac » dans l’UI.</summary>
    public static bool EstObjetQuiDebloqueGrilleSac(int id) => id == IdObjetSacDos || id == IdObjetCeinturePoches;

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

    /// <summary>Stockage craft (9 cases) ; en inventaire (Q) seules 0–3 sont visibles / actives (2×2). Cases 4–8 réservées à l’établi (E sur table ID 200).</summary>
    public SlotInventaire[] GrilleCraft3x3 = new SlotInventaire[9];

    /// <summary>True si le menu a été ouvert depuis l’atelier posé : recettes et UI en 3×3. False après Q ou fermeture du menu.</summary>
    public bool CraftGrille3x3AuTable { get; set; }

    /// <summary>Slot contenant le résultat d'une recette valide.</summary>
    public SlotInventaire SlotResultatCraft = new SlotInventaire();

    /// <summary>Analyse la grille craft ; le détail des recettes est dans <see cref="Atlas_Matiere.EvaluerRecette"/>.</summary>
    public void VerifierRecettes()
    {
        SlotResultatCraft = Atlas_Matiere.EvaluerRecette(GrilleCraft3x3, CraftGrille3x3AuTable);
    }

    /// <summary>Vide la zone craft utilisée (4 cases en poche, 9 à l’établi) après prise du résultat.</summary>
    public void ConsommerIngredientsCraft()
    {
        if (GrilleCraft3x3 == null) return;
        int n = CraftGrille3x3AuTable ? GrilleCraft3x3.Length : 4;
        for (int i = 0; i < n && i < GrilleCraft3x3.Length; i++)
        {
            if (!GrilleCraft3x3[i].EstVide)
                GrilleCraft3x3[i] = new SlotInventaire();
        }
    }

    /// <summary>True si la grille « sac » du menu anatomie doit s’afficher (sac ou ceinture à poches équipé).</summary>
    public bool AStockageSacOuCeintureEquipe() =>
        (!EquipementSacDos.EstVide && EstObjetQuiDebloqueGrilleSac(EquipementSacDos.ID)) ||
        (!EquipementCeinture.EstVide && EstObjetQuiDebloqueGrilleSac(EquipementCeinture.ID));

    /// <summary>Équipe un sac ; passer un slot vide pour retirer (ou utiliser <see cref="RetirerEquipementSacDos"/>).</summary>
    public void AssignerEquipementSacDos(SlotInventaire slot)
    {
        EquipementSacDos = slot;
        NotifierChangementEquipementCorps();
    }

    public void RetirerEquipementSacDos()
    {
        EquipementSacDos = new SlotInventaire();
        NotifierChangementEquipementCorps();
    }

    public void AssignerEquipementCeinture(SlotInventaire slot)
    {
        EquipementCeinture = slot;
        NotifierChangementEquipementCorps();
    }

    public void RetirerEquipementCeinture()
    {
        EquipementCeinture = new SlotInventaire();
        NotifierChangementEquipementCorps();
    }

    private void NotifierChangementEquipementCorps()
    {
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }

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
    private const string MetaSignatureAtelier200 = "SigAtelier200";
    private const string MetaSignatureCorde20 = "SigCorde20";
    private const string MetaSignatureTissu21 = "SigTissu21";
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
                CraftGrille3x3AuTable = false;
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
            if (mainActive.EstVide) ExecuterMinageVoxel(); // MAIN VIDE = CREUSE DIRECTEMENT
            else
            {
                _gaucheMaintenu = true;
                _mouvementSourisCumule = Vector2.Zero;
            }
        }
        else if (@event.IsActionReleased("clic_gauche") && _gaucheMaintenu)
        {
            _gaucheMaintenu = false;
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
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
        }
        else if (@event.IsActionPressed("clic_droit"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!mainActive.EstVide) _forceLancer = 0f; // MAIN PLEINE = DÉBUT CHARGE LANCER/POSER
        }
        else if (@event.IsActionReleased("clic_droit"))
        {
            SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
            if (!mainActive.EstVide)
            {
                // Atelier : pose uniquement avec E (interagir) — le clic droit ne fait rien (évite double bind pose/lancer).
                if (mainActive.ID == 200)
                {
                    _forceLancer = 0f;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                // IDENTIFICATION DE LA MATIÈRE : Est-ce du terrain (Voxel) ?
                bool estTerrainVoxel = mainActive.ID >= 1 && mainActive.ID <= 9;
                // Clic bref = poser. Maintien du clic = lancer (seuil ~0,4 s pour éviter de lancer par accident).
                if (estTerrainVoxel || _forceLancer < 0.4f)
                {
                    // Clic droit court + lame / roche plate / éclat + sol : fauchage (même ressenti qu’un coup) — le gauche le fait aussi.
                    if (!estTerrainVoxel && _forceLancer < 0.4f && ExecuterFauchageSolPrioritaireClicDroit())
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
    private void MettreAJourObjetEnMain()
    {
        var main = MainGaucheEstActive ? MainGauche : MainDroite;
        if (main.EstVide || !EstObjetAvecVisuel(main.ID))
        {
            NettoyerModelesEnfants(_objetEnMain);
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            return;
        }
        if (main.ID == 105)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotDague105(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureDague105) ? (int)_objetEnMain.GetMeta(MetaSignatureDague105).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleArme(_objetEnMain, main, 0.35f, ObtenirFacteurEchelleLameDague(main));
                _objetEnMain.SetMeta(MetaSignatureDague105, sig);
            }
            // +20 % vs l’ancien 0,5, puis +25 % (0,6 → 0,75) : lisibilité dague en main.
            _objetEnMain.Scale = Vector3.One * (0.5f * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = new Vector3(-15f + _rotationManuelleX, 10f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 106)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotHachette106(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureHachette106) ? (int)_objetEnMain.GetMeta(MetaSignatureHachette106).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleArme(_objetEnMain, main, 0.42f, 1f);
                _objetEnMain.SetMeta(MetaSignatureHachette106, sig);
            }
            _objetEnMain.Scale = Vector3.One * (0.52f * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = new Vector3(-18f + _rotationManuelleX, 12f + _rotationManuelleY, 4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 20)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotCorde20(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureCorde20) ? (int)_objetEnMain.GetMeta(MetaSignatureCorde20).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleCordeTier0Gazon(_objetEnMain, main, 0.38f);
                _objetEnMain.SetMeta(MetaSignatureCorde20, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-15f + _rotationManuelleX, 10f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 21)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            int sig = SignatureSlotTissu21(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureTissu21) ? (int)_objetEnMain.GetMeta(MetaSignatureTissu21).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleTissuTier0(_objetEnMain, main, 0.36f);
                _objetEnMain.SetMeta(MetaSignatureTissu21, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-12f + _rotationManuelleX, 8f + _rotationManuelleY, 4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 200)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotAtelier200(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureAtelier200) ? (int)_objetEnMain.GetMeta(MetaSignatureAtelier200).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleAtelierPrimitif(_objetEnMain, main);
                _objetEnMain.SetMeta(MetaSignatureAtelier200, sig);
            }
            _objetEnMain.Scale = Vector3.One * 0.35f;
            _objetEnMain.RotationDegrees = new Vector3(0 + _rotationManuelleX, 90 + _rotationManuelleY, 0 + _rotationManuelleZ);
            return;
        }
        NettoyerModelesEnfants(_objetEnMain);
        if (_objetEnMain.HasMeta(MetaSignatureDague105))
            _objetEnMain.RemoveMeta(MetaSignatureDague105);
        if (_objetEnMain.HasMeta(MetaSignatureHachette106))
            _objetEnMain.RemoveMeta(MetaSignatureHachette106);
        if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
            _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
        if (_objetEnMain.HasMeta(MetaSignatureCorde20))
            _objetEnMain.RemoveMeta(MetaSignatureCorde20);
        if (_objetEnMain.HasMeta(MetaSignatureTissu21))
            _objetEnMain.RemoveMeta(MetaSignatureTissu21);
        int idxMorphMain = main.IndexMorphologique;
        Mesh m = main.EstUnEclat ? main.MeshEclat : ObtenirMeshDepuisCache(main.ID, idxMorphMain, main.IndexTaille);
        _objetEnMain.Mesh = m;
        if (main.ID == 30 || main.ID == 32)
        {
            _objetEnMain.Scale = Vector3.One * 0.38f;
            _objetEnMain.RotationDegrees = new Vector3(15f + _rotationManuelleX, 55f + _rotationManuelleY, -25f + _rotationManuelleZ);
        }
        else if (ItemPhysique.EstIdRocheMatiere(main.ID))
        {
            Vector3 sf = ItemPhysique.EchelleMorphologieRoche(main.IndexMorphologique);
            _objetEnMain.Scale = sf * 0.5f;
            _objetEnMain.RotationDegrees = new Vector3(-15 + _rotationManuelleX, 10 + _rotationManuelleY, 5 + _rotationManuelleZ);
        }
        else
        {
            _objetEnMain.Scale = Vector3.One * 0.5f;
            _objetEnMain.RotationDegrees = new Vector3(-15 + _rotationManuelleX, 10 + _rotationManuelleY, 5 + _rotationManuelleZ);
        }
        if (main.EstUnEclat)
        {
            if (ItemPhysique.EstIdRocheMatiere(main.ID))
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0);
            else if (main.ID == 30 || main.ID == 32)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, main.IndexMorphologique, 0);
            else if (main.ID >= 1 && main.ID <= 9)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0);
            else
                _objetEnMain.MaterialOverride = null;
        }
        else if (m != null)
        {
            int morphMat = main.ID is 20 or 21 ? main.IndexMorphologique
                : (main.ID == 30 || main.ID == 32) ? main.IndexMorphologique : 0;
            int tresMat = main.ID is 20 or 21 ? main.NiveauFracture : 0;
            AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, morphMat, tresMat);
        }
    }

    /// <summary>Assigne le Mesh exact au SubViewport de chaque slot (pierre en 3D dans l'UI).</summary>
    private void MettreAJourPreviewsSlots()
    {
        MettreAJourPreviewSlot(_meshPreviewGauche, MainGauche);
        MettreAJourPreviewSlot(_meshPreviewDroite, MainDroite);
    }

    private void MettreAJourPreviewSlot(MeshInstance3D meshNode, SlotInventaire slot)
    {
        if (slot.EstVide || !EstObjetAvecVisuel(slot.ID))
        {
            NettoyerModelesEnfants(meshNode);
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            return;
        }
        if (slot.ID == 105)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotDague105(slot);
            int prev = meshNode.HasMeta(MetaSignatureDague105) ? (int)meshNode.GetMeta(MetaSignatureDague105).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                // Moitié de l’échelle précédente (0,6 → 0,3) dans les slots HUD / menu anatomie.
                InstancierModeleArme(meshNode, slot, 0.3f, ObtenirFacteurEchelleLameDague(slot));
                meshNode.SetMeta(MetaSignatureDague105, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(20f, 45f, -20f);
            return;
        }
        if (slot.ID == 106)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotHachette106(slot);
            int prev = meshNode.HasMeta(MetaSignatureHachette106) ? (int)meshNode.GetMeta(MetaSignatureHachette106).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleArme(meshNode, slot, 0.34f, 1f);
                meshNode.SetMeta(MetaSignatureHachette106, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(22f, 40f, -18f);
            return;
        }
        if (slot.ID == 20)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotCorde20(slot);
            int prev = meshNode.HasMeta(MetaSignatureCorde20) ? (int)meshNode.GetMeta(MetaSignatureCorde20).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleCordeTier0Gazon(meshNode, slot, 0.32f);
                meshNode.SetMeta(MetaSignatureCorde20, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(12f, 35f, -8f);
            return;
        }
        if (slot.ID == 21)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            int sig = SignatureSlotTissu21(slot);
            int prev = meshNode.HasMeta(MetaSignatureTissu21) ? (int)meshNode.GetMeta(MetaSignatureTissu21).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleTissuTier0(meshNode, slot, 0.3f);
                meshNode.SetMeta(MetaSignatureTissu21, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(10f, 32f, -6f);
            return;
        }
        if (slot.ID == 200)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotAtelier200(slot);
            int prev = meshNode.HasMeta(MetaSignatureAtelier200) ? (int)meshNode.GetMeta(MetaSignatureAtelier200).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleAtelierPrimitif(meshNode, slot);
                meshNode.SetMeta(MetaSignatureAtelier200, sig);
            }
            meshNode.Scale = Vector3.One * 0.8f;
            meshNode.RotationDegrees = new Vector3(0f, 45f, 0f);
            return;
        }
        NettoyerModelesEnfants(meshNode);
        if (meshNode.HasMeta(MetaSignatureDague105))
            meshNode.RemoveMeta(MetaSignatureDague105);
        if (meshNode.HasMeta(MetaSignatureHachette106))
            meshNode.RemoveMeta(MetaSignatureHachette106);
        if (meshNode.HasMeta(MetaSignatureAtelier200))
            meshNode.RemoveMeta(MetaSignatureAtelier200);
        if (meshNode.HasMeta(MetaSignatureCorde20))
            meshNode.RemoveMeta(MetaSignatureCorde20);
        if (meshNode.HasMeta(MetaSignatureTissu21))
            meshNode.RemoveMeta(MetaSignatureTissu21);
        Mesh m = slot.EstUnEclat ? slot.MeshEclat : ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique, slot.IndexTaille);
        meshNode.Mesh = m;
        if (slot.ID == 30 || slot.ID == 32)
        {
            meshNode.Scale = Vector3.One * 0.72f;
            meshNode.RotationDegrees = new Vector3(68f, 18f, 0);
        }
        else if (ItemPhysique.EstIdRocheMatiere(slot.ID))
        {
            meshNode.Scale = ItemPhysique.EchelleMorphologieRoche(slot.IndexMorphologique) * 0.85f;
            meshNode.RotationDegrees = Vector3.Zero;
        }
        else
        {
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = Vector3.Zero;
        }
        if (slot.EstUnEclat)
        {
            if (ItemPhysique.EstIdRocheMatiere(slot.ID))
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
            else if (slot.ID == 30 || slot.ID == 32)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, slot.IndexMorphologique, 0);
            else if (slot.ID >= 1 && slot.ID <= 9)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
            else
                meshNode.MaterialOverride = null;
        }
        else if (m != null)
        {
            int morphMat = slot.ID is 20 or 21 ? slot.IndexMorphologique
                : (slot.ID == 30 || slot.ID == 32) ? slot.IndexMorphologique : 0;
            int tresMat = slot.ID is 20 or 21 ? slot.NiveauFracture : 0;
            AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, morphMat, tresMat);
        }
    }

    /// <summary>True si le slot doit afficher un mesh 3D dans l’UI (HUD ou menu anatomie).</summary>
    public bool InventaireSlotAunVisuel3D(SlotInventaire s) => !s.EstVide && EstObjetAvecVisuel(s.ID);

    /// <summary>Même rendu que les previews HUD, pour les panels G/D du menu anatomie.</summary>
    public void SynchroniserPreviewSlotMenu(MeshInstance3D meshNode, SlotInventaire slot) => MettreAJourPreviewSlot(meshNode, slot);

    /// <summary>Cache le SubViewport quand pas d'objet avec visuel (pierre, fibre, corde), pour laisser voir la couleur du slot.</summary>
    private void MettreAJourVisibilitePreviews()
    {
        if (_viewportSlotGauche != null) _viewportSlotGauche.Visible = !MainGauche.EstVide && EstObjetAvecVisuel(MainGauche.ID);
        if (_viewportSlotDroite != null) _viewportSlotDroite.Visible = !MainDroite.EstVide && EstObjetAvecVisuel(MainDroite.ID);
    }

    private static bool EstObjetProcedural(int id) => ItemPhysique.EstIdRocheMatiere(id);

    private static bool PeutUtiliserFrappe(SlotInventaire s)
    {
        if (s.EstVide) return false;
        if (EstObjetProcedural(s.ID)) return true;
        if (s.ID == 105 || s.ID == 106) return true;
        return s.ID == 100 && s.EstUnEclat && s.MeshEclat != null;
    }

    /// <summary>True si l'objet a un mesh à afficher en main / preview.</summary>
    private static bool EstObjetAvecVisuel(int id)
    {
        if (id >= 1 && id <= 9) return true;
        return ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == 30 || id == 32 || id == 34 || id == 100 || id == 105 || id == 106 || id == 200;
    }

    public static void NettoyerModelesEnfants(Node3D parent)
    {
        if (parent == null) return;
        Godot.Collections.Array<Node> enfants = parent.GetChildren();
        for (int i = enfants.Count - 1; i >= 0; i--)
        {
            Node n = enfants[i];
            if (n.Name.ToString().Contains("ModeleArme"))
                n.Free();
        }
        if (parent.HasMeta(MetaSignatureCorde20))
            parent.RemoveMeta(MetaSignatureCorde20);
        if (parent.HasMeta(MetaSignatureTissu21))
            parent.RemoveMeta(MetaSignatureTissu21);
    }

    private static Aabb TransformerAabb(Transform3D t, Aabb a)
    {
        Vector3 p = a.Position;
        Vector3 s = a.Size;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) != 0 ? p.X + s.X : p.X,
                (i & 2) != 0 ? p.Y + s.Y : p.Y,
                (i & 4) != 0 ? p.Z + s.Z : p.Z);
            Vector3 w = t * corner;
            min.X = Mathf.Min(min.X, w.X); min.Y = Mathf.Min(min.Y, w.Y); min.Z = Mathf.Min(min.Z, w.Z);
            max.X = Mathf.Max(max.X, w.X); max.Y = Mathf.Max(max.Y, w.Y); max.Z = Mathf.Max(max.Z, w.Z);
        }
        return new Aabb(min, max - min);
    }

    private static void AccumulerAabbMeshes(Node3D n, Transform3D parentVersRacine, ref Aabb? combine)
    {
        Transform3D racineVersNoeud = parentVersRacine * n.Transform;
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            Aabb b = TransformerAabb(racineVersNoeud, mi.Mesh.GetAabb());
            combine = combine.HasValue ? combine.Value.Merge(b) : b;
        }
        foreach (Node ch in n.GetChildren())
        {
            if (ch is Node3D c3)
                AccumulerAabbMeshes(c3, racineVersNoeud, ref combine);
            else
                AccumulerAabbSousNoeudsSansTransform3D(ch, racineVersNoeud, ref combine);
        }
    }

    /// <summary>Certains GLB insèrent des <see cref="Node"/> sans transform ; les meshes descendants doivent quand même être pris en compte pour l’AABB.</summary>
    private static void AccumulerAabbSousNoeudsSansTransform3D(Node n, Transform3D parentVersRacine, ref Aabb? combine)
    {
        foreach (Node ch in n.GetChildren())
        {
            if (ch is Node3D c3)
                AccumulerAabbMeshes(c3, parentVersRacine, ref combine);
            else
                AccumulerAabbSousNoeudsSansTransform3D(ch, parentVersRacine, ref combine);
        }
    }

    /// <summary>Réduit le GLB (souvent en unités Tripo) pour la caméra / le SubViewport, et centre le pivot sur la géométrie.</summary>
    public static void NormaliserEchelleEtCentrerModeleArme(Node3D modeleRacine, float tailleMaxDimension)
    {
        if (modeleRacine == null) return;
        Aabb? combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb box = combine.Value;
        float m = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
        if (m < 1e-8f) return;
        float s = tailleMaxDimension / m;
        Vector3 centre = box.GetCenter();
        modeleRacine.Scale = modeleRacine.Scale * s;
        modeleRacine.Position = -centre * s;
    }

    /// <summary>Comme les armes mais ancre le bas du mesh sur Y=0 (pivot sol) et centre en X/Z — évite la table qui flotte.</summary>
    public static void NormaliserEchelleTableAtelierAuSol(Node3D modeleRacine, float tailleMaxDimension)
    {
        if (modeleRacine == null) return;
        Aabb? combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb box = combine.Value;
        float m = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
        if (m < 1e-8f) return;
        float s = tailleMaxDimension / m;
        modeleRacine.Scale = modeleRacine.Scale * s;
        // Conserver la translation d’origine du GLB : sinon le min Y est calculé avec l’ancienne Position
        // mais on l’écrase, ce qui remonte le mesh (table qui flotte).
        Vector3 posAvant = modeleRacine.Position;
        combine = null;
        AccumulerAabbMeshes(modeleRacine, Transform3D.Identity, ref combine);
        if (!combine.HasValue) return;
        Aabb apres = combine.Value;
        Vector3 centre = apres.GetCenter();
        modeleRacine.Position = new Vector3(
            posAvant.X - centre.X,
            posAvant.Y - apres.Position.Y,
            posAvant.Z - centre.Z);
    }

    private static int SignatureSlotDague105(SlotInventaire s)
    {
        if (s.ID != 105) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexTailleLameRoche, s.NiveauFracture);
    }

    private static int SignatureSlotHachette106(SlotInventaire s)
    {
        if (s.ID != 106) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.IndexTaille, s.IndexBotanique, s.NiveauFracture);
    }

    private static int SignatureSlotAtelier200(SlotInventaire s)
    {
        if (s.ID != 200) return -1;
        return HashCode.Combine(s.IndexBotanique, s.IndexChimique, s.IndexMorphologique);
    }

    private static int SignatureSlotCorde20(SlotInventaire s)
    {
        if (s.ID != 20) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture);
    }

    private static int SignatureSlotTissu21(SlotInventaire s)
    {
        if (s.ID != 21) return -1;
        return HashCode.Combine(s.IndexChimique, s.IndexMorphologique, s.NiveauFracture);
    }

    /// <summary>Échelle du GLB dague : référence = roche moyenne (index 2) ; plus grosse roche plate → lame un peu plus massive.</summary>
    private static float ObtenirFacteurEchelleLameDague(SlotInventaire slot)
    {
        if (slot.ID != 105) return 1f;
        int t = slot.IndexTailleLameRoche <= 0 ? 2 : Mathf.Clamp(slot.IndexTailleLameRoche, 0, 4);
        return 1f + (t - 2) * 0.065f;
    }

    /// <summary>Cherche un <see cref="MeshInstance3D"/> dont le nom contient <paramref name="sousChaine"/> (suffixes d’import Godot).</summary>
    public static MeshInstance3D TrouverMeshInstanceDontLeNomContient(Node racine, string sousChaine)
    {
        if (racine == null || string.IsNullOrEmpty(sousChaine)) return null;
        var pile = new List<Node> { racine };
        for (int i = 0; i < pile.Count; i++)
        {
            foreach (Node c in pile[i].GetChildren())
            {
                if (c is MeshInstance3D mi && c.Name.ToString().Contains(sousChaine))
                    return mi;
                pile.Add(c);
            }
        }
        return null;
    }

    /// <summary>
    /// Recrée le mesh avec une normale par triangle (shading « facette »). Les GLB de corde/tissu arrivent souvent
    /// avec des normales lissées : N·L est quasi constant et le tressage disparaît visuellement malgré la géométrie.
    /// </summary>
    private static Mesh ForcerMeshNormalesParFacette(Mesh source)
    {
        if (source == null || source.GetSurfaceCount() == 0) return null;
        var output = new ArrayMesh();
        for (int surf = 0; surf < source.GetSurfaceCount(); surf++)
        {
            Godot.Collections.Array arrays = source.SurfaceGetArrays(surf);
            Variant vertVar = arrays[(int)Mesh.ArrayType.Vertex];
            if (vertVar.VariantType == Variant.Type.Nil) continue;
            Vector3[] verts = vertVar.AsVector3Array();
            if (verts == null || verts.Length < 3) continue;

            Vector2[] uvs = null;
            Variant uvVar = arrays[(int)Mesh.ArrayType.TexUV];
            if (uvVar.VariantType != Variant.Type.Nil)
                uvs = uvVar.AsVector2Array();

            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            bool ajoute = false;

            void PousserTri(Vector3 a, Vector3 b, Vector3 c, Vector2? uva, Vector2? uvb, Vector2? uvc)
            {
                Vector3 n = (b - a).Cross(c - a);
                if (n.LengthSquared() < 1e-16f) return;
                n = n.Normalized();
                if (uva.HasValue && uvb.HasValue && uvc.HasValue)
                {
                    st.SetNormal(n); st.SetUV(uva.Value); st.AddVertex(a);
                    st.SetNormal(n); st.SetUV(uvb.Value); st.AddVertex(b);
                    st.SetNormal(n); st.SetUV(uvc.Value); st.AddVertex(c);
                }
                else
                {
                    st.SetNormal(n); st.AddVertex(a);
                    st.SetNormal(n); st.AddVertex(b);
                    st.SetNormal(n); st.AddVertex(c);
                }
                ajoute = true;
            }

            Variant idxVar = arrays[(int)Mesh.ArrayType.Index];
            if (idxVar.VariantType != Variant.Type.Nil)
            {
                int[] idx = idxVar.AsInt32Array();
                if (idx != null && idx.Length >= 3)
                {
                    for (int i = 0; i + 2 < idx.Length; i += 3)
                    {
                        int ia = idx[i], ib = idx[i + 1], ic = idx[i + 2];
                        if ((uint)ia >= (uint)verts.Length || (uint)ib >= (uint)verts.Length || (uint)ic >= (uint)verts.Length)
                            continue;
                        Vector3 a = verts[ia], b = verts[ib], c = verts[ic];
                        if (uvs != null && ia < uvs.Length && ib < uvs.Length && ic < uvs.Length)
                            PousserTri(a, b, c, uvs[ia], uvs[ib], uvs[ic]);
                        else
                            PousserTri(a, b, c, null, null, null);
                    }
                }
            }
            else
            {
                for (int i = 0; i + 2 < verts.Length; i += 3)
                {
                    Vector3 a = verts[i], b = verts[i + 1], c = verts[i + 2];
                    if (uvs != null && i + 2 < uvs.Length)
                        PousserTri(a, b, c, uvs[i], uvs[i + 1], uvs[i + 2]);
                    else
                        PousserTri(a, b, c, null, null, null);
                }
            }

            if (ajoute)
                st.Commit(output);
        }
        return output.GetSurfaceCount() > 0 ? output : null;
    }

    private static void RemplacerMeshParNormalesFacettes(MeshInstance3D mi)
    {
        if (mi?.Mesh == null) return;
        Mesh plat = ForcerMeshNormalesParFacette(mi.Mesh);
        if (plat != null)
            mi.Mesh = plat;
    }

    /// <param name="tailleMaxMetres">Hors main : ~1,1 m pour une table lisible au sol.</param>
    /// <param name="ancrerBaseAuSol">True une fois posée : base du mesh sur Y=0 sous le RigidBody.</param>
    public static void InstancierModeleAtelierPrimitif(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.88f, bool ancrerBaseAuSol = false)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Ateliers/table_de_Craft_tiere_0.glb");
        if (scene == null) return;

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        var rng = new RandomNumberGenerator();
        rng.Seed = unchecked((ulong)(uint)HashCode.Combine(slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique, 200));

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nomLower = mi.Name.ToString().ToLowerInvariant();
                if (nomLower.Contains("cord"))
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, 20, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture);
                }
                else if (nomLower.Contains("roche"))
                {
                    int randChimique = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
                    int idRoche = ItemPhysique.IdRocheMatiereMin + randChimique;
                    AppliquerMaterielObjet(mi, idRoche, randChimique, 0, 0);
                }
                else
                    mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar();
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Corde tressée tier 0 (gazon) : GLB <c>traisagre_corde_tier0.glb</c> + matériaux <see cref="Atlas_Matiere.ObtenirMaterielCorde"/> (même logique cord/roche que l’atelier).</summary>
    public static void InstancierModeleCordeTier0Gazon(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.34f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/traisagre_corde_tier0.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        var rng = new RandomNumberGenerator();
        rng.Seed = unchecked((ulong)(uint)HashCode.Combine(slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, 20));

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nomLower = mi.Name.ToString().ToLowerInvariant();
                if (nomLower.Contains("cord"))
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, 20, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture);
                }
                else if (nomLower.Contains("roche"))
                {
                    int randChimique = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
                    int idRoche = ItemPhysique.IdRocheMatiereMin + randChimique;
                    AppliquerMaterielObjet(mi, idRoche, randChimique, 0, 0);
                }
                else
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    AppliquerMaterielObjet(mi, 20, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture);
                }
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Tissu tier 0 : GLB <c>tissu_tier0.glb</c> ; matériau identique à la corde (<see cref="Atlas_Matiere.ObtenirMaterielCorde"/>), sans triplanar bruit sur le relief.</summary>
    public static void InstancierModeleTissuTier0(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.36f)
    {
        PackedScene scene = GD.Load<PackedScene>("res://Modeles/materials/tissu_tier0.glb");
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";

        void ParcourirMeshes(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                RemplacerMeshParNormalesFacettes(mi);
                AppliquerMaterielObjet(mi, 21, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture);
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshes(c);
        }

        ParcourirMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    public static void InstancierModeleArme(Node3D parent, SlotInventaire slot, float tailleMaxUnites = 0.525f, float facteurEchelleLame = 1f)
    {
        NettoyerModelesEnfants(parent);
        if (slot.ID != 105 && slot.ID != 106) return;

        if (slot.ID == 106)
        {
            PackedScene sceneHachette = GD.Load<PackedScene>("res://Modeles/Equipements/hachette_premitive_tier0.glb");
            if (sceneHachette == null) return;

            float tailleNorm = tailleMaxUnites * Mathf.Clamp(facteurEchelleLame, 0.72f, 1.28f);
            Node3D modeleHachette = sceneHachette.Instantiate<Node3D>();
            modeleHachette.Name = "ModeleArme";

            MeshInstance3D partA = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_1")
                ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_1");
            MeshInstance3D partB = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
                ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_4");
            MeshInstance3D partC = modeleHachette.GetNodeOrNull<MeshInstance3D>("tripo_part_5")
                ?? TrouverMeshInstanceDontLeNomContient(modeleHachette, "tripo_part_5");

            // Ordre réel du GLB hachette : Part_4 = lame (roche), Part_5 = manche (bâton), Part_1 = lien (corde).
            MeshInstance3D miLame106 = partB;
            MeshInstance3D miManche106 = partC;
            MeshInstance3D miCorde106 = partA;

            int idRoche106 = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            if (miLame106 != null)
                AppliquerMaterielObjet(miLame106, idRoche106, slot.IndexChimique, 0, 0);
            if (miManche106 != null)
                AppliquerMaterielObjet(miManche106, 32, 0, 0, 0);
            if (miCorde106 != null)
                AppliquerMaterielObjet(miCorde106, 20, slot.IndexMorphologique, slot.IndexTaille, slot.NiveauFracture);

            NormaliserEchelleEtCentrerModeleArme(modeleHachette, tailleNorm);
            parent.AddChild(modeleHachette);
            return;
        }

        PackedScene scene = GD.Load<PackedScene>("res://Modeles/Equipements/Dague_Pure_Tier0.glb");
        if (scene == null) return;

        float tailleNormDague = tailleMaxUnites * Mathf.Clamp(facteurEchelleLame, 0.72f, 1.28f);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        // tripo_part_4 = lame, tripo_part_3 = manche (ordre mesh du .glb ; les matériaux étaient inversés si on croisait 3/4).
        MeshInstance3D meshLame = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4");
        MeshInstance3D meshManche = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_3")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_3");

        int idRocheDague = ItemPhysique.IdRocheMatiereMin + Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        if (meshLame != null)
            AppliquerMaterielObjet(meshLame, idRocheDague, slot.IndexChimique, 0, 0);
        if (meshManche != null)
            AppliquerMaterielObjet(meshManche, 20, slot.IndexMorphologique, slot.IndexTaille, slot.NiveauFracture);

        NormaliserEchelleEtCentrerModeleArme(modele, tailleNormDague);
        parent.AddChild(modele);
    }

    private static bool EstMatiereFlexible(int id)
    {
        int[] flexibles = { 15, 16, 17, 20, 21 }; // 20 corde, 21 tissu : flexibles
        return Array.IndexOf(flexibles, id) != -1;
    }

    private static bool EstObjetRigide(int id)
    {
        return ItemPhysique.EstIdRocheMatiere(id);
    }

    private void AssurerDurabiliteOutilsSurLesMains()
    {
        if (MainGauche.ID == 105 || MainGauche.ID == 106)
        {
            var m = MainGauche;
            Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref m);
            MainGauche = m;
        }
        if (MainDroite.ID == 105 || MainDroite.ID == 106)
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
            if (MainGauche.ID != 105 && MainGauche.ID != 106) return;
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
            if (MainDroite.ID != 105 && MainDroite.ID != 106) return;
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
            else
                GD.Print("ZERO-K : La hachette primitive se brise — lame ou manche a cédé. Il vous faudra refaire l’outil.");
        }
        RafraichirHUD();
    }

    private static void RemplirDurabiliteOutilDepuisItemPhysique(ref SlotInventaire slot, ItemPhysique item)
    {
        if ((slot.ID != 105 && slot.ID != 106) || item == null) return;
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

    /// <summary>ID morpho auto : dimensions réelles (m) → entier stable (comme index cache roche, pour crafts / réseau futur).</summary>
    /// <summary>Récupère ScaleEclat inventaire depuis le cylindre réel (bûche/bâton sans scale sur le RB).</summary>
    private static float ObtenirLongueurBoisWorld(int indexTaille) =>
        indexTaille switch { 0 => 1.2f, 1 => 1.0f, 2 => 0.5f, 3 => 0.25f, _ => 1.0f };

    /// <summary>Fente longitudinale (0–3) sur l’objet posé ; les anciennes valeurs hors plage sont ramenées à 0.</summary>
    private static int MorphologieBoisDepuisItem(ItemPhysique item)
    {
        if (item == null || (item.ID_Objet != 30 && item.ID_Objet != 32)) return 0;
        int m = item.IndexCacheMemoire;
        return m >= 0 && m <= 3 ? m : 0;
    }

    /// <summary>Longueur inventaire (ScaleEclat.Z) : meta prioritaire, sinon déduit du mesh local (évite tronc → standard si meta perdu).</summary>
    private static Vector3 ScaleEclatBoisAuRamassage(ItemPhysique item)
    {
        if (item == null || (item.ID_Objet != 30 && item.ID_Objet != 32))
            return Vector3.One;
        if (item.HasMeta("ScaleLongueurBois"))
            return new Vector3(1, 1, (float)item.GetMeta("ScaleLongueurBois").AsSingle());
        int t = Mathf.Clamp(item.IndexTailleRoche, 0, 4);
        float baseLen = ObtenirLongueurBoisWorld(t);
        Mesh m = item.ObtenirMeshVisuel();
        if (m != null)
        {
            float meshLen = m.GetAabb().Size.Y;
            if (meshLen > 0.02f)
                return new Vector3(1, 1, meshLen / Mathf.Max(0.001f, baseLen));
        }
        return Vector3.One;
    }

    private static void CalculerDimensionsBoisPose(int idObjet, int indexMorphologique, int indexTaille, out float baseRadius, out float baseLength, out float w, out float h)
    {
        int f = Mathf.Clamp(indexMorphologique, 0, 3);
        int t = Mathf.Clamp(indexTaille, 0, 3);
        baseRadius = idObjet == 30 ? 0.12f : 0.02f;
        baseLength = ObtenirLongueurBoisWorld(t);
        w = baseRadius * 2f;
        h = baseRadius * 2f;
        if (f == 1) h = baseRadius;
        else if (f == 2) { w = baseRadius; h = baseRadius; }
        else if (f >= 3) { w = baseRadius; h = baseRadius * 0.3f; }
    }

    public static Mesh GenererMeshBoisFendu(float rayon, float hauteur, int morpho)
    {
        if (morpho <= 0) return new CylinderMesh { TopRadius = rayon, BottomRadius = rayon, Height = hauteur };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float angleMax = Mathf.Pi / Mathf.Pow(2, morpho - 1);
        int segments = Mathf.Max(4, 16 / morpho);
        float demiH = hauteur * 0.5f;

        Vector3[] arcTop = new Vector3[segments + 1];
        Vector3[] arcBot = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * angleMax;
            float x = Mathf.Sin(a) * rayon;
            float z = Mathf.Cos(a) * rayon;
            arcTop[i] = new Vector3(x, demiH, z);
            arcBot[i] = new Vector3(x, -demiH, z);
        }
        Vector3 centerTop = new Vector3(0, demiH, 0);
        Vector3 centerBot = new Vector3(0, -demiH, 0);

        int idx = 0;
        void AddTri(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            st.SetNormal(n); st.SetUV(uv1); st.AddVertex(v1);
            st.SetNormal(n); st.SetUV(uv2); st.AddVertex(v2);
            st.SetNormal(n); st.SetUV(uv3); st.AddVertex(v3);
            st.AddIndex(idx); st.AddIndex(idx + 1); st.AddIndex(idx + 2);
            idx += 3;
        }

        // 1. Ecorce (Courbe Extérieure)
        for (int i = 0; i < segments; i++)
        {
            Vector3 t1 = arcTop[i], t2 = arcTop[i + 1], b1 = arcBot[i], b2 = arcBot[i + 1];
            Vector3 nMid = new Vector3((t1.X + t2.X) * 0.5f, 0, (t1.Z + t2.Z) * 0.5f).Normalized();
            float u1 = (float)i / segments, u2 = (float)(i + 1) / segments;
            AddTri(t1, t2, b1, nMid, new Vector2(u1, 0), new Vector2(u2, 0), new Vector2(u1, 1));
            AddTri(t2, b2, b1, nMid, new Vector2(u2, 0), new Vector2(u2, 1), new Vector2(u1, 1));
        }
        // 2. Capuchon Haut
        for (int i = 0; i < segments; i++)
        {
            AddTri(centerTop, arcTop[i + 1], arcTop[i], Vector3.Up, new Vector2(0.5f, 0.5f), new Vector2(arcTop[i + 1].X / rayon, arcTop[i + 1].Z / rayon), new Vector2(arcTop[i].X / rayon, arcTop[i].Z / rayon));
        }
        // 3. Capuchon Bas
        for (int i = 0; i < segments; i++)
        {
            AddTri(centerBot, arcBot[i], arcBot[i + 1], Vector3.Down, new Vector2(0.5f, 0.5f), new Vector2(arcBot[i].X / rayon, arcBot[i].Z / rayon), new Vector2(arcBot[i + 1].X / rayon, arcBot[i + 1].Z / rayon));
        }
        // 4. Aubier - Face A
        Vector3 nA = new Vector3(-1, 0, 0);
        AddTri(centerTop, centerBot, arcTop[0], nA, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0));
        AddTri(arcTop[0], centerBot, arcBot[0], nA, new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1));
        // 5. Aubier - Face B
        Vector3 dirB = new Vector3(arcTop[segments].X, 0, arcTop[segments].Z).Normalized();
        Vector3 nB = new Vector3(dirB.Z, 0, -dirB.X);
        AddTri(centerTop, arcTop[segments], centerBot, nB, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));
        AddTri(arcTop[segments], arcBot[segments], centerBot, nB, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        return st.Commit();
    }

    public static Mesh ObtenirMeshDepuisCache(int id, int indexMorpho, int indexTaille = 2)
    {
        if (ItemPhysique.EstIdRocheMatiere(id))
        {
            float r = ItemPhysique.RayonBaseRochesJoueur(indexTaille);
            return new SphereMesh { Radius = r, Height = r * 2f };
        }
        else if (id == 15 || id == 16) return new CapsuleMesh { Radius = 0.009f, Height = 0.34f };
        else if (id == 17) return new CapsuleMesh { Radius = 0.009f, Height = 0.38f };
        else if (id == 20) return null; // GLB res://Modeles/materials/traisagre_corde_tier0.glb via InstancierModeleCordeTier0Gazon
        else if (id == 21) return null; // GLB res://Modeles/materials/tissu_tier0.glb via InstancierModeleTissuTier0
        else if (id == 30 || id == 32)
        {
            CalculerDimensionsBoisPose(id, indexMorpho, indexTaille, out float br, out float bl, out _, out _);
            return GenererMeshBoisFendu(br, bl, indexMorpho);
        }
        else if (id == 34) return new QuadMesh { Size = new Vector2(0.12f, 0.18f) }; // Feuilles (même style que feuillage arbre)
        if (id >= 1 && id <= 9)
            return new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) };
        return null;
    }

    public static void AppliquerMaterielObjet(MeshInstance3D visuel, int idObjet, int indexChimique, int indexMorphologique = 0, int niveauTressage = 0)
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
        if (idObjet == 20 || idObjet == 21) { visuel.MaterialOverride = Atlas_Matiere.ObtenirMaterielCorde(indexChimique, indexMorphologique, niveauTressage); return; }
        if (idObjet == 30 || idObjet == 32)
        {
            visuel.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar();
            return;
        }
        if (idObjet == 34) { visuel.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.55f, 0.15f), Roughness = 0.95f, Metallic = 0f }; return; }
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

    /// <summary>E : si la main active tient un objet → accrocher (corde) ou poser (flexible / autres) ; sinon ramasser.</summary>
    private void ExecuterToucheInteragir()
    {
        _rayon.ForceRaycastUpdate();
        if (_rayon.IsColliding())
        {
            Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            var itemTouche = objetTouche as ItemPhysique
                ?? (objetTouche as Node)?.GetParent() as ItemPhysique
                ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");

            if (itemTouche != null && itemTouche.ID_Objet == 200)
            {
                if (Input.IsKeyPressed(Key.Shift))
                {
                    ExecuterRamassageObjet();
                    return;
                }

                // E (seul) = Ouvrir le plan de travail (Grille 3x3)
                else
                {
                    if (_menuAnatomie != null)
                    {
                        CraftGrille3x3AuTable = true;
                        if (!_menuAnatomie.EstOuvert)
                            _menuAnatomie.BasculerVisibilite();
                        else
                            _menuAnatomie.RafraichirMenu();

                        GetViewport().SetInputAsHandled();
                        GD.Print("ZERO-K : Plan de travail 3x3 de l'Atelier ouvert.");
                    }
                }
                return;
            }
        }

        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (!mainActive.EstVide)
        {
            if (mainActive.ID == 20)
            {
                if (ExecuterAttacheCordeSiPossible(mainActive))
                    return;
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            if (mainActive.ID == 21)
            {
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            if (EstMatiereFlexible(mainActive.ID))
            {
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            if (EstObjetPosableAuSol(mainActive))
            {
                ExecuterPlacementDepuisInteragir(mainActive);
                return;
            }
            GD.Print("ZERO-K : Cet objet ne se pose pas avec E (utilisez le clic droit pour le terrain / certains cas).");
            return;
        }
        ExecuterRamassageObjet();
    }

    /// <summary>True si la corde ou la fibre peut « s'étirer » visuellement (ScaleEclat) : les deux brins de la corde doivent être étirables.</summary>
    public static bool ObtenirSlotFlexibleEtirable(SlotInventaire s)
    {
        if (s.ID == 20 || s.ID == 21)
        {
            bool a = Atlas_Matiere.ObtenirProfilFlexible(s.IndexChimique, out var pa) && pa.Etirable;
            bool b = Atlas_Matiere.ObtenirProfilFlexible(s.IndexMorphologique, out var pb) && pb.Etirable;
            return a && b;
        }
        if (EstMatiereFlexible(s.ID))
            return Atlas_Matiere.ObtenirProfilFlexible(s.ID, out var p) && p.Etirable;
        return false;
    }

    /// <summary>Échelle pour l’établi CAO (hors 30/32, gérés à part) : fibres/corde non élastiques = taille naturelle, sans ScaleEclat « étiré ».</summary>
    public static Vector3 ObtenirEchellePieceFlexibleCAO(SlotInventaire slot)
    {
        bool estFlexOuCorde = slot.ID == 15 || slot.ID == 16 || slot.ID == 17 || slot.ID == 20 || slot.ID == 21;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(slot))
            return Vector3.One;
        if (slot.ScaleEclat != Vector3.Zero)
            return slot.ScaleEclat;
        return Vector3.One;
    }

    /// <summary>Fibres + corde : manipulation fine sur le plan de l’établi (rayon réduit).</summary>
    public static bool EstFlexibleOuCordePourPlanCAO(int idObjet) => idObjet is 15 or 16 or 17 or 20 or 21;

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (s.ID >= 1 && s.ID <= 9 && s.ID != 4) return true;
        return s.ID == 999 || ItemPhysique.EstIdRocheMatiere(s.ID) || s.ID == 30 || s.ID == 32 || s.ID == 34 || s.ID == 21 || s.ID == 200;
    }

    /// <summary>Corde (20) : accrocher au point de visée si surface valide (sol, roche, arbre, bloc posé).</summary>
    private bool ExecuterAttacheCordeSiPossible(SlotInventaire mainCorde)
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        Node col = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (col == null) return false;
        if (col == this || col.IsAncestorOf(this) || IsAncestorOf(col)) return false;

        bool ancre = col is StaticBody3D || col is RigidBody3D || ResoudreRigidBodyDepuisCollider(col) != null || col.IsInGroup("BlocsPoses") || ObtenirArbreDepuisCollider(col) != null;
        if (!ancre) return false;

        Vector3 pt = _rayon.GetCollisionPoint();
        Vector3 n = _rayon.GetCollisionNormal().Normalized();
        Vector3 tangent = Vector3.Up.Cross(n);
        if (tangent.LengthSquared() < 1e-4f) tangent = Vector3.Right.Cross(n);
        tangent = tangent.Normalized();

        Node3D corps = CreerBlocPose(pt + n * 0.07f, mainCorde);
        if (corps == null) return false;
        corps.SetMeta("Corde_Accrochee", true);
        corps.SetMeta("Corde_Normal", n);
        var b = Basis.LookingAt(tangent, n).Orthonormalized();
        corps.GlobalTransform = new Transform3D(b, corps.GlobalPosition);

        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;
        RafraichirHUD();
        GD.Print("ZERO-K : Corde accrochée à la surface (E).");
        return true;
    }

    /// <summary>Pose via E : portée courte pour fibres/corde, normale pour le reste. Respecte l’élasticité (pas d’étirement si non élastique).</summary>
    private void ExecuterPlacementDepuisInteragir(SlotInventaire mainActive)
    {
        ExecuterPlacementAvecOptions(mainActive, depuisInteragir: true);
    }

    private static string LireGenomeSurItemPhysique(ItemPhysique item)
    {
        if (item == null) return "";
        if (!string.IsNullOrEmpty(item.GenomeAssemblage)) return item.GenomeAssemblage;
        return item.HasMeta(MetaGenomeAssemblage) ? item.GetMeta(MetaGenomeAssemblage).AsString() : "";
    }

    /// <summary>Phase 2 pure : ramassage des objets physiques (Caillou, Silex, BlocsPoses). Touche E (interagir).
    /// Copie IndexCacheMemoire dans le SlotInventaire pour conserver la forme exacte.</summary>
    private void ExecuterRamassageObjet()
    {
        if (!MainGauche.EstVide && !MainDroite.EstVide) return;
        if (!_rayon.IsColliding()) return;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (objetTouche == null) return;

        SlotInventaire nouveauSlot = default;

        if (objetTouche.IsInGroup("BlocsPoses"))
        {
            int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
            var item = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            nouveauSlot = new SlotInventaire
            {
                ID = id,
                IndexMorphologique = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? MorphologieBoisDepuisItem(item)
                    : (item?.IndexCacheMemoire ?? 0),
                IndexChimique = item?.IndexChimique ?? 0,
                IndexTaille = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item != null && (item.ID_Objet == 105 || item.ID_Objet == 106 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) ? item.IndexTailleRoche : 2),
                IndexTailleLameRoche = item != null && item.ID_Objet == 105 && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item != null && item.ID_Objet == 105 ? 2 : 0),
                EstUnEclat = item?.EstUnEclat ?? false,
                MeshEclat = (item != null && item.EstUnEclat) ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item?.NiveauFracture ?? 0,
                // FIX CRITIQUE : bois 30/32 → meta ScaleLongueurBois ou repli sur la longueur mesh
                ScaleEclat = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? ScaleEclatBoisAuRamassage(item)
                    : (item != null ? item.Scale : Vector3.One),
                IndexBotanique = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == 106) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
            if ((nouveauSlot.ID == 105 || nouveauSlot.ID == 106) && item != null)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else if (objetTouche is RigidBody3D rb)
        {
            // BlocChutant (fibre, buisson tombé) : pas d'ItemPhysique, on lit le meta.
            if (objetTouche is BlocChutant)
            {
                int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
                nouveauSlot = new SlotInventaire { ID = id, IndexMorphologique = 0, IndexChimique = 0 };
            }
            else
            {
            var item = rb as ItemPhysique ?? (rb as Node)?.GetParent() as ItemPhysique ?? rb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (ItemPhysique.EstIdRocheMatiere(item.ID_Objet) && item.IndexTailleRoche >= 3)
            {
                GD.Print("ZERO-K : Masse excessive. La colonne vertébrale céderait. Action bloquée.");
                return;
            }
            nouveauSlot = new SlotInventaire
            {
                ID = item.ID_Objet,
                IndexMorphologique = item.ID_Objet == 30 || item.ID_Objet == 32 ? MorphologieBoisDepuisItem(item) : item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = item.ID_Objet == 30 || item.ID_Objet == 32
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2),
                IndexTailleLameRoche = item.ID_Objet == 105 && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item.ID_Objet == 105 ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == 106 || item.ID_Objet == 200) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
            }
        }
        else if (objetTouche is StaticBody3D sb)
        {
            var item = sb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (ItemPhysique.EstIdRocheMatiere(item.ID_Objet) && item.IndexTailleRoche >= 3)
            {
                GD.Print("ZERO-K : Masse excessive. La colonne vertébrale céderait. Action bloquée.");
                return;
            }
            nouveauSlot = new SlotInventaire
            {
                ID = item.ID_Objet,
                IndexMorphologique = item.ID_Objet == 30 || item.ID_Objet == 32 ? MorphologieBoisDepuisItem(item) : item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = item.ID_Objet == 30 || item.ID_Objet == 32
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2),
                IndexTailleLameRoche = item.ID_Objet == 105 && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item.ID_Objet == 105 ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == 106) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else
            return;

        if (MainGaucheEstActive)
        {
            if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else return;
        }
        else
        {
            if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else return;
        }
        objetTouche.QueueFree();
        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }

    /// <summary>Placement (construction ou rejet d'objet). Clic droit.</summary>
    private void ExecuterPlacement()
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide)
        {
            GD.Print("ZERO-K : La main sélectionnée est vide. Impossible de poser.");
            return;
        }
        ExecuterPlacementAvecOptions(mainActive, depuisInteragir: false);
    }

    private void ExecuterPlacementAvecOptions(SlotInventaire mainActive, bool depuisInteragir)
    {
        if (mainActive.EstVide) return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeChute;

        if (mainActive.ID == 200)
        {
            Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            if (!EstSolViseParRayon(_rayon, noeudCol))
            {
                GD.Print("ZERO-K : Posez l’atelier sur le sol (terrain / herbe), pas sur un objet vertical.");
                return;
            }

            if (_gestionnaireMonde != null && _gestionnaireMonde.UseArchitectureReseau)
                _gestionnaireMonde.AppliquerFauchageGlobal(pointImpact, RayonFauchagePoseAtelier200);

            // FIX CRITIQUE : On supprime la lecture du voxel hSurf + 1f.
            // L'objet se pose EXACTEMENT sur le point du raycast, ancré par son pivot.
            pointDeChute = pointImpact;
        }
        else
        {
            float decalNormale = 0.1f;
            pointDeChute = pointImpact + (normaleImpact * decalNormale);
        }
        float distance = GlobalPosition.DistanceTo(pointDeChute);
        // Flexible / corde avec E : on peut poser près du corps (manipulation fine) ; clic droit garde la marge anti-auto-collision
        bool flexOuCordeE = depuisInteragir && (EstMatiereFlexible(mainActive.ID) || mainActive.ID == 20 || mainActive.ID == 21);
        // Atelier : marge courte pour poser sous la visée (évite un rejet silencieux puis une pose « ailleurs »).
        float distMin = flexOuCordeE ? 0.35f : (mainActive.ID == 200 ? 0.55f : 1.4f);
        if (distance < distMin) return;

        int id = mainActive.ID;
        if (id == 0) return;
        if (id >= 1 && id <= 9 && id != 4)
        {
            _gestionnaireMonde?.AppliquerCreationGlobale(pointImpact, normaleImpact, RAYON_SCULPTURE, id);
        }
        else if (id == 999 || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == 30 || id == 32 || id == 34 || id == 105 || id == 106 || id == 200)
        {
            Node3D nePose = CreerBlocPose(pointDeChute, mainActive);
            if (id != 200)
                AppliquerImpulsionLacherDoux(nePose);
        }
        else
        {
            GD.Print($"ZERO-K : Matière {id} non géologique. Pose ignorée.");
            return;
        }

        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;

        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }

    /// <summary>Clic droit court : si la visée est le sol et l’outil peut faucher, exécute le même fauchage que le clic gauche (gazon 3D → fibres).</summary>
    /// <returns>True si le fauchage a été traité (ne pas enchaîner sur la pose au sol).</returns>
    private bool ExecuterFauchageSolPrioritaireClicDroit()
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive))
            return false;

        // Roche plate (morph 1) ou dague — pas la hachette pour le gazon.
        bool estOutilFaucheur = mainActive.ID == 105
            || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && mainActive.IndexMorphologique == 1);
        if (!estOutilFaucheur)
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSolViseParRayon(_rayon, objetTouche))
            return false;

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);
        if (effPelle >= 0.6f)
            return false;

        ExecuterCreusage(1f, effPelle, masseOutil, _rayon.GetCollisionPoint());
        JouerAnimationFrappe(TypeMouvementFrappe.Estoc);
        return true;
    }

    /// <summary>Terrain voxel / sections de sol : creusage (pelle) ou fauchage (lame) selon l’outil émergent.</summary>
    /// <remarks>Le raycast touche souvent le <see cref="CollisionShape3D"/> enfant ; le <see cref="StaticBody3D"/> s’appelle <c>CollisionSection_*</c>.</remarks>
    private static bool EstSurfaceTerrainVisee(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur.IsInGroup("Terrain")) return true;
            string nm = cur.Name.ToString();
            if (nm.Contains("Terrain") || nm.Contains("CollisionSection")) return true;
        }
        return false;
    }

    /// <summary>
    /// Sol du monde procédural (Monde_Client) : corps créés uniquement via <see cref="PhysicsServer3D"/> sans nœud <see cref="CollisionObject3D"/>.
    /// Dans ce cas <see cref="RayCast3D.GetCollider"/> est souvent <c>null</c> alors que <see cref="RayCast3D.IsColliding"/> est vrai.
    /// </summary>
    private static bool EstSolMondeSansColliderNode(RayCast3D rayon)
    {
        if (!rayon.IsColliding()) return false;
        if (rayon.GetCollider() != null) return false;
        return rayon.GetCollisionNormal().Y >= 0.18f;
    }

    /// <summary>True si la visée est le sol (nœuds terrain legacy OU mesh monde AAA sans objet associé au raycast).</summary>
    private static bool EstSolViseParRayon(RayCast3D rayon, Node noeudDepuisCollider)
    {
        return EstSurfaceTerrainVisee(noeudDepuisCollider) || EstSolMondeSansColliderNode(rayon);
    }

    /// <summary>Collider Jolt = souvent <see cref="CollisionShape3D"/> ; on remonte au corps pour groupes / noms.</summary>
    private static Node NoeudDepuisColliderRaycast(GodotObject collider)
    {
        if (collider == null) return null;
        if (collider is CollisionShape3D sh)
            return sh.GetParent() as Node ?? sh;
        return collider as Node;
    }

    /// <summary>Hache = tranchant perpendiculaire à la frappe (<c>alignement</c> → 0). Pelle = plat aligné (<c>alignement</c> → 1).</summary>
    private (float efficaciteHache, float efficacitePelle, float masse) AnalyserOutilCAO(Vector3 directionFrappe)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        directionFrappe = directionFrappe.Normalized();

        if (mainActive.ID == 100 && mainActive.MeshEclat != null)
        {
            int clef = ClefRegistreOutilForge(mainActive);
            if (RegistreOutilsForges.TryGetValue(clef, out var stats))
            {
                Vector3 normaleFacePlate = (_objetEnMain.GlobalTransform.Basis * stats.AxeTranchantLocal).Normalized();
                float frappeSurLePlat = Mathf.Abs(directionFrappe.Dot(normaleFacePlate));

                float erreurHache = frappeSurLePlat;
                if (erreurHache < 0.65f) erreurHache = 0f;
                else erreurHache = (erreurHache - 0.65f) * 2.85f;
                float effHache = 1.0f - Mathf.Clamp(erreurHache, 0f, 1f);

                float erreurPelle = 1.0f - frappeSurLePlat;
                if (erreurPelle < 0.65f) erreurPelle = 0f;
                else erreurPelle = (erreurPelle - 0.65f) * 2.85f;
                float effPelle = 1.0f - Mathf.Clamp(erreurPelle, 0f, 1f);

                return (effHache, effPelle, stats.Masse);
            }
        }

        if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            return (0.88f, 0.12f, 3.0f);
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
            return (0.82f, 0.18f, 2.0f);
        if (mainActive.ID == 105)
            return (0.78f, 0.22f, 1.15f);
        if (mainActive.ID == 106)
            return (0.88f, 0.12f, 2.05f);
        if (ItemPhysique.EstIdRocheMatiere(mainActive.ID))
        {
            float m = mainActive.IndexTaille switch { 0 => 1f, 1 => 2f, 2 => 8f, 3 => 14f, 4 => 20f, _ => 8f };
            return (0.65f, 0.35f, m);
        }

        return (0.1f, 0.1f, 1.0f);
    }

    /// <summary>Épaisseur effective pour <see cref="ArbreVivant.SubirDegats"/> (tronc / lames) — indépendante du multiplicateur d’impact émergent.</summary>
    private float CalculerEpaisseurLamePourImpact(SlotInventaire mainActive, Vector3 directionFrappe)
    {
        float epaisseurLame = 0.2f;
        if (mainActive.ID == 100 && mainActive.MeshEclat != null)
        {
            int clef = ClefRegistreOutilForge(mainActive);
            if (RegistreOutilsForges.TryGetValue(clef, out var stats))
            {
                epaisseurLame = stats.EpaisseurLameBase;
                Vector3 normaleFacePlate = (_objetEnMain.GlobalTransform.Basis * stats.AxeTranchantLocal).Normalized();
                float frappeSurLePlat = Mathf.Abs(directionFrappe.Normalized().Dot(normaleFacePlate));

                float erreurHache = frappeSurLePlat;
                if (erreurHache < 0.65f) erreurHache = 0f;
                else erreurHache = (erreurHache - 0.65f) * 2.85f;

                epaisseurLame = stats.EpaisseurLameBase * (1.0f + erreurHache * 15.0f);
            }
        }
        else if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            Aabb boite = mainActive.MeshEclat.GetAabb();
            epaisseurLame = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));
        }
        else if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            epaisseurLame = 0.05f;
        else if (mainActive.ID == 105)
            epaisseurLame = 0.04f;
        else if (mainActive.ID == 106)
            epaisseurLame = 0.065f;

        return epaisseurLame;
    }

    /// <summary>True si la pointe (manche→lame) est alignée sur la visée caméra→cible — les rotations R / Maj+R / Ctrl+R sur l’objet en main sont prises en compte via <see cref="GlobalTransform"/>.</summary>
    private bool EstFrappeDagueAvecLaLame(Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (_objetEnMain == null || _camera == null) return false;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D lameMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_3")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_3");
        if (lameMi?.Mesh == null || mancheMi?.Mesh == null) return false;
        Vector3 cL = lameMi.GlobalTransform * lameMi.Mesh.GetAabb().GetCenter();
        Vector3 cM = mancheMi.GlobalTransform * mancheMi.Mesh.GetAabb().GetCenter();
        Vector3 lameDepuisManche = cL - cM;
        if (lameDepuisManche.LengthSquared() < 1e-10f) return false;
        lameDepuisManche = lameDepuisManche.Normalized();
        Vector3 versCible = pointImpact - _camera.GlobalPosition;
        if (versCible.LengthSquared() < 1e-10f) return false;
        versCible = versCible.Normalized();

        float alignVisée = versCible.Dot(lameDepuisManche);
        float alignMouvement = 0f;

        if (directionFrappe.LengthSquared() > 1e-8f)
        {
            Vector3 dirNorm = directionFrappe.Normalized();
            alignMouvement = dirNorm.Dot(lameDepuisManche);

            if (Mathf.Abs(dirNorm.Y) > 0.5f)
                alignMouvement += 0.4f;
        }

        const float seuil = 0.15f;
        return Mathf.Max(alignVisée, alignMouvement) > seuil;
    }

    /// <summary>Hachette 106 : lame <c>tripo_part_4</c>, manche <c>tripo_part_5</c> (aligné avec <see cref="InstancierModeleArme"/> id 106).</summary>
    private bool EstFrappeHachette106AvecLaLame(Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (_objetEnMain == null || _camera == null) return false;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D lameMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_5")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_5");
        if (lameMi?.Mesh == null || mancheMi?.Mesh == null) return false;
        Vector3 cL = lameMi.GlobalTransform * lameMi.Mesh.GetAabb().GetCenter();
        Vector3 cM = mancheMi.GlobalTransform * mancheMi.Mesh.GetAabb().GetCenter();
        Vector3 lameDepuisManche = cL - cM;
        if (lameDepuisManche.LengthSquared() < 1e-10f) return false;
        lameDepuisManche = lameDepuisManche.Normalized();
        Vector3 versCible = pointImpact - _camera.GlobalPosition;
        if (versCible.LengthSquared() < 1e-10f) return false;
        versCible = versCible.Normalized();

        float alignVisée = versCible.Dot(lameDepuisManche);
        float alignMouvement = 0f;

        if (directionFrappe.LengthSquared() > 1e-8f)
        {
            Vector3 dirNorm = directionFrappe.Normalized();
            alignMouvement = dirNorm.Dot(lameDepuisManche);

            if (Mathf.Abs(dirNorm.Y) > 0.5f)
                alignMouvement += 0.4f;
        }

        const float seuil = 0.15f;
        return Mathf.Max(alignVisée, alignMouvement) > seuil;
    }

    /// <summary>Relâchement clic gauche : sol → creusage / fauchage ; sinon frappe roches, arbres, rigides.</summary>
    private void ExecuterAction(float force, TypeMouvementFrappe mouvement)
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive)) return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
        {
            GD.Print("ZERO-K : Aucune collision sous la visée — rapprochez-vous du sol ou vérifiez le chargement des chunks.");
            return;
        }

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        Vector3 pointImpact = _rayon.GetCollisionPoint();

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (mouvement == TypeMouvementFrappe.DeHautEnBas) directionMouvement = -_camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.DeBasEnHaut) directionMouvement = _camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.GaucheADroite) directionMouvement = _camera.GlobalTransform.Basis.X.Normalized();
        else if (mouvement == TypeMouvementFrappe.DroiteAGauche) directionMouvement = -_camera.GlobalTransform.Basis.X.Normalized();

        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);

        if (EstSolViseParRayon(_rayon, objetTouche))
        {
            ExecuterCreusage(force, effPelle, masseOutil, pointImpact);
            return;
        }

        if (objetTouche == null)
        {
            GD.Print("ZERO-K : Objet touché non reconnu (ni sol ni rigide avec nœud).");
            return;
        }

        ExecuterFrappePhysique(force, effHache, masseOutil, objetTouche, pointImpact, directionMouvement);
    }

    private void JouerAnimationFrappe(TypeMouvementFrappe type)
    {
        if (_objetEnMain == null) return;
        bool visuelEnMain = _objetEnMain.Mesh != null || _objetEnMain.FindChild("ModeleArme", true, false) != null;
        if (!visuelEnMain) return;
        _tweenFrappe?.Kill();
        _tweenFrappe = CreateTween();

        MettreAJourObjetEnMain();

        Vector3 posCible = _objetEnMain.Position;
        Vector3 rotCible = _objetEnMain.RotationDegrees;

        if (type == TypeMouvementFrappe.Estoc) { posCible.Z -= 0.5f; rotCible.X -= 20f; }
        else if (type == TypeMouvementFrappe.DeHautEnBas) { posCible.Y -= 0.4f; rotCible.X -= 70f; }
        else if (type == TypeMouvementFrappe.DeBasEnHaut) { posCible.Y += 0.4f; rotCible.X += 70f; }
        else if (type == TypeMouvementFrappe.GaucheADroite) { posCible.X += 0.4f; rotCible.Y -= 70f; rotCible.Z -= 45f; }
        else if (type == TypeMouvementFrappe.DroiteAGauche) { posCible.X -= 0.4f; rotCible.Y += 70f; rotCible.Z += 45f; }

        _tweenFrappe.TweenProperty(_objetEnMain, "position", posCible, 0.08f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tweenFrappe.Parallel().TweenProperty(_objetEnMain, "rotation_degrees", rotCible, 0.08f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tweenFrappe.TweenCallback(Callable.From(ReposerObjetEnMainApresFrappe)).SetDelay(0.15f);
    }

    private void ReposerObjetEnMainApresFrappe()
    {
        _objetEnMain.Position = new Vector3(0.3f, -0.25f, -0.8f);
        MettreAJourObjetEnMain();
    }

    private void ExecuterCreusage(float force, float efficacitePelle, float masseOutil, Vector3 pointImpact)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        if (efficacitePelle < 0.6f)
        {
            // Fauchage : dague (105), roche plate (morpho 1), ou éclat — pas la hachette (106), inadaptée au gazon fin.
            bool estOutilFaucheur = mainActive.ID == 105
                || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && mainActive.IndexMorphologique == 1)
                || mainActive.EstUnEclat;

            if (estOutilFaucheur)
            {
                _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 3.1f);
                if (mainActive.ID == 105)
                    AppliquerUsureOutilMainActive(0.75f);
                GD.Print("ZERO-K : Fauchage de la flore. Récolte de fibres en cours.");
                return;
            }
            GD.Print("ZERO-K : L'angle de cette lame ne permet pas de déplacer la terre. Il vous faut une surface plate (Pelle/Houe).");
            return;
        }

        float forceCreusage = masseOutil * force * efficacitePelle;

        if (forceCreusage > 10f)
        {
            GD.Print($"ZERO-K : Extraction du sol réussie. (Force Volume: {forceCreusage:F1})");
            if (mainActive.ID == 105 || mainActive.ID == 106)
                AppliquerUsureOutilMainActive(3.2f);
        }
        else if (mainActive.ID == 105 && efficacitePelle >= 0.6f)
        {
            // Dague mal orientée en « pelle » : le creusage formel est trop faible, mais on gratte quand même un peu + fauchage herbe.
            _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpact, 0.95f, 4.5f);
            _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 2.8f);
            AppliquerUsureOutilMainActive(2.4f);
            GD.Print("ZERO-K : La dague racle la surface (coup orienté pelle, peu de pénétration).");
        }
        else
        {
            GD.Print("ZERO-K : Manque de force ou outil trop léger pour percer ce sol.");
        }
    }

    /// <summary>Arbres vivants/morts, roches, rigides — efficacité hache émergente.</summary>
    private void ExecuterFrappePhysique(float force, float efficaciteHache, float masseOutil, Node objetTouche, Vector3 pointImpact, Vector3 directionFrappe)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        if (efficaciteHache < 0.4f && masseOutil > 2f)
        {
            GD.Print("ZERO-K : REBOND MASSIF ! Vous frappez avec le plat de l'outil. Choc structurel violent !");
            return;
        }

        float multiplicateurLame = Mathf.Clamp(efficaciteHache * 20.0f, 1.0f, 40.0f);
        if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.5f);
        else if (mainActive.ID == 105 && EstFrappeDagueAvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.35f);
        else if (mainActive.ID == 106 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.85f);
        else if (mainActive.EstUnEclat && mainActive.MeshEclat != null && mainActive.ID != 100)
            multiplicateurLame = Mathf.Min(multiplicateurLame, 40.0f);

        float forceImpact = (masseOutil * force * 15f) * multiplicateurLame;
        float epaisseurLame = CalculerEpaisseurLamePourImpact(mainActive, directionFrappe);

        if (objetTouche == null)
            return;

        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            bool outilTranchantPourArbre = mainActive.ID == 106
                || mainActive.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && mainActive.IndexMorphologique == 1); // roche plate
            if (!outilTranchantPourArbre) return;

            float forceCoupe = forceImpact;
            if (mainActive.EstUnEclat && arbre.AgeEnJours <= 2)
                forceCoupe = Mathf.Max(forceCoupe, arbre.AgeEnJours <= 1 ? 36f : 48f);
            if (mainActive.ID == 106)
                forceCoupe *= 1.14f;

            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, forceCoupe, epaisseurLame, mainActive.ID == 106);
            if (resultatCoupe == 0) GD.Print("ZERO-K : Rebond. La force d'impact est insuffisante pour entamer ce bois.");
            else if (resultatCoupe == 1) JouerSonEtEffetCoupeArbre(pointImpact);
            else if (resultatCoupe == 2) { JouerSonEtEffetCoupeArbre(pointImpact); GD.Print("ZERO-K : Arbre abattu."); }
            else if (resultatCoupe == 3) { JouerSonEtEffetCoupeArbre(pointImpact); GD.Print("ZERO-K : Branche amputée."); }
            return;
        }

        RigidBody3D rbCible = ResoudreRigidBodyDepuisCollider(objetTouche);
        if (rbCible == null) return;

        if (rbCible.Name.ToString().Contains("ArbreMort"))
        {
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = main.ID == 106
                || main.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(main.ID) && main.IndexMorphologique == 1); // roche plate
            if (!outilTranchantPourArbre)
                return;

            // Étape 1 : arrachage du feuillage (une action par frappe)
            Node feuillage = rbCible.GetNodeOrNull("Feuillage");
            if (feuillage != null)
            {
                Material matFeuilles = (feuillage as MeshInstance3D)?.MaterialOverride?.Duplicate() as Material;
                feuillage.QueueFree();
                JouerSonEtEffetCoupeArbre(pointImpact);
                GD.Print("ZERO-K : Feuillage arraché du cadavre végétal.");
                int quantite = 3 + (int)(rbCible.Mass / 100f);
                for (int i = 0; i < quantite; i++)
                {
                    var bloc = BlocChutant.CreerFeuillageArrache(pointImpact, matFeuilles);
                    GetTree().CurrentScene.AddChild(bloc);
                    bloc.GlobalPosition = pointImpact + new Vector3((float)GD.Randf() - 0.5f, 0.5f, (float)GD.Randf() - 0.5f);
                }
                return;
            }

            int age = rbCible.HasMeta("Age") ? (int)rbCible.GetMeta("Age").AsInt32() : 1;
            int branchesRestantes = rbCible.HasMeta("BranchesRestantes") ? (int)rbCible.GetMeta("BranchesRestantes").AsInt32() : 0;
            byte essenceBois = rbCible.HasMeta("IndexBotanique")
                ? (byte)Mathf.Clamp(rbCible.GetMeta("IndexBotanique").AsInt32(), 0, 255)
                : LSystem_Botanique.IndexChene;

            // Étape 2 : ébranchage (bâtons 32) avant débitage du tronc
            if (branchesRestantes > 0)
            {
                JouerSonEtEffetCoupeArbre(pointImpact);
                branchesRestantes--;
                rbCible.SetMeta("BranchesRestantes", branchesRestantes);
                GD.Print($"ZERO-K : Branche amputée. Reste : {branchesRestantes}");
                var slotBaton = new SlotInventaire
                {
                    ID = 32,
                    IndexBotanique = essenceBois,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One
                };
                // Surélève le spawn du bâton pour éviter le clip sous le sol
                Node3D baton = CreerBlocPose(pointImpact + directionFrappe * 0.2f + Vector3.Up * 0.8f, slotBaton);
                if (baton is RigidBody3D rbBaton)
                    rbBaton.ApplyCentralImpulse(directionFrappe * 3f);
                return;
            }

            // ÉTAPE 3 : LIBÉRATION DU TRONC BRUT UNIQUE
            bool peutLibererTronc = main.ID == 106
                || main.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(main.ID) && main.IndexMorphologique == 1);
            if (!peutLibererTronc)
            {
                GD.Print("ZERO-K : Il faut un tranchant : roche plate, éclat ou hachette.");
                return;
            }

            float hauteurTronc = rbCible.HasMeta("HauteurTronc") ? (float)rbCible.GetMeta("HauteurTronc").AsSingle() : 4.0f;
            float scaleZ = hauteurTronc / 1.2f; // Base du Tronc Brut = 1.2m

            JouerSonEtEffetCoupeArbre(pointImpact);
            GD.Print($"ZERO-K : Le cadavre est purgé. Vous obtenez un Tronc Brut massif ({hauteurTronc:F1}m).");

            var slotTroncLong = new SlotInventaire
            {
                ID = 30,
                IndexBotanique = essenceBois,
                IndexMorphologique = 0,
                IndexTaille = 0,
                ScaleEclat = new Vector3(1, 1, scaleZ)
            };

            Node3D leTronc = CreerBlocPose(rbCible.GlobalPosition + Vector3.Up * 0.8f, slotTroncLong);
            if (leTronc != null)
                leTronc.GlobalRotation = rbCible.GlobalRotation;

            rbCible.QueueFree();
            return;
        }

        if (rbCible.Name.ToString().Contains("BrancheMorte"))
        {
            var mainB = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = mainB.ID == 106
                || mainB.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(mainB.ID) && mainB.IndexMorphologique == 1); // roche plate
            if (!outilTranchantPourArbre) return;
            rbCible.ApplyCentralImpulse(directionFrappe * (10f * force));
            JouerSonEtEffetCoupeArbre(pointImpact);
            GD.Print("ZERO-K : Coup sur la branche tombée.");
            return;
        }

        var item = rbCible as ItemPhysique ?? rbCible.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (item == null)
        {
            rbCible.ApplyCentralImpulse(directionFrappe * (4f * force));
            GD.Print($"ZERO-K : Frappe sur « {rbCible.Name} » (corps rigide non outillé) — impulsion seule.");
            return;
        }

        Vector3 dirFrappeObj = -_rayon.GetCollisionNormal();
        float impulsionFrappe = 4f * force * (1f + rbCible.Mass * 0.5f);

        if (item.ID_Objet == 30 || item.ID_Objet == 32)
        {
            // Post-abattage (bois au sol) : standardisation/fente réservée à la hachette.
            if (mainActive.ID != 106)
            {
                GD.Print("ZERO-K : Il vous faut une Hachette (ID 106) pour standardiser/fendre le bois au sol.");
                rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);
                return;
            }
            if (!EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            {
                GD.Print("ZERO-K : Orientez le tranchant vers la cible — ce coup porte le manche ou le plat.");
                rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);
                return;
            }

            Vector3 axeBois = rbCible.GlobalTransform.Basis.Z.Normalized();
            float alignement = Mathf.Abs(directionFrappe.Normalized().Dot(axeBois));
            AppliquerUsureOutilMainActive(2.5f);

            if (alignement < 0.5f)
            {
                // COUPE TRANSVERSALE (Sur la largeur)
                float scaleZActuel = item.HasMeta("ScaleLongueurBois")
                    ? (float)item.GetMeta("ScaleLongueurBois").AsSingle()
                    : (item.Scale.Z > 0.1f ? item.Scale.Z : 1f);
                float vraieLongueur = (item.IndexTailleRoche == 0 ? 1.2f : 1.0f) * scaleZActuel;
                // axeBois déjà calculé juste avant alignement.

                // A) Débitage du Tronc Brut Géant (On tranche 1 mètre)
                if (item.IndexTailleRoche == 0 && vraieLongueur > 1.4f)
                {
                    float longueurRestante = Mathf.Max(0f, vraieLongueur - 1.0f);
                    int nbStandardsTotal = Mathf.Max(1, Mathf.FloorToInt(vraieLongueur / 1.0f));
                    GD.Print($"ZERO-K : Vous tranchez une Bûche Standard ({nbStandardsTotal} standards possibles). Reste du tronc : {longueurRestante:F1}m.");

                    var slotStandard = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = 1,
                        ScaleEclat = Vector3.One
                    };
                    var slotReste = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = 0,
                        // ScaleEclat.Z est un multiplicateur de la base 1.2m (pas une longueur en mètres).
                        ScaleEclat = new Vector3(1, 1, longueurRestante / 1.2f)
                    };

                    Vector3 centreCible = rbCible.GlobalPosition;
                    Vector3 lift = Vector3.Up * 0.35f;
                    Node3D pStandard = CreerBlocPose(centreCible + axeBois * (vraieLongueur * 0.4f) + lift, slotStandard);
                    Node3D pReste = CreerBlocPose(centreCible - axeBois * 0.5f + lift, slotReste);

                    if (pStandard != null) pStandard.GlobalRotation = rbCible.GlobalRotation;
                    if (pReste != null) pReste.GlobalRotation = rbCible.GlobalRotation;
                }
                // B) Le Tronc Brut est court (<= 1.4m), il devient Standard.
                else if (item.IndexTailleRoche == 0)
                {
                    GD.Print("ZERO-K : Le bout du tronc devient une Bûche Standard pure.");
                    var slotStandard = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = 1,
                        ScaleEclat = Vector3.One
                    };
                    Node3D p = CreerBlocPose(rbCible.GlobalPosition + Vector3.Up * 0.35f, slotStandard);
                    if (p != null) p.GlobalRotation = rbCible.GlobalRotation;
                }
                // C) Logique classique pour les Bûches (Standard -> Courte -> Rondin)
                else
                {
                    if (item.IndexTailleRoche >= 3)
                    {
                        GD.Print("ZERO-K : Ce bois est déjà trop court.");
                        return;
                    }
                    int nouvelleTaille = item.IndexTailleRoche + 1;
                    GD.Print($"ZERO-K : Coupe Transversale. Raccourcissement à l'étape {nouvelleTaille}.");
                    var boisRaccourci = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = nouvelleTaille,
                        ScaleEclat = Vector3.One,
                        EstUnEclat = false
                    };
                    Vector3 baseElev = rbCible.GlobalPosition + Vector3.Up * 0.4f;
                    Node3D piece1 = CreerBlocPose(baseElev + directionFrappe * 0.15f, boisRaccourci);
                    Node3D piece2 = CreerBlocPose(baseElev - directionFrappe * 0.15f, boisRaccourci);
                    if (piece1 != null) piece1.GlobalRotation = rbCible.GlobalRotation;
                    if (piece2 != null) piece2.GlobalRotation = rbCible.GlobalRotation;
                }
            }
            else
            {
                if (item.IndexTailleRoche == 0)
                {
                    if (item.ID_Objet == 30)
                        GD.Print("ZERO-K : Tronc Brut. Coupez-le sur la largeur d'abord pour le standardiser.");
                    else
                        GD.Print("ZERO-K : Brin brut. Coupez-le sur la largeur d'abord pour le standardiser.");
                    rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe * 0.5f);
                    return;
                }
                int fenteActuelle = MorphologieBoisDepuisItem(item);
                if (fenteActuelle >= 3)
                {
                    GD.Print("ZERO-K : Bois réduit à son épaisseur minimale (Planchette).");
                    rbCible.QueueFree();
                    return;
                }
                int nouvelleFente = fenteActuelle + 1;
                GD.Print($"ZERO-K : Coupe Longitudinale. Fente à l'étape {nouvelleFente}.");
                var boisFendu = new SlotInventaire
                {
                    ID = item.ID_Objet,
                    IndexBotanique = item.IndexBotanique,
                    IndexMorphologique = nouvelleFente,
                    IndexChimique = item.IndexChimique,
                    IndexTaille = item.IndexTailleRoche,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false
                };
                Vector3 baseElevLong = rbCible.GlobalPosition + Vector3.Up * 0.4f;
                Node3D b1 = CreerBlocPose(baseElevLong + Vector3.Right * 0.1f, boisFendu);
                Node3D b2 = CreerBlocPose(baseElevLong + Vector3.Left * 0.1f, boisFendu);
                if (b1 != null) b1.GlobalRotation = rbCible.GlobalRotation;
                if (b2 != null) b2.GlobalRotation = rbCible.GlobalRotation;
            }
            rbCible.QueueFree();
            return;
        }

        rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);

        if ((mainActive.ID == 105 || mainActive.ID == 106) && ItemPhysique.EstIdRocheMatiere(item.ID_Objet))
        {
            bool tranchantOk = mainActive.ID == 105
                ? EstFrappeDagueAvecLaLame(pointImpact, directionFrappe)
                : EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
            if (tranchantOk)
                GD.Print("ZERO-K : L’outil ne peut pas briser cette roche — trop léger. Il faut un choc contondant ou une pierre lancée.");
            else
                GD.Print("ZERO-K : Vous heurtez la pierre avec le manche ou le plat, sans effet de taille.");
            return;
        }

        if ((mainActive.ID == 105 && !EstFrappeDagueAvecLaLame(pointImpact, directionFrappe))
            || (mainActive.ID == 106 && !EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe)))
        {
            GD.Print("ZERO-K : Orientez le tranchant vers la cible — ce coup porte le manche ou le plat.");
            return;
        }

        Vector3 dirVue = (pointImpact - _camera.GlobalPosition).Normalized();
        int resultatFracture = item.SubirDegats(forceImpact, dirVue, pointImpact);
        if (resultatFracture == 0)
            GD.Print("ZERO-K : L'impact n'est pas assez puissant. La roche résonne mais ne cède pas (Rebond).");
        else if (mainActive.ID == 105 || mainActive.ID == 106)
            AppliquerUsureOutilMainActive(2.15f + forceImpact * 0.017f);
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
                    ? ArbreVivant.ObtenirMaterielBoisTriplanar()
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
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar()
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
            else if (id == 20 || id == 21)
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
        }
        // Fibres / corde non élastiques : ne pas appliquer d’échelle « étirée » (herbe, liane, corde boyau+herbe, etc.)
        bool estFlexOuCorde = id == 15 || id == 16 || id == 17 || id == 20 || id == 21;
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
            if (!mainActive.EstVide && mainActive.ID != 200 && Input.IsActionPressed("clic_droit"))
                _forceLancer = Mathf.Min(5.0f, _forceLancer + (VitesseChargeBras * 2.5f) * dt);
        }
        else
        {
            if (_gaucheMaintenu) _gaucheMaintenu = false;
            _forceLancer = 0f;
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
