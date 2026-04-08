using Godot;
using System;

public partial class Joueur
{
    public static int ObtenirQuantiteSlot(SlotInventaire s)
    {
        if (s.ID == 0) return 0;
        return s.Quantite > 0 ? s.Quantite : 1;
    }

    public static int ObtenirPileMax(SlotInventaire s)
    {
        if (s.EstVide) return 0;
        if (s.ID == Joueur.IdObjetBaie) return 20;
        if (s.ID is 15 or 16 or 17 or 20 or 21) return 15;
        if (ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexTaille <= 1) return 5;
        return 1;
    }

    public static bool EstVarianteLiane(SlotInventaire s) => !s.EstVide && s.IndexBotanique == Joueur.TagVarianteLiane;
    public static bool EstVarianteHerbeSolide(SlotInventaire s) => !s.EstVide && s.IndexBotanique == Joueur.TagVarianteHerbeSolide;

    public static bool EstSacTier0Liane(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetSacTier0 && EstVarianteLiane(s);
    public static bool EstSacTier0HerbeSolide(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetSacTier0 && EstVarianteHerbeSolide(s);
    public static bool EstCeintureSacochesHerbeSolide(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetCeintureSacoches && EstVarianteHerbeSolide(s);
    public static int ObtenirCapaciteSacStockage(SlotInventaire sacEquipe) => EstSacTier0HerbeSolide(sacEquipe) ? 2 : 1;
    public static int ObtenirCapaciteCeintureStockage(SlotInventaire ceintureEquipe) => EstCeintureSacochesHerbeSolide(ceintureEquipe) ? 8 : 4;

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
            && a.CleConteneur == b.CleConteneur;
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

    /// <summary>Grille affichée et utilisée pour les clics craft : plan de l’atelier (9) ou poche (4).</summary>
    public SlotInventaire[] ObtenirGrilleCraftAffichee()
    {
        if (CraftGrille3x3AuTable && AtelierPlanTravailOuvert != null && GodotObject.IsInstanceValid(AtelierPlanTravailOuvert))
            return AtelierPlanTravailOuvert.GrillePlanTravailAtelier;
        return GrilleCraftPoche;
    }

    public ref SlotInventaire RefSlotCraft(int idx)
    {
        if (CraftGrille3x3AuTable && AtelierPlanTravailOuvert != null && GodotObject.IsInstanceValid(AtelierPlanTravailOuvert))
            return ref AtelierPlanTravailOuvert.GrillePlanTravailAtelier[idx];
        return ref GrilleCraftPoche[idx];
    }

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
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }
}
