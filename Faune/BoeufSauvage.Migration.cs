using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	/// <summary>Vitesse de marche réelle (m/s) tenant compte du niveau et des gènes — sert de base à l'estime de migration.</summary>
	private float VitesseMarcheEffective() =>
		VitesseMarche * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));

	private bool MigrationHerbeActive => _enMigrationHerbe && _migrationDirection.LengthSquared() > 0.01f;

	/// <summary>Diffuse au troupeau « j'ai trouvé de l'herbe ici » : les congénères affamés viendront la rejoindre.</summary>
	private void SignalerHerbeTrouveeAuTroupeau(Vector3 pointHerbe)
	{
		if (!ActiverCohesionTroupeau)
			return;
		// Appel « frais » = la balise n'était pas déjà active : on meugle une fois à la découverte,
		// pas en continu pendant que la bête broute (le cooldown audio sert de garde-fou secondaire).
		bool appelFrais = _beaconHerbeRestant <= 0f;
		_beaconHerbePosition = pointHerbe;
		_beaconHerbeRestant = Mathf.Max(1f, DureeAppelHerbeTroupeau);
		if (appelFrais)
			JouerMeuglementAppel();
	}

	/// <summary>Cherche un congénère qui appelle vers de l'herbe (appel actif) dans la portée et renvoie son point d'herbe.</summary>
	private bool TrouverAppelHerbeTroupeau(out Vector3 pointHerbe)
	{
		pointHerbe = Vector3.Zero;
		if (!ActiverCohesionTroupeau)
			return false;
		IReadOnlyList<BoeufSauvage> pop = ObtenirPopulationLocale();
		if (pop == null)
			return false;
		float rayon = Mathf.Max(8f, RayonAppelHerbeTroupeau);
		float rayon2 = rayon * rayon;
		float meilleur = float.MaxValue;
		bool trouve = false;
		for (int i = 0; i < pop.Count; i++)
		{
			BoeufSauvage b = pop[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (b._beaconHerbeRestant <= 0f)
				continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b._beaconHerbePosition);
			if (d2 > rayon2 || d2 >= meilleur)
				continue;
			meilleur = d2;
			pointHerbe = b._beaconHerbePosition;
			trouve = true;
		}
		return trouve;
	}

	/// <summary>
	/// Si un congénère appelle vers de l'herbe, on le rejoint (avec un léger décalage pour ne pas s'empiler :
	/// le troupeau reste éparpillé, pas trop serré) au lieu de partir en exode solo.
	/// </summary>
	private bool EssayerRejoindreAppelHerbe()
	{
		if (!TrouverAppelHerbeTroupeau(out Vector3 beacon))
			return false;
		Vector3 d = beacon - GlobalPosition;
		d.Y = 0f;
		float seuilProche = RayonMangerHerbe * 1.5f;
		if (d.LengthSquared() <= seuilProche * seuilProche)
			return false; // déjà sur le pâturage : laisser le broutage normal opérer.

		Vector3 offset = new Vector3(_rng.RandfRange(-3.5f, 3.5f), 0f, _rng.RandfRange(-3.5f, 3.5f));
		Vector3 cible = beacon + offset;
		_cibleCourante = new Vector3(cible.X, GlobalPosition.Y, cible.Z);
		if (_enMigrationHerbe)
			TerminerMigrationHerbe(); // on abandonne l'exode solo pour rejoindre le pâturage trouvé par le troupeau.
		_etat = EtatBoeuf.Broutage;
		if (_tempsBroutage <= 0f)
			_tempsBroutage = DureeBroutage;
		return true;
	}

	/// <summary>
	/// Démarre (ou poursuit) un exode alimentaire : l'animal part en ligne droite vers un nouveau pâturage.
	/// Tout le troupeau part dans la même direction (on suit un allié déjà en migration), ce qui les fait
	/// migrer ensemble au lieu de se disperser chacun de son côté.
	/// </summary>
	private void DemarrerMigrationHerbe(bool forcerNouvelleEtape)
	{
		if (!ActiverMigrationHerbe)
		{
			// Migration désactivée : repli sur la recherche locale d'herbe / errance classique.
			ChoisirCibleVersHerbeOuErrance();
			return;
		}
		// Déjà en migration : ne pas réinitialiser l'étape (sinon la distance restante / l'estime se remettent à zéro
		// à chaque tick du cerveau). On se contente de réactualiser la cible visée.
		if (_enMigrationHerbe && !forcerNouvelleEtape)
		{
			ActualiserCibleMigration();
			return;
		}

		// OBSERVATION LARGE avant de foncer : balaie tout autour (toutes directions, jusqu'à ~64 m) pour viser
		// le pâturage le PLUS PROCHE — même 50 m derrière soi — au lieu de partir tout droit dans les montagnes.
		// On part alors en migration DIRIGÉE vers ce point précis (et non en cap aveugle) : la machinerie d'exode
		// gère le trajet et l'arrêt pour brouter en chemin, sans re-balayer à chaque tick (coût FPS maîtrisé).
		if (BalayageLargeHerbeMeilleurPoint(out Vector3 herbeLarge, RayonObservationHerbeAvantMigration))
		{
			SignalerHerbeTrouveeAuTroupeau(herbeLarge); // appelle aussi le troupeau vers ce pâturage.
			Vector3 versHerbe = herbeLarge - GlobalPosition;
			versHerbe.Y = 0f;
			float distHerbe = versHerbe.Length();
			_migrationDirection = distHerbe > 0.01f ? versHerbe.Normalized() : -GlobalTransform.Basis.Z;
			_migrationDirection.Y = 0f;
			if (_migrationDirection.LengthSquared() < 0.01f)
				_migrationDirection = Vector3.Forward;
			_migrationDirection = _migrationDirection.Normalized();
			_migrationResteM = Mathf.Max(RayonMangerHerbe, distHerbe);
			_migrationVitesse = Mathf.Max(0.4f, VitesseMarcheEffective());
			_migrationHorodatageUnixSec = Time.GetUnixTimeFromSystem();
			_enMigrationHerbe = true;
			_etat = EtatBoeuf.Errance;
			ActualiserCibleMigration();
			return;
		}

		Vector3 dir = ChoisirDirectionMigrationTroupeau();
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.01f)
		{
			dir = -GlobalTransform.Basis.Z;
			dir.Y = 0f;
			if (dir.LengthSquared() < 0.01f)
				dir = Vector3.Forward;
		}
		_migrationDirection = dir.Normalized();
		float dMin = Mathf.Min(DistanceMigrationHerbeMin, DistanceMigrationHerbeMax);
		float dMax = Mathf.Max(DistanceMigrationHerbeMin, DistanceMigrationHerbeMax);
		_migrationResteM = _rng.RandfRange(dMin, dMax);
		_migrationVitesse = Mathf.Max(0.4f, VitesseMarcheEffective());
		_migrationHorodatageUnixSec = Time.GetUnixTimeFromSystem();
		_enMigrationHerbe = true;
		_etat = EtatBoeuf.Errance;
		ActualiserCibleMigration();
	}

	private void TerminerMigrationHerbe()
	{
		_enMigrationHerbe = false;
		_migrationDirection = Vector3.Zero;
		_migrationResteM = 0f;
	}

	/// <summary>Vise un point devant soi le long de la direction de migration et fait suivre l'ancre du troupeau.</summary>
	private void ActualiserCibleMigration()
	{
		float avance = Mathf.Clamp(_migrationResteM, 6f, 24f);
		Vector3 cible = GlobalPosition + _migrationDirection * avance;
		_cibleCourante = new Vector3(cible.X, GlobalPosition.Y, cible.Z);
		// L'ancre du troupeau suit la migration : sinon la cohésion / le rappel d'ancrage tireraient l'animal en arrière.
		_ancreTroupeau = _ancreTroupeau.Lerp(GlobalPosition + _migrationDirection * 10f, 0.5f);
	}

	/// <summary>
	/// Choisit la direction d'exode : suit un allié déjà parti (migration de meute) sinon évalue plusieurs caps
	/// tout autour et retient le plus praticable (terrain le plus plat), pour éviter de partir droit dans les montagnes.
	/// </summary>
	private Vector3 ChoisirDirectionMigrationTroupeau()
	{
		IReadOnlyList<BoeufSauvage> pop = ObtenirPopulationLocale();
		if (pop != null)
		{
			float meilleur = float.MaxValue;
			Vector3 dirAllie = Vector3.Zero;
			float rayon = RayonRassemblement * 1.25f;
			float rayon2 = rayon * rayon;
			for (int i = 0; i < pop.Count; i++)
			{
				BoeufSauvage b = pop[i];
				if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
					continue;
				if (!b.MigrationHerbeActive)
					continue;
				float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
				if (d2 < meilleur && d2 <= rayon2)
				{
					meilleur = d2;
					dirAllie = b._migrationDirection;
				}
			}
			if (dirAllie.LengthSquared() > 0.01f)
				return dirAllie.Normalized();
		}

		Vector3 cap = -GlobalTransform.Basis.Z;
		cap.Y = 0f;
		if (cap.LengthSquared() < 0.01f)
			cap = Vector3.Forward;
		cap = cap.Normalized();

		// Échantillonne 12 caps répartis sur 360° et garde celui dont le terrain est le plus plat (pente moyenne la plus
		// faible). Un léger malus pénalise les demi-tours pour garder un cap cohérent quand plusieurs directions se valent.
		const int directions = 12;
		float baseAngle = Mathf.Atan2(cap.Z, cap.X);
		float meilleurScore = float.MaxValue;
		Vector3 meilleureDir = cap;
		bool trouve = false;
		for (int i = 0; i < directions; i++)
		{
			float angle = baseAngle + (Mathf.Tau * i) / directions;
			Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
			float score = ScoreDirectionMigration(dir, out bool praticable);
			if (!praticable)
				continue;
			float ecart = 1f - cap.Dot(dir); // 0 = même sens, 2 = demi-tour.
			score += ecart * 5f;
			if (score < meilleurScore)
			{
				meilleurScore = score;
				meilleureDir = dir;
				trouve = true;
			}
		}
		return trouve ? meilleureDir.Normalized() : cap;
	}

	/// <summary>
	/// Note la praticabilité d'un cap : sonde la pente du terrain à plusieurs distances. Renvoie la pente moyenne
	/// (plus bas = plus plat = mieux) ; <paramref name="praticable"/> est faux si une falaise/montagne ou de l'eau
	/// profonde barre le chemin (direction à rejeter).
	/// </summary>
	private float ScoreDirectionMigration(Vector3 dir, out bool praticable)
	{
		praticable = false;
		float somme = 0f;
		int echantillons = 0;
		Span<float> distances = stackalloc float[] { 12f, 26f, 40f };
		for (int i = 0; i < distances.Length; i++)
		{
			Vector3 p = GlobalPosition + dir * distances[i];
			int pente = CalculerPenteTerrain(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Z), out int h);
			if (h < 80 || h > 320)
				return float.MaxValue;   // hors-limites : eau profonde ou pic infranchissable.
			if (pente > 90)
				return float.MaxValue;   // falaise / flanc de montagne marqué : on évite.
			somme += pente;
			echantillons++;
		}
		if (echantillons == 0)
			return float.MaxValue;
		praticable = true;
		return somme / echantillons;
	}

	/// <summary>
	/// Balaie l'herbe sur des anneaux concentriques croissants (toutes directions) et renvoie le pâturage le PLUS PROCHE
	/// repéré sur les chunks chargés. Lecture seule (ne génère jamais de chunk) et borné (~60 requêtes) : n'est appelé
	/// qu'au moment de décider/relancer un exode, donc le coût FPS reste maîtrisé.
	/// </summary>
	private bool BalayageLargeHerbeMeilleurPoint(out Vector3 point, float rayonMax)
	{
		point = GlobalPosition;
		if (_gestionnaire == null)
			return false;
		rayonMax = Mathf.Clamp(rayonMax, RayonRechercheHerbeVisible, 160f);
		float depart = Mathf.Max(RayonMangerHerbe + 1f, RayonRechercheHerbeVisible * 0.75f);
		const int anneaux = 6;
		const int parAnneau = 10;
		float decalAngle = _rng.RandfRange(0f, Mathf.Tau); // brise l'alignement systématique des sondes.
		for (int a = 0; a < anneaux; a++)
		{
			float t = anneaux <= 1 ? 1f : (float)a / (anneaux - 1);
			float rayon = Mathf.Lerp(depart, rayonMax, t);
			for (int s = 0; s < parAnneau; s++)
			{
				float angle = decalAngle + (Mathf.Tau * s) / parAnneau;
				Vector3 cand = GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rayon;
				if (!PositionTerrainValide(cand))
					continue;
				if (!HerbeDisponibleAutour(cand, RayonMangerHerbe))
					continue;
				point = new Vector3(cand.X, GlobalPosition.Y, cand.Z);
				return true; // anneaux croissants → le 1er trouvé est (à peu près) le plus proche.
			}
		}
		return false;
	}

	/// <summary>Avance l'exode : décrémente la distance restante, repère l'herbe en chemin, enchaîne les étapes.</summary>
	private void MettreAJourMigrationHerbe(float dt)
	{
		if (!MigrationHerbeActive)
			return;

		_migrationResteM -= Mathf.Max(0f, _migrationVitesse) * Mathf.Max(0f, dt);
		_migrationHorodatageUnixSec = Time.GetUnixTimeFromSystem();

		// Détection d'herbe en chemin (throttlée) : une seule requête de gazon par ~0,5 s pour préserver les FPS.
		_cooldownVerifHerbeMigration -= dt;
		if (_cooldownVerifHerbeMigration <= 0f)
		{
			_cooldownVerifHerbeMigration = 0.5f;
			float rayonDetection = Mathf.Max(RayonMangerHerbe, RayonDetectionHerbeMigration);
			if (HerbeDisponibleAutour(GlobalPosition, rayonDetection))
			{
				SignalerHerbeTrouveeAuTroupeau(GlobalPosition); // appelle le troupeau vers ce pâturage.
				TerminerMigrationHerbe();
				ForcerEtatBroutageSiBesoin(FaimCritiquePrioritaire());
				return;
			}
			// Un congénère a trouvé de l'herbe et appelle : on le rejoint plutôt que continuer l'exode solo.
			if (EssayerRejoindreAppelHerbe())
				return;
		}

		if (_migrationResteM <= 0f)
		{
			// Étape terminée sans herbe : on repart pour une nouvelle étape (poursuite de l'exode).
			DemarrerMigrationHerbe(forcerNouvelleEtape: true);
			return;
		}

		_etat = EtatBoeuf.Errance;
		ActualiserCibleMigration();
	}

	/// <summary>Centre (barycentre) du troupeau proche, mis en cache et throttlé (~4 Hz).</summary>
	private bool CalculerCentreTroupeau(out Vector3 centre, out int nbVoisins, float rayon)
	{
		if (_cooldownCentreTroupeau > 0f && _nbVoisinsTroupeauCache >= 0)
		{
			centre = _centreTroupeauCache;
			nbVoisins = _nbVoisinsTroupeauCache;
			return nbVoisins > 0;
		}
		_cooldownCentreTroupeau = 0.25f;

		centre = GlobalPosition;
		nbVoisins = 0;
		IReadOnlyList<BoeufSauvage> pop = ObtenirPopulationLocale();
		if (pop == null)
		{
			_centreTroupeauCache = centre;
			_nbVoisinsTroupeauCache = 0;
			return false;
		}
		float rayon2 = Mathf.Max(1f, rayon) * Mathf.Max(1f, rayon);
		Vector3 somme = GlobalPosition;
		int compte = 1;
		for (int i = 0; i < pop.Count; i++)
		{
			BoeufSauvage b = pop[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (GlobalPosition.DistanceSquaredTo(b.GlobalPosition) > rayon2)
				continue;
			somme += b.GlobalPosition;
			compte++;
			nbVoisins++;
		}
		centre = somme / compte;
		_centreTroupeauCache = centre;
		_nbVoisinsTroupeauCache = nbVoisins;
		return nbVoisins > 0;
	}

	/// <summary>Regroupe l'animal vers le centre du troupeau s'il s'en est trop éloigné (anti-dispersion).</summary>
	private bool AppliquerCohesionTroupeau()
	{
		if (!ActiverCohesionTroupeau)
			return false;
		float rayonCohesion = Mathf.Max(4f, RayonCohesionTroupeau);
		if (!CalculerCentreTroupeau(out Vector3 centre, out int nbVoisins, RayonRassemblement) || nbVoisins <= 0)
			return false;

		// L'ancre dérive doucement vers le centre du troupeau : le groupe peut brouter-marcher ensemble.
		_ancreTroupeau = _ancreTroupeau.Lerp(new Vector3(centre.X, _ancreTroupeau.Y, centre.Z), 0.05f);

		Vector3 versCentre = centre - GlobalPosition;
		versCentre.Y = 0f;
		float dist = versCentre.Length();
		if (dist <= rayonCohesion)
			return false;

		_etat = EtatBoeuf.Soutien;
		Vector3 cible = centre - versCentre.Normalized() * (rayonCohesion * 0.5f);
		_cibleCourante = new Vector3(cible.X, GlobalPosition.Y, cible.Z);
		return true;
	}

	/// <summary>Vrai si une menace (joueur perçu, ou allié en fuite/charge à proximité) pèse sur le troupeau.</summary>
	private bool EvaluerMenaceTroupeau(out Vector3 positionMenace)
	{
		positionMenace = Vector3.Zero;
		if (!ActiverFormationProtectrice)
			return false;

		// Le joueur en créatif n'existe pas pour la faune : jamais une menace.
		Vector3 posJoueur = Vector3.Zero;
		bool joueurConnu = !JoueurEnModeCreatif() && EssayerObtenirPositionJoueur(out posJoueur);
		if (joueurConnu && _memoireDetectionJoueur > 0f
			&& GlobalPosition.DistanceTo(posJoueur) <= Mathf.Max(6f, RayonMenaceTroupeau))
		{
			positionMenace = posJoueur;
			return true;
		}

		IReadOnlyList<BoeufSauvage> pop = ObtenirPopulationLocale();
		if (pop == null)
			return false;
		float rayon = Mathf.Max(6f, RayonMenaceTroupeau);
		float rayon2 = rayon * rayon;
		for (int i = 0; i < pop.Count; i++)
		{
			BoeufSauvage b = pop[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (b._etat != EtatBoeuf.Fuite && b._etat != EtatBoeuf.Charge)
				continue;
			if (GlobalPosition.DistanceSquaredTo(b.GlobalPosition) > rayon2)
				continue;
			// Un allié réagit à une menace : on s'aligne sur le joueur s'il est connu, sinon sur la position de l'allié.
			positionMenace = joueurConnu ? posJoueur : b.GlobalPosition;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Formation protectrice : le mâle s'interpose entre la menace et le centre du troupeau ;
	/// les femelles et les veaux se replient vers le centre, côté opposé à la menace.
	/// </summary>
	private bool AppliquerFormationProtectrice(Vector3 positionMenace)
	{
		if (!CalculerCentreTroupeau(out Vector3 centre, out int nbVoisins, RayonRassemblement) || nbVoisins <= 0)
			return false;

		Vector3 versMenace = positionMenace - centre;
		versMenace.Y = 0f;
		if (versMenace.LengthSquared() < 0.01f)
			return false;
		versMenace = versMenace.Normalized();

		Vector3 cible;
		if (EstTaureau)
		{
			// Mâle : se place entre le troupeau et la menace (bouclier).
			cible = centre + versMenace * Mathf.Max(3.5f, RayonCohesionTroupeau * 0.35f);
		}
		else
		{
			// Femelle / veau : se replie au centre, côté opposé à la menace.
			cible = centre - versMenace * Mathf.Max(2f, RayonCohesionTroupeau * 0.2f);
		}

		_etat = EtatBoeuf.Soutien;
		_cibleCourante = new Vector3(cible.X, GlobalPosition.Y, cible.Z);
		return true;
	}
}
