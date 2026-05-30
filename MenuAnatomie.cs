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
	private SubViewportContainer[] _vpSacStockage;
	private MeshInstance3D[] _meshPreviewSacStockage;
	private Label[] _lblSacStockage;
	private ulong[] _empreinteSacStockageLast;
	private SubViewportContainer[] _vpCeintureStockage;
	private MeshInstance3D[] _meshPreviewCeintureStockage;
	private Label[] _lblCeintureStockage;
	private ulong[] _empreinteCeintureStockageLast;

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
	private Label _lblAnalyseurTitre;
	private Label _lblAnalyseurAide;
	private VBoxContainer _colAnalyseurContenu;
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
	private int _versionCatalogueCreatifChargee;
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
	private readonly Dictionary<string, Label> _labelsEtatOsCorps = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, StyleBoxFlat> _stylesRemplissageSanteCorps = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, ColorRect> _segmentsBrulureSanteCorps = new(StringComparer.OrdinalIgnoreCase);
	private static readonly string[] ClesSectionsSanteCorpsOrdre =
	{
		"tete", "torse", "bras_gauche", "bras_droit", "jambe_gauche", "jambe_droite"
	};
	private const float DistanceCameraApercuJoueurCorps = 2.55f;
	private const float DecalageLateralCameraApercuJoueurCorps = 0.00f;
	private const float HauteurCameraApercuJoueurCorps = 0.14f;
	private const float HauteurCibleCameraApercuJoueurCorps = 0.62f;
	private SubViewportContainer _vpApercuJoueurCorps;
	private SubViewport _svApercuJoueurCorps;
	private Camera3D _cameraApercuJoueurCorps;
	private Node3D _racineApercuJoueurCorps;
	private Node3D _avatarApercuJoueurCorps;
	private ulong _empreinteAvatarApercuJoueurCorps;

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
		_avatarApercuJoueurCorps = null;
		_racineApercuJoueurCorps = null;
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

}
