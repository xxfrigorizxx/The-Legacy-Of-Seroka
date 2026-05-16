using Godot;
using System;
using Godot.Collections;

/// <summary>
/// Fabrique un matériau voxel robuste pour runtime exporté.
/// Objectif: éviter le rendu magenta quand la ressource Texture2DArray exportée est invalide.
/// </summary>
public static class TerrainMaterialFactory
{
    private static readonly string[] CheminsTexturesTerrain =
    {
        "res://textures/terrain/00_vide.jpg",
        "res://textures/terrain/01_herbe.jpg",
        "res://textures/terrain/02_roche.png",
        "res://textures/terrain/03_sable.png",
        "res://textures/terrain/04_eau_fantome.jpg",
        "res://textures/terrain/05_neige.png",
        "res://textures/terrain/06_terre_aride.png",
        "res://textures/terrain/07_boue.png",
        "res://textures/terrain/08_argile.jpg",
        "res://textures/terrain/09_glace.png"
    };

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

        Texture2DArray textureArray = null;

        // Ordre volontaire: garder le rendu identique éditeur/launcher quand possible,
        // mais ne jamais garder une texture array invalide (symptôme: terrain magenta).
        textureArray = ExtraireTextureArrayDepuisMateriel(materielExistant);
        if (!EstTextureArrayValide(textureArray))
            textureArray = ChargerTextureArrayDepuisRessource();
        if (!EstTextureArrayValide(textureArray))
            textureArray = ConstruireTextureArrayDepuisTexturesTerrain();

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
            if (couche0 == null)
                return false;
            return couche0.GetWidth() > 0 && couche0.GetHeight() > 0;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SEROKA TERRAIN: texture array invalide ({ex.Message}).");
            return false;
        }
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
