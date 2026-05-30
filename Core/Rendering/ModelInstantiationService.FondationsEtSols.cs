using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    /// <summary>Plateau de marche roche : triplanar monde + sans relief normal (évite le sole noir).</summary>
    public static StandardMaterial3D ObtenirMaterielRochePlateauFondation(int indexChimique)
    {
        int idx = Mathf.Clamp(indexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        var mat = (StandardMaterial3D)ItemPhysique.CreerMaterielProcedural(false, idx).Duplicate(true);
        mat.Uv1WorldTriplanar = true;
        mat.Uv1Triplanar = true;
        mat.Uv1Scale = new Vector3(0.85f, 0.85f, 0.85f);
        mat.NormalEnabled = false;
        mat.AlbedoColor = new Color(1.12f, 1.10f, 1.06f);
        return mat;
    }

    /// <summary>Plateau de marche bois (fondation bois pure ou sole bois).</summary>
    public static StandardMaterial3D ObtenirMaterielBoisPlateauFondation(byte essenceBois)
    {
        byte essence = essenceBois;
        if (essence == Joueur.TagVarianteLiane || essence == Joueur.TagVarianteHerbeSolide
            || essence == Joueur.TagVarianteIntestin || essence == Joueur.TagVarianteIntestinSolide)
            essence = LSystem_Botanique.IndexChene;
        var mat = (StandardMaterial3D)ArbreVivant.ObtenirMaterielBoisTriplanar(essence).Duplicate(true);
        mat.Uv1WorldTriplanar = true;
        mat.Uv1Triplanar = true;
        mat.Uv1Scale = new Vector3(0.9f, 0.9f, 0.9f);
        mat.NormalEnabled = false;
        mat.AlbedoColor = new Color(1.10f, 1.06f, 1.0f);
        return mat;
    }

    /// <summary>Plancher bois posé : texture/triplanar de l'essence craftée (chêne, bouleau, pin, etc.), légèrement éclairci pour la nuit.</summary>
    public static StandardMaterial3D ObtenirMaterielSolBois(byte essenceBois)
    {
        byte essence = essenceBois;
        if (essence == Joueur.TagVarianteLiane || essence == Joueur.TagVarianteHerbeSolide
            || essence == Joueur.TagVarianteIntestin || essence == Joueur.TagVarianteIntestinSolide)
            essence = LSystem_Botanique.IndexChene;

        var mat = (StandardMaterial3D)ArbreVivant.ObtenirMaterielBoisTriplanar(essence).Duplicate(true);
        mat.Uv1Triplanar = true;
        mat.Uv1WorldTriplanar = true;
        mat.Uv1Scale = new Vector3(0.85f, 0.85f, 0.85f);
        mat.Uv1TriplanarSharpness = 2f;
        mat.NormalEnabled = false;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        // Conserver la teinte de l'essence : pas d'albedo blanc uniforme.
        Color baseC = mat.AlbedoColor;
        const float boostLuminosite = 1.14f;
        mat.AlbedoColor = new Color(
            Mathf.Min(baseC.R * boostLuminosite, 1.25f),
            Mathf.Min(baseC.G * boostLuminosite, 1.25f),
            Mathf.Min(baseC.B * boostLuminosite, 1.25f));
        mat.Roughness = 0.9f;
        mat.Metallic = 0f;
        mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
        return mat;
    }

    /// <summary>Plancher roche posé : triplanar monde (comme sol bois / plateau fondation), teinte selon IndexChimique.</summary>
    public static StandardMaterial3D ObtenirMaterielSolRoche(int indexChimique)
    {
        int idx = Mathf.Clamp(indexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        var mat = (StandardMaterial3D)ObtenirMaterielRochePlateauFondation(idx).Duplicate(true);
        mat.Uv1Scale = new Vector3(0.85f, 0.85f, 0.85f);
        mat.Uv1TriplanarSharpness = 2f;
        mat.NormalEnabled = false;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        Color baseC = mat.AlbedoColor;
        const float boostLuminosite = 1.12f;
        mat.AlbedoColor = new Color(
            Mathf.Min(baseC.R * boostLuminosite, 1.25f),
            Mathf.Min(baseC.G * boostLuminosite, 1.25f),
            Mathf.Min(baseC.B * boostLuminosite, 1.25f));
        mat.Roughness = 0.9f;
        mat.Metallic = 0f;
        mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
        return mat;
    }

    private static void AppliquerMaterielPlancherSurMeshesGlb(Node3D modeleRacine, Material mat)
    {
        if (modeleRacine == null || mat == null) return;
        foreach (MeshInstance3D mi in ListerMeshes(modeleRacine))
            mi.MaterialOverride = mat;
    }

    private static bool EstMeshPlateauMarcheFondation(MeshInstance3D mi, float minY, float maxY, float hauteur)
    {
        if (mi?.Mesh == null) return false;
        Aabb aabbMesh = TransformerAabb(mi.Transform, mi.Mesh.GetAabb());
        bool toucheSommet = aabbMesh.End.Y >= (maxY - Mathf.Max(0.04f, hauteur * 0.1f));
        bool couchePlate = aabbMesh.Size.Y <= hauteur * 0.45f;
        return toucheSommet && couchePlate;
    }

    /// <summary>Collision plane unique pour marcher sans micro-bosses (trimesh des bûches + plateau).</summary>
    public static void AjouterCollisionPlateauFondation(RigidBody3D corps, Node3D meshRoot)
    {
        if (corps == null || meshRoot == null) return;
        Aabb? bounds = null;
        AccumulerAabbMeshes(meshRoot, Transform3D.Identity, ref bounds);
        const float epaisseurPlateau = 0.18f;
        if (bounds.HasValue)
        {
            Aabb b = bounds.Value;
            float topY = b.End.Y;
            Vector3 centre = b.GetCenter();
            float largeur = Mathf.Clamp(b.Size.X * 0.97f, 0.8f, 4.05f);
            float profondeur = Mathf.Clamp(b.Size.Z * 0.97f, 0.8f, 4.05f);
            corps.AddChild(new CollisionShape3D
            {
                Name = "CollisionPlateauMarche",
                Shape = new BoxShape3D { Size = new Vector3(largeur, epaisseurPlateau, profondeur) },
                Position = new Vector3(centre.X, topY - epaisseurPlateau * 0.5f, centre.Z)
            });
            return;
        }
        corps.AddChild(new CollisionShape3D
        {
            Name = "CollisionPlateauMarche",
            Shape = new BoxShape3D { Size = new Vector3(4f, epaisseurPlateau, 4f) },
            Position = new Vector3(0f, 1f - epaisseurPlateau * 0.5f, 0f)
        });
    }

    /// <summary>Fondations: variantes bois/roche/mixte chargées depuis Modeles/structure/fondation.</summary>
    public static void InstancierModeleFondation(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.96f, bool ancrerBaseAuSol = true)
    {
        string cheminGlb = slot.ID switch
        {
            IdObjetFondationBois => "res://Modeles/structure/fondation/Fondation+bois.glb",
            IdObjetFondationRoche => "res://Modeles/structure/fondation/fondation+roche.glb",
            IdObjetFondationBoisSoleRoche => "res://Modeles/structure/fondation/fondation+bois+sole+roche.glb",
            IdObjetFondationRocheSoleBois => "res://Modeles/structure/fondation/fondation en roche sole en bois.glb",
            _ => ""
        };
        if (string.IsNullOrEmpty(cheminGlb))
            return;

        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        int idxRoche = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);

        if (scene == null)
        {
            bool dominanteRoche = slot.ID == IdObjetFondationRoche || slot.ID == IdObjetFondationRocheSoleBois;
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = ancrerBaseAuSol ? new Vector3(4f, 1f, 4f) : new Vector3(0.92f, 0.18f, 0.92f) },
                MaterialOverride = dominanteRoche
                    ? ItemPhysique.CreerMaterielProcedural(false, idxRoche)
                    : ArbreVivant.ObtenirMaterielBoisTriplanar((byte)Mathf.Clamp((int)essenceBois, 0, 4))
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        List<MeshInstance3D> meshesFondation = ListerMeshes(modele);
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        foreach (MeshInstance3D mesh in meshesFondation)
        {
            if (mesh?.Mesh == null) continue;
            Aabb aabbLocal = TransformerAabb(mesh.Transform, mesh.Mesh.GetAabb());
            minY = Mathf.Min(minY, aabbLocal.Position.Y);
            maxY = Mathf.Max(maxY, aabbLocal.End.Y);
        }
        if (minY > maxY)
        {
            minY = -0.5f;
            maxY = 0.5f;
        }
        float hauteur = Mathf.Max(0.001f, maxY - minY);

        string genomeFondation = slot.GenomeAssemblage ?? "";
        bool topBois;
        bool sideBois;
        if (genomeFondation.Contains("TOPBOIS", StringComparison.OrdinalIgnoreCase))
            topBois = true;
        else if (genomeFondation.Contains("TOPROCH", StringComparison.OrdinalIgnoreCase))
            topBois = false;
        else
            topBois = slot.ID == IdObjetFondationBoisSoleRoche;

        if (genomeFondation.Contains("SIDEBOIS", StringComparison.OrdinalIgnoreCase))
            sideBois = true;
        else if (genomeFondation.Contains("SIDEROCH", StringComparison.OrdinalIgnoreCase))
            sideBois = false;
        else
            sideBois = slot.ID == IdObjetFondationRocheSoleBois;

        bool mixteBaseBois = slot.ID == IdObjetFondationBoisSoleRoche;
        bool mixteBaseRoche = slot.ID == IdObjetFondationRocheSoleBois;
        bool estMixteFondation = mixteBaseBois || mixteBaseRoche;

        static bool NomSuggereRoche(string nom)
        {
            return nom.Contains("rock")
                || nom.Contains("roche")
                || nom.Contains("stone")
                || nom.Contains("pierre")
                || nom.Contains("caill")
                || nom.Contains("tripo");
        }

        static bool NomSuggereBois(string nom)
        {
            return nom.Contains("wood")
                || nom.Contains("bois")
                || nom.Contains("log")
                || nom.Contains("buche")
                || nom.Contains("bûche")
                || nom.Contains("rondin");
        }

        /// <summary>
        /// GLB mixtes Blender : noms de mesh explicites (priorité sur heuristique top/side).
        /// fondation+bois+sole+roche → bois + pierre ; fondation en roche sole en bois → bois + tripo_12.
        /// </summary>
        static bool? ResoudreRocheDepuisNomMeshFondationMixte(string nomBrut)
        {
            string nom = nomBrut.ToLowerInvariant();
            int sep = nom.LastIndexOf('/');
            if (sep >= 0)
                nom = nom[(sep + 1)..];

            if (nom == "bois" || nom.StartsWith("bois_") || nom.EndsWith("_bois"))
                return false;
            if (nom == "pierre" || nom.StartsWith("pierre_") || nom.EndsWith("_pierre"))
                return true;
            if (nom.Contains("tripo"))
                return true;

            return null;
        }

        void ParcourirMeshesFondation(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                bool estRoche = NomSuggereRoche(nom);
                bool estBois = NomSuggereBois(nom);
                bool forcerRoche = slot.ID == IdObjetFondationRoche;
                bool forcerBois = slot.ID == IdObjetFondationBois;
                // Règle de rendu fidèle au craft:
                // - fondation pure roche => tout en roche du slot crafté
                // - fondation pure bois => tout en essence bois du slot crafté
                // - fondations mixtes => parties détectées "roche" en roche craftée, le reste en bois crafté
                bool nomTagConnu = estRoche || estBois;
                bool estMixte = !forcerBois && !forcerRoche && estMixteFondation;
                bool appliquerRoche;

                if (forcerRoche) appliquerRoche = true;
                else if (forcerBois) appliquerRoche = false;
                else if (estMixte)
                {
                    bool? rocheDepuisNomMesh = ResoudreRocheDepuisNomMeshFondationMixte(nom);
                    if (rocheDepuisNomMesh.HasValue)
                        appliquerRoche = rocheDepuisNomMesh.Value;
                    else if (nomTagConnu && !(estRoche && estBois))
                        appliquerRoche = estRoche;
                    else if (mi.Mesh != null)
                    {
                        // Repli rare : mesh sans nom Blender connu → position Y (top = plateau craft).
                        Aabb aabbMesh = TransformerAabb(mi.Transform, mi.Mesh.GetAabb());
                        float epaisseurRelative = aabbMesh.Size.Y / hauteur;
                        bool toucheSommet = aabbMesh.End.Y >= (maxY - Mathf.Max(0.03f, hauteur * 0.08f));
                        bool estTop = toucheSommet && epaisseurRelative <= 0.68f;
                        appliquerRoche = estTop ? !topBois : !sideBois;
                    }
                    else
                        appliquerRoche = !sideBois;
                }
                else if (nomTagConnu && !(estRoche && estBois))
                    appliquerRoche = estRoche;
                else
                    appliquerRoche = estRoche && !estBois;

                bool estPlateauMarche = EstMeshPlateauMarcheFondation(mi, minY, maxY, hauteur);
                bool? rocheDepuisNomPlateau = ResoudreRocheDepuisNomMeshFondationMixte(nom);
                if (rocheDepuisNomPlateau.HasValue && mi.Mesh != null)
                {
                    Aabb aabbNom = TransformerAabb(mi.Transform, mi.Mesh.GetAabb());
                    if (aabbNom.Size.Y <= hauteur * 0.5f)
                        estPlateauMarche = true;
                }

                if (estPlateauMarche)
                {
                    mi.MaterialOverride = appliquerRoche
                        ? ObtenirMaterielRochePlateauFondation(idxRoche)
                        : ObtenirMaterielBoisPlateauFondation(essenceBois);
                }
                else
                {
                    RemplacerMeshParNormalesFacettes(mi);
                    if (appliquerRoche)
                        AppliquerMaterielObjet(mi, ItemPhysique.IdRocheMatiereMin + idxRoche, idxRoche, 0, 0, essenceBois);
                    else
                        mi.MaterialOverride = ArbreVivant.ObtenirMaterielBoisTriplanar(essenceBois);
                }
            }
            foreach (Node c in n.GetChildren())
                ParcourirMeshesFondation(c);
        }

        ParcourirMeshesFondation(modele);
        if (ancrerBaseAuSol)
            NormaliserDimensionsAncrerAuSol(modele, 4f, 1f, 4f);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Plancher bois carré 4,1×4,1×0,08 m, texture de l'essence craftée.</summary>
    public static void InstancierModeleSolBois(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/structure/sol/sol_boie.glb";
        float emprise = Joueur.PlancherEmpriseMetres;
        float epaisseur = Joueur.PlancherEpaisseurMetres;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        byte essenceBois = slot.IndexBotanique;
        if (essenceBois == Joueur.TagVarianteLiane || essenceBois == Joueur.TagVarianteHerbeSolide
            || essenceBois == Joueur.TagVarianteIntestin || essenceBois == Joueur.TagVarianteIntestinSolide)
            essenceBois = LSystem_Botanique.IndexChene;
        Material matBois = ObtenirMaterielSolBois(essenceBois);

        if (scene == null)
        {
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(emprise, epaisseur, emprise) },
                MaterialOverride = matBois
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matBois);
        if (ancrerBaseAuSol)
            NormaliserDimensionsPlancherAncrerAuSol(modele, emprise, epaisseur);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.72f);
        parent.AddChild(modele);
    }

    /// <summary>Plancher roche carré 4,1×4,1×0,08 m : GLB <c>sol_roche.glb</c>, texture triplanar selon roche craftée.</summary>
    public static void InstancierModeleSolRoche(Node3D parent, SlotInventaire slot, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/structure/sol/sol_roche.glb";
        float emprise = Joueur.PlancherEmpriseMetres;
        float epaisseur = Joueur.PlancherEpaisseurMetres;
        int idxRoche = Mathf.Clamp(slot.IndexChimique, 0, ItemPhysique.TableGeologique.Length - 1);
        Material matRoche = ObtenirMaterielSolRoche(idxRoche);
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);

        if (scene == null)
        {
            GD.PrintErr($"ZERO-K : GLB plancher roche introuvable ({cheminGlb}). Repli box {emprise}×{epaisseur} m.");
            var fb = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(emprise, epaisseur, emprise) },
                MaterialOverride = matRoche
            };
            parent.AddChild(fb);
            return;
        }

        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielPlancherSurMeshesGlb(modele, matRoche);
        if (ancrerBaseAuSol)
            NormaliserDimensionsPlancherAncrerAuSol(modele, emprise, epaisseur);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, 0.72f);
        parent.AddChild(modele);
    }
}
