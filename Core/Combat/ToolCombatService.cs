using Godot;
using System;

public partial class Joueur
{
    private const float DureeMinageMainNueSecondes = 3.0f;
    private const float DureeMinagePiochePierreSecondes = 4.0f;
    private const float IntervalleParticulesMinageMainNue = 0.12f;
    private const float DureeRecuperationAtelierMainNue = 5.0f;
    private const float DureeRecuperationAtelierHachette = 2.85f;
    private const float IntervalleParticulesRecuperationAtelier = 0.14f;
    private float _progressionMinageMainNue;
    private float _cooldownParticulesMinageMainNue;
    private float _progressionRecuperationAtelier;
    private float _cooldownParticulesRecuperationAtelier;
    private ItemPhysique _atelierCibleRecuperation;

    private static bool EstMatiereMinableMainNue(int idMatiere)
    {
        // Main nue : sable + familles de terres (dont herbe/terre de surface) uniquement.
        return idMatiere == 1 || idMatiere == 3 || idMatiere == 6 || idMatiere == 7 || idMatiere == 8 || idMatiere == 9;
    }

    private static bool EstMatiereMinablePioche(int idMatiere)
    {
        // Pioche : roche voxel.
        return idMatiere == 2;
    }

    private void ReinitialiserMinageMainNueProgression()
    {
        _progressionMinageMainNue = 0f;
        _cooldownParticulesMinageMainNue = 0f;
        _progressionRecuperationAtelier = 0f;
        _cooldownParticulesRecuperationAtelier = 0f;
        _atelierCibleRecuperation = null;
    }

    private bool EssayerObtenirCibleMinageMainNue(out Vector3 pointImpactVoxel, out Vector3 normaleImpact, out int idExtrait)
    {
        pointImpactVoxel = Vector3.Zero;
        normaleImpact = Vector3.Up;
        idExtrait = 0;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses")))
            return false;

        pointImpactVoxel = _rayon.GetCollisionPoint();
        normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeSondage = pointImpactVoxel - (normaleImpact * 0.5f);
        idExtrait = _gestionnaireMonde?.ObtenirMatiereExacte(pointDeSondage) ?? 0;
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
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses")))
            return false;

        pointImpactVoxel = _rayon.GetCollisionPoint();
        normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeSondage = pointImpactVoxel - (normaleImpact * 0.5f);
        idExtrait = _gestionnaireMonde?.ObtenirMatiereExacte(pointDeSondage) ?? 0;
        return EstMatiereMinablePioche(idExtrait);
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
        var item = objetTouche as ItemPhysique
            ?? (objetTouche as Node)?.GetParent() as ItemPhysique
            ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (item == null || item.ID_Objet != 200) return false;
        atelier = item;
        return true;
    }

    private static SlotInventaire ConstruireSlotAtelier(ItemPhysique atelier)
    {
        return new SlotInventaire
        {
            ID = 200,
            IndexBotanique = atelier != null ? atelier.IndexBotanique : LSystem_Botanique.IndexChene,
            IndexMorphologique = atelier != null ? atelier.IndexCacheMemoire : 0,
            IndexChimique = atelier != null ? atelier.IndexChimique : 0,
            IndexTaille = 0,
            ScaleEclat = Vector3.One,
            EstUnEclat = false,
            Quantite = 1
        };
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
        if (TenterEmpilementComplet(ref MainGauche, slot)) return true;
        if (TenterEmpilementComplet(ref MainDroite, slot)) return true;
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

        if (MainGauche.EstVide) { MainGauche = slot; return true; }
        if (MainDroite.EstVide) { MainDroite = slot; return true; }
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
        return false;
    }

    private void EmmettreParticulesMinageMainNue(Vector3 position, Vector3 normale)
    {
        if (GetTree()?.CurrentScene == null) return;
        Vector3 n = normale.LengthSquared() > 1e-5f ? normale.Normalized() : Vector3.Up;
        var container = new Node3D { Name = "FxMinageMainNue" };
        GetTree().CurrentScene.AddChild(container);
        container.GlobalPosition = position + n * 0.02f;

        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.34f, 0.24f), Roughness = 0.95f, Metallic = 0f };
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

    private void MettreAJourMinageMainNueOuAtelier(float dt, SlotInventaire mainActive)
    {
        bool mainVide = mainActive.EstVide;
        bool hachette = mainActive.ID == 106;
        bool pelle = mainActive.ID == IdObjetPellePierreTier0;
        bool pioche = mainActive.ID == IdObjetPiochePierreTier0;

        if (EssayerObtenirAtelierSousVisee(out ItemPhysique atelier, out Vector3 pAtelier, out Vector3 nAtelier))
        {
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

            float duree = mainVide ? DureeRecuperationAtelierMainNue : DureeRecuperationAtelierHachette;
            if (_progressionRecuperationAtelier < duree)
                return;

            if (!EssayerAjouterDansInventaire(slotAtelier))
            {
                ReinitialiserMinageMainNueProgression();
                return;
            }
            atelier.QueueFree();
            RafraichirHUD();
            GD.Print("ZERO-K : Atelier récupéré dans l'inventaire.");
            ReinitialiserMinageMainNueProgression();
            return;
        }

        _progressionRecuperationAtelier = 0f;
        _cooldownParticulesRecuperationAtelier = 0f;
        _atelierCibleRecuperation = null;

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
            ReinitialiserMinageMainNueProgression();
            return;
        }

        _progressionMinageMainNue += dt;
        _cooldownParticulesMinageMainNue -= dt;
        if (_cooldownParticulesMinageMainNue <= 0f)
        {
            _cooldownParticulesMinageMainNue = IntervalleParticulesMinageMainNue;
            EmmettreParticulesMinageMainNue(pointImpactVoxel, normaleImpact);
        }

        float dureeMinage = pioche ? DureeMinagePiochePierreSecondes : (pelle ? (DureeMinageMainNueSecondes * 0.95f) : DureeMinageMainNueSecondes);
        if (_progressionMinageMainNue < dureeMinage)
            return;

        ExecuterMinageVoxelMainNue(pointImpactVoxel, idExtrait);
        ReinitialiserMinageMainNueProgression();
    }

    private void ExecuterMinageVoxelMainNue(Vector3 pointImpactVoxel, int idExtrait)
    {
        if (!EstMatiereMinableMainNue(idExtrait) && !EstMatiereMinablePioche(idExtrait))
            return;
        if (MainGaucheEstActive && !MainGauche.EstVide && !MainDroite.EstVide) return;
        if (!MainGaucheEstActive && !MainDroite.EstVide && !MainGauche.EstVide) return;

        _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpactVoxel, RAYON_SCULPTURE, 5.0f);
        var nouveauSlot = new SlotInventaire { ID = idExtrait, IndexMorphologique = 0, IndexChimique = 0 };
        if (MainGaucheEstActive)
        {
            if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else MainDroite = nouveauSlot;
        }
        else
        {
            if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else MainGauche = nouveauSlot;
        }
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
        if (mainActive.ID == 106)
            return (0.88f, 0.12f, 2.05f);
        if (mainActive.ID == IdObjetPiochePierreTier0)
            return (0.92f, 0.08f, 2.25f);
        if (mainActive.ID == IdObjetPellePierreTier0)
            return (0.26f, 0.95f, 2.15f);
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
        else if (mainActive.ID == 106)
            epaisseurLame = 0.065f;
        else if (mainActive.ID == IdObjetPiochePierreTier0)
            epaisseurLame = 0.06f;
        else if (mainActive.ID == IdObjetPellePierreTier0)
            epaisseurLame = 0.09f;

        return epaisseurLame;
    }

    /// <summary>True si la pointe (manche→lame) est alignée sur la visée caméra→cible — les rotations R / Maj+R / Ctrl+R sur l’objet en main sont prises en compte via <see cref="GlobalTransform"/>.</summary>
    private bool EstFrappeDagueAvecLaLame(Vector3 pointImpact, Vector3 directionFrappe)
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

        const float seuil = 0.15f;
        return Mathf.Max(alignVisée, alignMouvement) > seuil;
    }

    /// <summary>Hachette 106 : lame <c>tripo_part_4</c>, manche <c>tripo_part_5</c> (aligné avec <see cref="InstancierModeleArme"/> id 106).</summary>
    private bool EstFrappeHachette106AvecLaLame(Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (_objetEnMain == null || _camera == null) return false;
        var modele = _objetEnMain.FindChild("ModeleArme", true, false) as Node3D;
        if (modele == null) return false;
        MeshInstance3D lameMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_4")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_4");
        MeshInstance3D mancheMi = modele.GetNodeOrNull<MeshInstance3D>("tripo_part_5")
            ?? TrouverMeshInstanceDontLeNomContient(modele, "tripo_part_5");
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

        const float seuil = 0.15f;
        return Mathf.Max(alignVisée, alignMouvement) > seuil;
    }

    /// <summary>Relâchement clic gauche : sol → creusage / fauchage ; sinon frappe roches, arbres, rigides.</summary>
    private void ExecuterAction(float force, TypeMouvementFrappe mouvement)
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive)) return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
        {
            GD.Print("ZERO-K : Aucune collision sous la visée — rapprochez-vous du sol ou vérifiez le chargement des chunks.");
            return;
        }

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        Vector3 pointImpact = _rayon.GetCollisionPoint();

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (mouvement == TypeMouvementFrappe.DeHautEnBas) directionMouvement = -_camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.DeBasEnHaut) directionMouvement = _camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.GaucheADroite) directionMouvement = _camera.GlobalTransform.Basis.X.Normalized();
        else if (mouvement == TypeMouvementFrappe.DroiteAGauche) directionMouvement = -_camera.GlobalTransform.Basis.X.Normalized();

        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);

        if (EstSolViseParRayon(_rayon, objetTouche))
        {
            ExecuterCreusage(force, effPelle, masseOutil, pointImpact);
            return;
        }

        if (objetTouche == null)
        {
            GD.Print("ZERO-K : Objet touché non reconnu (ni sol ni rigide avec nœud).");
            return;
        }

        ExecuterFrappePhysique(force, effHache, masseOutil, objetTouche, pointImpact, directionMouvement);
    }

    private void JouerAnimationFrappe(TypeMouvementFrappe type)
    {
        if (_objetEnMain == null) return;
        bool visuelEnMain = _objetEnMain.Mesh != null || _objetEnMain.FindChild("ModeleArme", true, false) != null;
        if (!visuelEnMain) return;
        _tweenFrappe?.Kill();
        _tweenFrappe = CreateTween();

        MettreAJourObjetEnMain();

        Vector3 posCible = _objetEnMain.Position;
        Vector3 rotCible = _objetEnMain.RotationDegrees;

        if (type == TypeMouvementFrappe.Estoc) { posCible.Z -= 0.5f; rotCible.X -= 20f; }
        else if (type == TypeMouvementFrappe.DeHautEnBas) { posCible.Y -= 0.4f; rotCible.X -= 70f; }
        else if (type == TypeMouvementFrappe.DeBasEnHaut) { posCible.Y += 0.4f; rotCible.X += 70f; }
        else if (type == TypeMouvementFrappe.GaucheADroite) { posCible.X += 0.4f; rotCible.Y -= 70f; rotCible.Z -= 45f; }
        else if (type == TypeMouvementFrappe.DroiteAGauche) { posCible.X -= 0.4f; rotCible.Y += 70f; rotCible.Z += 45f; }

        _tweenFrappe.TweenProperty(_objetEnMain, "position", posCible, 0.08f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tweenFrappe.Parallel().TweenProperty(_objetEnMain, "rotation_degrees", rotCible, 0.08f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tweenFrappe.TweenCallback(Callable.From(ReposerObjetEnMainApresFrappe)).SetDelay(0.15f);
    }

    private void ReposerObjetEnMainApresFrappe()
    {
        _objetEnMain.Position = new Vector3(0.3f, -0.25f, -0.8f);
        MettreAJourObjetEnMain();
    }

    private void ExecuterCreusage(float force, float efficacitePelle, float masseOutil, Vector3 pointImpact)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        if (efficacitePelle < 0.6f)
        {
            // Fauchage : dague (105), roche plate (1) ou en pointe (3), ou éclat — pas la hachette (106), inadaptée au gazon fin.
            bool estOutilFaucheur = mainActive.ID == 105
                || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && (mainActive.IndexMorphologique == 1 || mainActive.IndexMorphologique == 3))
                || mainActive.EstUnEclat;

            if (estOutilFaucheur)
            {
                _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 3.1f);
                if (mainActive.ID == 105)
                    AppliquerUsureOutilMainActive(0.75f);
                GD.Print("ZERO-K : Fauchage de la flore. Récolte de fibres en cours.");
                return;
            }
            GD.Print("ZERO-K : L'angle de cette lame ne permet pas de déplacer la terre. Il vous faut une surface plate (Pelle/Houe).");
            return;
        }

        float forceCreusage = masseOutil * force * efficacitePelle;
        if (mainActive.ID == IdObjetPellePierreTier0)
        {
            int idMatiereImpact = _gestionnaireMonde?.ObtenirMatiereExacte(pointImpact - (_rayon.GetCollisionNormal() * 0.45f)) ?? 0;
            // Pelle pierre tier0 : +5% uniquement sur terre/sable/terre aride.
            if (idMatiereImpact == 1 || idMatiereImpact == 3 || idMatiereImpact == 6)
                forceCreusage *= 1.05f;
        }

        if (forceCreusage > 10f)
        {
            GD.Print($"ZERO-K : Extraction du sol réussie. (Force Volume: {forceCreusage:F1})");
            if (mainActive.ID == 105 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0)
                AppliquerUsureOutilMainActive(3.2f);
        }
        else if (mainActive.ID == 105 && efficacitePelle >= 0.6f)
        {
            // Dague mal orientée en « pelle » : le creusage formel est trop faible, mais on gratte quand même un peu + fauchage herbe.
            _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpact, 0.95f, 4.5f);
            _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 2.8f);
            AppliquerUsureOutilMainActive(2.4f);
            GD.Print("ZERO-K : La dague racle la surface (coup orienté pelle, peu de pénétration).");
        }
        else
        {
            GD.Print("ZERO-K : Manque de force ou outil trop léger pour percer ce sol.");
        }
    }

    /// <summary>Arbres vivants/morts, roches, rigides — efficacité hache émergente.</summary>
    private void ExecuterFrappePhysique(float force, float efficaciteHache, float masseOutil, Node objetTouche, Vector3 pointImpact, Vector3 directionFrappe)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        if (efficaciteHache < 0.4f && masseOutil > 2f)
        {
            GD.Print("ZERO-K : REBOND MASSIF ! Vous frappez avec le plat de l'outil. Choc structurel violent !");
            return;
        }

        float multiplicateurLame = Mathf.Clamp(efficaciteHache * 20.0f, 1.0f, 40.0f);
        if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.5f);
        else if (mainActive.ID == 105 && EstFrappeDagueAvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.35f);
        else if (mainActive.ID == 106 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            multiplicateurLame = Mathf.Max(multiplicateurLame, 2.85f);
        else if (mainActive.EstUnEclat && mainActive.MeshEclat != null && mainActive.ID != 100)
            multiplicateurLame = Mathf.Min(multiplicateurLame, 40.0f);

        float forceImpact = (masseOutil * force * 15f) * multiplicateurLame;
        float epaisseurLame = CalculerEpaisseurLamePourImpact(mainActive, directionFrappe);

        if (objetTouche == null)
            return;

        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            bool outilTranchantPourArbre = mainActive.ID == 106
                || mainActive.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && (mainActive.IndexMorphologique == 1 || mainActive.IndexMorphologique == 3));
            if (!outilTranchantPourArbre) return;

            float forceCoupe = forceImpact;
            if (mainActive.EstUnEclat && arbre.AgeEnJours <= 2)
                forceCoupe = Mathf.Max(forceCoupe, arbre.AgeEnJours <= 1 ? 36f : 48f);
            if (mainActive.ID == 106)
                forceCoupe *= 1.14f;

            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, forceCoupe, epaisseurLame, mainActive.ID == 106);
            if (resultatCoupe == 0) GD.Print("ZERO-K : Rebond. La force d'impact est insuffisante pour entamer ce bois.");
            else if (resultatCoupe == 1) JouerSonEtEffetCoupeArbre(pointImpact);
            else if (resultatCoupe == 2) { JouerSonEtEffetCoupeArbre(pointImpact); GD.Print("ZERO-K : Arbre abattu."); }
            else if (resultatCoupe == 3) { JouerSonEtEffetCoupeArbre(pointImpact); GD.Print("ZERO-K : Branche amputée."); }
            return;
        }

        RigidBody3D rbCible = ResoudreRigidBodyDepuisCollider(objetTouche);
        if (rbCible == null) return;

        if (rbCible.Name.ToString().Contains("ArbreMort"))
        {
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = main.ID == 106
                || main.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(main.ID) && (main.IndexMorphologique == 1 || main.IndexMorphologique == 3));
            if (!outilTranchantPourArbre)
                return;

            // Étape 1 : arrachage du feuillage (une action par frappe)
            Node feuillage = rbCible.GetNodeOrNull("Feuillage");
            if (feuillage != null)
            {
                Material matFeuilles = (feuillage as MeshInstance3D)?.MaterialOverride?.Duplicate() as Material;
                feuillage.QueueFree();
                JouerSonEtEffetCoupeArbre(pointImpact);
                GD.Print("ZERO-K : Feuillage arraché du cadavre végétal.");
                int quantite = 3 + (int)(rbCible.Mass / 100f);
                Vector3 baseFeuillage = CalculerPointAuDessusSol(rbCible.GlobalPosition.Lerp(pointImpact, 0.5f) + Vector3.Up * 1.2f, 0.42f);
                for (int i = 0; i < quantite; i++)
                {
                    var bloc = BlocChutant.CreerFeuillageArrache(baseFeuillage, matFeuilles);
                    GetTree().CurrentScene.AddChild(bloc);
                    bloc.GlobalPosition = baseFeuillage + new Vector3(((float)GD.Randf() - 0.5f) * 0.65f, (float)i * 0.06f + 0.12f, ((float)GD.Randf() - 0.5f) * 0.65f);
                }
                return;
            }

            int age = rbCible.HasMeta("Age") ? (int)rbCible.GetMeta("Age").AsInt32() : 1;
            int branchesRestantes = rbCible.HasMeta("BranchesRestantes") ? (int)rbCible.GetMeta("BranchesRestantes").AsInt32() : 0;
            byte essenceBois = rbCible.HasMeta("IndexBotanique")
                ? (byte)Mathf.Clamp(rbCible.GetMeta("IndexBotanique").AsInt32(), 0, 255)
                : LSystem_Botanique.IndexChene;

            // Étape 2 : ébranchage (bâtons 32) avant débitage du tronc
            if (branchesRestantes > 0)
            {
                JouerSonEtEffetCoupeArbre(pointImpact);
                branchesRestantes--;
                rbCible.SetMeta("BranchesRestantes", branchesRestantes);
                GD.Print($"ZERO-K : Branche amputée. Reste : {branchesRestantes}");
                var slotBaton = new SlotInventaire
                {
                    ID = 32,
                    IndexBotanique = essenceBois,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One
                };
                // Surélève le spawn du bâton pour éviter le clip sous le sol
                Node3D baton = CreerBlocPose(pointImpact + directionFrappe * 0.2f + Vector3.Up * 0.8f, slotBaton);
                if (baton is RigidBody3D rbBaton)
                    rbBaton.ApplyCentralImpulse(directionFrappe * 3f);
                return;
            }

            // ÉTAPE 3 : LIBÉRATION DU TRONC BRUT UNIQUE
            bool peutLibererTronc = main.ID == 106
                || main.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(main.ID) && (main.IndexMorphologique == 1 || main.IndexMorphologique == 3));
            if (!peutLibererTronc)
            {
                GD.Print("ZERO-K : Il faut un tranchant : roche plate ou en pointe, éclat ou hachette.");
                return;
            }

            float hauteurTronc = rbCible.HasMeta("HauteurTronc") ? (float)rbCible.GetMeta("HauteurTronc").AsSingle() : 4.0f;
            float scaleZ = hauteurTronc / 1.2f; // Base du Tronc Brut = 1.2m

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

            CalculerDimensionsBoisPose(30, 0, 0, out float rayonTroncSpawn, out float longueurBaseTronc, out _, out _);
            float longueurTroncMonde = longueurBaseTronc * scaleZ;
            Vector3 refSpawn = rbCible.GlobalPosition.Lerp(pointImpact, 0.45f);
            float margeSol = rayonTroncSpawn + Mathf.Clamp(longueurTroncMonde * 0.22f, 0.25f, 1.35f);
            Vector3 posTronc = CalculerPointAuDessusSol(refSpawn + Vector3.Up * 1.5f, margeSol);
            Node3D leTronc = CreerBlocPose(posTronc, slotTroncLong);
            if (leTronc != null)
                leTronc.GlobalRotation = rbCible.GlobalRotation;

            rbCible.QueueFree();
            return;
        }

        if (rbCible.Name.ToString().Contains("BrancheMorte"))
        {
            var mainB = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = mainB.ID == 106
                || mainB.EstUnEclat
                || (ItemPhysique.EstIdRocheMatiere(mainB.ID) && (mainB.IndexMorphologique == 1 || mainB.IndexMorphologique == 3));
            if (!outilTranchantPourArbre) return;
            rbCible.ApplyCentralImpulse(directionFrappe * (10f * force));
            JouerSonEtEffetCoupeArbre(pointImpact);
            GD.Print("ZERO-K : Coup sur la branche tombée.");
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
            // Post-abattage (bois au sol) : standardisation/fente réservée à la hachette.
            if (mainActive.ID != 106)
            {
                GD.Print("ZERO-K : Il vous faut une Hachette (ID 106) pour standardiser/fendre le bois au sol.");
                rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);
                return;
            }
            if (!EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            {
                GD.Print("ZERO-K : Orientez le tranchant vers la cible — ce coup porte le manche ou le plat.");
                rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);
                return;
            }

            Vector3 axeBois = rbCible.GlobalTransform.Basis.Z.Normalized();
            float alignement = Mathf.Abs(directionFrappe.Normalized().Dot(axeBois));
            AppliquerUsureOutilMainActive(2.5f);

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
            rbCible.QueueFree();
            return;
        }

        rbCible.ApplyCentralImpulse(dirFrappeObj * impulsionFrappe);

        if ((mainActive.ID == 105 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0) && ItemPhysique.EstIdRocheMatiere(item.ID_Objet))
        {
            bool tranchantOk = mainActive.ID == 105
                ? EstFrappeDagueAvecLaLame(pointImpact, directionFrappe)
                : EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
            if (tranchantOk)
                GD.Print("ZERO-K : L’outil ne peut pas briser cette roche — trop léger. Il faut un choc contondant ou une pierre lancée.");
            else
                GD.Print("ZERO-K : Vous heurtez la pierre avec le manche ou le plat, sans effet de taille.");
            return;
        }

        if ((mainActive.ID == 105 && !EstFrappeDagueAvecLaLame(pointImpact, directionFrappe))
            || ((mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0) && !EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe)))
        {
            GD.Print("ZERO-K : Orientez le tranchant vers la cible — ce coup porte le manche ou le plat.");
            return;
        }

        Vector3 dirVue = (pointImpact - _camera.GlobalPosition).Normalized();
        int resultatFracture = item.SubirDegats(forceImpact, dirVue, pointImpact);
        if (resultatFracture == 0)
            GD.Print("ZERO-K : L'impact n'est pas assez puissant. La roche résonne mais ne cède pas (Rebond).");
        else if (mainActive.ID == 105 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0)
            AppliquerUsureOutilMainActive(2.15f + forceImpact * 0.017f);
    }

    /// <summary>Rayon vertical sur le masque collision 1 ; place un point au-dessus du sol (évite tronc/branches sous le terrain).</summary>
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
