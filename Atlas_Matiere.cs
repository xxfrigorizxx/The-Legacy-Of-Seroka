using Godot;
using System;
using System.Collections.Generic;

/// <summary>Propriétés d'une matière flexible (herbe, liane, boyau…). Référence data pour cordes et UI.</summary>
public struct ProfilMatiereFlexible
{
    public string Nom;
    public Color CouleurCorde;
    public float Durabilite;
    public float TensionMax;
    public float Flexibilite;
    public bool Fragile;
    public bool Etirable;
}

/// <summary>Atlas statique : noms, flexibles, cordes, durabilité outils, matrice craft 2×2.</summary>
public static class Atlas_Matiere
{
    private const float PerteFlexParMix = 0.38f;

    public static readonly ProfilMatiereFlexible[] TableMatiereFlexible =
    {
        new ProfilMatiereFlexible { Nom = "Herbe", CouleurCorde = new Color(0.35f, 0.52f, 0.18f), Durabilite = 4f, TensionMax = 3f, Flexibilite = 1f, Fragile = true, Etirable = false },
        new ProfilMatiereFlexible { Nom = "Liane", CouleurCorde = new Color(0.4f, 0.38f, 0.22f), Durabilite = 10f, TensionMax = 8f, Flexibilite = 0.7f, Fragile = false, Etirable = false },
        new ProfilMatiereFlexible { Nom = "Boyau", CouleurCorde = new Color(0.6f, 0.45f, 0.35f), Durabilite = 14f, TensionMax = 14f, Flexibilite = 0.5f, Fragile = false, Etirable = true }
    };

    public static int IdFlexibleToIndex(int id)
    {
        if (id == 15) return 0;
        if (id == 16) return 1;
        if (id == 17) return 2;
        return -1;
    }

    public static bool ObtenirProfilFlexible(int id, out ProfilMatiereFlexible p)
    {
        int i = IdFlexibleToIndex(id);
        if (i < 0 || i >= TableMatiereFlexible.Length) { p = default; return false; }
        p = TableMatiereFlexible[i];
        return true;
    }

    public static float ObtenirFlexibiliteEffective(SlotInventaire slot)
    {
        if (slot.ID == 20 || slot.ID == 21)
        {
            float fa = ObtenirProfilFlexible(slot.IndexChimique, out var pa) ? pa.Flexibilite : 0.5f;
            float fb = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb) ? pb.Flexibilite : 0.5f;
            float baseFlex = (fa + fb) * 0.5f;
            return baseFlex * Mathf.Max(0f, 1f - slot.NiveauFracture * PerteFlexParMix);
        }
        return ObtenirProfilFlexible(slot.ID, out var p) ? p.Flexibilite : 0f;
    }

    public static Color ObtenirTeinteCordeTressage(int idMatiereA, int idMatiereB, int niveauTressage = 0)
    {
        bool okA = ObtenirProfilFlexible(idMatiereA, out var pa);
        bool okB = ObtenirProfilFlexible(idMatiereB, out var pb);
        Color c;
        if (!okA && !okB) c = new Color(0.52f, 0.42f, 0.28f);
        else if (!okA) c = pb.CouleurCorde;
        else if (!okB) c = pa.CouleurCorde;
        else c = new Color(
            (pa.CouleurCorde.R + pb.CouleurCorde.R) * 0.5f,
            (pa.CouleurCorde.G + pb.CouleurCorde.G) * 0.5f,
            (pa.CouleurCorde.B + pb.CouleurCorde.B) * 0.5f
        );
        if (niveauTressage > 0) c = c * Mathf.Pow(0.84f, niveauTressage);
        return c;
    }

    public static Material ObtenirMaterielCorde(int idA, int idB, int niveauTressage)
    {
        float assombri = niveauTressage > 0 ? Mathf.Pow(0.84f, niveauTressage) : 1f;
        Color ca = (ObtenirProfilFlexible(idA, out var pa) ? pa.CouleurCorde : new Color(0.52f, 0.42f, 0.28f)) * assombri;
        Color cb = (ObtenirProfilFlexible(idB, out var pb) ? pb.CouleurCorde : new Color(0.52f, 0.42f, 0.28f)) * assombri;

        // Pas de texture d’albedo ni triplanar : une projection sur le volume lisse le relief et « bouge » en monde.
        // Couleur plate : N·L et ombres suivent les normales du mesh (effet facetté comme le .glb en viewport gris).
        Color albedo = idA == idB ? ca : ca.Lerp(cb, 0.5f);

        return new StandardMaterial3D
        {
            AlbedoColor = albedo,
            Roughness = 0.9f,
            Metallic = 0f,
            NormalEnabled = false,
            RimEnabled = false,
            Uv1Triplanar = false,
            Uv1WorldTriplanar = false
        };
    }

    public static void ObtenirStatsCorde(int idA, int idB, out float durabilite, out float tensionMax)
    {
        bool okA = ObtenirProfilFlexible(idA, out var pa);
        bool okB = ObtenirProfilFlexible(idB, out var pb);
        if (!okA && !okB) { durabilite = 6f; tensionMax = 5f; return; }
        if (!okA) { pa = pb; }
        if (!okB) { pb = pa; }
        float baseDurabilite = (pa.Durabilite + pb.Durabilite) * 0.5f;
        float baseTension = (pa.TensionMax + pb.TensionMax) * 0.5f;
        durabilite = baseDurabilite * 1.35f;
        tensionMax = baseTension * 1.5f;
        if (pa.Fragile || pb.Fragile) durabilite *= 0.75f;
    }

    public static float ObtenirDurabiliteBois(byte indexBotanique)
    {
        return indexBotanique switch
        {
            0 => 18.0f,
            _ => 10.0f
        };
    }

    public static float CalculerDurabiliteMaxNouvelleDague(SlotInventaire rochePlate, SlotInventaire corde)
    {
        int idxLame = Mathf.Clamp(rochePlate.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, out float durCord, out _);
        float maxDur = mineral * 2.4f + durCord * 12f;
        maxDur *= Mathf.Max(0.4f, 1f - corde.NiveauFracture * 0.11f);
        return Mathf.Clamp(maxDur, 55f, 480f);
    }

    public static float CalculerDurabiliteMaxDagueDepuisSlot(SlotInventaire dague)
    {
        int idxLame = Mathf.Clamp(dague.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(dague.IndexMorphologique, dague.IndexTaille, out float durCord, out _);
        float maxDur = mineral * 2.4f + durCord * 12f;
        maxDur *= Mathf.Max(0.4f, 1f - dague.NiveauFracture * 0.11f);
        return Mathf.Clamp(maxDur, 55f, 480f);
    }

    public static float CalculerDurabiliteMaxNouvelleHachette(SlotInventaire roche, SlotInventaire corde, SlotInventaire baton)
    {
        int idxLame = Mathf.Clamp(roche.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(baton.IndexBotanique);
        float maxDur = mineral * 3.5f + durCord * 6f + durBois * 15f;
        maxDur *= Mathf.Max(0.4f, 1f - corde.NiveauFracture * 0.11f);
        return Mathf.Clamp(maxDur, 100f, 1000f);
    }

    public static float CalculerDurabiliteMaxHachetteDepuisSlot(SlotInventaire hachette)
    {
        int idxLame = Mathf.Clamp(hachette.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(hachette.IndexMorphologique, hachette.IndexTaille, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(hachette.IndexBotanique);
        float maxDur = mineral * 3.5f + durCord * 6f + durBois * 15f;
        maxDur *= Mathf.Max(0.4f, 1f - hachette.NiveauFracture * 0.11f);
        return Mathf.Clamp(maxDur, 100f, 1000f);
    }

    public static void InitialiserDurabiliteOutilSiBesoin(ref SlotInventaire s)
    {
        if (s.ID != 105 && s.ID != 106) return;
        if (s.DurabiliteOutilMax > 0.5f)
        {
            if (s.DurabiliteOutilActuelle <= 0f)
                s.DurabiliteOutilActuelle = s.DurabiliteOutilMax;
            return;
        }
        if (s.ID == 105)
        {
            float max = CalculerDurabiliteMaxDagueDepuisSlot(s);
            s.DurabiliteOutilMax = max;
            s.DurabiliteOutilActuelle = max;
        }
        else if (s.ID == 106)
        {
            float max = CalculerDurabiliteMaxHachetteDepuisSlot(s);
            s.DurabiliteOutilMax = max;
            s.DurabiliteOutilActuelle = max;
        }
    }

    public static string ObtenirNomObjet(SlotInventaire slot)
    {
        if (slot.EstVide)
            return "";
        int id = slot.ID;
        if (id >= 40 && id <= 49)
        {
            int indexMatiere = id - 40;
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
            string essence = slot.IndexBotanique == 0 ? "Chêne" : "Bois";
            return $"Atelier Primitif en {essence}";
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
        if (id == 100)
        {
            int clef = Joueur.ClefRegistreOutilForge(slot);
            if (clef != 0 && Joueur.RegistreOutilsForges.TryGetValue(clef, out var st) && !string.IsNullOrEmpty(st.Nom))
                return st.Nom;
            return "Outil forgé";
        }
        if (id == Joueur.IdObjetSacDos) return "Sac à dos";
        if (id == Joueur.IdObjetCeinturePoches) return "Ceinture à poches";
        if (ObtenirProfilFlexible(id, out var flex))
            return flex.Nom;
        if (id == 30)
        {
            string essence = slot.IndexBotanique == 0 ? "Chêne" : "Bois";
            string longueur = slot.IndexTaille switch { 0 => "Tronc Brut", 1 => "Bûche Standard", 2 => "Demi-Bûche Courte", 3 => "Rondin", _ => "Morceau" };
            string fente = slot.IndexMorphologique switch { 0 => "", 1 => " (Fendue en 2)", 2 => " (Fendue en 4)", 3 => " (Fendue en 8)", _ => " (Éclat)" };
            return $"{longueur}{fente} de {essence}";
        }
        if (id == 32)
        {
            string essence = slot.IndexBotanique == 0 ? "Chêne" : "Bois";
            string longueur = slot.IndexTaille switch { 0 => "Brin brut", 1 => "Bâton standard", 2 => "Demi-bâton", 3 => "Rondin fin", _ => "Morceau" };
            string fente = slot.IndexMorphologique switch { 0 => "", 1 => " (Fendu en 2)", 2 => " (Fendu en 4)", 3 => " (Planchette)", _ => "" };
            return $"{longueur}{fente} de {essence}";
        }
        if (id == 20)
        {
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"{pa.Nom}+{pb.Nom}";
            if (a)
                return pa.Nom;
            if (b)
                return pb.Nom;
            return "Corde";
        }
        if (id == 21)
        {
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"Tissu (tier 0) {pa.Nom}+{pb.Nom}";
            if (a)
                return $"Tissu (tier 0) {pa.Nom}";
            if (b)
                return $"Tissu (tier 0) {pb.Nom}";
            return "Tissu (tier 0)";
        }
        return id switch
        {
            1 => "Terre",
            2 => "Roche",
            3 => "Sable",
            4 => "Neige",
            5 => "Neige / glace",
            6 => "Terre aride",
            7 => "Boue",
            8 => "Terre tropicale",
            9 => "Terre gelée",
            34 => "Feuillage",
            999 => "Végétation",
            _ => $"Objet #{id}"
        };
    }

    /// <param name="grilleCraft3x3Table">True uniquement si le menu craft est ouvert depuis l’établi : grille 3×3 et indices c0–c3 sur le bloc haut-gauche (0,1,3,4). False = inventaire (Q) : 2×2 classique (0–3), pas de 3×3.</param>
    public static SlotInventaire EvaluerRecette(SlotInventaire[] grille, bool grilleCraft3x3Table = false)
    {
        if (grille == null || grille.Length < 4)
            return new SlotInventaire();

        int nCell = grilleCraft3x3Table ? Mathf.Min(grille.Length, 9) : 4;
        int strideColonne = grilleCraft3x3Table ? 3 : 2;

        var ingredients = new List<SlotInventaire>();
        for (int i = 0; i < nCell; i++)
        {
            if (!grille[i].EstVide)
                ingredients.Add(grille[i]);
        }
        if (ingredients.Count == 0)
            return new SlotInventaire();

        if (ingredients.Count == 2)
        {
            for (int col = 0; col < 2; col++)
            {
                if (col + strideColonne >= grille.Length) break;
                SlotInventaire haut = grille[col];
                SlotInventaire bas = grille[col + strideColonne];
                if (haut.EstVide || bas.EstVide) continue;
                bool estRochePlate = ItemPhysique.EstIdRocheMatiere(haut.ID) && haut.IndexMorphologique == 1;
                bool estCorde = bas.ID == 20;
                if (estRochePlate && estCorde)
                {
                    float dMax = CalculerDurabiliteMaxNouvelleDague(haut, bas);
                    return new SlotInventaire
                    {
                        ID = 105,
                        IndexChimique = haut.ID - ItemPhysique.IdRocheMatiereMin,
                        IndexMorphologique = bas.IndexChimique,
                        IndexTaille = bas.IndexMorphologique,
                        IndexTailleLameRoche = Mathf.Clamp(haut.IndexTaille, 0, 4),
                        NiveauFracture = bas.NiveauFracture,
                        EstUnEclat = false,
                        DurabiliteOutilMax = dMax,
                        DurabiliteOutilActuelle = dMax
                    };
                }
            }
        }

        static bool EstSlotRochePlateCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 1;
        static bool EstSlotCordeCraft(SlotInventaire s) => !s.EstVide && s.ID == 20;
        static bool EstSlotBatonCraft(SlotInventaire s) => !s.EstVide && s.ID == 32;

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

        // RECETTE : 4 cordes (20) en carré 2×2 (inventaire ou coin haut-gauche de l’établi) → tissu primitif tier 0 (21).
        if (EstSlotCordeCraft(c0) && EstSlotCordeCraft(c1) && EstSlotCordeCraft(c2) && EstSlotCordeCraft(c3))
        {
            int nf = Mathf.Max(Mathf.Max(c0.NiveauFracture, c1.NiveauFracture), Mathf.Max(c2.NiveauFracture, c3.NiveauFracture));
            return new SlotInventaire
            {
                ID = 21,
                IndexChimique = c0.IndexChimique,
                IndexMorphologique = c0.IndexMorphologique,
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
                NiveauFracture = corde.NiveauFracture,
                DurabiliteOutilMax = dMax,
                DurabiliteOutilActuelle = dMax
            };
        }

        if (EstSlotRochePlateCraft(c0) && EstSlotCordeCraft(c1) && c2.EstVide && EstSlotBatonCraft(c3))
            return ConstruireHachette106(c0, c1, c3);
        if (EstSlotCordeCraft(c0) && EstSlotRochePlateCraft(c1) && EstSlotBatonCraft(c2) && c3.EstVide)
            return ConstruireHachette106(c1, c0, c2);

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
        // Bois A : demi-bûche (fente longitudinale morph 1), longueur standard (1) ou courte (2) après débitage.
        // Bois B : cylindre plein (morph 0), bûche standard (1) OU demi-bûche courte (2) — le jeu ne raccourcit pas toujours avant la fente.
        // Roche : ronde (morph 0), taille mini (0) ou petite (1).
        // À l’établi (3×3) : corde en [4], bûches [0]/[3] ou l’inverse. En poche (2×2) : indices 0–3 classiques.
        static bool EstDemiBucheCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && (s.IndexTaille == 1 || s.IndexTaille == 2);
        static bool EstBuchePleineCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && (s.IndexTaille == 1 || s.IndexTaille == 2);
        static bool EstPetiteRocheRondeCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 0 && (s.IndexTaille == 0 || s.IndexTaille == 1);

        bool paireBoisOk = (EstDemiBucheCraft(c0) && EstBuchePleineCraft(c2)) || (EstDemiBucheCraft(c2) && EstBuchePleineCraft(c0));
        bool estCordeAtelier = c3.ID == 20;
        byte essenceAtelier = EstDemiBucheCraft(c0) ? c0.IndexBotanique : c2.IndexBotanique;

        if (paireBoisOk && EstPetiteRocheRondeCraft(c1) && estCordeAtelier)
        {
            return new SlotInventaire
            {
                ID = 200,
                IndexBotanique = essenceAtelier,
                IndexChimique = c3.IndexChimique,
                IndexMorphologique = c3.IndexMorphologique
            };
        }

        return new SlotInventaire();
    }
}
