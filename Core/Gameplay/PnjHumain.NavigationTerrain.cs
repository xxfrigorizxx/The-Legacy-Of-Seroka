using Godot;

/// <summary>
/// Contournement d'obstacles (arbres, rochers) et lecture de la pente devant soi.
/// Raycasts espacés (~7 Hz) pour ne pas tuer les FPS — inspiré de BoeufSauvage.NavigationTerrain.
/// </summary>
public partial class PnjHumain : CharacterBody3D
{
	private const float IntervalleVisionTerrainSec = 0.12f;
	private const float DistanceVisionAvantPnj = 4.2f;
	private const float DistanceVisionMigrationPnj = 7.8f;
	private const float AngleVisionLateraleDeg = 58f;
	private const float AngleVisionLateraleMigrationDeg = 82f;
	private const float HauteurYeuxPnj = 1.05f;
	private const float IntervalleDetectionCoincageSec = 0.28f;
	private const float ProgressionMinCoincage = 0.06f;
	private const int DeltaHauteurMaxMarche = 2;
	private const int DeltaHauteurMur = 4;
	private const float CooldownCreusageSec = 0.55f;
	private const float RayonCreusageMainNu = 0.42f;
	private const float ProfondeurVideCritique = 2.4f;
	/// <summary>Même marge que le joueur — évite le contact parfait qui fait vibrer le corps.</summary>
	private const float MargeEpsilonPiedsSurSol = 0.07f;
	/// <summary>Y du pivot CharacterBody3D quand la hauteur terrain procédurale est connue (hitboxes jambes + epsilon).</summary>
	public const float DecalageYOrigineDepuisHauteurTerrainVoxel = 0.865f;

	private float _cooldownEvaluationVisionTerrain;
	private float _biaisEvitementTerrain;
	private float _timerDetectionCoincage;
	private Vector3 _positionReferenceCoincage;
	private int _streakCoincage;
	private float _cooldownCreusageObstacle;
	private float _vitesseHorizReelleLissee;
	private bool _demandeSautStrategiqueNav;

	private bool EnNavigationMigration => _etatPnj == EtatPnj.Migration;

	/// <summary>Ajuste la direction désirée pour contourner arbres/murs et éviter les falaises trop hautes.</summary>
	private Vector3 AdapterDirectionNavigation(Vector3 direction, float dt)
	{
		if (direction.LengthSquared() < 0.001f || _pnjDansEau)
			return direction;

		Vector3 dir = direction.Normalized();
		MettreAJourVitesseReelle(dt);
		EvaluerCoincageEtDeblocage(dt, dir);

		float angleLat = Mathf.DegToRad(EnNavigationMigration ? AngleVisionLateraleMigrationDeg : AngleVisionLateraleDeg);
		Vector3 dirCentre = dir;
		if (Mathf.Abs(_biaisEvitementTerrain) > 0.01f)
		{
			float facteurBiais = Mathf.Abs(_biaisEvitementTerrain) > 1f ? 0.72f : 0.48f;
			dirCentre = dirCentre.Rotated(Vector3.Up, _biaisEvitementTerrain * angleLat * facteurBiais).Normalized();
			float decay = Mathf.Abs(_biaisEvitementTerrain) > 1f ? 1.8f : 3.5f;
			_biaisEvitementTerrain = Mathf.Lerp(_biaisEvitementTerrain, 0f, Mathf.Clamp(decay * dt, 0f, 1f));
		}

		if (_cooldownEvaluationVisionTerrain > 0f)
			return dirCentre;

		_cooldownEvaluationVisionTerrain = IntervalleVisionTerrainSec;

		Vector3 dirGauche = dir.Rotated(Vector3.Up, -angleLat).Normalized();
		Vector3 dirDroite = dir.Rotated(Vector3.Up, angleLat).Normalized();
		Vector3 dirGaucheLeger = dir.Rotated(Vector3.Up, -angleLat * 0.5f).Normalized();
		Vector3 dirDroiteLeger = dir.Rotated(Vector3.Up, angleLat * 0.5f).Normalized();
		Vector3 dirGaucheFort = dir.Rotated(Vector3.Up, -angleLat * 1.35f).Normalized();
		Vector3 dirDroiteFort = dir.Rotated(Vector3.Up, angleLat * 1.35f).Normalized();

		float scoreCentre = EvaluerScoreDirection(dirCentre, dir);
		float scoreGauche = EvaluerScoreDirection(dirGauche, dir);
		float scoreDroite = EvaluerScoreDirection(dirDroite, dir);
		float scoreGaucheLeger = EvaluerScoreDirection(dirGaucheLeger, dir);
		float scoreDroiteLeger = EvaluerScoreDirection(dirDroiteLeger, dir);
		float scoreGaucheFort = EvaluerScoreDirection(dirGaucheFort, dir);
		float scoreDroiteFort = EvaluerScoreDirection(dirDroiteFort, dir);

		Vector3 meilleur = dirCentre;
		float meilleurScore = scoreCentre;
		float biais = 0f;
		void Considerer(Vector3 candidat, float score, float biaisCandidat)
		{
			if (score <= meilleurScore + 0.03f)
				return;
			meilleur = candidat;
			meilleurScore = score;
			biais = biaisCandidat;
		}

		Considerer(dirGaucheLeger, scoreGaucheLeger, 0.55f);
		Considerer(dirDroiteLeger, scoreDroiteLeger, -0.55f);
		Considerer(dirGauche, scoreGauche, 1f);
		Considerer(dirDroite, scoreDroite, -1f);
		if (EnNavigationMigration)
		{
			Considerer(dirGaucheFort, scoreGaucheFort, 1.35f);
			Considerer(dirDroiteFort, scoreDroiteFort, -1.35f);
		}

		_biaisEvitementTerrain = Mathf.Lerp(_biaisEvitementTerrain, biais, 0.8f);
		return meilleur;
	}

	private float EvaluerScoreDirection(Vector3 probeDir, Vector3 dirObjectif)
	{
		float ouverture = EvaluerOuvertureRayon(probeDir);
		float pente = EvaluerScorePenteDevant(probeDir);
		float alignement = Mathf.Clamp(dirObjectif.Dot(probeDir), -1f, 1f) * 0.5f + 0.5f;
		float biome = EvaluerScoreBiomeDevant(probeDir);
		if (EnNavigationMigration)
			return ouverture * 0.58f + pente * 0.18f + alignement * 0.04f + biome * 0.20f;
		return ouverture * 0.50f + pente * 0.28f + alignement * 0.08f + biome * 0.14f;
	}

	private float EvaluerScoreBiomeDevant(Vector3 dir)
	{
		if (!DoitMigrerPourEtablirCamp() && !ColonieDoitResterUnieSansCamp())
			return 0.5f;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x0 = Mathf.FloorToInt(GlobalPosition.X);
		int z0 = Mathf.FloorToInt(GlobalPosition.Z);
		float scoreOrigine = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x0, z0, seed);
		float probe = EnNavigationMigration ? 30f : 18f;
		Vector3 p = GlobalPosition + dir * probe;
		int x1 = Mathf.FloorToInt(p.X);
		int z1 = Mathf.FloorToInt(p.Z);
		float score = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x1, z1, seed);
		float delta = score - scoreOrigine;
		return Mathf.Clamp(0.5f + delta * 0.22f, 0f, 1f);
	}

	private float ObtenirDistanceVisionActuelle()
		=> EnNavigationMigration ? DistanceVisionMigrationPnj : DistanceVisionAvantPnj;

	private float EvaluerOuvertureRayon(Vector3 dir)
	{
		World3D world = GetWorld3D();
		if (world?.DirectSpaceState == null)
			return 0.5f;

		float distVision = ObtenirDistanceVisionActuelle();
		Vector3 origine = GlobalPosition + Vector3.Up * HauteurYeuxPnj;
		Vector3 fin = origine + dir * distVision;
		var q = PhysicsRayQueryParameters3D.Create(origine, fin);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		float score = 1f;
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
		{
			float dist = origine.DistanceTo((Vector3)hit["position"]);
			score = Mathf.Clamp(dist / distVision, 0f, 1f);
			if (dist < 1.15f)
				score *= 0.12f; // arbre / mur très proche : éviter fortement
			if (hit.ContainsKey("normal"))
			{
				Vector3 n = ((Vector3)hit["normal"]).Normalized();
				if (n.Y < 0.4f)
					score *= 0.35f;
			}
		}

		// Second rayon à hauteur genoux : détecte les troncs bas.
		Vector3 origineBas = GlobalPosition + Vector3.Up * 0.45f;
		Vector3 finBas = origineBas + dir * distVision;
		var qBas = PhysicsRayQueryParameters3D.Create(origineBas, finBas);
		qBas.CollideWithBodies = true;
		qBas.CollideWithAreas = false;
		if (GetRid().IsValid)
			qBas.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hitBas = world.DirectSpaceState.IntersectRay(qBas);
		if (hitBas != null && hitBas.Count > 0 && hitBas.ContainsKey("position"))
		{
			float distBas = origineBas.DistanceTo((Vector3)hitBas["position"]);
			if (distBas < 1.35f)
				score *= Mathf.Clamp(distBas / 1.35f, 0.08f, 0.55f);
		}

		Vector3 origineVide = fin + Vector3.Up * 0.55f;
		Vector3 finVide = origineVide + Vector3.Down * (ProfondeurVideCritique + 0.75f);
		var qSol = PhysicsRayQueryParameters3D.Create(origineVide, finVide);
		qSol.CollideWithBodies = true;
		qSol.CollideWithAreas = false;
		if (GetRid().IsValid)
			qSol.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var sol = world.DirectSpaceState.IntersectRay(qSol);
		if (sol == null || sol.Count == 0 || !sol.ContainsKey("position"))
			score *= 0.2f;
		else
		{
			Vector3 pSol = (Vector3)sol["position"];
			if (origineVide.Y - pSol.Y > ProfondeurVideCritique)
				score *= 0.3f;
		}

		return score;
	}

	private float EvaluerScorePenteDevant(Vector3 dir)
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x0 = Mathf.FloorToInt(GlobalPosition.X);
		int z0 = Mathf.FloorToInt(GlobalPosition.Z);
		int h0 = Generateur_Voxel.ObtenirHauteurTerrainMonde(x0, z0, seed);

		Vector3 avant = GlobalPosition + dir * 1.4f;
		int x1 = Mathf.FloorToInt(avant.X);
		int z1 = Mathf.FloorToInt(avant.Z);
		int h1 = Generateur_Voxel.ObtenirHauteurTerrainMonde(x1, z1, seed);
		int delta = h1 - h0;

		if (delta <= DeltaHauteurMaxMarche)
			return 1f;
		if (delta <= DeltaHauteurMur)
			return 0.45f;
		return 0.08f;
	}

	private void MettreAJourVitesseReelle(float dt)
	{
		Vector3 v = new Vector3(Velocity.X, 0f, Velocity.Z);
		float cible = v.Length();
		float k = Mathf.Clamp(8f * dt, 0f, 1f);
		_vitesseHorizReelleLissee = Mathf.Lerp(_vitesseHorizReelleLissee, cible, k);
	}

	private void EvaluerCoincageEtDeblocage(float dt, Vector3 direction)
	{
		_cooldownCreusageObstacle -= dt;
		_timerDetectionCoincage += dt;
		if (_timerDetectionCoincage < IntervalleDetectionCoincageSec)
			return;

		Vector3 delta = GlobalPosition - _positionReferenceCoincage;
		delta.Y = 0f;
		bool peuAvance = delta.Length() < ProgressionMinCoincage;
		bool quasiImmobile = _vitesseHorizReelleLissee < 0.35f;
		bool veutAvancer = direction.LengthSquared() > 0.01f;

		if (veutAvancer && peuAvance && quasiImmobile)
		{
			_streakCoincage++;
			if (_streakCoincage >= 2)
			{
				if (IsOnFloor() && (DoitTenterSautEscaladePnj(direction) || DoitSauterPnj(direction)))
					_demandeSautStrategiqueNav = true;
				else if (!TenterCreuserObstacleDevant(direction))
					TenterContournerObstacleLocal(direction);

				_cooldownEvaluationVisionTerrain = 0f;
				_streakCoincage = 0;
			}
		}
		else
			_streakCoincage = Mathf.Max(0, _streakCoincage - 1);

		_positionReferenceCoincage = GlobalPosition;
		_timerDetectionCoincage = 0f;
	}

	private void TenterContournerObstacleLocal(Vector3 direction)
	{
		float angleLat = Mathf.DegToRad(EnNavigationMigration ? AngleVisionLateraleMigrationDeg : AngleVisionLateraleDeg);
		Vector3 dirGauche = direction.Rotated(Vector3.Up, -angleLat).Normalized();
		Vector3 dirDroite = direction.Rotated(Vector3.Up, angleLat).Normalized();
		Vector3 dirGaucheFort = direction.Rotated(Vector3.Up, -angleLat * 1.35f).Normalized();
		Vector3 dirDroiteFort = direction.Rotated(Vector3.Up, angleLat * 1.35f).Normalized();
		float scoreGauche = EvaluerScoreDirection(dirGauche, direction);
		float scoreDroite = EvaluerScoreDirection(dirDroite, direction);
		float scoreGaucheFort = EvaluerScoreDirection(dirGaucheFort, direction);
		float scoreDroiteFort = EvaluerScoreDirection(dirDroiteFort, direction);
		float meilleurGauche = Mathf.Max(scoreGauche, scoreGaucheFort);
		float meilleurDroite = Mathf.Max(scoreDroite, scoreDroiteFort);
		float signe = meilleurGauche >= meilleurDroite ? 1f : -1f;
		if (Mathf.Abs(meilleurGauche - meilleurDroite) < 0.05f)
			signe = _rngPnj.Randf() < 0.5f ? 1f : -1f;
		_biaisEvitementTerrain = signe * (EnNavigationMigration ? 2.4f : 1.6f);
	}

	/// <summary>Creuse une marche de terrain (neige/sable/aride) sans toucher herbe/buissons.</summary>
	private bool TenterCreuserObstacleDevant(Vector3 direction)
	{
		if (_cooldownCreusageObstacle > 0f)
			return false;

		World3D world = GetWorld3D();
		if (world?.DirectSpaceState == null)
			return false;

		Vector3 d = direction.Normalized();
		Vector3 origineYeux = GlobalPosition + Vector3.Up * HauteurYeuxPnj;
		var qArbre = PhysicsRayQueryParameters3D.Create(origineYeux, origineYeux + d * 1.6f);
		qArbre.CollideWithBodies = true;
		qArbre.CollideWithAreas = false;
		if (GetRid().IsValid)
			qArbre.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hitArbre = world.DirectSpaceState.IntersectRay(qArbre);
		if (hitArbre != null && hitArbre.Count > 0 && hitArbre.ContainsKey("position"))
		{
			float dist = origineYeux.DistanceTo((Vector3)hitArbre["position"]);
			if (dist < 1.35f)
				return false; // arbre/obstacle solide : contourner, pas creuser
		}

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x0 = Mathf.FloorToInt(GlobalPosition.X);
		int z0 = Mathf.FloorToInt(GlobalPosition.Z);
		int h0 = Generateur_Voxel.ObtenirHauteurTerrainMonde(x0, z0, seed);
		Vector3 avant = GlobalPosition + d * 1.1f;
		int x1 = Mathf.FloorToInt(avant.X);
		int z1 = Mathf.FloorToInt(avant.Z);
		int h1 = Generateur_Voxel.ObtenirHauteurTerrainMonde(x1, z1, seed);
		int delta = h1 - h0;
		if (delta < 1 || delta > 2)
			return false;
		if (!PnjHumainBiomeInstinct.EstMatiereCrevassableMainNu(x1, z1, seed))
			return false;

		float yBloc = h0 + (delta == 1 ? 1.5f : 2.5f);
		Vector3 impact = new Vector3(avant.X, yBloc, avant.Z);
		gm?.AppliquerCreusageTerrainPnj(impact, RayonCreusageMainNu);
		_cooldownCreusageObstacle = CooldownCreusageSec;
		_cooldownEvaluationVisionTerrain = 0f;
		DiagForage($"creusage terrain Δ{delta} bloc devant ({x1},{z1})");
		return true;
	}

	/// <summary>Point le plus bas des hitboxes actives — même logique que le joueur (pas la capsule de référence désactivée).</summary>
	private float CalculerBasCollisionLocal()
	{
		float minY = float.MaxValue;
		foreach (Node c in GetChildren())
		{
			if (c is not CollisionShape3D cs || cs.Disabled || cs.Shape == null)
				continue;
			float y = CalculerBasYLocalPourCollisionShape(cs);
			if (y != float.MaxValue)
				minY = Mathf.Min(minY, y);
		}
		return minY == float.MaxValue ? -0.9f : minY;
	}

	private static float CalculerBasYLocalPourCollisionShape(CollisionShape3D cs)
	{
		if (cs?.Shape == null)
			return float.MaxValue;
		Transform3D t = cs.Transform;
		switch (cs.Shape)
		{
			case CapsuleShape3D cap:
			{
				float half = cap.Height * 0.5f + cap.Radius;
				return (t * new Vector3(0f, -half, 0f)).Y;
			}
			case SphereShape3D sph:
				return (t * new Vector3(0f, -sph.Radius, 0f)).Y;
			default:
				return float.MaxValue;
		}
	}

	/// <summary>Y global du pivot pour poser le bas des hitboxes sur le contact sol (raycast / mesh).</summary>
	public float CalculerYOriginePourPiedsSurSurface(float yContactSolWorld, float epsilon = -1f)
	{
		if (epsilon < 0f)
			epsilon = MargeEpsilonPiedsSurSol;
		return yContactSolWorld - CalculerBasCollisionLocal() + epsilon;
	}

	/// <summary>Aligne le PNJ sur le sol collision après re-mat (évite chute / invisibilité).</summary>
	public void PositionnerSurSolApresRemat(int tentativesRestantes = 16)
	{
		if (!GodotObject.IsInstanceValid(this))
			return;

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		PhysicsDirectSpaceState3D espace = GetWorld3D()?.DirectSpaceState;
		Vector3 depuis = new Vector3(GlobalPosition.X, GlobalPosition.Y + 12f, GlobalPosition.Z);
		if (espace != null)
		{
			var q = PhysicsRayQueryParameters3D.Create(depuis, depuis + Vector3.Down * 40f);
			q.CollideWithBodies = true;
			q.CollideWithAreas = false;
			if (GetRid().IsValid)
				q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var hit = espace.IntersectRay(q);
			if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
			{
				Vector3 sol = (Vector3)hit["position"];
				GlobalPosition = new Vector3(GlobalPosition.X, CalculerYOriginePourPiedsSurSurface(sol.Y), GlobalPosition.Z);
				Velocity = Vector3.Zero;
				return;
			}
		}

		float yProc = PnjHumainBiomeInstinct.HauteurSolMonde(GlobalPosition.X, GlobalPosition.Z, seed);
		GlobalPosition = new Vector3(GlobalPosition.X, yProc, GlobalPosition.Z);
		Velocity = Vector3.Zero;
		if (tentativesRestantes > 0)
			Callable.From(() => PositionnerSurSolApresRemat(tentativesRestantes - 1)).CallDeferred();
	}
}
