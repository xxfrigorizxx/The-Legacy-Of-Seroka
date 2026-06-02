using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class Monde_Client : Node3D
{
	/// <summary>
	/// Applique le gate FPS et le ramp-up post-dégel sur un budget de streaming.
	/// - Si la zone est critique (doitGarantirProcheJoueur=true), retourne budgetActuel inchangé (anti-chute).
	/// - Sinon, si gelé (FPS &lt; seuil), retourne 0 (arrêt net du streaming non-critique).
	/// - Sinon pendant le ramp-up, interpole de 1 vers budgetActuel sur DureeRampUpPostDegel secondes.
	/// </summary>
	private int AppliquerGateEtRampUp(int budgetActuel, bool doitGarantirProcheJoueur, int minSortieGel = 1)
	{
		if (doitGarantirProcheJoueur || _streamingChunksPrioritaireCetteFrame) return budgetActuel;
		// Grâce post-panneau graphismes : sinon le gate peut bloquer tout (0 requête chunk) malgré un RenderDistance élevé.
		if (_timerGraceStreamingReglageUtilisateur > 0f) return budgetActuel;
		if (_timerGraceStreamingBootstrap > 0f) return budgetActuel;
		if (!ActiverGateFpsStrict) return budgetActuel;
		// Ne pas renvoyer 0 : bloque toute intégration mesh / requête distante → sol visible sous les pieds seulement, arbres flottants.
		if (_gateStreamingGele) return minSortieGel;
		if (_tempsDepuisDegel < DureeRampUpPostDegel)
		{
			float t = Mathf.Clamp(_tempsDepuisDegel / Mathf.Max(0.01f, DureeRampUpPostDegel), 0f, 1f);
			int plafond = Mathf.Max(minSortieGel, Mathf.RoundToInt(Mathf.Lerp(minSortieGel, Mathf.Max(minSortieGel, budgetActuel), t)));
			return Mathf.Min(budgetActuel, plafond);
		}
		return budgetActuel;
	}

	/// <summary>
	/// Budget flore dynamique avec garde-fou visuel:
	/// - applique urgence + gate/ramp-up;
	/// - évite un budget 0 prolongé en déplacement (herbe/buissons invisibles).
	/// </summary>
	private int CalculerBudgetFloreDynamique(bool enChargement, bool prioriteJoueur)
	{
		int budgetFlore = enChargement
			? Mathf.Max(1, MaxFloreParFrameChargement)
			: Mathf.Max(1, MaxFloreParFrameExploration);

		if (!enChargement && _niveauUrgencePerf >= 2)
			budgetFlore = Mathf.Max(1, Mathf.Min(budgetFlore, 1));
		else if (!enChargement && _niveauUrgencePerf == 1)
			budgetFlore = Mathf.Max(1, Mathf.Min(budgetFlore, 2));

		if (!enChargement)
			budgetFlore = AppliquerGateEtRampUp(budgetFlore, false, 1);

		// Même sous gate sévère, garder un flux minimal quand on se déplace et que la file n'est pas vide.
		if (!enChargement && budgetFlore <= 0 && _fileFloreDifferee.Count > 0 && (prioriteJoueur || _joueur != null))
			budgetFlore = 1;

		return Mathf.Max(0, budgetFlore);
	}

	private void MettreAJourAutoDiagnostic(float dt)
	{
		if (_timerFreinSpike > 0f)
			_timerFreinSpike = Mathf.Max(0f, _timerFreinSpike - dt);
		if (ActiverAntiSpikeFrameTime)
		{
			float frameMs = dt * 1000f;
			float seuilSpike = Mathf.Clamp(SeuilSpikeFrameMs, 14f, 45f);
			if (frameMs >= seuilSpike)
				_timerFreinSpike = Mathf.Max(_timerFreinSpike, Mathf.Clamp(DureeFreinSpikeSec, 0.08f, 1.2f));
		}

		if (!ModeAutoDiagnosticAdaptatif)
		{
			_ratioChargeAuto = 1f;
			_facteurMouvementAuto = 1f;
			_niveauUrgencePerf = 0;
			_maxAjoutsRadarParPasseDyn = MaxAjoutsRadarParPasse;
			_maxRequetesDyn = Mathf.Max(1, MaxChunksParFrame);
			_maxTravailleursDyn = Mathf.Clamp(MaxTravailleursCalcul, 2, 16);
			_maxTransitionsDormanceDyn = 64;
			_intervalleCullingDyn = 0.03f;
			_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile;
			_maxBasculesCullingDyn = Mathf.Max(8, MaxBasculesCullingParPasse);
			if (_timerFreinSpike > 0f)
			{
				_maxAjoutsRadarParPasseDyn = Mathf.Max(140, Mathf.RoundToInt(_maxAjoutsRadarParPasseDyn * 0.72f));
				_maxRequetesDyn = Mathf.Max(2, Mathf.RoundToInt(_maxRequetesDyn * 0.65f));
				_maxTransitionsDormanceDyn = Mathf.Max(10, Mathf.RoundToInt(_maxTransitionsDormanceDyn * 0.62f));
				_maxBasculesCullingDyn = Mathf.Max(24, Mathf.RoundToInt(_maxBasculesCullingDyn * 0.52f));
				_intervalleCullingDyn *= 1.35f;
			}
			return;
		}

		float fps = (float)Engine.GetFramesPerSecond();
		if (fps > 1f)
		{
			float alpha = Mathf.Clamp(dt * 2.0f, 0.04f, 0.22f);
			_fpsMoyenneAuto = Mathf.Lerp(_fpsMoyenneAuto, fps, alpha);
		}

		if (_timerGraceStreamingBootstrap > 0f)
			_timerGraceStreamingBootstrap = Mathf.Max(0f, _timerGraceStreamingBootstrap - dt);

		// === Gate FPS strict : ralentit le lointain si FPS bas — pas pendant bootstrap / monde incomplet. ===
		if (ActiverGateFpsStrict && _timerGraceStreamingReglageUtilisateur <= 0f && _timerGraceStreamingBootstrap <= 0f)
		{
			float fpsInstant = fps > 1f ? fps : _fpsMoyenneAuto;
			// Utilise à la fois FPS instantané et moyen pour une réaction rapide sans bruit.
			float fpsSignal = Mathf.Min(fpsInstant, _fpsMoyenneAuto);
			_tempsEtatGate += dt;
			if (!_gateStreamingGele)
			{
				if (fpsSignal < SeuilFpsGateStrict && _tempsEtatGate >= Mathf.Max(0.05f, DureeMinEtatOuvertSec))
				{
					_gateStreamingGele = true;
					_tempsFpsStableHaut = 0f;
					_tempsDepuisDegel = 0f;
					_tempsEtatGate = 0f;
				}
			}
			else
			{
				if (fpsSignal >= SeuilFpsGateReprise)
					_tempsFpsStableHaut += dt;
				else
					_tempsFpsStableHaut = 0f;
				if (_tempsFpsStableHaut >= DureeStabiliteReprise && _tempsEtatGate >= Mathf.Max(0.05f, DureeMinEtatGeleSec))
				{
					_gateStreamingGele = false;
					_tempsDepuisDegel = 0f;
					_tempsEtatGate = 0f;
				}
			}
			if (!_gateStreamingGele)
				_tempsDepuisDegel = Mathf.Min(_tempsDepuisDegel + dt, DureeRampUpPostDegel + 1f);
		}
		else
		{
			_gateStreamingGele = false;
			_tempsDepuisDegel = DureeRampUpPostDegel + 1f;
			_tempsEtatGate = DureeMinEtatOuvertSec + 1f;
		}

		float cible = Mathf.Clamp(FpsCibleAutoDiagnostic, 45, 240);
		float ratio = Mathf.Clamp(_fpsMoyenneAuto / cible, RatioChargeMinimumAuto, 1.15f);
		if (_fpsMoyenneAuto < 22f) ratio *= 0.35f;
		else if (_fpsMoyenneAuto < 30f) ratio *= 0.45f;
		else if (_fpsMoyenneAuto < 45f) ratio *= 0.60f;
		else if (_fpsMoyenneAuto < 55f) ratio *= 0.75f;
		else if (_fpsMoyenneAuto < 70f) ratio *= 0.88f;
		_ratioChargeAuto = Mathf.Clamp(ratio, RatioChargeMinimumAuto, 1.1f);
		int seuilForte = Mathf.Clamp(SeuilFpsUrgenceForte, 20, 59);
		int seuilCritique = Mathf.Clamp(SeuilFpsUrgenceCritique, 15, seuilForte - 1);
		int seuilExtreme = Mathf.Clamp(SeuilFpsUrgenceExtreme, 10, seuilCritique);
		int seuilSortieExtreme = Mathf.Clamp(SeuilFpsSortieUrgenceExtreme, seuilForte, 90);
		if (!ModeSurvieFpsAgressif)
			_niveauUrgencePerf = 0;
		else
		{
			// Hystérésis anti-pompage: une fois en mode extrême, on n'en sort qu'au-dessus d'un seuil plus haut.
			if (_niveauUrgencePerf >= 3)
			{
				if (_fpsMoyenneAuto >= seuilSortieExtreme) _niveauUrgencePerf = 1;
				else _niveauUrgencePerf = 3;
			}
			else if (_fpsMoyenneAuto <= seuilExtreme)
				_niveauUrgencePerf = 3;
			else if (_fpsMoyenneAuto <= seuilCritique)
				_niveauUrgencePerf = 2;
			else if (_fpsMoyenneAuto <= seuilForte)
				_niveauUrgencePerf = 1;
			else
				_niveauUrgencePerf = 0;
		}

		float vitesseXZ = 0f;
		if (EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
		{
			Vector3 vel = joueurRef.Velocity;
			vitesseXZ = Mathf.Sqrt(vel.X * vel.X + vel.Z * vel.Z);
		}
		float tMouvement = Mathf.Clamp((vitesseXZ - 0.6f) / 5.0f, 0f, 1f);
		_facteurMouvementAuto = Mathf.Lerp(1f, 0.54f, tMouvement);
		float ratioStable = Mathf.Clamp(_ratioChargeAuto * _facteurMouvementAuto, RatioChargeMinimumAuto, 1.05f);
		if (_timerFreinSpike > 0f)
			ratioStable = Mathf.Clamp(ratioStable * 0.64f, RatioChargeMinimumAuto, 1.05f);

		int cpuCount = Math.Max(1, System.Environment.ProcessorCount);
		_maxAjoutsRadarParPasseDyn = Mathf.Clamp(Mathf.RoundToInt(MaxAjoutsRadarParPasse * ratioStable), 24, MaxAjoutsRadarParPasse);
		_maxRequetesDyn = Mathf.Clamp(Mathf.RoundToInt(MaxChunksParFrame * Mathf.Lerp(0.35f, 1.20f, ratioStable)), 1, 56);
		_maxTravailleursDyn = Mathf.Clamp(
			Mathf.RoundToInt(MaxTravailleursCalcul * Mathf.Lerp(0.30f, 1.05f, ratioStable)),
			1,
			Mathf.Clamp(cpuCount - 1, 1, 12));
		_maxTransitionsDormanceDyn = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(6f, 96f, ratioStable)), 4, 120);
		_intervalleCullingDyn = Mathf.Lerp(0.14f, 0.02f, ratioStable);
		_intervalleRadarImmobileDyn = IntervalleRafraichissementRadarImmobile * Mathf.Lerp(2.4f, 0.82f, ratioStable);
		_maxBasculesCullingDyn = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(18f, Mathf.Max(18, MaxBasculesCullingParPasse), ratioStable)), 12, Mathf.Max(12, MaxBasculesCullingParPasse));
		if (_niveauUrgencePerf >= 3)
		{
			_maxAjoutsRadarParPasseDyn = Mathf.Min(_maxAjoutsRadarParPasseDyn, 18);
			_maxRequetesDyn = Mathf.Min(_maxRequetesDyn, 1);
			_maxTravailleursDyn = 1;
			_maxTransitionsDormanceDyn = Mathf.Min(_maxTransitionsDormanceDyn, 6);
			_maxBasculesCullingDyn = Mathf.Max(28, Mathf.Min(_maxBasculesCullingDyn, 44));
			_intervalleCullingDyn = Mathf.Max(_intervalleCullingDyn, 0.18f);
			_intervalleRadarImmobileDyn = Mathf.Max(_intervalleRadarImmobileDyn, IntervalleRafraichissementRadarImmobile * 3.2f);
		}
		else if (_fpsMoyenneAuto < 30f)
		{
			_maxAjoutsRadarParPasseDyn = Mathf.Min(_maxAjoutsRadarParPasseDyn, 42);
			_maxRequetesDyn = Mathf.Min(_maxRequetesDyn, 2);
			_maxTravailleursDyn = 1;
			_maxTransitionsDormanceDyn = Mathf.Min(_maxTransitionsDormanceDyn, 10);
			_maxBasculesCullingDyn = Mathf.Max(24, Mathf.Min(_maxBasculesCullingDyn, 40));
			_intervalleCullingDyn = Mathf.Max(_intervalleCullingDyn, 0.14f);
			_intervalleRadarImmobileDyn = Mathf.Max(_intervalleRadarImmobileDyn, IntervalleRafraichissementRadarImmobile * 2.4f);
		}
		if (_timerFreinSpike > 0f)
		{
			_maxBasculesCullingDyn = Mathf.Max(20, Mathf.RoundToInt(_maxBasculesCullingDyn * 0.55f));
			_intervalleCullingDyn *= 1.25f;
		}
	}

	public void EnqueueMiseAJourMainThread(Action action) => _misesAJourMainThread.Enqueue(action);
	public void EnqueueMiseAJourUrgente(Action action) => _misesAJourUrgentes.Enqueue(action);

	/// <summary>Dépose un travail d'intégration (mesh, collision, flore) avec coût estimé pour respecter un budget de triangles par frame.</summary>
	public void EnqueueIntegration(Action action, int coutVerticesEstime = 12000)
	{
		if (action == null) return;
		_fileIntegrationMainThread.Enqueue(new TacheIntegration(action, Mathf.Max(1, coutVerticesEstime)));
	}

	private void AjouterEnFileSolidification(ChunkData data)
	{
		if (data == null || _setSolidificationNormale.Contains(data))
			return;
		_fileAttenteSolidification.Add(data);
		_setSolidificationNormale.Add(data);
		data.EstEnFileSolidification = true;
	}

	private void RetirerDeFileSolidification(ChunkData data)
	{
		if (data == null || !_setSolidificationNormale.Remove(data))
			return;
		_fileAttenteSolidification.Remove(data);
		data.EstEnFileSolidification = false;
	}

	private void DemanderRafraichissementRadar(Vector3 positionObservation, float cooldownSec)
	{
		_positionRadarEnAttente = positionObservation;
		_rebuildRadarEnAttente = true;
		if (_radarEnCours || _cooldownRebuildRadar > 0f)
			return;
		_rebuildRadarEnAttente = false;
		_cooldownRebuildRadar = Mathf.Max(0.01f, cooldownSec);
		ActualiserVisibiliteEtTriChunks(positionObservation);
	}

	public bool BootstrapInitialStabilise()
	{
		// Profondeur (tranches 100 m) : au boot, un chunk sous les pieds suffit ; la grille 5×5 se remplit en jeu.
		if (ModeProfondeurTranchesActif())
		{
			if (!EssayerObtenirJoueurDansArbre(out CharacterBody3D joueurRef))
				return false;
			Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(joueurRef.GlobalPosition, TailleChunk);
			if (!ChunkCollisionActive(c))
				return false;
		}
		else if (!ChunkSousPiedsAPret())
			return false;
		int seuilBacklog = ModeProfondeurTranchesActif()
			? Mathf.Max(SeuilBacklogBootstrapStable, 96)
			: SeuilBacklogBootstrapStable;
		if (CompterBacklog() > Mathf.Max(0, seuilBacklog))
			return false;
		if (ExigerSolidificationVidePourBootstrap
			&& (_fileAttenteSolidificationUrgente.Count > 0 || _fileAttenteSolidification.Count > 0))
			return false;
		return true;
	}
}
