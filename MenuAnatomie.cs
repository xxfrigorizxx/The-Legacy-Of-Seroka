using Godot;
using System;

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
	[Export] public Panel EquipementCorpsSlot;
	[Export] public Panel SlotResultatCraft;

	private Label _lblMainGauche;
	private Label _lblMainDroite;
	private bool _abonneViewport;
	private SubViewportContainer _vpMenuGauche;
	private SubViewportContainer _vpMenuDroite;
	private MeshInstance3D _meshPreviewMenuG;
	private MeshInstance3D _meshPreviewMenuD;

	/// <summary>Objet « tenu par la souris » dans l’inventaire Q : échange au clic avec une main ou une case 2×2.</summary>
	private SlotInventaire _curseurMenu;
	private Label[] _lblCraft;
	private MeshInstance3D[] _meshPreviewCraft;
	private SubViewportContainer[] _vpCraft;
	private SubViewportContainer _vpResultatCraft;
	private MeshInstance3D _meshPreviewResultatCraft;
	private Label _lblResultatCraft;

	private const string CheminGrilleSac = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/GrilleSac";
	private const string CheminMainGauche = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneMainsCeinture/MainGaucheSlot";
	private const string CheminMainDroite = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneMainsCeinture/MainDroiteSlot";
	private const string CheminGrilleAssemblage = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneCraft/CadreCraft/GrilleAssemblage";
	private const string CheminSlotResultatCraft = "MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite/LigneCraft/CraftSortie";

	private bool _clicsMainsConnectes;
	private bool _clicsCraftConnectes;
	private bool _clicsSlotResultatCraftConnecte;
	private bool _barreOngletsJeuConfiguree;

	private const string CheminBarreOnglets = "MarginPrincipal/VBoxPrincipal/BarreOnglets";
	private const string CheminVBoxPrincipal = "MarginPrincipal/VBoxPrincipal";
	private const string CheminCorpsHBox = "MarginPrincipal/VBoxPrincipal/CorpsHBox";

	private enum ModeEcranBarreMenu
	{
		Inventaire,
		SauvegarderQuitter
	}

	private ModeEcranBarreMenu _ecranBarreCourant = ModeEcranBarreMenu.Inventaire;
	private Panel _ongletInventaireBarre;
	private Panel _ongletQuitterBarre;
	private HBoxContainer _corpsHBoxRef;
	private Panel _panneauSauvegarderQuitter;

	private Panel _conteneurFlottantCurseur;
	private SubViewportContainer _vpCurseurSouris;
	private MeshInstance3D _meshCurseurSouris;
	private Label _lblCurseurSouris;
	/// <summary>Infobulle près du curseur : nom exact du slot survolé (débogage des noms / ADN).</summary>
	private Panel _panneauInfobulleSlot;
	private Label _lblInfobulleSlot;

	private void ResoudreReferencesSlotsMains()
	{
		if (MainGaucheSlot == null || !GodotObject.IsInstanceValid(MainGaucheSlot))
			MainGaucheSlot = GetNodeOrNull<Panel>(CheminMainGauche) ?? FindChild("MainGaucheSlot", true, false) as Panel;
		if (MainDroiteSlot == null || !GodotObject.IsInstanceValid(MainDroiteSlot))
			MainDroiteSlot = GetNodeOrNull<Panel>(CheminMainDroite) ?? FindChild("MainDroiteSlot", true, false) as Panel;
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

	public void Initialiser(Joueur joueur)
	{
		_joueurRef = joueur;
		ResoudreReferencesSlotsMains();
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
		}
	}

	private void AssurerPreviews3DMains()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreReferencesSlotsMains();
		if (MainGaucheSlot == null || MainDroiteSlot == null) return;
		if (_meshPreviewMenuG != null && GodotObject.IsInstanceValid(_meshPreviewMenuG)) return;

		_meshPreviewMenuG = CreerViewportPreviewDansSlot(MainGaucheSlot, "ViewportMenuMainG", out _vpMenuGauche);
		_meshPreviewMenuD = CreerViewportPreviewDansSlot(MainDroiteSlot, "ViewportMenuMainD", out _vpMenuDroite);
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
	}

	private void TraiterClicInventaire(InputEvent e, int mode, int craftIdx = -1)
	{
		if (_joueurRef == null) return;
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;

		if (mode == 0)
			EchangerCurseurAvec(ref _joueurRef.MainGauche);
		else if (mode == 1)
			EchangerCurseurAvec(ref _joueurRef.MainDroite);
		else if (mode == 2 && craftIdx >= 0)
		{
			if (!_joueurRef.CraftGrille3x3AuTable && craftIdx >= 4)
				return;
			var g = _joueurRef.ObtenirGrilleCraftAffichee();
			if (g == null || craftIdx >= g.Length)
				return;
			EchangerCurseurAvec(ref _joueurRef.RefSlotCraft(craftIdx));
			_joueurRef.VerifierRecettes();
		}
		else if (mode == 3)
		{
			if (_curseurMenu.EstVide && !_joueurRef.SlotResultatCraft.EstVide)
			{
				_curseurMenu = _joueurRef.SlotResultatCraft;
				_joueurRef.ConsommerIngredientsCraft();
				_joueurRef.VerifierRecettes();
			}
			else
				return;
		}
		else
			return;

		GetViewport()?.SetInputAsHandled();
		_joueurRef.RafraichirHUD();
	}

	private void EchangerCurseurAvec(ref SlotInventaire slot)
	{
		var a = _curseurMenu;
		_curseurMenu = slot;
		slot = a;
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
			var g = _joueurRef.ObtenirGrilleCraftAffichee();
			bool place = false;
			int maxI = _joueurRef.CraftGrille3x3AuTable ? 9 : 4;
			if (g != null)
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
			_clicsCraftConnectes = false;
			CallDeferred(nameof(ConnecterClicsInventaire));
		}
		if (_vpCraft != null && nChild > 0 && _vpCraft[0] != null && GodotObject.IsInstanceValid(_vpCraft[0]))
			return;
		_meshPreviewCraft = new MeshInstance3D[nChild];
		_vpCraft = new SubViewportContainer[nChild];
		_lblCraft = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (GrilleAssemblage.GetChild(i) is not Panel p) continue;
			_meshPreviewCraft[i] = CreerViewportPreviewDansSlot(p, $"VpCraft{i}", out _vpCraft[i]);
			_lblCraft[i] = TrouverOuCreerLabel(p, " ");
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
		bool montre = EstOuvert && !_curseurMenu.EstVide && _ecranBarreCourant == ModeEcranBarreMenu.Inventaire;
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
		_conteneurFlottantCurseur.GlobalPosition = GetGlobalMousePosition() - _conteneurFlottantCurseur.Size * 0.5f;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || !EstOuvert || _ecranBarreCourant != ModeEcranBarreMenu.Inventaire)
			return;
		MettreAJourInfobulleSourisInventaire();
		if (_conteneurFlottantCurseur != null && _conteneurFlottantCurseur.Visible)
		{
			Vector2 demi = _conteneurFlottantCurseur.Size * 0.5f;
			_conteneurFlottantCurseur.GlobalPosition = GetGlobalMousePosition() - demi;
		}
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
		if (SlotResultatCraft != null && GodotObject.IsInstanceValid(SlotResultatCraft)
			&& (h == SlotResultatCraft || SlotResultatCraft.IsAncestorOf(h)))
		{
			slot = _joueurRef.SlotResultatCraft;
			return true;
		}
		if (GrilleAssemblage != null && GodotObject.IsInstanceValid(GrilleAssemblage) && GrilleAssemblage.IsAncestorOf(h))
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

	private void RafraichirCellulesCraft()
	{
		if (_joueurRef == null || GrilleAssemblage == null) return;
		AssurerPreviewsCraft();
		_joueurRef.VerifierRecettes();
		var gCraft = _joueurRef.ObtenirGrilleCraftAffichee();
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
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewCraft[i], s);
					else
					{
						_meshPreviewCraft[i].Mesh = null;
						_meshPreviewCraft[i].MaterialOverride = null;
					}
				}
			}
			if (_lblCraft != null && i < _lblCraft.Length && _lblCraft[i] != null)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(s);
				_lblCraft[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
				_lblCraft[i].Visible = !vis || !vpOk;
			}
		}

		ResoudreSlotResultatCraft();
		if (SlotResultatCraft != null)
		{
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
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewResultatCraft, sRes);
				else if (_meshPreviewResultatCraft != null)
				{
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
		btnQuit.Pressed += () => GetTree().Quit();
		col.AddChild(btnQuit);

		vbox.AddChild(_panneauSauvegarderQuitter);
	}

	private void AppliquerEcranBarre(ModeEcranBarreMenu mode)
	{
		if (Engine.IsEditorHint()) return;
		_ecranBarreCourant = mode;
		AssurerPanneauSauvegarderQuitter();
		if (_corpsHBoxRef != null)
			_corpsHBoxRef.Visible = mode == ModeEcranBarreMenu.Inventaire;
		if (_panneauSauvegarderQuitter != null)
			_panneauSauvegarderQuitter.Visible = mode == ModeEcranBarreMenu.SauvegarderQuitter;
		MettreAJourStyleOngletsBarre();
		RafraichirAffichageCurseurSouris();
	}

	private void MettreAJourStyleOngletsBarre()
	{
		Color actif = Colors.White;
		Color inactif = new(0.62f, 0.62f, 0.62f);
		if (_ongletInventaireBarre != null)
			_ongletInventaireBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire ? actif : inactif;
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
		if (EquipementCorpsSlot != null) EquipementCorpsSlot.Hide();

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
			// Grille cachée : pas d’expansion verticale (sinon trou sous les mains) ; visible avec sac : prend l’espace restant.
			sac.SizeFlagsVertical = sac.Visible ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;
		}
	}

	public void BasculerVisibilite()
	{
		EstOuvert = !EstOuvert;
		Visible = EstOuvert;

		if (!EstOuvert)
		{
			ResoudreCurseurAvantFermeture();
			if (_joueurRef != null)
			{
				_joueurRef.CraftGrille3x3AuTable = false;
				_joueurRef.AtelierPlanTravailOuvert = null;
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
				_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuG, _joueurRef.MainGauche);
		}
		if (_lblMainGauche != null)
		{
			bool montrerTexteG = !visG || !previewGOk;
			_lblMainGauche.Visible = montrerTexteG;
			string nomG = Atlas_Matiere.ObtenirNomObjet(_joueurRef.MainGauche);
			_lblMainGauche.Text = string.IsNullOrEmpty(nomG) ? "Main G\n[Vide]" : $"Main G\n[{nomG}]";
		}
		AppliquerBordureActive(MainGaucheSlot, _joueurRef.MainGaucheEstActive);

		if (previewDOk)
		{
			_vpMenuDroite.Visible = visD;
			if (_meshPreviewMenuD != null)
				_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuD, _joueurRef.MainDroite);
		}
		if (_lblMainDroite != null)
		{
			bool montrerTexteD = !visD || !previewDOk;
			_lblMainDroite.Visible = montrerTexteD;
			string nomD = Atlas_Matiere.ObtenirNomObjet(_joueurRef.MainDroite);
			_lblMainDroite.Text = string.IsNullOrEmpty(nomD) ? "Main D\n[Vide]" : $"Main D\n[{nomD}]";
		}
		AppliquerBordureActive(MainDroiteSlot, !_joueurRef.MainGaucheEstActive);

		AppliquerDispositionGrilleCraft();

		if (ObtenirGrilleSac() is GridContainer grilleSac)
		{
			bool afficher = _joueurRef.AStockageSacOuCeintureEquipe();
			grilleSac.Visible = afficher;
			grilleSac.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleSac.SizeFlagsVertical = afficher ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;
			foreach (Node enfant in grilleSac.GetChildren())
			{
				if (enfant is Control c) c.Visible = afficher;
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
		GrilleAssemblage.Columns = etabli ? 3 : 2;
		for (int i = 0; i < GrilleAssemblage.GetChildCount(); i++)
		{
			if (GrilleAssemblage.GetChild(i) is Control c)
				c.Visible = etabli || i < 4;
		}
		if (GrilleAssemblage.GetParent() is Panel cadre)
			cadre.CustomMinimumSize = etabli ? new Vector2(240, 240) : new Vector2(168, 168);
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
