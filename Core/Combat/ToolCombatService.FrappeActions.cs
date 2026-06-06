using Godot;
using System;

public partial class Joueur
{
    /// <summary>Relâchement clic gauche : sol → creusage / fauchage ; sinon frappe roches, arbres, rigides.</summary>
    private void ExecuterAction(float force, TypeMouvementFrappe mouvement)
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive)) return;
        // Alignement lame (dague) lit la géométrie sur _objetEnMain : doit refléter la main sélectionnée avant le raycast.
        MettreAJourObjetEnMain();
        force *= MultiplicateurForceFrappeEndurance();

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
        {
            GD.Print("ZERO-K : Aucune collision sous la visée — rapprochez-vous du sol ou vérifiez le chargement des chunks.");
            return;
        }

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        Vector3 pointImpact = _rayon.GetCollisionPoint();
        bool viseTerrain = EstSolViseParRayon(_rayon, objetTouche);
        // Sol prioritaire : ne pas détourner vers un cadavre d'arbre proche (sinon le fauchage ne part jamais).
        if (!viseTerrain)
        {
            RigidBody3D cadavreArbreVise = ResoudreCadavreArbreCible(objetTouche, pointImpact);
            if (cadavreArbreVise != null)
                objetTouche = cadavreArbreVise;
        }

        BoeufSauvage boeufSousVisee = ObtenirBoeufDepuisCollider(objetTouche);

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (mouvement == TypeMouvementFrappe.DeHautEnBas) directionMouvement = -_camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.DeBasEnHaut) directionMouvement = _camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.GaucheADroite) directionMouvement = _camera.GlobalTransform.Basis.X.Normalized();
        else if (mouvement == TypeMouvementFrappe.DroiteAGauche) directionMouvement = -_camera.GlobalTransform.Basis.X.Normalized();

        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);

        bool surfaceFauchable = viseTerrain
            || (EstOutilFaucheurEnMain(mainActive) && EstSurfaceHorizontaleFauchable());
        if (boeufSousVisee == null && surfaceFauchable)
        {
            if (mainActive.ID == IdObjetLancePierreTier0)
            {
                GD.Print("ZERO-K : La lance est dédiée au combat. Utilisez-la contre une cible ou en lancer.");
                return;
            }
            ExecuterCreusage(force, effPelle, masseOutil, pointImpact);
            return;
        }

        if (objetTouche == null)
        {
            GD.Print("ZERO-K : Objet touché non reconnu (ni sol ni rigide avec nœud).");
            return;
        }

        ExecuterFrappePhysique(force, effHache, masseOutil, objetTouche, pointImpact, directionMouvement, mouvement);
    }

    private void ExecuterActionMainNue(float force, TypeMouvementFrappe mouvement)
    {
        force *= MultiplicateurForceFrappeEndurance();
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (objetTouche == null)
            return;
        if (ObtenirBoeufDepuisCollider(objetTouche) == null && EstSolViseParRayon(_rayon, objetTouche))
            return;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (mouvement == TypeMouvementFrappe.DeHautEnBas) directionMouvement = -_camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.DeBasEnHaut) directionMouvement = _camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.GaucheADroite) directionMouvement = _camera.GlobalTransform.Basis.X.Normalized();
        else if (mouvement == TypeMouvementFrappe.DroiteAGauche) directionMouvement = -_camera.GlobalTransform.Basis.X.Normalized();
        ExecuterFrappeMainNue(force, objetTouche, pointImpact, directionMouvement, mouvement);
    }

    private void ExecuterFrappeMainNue(float force, Node objetTouche, Vector3 pointImpact, Vector3 directionFrappe, TypeMouvementFrappe mouvement)
    {
        float multiplicateurDegatsBras = ObtenirMultiplicateurDegatsFrappeSelonEtatOsBras();
        if (multiplicateurDegatsBras <= 0f)
        {
            AfficherMessageEtatBrasAction("ZERO-K : Bras casse -> aucun degat de frappe.");
            return;
        }

        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            GD.Print("ZERO-K : Vos poings ne suffisent pas pour blesser un arbre. Utilisez un outil.");
            return;
        }

        Vector3 normaleImpact = _rayon != null && _rayon.IsColliding() ? _rayon.GetCollisionNormal() : -directionFrappe;
        float massePoing = MasseFrappeMainNueKg * Mathf.Clamp(ObtenirMultiplicateurDegatsForce(), 0.9f, 2.2f);

        BoeufSauvage boeufTouche = ObtenirBoeufDepuisCollider(objetTouche);
        if (boeufTouche != null)
        {
            bool etaitCadavreDepecable = boeufTouche.EstCadavreDepecable();
            string nomZone = NomZoneDepuisColliderRaycast(_rayon.GetCollider());
            float intensite = CalculerIntensiteImpactPhysique(
                massePoing,
                force,
                mouvement,
                directionFrappe,
                normaleImpact,
                CoefMainNueFaune,
                0.74f,
                CoefficientZoneBovin(nomZone));
            float degats = Mathf.Clamp(intensite * 0.92f, 0.06f, 12f);
            degats *= multiplicateurDegatsBras;
            bool applique = boeufTouche.RecevoirImpactCombat(
                degats,
                pointImpact,
                directionFrappe,
                false,
                false,
                nomZone,
                (ulong)GetInstanceId());
            if (applique)
                AjouterXpFutureState("Force", 1UL);
            if (applique && !etaitCadavreDepecable && boeufTouche.EstCadavreDepecable())
                AjouterXpMetier("Chasseur", 1UL);
            return;
        }

        RigidBody3D rbCible = ResoudreRigidBodyDepuisCollider(objetTouche);
        if (rbCible == null)
            return;

        Vector3 dirFrappeObj = _rayon != null && _rayon.IsColliding() ? -_rayon.GetCollisionNormal() : directionFrappe;
        float intensiteRigid = CalculerIntensiteImpactPhysique(
            massePoing,
            force,
            mouvement,
            directionFrappe,
            normaleImpact,
            CoefMainNueRigid,
            0.76f,
            0.88f);
        intensiteRigid *= multiplicateurDegatsBras;
        rbCible.ApplyCentralImpulse(dirFrappeObj.Normalized() * Mathf.Clamp(intensiteRigid * 0.16f, 0.35f, 4.6f));

        var item = rbCible as ItemPhysique ?? rbCible.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (item == null)
            return;

        Vector3 dirVue = (pointImpact - _camera.GlobalPosition).Normalized();
        int resultat = item.SubirDegats(Mathf.Clamp(intensiteRigid * 0.95f, 0.04f, 45f), dirVue, pointImpact);
        if (resultat > 0)
            AjouterXpFutureState("Force", 1UL);
    }

    private void JouerAnimationFrappe(TypeMouvementFrappe type)
    {
        if (_objetEnMain == null) return;
        bool visuelEnMain = _objetEnMain.Mesh != null || _objetEnMain.FindChild("ModeleArme", true, false) != null;
        if (!visuelEnMain) return;
        ImpulserPoseBrasFrappe(type);
        _tweenFrappe?.Kill();
        _tweenFrappe = CreateTween();
        const float RalentissementFrappe = 1.10f; // +10%
        float dureeCoup = 0.09f * RalentissementFrappe;
        float dureeRetour = 0.11f * RalentissementFrappe;

        MettreAJourObjetEnMain();

        Vector3 posBase = _objetEnMain.Position;
        Vector3 rotBase = _objetEnMain.RotationDegrees;
        Vector3 posCible = posBase;
        Vector3 rotCible = rotBase;

        if (type == TypeMouvementFrappe.Estoc) { posCible.Z -= 0.26f; rotCible.X -= 12f; }
        else if (type == TypeMouvementFrappe.DeHautEnBas) { posCible.Y -= 0.18f; rotCible.X -= 36f; }
        else if (type == TypeMouvementFrappe.DeBasEnHaut) { posCible.Y += 0.16f; rotCible.X += 30f; }
        else if (type == TypeMouvementFrappe.GaucheADroite) { posCible.X += 0.20f; rotCible.Y -= 34f; rotCible.Z -= 20f; }
        else if (type == TypeMouvementFrappe.DroiteAGauche) { posCible.X -= 0.20f; rotCible.Y += 34f; rotCible.Z += 20f; }

        _tweenFrappe.TweenProperty(_objetEnMain, "position", posCible, dureeCoup).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tweenFrappe.Parallel().TweenProperty(_objetEnMain, "rotation_degrees", rotCible, dureeCoup).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tweenFrappe.TweenProperty(_objetEnMain, "position", posBase, dureeRetour).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _tweenFrappe.Parallel().TweenProperty(_objetEnMain, "rotation_degrees", rotBase, dureeRetour).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _tweenFrappe.TweenCallback(Callable.From(ReposerObjetEnMainApresFrappe));
    }

    private void ReposerObjetEnMainApresFrappe()
    {
        MettreAJourObjetEnMain();
    }

    private void ExecuterCreusage(float force, float efficacitePelle, float masseOutil, Vector3 pointImpact)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        float multiplicateurForce = ObtenirMultiplicateurDegatsForce();

        // Dague/faux sur buisson: interdit en coup instantane, uniquement en maintien.
        if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0) && (_gestionnaireMonde?.EssayerDetecterBuissonSousPoint(pointImpact, RayonDetectionBuisson, out Vector3 posBuisson, out _)) == true
            && pointImpact.DistanceTo(posBuisson) <= DistanceMaxViseeDirecteBuisson)
        {
            GD.Print(mainActive.ID == IdObjetFauxPierreTier0
                ? "ZERO-K : Maintenez avec la faux pour couper le buisson."
                : "ZERO-K : Maintenez avec la dague (aloe: 1s) pour recolter le buisson.");
            return;
        }

        // Hachette: coupe immédiate de buisson -> branche courte.
        if (mainActive.ID == 106)
        {
            if ((_gestionnaireMonde?.RecolterBuissonGlobal(pointImpact, RayonDetectionBuisson, 0)) == true)
            {
                AppliquerUsureOutilMainActive(1.7f);
                GD.Print("ZERO-K : Branche de buisson récoltée à la hachette.");
                return;
            }
        }

        if (EstOutilFaucheurEnMain(mainActive))
        {
            _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 3.1f);
            if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0)
                AppliquerUsureOutilMainActive(mainActive.ID == IdObjetFauxPierreTier0 ? 0.78f : 0.75f);
            if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0)
                AjouterXpFutureState("Dextiriter", 1UL);
            else if (EstRocheFaucheuseEnMain(mainActive) && ObtenirNiveauFutureState("Dextiriter") < 15UL)
                AjouterXpFutureState("Dextiriter", 1UL);

            if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0) && efficacitePelle >= 0.6f)
            {
                int idMatiereImpact = _gestionnaireMonde?.ObtenirMatiereExacte(pointImpact - (_rayon.GetCollisionNormal() * 0.45f)) ?? 0;
                _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpact, 0.95f, 4.5f);
                _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 2.8f);
                AppliquerUsureOutilMainActive(2.4f);
                AttribuerXpMetierExtractionTerrain(idMatiereImpact);
                GD.Print(mainActive.ID == IdObjetFauxPierreTier0
                    ? "ZERO-K : La faux racle la surface (coup orienté pelle, peu de pénétration)."
                    : "ZERO-K : La dague racle la surface (coup orienté pelle, peu de pénétration).");
                return;
            }

            GD.Print("ZERO-K : Fauchage de la flore. Récolte de fibres en cours.");
            return;
        }

        if (efficacitePelle < 0.6f)
        {
            AlerteSqueletteBoiteNoire("Mauvais angle de lame pour deplacer la terre. Il faut une surface plate (Pelle/Houe).");
            return;
        }

        float forceCreusage = masseOutil * force * efficacitePelle * multiplicateurForce;
        if (mainActive.ID == IdObjetPellePierreTier0)
        {
            int idMatiereImpact = _gestionnaireMonde?.ObtenirMatiereExacte(pointImpact - (_rayon.GetCollisionNormal() * 0.45f)) ?? 0;
            // Pelle pierre tier0 : bonus sur terre/sable/terre aride/neige.
            if (idMatiereImpact == 1 || idMatiereImpact == 3 || idMatiereImpact == 5 || idMatiereImpact == 6)
                forceCreusage *= 1.05f;
        }

        if (forceCreusage > 10f)
        {
            GD.Print($"ZERO-K : Extraction du sol réussie. (Force Volume: {forceCreusage:F1})");
            if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0)
                AppliquerUsureOutilMainActive(3.2f);
        }
        else
        {
            GD.Print("ZERO-K : Manque de force ou outil trop léger pour percer ce sol.");
        }
    }

    /// <summary>True si la cible est un arbre tombé (<c>ArbreMort</c>) ou une branche morte au sol — exemptions gameplay ci-dessous.</summary>
    private static bool EstRigidCadavreBoise(RigidBody3D rb)
    {
        if (rb == null) return false;
        string n = rb.Name.ToString();
        return n.Contains("ArbreMort") || n.Contains("BrancheMorte");
    }

    /// <summary>Feuillage, branches, tronc brut sur un <see cref="ArbreMort"/> abattu.</summary>
    private void ExecuterFrappeCadavreArbre(RigidBody3D rbCible, SlotInventaire main, Vector3 pointImpact, Vector3 directionFrappe)
    {
        ReparerMetaIndexBotaniqueSurCadavreSiPossible(rbCible);
        bool fauxSurCadavre = main.ID == IdObjetFauxPierreTier0 && EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe);
        bool outilTranchantPourArbre = main.ID == 106
            || main.ID == IdObjetHachePierreTier1
            || main.EstUnEclat
            || EstRocheTranchantePourBois(main)
            || fauxSurCadavre;
        if (!outilTranchantPourArbre)
        {
            GD.Print("ZERO-K : Cadavre d'arbre — utilisez une hachette (106), un éclat, une roche plate/pointe, ou la lame de la faux (orientation tranchante). Pas la pelle ni la pioche comme tranchant.");
            return;
        }

        byte essenceBois = LireIndexBotaniqueBoisSurRigid(rbCible);
        bool brancheMorte = essenceBois == LSystem_Botanique.IndexCheneMort || essenceBois == LSystem_Botanique.IndexBouleauMort;
        int branchesRestantes = rbCible.HasMeta("BranchesRestantes") ? (int)rbCible.GetMeta("BranchesRestantes").AsInt32() : 0;
        branchesRestantes = Mathf.Clamp(branchesRestantes, 0, 10);

        Node feuillage = rbCible.GetNodeOrNull("Feuillage");
        if (!brancheMorte && feuillage != null && feuillage is MeshInstance3D miFeu && miFeu.Mesh != null)
        {
            Mesh meshFeuillage = miFeu.Mesh;
            Material matFeuilles = miFeu.MaterialOverride?.Duplicate() as Material;
            miFeu.QueueFree();
            JouerSonEtEffetCoupeArbre(pointImpact);
            GD.Print("ZERO-K : Feuillage arraché du cadavre végétal.");
            byte essenceFeuillage = LireIndexBotaniqueBoisSurRigid(rbCible);
            int quantite = 3 + (int)(rbCible.Mass / 100f);
            Vector3 baseFeuillage = CalculerPointAuDessusSol(rbCible.GlobalPosition.Lerp(pointImpact, 0.5f) + Vector3.Up * 1.2f, 0.42f);
            for (int i = 0; i < quantite; i++)
            {
                var bloc = BlocChutant.CreerFeuillageArrache(baseFeuillage, matFeuilles, meshFeuillage, essenceFeuillage);
                GetTree().CurrentScene.AddChild(bloc);
                bloc.GlobalPosition = baseFeuillage + new Vector3(((float)GD.Randf() - 0.5f) * 0.65f, (float)i * 0.06f + 0.12f, ((float)GD.Randf() - 0.5f) * 0.65f);
            }
            AjusterCollisionCadavreArbre(rbCible, feuillagePresent: false, branchesRestantes);
            return;
        }

        if (feuillage != null)
            feuillage.QueueFree();
        byte essenceBrancheAuSol = main.ID == IdObjetFauxPierreTier0 ? LSystem_Botanique.IndexChene : essenceBois;

        if (branchesRestantes > 0)
        {
            JouerSonEtEffetCoupeArbre(pointImpact);
            branchesRestantes--;
            rbCible.SetMeta("BranchesRestantes", branchesRestantes);
            GD.Print(brancheMorte
                ? $"ZERO-K : Branche morte amputée. Restes morts : {branchesRestantes}"
                : $"ZERO-K : Branche amputée. Reste : {branchesRestantes}");
            Material matEssence = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBrancheAuSol);
            var blocBr = BlocChutant.Creer(pointImpact, BlocChutant.ID_BRANCHE, matEssence);
            blocBr.SetMeta("IndexBotanique", (int)essenceBrancheAuSol);
            GetTree().CurrentScene.AddChild(blocBr);
            Vector3 posBr = CalculerPointAuDessusSol(pointImpact + directionFrappe * 0.2f + Vector3.Up * 0.8f, 0.22f);
            blocBr.GlobalPosition = posBr;
            Vector3 imp = directionFrappe.LengthSquared() > 1e-6f ? directionFrappe.Normalized() * 3f : Vector3.Up * 2f;
            blocBr.ApplyCentralImpulse(imp);
            AjusterCollisionCadavreArbre(rbCible, feuillagePresent: false, branchesRestantes);
            return;
        }

        bool peutLibererTronc = main.ID == 106
            || main.ID == IdObjetHachePierreTier1
            || main.EstUnEclat
            || EstRocheTranchantePourBois(main);
        if (!peutLibererTronc)
        {
            AlerteSqueletteBoiteNoire("Il faut un tranchant: roche plate ou en pointe, eclat ou hachette.");
            return;
        }

        float hauteurTronc = rbCible.HasMeta("HauteurTronc") ? (float)rbCible.GetMeta("HauteurTronc").AsSingle() : 4.0f;
        float scaleZ = hauteurTronc / 1.2f;

        JouerSonEtEffetCoupeArbre(pointImpact);
        GD.Print($"ZERO-K : Le cadavre est purgé. Vous obtenez un Tronc Brut massif ({hauteurTronc:F1}m).");

        var slotTroncLong = new SlotInventaire
        {
            ID = 30,
            IndexBotanique = essenceBois,
            IndexMorphologique = 0,
            IndexTaille = 0,
            ScaleEclat = new Vector3(1, 1, scaleZ)
        };

        CalculerDimensionsBoisPose(30, 0, 0, out float rayonTroncSpawn, out _, out _, out _);
        Vector3 refSpawn = rbCible.GlobalPosition;
        Vector3 posTronc = CalculerPointAuDessusSol(refSpawn, Mathf.Clamp(rayonTroncSpawn * 0.35f, 0.12f, 0.38f));
        Node3D leTronc = CreerBlocPose(posTronc, slotTroncLong);
        if (leTronc != null)
        {
            leTronc.GlobalRotation = rbCible.GlobalRotation;
            leTronc.GlobalPosition = refSpawn;
            if (leTronc is RigidBody3D rbTronc)
            {
                // Bûche de cadavre : objet lâché, pas un bloc posé figé dans les airs.
                rbTronc.RemoveFromGroup("BlocsPoses");
                rbTronc.Freeze = false;
                rbTronc.GravityScale = 1f;
                rbTronc.Sleeping = false;
                Vector3 impulsion = directionFrappe.LengthSquared() > 1e-6f
                    ? directionFrappe.Normalized() * 2.4f + Vector3.Down * 1.8f
                    : Vector3.Down * 3.2f;
                rbTronc.ApplyCentralImpulse(impulsion);
            }
        }

        rbCible.QueueFree();
    }

    /// <summary>
    /// Réduit la collision du cadavre d'arbre après retrait feuillage/branches.
    /// Le mesh du "Bois" reste volontairement inchangé pour éviter des recooks lourds en plein combat;
    /// on pilote donc la hitbox via une box dynamique qui suit l'état restant.
    /// </summary>
    private static void AjusterCollisionCadavreArbre(RigidBody3D cadavre, bool feuillagePresent, int branchesRestantes)
    {
        if (cadavre == null || !GodotObject.IsInstanceValid(cadavre))
            return;

        float hauteurTronc = cadavre.HasMeta("HauteurTronc") ? (float)cadavre.GetMeta("HauteurTronc").AsSingle() : 4.0f;
        float rayonBase = cadavre.HasMeta("RayonTroncBase") ? (float)cadavre.GetMeta("RayonTroncBase").AsSingle() : 0.22f;
        float rayonSommet = cadavre.HasMeta("RayonTroncSommet") ? (float)cadavre.GetMeta("RayonTroncSommet").AsSingle() : rayonBase * 0.65f;
        float rayonTronc = Mathf.Max(0.12f, Mathf.Max(rayonBase, rayonSommet));
        float ratioBranches = Mathf.Clamp(branchesRestantes / 10.0f, 0f, 1f);

        // largeur supplémentaire "virtuelle" des branchages restants
        float extraBranchage = feuillagePresent
            ? Mathf.Clamp(rayonTronc * 2.3f, 0.65f, 2.0f)
            : Mathf.Lerp(0.06f, Mathf.Clamp(rayonTronc * 1.3f, 0.18f, 0.9f), ratioBranches);

        float largeur = Mathf.Max(0.42f, rayonTronc * 2f + extraBranchage);
        float hauteur = Mathf.Clamp(Mathf.Max(hauteurTronc * 0.88f, 0.8f), 0.8f, 7.5f);

        CollisionShape3D collisionDynamique = null;
        foreach (Node enfant in cadavre.GetChildren())
        {
            if (enfant is not CollisionShape3D cs)
                continue;
            if (cs.Name == "CollisionCadavreDynamique")
            {
                collisionDynamique = cs;
                continue;
            }
            // Désactive les anciennes collisions figées (convex + englobante initiales).
            cs.Disabled = true;
        }

        if (collisionDynamique == null)
        {
            collisionDynamique = new CollisionShape3D { Name = "CollisionCadavreDynamique" };
            cadavre.AddChild(collisionDynamique);
        }

        collisionDynamique.Disabled = false;
        if (collisionDynamique.Shape is not BoxShape3D box)
            box = new BoxShape3D();
        box.Size = new Vector3(largeur, hauteur, largeur);
        collisionDynamique.Shape = box;
        collisionDynamique.Position = new Vector3(0f, hauteur * 0.5f, 0f);
    }
}
