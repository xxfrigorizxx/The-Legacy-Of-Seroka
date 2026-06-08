using Godot;

public partial class Joueur
{
    private static StandardMaterial3D _materielScorieFourTorchieCache;
    private static StandardMaterial3D _materielBoisBruleFourCache;
    private static ImageTexture _textureBoisBruleFourCache;

    /// <summary>Texture charbon/bois calciné (noir-blanc) pour le mesh bois_bruler du four.</summary>
    public static ImageTexture ObtenirTextureBoisBruleFourTorchie()
    {
        if (_textureBoisBruleFourCache != null)
            return _textureBoisBruleFourCache;

        const int taille = 256;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float nx = x / (float)taille;
                float ny = y / (float)taille;
                float n1 = Mathf.Sin(nx * 18.3f + ny * 11.7f) * Mathf.Cos(ny * 21.1f - nx * 9.4f);
                float n2 = Mathf.Sin(nx * 43f - 2.1f) * Mathf.Sin(ny * 37f + 1.8f) * 0.45f;
                float fissure = Mathf.Abs(Mathf.Sin(nx * 62f + ny * 19f)) > 0.92f ? 0.22f : 0f;
                float bruit = (n1 + n2) * 0.5f + fissure;
                float cendre = Mathf.Clamp(0.72f + bruit * 0.18f, 0f, 1f);
                float charbon = Mathf.Clamp(0.12f + bruit * 0.14f - fissure * 0.5f, 0f, 1f);
                float mix = Mathf.Clamp(Mathf.Sin(nx * 7.2f + ny * 5.8f) * 0.5f + 0.5f + bruit * 0.35f, 0f, 1f);
                float g = Mathf.Lerp(charbon, cendre, mix);
                img.SetPixel(x, y, new Color(g, g, g * 0.98f));
            }
        }

        _textureBoisBruleFourCache = ImageTexture.CreateFromImage(img);
        return _textureBoisBruleFourCache;
    }

    public static StandardMaterial3D ObtenirMaterielScorieFourTorchie()
    {
        if (_materielScorieFourTorchieCache != null)
            return _materielScorieFourTorchieCache;

        _materielScorieFourTorchieCache = new StandardMaterial3D
        {
            AlbedoTexture = ObtenirTextureTorchie(),
            Roughness = 0.91f,
            Metallic = 0f
        };
        return _materielScorieFourTorchieCache;
    }

    public static StandardMaterial3D ObtenirMaterielBoisBruleFourTorchie()
    {
        if (_materielBoisBruleFourCache != null)
            return _materielBoisBruleFourCache;

        _materielBoisBruleFourCache = new StandardMaterial3D
        {
            AlbedoTexture = ObtenirTextureBoisBruleFourTorchie(),
            Roughness = 0.88f,
            Metallic = 0f
        };
        return _materielBoisBruleFourCache;
    }

    private static void AppliquerMateriauxFourTorchieSurMeshes(Node racine)
    {
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                string nom = mi.Name.ToString().ToLowerInvariant();
                if (nom.Contains("bois") && nom.Contains("brul"))
                    mi.MaterialOverride = ObtenirMaterielBoisBruleFourTorchie();
                else if (nom.Contains("scorie"))
                    mi.MaterialOverride = ObtenirMaterielScorieFourTorchie();
                else
                    mi.MaterialOverride = ObtenirMaterielScorieFourTorchie();
            }
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    /// <summary>Four en torchie (GLB atelier : scorie + bois_bruler).</summary>
    public static void InstancierModeleFourTorchie(Node3D parent, SlotInventaire slot, float tailleMaxMetres = TailleFourTorchiePoseMetres, bool ancrerBaseAuSol = true)
    {
        const string cheminGlb = "res://Modeles/Ateliers/four_torchie.glb";
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.55f, 0.9f) },
                MaterialOverride = ObtenirMaterielScorieFourTorchie()
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        AppliquerMateriauxFourTorchieSurMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
