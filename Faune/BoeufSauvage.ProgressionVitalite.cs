using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void MettreAJourAgeEtEvolution(float dt)
	{
		_ageSecondes += dt;
		_cooldownAge -= dt;
		if (_cooldownAge <= 0f)
		{
			_cooldownAge += Mathf.Max(5f, IntervalleCycleAgeSecondes);
			AjouterExperience(ExperienceCycleAge, "vieillissement");
		}
	}

	private void AjouterExperience(float quantite, string typeEvenement)
	{
		if (quantite <= 0f) return;
		_experience += quantite;
		EmitSignal(SignalName.EvolutionEvenement, typeEvenement, quantite, _niveau, _ageSecondes / 3600f);
		if (!AutoriserNiveauxParExperience)
			return;
		while (_experience >= ExperienceParNiveau)
		{
			_experience -= ExperienceParNiveau;
			_niveau++;
			MettreAJourStatsDerivees();
			EvaluerDeblocages();
			EmitSignal(SignalName.EvolutionEvenement, "niveau_plus", 1f, _niveau, _ageSecondes / 3600f);
		}
	}

	private void MettreAJourStatsDerivees()
	{
		_faimMaxActuelle = FaimMax * (ConstitutionActuelle / Mathf.Max(0.1f, ConstitutionBase));
		_faimCourante = Mathf.Clamp(_faimCourante, 0f, _faimMaxActuelle);
		_staminaMaxActuelle = StaminaMax * (ConstitutionActuelle / Mathf.Max(0.1f, ConstitutionBase));
		_staminaCourante = Mathf.Clamp(_staminaCourante, 0f, _staminaMaxActuelle);
		_vieMaxActuelle = VieMax * (ConstitutionActuelle / Mathf.Max(0.1f, ConstitutionBase));
		_vieCourante = Mathf.Clamp(_vieCourante, 0f, _vieMaxActuelle);
	}

	/// <summary>Seuls les taureaux chargent le joueur ; les vaches fuient (comportement bovin).</summary>
	private bool PeutEngagerChargeContreJoueur() => EstTaureau;

	private void MarquerFinEngagementChargeJoueur(bool apresImpactReussi)
	{
		_etat = EtatBoeuf.Fuite;
		_tempsCharge = 0f;

		// Dose le « 1 coup puis fuite » selon la taille de la meute hostile.
		// Seul ou à deux : le repli systématique est ridicule -> on reste pressant (recul minime, ré-engage vite).
		// En meute (≥5) : harcèlement tournant classique -> chacun frappe puis cède la place.
		int nbHostiles = CompterTaureauxHostilesProches();
		float facteurMeute = Mathf.Clamp((nbHostiles - 1) / 4f, 0f, 1f); // 1 hostile=0 … 5+ hostiles=1

		float fuitePleine = apresImpactReussi ? 2.2f : 3.5f;
		float fuiteSolo = apresImpactReussi ? 0.35f : 0.9f;
		_tempsFuite = Mathf.Lerp(fuiteSolo, fuitePleine, facteurMeute);
		_cooldownReengagementChargeJoueur = Mathf.Lerp(
			CooldownReengagementChargeJoueurSec * 0.25f,
			CooldownReengagementChargeJoueurSec,
			facteurMeute);

		_impactChargeJoueurPlanifie = false;
		_indiceFormeImpactChargePlanifie = -1;
		if (_joueur != null && GodotObject.IsInstanceValid(_joueur))
		{
			Vector3 fuite = GlobalPosition - _joueur.GlobalPosition;
			fuite.Y = 0f;
			if (fuite.LengthSquared() > 0.001f)
			{
				// Solo : court recul pour se replacer face au joueur ; meute : vrai décrochage.
				float distRecul = Mathf.Lerp(3.5f, _rng.RandfRange(12f, 20f), facteurMeute);
				_cibleCourante = GlobalPosition + fuite.Normalized() * distRecul;
			}
		}
	}

	private void EvaluerDeblocages()
	{
		VerifierDeblocage(ref _peutEsquiver, _niveau >= 3, "deblocage_esquive");
		VerifierDeblocage(ref _peutAttaquer, _niveau >= 4 && EstTaureau, "deblocage_charge");
		VerifierDeblocage(ref _peutSuivre, _niveau >= 5, "deblocage_suivi");
		VerifierDeblocage(ref _peutAider, _niveau >= 7, "deblocage_aide");
	}

	private void VerifierDeblocage(ref bool flag, bool condition, string evenement)
	{
		if (flag || !condition) return;
		flag = true;
		EmitSignal(SignalName.EvolutionEvenement, evenement, 1f, _niveau, _ageSecondes / 3600f);
	}

	private float RatioFaimCourant()
	{
		if (_faimMaxActuelle <= 0.001f)
			return 0f;
		return Mathf.Clamp(_faimCourante / _faimMaxActuelle, 0f, 1f);
	}

	private float RatioStaminaCourant()
	{
		if (_staminaMaxActuelle <= 0.001f)
			return 0f;
		return Mathf.Clamp(_staminaCourante / _staminaMaxActuelle, 0f, 1f);
	}

	private bool SprintAutoriseParStamina()
	{
		float seuilMini = Mathf.Max(0.05f, CoutStaminaCourseParSeconde * 0.05f);
		return _staminaCourante > seuilMini;
	}

	private bool EssayerDepenserStamina(float cout)
	{
		float c = Mathf.Max(0f, cout);
		if (c <= 0f)
			return true;
		if (_staminaCourante < c)
			return false;
		_staminaCourante = Mathf.Max(0f, _staminaCourante - c);
		MettreAJourAffichageFaim3D();
		return true;
	}

	private void RegenererStamina(float dt, bool enEffortIntense)
	{
		if (_staminaCourante >= _staminaMaxActuelle - 0.001f)
			return;
		float regenBase = Mathf.Max(0f, RegenerationStaminaParSeconde) * dt;
		if (regenBase <= 0.0001f)
			return;
		float facteur = enEffortIntense ? 0.25f : 1f;
		float regenPotentielle = regenBase * facteur;
		float manque = Mathf.Max(0f, _staminaMaxActuelle - _staminaCourante);
		float regen = Mathf.Min(manque, regenPotentielle);
		if (regen <= 0.0001f)
			return;

		float coutFaim = regen * Mathf.Max(0f, CoutFaimParPointStaminaRegen);
		if (coutFaim > 0f)
		{
			float ratioPossible = _faimCourante <= 0.0001f ? 0f : Mathf.Clamp(_faimCourante / coutFaim, 0f, 1f);
			regen *= ratioPossible;
			coutFaim *= ratioPossible;
		}
		if (regen <= 0.0001f)
			return;

		_staminaCourante = Mathf.Min(_staminaMaxActuelle, _staminaCourante + regen);
		_faimCourante = Mathf.Max(0f, _faimCourante - coutFaim);
	}

	private void GererRegenerationVie(float dt)
	{
		if (_etat == EtatBoeuf.Mort)
			return;
		if (_faimCourante <= 0.0001f)
			return; // Pas de regen vie en famine totale.
		_cooldownRegenVie -= dt;
		if (_cooldownRegenVie > 0f)
			return;
		_cooldownRegenVie = Mathf.Max(1f, IntervalleRegenVieSecondes);
		if (_vieCourante >= _vieMaxActuelle - 0.001f)
			return;
		float gain = _vieMaxActuelle * Mathf.Clamp(RegenViePourcentageParCycle, 0f, 1f);
		if (gain <= 0.0001f)
			return;
		_vieCourante = Mathf.Min(_vieMaxActuelle, _vieCourante + gain);
		MettreAJourAffichageFaim3D();
	}

	private void GererDegatsFamine(float dt)
	{
		if (_etat == EtatBoeuf.Mort)
			return;
		if (_faimCourante > 0.0001f)
			return;

		float degats = Mathf.Max(0.01f, DegatsVieParSecondeFaimNulle) * dt;
		_vieCourante = Mathf.Max(0f, _vieCourante - degats);
		_flashRougeDegatsRestant = Mathf.Max(_flashRougeDegatsRestant, Mathf.Max(0.05f, DureeFlashRougeDegats));
		AppliquerFlashRougeSurMateriaux(1f);
		MettreAJourAffichageFaim3D();
		if (_vieCourante <= 0.0001f)
			BasculerEnMort();
	}
}
