using Godot;

public partial class Joueur
{
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

    /// <summary>True si la grille « sac » du menu anatomie doit s’afficher (sac ou ceinture à poches équipé).</summary>
    public bool AStockageSacOuCeintureEquipe() =>
        (!EquipementSacDos.EstVide && EstObjetQuiDebloqueGrilleSac(EquipementSacDos.ID)) ||
        (!EquipementCeinture.EstVide && EstObjetQuiDebloqueGrilleSac(EquipementCeinture.ID));

    /// <summary>Équipe un sac ; passer un slot vide pour retirer (ou utiliser <see cref="RetirerEquipementSacDos"/>).</summary>
    public void AssignerEquipementSacDos(SlotInventaire slot)
    {
        EquipementSacDos = slot;
        NotifierChangementEquipementCorps();
    }

    public void RetirerEquipementSacDos()
    {
        EquipementSacDos = new SlotInventaire();
        NotifierChangementEquipementCorps();
    }

    public void AssignerEquipementCeinture(SlotInventaire slot)
    {
        EquipementCeinture = slot;
        NotifierChangementEquipementCorps();
    }

    public void RetirerEquipementCeinture()
    {
        EquipementCeinture = new SlotInventaire();
        NotifierChangementEquipementCorps();
    }

    private void NotifierChangementEquipementCorps()
    {
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }
}
