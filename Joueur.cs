using Godot;
using System;
using System.Collections.Generic;

/// <summary>Propriétés d'une matière flexible (herbe, liane, boyau, racine traitée...). Comme TableGeologique mais pour les fibres.</summary>
public struct ProfilMatiereFlexible
{
    public string Nom;
    public Color CouleurCorde;      // Teinte que donne cette matière quand tressée
    public float Durabilite;        // Résistance à l'usure (0-20)
    public float TensionMax;       // Charge avant rupture (0-20)
    public float Flexibilite;      // 0-1 : capacité à être tressée/retressée (herbe=1, liane=0.7, boyau=0.5)
    public bool Fragile;           // Se dégrade vite
    public bool Etirable;          // Peut s'allonger sous tension
}

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
    }

    public bool EstVide => ID == 0;
}

public partial class Joueur : CharacterBody3D
{
    /// <summary>Méta et slots : même clé pour l’établi CAO et les corps posés.</summary>
    public const string MetaGenomeAssemblage = "GenomeAssemblage";

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

    /// <summary>Nom lisible pour l’UI (menu anatomie, inventaire) à partir du slot en main.</summary>
    public static string ObtenirNomObjet(SlotInventaire slot)
    {
        if (slot.EstVide)
            return "";
        int id = slot.ID;
        if (id >= 40 && id <= 49)
        {
            int indexMatiere = id - 40;
            string matiere = "Matière Inconnue";
            if (indexMatiere >= 0 && indexMatiere < ItemPhysique.TableGeologique.Length)
                matiere = ItemPhysique.TableGeologique[indexMatiere].Nom;

            string taille = slot.IndexTaille switch { 0 => "Mini", 1 => "Petite", 2 => "Moyenne", 3 => "Grosse", 4 => "Énorme", _ => "" };
            string forme = slot.IndexMorphologique switch { 0 => "Ronde", 1 => "Plate", 2 => "Ovale", 3 => "en Pointe", _ => "Déformée" };

            return $"{taille} Roche {forme} en {matiere}";
        }
        if (id == 100)
        {
            int clef = ClefRegistreOutilForge(slot);
            if (clef != 0 && RegistreOutilsForges.TryGetValue(clef, out var st) && !string.IsNullOrEmpty(st.Nom))
                return st.Nom;
            return "Outil forgé";
        }
        if (id == IdObjetSacDos) return "Sac à dos";
        if (id == IdObjetCeinturePoches) return "Ceinture à poches";
        if (ObtenirProfilFlexible(id, out var flex))
            return flex.Nom;
        if (id == 20)
        {
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"{pa.Nom}+{pb.Nom}";
            if (a)
                return pa.Nom;
            if (b)
                return pb.Nom;
            return "Corde";
        }
        return id switch
        {
            1 => "Terre",
            2 => "Roche",
            3 => "Sable",
            4 => "Neige",
            5 => "Neige / glace",
            6 => "Terre aride",
            7 => "Boue",
            8 => "Terre tropicale",
            9 => "Terre gelée",
            30 => "Bûche",
            32 => "Bâton",
            34 => "Feuillage",
            999 => "Végétation",
            _ => $"Objet #{id}"
        };
    }

    public enum TypeMouvementFrappe { Estoc, DeHautEnBas, DeBasEnHaut, GaucheADroite, DroiteAGauche }

    public const float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;

    // Sensibilité chirurgicale de la souris
    public const float MouseSensitivity = 0.003f;

    /// <summary>Rayon du pinceau de sculpture (minage ET pose). Symétrie absolue.</summary>
    private const float RAYON_SCULPTURE = 1.0f;

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

    /// <summary>Grille 2×2 de l’inventaire (Q) : assemblage / craft futur ; indices 0–1 ligne du haut, 2–3 ligne du bas.</summary>
    public SlotInventaire[] GrilleCraft2x2 = new SlotInventaire[4];

    /// <summary>Slot contenant le résultat d'une recette valide.</summary>
    public SlotInventaire SlotResultatCraft = new SlotInventaire();

    /// <summary>Analyse la grille 2x2 et applique une recette stricte si correspondante.</summary>
    public void VerifierRecettes()
    {
        SlotResultatCraft = new SlotInventaire();
        if (GrilleCraft2x2 == null || GrilleCraft2x2.Length < 4) return;

        var ingredients = new List<SlotInventaire>();
        for (int i = 0; i < 4; i++)
        {
            if (!GrilleCraft2x2[i].EstVide)
                ingredients.Add(GrilleCraft2x2[i]);
        }

        if (ingredients.Count == 0) return;

        // ==========================================
        // MATRICE DES RECETTES (DÉTERMINISTE)
        // ==========================================

        // RECETTE 1 : Corde — EXACTEMENT 2 × Herbe ID 15
        if (ingredients.Count == 2 && ingredients[0].ID == 15 && ingredients[1].ID == 15)
        {
            SlotResultatCraft = new SlotInventaire
            {
                ID = 20,
                IndexChimique = 15,
                IndexMorphologique = 15,
                EstUnEclat = false,
                NiveauFracture = 0
            };
            return;
        }
    }

    /// <summary>Vide les quatre cases de la grille craft (consommation après prise du résultat).</summary>
    public void ConsommerIngredientsCraft()
    {
        if (GrilleCraft2x2 == null || GrilleCraft2x2.Length < 4) return;
        for (int i = 0; i < 4; i++)
        {
            if (!GrilleCraft2x2[i].EstVide)
                GrilleCraft2x2[i] = new SlotInventaire();
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

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _camera = GetNode<Camera3D>("Camera3D");
        _rayon = GetNode<RayCast3D>("Camera3D/RayCast3D");
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
            string n = ObtenirNomObjet(MainGauche);
            _lblHudNomMainG.Text = MainGauche.EstVide ? "" : n;
            _lblHudNomMainG.Visible = !MainGauche.EstVide && !string.IsNullOrEmpty(n);
        }
        if (_lblHudNomMainD != null)
        {
            string n = ObtenirNomObjet(MainDroite);
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

    public override void _Input(InputEvent @event)
    {
        if (_menuAnatomie != null && @event.IsActionPressed("inventaire"))
        {
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
                // IDENTIFICATION DE LA MATIÈRE : Est-ce du terrain (Voxel) ?
                bool estTerrainVoxel = mainActive.ID >= 1 && mainActive.ID <= 9;
                // Clic bref = poser. Maintien du clic = lancer (seuil ~0,4 s pour éviter de lancer par accident).
                if (estTerrainVoxel || _forceLancer < 0.4f)
                {
                    ExecuterPlacement();
                }
                else
                {
                    ExecuterLancer(Mathf.Clamp(_forceLancer, 0.5f, 2.0f));
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
                        _rotationManuelleZ += 15f;
                        if (_rotationManuelleZ >= 360f) _rotationManuelleZ -= 360f;
                    }
                    else if (keyEvent.ShiftPressed)
                    {
                        _rotationManuelleX += 15f;
                        if (_rotationManuelleX >= 360f) _rotationManuelleX -= 360f;
                    }
                    else
                    {
                        _rotationManuelleY += 15f;
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
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            return;
        }
        Mesh m = main.EstUnEclat ? main.MeshEclat : ObtenirMeshDepuisCache(main.ID, main.IndexMorphologique, main.IndexTaille);
        _objetEnMain.Mesh = m;
        // CylinderMesh : hauteur sur Y, rayon sur X/Z — ScaleEclat (r,r,h) du monde posé → (r,h,r) en main
        if ((main.ID == 30 || main.ID == 32) && main.ScaleEclat.LengthSquared() > 1e-6f)
        {
            float r = main.ScaleEclat.X;
            float h = main.ScaleEclat.Z;
            _objetEnMain.Scale = new Vector3(r * 0.45f, h * 0.45f, r * 0.45f);
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
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0);
            else if (main.ID >= 1 && main.ID <= 9)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0);
            else
                _objetEnMain.MaterialOverride = null;
        }
        else if (m != null)
            AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, main.ID == 20 ? main.IndexMorphologique : 0, main.ID == 20 ? main.NiveauFracture : 0);
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
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            return;
        }
        Mesh m = slot.EstUnEclat ? slot.MeshEclat : ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique, slot.IndexTaille);
        meshNode.Mesh = m;
        if (slot.ID == 30 || slot.ID == 32)
        {
            Vector3 s = slot.ScaleEclat.LengthSquared() > 1e-6f ? slot.ScaleEclat : Vector3.One;
            meshNode.Scale = new Vector3(s.X, s.Z, s.X);
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
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
            else if (slot.ID >= 1 && slot.ID <= 9)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
            else
                meshNode.MaterialOverride = null;
        }
        else if (m != null)
            AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, slot.ID == 20 ? slot.IndexMorphologique : 0, slot.ID == 20 ? slot.NiveauFracture : 0);
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
        return s.ID == 100 && s.EstUnEclat && s.MeshEclat != null;
    }

    /// <summary>True si l'objet a un mesh à afficher en main / preview.</summary>
    private static bool EstObjetAvecVisuel(int id)
    {
        if (id >= 1 && id <= 9) return true;
        return ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 30 || id == 32 || id == 34 || id == 100;
    }

    private static bool EstMatiereFlexible(int id)
    {
        int[] flexibles = { 15, 16, 17, 20 }; // 20 = corde : flexible, peut être retressée
        return Array.IndexOf(flexibles, id) != -1;
    }

    private static bool EstObjetRigide(int id)
    {
        return ItemPhysique.EstIdRocheMatiere(id);
    }

    /// <summary>Table des matières flexibles (comme TableGeologique pour les roches). ID 15=herbe, 16=liane, 17=boyau. Ajouter racine traitée etc. plus tard.</summary>
    private static readonly ProfilMatiereFlexible[] TableMatiereFlexible = new ProfilMatiereFlexible[]
    {
        new ProfilMatiereFlexible { Nom = "Herbe", CouleurCorde = new Color(0.35f, 0.52f, 0.18f), Durabilite = 4f, TensionMax = 3f, Flexibilite = 1f, Fragile = true, Etirable = false },
        new ProfilMatiereFlexible { Nom = "Liane", CouleurCorde = new Color(0.4f, 0.38f, 0.22f), Durabilite = 10f, TensionMax = 8f, Flexibilite = 0.7f, Fragile = false, Etirable = false },
        new ProfilMatiereFlexible { Nom = "Boyau", CouleurCorde = new Color(0.6f, 0.45f, 0.35f), Durabilite = 14f, TensionMax = 14f, Flexibilite = 0.5f, Fragile = false, Etirable = true }
    };

    private const float SEUIL_MIN_FLEXIBILITE = 0.18f;   // En-dessous = trop rigide pour tresser
    private const float PERTE_FLEX_PAR_MIX = 0.38f;      // Chaque retressage réduit la flexibilité (~38 %)

    private static int IdFlexibleToIndex(int id)
    {
        if (id == 15) return 0; if (id == 16) return 1; if (id == 17) return 2;
        return -1;
    }

    private static bool ObtenirProfilFlexible(int id, out ProfilMatiereFlexible p)
    {
        int i = IdFlexibleToIndex(id);
        if (i < 0 || i >= TableMatiereFlexible.Length) { p = default; return false; }
        p = TableMatiereFlexible[i]; return true;
    }

    /// <summary>Flexibilité effective d'un slot : fibre = Flexibilite de la table, corde = baseFlex * (1 - perte par niveau). Tier 2 + tier 1 = on peut tresser si les deux ont assez de flex.</summary>
    private static float ObtenirFlexibiliteEffective(SlotInventaire slot)
    {
        if (slot.ID == 20)
        {
            float fa = ObtenirProfilFlexible(slot.IndexChimique, out var pa) ? pa.Flexibilite : 0.5f;
            float fb = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb) ? pb.Flexibilite : 0.5f;
            float baseFlex = (fa + fb) * 0.5f;
            return baseFlex * Mathf.Max(0f, 1f - slot.NiveauFracture * PERTE_FLEX_PAR_MIX);
        }
        return ObtenirProfilFlexible(slot.ID, out var p) ? p.Flexibilite : 0f;
    }

    /// <summary>Teinte de la corde selon les deux matières tressées. Chaque retressage assombrit un peu.</summary>
    private static Color ObtenirTeinteCordeTressage(int idMatiereA, int idMatiereB, int niveauTressage = 0)
    {
        bool okA = ObtenirProfilFlexible(idMatiereA, out var pa);
        bool okB = ObtenirProfilFlexible(idMatiereB, out var pb);
        Color c;
        if (!okA && !okB) c = new Color(0.52f, 0.42f, 0.28f);
        else if (!okA) c = pb.CouleurCorde;
        else if (!okB) c = pa.CouleurCorde;
        else c = new Color(
            (pa.CouleurCorde.R + pb.CouleurCorde.R) * 0.5f,
            (pa.CouleurCorde.G + pb.CouleurCorde.G) * 0.5f,
            (pa.CouleurCorde.B + pb.CouleurCorde.B) * 0.5f
        );
        if (niveauTressage > 0) c = c * Mathf.Pow(0.84f, niveauTressage);
        return c;
    }

    /// <summary>Matériau corde : si 2 matières différentes = dégradé (on voit ce qui est mixé). Chaque retressage assombrit.</summary>
    private static Material ObtenirMaterielCorde(int idA, int idB, int niveauTressage)
    {
        float assombri = niveauTressage > 0 ? Mathf.Pow(0.84f, niveauTressage) : 1f;
        Color ca = (ObtenirProfilFlexible(idA, out var pa) ? pa.CouleurCorde : new Color(0.52f, 0.42f, 0.28f)) * assombri;
        Color cb = (ObtenirProfilFlexible(idB, out var pb) ? pb.CouleurCorde : new Color(0.52f, 0.42f, 0.28f)) * assombri;
        var mat = new StandardMaterial3D { Roughness = 0.85f };
        if (idA == idB)
        {
            mat.AlbedoColor = ca;
        }
        else
        {
            var grad = new Gradient();
            grad.AddPoint(0f, ca);
            grad.AddPoint(1f, cb);
            var tex = new GradientTexture2D { Width = 32, Height = 64, Gradient = grad };
            tex.FillFrom = new Vector2(0.5f, 0f);
            tex.FillTo = new Vector2(0.5f, 1f);
            mat.AlbedoTexture = tex;
        }
        return mat;
    }

    /// <summary>Durabilité et tension de la corde : tressage = flexible mais un peu moins que les brins bruts, mais plus résistant et supporte plus de tension/force.</summary>
    private static void ObtenirStatsCorde(int idA, int idB, out float durabilite, out float tensionMax)
    {
        bool okA = ObtenirProfilFlexible(idA, out var pa);
        bool okB = ObtenirProfilFlexible(idB, out var pb);
        if (!okA && !okB) { durabilite = 6f; tensionMax = 5f; return; }
        if (!okA) { pa = pb; } if (!okB) { pb = pa; }
        float baseDurabilite = (pa.Durabilite + pb.Durabilite) * 0.5f;
        float baseTension = (pa.TensionMax + pb.TensionMax) * 0.5f;
        durabilite = baseDurabilite * 1.35f;  // Corde plus résistante que les fibres brutes
        tensionMax = baseTension * 1.5f;      // Supporte plus de tension et de force
        if (pa.Fragile || pb.Fragile) durabilite *= 0.75f;
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
    private static Vector3 ScaleEclatDepuisItemBois(ItemPhysique item)
    {
        if (item == null) return Vector3.One;
        // Mesh déjà « cuit » en taille réelle au sol : ne pas remettre ScaleEclat (sinon double échelle en main).
        if (item.HasMeta(ItemPhysique.MetaScaleEclatInventaire))
            return Vector3.One;
        Vector3 sc = item.Scale;
        if (sc.LengthSquared() < 1e-8f) sc = Vector3.One;
        foreach (Node c in item.GetChildren())
        {
            if (c is MeshInstance3D m && m.Mesh is CylinderMesh cy)
            {
                if (item.ID_Objet == 30)
                    return new Vector3(cy.TopRadius / 0.12f * sc.X, cy.TopRadius / 0.12f * sc.Y, cy.Height / 0.6f * sc.Z);
                if (item.ID_Objet == 32)
                    return new Vector3(cy.TopRadius / 0.02f * sc.X, cy.TopRadius / 0.02f * sc.Y, cy.Height / 0.5f * sc.Z);
            }
        }
        // Bûche/bâton taillé : plus de CylinderMesh (mesh procédural). Le scale du RigidBody = ScaleEclat posé (non écrasé si EstUnEclat).
        if (item.ID_Objet == 30 || item.ID_Objet == 32)
            return sc;
        return Vector3.One;
    }

    private static int CalculerIndexMorphoBois(float rayonM, float longueurM, int idObjet)
    {
        int r = Mathf.Clamp((int)(rayonM * 2500f), 20, 500);
        int L = Mathf.Clamp((int)(longueurM * 250f), 25, 900);
        unchecked
        {
            int h = idObjet * 73856093 + r * 19349663 + L * 83492791;
            return h == 0 ? 1 : h;
        }
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
        else if (id == 20) return CreerMeshCordeTressee();
        // Træ (30) og pinde (32) — synlighed i inventar og hånd
        else if (id == 30) return new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.6f };
        else if (id == 32) return new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.5f };
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
            visuel.MaterialOverride = ObtenirProfilFlexible(idObjet, out var pf)
                ? new StandardMaterial3D { AlbedoColor = pf.CouleurCorde, Roughness = 0.9f, Metallic = 0f }
                : new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.55f, 0.15f), Roughness = 0.9f };
            return;
        }
        if (idObjet == 20) { visuel.MaterialOverride = ObtenirMaterielCorde(indexChimique, indexMorphologique, niveauTressage); return; }
        if (idObjet == 30 || idObjet == 32) { visuel.MaterialOverride = ArbreVivant.ObtenirMaterielBois(); return; }
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
                if (ip.ID_Objet == 15 || ip.ID_Objet == 20 || ip.ID_Objet == 34)
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
        Object colliderObj = _rayon.GetCollider();
        Node objetTouche = colliderObj as Node;
        // ArbreVivant : coupe avec pierre ou silex — branches et bûches tombent au sol
        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchant = ItemPhysique.EstIdRocheMatiere(main.ID) || main.EstUnEclat;
            if (!outilTranchant) return;

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
            }
            Vector3 pointImpact = _rayon.GetCollisionPoint();
            Vector3 directionFrappe = -_rayon.GetCollisionNormal();
            if (directionFrappe.LengthSquared() < 0.1f)
                directionFrappe = -_camera.GlobalTransform.Basis.Z.Normalized();
            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, degatsArbre, epaisseurLame);
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
        if (s.ID == 20)
        {
            bool a = ObtenirProfilFlexible(s.IndexChimique, out var pa) && pa.Etirable;
            bool b = ObtenirProfilFlexible(s.IndexMorphologique, out var pb) && pb.Etirable;
            return a && b;
        }
        if (EstMatiereFlexible(s.ID))
            return ObtenirProfilFlexible(s.ID, out var p) && p.Etirable;
        return false;
    }

    /// <summary>Échelle pour l’établi CAO (hors 30/32, gérés à part) : fibres/corde non élastiques = taille naturelle, sans ScaleEclat « étiré ».</summary>
    public static Vector3 ObtenirEchellePieceFlexibleCAO(SlotInventaire slot)
    {
        bool estFlexOuCorde = slot.ID == 15 || slot.ID == 16 || slot.ID == 17 || slot.ID == 20;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(slot))
            return Vector3.One;
        if (slot.ScaleEclat != Vector3.Zero)
            return slot.ScaleEclat;
        return Vector3.One;
    }

    /// <summary>Fibres + corde : manipulation fine sur le plan de l’établi (rayon réduit).</summary>
    public static bool EstFlexibleOuCordePourPlanCAO(int idObjet) => idObjet is 15 or 16 or 17 or 20;

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (s.ID >= 1 && s.ID <= 9 && s.ID != 4) return true;
        return s.ID == 999 || ItemPhysique.EstIdRocheMatiere(s.ID) || s.ID == 30 || s.ID == 32 || s.ID == 34;
    }

    /// <summary>Corde (20) : accrocher au point de visée si surface valide (sol, roche, arbre, bloc posé).</summary>
    private bool ExecuterAttacheCordeSiPossible(SlotInventaire mainCorde)
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        var col = _rayon.GetCollider() as Node;
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

        Node objetTouche = (Node)_rayon.GetCollider();
        if (objetTouche == null) return;

        SlotInventaire nouveauSlot = default;

        if (objetTouche.IsInGroup("BlocsPoses"))
        {
            int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
            var item = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            nouveauSlot = new SlotInventaire
            {
                ID = id,
                IndexMorphologique = item?.IndexCacheMemoire ?? 0,
                IndexChimique = item?.IndexChimique ?? 0,
                IndexTaille = item != null && ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2,
                EstUnEclat = item?.EstUnEclat ?? false,
                MeshEclat = (item != null && item.EstUnEclat) ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item?.NiveauFracture ?? 0,
                ScaleEclat = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32)
                    ? ScaleEclatDepuisItemBois(item)
                    : (item != null ? item.Scale : Vector3.One),
                IndexBotanique = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
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
                IndexMorphologique = item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2,
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32) ? ScaleEclatDepuisItemBois(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
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
                IndexMorphologique = item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 2,
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32) ? ScaleEclatDepuisItemBois(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32) ? item.IndexBotanique : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item)
            };
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
        Vector3 pointDeChute = pointImpact + (normaleImpact * 0.1f);
        float distance = GlobalPosition.DistanceTo(pointDeChute);
        // Flexible / corde avec E : on peut poser près du corps (manipulation fine) ; clic droit garde la marge anti-auto-collision
        bool flexOuCordeE = depuisInteragir && (EstMatiereFlexible(mainActive.ID) || mainActive.ID == 20);
        float distMin = flexOuCordeE ? 0.35f : 1.4f;
        if (distance < distMin) return;

        int id = mainActive.ID;
        if (id == 0) return;
        if (id >= 1 && id <= 9 && id != 4)
        {
            _gestionnaireMonde?.AppliquerCreationGlobale(pointImpact, normaleImpact, RAYON_SCULPTURE, id);
        }
        else if (id == 999 || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 30 || id == 32 || id == 34)
        {
            CreerBlocPose(pointDeChute, mainActive);
        }
        else
        {
            GD.Print($"ZERO-K : Matière {id} non géologique. Pose ignorée.");
            return;
        }

        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;

        RafraichirHUD();
    }

    /// <summary>Terrain voxel / sections de sol : creusage (pelle) ou fauchage (lame) selon l’outil émergent.</summary>
    private static bool EstSurfaceTerrainVisee(Node n)
    {
        if (n == null) return false;
        if (n.IsInGroup("Terrain")) return true;
        string nm = n.Name.ToString();
        return nm.Contains("Terrain") || nm.Contains("CollisionSection");
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

        return epaisseurLame;
    }

    /// <summary>Relâchement clic gauche : sol → creusage / fauchage ; sinon frappe roches, arbres, rigides.</summary>
    private void ExecuterAction(float force, TypeMouvementFrappe mouvement)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive)) return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;

        Node objetTouche = _rayon.GetCollider() as Node;
        Vector3 pointImpact = _rayon.GetCollisionPoint();

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (mouvement == TypeMouvementFrappe.DeHautEnBas) directionMouvement = -_camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.DeBasEnHaut) directionMouvement = _camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.GaucheADroite) directionMouvement = _camera.GlobalTransform.Basis.X.Normalized();
        else if (mouvement == TypeMouvementFrappe.DroiteAGauche) directionMouvement = -_camera.GlobalTransform.Basis.X.Normalized();

        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);

        if (EstSurfaceTerrainVisee(objetTouche))
        {
            ExecuterCreusage(force, effPelle, masseOutil, pointImpact);
            return;
        }

        ExecuterFrappePhysique(force, effHache, masseOutil, objetTouche, pointImpact, directionMouvement);
    }

    private void JouerAnimationFrappe(TypeMouvementFrappe type)
    {
        if (_objetEnMain == null || _objetEnMain.Mesh == null) return;
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
            if (mainActive.EstUnEclat || ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            {
                _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 1.5f);
                GD.Print("ZERO-K : Lame sur le sol. Fauchage en cours.");
                return;
            }
            GD.Print("ZERO-K : L'angle de cette lame ne permet pas de déplacer la terre. Il vous faut une surface plate (Pelle/Houe).");
            return;
        }

        float forceCreusage = masseOutil * force * efficacitePelle;

        if (forceCreusage > 10f)
        {
            GD.Print($"ZERO-K : Extraction du sol réussie. (Force Volume: {forceCreusage:F1})");
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
        else if (mainActive.EstUnEclat && mainActive.MeshEclat != null && mainActive.ID != 100)
            multiplicateurLame = Mathf.Min(multiplicateurLame, 40.0f);

        float forceImpact = (masseOutil * force * 15f) * multiplicateurLame;
        float epaisseurLame = CalculerEpaisseurLamePourImpact(mainActive, directionFrappe);

        if (objetTouche == null)
            return;

        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            bool outilTranchant = ItemPhysique.EstIdRocheMatiere(mainActive.ID) || mainActive.EstUnEclat;
            if (!outilTranchant) return;

            float forceCoupe = forceImpact;
            if (mainActive.EstUnEclat && arbre.AgeEnJours <= 2)
                forceCoupe = Mathf.Max(forceCoupe, arbre.AgeEnJours <= 1 ? 36f : 48f);

            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, forceCoupe, epaisseurLame);
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
            bool outilTranchant = ItemPhysique.EstIdRocheMatiere(main.ID) || main.EstUnEclat;
            if (!outilTranchant) return;

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
            }
            else
            {
                float pv = rbCible.HasMeta("PV") ? (float)rbCible.GetMeta("PV") : 50f;
                int age = rbCible.HasMeta("Age") ? (int)rbCible.GetMeta("Age") : 1;

                float seuilBoisMort = 30f + (age * 10f);
                if (forceImpact < seuilBoisMort || (epaisseurLame > 0.05f && age >= 3))
                {
                    GD.Print("ZERO-K : Rebond. Le bois absorbe le coup. L'outil manque d'inertie ou de tranchant.");
                    rbCible.ApplyCentralImpulse(directionFrappe * (5f * force));
                    return;
                }

                pv -= forceImpact;
                rbCible.SetMeta("PV", pv);
                JouerSonEtEffetCoupeArbre(pointImpact);

                if (pv <= 0)
                {
                    float hauteurTronc = rbCible.HasMeta("HauteurTronc") ? (float)rbCible.GetMeta("HauteurTronc") : (1.0f + age * 1.0f);
                    float rayonBase = rbCible.HasMeta("RayonTroncBase") ? (float)rbCible.GetMeta("RayonTroncBase") : (0.15f + age * 0.05f);
                    float rayonSommet = rbCible.HasMeta("RayonTroncSommet") ? (float)rbCible.GetMeta("RayonTroncSommet") : rayonBase;
                    float rayonTronc = Mathf.Max(0.04f, (rayonBase + rayonSommet) * 0.5f);
                    Vector3 scaleTronc = new Vector3(rayonTronc / 0.12f, rayonTronc / 0.12f, hauteurTronc / 0.6f);
                    Vector3 centreTronc = rbCible.GlobalPosition + new Vector3(0, hauteurTronc * 0.5f, 0);

                    byte essenceBois = rbCible.HasMeta("IndexBotanique")
                        ? (byte)Mathf.Clamp((int)rbCible.GetMeta("IndexBotanique").AsInt32(), 0, 255)
                        : LSystem_Botanique.IndexChene;
                    int morphoBuche = CalculerIndexMorphoBois(rayonTronc, hauteurTronc, 30);
                    var slotBuche = new SlotInventaire
                    {
                        ID = 30,
                        ScaleEclat = scaleTronc,
                        IndexBotanique = essenceBois,
                        IndexMorphologique = morphoBuche
                    };
                    var buche = CreerBlocPose(centreTronc, slotBuche);
                    if (buche is RigidBody3D rbBuche)
                        rbBuche.ApplyCentralImpulse(new Vector3((float)GD.Randf() - 0.5f, 2f, (float)GD.Randf() - 0.5f) * 2f);

                    int nbBranches = 2 + age + (int)((float)GD.Randf() * 2f);
                    float longueurBrancheMoy = rbCible.HasMeta("LongueurBrancheMoy") ? (float)rbCible.GetMeta("LongueurBrancheMoy") : (0.8f + age * 0.4f);
                    float epaisseurBrancheMoy = rbCible.HasMeta("EpaisseurBrancheMoy") ? (float)rbCible.GetMeta("EpaisseurBrancheMoy") : (0.03f + age * 0.01f);

                    for (int i = 0; i < nbBranches; i++)
                    {
                        float L = longueurBrancheMoy * (0.5f + (float)GD.Randf() * 1.0f);
                        float e = epaisseurBrancheMoy * (0.55f + (float)GD.Randf() * 0.85f);
                        L = Mathf.Clamp(L, 0.12f, 4f);
                        e = Mathf.Clamp(e, 0.006f, 0.14f);
                        Vector3 scaleBranche = new Vector3(e / 0.02f, e / 0.02f, L / 0.5f);
                        int morphoBaton = CalculerIndexMorphoBois(e, L, 32);
                        var slotBaton = new SlotInventaire
                        {
                            ID = 32,
                            ScaleEclat = scaleBranche,
                            IndexBotanique = essenceBois,
                            IndexMorphologique = morphoBaton
                        };
                        Vector3 offset = new Vector3((float)GD.Randf() - 0.5f, hauteurTronc * 0.75f + (i * 0.35f), (float)GD.Randf() - 0.5f);
                        var baton = CreerBlocPose(rbCible.GlobalPosition + offset, slotBaton);
                        if (baton is RigidBody3D rbBaton)
                            rbBaton.ApplyCentralImpulse(new Vector3((float)GD.Randf() - 0.5f, 1f, (float)GD.Randf() - 0.5f) * 3f);
                    }
                    rbCible.QueueFree();
                    GD.Print("ZERO-K : L'arbre est démembré. Le bois réagit à la gravité.");
                }
                else
                {
                    Vector3 dirPousseCadavre = -_rayon.GetCollisionNormal();
                    rbCible.ApplyCentralImpulse(dirPousseCadavre * (5f * force));
                }
            }
            return;
        }

        if (rbCible.Name.ToString().Contains("BrancheMorte"))
        {
            var mainB = MainGaucheEstActive ? MainGauche : MainDroite;
            bool tranchantB = ItemPhysique.EstIdRocheMatiere(mainB.ID) || mainB.EstUnEclat;
            if (!tranchantB) return;
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
        rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);

        if (item.ID_Objet == 30 || item.ID_Objet == 32)
        {
            GD.Print("ZERO-K : Ce bois durci ne peut pas être fendu davantage avec ces outils rudimentaires.");
            return;
        }

        Vector3 dirVue = (pointImpact - _camera.GlobalPosition).Normalized();
        int resultatFracture = item.SubirDegats(forceImpact, dirVue, pointImpact);
        if (resultatFracture == 0)
            GD.Print("ZERO-K : L'impact n'est pas assez puissant. La roche résonne mais ne cède pas (Rebond).");
    }

    /// <summary>Point de spawn du lancer : rayon depuis la caméra, arrêté au premier obstacle (sol / relief) pour ne pas faire apparaître l’objet sous le terrain.</summary>
    private Vector3 CalculerPointSpawnLancer(Vector3 direction)
    {
        direction = direction.Normalized();
        Vector3 orig = _camera.GlobalPosition;
        float portee = 2.8f;
        var query = PhysicsRayQueryParameters3D.Create(orig, orig + direction * portee);
        query.CollisionMask = 0xFFFFFFFF;
        var excl = new Godot.Collections.Array<Rid> { GetRid() };
        query.Exclude = excl;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
        {
            Vector3 pos = ((Vector3)hit["position"]);
            Vector3 n = hit.ContainsKey("normal") ? ((Vector3)hit["normal"]).Normalized() : Vector3.Up;
            return pos + n * 0.28f + direction * 0.12f;
        }
        return orig + direction * 1.5f;
    }

    /// <summary>Lance la roche tenue : spawn devant la caméra + impulsion (raycast pour ne pas traverser le sol).</summary>
    private void ExecuterLancer(float force)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide) return;

        Vector3 direction = -_camera.GlobalTransform.Basis.Z.Normalized();
        Vector3 pointDeSpawn = CalculerPointSpawnLancer(direction);

        // 2. On invoque le bloc
        Node3D corpsCree = CreerBlocPose(pointDeSpawn, mainActive);

        // 3. Si c'est un objet soumis à la gravité, on applique l'énergie cinétique
        if (corpsCree is RigidBody3D rb)
        {
            rb.ApplyCentralImpulse(direction * (15f * force));
        }

        // 4. On vide la main
        if (MainGaucheEstActive) MainGauche = default;
        else MainDroite = default;
        RafraichirHUD();
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
                    ? ArbreVivant.ObtenirMaterielBois()
                    : ItemPhysique.CreerMaterielProcedural(ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID), chimPourRoche);
            }
            item.AddChild(new MeshInstance3D { Name = "MeshInstance3D", Mesh = meshPose, MaterialOverride = matVisuel });
            item.AddChild(new CollisionShape3D { Name = "CollisionShape3D", Shape = ItemPhysique.CreerShapeCollisionConvexeRobuste(meshPose) });
            corps = item;
        }
        else if (ItemPhysique.EstIdRocheMatiere(id))
        {
            float tailleBase = mainActive.IndexTaille switch { 0 => 0.08f, 1 => 0.15f, 2 => 0.25f, 3 => 0.40f, 4 => 0.65f, _ => 0.2f };
            Vector3 scaleForme = ItemPhysique.EchelleMorphologieRoche(mainActive.IndexMorphologique);
            var item = new ItemPhysique
            {
                ID_Objet = id,
                IndexChimique = id - ItemPhysique.IdRocheMatiereMin,
                IndexCacheMemoire = mainActive.IndexMorphologique,
                IndexTailleRoche = Mathf.Clamp(mainActive.IndexTaille, 0, 4),
                NiveauFracture = mainActive.NiveauFracture,
                Name = "ItemPhysique",
                Scale = scaleForme
            };
            var meshNode = new MeshInstance3D { Mesh = new SphereMesh { Radius = tailleBase, Height = tailleBase * 2f } };
            AppliquerMaterielObjet(meshNode, id, ItemPhysique.IndexChimiqueDepuisIdRoche(id), 0, 0);
            item.AddChild(meshNode);
            item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = tailleBase } });
            corps = item;
        }
        else if (id == 15 || id == 16 || id == 17) // Fibres flexibles : fagot de brins (teinte selon profil)
        {
            Color teinte = ObtenirProfilFlexible(id, out var profilF)
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
        else if (id == 20) // Tressage / corde : dégradé des 2 matières (on voit ce qui est mixé). Chaque retressage assombrit.
        {
            int idA = mainActive.IndexChimique, idB = mainActive.IndexMorphologique;
            var item = new ItemPhysique { ID_Objet = id, IndexChimique = idA, IndexCacheMemoire = idB, NiveauFracture = mainActive.NiveauFracture, Name = "ItemPhysique" };
            var matCorde = ObtenirMaterielCorde(idA, idB, mainActive.NiveauFracture);
            item.AddChild(new MeshInstance3D { Mesh = CreerMeshCordeTressee(), MaterialOverride = matCorde });
            item.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.045f, Height = 0.28f } });
            corps = item;
        }
        else if (id == 30) // Bûche : cylindre aux vraies dimensions (hitbox = mesh, sans scale sur le RigidBody)
        {
            Vector3 se = mainActive.ScaleEclat;
            float sr = se.LengthSquared() > 1e-8f ? se.X : 1f;
            float sh = se.LengthSquared() > 1e-8f ? se.Z : 1f;
            float r = 0.12f * sr;
            float h = 0.6f * sh;
            var item = new ItemPhysique
            {
                ID_Objet = id,
                Name = "ItemPhysique",
                ContinuousCd = true,
                IndexBotanique = mainActive.IndexBotanique,
                IndexCacheMemoire = mainActive.IndexMorphologique
            };
            var meshNode = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h }, MaterialOverride = ArbreVivant.ObtenirMaterielBois() };
            meshNode.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
            var colNode = new CollisionShape3D { Shape = new CylinderShape3D { Radius = r, Height = h } };
            colNode.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
            item.AddChild(meshNode);
            item.AddChild(colNode);
            corps = item;
        }
        else if (id == 32) // Bâton : idem dimensions réelles
        {
            Vector3 se = mainActive.ScaleEclat;
            float sr = se.LengthSquared() > 1e-8f ? se.X : 1f;
            float sh = se.LengthSquared() > 1e-8f ? se.Z : 1f;
            float r = 0.02f * sr;
            float h = 0.5f * sh;
            var item = new ItemPhysique
            {
                ID_Objet = id,
                Name = "ItemPhysique",
                ContinuousCd = true,
                IndexBotanique = mainActive.IndexBotanique,
                IndexCacheMemoire = mainActive.IndexMorphologique
            };
            var meshNode = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h }, MaterialOverride = ArbreVivant.ObtenirMaterielBois() };
            meshNode.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
            var colNode = new CollisionShape3D { Shape = new CylinderShape3D { Radius = r, Height = h } };
            colNode.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
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
        else // 999 Buisson
        {
            var sb = new StaticBody3D();
            sb.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } });
            sb.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.8f, 0.2f) } });
            corps = sb;
        }
        corps.SetMeta("ID_Matiere", id);
        corps.AddToGroup("BlocsPoses");
        GetParent().AddChild(corps);
        corps.GlobalPosition = pointDeChute;
        // Même calque que le terrain PhysicsServer3D / StaticBody (bit 1) : collision fiable au sol.
        if (corps is RigidBody3D rbPose)
        {
            rbPose.CollisionLayer = 1;
            rbPose.CollisionMask = 1;
        }
        // Fibres / corde non élastiques : ne pas appliquer d’échelle « étirée » (herbe, liane, corde boyau+herbe, etc.)
        bool estFlexOuCorde = id == 15 || id == 16 || id == 17 || id == 20;
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
            if (!mainActive.EstVide && Input.IsActionPressed("clic_droit")) _forceLancer = Mathf.Min(1f, _forceLancer + VitesseChargeBras * dt);
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
