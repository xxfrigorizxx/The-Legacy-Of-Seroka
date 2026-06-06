using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private static float ObtenirMasseUnitaireRocheInventaireKg(int indexTaille) => Mathf.Clamp(indexTaille, 0, 4) switch
    {
        0 => 0.45f,
        1 => 1.10f,
        2 => 2.20f,
        3 => 4.60f,
        _ => 9.20f
    };

    private static float ObtenirMasseUnitaireSimpleParIdKg(int idObjet) => idObjet switch
    {
        15 => 0.08f,
        16 => 0.08f,
        17 => 0.08f,
        20 => 0.08f,
        21 => 0.10f,
        IdObjetCeinturePoches => 0.14f,
        IdObjetCeintureSacoches => 0.18f,
        IdObjetPochetteTier0 => 0.12f,
        IdObjetSacTier0 => 0.16f,
        IdObjetCarnetSavoir => 0.24f,
        IdObjetSteakCru => 0.14f,
        IdObjetSteakCuit => 0.14f,
        IdObjetOsBoeuf => 0.09f,
        IdObjetCuirBoeuf => 0.11f,
        IdObjetIntestinBoeuf => 0.12f,
        IdObjetIntestinBoeufNettoye => 0.12f,
        IdObjetCharbonBasseQualite => 0.10f,
        IdObjetCharbonMoyenneQualite => 0.11f,
        IdObjetCharbonBonneQualite => 0.12f,
        IdObjetCharbonAntracite => 0.14f,
        105 => 0.32f,
        106 => 0.58f,
        IdObjetHachePierreTier1 => 0.64f,
        IdObjetPellePierreTier0 => 0.62f,
        IdObjetPiochePierreTier0 => 0.66f,
        IdObjetLancePierreTier0 => 0.60f,
        IdObjetFauxPierreTier0 => 0.38f,
        IdObjetBaie => 0.03f,
        34 => 0.04f,
        999 => 1.2f,
        200 => 12.0f,
        IdObjetTableArtisanaTier1 => 18.0f,
        IdObjetTableAnalyseTier1 => 11.5f,
        IdObjetRackBatons => 8.0f,
        IdObjetRackBuches => 8.0f,
        IdObjetCoffreBoisTier0 => 42f,
        IdObjetPitFeu => 26f,
        IdObjetPitFeuRoche => 34f,
        IdObjetAllumeFeu => 0.26f,
        IdObjetFondationBois => 38f,
        IdObjetFondationRoche => 62f,
        IdObjetFondationBoisSoleRoche => 54f,
        IdObjetFondationRocheSoleBois => 58f,
        IdObjetSolBois => 12f,
        IdObjetSolRoche => 18f,
        IdObjetMuretBois => 16f,
        IdObjetMuretPierre => 16f,
        IdObjetMurBois => 24f,
        IdObjetMurBoisFenetre => 24f,
        IdObjetMurBoisCadrePorte => 24f,
        IdObjetPorteBois => 18f,
        IdObjetToitChaume => 14f,
        IdObjetTorche => 1.2f,
        IdObjetFenetreBois => 6.5f,
        IdObjetTableBoisDecorative => 10.0f,
        IdObjetMailletBois => 0.72f,
        IdObjetBolBois => 0.28f,
        IdObjetBolEau => 0.55f,
        IdObjetMortierPilonBois => 0.98f,
        IdObjetAtelleJambe => 0.34f,
        IdObjetAtelleBras => 0.34f,
        IdObjetBandageTier1 => 0.12f,
        _ => 0.5f
    };

    private static float ObtenirMasseUnitaireBoisInventaireKg(SlotInventaire slot)
    {
        int idPourDim = slot.ID;
        if (slot.ID == BlocChutant.ID_BRANCHE)
        {
            if (slot.IndexMorphologique == 1)
            {
                const float rBuisson = 0.0267f;
                const float lenBuisson = 0.2f;
                return Mathf.Max(0.05f, Mathf.Pi * rBuisson * rBuisson * lenBuisson * 520f);
            }
            idPourDim = 32;
        }
        CalculerDimensionsBoisPose(idPourDim, slot.IndexMorphologique, slot.IndexTaille, out float baseRadius, out float baseLength, out float w, out float h);
        float longueur = baseLength;
        if (slot.ScaleEclat.Z > 0.01f)
            longueur *= slot.ScaleEclat.Z;
        float rayonX = Mathf.Max(0.001f, w * 0.5f);
        float rayonY = Mathf.Max(0.001f, h * 0.5f);
        float volume = Mathf.Pi * rayonX * rayonY * Mathf.Max(0.05f, longueur);
        return Mathf.Max(0.05f, volume * 520f);
    }

    public static float ObtenirMasseSlotInventaireKg(SlotInventaire slot)
    {
        if (slot.EstVide)
            return 0f;
        if (ItemPhysique.EstIdRocheMatiere(slot.ID))
            return ObtenirMasseUnitaireRocheInventaireKg(slot.IndexTaille);
        if (slot.ID == 30 || slot.ID == 32 || slot.ID == BlocChutant.ID_BRANCHE)
            return ObtenirMasseUnitaireBoisInventaireKg(slot);
        return ObtenirMasseUnitaireSimpleParIdKg(slot.ID);
    }

    private static float ObtenirMasseTotaleSlotInventaireKg(SlotInventaire slot)
    {
        if (slot.EstVide)
            return 0f;
        return ObtenirMasseSlotInventaireKg(slot) * Mathf.Max(1, ObtenirQuantiteSlot(slot));
    }

    public float ObtenirPoidsTotalPorteKg()
    {
        float total = 0f;
        total += ObtenirMasseTotaleSlotInventaireKg(MainGauche);
        total += ObtenirMasseTotaleSlotInventaireKg(MainDroite);
        total += ObtenirMasseTotaleSlotInventaireKg(EquipementSacDos);
        total += ObtenirMasseTotaleSlotInventaireKg(EquipementCeinture);
        total += ObtenirMasseTotaleSlotInventaireKg(EquipementCarnet);
        for (int i = 0; i < GrilleCraftPoche.Length; i++)
            total += ObtenirMasseTotaleSlotInventaireKg(GrilleCraftPoche[i]);
        for (int i = 0; i < GrilleSacStockage.Length; i++)
            total += ObtenirMasseTotaleSlotInventaireKg(GrilleSacStockage[i]);
        for (int i = 0; i < GrilleCeintureStockage.Length; i++)
            total += ObtenirMasseTotaleSlotInventaireKg(GrilleCeintureStockage[i]);
        return total;
    }

    public float ObtenirMassePhysiqueLogiqueKg()
    {
        // Masse logique = corps de base + charge portée, bornée pour éviter les extrêmes non jouables.
        RaceJoueur race = GameState.Instance?.RaceJoueurCourante ?? RaceJoueur.Humain;
        float masseBase = race == RaceJoueur.Orc ? MasseCorporelleBaseOrcKg : MasseCorporelleBaseHumainKg;
        float masse = masseBase + ObtenirPoidsTotalPorteKg();
        return Mathf.Clamp(masse, 45f, 260f);
    }

    public void AppliquerPousseeBovin(Vector3 directionHorizontale, float impulsionMetresParSeconde)
    {
        Vector3 dir = directionHorizontale;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.0001f || impulsionMetresParSeconde <= 0.001f)
            return;
        dir = dir.Normalized();
        float masse = ObtenirMassePhysiqueLogiqueKg();
        float attenuation = Mathf.Clamp(80f / Mathf.Max(45f, masse), 0.35f, 1.2f);
        float deltaV = Mathf.Max(1.3f, impulsionMetresParSeconde * attenuation);
        Vector3 v = Velocity;
        v.X += dir.X * deltaV;
        v.Z += dir.Z * deltaV;
        if (IsOnFloor())
            v.Y = Mathf.Max(v.Y, Mathf.Clamp(deltaV * 0.18f, 0.15f, 0.65f));
        Velocity = v;
        JouerFlashDegatsBovin();
    }

    public float ObtenirCapacitePoidsMaxKg()
    {
        int forceEffective = ObtenirForceEffective();
        float bonusForceKg = (forceEffective - ValeurNeutreStat) * BonusChargeKgParPointForce;
        return Mathf.Max(0.1f, CapacitePoidsBaseHumainKg + bonusForceKg);
    }

    /// <summary>
    /// Au-delÃ  de la charge Â« confort Â», le joueur peut encore porter mais se dÃ©place plus lentement
    /// (plus la surcharge est grande, plus le facteur est bas).
    /// </summary>
    public float ObtenirFacteurVitesseSelonChargePortee()
    {
        float cap = ObtenirCapacitePoidsMaxKg();
        if (cap < 0.01f)
            return 1f;
        float poids = ObtenirPoidsTotalPorteKg();
        if (poids <= cap)
            return 1f;
        float surplusRelatif = (poids - cap) / cap;
        const float intensiteRalentissement = 1.15f;
        return Mathf.Clamp(1f / (1f + intensiteRalentissement * surplusRelatif), 0.1f, 1f);
    }

    public float ObtenirMultiplicateurVitesseMetaboliste()
    {
        return CoefficientDepuisStat(ObtenirMetabolismeEffective());
    }
}
