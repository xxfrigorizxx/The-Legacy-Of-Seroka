using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Comportement de base du PNJ humain : ERRANCE simple (idle <-> marche vers un point au sol).
/// Première brique d'IA, volontairement minimale et sans triche (vitesse = celle du joueur).
/// Les briques suivantes (récolte, carnet du savoir, regroupement/échange, hiérarchie, diplomatie) viendront par-dessus.
/// </summary>
public partial class PnjHumain : CharacterBody3D
{
	private const float RayonErrancePnj = 10f;
	private const float RayonRechercheBaie = 22f;
	private const float SeuilFaimForage = 0.75f;   // sous 75% : va chercher des baies
	private const float SatieteCible = 0.95f;      // mange jusqu'à ~95% puis vaque à autre chose (social)
	private const float FaimUrgente = 0.60f;     // stress : plus d'errance ni de pause
	private const float FaimCritique = 0.35f;      // survie : migration immédiate
	// Mêmes constantes d'analyse que le joueur (Joueur.cs) -> même taux de réussite à Intelligence égale.
	private const float ChanceAnalyseBasePnj = 0.50f;
	private const float ChanceAnalyseMinPnj = 0.05f;
	private const float ChanceAnalyseMaxPnj = 0.95f;
	private const float BonusAnalyseParPointIntelPnj = 0.0001f;
	private const int IntelNeutrePnj = 10;
	public static bool DiagnosticForagePnj = false; // logs verbeux — désactivé (coûteux en perf)

	private void DiagForage(string msg)
	{
		if (DiagnosticForagePnj)
			GD.Print($"ZERO-K PNJ[{NomPnj}] forage: {msg}");
	}
	private const float PorteeRencontre = 5f;   // face à face pour échanger le savoir
	private const float RayonRegroupement = 16f;  // distance pour aller se regrouper
	// Migration : après N recherches infructueuses, le PNJ comprend que sa zone est vide et part vers un autre biome.
	private const int SeuilEchecsAvantMigration = 3;
	private const float TempsBonBiomeAvantCampSec = 5f;
	private const float TempsEvaluationSiteCampSec = 7f;
	private const float SeuilQualiteCamp = 2.2f;
	private const float RayonErranceCamp = 4f;
	/// <summary>Rayon d'expédition depuis l'ancre du camp (cueillette baies / roches) — base = camp, mais ils peuvent aller loin.</summary>
	private const float RayonCueilletteMaxCamp = 52f;
	private const float RayonRechercheBaieCamp = 52f;
	/// <summary>Anneaux de cueillette : on vide d'abord le voisinage proche, puis on s'éloigne seulement si plus rien.</summary>
	private static readonly float[] RayonsAnneauCueilletteCamp = { 18f, 36f, RayonCueilletteMaxCamp };
	private const float MargePerimetreCueilletteCamp = 2f;
	private const float RayonCueilletteGarde = 14f;
	private const float RayonRecolteBaiePnj = 3.6f;

	/// <summary>Slot inventaire réservé pour un dépôt au stock (baie pas retirée avant d'arriver sur zone).</summary>
	private int _indexSlotBaiePourDepot = -1;

	private bool _biomeHostileCache;
	private float _biomeHostileCacheExpire;
	private int _biomeCacheCellX = int.MinValue, _biomeCacheCellZ = int.MinValue;

	private static readonly string[] NomsCouleursBaie =
		{ "rouge", "violette", "orange", "bleue", "jaune", "verte", "noire", "rose", "cyan" };

	/// <summary>Met à jour l'état (idle/marche/forage) et renvoie la direction horizontale désirée (normalisée) ou zéro.</summary>
	private Vector3 CalculerDeplacementComportement(float dt)
	{
		_cooldownEtatPnj -= dt;
		_cooldownRechercheBaie -= dt;
		_cooldownRencontre -= dt;

		// Rencontre : si un autre PNJ est juste en face, on échange le savoir + on forme/rejoint une société.
		TenterRencontreProche();

		// Chef en évaluation de camp : il ne fait que choisir l'emplacement (marqueur « Camp ? » au-dessus).
		if (_phaseCampChef == PhaseCampChef.Evaluation)
			return CalculerDeplacementEvaluationCamp(dt);

		TickOrdresSociete(dt);
		if (ExecuterOrdreChefActif(dt, out Vector3 dirOrdre))
			return dirOrdre;

		// Priorité survie / campement : ne pas attendre la fin du cooldown idle en biome hostile.
		if (_phaseCampChef != PhaseCampChef.Evaluation && !ObéitOrdreChefActif()
			&& _etatPnj != EtatPnj.Forage && _etatPnj != EtatPnj.Migration
			&& (DoitPartirEnMigration() || DoitMigrerPourEtablirCamp()))
		{
			EntrerEnMigration();
		}

		if (_enPauseCamp && ExecuterComportementCampStructure(dt, out Vector3 dirCamp))
			return dirCamp;

		// Sans ordre (ou désobéissant) : libre — baies si faim, analyse, errance, social.
		// En migration on scrute TOUJOURS (pour repérer le vert dès qu'on y arrive), sinon seulement quand on a faim.
		bool aFaim = RatioFaim() < SeuilFaimForage;
		bool biomeHostile = BiomeLocalHostile();
		bool biomeFavorable = BiomeLocalFavorablePourCampement();
		bool cueillirPourReserve = _enPauseCamp && DoitCueillirPourReserveColonie() && RoleAutoriseCueilletteReserve() && !ObéitOrdreChefActif();
		bool sousOrdreCueillette = ObéitOrdreChefActif();
		bool prioriteCamp = PrioriteEtablirCampEnBiomeFavorable(biomeHostile, biomeFavorable);
		bool doitScruterBaie = !prioriteCamp && (aFaim || cueillirPourReserve || sousOrdreCueillette
			|| (_etatPnj == EtatPnj.Migration && RatioFaim() < SeuilFaimForage));
		if (_roleVillageois == RoleVillageoisPnj.Garde && RatioFaim() >= SeuilFaimForage && !sousOrdreCueillette)
			doitScruterBaie = aFaim;

		TickCampEtChef(dt, biomeHostile, biomeFavorable);

		// Instinct : biome hostile + faim -> migration (grille UNE seule fois tant que la cible est fixée).
		if (aFaim && biomeHostile && _etatPnj != EtatPnj.Forage && _etatPnj != EtatPnj.Migration && !_enPauseCamp)
			EntrerEnMigration();

		if (_etatPnj != EtatPnj.Forage && doitScruterBaie && _cooldownRechercheBaie <= 0f)
		{
			float delaiScan = _etatPnj == EtatPnj.Migration ? 0.8f
				: RatioFaim() < FaimCritique ? 0.9f
				: RatioFaim() < FaimUrgente ? 1.4f
				: 2.5f;
			_cooldownRechercheBaie = delaiScan;
			if (EssayerCiblerBuissonComestible())
			{
				if (prioriteCamp || (DoitMigrerPourEtablirCamp() && RatioFaim() >= SeuilFaimForage))
				{
					// Campement d'abord : ignorer les baies tant que le camp n'est pas posé.
				}
				else
					TerminerMigration();
			}
			else if (_etatPnj != EtatPnj.Migration)
			{
				_echecsRechercheBaie++;
				int seuilEchecs = RatioFaim() < FaimCritique ? 1
					: RatioFaim() < FaimUrgente ? 2
					: SeuilEchecsAvantMigration;
				if (_enPauseCamp && (cueillirPourReserve || sousOrdreCueillette || DoitTravaillerCommeCueilleur()))
					seuilEchecs = Mathf.Max(seuilEchecs, SeuilEchecsAvantMigration * 4);
				if (_echecsRechercheBaie >= seuilEchecs || (biomeHostile && !_enPauseCamp))
					EntrerEnMigration();
			}
		}

		switch (_etatPnj)
		{
			case EtatPnj.Migration:
			{
				if (RatioFaim() >= SatieteCible && !biomeHostile && biomeFavorable)
				{
					TerminerMigration();
					EntrerEnIdle();
					return Vector3.Zero;
				}
				if (!_aCibleMigrationAbsolue)
				{
					EntrerEnMigration();
					if (!_aCibleMigrationAbsolue)
						return Vector3.Zero;
				}
				Vector3 versCible = _cibleMigrationAbsolue - GlobalPosition;
				versCible.Y = 0f;
				float dist = versCible.Length();
				if (dist < 6f)
				{
					if (!biomeHostile)
					{
						TerminerMigration();
						TenterEtablirCampApresArrivee(biomeFavorable);
						EntrerEnIdleSansRecursion();
						return Vector3.Zero;
					}
					// Cible atteinte mais biome encore mauvais : recalcule le biome favorable le plus proche.
					RechoisirCibleMigration();
					versCible = _cibleMigrationAbsolue - GlobalPosition;
					versCible.Y = 0f;
					dist = versCible.Length();
					if (dist < 0.5f)
						return Vector3.Zero;
				}
				return AffinerDirectionMigrationColonie(versCible).Normalized();
			}
			case EtatPnj.Forage:
			{
				Vector3 versB = _posBuissonCible - GlobalPosition;
				versB.Y = 0f;
				if (versB.Length() < 1.5f)
				{
					TenterMangerBuissonCible();
					ApresForageOuEchec();
					return Vector3.Zero;
				}
				if (_cooldownEtatPnj <= 0f)
				{
					ApresForageOuEchec();
					return Vector3.Zero;
				}
				return versB.Normalized();
			}
			case EtatPnj.Marche:
			{
				Vector3 versM = _ciblePnj - GlobalPosition;
				versM.Y = 0f;
				if (versM.Length() < 0.6f || _cooldownEtatPnj <= 0f)
				{
					EntrerEnIdle();
					return Vector3.Zero;
				}
				return versM.Normalized();
			}
			default: // Idle
				if (_enPauseCamp && DoitPartirEnMigration())
				{
					LeverCampIci();
					EntrerEnMigration();
					return Vector3.Zero;
				}
				if (_cooldownEtatPnj <= 0f)
				{
					if (DoitPartirEnMigration())
						EntrerEnMigration();
					else if (aFaim)
						EntrerEnIdleSansRecursion(); // la recherche de baies tourne au-dessus
					else if (_enPauseCamp)
					{
						if (!ExecuterComportementCampStructure(dt, out _))
							EntrerEnMarcheCamp();
					}
					else if (prioriteCamp)
						EntrerEnRegroupementColonie();
					else if (!ColonieDoitResterUnieSansCamp() && !DoitMigrerPourEtablirCamp())
						EntrerEnMarche();
					else
						EntrerEnMigration();
				}
				return Vector3.Zero;
		}
	}

	// ----- Récolte / consommation / apprentissage des baies -----

	private Gestionnaire_Monde ObtenirGestionnaireMonde()
	{
		if (_gmCache != null && GodotObject.IsInstanceValid(_gmCache))
			return _gmCache;
		Node scene = GetTree()?.CurrentScene;
		_gmCache = scene?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
		return _gmCache;
	}

	/// <summary>Cherche le buisson le plus proche ; le cible sauf si sa couleur est apprise toxique. Renvoie true si une cible comestible a été trouvée.</summary>
	private bool EssayerCiblerBuissonComestible(bool ignorerPerimetreCamp = false, float? rayonForce = null)
	{
		if (PrioriteEtablirCampEnBiomeFavorable(BiomeLocalHostile(), BiomeLocalFavorablePourCampement()))
			return false;

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null)
		{
			DiagForage("Gestionnaire_Monde introuvable");
			return false;
		}

		if (_enPauseCamp && !ignorerPerimetreCamp && rayonForce == null)
			return EssayerCiblerBuissonCampParAnneaux(gm);

		float rayon = rayonForce ?? ObtenirRayonRechercheBaieActuel();
		Vector3 centreScan = _enPauseCamp && !ignorerPerimetreCamp
			? new Vector3(_ancreCamp.X, GlobalPosition.Y, _ancreCamp.Y)
			: GlobalPosition;
		if (!gm.EssayerDetecterBuissonPourPnj(centreScan, rayon, out Vector3 pos, out byte typeFlore, pleinSeulement: true))
		{
			DiagForage($"aucun buisson plein dans {rayon:0} m");
			return false;
		}
		return FinaliserCibleBuissonComestible(pos, typeFlore, centreScan);
	}

	/// <summary>Parcourt les anneaux proche→loin depuis l'ancre du camp : pas de mur invisible, extension naturelle quand le voisinage est vide.</summary>
	private bool EssayerCiblerBuissonCampParAnneaux(Gestionnaire_Monde gm)
	{
		Vector3 centreScan = new Vector3(_ancreCamp.X, GlobalPosition.Y, _ancreCamp.Y);
		float rayonMaxAutorise = ObtenirRayonCueilletteMaxEffectifCamp();
		for (int a = 0; a < RayonsAnneauCueilletteCamp.Length; a++)
		{
			float rayonAnneau = Mathf.Min(RayonsAnneauCueilletteCamp[a], rayonMaxAutorise);
			if (rayonAnneau < 4f)
				continue;
			if (!gm.EssayerDetecterBuissonPourPnj(centreScan, rayonAnneau, out Vector3 pos, out byte typeFlore, pleinSeulement: true))
			{
				DiagForage($"anneau {a + 1}/{RayonsAnneauCueilletteCamp.Length} ({rayonAnneau:0} m) : vide");
				continue;
			}
			float distCamp = new Vector2(pos.X, pos.Z).DistanceTo(_ancreCamp);
			if (distCamp > rayonAnneau + MargePerimetreCueilletteCamp)
				continue;
			if (!FinaliserCibleBuissonComestible(pos, typeFlore, centreScan))
				continue;
			_anneauCueilletteCamp = a;
			DiagForage($"anneau cueillette {a + 1} ({rayonAnneau:0} m), buisson à {distCamp:0.0} m du camp");
			return true;
		}
		DiagForage($"aucun buisson comestible dans {rayonMaxAutorise:0} m autour du camp");
		return false;
	}

	private bool FinaliserCibleBuissonComestible(Vector3 pos, byte typeFlore, Vector3 centreScan)
	{
		int couleur = Joueur.IndexCouleurBaieDepuisVariante(Chunk_Serveur.ObtenirVarianteBuisson(typeFlore));
		if (CouleurApprisToxique(couleur))
		{
			DiagForage($"buisson {NomCouleurBaie(couleur)} connu toxique -> évité");
			return false;
		}
		_posBuissonCible = pos;
		_couleurCibleBaie = couleur;
		_forageRoche = false;
		_etatPnj = EtatPnj.Forage;
		float distMarche = GlobalPosition.DistanceTo(pos);
		_cooldownEtatPnj = Mathf.Clamp(distMarche * 0.55f + 4f, 6f, 28f);
		DiagForage($"cible buisson {NomCouleurBaie(couleur)} à {distMarche:0.0} m (scan depuis {centreScan.DistanceTo(pos):0.0} m du centre)");
		return true;
	}

	private float ObtenirRayonCueilletteMaxEffectifCamp()
	{
		if (_roleVillageois == RoleVillageoisPnj.Garde && RatioFaim() >= FaimUrgente)
			return RayonCueilletteGarde;
		return RayonCueilletteMaxCamp;
	}

	private bool RoleAutoriseCueilletteReserve()
		=> _roleVillageois == RoleVillageoisPnj.Cueilleur
			|| _roleVillageois == RoleVillageoisPnj.Libre
			|| ObéitOrdreChefActif();

	private bool DoitTravaillerCommeCueilleur()
		=> _roleVillageois == RoleVillageoisPnj.Cueilleur
			|| (_roleVillageois == RoleVillageoisPnj.Libre && (DoitCueillirPourReserveColonie() || ObéitOrdreChefActif()));

	private void TenterMangerBuissonCible()
	{
		if (EssayerRecolterBaiesVersInventaire(_posBuissonCible))
			return;
		_cooldownRechercheBaie = 6f;
		DiagForage("récolte vide -> on cherche ailleurs");
	}

	/// <summary>Cueillette oracle serveur → inventaire PNJ (mains / slots craft).</summary>
	private bool EssayerRecolterBaiesVersInventaire(Vector3 pointBuisson)
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null)
			return false;

		Vector3 point = new Vector3(pointBuisson.X, GlobalPosition.Y, pointBuisson.Z);
		if (!gm.RecolterBaiesBuissonPourPnj(point, RayonRecolteBaiePnj, out int quantite, out byte couleurBrute)
			|| quantite <= 0)
		{
			if (!gm.RecolterBaiesBuissonPourPnj(GlobalPosition, RayonRecolteBaiePnj, out quantite, out couleurBrute)
				|| quantite <= 0)
				return false;
		}

		int couleur = Joueur.ClampIndexCouleurBaie(couleurBrute);
		if (!AjouterBaieInventaire(couleur, quantite))
		{
			DiagForage($"récolté {quantite} baie(s) mais inventaire plein");
			return false;
		}
		DiagForage($"récolté {quantite} {NomCouleurBaie(couleur)} -> inventaire (total {CompterBaiesInventairePourCamp()})");
		_anneauCueilletteCamp = 0;
		_echecsRechercheBaie = 0;
		return true;
	}

	/// <summary>
	/// Quand il a faim : mange d'abord une baie CONNUE COMESTIBLE du stock (la faim remonte).
	/// Sinon, il DÉCOUVRE une baie INCONNUE -> soit en l'ANALYSANT (prudent : détruit l'échantillon, peut rater,
	/// aucun risque), soit en la MANGEANT EN ESSAI (il subit l'effet MAIS l'apprend à coup sûr). Plus il est
	/// intelligent, plus il analyse ; affamé au point critique, il mange pour survivre. Il ne touche jamais une
	/// baie connue toxique. Anti-famine + anti-triche (aucune connaissance gratuite).
	/// </summary>
	private void MangerDepuisInventaireSiBesoin(float dt)
	{
		_cooldownMangerStock -= dt;
		if (_cooldownMangerStock > 0f || Inventaire == null)
			return;

		bool remplirReserve = DoitCueillirPourReserveColonie();
		float satieteCible = remplirReserve ? 0.62f : SatieteCible;
		if (RatioFaim() >= satieteCible)
			return;

		// 1) Manger une baie CONNUE COMESTIBLE (nutrition sûre).
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].ID != Joueur.IdObjetBaie || Inventaire[i].Quantite <= 0)
				continue;
			int couleur = Joueur.ClampIndexCouleurBaie(Inventaire[i].IndexChimique);
			if (!CouleurApprisComestible(couleur))
				continue;
			RetirerBaieInventaire(couleur);
			MangerBaie(couleur);
			_cooldownMangerStock = 0.8f;
			return;
		}

		// 2) Baie inconnue : analyse (prudent) ou essai en mangeant (affamé).
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].ID != Joueur.IdObjetBaie || Inventaire[i].Quantite <= 0)
				continue;
			int couleur = Joueur.ClampIndexCouleurBaie(Inventaire[i].IndexChimique);
			if (ConnaissanceBaie(couleur))
				continue;

			bool affameCritique = RatioFaim() < 0.25f;
			bool prudent = !affameCritique && _rngPnj.Randf() < ProbabilitePrudencePnj();
			if (prudent)
				AnalyserBaieDepuisInventaire(couleur);
			else
			{
				RetirerBaieInventaire(couleur);
				MangerBaie(couleur);
				DiagForage($"essai en mangeant {NomCouleurBaie(couleur)} -> apprend l'effet (faim {Mathf.RoundToInt(RatioFaim() * 100f)}%)");
			}
			_cooldownMangerStock = 0.8f;
			return;
		}
	}

	/// <summary>Identifie les baies inconnues (analyse ou essai) même hors période de faim — requis avant tri au stock.</summary>
	private void TenterIdentifierBaiesInconnues(float dt)
	{
		_cooldownIdentifierBaie -= dt;
		if (_cooldownIdentifierBaie > 0f || Inventaire == null)
			return;
		if (CompterBaiesInconnuesInventaire() <= 0)
			return;

		if (!EssayerTrouverPremiereBaieInconnue(out int couleur))
			return;

		bool affameCritique = RatioFaim() < 0.25f;
		bool affame = RatioFaim() < SeuilFaimForage;
		bool prudent = !affameCritique && _rngPnj.Randf() < ProbabilitePrudencePnj();

		if (prudent || !affame)
			AnalyserBaieDepuisInventaire(couleur);
		else
		{
			RetirerBaieInventaire(couleur);
			MangerBaie(couleur);
			DiagForage($"essai en mangeant {NomCouleurBaie(couleur)} (identification)");
		}
		_cooldownIdentifierBaie = 0.85f;
	}

	private bool EssayerTrouverPremiereBaieInconnue(out int couleur)
	{
		couleur = 0;
		if (Inventaire == null)
			return false;
		for (int i = 0; i < Inventaire.Length; i++)
		{
			if (Inventaire[i].ID != Joueur.IdObjetBaie || Inventaire[i].Quantite <= 0)
				continue;
			couleur = Joueur.ClampIndexCouleurBaie(Inventaire[i].IndexChimique);
			if (!ConnaissanceBaie(couleur))
				return true;
		}
		return false;
	}

	/// <summary>
	/// Probabilité que le PNJ soit PRUDENT face à une baie inconnue : il l'ANALYSE (sans risque) plutôt que de la
	/// MANGER en essai. Plus il est intelligent, plus il est prudent (~30% à Int 10, jusqu'à ~90% très intelligent).
	/// </summary>
	private float ProbabilitePrudencePnj()
	{
		float p = 0.30f + (Intelligence - IntelNeutrePnj) * 0.03f;
		return Mathf.Clamp(p, 0.20f, 0.90f);
	}

	/// <summary>Chance de réussite d'analyse, même formule que le joueur (base 50% + 0,01%/point d'Intelligence autour de 10).</summary>
	private float ChanceAnalysePnj()
	{
		float c = ChanceAnalyseBasePnj + (Intelligence - IntelNeutrePnj) * BonusAnalyseParPointIntelPnj;
		return Mathf.Clamp(c, ChanceAnalyseMinPnj, ChanceAnalyseMaxPnj);
	}

	/// <summary>
	/// Analyse d'une baie du STOCK : CONSOMME un échantillon de l'inventaire (exactement comme l'analyseur du joueur).
	/// Aucune connaissance gratuite : sans baie en main, rien n'est appris. Échec possible (basé sur l'Intelligence) -> échantillon perdu.
	/// </summary>
	private void AnalyserBaieDepuisInventaire(int couleur)
	{
		couleur = Joueur.ClampIndexCouleurBaie(couleur);
		if (ConnaissanceBaie(couleur))
			return; // déjà connue : pas besoin d'en gâcher une
		if (!RetirerBaieInventaire(couleur))
		{
			DiagForage($"analyse {NomCouleurBaie(couleur)} impossible : aucun échantillon en stock");
			return; // PAS d'échantillon -> on n'apprend rien (anti-triche)
		}
		bool succes = _rngPnj.Randf() < ChanceAnalysePnj();
		GagnerXpAnalyse(succes ? 1 : 2);
		if (!succes)
		{
			DiagForage($"analyse {NomCouleurBaie(couleur)} ratée -> échantillon perdu, rien appris");
			return;
		}
		(float faim, int degats, bool poison) = EffetBaiePnj(couleur);
		bool toxique = faim < 0f || degats > 0 || poison;
		string nom = NomCouleurBaie(couleur);
		string ligne = toxique
			? $"Baie {nom} : TOXIQUE (éviter)"
			: $"Baie {nom} : comestible (+{Mathf.RoundToInt(faim)} faim)";
		ApprendreConnaissanceBaie(ligne);
		DiagForage($"analyse {NomCouleurBaie(couleur)} réussie -> {(toxique ? "TOXIQUE" : "comestible")}");
	}

	private void GagnerXpAnalyse(int xp)
	{
		_xpAnalyse += xp;
		// Apprend « mieux » avec le temps : +1 Intelligence tous les 25 XP (les IA progressent).
		while (_xpAnalyse >= 25)
		{
			_xpAnalyse -= 25;
			_intelligence++;
		}
	}

	private bool AjouterBaieInventaire(int couleur, int quantite)
	{
		if (Inventaire == null || quantite <= 0)
			return false;
		couleur = Joueur.ClampIndexCouleurBaie(couleur);
		var slotRef = new SlotInventaire { ID = Joueur.IdObjetBaie, IndexChimique = couleur, Quantite = 1 };
		int pileMax = Joueur.ObtenirPileMax(slotRef);
		int restant = quantite;

		for (int i = 0; i < Inventaire.Length && restant > 0; i++)
		{
			if (Inventaire[i].ID == Joueur.IdObjetBaie && Inventaire[i].IndexChimique == couleur && Inventaire[i].Quantite < pileMax)
			{
				int ajout = Mathf.Min(pileMax - Inventaire[i].Quantite, restant);
				SlotInventaire s = Inventaire[i];
				s.Quantite += ajout;
				Inventaire[i] = s;
				restant -= ajout;
			}
		}

		// Nouvelles piles : mains d'abord (visible), puis slots craft.
		int[] ordreSlots = { IdxMainDroite, IdxMainGauche, 2, 3, 4, 5 };
		foreach (int i in ordreSlots)
		{
			if (restant <= 0 || i < 0 || i >= Inventaire.Length)
				continue;
			if (!Inventaire[i].EstVide)
				continue;
			int ajout = Mathf.Min(pileMax, restant);
			Inventaire[i] = new SlotInventaire { ID = Joueur.IdObjetBaie, IndexChimique = couleur, Quantite = ajout };
			restant -= ajout;
		}

		return restant < quantite;
	}

	internal void AnnulerReservationDepotBaie() => _indexSlotBaiePourDepot = -1;

	private bool RetirerBaieInventaire(int couleur)
	{
		if (Inventaire == null)
			return false;
		// On mange d'abord depuis les SLOTS DE CRAFT (fin du tableau), pour garder les MAINS remplies (visibles) plus longtemps.
		for (int i = Inventaire.Length - 1; i >= 0; i--)
		{
			if (Inventaire[i].ID == Joueur.IdObjetBaie && Inventaire[i].IndexChimique == couleur && Inventaire[i].Quantite > 0)
			{
				SlotInventaire s = Inventaire[i];
				s.Quantite--;
				Inventaire[i] = s.Quantite <= 0 ? default : s;
				return true;
			}
		}
		return false;
	}

	private bool ConnaissanceBaie(int couleur) => CarnetContient($"Baie {NomCouleurBaie(couleur)} :");
	private bool CouleurApprisComestible(int couleur) => CarnetContient($"Baie {NomCouleurBaie(couleur)} : comestible");

	// ----- Rencontre + transmission du savoir entre PNJ -----

	private PnjHumain TrouverPnjProche(float portee)
	{
		float meilleur = portee * portee;
		PnjHumain best = null;
		foreach (PnjHumain p in PnjHumain.Tous)
		{
			if (p == this || p == null || !GodotObject.IsInstanceValid(p))
				continue;
			float d = GlobalPosition.DistanceSquaredTo(p.GlobalPosition);
			if (d < meilleur) { meilleur = d; best = p; }
		}
		return best;
	}

	public void MarquerRencontre(float sec) => _cooldownRencontre = Mathf.Max(_cooldownRencontre, sec);

	private void TenterRencontreProche()
	{
		if (_cooldownRencontre > 0f)
			return;
		PnjHumain autre = TrouverPnjProche(PorteeRencontre);
		if (autre == null)
			return;
		_cooldownRencontre = 8f;
		autre.MarquerRencontre(8f); // évite un double échange immédiat

		// Collaboration : on forme/rejoint une société, puis on échange le savoir dans les deux sens.
		SocietePnj.Rencontrer(this, autre);
		int avant = NombreConnaissances;
		Transmettre(autre);
		autre.Transmettre(this);
		DiagForage($"rencontre {autre.NomPnj} -> société {(_societe != null ? _societe.Nom : "?")} ; carnet {avant}->{NombreConnaissances}");
	}

	/// <summary>Transmet son carnet au récepteur. Un rebelle FALSIFIE parfois l'info (désinformation dangereuse).</summary>
	private void Transmettre(PnjHumain recepteur)
	{
		if (recepteur == null)
			return;
		foreach (string c in new List<string>(Carnet))
		{
			bool mensonge = _estRebelle && _rngPnj.Randf() < 0.6f;
			string info = mensonge ? Falsifier(c) : c;
			recepteur.RecevoirConnaissance(info);
			EnregistrerActe(!mensonge); // partage vrai = acte bon ; mensonge = acte mauvais
		}
	}

	/// <summary>Inverse la sûreté d'une baie (sûre <-> toxique) : base de la désinformation.</summary>
	private static string Falsifier(string ligne)
	{
		if (string.IsNullOrEmpty(ligne))
			return ligne;
		int idx = ligne.IndexOf(" : ", StringComparison.Ordinal);
		if (idx <= 0)
			return ligne;
		string prefixe = ligne.Substring(0, idx);
		if (ligne.Contains(" : comestible"))
			return prefixe + " : TOXIQUE (éviter)";
		if (ligne.Contains(" : TOXIQUE"))
			return prefixe + " : comestible (+2 faim)";
		return ligne;
	}

	/// <summary>Mange une baie : applique l'effet aux vitaux ET apprend (note au carnet) si elle est sûre ou toxique.</summary>
	private void MangerBaie(int couleur)
	{
		couleur = Joueur.ClampIndexCouleurBaie(couleur);
		if (CouleurApprisToxique(couleur))
			return; // sécurité : ne se ré-empoisonne pas avec une couleur déjà connue dangereuse

		(float faimDelta, int degats, bool poison) = EffetBaiePnj(couleur);
		_faim = Mathf.Clamp(_faim + faimDelta, 0f, _faimMax);
		if (degats > 0)
			BlesserMembreAleatoire(degats);
		if (poison)
			BlesserMembreAleatoire(8); // pas de poison-sur-durée encore : dégât immédiat modéré
		if (couleur == 8)
			SoignerMembreLePlusFaible(5);

		bool toxique = faimDelta < 0f || degats > 0 || poison;
		string nom = NomCouleurBaie(couleur);
		if (!ConnaissanceBaie(couleur))
		{
			string ligne = toxique
				? $"Baie {nom} : TOXIQUE (éviter)"
				: $"Baie {nom} : comestible (+{Mathf.RoundToInt(faimDelta)} faim)";
			ApprendreConnaissanceBaie(ligne);
		}
	}

	/// <summary>Effet d'une baie sur un PNJ (réplique simplifiée de AppliquerEffetsBaieSelonCouleur du joueur).</summary>
	private static (float faim, int degats, bool poison) EffetBaiePnj(int couleur)
	{
		var effet = couleur switch
		{
			0 => (+2f, 5, false),   // rouge : nourrit un peu mais blesse
			1 => (-10f, 0, false),  // violette : fait perdre de la satiété
			2 => (-5f, 0, false),   // orange : fait perdre de la satiété
			3 => (+3f, 0, false),   // bleue
			4 => (+1f, 0, false),   // jaune
			5 => (+3f, 0, false),   // verte
			6 => (+2f, 0, false),   // noire
			7 => (0f, 0, true),     // rose : poison
			8 => (+5f, 0, false),   // cyan : nourrit + soigne
			_ => (+1f, 0, false)
		};
		if (effet.Item1 > 0f)
			effet.Item1 *= 1.65f; // PNJ : baies comestibles rassasient un peu plus
		return effet;
	}

	private static string NomCouleurBaie(int couleur)
	{
		couleur = Joueur.ClampIndexCouleurBaie(couleur);
		return couleur >= 0 && couleur < NomsCouleursBaie.Length ? NomsCouleursBaie[couleur] : couleur.ToString();
	}

	private bool CouleurApprisToxique(int couleur) => CarnetContient($"Baie {NomCouleurBaie(couleur)} : TOXIQUE");

	private bool CarnetContient(string fragment)
	{
		foreach (string l in Carnet)
			if (l != null && l.Contains(fragment))
				return true;
		return false;
	}

	private void BlesserMembreAleatoire(int degats)
	{
		if (NombreMembres <= 0)
			return;
		int i = _rngPnj.RandiRange(0, NombreMembres - 1);
		DefinirPvMembre(i, PvMembre(i) - degats);
	}

	private void SoignerMembreLePlusFaible(int soin)
	{
		int pire = -1; float ratioPire = 2f;
		for (int i = 0; i < NombreMembres; i++)
		{
			int max = PvMembreMax(i);
			if (max <= 0) continue;
			float r = (float)PvMembre(i) / max;
			if (r < ratioPire) { ratioPire = r; pire = i; }
		}
		if (pire >= 0)
			DefinirPvMembre(pire, PvMembre(pire) + soin);
	}

	private bool BiomeLocalFavorablePourCampement()
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x = Mathf.FloorToInt(GlobalPosition.X);
		int z = Mathf.FloorToInt(GlobalPosition.Z);
		return PnjHumainBiomeInstinct.EstBiomeFavorablePourCampement(x, z, seed);
	}

	/// <summary>Migration longue distance si zone hostile, faim critique, échecs répétés, ou besoin d'un biome propice au camp.</summary>
	private bool DoitPartirEnMigration()
	{
		if (DoitMigrerPourEtablirCamp())
			return true;
		if (_enPauseCamp)
		{
			if (BiomeLocalHostile() || RatioFaim() < FaimCritique)
				return true;
			if (RatioFaim() < SeuilFaimForage && _echecsRechercheBaie >= SeuilEchecsAvantMigration)
				return true;
			return false;
		}
		if (BiomeLocalHostile())
			return true;
		if (RatioFaim() < FaimCritique)
			return true;
		if (RatioFaim() < SeuilFaimForage && _echecsRechercheBaie >= SeuilEchecsAvantMigration)
			return true;
		return false;
	}

	private bool CampSocieteOuPersoEtabli()
	{
		if (_enPauseCamp)
			return true;
		return _societe != null && _societe.CampSocieteEtabli;
	}

	private bool ColonieDoitResterUnieSansCamp()
		=> _societe != null && !_societe.CampSocieteEtabli && !_campRebelleSepare;

	/// <summary>Déjà en plaine/forêt viable : installer le camp ici, pas migrer ni cueillir solo.</summary>
	private bool PrioriteEtablirCampEnBiomeFavorable(bool biomeHostile, bool biomeFavorable)
		=> ColonieDoitResterUnieSansCamp() && biomeFavorable && !biomeHostile && RatioFaim() >= SeuilFaimForage;

	private bool EstChefSocieteOuSolo()
		=> _societe == null || _societe.ChefActuel() == this;

	/// <summary>Quitter neige / zone médiocre pour installer le camp, même à faim pleine.</summary>
	private bool DoitMigrerPourEtablirCamp()
	{
		if (CampSocieteOuPersoEtabli() || _phaseCampChef == PhaseCampChef.Evaluation)
			return false;
		if (BiomeLocalFavorablePourCampement() && !BiomeLocalHostile())
			return false;
		if (RatioFaim() < FaimCritique)
			return false;

		if (_societe != null)
		{
			if (!EstChefSocieteOuSolo() && !_societe.CampSocieteEtabli)
				return BiomeLocalHostile() || !BiomeLocalFavorablePourCampement();
			if (!PeutDemarrerEvaluationCamp())
				return false;
		}

		return BiomeLocalHostile() || !BiomeLocalFavorablePourCampement();
	}

	private void TickCampEtChef(float dt, bool biomeHostile, bool biomeFavorable)
	{
		bool migrationLoinPourCamp = _etatPnj == EtatPnj.Migration && DoitMigrerPourEtablirCamp();
		if (biomeFavorable && !biomeHostile && !migrationLoinPourCamp && !DoitPartirEnMigration())
			_tempsDansBonBiome += dt;
		else if (!PrioriteEtablirCampEnBiomeFavorable(biomeHostile, biomeFavorable))
			_tempsDansBonBiome = 0f;

		if (_societe != null && !_societe.CampSocieteEtabli && PrioriteEtablirCampEnBiomeFavorable(biomeHostile, biomeFavorable))
			_societe.EffacerObjectifCampementColonie();

		if (_societe != null && !_societe.CampSocieteEtabli && EstChefSocieteOuSolo() && _societe.ChefActuel() == this)
		{
			if (!_societe.AObjectifCampementColonie)
			{
				Gestionnaire_Monde gmChef = ObtenirGestionnaireMonde();
				int seedChef = gmChef?.SeedTerrain ?? 19847;
				_societe.CalculerEtPublierCibleCampement(seedChef, new Vector2(GlobalPosition.X, GlobalPosition.Z), this);
			}
			else if (_societe.EssayerObtenirCibleCampementColonie(out Vector2 cibleColonie))
			{
				Gestionnaire_Monde gmChef = ObtenirGestionnaireMonde();
				int seedChef = gmChef?.SeedTerrain ?? 19847;
				int cx = Mathf.FloorToInt(cibleColonie.X);
				int cz = Mathf.FloorToInt(cibleColonie.Y);
				int x = Mathf.FloorToInt(GlobalPosition.X);
				int z = Mathf.FloorToInt(GlobalPosition.Z);
				float scoreCible = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(cx, cz, seedChef);
				float scoreIci = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x, z, seedChef);
				if (biomeHostile && scoreCible <= scoreIci + 0.35f)
				{
					_societe.EffacerObjectifCampementColonie();
					if (_etatPnj == EtatPnj.Migration)
						RechoisirCibleMigration();
				}
			}
		}

		if (_enPauseCamp)
		{
			MettreAJourEtiquetteCamp();
			return;
		}

		// Membre : rejoindre le camp du chef dès qu'il est établi (pas de second camp).
		if (_societe != null && !_campRebelleSepare && _societe.CampSocieteEtabli)
		{
			AccepterCampDepuisChef(_societe.StructureCamp.Ancre, _societe.IndexMembre(this));
			return;
		}

		if (_phaseCampChef == PhaseCampChef.Evaluation)
		{
			PnjHumain chef = _societe?.ChefActuel();
			if (chef != null && chef != this)
			{
				AnnulerEvaluationCamp();
				return;
			}
			TickEvaluationCampChef(dt, biomeHostile, biomeFavorable);
			return;
		}

		bool peutEvaluer = biomeFavorable && !biomeHostile && RatioFaim() >= 0.45f
			&& _tempsDansBonBiome >= TempsBonBiomeAvantCampSec;
		if (!peutEvaluer || !PeutDemarrerEvaluationCamp())
			return;

		CommencerEvaluationCampChef();
	}

	private bool PeutDemarrerEvaluationCamp()
	{
		if (_societe != null && _societe.NombreMembres >= 2)
		{
			PnjHumain chef = _societe.ChefActuel();
			if (chef == null)
			{
				// Avant élection du chef (~8 s) : le premier membre (pivot) choisit le site.
				return _societe.Membres.Count > 0 && _societe.Membres[0] == this;
			}
			if (chef == this)
				return true;
			if (PeutEtablirCampRebelleSepare())
				return true;
			return false;
		}
		return _societe == null;
	}

	private bool PeutEtablirCampRebelleSepare()
	{
		if (!_estRebelle || _societe == null || _societe.ChefActuel() == this)
			return false;
		return _societe.CampSocieteEtabli;
	}

	private bool EmplacementCampValide(Vector2 ancre) => CampPnjStructure.EstEmplacementLibre(ancre);

	private void CommencerEvaluationCampChef()
	{
		if (!PeutDemarrerEvaluationCamp())
			return;
		_phaseCampChef = PhaseCampChef.Evaluation;
		_tempsEvaluationCampSite = 0f;
		_essaisEmplacementCamp = 0;
		_siteCampPropose = new Vector2(GlobalPosition.X, GlobalPosition.Z);
		if (!EmplacementCampValide(_siteCampPropose))
			ChoisirNouveauSiteCampLocal();
		_etatPnj = EtatPnj.Idle;
		_cooldownEtatPnj = 1.5f;
		MettreAJourEtiquetteCamp();
		bool rebelle = PeutEtablirCampRebelleSepare();
		DiagForage(rebelle ? "évaluation camp rebelle (séparé)" : "évaluation d'emplacement de camp (chef)");
	}

	private void TickEvaluationCampChef(float dt, bool biomeHostile, bool biomeFavorable)
	{
		if (biomeHostile || RatioFaim() < FaimCritique)
		{
			AnnulerEvaluationCamp();
			return;
		}

		float qualite = EvaluerQualiteEmplacementCamp();
		bool surProposition = GlobalPosition.DistanceTo(new Vector3(_siteCampPropose.X, GlobalPosition.Y, _siteCampPropose.Y)) < 2.5f;

		if (qualite < SeuilQualiteCamp || !biomeFavorable)
		{
			_tempsEvaluationCampSite = 0f;
			if (_cooldownEtatPnj <= 0f && _essaisEmplacementCamp < 10)
				ChoisirNouveauSiteCampLocal();
		}
		else if (surProposition)
		{
			_tempsEvaluationCampSite += dt;
			if (_tempsEvaluationCampSite >= TempsEvaluationSiteCampSec)
				ConfirmerCampChef();
		}
		else if (_cooldownEtatPnj <= 0f)
		{
			Vector3 cible = new Vector3(_siteCampPropose.X, GlobalPosition.Y, _siteCampPropose.Y);
			if (SolExistePourCible(cible, out float y))
				cible.Y = y;
			_ciblePnj = cible;
			_etatPnj = EtatPnj.Marche;
			_cooldownEtatPnj = 10f;
		}

		MettreAJourEtiquetteCamp();
	}

	private Vector3 CalculerDeplacementEvaluationCamp(float dt)
	{
		TickCampEtChef(dt, BiomeLocalHostile(), BiomeLocalFavorablePourCampement());

		if (_etatPnj == EtatPnj.Marche)
		{
			Vector3 vers = _ciblePnj - GlobalPosition;
			vers.Y = 0f;
			if (vers.Length() < 0.8f || _cooldownEtatPnj <= 0f)
			{
				_etatPnj = EtatPnj.Idle;
				_cooldownEtatPnj = 1.2f;
				return Vector3.Zero;
			}
			return vers.Normalized();
		}
		return Vector3.Zero;
	}

	/// <summary>Score basé sur ce qu'ils savent faire : baies, biome, pente.</summary>
	private float EvaluerQualiteEmplacementCamp()
	{
		float score = 0f;
		if (BiomeLocalHostile())
			return -5f;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x = Mathf.FloorToInt(GlobalPosition.X);
		int z = Mathf.FloorToInt(GlobalPosition.Z);
		score += PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x, z, seed);
		if (PenteLocaleRaide())
			score -= 2.5f;
		if (gm != null)
		{
			if (gm.EssayerDetecterBuissonPourPnj(GlobalPosition, RayonRechercheBaie, out _, out _, pleinSeulement: true))
				score += 4f;
			else if (gm.EssayerDetecterBuissonPourPnj(GlobalPosition, RayonRechercheBaie * 1.6f, out _, out _, pleinSeulement: true))
				score += 1.5f;
			else
				score -= 1.5f;
		}
		// Plus savant = plus exigeant (attend un meilleur spot).
		score -= (Intelligence - IntelNeutrePnj) * 0.04f;
		return score;
	}

	private void ChoisirNouveauSiteCampLocal()
	{
		_essaisEmplacementCamp++;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		Vector2 origine = new Vector2(GlobalPosition.X, GlobalPosition.Z);
		float meilleurScore = float.MinValue;
		Vector2 meilleur = _siteCampPropose;
		for (int i = 0; i < 7; i++)
		{
			float angle = _rngPnj.RandfRange(0f, Mathf.Tau);
			float dist = _rngPnj.RandfRange(7f, 24f);
			Vector2 cand = origine + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			int x = Mathf.FloorToInt(cand.X);
			int z = Mathf.FloorToInt(cand.Y);
			if (PnjHumainBiomeInstinct.EstZoneHostileRapide(x, z, seed))
				continue;
			if (!CampPnjStructure.EstEmplacementLibre(cand))
				continue;
			float s = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x, z, seed);
			if (gm != null && gm.EssayerDetecterBuissonPourPnj(new Vector3(cand.X, GlobalPosition.Y, cand.Y), RayonRechercheBaie, out _, out _, pleinSeulement: true))
				s += 3.5f;
			if (s > meilleurScore)
			{
				meilleurScore = s;
				meilleur = cand;
			}
		}
		_siteCampPropose = meilleur;
		Vector3 cible = new Vector3(meilleur.X, GlobalPosition.Y, meilleur.Y);
		if (SolExistePourCible(cible, out float y))
			cible.Y = y;
		_ciblePnj = cible;
		_etatPnj = EtatPnj.Marche;
		_cooldownEtatPnj = 12f;
		DiagForage($"nouveau site camp candidat ({meilleur.X:0},{meilleur.Y:0}) score~{meilleurScore:0.0}");
	}

	private void ConfirmerCampChef()
	{
		if (!PeutDemarrerEvaluationCamp() || !EmplacementCampValide(_siteCampPropose))
		{
			ChoisirNouveauSiteCampLocal();
			return;
		}
		// Pas de téléportation : le chef a marché jusqu'au site pendant l'évaluation.
		bool campRebelle = PeutEtablirCampRebelleSepare();
		EtablirCampIci(campRebelle, _siteCampPropose);
		_phaseCampChef = PhaseCampChef.Etabli;
		if (!campRebelle)
		{
			PropagerCampSocieteSiChef();
			_societe?.NotifierCampEtabli();
		}
		MettreAJourEtiquetteCamp();
	}

	private void AnnulerEvaluationCamp()
	{
		_phaseCampChef = PhaseCampChef.Aucune;
		_tempsEvaluationCampSite = 0f;
		_etatPnj = EtatPnj.Idle;
		MettreAJourEtiquetteCamp();
	}

	private void TenterEtablirCampApresArrivee(bool biomeFavorable)
	{
		if (!biomeFavorable || !PeutDemarrerEvaluationCamp())
			return;
		CommencerEvaluationCampChef();
	}

	public void EtablirCampDepuisSauvegarde(Vector2 ancre)
	{
		_enPauseCamp = true;
		_ancreCamp = ancre;
		_phaseCampChef = PhaseCampChef.Etabli;
		_aCibleMigrationAbsolue = false;
		_cibleMigrationAbsolue = Vector3.Zero;
		_etatPnj = EtatPnj.Idle;
		if (_societe != null && _societe.ChefActuel() == this)
			InitialiserStructureCamp(ancre);
		else if (_societe == null)
			InitialiserStructureCamp(ancre);
		else
			AccepterCampDepuisChef(ancre, _societe?.IndexMembre(this) ?? 0);
	}

	private void EtablirCampIci(bool campRebelleSepare = false, Vector2? ancreCamp = null)
	{
		_enPauseCamp = true;
		_ancreCamp = ancreCamp ?? new Vector2(GlobalPosition.X, GlobalPosition.Z);
		_siteCampPropose = _ancreCamp;
		_campRebelleSepare = campRebelleSepare;
		TerminerMigration();
		_etatPnj = EtatPnj.Idle;
		_cooldownEtatPnj = _rngPnj.RandfRange(2.5f, 5f);
		if (campRebelleSepare)
		{
			if (!CampPnjStructure.EstEmplacementLibre(_ancreCamp))
			{
				LeverCampIci();
				return;
			}
			Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
			int seed = gm?.SeedTerrain ?? 19847;
			_campPerso = CampPnjStructure.Creer(_ancreCamp, seed);
			_campPerso.MaterialiserMarqueurs(gm, seed);
			CampPnjStructure.EnregistrerAncre(_ancreCamp);
		}
		else
			InitialiserStructureCamp(_ancreCamp);
		MettreAJourEtiquetteCamp();
		DiagForage($"CAMP établi à ({_ancreCamp.X:0},{_ancreCamp.Y:0}){(campRebelleSepare ? " [rebelle]" : "")}");
	}

	private void LeverCampIci()
	{
		AnnulerReservationDepotBaie();
		if (_campPerso != null)
		{
			CampPnjStructure.RetirerAncre(_ancreCamp);
			_campPerso.LibererMarqueurs();
			_campPerso = null;
		}
		_enPauseCamp = false;
		_phaseCampChef = PhaseCampChef.Aucune;
		_campRebelleSepare = false;
		_tempsDansBonBiome = 0f;
		MettreAJourEtiquetteCamp();
	}

	public void AccepterCampDepuisChef(Vector2 ancre, int indiceEmplacement = 0)
	{
		_enPauseCamp = true;
		_ancreCamp = ancre;
		_phaseCampChef = PhaseCampChef.Etabli;
		_campRebelleSepare = false;
		TerminerMigration();
		_etatPnj = EtatPnj.Idle;
		_cooldownEtatPnj = _rngPnj.RandfRange(2f, 5f);
		RepositionnerAutourDuCamp(indiceEmplacement);
		_campPerso = null;
		MettreAJourEtiquetteCamp();
	}

	private void RepositionnerAutourDuCamp(int indiceEmplacement)
	{
		Vector2 decal = CalculerDecalageEmplacementCamp(indiceEmplacement);
		Vector3 cible = new Vector3(_ancreCamp.X + decal.X, GlobalPosition.Y, _ancreCamp.Y + decal.Y);
		if (!SolExistePourCible(cible, out float y))
			return;
		cible.Y = y;
		if (GlobalPosition.DistanceTo(cible) < 1.2f)
			return;
		_ciblePnj = cible;
		_etatPnj = EtatPnj.Marche;
		_cooldownEtatPnj = 14f;
	}

	private static Vector2 CalculerDecalageEmplacementCamp(int indice)
	{
		if (indice <= 0)
			return Vector2.Zero;
		float angle = (indice - 1) * (Mathf.Tau / 7f) + 0.37f * indice;
		float dist = 3.2f + (indice % 4) * 1.7f;
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
	}

	private void PropagerCampSocieteSiChef()
	{
		if (_societe == null || _societe.ChefActuel() != this)
			return;
		int slot = 1;
		foreach (PnjHumain m in _societe.Membres)
		{
			if (m == null || m == this || !GodotObject.IsInstanceValid(m))
				continue;
			if (!m._enPauseCamp)
				m.AccepterCampDepuisChef(_ancreCamp, slot++);
		}
	}

	private void EntrerEnMarcheCamp()
	{
		Vector3 centre = new Vector3(_ancreCamp.X, GlobalPosition.Y, _ancreCamp.Y);
		for (int i = 0; i < 5; i++)
		{
			float angle = _rngPnj.RandfRange(0f, Mathf.Tau);
			float dist = _rngPnj.RandfRange(1.2f, RayonErranceCamp);
			Vector3 cand = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
			if (SolExistePourCible(cand, out float y))
			{
				cand.Y = y;
				_ciblePnj = cand;
				_etatPnj = EtatPnj.Marche;
				_cooldownEtatPnj = _rngPnj.RandfRange(2.5f, 5.5f);
				return;
			}
		}
		EntrerEnIdleSansRecursion();
	}

	private bool BiomeLocalHostile()
	{
		int cx = Mathf.FloorToInt(GlobalPosition.X / 32f);
		int cz = Mathf.FloorToInt(GlobalPosition.Z / 32f);
		double now = Time.GetTicksMsec() / 1000.0;
		if (cx == _biomeCacheCellX && cz == _biomeCacheCellZ && now < (double)_biomeHostileCacheExpire)
			return _biomeHostileCache;

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x = Mathf.FloorToInt(GlobalPosition.X);
		int z = Mathf.FloorToInt(GlobalPosition.Z);
		_biomeHostileCache = PnjHumainBiomeInstinct.EstZoneHostileRapide(x, z, seed);
		_biomeCacheCellX = cx;
		_biomeCacheCellZ = cz;
		_biomeHostileCacheExpire = (float)now + 2.5f;
		return _biomeHostileCache;
	}

	/// <summary>Recalcule la destination vers le biome favorable le plus proche (seed + pente + distance).</summary>
	private void RechoisirCibleMigration()
	{
		_aCibleMigrationAbsolue = false;
		_cibleMigrationAbsolue = Vector3.Zero;
		if (_societe != null && _societe.PeutFixerCibleCampement(this))
			_societe.EffacerObjectifCampementColonie();
		ChoisirCibleMigrationInstinctUneFois();
	}

	/// <summary>Évite de foncer dans la neige / un mur : combine objectif collectif, chef et score biome devant soi.</summary>
	private Vector3 AffinerDirectionMigrationColonie(Vector3 versCible)
	{
		if (versCible.LengthSquared() < 0.0001f)
			return versCible;
		Vector3 objectif = versCible.Normalized();
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x0 = Mathf.FloorToInt(GlobalPosition.X);
		int z0 = Mathf.FloorToInt(GlobalPosition.Z);
		float scoreOrigine = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x0, z0, seed);

		if (_societe != null && !EstChefSocieteOuSolo())
		{
			PnjHumain chef = _societe.ChefActuel();
			if (chef != null && GodotObject.IsInstanceValid(chef) && chef != this)
			{
				Vector3 versChef = chef.GlobalPosition - GlobalPosition;
				versChef.Y = 0f;
				float distChef = versChef.Length();
				if (distChef > 1.5f)
				{
					Vector3 dirChef = versChef / distChef;
					float poidsChef = distChef > 28f ? 0.55f : distChef > 14f ? 0.35f : 0.12f;
					objectif = (objectif * (1f - poidsChef) + dirChef * poidsChef).Normalized();
				}
			}
		}

		Vector3 meilleurDir = objectif;
		float meilleurScore = float.MinValue;
		for (int i = 0; i < 9; i++)
		{
			Vector3 dir = i == 0
				? objectif
				: objectif.Rotated(Vector3.Up, (i - 1) * Mathf.Tau / 8f).Normalized();
			Vector3 p = GlobalPosition + dir * 32f;
			int x1 = Mathf.FloorToInt(p.X);
			int z1 = Mathf.FloorToInt(p.Z);
			float scoreBio = PnjHumainBiomeInstinct.EvaluerScoreBiomeRapide(x1, z1, seed);
			float align = objectif.Dot(dir);
			float composite = (scoreBio - scoreOrigine) * 1.4f + align * 2.2f;
			if (composite > meilleurScore)
			{
				meilleurScore = composite;
				meilleurDir = dir;
			}
		}
		return meilleurDir;
	}

	private bool PenteLocaleRaide()
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		int x = Mathf.FloorToInt(GlobalPosition.X);
		int z = Mathf.FloorToInt(GlobalPosition.Z);
		return PnjHumainBiomeInstinct.CalculerPenteTerrain(x, z, seed, out _) > 42;
	}

	/// <summary>Pousse la cible plus loin — déprécié au profit de <see cref="RechoisirCibleMigration"/>.</summary>
	private void ProlongerMigrationDansLaMemeDirection() => RechoisirCibleMigration();

	/// <summary>Balaye la seed pour fixer la destination de migration (partagée en colonie si applicable).</summary>
	private void ChoisirCibleMigrationInstinctUneFois()
	{
		if (_aCibleMigrationAbsolue)
			return;
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		int seed = gm?.SeedTerrain ?? 19847;
		Vector2 origine = new Vector2(GlobalPosition.X, GlobalPosition.Z);
		bool rechercheCamp = DoitMigrerPourEtablirCamp() || ColonieDoitResterUnieSansCamp();

		if (_societe != null && rechercheCamp)
		{
			if (_societe.EssayerObtenirCibleCampementColonie(out Vector2 partagee))
			{
				DefinirCibleMigrationAbsolue(partagee, seed);
				DiagForage($"MIGRATION colonie -> ({partagee.X:0},{partagee.Y:0}) à {origine.DistanceTo(partagee):0} m");
				return;
			}
			if (_societe.CalculerEtPublierCibleCampement(seed, origine, this)
				&& _societe.EssayerObtenirCibleCampementColonie(out Vector2 publiee))
			{
				DefinirCibleMigrationAbsolue(publiee, seed);
				DiagForage($"MIGRATION colonie (fixée) -> ({publiee.X:0},{publiee.Y:0})");
				return;
			}
		}

		if (rechercheCamp)
		{
			Vector2 depart = _societe != null ? _societe.ObtenirCentroideMembres() : origine;
			if (depart.LengthSquared() < 1f)
				depart = origine;
			if (PnjHumainBiomeInstinct.EssayerTrouverBiomePourCampement(seed, depart, out Vector2 cibleCamp)
				|| PnjHumainBiomeInstinct.EssayerTrouverMeilleureDirectionCampement(seed, depart, out cibleCamp))
			{
				DefinirCibleMigrationAbsolue(cibleCamp, seed);
				DiagForage($"MIGRATION campement -> ({cibleCamp.X:0},{cibleCamp.Y:0}) à {depart.DistanceTo(cibleCamp):0} m");
				return;
			}
		}
		else if (PnjHumainBiomeInstinct.EssayerTrouverBiomeFavorable(seed, origine, out Vector2 cible))
		{
			DefinirCibleMigrationAbsolue(cible, seed);
			DiagForage($"MIGRATION fixée -> ({cible.X:0},{cible.Y:0}) à {origine.DistanceTo(cible):0} m");
			return;
		}

		if (PnjHumainBiomeInstinct.EssayerTrouverMeilleureDirectionCampement(seed, origine, out Vector2 secours, 320f))
		{
			DefinirCibleMigrationAbsolue(secours, seed);
			DiagForage($"MIGRATION secours directionnelle -> ({secours.X:0},{secours.Y:0})");
		}
	}

	private void ApresForageOuEchec()
	{
		if (DoitPartirEnMigration())
			EntrerEnMigration();
		else
			EntrerEnIdleSansRecursion();
	}

	private void EntrerEnIdleSansRecursion()
	{
		_etatPnj = EtatPnj.Idle;
		_cooldownEtatPnj = _rngPnj.RandfRange(2.5f, 5.5f);
	}

	private void EntrerEnIdle()
	{
		if (DoitPartirEnMigration())
		{
			EntrerEnMigration();
			return;
		}
		EntrerEnIdleSansRecursion();
	}

	/// <summary>Engage la migration : scanne la seed pour le biome favorable le plus proche.</summary>
	private void EntrerEnMigration()
	{
		if (_enPauseCamp && !BiomeLocalHostile() && RatioFaim() >= FaimCritique)
		{
			if (DoitCueillirPourReserveColonie() || ObéitOrdreChefActif() || DoitTravaillerCommeCueilleur())
				return;
			if (_echecsRechercheBaie < SeuilEchecsAvantMigration * 4)
				return;
		}
		AnnulerReservationDepotBaie();
		_enPauseCamp = false;
		_etatPnj = EtatPnj.Migration;
		RechoisirCibleMigration();
	}

	private void TerminerMigration()
	{
		_echecsRechercheBaie = 0;
		_aCibleMigrationAbsolue = false;
		_cibleMigrationAbsolue = Vector3.Zero;
	}

	private float ObtenirRayonRechercheBaieActuel()
		=> _enPauseCamp ? RayonRechercheBaieCamp : RayonRechercheBaie;

	private void EntrerEnMarcheCueilletteOrdre()
	{
		_forageRoche = false;
		Vector2 origine = _enPauseCamp ? _ancreCamp : new Vector2(GlobalPosition.X, GlobalPosition.Z);
		float rayonMarche = _enPauseCamp
			? RayonsAnneauCueilletteCamp[Mathf.Clamp(_anneauCueilletteCamp, 0, RayonsAnneauCueilletteCamp.Length - 1)]
			: RayonErrancePnj;
		for (int i = 0; i < 8; i++)
		{
			float angle = _rngPnj.RandfRange(0f, Mathf.Tau);
			float dist = _rngPnj.RandfRange(4f, rayonMarche);
			Vector3 cand = new Vector3(origine.X + Mathf.Cos(angle) * dist, GlobalPosition.Y, origine.Y + Mathf.Sin(angle) * dist);
			if (SolExistePourCible(cand, out float y))
			{
				cand.Y = y;
				_ciblePnj = cand;
				_etatPnj = EtatPnj.Marche;
				_cooldownEtatPnj = _rngPnj.RandfRange(4f, 9f);
				return;
			}
		}
		EntrerEnIdle();
	}

	private void EntrerEnRegroupementColonie()
	{
		TerminerMigration();
		Vector3 cible = GlobalPosition;
		if (_societe != null)
		{
			Vector2 centroide = _societe.ObtenirCentroideMembres();
			if (centroide.LengthSquared() > 1f)
				cible = new Vector3(centroide.X, GlobalPosition.Y, centroide.Y);
			PnjHumain chef = _societe.ChefActuel();
			if (chef != null && GodotObject.IsInstanceValid(chef) && chef != this)
			{
				Vector3 versChef = chef.GlobalPosition;
				versChef.Y = GlobalPosition.Y;
				if (GlobalPosition.DistanceTo(versChef) > 6f)
					cible = versChef;
			}
		}
		if (SolExistePourCible(cible, out float y))
			cible.Y = y;
		if (GlobalPosition.DistanceTo(cible) < 3f)
		{
			EntrerEnIdleSansRecursion();
			return;
		}
		_ciblePnj = cible;
		_etatPnj = EtatPnj.Marche;
		_cooldownEtatPnj = _rngPnj.RandfRange(5f, 9f);
	}

	private void EntrerEnMarche()
	{
		if (_enPauseCamp)
		{
			EntrerEnMarcheCamp();
			return;
		}

		if (PrioriteEtablirCampEnBiomeFavorable(BiomeLocalHostile(), BiomeLocalFavorablePourCampement()))
		{
			EntrerEnRegroupementColonie();
			return;
		}

		if (DoitPartirEnMigration() || DoitMigrerPourEtablirCamp())
		{
			EntrerEnMigration();
			return;
		}

		if (ColonieDoitResterUnieSansCamp())
		{
			EntrerEnMigration();
			return;
		}

		// Se regrouper (société émergente) — uniquement rassasié et en zone viable.
		if (_rngPnj.Randf() < 0.45f)
		{
			PnjHumain autre = TrouverPnjProche(RayonRegroupement);
			if (autre != null)
			{
				Vector3 versAutre = autre.GlobalPosition;
				if (SolExistePourCible(versAutre, out float yAutre))
					versAutre.Y = yAutre;
				_ciblePnj = versAutre;
				_etatPnj = EtatPnj.Marche;
				_cooldownEtatPnj = _rngPnj.RandfRange(3f, 7f);
				return;
			}
		}

		// Errance locale (rassasié, zone viable).
		float distMax = RayonErrancePnj;
		for (int i = 0; i < 6; i++)
		{
			float angle = _rngPnj.RandfRange(0f, Mathf.Tau);
			float dist = _rngPnj.RandfRange(3f, distMax);
			Vector3 cand = GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
			if (SolExistePourCible(cand, out float y))
			{
				cand.Y = y;
				_ciblePnj = cand;
				_etatPnj = EtatPnj.Marche;
				_cooldownEtatPnj = _rngPnj.RandfRange(3f, 7f); // sécurité : abandonne la cible si pas atteinte
				return;
			}
		}
		EntrerEnIdle();
	}

	private bool SolExistePourCible(Vector3 approx, out float y)
	{
		y = 0f;
		PhysicsDirectSpaceState3D espace = GetWorld3D()?.DirectSpaceState;
		if (espace == null)
			return false;
		Vector3 haut = new Vector3(approx.X, GlobalPosition.Y + 6f, approx.Z);
		var q = PhysicsRayQueryParameters3D.Create(haut, haut + Vector3.Down * 30f);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = espace.IntersectRay(q);
		if (hit == null || hit.Count == 0 || !hit.ContainsKey("position"))
			return false;
		y = ((Vector3)hit["position"]).Y;
		return true;
	}

	/// <summary>Oriente le corps vers la direction de déplacement (même convention que la faune : modèle face -Z + rig yaw 180).</summary>
	private void OrienterVersDirection(Vector3 dir, float dt)
	{
		if (dir.LengthSquared() < 0.0001f)
			return;
		dir = dir.Normalized();
		float yawCible = Mathf.Atan2(-dir.X, -dir.Z);
		float k = Mathf.Clamp(1f - Mathf.Exp(-9f * dt), 0f, 1f);
		Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, yawCible, k), 0f);
	}
}
