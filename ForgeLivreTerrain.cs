using Godot;
using Godot.Collections;

[Tool]
public partial class ForgeLivreTerrain : Node
{
	[Export] public Texture2D[] TexturesInput = new Texture2D[0];
	[Export] public bool UtiliserTexturesInput = false;

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

	private bool _boutonGenerer;

	[Export]
	public bool BoutonGenerer
	{
		get => _boutonGenerer;
		set
		{
			if (value == true)
			{
				CreerLivre();
			}
			_boutonGenerer = false; // Décoche la case automatiquement après l'action
		}
	}

	private void CreerLivre()
	{
		string[] cheminsTextures = ConstruireCheminsTexturesTerrain();
		var images = new Array<Image>();
		Vector2I? tailleRef = null;
		Image.Format formatRef = Image.Format.Rgba8;

		for (int i = 0; i < cheminsTextures.Length; i++)
		{
			Image img;
			Texture2D texture = null;

			if (UtiliserTexturesInput && TexturesInput != null && i < TexturesInput.Length && TexturesInput[i] != null)
				texture = TexturesInput[i];
			else
				texture = GD.Load<Texture2D>(cheminsTextures[i]);

			if (texture != null)
			{
				img = texture.GetImage();
				if (img == null || img.GetWidth() == 0 || img.GetHeight() == 0)
				{
					GD.PrintErr($"ForgeLivreTerrain : texture invalide couche {i} ({cheminsTextures[i]}), placeholder utilisé.");
					img = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
					img.Fill(new Color(1f, 0f, 1f, 1f));
				}
			}
			else
			{
				img = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
				img.Fill(new Color(1f, 0f, 1f, 1f));
				GD.PrintErr($"ForgeLivreTerrain : texture manquante couche {i} ({cheminsTextures[i]}), placeholder magenta utilisé.");
			}

			if (tailleRef == null)
			{
				tailleRef = new Vector2I(img.GetWidth(), img.GetHeight());
				formatRef = img.GetFormat();
			}
			else if (img.GetWidth() != tailleRef.Value.X || img.GetHeight() != tailleRef.Value.Y)
			{
				img.Resize(tailleRef.Value.X, tailleRef.Value.Y);
			}

			if (img.GetFormat() != formatRef)
				img.Convert(formatRef);

			images.Add(img);
		}

		if (images.Count == 0)
		{
			GD.PrintErr("ForgeLivreTerrain : aucune texture valide.");
			return;
		}

		var livre = new Texture2DArray();
		Error err = livre.CreateFromImages(images);
		if (err != Error.Ok)
		{
			GD.PrintErr($"ForgeLivreTerrain : CreateFromImages échoué ({err}).");
			return;
		}

		err = ResourceSaver.Save(livre, "res://Livre_Terrain.tres", ResourceSaver.SaverFlags.Compress);
		if (err != Error.Ok)
		{
			GD.PrintErr($"ForgeLivreTerrain : sauvegarde échouée ({err}).");
			return;
		}

		GD.Print($"ForgeLivreTerrain : Livre_Terrain.tres créé avec {images.Count} couches (0..{images.Count - 1}).");
	}

	private static string[] ConstruireCheminsTexturesTerrain()
	{
		var chemins = new string[CoucheTextureMaxTerrain + 1];
		for (int i = 0; i <= CoucheTextureMaxTerrain; i++)
			chemins[i] = "res://textures/terrain/02_roche.png";

		// 30/31 restent réservés (bois/feuilles shader), mais on met des couches valides.
		chemins[30] = "res://textures/terrain/02_roche.png";
		chemins[31] = "res://textures/terrain/02_roche.png";

		foreach (var kv in CheminsTexturesOverrides)
			chemins[kv.Key] = kv.Value;

		return chemins;
	}
}
