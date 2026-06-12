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

    private static bool EstCraftArtisana(int idObjet)
    {
        return idObjet == 200 // Table de craft / atelier
            || idObjet == IdObjetTableArtisanaTier1
            || idObjet == IdObjetTableAnalyseTier1
            || idObjet == IdObjetRackBatons
            || idObjet == IdObjetRackBuches
            || idObjet == IdObjetBolBois
            || idObjet == IdObjetMailletBois
            || idObjet == IdObjetMortierPilonBois
            || idObjet == IdObjetAtelleJambe
            || idObjet == IdObjetAtelleBras
            || idObjet == IdObjetBandageTier1
            || idObjet == IdObjetCoffreBoisTier0
            || idObjet == IdObjetSolBois
            || idObjet == IdObjetSolRoche
            || idObjet == IdObjetFondationBois
            || idObjet == IdObjetFondationRoche
            || idObjet == IdObjetFondationBoisSoleRoche
            || idObjet == IdObjetFondationRocheSoleBois
            || idObjet == IdObjetMuretBois
            || idObjet == IdObjetMuretPierre;
    }

    private static bool EstCraftForgeron(int idObjet)
    {
        return idObjet == IdObjetPellePierreTier0
            || idObjet == IdObjetPiochePierreTier0
            || idObjet == IdObjetLancePierreTier0
            || idObjet == IdObjetFauxPierreTier0
            || idObjet == IdObjetHachePierreTier1
            || idObjet == 105
            || idObjet == IdObjetMailletBois;
    }

    private static bool EstRecetteAutoriseeTableStructuresT1(int idObjet)
    {
        return idObjet == IdObjetFondationBois
            || idObjet == IdObjetFondationRoche
            || idObjet == IdObjetFondationBoisSoleRoche
            || idObjet == IdObjetFondationRocheSoleBois
            || idObjet == IdObjetSolBois
            || idObjet == IdObjetSolRoche
            || idObjet == IdObjetMuretBois
            || idObjet == IdObjetMuretPierre
            || idObjet == IdObjetMurBois
            || idObjet == IdObjetMurBoisFenetre
            || idObjet == IdObjetMurBoisCadrePorte
            || idObjet == IdObjetPorteBois
            || idObjet == IdObjetToitChaume
            || idObjet == IdObjetFenetreBois;
    }

    public SlotInventaire AppliquerBonusMetierTraisageAuResultatCraft(SlotInventaire resultatCraft)
    {
        if (resultatCraft.EstVide)
            return resultatCraft;
        SlotInventaire resultat = resultatCraft;
        resultat.Quantite = ObtenirQuantiteSlot(resultat);
        if (EstCraftSacOuCeintureTraisage(resultat.ID))
            AjouterXpMetier("Traisage", 1UL);
        if (EstCraftArtisana(resultat.ID))
            AjouterXpMetier("Artisana", 1UL);
        if (EstCraftForgeron(resultat.ID))
            AjouterXpMetier("Forgeron", 1UL);
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

    private static bool EstMiniMorceauBucheBol(in SlotInventaire s) =>
        Atlas_Matiere.EstBucheRondinCourtPourBolBois(s);

    private bool TrouverIndexDagueRecetteBol(SlotInventaire[] grille, int nCases, out int indexDague)
    {
        indexDague = -1;
        int indexBuche = -1;
        int nbIngredients = 0;
        for (int i = 0; i < nCases && i < grille.Length; i++)
        {
            SlotInventaire s = grille[i];
            if (s.EstVide)
                continue;
            nbIngredients++;
            if (s.ID == 105)
                indexDague = i;
            else if (EstMiniMorceauBucheBol(s))
                indexBuche = i;
            else
                return false;
        }
        return nbIngredients == 2 && indexDague >= 0 && indexBuche >= 0;
    }

    /// <summary>Analyse la grille craft ; le détail des recettes est dans <see cref="Atlas_Matiere.EvaluerRecette"/>.</summary>
    public void VerifierRecettes()
    {
        if (StockageRackBatonsOuvert || StockageCoffreOuvert || StockageFourTorchieOuvert)
        {
            SlotResultatCraft = new SlotInventaire();
            return;
        }
        SlotInventaire[] g = ObtenirGrilleCraftAffichee();
        if (g == null) return;
        SlotInventaire resultat = Atlas_Matiere.EvaluerRecette(g, CraftGrille3x3AuTable, IdStationCraftOuverte);
        if (!resultat.EstVide && IdStationCraftOuverte == IdObjetTableArtisanaTier1 && !EstRecetteAutoriseeTableStructuresT1(resultat.ID))
            resultat = new SlotInventaire();
        if (!resultat.EstVide && !EstCraftDebloque(resultat))
            resultat = new SlotInventaire();
        SlotResultatCraft = resultat;
    }

    /// <summary>Vide la zone craft utilisée (4 cases en poche, 9 sur l’atelier) après prise du résultat.</summary>
    public void ConsommerIngredientsCraft()
    {
        if (StockageCoffreOuvert)
            return;
        SlotInventaire[] g = ObtenirGrilleCraftAffichee();
        if (g == null) return;
        int n = CraftGrille3x3AuTable ? 9 : 4;
        bool donneXpDextiriter = CraftDonneXpDextiriter(g, n);
        int indexDagueRecetteBol = -1;
        bool estCraftBolBois = false;
        if (SlotResultatCraft.ID == IdObjetBolBois)
            estCraftBolBois = TrouverIndexDagueRecetteBol(g, n, out indexDagueRecetteBol);

        int indexBolEauArgileHumid = -1;
        int indexArgileHumid = -1;
        bool estCraftArgileHumidifiee = SlotResultatCraft.ID == IdObjetArgileHumidifiee;
        if (estCraftArgileHumidifiee)
        {
            for (int i = 0; i < n && i < g.Length; i++)
            {
                SlotInventaire s = g[i];
                if (s.EstVide) continue;
                if (s.ID == IdObjetBolEau)
                    indexBolEauArgileHumid = i;
                else if (Atlas_Matiere.EstSlotVoxelArgile(s))
                    indexArgileHumid = i;
                else
                {
                    estCraftArgileHumidifiee = false;
                    break;
                }
            }
            if (indexBolEauArgileHumid < 0 || indexArgileHumid < 0)
                estCraftArgileHumidifiee = false;
        }

        for (int i = 0; i < n && i < g.Length; i++)
        {
            if (g[i].EstVide) continue;
            if (estCraftBolBois && i == indexDagueRecetteBol)
            {
                SlotInventaire dague = g[i];
                Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref dague);
                // La dague sert d'outil de sculpture: -2 de durabilité mais n'est jamais consommée comme ingrédient.
                dague.DurabiliteOutilActuelle = Mathf.Max(1f, dague.DurabiliteOutilActuelle - 2f);
                g[i] = dague;
                continue;
            }
            if (estCraftArgileHumidifiee && i == indexBolEauArgileHumid)
            {
                SlotInventaire bolEau = g[i];
                g[i] = new SlotInventaire
                {
                    ID = IdObjetBolBois,
                    IndexBotanique = bolEau.IndexBotanique,
                    IndexChimique = bolEau.IndexChimique,
                    IndexMorphologique = bolEau.IndexMorphologique,
                    Quantite = ObtenirQuantiteSlot(bolEau)
                };
                continue;
            }
            if (estCraftArgileHumidifiee && i == indexArgileHumid)
            {
                int qArgile = ObtenirQuantiteSlot(g[i]) - 1;
                g[i] = qArgile <= 0 ? new SlotInventaire() : new SlotInventaire
                {
                    ID = g[i].ID,
                    IndexBotanique = g[i].IndexBotanique,
                    IndexChimique = g[i].IndexChimique,
                    IndexMorphologique = g[i].IndexMorphologique,
                    IndexTaille = g[i].IndexTaille,
                    ScaleEclat = g[i].ScaleEclat,
                    EstUnEclat = g[i].EstUnEclat,
                    MeshEclat = g[i].MeshEclat,
                    NiveauFracture = g[i].NiveauFracture,
                    GenomeAssemblage = g[i].GenomeAssemblage,
                    Quantite = qArgile
                };
                continue;
            }
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
