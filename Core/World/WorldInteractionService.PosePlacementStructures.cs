using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>Placement (construction ou rejet d'objet). Clic droit.</summary>
    private void ExecuterPlacement()
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide)
        {
            GD.Print("ZERO-K : La main sélectionnée est vide. Impossible de poser.");
            return;
        }
        ExecuterPlacementAvecOptions(mainActive, depuisInteragir: false);
    }

    private bool ExecuterPlacementModeGhostLancer(SlotInventaire mainActive)
    {
        if (mainActive.EstVide || !EstObjetLancableAuMaintien(mainActive))
            return false;

        if (!EssayerCalculerApercuPlacementObjetLancable(
            mainActive,
            out Vector3 pointDeChute,
            out Vector3 pointAligne,
            out Vector3 rotationDeg,
            out bool poseValide))
            return false;
        if (!poseValide)
            return false;

        Node3D nePose = CreerBlocPose(pointDeChute, mainActive);
        if (nePose == null)
            return false;
        ForcerQuantiteObjetPoseUnitaireSiItemPhysique(nePose);

        AppliquerTransformPoseStructure(nePose, pointAligne, rotationDeg);
        AjusterPoseObjetLancableAuSol(nePose);
        ConsommerUneUniteMainActive();
        ReinitialiserRotationManuelle();
        RafraichirHUD();

        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
        return true;
    }

    private void ForcerQuantiteObjetPoseUnitaireSiItemPhysique(Node3D noeudPose)
    {
        if (noeudPose is ItemPhysique itemPose)
            itemPose.SetMeta(MetaQuantiteObjetPose, 1);
    }

    private void ExecuterPlacementAvecOptions(SlotInventaire mainActive, bool depuisInteragir)
    {
        if (mainActive.EstVide) return;
        if (ItemPhysique.EstPinceOsPorteObjet(mainActive)
            && EssayerObtenirPinceOsEnMain(out bool mainGauchePince)
            && EssayerDeposerChargePinceEnMain(mainGauchePince))
        {
            RafraichirHUD();
            ReinitialiserRotationManuelle();
            return;
        }
        if (ItemPhysique.EstPinceOsPorteObjet(mainActive))
            return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;

        if (EssayerAjouterCombustiblePitFeuRocheSousVisee(ref mainActive))
            return;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeChute;
        Vector3 pointAligneStructure = Vector3.Zero;
        Vector3 rotationStructureDeg = Vector3.Zero;
        bool structureFixe = EstStructureFixePose(mainActive.ID);

        if (structureFixe)
        {
            if (!EssayerCalculerPoseStructureFixe(mainActive, depuisInteragir, out pointDeChute, out pointAligneStructure, out rotationStructureDeg, out bool poseStructureValide))
                return;
            if (!poseStructureValide)
                return;
        }
        else
        {
            float decalNormale = 0.1f;
            pointDeChute = pointImpact + (normaleImpact * decalNormale);
        }
        float distance = GlobalPosition.DistanceTo(pointDeChute);
        // Flexible / corde avec E : on peut poser près du corps (manipulation fine) ; clic droit garde la marge anti-auto-collision
        bool flexOuCordeE = depuisInteragir && (EstMatiereFlexible(mainActive.ID) || mainActive.ID == 20 || mainActive.ID == 21);
        // Atelier : marge courte pour poser sous la visée (évite un rejet silencieux puis une pose « ailleurs »).
        // Clic droit + objet lançable : même ordre de marge que l'atelier — pose au sol près des pieds / exactement sous le clic.
        float distMin = flexOuCordeE ? 0.35f : (structureFixe ? 0.55f : 1.4f);
        if (!depuisInteragir && EstObjetLancableAuMaintien(mainActive))
            distMin = Mathf.Min(distMin, 0.55f);
        // Four / pit à feu : sol direct, preview à 0,25 m — même marge à la pose réelle.
        if (EstIdFourTorchie(mainActive.ID) || EstIdPitFeu(mainActive.ID))
            distMin = Mathf.Min(distMin, 0.25f);
        if (distance < distMin) return;

        int id = mainActive.ID;
        if (id == 0) return;
        if (EstSlotTerrainVoxelPosable(mainActive))
        {
            int idVoxel = ResoudreIdVoxelPose(mainActive);
            _gestionnaireMonde?.AppliquerCreationGlobale(pointImpact, normaleImpact, RAYON_SCULPTURE, idVoxel);
        }
		else if (id == 10 || id == 11 || id == IdObjetAloeVera)
		{
			Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
			if (!EstSolViseParRayon(_rayon, noeudCol))
			{
				GD.Print("ZERO-K : Replantation buisson impossible hors sol.");
				return;
			}
			byte typeBuisson;
			if (id == IdObjetAloeVera)
				typeBuisson = Chunk_Serveur.ConstruireTypeBuisson(Chunk_Serveur.VarianteBuissonAloeVera, plein: false);
			else
			{
				byte varianteCouleur = (byte)Mathf.Clamp(mainActive.IndexChimique, 0, 120);
				typeBuisson = Chunk_Serveur.ConstruireTypeBuisson(varianteCouleur, plein: id == 10);
			}
			bool plante = _gestionnaireMonde?.PlanterBuissonGlobal(pointImpact, normaleImpact, typeBuisson) ?? false;
			if (!plante)
			{
				GD.Print("ZERO-K : Sol non valide pour replanter ce buisson (terre plate requise).");
				return;
			}
			GD.Print("ZERO-K : Buisson replanté.");
		}
        else if (id == 999 || id == BlocChutant.ID_BRANCHE || id == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0 || id == IdObjetCarnetSavoir || id == 30 || id == 32 || id == 34 || id == 105 || id == 106 || id == IdObjetHachePierreTier1 || id == IdObjetAtelleJambe || id == IdObjetAtelleBras || id == IdObjetBandageTier1 || id == IdObjetPellePierreTier0 || id == IdObjetPiochePierreTier0 || id == IdObjetLancePierreTier0 || id == IdObjetFauxPierreTier0 || id == IdObjetAllumeFeu || id == IdObjetFenetreBois || id == 200 || id == IdObjetTableBoisDecorative || id == IdObjetTableArtisanaTier1 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0 || EstIdPitFeu(id) || EstIdFourTorchie(id) || EstIdFondation(id) || EstIdPlancher(id) || EstIdMuret(id) || EstIdMurBois(id) || EstIdPorteBois(id) || EstIdToitChaume(id) || EstIdTorche(id))
        {
            Vector3 pointSpawn = (structureFixe && (mainActive.ID == IdObjetTableBoisDecorative || mainActive.ID == IdObjetTableArtisanaTier1 || EstIdFondation(mainActive.ID) || EstIdPlancher(mainActive.ID) || EstIdMuret(mainActive.ID) || EstIdMurBois(mainActive.ID) || EstIdPorteBois(mainActive.ID) || EstIdToitChaume(mainActive.ID) || EstIdTorche(mainActive.ID)))
                ? pointAligneStructure
                : pointDeChute;
            Node3D nePose = CreerBlocPose(pointSpawn, mainActive);
            if (nePose == null)
                return;
            if (structureFixe)
                AppliquerTransformPoseStructure(nePose, pointAligneStructure, rotationStructureDeg);
            if (EstIdPorteBois(id) && nePose is ItemPhysique portePosee)
            {
                float baseYaw = Mathf.PosMod(rotationStructureDeg.Y, 360f);
                Vector3 baseP = portePosee.GlobalPosition;
                portePosee.SetMeta("Porte_BaseYaw", baseYaw);
                portePosee.SetMeta("Porte_BasePos", baseP);
                portePosee.SetMeta("Porte_AngleOuverture", 90f);
                portePosee.SetMeta("Porte_Charniere", -1);
                portePosee.SetMeta("Porte_Ouverte", false);
            }
            if (EstIdToitChaume(id))
                CallDeferred(nameof(RecalculerAssemblageToitsChaumeGlobal));
            // Clic droit rapide : un objet lançable doit se déposer au sol sans mini-impulsion.
            // La poussée douce reste utile pour les poses via touche Interagir.
            bool estLancable = EstObjetLancableAuMaintien(mainActive);
            if (estLancable)
            {
                ForcerQuantiteObjetPoseUnitaireSiItemPhysique(nePose);
                if (!structureFixe)
                    AjusterPoseObjetLancableAuSol(nePose);
            }
            bool appliquerImpulsionPose = !structureFixe && (depuisInteragir || !estLancable);
            if (appliquerImpulsionPose)
                AppliquerImpulsionLacherDoux(nePose);
        }

        else
        {
            GD.Print($"ZERO-K : Matière {id} non géologique. Pose ignorée.");
            return;
        }

        ConsommerUneUniteMainActive();

        if (!structureFixe)
            ReinitialiserRotationManuelle();
        RafraichirHUD();

        // Persistance immédiate : évite de perdre tables / racks / blocs si crash ou fermeture avant autosauvegarde.
        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
    }

    private bool EssayerCalculerPoseStructureFixe(
        SlotInventaire mainActive,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;

        if (mainActive.EstVide || !EstStructureFixePose(mainActive.ID))
            return false;

        if (EstIdPlancher(mainActive.ID))
            return EssayerCalculerPosePlancher(mainActive.ID, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
        if (EstIdMuret(mainActive.ID))
            return EssayerCalculerPoseMuretBois(mainActive.ID, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
        if (EstIdMurBois(mainActive.ID))
            return EssayerCalculerPoseMurBois(mainActive.ID, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
        if (EstIdPorteBois(mainActive.ID))
            return EssayerCalculerPosePorteBois(mainActive.ID, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
        if (EstIdToitChaume(mainActive.ID))
            return EssayerCalculerPoseToitChaume(mainActive.ID, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
        if (EstIdTorche(mainActive.ID))
            return EssayerCalculerPoseTorche(mainActive.ID, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
        if (EstIdFourTorchie(mainActive.ID))
        {
            Node noeudColFour = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            if (!EstSolViseParRayon(_rayon, noeudColFour))
            {
                if (noeudColFour != null && EstNoeudSupportStructure(noeudColFour))
                    GD.Print("SEROKA : Le four en torchie doit être posé sur le sol du monde (voxel/herbe), pas sur un plancher ou une structure.");
                else
                    GD.Print("SEROKA : Posez le four en torchie directement sur le sol (pas sur une structure).");
                return false;
            }
            pointDeChute = _rayon.GetCollisionPoint();
            pointAligne = pointDeChute;
            rotationDeg = CalculerRotationStructureFixe(mainActive.ID);
            if (!EssayerAjusterStructureSansChevauchement(mainActive.ID, ref pointDeChute, ref pointAligne))
            {
                GD.Print("SEROKA : Espace insuffisant pour poser le four en torchie ici.");
                return false;
            }
            float distanceFour = GlobalPosition.DistanceTo(pointDeChute);
            float distMinFour = 0.25f;
            poseValide = distanceFour >= distMinFour;
            return true;
        }

        Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSurfaceSupportStructureVisee(_rayon, noeudCol))
        {
            GD.Print("ZERO-K : Posez cette structure sur une surface horizontale (sol, fondation ou meuble de structure).");
            return false;
        }

        // Plus de fauchage automatique a la pose de l'atelier :
        // cela faisait apparaitre des fibres loin du visuel reel de l'herbe.
        // FIX CRITIQUE : On supprime la lecture du voxel hSurf + 1f.
        // L'objet se pose EXACTEMENT sur le point du raycast, ancré par son pivot.
        pointDeChute = _rayon.GetCollisionPoint();
        ItemPhysique structureSupport = ResoudreStructureSupportDepuisNoeud(noeudCol);
        if (structureSupport != null && !EstIdFondation(mainActive.ID))
        {
            float ySommetSupport = structureSupport.GlobalPosition.Y + ObtenirDimensionsApproxStructurePose(structureSupport.ID_Objet).Y;
            float yMinimalPose = ySommetSupport + MargeEmpilementStructureMetres;
            if (pointDeChute.Y < yMinimalPose)
                pointDeChute = new Vector3(pointDeChute.X, yMinimalPose, pointDeChute.Z);
        }
        if (EstIdFondation(mainActive.ID))
        {
            ItemPhysique? fondationReference = null;
            if (structureSupport != null && EstIdFondation(structureSupport.ID_Objet))
                fondationReference = structureSupport;
            else if (EssayerResoudreFondationHoteEmpilement(pointDeChute, out ItemPhysique hote, out Vector3 xzCentre))
            {
                fondationReference = hote;
                pointDeChute.X = xzCentre.X;
                pointDeChute.Z = xzCentre.Z;
            }
            float yPose = CalculerYPoseFondation(pointDeChute.Y, fondationReference);
            pointDeChute = new Vector3(pointDeChute.X, yPose, pointDeChute.Z);
            pointAligne = CalculerPositionPoseFondation(pointDeChute, fondationReference != null);
        }
        else
            pointAligne = pointDeChute;
        rotationDeg = CalculerRotationStructureFixe(mainActive.ID);
        if (!EssayerAjusterStructureSansChevauchement(mainActive.ID, ref pointDeChute, ref pointAligne))
        {
            GD.Print("ZERO-K : Espace insuffisant autour de la cible (aucune position libre proche).");
            return false;
        }

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        bool flexOuCordeE = depuisInteragir && (EstMatiereFlexible(mainActive.ID) || mainActive.ID == 20 || mainActive.ID == 21);
        float distMin = flexOuCordeE ? 0.35f : 0.55f;
        if (!depuisInteragir && EstObjetLancableAuMaintien(mainActive))
            distMin = Mathf.Min(distMin, 0.55f);
        poseValide = distance >= distMin;
        return true;
    }

    private bool EstStructureFixePose(int idObjet)
    {
        return idObjet == 200
            || idObjet == IdObjetTableBoisDecorative
            || idObjet == IdObjetTableArtisanaTier1
            || idObjet == IdObjetTableAnalyseTier1
            || idObjet == IdObjetRackBatons
            || idObjet == IdObjetRackBuches
            || idObjet == IdObjetCoffreBoisTier0
            || EstIdPitFeu(idObjet)
            || EstIdFourTorchie(idObjet)
            || EstIdFondation(idObjet)
            || EstIdPlancher(idObjet)
            || EstIdMuret(idObjet)
            || EstIdMurBois(idObjet)
            || EstIdPorteBois(idObjet)
            || EstIdToitChaume(idObjet)
            || EstIdTorche(idObjet);
    }

    private bool EssayerCalculerPosePlancher(
        int idPlancher,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;

        if (!_rayon.IsColliding())
            return false;

        Vector3 pointVisée = _rayon.GetCollisionPoint();
        Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        ItemPhysique? candidatRaycast = ResoudreFondationHoteDepuisNoeud(noeudCol);
        ItemPhysique fondation = TrouverFondationPourPlancher(pointVisée, candidatRaycast);
        if (fondation == null)
        {
            GD.Print("ZERO-K : Posez le plancher sur le dessus d'une fondation.");
            return false;
        }

        if (FondationPossedeDejaPlancher(fondation.GlobalPosition))
        {
            GD.Print("ZERO-K : Cette fondation possède déjà un plancher.");
            return false;
        }

        float yPlateau = fondation.GlobalPosition.Y + HauteurApproxFondationMetres;
        pointDeChute = new Vector3(fondation.GlobalPosition.X, yPlateau + MargeEmpilementStructureMetres, fondation.GlobalPosition.Z);
        pointAligne = pointDeChute;
        rotationDeg = CalculerRotationStructureFixe(idPlancher);
        if (!EstPositionStructureLibre(idPlancher, pointDeChute, pointAligne, fondation.GlobalPosition.Y))
        {
            GD.Print("ZERO-K : Espace insuffisant pour poser le plancher ici.");
            return false;
        }

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        float distMin = depuisInteragir ? 0.35f : 0.55f;
        poseValide = distance >= distMin;
        return true;
    }

    private bool EssayerCalculerPoseMuretBois(
        int idMuret,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;

        if (!_rayon.IsColliding())
            return false;

        Vector3 pointVisee = _rayon.GetCollisionPoint();
        Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        Vector3 normale = _rayon.GetCollisionNormal();
        float rotationManuelleMuret = Mathf.PosMod(Mathf.Round(_rotationManuelleY / PasRotationMuretDegres) * PasRotationMuretDegres, 360f);
        bool autoriserFondation = _modeSnapMuretManuel == ModeSnapMuretAuto || _modeSnapMuretManuel == ModeSnapMuretFondation;
        bool autoriserMuret = _modeSnapMuretManuel == ModeSnapMuretAuto || _modeSnapMuretManuel == ModeSnapMuretMuret;
        bool autoriserTerrain = _modeSnapMuretManuel == ModeSnapMuretAuto || _modeSnapMuretManuel == ModeSnapMuretTerrain;

        // Priorité auto: fondation d'abord (plus stable pour compléter les 4 côtés),
        // puis muret->muret, puis terrain libre.
        ItemPhysique fondation = null;
        if (autoriserFondation)
        {
            fondation = ResoudreFondationHoteDepuisNoeud(noeudCol);
            if (fondation == null)
                fondation = TrouverFondationPourPlancher(pointVisee, null);
        }
        if (fondation != null)
        {
            Vector3 centreFondation = fondation.GlobalPosition;
            Vector3 local = pointVisee - centreFondation;
            bool axeX;
            bool signePositif;

            // Si on vise une face latérale, la normale donne le côté physique exact.
            if (Mathf.Abs(normale.Y) <= 0.65f)
            {
                axeX = Mathf.Abs(normale.X) >= Mathf.Abs(normale.Z);
                signePositif = axeX ? normale.X >= 0f : normale.Z >= 0f;
            }
            else
            {
                // Vise dessus / coin : choisir le côté de fondation le plus proche du point visé.
                float demi = FondationPasSnapMetres * 0.5f;
                float dPosX = Mathf.Abs(local.X - demi);
                float dNegX = Mathf.Abs(local.X + demi);
                float dPosZ = Mathf.Abs(local.Z - demi);
                float dNegZ = Mathf.Abs(local.Z + demi);
                float dMin = dPosX;
                axeX = true;
                signePositif = true;
                if (dNegX < dMin) { dMin = dNegX; axeX = true; signePositif = false; }
                if (dPosZ < dMin) { dMin = dPosZ; axeX = false; signePositif = true; }
                if (dNegZ < dMin) { axeX = false; signePositif = false; }
            }

            float offset = MuretOffsetCentreDepuisFondationMetres;
            Vector3 centreMuret = axeX
                ? new Vector3(centreFondation.X + (signePositif ? offset : -offset), centreFondation.Y, centreFondation.Z)
                : new Vector3(centreFondation.X, centreFondation.Y, centreFondation.Z + (signePositif ? offset : -offset));

            if (Mathf.Abs((axeX ? local.X : local.Z)) > MuretToleranceSnapFondationMetres)
            {
                GD.Print("ZERO-K : Visez plus près du bord de la fondation pour poser le muret.");
                return false;
            }

            float yPose = centreFondation.Y + MuretHauteurMetres * 0.5f + MargeEmpilementStructureMetres;
            pointDeChute = new Vector3(centreMuret.X, yPose, centreMuret.Z);
            pointAligne = pointDeChute;
            // Axe GLB muret: longueur déjà orientée comme un côté X de fondation.
            // -> côté X = 0°, côté Z = 90°.
            float rotationBase = axeX ? 0f : 90f;
            float rotationFinale = Mathf.PosMod(rotationBase + rotationManuelleMuret, 360f);
            rotationDeg = new Vector3(0f, rotationFinale, 0f);
        }
        else if (autoriserMuret)
        {
            ItemPhysique muretSupport = ResoudreMuretDepuisNoeud(noeudCol);
            if (muretSupport == null)
                muretSupport = TrouverMuretProchePourSnap(pointVisee);
            if (muretSupport != null)
            {
                Vector3 dimsSupport = ObtenirDimensionsApproxStructurePose(muretSupport.ID_Objet);
                bool supportEstMur = EstIdMurBois(muretSupport.ID_Objet);
                float rotSupport = Mathf.PosMod(Mathf.Round(muretSupport.GlobalRotationDegrees.Y / 90f) * 90f, 360f);
                float rad = rotSupport * Mathf.Pi / 180f;
                Vector3 dirLong = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 dirPerp = new Vector3(-dirLong.Z, 0f, dirLong.X);
                Vector3 local = pointVisee - muretSupport.GlobalPosition;

                float empriseSupport = Mathf.Max(dimsSupport.X, dimsSupport.Z);
                bool poseAuDessus = supportEstMur
                    || normale.Y >= 0.65f
                    || (Mathf.Abs(local.X) <= empriseSupport * 0.5f && Mathf.Abs(local.Z) <= empriseSupport * 0.5f && local.Y >= dimsSupport.Y * 0.35f);
                if (poseAuDessus)
                {
                    float yEmpile = muretSupport.GlobalPosition.Y + dimsSupport.Y * 0.5f + MuretHauteurMetres * 0.5f + MargeEmpilementStructureMetres;
                    pointDeChute = new Vector3(muretSupport.GlobalPosition.X, yEmpile, muretSupport.GlobalPosition.Z);
                    pointAligne = pointDeChute;
                    rotationDeg = new Vector3(0f, Mathf.PosMod(rotSupport + rotationManuelleMuret, 360f), 0f);
                }
                else
                {
                    float projLong = local.Dot(dirLong);
                    float projPerp = local.Dot(dirPerp);
                    bool snapSurCote = Mathf.Abs(projPerp) >= Mathf.Abs(projLong);
                    Vector3 centre;
                    if (snapSurCote)
                    {
                        float signe = projPerp >= 0f ? 1f : -1f;
                        centre = muretSupport.GlobalPosition + dirPerp * (MuretEpaisseurMetres - FondationPenetrationMetres) * signe;
                    }
                    else
                    {
                        float signe = projLong >= 0f ? 1f : -1f;
                        centre = muretSupport.GlobalPosition + dirLong * (MuretLongueurMetres - FondationPenetrationMetres) * signe;
                    }
                    pointDeChute = new Vector3(centre.X, muretSupport.GlobalPosition.Y, centre.Z);
                    pointAligne = pointDeChute;
                    rotationDeg = new Vector3(0f, Mathf.PosMod(rotSupport + rotationManuelleMuret, 360f), 0f);
                }
            }
            else if (autoriserTerrain)
            {
                float yTerrain = pointVisee.Y + MuretHauteurMetres * 0.5f + MargeEmpilementStructureMetres;
                pointDeChute = new Vector3(pointVisee.X, yTerrain, pointVisee.Z);
                pointAligne = pointDeChute;
                rotationDeg = new Vector3(0f, rotationManuelleMuret, 0f);
            }
            else
            {
                GD.Print("ZERO-K : Aucun muret support trouvé pour ce mode de snap.");
                return false;
            }
        }
        else if (autoriserTerrain)
        {
            // Mode terrain forcé.
            float yTerrain = pointVisee.Y + MuretHauteurMetres * 0.5f + MargeEmpilementStructureMetres;
            pointDeChute = new Vector3(pointVisee.X, yTerrain, pointVisee.Z);
            pointAligne = pointDeChute;
            rotationDeg = new Vector3(0f, rotationManuelleMuret, 0f);
        }
        else
        {
            GD.Print("ZERO-K : Ce mode de snap n'a trouvé aucun support valide.");
            return false;
        }

        if (MuretExisteDejaSurPosition(pointAligne))
        {
            GD.Print("ZERO-K : Un muret est déjà posé sur ce côté de fondation.");
            return false;
        }

        float? yBaseFondation = fondation != null ? fondation.GlobalPosition.Y : null;
        if (!EstPositionStructureLibre(idMuret, pointDeChute, pointAligne, yBaseFondation))
        {
            GD.Print("ZERO-K : Espace insuffisant pour poser le muret ici.");
            return false;
        }

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        float distMin = depuisInteragir ? 0.35f : 0.55f;
        poseValide = distance >= distMin;
        return true;
    }

    private bool EssayerCalculerPoseMurBois(
        int idMur,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (!_rayon.IsColliding())
            return false;

        Vector3 pointVisee = _rayon.GetCollisionPoint();
        Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        ItemPhysique support = ResoudreMurSupportDepuisNoeud(noeudCol);
        if (support == null)
            support = TrouverSupportMurProche(pointVisee);
        if (support == null)
        {
            GD.Print("ZERO-K : Posez le mur bois sur un muret.");
            return false;
        }

        Vector3 dimsSupport = ObtenirDimensionsApproxStructurePose(support.ID_Objet);
        float yBase = support.GlobalPosition.Y + (dimsSupport.Y * 0.5f) + (MurHauteurMetres * 0.5f) + MargeEmpilementStructureMetres;
        pointDeChute = new Vector3(support.GlobalPosition.X, yBase, support.GlobalPosition.Z);
        pointAligne = pointDeChute;

        float baseY = Mathf.PosMod(Mathf.Round(support.GlobalRotationDegrees.Y / 10f) * 10f, 360f);
        float rotManuelle = Mathf.PosMod(Mathf.Round(_rotationManuelleY / 10f) * 10f, 360f);
        rotationDeg = new Vector3(0f, Mathf.PosMod(baseY + rotManuelle, 360f), 0f);

        if (!EstPositionStructureLibre(idMur, pointDeChute, pointAligne))
        {
            GD.Print("ZERO-K : Espace insuffisant pour poser ce mur.");
            return false;
        }
        float distance = GlobalPosition.DistanceTo(pointDeChute);
        float distMin = depuisInteragir ? 0.35f : 0.55f;
        poseValide = distance >= distMin;
        return true;
    }

    private bool EssayerCalculerPosePorteBois(
        int idPorte,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (!_rayon.IsColliding())
            return false;

        Vector3 pointVisee = _rayon.GetCollisionPoint();
        Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        ItemPhysique cadre = ResoudreCadrePorteDepuisNoeud(noeudCol);
        if (cadre == null)
            cadre = TrouverCadrePorteProche(pointVisee);
        if (cadre == null)
        {
            GD.Print("ZERO-K : Posez la porte dans un mur cadre de porte.");
            return false;
        }

        if (CadrePossedeDejaPorte(cadre))
        {
            GD.Print("ZERO-K : Ce cadre de porte contient déjà une porte.");
            return false;
        }

        // Le cadre est un mur de 3 m centré sur son volume.
        // On aligne la base de la porte sur la base du cadre (souvent posé sur muret).
        float yBaseCadre = cadre.GlobalPosition.Y - (MurHauteurMetres * 0.5f);
        float yBase = yBaseCadre + (PorteHauteurMetres * 0.5f) + MargeEmpilementStructureMetres;
        pointDeChute = new Vector3(cadre.GlobalPosition.X, yBase, cadre.GlobalPosition.Z);
        pointAligne = pointDeChute;

        float baseY = Mathf.PosMod(Mathf.Round(cadre.GlobalRotationDegrees.Y / 10f) * 10f, 360f);
        float rotManuelle = Mathf.PosMod(Mathf.Round(_rotationManuelleY / 90f) * 90f, 360f);
        rotationDeg = new Vector3(0f, Mathf.PosMod(baseY + rotManuelle, 360f), 0f);

        if (!EstPositionStructureLibre(idPorte, pointDeChute, pointAligne))
        {
            GD.Print("ZERO-K : Espace insuffisant pour poser la porte dans ce cadre.");
            return false;
        }
        float distance = GlobalPosition.DistanceTo(pointDeChute);
        float distMin = depuisInteragir ? 0.35f : 0.55f;
        poseValide = distance >= distMin;
        return true;
    }

    private bool EssayerCalculerPoseToitChaume(
        int idToit,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (!_rayon.IsColliding())
            return false;

        Vector3 pointVisee = _rayon.GetCollisionPoint();
        Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        ItemPhysique support = ResoudreSupportToitDepuisNoeud(noeudCol);
        if (support == null)
            support = TrouverSupportToitProche(pointVisee);
        if (support == null)
        {
            GD.Print("ZERO-K : Posez le toit chaume sur une structure (mur, muret, plancher, fondation, toit).");
            return false;
        }

        float yCentre;
        if (EstIdToitChaume(support.ID_Objet))
            yCentre = support.GlobalPosition.Y;
        else
        {
            Vector3 dimsSupport = ObtenirDimensionsApproxStructurePose(support.ID_Objet);
            yCentre = support.GlobalPosition.Y + (dimsSupport.Y * 0.5f) + (ToitChaumeHauteurMetres * 0.5f) + MargeEmpilementStructureMetres;
        }
        yCentre += ToitChaumeDecalageHauteurMetres;

        Vector3 origineSnap = support.GlobalPosition;
        ItemPhysique fondationRef = TrouverFondationSousPoint(pointVisee);
        if (fondationRef == null && (EstIdMurBois(support.ID_Objet) || EstIdMuret(support.ID_Objet)))
            fondationRef = TrouverFondationSousPoint(support.GlobalPosition);
        if (fondationRef != null)
            origineSnap = fondationRef.GlobalPosition;

        float localX = pointVisee.X - origineSnap.X;
        float localZ = pointVisee.Z - origineSnap.Z;
        Vector3 centreSnap = new Vector3(
            origineSnap.X + Mathf.Round(localX / ToitChaumePasGrilleMetres) * ToitChaumePasGrilleMetres,
            yCentre,
            origineSnap.Z + Mathf.Round(localZ / ToitChaumePasGrilleMetres) * ToitChaumePasGrilleMetres);

        pointDeChute = centreSnap;
        pointAligne = centreSnap;

        // Le toit modulaire est orienté par la logique d'assemblage (solo/long/L/carré),
        // pas par le support, pour garder un rendu cohérent quand le voisinage change.
        rotationDeg = Vector3.Zero;

        if (!EstPositionStructureLibre(idToit, pointDeChute, pointAligne))
        {
            GD.Print("ZERO-K : Un toit est déjà présent à cet emplacement.");
            return false;
        }

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        float distMin = depuisInteragir ? 0.35f : 0.55f;
        poseValide = distance >= distMin;
        return true;
    }

    private bool EssayerCalculerPoseTorche(
        int idTorche,
        bool depuisInteragir,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (!_rayon.IsColliding())
            return false;

        Vector3 pointVisee = _rayon.GetCollisionPoint();
        Vector3 normale = _rayon.GetCollisionNormal().Normalized();
        bool poseMur = Mathf.Abs(normale.Y) < 0.65f;
        if (poseMur)
        {
            // On oriente la torche pour qu'elle sorte du mur (tête vers l'extérieur).
            float yaw = Mathf.RadToDeg(Mathf.Atan2(-normale.X, -normale.Z));
            float rotManuelleMur = Mathf.PosMod(Mathf.Round(_rotationManuelleY / 90f) * 90f, 360f);
            pointDeChute = pointVisee + normale * TorcheOffsetMurMetres + Vector3.Up * 0.12f;
            pointAligne = pointDeChute;
            rotationDeg = new Vector3(TorcheAngleMurDegres, Mathf.PosMod(yaw + rotManuelleMur, 360f), 0f);
        }
        else
        {
            // Le modèle torche est ancré à sa base: Y = sol + marge (pas +hauteur/2).
            float y = pointVisee.Y + MargeEmpilementStructureMetres;
            pointDeChute = new Vector3(pointVisee.X, y, pointVisee.Z);
            pointAligne = pointDeChute;
            rotationDeg = new Vector3(0f, Mathf.PosMod(Mathf.Round(_rotationManuelleY / 15f) * 15f, 360f), 0f);
        }

        if (!EstPositionStructureLibre(idTorche, pointDeChute, pointAligne))
        {
            GD.Print("ZERO-K : Espace insuffisant pour poser la torche ici.");
            return false;
        }

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        float distMin = depuisInteragir ? 0.35f : 0.55f;
        poseValide = distance >= distMin;
        return true;
    }

    private ItemPhysique ResoudreMurSupportDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses")
                && (EstIdMuret(item.ID_Objet) || EstIdMurBois(item.ID_Objet)))
                return item;
        }
        return null;
    }

    private ItemPhysique ResoudreSupportToitDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses")
                && (EstIdToitChaume(item.ID_Objet) || EstIdMurBois(item.ID_Objet) || EstIdMuret(item.ID_Objet) || EstIdPlancher(item.ID_Objet) || EstIdFondation(item.ID_Objet)))
                return item;
        }
        return null;
    }

    private ItemPhysique TrouverSupportToitProche(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip)
                continue;
            if (!(EstIdToitChaume(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet) || EstIdMuret(ip.ID_Objet) || EstIdPlancher(ip.ID_Objet) || EstIdFondation(ip.ID_Objet)))
                continue;
            Vector3 dims = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float emprise = Mathf.Max(dims.X, dims.Z) * 0.65f;
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > emprise || dz > emprise)
                continue;
            float yRef = EstIdToitChaume(ip.ID_Objet)
                ? ip.GlobalPosition.Y
                : (ip.GlobalPosition.Y + dims.Y * 0.5f);
            float dy = Mathf.Abs(yRef - worldPoint.Y);
            if (dy > 2.2f)
                continue;
            float score = dx * dx + dz * dz + dy * dy * 0.35f;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = ip;
            }
        }
        return meilleur;
    }

    private static Vector2I CleGrilleToitChaume(Vector3 positionMonde)
    {
        int gx = Mathf.RoundToInt(positionMonde.X / ToitChaumePasGrilleMetres);
        int gz = Mathf.RoundToInt(positionMonde.Z / ToitChaumePasGrilleMetres);
        return new Vector2I(gx, gz);
    }

    private static Node3D ObtenirNoeudMeshToit(ItemPhysique toit)
    {
        foreach (Node enfant in toit.GetChildren())
        {
            if (enfant is Node3D n3 && enfant.Name == "MeshInstance3D")
                return n3;
        }
        return null;
    }

    private static void SupprimerCollisionsToitChaume(ItemPhysique toit)
    {
        var aSupprimer = new List<Node>();
        foreach (Node enfant in toit.GetChildren())
        {
            if (enfant is CollisionShape3D shape && enfant.Name.ToString().Contains("CollisionToitChaume", StringComparison.Ordinal))
                aSupprimer.Add(shape);
        }
        for (int i = 0; i < aSupprimer.Count; i++)
            aSupprimer[i].QueueFree();
    }

    private static void DefinirCollisionToitActive(ItemPhysique toit, bool active)
    {
        foreach (Node enfant in toit.GetChildren())
        {
            if (enfant is CollisionShape3D shape && enfant.Name.ToString().Contains("CollisionToitChaume", StringComparison.Ordinal))
                shape.Disabled = !active;
        }
    }

    private static void AppliquerVisuelToitChaumeCompose(
        ItemPhysique toit,
        ToitChaumeVarianteVisuelle variante,
        float rotationLocaleY,
        Vector3 decalageLocal,
        float facteurEchelleXZ)
    {
        Node3D meshRoot = ObtenirNoeudMeshToit(toit);
        if (meshRoot == null)
            return;

        SlotInventaire slot = new SlotInventaire
        {
            ID = toit.ID_Objet,
            IndexBotanique = toit.IndexBotanique,
            IndexChimique = toit.IndexChimique,
            IndexMorphologique = toit.IndexCacheMemoire,
            Quantite = 1
        };

        NettoyerModelesEnfants(meshRoot);
        InstancierModeleToitChaume(meshRoot, slot, variante, true, facteurEchelleXZ, decalageLocal, rotationLocaleY);
        SupprimerCollisionsToitChaume(toit);
        AjouterCollisionToitChaume(toit, meshRoot);
        DefinirCollisionToitActive(toit, true);
        toit.Visible = true;
    }

    public void PlanifierRecalculAssemblageToitsChaume()
    {
        CallDeferred(nameof(RecalculerAssemblageToitsChaumeGlobal));
    }

    private void RecalculerAssemblageToitsChaumeGlobal()
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return;

        var toits = new List<ItemPhysique>();
        var parCellule = new Dictionary<Vector2I, ItemPhysique>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdToitChaume(ip.ID_Objet))
                continue;
            toits.Add(ip);
            Vector2I cle = CleGrilleToitChaume(ip.GlobalPosition);
            if (!parCellule.ContainsKey(cle))
                parCellule[cle] = ip;
            else
            {
                float dActuel = ip.GlobalPosition.DistanceSquaredTo(new Vector3(cle.X * ToitChaumePasGrilleMetres, ip.GlobalPosition.Y, cle.Y * ToitChaumePasGrilleMetres));
                float dExistant = parCellule[cle].GlobalPosition.DistanceSquaredTo(new Vector3(cle.X * ToitChaumePasGrilleMetres, parCellule[cle].GlobalPosition.Y, cle.Y * ToitChaumePasGrilleMetres));
                if (dActuel < dExistant)
                    parCellule[cle] = ip;
            }
        }

        for (int i = 0; i < toits.Count; i++)
        {
            AppliquerVisuelToitChaumeCompose(toits[i], ToitChaumeVarianteVisuelle.Solo, 0f, Vector3.Zero, 1f);
            toits[i].Visible = true;
            DefinirCollisionToitActive(toits[i], true);
        }

        var assignes = new HashSet<ItemPhysique>();
        foreach (KeyValuePair<Vector2I, ItemPhysique> pair in parCellule)
        {
            Vector2I[] anchors = new[]
            {
                pair.Key,
                new Vector2I(pair.Key.X - 1, pair.Key.Y),
                new Vector2I(pair.Key.X, pair.Key.Y - 1),
                new Vector2I(pair.Key.X - 1, pair.Key.Y - 1)
            };
            for (int a = 0; a < anchors.Length; a++)
            {
                Vector2I anchor = anchors[a];
                Vector2I c00 = anchor;
                Vector2I c10 = new Vector2I(anchor.X + 1, anchor.Y);
                Vector2I c01 = new Vector2I(anchor.X, anchor.Y + 1);
                Vector2I c11 = new Vector2I(anchor.X + 1, anchor.Y + 1);

                bool h00 = parCellule.TryGetValue(c00, out ItemPhysique i00) && !assignes.Contains(i00);
                bool h10 = parCellule.TryGetValue(c10, out ItemPhysique i10) && !assignes.Contains(i10);
                bool h01 = parCellule.TryGetValue(c01, out ItemPhysique i01) && !assignes.Contains(i01);
                bool h11 = parCellule.TryGetValue(c11, out ItemPhysique i11) && !assignes.Contains(i11);
                int count = (h00 ? 1 : 0) + (h10 ? 1 : 0) + (h01 ? 1 : 0) + (h11 ? 1 : 0);
                if (count < 2)
                    continue;

                if (!h00)
                    continue;
                ItemPhysique anchorItem = i00;
                if (anchorItem == null || assignes.Contains(anchorItem))
                    continue;

                if (count == 4)
                {
                    Vector3 decal4 = new Vector3(ToitChaumePasGrilleMetres * 0.5f, 0f, ToitChaumePasGrilleMetres * 0.5f);
                    AppliquerVisuelToitChaumeCompose(anchorItem, ToitChaumeVarianteVisuelle.Solo, 0f, decal4, 2f);
                    assignes.Add(anchorItem);
                    if (h10) { i10.Visible = false; DefinirCollisionToitActive(i10, false); assignes.Add(i10); }
                    if (h01) { i01.Visible = false; DefinirCollisionToitActive(i01, false); assignes.Add(i01); }
                    if (h11) { i11.Visible = false; DefinirCollisionToitActive(i11, false); assignes.Add(i11); }
                    continue;
                }

                if (count == 3)
                {
                    Vector3 decal3 = new Vector3(ToitChaumePasGrilleMetres * 0.5f, 0f, ToitChaumePasGrilleMetres * 0.5f);
                    float rotL = !h11 ? 0f : (!h01 ? 90f : (!h10 ? 270f : 180f));
                    AppliquerVisuelToitChaumeCompose(anchorItem, ToitChaumeVarianteVisuelle.Angle, rotL, decal3, 1f);
                    assignes.Add(anchorItem);
                    if (h10 && i10 != anchorItem) { i10.Visible = false; DefinirCollisionToitActive(i10, false); assignes.Add(i10); }
                    if (h01 && i01 != anchorItem) { i01.Visible = false; DefinirCollisionToitActive(i01, false); assignes.Add(i01); }
                    if (h11 && i11 != anchorItem) { i11.Visible = false; DefinirCollisionToitActive(i11, false); assignes.Add(i11); }
                    continue;
                }

                bool horizontal = h00 && h10;
                bool vertical = h00 && h01;
                if (!horizontal && !vertical)
                    continue;

                float rot = horizontal ? 0f : 90f;
                Vector3 decal = horizontal
                    ? new Vector3(ToitChaumePasGrilleMetres * 0.5f, 0f, 0f)
                    : new Vector3(0f, 0f, ToitChaumePasGrilleMetres * 0.5f);
                AppliquerVisuelToitChaumeCompose(anchorItem, ToitChaumeVarianteVisuelle.Long, rot, decal, 1f);
                assignes.Add(anchorItem);
                if (h10 && i10 != anchorItem) { i10.Visible = false; DefinirCollisionToitActive(i10, false); assignes.Add(i10); }
                if (h01 && i01 != anchorItem) { i01.Visible = false; DefinirCollisionToitActive(i01, false); assignes.Add(i01); }
                if (h11 && i11 != anchorItem) { i11.Visible = false; DefinirCollisionToitActive(i11, false); assignes.Add(i11); }
            }
        }
    }
}
