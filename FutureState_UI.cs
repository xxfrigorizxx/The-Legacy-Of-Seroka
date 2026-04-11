using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>UI dédiée aux Future States du joueur (touche K).</summary>
public partial class FutureState_UI : CanvasLayer
{
    private enum ModeAffichage
    {
        FutureStates,
        Metiers
    }

    public bool EstOuvert { get; private set; }

    private Joueur _joueur;
    private VBoxContainer _listeStats;
    private Label _lblMax;
    private Label _lblTitre;
    private Button _btnOngletInventaire;
    private Button _btnOngletFutureState;
    private Button _btnOngletMetiers;
    private ModeAffichage _modeCourant = ModeAffichage.FutureStates;

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
        DefinirModeFutureStates();
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

        if (_modeCourant == ModeAffichage.FutureStates)
        {
            IReadOnlyDictionary<string, ulong> stats = _joueur.ObtenirFutureStates();
            foreach (KeyValuePair<string, ulong> kv in stats.OrderBy(s => s.Key))
            {
                ulong xpCourante = _joueur.ObtenirXpFutureState(kv.Key);
                ulong xpProchain = _joueur.ObtenirXpNecessaireProchainNiveauFutureState(kv.Key);
                Label ligne = new Label
                {
                    Text = $"{kv.Key} | Niveau: {FormaterNiveau(kv.Value)} | XP: {FormaterNiveau(xpCourante)} / {FormaterNiveau(xpProchain)}",
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                ligne.AddThemeFontSizeOverride("font_size", 20);
                ligne.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
                _listeStats.AddChild(ligne);

                if (string.Equals(kv.Key, "Force", System.StringComparison.OrdinalIgnoreCase))
                {
                    float bonusDegatsPct = (kv.Value * 0.01f);
                    float chargeActuelle = _joueur.ObtenirPoidsTotalPorteKg();
                    float chargeMax = _joueur.ObtenirCapacitePoidsMaxKg();

                    var ligneForce = new Label
                    {
                        Text = $"  Bonus degats: +{bonusDegatsPct:F2}% | Charge: {chargeActuelle:F2}kg / {chargeMax:F2}kg",
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ligneForce.AddThemeFontSizeOverride("font_size", 16);
                    ligneForce.AddThemeColorOverride("font_color", new Color(0.78f, 0.9f, 0.8f));
                    _listeStats.AddChild(ligneForce);
                }
            }
        }
        else
        {
            IReadOnlyDictionary<string, ulong> metiers = _joueur.ObtenirMetiers();
            foreach (KeyValuePair<string, ulong> kv in metiers.OrderBy(s => s.Key))
            {
                ulong xpCourante = _joueur.ObtenirXpMetier(kv.Key);
                ulong xpProchain = _joueur.ObtenirXpNecessaireProchainNiveauMetier(kv.Key);
                Label ligne = new Label
                {
                    Text = $"{kv.Key} | Niveau: {FormaterNiveau(kv.Value)} | XP: {FormaterNiveau(xpCourante)} / {FormaterNiveau(xpProchain)}",
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                ligne.AddThemeFontSizeOverride("font_size", 20);
                ligne.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
                _listeStats.AddChild(ligne);

                if (string.Equals(kv.Key, "Bucheron", System.StringComparison.OrdinalIgnoreCase))
                {
                    float bonusDegatsArbre = _joueur.ObtenirBonusDegatsArbreBucheron();
                    var ligneBucheron = new Label
                    {
                        Text = $"  Bonus degats arbres: +{bonusDegatsArbre:F2}",
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ligneBucheron.AddThemeFontSizeOverride("font_size", 16);
                    ligneBucheron.AddThemeColorOverride("font_color", new Color(0.80f, 0.92f, 0.78f));
                    _listeStats.AddChild(ligneBucheron);
                }
            }
        }

        if (_lblMax != null)
            _lblMax.Text = $"Niveau max global: {FormaterNiveau(Joueur.NiveauMaxFutureState)}";
    }

    private void OuvrirInventaireDepuisOnglet()
    {
        _joueur?.OuvrirInventaireDepuisFutureState();
    }

    private void OuvrirFutureStatesDepuisOnglet()
    {
        DefinirModeFutureStates();
    }

    private void OuvrirMetiersDepuisOnglet()
    {
        DefinirModeMetiers();
    }

    public void DefinirModeFutureStates()
    {
        _modeCourant = ModeAffichage.FutureStates;
        MettreAJourStyleOnglets();
        if (_lblTitre != null)
            _lblTitre.Text = "Future States du joueur";
        if (EstOuvert)
            Rafraichir();
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

    private void MettreAJourStyleOnglets()
    {
        if (_btnOngletFutureState != null)
            _btnOngletFutureState.Disabled = _modeCourant == ModeAffichage.FutureStates;
        if (_btnOngletMetiers != null)
            _btnOngletMetiers.Disabled = _modeCourant == ModeAffichage.Metiers;
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

        var panneau = new Panel { CustomMinimumSize = new Vector2(720, 430), MouseFilter = Control.MouseFilterEnum.Stop };
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
        barre.AddThemeConstantOverride("separation", 8);
        colonne.AddChild(barre);

        _btnOngletInventaire = new Button { Text = "Inventaire", CustomMinimumSize = new Vector2(180, 36) };
        _btnOngletInventaire.Pressed += OuvrirInventaireDepuisOnglet;
        barre.AddChild(_btnOngletInventaire);

        _btnOngletFutureState = new Button { Text = "Future States", CustomMinimumSize = new Vector2(180, 36) };
        _btnOngletFutureState.Pressed += OuvrirFutureStatesDepuisOnglet;
        barre.AddChild(_btnOngletFutureState);

        _btnOngletMetiers = new Button { Text = "Metiers", CustomMinimumSize = new Vector2(180, 36) };
        _btnOngletMetiers.Pressed += OuvrirMetiersDepuisOnglet;
        barre.AddChild(_btnOngletMetiers);

        _lblTitre = new Label
        {
            Text = "Future States du joueur",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _lblTitre.AddThemeFontSizeOverride("font_size", 30);
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

        _lblMax = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _lblMax.AddThemeFontSizeOverride("font_size", 16);
        _lblMax.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.35f));
        colonne.AddChild(_lblMax);

        var sep = new HSeparator();
        colonne.AddChild(sep);

        _listeStats = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _listeStats.AddThemeConstantOverride("separation", 8);
        colonne.AddChild(_listeStats);
    }

    private static string FormaterNiveau(ulong valeur)
    {
        return valeur.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
    }
}
