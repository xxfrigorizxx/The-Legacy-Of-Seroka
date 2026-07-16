using Godot;

public partial class Joueur
{
    private static StandardMaterial3D _materielArgileHumidifieeCache;
    private static ImageTexture _textureArgileHumidifieeCache;

    private static readonly Color CouleurArgileFroide = new(0.46f, 0.32f, 0.24f);
    private static readonly Color CouleurArgileChaude = new(0.95f, 0.42f, 0.14f);
    private static readonly Color CouleurCeramiqueFroide = new(0.78f, 0.62f, 0.48f);
    private static readonly Color CouleurCeramiqueChaude = new(0.88f, 0.36f, 0.12f);

    /// <summary>Texture procédurale argile humide (brun-rouge, grain irrégulier).</summary>
    public static ImageTexture ObtenirTextureArgileHumidifiee()
    {
        if (_textureArgileHumidifieeCache != null)
            return _textureArgileHumidifieeCache;

        const int taille = 128;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float n1 = Mathf.Sin(x * 0.19f) * Mathf.Cos(y * 0.23f);
                float n2 = Mathf.Sin(x * 0.41f + y * 0.31f) * 0.5f;
                float humid = 0.06f * Mathf.Sin(x * 0.27f + y * 0.19f);
                float bruit = (n1 + n2) * 0.5f + humid;
                float r = 0.50f + bruit * 0.14f;
                float g = 0.34f + bruit * 0.10f;
                float b = 0.24f + bruit * 0.07f;
                img.SetPixel(x, y, new Color(r, g, b));
            }
        }

        _textureArgileHumidifieeCache = ImageTexture.CreateFromImage(img);
        return _textureArgileHumidifieeCache;
    }

    public static StandardMaterial3D ObtenirMaterielArgileHumidifiee()
    {
        if (_materielArgileHumidifieeCache != null)
            return _materielArgileHumidifieeCache;

        _materielArgileHumidifieeCache = new StandardMaterial3D
        {
            AlbedoTexture = ObtenirTextureArgileHumidifiee(),
            AlbedoColor = new Color(0.92f, 0.88f, 0.84f),
            Roughness = 0.78f,
            Metallic = 0f,
            Uv1Scale = new Vector3(2.5f, 2.5f, 1f)
        };
        return _materielArgileHumidifieeCache;
    }

    /// <summary>Matériau bol avec teinte thermique progressive (0 = froid, 1 = incandescent).</summary>
    public static StandardMaterial3D CreerMaterielBolTeinteProgressive(float facteurChaleur, bool ceramique = false)
    {
        facteurChaleur = Mathf.Clamp(facteurChaleur, 0f, 1f);
        Color froid = ceramique ? CouleurCeramiqueFroide : CouleurArgileFroide;
        Color chaud = ceramique ? CouleurCeramiqueChaude : CouleurArgileChaude;
        Color albedo = froid.Lerp(chaud, facteurChaleur);

        var mat = new StandardMaterial3D
        {
            AlbedoColor = albedo,
            Roughness = Mathf.Lerp(ceramique ? 0.58f : 0.78f, ceramique ? 0.68f : 0.72f, facteurChaleur),
            Metallic = Mathf.Lerp(0f, 0.04f, facteurChaleur),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled
        };

        if (!ceramique && facteurChaleur < 0.92f)
        {
            mat.AlbedoTexture = ObtenirTextureArgileHumidifiee();
            mat.AlbedoColor = new Color(0.92f, 0.88f, 0.84f).Lerp(albedo, facteurChaleur);
        }

        if (facteurChaleur > 0.04f)
        {
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.75f, 0.22f, 0.05f).Lerp(new Color(0.92f, 0.28f, 0.06f), facteurChaleur);
            mat.EmissionEnergyMultiplier = facteurChaleur * (ceramique ? 0.55f : 0.85f);
        }

        return mat;
    }

    private static void AppliquerMateriauSurMeshes(Node racine, Material materiau)
    {
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    private static void AppliquerMateriauArgileHumidifieeSurMeshes(Node racine)
        => AppliquerMateriauSurMeshes(racine, ObtenirMaterielArgileHumidifiee());

    /// <summary>Motte d'argile humidifiée (GLB + texture procédurale).</summary>
    public static void InstancierModeleArgileHumidifiee(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.28f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/argile_humidifier.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new SphereMesh { Radius = 0.10f, Height = 0.08f, RadialSegments = 12, Rings = 8 },
                MaterialOverride = ObtenirMaterielArgileHumidifiee()
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauArgileHumidifieeSurMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Bol modelé en argile — teinte thermique progressive (0–1).</summary>
    public static void InstancierModeleBolArgile(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false, float facteurChauffe = 0f)
    {
        Material mat = facteurChauffe <= 0.001f
            ? ObtenirMaterielArgileHumidifiee()
            : CreerMaterielBolTeinteProgressive(facteurChauffe, ceramique: false);
        InstancierModeleBolArgileInterne(parent, tailleMaxMetres, ancrerBaseAuSol, mat);
    }

    /// <summary>Bol en céramique — teinte progressive (facteurChaleur 1 = chaud, 0 = refroidi ; -1 = auto depuis le slot).</summary>
    public static void InstancierModeleBolCeramique(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false, float facteurChaleur = -1f)
    {
        if (facteurChaleur < 0f)
            facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(slot);
        Material mat = facteurChaleur <= 0.001f
            ? CreerMaterielBolTeinteProgressive(0f, ceramique: true)
            : CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: true);
        InstancierModeleBolArgileInterne(parent, tailleMaxMetres, ancrerBaseAuSol, mat);
    }

    /// <summary>Bol en céramique plein d'étain (GLB dédié : bowl_ceramique_etain.glb).</summary>
    public static void InstancierModeleBolCeramiqueEtain(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/bowl_ceramique_etain.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            InstancierModeleBolCeramique(parent, slot, tailleMaxMetres, ancrerBaseAuSol, 0f);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauxBolCeramiqueEtainSurMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    private static ImageTexture _textureCeramiqueBolCache;
    private static Shader _shaderTriplanarAlbedo;
    private static Material _materielTriplanarCeramiqueBolCache;
    private static Material _materielTriplanarEtainBolCache;

    /// <summary>Texture procédurale céramique (beige grainé, alignée bol refroidi).</summary>
    public static ImageTexture ObtenirTextureCeramiqueBol()
    {
        if (_textureCeramiqueBolCache != null)
            return _textureCeramiqueBolCache;

        const int taille = 128;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float nx = x / (float)taille;
                float ny = y / (float)taille;
                float grain = Mathf.Sin(nx * 16.2f + ny * 12.7f) * Mathf.Cos(ny * 18.1f - nx * 9.3f);
                float pore = Mathf.Abs(Mathf.Sin(nx * 41f - ny * 27f)) > 0.93f ? 0.06f : 0f;
                float bruit = grain * 0.5f + pore;
                float r = 0.76f + bruit * 0.08f;
                float g = 0.60f + bruit * 0.06f;
                float b = 0.46f + bruit * 0.05f;
                img.SetPixel(x, y, new Color(r, g, b));
            }
        }

        _textureCeramiqueBolCache = ImageTexture.CreateFromImage(img);
        return _textureCeramiqueBolCache;
    }

    private static Shader ObtenirShaderTriplanarAlbedo()
    {
        if (_shaderTriplanarAlbedo != null)
            return _shaderTriplanarAlbedo;
        const string chemin = "res://shaders/TriplanarAlbedoMat.gdshader";
        _shaderTriplanarAlbedo = ResourceLoader.Exists(chemin) ? GD.Load<Shader>(chemin) : null;
        return _shaderTriplanarAlbedo;
    }

    private static Material ObtenirMaterielTriplanarCeramiqueBol()
    {
        if (_materielTriplanarCeramiqueBolCache != null)
            return _materielTriplanarCeramiqueBolCache;

        Shader shader = ObtenirShaderTriplanarAlbedo();
        if (shader == null)
        {
            _materielTriplanarCeramiqueBolCache = CreerMaterielBolTeinteProgressive(0f, ceramique: true);
            return _materielTriplanarCeramiqueBolCache;
        }

        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("albedo_tex", ObtenirTextureCeramiqueBol());
        mat.SetShaderParameter("echelle_triplanar", 5.5f);
        mat.SetShaderParameter("albedo_tinte", new Color(0.94f, 0.90f, 0.86f));
        mat.SetShaderParameter("roughness", 0.58f);
        mat.SetShaderParameter("metallic", 0.0f);
        _materielTriplanarCeramiqueBolCache = mat;
        return _materielTriplanarCeramiqueBolCache;
    }

    private static Material ObtenirMaterielTriplanarEtainBol()
    {
        if (_materielTriplanarEtainBolCache != null)
            return _materielTriplanarEtainBolCache;

        Shader shader = ObtenirShaderTriplanarAlbedo();
        Texture2D texEtain = ResourceLoader.Exists("res://textures/items/minerais/166_etain.png")
            ? GD.Load<Texture2D>("res://textures/items/minerais/166_etain.png")
            : null;
        if (shader == null || texEtain == null)
        {
            _materielTriplanarEtainBolCache = ObtenirMaterielEtain();
            return _materielTriplanarEtainBolCache;
        }

        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("albedo_tex", texEtain);
        mat.SetShaderParameter("echelle_triplanar", 4.5f);
        mat.SetShaderParameter("albedo_tinte", new Color(0.96f, 0.96f, 0.98f));
        mat.SetShaderParameter("roughness", 0.34f);
        mat.SetShaderParameter("metallic", 0.62f);
        _materielTriplanarEtainBolCache = mat;
        return _materielTriplanarEtainBolCache;
    }

    private static float ObtenirVolumeAabbMeshLocal(MeshInstance3D mi)
    {
        if (mi?.Mesh == null)
            return float.MaxValue;
        Vector3 s = mi.Mesh.GetAabb().Size;
        return Mathf.Max(0.000001f, s.X * s.Y * s.Z);
    }

    private static void CollecterMeshesRecursif(Node racine, System.Collections.Generic.List<MeshInstance3D> sortie)
    {
        if (racine is MeshInstance3D mi && mi.Mesh != null)
            sortie.Add(mi);
        foreach (Node enfant in racine.GetChildren())
            CollecterMeshesRecursif(enfant, sortie);
    }

    private static Material _materielEtainSolidifieArgenteCache;

    private static bool EstNomMeshRemplissageMetalOuScorie(string nomMesh)
    {
        if (string.IsNullOrEmpty(nomMesh))
            return false;
        string nom = nomMesh.ToLowerInvariant();
        return nom.Contains("liquid") || nom.Contains("liquide") || nom.Contains("metal")
            || nom.Contains("etain") || nom.Contains("tin") || nom.Contains("fill")
            || nom.Contains("scorie") || nom.Contains("slag")
            || nom.Contains("ore") || nom.Contains("minerai") || nom.Contains("lingo");
    }

    private static bool EstNomMeshCoqueCeramique(string nomMesh)
    {
        if (string.IsNullOrEmpty(nomMesh))
            return false;
        string nom = nomMesh.ToLowerInvariant();
        return nom.Contains("bowl") || nom.Contains("bol") || nom.Contains("ceram")
            || nom.Contains("moule") || nom.Contains("tripo");
    }

    private static MeshInstance3D ResoudreMeshRemplissage(
        System.Collections.Generic.List<MeshInstance3D> meshes,
        bool remplissageSurMeshLePlusGrand,
        bool utiliserHeuristiqueNoms = true)
    {
        if (utiliserHeuristiqueNoms)
        {
            MeshInstance3D parNomFill = null;
            int nbCoques = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                if (EstNomMeshRemplissageMetalOuScorie(meshes[i].Name))
                    parNomFill = meshes[i];
                if (EstNomMeshCoqueCeramique(meshes[i].Name))
                    nbCoques++;
            }
            if (parNomFill != null && nbCoques > 0)
                return parNomFill;
        }

        MeshInstance3D choix = meshes[0];
        float volRef = ObtenirVolumeAabbMeshLocal(choix);
        for (int i = 1; i < meshes.Count; i++)
        {
            float vol = ObtenirVolumeAabbMeshLocal(meshes[i]);
            bool meilleur = remplissageSurMeshLePlusGrand ? vol > volRef : vol < volRef;
            if (meilleur)
            {
                volRef = vol;
                choix = meshes[i];
            }
        }
        return choix;
    }

  private static Material ObtenirMaterielEtainLiquideArgente(float facteurChaleur)
    {
        facteurChaleur = Mathf.Clamp(facteurChaleur, 0f, 1f);
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.82f, 0.85f, 0.90f).Lerp(new Color(0.94f, 0.96f, 0.99f), facteurChaleur),
            Metallic = Mathf.Lerp(0.70f, 0.94f, facteurChaleur),
            Roughness = Mathf.Lerp(0.32f, 0.08f, facteurChaleur),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled
        };
        if (facteurChaleur > 0.08f)
        {
            mat.EmissionEnabled = true;
            mat.Emission = new Color(0.65f, 0.70f, 0.82f);
            mat.EmissionEnergyMultiplier = facteurChaleur * 0.35f;
        }
        return mat;
    }

    private static Material ObtenirMaterielEtainSolidifieArgente()
    {
        if (_materielEtainSolidifieArgenteCache != null)
            return _materielEtainSolidifieArgenteCache;
        _materielEtainSolidifieArgenteCache = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.80f, 0.84f),
            Metallic = 0.68f,
            Roughness = 0.38f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Disabled
        };
        return _materielEtainSolidifieArgenteCache;
    }

    private static Material ObtenirMaterielCeramiqueBolOpaque()
    {
        Material baseMat = ObtenirMaterielTriplanarCeramiqueBol();
        return baseMat.Duplicate() as Material ?? baseMat;
    }

    private static void AppliquerMateriauxDeuxZones(
        Node racine,
        Material matCoque,
        Material matRemplissage,
        bool remplissageSurMeshLePlusGrand,
        bool utiliserHeuristiqueNoms = true)
    {
        var meshes = new System.Collections.Generic.List<MeshInstance3D>();
        CollecterMeshesRecursif(racine, meshes);
        if (meshes.Count == 0)
            return;
        if (meshes.Count == 1)
        {
            meshes[0].MaterialOverride = matCoque;
            meshes[0].CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            return;
        }

        MeshInstance3D meshRemplissage = ResoudreMeshRemplissage(meshes, remplissageSurMeshLePlusGrand, utiliserHeuristiqueNoms);
        foreach (MeshInstance3D mi in meshes)
        {
            mi.MaterialOverride = mi == meshRemplissage ? matRemplissage : matCoque;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }
    }

    /// <summary>Coque = céramique ; remplissage = étain / scorie (heuristique volume + noms de mesh).</summary>
    private static void AppliquerMateriauxBolEtainSurMeshes(Node racine, bool remplissageSurMeshLePlusGrand = true)
    {
        AppliquerMateriauxDeuxZones(
            racine,
            ObtenirMaterielCeramiqueBolOpaque(),
            ObtenirMaterielTriplanarEtainBol(),
            remplissageSurMeshLePlusGrand);
    }

    private static void AppliquerMateriauxBolEtainSurMeshesAvecChaleur(
        Node racine,
        float facteurChaleur,
        bool remplissageSurMeshLePlusGrand,
        bool liquide,
        bool utiliserHeuristiqueNoms = true)
    {
        bool chaud = facteurChaleur > 0.04f;
        Material matCoque = chaud
            ? CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false)
            : ObtenirMaterielCeramiqueBolOpaque();
        Material matMetal = chaud && liquide
            ? CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false)
            : liquide
                ? ObtenirMaterielEtainLiquideArgente(Mathf.Max(facteurChaleur, 0.35f))
                : ObtenirMaterielEtainSolidifieArgente();
        AppliquerMateriauxDeuxZones(racine, matCoque, matMetal, remplissageSurMeshLePlusGrand, utiliserHeuristiqueNoms);
    }

    /// <summary>bowl_plain2.glb — coque = grand mesh, étain liquide = petit mesh (sans heuristique de noms).</summary>
    private static void AppliquerMateriauxBolPlain2SurMeshes(Node racine, float facteurChaleur, bool liquide)
    {
        AppliquerMateriauxBolEtainSurMeshesAvecChaleur(
            racine,
            facteurChaleur,
            remplissageSurMeshLePlusGrand: false,
            liquide,
            utiliserHeuristiqueNoms: false);
    }

    /// <summary>Coque = céramique (AABB plus petit) ; remplissage minéral = étain (AABB plus grand).</summary>
    private static void AppliquerMateriauxBolCeramiqueEtainSurMeshes(Node racine)
        => AppliquerMateriauxBolEtainSurMeshes(racine, remplissageSurMeshLePlusGrand: true);

    private static void InstancierModeleBolArgileInterne(Node3D parent, float tailleMaxMetres, bool ancrerBaseAuSol, Material materiau)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/bowl_modeler.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new SphereMesh { Radius = 0.09f, Height = 0.05f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = materiau
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauSurMeshes(modele, materiau);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    /// <summary>Moule modelé en argile — teinte thermique progressive (0–1).</summary>
    public static void InstancierModeleMouleArgile(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.32f, bool ancrerBaseAuSol = false, float facteurChauffe = 0f)
    {
        Material mat = facteurChauffe <= 0.001f
            ? ObtenirMaterielArgileHumidifiee()
            : CreerMaterielBolTeinteProgressive(facteurChauffe, ceramique: false);
        InstancierModeleMouleInterne(parent, tailleMaxMetres, ancrerBaseAuSol, mat);
    }

    /// <summary>Moule en céramique — teinte progressive (facteurChaleur 1 = chaud, 0 = refroidi ; -1 = auto depuis le slot).</summary>
    public static void InstancierModeleMouleCeramique(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.32f, bool ancrerBaseAuSol = false, float facteurChaleur = -1f)
    {
        if (FourTorchieThermodynamique.EstMouleEtainFonduChaud(slot)
            || FourTorchieThermodynamique.EstMouleEtainSolidifie(slot))
        {
            if (facteurChaleur < 0f)
                facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(slot);
            InstancierModeleMouleCeramiqueEtainPlain(parent, slot, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur);
            return;
        }

        if (facteurChaleur < 0f)
            facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(slot);
        Material mat = facteurChaleur <= 0.001f
            ? CreerMaterielBolTeinteProgressive(0f, ceramique: true)
            : CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: true);
        InstancierModeleMouleInterne(parent, tailleMaxMetres, ancrerBaseAuSol, mat);
    }

    /// <summary>Bol étain fondu liquide chaud — bowl_plain2.glb.</summary>
    public static void InstancierModeleBolEtainFonduChaud(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false, float facteurChaleur = -1f)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/bowl_plain2.glb";
        if (facteurChaleur < 0f)
            facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolEtainFonduSlot(slot);
        InstancierModeleBolGlbAvecMateriauxEtain(parent, cheminGlb, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur, liquide: true, remplissageSurMeshLePlusGrand: false);
    }

    /// <summary>Bol étain solidifié (refroidi) — bowl_plain2.glb, étain durci.</summary>
    public static void InstancierModeleBolEtainSolidifie(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/bowl_plain2.glb";
        InstancierModeleBolGlbAvecMateriauxEtain(parent, cheminGlb, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur: 0f, liquide: false, remplissageSurMeshLePlusGrand: false);
    }

    /// <summary>Bol céramique avec scories — bowl_ceramique_scorie.glb.</summary>
    public static void InstancierModeleBolCeramiqueScorie(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false, float facteurChaleur = -1f)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/bowl_ceramique_scorie.glb";
        if (facteurChaleur < 0f)
            facteurChaleur = ItemPhysique.ObtenirFacteurChaleurBolScorieDepuisSlot(slot);
        InstancierModeleBolGlbAvecMateriauxScorie(parent, cheminGlb, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur);
    }

    /// <summary>Moule céramique rempli d'étain fondu — Moule_lingo._plain.glb.</summary>
    public static void InstancierModeleMouleCeramiqueEtainPlain(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.32f, bool ancrerBaseAuSol = false, float facteurChaleur = 0f)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/Moule_lingo._plain.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            InstancierModeleMouleCeramique(parent, slot, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauxMouleEtainPlainSurMeshes(modele, facteurChaleur);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    private static void InstancierModeleBolGlbAvecMateriauxEtain(
        Node3D parent,
        string cheminGlb,
        float tailleMaxMetres,
        bool ancrerBaseAuSol,
        float facteurChaleur,
        bool liquide,
        bool remplissageSurMeshLePlusGrand,
        bool utiliserHeuristiqueNoms = true)
    {
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            InstancierModeleBolCeramique(parent, new SlotInventaire { ID = Joueur.IdObjetBolCeramique }, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        const string cheminPlain2 = "res://Modeles/materials/travailler/bowl_plain2.glb";
        bool estBolPlain2 = cheminGlb == cheminPlain2;
        if (estBolPlain2)
            AppliquerMateriauxBolPlain2SurMeshes(modele, facteurChaleur, liquide);
        else
            AppliquerMateriauxBolEtainSurMeshesAvecChaleur(modele, facteurChaleur, remplissageSurMeshLePlusGrand, liquide, utiliserHeuristiqueNoms);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    private static void InstancierModeleBolGlbAvecMateriauxScorie(Node3D parent, string cheminGlb, float tailleMaxMetres, bool ancrerBaseAuSol, float facteurChaleur)
    {
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            InstancierModeleBolCeramique(parent, new SlotInventaire { ID = Joueur.IdObjetBolCeramique }, tailleMaxMetres, ancrerBaseAuSol, facteurChaleur);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauxBolScorieSurMeshes(modele);
        if (facteurChaleur > 0.04f)
            AppliquerChaleurArgileSurMeshesCeramique(modele, facteurChaleur, remplissageSurMeshLePlusGrand: false);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }

    private static Material _materielTriplanarScorieBolCache;

    private static Material ObtenirMaterielTriplanarScorieBol()
    {
        if (_materielTriplanarScorieBolCache != null)
            return _materielTriplanarScorieBolCache;

        Shader shader = ObtenirShaderTriplanarAlbedo();
        if (shader == null)
        {
            _materielTriplanarScorieBolCache = CreerMaterielBolTeinteProgressive(0f, ceramique: true);
            return _materielTriplanarScorieBolCache;
        }

        const int taille = 128;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float bruit = Mathf.Sin(x * 0.42f + y * 0.37f) * Mathf.Cos(x * 0.19f - y * 0.51f);
                float r = 0.22f + bruit * 0.06f;
                float g = 0.20f + bruit * 0.05f;
                float b = 0.18f + bruit * 0.04f;
                img.SetPixel(x, y, new Color(r, g, b));
            }
        }

        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("albedo_tex", ImageTexture.CreateFromImage(img));
        mat.SetShaderParameter("echelle_triplanar", 5f);
        mat.SetShaderParameter("albedo_tinte", new Color(0.55f, 0.48f, 0.42f));
        mat.SetShaderParameter("roughness", 0.88f);
        mat.SetShaderParameter("metallic", 0.05f);
        _materielTriplanarScorieBolCache = mat;
        return _materielTriplanarScorieBolCache;
    }

    private static void AppliquerMateriauxBolScorieSurMeshes(Node racine)
    {
        AppliquerMateriauxDeuxZones(
            racine,
            ObtenirMaterielCeramiqueBolOpaque(),
            ObtenirMaterielTriplanarScorieBol(),
            remplissageSurMeshLePlusGrand: false);
    }

    private static void AppliquerMateriauxMouleEtainPlainSurMeshes(Node racine, float facteurChaleur)
    {
        bool chaud = facteurChaleur > 0.04f;
        Material matCoque = chaud
            ? CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false)
            : CreerMaterielBolTeinteProgressive(0f, ceramique: true);
        Material matMetal = chaud
            ? CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false)
            : ObtenirMaterielEtainSolidifieArgente();
        AppliquerMateriauxDeuxZones(racine, matCoque, matMetal, remplissageSurMeshLePlusGrand: false, utiliserHeuristiqueNoms: false);
    }

    /// <summary>Teinte chauffe argile sur la coque céramique uniquement (remplissage inchangé).</summary>
    private static void AppliquerChaleurArgileSurMeshesCeramique(Node racine, float facteurChaleur, bool remplissageSurMeshLePlusGrand = true)
    {
        if (facteurChaleur <= 0.04f)
            return;

        var meshes = new System.Collections.Generic.List<MeshInstance3D>();
        CollecterMeshesRecursif(racine, meshes);
        if (meshes.Count <= 1)
        {
            if (meshes.Count == 1)
                meshes[0].MaterialOverride = CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false);
            return;
        }

        MeshInstance3D meshRemplissage = meshes[0];
        float volRef = ObtenirVolumeAabbMeshLocal(meshRemplissage);
        for (int i = 1; i < meshes.Count; i++)
        {
            float vol = ObtenirVolumeAabbMeshLocal(meshes[i]);
            bool meilleur = remplissageSurMeshLePlusGrand ? vol > volRef : vol < volRef;
            if (meilleur)
            {
                volRef = vol;
                meshRemplissage = meshes[i];
            }
        }

        Material matChaud = CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false);
        foreach (MeshInstance3D mi in meshes)
        {
            if (mi != meshRemplissage)
                mi.MaterialOverride = matChaud;
        }
    }

    private static void InstancierModeleMouleInterne(Node3D parent, float tailleMaxMetres, bool ancrerBaseAuSol, Material materiau)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/Moule_lingo.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.06f, 0.22f) },
                MaterialOverride = materiau
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauSurMeshes(modele, materiau);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
