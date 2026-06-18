using Godot;
using System;
using System.Collections.Generic;

public static partial class Atlas_Matiere
{
    /// <param name="grilleCraft3x3Table">True uniquement si le menu craft est ouvert depuis une station : grille 3×3 et indices c0–c3 sur le bloc haut-gauche (0,1,3,4). False = inventaire (Q) : 2×2 classique (0–3), pas de 3×3.</param>
    /// <param name="idStationCraft">ID de la station de craft ouverte (0 si inventaire/poche).</param>
    public static SlotInventaire EvaluerRecette(SlotInventaire[] grille, bool grilleCraft3x3Table = false, int idStationCraft = 0)
    {
        if (grille == null || grille.Length < 4)
            return new SlotInventaire();

        int nCell = grilleCraft3x3Table ? Mathf.Min(grille.Length, 9) : 4;
        int strideColonne = grilleCraft3x3Table ? 3 : 2;

        var ingredients = new List<SlotInventaire>();
        var indicesIngredients = new List<int>();
        for (int i = 0; i < nCell; i++)
        {
            if (!grille[i].EstVide)
            {
                ingredients.Add(grille[i]);
                indicesIngredients.Add(i);
            }
        }
        if (ingredients.Count == 0)
            return new SlotInventaire();

        if (ingredients.Count == 3)
        {
            bool argileHumid = false;
            bool fibreHerbe = false;
            bool boue = false;
            bool invalide = false;
            for (int i = 0; i < ingredients.Count; i++)
            {
                SlotInventaire s = ingredients[i];
                if (s.ID == Joueur.IdObjetArgileHumidifiee) argileHumid = true;
                else if (EstSlotBrinHerbe(s)) fibreHerbe = true;
                else if (EstSlotVoxelBoue(s)) boue = true;
                else invalide = true;
            }
            if (!invalide && argileHumid && fibreHerbe && boue)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetTorchie,
                    Quantite = 3,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0
                };
            }
        }

        static bool EstSlotTorchieCraft(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetTorchie;
        static bool EstSlotArgileHumidifieeCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == Joueur.IdObjetArgileHumidifiee;
        static bool EstSlotOsBoeufCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == Joueur.IdObjetOsBoeuf;

        // Pince en os (160) 3×3 — 4× os :
        // ( )(O)( )
        // ( )(O)( )
        // (O)( )(O)
        if (grilleCraft3x3Table && grille.Length >= 9
            && grille[0].EstVide && EstSlotOsBoeufCraft(grille[1]) && grille[2].EstVide
            && grille[3].EstVide && EstSlotOsBoeufCraft(grille[4]) && grille[5].EstVide
            && EstSlotOsBoeufCraft(grille[6]) && grille[7].EstVide && EstSlotOsBoeufCraft(grille[8]))
        {
            return new SlotInventaire
            {
                ID = Joueur.IdObjetPinceOs,
                Quantite = 1,
                IndexChimique = 0,
                IndexMorphologique = 0,
                IndexTaille = 0,
                ScaleEclat = Vector3.One,
                EstUnEclat = false,
                MeshEclat = null,
                NiveauFracture = 0
            };
        }

        // Bol en argile (158) 3×3 — 3× argile humidifiée en V :
        // ( )( )( )
        // (A)( )(A)
        // ( )(A)( )
        if (grilleCraft3x3Table && grille.Length >= 9
            && grille[0].EstVide && grille[1].EstVide && grille[2].EstVide
            && EstSlotArgileHumidifieeCraft(grille[3]) && grille[4].EstVide && EstSlotArgileHumidifieeCraft(grille[5])
            && grille[6].EstVide && EstSlotArgileHumidifieeCraft(grille[7]) && grille[8].EstVide)
        {
            return new SlotInventaire
            {
                ID = Joueur.IdObjetBolArgile,
                Quantite = 1,
                IndexChimique = 0,
                IndexMorphologique = 0,
                IndexTaille = 0,
                ScaleEclat = Vector3.One,
                EstUnEclat = false,
                MeshEclat = null,
                NiveauFracture = 0
            };
        }

        // Moule en argile (161) 3×3 — 5× argile humidifiée :
        // (A)( )(A)
        // (A)(A)(A)
        if (grilleCraft3x3Table && grille.Length >= 9
            && EstSlotArgileHumidifieeCraft(grille[0]) && grille[1].EstVide && EstSlotArgileHumidifieeCraft(grille[2])
            && EstSlotArgileHumidifieeCraft(grille[3]) && EstSlotArgileHumidifieeCraft(grille[4]) && EstSlotArgileHumidifieeCraft(grille[5])
            && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide)
        {
            return new SlotInventaire
            {
                ID = Joueur.IdObjetMouleArgile,
                Quantite = 1,
                IndexChimique = 0,
                IndexMorphologique = 0,
                IndexTaille = 0,
                ScaleEclat = Vector3.One,
                EstUnEclat = false,
                MeshEclat = null,
                NiveauFracture = 0
            };
        }

        // Four en torchie (157) 3×3 :
        // ( )(T)( )
        // (T)(T)(T)
        // (T)( )(T)
        if (grilleCraft3x3Table && grille.Length >= 9
            && grille[0].EstVide && EstSlotTorchieCraft(grille[1]) && grille[2].EstVide
            && EstSlotTorchieCraft(grille[3]) && EstSlotTorchieCraft(grille[4]) && EstSlotTorchieCraft(grille[5])
            && EstSlotTorchieCraft(grille[6]) && grille[7].EstVide && EstSlotTorchieCraft(grille[8]))
        {
            int nf = Mathf.Max(
                Mathf.Max(Mathf.Max(grille[1].NiveauFracture, grille[3].NiveauFracture), Mathf.Max(grille[4].NiveauFracture, grille[5].NiveauFracture)),
                Mathf.Max(grille[6].NiveauFracture, grille[8].NiveauFracture));
            return new SlotInventaire
            {
                ID = Joueur.IdObjetFourTorchie,
                Quantite = 1,
                IndexChimique = 0,
                IndexMorphologique = 0,
                IndexTaille = 0,
                NiveauFracture = nf,
                EstUnEclat = false
            };
        }

        static bool EstSlotRocheVoxelBruteCraft(SlotInventaire s) => !s.EstVide && s.ID == 2;
        static SlotInventaire ConstruirePetiteRocheMarbre(int indexMorphologique)
        {
            return new SlotInventaire
            {
                ID = 47, // Marbre (profil équilibré demandé).
                IndexMorphologique = Mathf.Clamp(indexMorphologique, 0, 3),
                IndexTaille = 1,
                IndexChimique = 0,
                EstUnEclat = false,
                NiveauFracture = 0
            };
        }
        static bool EnsembleEgalePatron(HashSet<int> ensemble, params int[] patron)
        {
            if (ensemble.Count != patron.Length) return false;
            for (int i = 0; i < patron.Length; i++)
            {
                if (!ensemble.Contains(patron[i]))
                    return false;
            }
            return true;
        }

        // Façonnage roche voxel brute (ID 2) -> petite roche matière (ID 47, marbre) selon la forme du patron.
        if (ingredients.Count == 2 && EstSlotRocheVoxelBruteCraft(ingredients[0]) && EstSlotRocheVoxelBruteCraft(ingredients[1]))
        {
            int idxA = indicesIngredients[0];
            int idxB = indicesIngredients[1];
            int rowA = idxA / strideColonne;
            int colA = idxA % strideColonne;
            int rowB = idxB / strideColonne;
            int colB = idxB % strideColonne;

            bool verticale = colA == colB && Mathf.Abs(rowA - rowB) == 1;
            if (verticale)
                return ConstruirePetiteRocheMarbre(3); // Pointe

            bool horizontale = rowA == rowB && Mathf.Abs(colA - colB) == 1;
            if (horizontale)
                return ConstruirePetiteRocheMarbre(1); // Plate
        }

        if (ingredients.Count == 4)
        {
            bool toutesRochesVoxel = true;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!EstSlotRocheVoxelBruteCraft(ingredients[i]))
                {
                    toutesRochesVoxel = false;
                    break;
                }
            }

            if (toutesRochesVoxel)
            {
                int lignes = grilleCraft3x3Table ? 3 : 2;
                var indices = new HashSet<int>(indicesIngredients);
                for (int row = 0; row <= lignes - 2; row++)
                {
                    for (int col = 0; col <= strideColonne - 2; col++)
                    {
                        int origine = row * strideColonne + col;
                        if (EnsembleEgalePatron(indices, origine, origine + 1, origine + strideColonne, origine + strideColonne + 1))
                            return ConstruirePetiteRocheMarbre(0); // Ronde
                    }
                }
            }
        }

        if (grilleCraft3x3Table && ingredients.Count == 6)
        {
            bool toutesRochesVoxel = true;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!EstSlotRocheVoxelBruteCraft(ingredients[i]))
                {
                    toutesRochesVoxel = false;
                    break;
                }
            }

            if (toutesRochesVoxel)
            {
                var indices = new HashSet<int>(indicesIngredients);
                bool estRectangle2x3Ou3x2 =
                    EnsembleEgalePatron(indices, 0, 1, 3, 4, 6, 7) // 2x3 gauche
                    || EnsembleEgalePatron(indices, 1, 2, 4, 5, 7, 8) // 2x3 droite
                    || EnsembleEgalePatron(indices, 0, 1, 2, 3, 4, 5) // 3x2 haut
                    || EnsembleEgalePatron(indices, 3, 4, 5, 6, 7, 8); // 3x2 bas
                if (estRectangle2x3Ou3x2)
                    return ConstruirePetiteRocheMarbre(2); // Ovale
            }
        }

        // Un seul ingrédient : bâton brut (32, chim. 0) ou branche (31) → bâton façonné (32, chim. 1), même essence (IndexBotanique) pour solidité / crafts (rack, outils).
        if (ingredients.Count == 1)
        {
            var br = ingredients[0];
            // Établi 3x3 : rondin court fendu en 8 → maillet / pilon bois.
            bool estMiniMorceauBucheMaillet = EstBucheRondinFendueEn8PourMaillet(br);
            if (grilleCraft3x3Table && estMiniMorceauBucheMaillet)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetMailletBois,
                    IndexBotanique = br.IndexBotanique,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0
                };
            }
            if (br.ID == 32 && br.IndexChimique == 0)
            {
                return new SlotInventaire
                {
                    ID = 32,
                    IndexBotanique = br.IndexBotanique,
                    IndexChimique = 1,
                    IndexMorphologique = br.IndexMorphologique,
                    IndexTaille = br.IndexTaille,
                    ScaleEclat = br.ScaleEclat,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0
                };
            }
            if (br.ID == BlocChutant.ID_BRANCHE)
            {
                return new SlotInventaire
                {
                    ID = 32,
                    IndexBotanique = br.IndexBotanique,
                    IndexChimique = 1,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0
                };
            }
        }

        if (ingredients.Count == 2)
        {
            bool AdjacentDansGrille(int a, int b)
            {
                int diff = Mathf.Abs(a - b);
                if (diff == strideColonne) return true;
                if (diff == 1)
                    return (a / strideColonne) == (b / strideColonne);
                return false;
            }
            SlotInventaire sA = ingredients[0];
            SlotInventaire sB = ingredients[1];

            bool aBucheBol = EstBucheRondinCourtPourBolBois(sA);
            bool bBucheBol = EstBucheRondinCourtPourBolBois(sB);
            bool aDague = sA.ID == 105;
            bool bDague = sB.ID == 105;
            // Grille inventaire 2×2 (Q) ou établi 3×3 : rondin court + dague, positions libres.
            if ((aBucheBol && bDague) || (bBucheBol && aDague))
            {
                SlotInventaire sourceBois = aBucheBol ? sA : sB;
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetBolBois,
                    IndexBotanique = sourceBois.IndexBotanique,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0
                };
            }

            bool aBolEau = sA.ID == Joueur.IdObjetBolEau;
            bool bBolEau = sB.ID == Joueur.IdObjetBolEau;
            bool aArgile = EstSlotVoxelArgile(sA);
            bool bArgile = EstSlotVoxelArgile(sB);
            if ((aBolEau && bArgile) || (bBolEau && aArgile))
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetArgileHumidifiee,
                    Quantite = 1,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0
                };
            }

            // Boue : bol rempli d'eau + voxel terre aride (ID terrain 6) -> 1 voxel boue (ID terrain 7).
            bool aTerreAride = EstSlotVoxelTerreAride(sA);
            bool bTerreAride = EstSlotVoxelTerreAride(sB);
            if ((aBolEau && bTerreAride) || (bBolEau && aTerreAride))
            {
                return ConstruireSlotInventaireVoxelSurface(7);
            }

            bool aBol = sA.ID == Joueur.IdObjetBolBois;
            bool bBol = sB.ID == Joueur.IdObjetBolBois;
            bool aMaillet = sA.ID == Joueur.IdObjetMailletBois;
            bool bMaillet = sB.ID == Joueur.IdObjetMailletBois;
            if (grilleCraft3x3Table && ((aBol && bMaillet) || (bBol && aMaillet)))
            {
                SlotInventaire slotBol = aBol ? sA : sB;
                SlotInventaire slotMaillet = aMaillet ? sA : sB;
                byte essenceBol = slotBol.IndexBotanique;
                byte essencePilon = slotMaillet.IndexBotanique;
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetMortierPilonBois,
                    IndexBotanique = essenceBol,
                    IndexChimique = essencePilon,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    ScaleEclat = Vector3.One,
                    EstUnEclat = false,
                    MeshEclat = null,
                    NiveauFracture = 0,
                    GenomeAssemblage = $"MORTIERPILON:{essenceBol},{essencePilon}"
                };
            }

            bool aSilex = ItemPhysique.EstMatiereSilexParIdObjet(sA.ID);
            bool bSilex = ItemPhysique.EstMatiereSilexParIdObjet(sB.ID);
            bool aSulfure = ItemPhysique.EstIdRocheMatiere(sA.ID)
                && (ItemPhysique.IndexChimiqueDepuisIdRoche(sA.ID) == 10 || ItemPhysique.IndexChimiqueDepuisIdRoche(sA.ID) == 11);
            bool bSulfure = ItemPhysique.EstIdRocheMatiere(sB.ID)
                && (ItemPhysique.IndexChimiqueDepuisIdRoche(sB.ID) == 10 || ItemPhysique.IndexChimiqueDepuisIdRoche(sB.ID) == 11);
            if (AdjacentDansGrille(indicesIngredients[0], indicesIngredients[1]) && ((aSilex && bSulfure) || (bSilex && aSulfure)))
            {
                SlotInventaire rocheSulfureuse = aSulfure ? sA : sB;
                int idxSulfure = ItemPhysique.IndexChimiqueDepuisIdRoche(rocheSulfureuse.ID);
                float dMax = CalculerDurabiliteMaxNouvelAllumeFeu(rocheSulfureuse);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetAllumeFeu,
                    IndexChimique = idxSulfure,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    EstUnEclat = false,
                    NiveauFracture = 0,
                    DurabiliteOutilMax = dMax,
                    DurabiliteOutilActuelle = dMax
                };
            }

            bool aTissu = sA.ID == 21;
            bool bTissu = sB.ID == 21;
            bool aBranche = sA.ID == BlocChutant.ID_BRANCHE;
            bool bBranche = sB.ID == BlocChutant.ID_BRANCHE;
            if ((aTissu && bBranche) || (bTissu && aBranche))
            {
                int idxTissu = aTissu ? indicesIngredients[0] : indicesIngredients[1];
                int idxBranche = aBranche ? indicesIngredients[0] : indicesIngredients[1];
                int rowTissu = idxTissu / strideColonne;
                int colTissu = idxTissu % strideColonne;
                int rowBranche = idxBranche / strideColonne;
                int colBranche = idxBranche % strideColonne;
                bool tissuAuDessusBranche = colTissu == colBranche && rowBranche - rowTissu == 1;
                if (tissuAuDessusBranche)
                {
                    SlotInventaire tissu = aTissu ? sA : sB;
                    SlotInventaire branche = aBranche ? sA : sB;
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetTorche,
                        IndexBotanique = branche.IndexBotanique,
                        IndexChimique = tissu.IndexChimique,
                        IndexMorphologique = tissu.IndexMorphologique,
                        IndexTaille = 0,
                        EstUnEclat = false,
                        NiveauFracture = 0
                    };
                }
            }

            // 2 cordes d'herbe simples -> 1 corde d'herbe solide tier 2.
            if (ingredients[0].ID == 20 && ingredients[1].ID == 20
                && ingredients[0].IndexChimique == 15 && ingredients[0].IndexMorphologique == 15
                && ingredients[1].IndexChimique == 15 && ingredients[1].IndexMorphologique == 15
                && ingredients[0].IndexBotanique < NiveauCordeSolideTier2
                && ingredients[1].IndexBotanique < NiveauCordeSolideTier2)
            {
                return new SlotInventaire
                {
                    ID = 20,
                    IndexChimique = 15,
                    IndexMorphologique = 15,
                    IndexBotanique = NiveauCordeSolideTier2,
                    EstUnEclat = false,
                    NiveauFracture = 0
                };
            }
            // 2 fibres boyau (17) -> 1 corde boyau (17/17).
            if (ingredients[0].ID == 17 && ingredients[1].ID == 17)
            {
                return new SlotInventaire
                {
                    ID = 20,
                    IndexChimique = 17,
                    IndexMorphologique = 17,
                    IndexBotanique = LSystem_Botanique.IndexChene,
                    NiveauFracture = Mathf.Max(ingredients[0].NiveauFracture, ingredients[1].NiveauFracture),
                    EstUnEclat = false
                };
            }
            // 2 intestins (nettoyés ou bruts) -> 1 corde d'intestin.
            if (Joueur.EstIntestinUtilisablePourCraft(ingredients[0])
                && Joueur.EstIntestinUtilisablePourCraft(ingredients[1]))
            {
                return new SlotInventaire
                {
                    ID = 20,
                    IndexChimique = 17,
                    IndexMorphologique = 17,
                    IndexBotanique = Joueur.TagVarianteIntestin,
                    EstUnEclat = false,
                    NiveauFracture = 0
                };
            }
            // 2 cordes d'intestin -> 1 corde d'intestin solide.
            if (EstSlotCordeIntestinCraft(ingredients[0]) && EstSlotCordeIntestinCraft(ingredients[1]))
            {
                return new SlotInventaire
                {
                    ID = 20,
                    IndexChimique = 17,
                    IndexMorphologique = 17,
                    IndexBotanique = Joueur.TagVarianteIntestinSolide,
                    EstUnEclat = false,
                    NiveauFracture = 0
                };
            }

            // 2 fibres différentes côte à côte (gauche → droite) → corde mixte (outils / bandage ; pas tissu).
            if (AdjacentDansGrille(indicesIngredients[0], indicesIngredients[1]))
            {
                int idxA = indicesIngredients[0];
                int idxB = indicesIngredients[1];
                int idxGauche = -1;
                int idxDroite = -1;
                if (idxB - idxA == 1 && idxA / strideColonne == idxB / strideColonne)
                {
                    idxGauche = idxA;
                    idxDroite = idxB;
                }
                else if (idxA - idxB == 1 && idxB / strideColonne == idxA / strideColonne)
                {
                    idxGauche = idxB;
                    idxDroite = idxA;
                }

                if (idxGauche >= 0 && idxDroite >= 0)
                {
                    SlotInventaire gauche = grille[idxGauche];
                    SlotInventaire droite = grille[idxDroite];
                    bool gFib = EstFibreTorsadeCraft(gauche.ID);
                    bool dFib = EstFibreTorsadeCraft(droite.ID);
                    bool gInt = EstIntestinNettoyeCraft(gauche);
                    bool dInt = EstIntestinNettoyeCraft(droite);
                    if (gFib && dFib && gauche.ID != droite.ID)
                    {
                        return new SlotInventaire
                        {
                            ID = 20,
                            IndexChimique = gauche.ID,
                            IndexMorphologique = droite.ID,
                            IndexBotanique = LSystem_Botanique.IndexChene,
                            NiveauFracture = Mathf.Max(gauche.NiveauFracture, droite.NiveauFracture),
                            EstUnEclat = false
                        };
                    }
                    if ((gInt && dFib) || (gFib && dInt))
                    {
                        return new SlotInventaire
                        {
                            ID = 20,
                            IndexChimique = EncoderMatiereCordeMixte(gauche),
                            IndexMorphologique = EncoderMatiereCordeMixte(droite),
                            IndexBotanique = Joueur.TagVarianteCordeIntestinMixe,
                            NiveauFracture = Mathf.Max(gauche.NiveauFracture, droite.NiveauFracture),
                            EstUnEclat = false
                        };
                    }
                }
            }

            for (int col = 0; col < 2; col++)
            {
                if (col + strideColonne >= grille.Length) break;
                SlotInventaire haut = grille[col];
                SlotInventaire bas = grille[col + strideColonne];
                if (haut.EstVide || bas.EstVide) continue;
                // Dague : uniquement petite roche en pointe (morph 3, taille mini ou petite).
                bool estPetiteRochePointe = ItemPhysique.EstIdRocheMatiere(haut.ID) && haut.IndexMorphologique == 3
                    && (haut.IndexTaille == 0 || haut.IndexTaille == 1);
                bool estCorde = EstSlotLigatureOutilCraft(bas);
                if (estPetiteRochePointe && estCorde)
                {
                    SlotInventaire ligature = NormaliserLigatureOutil(bas);
                    float dMax = CalculerDurabiliteMaxNouvelleDague(haut, ligature);
                    return new SlotInventaire
                    {
                        ID = 105,
                        IndexChimique = haut.ID - ItemPhysique.IdRocheMatiereMin,
                        IndexMorphologique = ligature.IndexChimique,
                        IndexTaille = ligature.IndexMorphologique,
                        IndexTailleLameRoche = Mathf.Clamp(haut.IndexTaille, 0, 4),
                        NiveauFracture = ligature.IndexBotanique,
                        EstUnEclat = false,
                        DurabiliteOutilMax = dMax,
                        DurabiliteOutilActuelle = dMax
                    };
                }
            }
        }

        // Hachette : uniquement petite roche plate (morph 1, taille mini ou petite).
        static bool EstSlotRochePlateCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 1
            && (s.IndexTaille == 0 || s.IndexTaille == 1);
        static bool EstSlotCordeOuLianeCraft(SlotInventaire s) => !s.EstVide && (s.ID == 20 || s.ID == 16);
        static bool EstSlotCordeIntestinCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 20 && s.IndexChimique == 17 && s.IndexMorphologique == 17 && Joueur.EstVarianteIntestin(s);
        static bool EstSlotCordeIntestinSolideCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 20 && s.IndexChimique == 17 && s.IndexMorphologique == 17 && Joueur.EstVarianteIntestinSolide(s);
        static byte VarianteLigatureCraft(SlotInventaire s)
        {
            if (s.EstVide) return 0;
            if (Joueur.EstVarianteCordeIntestinMixe(s)) return Joueur.TagVarianteCordeIntestinMixe;
            if (s.ID == 16 || Joueur.EstVarianteLiane(s)) return Joueur.TagVarianteLiane;
            if (EstSlotCordeIntestinSolideCraft(s) || Joueur.EstVarianteIntestinSolide(s)) return Joueur.TagVarianteIntestinSolide;
            if (EstSlotCordeIntestinCraft(s) || Joueur.EstVarianteIntestin(s)) return Joueur.TagVarianteIntestin;
            if (EstSlotCordeHerbeSolideCraft(s) || Joueur.EstVarianteHerbeSolide(s)) return Joueur.TagVarianteHerbeSolide;
            return LSystem_Botanique.IndexChene;
        }
        static bool MemeVarianteLigature(SlotInventaire a, SlotInventaire b) =>
            VarianteLigatureCraft(a) == VarianteLigatureCraft(b);
        static bool EstSlotTissuBaseCraft(SlotInventaire s) =>
            EstSlotTissuCraft(s) && !Joueur.EstVarianteLiane(s) && !Joueur.EstVarianteHerbeSolide(s) && !Joueur.EstVarianteIntestin(s) && !Joueur.EstVarianteIntestinSolide(s);
        static bool EstSlotCordeHerbeSolideCraft(SlotInventaire s)
        {
            if (s.EstVide || s.ID != 20) return false;
            // Compatibilité large: accepte l'encodage actuel (15/15 + tier2) et les variantes taguées herbe solide.
            bool encodageSolide = s.IndexChimique == 15 && s.IndexMorphologique == 15 && s.IndexBotanique >= NiveauCordeSolideTier2;
            bool tagSolide = s.IndexBotanique == Joueur.TagVarianteHerbeSolide;
            return encodageSolide || tagSolide;
        }
        static bool EstSlotTissuHerbeSolideCraft(SlotInventaire s)
        {
            if (s.EstVide || s.ID != 21) return false;
            // Compatibilité sauvegardes: tissu tagué herbe solide OU ancien encodage 15/15+tier2.
            if (Joueur.EstVarianteHerbeSolide(s)) return true;
            return s.IndexChimique == 15 && s.IndexMorphologique == 15 && s.IndexBotanique >= NiveauCordeSolideTier2;
        }
        static bool EstSlotTissuIntestinCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 21 && s.IndexChimique == 17 && s.IndexMorphologique == 17 && Joueur.EstVarianteIntestin(s);
        static bool EstSlotTissuIntestinSolideCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 21 && s.IndexChimique == 17 && s.IndexMorphologique == 17 && Joueur.EstVarianteIntestinSolide(s);
        // Outils à durabilité: autorise corde (20) OU liane brute (16) comme ligature.
        static bool EstSlotLigatureOutilCraft(SlotInventaire s) => !s.EstVide && (s.ID == 20 || s.ID == 16);
        /// <summary>Corde 20 avec deux matières flexibles distinctes (ex. 15/16) — bandage/outils oui, tissu non.</summary>
        static bool EstCordeMixteCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 20
            && (Joueur.EstVarianteCordeIntestinMixe(s)
                || (s.IndexChimique != s.IndexMorphologique
                    && ObtenirProfilFlexible(s.IndexChimique, out _)
                    && ObtenirProfilFlexible(s.IndexMorphologique, out _)));
        static bool EstFibreTorsadeCraft(int id) => id is 15 or 16 or 17;
        static bool EstIntestinNettoyeCraft(SlotInventaire s) =>
            Joueur.EstIntestinUtilisablePourCraft(s);
        static int EncoderMatiereCordeMixte(SlotInventaire s) =>
            EstIntestinNettoyeCraft(s) ? Joueur.IdObjetIntestinBoeufNettoye : s.ID;
        static SlotInventaire NormaliserLigatureOutil(SlotInventaire s)
        {
            if (s.ID == 20) return s;
            if (s.ID == 16)
            {
                // Encodage "corde mono-liane" pour le calcul de durabilité des outils.
                return new SlotInventaire
                {
                    ID = 20,
                    IndexChimique = 16,
                    IndexMorphologique = 16,
                    IndexBotanique = 0,
                    EstUnEclat = false,
                    NiveauFracture = 0
                };
            }
            return s;
        }

        // Bandage tier 1 (135) — grille poche 2×2 uniquement :
        // (L)(L) / (L)()
        if (!grilleCraft3x3Table && grille.Length >= 4
            && EstSlotLigatureOutilCraft(grille[0])
            && EstSlotLigatureOutilCraft(grille[1])
            && EstSlotLigatureOutilCraft(grille[2])
            && grille[3].EstVide)
        {
            SlotInventaire ligA = NormaliserLigatureOutil(grille[0]);
            SlotInventaire ligB = NormaliserLigatureOutil(grille[1]);
            SlotInventaire ligC = NormaliserLigatureOutil(grille[2]);
            bool ligaturesMemeVariante = MemeVarianteLigature(ligA, ligB)
                && MemeVarianteLigature(ligA, ligC)
                && Joueur.SontEmpilables(ligA, ligB)
                && Joueur.SontEmpilables(ligA, ligC);
            if (ligaturesMemeVariante)
            {
                byte varianteLig = VarianteLigatureCraft(ligA);
                string genomeBandage = string.Join(";", new[]
                {
                    "BANDAGE135",
                    $"LIGV={varianteLig}",
                    $"LIGC={ligA.IndexChimique}",
                    $"LIGM={ligA.IndexMorphologique}"
                });
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetBandageTier1,
                    IndexBotanique = varianteLig,
                    IndexChimique = ligA.IndexChimique,
                    IndexMorphologique = ligA.IndexMorphologique,
                    GenomeAssemblage = genomeBandage,
                    NiveauFracture = Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[1].NiveauFracture), grille[2].NiveauFracture),
                    EstUnEclat = false
                };
            }
        }

        static bool EstSlotTissuCraft(SlotInventaire s) => !s.EstVide && s.ID == 21;
        static bool EstSlotBatonCraft(SlotInventaire s) => !s.EstVide && s.ID == 32;
        /// <summary>Manche de hachette primitive : bâton brut (32) ou branche (31), même essence <see cref="SlotInventaire.IndexBotanique"/>.</summary>
        static bool EstSlotMancheHachettePrimitive(SlotInventaire s) => !s.EstVide && (s.ID == 32 || s.ID == BlocChutant.ID_BRANCHE);
        // D = demi-bûche standard fendue en 2 (ID 30, morph 1, taille 1) — comme l'analyseur « Rack à bûches ».
        static bool EstSlotBucheQuartB1RackCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;
        // d = demi-bûche courte (taille 2), pleine ou fendue en 2 (morph 0 ou 1).
        static bool EstSlotBucheQuartB2RackCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 30 && s.IndexTaille == 2 && s.IndexMorphologique is 0 or 1;
        static bool EstSlotPochetteTier0Craft(SlotInventaire s) => !s.EstVide && s.ID == Joueur.IdObjetPochetteTier0;

        SlotInventaire c0, c1, c2, c3;
        if (grilleCraft3x3Table)
        {
            c0 = grille[0];
            c1 = grille[1];
            c2 = grille[3];
            c3 = grille[4];
        }
        else
        {
            c0 = grille[0];
            c1 = grille[1];
            c2 = grille[2];
            c3 = grille[3];
        }

        // RECETTE 2×2 : Pit à feu (120) = 4 branches (31) de même essence.
        bool pitFeuCarreBranches =
            !c0.EstVide && c0.ID == BlocChutant.ID_BRANCHE &&
            !c1.EstVide && c1.ID == BlocChutant.ID_BRANCHE &&
            !c2.EstVide && c2.ID == BlocChutant.ID_BRANCHE &&
            !c3.EstVide && c3.ID == BlocChutant.ID_BRANCHE &&
            c1.IndexBotanique == c0.IndexBotanique &&
            c2.IndexBotanique == c0.IndexBotanique &&
            c3.IndexBotanique == c0.IndexBotanique;
        if (pitFeuCarreBranches)
        {
            bool horsCarreVides = !grilleCraft3x3Table
                || (grille.Length >= 9 && grille[2].EstVide && grille[5].EstVide && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide);
            if (horsCarreVides)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetPitFeu,
                    IndexBotanique = c0.IndexBotanique,
                    EstUnEclat = false,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = Mathf.Max(Mathf.Max(c0.NiveauFracture, c1.NiveauFracture), Mathf.Max(c2.NiveauFracture, c3.NiveauFracture))
                };
            }
        }

        // RECETTE 3×3 atelier : Pit à feu roche (122) = pit à feu (120) au centre + 8 roches matière autour.
        if (grilleCraft3x3Table && grille.Length >= 9
            && !grille[4].EstVide && grille[4].ID == Joueur.IdObjetPitFeu)
        {
            bool rochesAutour = true;
            for (int i = 0; i < 9; i++)
            {
                if (i == 4) continue;
                SlotInventaire s = grille[i];
                if (s.EstVide || !ItemPhysique.EstIdRocheMatiere(s.ID))
                {
                    rochesAutour = false;
                    break;
                }
            }
            if (rochesAutour)
            {
                int nf = grille[4].NiveauFracture;
                for (int i = 0; i < 9; i++)
                {
                    if (i == 4) continue;
                    nf = Mathf.Max(nf, grille[i].NiveauFracture);
                }
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetPitFeuRoche,
                    IndexBotanique = grille[4].IndexBotanique,
                    IndexChimique = grille[4].IndexChimique,
                    IndexMorphologique = grille[4].IndexMorphologique,
                    IndexTaille = grille[4].IndexTaille,
                    NiveauFracture = nf,
                    EstUnEclat = false
                };
            }
        }

        if (grilleCraft3x3Table && grille.Length >= 9 && idStationCraft == Joueur.IdObjetTableArtisanaTier1)
        {
            static bool EstDemiBucheStandardFondation(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;
            static bool EstRocheMoyenneFondation(SlotInventaire s) =>
                !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexTaille == 2;
            static int TypeRocheFondation(SlotInventaire s) =>
                ItemPhysique.IndexChimiqueDepuisIdRoche(s.ID);

            static int MaxFractureSlots(SlotInventaire[] g)
            {
                int max = 0;
                for (int i = 0; i < 9; i++)
                    max = Mathf.Max(max, g[i].NiveauFracture);
                return max;
            }

            bool fondationBois = true;
            byte essenceFondationBois = grille[0].IndexBotanique;
            for (int i = 0; i < 9; i++)
            {
                SlotInventaire s = grille[i];
                if (!EstDemiBucheStandardFondation(s) || s.IndexBotanique != essenceFondationBois)
                {
                    fondationBois = false;
                    break;
                }
            }
            if (fondationBois)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetFondationBois,
                    IndexBotanique = essenceFondationBois,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    EstUnEclat = false
                };
            }

            bool fondationRoche = true;
            int chimieRefRoche = TypeRocheFondation(grille[0]);
            for (int i = 0; i < 9; i++)
            {
                SlotInventaire s = grille[i];
                if (!EstRocheMoyenneFondation(s) || TypeRocheFondation(s) != chimieRefRoche)
                {
                    fondationRoche = false;
                    break;
                }
            }
            if (fondationRoche)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetFondationRoche,
                    IndexBotanique = 0,
                    IndexChimique = chimieRefRoche,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    EstUnEclat = false
                };
            }

            bool fondationBoisSoleRoche = true;
            byte essenceMixteA = grille[0].IndexBotanique;
            int chimieMixteA = TypeRocheFondation(grille[6]);
            for (int i = 0; i < 6; i++)
            {
                SlotInventaire s = grille[i];
                if (!EstDemiBucheStandardFondation(s) || s.IndexBotanique != essenceMixteA)
                {
                    fondationBoisSoleRoche = false;
                    break;
                }
            }
            for (int i = 6; i < 9 && fondationBoisSoleRoche; i++)
            {
                SlotInventaire s = grille[i];
                if (!EstRocheMoyenneFondation(s) || TypeRocheFondation(s) != chimieMixteA)
                {
                    fondationBoisSoleRoche = false;
                    break;
                }
            }
            if (fondationBoisSoleRoche)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetFondationBoisSoleRoche,
                    IndexBotanique = essenceMixteA,
                    IndexChimique = chimieMixteA,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    GenomeAssemblage = "FONDMIX:TOPBOIS_SIDEROCH",
                    EstUnEclat = false
                };
            }

            bool fondationRocheSoleBois = true;
            int chimieMixteB = TypeRocheFondation(grille[0]);
            byte essenceMixteB = grille[6].IndexBotanique;
            for (int i = 0; i < 6; i++)
            {
                SlotInventaire s = grille[i];
                if (!EstRocheMoyenneFondation(s) || TypeRocheFondation(s) != chimieMixteB)
                {
                    fondationRocheSoleBois = false;
                    break;
                }
            }
            for (int i = 6; i < 9 && fondationRocheSoleBois; i++)
            {
                SlotInventaire s = grille[i];
                if (!EstDemiBucheStandardFondation(s) || s.IndexBotanique != essenceMixteB)
                {
                    fondationRocheSoleBois = false;
                    break;
                }
            }
            if (fondationRocheSoleBois)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetFondationRocheSoleBois,
                    IndexBotanique = essenceMixteB,
                    IndexChimique = chimieMixteB,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    GenomeAssemblage = "FONDMIX:TOPROCH_SIDEBOIS",
                    EstUnEclat = false
                };
            }

            // Plancher bois : 3 demi-bûches standard (fendues en 2) côte à côte, même essence, ligne du milieu.
            bool solBoisPatron =
                EstDemiBucheStandardFondation(grille[3])
                && EstDemiBucheStandardFondation(grille[4])
                && EstDemiBucheStandardFondation(grille[5])
                && grille[3].IndexBotanique == grille[4].IndexBotanique
                && grille[4].IndexBotanique == grille[5].IndexBotanique;
            if (solBoisPatron)
            {
                bool autresVides = true;
                for (int i = 0; i < 9; i++)
                {
                    if (i == 3 || i == 4 || i == 5) continue;
                    if (!grille[i].EstVide)
                    {
                        autresVides = false;
                        break;
                    }
                }
                if (autresVides)
                {
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetSolBois,
                        IndexBotanique = grille[3].IndexBotanique,
                        IndexChimique = 0,
                        IndexMorphologique = 0,
                        IndexTaille = 0,
                        NiveauFracture = MaxFractureSlots(grille),
                        EstUnEclat = false
                    };
                }
            }

            // Muret bois : 3 bûches standards pleines côte à côte, même essence, ligne du milieu.
            static bool EstBuchePleineStandardMuret(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 1;
            bool muretBoisPatron =
                EstBuchePleineStandardMuret(grille[3])
                && EstBuchePleineStandardMuret(grille[4])
                && EstBuchePleineStandardMuret(grille[5])
                && grille[3].IndexBotanique == grille[4].IndexBotanique
                && grille[4].IndexBotanique == grille[5].IndexBotanique;
            if (muretBoisPatron)
            {
                bool autresVides = true;
                for (int i = 0; i < 9; i++)
                {
                    if (i == 3 || i == 4 || i == 5) continue;
                    if (!grille[i].EstVide)
                    {
                        autresVides = false;
                        break;
                    }
                }
                if (autresVides)
                {
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetMuretBois,
                        IndexBotanique = grille[3].IndexBotanique,
                        IndexChimique = 0,
                        IndexMorphologique = 0,
                        IndexTaille = 0,
                        NiveauFracture = MaxFractureSlots(grille),
                        EstUnEclat = false
                    };
                }
            }

            // Mur bois : 9 bûches standards pleines (3x3), toutes de la même essence.
            bool murBoisPatron = true;
            byte essenceMurBois = 0;
            for (int i = 0; i < 9; i++)
            {
                SlotInventaire s = grille[i];
                bool estBucheStandardPleine = !s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 1;
                if (!estBucheStandardPleine)
                {
                    murBoisPatron = false;
                    break;
                }
                if (i == 0)
                    essenceMurBois = s.IndexBotanique;
                else if (s.IndexBotanique != essenceMurBois)
                {
                    murBoisPatron = false;
                    break;
                }
            }
            if (murBoisPatron)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetMurBois,
                    IndexBotanique = essenceMurBois,
                    IndexChimique = 0,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    EstUnEclat = false
                };
            }

            // Mur bois fenêtré :
            // (B)(B)(B)
            // (B)(F)(B)
            // (B)(B)(B)
            // avec B = bûche standard pleine (toutes de la même essence), F = fenêtre bois.
            static bool EstBuchePleineStandardMurFenetre(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 1;
            bool murFenetrePatron =
                EstBuchePleineStandardMurFenetre(grille[0]) && EstBuchePleineStandardMurFenetre(grille[1]) && EstBuchePleineStandardMurFenetre(grille[2]) &&
                EstBuchePleineStandardMurFenetre(grille[3]) && !grille[4].EstVide && grille[4].ID == Joueur.IdObjetFenetreBois && EstBuchePleineStandardMurFenetre(grille[5]) &&
                EstBuchePleineStandardMurFenetre(grille[6]) && EstBuchePleineStandardMurFenetre(grille[7]) && EstBuchePleineStandardMurFenetre(grille[8]);
            if (murFenetrePatron)
            {
                byte essenceMur = grille[0].IndexBotanique;
                bool buchesMemesEssence =
                    grille[1].IndexBotanique == essenceMur
                    && grille[2].IndexBotanique == essenceMur
                    && grille[3].IndexBotanique == essenceMur
                    && grille[5].IndexBotanique == essenceMur
                    && grille[6].IndexBotanique == essenceMur
                    && grille[7].IndexBotanique == essenceMur
                    && grille[8].IndexBotanique == essenceMur;
                if (buchesMemesEssence)
                {
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetMurBoisFenetre,
                        IndexBotanique = essenceMur,
                        // La fenêtre intégrée garde l'essence de la fenêtre composant craftée.
                        IndexChimique = grille[4].IndexBotanique,
                        IndexMorphologique = 0,
                        IndexTaille = 0,
                        NiveauFracture = MaxFractureSlots(grille),
                        EstUnEclat = false
                    };
                }
            }

            // Mur cadre de porte bois :
            // (B)(B)(B)
            // (B)( )(B)
            // (B)( )(B)
            // avec B = bûche standard pleine, toutes de la même essence.
            static bool EstBuchePleineStandardMurCadre(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 1;
            bool murCadrePortePatron =
                EstBuchePleineStandardMurCadre(grille[0]) && EstBuchePleineStandardMurCadre(grille[1]) && EstBuchePleineStandardMurCadre(grille[2]) &&
                EstBuchePleineStandardMurCadre(grille[3]) && grille[4].EstVide && EstBuchePleineStandardMurCadre(grille[5]) &&
                EstBuchePleineStandardMurCadre(grille[6]) && grille[7].EstVide && EstBuchePleineStandardMurCadre(grille[8]);
            if (murCadrePortePatron)
            {
                byte essenceMurCadre = grille[0].IndexBotanique;
                bool memeEssence =
                    grille[1].IndexBotanique == essenceMurCadre
                    && grille[2].IndexBotanique == essenceMurCadre
                    && grille[3].IndexBotanique == essenceMurCadre
                    && grille[5].IndexBotanique == essenceMurCadre
                    && grille[6].IndexBotanique == essenceMurCadre
                    && grille[8].IndexBotanique == essenceMurCadre;
                if (memeEssence)
                {
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetMurBoisCadrePorte,
                        IndexBotanique = essenceMurCadre,
                        IndexChimique = 0,
                        IndexMorphologique = 0,
                        IndexTaille = 0,
                        NiveauFracture = MaxFractureSlots(grille),
                        EstUnEclat = false
                    };
                }
            }

            // Porte bois :
            // ( )(DB)( )
            // ( )(DB)( )
            // ( )(DB)( )
            // DB = demi-bûche standard fendue en 2 (ID 30, morpho 1, taille 1), même essence.
            bool porteBoisPatron =
                grille[0].EstVide && EstDemiBucheStandardFondation(grille[1]) && grille[2].EstVide
                && grille[3].EstVide && EstDemiBucheStandardFondation(grille[4]) && grille[5].EstVide
                && grille[6].EstVide && EstDemiBucheStandardFondation(grille[7]) && grille[8].EstVide;
            if (porteBoisPatron)
            {
                byte essencePorte = grille[1].IndexBotanique;
                bool memeEssence =
                    grille[4].IndexBotanique == essencePorte
                    && grille[7].IndexBotanique == essencePorte;
                if (memeEssence)
                {
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetPorteBois,
                        IndexBotanique = essencePorte,
                        IndexChimique = 0,
                        IndexMorphologique = 0,
                        IndexTaille = 0,
                        NiveauFracture = MaxFractureSlots(grille),
                        EstUnEclat = false
                    };
                }
            }

            // Toit chaume :
            // ( )(L)( )
            // (L)(Br)(L)
            // ( )( )( )
            // L = ligature (corde/liane), Br = branche brute.
            bool toitChaumePatron =
                grille[0].EstVide && EstSlotCordeOuLianeCraft(grille[1]) && grille[2].EstVide
                && EstSlotCordeOuLianeCraft(grille[3]) && !grille[4].EstVide && grille[4].ID == BlocChutant.ID_BRANCHE && EstSlotCordeOuLianeCraft(grille[5])
                && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            if (toitChaumePatron)
            {
                SlotInventaire ligA = grille[1];
                SlotInventaire ligB = grille[3];
                SlotInventaire ligC = grille[5];
                bool ligaturesIdentiques =
                    MemeVarianteLigature(ligA, ligB)
                    && MemeVarianteLigature(ligA, ligC)
                    && Joueur.SontEmpilables(ligA, ligB)
                    && Joueur.SontEmpilables(ligA, ligC);
                if (ligaturesIdentiques)
                {
                    byte varianteLig = VarianteLigatureCraft(ligA);
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetToitChaume,
                        // La texture du toit suit la variante de ligature utilisée au craft.
                        IndexBotanique = varianteLig,
                        IndexChimique = ligA.IndexChimique,
                        IndexMorphologique = ligA.IndexMorphologique,
                        IndexTaille = 0,
                        NiveauFracture = Mathf.Max(Mathf.Max(ligA.NiveauFracture, ligB.NiveauFracture), Mathf.Max(ligC.NiveauFracture, grille[4].NiveauFracture)),
                        EstUnEclat = false
                    };
                }
            }

            // Fenêtre bois (composant) :
            // (L)(DB)(L)
            // (DB)(B)(DB)
            // (L)(DB)(L)
            bool fenetreBoisPatron =
                EstSlotCordeOuLianeCraft(grille[0]) && EstDemiBucheStandardFondation(grille[1]) && EstSlotCordeOuLianeCraft(grille[2]) &&
                EstDemiBucheStandardFondation(grille[3]) && EstSlotBatonCraft(grille[4]) && EstDemiBucheStandardFondation(grille[5]) &&
                EstSlotCordeOuLianeCraft(grille[6]) && EstDemiBucheStandardFondation(grille[7]) && EstSlotCordeOuLianeCraft(grille[8]);
            if (fenetreBoisPatron)
            {
                bool boisUniformes =
                    grille[1].IndexBotanique == grille[3].IndexBotanique
                    && grille[3].IndexBotanique == grille[5].IndexBotanique
                    && grille[5].IndexBotanique == grille[7].IndexBotanique
                    && grille[7].IndexBotanique == grille[4].IndexBotanique;
                bool ligaturesUniformes =
                    MemeVarianteLigature(grille[0], grille[2])
                    && MemeVarianteLigature(grille[0], grille[6])
                    && MemeVarianteLigature(grille[0], grille[8]);
                if (boisUniformes && ligaturesUniformes)
                {
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetFenetreBois,
                        IndexBotanique = grille[1].IndexBotanique,
                        IndexChimique = grille[0].IndexChimique,
                        IndexMorphologique = grille[0].IndexMorphologique,
                        IndexTaille = 0,
                        NiveauFracture = MaxFractureSlots(grille),
                        EstUnEclat = false
                    };
                }
            }

            // Muret pierre : 3 roches moyennes côte à côte, même type, ligne du milieu.
            static bool EstRocheMoyenneMuret(SlotInventaire s) =>
                !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexTaille == 2;
            static int TypeRocheMuret(SlotInventaire s) =>
                ItemPhysique.IndexChimiqueDepuisIdRoche(s.ID);

            bool muretPierreMilieu =
                EstRocheMoyenneMuret(grille[3])
                && EstRocheMoyenneMuret(grille[4])
                && EstRocheMoyenneMuret(grille[5])
                && TypeRocheMuret(grille[3]) == TypeRocheMuret(grille[4])
                && TypeRocheMuret(grille[4]) == TypeRocheMuret(grille[5])
                && grille[0].EstVide && grille[1].EstVide && grille[2].EstVide
                && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            if (muretPierreMilieu)
            {
                int idxType = TypeRocheMuret(grille[3]);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetMuretPierre,
                    IndexBotanique = 0,
                    IndexChimique = idxType,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    EstUnEclat = false
                };
            }

            // Plancher roche : 3 roches moyennes (taille 2) côte à côte, même type chimique, ligne du haut ou du bas.
            static bool EstRocheMoyenneSol(SlotInventaire s) =>
                !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexTaille == 2;
            static int TypeRocheSol(SlotInventaire s) =>
                ItemPhysique.IndexChimiqueDepuisIdRoche(s.ID);

            bool solRocheHaut =
                EstRocheMoyenneSol(grille[0])
                && EstRocheMoyenneSol(grille[1])
                && EstRocheMoyenneSol(grille[2])
                && TypeRocheSol(grille[0]) == TypeRocheSol(grille[1])
                && TypeRocheSol(grille[1]) == TypeRocheSol(grille[2])
                && grille[3].EstVide && grille[4].EstVide && grille[5].EstVide
                && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            bool solRocheBas =
                EstRocheMoyenneSol(grille[6])
                && EstRocheMoyenneSol(grille[7])
                && EstRocheMoyenneSol(grille[8])
                && TypeRocheSol(grille[6]) == TypeRocheSol(grille[7])
                && TypeRocheSol(grille[7]) == TypeRocheSol(grille[8])
                && grille[0].EstVide && grille[1].EstVide && grille[2].EstVide
                && grille[3].EstVide && grille[4].EstVide && grille[5].EstVide;
            if (solRocheHaut || solRocheBas)
            {
                int idxType = solRocheHaut ? TypeRocheSol(grille[0]) : TypeRocheSol(grille[6]);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetSolRoche,
                    IndexBotanique = 0,
                    IndexChimique = idxType,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    NiveauFracture = MaxFractureSlots(grille),
                    EstUnEclat = false
                };
            }
        }

        // RECETTE ATELIER : 6 cordes (20) → ceinture à poches (102). Formes : 2×3 (colonnes gauche/droite) ou 3×2 (lignes haut/bas).
        if (grilleCraft3x3Table && grille.Length >= 9)
        {
            // RECETTE ATELIER : Rack à bûches (110), patron strict.
            // (D) ( ) (D)   D = demi-bûche standard fendue en 2
            // (D) ( ) (D)
            // (L) (d) (L)   d = demi-bûche courte (même essence)
            bool rackBuchesPatron =
                EstSlotBucheQuartB1RackCraft(grille[0]) && grille[1].EstVide && EstSlotBucheQuartB1RackCraft(grille[2]) &&
                EstSlotBucheQuartB1RackCraft(grille[3]) && grille[4].EstVide && EstSlotBucheQuartB1RackCraft(grille[5]) &&
                EstSlotCordeOuLianeCraft(grille[6]) && EstSlotBucheQuartB2RackCraft(grille[7]) && EstSlotCordeOuLianeCraft(grille[8]);
            if (rackBuchesPatron)
            {
                SlotInventaire bRef = grille[0];
                bool memesB1 = Joueur.SontEmpilables(grille[0], grille[2])
                    && Joueur.SontEmpilables(grille[0], grille[3])
                    && Joueur.SontEmpilables(grille[0], grille[5]);
                bool b2Compatible = grille[7].ID == bRef.ID
                    && grille[7].IndexBotanique == bRef.IndexBotanique
                    && EstSlotBucheQuartB2RackCraft(grille[7]);
                bool ligaturesUniformes = MemeVarianteLigature(grille[6], grille[8]);
                if (memesB1 && b2Compatible && ligaturesUniformes)
                {
                    int nf = Mathf.Max(
                        Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[2].NiveauFracture), Mathf.Max(grille[3].NiveauFracture, grille[5].NiveauFracture)),
                        Mathf.Max(Mathf.Max(grille[6].NiveauFracture, grille[8].NiveauFracture), grille[7].NiveauFracture));
                    byte tagVariante = VarianteLigatureCraft(grille[6]);
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetRackBuches,
                        IndexChimique = grille[6].IndexChimique,
                        IndexMorphologique = grille[6].IndexMorphologique,
                        IndexBotanique = bRef.IndexBotanique,
                        NiveauFracture = nf,
                        GenomeAssemblage = $"RACKBL:{tagVariante}",
                        EstUnEclat = false
                    };
                }
            }

            // RECETTE ATELIER : Rack à bâtons (109), patron strict type « U » ligaturé (comme en jeu) + symétries.
            // Base : ligne haute vide ; milieu L-B-L ; bas B-·-B (corde/liane 20|16, bâtons façonnés 32).
            static void Tourner90HoraireRoles(int[] a)
            {
                int[] b = new int[9];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                        b[c * 3 + (2 - r)] = a[r * 3 + c];
                }
                for (int i = 0; i < 9; i++) a[i] = b[i];
            }

            static void MiroirHorizontalRoles(int[] a)
            {
                for (int r = 0; r < 3; r++)
                {
                    int i0 = r * 3;
                    (a[i0], a[i0 + 2]) = (a[i0 + 2], a[i0]);
                }
            }

            static bool CorrespondPatronRackBatonsRoles(SlotInventaire[] g, int[] roles,
                out SlotInventaire slotRack)
            {
                slotRack = new SlotInventaire();
                SlotInventaire refBaton = default;
                SlotInventaire lig0 = default;
                SlotInventaire lig1 = default;
                int nbL = 0;
                int nfRack = 0;
                for (int i = 0; i < 9; i++)
                {
                    int att = roles[i];
                    SlotInventaire s = g[i];
                    if (att == 0)
                    {
                        if (!s.EstVide)
                            return false;
                        continue;
                    }
                    if (s.EstVide)
                        return false;
                    nfRack = Mathf.Max(nfRack, s.NiveauFracture);
                    if (att == 2)
                    {
                        if (!EstSlotBatonCraft(s))
                            return false;
                        if (refBaton.EstVide)
                            refBaton = s;
                        else if (!Joueur.SontEmpilables(refBaton, s))
                            return false;
                        continue;
                    }
                    if (att == 1)
                    {
                        if (!EstSlotCordeOuLianeCraft(s))
                            return false;
                        if (nbL == 0) lig0 = s;
                        else lig1 = s;
                        nbL++;
                        continue;
                    }
                    return false;
                }
                if (nbL != 2 || refBaton.EstVide)
                    return false;
                if (!MemeVarianteLigature(lig0, lig1))
                    return false;
                bool toutesLigaturesLiane = lig0.ID == 16 && lig1.ID == 16;
                bool toutesLigaturesHerbeSolide = EstSlotCordeHerbeSolideCraft(lig0) && EstSlotCordeHerbeSolideCraft(lig1);
                bool toutesLigaturesIntestinSolide = EstSlotCordeIntestinSolideCraft(lig0) && EstSlotCordeIntestinSolideCraft(lig1);
                bool toutesLigaturesIntestin = EstSlotCordeIntestinCraft(lig0) && EstSlotCordeIntestinCraft(lig1);
                byte tagVariante = toutesLigaturesLiane ? Joueur.TagVarianteLiane
                    : (toutesLigaturesIntestinSolide ? Joueur.TagVarianteIntestinSolide
                    : (toutesLigaturesIntestin ? Joueur.TagVarianteIntestin
                    : (toutesLigaturesHerbeSolide ? Joueur.TagVarianteHerbeSolide : LSystem_Botanique.IndexChene)));
                slotRack = new SlotInventaire
                {
                    ID = Joueur.IdObjetRackBatons,
                    IndexChimique = lig0.IndexChimique,
                    IndexMorphologique = lig0.IndexMorphologique,
                    IndexBotanique = refBaton.IndexBotanique,
                    NiveauFracture = nfRack,
                    GenomeAssemblage = $"RACKL:{tagVariante}",
                    EstUnEclat = false
                };
                return true;
            }

            {
                // 0 = vide, 1 = ligature, 2 = bâton — même forme que la grille atelier (indices 0..8 ligne par ligne).
                int[] baseRack = { 0, 0, 0, 1, 2, 1, 2, 0, 2 };
                var vu = new HashSet<string>();
                var travail = new int[9];
                for (int m = 0; m < 2; m++)
                {
                    for (int i = 0; i < 9; i++) travail[i] = baseRack[i];
                    if (m == 1)
                        MiroirHorizontalRoles(travail);
                    for (int rot = 0; rot < 4; rot++)
                    {
                        string cle = string.Join(",", travail);
                        if (vu.Add(cle) && CorrespondPatronRackBatonsRoles(grille, travail, out SlotInventaire sr))
                            return sr;
                        Tourner90HoraireRoles(travail);
                    }
                }
            }

            bool blocGauche = EstSlotCordeOuLianeCraft(grille[0]) && EstSlotCordeOuLianeCraft(grille[1]) && EstSlotCordeOuLianeCraft(grille[3]) && EstSlotCordeOuLianeCraft(grille[4]) && EstSlotCordeOuLianeCraft(grille[6]) && EstSlotCordeOuLianeCraft(grille[7])
                && grille[2].EstVide && grille[5].EstVide && grille[8].EstVide;
            bool blocDroit = EstSlotCordeOuLianeCraft(grille[1]) && EstSlotCordeOuLianeCraft(grille[2]) && EstSlotCordeOuLianeCraft(grille[4]) && EstSlotCordeOuLianeCraft(grille[5]) && EstSlotCordeOuLianeCraft(grille[7]) && EstSlotCordeOuLianeCraft(grille[8])
                && grille[0].EstVide && grille[3].EstVide && grille[6].EstVide;
            bool blocHaut = EstSlotCordeOuLianeCraft(grille[0]) && EstSlotCordeOuLianeCraft(grille[1]) && EstSlotCordeOuLianeCraft(grille[2]) && EstSlotCordeOuLianeCraft(grille[3]) && EstSlotCordeOuLianeCraft(grille[4]) && EstSlotCordeOuLianeCraft(grille[5])
                && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            bool blocBas = EstSlotCordeOuLianeCraft(grille[3]) && EstSlotCordeOuLianeCraft(grille[4]) && EstSlotCordeOuLianeCraft(grille[5]) && EstSlotCordeOuLianeCraft(grille[6]) && EstSlotCordeOuLianeCraft(grille[7]) && EstSlotCordeOuLianeCraft(grille[8])
                && grille[0].EstVide && grille[1].EstVide && grille[2].EstVide;
            if (blocGauche || blocDroit || blocHaut || blocBas)
            {
                SlotInventaire refC = blocGauche ? grille[0] : blocDroit ? grille[1] : blocHaut ? grille[0] : grille[3];
                byte varianteCeinture = VarianteLigatureCraft(refC);
                bool varianteUniforme = blocGauche
                    ? MemeVarianteLigature(grille[0], grille[1]) && MemeVarianteLigature(grille[0], grille[3]) && MemeVarianteLigature(grille[0], grille[4]) && MemeVarianteLigature(grille[0], grille[6]) && MemeVarianteLigature(grille[0], grille[7])
                    : blocDroit
                    ? MemeVarianteLigature(grille[1], grille[2]) && MemeVarianteLigature(grille[1], grille[4]) && MemeVarianteLigature(grille[1], grille[5]) && MemeVarianteLigature(grille[1], grille[7]) && MemeVarianteLigature(grille[1], grille[8])
                    : blocHaut
                    ? MemeVarianteLigature(grille[0], grille[1]) && MemeVarianteLigature(grille[0], grille[2]) && MemeVarianteLigature(grille[0], grille[3]) && MemeVarianteLigature(grille[0], grille[4]) && MemeVarianteLigature(grille[0], grille[5])
                    : MemeVarianteLigature(grille[3], grille[4]) && MemeVarianteLigature(grille[3], grille[5]) && MemeVarianteLigature(grille[3], grille[6]) && MemeVarianteLigature(grille[3], grille[7]) && MemeVarianteLigature(grille[3], grille[8]);
                if (!varianteUniforme)
                    return new SlotInventaire();
                int nf;
                if (blocGauche)
                    nf = Mathf.Max(Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[1].NiveauFracture), Mathf.Max(grille[3].NiveauFracture, grille[4].NiveauFracture)), Mathf.Max(grille[6].NiveauFracture, grille[7].NiveauFracture));
                else if (blocDroit)
                    nf = Mathf.Max(Mathf.Max(Mathf.Max(grille[1].NiveauFracture, grille[2].NiveauFracture), Mathf.Max(grille[4].NiveauFracture, grille[5].NiveauFracture)), Mathf.Max(grille[7].NiveauFracture, grille[8].NiveauFracture));
                else if (blocHaut)
                    nf = Mathf.Max(Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[1].NiveauFracture), Mathf.Max(grille[2].NiveauFracture, grille[3].NiveauFracture)), Mathf.Max(grille[4].NiveauFracture, grille[5].NiveauFracture));
                else
                    nf = Mathf.Max(Mathf.Max(Mathf.Max(grille[3].NiveauFracture, grille[4].NiveauFracture), Mathf.Max(grille[5].NiveauFracture, grille[6].NiveauFracture)), Mathf.Max(grille[7].NiveauFracture, grille[8].NiveauFracture));
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetCeinturePoches,
                    IndexChimique = refC.IndexChimique,
                    IndexMorphologique = refC.IndexMorphologique,
                    IndexBotanique = varianteCeinture,
                    NiveauFracture = nf,
                    EstUnEclat = false
                };
            }

            // RECETTE ATELIER : Pochette tier 0 (103)
            // Patron strict : [1]=tissu, [4]=corde tressée, [7]=tissu ; tout le reste vide.
            bool pochetteTier0 = EstSlotTissuCraft(grille[1]) && EstSlotCordeOuLianeCraft(grille[4]) && EstSlotTissuCraft(grille[7])
                && grille[0].EstVide && grille[2].EstVide && grille[3].EstVide && grille[5].EstVide && grille[6].EstVide && grille[8].EstVide;
            if (pochetteTier0)
            {
                int nf = Mathf.Max(grille[1].NiveauFracture, Mathf.Max(grille[4].NiveauFracture, grille[7].NiveauFracture));
                byte variante = VarianteLigatureCraft(grille[4]);
                bool okVariante = variante == Joueur.TagVarianteLiane
                    ? Joueur.EstVarianteLiane(grille[1]) && Joueur.EstVarianteLiane(grille[7])
                    : variante == Joueur.TagVarianteIntestinSolide
                    ? EstSlotTissuIntestinSolideCraft(grille[1]) && EstSlotTissuIntestinSolideCraft(grille[7])
                    : variante == Joueur.TagVarianteIntestin
                    ? EstSlotTissuIntestinCraft(grille[1]) && EstSlotTissuIntestinCraft(grille[7])
                    : variante == Joueur.TagVarianteHerbeSolide
                    ? EstSlotTissuHerbeSolideCraft(grille[1]) && EstSlotTissuHerbeSolideCraft(grille[7])
                    : EstSlotTissuBaseCraft(grille[1]) && EstSlotTissuBaseCraft(grille[7]);
                if (!okVariante)
                    return new SlotInventaire();
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetPochetteTier0,
                    IndexChimique = grille[4].IndexChimique,
                    IndexMorphologique = grille[4].IndexMorphologique,
                    IndexBotanique = variante,
                    NiveauFracture = nf,
                    EstUnEclat = false
                };
            }

            // RECETTE ATELIER : Sac tier 0 (101) = ficelle au-dessus de la pochette tier 0.
            // Patron strict :
            // [0]=vide [1]=ficelle [2]=vide
            // [3]=vide [4]=pochette [5]=vide
            // [6]=vide [7]=vide [8]=vide
            bool sacTier0 = grille[0].EstVide && EstSlotCordeOuLianeCraft(grille[1]) && grille[2].EstVide
                && grille[3].EstVide && !grille[4].EstVide && grille[4].ID == Joueur.IdObjetPochetteTier0 && grille[5].EstVide
                && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            if (sacTier0)
            {
                byte varianteCorde = VarianteLigatureCraft(grille[1]);
                byte variantePochette = Joueur.EstVarianteIntestinSolide(grille[4]) ? Joueur.TagVarianteIntestinSolide
                    : (Joueur.EstVarianteIntestin(grille[4]) ? Joueur.TagVarianteIntestin
                    : (Joueur.EstVarianteLiane(grille[4]) ? Joueur.TagVarianteLiane
                    : (Joueur.EstVarianteHerbeSolide(grille[4]) ? Joueur.TagVarianteHerbeSolide : LSystem_Botanique.IndexChene)));
                if (varianteCorde != variantePochette)
                    return new SlotInventaire();
                int nf = Mathf.Max(grille[1].NiveauFracture, grille[4].NiveauFracture);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetSacTier0,
                    IndexChimique = grille[1].IndexChimique,
                    IndexMorphologique = grille[1].IndexMorphologique,
                    IndexBotanique = varianteCorde,
                    NiveauFracture = nf,
                    EstUnEclat = false
                };
            }

            // RECETTE ATELIER : Ceinture à sacoches (104) = 4× pochette tier 0 aux coins + ceinture (102) au centre.
            bool ceintureSacoches =
                EstSlotPochetteTier0Craft(grille[0]) && EstSlotPochetteTier0Craft(grille[2]) && EstSlotPochetteTier0Craft(grille[6]) && EstSlotPochetteTier0Craft(grille[8])
                && grille[1].EstVide && grille[3].EstVide && grille[5].EstVide && grille[7].EstVide
                && !grille[4].EstVide && grille[4].ID == Joueur.IdObjetCeinturePoches;
            if (ceintureSacoches)
            {
                var refB = grille[4];
                byte p0 = Joueur.EstVarianteIntestinSolide(grille[0]) ? Joueur.TagVarianteIntestinSolide : (Joueur.EstVarianteIntestin(grille[0]) ? Joueur.TagVarianteIntestin : (Joueur.EstVarianteHerbeSolide(grille[0]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[0]) ? Joueur.TagVarianteLiane : (byte)0)));
                byte p1 = Joueur.EstVarianteIntestinSolide(grille[2]) ? Joueur.TagVarianteIntestinSolide : (Joueur.EstVarianteIntestin(grille[2]) ? Joueur.TagVarianteIntestin : (Joueur.EstVarianteHerbeSolide(grille[2]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[2]) ? Joueur.TagVarianteLiane : (byte)0)));
                byte p2 = Joueur.EstVarianteIntestinSolide(grille[6]) ? Joueur.TagVarianteIntestinSolide : (Joueur.EstVarianteIntestin(grille[6]) ? Joueur.TagVarianteIntestin : (Joueur.EstVarianteHerbeSolide(grille[6]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[6]) ? Joueur.TagVarianteLiane : (byte)0)));
                byte p3 = Joueur.EstVarianteIntestinSolide(grille[8]) ? Joueur.TagVarianteIntestinSolide : (Joueur.EstVarianteIntestin(grille[8]) ? Joueur.TagVarianteIntestin : (Joueur.EstVarianteHerbeSolide(grille[8]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[8]) ? Joueur.TagVarianteLiane : (byte)0)));
                bool versionLiane = Joueur.EstVarianteLiane(refB)
                    && Joueur.EstVarianteLiane(grille[0]) && Joueur.EstVarianteLiane(grille[2])
                    && Joueur.EstVarianteLiane(grille[6]) && Joueur.EstVarianteLiane(grille[8]);
                bool versionIntestin = Joueur.EstVarianteIntestin(refB)
                    && Joueur.EstVarianteIntestin(grille[0]) && Joueur.EstVarianteIntestin(grille[2])
                    && Joueur.EstVarianteIntestin(grille[6]) && Joueur.EstVarianteIntestin(grille[8]);
                bool versionIntestinSolide = Joueur.EstVarianteIntestinSolide(refB)
                    && Joueur.EstVarianteIntestinSolide(grille[0]) && Joueur.EstVarianteIntestinSolide(grille[2])
                    && Joueur.EstVarianteIntestinSolide(grille[6]) && Joueur.EstVarianteIntestinSolide(grille[8]);
                bool versionHerbeSolide = Joueur.EstVarianteHerbeSolide(refB)
                    && Joueur.EstVarianteHerbeSolide(grille[0]) && Joueur.EstVarianteHerbeSolide(grille[2])
                    && Joueur.EstVarianteHerbeSolide(grille[6]) && Joueur.EstVarianteHerbeSolide(grille[8]);
                int nf = Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[2].NiveauFracture), Mathf.Max(Mathf.Max(grille[6].NiveauFracture, grille[8].NiveauFracture), refB.NiveauFracture));
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetCeintureSacoches,
                    IndexChimique = refB.IndexChimique,
                    IndexMorphologique = refB.IndexMorphologique,
                    IndexBotanique = versionLiane ? Joueur.TagVarianteLiane : (versionIntestinSolide ? Joueur.TagVarianteIntestinSolide : (versionIntestin ? Joueur.TagVarianteIntestin : (versionHerbeSolide ? Joueur.TagVarianteHerbeSolide : LSystem_Botanique.IndexChene))),
                    GenomeAssemblage = Joueur.EncoderConfigPochettesCeinture(p0, p1, p2, p3),
                    NiveauFracture = nf,
                    EstUnEclat = false
                };
            }
        }

        // RECETTE : 4 cordes (20) en carré 2×2 strict — poche (Q) : cases 0–3 ; établi : coin haut-gauche 0,1,3,4 et rien d’autre sur le 3×3 (sinon ceinture / autres recettes).
        bool tissu2x2 = EstSlotCordeOuLianeCraft(c0) && EstSlotCordeOuLianeCraft(c1) && EstSlotCordeOuLianeCraft(c2) && EstSlotCordeOuLianeCraft(c3);
        if (tissu2x2 && grilleCraft3x3Table && grille.Length >= 9)
            tissu2x2 = grille[2].EstVide && grille[5].EstVide && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
        if (tissu2x2)
        {
            if (EstCordeMixteCraft(c0) || EstCordeMixteCraft(c1) || EstCordeMixteCraft(c2) || EstCordeMixteCraft(c3))
                return new SlotInventaire();
            byte varianteTissu = VarianteLigatureCraft(c0);
            bool varianteUniforme = VarianteLigatureCraft(c1) == varianteTissu
                && VarianteLigatureCraft(c2) == varianteTissu
                && VarianteLigatureCraft(c3) == varianteTissu;
            if (!varianteUniforme)
                return new SlotInventaire();
            int nf = Mathf.Max(Mathf.Max(c0.NiveauFracture, c1.NiveauFracture), Mathf.Max(c2.NiveauFracture, c3.NiveauFracture));
            return new SlotInventaire
            {
                ID = 21,
                IndexChimique = c0.IndexChimique,
                IndexMorphologique = c0.IndexMorphologique,
                IndexBotanique = varianteTissu,
                NiveauFracture = nf,
                EstUnEclat = false
            };
        }

        SlotInventaire ConstruireHachette106(SlotInventaire roche, SlotInventaire corde, SlotInventaire baton)
        {
            float dMax = CalculerDurabiliteMaxNouvelleHachette(roche, corde, baton);
            return new SlotInventaire
            {
                ID = 106,
                IndexChimique = roche.ID - ItemPhysique.IdRocheMatiereMin,
                IndexMorphologique = corde.IndexChimique,
                IndexTaille = corde.IndexMorphologique,
                IndexBotanique = baton.IndexBotanique,
                EstUnEclat = false,
                NiveauFracture = corde.IndexBotanique,
                DurabiliteOutilMax = dMax,
                DurabiliteOutilActuelle = dMax
            };
        }

        if (EstSlotRochePlateCraft(c0) && EstSlotLigatureOutilCraft(c1) && c2.EstVide && EstSlotMancheHachettePrimitive(c3))
            return ConstruireHachette106(c0, NormaliserLigatureOutil(c1), c3);
        if (EstSlotLigatureOutilCraft(c0) && EstSlotRochePlateCraft(c1) && EstSlotMancheHachettePrimitive(c2) && c3.EstVide)
            return ConstruireHachette106(c1, NormaliserLigatureOutil(c0), c2);

        // Pelle pierre tier0 (107) : colonne verticale en 3x3 atelier [1]=bâton façonné (toute essence), [4]=ficelle, [7]=petite roche ovale.
        static bool EstSlotBatonFaconneCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 32 && s.IndexChimique == 1;
        /// <summary>Bâton façonné hors tag craft « en T » (morph 4 réservé au composant PB).</summary>
        static bool EstSlotBatonFaconneMorphStandardCraft(SlotInventaire s) =>
            EstSlotBatonFaconneCraft(s) && s.IndexMorphologique != 4;
        static bool EstSlotBatonEnTCraft(SlotInventaire s) =>
            EstSlotBatonFaconneCraft(s) && s.IndexMorphologique == 4;
        static bool EstSlotPetiteRocheOvaleCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 2 && (s.IndexTaille == 0 || s.IndexTaille == 1);
        // Bâton en T (morph 4) : trois bâtons façonnés standard en T sur l’établi 3×3 — cases [1],[3],[4], le reste vide.
        if (grilleCraft3x3Table && grille.Length >= 9
            && EstSlotBatonFaconneMorphStandardCraft(grille[1])
            && EstSlotBatonFaconneMorphStandardCraft(grille[3])
            && EstSlotBatonFaconneMorphStandardCraft(grille[4]))
        {
            bool autresVides = grille[0].EstVide && grille[2].EstVide && grille[5].EstVide && grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            if (autresVides)
            {
                SlotInventaire pivot = grille[4];
                return new SlotInventaire
                {
                    ID = 32,
                    IndexChimique = 1,
                    IndexMorphologique = 4,
                    IndexBotanique = pivot.IndexBotanique,
                    IndexTaille = pivot.IndexTaille,
                    ScaleEclat = pivot.ScaleEclat,
                    EstUnEclat = false,
                    NiveauFracture = 0
                };
            }
        }
        if (grilleCraft3x3Table && grille.Length >= 9
            && EstSlotBatonFaconneCraft(grille[1]) && EstSlotLigatureOutilCraft(grille[4]) && EstSlotPetiteRocheOvaleCraft(grille[7]))
        {
            bool autresVides = grille[0].EstVide && grille[2].EstVide && grille[3].EstVide
                && grille[5].EstVide && grille[6].EstVide && grille[8].EstVide;
            if (autresVides)
            {
                SlotInventaire roche = grille[7];
                SlotInventaire corde = NormaliserLigatureOutil(grille[4]);
                SlotInventaire baton = grille[1];
                float dMax = CalculerDurabiliteMaxNouvellePelle(roche, corde, baton);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetPellePierreTier0,
                    IndexChimique = roche.ID - ItemPhysique.IdRocheMatiereMin,
                    IndexMorphologique = corde.IndexChimique,
                    IndexTaille = corde.IndexMorphologique,
                    IndexBotanique = baton.IndexBotanique,
                    EstUnEclat = false,
                    NiveauFracture = corde.IndexBotanique,
                    DurabiliteOutilMax = dMax,
                    DurabiliteOutilActuelle = dMax
                };
            }
        }

        static bool EstSlotPetiteRochePointeCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 3 && (s.IndexTaille == 0 || s.IndexTaille == 1);
        // Pioche pierre tier0 (108) 3x3:
        // [1]=petite roche en pointe, [2]=ficelle, [3]=petite roche en pointe, [5]=bâton façonné, [8]=bâton façonné.
        if (grilleCraft3x3Table && grille.Length >= 9
            && EstSlotPetiteRochePointeCraft(grille[0]) && EstSlotLigatureOutilCraft(grille[1]) && EstSlotPetiteRochePointeCraft(grille[2])
            && EstSlotBatonFaconneCraft(grille[4]) && EstSlotBatonFaconneCraft(grille[7]))
        {
            bool autresVides = grille[3].EstVide && grille[5].EstVide && grille[6].EstVide && grille[8].EstVide;
            if (autresVides)
            {
                SlotInventaire rocheA = grille[0];
                SlotInventaire rocheB = grille[2];
                SlotInventaire corde = NormaliserLigatureOutil(grille[1]);
                SlotInventaire baton = grille[4];
                float dMax = CalculerDurabiliteMaxNouvellePioche(rocheA, corde, baton);
                int idxRocheA = rocheA.ID - ItemPhysique.IdRocheMatiereMin;
                int idxRocheB = rocheB.ID - ItemPhysique.IdRocheMatiereMin;
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetPiochePierreTier0,
                    // Tête principale.
                    IndexChimique = idxRocheA,
                    // Ligature (conserve le schéma outils existant).
                    IndexMorphologique = corde.IndexChimique,
                    IndexTaille = corde.IndexMorphologique,
                    IndexBotanique = baton.IndexBotanique,
                    EstUnEclat = false,
                    NiveauFracture = corde.IndexBotanique,
                    // Tête secondaire: persistée explicitement pour afficher deux roches différentes.
                    GenomeAssemblage = $"PICKR:{idxRocheB}",
                    DurabiliteOutilMax = dMax,
                    DurabiliteOutilActuelle = dMax
                };
            }
        }

        // Hache en pierre tier 1 (132) 3×3:
        // (R)(R)(B)
        // ( )( )(B)
        // ( )( )(B)
        // R = petite roche plate (même matière), B = bâton (même essence).
        if (grilleCraft3x3Table && grille.Length >= 9
            && EstSlotRochePlateCraft(grille[0]) && EstSlotRochePlateCraft(grille[1]) && EstSlotBatonCraft(grille[2])
            && grille[3].EstVide && grille[4].EstVide && EstSlotBatonCraft(grille[5])
            && grille[6].EstVide && grille[7].EstVide && EstSlotBatonCraft(grille[8]))
        {
            bool memeMatiereRoches = grille[0].ID == grille[1].ID;
            bool memeEssenceBatons = grille[2].IndexBotanique == grille[5].IndexBotanique
                && grille[2].IndexBotanique == grille[8].IndexBotanique;
            if (memeMatiereRoches && memeEssenceBatons)
            {
                int idxRoche = grille[0].ID - ItemPhysique.IdRocheMatiereMin;
                float dMax = CalculerDurabiliteMaxNouvelleHachePierre(grille[0], grille[1], grille[2]);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetHachePierreTier1,
                    IndexChimique = idxRoche,
                    IndexMorphologique = 0,
                    IndexTaille = 0,
                    IndexBotanique = grille[2].IndexBotanique,
                    EstUnEclat = false,
                    NiveauFracture = Mathf.Max(grille[0].NiveauFracture, Mathf.Max(grille[1].NiveauFracture, Mathf.Max(grille[2].NiveauFracture, Mathf.Max(grille[5].NiveauFracture, grille[8].NiveauFracture)))),
                    DurabiliteOutilMax = dMax,
                    DurabiliteOutilActuelle = dMax
                };
            }
        }

        // Atelle de jambe (133) 3×3:
        // (Br)(L)(Br)
        // (Br)(L)(Br)
        // (Br)(L)(Br)
        // Br = branche brute (31) même essence, L = ligature craft (20/16) même variante.
        if (grilleCraft3x3Table && grille.Length >= 9
            && !grille[0].EstVide && grille[0].ID == BlocChutant.ID_BRANCHE
            && EstSlotLigatureOutilCraft(grille[1])
            && !grille[2].EstVide && grille[2].ID == BlocChutant.ID_BRANCHE
            && !grille[3].EstVide && grille[3].ID == BlocChutant.ID_BRANCHE
            && EstSlotLigatureOutilCraft(grille[4])
            && !grille[5].EstVide && grille[5].ID == BlocChutant.ID_BRANCHE
            && !grille[6].EstVide && grille[6].ID == BlocChutant.ID_BRANCHE
            && EstSlotLigatureOutilCraft(grille[7])
            && !grille[8].EstVide && grille[8].ID == BlocChutant.ID_BRANCHE)
        {
            byte essence = grille[0].IndexBotanique;
            bool branchesMemeEssence = grille[2].IndexBotanique == essence
                && grille[3].IndexBotanique == essence
                && grille[5].IndexBotanique == essence
                && grille[6].IndexBotanique == essence
                && grille[8].IndexBotanique == essence;
            SlotInventaire ligA = NormaliserLigatureOutil(grille[1]);
            SlotInventaire ligB = NormaliserLigatureOutil(grille[4]);
            SlotInventaire ligC = NormaliserLigatureOutil(grille[7]);
            bool ligaturesIdentiques = MemeVarianteLigature(ligA, ligB)
                && MemeVarianteLigature(ligA, ligC)
                && Joueur.SontEmpilables(ligA, ligB)
                && Joueur.SontEmpilables(ligA, ligC);
            if (branchesMemeEssence && ligaturesIdentiques)
            {
                byte varianteLig = VarianteLigatureCraft(ligA);
                string genomeAtelle = string.Join(";", new[]
                {
                    "ATELLE133",
                    $"BOIS={essence}",
                    $"LIGV={varianteLig}",
                    $"LIGC={ligA.IndexChimique}",
                    $"LIGM={ligA.IndexMorphologique}"
                });
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetAtelleJambe,
                    IndexBotanique = essence,
                    IndexChimique = ligA.IndexChimique,
                    IndexMorphologique = ligA.IndexMorphologique,
                    GenomeAssemblage = genomeAtelle,
                    NiveauFracture = Mathf.Max(
                        Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[2].NiveauFracture), Mathf.Max(grille[3].NiveauFracture, grille[5].NiveauFracture)),
                        Mathf.Max(Mathf.Max(grille[6].NiveauFracture, grille[8].NiveauFracture), Mathf.Max(ligA.NiveauFracture, Mathf.Max(ligB.NiveauFracture, ligC.NiveauFracture)))
                    ),
                    EstUnEclat = false
                };
            }
        }

        // Atelle de bras (134) 3×3:
        // (Br)(L)(Br)
        // (L)(L)(Br)
        // (Br)(Br)(Br)
        // Br = branche brute (31) même essence, L = ligature craft (20/16) même variante.
        if (grilleCraft3x3Table && grille.Length >= 9
            && !grille[0].EstVide && grille[0].ID == BlocChutant.ID_BRANCHE
            && EstSlotLigatureOutilCraft(grille[1])
            && !grille[2].EstVide && grille[2].ID == BlocChutant.ID_BRANCHE
            && EstSlotLigatureOutilCraft(grille[3])
            && EstSlotLigatureOutilCraft(grille[4])
            && !grille[5].EstVide && grille[5].ID == BlocChutant.ID_BRANCHE
            && !grille[6].EstVide && grille[6].ID == BlocChutant.ID_BRANCHE
            && !grille[7].EstVide && grille[7].ID == BlocChutant.ID_BRANCHE
            && !grille[8].EstVide && grille[8].ID == BlocChutant.ID_BRANCHE)
        {
            byte essence = grille[0].IndexBotanique;
            bool branchesMemeEssence = grille[2].IndexBotanique == essence
                && grille[5].IndexBotanique == essence
                && grille[6].IndexBotanique == essence
                && grille[7].IndexBotanique == essence
                && grille[8].IndexBotanique == essence;
            SlotInventaire ligA = NormaliserLigatureOutil(grille[1]);
            SlotInventaire ligB = NormaliserLigatureOutil(grille[3]);
            SlotInventaire ligC = NormaliserLigatureOutil(grille[4]);
            bool ligaturesIdentiques = MemeVarianteLigature(ligA, ligB)
                && MemeVarianteLigature(ligA, ligC)
                && Joueur.SontEmpilables(ligA, ligB)
                && Joueur.SontEmpilables(ligA, ligC);
            if (branchesMemeEssence && ligaturesIdentiques)
            {
                byte varianteLig = VarianteLigatureCraft(ligA);
                string genomeAtelle = string.Join(";", new[]
                {
                    "ATELLE134",
                    $"BOIS={essence}",
                    $"LIGV={varianteLig}",
                    $"LIGC={ligA.IndexChimique}",
                    $"LIGM={ligA.IndexMorphologique}"
                });
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetAtelleBras,
                    IndexBotanique = essence,
                    IndexChimique = ligA.IndexChimique,
                    IndexMorphologique = ligA.IndexMorphologique,
                    GenomeAssemblage = genomeAtelle,
                    NiveauFracture = Mathf.Max(
                        Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[2].NiveauFracture), Mathf.Max(grille[5].NiveauFracture, grille[6].NiveauFracture)),
                        Mathf.Max(Mathf.Max(grille[7].NiveauFracture, grille[8].NiveauFracture), Mathf.Max(ligA.NiveauFracture, Mathf.Max(ligB.NiveauFracture, ligC.NiveauFracture)))
                    ),
                    EstUnEclat = false
                };
            }
        }

        // Lance pierre tier0 (111) 3x3 (patron + miroir horizontal):
        // Patron: [1]=ligature [2]=petite roche en pointe [4]=bâton façonné [5]=ligature [6]=bâton façonné.
        // Miroir: [0]=petite roche en pointe [1]=ligature [3]=ligature [4]=bâton façonné [8]=bâton façonné.
        if (grilleCraft3x3Table && grille.Length >= 9)
        {
            bool patronA = grille[0].EstVide
                && EstSlotLigatureOutilCraft(grille[1])
                && EstSlotPetiteRochePointeCraft(grille[2])
                && grille[3].EstVide
                && EstSlotBatonFaconneCraft(grille[4])
                && EstSlotLigatureOutilCraft(grille[5])
                && EstSlotBatonFaconneCraft(grille[6])
                && grille[7].EstVide
                && grille[8].EstVide;

            bool patronB = EstSlotPetiteRochePointeCraft(grille[0])
                && EstSlotLigatureOutilCraft(grille[1])
                && grille[2].EstVide
                && EstSlotLigatureOutilCraft(grille[3])
                && EstSlotBatonFaconneCraft(grille[4])
                && grille[5].EstVide
                && grille[6].EstVide
                && grille[7].EstVide
                && EstSlotBatonFaconneCraft(grille[8]);

            if (patronA || patronB)
            {
                SlotInventaire roche = patronA ? grille[2] : grille[0];
                SlotInventaire corde = NormaliserLigatureOutil(grille[1]);
                SlotInventaire baton = grille[4];
                float dMax = CalculerDurabiliteMaxNouvelleLance(roche, corde, baton);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetLancePierreTier0,
                    IndexChimique = roche.ID - ItemPhysique.IdRocheMatiereMin,
                    IndexMorphologique = corde.IndexChimique,
                    IndexTaille = corde.IndexMorphologique,
                    IndexBotanique = baton.IndexBotanique,
                    EstUnEclat = false,
                    NiveauFracture = corde.IndexBotanique,
                    DurabiliteOutilMax = dMax,
                    DurabiliteOutilActuelle = dMax
                };
            }
        }

        // Faux primitive pierre tier0 (112) 3×3 : (X)(R)(X) / (PB)(C)(PB) / (X)(B)(X).
        if (grilleCraft3x3Table && grille.Length >= 9)
        {
            bool patronFaux = grille[0].EstVide
                && EstSlotPetiteRochePointeCraft(grille[1])
                && grille[2].EstVide
                && EstSlotBatonEnTCraft(grille[3])
                && EstSlotLigatureOutilCraft(grille[4])
                && EstSlotBatonEnTCraft(grille[5])
                && grille[6].EstVide
                && EstSlotBatonFaconneMorphStandardCraft(grille[7])
                && grille[8].EstVide;
            if (patronFaux)
            {
                SlotInventaire roche = grille[1];
                SlotInventaire corde = NormaliserLigatureOutil(grille[4]);
                SlotInventaire manche = grille[7];
                SlotInventaire pb1 = grille[3];
                SlotInventaire pb2 = grille[5];
                float dMax = CalculerDurabiliteMaxNouvelleFaux(roche, corde, manche, pb1, pb2);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetFauxPierreTier0,
                    IndexChimique = roche.ID - ItemPhysique.IdRocheMatiereMin,
                    IndexMorphologique = corde.IndexChimique,
                    IndexTaille = corde.IndexMorphologique,
                    IndexBotanique = manche.IndexBotanique,
                    IndexTailleLameRoche = Mathf.Clamp(roche.IndexTaille, 0, 4),
                    EstUnEclat = false,
                    NiveauFracture = corde.IndexBotanique,
                    DurabiliteOutilMax = dMax,
                    DurabiliteOutilActuelle = dMax
                };
            }

            // Coffre en bois (113) 3×3 : (C)(B)(C) / (BL)(C)(BL) / (BL)(BL)(BL) — BL = demi-bûche longueur standard (30 morph 1 taille 1).
            static bool EstDemiRondinLongueurStandardCoffre(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;
            static bool MemeEssenceCinqDemiBuches(SlotInventaire a, SlotInventaire b, SlotInventaire c, SlotInventaire d, SlotInventaire e)
            {
                byte t = a.IndexBotanique;
                return b.IndexBotanique == t && c.IndexBotanique == t && d.IndexBotanique == t && e.IndexBotanique == t;
            }

            bool patronCoffreBois = EstSlotLigatureOutilCraft(grille[0])
                && EstSlotBatonCraft(grille[1])
                && EstSlotLigatureOutilCraft(grille[2])
                && EstDemiRondinLongueurStandardCoffre(grille[3])
                && EstSlotLigatureOutilCraft(grille[4])
                && EstDemiRondinLongueurStandardCoffre(grille[5])
                && EstDemiRondinLongueurStandardCoffre(grille[6])
                && EstDemiRondinLongueurStandardCoffre(grille[7])
                && EstDemiRondinLongueurStandardCoffre(grille[8])
                && MemeVarianteLigature(NormaliserLigatureOutil(grille[0]), NormaliserLigatureOutil(grille[2]))
                && MemeVarianteLigature(NormaliserLigatureOutil(grille[0]), NormaliserLigatureOutil(grille[4]))
                && MemeEssenceCinqDemiBuches(grille[3], grille[5], grille[6], grille[7], grille[8]);
            if (patronCoffreBois)
            {
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetCoffreBoisTier0,
                    IndexBotanique = grille[3].IndexBotanique,
                    IndexChimique = grille[1].IndexChimique,
                    IndexMorphologique = grille[1].IndexMorphologique,
                    IndexTaille = grille[1].IndexTaille,
                    EstUnEclat = false,
                    NiveauFracture = grille[1].NiveauFracture
                };
            }

            // Table d'analyse tier 1 (131) :
            // (C)(C)(MP)
            // (L)(B)(L)
            // (O)( )(O)
            // C = cuir (117), MP = mortier/pilon (130), B = demi-cylindre standard (30 morph 1 taille 1),
            // L = ligature craft, O = os (116), case [7] vide.
            static bool EstDemiCylindreStandardAnalyseT1(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;
            bool patronTableAnalyseT1 =
                !grille[0].EstVide && grille[0].ID == Joueur.IdObjetCuirBoeuf
                && !grille[1].EstVide && grille[1].ID == Joueur.IdObjetCuirBoeuf
                && !grille[2].EstVide && grille[2].ID == Joueur.IdObjetMortierPilonBois
                && EstSlotLigatureOutilCraft(grille[3])
                && EstDemiCylindreStandardAnalyseT1(grille[4])
                && EstSlotLigatureOutilCraft(grille[5])
                && !grille[6].EstVide && grille[6].ID == Joueur.IdObjetOsBoeuf
                && grille[7].EstVide
                && !grille[8].EstVide && grille[8].ID == Joueur.IdObjetOsBoeuf;
            if (patronTableAnalyseT1)
            {
                // Contraintes demandées:
                // - les 2 cuirs doivent être identiques (même peau / même genome)
                // - les 2 liages doivent être identiques (même variante/type)
                if (!Joueur.SontEmpilables(grille[0], grille[1]) || (grille[0].GenomeAssemblage ?? "") != (grille[1].GenomeAssemblage ?? ""))
                    return new SlotInventaire();
                SlotInventaire liageA = NormaliserLigatureOutil(grille[3]);
                SlotInventaire liageB = NormaliserLigatureOutil(grille[5]);
                if (!MemeVarianteLigature(liageA, liageB) || !Joueur.SontEmpilables(liageA, liageB))
                    return new SlotInventaire();

                byte essencePlanche = grille[4].IndexBotanique;
                // Mortier/pilon (slot ID 130): prioriser GenomeAssemblage MORTIERPILON:x,y, sinon fallback sur les indices du slot.
                byte essenceMortier = grille[2].IndexBotanique;
                byte essencePilon = (byte)Mathf.Clamp(grille[2].IndexChimique, 0, 255);
                string genomeMp = grille[2].GenomeAssemblage ?? "";
                if (genomeMp.StartsWith("MORTIERPILON:", StringComparison.Ordinal))
                {
                    string[] mpParts = genomeMp.Substring("MORTIERPILON:".Length).Split(',');
                    if (mpParts.Length >= 2)
                    {
                        if (byte.TryParse(mpParts[0], out byte bMort))
                            essenceMortier = bMort;
                        if (byte.TryParse(mpParts[1], out byte bPil))
                            essencePilon = bPil;
                    }
                }

                // Bois1/Bois2 aléatoires mais distincts, déterministes par combinaison d'ingrédients.
                var rng = new RandomNumberGenerator();
                rng.Seed = unchecked((ulong)(uint)HashCode.Combine(
                    grille[0].GenomeAssemblage ?? "",
                    grille[1].GenomeAssemblage ?? "",
                    grille[2].GenomeAssemblage ?? "",
                    grille[3].IndexBotanique,
                    grille[4].IndexBotanique,
                    grille[5].IndexBotanique,
                    grille[6].ID,
                    grille[8].ID));
                byte bois1 = (byte)rng.RandiRange(0, 4);
                byte bois2 = (byte)rng.RandiRange(0, 4);
                if (bois2 == bois1)
                    bois2 = (byte)((bois1 + 1 + rng.RandiRange(0, 3)) % 5);

                // Roches 1/2/3 aléatoires (textures caillou), persistées dans le genome pour rendu stable.
                int idxRoche1 = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
                int idxRoche2 = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
                int idxRoche3 = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);

                string genomeTableAnalyse = string.Join(";", new[]
                {
                    "TABLEANALYSE131",
                    $"PLAN={essencePlanche}",
                    $"BOIS1={bois1}",
                    $"BOIS2={bois2}",
                    $"LIGV={liageA.IndexBotanique}",
                    $"LIGC={liageA.IndexChimique}",
                    $"LIGM={liageA.IndexMorphologique}",
                    $"MPM={essenceMortier}",
                    $"MPP={essencePilon}",
                    $"CUIR={grille[0].GenomeAssemblage ?? ""}",
                    $"R1={idxRoche1}",
                    $"R2={idxRoche2}",
                    $"R3={idxRoche3}"
                });

                return new SlotInventaire
                {
                    ID = Joueur.IdObjetTableAnalyseTier1,
                    IndexBotanique = essencePlanche,
                    IndexChimique = liageA.IndexChimique,
                    IndexMorphologique = liageA.IndexMorphologique,
                    GenomeAssemblage = genomeTableAnalyse,
                    NiveauFracture = Mathf.Max(grille[0].NiveauFracture, Mathf.Max(grille[1].NiveauFracture, Mathf.Max(grille[2].NiveauFracture, Mathf.Max(grille[3].NiveauFracture, Mathf.Max(grille[4].NiveauFracture, Mathf.Max(grille[5].NiveauFracture, Mathf.Max(grille[6].NiveauFracture, grille[8].NiveauFracture))))))),
                    EstUnEclat = false
                };
            }

            // Table artisanat structures T1 (148) : station dédiée craft structures.
            // (H)( )(P)
            // (R)(R)(DB)
            // ( )(T)( )
            // H = hachette primitive (106), P = pioche pierre tier0 (108),
            // R = petite roche ronde (morph 0, taille 0/1), DB = demi-bûche fendue en 2 standard, T = atelier primitif (200).
            static bool EstPetiteRocheRondeTableArtisana(SlotInventaire s) =>
                !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 0 && (s.IndexTaille == 0 || s.IndexTaille == 1);
            static bool EstDemiBucheStandardTableArtisana(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;

            bool patronTableArtisanaT1 =
                !grille[0].EstVide && grille[0].ID == 106
                && grille[1].EstVide
                && !grille[2].EstVide && grille[2].ID == Joueur.IdObjetPiochePierreTier0
                && EstPetiteRocheRondeTableArtisana(grille[3])
                && EstPetiteRocheRondeTableArtisana(grille[4])
                && EstDemiBucheStandardTableArtisana(grille[5])
                && grille[6].EstVide
                && !grille[7].EstVide && grille[7].ID == 200
                && grille[8].EstVide;
            if (idStationCraft == 200 && patronTableArtisanaT1)
            {
                // Cohérence matériaux : les deux petites roches doivent être de même type.
                int typeRoche = ItemPhysique.IndexChimiqueDepuisIdRoche(grille[3].ID);
                bool rochesMemeType = ItemPhysique.IndexChimiqueDepuisIdRoche(grille[4].ID) == typeRoche;
                if (!rochesMemeType)
                    return new SlotInventaire();

                byte essenceBase = grille[5].IndexBotanique;
                string genomeTableArtisana = string.Join(";", new[]
                {
                    "TABLEARTISANA148",
                    $"H_B={grille[0].IndexBotanique}",
                    $"H_R={grille[0].IndexChimique}",
                    $"H_C={grille[0].IndexMorphologique}",
                    $"H_M={grille[0].IndexTaille}",
                    $"P_B={grille[2].IndexBotanique}",
                    $"P_R={grille[2].IndexChimique}",
                    $"P_C={grille[2].IndexMorphologique}",
                    $"P_M={grille[2].IndexTaille}",
                    $"R_T={typeRoche}",
                    $"T_B={grille[7].IndexBotanique}",
                    $"T_C={grille[7].IndexChimique}",
                    $"T_M={grille[7].IndexMorphologique}",
                    $"DB_B={grille[5].IndexBotanique}"
                });

                return new SlotInventaire
                {
                    ID = Joueur.IdObjetTableArtisanaTier1,
                    IndexBotanique = essenceBase,
                    IndexChimique = grille[7].IndexChimique,
                    IndexMorphologique = grille[7].IndexMorphologique,
                    GenomeAssemblage = genomeTableArtisana,
                    NiveauFracture = Mathf.Max(
                        Mathf.Max(grille[0].NiveauFracture, grille[2].NiveauFracture),
                        Mathf.Max(grille[3].NiveauFracture, Mathf.Max(grille[4].NiveauFracture, Mathf.Max(grille[5].NiveauFracture, grille[7].NiveauFracture)))),
                    EstUnEclat = false
                };
            }
        }

        // Table décorative bois (147) — craft sur atelier primitif (200).
        // (L)(BF)(L)
        // (B)( )(B)
        // ( )( )( )
        if (grilleCraft3x3Table && grille.Length >= 9 && idStationCraft == 200)
        {
            static bool EstDemiBucheStandardTableDeco(SlotInventaire s) =>
                !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;
            bool tableDecoPatron =
                EstSlotCordeOuLianeCraft(grille[0]) && EstDemiBucheStandardTableDeco(grille[1]) && EstSlotCordeOuLianeCraft(grille[2]) &&
                EstSlotBatonCraft(grille[3]) && grille[4].EstVide && EstSlotBatonCraft(grille[5]) &&
                grille[6].EstVide && grille[7].EstVide && grille[8].EstVide;
            if (tableDecoPatron)
            {
                SlotInventaire ligA = grille[0];
                SlotInventaire ligB = grille[2];
                bool ligaturesIdentiques = MemeVarianteLigature(ligA, ligB) && Joueur.SontEmpilables(ligA, ligB);
                bool batonsMemeEssence = grille[3].IndexBotanique == grille[5].IndexBotanique;
                if (ligaturesIdentiques && batonsMemeEssence)
                {
                    byte essenceDemiBuche = grille[1].IndexBotanique;
                    byte essenceBaton = grille[3].IndexBotanique;
                    byte varianteLig = VarianteLigatureCraft(ligA);
                    string genomeTableDeco = string.Join(";", new[]
                    {
                        "TABLEDECO147",
                        $"BF={essenceDemiBuche}",
                        $"BAT={essenceBaton}",
                        $"LIGV={varianteLig}",
                        $"LIGC={ligA.IndexChimique}",
                        $"LIGM={ligA.IndexMorphologique}"
                    });
                    return new SlotInventaire
                    {
                        ID = Joueur.IdObjetTableBoisDecorative,
                        IndexBotanique = essenceDemiBuche,
                        IndexChimique = ligA.IndexChimique,
                        IndexMorphologique = ligA.IndexMorphologique,
                        IndexTaille = 0,
                        GenomeAssemblage = genomeTableDeco,
                        NiveauFracture = Mathf.Max(
                            Mathf.Max(ligA.NiveauFracture, ligB.NiveauFracture),
                            Mathf.Max(grille[1].NiveauFracture, Mathf.Max(grille[3].NiveauFracture, grille[5].NiveauFracture))),
                        EstUnEclat = false
                    };
                }
            }
        }

        if (ingredients.Count == 2 && ingredients[0].ID == 15 && ingredients[1].ID == 15)
        {
            return new SlotInventaire
            {
                ID = 20,
                IndexChimique = 15,
                IndexMorphologique = 15,
                EstUnEclat = false,
                NiveauFracture = 0
            };
        }

        // RECETTE 4 : ATELIER PRIMITIF (ID 200)
        // Bois A : demi-bûche courte fendue en 2 (morph 1, taille 2) en [0] = DBF.
        // Bois B : demi-bûche courte non fendue (morph 0, taille 2) en [2] = DB.
        // Roche : ronde (morph 0), taille mini (0) ou petite (1).
        // Liage : ligature/corde (ID 16 ou 20) en [3] = L.
        // Patron exact (2×2) : (DBF)(R) / (DB)(L), avec les deux demi-bûches de même essence.
        static bool EstDemiBucheCourteAtelierCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 2;
        static bool EstDemiBucheFendueEn2AtelierCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 2;
        static bool EstPetiteRocheRondeCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 0 && (s.IndexTaille == 0 || s.IndexTaille == 1);

        bool paireBoisOk = EstDemiBucheFendueEn2AtelierCraft(c0) && EstDemiBucheCourteAtelierCraft(c2);
        bool memeEssenceBois = paireBoisOk && c0.IndexBotanique == c2.IndexBotanique;
        bool estLiageAtelier = c3.ID == 16 || c3.ID == 20;
        byte essenceAtelier = c2.IndexBotanique;

        if (paireBoisOk && memeEssenceBois && EstPetiteRocheRondeCraft(c1) && estLiageAtelier)
        {
            int idxRocheAtelier = ItemPhysique.IndexChimiqueDepuisIdRoche(c1.ID);
            string genomeAtelier = string.Join(";", new[]
            {
                "ATELIER200",
                $"R={idxRocheAtelier}",
                $"LIGC={c3.IndexChimique}",
                $"LIGM={c3.IndexMorphologique}"
            });
            return new SlotInventaire
            {
                ID = 200,
                IndexBotanique = essenceAtelier,
                IndexChimique = c3.IndexChimique,
                IndexMorphologique = c3.IndexMorphologique,
                GenomeAssemblage = genomeAtelier
            };
        }

        return new SlotInventaire();
    }
}
