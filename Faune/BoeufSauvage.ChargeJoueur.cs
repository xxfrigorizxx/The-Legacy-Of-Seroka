using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private bool EssayerAppliquerImpactChargeJoueur()
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur)) return false;
		if (JoueurEnModeCreatif()) return false; // joueur créatif ignoré : pas d'impact de charge.
		if (_cooldownImpactChargeJoueur > 0f || _impactChargeJoueurPlanifie) return false;
		if (!PeutEngagerChargeContreJoueur())
			return false;
		if (Velocity.Y > 1.15f || GlobalPosition.Y > _joueur.GlobalPosition.Y + DeltaYMaxDegatsChargeSurJoueur)
			return false;
		Vector3 dir = _joueur.GlobalPosition - GlobalPosition;
		dir.Y = 0f;
		float distJoueur = dir.Length();
		if (distJoueur < 0.0001f)
			dir = -GlobalTransform.Basis.Z;
		else
			dir /= distJoueur;
		if (distJoueur > DistanceMaxDeclenchementAttaqueCharge)
			return false;
		if (distJoueur > 1.85f && !PossedeLigneDeVueSurJoueur())
			return false;
		if (!ContactChargeCrediblePourAnimation(dir * distJoueur))
			return false;

		bool joueurDevant = JoueurDevantPourAttaqueCharge();
		bool impactResolu = EssayerResoudreImpactChargeSurJoueur(joueurDevant, out Vector3 pointImpact, out int shapeIdx);
		// Corps-à-corps strict : on ne déclenche l'attaque que sur contact réel (rayon touchant le joueur)
		// ou à bout portant devant soi. Plus d'animation de coup de tête « dans le vide » à distance.
		bool contactBoutPortant = distJoueur <= DistanceContactBoutPortantCharge && joueurDevant;
		if (!impactResolu && !contactBoutPortant)
			return false;
		if (!impactResolu)
		{
			pointImpact = _joueur.GlobalPosition + Vector3.Up * (joueurDevant ? 1.0f : 0.55f);
			shapeIdx = -1;
		}
		if (!DeclencherAnimationAttaqueChargeVersJoueur())
			return false;

		_impactChargeJoueurPlanifie = true;
		_impactChargeCoupDeTetePlanifie = joueurDevant;
		_pointImpactChargePlanifie = pointImpact;
		_dirImpactChargePlanifie = dir;
		_indiceFormeImpactChargePlanifie = shapeIdx;
		_delaiImpactChargePlanifie = DelaiDegatsApresDebutAnimationCharge;
		_cooldownImpactChargeJoueur = Mathf.Max(0.05f, CooldownImpactChargeJoueur);
		return true;
	}

	private void MettreAJourImpactChargeJoueurPlanifie(float dt)
	{
		if (!_impactChargeJoueurPlanifie)
			return;
		_delaiImpactChargePlanifie -= dt;
		if (_delaiImpactChargePlanifie > 0f)
		{
			if (_joueur != null && GodotObject.IsInstanceValid(_joueur))
			{
				Vector3 d = _joueur.GlobalPosition - GlobalPosition;
				d.Y = 0f;
				if (d.Length() > DistanceMaxImpactChargeApresDelai + 0.35f)
				{
					_impactChargeJoueurPlanifie = false;
					_indiceFormeImpactChargePlanifie = -1;
				}
			}
			return;
		}

		_impactChargeJoueurPlanifie = false;
		int shapeIdx = _indiceFormeImpactChargePlanifie;
		_indiceFormeImpactChargePlanifie = -1;
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return;

		Vector3 versJoueur = _joueur.GlobalPosition - GlobalPosition;
		versJoueur.Y = 0f;
		float distImpact = versJoueur.Length();
		// Corps-à-corps : le coup ne porte qu'au contact réel (rayon frais touchant le joueur) et à courte distance.
		// Sinon « coup dans le vide » : aucun dégât et fin d'engagement (évite de toucher le joueur de loin).
		bool contactFrais = EssayerResoudreImpactChargeSurJoueur(_impactChargeCoupDeTetePlanifie, out Vector3 pointFrais, out int shapeFrais);
		bool boutPortant = distImpact <= DistanceContactBoutPortantCharge && JoueurDevantPourAttaqueCharge();
		if (distImpact > DistanceMaxImpactChargeApresDelai || (!contactFrais && !boutPortant))
		{
			MarquerFinEngagementChargeJoueur(apresImpactReussi: false);
			return;
		}

		float impulsion = Mathf.Max(0.1f, ImpulsionChargeSurJoueur);
		Vector3 pointImpact = _pointImpactChargePlanifie;
		int shapeImpact = shapeIdx;
		if (contactFrais)
		{
			pointImpact = pointFrais;
			shapeImpact = shapeFrais;
		}
		if (_joueur is Joueur joueurHumain)
		{
			joueurHumain.RecevoirImpactChargeBovin(
				pointImpact,
				_dirImpactChargePlanifie,
				impulsion,
				_impactChargeCoupDeTetePlanifie,
				ChanceFractureOsChargeJoueurPct,
				shapeImpact);
		}
		else
		{
			Vector3 d = _dirImpactChargePlanifie;
			d.Y = 0f;
			if (d.LengthSquared() > 1e-6f)
				d = d.Normalized();
			Vector3 v = _joueur.Velocity;
			v.X += d.X * impulsion;
			v.Z += d.Z * impulsion;
			_joueur.Velocity = v;
		}
		MarquerFinEngagementChargeJoueur(apresImpactReussi: true);
	}

	/// <summary>
	/// Choisit le point visé d'une charge selon une pondération réaliste des zones du joueur.
	/// Coup de tête (face) : surtout torse/bras, parfois jambes, rarement la tête.
	/// Ruade (côté/arrière) : surtout jambes/torse, jamais la tête.
	/// </summary>
	private Vector3 ChoisirPointCibleZoneCharge(Joueur j, bool coupDeTete)
	{
		float pTorse, pBras, pJambes; // (la tête prend le reste)
		if (coupDeTete)
		{
			pTorse = 0.42f; pBras = 0.28f; pJambes = 0.18f; // tête = 0.12
		}
		else
		{
			pTorse = 0.34f; pBras = 0.16f; pJambes = 0.50f; // tête = 0 (ruade basse)
		}

		float r = _rng.Randf();
		string zone;
		if (r < pTorse)
			zone = "torse";
		else if (r < pTorse + pBras)
			zone = _rng.Randf() < 0.5f ? "bras_gauche" : "bras_droit";
		else if (r < pTorse + pBras + pJambes)
			zone = _rng.Randf() < 0.5f ? "jambe_gauche" : "jambe_droite";
		else
			zone = "tete";

		return j.ObtenirCentreHitboxMonde(zone);
	}

	/// <summary>
	/// Compte les taureaux adultes réellement hostiles au joueur dans le voisinage (soi inclus).
	/// Sert à doser le « 1 coup puis fuite » : seul/duo = pression continue, meute = harcèlement tournant.
	/// </summary>
	private int CompterTaureauxHostilesProches()
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return 1;
		IReadOnlyList<BoeufSauvage> pop = ObtenirPopulationLocale();
		if (pop == null)
			return 1;

		Vector3 pj = _joueur.GlobalPosition;
		const float rayon2 = 16f * 16f;
		int compte = 1; // soi-même
		for (int i = 0; i < pop.Count; i++)
		{
			BoeufSauvage b = pop[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b))
				continue;
			if (!b.EstTaureau || b._estVeauActif || b._etat == EtatBoeuf.Mort)
				continue;
			if (b.GlobalPosition.DistanceSquaredTo(pj) > rayon2)
				continue;
			// Hostile = en approche/charge, en soutien, impact planifié, ou vient d'attaquer (cooldown actif).
			bool hostile = b._etat == EtatBoeuf.Charge
				|| b._etat == EtatBoeuf.Soutien
				|| b._impactChargeJoueurPlanifie
				|| b._cooldownReengagementChargeJoueur > 0f;
			if (hostile)
				compte++;
		}
		return compte;
	}

	private bool JoueurDevantPourAttaqueCharge()
	{
		if (_joueur == null)
			return true;
		Vector3 forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 1e-6f)
			return true;
		forward = forward.Normalized();
		Vector3 versJoueur = _joueur.GlobalPosition - GlobalPosition;
		versJoueur.Y = 0f;
		if (versJoueur.LengthSquared() < 1e-6f)
			return true;
		return forward.Dot(versJoueur.Normalized()) >= 0.25f;
	}

	/// <summary>Raycast principal selon le type d'attaque (tête/torse face à face, jambes/tronc en ruade).</summary>
	private bool EssayerResoudreImpactChargeSurJoueur(bool coupDeTete, out Vector3 pointImpact, out int indiceFormeJoueur)
	{
		pointImpact = _joueur != null ? _joueur.GlobalPosition : GlobalPosition;
		indiceFormeJoueur = -1;
		if (_joueur == null)
			return false;

		PhysicsDirectSpaceState3D espace = GetWorld3D()?.DirectSpaceState;
		if (espace == null)
			return false;

		var exclude = new Godot.Collections.Array<Rid> { GetRid() };

		static bool EstJoueurOuEnfant(CharacterBody3D joueur, GodotObject collider)
		{
			if (collider == joueur)
				return true;
			return collider is Node n && joueur.IsAncestorOf(n);
		}

		bool EssayerRayVersCible(Vector3 origine, Vector3 cibleMonde, out Vector3 pos, out int shapeIdx, out float dist2Origine)
		{
			pos = cibleMonde;
			shapeIdx = -1;
			dist2Origine = float.MaxValue;
			Vector3 delta = cibleMonde - origine;
			float longueur = delta.Length();
			if (longueur < 0.04f)
				return false;
			delta = delta / longueur * Mathf.Min(longueur + 0.65f, 3.6f);

			var requete = PhysicsRayQueryParameters3D.Create(origine, origine + delta);
			requete.CollisionMask = 1u;
			requete.CollideWithBodies = true;
			requete.CollideWithAreas = false;
			requete.Exclude = exclude;

			Godot.Collections.Dictionary impact = espace.IntersectRay(requete);
			if (impact.Count == 0 || !impact.TryGetValue("collider", out Variant colliderV))
				return false;
			if (!EstJoueurOuEnfant(_joueur, colliderV.AsGodotObject()))
				return false;
			if (!impact.TryGetValue("position", out Variant posV))
				return false;

			pos = (Vector3)posV;
			shapeIdx = impact.TryGetValue("shape", out Variant shapeV) ? shapeV.AsInt32() : -1;
			dist2Origine = origine.DistanceSquaredTo(pos);
			return true;
		}

		Vector3 origine;
		Vector3 cible;
		if (_joueur is Joueur jCible)
		{
			// Zone visée PONDÉRÉE : surtout torse/bras, parfois jambes, rarement la tête (réaliste : la tête/cornes
			// d'un bovin arrivent à hauteur du tronc humain, pas du visage). Fini le « toujours la tête ».
			cible = ChoisirPointCibleZoneCharge(jCible, coupDeTete);
			// Léger décalage horizontal vers le bovin : on vise la face avant de la hitbox (impact crédible).
			Vector3 versBovin = GlobalPosition - _joueur.GlobalPosition;
			versBovin.Y = 0f;
			if (versBovin.LengthSquared() > 1e-6f)
				cible += versBovin.Normalized() * 0.22f;
			// Origine à hauteur de la zone visée (corne qui pique le torse / encornement bas vers les jambes).
			float hCible = cible.Y - GlobalPosition.Y;
			origine = GlobalPosition + Vector3.Up * Mathf.Clamp(hCible + 0.12f, 0.55f, 1.2f);
		}
		else
		{
			origine = GlobalPosition + Vector3.Up * (coupDeTete ? 1.1f : 0.78f);
			cible = _joueur.GlobalPosition + Vector3.Up * (coupDeTete ? 1.02f : 0.52f);
		}

		if (EssayerRayVersCible(origine, cible, out pointImpact, out indiceFormeJoueur, out _))
			return true;

		float meilleurDist2 = float.MaxValue;
		bool trouve = false;
		// Hauteurs de repli (offsets au-dessus de l'origine joueur ≈ taille). Torse ~0.12, tête ~0.55, jambes ~-0.3.
		// On ne vise PLUS au-dessus de la tête : front = torse/haut-torse, ruade = jambes/bas.
		float[] hauteurs = coupDeTete
			? new[] { 0.12f, 0.30f, 0.52f }
			: new[] { -0.28f, -0.05f, 0.12f };
		foreach (float h in hauteurs)
		{
			Vector3 o = GlobalPosition + Vector3.Up * Mathf.Max(0.5f, h - 0.14f);
			Vector3 c = _joueur.GlobalPosition + Vector3.Up * h;
			if (!EssayerRayVersCible(o, c, out Vector3 pos, out int shapeIdx, out float dist2))
				continue;
			if (dist2 >= meilleurDist2)
				continue;
			meilleurDist2 = dist2;
			pointImpact = pos;
			indiceFormeJoueur = shapeIdx;
			trouve = true;
		}

		return trouve;
	}

	/// <summary>Évite ruade / coup de tête animés sans vraie approche (contact crédible).</summary>
	private bool ContactChargeCrediblePourAnimation(Vector3 dirVersJoueurHoriz)
	{
		dirVersJoueurHoriz.Y = 0f;
		float dist = dirVersJoueurHoriz.Length();
		if (dist < 0.0001f)
			return true;
		if (_etat == EtatBoeuf.Charge && dist <= DistanceMaxDeclenchementAttaqueCharge)
			return true;
		if (dist <= DistanceAttaqueChargeFaceAFace && JoueurDevantPourAttaqueCharge())
			return true;
		Vector3 versJ = dirVersJoueurHoriz / dist;
		Vector3 vH = new Vector3(Velocity.X, 0f, Velocity.Z);
		float approche = vH.Dot(versJ);
		if (dist <= 1.25f)
			return true;
		return dist <= 1.7f && approche >= 0.85f;
	}

	/// <summary>Joue <see cref="_clipAttaqueKick"/> ou <see cref="_clipAttaqueTete"/> — prioritaire sur la locomotion de charge.</summary>
	private bool DeclencherAnimationAttaqueChargeVersJoueur()
	{
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
			return false;
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return false;
		Vector3 forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 1e-6f)
			forward = Vector3.Forward;
		forward = forward.Normalized();
		Vector3 versJoueur = _joueur.GlobalPosition - GlobalPosition;
		versJoueur.Y = 0f;
		float dot = versJoueur.LengthSquared() > 1e-6f ? forward.Dot(versJoueur.Normalized()) : 1f;
		bool joueurDevant = dot >= 0.25f;
		string clip = joueurDevant ? _clipAttaqueTete : _clipAttaqueKick;
		string noeud = joueurDevant ? NomNoeudAttaqueTete : NomNoeudAttaqueKick;
		if (string.IsNullOrEmpty(clip) || !_animationPlayer.HasAnimation(clip))
			return false;

		float duree = 0.72f;
		Animation animRef = _animationPlayer.GetAnimation(ObtenirStringNameAnimation(clip));
		if (animRef != null)
			duree = Mathf.Clamp(animRef.Length, 0.38f, 2.4f);
		_tempsVerrouAnimationCombat = Mathf.Max(_tempsVerrouAnimationCombat, duree);
		_noeudAnimationCombatVerrou = noeud;

		if (_blendLocomotionActif && _playbackEtatFaune != null && _animationTreeFaune != null && _animationTreeFaune.Active)
		{
			bool noeudPresent = (noeud == NomNoeudAttaqueTete && _machineAPorteAttaqueTete)
				|| (noeud == NomNoeudAttaqueKick && _machineAPorteAttaqueKick);
			if (noeudPresent)
			{
				if (_etatCourantMachineAnimation != noeud
					&& _etatCourantMachineAnimation != NomNoeudAttaqueKick
					&& _etatCourantMachineAnimation != NomNoeudAttaqueTete
					&& _etatCourantMachineAnimation != NomNoeudDeplacement)
				{
					_playbackEtatFaune.Travel(NomNoeudDeplacementString);
					_etatCourantMachineAnimation = NomNoeudDeplacement;
				}
				_playbackEtatFaune.Travel(ObtenirNomEtatAnimation(noeud));
				_etatCourantMachineAnimation = noeud;
				return true;
			}
		}

		if (_animationTreeFaune != null && _animationTreeFaune.Active)
			_animationTreeFaune.Active = false;
		_animationPlayer.Play(ObtenirStringNameAnimation(clip), 0.05f);
		return true;
	}
}
