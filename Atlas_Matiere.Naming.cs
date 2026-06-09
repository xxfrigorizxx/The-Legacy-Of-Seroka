using Godot;
using System;
using System.Collections.Generic;

public static partial class Atlas_Matiere
{
    private const string PrefixeGenomeVoxelTerrain = "VOXEL_TERRAIN:";

    /// <summary>Sable de quartz (blanc) — même texture que le sable classique, teinte shader.</summary>
    public const int IdVoxelSableQuartz = 49;

    public static bool EstIdVoxelTerrainMinerai(int idVoxel) =>
        (idVoxel >= 10 && idVoxel <= 29) || (idVoxel >= 32 && idVoxel <= 48);

    /// <summary>Voxels de surface (herbe, sable, neige, sable quartz, etc.).</summary>
    public static bool EstIdVoxelSurfaceTerrain(int idVoxel) =>
        (idVoxel >= 1 && idVoxel <= 9) || idVoxel == IdVoxelSableQuartz;

    /// <summary>Posable depuis l'inventaire (comme les voxels terrain 1–9, hors eau).</summary>
    public static bool EstIdVoxelSurfacePosable(int idVoxel) =>
        (idVoxel >= 1 && idVoxel <= 9 && idVoxel != 4) || idVoxel == IdVoxelSableQuartz;

    public static bool EssayerLireIdVoxelTerrain(in SlotInventaire slot, out int idVoxel)
    {
        idVoxel = 0;
        return !slot.EstVide && EstGenomeVoxelTerrainValide(slot.GenomeAssemblage, out idVoxel);
    }

    /// <summary>Valide un tag VOXEL_TERRAIN (minerais, sable de quartz, etc.) pour injection RPC / persistance.</summary>
    public static bool EstGenomeVoxelTerrainValide(string genome, out int idVoxel)
    {
        idVoxel = 0;
        if (string.IsNullOrWhiteSpace(genome)) return false;
        string g = genome.Trim();
        if (!g.StartsWith(PrefixeGenomeVoxelTerrain, StringComparison.OrdinalIgnoreCase))
            return false;
        string brut = g.Substring(PrefixeGenomeVoxelTerrain.Length).Trim();
        if (!int.TryParse(brut, out idVoxel))
            return false;
        return EstIdVoxelSurfaceTerrain(idVoxel) || EstIdVoxelTerrainMinerai(idVoxel);
    }

    public static bool EstGenomeVoxelTerrainValide(string genome) =>
        EstGenomeVoxelTerrainValide(genome, out _);

    /// <summary>Slot inventaire pour un voxel de surface (IDs 1–9 directs ; 49+ via tag pour éviter collision roche matière 40–51).</summary>
    public static SlotInventaire ConstruireSlotInventaireVoxelSurface(int idVoxelTerrain, int quantite = 1)
    {
        quantite = Mathf.Max(1, quantite);
        if (idVoxelTerrain >= 1 && idVoxelTerrain <= 9)
        {
            return new SlotInventaire
            {
                ID = idVoxelTerrain,
                Quantite = quantite,
                IndexMorphologique = 0,
                IndexChimique = 0
            };
        }
        if (EstIdVoxelSurfaceTerrain(idVoxelTerrain))
        {
            return new SlotInventaire
            {
                ID = 2,
                GenomeAssemblage = $"{PrefixeGenomeVoxelTerrain}{idVoxelTerrain}",
                Quantite = quantite
            };
        }
        return new SlotInventaire();
    }

    /// <summary>Voxel argile (ID terrain 8), en inventaire direct ou via tag VOXEL_TERRAIN.</summary>
    public static bool EstSlotVoxelArgile(in SlotInventaire slot)
    {
        if (slot.EstVide) return false;
        if (slot.ID == 8) return true;
        return EssayerLireIdVoxelTerrain(slot, out int idVoxel) && idVoxel == 8;
    }

    /// <summary>Voxel boue (ID terrain 7), en inventaire direct ou via tag VOXEL_TERRAIN.</summary>
    public static bool EstSlotVoxelBoue(in SlotInventaire slot)
    {
        if (slot.EstVide) return false;
        if (slot.ID == 7) return true;
        return EssayerLireIdVoxelTerrain(slot, out int idVoxel) && idVoxel == 7;
    }

    /// <summary>Brin / fibre d'herbe (ID objet 15).</summary>
    public static bool EstSlotBrinHerbe(in SlotInventaire slot) => !slot.EstVide && slot.ID == 15;

    public static string ObtenirNomVoxelTerrain(int idVoxel) => idVoxel switch
    {
        1 => "Voxel terrain: Herbe",
        2 => "Voxel terrain: Roche",
        3 => "Voxel terrain: Sable",
        4 => "Voxel terrain: Eau",
        5 => "Voxel terrain: Neige",
        6 => "Voxel terrain: Terre aride",
        7 => "Voxel terrain: Boue",
        8 => "Voxel terrain: Argile",
        9 => "Voxel terrain: Glace",
        IdVoxelSableQuartz => "Voxel terrain: Sable de quartz",
        10 => "Voxel minerai: Charbon",
        11 => "Voxel minerai: Jade",
        12 => "Voxel minerai: Opale",
        13 => "Voxel minerai: Diamant",
        14 => "Voxel minerai: Topaze",
        15 => "Voxel minerai: Rubis",
        16 => "Voxel minerai: Saphir",
        17 => "Voxel minerai: Émeraude",
        18 => "Voxel minerai: Améthyste",
        19 => "Voxel minerai: Quartz",
        20 => "Voxel minerai: Palladium",
        21 => "Voxel minerai: Platine",
        22 => "Voxel minerai: Argent",
        23 => "Voxel minerai: Or",
        24 => "Voxel minerai: Bismuth",
        25 => "Voxel minerai: Manganèse",
        26 => "Voxel minerai: Titane",
        27 => "Voxel minerai: Tungstène",
        28 => "Voxel minerai: Cobalt",
        29 => "Voxel minerai: Chrome",
        30 => "Voxel terrain: Tronc",
        31 => "Voxel terrain: Feuillage",
        32 => "Voxel minerai: Nickel",
        33 => "Voxel minerai: Aluminium",
        34 => "Voxel minerai: Fer",
        35 => "Voxel minerai: Plomb",
        36 => "Voxel minerai: Zinc",
        37 => "Voxel minerai: Étain",
        38 => "Voxel minerai: Cuivre",
        39 => "Voxel minerai: Soufre",
        40 => "Voxel minerai: Salpêtre",
        41 => "Voxel minerai: Uranium",
        42 => "Voxel minerai: Thorium",
        43 => "Voxel minerai: Plutonium",
        44 => "Voxel minerai: Sel",
        45 => "Voxel minerai: Graphite",
        46 => "Voxel minerai: Calcaire",
        47 => "Voxel minerai: Gypse",
        48 => "Voxel minerai: Obsidienne",
        _ => $"Voxel terrain #{idVoxel}"
    };

    public static string ObtenirNomObjet(SlotInventaire slot)
    {
        if (slot.EstVide)
            return "";
        if (EssayerLireIdVoxelTerrain(slot, out int idVoxelTerrain))
            return ObtenirNomVoxelTerrain(idVoxelTerrain);
        int id = slot.ID;
        if (ItemPhysique.EstIdRocheMatiere(id))
        {
            int indexMatiere = id - ItemPhysique.IdRocheMatiereMin;
            string matiere = "Matière Inconnue";
            if (indexMatiere >= 0 && indexMatiere < ItemPhysique.TableGeologique.Length)
                matiere = ItemPhysique.TableGeologique[indexMatiere].Nom;
            string taille = slot.IndexTaille switch { 0 => "Mini", 1 => "Petite", 2 => "Moyenne", 3 => "Grosse", 4 => "Énorme", _ => "" };
            string forme = slot.IndexMorphologique switch { 0 => "Ronde", 1 => "Plate", 2 => "Ovale", 3 => "en Pointe", _ => "Déformée" };
            return $"{taille} Roche {forme} en {matiere}";
        }
        if (id == 105)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Dague en {ItemPhysique.TableGeologique[i].Nom}";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == 200)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Atelier Primitif en {essence}";
        }
        if (id == Joueur.IdObjetTableBoisDecorative)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Table décorative ({essence})";
        }
        if (id == Joueur.IdObjetTableArtisanaTier1)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Table artisanat structures T1 ({essence})";
        }
        if (id == Joueur.IdObjetTableAnalyseTier1)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Table d'Analyse Tier 1 en {essence}";
        }
        if (id == 106)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Hachette en {ItemPhysique.TableGeologique[i].Nom}";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetHachePierreTier1)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Hache en {ItemPhysique.TableGeologique[i].Nom}";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetAtelleJambe)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Atelle de jambe ({essence})";
        }
        if (id == Joueur.IdObjetAtelleBras)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Atelle de bras ({essence})";
        }
        if (id == Joueur.IdObjetBandageTier1)
        {
            var lig = new SlotInventaire
            {
                ID = Joueur.EstVarianteLiane(slot) ? (byte)16 : (byte)20,
                IndexChimique = slot.IndexChimique,
                IndexMorphologique = slot.IndexMorphologique,
                IndexBotanique = slot.IndexBotanique
            };
            return $"Bandage tier 1 ({ObtenirNomObjet(lig)})";
        }
        if (id == Joueur.IdObjetPellePierreTier0)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Pelle en {ItemPhysique.TableGeologique[i].Nom}";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetPiochePierreTier0)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Pioche en {ItemPhysique.TableGeologique[i].Nom}";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetLancePierreTier0)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Lance en {ItemPhysique.TableGeologique[i].Nom}";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetFauxPierreTier0)
        {
            int i = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nom = $"Faux primitive ({ItemPhysique.TableGeologique[i].Nom})";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetCoffreBoisTier0)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Coffre en bois ({essence})";
        }
        if (id == 100)
        {
            int clef = Joueur.ClefRegistreOutilForge(slot);
            if (clef != 0 && Joueur.RegistreOutilsForges.TryGetValue(clef, out var st) && !string.IsNullOrEmpty(st.Nom))
                return st.Nom;
            return "Outil forgé";
        }
        if (id == Joueur.IdObjetSacDos) return "Sac à dos";
        if (id == Joueur.IdObjetSacTier0)
        {
            if (Joueur.EstVarianteIntestinSolide(slot)) return "Sac tier 0 en intestin solide";
            if (Joueur.EstVarianteIntestin(slot)) return "Sac tier 0 en intestin";
            if (Joueur.EstVarianteHerbeSolide(slot)) return "Sac tier 0 solide (herbe, tier 2)";
            if (Joueur.EstVarianteLiane(slot)) return "Sac tier 0 en liane";
            return "Sac tier 0";
        }
        if (id == Joueur.IdObjetCeinturePoches)
        {
            string prefixe = Joueur.EstVarianteIntestinSolide(slot)
                ? "Ceinture à poches en intestin solide"
                : (Joueur.EstVarianteIntestin(slot)
                ? "Ceinture à poches en intestin"
                : (Joueur.EstVarianteHerbeSolide(slot)
                ? "Ceinture à poches solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Ceinture à poches en liane" : "Ceinture à poches")));
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"{prefixe} ({pa.Nom}+{pb.Nom})";
            if (a)
                return $"{prefixe} ({pa.Nom})";
            if (b)
                return $"{prefixe} ({pb.Nom})";
            return prefixe;
        }
        if (id == Joueur.IdObjetCeintureSacoches)
        {
            string prefixe = Joueur.EstVarianteIntestinSolide(slot)
                ? "Ceinture à sacoches en intestin solide"
                : (Joueur.EstVarianteIntestin(slot)
                ? "Ceinture à sacoches en intestin"
                : (Joueur.EstVarianteHerbeSolide(slot)
                ? "Ceinture à sacoches solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Ceinture à sacoches en liane" : "Ceinture à sacoches")));
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"{prefixe} ({pa.Nom}+{pb.Nom})";
            if (a)
                return $"{prefixe} ({pa.Nom})";
            if (b)
                return $"{prefixe} ({pb.Nom})";
            return prefixe;
        }
        if (id == Joueur.IdObjetPochetteTier0)
        {
            string prefixe = Joueur.EstVarianteIntestinSolide(slot)
                ? "Pochette tier 0 en intestin solide"
                : (Joueur.EstVarianteIntestin(slot)
                ? "Pochette tier 0 en intestin"
                : (Joueur.EstVarianteHerbeSolide(slot)
                ? "Pochette tier 0 solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Pochette tier 0 en liane" : "Pochette tier 0")));
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"{prefixe} ({pa.Nom}+{pb.Nom})";
            if (a)
                return $"{prefixe} ({pa.Nom})";
            if (b)
                return $"{prefixe} ({pb.Nom})";
            return prefixe;
        }
        if (ObtenirProfilFlexible(id, out var flex))
            return flex.Nom;
        if (id == 30)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            string longueur = slot.IndexTaille switch { 0 => "Tronc Brut", 1 => "Bûche Standard", 2 => "Demi-Bûche Courte", 3 => "Rondin", _ => "Morceau" };
            string fente = slot.IndexMorphologique switch { 0 => "", 1 => " (Fendue en 2)", 2 => " (Fendue en 4)", 3 => " (Fendue en 8)", _ => " (Éclat)" };
            return $"{longueur}{fente} de {essence}";
        }
        // Même ID pour branches tombées (arbre, cadavre, buisson) : l’essence vient d’IndexBotanique / meta sur BlocChutant.
        if (id == BlocChutant.ID_BRANCHE)
        {
            string essence = slot.IndexBotanique switch
            {
                0 => "Chêne",
                1 => "Bouleau",
                2 => "Pin",
                3 => "Sapin",
                4 => "Fromager",
                5 => "Chêne mort",
                6 => "Bouleau mort",
                _ => "bois"
            };
            return $"Branche brute ({essence})";
        }
        if (id == 32)
        {
            if (slot.IndexChimique == 1 && slot.IndexMorphologique == 4)
            {
                string essenceEnT = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
                return $"Bâton façonné en T ({essenceEnT})";
            }
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            string longueur = slot.IndexTaille switch { 0 => "Brin brut", 1 => "Bâton standard", 2 => "Demi-bâton", 3 => "Rondin fin", _ => "Morceau" };
            string fente = slot.IndexMorphologique switch { 0 => "", 1 => " (Fendu en 2)", 2 => " (Fendu en 4)", 3 => " (Planchette)", _ => "" };
            float zL = slot.ScaleEclat.Z;
            string partLong = "";
            if (zL > 1e-4f && zL < 0.29f)
                partLong = "Quart de bâton · ";
            else if (zL > 1e-4f && zL < 0.53f)
                partLong = "Demi-bâton · ";
            if (slot.IndexChimique == 1 && slot.IndexBotanique == LSystem_Botanique.IndexChene)
                return $"{partLong}Bâton de chêne · {longueur.ToLowerInvariant()}{fente}";
            return $"{partLong}{longueur}{fente} de {essence}";
        }
        if (id == 20)
        {
            if (Joueur.EstVarianteCordeIntestinMixe(slot))
            {
                static string NomMatiereEncordeeMixte(int code) =>
                    code == Joueur.IdObjetIntestinBoeufNettoye
                        ? "Intestin"
                        : (ObtenirProfilFlexible(code, out var px) ? px.Nom : "Matière");
                return $"Corde {NomMatiereEncordeeMixte(slot.IndexChimique)}+{NomMatiereEncordeeMixte(slot.IndexMorphologique)}";
            }
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            bool varianteIntestin = Joueur.EstVarianteIntestin(slot);
            bool varianteIntestinSolide = Joueur.EstVarianteIntestinSolide(slot);
            if (varianteIntestinSolide)
                return "Corde d'intestin solide";
            if (varianteIntestin)
                return "Corde d'intestin";
            bool solideTier2 = slot.IndexBotanique >= NiveauCordeSolideTier2;
            if (a && b)
            {
                if (slot.IndexChimique == 15 && slot.IndexMorphologique == 15)
                    return solideTier2 ? "Corde d'herbe solide (tier 2)" : "Corde d'herbe";
                return solideTier2 ? $"Corde solide (tier 2) {pa.Nom}+{pb.Nom}" : $"Corde {pa.Nom}+{pb.Nom}";
            }
            if (a)
                return solideTier2 ? $"Corde solide (tier 2) {pa.Nom}" : $"Corde {pa.Nom}";
            if (b)
                return solideTier2 ? $"Corde solide (tier 2) {pb.Nom}" : $"Corde {pb.Nom}";
            return solideTier2 ? "Corde solide (tier 2)" : "Corde";
        }
        if (id == 21)
        {
            string prefixe = Joueur.EstVarianteIntestinSolide(slot)
                ? "Tissu en intestin solide"
                : (Joueur.EstVarianteIntestin(slot)
                ? "Tissu en intestin"
                : (Joueur.EstVarianteHerbeSolide(slot)
                ? "Tissu solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Tissu en liane (tier 0)" : "Tissu (tier 0)")));
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"{prefixe} {pa.Nom}+{pb.Nom}";
            if (a)
                return $"{prefixe} {pa.Nom}";
            if (b)
                return $"{prefixe} {pb.Nom}";
            return prefixe;
        }
        if (id == Joueur.IdObjetBaie)
        {
            string couleur = Joueur.ObtenirLexemeCouleurBaiePourNomInventaire(slot.IndexChimique);
            int q = Joueur.ObtenirQuantiteSlot(slot);
            return q > 1 ? $"Petites baies {couleur}s x{q}" : $"Petite baie {couleur}";
        }
        if (id == Joueur.IdObjetAloeVera)
            return "Aloe vera";
        if (id == Joueur.IdObjetRackBatons)
            return "Rack à bâtons";
        if (id == Joueur.IdObjetRackBuches)
            return "Rack à bûches";
        if (id == Joueur.IdObjetPitFeu)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Pit à feu ({essence})";
        }
        if (id == Joueur.IdObjetPitFeuRoche)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Pit à feu roche ({essence})";
        }
        if (id == Joueur.IdObjetFondationBois)
            return "Fondation bois";
        if (id == Joueur.IdObjetFondationRoche)
            return "Fondation roche";
        if (id == Joueur.IdObjetFondationBoisSoleRoche)
            return "Fondation bois sole roche";
        if (id == Joueur.IdObjetFondationRocheSoleBois)
            return "Fondation roche sole bois";
        if (id == Joueur.IdObjetSolBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Plancher bois ({essence})";
        }
        if (id == Joueur.IdObjetSolRoche)
        {
            int chim = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nomRoche = ItemPhysique.TableGeologique[chim].Nom;
            return $"Plancher roche ({nomRoche})";
        }
        if (id == Joueur.IdObjetMuretBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Muret bois ({essence})";
        }
        if (id == Joueur.IdObjetMuretPierre)
        {
            int chim = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
            string nomRoche = ItemPhysique.TableGeologique[chim].Nom;
            return $"Muret roche ({nomRoche})";
        }
        if (id == Joueur.IdObjetMurBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Mur bois ({essence})";
        }
        if (id == Joueur.IdObjetMurBoisFenetre)
        {
            string EssenceBois(byte e) => e switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            byte essenceMur = slot.IndexBotanique;
            byte essenceFenetre = (byte)Mathf.Clamp(slot.IndexChimique, 0, 4);
            return $"Mur fenêtré ({EssenceBois(essenceMur)} / {EssenceBois(essenceFenetre)})";
        }
        if (id == Joueur.IdObjetMurBoisCadrePorte)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Mur cadre de porte ({essence})";
        }
        if (id == Joueur.IdObjetPorteBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Porte bois ({essence})";
        }
        if (id == Joueur.IdObjetToitChaume)
        {
            string ligature = slot.IndexBotanique switch
            {
                Joueur.TagVarianteLiane => "liane",
                Joueur.TagVarianteHerbeSolide => "herbe solide",
                Joueur.TagVarianteIntestin => "intestin",
                Joueur.TagVarianteIntestinSolide => "intestin solide",
                _ => "liane"
            };
            return $"Toit chaume ({ligature})";
        }
        if (id == Joueur.IdObjetTorche)
        {
            string ligature = slot.IndexBotanique switch
            {
                Joueur.TagVarianteLiane => "liane",
                Joueur.TagVarianteHerbeSolide => "herbe solide",
                Joueur.TagVarianteIntestin => "intestin",
                Joueur.TagVarianteIntestinSolide => "intestin solide",
                _ => "tissu"
            };
            return $"Torche ({ligature})";
        }
        if (id == Joueur.IdObjetFenetreBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Fenêtre bois ({essence})";
        }
        if (id == Joueur.IdObjetMailletBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Maillet en {essence}";
        }
        if (id == Joueur.IdObjetBolBois)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Bol en {essence}";
        }
        if (id == Joueur.IdObjetBolEau)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Bol d'eau ({essence})";
        }
        if (id == Joueur.IdObjetArgileHumidifiee)
            return "Argile humidifiée";
        if (id == Joueur.IdObjetBolArgile)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            return q > 1 ? $"Bol en argile x{q}" : "Bol en argile";
        }
        if (id == Joueur.IdObjetBolCeramique)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            string etat = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(slot) > 0.04f ? " (chaud)" : "";
            return q > 1 ? $"Bol en céramique x{q}{etat}" : $"Bol en céramique{etat}";
        }
        if (id == Joueur.IdObjetMouleArgile)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            return q > 1 ? $"Moule en argile x{q}" : "Moule en argile";
        }
        if (id == Joueur.IdObjetMouleCeramique)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            string etat = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(slot) > 0.04f ? " (chaud)" : "";
            return q > 1 ? $"Moule en céramique x{q}{etat}" : $"Moule en céramique{etat}";
        }
        if (id == Joueur.IdObjetChamotte)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            return q > 1 ? $"Chamotte x{q}" : "Chamotte";
        }
        if (id == Joueur.IdObjetTorchie)
            return "Torchie";
        if (id == Joueur.IdObjetFourTorchie)
            return "Four en Torchie";
        if (id == Joueur.IdObjetPinceOs)
            return "Pince en os";
        if (id == Joueur.IdObjetMortierPilonBois)
        {
            string EssenceBois(byte idx) => idx switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            byte essenceBol = slot.IndexBotanique;
            byte essencePilon = (byte)Mathf.Clamp(slot.IndexChimique, 0, 255);
            string g = slot.GenomeAssemblage ?? "";
            if (g.StartsWith("MORTIERPILON:", StringComparison.Ordinal))
            {
                string[] morceaux = g.Substring("MORTIERPILON:".Length).Split(',');
                if (morceaux.Length >= 2)
                {
                    if (byte.TryParse(morceaux[0], out byte b))
                        essenceBol = b;
                    if (byte.TryParse(morceaux[1], out byte p))
                        essencePilon = p;
                }
            }
            string nomBol = EssenceBois(essenceBol);
            string nomPilon = EssenceBois(essencePilon);
            return nomBol == nomPilon
                ? $"Mortier avec pilon ({nomBol})"
                : $"Mortier {nomBol} + pilon {nomPilon}";
        }
        if (id == Joueur.IdObjetAllumeFeu)
        {
            string pierre = slot.IndexChimique switch { 10 => "Marcassite", 11 => "Pyrite", _ => "Sulfure" };
            string nom = $"Allume-feu ({pierre})";
            if (slot.DurabiliteOutilMax > 0.5f)
            {
                int a = Mathf.Max(0, Mathf.RoundToInt(slot.DurabiliteOutilActuelle));
                int m = Mathf.Max(1, Mathf.RoundToInt(slot.DurabiliteOutilMax));
                return $"{nom} ({a}/{m})";
            }
            return nom;
        }
        if (id == Joueur.IdObjetCarnetSavoir)
            return "Carnet du savoir";
        if (id == Joueur.IdObjetSteakCru || id == Joueur.IdObjetSteakCuit || id == Joueur.IdObjetOsBoeuf || id == Joueur.IdObjetCuirBoeuf || id == Joueur.IdObjetIntestinBoeuf || id == Joueur.IdObjetIntestinBoeufNettoye)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            string nom = id == Joueur.IdObjetSteakCru ? "Steak cru"
                : (id == Joueur.IdObjetSteakCuit
                    ? (FourTorchieThermodynamique.EstSteakBrule(slot) ? "Steak cuit (brûlé)" : "Steak cuit")
                : (id == Joueur.IdObjetOsBoeuf ? "Os"
                : (id == Joueur.IdObjetCuirBoeuf ? "Cuir"
                : (id == Joueur.IdObjetIntestinBoeufNettoye ? "Intestin propre" : "Intestin"))));
            return q > 1 ? $"{nom} x{q}" : nom;
        }
        if (Joueur.EstIdCharbonRecolte(id))
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            string nom = id switch
            {
                Joueur.IdObjetCharbonBasseQualite => "Charbon (basse qualité)",
                Joueur.IdObjetCharbonMoyenneQualite => "Charbon (qualité moyenne)",
                Joueur.IdObjetCharbonBonneQualite => "Charbon (bonne qualité)",
                Joueur.IdObjetCharbonAntracite => "Charbon anthracite",
                _ => "Charbon"
            };
            return q > 1 ? $"{nom} x{q}" : nom;
        }
        if (EstIdVoxelSurfaceTerrain(id))
            return ObtenirNomCourtVoxelTerrain(id);
        return id switch
        {
            10 => "Buisson plein",
            11 => "Buisson vide",
            34 => "Feuillage",
            999 => "Végétation",
            _ => $"Objet #{id}"
        };
    }

    /// <summary>Nom inventaire / HUD pour un voxel terrain de base (IDs 1–9, aligné sur les textures terrain).</summary>
    public static string ObtenirNomCourtVoxelTerrain(int idVoxel) => idVoxel switch
    {
        1 => "Herbe",
        2 => "Roche",
        3 => "Sable",
        4 => "Eau",
        5 => "Neige",
        6 => "Terre aride",
        7 => "Boue",
        8 => "Argile",
        9 => "Glace",
        IdVoxelSableQuartz => "Sable de quartz",
        _ => $"Voxel #{idVoxel}"
    };
}
