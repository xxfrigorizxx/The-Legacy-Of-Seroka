using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void ReinitialiserReferencePositionMetaboliste()
    {
        _positionReferenceMetaboliste = GlobalPosition;
        _positionReferenceMetabolisteInitialisee = true;
    }

    private void MettreAJourProgressionMetabolisteParDeplacement()
    {
        if (!_positionReferenceMetabolisteInitialisee)
        {
            ReinitialiserReferencePositionMetaboliste();
            return;
        }
        Vector3 positionActuelle = GlobalPosition;
        Vector3 delta = positionActuelle - _positionReferenceMetaboliste;
        _positionReferenceMetaboliste = positionActuelle;
        float distanceHorizontale = new Vector2(delta.X, delta.Z).Length();
        if (distanceHorizontale <= 0.001f)
            return;
        // Ignore les téléportations/repositionnements ponctuels.
        if (distanceHorizontale > 40f)
            return;
        _distanceCumuleeMetabolisteMetres += distanceHorizontale;
        while (_distanceCumuleeMetabolisteMetres >= DistanceParXpMetabolisteMetres)
        {
            _distanceCumuleeMetabolisteMetres -= DistanceParXpMetabolisteMetres;
            AjouterXpFutureState("Metaboliste", 1UL);
        }
    }

    private int ObtenirNombreSautsMaxAgiliter()
    {
        ulong paliers = ObtenirNiveauFutureState("Agiliter") / NiveauxParSautAdditionnelAgiliter;
        if (paliers >= (ulong)(int.MaxValue - 1))
            return int.MaxValue;
        return 1 + (int)paliers;
    }

    public bool PeutPorterSlotSupplementaire(SlotInventaire slot)
    {
        _ = slot;
        return true;
    }

    private void AppliquerMetabolismeJoueur(float dt, bool effortIntense, bool sprintActif)
    {
        float drainFaim = DrainFaimPassifParSeconde;
        if (effortIntense)
            drainFaim += DrainFaimEffortParSeconde;
        if (sprintActif)
            drainFaim += DrainFaimSprintParSeconde;
        _faimJoueur = Mathf.Max(0f, _faimJoueur - drainFaim * FacteurRalentissementDrainFaim * dt);

        float drainEndurance = 0f;
        if (effortIntense)
            drainEndurance += DrainEnduranceActionParSeconde;
        if (sprintActif)
            drainEndurance += DrainEnduranceSprintParSeconde;
        if (drainEndurance > 0f)
        {
            _enduranceJoueur = Mathf.Max(0f, _enduranceJoueur - drainEndurance * dt);
        }
        else if (_enduranceJoueur < ObtenirEnduranceMaxEffective() - 0.001f && _faimJoueur > 0.001f)
        {
            float manque = ObtenirEnduranceMaxEffective() - _enduranceJoueur;
            float regenSouhaitee = Mathf.Min(RegenEnduranceParSeconde * dt, manque);
            float regenLimiteeParFaim = _faimJoueur / Mathf.Max(0.0001f, CoutFaimParPointEndurance);
            float regen = Mathf.Min(regenSouhaitee, regenLimiteeParFaim);
            if (regen > 0f)
            {
                _enduranceJoueur = Mathf.Min(ObtenirEnduranceMaxEffective(), _enduranceJoueur + regen);
                _faimJoueur = Mathf.Max(0f, _faimJoueur - regen * CoutFaimParPointEndurance * FacteurRalentissementDrainFaim);
            }
        }

        if (_faimJoueur <= 0.001f)
        {
            int degatsFaim = Mathf.Max(1, Mathf.RoundToInt(DegatsTorseParSecondeFaimNulle * dt));
            // La faim détruit la chair/PV mais ne casse pas l'os.
            AppliquerDegatsSectionCorps(SectionCorpsTorse, degatsFaim, affecterOs: false);
        }

        AppliquerPlafondEnduranceSelonTorse();
        MettreAJourHudStatsSurvie();
    }


    private float ObtenirRatioPvTorse() => ObtenirRatioPvSectionCorps(SectionCorpsTorse);

    /// <summary>Torse ≤ 50 % PV : plafond d'énergie réduit de moitié.</summary>
    private float ObtenirEnduranceMaxEffective()
    {
        if (ObtenirRatioPvTorse() <= RatioPvSeuilFelureMembre)
            return EnduranceMaxJoueur * 0.5f;
        return EnduranceMaxJoueur;
    }

    private void AppliquerPlafondEnduranceSelonTorse()
    {
        float plafond = ObtenirEnduranceMaxEffective();
        if (_enduranceJoueur > plafond)
            _enduranceJoueur = plafond;
    }

    private float RatioEnduranceJoueur()
    {
        float max = ObtenirEnduranceMaxEffective();
        if (max <= 0.001f)
            return 0f;
        return Mathf.Clamp(_enduranceJoueur / max, 0f, 1f);
    }

    private static float ObtenirContributionVitesseJambeDepuisRatioOs(float ratioOs)
    {
        if (ratioOs <= 0.35f)
            return 0.05f; // CASSE
        if (ratioOs <= 0.70f)
            return 0.25f; // FELURE
        return 0.50f; // BON ETAT
    }

    private static float ObtenirContributionVitesseJambeDepuisRatioPv(float ratioPv)
    {
        if (ratioPv <= 0f)
            return 0.05f;
        if (ratioPv <= RatioPvSeuilFelureMembre)
            return 0.25f;
        return 0.50f;
    }

    private float ObtenirContributionVitesseJambeCombinee(string sectionJambe)
    {
        float maxOs = Mathf.Max(1f, ObtenirIntegriteOsBaseSection(sectionJambe));
        float ratioOs = sectionJambe switch
        {
            SectionCorpsJambeGauche => Mathf.Clamp(_integriteOsJambeGauche / maxOs, 0f, 1f),
            SectionCorpsJambeDroite => Mathf.Clamp(_integriteOsJambeDroite / maxOs, 0f, 1f),
            _ => 1f
        };
        float contribOs = ObtenirContributionVitesseJambeDepuisRatioOs(ratioOs);
        float contribPv = ObtenirContributionVitesseJambeDepuisRatioPv(ObtenirRatioPvSectionCorps(sectionJambe));
        return Mathf.Min(contribOs, contribPv);
    }

    private float ObtenirFacteurVitesseSelonEtatOsJambes()
    {
        float contributionG = ObtenirContributionVitesseJambeCombinee(SectionCorpsJambeGauche);
        float contributionD = ObtenirContributionVitesseJambeCombinee(SectionCorpsJambeDroite);
        return Mathf.Clamp(contributionG + contributionD, 0.10f, 1.00f);
    }

    private bool PeutSprinter()
    {
        return _enduranceJoueur > 0.01f;
    }

    private float MultiplicateurForceFrappeEndurance()
    {
        return _enduranceJoueur <= 0.01f ? 0.5f : 1f;
    }
}
