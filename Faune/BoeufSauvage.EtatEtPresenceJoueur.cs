using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void ResynchroniserReferenceJoueur()
	{
		if (_joueur != null && GodotObject.IsInstanceValid(_joueur) && _joueur.IsInsideTree())
			return;
		_joueur = null;
		if (_gestionnaire != null && GodotObject.IsInstanceValid(_gestionnaire))
			_joueur = _gestionnaire.ObtenirJoueurSiValide();
	}

	private bool EssayerObtenirPositionJoueur(out Vector3 positionJoueur)
	{
		positionJoueur = Vector3.Zero;
		ResynchroniserReferenceJoueur();
		if (_joueur == null)
			return false;
		try
		{
			if (!GodotObject.IsInstanceValid(_joueur) || !_joueur.IsInsideTree())
			{
				_joueur = null;
				return false;
			}
			positionJoueur = _joueur.GlobalPosition;
			return true;
		}
		catch (ObjectDisposedException)
		{
			_joueur = null;
			return false;
		}
	}

	private void GererPresenceJoueur()
	{
		if (!EssayerObtenirPositionJoueur(out Vector3 posJoueur))
			return;
		if (FaimCritiquePrioritaire())
			return; // Sous 25% de faim: l'animal ignore le joueur et cherche a manger en priorite.
		Vector3 d = posJoueur - GlobalPosition;
		d.Y = 0f;
		float dist = d.Length();
		if (dist <= 0.001f) return;
		if (!PeutPercevoirJoueur(dist, d))
			return;
		float geneConfiance = Mathf.Clamp(_geneConfiance, 0f, 1f);
		float geneFuite = Mathf.Clamp(_geneReflexeFuite, 0f, 1f);
		float geneAttaque = Mathf.Clamp(_geneReflexeAttaque, 0f, 1f);
		float facteurPersonnalitePeur = Mathf.Lerp(1.35f, 0.7f, _genePersonnalite);
		float facteurConfiancePeur = Mathf.Lerp(1.42f, 0.62f, geneConfiance);
		float facteurFuitePeur = Mathf.Lerp(0.82f, 1.36f, geneFuite);
		float distancePeurEffective = DistancePeurJoueur * facteurPersonnalitePeur * facteurConfiancePeur * facteurFuitePeur;
		float chanceAgressivite = Mathf.Lerp(0.03f, 0.32f, geneAttaque) * Mathf.Lerp(1.08f, 0.62f, geneConfiance);
		chanceAgressivite *= Mathf.Lerp(0.95f, 0.65f, geneFuite);
		chanceAgressivite = Mathf.Clamp(chanceAgressivite, 0.01f, 0.9f);
		float chanceResterCalme = Mathf.Lerp(0.05f, 0.68f, geneConfiance);
		chanceResterCalme *= Mathf.Lerp(1f, 0.58f, geneFuite);
		chanceResterCalme *= Mathf.Lerp(1f, 0.75f, geneAttaque);
		chanceResterCalme = Mathf.Clamp(chanceResterCalme, 0f, 0.85f);

		if (EstTaureau && TaureauProtegeFemelles && TrouverFemelleMenaceeParJoueur(out _))
		{
			if (!EssayerDepenserStamina(CoutStaminaAttaque))
				return;
			_etat = EtatBoeuf.Charge;
			_tempsCharge = Mathf.Max(_tempsCharge, DureeChargeProtection);
			_cibleCourante = posJoueur;
			EmitSignal(SignalName.EvolutionEvenement, "charge_protection_troupeau", 1f, _niveau, _ageSecondes / 3600f);
			return;
		}

		if (PeutEngagerChargeContreJoueur() && _cooldownReengagementChargeJoueur <= 0f
			&& dist <= DistanceDeclenchementEngagementCharge
			&& _faimCourante > SeuilRechercheHerbe + 10f)
		{
			bool faceAFace = JoueurDevantPourAttaqueCharge() || dist <= 1.35f;
			bool declencher = faceAFace
				|| (dist <= DistanceAttaqueChargeFaceAFace && _rng.Randf() < Mathf.Max(0.55f, chanceAgressivite));
			if (declencher && EssayerDepenserStamina(CoutStaminaAttaque))
			{
				_etat = EtatBoeuf.Charge;
				_tempsCharge = Mathf.Max(_tempsCharge, faceAFace ? 2.4f : 1.6f);
				_cibleCourante = posJoueur;
				EmitSignal(SignalName.EvolutionEvenement, "charge_joueur", 1f, _niveau, _ageSecondes / 3600f);
				return;
			}
		}

		if (dist < distancePeurEffective)
		{
			if (dist > DistanceDeclenchementEngagementCharge && _rng.Randf() < chanceResterCalme)
				return;
			_tempsFuite = 3.0f;
			if (_peutEsquiver && _rng.Randf() < 0.22f)
			{
				Vector3 tangent = d.Normalized().Rotated(Vector3.Up, _rng.RandfRange(-Mathf.Pi / 2f, Mathf.Pi / 2f));
				_cibleCourante = GlobalPosition - tangent * _rng.RandfRange(6f, 10f);
				AjouterExperience(ExperienceEsquive, "esquive");
			}
		}
	}

	private void GererEtatEtCible(float dt)
	{
		if (FaimCritiquePrioritaire())
			ForcerEtatBroutageSiBesoin(prioriteAbsolue: true);
		else if (DoitEntrerBroutageSelonSeuils())
			ForcerEtatBroutageSiBesoin(prioriteAbsolue: false);

		if (_etat == EtatBoeuf.Charge && !PeutEngagerChargeContreJoueur())
		{
			_etat = EtatBoeuf.Fuite;
			_tempsCharge = 0f;
			_impactChargeJoueurPlanifie = false;
			if (_tempsFuite <= 0.05f)
				_tempsFuite = 3f;
		}

		bool combatChargeEnCours = _impactChargeJoueurPlanifie || _tempsVerrouAnimationCombat > 0.01f;
		if (_etat == EtatBoeuf.Charge && (_tempsCharge > 0f || combatChargeEnCours)
			&& EssayerObtenirPositionJoueur(out Vector3 posJoueurCharge))
		{
			_cibleCourante = posJoueurCharge;
			Vector3 versJoueur = posJoueurCharge - GlobalPosition;
			versJoueur.Y = 0f;
			float dist = versJoueur.Length();
			if (combatChargeEnCours)
				return;
			if (dist <= DistanceMaxDeclenchementAttaqueCharge && EssayerAppliquerImpactChargeJoueur())
			{
				_tempsCharge = Mathf.Max(_tempsCharge, 1.8f);
				return;
			}
			if (_tempsCharge <= 0.05f)
			{
				MarquerFinEngagementChargeJoueur(apresImpactReussi: false);
				return;
			}
			return;
		}

		if (_tempsFuite > 0f)
		{
			_etat = EtatBoeuf.Fuite;
			Vector3 fuite = EssayerObtenirPositionJoueur(out Vector3 posFuite)
				? (GlobalPosition - posFuite)
				: Vector3.Forward;
			fuite.Y = 0f;
			if (fuite.LengthSquared() < 0.001f) fuite = Vector3.Forward;
			_cibleCourante = GlobalPosition + fuite.Normalized() * _rng.RandfRange(10f, 18f);
			return;
		}

		if (_etat == EtatBoeuf.Broutage)
		{
			_tempsBroutage -= dt;
			_cooldownMorsure -= dt;
			float seuilHerbe = Mathf.Max(1.0f, RayonMangerHerbe * 0.9f);
			if (GlobalPosition.DistanceSquaredTo(_cibleCourante) > seuilHerbe * seuilHerbe)
			{
				// Se déplace vers une zone réellement couverte en mesh herbe.
				if (!HerbeDisponibleAutour(_cibleCourante, RayonMangerHerbe) && TrouverPointHerbeProche(out Vector3 h2))
					_cibleCourante = h2;
			}
			if (_cooldownMorsure <= 0f)
			{
				_cooldownMorsure = 0.85f;
				bool aMange = ConsommerHerbeSousPattes();
				if (!aMange)
				{
					_echecsMorsureConsecutifs++;
					_cooldownMorsure = 0.65f;
					if (TrouverPointHerbeProche(out Vector3 h3))
						_cibleCourante = h3;
					else if (_echecsMorsureConsecutifs >= 3)
					{
						// Évite le spam statique sans herbe: repart chercher ailleurs.
						_etat = EtatBoeuf.Errance;
						ChoisirNouvelleCible(false);
						return;
					}
				}
				else
				{
					_echecsMorsureConsecutifs = 0;
				}
			}
			if (_tempsBroutage <= 0f || _faimCourante >= _faimMaxActuelle - 2f)
			{
				_etat = EtatBoeuf.Errance;
				ChoisirNouvelleCible(false);
			}
			return;
		}

		if (_peutAider && TrouverAllieEnDetresse(out BoeufSauvage allie))
		{
			_etat = EtatBoeuf.Soutien;
			_cibleCourante = allie.GlobalPosition;
			if (GlobalPosition.DistanceSquaredTo(allie.GlobalPosition) < 16f)
				AjouterExperience(0.5f, "aide_allie");
			return;
		}

		if (DoitEntrerBroutageSelonSeuils() && _etat != EtatBoeuf.Fuite)
		{
			ForcerEtatBroutageSiBesoin(prioriteAbsolue: FaimCritiquePrioritaire());
			return;
		}

		if (_peutSuivre && TrouverAllieLePlusProche(out BoeufSauvage proche))
		{
			float d2 = GlobalPosition.DistanceSquaredTo(proche.GlobalPosition);
			float min2 = 11f * 11f;
			float max2 = (RayonRassemblement * 1.1f) * (RayonRassemblement * 1.1f);
			if (d2 > min2 && d2 < max2)
			{
				_etat = EtatBoeuf.Soutien;
				_cibleCourante = proche.GlobalPosition;
				return;
			}
		}

		if (_deblocageStrategieTroupeau && TrouverAllieLePlusProche(out BoeufSauvage procheTroupeau))
		{
			float dTroupe2 = GlobalPosition.DistanceSquaredTo(procheTroupeau.GlobalPosition);
			float minTroupe2 = 7.5f * 7.5f;
			float maxTroupe2 = (RayonRassemblement * 1.35f) * (RayonRassemblement * 1.35f);
			if (dTroupe2 > minTroupe2 && dTroupe2 < maxTroupe2)
			{
				_etat = EtatBoeuf.Soutien;
				_cibleCourante = procheTroupeau.GlobalPosition.Lerp(_ancreTroupeau, 0.35f);
				return;
			}
		}

		_etat = EtatBoeuf.Errance;
		if (_tempsIdleErrance > 0f)
		{
			_cibleCourante = GlobalPosition; // Déclenche l'animation idle le temps de la pause.
			return;
		}

		if (GlobalPosition.DistanceSquaredTo(_cibleCourante) < 1.8f * 1.8f)
		{
			float minIdle = Mathf.Max(0f, DureeIdleErranceMin);
			float maxIdle = Mathf.Max(minIdle, DureeIdleErranceMax);
			_tempsIdleErrance = _rng.RandfRange(minIdle, maxIdle);
			if (_tempsIdleErrance > 0.01f)
			{
				_cibleCourante = GlobalPosition;
				return;
			}
		}

		if (_cooldownChoixCible <= 0f || GlobalPosition.DistanceSquaredTo(_cibleCourante) < 1.8f * 1.8f)
			ChoisirNouvelleCible(false);
	}
}
