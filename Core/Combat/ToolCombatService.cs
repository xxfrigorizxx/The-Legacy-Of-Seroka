using Godot;
using System;

public partial class Joueur
{
    private const float DureeMinageMainNueSecondes = 3.0f;
    private const float DureeMinagePiochePierreSecondes = 4.0f;
    private const float IntervalleParticulesMinageMainNue = 0.12f;
    private const float DureeRecuperationAtelierMainNue = 5.0f;
    private const float DureeRecuperationAtelierHachette = 2.85f;
    private const float DureeRecuperationRackMainNue = 2.8f;
    private const float DureeRecuperationRackHachette = 1.25f;
    private const float DureeRecuperationFondationBoisHachette = 15.0f;
    private const float DureeRecuperationFondationRochePioche = 15.0f;
    private const float DureeRecuperationFondationMixteOutil = 15.0f;
    private const float IntervalleParticulesRecuperationAtelier = 0.14f;
    private float _progressionMinageMainNue;
    private float _cooldownParticulesMinageMainNue;
    private float _progressionRecuperationAtelier;
    private float _cooldownParticulesRecuperationAtelier;
    private float _cooldownMessageRecuperationFondation;
    private ItemPhysique _atelierCibleRecuperation;
    private const float DureeRecolteBuissonOutilSecondes = 3.0f;
    private const float DureeRecolteLianeDagueSecondes = 2.0f;
    private const float RayonDetectionBuisson = 1.25f;
    private const float DistanceMaxViseeDirecteBuisson = 0.55f;
    private const float IntervalleParticulesMinageBuisson = 0.11f;
    private const float IntervalleParticulesMinageLiane = 0.10f;
    private const float DureeDepecageDagueCadavreSecondes = 3.0f;
    private const float IntervalleParticulesDepecageCadavre = 0.10f;
    private float _progressionRecolteBuisson;
    private float _cooldownParticulesMinageBuisson;
    private float _progressionRecolteLianeDague;
    private float _cooldownParticulesMinageLiane;
    private float _progressionDepecageCadavreDague;
    private float _cooldownParticulesDepecageCadavre;
    private float _tempsPerteCibleDepecageCadavre;
    private float _tempsPerteCibleLiane;
    private Vector3 _pointRecolteBuisson;
    private Vector3 _pointRecolteLiane;
    private Vector3 _pointDepecageCadavre;

    private Vector3I _posBuissonRecolte;
    private bool _aCibleBuissonRecolte;
    private ArbreVivant _arbreCibleLiane;
    private BoeufSauvage _boeufCadavreCibleDepecage;
    private bool _bloquerActionClicGaucheApresMinageBuisson;
    private bool _bloquerActionClicGaucheApresDepecage;

    private static bool EstMatiereMinableMainNue(int idMatiere)
    {
        // Main nue : sable + terres + neige (ID 5).
        return idMatiere == 1 || idMatiere == 3 || idMatiere == 5 || idMatiere == 6 || idMatiere == 7 || idMatiere == 8 || idMatiere == 9;
    }

    private static bool EstMatiereMinablePioche(int idMatiere)
    {
        // Pioche : roche voxel.
        return idMatiere == 2;
    }

    /// <summary>Roche matière plate, ovale ou en pointe : même convention que l’entaille d’<see cref="ArbreVivant"/> vivant.</summary>
    private static bool EstRocheTranchantePourBois(SlotInventaire slot)
    {
        return !slot.EstVide && ItemPhysique.EstIdRocheMatiere(slot.ID)
            && (slot.IndexMorphologique == 1 || slot.IndexMorphologique == 2 || slot.IndexMorphologique == 3);
    }

    /// <summary>
    /// Cadavre d'arbre : l'essence est normalement sur le <see cref="RigidBody3D"/> ; si la méta a été perdue,
    /// on la relit sur l'enfant « Bois » (copié à la chute depuis <see cref="ArbreVivant"/>).
    /// </summary>
    private static byte LireIndexBotaniqueBoisSurRigid(RigidBody3D rb)
    {
        if (rb == null) return LSystem_Botanique.IndexChene;
        if (rb.HasMeta("IndexBotanique"))
            return (byte)Mathf.Clamp(rb.GetMeta("IndexBotanique").AsInt32(), 0, 255);
        var bois = rb.GetNodeOrNull<MeshInstance3D>("Bois");
        if (bois != null && bois.HasMeta("IndexBotanique"))
            return (byte)Mathf.Clamp(bois.GetMeta("IndexBotanique").AsInt32(), 0, 255);
        return LSystem_Botanique.IndexChene;
    }

    /// <summary>Répare la méta sur le corps si elle manque encore (anciens cadavres) mais que « Bois » la porte.</summary>
    private static void ReparerMetaIndexBotaniqueSurCadavreSiPossible(RigidBody3D cadavre)
    {
        if (cadavre == null || cadavre.HasMeta("IndexBotanique")) return;
        var bois = cadavre.GetNodeOrNull<MeshInstance3D>("Bois");
        if (bois != null && bois.HasMeta("IndexBotanique"))
            cadavre.SetMeta("IndexBotanique", bois.GetMeta("IndexBotanique"));
    }

    private void ReinitialiserMinageMainNueProgression()
    {
        _progressionMinageMainNue = 0f;
        _cooldownParticulesMinageMainNue = 0f;
        _progressionRecuperationAtelier = 0f;
        _cooldownParticulesRecuperationAtelier = 0f;
        _cooldownMessageRecuperationFondation = 0f;
        _atelierCibleRecuperation = null;
        _progressionRecolteBuisson = 0f;
        _cooldownParticulesMinageBuisson = 0f;
        _aCibleBuissonRecolte = false;
        _pointRecolteBuisson = Vector3.Zero;
        _posBuissonRecolte = default;
        _bloquerActionClicGaucheApresMinageBuisson = false;
        ReinitialiserDepecageCadavreDagueProgression();
        ReinitialiserMinageLianeDagueProgression();
    }

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
        AppliquerUsureOutilMainActive(0.95f);
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

    /// <summary>Dague/Pelle : minage maintenu 3s sur buisson (dague coupe, pelle déracine replantable).</summary>
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
        if (_progressionRecolteBuisson < DureeRecolteBuissonOutilSecondes)
            return true;

        byte mode = dague ? (byte)1 : (byte)2;
        bool succes = _gestionnaireMonde?.RecolterBuissonGlobal(_pointRecolteBuisson, RayonDetectionBuisson, mode) ?? false;
        if (succes)
        {
            if (dague)
            {
                AppliquerUsureOutilMainActive(1.6f);
                GD.Print("ZERO-K : Dague: branche de buisson récoltée.");
            }
            else
            {
                AppliquerUsureOutilMainActive(2.1f);
                GD.Print("ZERO-K : Buisson déraciné (plante replantable récupérée).");
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
            if (id >= 1 && id <= 9)
                return id;
        }

        // Repli conservateur pour ne pas bloquer le minage si un cas limite est rencontré.
        int fallback = _gestionnaireMonde.ObtenirMatiereExacte(pointImpactVoxel - n * 0.5f);
        return (fallback >= 1 && fallback <= 9) ? fallback : 0;
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
        if (item == null || (item.ID_Objet != 200 && item.ID_Objet != IdObjetRackBatons && item.ID_Objet != IdObjetRackBuches)) return false;
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

    private void MettreAJourMinageMainNueOuAtelier(float dt, SlotInventaire mainActive)
    {
        _cooldownMessageRecuperationFondation = Mathf.Max(0f, _cooldownMessageRecuperationFondation - dt);
        bool mainVide = mainActive.EstVide;
        bool hachette = mainActive.ID == 106;
        bool dague = mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0;
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

            bool estRack = atelier.ID_Objet == IdObjetRackBatons || atelier.ID_Objet == IdObjetRackBuches;
            float duree = estRack
                ? (mainVide ? DureeRecuperationRackMainNue : DureeRecuperationRackHachette)
                : (mainVide ? DureeRecuperationAtelierMainNue : DureeRecuperationAtelierHachette);
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
            GD.Print(estRack
                ? (atelier.ID_Objet == IdObjetRackBatons ? "ZERO-K : Rack à bâtons récupéré dans l'inventaire." : "ZERO-K : Rack à bûches récupéré dans l'inventaire.")
                : "ZERO-K : Atelier récupéré dans l'inventaire.");
            ReinitialiserMinageMainNueProgression();
            return;
        }

        if (EssayerObtenirFondationSousVisee(out ItemPhysique fondation, out Vector3 pFondation, out Vector3 nFondation))
        {
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
            ReinitialiserMinageMainNueProgression();
            return;
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

        ExecuterMinageVoxelMainNue(pointImpactVoxel, idExtrait);
        ReinitialiserMinageMainNueProgression();
    }

    private void ExecuterMinageVoxelMainNue(Vector3 pointImpactVoxel, int idExtrait)
    {
        if (!EstMatiereMinableMainNue(idExtrait) && !EstMatiereMinablePioche(idExtrait))
            return;
        var nouveauSlot = new SlotInventaire { ID = idExtrait, IndexMorphologique = 0, IndexChimique = 0, Quantite = 1 };
        if (!ADeLaPlacePourSlotInventaire(nouveauSlot))
            return;

        _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpactVoxel, RAYON_SCULPTURE, 5.0f);
        if (!EssayerAjouterDansInventaire(nouveauSlot))
            return;
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
        BoeufSauvage boeufSousVisee = ObtenirBoeufDepuisCollider(objetTouche);
        Vector3 pointImpact = _rayon.GetCollisionPoint();

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        if (mouvement == TypeMouvementFrappe.DeHautEnBas) directionMouvement = -_camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.DeBasEnHaut) directionMouvement = _camera.GlobalTransform.Basis.Y.Normalized();
        else if (mouvement == TypeMouvementFrappe.GaucheADroite) directionMouvement = _camera.GlobalTransform.Basis.X.Normalized();
        else if (mouvement == TypeMouvementFrappe.DroiteAGauche) directionMouvement = -_camera.GlobalTransform.Basis.X.Normalized();

        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);

        if (boeufSousVisee == null && EstSolViseParRayon(_rayon, objetTouche))
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

        // Dague sur buisson: interdit en coup instantané, uniquement minage maintenu 3s.
        if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0) && (_gestionnaireMonde?.EssayerDetecterBuissonSousPoint(pointImpact, RayonDetectionBuisson, out Vector3 posBuisson, out _)) == true
            && pointImpact.DistanceTo(posBuisson) <= DistanceMaxViseeDirecteBuisson)
        {
            GD.Print(mainActive.ID == IdObjetFauxPierreTier0
                ? "ZERO-K : Maintenez 3s avec la faux pour couper le buisson."
                : "ZERO-K : Maintenez 3s avec la dague pour couper le buisson.");
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

        if (efficacitePelle < 0.6f)
        {
            // Fauchage : dague (105), roche plate (1) ou en pointe (3), ou éclat — pas la hachette (106), inadaptée au gazon fin.
            bool estRocheFaucheuse = ItemPhysique.EstIdRocheMatiere(mainActive.ID) && (mainActive.IndexMorphologique == 1 || mainActive.IndexMorphologique == 3);
            bool estOutilFaucheur = mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0
                || estRocheFaucheuse
                || mainActive.EstUnEclat;

            if (estOutilFaucheur)
            {
                _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 3.1f);
                if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0)
                    AppliquerUsureOutilMainActive(mainActive.ID == IdObjetFauxPierreTier0 ? 0.78f : 0.75f);
                if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0)
                    AjouterXpFutureState("Dextiriter", 1UL);
                else if (estRocheFaucheuse && ObtenirNiveauFutureState("Dextiriter") < 15UL)
                    AjouterXpFutureState("Dextiriter", 1UL);
                GD.Print("ZERO-K : Fauchage de la flore. Récolte de fibres en cours.");
                return;
            }
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
        else if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0) && efficacitePelle >= 0.6f)
        {
            // Dague mal orientée en « pelle » : le creusage formel est trop faible, mais on gratte quand même un peu + fauchage herbe.
            _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpact, 0.95f, 4.5f);
            _gestionnaireMonde?.AppliquerFauchageGlobal(pointImpact, 2.8f);
            AppliquerUsureOutilMainActive(2.4f);
            GD.Print(mainActive.ID == IdObjetFauxPierreTier0
                ? "ZERO-K : La faux racle la surface (coup orienté pelle, peu de pénétration)."
                : "ZERO-K : La dague racle la surface (coup orienté pelle, peu de pénétration).");
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

    /// <summary>Arbres vivants/morts, roches, rigides — efficacité hache émergente.</summary>
    private void ExecuterFrappePhysique(float force, float efficaciteHache, float masseOutil, Node objetTouche, Vector3 pointImpact, Vector3 directionFrappe, TypeMouvementFrappe mouvement)
    {
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        float multiplicateurForce = ObtenirMultiplicateurDegatsForce();

        // Évite un « soft-lock » : avec pelle/outil lourd ou mauvais angle, efficaciteHache peut chuter avant d'atteindre ArbreMort.
        RigidBody3D probeCadavre = ResoudreRigidBodyDepuisCollider(objetTouche);
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
        float epaisseurLame = CalculerEpaisseurLamePourImpact(mainActive, directionFrappe);

        if (objetTouche == null)
            return;

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
            forceCoupe += ObtenirBonusDegatsArbreBucheron();

            bool hachetteBonneOrientation = mainActive.ID == 106 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
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
            }
            else if (resultatCoupe == 3)
            {
                JouerSonEtEffetCoupeArbre(pointImpact);
                bool brancheMorte = arbre.IndexBotanique == LSystem_Botanique.IndexCheneMort || arbre.IndexBotanique == LSystem_Botanique.IndexBouleauMort;
                GD.Print(brancheMorte
                    ? "ZERO-K : Branche morte amputée — branche au sol (essence conservée)."
                    : "ZERO-K : Branche amputée — branche au sol (essence conservée).");
            }

            if (mainActive.ID == 106 && resultatCoupe > 0)
            {
                int idCasse = AppliquerUsureOutilMainActive(2.0f);
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
                return;
            }
            bool etaitCadavreDepecable = boeufTouche.EstCadavreDepecable();

            bool tranchant = false;
            if (mainActive.ID == 105)
                tranchant = EstFrappeDagueAvecLaLame(pointImpact, directionFrappe);
            else if (mainActive.ID == IdObjetFauxPierreTier0)
                tranchant = EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe);
            else if (mainActive.ID == 106 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetPellePierreTier0)
                tranchant = EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
            else if (mainActive.ID == IdObjetLancePierreTier0)
                tranchant = EstFrappeLance111AvecLaPointe(pointImpact, directionFrappe);
            else if (mainActive.EstUnEclat)
                tranchant = efficaciteHache > 0.45f;
            else if (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && mainActive.IndexMorphologique == 3)
                tranchant = true;

            Vector3 dirAvantCamera = _camera != null ? -_camera.GlobalTransform.Basis.Z.Normalized() : directionFrappe.Normalized();
            float alignPointee = Mathf.Clamp(directionFrappe.Normalized().Dot(dirAvantCamera), -1f, 1f);
            bool perforant = tranchant && alignPointee > 0.68f && (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetLancePierreTier0 || mainActive.ID == 100 || mainActive.EstUnEclat);
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
            if (applique && (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetLancePierreTier0 || mainActive.ID == 100))
                AppliquerUsureOutilMainActive(0.85f + (baseDegats * 0.024f));
            return;
        }

        RigidBody3D rbCible = ResoudreRigidBodyDepuisCollider(objetTouche);
        if (rbCible == null) return;

        if (rbCible.Name.ToString().Contains("ArbreMort"))
        {
            ReparerMetaIndexBotaniqueSurCadavreSiPossible(rbCible);
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool fauxSurCadavre = main.ID == IdObjetFauxPierreTier0 && EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe);
            bool outilTranchantPourArbre = main.ID == 106
                || main.EstUnEclat
                || EstRocheTranchantePourBois(main)
                || fauxSurCadavre;
            if (!outilTranchantPourArbre)
            {
                GD.Print("ZERO-K : Cadavre d'arbre — utilisez une hachette (106), un éclat, une roche plate/pointe, ou la lame de la faux (orientation tranchante). Pas la pelle ni la pioche comme tranchant.");
                return;
            }

            // Étape 1 : arrachage du feuillage (une action par frappe) — uniquement si le mesh existe (sinon enchaîner branches / tronc).
            Node feuillage = rbCible.GetNodeOrNull("Feuillage");
            if (feuillage is MeshInstance3D miFeu && miFeu.Mesh != null)
            {
                Mesh meshFeuillage = miFeu.Mesh;
                Material matFeuilles = miFeu.MaterialOverride?.Duplicate() as Material;
                miFeu.QueueFree();
                JouerSonEtEffetCoupeArbre(pointImpact);
                GD.Print("ZERO-K : Feuillage arraché du cadavre végétal.");
                int quantite = 3 + (int)(rbCible.Mass / 100f);
                Vector3 baseFeuillage = CalculerPointAuDessusSol(rbCible.GlobalPosition.Lerp(pointImpact, 0.5f) + Vector3.Up * 1.2f, 0.42f);
                for (int i = 0; i < quantite; i++)
                {
                    var bloc = BlocChutant.CreerFeuillageArrache(baseFeuillage, matFeuilles, meshFeuillage);
                    GetTree().CurrentScene.AddChild(bloc);
                    bloc.GlobalPosition = baseFeuillage + new Vector3(((float)GD.Randf() - 0.5f) * 0.65f, (float)i * 0.06f + 0.12f, ((float)GD.Randf() - 0.5f) * 0.65f);
                }
                return;
            }

            if (feuillage != null)
                feuillage.QueueFree();

            int age = rbCible.HasMeta("Age") ? (int)rbCible.GetMeta("Age").AsInt32() : 1;
            int branchesRestantes = rbCible.HasMeta("BranchesRestantes") ? (int)rbCible.GetMeta("BranchesRestantes").AsInt32() : 0;
            // Migration/standardisation: anciens cadavres peuvent avoir des valeurs absurdes (incoupables ou spam bâtons).
            branchesRestantes = Mathf.Clamp(branchesRestantes, 0, 10);
            byte essenceBois = LireIndexBotaniqueBoisSurRigid(rbCible);
            // Faux : bâtonnage / petit bois « brut » — pas la même essence que le fût (évite bâton = essence du cadavre).
            byte essenceBrancheAuSol = main.ID == IdObjetFauxPierreTier0 ? LSystem_Botanique.IndexChene : essenceBois;

            // Étape 2 : ébranchage (BlocChutant branche, essence) avant débitage du tronc
            if (branchesRestantes > 0)
            {
                JouerSonEtEffetCoupeArbre(pointImpact);
                branchesRestantes--;
                rbCible.SetMeta("BranchesRestantes", branchesRestantes);
                bool brancheMorte = essenceBois == LSystem_Botanique.IndexCheneMort || essenceBois == LSystem_Botanique.IndexBouleauMort;
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
                return;
            }

            // ÉTAPE 3 : LIBÉRATION DU TRONC BRUT UNIQUE
            bool peutLibererTronc = main.ID == 106
                || main.EstUnEclat
                || EstRocheTranchantePourBois(main);
            if (!peutLibererTronc)
            {
                AlerteSqueletteBoiteNoire("Il faut un tranchant: roche plate ou en pointe, eclat ou hachette.");
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
            bool fauxSurBrancheMorte = mainB.ID == IdObjetFauxPierreTier0 && EstFrappeFaux112AvecLaLame(pointImpact, directionFrappe);
            bool outilTranchantPourArbre = mainB.ID == 106
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
            if (mainActive.ID != 106)
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

        if ((mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetLancePierreTier0) && ItemPhysique.EstIdRocheMatiere(item.ID_Objet))
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
            || ((mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0) && !EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe))
            || (mainActive.ID == IdObjetLancePierreTier0 && !EstFrappeLance111AvecLaPointe(pointImpact, directionFrappe)))
        {
            AlerteSqueletteBoiteNoire("Oriente le tranchant vers la cible: ce coup porte le manche ou le plat.");
            return;
        }

        Vector3 dirVue = (pointImpact - _camera.GlobalPosition).Normalized();
        int resultatFracture = item.SubirDegats(forceImpact, dirVue, pointImpact);
        if (resultatFracture == 0)
            GD.Print("ZERO-K : L'impact n'est pas assez puissant. La roche résonne mais ne cède pas (Rebond).");
        else if (mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0 || mainActive.ID == 106 || mainActive.ID == IdObjetPellePierreTier0 || mainActive.ID == IdObjetPiochePierreTier0 || mainActive.ID == IdObjetLancePierreTier0)
            AppliquerUsureOutilMainActive(2.15f + forceImpact * 0.017f);
    }

    /// <summary>Rayon vertical sur le masque collision 1 ; place un point au-dessus du sol (évite tronc/branches sous le terrain).</summary>
    private void ExecuterLootDepecageCadavreBoeuf(BoeufSauvage boeuf, Vector3 pointImpact, Vector3 directionFrappe)
    {
        if (boeuf == null || !GodotObject.IsInstanceValid(boeuf))
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
