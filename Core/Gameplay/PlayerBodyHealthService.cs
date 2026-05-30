using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void InitialiserSanteCorps()
    {
        _pvTete = ObtenirPvMaxSectionCorps(SectionCorpsTete);
        _pvTorse = ObtenirPvMaxSectionCorps(SectionCorpsTorse);
        _pvBrasGauche = ObtenirPvMaxSectionCorps(SectionCorpsBrasGauche);
        _pvBrasDroit = ObtenirPvMaxSectionCorps(SectionCorpsBrasDroit);
        _pvJambeGauche = ObtenirPvMaxSectionCorps(SectionCorpsJambeGauche);
        _pvJambeDroite = ObtenirPvMaxSectionCorps(SectionCorpsJambeDroite);
        _integriteOsTete = ObtenirIntegriteOsBaseSection(SectionCorpsTete);
        _integriteOsTorse = ObtenirIntegriteOsBaseSection(SectionCorpsTorse);
        _integriteOsBrasGauche = ObtenirIntegriteOsBaseSection(SectionCorpsBrasGauche);
        _integriteOsBrasDroit = ObtenirIntegriteOsBaseSection(SectionCorpsBrasDroit);
        _integriteOsJambeGauche = ObtenirIntegriteOsBaseSection(SectionCorpsJambeGauche);
        _integriteOsJambeDroite = ObtenirIntegriteOsBaseSection(SectionCorpsJambeDroite);
    }

    private static string NormaliserCleSectionCorps(string cleSection)
    {
        if (string.IsNullOrWhiteSpace(cleSection))
            return SectionCorpsTorse;

        string cle = cleSection.Trim().ToLowerInvariant();
        if (cle.Contains("hitboxtete") || cle.Contains("tete") || cle.Contains("head"))
            return SectionCorpsTete;
        if (cle.Contains("hitboxcorps") || cle.Contains("torse") || cle.Contains("corps") || cle.Contains("chest"))
            return SectionCorpsTorse;
        if (cle.Contains("hitboxbrasg") || cle.Contains("brasg") || cle.Contains("bras_g") || cle.Contains("leftarm"))
            return SectionCorpsBrasGauche;
        if (cle.Contains("hitboxbrasd") || cle.Contains("brasd") || cle.Contains("bras_d") || cle.Contains("rightarm"))
            return SectionCorpsBrasDroit;
        if (cle.Contains("hitboxjambeg") || cle.Contains("jambeg") || cle.Contains("jambe_g") || cle.Contains("leftleg"))
            return SectionCorpsJambeGauche;
        if (cle.Contains("hitboxjambed") || cle.Contains("jambed") || cle.Contains("jambe_d") || cle.Contains("rightleg"))
            return SectionCorpsJambeDroite;
        return SectionCorpsTorse;
    }

    private static int ObtenirPvBaseSectionCorps(string cleSection)
    {
        return cleSection switch
        {
            SectionCorpsTete => 80,
            SectionCorpsTorse => 180,
            SectionCorpsBrasGauche => 110,
            SectionCorpsBrasDroit => 110,
            SectionCorpsJambeGauche => 140,
            SectionCorpsJambeDroite => 140,
            _ => 180
        };
    }

    private static float ObtenirIntegriteOsBaseSection(string cleSection)
    {
        return cleSection switch
        {
            SectionCorpsTete => 90f,
            SectionCorpsTorse => 140f,
            SectionCorpsBrasGauche => 100f,
            SectionCorpsBrasDroit => 100f,
            SectionCorpsJambeGauche => 120f,
            SectionCorpsJambeDroite => 120f,
            _ => 120f
        };
    }

    private const float RatioEtatOsSeuilCasse = 0.35f;
    private const float RatioEtatOsSeuilFelure = 0.70f;
    private const float RatioIntegriteOsFixerFelure = 0.55f;
    private const float RatioIntegriteOsFixerCasse = 0.05f;
    /// <summary>En dessous ou égal : contribution jambe réduite (équivalent fêlure os).</summary>
    private const float RatioPvSeuilFelureMembre = 0.50f;
    private const int DegatsChargeBovinCoupDeTete = 5;
    private const int DegatsChargeBovinCoupDeSabot = 6;
    private const float DureeAtelleJambeSec = 180f;
    private const float DureeAtelleBrasSec = 180f;
    private const float DureeFlashDegatsBovinSec = 0.26f;
    private const float IntensiteMaxFlashDegatsBovin = 0.85f;

    private int ObtenirConstitutionEffective()
    {
        RaceJoueur race = GameState.Instance?.RaceJoueurCourante ?? RaceJoueur.Humain;
        int baseConstitution = race == RaceJoueur.Orc ? 20 : 10;
        ulong niveauConstitution = ObtenirNiveauFutureState("Constitution");
        return Math.Max(1, baseConstitution + SaturerVersInt(niveauConstitution));
    }

    private float ObtenirPvMaxBrutSectionCorps(string cleSection)
    {
        float baseSection = ObtenirPvBaseSectionCorps(cleSection);
        float bonusConstitution = (ObtenirConstitutionEffective() - ValeurNeutreStat) * BonusPvParPointConstitution;
        return Math.Max(1f, baseSection + bonusConstitution);
    }

    private float ObtenirMalusPvMaxBrulureSection(string cleSection)
    {
        return cleSection switch
        {
            SectionCorpsTete => _malusPvMaxBrulureTete,
            SectionCorpsBrasGauche => _malusPvMaxBrulureBrasGauche,
            SectionCorpsBrasDroit => _malusPvMaxBrulureBrasDroit,
            SectionCorpsJambeGauche => _malusPvMaxBrulureJambeGauche,
            SectionCorpsJambeDroite => _malusPvMaxBrulureJambeDroite,
            _ => _malusPvMaxBrulureTorse
        };
    }

    private void DefinirMalusPvMaxBrulureSection(string cleSection, float valeur)
    {
        string section = NormaliserCleSectionCorps(cleSection);
        float pvMaxBrut = ObtenirPvMaxBrutSectionCorps(section);
        float plafond = Mathf.Max(0f, pvMaxBrut);
        float clamp = Mathf.Clamp(valeur, 0f, plafond);
        switch (section)
        {
            case SectionCorpsTete:
                _malusPvMaxBrulureTete = clamp;
                break;
            case SectionCorpsBrasGauche:
                _malusPvMaxBrulureBrasGauche = clamp;
                break;
            case SectionCorpsBrasDroit:
                _malusPvMaxBrulureBrasDroit = clamp;
                break;
            case SectionCorpsJambeGauche:
                _malusPvMaxBrulureJambeGauche = clamp;
                break;
            case SectionCorpsJambeDroite:
                _malusPvMaxBrulureJambeDroite = clamp;
                break;
            default:
                _malusPvMaxBrulureTorse = clamp;
                break;
        }
    }

    private void ClamperPvSectionAuMaximum(string cleSection)
    {
        string section = NormaliserCleSectionCorps(cleSection);
        float pvMax = ObtenirPvMaxSectionCorps(section);
        switch (section)
        {
            case SectionCorpsTete:
                _pvTete = Mathf.Min(_pvTete, pvMax);
                break;
            case SectionCorpsBrasGauche:
                _pvBrasGauche = Mathf.Min(_pvBrasGauche, pvMax);
                break;
            case SectionCorpsBrasDroit:
                _pvBrasDroit = Mathf.Min(_pvBrasDroit, pvMax);
                break;
            case SectionCorpsJambeGauche:
                _pvJambeGauche = Mathf.Min(_pvJambeGauche, pvMax);
                break;
            case SectionCorpsJambeDroite:
                _pvJambeDroite = Mathf.Min(_pvJambeDroite, pvMax);
                break;
            default:
                _pvTorse = Mathf.Min(_pvTorse, pvMax);
                break;
        }
    }

    private void ClamperToutesSectionsPvAuMaximum()
    {
        ClamperPvSectionAuMaximum(SectionCorpsTete);
        ClamperPvSectionAuMaximum(SectionCorpsTorse);
        ClamperPvSectionAuMaximum(SectionCorpsBrasGauche);
        ClamperPvSectionAuMaximum(SectionCorpsBrasDroit);
        ClamperPvSectionAuMaximum(SectionCorpsJambeGauche);
        ClamperPvSectionAuMaximum(SectionCorpsJambeDroite);
    }

    private void AjouterBrulureSectionCorps(string cleSection, float pertePvMax)
    {
        if (pertePvMax <= 0f)
            return;
        string section = NormaliserCleSectionCorps(cleSection);
        float courant = ObtenirMalusPvMaxBrulureSection(section);
        DefinirMalusPvMaxBrulureSection(section, courant + pertePvMax);
        ClamperPvSectionAuMaximum(section);
    }

    private bool SlotInventaireTorcheAllumee(SlotInventaire slot)
    {
        return !slot.EstVide
            && slot.ID == IdObjetTorche
            && (slot.GenomeAssemblage ?? "").StartsWith("TORCHE:1", StringComparison.Ordinal);
    }

    private bool JoueurTientTorcheAllumee()
    {
        return SlotInventaireTorcheAllumee(MainGauche) || SlotInventaireTorcheAllumee(MainDroite);
    }

    private bool EstSourceFeuActive(ItemPhysique item)
    {
        if (item == null || !GodotObject.IsInstanceValid(item) || !item.IsInsideTree())
            return false;
        if (item.ID_Objet == IdObjetTorche)
            return item.EstTorcheAllumee();
        if (item.ID_Objet == IdObjetPitFeu || item.ID_Objet == IdObjetPitFeuRoche)
            return item.EstPitFeuAllume();
        return false;
    }

    private bool EssayerResoudreSectionCorpsAuContactPointMonde(
        Vector3 pointMonde,
        float rayonContact,
        out string sectionTouchee,
        out float distanceCarree)
    {
        sectionTouchee = SectionCorpsTorse;
        distanceCarree = float.MaxValue;
        float rayonCarre = Mathf.Max(0.001f, rayonContact) * Mathf.Max(0.001f, rayonContact);
        bool touche = false;
        foreach (Node enfant in GetChildren())
        {
            if (enfant is not CollisionShape3D hitbox || hitbox.Shape == null)
                continue;
            string cle = NormaliserCleSectionCorps(hitbox.Name);
            Transform3D xf = hitbox.GlobalTransform;
            Vector3 local = xf.AffineInverse() * pointMonde;
            Vector3 procheLocal = PlusProchePointLocalSurForme(local, hitbox.Shape);
            Vector3 procheMonde = xf * procheLocal;
            float d2 = procheMonde.DistanceSquaredTo(pointMonde);
            if (d2 > rayonCarre || d2 >= distanceCarree)
                continue;
            distanceCarree = d2;
            sectionTouchee = cle;
            touche = true;
        }
        return touche;
    }

    private bool EssayerTrouverSourceFeuAuContact(out ItemPhysique sourceFeu, out Vector3 pointFeu, out string sectionTouchee)
    {
        sourceFeu = null;
        pointFeu = Vector3.Zero;
        sectionTouchee = SectionCorpsTorse;
        SceneTree arbre = GetTree();
        if (arbre == null)
            return false;

        float meilleureDistanceContactCarree = float.MaxValue;
        Godot.Collections.Array<Node> candidats = arbre.GetNodesInGroup("BlocsPoses");
        for (int i = 0; i < candidats.Count; i++)
        {
            if (candidats[i] is not ItemPhysique item || !EstSourceFeuActive(item))
                continue;

            // Une torche tenue en main ne doit pas blesser le joueur.
            if (item.ID_Objet == IdObjetTorche && JoueurTientTorcheAllumee() && item.GetParent() == this)
                continue;

            if (!item.EssayerObtenirZoneContactFlammeMonde(out Vector3 point, out float rayon))
                continue;

            if (!EssayerResoudreSectionCorpsAuContactPointMonde(point, rayon, out string sectionCandidate, out float distanceCandidate))
                continue;
            if (distanceCandidate >= meilleureDistanceContactCarree)
                continue;

            meilleureDistanceContactCarree = distanceCandidate;
            sourceFeu = item;
            pointFeu = point;
            sectionTouchee = sectionCandidate;
        }

        return sourceFeu != null;
    }

    private void MettreAJourDegatsBrulureFeu(float dt)
    {
        _cooldownDegatsBrulureFeuRestant = Mathf.Max(0f, _cooldownDegatsBrulureFeuRestant - dt);
        if (_cooldownDegatsBrulureFeuRestant > 0f)
            return;

        if (!EssayerTrouverSourceFeuAuContact(out _, out _, out string sectionTouchee))
            return;

        // Dégât feu = perte de PV max (brûlure) : non récupérable avec un bandage standard.
        AjouterBrulureSectionCorps(sectionTouchee, PertePvMaxBrulureParImpact);
        _cooldownDegatsBrulureFeuRestant = IntervalleDegatsBrulureFeuSec;
        RafraichirHUD();
        _menuAnatomie?.RafraichirSanteCorpsImmediate();
        MettreAJourEffetVisionTete();
    }

    private void ReinitialiserBruluresFeu()
    {
        _cooldownDegatsBrulureFeuRestant = 0f;
        _malusPvMaxBrulureTete = 0f;
        _malusPvMaxBrulureTorse = 0f;
        _malusPvMaxBrulureBrasGauche = 0f;
        _malusPvMaxBrulureBrasDroit = 0f;
        _malusPvMaxBrulureJambeGauche = 0f;
        _malusPvMaxBrulureJambeDroite = 0f;
    }

    public void SoignerBrulureSectionCorps(string cleSection, float pointsRecuperes)
    {
        if (pointsRecuperes <= 0f)
            return;
        string section = NormaliserCleSectionCorps(cleSection);
        float courant = ObtenirMalusPvMaxBrulureSection(section);
        DefinirMalusPvMaxBrulureSection(section, Mathf.Max(0f, courant - pointsRecuperes));
        ClamperPvSectionAuMaximum(section);
        RafraichirHUD();
        _menuAnatomie?.RafraichirSanteCorpsImmediate();
    }

    private float ObtenirPvMaxSectionCorps(string cleSection)
    {
        string section = NormaliserCleSectionCorps(cleSection);
        float brut = ObtenirPvMaxBrutSectionCorps(section);
        float malus = ObtenirMalusPvMaxBrulureSection(section);
        return Mathf.Max(0f, brut - malus);
    }

    private float ObtenirIntegriteOsSectionCorps(string cleSection)
    {
        return cleSection switch
        {
            SectionCorpsTete => _integriteOsTete,
            SectionCorpsBrasGauche => _integriteOsBrasGauche,
            SectionCorpsBrasDroit => _integriteOsBrasDroit,
            SectionCorpsJambeGauche => _integriteOsJambeGauche,
            SectionCorpsJambeDroite => _integriteOsJambeDroite,
            _ => _integriteOsTorse
        };
    }

    private void DefinirIntegriteOsSectionCorps(string cleSection, float valeur)
    {
        float maxSection = ObtenirIntegriteOsBaseSection(cleSection);
        float clamp = Mathf.Clamp(valeur, 0f, maxSection);
        switch (cleSection)
        {
            case SectionCorpsTete:
                _integriteOsTete = clamp;
                break;
            case SectionCorpsBrasGauche:
                _integriteOsBrasGauche = clamp;
                break;
            case SectionCorpsBrasDroit:
                _integriteOsBrasDroit = clamp;
                break;
            case SectionCorpsJambeGauche:
                _integriteOsJambeGauche = clamp;
                break;
            case SectionCorpsJambeDroite:
                _integriteOsJambeDroite = clamp;
                break;
            default:
                _integriteOsTorse = clamp;
                break;
        }
    }

    private enum EtatOsSimple
    {
        BonEtat,
        Felure,
        Casse
    }

    private EtatOsSimple EvaluerEtatOsSectionCorps(string cleSection)
    {
        float maxSection = Mathf.Max(1f, ObtenirIntegriteOsBaseSection(cleSection));
        float ratio = Mathf.Clamp(ObtenirIntegriteOsSectionCorps(cleSection) / maxSection, 0f, 1f);
        if (ratio <= RatioEtatOsSeuilCasse)
            return EtatOsSimple.Casse;
        if (ratio <= RatioEtatOsSeuilFelure)
            return EtatOsSimple.Felure;
        return EtatOsSimple.BonEtat;
    }

    private float ObtenirPvActuelSectionCorps(string cleSection)
    {
        string section = NormaliserCleSectionCorps(cleSection);
        return section switch
        {
            SectionCorpsTete => _pvTete,
            SectionCorpsBrasGauche => _pvBrasGauche,
            SectionCorpsBrasDroit => _pvBrasDroit,
            SectionCorpsJambeGauche => _pvJambeGauche,
            SectionCorpsJambeDroite => _pvJambeDroite,
            _ => _pvTorse
        };
    }

    private float ObtenirRatioPvSectionCorps(string cleSection)
    {
        float max = ObtenirPvMaxSectionCorps(cleSection);
        if (max <= 0.001f)
            return 0f;
        return Mathf.Clamp(ObtenirPvActuelSectionCorps(cleSection) / max, 0f, 1f);
    }

    private int CalculerDegatsImpactZonePct(string cleSection, float ratioPct)
    {
        float max = ObtenirPvMaxSectionCorps(cleSection);
        return Mathf.Max(1, Mathf.CeilToInt(max * Mathf.Clamp(ratioPct, 0.01f, 1f)));
    }

    private static EtatOsSimple EvaluerEtatMembreDepuisRatioPv(float ratioPv)
    {
        if (ratioPv <= 0f)
            return EtatOsSimple.Casse;
        if (ratioPv <= RatioPvSeuilFelureMembre)
            return EtatOsSimple.Felure;
        return EtatOsSimple.BonEtat;
    }

    /// <summary>Combine os + PV : le pire des deux états s'applique (les deux s'accumulent).</summary>
    private EtatOsSimple EvaluerEtatEffectifMembre(string cleSection)
    {
        EtatOsSimple etatOs = EvaluerEtatOsSectionCorps(cleSection);
        EtatOsSimple etatPv = EvaluerEtatMembreDepuisRatioPv(ObtenirRatioPvSectionCorps(cleSection));
        return (EtatOsSimple)Mathf.Max((int)etatOs, (int)etatPv);
    }

    private void DefinirEtatOsSectionCorps(string cleSection, EtatOsSimple etat)
    {
        float maxSection = Mathf.Max(1f, ObtenirIntegriteOsBaseSection(cleSection));
        float cible = etat switch
        {
            EtatOsSimple.Casse => maxSection * RatioIntegriteOsFixerCasse,
            EtatOsSimple.Felure => maxSection * RatioIntegriteOsFixerFelure,
            _ => maxSection
        };
        DefinirIntegriteOsSectionCorps(cleSection, cible);
    }

    private static float CalculerChanceFissureChute(float hauteurChuteMetres)
    {
        int metresEntiers = Mathf.FloorToInt(Mathf.Max(0f, hauteurChuteMetres));
        if (metresEntiers <= 5)
            return 0f;
        return Mathf.Clamp(metresEntiers - 5, 0, 100);
    }

    private static float CalculerChanceCasseChute(float hauteurChuteMetres)
    {
        int metresEntiers = Mathf.FloorToInt(Mathf.Max(0f, hauteurChuteMetres));
        if (metresEntiers < 25)
            return 0f;
        return Mathf.Clamp(metresEntiers - 24, 0, 100);
    }

    private static bool TirageChanceReussit(float chancePct)
    {
        if (chancePct <= 0f)
            return false;
        if (chancePct >= 100f)
            return true;
        return GD.Randf() * 100f < chancePct;
    }

    private void AppliquerRisqueChuteSurJambe(string sectionJambe, float chanceFissure, float chanceCasse)
    {
        EtatOsSimple etatInitial = EvaluerEtatOsSectionCorps(sectionJambe);
        if (etatInitial == EtatOsSimple.Casse)
            return;

        // Règle demandée: on lance d'abord le dé de fissure, puis le dé de casse.
        bool fissureReussie = TirageChanceReussit(chanceFissure);
        if (fissureReussie)
        {
            if (etatInitial == EtatOsSimple.BonEtat)
                DefinirEtatOsSectionCorps(sectionJambe, EtatOsSimple.Felure);
            else if (etatInitial == EtatOsSimple.Felure)
                DefinirEtatOsSectionCorps(sectionJambe, EtatOsSimple.Casse);
        }

        EtatOsSimple etatApresFissure = EvaluerEtatOsSectionCorps(sectionJambe);
        if (etatApresFissure != EtatOsSimple.Casse && TirageChanceReussit(chanceCasse))
            DefinirEtatOsSectionCorps(sectionJambe, EtatOsSimple.Casse);
    }

    private void AppliquerRisquesChuteOsJambes(float hauteurChuteMetres)
    {
        float chanceFissure = CalculerChanceFissureChute(hauteurChuteMetres);
        float chanceCasse = CalculerChanceCasseChute(hauteurChuteMetres);
        if (chanceFissure <= 0f && chanceCasse <= 0f)
            return;

        AppliquerRisqueChuteSurJambe(SectionCorpsJambeGauche, chanceFissure, chanceCasse);
        AppliquerRisqueChuteSurJambe(SectionCorpsJambeDroite, chanceFissure, chanceCasse);
    }

    private static bool SectionPeutRecevoirFractureOsAttaqueBovin(string section)
    {
        return section == SectionCorpsBrasGauche
            || section == SectionCorpsBrasDroit
            || section == SectionCorpsJambeGauche
            || section == SectionCorpsJambeDroite;
    }

    public Vector3 ObtenirCentreHitboxMonde(string cleSection)
    {
        string section = NormaliserCleSectionCorps(cleSection);
        foreach (Node enfant in GetChildren())
        {
            if (enfant is CollisionShape3D cs && cs.Shape != null
                && NormaliserCleSectionCorps(cs.Name) == section)
                return cs.GlobalPosition;
        }
        return GlobalPosition + Vector3.Up * 0.9f;
    }

    /// <summary>Impact bovin : forme touchée + contexte coup de tête (haut) vs ruade (bas / arrière).</summary>
    public string ResoudreSectionCorpsDepuisImpactCharge(Vector3 pointMonde, int indiceFormePhysique = -1, bool coupDeTete = false)
    {
        if (indiceFormePhysique >= 0)
        {
            uint proprietaire = ShapeFindOwner(indiceFormePhysique);
            if (proprietaire != uint.MaxValue)
            {
                GodotObject noeud = ShapeOwnerGetOwner(proprietaire);
                if (noeud is CollisionShape3D cs)
                {
                    string section = NormaliserCleSectionCorps(cs.Name);
                    return CorrigerSectionChargeBovin(pointMonde, section, coupDeTete);
                }
            }
        }
        return ResoudreSectionCorpsDepuisPointMonde(pointMonde, coupDeTete);
    }

    private string CorrigerSectionChargeBovin(Vector3 pointMonde, string section, bool coupDeTete)
    {
        section = NormaliserCleSectionCorps(section);
        float hauteurRelative = pointMonde.Y - GlobalPosition.Y;
        if (coupDeTete)
        {
            if ((section == SectionCorpsJambeGauche || section == SectionCorpsJambeDroite)
                && hauteurRelative >= 0.58f)
                return hauteurRelative >= 0.92f ? SectionCorpsTete : SectionCorpsTorse;
            if ((section == SectionCorpsBrasGauche || section == SectionCorpsBrasDroit)
                && hauteurRelative >= 1.05f)
                return SectionCorpsTete;
        }
        else
        {
            if (section == SectionCorpsTete && hauteurRelative < 0.82f)
                return SectionCorpsTorse;
        }
        return section;
    }

    private static float ObtenirBiasDistanceResolutionSection(string section)
    {
        if (section == SectionCorpsTorse)
            return 1.18f;
        if (section == SectionCorpsTete)
            return 1.08f;
        if (section == SectionCorpsBrasGauche || section == SectionCorpsBrasDroit
            || section == SectionCorpsJambeGauche || section == SectionCorpsJambeDroite)
            return 0.82f;
        return 1f;
    }

    private static float ObtenirBiasDistanceResolutionSectionCharge(bool coupDeTete, string section)
    {
        if (coupDeTete)
        {
            if (section == SectionCorpsTete)
                return 0.68f;
            if (section == SectionCorpsTorse)
                return 0.82f;
            if (section == SectionCorpsBrasGauche || section == SectionCorpsBrasDroit)
                return 1.05f;
            return 1.42f;
        }
        if (section == SectionCorpsJambeGauche || section == SectionCorpsJambeDroite)
            return 0.78f;
        if (section == SectionCorpsTorse)
            return 0.95f;
        if (section == SectionCorpsTete)
            return 1.25f;
        return 1.05f;
    }

    private static Vector3 PlusProchePointLocalSurForme(Vector3 local, Shape3D forme)
    {
        switch (forme)
        {
            case SphereShape3D sphere:
            {
                float len2 = local.LengthSquared();
                if (len2 < 1e-8f)
                    return new Vector3(0f, sphere.Radius, 0f);
                return local.Normalized() * sphere.Radius;
            }
            case CapsuleShape3D capsule:
            {
                float r = capsule.Radius;
                float half = capsule.Height * 0.5f;
                float cylHalf = Mathf.Max(0f, half - r);
                Vector3 p0 = new Vector3(0f, -cylHalf, 0f);
                Vector3 p1 = new Vector3(0f, cylHalf, 0f);
                Vector3 ab = p1 - p0;
                float t = ab.LengthSquared() > 1e-8f
                    ? Mathf.Clamp((local - p0).Dot(ab) / ab.LengthSquared(), 0f, 1f)
                    : 0f;
                Vector3 onAxis = p0 + ab * t;
                Vector3 perp = local - onAxis;
                float perpLen = perp.Length();
                if (perpLen <= r)
                    return local;
                return onAxis + perp / perpLen * r;
            }
            default:
                return Vector3.Zero;
        }
    }

    /// <summary>Détermine la section touchée : point le plus proche sur chaque hitbox (membres favorisés vs torse/tête).</summary>
    public string ResoudreSectionCorpsDepuisPointMonde(Vector3 pointMonde, bool? biaisChargeBovinCoupDeTete = null)
    {
        string section = SectionCorpsTorse;
        float meilleurScore = float.MaxValue;
        foreach (Node enfant in GetChildren())
        {
            if (enfant is not CollisionShape3D hitbox || hitbox.Shape == null)
                continue;
            string cle = NormaliserCleSectionCorps(hitbox.Name);
            Transform3D xf = hitbox.GlobalTransform;
            Vector3 local = xf.AffineInverse() * pointMonde;
            Vector3 procheLocal = PlusProchePointLocalSurForme(local, hitbox.Shape);
            Vector3 procheMonde = xf * procheLocal;
            float bias = biaisChargeBovinCoupDeTete.HasValue
                ? ObtenirBiasDistanceResolutionSectionCharge(biaisChargeBovinCoupDeTete.Value, cle)
                : ObtenirBiasDistanceResolutionSection(cle);
            float score = procheMonde.DistanceSquaredTo(pointMonde) * bias;
            if (score >= meilleurScore)
                continue;
            meilleurScore = score;
            section = cle;
        }
        if (biaisChargeBovinCoupDeTete.HasValue)
            section = CorrigerSectionChargeBovin(pointMonde, section, biaisChargeBovinCoupDeTete.Value);
        return section;
    }

    /// <summary>
    /// Attaque bovin sur une section précise : fracture os uniquement bras/jambes (5 % fêlure ou fêlure→cassé).
    /// </summary>
    public void AppliquerRisqueAttaqueBovinSurOsSection(string sectionCorps, float chancePct = 5f)
    {
        string section = NormaliserCleSectionCorps(sectionCorps);
        if (!SectionPeutRecevoirFractureOsAttaqueBovin(section))
            return;
        if (!TirageChanceReussit(chancePct))
            return;

        EtatOsSimple etat = EvaluerEtatOsSectionCorps(section);
        if (etat == EtatOsSimple.Casse)
            return;

        string nom = NomSectionCorpsPourLog(section);
        if (etat == EtatOsSimple.Felure)
        {
            DefinirEtatOsSectionCorps(section, EtatOsSimple.Casse);
            GD.Print($"ZERO-K : Attaque bovin -> {nom} : os casse.");
        }
        else
        {
            DefinirEtatOsSectionCorps(section, EtatOsSimple.Felure);
            GD.Print($"ZERO-K : Attaque bovin -> {nom} : os felure.");
        }
    }

    /// <summary>Impact charge bovin : coup de tête 5 PV, sabot 6 PV sur la zone touchée (hitbox du raycast).</summary>
    public void RecevoirImpactChargeBovin(
        Vector3 pointImpactMonde,
        Vector3 directionPousseeHorizontale,
        float impulsionMetresParSeconde,
        bool estCoupDeTete,
        float chanceFractureOsPct = 5f,
        int indiceFormeImpactPhysique = -1)
    {
        string section = ResoudreSectionCorpsDepuisImpactCharge(pointImpactMonde, indiceFormeImpactPhysique, estCoupDeTete);
        int degatsZone = estCoupDeTete ? DegatsChargeBovinCoupDeTete : DegatsChargeBovinCoupDeSabot;
        bool peutFracturerOs = SectionPeutRecevoirFractureOsAttaqueBovin(section);
        AppliquerDegatsSectionCorps(section, degatsZone, affecterOs: peutFracturerOs);
        AppliquerRisqueAttaqueBovinSurOsSection(section, chanceFractureOsPct);
        AppliquerPousseeBovin(directionPousseeHorizontale, impulsionMetresParSeconde);
        GD.Print($"ZERO-K : Charge bovin -> impact sur {NomSectionCorpsPourLog(section)}.");
    }

    private static string NomSectionCorpsPourLog(string section)
    {
        return section switch
        {
            SectionCorpsTete => "tete",
            SectionCorpsBrasGauche => "bras gauche",
            SectionCorpsBrasDroit => "bras droit",
            SectionCorpsJambeGauche => "jambe gauche",
            SectionCorpsJambeDroite => "jambe droite",
            _ => "torse"
        };
    }

    /// <summary>Compatibilité : applique le risque sur les deux bras (ancien comportement).</summary>
    public void AppliquerRisqueAttaqueBovinSurOsBras(float chancePctParBras = 5f)
    {
        AppliquerRisqueAttaqueBovinSurOsSection(SectionCorpsBrasGauche, chancePctParBras);
        AppliquerRisqueAttaqueBovinSurOsSection(SectionCorpsBrasDroit, chancePctParBras);
    }

    private void SuivreEtAppliquerRisquesChuteOsJambes(bool estDansEau)
    {
        bool estAuSol = IsOnFloor();
        if (!estAuSol)
        {
            if (_etatAuSolPrecedent)
                _sommetYChuteCourante = GlobalPosition.Y;
            else
                _sommetYChuteCourante = Mathf.Max(_sommetYChuteCourante, GlobalPosition.Y);
            _etatAuSolPrecedent = false;
            return;
        }

        if (!_etatAuSolPrecedent && !estDansEau)
        {
            float hauteurChute = Mathf.Max(0f, _sommetYChuteCourante - GlobalPosition.Y);
            AppliquerRisquesChuteOsJambes(hauteurChute);
        }

        _etatAuSolPrecedent = true;
        _sommetYChuteCourante = GlobalPosition.Y;
    }

    private static int CalculerDegatsOsDepuisImpact(int degatsImpact)
    {
        if (degatsImpact <= 0)
            return 0;
        // Indépendant des PV restants : les os se dégradent sur l'impact brut.
        return Mathf.Max(1, Mathf.CeilToInt(degatsImpact * 0.35f));
    }

    public void AppliquerDegatsSectionCorps(string cleSection, int degats, bool affecterOs = true)
    {
        if (degats <= 0)
            return;
        float multiplicateurDegats = ObtenirMultiplicateurDegatsConsommationBaies();
        if (multiplicateurDegats < 0.9999f)
            degats = Mathf.Max(1, Mathf.RoundToInt(degats * multiplicateurDegats));

        string section = NormaliserCleSectionCorps(cleSection);
        float pvAvant = section switch
        {
            SectionCorpsTete => _pvTete,
            SectionCorpsBrasGauche => _pvBrasGauche,
            SectionCorpsBrasDroit => _pvBrasDroit,
            SectionCorpsJambeGauche => _pvJambeGauche,
            SectionCorpsJambeDroite => _pvJambeDroite,
            _ => _pvTorse
        };
        switch (section)
        {
            case SectionCorpsTete:
                _pvTete = Mathf.Max(0, _pvTete - degats);
                break;
            case SectionCorpsBrasGauche:
                _pvBrasGauche = Mathf.Max(0, _pvBrasGauche - degats);
                break;
            case SectionCorpsBrasDroit:
                _pvBrasDroit = Mathf.Max(0, _pvBrasDroit - degats);
                break;
            case SectionCorpsJambeGauche:
                _pvJambeGauche = Mathf.Max(0, _pvJambeGauche - degats);
                break;
            case SectionCorpsJambeDroite:
                _pvJambeDroite = Mathf.Max(0, _pvJambeDroite - degats);
                break;
            default:
                _pvTorse = Mathf.Max(0, _pvTorse - degats);
                break;
        }
        float pvApres = section switch
        {
            SectionCorpsTete => _pvTete,
            SectionCorpsBrasGauche => _pvBrasGauche,
            SectionCorpsBrasDroit => _pvBrasDroit,
            SectionCorpsJambeGauche => _pvJambeGauche,
            SectionCorpsJambeDroite => _pvJambeDroite,
            _ => _pvTorse
        };
        int degatsEffectifs = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, pvAvant - pvApres)));
        if (affecterOs)
        {
            int degatsOs = CalculerDegatsOsDepuisImpact(degatsEffectifs);
            if (degatsOs > 0)
                DefinirIntegriteOsSectionCorps(section, ObtenirIntegriteOsSectionCorps(section) - degatsOs);
        }
        AjouterXpConstitutionDepuisDegats(degatsEffectifs);
        if (section == SectionCorpsTorse)
            AppliquerPlafondEnduranceSelonTorse();
        RafraichirHUD();
        _menuAnatomie?.RafraichirSanteCorpsImmediate();
        MettreAJourEffetVisionTete();
        VerifierMortJoueurSiNecessaire();
    }

    private void AjouterXpConstitutionDepuisDegats(int degatsEffectifs)
    {
        if (degatsEffectifs <= 0)
            return;
        AjouterFutureStateSiAbsent("Constitution", 0UL);
        ulong gain = (ulong)degatsEffectifs;
        _degatsCumulesConstitution = _degatsCumulesConstitution > ulong.MaxValue - gain
            ? ulong.MaxValue
            : _degatsCumulesConstitution + gain;
        ulong xpConstitution = _degatsCumulesConstitution / (ulong)DegatsParPointXpConstitution;
        _degatsCumulesConstitution %= (ulong)DegatsParPointXpConstitution;
        if (xpConstitution > 0UL)
            AjouterXpFutureState("Constitution", xpConstitution);
    }

    private const float GainFaimConsommationSteakCru = 5f;
    private const float GainFaimConsommationSteakCuit = 50f;

    private void AppliquerVariationFaim(float variation)
    {
        _faimJoueur = Mathf.Clamp(_faimJoueur + variation, 0f, FaimMaxJoueur);
    }

    private string ObtenirSectionCorpsAleatoire()
    {
        int idx = (int)GD.Randi() % SectionsCorpsToutes.Length;
        if (idx < 0 || idx >= SectionsCorpsToutes.Length)
            idx = 0;
        return SectionsCorpsToutes[idx];
    }

    private string ObtenirSectionCorpsPlusEndommagee()
    {
        string sectionChoisie = SectionCorpsTorse;
        float manqueMax = -1f;
        for (int i = 0; i < SectionsCorpsToutes.Length; i++)
        {
            string section = SectionsCorpsToutes[i];
            float manque = Mathf.Max(0f, ObtenirPvMaxSectionCorps(section) - ObtenirPvActuelSectionCorps(section));
            if (manque > manqueMax)
            {
                manqueMax = manque;
                sectionChoisie = section;
            }
        }
        return sectionChoisie;
    }


    public void SoignerSectionCorps(string cleSection, int pointsSoin)
    {
        if (pointsSoin <= 0)
            return;

        string section = NormaliserCleSectionCorps(cleSection);
        switch (section)
        {
            case SectionCorpsTete:
                _pvTete = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvTete + pointsSoin);
                break;
            case SectionCorpsBrasGauche:
                _pvBrasGauche = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvBrasGauche + pointsSoin);
                break;
            case SectionCorpsBrasDroit:
                _pvBrasDroit = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvBrasDroit + pointsSoin);
                break;
            case SectionCorpsJambeGauche:
                _pvJambeGauche = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvJambeGauche + pointsSoin);
                break;
            case SectionCorpsJambeDroite:
                _pvJambeDroite = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvJambeDroite + pointsSoin);
                break;
            default:
                _pvTorse = Mathf.Min(ObtenirPvMaxSectionCorps(section), _pvTorse + pointsSoin);
                break;
        }
        RafraichirHUD();
        _menuAnatomie?.RafraichirSanteCorpsImmediate();
        MettreAJourEffetVisionTete();
    }

    public IReadOnlyList<SectionSanteCorps> ObtenirEtatSanteCorps()
    {
        _cacheSanteCorps[0] = ConstruireEtatSanteSection(SectionCorpsTete, "Tete", "Os + chair", "Crane", _pvTete, _integriteOsTete);
        _cacheSanteCorps[1] = ConstruireEtatSanteSection(SectionCorpsTorse, "Torse", "Os + chair", "Cage thoracique", _pvTorse, _integriteOsTorse);
        _cacheSanteCorps[2] = ConstruireEtatSanteSection(SectionCorpsBrasGauche, "Bras gauche", "Os + chair", "Os du bras", _pvBrasGauche, _integriteOsBrasGauche);
        _cacheSanteCorps[3] = ConstruireEtatSanteSection(SectionCorpsBrasDroit, "Bras droit", "Os + chair", "Os du bras", _pvBrasDroit, _integriteOsBrasDroit);
        _cacheSanteCorps[4] = ConstruireEtatSanteSection(SectionCorpsJambeGauche, "Jambe gauche", "Os + chair", "Os de la jambe", _pvJambeGauche, _integriteOsJambeGauche);
        _cacheSanteCorps[5] = ConstruireEtatSanteSection(SectionCorpsJambeDroite, "Jambe droite", "Os + chair", "Os de la jambe", _pvJambeDroite, _integriteOsJambeDroite);
        return _cacheSanteCorps;
    }

    private SectionSanteCorps ConstruireEtatSanteSection(
        string cle,
        string nom,
        string matiere,
        string os,
        float pointsVie,
        float integriteOs)
    {
        float pointsVieMax = ObtenirPvMaxSectionCorps(cle);
        float pointsVieMaxBrut = ObtenirPvMaxBrutSectionCorps(cle);
        float pointsVieBrulureBloquee = ObtenirMalusPvMaxBrulureSection(cle);
        return new SectionSanteCorps(
            cle,
            nom,
            matiere,
            os,
            pointsVie,
            pointsVieMax,
            pointsVieMaxBrut,
            pointsVieBrulureBloquee,
            integriteOs,
            ObtenirIntegriteOsBaseSection(cle));
    }

    public float ObtenirRatioSanteGlobaleCorps()
    {
        float pvActuels = _pvTete + _pvTorse + _pvBrasGauche + _pvBrasDroit + _pvJambeGauche + _pvJambeDroite;
        float pvMax = ObtenirPvMaxSectionCorps(SectionCorpsTete)
            + ObtenirPvMaxSectionCorps(SectionCorpsTorse)
            + ObtenirPvMaxSectionCorps(SectionCorpsBrasGauche)
            + ObtenirPvMaxSectionCorps(SectionCorpsBrasDroit)
            + ObtenirPvMaxSectionCorps(SectionCorpsJambeGauche)
            + ObtenirPvMaxSectionCorps(SectionCorpsJambeDroite);
        if (pvMax <= 0)
            return 0f;
        return pvActuels / (float)pvMax;
    }
}
