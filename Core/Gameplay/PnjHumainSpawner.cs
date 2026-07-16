using Godot;

/// <summary>
/// Instanciation / re-matérialisation des PNJ humains (spawn créatif et rattrapage hors-chunk).
/// </summary>
public static class PnjHumainSpawner
{
	/// <summary>Re-matérialise un PNJ virtuel dans la scène monde aux coordonnées exactes du rattrapage.</summary>
	public static bool RematerialiserDepuisVirtuel(Node parentMonde, PnjHumainEtatVirtuel v, int seed)
	{
		if (parentMonde == null || !GodotObject.IsInstanceValid(parentMonde) || v == null)
			return false;

		var pnj = new PnjHumain();
		pnj.Configurer(v.Sexe);
		parentMonde.AddChild(pnj);

		Joueur joueur = parentMonde.GetNodeOrNull<Joueur>("Joueur");
		if (joueur != null && GodotObject.IsInstanceValid(joueur))
		{
			pnj.CollisionLayer = joueur.CollisionLayer;
			pnj.CollisionMask = joueur.CollisionMask;
		}

		pnj.RestaurerDepuisVirtuel(v, seed);
		EcarterSiEmpileAvecAutrePnj(pnj);
		Callable.From(() => pnj.PositionnerSurSolApresRemat()).CallDeferred();
		return true;
	}

	/// <summary>Évite plusieurs PNJ exactement au même point après re-mat.</summary>
	private static void EcarterSiEmpileAvecAutrePnj(PnjHumain pnj)
	{
		if (pnj == null || !GodotObject.IsInstanceValid(pnj))
			return;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int tentative = 0; tentative < 8; tentative++)
		{
			bool collision = false;
			foreach (PnjHumain autre in PnjHumain.Tous)
			{
				if (autre == null || autre == pnj || !GodotObject.IsInstanceValid(autre))
					continue;
				if (pnj.GlobalPosition.DistanceTo(autre.GlobalPosition) < 1.4f)
				{
					collision = true;
					break;
				}
			}
			if (!collision)
				return;
			float a = rng.RandfRange(0f, Mathf.Tau);
			float d = rng.RandfRange(2.5f, 5f);
			Vector3 p = pnj.GlobalPosition + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
			pnj.GlobalPosition = new Vector3(p.X, pnj.GlobalPosition.Y, p.Z);
		}
	}

	/// <summary>Spawn debug autour d'une position (commande /INVOCA HOMINA).</summary>
	public static int FaireApparaitreAutourDe(Joueur joueur, int nombre)
	{
		nombre = Mathf.Clamp(nombre, 1, 24);
		if (joueur == null || !GodotObject.IsInstanceValid(joueur))
			return 0;
		Node parent = joueur.GetParent();
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return 0;
		PhysicsDirectSpaceState3D espace = joueur.GetWorld3D()?.DirectSpaceState;
		if (espace == null)
			return 0;

		Vector3 centre = joueur.GlobalPosition;
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		int spawnes = 0;
		int tentatives = 0;
		int budget = Mathf.Max(nombre * 10, nombre + 8);
		PnjHumain pivotSociete = null;
		while (spawnes < nombre && tentatives < budget)
		{
			tentatives++;
			float angle = rng.RandfRange(0f, Mathf.Tau);
			float dist = rng.RandfRange(2.5f, 7f);
			Vector3 approx = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
			if (!EssayerTrouverSol(espace, joueur, new Vector3(approx.X, centre.Y + 6f, approx.Z), out Vector3 sol)
				&& !EssayerTrouverSol(espace, joueur, new Vector3(approx.X, centre.Y + 30f, approx.Z), out sol))
				continue;

			var pnj = new PnjHumain();
			pnj.Configurer(rng.Randf() < 0.5f ? SexeJoueur.Masculin : SexeJoueur.Feminin);
			parent.AddChild(pnj);
			pnj.CollisionLayer = joueur.CollisionLayer;
			pnj.CollisionMask = joueur.CollisionMask;
			pnj.GlobalPosition = new Vector3(sol.X, pnj.CalculerYOriginePourPiedsSurSurface(sol.Y), sol.Z);
			if (pivotSociete == null)
				pivotSociete = pnj;
			else
				SocietePnj.Rencontrer(pivotSociete, pnj);
			spawnes++;
		}

		if (spawnes > 0 && pivotSociete?.Societe != null)
		{
			SocietePnj soc = pivotSociete.Societe;
			Gestionnaire_Monde gm = parent.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
			int seed = gm?.SeedTerrain ?? 19847;
			Vector2 centroide = soc.ObtenirCentroideMembres();
			int cx = Mathf.FloorToInt(centroide.X);
			int cz = Mathf.FloorToInt(centroide.Y);
			if (!PnjHumainBiomeInstinct.EstBiomeFavorablePourCampement(cx, cz, seed))
				soc.CalculerEtPublierCibleCampement(seed, centroide, pivotSociete);
			GD.Print($"ZERO-K PNJ société [{soc.Nom}] : {spawnes} membres regroupés après invocation.");
		}

		if (spawnes > 0)
			GD.Print($"ZERO-K : {spawnes} PNJ humain(s) invoqué(s) autour du joueur.");
		return spawnes;
	}

	private static bool EssayerTrouverSol(PhysicsDirectSpaceState3D espace, Joueur joueur, Vector3 depuis, out Vector3 sol)
	{
		sol = Vector3.Zero;
		if (espace == null)
			return false;
		var q = PhysicsRayQueryParameters3D.Create(depuis, depuis + Vector3.Down * 80f);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (joueur.GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { joueur.GetRid() };
		var hit = espace.IntersectRay(q);
		if (hit == null || hit.Count == 0 || !hit.ContainsKey("position"))
			return false;
		sol = (Vector3)hit["position"];
		return true;
	}
}
