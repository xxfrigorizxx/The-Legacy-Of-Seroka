using Godot;
using System;

/// <summary>
/// Boucle de tick serveur (orchestration). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: ordre fonctionnel figé (workers -> demandes -> réplication -> spawn -> décharge -> eau).
/// </summary>
public partial class Monde_Serveur : Node
{
	public override void _PhysicsProcess(double delta)
	{
		_serverTickOrchestrator ??= new ServerTickOrchestrator(this);
		_serverTickOrchestrator.Execute(delta);
	}

	internal void ExecuterTickMonolithique(double delta)
	{
		// CONTRAT DE TICK SERVEUR (ordre fonctionnel figé pour équivalence gameplay/perf):
		// workers -> demandes chunks -> réplication réseau -> spawn -> décharge -> eau runtime.
		AssurerServicesRefactoInitialises();
		bool hadModifications = _modificationEnCours;
		_modificationEnCours = false;
		int integrationsWorkers = TickIntegrerWorkers();
		TickTraiterDemandesChunks(hadModifications, out int demandesTraitees, out int chargesDisque);
		int envoisCeTick = TickReplicationsReseau();
		TickSpawnProgressif(out int nArbres, out int nPierres);
		TickDechargement(delta);
		_ = _waterSimulationService.TickRuntime();
		MettreAJourDiagnosticBaselineServeur((float)delta, new SnapshotTickServeur
		{
			IntegrationsWorkers = integrationsWorkers,
			DemandesTraitees = demandesTraitees,
			ChargesDisque = chargesDisque,
			EnvoisReseau = envoisCeTick,
			ArbresSpawns = nArbres,
			PierresSpawns = nPierres,
			FileDemandesRestantes = _chunksEnAttenteEnvoi.Count,
			FileEnvoisRestants = _fileEnvoiReseau.Count,
			FileEauRestante = _fileEau.Count
		});
	}

	private int TickIntegrerWorkers()
	{
		int integrationsWorkers = 0;
		while (integrationsWorkers < MaxIntegrationsWorkersParTick && _chunksGeneres.TryDequeue(out var result))
		{
			var cleGeneree = new Vector3I(result.coord.X, result.coordY, result.coord.Y);
			_chunksEnCoursGeneration.Remove(cleGeneree);
			_chunksEnGenerationActive--;
			if (TryGetChunkRuntime(result.coord, result.coordY, out var existant) && existant.EstChargeDepuisDisque)
				continue;
			DefinirChunkRuntime(result.coord, result.coordY, result.chunk);
			SynchroniserFrontieresAvecVoisinsCharges(result.coord, result.chunk);
			_spawnPipelineService.SpawnerArbresChunkAvecPrioriteSauvegarde(result.coord, result.chunk);
			if (ActiverGenerationAbysse)
			{
				_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = result.coord, Donnees = result.chunk.ObtenirDonneesPourClient() });
				_spawnPipelineService.DeclencherEnsemencement(result.coord, result.chunk, TailleChunk, (coord, ch) => _spawnPipelineService.LibererRochesChunk(coord));
			}
			else
			{
				_spawnPipelineService.DeclencherEnsemencement(result.coord, result.chunk, TailleChunk, (coord, ch) =>
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() }));
			}
			integrationsWorkers++;
		}
		return integrationsWorkers;
	}

	private void TickTraiterDemandesChunks(bool hadModifications, out int demandesTraitees, out int chargesDisque)
	{
		demandesTraitees = 0;
		chargesDisque = 0;
		if (hadModifications)
			return;
		Vector3 posObs = Vector3.Zero;
		try { posObs = InvokerPositionJoueurStreaming(); }
		catch (ObjectDisposedException) { posObs = Vector3.Zero; }
		if (ActiverGenerationAbysse)
			PurgerRuntimeAbysseHorsFenetre(posObs);
		float rayonMaxCarrePurge = (RenderDistance + 1) * (RenderDistance + 1);
		_chunksEnAttenteEnvoi.RemoveAll(c =>
		{
			if (c.EstAbysse && !EstCoordYDansFenetrePaliersAbysse(c.CoordY, posObs)) return true;
			if (_demandesForceesSansPurge.Contains(c.Cle3D)) return false;
			float d2 = DistanceCarreeAuJoueur(c, posObs);
			return d2 > rayonMaxCarrePurge;
		});

		Vector3 posObservation = posObs;
		int budgetChargesDisque = ActiverGenerationAbysse ? Mathf.Max(4, MaxChargesDisqueParTick * 5) : MaxChargesDisqueParTick;
		int budgetDemandes = ActiverGenerationAbysse
			? Mathf.Max(2, MaxDemandesChunksAbysseParTick + 2)
			: Mathf.Max(1, MaxDemandesChunksParTick);
		while (_chunksEnAttenteEnvoi.Count > 0 && _chunksEnGenerationActive < LancerMaxTaches && demandesTraitees < budgetDemandes)
		{
			demandesTraitees++;
			DemandeChunk demande = ExtraireChunkLePlusProche(_chunksEnAttenteEnvoi, posObservation);
			Vector2I chunkCible = demande.Coord;
			int coordYCible = demande.CoordY;
			Vector3I cleDemande = demande.Cle3D;
			_demandesEnAttenteSet.Remove(cleDemande);
			bool demandeForcee = _demandesForceesSansPurge.Remove(cleDemande);
			float distCarree = DistanceCarreeAuJoueur(demande, posObservation);
			float rayonMaxCarre = (RenderDistance + 1) * (RenderDistance + 1);
			if (!demandeForcee && distCarree > rayonMaxCarre)
			{
				_chunksEnAttenteEnvoi.Add(demande);
				_demandesEnAttenteSet.Add(cleDemande);
				continue;
			}
			if (TryGetChunkRuntime(chunkCible, coordYCible, out var existant))
			{
				_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = existant.ObtenirDonneesPourClient() });
				continue;
			}
			Chunk_Serveur chunkActuel = null;
			if (FichierChunkExiste(chunkCible, coordYCible))
			{
				if (chargesDisque >= budgetChargesDisque)
				{
					_chunksEnAttenteEnvoi.Add(demande);
					_demandesEnAttenteSet.Add(cleDemande);
					continue;
				}
				chunkActuel = _chunkPersistenceService.ChargerChunkDepuisDisque(chunkCible, coordYCible);
				if (chunkActuel == null)
					GD.PrintErr($"ZERO-K DIAG : Fallback procédural pour {chunkCible} après échec de chargement disque.");
				chargesDisque++;
			}
			if (chunkActuel == null)
			{
				_chunkGenerationScheduler.PlanifierGenerationChunk(chunkCible, coordYCible, cleDemande);
				continue;
			}
			DefinirChunkRuntime(chunkCible, coordYCible, chunkActuel);
			SynchroniserFrontieresAvecVoisinsCharges(chunkCible, chunkActuel);
			RepousserBorduresChunkDisqueVersVoisinsProceduraux(chunkCible, chunkActuel);
			_spawnPipelineService.SpawnerArbresChunkAvecPrioriteSauvegarde(chunkCible, chunkActuel);
			if (!_pierrePersistenceService.ChargerEtSpawnerPierresChunk(chunkCible, coordYCible))
			{
				if (ActiverGenerationAbysse)
				{
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = chunkActuel.ObtenirDonneesPourClient() });
					_spawnPipelineService.DeclencherEnsemencement(chunkCible, chunkActuel, TailleChunk, (coord, ch) => _spawnPipelineService.LibererRochesChunk(coord));
				}
				else
				{
					_spawnPipelineService.DeclencherEnsemencement(chunkCible, chunkActuel, TailleChunk, (coord, ch) =>
						_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = coord, Donnees = ch.ObtenirDonneesPourClient() }));
				}
			}
			else
			{
				_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = chunkActuel.ObtenirDonneesPourClient() });
			}
		}
	}

	private int TickReplicationsReseau()
	{
		int envoisCeTick = 0;
		while (_fileEnvoiReseau.Count > 0 && envoisCeTick < MaxChunksEnvoiParTick)
		{
			ColisChunk colis = _fileEnvoiReseau.Dequeue();
			_onEnvoyerChunk?.Invoke(colis.Coord, colis.Donnees);
			_spawnPipelineService.LibererRochesChunk(colis.Coord);
			envoisCeTick++;
		}
		return envoisCeTick;
	}

	private void TickSpawnProgressif(out int nArbres, out int nPierres)
	{
		ReveillerPierresDansRayon();
		float facteurPressionSpawn = CalculerFacteurPressionSpawn();
		nArbres = 0;
		int budgetArbresTick = Mathf.Max(1, Mathf.RoundToInt(CalculerBudgetSpawnAdaptatif(MaxArbresSpawnParTick) * facteurPressionSpawn));
		ulong t0Arbres = Time.GetTicksUsec();
		ulong budgetUsArbres = (ulong)Mathf.Max(110f, BudgetMsSpawnArbresParTick * 1000f * facteurPressionSpawn);
		while (nArbres < budgetArbresTick && _fileSpawnArbres.Count > 0)
		{
			if (Time.GetTicksUsec() - t0Arbres >= budgetUsArbres) break;
			var a = _fileSpawnArbres.Dequeue();
			if (!_chunks.ContainsKey(a.coord)) continue;
			InstancierArbreVivant(a.pos, a.age, a.seed, a.indexBotanique, a.joursRattrapage);
			nArbres++;
		}

		nPierres = 0;
		int budgetPierresTick = Mathf.Max(1, Mathf.RoundToInt(CalculerBudgetSpawnAdaptatif(MaxPierresParFrame) * Mathf.Clamp(facteurPressionSpawn * 0.95f, 0.2f, 1f)));
		ulong t0Pierres = Time.GetTicksUsec();
		ulong budgetUsPierres = (ulong)Mathf.Max(95f, BudgetMsSpawnPierresParTick * 1000f * Mathf.Clamp(facteurPressionSpawn * 0.9f, 0.2f, 1f));
		while (nPierres < budgetPierresTick && _filePierresAInstancier.Count > 0)
		{
			if (Time.GetTicksUsec() - t0Pierres >= budgetUsPierres) break;
			var (pos, id, idx, chim) = _filePierresAInstancier.Dequeue();
			if (idx < 0)
			{
				float distEau = Mathf.Abs(pos.Y - NIVEAU_EAU);
				bool formesCassées = distEau > SeuilDistanceEauFormesCassées;
				idx = formesCassées ? -2 : -1;
			}
			GenererItemPhysique(pos, id, idx, chim);
			nPierres++;
		}
	}

	private void TickDechargement(double delta)
	{
		_tempsDepuisVerifDecharge += (float)delta;
		if (_tempsDepuisVerifDecharge >= IntervalleEvaluationTectonique)
		{
			_tempsDepuisVerifDecharge = 0f;
			EvaluerDechargementChunks();
		}
		ProcesserDechargeProgressive();
	}

	private void AssurerServicesRefactoInitialises()
	{
		_chunkPersistenceService ??= new ChunkPersistenceService(this);
		_floraPersistenceService ??= new FloraPersistenceService(this);
		_arbrePersistenceService ??= new ArbrePersistenceService(this);
		_pierrePersistenceService ??= new PierrePersistenceService(this);
		_chunkGenerationKernel ??= new ChunkGenerationKernel(this);
		_chunkGenerationScheduler ??= new ChunkGenerationScheduler(this, _chunkGenerationKernel);
		_waterSimulationService ??= new WaterSimulationService(this);
		_spawnPipelineService ??= new SpawnPipelineService(this);
	}

	private void MettreAJourDiagnosticBaselineServeur(float dt, in SnapshotTickServeur snapshot)
	{
		if (!ActiverDiagnosticBaselineServeur)
			return;
		_cooldownDiagnosticBaselineServeur += Mathf.Max(0f, dt);
		float intervalle = Mathf.Max(0.5f, IntervalleDiagnosticBaselineServeurSec);
		if (_cooldownDiagnosticBaselineServeur < intervalle)
			return;
		_cooldownDiagnosticBaselineServeur = 0f;
		GD.Print(
			$"ZERO-K BASELINE SERVEUR [{NomDimension}] " +
			$"wrk={snapshot.IntegrationsWorkers} req={snapshot.DemandesTraitees} io={snapshot.ChargesDisque} send={snapshot.EnvoisReseau} " +
			$"arb={snapshot.ArbresSpawns} roc={snapshot.PierresSpawns} " +
			$"qReq={snapshot.FileDemandesRestantes} qSend={snapshot.FileEnvoisRestants} qEau={snapshot.FileEauRestante}");
	}
}
