using Godot;
using System;

public partial class Joueur
{
    private void MettreAJourObjetEnMain()
    {
        var main = MainGaucheEstActive ? MainGauche : MainDroite;
        if (main.EstVide || !EstObjetAvecVisuel(main.ID))
        {
            NettoyerModelesEnfants(_objetEnMain);
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            if (_objetEnMain.HasMeta(MetaSignaturePochette103))
                _objetEnMain.RemoveMeta(MetaSignaturePochette103);
            if (_objetEnMain.HasMeta(MetaSignatureSac101))
                _objetEnMain.RemoveMeta(MetaSignatureSac101);
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            return;
        }
        if (main.ID == 105)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotDague105(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureDague105) ? (int)_objetEnMain.GetMeta(MetaSignatureDague105).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleArme(_objetEnMain, main, 0.35f, ObtenirFacteurEchelleLameDague(main));
                _objetEnMain.SetMeta(MetaSignatureDague105, sig);
            }
            // +20 % vs l’ancien 0,5, puis +25 % (0,6 → 0,75) : lisibilité dague en main.
            _objetEnMain.Scale = Vector3.One * (0.5f * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = new Vector3(-15f + _rotationManuelleX, 10f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 106)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotHachette106(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureHachette106) ? (int)_objetEnMain.GetMeta(MetaSignatureHachette106).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleArme(_objetEnMain, main, 0.42f, 1f);
                _objetEnMain.SetMeta(MetaSignatureHachette106, sig);
            }
            _objetEnMain.Scale = Vector3.One * (0.52f * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = new Vector3(-18f + _rotationManuelleX, 12f + _rotationManuelleY, 4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 20)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotCorde20(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureCorde20) ? (int)_objetEnMain.GetMeta(MetaSignatureCorde20).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleCordeTier0Gazon(_objetEnMain, main, 0.38f);
                _objetEnMain.SetMeta(MetaSignatureCorde20, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-15f + _rotationManuelleX, 10f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 21)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotTissu21(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureTissu21) ? (int)_objetEnMain.GetMeta(MetaSignatureTissu21).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleTissuTier0(_objetEnMain, main, 0.36f);
                _objetEnMain.SetMeta(MetaSignatureTissu21, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-12f + _rotationManuelleX, 8f + _rotationManuelleY, 4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetCeinturePoches)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotCeinture102(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureCeinture102) ? (int)_objetEnMain.GetMeta(MetaSignatureCeinture102).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleCeinturePoches(_objetEnMain, main, 0.38f);
                _objetEnMain.SetMeta(MetaSignatureCeinture102, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-10f + _rotationManuelleX, 18f + _rotationManuelleY, 2f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetPochetteTier0)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotPochette103(main);
            int prev = _objetEnMain.HasMeta(MetaSignaturePochette103) ? (int)_objetEnMain.GetMeta(MetaSignaturePochette103).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModelePochetteTier0(_objetEnMain, main, 0.35f);
                _objetEnMain.SetMeta(MetaSignaturePochette103, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-9f + _rotationManuelleX, 16f + _rotationManuelleY, 1f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetSacTier0)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            if (_objetEnMain.HasMeta(MetaSignaturePochette103))
                _objetEnMain.RemoveMeta(MetaSignaturePochette103);
            int sig = SignatureSlotSac101(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureSac101) ? (int)_objetEnMain.GetMeta(MetaSignatureSac101).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleSacTier0(_objetEnMain, main, 0.38f);
                _objetEnMain.SetMeta(MetaSignatureSac101, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-11f + _rotationManuelleX, 20f + _rotationManuelleY, 3f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 200)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotAtelier200(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureAtelier200) ? (int)_objetEnMain.GetMeta(MetaSignatureAtelier200).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleAtelierPrimitif(_objetEnMain, main);
                _objetEnMain.SetMeta(MetaSignatureAtelier200, sig);
            }
            _objetEnMain.Scale = Vector3.One * 0.35f;
            _objetEnMain.RotationDegrees = new Vector3(0 + _rotationManuelleX, 90 + _rotationManuelleY, 0 + _rotationManuelleZ);
            return;
        }
        NettoyerModelesEnfants(_objetEnMain);
        if (_objetEnMain.HasMeta(MetaSignatureDague105))
            _objetEnMain.RemoveMeta(MetaSignatureDague105);
        if (_objetEnMain.HasMeta(MetaSignatureHachette106))
            _objetEnMain.RemoveMeta(MetaSignatureHachette106);
        if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
            _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
        if (_objetEnMain.HasMeta(MetaSignatureCorde20))
            _objetEnMain.RemoveMeta(MetaSignatureCorde20);
        if (_objetEnMain.HasMeta(MetaSignatureTissu21))
            _objetEnMain.RemoveMeta(MetaSignatureTissu21);
        if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
            _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
        if (_objetEnMain.HasMeta(MetaSignaturePochette103))
            _objetEnMain.RemoveMeta(MetaSignaturePochette103);
        if (_objetEnMain.HasMeta(MetaSignatureSac101))
            _objetEnMain.RemoveMeta(MetaSignatureSac101);
        int idxMorphMain = main.IndexMorphologique;
        Mesh m = main.EstUnEclat ? main.MeshEclat : ObtenirMeshDepuisCache(main.ID, idxMorphMain, main.IndexTaille);
        _objetEnMain.Mesh = m;
        if (main.ID == 30 || main.ID == 32)
        {
            _objetEnMain.Scale = Vector3.One * 0.38f;
            _objetEnMain.RotationDegrees = new Vector3(15f + _rotationManuelleX, 55f + _rotationManuelleY, -25f + _rotationManuelleZ);
        }
        else if (ItemPhysique.EstIdRocheMatiere(main.ID))
        {
            Vector3 sf = ItemPhysique.EchelleMorphologieRoche(main.IndexMorphologique);
            _objetEnMain.Scale = sf * 0.5f;
            _objetEnMain.RotationDegrees = new Vector3(-15 + _rotationManuelleX, 10 + _rotationManuelleY, 5 + _rotationManuelleZ);
        }
        else
        {
            _objetEnMain.Scale = Vector3.One * 0.5f;
            _objetEnMain.RotationDegrees = new Vector3(-15 + _rotationManuelleX, 10 + _rotationManuelleY, 5 + _rotationManuelleZ);
        }
        if (main.EstUnEclat)
        {
            if (ItemPhysique.EstIdRocheMatiere(main.ID))
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0);
            else if (main.ID == 30 || main.ID == 32)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, main.IndexMorphologique, 0);
            else if (main.ID >= 1 && main.ID <= 9)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0);
            else
                _objetEnMain.MaterialOverride = null;
        }
        else if (m != null)
        {
            int morphMat = main.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? main.IndexMorphologique
                : (main.ID == 30 || main.ID == 32) ? main.IndexMorphologique : 0;
            int tresMat = main.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? main.NiveauFracture : 0;
            AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, morphMat, tresMat);
        }
    }

    /// <summary>Assigne le Mesh exact au SubViewport de chaque slot (pierre en 3D dans l'UI).</summary>
    private void MettreAJourPreviewsSlots()
    {
        MettreAJourPreviewSlot(_meshPreviewGauche, MainGauche);
        MettreAJourPreviewSlot(_meshPreviewDroite, MainDroite);
    }

    private void MettreAJourPreviewSlot(MeshInstance3D meshNode, SlotInventaire slot)
    {
        if (slot.EstVide || !EstObjetAvecVisuel(slot.ID))
        {
            NettoyerModelesEnfants(meshNode);
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            if (meshNode.HasMeta(MetaSignaturePochette103))
                meshNode.RemoveMeta(MetaSignaturePochette103);
            if (meshNode.HasMeta(MetaSignatureSac101))
                meshNode.RemoveMeta(MetaSignatureSac101);
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            return;
        }
        if (slot.ID == 105)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotDague105(slot);
            int prev = meshNode.HasMeta(MetaSignatureDague105) ? (int)meshNode.GetMeta(MetaSignatureDague105).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                // Moitié de l’échelle précédente (0,6 → 0,3) dans les slots HUD / menu anatomie.
                InstancierModeleArme(meshNode, slot, 0.3f, ObtenirFacteurEchelleLameDague(slot));
                meshNode.SetMeta(MetaSignatureDague105, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(20f, 45f, -20f);
            return;
        }
        if (slot.ID == 106)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotHachette106(slot);
            int prev = meshNode.HasMeta(MetaSignatureHachette106) ? (int)meshNode.GetMeta(MetaSignatureHachette106).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleArme(meshNode, slot, 0.34f, 1f);
                meshNode.SetMeta(MetaSignatureHachette106, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(22f, 40f, -18f);
            return;
        }
        if (slot.ID == 20)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotCorde20(slot);
            int prev = meshNode.HasMeta(MetaSignatureCorde20) ? (int)meshNode.GetMeta(MetaSignatureCorde20).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleCordeTier0Gazon(meshNode, slot, 0.32f);
                meshNode.SetMeta(MetaSignatureCorde20, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(12f, 35f, -8f);
            return;
        }
        if (slot.ID == 21)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotTissu21(slot);
            int prev = meshNode.HasMeta(MetaSignatureTissu21) ? (int)meshNode.GetMeta(MetaSignatureTissu21).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleTissuTier0(meshNode, slot, 0.3f);
                meshNode.SetMeta(MetaSignatureTissu21, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(10f, 32f, -6f);
            return;
        }
        if (slot.ID == IdObjetCeinturePoches)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            int sig = SignatureSlotCeinture102(slot);
            int prev = meshNode.HasMeta(MetaSignatureCeinture102) ? (int)meshNode.GetMeta(MetaSignatureCeinture102).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleCeinturePoches(meshNode, slot, 0.32f);
                meshNode.SetMeta(MetaSignatureCeinture102, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(8f, 28f, -4f);
            return;
        }
        if (slot.ID == IdObjetPochetteTier0)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotPochette103(slot);
            int prev = meshNode.HasMeta(MetaSignaturePochette103) ? (int)meshNode.GetMeta(MetaSignaturePochette103).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModelePochetteTier0(meshNode, slot, 0.3f);
                meshNode.SetMeta(MetaSignaturePochette103, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(7f, 24f, -3f);
            return;
        }
        if (slot.ID == IdObjetSacTier0)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            if (meshNode.HasMeta(MetaSignaturePochette103))
                meshNode.RemoveMeta(MetaSignaturePochette103);
            int sig = SignatureSlotSac101(slot);
            int prev = meshNode.HasMeta(MetaSignatureSac101) ? (int)meshNode.GetMeta(MetaSignatureSac101).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleSacTier0(meshNode, slot, 0.33f);
                meshNode.SetMeta(MetaSignatureSac101, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(8f, 30f, -4f);
            return;
        }
        if (slot.ID == 200)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            int sig = SignatureSlotAtelier200(slot);
            int prev = meshNode.HasMeta(MetaSignatureAtelier200) ? (int)meshNode.GetMeta(MetaSignatureAtelier200).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleAtelierPrimitif(meshNode, slot);
                meshNode.SetMeta(MetaSignatureAtelier200, sig);
            }
            meshNode.Scale = Vector3.One * 0.8f;
            meshNode.RotationDegrees = new Vector3(0f, 45f, 0f);
            return;
        }
        NettoyerModelesEnfants(meshNode);
        if (meshNode.HasMeta(MetaSignatureDague105))
            meshNode.RemoveMeta(MetaSignatureDague105);
        if (meshNode.HasMeta(MetaSignatureHachette106))
            meshNode.RemoveMeta(MetaSignatureHachette106);
        if (meshNode.HasMeta(MetaSignatureAtelier200))
            meshNode.RemoveMeta(MetaSignatureAtelier200);
        if (meshNode.HasMeta(MetaSignatureCorde20))
            meshNode.RemoveMeta(MetaSignatureCorde20);
        if (meshNode.HasMeta(MetaSignatureTissu21))
            meshNode.RemoveMeta(MetaSignatureTissu21);
        if (meshNode.HasMeta(MetaSignatureCeinture102))
            meshNode.RemoveMeta(MetaSignatureCeinture102);
        if (meshNode.HasMeta(MetaSignaturePochette103))
            meshNode.RemoveMeta(MetaSignaturePochette103);
        if (meshNode.HasMeta(MetaSignatureSac101))
            meshNode.RemoveMeta(MetaSignatureSac101);
        Mesh m = slot.EstUnEclat ? slot.MeshEclat : ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique, slot.IndexTaille);
        meshNode.Mesh = m;
        if (slot.ID == 30 || slot.ID == 32)
        {
            meshNode.Scale = Vector3.One * 0.72f;
            meshNode.RotationDegrees = new Vector3(68f, 18f, 0);
        }
        else if (ItemPhysique.EstIdRocheMatiere(slot.ID))
        {
            meshNode.Scale = ItemPhysique.EchelleMorphologieRoche(slot.IndexMorphologique) * 0.85f;
            meshNode.RotationDegrees = Vector3.Zero;
        }
        else
        {
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = Vector3.Zero;
        }
        if (slot.EstUnEclat)
        {
            if (ItemPhysique.EstIdRocheMatiere(slot.ID))
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
            else if (slot.ID == 30 || slot.ID == 32)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, slot.IndexMorphologique, 0);
            else if (slot.ID >= 1 && slot.ID <= 9)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
            else
                meshNode.MaterialOverride = null;
        }
        else if (m != null)
        {
            int morphMat = slot.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? slot.IndexMorphologique
                : (slot.ID == 30 || slot.ID == 32) ? slot.IndexMorphologique : 0;
            int tresMat = slot.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? slot.NiveauFracture : 0;
            AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, morphMat, tresMat);
        }
    }

    /// <summary>True si le slot doit afficher un mesh 3D dans l’UI (HUD ou menu anatomie).</summary>
    public bool InventaireSlotAunVisuel3D(SlotInventaire s) => !s.EstVide && EstObjetAvecVisuel(s.ID);

    /// <summary>Même rendu que les previews HUD, pour les panels G/D du menu anatomie.</summary>
    public void SynchroniserPreviewSlotMenu(MeshInstance3D meshNode, SlotInventaire slot) => MettreAJourPreviewSlot(meshNode, slot);

    /// <summary>Cache le SubViewport quand pas d'objet avec visuel (pierre, fibre, corde), pour laisser voir la couleur du slot.</summary>
    private void MettreAJourVisibilitePreviews()
    {
        if (_viewportSlotGauche != null) _viewportSlotGauche.Visible = !MainGauche.EstVide && EstObjetAvecVisuel(MainGauche.ID);
        if (_viewportSlotDroite != null) _viewportSlotDroite.Visible = !MainDroite.EstVide && EstObjetAvecVisuel(MainDroite.ID);
    }

}
