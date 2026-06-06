using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>Allume-feu préhistorique : modèle GLB avec matériau dépendant de la roche sulfureuse (marcassite/pyrite).</summary>
    public static void InstancierModeleAllumeFeu(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.42f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/Equipements/alume_feu_preistorique.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        int idxSulfure = Mathf.Clamp(slot.IndexChimique, ItemPhysique.IndexChimiqueSilex, ItemPhysique.TableGeologique.Length - 1);
        if (idxSulfure != 10 && idxSulfure != 11)
            idxSulfure = 10;
        Material matSilex = ItemPhysique.CreerMaterielProcedural(true, ItemPhysique.IndexChimiqueSilex);
        Material matSulfure = ItemPhysique.CreerMaterielProcedural(false, idxSulfure);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.05f, 0.08f) },
                MaterialOverride = matSulfure
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        int meshIndex = 0;
        void ParcourirMeshesAllumeFeu(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estSilex = nom.Contains("silex") || nom.Contains("flint");
                bool estSulfure = nom.Contains("pyrit") || nom.Contains("marcas") || nom.Contains("sulf");
                if (estSilex)
                    mi.MaterialOverride = matSilex;
                else if (estSulfure)
                    mi.MaterialOverride = matSulfure;
                else
                    mi.MaterialOverride = (meshIndex++ % 2 == 0) ? matSulfure : matSilex;
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshesAllumeFeu(c);
        }

        ParcourirMeshesAllumeFeu(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Maillet en bois: GLB dédié avec matière bois de l'essence craftée.</summary>
    public static void InstancierModeleMailletBois(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.42f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/pillon+en+bois.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new CapsuleMesh { Radius = 0.06f, Height = 0.28f },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois)
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        foreach (MeshInstance3D mi in ListerMeshes(modele))
            mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Bol en bois: GLB dédié avec matériau bois de l'essence utilisée au craft.</summary>
    public static void InstancierModeleBolBois(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.32f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/Bowl+en+bois.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new SphereMesh { Radius = 0.10f, Height = 0.08f },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois)
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        foreach (MeshInstance3D mi in ListerMeshes(modele))
            mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Bol rempli : même bol bois (essence du craft) + mesh « liquide » texturé avec le matériau eau du jeu.</summary>
    public static void InstancierModeleBolEau(Node3D parent, SlotInventaire slot, Material materielLiquide, float tailleMaxMetres = 0.32f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/bowl_plaine.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
        Material matLiquide = materielLiquide ?? ConstruireMaterielEauBolFallback();

        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new SphereMesh { Radius = 0.10f, Height = 0.08f },
                MaterialOverride = matBois
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        foreach (MeshInstance3D mi in ListerMeshes(modele))
        {
            string nom = mi.Name.ToString().ToLowerInvariant();
            // Le mesh « liquide » (cf. bowl_plaine.glb) reçoit l'eau ; tout le reste garde le bois.
            mi.MaterialOverride = nom.Contains("liquid") ? matLiquide : matBois;
        }
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Matériau eau de repli (bleu translucide) si le matériau eau du monde n'est pas disponible.</summary>
    private static Material ConstruireMaterielEauBolFallback()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.45f, 0.62f, 0.72f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.12f,
            Metallic = 0.0f
        };
    }

    private static bool ExtraireEssencesMortierPilon(SlotInventaire slot, out byte essenceBol, out byte essencePilon)
    {
        essenceBol = slot.IndexBotanique;
        essencePilon = (byte)Mathf.Clamp(slot.IndexChimique, 0, 255);
        string g = slot.GenomeAssemblage ?? "";
        if (!g.StartsWith("MORTIERPILON:", StringComparison.Ordinal))
            return false;
        string[] morceaux = g.Substring("MORTIERPILON:".Length).Split(',');
        if (morceaux.Length < 2)
            return false;
        bool okBol = byte.TryParse(morceaux[0], out byte b);
        bool okPilon = byte.TryParse(morceaux[1], out byte p);
        if (okBol) essenceBol = b;
        if (okPilon) essencePilon = p;
        return okBol || okPilon;
    }

    /// <summary>Mortier + pilon: deux matériaux bois séparés (mortier hérite bol, pilon hérite pilon source).</summary>
    public static void InstancierModeleMortierPilonBois(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.44f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/mortier+et+pillon.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        ExtraireEssencesMortierPilon(slot, out byte essenceBol, out byte essencePilon);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.12f, Height = 0.20f },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBol)
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        Material matBol = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBol);
        Material matPilon = ArbreVivant.ObtenirMaterielBoisTriplanar(essencePilon);
        int ordinal = 0;
        foreach (MeshInstance3D mi in ListerMeshes(modele))
        {
            string nom = mi.Name.ToString().ToLowerInvariant();
            bool estPilon = nom.Contains("pilon") || nom.Contains("pestle") || nom.Contains("club");
            bool estBolMortier = nom.Contains("mortier") || nom.Contains("bol") || nom.Contains("bowl") || nom.Contains("mortar");
            if (estPilon)
                mi.MaterialOverride = matPilon;
            else if (estBolMortier)
                mi.MaterialOverride = matBol;
            else
                mi.MaterialOverride = ordinal++ == 0 ? matBol : matPilon;
        }

        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Atelle de jambe: GLB soin, textures branchage/liage héritées du craft.</summary>
    public static void InstancierModeleAtelleJambe(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.34f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/soin/Atelle_jambe.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBranche = slot.IndexBotanique;
        int ligC = slot.IndexChimique;
        int ligM = slot.IndexMorphologique;
        byte ligV = LSystem_Botanique.IndexChene;
        string g = slot.GenomeAssemblage ?? "";
        if (g.StartsWith("ATELLE133", StringComparison.Ordinal))
        {
            string[] morceaux = g.Split(';');
            for (int i = 0; i < morceaux.Length; i++)
            {
                string m = morceaux[i];
                if (m.StartsWith("BOIS=", StringComparison.Ordinal) && byte.TryParse(m.Substring("BOIS=".Length), out byte b))
                    essenceBranche = b;
                else if (m.StartsWith("LIGV=", StringComparison.Ordinal) && byte.TryParse(m.Substring("LIGV=".Length), out byte v))
                    ligV = v;
                else if (m.StartsWith("LIGC=", StringComparison.Ordinal) && int.TryParse(m.Substring("LIGC=".Length), out int c))
                    ligC = c;
                else if (m.StartsWith("LIGM=", StringComparison.Ordinal) && int.TryParse(m.Substring("LIGM=".Length), out int m2))
                    ligM = m2;
            }
        }

        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.10f, 0.12f) },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBranche)
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        Material matBranche = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBranche);
        var slotLigature = new SlotInventaire
        {
            ID = 20,
            IndexChimique = ligC,
            IndexMorphologique = ligM,
            IndexBotanique = ligV,
            EstUnEclat = false
        };
        var dummyLigature = new MeshInstance3D();
        AppliquerMaterielObjet(dummyLigature, 20, slotLigature.IndexChimique, slotLigature.IndexMorphologique, 0, slotLigature.IndexBotanique);
        Material matLigature = dummyLigature.MaterialOverride ?? Atlas_Matiere.ObtenirMaterielCorde(slotLigature.IndexChimique, slotLigature.IndexMorphologique, 0);
        int ordinal = 0;
        foreach (MeshInstance3D mi in ListerMeshes(modele))
        {
            string nom = mi.Name.ToString().ToLowerInvariant();
            bool estBranche = nom.Contains("branche") || nom.Contains("branch") || nom.Contains("bois") || nom.Contains("wood") || nom.Contains("baton") || nom.Contains("stick");
            bool estLigature = nom.Contains("liage") || nom.Contains("ligature") || nom.Contains("corde") || nom.Contains("rope") || nom.Contains("lien");
            if (estBranche)
                mi.MaterialOverride = matBranche;
            else if (estLigature)
                mi.MaterialOverride = matLigature;
            else
                mi.MaterialOverride = (ordinal++ % 2 == 0) ? matBranche : matLigature;
        }

        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Atelle de bras: GLB soin, textures branchage/liage héritées du craft.</summary>
    public static void InstancierModeleAtelleBras(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.34f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/soin/Atelle_Bras.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBranche = slot.IndexBotanique;
        int ligC = slot.IndexChimique;
        int ligM = slot.IndexMorphologique;
        byte ligV = LSystem_Botanique.IndexChene;
        string g = slot.GenomeAssemblage ?? "";
        if (g.StartsWith("ATELLE134", StringComparison.Ordinal))
        {
            string[] morceaux = g.Split(';');
            for (int i = 0; i < morceaux.Length; i++)
            {
                string m = morceaux[i];
                if (m.StartsWith("BOIS=", StringComparison.Ordinal) && byte.TryParse(m.Substring("BOIS=".Length), out byte b))
                    essenceBranche = b;
                else if (m.StartsWith("LIGV=", StringComparison.Ordinal) && byte.TryParse(m.Substring("LIGV=".Length), out byte v))
                    ligV = v;
                else if (m.StartsWith("LIGC=", StringComparison.Ordinal) && int.TryParse(m.Substring("LIGC=".Length), out int c))
                    ligC = c;
                else if (m.StartsWith("LIGM=", StringComparison.Ordinal) && int.TryParse(m.Substring("LIGM=".Length), out int m2))
                    ligM = m2;
            }
        }

        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.10f, 0.12f) },
                MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBranche)
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        Material matBranche = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBranche);
        var slotLigature = new SlotInventaire
        {
            ID = 20,
            IndexChimique = ligC,
            IndexMorphologique = ligM,
            IndexBotanique = ligV,
            EstUnEclat = false
        };
        var dummyLigature = new MeshInstance3D();
        AppliquerMaterielObjet(dummyLigature, 20, slotLigature.IndexChimique, slotLigature.IndexMorphologique, 0, slotLigature.IndexBotanique);
        Material matLigature = dummyLigature.MaterialOverride ?? Atlas_Matiere.ObtenirMaterielCorde(slotLigature.IndexChimique, slotLigature.IndexMorphologique, 0);
        int ordinal = 0;
        foreach (MeshInstance3D mi in ListerMeshes(modele))
        {
            string nom = mi.Name.ToString().ToLowerInvariant();
            bool estBranche = nom.Contains("branche") || nom.Contains("branch") || nom.Contains("bois") || nom.Contains("wood") || nom.Contains("baton") || nom.Contains("stick");
            bool estLigature = nom.Contains("liage") || nom.Contains("ligature") || nom.Contains("corde") || nom.Contains("rope") || nom.Contains("lien");
            if (estBranche)
                mi.MaterialOverride = matBranche;
            else if (estLigature)
                mi.MaterialOverride = matLigature;
            else
                mi.MaterialOverride = (ordinal++ % 2 == 0) ? matBranche : matLigature;
        }

        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Bandage tier 1 : GLB soin, texture liage héritée du craft.</summary>
    public static void InstancierModeleBandageTier1(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.28f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/soin/Bandage_tier1.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        int ligC = slot.IndexChimique;
        int ligM = slot.IndexMorphologique;
        byte ligV = slot.IndexBotanique;
        string g = slot.GenomeAssemblage ?? "";
        if (g.StartsWith("BANDAGE135", StringComparison.Ordinal))
        {
            string[] morceaux = g.Split(';');
            for (int i = 0; i < morceaux.Length; i++)
            {
                string m = morceaux[i];
                if (m.StartsWith("LIGV=", StringComparison.Ordinal) && byte.TryParse(m.Substring("LIGV=".Length), out byte v))
                    ligV = v;
                else if (m.StartsWith("LIGC=", StringComparison.Ordinal) && int.TryParse(m.Substring("LIGC=".Length), out int c))
                    ligC = c;
                else if (m.StartsWith("LIGM=", StringComparison.Ordinal) && int.TryParse(m.Substring("LIGM=".Length), out int m2))
                    ligM = m2;
            }
        }

        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.06f, 0.10f) },
                MaterialOverride = Atlas_Matiere.ObtenirMaterielCorde(ligC, ligM, 0)
            };
            parent.AddChild(fallback);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        bool estLiane = ligV == Joueur.TagVarianteLiane;
        var slotLigature = new SlotInventaire
        {
            ID = estLiane ? (byte)16 : (byte)20,
            IndexChimique = ligC,
            IndexMorphologique = ligM,
            IndexBotanique = ligV,
            EstUnEclat = false
        };
        var dummyLigature = new MeshInstance3D();
        AppliquerMaterielObjet(dummyLigature, slotLigature.ID, slotLigature.IndexChimique, slotLigature.IndexMorphologique, 0, slotLigature.IndexBotanique);
        Material matLigature = dummyLigature.MaterialOverride ?? Atlas_Matiere.ObtenirMaterielCorde(slotLigature.IndexChimique, slotLigature.IndexMorphologique, 0);
        foreach (MeshInstance3D mi in ListerMeshes(modele))
            mi.MaterialOverride = matLigature;

        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
