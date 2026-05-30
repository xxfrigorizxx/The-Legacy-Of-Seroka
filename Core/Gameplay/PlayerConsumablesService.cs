using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void ActiverPoisonBaieRose()
    {
        _sectionPoisonBaieRose = ObtenirSectionCorpsAleatoire();
        _dureePoisonBaieRoseRestanteSec = DureePoisonBaieRoseSec;
        _accumulateurDegatsPoisonBaieRose = 0f;
        _degatsPoisonBaieRoseRestants = DegatsTotalPoisonBaieRose * Mathf.Max(MultiplicateurPoisonMin, _multiplicateurPoisonBaieRose);
    }

    private void AffaiblirPoisonBaieRose()
    {
        _multiplicateurPoisonBaieRose = Mathf.Max(MultiplicateurPoisonMin, _multiplicateurPoisonBaieRose * 0.5f);
        if (_dureePoisonBaieRoseRestanteSec > 0.0001f && _degatsPoisonBaieRoseRestants > 0f)
            _degatsPoisonBaieRoseRestants *= 0.5f;
    }

    private void MettreAJourEffetsConsommationBaies(float dt)
    {
        _timerBuffVitesseBaieNoireRestant = Mathf.Max(0f, _timerBuffVitesseBaieNoireRestant - dt);
        _timerBuffSautBaieOrangeRestant = Mathf.Max(0f, _timerBuffSautBaieOrangeRestant - dt);
        _timerBuffReductionDegatsBaieBleueRestant = Mathf.Max(0f, _timerBuffReductionDegatsBaieBleueRestant - dt);

        if (_dureePoisonBaieRoseRestanteSec <= 0.0001f || _degatsPoisonBaieRoseRestants <= 0.0001f)
        {
            _dureePoisonBaieRoseRestanteSec = 0f;
            _degatsPoisonBaieRoseRestants = 0f;
            _accumulateurDegatsPoisonBaieRose = 0f;
            return;
        }

        float pas = Mathf.Min(dt, _dureePoisonBaieRoseRestanteSec);
        if (pas <= 0f)
            return;

        float ratioParSeconde = _degatsPoisonBaieRoseRestants / Mathf.Max(0.0001f, _dureePoisonBaieRoseRestanteSec);
        float degatsPas = Mathf.Min(_degatsPoisonBaieRoseRestants, ratioParSeconde * pas);
        _dureePoisonBaieRoseRestanteSec = Mathf.Max(0f, _dureePoisonBaieRoseRestanteSec - pas);
        _degatsPoisonBaieRoseRestants = Mathf.Max(0f, _degatsPoisonBaieRoseRestants - degatsPas);
        _accumulateurDegatsPoisonBaieRose += degatsPas;

        int degatsEntiers = Mathf.FloorToInt(_accumulateurDegatsPoisonBaieRose);
        if (degatsEntiers > 0)
        {
            _accumulateurDegatsPoisonBaieRose -= degatsEntiers;
            AppliquerDegatsSectionCorps(_sectionPoisonBaieRose, degatsEntiers, affecterOs: false);
        }

        if (_dureePoisonBaieRoseRestanteSec <= 0.0001f && _degatsPoisonBaieRoseRestants > 0.0001f)
        {
            int degatsRestants = Mathf.CeilToInt(_degatsPoisonBaieRoseRestants + _accumulateurDegatsPoisonBaieRose);
            _degatsPoisonBaieRoseRestants = 0f;
            _accumulateurDegatsPoisonBaieRose = 0f;
            if (degatsRestants > 0)
                AppliquerDegatsSectionCorps(_sectionPoisonBaieRose, degatsRestants, affecterOs: false);
        }
    }

    private float ObtenirMultiplicateurVitesseConsommationBaies()
        => _timerBuffVitesseBaieNoireRestant > 0f ? MultiplicateurVitesseBaieNoire : 1f;

    private float ObtenirMultiplicateurSautConsommationBaies()
        => _timerBuffSautBaieOrangeRestant > 0f ? MultiplicateurSautBaieOrange : 1f;

    private float ObtenirMultiplicateurDegatsConsommationBaies()
        => _timerBuffReductionDegatsBaieBleueRestant > 0f ? MultiplicateurDegatsBaieBleue : 1f;

    private void ReinitialiserEffetsConsommationBaies()
    {
        _timerBuffVitesseBaieNoireRestant = 0f;
        _timerBuffSautBaieOrangeRestant = 0f;
        _timerBuffReductionDegatsBaieBleueRestant = 0f;
        _sectionPoisonBaieRose = SectionCorpsTorse;
        _degatsPoisonBaieRoseRestants = 0f;
        _dureePoisonBaieRoseRestanteSec = 0f;
        _accumulateurDegatsPoisonBaieRose = 0f;
        _multiplicateurPoisonBaieRose = 1f;
    }

    /// <summary>Effets d’une baie mangée selon sa couleur (index chimique).</summary>
    public void AppliquerEffetsConsommationBaie(int indexCouleurBaie)
    {
        int idx = ClampIndexCouleurBaie(indexCouleurBaie);
        switch (idx)
        {
            case 0: // rouge
                AppliquerDegatsSectionCorps(ObtenirSectionCorpsAleatoire(), 5, affecterOs: false);
                AppliquerVariationFaim(+2f);
                break;
            case 1: // violette (mauve)
                AppliquerVariationFaim(-10f);
                AffaiblirPoisonBaieRose();
                break;
            case 2: // orange
                AppliquerVariationFaim(-5f);
                _timerBuffSautBaieOrangeRestant = Mathf.Max(_timerBuffSautBaieOrangeRestant, DureeBuffSautBaieOrangeSec);
                break;
            case 3: // bleue
                AppliquerVariationFaim(+3f);
                _timerBuffReductionDegatsBaieBleueRestant = Mathf.Max(_timerBuffReductionDegatsBaieBleueRestant, DureeBuffReductionDegatsBaieBleueSec);
                break;
            case 4: // jaune
                AppliquerVariationFaim(+1f);
                break;
            case 5: // verte
                AppliquerVariationFaim(+3f);
                break;
            case 6: // noire
                AppliquerVariationFaim(+2f);
                _timerBuffVitesseBaieNoireRestant = Mathf.Max(_timerBuffVitesseBaieNoireRestant, DureeBuffVitesseBaieNoireSec);
                break;
            case 7: // rose
                ActiverPoisonBaieRose();
                break;
            case 8: // cyan fluorescente
                SoignerSectionCorps(ObtenirSectionCorpsPlusEndommagee(), 5);
                AppliquerVariationFaim(+5f);
                break;
        }
        MettreAJourHudStatsSurvie();
    }

    /// <summary>Effets d'un steak mangé : cru +5 faim, cuit +50 faim.</summary>
    public void AppliquerEffetsConsommationSteak(bool cuit)
    {
        float gain = cuit ? GainFaimConsommationSteakCuit : GainFaimConsommationSteakCru;
        _faimJoueur = Mathf.Min(FaimMaxJoueur, _faimJoueur + gain);
        MettreAJourHudStatsSurvie();
    }
}
