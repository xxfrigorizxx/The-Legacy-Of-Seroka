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
        if (niveauQualiteCorde == Joueur.TagVarianteIntestin || niveauQualiteCorde == Joueur.TagVarianteIntestinSolide)
        {
            durabilite *= 10f;
            tensionMax *= 10f;
            return;
        }
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

    public static float CalculerDurabiliteMaxNouvelleLance(SlotInventaire roche, SlotInventaire corde, SlotInventaire baton)
    {
        int idxLame = Mathf.Clamp(roche.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, corde.IndexBotanique, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(baton.IndexBotanique);
        float capPierre = mineral * 0.86f;
        float capCorde = durCord * 7.1f;
        float capBois = durBois * 5.4f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 26f, 200f);
    }

    public static float CalculerDurabiliteMaxLanceDepuisSlot(SlotInventaire lance)
    {
        int idxLame = Mathf.Clamp(lance.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(lance.IndexMorphologique, lance.IndexTaille, lance.NiveauFracture, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(lance.IndexBotanique);
        float capPierre = mineral * 0.86f;
        float capCorde = durCord * 7.1f;
        float capBois = durBois * 5.4f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 26f, 200f);
    }

    /// <summary>Durabilité max à la création : roche pointe + ligature + manche + deux bâtons en T (craft).</summary>
    public static float CalculerDurabiliteMaxNouvelleFaux(SlotInventaire rocheLame, SlotInventaire corde, SlotInventaire manche, SlotInventaire batonT1, SlotInventaire batonT2)
    {
        int idxLame = Mathf.Clamp(rocheLame.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(corde.IndexChimique, corde.IndexMorphologique, corde.IndexBotanique, out float durCord, out _);
        float dManche = ObtenirDurabiliteBois(manche.IndexBotanique);
        float dT1 = ObtenirDurabiliteBois(batonT1.IndexBotanique);
        float dT2 = ObtenirDurabiliteBois(batonT2.IndexBotanique);
        float durBoisMin = Mathf.Min(dManche, Mathf.Min(dT1, dT2));
        float capPierre = mineral * 0.68f;
        float capCorde = durCord * 7.2f;
        float capBois = durBoisMin * 4.05f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 20f, 155f);
    }

    public static float CalculerDurabiliteMaxFauxDepuisSlot(SlotInventaire faux)
    {
        int idxLame = Mathf.Clamp(faux.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        ObtenirStatsCorde(faux.IndexMorphologique, faux.IndexTaille, faux.NiveauFracture, out float durCord, out _);
        float durBois = ObtenirDurabiliteBois(faux.IndexBotanique);
        float capPierre = mineral * 0.68f;
        float capCorde = durCord * 7.2f;
        float capBois = durBois * 4.35f;
        float maxDur = Mathf.Min(capPierre, Mathf.Min(capCorde, capBois));
        return Mathf.Clamp(maxDur, 20f, 155f);
    }

    public static float CalculerDurabiliteMaxNouvelAllumeFeu(SlotInventaire rocheSulfureuse)
    {
        int idx = Mathf.Clamp(rocheSulfureuse.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idx].ResistanceFuture;
        float maxDur = mineral * 0.22f + 6f;
        return Mathf.Clamp(maxDur, 8f, 60f);
    }

    public static float CalculerDurabiliteMaxAllumeFeuDepuisSlot(SlotInventaire allumeFeu)
    {
        int idx = Mathf.Clamp(allumeFeu.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idx].ResistanceFuture;
        float maxDur = mineral * 0.22f + 6f;
        return Mathf.Clamp(maxDur, 8f, 60f);
    }

    public static void InitialiserDurabiliteOutilSiBesoin(ref SlotInventaire s)
    {
        if (s.ID != 105 && s.ID != 106 && s.ID != Joueur.IdObjetPellePierreTier0 && s.ID != Joueur.IdObjetPiochePierreTier0 && s.ID != Joueur.IdObjetLancePierreTier0 && s.ID != Joueur.IdObjetFauxPierreTier0 && s.ID != Joueur.IdObjetAllumeFeu) return;
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
        else if (s.ID == Joueur.IdObjetLancePierreTier0)
        {
            float max = CalculerDurabiliteMaxLanceDepuisSlot(s);
            s.DurabiliteOutilMax = max;
            s.DurabiliteOutilActuelle = max;
        }
        else if (s.ID == Joueur.IdObjetFauxPierreTier0)
        {
            float max = CalculerDurabiliteMaxFauxDepuisSlot(s);
            s.DurabiliteOutilMax = max;
            s.DurabiliteOutilActuelle = max;
        }
        else if (s.ID == Joueur.IdObjetAllumeFeu)
        {
            float max = CalculerDurabiliteMaxAllumeFeuDepuisSlot(s);
            s.DurabiliteOutilMax = max;
            s.DurabiliteOutilActuelle = max;
        }
    }

    public static string ObtenirNomObjet(SlotInventaire slot)
    {
        if (slot.EstVide)
            return "";
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
        if (id == Joueur.IdObjetRackBatons)
            return "Rack à bâtons";
        if (id == Joueur.IdObjetRackBuches)
            return "Rack à bûches";
        if (id == Joueur.IdObjetPitFeu)
        {
            string essence = slot.IndexBotanique switch { 0 => "Chêne", 1 => "Bouleau", 2 => "Pin", 3 => "Sapin", 4 => "Fromager", _ => "Bois" };
            return $"Pit à feu ({essence})";
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
        if (id == Joueur.IdObjetSteakCru || id == Joueur.IdObjetOsBoeuf || id == Joueur.IdObjetCuirBoeuf || id == Joueur.IdObjetIntestinBoeuf || id == Joueur.IdObjetIntestinBoeufNettoye)
        {
            int q = Joueur.ObtenirQuantiteSlot(slot);
            string nom = id == Joueur.IdObjetSteakCru ? "Steak cru"
                : (id == Joueur.IdObjetOsBoeuf ? "Os"
                : (id == Joueur.IdObjetCuirBoeuf ? "Cuir"
                : (id == Joueur.IdObjetIntestinBoeufNettoye ? "Intestin propre" : "Intestin")));
            return q > 1 ? $"{nom} x{q}" : nom;
        }
        return id switch
        {
            1 => "Terre",
            2 => "Roche",
            3 => "Sable",
            4 => "Neige",
            5 => "Neige glacee",
            6 => "Terre aride",
            7 => "Boue",
            8 => "Herbe",
            9 => "Terre gelée",
            10 => "Buisson plein",
            11 => "Buisson vide",
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
            // 2 intestins propres -> 1 corde d'intestin.
            if (ingredients[0].ID == Joueur.IdObjetIntestinBoeufNettoye && ingredients[1].ID == Joueur.IdObjetIntestinBoeufNettoye)
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
        /// <summary>Manche de hachette primitive : bâton brut (32) ou branche (31), même essence <see cref="SlotInventaire.IndexBotanique"/>.</summary>
        static bool EstSlotMancheHachettePrimitive(SlotInventaire s) => !s.EstVide && (s.ID == 32 || s.ID == BlocChutant.ID_BRANCHE);
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
