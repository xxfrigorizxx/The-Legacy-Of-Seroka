using Godot;
using System;

public partial class Joueur
{
    private static string ObtenirCheminModeleCharbon(int idObjet) => idObjet switch
    {
        IdObjetCharbonBasseQualite => "res://Modeles/materials/Minerais/Charbon_basse_qualiter.glb",
        IdObjetCharbonMoyenneQualite => "res://Modeles/materials/Minerais/charbon_moyen_qualiter.glb",
        IdObjetCharbonBonneQualite => "res://Modeles/materials/Minerais/Charbon_bonne_qualiter.glb",
        IdObjetCharbonAntracite => "res://Modeles/materials/Minerais/Charbon_antracite_qualiter.glb",
        _ => ""
    };

    private static string ObtenirCheminModeleQuartz(int idObjet) => idObjet switch
    {
        IdObjetQuartz => "res://Modeles/materials/Minerais/quartz.glb",
        IdObjetQuartzPur => "res://Modeles/materials/Minerais/quartz_pure.glb",
        _ => ""
    };

    private static string ObtenirCheminTextureQuartz(int idObjet) => idObjet switch
    {
        IdObjetQuartz => "res://textures/items/minerais/164_quartz.png",
        IdObjetQuartzPur => "res://textures/items/minerais/165_quartz_pur.png",
        _ => ""
    };

    private const string CheminModeleEtain = "res://Modeles/materials/Minerais/etain.glb";
    private const string CheminTextureEtain = "res://textures/items/minerais/166_etain.png";

    private static readonly StandardMaterial3D[] MaterielsCharbonCache = new StandardMaterial3D[4];
    private static readonly Material[] MaterielsQuartzCache = new Material[2];
    private static StandardMaterial3D _materielEtainCache;
    private static Shader _shaderMineraiQuartz;

    /// <summary>Matériau noir mat ; anthracite = noir profond avec léger reflet.</summary>
    public static StandardMaterial3D ObtenirMaterielCharbon(int idObjet)
    {
        int idx = idObjet switch
        {
            IdObjetCharbonBasseQualite => 0,
            IdObjetCharbonMoyenneQualite => 1,
            IdObjetCharbonBonneQualite => 2,
            IdObjetCharbonAntracite => 3,
            _ => -1
        };
        if (idx < 0)
            return new StandardMaterial3D { AlbedoColor = Colors.Black, Roughness = 1f, Metallic = 0f };

        if (MaterielsCharbonCache[idx] != null)
            return MaterielsCharbonCache[idx];

        StandardMaterial3D mat = idObjet == IdObjetCharbonAntracite
            ? new StandardMaterial3D
            {
                AlbedoColor = new Color(0.006f, 0.006f, 0.008f),
                Roughness = 0.38f,
                Metallic = 0.22f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
                NormalEnabled = false,
                RimEnabled = false
            }
            : new StandardMaterial3D
            {
                AlbedoColor = idObjet switch
                {
                    IdObjetCharbonBasseQualite => new Color(0.16f, 0.16f, 0.16f),
                    IdObjetCharbonMoyenneQualite => new Color(0.08f, 0.08f, 0.08f),
                    IdObjetCharbonBonneQualite => new Color(0.018f, 0.018f, 0.018f),
                    _ => new Color(0.12f, 0.12f, 0.12f)
                },
                Roughness = 1f,
                Metallic = 0f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
                NormalEnabled = false,
                RimEnabled = false
            };

        MaterielsCharbonCache[idx] = mat;
        return mat;
    }

    private static Shader ObtenirShaderMineraiQuartz()
    {
        if (_shaderMineraiQuartz != null)
            return _shaderMineraiQuartz;
        const string chemin = "res://shaders/MineraiQuartz.gdshader";
        _shaderMineraiQuartz = ResourceLoader.Exists(chemin) ? GD.Load<Shader>(chemin) : null;
        return _shaderMineraiQuartz;
    }

    /// <summary>Quartz : laiteux veiné (shader + texture procédurale) ; pur : blanc translucide type cristal.</summary>
    public static Material ObtenirMaterielQuartz(int idObjet)
    {
        int idx = idObjet switch
        {
            IdObjetQuartz => 0,
            IdObjetQuartzPur => 1,
            _ => -1
        };
        if (idx < 0)
            return new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.91f, 0.88f), Roughness = 0.55f };

        if (MaterielsQuartzCache[idx] != null)
            return MaterielsQuartzCache[idx];

        Shader shader = ObtenirShaderMineraiQuartz();
        bool pur = idObjet == IdObjetQuartzPur;
        string cheminTex = ObtenirCheminTextureQuartz(idObjet);
        Texture2D albedo = ResourceLoader.Exists(cheminTex) ? GD.Load<Texture2D>(cheminTex) : null;

        if (shader == null)
        {
            var fallback = new StandardMaterial3D
            {
                AlbedoTexture = albedo,
                AlbedoColor = pur ? Colors.White : new Color(0.94f, 0.93f, 0.90f),
                Roughness = pur ? 0.12f : 0.38f,
                Transparency = pur ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
            };
            MaterielsQuartzCache[idx] = fallback;
            return fallback;
        }

        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("albedo_tex", albedo);
        mat.SetShaderParameter("mode_pur", pur ? 1.0f : 0.0f);
        mat.SetShaderParameter("teinte_base", pur
            ? new Color(1.04f, 1.04f, 1.02f, 1f)
            : new Color(0.97f, 0.96f, 0.93f, 1f));
        if (pur)
            mat.RenderPriority = 1;

        MaterielsQuartzCache[idx] = mat;
        return mat;
    }

    private static void AppliquerMateriauCharbonSurMeshes(Node racine, int idObjet)
    {
        Material materiau = ObtenirMaterielCharbon(idObjet);
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    private static void AppliquerMaterielQuartzSurMeshes(Node racine, int idObjet)
    {
        Material materiau = ObtenirMaterielQuartz(idObjet);
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    /// <summary>Étain : roche grise veinée d'argent (texture procédurale + reflets métalliques).</summary>
    public static StandardMaterial3D ObtenirMaterielEtain()
    {
        if (_materielEtainCache != null)
            return _materielEtainCache;

        Texture2D albedo = ResourceLoader.Exists(CheminTextureEtain) ? GD.Load<Texture2D>(CheminTextureEtain) : null;
        _materielEtainCache = new StandardMaterial3D
        {
            AlbedoTexture = albedo,
            AlbedoColor = new Color(0.96f, 0.96f, 0.98f),
            Roughness = 0.34f,
            Metallic = 0.62f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
            NormalEnabled = false,
            RimEnabled = false
        };
        return _materielEtainCache;
    }

    private static void AppliquerMateriauEtainSurMeshes(Node racine)
    {
        Material materiau = ObtenirMaterielEtain();
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    /// <summary>Morceau de charbon miné (GLB) — qualité selon l'ID objet.</summary>
    public static void InstancierModeleCharbon(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.22f)
    {
        if (!EstIdCharbonRecolte(slot.ID))
            return;
        string chemin = ObtenirCheminModeleCharbon(slot.ID);
        if (string.IsNullOrEmpty(chemin) || !ResourceLoader.Exists(chemin))
            return;

        PackedScene scene = GD.Load<PackedScene>(chemin);
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauCharbonSurMeshes(modele, slot.ID);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Morceau de quartz miné (GLB) — quartz ou quartz pur.</summary>
    public static void InstancierModeleQuartz(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.22f)
    {
        if (!EstIdQuartzRecolte(slot.ID))
            return;
        string chemin = ObtenirCheminModeleQuartz(slot.ID);
        if (string.IsNullOrEmpty(chemin) || !ResourceLoader.Exists(chemin))
            return;

        PackedScene scene = GD.Load<PackedScene>(chemin);
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMaterielQuartzSurMeshes(modele, slot.ID);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Morceau de minerai d'étain miné (GLB).</summary>
    public static void InstancierModeleEtain(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.22f)
    {
        if (!EstIdEtainRecolte(slot.ID))
            return;
        if (!ResourceLoader.Exists(CheminModeleEtain))
            return;

        PackedScene scene = GD.Load<PackedScene>(CheminModeleEtain);
        if (scene == null) return;

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauEtainSurMeshes(modele);
        NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
