using Godot;
using System.Collections.Generic;

/// <summary>
/// Bootstrap de l'architecture réseau (serveurs par dimension, client, océan, cycle solaire). Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: ordre d'initialisation et câblage RPC/dimension identiques à l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void DemarrerArchitectureReseau()
	{
		DetecterProfilMaterielEtAjuster();
		_networkManager = new NetworkManager();
		AddChild(_networkManager);
		_networkManager.DemarrerHostSolo();
		_networkManager.CommandeAdminDemandee += SurCommandeAdminDemandee;
		_networkManager.InjectionItemCreatifDemandee += SurInjectionItemCreatifDemandee;
		_networkManager.DemandeChunkDimensionDemandee += SurDemandeChunkDimensionDemandee;

		_serveurParDimension.Clear();
		_attenteChunksParDimension.Clear();
		_dimensionParPeer.Clear();
		_racineParDimension.Clear();
		_arbresParDimension.Clear();

		// Crée une racine de scène par dimension (Alpha + Abysse + Beta + Omega + Delta) avant d'instancier les serveurs.
		foreach (var info in ConstantesDimensions.Toutes())
		{
			var racine = new Node3D { Name = info.NomCanonique };
			AddChild(racine);
			_racineParDimension[info.Id] = racine;
		}

		int seedAlpha = GetNode<GameState>("/root/GameState").SeedTerrainActuel;
		Material materielTerrainResolu = TerrainMaterialFactory.ObtenirMaterielTerrainRobuste(MaterielTerrain);

		// Alpha + clones (Beta/Omega/Delta) : même seed, même algorithme, fuseaux décalés de 0/+6/+12/+18 h.
		foreach (var info in ConstantesDimensions.ToutesAlphaLike())
		{
			var serveur = new Monde_Serveur
			{
				NomDimension = info.NomCanonique,
				ActiverGenerationAbysse = false,
				ActiverProfondeurEtendue = ActiverProfondeurEtendue,
				ProfondeurMaxMetres = ProfondeurMaxMetres,
				TailleChunk = TailleChunk,
				HauteurMax = HauteurMax,
				SeedTerrain = seedAlpha,
				RenderDistance = RenderDistance,
				FuseauHoraireHeures = FuseauHoraireHeures + info.FuseauOffsetHeures,
				ModeEssencesPartoutTemporaire = ModeEssencesPartoutTemporaire,
				RatioJungleModeTest = RatioJungleModeTest,
				MaterielTerrain = materielTerrainResolu
			};
			_serveurParDimension[info.Id] = serveur;
			_attenteChunksParDimension[info.Id] = new Dictionary<Vector3I, HashSet<long>>();
			if (info.Id == (int)DimensionJeu.Alpha)
				_mondeServeurAlpha = serveur;
		}

		// APISARA : génération abyssale dédiée, seed décalée +9137 (bruits Abysse historiques), heure forcée 13h30.
		_mondeServeurAbysse = new Gestionnaire_Abysse
		{
			NomDimension = ConstantesDimensionAbysse.Apisara,
			ActiverGenerationAbysse = true,
			TailleChunk = TailleChunk,
			HauteurMax = HauteurMax,
			SeedTerrain = seedAlpha + 9137,
			RenderDistance = RenderDistance,
			FuseauHoraireHeures = FuseauHoraireHeures + ConstantesDimensions.ObtenirInfoOuAlpha((int)DimensionJeu.Abysse).FuseauOffsetHeures,
			ModeEssencesPartoutTemporaire = ModeEssencesPartoutTemporaire,
			RatioJungleModeTest = RatioJungleModeTest,
			MaterielTerrain = materielTerrainResolu
		};
		_serveurParDimension[(int)DimensionJeu.Abysse] = _mondeServeurAbysse;
		_attenteChunksParDimension[(int)DimensionJeu.Abysse] = new Dictionary<Vector3I, HashSet<long>>();

		if (!_serveurParDimension.ContainsKey(_dimensionLocaleActive))
			_dimensionLocaleActive = (int)DimensionJeu.Alpha;
		_mondeServeur = ObtenirServeurDimension(_dimensionLocaleActive) ?? _mondeServeurAlpha;
		DefinirDimensionPeer(Multiplayer.GetUniqueId(), _dimensionLocaleActive);

		_mondeClient = new Monde_Client();
		_mondeClient.TailleChunk = TailleChunk;
		_mondeClient.HauteurMax = HauteurMax;
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
		_mondeClient.MaterielTerrain = TerrainMaterialFactory.ObtenirMaterielTerrainRobuste(MaterielTerrain);
		ConfigurerProfilMondeClientSelonMateriel();
		_mondeClient.Initialiser(
			_joueur,
			GetNode<GameState>("/root/GameState").SeedTerrainActuel,
			coord =>
			{
				Vector3 posJ = ObtenirPositionJoueurOuSpawn();
				int coordY = Mathf.FloorToInt(posJ.Y / Mathf.Max(1f, _mondeServeur?.HauteurMax ?? 1));
				_mondeServeur?.EnregistrerDemandeChunk(coord, coordY, posJ);
			},
			(pointImpact, rayon, forceDegats) => _mondeServeur.AppliquerDestructionGlobale(pointImpact, rayon, forceDegats),
			(pointImpact, normale, rayon, idMatiere) => _mondeServeur.AppliquerCreationGlobale(pointImpact, normale, rayon, idMatiere)
		);
		_mondeClient.ConfigurerReseauChunks(_networkManager, _dimensionLocaleActive);
		AppliquerOptionsGraphiques(CapturerOptionsGraphiquesCourantes(_optionsGraphiquesActuelles?.Preset ?? PresetGraphique.Personnalise), sauvegarder: false, synchroniserUi: false);

		// Initialise et reparente chaque serveur sous sa racine dédiée (Alpha, Beta, Omega, Delta, Abysse).
		foreach (var kv in _serveurParDimension)
		{
			InitialiserDimensionServeur(kv.Value, kv.Key);
			if (_racineParDimension.TryGetValue(kv.Key, out Node3D racine) && racine != null && kv.Value != null)
				racine.AddChild(kv.Value);
		}
		MettreAJourVisibiliteArbresParDimension(_dimensionLocaleActive);
		AddChild(_mondeClient);
		ReparenterNoeudDansDimension(_joueur, _dimensionLocaleActive);
		MettreAJourAtmosphereAbysseLocale(_dimensionLocaleActive);
		_dimensionCoordinator.MettreAJourSuspensionServeursDimensions(_dimensionLocaleActive);

		// Croissance des arbres + jour absolu au passage minuit
		var cycleSolaire = GetParent()?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (cycleSolaire != null)
		{
			cycleSolaire.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
			if (_mondeServeurAbysse is Gestionnaire_Abysse gestionnaireAbysseDistance)
				gestionnaireAbysseDistance.ConfigurerDistanceBrouillardProgressive(RenderDistance, TailleChunk, 2);
			cycleSolaire.Connect("NouveauJour", Callable.From(() =>
			{
				GameState.Instance?.IncrementerJourAbsolu();
				foreach (var kv in _serveurParDimension)
				{
					if (kv.Value == null || kv.Value.EstSimulationSuspendue)
						continue;
					kv.Value.FairePousserArbresDuJour();
				}
			}));
		}

		// Matrice visqueuse : Area3D océan (Y < 103) impose damp + gravité réduite (Archimède)
		CreerAreaOcean();

		// Lier le chunk de spawn en priorité pour éviter chute libre (comme les 2 fois précédentes)
		Vector3 pos = _joueur.GlobalPosition;
		Vector2I chunkSpawn = WorldToChunkCoord(pos, TailleChunk);
		_mondeClient.ReserverChunkSpawnPrioritaire(chunkSpawn);

		// Envoyer le fuseau horaire de la dimension au client (spawn / portail)
		EnvoyerFuseauHoraireAuPeer(1); // Peer 1 = hôte local en Solo
		Multiplayer.PeerConnected += EnvoyerFuseauHoraireAuPeer;
		Multiplayer.PeerConnected += SurPeerConnecteDimensions;
		Multiplayer.PeerDisconnected += SurPeerDeconnecteDimensions;

		Callable.From(InitialiserPortailsNexusSiNecessaire).CallDeferred();
	}
}
