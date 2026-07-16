using Godot;
using System;

public partial class MenuAnatomie : Control
{
	private Panel _cadreFourTorchie;
	private GridContainer _grilleFourCombustible;
	private GridContainer _grilleFourCuisson;
	private GridContainer _grilleFourResultat;
	private Label _lblFourTorchieTitre;
	private Label _lblFourTorchieTemperature;
	private ProgressBar _barreFourTorchieTemperature;
	private ProgressBar _barreFourTorchieCombustion;
	private ProgressBar[] _barresFourCuisson;
	private SubViewportContainer[] _vpFourTorchie;
	private MeshInstance3D[] _meshPreviewFourTorchie;
	private Label[] _lblFourTorchie;
	private ulong[] _empreinteFourTorchieLast;
	private bool _clicsGrilleFourTorchieConnectes;
	private float _accumRafraichFourTorchie;

	private void AssurerCadreFourTorchie()
	{
		if (Engine.IsEditorHint() || _cadreFourTorchie != null && GodotObject.IsInstanceValid(_cadreFourTorchie))
			return;

		Control zoneDroite = GetNodeOrNull<Control>("MarginPrincipal/VBoxPrincipal/CorpsHBox/ZoneDroite")
			?? FindChild("ZoneDroite", true, false) as Control;
		if (zoneDroite == null)
			return;

		_cadreFourTorchie = new Panel
		{
			Name = "CadreFourTorchie",
			Visible = false,
			CustomMinimumSize = new Vector2(420, 220)
		};
		_cadreFourTorchie.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (ObtenirCadreCoffreBois()?.GetThemeStylebox("panel") is StyleBox styleCadre)
			_cadreFourTorchie.AddThemeStyleboxOverride("panel", (StyleBox)styleCadre.Duplicate());

		var vbox = new VBoxContainer { Name = "VBoxFour", MouseFilter = Control.MouseFilterEnum.Ignore };
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = 8f;
		vbox.OffsetTop = 6f;
		vbox.OffsetRight = -8f;
		vbox.OffsetBottom = -6f;
		_cadreFourTorchie.AddChild(vbox);

		_lblFourTorchieTitre = new Label
		{
			Text = "Four en torchie",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_lblFourTorchieTitre.AddThemeFontSizeOverride("font_size", 14);
		vbox.AddChild(_lblFourTorchieTitre);

		var haut = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		vbox.AddChild(haut);

		_grilleFourCombustible = CreerGrilleFourTorchie(haut, 1, "GrilleFourCombustible");
		var colTemp = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		haut.AddChild(colTemp);
		_lblFourTorchieTemperature = new Label { Text = "Température : 0 °C", HorizontalAlignment = HorizontalAlignment.Center };
		_lblFourTorchieTemperature.AddThemeFontSizeOverride("font_size", 12);
		colTemp.AddChild(_lblFourTorchieTemperature);
		_barreFourTorchieTemperature = CreerBarreFourTorchie(colTemp, "BarreTemperatureFour", new Color(0.92f, 0.35f, 0.12f));
		_barreFourTorchieCombustion = CreerBarreFourTorchie(colTemp, "BarreCombustionFour", new Color(0.95f, 0.5f, 0.18f));

		var lblCuisson = new Label { Text = "Cuisson (4 emplacements)", HorizontalAlignment = HorizontalAlignment.Left };
		lblCuisson.AddThemeFontSizeOverride("font_size", 11);
		vbox.AddChild(lblCuisson);
		_grilleFourCuisson = CreerGrilleFourTorchie(vbox, 4, "GrilleFourCuisson");

		var lblResultat = new Label { Text = "Résultats (4 emplacements)", HorizontalAlignment = HorizontalAlignment.Left };
		lblResultat.AddThemeFontSizeOverride("font_size", 11);
		vbox.AddChild(lblResultat);
		_grilleFourResultat = CreerGrilleFourTorchie(vbox, 4, "GrilleFourResultat");

		zoneDroite.AddChild(_cadreFourTorchie);
		CallDeferred(nameof(ConnecterClicsInventaire));
	}

	private static GridContainer CreerGrilleFourTorchie(Node parent, int colonnes, string nom)
	{
		var grille = new GridContainer
		{
			Name = nom,
			Columns = colonnes,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		grille.AddThemeConstantOverride("h_separation", 4);
		grille.AddThemeConstantOverride("v_separation", 4);
		parent.AddChild(grille);
		return grille;
	}

	private static ProgressBar CreerBarreFourTorchie(Node parent, string nom, Color couleur)
	{
		var barre = new ProgressBar
		{
			Name = nom,
			MinValue = 0,
			MaxValue = 900,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 14),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		var fond = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.55f) };
		fond.SetCornerRadiusAll(2);
		var remplissage = new StyleBoxFlat { BgColor = couleur };
		remplissage.SetCornerRadiusAll(2);
		barre.AddThemeStyleboxOverride("background", fond);
		barre.AddThemeStyleboxOverride("fill", remplissage);
		parent.AddChild(barre);
		return barre;
	}

	private Panel ObtenirCadreFourTorchie() => _cadreFourTorchie;

	private void AssurerPreviewsFourTorchie()
	{
		if (Engine.IsEditorHint() || _grilleFourCombustible == null)
			return;
		int nChild = ItemPhysique.FourTorchieNbSlots;
		if (_vpFourTorchie != null && _vpFourTorchie.Length == nChild)
			return;

		_vpFourTorchie = new SubViewportContainer[nChild];
		_meshPreviewFourTorchie = new MeshInstance3D[nChild];
		_lblFourTorchie = new Label[nChild];
		_empreinteFourTorchieLast = new ulong[nChild];
		_barresFourCuisson = new ProgressBar[ItemPhysique.FourTorchieNbCuisson];

		AssurerCasesGrilleFour(_grilleFourCombustible, 1, 0);
		AssurerCasesGrilleFour(_grilleFourCuisson, 4, 1);
		AssurerCasesGrilleFour(_grilleFourResultat, 4, 5);

		for (int i = 0; i < nChild; i++)
		{
			Panel p = ObtenirPanelFourTorchieIndex(i);
			if (p == null) continue;
			_meshPreviewFourTorchie[i] = CreerViewportPreviewDansSlot(p, $"VpFour{i}", out _vpFourTorchie[i]);
			_lblFourTorchie[i] = TrouverOuCreerLabel(p, " ");
		}

		for (int i = 0; i < ItemPhysique.FourTorchieNbCuisson; i++)
		{
			Panel p = ObtenirPanelFourTorchieIndex(ItemPhysique.FourTorchiePremierSlotCuisson + i);
			if (p == null) continue;
			_barresFourCuisson[i] = CreerBarreFourTorchie(p, $"BarreCuissonFour{i}", new Color(0.45f, 0.8f, 0.35f));
			_barresFourCuisson[i].SetAnchorsPreset(Control.LayoutPreset.BottomWide);
			_barresFourCuisson[i].OffsetLeft = 3f;
			_barresFourCuisson[i].OffsetRight = -3f;
			_barresFourCuisson[i].OffsetTop = -11f;
			_barresFourCuisson[i].OffsetBottom = -3f;
			_barresFourCuisson[i].Visible = false;
		}

		_clicsGrilleFourTorchieConnectes = false;
		CallDeferred(nameof(ConnecterClicsInventaire));
	}

	private void AssurerCasesGrilleFour(GridContainer grille, int count, int indexDebut)
	{
		if (grille == null) return;
		while (grille.GetChildCount() < count)
		{
			int idx = indexDebut + grille.GetChildCount();
			Panel p = CreerCaseStockageSupplementaire(grille, idx);
			p.Name = $"FourCell{idx}";
		}
	}

	private Panel ObtenirPanelFourTorchieIndex(int idx)
	{
		if (idx == ItemPhysique.FourTorchieSlotCombustible)
			return _grilleFourCombustible?.GetChild(0) as Panel;
		if (ItemPhysique.EstIndexSlotCuissonFourTorchie(idx))
			return _grilleFourCuisson?.GetChild(idx - ItemPhysique.FourTorchiePremierSlotCuisson) as Panel;
		if (ItemPhysique.EstIndexSlotResultatFourTorchie(idx))
			return _grilleFourResultat?.GetChild(idx - ItemPhysique.FourTorchiePremierSlotResultat) as Panel;
		return null;
	}

	private void MettreAJourVisibiliteFourTorchie()
	{
		if (_joueurRef == null) return;
		AssurerCadreFourTorchie();
		bool four = _joueurRef.StockageFourTorchieOuvert;
		if (_cadreFourTorchie != null)
			_cadreFourTorchie.Visible = four;
		Control ligneCraft = GrilleAssemblage?.GetParent()?.GetParent() as Control;
		Panel cadreCoffre = ObtenirCadreCoffreBois();
		if (four)
		{
			if (ligneCraft != null) ligneCraft.Visible = false;
			if (cadreCoffre != null) cadreCoffre.Visible = false;
		}
	}

	private void RafraichirCellulesFourTorchie()
	{
		if (_joueurRef == null || !_joueurRef.StockageFourTorchieOuvert)
			return;
		AssurerCadreFourTorchie();
		AssurerPreviewsFourTorchie();
		if (_joueurRef.FourTorchieOuvert == null || !GodotObject.IsInstanceValid(_joueurRef.FourTorchieOuvert))
			return;

		ItemPhysique four = _joueurRef.FourTorchieOuvert;
		float temp = four.ObtenirTemperatureFourTorchie();
		float plafond = four.ObtenirPlafondThermiqueActifFourTorchie();
		if (_lblFourTorchieTemperature != null)
		{
			if (plafond > FourTorchieThermodynamique.TempAmbianteC + 5f)
				_lblFourTorchieTemperature.Text = $"Température : {temp:F0} °C (plafond ~{plafond:F0} °C)";
			else
				_lblFourTorchieTemperature.Text = $"Température : {temp:F0} °C";
		}
		if (_barreFourTorchieTemperature != null)
		{
			_barreFourTorchieTemperature.Visible = true;
			_barreFourTorchieTemperature.MaxValue = 900;
			_barreFourTorchieTemperature.Value = temp;
		}

		float pComb = four.ObtenirProgressionCombustionFourTorchie();
		if (_barreFourTorchieCombustion != null)
		{
			_barreFourTorchieCombustion.Visible = pComb >= 0f;
			if (pComb >= 0f)
				_barreFourTorchieCombustion.Value = pComb * 100f;
		}

		for (int i = 0; i < ItemPhysique.FourTorchieNbSlots; i++)
		{
			ref SlotInventaire s = ref _joueurRef.RefSlotFourTorchie(i);
			Panel panel = ObtenirPanelFourTorchieIndex(i);
			bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
			bool vpOk = _vpFourTorchie != null && i < _vpFourTorchie.Length && _vpFourTorchie[i] != null && GodotObject.IsInstanceValid(_vpFourTorchie[i]);
			if (vpOk)
			{
				_vpFourTorchie[i].Visible = vis;
				if (_meshPreviewFourTorchie != null && i < _meshPreviewFourTorchie.Length && _meshPreviewFourTorchie[i] != null)
				{
					if (vis)
					{
						ulong em = EmpreinteSlotPourPreviewMenu(s);
						if (s.ID == Joueur.IdObjetBolArgile || s.ID == Joueur.IdObjetMouleArgile)
						{
							float prog = 0f;
							if (ItemPhysique.EstIndexSlotCuissonFourTorchie(i))
							{
								int idxC = i - ItemPhysique.FourTorchiePremierSlotCuisson;
								float p = four.ObtenirProgressionCuissonFourTorchie(idxC);
								if (p >= 0f) prog = p;
							}
							float facteur = FourTorchieThermodynamique.ObtenirFacteurTeinteChauffeBolArgile(temp, prog);
							em ^= (ulong)Mathf.RoundToInt(facteur * 80f) << 32;
						}
						else if (s.ID == Joueur.IdObjetBolCeramique || s.ID == Joueur.IdObjetMouleCeramique
							|| s.ID == Joueur.IdObjetBolEtainFonduChaud || s.ID == Joueur.IdObjetBolCeramiqueScorie)
						{
							float facteurCer = FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramiqueSlot(s);
							em ^= (ulong)Mathf.RoundToInt(facteurCer * 80f) << 32;
							em ^= (ulong)s.IndexChimique << 40;
						}
						if (_empreinteFourTorchieLast == null || i >= _empreinteFourTorchieLast.Length || em != _empreinteFourTorchieLast[i])
						{
							_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewFourTorchie[i], s);
							if (_empreinteFourTorchieLast != null && i < _empreinteFourTorchieLast.Length)
								_empreinteFourTorchieLast[i] = em;
						}
					}
					else
					{
						if (_empreinteFourTorchieLast != null && i < _empreinteFourTorchieLast.Length)
							_empreinteFourTorchieLast[i] = 0UL;
						_meshPreviewFourTorchie[i].Mesh = null;
						_meshPreviewFourTorchie[i].MaterialOverride = null;
					}
				}
			}
			if (_lblFourTorchie != null && i < _lblFourTorchie.Length && _lblFourTorchie[i] != null)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(s);
				if (s.EstVide)
				{
					if (i == ItemPhysique.FourTorchieSlotCombustible) nom = "Combustible";
					else if (ItemPhysique.EstIndexSlotCuissonFourTorchie(i)) nom = "Cuisson";
					else if (ItemPhysique.EstIndexSlotResultatFourTorchie(i)) nom = "Résultat";
				}
				_lblFourTorchie[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
				_lblFourTorchie[i].Visible = !vis || !vpOk;
			}
			if (panel != null)
				RafraichirQuantiteSlot(panel, s);
		}

		for (int c = 0; c < ItemPhysique.FourTorchieNbCuisson; c++)
		{
			if (_barresFourCuisson == null || c >= _barresFourCuisson.Length || _barresFourCuisson[c] == null)
				continue;
			float pCuis = four.ObtenirProgressionCuissonFourTorchie(c);
			_barresFourCuisson[c].Visible = pCuis >= 0f;
			if (pCuis >= 0f)
				_barresFourCuisson[c].Value = pCuis * 100f;
		}
	}
}
