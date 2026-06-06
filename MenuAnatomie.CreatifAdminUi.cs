using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void AssurerPanneauCreatifAdmin()
	{
		if (Engine.IsEditorHint()) return;
		var vbox = GetNodeOrNull<VBoxContainer>(CheminVBoxPrincipal) ?? FindChild("VBoxPrincipal", true, false) as VBoxContainer;
		if (vbox == null) return;
		if (_panneauCreatifAdmin != null) return;

		_panneauCreatifAdmin = new Panel
		{
			Name = "PanneauCreatifAdmin",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_panneauCreatifAdmin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_panneauCreatifAdmin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var marge = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		marge.AddThemeConstantOverride("margin_left", 12);
		marge.AddThemeConstantOverride("margin_top", 12);
		marge.AddThemeConstantOverride("margin_right", 12);
		marge.AddThemeConstantOverride("margin_bottom", 12);
		_panneauCreatifAdmin.AddChild(marge);

		var colonne = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		colonne.AddThemeConstantOverride("separation", 8);
		marge.AddChild(colonne);

		var titre = new Label
		{
			Text = "Catalogue Creatif/Admin",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		titre.AddThemeFontSizeOverride("font_size", 20);
		colonne.AddChild(titre);

		var ligneFiltres = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		colonne.AddChild(ligneFiltres);

		_filtreCategorieCreatifAdmin = new OptionButton();
		_filtreCategorieCreatifAdmin.AddItem("Tous variantes");
		_filtreCategorieCreatifAdmin.AddItem("Tous");
		_filtreCategorieCreatifAdmin.AddItem("Structures");
		_filtreCategorieCreatifAdmin.AddItem("Bois");
		_filtreCategorieCreatifAdmin.AddItem("Pierre");
		_filtreCategorieCreatifAdmin.AddItem("Outils");
		_filtreCategorieCreatifAdmin.AddItem("Consommables");
		_filtreCategorieCreatifAdmin.AddItem("Admin");
		_filtreCategorieCreatifAdmin.ItemSelected += idx =>
		{
			_categorieCreatifActive = (CategorieCreatifAdmin)Mathf.Clamp((int)idx, 0, (int)CategorieCreatifAdmin.Admin);
			_pageCreatifAdmin = 0;
			RafraichirPanneauCreatifAdmin();
		};
		ligneFiltres.AddChild(_filtreCategorieCreatifAdmin);

		_rechercheCreatifAdmin = new LineEdit
		{
			PlaceholderText = "Filtre nom/ID...",
			ClearButtonEnabled = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_rechercheCreatifAdmin.TextChanged += _ =>
		{
			_pageCreatifAdmin = 0;
			_creatifAdminListeSale = true;
			RafraichirPanneauCreatifAdminSiThrottle();
		};
		ligneFiltres.AddChild(_rechercheCreatifAdmin);

		_listeCreatifAdmin = new ItemList
		{
			SelectMode = ItemList.SelectModeEnum.Single,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 340f)
		};
		_listeCreatifAdmin.ItemActivated += _ => InjecterSelectionCreatifAdmin();
		colonne.AddChild(_listeCreatifAdmin);

		var lignePagination = new HBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
		};
		colonne.AddChild(lignePagination);
		_btnPagePrecCreatifAdmin = new Button { Text = "Page -" };
		_btnPagePrecCreatifAdmin.Pressed += () =>
		{
			if (_pageCreatifAdmin <= 0) return;
			_pageCreatifAdmin--;
			RafraichirPanneauCreatifAdmin();
		};
		_btnPageSuivCreatifAdmin = new Button { Text = "Page +" };
		_btnPageSuivCreatifAdmin.Pressed += () =>
		{
			_pageCreatifAdmin++;
			RafraichirPanneauCreatifAdmin();
		};
		_lblPaginationCreatifAdmin = new Label { Text = "Page 1/1", HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		lignePagination.AddChild(_btnPagePrecCreatifAdmin);
		lignePagination.AddChild(_lblPaginationCreatifAdmin);
		lignePagination.AddChild(_btnPageSuivCreatifAdmin);

		_btnInjecterCreatifAdmin = new Button
		{
			Text = "Injecter stack max (serveur)",
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
		};
		_btnInjecterCreatifAdmin.Pressed += InjecterSelectionCreatifAdmin;
		colonne.AddChild(_btnInjecterCreatifAdmin);

		vbox.AddChild(_panneauCreatifAdmin);
	}

	private void ReconstruireCatalogueCreatifAdminSiNecessaire()
	{
		if (_catalogueCreatifAdmin.Count > 0 && _versionCatalogueCreatifChargee == CreatifCatalogueService.VersionCatalogue)
			return;
		_catalogueCreatifAdmin.Clear();
		_versionCatalogueCreatifChargee = CreatifCatalogueService.VersionCatalogue;
		var signatures = new HashSet<string>(StringComparer.Ordinal);
		static bool EstEssenceBoisValide(byte e) => e <= 4;
		static string NomEssence(byte e) => e switch
		{
			0 => "Chêne",
			1 => "Bouleau",
			2 => "Pin",
			3 => "Sapin",
			4 => "Fromager",
			_ => "Bois"
		};
		static string NomLigature(byte tag) => tag switch
		{
			Joueur.TagVarianteLiane => "Liane",
			Joueur.TagVarianteHerbeSolide => "Herbe solide",
			Joueur.TagVarianteIntestin => "Intestin",
			Joueur.TagVarianteIntestinSolide => "Intestin solide",
			_ => "Défaut"
		};
		static (int chimie, int morphologie) ProfilLigature(byte tag) => tag switch
		{
			Joueur.TagVarianteLiane => (16, 16),
			Joueur.TagVarianteHerbeSolide => (15, 15),
			Joueur.TagVarianteIntestin => (17, 17),
			Joueur.TagVarianteIntestinSolide => (17, 17),
			_ => (15, 15)
		};

		void Ajouter(SlotInventaire s, CategorieCreatifAdmin categorie, string suffixe = "")
		{
			if (s.EstVide) return;
			if (s.Quantite <= 0) s.Quantite = 1;
			string cle = string.Join("|",
				s.ID, s.IndexMorphologique, s.IndexChimique, s.IndexTaille, s.IndexBotanique,
				s.IndexTailleLameRoche, s.NiveauFracture, s.GenomeAssemblage ?? "", s.CleConteneur ?? "");
			if (!signatures.Add(cle)) return;

			Atlas_Matiere.InitialiserDurabiliteOutilSiBesoin(ref s);
			_catalogueCreatifAdmin.Add(new EntreeCatalogueCreatifAdmin
			{
				Slot = s,
				Nom = Atlas_Matiere.ObtenirNomObjet(s),
				Suffixe = suffixe,
				Categorie = categorie
			});
		}

		byte[] essencesBois = { 0, 1, 2, 3, 4 };
		byte[] tagsLigatures = { Joueur.TagVarianteLiane, Joueur.TagVarianteHerbeSolide, Joueur.TagVarianteIntestin, Joueur.TagVarianteIntestinSolide };

		// Roches / minerais.
		for (int idRoche = ItemPhysique.IdRocheMatiereMin; idRoche <= ItemPhysique.IdRocheMatiereMax; idRoche++)
		for (int taille = 0; taille <= 4; taille++)
		for (int morph = 0; morph <= 3; morph++)
			Ajouter(new SlotInventaire { ID = idRoche, IndexTaille = taille, IndexMorphologique = morph, Quantite = 1 }, CategorieCreatifAdmin.Pierre);

		// Voxels minerais de terrain (pose directe): tag GenomeAssemblage pour éviter les collisions d'IDs gameplay.
		static SlotInventaire CreerSlotVoxelMinerai(int idVoxel)
		{
			return new SlotInventaire
			{
				// ID proxy "roche terrain" pour éviter toute collision inventaire avec des IDs gameplay existants
				// (buissons, corde/tissu, roches matière 40..51, etc.). L'ID voxel réel est porté par le genome tag.
				ID = 2,
				GenomeAssemblage = $"VOXEL_TERRAIN:{idVoxel}",
				Quantite = 1
			};
		}
		(string nom, int idVoxel)[] mineraisVoxel =
		{
			("Charbon", 10), ("Jade", 11), ("Opale", 12), ("Diamant", 13), ("Topaze", 14),
			("Rubis", 15), ("Saphir", 16), ("Émeraude", 17), ("Améthyste", 18), ("Quartz", 19),
			("Palladium", 20), ("Platine", 21), ("Argent", 22), ("Or", 23), ("Bismuth", 24),
			("Manganèse", 25), ("Titane", 26), ("Tungstène", 27), ("Cobalt", 28), ("Chrome", 29),
			("Nickel", 32), ("Aluminium", 33), ("Fer", 34), ("Plomb", 35), ("Zinc", 36),
			("Étain", 37), ("Cuivre", 38), ("Soufre", 39), ("Salpêtre", 40), ("Uranium", 41),
			("Thorium", 42), ("Plutonium", 43), ("Sel", 44), ("Graphite", 45), ("Calcaire", 46),
			("Gypse", 47), ("Obsidienne", 48)
		};
		for (int i = 0; i < mineraisVoxel.Length; i++)
		{
			var m = mineraisVoxel[i];
			Ajouter(CreerSlotVoxelMinerai(m.idVoxel), CategorieCreatifAdmin.Pierre, $"Voxel minerai: {m.nom} (ID {m.idVoxel})");
		}

		int[] idsCharbonRecolte =
		{
			Joueur.IdObjetCharbonBasseQualite,
			Joueur.IdObjetCharbonMoyenneQualite,
			Joueur.IdObjetCharbonBonneQualite,
			Joueur.IdObjetCharbonAntracite
		};
		foreach (int idCharbon in idsCharbonRecolte)
			Ajouter(new SlotInventaire { ID = idCharbon, Quantite = 1 }, CategorieCreatifAdmin.Pierre);

		int[] idsConsommables = {
			1,2,3,4,5,6,7,8,9,
			Joueur.IdObjetSteakCru, Joueur.IdObjetSteakCuit, Joueur.IdObjetAtelleJambe, Joueur.IdObjetAtelleBras, Joueur.IdObjetBandageTier1
		};
		foreach (int id in idsConsommables)
			Ajouter(new SlotInventaire { ID = id, Quantite = 1 }, CategorieCreatifAdmin.Consommables);

		for (int couleurBaie = 0; couleurBaie < Joueur.BaieNombreCouleurs; couleurBaie++)
		{
			Ajouter(
				new SlotInventaire { ID = Joueur.IdObjetBaie, IndexChimique = couleurBaie, Quantite = 1 },
				CategorieCreatifAdmin.Consommables,
				$"Baie {Joueur.ObtenirLexemeCouleurBaiePourNomInventaire(couleurBaie)}");
		}

		int[] idsAdminDivers = {
			Joueur.IdObjetCarnetSavoir, Joueur.IdObjetOsBoeuf, Joueur.IdObjetCuirBoeuf,
			Joueur.IdObjetIntestinBoeuf, Joueur.IdObjetIntestinBoeufNettoye,
			15,16,17,20,21,100,101,102,103,104
		};
		foreach (int id in idsAdminDivers)
			Ajouter(new SlotInventaire { ID = id, Quantite = 1 }, CategorieCreatifAdmin.Admin);

		// Bois/branches/bûches avec variantes essence.
		foreach (byte essence in essencesBois)
		{
			for (int taille = 0; taille <= 3; taille++)
			for (int morph = 0; morph <= 3; morph++)
				Ajouter(new SlotInventaire { ID = 30, IndexBotanique = essence, IndexTaille = taille, IndexMorphologique = morph, Quantite = 1 },
					CategorieCreatifAdmin.Bois, $"Bûche {NomEssence(essence)} T{taille} M{morph}");
			for (int taille = 0; taille <= 3; taille++)
			for (int morph = 0; morph <= 3; morph++)
				Ajouter(new SlotInventaire { ID = 32, IndexBotanique = essence, IndexChimique = 0, IndexTaille = taille, IndexMorphologique = morph, Quantite = 1 },
					CategorieCreatifAdmin.Bois, $"Bâton brut {NomEssence(essence)} T{taille} M{morph}");
			Ajouter(new SlotInventaire { ID = 32, IndexBotanique = essence, IndexChimique = 1, IndexMorphologique = 0, Quantite = 1 },
				CategorieCreatifAdmin.Bois, $"Bâton façonné {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = 32, IndexBotanique = essence, IndexChimique = 1, IndexMorphologique = 4, Quantite = 1 },
				CategorieCreatifAdmin.Bois, $"Bâton en T {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = BlocChutant.ID_BRANCHE, IndexBotanique = essence, IndexMorphologique = 0, Quantite = 1 },
				CategorieCreatifAdmin.Bois, $"Branche arbre · {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = BlocChutant.ID_BRANCHE, IndexBotanique = essence, IndexMorphologique = 1, Quantite = 1 },
				CategorieCreatifAdmin.Bois, $"Branche buisson · {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = BlocChutant.ID_FEUILLE_ARRACHEE, IndexBotanique = essence, Quantite = 1 },
				CategorieCreatifAdmin.Bois, $"Feuille {NomEssence(essence)}");
		}
		byte[] essencesBrancheMortes = { 5, 6 };
		foreach (byte essenceMort in essencesBrancheMortes)
		{
			string nomMort = essenceMort switch { 5 => "Chêne mort", 6 => "Bouleau mort", _ => "Bois mort" };
			Ajouter(new SlotInventaire { ID = BlocChutant.ID_BRANCHE, IndexBotanique = essenceMort, IndexMorphologique = 0, Quantite = 1 },
				CategorieCreatifAdmin.Bois, $"Branche arbre · {nomMort}");
		}

		// Variantes ligatures / textile / équipements souples.
		foreach (byte tagLig in tagsLigatures)
		{
			var profil = ProfilLigature(tagLig);
			string nomLig = NomLigature(tagLig);
			Ajouter(new SlotInventaire { ID = 20, IndexChimique = profil.chimie, IndexMorphologique = profil.morphologie, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Admin, $"Corde: {nomLig}");
			Ajouter(new SlotInventaire { ID = 21, IndexChimique = profil.chimie, IndexMorphologique = profil.morphologie, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Admin, $"Tissu: {nomLig}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetPochetteTier0, IndexChimique = profil.chimie, IndexMorphologique = profil.morphologie, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Admin, $"Pochette: {nomLig}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetSacTier0, IndexChimique = profil.chimie, IndexMorphologique = profil.morphologie, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Admin, $"Sac: {nomLig}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetCeinturePoches, IndexChimique = profil.chimie, IndexMorphologique = profil.morphologie, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Admin, $"Ceinture poches: {nomLig}");
			Ajouter(new SlotInventaire
				{
					ID = Joueur.IdObjetCeintureSacoches,
					IndexChimique = profil.chimie,
					IndexMorphologique = profil.morphologie,
					IndexBotanique = tagLig,
					GenomeAssemblage = Joueur.EncoderConfigPochettesCeinture(tagLig, tagLig, tagLig, tagLig),
					Quantite = 1
				},
				CategorieCreatifAdmin.Admin, $"Ceinture sacoches: {nomLig}");
		}

		// Structures bois/mixes.
		foreach (byte essence in essencesBois)
		{
			Ajouter(new SlotInventaire { ID = 200, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Atelier {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetTableBoisDecorative, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Table déco {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetTableArtisanaTier1, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Table artisana T1 {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetTableAnalyseTier1, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Table analyse T1 {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetPitFeu, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Essence: {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetPitFeuRoche, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Essence: {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetCoffreBoisTier0, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Coffre {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetRackBatons, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Rack bâtons {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetRackBuches, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Rack bûches {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Fondation bois {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetSolBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Plancher bois {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetMuretBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Muret bois {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetMurBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Mur bois {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetMurBoisCadrePorte, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Mur cadre porte {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetPorteBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Porte bois {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetFenetreBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Fenêtre bois {NomEssence(essence)}");
			foreach (byte essenceFenetre in essencesBois)
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetMurBoisFenetre, IndexBotanique = essence, IndexChimique = essenceFenetre, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Mur fenêtré {NomEssence(essence)} / {NomEssence(essenceFenetre)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetMailletBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Maillet {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetBolBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Bol {NomEssence(essence)}");
		}
		for (int chim = 0; chim < ItemPhysique.TableGeologique.Length; chim++)
		{
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetSolRoche, IndexChimique = chim, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Plancher roche {ItemPhysique.TableGeologique[chim].Nom}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetMuretPierre, IndexChimique = chim, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Muret roche {ItemPhysique.TableGeologique[chim].Nom}");
		}
		foreach (byte essenceBol in essencesBois)
		{
			foreach (byte essencePilon in essencesBois)
			{
				Ajouter(new SlotInventaire
				{
					ID = Joueur.IdObjetMortierPilonBois,
					IndexBotanique = essenceBol,
					IndexChimique = essencePilon,
					GenomeAssemblage = $"MORTIERPILON:{essenceBol},{essencePilon}",
					Quantite = 1
				}, CategorieCreatifAdmin.Structures, $"Mortier {NomEssence(essenceBol)} + pilon {NomEssence(essencePilon)}");
			}
		}
		foreach (byte tagLig in tagsLigatures)
		{
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetCoffreBoisTier0, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Structures, $"Coffre ligature: {NomLigature(tagLig)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetToitChaume, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Structures, $"Toit chaume: {NomLigature(tagLig)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetTorche, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Structures, $"Torche: {NomLigature(tagLig)}");
			foreach (byte essence in essencesBois)
			{
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetRackBatons, IndexBotanique = essence, GenomeAssemblage = $"RACKL:{tagLig}", Quantite = 1 },
					CategorieCreatifAdmin.Structures, $"Rack bâtons {NomEssence(essence)} + {NomLigature(tagLig)}");
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetRackBuches, IndexBotanique = essence, GenomeAssemblage = $"RACKBL:{tagLig}", Quantite = 1 },
					CategorieCreatifAdmin.Structures, $"Rack bûches {NomEssence(essence)} + {NomLigature(tagLig)}");
			}
		}

		for (int chim = 0; chim < ItemPhysique.TableGeologique.Length; chim++)
		{
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationRoche, IndexChimique = chim, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Roche: {ItemPhysique.TableGeologique[chim].Nom}");
			foreach (byte essence in essencesBois)
			{
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationBoisSoleRoche, IndexBotanique = essence, IndexChimique = chim, Quantite = 1, GenomeAssemblage = "FONDMIX:TOPBOIS_SIDEBOIS" },
					CategorieCreatifAdmin.Structures, $"Mixte {NomEssence(essence)} + {ItemPhysique.TableGeologique[chim].Nom} [Top bois / Side bois]");
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationBoisSoleRoche, IndexBotanique = essence, IndexChimique = chim, Quantite = 1, GenomeAssemblage = "FONDMIX:TOPBOIS_SIDEROCH" },
					CategorieCreatifAdmin.Structures, $"Mixte {NomEssence(essence)} + {ItemPhysique.TableGeologique[chim].Nom} [Top bois / Side roche]");
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationRocheSoleBois, IndexBotanique = essence, IndexChimique = chim, Quantite = 1, GenomeAssemblage = "FONDMIX:TOPROCH_SIDEBOIS" },
					CategorieCreatifAdmin.Structures, $"Mixte {NomEssence(essence)} + {ItemPhysique.TableGeologique[chim].Nom} [Top roche / Side bois]");
				Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationRocheSoleBois, IndexBotanique = essence, IndexChimique = chim, Quantite = 1, GenomeAssemblage = "FONDMIX:TOPROCH_SIDEROCH" },
					CategorieCreatifAdmin.Structures, $"Mixte {NomEssence(essence)} + {ItemPhysique.TableGeologique[chim].Nom} [Top roche / Side roche]");
			}
		}

		// Outils pierre avec toutes chimies.
		int[] outilsPierre = { 105, 106, Joueur.IdObjetHachePierreTier1, Joueur.IdObjetPellePierreTier0, Joueur.IdObjetPiochePierreTier0, Joueur.IdObjetLancePierreTier0, Joueur.IdObjetFauxPierreTier0 };
		foreach (int idOutil in outilsPierre)
			for (int chim = 0; chim < ItemPhysique.TableGeologique.Length; chim++)
				foreach (byte essence in essencesBois)
					Ajouter(new SlotInventaire { ID = idOutil, IndexChimique = chim, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Outils,
						$"{ItemPhysique.TableGeologique[chim].Nom} / {NomEssence(essence)}");

		// Atèle jambe : toutes combinaisons branche/ligature (pour tests visuels).
		foreach (byte essence in essencesBois)
		foreach (byte tagLig in tagsLigatures)
		{
			var profil = ProfilLigature(tagLig);
			Ajouter(new SlotInventaire
				{
					ID = Joueur.IdObjetAtelleJambe,
					IndexBotanique = essence,
					IndexChimique = profil.chimie,
					IndexMorphologique = profil.morphologie,
					GenomeAssemblage = $"ATELLE133;BOIS={essence};LIGV={tagLig};LIGC={profil.chimie};LIGM={profil.morphologie}",
					Quantite = 1
				},
				CategorieCreatifAdmin.Consommables, $"Atèle {NomEssence(essence)} + {NomLigature(tagLig)}");
		}
		foreach (byte essence in essencesBois)
		foreach (byte tagLig in tagsLigatures)
		{
			var profil = ProfilLigature(tagLig);
			Ajouter(new SlotInventaire
				{
					ID = Joueur.IdObjetAtelleBras,
					IndexBotanique = essence,
					IndexChimique = profil.chimie,
					IndexMorphologique = profil.morphologie,
					GenomeAssemblage = $"ATELLE134;BOIS={essence};LIGV={tagLig};LIGC={profil.chimie};LIGM={profil.morphologie}",
					Quantite = 1
				},
				CategorieCreatifAdmin.Consommables, $"Atèle bras {NomEssence(essence)} + {NomLigature(tagLig)}");
		}

		// Objets admin orientés test/debug.
		Ajouter(new SlotInventaire { ID = Joueur.IdObjetAllumeFeu, IndexChimique = 10, Quantite = 1 }, CategorieCreatifAdmin.Admin, "Marcassite");
		Ajouter(new SlotInventaire { ID = Joueur.IdObjetAllumeFeu, IndexChimique = 11, Quantite = 1 }, CategorieCreatifAdmin.Admin, "Pyrite");

		// Tout IdObjet* de Joueur absent du catalogue (nouveaux objets craftés / posables).
		var entreesPourAuto = new List<CreatifCatalogueService.EntreeCatalogueCreatif>(_catalogueCreatifAdmin.Count);
		for (int i = 0; i < _catalogueCreatifAdmin.Count; i++)
		{
			var e = _catalogueCreatifAdmin[i];
			entreesPourAuto.Add(new CreatifCatalogueService.EntreeCatalogueCreatif
			{
				Slot = e.Slot,
				Nom = e.Nom,
				Suffixe = e.Suffixe,
				Categorie = (CreatifCatalogueService.CategorieCreatif)e.Categorie
			});
		}
		CreatifCatalogueService.CompleterEntreesDepuisIdsObjetsJoueur(
			entreesPourAuto,
			signatures,
			(s, cat, suffixe) => Ajouter(s, (CategorieCreatifAdmin)cat, suffixe));

		// Nettoyage final : noms vides, ou catégorie Bois avec IndexBotanique hors plage jeu (0–4 vivant, 5–6 mort).
		_catalogueCreatifAdmin.RemoveAll(e =>
		{
			if (string.IsNullOrWhiteSpace(e.Nom)) return true;
			if (e.Categorie != CategorieCreatifAdmin.Bois) return false;
			byte bot = e.Slot.IndexBotanique;
			return bot > 6 || (bot > 4 && bot != 5 && bot != 6);
		});
		_catalogueCreatifAdmin.Sort((a, b) =>
		{
			int c = a.Categorie.CompareTo(b.Categorie);
			if (c != 0) return c;
			return string.Compare(a.Nom, b.Nom, StringComparison.OrdinalIgnoreCase);
		});
	}

	private static bool EntreeDansCategorie(in EntreeCatalogueCreatifAdmin e, CategorieCreatifAdmin categorie)
	{
		return categorie == CategorieCreatifAdmin.TousVariants
			|| categorie == CategorieCreatifAdmin.Tous
			|| e.Categorie == categorie;
	}

	private void RafraichirPanneauCreatifAdminSiThrottle(bool force = false)
	{
		if (!force && !_creatifAdminListeSale)
			return;
		ulong now = Time.GetTicksMsec();
		if (!force && now - _msDernierRafraichCreatifAdmin < IntervalleRafraichCreatifAdminMs)
			return;
		_msDernierRafraichCreatifAdmin = now;
		_creatifAdminListeSale = false;
		RafraichirPanneauCreatifAdmin();
	}

	private void RafraichirPanneauCreatifAdmin()
	{
		AssurerPanneauCreatifAdmin();
		if (_panneauCreatifAdmin == null || _listeCreatifAdmin == null) return;
		if (_filtreCategorieCreatifAdmin != null)
			_filtreCategorieCreatifAdmin.Select((int)_categorieCreatifActive);
		bool autorise = _joueurRef != null && _joueurRef.ModeCreatifActif;
		_panneauCreatifAdmin.Visible = autorise && _ecranBarreCourant == ModeEcranBarreMenu.CreatifAdmin;
		if (!autorise) return;

		ReconstruireCatalogueCreatifAdminSiNecessaire();
		_indicesFiltresCreatifAdmin.Clear();
		string filtre = (_rechercheCreatifAdmin?.Text ?? "").Trim().ToLowerInvariant();
		for (int i = 0; i < _catalogueCreatifAdmin.Count; i++)
		{
			var e = _catalogueCreatifAdmin[i];
			if (!EntreeDansCategorie(e, _categorieCreatifActive))
				continue;
			string label = $"{e.Nom} [ID {e.Slot.ID}]";
			if (!string.IsNullOrEmpty(e.Suffixe))
				label += $" | {e.Suffixe}";
			if (string.IsNullOrEmpty(filtre) || label.ToLowerInvariant().Contains(filtre))
				_indicesFiltresCreatifAdmin.Add(i);
		}

		int totalPages = Mathf.Max(1, Mathf.CeilToInt(_indicesFiltresCreatifAdmin.Count / (float)TaillePageCreatifAdmin));
		_pageCreatifAdmin = Mathf.Clamp(_pageCreatifAdmin, 0, totalPages - 1);
		int debut = _pageCreatifAdmin * TaillePageCreatifAdmin;
		int fin = Mathf.Min(debut + TaillePageCreatifAdmin, _indicesFiltresCreatifAdmin.Count);

		_listeCreatifAdmin.Clear();
		if (_indicesFiltresCreatifAdmin.Count == 0)
		{
			_listeCreatifAdmin.AddItem("Aucun objet pour ce filtre.");
			if (_lblPaginationCreatifAdmin != null)
				_lblPaginationCreatifAdmin.Text = "Page 0/0 (0 objet)";
			if (_btnPagePrecCreatifAdmin != null) _btnPagePrecCreatifAdmin.Disabled = true;
			if (_btnPageSuivCreatifAdmin != null) _btnPageSuivCreatifAdmin.Disabled = true;
			if (_btnInjecterCreatifAdmin != null) _btnInjecterCreatifAdmin.Disabled = true;
			return;
		}

		for (int i = debut; i < fin; i++)
		{
			var e = _catalogueCreatifAdmin[_indicesFiltresCreatifAdmin[i]];
			string label = $"{e.Nom} [ID {e.Slot.ID}]";
			if (!string.IsNullOrEmpty(e.Suffixe))
				label += $" | {e.Suffixe}";
			_listeCreatifAdmin.AddItem(label);
		}

		if (_lblPaginationCreatifAdmin != null)
			_lblPaginationCreatifAdmin.Text = $"Page {_pageCreatifAdmin + 1}/{totalPages} ({_indicesFiltresCreatifAdmin.Count} objets)";
		if (_btnPagePrecCreatifAdmin != null) _btnPagePrecCreatifAdmin.Disabled = _pageCreatifAdmin <= 0;
		if (_btnPageSuivCreatifAdmin != null) _btnPageSuivCreatifAdmin.Disabled = _pageCreatifAdmin >= totalPages - 1;
		if (_btnInjecterCreatifAdmin != null) _btnInjecterCreatifAdmin.Disabled = _listeCreatifAdmin.ItemCount <= 0;
		if (_listeCreatifAdmin.ItemCount > 0 && _listeCreatifAdmin.GetSelectedItems().Length == 0)
			_listeCreatifAdmin.Select(0);
	}

	private void InjecterSelectionCreatifAdmin()
	{
		if (_joueurRef == null || _listeCreatifAdmin == null) return;
		int selected = _listeCreatifAdmin.GetSelectedItems().Length > 0 ? _listeCreatifAdmin.GetSelectedItems()[0] : -1;
		if (selected < 0) return;
		int globalIndex = _pageCreatifAdmin * TaillePageCreatifAdmin + selected;
		if (globalIndex < 0 || globalIndex >= _indicesFiltresCreatifAdmin.Count) return;
		var entree = _catalogueCreatifAdmin[_indicesFiltresCreatifAdmin[globalIndex]];
		bool envoye = _joueurRef.DemanderInjectionItemCreatifAdmin(entree.Slot);
		if (!envoye)
			Joueur.AlerteSqueletteBoiteNoire("Injection refusée: mode créatif admin inactif.");
	}
}
