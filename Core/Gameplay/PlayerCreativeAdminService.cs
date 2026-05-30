using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    public void DefinirModeCreatifDepuisServeur(bool actif, bool noclip)
    {
        _modeCreatifAdmin = actif;
        _noclipAdmin = actif && noclip;
        AppliquerEtatCollisionModeCreatif();
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
        RafraichirHUD();
    }

    private void AppliquerEtatCollisionModeCreatif()
    {
        if (!_modeCreatifAdmin || !_noclipAdmin)
        {
            CollisionLayer = _collisionLayerParDefaut;
            CollisionMask = _collisionMaskParDefaut;
            return;
        }

        // Noclip admin : on coupe les collisions corps<->monde.
        CollisionLayer = 0u;
        CollisionMask = 0u;
    }

    public bool DemanderInjectionItemCreatifAdmin(SlotInventaire slot)
    {
        if (!_modeCreatifAdmin || _gestionnaireMonde == null || slot.EstVide) return false;
        return _gestionnaireMonde.DemanderInjectionItemCreatif(slot);
    }

    public void InjecterSlotCreatifAdmin(SlotInventaire slot)
    {
        if (slot.EstVide) return;
        Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref slot);
        slot.Quantite = Mathf.Max(1, ObtenirPileMax(slot));
        int restant = slot.Quantite;

        void EmpilerDans(ref SlotInventaire dest)
        {
            if (restant <= 0) return;
            if (dest.EstVide || !SontEmpilables(dest, slot)) return;
            int max = ObtenirPileMax(dest);
            int q = ObtenirQuantiteSlot(dest);
            int libre = Mathf.Max(0, max - q);
            if (libre <= 0) return;
            int ajout = Mathf.Min(libre, restant);
            dest.Quantite = q + ajout;
            restant -= ajout;
        }

        EmpilerDans(ref MainGauche);
        EmpilerDans(ref MainDroite);
        if (ASacEquipe())
        {
            for (int i = 0; i < GrilleSacStockage.Length && restant > 0; i++)
            {
                ref SlotInventaire s = ref RefSlotSac(i);
                EmpilerDans(ref s);
            }
        }
        if (ACeintureSacochesEquipe())
        {
            for (int i = 0; i < GrilleCeintureStockage.Length && restant > 0; i++)
            {
                ref SlotInventaire s = ref RefSlotCeintureStockage(i);
                EmpilerDans(ref s);
            }
        }

        void DeposerNouveau(ref SlotInventaire dest)
        {
            if (restant <= 0 || !dest.EstVide) return;
            SlotInventaire copie = slot;
            copie.Quantite = Mathf.Min(restant, Mathf.Max(1, ObtenirPileMax(copie)));
            dest = copie;
            restant -= copie.Quantite;
        }

        DeposerNouveau(ref MainGauche);
        DeposerNouveau(ref MainDroite);
        if (ASacEquipe())
        {
            for (int i = 0; i < GrilleSacStockage.Length && restant > 0; i++)
            {
                ref SlotInventaire s = ref RefSlotSac(i);
                DeposerNouveau(ref s);
            }
        }
        if (ACeintureSacochesEquipe())
        {
            for (int i = 0; i < GrilleCeintureStockage.Length && restant > 0; i++)
            {
                ref SlotInventaire s = ref RefSlotCeintureStockage(i);
                DeposerNouveau(ref s);
            }
        }

        if (restant > 0)
        {
            SlotInventaire force = slot;
            force.Quantite = restant;
            if (MainGaucheEstActive) MainGauche = force;
            else MainDroite = force;
        }

        VerifierRecettes();
        RafraichirHUD();
        if (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            _menuAnatomie.RafraichirMenu();
    }
}
