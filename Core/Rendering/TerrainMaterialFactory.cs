using Godot;
using System;
using Godot.Collections;

/// <summary>
/// Fabrique un matériau voxel robuste pour runtime exporté.
/// Objectif: éviter le rendu magenta quand la ressource Texture2DArray exportée est invalide.
/// </summary>
public static class TerrainMaterialFactory
{
    private const int CoucheTextureMaxTerrain = 49;

    private static readonly Dictionary<int, string> CheminsTexturesOverrides = new()
    {
        [0] = "res://textures/terrain/00_vide.jpg",
        [1] = "res://textures/terrain/01_herbe.jpg",
        [2] = "res://textures/terrain/02_roche.png",
        [3] = "res://textures/terrain/03_sable.png",
        [4] = "res://textures/terrain/04_eau_fantome.jpg",
        [5] = "res://textures/terrain/05_neige.png",
        [6] = "res://textures/terrain/06_terre_aride.png",
        [7] = "res://textures/terrain/07_boue.png",
        [8] = "res://textures/terrain/08_argile.jpg",
        [9] = "res://textures/terrain/09_glace.png",

        [10] = "res://textures/terrain/minerais/10_minerai_charbon.png",
        [11] = "res://textures/terrain/minerais/11_minerai_jade.png",
        [12] = "res://textures/terrain/minerais/12_minerai_opale.png",
        [13] = "res://textures/terrain/minerais/13_minerai_diamant.png",
        [14] = "res://textures/terrain/minerais/14_minerai_topaze.png",
        [15] = "res://textures/terrain/minerais/15_minerai_rubis.png",
        [16] = "res://textures/terrain/minerais/16_minerai_saphir.png",
        [17] = "res://textures/terrain/minerais/17_minerai_emeraude.png",
        [18] = "res://textures/terrain/minerais/18_minerai_amethyste.png",
        [19] = "res://textures/terrain/minerais/19_minerai_quartz.png",
        [20] = "res://textures/terrain/minerais/20_minerai_palladium.png",
        [21] = "res://textures/terrain/minerais/21_minerai_platine.png",
        [22] = "res://textures/terrain/minerais/22_minerai_argent.png",
        [23] = "res://textures/terrain/minerais/23_minerai_or.png",
        [24] = "res://textures/terrain/minerais/24_minerai_bismuth.png",
        [25] = "res://textures/terrain/minerais/25_minerai_manganese.png",
        [26] = "res://textures/terrain/minerais/26_minerai_titane.png",
        [27] = "res://textures/terrain/minerais/27_minerai_tungstene.png",
        [28] = "res://textures/terrain/minerais/28_minerai_cobalt.png",
        [29] = "res://textures/terrain/minerais/29_minerai_chrome.png",
        [32] = "res://textures/terrain/minerais/32_minerai_nickel.png",
        [33] = "res://textures/terrain/minerais/33_minerai_aluminium.png",
        [34] = "res://textures/terrain/minerais/34_minerai_fer.png",
        [35] = "res://textures/terrain/minerais/35_minerai_plomb.png",
        [36] = "res://textures/terrain/minerais/36_minerai_zinc.png",
        [37] = "res://textures/terrain/minerais/37_minerai_etain.png",
        [38] = "res://textures/terrain/minerais/38_minerai_cuivre.png",
        [39] = "res://textures/terrain/minerais/39_minerai_soufre.png",
        [40] = "res://textures/terrain/minerais/40_minerai_salpetre.png",
        [41] = "res://textures/terrain/minerais/41_minerai_uranium.png",
        [42] = "res://textures/terrain/minerais/42_minerai_thorium.png",
        [43] = "res://textures/terrain/minerais/43_minerai_plutonium.png",
        [44] = "res://textures/terrain/minerais/44_minerai_sel.png",
        [45] = "res://textures/terrain/minerais/45_minerai_graphite.png",
        [46] = "res://textures/terrain/minerais/46_minerai_calcaire.png",
        [47] = "res://textures/terrain/minerais/47_minerai_gypse.png",
        [48] = "res://textures/terrain/minerais/48_obsidienne.png",
        [Atlas_Matiere.IdVoxelSableQuartz] = "res://textures/terrain/03_sable.png",
    };

    private static readonly string[] CheminsTexturesTerrain = ConstruireCheminsTexturesTerrain();

    private static Texture2DArray _cacheTextureArray;
    private static Material _cacheMaterielTerrain;

    public static Material ObtenirMaterielTerrainRobuste(Material materielExistant = null)
    {
        if (_cacheMaterielTerrain != null && GodotObject.IsInstanceValid(_cacheMaterielTerrain))
            return _cacheMaterielTerrain;

        Shader shaderTerrain = GD.Load<Shader>("res://TerrainVoxel.gdshader");
        if (shaderTerrain == null)
        {
            GD.PrintErr("SEROKA TERRAIN: shader TerrainVoxel.gdshader introuvable, fallback standard.");
            _cacheMaterielTerrain = ConstruireFallbackStandard();
            return _cacheMaterielTerrain;
        }

        // Minéraux: on reconstruit systématiquement depuis les PNG sources.
        // Motif: évite d'utiliser une Texture2DArray legacy (ex: Livre_Terrain.tres 10 couches)
        // qui empêcherait l'affichage des couches minerais 10..48.
        Texture2DArray textureArray = ConstruireTextureArrayDepuisTexturesTerrain();

        if (!EstTextureArrayValide(textureArray))
        {
            GD.PrintErr("SEROKA TERRAIN: impossible de préparer la texture array, fallback standard.");
            _cacheMaterielTerrain = ConstruireFallbackStandard();
            return _cacheMaterielTerrain;
        }

        var mat = new ShaderMaterial
        {
            Shader = shaderTerrain
        };
        mat.SetShaderParameter("texture_array", textureArray);
        // Calibrage release: rapprocher le rendu launcher du rendu Godot éditeur.
        // On cible uniquement les teintes pilotées shader (ID 1 herbe, ID 4 eau).
        if (!OS.IsDebugBuild())
        {
            mat.SetShaderParameter("herbe_saturation_boost", 1.18f);
            mat.SetShaderParameter("herbe_value_boost", 1.10f);
            mat.SetShaderParameter("eau_saturation_boost", 1.10f);
            mat.SetShaderParameter("eau_value_boost", 1.06f);
        }
        _cacheMaterielTerrain = mat;
        GD.Print("SEROKA TERRAIN: matériau voxel runtime robuste prêt.");
        return _cacheMaterielTerrain;
    }

    private static Texture2DArray ExtraireTextureArrayDepuisMateriel(Material materiel)
    {
        if (materiel is not ShaderMaterial sm)
            return null;
        Variant v = sm.GetShaderParameter("texture_array");
        if (v.VariantType == Variant.Type.Nil)
            return null;
        return v.AsGodotObject() as Texture2DArray;
    }

    private static Texture2DArray ChargerTextureArrayDepuisRessource()
    {
        if (_cacheTextureArray != null && GodotObject.IsInstanceValid(_cacheTextureArray))
            return _cacheTextureArray;

        Resource res = GD.Load<Resource>("res://Livre_Terrain.tres");
        if (res is Texture2DArray t2dArray)
        {
            _cacheTextureArray = t2dArray;
            GD.Print("SEROKA TERRAIN: texture array chargée depuis Livre_Terrain.tres.");
            return _cacheTextureArray;
        }
        return null;
    }

    private static bool EstTextureArrayValide(Texture2DArray textureArray)
    {
        if (textureArray == null || !GodotObject.IsInstanceValid(textureArray))
            return false;
        try
        {
            Image couche0 = textureArray.GetLayerData(0);
            Image coucheMax = textureArray.GetLayerData(CoucheTextureMaxTerrain);
            if (couche0 == null || coucheMax == null)
                return false;
            return couche0.GetWidth() > 0
                && couche0.GetHeight() > 0
                && coucheMax.GetWidth() > 0
                && coucheMax.GetHeight() > 0;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SEROKA TERRAIN: texture array invalide ({ex.Message}).");
            return false;
        }
    }

    private static string[] ConstruireCheminsTexturesTerrain()
    {
        var chemins = new string[CoucheTextureMaxTerrain + 1];
        for (int i = 0; i <= CoucheTextureMaxTerrain; i++)
            chemins[i] = "res://textures/terrain/02_roche.png";

        // Réserves techniques (30/31): on garde une couche valide même si la couleur est forcée dans le shader.
        chemins[30] = "res://textures/terrain/02_roche.png";
        chemins[31] = "res://textures/terrain/02_roche.png";

        foreach (var kv in CheminsTexturesOverrides)
            chemins[kv.Key] = kv.Value;

        return chemins;
    }

    private static Texture2DArray ConstruireTextureArrayDepuisTexturesTerrain()
    {
        var images = new Array<Image>();
        Vector2I tailleRef = Vector2I.Zero;
        Image.Format formatRef = Image.Format.Rgba8;

        for (int i = 0; i < CheminsTexturesTerrain.Length; i++)
        {
            Texture2D tex = GD.Load<Texture2D>(CheminsTexturesTerrain[i]);
            Image image = tex?.GetImage();
            if (image == null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
            {
                image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
                image.Fill(new Color(0.42f, 0.42f, 0.42f, 1f));
                GD.PrintErr($"SEROKA TERRAIN: texture source invalide ({CheminsTexturesTerrain[i]}), placeholder utilisé.");
            }

            if (i == 0)
            {
                tailleRef = new Vector2I(image.GetWidth(), image.GetHeight());
            }
            else
            {
                if (image.GetWidth() != tailleRef.X || image.GetHeight() != tailleRef.Y)
                    image.Resize(tailleRef.X, tailleRef.Y);
            }
            if (image.GetFormat() != formatRef)
                image.Convert(formatRef);

            images.Add(image);
        }

        var livre = new Texture2DArray();
        Error err = livre.CreateFromImages(images);
        if (err != Error.Ok)
        {
            GD.PrintErr($"SEROKA TERRAIN: CreateFromImages échoué ({err}).");
            return null;
        }

        _cacheTextureArray = livre;
        GD.Print("SEROKA TERRAIN: texture array reconstruite depuis textures/terrain.");
        return _cacheTextureArray;
    }

    private static Material ConstruireFallbackStandard()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(0.44f, 0.40f, 0.34f),
            Roughness = 0.95f,
            Metallic = 0f
        };
    }
}
