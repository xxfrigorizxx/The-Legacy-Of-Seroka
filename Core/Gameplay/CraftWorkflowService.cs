using Godot;

public partial class Joueur
{
    private static bool EstCraftSacOuCeintureTraisage(int idObjet)
    {
        return idObjet == IdObjetSacTier0
            || idObjet == IdObjetCeinturePoches
            || idObjet == IdObjetCeintureSacoches;
    }

    private static bool EstCraftTissuOuCordePourDoubleTraisage(int idObjet)
    {
        return idObjet == 17 || idObjet == 20 || idObjet == 21;
    }

    public SlotInventaire AppliquerBonusMetierTraisageAuResultatCraft(SlotInventaire resultatCraft)
    {
        if (resultatCraft.EstVide)
            return resultatCraft;
        SlotInventaire resultat = resultatCraft;
        resultat.Quantite = ObtenirQuantiteSlot(resultat);
        if (EstCraftSacOuCeintureTraisage(resultat.ID))
            AjouterXpMetier("Traisage", 1UL);
        if (EstCraftTissuOuCordePourDoubleTraisage(resultat.ID))
        {
            float chanceDouble = Mathf.Clamp(ObtenirNiveauMetier("Traisage") * 0.0001f, 0f, 1f);
            if ((float)GD.Randf() < chanceDouble)
                resultat.Quantite += 1;
        }
        return resultat;
    }

    private static bool EstIngredientDextiriter(int idObjet)
    {
        return idObjet == 15   // Fibre d'herbe
            || idObjet == 16   // Liane
            || idObjet == 17   // Tissu
            || idObjet == 20   // Corde
            || idObjet == 21   // Corde tressée
            || idObjet == IdObjetPochetteTier0
            || idObjet == IdObjetSacTier0
            || idObjet == IdObjetCeinturePoches
            || idObjet == IdObjetCeintureSacoches;
    }

    private bool CraftDonneXpDextiriter(SlotInventaire[] grille, int casesUtilisees)
    {
        bool aUnIngredient = false;
        for (int i = 0; i < casesUtilisees && i < grille.Length; i++)
        {
            SlotInventaire s = grille[i];
            if (s.EstVide)
                continue;
            aUnIngredient = true;
            if (!EstIngredientDextiriter(s.ID))
                return false;
        }
        return aUnIngredient;
    }

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
        bool donneXpDextiriter = CraftDonneXpDextiriter(g, n);
        for (int i = 0; i < n && i < g.Length; i++)
        {
            if (g[i].EstVide) continue;
            int q = ObtenirQuantiteSlot(g[i]) - 1;
            if (q <= 0)
                g[i] = new SlotInventaire();
            else
                g[i].Quantite = q;
        }
        if (donneXpDextiriter)
            AjouterXpFutureState("Dextiriter", 2UL);
    }
}
