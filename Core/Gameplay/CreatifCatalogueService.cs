using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>Construction du catalogue inventaire créatif/admin (variantes + IDs objets manquants).</summary>
public static class CreatifCatalogueService
{
    public const int VersionCatalogue = 18;

    public enum CategorieCreatif
    {
        TousVariants,
        Tous,
        Structures,
        Bois,
        Pierre,
        Outils,
        Consommables,
        Admin
    }

    public struct EntreeCatalogueCreatif
    {
        public SlotInventaire Slot;
        public string Nom;
        public string Suffixe;
        public CategorieCreatif Categorie;
    }

    private static readonly byte[] EssencesBois = { 0, 1, 2, 3, 4 };

    private static readonly int[] IdsVariantesEssenceBois =
    {
        200,
        Joueur.IdObjetTableBoisDecorative,
        Joueur.IdObjetTableArtisanaTier1,
        Joueur.IdObjetTableAnalyseTier1,
        Joueur.IdObjetPitFeu,
        Joueur.IdObjetPitFeuRoche,
        Joueur.IdObjetCoffreBoisTier0,
        Joueur.IdObjetRackBatons,
        Joueur.IdObjetRackBuches,
        Joueur.IdObjetFondationBois,
        Joueur.IdObjetFondationBoisSoleRoche,
        Joueur.IdObjetFondationRocheSoleBois,
        Joueur.IdObjetMailletBois,
        Joueur.IdObjetBolBois,
        Joueur.IdObjetMortierPilonBois,
        Joueur.IdObjetSolBois,
        Joueur.IdObjetSolRoche,
        Joueur.IdObjetMuretBois,
        Joueur.IdObjetMurBois,
        Joueur.IdObjetMurBoisCadrePorte,
        Joueur.IdObjetPorteBois,
        Joueur.IdObjetTorche,
        Joueur.IdObjetFenetreBois,
        Joueur.IdObjetBandageTier1
    };

    /// <summary>Ajoute les entrées pour tout <c>IdObjet*</c> public de <see cref="Joueur"/> absent du catalogue.</summary>
    public static void CompleterEntreesDepuisIdsObjetsJoueur(
        IReadOnlyCollection<EntreeCatalogueCreatif> catalogueExistant,
        HashSet<string> signatures,
        Action<SlotInventaire, CategorieCreatif, string> ajouter)
    {
        var idsPresents = new HashSet<int>();
        foreach (EntreeCatalogueCreatif e in catalogueExistant)
            idsPresents.Add(e.Slot.ID);

        foreach (FieldInfo field in typeof(Joueur).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(int) || !field.Name.StartsWith("IdObjet", StringComparison.Ordinal))
                continue;
            if (field.Name == nameof(Joueur.IdObjetSacDos))
                continue;

            int id = (int)field.GetValue(null)!;
            if (id <= 0 || idsPresents.Contains(id))
                continue;

            if (id == Joueur.IdObjetMurBoisFenetre)
            {
                foreach (byte essenceMur in EssencesBois)
                {
                    foreach (byte essenceFenetre in EssencesBois)
                    {
                        ajouter(
                            new SlotInventaire { ID = id, IndexBotanique = essenceMur, IndexChimique = essenceFenetre, Quantite = 1 },
                            CategorieCreatif.Structures,
                            $"Mur {NomEssence(essenceMur)} / Fenêtre {NomEssence(essenceFenetre)}");
                    }
                }
                continue;
            }

            if (AccepteVariantesEssenceBois(id))
            {
                foreach (byte essence in EssencesBois)
                {
                    ajouter(
                        new SlotInventaire { ID = id, IndexBotanique = essence, Quantite = 1 },
                        CategorieCreatif.Structures,
                        $"Essence: {NomEssence(essence)}");
                }
                continue;
            }

            if (id == Joueur.IdObjetFondationRoche)
            {
                for (int chim = 0; chim < ItemPhysique.TableGeologique.Length; chim++)
                {
                    ajouter(
                        new SlotInventaire { ID = id, IndexChimique = chim, Quantite = 1 },
                        CategorieCreatif.Structures,
                        ItemPhysique.TableGeologique[chim].Nom);
                }
                continue;
            }

            if (id == Joueur.IdObjetMuretPierre)
            {
                for (int chim = 0; chim < ItemPhysique.TableGeologique.Length; chim++)
                {
                    ajouter(
                        new SlotInventaire { ID = id, IndexChimique = chim, Quantite = 1 },
                        CategorieCreatif.Structures,
                        ItemPhysique.TableGeologique[chim].Nom);
                }
                continue;
            }

            ajouter(new SlotInventaire { ID = id, Quantite = 1 }, InfererCategorie(id), "Catalogue auto");
        }
    }

    private static bool AccepteVariantesEssenceBois(int id)
    {
        for (int i = 0; i < IdsVariantesEssenceBois.Length; i++)
        {
            if (IdsVariantesEssenceBois[i] == id)
                return true;
        }
        return false;
    }

    private static CategorieCreatif InfererCategorie(int id)
    {
        if (id >= 1 && id <= 9)
            return CategorieCreatif.Consommables;
        if (ItemPhysique.EstIdRocheMatiere(id))
            return CategorieCreatif.Pierre;
        if (id == 30 || id == 32 || id == BlocChutant.ID_BRANCHE || id == BlocChutant.ID_FEUILLE_ARRACHEE)
            return CategorieCreatif.Bois;
        if (id == 200 || id == Joueur.IdObjetTableBoisDecorative || id == Joueur.IdObjetTableArtisanaTier1 || id == Joueur.IdObjetTableAnalyseTier1 || id == Joueur.IdObjetRackBatons
            || id == Joueur.IdObjetRackBuches || id == Joueur.IdObjetCoffreBoisTier0
            || id == Joueur.IdObjetPitFeu || id == Joueur.IdObjetPitFeuRoche
            || id == Joueur.IdObjetFondationBois || id == Joueur.IdObjetFondationRoche
            || id == Joueur.IdObjetFondationBoisSoleRoche || id == Joueur.IdObjetFondationRocheSoleBois
            || id == Joueur.IdObjetSolBois || id == Joueur.IdObjetSolRoche || id == Joueur.IdObjetMuretBois || id == Joueur.IdObjetMuretPierre || id == Joueur.IdObjetMurBois || id == Joueur.IdObjetMurBoisFenetre || id == Joueur.IdObjetMurBoisCadrePorte || id == Joueur.IdObjetPorteBois || id == Joueur.IdObjetToitChaume || id == Joueur.IdObjetMailletBois
            || id == Joueur.IdObjetTorche || id == Joueur.IdObjetFenetreBois
            || id == Joueur.IdObjetBolBois || id == Joueur.IdObjetMortierPilonBois)
            return CategorieCreatif.Structures;
        if (id == 105 || id == 106 || id == Joueur.IdObjetHachePierreTier1
            || id == Joueur.IdObjetPellePierreTier0 || id == Joueur.IdObjetPiochePierreTier0
            || id == Joueur.IdObjetLancePierreTier0 || id == Joueur.IdObjetFauxPierreTier0)
            return CategorieCreatif.Outils;
        if (id == Joueur.IdObjetBaie || id == Joueur.IdObjetSteakCru || id == Joueur.IdObjetSteakCuit
            || id == Joueur.IdObjetAtelleJambe || id == Joueur.IdObjetAtelleBras || id == Joueur.IdObjetBandageTier1)
            return CategorieCreatif.Consommables;
        return CategorieCreatif.Admin;
    }

    private static string NomEssence(byte e) => e switch
    {
        0 => "Chêne",
        1 => "Bouleau",
        2 => "Pin",
        3 => "Sapin",
        4 => "Fromager",
        _ => "Bois"
    };
}
