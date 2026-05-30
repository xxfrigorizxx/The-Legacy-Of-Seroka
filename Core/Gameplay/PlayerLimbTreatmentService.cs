using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void RafraichirTexteSelectionAtelleBras()
    {
        if (_labelSelectionAtelleBras == null || !GodotObject.IsInstanceValid(_labelSelectionAtelleBras))
            return;

        EtatOsSimple etatG = EvaluerEtatOsSectionCorps(SectionCorpsBrasGauche);
        EtatOsSimple etatD = EvaluerEtatOsSectionCorps(SectionCorpsBrasDroit);
        string timerG = _timerAtelleBrasGaucheRestant > 0f
            ? $"Atelle active: {FormaterTempsAtelleMmSs(_timerAtelleBrasGaucheRestant)}"
            : "Atelle: disponible";
        string timerD = _timerAtelleBrasDroitRestant > 0f
            ? $"Atelle active: {FormaterTempsAtelleMmSs(_timerAtelleBrasDroitRestant)}"
            : "Atelle: disponible";

        _labelSelectionAtelleBras.Text =
            $"1) Bras gauche - Etat os: {NomEtatOsSimple(etatG)} - {timerG}\n" +
            $"2) Bras droit - Etat os: {NomEtatOsSimple(etatD)} - {timerD}\n" +
            "Touches: [1] gauche, [2] droite, [Echap] annuler";
    }

    private void ReparerUnStadeBrasDepuisAtelle(string sectionBras)
    {
        EtatOsSimple etat = EvaluerEtatOsSectionCorps(sectionBras);
        if (etat == EtatOsSimple.Casse)
            DefinirEtatOsSectionCorps(sectionBras, EtatOsSimple.Felure);
        else if (etat == EtatOsSimple.Felure)
            DefinirEtatOsSectionCorps(sectionBras, EtatOsSimple.BonEtat);
    }

    private void MettreAJourTimersAtellesBras(float dt)
    {
        if (_timerAtelleBrasGaucheRestant > 0f)
        {
            _timerAtelleBrasGaucheRestant = Mathf.Max(0f, _timerAtelleBrasGaucheRestant - dt);
            if (_timerAtelleBrasGaucheRestant <= 0f)
            {
                ReparerUnStadeBrasDepuisAtelle(SectionCorpsBrasGauche);
                GD.Print("ZERO-K : Atelle terminee sur bras gauche (+1 stade de reparation).");
                RafraichirHUD();
            }
        }
        if (_timerAtelleBrasDroitRestant > 0f)
        {
            _timerAtelleBrasDroitRestant = Mathf.Max(0f, _timerAtelleBrasDroitRestant - dt);
            if (_timerAtelleBrasDroitRestant <= 0f)
            {
                ReparerUnStadeBrasDepuisAtelle(SectionCorpsBrasDroit);
                GD.Print("ZERO-K : Atelle terminee sur bras droit (+1 stade de reparation).");
                RafraichirHUD();
            }
        }

        if (_selectionAtelleBrasEnCours)
            RafraichirTexteSelectionAtelleBras();
    }

    private bool EssayerAppliquerAtelleSurBrasChoisi(string sectionBras)
    {
        if (!MainActiveContientAtelleBras())
        {
            GD.Print("ZERO-K : Plus d'atelle en main active.");
            return false;
        }

        if (ObtenirTimerAtelleBras(sectionBras) > 0f)
        {
            GD.Print("ZERO-K : Ce bras a deja une atelle active. Attends la fin du timer.");
            return false;
        }

        EtatOsSimple etat = EvaluerEtatOsSectionCorps(sectionBras);
        if (etat == EtatOsSimple.BonEtat)
        {
            GD.Print("ZERO-K : Ce bras est deja en bon etat.");
            return false;
        }

        DefinirTimerAtelleBras(sectionBras, DureeAtelleBrasSec);
        ConsommerUneUniteMainActive();
        RafraichirHUD();
        string nomBras = sectionBras == SectionCorpsBrasGauche ? "gauche" : "droit";
        GD.Print($"ZERO-K : Atelle appliquee sur bras {nomBras} (reparation dans 3 min).");
        return true;
    }

    private void OuvrirSelectionAtelleBras()
    {
        _selectionAtelleBrasEnCours = true;
        AssurerUiSelectionAtelleBras();
        RafraichirTexteSelectionAtelleBras();
        if (_panneauSelectionAtelleBras != null && GodotObject.IsInstanceValid(_panneauSelectionAtelleBras))
            _panneauSelectionAtelleBras.Visible = true;
        GD.Print("ZERO-K : Choix atelle -> touche 1 = bras gauche, touche 2 = bras droit, Echap = annuler.");
    }

    private void FermerSelectionAtelleBras(bool consommerEvenement)
    {
        _selectionAtelleBrasEnCours = false;
        if (_panneauSelectionAtelleBras != null && GodotObject.IsInstanceValid(_panneauSelectionAtelleBras))
            _panneauSelectionAtelleBras.Visible = false;
        if (consommerEvenement)
            GetViewport()?.SetInputAsHandled();
    }

    private bool GererInputSelectionAtelleBras(InputEvent @event)
    {
        if (!_selectionAtelleBrasEnCours)
            return false;

        if (@event.IsActionPressed("ui_cancel")
            || (@event is InputEventKey keyEsc && keyEsc.Pressed && !keyEsc.Echo && keyEsc.Keycode == Key.Escape))
        {
            GD.Print("ZERO-K : Selection atelle annulee.");
            FermerSelectionAtelleBras(consommerEvenement: true);
            return true;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            bool choixGauche = key.Keycode == Key.Key1 || key.Keycode == Key.Kp1;
            bool choixDroite = key.Keycode == Key.Key2 || key.Keycode == Key.Kp2;
            if (choixGauche || choixDroite)
            {
                string section = choixGauche ? SectionCorpsBrasGauche : SectionCorpsBrasDroit;
                _ = EssayerAppliquerAtelleSurBrasChoisi(section);
                FermerSelectionAtelleBras(consommerEvenement: true);
                return true;
            }
        }

        return false;
    }

    private bool TraiterClicDroitAtelleBras()
    {
        if (_selectionAtelleBrasEnCours || _selectionBandageEnCours)
            return true;
        if (!MainActiveContientAtelleBras())
            return false;

        OuvrirSelectionAtelleBras();
        return true;
    }

    private float ObtenirTimerAtelleJambe(string sectionJambe)
        => sectionJambe == SectionCorpsJambeGauche ? _timerAtelleJambeGaucheRestant : _timerAtelleJambeDroiteRestant;

    private void DefinirTimerAtelleJambe(string sectionJambe, float valeur)
    {
        float v = Mathf.Max(0f, valeur);
        if (sectionJambe == SectionCorpsJambeGauche)
            _timerAtelleJambeGaucheRestant = v;
        else
            _timerAtelleJambeDroiteRestant = v;
    }

    private bool MainActiveContientAtelleJambe()
    {
        SlotInventaire main = MainGaucheEstActive ? MainGauche : MainDroite;
        return !main.EstVide && main.ID == IdObjetAtelleJambe;
    }

    private float ObtenirTimerAtelleBras(string sectionBras)
        => sectionBras == SectionCorpsBrasGauche ? _timerAtelleBrasGaucheRestant : _timerAtelleBrasDroitRestant;

    private void DefinirTimerAtelleBras(string sectionBras, float valeur)
    {
        float v = Mathf.Max(0f, valeur);
        if (sectionBras == SectionCorpsBrasGauche)
            _timerAtelleBrasGaucheRestant = v;
        else
            _timerAtelleBrasDroitRestant = v;
    }

    private bool MainActiveContientAtelleBras()
    {
        SlotInventaire main = MainGaucheEstActive ? MainGauche : MainDroite;
        return !main.EstVide && main.ID == IdObjetAtelleBras;
    }

    private static string FormaterTempsAtelleMmSs(float tempsSec)
    {
        int sec = Mathf.Max(0, Mathf.CeilToInt(tempsSec));
        int mm = sec / 60;
        int ss = sec % 60;
        return $"{mm:00}:{ss:00}";
    }

    private static string NomEtatOsSimple(EtatOsSimple etat)
    {
        return etat switch
        {
            EtatOsSimple.Casse => "CASSE",
            EtatOsSimple.Felure => "FELURE",
            _ => "BON ETAT"
        };
    }

    private void AssurerUiSelectionAtelleJambe()
    {
        if (_panneauSelectionAtelleJambe != null && GodotObject.IsInstanceValid(_panneauSelectionAtelleJambe))
            return;

        Node parentUi = GetParent()?.GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (parentUi == null && _racineMenuAnatomieViewport != null && GodotObject.IsInstanceValid(_racineMenuAnatomieViewport))
            parentUi = _racineMenuAnatomieViewport;
        if (parentUi == null)
            return;

        _panneauSelectionAtelleJambe = new PanelContainer
        {
            Name = "PanneauSelectionAtelleJambe",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panneauSelectionAtelleJambe.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _panneauSelectionAtelleJambe.OffsetLeft = -260f;
        _panneauSelectionAtelleJambe.OffsetTop = 26f;
        _panneauSelectionAtelleJambe.OffsetRight = 260f;
        _panneauSelectionAtelleJambe.OffsetBottom = 164f;

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 5);
        _panneauSelectionAtelleJambe.AddChild(col);

        var titre = new Label
        {
            Text = "ATELLE JAMBE - CHOISIR CIBLE",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titre.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(titre);

        _labelSelectionAtelleJambe = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _labelSelectionAtelleJambe.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(_labelSelectionAtelleJambe);

        parentUi.AddChild(_panneauSelectionAtelleJambe);
    }

    private void RafraichirTexteSelectionAtelleJambe()
    {
        if (_labelSelectionAtelleJambe == null || !GodotObject.IsInstanceValid(_labelSelectionAtelleJambe))
            return;

        EtatOsSimple etatG = EvaluerEtatOsSectionCorps(SectionCorpsJambeGauche);
        EtatOsSimple etatD = EvaluerEtatOsSectionCorps(SectionCorpsJambeDroite);
        string timerG = _timerAtelleJambeGaucheRestant > 0f
            ? $"Atelle active: {FormaterTempsAtelleMmSs(_timerAtelleJambeGaucheRestant)}"
            : "Atelle: disponible";
        string timerD = _timerAtelleJambeDroiteRestant > 0f
            ? $"Atelle active: {FormaterTempsAtelleMmSs(_timerAtelleJambeDroiteRestant)}"
            : "Atelle: disponible";

        _labelSelectionAtelleJambe.Text =
            $"1) Jambe gauche - Etat os: {NomEtatOsSimple(etatG)} - {timerG}\n" +
            $"2) Jambe droite - Etat os: {NomEtatOsSimple(etatD)} - {timerD}\n" +
            "Touches: [1] gauche, [2] droite, [Echap] annuler";
    }

    private void ReparerUnStadeJambeDepuisAtelle(string sectionJambe)
    {
        EtatOsSimple etat = EvaluerEtatOsSectionCorps(sectionJambe);
        if (etat == EtatOsSimple.Casse)
            DefinirEtatOsSectionCorps(sectionJambe, EtatOsSimple.Felure);
        else if (etat == EtatOsSimple.Felure)
            DefinirEtatOsSectionCorps(sectionJambe, EtatOsSimple.BonEtat);
    }

    private void MettreAJourTimersAtellesJambes(float dt)
    {
        if (_timerAtelleJambeGaucheRestant > 0f)
        {
            _timerAtelleJambeGaucheRestant = Mathf.Max(0f, _timerAtelleJambeGaucheRestant - dt);
            if (_timerAtelleJambeGaucheRestant <= 0f)
            {
                ReparerUnStadeJambeDepuisAtelle(SectionCorpsJambeGauche);
                GD.Print("ZERO-K : Atelle terminee sur jambe gauche (+1 stade de reparation).");
                RafraichirHUD();
            }
        }
        if (_timerAtelleJambeDroiteRestant > 0f)
        {
            _timerAtelleJambeDroiteRestant = Mathf.Max(0f, _timerAtelleJambeDroiteRestant - dt);
            if (_timerAtelleJambeDroiteRestant <= 0f)
            {
                ReparerUnStadeJambeDepuisAtelle(SectionCorpsJambeDroite);
                GD.Print("ZERO-K : Atelle terminee sur jambe droite (+1 stade de reparation).");
                RafraichirHUD();
            }
        }

        if (_selectionAtelleJambeEnCours)
            RafraichirTexteSelectionAtelleJambe();
    }

    private bool EssayerAppliquerAtelleSurJambeChoisie(string sectionJambe)
    {
        if (!MainActiveContientAtelleJambe())
        {
            GD.Print("ZERO-K : Plus d'atelle en main active.");
            return false;
        }

        if (ObtenirTimerAtelleJambe(sectionJambe) > 0f)
        {
            GD.Print("ZERO-K : Cette jambe a deja une atelle active. Attends la fin du timer.");
            return false;
        }

        EtatOsSimple etat = EvaluerEtatOsSectionCorps(sectionJambe);
        if (etat == EtatOsSimple.BonEtat)
        {
            GD.Print("ZERO-K : Cette jambe est deja en bon etat.");
            return false;
        }

        DefinirTimerAtelleJambe(sectionJambe, DureeAtelleJambeSec);
        ConsommerUneUniteMainActive();
        RafraichirHUD();
        string nomJambe = sectionJambe == SectionCorpsJambeGauche ? "gauche" : "droite";
        GD.Print($"ZERO-K : Atelle appliquee sur jambe {nomJambe} (reparation dans 3 min).");
        return true;
    }

    private void OuvrirSelectionAtelleJambe()
    {
        _selectionAtelleJambeEnCours = true;
        AssurerUiSelectionAtelleJambe();
        RafraichirTexteSelectionAtelleJambe();
        if (_panneauSelectionAtelleJambe != null && GodotObject.IsInstanceValid(_panneauSelectionAtelleJambe))
            _panneauSelectionAtelleJambe.Visible = true;
        GD.Print("ZERO-K : Choix atelle -> touche 1 = jambe gauche, touche 2 = jambe droite, Echap = annuler.");
    }

    private void FermerSelectionAtelleJambe(bool consommerEvenement)
    {
        _selectionAtelleJambeEnCours = false;
        if (_panneauSelectionAtelleJambe != null && GodotObject.IsInstanceValid(_panneauSelectionAtelleJambe))
            _panneauSelectionAtelleJambe.Visible = false;
        if (consommerEvenement)
            GetViewport()?.SetInputAsHandled();
    }

    private bool GererInputSelectionAtelleJambe(InputEvent @event)
    {
        if (!_selectionAtelleJambeEnCours)
            return false;

        if (@event.IsActionPressed("ui_cancel")
            || (@event is InputEventKey keyEsc && keyEsc.Pressed && !keyEsc.Echo && keyEsc.Keycode == Key.Escape))
        {
            GD.Print("ZERO-K : Selection atelle annulee.");
            FermerSelectionAtelleJambe(consommerEvenement: true);
            return true;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            bool choixGauche = key.Keycode == Key.Key1 || key.Keycode == Key.Kp1;
            bool choixDroite = key.Keycode == Key.Key2 || key.Keycode == Key.Kp2;
            if (choixGauche || choixDroite)
            {
                string section = choixGauche ? SectionCorpsJambeGauche : SectionCorpsJambeDroite;
                _ = EssayerAppliquerAtelleSurJambeChoisie(section);
                FermerSelectionAtelleJambe(consommerEvenement: true);
                return true;
            }
        }

        return false;
    }

    private bool TraiterClicDroitAtelleJambe()
    {
        if (_selectionAtelleJambeEnCours || _selectionBandageEnCours)
            return true;
        if (!MainActiveContientAtelleJambe())
            return false;

        OuvrirSelectionAtelleJambe();
        return true;
    }

    private void AssurerUiSelectionAtelleBras()
    {
        if (_panneauSelectionAtelleBras != null && GodotObject.IsInstanceValid(_panneauSelectionAtelleBras))
            return;

        Node parentUi = GetParent()?.GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (parentUi == null && _racineMenuAnatomieViewport != null && GodotObject.IsInstanceValid(_racineMenuAnatomieViewport))
            parentUi = _racineMenuAnatomieViewport;
        if (parentUi == null)
            return;

        _panneauSelectionAtelleBras = new PanelContainer
        {
            Name = "PanneauSelectionAtelleBras",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panneauSelectionAtelleBras.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _panneauSelectionAtelleBras.OffsetLeft = -260f;
        _panneauSelectionAtelleBras.OffsetTop = 26f;
        _panneauSelectionAtelleBras.OffsetRight = 260f;
        _panneauSelectionAtelleBras.OffsetBottom = 164f;

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 5);
        _panneauSelectionAtelleBras.AddChild(col);

        var titre = new Label
        {
            Text = "ATELLE BRAS - CHOISIR CIBLE",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titre.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(titre);

        _labelSelectionAtelleBras = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _labelSelectionAtelleBras.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(_labelSelectionAtelleBras);

        parentUi.AddChild(_panneauSelectionAtelleBras);
    }
}
