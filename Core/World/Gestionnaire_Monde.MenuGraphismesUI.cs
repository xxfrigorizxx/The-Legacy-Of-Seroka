using Godot;

/// <summary>
/// Menu pause + panneau de réglages graphiques (UI). Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: mêmes contrôles, mêmes effets d'application/preset que l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void CreerMenuPause()
	{
		// Au-dessus de l’inventaire (calque 100 sur le joueur).
		var layer = new CanvasLayer { Layer = 101, ProcessMode = ProcessModeEnum.Always };
		AddChild(layer);
		_panelPause = new Panel();
		_panelPause.SetAnchorsPreset(Control.LayoutPreset.Center);
		_panelPause.OffsetLeft = -100;
		_panelPause.OffsetTop = -80;
		_panelPause.OffsetRight = 100;
		_panelPause.OffsetBottom = 80;
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = 20;
		vbox.OffsetTop = 20;
		vbox.OffsetRight = -20;
		vbox.OffsetBottom = -20;
		vbox.AddThemeConstantOverride("separation", 10);
		_panelPause.AddChild(vbox);
		var lbl = new Label { Text = "Pause", HorizontalAlignment = HorizontalAlignment.Center };
		vbox.AddChild(lbl);
		var btnResume = new Button { Text = "Reprendre" };
		btnResume.Pressed += () => { ToggleMenuPause(); };
		vbox.AddChild(btnResume);
		var btnSave = new Button { Text = "Sauvegarder" };
		btnSave.Pressed += () => SauvegarderManuelDepuisMenu("BoutonPause");
		vbox.AddChild(btnSave);
		var btnGraphismes = new Button { Text = "Graphismes" };
		btnGraphismes.Pressed += () =>
		{
			if (_panelGraphismes != null)
			{
				SynchroniserPanelGraphiqueDepuisOptions(CapturerOptionsGraphiquesCourantes(_optionsGraphiquesActuelles?.Preset ?? PresetGraphique.Personnalise));
				_panelGraphismes.Visible = true;
				_editionGraphiqueEnDirect = true;
				ForcerCycleSolaireActif();
				RafraichirIndicateurModeEditionGraphique();
				// Edition en direct : on laisse le monde tourner pendant les ajustements.
				_panelPause.Visible = false;
				GetTree().Paused = false;
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		};
		vbox.AddChild(btnGraphismes);
		var btnMenu = new Button { Text = "Menu principal" };
		btnMenu.Pressed += () =>
		{
			ToggleMenuPause();
			GetTree().Paused = false;
			SauvegarderManuelDepuisMenu();
			GetTree().ChangeSceneToFile("res://menu_principal.tscn");
		};
		vbox.AddChild(btnMenu);
		var btnQuit = new Button { Text = "Quitter le jeu" };
		btnQuit.Pressed += () =>
		{
			SauvegarderManuelDepuisMenu();
			GetTree().Quit();
		};
		vbox.AddChild(btnQuit);
		layer.AddChild(_panelPause);
		CreerPanelGraphismes(layer);
		_panelPause.Visible = false;
	}

	private (HSlider slider, Label valeur) CreerLigneSlider(Control parent, string texte, float min, float max, float pas)
	{
		var ligne = new HBoxContainer();
		ligne.AddThemeConstantOverride("separation", 8);
		parent.AddChild(ligne);
		var label = new Label
		{
			Text = texte,
			CustomMinimumSize = new Vector2(230, 0),
			SizeFlagsHorizontal = Control.SizeFlags.Fill
		};
		ligne.AddChild(label);
		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = pas,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		var btnMoins = new Button { Text = "-", CustomMinimumSize = new Vector2(28, 0) };
		btnMoins.Pressed += () => slider.Value = Mathf.Max(slider.MinValue, slider.Value - slider.Step);
		ligne.AddChild(btnMoins);
		ligne.AddChild(slider);
		var btnPlus = new Button { Text = "+", CustomMinimumSize = new Vector2(28, 0) };
		btnPlus.Pressed += () => slider.Value = Mathf.Min(slider.MaxValue, slider.Value + slider.Step);
		ligne.AddChild(btnPlus);
		var valeur = new Label
		{
			Text = "-",
			HorizontalAlignment = HorizontalAlignment.Right,
			CustomMinimumSize = new Vector2(70, 0)
		};
		ligne.AddChild(valeur);
		return (slider, valeur);
	}

	private void CreerPanelGraphismes(CanvasLayer layer)
	{
		_panelGraphismes = new Panel
		{
			Visible = false
		};
		_panelGraphismes.SetAnchorsPreset(Control.LayoutPreset.Center);
		_panelGraphismes.OffsetLeft = -360;
		_panelGraphismes.OffsetTop = -270;
		_panelGraphismes.OffsetRight = 360;
		_panelGraphismes.OffsetBottom = 270;

		var marge = new MarginContainer();
		marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		marge.AddThemeConstantOverride("margin_left", 16);
		marge.AddThemeConstantOverride("margin_top", 16);
		marge.AddThemeConstantOverride("margin_right", 16);
		marge.AddThemeConstantOverride("margin_bottom", 16);
		_panelGraphismes.AddChild(marge);

		var racine = new VBoxContainer();
		racine.AddThemeConstantOverride("separation", 8);
		marge.AddChild(racine);

		racine.AddChild(new Label
		{
			Text = "Reglages graphiques avances",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		_optionPresetGraphique = new OptionButton();
		_optionPresetGraphique.AddItem("Faible", (int)PresetGraphique.Faible);
		_optionPresetGraphique.AddItem("Moyen", (int)PresetGraphique.Moyen);
		_optionPresetGraphique.AddItem("Eleve", (int)PresetGraphique.Eleve);
		_optionPresetGraphique.AddItem("Ultra", (int)PresetGraphique.Ultra);
		_optionPresetGraphique.AddItem("Personnalise", (int)PresetGraphique.Personnalise);
		_optionPresetGraphique.ItemSelected += (_) => AppliquerPresetDepuisUI();
		racine.AddChild(_optionPresetGraphique);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		racine.AddChild(scroll);

		var contenu = new VBoxContainer();
		contenu.AddThemeConstantOverride("separation", 5);
		scroll.AddChild(contenu);

		(_sliderRenderDistance, _labelRenderDistanceValeur) = CreerLigneSlider(contenu, "Distance de rendu (chunks)", 6, 64, 1);
		(_sliderRayonQualiteProche, _labelRayonQualiteProcheValeur) = CreerLigneSlider(contenu, "Qualite proche chunks", 1, 24, 1);
		(_sliderDetailChunks, _labelDetailChunksValeur) = CreerLigneSlider(contenu, "Distance detail (chunks)", 6, 64, 1);
		(_sliderRayonGazon, _labelRayonGazonValeur) = CreerLigneSlider(contenu, "Visibilite gazon", 1, 24, 1);
		(_sliderRayonBuissons, _labelRayonBuissonsValeur) = CreerLigneSlider(contenu, "Visibilite buissons", 2, 32, 1);
		(_sliderRayonHorizon, _labelRayonHorizonValeur) = CreerLigneSlider(contenu, "Rayon horizon LOD", 24, 240, 1);
		(_sliderPasHorizon, _labelPasHorizonValeur) = CreerLigneSlider(contenu, "Pas horizon (metres)", 12, 80, 1);
		(_sliderAngleCulling, _labelAngleCullingValeur) = CreerLigneSlider(contenu, "Angle culling camera", 80, 175, 1);
		(_sliderMargeToujoursVisible, _labelMargeToujoursVisibleValeur) = CreerLigneSlider(contenu, "Marge toujours visible", 1, 32, 1);
		(_sliderMaxChunksFrame, _labelMaxChunksFrameValeur) = CreerLigneSlider(contenu, "Max chunks / frame", 2, 40, 1);
		(_sliderLodEtapes, _labelLodEtapesValeur) = CreerLigneSlider(contenu, "Etapes LOD texture", 8, 24, 1);

		_checkActiverHorizon = new CheckBox { Text = "Activer horizon lointain simplifie" };
		_checkActiverCulling = new CheckBox { Text = "Activer culling camera des chunks" };
		_checkLodUltraSmooth = new CheckBox { Text = "LOD texture ultra smooth" };
		_checkModeSurvieAgressif = new CheckBox
		{
			Text = "Sauver les FPS (gel streaming + plafonds; décoche = distance de rendu pleine, gate FPS désactivé)"
		};
		contenu.AddChild(_checkActiverHorizon);
		contenu.AddChild(_checkActiverCulling);
		contenu.AddChild(_checkLodUltraSmooth);
		contenu.AddChild(_checkModeSurvieAgressif);

		_sliderRenderDistance.ValueChanged += (_) =>
		{
			_sliderDetailChunks.MaxValue = _sliderRenderDistance.Value;
			if (_sliderDetailChunks.Value > _sliderDetailChunks.MaxValue)
				_sliderDetailChunks.Value = _sliderDetailChunks.MaxValue;
		};

		_sliderRenderDistance.ValueChanged += (_) => _labelRenderDistanceValeur.Text = $"{_sliderRenderDistance.Value:0}";
		_sliderRayonQualiteProche.ValueChanged += (_) => _labelRayonQualiteProcheValeur.Text = $"{_sliderRayonQualiteProche.Value:0}";
		_sliderDetailChunks.ValueChanged += (_) => _labelDetailChunksValeur.Text = $"{_sliderDetailChunks.Value:0}";
		_sliderRayonGazon.ValueChanged += (_) => _labelRayonGazonValeur.Text = $"{_sliderRayonGazon.Value:0}";
		_sliderRayonBuissons.ValueChanged += (_) => _labelRayonBuissonsValeur.Text = $"{_sliderRayonBuissons.Value:0}";
		_sliderRayonHorizon.ValueChanged += (_) => _labelRayonHorizonValeur.Text = $"{_sliderRayonHorizon.Value:0}";
		_sliderPasHorizon.ValueChanged += (_) => _labelPasHorizonValeur.Text = $"{_sliderPasHorizon.Value:0}m";
		_sliderAngleCulling.ValueChanged += (_) => _labelAngleCullingValeur.Text = $"{_sliderAngleCulling.Value:0}deg";
		_sliderMargeToujoursVisible.ValueChanged += (_) => _labelMargeToujoursVisibleValeur.Text = $"{_sliderMargeToujoursVisible.Value:0}";
		_sliderMaxChunksFrame.ValueChanged += (_) => _labelMaxChunksFrameValeur.Text = $"{_sliderMaxChunksFrame.Value:0}";
		_sliderLodEtapes.ValueChanged += (_) => _labelLodEtapesValeur.Text = $"{_sliderLodEtapes.Value:0}";
		_sliderRenderDistance.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonQualiteProche.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderDetailChunks.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonGazon.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonBuissons.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderRayonHorizon.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderPasHorizon.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderAngleCulling.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderMargeToujoursVisible.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderMaxChunksFrame.ValueChanged += (_) => SurControleGraphiqueModifie();
		_sliderLodEtapes.ValueChanged += (_) => SurControleGraphiqueModifie();
		_checkActiverHorizon.Toggled += (_) => SurControleGraphiqueModifie();
		_checkActiverCulling.Toggled += (_) => SurControleGraphiqueModifie();
		_checkLodUltraSmooth.Toggled += (_) => SurControleGraphiqueModifie();
		_checkModeSurvieAgressif.Toggled += (_) => SurControleGraphiqueModifie();

		_labelModeEditionGraphique = new Label { Text = "Mode: PAUSE" };
		_labelAutoHybride = new Label { Text = "Auto hybride inactif." };
		racine.AddChild(_labelModeEditionGraphique);
		racine.AddChild(_labelAutoHybride);

		var boutons = new HBoxContainer();
		boutons.AddThemeConstantOverride("separation", 8);
		var btnAuto = new Button { Text = "Auto hybride" };
		btnAuto.Pressed += LancerAutoHybrideGraphique;
		var btnAppliquer = new Button { Text = "Appliquer" };
		btnAppliquer.Pressed += () =>
		{
			_autoHybrideActif = false;
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData lus = LireOptionsDepuisPanel();
			AppliquerOptionsGraphiques(lus, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			_labelAutoHybride.Text = "Reglages appliques.";
		};
		var btnAppliquerMicroReload = new Button { Text = "Appliquer + micro reload" };
		btnAppliquerMicroReload.Pressed += () =>
		{
			_autoHybrideActif = false;
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData lus = LireOptionsDepuisPanel();
			AppliquerOptionsGraphiques(lus, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			ForcerMicroReloadGraphiqueMaintenant();
			_labelAutoHybride.Text = "Reglages appliques + micro reload force.";
		};
		var btnReset = new Button { Text = "Reset (Moyen)" };
		btnReset.Pressed += () =>
		{
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData preset = GraphicsOptionsService.ConstruirePreset(PresetGraphique.Moyen, CapturerOptionsGraphiquesCourantes(PresetGraphique.Moyen));
			AppliquerOptionsGraphiques(preset, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			_labelAutoHybride.Text = "Preset moyen applique.";
		};
		var btnResetComplet = new Button { Text = "Reset complet (defaut projet)" };
		btnResetComplet.Pressed += () =>
		{
			ForcerControleUtilisateurSurGraphismes();
			GraphicsOptionsData defautProjet = (_optionsGraphiquesDefautProjet ?? CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise)).Clone();
			defautProjet.Preset = PresetGraphique.Personnalise;
			AppliquerOptionsGraphiques(defautProjet, sauvegarder: true, synchroniserUi: true, prioriteChargementStreamApresReglageManuel: true);
			ForcerMicroReloadGraphiqueMaintenant();
			_labelAutoHybride.Text = "Reset complet applique (defaut projet).";
		};
		var btnFermer = new Button { Text = "Fermer" };
		btnFermer.Pressed += () =>
		{
			_panelGraphismes.Visible = false;
			_editionGraphiqueEnDirect = false;
			ForcerCycleSolaireActif();
			RafraichirIndicateurModeEditionGraphique();
			if (_panelPause != null)
				_panelPause.Visible = true;
			GetTree().Paused = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
		};
		boutons.AddChild(btnAuto);
		boutons.AddChild(btnAppliquer);
		boutons.AddChild(btnAppliquerMicroReload);
		boutons.AddChild(btnReset);
		boutons.AddChild(btnResetComplet);
		boutons.AddChild(btnFermer);
		racine.AddChild(boutons);

		layer.AddChild(_panelGraphismes);
		SynchroniserPanelGraphiqueDepuisOptions(CapturerOptionsGraphiquesCourantes(_optionsGraphiquesActuelles?.Preset ?? PresetGraphique.Personnalise));
		RafraichirIndicateurModeEditionGraphique();
	}

	private void AppliquerPresetDepuisUI()
	{
		if (_synchronisationUiGraphiqueEnCours)
			return;
		if (_optionPresetGraphique == null || _optionPresetGraphique.Selected < 0)
			return;
		PresetGraphique preset = (PresetGraphique)_optionPresetGraphique.GetItemId(_optionPresetGraphique.Selected);
		if (preset == PresetGraphique.Personnalise)
			return;
		ForcerControleUtilisateurSurGraphismes();
		GraphicsOptionsData baseOptions = CapturerOptionsGraphiquesCourantes(preset);
		GraphicsOptionsData p = GraphicsOptionsService.ConstruirePreset(preset, baseOptions);
		AppliquerOptionsGraphiques(p, sauvegarder: false, synchroniserUi: true);
		if (_mondeClient != null)
			_mondeClient.SignalerGraceStreamingApresReglageManuel();
		if (_labelAutoHybride != null)
			_labelAutoHybride.Text = $"Preset {preset} previsualise. Clique Appliquer pour sauvegarder.";
	}

	private void SurControleGraphiqueModifie()
	{
		if (_synchronisationUiGraphiqueEnCours)
			return;
		ForcerControleUtilisateurSurGraphismes();
		ForcerCycleSolaireActif();
		GraphicsOptionsData previsualisation = LireOptionsDepuisPanel();
		AppliquerOptionsGraphiques(previsualisation, sauvegarder: false, synchroniserUi: false);
		// Même hors mode LIVE : sans grâce streaming, ModeSurvieFpsAgressif plafonnait le radar et la distance de rendu « ne marchait pas ».
		if (_mondeClient != null)
			_mondeClient.SignalerGraceStreamingApresReglageManuel();
		if (_optionPresetGraphique != null)
		{
			int idx = _optionPresetGraphique.GetItemIndex((int)PresetGraphique.Personnalise);
			if (idx >= 0)
				_optionPresetGraphique.Select(idx);
		}
		if (_labelAutoHybride != null)
			_labelAutoHybride.Text = "Previsualisation active (non sauvegardee).";
	}

	private GraphicsOptionsData LireOptionsDepuisPanel()
	{
		return GraphicsOptionsService.Normaliser(new GraphicsOptionsData
		{
			Preset = PresetGraphique.Personnalise,
			RenderDistance = Mathf.RoundToInt((float)_sliderRenderDistance.Value),
			RenderDistanceDetailChunks = Mathf.RoundToInt((float)_sliderDetailChunks.Value),
			RayonQualiteProcheChunks = Mathf.RoundToInt((float)_sliderRayonQualiteProche.Value),
			RayonGazonVisibleChunks = Mathf.RoundToInt((float)_sliderRayonGazon.Value),
			RayonBuissonsVisibleChunks = Mathf.RoundToInt((float)_sliderRayonBuissons.Value),
			ActiverHorizonLod = _checkActiverHorizon.ButtonPressed,
			RayonHorizonChunks = Mathf.RoundToInt((float)_sliderRayonHorizon.Value),
			PasHorizonMetres = (float)_sliderPasHorizon.Value,
			ActiverCullingCameraChunks = _checkActiverCulling.ButtonPressed,
			AngleCullingCameraDeg = (float)_sliderAngleCulling.Value,
			MargeChunksToujoursVisibles = Mathf.RoundToInt((float)_sliderMargeToujoursVisible.Value),
			MaxChunksParFrame = Mathf.RoundToInt((float)_sliderMaxChunksFrame.Value),
			LODTextureEtapes = Mathf.RoundToInt((float)_sliderLodEtapes.Value),
			ProfilLodCinematiqueUltraSmooth = _checkLodUltraSmooth.ButtonPressed,
			ModeSurvieFpsAgressif = _checkModeSurvieAgressif.ButtonPressed,
			FpsCibleAutoDiagnostic = _optionsGraphiquesActuelles?.FpsCibleAutoDiagnostic ?? 60,
			SeuilFpsUrgenceForte = _optionsGraphiquesActuelles?.SeuilFpsUrgenceForte ?? 42,
			SeuilFpsUrgenceCritique = _optionsGraphiquesActuelles?.SeuilFpsUrgenceCritique ?? 30,
			SeuilFpsUrgenceExtreme = _optionsGraphiquesActuelles?.SeuilFpsUrgenceExtreme ?? 24,
			SeuilFpsSortieUrgenceExtreme = _optionsGraphiquesActuelles?.SeuilFpsSortieUrgenceExtreme ?? 56
		});
	}

	private void SynchroniserPanelGraphiqueDepuisOptions(GraphicsOptionsData options)
	{
		if (_panelGraphismes == null)
			return;
		GraphicsOptionsData o = GraphicsOptionsService.Normaliser(options?.Clone() ?? CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise));
		_synchronisationUiGraphiqueEnCours = true;
		if (_optionPresetGraphique != null)
		{
			int idx = _optionPresetGraphique.GetItemIndex((int)o.Preset);
			int idxSel = idx >= 0 ? idx : _optionPresetGraphique.GetItemIndex((int)PresetGraphique.Personnalise);
			// Select() émet ItemSelected (souvent en différé) : sans blocage, AppliquerPresetDepuisUI réécrit tout le monde avec le preset et annule les curseurs.
			_optionPresetGraphique.SetBlockSignals(true);
			_optionPresetGraphique.Select(idxSel);
			_optionPresetGraphique.SetBlockSignals(false);
		}
		_sliderRenderDistance.SetValueNoSignal(o.RenderDistance);
		_sliderRayonQualiteProche.SetValueNoSignal(o.RayonQualiteProcheChunks);
		_sliderDetailChunks.MaxValue = o.RenderDistance;
		_sliderDetailChunks.SetValueNoSignal(Mathf.Clamp(o.RenderDistanceDetailChunks, 6, o.RenderDistance));
		_sliderRayonGazon.SetValueNoSignal(o.RayonGazonVisibleChunks);
		_sliderRayonBuissons.SetValueNoSignal(o.RayonBuissonsVisibleChunks);
		_sliderRayonHorizon.SetValueNoSignal(o.RayonHorizonChunks);
		_sliderPasHorizon.SetValueNoSignal(o.PasHorizonMetres);
		_sliderAngleCulling.SetValueNoSignal(o.AngleCullingCameraDeg);
		_sliderMargeToujoursVisible.SetValueNoSignal(o.MargeChunksToujoursVisibles);
		_sliderMaxChunksFrame.SetValueNoSignal(o.MaxChunksParFrame);
		_sliderLodEtapes.SetValueNoSignal(o.LODTextureEtapes);
		_checkActiverHorizon.ButtonPressed = o.ActiverHorizonLod;
		_checkActiverCulling.ButtonPressed = o.ActiverCullingCameraChunks;
		_checkLodUltraSmooth.ButtonPressed = o.ProfilLodCinematiqueUltraSmooth;
		_checkModeSurvieAgressif.ButtonPressed = o.ModeSurvieFpsAgressif;

		_labelRenderDistanceValeur.Text = $"{o.RenderDistance}";
		_labelRayonQualiteProcheValeur.Text = $"{o.RayonQualiteProcheChunks}";
		_labelDetailChunksValeur.Text = $"{o.RenderDistanceDetailChunks}";
		_labelRayonGazonValeur.Text = $"{o.RayonGazonVisibleChunks}";
		_labelRayonBuissonsValeur.Text = $"{o.RayonBuissonsVisibleChunks}";
		_labelRayonHorizonValeur.Text = $"{o.RayonHorizonChunks}";
		_labelPasHorizonValeur.Text = $"{o.PasHorizonMetres:0}m";
		_labelAngleCullingValeur.Text = $"{o.AngleCullingCameraDeg:0}deg";
		_labelMargeToujoursVisibleValeur.Text = $"{o.MargeChunksToujoursVisibles}";
		_labelMaxChunksFrameValeur.Text = $"{o.MaxChunksParFrame}";
		_labelLodEtapesValeur.Text = $"{o.LODTextureEtapes}";
		_synchronisationUiGraphiqueEnCours = false;
	}

	private void ToggleMenuPause()
	{
		if (_panelPause == null) CreerMenuPause();
		_pauseVisible = !_pauseVisible;
		_panelPause.Visible = _pauseVisible;
		if (!_pauseVisible && _panelGraphismes != null)
		{
			_panelGraphismes.Visible = false;
			_editionGraphiqueEnDirect = false;
		}
		RafraichirIndicateurModeEditionGraphique();
		GetTree().Paused = _pauseVisible;
		Input.MouseMode = _pauseVisible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
	}
}
