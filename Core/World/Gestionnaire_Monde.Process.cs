using Godot;
using System;

/// <summary>
/// Boucle principale par frame (<see cref="_Process"/>) et autosauvegarde progressive. Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: ordre des sous-étapes du tick, budgets par frame et timings d'overlay/spawn identiques à l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	public override void _Process(double delta)
	{
		ulong debutProcessUs = ActiverProfilagePerfGestionnaire ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownLogAutosaveDiag = Mathf.Max(0f, _cooldownLogAutosaveDiag - (float)delta);
		_cooldownDiagnosticCollisionAbysse = Math.Max(0.0, _cooldownDiagnosticCollisionAbysse - delta);
		_cooldownDrainProfilage += (float)delta;
		TraiterWarmupShadersProgressif((float)delta);
		SurveillerDeriveRuntime((float)delta);
		TraiterAutoHybrideGraphique((float)delta);
		MettreAJourEffetsRemousSuivis();
		if (ActiverAutosauvegarde && IntervalleAutosauvegardeSecondes > 0f)
		{
			_secondesDepuisAutosauvegarde += delta;
			if (_secondesDepuisAutosauvegarde >= IntervalleAutosauvegardeSecondes)
			{
				_secondesDepuisAutosauvegarde = 0;
				ExecuterAutosauvegardeProgressive();
			}
		}

		// Verrou anti-chute : tant que le spawn n'est pas aligné au sol, ancrer le joueur (sans snap visible si déjà au bon endroit).
		if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol && _joueur != null)
		{
			if (_joueur.GlobalPosition.DistanceSquaredTo(_spawnInitialEnAttente) > 0.12f * 0.12f)
				_joueur.GlobalPosition = _spawnInitialEnAttente;
			_joueur.Velocity = Vector3.Zero;
			_joueur.Visible = false;
		}
		// Anti-traversée : mesh visible sans collision sous les pieds → pas de chute libre en attendant la solidification.
		else if (_joueur != null && _joueur.Visible && UseArchitectureReseau && _mondeClient != null
			&& _dimensionLocaleActive != (int)DimensionJeu.Abysse && _joueur.Velocity.Y < -0.2f)
		{
			Vector2I chunkPieds = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
			if (!_mondeClient.ChunkCollisionActive(chunkPieds))
				_joueur.Velocity = new Vector3(_joueur.Velocity.X, 0f, _joueur.Velocity.Z);
		}

		// Garde-fou profondeur extrême Abysse : stabilise l'état physique au fond absolu.
		if (_joueur != null && _dimensionLocaleActive == (int)DimensionJeu.Abysse)
		{
			const float fondAbsolu = ConstantesDimensionAbysse.FondAbsolu;
			float y = _joueur.GlobalPosition.Y;
			// Amorti avant le plancher : évite une décélération infinie « écrasement » sur un seul tick.
			if (y < fondAbsolu + 42f && y > fondAbsolu && _joueur.Velocity.Y < -8f)
				_joueur.Velocity = new Vector3(_joueur.Velocity.X, Mathf.Max(_joueur.Velocity.Y, -22f), _joueur.Velocity.Z);
			if (y <= fondAbsolu)
			{
				_joueur.GlobalPosition = new Vector3(_joueur.GlobalPosition.X, fondAbsolu, _joueur.GlobalPosition.Z);
				_joueur.Velocity = new Vector3(_joueur.Velocity.X * 0.35f, 0f, _joueur.Velocity.Z * 0.35f);
			}
		}
		MettreAJourEmerukedesiParotaromaStage1(delta);

		bool spawnPretActuel = EstSpawnPret();
		bool spawnPretEtAligneActuel = spawnPretActuel && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse && _cooldownDiagnosticCollisionAbysse <= 0.0)
		{
			JournaliserDiagnosticCollisionAbysse();
			_cooldownDiagnosticCollisionAbysse = IntervalleDiagnosticCollisionAbysseSec;
		}
		if (_gateTpDimensionActif)
		{
			_secondesGateTpDimension += delta;
			_cooldownPulseReveilPierresTp = Math.Max(0.0, _cooldownPulseReveilPierresTp - delta);
			if (_cooldownPulseReveilPierresTp <= 0.0)
			{
				_mondeServeur?.ForcerPulseReveilPierres();
				_cooldownPulseReveilPierresTp = IntervallePulseReveilPierresTpSec;
			}
			if (CollisionLocalePretePourTpDimension() || _secondesGateTpDimension >= DureeMaxGateTpDimensionSec)
			{
				_gateTpDimensionActif = false;
				_secondesGateTpDimension = 0.0;
			}
			else if (_overlayChargement != null)
			{
				_overlayChargement.Visible = true;
			}
		}
		if (_dimensionLocaleActive == (int)DimensionJeu.Abysse && UseArchitectureReseau && _joueur != null && _mondeClient != null && !_gateTpDimensionActif)
		{
			bool pretMarcheAbysse = _mondeClient.AbyssePretPourDeplacement(_joueur.GlobalPosition);
			if (!pretMarcheAbysse)
			{
				_verrouMarcheAbysseActif = true;
				_secondesStabiliteMarcheAbysse = 0.0;
				_secondesVerrouMarcheAbysse += delta;
				if (_secondesVerrouMarcheAbysse >= DureeMaxVerrouMarcheAbysseSec)
				{
					// Filet anti-soft-lock: on relâche même si la croix n'est pas encore prête.
					_verrouMarcheAbysseActif = false;
					_secondesVerrouMarcheAbysse = 0.0;
					_secondesStabiliteMarcheAbysse = 0.0;
				}
			}
			else if (_verrouMarcheAbysseActif)
			{
				_secondesVerrouMarcheAbysse += delta;
				_secondesStabiliteMarcheAbysse += delta;
				if (_secondesStabiliteMarcheAbysse >= DureeStabiliteSortieVerrouMarcheAbysseSec
					|| _secondesVerrouMarcheAbysse >= DureeMaxVerrouMarcheAbysseSec)
				{
					_verrouMarcheAbysseActif = false;
					_secondesVerrouMarcheAbysse = 0.0;
					_secondesStabiliteMarcheAbysse = 0.0;
				}
			}
			else
			{
				_secondesVerrouMarcheAbysse = 0.0;
				_secondesStabiliteMarcheAbysse = 0.0;
			}
		}
		else
		{
			_verrouMarcheAbysseActif = false;
			_secondesVerrouMarcheAbysse = 0.0;
			_secondesStabiliteMarcheAbysse = 0.0;
		}
		_chargementAbysseEnCours = false;
		_secondesStabiliteAbyssePret = 0.0;
		_secondesVerrouAbysse = 0.0;
		_cooldownRearmementVerrouAbysse = 0.0;
		// Le cycle solaire ne doit être neutralisé que pendant le bootstrap strict du spawn.
		// IMPORTANT: ne pas lier le ciel aux cardinaux, sinon le cycle peut rester figé alors que le joueur est déjà jouable.
		bool chargementVisuelActif = _overlayChargement != null
			&& _overlayChargement.Visible
			&& (!spawnPretEtAligneActuel || _gateTpDimensionActif);
		MettreAJourEtatCycleSolaire(chargementVisuelActif);

		_secondesChargementMondeAbsolu += delta;
		// Masquer l'overlay quand le sol minimal sous les pieds est prêt, ou après timeout (évite chargement infini si file / grille trop large).
		if (_overlayChargement != null && _overlayChargement.Visible)
		{
			if (_labelChargementPrincipal != null && _labelChargementPrincipal.Text != "Chargement du monde...")
				_labelChargementPrincipal.Text = "Chargement du monde...";
			_secondesOverlayChargement += delta;
			double secondesAttenteEffective = Math.Max(_secondesOverlayChargement, _secondesChargementMondeAbsolu);
			_cooldownRenfortSpawnChunks = Math.Max(0.0, _cooldownRenfortSpawnChunks - delta);
			if (_mondeClient != null && secondesAttenteEffective >= 3.0 && !spawnPretActuel && _cooldownRenfortSpawnChunks <= 0.0)
			{
				_cooldownRenfortSpawnChunks = IntervalleRenfortSpawnChunksSec;
				Vector2I chunkSpawn = WorldToChunkCoord(ObtenirPointReferenceSpawn(), TailleChunk);
				_mondeClient.ReserverChunkSpawnPrioritaire(chunkSpawn);
			}
			_cooldownLogDiagnosticChargement = Math.Max(0.0, _cooldownLogDiagnosticChargement - delta);
			if (_cooldownLogDiagnosticChargement <= 0.0)
			{
				_cooldownLogDiagnosticChargement = IntervalleLogDiagnosticChargementSec;
				GD.Print($"ZERO-K CHARGEMENT: attente={secondesAttenteEffective:0.0}s spawnPret={spawnPretActuel} aligne={_spawnAligneAuSol} gateTp={_gateTpDimensionActif}");
			}
			bool forcerMasquageAbsolu = secondesAttenteEffective >= TimeoutAbsoluOverlayChargementSec;
			if (_gateTpDimensionActif && _secondesGateTpDimension < DureeMaxGateTpDimensionSec && !forcerMasquageAbsolu)
			{
				// Gate TP : ne bloque le masquage que le temps du transfert dimensionnel (~8 s max), sauf plafond absolu.
				goto FinBlocOverlay;
			}
			bool spawnPret = spawnPretActuel;
			// Nouveau monde : raycast dès que le chunk central a collision (les cardinaux arrivent ensuite).
			if (spawnPret && _spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
				FinaliserSpawnInitialAuSol();
			bool spawnPretEtAligne = spawnPret && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
			// Fallback UX : chunk local prêt → alignement + masquage overlay (nouveau monde inclus).
			if (!spawnPretEtAligne && _joueur != null && secondesAttenteEffective >= 4.0)
			{
				if (spawnPret)
				{
					if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
						FinaliserSpawnInitialAuSol(autoriserFallbackSansRaycast: secondesAttenteEffective >= 12.0);
					spawnPretEtAligne = spawnPret && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
				}
			}
			const double timeoutOverlaySec = 45.0;
			bool meshSousPiedsPret = UseArchitectureReseau && _mondeClient != null && _mondeClient.ChunkMeshGrilleSousPiedsPret();
			bool peutMasquerOverlayVisuel = meshSousPiedsPret && secondesAttenteEffective >= 6.0;
			if (spawnPretEtAligne || peutMasquerOverlayVisuel || secondesAttenteEffective >= timeoutOverlaySec || forcerMasquageAbsolu)
			{
				bool bootstrapClientStable = !UseArchitectureReseau
					|| _mondeClient == null
					|| _mondeClient.BootstrapInitialStabilise()
					|| !ExigerBootstrapClientStableAvantMasquerOverlay
					|| secondesAttenteEffective >= Math.Max(0.0f, DureeMaxAttenteBootstrapClientSec)
					|| (spawnPretEtAligne && secondesAttenteEffective >= 4.0)
					|| (peutMasquerOverlayVisuel && secondesAttenteEffective >= 8.0)
					|| forcerMasquageAbsolu;
				if (!bootstrapClientStable)
				{
					// On garde l’overlay un peu plus longtemps pour préchauffer collision/files et lisser les premières secondes de déplacement.
					goto FinBlocOverlay;
				}
				if (!spawnPretEtAligne && (secondesAttenteEffective >= timeoutOverlaySec || forcerMasquageAbsolu))
					GD.PrintErr($"ZERO-K : Timeout chargement monde ({secondesAttenteEffective:0.0} s) — overlay masqué. Vérifiez réseau / Monde_Client si le sol manque.");
				if (_spawnDoitEtreAligneAuSol && !_spawnAligneAuSol)
					FinaliserSpawnInitialAuSol(autoriserFallbackSansRaycast: secondesAttenteEffective >= 12.0 || forcerMasquageAbsolu || peutMasquerOverlayVisuel);
				_overlayChargement.Visible = false;
				// Reconnexion / reload : éviter l'écran gris vide si le raycast sol n'a pas encore de collision mais le monde charge.
				if (_joueur != null && !_joueur.Visible)
					_joueur.Visible = true;
				if (_ajusterPiedsJoueurSurSurfaceApresRestauration)
				{
					_ajusterPiedsJoueurSurSurfaceApresRestauration = false;
					Callable.From(AjusterJoueurPositionRestaureeSurSurfaceProche).CallDeferred();
				}
			}
		}
FinBlocOverlay:

		bool spawnPretEtAlignePourRestauration = EstSpawnPret() && (!_spawnDoitEtreAligneAuSol || _spawnAligneAuSol);
		bool restaurationSolVientDeTourner = EssayerRestaurerObjetsPersistantsPhaseSol(spawnPretEtAlignePourRestauration);
		// Réécrit inventaire + placed_objects + chunks après reload. Décalé de quelques frames : une sauvegarde
		// immédiate dans la même frame que la restauration sol a rarement provoqué un plantage moteur (PagedArray hors limites).
		if (restaurationSolVientDeTourner && !_synchronisationDisquePostRestaurationSolEffectuee)
		{
			_synchronisationDisquePostRestaurationSolEffectuee = true;
			CallDeferred(nameof(LancerSynchronisationDisquePostRestaurationSolDifferee));
		}
		int budgetDepgelSol = Mathf.Clamp(64 + _rigidBodiesAttenteCollisionSolRestauration.Count / 2, 64, 256);
		TraiterDepgelRigidBodiesRestaurationSol(budgetDepgelSol);

		_worldUiFacade.MettreAJourEntetesMonde(
			_joueur,
			_labelCoords,
			_labelHeureDimension,
			_dimensionLocaleActive,
			ObtenirServeurDimension,
			FuseauHoraireHeures,
			ref _dernieresCoordsAffichees,
			ref _dernierTexteHeureDimension);

		if (UseArchitectureReseau)
		{
			_secondesDormanceObjets += delta;
			float intervalleDormanceObjets = 0.4f;
			if (_cacheRigidBodiesDormance.TryGetValue("BlocsPoses", out var listeBlocs) && listeBlocs.Count > 180)
				intervalleDormanceObjets = 0.85f;
			float fps = (float)Engine.GetFramesPerSecond();
			if (fps < 22f)
				intervalleDormanceObjets = Mathf.Max(intervalleDormanceObjets, 1.1f);
			if (_secondesDormanceObjets >= intervalleDormanceObjets)
			{
				_secondesDormanceObjets = 0;
				ulong debutDormanceUs = ActiverProfilagePerfGestionnaire ? PerfBudgetMonitor.Begin() : 0UL;
				MettreAJourDormanceObjetsPoses((float)delta);
				MettreAJourLodObjetsAuSol((float)delta);
				if (ActiverProfilagePerfGestionnaire)
					PerfBudgetMonitor.End("GestionnaireMonde/DormanceObjets", debutDormanceUs);
			}
			// Monde_Client gère son propre _Process
			if (ActiverProfilagePerfGestionnaire)
			{
				PerfBudgetMonitor.End("GestionnaireMonde/Process", debutProcessUs);
				if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageSec))
				{
					_cooldownDrainProfilage = 0f;
					PerfBudgetMonitor.FlushSiEchu("GestionnaireMonde", IntervalleLogProfilageSec);
				}
			}
			return;
		}

		// Legacy : goutte-à-goutte visuel (1 mesh/frame max, évite Upload Stall VRAM)
		const int MaxMeshesParFrame = 2;
		int actionsExecutees = 0;
		while (actionsExecutees < MaxMeshesParFrame && _misesAJourUrgentes.TryDequeue(out var a))
		{
			a.Invoke();
			actionsExecutees++;
		}
		while (actionsExecutees < MaxMeshesParFrame && _misesAJourMainThread.TryDequeue(out var a))
		{
			a.Invoke();
			actionsExecutees++;
		}

		Vector2I cj = ObtenirCoordonneesChunkJoueur();
		bool chunkChange = cj != _ancienChunkJoueur;
		if (chunkChange) _ancienChunkJoueur = cj;

		// Radar strict : uniquement quand le joueur change de chunk (zéro alloc quand immobile)
		if (chunkChange)
			ActualiserVisibiliteEtTriChunksLegacy();

		int n = 0;
		while (_chunksACharger.Count > 0 && n < MaxChunksParFrame)
		{
			Vector2I c = _chunksACharger[0];
			_chunksACharger.RemoveAt(0);
			LancerGenerationChunk(c.X, c.Y);
			n++;
		}

		// Eau runtime purement événementielle (legacy) : uniquement file des voxels réveillés.
		if (_fileEau.Count > 0)
		{
			_tickEauLegacy++;
			int eauCount = Math.Min(_fileEau.Count, MaxEauParTick);
			for (int i = 0; i < eauCount; i++)
			{
				Vector3I pos = _fileEau.Dequeue();
				_eauActive.Remove(pos);
				if (!EstVoxelEauLegacy(pos)) continue;
				Vector3I posBas = pos + new Vector3I(0, -1, 0);
				if (posBas.Y < 0) { DefinirVoxelLegacy(pos, 0); DemanderMiseAJourMeshLegacy(pos); continue; }
				if (EstVoxelAirLegacy(posBas))
				{
					DefinirVoxelLegacy(posBas, 4);
					DefinirVoxelLegacy(pos, 0);
					MemoriserFluxEauLegacy(pos, posBas);
					ActiverEauLegacy(posBas);
					DemanderMiseAJourMeshLegacy(pos);
					DemanderMiseAJourMeshLegacy(posBas);
					ReveillerEauAdjacenteLegacy(new Vector3(pos.X, pos.Y, pos.Z));
					continue;
				}
				bool aPression = EstVoxelEauLegacy(pos + new Vector3I(0, 1, 0));
				foreach (var d in DirEauHorizLegacy)
				{
					Vector3I pc = pos + d, pcb = pc + new Vector3I(0, -1, 0);
					if (!EstVoxelAirLegacy(pc)) continue;
					if (!PeutCoulerVersLegacy(pos, pc)) continue;
					if (aPression || EstVoxelAirLegacy(pcb))
					{
						DefinirVoxelLegacy(pc, 4);
						DefinirVoxelLegacy(pos, 0);
						MemoriserFluxEauLegacy(pos, pc);
						ActiverEauLegacy(pc);
						DemanderMiseAJourMeshLegacy(pos);
						DemanderMiseAJourMeshLegacy(pc);
						ReveillerEauAdjacenteLegacy(new Vector3(pos.X, pos.Y, pos.Z));
						break;
					}
				}
			}
		}
		if (ActiverProfilagePerfGestionnaire)
		{
			PerfBudgetMonitor.End("GestionnaireMonde/Process", debutProcessUs);
			if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageSec))
			{
				_cooldownDrainProfilage = 0f;
				PerfBudgetMonitor.FlushSiEchu("GestionnaireMonde", IntervalleLogProfilageSec);
			}
		}
	}

	/// <summary>
	/// Filet de sécurité anti-crash : sauvegarde régulière du joueur et d'un lot de chunks actifs.
	/// La sauvegarde complète reste assurée par le bouton manuel, _Notification et _ExitTree.
	/// </summary>
	private void ExecuterAutosauvegardeProgressive()
	{
		ulong debutAutosaveUs = ActiverProfilagePerfGestionnaire ? PerfBudgetMonitor.Begin() : 0UL;
		if (_joueur != null)
		{
			GameState.Instance?.SauvegarderPositionJoueur(_joueur.GlobalPosition);
			SauvegarderSessionJoueur(_dimensionLocaleActive, _joueur.GlobalPosition);
		}
		if (_joueur is Joueur j)
		{
			if (_restaurationPersistantPhaseJoueurFaite)
			{
				if (_restaurationPersistantObjetsSolFaite)
					j.SauvegarderEtatPersistantMonde(GetTree());
				else
					j.SauvegarderEtatPersistantJoueurSeulement();
			}
		}

		if (UseArchitectureReseau)
		{
			int budget = Mathf.Max(1, MaxChunksAutosauvegardeParCycle);
			int n = 0;
			int backlogDirty = 0;
			int backlogDecharge = 0;
			foreach (var kv in _serveurParDimension)
			{
				n += kv.Value?.SauvegarderChunksActifsProgressif(budget) ?? 0;
				var b = kv.Value?.ObtenirBacklogsPersistance() ?? (0, 0);
				backlogDirty += b.Item1;
				backlogDecharge += b.Item2;
			}
			if (n > 0 || (_cooldownLogAutosaveDiag <= 0f && (backlogDirty > 0 || backlogDecharge > 0)))
			{
				GD.Print($"ZERO-K : Autosauvegarde progressive ({n} chunk(s)).");
				GD.Print($"ZERO-K PERF: backlog persistance dirty={backlogDirty} decharge={backlogDecharge} budget={budget}.");
				_cooldownLogAutosaveDiag = 15f;
			}
		}
		if (ActiverProfilagePerfGestionnaire)
			PerfBudgetMonitor.End("GestionnaireMonde/Autosave", debutAutosaveUs);
	}
}
