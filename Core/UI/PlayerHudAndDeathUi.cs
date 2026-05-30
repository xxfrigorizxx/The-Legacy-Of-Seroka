using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void VerifierMortJoueurSiNecessaire()
    {
        if (_mortJoueurEnCours)
            return;
        if (_pvTorse > 0f && _pvTete > 0f)
            return;
        _mortJoueurEnCours = true;
        Callable.From(ExecuterMortJoueurRetourCreationPersonnage).CallDeferred();
    }

    private void ExecuterMortJoueurRetourCreationPersonnage()
    {
        if (!IsInstanceValid(this))
            return;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        int dimMort = _gestionnaireMonde?.ObtenirDimensionLocaleActiveId() ?? (int)DimensionJeu.Alpha;
        GameState.Instance?.SauvegarderDernierePoseMort(dimMort, GlobalPosition);
        GameState.Instance?.PreparerMortNouveauPersonnageMemeMonde();
        AssurerUiMortRecreationPersonnage();
        OuvrirUiMortApresDeces();
        GetTree().Paused = true;
    }

    private static StyleBoxFlat CreerStylePanneauMort()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.14f, 0.96f),
            BorderColor = new Color(0.55f, 0.2f, 0.2f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2
        };
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(28);
        return style;
    }

    private void AssurerUiMortRecreationPersonnage()
    {
        if (_layerMortRecreation != null && GodotObject.IsInstanceValid(_layerMortRecreation))
            return;

        _layerMortRecreation = new CanvasLayer
        {
            Name = "LayerMortRecreation",
            Layer = 120,
            ProcessMode = ProcessModeEnum.Always
        };
        var racine = new Control { Name = "RacineMortRecreation" };
        racine.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        racine.MouseFilter = Control.MouseFilterEnum.Stop;
        _layerMortRecreation.AddChild(racine);

        var fond = new ColorRect
        {
            Color = new Color(0.02f, 0.02f, 0.06f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        fond.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        racine.AddChild(fond);

        var centre = new CenterContainer { Name = "CentreMort" };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        racine.AddChild(centre);

        _panneauMortCitation = ConstruirePanneauMortCitation();
        _panneauMortChoix = ConstruirePanneauMortChoix();
        _panneauMortCreation = ConstruirePanneauMortCreation();
        centre.AddChild(_panneauMortCitation);
        centre.AddChild(_panneauMortChoix);
        centre.AddChild(_panneauMortCreation);

        GetTree().Root.AddChild(_layerMortRecreation);
        _layerMortRecreation.Visible = false;
    }

    private Control ConstruirePanneauMortCitation()
    {
        var panneau = new PanelContainer { Name = "PanneauMortCitation" };
        panneau.AddThemeStyleboxOverride("panel", CreerStylePanneauMort());
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);

        var titre = new Label
        {
            Text = "Vous êtes mort",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titre.AddThemeFontSizeOverride("font_size", 26);

        var citation = new Label
        {
            Text = CitationMortNature,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(420, 0)
        };
        citation.AddThemeFontSizeOverride("font_size", 18);
        citation.AddThemeColorOverride("font_color", new Color(0.88f, 0.86f, 0.78f));

        var btnContinuer = new Button { Text = "Continuer" };
        btnContinuer.Pressed += AfficherEtapeMortChoix;

        vbox.AddChild(titre);
        vbox.AddChild(citation);
        vbox.AddChild(btnContinuer);
        panneau.AddChild(vbox);
        return panneau;
    }

    private Control ConstruirePanneauMortChoix()
    {
        var panneau = new PanelContainer { Name = "PanneauMortChoix", Visible = false };
        panneau.AddThemeStyleboxOverride("panel", CreerStylePanneauMort());
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);

        string nomMonde = GameState.Instance?.NomMondeActuel ?? "ce monde";
        var intro = new Label
        {
            Text = $"Le monde « {nomMonde} » subsiste.\nVotre ancien personnage n’existe plus.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(380, 0)
        };
        intro.AddThemeFontSizeOverride("font_size", 14);

        var btnReincarner = new Button { Text = "Se réincarner" };
        btnReincarner.Pressed += AfficherEtapeMortRecreation;

        var btnAbandonner = new Button { Text = "Abandonner" };
        btnAbandonner.Pressed += AbandonnerMondeApresMort;

        vbox.AddChild(intro);
        vbox.AddChild(btnReincarner);
        vbox.AddChild(btnAbandonner);
        panneau.AddChild(vbox);
        return panneau;
    }

    private Control ConstruirePanneauMortCreation()
    {
        var panneau = new PanelContainer { Name = "PanneauMortCreation", Visible = false };
        panneau.AddThemeStyleboxOverride("panel", CreerStylePanneauMort());

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);

        string nomMonde = GameState.Instance?.NomMondeActuel ?? "ce monde";
        var titre = new Label
        {
            Text = "Nouveau personnage",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titre.AddThemeFontSizeOverride("font_size", 22);
        var sousTitre = new Label
        {
            Text = $"Nommez votre réincarnation pour « {nomMonde} ».\nProgression vierge ; la carte reste la même.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(360, 0)
        };
        sousTitre.AddThemeFontSizeOverride("font_size", 13);

        _lineNomMortRecreation = new LineEdit
        {
            PlaceholderText = "Nom du personnage",
            CustomMinimumSize = new Vector2(320, 0)
        };

        var ligneRace = new HBoxContainer();
        ligneRace.AddThemeConstantOverride("separation", 8);
        var btnRacePrec = new Button { Text = "<" };
        _labelRaceMortRecreation = new Label { Text = "Humain", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
        var btnRaceSuiv = new Button { Text = ">" };
        btnRacePrec.Pressed += () => ChangerRaceMortRecreation(-1);
        btnRaceSuiv.Pressed += () => ChangerRaceMortRecreation(1);
        ligneRace.AddChild(btnRacePrec);
        ligneRace.AddChild(_labelRaceMortRecreation);
        ligneRace.AddChild(btnRaceSuiv);

        var ligneSexe = new HBoxContainer();
        ligneSexe.AddThemeConstantOverride("separation", 8);
        var btnSexePrec = new Button { Text = "<" };
        _labelSexeMortRecreation = new Label { Text = "Masculin", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
        var btnSexeSuiv = new Button { Text = ">" };
        btnSexePrec.Pressed += () => ChangerSexeMortRecreation(-1);
        btnSexeSuiv.Pressed += () => ChangerSexeMortRecreation(1);
        ligneSexe.AddChild(btnSexePrec);
        ligneSexe.AddChild(_labelSexeMortRecreation);
        ligneSexe.AddChild(btnSexeSuiv);

        _labelErreurMortRecreation = new Label { Modulate = new Color(1f, 0.45f, 0.45f), AutowrapMode = TextServer.AutowrapMode.WordSmart };

        var btnConfirmer = new Button { Text = "Entrer dans ce monde" };
        btnConfirmer.Pressed += ConfirmerRecreationPersonnageApresMort;

        var btnRetour = new Button { Text = "Retour" };
        btnRetour.Pressed += AfficherEtapeMortChoix;

        vbox.AddChild(titre);
        vbox.AddChild(sousTitre);
        vbox.AddChild(new Label { Text = "Nom" });
        vbox.AddChild(_lineNomMortRecreation);
        vbox.AddChild(new Label { Text = "Race" });
        vbox.AddChild(ligneRace);
        vbox.AddChild(new Label { Text = "Sexe" });
        vbox.AddChild(ligneSexe);
        vbox.AddChild(_labelErreurMortRecreation);
        vbox.AddChild(btnConfirmer);
        vbox.AddChild(btnRetour);
        panneau.AddChild(vbox);
        return panneau;
    }

    private void OuvrirUiMortApresDeces()
    {
        AssurerUiMortRecreationPersonnage();
        AfficherEtapeMortCitation();
        _layerMortRecreation.Visible = true;
    }

    private void AfficherEtapeMortCitation()
    {
        if (_panneauMortCitation != null) _panneauMortCitation.Visible = true;
        if (_panneauMortChoix != null) _panneauMortChoix.Visible = false;
        if (_panneauMortCreation != null) _panneauMortCreation.Visible = false;
    }

    private void AfficherEtapeMortChoix()
    {
        if (_panneauMortCitation != null) _panneauMortCitation.Visible = false;
        if (_panneauMortChoix != null) _panneauMortChoix.Visible = true;
        if (_panneauMortCreation != null) _panneauMortCreation.Visible = false;
    }

    private void AfficherEtapeMortRecreation()
    {
        _raceMortRecreation = RaceJoueur.Humain;
        _sexeMortRecreation = SexeJoueur.Masculin;
        MettreAJourAffichageRaceSexeMortRecreation();
        if (_lineNomMortRecreation != null)
            _lineNomMortRecreation.Text = "";
        if (_labelErreurMortRecreation != null)
            _labelErreurMortRecreation.Text = "";
        if (_panneauMortCitation != null) _panneauMortCitation.Visible = false;
        if (_panneauMortChoix != null) _panneauMortChoix.Visible = false;
        if (_panneauMortCreation != null) _panneauMortCreation.Visible = true;
        _lineNomMortRecreation?.GrabFocus();
    }

    private void AbandonnerMondeApresMort()
    {
        FermerUiMortRecreationPersonnage();
        _mortJoueurEnCours = false;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://menu_principal.tscn");
    }

    private void FermerUiMortRecreationPersonnage()
    {
        if (_layerMortRecreation != null && GodotObject.IsInstanceValid(_layerMortRecreation))
            _layerMortRecreation.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void ChangerRaceMortRecreation(int delta)
    {
        int v = (int)_raceMortRecreation + delta;
        while (v < 0) v += 2;
        while (v > 1) v -= 2;
        _raceMortRecreation = (RaceJoueur)v;
        MettreAJourAffichageRaceSexeMortRecreation();
    }

    private void ChangerSexeMortRecreation(int delta)
    {
        int v = (int)_sexeMortRecreation + delta;
        while (v < 0) v += 2;
        while (v > 1) v -= 2;
        _sexeMortRecreation = (SexeJoueur)v;
        MettreAJourAffichageRaceSexeMortRecreation();
    }

    private void MettreAJourAffichageRaceSexeMortRecreation()
    {
        if (_labelRaceMortRecreation != null)
            _labelRaceMortRecreation.Text = _raceMortRecreation == RaceJoueur.Orc ? "Orc" : "Humain";
        if (_labelSexeMortRecreation != null)
            _labelSexeMortRecreation.Text = _sexeMortRecreation == SexeJoueur.Feminin ? "Féminin" : "Masculin";
    }

    private void ConfirmerRecreationPersonnageApresMort()
    {
        GameState etat = GameState.Instance;
        if (etat == null)
            return;
        string nom = _lineNomMortRecreation?.Text ?? "";
        if (!etat.EssayerFinaliserRecreationPersonnageSurMondeExistant(nom, _raceMortRecreation, _sexeMortRecreation, out string erreur))
        {
            if (_labelErreurMortRecreation != null)
                _labelErreurMortRecreation.Text = erreur ?? "Création impossible.";
            return;
        }

        ReinitialiserEtatJoueurNouveauPersonnageMemeMonde();
        RedimensionnerHitboxesSiOrc();
        InitialiserModeleHumainJoueur();
        _gestionnaireMonde?.RepositionnerJoueurApresMortNouveauPersonnage();
        FermerUiMortRecreationPersonnage();
        GetTree().Paused = false;
        _mortJoueurEnCours = false;
        GD.Print($"ZERO-K : Nouveau personnage « {etat.NomPersonnageJoue} » dans le monde « {etat.NomMondeActuel} ».");
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

    private void CreerHudStatsSurvie()
    {
        if (GetParent() == null)
            return;
        CanvasLayer hudInventaire = GetParent().GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (_menuAnatomie == null && (hudInventaire == null || !GodotObject.IsInstanceValid(hudInventaire)))
            return;
        if (_hudStatsSurvie != null && GodotObject.IsInstanceValid(_hudStatsSurvie))
            return;

        _hudStatsSurvie = new MarginContainer
        {
            Name = "HUD_StatsSurvie",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hudStatsSurvie.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var boite = new VBoxContainer
        {
            Name = "Boite_StatsSurvie",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        boite.AddThemeConstantOverride("separation", 6);
        boite.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _labelFaim = new Label
        {
            Name = "LabelFaim",
            Text = "Faim 100%",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _barreFaim = new ProgressBar
        {
            Name = "BarreFaim",
            MinValue = 0,
            MaxValue = FaimMaxJoueur,
            Value = _faimJoueur,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, 18f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _barreFaim.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _barreFaim.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.86f, 0.66f, 0.22f) });

        _labelEndurance = new Label
        {
            Name = "LabelEndurance",
            Text = "Énergie 100%",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _barreEndurance = new ProgressBar
        {
            Name = "BarreEndurance",
            MinValue = 0,
            MaxValue = EnduranceMaxJoueur,
            Value = _enduranceJoueur,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, 18f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _barreEndurance.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _barreEndurance.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.2f, 0.72f, 0.94f) });

        boite.AddChild(_labelFaim);
        boite.AddChild(_barreFaim);
        boite.AddChild(_labelEndurance);
        boite.AddChild(_barreEndurance);
        _hudStatsSurvie.AddChild(boite);

        if (_menuAnatomie != null)
        {
            _menuAnatomie.AttacherHudFaimEnergieJoueur(_hudStatsSurvie);
        }
        else if (hudInventaire != null && GodotObject.IsInstanceValid(hudInventaire))
        {
            _hudStatsSurvie.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _hudStatsSurvie.OffsetLeft = 16f;
            _hudStatsSurvie.OffsetTop = 16f;
            _hudStatsSurvie.OffsetRight = 280f;
            _hudStatsSurvie.OffsetBottom = 90f;
            hudInventaire.AddChild(_hudStatsSurvie);
        }

        MettreAJourHudStatsSurvie(force: true);
    }

    private void MettreAJourHudStatsSurvie(bool force = false)
    {
        float faimClampee = Mathf.Clamp(_faimJoueur, 0f, FaimMaxJoueur);
        float maxEnduranceEffective = ObtenirEnduranceMaxEffective();
        float enduranceClampee = Mathf.Clamp(_enduranceJoueur, 0f, maxEnduranceEffective);
        int pctFaim = Mathf.RoundToInt(Mathf.Clamp((faimClampee / FaimMaxJoueur) * 100f, 0f, 100f));
        int pctEndurance = Mathf.RoundToInt(Mathf.Clamp((enduranceClampee / maxEnduranceEffective) * 100f, 0f, 100f));

        if (_barreFaim != null)
        {
            if (force || float.IsNaN(_derniereValeurBarreFaimHud) || Mathf.Abs(_derniereValeurBarreFaimHud - faimClampee) > 0.02f)
            {
                _barreFaim.Value = faimClampee;
                _derniereValeurBarreFaimHud = faimClampee;
            }
        }
        if (_barreEndurance != null)
        {
            if (force || float.IsNaN(_derniereValeurBarreEnduranceHud) || Mathf.Abs(_derniereValeurBarreEnduranceHud - enduranceClampee) > 0.02f
                || Mathf.Abs(_barreEndurance.MaxValue - maxEnduranceEffective) > 0.02f)
            {
                _barreEndurance.MaxValue = maxEnduranceEffective;
                _barreEndurance.Value = enduranceClampee;
                _derniereValeurBarreEnduranceHud = enduranceClampee;
            }
        }
        if (_labelFaim != null)
        {
            if (force || pctFaim != _dernierPourcentageFaimHud)
            {
                _labelFaim.Text = $"Faim {pctFaim}%";
                _dernierPourcentageFaimHud = pctFaim;
            }
        }
        if (_labelEndurance != null)
        {
            if (force || pctEndurance != _dernierPourcentageEnduranceHud)
            {
                _labelEndurance.Text = $"Énergie {pctEndurance}%";
                _dernierPourcentageEnduranceHud = pctEndurance;
            }
        }

        MettreAJourEffetVisionTete();
    }
}
