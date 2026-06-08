using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	private int RayonDetailChunksActif()
	{
		int max = Mathf.Max(2, RenderDistance);
		int detail = Mathf.Clamp(RenderDistanceDetailChunks, 2, max);
		return Mathf.Min(detail, max);
	}

	/// <summary>Rayon de chargement réseau/terrain réel. Indépendant du rayon de détail visuel.</summary>
	private int RayonChargementChunksActif()
	{
		// Respecte le slider (jusqu'à 2 chunks) : la dormance physique ne doit pas forcer 6+ chunks de visuel.
		int rendu = Mathf.Max(2, RenderDistance);
		if (!ModeSurvieFpsAgressif && RenderDistance > RayonDormancePhysique)
			rendu = Mathf.Max(rendu, RayonDormancePhysique + 1);
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
			rendu = Mathf.Min(rendu, JoueurEnModeVolCreatif() ? 6 : 10);
		else if (ModeProfondeurTranchesActif())
			rendu = Mathf.Min(rendu, Mathf.Max(RayonDormancePhysique + MargePreloadChunks + 2, PlafondRayonChargementProfondeurChunks));
		if (JoueurEnModeVolCreatif() && ModeProfondeurTranchesActif())
			rendu = Mathf.Min(rendu, 8);
		return Mathf.Max(rendu, RayonDetailChunksActif());
	}

	private int DemiFenetreTranchesStreamingActif()
	{
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef)
			&& ConstantesProfondeurVerticale.EstProcheJonctionTrancheMonde(joueurRef.GlobalPosition.Y))
			return Mathf.Max(1, ConstantesProfondeurVerticale.DemiFenetreTranches);
		// Noclip créatif : une tranche par défaut, sauf près d'une jonction (Y=100,200…) où ±1 est requis pour le voile MC.
		if (JoueurEnModeVolCreatif())
		{
			if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRefCreatif))
			{
				if (ConstantesProfondeurVerticale.EstProcheJonctionTrancheMonde(joueurRefCreatif.GlobalPosition.Y))
					return 1;
			}
			return 0;
		}
		float vy = _joueur?.Velocity.Y ?? 0f;
		// Survie : ±1 tranche (3 couches) au lieu de ±2 (5) — moins de chunks par colonne XZ.
		if (ModeSurvieFpsAgressif)
			return 1;
		return ConstantesProfondeurVerticale.DemiFenetreTranchesStreaming(vy);
	}

	/// <summary>Demi-côté (chunks) du disque « toujours visible » pour le culling caméra. Suit strictement le slider <see cref="RenderDistance"/> (2–64).</summary>
	private int DisqueToujoursVisibleChunksCulling()
		=> Mathf.Max(2, RenderDistance);

	/// <summary>
	/// Rayon (chunks) pour construire la file radar / purge : toujours la distance de chargement utilisateur (<see cref="RayonChargementChunksActif"/>).
	/// Anciennement lié à <c>_rayonRequetesActuel</c> en mode « Sauver les FPS », ce qui tronquait la file avant le slider RenderDistance
	/// (le panneau graphismes n’avait pas le contrôle absolu sur ce qui peut entrer en file).
	/// Le débit réseau / intégration reste limité par <c>_rayonRequetesActuel</c>, le gate FPS et <c>nbRequetes</c>.
	/// </summary>
	private int RayonRadarPreparationActif()
	{
		int minRadar = Mathf.Max(RayonDormancePhysique + 2, RayonInitialRequetesChunks);
		int cible = RayonChargementChunksActif();
		return Mathf.Max(minRadar, cible);
	}

	private void AppliquerParametresLodTextureTerrain()
	{
		if (!(MaterielTerrain is ShaderMaterial sm)) return;
		float detailMetres = RayonDetailChunksActif() * TailleChunk;
		float start = Mathf.Max(240f, Mathf.Max(15f, RayonDetailChunksActif() + 4f) * TailleChunk);
		if (ProfilLodCinematiqueUltraSmooth) start += TailleChunk * 4f;
		if (RenderDistance >= 36)
		{
			float k = Mathf.Clamp((RenderDistance - 32f) / 40f, 0f, 1f);
			start *= Mathf.Lerp(1f, 0.9f, k);
		}
		float end = start + Mathf.Max(1800f, Mathf.Max(RenderDistance, RayonHorizonChunks) * TailleChunk * 8f);
		float mip = ProfilLodCinematiqueUltraSmooth ? 2.8f : 3.4f;
		if (RenderDistance >= 36)
		{
			float k = Mathf.Clamp((RenderDistance - 32f) / 40f, 0f, 1f);
			mip = Mathf.Min(mip + 0.12f * k, 4.2f);
		}
		float blend = ProfilLodCinematiqueUltraSmooth ? 0.92f : 0.82f;
		float jitter = ProfilLodCinematiqueUltraSmooth ? 84f : 56f;
		float steps = Mathf.Clamp(LODTextureEtapes, 8, 24);

		sm.SetShaderParameter("lod_texture_start", start);
		sm.SetShaderParameter("lod_texture_end", end);
		sm.SetShaderParameter("lod_far_mip", mip);
		sm.SetShaderParameter("lod_steps", steps);
		sm.SetShaderParameter("lod_step_blend", blend);
		sm.SetShaderParameter("lod_start_jitter", jitter);
		_ = detailMetres; // garde explicite la base physique (chunks->mètres) pour future extension.
	}

	public void ReappliquerReglagesGraphiquesRuntime()
	{
		Chunk_Client.RayonQualiteMaxChunks = Mathf.Max(1, RayonQualiteMaxChunks);
		AppliquerLimitesVisibiliteFloreDimension();
		AppliquerParametresLodTextureTerrain();
		if (ActiverHorizonLod)
		{
			// InitialiserHorizonLointain() ne fait rien si le mesh existe déjà : sans recréation, PasHorizon / RayonHorizon ne changent jamais en jeu.
			if (_horizonLodMesh != null && GodotObject.IsInstanceValid(_horizonLodMesh))
			{
				if (_horizonLodMesh.IsInsideTree())
					RemoveChild(_horizonLodMesh);
				_horizonLodMesh.QueueFree();
				_horizonLodMesh = null;
			}
			_centreHorizonCell = new Vector2I(int.MinValue, int.MinValue);
			_timerMajHorizon = 0f;
			Callable.From(InitialiserHorizonLointain).CallDeferred();
		}
		else if (_horizonLodMesh != null && GodotObject.IsInstanceValid(_horizonLodMesh))
		{
			_horizonLodMesh.QueueFree();
			_horizonLodMesh = null;
			_centreHorizonCell = new Vector2I(int.MinValue, int.MinValue);
		}

		Vector2I centre = ObtenirCoordonneesChunkJoueur();
		ReplanifierFloreAutourJoueur(centre);

		// Si le culling est désactivé, il faut forcer la visibilité des chunks déjà cachés.
		if (!ActiverCullingCameraChunks)
		{
			foreach (var kv in _chunksData)
			{
				ChunkData data = kv.Value;
				if (data == null) continue;
				data.CullingVisible = true;
				if (data.VisualInstanceRID.IsValid)
					RenderingServer.Singleton.InstanceSetVisible(data.VisualInstanceRID, true);
				if (data.WaterInstanceRID.IsValid)
					RenderingServer.Singleton.InstanceSetVisible(data.WaterInstanceRID, true);
				if (data._nodeFlore is Node3D flore)
					flore.Visible = true;
			}
		}
		else
		{
			// Réévalue vite la visibilité après changement d'angle/marge.
			_timerCullingCamera = 0f;
		}
	}

	public void ForcerModeStreamingUtilisateur(bool activerProtectionsFps)
	{
		ModeAutoDiagnosticAdaptatif = activerProtectionsFps;
		ActiverGateFpsStrict = activerProtectionsFps;
		ActiverAntiSpikeFrameTime = activerProtectionsFps;
		if (!activerProtectionsFps)
		{
			_ratioChargeAuto = 1f;
			_facteurMouvementAuto = 1f;
			_niveauUrgencePerf = 0;
			_timerFreinSpike = 0f;
			_gateStreamingGele = false;
			_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
			_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
			int minRayon = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
			int cible = Mathf.Max(minRayon, RayonChargementChunksActif());
			_rayonRequetesActuel = Mathf.Clamp(Mathf.Max(_rayonRequetesActuel, cible - 1), minRayon, cible);
		}

		Vector3 posObs = ObtenirPositionObservation();
		_cooldownRebuildRadar = 0f;
		_rebuildRadarEnAttente = false;
		if (!_radarEnCours)
			ActualiserVisibiliteEtTriChunks(posObs);
		if (!activerProtectionsFps && ActiverCullingCameraChunks)
			ReinitialiserVisibiliteCullingTousLesChunksCharges(posObs);
	}

	/// <summary>À appeler quand le joueur valide explicitement les graphismes (bouton Appliquer) : laisse converger vers RenderDistance sans plafonds d’urgence immédiats.</summary>
	/// <summary>Nouveau monde / reconnexion : « Sauver les FPS » reste actif mais les chunks continuent de se charger.</summary>
	public void DemarrerGraceStreamingBootstrapNouveauMonde()
	{
		_timerGraceStreamingBootstrap = Mathf.Max(_timerGraceStreamingBootstrap, DureeGraceStreamingBootstrapNouveauMondeSec);
		_gateStreamingGele = false;
		_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
		_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
		_timerGraceStreamingReglageUtilisateur = Mathf.Max(
			_timerGraceStreamingReglageUtilisateur,
			Mathf.Min(DureeGraceStreamingReglageUtilisateurSec, DureeGraceStreamingBootstrapNouveauMondeSec * 0.65f));
		int minRayon = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		int cible = Mathf.Max(minRayon, RayonChargementChunksActif());
		_rayonRequetesActuel = Mathf.Clamp(Mathf.Max(_rayonRequetesActuel, minRayon + 6), minRayon, cible);
		_timerExpansionRequetes = 0f;
		_timerProgressionForceeRayon = 0f;
	}

	public void SignalerGraceStreamingApresReglageManuel()
	{
		_timerGraceStreamingReglageUtilisateur = DureeGraceStreamingReglageUtilisateurSec;
		_gateStreamingGele = false;
		_tempsFpsStableHaut = 0f;
		_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
		_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
	}

	/// <summary>Avance la fenêtre de requêtes après une hausse de <see cref="RenderDistance"/> (évite d’attendre uniquement les +1 / 0,3 s).</summary>
	public void ImpulserConvergenceVersRenderDistance()
	{
		if (!ModeSurvieFpsAgressif)
			return;
		int minRayon = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		int cible = Mathf.Max(minRayon, RayonChargementChunksActif());
		int gap = Mathf.Max(0, cible - _rayonRequetesActuel);
		if (gap <= 0)
			return;
		float frac = Mathf.Clamp(FractionImpulsionHausseRenderDistance, 0.05f, 0.95f);
		int saut = Mathf.Max(1, Mathf.RoundToInt(gap * frac));
		_rayonRequetesActuel = Mathf.Clamp(_rayonRequetesActuel + saut, minRayon, cible);
	}

	public void ForcerRafraichissementStreamingGraphique(bool microReload)
	{
		Vector3 positionObservation = ObtenirPositionObservation();

		int minRayon = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		int cible = Mathf.Max(minRayon, RayonChargementChunksActif());
		_rayonRequetesActuel = Mathf.Clamp(Mathf.Max(_rayonRequetesActuel, cible - 1), minRayon, cible);
		_timerExpansionRequetes = 0f;
		_timerProgressionForceeRayon = 0f;
		_timerRafraichissementRadarImmobile = 0f;
		_tempsDepuisNettoyage = IntervalleNettoyageChunks;
		_cooldownRebuildRadar = 0f;

		if (microReload)
			NettoyerChunksObsoles(positionObservation);
		else
			PurgerChunksObsolètesDeLaFile(positionObservation);

		_rebuildRadarEnAttente = false;
		if (!_radarEnCours)
			ActualiserVisibiliteEtTriChunks(positionObservation);
		else
			DemanderRafraichissementRadar(positionObservation, 0.01f);
	}

	public float LireFpsMoyenAutoDiagnostic() => _fpsMoyenneAuto;

	public int LireNiveauUrgencePerformance() => _niveauUrgencePerf;

	public void Initialiser(CharacterBody3D joueur, int seed, Action<Vector2I> enregistrerDemandeChunk,
		Action<Vector3, float, float> demanderDestruction, Action<Vector3, Vector3, float, int> demanderCreation)
	{
		_joueur = joueur;
		_seedTerrain = seed;
		_enregistrerDemandeChunk = enregistrerDemandeChunk;
		_demanderDestruction = demanderDestruction;
		_demanderCreation = demanderCreation;
		Chunk_Client.RayonQualiteMaxChunks = Mathf.Max(1, RayonQualiteMaxChunks);
		AppliquerLimitesVisibiliteFloreDimension();
		_rayonRequetesActuel = Mathf.Max(RayonDormancePhysique + 1, RayonInitialRequetesChunks);
		// Grâce bootstrap courte au démarrage (débloque le gate FPS sans saturer 50 s en mode chargement collision).
		_timerGraceStreamingBootstrap = Mathf.Max(_timerGraceStreamingBootstrap, 18f);
		_gateStreamingGele = false;
		_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
		if (ModeSurvieFpsAgressif)
			DemarrerGraceStreamingBootstrapNouveauMonde();
		_timerExpansionRequetes = IntervalleExpansionRequetesSec;
		_timerProgressionForceeRayon = IntervalleProgressionForceeRayonSec;
		_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile;
		AppliquerParametresLodTextureTerrain();
	}

	public void ConfigurerReseauChunks(NetworkManager networkManager, int dimensionActive)
	{
		_networkManager = networkManager;
		_dimensionReseauActive = dimensionActive;
		_timerTrimAbysse = 0f;
		_demandesAbysseFrameDerniereEmission.Clear();
		AppliquerLimitesVisibiliteFloreDimension();
	}

	public void DefinirDimensionReseauActive(int dimensionId)
	{
		_dimensionReseauActive = dimensionId;
		_timerTrimAbysse = 0f;
		_demandesAbysseFrameDerniereEmission.Clear();
		AppliquerLimitesVisibiliteFloreDimension();
	}

	/// <summary>APISARA : plafonne gazon/buissons pour éviter des milliers d'instances MultiMesh dans le goufre.</summary>
	private void AppliquerLimitesVisibiliteFloreDimension()
	{
		if (_dimensionReseauActive == (int)DimensionJeu.Abysse)
		{
			bool dansGoufre = EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef)
				&& ConstantesDimensionAbysse.EstDansTrouNoirXZ(joueurRef.GlobalPosition.X, joueurRef.GlobalPosition.Z);
			int maxGazon = dansGoufre ? 9 : 6;
			Chunk_Client.RayonVisibiliteGazonChunks = Mathf.Clamp(RayonGazonVisibleChunks, 1, maxGazon);
			Chunk_Client.RayonVisibiliteBuissonsChunks = Mathf.Clamp(RayonBuissonsVisibleChunks, 2, dansGoufre ? 12 : 10);
		}
		else
		{
			Chunk_Client.RayonVisibiliteGazonChunks = Mathf.Max(1, RayonGazonVisibleChunks);
			Chunk_Client.RayonVisibiliteBuissonsChunks = Mathf.Max(2, RayonBuissonsVisibleChunks);
		}
	}
}
