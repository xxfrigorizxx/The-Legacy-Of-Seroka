public partial class Joueur
{
    /// <summary>Analyse la grille craft ; le détail des recettes est dans <see cref="Atlas_Matiere.EvaluerRecette"/>.</summary>
    public void VerifierRecettes()
    {
        if (StockageRackBatonsOuvert)
        {
            SlotResultatCraft = new SlotInventaire();
            return;
        }
        SlotInventaire[] g = ObtenirGrilleCraftAffichee();
        if (g == null) return;
        SlotResultatCraft = Atlas_Matiere.EvaluerRecette(g, CraftGrille3x3AuTable);
    }

    /// <summary>Vide la zone craft utilisée (4 cases en poche, 9 sur l’atelier) après prise du résultat.</summary>
    public void ConsommerIngredientsCraft()
    {
        SlotInventaire[] g = ObtenirGrilleCraftAffichee();
        if (g == null) return;
        int n = CraftGrille3x3AuTable ? 9 : 4;
        for (int i = 0; i < n && i < g.Length; i++)
        {
            if (g[i].EstVide) continue;
            int q = ObtenirQuantiteSlot(g[i]) - 1;
            if (q <= 0)
                g[i] = new SlotInventaire();
            else
                g[i].Quantite = q;
        }
    }
}
