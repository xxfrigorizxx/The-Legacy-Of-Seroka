using Godot;

public partial class Joueur
{
    private static ImageTexture _textureOsBoeufCache;
    private static StandardMaterial3D _materielOsBoeufCache;

    /// <summary>Texture procédurale os (ivoire poreux).</summary>
    public static ImageTexture ObtenirTextureOsBoeuf()
    {
        if (_textureOsBoeufCache != null)
            return _textureOsBoeufCache;

        const int taille = 128;
        var img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float n1 = Mathf.Sin(x * 0.31f) * Mathf.Cos(y * 0.27f);
                float n2 = Mathf.Sin(x * 0.53f + y * 0.41f) * 0.5f;
                float pores = 0.08f * Mathf.Sin(x * 0.71f + y * 0.63f) * Mathf.Cos(x * 0.19f - y * 0.23f);
                float bruit = (n1 + n2) * 0.5f + pores;
                float r = 0.90f + bruit * 0.08f;
                float g = 0.86f + bruit * 0.07f;
                float b = 0.78f + bruit * 0.06f;
                img.SetPixel(x, y, new Color(r, g, b));
            }
        }

        _textureOsBoeufCache = ImageTexture.CreateFromImage(img);
        return _textureOsBoeufCache;
    }

    public static StandardMaterial3D ObtenirMaterielOsBoeuf()
    {
        if (_materielOsBoeufCache != null)
            return _materielOsBoeufCache;

        _materielOsBoeufCache = new StandardMaterial3D
        {
            AlbedoTexture = ObtenirTextureOsBoeuf(),
            AlbedoColor = new Color(0.96f, 0.93f, 0.88f),
            Roughness = 0.82f,
            Metallic = 0f,
            Uv1Scale = new Vector3(2.5f, 2.5f, 1f)
        };
        return _materielOsBoeufCache;
    }

    private static void AppliquerMateriauOsBoeufSurMeshes(Node racine)
    {
        Material mat = ObtenirMaterielOsBoeuf();
        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = mat;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    /// <summary>Seuls les meshes céramique (bol / moule) reçoivent la teinte chaude — le reste reste os.</summary>
    private static bool EstMeshCeramiquePinceOs(string nomMesh, SlotInventaire objetPorte)
    {
        if (string.IsNullOrEmpty(nomMesh) || objetPorte.EstVide)
            return false;
        string nom = nomMesh.ToLowerInvariant();
        if (objetPorte.ID == Joueur.IdObjetMouleCeramique)
            return nom.Contains("tripo") || nom.Contains("moule") || nom.Contains("lingo");
        if (objetPorte.ID == Joueur.IdObjetBolCeramique)
            return nom.Contains("bowl") || nom.Contains("bol");
        return false;
    }

    private static void AppliquerMateriauxPinceOsAvecObjetCeramique(Node3D racine, SlotInventaire objetPorte)
    {
        float facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(objetPorte);
        Material matCeramique = facteurChaleur <= 0.001f
            ? CreerMaterielBolTeinteProgressive(0f, ceramique: true)
            : CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: true);
        Material matOs = ObtenirMaterielOsBoeuf();

        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
                mi.MaterialOverride = EstMeshCeramiquePinceOs(mi.Name, objetPorte) ? matCeramique : matOs;
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    private static string ObtenirCheminGlbPinceOs(SlotInventaire slot, out SlotInventaire objetPorte)
    {
        objetPorte = default;
        if (!ItemPhysique.EstPinceOsPorteObjet(slot)
            || !ItemPhysique.EssayerLireObjetPortePinceOs(slot, out objetPorte))
            return "res://Modeles/materials/travailler/pince_os.glb";
        if (objetPorte.ID == Joueur.IdObjetMouleCeramique)
            return "res://Modeles/materials/travailler/pince_os_Moule_lingo.glb";
        if (objetPorte.ID == Joueur.IdObjetBolCeramique)
            return "res://Modeles/materials/travailler/pince_os_bowl.glb";
        return "res://Modeles/materials/travailler/pince_os.glb";
    }

    /// <summary>Pince en os — vide, bol céramique (pince_os_bowl) ou moule céramique (pince_os_Moule_lingo).</summary>
    public static void InstancierModelePinceOs(Node3D parent, SlotInventaire slot, float tailleMaxMetres = 0.36f, bool ancrerBaseAuSol = false)
    {
        string cheminGlb = ObtenirCheminGlbPinceOs(slot, out SlotInventaire objetPorte);
        bool porteObjetCeramique = !objetPorte.EstVide;
        PackedScene scene = GD.Load<PackedScene>(cheminGlb);
        if (scene == null)
        {
            var fallback = new MeshInstance3D
            {
                Name = "ModeleArme",
                Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.02f, 0.18f) },
                MaterialOverride = ObtenirMaterielOsBoeuf()
            };
            parent.AddChild(fallback);
            return;
        }

        NettoyerModelesEnfants(parent);
        Node3D modele = scene.Instantiate<Node3D>();
        modele.Name = "ModeleArme";
        if (porteObjetCeramique)
            AppliquerMateriauxPinceOsAvecObjetCeramique(modele, objetPorte);
        else
            AppliquerMateriauOsBoeufSurMeshes(modele);
        if (ancrerBaseAuSol)
            NormaliserEchelleTableAtelierAuSol(modele, tailleMaxMetres);
        else
            NormaliserEchelleEtCentrerModeleArme(modele, tailleMaxMetres);
        parent.AddChild(modele);
    }
}
