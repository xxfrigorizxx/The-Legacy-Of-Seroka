using Godot;
using System;

public partial class Joueur
{
    private static bool ModeleArmeAbsent(Node parent)
    {
        if (parent == null)
            return true;
        if (parent.GetNodeOrNull<Node>("ModeleArme") != null)
            return false;
        return parent.FindChild("ModeleArme", true, false) == null;
    }

    private static void RetirerMetaSiDifferente(Node3D node, string cleMeta, string cleMetaCourante)
    {
        if (node == null || cleMeta == cleMetaCourante)
            return;
        if (node.HasMeta(cleMeta))
            node.RemoveMeta(cleMeta);
    }

    private static int CalculerSignatureGlobaleObjetTenu(bool mainGauche, in SlotInventaire slot)
    {
        if (slot.EstVide)
            return mainGauche ? int.MinValue : int.MinValue + 1;
        int sig = HashCode.Combine(
            slot.ID,
            slot.IndexChimique,
            slot.IndexMorphologique,
            slot.IndexTaille,
            slot.IndexBotanique,
            slot.NiveauFracture,
            slot.IndexTailleLameRoche,
            HashCode.Combine(slot.EstUnEclat ? 1 : 0, slot.GenomeAssemblage ?? "", slot.CleConteneur ?? ""));
        if (slot.EstUnEclat && slot.MeshEclat != null)
            sig = HashCode.Combine(sig, slot.MeshEclat.GetInstanceId());
        return HashCode.Combine(mainGauche ? 1 : 0, sig);
    }

    private static void RetirerToutesMetaSignaturesVisuel(Node3D node)
    {
        if (node == null)
            return;
        if (node.HasMeta(MetaSignatureDague105)) node.RemoveMeta(MetaSignatureDague105);
        if (node.HasMeta(MetaSignatureHachette106)) node.RemoveMeta(MetaSignatureHachette106);
        if (node.HasMeta(MetaSignaturePelle107)) node.RemoveMeta(MetaSignaturePelle107);
        if (node.HasMeta(MetaSignaturePioche108)) node.RemoveMeta(MetaSignaturePioche108);
        if (node.HasMeta(MetaSignatureLance111)) node.RemoveMeta(MetaSignatureLance111);
        if (node.HasMeta(MetaSignatureFaux112)) node.RemoveMeta(MetaSignatureFaux112);
        if (node.HasMeta(MetaSignatureAtelier200)) node.RemoveMeta(MetaSignatureAtelier200);
        if (node.HasMeta(MetaSignatureCorde20)) node.RemoveMeta(MetaSignatureCorde20);
        if (node.HasMeta(MetaSignatureTissu21)) node.RemoveMeta(MetaSignatureTissu21);
        if (node.HasMeta(MetaSignatureCeinture102)) node.RemoveMeta(MetaSignatureCeinture102);
        if (node.HasMeta(MetaSignatureCeinture104)) node.RemoveMeta(MetaSignatureCeinture104);
        if (node.HasMeta(MetaSignaturePochette103)) node.RemoveMeta(MetaSignaturePochette103);
        if (node.HasMeta(MetaSignatureSac101)) node.RemoveMeta(MetaSignatureSac101);
        if (node.HasMeta(MetaSignatureRack109)) node.RemoveMeta(MetaSignatureRack109);
        if (node.HasMeta(MetaSignatureCoffre113)) node.RemoveMeta(MetaSignatureCoffre113);
        if (node.HasMeta(MetaSignaturePitFeu120)) node.RemoveMeta(MetaSignaturePitFeu120);
        if (node.HasMeta(MetaSignaturePitFeuRoche122)) node.RemoveMeta(MetaSignaturePitFeuRoche122);
        if (node.HasMeta(MetaSignatureFondation)) node.RemoveMeta(MetaSignatureFondation);
        if (node.HasMeta(MetaSignatureAllumeFeu121)) node.RemoveMeta(MetaSignatureAllumeFeu121);
        if (node.HasMeta(MetaSignatureMailletBois128)) node.RemoveMeta(MetaSignatureMailletBois128);
        if (node.HasMeta(MetaSignatureBolBois129)) node.RemoveMeta(MetaSignatureBolBois129);
        if (node.HasMeta(MetaSignatureMortierPilon130)) node.RemoveMeta(MetaSignatureMortierPilon130);
        if (node.HasMeta(MetaSignatureFenetreBois146)) node.RemoveMeta(MetaSignatureFenetreBois146);
        if (node.HasMeta(MetaSignatureAtelleJambe133)) node.RemoveMeta(MetaSignatureAtelleJambe133);
        if (node.HasMeta(MetaSignatureAtelleBras134)) node.RemoveMeta(MetaSignatureAtelleBras134);
        if (node.HasMeta(MetaSignatureBandageTier1135)) node.RemoveMeta(MetaSignatureBandageTier1135);
        if (node.HasMeta(MetaSignatureCarnet114)) node.RemoveMeta(MetaSignatureCarnet114);
        if (node.HasMeta(MetaSignatureBaie35)) node.RemoveMeta(MetaSignatureBaie35);
        if (node.HasMeta(MetaSignatureLootCuir117)) node.RemoveMeta(MetaSignatureLootCuir117);
        if (node.HasMeta(MetaSignatureTableAnalyse131)) node.RemoveMeta(MetaSignatureTableAnalyse131);
    }

    private void InvaliderCacheVisuelObjetEnMain()
    {
        if (_objetEnMain == null || !GodotObject.IsInstanceValid(_objetEnMain))
            return;
        NettoyerModelesEnfants(_objetEnMain);
        RetirerToutesMetaSignaturesVisuel(_objetEnMain);
        _objetEnMain.Mesh = null;
        _objetEnMain.MaterialOverride = null;
    }

    private void MettreAJourObjetEnMain()
    {
        if (_objetEnMain == null || !GodotObject.IsInstanceValid(_objetEnMain))
            return;

        bool mainGauche = MainGaucheEstActive;
        var main = mainGauche ? MainGauche : MainDroite;
        int sigGlobale = CalculerSignatureGlobaleObjetTenu(mainGauche, main);
        if (sigGlobale != _derniereSignatureGlobaleObjetTenu)
        {
            InvaliderCacheVisuelObjetEnMain();
            _derniereSignatureGlobaleObjetTenu = sigGlobale;
        }
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
            if (_objetEnMain.HasMeta(MetaSignatureTableAnalyse131))
                _objetEnMain.RemoveMeta(MetaSignatureTableAnalyse131);
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
            if (_objetEnMain.HasMeta(MetaSignatureMailletBois128))
                _objetEnMain.RemoveMeta(MetaSignatureMailletBois128);
            if (_objetEnMain.HasMeta(MetaSignatureBolBois129))
                _objetEnMain.RemoveMeta(MetaSignatureBolBois129);
            if (_objetEnMain.HasMeta(MetaSignatureMortierPilon130))
                _objetEnMain.RemoveMeta(MetaSignatureMortierPilon130);
            if (_objetEnMain.HasMeta(MetaSignatureAtelleJambe133))
                _objetEnMain.RemoveMeta(MetaSignatureAtelleJambe133);
            if (_objetEnMain.HasMeta(MetaSignatureAtelleBras134))
                _objetEnMain.RemoveMeta(MetaSignatureAtelleBras134);
            if (_objetEnMain.HasMeta(MetaSignatureBandageTier1135))
                _objetEnMain.RemoveMeta(MetaSignatureBandageTier1135);
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
            if (_objetEnMain.HasMeta(MetaSignatureTableAnalyse131))
                _objetEnMain.RemoveMeta(MetaSignatureTableAnalyse131);
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
        if (main.ID == 106 || main.ID == IdObjetHachePierreTier1 || main.ID == IdObjetPellePierreTier0 || main.ID == IdObjetPiochePierreTier0 || main.ID == IdObjetLancePierreTier0)
        {
            bool estHachePierre = main.ID == IdObjetHachePierreTier1;
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
                InstancierModeleArme(_objetEnMain, main, estLance ? 0.48f : (estPioche ? 0.46f : (estPelle ? 0.44f : (estHachePierre ? 0.45f : 0.42f))), 1f);
                _objetEnMain.SetMeta(estLance ? MetaSignatureLance111 : (estPioche ? MetaSignaturePioche108 : (estPelle ? MetaSignaturePelle107 : MetaSignatureHachette106)), sig);
            }
            _objetEnMain.Scale = Vector3.One * ((estLance ? 0.60f * 1.4f : (estPioche ? 0.56f * 1.4f : (estPelle ? 0.54f * 1.4f : (estHachePierre ? 0.57f * 1.4f : 0.52f)))) * 1.2f * 1.25f);
            _objetEnMain.RotationDegrees = estLance
                ? new Vector3(-15f + _rotationManuelleX, 11f + _rotationManuelleY, 3f + _rotationManuelleZ)
                : estPioche
                ? new Vector3(-20f + _rotationManuelleX, 13f + _rotationManuelleY, 5f + _rotationManuelleZ)
                : (estPelle
                ? new Vector3(-17f + _rotationManuelleX, 14f + _rotationManuelleY, 6f + _rotationManuelleZ)
                : (estHachePierre
                ? new Vector3(-18f + _rotationManuelleX, 12f + _rotationManuelleY, 4f + _rotationManuelleZ)
                : new Vector3(-18f + _rotationManuelleX, 12f + _rotationManuelleY, 4f + _rotationManuelleZ)));
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
        if (main.ID == BlocChutant.ID_FEUILLE_ARRACHEE && BlocChutant.EssenceUtiliseFeuilleGlb(main.IndexBotanique))
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleFeuilleArrachee(_objetEnMain, main, 0.2f);
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-8f + _rotationManuelleX, 28f + _rotationManuelleY, 4f + _rotationManuelleZ);
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
        if (main.ID == IdObjetSteakCuit)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            NettoyerModelesEnfants(_objetEnMain);
            InstancierModeleSteakCuit(_objetEnMain, main, 0.18f);
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
        if (main.ID == IdObjetMailletBois)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexBotanique);
            int prev = _objetEnMain.HasMeta(MetaSignatureMailletBois128) ? (int)_objetEnMain.GetMeta(MetaSignatureMailletBois128).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleMailletBois(_objetEnMain, main, 0.34f, false);
                _objetEnMain.SetMeta(MetaSignatureMailletBois128, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-8f + _rotationManuelleX, 52f + _rotationManuelleY, 6f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetFenetreBois)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexBotanique);
            int prev = _objetEnMain.HasMeta(MetaSignatureFenetreBois146) ? (int)_objetEnMain.GetMeta(MetaSignatureFenetreBois146).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleFenetreBois(_objetEnMain, main, 0.34f, false);
                _objetEnMain.SetMeta(MetaSignatureFenetreBois146, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-6f + _rotationManuelleX, 50f + _rotationManuelleY, 3f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetBolBois)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexBotanique);
            int prev = _objetEnMain.HasMeta(MetaSignatureBolBois129) ? (int)_objetEnMain.GetMeta(MetaSignatureBolBois129).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleBolBois(_objetEnMain, main, 0.28f, false);
                _objetEnMain.SetMeta(MetaSignatureBolBois129, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-6f + _rotationManuelleX, 40f + _rotationManuelleY, 2f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetMortierPilonBois)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexBotanique, main.IndexChimique, main.GenomeAssemblage ?? "");
            int prev = _objetEnMain.HasMeta(MetaSignatureMortierPilon130) ? (int)_objetEnMain.GetMeta(MetaSignatureMortierPilon130).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleMortierPilonBois(_objetEnMain, main, 0.34f, false);
                _objetEnMain.SetMeta(MetaSignatureMortierPilon130, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-5f + _rotationManuelleX, 44f + _rotationManuelleY, 3f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetAtelleJambe || main.ID == IdObjetAtelleBras)
        {
            bool estAtelleBras = main.ID == IdObjetAtelleBras;
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexBotanique, main.IndexChimique, main.IndexMorphologique, main.GenomeAssemblage ?? "");
            string cleMeta = estAtelleBras ? MetaSignatureAtelleBras134 : MetaSignatureAtelleJambe133;
            int prev = _objetEnMain.HasMeta(cleMeta) ? (int)_objetEnMain.GetMeta(cleMeta).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                if (estAtelleBras)
                    InstancierModeleAtelleBras(_objetEnMain, main, 0.34f, false);
                else
                    InstancierModeleAtelleJambe(_objetEnMain, main, 0.34f, false);
                _objetEnMain.SetMeta(cleMeta, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-8f + _rotationManuelleX, 54f + _rotationManuelleY, 5f + _rotationManuelleZ);
            return;
        }
        if (main.ID == IdObjetBandageTier1)
        {
            _objetEnMain.Mesh = null;
            _objetEnMain.MaterialOverride = null;
            int sig = HashCode.Combine(main.ID, main.IndexBotanique, main.IndexChimique, main.IndexMorphologique, main.GenomeAssemblage ?? "");
            int prev = _objetEnMain.HasMeta(MetaSignatureBandageTier1135) ? (int)_objetEnMain.GetMeta(MetaSignatureBandageTier1135).AsInt32() : int.MinValue;
            bool manqueModele = _objetEnMain.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                InstancierModeleBandageTier1(_objetEnMain, main, 0.28f, false);
                _objetEnMain.SetMeta(MetaSignatureBandageTier1135, sig);
            }
            _objetEnMain.Scale = Vector3.One;
            _objetEnMain.RotationDegrees = new Vector3(-6f + _rotationManuelleX, 48f + _rotationManuelleY, 4f + _rotationManuelleZ);
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
        if (main.ID == 200 || main.ID == IdObjetTableBoisDecorative || main.ID == IdObjetTableArtisanaTier1 || main.ID == IdObjetTableAnalyseTier1 || main.ID == IdObjetRackBatons || main.ID == IdObjetRackBuches || main.ID == IdObjetCoffreBoisTier0 || main.ID == IdObjetPitFeu || main.ID == IdObjetPitFeuRoche || EstIdFondation(main.ID) || EstIdPlancher(main.ID) || EstIdMuret(main.ID) || EstIdMurBois(main.ID) || EstIdPorteBois(main.ID) || EstIdToitChaume(main.ID) || EstIdTorche(main.ID))
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
            int sig = (main.ID == 200 || main.ID == IdObjetTableBoisDecorative || main.ID == IdObjetTableArtisanaTier1 || main.ID == IdObjetTableAnalyseTier1)
                ? SignatureSlotAtelier200(main)
                : (main.ID == IdObjetCoffreBoisTier0
                ? SignatureSlotCoffre113(main)
                : ((main.ID == IdObjetPitFeu || main.ID == IdObjetPitFeuRoche || EstIdFondation(main.ID) || EstIdPlancher(main.ID) || EstIdMuret(main.ID) || EstIdMurBois(main.ID) || EstIdPorteBois(main.ID) || EstIdToitChaume(main.ID) || EstIdTorche(main.ID))
                ? HashCode.Combine(main.ID, main.IndexBotanique, main.IndexChimique, main.IndexMorphologique, main.NiveauFracture, main.GenomeAssemblage ?? "")
                : SignatureSlotRack109(main)));
            string cleSig = (main.ID == 200 || main.ID == IdObjetTableBoisDecorative || main.ID == IdObjetTableArtisanaTier1 || main.ID == IdObjetTableAnalyseTier1)
                ? ((main.ID == 200 || main.ID == IdObjetTableBoisDecorative || main.ID == IdObjetTableArtisanaTier1) ? MetaSignatureAtelier200 : MetaSignatureTableAnalyse131)
                : (main.ID == IdObjetCoffreBoisTier0
                ? MetaSignatureCoffre113
                : (main.ID == IdObjetPitFeu ? MetaSignaturePitFeu120 : (main.ID == IdObjetPitFeuRoche ? MetaSignaturePitFeuRoche122 : (EstIdFondation(main.ID) ? MetaSignatureFondation : ((EstIdSolBois(main.ID) || EstIdMuretBois(main.ID) || EstIdMurBois(main.ID) || EstIdPorteBois(main.ID) || EstIdToitChaume(main.ID) || EstIdTorche(main.ID)) ? MetaSignatureSolBois136 : ((EstIdSolRoche(main.ID) || EstIdMuretPierre(main.ID)) ? MetaSignatureSolRoche137 : MetaSignatureRack109))))));
            int prev = _objetEnMain.HasMeta(cleSig) ? (int)_objetEnMain.GetMeta(cleSig).AsInt32() : -1;
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureAtelier200, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureTableAnalyse131, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureRack109, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureCoffre113, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignaturePitFeu120, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignaturePitFeuRoche122, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureFondation, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureSolBois136, cleSig);
            RetirerMetaSiDifferente(_objetEnMain, MetaSignatureSolRoche137, cleSig);
            bool manqueModele = ModeleArmeAbsent(_objetEnMain);
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(_objetEnMain);
                if (main.ID == 200) InstancierModeleAtelierPrimitif(_objetEnMain, main);
                else if (main.ID == IdObjetTableBoisDecorative) InstancierModeleTableBoisDecorative(_objetEnMain, main);
                else if (main.ID == IdObjetTableArtisanaTier1) InstancierModeleTableArtisanaTier1(_objetEnMain, main);
                else if (main.ID == IdObjetTableAnalyseTier1) InstancierModeleTableAnalyseTier1(_objetEnMain, main);
                else if (main.ID == IdObjetRackBatons) InstancierModeleRackBatons(_objetEnMain, main);
                else if (main.ID == IdObjetRackBuches) InstancierModeleRackBuches(_objetEnMain, main);
                else if (main.ID == IdObjetCoffreBoisTier0) InstancierModeleCoffreBoisTier0(_objetEnMain, main, 0.72f, false);
                else if (main.ID == IdObjetPitFeuRoche) InstancierModelePitFeuRoche(_objetEnMain, main, 0.70f, false);
                else if (EstIdFondation(main.ID)) InstancierModeleFondation(_objetEnMain, main, 0.74f, false);
                else if (EstIdSolBois(main.ID)) InstancierModeleSolBois(_objetEnMain, main, false);
                else if (EstIdSolRoche(main.ID)) InstancierModeleSolRoche(_objetEnMain, main, false);
                else if (EstIdMuret(main.ID)) InstancierModeleMuretBois(_objetEnMain, main, false);
                else if (EstIdMurBois(main.ID))
                {
                    if (EstIdMurBoisFenetre(main.ID)) InstancierModeleMurBoisFenetre(_objetEnMain, main, false);
                    else if (EstIdMurBoisCadrePorte(main.ID)) InstancierModeleMurBoisCadrePorte(_objetEnMain, main, false);
                    else InstancierModeleMurBois(_objetEnMain, main, false);
                }
                else if (EstIdPorteBois(main.ID)) InstancierModelePorteBois(_objetEnMain, main, false);
                else if (EstIdToitChaume(main.ID)) InstancierModeleToitChaume(_objetEnMain, main, ToitChaumeVarianteVisuelle.Solo, false);
                else if (EstIdTorche(main.ID)) InstancierModeleTorche(_objetEnMain, main, false);
                else InstancierModelePitFeu(_objetEnMain, main, 0.66f, false);
                _objetEnMain.SetMeta(cleSig, sig);
            }
            _objetEnMain.Scale = Vector3.One * ((main.ID == 200 || main.ID == IdObjetTableBoisDecorative || main.ID == IdObjetTableArtisanaTier1 || main.ID == IdObjetTableAnalyseTier1) ? 0.35f : (main.ID == IdObjetCoffreBoisTier0 ? 0.38f : ((main.ID == IdObjetPitFeu || main.ID == IdObjetPitFeuRoche) ? 0.40f : (EstIdFondation(main.ID) ? 0.40f : ((EstIdPlancher(main.ID) || EstIdMuret(main.ID) || EstIdMurBois(main.ID) || EstIdPorteBois(main.ID) || EstIdToitChaume(main.ID) || EstIdTorche(main.ID)) ? 0.38f : 0.42f)))));
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
        if (_objetEnMain.HasMeta(MetaSignatureTableAnalyse131))
            _objetEnMain.RemoveMeta(MetaSignatureTableAnalyse131);
        if (_objetEnMain.HasMeta(MetaSignatureRack109))
            _objetEnMain.RemoveMeta(MetaSignatureRack109);
        if (_objetEnMain.HasMeta(MetaSignatureCoffre113))
            _objetEnMain.RemoveMeta(MetaSignatureCoffre113);
        if (_objetEnMain.HasMeta(MetaSignaturePitFeu120))
            _objetEnMain.RemoveMeta(MetaSignaturePitFeu120);
        if (_objetEnMain.HasMeta(MetaSignaturePitFeuRoche122))
            _objetEnMain.RemoveMeta(MetaSignaturePitFeuRoche122);
        if (_objetEnMain.HasMeta(MetaSignatureFondation))
            _objetEnMain.RemoveMeta(MetaSignatureFondation);
        if (_objetEnMain.HasMeta(MetaSignatureSolBois136))
            _objetEnMain.RemoveMeta(MetaSignatureSolBois136);
        if (_objetEnMain.HasMeta(MetaSignatureSolRoche137))
            _objetEnMain.RemoveMeta(MetaSignatureSolRoche137);
        if (_objetEnMain.HasMeta(MetaSignatureAllumeFeu121))
            _objetEnMain.RemoveMeta(MetaSignatureAllumeFeu121);
        if (_objetEnMain.HasMeta(MetaSignatureMailletBois128))
            _objetEnMain.RemoveMeta(MetaSignatureMailletBois128);
        if (_objetEnMain.HasMeta(MetaSignatureBolBois129))
            _objetEnMain.RemoveMeta(MetaSignatureBolBois129);
        if (_objetEnMain.HasMeta(MetaSignatureMortierPilon130))
            _objetEnMain.RemoveMeta(MetaSignatureMortierPilon130);
        if (_objetEnMain.HasMeta(MetaSignatureFenetreBois146))
            _objetEnMain.RemoveMeta(MetaSignatureFenetreBois146);
        if (_objetEnMain.HasMeta(MetaSignatureAtelleJambe133))
            _objetEnMain.RemoveMeta(MetaSignatureAtelleJambe133);
        if (_objetEnMain.HasMeta(MetaSignatureAtelleBras134))
            _objetEnMain.RemoveMeta(MetaSignatureAtelleBras134);
        if (_objetEnMain.HasMeta(MetaSignatureBandageTier1135))
            _objetEnMain.RemoveMeta(MetaSignatureBandageTier1135);
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
        else if (main.ID == IdObjetAloeVera)
        {
            // Aloe en main: x2 par rapport au scale générique.
            _objetEnMain.Scale = Vector3.One * 1.0f;
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
            else if (EstSlotTerrainVoxelPosable(main))
                AppliquerMaterielObjet(_objetEnMain, ResoudreIdVoxelPose(main), main.IndexChimique, 0, 0, main.IndexBotanique);
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
            if (meshNode.HasMeta(MetaSignatureTableAnalyse131))
                meshNode.RemoveMeta(MetaSignatureTableAnalyse131);
            if (meshNode.HasMeta(MetaSignatureAtelleJambe133))
                meshNode.RemoveMeta(MetaSignatureAtelleJambe133);
            if (meshNode.HasMeta(MetaSignatureAtelleBras134))
                meshNode.RemoveMeta(MetaSignatureAtelleBras134);
            if (meshNode.HasMeta(MetaSignatureBandageTier1135))
                meshNode.RemoveMeta(MetaSignatureBandageTier1135);
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
            if (meshNode.HasMeta(MetaSignatureMailletBois128))
                meshNode.RemoveMeta(MetaSignatureMailletBois128);
            if (meshNode.HasMeta(MetaSignatureBolBois129))
                meshNode.RemoveMeta(MetaSignatureBolBois129);
            if (meshNode.HasMeta(MetaSignatureMortierPilon130))
                meshNode.RemoveMeta(MetaSignatureMortierPilon130);
            if (meshNode.HasMeta(MetaSignatureFenetreBois146))
                meshNode.RemoveMeta(MetaSignatureFenetreBois146);
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
        if (slot.ID == 106 || slot.ID == IdObjetHachePierreTier1 || slot.ID == IdObjetPellePierreTier0 || slot.ID == IdObjetPiochePierreTier0 || slot.ID == IdObjetLancePierreTier0)
        {
            bool estHachePierre = slot.ID == IdObjetHachePierreTier1;
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
                InstancierModeleArme(meshNode, slot, estLance ? 0.37f : (estPioche ? 0.36f : (estPelle ? 0.35f : (estHachePierre ? 0.35f : 0.34f))), 1f);
                meshNode.SetMeta(estLance ? MetaSignatureLance111 : (estPioche ? MetaSignaturePioche108 : (estPelle ? MetaSignaturePelle107 : MetaSignatureHachette106)), sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = estLance ? new Vector3(18f, 44f, -14f) : (estPioche ? new Vector3(22f, 42f, -18f) : (estPelle ? new Vector3(20f, 38f, -16f) : (estHachePierre ? new Vector3(22f, 40f, -18f) : new Vector3(22f, 40f, -18f))));
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
        if (slot.ID == BlocChutant.ID_FEUILLE_ARRACHEE && BlocChutant.EssenceUtiliseFeuilleGlb(slot.IndexBotanique))
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleFeuilleArrachee(meshNode, slot, 0.16f);
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-4f, 32f, 2f);
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
        if (slot.ID == IdObjetSteakCuit)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            NettoyerModelesEnfants(meshNode);
            InstancierModeleSteakCuit(meshNode, slot, 0.16f);
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
        if (slot.ID == IdObjetMailletBois)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexBotanique);
            int prev = meshNode.HasMeta(MetaSignatureMailletBois128) ? (int)meshNode.GetMeta(MetaSignatureMailletBois128).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleMailletBois(meshNode, slot, 0.30f, false);
                meshNode.SetMeta(MetaSignatureMailletBois128, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-8f, 56f, 8f);
            return;
        }
        if (slot.ID == IdObjetFenetreBois)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexBotanique);
            int prev = meshNode.HasMeta(MetaSignatureFenetreBois146) ? (int)meshNode.GetMeta(MetaSignatureFenetreBois146).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleFenetreBois(meshNode, slot, 0.30f, false);
                meshNode.SetMeta(MetaSignatureFenetreBois146, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-4f, 54f, 4f);
            return;
        }
        if (slot.ID == IdObjetBolBois)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexBotanique);
            int prev = meshNode.HasMeta(MetaSignatureBolBois129) ? (int)meshNode.GetMeta(MetaSignatureBolBois129).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleBolBois(meshNode, slot, 0.24f, false);
                meshNode.SetMeta(MetaSignatureBolBois129, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-4f, 48f, 4f);
            return;
        }
        if (slot.ID == IdObjetMortierPilonBois)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexBotanique, slot.IndexChimique, slot.GenomeAssemblage ?? "");
            int prev = meshNode.HasMeta(MetaSignatureMortierPilon130) ? (int)meshNode.GetMeta(MetaSignatureMortierPilon130).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleMortierPilonBois(meshNode, slot, 0.30f, false);
                meshNode.SetMeta(MetaSignatureMortierPilon130, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-3f, 52f, 4f);
            return;
        }
        if (slot.ID == IdObjetAtelleJambe || slot.ID == IdObjetAtelleBras)
        {
            bool estAtelleBras = slot.ID == IdObjetAtelleBras;
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique, slot.GenomeAssemblage ?? "");
            string cleMeta = estAtelleBras ? MetaSignatureAtelleBras134 : MetaSignatureAtelleJambe133;
            int prev = meshNode.HasMeta(cleMeta) ? (int)meshNode.GetMeta(cleMeta).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                if (estAtelleBras)
                    InstancierModeleAtelleBras(meshNode, slot, 0.30f, false);
                else
                    InstancierModeleAtelleJambe(meshNode, slot, 0.30f, false);
                meshNode.SetMeta(cleMeta, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-6f, 58f, 6f);
            return;
        }
        if (slot.ID == IdObjetBandageTier1)
        {
            meshNode.Mesh = null;
            meshNode.MaterialOverride = null;
            int sig = HashCode.Combine(slot.ID, slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique, slot.GenomeAssemblage ?? "");
            int prev = meshNode.HasMeta(MetaSignatureBandageTier1135) ? (int)meshNode.GetMeta(MetaSignatureBandageTier1135).AsInt32() : int.MinValue;
            bool manqueModele = meshNode.FindChild("ModeleArme", true, false) == null;
            if (manqueModele || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                InstancierModeleBandageTier1(meshNode, slot, 0.26f, false);
                meshNode.SetMeta(MetaSignatureBandageTier1135, sig);
            }
            meshNode.Scale = Vector3.One;
            meshNode.RotationDegrees = new Vector3(-4f, 52f, 5f);
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
        if (slot.ID == 200 || slot.ID == IdObjetTableBoisDecorative || slot.ID == IdObjetTableArtisanaTier1 || slot.ID == IdObjetTableAnalyseTier1 || slot.ID == IdObjetRackBatons || slot.ID == IdObjetRackBuches || slot.ID == IdObjetCoffreBoisTier0 || slot.ID == IdObjetPitFeu || slot.ID == IdObjetPitFeuRoche || EstIdFondation(slot.ID) || EstIdPlancher(slot.ID) || EstIdMuret(slot.ID) || EstIdMurBois(slot.ID) || EstIdPorteBois(slot.ID) || EstIdToitChaume(slot.ID) || EstIdTorche(slot.ID))
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
            int sig = (slot.ID == 200 || slot.ID == IdObjetTableBoisDecorative || slot.ID == IdObjetTableArtisanaTier1 || slot.ID == IdObjetTableAnalyseTier1)
                ? SignatureSlotAtelier200(slot)
                : (slot.ID == IdObjetCoffreBoisTier0
                ? SignatureSlotCoffre113(slot)
                : ((slot.ID == IdObjetPitFeu || slot.ID == IdObjetPitFeuRoche || EstIdFondation(slot.ID) || EstIdPlancher(slot.ID) || EstIdMuret(slot.ID) || EstIdMurBois(slot.ID) || EstIdPorteBois(slot.ID) || EstIdToitChaume(slot.ID) || EstIdTorche(slot.ID))
                ? HashCode.Combine(slot.ID, slot.IndexBotanique, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture, slot.GenomeAssemblage ?? "")
                : SignatureSlotRack109(slot)));
            string cleSig = (slot.ID == 200 || slot.ID == IdObjetTableBoisDecorative || slot.ID == IdObjetTableArtisanaTier1 || slot.ID == IdObjetTableAnalyseTier1)
                ? ((slot.ID == 200 || slot.ID == IdObjetTableBoisDecorative || slot.ID == IdObjetTableArtisanaTier1) ? MetaSignatureAtelier200 : MetaSignatureTableAnalyse131)
                : (slot.ID == IdObjetCoffreBoisTier0
                ? MetaSignatureCoffre113
                : (slot.ID == IdObjetPitFeu ? MetaSignaturePitFeu120 : (slot.ID == IdObjetPitFeuRoche ? MetaSignaturePitFeuRoche122 : (EstIdFondation(slot.ID) ? MetaSignatureFondation : ((EstIdSolBois(slot.ID) || EstIdMuretBois(slot.ID) || EstIdMurBois(slot.ID) || EstIdPorteBois(slot.ID) || EstIdToitChaume(slot.ID) || EstIdTorche(slot.ID)) ? MetaSignatureSolBois136 : ((EstIdSolRoche(slot.ID) || EstIdMuretPierre(slot.ID)) ? MetaSignatureSolRoche137 : MetaSignatureRack109))))));
            int prev = meshNode.HasMeta(cleSig) ? (int)meshNode.GetMeta(cleSig).AsInt32() : -1;
            RetirerMetaSiDifferente(meshNode, MetaSignatureAtelier200, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignatureTableAnalyse131, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignatureRack109, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignatureCoffre113, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignaturePitFeu120, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignaturePitFeuRoche122, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignatureFondation, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignatureSolBois136, cleSig);
            RetirerMetaSiDifferente(meshNode, MetaSignatureSolRoche137, cleSig);
            bool manque = ModeleArmeAbsent(meshNode);
            if (manque || sig != prev)
            {
                NettoyerModelesEnfants(meshNode);
                if (slot.ID == 200) InstancierModeleAtelierPrimitif(meshNode, slot);
                else if (slot.ID == IdObjetTableBoisDecorative) InstancierModeleTableBoisDecorative(meshNode, slot);
                else if (slot.ID == IdObjetTableArtisanaTier1) InstancierModeleTableArtisanaTier1(meshNode, slot);
                else if (slot.ID == IdObjetTableAnalyseTier1) InstancierModeleTableAnalyseTier1(meshNode, slot);
                else if (slot.ID == IdObjetRackBatons) InstancierModeleRackBatons(meshNode, slot);
                else if (slot.ID == IdObjetRackBuches) InstancierModeleRackBuches(meshNode, slot);
                else if (slot.ID == IdObjetCoffreBoisTier0) InstancierModeleCoffreBoisTier0(meshNode, slot, 0.78f, false);
                else if (slot.ID == IdObjetPitFeuRoche) InstancierModelePitFeuRoche(meshNode, slot, 0.80f, false);
                else if (EstIdFondation(slot.ID)) InstancierModeleFondation(meshNode, slot, 0.82f, false);
                else if (EstIdSolBois(slot.ID)) InstancierModeleSolBois(meshNode, slot, false);
                else if (EstIdSolRoche(slot.ID)) InstancierModeleSolRoche(meshNode, slot, false);
                else if (EstIdMuret(slot.ID)) InstancierModeleMuretBois(meshNode, slot, false);
                else if (EstIdMurBois(slot.ID))
                {
                    if (EstIdMurBoisFenetre(slot.ID)) InstancierModeleMurBoisFenetre(meshNode, slot, false);
                    else if (EstIdMurBoisCadrePorte(slot.ID)) InstancierModeleMurBoisCadrePorte(meshNode, slot, false);
                    else InstancierModeleMurBois(meshNode, slot, false);
                }
                else if (EstIdPorteBois(slot.ID)) InstancierModelePorteBois(meshNode, slot, false);
                else if (EstIdToitChaume(slot.ID)) InstancierModeleToitChaume(meshNode, slot, ToitChaumeVarianteVisuelle.Solo, false);
                else if (EstIdTorche(slot.ID)) InstancierModeleTorche(meshNode, slot, false);
                else InstancierModelePitFeu(meshNode, slot, 0.76f, false);
                meshNode.SetMeta(cleSig, sig);
            }
            meshNode.Scale = Vector3.One * ((slot.ID == 200 || slot.ID == IdObjetTableBoisDecorative || slot.ID == IdObjetTableArtisanaTier1 || slot.ID == IdObjetTableAnalyseTier1) ? 0.8f : (slot.ID == IdObjetCoffreBoisTier0 ? 0.82f : ((slot.ID == IdObjetPitFeu || slot.ID == IdObjetPitFeuRoche) ? 0.86f : (EstIdFondation(slot.ID) ? 0.86f : ((EstIdPlancher(slot.ID) || EstIdMuret(slot.ID) || EstIdMurBois(slot.ID) || EstIdPorteBois(slot.ID) || EstIdToitChaume(slot.ID) || EstIdTorche(slot.ID)) ? 0.84f : 0.92f)))));
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
        if (meshNode.HasMeta(MetaSignatureTableAnalyse131))
            meshNode.RemoveMeta(MetaSignatureTableAnalyse131);
        if (meshNode.HasMeta(MetaSignatureRack109))
            meshNode.RemoveMeta(MetaSignatureRack109);
        if (meshNode.HasMeta(MetaSignatureCoffre113))
            meshNode.RemoveMeta(MetaSignatureCoffre113);
        if (meshNode.HasMeta(MetaSignaturePitFeu120))
            meshNode.RemoveMeta(MetaSignaturePitFeu120);
        if (meshNode.HasMeta(MetaSignaturePitFeuRoche122))
            meshNode.RemoveMeta(MetaSignaturePitFeuRoche122);
        if (meshNode.HasMeta(MetaSignatureFondation))
            meshNode.RemoveMeta(MetaSignatureFondation);
        if (meshNode.HasMeta(MetaSignatureSolBois136))
            meshNode.RemoveMeta(MetaSignatureSolBois136);
        if (meshNode.HasMeta(MetaSignatureSolRoche137))
            meshNode.RemoveMeta(MetaSignatureSolRoche137);
        if (meshNode.HasMeta(MetaSignatureAllumeFeu121))
            meshNode.RemoveMeta(MetaSignatureAllumeFeu121);
        if (meshNode.HasMeta(MetaSignatureMailletBois128))
            meshNode.RemoveMeta(MetaSignatureMailletBois128);
        if (meshNode.HasMeta(MetaSignatureBolBois129))
            meshNode.RemoveMeta(MetaSignatureBolBois129);
        if (meshNode.HasMeta(MetaSignatureMortierPilon130))
            meshNode.RemoveMeta(MetaSignatureMortierPilon130);
        if (meshNode.HasMeta(MetaSignatureFenetreBois146))
            meshNode.RemoveMeta(MetaSignatureFenetreBois146);
        if (meshNode.HasMeta(MetaSignatureAtelleJambe133))
            meshNode.RemoveMeta(MetaSignatureAtelleJambe133);
        if (meshNode.HasMeta(MetaSignatureAtelleBras134))
            meshNode.RemoveMeta(MetaSignatureAtelleBras134);
        if (meshNode.HasMeta(MetaSignatureBandageTier1135))
            meshNode.RemoveMeta(MetaSignatureBandageTier1135);
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
            else if (EstSlotTerrainVoxelPosable(slot))
                AppliquerMaterielObjet(meshNode, ResoudreIdVoxelPose(slot), slot.IndexChimique, 0, 0, slot.IndexBotanique);
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

    private static ulong CalculerEmpreinteSlotApercuAvatar(in SlotInventaire slot)
    {
        if (slot.EstVide)
            return 0UL;
        var hc = new HashCode();
        hc.Add(slot.ID);
        hc.Add(slot.IndexMorphologique);
        hc.Add(slot.IndexChimique);
        hc.Add(slot.IndexTaille);
        hc.Add(slot.NiveauFracture);
        hc.Add(slot.EstUnEclat);
        hc.Add(slot.IndexBotanique);
        hc.Add(slot.Quantite);
        hc.Add(slot.GenomeAssemblage ?? "");
        hc.Add(slot.CleConteneur ?? "");
        hc.Add(Mathf.RoundToInt(slot.DurabiliteOutilActuelle * 1000f));
        hc.Add(Mathf.RoundToInt(slot.DurabiliteOutilMax * 1000f));
        hc.Add(slot.IndexTailleLameRoche);
        hc.Add(slot.ScaleEclat.X);
        hc.Add(slot.ScaleEclat.Y);
        hc.Add(slot.ScaleEclat.Z);
        if (slot.EstUnEclat && slot.MeshEclat != null && GodotObject.IsInstanceValid(slot.MeshEclat))
            hc.Add(slot.MeshEclat.GetInstanceId());
        return unchecked((ulong)(uint)hc.ToHashCode());
    }

    /// <summary>Empreinte visuelle de l'avatar (corps + apparence + équipements) pour décider de régénérer le clone UI.</summary>
    public ulong CalculerEmpreinteAvatarApercuUi()
    {
        var hc = new HashCode();
        RaceJoueur race = GameState.Instance?.RaceJoueurCourante ?? RaceJoueur.Humain;
        SexeJoueur sexe = GameState.Instance?.SexeJoueurCourante ?? SexeJoueur.Masculin;
        hc.Add((int)race);
        hc.Add((int)sexe);
        hc.Add(CouleurPeauHumain);
        hc.Add(CouleurSousVetementHumain);
        hc.Add(TexturePeauHumain?.GetInstanceId() ?? 0UL);
        hc.Add(TextureSousVetementHumain?.GetInstanceId() ?? 0UL);
        hc.Add(CalculerEmpreinteSlotApercuAvatar(EquipementCeinture));
        hc.Add(CalculerEmpreinteSlotApercuAvatar(EquipementSacDos));
        hc.Add(CalculerEmpreinteSlotApercuAvatar(EquipementCarnet));
        return unchecked((ulong)(uint)hc.ToHashCode());
    }

    /// <summary>Clone du rig gameplay pour un rendu UI isolé dans un SubViewport dédié.</summary>
    public Node3D CreerCloneAvatarApercuUi()
    {
        if (_rigHumain == null || !GodotObject.IsInstanceValid(_rigHumain))
            return null;
        if (_rigHumain.Duplicate() is not Node3D clone)
            return null;
        clone.Name = "HumainRigApercuUi";
        // Le rig source peut être masqué en FPS (Visible=false récursif). Le clone UI doit rester
        // complètement indépendant pour l'aperçu du menu anatomie.
        ForcerVisibiliteCloneApercuUi(clone);
        NeutraliserNoeudsIkCloneApercuUi(clone);
        return clone;
    }

    /// <summary>Réactive explicitement la visibilité de tous les nœuds visuels du clone UI.</summary>
    private static void ForcerVisibiliteCloneApercuUi(Node racine)
    {
        if (racine == null || !GodotObject.IsInstanceValid(racine))
            return;

        if (racine is Node3D n3d)
            n3d.Visible = true;

        if (racine is VisualInstance3D vi)
            vi.Visible = true;

        foreach (Node enfant in racine.GetChildren())
            ForcerVisibiliteCloneApercuUi(enfant);
    }

    /// <summary>
    /// Le clone UI n'a pas la hiérarchie gameplay (Camera3D/AimantMainDroiteIK).
    /// On supprime les IK runtime qui référencent ces chemins pour éviter les erreurs
    /// "Node not found" au moment du AddChild dans le SubViewport.
    /// </summary>
    private static void NeutraliserNoeudsIkCloneApercuUi(Node racine)
    {
        if (racine == null || !GodotObject.IsInstanceValid(racine))
            return;

        var enfants = racine.GetChildren();
        for (int i = enfants.Count - 1; i >= 0; i--)
        {
            if (enfants[i] is not Node enfant || !GodotObject.IsInstanceValid(enfant))
                continue;

            bool estIkRuntime = enfant is SkeletonIK3D
                || string.Equals(enfant.Name.ToString(), "IK_BrasDroitFPS", StringComparison.Ordinal)
                || string.Equals(enfant.Name.ToString(), "AimantMainDroiteIK", StringComparison.Ordinal);

            if (estIkRuntime)
            {
                // Important: le clone UI est ajouté immédiatement dans un SubViewport.
                // QueueFree() laisserait le nœud IK vivant jusqu'à la frame suivante,
                // ce qui déclenche "Node not found ... AimantMainDroiteIK" à l'entrée dans l'arbre.
                enfant.Free();
                continue;
            }

            NeutraliserNoeudsIkCloneApercuUi(enfant);
        }
    }

    /// <summary>Maintient le clone UI aligné avec l'orientation/pose globale du rig local.</summary>
    public void SynchroniserTransformAvatarApercuUi(Node3D avatarApercu)
    {
        if (avatarApercu == null || !GodotObject.IsInstanceValid(avatarApercu))
            return;
        if (_rigHumain == null || !GodotObject.IsInstanceValid(_rigHumain))
            return;
        // Aperçu UI: orientation fixe face caméra (pas de profil), plus bas et taille augmentée.
        avatarApercu.Position = _rigHumain.Position + new Vector3(0f, -0.34f, 0f);
        avatarApercu.Rotation = new Vector3(0f, Mathf.DegToRad(270f), 0f);
        avatarApercu.Scale = Vector3.One * 0.99f;
        avatarApercu.Visible = true;
    }

    /// <summary>Cache le SubViewport quand pas d'objet avec visuel (pierre, fibre, corde), pour laisser voir la couleur du slot.</summary>
    private void MettreAJourVisibilitePreviews()
    {
        if (_viewportSlotGauche != null) _viewportSlotGauche.Visible = !MainGauche.EstVide && EstObjetAvecVisuel(MainGauche.ID);
        if (_viewportSlotDroite != null) _viewportSlotDroite.Visible = !MainDroite.EstVide && EstObjetAvecVisuel(MainDroite.ID);
        if (_viewportSlotCarnet != null) _viewportSlotCarnet.Visible = !EquipementCarnet.EstVide && EstObjetAvecVisuel(EquipementCarnet.ID);
    }

}
