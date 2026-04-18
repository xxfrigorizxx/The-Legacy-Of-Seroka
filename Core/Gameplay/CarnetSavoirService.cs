using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private const string ActionOuvrirCarnetSavoir = "ouvrir_carnet_savoir";

    private Panel _slotCarnet;
    private SubViewportContainer _viewportSlotCarnet;
    private MeshInstance3D _meshPreviewCarnet;

    private CanvasLayer _layerCarnetSavoir;
    private Panel _panneauCarnetSavoir;
    private TextEdit _zoneTexteCarnetSavoir;
    private Label _labelPageCarnetSavoir;
    private bool _carnetSavoirOuvert;
    private float _cooldownMessageCarnet;
    private readonly List<string> _pagesCarnetSavoir = new List<string>();
    private int _indexPageCarnetSavoir;

    public void ResoudreSlotCarnetHud()
    {
        if (_slotCarnet != null && GodotObject.IsInstanceValid(_slotCarnet))
            return;
        _slotCarnet = GetParent()?.GetNodeOrNull<Panel>("Gestionnaire_Monde/HUD_Carnet/Slot_Carnet_Savoir");
    }

    public void InitialiserCarnetSavoirSysteme()
    {
        ResoudreSlotCarnetHud();
        ConstruireUiCarnetSavoirSiNecessaire();
        if (_pagesCarnetSavoir.Count == 0)
            InitialiserCarnetParDefautSiAucuneDonnee();
    }

    public bool CarnetSavoirOuvert() => _carnetSavoirOuvert;

    public bool EssayerBasculerCarnetDepuisInput(InputEvent @event)
    {
        if (@event == null || !@event.IsActionPressed(ActionOuvrirCarnetSavoir))
            return false;

        if (_carnetSavoirOuvert)
        {
            FermerCarnetSavoirUI();
            return true;
        }

        if (_menuFutureState != null && _menuFutureState.EstOuvert)
            return true;
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            return true;
        if (_modelisateur != null && _modelisateur.EstOuvert)
            return true;

        if (!ACarnetSavoirEquipe())
        {
            if (_cooldownMessageCarnet <= 0f)
            {
                GD.Print("ZERO-K : Place le carnet du savoir dans son slot dédié pour l'ouvrir.");
                _cooldownMessageCarnet = 1.25f;
            }
            return true;
        }

        OuvrirCarnetSavoirUI();
        return true;
    }

    public void FermerCarnetSavoirUI()
    {
        if (!_carnetSavoirOuvert)
            return;

        SynchroniserPageCouranteCarnetDepuisUi();
        _carnetSavoirOuvert = false;
        if (_panneauCarnetSavoir != null && GodotObject.IsInstanceValid(_panneauCarnetSavoir))
            _panneauCarnetSavoir.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OuvrirCarnetSavoirUI()
    {
        ConstruireUiCarnetSavoirSiNecessaire();
        if (_panneauCarnetSavoir == null || !GodotObject.IsInstanceValid(_panneauCarnetSavoir))
            return;

        if (_pagesCarnetSavoir.Count == 0)
            InitialiserPagesCarnetParDefaut();

        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, Mathf.Max(0, _pagesCarnetSavoir.Count - 1));
        RafraichirContenuUiCarnet();
        _panneauCarnetSavoir.Visible = true;
        _carnetSavoirOuvert = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void InitialiserCarnetParDefautSiAucuneDonnee()
    {
        if (_pagesCarnetSavoir.Count == 0)
            InitialiserPagesCarnetParDefaut();
        if (EquipementCarnet.EstVide)
            EquipementCarnet = CreerSlotCarnetSavoirParDefaut();
    }

    public void DefinirEtatCarnetDepuisSauvegarde(string[] pages, int indexPage)
    {
        _pagesCarnetSavoir.Clear();
        if (pages != null)
        {
            for (int i = 0; i < pages.Length; i++)
                _pagesCarnetSavoir.Add(pages[i] ?? "");
        }
        CorrigerPagesInitialesCarnetSiNecessaire();

        _indexPageCarnetSavoir = Mathf.Clamp(indexPage, 0, Mathf.Max(0, _pagesCarnetSavoir.Count - 1));
        RafraichirContenuUiCarnet();
    }

    public void ObtenirEtatCarnetPourSauvegarde(out string[] pages, out int indexPage)
    {
        SynchroniserPageCouranteCarnetDepuisUi();
        pages = _pagesCarnetSavoir.ToArray();
        indexPage = Mathf.Clamp(_indexPageCarnetSavoir, 0, Mathf.Max(0, _pagesCarnetSavoir.Count - 1));
    }

    public static SlotInventaire CreerSlotCarnetSavoirParDefaut()
    {
        return new SlotInventaire
        {
            ID = IdObjetCarnetSavoir,
            IndexMorphologique = 0,
            IndexChimique = 0,
            IndexTaille = 1,
            IndexBotanique = 0,
            Quantite = 1
        };
    }

    private void InitialiserPagesCarnetParDefaut()
    {
        _pagesCarnetSavoir.Clear();
        _pagesCarnetSavoir.Add(ObtenirContenuPageInitialeCarnet1());
        _pagesCarnetSavoir.Add(ObtenirContenuPageInitialeCarnet2());
        _indexPageCarnetSavoir = 0;
    }

    private static string ObtenirContenuPageInitialeCarnet1()
    {
        return "Carnet du savoir - Initiation\n\n" +
            "Controles de base:\n" +
            "- Z : ouvrir/fermer le carnet du savoir (si equipe dans le slot carnet)\n" +
            "- Q : ouvrir/fermer l'inventaire et l'anatomie\n" +
            "- E : interagir / recolter / ramasser\n" +
            "- Clic gauche : action principale (frapper, miner, couper)\n" +
            "- Clic droit : poser / utiliser / lancer selon l'objet\n" +
            "- TAB : changer la main active\n" +
            "- WASD : deplacement\n" +
            "- Espace : saut\n" +
            "- Shift (maintien) : course";
    }

    private static string ObtenirContenuPageInitialeCarnet2()
    {
        return "Corps et survie\n\n" +
            "Ton corps est segmente (tete, torse, bras, jambes).\n" +
            "Les degats locaux impactent les performances.\n\n" +
            "Metabolisme:\n" +
            "- Faim et energie evoluent en continu.\n" +
            "- Le sprint et les actions lourdes vident plus vite l'energie.\n" +
            "- Le poids porte influence ton confort de mouvement.\n\n" +
            "Conseil: observe souvent les UI de survie et adapte ton rythme.";
    }

    /// <summary>
    /// Migration douce: certains anciens saves du carnet ont page 1 vide.
    /// On restaure les pages d'initiation uniquement si tout le carnet est vide.
    /// </summary>
    private void CorrigerPagesInitialesCarnetSiNecessaire()
    {
        if (_pagesCarnetSavoir.Count == 0)
        {
            InitialiserPagesCarnetParDefaut();
            return;
        }

        bool toutesVides = true;
        for (int i = 0; i < _pagesCarnetSavoir.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(_pagesCarnetSavoir[i]))
            {
                toutesVides = false;
                break;
            }
        }

        if (toutesVides)
        {
            InitialiserPagesCarnetParDefaut();
            return;
        }

        // Sécurise au moins 2 pages pour conserver la navigation attendue.
        if (_pagesCarnetSavoir.Count == 1)
            _pagesCarnetSavoir.Add(string.Empty);

        // Répare les saves où les premières pages ont été initialisées vides.
        bool page1Vide = string.IsNullOrWhiteSpace(_pagesCarnetSavoir[0]);
        bool page2Vide = string.IsNullOrWhiteSpace(_pagesCarnetSavoir[1]);
        if (page1Vide && page2Vide)
        {
            _pagesCarnetSavoir[0] = ObtenirContenuPageInitialeCarnet1();
            _pagesCarnetSavoir[1] = ObtenirContenuPageInitialeCarnet2();
        }
    }

    private void ConstruireUiCarnetSavoirSiNecessaire()
    {
        if (_layerCarnetSavoir != null && GodotObject.IsInstanceValid(_layerCarnetSavoir))
            return;

        _layerCarnetSavoir = new CanvasLayer
        {
            Name = "LayerCarnetSavoir",
            Layer = 102,
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_layerCarnetSavoir);

        _panneauCarnetSavoir = new Panel
        {
            Name = "PanneauCarnetSavoir",
            Visible = false
        };
        _panneauCarnetSavoir.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panneauCarnetSavoir.CustomMinimumSize = new Vector2(920f, 620f);
        _panneauCarnetSavoir.OffsetLeft = -460f;
        _panneauCarnetSavoir.OffsetTop = -310f;
        _panneauCarnetSavoir.OffsetRight = 460f;
        _panneauCarnetSavoir.OffsetBottom = 310f;
        _layerCarnetSavoir.AddChild(_panneauCarnetSavoir);

        var marge = new MarginContainer();
        marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        marge.AddThemeConstantOverride("margin_left", 16);
        marge.AddThemeConstantOverride("margin_top", 16);
        marge.AddThemeConstantOverride("margin_right", 16);
        marge.AddThemeConstantOverride("margin_bottom", 16);
        _panneauCarnetSavoir.AddChild(marge);

        var colonne = new VBoxContainer();
        colonne.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        colonne.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        colonne.AddThemeConstantOverride("separation", 10);
        marge.AddChild(colonne);

        var entete = new HBoxContainer();
        entete.AddThemeConstantOverride("separation", 8);
        colonne.AddChild(entete);

        var titre = new Label { Text = "Carnet du savoir" };
        titre.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titre.AddThemeFontSizeOverride("font_size", 24);
        entete.AddChild(titre);

        _labelPageCarnetSavoir = new Label { Text = "Page 1/1", HorizontalAlignment = HorizontalAlignment.Right };
        _labelPageCarnetSavoir.CustomMinimumSize = new Vector2(140f, 0f);
        entete.AddChild(_labelPageCarnetSavoir);

        _zoneTexteCarnetSavoir = new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _zoneTexteCarnetSavoir.TextChanged += SynchroniserPageCouranteCarnetDepuisUi;
        colonne.AddChild(_zoneTexteCarnetSavoir);

        var barreActions = new HBoxContainer();
        barreActions.AddThemeConstantOverride("separation", 8);
        colonne.AddChild(barreActions);

        var boutonPrecedent = new Button { Text = "< Page precedente" };
        boutonPrecedent.Pressed += AllerPagePrecedenteCarnet;
        barreActions.AddChild(boutonPrecedent);

        var boutonSuivante = new Button { Text = "Page suivante >" };
        boutonSuivante.Pressed += AllerPageSuivanteCarnet;
        barreActions.AddChild(boutonSuivante);

        var boutonAjouter = new Button { Text = "+ Ajouter page" };
        boutonAjouter.Pressed += AjouterPageCarnet;
        barreActions.AddChild(boutonAjouter);

        var boutonSupprimer = new Button { Text = "- Supprimer page" };
        boutonSupprimer.Pressed += SupprimerPageCouranteCarnet;
        barreActions.AddChild(boutonSupprimer);

        var separateur = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        barreActions.AddChild(separateur);

        var boutonFermer = new Button { Text = "Fermer (Z)" };
        boutonFermer.Pressed += FermerCarnetSavoirUI;
        barreActions.AddChild(boutonFermer);
    }

    private void SynchroniserPageCouranteCarnetDepuisUi()
    {
        if (_zoneTexteCarnetSavoir == null || !GodotObject.IsInstanceValid(_zoneTexteCarnetSavoir))
            return;
        if (_pagesCarnetSavoir.Count == 0)
            _pagesCarnetSavoir.Add("");
        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, _pagesCarnetSavoir.Count - 1);
        _pagesCarnetSavoir[_indexPageCarnetSavoir] = _zoneTexteCarnetSavoir.Text ?? "";
    }

    private void AllerPagePrecedenteCarnet()
    {
        SynchroniserPageCouranteCarnetDepuisUi();
        _indexPageCarnetSavoir = Mathf.Max(0, _indexPageCarnetSavoir - 1);
        RafraichirContenuUiCarnet();
    }

    private void AllerPageSuivanteCarnet()
    {
        SynchroniserPageCouranteCarnetDepuisUi();
        _indexPageCarnetSavoir = Mathf.Min(_pagesCarnetSavoir.Count - 1, _indexPageCarnetSavoir + 1);
        RafraichirContenuUiCarnet();
    }

    private void AjouterPageCarnet()
    {
        SynchroniserPageCouranteCarnetDepuisUi();
        _pagesCarnetSavoir.Insert(_indexPageCarnetSavoir + 1, "");
        _indexPageCarnetSavoir++;
        RafraichirContenuUiCarnet();
    }

    private void SupprimerPageCouranteCarnet()
    {
        if (_pagesCarnetSavoir.Count <= 1)
        {
            _pagesCarnetSavoir[0] = "";
            _indexPageCarnetSavoir = 0;
            RafraichirContenuUiCarnet();
            return;
        }

        SynchroniserPageCouranteCarnetDepuisUi();
        _pagesCarnetSavoir.RemoveAt(_indexPageCarnetSavoir);
        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, _pagesCarnetSavoir.Count - 1);
        RafraichirContenuUiCarnet();
    }

    private void RafraichirContenuUiCarnet()
    {
        if (_zoneTexteCarnetSavoir == null || !GodotObject.IsInstanceValid(_zoneTexteCarnetSavoir))
            return;
        if (_pagesCarnetSavoir.Count == 0)
            _pagesCarnetSavoir.Add("");

        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, _pagesCarnetSavoir.Count - 1);
        _zoneTexteCarnetSavoir.Text = _pagesCarnetSavoir[_indexPageCarnetSavoir] ?? "";
        if (_labelPageCarnetSavoir != null && GodotObject.IsInstanceValid(_labelPageCarnetSavoir))
            _labelPageCarnetSavoir.Text = $"Page {_indexPageCarnetSavoir + 1}/{_pagesCarnetSavoir.Count}";
    }
}
