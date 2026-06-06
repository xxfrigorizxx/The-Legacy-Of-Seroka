using Godot;
using System;

public partial class Joueur
{
    private void MettreAJourMinageMainNueOuAtelier(float dt, SlotInventaire mainActive)
    {
        _cooldownMessageRecuperationFondation = Mathf.Max(0f, _cooldownMessageRecuperationFondation - dt);
        _cooldownMessageEtatBrasAction = Mathf.Max(0f, _cooldownMessageEtatBrasAction - dt);
        _cooldownMessageInventairePleinMinage = Mathf.Max(0f, _cooldownMessageInventairePleinMinage - dt);
        bool mainVide = mainActive.EstVide;
        bool hachette = mainActive.ID == 106 || mainActive.ID == IdObjetHachePierreTier1;
        bool hachePierreTier1 = mainActive.ID == IdObjetHachePierreTier1;
        bool dague = mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0;
        bool pelle = mainActive.ID == IdObjetPellePierreTier0;
        bool pioche = mainActive.ID == IdObjetPiochePierreTier0;

        if (EssayerObtenirAtelierSousVisee(out ItemPhysique atelier, out Vector3 pAtelier, out Vector3 nAtelier))
        {
            EtatOsSimple etatBrasAction = ObtenirEtatOsBrasMainActive();
            if (etatBrasAction == EtatOsSimple.Casse)
            {
                AfficherMessageEtatBrasAction("ZERO-K : Bras casse -> action refusee sur meuble/structure.");
                ReinitialiserMinageMainNueProgression();
                return;
            }

            if (!mainVide && !hachette)
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }

            var slotAtelier = ConstruireSlotAtelier(atelier);
            if (!ADeLaPlacePourSlotInventaire(slotAtelier))
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }

            if (_atelierCibleRecuperation != atelier)
            {
                _atelierCibleRecuperation = atelier;
                _progressionRecuperationAtelier = 0f;
                _cooldownParticulesRecuperationAtelier = 0f;
            }

            _progressionRecuperationAtelier += dt;
            _cooldownParticulesRecuperationAtelier -= dt;
            if (_cooldownParticulesRecuperationAtelier <= 0f)
            {
                _cooldownParticulesRecuperationAtelier = IntervalleParticulesRecuperationAtelier;
                EmmettreParticulesRecuperationAtelier(pAtelier, nAtelier);
            }

            bool estRack = atelier.ID_Objet == IdObjetRackBatons || atelier.ID_Objet == IdObjetRackBuches;
            float duree = estRack
                ? (mainVide ? DureeRecuperationRackMainNue : DureeRecuperationRackHachette)
                : (mainVide ? DureeRecuperationAtelierMainNue : DureeRecuperationAtelierHachette);
            if (etatBrasAction == EtatOsSimple.Felure)
                duree *= 2f;
            if (hachePierreTier1 && !mainVide)
                duree *= 0.5f;
            if (_progressionRecuperationAtelier < duree)
                return;

            if (!EssayerAjouterDansInventaire(slotAtelier))
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }
            if (AtelierPlanTravailOuvert == atelier) AtelierPlanTravailOuvert = null;
            if (RackBatonsOuvert == atelier) RackBatonsOuvert = null;
            if (StockageRackBatonsOuvert && RackBatonsOuvert == null)
                StockageRackBatonsOuvert = false;
            atelier.QueueFree();
            RafraichirHUD();
            GD.Print(atelier.ID_Objet switch
            {
                IdObjetRackBatons => "ZERO-K : Rack à bâtons récupéré dans l'inventaire.",
                IdObjetRackBuches => "ZERO-K : Rack à bûches récupéré dans l'inventaire.",
                IdObjetTableAnalyseTier1 => "ZERO-K : Table d'analyse récupérée dans l'inventaire.",
                IdObjetCoffreBoisTier0 => "ZERO-K : Coffre en bois récupéré dans l'inventaire.",
                _ => "ZERO-K : Atelier récupéré dans l'inventaire."
            });
            ReinitialiserMinageMainNueProgression();
            return;
        }

        if (EssayerObtenirFondationSousVisee(out ItemPhysique fondation, out Vector3 pFondation, out Vector3 nFondation))
        {
            EtatOsSimple etatBrasAction = ObtenirEtatOsBrasMainActive();
            if (etatBrasAction == EtatOsSimple.Casse)
            {
                AfficherMessageEtatBrasAction("ZERO-K : Bras casse -> action refusee sur meuble/structure.");
                ReinitialiserMinageMainNueProgression();
                return;
            }

            if (!OutilValideRecuperationFondation(fondation.ID_Objet, hachette, pioche))
            {
                AfficherMessageRecuperationFondation("ZERO-K : Outil invalide pour cette fondation (hachette/ pioche selon matériau).");
                ReinitialiserMinageMainNueProgression();
                return;
            }

            var slotFondation = ConstruireSlotFondation(fondation);
            if (!ADeLaPlacePourSlotInventaire(slotFondation))
            {
                AfficherMessageRecuperationFondation("ZERO-K : Inventaire plein, impossible de récupérer la fondation.");
                ReinitialiserMinageMainNueProgression();
                return;
            }

            if (_atelierCibleRecuperation != fondation)
            {
                _atelierCibleRecuperation = fondation;
                _progressionRecuperationAtelier = 0f;
                _cooldownParticulesRecuperationAtelier = 0f;
            }

            _progressionRecuperationAtelier += dt;
            _cooldownParticulesRecuperationAtelier -= dt;
            if (_cooldownParticulesRecuperationAtelier <= 0f)
            {
                _cooldownParticulesRecuperationAtelier = IntervalleParticulesRecuperationAtelier;
                EmmettreParticulesRecuperationAtelier(pFondation, nFondation);
            }

            float duree = ObtenirDureeRecuperationFondation(fondation.ID_Objet);
            if (etatBrasAction == EtatOsSimple.Felure)
                duree *= 2f;
            if (hachePierreTier1 && fondation.ID_Objet != IdObjetFondationRoche)
                duree *= 0.5f;
            if (_progressionRecuperationAtelier < duree)
                return;

            if (!EssayerAjouterDansInventaire(slotFondation))
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }
            AppliquerUsureOutilMainActive(fondation.ID_Objet == IdObjetFondationRoche ? 0.95f : 0.7f);
            fondation.QueueFree();
            RafraichirHUD();
            GD.Print(fondation.ID_Objet switch
            {
                IdObjetFondationBois => "ZERO-K : Fondation bois récupérée (hachette).",
                IdObjetFondationRoche => "ZERO-K : Fondation roche récupérée (pioche).",
                _ => "ZERO-K : Fondation mixte récupérée."
            });
            ReinitialiserMinageMainNueProgression();
            return;
        }

        _progressionRecuperationAtelier = 0f;
        _cooldownParticulesRecuperationAtelier = 0f;
        _atelierCibleRecuperation = null;

        if (dague && MettreAJourDepecageCadavreDague(dt, mainActive))
            return;

        if (dague && MettreAJourRecolteLianeDague(dt, mainActive))
            return;

        if ((dague || pelle) && MettreAJourRecolteBuissonOutil(dt, mainActive))
            return;

        if (!mainVide && !pelle && !pioche)
        {
            ReinitialiserMinageMainNueProgression();
            return;
        }

        Vector3 pointImpactVoxel;
        Vector3 normaleImpact;
        int idExtrait;
        bool cibleValide;
        if (pioche)
            cibleValide = EssayerObtenirCibleMinagePioche(out pointImpactVoxel, out normaleImpact, out idExtrait);
        else
            cibleValide = EssayerObtenirCibleMinageMainNue(out pointImpactVoxel, out normaleImpact, out idExtrait);
        if (!cibleValide)
        {
            if (!_aCibleMinageActive)
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }
            _tempsPerteCibleMinage += dt;
            if (_tempsPerteCibleMinage > DureeGracePerteCibleMinageSecondes)
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }
            pointImpactVoxel = _pointCibleMinage;
            normaleImpact = _normaleCibleMinage;
            idExtrait = _idCibleMinage;
        }
        else
        {
            _tempsPerteCibleMinage = 0f;
            bool cibleChangee = _aCibleMinageActive
                && (idExtrait != _idCibleMinage
                    || pointImpactVoxel.DistanceSquaredTo(_pointCibleMinage) > 0.42f * 0.42f);
            if (cibleChangee)
            {
                _progressionMinageMainNue = 0f;
                _cooldownParticulesMinageMainNue = 0f;
            }
            _aCibleMinageActive = true;
            _pointCibleMinage = pointImpactVoxel;
            _normaleCibleMinage = normaleImpact;
            _idCibleMinage = idExtrait;
        }

        _progressionMinageMainNue += dt;
        _cooldownParticulesMinageMainNue -= dt;
        if (_cooldownParticulesMinageMainNue <= 0f)
        {
            _cooldownParticulesMinageMainNue = IntervalleParticulesMinageMainNue;
            EmmettreParticulesMinageMainNue(pointImpactVoxel, normaleImpact, idExtrait);
        }

        float dureeMinage = pioche ? DureeMinagePiochePierreSecondes : (pelle ? (DureeMinageMainNueSecondes * 0.95f) : DureeMinageMainNueSecondes);
        if (_progressionMinageMainNue < dureeMinage)
            return;

        ExecuterMinageVoxelMainNue(pointImpactVoxel, idExtrait, pelle || pioche);
        ReinitialiserMinageMainNueProgression();
    }

    private float _progressionRemplissageBolEau;
    private const float DureeRemplissageBolEauSec = 0.5f;

    /// <summary>True si la visée (ou juste devant la caméra) traverse de l'eau. Utilise EstPointDansEau (l'eau n'est pas un voxel solide).</summary>
    private bool ViseDeLEauPourRemplissage()
    {
        if (_gestionnaireMonde == null || _camera == null)
            return false;
        _rayon.ForceRaycastUpdate();
        Vector3 baseCam = _camera.GlobalPosition;
        Vector3 avant = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (!_rayon.IsColliding())
        {
            // Eau libre (pas de fond touché à portée) : sonder quelques points devant la caméra.
            for (float d = 1.0f; d <= 3.5f; d += 0.5f)
                if (_gestionnaireMonde.EstPointDansEau(baseCam + avant * d))
                    return true;
            return false;
        }
        Vector3 impact = _rayon.GetCollisionPoint();
        // L'eau n'a pas de collision : le rayon touche le fond. Sonder au-dessus du fond et à mi-chemin.
        Vector3[] tests =
        {
            impact + Vector3.Up * 0.4f,
            impact + Vector3.Up * 1.0f,
            (baseCam + impact) * 0.5f
        };
        foreach (Vector3 p in tests)
            if (_gestionnaireMonde.EstPointDansEau(p))
                return true;
        return false;
    }

    /// <summary>Clic gauche maintenu ~0,5 s avec un bol vide en visant l'eau : le bol vide devient un bol d'eau (même essence).</summary>
    private void MettreAJourRemplissageBolEau(float dt)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.ID != IdObjetBolBois || mainActive.EstVide)
        {
            _progressionRemplissageBolEau = 0f;
            return;
        }
        if (!ViseDeLEauPourRemplissage())
        {
            _progressionRemplissageBolEau = 0f;
            return;
        }
        _progressionRemplissageBolEau += dt;
        if (_progressionRemplissageBolEau < DureeRemplissageBolEauSec)
            return;
        _progressionRemplissageBolEau = 0f;

        var bolEau = new SlotInventaire
        {
            ID = IdObjetBolEau,
            Quantite = 1,
            IndexBotanique = mainActive.IndexBotanique,
            IndexChimique = mainActive.IndexChimique,
            IndexMorphologique = mainActive.IndexMorphologique
        };

        if (Joueur.ObtenirQuantiteSlot(mainActive) <= 1)
        {
            // Transformation directe : 1 bol vide en main → 1 bol d'eau.
            if (MainGaucheEstActive) MainGauche = bolEau;
            else MainDroite = bolEau;
        }
        else
        {
            // Pile de bols : ne remplir qu'une unité, ranger le bol d'eau (ou le poser si plein).
            if (!ADeLaPlacePourSlotInventaire(bolEau))
            {
                GD.Print("SEROKA : Inventaire plein — videz une place pour remplir un bol.");
                return;
            }
            mainActive.Quantite -= 1;
            if (MainGaucheEstActive) MainGauche = mainActive;
            else MainDroite = mainActive;
            EssayerAjouterDansInventaire(bolEau);
        }
        RafraichirHUD();
        MettreAJourObjetEnMain();
        GD.Print("SEROKA : Bol rempli d'eau.");
    }

    private void ExecuterMinageVoxelMainNue(Vector3 pointImpactVoxel, int idExtrait, bool consommerUsureOutil)
    {
        if (!EstMatiereMinableMainNue(idExtrait) && !EstMatiereMinablePioche(idExtrait))
            return;

        SlotInventaire nouveauSlot;
        if (Atlas_Matiere.EstIdVoxelTerrainMinerai(idExtrait))
        {
            int seed = _gestionnaireMonde?.SeedTerrain ?? 0;
            nouveauSlot = ConstruireSlotLootMineraiVoxel(idExtrait, pointImpactVoxel, seed);
        }
        else
            nouveauSlot = new SlotInventaire { ID = idExtrait, IndexMorphologique = 0, IndexChimique = 0, Quantite = 1 };

        Vector3 centreVoxel = new Vector3(
            Mathf.Floor(pointImpactVoxel.X) + 0.5f,
            Mathf.Floor(pointImpactVoxel.Y) + 0.5f,
            Mathf.Floor(pointImpactVoxel.Z) + 0.5f);
        _gestionnaireMonde?.AppliquerDestructionGlobale(centreVoxel, RAYON_SCULPTURE, 5.0f);

        if (!ADeLaPlacePourSlotInventaire(nouveauSlot))
        {
            if (_cooldownMessageInventairePleinMinage <= 0f)
            {
                _cooldownMessageInventairePleinMinage = 1.2f;
                GD.Print("ZERO-K : Inventaire plein — le bloc est détruit mais le butin n'a pas pu être récupéré.");
            }
            if (consommerUsureOutil)
                AppliquerUsureOutilMainActive(1f);
            AttribuerXpMetierExtractionTerrain(idExtrait);
            RafraichirHUD();
            return;
        }
        if (!EssayerAjouterDansInventaire(nouveauSlot))
            return;
        if (consommerUsureOutil)
            AppliquerUsureOutilMainActive(1f);
        AttribuerXpMetierExtractionTerrain(idExtrait);
        RafraichirHUD();
    }

    private (float efficaciteHache, float efficacitePelle, float masse) AnalyserOutilCAO(Vector3 directionFrappe)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        directionFrappe = directionFrappe.Normalized();

        if (mainActive.ID == 100 && mainActive.MeshEclat != null)
        {
            int clef = ClefRegistreOutilForge(mainActive);
            if (RegistreOutilsForges.TryGetValue(clef, out var stats))
            {
                Vector3 normaleFacePlate = (_objetEnMain.GlobalTransform.Basis * stats.AxeTranchantLocal).Normalized();
                float frappeSurLePlat = Mathf.Abs(directionFrappe.Dot(normaleFacePlate));

                float erreurHache = frappeSurLePlat;
                if (erreurHache < 0.65f) erreurHache = 0f;
                else erreurHache = (erreurHache - 0.65f) * 2.85f;
                float effHache = 1.0f - Mathf.Clamp(erreurHache, 0f, 1f);

                float erreurPelle = 1.0f - frappeSurLePlat;
                if (erreurPelle < 0.65f) erreurPelle = 0f;
                else erreurPelle = (erreurPelle - 0.65f) * 2.85f;
                float effPelle = 1.0f - Mathf.Clamp(erreurPelle, 0f, 1f);

                return (effHache, effPelle, stats.Masse);
            }
        }

        if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            return (0.88f, 0.12f, 3.0f);
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
            return (0.82f, 0.18f, 2.0f);
        if (mainActive.ID == 105)
            return (0.78f, 0.22f, 1.15f);
        if (mainActive.ID == IdObjetFauxPierreTier0)
            return (0.79f, 0.21f, 1.28f);
        if (mainActive.ID == 106)
            return (0.88f, 0.12f, 2.05f);
        if (mainActive.ID == IdObjetHachePierreTier1)
            // Hache pierre tier 1 : nettement plus puissante que la hachette primitive.
            return (0.96f, 0.10f, 4.10f);
        if (mainActive.ID == IdObjetPiochePierreTier0)
            return (0.92f, 0.08f, 2.25f);
        if (mainActive.ID == IdObjetPellePierreTier0)
            return (0.26f, 0.95f, 2.15f);
        if (mainActive.ID == IdObjetLancePierreTier0)
            return (0.91f, 0.05f, 1.9f);
        if (ItemPhysique.EstIdRocheMatiere(mainActive.ID))
        {
            float m = mainActive.IndexTaille switch { 0 => 1f, 1 => 2f, 2 => 8f, 3 => 14f, 4 => 20f, _ => 8f };
            return (0.65f, 0.35f, m);
        }

        return (0.1f, 0.1f, 1.0f);
    }

    /// <summary>Épaisseur effective pour <see cref="ArbreVivant.SubirDegats"/> (tronc / lames) — indépendante du multiplicateur d’impact émergent.</summary>
    private float CalculerEpaisseurLamePourImpact(SlotInventaire mainActive, Vector3 directionFrappe)
    {
        float epaisseurLame = 0.2f;
        if (mainActive.ID == 100 && mainActive.MeshEclat != null)
        {
            int clef = ClefRegistreOutilForge(mainActive);
            if (RegistreOutilsForges.TryGetValue(clef, out var stats))
            {
                epaisseurLame = stats.EpaisseurLameBase;
                Vector3 normaleFacePlate = (_objetEnMain.GlobalTransform.Basis * stats.AxeTranchantLocal).Normalized();
                float frappeSurLePlat = Mathf.Abs(directionFrappe.Normalized().Dot(normaleFacePlate));

                float erreurHache = frappeSurLePlat;
                if (erreurHache < 0.65f) erreurHache = 0f;
                else erreurHache = (erreurHache - 0.65f) * 2.85f;

                epaisseurLame = stats.EpaisseurLameBase * (1.0f + erreurHache * 15.0f);
            }
        }
        else if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            Aabb boite = mainActive.MeshEclat.GetAabb();
            epaisseurLame = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));
        }
        else if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            epaisseurLame = 0.05f;
        else if (mainActive.ID == 105)
            epaisseurLame = 0.04f;
        else if (mainActive.ID == IdObjetFauxPierreTier0)
            epaisseurLame = 0.042f;
        else if (mainActive.ID == 106)
            epaisseurLame = 0.065f;
        else if (mainActive.ID == IdObjetHachePierreTier1)
            epaisseurLame = 0.05f;
        else if (mainActive.ID == IdObjetPiochePierreTier0)
            epaisseurLame = 0.06f;
        else if (mainActive.ID == IdObjetPellePierreTier0)
            epaisseurLame = 0.09f;
        else if (mainActive.ID == IdObjetLancePierreTier0)
            epaisseurLame = 0.045f;

        return epaisseurLame;
    }

    /// <summary>True si la pointe (manche→lame) est alignée sur la visée caméra→cible — les rotations R / Maj+R / Ctrl+R sur l’objet en main sont prises en compte via <see cref="GlobalTransform"/>.</summary>
    /// <param name="seuilAlignementMin">Combat vivant : ~0,15. Cadavre au sol : passer ~0,04 pour accepter des coups « hachoir ».</param>
    private bool EstFrappeDagueAvecLaLame(Vector3 pointImpact, Vector3 directionFrappe, float seuilAlignementMin = 0.15f)
    {
        if (_objetEnMain == null || _camera == null) return false;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D lameMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_3")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_3");
        if (lameMi?.Mesh == null || mancheMi?.Mesh == null) return false;
        Vector3 cL = lameMi.GlobalTransform * lameMi.Mesh.GetAabb().GetCenter();
        Vector3 cM = mancheMi.GlobalTransform * mancheMi.Mesh.GetAabb().GetCenter();
        Vector3 lameDepuisManche = cL - cM;
        if (lameDepuisManche.LengthSquared() < 1e-10f) return false;
        lameDepuisManche = lameDepuisManche.Normalized();
        Vector3 versCible = pointImpact - _camera.GlobalPosition;
        if (versCible.LengthSquared() < 1e-10f) return false;
        versCible = versCible.Normalized();

        float alignVisée = versCible.Dot(lameDepuisManche);
        float alignMouvement = 0f;

        if (directionFrappe.LengthSquared() > 1e-8f)
        {
            Vector3 dirNorm = directionFrappe.Normalized();
            alignMouvement = dirNorm.Dot(lameDepuisManche);

            if (Mathf.Abs(dirNorm.Y) > 0.5f)
                alignMouvement += 0.4f;
        }

        return Mathf.Max(alignVisée, alignMouvement) > seuilAlignementMin;
    }

    /// <summary>Hachette 106 : lame <c>tripo_part_4</c>, manche <c>tripo_part_5</c> (aligné avec <see cref="InstancierModeleArme"/> id 106).</summary>
    private bool EstFrappeHachette106AvecLaLame(Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (_objetEnMain == null || _camera == null) return false;
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D lameMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4")
            // Hache pierre: noms GLB variables (pas toujours tripo_part_*).
            ?? TrouverMeshParMots(modele, "roche", "rock", "stone", "pierre", "head", "blade", "lame");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_5")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_5")
            ?? TrouverMeshParMots(modele, "manche", "bois", "wood", "baton", "stick", "handle", "shaft");
        if (lameMi?.Mesh == null || mancheMi?.Mesh == null) return false;
        Vector3 cL = lameMi.GlobalTransform * lameMi.Mesh.GetAabb().GetCenter();
        Vector3 cM = mancheMi.GlobalTransform * mancheMi.Mesh.GetAabb().GetCenter();
        Vector3 lameDepuisManche = cL - cM;
        if (lameDepuisManche.LengthSquared() < 1e-10f) return false;
        lameDepuisManche = lameDepuisManche.Normalized();
        Vector3 versCible = pointImpact - _camera.GlobalPosition;
        if (versCible.LengthSquared() < 1e-10f) return false;
        versCible = versCible.Normalized();

        float alignVisée = versCible.Dot(lameDepuisManche);
        float alignMouvement = 0f;

        if (directionFrappe.LengthSquared() > 1e-8f)
        {
            Vector3 dirNorm = directionFrappe.Normalized();
            alignMouvement = dirNorm.Dot(lameDepuisManche);

            if (Mathf.Abs(dirNorm.Y) > 0.5f)
                alignMouvement += 0.4f;
        }

        // Hache pierre : selon certains GLB, la vectorisation lame/manche est inversée.
        // On accepte l'orientation opposée pour éviter les faux "coup de manche".
        if (mainActive.ID == IdObjetHachePierreTier1)
        {
            alignVisée = Mathf.Max(alignVisée, -alignVisée);
            alignMouvement = Mathf.Max(alignMouvement, -alignMouvement);
        }

        const float seuil = 0.15f;
        return Mathf.Max(alignVisée, alignMouvement) > seuil;
    }

    /// <summary>Lance 111 : pointe <c>tripo_part_2</c>, manche <c>tripo_part_0</c> (fallback par mots-clés).</summary>
    private bool EstFrappeLance111AvecLaPointe(Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (_objetEnMain == null || _camera == null) return false;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D pointeMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_2")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_2")
            ?? TrouverMeshParMots(modele, "tip", "pointe", "spear", "lance", "head", "blade", "lame", "stone", "rock", "pierre");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_0")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_0")
            ?? TrouverMeshParMots(modele, "handle", "shaft", "manche", "baton", "stick", "bois", "wood");
        if (pointeMi?.Mesh == null || mancheMi?.Mesh == null) return false;
        Vector3 cL = pointeMi.GlobalTransform * pointeMi.Mesh.GetAabb().GetCenter();
        Vector3 cM = mancheMi.GlobalTransform * mancheMi.Mesh.GetAabb().GetCenter();
        Vector3 pointeDepuisManche = cL - cM;
        if (pointeDepuisManche.LengthSquared() < 1e-10f) return false;
        pointeDepuisManche = pointeDepuisManche.Normalized();
        Vector3 versCible = pointImpact - _camera.GlobalPosition;
        if (versCible.LengthSquared() < 1e-10f) return false;
        versCible = versCible.Normalized();

        float alignVisée = versCible.Dot(pointeDepuisManche);
        float alignMouvement = 0f;
        if (directionFrappe.LengthSquared() > 1e-8f)
            alignMouvement = directionFrappe.Normalized().Dot(pointeDepuisManche);
        return Mathf.Max(alignVisée, alignMouvement) > 0.22f;
    }

    /// <summary>Faux 112 : lame <c>tripo_part_2</c>, manche bois <c>tripo_part_0</c> (même convention que pelle/lance sur l’épée GLB).</summary>
    private bool EstFrappeFaux112AvecLaLame(Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (_objetEnMain == null || _camera == null) return false;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D lameMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_2")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_2")
            ?? TrouverMeshParMots(modele, "tip", "pointe", "blade", "lame", "head", "stone", "rock", "pierre", "epee", "épée");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_0")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_0")
            ?? TrouverMeshParMots(modele, "handle", "shaft", "manche", "baton", "stick", "bois", "wood");
        if (lameMi?.Mesh == null || mancheMi?.Mesh == null) return false;
        Vector3 cL = lameMi.GlobalTransform * lameMi.Mesh.GetAabb().GetCenter();
        Vector3 cM = mancheMi.GlobalTransform * mancheMi.Mesh.GetAabb().GetCenter();
        Vector3 lameDepuisManche = cL - cM;
        if (lameDepuisManche.LengthSquared() < 1e-10f) return false;
        lameDepuisManche = lameDepuisManche.Normalized();
        Vector3 versCible = pointImpact - _camera.GlobalPosition;
        if (versCible.LengthSquared() < 1e-10f) return false;
        versCible = versCible.Normalized();

        float alignVisée = versCible.Dot(lameDepuisManche);
        float alignMouvement = 0f;
        if (directionFrappe.LengthSquared() > 1e-8f)
            alignMouvement = directionFrappe.Normalized().Dot(lameDepuisManche);
        return Mathf.Max(alignVisée, alignMouvement) > 0.18f;
    }

    private static BoeufSauvage ObtenirBoeufDepuisCollider(Node col)
    {
        for (Node n = col; n != null; n = n.GetParent())
            if (n is BoeufSauvage b) return b;
        return null;
    }

    private static string NomZoneDepuisColliderRaycast(GodotObject collider)
    {
        if (collider is CollisionShape3D cs)
            return cs.Name.ToString();
        return string.Empty;
    }

    private float MultiplicateurMateriauArmeContreFaune(SlotInventaire mainActive)
    {
        if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            return 1.18f;
        if (mainActive.ID == 105)
            return 1.14f;
        if (mainActive.ID == IdObjetFauxPierreTier0)
            return 1.12f;
        if (mainActive.ID == 106)
            return 1.22f;
        if (mainActive.ID == IdObjetHachePierreTier1)
            return 1.44f;
        if (mainActive.ID == IdObjetPiochePierreTier0)
            return 1.05f;
        if (mainActive.ID == IdObjetPellePierreTier0)
            return 0.86f;
        if (mainActive.ID == IdObjetLancePierreTier0)
            return 1.28f;
        if (mainActive.EstUnEclat)
            return 1.1f;
        if (mainActive.ID == 100)
            return 1f + Mathf.Clamp(mainActive.IndexChimique, 0, 9) * 0.025f;
        if (ItemPhysique.EstIdRocheMatiere(mainActive.ID))
            return 0.95f;
        return 0.9f;
    }

    private const float MasseFrappeMainNueKg = 3.2f;
    private const float CoefMainNueFaune = 0.72f;
    private const float CoefMainNueRigid = 0.48f;

    private static float CoefficientContactImpact(bool tranchant, bool perforant)
    {
        if (perforant) return 1.34f;
        if (tranchant) return 1.12f;
        return 0.82f;
    }

    private float CalculerVitesseFrappe(TypeMouvementFrappe mouvement, Vector3 directionFrappe, Vector3 normaleImpact)
    {
        float vitesseBase = 2.35f + Mathf.Clamp(_mouvementSourisCumule.Length() / 105f, 0f, 2.8f);
        float bonusMouvement = mouvement switch
        {
            TypeMouvementFrappe.DeHautEnBas => 1.18f,
            TypeMouvementFrappe.GaucheADroite => 1.08f,
            TypeMouvementFrappe.DroiteAGauche => 1.08f,
            TypeMouvementFrappe.Estoc => 1.14f,
            _ => 1f
        };
        Vector3 dirNorm = directionFrappe.LengthSquared() > 1e-6f ? directionFrappe.Normalized() : Vector3.Forward;
        Vector3 normaleNorm = normaleImpact.LengthSquared() > 1e-6f ? normaleImpact.Normalized() : Vector3.Up;
        // Plus le coup arrive "dans" la surface, plus l'impact est efficace.
        float alignementSurface = Mathf.Clamp((-dirNorm).Dot(normaleNorm), -1f, 1f);
        float facteurAlignement = Mathf.Lerp(0.72f, 1.28f, (alignementSurface + 1f) * 0.5f);
        return Mathf.Max(0.45f, vitesseBase * bonusMouvement * facteurAlignement);
    }

    private float CalculerIntensiteImpactPhysique(
        float masseImpact,
        float forceMotrice,
        TypeMouvementFrappe mouvement,
        Vector3 directionFrappe,
        Vector3 normaleImpact,
        float coefficientContact,
        float coefficientMatiere,
        float coefficientCible)
    {
        float masse = Mathf.Max(0.05f, masseImpact);
        float vitesse = CalculerVitesseFrappe(mouvement, directionFrappe, normaleImpact);
        float energie = 0.5f * masse * vitesse * vitesse;
        float impulsion = masse * vitesse * Mathf.Clamp(forceMotrice, 0.2f, 4f);
        float intensite = (energie * 0.58f + impulsion * 0.42f)
            * Mathf.Max(0.15f, coefficientContact)
            * Mathf.Max(0.15f, coefficientMatiere)
            * Mathf.Max(0.15f, coefficientCible);
        return Mathf.Max(0.01f, intensite);
    }

    private static float CoefficientZoneBovin(string nomZone)
    {
        string nom = (nomZone ?? string.Empty).ToLowerInvariant();
        if (nom.Contains("tete")) return 1.08f;
        if (nom.Contains("ventre")) return 1.03f;
        return 1f;
    }
}
