using Godot;
using System;

public partial class Joueur
{
    private const string PrefixConfigPochettesCeinture = "PCH:";
    private const int NiveauCordeSolideTier2 = 2;
    public static int ObtenirQuantiteSlot(SlotInventaire s)
    {
        if (s.ID == 0) return 0;
        return s.Quantite > 0 ? s.Quantite : 1;
    }

    public static int ObtenirPileMax(SlotInventaire s)
    {
        if (s.EstVide) return 0;
        if (s.ID == Joueur.IdObjetBaie) return 20;
        if (s.ID == 30 || s.ID == 32) return 30;
        if (s.ID is 15 or 16 or 17 or 20 or 21) return 15;
        if (ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexTaille <= 1) return 5;
        return 1;
    }

    private static bool EstObjetFlexibleComposeAvecTag(SlotInventaire s) =>
        !s.EstVide && (s.ID == 20 || s.ID == 21 || s.ID == Joueur.IdObjetCeinturePoches || s.ID == Joueur.IdObjetCeintureSacoches || s.ID == Joueur.IdObjetPochetteTier0 || s.ID == Joueur.IdObjetSacTier0 || s.ID == Joueur.IdObjetRackBatons || s.ID == Joueur.IdObjetRackBuches);

    private static bool EstEncodageLegacyLiane(SlotInventaire s) =>
        EstObjetFlexibleComposeAvecTag(s) && s.IndexChimique == 16 && s.IndexMorphologique == 16 && s.IndexBotanique < NiveauCordeSolideTier2;

    private static bool EstEncodageLegacyHerbeSolide(SlotInventaire s) =>
        EstObjetFlexibleComposeAvecTag(s) && s.IndexChimique == 15 && s.IndexMorphologique == 15 && s.IndexBotanique >= NiveauCordeSolideTier2;

    public static bool EstVarianteLiane(SlotInventaire s) =>
        !s.EstVide && (s.IndexBotanique == Joueur.TagVarianteLiane || EstEncodageLegacyLiane(s));

    public static bool EstVarianteHerbeSolide(SlotInventaire s) =>
        !s.EstVide && (s.IndexBotanique == Joueur.TagVarianteHerbeSolide || EstEncodageLegacyHerbeSolide(s));

    public static bool EstSacTier0Liane(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetSacTier0 && EstVarianteLiane(s);
    public static bool EstSacTier0HerbeSolide(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetSacTier0 && EstVarianteHerbeSolide(s);
    public static bool EstCeintureSacochesHerbeSolide(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetCeintureSacoches && EstVarianteHerbeSolide(s);
    public static int ObtenirCapaciteSacStockage(SlotInventaire sacEquipe) => EstSacTier0HerbeSolide(sacEquipe) ? 2 : 1;
    private static int ObtenirCapacitePochetteDepuisTag(byte tag) => tag == Joueur.TagVarianteHerbeSolide ? 2 : 1;
    private static int ObtenirMultiplicateurPilePochetteDepuisTag(byte tag) => tag == Joueur.TagVarianteLiane ? 2 : 1;

    private static byte[] ObtenirTagsPochettesCeinture(SlotInventaire ceinture)
    {
        // Compatibilité anciens objets: infère 4 pochettes homogènes depuis la variante globale.
        byte tagCompat = EstVarianteHerbeSolide(ceinture) ? Joueur.TagVarianteHerbeSolide
            : (EstVarianteLiane(ceinture) ? Joueur.TagVarianteLiane : (byte)0);
        var tagsParDefaut = new byte[] { tagCompat, tagCompat, tagCompat, tagCompat };
        if (string.IsNullOrEmpty(ceinture.GenomeAssemblage) || !ceinture.GenomeAssemblage.StartsWith(PrefixConfigPochettesCeinture))
            return tagsParDefaut;

        string raw = ceinture.GenomeAssemblage.Substring(PrefixConfigPochettesCeinture.Length);
        string[] parts = raw.Split(',');
        if (parts.Length != 4) return tagsParDefaut;
        var tags = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            if (!byte.TryParse(parts[i], out tags[i]))
                return tagsParDefaut;
        }
        return tags;
    }

    public static string EncoderConfigPochettesCeinture(byte p0, byte p1, byte p2, byte p3)
        => $"{PrefixConfigPochettesCeinture}{p0},{p1},{p2},{p3}";

    public static int ObtenirCapaciteCeintureStockage(SlotInventaire ceintureEquipe)
    {
        var tags = ObtenirTagsPochettesCeinture(ceintureEquipe);
        int cap = 0;
        for (int i = 0; i < tags.Length; i++)
            cap += ObtenirCapacitePochetteDepuisTag(tags[i]);
        return Mathf.Clamp(cap, 1, 16);
    }

    public static int ObtenirMultiplicateurPileCeintureSlot(SlotInventaire ceintureEquipe, int indexSlot)
    {
        if (indexSlot < 0) return 1;
        var tags = ObtenirTagsPochettesCeinture(ceintureEquipe);
        int baseSlot = 0;
        for (int i = 0; i < tags.Length; i++)
        {
            int cap = ObtenirCapacitePochetteDepuisTag(tags[i]);
            if (indexSlot >= baseSlot && indexSlot < baseSlot + cap)
                return ObtenirMultiplicateurPilePochetteDepuisTag(tags[i]);
            baseSlot += cap;
        }
        return 1;
    }

    private static bool MemeScaleInventaire(Vector3 a, Vector3 b)
    {
        const float eps = 0.0005f;
        return Mathf.Abs(a.X - b.X) <= eps
            && Mathf.Abs(a.Y - b.Y) <= eps
            && Mathf.Abs(a.Z - b.Z) <= eps;
    }

    public static bool SontEmpilables(SlotInventaire a, SlotInventaire b)
    {
        if (a.EstVide || b.EstVide) return false;
        return a.ID == b.ID
            && a.IndexMorphologique == b.IndexMorphologique
            && a.IndexChimique == b.IndexChimique
            && a.IndexTaille == b.IndexTaille
            && a.NiveauFracture == b.NiveauFracture
            && a.IndexBotanique == b.IndexBotanique
            && a.IndexTailleLameRoche == b.IndexTailleLameRoche
            && a.EstUnEclat == b.EstUnEclat
            && a.GenomeAssemblage == b.GenomeAssemblage
            && a.CleConteneur == b.CleConteneur
            // Important pour le bois coupé (demi/quart): empilement uniquement si même longueur réelle.
            && MemeScaleInventaire(a.ScaleEclat, b.ScaleEclat);
    }

    private static SlotInventaire[] CopierSlots(SlotInventaire[] src, int longueur)
    {
        var dst = new SlotInventaire[longueur];
        for (int i = 0; i < longueur; i++)
            dst[i] = (src != null && i < src.Length) ? src[i] : new SlotInventaire();
        return dst;
    }

    private static string GenererCleConteneur() => Guid.NewGuid().ToString("N");

    private void SauvegarderStockageSacEquipeDansMemoire()
    {
        if (EquipementSacDos.EstVide || EquipementSacDos.ID != IdObjetSacTier0) return;
        if (string.IsNullOrEmpty(EquipementSacDos.CleConteneur))
            EquipementSacDos.CleConteneur = GenererCleConteneur();
        _memoireStockageSacs[EquipementSacDos.CleConteneur] = CopierSlots(GrilleSacStockage, ObtenirCapaciteSacStockage(EquipementSacDos));
    }

    private void SauvegarderStockageCeintureSacochesEquipeDansMemoire()
    {
        if (EquipementCeinture.EstVide || EquipementCeinture.ID != IdObjetCeintureSacoches) return;
        if (string.IsNullOrEmpty(EquipementCeinture.CleConteneur))
            EquipementCeinture.CleConteneur = GenererCleConteneur();
        _memoireStockageSacs[EquipementCeinture.CleConteneur] = CopierSlots(GrilleCeintureStockage, ObtenirCapaciteCeintureStockage(EquipementCeinture));
    }

    private void ChargerStockageDepuisSacEquipe()
    {
        int capacite = ObtenirCapaciteSacStockage(EquipementSacDos);
        if (EquipementSacDos.EstVide || EquipementSacDos.ID != IdObjetSacTier0)
        {
            GrilleSacStockage = new SlotInventaire[capacite];
            return;
        }
        if (string.IsNullOrEmpty(EquipementSacDos.CleConteneur))
            EquipementSacDos.CleConteneur = GenererCleConteneur();
        if (_memoireStockageSacs.TryGetValue(EquipementSacDos.CleConteneur, out var slots))
            GrilleSacStockage = CopierSlots(slots, capacite);
        else
            GrilleSacStockage = new SlotInventaire[capacite];
    }

    private void ChargerStockageDepuisCeintureSacochesEquipe()
    {
        int capacite = ObtenirCapaciteCeintureStockage(EquipementCeinture);
        if (EquipementCeinture.EstVide || EquipementCeinture.ID != IdObjetCeintureSacoches)
        {
            GrilleCeintureStockage = new SlotInventaire[capacite];
            return;
        }
        if (string.IsNullOrEmpty(EquipementCeinture.CleConteneur))
            EquipementCeinture.CleConteneur = GenererCleConteneur();
        if (_memoireStockageSacs.TryGetValue(EquipementCeinture.CleConteneur, out var slots))
            GrilleCeintureStockage = CopierSlots(slots, capacite);
        else
            GrilleCeintureStockage = new SlotInventaire[capacite];
    }

    public ref SlotInventaire RefSlotSac(int idx) => ref GrilleSacStockage[idx];

    public ref SlotInventaire RefSlotCeintureStockage(int idx) => ref GrilleCeintureStockage[idx];

    public bool ASacEquipe() => !EquipementSacDos.EstVide && EquipementSacDos.ID == IdObjetSacTier0;

    public bool ACeintureSacochesEquipe() => !EquipementCeinture.EstVide && EquipementCeinture.ID == IdObjetCeintureSacoches;

    /// <summary>Grille affichée et utilisée pour les clics craft : plan de l’atelier (9) ou poche (4). Le coffre utilise <see cref="RefSlotCoffreStockage"/> (10 slots), pas cette méthode.</summary>
    public SlotInventaire[] ObtenirGrilleCraftAffichee()
    {
        if (StockageRackBatonsOuvert && RackBatonsOuvert != null && GodotObject.IsInstanceValid(RackBatonsOuvert))
            return RackBatonsOuvert.GrillePlanTravailAtelier;
        if (CraftGrille3x3AuTable && AtelierPlanTravailOuvert != null && GodotObject.IsInstanceValid(AtelierPlanTravailOuvert))
            return AtelierPlanTravailOuvert.GrillePlanTravailAtelier;
        return GrilleCraftPoche;
    }

    public ref SlotInventaire RefSlotCraft(int idx)
    {
        if (StockageRackBatonsOuvert && RackBatonsOuvert != null && GodotObject.IsInstanceValid(RackBatonsOuvert))
            return ref RackBatonsOuvert.GrillePlanTravailAtelier[idx];
        if (CraftGrille3x3AuTable && AtelierPlanTravailOuvert != null && GodotObject.IsInstanceValid(AtelierPlanTravailOuvert))
            return ref AtelierPlanTravailOuvert.GrillePlanTravailAtelier[idx];
        return ref GrilleCraftPoche[idx];
    }

    /// <summary>Réinitialise les flags d’inventaire liés à un conteneur monde si le nœud a été libéré (chunk, minage, déchargement) — évite que <see cref="RefSlotCoffreStockage"/> retombe sur <c>GrilleCraftPoche[0]</c> pour tous les index.</summary>
    public void ReinitialiserConteneurOuvertSiReferencePerdue()
    {
        if (StockageCoffreOuvert && (CoffreOuvert == null || !GodotObject.IsInstanceValid(CoffreOuvert)))
        {
            StockageCoffreOuvert = false;
            CoffreOuvert = null;
        }
        if (StockageRackBatonsOuvert && (RackBatonsOuvert == null || !GodotObject.IsInstanceValid(RackBatonsOuvert)))
        {
            StockageRackBatonsOuvert = false;
            RackBatonsOuvert = null;
        }
        if (AtelierPlanTravailOuvert != null && !GodotObject.IsInstanceValid(AtelierPlanTravailOuvert))
        {
            AtelierPlanTravailOuvert = null;
            CraftGrille3x3AuTable = false;
        }
    }

    /// <summary>10 slots du coffre ouvert (menu Q). Garde-fou : index 0–9.</summary>
    public ref SlotInventaire RefSlotCoffreStockage(int idx)
    {
        if (!StockageCoffreOuvert || CoffreOuvert == null || !GodotObject.IsInstanceValid(CoffreOuvert))
            return ref GrilleCraftPoche[0];
        idx = Mathf.Clamp(idx, 0, 9);
        return ref CoffreOuvert.GrilleStockageCoffre[idx];
    }

    /// <summary>Copie la grille du coffre posé vers la mémoire joueur (ramassage / cohérence inventaire).</summary>
    public void MemoriserContenuCoffreDepuisItem(ItemPhysique item, string cle)
    {
        if (item == null || item.GrilleStockageCoffre == null || item.GrilleStockageCoffre.Length < 10) return;
        string k = string.IsNullOrEmpty(cle) ? GenererCleConteneur() : cle;
        _memoireStockageSacs[k] = CopierSlots(item.GrilleStockageCoffre, 10);
    }

    /// <summary>Remplit la grille du coffre posé depuis la mémoire (repose depuis inventaire).</summary>
    public void RestaurerContenuCoffreSurItem(ItemPhysique item, string cle)
    {
        if (item == null || item.GrilleStockageCoffre == null) return;
        for (int i = 0; i < 10; i++)
            item.GrilleStockageCoffre[i] = new SlotInventaire();
        if (string.IsNullOrEmpty(cle) || !_memoireStockageSacs.TryGetValue(cle, out var slots) || slots == null)
            return;
        int n = Mathf.Min(10, slots.Length);
        for (int i = 0; i < n; i++)
            item.GrilleStockageCoffre[i] = slots[i];
    }

    /// <summary>True si l’objet ne doit pas entrer dans un coffre (structures lourdes, coffre dans coffre).</summary>
    public static bool EstObjetInterditDansCoffre(SlotInventaire s)
    {
        if (s.EstVide) return false;
        return s.ID == 200 || s.ID == IdObjetRackBatons || s.ID == IdObjetRackBuches || s.ID == IdObjetCoffreBoisTier0;
    }

    public static bool EstSlotStockableRackBatons(SlotInventaire s) => !s.EstVide && (s.ID == 30 || s.ID == 32);

    public static bool EstSlotStockableRackBuches(SlotInventaire s) => !s.EstVide && s.ID == 30;

    public bool RackOuvertEstBuches()
    {
        return StockageRackBatonsOuvert
            && RackBatonsOuvert != null
            && GodotObject.IsInstanceValid(RackBatonsOuvert)
            && RackBatonsOuvert.ID_Objet == IdObjetRackBuches;
    }

    public int ObtenirCapaciteRackOuvert() => RackOuvertEstBuches() ? 10 : 30;

    public bool EstSlotStockableDansRackOuvert(SlotInventaire s)
    {
        return RackOuvertEstBuches() ? EstSlotStockableRackBuches(s) : EstSlotStockableRackBatons(s);
    }

    public int CompterQuantiteRackOuvert()
    {
        if (!(StockageRackBatonsOuvert && RackBatonsOuvert != null && GodotObject.IsInstanceValid(RackBatonsOuvert)))
            return 0;
        int capacite = ObtenirCapaciteRackOuvert();
        int total = 0;
        var g = RackBatonsOuvert.GrillePlanTravailAtelier;
        int n = Mathf.Min(9, g.Length);
        for (int i = 0; i < n; i++)
        {
            if (!EstSlotStockableDansRackOuvert(g[i])) continue;
            total += ObtenirQuantiteSlot(g[i]);
        }
        return Mathf.Clamp(total, 0, capacite);
    }

    public int CompterQuantiteRackBatons() => CompterQuantiteRackOuvert();

    /// <summary>True si la grille « sac » du menu anatomie doit s’afficher (phase actuelle : sac tier 0 équipé).</summary>
    public bool AStockageSacOuCeintureEquipe() => ASacEquipe();

    /// <summary>Équipe un sac ; passer un slot vide pour retirer (ou utiliser <see cref="RetirerEquipementSacDos"/>).</summary>
    public void AssignerEquipementSacDos(SlotInventaire slot)
    {
        SauvegarderStockageSacEquipeDansMemoire();
        EquipementSacDos = slot;
        ChargerStockageDepuisSacEquipe();
        NotifierChangementEquipementCorps();
    }

    public void RetirerEquipementSacDos()
    {
        SauvegarderStockageSacEquipeDansMemoire();
        EquipementSacDos = new SlotInventaire();
        ChargerStockageDepuisSacEquipe();
        NotifierChangementEquipementCorps();
    }

    public void AssignerEquipementCeinture(SlotInventaire slot)
    {
        SauvegarderStockageCeintureSacochesEquipeDansMemoire();
        EquipementCeinture = slot;
        ChargerStockageDepuisCeintureSacochesEquipe();
        NotifierChangementEquipementCorps();
    }

    public void RetirerEquipementCeinture()
    {
        SauvegarderStockageCeintureSacochesEquipeDansMemoire();
        EquipementCeinture = new SlotInventaire();
        ChargerStockageDepuisCeintureSacochesEquipe();
        NotifierChangementEquipementCorps();
    }

    private void NotifierChangementEquipementCorps()
    {
        RafraichirVisuelsEquipementsCorps();
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }
}
