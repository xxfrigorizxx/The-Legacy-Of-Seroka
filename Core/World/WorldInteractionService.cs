using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private const float RayonInteractionBaiesBuisson = 1.2f;

    /// <summary>Ouvre le conteneur sous visée (atelier 200, racks 109/110, coffre 113).</summary>
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
        if (idT != 200 && idT != IdObjetRackBatons && idT != IdObjetRackBuches && idT != IdObjetCoffreBoisTier0)
            return false;

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
            else
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
                : (idT == IdObjetRackBuches ? "ZERO-K : Rack à bûches ouvert." : "ZERO-K : Coffre en bois ouvert.")));
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
        if (typeFlore != 1)
        {
            GD.Print("ZERO-K : Ce buisson est vide, aucune baie à ramasser.");
            return true;
        }

        var slotTest = new SlotInventaire { ID = IdObjetBaie, IndexChimique = 0, Quantite = 1 };
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
            string c = couleur == 0 ? "rouges" : "colorées";
            GD.Print($"ZERO-K : {ajoutees} baie(s) {c} récoltée(s) sur le buisson.");
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

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (s.ID >= 1 && s.ID <= 9 && s.ID != 4) return true;
        return s.ID == 999 || s.ID == 10 || s.ID == 11 || s.ID == BlocChutant.ID_BRANCHE || s.ID == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(s.ID) || s.ID == 30 || s.ID == 32 || s.ID == 34 || s.ID == 21 || s.ID == IdObjetCeinturePoches || s.ID == IdObjetCeintureSacoches || s.ID == IdObjetPochetteTier0 || s.ID == IdObjetSacTier0 || s.ID == IdObjetPellePierreTier0 || s.ID == IdObjetPiochePierreTier0 || s.ID == IdObjetLancePierreTier0 || s.ID == IdObjetFauxPierreTier0 || s.ID == 200 || s.ID == IdObjetRackBatons || s.ID == IdObjetRackBuches || s.ID == IdObjetCoffreBoisTier0;
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
            if (id == 200 || id == IdObjetRackBatons || id == IdObjetRackBuches)
            {
                GD.Print("ZERO-K : Structure fixée au monde. Récupération uniquement par minage.");
                return;
            }
            var item = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            byte indexBotaniqueRamasse = LSystem_Botanique.IndexChene;
            if (item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0))
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
                    : (item != null && (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) ? item.IndexTailleRoche : (id == BlocChutant.ID_BRANCHE ? 2 : 0)),
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
            if ((nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0) && item != null)
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
                    IndexChimique = 0,
                    IndexTaille = id == BlocChutant.ID_BRANCHE ? 2 : 0,
                    IndexBotanique = boisChute ? idxBot : LSystem_Botanique.IndexChene
                };
            }
            else
            {
            var item = rb as ItemPhysique ?? (rb as Node)?.GetParent() as ItemPhysique ?? rb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (item.ID_Objet == 200 || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches)
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
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 0),
                IndexTailleLameRoche = (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : ((item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == 200 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0)
                    ? item.IndexBotanique
                    : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = item.HasMeta("CleConteneur") ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
            }
        }
        else if (objetTouche is StaticBody3D sb)
        {
            var item = sb.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            if (item == null) return;
            if (item.ID_Objet == 200 || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches)
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
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 0),
                IndexTailleLameRoche = (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : ((item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0)
                    ? item.IndexBotanique
                    : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = item.HasMeta("CleConteneur") ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0)
                RemplirDurabiliteOutilDepuisItemPhysique(ref nouveauSlot, item);
        }
        else
            return;

        PreparerRamassageCoffreVersInventaire(ref nouveauSlot, objetTouche);
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

    private void ExecuterPlacementAvecOptions(SlotInventaire mainActive, bool depuisInteragir)
    {
        if (mainActive.EstVide) return;

        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;

        Vector3 pointImpact = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        Vector3 pointDeChute;

        if (mainActive.ID == 200 || mainActive.ID == IdObjetRackBatons || mainActive.ID == IdObjetRackBuches || mainActive.ID == IdObjetCoffreBoisTier0)
        {
            Node noeudCol = NoeudDepuisColliderRaycast(_rayon.GetCollider());
            if (!EstSolViseParRayon(_rayon, noeudCol))
            {
                GD.Print("ZERO-K : Posez cette structure sur le sol (terrain / herbe), pas sur un objet vertical.");
                return;
            }

            // Plus de fauchage automatique a la pose de l'atelier :
            // cela faisait apparaitre des fibres loin du visuel reel de l'herbe.

            // FIX CRITIQUE : On supprime la lecture du voxel hSurf + 1f.
            // L'objet se pose EXACTEMENT sur le point du raycast, ancré par son pivot.
            pointDeChute = pointImpact;
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
        float distMin = flexOuCordeE ? 0.35f : ((mainActive.ID == 200 || mainActive.ID == IdObjetRackBatons || mainActive.ID == IdObjetRackBuches || mainActive.ID == IdObjetCoffreBoisTier0) ? 0.55f : 1.4f);
        if (!depuisInteragir && EstObjetLancableAuMaintien(mainActive))
            distMin = Mathf.Min(distMin, 0.55f);
        if (distance < distMin) return;

        int id = mainActive.ID;
        if (id == 0) return;
        if (id >= 1 && id <= 9 && id != 4)
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
        else if (id == 999 || id == BlocChutant.ID_BRANCHE || id == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0 || id == IdObjetCarnetSavoir || id == 30 || id == 32 || id == 34 || id == 105 || id == 106 || id == IdObjetPellePierreTier0 || id == IdObjetPiochePierreTier0 || id == IdObjetLancePierreTier0 || id == IdObjetFauxPierreTier0 || id == 200 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0)
        {
            Node3D nePose = CreerBlocPose(pointDeChute, mainActive);
            // Clic droit rapide : un objet lançable doit se déposer au sol sans mini-impulsion.
            // La poussée douce reste utile pour les poses via touche Interagir.
            bool estLancable = EstObjetLancableAuMaintien(mainActive);
            bool structureFixe = id == 200 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0;
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

        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }

    private bool EstObjetLancableAuMaintien(SlotInventaire slot)
    {
        if (slot.EstVide) return false;
        bool estTerrainVoxel = slot.ID >= 1 && slot.ID <= 9;
        bool estAtelier = slot.ID == 200;
        bool estRackBatons = slot.ID == IdObjetRackBatons || slot.ID == IdObjetRackBuches;
        bool estBuisson = slot.ID == 10 || slot.ID == 11;
        bool estCoffre = slot.ID == IdObjetCoffreBoisTier0;
        return !estTerrainVoxel && !estAtelier && !estRackBatons && !estBuisson && !estCoffre;
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
