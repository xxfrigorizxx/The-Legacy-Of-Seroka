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
            Metallic = Mathf.Lerp(0f, 0.04f, facteurChaleur)
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
        if (facteurChaleur < 0f)
            facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(slot);
        Material mat = facteurChaleur <= 0.001f
            ? CreerMaterielBolTeinteProgressive(0f, ceramique: true)
            : CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: true);
        InstancierModeleMouleInterne(parent, tailleMaxMetres, ancrerBaseAuSol, mat);
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
