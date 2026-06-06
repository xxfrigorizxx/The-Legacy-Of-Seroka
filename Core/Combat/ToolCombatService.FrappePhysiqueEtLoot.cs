using Godot;
using System;

public partial class Joueur
{
    /// <summary>Arbres vivants/morts, roches, rigides — efficacité hache émergente.</summary>
    private void ExecuterFrappePhysique(float force, float efficaciteHache, float masseOutil, Node objetTouche, Vector3 pointImpact, Vector3 directionFrappe, TypeMouvementFrappe mouvement)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        float multiplicateurDegatsBras = ObtenirMultiplicateurDegatsFrappeSelonEtatOsBras();
        if (multiplicateurDegatsBras <= 0f)
        {
            AfficherMessageEtatBrasAction("ZERO-K : Bras casse -> aucun degat de frappe.");
            return;
        }
        float multiplicateurForce = ObtenirMultiplicateurDegatsForce();

        // Évite un « soft-lock » : avec pelle/outil lourd ou mauvais angle, efficaciteHache peut chuter avant d'atteindre ArbreMort.
        bool viseTerrain = EstSolViseParRayon(_rayon, objetTouche);
        RigidBody3D probeCadavre = viseTerrain ? null : ResoudreCadavreArbreCible(objetTouche, pointImpact);
        bool cadavreBoise = EstRigidCadavreBoise(probeCadavre);

        if (!cadavreBoise && efficaciteHache < 0.4f && masseOutil > 2f && mainActive.ID != 106)
        {
            AlerteSqueletteBoiteNoire("REBOND MASSIF ! Tu frappes avec le plat de l'outil. Choc structurel violent !");
            return;
        }

        float multiplicateurLame = Mathf.Clamp(efficaciteHache * 20.0f, 1.0f, 40.0f);
        if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.5f);
        else if (mainActive.ID == 105 && EstFrappeDagueAvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.35f);
        else if (mainActive.ID == IdObjetFauxPierreTier0 && EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.32f);
        else if (mainActive.ID == 106 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.85f);
        else if (mainActive.ID == IdObjetHachePierreTier1 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 5.70f);
        else if (mainActive.ID == IdObjetLancePierreTier0 && EstFrappeLance111AvecLaPointe(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.95f);
        else if (mainActive.EstUnEclat && mainActive.MeshEclat != null && mainActive.ID != 100)
            multiplicateurLame = Mathf.Min(multiplicateurLame, 40.0f);

        Vector3 normaleImpact = _rayon != null && _rayon.IsColliding() ? _rayon.GetCollisionNormal() : -directionFrappe;
        float facteurContactOutil = Mathf.Clamp(multiplicateurLame / 2.2f, 0.82f, 1.36f);
        float forceImpact = CalculerIntensiteImpactPhysique(
            masseOutil * multiplicateurForce,
            force,
            mouvement,
            directionFrappe,
            normaleImpact,
            facteurContactOutil,
            1f,
            1f) * 9.4f;
        forceImpact *= multiplicateurDegatsBras;
        float epaisseurLame = CalculerEpaisseurLamePourImpact(mainActive, directionFrappe);

        if (objetTouche == null)
            return;

        RigidBody3D cadavreArbre = viseTerrain ? null : ResoudreCadavreArbreCible(objetTouche, pointImpact);
        if (cadavreArbre != null)
        {
            ExecuterFrappeCadavreArbre(cadavreArbre, mainActive, pointImpact, directionFrappe);
            return;
        }

        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            bool arbreJungle = arbre.IndexBotanique == LSystem_Botanique.IndexJungle;
            bool rochePlate = ItemPhysique.EstIdRocheMatiere(mainActive.ID)
                && (mainActive.IndexMorphologique == 1 || mainActive.IndexMorphologique == 2);
            bool rochePointe = ItemPhysique.EstIdRocheMatiere(mainActive.ID) && mainActive.IndexMorphologique == 3;
            // Dague sur liane: désormais en maintien (2s), pas en clic instantané.
            if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0) && arbre.IndexBotanique == LSystem_Botanique.IndexJungle)
            {
                GD.Print(mainActive.ID == IdObjetFauxPierreTier0
                    ? "ZERO-K : Maintenez le clic avec la faux pendant 2s pour couper la liane."
                    : "ZERO-K : Maintenez le clic avec la dague pendant 2s pour couper la liane.");
                return;
            }

            bool outilTranchantPourArbre = mainActive.ID == 106
                || mainActive.ID == IdObjetHachePierreTier1
                || mainActive.EstUnEclat
                || rochePlate
                || rochePointe;
            if (!outilTranchantPourArbre)
            {
                GD.Print("ZERO-K : Pour entamer un arbre, utilisez une roche matière aplatie (plate/ovale), un éclat, ou une hachette.");
                return;
            }
            if (rochePointe && arbre.AgeEnJours <= 2)
            {
                GD.Print("ZERO-K : Sur jeune arbre (âge 1-2), seule la roche plate entame le bois.");
                return;
            }

            // Normalisation anti-explosion: la chaîne de multiplicateurs amont peut sinon one-shot tout (bois/roche).
            float finesseLame = Mathf.Clamp(0.11f / Mathf.Max(0.02f, epaisseurLame), 0.55f, 1.35f);
            float forceCoupe = Mathf.Pow(Mathf.Max(0f, forceImpact), 0.72f) * 0.58f * finesseLame;
            if (mainActive.EstUnEclat && arbre.AgeEnJours <= 2)
                forceCoupe = Mathf.Max(forceCoupe, arbre.AgeEnJours <= 1 ? 36f : 48f);
            if (rochePlate && arbre.AgeEnJours <= 2)
            {
                // Early game: seule la roche plate aide à sortir du hard-lock bois sur jeunes arbres.
                forceCoupe *= 1.16f;
                forceCoupe = Mathf.Max(forceCoupe, arbre.AgeEnJours <= 1 ? 34f : 45f);
            }
            if (mainActive.ID == 106)
                forceCoupe *= 1.08f;
            else if (mainActive.ID == IdObjetHachePierreTier1)
                forceCoupe *= 2.16f;
            forceCoupe += ObtenirBonusDegatsArbreBucheron();

            bool hachetteBonneOrientation = mainActive.ID == IdObjetHachePierreTier1
                || (mainActive.ID == 106 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe));
            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, forceCoupe, epaisseurLame, hachetteBonneOrientation);
            if (resultatCoupe == 0) GD.Print("ZERO-K : Rebond. La force d'impact est insuffisante pour entamer ce bois.");
            else if (resultatCoupe == 1) JouerSonEtEffetCoupeArbre(pointImpact);
            else if (resultatCoupe == 2)
            {
                JouerSonEtEffetCoupeArbre(pointImpact);
                if (arbreJungle)
                {
                    int quantiteLianes = 2 + Mathf.Clamp(arbre.AgeEnJours / 2, 0, 5);
                    Vector3 baseDrop = pointImpact + Vector3.Up * 0.7f;
                    for (int i = 0; i < quantiteLianes; i++)
                    {
                        var slotLiane = new SlotInventaire { ID = 16, IndexChimique = 16, IndexMorphologique = 16, IndexTaille = 0, ScaleEclat = Vector3.One };
                        Vector3 offset = new Vector3(((float)GD.Randf() - 0.5f) * 0.8f, (float)GD.Randf() * 0.5f, ((float)GD.Randf() - 0.5f) * 0.8f);
                        Node3D lianeDrop = CreerBlocPose(baseDrop + offset, slotLiane);
                        if (lianeDrop is RigidBody3D rbLianeDrop)
                            rbLianeDrop.ApplyCentralImpulse(directionFrappe.Normalized() * 1.1f + Vector3.Up * 1.4f);
                    }
                }
                GD.Print("ZERO-K : Arbre abattu.");
                AjouterXpMetier("Bucheron", 1UL);
                if (_arbreCibleLiane == arbre)
                    ReinitialiserMinageLianeDagueProgression();
            }
            else if (resultatCoupe == 3)
            {
                JouerSonEtEffetCoupeArbre(pointImpact);
                bool brancheMorte = arbre.IndexBotanique == LSystem_Botanique.IndexCheneMort || arbre.IndexBotanique == LSystem_Botanique.IndexBouleauMort;
                GD.Print(brancheMorte
                    ? "ZERO-K : Branche morte amputée — branche au sol (essence conservée)."
                    : "ZERO-K : Branche amputée — branche au sol (essence conservée).");
            }

            if ((mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1) && resultatCoupe > 0)
            {
                float coutUsure = mainActive.ID == IdObjetHachePierreTier1 ? 0.8f : 2.0f;
                int idCasse = AppliquerUsureOutilMainActive(coutUsure);
                bool hachetteCassee = idCasse == 106;
                AjouterXpFutureState("Force", hachetteCassee ? 2UL : 1UL);
            }
            if (rochePlate && resultatCoupe > 0 && ObtenirNiveauFutureState("Force") < 15UL)
                AjouterXpFutureState("Force", 1UL);
            return;
        }

        BoeufSauvage boeufTouche = ObtenirBoeufDepuisCollider(objetTouche);
        if (boeufTouche != null)
        {
            if (boeufTouche.EstCadavreDepecable())
            {
                if (mainActive.ID == 105)
                    GD.Print("ZERO-K : Maintenez clic gauche 3s avec la dague pour dépiter ce cadavre.");
                else if (mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1)
                    GD.Print("ZERO-K : La hachette ne dépèce pas — maintenez clic gauche 3s avec la dague (105) sur la carcasse.");
                return;
            }
            bool etaitCadavreDepecable = boeufTouche.EstCadavreDepecable();

            bool tranchant = false;
            if (mainActive.ID == 105)
                tranchant = EstFrappeDagueAvecLaLame(pointImpact, directionFrappe);
            else if (mainActive.ID == IdObjetFauxPierreTier0)
                tranchant = EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe);
            else if (mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetPellePierreTier0)
                tranchant = EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
            else if (mainActive.ID == IdObjetLancePierreTier0)
                tranchant = EstFrappeLance111AvecLaPointe(pointImpact, directionFrappe);
            else if (mainActive.EstUnEclat)
                tranchant = efficaciteHache > 0.45f;
            else if (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && mainActive.IndexMorphologique == 3)
                tranchant = true;

            Vector3 dirAvantCamera = _camera != null ? -_camera.GlobalTransform.Basis.Z.Normalized() : directionFrappe.Normalized();
            float alignPointee = Mathf.Clamp(directionFrappe.Normalized().Dot(dirAvantCamera), -1f, 1f);
            bool perforant = tranchant && alignPointee > 0.68f && (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetLancePierreTier0 || mainActive.ID == 100 || mainActive.EstUnEclat);
            string nomZone = NomZoneDepuisColliderRaycast(_rayon.GetCollider());

            float materiau = MultiplicateurMateriauArmeContreFaune(mainActive);
            float baseDegats = CalculerIntensiteImpactPhysique(
                masseOutil * multiplicateurForce,
                force,
                mouvement,
                directionFrappe,
                normaleImpact,
                CoefficientContactImpact(tranchant, perforant),
                materiau,
                CoefficientZoneBovin(nomZone));
            baseDegats = Mathf.Clamp(baseDegats, 0.05f, 120f);
            baseDegats *= multiplicateurDegatsBras;

            bool applique = boeufTouche.RecevoirImpactCombat(
                baseDegats,
                pointImpact,
                directionFrappe,
                tranchant,
                perforant,
                nomZone,
                (ulong)GetInstanceId());

            if (applique)
                AjouterXpFutureState("Force", 2UL);
            if (applique && !etaitCadavreDepecable && boeufTouche.EstCadavreDepecable())
                AjouterXpMetier("Chasseur", 1UL);
            if (applique && (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetLancePierreTier0 || mainActive.ID == 100))
            {
                float coutUsure = 0.85f + (baseDegats * 0.024f);
                if (mainActive.ID == IdObjetHachePierreTier1)
                    coutUsure *= 0.4f;
                AppliquerUsureOutilMainActive(coutUsure);
            }
            return;
        }

        RigidBody3D rbCible = ResoudreRigidBodyDepuisCollider(objetTouche);
        if (rbCible == null) return;

        if (rbCible.Name.ToString().Contains("BrancheMorte"))
        {
            var mainB = MainGaucheEstActive ? MainGauche : MainDroite;
            bool fauxSurBrancheMorte = mainB.ID == IdObjetFauxPierreTier0 && EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe);
            bool outilTranchantPourArbre = mainB.ID == 106
                || mainB.ID == IdObjetHachePierreTier1
                || mainB.EstUnEclat
                || EstRocheTranchantePourBois(mainB)
                || fauxSurBrancheMorte;
            if (!outilTranchantPourArbre) return;
            JouerSonEtEffetCoupeArbre(pointImpact);
            byte essenceBranche = LireIndexBotaniqueBoisSurRigid(rbCible);
            byte essenceBaton = mainB.ID == IdObjetFauxPierreTier0 ? LSystem_Botanique.IndexChene : essenceBranche;
            var slotBatonStandard = new SlotInventaire
            {
                ID = 32,
                IndexBotanique = essenceBaton,
                IndexMorphologique = 0,
                IndexTaille = 1,
                ScaleEclat = Vector3.One
            };
            Vector3 posBaton = CalculerPointAuDessusSol(pointImpact + directionFrappe * 0.12f + Vector3.Up * 0.65f, 0.18f);
            Node3D baton = CreerBlocPose(posBaton, slotBatonStandard);
            if (baton is RigidBody3D rbBaton)
                rbBaton.ApplyCentralImpulse(directionFrappe.Normalized() * 1.35f + Vector3.Up * 0.65f);
            rbCible.QueueFree();
            GD.Print("ZERO-K : Branche tombée transformée en bâton standard.");
            return;
        }

        var item = rbCible as ItemPhysique ?? rbCible.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (item == null)
        {
            rbCible.ApplyCentralImpulse(directionFrappe * (4f * force));
            GD.Print($"ZERO-K : Frappe sur « {rbCible.Name} » (corps rigide non outillé) — impulsion seule.");
            return;
        }

        Vector3 dirFrappeObj = -_rayon.GetCollisionNormal();
        float impulsionFrappe = 4f * force * (1f + rbCible.Mass * 0.5f);

        if (item.ID_Objet == 30 || item.ID_Objet == 32)
        {
            // Post-abattage : tronc/bûche/bâton au sol — uniquement hachette (la roche plate sert aux jeunes arbres vivants, pas au débitage).
            if (mainActive.ID != 106 && mainActive.ID != IdObjetHachePierreTier1)
            {
                AlerteSqueletteBoiteNoire("Il faut une hachette pour standardiser ou fendre le bois au sol.");
                rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);
                return;
            }
            bool coupeNette = EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
            if (!coupeNette)
            {
                // Tolérance gameplay: avec la hachette on peut continuer à travailler le bois, mais plus lentement.
                AlerteSqueletteBoiteNoire("Coup manche/plat: la coupe progresse, mais plus lentement.");
                impulsionFrappe *= 0.8f;
            }

            Vector3 axeBois = rbCible.GlobalTransform.Basis.Z.Normalized();
            float alignement = Mathf.Abs(directionFrappe.Normalized().Dot(axeBois));
            AppliquerUsureOutilMainActive(mainActive.ID == IdObjetHachePierreTier1 ? 1.0f : 2.5f);

            if (alignement < 0.5f)
            {
                // COUPE TRANSVERSALE (Sur la largeur)
                // Bâtons / branches (32) : toujours couper la longueur en deux (demi puis quart, etc.) — pas la logique tronc 1,2 m.
                if (item.ID_Objet == 32)
                {
                    int tStick = Mathf.Clamp(item.IndexTailleRoche, 0, 4);
                    CalculerDimensionsBoisPose(32, item.IndexCacheMemoire, tStick, out _, out float baseLenStick, out _, out _);
                    float scaleZStick = item.HasMeta("ScaleLongueurBois")
                        ? (float)item.GetMeta("ScaleLongueurBois").AsSingle()
                        : (item.Scale.Z > 0.1f ? item.Scale.Z : 1f);
                    float longueurM = baseLenStick * scaleZStick;
                    const float longueurMinPiece = 0.07f;
                    if (longueurM * 0.5f < longueurMinPiece)
                    {
                        GD.Print("ZERO-K : Ce bâton est déjà trop court pour être coupé en deux à la hache.");
                        rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe * 0.5f);
                        return;
                    }
                    float nouveauScaleZ = scaleZStick * 0.5f;
                    Vector3 sep = directionFrappe.Normalized();
                    if (sep.LengthSquared() < 1e-6f) sep = rbCible.GlobalTransform.Basis.X;
                    var moitie = new SlotInventaire
                    {
                        ID = 32,
                        IndexBotanique = item.IndexBotanique,
                        IndexChimique = item.IndexChimique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexTaille = tStick,
                        ScaleEclat = new Vector3(1f, 1f, nouveauScaleZ),
                        EstUnEclat = false
                    };
                    Vector3 baseElevB = rbCible.GlobalPosition + Vector3.Up * 0.22f;
                    Node3D pb1 = CreerBlocPose(baseElevB + sep * 0.14f + axeBois * 0.05f, moitie);
                    Node3D pb2 = CreerBlocPose(baseElevB - sep * 0.14f - axeBois * 0.05f, moitie);
                    if (pb1 != null) pb1.GlobalRotation = rbCible.GlobalRotation;
                    if (pb2 != null) pb2.GlobalRotation = rbCible.GlobalRotation;
                    string msg = nouveauScaleZ < 0.34f
                        ? "ZERO-K : Vous partagez le bâton en quarts (longueur)."
                        : "ZERO-K : Vous coupez le bâton en deux (demi-longueur).";
                    GD.Print(msg);
                    AjouterXpFutureState("Force", 1UL);
                    rbCible.QueueFree();
                    return;
                }

                float scaleZActuel = item.HasMeta("ScaleLongueurBois")
                    ? (float)item.GetMeta("ScaleLongueurBois").AsSingle()
                    : (item.Scale.Z > 0.1f ? item.Scale.Z : 1f);
                float vraieLongueur = (item.IndexTailleRoche == 0 ? 1.2f : 1.0f) * scaleZActuel;
                // axeBois déjà calculé juste avant alignement.

                // A) Débitage du Tronc Brut Géant (On tranche 1 mètre) — bûches (30) uniquement
                if (item.ID_Objet == 30 && item.IndexTailleRoche == 0 && vraieLongueur > 1.4f)
                {
                    float longueurRestante = Mathf.Max(0f, vraieLongueur - 1.0f);
                    int nbStandardsTotal = Mathf.Max(1, Mathf.FloorToInt(vraieLongueur / 1.0f));
                    GD.Print($"ZERO-K : Vous tranchez une Bûche Standard ({nbStandardsTotal} standards possibles). Reste du tronc : {longueurRestante:F1}m.");

                    var slotStandard = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = 1,
                        ScaleEclat = Vector3.One
                    };
                    var slotReste = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = 0,
                        // ScaleEclat.Z est un multiplicateur de la base 1.2m (pas une longueur en mètres).
                        ScaleEclat = new Vector3(1, 1, longueurRestante / 1.2f)
                    };

                    Vector3 centreCible = rbCible.GlobalPosition;
                    Vector3 lift = Vector3.Up * 0.35f;
                    Node3D pStandard = CreerBlocPose(centreCible + axeBois * (vraieLongueur * 0.4f) + lift, slotStandard);
                    Node3D pReste = CreerBlocPose(centreCible - axeBois * 0.5f + lift, slotReste);

                    if (pStandard != null) pStandard.GlobalRotation = rbCible.GlobalRotation;
                    if (pReste != null) pReste.GlobalRotation = rbCible.GlobalRotation;
                }
                // B) Le Tronc Brut est court (<= 1.4m), il devient Standard. — bûche (30) uniquement
                else if (item.ID_Objet == 30 && item.IndexTailleRoche == 0)
                {
                    GD.Print("ZERO-K : Le bout du tronc devient une Bûche Standard pure.");
                    var slotStandard = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = 1,
                        ScaleEclat = Vector3.One
                    };
                    Node3D p = CreerBlocPose(rbCible.GlobalPosition + Vector3.Up * 0.35f, slotStandard);
                    if (p != null) p.GlobalRotation = rbCible.GlobalRotation;
                }
                // C) Logique classique pour les Bûches (Standard -> Courte -> Rondin) — bûche (30) uniquement
                else if (item.ID_Objet == 30)
                {
                    if (item.IndexTailleRoche >= 3)
                    {
                        GD.Print("ZERO-K : Ce bois est déjà trop court.");
                        return;
                    }
                    int nouvelleTaille = item.IndexTailleRoche + 1;
                    GD.Print($"ZERO-K : Coupe Transversale. Raccourcissement à l'étape {nouvelleTaille}.");
                    var boisRaccourci = new SlotInventaire
                    {
                        ID = item.ID_Objet,
                        IndexBotanique = item.IndexBotanique,
                        IndexMorphologique = item.IndexCacheMemoire,
                        IndexChimique = item.IndexChimique,
                        IndexTaille = nouvelleTaille,
                        ScaleEclat = Vector3.One,
                        EstUnEclat = false
                    };
                    Vector3 baseElev = rbCible.GlobalPosition + Vector3.Up * 0.4f;
                    Node3D piece1 = CreerBlocPose(baseElev + directionFrappe * 0.15f, boisRaccourci);
                    Node3D piece2 = CreerBlocPose(baseElev - directionFrappe * 0.15f, boisRaccourci);
                    if (piece1 != null) piece1.GlobalRotation = rbCible.GlobalRotation;
                    if (piece2 != null) piece2.GlobalRotation = rbCible.GlobalRotation;
                }
            }
            else
            {
                if (item.IndexTailleRoche == 0)
                {
                    if (item.ID_Objet == 30)
                    {
                        GD.Print("ZERO-K : Tronc Brut. Coupez-le sur la largeur d'abord pour le standardiser.");
                        rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe * 0.5f);
                        return;
                    }
                    if (item.ID_Objet == 32)
                    {
                        float szLong = item.HasMeta("ScaleLongueurBois")
                            ? (float)item.GetMeta("ScaleLongueurBois").AsSingle()
                            : (item.Scale.Z > 0.1f ? item.Scale.Z : 1f);
                        if (szLong >= 0.995f)
                        {
                            GD.Print("ZERO-K : Branche entière : coupez d’abord en travers (tranchant perpendiculaire à la longueur du bois) pour obtenir des demi-bâtons, puis recommencez pour des quarts.");
                            rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe * 0.5f);
                            return;
                        }
                    }
                }
                int fenteActuelle = MorphologieBoisDepuisItem(item);
                if (fenteActuelle >= 3)
                {
                    GD.Print("ZERO-K : Bois réduit à son épaisseur minimale (Planchette).");
                    rbCible.QueueFree();
                    return;
                }
                int nouvelleFente = fenteActuelle + 1;
                GD.Print($"ZERO-K : Coupe Longitudinale. Fente à l'étape {nouvelleFente}.");
                float scaleLongueurFente = 1f;
                if (item.ID_Objet == 32)
                {
                    scaleLongueurFente = item.HasMeta("ScaleLongueurBois")
                        ? (float)item.GetMeta("ScaleLongueurBois").AsSingle()
                        : (item.Scale.Z > 0.1f ? item.Scale.Z : 1f);
                }
                var boisFendu = new SlotInventaire
                {
                    ID = item.ID_Objet,
                    IndexBotanique = item.IndexBotanique,
                    IndexMorphologique = nouvelleFente,
                    IndexChimique = item.IndexChimique,
                    IndexTaille = item.IndexTailleRoche,
                    ScaleEclat = item.ID_Objet == 32 ? new Vector3(1f, 1f, scaleLongueurFente) : Vector3.One,
                    EstUnEclat = false
                };
                Vector3 baseElevLong = rbCible.GlobalPosition + Vector3.Up * 0.4f;
                Node3D b1 = CreerBlocPose(baseElevLong + Vector3.Right * 0.1f, boisFendu);
                Node3D b2 = CreerBlocPose(baseElevLong + Vector3.Left * 0.1f, boisFendu);
                if (b1 != null) b1.GlobalRotation = rbCible.GlobalRotation;
                if (b2 != null) b2.GlobalRotation = rbCible.GlobalRotation;
            }
            AjouterXpFutureState("Force", 1UL);
            rbCible.QueueFree();
            return;
        }

        rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);

        if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetLancePierreTier0) && ItemPhysique.EstIdRocheMatiere(item.ID_Objet))
        {
            bool tranchantOk = mainActive.ID == 105
                ? EstFrappeDagueAvecLaLame(pointImpact, directionFrappe)
                : (mainActive.ID == IdObjetFauxPierreTier0
                ? EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe)
                : (mainActive.ID == IdObjetLancePierreTier0
                ? EstFrappeLance111AvecLaPointe(pointImpact, directionFrappe)
                : EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe)));
            if (tranchantOk)
                GD.Print("ZERO-K : L’outil ne peut pas briser cette roche — trop léger. Il faut un choc contondant ou une pierre lancée.");
            else
                AlerteSqueletteBoiteNoire("Tu heurtes la pierre avec le manche ou le plat, sans effet de taille.");
            return;
        }

        if ((mainActive.ID == 105 && !EstFrappeDagueAvecLaLame(pointImpact, directionFrappe))
            || (mainActive.ID == IdObjetFauxPierreTier0 && !EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe))
            || ((mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0) && !EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            || (mainActive.ID == IdObjetLancePierreTier0 && !EstFrappeLance111AvecLaPointe(pointImpact, directionFrappe)))
        {
            AlerteSqueletteBoiteNoire("Oriente le tranchant vers la cible: ce coup porte le manche ou le plat.");
            return;
        }

        Vector3 dirVue = (pointImpact - _camera.GlobalPosition).Normalized();
        int resultatFracture = item.SubirDegats(forceImpact, dirVue, pointImpact);
        if (resultatFracture == 0)
            GD.Print("ZERO-K : L'impact n'est pas assez puissant. La roche résonne mais ne cède pas (Rebond).");
        else if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetLancePierreTier0)
        {
            float coutUsure = 2.15f + forceImpact * 0.017f;
            if (mainActive.ID == IdObjetHachePierreTier1)
                coutUsure *= 0.4f;
            AppliquerUsureOutilMainActive(coutUsure);
        }
    }

    /// <summary>Rayon vertical sur le masque collision 1 ; place un point au-dessus du sol (évite tronc/branches sous le terrain).</summary>
    private void ExecuterLootDepecageCadavreBoeuf(BoeufSauvage boeuf, Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (boeuf == null || !GodotObject.IsInstanceValid(boeuf) || !boeuf.EstCadavreDepecable())
            return;
        Texture2D texPeau = boeuf.EssayerObtenirTexturePeauPourCuir();
        string genomeCuir = boeuf.ConstruireGenomePeauPourSlotCuir(texPeau);
        Vector3 basePos = boeuf.GlobalPosition + Vector3.Up * 0.28f;
        Vector3 dir = directionFrappe.LengthSquared() > 1e-6f ? directionFrappe.Normalized() : -GlobalTransform.Basis.Z.Normalized();
        Vector3 orth = Vector3.Up.Cross(dir);
        if (orth.LengthSquared() < 1e-4f)
            orth = GlobalTransform.Basis.X;
        orth = orth.Normalized();

        for (int i = 0; i < 3; i++)
        {
            var slotSteak = new SlotInventaire { ID = IdObjetSteakCru, Quantite = 1, IndexChimique = 0, IndexMorphologique = 0, IndexTaille = 0 };
            Vector3 off = orth * (0.14f * (i - 1)) + Vector3.Up * (0.03f * i);
            Node3D n = CreerBlocPose(basePos + off + dir * 0.18f, slotSteak);
            if (n is RigidBody3D rb)
                rb.ApplyCentralImpulse(dir * 0.55f + Vector3.Up * 0.38f + orth * 0.12f * (i - 1));
        }

        var slotOs = new SlotInventaire { ID = IdObjetOsBoeuf, Quantite = 10, IndexChimique = 0, IndexMorphologique = 0, IndexTaille = 0 };
        Node3D nOs = CreerBlocPose(basePos + dir * 0.38f, slotOs);
        if (nOs is RigidBody3D rbOs)
            rbOs.ApplyCentralImpulse(dir * 0.48f + Vector3.Up * 0.32f);

        var slotCuir = new SlotInventaire
        {
            ID = IdObjetCuirBoeuf,
            Quantite = 2,
            IndexChimique = 0,
            IndexMorphologique = 0,
            IndexTaille = 0,
            GenomeAssemblage = genomeCuir
        };
        Node3D nCuir = CreerBlocPose(basePos - orth * 0.2f + dir * 0.24f, slotCuir);
        if (nCuir is ItemPhysique ipC && !string.IsNullOrEmpty(genomeCuir))
        {
            ipC.GenomeAssemblage = genomeCuir;
            ipC.SetMeta(MetaGenomeAssemblage, genomeCuir);
        }
        if (nCuir is RigidBody3D rbC)
            rbC.ApplyCentralImpulse(-orth * 0.22f + Vector3.Up * 0.3f + dir * 0.22f);

        var slotIntestin = new SlotInventaire
        {
            ID = IdObjetIntestinBoeuf,
            Quantite = 2,
            IndexChimique = 0,
            IndexMorphologique = 0,
            IndexTaille = 0
        };
        Node3D nIntestin = CreerBlocPose(basePos + orth * 0.2f + dir * 0.12f, slotIntestin);
        if (nIntestin is RigidBody3D rbI)
            rbI.ApplyCentralImpulse(orth * 0.26f + Vector3.Up * 0.34f + dir * 0.18f);

        boeuf.FinaliserCadavreApresDepecage();
        AjouterXpMetier("Boucher", 1UL);
        GD.Print("ZERO-K : Viande, os, cuir et intestins récupérés sur la carcasse.");
    }

    private Vector3 CalculerPointAuDessusSol(Vector3 reference, float clearanceSelonNormale)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        Vector3 haut = reference + Vector3.Up * 16f;
        Vector3 bas = reference - Vector3.Up * 48f;
        if (space == null)
            return reference + Vector3.Up * Mathf.Max(0.35f, clearanceSelonNormale);
        var q = PhysicsRayQueryParameters3D.Create(haut, bas);
        q.CollisionMask = 1;
        var hit = space.IntersectRay(q);
        if (hit.Count > 0 && hit.ContainsKey("position"))
        {
            Vector3 p = hit["position"].AsVector3();
            Vector3 n = hit.ContainsKey("normal") ? hit["normal"].AsVector3().Normalized() : Vector3.Up;
            return p + n * Mathf.Max(0.08f, clearanceSelonNormale);
        }
        return reference + Vector3.Up * Mathf.Max(0.4f, clearanceSelonNormale);
    }
}
