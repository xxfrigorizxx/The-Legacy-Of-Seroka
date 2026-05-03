using Godot;
using System;
using System.Collections.Generic;

// [Tool] : layout aussi dans l’éditeur (sans ça la racine fait 0×0 → tout au coin).
[Tool]
public partial class MenuAnatomie : Control
{
	private static readonly Vector2 TailleReferenceEditeur = new(1920f, 1080f);
	public bool EstOuvert { get; private set; }
	private Joueur _joueurRef;

	[Export] public Panel MainGaucheSlot;
	[Export] public Panel MainDroiteSlot;
	[Export] public Panel VueJoueurPanel;
	[Export] public GridContainer GrilleAssemblage;
	[Export] public ColorRect FondSombre;
	[Export] public Panel InterfaceFutureSlot;
	[Export] public Panel SacSlot;
	[Export] public Panel EquipementSacSlot;
	[Export] public Panel CarnetSavoirSlot;
	/// <summary>Grille <c>GrilleEquipCorps</c> : pour l’instant une seule case (ceinture 102). Pour d’autres équipements, ajouter des panneaux frères dans la scène et augmenter <c>columns</c> si besoin.</summary>
	[Export] public Panel EquipementCorpsSlot;
	[Export] public Panel SlotResultatCraft;

	private Label _lblMainGauche;
	private Label _lblMainDroite;
	private Label _lblModeRack;
	private bool _abonneViewport;
	private SubViewportContainer _vpMenuGauche;
	private SubViewportContainer _vpMenuDroite;
	private MeshInstance3D _meshPreviewMenuG;
	private MeshInstance3D _meshPreviewMenuD;
	private SubViewportContainer _vpMenuCeinture;
	private MeshInstance3D _meshPreviewMenuCeinture;
	private Label _lblSlotCeinture;
	private SubViewportContainer _vpMenuSacEquip;
	private MeshInstance3D _meshPreviewMenuSacEquip;
	private Label _lblSlotSacEquip;
	private SubViewportContainer _vpMenuCarnet;
	private MeshInstance3D _meshPreviewMenuCarnet;
	private Label _lblSlotCarnet;

	/// <summary>Objet « tenu par la souris » dans l’inventaire Q : échange au clic avec une main ou une case 2×2.</summary>
	private SlotInventaire _curseurMenu;
	private Label[] _lblCraft;
	private MeshInstance3D[] _meshPreviewCraft;
	private SubViewportContainer[] _vpCraft;
	private SubViewportContainer _vpResultatCraft;
	private MeshInstance3D _meshPreviewResultatCraft;
	private Label _lblResultatCraft;

	private const string CheminGrilleSac = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/GrilleSac";
	private const string CheminGrilleCeintureStockage = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/GrilleCeintureStockage";
	private const string CheminCadreCoffreBois = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/CadreCoffreBois";
	private const string CheminGrilleCoffreBois = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/CadreCoffreBois/GrilleCoffreBois";
	private const string CheminMainGauche = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneMainsCeinture/MainGaucheSlot";
	private const string CheminMainDroite = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneMainsCeinture/MainDroiteSlot";
	private const string CheminEquipementCorpsSlot = "MarginPrincipal/VBoxPrincipal/CorpsHBox/GrilleEquipCorps/EquipementCorpsSlot";
	private const string CheminEquipementSacSlot = "MarginPrincipal/VBoxPrincipal/CorpsHBox/GrilleEquipCorps/EquipementSacSlot";
	private const string CheminCarnetSavoirSlot = "MarginPrincipal/VBoxPrincipal/CorpsHBox/GrilleEquipCorps/CarnetSavoirSlot";
	private const string CheminGrilleAssemblage = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneCraft/CadreCraft/GrilleAssemblage";
	private const string CheminSlotResultatCraft = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneCraft/CraftSortie";
	private const string CheminLigneMainsCeinture = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneMainsCeinture";

	private bool _clicsMainsConnectes;
	private bool _clicsSlotCeintureConnecte;
	private bool _clicsSlotSacConnecte;
	private bool _clicsCraftConnectes;
	private bool _clicsSlotResultatCraftConnecte;
	private bool _clicsGrilleSacConnectes;
	private bool _clicsGrilleCeintureStockageConnectes;
	private bool _clicsGrilleCoffreConnectes;
	private bool _clicsGrilleAnalyseurConnectes;
	private bool _barreOngletsJeuConfiguree;
	private SubViewportContainer[] _vpCoffre;
	private MeshInstance3D[] _meshPreviewCoffre;
	private Label[] _lblCoffre;

	private const string CheminBarreOnglets = "MarginPrincipal/VBoxPrincipal/BarreOnglets";
	private const string CheminVBoxPrincipal = "MarginPrincipal/VBoxPrincipal";
	private const string CheminCorpsHBox = "MarginPrincipal/VBoxPrincipal/CorpsHBox";
	private const string CheminVueJoueurPanel = "MarginPrincipal/VBoxPrincipal/CorpsHBox/VueJoueurPanel";

	private enum ModeEcranBarreMenu
	{
		Inventaire,
		Analyseur,
		CreatifAdmin,
		SauvegarderQuitter
	}

	private ModeEcranBarreMenu _ecranBarreCourant = ModeEcranBarreMenu.Inventaire;
	private Panel _ongletInventaireBarre;
	private Panel _ongletFutureStateBarre;
	private Panel _ongletMetierBarre;
	private Panel _ongletAnalyseurBarre;
	private Panel _ongletCreatifBarre;
	private Panel _ongletQuitterBarre;
	private HBoxContainer _corpsHBoxRef;
	private Panel _panneauSauvegarderQuitter;
	private Panel _panneauAnalyseur;
	private GridContainer _grilleAnalyseur;
	private Label _lblAnalyseurChance;
	private Label _lblAnalyseurMessage;
	private Button _btnAnalyser;
	private Panel[] _slotsAnalyseur;
	private Label[] _lblAnalyseur;
	private SubViewportContainer[] _vpAnalyseur;
	private MeshInstance3D[] _meshPreviewAnalyseur;
	private Panel _panneauCreatifAdmin;
	private ItemList _listeCreatifAdmin;
	private LineEdit _rechercheCreatifAdmin;
	private Label _lblPaginationCreatifAdmin;
	private Button _btnPagePrecCreatifAdmin;
	private Button _btnPageSuivCreatifAdmin;
	private Button _btnInjecterCreatifAdmin;
	private OptionButton _filtreCategorieCreatifAdmin;
	private readonly List<EntreeCatalogueCreatifAdmin> _catalogueCreatifAdmin = new List<EntreeCatalogueCreatifAdmin>();
	private readonly List<int> _indicesFiltresCreatifAdmin = new List<int>();
	private int _pageCreatifAdmin;
	private const int TaillePageCreatifAdmin = 24;
	private CategorieCreatifAdmin _categorieCreatifActive = CategorieCreatifAdmin.Tous;

	private enum CategorieCreatifAdmin
	{
		TousVariants,
		Tous,
		Structures,
		Bois,
		Pierre,
		Outils,
		Consommables,
		Admin
	}

	private struct EntreeCatalogueCreatifAdmin
	{
		public SlotInventaire Slot;
		public string Nom;
		public string Suffixe;
		public CategorieCreatifAdmin Categorie;
	}

	private Panel _conteneurFlottantCurseur;
	private SubViewportContainer _vpCurseurSouris;
	private MeshInstance3D _meshCurseurSouris;
	private Label _lblCurseurSouris;
	private Label _lblCurseurQuantite;
	/// <summary>Infobulle près du curseur : nom exact du slot survolé (débogage des noms / ADN).</summary>
	private Panel _panneauInfobulleSlot;
	private Label _lblInfobulleSlot;
	private Panel _panneauSanteCorps;
	/// <summary>Zone sous les barres de PV (menu Q) pour le bloc faim / énergie du joueur.</summary>
	private VBoxContainer _boiteFaimEnergieExterne;
	private Label _lblSanteGlobaleCorps;
	private Label _lblForceEtMultiplicateur;
	private Label _lblPoidsMaxSousApercu;
	private readonly Dictionary<string, ProgressBar> _barresSanteCorps = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Label> _labelsSanteCorps = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, StyleBoxFlat> _stylesRemplissageSanteCorps = new(StringComparer.OrdinalIgnoreCase);
	private const float DistanceCameraApercuJoueurCorps = 2.18f;
	private const float DecalageLateralCameraApercuJoueurCorps = 0.00f;
	private const float HauteurCameraApercuJoueurCorps = -0.56f;
	private const float HauteurCibleCameraApercuJoueurCorps = -0.06f;
	private SubViewportContainer _vpApercuJoueurCorps;
	private SubViewport _svApercuJoueurCorps;
	private Camera3D _cameraApercuJoueurCorps;

	/// <summary>Évite <c>SynchroniserPreviewSlotMenu</c> quand le slot n’a pas changé (même rendu).</summary>
	private ulong _empreinteMainGLast;
	private ulong _empreinteMainDLast;
	private ulong _empreinteCeintureLast;
	private ulong _empreinteSacLast;
	private ulong[] _empreinteCraftLast;
	private ulong[] _empreinteCoffreLast;
	private ulong[] _empreinteAnalyseurLast;
	private ulong _empreinteResultatCraftLast;
	private float _accumulateurInfobulleInventaire;
	private const float IntervalleInfobulleInventaireSec = 0.05f;
	private int _compteurFrameMenuProcess;

	private static ulong EmpreinteSlotPourPreviewMenu(in SlotInventaire s)
	{
		if (s.EstVide) return 0UL;
		var hc = new HashCode();
		hc.Add(s.ID);
		hc.Add(s.IndexMorphologique);
		hc.Add(s.IndexChimique);
		hc.Add(s.IndexTaille);
		hc.Add(s.NiveauFracture);
		hc.Add(s.EstUnEclat);
		hc.Add(s.IndexBotanique);
		hc.Add(s.Quantite);
		hc.Add(s.GenomeAssemblage ?? "");
		hc.Add(s.CleConteneur ?? "");
		hc.Add(Mathf.RoundToInt(s.DurabiliteOutilActuelle * 1000f));
		hc.Add(Mathf.RoundToInt(s.DurabiliteOutilMax * 1000f));
		hc.Add(s.IndexTailleLameRoche);
		hc.Add(s.ScaleEclat.X);
		hc.Add(s.ScaleEclat.Y);
		hc.Add(s.ScaleEclat.Z);
		if (s.EstUnEclat && s.MeshEclat != null && GodotObject.IsInstanceValid(s.MeshEclat))
			hc.Add(s.MeshEclat.GetInstanceId());
		return unchecked((ulong)(uint)hc.ToHashCode());
	}

	private void ResoudreReferencesSlotsMains()
	{
		if (MainGaucheSlot == null || !GodotObject.IsInstanceValid(MainGaucheSlot))
			MainGaucheSlot = GetNodeOrNull<Panel>(CheminMainGauche) ?? FindChild("MainGaucheSlot", true, false) as Panel;
		if (MainDroiteSlot == null || !GodotObject.IsInstanceValid(MainDroiteSlot))
			MainDroiteSlot = GetNodeOrNull<Panel>(CheminMainDroite) ?? FindChild("MainDroiteSlot", true, false) as Panel;
		if (EquipementCorpsSlot == null || !GodotObject.IsInstanceValid(EquipementCorpsSlot))
			EquipementCorpsSlot = GetNodeOrNull<Panel>(CheminEquipementCorpsSlot) ?? FindChild("EquipementCorpsSlot", true, false) as Panel;
		if (EquipementSacSlot == null || !GodotObject.IsInstanceValid(EquipementSacSlot))
			EquipementSacSlot = GetNodeOrNull<Panel>(CheminEquipementSacSlot) ?? FindChild("EquipementSacSlot", true, false) as Panel;
		if (CarnetSavoirSlot == null || !GodotObject.IsInstanceValid(CarnetSavoirSlot))
			CarnetSavoirSlot = GetNodeOrNull<Panel>(CheminCarnetSavoirSlot) ?? FindChild("CarnetSavoirSlot", true, false) as Panel;
	}

	private void ResoudreGrilleAssemblage()
	{
		if (GrilleAssemblage != null && GodotObject.IsInstanceValid(GrilleAssemblage)) return;
		GrilleAssemblage = GetNodeOrNull<GridContainer>(CheminGrilleAssemblage)
			?? FindChild("GrilleAssemblage", true, false) as GridContainer;
	}

	private void ResoudreSlotResultatCraft()
	{
		if (SlotResultatCraft != null && GodotObject.IsInstanceValid(SlotResultatCraft)) return;
		SlotResultatCraft = GetNodeOrNull<Panel>(CheminSlotResultatCraft)
			?? FindChild("CraftSortie", true, false) as Panel;
	}

	private GridContainer ObtenirGrilleSac()
	{
		var g = GetNodeOrNull<GridContainer>(CheminGrilleSac);
		return g ?? FindChild("GrilleSac", true, false) as GridContainer;
	}

	private GridContainer ObtenirGrilleCeintureStockage()
	{
		var g = GetNodeOrNull<GridContainer>(CheminGrilleCeintureStockage);
		return g ?? FindChild("GrilleCeintureStockage", true, false) as GridContainer;
	}

	private Panel ObtenirCadreCoffreBois()
	{
		return GetNodeOrNull<Panel>(CheminCadreCoffreBois) ?? FindChild("CadreCoffreBois", true, false) as Panel;
	}

	private GridContainer ObtenirGrilleCoffreBois()
	{
		var g = GetNodeOrNull<GridContainer>(CheminGrilleCoffreBois);
		return g ?? FindChild("GrilleCoffreBois", true, false) as GridContainer;
	}

	private void MettreAJourVisibiliteLigneCraftVersusCoffre()
	{
		if (_joueurRef == null) return;
		ResoudreGrilleAssemblage();
		Panel cadreCoffre = ObtenirCadreCoffreBois();
		Control ligneCraft = GrilleAssemblage?.GetParent()?.GetParent() as Control;
		bool coffre = _joueurRef.StockageCoffreOuvert;
		if (cadreCoffre != null)
			cadreCoffre.Visible = coffre;
		if (ligneCraft != null)
			ligneCraft.Visible = !coffre;
	}

	private Panel CreerCaseStockageSupplementaire(GridContainer grille, int idx)
	{
		Panel p = new Panel { Name = $"SlotAuto_{idx}", CustomMinimumSize = new Vector2(96, 96), MouseFilter = Control.MouseFilterEnum.Stop };
		if (grille != null && grille.GetChildCount() > 0 && grille.GetChild(0) is Panel template)
		{
			p.CustomMinimumSize = template.CustomMinimumSize;
			if (template.GetThemeStylebox("panel") is StyleBox sb)
				p.AddThemeStyleboxOverride("panel", (StyleBox)sb.Duplicate());
		}
		grille?.AddChild(p);
		return p;
	}

	private void ConnecterCaseStockageSiNecessaire(Panel p, int modeClic, int idx)
	{
		if (p == null) return;
		string cleMeta = $"ClickBound_{modeClic}";
		if (p.HasMeta(cleMeta)) return;
		p.GuiInput += e => TraiterClicInventaire(e, modeClic, idx);
		p.SetMeta(cleMeta, true);
	}

	private void AssurerCapaciteGrillesStockage()
	{
		if (_joueurRef == null) return;
		int capSac = _joueurRef.ASacEquipe() ? Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos) : 1;
		int capCeinture = _joueurRef.ACeintureSacochesEquipe() ? Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture) : 4;

		if (ObtenirGrilleSac() is GridContainer grilleSac)
		{
			grilleSac.Columns = Mathf.Max(1, Mathf.Min(2, capSac));
			while (grilleSac.GetChildCount() < capSac)
				CreerCaseStockageSupplementaire(grilleSac, grilleSac.GetChildCount());
			for (int i = 0; i < grilleSac.GetChildCount(); i++)
			{
				if (grilleSac.GetChild(i) is Panel p)
					ConnecterCaseStockageSiNecessaire(p, 6, i);
			}
		}

		if (ObtenirGrilleCeintureStockage() is GridContainer grilleCeinture)
		{
			grilleCeinture.Columns = capCeinture > 4 ? 4 : Mathf.Max(1, capCeinture);
			while (grilleCeinture.GetChildCount() < capCeinture)
				CreerCaseStockageSupplementaire(grilleCeinture, grilleCeinture.GetChildCount());
			for (int i = 0; i < grilleCeinture.GetChildCount(); i++)
			{
				if (grilleCeinture.GetChild(i) is Panel p)
					ConnecterCaseStockageSiNecessaire(p, 7, i);
			}
		}
	}

	public void Initialiser(Joueur joueur)
	{
		_joueurRef = joueur;
		ResoudreReferencesSlotsMains();
		AssurerPanneauSanteCorps();
		_lblMainGauche = TrouverOuCreerLabel(MainGaucheSlot, "Main G\n[Vide]");
		_lblMainDroite = TrouverOuCreerLabel(MainDroiteSlot, "Main D\n[Vide]");
		AssurerPreviews3DMains();
		ConnecterClicsInventaire();
		if (!Engine.IsEditorHint())
		{
			SetProcess(false);
			CallDeferred(nameof(ConnecterClicsInventaire));
			CallDeferred(nameof(ConfigurerBarreOngletsJeu));
			CallDeferred(nameof(RafraichirMenu));
			CallDeferred(nameof(AssurerPanneauSanteCorps));
		}
	}

	private void AssurerPanneauSanteCorps()
	{
		if (VueJoueurPanel == null || !GodotObject.IsInstanceValid(VueJoueurPanel))
			VueJoueurPanel = GetNodeOrNull<Panel>(CheminVueJoueurPanel) ?? FindChild("VueJoueurPanel", true, false) as Panel;
		if (VueJoueurPanel == null)
			return;

		if (_panneauSanteCorps != null && GodotObject.IsInstanceValid(_panneauSanteCorps))
			return;

		if (VueJoueurPanel.GetNodeOrNull<Label>("Label") is Label labelScene)
			labelScene.Visible = false;

		_panneauSanteCorps = new Panel
		{
			Name = "PanneauSanteCorps",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_panneauSanteCorps.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_panneauSanteCorps.OffsetLeft = 8f;
		_panneauSanteCorps.OffsetTop = 8f;
		_panneauSanteCorps.OffsetRight = -8f;
		_panneauSanteCorps.OffsetBottom = -8f;
		VueJoueurPanel.AddChild(_panneauSanteCorps);

		var marge = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		marge.AddThemeConstantOverride("margin_left", 10);
		marge.AddThemeConstantOverride("margin_top", 10);
		marge.AddThemeConstantOverride("margin_right", 10);
		marge.AddThemeConstantOverride("margin_bottom", 10);
		_panneauSanteCorps.AddChild(marge);

		var colonne = new VBoxContainer
		{
			Name = "ColonneSanteCorps",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		colonne.AddThemeConstantOverride("separation", 8);
		marge.AddChild(colonne);

		var titre = new Label
		{
			Text = "Anatomie / Points de vie",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		titre.AddThemeFontSizeOverride("font_size", 16);
		titre.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.98f));
		colonne.AddChild(titre);

		_lblSanteGlobaleCorps = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblSanteGlobaleCorps.AddThemeFontSizeOverride("font_size", 13);
		_lblSanteGlobaleCorps.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 0.86f));
		colonne.AddChild(_lblSanteGlobaleCorps);

		_lblForceEtMultiplicateur = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblForceEtMultiplicateur.AddThemeFontSizeOverride("font_size", 12);
		_lblForceEtMultiplicateur.AddThemeColorOverride("font_color", new Color(0.86f, 0.90f, 0.98f));
		colonne.AddChild(_lblForceEtMultiplicateur);

		var cadreApercu = new Panel
		{
			Name = "CadreApercuJoueurCorps",
			CustomMinimumSize = new Vector2(0, 380),
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		cadreApercu.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		colonne.AddChild(cadreApercu);
		AssurerApercuJoueurCorps(cadreApercu);

		_lblPoidsMaxSousApercu = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblPoidsMaxSousApercu.AddThemeFontSizeOverride("font_size", 12);
		_lblPoidsMaxSousApercu.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.78f));
		colonne.AddChild(_lblPoidsMaxSousApercu);

		colonne.AddChild(new HSeparator());

		var scroll = new ScrollContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		colonne.AddChild(scroll);

		var colonneSections = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		colonneSections.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		colonneSections.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(colonneSections);

		CreerLigneSanteCorps(colonneSections, "tete", "Tete");
		CreerLigneSanteCorps(colonneSections, "torse", "Torse");
		CreerLigneSanteCorps(colonneSections, "bras_gauche", "Bras gauche");
		CreerLigneSanteCorps(colonneSections, "bras_droit", "Bras droit");
		CreerLigneSanteCorps(colonneSections, "jambe_gauche", "Jambe gauche");
		CreerLigneSanteCorps(colonneSections, "jambe_droite", "Jambe droite");

		colonne.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
		_boiteFaimEnergieExterne = new VBoxContainer
		{
			Name = "BoiteFaimEnergieExterne",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_boiteFaimEnergieExterne.AddThemeConstantOverride("separation", 6);
		_boiteFaimEnergieExterne.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		colonne.AddChild(_boiteFaimEnergieExterne);
	}

	private void AssurerApercuJoueurCorps(Panel parent)
	{
		if (parent == null || (_vpApercuJoueurCorps != null && GodotObject.IsInstanceValid(_vpApercuJoueurCorps)))
			return;

		var centreApercu = new CenterContainer
		{
			Name = "CentreApercuJoueurCorps",
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		centreApercu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centreApercu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		parent.AddChild(centreApercu);

		_vpApercuJoueurCorps = new SubViewportContainer
		{
			Name = "ApercuJoueurCorpsViewport",
			Stretch = true,
			CustomMinimumSize = new Vector2(250f, 360f),
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_vpApercuJoueurCorps.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		_vpApercuJoueurCorps.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		centreApercu.AddChild(_vpApercuJoueurCorps);

		_svApercuJoueurCorps = new SubViewport
		{
			Size = new Vector2I(300, 460),
			RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
			World3D = _joueurRef?.GetWorld3D(),
			TransparentBg = true
		};
		_vpApercuJoueurCorps.AddChild(_svApercuJoueurCorps);

		_cameraApercuJoueurCorps = new Camera3D
		{
			Name = "CameraApercuJoueurCorps",
			Fov = 33f,
			Near = 0.02f,
			Far = 180f,
			Current = true
		};
		_svApercuJoueurCorps.AddChild(_cameraApercuJoueurCorps);

		var light = new DirectionalLight3D
		{
			Name = "LightApercuJoueurCorps",
			LightEnergy = 1.25f,
			RotationDegrees = new Vector3(-35f, 40f, 0f)
		};
		// Cette lumière d'aperçu partage le World3D principal: l'empêcher de créer un disque/halo dans le ciel.
		light.Set("sky_mode", 1); // LightOnly
		light.Set("light_volumetric_fog_energy", 0.0f);
		_svApercuJoueurCorps.AddChild(light);
		var lightRemplissage = new OmniLight3D
		{
			Name = "FillApercuJoueurCorps",
			Position = new Vector3(-0.5f, 1.15f, 1.2f),
			LightEnergy = 0.35f,
			OmniRange = 5.0f
		};
		_svApercuJoueurCorps.AddChild(lightRemplissage);

		MettreAJourCameraApercuJoueurCorps(0f);
	}

	private void CreerLigneSanteCorps(VBoxContainer parent, string cleSection, string nomSection)
	{
		var ligne = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		ligne.AddThemeConstantOverride("separation", 2);
		parent.AddChild(ligne);

		var entete = new Label
		{
			Text = nomSection,
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		entete.AddThemeFontSizeOverride("font_size", 12);
		entete.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.95f));
		ligne.AddChild(entete);

		var barre = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 100,
			Value = 100,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 16),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		barre.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var fondBarre = new StyleBoxFlat
		{
			BgColor = new Color(0.22f, 0.22f, 0.22f, 1f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6
		};
		var remplissage = new StyleBoxFlat
		{
			BgColor = new Color(0.25f, 0.82f, 0.35f, 1f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6
		};
		barre.AddThemeStyleboxOverride("background", fondBarre);
		barre.AddThemeStyleboxOverride("fill", remplissage);
		ligne.AddChild(barre);

		var lblInfos = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		lblInfos.AddThemeFontSizeOverride("font_size", 11);
		lblInfos.AddThemeColorOverride("font_color", new Color(0.77f, 0.82f, 0.88f));
		ligne.AddChild(lblInfos);

		_barresSanteCorps[cleSection] = barre;
		_labelsSanteCorps[cleSection] = lblInfos;
		_stylesRemplissageSanteCorps[cleSection] = remplissage;
	}

	private static Color CouleurSanteDepuisRatio(float ratio)
	{
		if (ratio >= 0.66f) return new Color(0.25f, 0.82f, 0.35f, 1f);
		if (ratio >= 0.33f) return new Color(0.95f, 0.67f, 0.20f, 1f);
		return new Color(0.88f, 0.22f, 0.22f, 1f);
	}

	private void RafraichirPanneauSanteCorps()
	{
		if (_joueurRef == null)
			return;
		AssurerPanneauSanteCorps();
		if (_svApercuJoueurCorps != null && GodotObject.IsInstanceValid(_svApercuJoueurCorps))
			_svApercuJoueurCorps.World3D = _joueurRef.GetWorld3D();
		if (_barresSanteCorps.Count == 0)
			return;

		float ratioGlobal = _joueurRef.ObtenirRatioSanteGlobaleCorps();
		if (_lblSanteGlobaleCorps != null)
			_lblSanteGlobaleCorps.Text = $"Sante globale: {(ratioGlobal * 100f):F0}%";
		if (_lblForceEtMultiplicateur != null)
		{
			ulong niveauForce = _joueurRef.ObtenirNiveauFutureState("Force");
			float multiplicateurForce = _joueurRef.ObtenirMultiplicateurCapaciteChargeForce();
			_lblForceEtMultiplicateur.Text = $"Force: niv {niveauForce:N0} | Multiplicateur: x{multiplicateurForce:F4}";
		}
		if (_lblPoidsMaxSousApercu != null)
		{
			float poidsActuelKg = _joueurRef.ObtenirPoidsTotalPorteKg();
			float poidsMaxKg = _joueurRef.ObtenirCapacitePoidsMaxKg();
			_lblPoidsMaxSousApercu.Text = $"Poids: {poidsActuelKg:F2} / {poidsMaxKg:F2} kg";
		}

		IReadOnlyList<Joueur.SectionSanteCorps> sections = _joueurRef.ObtenirEtatSanteCorps();
		for (int i = 0; i < sections.Count; i++)
		{
			Joueur.SectionSanteCorps section = sections[i];
			if (_barresSanteCorps.TryGetValue(section.Cle, out ProgressBar barre))
			{
				barre.MaxValue = Mathf.Max(1, section.PointsVieMax);
				barre.Value = Mathf.Clamp(section.PointsVie, 0, section.PointsVieMax);
				if (_stylesRemplissageSanteCorps.TryGetValue(section.Cle, out StyleBoxFlat styleRemplissage))
				{
					float ratioSection = section.PointsVieMax > 0 ? section.PointsVie / (float)section.PointsVieMax : 0f;
					styleRemplissage.BgColor = CouleurSanteDepuisRatio(ratioSection);
				}
			}
			if (_labelsSanteCorps.TryGetValue(section.Cle, out Label lbl))
				lbl.Text = $"{section.PointsVie:F2}/{section.PointsVieMax:F2} PV  |  Matiere: {section.Matiere}";
		}
	}

	private void AssurerPreviews3DMains()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreReferencesSlotsMains();
		if (MainGaucheSlot == null || MainDroiteSlot == null) return;
		if (_meshPreviewMenuG == null || !GodotObject.IsInstanceValid(_meshPreviewMenuG))
		{
			_meshPreviewMenuG = CreerViewportPreviewDansSlot(MainGaucheSlot, "ViewportMenuMainG", out _vpMenuGauche);
			_meshPreviewMenuD = CreerViewportPreviewDansSlot(MainDroiteSlot, "ViewportMenuMainD", out _vpMenuDroite);
		}
		if (EquipementCorpsSlot != null && (_meshPreviewMenuCeinture == null || !GodotObject.IsInstanceValid(_meshPreviewMenuCeinture)))
		{
			_meshPreviewMenuCeinture = CreerViewportPreviewDansSlot(EquipementCorpsSlot, "ViewportMenuCeinture", out _vpMenuCeinture);
			_lblSlotCeinture = TrouverOuCreerLabel(EquipementCorpsSlot, "Ceinture\n[vide]");
		}
		if (EquipementSacSlot != null && (_meshPreviewMenuSacEquip == null || !GodotObject.IsInstanceValid(_meshPreviewMenuSacEquip)))
		{
			_meshPreviewMenuSacEquip = CreerViewportPreviewDansSlot(EquipementSacSlot, "ViewportMenuSacEquip", out _vpMenuSacEquip);
			_lblSlotSacEquip = TrouverOuCreerLabel(EquipementSacSlot, "Sac\n[vide]");
		}
		if (CarnetSavoirSlot != null && (_meshPreviewMenuCarnet == null || !GodotObject.IsInstanceValid(_meshPreviewMenuCarnet)))
		{
			_meshPreviewMenuCarnet = CreerViewportPreviewDansSlot(CarnetSavoirSlot, "ViewportMenuCarnet", out _vpMenuCarnet);
			_lblSlotCarnet = TrouverOuCreerLabel(CarnetSavoirSlot, "Carnet\n[vide]");
		}
	}

	private static MeshInstance3D CreerViewportPreviewDansSlot(Panel panel, string nomConteneur, out SubViewportContainer holder)
	{
		holder = new SubViewportContainer
		{
			Name = nomConteneur,
			Stretch = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		holder.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.AddChild(holder);
		panel.MoveChild(holder, 0);

		var viewport = new SubViewport
		{
			Size = new Vector2I(72, 72),
			RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
			World3D = new World3D(),
			TransparentBg = true
		};
		holder.AddChild(viewport);

		var cam = new Camera3D();
		cam.SetOrthogonal(0.5f, 0.01f, 10f);
		cam.Position = new Vector3(0, 0, 1.2f);
		viewport.AddChild(cam);

		var meshNode = new MeshInstance3D();
		meshNode.Position = Vector3.Zero;
		meshNode.RotationDegrees = new Vector3(-20, 25, 0);
		viewport.AddChild(meshNode);

		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-45, 30, 0);
		light.Set("sky_mode", 1);
		viewport.AddChild(light);

		return meshNode;
	}

	private void ConnecterClicsInventaire()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreReferencesSlotsMains();
		ResoudreGrilleAssemblage();
		ResoudreSlotResultatCraft();
		void Branche(Panel pan, Control.GuiInputEventHandler fn)
		{
			if (pan == null) return;
			pan.MouseFilter = Control.MouseFilterEnum.Stop;
			pan.GuiInput += fn;
		}
		if (!_clicsMainsConnectes)
		{
			_clicsMainsConnectes = true;
			Branche(MainGaucheSlot, e => TraiterClicInventaire(e, 0));
			Branche(MainDroiteSlot, e => TraiterClicInventaire(e, 1));
		}
		if (!_clicsCraftConnectes && GrilleAssemblage != null)
		{
			_clicsCraftConnectes = true;
			GrilleAssemblage.MouseFilter = Control.MouseFilterEnum.Ignore;
			int n = GrilleAssemblage.GetChildCount();
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				if (GrilleAssemblage.GetChild(i) is Panel cp)
					Branche(cp, e => TraiterClicInventaire(e, 2, idx));
			}
		}
		if (!_clicsSlotResultatCraftConnecte && SlotResultatCraft != null)
		{
			_clicsSlotResultatCraftConnecte = true;
			Branche(SlotResultatCraft, e => TraiterClicInventaire(e, 3));
		}
		ResoudreReferencesSlotsMains();
		if (!_clicsSlotCeintureConnecte && EquipementCorpsSlot != null)
		{
			_clicsSlotCeintureConnecte = true;
			Branche(EquipementCorpsSlot, e => TraiterClicInventaire(e, 4));
		}
		if (!_clicsSlotSacConnecte && EquipementSacSlot != null)
		{
			_clicsSlotSacConnecte = true;
			Branche(EquipementSacSlot, e => TraiterClicInventaire(e, 5));
		}
		if (CarnetSavoirSlot != null && !CarnetSavoirSlot.HasMeta("ClickBound_8"))
		{
			Branche(CarnetSavoirSlot, e => TraiterClicInventaire(e, 8));
			CarnetSavoirSlot.SetMeta("ClickBound_8", true);
		}
		if (!_clicsGrilleSacConnectes && ObtenirGrilleSac() is GridContainer grilleSac)
		{
			_clicsGrilleSacConnectes = true;
			grilleSac.MouseFilter = Control.MouseFilterEnum.Ignore;
			int n = grilleSac.GetChildCount();
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				if (grilleSac.GetChild(i) is Panel cp)
				{
					Branche(cp, e => TraiterClicInventaire(e, 6, idx));
					cp.SetMeta("ClickBound_6", true);
				}
			}
		}
		if (!_clicsGrilleCeintureStockageConnectes && ObtenirGrilleCeintureStockage() is GridContainer grilleCeint)
		{
			_clicsGrilleCeintureStockageConnectes = true;
			grilleCeint.MouseFilter = Control.MouseFilterEnum.Ignore;
			for (int i = 0; i < grilleCeint.GetChildCount(); i++)
			{
				int idx = i;
				if (grilleCeint.GetChild(i) is Panel cp)
				{
					Branche(cp, e => TraiterClicInventaire(e, 7, idx));
					cp.SetMeta("ClickBound_7", true);
				}
			}
		}
		if (!_clicsGrilleCoffreConnectes && ObtenirGrilleCoffreBois() is GridContainer grilleCoffre)
		{
			_clicsGrilleCoffreConnectes = true;
			grilleCoffre.MouseFilter = Control.MouseFilterEnum.Ignore;
			int nC = grilleCoffre.GetChildCount();
			for (int i = 0; i < nC; i++)
			{
				int idx = i;
				if (grilleCoffre.GetChild(i) is Panel cp)
					Branche(cp, e => TraiterClicInventaire(e, 9, idx));
			}
		}
		AssurerPanneauAnalyseur();
		if (!_clicsGrilleAnalyseurConnectes && _slotsAnalyseur != null && _slotsAnalyseur.Length > 0)
		{
			_clicsGrilleAnalyseurConnectes = true;
			for (int i = 0; i < _slotsAnalyseur.Length; i++)
			{
				int idx = i;
				Panel cp = _slotsAnalyseur[i];
				if (cp == null) continue;
				Branche(cp, e => TraiterClicInventaire(e, 10, idx));
			}
		}
		AssurerCapaciteGrillesStockage();
	}

	private void TraiterClicInventaire(InputEvent e, int mode, int craftIdx = -1)
	{
		if (_joueurRef == null) return;
		if (e is not InputEventMouseButton mb || !mb.Pressed)
			return;
		bool clicGauche = mb.ButtonIndex == MouseButton.Left;
		bool clicDroit = mb.ButtonIndex == MouseButton.Right;
		if (!clicGauche && !clicDroit)
			return;

		if (mode == 0)
			InteragirCurseurAvecSlot(ref _joueurRef.MainGauche, clicGauche, clicDroit);
		else if (mode == 1)
			InteragirCurseurAvecSlot(ref _joueurRef.MainDroite, clicGauche, clicDroit);
		else if (mode == 2 && craftIdx >= 0)
		{
			if (!_joueurRef.CraftGrille3x3AuTable && craftIdx >= 4)
				return;
			var g = _joueurRef.ObtenirGrilleCraftAffichee();
			if (g == null || craftIdx >= g.Length)
				return;
            if (_joueurRef.StockageRackBatonsOuvert)
            {
                TraiterClicRackBatons(ref _joueurRef.RefSlotCraft(craftIdx), clicGauche, clicDroit, craftIdx);
                if (_joueurRef.RackBatonsOuvert != null && GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert))
                {
                    if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBatons)
                        _joueurRef.SynchroniserVisuelRackBatons(_joueurRef.RackBatonsOuvert);
                    else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBuches)
                        _joueurRef.SynchroniserVisuelRackBuches(_joueurRef.RackBatonsOuvert);
                    else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche)
                        _joueurRef.RackBatonsOuvert.SynchroniserCombustiblePitFeuRocheDepuisGrille();
                }
                _joueurRef.VerifierRecettes();
                GetViewport()?.SetInputAsHandled();
                _joueurRef.RafraichirHUD();
                RafraichirMenu();
                return;
            }
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotCraft(craftIdx), clicGauche, clicDroit);
			_joueurRef.VerifierRecettes();
		}
		else if (mode == 3)
		{
			if (clicGauche && _curseurMenu.EstVide && !_joueurRef.SlotResultatCraft.EstVide)
			{
				_curseurMenu = _joueurRef.AppliquerBonusMetierTraisageAuResultatCraft(_joueurRef.SlotResultatCraft);
				_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
				_joueurRef.ConsommerIngredientsCraft();
				_joueurRef.VerifierRecettes();
			}
			else
				return;
		}
		else if (mode == 4)
		{
			if (!EchangerCurseurAvecEquipementCeintureSiValide())
				return;
		}
		else if (mode == 5)
		{
			if (!EchangerCurseurAvecEquipementSacSiValide())
				return;
		}
		else if (mode == 6 && craftIdx >= 0)
		{
			int capSac = Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos);
			if (!_joueurRef.ASacEquipe() || craftIdx < 0 || craftIdx >= capSac) return;
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotSac(craftIdx), clicGauche, clicDroit, slotSacStockage: true);
		}
		else if (mode == 7 && craftIdx >= 0)
		{
			int capCeinture = Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture);
			if (!_joueurRef.ACeintureSacochesEquipe() || craftIdx < 0 || craftIdx >= capCeinture) return;
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotCeintureStockage(craftIdx), clicGauche, clicDroit, slotCeintureStockage: true, indexSlotCeinture: craftIdx);
		}
		else if (mode == 8)
		{
			if (!EchangerCurseurAvecEquipementCarnetSiValide())
				return;
		}
		else if (mode == 9 && craftIdx >= 0)
		{
			if (!_joueurRef.StockageCoffreOuvert || craftIdx < 0 || craftIdx > 9) return;
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotCoffreStockage(craftIdx), clicGauche, clicDroit, slotCoffreStockage: true);
			_joueurRef.VerifierRecettes();
		}
		else if (mode == 10 && craftIdx >= 0)
		{
			if (_joueurRef.GrilleAnalyseurManuel == null || craftIdx >= _joueurRef.GrilleAnalyseurManuel.Length)
				return;
			InteragirCurseurAvecSlot(ref _joueurRef.GrilleAnalyseurManuel[craftIdx], clicGauche, clicDroit);
		}
		else
			return;

		GetViewport()?.SetInputAsHandled();
		_joueurRef.RafraichirHUD();
	}

	private int CompterQuantiteTotaleRack()
	{
		if (_joueurRef == null || !_joueurRef.StockageRackBatonsOuvert) return 0;
		return _joueurRef.CompterQuantiteRackOuvert();
	}

	private void DeposerDepuisCurseurVersRack(ref SlotInventaire destination, int quantiteSouhaitee)
	{
		if (_joueurRef == null || _curseurMenu.EstVide || !_joueurRef.EstSlotStockableDansRackOuvert(_curseurMenu)) return;
		int qCur = Joueur.ObtenirQuantiteSlot(_curseurMenu);
		if (qCur <= 0) return;
		int capacite = _joueurRef.ObtenirCapaciteRackOuvert();
		int espaceGlobal = Mathf.Max(0, capacite - CompterQuantiteTotaleRack());
		if (espaceGlobal <= 0) return;

		if (destination.EstVide)
		{
			int move = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), Mathf.Min(espaceGlobal, capacite));
			if (move <= 0) return;
			destination = _curseurMenu;
			destination.Quantite = move;
			if (qCur - move <= 0) _curseurMenu = new SlotInventaire();
			else _curseurMenu.Quantite = qCur - move;
			return;
		}

		if (!Joueur.SontEmpilables(destination, _curseurMenu)) return;
		int qDst = Joueur.ObtenirQuantiteSlot(destination);
		int moveStack = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), Mathf.Min(espaceGlobal, capacite - qDst));
		if (moveStack <= 0) return;
		destination.Quantite = qDst + moveStack;
		if (qCur - moveStack <= 0) _curseurMenu = new SlotInventaire();
		else _curseurMenu.Quantite = qCur - moveStack;
	}

	private void DeposerDepuisCurseurVersSlotSimple(ref SlotInventaire destination, int quantiteSouhaitee)
	{
		if (_curseurMenu.EstVide) return;
		int qCur = Joueur.ObtenirQuantiteSlot(_curseurMenu);
		if (qCur <= 0) return;

		if (destination.EstVide)
		{
			int maxPile = Mathf.Max(1, Joueur.ObtenirPileMax(_curseurMenu));
			int move = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), maxPile);
			if (move <= 0) return;
			destination = _curseurMenu;
			destination.Quantite = move;
			if (qCur - move <= 0) _curseurMenu = new SlotInventaire();
			else _curseurMenu.Quantite = qCur - move;
			return;
		}

		if (!Joueur.SontEmpilables(destination, _curseurMenu)) return;
		int qDst = Joueur.ObtenirQuantiteSlot(destination);
		int maxPileDst = Mathf.Max(1, Joueur.ObtenirPileMax(destination));
		int moveStack = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), Mathf.Max(0, maxPileDst - qDst));
		if (moveStack <= 0) return;
		destination.Quantite = qDst + moveStack;
		if (qCur - moveStack <= 0) _curseurMenu = new SlotInventaire();
		else _curseurMenu.Quantite = qCur - moveStack;
	}

	private void TraiterClicRackBatons(ref SlotInventaire slotRack, bool clicGauche, bool clicDroit, int rackIdx)
	{
		bool pitRoche = _joueurRef != null && _joueurRef.RackOuvertEstPitFeuRoche();
		if (pitRoche)
		{
			if (clicGauche)
			{
				if (_curseurMenu.EstVide)
				{
					if (!slotRack.EstVide)
					{
						_curseurMenu = slotRack;
						_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
						slotRack = new SlotInventaire();
					}
				}
				else
				{
					if (_joueurRef.EstSlotSortiePitFeuRoche(rackIdx))
						return;
					if (!_joueurRef.EstSlotStockableDansPitFeuRocheIndex(rackIdx, _curseurMenu))
						return;
					DeposerDepuisCurseurVersSlotSimple(ref slotRack, int.MaxValue);
				}
				return;
			}

			if (clicDroit)
			{
				if (_curseurMenu.EstVide)
				{
					if (!slotRack.EstVide)
					{
						int q = Joueur.ObtenirQuantiteSlot(slotRack);
						_curseurMenu = slotRack;
						_curseurMenu.Quantite = 1;
						if (q <= 1) slotRack = new SlotInventaire();
						else slotRack.Quantite = q - 1;
					}
				}
				else
				{
					if (_joueurRef.EstSlotSortiePitFeuRoche(rackIdx))
						return;
					if (!_joueurRef.EstSlotStockableDansPitFeuRocheIndex(rackIdx, _curseurMenu))
						return;
					DeposerDepuisCurseurVersSlotSimple(ref slotRack, 1);
				}
			}
			return;
		}

		// Rack dédié: capacité globale pilotée par le type de rack ouvert (bâtons/bûches).
		if (clicGauche)
		{
			if (_curseurMenu.EstVide)
			{
				if (!slotRack.EstVide)
				{
					_curseurMenu = slotRack;
					_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
					slotRack = new SlotInventaire();
				}
			}
			else
			{
				DeposerDepuisCurseurVersRack(ref slotRack, int.MaxValue);
			}
			return;
		}

		if (clicDroit)
		{
			if (_curseurMenu.EstVide)
			{
				if (!slotRack.EstVide)
				{
					int q = Joueur.ObtenirQuantiteSlot(slotRack);
					_curseurMenu = slotRack;
					_curseurMenu.Quantite = 1;
					if (q <= 1) slotRack = new SlotInventaire();
					else slotRack.Quantite = q - 1;
				}
			}
			else
			{
				DeposerDepuisCurseurVersRack(ref slotRack, 1);
			}
		}
	}

	private static SlotInventaire CopierSlotUnitaire(SlotInventaire src)
	{
		var s = src;
		s.Quantite = 1;
		return s;
	}

	private static bool PeutEmpiler(SlotInventaire a, SlotInventaire b, int maxPile) => Joueur.SontEmpilables(a, b) && maxPile > 1;

	private int ObtenirMultiplicateurPileSlotSac()
	{
		if (_joueurRef == null || !_joueurRef.ASacEquipe()) return 1;
		return Joueur.ObtenirMultiplicateurPileSac(_joueurRef.EquipementSacDos);
	}

	private int ObtenirMultiplicateurPileSlotCeinture(int indexSlotCeinture)
	{
		if (_joueurRef == null || !_joueurRef.ACeintureSacochesEquipe()) return 1;
		return Joueur.ObtenirMultiplicateurPileCeintureSlot(_joueurRef.EquipementCeinture, indexSlotCeinture);
	}

	private int ObtenirPileMaxContexte(SlotInventaire slot, bool slotSacStockage, bool slotCeintureStockage, int indexSlotCeinture = -1)
	{
		int max = Joueur.ObtenirPileMax(slot);
		if (slotSacStockage) max *= ObtenirMultiplicateurPileSlotSac();
		if (slotCeintureStockage) max *= ObtenirMultiplicateurPileSlotCeinture(indexSlotCeinture);
		return max;
	}

	private void InteragirCurseurAvecSlot(ref SlotInventaire slot, bool clicGauche, bool clicDroit, bool slotSacStockage = false, bool slotCeintureStockage = false, int indexSlotCeinture = -1, bool slotCoffreStockage = false)
	{
		if (clicGauche)
		{
			InteractionClicGauche(ref slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture, slotCoffreStockage);
			return;
		}
		if (clicDroit)
			InteractionClicDroit(ref slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture, slotCoffreStockage);
	}

	private void InteractionClicGauche(ref SlotInventaire slot, bool slotSacStockage, bool slotCeintureStockage, int indexSlotCeinture, bool slotCoffreStockage = false)
	{
		if (slotCoffreStockage && !_curseurMenu.EstVide && Joueur.EstObjetInterditDansCoffre(_curseurMenu))
		{
			if (slot.EstVide)
				return;
			if (!PeutEmpiler(slot, _curseurMenu, ObtenirPileMaxContexte(slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture)))
				return;
		}
		if (_curseurMenu.EstVide)
		{
			_curseurMenu = slot;
			if (!_curseurMenu.EstVide) _curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
			slot = new SlotInventaire();
			return;
		}
		if (slot.EstVide)
		{
			slot = _curseurMenu;
			slot.Quantite = Joueur.ObtenirQuantiteSlot(slot);
			_curseurMenu = new SlotInventaire();
			return;
		}
		int max = ObtenirPileMaxContexte(slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture);
		if (PeutEmpiler(slot, _curseurMenu, max))
		{
			int qDst = Joueur.ObtenirQuantiteSlot(slot);
			int qSrc = Joueur.ObtenirQuantiteSlot(_curseurMenu);
			int place = Mathf.Max(0, max - qDst);
			int depose = Mathf.Min(place, qSrc);
			if (depose > 0)
			{
				slot.Quantite = qDst + depose;
				qSrc -= depose;
				if (qSrc <= 0) _curseurMenu = new SlotInventaire();
				else _curseurMenu.Quantite = qSrc;
				return;
			}
		}
		if (slotCoffreStockage && Joueur.EstObjetInterditDansCoffre(_curseurMenu))
			return;
		var a = _curseurMenu;
		_curseurMenu = slot;
		slot = a;
		_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
		slot.Quantite = Joueur.ObtenirQuantiteSlot(slot);
	}

	private void InteractionClicDroit(ref SlotInventaire slot, bool slotSacStockage, bool slotCeintureStockage, int indexSlotCeinture, bool slotCoffreStockage = false)
	{
		if (_curseurMenu.EstVide)
		{
			if (slot.EstVide) return;
			int q = Joueur.ObtenirQuantiteSlot(slot);
			int prendre = Mathf.CeilToInt(q * 0.5f);
			_curseurMenu = slot;
			_curseurMenu.Quantite = prendre;
			int reste = q - prendre;
			if (reste <= 0) slot = new SlotInventaire();
			else slot.Quantite = reste;
			return;
		}
		if (slot.EstVide)
		{
			if (slotCoffreStockage && Joueur.EstObjetInterditDansCoffre(_curseurMenu))
				return;
			slot = CopierSlotUnitaire(_curseurMenu);
			int qSrc = Joueur.ObtenirQuantiteSlot(_curseurMenu) - 1;
			if (qSrc <= 0) _curseurMenu = new SlotInventaire();
			else _curseurMenu.Quantite = qSrc;
			return;
		}
		int max = ObtenirPileMaxContexte(slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture);
		if (!PeutEmpiler(slot, _curseurMenu, max)) return;
		int qDst = Joueur.ObtenirQuantiteSlot(slot);
		if (qDst >= max) return;
		slot.Quantite = qDst + 1;
		int qSrc2 = Joueur.ObtenirQuantiteSlot(_curseurMenu) - 1;
		if (qSrc2 <= 0) _curseurMenu = new SlotInventaire();
		else _curseurMenu.Quantite = qSrc2;
	}

	private void EchangerCurseurAvec(ref SlotInventaire slot)
	{
		var a = _curseurMenu;
		_curseurMenu = slot;
		slot = a;
	}

	/// <summary>Échange curseur ↔ équipement ceinture : ceinture simple (102) ou ceinture à sacoches (104).</summary>
	private bool EchangerCurseurAvecEquipementCeintureSiValide()
	{
		if (_joueurRef == null) return false;
		if (!_curseurMenu.EstVide && _curseurMenu.ID != Joueur.IdObjetCeinturePoches && _curseurMenu.ID != Joueur.IdObjetCeintureSacoches)
		{
			GD.Print("ZERO-K : Ce slot rouge n’accepte que les ceintures.");
			return false;
		}
		SlotInventaire surCeinture = _joueurRef.EquipementCeinture;
		SlotInventaire depuisCurseur = _curseurMenu;
		_joueurRef.AssignerEquipementCeinture(depuisCurseur);
		_curseurMenu = surCeinture;
		return true;
	}

	private bool EchangerCurseurAvecEquipementSacSiValide()
	{
		if (_joueurRef == null) return false;
		if (!_curseurMenu.EstVide && _curseurMenu.ID != Joueur.IdObjetSacTier0)
		{
			GD.Print("ZERO-K : Ce slot n’accepte que le sac tier 0.");
			return false;
		}
		SlotInventaire surSac = _joueurRef.EquipementSacDos;
		SlotInventaire depuisCurseur = _curseurMenu;
		_joueurRef.AssignerEquipementSacDos(depuisCurseur);
		_curseurMenu = surSac;
		return true;
	}

	private bool EchangerCurseurAvecEquipementCarnetSiValide()
	{
		if (_joueurRef == null) return false;
		if (!_curseurMenu.EstVide && _curseurMenu.ID != Joueur.IdObjetCarnetSavoir)
		{
			GD.Print("ZERO-K : Ce slot n'accepte que le carnet du savoir.");
			return false;
		}
		SlotInventaire surCarnet = _joueurRef.EquipementCarnet;
		SlotInventaire depuisCurseur = _curseurMenu;
		_joueurRef.AssignerEquipementCarnet(depuisCurseur);
		_curseurMenu = surCarnet;
		return true;
	}

	private void ResoudreCurseurAvantFermeture()
	{
		if (_joueurRef == null || _curseurMenu.EstVide) return;
		if (_joueurRef.MainGauche.EstVide)
		{
			_joueurRef.MainGauche = _curseurMenu;
			_curseurMenu = new SlotInventaire();
		}
		else if (_joueurRef.MainDroite.EstVide)
		{
			_joueurRef.MainDroite = _curseurMenu;
			_curseurMenu = new SlotInventaire();
		}
		else
		{
			bool place = false;
			if (_joueurRef.StockageCoffreOuvert && _joueurRef.CoffreOuvert != null && GodotObject.IsInstanceValid(_joueurRef.CoffreOuvert)
				&& !Joueur.EstObjetInterditDansCoffre(_curseurMenu))
			{
				for (int i = 0; i < 10; i++)
				{
					ref SlotInventaire sc = ref _joueurRef.RefSlotCoffreStockage(i);
					if (!sc.EstVide) continue;
					sc = _curseurMenu;
					place = true;
					break;
				}
			}
			var g = _joueurRef.ObtenirGrilleCraftAffichee();
			int maxI = _joueurRef.CraftGrille3x3AuTable ? 9 : 4;
			if (!place && g != null)
			{
				for (int i = 0; i < maxI && i < g.Length; i++)
				{
					if (!g[i].EstVide) continue;
					g[i] = _curseurMenu;
					place = true;
					break;
				}
			}
			if (place)
				_curseurMenu = new SlotInventaire();
			else
			{
				if (_joueurRef.MainGaucheEstActive)
					EchangerCurseurAvec(ref _joueurRef.MainGauche);
				else
					EchangerCurseurAvec(ref _joueurRef.MainDroite);
				// Garde l’ancien contenu de la main dans le curseur pour la prochaine ouverture.
			}
		}
		_joueurRef.RafraichirHUD();
	}

	private void AssurerPreviewsCraft()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreGrilleAssemblage();
		if (GrilleAssemblage == null) return;
		int nChild = GrilleAssemblage.GetChildCount();
		if (_vpCraft != null && _vpCraft.Length != nChild)
		{
			_vpCraft = null;
			_meshPreviewCraft = null;
			_lblCraft = null;
			_empreinteCraftLast = null;
			_clicsCraftConnectes = false;
			CallDeferred(nameof(ConnecterClicsInventaire));
		}
		if (_vpCraft != null && nChild > 0 && _vpCraft[0] != null && GodotObject.IsInstanceValid(_vpCraft[0]))
		{
			if (_empreinteCraftLast == null || _empreinteCraftLast.Length != nChild)
				_empreinteCraftLast = new ulong[nChild];
			return;
		}
		_meshPreviewCraft = new MeshInstance3D[nChild];
		_vpCraft = new SubViewportContainer[nChild];
		_lblCraft = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (GrilleAssemblage.GetChild(i) is not Panel p) continue;
			_meshPreviewCraft[i] = CreerViewportPreviewDansSlot(p, $"VpCraft{i}", out _vpCraft[i]);
			_lblCraft[i] = TrouverOuCreerLabel(p, " ");
		}
		_empreinteCraftLast = new ulong[nChild];
	}

	private void AssurerPreviewsCoffre()
	{
		if (Engine.IsEditorHint()) return;
		if (ObtenirGrilleCoffreBois() is not GridContainer grille || grille == null) return;
		int nChild = grille.GetChildCount();
		if (_vpCoffre != null && _vpCoffre.Length != nChild)
		{
			_vpCoffre = null;
			_meshPreviewCoffre = null;
			_lblCoffre = null;
			_empreinteCoffreLast = null;
			_clicsGrilleCoffreConnectes = false;
			CallDeferred(nameof(ConnecterClicsInventaire));
		}
		if (_vpCoffre != null && nChild > 0 && _vpCoffre[0] != null && GodotObject.IsInstanceValid(_vpCoffre[0]))
		{
			if (_empreinteCoffreLast == null || _empreinteCoffreLast.Length != nChild)
				_empreinteCoffreLast = new ulong[nChild];
			return;
		}
		_meshPreviewCoffre = new MeshInstance3D[nChild];
		_vpCoffre = new SubViewportContainer[nChild];
		_lblCoffre = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (grille.GetChild(i) is not Panel p) continue;
			_meshPreviewCoffre[i] = CreerViewportPreviewDansSlot(p, $"VpCoffre{i}", out _vpCoffre[i]);
			_lblCoffre[i] = TrouverOuCreerLabel(p, " ");
		}
		_empreinteCoffreLast = new ulong[nChild];
	}

	private void RafraichirCellulesCoffre()
	{
		if (_joueurRef == null || !_joueurRef.StockageCoffreOuvert) return;
		if (ObtenirGrilleCoffreBois() is not GridContainer grille || grille == null) return;
		AssurerPreviewsCoffre();
		for (int i = 0; i < 10 && i < grille.GetChildCount(); i++)
		{
			ref SlotInventaire s = ref _joueurRef.RefSlotCoffreStockage(i);
			bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
			bool vpOk = _vpCoffre != null && i < _vpCoffre.Length && _vpCoffre[i] != null && GodotObject.IsInstanceValid(_vpCoffre[i]);
			if (vpOk)
			{
				_vpCoffre[i].Visible = vis;
				if (_meshPreviewCoffre != null && i < _meshPreviewCoffre.Length && _meshPreviewCoffre[i] != null)
				{
					if (vis)
					{
						ulong em = EmpreinteSlotPourPreviewMenu(s);
						if (_empreinteCoffreLast == null || i >= _empreinteCoffreLast.Length || em != _empreinteCoffreLast[i])
						{
							_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewCoffre[i], s);
							if (_empreinteCoffreLast != null && i < _empreinteCoffreLast.Length)
								_empreinteCoffreLast[i] = em;
						}
					}
					else
					{
						if (_empreinteCoffreLast != null && i < _empreinteCoffreLast.Length)
							_empreinteCoffreLast[i] = 0UL;
						_meshPreviewCoffre[i].Mesh = null;
						_meshPreviewCoffre[i].MaterialOverride = null;
					}
				}
			}
			if (_lblCoffre != null && i < _lblCoffre.Length && _lblCoffre[i] != null)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(s);
				_lblCoffre[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
				_lblCoffre[i].Visible = !vis || !vpOk;
			}
			if (grille.GetChild(i) is Panel panelCase)
				RafraichirQuantiteSlot(panelCase, s);
		}
	}

	private void AssurerApercuFlottantCurseur()
	{
		if (Engine.IsEditorHint() || _conteneurFlottantCurseur != null) return;
		_conteneurFlottantCurseur = new Panel
		{
			Name = "FlottantCurseurInventaire",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(96, 118)
		};
		_conteneurFlottantCurseur.Size = _conteneurFlottantCurseur.CustomMinimumSize;
		_conteneurFlottantCurseur.ZIndex = 512;

		var vbox = new VBoxContainer
		{
			Name = "VBoxCurseur",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = vbox.OffsetTop = 4;
		vbox.OffsetRight = vbox.OffsetBottom = -4;
		_conteneurFlottantCurseur.AddChild(vbox);

		var cadreVp = new Panel
		{
			CustomMinimumSize = new Vector2(88, 88),
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vbox.AddChild(cadreVp);
		_meshCurseurSouris = CreerViewportPreviewDansSlot(cadreVp, "VpCurseurSouris", out _vpCurseurSouris);
		_lblCurseurQuantite = TrouverOuCreerLabelQuantite(cadreVp);

		_lblCurseurSouris = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_lblCurseurSouris.AddThemeFontSizeOverride("font_size", 11);
		_lblCurseurSouris.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		_lblCurseurSouris.AddThemeConstantOverride("outline_size", 2);
		vbox.AddChild(_lblCurseurSouris);

		AddChild(_conteneurFlottantCurseur);
		MoveChild(_conteneurFlottantCurseur, GetChildCount() - 1);
		_conteneurFlottantCurseur.Visible = false;
	}

	private void RafraichirAffichageCurseurSouris()
	{
		if (Engine.IsEditorHint() || _joueurRef == null) return;
		AssurerApercuFlottantCurseur();
		bool ecranAvecCurseurFlottant = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire
			|| _ecranBarreCourant == ModeEcranBarreMenu.Analyseur;
		bool montre = EstOuvert && !_curseurMenu.EstVide && ecranAvecCurseurFlottant;
		_conteneurFlottantCurseur.Visible = montre;
		if (!montre) return;

		bool vis = _joueurRef.InventaireSlotAunVisuel3D(_curseurMenu);
		bool vpOk = _vpCurseurSouris != null && GodotObject.IsInstanceValid(_vpCurseurSouris);
		if (vpOk)
		{
			_vpCurseurSouris.Visible = vis;
			if (vis && _meshCurseurSouris != null)
				_joueurRef.SynchroniserPreviewSlotMenu(_meshCurseurSouris, _curseurMenu);
			else if (_meshCurseurSouris != null)
			{
				_meshCurseurSouris.Mesh = null;
				_meshCurseurSouris.MaterialOverride = null;
			}
		}
		if (_lblCurseurSouris != null)
		{
			string nom = Atlas_Matiere.ObtenirNomObjet(_curseurMenu);
			_lblCurseurSouris.Text = string.IsNullOrEmpty(nom) ? " " : nom;
			_lblCurseurSouris.Visible = !vis || !vpOk;
		}
		if (_lblCurseurQuantite != null)
		{
			int q = Joueur.ObtenirQuantiteSlot(_curseurMenu);
			_lblCurseurQuantite.Visible = q > 1;
			_lblCurseurQuantite.Text = q > 1 ? $"x{q}" : "";
		}
		_conteneurFlottantCurseur.GlobalPosition = GetGlobalMousePosition() - _conteneurFlottantCurseur.Size * 0.5f;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || !EstOuvert)
			return;
		bool ecranInventaire = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire;
		bool ecranAnalyseur = _ecranBarreCourant == ModeEcranBarreMenu.Analyseur;
		if (!ecranInventaire && !ecranAnalyseur)
			return;

		if (ecranInventaire)
		{
			_accumulateurInfobulleInventaire += (float)delta;
			if (_accumulateurInfobulleInventaire >= IntervalleInfobulleInventaireSec)
			{
				_accumulateurInfobulleInventaire = 0f;
				MettreAJourInfobulleSourisInventaire();
			}
			else
				RepositionnerInfobulleSlotSourisSiVisible();

			_compteurFrameMenuProcess++;
			if ((_compteurFrameMenuProcess & 1) == 0)
				MettreAJourCameraApercuJoueurCorps((float)delta);
		}

		if (_conteneurFlottantCurseur != null && _conteneurFlottantCurseur.Visible)
		{
			Vector2 demi = _conteneurFlottantCurseur.Size * 0.5f;
			_conteneurFlottantCurseur.GlobalPosition = GetGlobalMousePosition() - demi;
		}
	}

	private void MettreAJourCameraApercuJoueurCorps(float delta)
	{
		if (_joueurRef == null || _cameraApercuJoueurCorps == null || !GodotObject.IsInstanceValid(_cameraApercuJoueurCorps))
			return;
		_ = delta;
		Vector3 cible = _joueurRef.GlobalPosition + new Vector3(0f, HauteurCibleCameraApercuJoueurCorps, 0f);
		// Place la caméra côté visage (et non derrière le dos).
		Vector3 devant = (-_joueurRef.GlobalTransform.Basis.Z).Normalized();
		Vector3 droite = _joueurRef.GlobalTransform.Basis.X.Normalized();
		Vector3 posCam = cible
			+ devant * DistanceCameraApercuJoueurCorps
			+ droite * DecalageLateralCameraApercuJoueurCorps
			+ new Vector3(0f, HauteurCameraApercuJoueurCorps, 0f);
		_cameraApercuJoueurCorps.GlobalPosition = posCam;
		_cameraApercuJoueurCorps.LookAt(cible, Vector3.Up);
	}

	private void AssurerInfobulleInventaire()
	{
		if (_panneauInfobulleSlot != null && GodotObject.IsInstanceValid(_panneauInfobulleSlot)) return;
		_panneauInfobulleSlot = new Panel
		{
			Name = "InfobulleNomSlot",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
			ZIndex = 640
		};
		_lblInfobulleSlot = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top
		};
		_lblInfobulleSlot.AddThemeFontSizeOverride("font_size", 13);
		_lblInfobulleSlot.AddThemeColorOverride("font_outline_color", Colors.Black);
		_lblInfobulleSlot.AddThemeConstantOverride("outline_size", 2);
		_lblInfobulleSlot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lblInfobulleSlot.OffsetLeft = 8;
		_lblInfobulleSlot.OffsetTop = 6;
		_lblInfobulleSlot.OffsetRight = -8;
		_lblInfobulleSlot.OffsetBottom = -6;
		_panneauInfobulleSlot.AddChild(_lblInfobulleSlot);
		AddChild(_panneauInfobulleSlot);
		MoveChild(_panneauInfobulleSlot, GetChildCount() - 1);
	}

	private bool TryObtenirSlotSousControleSouris(Control h, out SlotInventaire slot)
	{
		slot = default;
		if (h == null || _joueurRef == null) return false;
		ResoudreReferencesSlotsMains();
		ResoudreGrilleAssemblage();
		ResoudreSlotResultatCraft();

		if (MainGaucheSlot != null && GodotObject.IsInstanceValid(MainGaucheSlot)
			&& (h == MainGaucheSlot || MainGaucheSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.MainGauche;
			return true;
		}
		if (MainDroiteSlot != null && GodotObject.IsInstanceValid(MainDroiteSlot)
			&& (h == MainDroiteSlot || MainDroiteSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.MainDroite;
			return true;
		}
		if (EquipementCorpsSlot != null && GodotObject.IsInstanceValid(EquipementCorpsSlot)
			&& (h == EquipementCorpsSlot || EquipementCorpsSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.EquipementCeinture;
			return true;
		}
		if (EquipementSacSlot != null && GodotObject.IsInstanceValid(EquipementSacSlot)
			&& (h == EquipementSacSlot || EquipementSacSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.EquipementSacDos;
			return true;
		}
		if (CarnetSavoirSlot != null && GodotObject.IsInstanceValid(CarnetSavoirSlot)
			&& (h == CarnetSavoirSlot || CarnetSavoirSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.EquipementCarnet;
			return true;
		}
		if (SlotResultatCraft != null && GodotObject.IsInstanceValid(SlotResultatCraft)
			&& (h == SlotResultatCraft || SlotResultatCraft.IsAncestorOf(h)))
		{
			slot = _joueurRef.SlotResultatCraft;
			return true;
		}
		if (!_joueurRef.StockageCoffreOuvert && GrilleAssemblage != null && GodotObject.IsInstanceValid(GrilleAssemblage) && GrilleAssemblage.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == GrilleAssemblage && cur is Panel)
				{
					int idx = cur.GetIndex();
					if (!_joueurRef.CraftGrille3x3AuTable && idx >= 4)
						break;
					var g = _joueurRef.ObtenirGrilleCraftAffichee();
					if (g != null && idx >= 0 && idx < g.Length)
					{
						slot = g[idx];
						return true;
					}
					break;
				}
			}
		}
		if (ObtenirGrilleSac() is GridContainer grilleSac && GodotObject.IsInstanceValid(grilleSac) && grilleSac.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == grilleSac && cur is Panel)
				{
					int idx = cur.GetIndex();
					int capSac = Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos);
					if (!_joueurRef.ASacEquipe() || idx < 0 || idx >= capSac) break;
					slot = _joueurRef.RefSlotSac(idx);
					return true;
				}
			}
		}
		if (ObtenirGrilleCeintureStockage() is GridContainer grilleCeint && GodotObject.IsInstanceValid(grilleCeint) && grilleCeint.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == grilleCeint && cur is Panel)
				{
					int idx = cur.GetIndex();
					int capCeinture = Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture);
					if (!_joueurRef.ACeintureSacochesEquipe() || idx < 0 || idx >= capCeinture) break;
					slot = _joueurRef.RefSlotCeintureStockage(idx);
					return true;
				}
			}
		}
		if (ObtenirGrilleCoffreBois() is GridContainer grilleCoffre && GodotObject.IsInstanceValid(grilleCoffre) && grilleCoffre.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == grilleCoffre && cur is Panel)
				{
					int idx = cur.GetIndex();
					if (!_joueurRef.StockageCoffreOuvert || idx < 0 || idx > 9) break;
					slot = _joueurRef.RefSlotCoffreStockage(idx);
					return true;
				}
			}
		}
		return false;
	}

	private void MettreAJourInfobulleSourisInventaire()
	{
		if (Engine.IsEditorHint() || _joueurRef == null)
			return;
		AssurerInfobulleInventaire();
		var vp = GetViewport();
		Control h = vp?.GuiGetHoveredControl();
		if (h == null || !TryObtenirSlotSousControleSouris(h, out SlotInventaire sl) || sl.EstVide)
		{
			if (_panneauInfobulleSlot != null)
				_panneauInfobulleSlot.Visible = false;
			return;
		}
		string nom = Atlas_Matiere.ObtenirNomObjet(sl);
		if (string.IsNullOrEmpty(nom))
		{
			_panneauInfobulleSlot.Visible = false;
			return;
		}
		_lblInfobulleSlot.Text = nom;
		const float maxL = 300f;
		Vector2 ms = _lblInfobulleSlot.GetMinimumSize();
		ms.X = Mathf.Min(Mathf.Max(ms.X, 80f), maxL);
		ms.Y = Mathf.Max(ms.Y, 22f);
		_panneauInfobulleSlot.CustomMinimumSize = ms + new Vector2(16f, 12f);
		_panneauInfobulleSlot.Size = _panneauInfobulleSlot.CustomMinimumSize;
		Vector2 posSouris = GetGlobalMousePosition();
		Rect2 vr = GetViewport().GetVisibleRect();
		Vector2 p = posSouris + new Vector2(14f, 18f);
		if (p.X + _panneauInfobulleSlot.Size.X > vr.Position.X + vr.Size.X)
			p.X = posSouris.X - _panneauInfobulleSlot.Size.X - 10f;
		if (p.Y + _panneauInfobulleSlot.Size.Y > vr.Position.Y + vr.Size.Y)
			p.Y = posSouris.Y - _panneauInfobulleSlot.Size.Y - 10f;
		_panneauInfobulleSlot.GlobalPosition = p;
		_panneauInfobulleSlot.Visible = true;
	}

	private void RepositionnerInfobulleSlotSourisSiVisible()
	{
		if (_panneauInfobulleSlot == null || !_panneauInfobulleSlot.Visible)
			return;
		Vector2 posSouris = GetGlobalMousePosition();
		Rect2 vr = GetViewport().GetVisibleRect();
		Vector2 p = posSouris + new Vector2(14f, 18f);
		if (p.X + _panneauInfobulleSlot.Size.X > vr.Position.X + vr.Size.X)
			p.X = posSouris.X - _panneauInfobulleSlot.Size.X - 10f;
		if (p.Y + _panneauInfobulleSlot.Size.Y > vr.Position.Y + vr.Size.Y)
			p.Y = posSouris.Y - _panneauInfobulleSlot.Size.Y - 10f;
		_panneauInfobulleSlot.GlobalPosition = p;
	}

	private void RafraichirCellulesCraft()
	{
		if (_joueurRef == null || GrilleAssemblage == null) return;
		if (_joueurRef.StockageCoffreOuvert)
			RafraichirCellulesCoffre();
		else
		{
			AssurerPreviewsCraft();
			_joueurRef.VerifierRecettes();
			var gCraft = _joueurRef.ObtenirGrilleCraftAffichee();
			bool pitRoche = _joueurRef.StockageRackBatonsOuvert
				&& _joueurRef.RackBatonsOuvert != null
				&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
				&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
			int nActives = _joueurRef.CraftGrille3x3AuTable ? 9 : 4;
			for (int i = 0; i < 9; i++)
			{
				SlotInventaire s = (gCraft != null && i < nActives && i < gCraft.Length) ? gCraft[i] : default;
				bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
				bool vpOk = _vpCraft != null && i < _vpCraft.Length && _vpCraft[i] != null && GodotObject.IsInstanceValid(_vpCraft[i]);
				if (vpOk)
				{
					_vpCraft[i].Visible = vis;
					if (_meshPreviewCraft != null && i < _meshPreviewCraft.Length && _meshPreviewCraft[i] != null)
					{
						if (vis)
						{
							ulong em = EmpreinteSlotPourPreviewMenu(s);
							if (_empreinteCraftLast == null || i >= _empreinteCraftLast.Length || em != _empreinteCraftLast[i])
							{
								_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewCraft[i], s);
								if (_empreinteCraftLast != null && i < _empreinteCraftLast.Length)
									_empreinteCraftLast[i] = em;
							}
						}
						else
						{
							if (_empreinteCraftLast != null && i < _empreinteCraftLast.Length)
								_empreinteCraftLast[i] = 0UL;
							_meshPreviewCraft[i].Mesh = null;
							_meshPreviewCraft[i].MaterialOverride = null;
						}
					}
				}
				if (_lblCraft != null && i < _lblCraft.Length && _lblCraft[i] != null)
				{
					string nom = Atlas_Matiere.ObtenirNomObjet(s);
					if (pitRoche && s.EstVide)
					{
						if (i == 0) nom = "Combustible";
						else if (i == 1) nom = "Cuisson";
						else if (i == 2) nom = "Resultat";
					}
					_lblCraft[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
					_lblCraft[i].Visible = !vis || !vpOk;
				}
				if (GrilleAssemblage.GetChild(i) is Panel panelCase)
					RafraichirQuantiteSlot(panelCase, s);
			}
		}

		ResoudreSlotResultatCraft();
		if (SlotResultatCraft != null)
		{
			bool modeRack = _joueurRef.StockageRackBatonsOuvert;
			bool modeCoffre = _joueurRef.StockageCoffreOuvert;
			SlotResultatCraft.Visible = !modeRack && !modeCoffre;
			if (modeRack || modeCoffre)
			{
				_empreinteResultatCraftLast = 0UL;
				if (_meshPreviewResultatCraft != null)
				{
					_meshPreviewResultatCraft.Mesh = null;
					_meshPreviewResultatCraft.MaterialOverride = null;
				}
				if (_lblResultatCraft != null)
					_lblResultatCraft.Visible = false;
				return;
			}

			if (_vpResultatCraft == null && GodotObject.IsInstanceValid(SlotResultatCraft))
			{
				_meshPreviewResultatCraft = CreerViewportPreviewDansSlot(SlotResultatCraft, "VpResultatCraft", out _vpResultatCraft);
				_lblResultatCraft = TrouverOuCreerLabel(SlotResultatCraft, " ");
			}

			var sRes = _joueurRef.SlotResultatCraft;
			bool visRes = _joueurRef.InventaireSlotAunVisuel3D(sRes);
			bool vpResOk = _vpResultatCraft != null && GodotObject.IsInstanceValid(_vpResultatCraft);

			if (vpResOk)
			{
				_vpResultatCraft.Visible = visRes;
				if (visRes && _meshPreviewResultatCraft != null)
				{
					ulong emRes = EmpreinteSlotPourPreviewMenu(sRes);
					if (emRes != _empreinteResultatCraftLast)
					{
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewResultatCraft, sRes);
						_empreinteResultatCraftLast = emRes;
					}
				}
				else if (_meshPreviewResultatCraft != null)
				{
					_empreinteResultatCraftLast = 0UL;
					_meshPreviewResultatCraft.Mesh = null;
					_meshPreviewResultatCraft.MaterialOverride = null;
				}
			}
			if (_lblResultatCraft != null)
			{
				string nomRes = Atlas_Matiere.ObtenirNomObjet(sRes);
				_lblResultatCraft.Text = string.IsNullOrEmpty(nomRes) ? " " : nomRes;
				_lblResultatCraft.Visible = !visRes || !vpResOk;
			}
			RafraichirQuantiteSlot(SlotResultatCraft, sRes);
		}
	}

	private Label TrouverOuCreerLabel(Panel parent, string texteDefaut)
	{
		if (parent == null) return null;
		var lbl = parent.GetNodeOrNull<Label>("Label") ?? TrouverLabelEnfant(parent);
		if (lbl == null)
		{
			lbl = new Label
			{
				Name = "Label",
				Text = texteDefaut,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			lbl.AddThemeFontSizeOverride("font_size", 12);
			lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
			lbl.AddThemeConstantOverride("outline_size", 3);
			parent.AddChild(lbl);
		}
		// Stop par défaut : le label recouvre le Panel et bloquait GuiInput (craft + mains).
		lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
		return lbl;
	}

	private void MettreAJourEnteteModeRack()
	{
		if (_joueurRef == null) return;
		ResoudreGrilleAssemblage();
		if (GrilleAssemblage?.GetParent() is not Panel cadre) return;

		if (_lblModeRack == null || !GodotObject.IsInstanceValid(_lblModeRack))
		{
			_lblModeRack = cadre.GetNodeOrNull<Label>("LabelModeRack");
			if (_lblModeRack == null)
			{
				_lblModeRack = new Label
				{
					Name = "LabelModeRack",
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				_lblModeRack.SetAnchorsPreset(Control.LayoutPreset.TopWide);
				_lblModeRack.OffsetLeft = 0;
				_lblModeRack.OffsetRight = 0;
				_lblModeRack.OffsetTop = -28;
				_lblModeRack.OffsetBottom = -6;
				_lblModeRack.AddThemeFontSizeOverride("font_size", 14);
				_lblModeRack.AddThemeColorOverride("font_outline_color", Colors.Black);
				_lblModeRack.AddThemeConstantOverride("outline_size", 3);
				cadre.AddChild(_lblModeRack);
			}
		}

		if (_joueurRef.StockageCoffreOuvert)
		{
			_lblModeRack.Text = "Stockage Coffre en bois  [10 emplacements]";
			_lblModeRack.Visible = true;
		}
		else if (_joueurRef.StockageRackBatonsOuvert)
		{
			int total = _joueurRef.CompterQuantiteRackOuvert();
			int cap = _joueurRef.ObtenirCapaciteRackOuvert();
			bool rackBuches = _joueurRef.RackBatonsOuvert != null
				&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
				&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBuches;
            bool pitRoche = _joueurRef.RackBatonsOuvert != null
                && GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
                && _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
            _lblModeRack.Text = pitRoche
                ? $"Pit roche | Combustible [{total}/{cap}] | Cuisson: slot 2 | Resultat: slot 3"
                : (rackBuches
                    ? $"Stockage Rack a buches  [{total}/{cap}]"
                    : $"Stockage Rack a batons  [{total}/{cap}]");
			_lblModeRack.Visible = true;
		}
		else
		{
			_lblModeRack.Visible = false;
		}
	}

	private Label TrouverOuCreerLabelQuantite(Panel parent)
	{
		if (parent == null) return null;
		var lbl = parent.GetNodeOrNull<Label>("QtyLabel");
		if (lbl == null)
		{
			lbl = new Label
			{
				Name = "QtyLabel",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Visible = false
			};
			lbl.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			lbl.OffsetLeft = -52f;
			lbl.OffsetTop = 2f;
			lbl.OffsetRight = -4f;
			lbl.OffsetBottom = 18f;
			lbl.AddThemeFontSizeOverride("font_size", 12);
			lbl.AddThemeColorOverride("font_color", Colors.White);
			lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
			lbl.AddThemeConstantOverride("outline_size", 2);
			lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
			parent.AddChild(lbl);
		}
		return lbl;
	}

	private void RafraichirQuantiteSlot(Panel panel, SlotInventaire slot)
	{
		if (panel == null) return;
		var lbl = TrouverOuCreerLabelQuantite(panel);
		if (lbl == null) return;
		int q = Joueur.ObtenirQuantiteSlot(slot);
		lbl.Visible = !slot.EstVide && q > 1;
		lbl.Text = lbl.Visible ? $"x{q}" : "";
	}

	private Label TrouverLabelEnfant(Node parent)
	{
		if (parent == null) return null;
		foreach (Node enfant in parent.GetChildren())
		{
			if (enfant is Label lbl) return lbl;
		}
		return null;
	}

	private void DesactiverFocusParasite(Node parent)
	{
		if (parent is Control c)
			c.FocusMode = Control.FocusModeEnum.None;
		foreach (Node enfant in parent.GetChildren())
			DesactiverFocusParasite(enfant);
	}

	/// <summary>Premier onglet = inventaire ; dernier = écran Sauvegarder / Quitter (2 boutons). Onglets intermédiaires masqués.</summary>
	private void ConfigurerBarreOngletsJeu()
	{
		if (Engine.IsEditorHint() || _barreOngletsJeuConfiguree) return;
		var barre = GetNodeOrNull<HBoxContainer>(CheminBarreOnglets) ?? FindChild("BarreOnglets", true, false) as HBoxContainer;
		if (barre == null) return;
		_barreOngletsJeuConfiguree = true;
		foreach (Node enfant in barre.GetChildren())
		{
			if (enfant is not Panel pan) continue;
			string nom = pan.Name;
			if (nom == "Onglet0")
			{
				_ongletInventaireBarre = pan;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lInv)
				{
					lInv.Text = "Inventaire";
					lInv.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletInventaireBarre;
			}
			else if (nom == "Onglet1")
			{
				_ongletFutureStateBarre = pan;
				pan.Visible = true;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lFuture)
				{
					lFuture.Text = "Future States";
					lFuture.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletFutureStateBarre;
			}
			else if (nom == "Onglet2")
			{
				_ongletMetierBarre = pan;
				pan.Visible = true;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lMetier)
				{
					lMetier.Text = "Metiers";
					lMetier.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletMetierBarre;
			}
			else if (nom == "Onglet3")
			{
				_ongletAnalyseurBarre = pan;
				pan.Visible = true;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lAna)
				{
					lAna.Text = "Analyseur";
					lAna.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletAnalyseurBarre;
			}
			else if (nom == "Onglet4")
			{
				_ongletCreatifBarre = pan;
				pan.Visible = _joueurRef != null && _joueurRef.ModeCreatifActif;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lCreatif)
				{
					lCreatif.Text = "Creatif/Admin";
					lCreatif.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletCreatifAdminBarre;
			}
			else if (nom == "Onglet11")
			{
				_ongletQuitterBarre = pan;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lQuit)
				{
					lQuit.Text = "Sauvegarder / Quitter";
					lQuit.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletQuitterJeuBarre;
			}
			else if (nom.ToString().StartsWith("Onglet", StringComparison.Ordinal))
				pan.Visible = false;
		}
		AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
	}

	private Gestionnaire_Monde ObtenirGestionnaireMonde()
	{
		if (_joueurRef == null) return null;
		Node parent = _joueurRef.GetParent();
		return parent?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
	}

	private void AssurerPanneauSauvegarderQuitter()
	{
		if (Engine.IsEditorHint()) return;
		var vbox = GetNodeOrNull<VBoxContainer>(CheminVBoxPrincipal) ?? FindChild("VBoxPrincipal", true, false) as VBoxContainer;
		if (vbox == null) return;
		_corpsHBoxRef ??= GetNodeOrNull<HBoxContainer>(CheminCorpsHBox) ?? vbox.GetNodeOrNull<HBoxContainer>("CorpsHBox");
		if (_panneauSauvegarderQuitter != null) return;

		_panneauSauvegarderQuitter = new Panel
		{
			Name = "PanneauSauvegarderQuitter",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_panneauSauvegarderQuitter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_panneauSauvegarderQuitter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centre.OffsetLeft = centre.OffsetTop = 8;
		centre.OffsetRight = centre.OffsetBottom = -8;
		_panneauSauvegarderQuitter.AddChild(centre);

		var col = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		col.AddThemeConstantOverride("separation", 16);
		centre.AddChild(col);

		var titre = new Label
		{
			Text = "Sauvegarder ou quitter",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		titre.AddThemeFontSizeOverride("font_size", 18);
		col.AddChild(titre);

		var btnSauve = new Button { Text = "Sauvegarder", CustomMinimumSize = new Vector2(220, 40) };
		btnSauve.Pressed += () => ObtenirGestionnaireMonde()?.SauvegarderManuelDepuisMenu();
		col.AddChild(btnSauve);

		var btnQuit = new Button { Text = "Quitter le jeu", CustomMinimumSize = new Vector2(220, 40) };
		btnQuit.Pressed += () =>
		{
			ObtenirGestionnaireMonde()?.SauvegarderManuelDepuisMenu();
			GetTree().Quit();
		};
		col.AddChild(btnQuit);

		vbox.AddChild(_panneauSauvegarderQuitter);
	}

	private void AssurerPanneauAnalyseur()
	{
		if (Engine.IsEditorHint()) return;
		var vbox = GetNodeOrNull<VBoxContainer>(CheminVBoxPrincipal) ?? FindChild("VBoxPrincipal", true, false) as VBoxContainer;
		if (vbox == null) return;
		if (_panneauAnalyseur != null) return;

		_panneauAnalyseur = new Panel
		{
			Name = "PanneauAnalyseurManuel",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_panneauAnalyseur.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_panneauAnalyseur.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		_panneauAnalyseur.CustomMinimumSize = new Vector2(0f, 220f);

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centre.OffsetLeft = centre.OffsetTop = 8;
		centre.OffsetRight = centre.OffsetBottom = -8;
		_panneauAnalyseur.AddChild(centre);

		var col = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(560f, 360f)
		};
		col.AddThemeConstantOverride("separation", 12);
		centre.AddChild(col);

		var titre = new Label
		{
			Text = "Analyseur manuel",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		titre.AddThemeFontSizeOverride("font_size", 20);
		col.AddChild(titre);

		var aide = new Label
		{
			Text = "Dépose des objets. L'analyse consomme ce que tu as mis.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		aide.AddThemeFontSizeOverride("font_size", 13);
		col.AddChild(aide);

		_grilleAnalyseur = new GridContainer
		{
			Name = "GrilleAnalyseur",
			Columns = Joueur.CapaciteAnalyseurManuel,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_grilleAnalyseur.AddThemeConstantOverride("h_separation", 12);
		_grilleAnalyseur.AddThemeConstantOverride("v_separation", 12);
		col.AddChild(_grilleAnalyseur);

		_slotsAnalyseur = new Panel[Joueur.CapaciteAnalyseurManuel];
		for (int i = 0; i < Joueur.CapaciteAnalyseurManuel; i++)
		{
			var slot = new Panel
			{
				Name = $"AnalyseurSlot{i}",
				CustomMinimumSize = new Vector2(96f, 96f),
				MouseFilter = Control.MouseFilterEnum.Stop
			};
			_grilleAnalyseur.AddChild(slot);
			_slotsAnalyseur[i] = slot;
		}

		_btnAnalyser = new Button
		{
			Name = "BtnAnalyser",
			Text = "Analyser",
			CustomMinimumSize = new Vector2(220f, 40f)
		};
		_btnAnalyser.Pressed += () =>
		{
			if (_joueurRef == null) return;
			_joueurRef.EssayerAnalyserCrafts(out string msgAnalyse);
			RafraichirLabelChanceAnalyseur();
			if (_lblAnalyseurMessage != null)
				_lblAnalyseurMessage.Text = ConstruireTexteEtatAnalyseur(string.IsNullOrEmpty(msgAnalyse) ? _joueurRef.MessageAnalyseurManuel : msgAnalyse);
			GetViewport()?.SetInputAsHandled();
			_joueurRef.RafraichirHUD();
			RafraichirMenu();
		};
		col.AddChild(_btnAnalyser);

		_lblAnalyseurChance = new Label
		{
			Name = "AnalyseurChance",
			Text = "Chance de réussite: --,--%",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblAnalyseurChance.AddThemeFontSizeOverride("font_size", 14);
		_lblAnalyseurChance.AddThemeColorOverride("font_color", new Color(0.90f, 0.95f, 1.00f));
		col.AddChild(_lblAnalyseurChance);

		_lblAnalyseurMessage = new Label
		{
			Name = "AnalyseurMessage",
			Text = "Dépose des objets puis clique sur Analyser.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(560f, 80f)
		};
		_lblAnalyseurMessage.AddThemeFontSizeOverride("font_size", 13);
		_lblAnalyseurMessage.AddThemeColorOverride("font_outline_color", Colors.Black);
		_lblAnalyseurMessage.AddThemeConstantOverride("outline_size", 2);
		col.AddChild(_lblAnalyseurMessage);

		vbox.AddChild(_panneauAnalyseur);
	}

	private void AssurerPreviewsAnalyseur()
	{
		if (Engine.IsEditorHint()) return;
		if (_slotsAnalyseur == null || _slotsAnalyseur.Length == 0)
			return;
		if (_vpAnalyseur != null && _vpAnalyseur.Length == _slotsAnalyseur.Length && _vpAnalyseur[0] != null && GodotObject.IsInstanceValid(_vpAnalyseur[0]))
		{
			if (_empreinteAnalyseurLast == null || _empreinteAnalyseurLast.Length != _slotsAnalyseur.Length)
				_empreinteAnalyseurLast = new ulong[_slotsAnalyseur.Length];
			return;
		}
		_vpAnalyseur = new SubViewportContainer[_slotsAnalyseur.Length];
		_meshPreviewAnalyseur = new MeshInstance3D[_slotsAnalyseur.Length];
		_lblAnalyseur = new Label[_slotsAnalyseur.Length];
		for (int i = 0; i < _slotsAnalyseur.Length; i++)
		{
			if (_slotsAnalyseur[i] == null) continue;
			_meshPreviewAnalyseur[i] = CreerViewportPreviewDansSlot(_slotsAnalyseur[i], $"VpAnalyseur{i}", out _vpAnalyseur[i]);
			_lblAnalyseur[i] = TrouverOuCreerLabel(_slotsAnalyseur[i], " ");
		}
		_empreinteAnalyseurLast = new ulong[_slotsAnalyseur.Length];
	}

	private void RafraichirPanneauAnalyseur()
	{
		if (_joueurRef == null) return;
		AssurerPanneauAnalyseur();
		AssurerPreviewsAnalyseur();
		RafraichirLabelChanceAnalyseur();
		if (_lblAnalyseurMessage != null)
			_lblAnalyseurMessage.Text = ConstruireTexteEtatAnalyseur(string.IsNullOrEmpty(_joueurRef.MessageAnalyseurManuel)
				? "Dépose des objets puis clique sur Analyser."
				: _joueurRef.MessageAnalyseurManuel);
		if (_slotsAnalyseur == null) return;
		for (int i = 0; i < _slotsAnalyseur.Length; i++)
		{
			Panel panel = _slotsAnalyseur[i];
			if (panel == null) continue;
			SlotInventaire s = i < _joueurRef.GrilleAnalyseurManuel.Length ? _joueurRef.GrilleAnalyseurManuel[i] : default;
			bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
			bool vpOk = _vpAnalyseur != null && i < _vpAnalyseur.Length && _vpAnalyseur[i] != null && GodotObject.IsInstanceValid(_vpAnalyseur[i]);
			if (vpOk)
			{
				_vpAnalyseur[i].Visible = vis;
				if (vis && _meshPreviewAnalyseur != null && i < _meshPreviewAnalyseur.Length && _meshPreviewAnalyseur[i] != null)
				{
					ulong em = EmpreinteSlotPourPreviewMenu(s);
					if (_empreinteAnalyseurLast == null || i >= _empreinteAnalyseurLast.Length || em != _empreinteAnalyseurLast[i])
					{
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewAnalyseur[i], s);
						if (_empreinteAnalyseurLast != null && i < _empreinteAnalyseurLast.Length)
							_empreinteAnalyseurLast[i] = em;
					}
				}
				else if (_meshPreviewAnalyseur != null && i < _meshPreviewAnalyseur.Length && _meshPreviewAnalyseur[i] != null)
				{
					_meshPreviewAnalyseur[i].Mesh = null;
					_meshPreviewAnalyseur[i].MaterialOverride = null;
					if (_empreinteAnalyseurLast != null && i < _empreinteAnalyseurLast.Length)
						_empreinteAnalyseurLast[i] = 0UL;
				}
			}
			if (_lblAnalyseur != null && i < _lblAnalyseur.Length && _lblAnalyseur[i] != null)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(s);
				_lblAnalyseur[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
				_lblAnalyseur[i].Visible = !vis || !vpOk;
			}
			RafraichirQuantiteSlot(panel, s);
		}
	}

	private string ConstruireTexteEtatAnalyseur(string messagePrincipal)
	{
		if (_joueurRef == null)
			return messagePrincipal ?? "";
		string regle = "Règle: base 50% + 0,01% par point d'Intelligence autour de 10.";
		if (string.IsNullOrWhiteSpace(messagePrincipal))
			return regle;
		return $"{regle}\n\n{messagePrincipal}";
	}

	private void RafraichirLabelChanceAnalyseur()
	{
		if (_lblAnalyseurChance == null || _joueurRef == null)
			return;
		float chance = Mathf.Clamp(_joueurRef.ObtenirChanceReussiteAnalyseManuelle() * 100f, 0f, 100f);
		_lblAnalyseurChance.Text = $"Chance de réussite: {chance:F2}%";
	}

	private void RestituerGrilleAnalyseurAvantFermeture()
	{
		if (_joueurRef == null || _joueurRef.GrilleAnalyseurManuel == null) return;
		for (int i = 0; i < _joueurRef.GrilleAnalyseurManuel.Length; i++)
		{
			SlotInventaire s = _joueurRef.GrilleAnalyseurManuel[i];
			if (s.EstVide) continue;
			if (!_joueurRef.EssayerRangerSlotInventaireOuStockage(ref s) && !s.EstVide)
				_joueurRef.DeposerSlotAuSolDepuisMenu(s);
			_joueurRef.GrilleAnalyseurManuel[i] = new SlotInventaire();
		}
	}

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
			RafraichirPanneauCreatifAdmin();
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
		if (_catalogueCreatifAdmin.Count > 0) return;
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

		int[] idsConsommables = {
			1,2,3,4,5,6,7,8,9,
			Joueur.IdObjetSteakCru, Joueur.IdObjetSteakCuit, Joueur.IdObjetBaie
		};
		foreach (int id in idsConsommables)
			Ajouter(new SlotInventaire { ID = id, Quantite = 1 }, CategorieCreatifAdmin.Consommables);

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
			Ajouter(new SlotInventaire { ID = 30, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Bois, $"Essence: {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = 32, IndexBotanique = essence, IndexChimique = 0, Quantite = 1 }, CategorieCreatifAdmin.Bois, $"Essence: {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = BlocChutant.ID_BRANCHE, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Bois, $"Essence: {NomEssence(essence)}");
		}

		// Structures bois/mixes.
		foreach (byte essence in essencesBois)
		{
			Ajouter(new SlotInventaire { ID = 200, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Atelier {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetPitFeu, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Essence: {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetPitFeuRoche, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Essence: {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetCoffreBoisTier0, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Coffre {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetRackBatons, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Rack bâtons {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetRackBuches, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Rack bûches {NomEssence(essence)}");
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetFondationBois, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Structures, $"Fondation bois {NomEssence(essence)}");
		}
		foreach (byte tagLig in tagsLigatures)
		{
			Ajouter(new SlotInventaire { ID = Joueur.IdObjetCoffreBoisTier0, IndexBotanique = tagLig, Quantite = 1 },
				CategorieCreatifAdmin.Structures, $"Coffre ligature: {NomLigature(tagLig)}");
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
		int[] outilsPierre = { 105, 106, Joueur.IdObjetPellePierreTier0, Joueur.IdObjetPiochePierreTier0, Joueur.IdObjetLancePierreTier0, Joueur.IdObjetFauxPierreTier0 };
		foreach (int idOutil in outilsPierre)
			for (int chim = 0; chim < ItemPhysique.TableGeologique.Length; chim++)
				foreach (byte essence in essencesBois)
					Ajouter(new SlotInventaire { ID = idOutil, IndexChimique = chim, IndexBotanique = essence, Quantite = 1 }, CategorieCreatifAdmin.Outils,
						$"{ItemPhysique.TableGeologique[chim].Nom} / {NomEssence(essence)}");

		// Objets admin orientés test/debug.
		Ajouter(new SlotInventaire { ID = Joueur.IdObjetAllumeFeu, IndexChimique = 10, Quantite = 1 }, CategorieCreatifAdmin.Admin, "Marcassite");
		Ajouter(new SlotInventaire { ID = Joueur.IdObjetAllumeFeu, IndexChimique = 11, Quantite = 1 }, CategorieCreatifAdmin.Admin, "Pyrite");

		// Nettoyage final des entrées dont le nom ne se résout pas proprement.
		_catalogueCreatifAdmin.RemoveAll(e => string.IsNullOrWhiteSpace(e.Nom) || !EstEssenceBoisValide(e.Slot.IndexBotanique) && e.Slot.IndexBotanique <= 4);
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

	private void AppliquerEcranBarre(ModeEcranBarreMenu mode)
	{
		if (Engine.IsEditorHint()) return;
		if (mode == ModeEcranBarreMenu.CreatifAdmin && (_joueurRef == null || !_joueurRef.ModeCreatifActif))
			mode = ModeEcranBarreMenu.Inventaire;
		_ecranBarreCourant = mode;
		AssurerPanneauSauvegarderQuitter();
		AssurerPanneauAnalyseur();
		AssurerPanneauCreatifAdmin();
		if (_corpsHBoxRef != null)
			_corpsHBoxRef.Visible = mode == ModeEcranBarreMenu.Inventaire || mode == ModeEcranBarreMenu.Analyseur;
		if (_panneauAnalyseur != null)
			_panneauAnalyseur.Visible = mode == ModeEcranBarreMenu.Analyseur;
		if (_panneauCreatifAdmin != null)
			_panneauCreatifAdmin.Visible = mode == ModeEcranBarreMenu.CreatifAdmin;
		if (_panneauSauvegarderQuitter != null)
			_panneauSauvegarderQuitter.Visible = mode == ModeEcranBarreMenu.SauvegarderQuitter;
		MettreAJourStyleOngletsBarre();
		if (mode == ModeEcranBarreMenu.Analyseur)
			RafraichirPanneauAnalyseur();
		else if (mode == ModeEcranBarreMenu.CreatifAdmin)
			RafraichirPanneauCreatifAdmin();
		RafraichirAffichageCurseurSouris();
	}

	private void MettreAJourStyleOngletsBarre()
	{
		Color actif = Colors.White;
		Color inactif = new(0.62f, 0.62f, 0.62f);
		if (_ongletInventaireBarre != null)
			_ongletInventaireBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire ? actif : inactif;
		if (_ongletFutureStateBarre != null)
			_ongletFutureStateBarre.Modulate = inactif;
		if (_ongletMetierBarre != null)
			_ongletMetierBarre.Modulate = inactif;
		if (_ongletAnalyseurBarre != null)
			_ongletAnalyseurBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.Analyseur ? actif : inactif;
		if (_ongletCreatifBarre != null)
		{
			_ongletCreatifBarre.Visible = _joueurRef != null && _joueurRef.ModeCreatifActif;
			_ongletCreatifBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.CreatifAdmin ? actif : inactif;
		}
		if (_ongletQuitterBarre != null)
			_ongletQuitterBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.SauvegarderQuitter ? actif : inactif;
	}

	private void _OnOngletInventaireBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
	}

	private void _OnOngletQuitterJeuBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.SauvegarderQuitter);
	}

	private void _OnOngletFutureStateBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		_joueurRef?.OuvrirFutureStateDepuisMenu();
	}

	private void _OnOngletMetierBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		_joueurRef?.OuvrirMetiersDepuisMenu();
	}

	private void _OnOngletAnalyseurBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.Analyseur);
	}

	private void _OnOngletCreatifAdminBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.CreatifAdmin);
	}

	public void ForcerOngletInventaire()
	{
		AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
	}

	/// <summary>Accroche le bloc faim / énergie du joueur sous les barres de PV (colonne gauche du menu inventaire Q).</summary>
	public void AttacherHudFaimEnergieJoueur(Control widget)
	{
		if (widget == null)
			return;
		AssurerPanneauSanteCorps();
		if (_boiteFaimEnergieExterne == null || !GodotObject.IsInstanceValid(_boiteFaimEnergieExterne))
			return;
		Node parent = widget.GetParent();
		if (parent != null)
			parent.RemoveChild(widget);
		_boiteFaimEnergieExterne.AddChild(widget);
	}

	public override void _Ready()
	{
		bool editeur = Engine.IsEditorHint();
		if (!editeur)
		{
			Visible = false;
			EstOuvert = false;
		}

		RemplirParentOuViewport();
		AppliquerAncresContenu();
		CallDeferred(nameof(RemplirParentOuViewport));
		CallDeferred(nameof(AppliquerAncresContenu));

		if (!editeur)
		{
			var vp = GetViewport();
			if (vp != null && !_abonneViewport)
			{
				vp.SizeChanged += OnViewportSizeChangedMenu;
				_abonneViewport = true;
			}
		}

		// Tailles : celles de MenuAnatomie.tscn (éviter d’écraser → décalage / bande mince).

		if (GrilleAssemblage != null)
			GrilleAssemblage.Columns = 3;

		// 3. SÉCURISATION DES AUTRES SLOTS (nom dans la scène + Export)
		if (FindChild("InterfaceFutureSlot", true, false) is Control f1) f1.CustomMinimumSize = new Vector2(96, 96);
		if (FindChild("SacSlot", true, false) is Control f2)
		{
			f2.CustomMinimumSize = new Vector2(96, 96);
			f2.Hide();
		}
		if (SacSlot != null) SacSlot.Hide();

		if (FindChild("EquipementCorpsSlot", true, false) is Control f3) f3.CustomMinimumSize = new Vector2(96, 96);

		DesactiverFocusParasite(this);

		// VERROUILLAGE DE L'ÉTAT ZÉRO (Le joueur naît nu)
		if (SacSlot != null) SacSlot.Hide();

		// Ce slot est réservé pour le futur, on le tue visuellement pour l'instant
		Control slotJaune = FindChild("InterfaceFutureSlot", true, false) as Control;
		if (slotJaune != null) slotJaune.Hide();

		// Les cases grises « Sac » sont GrilleSac — masquées en jeu + pas d’expansion verticale (les mains remontent sous le craft).
		if (!editeur && ObtenirGrilleSac() is GridContainer grilleSacInit)
		{
			grilleSacInit.Hide();
			grilleSacInit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleSacInit.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			foreach (Node enfant in grilleSacInit.GetChildren())
			{
				if (enfant is Control c) c.Visible = false;
			}
		}
		if (!editeur && ObtenirGrilleCeintureStockage() is GridContainer grilleCeintInit)
		{
			grilleCeintInit.Hide();
			grilleCeintInit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleCeintInit.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			foreach (Node enfant in grilleCeintInit.GetChildren())
			{
				if (enfant is Control c) c.Visible = false;
			}
		}

		if (!editeur)
			CallDeferred(nameof(ConfigurerBarreOngletsJeu));
	}

	public override void _ExitTree()
	{
		if (_abonneViewport)
		{
			var vp = GetViewport();
			if (vp != null) vp.SizeChanged -= OnViewportSizeChangedMenu;
			_abonneViewport = false;
		}
		base._ExitTree();
	}

	private void OnViewportSizeChangedMenu()
	{
		RemplirParentOuViewport();
		AppliquerAncresContenu();
	}

	/// <summary>
	/// Sous CanvasLayer le parent a une taille explicite : on copie ce rectangle. Sinon visible rect du viewport.
	/// TopLeft + Size évite le conflit ancres FullRect avec un parent encore à 0×0 au premier frame.
	/// </summary>
	private void RemplirParentOuViewport()
	{
		if (!IsInstanceValid(this)) return;

		Vector2 refMin = CustomMinimumSize;
		if (refMin.X < 64f || refMin.Y < 64f)
			refMin = TailleReferenceEditeur;

		Vector2 taille;
		if (GetParent() is Control parent && parent.Size.X > 2f && parent.Size.Y > 2f)
			taille = parent.Size;
		else
			taille = GetViewport().GetVisibleRect().Size;

		if (taille.X < 2f || taille.Y < 2f)
		{
			if (!Engine.IsEditorHint())
				return;
			taille = refMin;
		}
		else if (Engine.IsEditorHint())
		{
			taille = new Vector2(
				Mathf.Max(taille.X, refMin.X),
				Mathf.Max(taille.Y, refMin.Y));
		}

		SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		Position = Vector2.Zero;
		Size = taille;
	}

	private void AppliquerAncresContenu()
	{
		if (FondSombre != null)
		{
			FondSombre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			FondSombre.OffsetLeft = FondSombre.OffsetTop = FondSombre.OffsetRight = FondSombre.OffsetBottom = 0;
		}

		if (GetNodeOrNull<MarginContainer>("MarginPrincipal") is MarginContainer marge)
		{
			marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			marge.OffsetLeft = marge.OffsetTop = marge.OffsetRight = marge.OffsetBottom = 0;
		}

		RenforcerDispositionCorps();
	}

	/// <summary>
	/// Le CorpsHBox doit prendre toute la hauteur sous la barre d’onglets ; sinon les 3 colonnes restent en bande mince en haut.
	/// </summary>
	private void RenforcerDispositionCorps()
	{
		if (GetNodeOrNull<VBoxContainer>("MarginPrincipal/VBoxPrincipal") is VBoxContainer vbox)
		{
			vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		}
		const string chemin = "MarginPrincipal/VBoxPrincipal/CorpsHBox";
		if (GetNodeOrNull<HBoxContainer>(chemin) is HBoxContainer corps)
		{
			corps.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			corps.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		}
		if (GetNodeOrNull<GridContainer>(chemin + "/GrilleEquipCorps") is GridContainer grille)
			grille.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		if (GetNodeOrNull<Control>(chemin + "/VueJoueurPanel") is Control vue)
			vue.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		if (GetNodeOrNull<VBoxContainer>(chemin + "/ZoneDroite") is VBoxContainer zone)
		{
			zone.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			zone.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		}
		if (ObtenirGrilleSac() is GridContainer sac)
		{
			sac.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			// Le sac ne doit jamais pousser les slots des mains hors écran.
			sac.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		}
		if (ObtenirGrilleCeintureStockage() is GridContainer ceintSt)
		{
			ceintSt.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			ceintSt.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		}
		if (GetNodeOrNull<HBoxContainer>(CheminLigneMainsCeinture) is HBoxContainer ligneMains)
		{
			ligneMains.Visible = true;
			ligneMains.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			ligneMains.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		}
	}

	public void BasculerVisibilite()
	{
		EstOuvert = !EstOuvert;
		Visible = EstOuvert;

		if (!EstOuvert)
		{
			ResoudreCurseurAvantFermeture();
			RestituerGrilleAnalyseurAvantFermeture();
			if (_joueurRef != null)
			{
				_joueurRef.CraftGrille3x3AuTable = false;
				_joueurRef.AtelierPlanTravailOuvert = null;
				_joueurRef.StockageRackBatonsOuvert = false;
				_joueurRef.RackBatonsOuvert = null;
				_joueurRef.StockageCoffreOuvert = false;
				_joueurRef.CoffreOuvert = null;
			}
		}

		if (!EstOuvert && _panneauInfobulleSlot != null)
			_panneauInfobulleSlot.Visible = false;

		Input.MouseMode = EstOuvert ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		if (!Engine.IsEditorHint())
			SetProcess(EstOuvert);

		if (EstOuvert)
		{
			CallDeferred(nameof(RemplirParentOuViewport));
			CallDeferred(nameof(AppliquerAncresContenu));
			CallDeferred(nameof(ConnecterClicsInventaire));
			if (!Engine.IsEditorHint())
				AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
			RafraichirMenu();
			CallDeferred(nameof(RafraichirMenu));
		}
	}

	// Cette fonction lit les données du Joueur et les affiche dans l'UI
	public void RafraichirMenu()
	{
		if (_joueurRef == null) return;
		MettreAJourVisibiliteLigneCraftVersusCoffre();
		RafraichirPanneauSanteCorps();
		ResoudreReferencesSlotsMains();
		if (_lblMainGauche == null) _lblMainGauche = TrouverOuCreerLabel(MainGaucheSlot, "Main G\n[Vide]");
		if (_lblMainDroite == null) _lblMainDroite = TrouverOuCreerLabel(MainDroiteSlot, "Main D\n[Vide]");
		AssurerPreviews3DMains();

		bool visG = _joueurRef.InventaireSlotAunVisuel3D(_joueurRef.MainGauche);
		bool visD = _joueurRef.InventaireSlotAunVisuel3D(_joueurRef.MainDroite);
		bool previewGOk = _vpMenuGauche != null && GodotObject.IsInstanceValid(_vpMenuGauche);
		bool previewDOk = _vpMenuDroite != null && GodotObject.IsInstanceValid(_vpMenuDroite);

		if (previewGOk)
		{
			_vpMenuGauche.Visible = visG;
			if (_meshPreviewMenuG != null)
			{
				ulong emG = EmpreinteSlotPourPreviewMenu(_joueurRef.MainGauche);
				if (emG != _empreinteMainGLast)
				{
					_empreinteMainGLast = emG;
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuG, _joueurRef.MainGauche);
				}
			}
		}
		if (_lblMainGauche != null)
		{
			bool montrerTexteG = !visG || !previewGOk;
			_lblMainGauche.Visible = montrerTexteG;
			string nomG = Atlas_Matiere.ObtenirNomObjet(_joueurRef.MainGauche);
			_lblMainGauche.Text = string.IsNullOrEmpty(nomG) ? "Main G\n[Vide]" : $"Main G\n[{nomG}]";
		}
		RafraichirQuantiteSlot(MainGaucheSlot, _joueurRef.MainGauche);
		AppliquerBordureActive(MainGaucheSlot, _joueurRef.MainGaucheEstActive);

		if (previewDOk)
		{
			_vpMenuDroite.Visible = visD;
			if (_meshPreviewMenuD != null)
			{
				ulong emD = EmpreinteSlotPourPreviewMenu(_joueurRef.MainDroite);
				if (emD != _empreinteMainDLast)
				{
					_empreinteMainDLast = emD;
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuD, _joueurRef.MainDroite);
				}
			}
		}
		if (_lblMainDroite != null)
		{
			bool montrerTexteD = !visD || !previewDOk;
			_lblMainDroite.Visible = montrerTexteD;
			string nomD = Atlas_Matiere.ObtenirNomObjet(_joueurRef.MainDroite);
			_lblMainDroite.Text = string.IsNullOrEmpty(nomD) ? "Main D\n[Vide]" : $"Main D\n[{nomD}]";
		}
		RafraichirQuantiteSlot(MainDroiteSlot, _joueurRef.MainDroite);
		AppliquerBordureActive(MainDroiteSlot, !_joueurRef.MainGaucheEstActive);

		ResoudreReferencesSlotsMains();
		AssurerPreviews3DMains();
		if (EquipementCorpsSlot != null && _meshPreviewMenuCeinture != null && GodotObject.IsInstanceValid(_meshPreviewMenuCeinture))
		{
			var eqC = _joueurRef.EquipementCeinture;
			bool visC = _joueurRef.InventaireSlotAunVisuel3D(eqC);
			bool vpCOk = _vpMenuCeinture != null && GodotObject.IsInstanceValid(_vpMenuCeinture);
			if (vpCOk)
			{
				_vpMenuCeinture.Visible = visC;
				if (visC)
				{
					ulong emC = EmpreinteSlotPourPreviewMenu(eqC);
					if (emC != _empreinteCeintureLast)
					{
						_empreinteCeintureLast = emC;
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuCeinture, eqC);
					}
				}
				else
				{
					_empreinteCeintureLast = 0UL;
					_meshPreviewMenuCeinture.Mesh = null;
					_meshPreviewMenuCeinture.MaterialOverride = null;
				}
			}
			if (_lblSlotCeinture != null)
			{
				bool montrerTexte = !visC || !vpCOk;
				_lblSlotCeinture.Visible = montrerTexte;
				string nomC = Atlas_Matiere.ObtenirNomObjet(eqC);
				_lblSlotCeinture.Text = string.IsNullOrEmpty(nomC) ? "Ceinture\n[slot vide]" : $"Ceinture\n[{nomC}]";
			}
			RafraichirQuantiteSlot(EquipementCorpsSlot, eqC);
		}
		if (EquipementSacSlot != null && _meshPreviewMenuSacEquip != null && GodotObject.IsInstanceValid(_meshPreviewMenuSacEquip))
		{
			var eqS = _joueurRef.EquipementSacDos;
			bool visS = _joueurRef.InventaireSlotAunVisuel3D(eqS);
			bool vpSOk = _vpMenuSacEquip != null && GodotObject.IsInstanceValid(_vpMenuSacEquip);
			if (vpSOk)
			{
				_vpMenuSacEquip.Visible = visS;
				if (visS)
				{
					ulong emS = EmpreinteSlotPourPreviewMenu(eqS);
					if (emS != _empreinteSacLast)
					{
						_empreinteSacLast = emS;
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuSacEquip, eqS);
					}
				}
				else
				{
					_empreinteSacLast = 0UL;
					_meshPreviewMenuSacEquip.Mesh = null;
					_meshPreviewMenuSacEquip.MaterialOverride = null;
				}
			}
			if (_lblSlotSacEquip != null)
			{
				bool montrerTexte = !visS || !vpSOk;
				_lblSlotSacEquip.Visible = montrerTexte;
				string nomS = Atlas_Matiere.ObtenirNomObjet(eqS);
				_lblSlotSacEquip.Text = string.IsNullOrEmpty(nomS) ? "Sac\n[slot vide]" : $"Sac\n[{nomS}]";
			}
			RafraichirQuantiteSlot(EquipementSacSlot, eqS);
		}
		if (CarnetSavoirSlot != null && _meshPreviewMenuCarnet != null && GodotObject.IsInstanceValid(_meshPreviewMenuCarnet))
		{
			var eqK = _joueurRef.EquipementCarnet;
			bool visK = _joueurRef.InventaireSlotAunVisuel3D(eqK);
			bool vpKOk = _vpMenuCarnet != null && GodotObject.IsInstanceValid(_vpMenuCarnet);
			if (vpKOk)
			{
				_vpMenuCarnet.Visible = visK;
				if (visK)
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuCarnet, eqK);
				else
				{
					_meshPreviewMenuCarnet.Mesh = null;
					_meshPreviewMenuCarnet.MaterialOverride = null;
				}
			}
			if (_lblSlotCarnet != null)
			{
				bool montrerTexte = !visK || !vpKOk;
				_lblSlotCarnet.Visible = montrerTexte;
				string nomK = Atlas_Matiere.ObtenirNomObjet(eqK);
				_lblSlotCarnet.Text = string.IsNullOrEmpty(nomK) ? "Carnet\n[vide]" : $"Carnet\n[{nomK}]";
			}
			RafraichirQuantiteSlot(CarnetSavoirSlot, eqK);
		}

		AppliquerDispositionGrilleCraft();
		MettreAJourEnteteModeRack();
		if (_ecranBarreCourant == ModeEcranBarreMenu.Analyseur)
			RafraichirPanneauAnalyseur();
		else if (_ecranBarreCourant == ModeEcranBarreMenu.CreatifAdmin)
			RafraichirPanneauCreatifAdmin();
		if (_joueurRef.StockageRackBatonsOuvert && _joueurRef.RackBatonsOuvert != null && GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert))
		{
			if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBatons)
				_joueurRef.SynchroniserVisuelRackBatons(_joueurRef.RackBatonsOuvert);
			else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBuches)
				_joueurRef.SynchroniserVisuelRackBuches(_joueurRef.RackBatonsOuvert);
            else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche)
                _joueurRef.RackBatonsOuvert.SynchroniserCombustiblePitFeuRocheDepuisGrille();
		}

		if (ObtenirGrilleSac() is GridContainer grilleSac)
		{
			AssurerCapaciteGrillesStockage();
			int capSac = Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos);
			bool afficher = _joueurRef.ASacEquipe();
			grilleSac.Visible = afficher;
			grilleSac.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleSac.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			for (int i = 0; i < grilleSac.GetChildCount(); i++)
			{
				if (grilleSac.GetChild(i) is not Control c) continue;
				bool visCase = afficher && i < capSac;
				c.Visible = visCase;
				if (c is Panel p && TrouverOuCreerLabel(p, " ") is Label l)
				{
					string nomSac = visCase ? Atlas_Matiere.ObtenirNomObjet(_joueurRef.RefSlotSac(i)) : "";
					int q = visCase ? Joueur.ObtenirQuantiteSlot(_joueurRef.RefSlotSac(i)) : 0;
					l.Text = string.IsNullOrEmpty(nomSac) ? " " : (q > 1 ? $"{nomSac} x{q}" : nomSac);
					RafraichirQuantiteSlot(p, visCase ? _joueurRef.RefSlotSac(i) : new SlotInventaire());
				}
			}
		}

		if (ObtenirGrilleCeintureStockage() is GridContainer grilleCeintSt)
		{
			AssurerCapaciteGrillesStockage();
			int capCeinture = Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture);
			bool afficherC = _joueurRef.ACeintureSacochesEquipe();
			grilleCeintSt.Visible = afficherC;
			grilleCeintSt.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleCeintSt.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			for (int i = 0; i < grilleCeintSt.GetChildCount(); i++)
			{
				if (grilleCeintSt.GetChild(i) is not Control c) continue;
				bool visCase = afficherC && i < capCeinture;
				c.Visible = visCase;
				if (c is Panel p && TrouverOuCreerLabel(p, " ") is Label l)
				{
					var sl = visCase ? _joueurRef.RefSlotCeintureStockage(i) : new SlotInventaire();
					string nom = visCase ? Atlas_Matiere.ObtenirNomObjet(sl) : "";
					int q = visCase ? Joueur.ObtenirQuantiteSlot(sl) : 0;
					l.Text = string.IsNullOrEmpty(nom) ? " " : (q > 1 ? $"{nom} x{q}" : nom);
					RafraichirQuantiteSlot(p, sl);
				}
			}
		}

		RafraichirCellulesCraft();
		RafraichirAffichageCurseurSouris();
	}

	/// <summary>Inventaire (Q) : 2×2 visible. Établi (E sur table) : 3×3.</summary>
	private void AppliquerDispositionGrilleCraft()
	{
		ResoudreGrilleAssemblage();
		if (GrilleAssemblage == null || _joueurRef == null) return;
		bool etabli = _joueurRef.CraftGrille3x3AuTable;
		bool pitRoche = _joueurRef.StockageRackBatonsOuvert
			&& _joueurRef.RackBatonsOuvert != null
			&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
			&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
		GrilleAssemblage.Columns = etabli ? 3 : 2;
		for (int i = 0; i < GrilleAssemblage.GetChildCount(); i++)
		{
			if (GrilleAssemblage.GetChild(i) is Control c)
				c.Visible = pitRoche ? i < 3 : (etabli || i < 4);
		}
		if (GrilleAssemblage.GetParent() is Panel cadre)
			cadre.CustomMinimumSize = pitRoche ? new Vector2(240, 96) : (etabli ? new Vector2(240, 240) : new Vector2(168, 168));
	}

	private void AppliquerBordureActive(Panel slot, bool estActif)
	{
		if (Engine.IsEditorHint() || slot == null) return;
		var style = slot.GetThemeStylebox("panel") as StyleBoxFlat;
		if (style != null)
		{
			var nouveauStyle = (StyleBoxFlat)style.Duplicate();
			nouveauStyle.BorderColor = estActif ? new Color(1, 0.9f, 0.2f) : new Color(1, 1, 1, 0.3f);
			slot.AddThemeStyleboxOverride("panel", nouveauStyle);
		}
	}
}
