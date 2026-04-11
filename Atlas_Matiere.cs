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
    private const int NiveauCordeSolideTier2 = 2;

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
        if (slot.ID == 20 || slot.ID == 21 || slot.ID == Joueur.IdObjetCeinturePoches || slot.ID == Joueur.IdObjetCeintureSacoches || slot.ID == Joueur.IdObjetPochetteTier0)
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
        ObtenirStatsCorde(idA, idB, 0, out durabilite, out tensionMax);
    }

    public static void ObtenirStatsCorde(int idA, int idB, int niveauQualiteCorde, out float durabilite, out float tensionMax)
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
        if (niveauQualiteCorde >= NiveauCordeSolideTier2)
        {
            durabilite *= 2.0f;
            tensionMax *= 1.35f;
        }
    }

    public static float ObtenirDurabiliteBois(byte indexBotanique)
    {
        return indexBotanique switch
        {
            0 => 18.0f, // Chêne : robuste
            1 => 13.0f, // Bouleau : tendre
            2 => 8.0f,  // Pin : résineux tendre
            3 => 7.0f,  // 📖 Sapin : Très tendre, similaire au Pin
            4 => 3.5f,  // 📖 Kapokier : Catastrophique pour forger un manche d'outil
            5 => 9.0f,  // Chêne mort : moitié de la résistance du chêne vivant
            6 => 6.5f,  // Bouleau mort : moitié de la résistance du bouleau vivant
            _ => 10.0f
        };
    }

    public static float CalculerDurabiliteMaxNouvelleDague(SlotInventaire rocheLame, SlotInventaire corde)
    {
        int idxLame = Mathf.Clamp(rocheLame.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, corde.IndexBotanique, out float durCord, out _);
        float capPierre = mineral * 0.62f;
        float capCorde = durCord * 8.0f;
        float maxDur = Mathf.Min(capPierre, capCorde);
        return Mathf.Clamp(maxDur, 18f, 140f);
    }

    public static float CalculerDurabiliteMaxDagueDepuisSlot(SlotInventaire dague)
    {
        int idxLame = Mathf.Clamp(dague.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(dague.IndexMorphologique, dague.IndexTaille, dague.NiveauFracture, out float durCord, out _);
        float capPierre = mineral * 0.62f;
        float capCorde = durCord * 8.0f;
        float maxDur = Mathf.Min(capPierre, capCorde);
        return Mathf.Clamp(maxDur, 18f, 140f);
    }

    public static float CalculerDurabiliteMaxNouvelleHachette(SlotInventaire roche, SlotInventaire corde, SlotInventaire baton)
    {
        int idxLame = Mathf.Clamp(roche.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, corde.IndexBotanique, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(baton.IndexBotanique);
        float capPierre = mineral * 0.78f;
        float capCorde = durCord * 6.8f;
        float capBois = durBois * 4.6f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 22f, 180f);
    }

    public static float CalculerDurabiliteMaxHachetteDepuisSlot(SlotInventaire hachette)
    {
        int idxLame = Mathf.Clamp(hachette.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(hachette.IndexMorphologique, hachette.IndexTaille, hachette.NiveauFracture, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(hachette.IndexBotanique);
        float capPierre = mineral * 0.78f;
        float capCorde = durCord * 6.8f;
        float capBois = durBois * 4.6f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 22f, 180f);
    }

    public static float CalculerDurabiliteMaxNouvellePelle(SlotInventaire roche, SlotInventaire corde, SlotInventaire baton)
    {
        int idxLame = Mathf.Clamp(roche.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, corde.IndexBotanique, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(baton.IndexBotanique);
        float capPierre = mineral * 0.75f;
        float capCorde = durCord * 6.6f;
        float capBois = durBois * 4.9f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 20f, 170f);
    }

    public static float CalculerDurabiliteMaxPelleDepuisSlot(SlotInventaire pelle)
    {
        int idxLame = Mathf.Clamp(pelle.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(pelle.IndexMorphologique, pelle.IndexTaille, pelle.NiveauFracture, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(pelle.IndexBotanique);
        float capPierre = mineral * 0.75f;
        float capCorde = durCord * 6.6f;
        float capBois = durBois * 4.9f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 20f, 170f);
    }

    public static float CalculerDurabiliteMaxNouvellePioche(SlotInventaire roche, SlotInventaire corde, SlotInventaire baton)
    {
        int idxLame = Mathf.Clamp(roche.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, corde.IndexBotanique, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(baton.IndexBotanique);
        float capPierre = mineral * 0.92f;
        float capCorde = durCord * 6.1f;
        float capBois = durBois * 4.3f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 24f, 190f);
    }

    public static float CalculerDurabiliteMaxPiocheDepuisSlot(SlotInventaire pioche)
    {
        int idxLame = Mathf.Clamp(pioche.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(pioche.IndexMorphologique, pioche.IndexTaille, pioche.NiveauFracture, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(pioche.IndexBotanique);
        float capPierre = mineral * 0.92f;
        float capCorde = durCord * 6.1f;
        float capBois = durBois * 4.3f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 24f, 190f);
    }

    public static void InitialiserDurabiliteOutilSiBesoin(ref SlotInventaire s)
    {
        if (s.ID != 105 && s.ID != 106 && s.ID != Joueur.IdObjetPellePierreTier0 && s.ID != Joueur.IdObjetPiochePierreTier0) return;
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
        else if (s.ID == Joueur.IdObjetPellePierreTier0)
        {
            float max = CalculerDurabiliteMaxPelleDepuisSlot(s);
            s.DurabiliteOutilMax = max;
            s.DurabiliteOutilActuelle = max;
        }
        else if (s.ID == Joueur.IdObjetPiochePierreTier0)
        {
            float max = CalculerDurabiliteMaxPiocheDepuisSlot(s);
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
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
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
            if (Joueur.EstVarianteHerbeSolide(slot)) return "Sac tier 0 solide (herbe, tier 2)";
            if (Joueur.EstVarianteLiane(slot)) return "Sac tier 0 en liane";
            return "Sac tier 0";
        }
        if (id == Joueur.IdObjetCeinturePoches)
        {
            string prefixe = Joueur.EstVarianteHerbeSolide(slot)
                ? "Ceinture à poches solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Ceinture à poches en liane" : "Ceinture à poches");
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
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
            if (a && b)
                return $"Ceinture à sacoches ({pa.Nom}+{pb.Nom})";
            if (a)
                return $"Ceinture à sacoches ({pa.Nom})";
            if (b)
                return $"Ceinture à sacoches ({pb.Nom})";
            return "Ceinture à sacoches";
        }
        if (id == Joueur.IdObjetPochetteTier0)
        {
            string prefixe = Joueur.EstVarianteHerbeSolide(slot)
                ? "Pochette tier 0 solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Pochette tier 0 en liane" : "Pochette tier 0");
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
        if (id == 32)
        {
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
            bool a = ObtenirProfilFlexible(slot.IndexChimique, out var pa);
            bool b = ObtenirProfilFlexible(slot.IndexMorphologique, out var pb);
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
            string prefixe = Joueur.EstVarianteHerbeSolide(slot)
                ? "Tissu solide (tier 2)"
                : (Joueur.EstVarianteLiane(slot) ? "Tissu en liane (tier 0)" : "Tissu (tier 0)");
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
            string couleur = slot.IndexChimique switch
            {
                1 => "violette",
                2 => "orange",
                _ => "rouge"
            };
            int q = Joueur.ObtenirQuantiteSlot(slot);
            return q > 1 ? $"Petites baies {couleur}s x{q}" : $"Petite baie {couleur}";
        }
        if (id == Joueur.IdObjetRackBatons)
            return "Rack à bâtons";
        if (id == Joueur.IdObjetRackBuches)
            return "Rack à bûches";
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
            10 => "Buisson plein",
            11 => "Buisson vide",
            31 => "Branche de buisson",
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

        // Une seule branche (bâton 32 brut) dans la grille → bâton façonné de la même essence : mêmes taille / morph / ScaleEclat, teinte façonnée (IndexChimique = 1).
        if (ingredients.Count == 1)
        {
            var br = ingredients[0];
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
        }

        if (ingredients.Count == 2)
        {
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
        static byte VarianteLigatureCraft(SlotInventaire s)
        {
            if (s.EstVide) return 0;
            if (s.ID == 16 || Joueur.EstVarianteLiane(s)) return Joueur.TagVarianteLiane;
            if (EstSlotCordeHerbeSolideCraft(s) || Joueur.EstVarianteHerbeSolide(s)) return Joueur.TagVarianteHerbeSolide;
            return LSystem_Botanique.IndexChene;
        }
        static bool MemeVarianteLigature(SlotInventaire a, SlotInventaire b) =>
            VarianteLigatureCraft(a) == VarianteLigatureCraft(b);
        static bool EstSlotTissuBaseCraft(SlotInventaire s) =>
            EstSlotTissuCraft(s) && !Joueur.EstVarianteLiane(s) && !Joueur.EstVarianteHerbeSolide(s);
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
        // Outils à durabilité: autorise corde (20) OU liane brute (16) comme ligature.
        static bool EstSlotLigatureOutilCraft(SlotInventaire s) => !s.EstVide && (s.ID == 20 || s.ID == 16);
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
        static bool EstSlotTissuCraft(SlotInventaire s) => !s.EstVide && s.ID == 21;
        static bool EstSlotBatonCraft(SlotInventaire s) => !s.EstVide && s.ID == 32;
        // B1 = bûche standard (taille 1) fendue en 4 (morph 2). La longueur « standard » est surtout dans IndexTaille, pas ScaleEclat.
        static bool EstSlotBucheQuartB1RackCraft(SlotInventaire s)
        {
            if (s.EstVide || s.ID != 30) return false;
            if (s.IndexMorphologique != 2) return false;
            if (s.IndexTaille != 1) return false;
            float z = s.ScaleEclat.Z;
            if (z <= 1e-4f) return true;
            return z >= 0.72f;
        }
        // B2 = demi-bûche courte (taille 2) fendue en 4, ou bûche standard avec longueur réellement réduite via ScaleEclat.
        static bool EstSlotBucheQuartB2RackCraft(SlotInventaire s)
        {
            if (s.EstVide || s.ID != 30) return false;
            if (s.IndexMorphologique != 2) return false;
            if (s.IndexTaille == 2) return true;
            if (s.IndexTaille != 1) return false;
            float z = s.ScaleEclat.Z;
            return z > 0.18f && z < 0.72f;
        }
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

        // RECETTE ATELIER : 6 cordes (20) → ceinture à poches (102). Formes : 2×3 (colonnes gauche/droite) ou 3×2 (lignes haut/bas).
        if (grilleCraft3x3Table && grille.Length >= 9)
        {
            // RECETTE ATELIER : Rack à bûches (110), patron strict.
            // (B1) ( ) (B1)
            // (B1) ( ) (B1)
            // ( C) (B2) ( C)
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
                    && grille[7].IndexMorphologique == bRef.IndexMorphologique
                    && grille[7].IndexChimique == bRef.IndexChimique
                    && grille[7].IndexBotanique == bRef.IndexBotanique;
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

            // RECETTE ATELIER : Rack à bâtons (109), sans position imposée.
            // Règle : exactement 3 bâtons (32) + 2 ligatures (corde 20 ou liane 16), tout le reste vide.
            int nbOccupes = 0;
            int nbBatons = 0;
            int nbLigatures = 0;
            bool toutesLigaturesLiane = true;
            bool toutesLigaturesHerbeSolide = true;
            int nfRack = 0;
            byte essenceBoisRack = LSystem_Botanique.IndexChene;
            bool essenceBoisDefinie = false;
            SlotInventaire refLigatureRack = default;
            for (int i = 0; i < 9; i++)
            {
                SlotInventaire s = grille[i];
                if (s.EstVide) continue;
                nbOccupes++;
                nfRack = Mathf.Max(nfRack, s.NiveauFracture);
                if (EstSlotBatonCraft(s))
                {
                    nbBatons++;
                    if (!essenceBoisDefinie)
                    {
                        essenceBoisRack = s.IndexBotanique;
                        essenceBoisDefinie = true;
                    }
                    continue;
                }
                if (EstSlotCordeOuLianeCraft(s))
                {
                    nbLigatures++;
                    if (refLigatureRack.EstVide) refLigatureRack = s;
                    if (s.ID != 16) toutesLigaturesLiane = false;
                    if (!EstSlotCordeHerbeSolideCraft(s)) toutesLigaturesHerbeSolide = false;
                    continue;
                }
                nbOccupes = 99;
                break;
            }
            if (nbOccupes == 5 && nbBatons == 3 && nbLigatures == 2)
            {
                byte tagVariante = toutesLigaturesLiane ? Joueur.TagVarianteLiane
                    : (toutesLigaturesHerbeSolide ? Joueur.TagVarianteHerbeSolide : LSystem_Botanique.IndexChene);
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetRackBatons,
                    IndexChimique = refLigatureRack.EstVide ? 0 : refLigatureRack.IndexChimique,
                    IndexMorphologique = refLigatureRack.EstVide ? 0 : refLigatureRack.IndexMorphologique,
                    // IMPORTANT : essence du bois du rack = essence des bâtons utilisés (visuel cohérent).
                    IndexBotanique = essenceBoisDefinie ? essenceBoisRack : LSystem_Botanique.IndexChene,
                    NiveauFracture = nfRack,
                    // Tag ligature stocké séparément pour ne pas écraser l'essence bois.
                    GenomeAssemblage = $"RACKL:{tagVariante}",
                    EstUnEclat = false
                };
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
                byte variantePochette = Joueur.EstVarianteLiane(grille[4]) ? Joueur.TagVarianteLiane
                    : (Joueur.EstVarianteHerbeSolide(grille[4]) ? Joueur.TagVarianteHerbeSolide : LSystem_Botanique.IndexChene);
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
                byte p0 = Joueur.EstVarianteHerbeSolide(grille[0]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[0]) ? Joueur.TagVarianteLiane : (byte)0);
                byte p1 = Joueur.EstVarianteHerbeSolide(grille[2]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[2]) ? Joueur.TagVarianteLiane : (byte)0);
                byte p2 = Joueur.EstVarianteHerbeSolide(grille[6]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[6]) ? Joueur.TagVarianteLiane : (byte)0);
                byte p3 = Joueur.EstVarianteHerbeSolide(grille[8]) ? Joueur.TagVarianteHerbeSolide : (Joueur.EstVarianteLiane(grille[8]) ? Joueur.TagVarianteLiane : (byte)0);
                bool versionLiane = Joueur.EstVarianteLiane(refB)
                    && Joueur.EstVarianteLiane(grille[0]) && Joueur.EstVarianteLiane(grille[2])
                    && Joueur.EstVarianteLiane(grille[6]) && Joueur.EstVarianteLiane(grille[8]);
                bool versionHerbeSolide = Joueur.EstVarianteHerbeSolide(refB)
                    && Joueur.EstVarianteHerbeSolide(grille[0]) && Joueur.EstVarianteHerbeSolide(grille[2])
                    && Joueur.EstVarianteHerbeSolide(grille[6]) && Joueur.EstVarianteHerbeSolide(grille[8]);
                int nf = Mathf.Max(Mathf.Max(grille[0].NiveauFracture, grille[2].NiveauFracture), Mathf.Max(Mathf.Max(grille[6].NiveauFracture, grille[8].NiveauFracture), refB.NiveauFracture));
                return new SlotInventaire
                {
                    ID = Joueur.IdObjetCeintureSacoches,
                    IndexChimique = refB.IndexChimique,
                    IndexMorphologique = refB.IndexMorphologique,
                    IndexBotanique = versionLiane ? Joueur.TagVarianteLiane : (versionHerbeSolide ? Joueur.TagVarianteHerbeSolide : LSystem_Botanique.IndexChene),
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

        if (EstSlotRochePlateCraft(c0) && EstSlotLigatureOutilCraft(c1) && c2.EstVide && EstSlotBatonCraft(c3))
            return ConstruireHachette106(c0, NormaliserLigatureOutil(c1), c3);
        if (EstSlotLigatureOutilCraft(c0) && EstSlotRochePlateCraft(c1) && EstSlotBatonCraft(c2) && c3.EstVide)
            return ConstruireHachette106(c1, NormaliserLigatureOutil(c0), c2);

        // Pelle pierre tier0 (107) : colonne verticale en 3x3 atelier [1]=bâton façonné (toute essence), [4]=ficelle, [7]=petite roche ovale.
        static bool EstSlotBatonFaconneCraft(SlotInventaire s) =>
            !s.EstVide && s.ID == 32 && s.IndexChimique == 1;
        static bool EstSlotPetiteRocheOvaleCraft(SlotInventaire s) =>
            !s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 2 && (s.IndexTaille == 0 || s.IndexTaille == 1);
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
