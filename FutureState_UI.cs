using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>UI dédiée a la progression du joueur (touche K).</summary>
public partial class FutureState_UI : CanvasLayer
{
    private enum ModeAffichage
    {
        Progression,
        Metiers,
        Personnage
    }

    public bool EstOuvert { get; private set; }

    private Joueur _joueur;
    private VBoxContainer _listeStats;
    private Label _lblTitre;
    private Button _btnOngletInventaire;
    private Button _btnOngletProgression;
    private Button _btnOngletMetiers;
    private Button _btnOngletPersonnage;
    private ModeAffichage _modeCourant = ModeAffichage.Progression;

    public override void _Ready()
    {
        Layer = 98;
        Visible = false;
        EstOuvert = false;
        ConstruireInterface();
    }

    public void Initialiser(Joueur joueur)
    {
        _joueur = joueur;
        DefinirModeProgression();
        Rafraichir();
    }

    public void BasculerVisibilite()
    {
        EstOuvert = !EstOuvert;
        Visible = EstOuvert;
        Input.MouseMode = EstOuvert ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (EstOuvert)
        {
            MettreAJourStyleOnglets();
            Rafraichir();
        }
    }

    public void Rafraichir()
    {
        if (_listeStats == null || _joueur == null)
            return;

        foreach (Node enfant in _listeStats.GetChildren())
            enfant.QueueFree();

        if (_modeCourant == ModeAffichage.Personnage)
        {
            Joueur.FicheStatutPersonnage fiche = _joueur.ObtenirFicheStatutPersonnage();
            float ratioPv = fiche.PointsVieMax > 0 ? fiche.PointsVieActuels / (float)fiche.PointsVieMax : 0f;

            AjouterLigneTitre($"PV: {fiche.PointsVieActuels} / {fiche.PointsVieMax} ({ratioPv * 100f:F1}%)");
            AjouterLigneTitre($"Force: {fiche.Force}");
            AjouterLigneTitre($"Constitution: {fiche.Constitution}");
            AjouterLigneTitre($"Defense: {fiche.Defense}");
            AjouterLigneTitre($"Agilite: {fiche.Agilite}");
            AjouterLigneTitre($"Intelligence: {fiche.Intelligence}");
            AjouterLigneTitre($"Metabolisme: {fiche.Metabolisme}");
            string bonusRacial = fiche.NomRace == "Orc"
                ? "Bonus racial Orc: XP Force x2, XP Constitution x2, XP Intelligence x0.5"
                : "Bonus racial Humain: aucun bonus ni malus";
            AjouterLigneSousTexte(bonusRacial);
        }
        else if (_modeCourant == ModeAffichage.Progression)
        {
            Joueur.FicheStatutPersonnage ficheProgression = _joueur.ObtenirFicheStatutPersonnage();
            IReadOnlyDictionary<string, ulong> stats = _joueur.ObtenirFutureStates();
            foreach (KeyValuePair<string, ulong> kv in stats.OrderBy(s => s.Key))
            {
                UInt128 xpCourante = _joueur.ObtenirXpFutureState(kv.Key);
                UInt128 xpProchain = _joueur.ObtenirXpNecessaireProchainNiveauFutureState(kv.Key);
                Label ligne = new Label
                {
                    Text = $"{kv.Key} | Niveau: {FormaterNiveau(kv.Value)} | XP: {FormaterNiveau(xpCourante)} / {FormaterNiveau(xpProchain)}",
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                ConfigurerLigneContenu(ligne);
                ligne.AddThemeFontSizeOverride("font_size", 20);
                ligne.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
                _listeStats.AddChild(ligne);

                if (string.Equals(kv.Key, "Force", System.StringComparison.OrdinalIgnoreCase))
                {
                    float bonusDegatsPct = (_joueur.ObtenirMultiplicateurDegatsForce() - 1f) * 100f;
                    float chargeActuelle = _joueur.ObtenirPoidsTotalPorteKg();
                    float chargeMax = _joueur.ObtenirCapacitePoidsMaxKg();

                    var ligneForce = new Label
                    {
                        Text = $"  Bonus dégâts: +{bonusDegatsPct:F2}% | Charge: {chargeActuelle:F2} kg / {chargeMax:F2} kg",
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ConfigurerLigneContenu(ligneForce);
                    ligneForce.AddThemeFontSizeOverride("font_size", 16);
                    ligneForce.AddThemeColorOverride("font_color", new Color(0.78f, 0.9f, 0.8f));
                    _listeStats.AddChild(ligneForce);
                }
                else if (string.Equals(kv.Key, "Metaboliste", System.StringComparison.OrdinalIgnoreCase))
                {
                    float bonusVitessePct = (_joueur.ObtenirMultiplicateurVitesseMetaboliste() - 1f) * 100f;
                    var ligneMetaboliste = new Label
                    {
                        Text = $"  Bonus vitesse de déplacement: +{bonusVitessePct:F3}%",
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ConfigurerLigneContenu(ligneMetaboliste);
                    ligneMetaboliste.AddThemeFontSizeOverride("font_size", 16);
                    ligneMetaboliste.AddThemeColorOverride("font_color", new Color(0.80f, 0.90f, 0.98f));
                    _listeStats.AddChild(ligneMetaboliste);
                }
                else if (string.Equals(kv.Key, "Intelligence", System.StringComparison.OrdinalIgnoreCase))
                {
                    float chancePct = _joueur.ObtenirChanceReussiteAnalyseManuelle() * 100f;
                    var ligneIntelligence = new Label
                    {
                        Text = $"  Analyseur: réussite actuelle {chancePct:F2}% (Intelligence actuelle: {ficheProgression.Intelligence} | base 50% + 0,01% par point autour de 10)",
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ConfigurerLigneContenu(ligneIntelligence);
                    ligneIntelligence.AddThemeFontSizeOverride("font_size", 16);
                    ligneIntelligence.AddThemeColorOverride("font_color", new Color(0.88f, 0.86f, 1f));
                    _listeStats.AddChild(ligneIntelligence);
                }
            }
        }
        else
        {
            IReadOnlyDictionary<string, ulong> metiers = _joueur.ObtenirMetiers();
            foreach (KeyValuePair<string, ulong> kv in metiers.OrderBy(s => s.Key))
            {
                UInt128 xpCourante = _joueur.ObtenirXpMetier(kv.Key);
                UInt128 xpProchain = _joueur.ObtenirXpNecessaireProchainNiveauMetier(kv.Key);
                Label ligne = new Label
                {
                    Text = $"{kv.Key} | Niveau: {FormaterNiveau(kv.Value)} | XP: {FormaterNiveau(xpCourante)} / {FormaterNiveau(xpProchain)}",
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                ConfigurerLigneContenu(ligne);
                ligne.AddThemeFontSizeOverride("font_size", 20);
                ligne.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
                _listeStats.AddChild(ligne);

                if (string.Equals(kv.Key, "Bucheron", System.StringComparison.OrdinalIgnoreCase))
                {
                    float bonusDegatsArbre = _joueur.ObtenirBonusDegatsArbreBucheron();
                    var ligneBucheron = new Label
                    {
                        Text = $"  Bonus dégâts arbres: +{bonusDegatsArbre:F2}",
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ConfigurerLigneContenu(ligneBucheron);
                    ligneBucheron.AddThemeFontSizeOverride("font_size", 16);
                    ligneBucheron.AddThemeColorOverride("font_color", new Color(0.80f, 0.92f, 0.78f));
                    _listeStats.AddChild(ligneBucheron);
                }
            }
        }

    }

    private void OuvrirInventaireDepuisOnglet()
    {
        _joueur?.OuvrirInventaireDepuisFutureState();
    }

    private void OuvrirProgressionDepuisOnglet()
    {
        DefinirModeProgression();
    }

    private void OuvrirMetiersDepuisOnglet()
    {
        DefinirModeMetiers();
    }

    public void DefinirModeProgression()
    {
        _modeCourant = ModeAffichage.Progression;
        MettreAJourStyleOnglets();
        if (_lblTitre != null)
            _lblTitre.Text = "Progression du joueur";
        if (EstOuvert)
            Rafraichir();
    }

    // Compatibilite avec les appels existants.
    public void DefinirModeFutureStates()
    {
        DefinirModeProgression();
    }

    public void DefinirModeMetiers()
    {
        _modeCourant = ModeAffichage.Metiers;
        MettreAJourStyleOnglets();
        if (_lblTitre != null)
            _lblTitre.Text = "Metiers du joueur";
        if (EstOuvert)
            Rafraichir();
    }

    public void DefinirModePersonnage()
    {
        _modeCourant = ModeAffichage.Personnage;
        MettreAJourStyleOnglets();
        if (_lblTitre != null)
            _lblTitre.Text = "Fiche personnage";
        if (EstOuvert)
            Rafraichir();
    }

    private void MettreAJourStyleOnglets()
    {
        if (_btnOngletProgression != null)
            _btnOngletProgression.Disabled = _modeCourant == ModeAffichage.Progression;
        if (_btnOngletMetiers != null)
            _btnOngletMetiers.Disabled = _modeCourant == ModeAffichage.Metiers;
        if (_btnOngletPersonnage != null)
            _btnOngletPersonnage.Disabled = _modeCourant == ModeAffichage.Personnage;
        if (_btnOngletInventaire != null)
            _btnOngletInventaire.Disabled = false;
    }

    private void ConstruireInterface()
    {
        var racine = new Control { Name = "FutureStateRoot", MouseFilter = Control.MouseFilterEnum.Stop };
        racine.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(racine);

        var fond = new ColorRect { Color = new Color(0f, 0f, 0f, 0.78f), MouseFilter = Control.MouseFilterEnum.Stop };
        fond.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        racine.AddChild(fond);

        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        racine.AddChild(centre);

        var panneau = new Panel { CustomMinimumSize = new Vector2(900, 500), MouseFilter = Control.MouseFilterEnum.Stop };
        centre.AddChild(panneau);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.12f, 0.98f),
            BorderColor = new Color(0.28f, 0.55f, 0.95f, 1f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        panneau.AddThemeStyleboxOverride("panel", style);

        var marge = new MarginContainer();
        marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        marge.AddThemeConstantOverride("margin_left", 22);
        marge.AddThemeConstantOverride("margin_top", 20);
        marge.AddThemeConstantOverride("margin_right", 22);
        marge.AddThemeConstantOverride("margin_bottom", 18);
        panneau.AddChild(marge);

        var colonne = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        colonne.AddThemeConstantOverride("separation", 10);
        marge.AddChild(colonne);

        var barre = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        barre.AddThemeConstantOverride("separation", 6);
        colonne.AddChild(barre);

        _btnOngletInventaire = new Button { Text = "Inventaire", CustomMinimumSize = new Vector2(150, 34) };
        _btnOngletInventaire.Pressed += OuvrirInventaireDepuisOnglet;
        barre.AddChild(_btnOngletInventaire);

        _btnOngletProgression = new Button { Text = "Progression", CustomMinimumSize = new Vector2(150, 34) };
        _btnOngletProgression.Pressed += OuvrirProgressionDepuisOnglet;
        barre.AddChild(_btnOngletProgression);

        _btnOngletMetiers = new Button { Text = "Metiers", CustomMinimumSize = new Vector2(150, 34) };
        _btnOngletMetiers.Pressed += OuvrirMetiersDepuisOnglet;
        barre.AddChild(_btnOngletMetiers);

        _btnOngletPersonnage = new Button { Text = "Personnage", CustomMinimumSize = new Vector2(150, 34) };
        _btnOngletPersonnage.Pressed += DefinirModePersonnage;
        barre.AddChild(_btnOngletPersonnage);

        _lblTitre = new Label
        {
            Text = "Progression du joueur",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _lblTitre.AddThemeFontSizeOverride("font_size", 28);
        _lblTitre.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 1f));
        colonne.AddChild(_lblTitre);

        var aide = new Label
        {
            Text = "K: ouvrir/fermer - Echap: fermer",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        aide.AddThemeFontSizeOverride("font_size", 14);
        aide.AddThemeColorOverride("font_color", new Color(0.68f, 0.75f, 0.9f));
        colonne.AddChild(aide);

        var sep = new HSeparator();
        colonne.AddChild(sep);

        var scrollStats = new ScrollContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        colonne.AddChild(scrollStats);

        _listeStats = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _listeStats.AddThemeConstantOverride("separation", 8);
        scrollStats.AddChild(_listeStats);
    }

    private static string FormaterNiveau(ulong valeur)
    {
        return valeur.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
    }

    private static string FormaterNiveau(UInt128 valeur)
    {
        return valeur.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
    }

    private void AjouterLigneTitre(string texte)
    {
        var ligne = new Label
        {
            Text = texte,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ConfigurerLigneContenu(ligne);
        ligne.AddThemeFontSizeOverride("font_size", 20);
        ligne.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
        _listeStats.AddChild(ligne);
    }

    private void AjouterLigneSousTexte(string texte)
    {
        var ligne = new Label
        {
            Text = texte,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ConfigurerLigneContenu(ligne);
        ligne.AddThemeFontSizeOverride("font_size", 16);
        ligne.AddThemeColorOverride("font_color", new Color(0.80f, 0.90f, 0.98f));
        _listeStats.AddChild(ligne);
    }

    private static void ConfigurerLigneContenu(Label ligne)
    {
        if (ligne == null)
            return;
        ligne.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ligne.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }
}
