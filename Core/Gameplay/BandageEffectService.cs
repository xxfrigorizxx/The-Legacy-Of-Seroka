using Godot;
using System.Collections.Generic;

/// <summary>Effets HoT / DoT des 19 variantes de bandage tier 1 (ID 135), sur une section du corps choisie.</summary>
public partial class Joueur
{
    private const float SeuilPvBandageProgressif = 0.0001f;

    private struct EffetBandageActif
    {
        public float PvCibleTotal;
        public float PvApplique;
        public float DureeInitialeSec;
        public float DureeRestanteSec;
        public float FractionPv;

        public bool EstEnCours(float seuil) => DureeRestanteSec > seuil;
    }

    private bool _selectionBandageEnCours;
    private PanelContainer _panneauSelectionBandage;
    private Label _labelSelectionBandage;
    private float _bandagePvEnAttente;
    private float _bandageDureeEnAttenteSec;
    private string _bandageNomEnAttente = "";

    private readonly Dictionary<string, EffetBandageActif> _bandagesActifsParSection = new();

    private static readonly string[] SectionsBandageOrdre =
    {
        SectionCorpsTete,
        SectionCorpsTorse,
        SectionCorpsBrasGauche,
        SectionCorpsBrasDroit,
        SectionCorpsJambeGauche,
        SectionCorpsJambeDroite
    };

    private static readonly string[] NomsLisiblesSectionsBandage =
    {
        "Tete",
        "Torse",
        "Bras gauche",
        "Bras droit",
        "Jambe gauche",
        "Jambe droite"
    };

    /// <summary>Résout l'effet à partir du slot bandage (LIGV/LIGC/LIGM + variante).</summary>
    public static bool EssayerObtenirEffetBandageTier1(in SlotInventaire bandage, out float pvTotal, out float dureeSec)
    {
        pvTotal = 0f;
        dureeSec = 0f;
        if (bandage.EstVide || bandage.ID != IdObjetBandageTier1)
            return false;

        byte variante = bandage.IndexBotanique;
        int chim = bandage.IndexChimique;
        int morph = bandage.IndexMorphologique;

        if (variante == TagVarianteLiane)
        {
            pvTotal = 10f;
            dureeSec = 300f;
            return true;
        }
        if (variante == TagVarianteHerbeSolide)
        {
            pvTotal = 10f;
            dureeSec = 300f;
            return true;
        }
        if (variante == TagVarianteIntestinSolide)
        {
            pvTotal = 15f;
            dureeSec = 120f;
            return true;
        }
        if (variante == TagVarianteIntestin)
        {
            pvTotal = 15f;
            dureeSec = 300f;
            return true;
        }
        if (variante == TagVarianteCordeIntestinMixe || EstVarianteCordeIntestinMixe(bandage))
            return EssayerObtenirEffetBandageIntestinMixte(chim, morph, out pvTotal, out dureeSec);

        if (chim == 15 && morph == 15)
        {
            pvTotal = 5f;
            dureeSec = 300f;
            return true;
        }
        if (chim == 17 && morph == 17)
        {
            pvTotal = -5f;
            dureeSec = 120f;
            return true;
        }
        if (chim == 16 && morph == 16)
        {
            pvTotal = 10f;
            dureeSec = 300f;
            return true;
        }
        if (chim == 15 && morph == 16)
        {
            pvTotal = 7f;
            dureeSec = 240f;
            return true;
        }
        if (chim == 16 && morph == 15)
        {
            pvTotal = 4f;
            dureeSec = 420f;
            return true;
        }
        if (chim == 15 && morph == 17)
        {
            pvTotal = -3f;
            dureeSec = 240f;
            return true;
        }
        if (chim == 17 && morph == 15)
        {
            pvTotal = -4f;
            dureeSec = 180f;
            return true;
        }
        if (chim == 16 && morph == 17)
        {
            pvTotal = -2f;
            dureeSec = 300f;
            return true;
        }
        if (chim == 17 && morph == 16)
        {
            pvTotal = -5f;
            dureeSec = 120f;
            return true;
        }

        return false;
    }

    private static bool EssayerObtenirEffetBandageIntestinMixte(int chim, int morph, out float pvTotal, out float dureeSec)
    {
        pvTotal = 0f;
        dureeSec = 0f;
        if (chim == IdObjetIntestinBoeufNettoye && morph == 15)
        {
            pvTotal = 7f;
            dureeSec = 240f;
            return true;
        }
        if (chim == 15 && morph == IdObjetIntestinBoeufNettoye)
        {
            pvTotal = 4f;
            dureeSec = 420f;
            return true;
        }
        if (chim == IdObjetIntestinBoeufNettoye && morph == 16)
        {
            pvTotal = 11f;
            dureeSec = 360f;
            return true;
        }
        if (chim == 16 && morph == IdObjetIntestinBoeufNettoye)
        {
            pvTotal = 6f;
            dureeSec = 660f;
            return true;
        }
        if (chim == IdObjetIntestinBoeufNettoye && morph == 17)
        {
            pvTotal = -7f;
            dureeSec = 180f;
            return true;
        }
        if (chim == 17 && morph == IdObjetIntestinBoeufNettoye)
        {
            pvTotal = -3f;
            dureeSec = 420f;
            return true;
        }
        return false;
    }

    public bool BandageTier1Actif
    {
        get
        {
            foreach (EffetBandageActif effet in _bandagesActifsParSection.Values)
            {
                if (effet.EstEnCours(SeuilPvBandageProgressif))
                    return true;
            }
            return false;
        }
    }

    public float ObtenirDureeRestanteBandageTier1(string sectionCorps)
    {
        string section = NormaliserCleSectionCorps(sectionCorps);
        if (_bandagesActifsParSection.TryGetValue(section, out EffetBandageActif effet))
            return Mathf.Max(0f, effet.DureeRestanteSec);
        return 0f;
    }

    private bool SectionBandageOccupee(string sectionCorps)
    {
        string section = NormaliserCleSectionCorps(sectionCorps);
        return _bandagesActifsParSection.TryGetValue(section, out EffetBandageActif effet)
            && effet.EstEnCours(SeuilPvBandageProgressif);
    }

    private static string ObtenirNomLisibleSectionBandage(string section)
    {
        for (int i = 0; i < SectionsBandageOrdre.Length; i++)
        {
            if (SectionsBandageOrdre[i] == section)
                return NomsLisiblesSectionsBandage[i];
        }
        return section;
    }

    private static string SectionBandageDepuisTouche(Key keycode)
    {
        int index = keycode switch
        {
            Key.Key1 or Key.Kp1 => 0,
            Key.Key2 or Key.Kp2 => 1,
            Key.Key3 or Key.Kp3 => 2,
            Key.Key4 or Key.Kp4 => 3,
            Key.Key5 or Key.Kp5 => 4,
            Key.Key6 or Key.Kp6 => 5,
            _ => -1
        };
        if (index < 0 || index >= SectionsBandageOrdre.Length)
            return "";
        return SectionsBandageOrdre[index];
    }

    private bool MainActiveContientBandageTier1()
    {
        SlotInventaire main = MainGaucheEstActive ? MainGauche : MainDroite;
        return !main.EstVide && main.ID == IdObjetBandageTier1;
    }

    private void AssurerUiSelectionBandage()
    {
        if (_panneauSelectionBandage != null && GodotObject.IsInstanceValid(_panneauSelectionBandage))
            return;

        Node parentUi = GetParent()?.GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (parentUi == null && _racineMenuAnatomieViewport != null && GodotObject.IsInstanceValid(_racineMenuAnatomieViewport))
            parentUi = _racineMenuAnatomieViewport;
        if (parentUi == null)
            return;

        _panneauSelectionBandage = new PanelContainer
        {
            Name = "PanneauSelectionBandage",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panneauSelectionBandage.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _panneauSelectionBandage.OffsetLeft = -280f;
        _panneauSelectionBandage.OffsetTop = 26f;
        _panneauSelectionBandage.OffsetRight = 280f;
        _panneauSelectionBandage.OffsetBottom = 248f;

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 5);
        _panneauSelectionBandage.AddChild(col);

        var titre = new Label
        {
            Text = "BANDAGE - CHOISIR ZONE",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titre.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(titre);

        _labelSelectionBandage = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _labelSelectionBandage.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(_labelSelectionBandage);

        parentUi.AddChild(_panneauSelectionBandage);
    }

    private void RafraichirTexteSelectionBandage()
    {
        if (_labelSelectionBandage == null || !GodotObject.IsInstanceValid(_labelSelectionBandage))
            return;

        string signe = _bandagePvEnAttente >= 0f ? "+" : "";
        var lignes = new System.Text.StringBuilder();
        lignes.Append($"{_bandageNomEnAttente}\nEffet: {signe}{_bandagePvEnAttente:0} PV en ");
        lignes.Append($"{FormaterTempsAtelleMmSs(_bandageDureeEnAttenteSec)} (progressif).\n\n");

        for (int i = 0; i < SectionsBandageOrdre.Length; i++)
        {
            string section = SectionsBandageOrdre[i];
            float pv = ObtenirPvActuelSectionCorps(section);
            float max = ObtenirPvMaxSectionCorps(section);
            lignes.Append($"{i + 1}) {NomsLisiblesSectionsBandage[i]} — PV {pv:0}/{max:0}");
            if (SectionBandageOccupee(section))
            {
                float restant = ObtenirDureeRestanteBandageTier1(section);
                lignes.Append($" — bandage actif ({FormaterTempsAtelleMmSs(restant)})");
            }
            lignes.Append('\n');
        }
        lignes.Append("Touches: [1] tete, [2] torse, [3] bras G, [4] bras D, [5] jambe G, [6] jambe D — [Echap] annuler");
        _labelSelectionBandage.Text = lignes.ToString();
    }

    private void OuvrirSelectionBandage()
    {
        _selectionBandageEnCours = true;
        AssurerUiSelectionBandage();
        RafraichirTexteSelectionBandage();
        if (_panneauSelectionBandage != null && GodotObject.IsInstanceValid(_panneauSelectionBandage))
            _panneauSelectionBandage.Visible = true;
        GD.Print("ZERO-K : Choix bandage -> [1-6] zone du corps, Echap = annuler.");
    }

    private void FermerSelectionBandage(bool consommerEvenement)
    {
        _selectionBandageEnCours = false;
        if (_panneauSelectionBandage != null && GodotObject.IsInstanceValid(_panneauSelectionBandage))
            _panneauSelectionBandage.Visible = false;
        if (consommerEvenement)
            GetViewport()?.SetInputAsHandled();
    }

    private bool GererInputSelectionBandage(InputEvent @event)
    {
        if (!_selectionBandageEnCours)
            return false;

        if (@event.IsActionPressed("ui_cancel")
            || (@event is InputEventKey keyEsc && keyEsc.Pressed && !keyEsc.Echo && keyEsc.Keycode == Key.Escape))
        {
            GD.Print("ZERO-K : Selection bandage annulee.");
            FermerSelectionBandage(consommerEvenement: true);
            return true;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            string section = SectionBandageDepuisTouche(key.Keycode);
            if (!string.IsNullOrEmpty(section))
            {
                if (EssayerAppliquerBandageSurSection(section))
                    FermerSelectionBandage(consommerEvenement: true);
                return true;
            }
        }

        return false;
    }

    private bool TraiterClicDroitBandageTier1()
    {
        if (_selectionBandageEnCours)
            return true;
        if (!MainActiveContientBandageTier1())
            return false;

        SlotInventaire main = MainGaucheEstActive ? MainGauche : MainDroite;
        if (!EssayerObtenirEffetBandageTier1(main, out float pvTotal, out float dureeSec))
        {
            GD.Print("ZERO-K : Ce bandage n'a pas d'effet reconnu.");
            return true;
        }

        _bandagePvEnAttente = pvTotal;
        _bandageDureeEnAttenteSec = dureeSec;
        _bandageNomEnAttente = Atlas_Matiere.ObtenirNomObjet(main);
        OuvrirSelectionBandage();
        return true;
    }

    private bool EssayerAppliquerBandageSurSection(string sectionCible)
    {
        if (!MainActiveContientBandageTier1())
        {
            GD.Print("ZERO-K : Plus de bandage en main active.");
            return false;
        }

        string section = NormaliserCleSectionCorps(sectionCible);
        if (SectionBandageOccupee(section))
        {
            GD.Print($"ZERO-K : Un bandage est deja actif sur {ObtenirNomLisibleSectionBandage(section)} " +
                $"({FormaterTempsAtelleMmSs(ObtenirDureeRestanteBandageTier1(section))} restantes). Choisis un autre membre.");
            return false;
        }

        float dureeInitiale = Mathf.Max(0.1f, _bandageDureeEnAttenteSec);
        _bandagesActifsParSection[section] = new EffetBandageActif
        {
            PvCibleTotal = _bandagePvEnAttente,
            PvApplique = 0f,
            DureeInitialeSec = dureeInitiale,
            DureeRestanteSec = dureeInitiale,
            FractionPv = 0f
        };

        ConsommerUneUniteMainActive();

        string signe = _bandagePvEnAttente >= 0f ? "+" : "";
        GD.Print($"ZERO-K : Bandage sur {ObtenirNomLisibleSectionBandage(section)} : {signe}{_bandagePvEnAttente:0} PV " +
            $"en {FormaterTempsAtelleMmSs(dureeInitiale)} (application progressive).");
        RafraichirHUD();
        return true;
    }

    private void MettreAJourEffetBandageTier1(float dt)
    {
        if (_selectionBandageEnCours)
            RafraichirTexteSelectionBandage();

        if (_bandagesActifsParSection.Count == 0)
            return;

        var sections = new List<string>(_bandagesActifsParSection.Keys);
        foreach (string section in sections)
        {
            if (!_bandagesActifsParSection.TryGetValue(section, out EffetBandageActif effet))
                continue;
            if (!effet.EstEnCours(SeuilPvBandageProgressif))
            {
                _bandagesActifsParSection.Remove(section);
                continue;
            }

            MettreAJourEffetBandageSection(section, ref effet, dt);
            if (effet.EstEnCours(SeuilPvBandageProgressif))
                _bandagesActifsParSection[section] = effet;
            else
                _bandagesActifsParSection.Remove(section);
        }
    }

    private void MettreAJourEffetBandageSection(string section, ref EffetBandageActif effet, float dt)
    {
        effet.DureeRestanteSec = Mathf.Max(0f, effet.DureeRestanteSec - dt);
        bool termine = effet.DureeRestanteSec <= SeuilPvBandageProgressif;
        float progression = termine
            ? 1f
            : 1f - (effet.DureeRestanteSec / effet.DureeInitialeSec);
        float pvCibleCumules = effet.PvCibleTotal * progression;
        float delta = pvCibleCumules - effet.PvApplique;
        effet.FractionPv += delta;

        while (effet.PvCibleTotal >= 0f && effet.FractionPv >= 1f - SeuilPvBandageProgressif)
        {
            int points = Mathf.Min(
                Mathf.FloorToInt(effet.FractionPv),
                Mathf.RoundToInt(ObtenirPvMaxSectionCorps(section) - ObtenirPvActuelSectionCorps(section)));
            if (points <= 0)
                break;
            AppliquerVariationPvSectionBandage(section, points);
            effet.FractionPv -= points;
            effet.PvApplique += points;
        }

        while (effet.PvCibleTotal < 0f && effet.FractionPv <= -1f + SeuilPvBandageProgressif)
        {
            int points = Mathf.Min(
                Mathf.FloorToInt(-effet.FractionPv),
                Mathf.RoundToInt(ObtenirPvActuelSectionCorps(section)));
            if (points <= 0)
                break;
            AppliquerVariationPvSectionBandage(section, -points);
            effet.FractionPv += points;
            effet.PvApplique -= points;
        }

        if (termine)
            TerminerEffetBandageSection(section, ref effet);
    }

    private void TerminerEffetBandageSection(string section, ref EffetBandageActif effet)
    {
        float reste = effet.PvCibleTotal - effet.PvApplique;
        if (Mathf.Abs(reste) >= SeuilPvBandageProgressif)
        {
            int points = reste >= 0f
                ? Mathf.CeilToInt(reste - SeuilPvBandageProgressif)
                : Mathf.FloorToInt(reste + SeuilPvBandageProgressif);
            if (points != 0)
                AppliquerVariationPvSectionBandage(section, points);
        }

        GD.Print($"ZERO-K : Bandage termine sur {ObtenirNomLisibleSectionBandage(section)}.");
        effet.DureeRestanteSec = 0f;
        RafraichirHUD();
    }

    private void AppliquerVariationPvSectionBandage(string section, int deltaPv)
    {
        if (deltaPv == 0 || string.IsNullOrEmpty(section))
            return;
        if (deltaPv > 0)
            SoignerSectionCorps(section, deltaPv);
        else
            AppliquerDegatsSectionCorps(section, -deltaPv, affecterOs: false);
    }

    private void ReinitialiserEffetBandageTier1()
    {
        _bandagesActifsParSection.Clear();
        _bandagePvEnAttente = 0f;
        _bandageDureeEnAttenteSec = 0f;
        _bandageNomEnAttente = "";
    }
}
