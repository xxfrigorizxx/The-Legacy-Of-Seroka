using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private static int SaturerVersInt(double valeur)
    {
        if (double.IsNaN(valeur) || double.IsInfinity(valeur))
            return 0;
        if (valeur <= int.MinValue)
            return int.MinValue;
        if (valeur >= int.MaxValue)
            return int.MaxValue;
        return (int)Math.Round(valeur);
    }

    private static void ObtenirBasesFicheSelonRace(RaceJoueur race, out int force, out int constitution, out int agilite, out int intelligence, out int metabolisme, out int defense)
    {
        if (race == RaceJoueur.Orc)
        {
            force = 20;
            constitution = 20;
            agilite = 10;
            intelligence = 0;
            metabolisme = 10;
            defense = 0;
            return;
        }

        force = 10;
        constitution = 10;
        agilite = 10;
        intelligence = 10;
        metabolisme = 10;
        defense = 0;
    }

    private static void AjouterBonusEquipementFiche(in SlotInventaire slot, ref int bonusPvEquip, ref int bonusForceEquip, ref int bonusAgiliteEquip, ref int bonusIntelligenceEquip, ref int bonusDefenseEquip)
    {
        if (slot.EstVide)
            return;

        switch (slot.ID)
        {
            case IdObjetSacTier0:
                bonusPvEquip += 8;
                break;
            case IdObjetCeinturePoches:
                bonusDefenseEquip += 1;
                break;
            case IdObjetCeintureSacoches:
                bonusDefenseEquip += 2;
                break;
            case IdObjetCarnetSavoir:
                bonusIntelligenceEquip += 2;
                break;
            case 105:
            case 106:
            case IdObjetHachePierreTier1:
            case IdObjetPellePierreTier0:
            case IdObjetPiochePierreTier0:
            case IdObjetLancePierreTier0:
            case IdObjetFauxPierreTier0:
                bonusForceEquip += 1;
                break;
        }
    }

    public FicheStatutPersonnage ObtenirFicheStatutPersonnage()
    {
        RaceJoueur race = GameState.Instance?.RaceJoueurCourante ?? RaceJoueur.Humain;
        ObtenirBasesFicheSelonRace(race, out int baseForce, out int baseConstitution, out int baseAgilite, out int baseIntelligence, out int baseMetabolisme, out int baseDefense);

        ulong niveauForce = ObtenirNiveauFutureState("Force");
        ulong niveauConstitution = ObtenirNiveauFutureState("Constitution");
        ulong niveauAgilite = ObtenirNiveauFutureState("Dextiriter");
        ulong niveauIntelligence = ObtenirNiveauFutureState("Intelligence");
        ulong niveauMetabolisme = ObtenirNiveauFutureState("Metaboliste");

        ulong niveauGlobal = 0UL;
        foreach (ulong niveau in _futureStates.Values)
        {
            if (niveauGlobal > ulong.MaxValue - niveau)
            {
                niveauGlobal = ulong.MaxValue;
                break;
            }
            niveauGlobal += niveau;
        }

        int bonusPvEquip = 0;
        int bonusForceEquip = 0;
        int bonusAgiliteEquip = 0;
        int bonusIntelligenceEquip = 0;
        int bonusDefenseEquip = 0;
        AjouterBonusEquipementFiche(MainGauche, ref bonusPvEquip, ref bonusForceEquip, ref bonusAgiliteEquip, ref bonusIntelligenceEquip, ref bonusDefenseEquip);
        AjouterBonusEquipementFiche(MainDroite, ref bonusPvEquip, ref bonusForceEquip, ref bonusAgiliteEquip, ref bonusIntelligenceEquip, ref bonusDefenseEquip);
        AjouterBonusEquipementFiche(EquipementSacDos, ref bonusPvEquip, ref bonusForceEquip, ref bonusAgiliteEquip, ref bonusIntelligenceEquip, ref bonusDefenseEquip);
        AjouterBonusEquipementFiche(EquipementCeinture, ref bonusPvEquip, ref bonusForceEquip, ref bonusAgiliteEquip, ref bonusIntelligenceEquip, ref bonusDefenseEquip);
        AjouterBonusEquipementFiche(EquipementCarnet, ref bonusPvEquip, ref bonusForceEquip, ref bonusAgiliteEquip, ref bonusIntelligenceEquip, ref bonusDefenseEquip);

        int force = SaturerVersInt(baseForce + (double)niveauForce + bonusForceEquip);
        int constitution = SaturerVersInt(baseConstitution + (double)niveauConstitution);
        int agilite = SaturerVersInt(baseAgilite + (double)niveauAgilite + bonusAgiliteEquip);
        int intelligence = SaturerVersInt(baseIntelligence + (double)niveauIntelligence + bonusIntelligenceEquip);
        int metabolisme = SaturerVersInt(baseMetabolisme + (double)niveauMetabolisme);
        int defense = SaturerVersInt(baseDefense + bonusDefenseEquip);

        float pvMaxFloat = ObtenirPvMaxSectionCorps(SectionCorpsTete)
            + ObtenirPvMaxSectionCorps(SectionCorpsTorse)
            + ObtenirPvMaxSectionCorps(SectionCorpsBrasGauche)
            + ObtenirPvMaxSectionCorps(SectionCorpsBrasDroit)
            + ObtenirPvMaxSectionCorps(SectionCorpsJambeGauche)
            + ObtenirPvMaxSectionCorps(SectionCorpsJambeDroite);
        int pvMax = SaturerVersInt(pvMaxFloat + bonusPvEquip);
        pvMax = Mathf.Max(1, pvMax);
        int pvActuels = SaturerVersInt(Mathf.Clamp(ObtenirRatioSanteGlobaleCorps(), 0f, 1f) * pvMax);

        return new FicheStatutPersonnage(
            race == RaceJoueur.Orc ? "Orc" : "Humain",
            niveauGlobal,
            pvActuels,
            pvMax,
            force,
            constitution,
            agilite,
            intelligence,
            metabolisme,
            defense);
    }

    public ulong ObtenirNiveauFutureState(string nomStat)
    {
        if (string.IsNullOrWhiteSpace(nomStat))
            return 0UL;
        return _futureStates.TryGetValue(nomStat, out ulong niveau) ? niveau : 0UL;
    }

    /// <summary>Probabilite de reussite de l'analyseur manuel (base 50 % + 0,01 % par point d'Intelligence autour de 10).</summary>
    public float ObtenirChanceReussiteAnalyseManuelle()
    {
        int intelligenceEffective = ObtenirFicheStatutPersonnage().Intelligence;
        float chance = ChanceAnalyseBase + ((intelligenceEffective - ValeurNeutreStat) * BonusGameplayParPointStat);
        return Mathf.Clamp(chance, ChanceAnalyseMin, ChanceAnalyseMax);
    }

    public UInt128 ObtenirXpFutureState(string nomStat)
    {
        if (string.IsNullOrWhiteSpace(nomStat))
            return UInt128.Zero;
        return _futureStateXp.TryGetValue(nomStat, out UInt128 xp) ? xp : UInt128.Zero;
    }

    private static UInt128 CalculerXpNiveauSuivant(ulong niveau)
    {
        UInt128 prochainNiveau = (UInt128)niveau + 1u;
        UInt128 termeLineaire = XpHybrideCoefLineaire * prochainNiveau;
        UInt128 termeQuadratique = (prochainNiveau * prochainNiveau) / XpHybrideDivQuadratique;
        return termeLineaire + termeQuadratique;
    }

    public UInt128 ObtenirXpNecessaireProchainNiveauFutureState(string nomStat)
    {
        ulong niveau = ObtenirNiveauFutureState(nomStat);
        return CalculerXpNiveauSuivant(niveau);
    }

    private ulong CalculerXpFutureStateEffectif(string nomStat, ulong xpGagne)
    {
        if (string.IsNullOrWhiteSpace(nomStat) || xpGagne == 0UL)
            return 0UL;
        return AppliquerMultiplicateurRacialXpFutureState(nomStat, xpGagne);
    }

    private void AjouterXpFutureStateInterne(string nomStat, ulong xpEffectif)
    {
        if (string.IsNullOrWhiteSpace(nomStat) || xpEffectif == 0UL)
            return;
        AjouterFutureStateSiAbsent(nomStat, 0UL);
        UInt128 xpActuel = ObtenirXpFutureState(nomStat);
        UInt128 gain = xpEffectif;
        UInt128 xpTotal = xpActuel > UInt128.MaxValue - gain ? UInt128.MaxValue : xpActuel + gain;
        ulong niveau = ObtenirNiveauFutureState(nomStat);
        while (niveau < NiveauMaxFutureState)
        {
            UInt128 cout = ObtenirXpNecessaireProchainNiveauFutureState(nomStat);
            if (xpTotal < cout || cout == UInt128.Zero || cout == UInt128.MaxValue)
                break;
            xpTotal -= cout;
            niveau++;
        }
        _futureStateXp[nomStat] = xpTotal;
        _futureStates[nomStat] = Math.Min(niveau, NiveauMaxFutureState);
        _menuFutureState?.Rafraichir();
    }

    public void AjouterXpFutureState(string nomStat, ulong xpGagne)
    {
        ulong xpEffectif = CalculerXpFutureStateEffectif(nomStat, xpGagne);
        if (xpEffectif == 0UL)
            return;
        AjouterXpFutureStateInterne(nomStat, xpEffectif);
    }

    public ulong AjouterXpFutureStateEtRetourEffectif(string nomStat, ulong xpGagne)
    {
        ulong xpEffectif = CalculerXpFutureStateEffectif(nomStat, xpGagne);
        if (xpEffectif == 0UL)
            return 0UL;
        AjouterXpFutureStateInterne(nomStat, xpEffectif);
        return xpEffectif;
    }

    public void DefinirNiveauFutureState(string nomStat, ulong niveau)
    {
        if (string.IsNullOrWhiteSpace(nomStat))
            return;
        _futureStates[nomStat] = Math.Min(niveau, NiveauMaxFutureState);
        _menuFutureState?.Rafraichir();
    }

    public void AjouterFutureStateSiAbsent(string nomStat, ulong niveauInitial = 0UL)
    {
        if (string.IsNullOrWhiteSpace(nomStat) || _futureStates.ContainsKey(nomStat))
            return;
        _futureStates[nomStat] = Math.Min(niveauInitial, NiveauMaxFutureState);
        _futureStateXp[nomStat] = UInt128.Zero;
        _menuFutureState?.Rafraichir();
    }

    public void OuvrirFutureStateDepuisMenu()
    {
        OuvrirFutureStateDepuisMenu(false);
    }

    public void OuvrirFutureStateDepuisMenu(bool ouvrirMetiers)
    {
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.BasculerVisibilite();
        if (_modelisateur != null && _modelisateur.EstOuvert && !_modelisateur.SaisieTexteEnCours)
            _modelisateur.BasculerVisibilite();
        if (_menuFutureState != null && !_menuFutureState.EstOuvert)
            _menuFutureState.BasculerVisibilite();
        if (ouvrirMetiers)
            _menuFutureState?.DefinirModeMetiers();
        else
            _menuFutureState?.DefinirModeFutureStates();
        _menuFutureState?.Rafraichir();
    }

    public void OuvrirMetiersDepuisMenu()
    {
        OuvrirFutureStateDepuisMenu(true);
    }

    public void OuvrirInventaireDepuisFutureState()
    {
        if (_menuFutureState != null && _menuFutureState.EstOuvert)
            _menuFutureState.BasculerVisibilite();
        if (_menuAnatomie != null && !_menuAnatomie.EstOuvert)
            _menuAnatomie.BasculerVisibilite();
        _menuAnatomie?.ForcerOngletInventaire();
        _menuAnatomie?.RafraichirMenu();
    }

    public IReadOnlyDictionary<string, ulong> ObtenirMetiers() => _metiers;

    public ulong ObtenirNiveauMetier(string nomMetier)
    {
        if (string.IsNullOrWhiteSpace(nomMetier))
            return 0UL;
        return _metiers.TryGetValue(nomMetier, out ulong niveau) ? niveau : 0UL;
    }

    public UInt128 ObtenirXpMetier(string nomMetier)
    {
        if (string.IsNullOrWhiteSpace(nomMetier))
            return UInt128.Zero;
        return _metierXp.TryGetValue(nomMetier, out UInt128 xp) ? xp : UInt128.Zero;
    }

    public UInt128 ObtenirXpNecessaireProchainNiveauMetier(string nomMetier)
    {
        ulong niveau = ObtenirNiveauMetier(nomMetier);
        return CalculerXpNiveauSuivant(niveau);
    }

    public void AjouterMetierSiAbsent(string nomMetier, ulong niveauInitial = 0UL)
    {
        if (string.IsNullOrWhiteSpace(nomMetier) || _metiers.ContainsKey(nomMetier))
            return;
        _metiers[nomMetier] = Math.Min(niveauInitial, NiveauMaxFutureState);
        _metierXp[nomMetier] = UInt128.Zero;
        _menuFutureState?.Rafraichir();
    }

    public void AjouterXpMetier(string nomMetier, ulong xpGagne)
    {
        if (string.IsNullOrWhiteSpace(nomMetier) || xpGagne == 0UL)
            return;
        xpGagne = AppliquerMultiplicateurRacialXpMetier(xpGagne);
        if (xpGagne == 0UL)
            return;
        AjouterMetierSiAbsent(nomMetier, 0UL);
        UInt128 xpActuel = ObtenirXpMetier(nomMetier);
        UInt128 gain = xpGagne;
        UInt128 xpTotal = xpActuel > UInt128.MaxValue - gain ? UInt128.MaxValue : xpActuel + gain;
        ulong niveau = ObtenirNiveauMetier(nomMetier);
        while (niveau < NiveauMaxFutureState)
        {
            UInt128 cout = ObtenirXpNecessaireProchainNiveauMetier(nomMetier);
            if (xpTotal < cout || cout == UInt128.Zero || cout == UInt128.MaxValue)
                break;
            xpTotal -= cout;
            niveau++;
        }
        _metierXp[nomMetier] = xpTotal;
        _metiers[nomMetier] = Math.Min(niveau, NiveauMaxFutureState);
        _menuFutureState?.Rafraichir();
    }

    public float ObtenirBonusDegatsArbreBucheron()
    {
        return ObtenirNiveauMetier("Bucheron") * 0.01f;
    }

    private static float CoefficientDepuisStat(int valeurStat)
    {
        float multiplicateur = 1f + ((valeurStat - ValeurNeutreStat) * BonusGameplayParPointStat);
        if (float.IsNaN(multiplicateur) || float.IsInfinity(multiplicateur))
            return 1f;
        return Mathf.Clamp(multiplicateur, 0.2f, 100000f);
    }

    private int ObtenirForceEffective()
    {
        return Math.Max(0, ObtenirFicheStatutPersonnage().Force);
    }

    private int ObtenirMetabolismeEffective()
    {
        return Math.Max(0, ObtenirFicheStatutPersonnage().Metabolisme);
    }

    public float ObtenirMultiplicateurDegatsForce()
    {
        return CoefficientDepuisStat(ObtenirForceEffective());
    }

    public float ObtenirMultiplicateurCapaciteChargeForce()
    {
        return CoefficientDepuisStat(ObtenirForceEffective());
    }

    /// <summary>Humain neutre x1. Orc : Force x2, Constitution x2 (reserve), Intelligence x0,5.</summary>
    private static ulong AppliquerMultiplicateurRacialXpFutureState(string nomStat, ulong xpGagne)
    {
        if (xpGagne == 0UL) return 0UL;
        RaceJoueur race = GameState.Instance?.RaceJoueurCourante ?? RaceJoueur.Humain;
        double mult = race switch
        {
            RaceJoueur.Humain => 1.0d,
            RaceJoueur.Orc => nomStat switch
            {
                "Intelligence" => 0.5d,
                "Force" => 2.0d,
                "Constitution" => 2.0d,
                _ => 1.0d
            },
            _ => 1.0d
        };
        double d = xpGagne * mult;
        if (d <= 0d) return 0UL;
        if (d >= ulong.MaxValue) return ulong.MaxValue;
        return (ulong)Math.Round(d);
    }

    /// <summary>Métiers sans bonus racial.</summary>
    private static ulong AppliquerMultiplicateurRacialXpMetier(ulong xpGagne)
    {
        return xpGagne;
    }
}
