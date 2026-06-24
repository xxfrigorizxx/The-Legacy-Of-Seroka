using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void GererReproductionEtGestation()
	{
		if (_etat == EtatBoeuf.Mort || GetParent() == null)
			return;

		MettreAJourMaturiteVeau();
		if (!ActiverReproductionFaune || !EstFemelle || _estVeauActif)
			return;

		if (_estEnGestation)
		{
			if (_tempsGestationRestant <= 0f)
				DonnerNaissance();
			return;
		}
	}

	private void MettreAJourMaturiteVeau()
	{
		if (!_estVeauActif)
			return;
		if (_ageSecondes < Mathf.Max(60f, DureeVeauAvantMaturiteSecondes))
			return;
		_estVeauActif = false;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourStatsDerivees();
		EmitSignal(SignalName.EvolutionEvenement, "maturite_veau", 1f, _niveau, _ageSecondes / 3600f);
	}

	private void TenterConceptionJournaliereSelective()
	{
		if (_tentativeReproductionJourEffectuee || !EstFemelle || _estVeauActif || _estEnGestation)
			return;
		_tentativeReproductionJourEffectuee = true;
		if (_faimCourante < SeuilRechercheHerbe * 0.85f)
			return;
		if (!TrouverMaleSurvivantPrioritairePourReproduction(out BoeufSauvage male))
			return;
		if (!male.PeutParticiperCommeMalePourReproduction())
			return;
		if (_rng.Randf() > Mathf.Clamp(ChanceConceptionJournaliere, 0f, 1f))
			return;
		CommencerGestationAvec(male);
	}

	private bool PeutParticiperCommeMalePourReproduction()
	{
		if (!EstTaureau || _etat == EtatBoeuf.Mort || _estVeauActif || _tentativeReproductionJourEffectuee)
			return false;
		if (_faimCourante < SeuilRechercheHerbe * 0.8f)
			return false;
		return true;
	}

	private void EvaluerAdaptationComportementaleSelonEnvironnement()
	{
		if (!ActiverEvolutionEnvironnementale || _etat == EtatBoeuf.Mort)
			return;

		float intensite = Mathf.Max(0.001f, IntensiteAdaptationComportementale);
		float ratioStamina = _staminaMaxActuelle > 0.01f ? _staminaCourante / _staminaMaxActuelle : 0f;
		bool estStress = _tempsFuite > 0f || _memoireDetectionJoueur > 0f;
		bool environnementStable = _faimCourante > Mathf.Max(10f, SeuilRechercheHerbe) && ratioStamina > 0.55f && !estStress;
		bool procheTroupe = false;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population != null)
		{
			int voisins = 0;
			float rayon2 = Mathf.Max(4f, RayonRassemblement);
			rayon2 *= rayon2;
			for (int i = 0; i < population.Count; i++)
			{
				BoeufSauvage b = population[i];
				if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
					continue;
				if (GlobalPosition.DistanceSquaredTo(b.GlobalPosition) <= rayon2)
					voisins++;
			}
			procheTroupe = voisins >= 2;
		}

		if (environnementStable)
		{
			_geneConfiance = Mathf.Clamp(_geneConfiance + intensite * 0.7f, 0f, 1f);
			_geneReflexeFuite = Mathf.Clamp(_geneReflexeFuite - intensite * 0.45f, 0f, 1f);
		}
		if (procheTroupe)
		{
			_geneConfiance = Mathf.Clamp(_geneConfiance + intensite * 0.55f, 0f, 1f);
			_geneReflexeAttaque = Mathf.Clamp(_geneReflexeAttaque + intensite * 0.18f, 0f, 1f);
		}
		if (estStress)
		{
			_geneReflexeFuite = Mathf.Clamp(_geneReflexeFuite + intensite * 0.85f, 0f, 1f);
			_geneConfiance = Mathf.Clamp(_geneConfiance - intensite * 0.55f, 0f, 1f);
		}
		if (_etat == EtatBoeuf.Charge)
			_geneReflexeAttaque = Mathf.Clamp(_geneReflexeAttaque + intensite * 0.7f, 0f, 1f);
		if (_etat == EtatBoeuf.Fuite)
			_geneReflexeAttaque = Mathf.Clamp(_geneReflexeAttaque - intensite * 0.35f, 0f, 1f);

		EvaluerDeblocagesAdaptationEnvironnement(environnementStable, procheTroupe, estStress);

		if (environnementStable || procheTroupe || estStress)
			EmitSignal(SignalName.EvolutionEvenement, "adaptation_environnement", intensite, _niveau, _ageSecondes / 3600f);
	}

	private void EvaluerDeblocagesAdaptationEnvironnement(bool environnementStable, bool procheTroupe, bool estStress)
	{
		float scoreCible =
			Mathf.Clamp(_geneConfiance, 0f, 1f) * 0.45f +
			(1f - Mathf.Clamp(_geneReflexeFuite, 0f, 1f)) * 0.35f +
			Mathf.Clamp(_geneReflexeAttaque, 0f, 1f) * 0.20f;
		if (environnementStable)
			scoreCible += 0.08f;
		if (procheTroupe)
			scoreCible += 0.06f;
		if (estStress)
			scoreCible -= 0.10f;

		scoreCible = Mathf.Clamp(scoreCible, 0f, 1f);
		_scoreAdaptationEnvironnement = Mathf.Lerp(_scoreAdaptationEnvironnement, scoreCible, 0.35f);

		VerifierDeblocage(ref _deblocageAnimationContextuelle, _scoreAdaptationEnvironnement >= 0.42f, "deblocage_animation_contextuelle");
		VerifierDeblocage(ref _deblocageStrategieTroupeau, procheTroupe && _scoreAdaptationEnvironnement >= 0.58f, "deblocage_pensee_troupeau");
		VerifierDeblocage(ref _deblocageAffichageTroupeau, _scoreAdaptationEnvironnement >= 0.50f, "deblocage_affichage_troupeau");
	}

	private void CommencerGestationAvec(BoeufSauvage male)
	{
		if (male == null || !GodotObject.IsInstanceValid(male))
			return;
		_estEnGestation = true;
		_tentativeReproductionJourEffectuee = true;
		_maleGestationReference = male;
		_tempsGestationRestant = Mathf.Max(10f, DureeGestationSecondes);
		_cooldownReproduction = Mathf.Max(5f, CooldownReproductionSecondes * 0.35f);
		male._tentativeReproductionJourEffectuee = true;
		male._cooldownReproduction = Mathf.Max(5f, male.CooldownReproductionSecondes * 0.35f);
		AjouterExperience(1.2f, "conception");
		male.AjouterExperience(0.6f, "reproduction");
	}

	private bool TrouverMaleProchePourReproduction(out BoeufSauvage male)
	{
		male = null;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null) return false;
		float meilleure = float.MaxValue;
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (!b.EstTaureau)
				continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
			if (d2 < meilleure && d2 <= 14f * 14f)
			{
				meilleure = d2;
				male = b;
			}
		}
		return male != null;
	}

	private bool TrouverMaleSurvivantPrioritairePourReproduction(out BoeufSauvage male)
	{
		male = null;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null)
			return false;
		float rayon = Mathf.Max(5f, RayonReproductionJour);
		float rayon2 = rayon * rayon;
		_scratchCandidatsReproduction.Clear();
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (!b.EstTaureau || b._estVeauActif || b._tentativeReproductionJourEffectuee)
				continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
			if (d2 > rayon2)
				continue;
			_scratchCandidatsReproduction.Add(b);
		}
		if (_scratchCandidatsReproduction.Count == 0)
			return false;

		_scratchCandidatsReproduction.Sort((a, b) => b._ageSecondes.CompareTo(a._ageSecondes));
		int garder = Mathf.Clamp(MaxCandidatsMalesParAge, 1, _scratchCandidatsReproduction.Count);
		float meilleurScore = float.MinValue;
		for (int i = 0; i < garder; i++)
		{
			BoeufSauvage c = _scratchCandidatsReproduction[i];
			float ageScore = Mathf.Clamp(c._ageSecondes / Mathf.Max(1f, _ageSecondes + 1f), 0f, 2.5f);
			float proxScore = 1f - Mathf.Clamp(GlobalPosition.DistanceTo(c.GlobalPosition) / rayon, 0f, 1f);
			float score = ageScore * 0.72f + proxScore * 0.28f + _rng.RandfRange(0f, 0.08f);
			if (score > meilleurScore)
			{
				meilleurScore = score;
				male = c;
			}
		}
		return male != null;
	}

	private void DonnerNaissance()
	{
		_estEnGestation = false;
		_tempsGestationRestant = 0f;
		_cooldownReproduction = Mathf.Max(10f, CooldownReproductionSecondes);

		bool naissanceMale = _rng.Randf() <= Mathf.Clamp(ProbabiliteNaissanceMale, 0f, 1f);
		string chemin = naissanceMale ? CheminSceneNaissanceMale : CheminSceneNaissanceFemelle;
		if (NaissanceSousFormeVeau)
		{
			string cheminVeau = naissanceMale ? CheminSceneVeauMale : CheminSceneVeauFemelle;
			if (!string.IsNullOrWhiteSpace(cheminVeau) && ResourceLoader.Exists(cheminVeau))
				chemin = cheminVeau;
		}
		if (string.IsNullOrWhiteSpace(chemin) || !ResourceLoader.Exists(chemin))
			return;
		var ps = GD.Load<PackedScene>(chemin);
		Node inst = ps?.Instantiate();
		if (inst is not BoeufSauvage bebe)
		{
			inst?.QueueFree();
			return;
		}

		Vector3 pos = GlobalPosition + new Vector3(_rng.RandfRange(-2.1f, 2.1f), 0f, _rng.RandfRange(-2.1f, 2.1f));
		Vector3 sol = TrouverSolPourNaissance(pos);
		GetParent().AddChild(bebe);
		bebe.GlobalPosition = sol + Vector3.Up * 0.2f;
		bebe.Configurer(_gestionnaire, _joueur, _seedTerrain, _ancreTroupeau);
		BoeufSauvage pere = _maleGestationReference != null && GodotObject.IsInstanceValid(_maleGestationReference)
			? _maleGestationReference
			: null;
		float minTaille = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float maxTaille = Mathf.Max(TailleGeneMin, TailleGeneMax);
		(float geneTailleA, float geneTailleB) = CroisementSBX(
			_geneTaille,
			pere != null ? pere._geneTaille : _geneTaille,
			minTaille, maxTaille, EtaSBX);
		float geneBebe = _rng.Randf() < 0.5f ? geneTailleA : geneTailleB;
		geneBebe = MutationPolynomiale(geneBebe, minTaille, maxTaille, EtaMutationPolynomiale, 1f);
		bebe.DefinirGeneTaille(geneBebe);

		float minVit = Mathf.Min(VitesseGeneMin, VitesseGeneMax);
		float maxVit = Mathf.Max(VitesseGeneMin, VitesseGeneMax);
		(float geneVitA, float geneVitB) = CroisementSBX(
			_geneVitesseDeplacement,
			pere != null ? pere._geneVitesseDeplacement : _geneVitesseDeplacement,
			minVit, maxVit, EtaSBX);
		float geneVitesseBebe = _rng.Randf() < 0.5f ? geneVitA : geneVitB;
		geneVitesseBebe = MutationPolynomiale(geneVitesseBebe, minVit, maxVit, EtaMutationPolynomiale, 1f);
		geneVitesseBebe += _rng.RandfRange(-IntensiteMutationVitesse, IntensiteMutationVitesse);
		_geneVitesseDeplacement = Mathf.Clamp(_geneVitesseDeplacement, minVit, maxVit);
		bebe._geneVitesseDeplacement = Mathf.Clamp(geneVitesseBebe, minVit, maxVit);

		(float genePersA, float genePersB) = CroisementSBX(
			_genePersonnalite,
			pere != null ? pere._genePersonnalite : _genePersonnalite,
			0f, 1f, EtaSBX);
		float genePersBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? genePersA : genePersB, 0f, 1f, EtaMutationPolynomiale, 0.9f);
		bebe._genePersonnalite = Mathf.Clamp(genePersBebe, 0f, 1f);

		(float geneConfA, float geneConfB) = CroisementSBX(
			_geneConfiance,
			pere != null ? pere._geneConfiance : _geneConfiance,
			0f, 1f, EtaSBX);
		float geneConfianceBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? geneConfA : geneConfB, 0f, 1f, EtaMutationPolynomiale, 0.85f);

		(float geneFuiteA, float geneFuiteB) = CroisementSBX(
			_geneReflexeFuite,
			pere != null ? pere._geneReflexeFuite : _geneReflexeFuite,
			0f, 1f, EtaSBX);
		float geneFuiteBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? geneFuiteA : geneFuiteB, 0f, 1f, EtaMutationPolynomiale, 0.85f);

		(float geneAttaqueA, float geneAttaqueB) = CroisementSBX(
			_geneReflexeAttaque,
			pere != null ? pere._geneReflexeAttaque : _geneReflexeAttaque,
			0f, 1f, EtaSBX);
		float geneAttaqueBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? geneAttaqueA : geneAttaqueB, 0f, 1f, EtaMutationPolynomiale, 0.85f);
		bebe.DefinirGenesComportementSocial(geneConfianceBebe, geneFuiteBebe, geneAttaqueBebe);

		(float navPrudenceA, float navPrudenceB) = CroisementSBX(
			_genePrudenceNavigation,
			pere != null ? pere._genePrudenceNavigation : _genePrudenceNavigation,
			0f, 1f, EtaSBX);
		(float navSautA, float navSautB) = CroisementSBX(
			_geneAudaceSaut,
			pere != null ? pere._geneAudaceSaut : _geneAudaceSaut,
			0f, 1f, EtaSBX);
		float genePrudence = MutationPolynomiale(_rng.Randf() < 0.5f ? navPrudenceA : navPrudenceB, 0f, 1f, EtaMutationPolynomiale, 0.8f);
		float geneSaut = MutationPolynomiale(_rng.Randf() < 0.5f ? navSautA : navSautB, 0f, 1f, EtaMutationPolynomiale, 0.8f);
		bebe.DefinirGenesNavigation(genePrudence, geneSaut);
		if (NaissanceSousFormeVeau)
			bebe.ConfigurerCommeVeau();

		// Inscrit le nouveau-né dans la population suivie + la banque de persistance : sinon les veaux (mâles comme
		// femelles) ne seraient ni comptés ni sauvegardés et disparaîtraient au déchargement de la zone.
		if (_gestionnaireFaune == null || !GodotObject.IsInstanceValid(_gestionnaireFaune))
			_gestionnaireFaune = GetParent() as GestionnaireFauneBoeufs;
		_gestionnaireFaune?.EnregistrerNouveauNe(bebe);

		_maleGestationReference = null;
		AjouterExperience(2.6f, "naissance");
	}

	private Vector3 TrouverSolPourNaissance(Vector3 approx)
	{
		int x = Mathf.FloorToInt(approx.X);
		int z = Mathf.FloorToInt(approx.Z);
		int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, _seedTerrain);
		Vector3 test = new Vector3(x + 0.5f, h + 60f, z + 0.5f);
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null)
			return approx;
		var q = PhysicsRayQueryParameters3D.Create(test, test + Vector3.Down * 120f);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
			return (Vector3)hit["position"];
		return approx;
	}
}
