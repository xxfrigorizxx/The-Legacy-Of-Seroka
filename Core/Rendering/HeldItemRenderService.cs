using Godot;
using System;

public partial class Joueur
{
    private void MettreAJourObjetEnMain()
    {
        if (_objetEnMain == null || !GodotObject.IsInstanceValid(_objetEnMain))
            return;

        var main = MainGaucheEstActive ? MainGauche : MainDroite;
        if (main.EstVide || !EstObjetAvecVisuel(main.ID))
        {
            NettoyerModelesEnfants(_objetEnMain);
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignaturePelle107))
                _objetEnMain.RemoveMeta(MetaSignaturePelle107);
            if (_objetEnMain.HasMeta(MetaSignaturePioche108))
                _objetEnMain.RemoveMeta(MetaSignaturePioche108);
            if (_objetEnMain.HasMeta(MetaSignatureLance111))
                _objetEnMain.RemoveMeta(MetaSignatureLance111);
            if (_objetEnMain.HasMeta(MetaSignatureFaux112))
                _objetEnMain.RemoveMeta(MetaSignatureFaux112);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
            if (_objetEnMain.HasMeta(MetaSignaturePochette103))
                _objetEnMain.RemoveMeta(MetaSignaturePochette103);
            if (_objetEnMain.HasMeta(MetaSignatureSac101))
                _objetEnMain.RemoveMeta(MetaSignatureSac101);
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            return;
        }
        if (main.ID == 105 || main.ID == IdObjetFauxPierreTier0)
        {
            bool estFaux = main.ID == IdObjetFauxPierreTier0;
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureHachette106))
                _objetEnMain.RemoveMeta(MetaSignatureHachette106);
            if (_objetEnMain.HasMeta(MetaSignaturePelle107))
                _objetEnMain.RemoveMeta(MetaSignaturePelle107);
            if (_objetEnMain.HasMeta(MetaSignaturePioche108))
                _objetEnMain.RemoveMeta(MetaSignaturePioche108);
            if (_objetEnMain.HasMeta(MetaSignatureLance111))
                _objetEnMain.RemoveMeta(MetaSignatureLance111);
            if (estFaux && _objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (!estFaux && _objetEnMain.HasMeta(MetaSignatureFaux112))
                _objetEnMain.RemoveMeta(MetaSignatureFaux112);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
            int sig = estFaux ? SignatureSlotFaux112(main) : SignatureSlotDague105(main);
            string cleMeta = estFaux ? MetaSignatureFaux112 : MetaSignatureDague105;
            int prev = _objetEnMain.HasMeta(cleMeta) ? (int)_objetEnMain.GetMeta(cleMeta).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleArme(_objetEnMain, main, estFaux ? 0.36f : 0.35f, ObtenirFacteurEchelleLameDague(main));
                _objetEnMain.SetMeta(cleMeta, sig);
            }
            // +20 % vs l’ancien 0,5, puis +25 % (0,6 → 0,75) : lisibilité dague / faux en main.
            _objetEnMain.Scale = Vector3.One * (0.5f * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = new Vector3(-15f + _rotationManuelleX, 10f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 106 || main.ID == IdObjetPellePierreTier0 || main.ID == IdObjetPiochePierreTier0 || main.ID == IdObjetLancePierreTier0)
        {
            bool estPelle = main.ID == IdObjetPellePierreTier0;
            bool estPioche = main.ID == IdObjetPiochePierreTier0;
            bool estLance = main.ID == IdObjetLancePierreTier0;
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            if (_objetEnMain.HasMeta(MetaSignatureDague105))
                _objetEnMain.RemoveMeta(MetaSignatureDague105);
            if (_objetEnMain.HasMeta(MetaSignatureFaux112))
                _objetEnMain.RemoveMeta(MetaSignatureFaux112);
            if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
                _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
            if (_objetEnMain.HasMeta(MetaSignatureCorde20))
                _objetEnMain.RemoveMeta(MetaSignatureCorde20);
            if (_objetEnMain.HasMeta(MetaSignatureTissu21))
                _objetEnMain.RemoveMeta(MetaSignatureTissu21);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
            int sig = estLance ? SignatureSlotLance111(main) : (estPioche ? SignatureSlotPioche108(main) : (estPelle ? SignatureSlotPelle107(main) : SignatureSlotHachette106(main)));
            int prev = estPioche
                ? (_objetEnMain.HasMeta(MetaSignaturePioche108) ? (int)_objetEnMain.GetMeta(MetaSignaturePioche108).AsInt32() : -1)
                : (estPelle
                ? (_objetEnMain.HasMeta(MetaSignaturePelle107) ? (int)_objetEnMain.GetMeta(MetaSignaturePelle107).AsInt32() : -1)
                : (estLance
                ? (_objetEnMain.HasMeta(MetaSignatureLance111) ? (int)_objetEnMain.GetMeta(MetaSignatureLance111).AsInt32() : -1)
                : (_objetEnMain.HasMeta(MetaSignatureHachette106) ? (int)_objetEnMain.GetMeta(MetaSignatureHachette106).AsInt32() : -1)));
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleArme(_objetEnMain, main, estLance ? 0.48f : (estPioche ? 0.46f : (estPelle ? 0.44f : 0.42f)), 1f);
                _objetEnMain.SetMeta(estLance ? MetaSignatureLance111 : (estPioche ? MetaSignaturePioche108 : (estPelle ? MetaSignaturePelle107 : MetaSignatureHachette106)), sig);
            }
            _objetEnMain.Scale = Vector3.One * ((estLance ? 0.60f * 1.4f : (estPioche ? 0.56f * 1.4f : (estPelle ? 0.54f * 1.4f : 0.52f))) * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = estLance
                ? new Vector3(-15f + _rotationManuelleX, 11f + _rotationManuelleY, 3f + _rotationManuelleZ)
                : estPioche
                ? new Vector3(-20f + _rotationManuelleX, 13f + _rotationManuelleY, 5f + _rotationManuelleZ)
                : (estPelle
                ? new Vector3(-17f + _rotationManuelleX, 14f + _rotationManuelleY, 6f + _rotationManuelleZ)
                : new Vector3(-18f + _rotationManuelleX, 12f + _rotationManuelleY, 4f + _rotationManuelleZ));
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
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
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
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
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
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
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
        if (main.ID == IdObjetCeintureSacoches)
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
            int sig = SignatureSlotCeinture104(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureCeinture104) ? (int)_objetEnMain.GetMeta(MetaSignatureCeinture104).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleCeintureSacoches(_objetEnMain, main, 0.39f);
                _objetEnMain.SetMeta(MetaSignatureCeinture104, sig);
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
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
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
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
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
        if (main.ID == IdObjetCarnetSavoir)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = SignatureSlotCarnet114(main);
            int prev = _objetEnMain.HasMeta(MetaSignatureCarnet114) ? (int)_objetEnMain.GetMeta(MetaSignatureCarnet114).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleCarnetSavoir(_objetEnMain, main, 0.34f);
                _objetEnMain.SetMeta(MetaSignatureCarnet114, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(6f + _rotationManuelleX, 115f + _rotationManuelleY, -9f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetBaie)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleBaie(_objetEnMain, main, 0.16f);
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-6f + _rotationManuelleX, 22f + _rotationManuelleY, 2f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetSteakCru)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleSteakCru(_objetEnMain, main, 0.18f);
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-4f + _rotationManuelleX, 18f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetOsBoeuf)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleOsBoeuf(_objetEnMain, main, 0.28f);
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(8f + _rotationManuelleX, 24f + _rotationManuelleY, -12f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetCuirBoeuf)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = (main.GenomeAssemblage ?? "").GetHashCode();
            int prev = _objetEnMain.HasMeta(MetaSignatureLootCuir117) ? (int)_objetEnMain.GetMeta(MetaSignatureLootCuir117).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleCuirBoeuf(_objetEnMain, main, 0.24f);
                _objetEnMain.SetMeta(MetaSignatureLootCuir117, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(2f + _rotationManuelleX, 40f + _rotationManuelleY, -6f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetIntestinBoeuf)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleIntestinBoeuf(_objetEnMain, main, 0.22f);
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(6f + _rotationManuelleX, 30f + _rotationManuelleY, -4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetIntestinBoeufNettoye)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleIntestinBoeufNettoye(_objetEnMain, main, 0.22f);
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(6f + _rotationManuelleX, 30f + _rotationManuelleY, -4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetAllumeFeu)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexChimique, Mathf.RoundToInt(main.DurabiliteOutilActuelle), Mathf.RoundToInt(main.DurabiliteOutilMax));
            int prev = _objetEnMain.HasMeta(MetaSignatureAllumeFeu121) ? (int)_objetEnMain.GetMeta(MetaSignatureAllumeFeu121).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleAllumeFeu(_objetEnMain, main, 0.34f, false);
                _objetEnMain.SetMeta(MetaSignatureAllumeFeu121, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-12f + _rotationManuelleX, 58f + _rotationManuelleY, 4f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 10 || main.ID == 11)
        {
            NettoyerModelesEnfants(_objetEnMain);
            _objetEnMain.Mesh = ObtenirMeshDepuisCache(main.ID, main.IndexMorphologique, main.IndexTaille);
            _objetEnMain.MaterialOverride = null;
            _objetEnMain.Scale = Vector3.One * 0.0048f;
            _objetEnMain.RotationDegrees = new Vector3(-6f + _rotationManuelleX, 25f + _rotationManuelleY, 0f + _rotationManuelleZ);
            return;
        }
        if (main.ID == 200 || main.ID == IdObjetRackBatons || main.ID == IdObjetRackBuches || main.ID == IdObjetCoffreBoisTier0 || main.ID == IdObjetPitFeu)
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
            if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
                _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
            if (_objetEnMain.HasMeta(MetaSignatureRack109))
                _objetEnMain.RemoveMeta(MetaSignatureRack109);
            if (_objetEnMain.HasMeta(MetaSignatureCoffre113))
                _objetEnMain.RemoveMeta(MetaSignatureCoffre113);
            if (_objetEnMain.HasMeta(MetaSignaturePitFeu120))
                _objetEnMain.RemoveMeta(MetaSignaturePitFeu120);
            int sig = main.ID == 200
                ? SignatureSlotAtelier200(main)
                : (main.ID == IdObjetCoffreBoisTier0
                ? SignatureSlotCoffre113(main)
                : (main.ID == IdObjetPitFeu
                ? HashCode.Combine(main.ID, main.IndexBotanique, main.IndexChimique, main.IndexMorphologique, main.NiveauFracture)
                : SignatureSlotRack109(main)));
            string cleSig = main.ID == 200
                ? MetaSignatureAtelier200
                : (main.ID == IdObjetCoffreBoisTier0
                ? MetaSignatureCoffre113
                : (main.ID == IdObjetPitFeu ? MetaSignaturePitFeu120 : MetaSignatureRack109));
            int prev = _objetEnMain.HasMeta(cleSig) ? (int)_objetEnMain.GetMeta(cleSig).AsInt32() : -1;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                if (main.ID == 200) InstancierModeleAtelierPrimitif(_objetEnMain, main);
                else if (main.ID == IdObjetRackBatons) InstancierModeleRackBatons(_objetEnMain, main);
                else if (main.ID == IdObjetRackBuches) InstancierModeleRackBuches(_objetEnMain, main);
                else if (main.ID == IdObjetCoffreBoisTier0) InstancierModeleCoffreBoisTier0(_objetEnMain, main, 0.72f, false);
                else InstancierModelePitFeu(_objetEnMain, main, 0.66f, false);
                _objetEnMain.SetMeta(cleSig, sig);
            }
            _objetEnMain.Scale = Vector3.One * (main.ID == 200 ? 0.35f : (main.ID == IdObjetCoffreBoisTier0 ? 0.38f : (main.ID == IdObjetPitFeu ? 0.40f : 0.42f)));
            _objetEnMain.RotationDegrees = new Vector3(0 + _rotationManuelleX, 90 + _rotationManuelleY, 0 + _rotationManuelleZ);
            return;
        }
        NettoyerModelesEnfants(_objetEnMain);
        if (_objetEnMain.HasMeta(MetaSignatureDague105))
            _objetEnMain.RemoveMeta(MetaSignatureDague105);
        if (_objetEnMain.HasMeta(MetaSignatureHachette106))
            _objetEnMain.RemoveMeta(MetaSignatureHachette106);
        if (_objetEnMain.HasMeta(MetaSignatureLance111))
            _objetEnMain.RemoveMeta(MetaSignatureLance111);
        if (_objetEnMain.HasMeta(MetaSignatureAtelier200))
            _objetEnMain.RemoveMeta(MetaSignatureAtelier200);
        if (_objetEnMain.HasMeta(MetaSignatureRack109))
            _objetEnMain.RemoveMeta(MetaSignatureRack109);
        if (_objetEnMain.HasMeta(MetaSignatureCoffre113))
            _objetEnMain.RemoveMeta(MetaSignatureCoffre113);
        if (_objetEnMain.HasMeta(MetaSignaturePitFeu120))
            _objetEnMain.RemoveMeta(MetaSignaturePitFeu120);
        if (_objetEnMain.HasMeta(MetaSignatureAllumeFeu121))
            _objetEnMain.RemoveMeta(MetaSignatureAllumeFeu121);
        if (_objetEnMain.HasMeta(MetaSignatureCorde20))
            _objetEnMain.RemoveMeta(MetaSignatureCorde20);
        if (_objetEnMain.HasMeta(MetaSignatureTissu21))
            _objetEnMain.RemoveMeta(MetaSignatureTissu21);
        if (_objetEnMain.HasMeta(MetaSignatureCeinture102))
            _objetEnMain.RemoveMeta(MetaSignatureCeinture102);
        if (_objetEnMain.HasMeta(MetaSignatureCeinture104))
            _objetEnMain.RemoveMeta(MetaSignatureCeinture104);
        if (_objetEnMain.HasMeta(MetaSignaturePochette103))
            _objetEnMain.RemoveMeta(MetaSignaturePochette103);
        if (_objetEnMain.HasMeta(MetaSignatureSac101))
            _objetEnMain.RemoveMeta(MetaSignatureSac101);
        int idxMorphMain = main.IndexMorphologique;
        Mesh m = main.EstUnEclat ? main.MeshEclat : ObtenirMeshDepuisCache(main.ID, idxMorphMain, main.IndexTaille);
        _objetEnMain.Mesh = m;
        if (main.ID == 30 || main.ID == 32 || (main.ID == BlocChutant.ID_BRANCHE && main.IndexMorphologique != 1))
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
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0, main.IndexBotanique);
            else if (main.ID == 30 || main.ID == 32 || main.ID == BlocChutant.ID_BRANCHE)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, main.IndexMorphologique, 0, main.IndexBotanique);
            else if (main.ID >= 1 && main.ID <= 9)
                AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, 0, 0, main.IndexBotanique);
            else
                _objetEnMain.MaterialOverride = null;
        }
        else if (m != null)
        {
            int morphMat = main.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetCeintureSacoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? main.IndexMorphologique
                : (main.ID == 30 || main.ID == 32 || main.ID == BlocChutant.ID_BRANCHE) ? main.IndexMorphologique : 0;
            int tresMat = main.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetCeintureSacoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? main.NiveauFracture : 0;
            AppliquerMaterielObjet(_objetEnMain, main.ID, main.IndexChimique, morphMat, tresMat, main.IndexBotanique);
        }
    }

    /// <summary>Assigne le Mesh exact au SubViewport de chaque slot (pierre en 3D dans l'UI).</summary>
    private void MettreAJourPreviewsSlots()
    {
        MettreAJourPreviewSlot(_meshPreviewGauche, MainGauche);
        MettreAJourPreviewSlot(_meshPreviewDroite, MainDroite);
        MettreAJourPreviewSlot(_meshPreviewCarnet, EquipementCarnet);
    }

    private void MettreAJourPreviewSlot(MeshInstance3D meshNode, SlotInventaire slot)
    {
        if (meshNode == null || !GodotObject.IsInstanceValid(meshNode))
            return;
        if (slot.EstVide || !EstObjetAvecVisuel(slot.ID))
        {
            NettoyerModelesEnfants(meshNode);
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignatureLance111))
                meshNode.RemoveMeta(MetaSignatureLance111);
            if (meshNode.HasMeta(MetaSignatureFaux112))
                meshNode.RemoveMeta(MetaSignatureFaux112);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
            if (meshNode.HasMeta(MetaSignaturePochette103))
                meshNode.RemoveMeta(MetaSignaturePochette103);
            if (meshNode.HasMeta(MetaSignatureSac101))
                meshNode.RemoveMeta(MetaSignatureSac101);
            if (meshNode.HasMeta(MetaSignatureCarnet114))
                meshNode.RemoveMeta(MetaSignatureCarnet114);
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            return;
        }
        if (slot.ID == 105 || slot.ID == IdObjetFauxPierreTier0)
        {
            bool estFaux = slot.ID == IdObjetFauxPierreTier0;
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureHachette106))
                meshNode.RemoveMeta(MetaSignatureHachette106);
            if (meshNode.HasMeta(MetaSignaturePelle107))
                meshNode.RemoveMeta(MetaSignaturePelle107);
            if (meshNode.HasMeta(MetaSignaturePioche108))
                meshNode.RemoveMeta(MetaSignaturePioche108);
            if (meshNode.HasMeta(MetaSignatureLance111))
                meshNode.RemoveMeta(MetaSignatureLance111);
            if (estFaux && meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (!estFaux && meshNode.HasMeta(MetaSignatureFaux112))
                meshNode.RemoveMeta(MetaSignatureFaux112);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
            int sig = estFaux ? SignatureSlotFaux112(slot) : SignatureSlotDague105(slot);
            string cleMeta = estFaux ? MetaSignatureFaux112 : MetaSignatureDague105;
            int prev = meshNode.HasMeta(cleMeta) ? (int)meshNode.GetMeta(cleMeta).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                // Moitié de l’échelle précédente (0,6 → 0,3) dans les slots HUD / menu anatomie.
                InstancierModeleArme(meshNode, slot, estFaux ? 0.305f : 0.3f, ObtenirFacteurEchelleLameDague(slot));
                meshNode.SetMeta(cleMeta, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(20f, 45f, -20f);
            return;
        }
        if (slot.ID == 106 || slot.ID == IdObjetPellePierreTier0 || slot.ID == IdObjetPiochePierreTier0 || slot.ID == IdObjetLancePierreTier0)
        {
            bool estPelle = slot.ID == IdObjetPellePierreTier0;
            bool estPioche = slot.ID == IdObjetPiochePierreTier0;
            bool estLance = slot.ID == IdObjetLancePierreTier0;
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            if (meshNode.HasMeta(MetaSignatureDague105))
                meshNode.RemoveMeta(MetaSignatureDague105);
            if (meshNode.HasMeta(MetaSignatureFaux112))
                meshNode.RemoveMeta(MetaSignatureFaux112);
            if (meshNode.HasMeta(MetaSignatureAtelier200))
                meshNode.RemoveMeta(MetaSignatureAtelier200);
            if (meshNode.HasMeta(MetaSignatureCorde20))
                meshNode.RemoveMeta(MetaSignatureCorde20);
            if (meshNode.HasMeta(MetaSignatureTissu21))
                meshNode.RemoveMeta(MetaSignatureTissu21);
            if (meshNode.HasMeta(MetaSignatureCeinture102))
                meshNode.RemoveMeta(MetaSignatureCeinture102);
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
            int sig = estLance ? SignatureSlotLance111(slot) : (estPioche ? SignatureSlotPioche108(slot) : (estPelle ? SignatureSlotPelle107(slot) : SignatureSlotHachette106(slot)));
            int prev = estPioche
                ? (meshNode.HasMeta(MetaSignaturePioche108) ? (int)meshNode.GetMeta(MetaSignaturePioche108).AsInt32() : -1)
                : (estPelle
                ? (meshNode.HasMeta(MetaSignaturePelle107) ? (int)meshNode.GetMeta(MetaSignaturePelle107).AsInt32() : -1)
                : (estLance
                ? (meshNode.HasMeta(MetaSignatureLance111) ? (int)meshNode.GetMeta(MetaSignatureLance111).AsInt32() : -1)
                : (meshNode.HasMeta(MetaSignatureHachette106) ? (int)meshNode.GetMeta(MetaSignatureHachette106).AsInt32() : -1)));
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleArme(meshNode, slot, estLance ? 0.37f : (estPioche ? 0.36f : (estPelle ? 0.35f : 0.34f)), 1f);
                meshNode.SetMeta(estLance ? MetaSignatureLance111 : (estPioche ? MetaSignaturePioche108 : (estPelle ? MetaSignaturePelle107 : MetaSignatureHachette106)), sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = estLance ? new Vector3(18f, 44f, -14f) : (estPioche ? new Vector3(22f, 42f, -18f) : (estPelle ? new Vector3(20f, 38f, -16f) : new Vector3(22f, 40f, -18f)));
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
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
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
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
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
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
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
        if (slot.ID == IdObjetCeintureSacoches)
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
            int sig = SignatureSlotCeinture104(slot);
            int prev = meshNode.HasMeta(MetaSignatureCeinture104) ? (int)meshNode.GetMeta(MetaSignatureCeinture104).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleCeintureSacoches(meshNode, slot, 0.33f);
                meshNode.SetMeta(MetaSignatureCeinture104, sig);
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
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
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
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
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
        if (slot.ID == IdObjetCarnetSavoir)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = SignatureSlotCarnet114(slot);
            int prev = meshNode.HasMeta(MetaSignatureCarnet114) ? (int)meshNode.GetMeta(MetaSignatureCarnet114).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleCarnetSavoir(meshNode, slot, 0.30f);
                meshNode.SetMeta(MetaSignatureCarnet114, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(10f, 44f, -6f);
            return;
        }
        if (slot.ID == IdObjetBaie)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleBaie(meshNode, slot, 0.14f);
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(0f, 30f, 0f);
            return;
        }
        if (slot.ID == IdObjetSteakCru)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleSteakCru(meshNode, slot, 0.16f);
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-2f, 22f, 4f);
            return;
        }
        if (slot.ID == IdObjetOsBoeuf)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleOsBoeuf(meshNode, slot, 0.252f);
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(6f, 28f, -10f);
            return;
        }
        if (slot.ID == IdObjetCuirBoeuf)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = (slot.GenomeAssemblage ?? "").GetHashCode();
            int prev = meshNode.HasMeta(MetaSignatureLootCuir117) ? (int)meshNode.GetMeta(MetaSignatureLootCuir117).AsInt32() : int.MinValue;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleCuirBoeuf(meshNode, slot, 0.216f);
                meshNode.SetMeta(MetaSignatureLootCuir117, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(4f, 36f, -4f);
            return;
        }
        if (slot.ID == IdObjetIntestinBoeuf)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleIntestinBoeuf(meshNode, slot, 0.2f);
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(8f, 30f, -2f);
            return;
        }
        if (slot.ID == IdObjetIntestinBoeufNettoye)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleIntestinBoeufNettoye(meshNode, slot, 0.2f);
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(8f, 30f, -2f);
            return;
        }
        if (slot.ID == IdObjetAllumeFeu)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexChimique, Mathf.RoundToInt(slot.DurabiliteOutilActuelle), Mathf.RoundToInt(slot.DurabiliteOutilMax));
            int prev = meshNode.HasMeta(MetaSignatureAllumeFeu121) ? (int)meshNode.GetMeta(MetaSignatureAllumeFeu121).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleAllumeFeu(meshNode, slot, 0.30f, false);
                meshNode.SetMeta(MetaSignatureAllumeFeu121, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-8f, 64f, 8f);
            return;
        }
        if (slot.ID == 10 || slot.ID == 11)
        {
            NettoyerModelesEnfants(meshNode);
            meshNode.Mesh = ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique, slot.IndexTaille);
            meshNode.MaterialOverride = null;
            meshNode.Scale = Vector3.One * 0.0044f;
            meshNode.RotationDegrees = new Vector3(0f, 28f, 0f);
            return;
        }
        if (slot.ID == 200 || slot.ID == IdObjetRackBatons || slot.ID == IdObjetRackBuches || slot.ID == IdObjetCoffreBoisTier0 || slot.ID == IdObjetPitFeu)
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
            if (meshNode.HasMeta(MetaSignatureCeinture104))
                meshNode.RemoveMeta(MetaSignatureCeinture104);
            if (meshNode.HasMeta(MetaSignatureRack109))
                meshNode.RemoveMeta(MetaSignatureRack109);
            if (meshNode.HasMeta(MetaSignatureCoffre113))
                meshNode.RemoveMeta(MetaSignatureCoffre113);
            if (meshNode.HasMeta(MetaSignaturePitFeu120))
                meshNode.RemoveMeta(MetaSignaturePitFeu120);
            int sig = slot.ID == 200
                ? SignatureSlotAtelier200(slot)
                : (slot.ID == IdObjetCoffreBoisTier0
                ? SignatureSlotCoffre113(slot)
                : (slot.ID == IdObjetPitFeu
                ? HashCode.Combine(slot.ID, slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture)
                : SignatureSlotRack109(slot)));
            string cleSig = slot.ID == 200
                ? MetaSignatureAtelier200
                : (slot.ID == IdObjetCoffreBoisTier0
                ? MetaSignatureCoffre113
                : (slot.ID == IdObjetPitFeu ? MetaSignaturePitFeu120 : MetaSignatureRack109));
            int prev = meshNode.HasMeta(cleSig) ? (int)meshNode.GetMeta(cleSig).AsInt32() : -1;
            bool manque = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                if (slot.ID == 200) InstancierModeleAtelierPrimitif(meshNode, slot);
                else if (slot.ID == IdObjetRackBatons) InstancierModeleRackBatons(meshNode, slot);
                else if (slot.ID == IdObjetRackBuches) InstancierModeleRackBuches(meshNode, slot);
                else if (slot.ID == IdObjetCoffreBoisTier0) InstancierModeleCoffreBoisTier0(meshNode, slot, 0.78f, false);
                else InstancierModelePitFeu(meshNode, slot, 0.76f, false);
                meshNode.SetMeta(cleSig, sig);
            }
            meshNode.Scale = Vector3.One * (slot.ID == 200 ? 0.8f : (slot.ID == IdObjetCoffreBoisTier0 ? 0.82f : (slot.ID == IdObjetPitFeu ? 0.86f : 0.92f)));
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
        if (meshNode.HasMeta(MetaSignatureRack109))
            meshNode.RemoveMeta(MetaSignatureRack109);
        if (meshNode.HasMeta(MetaSignatureCoffre113))
            meshNode.RemoveMeta(MetaSignatureCoffre113);
        if (meshNode.HasMeta(MetaSignaturePitFeu120))
            meshNode.RemoveMeta(MetaSignaturePitFeu120);
        if (meshNode.HasMeta(MetaSignatureAllumeFeu121))
            meshNode.RemoveMeta(MetaSignatureAllumeFeu121);
        if (meshNode.HasMeta(MetaSignatureCorde20))
            meshNode.RemoveMeta(MetaSignatureCorde20);
        if (meshNode.HasMeta(MetaSignatureTissu21))
            meshNode.RemoveMeta(MetaSignatureTissu21);
        if (meshNode.HasMeta(MetaSignatureCeinture102))
            meshNode.RemoveMeta(MetaSignatureCeinture102);
        if (meshNode.HasMeta(MetaSignatureCeinture104))
            meshNode.RemoveMeta(MetaSignatureCeinture104);
        if (meshNode.HasMeta(MetaSignaturePochette103))
            meshNode.RemoveMeta(MetaSignaturePochette103);
        if (meshNode.HasMeta(MetaSignatureSac101))
            meshNode.RemoveMeta(MetaSignatureSac101);
        Mesh m = slot.EstUnEclat ? slot.MeshEclat : ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique, slot.IndexTaille);
        meshNode.Mesh = m;
        if (slot.ID == 30 || slot.ID == 32 || slot.ID == BlocChutant.ID_BRANCHE)
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
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0, slot.IndexBotanique);
            else if (slot.ID == 30 || slot.ID == 32 || slot.ID == BlocChutant.ID_BRANCHE)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, slot.IndexMorphologique, 0, slot.IndexBotanique);
            else if (slot.ID >= 1 && slot.ID <= 9)
                AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0, slot.IndexBotanique);
            else
                meshNode.MaterialOverride = null;
        }
        else if (m != null)
        {
            int morphMat = slot.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetCeintureSacoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? slot.IndexMorphologique
                : (slot.ID == 30 || slot.ID == 32 || slot.ID == BlocChutant.ID_BRANCHE) ? slot.IndexMorphologique : 0;
            int tresMat = slot.ID is 20 or 21 or IdObjetCeinturePoches or IdObjetCeintureSacoches or IdObjetPochetteTier0 or IdObjetSacTier0 ? slot.NiveauFracture : 0;
            AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, morphMat, tresMat, slot.IndexBotanique);
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
        if (_viewportSlotCarnet != null) _viewportSlotCarnet.Visible = !EquipementCarnet.EstVide && EstObjetAvecVisuel(EquipementCarnet.ID);
    }

}
