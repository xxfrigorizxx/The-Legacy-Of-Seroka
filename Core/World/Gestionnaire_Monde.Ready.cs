using Godot;

/// <summary>
/// Initialisation du monde (<see cref="_Ready"/>) : coordinateurs, HUD, options graphiques, spawn/reconnexion,
/// demarrage reseau/legacy et overlays de chargement. Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: ordre exact des etapes d'initialisation (incluant le spawn et le demarrage reseau) identique a l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
			TreeExiting += EssayerSauvegardeCompleteAvantSortieScene;
		_dimensionCoordinator = new DimensionCoordinator(
			AppliquerChangementDimensionLocale,
			TransfererPeerVersDimension,
			MettreAJourSuspensionServeursDimensions);
		_worldLifecycleBootstrap = new WorldLifecycleBootstrap(
			AssurerCalquesHudInventaireEtCarnet,
			CreerRepereCentreEcran,
			CreerOverlayEmerukedesiParotaromaStage1,
			AssurerOverlayPortailTransition,
			InitialiserWarmupShadersProgressif);
		DirAccess.MakeDirRecursiveAbsolute("user://chunks");
		_joueur = GetParent().GetNode<CharacterBody3D>("Joueur");
		// F5 / lancement direct : GameState reste sur « MonMonde » par défaut alors que les sauvegardes sont dans le dernier monde du menu.
		GameState.Instance?.AppliquerDernierMondeJoueSiChargementDirectVersMondeZero();
		// Aligner la seed exportée de la scène sur le monde chargé (évite spawn / outils basés sur 19847 alors que le terrain utilise GameState).
		if (GameState.Instance != null)
			SeedTerrain = GameState.Instance.SeedTerrainActuel;
		// Dernier monde joué = celui dont on charge les sauvegardes (évite F5 / reprise sur le mauvais dossier).
		GameState.Instance?.PublierMondeActuelCommeDernierJoueSurDisque();
		_worldLifecycleBootstrap.AssurerCalquesHudInventaireEtCarnet();
		Chunk_Client.EchelleGazon = EchelleGazon;
		_optionsGraphiquesDefautProjet = CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise);
		ChargerOptionsGraphiquesAuDemarrage();

		// Affichage des coordonnées en haut au centre
		var canvas = new CanvasLayer { Layer = 10 };
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop, false);
		panel.OffsetLeft = -70;
		panel.OffsetTop = 8;
		panel.OffsetRight = 70;
		panel.OffsetBottom = 36;
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0, 0, 0, 0.6f);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(6);
		panel.AddThemeStyleboxOverride("panel", style);
		_labelCoords = new Label();
		_labelCoords.AddThemeFontSizeOverride("font_size", 14);
		_labelCoords.HorizontalAlignment = HorizontalAlignment.Center;
		panel.AddChild(_labelCoords);

		// Horloge dimension active en haut à droite (diagnostic temps 1:1 / fuseaux).
		var panelHeure = new PanelContainer();
		panelHeure.SetAnchorsPreset(Control.LayoutPreset.TopRight, false);
		panelHeure.OffsetLeft = -240f;
		panelHeure.OffsetTop = 8f;
		panelHeure.OffsetRight = -12f;
		panelHeure.OffsetBottom = 36f;
		var styleHeure = new StyleBoxFlat();
		styleHeure.BgColor = new Color(0f, 0f, 0f, 0.6f);
		styleHeure.SetCornerRadiusAll(4);
		styleHeure.SetContentMarginAll(6);
		panelHeure.AddThemeStyleboxOverride("panel", styleHeure);
		_labelHeureDimension = new Label();
		_labelHeureDimension.AddThemeFontSizeOverride("font_size", 14);
		_labelHeureDimension.HorizontalAlignment = HorizontalAlignment.Right;
		panelHeure.AddChild(_labelHeureDimension);
		AddChild(canvas);
		canvas.AddChild(panel);
		canvas.AddChild(panelHeure);
		_worldLifecycleBootstrap.InitialiserOverlaysEtReperes();

		// Position : chargée si monde existant, sinon spawn par défaut (terrain généré → joueur déposé)
		Vector3 posSpawn = _joueur.GlobalPosition;
		int dimensionReconnexion = (int)DimensionJeu.Alpha;
		var sessionSauvegardee = ChargerSessionJoueur();
		if (sessionSauvegardee.HasValue)
		{
			dimensionReconnexion = sessionSauvegardee.Value.DimensionId;
			posSpawn = sessionSauvegardee.Value.Position;
			GD.Print($"ZERO-K : Session joueur restaurée dimension={dimensionReconnexion} pos={posSpawn}");
		}
		var posSauvegardee = GameState.Instance?.ObtenirPositionJoueurSauvegardee();
		bool positionPersistanteConnue = sessionSauvegardee.HasValue || posSauvegardee.HasValue;
		_spawnDoitEtreAligneAuSol = !positionPersistanteConnue && ForcerAlignementSolAuChargement;
		_spawnAligneAuSol = !_spawnDoitEtreAligneAuSol;
		_ajusterPiedsJoueurSurSurfaceApresRestauration = positionPersistanteConnue;
		if (sessionSauvegardee.HasValue)
		{
			GD.Print($"ZERO-K : Reconnexion joueur à {posSpawn} (dimension {dimensionReconnexion})");
		}
		else if (posSauvegardee.HasValue)
		{
			posSpawn = posSauvegardee.Value;
			GD.Print($"ZERO-K : Joueur reconnecté à {posSpawn}");
		}
		else
		{
			// Nouveau monde: spawn déterministe basé sur la seed (et pas uniquement la position fixe de la scène).
			double offsetLocal;
			double distanceHeures;
			dimensionReconnexion = SelectionnerDimensionInitialeParFuseauReel(out offsetLocal, out distanceHeures);
			_dimensionLocaleActive = dimensionReconnexion;
			DefinirDimensionPeer(Multiplayer.GetUniqueId(), _dimensionLocaleActive);
			string nomDimension = ConstantesDimensions.ObtenirNomCanonique(dimensionReconnexion);
			GD.Print($"ZERO-K : Spawn initial dimension={nomDimension} (id={dimensionReconnexion}) offsetLocal={offsetLocal:0.##}h ecart={distanceHeures:0.##}h");
			posSpawn = CalculerSpawnInitialDepuisSeed();
			GD.Print($"ZERO-K : Spawn initial seed={SeedTerrain} -> {posSpawn}");
		}
		posSpawn = AssurerSpawnAuDessusDuSol(posSpawn, conserverHauteurSauvegardee: positionPersistanteConnue);
		_joueur.GlobalPosition = posSpawn;
		_spawnInitialEnAttente = posSpawn;
		if (_spawnDoitEtreAligneAuSol)
			_joueur.Visible = false; // Apparaît seulement après alignement raycast sur le sol.

		if (UseArchitectureReseau)
		{
			if (sessionSauvegardee.HasValue)
				_dimensionLocaleActive = dimensionReconnexion;
			DemarrerArchitectureReseau();
			// Reconnexion : si la dernière dimension active n'est pas Alpha (déjà l'état par défaut au boot)
			// et qu'elle existe bien dans nos serveurs, on bascule dessus à la même position. Couvre Abysse + Beta/Omega/Delta.
			if (sessionSauvegardee.HasValue
				&& dimensionReconnexion != (int)DimensionJeu.Alpha
				&& _serveurParDimension.ContainsKey(dimensionReconnexion))
			{
				string nomCanonique = ConstantesDimensions.ObtenirNomCanonique(dimensionReconnexion);
				// Ne pas recharger placed_objects ici : Gestionnaire_Monde._Ready s'exécute avant Joueur._Ready,
				// le terrain n'est pas prêt, et RechargerEtatPersistantDimensionActive poserait _persistantObjetsSolCharges
				// ce qui empêche la phase B (EssayerRestaurerObjetsPersistantsPhaseSol) de respawner les constructions.
				_dimensionCoordinator.AppliquerChangementDimensionLocale(dimensionReconnexion, posSpawn, $"Reconnexion en {nomCanonique}.", rechargerPersistanceDimension: false);
			}
		}
		else
		{
			DemarrerLegacy();
		}

		if (PreGenererAuDemarrage)
			_ = PreGenererMonde(RayonPreGeneration);

		CreerMenuPause();

		// Overlay "Chargement du monde..." — empêche de traverser le sol avant que la collision soit prête
		_overlayChargement = new CanvasLayer { Layer = 50 };
		var panelChargement = new PanelContainer();
		panelChargement.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var styleChargement = new StyleBoxFlat();
		styleChargement.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
		styleChargement.SetCornerRadiusAll(8);
		styleChargement.SetContentMarginAll(24);
		panelChargement.AddThemeStyleboxOverride("panel", styleChargement);
		var lblChargement = new Label { Text = "Chargement du monde...", HorizontalAlignment = HorizontalAlignment.Center };
		lblChargement.AddThemeFontSizeOverride("font_size", 22);
		_labelChargementPrincipal = lblChargement;
		panelChargement.AddChild(lblChargement);
		_overlayChargement.AddChild(panelChargement);
		AddChild(_overlayChargement);
		_secondesOverlayChargement = 0;
		// Forge automatique du matériau eau (bypass de l'éditeur) — sanctuarisation : le GC ne le détruira pas car lié au nœud.
		var shaderEau = GD.Load<Shader>("res://EauTriplanar.gdshader");
		if (shaderEau != null)
		{
			var matEau = new ShaderMaterial();
			matEau.Shader = shaderEau;
			matEau.SetShaderParameter("albedo_color", new Color(0.1f, 0.3f, 0.6f, 0.6f));
			MaterielEau = matEau;
		}
		if (UseArchitectureReseau)
			_worldLifecycleBootstrap.InitialiserWarmupShadersProgressif();

		CallDeferred(nameof(RestaurerEtatPersistantMonde));
	}
}
