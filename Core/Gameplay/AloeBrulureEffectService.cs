using Godot;
using System.Collections.Generic;

/// <summary>Soin Aloe Vera (ID 149) : récupère progressivement des PV max perdus par brûlure.</summary>
public partial class Joueur
{
    private const float SeuilSoinAloeProgressif = 0.0001f;
    private const float SoinBrulureAloeTotalPvMax = 5f;
    private const float DureeSoinBrulureAloeSec = 60f;

    private struct EffetAloeBrulureActif
    {
        public float SoinCibleTotal;
        public float SoinApplique;
        public float DureeInitialeSec;
        public float DureeRestanteSec;
        public float FractionSoin;

        public bool EstEnCours(float seuil) => DureeRestanteSec > seuil;
    }

    private bool _selectionAloeBrulureEnCours;
    private PanelContainer _panneauSelectionAloeBrulure;
    private Label _labelSelectionAloeBrulure;
    private readonly Dictionary<string, EffetAloeBrulureActif> _effetsAloeBrulureParSection = new();

    private static readonly string[] SectionsAloeBrulureOrdre =
    {
        SectionCorpsTete,
        SectionCorpsTorse,
        SectionCorpsBrasGauche,
        SectionCorpsBrasDroit,
        SectionCorpsJambeGauche,
        SectionCorpsJambeDroite
    };

    private static readonly string[] NomsLisiblesSectionsAloeBrulure =
    {
        "Tete",
        "Torse",
        "Bras gauche",
        "Bras droit",
        "Jambe gauche",
        "Jambe droite"
    };

    private static string SectionAloeBrulureDepuisTouche(Key keycode)
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
        if (index < 0 || index >= SectionsAloeBrulureOrdre.Length)
            return "";
        return SectionsAloeBrulureOrdre[index];
    }

    private static string ObtenirNomLisibleSectionAloeBrulure(string section)
    {
        for (int i = 0; i < SectionsAloeBrulureOrdre.Length; i++)
        {
            if (SectionsAloeBrulureOrdre[i] == section)
                return NomsLisiblesSectionsAloeBrulure[i];
        }
        return section;
    }

    private bool MainActiveContientAloeVera()
    {
        SlotInventaire main = MainGaucheEstActive ? MainGauche : MainDroite;
        return !main.EstVide && main.ID == IdObjetAloeVera;
    }

    private bool SectionAloeBrulureOccupee(string sectionCorps)
    {
        string section = NormaliserCleSectionCorps(sectionCorps);
        return _effetsAloeBrulureParSection.TryGetValue(section, out EffetAloeBrulureActif effet)
            && effet.EstEnCours(SeuilSoinAloeProgressif);
    }

    private void AssurerUiSelectionAloeBrulure()
    {
        if (_panneauSelectionAloeBrulure != null && GodotObject.IsInstanceValid(_panneauSelectionAloeBrulure))
            return;

        Node parentUi = GetParent()?.GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (parentUi == null && _racineMenuAnatomieViewport != null && GodotObject.IsInstanceValid(_racineMenuAnatomieViewport))
            parentUi = _racineMenuAnatomieViewport;
        if (parentUi == null)
            return;

        _panneauSelectionAloeBrulure = new PanelContainer
        {
            Name = "PanneauSelectionAloeBrulure",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panneauSelectionAloeBrulure.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _panneauSelectionAloeBrulure.OffsetLeft = -300f;
        _panneauSelectionAloeBrulure.OffsetTop = 26f;
        _panneauSelectionAloeBrulure.OffsetRight = 300f;
        _panneauSelectionAloeBrulure.OffsetBottom = 252f;

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 5);
        _panneauSelectionAloeBrulure.AddChild(col);

        var titre = new Label
        {
            Text = "ALOE VERA - SOIN BRULURE",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titre.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(titre);

        _labelSelectionAloeBrulure = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _labelSelectionAloeBrulure.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(_labelSelectionAloeBrulure);

        parentUi.AddChild(_panneauSelectionAloeBrulure);
    }

    private void RafraichirTexteSelectionAloeBrulure()
    {
        if (_labelSelectionAloeBrulure == null || !GodotObject.IsInstanceValid(_labelSelectionAloeBrulure))
            return;

        var lignes = new System.Text.StringBuilder();
        lignes.Append($"Effet: +{SoinBrulureAloeTotalPvMax:0} PV max de brulure en {FormaterTempsAtelleMmSs(DureeSoinBrulureAloeSec)}.\n\n");
        for (int i = 0; i < SectionsAloeBrulureOrdre.Length; i++)
        {
            string section = SectionsAloeBrulureOrdre[i];
            float brulure = ObtenirMalusPvMaxBrulureSection(section);
            lignes.Append($"{i + 1}) {NomsLisiblesSectionsAloeBrulure[i]} — Brulure max: -{brulure:0}");
            if (SectionAloeBrulureOccupee(section))
                lignes.Append(" — aloe actif");
            lignes.Append('\n');
        }
        lignes.Append("Touches: [1-6] zone du corps — [Echap] annuler");
        _labelSelectionAloeBrulure.Text = lignes.ToString();
    }

    private void OuvrirSelectionAloeBrulure()
    {
        _selectionAloeBrulureEnCours = true;
        AssurerUiSelectionAloeBrulure();
        RafraichirTexteSelectionAloeBrulure();
        if (_panneauSelectionAloeBrulure != null && GodotObject.IsInstanceValid(_panneauSelectionAloeBrulure))
            _panneauSelectionAloeBrulure.Visible = true;
        GD.Print("ZERO-K : Aloe vera -> [1-6] zone du corps, Echap = annuler.");
    }

    private void FermerSelectionAloeBrulure(bool consommerEvenement)
    {
        _selectionAloeBrulureEnCours = false;
        if (_panneauSelectionAloeBrulure != null && GodotObject.IsInstanceValid(_panneauSelectionAloeBrulure))
            _panneauSelectionAloeBrulure.Visible = false;
        if (consommerEvenement)
            GetViewport()?.SetInputAsHandled();
    }

    private bool GererInputSelectionAloeBrulure(InputEvent @event)
    {
        if (!_selectionAloeBrulureEnCours)
            return false;

        if (@event.IsActionPressed("ui_cancel")
            || (@event is InputEventKey keyEsc && keyEsc.Pressed && !keyEsc.Echo && keyEsc.Keycode == Key.Escape))
        {
            GD.Print("ZERO-K : Selection aloe vera annulee.");
            FermerSelectionAloeBrulure(consommerEvenement: true);
            return true;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            string section = SectionAloeBrulureDepuisTouche(key.Keycode);
            if (!string.IsNullOrEmpty(section))
            {
                if (EssayerAppliquerAloeSurSection(section))
                    FermerSelectionAloeBrulure(consommerEvenement: true);
                return true;
            }
        }

        return false;
    }

    private bool TraiterClicDroitAloeVeraSoinBrulure()
    {
        if (_selectionAloeBrulureEnCours || _selectionBandageEnCours || _selectionAtelleBrasEnCours || _selectionAtelleJambeEnCours)
            return true;
        if (!MainActiveContientAloeVera())
            return false;

        OuvrirSelectionAloeBrulure();
        return true;
    }

    private bool EssayerAppliquerAloeSurSection(string sectionCible)
    {
        if (!MainActiveContientAloeVera())
        {
            GD.Print("ZERO-K : Plus d'aloe vera en main active.");
            return false;
        }

        string section = NormaliserCleSectionCorps(sectionCible);
        if (SectionAloeBrulureOccupee(section))
        {
            GD.Print($"ZERO-K : Un soin aloe est deja actif sur {ObtenirNomLisibleSectionAloeBrulure(section)}.");
            return false;
        }

        float brulureActuelle = ObtenirMalusPvMaxBrulureSection(section);
        if (brulureActuelle <= 0.001f)
        {
            GD.Print($"ZERO-K : {ObtenirNomLisibleSectionAloeBrulure(section)} n'a pas de degat de brulure a soigner.");
            return false;
        }

        _effetsAloeBrulureParSection[section] = new EffetAloeBrulureActif
        {
            SoinCibleTotal = SoinBrulureAloeTotalPvMax,
            SoinApplique = 0f,
            DureeInitialeSec = DureeSoinBrulureAloeSec,
            DureeRestanteSec = DureeSoinBrulureAloeSec,
            FractionSoin = 0f
        };
        ConsommerUneUniteMainActive();
        GD.Print($"ZERO-K : Aloe vera applique sur {ObtenirNomLisibleSectionAloeBrulure(section)} (+{SoinBrulureAloeTotalPvMax:0} brulure max / 1 min).");
        RafraichirHUD();
        return true;
    }

    private void MettreAJourEffetAloeBrulure(float dt)
    {
        if (_selectionAloeBrulureEnCours)
            RafraichirTexteSelectionAloeBrulure();

        if (_effetsAloeBrulureParSection.Count == 0)
            return;

        var sections = new List<string>(_effetsAloeBrulureParSection.Keys);
        foreach (string section in sections)
        {
            if (!_effetsAloeBrulureParSection.TryGetValue(section, out EffetAloeBrulureActif effet))
                continue;
            if (!effet.EstEnCours(SeuilSoinAloeProgressif))
            {
                _effetsAloeBrulureParSection.Remove(section);
                continue;
            }

            effet.DureeRestanteSec = Mathf.Max(0f, effet.DureeRestanteSec - dt);
            bool termine = effet.DureeRestanteSec <= SeuilSoinAloeProgressif;
            float progression = termine ? 1f : 1f - (effet.DureeRestanteSec / effet.DureeInitialeSec);
            float soinCumuleCible = effet.SoinCibleTotal * progression;
            float delta = soinCumuleCible - effet.SoinApplique;
            effet.FractionSoin += delta;

            while (effet.FractionSoin >= 1f - SeuilSoinAloeProgressif)
            {
                int pointsVoulus = Mathf.FloorToInt(effet.FractionSoin);
                int pointsPossibles = Mathf.RoundToInt(ObtenirMalusPvMaxBrulureSection(section));
                int points = Mathf.Min(pointsVoulus, pointsPossibles);
                if (points <= 0)
                    break;
                SoignerBrulureSectionCorps(section, points);
                effet.FractionSoin -= points;
                effet.SoinApplique += points;
            }

            if (termine)
            {
                int reste = Mathf.CeilToInt(effet.SoinCibleTotal - effet.SoinApplique - SeuilSoinAloeProgressif);
                if (reste > 0)
                {
                    int pointsPossibles = Mathf.RoundToInt(ObtenirMalusPvMaxBrulureSection(section));
                    int points = Mathf.Min(reste, pointsPossibles);
                    if (points > 0)
                        SoignerBrulureSectionCorps(section, points);
                }
                GD.Print($"ZERO-K : Soin aloe termine sur {ObtenirNomLisibleSectionAloeBrulure(section)}.");
                _effetsAloeBrulureParSection.Remove(section);
            }
            else
            {
                _effetsAloeBrulureParSection[section] = effet;
            }
        }
    }
}
