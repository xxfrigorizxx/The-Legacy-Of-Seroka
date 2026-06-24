using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	public Godot.Collections.Dictionary<string, Variant> ExtraireProfilEvolution()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "niveau", _niveau },
			{ "age_heures", _ageSecondes / 3600f },
			{ "experience", _experience },
			{ "force", ForceActuelle },
			{ "constitution", ConstitutionActuelle },
			{ "vitesse", VitesseStatActuelle },
			{ "faim", _faimCourante },
			{ "stamina", _staminaCourante },
			{ "vie", _vieCourante },
			{ "gene_taille", _geneTaille },
			{ "gene_vitesse", _geneVitesseDeplacement },
			{ "gene_personnalite", _genePersonnalite },
			{ "gene_confiance", _geneConfiance },
			{ "gene_fuite", _geneReflexeFuite },
			{ "gene_attaque", _geneReflexeAttaque },
			{ "score_adaptation_env", _scoreAdaptationEnvironnement },
			{ "gene_prudence_nav", _genePrudenceNavigation },
			{ "gene_audace_saut", _geneAudaceSaut },
			{ "score_navigation", _scoreNavigationEvolutif },
			{ "id_individu", _identifiantIndividu },
			{ "est_veau", _estVeauActif },
			{ "sexe", EstFemelle ? "femelle" : "male" },
			{ "gestation", _estEnGestation },
			{ "angle_vision_deg", AngleVisionActuelDegres() },
			{ "peut_attaquer", _peutAttaquer },
			{ "peut_esquiver", _peutEsquiver },
			{ "peut_suivre", _peutSuivre },
			{ "peut_aider", _peutAider },
			{ "deblocage_anim_contextuelle", _deblocageAnimationContextuelle },
			{ "deblocage_pensee_troupeau", _deblocageStrategieTroupeau },
			{ "deblocage_affichage_troupeau", _deblocageAffichageTroupeau }
		};
	}

	private float MultiplicateurNiveau => 1f + ((_niveau - 1) * BonusParNiveau);
	private bool EstFemelle => this is VacheSauvage;
	private bool EstTaureau => !EstFemelle;
	private float TailleEffective => _geneTaille * Mathf.Clamp(MultiplicateurTailleGlobale, 0.4f, 1.2f) * FacteurAgeMorphologique();
	private float FacteurAgeMorphologique() => _estVeauActif ? Mathf.Clamp(FacteurTailleVeau, 0.2f, 1f) : 1f;
	private float NormaliserGeneTaille()
	{
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		return Mathf.Clamp(Mathf.InverseLerp(min, max, _geneTaille), 0f, 1f);
	}
	private float FacteurTailleForce => Mathf.Lerp(0.72f, 1.62f, NormaliserGeneTaille());
	private float FacteurTailleConstitution => Mathf.Lerp(0.78f, 1.48f, NormaliserGeneTaille());
	private float FacteurTailleVitesse => Mathf.Lerp(1.35f, 0.72f, NormaliserGeneTaille());
	private float FacteurGeneVitesse => Mathf.Clamp(_geneVitesseDeplacement, 0.5f, 2f);
	private float ForceActuelle => ForceBase * MultiplicateurNiveau * FacteurTailleForce;
	private float ConstitutionActuelle => ConstitutionBase * MultiplicateurNiveau * FacteurTailleConstitution;
	private float VitesseStatActuelle => VitesseBase * MultiplicateurNiveau * FacteurTailleVitesse * FacteurGeneVitesse;

	public void Configurer(Gestionnaire_Monde gestionnaire, CharacterBody3D joueur, int seedTerrain, Vector3 ancreTroupeau)
	{
		_gestionnaire = gestionnaire;
		_gestionnaireFaune = GetParent() as GestionnaireFauneBoeufs;
		_joueur = joueur;
		_seedTerrain = seedTerrain;
		_ancreTroupeau = ancreTroupeau;
		_niveau = 1;
		_experience = 0f;
		_ageSecondes = 0f;
		_peutEsquiver = false;
		_peutAttaquer = false;
		_peutSuivre = false;
		_peutAider = false;
		_initialise = true;
		_diagnosticBlocageInitialisationDejaLogge = false;
		_diagnosticMortPersistanteDejaLogge = false;
		_framesDiagnosticSpawnRestantes = ActiverDiagnosticSpawnBovin
			? Mathf.Clamp(FramesDiagnosticSpawnBovin, 1, 120)
			: 0;
		MettreAJourStatsDerivees();
		EvaluerDeblocages();
		_faimCourante = _faimMaxActuelle;
		_staminaCourante = _staminaMaxActuelle;
		_vieCourante = _vieMaxActuelle;
		_cooldownRegenVie = Mathf.Max(1f, IntervalleRegenVieSecondes);
		MettreAJourAffichageFaim3D();
		ChoisirNouvelleCible(true);
	}

	public void DefinirGeneTaille(float gene)
	{
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		_geneTaille = Mathf.Clamp(gene, min, max);
		_geneTailleInitialise = true;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourStatsDerivees();
	}

	public void DefinirGenesNavigation(float prudence, float audaceSaut)
	{
		_genePrudenceNavigation = Mathf.Clamp(prudence, 0f, 1f);
		_geneAudaceSaut = Mathf.Clamp(audaceSaut, 0f, 1f);
		_genesNavigationInitialises = true;
	}

	public void DefinirGenesComportementSocial(float confiance, float reflexeFuite, float reflexeAttaque)
	{
		_geneConfiance = Mathf.Clamp(confiance, 0f, 1f);
		_geneReflexeFuite = Mathf.Clamp(reflexeFuite, 0f, 1f);
		_geneReflexeAttaque = Mathf.Clamp(reflexeAttaque, 0f, 1f);
		_genesComportementInitialises = true;
	}

	public void ConfigurerCommeVeau()
	{
		_estVeauActif = true;
		_ageSecondes = 0f;
		_tentativeReproductionJourEffectuee = true;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourStatsDerivees();
	}

	private void AssurerIdentifiantIndividu()
	{
		if (!string.IsNullOrWhiteSpace(_identifiantIndividu))
			return;
		_identifiantIndividu = Guid.NewGuid().ToString("N");
	}

	private void InitialiserGenesPersonnaliteSiNecessaire()
	{
		float minV = Mathf.Min(VitesseGeneMin, VitesseGeneMax);
		float maxV = Mathf.Max(VitesseGeneMin, VitesseGeneMax);
		if (_geneVitesseDeplacement <= 0.001f)
			_geneVitesseDeplacement = _rng.RandfRange(minV, maxV);
		else
			_geneVitesseDeplacement = Mathf.Clamp(_geneVitesseDeplacement, minV, maxV);
		_genePersonnalite = Mathf.Clamp(_genePersonnalite, 0f, 1f);
	}

	private void InitialiserGenesComportementSiNecessaire()
	{
		if (_genesComportementInitialises)
			return;
		_geneConfiance = Mathf.Clamp(_rng.RandfRange(0.32f, 0.72f), 0f, 1f);
		_geneReflexeFuite = Mathf.Clamp(_rng.RandfRange(0.35f, 0.78f), 0f, 1f);
		_geneReflexeAttaque = Mathf.Clamp(_rng.RandfRange(0.2f, 0.64f), 0f, 1f);
		_genesComportementInitialises = true;
	}

	public string ObtenirIdentifiantIndividu()
	{
		AssurerIdentifiantIndividu();
		return _identifiantIndividu;
	}

	public Godot.Collections.Dictionary ExtraireProfilPersistant()
	{
		AssurerIdentifiantIndividu();
		return new Godot.Collections.Dictionary
		{
			{ "id", _identifiantIndividu },
			{ "age", _ageSecondes },
			{ "niveau", _niveau },
			{ "experience", _experience },
			{ "faim", _faimCourante },
			{ "stamina", _staminaCourante },
			{ "vie", _vieCourante },
			{ "gene_taille", _geneTaille },
			{ "gene_vitesse", _geneVitesseDeplacement },
			{ "gene_personnalite", _genePersonnalite },
			{ "gene_confiance", _geneConfiance },
			{ "gene_fuite", _geneReflexeFuite },
			{ "gene_attaque", _geneReflexeAttaque },
			{ "gene_prudence_nav", _genePrudenceNavigation },
			{ "gene_audace_saut", _geneAudaceSaut },
			{ "score_navigation", _scoreNavigationEvolutif },
			{ "est_veau", _estVeauActif },
			{ "etat", (int)_etat },
			{ "cadavre_attend_depecage", _cadavreAttendDepecage },
			{ "cadavre_loot_distribue", _cadavreLootDistribue },
			{ "cadavre_coups_depecage", _coupsDepecageDagueValides },
			{ "cadavre_heure_mort_unix", _horodatageMortUnixSec },
			{ "x", GlobalPosition.X },
			{ "y", GlobalPosition.Y },
			{ "z", GlobalPosition.Z },
			{ "ancre_x", _ancreTroupeau.X },
			{ "ancre_y", _ancreTroupeau.Y },
			{ "ancre_z", _ancreTroupeau.Z },
			{ "migration_active", _enMigrationHerbe },
			{ "migration_dir_x", _migrationDirection.X },
			{ "migration_dir_z", _migrationDirection.Z },
			{ "migration_reste_m", _migrationResteM },
			{ "migration_vitesse", _migrationVitesse },
			{ "migration_horodatage_unix", _migrationHorodatageUnixSec }
		};
	}

	public void AppliquerProfilPersistant(Godot.Collections.Dictionary data)
	{
		if (data == null || data.Count == 0)
			return;
		if (data.TryGetValue("id", out Variant idv))
			_identifiantIndividu = idv.AsString();
		AssurerIdentifiantIndividu();
		if (data.TryGetValue("age", out Variant ageV))
			_ageSecondes = Mathf.Max(0f, ageV.AsSingle());
		if (data.TryGetValue("niveau", out Variant niveauV))
			_niveau = Mathf.Max(1, niveauV.AsInt32());
		if (data.TryGetValue("experience", out Variant xpV))
			_experience = Mathf.Max(0f, xpV.AsSingle());
		if (data.TryGetValue("gene_taille", out Variant gtV))
			DefinirGeneTaille(gtV.AsSingle());
		if (data.TryGetValue("gene_vitesse", out Variant gvV))
			_geneVitesseDeplacement = Mathf.Clamp(gvV.AsSingle(), Mathf.Min(VitesseGeneMin, VitesseGeneMax), Mathf.Max(VitesseGeneMin, VitesseGeneMax));
		if (data.TryGetValue("gene_personnalite", out Variant gpV))
			_genePersonnalite = Mathf.Clamp(gpV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("gene_confiance", out Variant gcV))
			_geneConfiance = Mathf.Clamp(gcV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("gene_fuite", out Variant gfV))
			_geneReflexeFuite = Mathf.Clamp(gfV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("gene_attaque", out Variant gaV))
			_geneReflexeAttaque = Mathf.Clamp(gaV.AsSingle(), 0f, 1f);
		_genesComportementInitialises = data.ContainsKey("gene_confiance") || data.ContainsKey("gene_fuite") || data.ContainsKey("gene_attaque");
		if (data.TryGetValue("gene_prudence_nav", out Variant gpnV) && data.TryGetValue("gene_audace_saut", out Variant gasV))
			DefinirGenesNavigation(gpnV.AsSingle(), gasV.AsSingle());
		if (data.TryGetValue("score_navigation", out Variant snV))
			_scoreNavigationEvolutif = Mathf.Clamp(snV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("est_veau", out Variant veauV))
			_estVeauActif = veauV.AsBool();
		if (data.TryGetValue("faim", out Variant faimV))
			_faimCourante = Mathf.Max(0f, faimV.AsSingle());
		if (data.TryGetValue("stamina", out Variant stV))
			_staminaCourante = Mathf.Max(0f, stV.AsSingle());
		if (data.TryGetValue("vie", out Variant vieV))
			_vieCourante = Mathf.Max(0f, vieV.AsSingle());
		int etatSauvegarde = data.TryGetValue("etat", out Variant etatV)
			? etatV.AsInt32()
			: -1;
		bool lootDistribue = data.TryGetValue("cadavre_loot_distribue", out Variant lootV) && lootV.AsBool();
		bool attendDepecage = data.TryGetValue("cadavre_attend_depecage", out Variant attendV) ? attendV.AsBool() : true;
		int coupsDepecage = data.TryGetValue("cadavre_coups_depecage", out Variant coupsV) ? Mathf.Max(0, coupsV.AsInt32()) : 0;
		double horodatageMortUnix = data.TryGetValue("cadavre_heure_mort_unix", out Variant hmV) ? hmV.AsDouble() : 0.0;
		if (data.TryGetValue("ancre_x", out Variant ax) && data.TryGetValue("ancre_y", out Variant ay) && data.TryGetValue("ancre_z", out Variant az))
			_ancreTroupeau = new Vector3(ax.AsSingle(), ay.AsSingle(), az.AsSingle());
		// Exode alimentaire : restaure l'état de migration. La position a déjà été avancée par l'estime
		// du gestionnaire pendant le déchargement ; on repart d'ici avec l'horloge remise à maintenant.
		bool migrationActive = data.TryGetValue("migration_active", out Variant maV) && maV.AsBool();
		if (migrationActive
			&& data.TryGetValue("migration_dir_x", out Variant mdxV)
			&& data.TryGetValue("migration_dir_z", out Variant mdzV))
		{
			Vector3 dirMig = new Vector3(mdxV.AsSingle(), 0f, mdzV.AsSingle());
			if (dirMig.LengthSquared() > 0.0001f)
			{
				_migrationDirection = dirMig.Normalized();
				_migrationResteM = data.TryGetValue("migration_reste_m", out Variant mrV) ? Mathf.Max(0f, mrV.AsSingle()) : 0f;
				_migrationVitesse = data.TryGetValue("migration_vitesse", out Variant mvV)
					? Mathf.Max(0.4f, mvV.AsSingle())
					: Mathf.Max(0.4f, VitesseEstimeMigrationDefaut);
				_migrationHorodatageUnixSec = Time.GetUnixTimeFromSystem();
				_enMigrationHerbe = _migrationResteM > 0.01f;
			}
		}
		MettreAJourStatsDerivees();
		bool etatMortSauvegarde = etatSauvegarde == (int)EtatBoeuf.Mort;
		if (etatMortSauvegarde || _vieCourante <= 0.0001f)
			RestaurerEtatMortPersistant(attendDepecage, lootDistribue, coupsDepecage, horodatageMortUnix);
		else
			_reconfigurationArbreAnimationEnAttente = false;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourAffichageFaim3D();
	}

	private void RestaurerEtatMortPersistant(bool attendDepecage, bool lootDistribue, int coupsDepecage, double horodatageMortUnix)
	{
		_horodatageMortUnixSec = horodatageMortUnix > 0.0
			? horodatageMortUnix
			: Time.GetUnixTimeFromSystem();
		if (!lootDistribue && EstCadavreExpireParTempsReel())
		{
			_cadavreLootDistribue = true;
			_cadavreAttendDepecage = false;
			Callable.From(() =>
			{
				_gestionnaireFaune?.NotifierCadavreRetireDeLaPersistance(this);
				if (IsInsideTree())
					QueueFree();
			}).CallDeferred();
			return;
		}
		_etat = EtatBoeuf.Mort;
		_vieCourante = 0f;
		Velocity = Vector3.Zero;
		_tempsMort = float.MaxValue;
		_cadavreLootDistribue = lootDistribue;
		_cadavreAttendDepecage = !lootDistribue && attendDepecage;
		_coupsDepecageDagueValides = Mathf.Max(0, coupsDepecage);
		_reconfigurationArbreAnimationEnAttente = false;
		if (ActiverDiagnosticSpawnBovin && !_diagnosticMortPersistanteDejaLogge)
		{
			_diagnosticMortPersistanteDejaLogge = true;
			GD.Print($"ZERO-K Faune [DiagSpawn] {Name}: profil persistant charge en cadavre (attendDepecage={_cadavreAttendDepecage}, lootDistribue={_cadavreLootDistribue}, coups={_coupsDepecageDagueValides}).");
		}
		if (!_cadavreLootDistribue)
			Callable.From(AppliquerAnimationMortApresChargementPersistance).CallDeferred();
	}

	private void AppliquerAnimationMortApresChargementPersistance()
	{
		if (_etat != EtatBoeuf.Mort || _cadavreLootDistribue || _animationPlayer == null)
			return;
		AppliquerPoseCadavreFigee();
	}

	private void InitialiserGenesNavigationSiNecessaire()
	{
		if (_genesNavigationInitialises)
			return;
		_genePrudenceNavigation = Mathf.Clamp(_rng.RandfRange(0.38f, 0.66f), 0f, 1f);
		_geneAudaceSaut = Mathf.Clamp(_rng.RandfRange(0.34f, 0.62f), 0f, 1f);
		_scoreNavigationEvolutif = 0.5f;
		_genesNavigationInitialises = true;
	}

	private void AjusterScoreNavigation(float delta)
	{
		if (!ActiverApprentissageNavigation)
			return;
		float t = Mathf.Max(0.005f, TauxApprentissageNavigation);
		_scoreNavigationEvolutif = Mathf.Clamp(_scoreNavigationEvolutif + delta * t, 0f, 1f);
		_genePrudenceNavigation = Mathf.Clamp(_genePrudenceNavigation + (-delta * 0.35f) * t, 0f, 1f);
		_geneAudaceSaut = Mathf.Clamp(_geneAudaceSaut + (delta * 0.30f) * t, 0f, 1f);
	}

	/// <summary>
	/// Croisement SBX inspiré de jMetal/jMetalPy (GitHub), adapté à des gènes scalaires.
	/// Référence: https://github.com/jMetal/jMetalPy
	/// </summary>
	private (float enfant1, float enfant2) CroisementSBX(float parent1, float parent2, float borneMin, float borneMax, float eta)
	{
		float y1 = Mathf.Min(parent1, parent2);
		float y2 = Mathf.Max(parent1, parent2);
		float lb = Mathf.Min(borneMin, borneMax);
		float ub = Mathf.Max(borneMin, borneMax);
		eta = Mathf.Max(0.01f, eta);

		if (Mathf.Abs(y1 - y2) < 1e-6f)
			return (Mathf.Clamp(y1, lb, ub), Mathf.Clamp(y2, lb, ub));

		float rand = _rng.Randf();
		float beta1 = 1f + (2f * (y1 - lb) / (y2 - y1));
		float alpha1 = 2f - Mathf.Pow(beta1, -(eta + 1f));
		float betaq1 = rand <= 1f / alpha1
			? Mathf.Pow(rand * alpha1, 1f / (eta + 1f))
			: Mathf.Pow(1f / (2f - rand * alpha1), 1f / (eta + 1f));
		float c1 = 0.5f * (y1 + y2 - betaq1 * (y2 - y1));

		float beta2 = 1f + (2f * (ub - y2) / (y2 - y1));
		float alpha2 = 2f - Mathf.Pow(beta2, -(eta + 1f));
		float betaq2 = rand <= 1f / alpha2
			? Mathf.Pow(rand * alpha2, 1f / (eta + 1f))
			: Mathf.Pow(1f / (2f - rand * alpha2), 1f / (eta + 1f));
		float c2 = 0.5f * (y1 + y2 + betaq2 * (y2 - y1));

		c1 = Mathf.Clamp(c1, lb, ub);
		c2 = Mathf.Clamp(c2, lb, ub);
		if (_rng.Randf() < 0.5f)
			return (c2, c1);
		return (c1, c2);
	}

	/// <summary>
	/// Mutation polynomiale inspirée de jMetal/jMetalPy (GitHub).
	/// Référence: https://github.com/jMetal/jMetalPy
	/// </summary>
	private float MutationPolynomiale(float valeur, float borneMin, float borneMax, float eta, float probabilite)
	{
		float lb = Mathf.Min(borneMin, borneMax);
		float ub = Mathf.Max(borneMin, borneMax);
		float y = Mathf.Clamp(valeur, lb, ub);
		if (_rng.Randf() > Mathf.Clamp(probabilite, 0f, 1f) || ub - lb < 1e-8f)
			return y;

		eta = Mathf.Max(0.01f, eta);
		float delta1 = (y - lb) / (ub - lb);
		float delta2 = (ub - y) / (ub - lb);
		float rnd = _rng.Randf();
		float mutPow = 1f / (eta + 1f);
		float deltaq;

		if (rnd <= 0.5f)
		{
			float xy = 1f - delta1;
			float val = 2f * rnd + (1f - 2f * rnd) * Mathf.Pow(xy, eta + 1f);
			deltaq = Mathf.Pow(val, mutPow) - 1f;
		}
		else
		{
			float xy = 1f - delta2;
			float val = 2f * (1f - rnd) + 2f * (rnd - 0.5f) * Mathf.Pow(xy, eta + 1f);
			deltaq = 1f - Mathf.Pow(val, mutPow);
		}

		y += deltaq * (ub - lb);
		return Mathf.Clamp(y, lb, ub);
	}

	private void InitialiserGeneTailleSiNecessaire()
	{
		if (_geneTailleInitialise)
			return;
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		float maxGenerationInitiale = Mathf.Clamp(TailleGeneMaxGenerationInitiale, min, max);
		_geneTaille = _rng.RandfRange(min, maxGenerationInitiale);
		if (EstTaureau)
			_geneTaille = Mathf.Clamp(_geneTaille + 0.1f, min, maxGenerationInitiale);
		_geneTailleInitialise = true;
	}
}
