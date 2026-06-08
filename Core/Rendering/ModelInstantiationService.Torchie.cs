using Godot;

public partial class Joueur
{
    private static ShaderMaterial _materielTorchieCache;
    private static ImageTexture _textureTorchieCache;
    private static int _revisionTextureTorchieEnCache = -1;
    private const int RevisionCacheTextureTorchie = RevisionRenduTorchie;

    private static float BruitTorchieHash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
        h ^= h >> 13;
        h *= 1274126177;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    private static float BruitTorchieLisse(float x, float y, int seed)
    {
        int x0 = (int)Mathf.Floor(x);
        int y0 = (int)Mathf.Floor(y);
        float fx = x - x0;
        float fy = y - y0;
        float u = fx * fx * (3f - 2f * fx);
        float v = fy * fy * (3f - 2f * fy);
        float a = BruitTorchieHash(x0, y0, seed);
        float b = BruitTorchieHash(x0 + 1, y0, seed);
        float c = BruitTorchieHash(x0, y0 + 1, seed);
        float d = BruitTorchieHash(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    private static float BruitTorchieFractal(float x, float y, int octaves, int seed)
    {
        float sum = 0f;
        float amp = 0.55f;
        float freq = 1f;
        float norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += BruitTorchieLisse(x * freq, y * freq, seed + i * 17) * amp;
            norm += amp;
            amp *= 0.52f;
            freq *= 2.13f;
        }
        return sum / Mathf.Max(norm, 0.0001f);
    }

    /// <summary>Texture procédurale torchie (triplanar — le GLB Tripo n'a pas d'UVs exploitables).</summary>
    public static ImageTexture ObtenirTextureTorchie()
    {
        if (_textureTorchieCache != null && _revisionTextureTorchieEnCache == RevisionCacheTextureTorchie)
            return _textureTorchieCache;
        _textureTorchieCache = null;

        const int taille = 256;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        float cx = taille * 0.5f;
        float cy = taille * 0.5f;

        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float nx = x / (float)taille;
                float ny = y / (float)taille;

                float macro = BruitTorchieFractal(nx * 3.2f, ny * 3.2f, 4, 41);
                float grain = BruitTorchieFractal(nx * 22f + 2.7f, ny * 22f - 1.3f, 3, 73);
                float speckle = BruitTorchieHash(x, y, 119);

                float zoneArgile = Mathf.Clamp(macro * 1.25f + grain * 0.28f - 0.38f, 0f, 1f);
                Color argile = new Color(0.68f, 0.42f, 0.26f);
                Color boue = new Color(0.32f, 0.26f, 0.18f);
                Color baseCouleur = argile.Lerp(boue, (1f - zoneArgile) * 0.9f);

                float humidite = BruitTorchieFractal(nx * 11f, ny * 11f, 2, 5);
                baseCouleur = baseCouleur.Lerp(new Color(0.22f, 0.18f, 0.12f), Mathf.Clamp(humidite - 0.55f, 0f, 1f) * 0.55f);
                baseCouleur = baseCouleur.Lerp(new Color(0.78f, 0.62f, 0.40f), Mathf.Clamp(0.35f - humidite, 0f, 1f) * 0.45f);

                float micro = (grain - 0.5f) * 0.22f + (speckle - 0.5f) * 0.10f;
                float r = baseCouleur.R + micro;
                float g = baseCouleur.G + micro * 0.82f;
                float b = baseCouleur.B + micro * 0.50f;

                float fibreEncastree = 0f;
                for (int f = 0; f < 16; f++)
                {
                    float angle = 0.18f + f * 0.37f;
                    float ca = Mathf.Cos(angle);
                    float sa = Mathf.Sin(angle);
                    float ox = (f % 5 - 2) * 13f;
                    float oy = ((f * 3) % 7 - 3) * 11f;
                    float dx = x - cx - ox;
                    float dy = y - cy - oy;
                    float proj = dx * ca + dy * sa;
                    float perp = -dx * sa + dy * ca;
                    float ondulation = Mathf.Sin(proj * 0.13f + f * 8.1f) * 5f;
                    float epaisseur = 1.4f + (f % 3) * 0.4f;
                    float dist = Mathf.Abs(perp + ondulation);
                    if (dist < epaisseur)
                        fibreEncastree = Mathf.Max(fibreEncastree, 1f - dist / epaisseur);
                }

                float touffe = BruitTorchieFractal(nx * 28f, ny * 28f, 2, 29);
                if (touffe > 0.70f)
                    fibreEncastree = Mathf.Max(fibreEncastree, (touffe - 0.70f) * 4.2f);
                fibreEncastree = Mathf.Clamp(fibreEncastree, 0f, 1f);

                float fibreExposee = 0f;
                if (speckle > 0.52f && touffe > 0.58f)
                    fibreExposee = (touffe - 0.58f) * 2.5f * (speckle - 0.30f);
                fibreExposee = Mathf.Clamp(fibreExposee, 0f, 1f);

                Color herbeFondu = new Color(0.58f, 0.48f, 0.32f);
                r = Mathf.Lerp(r, herbeFondu.R, fibreEncastree * 0.88f);
                g = Mathf.Lerp(g, herbeFondu.G, fibreEncastree * 0.88f);
                b = Mathf.Lerp(b, herbeFondu.B, fibreEncastree * 0.88f);

                Color paille = new Color(0.76f, 0.66f, 0.38f);
                r = Mathf.Lerp(r, paille.R, fibreExposee * 0.92f);
                g = Mathf.Lerp(g, paille.G, fibreExposee * 0.92f);
                b = Mathf.Lerp(b, paille.B, fibreExposee * 0.92f);

                img.SetPixel(x, y, new Color(
                    Mathf.Clamp(r, 0f, 1f),
                    Mathf.Clamp(g, 0f, 1f),
                    Mathf.Clamp(b, 0f, 1f)));
            }
        }

        _textureTorchieCache = ImageTexture.CreateFromImage(img);
        _revisionTextureTorchieEnCache = RevisionCacheTextureTorchie;
        return _textureTorchieCache;
    }

    public static ShaderMaterial ObtenirMaterielTorchie()
    {
        if (_materielTorchieCache != null && _revisionTextureTorchieEnCache == RevisionCacheTextureTorchie)
            return _materielTorchieCache;
        _materielTorchieCache = null;

        var shader = GD.Load<Shader>("res://shaders/TorchieMat.gdshader");
        _materielTorchieCache = new ShaderMaterial
        {
            Shader = shader
        };
        _materielTorchieCache.SetShaderParameter("albedo_tex", ObtenirTextureTorchie());
        _materielTorchieCache.SetShaderParameter("echelle_triplanar", 7.0f);
        return _materielTorchieCache;
    }

    private static void AppliquerMateriauTorchieSurMeshes(Node racine)
    {
        ShaderMaterial materiau = ObtenirMaterielTorchie();
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = materiau;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    /// <summary>Brique de torchie (GLB + shader triplanar : contourne UVs Tripo invalides).</summary>
    public static void InstancierModeleTorchie(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/materials/travailler/Torchie.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.08f, 0.10f) },
                MaterialOverride = ObtenirMaterielTorchie()
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauTorchieSurMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
