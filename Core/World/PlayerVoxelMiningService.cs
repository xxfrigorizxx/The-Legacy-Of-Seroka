using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>Phase 1 pure : minage du terrain Marching Cubes uniquement. Clic gauche.</summary>
    private void ExecuterMinageVoxel()
    {
        float multiplicateurForce = ObtenirMultiplicateurDegatsForce();
        _rayon.ForceRaycastUpdate();
        if (!_rayon.IsColliding()) return;
        Node objetTouche = NoeudDepuisColliderRaycast(_rayon.GetCollider());
        // ArbreVivant : seules roches brutes et Ã©clats â€” la dague (105) est trop fragile pour le bois.
        ArbreVivant arbre = ObtenirArbreDepuisCollider(objetTouche);
        if (arbre != null)
        {
            var main = MainGaucheEstActive ? MainGauche : MainDroite;
            bool outilTranchantPourArbre = ItemPhysique.EstIdRocheMatiere(main.ID) || main.EstUnEclat || main.ID == 106;
            if (!outilTranchantPourArbre) return;

            float degatsArbre = 5.0f * multiplicateurForce + ObtenirBonusDegatsArbreBucheron();
            float epaisseurLame = 0.2f;
            if (!main.EstVide)
            {
                if (main.EstUnEclat && main.MeshEclat != null)
                {
                    Aabb boite = main.MeshEclat.GetAabb();
                    epaisseurLame = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));
                    degatsArbre *= Mathf.Clamp(0.2f / Mathf.Max(0.005f, epaisseurLame), 1.0f, 40.0f);
                }
                else if (ItemPhysique.EstMatiereSilexParIdObjet(main.ID)) { epaisseurLame = 0.05f; degatsArbre *= 2.5f; }
                else if (main.ID == 106) { epaisseurLame = 0.065f; degatsArbre *= 2.2f; }
            }
            Vector3 pointImpact = _rayon.GetCollisionPoint();
            Vector3 directionFrappe = -_rayon.GetCollisionNormal();
            if (directionFrappe.LengthSquared() < 0.1f)
                directionFrappe = -_camera.GlobalTransform.Basis.Z.Normalized();
            bool hachetteBonneOrientation = main.ID == 106 && EstFrappeHachette106AvecLaLame(pointImpact, directionFrappe);
            int resultatCoupe = arbre.SubirDegats(pointImpact, directionFrappe, degatsArbre, epaisseurLame, hachetteBonneOrientation);
            if (resultatCoupe == 0) return;
            if (resultatCoupe == 2)
                AjouterXpMetier("Bucheron", 1UL);
            bool rochePlate = ItemPhysique.EstIdRocheMatiere(main.ID) && main.IndexMorphologique == 1;
            if (rochePlate && ObtenirNiveauFutureState("Force") < 15UL)
                AjouterXpFutureState("Force", 1UL);
            JouerSonEtEffetCoupeArbre(pointImpact);
            return;
        }
        // Si on touche un objet physique valide, on annule le minage (y compris CollisionShape3D sous RigidBody).
        if (objetTouche != null && (objetTouche is ItemPhysique || ResoudreRigidBodyDepuisCollider(objetTouche) != null || objetTouche.IsInGroup("BlocsPoses"))) return;

        // Si objetTouche est null, cela signifie qu'on a touchÃ© le terrain bas-niveau ! ON CONTINUE LE MINAGE.
        Vector3 pointImpactVoxel = _rayon.GetCollisionPoint();
        Vector3 normaleImpact = _rayon.GetCollisionNormal();
        int idExtrait = ObtenirMatiereSolideDepuisImpact(pointImpactVoxel, normaleImpact);
        if (idExtrait < 1 || idExtrait > 9)
            return;

        if (MainGaucheEstActive && !MainGauche.EstVide && !MainDroite.EstVide) return;
        if (!MainGaucheEstActive && !MainDroite.EstVide && !MainGauche.EstVide) return;

        float forceDegats = 5.0f * multiplicateurForce;
        SlotInventaire mainActive = MainGaucheEstActive ? MainGauche : MainDroite;

        // THÃ‰ORÃˆME DE LA LAME : L'Ã©paisseur dicte le tranchant
        if (mainActive.EstUnEclat && mainActive.MeshEclat != null)
        {
            Aabb boite = mainActive.MeshEclat.GetAabb();
            float epaisseur = Mathf.Min(boite.Size.X, Mathf.Min(boite.Size.Y, boite.Size.Z));

            float multiplicateur = 0.2f / Mathf.Max(0.005f, epaisseur);
            forceDegats *= Mathf.Clamp(multiplicateur, 1.0f, 40.0f);

            GD.Print($"ZERO-K : Lame dÃ©tectÃ©e. Ã‰paisseur: {epaisseur:F3}m | Tranchant: x{multiplicateur:F1}");
        }
        else if (ItemPhysique.EstMatiereSilexParIdObjet(mainActive.ID))
            forceDegats *= 2.5f;

        _gestionnaireMonde?.AppliquerDestructionGlobale(pointImpactVoxel, RAYON_SCULPTURE, forceDegats);
        bool extractionRoche = ItemPhysique.EstIdRocheMatiere(idExtrait);
        if (extractionRoche)
            AjouterXpMetier("Mineur", 1UL);
        else
            AjouterXpMetier("Terrassier", 1UL);
        AjouterXpFutureState("Force", 1UL);

        var nouveauSlot = new SlotInventaire { ID = idExtrait, IndexMorphologique = 0, IndexChimique = 0 };
        if (MainGaucheEstActive)
        {
            if (MainGauche.EstVide) MainGauche = nouveauSlot;
            else MainDroite = nouveauSlot;
        }
        else
        {
            if (MainDroite.EstVide) MainDroite = nouveauSlot;
            else MainGauche = nouveauSlot;
        }
        RafraichirHUD();
    }
}
