using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private const float RayonInteractionBaiesBuisson = 1.2f;
    private const float FondationPasSnapMetres = 4f;
    private const float FondationPenetrationMetres = 0.015f;
    private const float FondationDistanceCentreAdjacente = FondationPasSnapMetres - FondationPenetrationMetres;
    private const float FondationRayonSnapDouxMetres = 4.8f;
    private const float FondationToleranceAxePrincipalMetres = 0.12f;
    private const float FondationToleranceAxeSecondaireMetres = 0.12f;
    private const float NormaleSupportStructureMinY = 0.6f;
    private const float MargeChevauchementMetres = 0.02f;
    private const float MargeEmpilementStructureMetres = 0.01f;
    private const float PasRotationStructuresFixesDegres = 15f;

    /// <summary>Ouvre le conteneur/station sous visée (atelier 200, table analyse 131, racks 109/110, coffre 113, pit roche 122).</summary>
    private bool EssayerOuvrirAtelierSousVisee()
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var itemTouche = objetTouche as ItemPhysique
            ?? (objetTouche as Node)?.GetParent() as ItemPhysique
            ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (itemTouche == null || _menuAnatomie == null)
            return false;
        int idT = itemTouche.ID_Objet;
        if (idT != 200 && idT != IdObjetTableAnalyseTier1 && idT != IdObjetRackBatons && idT != IdObjetRackBuches && idT != IdObjetCoffreBoisTier0 && idT != IdObjetPitFeuRoche)
            return false;

        if (idT == IdObjetTableAnalyseTier1)
        {
            CraftGrille3x3AuTable = false;
            AtelierPlanTravailOuvert = null;
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
            StockageCoffreOuvert = false;
            CoffreOuvert = null;

            _menuAnatomie.OuvrirAnalyseurDepuisMonde(tier1: true, itemTouche);
            GetViewport().SetInputAsHandled();
            GD.Print("ZERO-K : Table d'analyse tier 1 ouverte.");
            return true;
        }
        OuvrirAnalyseurManuel();
        if (idT == 200)
        {
            AtelierPlanTravailOuvert = itemTouche;
            CraftGrille3x3AuTable = true;
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
            StockageCoffreOuvert = false;
            CoffreOuvert = null;
        }
        else if (idT == IdObjetCoffreBoisTier0)
        {
            CoffreOuvert = itemTouche;
            StockageCoffreOuvert = true;
            CraftGrille3x3AuTable = true;
            AtelierPlanTravailOuvert = null;
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
        }
        else
        {
            RackBatonsOuvert = itemTouche;
            StockageRackBatonsOuvert = true;
            CraftGrille3x3AuTable = true;
            AtelierPlanTravailOuvert = null;
            StockageCoffreOuvert = false;
            CoffreOuvert = null;
            if (itemTouche.ID_Objet == IdObjetRackBatons)
                SynchroniserVisuelRackBatons(itemTouche);
            else if (itemTouche.ID_Objet == IdObjetRackBuches)
                SynchroniserVisuelRackBuches(itemTouche);
        }
        if (!_menuAnatomie.EstOuvert)
            _menuAnatomie.BasculerVisibilite();
        else
            _menuAnatomie.RafraichirMenu();
        GetViewport().SetInputAsHandled();
        GD.Print(idT == 200
            ? "ZERO-K : Plan de travail 3x3 de l'Atelier ouvert."
            : (idT == IdObjetRackBatons ? "ZERO-K : Rack à bâtons ouvert."
                : (idT == IdObjetRackBuches ? "ZERO-K : Rack à bûches ouvert."
                    : (idT == IdObjetPitFeuRoche ? "ZERO-K : Pit à feu roche ouvert." : "ZERO-K : Coffre en bois ouvert."))));
        return true;
    }

    /// <summary>Clic gauche avec allume-feu : allume le pit visé, puis retire 1 point de durabilité.</summary>
    private bool EssayerAllumerPitFeuSousVisee(ref SlotInventaire mainActive)
    {
        if (mainActive.EstVide || mainActive.ID != IdObjetAllumeFeu)
            return false;
        Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref mainActive);
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var itemTouche = objetTouche as ItemPhysique
            ?? (objetTouche as Node)?.GetParent() as ItemPhysique
            ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (itemTouche == null || (itemTouche.ID_Objet != IdObjetPitFeu && itemTouche.ID_Objet != IdObjetPitFeuRoche))
            return false;
        if (itemTouche.EstPitFeuAllume())
        {
            GD.Print("ZERO-K : Ce pit à feu est déjà allumé.");
            return false;
        }
        bool active = itemTouche.ID_Objet == IdObjetPitFeuRoche
            ? itemTouche.ActiverPitFeuRocheAllume(300.0)
            : itemTouche.ActiverPitFeuAllume(300.0);
        if (!active)
        {
            if (itemTouche.ID_Objet == IdObjetPitFeuRoche)
                GD.Print("ZERO-K : Pit à feu roche vide — ajoutez des bâtons/branches avant l'allumage.");
            return false;
        }

        mainActive.DurabiliteOutilActuelle = Mathf.Max(0f, mainActive.DurabiliteOutilActuelle - 1f);
        if (mainActive.DurabiliteOutilActuelle <= 0.001f)
        {
            GD.Print("ZERO-K : L'allume-feu s'est brisé.");
            mainActive = new SlotInventaire();
        }

        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>Clic gauche avec pelle : éteint immédiatement un pit à feu roche allumé.</summary>
    private bool EssayerEteindrePitFeuRocheSousVisee(SlotInventaire mainActive)
    {
        if (mainActive.EstVide || mainActive.ID != IdObjetPellePierreTier0)
            return false;
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var itemTouche = objetTouche as ItemPhysique
            ?? (objetTouche as Node)?.GetParent() as ItemPhysique
            ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (itemTouche == null || itemTouche.ID_Objet != IdObjetPitFeuRoche || !itemTouche.EstPitFeuRocheAllume())
            return false;
        if (!itemTouche.EteindrePitFeuRoche())
            return false;
        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
        GD.Print("ZERO-K : Pit à feu roche éteint à la pelle.");
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>Clic droit : ajoute 1 bâton/branche dans un pit à feu roche visé.</summary>
    private bool EssayerAjouterCombustiblePitFeuRocheSousVisee(ref SlotInventaire mainActive)
    {
        if (mainActive.EstVide || (mainActive.ID != 32 && mainActive.ID != BlocChutant.ID_BRANCHE))
            return false;
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var itemTouche = objetTouche as ItemPhysique
            ?? (objetTouche as Node)?.GetParent() as ItemPhysique
            ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (itemTouche == null || itemTouche.ID_Objet != IdObjetPitFeuRoche)
            return false;
        if (!itemTouche.AjouterCombustiblePitFeuRoche(1, mainActive.ID))
            return false;
        ConsommerUneUniteMainActive();
        if (!Engine.IsEditorHint())
            SauvegarderEtatPersistantMonde(GetTree());
        RafraichirHUD();
        GD.Print($"ZERO-K : Combustible ajouté au pit roche ({itemTouche.ObtenirStockCombustiblePitFeuRoche()} unité(s)).");
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>E : ramassage uniquement (plus de pose/attache via E).</summary>
    private void ExecuterToucheInteragir()
    {
        if (EssayerRamasserBaiesBuissonSousVisee())
            return;
        ExecuterRamassageObjet();
    }

    /// <summary>Cueillette instantanée des baies sur buisson plein sous la visée (le buisson passe visuellement en vide).</summary>
    private bool EssayerRamasserBaiesBuissonSousVisee()
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding() || _gestionnaireMonde == null) return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSolViseParRayon(_rayon, objetTouche)) return false;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        if (!_gestionnaireMonde.EssayerDetecterBuissonSousPoint(pointImpact, RayonInteractionBaiesBuisson, out _, out byte typeFlore))
            return false;
        if (!Chunk_Serveur.EstBuissonPlein(typeFlore))
        {
            GD.Print("ZERO-K : Ce buisson est vide, aucune baie à ramasser.");
            return true;
        }

        byte couleurPourTest = (byte)Joueur.IndexCouleurBaieDepuisVariante(
            Chunk_Serveur.ObtenirVarianteBuisson(typeFlore));
        var slotTest = new SlotInventaire { ID = IdObjetBaie, IndexChimique = couleurPourTest, Quantite = 1 };
        if (!ADeLaPlacePourSlotInventaire(slotTest))
        {
            GD.Print("ZERO-K : Inventaire plein, impossible de cueillir les baies.");
            return true;
        }

        if (!_gestionnaireMonde.RecolterBaiesBuissonSousPoint(pointImpact, RayonInteractionBaiesBuisson, out int quantite, out byte couleur))
            return false;

        quantite = Mathf.Clamp(quantite, 1, 4);
        int restant = quantite;
        while (restant > 0)
        {
            var s = new SlotInventaire
            {
                ID = IdObjetBaie,
                IndexChimique = couleur,
                Quantite = restant,
                IndexMorphologique = 0,
                IndexTaille = 0,
                IndexBotanique = LSystem_Botanique.IndexChene
            };
            if (EssayerAjouterDansInventaire(s))
            {
                restant = 0;
                break;
            }
            restant--;
        }

        int ajoutees = quantite - restant;
        if (ajoutees > 0)
        {
            RafraichirHUD();
            bool pl = ajoutees > 1;
            string adj = Joueur.ObtenirAdjectifBaieAccorde(couleur, pl);
            GD.Print($"ZERO-K : {ajoutees} baie{(pl ? "s" : "")} {adj} récoltée{(pl ? "s" : "")} sur le buisson.");
        }
        else
        {
            GD.Print("ZERO-K : Aucune baie ajoutée (inventaire saturé).");
        }
        return true;
    }

    private void ConsommerUneUniteMainActive()
    {
        ref SlotInventaire s = ref (MainGaucheEstActive ? ref MainGauche : ref MainDroite);
        if (s.EstVide) return;
        int q = ObtenirQuantiteSlot(s) - 1;
        if (q <= 0) s = new SlotInventaire();
        else s.Quantite = q;
    }

    private static bool TenterEmpilementComplet(ref SlotInventaire destination, SlotInventaire source)
    {
        if (destination.EstVide || source.EstVide) return false;
        if (!SontEmpilables(destination, source)) return false;
        int max = ObtenirPileMax(destination);
        int qDst = ObtenirQuantiteSlot(destination);
        int qSrc = ObtenirQuantiteSlot(source);
        if (qDst + qSrc > max) return false;
        destination.Quantite = qDst + qSrc;
        return true;
    }

    /// <summary>True si la corde ou la fibre peut « s'étirer » visuellement (ScaleEclat) : les deux brins de la corde doivent être étirables.</summary>
    public static bool ObtenirSlotFlexibleEtirable(SlotInventaire s)
    {
        if (s.ID == 20 || s.ID == 21)
        {
            bool a = Atlas_Matiere.ObtenirProfilFlexible(s.IndexChimique, out var pa) && pa.Etirable;
            bool b = Atlas_Matiere.ObtenirProfilFlexible(s.IndexMorphologique, out var pb) && pb.Etirable;
            return a && b;
        }
        if (EstMatiereFlexible(s.ID))
            return Atlas_Matiere.ObtenirProfilFlexible(s.ID, out var p) && p.Etirable;
        return false;
    }

    /// <summary>Échelle pour l’établi CAO (hors 30/32, gérés à part) : fibres/corde non élastiques = taille naturelle, sans ScaleEclat « étiré ».</summary>
    public static Vector3 ObtenirEchellePieceFlexibleCAO(SlotInventaire slot)
    {
        bool estFlexOuCorde = slot.ID == 15 || slot.ID == 16 || slot.ID == 17 || slot.ID == 20 || slot.ID == 21 || slot.ID == IdObjetCeinturePoches || slot.ID == IdObjetCeintureSacoches || slot.ID == IdObjetPochetteTier0 || slot.ID == IdObjetSacTier0;
        if (estFlexOuCorde && !ObtenirSlotFlexibleEtirable(slot))
            return Vector3.One;
        if (slot.ScaleEclat != Vector3.Zero)
            return slot.ScaleEclat;
        return Vector3.One;
    }

    /// <summary>Fibres + corde : manipulation fine sur le plan de l’établi (rayon réduit).</summary>
    public static bool EstFlexibleOuCordePourPlanCAO(int idObjet) => idObjet is 15 or 16 or 17 or 20 or 21;

    private static bool EstIdTerrainVoxelPosable(int id) => id >= 1 && id <= 9 && id != 4;

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (EstIdTerrainVoxelPosable(s.ID)) return true;
        return s.ID == 999 || s.ID == 10 || s.ID == 11 || s.ID == BlocChutant.ID_BRANCHE || s.ID == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(s.ID) || s.ID == 30 || s.ID == 32 || s.ID == 34 || s.ID == 21 || s.ID == IdObjetCeinturePoches || s.ID == IdObjetCeintureSacoches || s.ID == IdObjetPochetteTier0 || s.ID == IdObjetSacTier0 || s.ID == IdObjetHachePierreTier1 || s.ID == IdObjetAtelleJambe || s.ID == IdObjetPellePierreTier0 || s.ID == IdObjetPiochePierreTier0 || s.ID == IdObjetLancePierreTier0 || s.ID == IdObjetFauxPierreTier0 || s.ID == IdObjetAllumeFeu || s.ID == 200 || s.ID == IdObjetTableAnalyseTier1 || s.ID == IdObjetRackBatons || s.ID == IdObjetRackBuches || s.ID == IdObjetCoffreBoisTier0 || s.ID == IdObjetPitFeuRoche || s.ID == IdObjetMortierPilonBois || EstIdFondation(s.ID);
    }

    /// <summary>Corde (20) : accrocher au point de visée si surface valide (sol, roche, arbre, bloc posé).</summary>
    private bool ExecuterAttacheCordeSiPossible(SlotInventaire mainCorde)
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return false;
        Node col = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (col == null) return false;
        if (col == this || col.IsAncestorOf(this) || IsAncestorOf(col)) return false;

        bool ancre = col is StaticBody3D || col is RigidBody3D || ResoudreRigidBodyDepuisCollider(col) != null || col.IsInGroup("BlocsPoses") || ObtenirArbreDepuisCollider(col) != null;
        if (!ancre) return false;

        Vector3 pt = _rayon.GetCollisionPoint();
        Vector3 n = _rayon.GetCollisionNormal().Normalized();
        Vector3 tangent = Vector3.Up.Cross(n);
        if (tangent.LengthSquared() < 1e-4f) tangent = Vector3.Right.Cross(n);
        tangent = tangent.Normalized();

        Node3D corps = CreerBlocPose(pt + n * 0.07f, mainCorde);
        if (corps == null) return false;
        corps.SetMeta("Corde_Accrochee", true);
        corps.SetMeta("Corde_Normal", n);
        var b = Basis.LookingAt(tangent, n).Orthonormalized();
        corps.GlobalTransform = new Transform3D(b, corps.GlobalPosition);

        ConsommerUneUniteMainActive();
        RafraichirHUD();
        GD.Print("ZERO-K : Corde accrochée à la surface (E).");
        return true;
    }

    /// <summary>Pose via E : portée courte pour fibres/corde, normale pour le reste. Respecte l’élasticité (pas d’étirement si non élastique).</summary>
    private void ExecuterPlacementDepuisInteragir(SlotInventaire mainActive)
    {
        ExecuterPlacementAvecOptions(mainActive, depuisInteragir: true);
    }

    private static string LireGenomeSurItemPhysique(ItemPhysique item)
    {
        if (item == null) return "";
        if (!string.IsNullOrEmpty(item.GenomeAssemblage)) return item.GenomeAssemblage;
        return item.HasMeta(MetaGenomeAssemblage) ? item.GetMeta(MetaGenomeAssemblage).AsString() : "";
    }

    /// <summary>Avant ajout inventaire : copie la grille du coffre posé vers la mémoire (même clé que <see cref="SlotInventaire.CleConteneur"/>).</summary>
    private void PreparerRamassageCoffreVersInventaire(ref SlotInventaire nouveauSlot, Node noeudTouche)
    {
        if (nouveauSlot.EstVide || nouveauSlot.ID != IdObjetCoffreBoisTier0) return;
        var item = noeudTouche as ItemPhysique
            ?? (noeudTouche as Node)?.GetParent() as ItemPhysique
            ?? (noeudTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (item == null) return;
        if (string.IsNullOrEmpty(nouveauSlot.CleConteneur))
            nouveauSlot.CleConteneur = Guid.NewGuid().ToString("N");
        MemoriserContenuCoffreDepuisItem(item, nouveauSlot.CleConteneur);
    }

    /// <summary>Phase 2 pure : ramassage des objets physiques (Caillou, Silex, BlocsPoses). Touche E (interagir).
    /// Copie IndexCacheMemoire dans le SlotInventaire pour conserver la forme exacte.</summary>
    private void ExecuterRamassageObjet()
    {
        if (!_rayon.IsColliding()) return;

        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (objetTouche == null) return;

        SlotInventaire nouveauSlot = default;

        if (objetTouche.IsInGroup("BlocsPoses"))
        {
            int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
            if (id == 200 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || EstIdPitFeu(id) || EstIdFondation(id))
            {
                GD.Print("ZERO-K : Structure fixée au monde. Récupération uniquement par minage.");
                return;
            }
            var item = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            byte indexBotaniqueRamasse = LSystem_Botanique.IndexChene;
            if (item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0 || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet)))
                indexBotaniqueRamasse = item.IndexBotanique;
            else if ((id == BlocChutant.ID_BRANCHE || id == BlocChutant.ID_BOIS) && objetTouche.HasMeta("IndexBotanique"))
                indexBotaniqueRamasse = (byte)Mathf.Clamp(objetTouche.GetMeta("IndexBotanique").AsInt32(), 0, 255);
            int morphoBlocPose = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE)
                ? MorphologieBoisDepuisItem(item)
                : (item?.IndexCacheMemoire ?? 0);
            if (id == BlocChutant.ID_BRANCHE && objetTouche.HasMeta(BlocChutant.MetaBrancheTailléeBuisson) && objetTouche.GetMeta(BlocChutant.MetaBrancheTailléeBuisson).AsBool())
                morphoBlocPose = 1;
            nouveauSlot = new SlotInventaire
            {
                ID = id,
                IndexMorphologique = morphoBlocPose,
                IndexChimique = item?.IndexChimique ?? 0,
                IndexTaille = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE)
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item != null && (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) ? item.IndexTailleRoche : (id == BlocChutant.ID_BRANCHE ? 2 : 0)),
                IndexTailleLameRoche = item != null && (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : (item != null && (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item?.EstUnEclat ?? false,
                MeshEclat = (item != null && item.EstUnEclat) ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item?.NiveauFracture ?? 0,
                // FIX CRITIQUE : bois 30/32 → meta ScaleLongueurBois ou repli sur la longueur mesh
                ScaleEclat = item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE)
                    ? ScaleEclatBoisAuRamassage(item)
                    : (item != null ? item.Scale : Vector3.One),
                IndexBotanique = indexBotaniqueRamasse,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = (item != null && item.HasMeta("CleConteneur")) ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if ((nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetHachePierreTier1 || nouveauSlot.ID == IdObjetAtelleJambe || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0 || nouveauSlot.ID == IdObjetAllumeFeu) && item != null)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else if (objetTouche is RigidBody3D rb)
        {
            // BlocChutant (fibre, buisson tombé) : pas d'ItemPhysique, on lit le meta.
            if (objetTouche is BlocChutant)
            {
                int id = objetTouche.HasMeta("ID_Matiere") ? (int)objetTouche.GetMeta("ID_Matiere").AsInt32() : 1;
                byte idxBot = LSystem_Botanique.IndexChene;
                if (objetTouche.HasMeta("IndexBotanique"))
                    idxBot = (byte)Mathf.Clamp(objetTouche.GetMeta("IndexBotanique").AsInt32(), 0, 255);
                bool boisChute = id == BlocChutant.ID_BRANCHE || id == BlocChutant.ID_BOIS;
                int morphBranche = 0;
                if (id == BlocChutant.ID_BRANCHE && objetTouche.HasMeta(BlocChutant.MetaBrancheTailléeBuisson) && objetTouche.GetMeta(BlocChutant.MetaBrancheTailléeBuisson).AsBool())
                    morphBranche = 1; // 1 = branche buisson taillée (réinjectée au jet)
                nouveauSlot = new SlotInventaire
                {
                    ID = id,
                    IndexMorphologique = morphBranche,
                    IndexChimique = id == BlocChutant.ID_BAIE && objetTouche.HasMeta(BlocChutant.MetaIndexCouleurBaie)
                        ? Joueur.ClampIndexCouleurBaie((int)objetTouche.GetMeta(BlocChutant.MetaIndexCouleurBaie).AsInt32())
                        : 0,
                    IndexTaille = id == BlocChutant.ID_BRANCHE ? 2 : 0,
                    IndexBotanique = boisChute ? idxBot : LSystem_Botanique.IndexChene
                };
            }
            else
            {
            var item = rb as ItemPhysique ?? (rb as Node)?.GetParent() as ItemPhysique ?? rb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (item.ID_Objet == 200 || item.ID_Objet == IdObjetTableAnalyseTier1 || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet))
            {
                GD.Print("ZERO-K : Structure fixée au monde. Récupération uniquement par minage.");
                return;
            }
            if (ItemPhysique.EstIdRocheMatiere(item.ID_Objet) && item.IndexTailleRoche >= 3)
            {
                GD.Print("ZERO-K : Masse excessive. La colonne vertébrale céderait. Action bloquée.");
                return;
            }
            nouveauSlot = new SlotInventaire
            {
                ID = item.ID_Objet,
                IndexMorphologique = item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE ? MorphologieBoisDepuisItem(item) : item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 0),
                IndexTailleLameRoche = (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : ((item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == 200 || item.ID_Objet == IdObjetTableAnalyseTier1 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0 || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet))
                    ? item.IndexBotanique
                    : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = item.HasMeta("CleConteneur") ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetHachePierreTier1 || nouveauSlot.ID == IdObjetAtelleJambe || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0 || nouveauSlot.ID == IdObjetAllumeFeu)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
            }
        }
        else if (objetTouche is StaticBody3D sb)
        {
            var item = sb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (item.ID_Objet == 200 || item.ID_Objet == IdObjetTableAnalyseTier1 || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet))
            {
                GD.Print("ZERO-K : Structure fixée au monde. Récupération uniquement par minage.");
                return;
            }
            if (ItemPhysique.EstIdRocheMatiere(item.ID_Objet) && item.IndexTailleRoche >= 3)
            {
                GD.Print("ZERO-K : Masse excessive. La colonne vertébrale céderait. Action bloquée.");
                return;
            }
            nouveauSlot = new SlotInventaire
            {
                ID = item.ID_Objet,
                IndexMorphologique = item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE ? MorphologieBoisDepuisItem(item) : item.IndexCacheMemoire,
                IndexChimique = item.IndexChimique,
                IndexTaille = item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE
                    ? Mathf.Clamp(item.IndexTailleRoche, 0, 4)
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 0),
                IndexTailleLameRoche = (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : ((item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0 || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet))
                    ? item.IndexBotanique
                    : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = item.HasMeta("CleConteneur") ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetHachePierreTier1 || nouveauSlot.ID == IdObjetAtelleJambe || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0 || nouveauSlot.ID == IdObjetAllumeFeu)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else
            return;

        PreparerRamassageCoffreVersInventaire(ref nouveauSlot, objetTouche);
        ItemPhysique itemQuantitePose = null;
        if (objetTouche.IsInGroup("BlocsPoses"))
            itemQuantitePose = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        else if (objetTouche is RigidBody3D rbQ && objetTouche is not BlocChutant)
            itemQuantitePose = rbQ as ItemPhysique ?? (rbQ as Node)?.GetParent() as ItemPhysique ?? rbQ.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        else if (objetTouche is StaticBody3D sbQ)
            itemQuantitePose = sbQ.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (itemQuantitePose != null && itemQuantitePose.HasMeta(MetaQuantiteObjetPose))
            nouveauSlot.Quantite = (int)itemQuantitePose.GetMeta(MetaQuantiteObjetPose).AsInt32();
        else
            nouveauSlot.Quantite = ObtenirQuantiteSlot(nouveauSlot);
        if (!EssayerAjouterDansInventaire(nouveauSlot))
        {
            GD.Print("ZERO-K : Inventaire plein. Impossible de ramasser cet objet.");
            return;
        }
        objetTouche.QueueFree();
        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }

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
        if (distance < distMin) return;

        int id = mainActive.ID;
        if (id == 0) return;
        if (EstIdTerrainVoxelPosable(id))
        {
            _gestionnaireMonde?.AppliquerCreationGlobale(pointImpact, normaleImpact, RAYON_SCULPTURE, id);
        }
		else if (id == 10 || id == 11)
		{
			Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
			if (!EstSolViseParRayon(_rayon, noeudCol))
			{
				GD.Print("ZERO-K : Replantation buisson impossible hors sol.");
				return;
			}
			byte varianteCouleur = (byte)Mathf.Clamp(mainActive.IndexChimique, 0, 120);
			byte typeBuisson = Chunk_Serveur.ConstruireTypeBuisson(varianteCouleur, plein: id == 10);
			bool plante = _gestionnaireMonde?.PlanterBuissonGlobal(pointImpact, normaleImpact, typeBuisson) ?? false;
			if (!plante)
			{
				GD.Print("ZERO-K : Sol non valide pour replanter ce buisson (terre plate requise).");
				return;
			}
			GD.Print("ZERO-K : Buisson replanté.");
		}
        else if (id == 999 || id == BlocChutant.ID_BRANCHE || id == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0 || id == IdObjetCarnetSavoir || id == 30 || id == 32 || id == 34 || id == 105 || id == 106 || id == IdObjetHachePierreTier1 || id == IdObjetAtelleJambe || id == IdObjetPellePierreTier0 || id == IdObjetPiochePierreTier0 || id == IdObjetLancePierreTier0 || id == IdObjetFauxPierreTier0 || id == IdObjetAllumeFeu || id == 200 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0 || EstIdPitFeu(id) || EstIdFondation(id))
        {
            Vector3 pointSpawn = (structureFixe && EstIdFondation(mainActive.ID))
                ? pointAligneStructure
                : pointDeChute;
            Node3D nePose = CreerBlocPose(pointSpawn, mainActive);
            if (nePose == null)
                return;
            if (structureFixe)
                AppliquerTransformPoseStructure(nePose, pointAligneStructure, rotationStructureDeg);
            // Clic droit rapide : un objet lançable doit se déposer au sol sans mini-impulsion.
            // La poussée douce reste utile pour les poses via touche Interagir.
            bool estLancable = EstObjetLancableAuMaintien(mainActive);
            if (estLancable)
                ForcerQuantiteObjetPoseUnitaireSiItemPhysique(nePose);
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
        if (structureSupport != null)
        {
            // Si la visée touche une structure existante, on impose un plancher Y au-dessus de son sommet.
            // Cela empêche toute fusion visuelle/collision quand la normale renvoyée est imprécise.
            float ySommetSupport = structureSupport.GlobalPosition.Y + ObtenirDimensionsApproxStructurePose(structureSupport.ID_Objet).Y;
            float yMinimalPose = ySommetSupport + MargeEmpilementStructureMetres;
            if (pointDeChute.Y < yMinimalPose)
                pointDeChute = new Vector3(pointDeChute.X, yMinimalPose, pointDeChute.Z);
        }
        pointAligne = EstIdFondation(mainActive.ID)
            ? CalculerPositionPoseFondation(pointDeChute)
            : pointDeChute;
        rotationDeg = CalculerRotationStructureFixe();
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
            || idObjet == IdObjetTableAnalyseTier1
            || idObjet == IdObjetRackBatons
            || idObjet == IdObjetRackBuches
            || idObjet == IdObjetCoffreBoisTier0
            || EstIdPitFeu(idObjet)
            || EstIdFondation(idObjet);
    }

    private bool EssayerCalculerApercuPlacementStructure(
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

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        return EssayerCalculerPoseStructureFixe(mainActive, depuisInteragir, out pointDeChute, out pointAligne, out rotationDeg, out poseValide);
    }

    private bool EssayerCalculerApercuPlacementObjetLancable(
        SlotInventaire mainActive,
        out Vector3 pointDeChute,
        out Vector3 pointAligne,
        out Vector3 rotationDeg,
        out bool poseValide)
    {
        pointDeChute = Vector3.Zero;
        pointAligne = Vector3.Zero;
        rotationDeg = Vector3.Zero;
        poseValide = false;
        if (mainActive.EstVide || !EstObjetLancableAuMaintien(mainActive))
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        pointDeChute = pointImpact + (normaleImpact * 0.1f);
        pointAligne = pointDeChute;
        rotationDeg = new Vector3(_rotationManuelleX, _rotationManuelleY, _rotationManuelleZ);

        float distance = GlobalPosition.DistanceTo(pointDeChute);
        poseValide = distance >= 0.55f;
        return true;
    }

    /// <summary>Structures fixes: conserve la rotation manuelle et fige X/Z sur la visée, seul Y reste recalé par la physique.</summary>
    private void AppliquerTransformPoseStructure(Node3D structure, Vector3 pointDeChute)
    {
        int idObjet = 0;
        if (structure is ItemPhysique item)
            idObjet = item.ID_Objet;
        else if (structure.HasMeta("ID_Matiere"))
            idObjet = structure.GetMeta("ID_Matiere").AsInt32();

        Vector3 pointAligne = pointDeChute;
        bool estFondation = EstIdFondation(idObjet);
        if (estFondation)
            pointAligne = CalculerPositionPoseFondation(pointDeChute);
        Vector3 rotation = EstStructureFixePose(idObjet)
            ? CalculerRotationStructureFixe()
            : new Vector3(_rotationManuelleX, _rotationManuelleY, _rotationManuelleZ);
        AppliquerTransformPoseStructure(structure, pointAligne, rotation);
    }

    private Vector3 CalculerRotationStructureFixe()
    {
        float rotationY = Mathf.Round(_rotationManuelleY / PasRotationStructuresFixesDegres) * PasRotationStructuresFixesDegres;
        return new Vector3(0f, rotationY, 0f);
    }

    private void AppliquerTransformPoseStructure(Node3D structure, Vector3 pointAligne, Vector3 rotationDeg)
    {
        Vector3 pos = structure.GlobalPosition;
        structure.GlobalPosition = new Vector3(pointAligne.X, pos.Y, pointAligne.Z);
        structure.GlobalRotationDegrees = rotationDeg;
        if (structure is RigidBody3D rb)
        {
            rb.LinearVelocity = Vector3.Zero;
            rb.AngularVelocity = Vector3.Zero;
            rb.Sleeping = true;
        }
    }

    private bool SontFondationsAdjacentes(float dx, float dz)
    {
        bool adjacentX = dz <= FondationToleranceAxeSecondaireMetres
            && Mathf.Abs(dx - FondationDistanceCentreAdjacente) <= FondationToleranceAxePrincipalMetres;
        bool adjacentZ = dx <= FondationToleranceAxeSecondaireMetres
            && Mathf.Abs(dz - FondationDistanceCentreAdjacente) <= FondationToleranceAxePrincipalMetres;
        return adjacentX || adjacentZ;
    }

    /// <summary>Fondation : première pose libre, puis snap doux uniquement près d'une fondation existante.</summary>
    private Vector3 CalculerPositionPoseFondation(Vector3 pointDeChute)
    {
        Vector3 meilleur = pointDeChute;
        float meilleurDistSq = float.MaxValue;
        bool meilleurAxePrefere = false;
        bool meilleurSignePrefere = false;
        int meilleurOrdre = int.MaxValue;
        bool candidatTrouve = false;
        bool fondationExistante = false;
        float rayonSq = FondationRayonSnapDouxMetres * FondationRayonSnapDouxMetres;
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return pointDeChute;

        void EvaluerCandidat(Vector3 p, bool candidatSurAxeX, bool candidatPositif, bool axePrefereX, bool signePreferePositif, int ordreCandidat)
        {
            float dx = p.X - pointDeChute.X;
            float dz = p.Z - pointDeChute.Z;
            float distSq = dx * dx + dz * dz;
            if (distSq > rayonSq)
                return;
            bool axePrefere = candidatSurAxeX == axePrefereX;
            bool signePrefere = candidatPositif == signePreferePositif;
            const float epsilonDist = 0.0001f;
            bool meilleurParDistance = distSq < (meilleurDistSq - epsilonDist);
            bool distanceQuasiEgale = Mathf.Abs(distSq - meilleurDistSq) <= epsilonDist;
            bool meilleurParPreference = distanceQuasiEgale
                && ((axePrefere && !meilleurAxePrefere)
                    || (axePrefere == meilleurAxePrefere && signePrefere && !meilleurSignePrefere)
                    || (axePrefere == meilleurAxePrefere && signePrefere == meilleurSignePrefere && ordreCandidat < meilleurOrdre));
            if (!candidatTrouve || meilleurParDistance || meilleurParPreference)
            {
                candidatTrouve = true;
                meilleurDistSq = distSq;
                meilleurAxePrefere = axePrefere;
                meilleurSignePrefere = signePrefere;
                meilleurOrdre = ordreCandidat;
                meilleur = new Vector3(p.X, pointDeChute.Y, p.Z);
            }
        }

        foreach (Node n in nodes)
        {
            if (n is not ItemPhysique ip || !EstIdFondation(ip.ID_Objet))
                continue;
            fondationExistante = true;
            Vector3 c = ip.GlobalPosition;
            Vector3 delta = pointDeChute - c;
            bool axePrefereX = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Z);
            bool signePreferePositif = axePrefereX ? delta.X >= 0f : delta.Z >= 0f;
            EvaluerCandidat(new Vector3(c.X + FondationDistanceCentreAdjacente, c.Y, c.Z), candidatSurAxeX: true, candidatPositif: true, axePrefereX, signePreferePositif, ordreCandidat: 0);
            EvaluerCandidat(new Vector3(c.X - FondationDistanceCentreAdjacente, c.Y, c.Z), candidatSurAxeX: true, candidatPositif: false, axePrefereX, signePreferePositif, ordreCandidat: 1);
            EvaluerCandidat(new Vector3(c.X, c.Y, c.Z + FondationDistanceCentreAdjacente), candidatSurAxeX: false, candidatPositif: true, axePrefereX, signePreferePositif, ordreCandidat: 2);
            EvaluerCandidat(new Vector3(c.X, c.Y, c.Z - FondationDistanceCentreAdjacente), candidatSurAxeX: false, candidatPositif: false, axePrefereX, signePreferePositif, ordreCandidat: 3);
        }

        if (!fondationExistante || !candidatTrouve)
            return pointDeChute;
        return meilleur;
    }

    private Vector3 ObtenirDimensionsApproxStructurePose(int idObjet)
    {
        if (EstIdFondation(idObjet))
            return new Vector3(FondationDistanceCentreAdjacente, 1f, FondationDistanceCentreAdjacente);
        if (idObjet == 200)
            return new Vector3(1.2f, 1.0f, 0.9f);
        if (idObjet == IdObjetTableAnalyseTier1)
            return new Vector3(1.53f, 1.20f, 1.20f);
        if (idObjet == IdObjetRackBatons || idObjet == IdObjetRackBuches)
            return new Vector3(0.95f, 0.72f, 0.62f);
        if (idObjet == IdObjetCoffreBoisTier0)
            return new Vector3(0.58f, 0.42f, 0.48f);
        if (EstIdPitFeu(idObjet))
            return new Vector3(0.98f, 0.45f, 0.98f);
        return new Vector3(0.8f, 0.8f, 0.8f);
    }

    private bool EstPositionStructureLibre(int idObjet, Vector3 pointDeChute, Vector3 pointAligne)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return true;
        Vector3 dimsPose = ObtenirDimensionsApproxStructurePose(idObjet);
        Vector3 posPose = new Vector3(pointAligne.X, pointDeChute.Y, pointAligne.Z);
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstStructureFixePose(ip.ID_Objet))
                continue;
            Vector3 dimsRef = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            Vector3 posRef = ip.GlobalPosition;
            // Les structures fixes sont ancrées au sol (Y = base). On considère collision
            // si la séparation verticale est inférieure à la plus petite hauteur utile.
            float yTolerance = Mathf.Min(dimsPose.Y, dimsRef.Y) - MargeChevauchementMetres;
            if (Mathf.Abs(posPose.Y - posRef.Y) > yTolerance)
                continue;
            float xTolerance = ((dimsPose.X + dimsRef.X) * 0.5f) - MargeChevauchementMetres;
            float zTolerance = ((dimsPose.Z + dimsRef.Z) * 0.5f) - MargeChevauchementMetres;
            if (Mathf.Abs(posPose.X - posRef.X) < xTolerance && Mathf.Abs(posPose.Z - posRef.Z) < zTolerance)
                return false;
        }
        return true;
    }

    private ItemPhysique ResoudreStructureSupportDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && EstStructureFixePose(item.ID_Objet))
                return item;
        }
        return null;
    }

    private bool EssayerAjusterStructureSansChevauchement(int idObjet, ref Vector3 pointDeChute, ref Vector3 pointAligne)
    {
        if (!EstStructureFixePose(idObjet))
            return true;
        if (EstPositionStructureLibre(idObjet, pointDeChute, pointAligne))
            return true;

        float baseStep = Mathf.Max(0.32f, Mathf.Max(ObtenirDimensionsApproxStructurePose(idObjet).X, ObtenirDimensionsApproxStructurePose(idObjet).Z) * 0.55f);
        Vector2[] directions = new Vector2[]
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0.70710677f, 0.70710677f),
            new Vector2(-0.70710677f, 0.70710677f),
            new Vector2(0.70710677f, -0.70710677f),
            new Vector2(-0.70710677f, -0.70710677f),
        };
        float[] yOffsets = new float[] { 0f, 0.2f, -0.2f, 0.4f };

        for (int ring = 1; ring <= 4; ring++)
        {
            float step = baseStep * ring;
            for (int d = 0; d < directions.Length; d++)
            {
                for (int y = 0; y < yOffsets.Length; y++)
                {
                    Vector3 candidatChute = pointDeChute + new Vector3(directions[d].X * step, yOffsets[y], directions[d].Y * step);
                    Vector3 candidatAligne = EstIdFondation(idObjet)
                        ? CalculerPositionPoseFondation(candidatChute)
                        : candidatChute;
                    if (!EstPositionStructureLibre(idObjet, candidatChute, candidatAligne))
                        continue;
                    pointDeChute = candidatChute;
                    pointAligne = candidatAligne;
                    return true;
                }
            }
        }
        return false;
    }

    private bool EstObjetLancableAuMaintien(SlotInventaire slot)
    {
        if (slot.EstVide) return false;
        bool estTerrainVoxel = slot.ID >= 1 && slot.ID <= 9;
        bool estAtelier = slot.ID == 200;
        bool estTableAnalyse = slot.ID == IdObjetTableAnalyseTier1;
        bool estRackBatons = slot.ID == IdObjetRackBatons || slot.ID == IdObjetRackBuches;
        bool estBuisson = slot.ID == 10 || slot.ID == 11;
        bool estCoffre = slot.ID == IdObjetCoffreBoisTier0;
        bool estPitFeu = EstIdPitFeu(slot.ID) || EstIdFondation(slot.ID);
        return !estTerrainVoxel && !estAtelier && !estTableAnalyse && !estRackBatons && !estBuisson && !estCoffre && !estPitFeu;
    }

    /// <summary>Clic droit court : si la visée est le sol et l’outil peut faucher, exécute le même fauchage que le clic gauche (gazon 3D → fibres).</summary>
    /// <returns>True si le fauchage a été traité (ne pas enchaîner sur la pose au sol).</returns>
    private bool ExecuterFauchageSolPrioritaireClicDroit()
    {
        AssurerDurabiliteOutilsSurLesMains();
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;
        if (mainActive.EstVide || !PeutUtiliserFrappe(mainActive))
            return false;

        // Roche plate (1), pointe (3) ou dague — pas la hachette pour le gazon.
        bool estOutilFaucheur = mainActive.ID == 105 || mainActive.ID == IdObjetFauxPierreTier0
            || (ItemPhysique.EstIdRocheMatiere(mainActive.ID) && (mainActive.IndexMorphologique == 1 || mainActive.IndexMorphologique == 3));
        if (!estOutilFaucheur)
            return false;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        if (!EstSolViseParRayon(_rayon, objetTouche))
            return false;

        Vector3 directionMouvement = -_camera.GlobalTransform.Basis.Z.Normalized();
        var (effHache, effPelle, masseOutil) = AnalyserOutilCAO(directionMouvement);
        if (effPelle >= 0.6f)
            return false;

        ExecuterCreusage(1f, effPelle, masseOutil, _rayon.GetCollisionPoint());
        JouerAnimationFrappe(TypeMouvementFrappe.Estoc);
        return true;
    }

    /// <summary>Terrain voxel / sections de sol : creusage (pelle) ou fauchage (lame) selon l’outil émergent.</summary>
    /// <remarks>Le raycast touche souvent le <see cref="CollisionShape3D"/> enfant ; le <see cref="StaticBody3D"/> s’appelle <c>CollisionSection_*</c>.</remarks>
    private static bool EstSurfaceTerrainVisee(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur.IsInGroup("Terrain")) return true;
            string nm = cur.Name.ToString();
            if (nm.Contains("Terrain") || nm.Contains("CollisionSection")) return true;
        }
        return false;
    }

    private bool EstNoeudSupportStructure(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (!cur.IsInGroup("BlocsPoses"))
                continue;
            if (cur is ItemPhysique item)
                return EstStructureFixePose(item.ID_Objet);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sol du monde procédural (Monde_Client) : corps créés uniquement via <see cref="PhysicsServer3D"/> sans nœud <see cref="CollisionObject3D"/>.
    /// Dans ce cas <see cref="RayCast3D.GetCollider"/> est souvent <c>null</c> alors que <see cref="RayCast3D.IsColliding"/> est vrai.
    /// </summary>
    private static bool EstSolMondeSansColliderNode(RayCast3D rayon)
    {
        if (!rayon.IsColliding()) return false;
        if (rayon.GetCollider() != null) return false;
        return rayon.GetCollisionNormal().Y >= 0.18f;
    }

    /// <summary>True si la visée est le sol (nœuds terrain legacy OU mesh monde AAA sans objet associé au raycast).</summary>
    private static bool EstSolViseParRayon(RayCast3D rayon, Node noeudDepuisCollider)
    {
        return EstSurfaceTerrainVisee(noeudDepuisCollider) || EstSolMondeSansColliderNode(rayon);
    }

    private bool EstSurfaceSupportStructureVisee(RayCast3D rayon, Node noeudDepuisCollider)
    {
        if (EstSolViseParRayon(rayon, noeudDepuisCollider))
            return true;
        if (!rayon.IsColliding())
            return false;
        if (rayon.GetCollisionNormal().Y < NormaleSupportStructureMinY)
            return false;
        return EstNoeudSupportStructure(noeudDepuisCollider);
    }

    /// <summary>Collider Jolt = souvent <see cref="CollisionShape3D"/> ; on remonte au corps pour groupes / noms.</summary>
    private static Node NoeudDepuisColliderRaycast(GodotObject collider)
    {
        if (collider == null) return null;
        if (collider is CollisionShape3D sh)
            return sh.GetParent() as Node ?? sh;
        return collider as Node;
    }

    /// <summary>Hache = tranchant perpendiculaire à la frappe (<c>alignement</c> → 0). Pelle = plat aligné (<c>alignement</c> → 1).</summary>
}
