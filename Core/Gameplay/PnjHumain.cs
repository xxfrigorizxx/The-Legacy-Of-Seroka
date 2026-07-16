using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// PNJ humain : équivalent simplifié d'un joueur. Le CORPS (collisions + calage des pieds + gravité) est une réplique
/// EXACTE du joueur (Joueur.tscn) pour qu'il marche sur les marching cubes exactement pareil (ni au-dessus, ni dedans).
/// Vitaux (PV par membre, faim, stamina) + inventaire + persistance. Aucune IA pour l'instant. Spawn via /INVOCA HOMINA.
/// </summary>
public partial class PnjHumain : CharacterBody3D
{
	private const float PivotHanchesSousMeshMixamo = 0.96f;   // identique au joueur (HauteurPiedsSousPivotRigMixamo)
	private const float Gravite = 24f;
	private const string CheminAnimationIdle = "res://Modeles/Animations/imobile.fbx";
	private const float RegenStaminaParSeconde = 0.8f;
	public const float DrainFaimOfflineParHeure = 1.3f;

	public struct Membre
	{
		public string Nom;
		public int Pv;
		public int PvMax;
	}

	private static readonly List<PnjHumain> _registre = new();
	public static IReadOnlyList<PnjHumain> Tous => _registre;

	private SexeJoueur _sexe = SexeJoueur.Masculin;
	private Node3D _rig;
	private Label3D _etiquetteVitaux;
	private float _cooldownAffichageVitaux;
	private float _cooldownMangerStock;
	private float _cooldownIdentifierBaie;

	private Membre[] _membres;
	private float _faimMax = 100f, _faim = 100f;
	private float _staminaMax = 100f, _stamina = 100f;
	// Intelligence (= au joueur : base Humain 10) -> pilote le taux de réussite d'analyse, comme le joueur.
	private int _intelligence = 10;
	private int _xpAnalyse;
	public int Intelligence => _intelligence;
	public int XpAnalyse => _xpAnalyse;

	// Inventaire de base = 2 MAINS + 4 SLOTS DE CRAFT (comme le joueur, sans sac).
	// Layout : [0]=Main droite, [1]=Main gauche, [2..5]=4 slots de craft.
	public const int IdxMainDroite = 0;
	public const int IdxMainGauche = 1;
	public const int NbSlotsInventairePnj = 6;
	public SlotInventaire[] Inventaire { get; private set; }
	public SlotInventaire MainDroite => (Inventaire != null && Inventaire.Length > IdxMainDroite) ? Inventaire[IdxMainDroite] : default;
	public SlotInventaire MainGauche => (Inventaire != null && Inventaire.Length > IdxMainGauche) ? Inventaire[IdxMainGauche] : default;

	// Carnet du savoir = « cerveau » du PNJ : tout ce qu'il a appris. Vide tant qu'il n'a pas agi/analysé (brique à venir).
	private readonly List<string> _carnet = new();
	public IReadOnlyList<string> Carnet => _carnet;

	// État social : nom, alignement (ratio d'actes bons/mauvais), rebelle (ment), société + rang.
	private string _nomPnj = "";
	private bool _estRebelle;
	private int _actesBons;
	private int _actesMauvais;
	private SocietePnj _societe;
	private static readonly string[] PrenomsPnj =
		{ "Aldo", "Bryn", "Cael", "Dara", "Ewen", "Fira", "Goran", "Hila", "Iorn", "Juna", "Kova", "Lena", "Mira", "Noll", "Oren", "Pia", "Roan", "Sora", "Talv", "Ysha" };

	public string NomPnj => string.IsNullOrEmpty(_nomPnj) ? NomAffichage : _nomPnj;
	public bool EstRebelle => _estRebelle;
	public SocietePnj Societe => _societe;
	public void DefinirSociete(SocietePnj s) => _societe = s;
	public static float CalculerDrainFaimVirtuel(bool enDeplacement) => CalculerDrainFaimCommun(enDeplacement);

	internal static float CalculerDrainFaimCommun(bool enDeplacement)
	{
		float drain = Joueur.DrainFaimPassifParSeconde;
		if (enDeplacement)
			drain += Joueur.DrainFaimEffortParSeconde;
		return drain * Joueur.FacteurRalentissementDrainFaim;
	}
	public int NombreConnaissances => _carnet.Count;
	public float RatioAlignement => (_actesBons + _actesMauvais) <= 0 ? 1f : (float)_actesBons / (_actesBons + _actesMauvais);
	public bool EstGentil => RatioAlignement >= 0.5f;
	public string RangSociete => _societe != null ? _societe.RangDe(this) : "Solitaire";
	public RoleVillageoisPnj RoleVillageois => _roleVillageois;

	internal void DefinirRoleVillageois(RoleVillageoisPnj role)
	{
		_roleVillageois = role;
		MettreAJourEtiquetteCamp();
	}

	/// <summary>Reçoit une connaissance transmise par un autre PNJ (peut être vraie ou falsifiée s'il ment).</summary>
	public void RecevoirConnaissance(string info)
	{
		if (!string.IsNullOrWhiteSpace(info) && !_carnet.Contains(info))
			_carnet.Add(info);
	}

	public void EnregistrerActe(bool bon)
	{
		if (bon) _actesBons++; else _actesMauvais++;
	}

	/// <summary>Restaure l'état social depuis la sauvegarde (rejoint la société par nom).</summary>
	public void ConfigurerSocialRestore(string nom, bool rebelle, int bons, int mauvais, string societeNom)
	{
		if (!string.IsNullOrWhiteSpace(nom)) _nomPnj = nom;
		_estRebelle = rebelle;
		_actesBons = bons;
		_actesMauvais = mauvais;
		if (!string.IsNullOrWhiteSpace(societeNom))
			SocietePnj.TrouverOuCreerParNom(societeNom).Ajouter(this);
	}

	public string NomSocieteOuVide => _societe != null ? _societe.Nom : "";
	public int ActesBons => _actesBons;
	public int ActesMauvais => _actesMauvais;

	/// <summary>Note une connaissance (apprise par essai/analyse). Sans ça, le PNJ « oublie ». Évite les doublons.</summary>
	public void NoterConnaissance(string ligne)
	{
		if (string.IsNullOrWhiteSpace(ligne) || _carnet.Contains(ligne))
			return;
		_carnet.Add(ligne);
	}

	/// <summary>Apprend une baie et la transmet aux alliés proches (même société ou voisinage).</summary>
	public void ApprendreConnaissanceBaie(string ligne)
	{
		if (string.IsNullOrWhiteSpace(ligne) || _carnet.Contains(ligne))
			return;
		_carnet.Add(ligne);
		PartagerConnaissanceBaieProche(ligne);
	}

	private void PartagerConnaissanceBaieProche(string ligne)
	{
		foreach (PnjHumain autre in Tous)
		{
			if (autre == null || autre == this || !GodotObject.IsInstanceValid(autre))
				continue;
			if (GlobalPosition.DistanceTo(autre.GlobalPosition) > 5f)
				continue;
			bool memeSociete = _societe != null && autre._societe == _societe;
			if (_societe != null && !memeSociete)
				continue;
			autre.RecevoirConnaissance(ligne);
		}
	}

	public void RestaurerCarnet(IEnumerable<string> lignes)
	{
		_carnet.Clear();
		if (lignes == null)
			return;
		foreach (string l in lignes)
			if (!string.IsNullOrWhiteSpace(l) && !_carnet.Contains(l))
				_carnet.Add(l);
	}

	// Animation + comportement (errance simple)
	private AnimationPlayer _animLecteur;
	private bool _aClipMarche;
	private bool _aClipJump;
	private string _clipLocomotionCourant = "";
	private enum EtatPnj { Idle, Marche, Forage, Migration }
	private EtatPnj _etatPnj = EtatPnj.Idle;
	private Vector3 _ciblePnj;
	private float _cooldownEtatPnj;
	private readonly RandomNumberGenerator _rngPnj = new();
	// Recherche/consommation de baies (brique 3 : récolter -> manger -> apprendre).
	private float _cooldownRechercheBaie;
	private int _couleurCibleBaie;
	private Vector3 _posBuissonCible;
	private Gestionnaire_Monde _gmCache;
	// Rencontres/transmission entre PNJ.
	private float _cooldownRencontre;
	// Migration : cible absolue (instinct biome via seed) + compteurs locaux.
	private int _echecsRechercheBaie;
	private int _anneauCueilletteCamp;
	private RoleVillageoisPnj _roleVillageois = RoleVillageoisPnj.Libre;
	private Vector3 _cibleMigrationAbsolue;
	private bool _aCibleMigrationAbsolue;
	private bool _enPauseCamp;
	private Vector2 _ancreCamp;
	private float _tempsDansBonBiome;
	private enum PhaseCampChef { Aucune, Evaluation, Etabli }
	private PhaseCampChef _phaseCampChef;
	private bool _campRebelleSepare;
	private float _tempsEvaluationCampSite;
	private int _essaisEmplacementCamp;
	private Vector2 _siteCampPropose;
	private Label3D _etiquetteCamp;
	private bool _forageRoche;
	private Vector3 _posRocheCible;

	public bool EstEnPauseCamp => _enPauseCamp;
	public bool EstEnEvaluationCamp => _phaseCampChef == PhaseCampChef.Evaluation;
	public Vector2 AncreCampXZ => _ancreCamp;

	public string NomAffichage => "Humain";
	public SexeJoueur SexePnj => _sexe;
	public int NombreMembres => _membres?.Length ?? 0;
	public string NomMembre(int i) => (_membres != null && i >= 0 && i < _membres.Length) ? _membres[i].Nom : "";
	public int PvMembre(int i) => (_membres != null && i >= 0 && i < _membres.Length) ? _membres[i].Pv : 0;
	public int PvMembreMax(int i) => (_membres != null && i >= 0 && i < _membres.Length) ? _membres[i].PvMax : 0;
	public float FaimCourante { get => _faim; set => _faim = Mathf.Clamp(value, 0f, _faimMax); }
	public float StaminaCourante { get => _stamina; set => _stamina = Mathf.Clamp(value, 0f, _staminaMax); }
	public float RatioFaim() => _faimMax > 0.01f ? Mathf.Clamp(_faim / _faimMax, 0f, 1f) : 0f;

	private float ObtenirDrainFaimParSeconde()
	{
		float drain = Joueur.DrainFaimPassifParSeconde;
		Vector3 horiz = new Vector3(Velocity.X, 0f, Velocity.Z);
		if (horiz.LengthSquared() > 0.04f)
			drain += Joueur.DrainFaimEffortParSeconde;
		return drain * Joueur.FacteurRalentissementDrainFaim;
	}
	public float RatioStamina() => _staminaMax > 0.01f ? Mathf.Clamp(_stamina / _staminaMax, 0f, 1f) : 0f;

	public float RatioVieGlobale()
	{
		if (_membres == null || _membres.Length == 0)
			return 0f;
		int pv = 0, max = 0;
		for (int i = 0; i < _membres.Length; i++) { pv += _membres[i].Pv; max += _membres[i].PvMax; }
		return max > 0 ? Mathf.Clamp((float)pv / max, 0f, 1f) : 0f;
	}

	/// <summary>À appeler AVANT AddChild (donc avant _Ready) pour fixer le sexe du modèle.</summary>
	public void Configurer(SexeJoueur sexe) => _sexe = sexe;

	public void DefinirPvMembre(int i, int pv)
	{
		if (_membres != null && i >= 0 && i < _membres.Length)
			_membres[i].Pv = Mathf.Clamp(pv, 0, _membres[i].PvMax);
	}

	public void ConfigurerIntelligenceRestore(int intelligence, int xpAnalyse)
	{
		_intelligence = Mathf.Max(1, intelligence);
		_xpAnalyse = Mathf.Max(0, xpAnalyse);
	}

	public void DefinirCibleMigrationAbsolue(Vector2 xz, int seed)
	{
		float y = PnjHumainBiomeInstinct.HauteurSolMonde(xz.X, xz.Y, seed);
		_cibleMigrationAbsolue = new Vector3(xz.X, y, xz.Y);
		_aCibleMigrationAbsolue = true;
		_enPauseCamp = false;
		_etatPnj = EtatPnj.Migration;
	}

	public Vector2 CibleMigrationAbsolueXZ => _aCibleMigrationAbsolue
		? new Vector2(_cibleMigrationAbsolue.X, _cibleMigrationAbsolue.Z)
		: Vector2.Zero;
	public bool EnMigrationVersBiome => _aCibleMigrationAbsolue;

	/// <summary>Exporte tout l'état pour la simulation virtuelle hors-chunk.</summary>
	public PnjHumainEtatVirtuel ExporterEtatVirtuel()
	{
		var etat = new PnjHumainEtatVirtuel
		{
			Sexe = _sexe,
			Faim = _faim,
			Stamina = _stamina,
			Nom = NomPnj,
			Rebelle = _estRebelle,
			ActesBons = _actesBons,
			ActesMauvais = _actesMauvais,
			SocieteNom = NomSocieteOuVide,
			Intelligence = _intelligence,
			XpAnalyse = _xpAnalyse
		};
		etat.DefinirPosition(GlobalPosition);
		if (_aCibleMigrationAbsolue)
			etat.DefinirCibleMigration(new Vector2(_cibleMigrationAbsolue.X, _cibleMigrationAbsolue.Z));
		if (_enPauseCamp)
			etat.DefinirCamp(_ancreCamp);

		if (_membres != null)
		{
			etat.PvMembres = new int[_membres.Length];
			for (int i = 0; i < _membres.Length; i++)
				etat.PvMembres[i] = _membres[i].Pv;
		}
		if (Inventaire != null)
		{
			etat.Inventaire = new SlotInventaire[Inventaire.Length];
			for (int i = 0; i < Inventaire.Length; i++)
				etat.Inventaire[i] = Inventaire[i];
		}
		foreach (string l in Carnet)
			if (!string.IsNullOrWhiteSpace(l))
				etat.Carnet.Add(l);
		return etat;
	}

	/// <summary>Restaure l'état depuis une simulation virtuelle (re-matérialisation).</summary>
	public void RestaurerDepuisVirtuel(PnjHumainEtatVirtuel v, int seed)
	{
		if (v == null)
			return;
		FaimCourante = v.Faim;
		StaminaCourante = v.Stamina;
		ConfigurerIntelligenceRestore(v.Intelligence, v.XpAnalyse);
		ConfigurerSocialRestore(v.Nom, v.Rebelle, v.ActesBons, v.ActesMauvais, v.SocieteNom);
		RestaurerCarnet(v.Carnet);
		if (v.PvMembres != null)
			for (int i = 0; i < v.PvMembres.Length && i < NombreMembres; i++)
				DefinirPvMembre(i, v.PvMembres[i]);
		if (v.Inventaire != null && Inventaire != null)
		{
			int n = Mathf.Min(v.Inventaire.Length, Inventaire.Length);
			for (int i = 0; i < n; i++)
				Inventaire[i] = v.Inventaire[i];
		}
		GlobalPosition = v.Position;
		if (v.EnPauseCamp)
			EtablirCampDepuisSauvegarde(new Vector2(v.CampX, v.CampZ));
		else if (v.ACibleMigration)
			DefinirCibleMigrationAbsolue(v.CibleMigration, seed);
		else
			_etatPnj = EtatPnj.Idle;
		_echecsRechercheBaie = 0;
	}

	public override void _Ready()
	{
		_registre.Add(this);
		_rngPnj.Randomize();
		if (string.IsNullOrEmpty(_nomPnj))
		{
			_nomPnj = PrenomsPnj[_rngPnj.RandiRange(0, PrenomsPnj.Length - 1)];
			_estRebelle = _rngPnj.Randf() < 0.12f; // rebelles rares (~12%)
		}
		CollisionLayer = 1;
		CollisionMask = 1;
		FloorSnapLength = 0.32f; // identique au joueur
		InitialiserVitauxEtInventaire();
		ConstruireCorpsCommeJoueur();
		ConstruireModele();
		ConstruireEtiquetteVitaux();
		ConstruireEtiquetteCamp();
		Callable.From(PositionnerEtiquetteAuDessusTete).CallDeferred();
		Callable.From(InstinctMigrationInitialeSiBesoin).CallDeferred();
	}

	private void InstinctMigrationInitialeSiBesoin()
	{
		if (!GodotObject.IsInstanceValid(this))
			return;
		bool biomeHostile = BiomeLocalHostile();
		bool biomeFavorable = BiomeLocalFavorablePourCampement();
		bool migrerPourCamp = DoitMigrerPourEtablirCamp();
		if (PrioriteEtablirCampEnBiomeFavorable(biomeHostile, biomeFavorable))
		{
			EntrerEnRegroupementColonie();
			return;
		}
		if (RatioFaim() >= SeuilFaimForage && !migrerPourCamp)
			return;
		if (biomeHostile || PenteLocaleRaide() || migrerPourCamp)
			EntrerEnMigration();
	}

	public override void _ExitTree()
	{
		_registre.Remove(this);
	}

	private void InitialiserVitauxEtInventaire()
	{
		_membres = new[]
		{
			new Membre { Nom = "Tête", Pv = 80, PvMax = 80 },
			new Membre { Nom = "Torse", Pv = 180, PvMax = 180 },
			new Membre { Nom = "Bras G", Pv = 110, PvMax = 110 },
			new Membre { Nom = "Bras D", Pv = 110, PvMax = 110 },
			new Membre { Nom = "Jambe G", Pv = 140, PvMax = 140 },
			new Membre { Nom = "Jambe D", Pv = 140, PvMax = 140 }
		};
		_faim = _faimMax;
		_stamina = _staminaMax;
		Inventaire = new SlotInventaire[NbSlotsInventairePnj];
	}

	/// <summary>Réplique EXACTE des collisions du joueur (Joueur.tscn) : hitboxes actives + capsule de référence désactivée.</summary>
	private void ConstruireCorpsCommeJoueur()
	{
		void Ajouter(string nom, Shape3D forme, Vector3 pos, Vector3 rotDeg, bool disabled = false)
		{
			AddChild(new CollisionShape3D { Name = nom, Shape = forme, Position = pos, RotationDegrees = rotDeg, Disabled = disabled });
		}
		Ajouter("HitboxJambeG", new CapsuleShape3D { Radius = 0.075f, Height = 0.56f }, new Vector3(-0.11f, -0.44f, 0f), Vector3.Zero);
		Ajouter("HitboxJambeD", new CapsuleShape3D { Radius = 0.075f, Height = 0.56f }, new Vector3(0.11f, -0.44f, 0f), Vector3.Zero);
		Ajouter("HitboxCorps", new CapsuleShape3D { Radius = 0.19f, Height = 0.4f }, new Vector3(0f, 0.12f, 0f), Vector3.Zero);
		Ajouter("HitboxTete", new SphereShape3D { Radius = 0.105f }, new Vector3(0f, 0.58f, 0f), Vector3.Zero);
		Ajouter("HitboxBrasG", new CapsuleShape3D { Radius = 0.055f, Height = 0.34f }, new Vector3(-0.27f, 0.05f, 0f), new Vector3(0f, 0f, 72f));
		Ajouter("HitboxBrasD", new CapsuleShape3D { Radius = 0.055f, Height = 0.34f }, new Vector3(0.27f, 0.05f, 0f), new Vector3(0f, 0f, -72f));
		// Capsule de référence (désactivée) : sert UNIQUEMENT au calage des pieds, comme chez le joueur.
		Ajouter("CollisionShape3D", new CapsuleShape3D { Radius = 0.4f, Height = 1.65f }, Vector3.Zero, Vector3.Zero, disabled: true);
	}

	/// <summary>Bas de la capsule de référence (= base des pieds), identique à CalculerBasPourAlignementPiedsDuMesh du joueur.</summary>
	private static float BasReferencePieds() => 0f - (1.65f * 0.5f + 0.4f); // = -1.225

	private void ConstruireModele()
	{
		string chemin = Joueur.ObtenirCheminGlbCorpsJoueur(RaceJoueur.Humain, _sexe);
		var scene = GD.Load<PackedScene>(chemin);
		if (scene == null)
		{
			GD.PrintErr($"ZERO-K PNJ : modèle humain introuvable : {chemin}");
			return;
		}

		_rig = scene.Instantiate<Node3D>();
		_rig.Name = "RigHumainPnj";
		AddChild(_rig);
		Joueur.AppliquerEchelleRigSelonRace(_rig, RaceJoueur.Humain);
		// Taille humaine réaliste (1,80-2,00 m) : on MESURE la hauteur réelle du mesh puis on ajuste l'échelle.
		CalibrerTaillePnj();
		// Calage des pieds IDENTIQUE au joueur : yRig = basPieds + 0.96 * échelle.
		_rig.Position = new Vector3(0f, BasReferencePieds() + PivotHanchesSousMeshMixamo * _rig.Scale.Y, 0f);
		_rig.RotationDegrees = new Vector3(0f, Joueur.YawRigMixamoVersGodotDeg, 0f);
		ChargerLocomotionPnj();
		ConfigurerAttachesMains();
	}

	private const float TaillePnjMinM = 1.80f; // fourchette humaine demandée : 1m80 à 2m
	private const float TaillePnjMaxM = 2.00f;

	/// <summary>Mesure la hauteur réelle du mesh (AABB) et règle l'échelle pour une taille humaine 1,80-2,00 m (un peu de variété entre individus).</summary>
	private void CalibrerTaillePnj()
	{
		if (_rig == null)
			return;
		Aabb? combine = null;
		Joueur.AccumulerAabbMeshes(_rig, Transform3D.Identity, ref combine);
		if (!combine.HasValue)
			return;
		float hauteurVisuelle = combine.Value.Size.Y; // hauteur actuelle, échelle du rig comprise
		if (hauteurVisuelle < 0.01f)
			return;
		float cible = _rngPnj.RandfRange(TaillePnjMinM, TaillePnjMaxM);
		_rig.Scale *= cible / hauteurVisuelle;
	}

	private void ConstruireEtiquetteVitaux()
	{
		_etiquetteVitaux = new Label3D
		{
			Name = "VitauxPnj",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = Colors.White,
			OutlineSize = 6,
			FontSize = 48,
			PixelSize = 0.0016f,
			Position = new Vector3(0f, 1.2f, 0f)
		};
		AddChild(_etiquetteVitaux);
		MettreAJourEtiquetteVitaux();
	}

	/// <summary>Place l'étiquette juste au-dessus de la tête (mesure l'os le plus haut, une fois la pose disponible).</summary>
	private void PositionnerEtiquetteAuDessusTete()
	{
		if (!GodotObject.IsInstanceValid(this) || _etiquetteVitaux == null || _rig == null)
			return;
		Skeleton3D sk = TrouverPremier<Skeleton3D>(_rig);
		if (sk == null || sk.GetBoneCount() == 0)
			return;
		sk.ForceUpdateAllBoneTransforms();
		Transform3D invNpc = GlobalTransform.AffineInverse();
		float maxY = float.MinValue;
		int n = sk.GetBoneCount();
		for (int i = 0; i < n; i++)
			maxY = Mathf.Max(maxY, (invNpc * sk.GlobalTransform * sk.GetBoneGlobalPose(i)).Origin.Y);
		if (maxY != float.MinValue)
		{
			_etiquetteVitaux.Position = new Vector3(0f, maxY + 0.4f, 0f);
			if (_etiquetteCamp != null)
				_etiquetteCamp.Position = new Vector3(0f, maxY + 1.05f, 0f);
		}
	}

	private void ConstruireEtiquetteCamp()
	{
		_etiquetteCamp = new Label3D
		{
			Name = "CampPnj",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Modulate = new Color(1f, 0.92f, 0.35f),
			OutlineSize = 8,
			FontSize = 40,
			PixelSize = 0.0018f,
			Visible = false
		};
		AddChild(_etiquetteCamp);
	}

	internal void MettreAJourEtiquetteCamp()
	{
		if (_etiquetteCamp == null || !GodotObject.IsInstanceValid(_etiquetteCamp))
			return;
		string ordre = ObtenirTexteEtiquetteOrdre();
		string structure = ObtenirTexteEtiquetteCampStructure();
		if (_enPauseCamp || _phaseCampChef == PhaseCampChef.Etabli)
		{
			string baseTxt = structure ?? "Camp etabli";
			if (!string.IsNullOrEmpty(ordre))
				baseTxt += $"\n{ordre}";
			_etiquetteCamp.Text = baseTxt;
			_etiquetteCamp.Modulate = new Color(0.45f, 1f, 0.55f);
			_etiquetteCamp.Visible = true;
			return;
		}
		if (_phaseCampChef == PhaseCampChef.Evaluation)
		{
			_etiquetteCamp.Text = "Camp ?";
			_etiquetteCamp.Modulate = new Color(1f, 0.92f, 0.35f);
			_etiquetteCamp.Visible = true;
			return;
		}
		if (!string.IsNullOrEmpty(ordre))
		{
			_etiquetteCamp.Text = ordre;
			_etiquetteCamp.Modulate = new Color(0.7f, 0.85f, 1f);
			_etiquetteCamp.Visible = true;
			return;
		}
		_etiquetteCamp.Visible = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Gravité + MoveAndSlide, exactement comme le joueur : repose sur les marching cubes via les hitboxes.
		// + NAGE (immergé ≥50%) et SAUT (franchir une marche), répliques de la physique du joueur.
		float dt = (float)delta;
		if (_cooldownSautPnj > 0f)
			_cooldownSautPnj -= dt;

		Vector3 dir = CalculerDeplacementComportement(dt);
		if (dir.LengthSquared() > 0.001f)
			dir = AdapterDirectionNavigation(dir, dt);

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm != null && PnjHumainContinuiteService.DoitDematerialiser(gm, this, dir))
		{
			PnjHumainContinuiteService.Dematerialiser(this, gm);
			return;
		}

		_pnjDansEau = EvaluerEtatEauPnj(out _surfaceEauPnj);

		Vector3 v = Velocity;
		if (_pnjDansEau)
		{
			AppliquerNagePnj(ref v, dir, dt); // NAGE : flotte vers la surface + se dirige
		}
		else
		{
			Vector3 horiz = dir * Joueur.Speed; // même vitesse de marche que le joueur (= au joueur, pas cheaté)
			v.X = horiz.X;
			v.Z = horiz.Z;
			if (!IsOnFloor())
			{
				v.Y -= Gravite * dt;
			}
			else
			{
				if (v.Y < 0f)
					v.Y = -2f;
				// SAUT : franchir une marche/un rebord bas devant soi (uniquement si on se déplace).
				if (dir.LengthSquared() > 0.01f && _cooldownSautPnj <= 0f && (_demandeSautStrategiqueNav || DoitSauterPnj(dir)))
				{
					v.Y = VitesseSautPnj;
					_cooldownSautPnj = CooldownSautPnjSec;
					_demandeSautStrategiqueNav = false;
				}
			}
		}

		Velocity = v;
		MoveAndSlide();

		if (gm != null && PnjHumainContinuiteService.DoitDematerialiser(gm, this, dir))
		{
			PnjHumainContinuiteService.Dematerialiser(this, gm);
			return;
		}

		// Oriente le corps vers le déplacement réel (marche au sol comme nage vers la rive).
		Vector3 dirFace = new Vector3(Velocity.X, 0f, Velocity.Z);
		bool enMouvement = dirFace.Length() > 0.25f;
		if (enMouvement)
			OrienterVersDirection(dirFace.Normalized(), dt);
		JouerClipLocomotion(ChoisirClipLocomotion(enMouvement));
	}

	/// <summary>Choisit le clip de locomotion : nage (placeholder Marche), saut/chute en l'air (Jump), sinon marche/idle.</summary>
	private string ChoisirClipLocomotion(bool enMouvement)
	{
		if (_pnjDansEau)
			return _aClipMarche ? "pnj/Marche" : "pnj/Idle"; // pas de clip de nage dédié -> membres en mouvement
		if (!IsOnFloor() && _aClipJump)
			return "pnj/Jump"; // en l'air : pose de saut/chute
		return enMouvement && _aClipMarche ? "pnj/Marche" : "pnj/Idle";
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_faim = Mathf.Max(0f, _faim - ObtenirDrainFaimParSeconde() * dt);
		_stamina = Mathf.Min(_staminaMax, _stamina + RegenStaminaParSeconde * dt);
		MangerDepuisInventaireSiBesoin(dt); // mange son stock de baies quand il a faim -> la faim remonte
		TenterIdentifierBaiesInconnues(dt); // analyse ou essai avant de pouvoir trier au stock
		MettreAJourObjetsEnMain();           // affiche ce qu'il tient en main (baie, etc.)

		_cooldownAffichageVitaux -= dt;
		if (_cooldownAffichageVitaux <= 0f)
		{
			_cooldownAffichageVitaux = 0.25f;
			MettreAJourEtiquetteVitaux();
			MettreAJourEtiquetteCamp();
		}
	}

	private void MettreAJourEtiquetteVitaux()
	{
		if (_etiquetteVitaux == null || !GodotObject.IsInstanceValid(_etiquetteVitaux))
			return;
		int vie = Mathf.RoundToInt(RatioVieGlobale() * 100f);
		int faim = Mathf.RoundToInt(RatioFaim() * 100f);
		int stam = Mathf.RoundToInt(RatioStamina() * 100f);
		int ratio = Mathf.RoundToInt(RatioAlignement * 100f);
		string aligne = EstGentil ? "Gentil" : "Mechant";
		string societe = _societe != null ? $"{_societe.Nom} ({RangSociete})" : "Sans societe";
		string membres = "";
		if (_membres != null)
			for (int i = 0; i < _membres.Length; i++)
				membres += (i > 0 ? "  " : "") + $"{_membres[i].Nom}:{_membres[i].Pv}";
		int baiesInv = CompterBaiesInventairePourCamp();
		_etiquetteVitaux.Text =
			$"{NomPnj}{(_estRebelle ? " (rebelle)" : "")}\n" +
			$"Vie {vie}%   Faim {faim}%   Stam {stam}%\n" +
			$"{aligne} {ratio}%   {societe}" + (baiesInv > 0 ? $"   Baies:{baiesInv}" : "") + "\n" +
			$"{membres}";
		_etiquetteVitaux.Modulate = vie > 50 ? (EstGentil ? Colors.White : new Color(1f, 0.7f, 0.7f)) : (vie > 20 ? Colors.Orange : Colors.Red);
	}

	/// <summary>Charge Idle (imobile.fbx) + Marche (Marcher.fbx) sur le squelette du PNJ, dans la bibliothèque « pnj ».</summary>
	private void ChargerLocomotionPnj()
	{
		if (_rig == null)
			return;
		Skeleton3D squelette = TrouverPremier<Skeleton3D>(_rig);
		if (squelette == null)
			return;

		_animLecteur = new AnimationPlayer { Name = "AnimationPlayerPnj" };
		_rig.AddChild(_animLecteur);
		_rig.MoveChild(_animLecteur, 0);
		_animLecteur.RootNode = new NodePath("..");

		string prefixLive = (_animLecteur.GetParent() ?? _rig).GetPathTo(squelette).ToString();
		var lib = new AnimationLibrary();
		FusionnerClipDansLib(lib, CheminAnimationIdle, "Idle", prefixLive);
		_aClipMarche = FusionnerClipDansLib(lib, "res://Modeles/Animations/Marcher.fbx", "Marche", prefixLive);
		_aClipJump = FusionnerClipDansLib(lib, "res://Modeles/Animations/Jump.fbx", "Jump", prefixLive);
		if (lib.GetAnimationList().Count == 0)
			return;
		_animLecteur.AddAnimationLibrary("pnj", lib);
		JouerClipLocomotion("pnj/Idle");
	}

	private bool FusionnerClipDansLib(AnimationLibrary lib, string chemin, string nom, string prefixLive)
	{
		if (lib == null || !ResourceLoader.Exists(chemin))
			return false;
		var sc = GD.Load<PackedScene>(chemin);
		Node temp = sc?.Instantiate();
		if (temp == null)
			return false;
		try
		{
			AnimationPlayer apSrc = TrouverPremier<AnimationPlayer>(temp);
			Skeleton3D skSrc = TrouverPremier<Skeleton3D>(temp);
			if (apSrc == null || skSrc == null)
				return false;
			Animation anim = ExtrairePremiereAnimation(apSrc);
			if (anim == null)
				return false;
			string prefixSrc = (apSrc.GetParent() ?? temp).GetPathTo(skSrc).ToString();
			RemapperPrefixeSquelette(anim, prefixSrc, prefixLive);
			RemapperParMarqueurSquelette(anim, prefixLive);
			anim.LoopMode = Animation.LoopModeEnum.Linear;
			lib.AddAnimation(nom, anim);
			return true;
		}
		finally
		{
			temp.QueueFree();
		}
	}

	private void JouerClipLocomotion(string clip)
	{
		if (_animLecteur == null || !GodotObject.IsInstanceValid(_animLecteur) || _clipLocomotionCourant == clip)
			return;
		if (!_animLecteur.HasAnimation(clip))
			return;
		_animLecteur.Play(clip, 0.15f);
		_clipLocomotionCourant = clip;
	}

	private static T TrouverPremier<T>(Node racine) where T : Node
	{
		if (racine is T t)
			return t;
		foreach (Node enfant in racine.GetChildren())
		{
			T trouve = TrouverPremier<T>(enfant);
			if (trouve != null)
				return trouve;
		}
		return null;
	}

	private static Animation ExtrairePremiereAnimation(AnimationPlayer ap)
	{
		if (ap == null)
			return null;
		foreach (StringName nomLib in ap.GetAnimationLibraryList())
		{
			AnimationLibrary lib = ap.GetAnimationLibrary(nomLib);
			if (lib == null)
				continue;
			foreach (StringName nom in lib.GetAnimationList())
			{
				Animation a = lib.GetAnimation(nom);
				if (a != null)
					return (Animation)a.Duplicate(true);
			}
		}
		foreach (StringName nom in ap.GetAnimationList())
		{
			Animation a = ap.GetAnimation(nom);
			if (a != null)
				return (Animation)a.Duplicate(true);
		}
		return null;
	}

	private static void RemapperPrefixeSquelette(Animation anim, string prefixeSrc, string prefixeLive)
	{
		if (anim == null || string.IsNullOrEmpty(prefixeSrc) || prefixeLive == null)
			return;
		if (string.Equals(prefixeSrc, prefixeLive, StringComparison.Ordinal))
			return;
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string s = anim.TrackGetPath(i).ToString();
			if (s.StartsWith(prefixeSrc, StringComparison.Ordinal))
				anim.TrackSetPath(i, new NodePath(prefixeLive + s.Substring(prefixeSrc.Length)));
		}
	}

	private static void RemapperParMarqueurSquelette(Animation anim, string cheminSqueletteLive)
	{
		if (anim == null || string.IsNullOrEmpty(cheminSqueletteLive))
			return;
		const string marqueur = "Skeleton3D";
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string s = anim.TrackGetPath(i).ToString();
			int idx = s.IndexOf(marqueur, StringComparison.Ordinal);
			if (idx < 0)
				continue;
			string queue = s.Substring(idx + marqueur.Length);
			anim.TrackSetPath(i, new NodePath(cheminSqueletteLive + queue));
		}
	}
}
