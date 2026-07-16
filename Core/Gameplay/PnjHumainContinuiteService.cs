using Godot;

using System;
using System.Collections.Generic;



/// <summary>

/// Continuité asynchrone des PNJ humains : dématérialisation hors zone de chunks chargés,

/// simulation virtuelle (déplacement, faim, pente, stamina) et re-matérialisation au rattrapage.

/// Ticks logiques espacés (pas chaque frame) pour préserver les FPS client.

/// </summary>

public static class PnjHumainContinuiteService

{

	private const float IntervalleTickLogiqueSec = 0.45f;

	private const int MargeChunksAvantDemat = 1; // démat à (rayon - 1) chunk de la zone active

	private const float IntervalleSauvegardeVirtuelsSec = 12f;
	private const float SeuilArriveeMigrationVirtuelle = 6f;



	private static readonly List<PnjHumainEtatVirtuel> _virtuels = new();

	private static readonly HashSet<ulong> _dematEnCours = new();

	private static float _accumulateurTick;

	private static float _accumulateurSauvegarde;

	private static float _tempsRematUrgenteRestant;
	private static Vector3 _centreRematUrgente;
	private static string _nomPrioritaireRemat;



	public static IReadOnlyList<PnjHumainEtatVirtuel> Virtuels => _virtuels;



	public static void Vider()

	{

		_virtuels.Clear();

		_dematEnCours.Clear();

	}



	public static void AjouterVirtuel(PnjHumainEtatVirtuel etat)

	{

		if (etat == null)

			return;

		_virtuels.Add(etat);

	}



	/// <summary>Vérifie chaque frame si un PNJ physique doit basculer en virtuel.</summary>

	public static void TickDematerialisationRapide(Gestionnaire_Monde gm)

	{

		if (gm == null || !GodotObject.IsInstanceValid(gm))

			return;

		VerifierDematerialisationPhysique(gm);

	}



	/// <summary>Tente chaque frame de re-matérialiser les PNJ virtuels dans la zone du joueur.</summary>

	public static void TickRematerialisationRapide(Gestionnaire_Monde gm)

	{

		if (gm == null || !GodotObject.IsInstanceValid(gm))

			return;

		TenterRematerialisation(gm);

	}



	/// <summary>Après /TP : force le streaming et retente la re-matérialisation plusieurs secondes.</summary>

	public static void DeclencherRematerialisationUrgente(Gestionnaire_Monde gm, Vector3 centre, string nomPrioritaire = null)

	{

		if (gm == null)

			return;

		_tempsRematUrgenteRestant = 6f;

		_centreRematUrgente = centre;

		_nomPrioritaireRemat = nomPrioritaire ?? "";

		gm.ForcerPreparationZoneAutour(centre, 4);

		TenterRematerialisation(gm);

	}



	public static void TickRematerialisationUrgente(Gestionnaire_Monde gm, float delta)

	{

		if (_tempsRematUrgenteRestant <= 0f || gm == null || !GodotObject.IsInstanceValid(gm))

			return;

		_tempsRematUrgenteRestant -= delta;

		gm.ForcerPreparationZoneAutour(_centreRematUrgente, 4);

		if (!string.IsNullOrWhiteSpace(_nomPrioritaireRemat))

			TenterRematerialiserVirtuelNom(gm, _nomPrioritaireRemat);

		TenterRematerialisation(gm);

		if (_virtuels.Count == 0)

			_tempsRematUrgenteRestant = 0f;

	}



	/// <summary>Tick principal : sim virtuelle (démat/remat gérés par les ticks rapides).</summary>

	public static void Tick(Gestionnaire_Monde gm, float delta)

	{

		if (gm == null || !GodotObject.IsInstanceValid(gm))

			return;



		_accumulateurTick += delta;

		if (_accumulateurTick >= IntervalleTickLogiqueSec)

		{

			float dt = _accumulateurTick;

			_accumulateurTick = 0f;

			SimulerVirtuels(gm, dt);

		}



		_accumulateurSauvegarde += delta;

		if (_accumulateurSauvegarde >= IntervalleSauvegardeVirtuelsSec && _virtuels.Count > 0)

		{

			_accumulateurSauvegarde = 0f;

			PnjHumainPersistance.Sauvegarder(gm, _virtuels);

		}

	}



	/// <summary>True si le PNJ doit basculer en virtuel (hors zone de suivi ou chute dans le vide).</summary>

	public static bool DoitDematerialiser(Gestionnaire_Monde gm, PnjHumain pnj, Vector3 directionHoriz = default)

	{

		if (gm == null || pnj == null || !GodotObject.IsInstanceValid(pnj))

			return false;



		// Hors zone de suivi : le PNJ continue sa route en data pure.

		if (!EstDansZoneDemat(gm, pnj.GlobalPosition))

			return true;



		// Filet anti-chute dans le vide (chunk déchargé) : sauvegarde l'état avant perte totale.

		if (!pnj.IsOnFloor() && pnj.Velocity.Y < -6f)

		{

			int seed = gm.SeedTerrain;

			float sol = PnjHumainBiomeInstinct.HauteurSolMonde(pnj.GlobalPosition.X, pnj.GlobalPosition.Z, seed);

			if (pnj.GlobalPosition.Y < sol - 5f)

				return true;

		}



		return false;

	}



	/// <summary>Zone où un PNJ physique est maintenu (rayon dormance - marge).</summary>

	public static bool EstDansZoneDemat(Gestionnaire_Monde gm, Vector3 pos)

	{

		if (gm == null || !gm.JoueurReferenceValide())

			return true;

		return EstDansZoneChunks(gm, pos, ObtenirRayonDemat(gm));

	}



	/// <summary>Zone où un PNJ virtuel peut réapparaître (rayon dormance plein, légèrement plus large que la démat).</summary>

	public static bool EstDansZoneRemat(Gestionnaire_Monde gm, Vector3 pos)

	{

		if (gm == null || !gm.JoueurReferenceValide())

			return false;

		return EstDansZoneChunks(gm, pos, ObtenirRayonRemat(gm));

	}



	private static bool EstDansZoneChunks(Gestionnaire_Monde gm, Vector3 pos, int rayon)

	{

		Vector3 joueur = gm.ObtenirPositionJoueurOuSpawn();

		int taille = gm.TailleChunk;

		Vector2I chunkJ = Gestionnaire_Monde.WorldToChunkCoord(joueur, taille);

		Vector2I chunkP = Gestionnaire_Monde.WorldToChunkCoord(pos, taille);

		int dx = Mathf.Abs(chunkP.X - chunkJ.X);

		int dz = Mathf.Abs(chunkP.Y - chunkJ.Y);

		return dx <= rayon && dz <= rayon;

	}



	private static int ObtenirRayonDemat(Gestionnaire_Monde gm)

	{

		int rayon = ObtenirRayonDormance(gm);

		return Mathf.Max(2, rayon - MargeChunksAvantDemat);

	}



	private static int ObtenirRayonRemat(Gestionnaire_Monde gm)

	{

		return ObtenirRayonDormance(gm);

	}



	private static int ObtenirRayonDormance(Gestionnaire_Monde gm)

	{

		int rayon = 5;

		if (gm.MondeClientReference != null && GodotObject.IsInstanceValid(gm.MondeClientReference))

			rayon = Mathf.Max(2, gm.MondeClientReference.RayonDormancePhysique);

		return rayon;

	}



	private static void VerifierDematerialisationPhysique(Gestionnaire_Monde gm)

	{

		var aDemat = new List<PnjHumain>();

		foreach (PnjHumain p in PnjHumain.Tous)

		{

			if (p == null || !GodotObject.IsInstanceValid(p))

				continue;

			Vector3 dir = new Vector3(p.Velocity.X, 0f, p.Velocity.Z);

			if (dir.LengthSquared() < 0.01f && p.EnMigrationVersBiome)

			{

				Vector2 cible = p.CibleMigrationAbsolueXZ;

				dir = new Vector3(cible.X - p.GlobalPosition.X, 0f, cible.Y - p.GlobalPosition.Z);

			}

			if (!DoitDematerialiser(gm, p, dir))

				continue;

			aDemat.Add(p);

		}

		foreach (PnjHumain p in aDemat)

			Dematerialiser(p, gm);

	}



	/// <summary>Verrouille l'état, sauvegarde, bascule en pure data et détruit le corps physique.</summary>

	public static void Dematerialiser(PnjHumain pnj, Gestionnaire_Monde gm)

	{

		if (pnj == null || !GodotObject.IsInstanceValid(pnj))

			return;



		ulong id = pnj.GetInstanceId();

		if (_dematEnCours.Contains(id))

			return;

		_dematEnCours.Add(id);



		PnjHumainEtatVirtuel etat = pnj.ExporterEtatVirtuel();

		if (etat == null)

		{

			_dematEnCours.Remove(id);

			return;

		}

		if (!etat.ACibleMigration && !etat.EnPauseCamp)

		{

			int seed = gm?.SeedTerrain ?? 19847;

			int ix = Mathf.FloorToInt(etat.PosX);

			int iz = Mathf.FloorToInt(etat.PosZ);

			bool hostile = PnjHumainBiomeInstinct.EstZoneHostileRapide(ix, iz, seed);

			bool favorable = PnjHumainBiomeInstinct.EstBiomeFavorablePourCampement(ix, iz, seed);

			if (hostile || etat.Faim < 35f)

			{

				if (PnjHumainBiomeInstinct.EssayerTrouverBiomeFavorable(seed, new Vector2(etat.PosX, etat.PosZ), out Vector2 cible))

					etat.DefinirCibleMigration(cible);

				else if (pnj.EnMigrationVersBiome)

				{

					Vector2 xz = pnj.CibleMigrationAbsolueXZ;

					if (xz.LengthSquared() > 1f)

						etat.DefinirCibleMigration(xz);

				}

			}

			else if (favorable)

				etat.DefinirCamp(new Vector2(etat.PosX, etat.PosZ));

		}

		_virtuels.Add(etat);

		pnj.QueueFree();

		_dematEnCours.Remove(id);

		PnjHumainPersistance.Sauvegarder(gm, _virtuels);

		GD.Print($"ZERO-K PNJ[{etat.Nom}] dématérialisé -> virtuel ({etat.PosX:0},{etat.PosZ:0}) cible ({etat.CibleMigrX:0},{etat.CibleMigrZ:0})");

	}



	private static void SimulerVirtuels(Gestionnaire_Monde gm, float dt)
	{
		if (_virtuels.Count == 0)
			return;
		SimulerEtatsVirtuels(_virtuels, gm.SeedTerrain, dt, retirerSiFamine: true);
	}

	/// <summary>Recap hors-ligne : avance migration/camp/faim pendant l'absence du joueur (max 24 h simulées).</summary>
	public static void SimulerRecapOffline(IReadOnlyList<PnjHumainEtatVirtuel> etats, float secondesEcoulees, int seedTerrain)
	{
		if (etats == null || etats.Count == 0 || secondesEcoulees <= 0f)
			return;
		float plafond = Mathf.Min(secondesEcoulees, 86400f);
		float restant = plafond;
		var liste = etats as List<PnjHumainEtatVirtuel> ?? new List<PnjHumainEtatVirtuel>(etats);
		while (restant > 0f)
		{
			float dt = Mathf.Min(IntervalleTickLogiqueSec, restant);
			SimulerEtatsVirtuels(liste, seedTerrain, dt, retirerSiFamine: false);
			restant -= dt;
		}
	}

	private static void SimulerEtatsVirtuels(List<PnjHumainEtatVirtuel> etats, int seed, float dt, bool retirerSiFamine)
	{
		if (etats == null || etats.Count == 0)
			return;
		float vitesseBase = Joueur.Speed;
		for (int i = etats.Count - 1; i >= 0; i--)
		{
			PnjHumainEtatVirtuel v = etats[i];
			if (v == null)
			{
				if (retirerSiFamine)
					etats.RemoveAt(i);
				continue;
			}

			Vector3 pos = v.Position;
			int ix = Mathf.FloorToInt(pos.X);
			int iz = Mathf.FloorToInt(pos.Z);
			bool biomeHostile = PnjHumainBiomeInstinct.EstZoneHostileRapide(ix, iz, seed);
			bool biomeFavorable = PnjHumainBiomeInstinct.EstBiomeFavorablePourCampement(ix, iz, seed);
			bool enDeplacement = false;
			if (v.EnPauseCamp)
			{
				if (biomeHostile || v.Faim < 35f)
				{
					v.LeverCamp();
					if (biomeHostile && PnjHumainBiomeInstinct.EssayerTrouverBiomeFavorable(seed, new Vector2(pos.X, pos.Z), out Vector2 fuite))
						v.DefinirCibleMigration(fuite);
				}
			}
			else if (v.ACibleMigration)
			{
				Vector2 cible = v.CibleMigration;
				Vector2 vers = cible - new Vector2(pos.X, pos.Z);
				float distCible = vers.Length();
				if (distCible > SeuilArriveeMigrationVirtuelle)
				{
					enDeplacement = true;
					Vector2 dir = vers / distCible;
					int pente = PnjHumainBiomeInstinct.CalculerPenteTerrain(ix, iz, seed, out _);
					float facteur = PnjHumainBiomeInstinct.FacteurVitesseSelonPente(pente);
					v.Stamina = Mathf.Max(0f, v.Stamina - PnjHumainBiomeInstinct.DrainStaminaPenteParSeconde(pente) * dt);
					if (v.Stamina <= 0.01f)
						facteur *= 0.35f;
					float avance = vitesseBase * facteur * dt;
					if (avance > distCible)
						avance = distCible;
					pos.X += dir.X * avance;
					pos.Z += dir.Y * avance;
					pos.Y = PnjHumainBiomeInstinct.HauteurSolMonde(pos.X, pos.Z, seed);
					v.DefinirPosition(pos);
				}
				else if (distCible <= SeuilArriveeMigrationVirtuelle)
				{
					if (biomeHostile
						|| PnjHumainBiomeInstinct.CalculerPenteTerrain(ix, iz, seed, out _) > 42)
					{
						if (PnjHumainBiomeInstinct.EssayerTrouverBiomeFavorable(seed, new Vector2(pos.X, pos.Z), out Vector2 nouvelleCible))
							v.DefinirCibleMigration(nouvelleCible);
					}
					else if (biomeFavorable
						&& PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(ix, iz, seed) >= 2.2f)
						v.DefinirCamp(new Vector2(pos.X, pos.Z));
				}
			}
			else if (biomeHostile || v.Faim < 35f)
			{
				if (PnjHumainBiomeInstinct.EssayerTrouverBiomeFavorable(seed, new Vector2(pos.X, pos.Z), out Vector2 nouvelle))
					v.DefinirCibleMigration(nouvelle);
			}
			else if (biomeFavorable
				&& PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(ix, iz, seed) >= 2.2f)
				v.DefinirCamp(new Vector2(pos.X, pos.Z));

			float drainFaim = PnjHumain.CalculerDrainFaimVirtuel(enDeplacement);
			v.Faim = Mathf.Max(0f, v.Faim - drainFaim * dt);
			v.Stamina = Mathf.Min(100f, v.Stamina + 0.8f * dt);

			if (v.Faim <= 0f)
			{
				if (retirerSiFamine)
				{
					GD.Print($"ZERO-K PNJ[{v.Nom}] mort par famine (simulation virtuelle).");
					etats.RemoveAt(i);
				}
				else
					v.Faim = 8f;
			}
		}
	}

	private static bool TenterRematerialiserVirtuelNom(Gestionnaire_Monde gm, string nom)

	{

		for (int i = _virtuels.Count - 1; i >= 0; i--)

		{

			PnjHumainEtatVirtuel v = _virtuels[i];

			if (v == null || string.IsNullOrEmpty(v.Nom) || !v.Nom.Contains(nom, StringComparison.OrdinalIgnoreCase))

				continue;

			if (!EstDansZoneRemat(gm, v.Position))

				continue;

			if (!PeutRematerialiserMaintenant(gm, v.Position))

				continue;

			if (!PnjHumainSpawner.RematerialiserDepuisVirtuel(gm, v, gm.SeedTerrain))

				continue;

			_virtuels.RemoveAt(i);

			PnjHumainPersistance.Sauvegarder(gm, _virtuels);

			GD.Print($"ZERO-K PNJ[{v.Nom}] re-matérialisé (urgent) à ({v.PosX:0.1},{v.PosY:0.1},{v.PosZ:0.1})");

			return true;

		}

		return false;

	}



	private static bool PeutRematerialiserMaintenant(Gestionnaire_Monde gm, Vector3 pos)

	{

		if (gm.EstCollisionChunkEtVoisinsPretsPourPoint(pos))

			return true;

		// /TP HOMINA : on force la réapparition même si la collision n'est pas encore prête (snap sol ensuite).

		return _tempsRematUrgenteRestant > 0f && EstDansZoneRemat(gm, pos);

	}



	private static void TenterRematerialisation(Gestionnaire_Monde gm)

	{

		if (_virtuels.Count == 0 || gm == null)

			return;

		if (!GodotObject.IsInstanceValid(gm))

			return;



		for (int i = _virtuels.Count - 1; i >= 0; i--)

		{

			PnjHumainEtatVirtuel v = _virtuels[i];

			if (v == null)

			{

				_virtuels.RemoveAt(i);

				continue;

			}

			if (!EstDansZoneRemat(gm, v.Position))

				continue;

			if (!PeutRematerialiserMaintenant(gm, v.Position))

				continue;

			if (!PnjHumainSpawner.RematerialiserDepuisVirtuel(gm, v, gm.SeedTerrain))

				continue;

			_virtuels.RemoveAt(i);

			PnjHumainPersistance.Sauvegarder(gm, _virtuels);

			GD.Print($"ZERO-K PNJ[{v.Nom}] re-matérialisé à ({v.PosX:0.1},{v.PosY:0.1},{v.PosZ:0.1}) faim={v.Faim:0}%");

		}

	}

}


