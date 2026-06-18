using Godot;

/// <summary>
/// Chargement / capture / application des options graphiques, profil auto-hybride et helpers materiel.
/// Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: derivation des reglages client/serveur a partir de RenderDistance et persistance options identiques a l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void ChargerOptionsGraphiquesAuDemarrage()
	{
		var defaut = (_optionsGraphiquesDefautProjet ?? CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise)).Clone();
		if (IgnorerFichierOptionsGraphiquesAuDemarrage)
		{
			_optionsGraphiquesChargeesUtilisateur = false;
			AppliquerOptionsGraphiques(defaut, sauvegarder: false, synchroniserUi: false);
			return;
		}
		_optionsGraphiquesChargeesUtilisateur = FileAccess.FileExists("user://options_graphics.cfg");
		GraphicsOptionsData chargees = GraphicsOptionsService.ChargerOuDefaut(defaut);
		AppliquerOptionsGraphiques(chargees, sauvegarder: false, synchroniserUi: false);
	}

	private GraphicsOptionsData CapturerOptionsGraphiquesCourantes(PresetGraphique preset)
	{
		return GraphicsOptionsService.Normaliser(new GraphicsOptionsData
		{
			Preset = preset,
			RenderDistance = RenderDistance,
			RenderDistanceDetailChunks = RenderDistanceDetailChunks,
			RayonQualiteProcheChunks = RayonQualiteProcheChunks,
			RayonGazonVisibleChunks = RayonGazonVisibleChunks,
			RayonBuissonsVisibleChunks = RayonBuissonsVisibleChunks,
			ActiverHorizonLod = ActiverHorizonLod,
			RayonHorizonChunks = RayonHorizonChunks,
			PasHorizonMetres = PasHorizonMetres,
			ActiverCullingCameraChunks = ActiverCullingCameraChunks,
			AngleCullingCameraDeg = AngleCullingCameraDeg,
			MargeChunksToujoursVisibles = MargeChunksToujoursVisibles,
			MaxChunksParFrame = MaxChunksParFrame,
			LODTextureEtapes = _mondeClient?.LODTextureEtapes ?? 12,
			ProfilLodCinematiqueUltraSmooth = _mondeClient?.ProfilLodCinematiqueUltraSmooth ?? true,
			ModeSurvieFpsAgressif = _mondeClient?.ModeSurvieFpsAgressif ?? true,
			FpsCibleAutoDiagnostic = _mondeClient?.FpsCibleAutoDiagnostic ?? 60,
			SeuilFpsUrgenceForte = _mondeClient?.SeuilFpsUrgenceForte ?? 42,
			SeuilFpsUrgenceCritique = _mondeClient?.SeuilFpsUrgenceCritique ?? 30,
			SeuilFpsUrgenceExtreme = _mondeClient?.SeuilFpsUrgenceExtreme ?? 24,
			SeuilFpsSortieUrgenceExtreme = _mondeClient?.SeuilFpsSortieUrgenceExtreme ?? 56,
			QualiteEclairage = _optionsGraphiquesActuelles?.QualiteEclairage ?? QualiteEclairageAaa.Ultra
		});
	}

	private void RestaurerParametresMondeClientNonExposesUtilisateur(bool modeProtectionFps)
	{
		if (_mondeClient == null)
			return;
		// On rétablit ces paramètres même après un ancien profil matériel.
		_mondeClient.MaxLancementsTravailleursParTick = modeProtectionFps ? 2 : 6;
		_mondeClient.BudgetFrameCibleMs = modeProtectionFps ? 16.2f : 22f;
		// 50 FPS de gel était trop agressif (beaucoup de configs restent 45–55) ; hors survie : gate désactivé via ForcerModeStreaming.
		_mondeClient.SeuilFpsGateStrict = modeProtectionFps ? 40f : 28f;
		_mondeClient.SeuilFpsGateReprise = modeProtectionFps ? 52f : 34f;
		_mondeClient.DureeStabiliteReprise = modeProtectionFps ? 0.20f : 0.12f;
		_mondeClient.DureeRampUpPostDegel = modeProtectionFps ? 0.55f : 0.18f;
		_mondeClient.DureeMinEtatGeleSec = modeProtectionFps ? 0.15f : 0.08f;
		_mondeClient.DureeMinEtatOuvertSec = modeProtectionFps ? 0.45f : 0.10f;
		_mondeClient.MaxChunksEvaluesCullingParPasse = modeProtectionFps ? 240 : 900;
		_mondeClient.MaxBasculesCullingParPasse = modeProtectionFps ? 96 : 300;
	}

	private void AppliquerOptionsGraphiques(GraphicsOptionsData options, bool sauvegarder, bool synchroniserUi, bool prioriteChargementStreamApresReglageManuel = false)
	{
		GraphicsOptionsData o = GraphicsOptionsService.Normaliser(options?.Clone() ?? new GraphicsOptionsData());
		int ancienRenderDistance = RenderDistance;
		RenderDistance = o.RenderDistance;
		RenderDistanceDetailChunks = o.RenderDistanceDetailChunks;
		RayonQualiteProcheChunks = o.RayonQualiteProcheChunks;
		RayonGazonVisibleChunks = o.RayonGazonVisibleChunks;
		RayonBuissonsVisibleChunks = o.RayonBuissonsVisibleChunks;
		ActiverHorizonLod = o.ActiverHorizonLod;
		RayonHorizonChunks = o.RayonHorizonChunks;
		PasHorizonMetres = o.PasHorizonMetres;
		ActiverCullingCameraChunks = o.ActiverCullingCameraChunks;
		AngleCullingCameraDeg = o.AngleCullingCameraDeg;
		MargeChunksToujoursVisibles = o.MargeChunksToujoursVisibles;
		MaxChunksParFrame = o.MaxChunksParFrame;

		if (_serveurParDimension.Count > 0)
		{
			bool modeProtectionFps = o.ModeSurvieFpsAgressif;
			foreach (var kv in _serveurParDimension)
			{
				Monde_Serveur serveur = kv.Value;
				if (serveur == null) continue;
				serveur.RenderDistance = RenderDistance;
				if (modeProtectionFps)
				{
					serveur.MultiplicateurCharge = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 18f), 1, 3);
					serveur.MaxDemandesChunksParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 12f), 2, 10);
					serveur.MaxIntegrationsWorkersParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 18f), 2, 6);
					serveur.MaxChunksEnvoiParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 8f), 8, 20);
				}
				else
				{
					// Priorité joueur : laisse les grosses distances pousser réellement le streaming.
					serveur.MultiplicateurCharge = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 10f), 2, 8);
					serveur.MaxDemandesChunksParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 2.5f), 8, 48);
					serveur.MaxIntegrationsWorkersParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 6f), 4, 16);
					serveur.MaxChunksEnvoiParTick = Mathf.Clamp(Mathf.RoundToInt(RenderDistance / 1.5f), 12, 80);
				}
			}
		}

		if (_mondeClient != null)
		{
			_mondeClient.RenderDistance = RenderDistance;
			_mondeClient.RenderDistanceDetailChunks = RenderDistanceDetailChunks;
			_mondeClient.RayonQualiteMaxChunks = RayonQualiteProcheChunks;
			_mondeClient.RayonGazonVisibleChunks = RayonGazonVisibleChunks;
			_mondeClient.RayonBuissonsVisibleChunks = RayonBuissonsVisibleChunks;
			_mondeClient.ActiverHorizonLod = ActiverHorizonLod;
			_mondeClient.RayonHorizonChunks = RayonHorizonChunks;
			_mondeClient.PasHorizonMetres = PasHorizonMetres;
			_mondeClient.ActiverCullingCameraChunks = ActiverCullingCameraChunks;
			_mondeClient.AngleCullingCameraDeg = AngleCullingCameraDeg;
			_mondeClient.MargeChunksToujoursVisibles = MargeChunksToujoursVisibles;
			_mondeClient.MaxChunksParFrame = MaxChunksParFrame;
			_mondeClient.LODTextureEtapes = o.LODTextureEtapes;
			_mondeClient.ProfilLodCinematiqueUltraSmooth = o.ProfilLodCinematiqueUltraSmooth;
			_mondeClient.ModeSurvieFpsAgressif = o.ModeSurvieFpsAgressif;
			_mondeClient.FpsCibleAutoDiagnostic = o.FpsCibleAutoDiagnostic;
			_mondeClient.SeuilFpsUrgenceForte = o.SeuilFpsUrgenceForte;
			_mondeClient.SeuilFpsUrgenceCritique = o.SeuilFpsUrgenceCritique;
			_mondeClient.SeuilFpsUrgenceExtreme = o.SeuilFpsUrgenceExtreme;
			_mondeClient.SeuilFpsSortieUrgenceExtreme = o.SeuilFpsSortieUrgenceExtreme;
			_mondeClient.MaxAjoutsRadarParPasse = o.ModeSurvieFpsAgressif
				? Mathf.Clamp(480 + RenderDistance * 8, 520, 2000)
				: Mathf.Clamp(1200 + RenderDistance * 40, 1600, 8000);
			// D’abord aligner gate / diagnostic / rayon requêtes sur le choix utilisateur, puis réglages dérivés et horizon.
			// Avant : Reappliquer puis Forcer → une frame pouvait laisser le gel actif alors que « Sauver les FPS » était décoché.
			_mondeClient.ForcerModeStreamingUtilisateur(o.ModeSurvieFpsAgressif);
			RestaurerParametresMondeClientNonExposesUtilisateur(o.ModeSurvieFpsAgressif);
			_mondeClient.ReappliquerReglagesGraphiquesRuntime();
			_mondeClient.ForcerRafraichissementStreamingGraphique(microReload: true);
			if (_joueur != null && RenderDistance > ancienRenderDistance)
			{
				Vector2I chunkActuel = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
				_mondeClient.ReserverChunkSpawnPrioritaire(chunkActuel);
				_mondeClient.ImpulserConvergenceVersRenderDistance();
			}
			// Décocher « Sauver les FPS » : grâce streaming pour débloquer tout de suite la distance mesurée (même sans bouton Appliquer dédié).
			if (!o.ModeSurvieFpsAgressif || prioriteChargementStreamApresReglageManuel)
				_mondeClient.SignalerGraceStreamingApresReglageManuel();
		}

		if (_joueur is Joueur joueurHumain)
			joueurHumain.ConfigurerFarClipPourRenderDistance(RenderDistance, TailleChunk);

		var cycleSolaire = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		cycleSolaire?.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
		AppliquerProfilEclairageAaa(o.QualiteEclairage);
		if (_mondeServeurAbysse is Gestionnaire_Abysse gestionnaireAbysseDistance)
			gestionnaireAbysseDistance.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);

		// Mode legacy : pas de Monde_Client — il faut rafraîchir la grille de chunks sinon RenderDistance ne bouge jamais tant qu’on ne change pas de chunk.
		if (!UseArchitectureReseau)
			ActualiserVisibiliteEtTriChunksLegacy();

		_optionsGraphiquesActuelles = o.Clone();
		if (synchroniserUi)
			SynchroniserPanelGraphiqueDepuisOptions(_optionsGraphiquesActuelles);
		if (sauvegarder)
		{
			GraphicsOptionsService.Sauvegarder(_optionsGraphiquesActuelles);
			_optionsGraphiquesChargeesUtilisateur = true;
		}
	}

	/// <summary>SSAO / SSIL / SDFGI sur le WorldEnvironment racine (surface).</summary>
	private void AppliquerProfilEclairageAaa(QualiteEclairageAaa qualite)
	{
		var we = GetParent()?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (we?.Environment == null)
			return;
		ProfilEclairageAAA.Appliquer(we.Environment, qualite);
		GD.Print($"SEROKA ÉCLAIRAGE : profil AAA {qualite} appliqué (SSAO/SSIL/SDFGI selon niveau).");
	}

	private void LancerAutoHybrideGraphique()
	{
		ForcerControleUtilisateurSurGraphismes();
		MettreAJourInfosMaterielDetecte();
		GraphicsOptionsData baseOptions = CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise);
		GraphicsOptionsData seed = GraphicsOptionsService.GenererBaseAutoMateriel(_nomCpuDetecte, _nomGpuDetecte, baseOptions);
		AppliquerOptionsGraphiques(seed, sauvegarder: true, synchroniserUi: true);
		_autoHybrideActif = true;
		_timerSessionAutoHybride = 0f;
		_timerAjustementAutoHybride = 0f;
		_fpsMinSessionAutoHybride = float.MaxValue;
		if (_labelAutoHybride != null)
			_labelAutoHybride.Text = "Auto hybride: analyse en cours...";
	}

	private void TraiterAutoHybrideGraphique(float dt)
	{
		if (!_autoHybrideActif || _mondeClient == null)
			return;
		if (_pauseVisible)
			return;

		_timerSessionAutoHybride += dt;
		_timerAjustementAutoHybride += dt;
		float fpsMoyen = _mondeClient.LireFpsMoyenAutoDiagnostic();
		_fpsMinSessionAutoHybride = Mathf.Min(_fpsMinSessionAutoHybride, fpsMoyen);

		const float intervalleAjustement = 4f;
		const float dureeSession = 18f;
		if (_timerAjustementAutoHybride >= intervalleAjustement)
		{
			_timerAjustementAutoHybride = 0f;
			GraphicsOptionsData ajuste = GraphicsOptionsService.AjusterSelonFps(
				CapturerOptionsGraphiquesCourantes(PresetGraphique.Personnalise),
				fpsMoyen,
				_fpsMinSessionAutoHybride);
			AppliquerOptionsGraphiques(ajuste, sauvegarder: true, synchroniserUi: true);
		}

		if (_timerSessionAutoHybride >= dureeSession)
		{
			_autoHybrideActif = false;
			if (_labelAutoHybride != null)
				_labelAutoHybride.Text = $"Auto hybride termine (FPS moyen {fpsMoyen:0}).";
		}
	}

	private void MettreAJourInfosMaterielDetecte()
	{
		_nomCpuDetecte = OS.GetProcessorName()?.ToLowerInvariant() ?? "";
		try
		{
			_nomGpuDetecte = RenderingServer.GetVideoAdapterName().ToLowerInvariant();
		}
		catch
		{
			_nomGpuDetecte = "";
		}
	}

	private void RafraichirIndicateurModeEditionGraphique()
	{
		if (_labelModeEditionGraphique == null)
			return;
		if (_editionGraphiqueEnDirect)
			_labelModeEditionGraphique.Text = "Mode: LIVE (application en direct)";
		else if (_pauseVisible)
			_labelModeEditionGraphique.Text = "Mode: PAUSE";
		else
			_labelModeEditionGraphique.Text = "Mode: JEU";
	}

	private void ForcerMicroReloadGraphiqueMaintenant()
	{
		if (_mondeClient == null)
			return;
		_mondeClient.ForcerRafraichissementStreamingGraphique(microReload: true);
		if (_joueur != null)
		{
			Vector2I chunkActuel = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
			_mondeClient.ReserverChunkSpawnPrioritaire(chunkActuel);
		}
		ForcerCycleSolaireActif();
	}

	private void ForcerControleUtilisateurSurGraphismes()
	{
		_verrouProfilMaterielUtilisateur = true;
		_optionsGraphiquesChargeesUtilisateur = true;
		ActiverProfilMaterielAuto = false;
		ForcerProfilGTX1060i710700F = false;
	}

	private void ForcerCycleSolaireActif()
	{
		var cycle = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		cycle?.DefinirChargementMondeActif(false);
	}
}
