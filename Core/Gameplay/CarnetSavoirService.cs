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
        else
            AssurerTroisPremieresPagesGuideCarnet();

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

    /// <summary>Remet le carnet au guide initial (après mort, nouveau personnage sur le même monde).</summary>
    public void ReinitialiserCarnetSavoirParDefaut()
    {
        _pagesCarnetSavoir.Clear();
        _indexPageCarnetSavoir = 0;
        EquipementCarnet = CreerSlotCarnetSavoirParDefaut();
        InitialiserPagesCarnetParDefaut();
        RafraichirContenuUiCarnet();
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
        _indexPageCarnetSavoir = 0;
        AssurerTroisPremieresPagesGuideCarnet();
    }

    /// <summary>Les 3 premières pages sont toujours le guide (avertissement + initiation + survie). Le reste sert aux notes.</summary>
    private void AssurerTroisPremieresPagesGuideCarnet()
    {
        while (_pagesCarnetSavoir.Count < 3)
            _pagesCarnetSavoir.Add("");
        _pagesCarnetSavoir[0] = ObtenirContenuPageBienvenueSquelette();
        _pagesCarnetSavoir[1] = ObtenirContenuPageInitialeCarnet1();
        _pagesCarnetSavoir[2] = ObtenirContenuPageInitialeCarnet2();
        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, Mathf.Max(0, _pagesCarnetSavoir.Count - 1));
    }

    /// <summary>Première page : voix du squelette (boîte noire), ton froid mais lisible.</summary>
    private static string ObtenirContenuPageBienvenueSquelette()
    {
        return "Bienvenue dans ce monde.\n\n" +
            "Tu n'étais pas prévu ici. Rien ne t'a été promis : ni aide, ni repère, ni garantie. " +
            "Ce que tu crois comprendre n'est peut-être qu'une ombre du réel.\n\n" +
            "Écoute ce que ton corps te dit, observe, et suppose que tout peut te nuire " +
            "tant que tu n'as pas vérifié le contraire.\n\n" +
            "— Le squelette (carnet du savoir)";
    }

    private static string ObtenirContenuPageInitialeCarnet1()
    {
        return "Carnet du savoir - Initiation\n\n" +
            "Les 3 premieres pages sont le guide du monde (tu ne peux pas les modifier).\n" +
            "Utilise « + Ajouter page » pour creer des feuilles libres : notes, schemas, decouvertes.\n\n" +
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
    /// Carnet vide : guide par defaut. Sinon : impose toujours les 3 premieres pages du guide, conserve la suite pour les notes.
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

        AssurerTroisPremieresPagesGuideCarnet();
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
        var stylePanneau = new StyleBoxFlat
        {
            BgColor = new Color(0.97f, 0.97f, 0.96f),
            BorderColor = new Color(0.72f, 0.70f, 0.66f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6
        };
        stylePanneau.SetBorderWidthAll(1);
        _panneauCarnetSavoir.AddThemeStyleboxOverride("panel", stylePanneau);
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
        titre.AddThemeColorOverride("font_color", new Color(0.08f, 0.08f, 0.09f));
        entete.AddChild(titre);

        _labelPageCarnetSavoir = new Label { Text = "Page 1/1", HorizontalAlignment = HorizontalAlignment.Right };
        _labelPageCarnetSavoir.CustomMinimumSize = new Vector2(140f, 0f);
        _labelPageCarnetSavoir.AddThemeColorOverride("font_color", new Color(0.12f, 0.12f, 0.14f));
        entete.AddChild(_labelPageCarnetSavoir);

        _zoneTexteCarnetSavoir = new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        // Rendu « cahier » : pages blanches, écriture noire (y compris pages guide en lecture seule).
        _zoneTexteCarnetSavoir.AddThemeColorOverride("background_color", Colors.White);
        _zoneTexteCarnetSavoir.AddThemeColorOverride("font_color", new Color(0.06f, 0.06f, 0.07f));
        _zoneTexteCarnetSavoir.AddThemeColorOverride("font_readonly_color", new Color(0.1f, 0.1f, 0.11f));
        _zoneTexteCarnetSavoir.AddThemeColorOverride("caret_color", Colors.Black);
        _zoneTexteCarnetSavoir.AddThemeColorOverride("selection_color", new Color(0.55f, 0.78f, 1f, 0.45f));
        _zoneTexteCarnetSavoir.AddThemeConstantOverride("line_spacing", 4);
        var styleBordTexte = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderColor = new Color(0.82f, 0.80f, 0.76f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4
        };
        styleBordTexte.SetBorderWidthAll(1);
        _zoneTexteCarnetSavoir.AddThemeStyleboxOverride("normal", styleBordTexte);
        _zoneTexteCarnetSavoir.AddThemeStyleboxOverride("read_only", styleBordTexte);
        _zoneTexteCarnetSavoir.TextChanged += OnTexteCarnetSavoirModifieParJoueur;
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

    private void OnTexteCarnetSavoirModifieParJoueur()
    {
        SynchroniserPageCouranteCarnetDepuisUi();
    }

    private void SynchroniserPageCouranteCarnetDepuisUi()
    {
        if (_zoneTexteCarnetSavoir == null || !GodotObject.IsInstanceValid(_zoneTexteCarnetSavoir))
            return;
        if (_pagesCarnetSavoir.Count == 0)
            _pagesCarnetSavoir.Add("");
        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, _pagesCarnetSavoir.Count - 1);
        // Pages 1 a 3 : texte du guide impose (non editable) — pas de sync depuis le TextEdit.
        if (_indexPageCarnetSavoir < 3)
            return;
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
        AssurerTroisPremieresPagesGuideCarnet();
        // Ne jamais inserer entre les 3 pages du guide : les notes viennent apres.
        int insertion = Mathf.Max(_indexPageCarnetSavoir + 1, 3);
        insertion = Mathf.Min(insertion, _pagesCarnetSavoir.Count);
        _pagesCarnetSavoir.Insert(insertion, "");
        _indexPageCarnetSavoir = insertion;
        RafraichirContenuUiCarnet();
    }

    private void SupprimerPageCouranteCarnet()
    {
        if (_indexPageCarnetSavoir < 3)
            return;
        if (_pagesCarnetSavoir.Count <= 3)
            return;

        SynchroniserPageCouranteCarnetDepuisUi();
        _pagesCarnetSavoir.RemoveAt(_indexPageCarnetSavoir);
        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, _pagesCarnetSavoir.Count - 1);
        AssurerTroisPremieresPagesGuideCarnet();
        RafraichirContenuUiCarnet();
    }

    private void RafraichirContenuUiCarnet()
    {
        if (_zoneTexteCarnetSavoir == null || !GodotObject.IsInstanceValid(_zoneTexteCarnetSavoir))
            return;
        if (_pagesCarnetSavoir.Count == 0)
            _pagesCarnetSavoir.Add("");

        _indexPageCarnetSavoir = Mathf.Clamp(_indexPageCarnetSavoir, 0, _pagesCarnetSavoir.Count - 1);
        // Éviter TextChanged pendant l'assignation programmatique (sinon page 0 pouvait être écrasée vide).
        _zoneTexteCarnetSavoir.TextChanged -= OnTexteCarnetSavoirModifieParJoueur;
        _zoneTexteCarnetSavoir.Text = _pagesCarnetSavoir[_indexPageCarnetSavoir] ?? "";
        _zoneTexteCarnetSavoir.Editable = _indexPageCarnetSavoir >= 3;
        _zoneTexteCarnetSavoir.TextChanged += OnTexteCarnetSavoirModifieParJoueur;
        if (_labelPageCarnetSavoir != null && GodotObject.IsInstanceValid(_labelPageCarnetSavoir))
            _labelPageCarnetSavoir.Text = $"Page {_indexPageCarnetSavoir + 1}/{_pagesCarnetSavoir.Count}";
    }
}
