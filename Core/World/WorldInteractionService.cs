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
    private const float FondationToleranceEmpilementXZMetres = 2.05f;
    private const float FondationToleranceDessusMetres = 0.55f;
    private const int OffsetEtagesFondationMax = 12;
    private const float NormaleSupportStructureMinY = 0.6f;
    private const float MargeChevauchementMetres = 0.02f;
    private const float MargeEmpilementStructureMetres = 0.01f;
    private const float PasRotationStructuresFixesDegres = 15f;
    private const float HauteurApproxFondationMetres = 1f;
    private const float HauteurSolBoisMetres = PlancherEpaisseurMetres;
    private const float MuretLongueurMetres = 4f;
    private const float MuretHauteurMetres = 1f;
    private const float MuretEpaisseurMetres = 0.22f;
    private const float MuretOffsetCentreDepuisFondationMetres = FondationPasSnapMetres * 0.5f + MuretEpaisseurMetres * 0.5f - FondationPenetrationMetres;
    private const float MuretToleranceSnapFondationMetres = 2.4f;
    private const float MuretTolerancePresenceMetres = 0.18f;
    private const float PasRotationMuretDegres = 10f;
    private const float MurLargeurMetres = 4f;
    private const float MurHauteurMetres = 3f;
    private const float MurEpaisseurMetres = 0.22f;
    private const float PorteLargeurMetres = 1.35f;
    private const float PorteHauteurMetres = 2.4f;
    private const float PorteEpaisseurMetres = 0.12f;
    private const float ToitChaumeHauteurMetres = 0.42f;
    private const float ToitChaumePasGrilleMetres = 4f;
    private const float ToitChaumeDecalageHauteurMetres = 0.10f;
    private const float TorcheHauteurMetres = 1.12f;
    private const float TorcheRayonMetres = 0.10f;
    private const float TorcheOffsetMurMetres = -0.015f;
    private const float TorcheAngleMurDegres = 45f;
    private const int ModeSnapMuretAuto = 0;
    private const int ModeSnapMuretFondation = 1;
    private const int ModeSnapMuretMuret = 2;
    private const int ModeSnapMuretTerrain = 3;
    private const float PasRotationSolBoisDegres = 90f;
    private const float ToleranceSolSurFondationMetres = 0.35f;

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

    private static bool EstIdTerrainVoxelPosable(int id) => id >= 1 && id <= 9 && id != 4;

    private static bool EstObjetPosableAuSol(SlotInventaire s)
    {
        if (s.EstVide || s.ID == 0) return false;
        if (EstIdTerrainVoxelPosable(s.ID)) return true;
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
        else if (id == 999 || id == BlocChutant.ID_BRANCHE || id == IdObjetBaie || ItemPhysique.EstIdRocheMatiere(id) || id == 15 || id == 16 || id == 17 || id == 20 || id == 21 || id == IdObjetCeinturePoches || id == IdObjetCeintureSacoches || id == IdObjetPochetteTier0 || id == IdObjetSacTier0 || id == IdObjetCarnetSavoir || id == 30 || id == 32 || id == 34 || id == 105 || id == 106 || id == IdObjetHachePierreTier1 || id == IdObjetAtelleJambe || id == IdObjetAtelleBras || id == IdObjetBandageTier1 || id == IdObjetPellePierreTier0 || id == IdObjetPiochePierreTier0 || id == IdObjetLancePierreTier0 || id == IdObjetFauxPierreTier0 || id == IdObjetAllumeFeu || id == IdObjetFenetreBois || id == 200 || id == IdObjetTableBoisDecorative || id == IdObjetTableArtisanaTier1 || id == IdObjetTableAnalyseTier1 || id == IdObjetRackBatons || id == IdObjetRackBuches || id == IdObjetCoffreBoisTier0 || EstIdPitFeu(id) || EstIdFondation(id) || EstIdPlancher(id) || EstIdMuret(id) || EstIdMurBois(id) || EstIdPorteBois(id) || EstIdToitChaume(id) || EstIdTorche(id))
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

    private ItemPhysique ResoudreCadrePorteDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && EstIdMurBoisCadrePorte(item.ID_Objet))
                return item;
        }
        return null;
    }

    private ItemPhysique TrouverCadrePorteProche(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdMurBoisCadrePorte(ip.ID_Objet))
                continue;
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > MurLargeurMetres * 0.65f || dz > MurLargeurMetres * 0.65f)
                continue;
            float dy = Mathf.Abs((ip.GlobalPosition.Y + MurHauteurMetres * 0.5f) - worldPoint.Y);
            if (dy > 2.4f)
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

    private bool CadrePossedeDejaPorte(ItemPhysique cadre)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdPorteBois(ip.ID_Objet))
                continue;
            float yBaseCadre = cadre.GlobalPosition.Y - (MurHauteurMetres * 0.5f);
            float yCentrePorteAttendu = yBaseCadre + PorteHauteurMetres * 0.5f;
            if (Mathf.Abs(ip.GlobalPosition.X - cadre.GlobalPosition.X) <= 0.18f
                && Mathf.Abs(ip.GlobalPosition.Z - cadre.GlobalPosition.Z) <= 0.18f
                && Mathf.Abs(ip.GlobalPosition.Y - yCentrePorteAttendu) <= 0.3f)
                return true;
        }
        return false;
    }

    private ItemPhysique TrouverSupportMurProche(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !(EstIdMuret(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet)))
                continue;
            Vector3 dims = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > dims.X * 0.65f || dz > dims.X * 0.65f)
                continue;
            float dy = Mathf.Abs((ip.GlobalPosition.Y + dims.Y * 0.5f) - worldPoint.Y);
            if (dy > 2.2f)
                continue;
            float score = dx * dx + dz * dz + dy * dy * 0.3f;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = ip;
            }
        }
        return meilleur;
    }

    private ItemPhysique ResoudreMuretDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && (EstIdMuret(item.ID_Objet) || EstIdMurBois(item.ID_Objet)))
                return item;
        }
        return null;
    }

    private ItemPhysique TrouverMuretProchePourSnap(Vector3 worldPoint)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;
        ItemPhysique meilleur = null;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !(EstIdMuret(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet)))
                continue;
            Vector3 dims = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float emprise = Mathf.Max(dims.X, dims.Z);
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > emprise * 0.65f || dz > emprise * 0.65f)
                continue;
            float dy = Mathf.Abs(ip.GlobalPosition.Y - worldPoint.Y);
            if (dy > (dims.Y + MuretHauteurMetres) * 1.2f)
                continue;
            float score = dx * dx + dz * dz + dy * dy * 0.5f;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleur = ip;
            }
        }
        return meilleur;
    }

    private bool MuretExisteDejaSurPosition(Vector3 pointAligne)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdMuret(ip.ID_Objet))
                continue;
            if (Mathf.Abs(ip.GlobalPosition.Y - pointAligne.Y) > 0.15f)
                continue;
            if (Mathf.Abs(ip.GlobalPosition.X - pointAligne.X) <= MuretTolerancePresenceMetres
                && Mathf.Abs(ip.GlobalPosition.Z - pointAligne.Z) <= MuretTolerancePresenceMetres)
                return true;
        }
        return false;
    }

    private ItemPhysique ResoudreFondationHoteDepuisNoeud(Node n)
    {
        for (Node cur = n; cur != null; cur = cur.GetParent())
        {
            if (cur is ItemPhysique item && item.IsInGroup("BlocsPoses") && EstIdFondation(item.ID_Objet))
                return item;
        }
        return null;
    }

    /// <summary>Choisit la fondation dont le plateau est sous la visée (évite la fondation voisine plus haute).</summary>
    private ItemPhysique TrouverFondationPourPlancher(Vector3 worldPoint, ItemPhysique? candidatRaycast)
    {
        if (candidatRaycast != null && EstPlateauFondationProcheDuPoint(candidatRaycast, worldPoint))
            return candidatRaycast;

        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return null;

        ItemPhysique meilleure = null;
        float meilleurScore = float.MaxValue;
        float demiEmprise = FondationPasSnapMetres * 0.5f;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdFondation(ip.ID_Objet))
                continue;
            float dx = Mathf.Abs(ip.GlobalPosition.X - worldPoint.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - worldPoint.Z);
            if (dx > demiEmprise || dz > demiEmprise)
                continue;
            Vector3 c = ip.GlobalPosition;
            float yTop = c.Y + HauteurApproxFondationMetres;
            if (worldPoint.Y < c.Y - 0.35f)
                continue;
            if (yTop > worldPoint.Y + 0.28f)
                continue;
            float score = (worldPoint.Y - yTop) * (worldPoint.Y - yTop) * 6f + dx * dx + dz * dz;
            if (score < meilleurScore)
            {
                meilleurScore = score;
                meilleure = ip;
            }
        }
        return meilleure;
    }

    private static bool EstPlateauFondationProcheDuPoint(ItemPhysique fondation, Vector3 worldPoint)
    {
        float yTop = fondation.GlobalPosition.Y + HauteurApproxFondationMetres;
        return worldPoint.Y >= fondation.GlobalPosition.Y + HauteurApproxFondationMetres * 0.55f
            && worldPoint.Y <= yTop + 0.35f;
    }

    private ItemPhysique TrouverFondationSousPoint(Vector3 worldPoint)
        => TrouverFondationPourPlancher(worldPoint, null);

    private bool FondationPossedeDejaPlancher(Vector3 centreFondation)
    {
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdPlancher(ip.ID_Objet))
                continue;
            float dx = Mathf.Abs(ip.GlobalPosition.X - centreFondation.X);
            float dz = Mathf.Abs(ip.GlobalPosition.Z - centreFondation.Z);
            if (dx <= ToleranceSolSurFondationMetres && dz <= ToleranceSolSurFondationMetres)
                return true;
        }
        return false;
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
            pointAligne = CalculerPositionPoseFondation(pointDeChute, FondationReposantSurFondationOuStructure(pointDeChute));
        Vector3 rotation = EstStructureFixePose(idObjet)
            ? CalculerRotationStructureFixe(idObjet)
            : new Vector3(_rotationManuelleX, _rotationManuelleY, _rotationManuelleZ);
        AppliquerTransformPoseStructure(structure, pointAligne, rotation);
    }

    private Vector3 CalculerRotationStructureFixe(int idObjet = 0)
    {
        float pas = (EstIdPlancher(idObjet) || EstIdToitChaume(idObjet)) ? PasRotationSolBoisDegres : PasRotationStructuresFixesDegres;
        float rotationY = Mathf.Round(_rotationManuelleY / pas) * pas;
        rotationY = Mathf.PosMod(rotationY, 360f);
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

    /// <summary>Y du pivot fondation (base du bloc ~1 m). Sur fondation : 1 étage au-dessus + offset molette ; au sol : contact + offset.</summary>
    private float CalculerYPoseFondation(float yContactOuSol, ItemPhysique? fondationReference)
    {
        if (fondationReference != null)
        {
            int etagesAuDessus = 1 + _offsetEtagesFondationManuel;
            return fondationReference.GlobalPosition.Y + etagesAuDessus * HauteurApproxFondationMetres + MargeEmpilementStructureMetres;
        }
        return yContactOuSol + _offsetEtagesFondationManuel * HauteurApproxFondationMetres;
    }

    private void AjusterOffsetEtagesFondation(int delta)
    {
        int avant = _offsetEtagesFondationManuel;
        _offsetEtagesFondationManuel = Mathf.Clamp(_offsetEtagesFondationManuel + delta, -OffsetEtagesFondationMax, OffsetEtagesFondationMax);
        if (_offsetEtagesFondationManuel == avant)
            return;
        GD.Print(_offsetEtagesFondationManuel == 0
            ? "ZERO-K : Hauteur fondation — 1 étage au-dessus de la cible (molette / Page Haut-Bas)."
            : $"ZERO-K : Hauteur fondation — +{_offsetEtagesFondationManuel} étage(s) supplémentaire(s) (~{_offsetEtagesFondationManuel * HauteurApproxFondationMetres:0.##} m).");
    }

    private void AjusterModeSnapMuret(int delta)
    {
        const int nbModes = 4;
        int mode = (_modeSnapMuretManuel + delta) % nbModes;
        if (mode < 0)
            mode += nbModes;
        _modeSnapMuretManuel = mode;
        string nomMode = _modeSnapMuretManuel switch
        {
            ModeSnapMuretFondation => "Fondation",
            ModeSnapMuretMuret => "Muret",
            ModeSnapMuretTerrain => "Terrain",
            _ => "Auto"
        };
        GD.Print($"ZERO-K : Snap muret = {nomMode} (molette).");
    }

    /// <summary>True si une fondation ou structure fixe est juste sous la position (empilement, pas sol libre).</summary>
    private bool FondationReposantSurFondationOuStructure(Vector3 pos)
    {
        var espace = GetWorld3D()?.DirectSpaceState;
        if (espace == null)
            return false;
        float profondeur = HauteurApproxFondationMetres + 0.35f;
        var q = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 0.08f, pos + Vector3.Down * profondeur);
        q.CollideWithAreas = false;
        var hit = espace.IntersectRay(q);
        if (hit.Count == 0 || !hit.ContainsKey("collider"))
            return false;
        Node noeud = NoeudDepuisColliderRaycast(hit["collider"].AsGodotObject());
        ItemPhysique support = ResoudreStructureSupportDepuisNoeud(noeud);
        if (support == null || !EstStructureFixePose(support.ID_Objet))
            return false;
        float ySupport = support.GlobalPosition.Y + ObtenirDimensionsApproxStructurePose(support.ID_Objet).Y;
        return pos.Y >= ySupport + MargeEmpilementStructureMetres - 0.08f;
    }

    /// <summary>Fondation sous le curseur (visée dessus) : centre X/Z pour empilement.</summary>
    private bool EssayerResoudreFondationHoteEmpilement(Vector3 pointDeChute, out ItemPhysique hote, out Vector3 xzCentre)
    {
        hote = null;
        xzCentre = pointDeChute;
        var nodes = GetTree()?.GetNodesInGroup("BlocsPoses");
        if (nodes == null)
            return false;
        float meilleurScore = float.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not ItemPhysique ip || !EstIdFondation(ip.ID_Objet))
                continue;
            Vector3 c = ip.GlobalPosition;
            float yTop = c.Y + HauteurApproxFondationMetres;
            if (pointDeChute.Y + 0.15f < yTop - FondationToleranceDessusMetres)
                continue;
            float dx = Mathf.Abs(pointDeChute.X - c.X);
            float dz = Mathf.Abs(pointDeChute.Z - c.Z);
            if (dx > FondationToleranceEmpilementXZMetres || dz > FondationToleranceEmpilementXZMetres)
                continue;
            float score = dx + dz;
            if (score >= meilleurScore)
                continue;
            meilleurScore = score;
            hote = ip;
            xzCentre = new Vector3(c.X, pointDeChute.Y, c.Z);
        }
        return hote != null;
    }

    /// <summary>Fondation : première pose libre, puis snap doux uniquement près d'une fondation existante (même étage).</summary>
    private Vector3 CalculerPositionPoseFondation(Vector3 pointDeChute, bool empilementPrioritaire = false)
    {
        if (empilementPrioritaire)
            return pointDeChute;

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
            if (Mathf.Abs(pointDeChute.Y - c.Y) > FondationToleranceDessusMetres)
                continue;
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
            return new Vector3(FondationDistanceCentreAdjacente, HauteurApproxFondationMetres, FondationDistanceCentreAdjacente);
        if (EstIdPlancher(idObjet))
            return new Vector3(PlancherEmpriseMetres, PlancherEpaisseurMetres, PlancherEmpriseMetres);
        if (EstIdMuret(idObjet))
            return new Vector3(MuretLongueurMetres, MuretHauteurMetres, MuretEpaisseurMetres);
        if (EstIdMurBois(idObjet))
            return new Vector3(MurLargeurMetres, MurHauteurMetres, MurEpaisseurMetres);
        if (EstIdPorteBois(idObjet))
            return new Vector3(PorteLargeurMetres, PorteHauteurMetres, PorteEpaisseurMetres);
        if (EstIdToitChaume(idObjet))
            return new Vector3(ToitChaumePasGrilleMetres, ToitChaumeHauteurMetres, ToitChaumePasGrilleMetres);
        if (EstIdTorche(idObjet))
            return new Vector3(TorcheRayonMetres * 2f, TorcheHauteurMetres, TorcheRayonMetres * 2f);
        if (idObjet == 200)
            return new Vector3(1.2f, 1.0f, 0.9f);
        if (idObjet == IdObjetTableBoisDecorative)
            return new Vector3(1.2f, 1.0f, 0.9f);
        if (idObjet == IdObjetTableArtisanaTier1)
            return new Vector3(1.35f, 1.05f, 1.0f);
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

    private bool EstPositionStructureLibre(int idObjet, Vector3 pointDeChute, Vector3 pointAligne, float? yBaseFondationPlancher = null)
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
            Vector3 posRef = ip.GlobalPosition;
            float dx = Mathf.Abs(posPose.X - posRef.X);
            float dz = Mathf.Abs(posPose.Z - posRef.Z);
            // Planchers voisins sur fondations adjacentes (~3,985 m) : autorisés (un plancher par fondation).
            if (EstIdPlancher(idObjet) && EstIdPlancher(ip.ID_Objet))
            {
                if (dx <= ToleranceSolSurFondationMetres && dz <= ToleranceSolSurFondationMetres)
                    return false;
                continue;
            }
            // Règle gameplay demandée : pour poser un plancher sur une fondation valide,
            // rien d'autre qu'un autre plancher ne doit bloquer la pose.
            if (EstIdPlancher(idObjet))
                continue;
            // Autres fondations voisines (souvent plus hautes) : n'empêchent pas le plancher sur la fondation visée.
            if (EstIdPlancher(idObjet) && EstIdFondation(ip.ID_Objet))
                continue;
            // Les murets en bordure ne doivent pas bloquer la pose d'un plancher.
            if (EstIdPlancher(idObjet) && EstIdMuret(ip.ID_Objet))
                continue;
            // Les murs et portes au-dessus d'une fondation ne doivent pas bloquer la pose d'un plancher.
            if (EstIdPlancher(idObjet) && (EstIdMurBois(ip.ID_Objet) || EstIdPorteBois(ip.ID_Objet)))
                continue;
            // Le muret est volontairement "accolé" à une fondation : ce contact ne bloque pas la pose.
            if (EstIdMuret(idObjet) && EstIdFondation(ip.ID_Objet))
                continue;
            // La porte est volontairement imbriquée dans le mur cadre de porte.
            if (EstIdPorteBois(idObjet) && EstIdMurBoisCadrePorte(ip.ID_Objet))
                continue;
            // Le battant de porte est contenu dans la travée du mur: autoriser le recouvrement avec les supports.
            if (EstIdPorteBois(idObjet) && (EstIdMuret(ip.ID_Objet) || EstIdMurBois(ip.ID_Objet)))
                continue;
            // Un seul battant par cadre.
            if (EstIdPorteBois(idObjet) && EstIdPorteBois(ip.ID_Objet))
            {
                if (dx <= 0.2f && dz <= 0.2f && Mathf.Abs(posPose.Y - posRef.Y) <= 0.35f)
                    return false;
                continue;
            }
            Vector3 dimsRef = ObtenirDimensionsApproxStructurePose(ip.ID_Objet);
            float yTolerance = Mathf.Min(dimsPose.Y, dimsRef.Y) - MargeChevauchementMetres;
            if (yBaseFondationPlancher.HasValue && EstIdFondation(ip.ID_Objet)
                && Mathf.Abs(posRef.Y - yBaseFondationPlancher.Value) > 0.35f)
                continue;
            if (Mathf.Abs(posPose.Y - posRef.Y) > yTolerance)
                continue;
            float xTolerance = ((dimsPose.X + dimsRef.X) * 0.5f) - MargeChevauchementMetres;
            float zTolerance = ((dimsPose.Z + dimsRef.Z) * 0.5f) - MargeChevauchementMetres;
            if (dx < xTolerance && dz < zTolerance)
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
        if (EstIdPlancher(idObjet))
            return EstPositionStructureLibre(idObjet, pointDeChute, pointAligne);
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
        float[] yOffsets = EstIdFondation(idObjet)
            ? new float[] { 0f, HauteurApproxFondationMetres, HauteurApproxFondationMetres * 2f, HauteurApproxFondationMetres * 3f }
            : (EstIdMuret(idObjet)
                ? new float[] { 0f, MuretHauteurMetres, MuretHauteurMetres * 2f, 0.2f, -0.2f }
                : new float[] { 0f, 0.2f, -0.2f, 0.4f });

        for (int ring = 1; ring <= 4; ring++)
        {
            float step = baseStep * ring;
            for (int d = 0; d < directions.Length; d++)
            {
                for (int y = 0; y < yOffsets.Length; y++)
                {
                    Vector3 candidatChute = pointDeChute + new Vector3(directions[d].X * step, yOffsets[y], directions[d].Y * step);
                    ItemPhysique? fondationRef = null;
                    bool empileCandidat = false;
                    if (EstIdFondation(idObjet))
                    {
                        if (EssayerResoudreFondationHoteEmpilement(candidatChute, out ItemPhysique hote, out Vector3 xzCentre))
                        {
                            fondationRef = hote;
                            empileCandidat = true;
                            candidatChute.X = xzCentre.X;
                            candidatChute.Z = xzCentre.Z;
                        }
                        candidatChute.Y = CalculerYPoseFondation(candidatChute.Y, fondationRef);
                    }
                    Vector3 candidatAligne = EstIdFondation(idObjet)
                        ? CalculerPositionPoseFondation(candidatChute, empileCandidat)
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
        bool estPitFeu = EstIdPitFeu(slot.ID) || EstIdFondation(slot.ID) || EstIdPlancher(slot.ID) || EstIdMuret(slot.ID) || EstIdMurBois(slot.ID) || EstIdPorteBois(slot.ID) || EstIdToitChaume(slot.ID);
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
