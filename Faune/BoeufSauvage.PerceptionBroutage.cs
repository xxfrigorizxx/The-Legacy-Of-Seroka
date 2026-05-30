using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private bool FaimCritiquePrioritaire() => RatioFaimCourant() <= 0.25f;

	private bool DoitEntrerBroutageSelonSeuils()
	{
		if (FaimCritiquePrioritaire())
			return true;
		return RatioFaimCourant() <= 0.50f || _faimCourante <= SeuilRechercheHerbe;
	}

	private void ForcerEtatBroutageSiBesoin(bool prioriteAbsolue)
	{
		if (_etat != EtatBoeuf.Broutage)
		{
			_etat = EtatBoeuf.Broutage;
			_tempsBroutage = DureeBroutage + _rng.RandfRange(0f, 2f);
			_cooldownMorsure = 0.15f;
			_echecsMorsureConsecutifs = 0;
		}
		else if (prioriteAbsolue)
		{
			_tempsBroutage = Mathf.Max(_tempsBroutage, DureeBroutage * 1.25f);
			_cooldownMorsure = Mathf.Min(_cooldownMorsure, 0.15f);
		}
		if (!TrouverPointHerbeProche(out Vector3 herbe))
			herbe = GlobalPosition;
		_cibleCourante = herbe;
	}

	private bool HerbeDisponibleAutour(Vector3 point, float rayon)
	{
		if (_gestionnaire == null) return false;
		return _gestionnaire.ExisteGazonFauneGlobal(point, rayon);
	}

	private bool TrouverPointHerbeProche(out Vector3 cibleHerbe)
	{
		cibleHerbe = GlobalPosition;
		if (_gestionnaire == null)
			return false;
		if (HerbeDisponibleAutour(GlobalPosition, RayonMangerHerbe))
			return true;

		float rayonMax = Mathf.Max(RayonMangerHerbe + 1f, RayonRechercheHerbeVisible);
		int essais = Mathf.Max(6, EssaisRechercheHerbe);
		for (int i = 0; i < essais; i++)
		{
			float angle = _rng.RandfRange(0f, Mathf.Tau);
			float dist = _rng.RandfRange(RayonMangerHerbe + 0.5f, rayonMax);
			Vector3 cand = GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
			if (!PositionTerrainValide(cand))
				continue;
			if (!HerbeDisponibleAutour(cand, RayonMangerHerbe))
				continue;
			cibleHerbe = new Vector3(cand.X, GlobalPosition.Y, cand.Z);
			return true;
		}
		return false;
	}

	private float AngleVisionActuelDegres()
	{
		float a = AngleVisionBaseDegres + GainAngleVisionParNiveauDegres * Mathf.Max(0, _niveau - 1);
		return Mathf.Clamp(a, Mathf.Min(AngleVisionBaseDegres, AngleVisionMaxDegres), Mathf.Max(AngleVisionBaseDegres, AngleVisionMaxDegres));
	}

	private bool ColliderEstJoueur(GodotObject collider)
	{
		if (_joueur == null || collider == null)
			return false;
		if (collider == _joueur)
			return true;
		return collider is Node n && _joueur.IsAncestorOf(n);
	}

	private bool PossedeLigneDeVueSurJoueur()
	{
		if (_joueur == null) return false;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;

		Vector3 origine = GlobalPosition + Vector3.Up * Mathf.Max(0.2f, HauteurYeuxPerception);
		Vector3 cible = _joueur.GlobalPosition + Vector3.Up * 1.0f;
		var q = PhysicsRayQueryParameters3D.Create(origine, cible);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit == null || hit.Count == 0)
			return false;
		if (!hit.ContainsKey("collider"))
			return false;
		return ColliderEstJoueur(hit["collider"].AsGodotObject());
	}

	private bool PeutPercevoirJoueur(float dist, Vector3 versJoueurHoriz)
	{
		if (_joueur == null || dist <= 0.001f)
			return false;

		// Mémoire courte de perception pour éviter clignotement de décision entre scans.
		if (_memoireDetectionJoueur > 0f && _cooldownVerificationVisionJoueur > 0f)
			return true;
		if (_cooldownVerificationVisionJoueur > 0f)
			return false;
		_cooldownVerificationVisionJoueur = Mathf.Max(0.05f, IntervalleVerificationVisionJoueur);

		bool detecte = false;
		if (dist <= Mathf.Max(0.1f, DistanceOuieJoueur))
			detecte = true;

		if (!detecte && UtiliserConeVisionJoueur && dist <= Mathf.Max(DistancePeurJoueur, DistanceVisionMaxJoueur))
		{
			Vector3 fwd = -GlobalTransform.Basis.Z;
			fwd.Y = 0f;
			fwd = fwd.LengthSquared() > 0.0001f ? fwd.Normalized() : Vector3.Forward;
			Vector3 dir = versJoueurHoriz.LengthSquared() > 0.0001f ? versJoueurHoriz.Normalized() : Vector3.Zero;
			float dot = Mathf.Clamp(fwd.Dot(dir), -1f, 1f);
			float angle = Mathf.RadToDeg(Mathf.Acos(dot));
			if (angle <= AngleVisionActuelDegres() * 0.5f && PossedeLigneDeVueSurJoueur())
				detecte = true;
		}

		if (detecte)
			_memoireDetectionJoueur = Mathf.Max(0.15f, MemoireDetectionSecondes);
		return detecte || _memoireDetectionJoueur > 0f;
	}

	private IReadOnlyList<BoeufSauvage> ObtenirPopulationLocale()
	{
		if (_gestionnaireFaune == null || !GodotObject.IsInstanceValid(_gestionnaireFaune))
			_gestionnaireFaune = GetParent() as GestionnaireFauneBoeufs;
		return _gestionnaireFaune?.ObtenirBoeufsActifs();
	}

	private bool TrouverFemelleMenaceeParJoueur(out VacheSauvage femelleMenacee)
	{
		femelleMenacee = null;
		if (_joueur == null) return false;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null) return false;
		float meilleure = float.MaxValue;
		for (int i = 0; i < population.Count; i++)
		{
			if (population[i] is not VacheSauvage f || f == this || !GodotObject.IsInstanceValid(f) || f._etat == EtatBoeuf.Mort)
				continue;
			float dJ = f.GlobalPosition.DistanceTo(_joueur.GlobalPosition);
			if (dJ > DistanceAlerteFemelle)
				continue;
			float dMoi = f.GlobalPosition.DistanceTo(GlobalPosition);
			if (dMoi > RayonRassemblement * 1.2f)
				continue;
			if (dJ < meilleure)
			{
				meilleure = dJ;
				femelleMenacee = f;
			}
		}
		return femelleMenacee != null;
	}
}
