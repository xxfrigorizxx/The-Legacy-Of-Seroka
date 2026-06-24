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

	private bool _profilerServeur;
	private bool _profilerServeurResolu;
	private double _cooldownDrainProfilageServeur;

	internal void ExecuterTickMonolithique(double delta)
	{
		// CONTRAT DE TICK SERVEUR (ordre fonctionnel figé pour équivalence gameplay/perf):
		// workers -> demandes chunks -> réplication réseau -> spawn -> décharge -> eau runtime.
		AssurerServicesRefactoInitialises();
		if (!_profilerServeurResolu)
		{
			// Profilage serveur : uniquement en éditeur (auto-désactivé en build distribué).
			_profilerServeur = OS.HasFeature("editor");
			_profilerServeurResolu = true;
		}
		bool prof = _profilerServeur;
		bool hadModifications = _modificationEnCours;
		_modificationEnCours = false;

		ulong tProf = prof ? PerfBudgetMonitor.Begin() : 0UL;
		int integrationsWorkers = TickIntegrerWorkers();
		if (prof) { PerfBudgetMonitor.End("Serveur/IntegrerWorkers", tProf); tProf = PerfBudgetMonitor.Begin(); }
		TickTraiterDemandesChunks(hadModifications, out int demandesTraitees, out int chargesDisque);
		if (prof) { PerfBudgetMonitor.End("Serveur/Demandes", tProf); tProf = PerfBudgetMonitor.Begin(); }
		int envoisCeTick = TickReplicationsReseau();
		if (prof) { PerfBudgetMonitor.End("Serveur/Replication", tProf); tProf = PerfBudgetMonitor.Begin(); }
		TickSpawnProgressif(out int nArbres, out int nPierres);
		if (prof) { PerfBudgetMonitor.End("Serveur/Spawn", tProf); tProf = PerfBudgetMonitor.Begin(); }
		TickDechargement(delta);
		TickScanTerrainFlottant();
		if (prof) { PerfBudgetMonitor.End("Serveur/Dechargement", tProf); tProf = PerfBudgetMonitor.Begin(); }
		_ = _waterSimulationService.TickRuntime();
		if (prof)
		{
			PerfBudgetMonitor.End("Serveur/Eau", tProf);
			_cooldownDrainProfilageServeur += delta;
			if (_cooldownDrainProfilageServeur >= 2.0)
			{
				_cooldownDrainProfilageServeur = 0.0;
				PerfBudgetMonitor.FlushSiEchu("Serveur", 2.0f, force: true);
				GD.Print($"DIAG Demandes -> file={_chunksEnAttenteEnvoi.Count} resend={_diagDemResend} disk={_diagDemDisk} gen={_diagDemGen} resort={_diagDemResort} (sur ~2s)");
				_diagDemResend = 0; _diagDemDisk = 0; _diagDemGen = 0; _diagDemResort = 0;
			}
		}
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
				_spawnPipelineService.DeclencherEnsemencement(result.coord, result.chunk, TailleChunk, (coord, ch) => _spawnPipelineService.LibererRochesChunk(coord, ch.ChunkOffsetY));
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

	private readonly System.Collections.Generic.List<DemandeChunk> _demandesAReinserer = new System.Collections.Generic.List<DemandeChunk>();
	private Vector2I _dernierChunkTriDemandes = new Vector2I(int.MinValue, int.MinValue);
	private int _dernierCoordYTriDemandes = int.MinValue;
	private int _compteurTickRetriDemandes;
	// Diagnostic : combien de demandes traitées par catégorie (re-envoi chunk déjà chargé / disque / génération).
	private int _diagDemResend, _diagDemDisk, _diagDemGen, _diagDemResort;
	/// <summary>Plafond de la file de demandes serveur : filet anti-flood (changement de dimension/monde neuf). Au-delà, les plus lointaines sont relâchées (le client les redemandera en s'approchant).</summary>
	private const int PlafondFileDemandes = 6000;

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

		Vector3 posObservation = posObs;
		int budgetChargesDisque = ActiverGenerationAbysse
			? Mathf.Max(4, MaxChargesDisqueParTick * 5)
			: (ModeProfondeurActive ? Mathf.Max(3, MaxChargesDisqueParTick * 3) : MaxChargesDisqueParTick);
		int budgetDemandes = ActiverGenerationAbysse
			? Mathf.Max(2, MaxDemandesChunksAbysseParTick + 2)
			: (ModeProfondeurActive ? Mathf.Max(8, MaxDemandesChunksParTick * 4) : Mathf.Max(1, MaxDemandesChunksParTick));
		if (_chunksEnAttenteEnvoi.Count == 0)
			return;

		Vector2I obsChunkTri = Gestionnaire_Monde.WorldToChunkCoord(posObs, TailleChunk);
		int coordYObsTri = CoordYDepuisMondeY(posObs.Y, HauteurMax);

		// COÛT MAÎTRISÉ : le gros travail O(n) (purge + tri + plafond) ne se fait QUE quand le joueur change
		// de chunk/tranche, ou ~toutes les 30 frames, ou en cas de flood. Sinon la file reste triée
		// (plus loin d'abord) et chaque tick ne fait que piocher la fin en O(1) = le plus proche.
		// Avant : O(n) re-scanné CHAQUE tick (cause du « Serveur/Demandes » constant à 7-8 ms).
		bool doitRetrier = obsChunkTri != _dernierChunkTriDemandes
			|| coordYObsTri != _dernierCoordYTriDemandes
			|| ++_compteurTickRetriDemandes >= 30
			|| _chunksEnAttenteEnvoi.Count > PlafondFileDemandes;
		if (doitRetrier)
		{
			_diagDemResort++;
			_dernierChunkTriDemandes = obsChunkTri;
			_dernierCoordYTriDemandes = coordYObsTri;
			_compteurTickRetriDemandes = 0;

			float rayonMaxCarrePurge = (RenderDistance + 1) * (RenderDistance + 1);
			_chunksEnAttenteEnvoi.RemoveAll(c =>
			{
				if (c.EstAbysse && !EstCoordYDansFenetrePaliersAbysse(c.CoordY, posObs)) { _demandesEnAttenteSet.Remove(c.Cle3D); return true; }
				if (_demandesForceesSansPurge.Contains(c.Cle3D)) return false;
				if (DistanceCarreeAuJoueur(c, posObs) > rayonMaxCarrePurge) { _demandesEnAttenteSet.Remove(c.Cle3D); return true; }
				return false;
			});

			bool integrerEcartY = ModeProfondeurActive || ActiverGenerationAbysse;
			float DistanceTri(DemandeChunk c)
			{
				int dx = c.Coord.X - obsChunkTri.X;
				int dz = c.Coord.Y - obsChunkTri.Y;
				float d = dx * dx + dz * dz;
				if (integrerEcartY || c.EstAbysse)
				{
					int dy = c.CoordY - coordYObsTri;
					d += dy * dy;
				}
				return d;
			}
			_chunksEnAttenteEnvoi.Sort((a, b) => DistanceTri(b).CompareTo(DistanceTri(a)));

			// File bornée : ne garder que les PlafondFileDemandes plus proches (à la fin après tri décroissant).
			if (_chunksEnAttenteEnvoi.Count > PlafondFileDemandes)
			{
				int aRetirer = _chunksEnAttenteEnvoi.Count - PlafondFileDemandes;
				for (int k = 0; k < aRetirer; k++)
					_demandesEnAttenteSet.Remove(_chunksEnAttenteEnvoi[k].Cle3D);
				_chunksEnAttenteEnvoi.RemoveRange(0, aRetirer);
			}
		}

		_demandesAReinserer.Clear();
		float rayonMaxCarre = (RenderDistance + 1) * (RenderDistance + 1);
		// Budget temps strict : le traitement des demandes ne doit pas accaparer le thread principal.
		// Au-delà, on reprend la frame suivante → priorité au déplacement du joueur (anti-lag).
		ulong debutBoucleDemandes = Time.GetTicksUsec();
		const ulong budgetBoucleDemandesUs = 2000;
		while (_chunksEnAttenteEnvoi.Count > 0 && _chunksEnGenerationActive < LancerMaxTaches && demandesTraitees < budgetDemandes)
		{
			if (demandesTraitees > 0 && Time.GetTicksUsec() - debutBoucleDemandes > budgetBoucleDemandesUs)
				break;
			demandesTraitees++;
			int dernierIdx = _chunksEnAttenteEnvoi.Count - 1;
			DemandeChunk demande = _chunksEnAttenteEnvoi[dernierIdx];
			_chunksEnAttenteEnvoi.RemoveAt(dernierIdx); // O(1) : retrait du dernier (le plus proche).
			Vector2I chunkCible = demande.Coord;
			int coordYCible = demande.CoordY;
			Vector3I cleDemande = demande.Cle3D;
			_demandesEnAttenteSet.Remove(cleDemande);
			bool demandeForcee = _demandesForceesSansPurge.Remove(cleDemande);
			float distCarree = DistanceCarreeAuJoueur(demande, posObservation);
			if (!demandeForcee && distCarree > rayonMaxCarre)
				continue; // Hors rayon : relâché (le client redemandera en s'approchant).
			if (TryGetChunkRuntime(chunkCible, coordYCible, out var existant))
			{
				// Anti-gaspillage : ne renvoyer un chunk déjà en RAM que s'il a RÉELLEMENT changé depuis le dernier
				// envoi (minage/pose/frontière). Sinon le client l'a déjà → on l'ignore, même si la demande est
				// « forcée » (forcée = ne pas PURGER la demande, ≠ forcer un renvoi identique).
				// Cause du diaporama : le client redemandait ses tranches (anti-spam 6 frames) et chaque demande
				// étant forcée, le serveur re-sérialisait (~260 Ko) et renvoyait ~160 chunks/s → ré-intégration client en boucle.
				if (existant.ABesoinDeReenvoiClient())
				{
					_diagDemResend++;
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = existant.ObtenirDonneesPourClient() });
					existant.MarquerEnvoyeAuClient();
				}
				continue;
			}
			Chunk_Serveur chunkActuel = null;
			if (FichierChunkExiste(chunkCible, coordYCible))
			{
				if (chargesDisque >= budgetChargesDisque)
				{
					_demandesAReinserer.Add(demande);
					_demandesEnAttenteSet.Add(cleDemande);
					continue;
				}
				chunkActuel = _chunkPersistenceService.ChargerChunkDepuisDisque(chunkCible, coordYCible);
				if (chunkActuel == null)
					GD.PrintErr($"ZERO-K DIAG : Fallback procédural pour {chunkCible} après échec de chargement disque.");
				chargesDisque++;
				_diagDemDisk++;
			}
			if (chunkActuel == null)
			{
				_diagDemGen++;
				_chunkGenerationScheduler.PlanifierGenerationChunk(chunkCible, coordYCible, cleDemande);
				continue;
			}
			DefinirChunkRuntime(chunkCible, coordYCible, chunkActuel);
			SynchroniserFrontieresAvecVoisinsCharges(chunkCible, chunkActuel);
			RepousserBorduresChunkDisqueVersVoisinsProceduraux(chunkCible, chunkActuel);
			if (chunkActuel.EstChargeDepuisDisque && chunkActuel.EstModifie)
				EnfilerScanTerrainFlottant(chunkCible, coordYCible);
			_spawnPipelineService.SpawnerArbresChunkAvecPrioriteSauvegarde(chunkCible, chunkActuel);
			if (!_pierrePersistenceService.ChargerEtSpawnerPierresChunk(chunkCible, coordYCible))
			{
				if (ActiverGenerationAbysse)
				{
					_fileEnvoiReseau.Enqueue(new ColisChunk { Coord = chunkCible, Donnees = chunkActuel.ObtenirDonneesPourClient() });
					_spawnPipelineService.DeclencherEnsemencement(chunkCible, chunkActuel, TailleChunk, (coord, ch) => _spawnPipelineService.LibererRochesChunk(coord, ch.ChunkOffsetY));
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
		// Ré-insère les demandes gardées (budget disque dépassé) pour le prochain tick.
		for (int k = 0; k < _demandesAReinserer.Count; k++)
			_chunksEnAttenteEnvoi.Add(_demandesAReinserer[k]);
		_demandesAReinserer.Clear();
	}

	private int TickReplicationsReseau()
	{
		int envoisCeTick = 0;
		while (_fileEnvoiReseau.Count > 0 && envoisCeTick < MaxChunksEnvoiParTick)
		{
			ColisChunk colis = _fileEnvoiReseau.Dequeue();
			_onEnvoyerChunk?.Invoke(colis.Coord, colis.Donnees);
			_spawnPipelineService.LibererRochesChunk(colis.Coord, colis.Donnees?.CoordChunkY ?? 0);
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
			if (!ColonneChunkRuntimeChargee(a.coord)) continue;
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
