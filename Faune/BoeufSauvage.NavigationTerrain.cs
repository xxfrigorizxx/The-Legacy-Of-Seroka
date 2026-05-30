using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void ChoisirNouvelleCible(bool initial)
	{
		_cooldownChoixCible = _rng.RandfRange(IntervalleNouveauButMin, IntervalleNouveauButMax);
		Vector3 meilleurPoint = _ancreTroupeau;

		for (int i = 0; i < 24; i++)
		{
			float angle = _rng.RandfRange(0f, Mathf.Tau);
			float distance = _rng.RandfRange(5f, RayonErrance);
			Vector3 basePoint = initial ? _ancreTroupeau : GlobalPosition;
			Vector3 candidat = basePoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

			if (GlobalPosition.DistanceSquaredTo(_ancreTroupeau) > RayonRassemblement * RayonRassemblement)
			{
				Vector3 retour = _ancreTroupeau - GlobalPosition;
				retour.Y = 0f;
				if (retour.LengthSquared() > 0.0001f)
					candidat = GlobalPosition + retour.Normalized() * _rng.RandfRange(8f, 18f);
			}

			if (!PositionTerrainValide(candidat))
				continue;

			meilleurPoint = new Vector3(candidat.X, GlobalPosition.Y, candidat.Z);
			break;
		}

		_cibleCourante = meilleurPoint;
	}

	private bool PositionTerrainValide(Vector3 p)
	{
		int x = Mathf.FloorToInt(p.X);
		int z = Mathf.FloorToInt(p.Z);
		int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, _seedTerrain);
		if (h < 80 || h > 320) return false;
		int hE = Generateur_Voxel.ObtenirHauteurTerrainMonde(x + 5, z, _seedTerrain);
		int hW = Generateur_Voxel.ObtenirHauteurTerrainMonde(x - 5, z, _seedTerrain);
		int hN = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z - 5, _seedTerrain);
		int hS = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z + 5, _seedTerrain);
		int pente = Mathf.Abs(h - hE) + Mathf.Abs(h - hW) + Mathf.Abs(h - hN) + Mathf.Abs(h - hS);
		return pente <= 56;
	}

	private bool TrouverAllieEnDetresse(out BoeufSauvage allie)
	{
		allie = null;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null) return false;
		float meilleure = float.MaxValue;
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort) continue;
			if (!b.EstEnDetresse()) continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
			if (d2 < meilleure)
			{
				meilleure = d2;
				allie = b;
			}
		}
		return allie != null && meilleure < RayonRassemblement * RayonRassemblement;
	}

	private bool TrouverAllieLePlusProche(out BoeufSauvage allie)
	{
		allie = null;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null) return false;
		float meilleure = float.MaxValue;
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort) continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
			if (d2 < meilleure)
			{
				meilleure = d2;
				allie = b;
			}
		}
		return allie != null;
	}

	private float CalculerRatioCohesionTroupeau()
	{
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null)
			return 0f;
		int voisins = 0;
		int proches = 0;
		float rayon = Mathf.Max(4f, RayonRassemblement);
		float rayon2 = rayon * rayon;
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			voisins++;
			if (GlobalPosition.DistanceSquaredTo(b.GlobalPosition) <= rayon2)
				proches++;
		}
		if (voisins <= 0)
			return 0f;
		return Mathf.Clamp((float)proches / voisins, 0f, 1f);
	}

	private float FacteurAnimationContextuelle()
	{
		if (!_deblocageAnimationContextuelle)
			return 1f;
		float stress = (_tempsFuite > 0f || _memoireDetectionJoueur > 0f) ? 1.10f : 1f;
		if (_cooldownCohesionAnimation <= 0f)
		{
			_cooldownCohesionAnimation = 0.18f;
			_cohesionAnimationCache = CalculerRatioCohesionTroupeau();
		}
		float cohesion = Mathf.Lerp(0.96f, 1.08f, _cohesionAnimationCache);
		return Mathf.Clamp(stress * cohesion, 0.92f, 1.20f);
	}

	private Vector3 AdapterStrategieTerrain(Vector3 direction, float dt, ref bool demandeSautStrategique)
	{
		if (!ActiverIATerrainAdaptative || direction == Vector3.Zero)
			return direction;

		float angleVisionEvolutif = Mathf.Lerp(AngleVisionLateraleDegres * 0.75f, AngleVisionLateraleDegres * 1.45f, _genePrudenceNavigation);
		Vector3 dirCentre = direction.Normalized();
		// Correctif: la perception latérale était inversée visuellement.
		Vector3 dirGauche = dirCentre.Rotated(Vector3.Up, -Mathf.DegToRad(angleVisionEvolutif)).Normalized();
		Vector3 dirDroite = dirCentre.Rotated(Vector3.Up, Mathf.DegToRad(angleVisionEvolutif)).Normalized();

		// Lissage permanent: léger biais mémorisé entre deux scans pour rester fluide.
		if (Mathf.Abs(_biaisEvitementTerrain) > 0.01f)
		{
			float angleLisse = _biaisEvitementTerrain * Mathf.DegToRad(angleVisionEvolutif) * 0.42f;
			dirCentre = dirCentre.Rotated(Vector3.Up, angleLisse).Normalized();
			_biaisEvitementTerrain = Mathf.Lerp(_biaisEvitementTerrain, 0f, Mathf.Clamp(4f * dt, 0f, 1f));
		}

		if (_cooldownEvaluationVisionTerrain > 0f)
			return dirCentre;

		_cooldownEvaluationVisionTerrain = Mathf.Max(0.05f, IntervalleEvaluationVisionTerrain);

		float scoreCentre = EvaluerOuvertureDirectionTerrain(dirCentre);
		float scoreGauche = EvaluerOuvertureDirectionTerrain(dirGauche);
		float scoreDroite = EvaluerOuvertureDirectionTerrain(dirDroite);

		Vector3 meilleur = dirCentre;
		float meilleurScore = scoreCentre;
		float biaisCible = 0f;
		if (scoreGauche > meilleurScore + 0.04f)
		{
			meilleur = dirGauche;
			meilleurScore = scoreGauche;
			biaisCible = 1f;
		}
		if (scoreDroite > meilleurScore + 0.04f)
		{
			meilleur = dirDroite;
			meilleurScore = scoreDroite;
			biaisCible = -1f;
		}

		_biaisEvitementTerrain = Mathf.Lerp(_biaisEvitementTerrain, biaisCible, 0.8f);

		// Saut stratégique seulement si l'avant est bloqué mais la zone n'est pas un vide.
		if (ActiverSautStrategique && _cooldownSautStrategique <= 0f && IsOnFloor())
		{
			float seuilBlocagePourSaut = Mathf.Lerp(0.16f, 0.27f, _geneAudaceSaut);
			if (scoreCentre < seuilBlocagePourSaut && PeutSauterObstacleDevant(dirCentre) && GlobalPosition.DistanceTo(_positionDernierSaut) >= Mathf.Max(0.4f, DistanceMiniEntreDeuxSauts))
				demandeSautStrategique = true;
		}

		return meilleur;
	}

	private void EvaluerCoincageEtDeblocage(float dt, Vector3 direction, ref bool demandeSautStrategique)
	{
		if (direction == Vector3.Zero || _etat == EtatBoeuf.Mort || _etat == EtatBoeuf.Broutage)
		{
			_streakCoincage = 0;
			_timerDetectionCoincage = 0f;
			_positionReferenceCoincage = GlobalPosition;
			return;
		}

		_timerDetectionCoincage += dt;
		float intervalle = Mathf.Max(0.12f, IntervalleDetectionCoincage);
		if (_timerDetectionCoincage < intervalle)
			return;

		Vector3 delta = GlobalPosition - _positionReferenceCoincage;
		delta.Y = 0f;
		float progression = delta.Length();
		Vector3 versCible = _cibleCourante - GlobalPosition;
		versCible.Y = 0f;
		float distCible = versCible.Length();
		float vitesseHoriz = new Vector3(Velocity.X, 0f, Velocity.Z).Length();

		bool devraitAvancer = distCible > Mathf.Max(0.5f, DistanceCibleMinPourDetectionCoincage);
		bool peuDeProgres = progression < Mathf.Max(0.02f, ProgressionMinAvantCoincage);
		bool quasiImmobile = vitesseHoriz < 0.85f;

		if (devraitAvancer && peuDeProgres && quasiImmobile)
		{
			_streakCoincage++;
			AjusterScoreNavigation(-0.75f);
			if (_streakCoincage >= 2)
			{
				if (ActiverSautStrategique && IsOnFloor())
				{
					// Priorité au saut pour sortir d'un trou/coin contre obstacle.
					if (PeutSauterObstacleDevant(direction))
					{
						_cooldownSautStrategique = 0f;
						demandeSautStrategique = true;
					}
				}

				if (!demandeSautStrategique)
				{
					float angleSortie = _rng.RandfRange(-Mathf.Pi * 0.9f, Mathf.Pi * 0.9f);
					Vector3 sortie = direction.Rotated(Vector3.Up, angleSortie).Normalized();
					_cibleCourante = GlobalPosition + sortie * _rng.RandfRange(7f, 13f);
				}

				AjouterExperience(ExperienceEsquive * 0.35f, "anti_coincage");
				_streakCoincage = 0;
			}
		}
		else
		{
			_streakCoincage = Mathf.Max(0, _streakCoincage - 1);
			if (progression > Mathf.Max(0.2f, ProgressionMinAvantCoincage * 1.4f))
				AjusterScoreNavigation(0.28f);
		}

		_positionReferenceCoincage = GlobalPosition;
		_timerDetectionCoincage = 0f;
	}

	private void MettreAJourApprentissageNavigation(float dt, Vector3 direction, float vitesseHoriz)
	{
		if (!ActiverApprentissageNavigation || dt <= 0f || _etat == EtatBoeuf.Mort)
			return;
		if (_dansEau)
		{
			AjusterScoreNavigation(-0.08f * dt);
			return;
		}
		if (direction == Vector3.Zero)
			return;
		float vitesseRef = Mathf.Max(0.3f, VitesseMarche * 0.45f);
		if (vitesseHoriz > vitesseRef)
			AjusterScoreNavigation(0.18f * dt);
	}

	private float EvaluerOuvertureDirectionTerrain(Vector3 dir)
	{
		if (dir == Vector3.Zero)
			return 0f;

		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null)
			return 0.5f;

		Vector3 origine = GlobalPosition + Vector3.Up * HauteurYeuxTerrain;
		Vector3 fin = origine + dir * DistanceVisionAvant;
		var q = PhysicsRayQueryParameters3D.Create(origine, fin);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		float score = 1f;
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
		{
			Vector3 p = (Vector3)hit["position"];
			float d = origine.DistanceTo(p);
			score = Mathf.Clamp(d / Mathf.Max(0.1f, DistanceVisionAvant), 0f, 1f);
			if (hit.ContainsKey("normal"))
			{
				Vector3 n = ((Vector3)hit["normal"]).Normalized();
				if (n.Y < 0.35f)
					score *= 0.6f;
			}
		}

		if (ActiverDetectionVideDevant)
		{
			Vector3 origineVide = fin + Vector3.Up * 0.55f;
			Vector3 finVide = origineVide + Vector3.Down * (ProfondeurVideCritique + 0.75f);
			var qSol = PhysicsRayQueryParameters3D.Create(origineVide, finVide);
			qSol.CollideWithBodies = true;
			qSol.CollideWithAreas = false;
			if (GetRid().IsValid)
				qSol.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var sol = world.DirectSpaceState.IntersectRay(qSol);
			if (sol == null || sol.Count == 0 || !sol.ContainsKey("position"))
				score *= 0.25f;
			else
			{
				Vector3 pSol = (Vector3)sol["position"];
				if (origineVide.Y - pSol.Y > ProfondeurVideCritique)
					score *= 0.35f;
			}
		}

		return score;
	}

	private bool PeutSauterObstacleDevant(Vector3 dir)
	{
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null)
			return false;

		Vector3 origineBasse = GlobalPosition + Vector3.Up * 0.42f;
		Vector3 finBasse = origineBasse + dir * Mathf.Max(0.9f, DistanceVisionAvant * 0.55f);
		var qb = PhysicsRayQueryParameters3D.Create(origineBasse, finBasse);
		qb.CollideWithBodies = true;
		qb.CollideWithAreas = false;
		if (GetRid().IsValid)
			qb.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hitBas = world.DirectSpaceState.IntersectRay(qb);
		if (hitBas == null || hitBas.Count == 0)
			return false;
		if (!hitBas.ContainsKey("position"))
			return false;

		// Vérifie qu'il y a de l'air au-dessus de l'obstacle (sinon saut inutile).
		Vector3 origineHaute = GlobalPosition + Vector3.Up * 1.25f;
		Vector3 finHaute = origineHaute + dir * Mathf.Max(0.9f, DistanceVisionAvant * 0.55f);
		var qh = PhysicsRayQueryParameters3D.Create(origineHaute, finHaute);
		qh.CollideWithBodies = true;
		qh.CollideWithAreas = false;
		if (GetRid().IsValid)
			qh.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hitHaut = world.DirectSpaceState.IntersectRay(qh);
		bool hautLibre = hitHaut == null || hitHaut.Count == 0;
		if (!hautLibre)
			return false;

		// Vérifie qu'il y a du sol juste après l'obstacle pour éviter les sauts suicides.
		Vector3 obstacle = (Vector3)hitBas["position"];
		Vector3 origineSol = obstacle + dir * 0.85f + Vector3.Up * 1.6f;
		Vector3 finSol = origineSol + Vector3.Down * (ProfondeurVideCritique + 2.4f);
		var qs = PhysicsRayQueryParameters3D.Create(origineSol, finSol);
		qs.CollideWithBodies = true;
		qs.CollideWithAreas = false;
		if (GetRid().IsValid)
			qs.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var sol = world.DirectSpaceState.IntersectRay(qs);
		if (sol == null || sol.Count == 0 || !sol.ContainsKey("position"))
			return false;

		Vector3 pSol = (Vector3)sol["position"];
		float drop = origineSol.Y - pSol.Y;
		return drop <= Mathf.Max(1.2f, ProfondeurVideCritique + 0.5f);
	}

	private bool DoitTenterSautEscalade(Vector3 direction)
	{
		if (!ActiverSautStrategique || _cooldownSautStrategique > 0f || _dansEau)
			return false;
		if (direction == Vector3.Zero || !IsOnFloor())
			return false;
		if (GlobalPosition.DistanceTo(_positionDernierSaut) < Mathf.Max(0.4f, DistanceMiniEntreDeuxSauts))
			return false;
		if (!PeutSauterObstacleDevant(direction))
			return false;

		Vector3 avant = GlobalPosition + direction.Normalized() * Mathf.Max(0.5f, DistanceSautEscalade);
		int hActuel = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(GlobalPosition.X), Mathf.FloorToInt(GlobalPosition.Z), _seedTerrain);
		int hAvant = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(avant.X), Mathf.FloorToInt(avant.Z), _seedTerrain);
		float delta = hAvant - hActuel;
		if (delta >= DeltaHauteurMinSautEscalade && delta <= DeltaHauteurMaxSautEscalade)
			return true;
		return false;
	}

	private Vector3 AjusterDirectionAntiObstacle(Vector3 direction)
	{
		if (_cooldownAntiBlocage > 0f || direction == Vector3.Zero)
			return direction;

		_cooldownAntiBlocage = 0.38f;
		Vector3 origine = GlobalPosition + Vector3.Up * 0.55f;
		Vector3 fin = origine + direction * 2.4f;
		var query = PhysicsRayQueryParameters3D.Create(origine, fin);
		query.CollideWithBodies = true;
		query.CollideWithAreas = false;
		if (GetRid().IsValid)
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);
		if (hit == null || hit.Count == 0)
			return direction;

		float amplitude = Mathf.Lerp(1.45f, 0.85f, _genePrudenceNavigation);
		float angle = _rng.RandfRange(-amplitude, amplitude);
		Vector3 tourne = direction.Rotated(Vector3.Up, angle).Normalized();
		_cibleCourante = GlobalPosition + tourne * _rng.RandfRange(5f, 10f);
		AjouterExperience(ExperienceEsquive * 0.5f, "evitement_obstacle");
		return tourne;
	}

	private bool ConsommerHerbeSousPattes()
	{
		if (_gestionnaire == null) return false;
		_verrouMouvementMorsure = Mathf.Max(_verrouMouvementMorsure, DureeImmobilePendantMorsure);
		DeclencherAnimationMorsureHerbe();
		Vector3 cible = GlobalPosition + Vector3.Down * 0.2f;
		// Variante "faune" : retire l'herbe visuelle sans générer de loot au sol.
		bool aMangeHerbe3D = _gestionnaire.AppliquerFauchageFauneGlobal(cible, RayonMangerHerbe);
		if (!aMangeHerbe3D)
			return false; // Sans mesh 3D d'herbe a portée, pas de nutrition.
		float gainFaim = Mathf.Max(0.1f, _faimMaxActuelle * 0.10f);
		_faimCourante = Mathf.Min(_faimMaxActuelle, _faimCourante + gainFaim);
		MettreAJourAffichageFaim3D();
		AjouterExperience(ExperienceBroutage, "broutage");
		return true;
	}

	private void DeclencherAnimationMorsureHerbe()
	{
		if (_playbackEtatFaune != null && _machineAPorteBroutage)
		{
			_playbackEtatFaune.Travel(NomNoeudBroutageString);
			return;
		}
		if (!string.IsNullOrEmpty(_clipManger) && !NomClipSembleMort(_clipManger) && _animationPlayer != null && _animationPlayer.HasAnimation(_clipManger))
			_animationPlayer.Play(_clipManger, 0.08f);
	}
}
