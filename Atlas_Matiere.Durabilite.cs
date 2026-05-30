using Godot;
using System;
using System.Collections.Generic;

public static partial class Atlas_Matiere
{
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

    public static float CalculerDurabiliteMaxNouvelleHachePierre(SlotInventaire rocheA, SlotInventaire rocheB, SlotInventaire baton)
    {
        int idxA = Mathf.Clamp(rocheA.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        int idxB = Mathf.Clamp(rocheB.ID - ItemPhysique.IdRocheMatiereMin, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = (ItemPhysique.TableGeologique[idxA].ResistanceFuture + ItemPhysique.TableGeologique[idxB].ResistanceFuture) * 0.5f;
        float durBois = ObtenirDurabiliteBois(baton.IndexBotanique);
        float capPierre = mineral * 0.74f;
        float capBois = durBois * 4.6f;
        float maxDurBase = Mathf.Min(capPierre, capBois);
        // Hache pierre tier 1 : 2x à 3x la résistance de la hachette primitive.
        float maxDur = maxDurBase * 2.4f;
        return Mathf.Clamp(maxDur, 55f, 430f);
    }

    public static float CalculerDurabiliteMaxHachePierreDepuisSlot(SlotInventaire hachePierre)
    {
        int idxLame = Mathf.Clamp(hachePierre.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        float mineral = ItemPhysique.TableGeologique[idxLame].ResistanceFuture;
        float durBois = ObtenirDurabiliteBois(hachePierre.IndexBotanique);
        float capPierre = mineral * 0.74f;
        float capBois = durBois * 4.6f;
        float maxDurBase = Mathf.Min(capPierre, capBois);
        float maxDur = maxDurBase * 2.4f;
        return Mathf.Clamp(maxDur, 55f, 430f);
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
        if (s.ID != 105 && s.ID != 106 && s.ID != Joueur.IdObjetHachePierreTier1 && s.ID != Joueur.IdObjetPellePierreTier0 && s.ID != Joueur.IdObjetPiochePierreTier0 && s.ID != Joueur.IdObjetLancePierreTier0 && s.ID != Joueur.IdObjetFauxPierreTier0 && s.ID != Joueur.IdObjetAllumeFeu) return;
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
        else if (s.ID == Joueur.IdObjetHachePierreTier1)
        {
            float max = CalculerDurabiliteMaxHachePierreDepuisSlot(s);
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
}
