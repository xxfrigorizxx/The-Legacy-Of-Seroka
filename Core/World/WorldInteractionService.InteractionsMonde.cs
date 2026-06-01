using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public partial class Joueur
{
    /// <summary>Ouvre le conteneur/station sous visée (atelier 200, table structures 148, table analyse 131, racks 109/110, coffre 113, pit roche 122).</summary>
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
        if (idT != 200 && idT != IdObjetTableArtisanaTier1 && idT != IdObjetTableAnalyseTier1 && idT != IdObjetRackBatons && idT != IdObjetRackBuches && idT != IdObjetCoffreBoisTier0 && idT != IdObjetPitFeuRoche)
            return false;

        if (idT == IdObjetTableAnalyseTier1)
        {
            CraftGrille3x3AuTable = false;
            IdStationCraftOuverte = 0;
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
        if (idT == 200 || idT == IdObjetTableArtisanaTier1)
        {
            AtelierPlanTravailOuvert = itemTouche;
            CraftGrille3x3AuTable = true;
            IdStationCraftOuverte = idT;
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
            IdStationCraftOuverte = 0;
            AtelierPlanTravailOuvert = null;
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
        }
        else
        {
            RackBatonsOuvert = itemTouche;
            StockageRackBatonsOuvert = true;
            CraftGrille3x3AuTable = true;
            IdStationCraftOuverte = 0;
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
            : (idT == IdObjetTableArtisanaTier1
                ? "ZERO-K : Table artisanat structures T1 ouverte."
            : (idT == IdObjetRackBatons ? "ZERO-K : Rack à bâtons ouvert."
                : (idT == IdObjetRackBuches ? "ZERO-K : Rack à bûches ouvert."
                    : (idT == IdObjetPitFeuRoche ? "ZERO-K : Pit à feu roche ouvert." : "ZERO-K : Coffre en bois ouvert.")))));
        return true;
    }

    /// <summary>Clic gauche avec allume-feu : allume un pit ou une torche visée, puis retire 1 point de durabilité.</summary>
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
        if (itemTouche == null || (itemTouche.ID_Objet != IdObjetPitFeu && itemTouche.ID_Objet != IdObjetPitFeuRoche && !EstIdTorche(itemTouche.ID_Objet)))
            return false;
        if (EstIdTorche(itemTouche.ID_Objet))
        {
            if (itemTouche.EstTorcheAllumee())
            {
                if (!itemTouche.EteindreTorche())
                    return false;
                if (!Engine.IsEditorHint())
                    SauvegarderEtatPersistantMonde(GetTree());
                GetViewport().SetInputAsHandled();
                GD.Print("ZERO-K : Torche éteinte.");
                return true;
            }
            if (!itemTouche.ActiverTorcheAllumee())
                return false;
            mainActive.DurabiliteOutilActuelle = Mathf.Max(0f, mainActive.DurabiliteOutilActuelle - 1f);
            if (mainActive.DurabiliteOutilActuelle <= 0.001f)
            {
                GD.Print("ZERO-K : L'allume-feu s'est brisé.");
                mainActive = new SlotInventaire();
            }
            if (!Engine.IsEditorHint())
                SauvegarderEtatPersistantMonde(GetTree());
            GetViewport().SetInputAsHandled();
            GD.Print("ZERO-K : Torche allumée.");
            return true;
        }
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

    /// <summary>Interaction porte bois : E ouvre/ferme en alternance.</summary>
    private bool EssayerBasculerPorteSousVisee()
    {
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding())
            return false;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        var itemTouche = objetTouche as ItemPhysique
            ?? (objetTouche as Node)?.GetParent() as ItemPhysique
            ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
        if (itemTouche == null || !EstIdPorteBois(itemTouche.ID_Objet))
            return false;

        float baseY = itemTouche.HasMeta("Porte_BaseYaw")
            ? (float)itemTouche.GetMeta("Porte_BaseYaw").AsDouble()
            : Mathf.PosMod(itemTouche.GlobalRotationDegrees.Y, 360f);
        Vector3 basePos = itemTouche.HasMeta("Porte_BasePos")
            ? itemTouche.GetMeta("Porte_BasePos").AsVector3()
            : itemTouche.GlobalPosition;
        bool ouverte = itemTouche.HasMeta("Porte_Ouverte") && itemTouche.GetMeta("Porte_Ouverte").AsBool();

        // Pivot latéral (charnière) : la porte tourne autour de son côté gauche en état fermé.
        float angleOuverture = ouverte ? 0f : 90f;
        float baseRad = Mathf.DegToRad(baseY);
        Vector3 axeLargeurFermee = new Basis(Vector3.Up, baseRad).X.Normalized();
        Vector3 hingeWorld = basePos - axeLargeurFermee * (PorteLargeurMetres * 0.5f);
        Vector3 demiLargeur = axeLargeurFermee * (PorteLargeurMetres * 0.5f);
        Vector3 centreCible = hingeWorld + (new Basis(Vector3.Up, Mathf.DegToRad(angleOuverture)) * demiLargeur);
        float cibleY = Mathf.PosMod(baseY + angleOuverture, 360f);

        itemTouche.GlobalPosition = new Vector3(centreCible.X, basePos.Y, centreCible.Z);
        itemTouche.GlobalRotationDegrees = new Vector3(0f, cibleY, 0f);
        itemTouche.SetMeta("Porte_BaseYaw", baseY);
        itemTouche.SetMeta("Porte_BasePos", basePos);
        itemTouche.SetMeta("Porte_AngleOuverture", 90f);
        itemTouche.SetMeta("Porte_Charniere", -1);
        itemTouche.SetMeta("Porte_Ouverte", !ouverte);
        itemTouche.Sleeping = true;
        GD.Print(!ouverte ? "ZERO-K : Porte ouverte." : "ZERO-K : Porte fermée.");
        GetViewport().SetInputAsHandled();
        return true;
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

    private const string PrefixeGenomeVoxelTerrain = "VOXEL_TERRAIN:";

    private static bool EstIdTerrainVoxelPosable(int id) => id >= 1 && id <= 9 && id != 4;

    private static bool EstIdMineraiVoxelTerrain(int id) =>
        (id >= 10 && id <= 29) || (id >= 32 && id <= 48);

    private static bool EssayerLireIdVoxelTerrainForce(in SlotInventaire slot, out int idVoxel)
    {
        idVoxel = 0;
        if (slot.EstVide || string.IsNullOrWhiteSpace(slot.GenomeAssemblage))
            return false;

        string genome = slot.GenomeAssemblage.Trim();
        if (!genome.StartsWith(PrefixeGenomeVoxelTerrain, StringComparison.OrdinalIgnoreCase))
            return false;

        string brut = genome.Substring(PrefixeGenomeVoxelTerrain.Length).Trim();
        if (!int.TryParse(brut, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return false;

        if (!EstIdTerrainVoxelPosable(parsed) && !EstIdMineraiVoxelTerrain(parsed))
            return false;

        idVoxel = parsed;
        return true;
    }

    private static bool EstSlotTerrainVoxelPosable(in SlotInventaire slot) =>
        EstIdTerrainVoxelPosable(slot.ID) || EssayerLireIdVoxelTerrainForce(slot, out _);

    private static int ResoudreIdVoxelPose(in SlotInventaire slot) =>
        EssayerLireIdVoxelTerrainForce(slot, out int idVoxel) ? idVoxel : slot.ID;

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (EstSlotTerrainVoxelPosable(s)) return true;
        return s.ID == 999 || s.ID == 10 || s.ID == 11 || s.ID == BlocChutant.ID_BRANCHE || s.ID == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(s.ID) || s.ID == 30 || s.ID == 32 || s.ID == 34 || s.ID == 21 || s.ID == IdObjetCeinturePoches || s.ID == IdObjetCeintureSacoches || s.ID == IdObjetPochetteTier0 || s.ID == IdObjetSacTier0 || s.ID == IdObjetHachePierreTier1 || s.ID == IdObjetAtelleJambe || s.ID == IdObjetAtelleBras || s.ID == IdObjetBandageTier1 || s.ID == IdObjetPellePierreTier0 || s.ID == IdObjetPiochePierreTier0 || s.ID == IdObjetLancePierreTier0 || s.ID == IdObjetFauxPierreTier0 || s.ID == IdObjetAllumeFeu || s.ID == IdObjetFenetreBois || s.ID == 200 || s.ID == IdObjetTableBoisDecorative || s.ID == IdObjetTableArtisanaTier1 || s.ID == IdObjetTableAnalyseTier1 || s.ID == IdObjetRackBatons || s.ID == IdObjetRackBuches || s.ID == IdObjetCoffreBoisTier0 || s.ID == IdObjetPitFeuRoche || s.ID == IdObjetMortierPilonBois || EstIdFondation(s.ID) || EstIdMuret(s.ID) || EstIdMurBois(s.ID) || EstIdPorteBois(s.ID) || EstIdToitChaume(s.ID) || EstIdTorche(s.ID);
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
            if (id == 200 || id == IdObjetTableBoisDecorative || id == IdObjetTableArtisanaTier1 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || EstIdPitFeu(id) || EstIdFondation(id))
            {
                GD.Print("ZERO-K : Structure fixée au monde. Récupération uniquement par minage.");
                return;
            }
            var item = objetTouche as ItemPhysique ?? (objetTouche as Node)?.GetParent() as ItemPhysique ?? (objetTouche as Node)?.GetNodeOrNull<ItemPhysique>("ItemPhysique");
            byte indexBotaniqueRamasse = LSystem_Botanique.IndexChene;
            if (item != null && (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == BlocChutant.ID_FEUILLE_ARRACHEE || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetAtelleBras || item.ID_Objet == IdObjetBandageTier1 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0 || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet) || EstIdToitChaume(item.ID_Objet)))
                indexBotaniqueRamasse = item.IndexBotanique;
            else if ((id == BlocChutant.ID_BRANCHE || id == BlocChutant.ID_BOIS || id == BlocChutant.ID_FEUILLE_ARRACHEE) && objetTouche.HasMeta("IndexBotanique"))
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
                    : (item != null && (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetAtelleBras || item.ID_Objet == IdObjetBandageTier1 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet)) ? item.IndexTailleRoche : (id == BlocChutant.ID_BRANCHE ? 2 : 0)),
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
            if ((nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetHachePierreTier1 || nouveauSlot.ID == IdObjetAtelleJambe || nouveauSlot.ID == IdObjetAtelleBras || nouveauSlot.ID == IdObjetBandageTier1 || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0 || nouveauSlot.ID == IdObjetAllumeFeu) && item != null)
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
                bool conserveEssence = boisChute || id == BlocChutant.ID_FEUILLE_ARRACHEE;
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
                    IndexBotanique = conserveEssence ? idxBot : LSystem_Botanique.IndexChene
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
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetAtelleBras || item.ID_Objet == IdObjetBandageTier1 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 0),
                IndexTailleLameRoche = (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : ((item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetAtelleBras || item.ID_Objet == IdObjetBandageTier1 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == 200 || item.ID_Objet == IdObjetTableAnalyseTier1 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0 || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet) || EstIdToitChaume(item.ID_Objet))
                    ? item.IndexBotanique
                    : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = item.HasMeta("CleConteneur") ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetHachePierreTier1 || nouveauSlot.ID == IdObjetAtelleJambe || nouveauSlot.ID == IdObjetAtelleBras || nouveauSlot.ID == IdObjetBandageTier1 || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0 || nouveauSlot.ID == IdObjetAllumeFeu)
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
                    : (item.ID_Objet == 105 || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetAtelleBras || item.ID_Objet == IdObjetBandageTier1 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || ItemPhysique.EstIdRocheMatiere(item.ID_Objet) ? item.IndexTailleRoche : 0),
                IndexTailleLameRoche = (item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) && item.HasMeta(MetaTailleLameRoche)
                    ? (int)item.GetMeta(MetaTailleLameRoche).AsInt32()
                    : ((item.ID_Objet == 105 || item.ID_Objet == IdObjetFauxPierreTier0) ? 2 : 0),
                EstUnEclat = item.EstUnEclat,
                MeshEclat = item.EstUnEclat ? item.ObtenirMeshVisuel() : null,
                NiveauFracture = item.NiveauFracture,
                ScaleEclat = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE) ? ScaleEclatBoisAuRamassage(item) : item.Scale,
                IndexBotanique = (item.ID_Objet == 30 || item.ID_Objet == 32 || item.ID_Objet == BlocChutant.ID_BRANCHE || item.ID_Objet == 106 || item.ID_Objet == IdObjetHachePierreTier1 || item.ID_Objet == IdObjetAtelleJambe || item.ID_Objet == IdObjetAtelleBras || item.ID_Objet == IdObjetBandageTier1 || item.ID_Objet == IdObjetPellePierreTier0 || item.ID_Objet == IdObjetPiochePierreTier0 || item.ID_Objet == IdObjetLancePierreTier0 || item.ID_Objet == IdObjetFauxPierreTier0 || item.ID_Objet == IdObjetPochetteTier0 || item.ID_Objet == IdObjetSacTier0 || item.ID_Objet == IdObjetCeinturePoches || item.ID_Objet == IdObjetCeintureSacoches || item.ID_Objet == IdObjetRackBatons || item.ID_Objet == IdObjetRackBuches || item.ID_Objet == IdObjetCoffreBoisTier0 || EstIdPitFeu(item.ID_Objet) || EstIdFondation(item.ID_Objet) || EstIdToitChaume(item.ID_Objet))
                    ? item.IndexBotanique
                    : LSystem_Botanique.IndexChene,
                GenomeAssemblage = LireGenomeSurItemPhysique(item),
                CleConteneur = item.HasMeta("CleConteneur") ? item.GetMeta("CleConteneur").AsString() : ""
            };
            if (nouveauSlot.ID == 105 || nouveauSlot.ID == 106 || nouveauSlot.ID == IdObjetHachePierreTier1 || nouveauSlot.ID == IdObjetAtelleJambe || nouveauSlot.ID == IdObjetAtelleBras || nouveauSlot.ID == IdObjetBandageTier1 || nouveauSlot.ID == IdObjetPellePierreTier0 || nouveauSlot.ID == IdObjetPiochePierreTier0 || nouveauSlot.ID == IdObjetLancePierreTier0 || nouveauSlot.ID == IdObjetFauxPierreTier0 || nouveauSlot.ID == IdObjetAllumeFeu)
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
        bool etaitToitChaume = itemQuantitePose != null && EstIdToitChaume(itemQuantitePose.ID_Objet);
        objetTouche.QueueFree();
        if (etaitToitChaume)
            CallDeferred(nameof(RecalculerAssemblageToitsChaumeGlobal));
        ReinitialiserRotationManuelle();
        RafraichirHUD();
    }
}
