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
public static partial class Atlas_Matiere
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



}
