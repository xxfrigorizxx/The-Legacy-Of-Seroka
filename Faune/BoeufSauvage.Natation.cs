using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private readonly Vector3[] _echantillonsImmersionFaune = new Vector3[6];

	private bool EstDansEau()
	{
		if (!ActiverNatationFaune)
			return false;
		if (ModeSmokeTestForcerDetectionEau)
			return true;
		if (_gestionnaire == null)
			return false;
		Vector3 dirAvant = _directionDeplacementHorizontale.LengthSquared() > 0.001f
			? _directionDeplacementHorizontale.Normalized()
			: (-GlobalTransform.Basis.Z).Normalized();
		Vector3 pPieds = GlobalPosition + Vector3.Up * 0.05f;
		Vector3 pBas = GlobalPosition + Vector3.Down * 0.38f;
		Vector3 pVentre = GlobalPosition + Vector3.Up * 0.62f;
		Vector3 pPoitrine = GlobalPosition + Vector3.Up * 1.03f;

		_echantillonsImmersionFaune[0] = pPieds - GlobalPosition;
		_echantillonsImmersionFaune[1] = pBas - GlobalPosition;
		_echantillonsImmersionFaune[2] = pVentre - GlobalPosition;
		_echantillonsImmersionFaune[3] = pPoitrine - GlobalPosition;
		_echantillonsImmersionFaune[4] = (pBas + dirAvant * 0.55f) - GlobalPosition;
		_echantillonsImmersionFaune[5] = (pVentre + dirAvant * 0.55f) - GlobalPosition;
		float ratioImmersion = _gestionnaire.CalculerRatioImmersion(GlobalPosition, _echantillonsImmersionFaune);
		if (ratioImmersion >= 0.5f)
			return true;

		// Anti "marche sur l'eau": si pas de sol dur sous les pattes mais eau détectée dessous, forcer nage.
		World3D world = GetWorld3D();
		if (world != null && world.DirectSpaceState != null)
		{
			Vector3 origine = GlobalPosition + Vector3.Up * 0.45f;
			Vector3 fin = origine + Vector3.Down * 2.8f;
			var q = PhysicsRayQueryParameters3D.Create(origine, fin);
			q.CollideWithBodies = true;
			q.CollideWithAreas = false;
			if (GetRid().IsValid)
				q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var sol = world.DirectSpaceState.IntersectRay(q);
			bool solProche = sol != null && sol.Count > 0 && sol.ContainsKey("position")
				&& origine.DistanceTo((Vector3)sol["position"]) <= 1.2f;
			bool eauDessous = _gestionnaire.EstPointDansEau(GlobalPosition + Vector3.Down * 0.55f)
				|| _gestionnaire.ObtenirMatiereExacte(GlobalPosition + Vector3.Down * 0.55f) == 4;
			if (!solProche && eauDessous && GlobalPosition.Y <= NiveauSurfaceEauReference + 1.2f)
				return true;
		}
		return false;
	}

	private bool PointImmergéFaune(Vector3 p)
	{
		if (_gestionnaire == null)
			return false;
		return _gestionnaire.EstPointImmergeEau(p);
	}

	private bool TrouverDirectionSortieEau(Vector3 directionActuelle, out Vector3 directionSortie)
	{
		directionSortie = Vector3.Zero;
		if (_gestionnaire == null)
			return false;

		float rayon = Mathf.Clamp(RayonRechercheSortieEau, 2f, 40f);
		Vector3 baseDir = directionActuelle.LengthSquared() > 0.001f
			? directionActuelle.Normalized()
			: (_directionDeplacementHorizontale.LengthSquared() > 0.001f
				? _directionDeplacementHorizontale.Normalized()
				: (-GlobalTransform.Basis.Z).Normalized());

		float meilleurScore = float.MinValue;
		int echantillonsAngle = 18;
		float pasDistance = 1.2f;
		for (int i = 0; i < echantillonsAngle; i++)
		{
			float t = i / (float)echantillonsAngle;
			float angle = -Mathf.Pi + t * Mathf.Tau;
			Vector3 dir = baseDir.Rotated(Vector3.Up, angle).Normalized();
			if (dir.LengthSquared() < 0.001f)
				continue;

			for (float d = 1.8f; d <= rayon; d += pasDistance)
			{
				Vector3 p = GlobalPosition + dir * d;
				bool eauPieds = PointImmergéFaune(p + Vector3.Up * 0.06f);
				bool eauVentre = PointImmergéFaune(p + Vector3.Up * 0.60f);
				if (eauPieds || eauVentre)
					continue;

				float alignement = Mathf.Clamp(baseDir.Dot(dir), -1f, 1f);
				float scoreOuverture = EvaluerOuvertureDirectionTerrain(dir);
				float score = scoreOuverture * 2.2f + alignement * 1.3f - (d / rayon) * 1.5f;
				if (score > meilleurScore)
				{
					meilleurScore = score;
					directionSortie = dir;
				}
				break;
			}
		}
		return directionSortie.LengthSquared() > 0.001f;
	}

	private Vector3 CalculerDirectionNage(Vector3 directionActuelle, float dt)
	{
		if (!_dansEau || _gestionnaire == null)
			return directionActuelle;
		float surfaceEau = _gestionnaire.ObtenirNiveauSurfaceEau();
		_eauIntentionRemonter = GlobalPosition.Y < surfaceEau - 0.55f;

		_cooldownDirectionNage -= dt;
		if (_cooldownDirectionNage > 0f && _directionNageEau.LengthSquared() > 0.001f)
			return _directionNageEau;
		_cooldownDirectionNage = Mathf.Max(0.05f, IntervalleRecalculDirectionNage);

		// Instinct bovin: chercher d'abord une rive sèche avant de conserver une nage aléatoire.
		if (TrouverDirectionSortieEau(directionActuelle, out Vector3 sortie))
		{
			_eauIntentionRemonter = true;
			_directionNageEau = sortie;
			return _directionNageEau;
		}

		if (directionActuelle.LengthSquared() > 0.001f)
		{
			_directionNageEau = directionActuelle.Normalized();
			return _directionNageEau;
		}

		float angle = _rng.RandfRange(0f, Mathf.Tau);
		_directionNageEau = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).Normalized();
		return _directionNageEau;
	}

	/// <param name="nageHorizontaleOk">Stamina suffisante pour nager en horizontal (comme avant).</param>
	/// <param name="remonteActive">Équivalent joueur « saut maintenu » : intention de remonter (rive / profondeur).</param>
	private void AppliquerPhysiqueNatation(float dt, ref Vector3 vHoriz, ref float vy, bool nageHorizontaleOk, bool remonteActive)
	{
		float surface = _gestionnaire != null ? _gestionnaire.ObtenirNiveauSurfaceEau() : (NiveauSurfaceEauReference + 0.35f);
		bool sousSurface = GlobalPosition.Y < surface;
		bool peutNager = _staminaCourante > 0.35f;
		bool nageHorizEffective = nageHorizontaleOk && peutNager;
		bool remonteEffective = remonteActive && peutNager && sousSurface;

		if (nageHorizEffective)
		{
			EssayerDepenserStamina(CoutStaminaNageParSeconde * dt);
			float maxNage = VitesseNageHorizontale * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
			if (vHoriz.Length() > maxNage)
				vHoriz = vHoriz.Normalized() * maxNage;
		}
		else
		{
			vHoriz *= Mathf.Clamp(1f - 1.6f * dt, 0f, 1f);
		}

		// Aligné sur Joueur._PhysicsProcess (eau) : remontée seulement si effort « vers le haut » ; sinon gravité atténuée sous l’eau.
		if (remonteEffective)
		{
			// Même principe que <c>sautMaintenu</c> chez le joueur (cible surface + 0,12 m).
			EssayerDepenserStamina(CoutStaminaMaintienSurfaceParSeconde * dt);
			float cibleY = surface + 0.12f;
			float erreurY = cibleY - GlobalPosition.Y;
			float vYCible = Mathf.Clamp(erreurY * 5.2f, -1.65f, 3.2f);
			vy = Mathf.MoveToward(vy, vYCible, 9.2f * dt);
		}
		else if (sousSurface && !IsOnFloor())
		{
			vy += GetGravity().Y * (0.32f * dt);
		}
		else if (!sousSurface)
		{
			vy = Mathf.MoveToward(vy, -0.16f, (GraviteDansEau + 0.9f) * dt);
		}

		if (remonteEffective)
		{
			// Stabilisation légère si remontée active (évite yoyo).
			float cibleSurface = surface - 0.50f;
			float erreurSurface = cibleSurface - GlobalPosition.Y;
			float correctionSurface = Mathf.Clamp(erreurSurface * 0.45f, -0.35f, 0.45f);
			vy = Mathf.MoveToward(vy, vy + correctionSurface, 2.2f * dt);
		}

		vy = Mathf.Clamp(vy, -2.1f, 2.35f);
	}
}
