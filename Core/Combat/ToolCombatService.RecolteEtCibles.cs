using Godot;
using System;

public partial class Joueur
{
    private void ReinitialiserMinageLianeDagueProgression()
    {
        _progressionRecolteLianeDague = 0f;
        _cooldownParticulesMinageLiane = 0f;
        _tempsPerteCibleLiane = 0f;
        _pointRecolteLiane = Vector3.Zero;
        _arbreCibleLiane = null;
    }

    private void ReinitialiserDepecageCadavreDagueProgression()
    {
        _progressionDepecageCadavreDague = 0f;
        _cooldownParticulesDepecageCadavre = 0f;
        _tempsPerteCibleDepecageCadavre = 0f;
        _pointDepecageCadavre = Vector3.Zero;
        _boeufCadavreCibleDepecage = null;
    }

    private void EmmettreParticulesDepecageCadavre(Vector3 position, Vector3 normale)
    {
        if (GetTree()?.CurrentScene == null) return;
        Vector3 n = normale.LengthSquared() > 1e-5f ? normale.Normalized() : Vector3.Up;
        var container = new Node3D { Name = "FxDepecageCadavre" };
        GetTree().CurrentScene.AddChild(container);
        container.GlobalPosition = position + n * 0.015f;

        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.34f, 0.42f), Roughness = 0.82f, Metallic = 0f };
        for (int i = 0; i < 9; i++)
        {
            var mi = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.018f, 0.018f, 0.018f) * (0.7f + GD.Randf() * 0.8f) },
                MaterialOverride = mat,
                Position = new Vector3((float)(GD.Randf() - 0.5f) * 0.14f, (float)GD.Randf() * 0.08f, (float)(GD.Randf() - 0.5f) * 0.14f)
            };
            container.AddChild(mi);
        }
        var timer = container.GetTree().CreateTimer(0.22);
        timer.Timeout += () => container.QueueFree();
    }

    /// <summary>Dépitage cadavre bovin : maintien 3s clic gauche avec dague (plus de coups comptés).</summary>
    private bool MettreAJourDepecageCadavreDague(float dt, SlotInventaire mainActive)
    {
        if (mainActive.ID != 105)
        {
            ReinitialiserDepecageCadavreDagueProgression();
            return false;
        }

        _rayon.ForceRaycastUpdate();
        Vector3 pointImpact = _pointDepecageCadavre;
        Vector3 normaleImpact = Vector3.Up;
        BoeufSauvage boeuf = null;
        if (_rayon.IsColliding())
        {
            Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            boeuf = ObtenirBoeufDepuisCollider(objetTouche);
            pointImpact = _rayon.GetCollisionPoint();
            normaleImpact = _rayon.GetCollisionNormal();
        }

        bool boeufValide = boeuf != null && boeuf.EstCadavreDepecable();
        if (!boeufValide)
        {
            // Petite grâce anti-jitter: la visée peut décrocher 1-2 frames sur un cadavre collé au sol.
            bool cibleMemoireValide = _boeufCadavreCibleDepecage != null
                && GodotObject.IsInstanceValid(_boeufCadavreCibleDepecage)
                && _boeufCadavreCibleDepecage.EstCadavreDepecable();
            if (!cibleMemoireValide)
            {
                ReinitialiserDepecageCadavreDagueProgression();
                return false;
            }

            _tempsPerteCibleDepecageCadavre += dt;
            if (_tempsPerteCibleDepecageCadavre > 0.35f)
            {
                ReinitialiserDepecageCadavreDagueProgression();
                return false;
            }
            boeuf = _boeufCadavreCibleDepecage;
            if (pointImpact == Vector3.Zero)
                pointImpact = boeuf.GlobalPosition + Vector3.Up * 0.36f;
            normaleImpact = Vector3.Up;
        }
        else
        {
            _tempsPerteCibleDepecageCadavre = 0f;
        }

        if (_boeufCadavreCibleDepecage != boeuf)
        {
            _boeufCadavreCibleDepecage = boeuf;
            _progressionDepecageCadavreDague = 0f;
            _cooldownParticulesDepecageCadavre = 0f;
        }

        _pointDepecageCadavre = pointImpact;
        _progressionDepecageCadavreDague += dt;
        _cooldownParticulesDepecageCadavre -= dt;
        if (_cooldownParticulesDepecageCadavre <= 0f)
        {
            _cooldownParticulesDepecageCadavre = IntervalleParticulesDepecageCadavre;
            EmmettreParticulesDepecageCadavre(pointImpact, normaleImpact);
        }

        if (_progressionDepecageCadavreDague < DureeDepecageDagueCadavreSecondes)
            return true;

        Vector3 directionFrappe = _camera != null ? -_camera.GlobalTransform.Basis.Z.Normalized() : -GlobalTransform.Basis.Z.Normalized();
        AppliquerUsureOutilMainActive(1f);
        ExecuterLootDepecageCadavreBoeuf(boeuf, _pointDepecageCadavre, directionFrappe);
        _bloquerActionClicGaucheApresDepecage = true;
        ReinitialiserDepecageCadavreDagueProgression();
        return true;
    }

    private bool EssayerObtenirCibleBuisson(out Vector3 pointImpact, out Vector3 pointBuissonMonde, out Vector3I posBuisson, out byte typeBuisson)
    {
        pointImpact = Vector3.Zero;
        pointBuissonMonde = Vector3.Zero;
        posBuisson = default;
        typeBuisson = 0;
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSolViseParRayon(_rayon, objetTouche)) return false;
        pointImpact = _rayon.GetCollisionPoint();
        if (_gestionnaireMonde == null) return false;
        if (!_gestionnaireMonde.EssayerDetecterBuissonSousPoint(pointImpact, RayonDetectionBuisson, out Vector3 posMondeBuisson, out typeBuisson))
            return false;
        // Evite qu'un buisson "proche" capture l'action quand on vise en fait le gazon a cote.
        if (pointImpact.DistanceTo(posMondeBuisson) > DistanceMaxViseeDirecteBuisson)
            return false;
        pointBuissonMonde = posMondeBuisson;
        posBuisson = new Vector3I(Mathf.FloorToInt(posMondeBuisson.X), Mathf.FloorToInt(posMondeBuisson.Y), Mathf.FloorToInt(posMondeBuisson.Z));
        return true;
    }

    /// <summary>Dague/Pelle : minage maintenu sur buisson (3s standard, 1s pour aloe vera a la dague).</summary>
    private bool MettreAJourRecolteBuissonOutil(float dt, SlotInventaire mainActive)
    {
        bool dague = mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0;
        bool pelle = mainActive.ID == IdObjetPellePierreTier0;
        if (!dague && !pelle) return false;
        if (!EssayerObtenirCibleBuisson(out Vector3 pointImpact, out Vector3 pointBuissonMonde, out Vector3I posBuisson, out byte typeBuisson))
        {
            _progressionRecolteBuisson = 0f;
            _cooldownParticulesMinageBuisson = 0f;
            _aCibleBuissonRecolte = false;
            return false;
        }

        if (!_aCibleBuissonRecolte || posBuisson != _posBuissonRecolte)
        {
            _aCibleBuissonRecolte = true;
            _posBuissonRecolte = posBuisson;
            _progressionRecolteBuisson = 0f;
            _cooldownParticulesMinageBuisson = 0f;
        }
        bool cibleAloe = Chunk_Serveur.EstTypeAloeVera(typeBuisson);
        if (dague && cibleAloe)
        {
            var slotAloe = new SlotInventaire
            {
                ID = IdObjetAloeVera,
                IndexChimique = Chunk_Serveur.VarianteBuissonAloeVera,
                Quantite = 1,
                IndexMorphologique = 0,
                IndexTaille = 0,
                IndexBotanique = LSystem_Botanique.IndexChene
            };
            if (!ADeLaPlacePourSlotInventaire(slotAloe))
            {
                AfficherMessageRecuperationFondation("ZERO-K : Inventaire plein, impossible de recuperer l'aloe vera.");
                _progressionRecolteBuisson = 0f;
                return true;
            }
        }
        _pointRecolteBuisson = pointBuissonMonde;
        _progressionRecolteBuisson += dt;
        _cooldownParticulesMinageBuisson -= dt;
        if (_cooldownParticulesMinageBuisson <= 0f)
        {
            _cooldownParticulesMinageBuisson = IntervalleParticulesMinageBuisson;
            Vector3 normale = _rayon != null && _rayon.IsColliding() ? _rayon.GetCollisionNormal() : Vector3.Up;
            // Retour visuel permanent pendant minage maintenu du buisson.
            EmmettreParticulesMinageMainNue(pointImpact, normale, 8);
        }
        float dureeRecolte = (dague && cibleAloe) ? DureeRecolteAloeDagueSecondes : DureeRecolteBuissonOutilSecondes;
        if (_progressionRecolteBuisson < dureeRecolte)
            return true;

        byte mode = dague
            ? (cibleAloe ? (byte)3 : (byte)1)
            : (byte)2;
        bool succes = _gestionnaireMonde?.RecolterBuissonGlobal(_pointRecolteBuisson, RayonDetectionBuisson, mode) ?? false;
        if (succes)
        {
            if (dague)
            {
                if (cibleAloe)
                {
                    var slotAloe = new SlotInventaire
                    {
                        ID = IdObjetAloeVera,
                        IndexChimique = Chunk_Serveur.VarianteBuissonAloeVera,
                        Quantite = 1,
                        IndexMorphologique = 0,
                        IndexTaille = 0,
                        IndexBotanique = LSystem_Botanique.IndexChene
                    };
                    if (EssayerAjouterDansInventaire(slotAloe))
                    {
                        RafraichirHUD();
                    }
                    else
                    {
                        Vector3 directionFrappe = _camera != null ? -_camera.GlobalTransform.Basis.Z.Normalized() : -GlobalTransform.Basis.Z.Normalized();
                        Node3D aloeAuSol = CreerBlocPose(_pointRecolteBuisson + Vector3.Up * 0.08f, slotAloe);
                        if (aloeAuSol is RigidBody3D rbAloe)
                            rbAloe.ApplyCentralImpulse(directionFrappe * 1.1f + Vector3.Up * 0.8f);
                        GD.Print("ZERO-K : Inventaire plein, aloe vera depose au sol.");
                    }
                }
                AppliquerUsureOutilMainActive(1.6f);
                GD.Print(cibleAloe
                    ? "ZERO-K : Aloe vera recolte (1s) et ajoute a l'inventaire."
                    : "ZERO-K : Dague: branche de buisson recoltee.");
            }
            else
            {
                AppliquerUsureOutilMainActive(2.1f);
                GD.Print("ZERO-K : Buisson deracine (plante replantable recuperee).");
            }
            _bloquerActionClicGaucheApresMinageBuisson = true;
        }
        _progressionRecolteBuisson = 0f;
        _cooldownParticulesMinageBuisson = 0f;
        _aCibleBuissonRecolte = false;
        return true;
    }

    /// <summary>Dague sur liane jungle: maintien 2s pour couper/récolter.</summary>
    private bool MettreAJourRecolteLianeDague(float dt, SlotInventaire mainActive)
    {
        if (mainActive.ID != 105 && mainActive.ID != IdObjetFauxPierreTier0)
        {
            ReinitialiserMinageLianeDagueProgression();
            return false;
        }

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
        {
            ReinitialiserMinageLianeDagueProgression();
            return false;
        }

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre == null || arbre.IndexBotanique != LSystem_Botanique.IndexJungle)
        {
            ReinitialiserMinageLianeDagueProgression();
            return false;
        }

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 directionFrappe = -_camera.GlobalTransform.Basis.Z.Normalized();
        bool cibleLianeValide = arbre.EstPointCibleLiane(pointImpact);

        if (_arbreCibleLiane != arbre)
        {
            _arbreCibleLiane = arbre;
            _progressionRecolteLianeDague = 0f;
            _cooldownParticulesMinageLiane = 0f;
            _tempsPerteCibleLiane = 0f;
        }

        if (!cibleLianeValide)
        {
            // Petite grâce anti-jitter: évite de reset la progression au moindre frame perdu.
            _tempsPerteCibleLiane += dt;
            if (_tempsPerteCibleLiane > 0.30f)
            {
                ReinitialiserMinageLianeDagueProgression();
                return false;
            }
            return true;
        }
        _tempsPerteCibleLiane = 0f;

        _pointRecolteLiane = pointImpact;
        _progressionRecolteLianeDague += dt;
        _cooldownParticulesMinageLiane -= dt;
        if (_cooldownParticulesMinageLiane <= 0f)
        {
            _cooldownParticulesMinageLiane = IntervalleParticulesMinageLiane;
            EmmettreParticulesMinageMainNue(pointImpact, normaleImpact, 8);
        }

        if (_progressionRecolteLianeDague < DureeRecolteLianeDagueSecondes)
            return true;

        if (arbre.EssayerCouperLiane(_pointRecolteLiane, directionFrappe, out Vector3 posSpawnLiane))
        {
            JouerSonEtEffetCoupeArbre(_pointRecolteLiane);
            var slotLiane = new SlotInventaire
            {
                ID = 16, // Liane (matière dédiée), pas herbe.
                IndexChimique = 16,
                IndexMorphologique = 16,
                IndexTaille = 0,
                ScaleEclat = Vector3.One
            };
            if (!EssayerAjouterDansInventaire(slotLiane))
            {
                // Fallback sol si inventaire plein.
                Node3D liane = CreerBlocPose(posSpawnLiane, slotLiane);
                if (liane is RigidBody3D rbLiane)
                    rbLiane.ApplyCentralImpulse(directionFrappe.Normalized() * 1.2f + Vector3.Up * 0.9f);
                GD.Print("ZERO-K : Inventaire plein, liane déposée au sol.");
            }
            else
            {
                RafraichirHUD();
            }
            AppliquerUsureOutilMainActive(1.15f);
            GD.Print("ZERO-K : Liane coupée (2s) et ajoutée à l'inventaire.");
        }
        else
        {
            GD.Print("ZERO-K : Cette zone n'a pas de liane exploitable.");
        }

        ReinitialiserMinageLianeDagueProgression();
        return true;
    }

    private bool EssayerObtenirCibleMinageMainNue(out Vector3 pointImpactVoxel, out Vector3 normaleImpact, out int idExtrait)
    {
        pointImpactVoxel = Vector3.Zero;
        normaleImpact = Vector3.Up;
        idExtrait = 0;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        // Bovin : le rayon touche le corps (CharacterBody3D), pas le voxel — sans ce garde-fou on sonde « derrière » la bête et on extrait de la terre.
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses") || ObtenirArbreDepuisCollider(objetTouche) != null || ObtenirBoeufDepuisCollider(objetTouche) != null))
            return false;

        pointImpactVoxel = _rayon.GetCollisionPoint();
        normaleImpact = _rayon.GetCollisionNormal();
        idExtrait = ObtenirMatiereSolideDepuisImpact(pointImpactVoxel, normaleImpact);
        return EstMatiereMinableMainNue(idExtrait);
    }

    private bool EssayerObtenirCibleMinagePioche(out Vector3 pointImpactVoxel, out Vector3 normaleImpact, out int idExtrait)
    {
        pointImpactVoxel = Vector3.Zero;
        normaleImpact = Vector3.Up;
        idExtrait = 0;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses") || ObtenirArbreDepuisCollider(objetTouche) != null || ObtenirBoeufDepuisCollider(objetTouche) != null))
            return false;

        pointImpactVoxel = _rayon.GetCollisionPoint();
        normaleImpact = _rayon.GetCollisionNormal();
        idExtrait = ObtenirMatiereSolideDepuisImpact(pointImpactVoxel, normaleImpact);
        return EstMatiereMinablePioche(idExtrait);
    }

    /// <summary>
    /// Lit la matière réellement frappée en privilégiant une profondeur faible sous la surface visée.
    /// Évite le décalage de couche (ex: miner la neige et récupérer la terre/roche du dessous).
    /// </summary>
    private int ObtenirMatiereSolideDepuisImpact(Vector3 pointImpactVoxel, Vector3 normaleImpact)
    {
        if (_gestionnaireMonde == null)
            return 0;

        Vector3 n = normaleImpact.LengthSquared() > 1e-6f ? normaleImpact.Normalized() : Vector3.Up;
        // On entre progressivement dans le volume solide à partir de la surface touchée.
        float[] profondeurs = { 0.08f, 0.16f, 0.28f, 0.42f };
        for (int i = 0; i < profondeurs.Length; i++)
        {
            Vector3 p = pointImpactVoxel - n * profondeurs[i];
            int id = _gestionnaireMonde.ObtenirMatiereExacte(p);
            if (Atlas_Matiere.EstIdVoxelSurfaceTerrain(id))
                return id;
            if (Atlas_Matiere.EstIdVoxelTerrainMinerai(id))
                return id;
        }

        // Repli conservateur pour ne pas bloquer le minage si un cas limite est rencontré.
        int fallback = _gestionnaireMonde.ObtenirMatiereExacte(pointImpactVoxel - n * 0.5f);
        if (Atlas_Matiere.EstIdVoxelSurfaceTerrain(fallback))
            return fallback;
        if (Atlas_Matiere.EstIdVoxelTerrainMinerai(fallback))
            return fallback;
        return 0;
    }

    private bool EssayerObtenirAtelierSousVisee(out ItemPhysique atelier, out Vector3 pointImpact, out Vector3 normaleImpact)
    {
        atelier = null;
        pointImpact = Vector3.Zero;
        normaleImpact = Vector3.Up;
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        pointImpact = _rayon.GetCollisionPoint();
        normaleImpact = _rayon.GetCollisionNormal();
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var item = ResoudreItemPhysiqueDepuisNoeudRaycast(objetTouche);
        if (item == null) return false;
        bool estMeubleRecuperable = item.ID_Objet == 200
            || item.ID_Objet == IdObjetTableAnalyseTier1
            || item.ID_Objet == IdObjetRackBatons
            || item.ID_Objet == IdObjetRackBuches
            || item.ID_Objet == IdObjetCoffreBoisTier0;
        if (!estMeubleRecuperable) return false;
        atelier = item;
        return true;
    }

    private static SlotInventaire ConstruireSlotAtelier(ItemPhysique atelier)
    {
        return new SlotInventaire
        {
            ID = atelier != null ? atelier.ID_Objet : 200,
            IndexBotanique = atelier != null ? atelier.IndexBotanique : LSystem_Botanique.IndexChene,
            IndexMorphologique = atelier != null ? atelier.IndexCacheMemoire : 0,
            IndexChimique = atelier != null ? atelier.IndexChimique : 0,
            IndexTaille = 0,
            ScaleEclat = Vector3.One,
            EstUnEclat = false,
            Quantite = 1,
            CleConteneur = (atelier != null && atelier.HasMeta("CleConteneur")) ? atelier.GetMeta("CleConteneur").AsString() : ""
        };
    }

    private bool EssayerObtenirFondationSousVisee(out ItemPhysique fondation, out Vector3 pointImpact, out Vector3 normaleImpact)
    {
        fondation = null;
        pointImpact = Vector3.Zero;
        normaleImpact = Vector3.Up;
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        pointImpact = _rayon.GetCollisionPoint();
        normaleImpact = _rayon.GetCollisionNormal();
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var item = ResoudreItemPhysiqueDepuisNoeudRaycast(objetTouche);
        if (item == null || !EstIdFondation(item.ID_Objet)) return false;
        fondation = item;
        return true;
    }

    private static ItemPhysique ResoudreItemPhysiqueDepuisNoeudRaycast(Node noeud)
    {
        for (Node cur = noeud; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique ip)
                return ip;
            if (cur is Node3D n3)
            {
                ItemPhysique enfant = n3.GetNodeOrNull<ItemPhysique>("ItemPhysique");
                if (enfant != null)
                    return enfant;
            }
        }
        return null;
    }

    private void AfficherMessageRecuperationFondation(string message)
    {
        if (_cooldownMessageRecuperationFondation > 0f)
            return;
        _cooldownMessageRecuperationFondation = 0.8f;
        GD.Print(message);
    }

    private static SlotInventaire ConstruireSlotFondation(ItemPhysique fondation)
    {
        return new SlotInventaire
        {
            ID = fondation != null ? fondation.ID_Objet : IdObjetFondationBois,
            IndexBotanique = fondation != null ? fondation.IndexBotanique : LSystem_Botanique.IndexChene,
            IndexMorphologique = fondation != null ? fondation.IndexCacheMemoire : 0,
            IndexChimique = fondation != null ? fondation.IndexChimique : 0,
            IndexTaille = 0,
            ScaleEclat = Vector3.One,
            EstUnEclat = false,
            Quantite = 1,
            GenomeAssemblage = fondation?.GenomeAssemblage ?? "",
            CleConteneur = (fondation != null && fondation.HasMeta("CleConteneur")) ? fondation.GetMeta("CleConteneur").AsString() : ""
        };
    }

    private static bool OutilValideRecuperationFondation(int idFondation, bool hachette, bool pioche)
    {
        if (idFondation == IdObjetFondationBois)
            return hachette;
        if (idFondation == IdObjetFondationRoche)
            return pioche;
        if (idFondation == IdObjetFondationBoisSoleRoche || idFondation == IdObjetFondationRocheSoleBois)
            return hachette || pioche;
        return false;
    }

    private static float ObtenirDureeRecuperationFondation(int idFondation)
    {
        if (idFondation == IdObjetFondationBois)
            return DureeRecuperationFondationBoisHachette;
        if (idFondation == IdObjetFondationRoche)
            return DureeRecuperationFondationRochePioche;
        return DureeRecuperationFondationMixteOutil;
    }

    private bool PeutRecevoirDansSlot(SlotInventaire destination, SlotInventaire source)
    {
        if (destination.EstVide) return true;
        if (!SontEmpilables(destination, source)) return false;
        int max = ObtenirPileMax(destination);
        return ObtenirQuantiteSlot(destination) + ObtenirQuantiteSlot(source) <= max;
    }

    private bool ADeLaPlacePourSlotInventaire(SlotInventaire slot)
    {
        if (PeutRecevoirDansSlot(MainGauche, slot) || PeutRecevoirDansSlot(MainDroite, slot))
            return true;
        if (ASacEquipe())
        {
            for (int i = 0; i < GrilleSacStockage.Length; i++)
                if (PeutRecevoirDansSlot(RefSlotSac(i), slot))
                    return true;
        }
        if (ACeintureSacochesEquipe())
        {
            for (int i = 0; i < GrilleCeintureStockage.Length; i++)
                if (PeutRecevoirDansSlot(RefSlotCeintureStockage(i), slot))
                    return true;
        }
        return false;
    }

    private bool EssayerAjouterDansInventaire(SlotInventaire slot)
    {
        slot.Quantite = ObtenirQuantiteSlot(slot);
        if (ASacEquipe())
        {
            for (int i = 0; i < GrilleSacStockage.Length; i++)
            {
                ref SlotInventaire s = ref RefSlotSac(i);
                if (TenterEmpilementComplet(ref s, slot)) return true;
            }
        }
        if (ACeintureSacochesEquipe())
        {
            for (int i = 0; i < GrilleCeintureStockage.Length; i++)
            {
                ref SlotInventaire s = ref RefSlotCeintureStockage(i);
                if (TenterEmpilementComplet(ref s, slot)) return true;
            }
        }
        if (TenterEmpilementComplet(ref MainGauche, slot)) return true;
        if (TenterEmpilementComplet(ref MainDroite, slot)) return true;

        if (ASacEquipe())
        {
            for (int i = 0; i < GrilleSacStockage.Length; i++)
            {
                ref SlotInventaire s = ref RefSlotSac(i);
                if (s.EstVide) { s = slot; return true; }
            }
        }
        if (ACeintureSacochesEquipe())
        {
            for (int i = 0; i < GrilleCeintureStockage.Length; i++)
            {
                ref SlotInventaire s = ref RefSlotCeintureStockage(i);
                if (s.EstVide) { s = slot; return true; }
            }
        }
        if (MainGauche.EstVide) { MainGauche = slot; return true; }
        if (MainDroite.EstVide) { MainDroite = slot; return true; }
        return false;
    }

    private static Color ObtenirCouleurParticulesMinage(int idExtrait)
    {
        return idExtrait switch
        {
            1 => new Color(0.42f, 0.34f, 0.24f),
            2 => new Color(0.42f, 0.42f, 0.44f),
            3 => new Color(0.86f, 0.78f, 0.56f),
            4 => new Color(0.9f, 0.9f, 0.94f),
            5 => new Color(0.86f, 0.9f, 0.96f),
            6 => new Color(0.58f, 0.42f, 0.24f),
            7 => new Color(0.33f, 0.25f, 0.17f),
            8 => new Color(0.38f, 0.49f, 0.24f),
            9 => new Color(0.66f, 0.7f, 0.75f),
            Atlas_Matiere.IdVoxelSableQuartz => new Color(0.92f, 0.90f, 0.87f),
            _ => new Color(0.42f, 0.34f, 0.24f)
        };
    }

    private void EmmettreParticulesMinageMainNue(Vector3 position, Vector3 normale, int idExtrait)
    {
        if (GetTree()?.CurrentScene == null) return;
        Vector3 n = normale.LengthSquared() > 1e-5f ? normale.Normalized() : Vector3.Up;
        var container = new Node3D { Name = "FxMinageMainNue" };
        GetTree().CurrentScene.AddChild(container);
        container.GlobalPosition = position + n * 0.02f;

        var mat = new StandardMaterial3D { AlbedoColor = ObtenirCouleurParticulesMinage(idExtrait), Roughness = 0.95f, Metallic = 0f };
        for (int i = 0; i < 7; i++)
        {
            var mi = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.025f, 0.025f, 0.025f) * (0.7f + GD.Randf() * 0.8f) },
                MaterialOverride = mat,
                Position = new Vector3((float)(GD.Randf() - 0.5f) * 0.14f, (float)GD.Randf() * 0.08f, (float)(GD.Randf() - 0.5f) * 0.14f)
            };
            container.AddChild(mi);
        }
        var timer = container.GetTree().CreateTimer(0.2);
        timer.Timeout += () => container.QueueFree();
    }

    private void EmmettreParticulesRecuperationAtelier(Vector3 position, Vector3 normale)
    {
        if (GetTree()?.CurrentScene == null) return;
        Vector3 n = normale.LengthSquared() > 1e-5f ? normale.Normalized() : Vector3.Up;
        var container = new Node3D { Name = "FxRecuperationAtelier" };
        GetTree().CurrentScene.AddChild(container);
        container.GlobalPosition = position + n * 0.02f;

        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.18f), Roughness = 0.9f, Metallic = 0f };
        for (int i = 0; i < 8; i++)
        {
            var mi = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.028f, 0.02f, 0.024f) * (0.7f + GD.Randf() * 0.9f) },
                MaterialOverride = mat,
                Position = new Vector3((float)(GD.Randf() - 0.5f) * 0.18f, (float)GD.Randf() * 0.1f, (float)(GD.Randf() - 0.5f) * 0.18f)
            };
            container.AddChild(mi);
        }
        var timer = container.GetTree().CreateTimer(0.22);
        timer.Timeout += () => container.QueueFree();
    }
}
