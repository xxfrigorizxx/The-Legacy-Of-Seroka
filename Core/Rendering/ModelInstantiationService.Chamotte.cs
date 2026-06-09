using Godot;

public partial class Joueur
{
    private static StandardMaterial3D _materielChamotteCache;
    private static ImageTexture _textureChamotteCache;

    /// <summary>Texture procédurale chamotte (morceaux de céramique poreuse, gris-rose).</summary>
    public static ImageTexture ObtenirTextureChamotte()
    {
        if (_textureChamotteCache != null)
            return _textureChamotteCache;

        const int taille = 128;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float n1 = Mathf.Sin(x * 0.37f + 1.1f) * Mathf.Cos(y * 0.29f - 0.4f);
                float n2 = Mathf.Sin(x * 0.61f + y * 0.47f) * 0.45f;
                float pore = 0.12f * Mathf.Sin(x * 0.83f + y * 0.71f) * Mathf.Cos(x * 0.23f - y * 0.19f);
                float eclat = 0.08f * Mathf.Sin(x * 1.17f + y * 0.93f);
                float bruit = (n1 + n2) * 0.5f + pore + eclat;
                float r = 0.72f + bruit * 0.12f;
                float g = 0.58f + bruit * 0.10f;
                float b = 0.50f + bruit * 0.08f;
                img.SetPixel(x, y, new Color(r, g, b));
            }
        }

        _textureChamotteCache = ImageTexture.CreateFromImage(img);
        return _textureChamotteCache;
    }

    public static StandardMaterial3D ObtenirMaterielChamotte()
    {
        if (_materielChamotteCache != null)
            return _materielChamotteCache;

        _materielChamotteCache = new StandardMaterial3D
        {
            AlbedoTexture = ObtenirTextureChamotte(),
            AlbedoColor = new Color(0.94f, 0.90f, 0.86f),
            Roughness = 0.88f,
            Metallic = 0f,
            Uv1Scale = new Vector3(2.8f, 2.8f, 1f)
        };
        return _materielChamotteCache;
    }

    /// <summary>Morceaux de chamotte (sur-cuisson argile au four).</summary>
    public static void InstancierModeleChamotte(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.30f, bool ancrerBaseAuSol = false)
    {
        const string cheminGlb = "res://Modeles/transition/chamotte.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.06f, 0.10f) },
                MaterialOverride = ObtenirMaterielChamotte()
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauSurMeshes(modele, ObtenirMaterielChamotte());
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
