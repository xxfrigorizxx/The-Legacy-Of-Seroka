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

    /// <summary>Meshes bol/moule/étain (hors os de la pince) reçoivent la teinte chaude.</summary>
    private static bool EstMeshCeramiquePinceOs(string nomMesh, SlotInventaire objetPorte)
    {
        if (string.IsNullOrEmpty(nomMesh) || objetPorte.EstVide)
            return false;
        string nom = nomMesh.ToLowerInvariant();
        if (nom.Contains("pince") || nom.Contains("clamp") || nom.Contains("tongs"))
            return false;
        if (objetPorte.ID == Joueur.IdObjetMouleCeramique)
            return nom.Contains("tripo") || nom.Contains("moule") || nom.Contains("lingo")
                || nom.Contains("liquid") || nom.Contains("metal") || nom.Contains("etain") || nom.Contains("tin") || nom.Contains("fill");
        if (objetPorte.ID == Joueur.IdObjetBolCeramique
            || objetPorte.ID == Joueur.IdObjetBolCeramiqueScorie)
            return nom.Contains("bowl") || nom.Contains("bol");
        if (objetPorte.ID == Joueur.IdObjetBolEtainFonduChaud)
            return true;
        return false;
    }

    private static void AppliquerMateriauxPinceOsAvecObjetCeramique(Node3D racine, SlotInventaire objetPorte)
    {
        float facteurChaleur = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(objetPorte);
        bool bolEtainChaud = objetPorte.ID == Joueur.IdObjetBolEtainFonduChaud;
        bool mouleEtainChaud = objetPorte.ID == Joueur.IdObjetMouleCeramique
            && FourTorchieThermodynamique.EstMouleEtainFonduChaud(objetPorte);
        bool teinteArgileChaude = bolEtainChaud || mouleEtainChaud
            || objetPorte.ID == Joueur.IdObjetBolCeramiqueScorie;
        Material matCeramique = facteurChaleur <= 0.001f
            ? CreerMaterielBolTeinteProgressive(0f, ceramique: true)
            : CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: !teinteArgileChaude);
        Material matMetalChaud = facteurChaleur <= 0.001f
            ? ObtenirMaterielEtainSolidifieArgente()
            : CreerMaterielBolTeinteProgressive(facteurChaleur, ceramique: false);
        Material matOs = ObtenirMaterielOsBoeuf();

        void Parcourir(Node n)
        {
            if (n is MeshInstance3D mi)
            {
                if (!EstMeshCeramiquePinceOs(mi.Name, objetPorte))
                    mi.MaterialOverride = matOs;
                else if (mouleEtainChaud && EstNomMeshRemplissageMetalPinceOs(mi.Name))
                    mi.MaterialOverride = matMetalChaud;
                else
                    mi.MaterialOverride = matCeramique;
            }
            foreach (Node enfant in n.GetChildren())
                Parcourir(enfant);
        }
        Parcourir(racine);
    }

    private static bool EstNomMeshRemplissageMetalPinceOs(string nomMesh)
    {
        if (string.IsNullOrEmpty(nomMesh))
            return false;
        string nom = nomMesh.ToLowerInvariant();
        return nom.Contains("liquid") || nom.Contains("liquide") || nom.Contains("metal")
            || nom.Contains("etain") || nom.Contains("tin") || nom.Contains("fill")
            || nom.Contains("lingo") || nom.Contains("plain");
    }

    private static string ObtenirCheminGlbPinceOs(SlotInventaire slot, out SlotInventaire objetPorte)
    {
        objetPorte = default;
        if (!ItemPhysique.EstPinceOsPorteObjet(slot)
            || !ItemPhysique.EssayerLireObjetPortePinceOs(slot, out objetPorte))
            return "res://Modeles/materials/travailler/pince_os.glb";
        if (objetPorte.ID == Joueur.IdObjetMouleCeramique)
        {
            if (FourTorchieThermodynamique.EstMouleEtainFonduChaud(objetPorte)
                || FourTorchieThermodynamique.EstMouleEtainSolidifie(objetPorte))
                return "res://Modeles/materials/travailler/pince_os_Moule_lingo.glb";
            return "res://Modeles/materials/travailler/pince_os_Moule_lingo.glb";
        }
        if (objetPorte.ID == Joueur.IdObjetBolEtainFonduChaud)
            return "res://Modeles/materials/travailler/pince_os_bowl_plain2.glb";
        if (objetPorte.ID == Joueur.IdObjetBolCeramiqueScorie)
            return "res://Modeles/materials/travailler/pince_os_bowl_ceramique_scorie.glb";
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
