using Godot;

/// <summary>
/// Déplacement avancé du PNJ : NAGE (quand il est immergé ≥50%, comme le joueur/faune) et SAUT (franchir une
/// marche/un rebord). Répliques fidèles de la physique du joueur (Core/Movement/PlayerMovementWater.cs : bloc « eau »
/// + saut) et de l'instinct de sortie d'eau de la faune (Faune/BoeufSauvage.Natation.cs). Rien de cheaté : mêmes
/// vitesses et même seuil d'immersion que le joueur.
/// </summary>
public partial class PnjHumain : CharacterBody3D
{
	private const float VitesseSautPnj = 7.6f;        // ~1,25 m sous Gravite=24 -> franchit une marche d'1 voxel
	private const float CooldownSautPnjSec = 0.7f;    // évite de spammer le saut contre un vrai mur
	private const float FacteurVitesseNagePnj = 0.5f; // ~ vitesse de nage du joueur (Speed * 0.4..0.58)

	private bool _pnjDansEau;
	private float _surfaceEauPnj = 103.35f;
	private float _cooldownSautPnj;

	// Mêmes points d'échantillonnage que le joueur (PlayerMovementWater) -> même seuil exact de bascule en nage.
	private static readonly Vector3[] EchantillonsImmersionPnj =
	{
		Vector3.Up * 0.08f, Vector3.Up * 0.28f, Vector3.Up * 0.48f,
		Vector3.Up * 0.68f, Vector3.Up * 0.88f, Vector3.Up * 1.08f, Vector3.Up * 1.28f
	};

	/// <summary>True si au moins 50% du corps est immergé (bascule en nage), exactement comme le joueur.</summary>
	private bool EvaluerEtatEauPnj(out float surfaceEau)
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		surfaceEau = gm?.ObtenirNiveauSurfaceEau() ?? 103.35f;
		if (gm == null)
			return false;
		return gm.CalculerRatioImmersion(GlobalPosition, EchantillonsImmersionPnj) >= 0.5f;
	}

	/// <summary>
	/// Physique de nage : avance vers son cap (cible de forage/social) ou, à défaut, vers une rive sèche (instinct :
	/// ne pas flotter sur place), et remonte vers la surface pour ne pas couler. Calqué sur le bloc « eau » de
	/// Joueur._PhysicsProcess (cible surface + 0,12 m, montée lissée).
	/// </summary>
	private void AppliquerNagePnj(ref Vector3 v, Vector3 dir, float dt)
	{
		Vector3 dirNage = dir;
		if (dirNage.LengthSquared() < 0.001f && TrouverDirectionSortieEauPnj(out Vector3 sortie))
			dirNage = sortie;

		float vitesse = Joueur.Speed * FacteurVitesseNagePnj;
		v.X = dirNage.X * vitesse;
		v.Z = dirNage.Z * vitesse;

		bool sousSurface = GlobalPosition.Y < _surfaceEauPnj;
		if (sousSurface)
		{
			// Remontée franche vers la surface (équivalent « saut maintenu » du joueur en nage).
			float cibleY = _surfaceEauPnj + 0.12f;
			float erreurY = cibleY - GlobalPosition.Y;
			float vYCible = Mathf.Clamp(erreurY * 5.2f, -1.65f, 3.2f);
			v.Y = Mathf.MoveToward(v.Y, vYCible, 9.2f * dt);
		}
		else
		{
			// À la surface : très légère gravité pour rester à fleur d'eau sans léviter.
			v.Y = Mathf.MoveToward(v.Y, -0.16f, (Gravite * 0.12f) * dt);
		}
		v.Y = Mathf.Clamp(v.Y, -4.5f, 4.5f); // évite les vitesses verticales extrêmes (clip eau/berge)
	}

	/// <summary>Cherche la direction horizontale la plus courte vers une case NON immergée (sortie d'eau). Inspiré de la faune.</summary>
	private bool TrouverDirectionSortieEauPnj(out Vector3 sortie)
	{
		sortie = Vector3.Zero;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null)
			return false;
		Vector3 baseDir = (-GlobalTransform.Basis.Z).Normalized();
		float meilleur = float.MinValue;
		for (int i = 0; i < 12; i++)
		{
			float angle = -Mathf.Pi + (i / 12f) * Mathf.Tau;
			Vector3 d = baseDir.Rotated(Vector3.Up, angle);
			if (d.LengthSquared() < 0.001f)
				continue;
			for (float dist = 1.8f; dist <= 12f; dist += 1.2f)
			{
				Vector3 p = GlobalPosition + d * dist;
				bool eau = gm.EstPointImmergeEau(p + Vector3.Up * 0.06f) || gm.EstPointImmergeEau(p + Vector3.Up * 0.60f);
				if (eau)
					continue;
				float score = baseDir.Dot(d) * 1.0f - (dist / 12f) * 1.5f; // privilégie une rive proche et droit devant
				if (score > meilleur) { meilleur = score; sortie = d; }
				break;
			}
		}
		return sortie.LengthSquared() > 0.001f;
	}

	/// <summary>
	/// Décide d'un SAUT : un obstacle bas barre la route (≈ une marche d'un voxel devant) MAIS l'espace au-dessus de
	/// cette marche est libre -> rebord franchissable. Si c'est bloqué jusqu'à hauteur de saut, c'est un vrai mur :
	/// pas de saut inutile (le cooldown évite de toute façon le matraquage).
	/// </summary>
	private bool DoitSauterPnj(Vector3 dirHoriz)
	{
		if (DoitTenterSautEscaladePnj(dirHoriz))
			return true;

		World3D w = GetWorld3D();
		if (w?.DirectSpaceState == null)
			return false;
		Vector3 d = new Vector3(dirHoriz.X, 0f, dirHoriz.Z);
		if (d.LengthSquared() < 0.001f)
			return false;
		d = d.Normalized();
		bool basBloque = RayObstaclePnj(w, GlobalPosition + Vector3.Down * 0.60f, d, 0.70f);
		if (!basBloque)
			return false;
		bool hautLibre = !RayObstaclePnj(w, GlobalPosition + Vector3.Up * 0.25f, d, 0.70f);
		return hautLibre;
	}

	/// <summary>Saut pour franchir une marche de 2–3 blocs (montagne douce) — 1 vérif / ~0,14 s max.</summary>
	private bool DoitTenterSautEscaladePnj(Vector3 dirHoriz)
	{
		if (_cooldownSautPnj > 0f || !IsOnFloor())
			return false;
		Vector3 d = new Vector3(dirHoriz.X, 0f, dirHoriz.Z);
		if (d.LengthSquared() < 0.001f)
			return false;
		d = d.Normalized();

		World3D w = GetWorld3D();
		if (w?.DirectSpaceState == null)
			return false;
		Vector3 origineBasse = GlobalPosition + Vector3.Up * 0.42f;
		var qb = PhysicsRayQueryParameters3D.Create(origineBasse, origineBasse + d * 1.1f);
		qb.CollideWithBodies = true;
		qb.CollideWithAreas = false;
		if (GetRid().IsValid)
			qb.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hitBas = w.DirectSpaceState.IntersectRay(qb);
		if (hitBas == null || hitBas.Count == 0)
			return false;

		Vector3 origineHaute = GlobalPosition + Vector3.Up * 1.2f;
		var qh = PhysicsRayQueryParameters3D.Create(origineHaute, origineHaute + d * 1.1f);
		qh.CollideWithBodies = true;
		qh.CollideWithAreas = false;
		if (GetRid().IsValid)
			qh.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hitHaut = w.DirectSpaceState.IntersectRay(qh);
		if (hitHaut != null && hitHaut.Count > 0)
			return false;

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		Vector3 avant = GlobalPosition + d * 1.2f;
		int h0 = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(GlobalPosition.X), Mathf.FloorToInt(GlobalPosition.Z), seed);
		int h1 = Generateur_Voxel.ObtenirHauteurTerrainMonde(Mathf.FloorToInt(avant.X), Mathf.FloorToInt(avant.Z), seed);
		int delta = h1 - h0;
		return delta >= 2 && delta <= 4;
	}

	private bool RayObstaclePnj(World3D w, Vector3 origine, Vector3 dir, float dist)
	{
		var q = PhysicsRayQueryParameters3D.Create(origine, origine + dir * dist);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		q.CollisionMask = 1;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() }; // s'exclut lui-même
		var hit = w.DirectSpaceState.IntersectRay(q);
		return hit != null && hit.Count > 0 && hit.ContainsKey("position");
	}
}
