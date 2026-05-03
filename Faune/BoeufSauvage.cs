using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class BoeufSauvage : CharacterBody3D
{
	/// <summary>Scène <c>Tests/SmokeVacheNatation.tscn</c> (Godot <c>--headless</c>) : simule l’eau pour valider nage / physique sans monde complet.</summary>
	public static bool ModeSmokeTestForcerDetectionEau { get; set; }

	/// <summary>État courant « dans l’eau » (natation) — utile pour smoke test et debug.</summary>
	public bool NatationEauDetectee => _dansEau;

	/// <summary>Nom du clip courant sur l’<see cref="AnimationPlayer"/> (smoke / debug).</summary>
	public string DiagnosticAnimationLocomotionCourante =>
		_animationPlayer != null && GodotObject.IsInstanceValid(_animationPlayer)
			? _animationPlayer.CurrentAnimation.ToString()
			: "";

	private enum EtatBoeuf
	{
		Errance,
		Fuite,
		Charge,
		Broutage,
		Soutien,
		Mort
	}

	[Signal] public delegate void EvolutionEvenementEventHandler(string typeEvenement, float intensite, int niveau, float ageHeures);

	[Export] public float VitesseMarche = 1.55f;
	[Export] public float VitesseFuite = 3.35f;
	[Export] public float AccelerationHorizontale = 4.8f;
	[Export] public float FreinageHorizontal = 5.6f;
	[Export] public float ForceGravite = 24f;
	[Export] public float RayonErrance = 22f;
	[Export] public float RayonRassemblement = 80f;
	[Export] public float DistancePeurJoueur = 8.5f;
	[Export] public float IntervalleNouveauButMin = 2.2f;
	[Export] public float IntervalleNouveauButMax = 7.0f;
	[Export(PropertyHint.Range, "0,8,0.1")] public float DureeIdleErranceMin = 0.8f;
	[Export(PropertyHint.Range, "0,12,0.1")] public float DureeIdleErranceMax = 2.4f;
	[Export] public float FaimMax = 100f;
	[Export] public float FaimParSeconde = 0.32f;
	[Export] public float SeuilRechercheHerbe = 48f;
	[Export] public float GainFaimParBouchee = 16f;
	[Export] public float RayonMangerHerbe = 1.35f;
	[Export] public float DureeBroutage = 3.8f;
	[Export(PropertyHint.Range, "2,40,0.5")] public float RayonRechercheHerbeVisible = 14f;
	[Export(PropertyHint.Range, "4,64,1")] public int EssaisRechercheHerbe = 20;
	[Export(PropertyHint.Range, "0.1,2,0.05")] public float DureeImmobilePendantMorsure = 0.6f;
	[Export] public float DureeCadavreAvantSuppression = 18f;
	[Export] public float ForceBase = 10f;
	[Export] public float ConstitutionBase = 10f;
	[Export] public float VitesseBase = 10f;
	[Export] public float ExperienceParNiveau = 100f;
	[Export] public float ExperienceCycleAge = 4f;
	[Export] public float IntervalleCycleAgeSecondes = 30f;
	[Export] public float BonusParNiveau = 0.001f; // 0.1%
	[ExportGroup("Progression")]
	[Export] public bool MonterNiveauParNouveauJour = true;
	[Export] public bool AutoriserNiveauxParExperience = false;
	[Export] public float ExperienceEsquive = 2f;
	[Export] public float ExperienceBroutage = 3f;
	[Export] public float ExperienceFuiteParSeconde = 1f;
	[Export] public bool UtiliserShaderPelageProcedural = false;
	[Export] public string CheminTextureDiffuseModele = "";
	/// <summary>Rotation Y locale appliquee au nœud Modele (degres), apres la transform de scene — corrige un mesh importe tourne sur le cote.</summary>
	[Export] public float CorrectionOrientationModeleDegres = 0f;
	/// <summary>Rotation Y supplementaire (degres) apres alignement -Z sur la direction (voir <see cref="OrienterCorpsVersDirectionDeplacement"/>). Reglez par pas de 90 si le mesh avance sur le cote.</summary>
	[Export] public float CorrectionYawRegardDegres = 0f;
	/// <summary>Si vrai : oriente le corps vers la <b>cible</b> (direction voulue). Si faux : vers la <b>velocite</b> apres collisions — souvent cause de marche laterale le long des murs.</summary>
	[Export] public bool PrefererDirectionCiblePourLOrientation = true;
	[Export] public float VitesseOrientationCorps = 12f;
	[ExportGroup("IA terrain adaptative (legere)")]
	[Export] public bool ActiverIATerrainAdaptative = true;
	[Export(PropertyHint.Range, "0.05,0.5,0.01")] public float IntervalleEvaluationVisionTerrain = 0.15f;
	[Export(PropertyHint.Range, "1,6,0.1")] public float DistanceVisionAvant = 2.35f;
	[Export(PropertyHint.Range, "5,75,1")] public float AngleVisionLateraleDegres = 28f;
	[Export(PropertyHint.Range, "0.2,2.5,0.05")] public float HauteurYeuxTerrain = 0.62f;
	[Export(PropertyHint.Range, "0.05,0.8,0.01")] public float LongueurStepAssist = 0.24f;
	[Export(PropertyHint.Range, "0.08,0.8,0.01")] public float HauteurMaxEnjambementObstacle = 0.28f;
	[Export(PropertyHint.Range, "0.2,1.5,0.01")] public float DistanceAvantEnjambementObstacle = 0.52f;
	[Export(PropertyHint.Range, "0.05,4,0.01")] public float VitesseMinEnjambementObstacle = 0.28f;
	[Export(PropertyHint.Range, "0.1,1,0.01")] public float NormalYMinSolEnjambementObstacle = 0.45f;
	[Export(PropertyHint.Range, "-1,0.9,0.01")] public float NormalYMaxObstacleEnjambement = 0.32f;
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float CooldownEnjambementObstacleSec = 0.09f;
	[Export] public bool ActiverDetectionVideDevant = true;
	[Export(PropertyHint.Range, "0.2,8,0.1")] public float ProfondeurVideCritique = 2.4f;
	[Export] public bool ActiverSautStrategique = true;
	[Export(PropertyHint.Range, "0.5,12,0.1")] public float ImpulsionSautStrategique = 4.2f;
	[Export(PropertyHint.Range, "1,4,0.1")] public float MultiplicateurHauteurSaut = 2.0f;
	[Export(PropertyHint.Range, "0.1,3,0.05")] public float CooldownSautStrategique = 0.9f;
	[Export(PropertyHint.Range, "0.2,4,0.05")] public float DeltaHauteurMinSautEscalade = 0.45f;
	[Export(PropertyHint.Range, "0.5,8,0.1")] public float DeltaHauteurMaxSautEscalade = 2.8f;
	[Export(PropertyHint.Range, "0.4,5,0.1")] public float DistanceSautEscalade = 1.8f;
	[Export(PropertyHint.Range, "0.2,4,0.05")] public float DistanceMiniEntreDeuxSauts = 1.6f;
	[Export(PropertyHint.Range, "0.15,1.5,0.05")] public float IntervalleDetectionCoincage = 0.42f;
	[Export(PropertyHint.Range, "0.02,1,0.01")] public float ProgressionMinAvantCoincage = 0.16f;
	[Export(PropertyHint.Range, "0.5,8,0.1")] public float DistanceCibleMinPourDetectionCoincage = 2.2f;
	[Export] public bool ActiverApprentissageNavigation = true;
	[Export(PropertyHint.Range, "0.005,0.5,0.005")] public float TauxApprentissageNavigation = 0.08f;
	[ExportGroup("Perception evolutive (cone + ligne de vue)")]
	[Export] public bool UtiliserConeVisionJoueur = true;
	[Export(PropertyHint.Range, "45,270,1")] public float AngleVisionBaseDegres = 120f;
	[Export(PropertyHint.Range, "0,20,0.1")] public float GainAngleVisionParNiveauDegres = 4f;
	[Export(PropertyHint.Range, "90,300,1")] public float AngleVisionMaxDegres = 270f;
	[Export(PropertyHint.Range, "2,80,0.5")] public float DistanceVisionMaxJoueur = 22f;
	[Export(PropertyHint.Range, "0.05,0.5,0.01")] public float IntervalleVerificationVisionJoueur = 0.12f;
	[Export(PropertyHint.Range, "0.1,4,0.05")] public float HauteurYeuxPerception = 1.05f;
	[Export(PropertyHint.Range, "0.1,10,0.1")] public float DistanceOuieJoueur = 3.4f;
	[Export(PropertyHint.Range, "0.1,3,0.05")] public float MemoireDetectionSecondes = 0.7f;
	[ExportGroup("Comportement taureau protecteur")]
	[Export] public bool TaureauProtegeFemelles = true;
	[Export(PropertyHint.Range, "4,80,0.5")] public float DistanceAlerteFemelle = 20f;
	[Export(PropertyHint.Range, "0.2,4,0.1")] public float DureeChargeProtection = 1.8f;
	[ExportGroup("Reproduction et mutation")]
	[Export] public bool ActiverReproductionFaune = true;
	[Export(PropertyHint.Range, "0.0001,0.2,0.0001")] public float ChanceConceptionParSeconde = 0.0125f;
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float ChanceConceptionJournaliere = 0.38f;
	[Export(PropertyHint.Range, "10,1200,1")] public float DureeGestationSecondes = 180f;
	[Export(PropertyHint.Range, "1,5,0.1")] public float MultiplicateurFaimGestation = 2.0f;
	[Export(PropertyHint.Range, "5,1200,1")] public float CooldownReproductionSecondes = 120f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ProbabiliteNaissanceMale = 0.45f;
	[Export(PropertyHint.File, "*.tscn,*.glb,*.gltf")] public string CheminSceneNaissanceFemelle = "res://Scenes/Faune/VacheSauvage.tscn";
	[Export(PropertyHint.File, "*.tscn,*.glb,*.gltf")] public string CheminSceneNaissanceMale = "res://Scenes/Faune/BoeufSauvage.tscn";
	[Export(PropertyHint.File, "*.tscn,*.glb,*.gltf")] public string CheminSceneVeauFemelle = "res://Scenes/Faune/VeauFemelleSauvage.tscn";
	[Export(PropertyHint.File, "*.tscn,*.glb,*.gltf")] public string CheminSceneVeauMale = "res://Scenes/Faune/VeauMaleSauvage.tscn";
	[Export] public bool NaissanceSousFormeVeau = true;
	[Export(PropertyHint.Range, "300,86400,1")] public float DureeVeauAvantMaturiteSecondes = 43200f;
	[Export(PropertyHint.Range, "0.25,0.95,0.01")] public float FacteurTailleVeau = 0.62f;
	[Export(PropertyHint.Range, "0.1,1,0.01")] public float RayonReproductionJour = 12f;
	[Export(PropertyHint.Range, "1,20,1")] public int MaxCandidatsMalesParAge = 4;
	[ExportGroup("Evolution comportementale par environnement")]
	[Export] public bool ActiverEvolutionEnvironnementale = true;
	[Export(PropertyHint.Range, "30,1800,1")] public float IntervalleEvaluationEnvironnementSecondes = 180f;
	[Export(PropertyHint.Range, "0.001,0.2,0.001")] public float IntensiteAdaptationComportementale = 0.035f;
	[Export(PropertyHint.Range, "2,120,1")] public float EtaSBX = 18f;
	[Export(PropertyHint.Range, "2,120,1")] public float EtaMutationPolynomiale = 22f;
	[Export(PropertyHint.Range, "0.4,2.0,0.01")] public float TailleGeneMin = 0.7f;
	[Export(PropertyHint.Range, "0.4,2.0,0.01")] public float TailleGeneMax = 1.5f;
	[Export(PropertyHint.Range, "0.4,2.0,0.01")] public float TailleGeneMaxGenerationInitiale = 1.0f;
	[Export(PropertyHint.Range, "0,0.5,0.005")] public float IntensiteMutationTaille = 0.08f;
	[Export(PropertyHint.Range, "0.5,2.0,0.01")] public float VitesseGeneMin = 0.82f;
	[Export(PropertyHint.Range, "0.5,2.0,0.01")] public float VitesseGeneMax = 1.32f;
	[Export(PropertyHint.Range, "0,0.4,0.005")] public float IntensiteMutationVitesse = 0.07f;
	[Export(PropertyHint.Range, "0.4,1.2,0.01")] public float MultiplicateurTailleGlobale = 0.8f;
	[Export] public bool AjusterHitboxSurModele = true;
	[Export(PropertyHint.Range, "0.6,1.4,0.01")] public float MultiplicateurHitbox = 0.95f;
	[Export] public bool UtiliserHitboxComposite = true;
	[Export(PropertyHint.Range, "0.4,1.6,0.01")] public float MultiplicateurHitboxTete = 1.05f;
	[Export(PropertyHint.Range, "0.4,1.6,0.01")] public float MultiplicateurHitboxVentre = 1.0f;
	[ExportGroup("UI faim")]
	[Export] public bool AfficherFaimAuDessusBovin = false;
	[Export(PropertyHint.Range, "0.5,4,0.05")] public float HauteurAffichageFaim = 1.55f;
	[Export(PropertyHint.Range, "0.05,1,0.01")] public float IntervalleMajCohesionUiSec = 0.2f;
	[ExportGroup("Stamina")]
	[Export] public bool AfficherStaminaAuDessusBovin = false;
	[Export] public float StaminaMax = 100f;
	[Export(PropertyHint.Range, "0.1,20,0.1")] public float CoutStaminaCourseParSeconde = 4.8f;
	[Export(PropertyHint.Range, "0.1,50,0.1")] public float CoutStaminaAttaque = 14f;
	[Export(PropertyHint.Range, "0.1,50,0.1")] public float CoutStaminaSaut = 10f;
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float RegenerationStaminaParSeconde = 6.5f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float CoutFaimParPointStaminaRegen = 0.08f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float SeuilFatigueCourse = 0.15f;
	[Export(PropertyHint.Range, "0.05,1,0.01")] public float DecalageVerticalBarreStamina = 0.32f;
	[ExportGroup("Natation")]
	[Export] public bool ActiverNatationFaune = true;
	[Export(PropertyHint.Range, "50,200,1")] public float NiveauSurfaceEauReference = 103f;
	[Export(PropertyHint.Range, "0.1,6,0.05")] public float VitesseNageHorizontale = 1.9f;
	[Export(PropertyHint.Range, "0.1,15,0.1")] public float PousseeRemonteeEau = 5.2f;
	[Export(PropertyHint.Range, "0.1,20,0.1")] public float GraviteDansEau = 3.0f;
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float CoutStaminaNageParSeconde = 6.0f;
	[Export(PropertyHint.Range, "0.1,20,0.1")] public float CoutStaminaMaintienSurfaceParSeconde = 2.4f;
	[Export(PropertyHint.Range, "0.1,6,0.05")] public float VitesseRemonteeNage = 1.65f;
	[Export(PropertyHint.Range, "0.1,4,0.05")] public float VitesseDescenteSansNage = 0.65f;
	[Export(PropertyHint.Range, "2,40,0.5")] public float RayonRechercheSortieEau = 10f;
	[Export(PropertyHint.Range, "0.05,1,0.01")] public float IntervalleRecalculDirectionNage = 0.25f;
	[ExportGroup("Vie")]
	[Export] public bool AfficherVieAuDessusBovin = false;
	[Export] public float VieMax = 50f;
	[Export(PropertyHint.Range, "0.001,0.2,0.001")] public float RegenViePourcentageParCycle = 0.01f;
	[Export(PropertyHint.Range, "30,7200,1")] public float IntervalleRegenVieSecondes = 900f; // 15 minutes
	[Export(PropertyHint.Range, "0.1,30,0.1")] public float DegatsVieParSecondeFaimNulle = 2.0f;
	[Export(PropertyHint.Range, "0.05,1,0.01")] public float DecalageVerticalBarreVie = 0.32f;
	[ExportGroup("Combat bovin")]
	[Export(PropertyHint.Range, "0.01,2,0.01")] public float MultiplicateurDegatsImpact = 0.11f;
	[Export(PropertyHint.Range, "0.01,3,0.01")] public float DegatsMinImpact = 0.18f;
	[Export(PropertyHint.Range, "0.05,2,0.01")] public float CooldownDegatsParSourceSecondes = 0.22f;
	[Export(PropertyHint.Range, "0.05,0.8,0.01")] public float CapDegatsParImpactRatioVieMax = 0.24f;
	[Export(PropertyHint.Range, "0.1,12,0.1")] public float ImpulsionChargeSurJoueur = 5.8f;
	[Export(PropertyHint.Range, "0.05,2,0.01")] public float CooldownImpactChargeJoueur = 0.38f;
	[Export(PropertyHint.Range, "0.05,2,0.01")] public float DureeFlashRougeDegats = 0.22f;
	[ExportGroup("Audio combat bovin")]
	[Export(PropertyHint.File, "*.ogg,*.wav,*.mp3")] public string CheminSonCriDegats = "res://Audio/Faune/cow_moo_hit.wav";
	[Export(PropertyHint.Range, "0.05,2,0.01")] public float CooldownCriDegatsSecondes = 0.35f;
	[Export(PropertyHint.Range, "-24,6,0.1")] public float VolumeCriDegatsDb = -4.5f;

	[ExportGroup("Dépeçage cadavre")]
	/// <summary>Nombre de coups de dague (lame) valides sur le cadavre avant la distribution du loot. Au-delà de 3 pour ralentir le dépeçage.</summary>
	[Export(PropertyHint.Range, "3,20,1")] public int CoupsDagueRequisPourFinDepecage = 6;

	[ExportGroup("Animation vivante (squelette + scenes + arbre)")]
	/// <summary>
	/// Pipeline : squelette sous Modele → fusion <c>locomotion_faune</c> → <see cref="AnimationTree"/> (machine d'états).
	/// Correspondance noms (clips GLB / chemins) ↔ champs code : marche→<see cref="_clipMarche"/> (walk…), course/galop→<see cref="_clipCourse"/>,
	/// saut→<see cref="_clipSaut"/> (jump…), saut en course→<see cref="_clipSautGalop"/> (gallop_jump…), manger→<see cref="_clipManger"/> (eating, eat…),
	/// mort→<see cref="_clipMort"/>, ruade arrière→<see cref="_clipAttaqueKick"/>, coup de tête avant→<see cref="_clipAttaqueTete"/>.
	/// Nœuds machine (fixes) : <c>Deplacement</c>, <c>Broutage</c>, <c>Mort</c>, <c>Nage</c>, <c>Saut</c>, <c>SautGalop</c>, <c>AttaqueKick</c>, <c>AttaqueTete</c>.
	/// Registre JSON <see cref="CheminRegistryAnimationsFaune"/> : clés idle, walk, run, trot, graze, swim, death, jump, gallop_jump, attack_kick, attack_head (motifs → clips).
	/// </summary>
	[Export] public bool UtiliserAnimationTreeLocomotion = true;
	[Export(PropertyHint.Range, "0.4,1.4,0.01")]
	public float MultiplicateurVitesseAnimation = 0.84f;
	/// <summary>Variation legere de vitesse d'animation (0 = desactive) pour casser la repetition mecanique en idle / errance.</summary>
	[Export(PropertyHint.Range, "0,0.2,0.001")]
	public float IntensiteMicroVivaciteAnimation = 0.045f;

	[ExportSubgroup("Reference squelette (PackedScene)")]
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminGlbSqueletteReference = "res://Modeles/Entites/Boeufs/BoeufSauvage.glb";
	[Export] public bool FusionnerAutomatiquementAnimationsDuGlbReference = true;

	[ExportSubgroup("Une scene par action (meme rig que la vache ; 1ere anim du fichier)")]
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminSceneAnimationIdle = "";
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminSceneAnimationMarche = "";
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminSceneAnimationCourse = "";
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminSceneAnimationBroutage = "";
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminSceneAnimationMort = "";

	[ExportSubgroup("Optionnel")]
	/// <summary>Si vide : recherche automatique sous <c>res://Modeles/Entites/Boeufs/</c> (fichiers dont le nom contient <c>anim</c>, ex. <c>BoeufSauvage_animations.glb</c>) puis candidats fixes.</summary>
	[Export(PropertyHint.File, "*.glb,*.gltf,*.fbx,*.tscn")]
	public string CheminSceneGltfAnimationsExternesMemeRig = "";
	[Export(PropertyHint.Dir)] public string DossierAnimationsAnimalesCompatibles = "res://Modeles/Entites/AnimationsAnimales/";
	[Export] public bool AfficherDiagnosticClipsUneFois = true;
	[Export] public bool ActiverSelectionEvolutionnaireAnimations = true;
	[Export(PropertyHint.File, "*.json")] public string CheminRegistryAnimationsFaune = "res://Faune/animation_registry_bovins.json";
	[Export(PropertyHint.Range, "2,180,0.5")] public float IntervalleVariationAnimationSecondes = 16f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float IntensiteSelectionAnimationEvolutive = 0.72f;
	[Export(PropertyHint.Range, "2,24,0.5")] public float IntervalleMinCycleIdleSecondes = 4.5f;
	[Export(PropertyHint.Range, "3,36,0.5")] public float IntervalleMaxCycleIdleSecondes = 11f;

	private Gestionnaire_Monde _gestionnaire;
	private GestionnaireFauneBoeufs _gestionnaireFaune;
	private CharacterBody3D _joueur;
	/// <summary>Direction horizontale normalisee vers <see cref="_cibleCourante"/> (ou zero), mise a jour chaque frame physique avant <see cref="MoveAndSlide"/>.</summary>
	private Vector3 _directionDeplacementHorizontale;
	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
	private Vector3 _ancreTroupeau;
	private Vector3 _cibleCourante;
	private float _cooldownChoixCible;
	private float _cooldownControleSol;
	private float _cooldownAntiBlocage;
	private float _cooldownEnjambementObstacle;
	private float _cooldownEvaluationVisionTerrain;
	private float _cooldownSautStrategique;
	private float _cooldownVerificationVisionJoueur;
	private float _memoireDetectionJoueur;
	private float _cooldownReproduction;
	private float _timerDetectionCoincage;
	private float _biaisEvitementTerrain;
	private float _cooldownMorsure;
	private int _echecsMorsureConsecutifs;
	private float _verrouMouvementMorsure;
	private float _tempsGestationRestant;
	private float _tempsFuite;
	private float _tempsCharge;
	private float _tempsBroutage;
	private float _tempsIdleErrance;
	private float _faimCourante = 100f;
	private float _faimMaxActuelle = 100f;
	private float _staminaCourante = 100f;
	private float _staminaMaxActuelle = 100f;
	private float _vieCourante = 50f;
	private float _vieMaxActuelle = 50f;
	private float _tempsMort;
	/// <summary>Cadavre : attend <see cref="CoupsDagueRequisPourFinDepecage"/> coups de dague avant disparition ; pas de décompte timer tant que vrai.</summary>
	private bool _cadavreAttendDepecage;
	/// <summary>True après distribution du loot (QueueFree côté joueur ou filet).</summary>
	private bool _cadavreLootDistribue;
	private int _coupsDepecageDagueValides;
	private float _ageSecondes;
	private float _experience;
	private float _cooldownAge;
	private float _cooldownRegenVie;
	private float _cooldownVerificationBarresUI;
	private float _cooldownEvaluationEnvironnement;
	private float _cohesionUiCachee = 1f;
	private ulong _tickDerniereMajCohesionUi;
	private float _cooldownDirectionNage;
	private float _cooldownTickCerveau;
	private float _dtAccumuleTickCerveau;
	private float _cooldownCohesionAnimation;
	private float _cohesionAnimationCache = 0f;
	private bool _dansEau;
	/// <summary>Aligné sur le joueur : remontée uniquement si « nage vers le haut » (rive / profondeur), pas dès qu’il reste du stamina.</summary>
	private bool _eauIntentionRemonter;
	private Vector3 _directionNageEau = Vector3.Zero;
	/// <summary>Fenêtre courte après un saut stratégique réel pour autoriser l’anim saut (évite micro-rebonds).</summary>
	private float _fenetreAnimSautStrategique;
	private CollisionShape3D _hitboxTete;
	private CollisionShape3D _hitboxVentre;
	private readonly Dictionary<ulong, double> _horodatageDernierDegatParSource = new();
	private AudioStreamPlayer3D _audioCriDegats;
	private double _horodatageDernierCriDegats = -999.0;
	private int _niveau = 1;
	private int _seedTerrain;
	private bool _initialise;
	private bool _peutEsquiver;
	private bool _peutAttaquer;
	private bool _peutSuivre;
	private bool _peutAider;
	private EtatBoeuf _etat = EtatBoeuf.Errance;
	private const string NomNoeudDeplacement = "Deplacement";
	private const string NomNoeudBroutage = "Broutage";
	private const string NomNoeudMort = "Mort";
	private const string NomNoeudSaut = "Saut";
	private const string NomNoeudSautGalop = "SautGalop";
	private const string NomNoeudAttaqueKick = "AttaqueKick";
	private const string NomNoeudAttaqueTete = "AttaqueTete";
	private static readonly StringName NomNoeudDeplacementString = new StringName(NomNoeudDeplacement);
	private static readonly StringName NomNoeudBroutageString = new StringName(NomNoeudBroutage);
	private static readonly StringName NomNoeudMortString = new StringName(NomNoeudMort);
	private static readonly StringName NomNoeudNageString = new StringName("Nage");
	private static readonly StringName NomNoeudSautString = new StringName(NomNoeudSaut);
	private static readonly StringName NomNoeudSautGalopString = new StringName(NomNoeudSautGalop);
	private static readonly StringName NomNoeudAttaqueKickString = new StringName(NomNoeudAttaqueKick);
	private static readonly StringName NomNoeudAttaqueTeteString = new StringName(NomNoeudAttaqueTete);
	private const string ParamBlendDeplacement = "parameters/Deplacement/blend_position";
	private static readonly StringName NomBibliothequeLocomotionFaune = "locomotion_faune";
	/// <summary>Nœud optionnel dans la scene (ex. <c>VacheSauvage.tscn</c>) : si present, on le configure au lieu d'en creer un dynamiquement.</summary>
	private const string NomNoeudAnimationTreeFauneEditeur = "AnimationTreeFaune";
	/// <summary>Faute de frappe frequente dans la scene : meme nœud que <see cref="NomNoeudAnimationTreeFauneEditeur"/>.</summary>
	private const string NomNoeudAnimationTreeFauTypo = "AnimationTreeFau";

	/// <summary>Chemins testes si <see cref="CheminSceneGltfAnimationsExternesMemeRig"/> est vide (meme dossier que le squelette).</summary>
	private static readonly string[] CheminsAnimationExterneAuto =
	{
		"res://Modeles/Entites/Boeufs/BoeufSauvage_animations.glb",
		"res://Modeles/Entites/Boeufs/BoeufSauvage_animations.gltf",
		"res://Modeles/Entites/Boeufs/boeuf_animations.glb",
		"res://Modeles/Entites/Boeufs/boeuf_animations.gltf",
	};

	/// <summary>Même remappage squelette pour tous les individus d’une scène : on ne recharge pas les GLB externes à chaque spawn.</summary>
	private static readonly object VerrouCacheBibliothequesAnimExternes = new object();
	private static readonly Dictionary<string, List<(string libDest, AnimationLibrary lib)>> CacheBibliothequesExternesRemappees =
		new(StringComparer.Ordinal);
	private static List<string> CacheListeCheminsDossierAnimationsCompatibles;
	private static string DossierListeCheminsCache = "";
	private static bool LogScanDossierAnimationsCompatiblesEffectue;
	private static bool DiagnosticListeClipsDejaAffichePourProcessus;

	/// <summary>Libère les <see cref="AnimationLibrary"/> mises en cache entre les individus (évite désordre dispose à la fermeture du moteur).</summary>
	public static void ViderCachesBibliothequesExternesPourDechargementMonde()
	{
		lock (VerrouCacheBibliothequesAnimExternes)
		{
			foreach (var liste in CacheBibliothequesExternesRemappees.Values)
			{
				if (liste == null) continue;
				foreach ((_, AnimationLibrary lib) in liste)
				{
					if (lib != null && GodotObject.IsInstanceValid(lib))
						lib.Dispose();
				}
			}
			CacheBibliothequesExternesRemappees.Clear();
			CacheListeCheminsDossierAnimationsCompatibles = null;
			DossierListeCheminsCache = "";
			LogScanDossierAnimationsCompatiblesEffectue = false;
			DiagnosticListeClipsDejaAffichePourProcessus = false;
		}
	}
	private AnimationPlayer _animationPlayer;
	private AnimationTree _animationTreeFaune;
	private AnimationNodeStateMachinePlayback _playbackEtatFaune;
	private string _etatCourantMachineAnimation = "";
	private bool _blendLocomotionActif;
	private bool _machineAPorteBroutage;
	private bool _machineAPorteMort;
	private bool _machineAPorteNage;
	private bool _machineAPorteSaut;
	private bool _machineAPorteSautGalop;
	private bool _machineAPorteAttaqueKick;
	private bool _machineAPorteAttaqueTete;
	private int _tentativesLiaisonPlaybackArbre;
	private const int MaxTentativesLiaisonPlaybackArbre = 40;
	private string _clipIdle = "";
	private string _clipMarche = "";
	private string _clipCourse = "";
	private string _clipManger = "";
	private string _clipMort = "";
	private string _clipTrot = "";
	private string _clipNage = "";
	private string _clipSaut = "";
	private string _clipSautGalop = "";
	/// <summary>Coup de pied / ruade (cible derriere ou sur le cote arriere).</summary>
	private string _clipAttaqueKick = "";
	/// <summary>Coup de tete frontal.</summary>
	private string _clipAttaqueTete = "";
	private float _tempsVerrouAnimationCombat;
	private string _noeudAnimationCombatVerrou = "";
	private float _timerCycleIdleSecondes;
	private int _indexCycleIdle = -1;
	private bool _reconfigurationArbreAnimationEnAttente;
	private float _cooldownReconfigurationArbreAnimation;
	[ExportGroup("Diagnostic performance")]
	[Export] public bool ActiverProfilagePerfBovin = false;
	[Export(PropertyHint.Range, "0.2,10,0.1")] public float IntervalleLogProfilageBovinSec = 2.0f;
	[Export(PropertyHint.Range, "0.05,2,0.01")] public float CooldownReconfigurationAnimationTreeSec = 0.25f;
	[ExportGroup("Diagnostic spawn")]
	[Export] public bool ActiverDiagnosticSpawnBovin = false;
	[Export(PropertyHint.Range, "1,120,1")] public int FramesDiagnosticSpawnBovin = 20;
	private float _cooldownDrainProfilage;
	private readonly Dictionary<string, StringName> _cacheStringNameAnimations = new(StringComparer.Ordinal);
	private float _dernierBlendAnimation = float.NaN;
	private float _derniereVitesseAnimation = float.NaN;
	private readonly List<BoeufSauvage> _scratchCandidatsReproduction = new();
	private readonly Dictionary<string, List<string>> _poolsAnimationsEvolutives = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<ShaderMaterial> _materiauxPelageInstances = new();
	private float _cooldownImpactChargeJoueur;
	private float _flashRougeDegatsRestant;
	private float _cooldownVariationAnimation;
	private Shader _shaderPelageBoeuf;
	private Texture2D _textureDiffuseModele;
	// Cache global + anti-spam : on n'essaie de charger la texture qu'une seule fois par chemin, quel que soit le nombre de bovins.
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D> _cacheTextureDiffuseBoeuf = new System.Collections.Generic.Dictionary<string, Texture2D>();
	private static readonly System.Collections.Generic.HashSet<string> _cheminsTextureIntrouvablesLoggues = new System.Collections.Generic.HashSet<string>();
	private Node3D _modeleVisuel;
	private Skeleton3D _squelletteModele;
	private float _phaseLocomotionSqueletteProcedurale;
	private bool _diagnosticClipsAffiche;
	private bool _animationTreeCreeParScript;
	private bool _estEnGestation;
	private bool _estVeauActif;
	private bool _tentativeReproductionJourEffectuee;
	private BoeufSauvage _maleGestationReference;
	private bool _geneTailleInitialise;
	private float _geneTaille = 1f;
	private float _geneVitesseDeplacement = 1f;
	private float _genePersonnalite = 0.5f;
	private bool _genesComportementInitialises;
	private float _geneConfiance = 0.5f;
	private float _geneReflexeFuite = 0.5f;
	private float _geneReflexeAttaque = 0.5f;
	private float _scoreAdaptationEnvironnement = 0.5f;
	private bool _deblocageAnimationContextuelle;
	private bool _deblocageStrategieTroupeau;
	private bool _deblocageAffichageTroupeau;
	private string _identifiantIndividu = "";
	private Transform3D _transformModeleBase;
	private Vector3 _positionReferenceCoincage;
	private Vector3 _positionDernierSaut;
	private int _streakCoincage;
	private bool _genesNavigationInitialises;
	private float _genePrudenceNavigation = 0.52f;
	private float _geneAudaceSaut = 0.48f;
	private float _scoreNavigationEvolutif = 0.5f;
	private Label3D _labelFaim3D;
	private Label3D _labelStamina3D;
	private Label3D _labelVie3D;
	private static readonly Dictionary<int, string[]> _cacheBarresRatio = new Dictionary<int, string[]>();
	private Cycle_Solaire _cycleSolaire;
	private bool _abonneNouveauJour;
	private int _framesDiagnosticSpawnRestantes;
	private bool _diagnosticBlocageInitialisationDejaLogge;
	private bool _diagnosticMortPersistanteDejaLogge;

	public bool EstEnDetresse() => _etat == EtatBoeuf.Fuite || _faimCourante < SeuilRechercheHerbe * 0.65f;

	public Godot.Collections.Dictionary<string, Variant> ExtraireProfilEvolution()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "niveau", _niveau },
			{ "age_heures", _ageSecondes / 3600f },
			{ "experience", _experience },
			{ "force", ForceActuelle },
			{ "constitution", ConstitutionActuelle },
			{ "vitesse", VitesseStatActuelle },
			{ "faim", _faimCourante },
			{ "stamina", _staminaCourante },
			{ "vie", _vieCourante },
			{ "gene_taille", _geneTaille },
			{ "gene_vitesse", _geneVitesseDeplacement },
			{ "gene_personnalite", _genePersonnalite },
			{ "gene_confiance", _geneConfiance },
			{ "gene_fuite", _geneReflexeFuite },
			{ "gene_attaque", _geneReflexeAttaque },
			{ "score_adaptation_env", _scoreAdaptationEnvironnement },
			{ "gene_prudence_nav", _genePrudenceNavigation },
			{ "gene_audace_saut", _geneAudaceSaut },
			{ "score_navigation", _scoreNavigationEvolutif },
			{ "id_individu", _identifiantIndividu },
			{ "est_veau", _estVeauActif },
			{ "sexe", EstFemelle ? "femelle" : "male" },
			{ "gestation", _estEnGestation },
			{ "angle_vision_deg", AngleVisionActuelDegres() },
			{ "peut_attaquer", _peutAttaquer },
			{ "peut_esquiver", _peutEsquiver },
			{ "peut_suivre", _peutSuivre },
			{ "peut_aider", _peutAider },
			{ "deblocage_anim_contextuelle", _deblocageAnimationContextuelle },
			{ "deblocage_pensee_troupeau", _deblocageStrategieTroupeau },
			{ "deblocage_affichage_troupeau", _deblocageAffichageTroupeau }
		};
	}

	private float MultiplicateurNiveau => 1f + ((_niveau - 1) * BonusParNiveau);
	private bool EstFemelle => this is VacheSauvage;
	private bool EstTaureau => !EstFemelle;
	private float TailleEffective => _geneTaille * Mathf.Clamp(MultiplicateurTailleGlobale, 0.4f, 1.2f) * FacteurAgeMorphologique();
	private float FacteurAgeMorphologique() => _estVeauActif ? Mathf.Clamp(FacteurTailleVeau, 0.2f, 1f) : 1f;
	private float NormaliserGeneTaille()
	{
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		return Mathf.Clamp(Mathf.InverseLerp(min, max, _geneTaille), 0f, 1f);
	}
	private float FacteurTailleForce => Mathf.Lerp(0.72f, 1.62f, NormaliserGeneTaille());
	private float FacteurTailleConstitution => Mathf.Lerp(0.78f, 1.48f, NormaliserGeneTaille());
	private float FacteurTailleVitesse => Mathf.Lerp(1.35f, 0.72f, NormaliserGeneTaille());
	private float FacteurGeneVitesse => Mathf.Clamp(_geneVitesseDeplacement, 0.5f, 2f);
	private float ForceActuelle => ForceBase * MultiplicateurNiveau * FacteurTailleForce;
	private float ConstitutionActuelle => ConstitutionBase * MultiplicateurNiveau * FacteurTailleConstitution;
	private float VitesseStatActuelle => VitesseBase * MultiplicateurNiveau * FacteurTailleVitesse * FacteurGeneVitesse;

	public void Configurer(Gestionnaire_Monde gestionnaire, CharacterBody3D joueur, int seedTerrain, Vector3 ancreTroupeau)
	{
		_gestionnaire = gestionnaire;
		_gestionnaireFaune = GetParent() as GestionnaireFauneBoeufs;
		_joueur = joueur;
		_seedTerrain = seedTerrain;
		_ancreTroupeau = ancreTroupeau;
		_niveau = 1;
		_experience = 0f;
		_ageSecondes = 0f;
		_peutEsquiver = false;
		_peutAttaquer = false;
		_peutSuivre = false;
		_peutAider = false;
		_initialise = true;
		_diagnosticBlocageInitialisationDejaLogge = false;
		_diagnosticMortPersistanteDejaLogge = false;
		_framesDiagnosticSpawnRestantes = ActiverDiagnosticSpawnBovin
			? Mathf.Clamp(FramesDiagnosticSpawnBovin, 1, 120)
			: 0;
		MettreAJourStatsDerivees();
		EvaluerDeblocages();
		_faimCourante = _faimMaxActuelle;
		_staminaCourante = _staminaMaxActuelle;
		_vieCourante = _vieMaxActuelle;
		_cooldownRegenVie = Mathf.Max(1f, IntervalleRegenVieSecondes);
		MettreAJourAffichageFaim3D();
		ChoisirNouvelleCible(true);
	}

	public void DefinirGeneTaille(float gene)
	{
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		_geneTaille = Mathf.Clamp(gene, min, max);
		_geneTailleInitialise = true;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourStatsDerivees();
	}

	public void DefinirGenesNavigation(float prudence, float audaceSaut)
	{
		_genePrudenceNavigation = Mathf.Clamp(prudence, 0f, 1f);
		_geneAudaceSaut = Mathf.Clamp(audaceSaut, 0f, 1f);
		_genesNavigationInitialises = true;
	}

	public void DefinirGenesComportementSocial(float confiance, float reflexeFuite, float reflexeAttaque)
	{
		_geneConfiance = Mathf.Clamp(confiance, 0f, 1f);
		_geneReflexeFuite = Mathf.Clamp(reflexeFuite, 0f, 1f);
		_geneReflexeAttaque = Mathf.Clamp(reflexeAttaque, 0f, 1f);
		_genesComportementInitialises = true;
	}

	public void ConfigurerCommeVeau()
	{
		_estVeauActif = true;
		_ageSecondes = 0f;
		_tentativeReproductionJourEffectuee = true;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourStatsDerivees();
	}

	private void AssurerIdentifiantIndividu()
	{
		if (!string.IsNullOrWhiteSpace(_identifiantIndividu))
			return;
		_identifiantIndividu = Guid.NewGuid().ToString("N");
	}

	private void InitialiserGenesPersonnaliteSiNecessaire()
	{
		float minV = Mathf.Min(VitesseGeneMin, VitesseGeneMax);
		float maxV = Mathf.Max(VitesseGeneMin, VitesseGeneMax);
		if (_geneVitesseDeplacement <= 0.001f)
			_geneVitesseDeplacement = _rng.RandfRange(minV, maxV);
		else
			_geneVitesseDeplacement = Mathf.Clamp(_geneVitesseDeplacement, minV, maxV);
		_genePersonnalite = Mathf.Clamp(_genePersonnalite, 0f, 1f);
	}

	private void InitialiserGenesComportementSiNecessaire()
	{
		if (_genesComportementInitialises)
			return;
		_geneConfiance = Mathf.Clamp(_rng.RandfRange(0.32f, 0.72f), 0f, 1f);
		_geneReflexeFuite = Mathf.Clamp(_rng.RandfRange(0.35f, 0.78f), 0f, 1f);
		_geneReflexeAttaque = Mathf.Clamp(_rng.RandfRange(0.2f, 0.64f), 0f, 1f);
		_genesComportementInitialises = true;
	}

	public string ObtenirIdentifiantIndividu()
	{
		AssurerIdentifiantIndividu();
		return _identifiantIndividu;
	}

	public Godot.Collections.Dictionary ExtraireProfilPersistant()
	{
		AssurerIdentifiantIndividu();
		return new Godot.Collections.Dictionary
		{
			{ "id", _identifiantIndividu },
			{ "age", _ageSecondes },
			{ "niveau", _niveau },
			{ "experience", _experience },
			{ "faim", _faimCourante },
			{ "stamina", _staminaCourante },
			{ "vie", _vieCourante },
			{ "gene_taille", _geneTaille },
			{ "gene_vitesse", _geneVitesseDeplacement },
			{ "gene_personnalite", _genePersonnalite },
			{ "gene_confiance", _geneConfiance },
			{ "gene_fuite", _geneReflexeFuite },
			{ "gene_attaque", _geneReflexeAttaque },
			{ "gene_prudence_nav", _genePrudenceNavigation },
			{ "gene_audace_saut", _geneAudaceSaut },
			{ "score_navigation", _scoreNavigationEvolutif },
			{ "est_veau", _estVeauActif },
			{ "etat", (int)_etat },
			{ "cadavre_attend_depecage", _cadavreAttendDepecage },
			{ "cadavre_loot_distribue", _cadavreLootDistribue },
			{ "cadavre_coups_depecage", _coupsDepecageDagueValides },
			{ "x", GlobalPosition.X },
			{ "y", GlobalPosition.Y },
			{ "z", GlobalPosition.Z },
			{ "ancre_x", _ancreTroupeau.X },
			{ "ancre_y", _ancreTroupeau.Y },
			{ "ancre_z", _ancreTroupeau.Z }
		};
	}

	public void AppliquerProfilPersistant(Godot.Collections.Dictionary data)
	{
		if (data == null || data.Count == 0)
			return;
		if (data.TryGetValue("id", out Variant idv))
			_identifiantIndividu = idv.AsString();
		AssurerIdentifiantIndividu();
		if (data.TryGetValue("age", out Variant ageV))
			_ageSecondes = Mathf.Max(0f, ageV.AsSingle());
		if (data.TryGetValue("niveau", out Variant niveauV))
			_niveau = Mathf.Max(1, niveauV.AsInt32());
		if (data.TryGetValue("experience", out Variant xpV))
			_experience = Mathf.Max(0f, xpV.AsSingle());
		if (data.TryGetValue("gene_taille", out Variant gtV))
			DefinirGeneTaille(gtV.AsSingle());
		if (data.TryGetValue("gene_vitesse", out Variant gvV))
			_geneVitesseDeplacement = Mathf.Clamp(gvV.AsSingle(), Mathf.Min(VitesseGeneMin, VitesseGeneMax), Mathf.Max(VitesseGeneMin, VitesseGeneMax));
		if (data.TryGetValue("gene_personnalite", out Variant gpV))
			_genePersonnalite = Mathf.Clamp(gpV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("gene_confiance", out Variant gcV))
			_geneConfiance = Mathf.Clamp(gcV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("gene_fuite", out Variant gfV))
			_geneReflexeFuite = Mathf.Clamp(gfV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("gene_attaque", out Variant gaV))
			_geneReflexeAttaque = Mathf.Clamp(gaV.AsSingle(), 0f, 1f);
		_genesComportementInitialises = data.ContainsKey("gene_confiance") || data.ContainsKey("gene_fuite") || data.ContainsKey("gene_attaque");
		if (data.TryGetValue("gene_prudence_nav", out Variant gpnV) && data.TryGetValue("gene_audace_saut", out Variant gasV))
			DefinirGenesNavigation(gpnV.AsSingle(), gasV.AsSingle());
		if (data.TryGetValue("score_navigation", out Variant snV))
			_scoreNavigationEvolutif = Mathf.Clamp(snV.AsSingle(), 0f, 1f);
		if (data.TryGetValue("est_veau", out Variant veauV))
			_estVeauActif = veauV.AsBool();
		if (data.TryGetValue("faim", out Variant faimV))
			_faimCourante = Mathf.Max(0f, faimV.AsSingle());
		if (data.TryGetValue("stamina", out Variant stV))
			_staminaCourante = Mathf.Max(0f, stV.AsSingle());
		if (data.TryGetValue("vie", out Variant vieV))
			_vieCourante = Mathf.Max(0f, vieV.AsSingle());
		int etatSauvegarde = data.TryGetValue("etat", out Variant etatV)
			? etatV.AsInt32()
			: -1;
		bool lootDistribue = data.TryGetValue("cadavre_loot_distribue", out Variant lootV) && lootV.AsBool();
		bool attendDepecage = data.TryGetValue("cadavre_attend_depecage", out Variant attendV) ? attendV.AsBool() : true;
		int coupsDepecage = data.TryGetValue("cadavre_coups_depecage", out Variant coupsV) ? Mathf.Max(0, coupsV.AsInt32()) : 0;
		if (data.TryGetValue("ancre_x", out Variant ax) && data.TryGetValue("ancre_y", out Variant ay) && data.TryGetValue("ancre_z", out Variant az))
			_ancreTroupeau = new Vector3(ax.AsSingle(), ay.AsSingle(), az.AsSingle());
		MettreAJourStatsDerivees();
		bool etatMortSauvegarde = etatSauvegarde == (int)EtatBoeuf.Mort;
		if (etatMortSauvegarde || _vieCourante <= 0.0001f)
			RestaurerEtatMortPersistant(attendDepecage, lootDistribue, coupsDepecage);
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourAffichageFaim3D();
	}

	private void RestaurerEtatMortPersistant(bool attendDepecage, bool lootDistribue, int coupsDepecage)
	{
		_etat = EtatBoeuf.Mort;
		_vieCourante = 0f;
		Velocity = Vector3.Zero;
		_tempsMort = float.MaxValue;
		_cadavreLootDistribue = lootDistribue;
		_cadavreAttendDepecage = !lootDistribue && attendDepecage;
		_coupsDepecageDagueValides = Mathf.Max(0, coupsDepecage);
		if (ActiverDiagnosticSpawnBovin && !_diagnosticMortPersistanteDejaLogge)
		{
			_diagnosticMortPersistanteDejaLogge = true;
			GD.Print($"ZERO-K Faune [DiagSpawn] {Name}: profil persistant charge en cadavre (attendDepecage={_cadavreAttendDepecage}, lootDistribue={_cadavreLootDistribue}, coups={_coupsDepecageDagueValides}).");
		}
	}

	private void InitialiserGenesNavigationSiNecessaire()
	{
		if (_genesNavigationInitialises)
			return;
		_genePrudenceNavigation = Mathf.Clamp(_rng.RandfRange(0.38f, 0.66f), 0f, 1f);
		_geneAudaceSaut = Mathf.Clamp(_rng.RandfRange(0.34f, 0.62f), 0f, 1f);
		_scoreNavigationEvolutif = 0.5f;
		_genesNavigationInitialises = true;
	}

	private void AjusterScoreNavigation(float delta)
	{
		if (!ActiverApprentissageNavigation)
			return;
		float t = Mathf.Max(0.005f, TauxApprentissageNavigation);
		_scoreNavigationEvolutif = Mathf.Clamp(_scoreNavigationEvolutif + delta * t, 0f, 1f);
		_genePrudenceNavigation = Mathf.Clamp(_genePrudenceNavigation + (-delta * 0.35f) * t, 0f, 1f);
		_geneAudaceSaut = Mathf.Clamp(_geneAudaceSaut + (delta * 0.30f) * t, 0f, 1f);
	}

	/// <summary>
	/// Croisement SBX inspiré de jMetal/jMetalPy (GitHub), adapté à des gènes scalaires.
	/// Référence: https://github.com/jMetal/jMetalPy
	/// </summary>
	private (float enfant1, float enfant2) CroisementSBX(float parent1, float parent2, float borneMin, float borneMax, float eta)
	{
		float y1 = Mathf.Min(parent1, parent2);
		float y2 = Mathf.Max(parent1, parent2);
		float lb = Mathf.Min(borneMin, borneMax);
		float ub = Mathf.Max(borneMin, borneMax);
		eta = Mathf.Max(0.01f, eta);

		if (Mathf.Abs(y1 - y2) < 1e-6f)
			return (Mathf.Clamp(y1, lb, ub), Mathf.Clamp(y2, lb, ub));

		float rand = _rng.Randf();
		float beta1 = 1f + (2f * (y1 - lb) / (y2 - y1));
		float alpha1 = 2f - Mathf.Pow(beta1, -(eta + 1f));
		float betaq1 = rand <= 1f / alpha1
			? Mathf.Pow(rand * alpha1, 1f / (eta + 1f))
			: Mathf.Pow(1f / (2f - rand * alpha1), 1f / (eta + 1f));
		float c1 = 0.5f * (y1 + y2 - betaq1 * (y2 - y1));

		float beta2 = 1f + (2f * (ub - y2) / (y2 - y1));
		float alpha2 = 2f - Mathf.Pow(beta2, -(eta + 1f));
		float betaq2 = rand <= 1f / alpha2
			? Mathf.Pow(rand * alpha2, 1f / (eta + 1f))
			: Mathf.Pow(1f / (2f - rand * alpha2), 1f / (eta + 1f));
		float c2 = 0.5f * (y1 + y2 + betaq2 * (y2 - y1));

		c1 = Mathf.Clamp(c1, lb, ub);
		c2 = Mathf.Clamp(c2, lb, ub);
		if (_rng.Randf() < 0.5f)
			return (c2, c1);
		return (c1, c2);
	}

	/// <summary>
	/// Mutation polynomiale inspirée de jMetal/jMetalPy (GitHub).
	/// Référence: https://github.com/jMetal/jMetalPy
	/// </summary>
	private float MutationPolynomiale(float valeur, float borneMin, float borneMax, float eta, float probabilite)
	{
		float lb = Mathf.Min(borneMin, borneMax);
		float ub = Mathf.Max(borneMin, borneMax);
		float y = Mathf.Clamp(valeur, lb, ub);
		if (_rng.Randf() > Mathf.Clamp(probabilite, 0f, 1f) || ub - lb < 1e-8f)
			return y;

		eta = Mathf.Max(0.01f, eta);
		float delta1 = (y - lb) / (ub - lb);
		float delta2 = (ub - y) / (ub - lb);
		float rnd = _rng.Randf();
		float mutPow = 1f / (eta + 1f);
		float deltaq;

		if (rnd <= 0.5f)
		{
			float xy = 1f - delta1;
			float val = 2f * rnd + (1f - 2f * rnd) * Mathf.Pow(xy, eta + 1f);
			deltaq = Mathf.Pow(val, mutPow) - 1f;
		}
		else
		{
			float xy = 1f - delta2;
			float val = 2f * (1f - rnd) + 2f * (rnd - 0.5f) * Mathf.Pow(xy, eta + 1f);
			deltaq = 1f - Mathf.Pow(val, mutPow);
		}

		y += deltaq * (ub - lb);
		return Mathf.Clamp(y, lb, ub);
	}

	private void InitialiserGeneTailleSiNecessaire()
	{
		if (_geneTailleInitialise)
			return;
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		float maxGenerationInitiale = Mathf.Clamp(TailleGeneMaxGenerationInitiale, min, max);
		_geneTaille = _rng.RandfRange(min, maxGenerationInitiale);
		if (EstTaureau)
			_geneTaille = Mathf.Clamp(_geneTaille + 0.1f, min, maxGenerationInitiale);
		_geneTailleInitialise = true;
	}

	private void AppliquerGeneTailleVisuelleEtPhysique()
	{
		if (!_geneTailleInitialise)
			return;
		float min = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float max = Mathf.Max(TailleGeneMin, TailleGeneMax);
		_geneTaille = Mathf.Clamp(_geneTaille, min, max);
		if (_modeleVisuel != null)
		{
			Transform3D baseT = _transformModeleBase;
			baseT.Basis = baseT.Basis.Scaled(Vector3.One * TailleEffective);
			_modeleVisuel.Transform = baseT;
		}

		CollisionShape3D col = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col != null)
			AjusterHitboxDepuisModele(col);
	}

	private void AjusterHitboxDepuisModele(CollisionShape3D col)
	{
		if (!AjusterHitboxSurModele || !EssayerMesurerBoiteModele(out Vector3 minB, out Vector3 maxB))
		{
			if (col.Shape is not BoxShape3D boxFallback)
			{
				boxFallback = new BoxShape3D();
				col.Shape = boxFallback;
			}
			float taille = Mathf.Clamp(TailleEffective * Mathf.Clamp(MultiplicateurHitbox, 0.6f, 1.4f), 0.1f, 2.5f);
			boxFallback.Size = new Vector3(0.35f * taille, 0.7f * taille, 0.9f * taille);
			col.Position = new Vector3(0f, boxFallback.Size.Y * 0.5f, 0f);
			SynchroniserHitboxSecondairesDesactivees();
			return;
		}

		Vector3 size = maxB - minB;
		float mul = Mathf.Clamp(MultiplicateurHitbox, 0.6f, 1.4f);
		Vector3 centre = (minB + maxB) * 0.5f;
		Vector3 sizeMul = size * mul;

		if (col.Shape is not BoxShape3D box)
		{
			box = new BoxShape3D();
			col.Shape = box;
		}
		box.Size = new Vector3(
			Mathf.Clamp(sizeMul.X * 0.82f, 0.16f, 4.0f),
			Mathf.Clamp(sizeMul.Y * 0.82f, 0.22f, 4.0f),
			Mathf.Clamp(sizeMul.Z * 0.82f, 0.16f, 4.5f));
		col.Position = new Vector3(centre.X, centre.Y, centre.Z);

		if (!UtiliserHitboxComposite)
		{
			SynchroniserHitboxSecondairesDesactivees();
			return;
		}

		float dimLong = Mathf.Max(sizeMul.X, sizeMul.Z);
		float dimLarge = Mathf.Min(sizeMul.X, sizeMul.Z);
		float signeAvant = (_modeleVisuel != null && _modeleVisuel.Transform.Basis.Z.Z > 0f) ? -1f : 1f;

		_hitboxTete ??= ObtenirOuCreerCollisionShape("CollisionShape3D_Tete");
		if (_hitboxTete.Shape is not SphereShape3D sphereTete)
		{
			sphereTete = new SphereShape3D();
			_hitboxTete.Shape = sphereTete;
		}
		sphereTete.Radius = Mathf.Clamp(dimLarge * 0.24f * Mathf.Clamp(MultiplicateurHitboxTete, 0.4f, 1.6f), 0.08f, 0.9f);
		float offsetLongTete = dimLong * 0.34f * signeAvant;
		_hitboxTete.Position = new Vector3(
			centre.X + (sizeMul.X >= sizeMul.Z ? offsetLongTete : 0f),
			minB.Y + sizeMul.Y * 0.58f,
			centre.Z + (sizeMul.Z > sizeMul.X ? offsetLongTete : 0f));
		_hitboxTete.Disabled = false;

		_hitboxVentre ??= ObtenirOuCreerCollisionShape("CollisionShape3D_Ventre");
		if (_hitboxVentre.Shape is not SphereShape3D sphereVentre)
		{
			sphereVentre = new SphereShape3D();
			_hitboxVentre.Shape = sphereVentre;
		}
		sphereVentre.Radius = Mathf.Clamp(dimLarge * 0.28f * Mathf.Clamp(MultiplicateurHitboxVentre, 0.4f, 1.6f), 0.1f, 1.1f);
		_hitboxVentre.Position = new Vector3(centre.X, minB.Y + sizeMul.Y * 0.36f, centre.Z);
		_hitboxVentre.Disabled = false;
	}

	private CollisionShape3D ObtenirOuCreerCollisionShape(string nom)
	{
		CollisionShape3D n = GetNodeOrNull<CollisionShape3D>(nom);
		if (n != null)
			return n;
		n = new CollisionShape3D { Name = nom };
		AddChild(n);
		return n;
	}

	private void SynchroniserHitboxSecondairesDesactivees()
	{
		_hitboxTete ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D_Tete");
		_hitboxVentre ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D_Ventre");
		if (_hitboxTete != null) _hitboxTete.Disabled = true;
		if (_hitboxVentre != null) _hitboxVentre.Disabled = true;
	}

	private bool EssayerMesurerBoiteModele(out Vector3 minB, out Vector3 maxB)
	{
		minB = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		maxB = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		if (_modeleVisuel == null || !GodotObject.IsInstanceValid(_modeleVisuel))
			return false;

		bool touche = false;
		AccumulerBoiteModeleRecursif(_modeleVisuel, _modeleVisuel.Transform, ref minB, ref maxB, ref touche);
		return touche;
	}

	private void AccumulerBoiteModeleRecursif(Node node, Transform3D toBody, ref Vector3 minB, ref Vector3 maxB, ref bool touche)
	{
		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb aabb = mi.Mesh.GetAabb();
			Vector3[] coins =
			{
				new Vector3(aabb.Position.X, aabb.Position.Y, aabb.Position.Z),
				new Vector3(aabb.End.X, aabb.Position.Y, aabb.Position.Z),
				new Vector3(aabb.Position.X, aabb.End.Y, aabb.Position.Z),
				new Vector3(aabb.Position.X, aabb.Position.Y, aabb.End.Z),
				new Vector3(aabb.End.X, aabb.End.Y, aabb.Position.Z),
				new Vector3(aabb.End.X, aabb.Position.Y, aabb.End.Z),
				new Vector3(aabb.Position.X, aabb.End.Y, aabb.End.Z),
				new Vector3(aabb.End.X, aabb.End.Y, aabb.End.Z),
			};

			foreach (Vector3 c in coins)
			{
				Vector3 p = toBody * c;
				minB = new Vector3(Mathf.Min(minB.X, p.X), Mathf.Min(minB.Y, p.Y), Mathf.Min(minB.Z, p.Z));
				maxB = new Vector3(Mathf.Max(maxB.X, p.X), Mathf.Max(maxB.Y, p.Y), Mathf.Max(maxB.Z, p.Z));
			}
			touche = true;
		}

		foreach (Node enfant in node.GetChildren())
		{
			if (enfant is not Node3D n3) continue;
			AccumulerBoiteModeleRecursif(n3, toBody * n3.Transform, ref minB, ref maxB, ref touche);
		}
	}

	private void InitialiserAffichageFaim3D()
	{
		if (!AfficherFaimAuDessusBovin)
			return;
		_labelFaim3D = GetNodeOrNull<Label3D>("UI_Faim");
		if (_labelFaim3D != null)
			return;

		_labelFaim3D = new Label3D
		{
			Name = "UI_Faim",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = false,
			FontSize = 36,
			OutlineSize = 8,
			PixelSize = 0.0026f,
			Modulate = new Color(0.76f, 1f, 0.76f, 1f)
		};
		AddChild(_labelFaim3D);
	}

	private void AssurerBarresUIDessusTete()
	{
		InitialiserAffichageFaim3D();
		InitialiserAffichageStamina3D();
		InitialiserAffichageVie3D();
		SupprimerLabelsUIDessusTeteSiDesactives();
	}

	/// <summary>Retire les labels 3D si les exports sont faux (ex. nœuds laissés dans la scène pour le debug).</summary>
	private void SupprimerLabelsUIDessusTeteSiDesactives()
	{
		if (!AfficherFaimAuDessusBovin)
		{
			Label3D f = GetNodeOrNull<Label3D>("UI_Faim");
			if (f != null && GodotObject.IsInstanceValid(f))
				f.QueueFree();
			_labelFaim3D = null;
		}
		if (!AfficherStaminaAuDessusBovin)
		{
			Label3D s = GetNodeOrNull<Label3D>("UI_Stamina");
			if (s != null && GodotObject.IsInstanceValid(s))
				s.QueueFree();
			_labelStamina3D = null;
		}
		if (!AfficherVieAuDessusBovin)
		{
			Label3D v = GetNodeOrNull<Label3D>("UI_Vie");
			if (v != null && GodotObject.IsInstanceValid(v))
				v.QueueFree();
			_labelVie3D = null;
		}
	}

	private void InitialiserAffichageStamina3D()
	{
		if (!AfficherStaminaAuDessusBovin)
			return;
		_labelStamina3D = GetNodeOrNull<Label3D>("UI_Stamina");
		if (_labelStamina3D != null)
			return;

		_labelStamina3D = new Label3D
		{
			Name = "UI_Stamina",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = false,
			FontSize = 34,
			OutlineSize = 8,
			PixelSize = 0.0024f,
			Modulate = new Color(0.48f, 0.79f, 1f, 1f)
		};
		AddChild(_labelStamina3D);
	}

	private void InitialiserAffichageVie3D()
	{
		if (!AfficherVieAuDessusBovin)
			return;
		_labelVie3D = GetNodeOrNull<Label3D>("UI_Vie");
		if (_labelVie3D != null)
			return;

		_labelVie3D = new Label3D
		{
			Name = "UI_Vie",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = false,
			FontSize = 34,
			OutlineSize = 8,
			PixelSize = 0.0024f,
			Modulate = new Color(1f, 0.35f, 0.35f, 1f)
		};
		AddChild(_labelVie3D);
	}

	private string ConstruireBarreRatio(float ratio, int segments = 10)
	{
		segments = Mathf.Clamp(segments, 4, 20);
		int pleins = Mathf.Clamp(Mathf.RoundToInt(ratio * segments), 0, segments);
		if (!_cacheBarresRatio.TryGetValue(segments, out var cacheSegment))
		{
			cacheSegment = new string[segments + 1];
			for (int i = 0; i <= segments; i++)
				cacheSegment[i] = "[" + new string('|', i) + new string('.', segments - i) + "]";
			_cacheBarresRatio[segments] = cacheSegment;
		}
		return cacheSegment[pleins];
	}

	private void MettreAJourAffichageStamina3D()
	{
		if (!AfficherStaminaAuDessusBovin || _labelStamina3D == null || !GodotObject.IsInstanceValid(_labelStamina3D))
		{
			MettreAJourAffichageVie3D();
			return;
		}
		float ratio = RatioStaminaCourant();
		int pct = Mathf.RoundToInt(ratio * 100f);
		_labelStamina3D.Text = $"Stamina {pct}% {ConstruireBarreRatio(ratio, 10)}";
		float baseY = Mathf.Max(0.6f, HauteurAffichageFaim * _geneTaille);
		_labelStamina3D.Position = new Vector3(0f, baseY + Mathf.Max(0.08f, DecalageVerticalBarreStamina), 0f);
		if (ratio <= 0.20f)
			_labelStamina3D.Modulate = new Color(1f, 0.39f, 0.39f, 1f);
		else if (ratio <= 0.50f)
			_labelStamina3D.Modulate = new Color(1f, 0.87f, 0.37f, 1f);
		else
			_labelStamina3D.Modulate = new Color(0.48f, 0.79f, 1f, 1f);
		MettreAJourAffichageVie3D();
	}

	private void MettreAJourAffichageVie3D()
	{
		if (!AfficherVieAuDessusBovin || _labelVie3D == null || !GodotObject.IsInstanceValid(_labelVie3D))
			return;
		float ratio = _vieMaxActuelle > 0.001f ? Mathf.Clamp(_vieCourante / _vieMaxActuelle, 0f, 1f) : 0f;
		int pct = Mathf.RoundToInt(ratio * 100f);
		_labelVie3D.Text = $"Vie {pct}% {ConstruireBarreRatio(ratio, 10)}";
		float baseY = Mathf.Max(0.6f, HauteurAffichageFaim * _geneTaille);
		float yStamina = baseY + Mathf.Max(0.08f, DecalageVerticalBarreStamina);
		_labelVie3D.Position = new Vector3(0f, yStamina + Mathf.Max(0.08f, DecalageVerticalBarreVie), 0f);
		if (ratio <= 0.20f)
			_labelVie3D.Modulate = new Color(1f, 0.28f, 0.28f, 1f);
		else if (ratio <= 0.50f)
			_labelVie3D.Modulate = new Color(1f, 0.76f, 0.32f, 1f);
		else
			_labelVie3D.Modulate = new Color(0.48f, 1f, 0.48f, 1f);
	}

	private void MettreAJourAffichageFaim3D()
	{
		if (!AfficherFaimAuDessusBovin || _labelFaim3D == null || !GodotObject.IsInstanceValid(_labelFaim3D))
		{
			MettreAJourAffichageStamina3D();
			return;
		}

		float ratio = _faimMaxActuelle > 0.001f ? Mathf.Clamp(_faimCourante / _faimMaxActuelle, 0f, 1f) : 0f;
		int pct = Mathf.RoundToInt(ratio * 100f);
		string infoTroupeau = "";
		if (_deblocageAffichageTroupeau)
		{
			ulong now = Time.GetTicksMsec();
			ulong intervalle = (ulong)Mathf.Clamp(IntervalleMajCohesionUiSec * 1000f, 50f, 1000f);
			if (_tickDerniereMajCohesionUi == 0 || now - _tickDerniereMajCohesionUi >= intervalle)
			{
				_cohesionUiCachee = CalculerRatioCohesionTroupeau();
				_tickDerniereMajCohesionUi = now;
			}
			int cohesion = Mathf.RoundToInt(_cohesionUiCachee * 100f);
			infoTroupeau = $" | Troupe {cohesion}%";
		}
		_labelFaim3D.Text = $"Faim {pct}%{infoTroupeau}";
		_labelFaim3D.Position = new Vector3(0f, Mathf.Max(0.6f, HauteurAffichageFaim * TailleEffective), 0f);

		if (ratio <= 0.25f)
			_labelFaim3D.Modulate = new Color(1f, 0.34f, 0.34f, 1f);
		else if (ratio <= 0.50f)
			_labelFaim3D.Modulate = new Color(1f, 0.86f, 0.34f, 1f);
		else
			_labelFaim3D.Modulate = new Color(0.76f, 1f, 0.76f, 1f);
		MettreAJourAffichageStamina3D();
	}

	private void EssayerAbonnementNouveauJour()
	{
		if (_abonneNouveauJour)
			return;
		Node scene = GetTree()?.CurrentScene;
		if (scene == null)
			return;
		_cycleSolaire = scene.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (_cycleSolaire == null)
			return;
		_cycleSolaire.Connect(Cycle_Solaire.SignalName.NouveauJour, Callable.From(SurNouveauJourFaune));
		_abonneNouveauJour = true;
	}

	private void SurNouveauJourFaune()
	{
		if (_etat == EtatBoeuf.Mort)
			return;

		if (MonterNiveauParNouveauJour)
		{
			_niveau = Mathf.Max(1, _niveau + 1);
			MettreAJourStatsDerivees();
			EvaluerDeblocages();
			EmitSignal(SignalName.EvolutionEvenement, "niveau_journalier", 1f, _niveau, _ageSecondes / 3600f);
		}

		_tentativeReproductionJourEffectuee = false;
		if (ActiverReproductionFaune)
			TenterConceptionJournaliereSelective();
	}

	public override void _ExitTree()
	{
		if (_abonneNouveauJour && _cycleSolaire != null && GodotObject.IsInstanceValid(_cycleSolaire))
		{
			var cb = Callable.From(SurNouveauJourFaune);
			if (_cycleSolaire.IsConnected(Cycle_Solaire.SignalName.NouveauJour, cb))
				_cycleSolaire.Disconnect(Cycle_Solaire.SignalName.NouveauJour, cb);
		}
		_abonneNouveauJour = false;
		_cycleSolaire = null;
		base._ExitTree();
	}

	public override void _Ready()
	{
		_rng.Randomize();
		_cooldownChoixCible = 0f;
		_cooldownControleSol = 0.2f;
		_cooldownAntiBlocage = 0.5f;
		_cooldownEnjambementObstacle = 0f;
		_cooldownEvaluationVisionTerrain = 0f;
		_cooldownSautStrategique = 0f;
		_cooldownVerificationVisionJoueur = 0f;
		_memoireDetectionJoueur = 0f;
		_cooldownReproduction = _rng.RandfRange(2f, 9f);
		_timerDetectionCoincage = 0f;
		_tempsGestationRestant = 0f;
		_estEnGestation = false;
		_tentativeReproductionJourEffectuee = false;
		_maleGestationReference = null;
		_echecsMorsureConsecutifs = 0;
		_verrouMouvementMorsure = 0f;
		_tempsIdleErrance = 0f;
		_streakCoincage = 0;
		_positionReferenceCoincage = GlobalPosition;
		_positionDernierSaut = GlobalPosition;
		_biaisEvitementTerrain = 0f;
		_cooldownAge = IntervalleCycleAgeSecondes;
		_cooldownRegenVie = Mathf.Max(1f, IntervalleRegenVieSecondes);
		_cooldownVerificationBarresUI = 0f;
		_cooldownEvaluationEnvironnement = _rng.RandfRange(8f, Mathf.Max(9f, IntervalleEvaluationEnvironnementSecondes));
		_cooldownDirectionNage = 0f;
		_cooldownTickCerveau = _rng.RandfRange(0.01f, 0.09f);
		_dtAccumuleTickCerveau = 0f;
		_cooldownCohesionAnimation = _rng.RandfRange(0.05f, 0.22f);
		_cohesionAnimationCache = 0f;
		_cooldownImpactChargeJoueur = 0f;
		_flashRougeDegatsRestant = 0f;
		_directionNageEau = Vector3.Zero;
		FloorSnapLength = Mathf.Max(0.05f, LongueurStepAssist);
		_modeleVisuel = GetNodeOrNull<Node3D>("Modele");
		if (_modeleVisuel != null)
			_transformModeleBase = _modeleVisuel.Transform;
		AssurerIdentifiantIndividu();
		InitialiserGenesPersonnaliteSiNecessaire();
		InitialiserGenesComportementSiNecessaire();
		InitialiserGenesNavigationSiNecessaire();
		InitialiserGeneTailleSiNecessaire();
		AppliquerGeneTailleVisuelleEtPhysique();
		InitialiserAffichageFaim3D();
		InitialiserAffichageStamina3D();
		InitialiserAffichageVie3D();
		SupprimerLabelsUIDessusTeteSiDesactives();
		MettreAJourAffichageFaim3D();
		CallDeferred(nameof(EssayerAbonnementNouveauJour));
		if (_modeleVisuel != null && Mathf.Abs(CorrectionOrientationModeleDegres) > 0.001f)
		{
			Basis correction = Basis.FromEuler(new Vector3(0f, Mathf.DegToRad(CorrectionOrientationModeleDegres), 0f));
			Transform3D t = _modeleVisuel.Transform;
			_modeleVisuel.Transform = new Transform3D(correction * t.Basis, t.Origin);
		}
		InitialiserAnimations();
		InitialiserAudioCombat();
		StabiliserMateriauxBoeuf();
	}

	private void InitialiserAudioCombat()
	{
		_audioCriDegats = GetNodeOrNull<AudioStreamPlayer3D>("AudioCriDegats");
		if (_audioCriDegats == null)
		{
			_audioCriDegats = new AudioStreamPlayer3D { Name = "AudioCriDegats" };
			AddChild(_audioCriDegats);
		}
		_audioCriDegats.MaxDistance = 36f;
		_audioCriDegats.UnitSize = 1f;
		_audioCriDegats.VolumeDb = VolumeCriDegatsDb;
		if (_audioCriDegats.Stream == null && !string.IsNullOrWhiteSpace(CheminSonCriDegats))
		{
			AudioStream stream = GD.Load<AudioStream>(CheminSonCriDegats);
			if (stream != null)
				_audioCriDegats.Stream = stream;
		}
	}

	private void JouerCriDegats(float degats)
	{
		if (_audioCriDegats == null || !GodotObject.IsInstanceValid(_audioCriDegats) || _audioCriDegats.Stream == null)
			return;
		double maintenant = Time.GetTicksMsec() / 1000.0;
		if ((maintenant - _horodatageDernierCriDegats) < Mathf.Max(0.05f, CooldownCriDegatsSecondes))
			return;
		_horodatageDernierCriDegats = maintenant;
		_audioCriDegats.PitchScale = _rng.RandfRange(0.94f, 1.06f);
		_audioCriDegats.VolumeDb = VolumeCriDegatsDb + Mathf.Clamp((degats - 1.2f) * 0.09f, -1.5f, 2.4f);
		_audioCriDegats.Play();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_initialise || _gestionnaire == null)
		{
			if (ActiverDiagnosticSpawnBovin && !_diagnosticBlocageInitialisationDejaLogge)
			{
				_diagnosticBlocageInitialisationDejaLogge = true;
				GD.Print($"ZERO-K Faune [DiagSpawn] {Name}: tick ignore car initialisation incomplete (initialise={_initialise}, gestionnaireNull={_gestionnaire == null}).");
			}
			return;
		}
		ulong debutFrameUs = ActiverProfilagePerfBovin ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownDrainProfilage += (float)delta;
		float dt = (float)delta;

		if (_etat == EtatBoeuf.Mort)
		{
			GererMort(dt);
			if (ActiverProfilagePerfBovin)
				PerfBudgetMonitor.End("Faune/BovinFrame", debutFrameUs);
			return;
		}
		_fenetreAnimSautStrategique = Mathf.Max(0f, _fenetreAnimSautStrategique - dt);
		MettreAJourFlashDegatsVisuel(dt);

		MettreAJourAgeEtEvolution(dt);
		float facteurGestation = (_estEnGestation && EstFemelle) ? Mathf.Max(1f, MultiplicateurFaimGestation) : 1f;
		float drainFaim = FaimParSeconde * facteurGestation * (ConstitutionBase / Mathf.Max(0.1f, ConstitutionActuelle));
		_faimCourante = Mathf.Max(0f, _faimCourante - drainFaim * dt);

		_cooldownChoixCible -= dt;
		_cooldownControleSol -= dt;
		_cooldownAntiBlocage -= dt;
		_cooldownEnjambementObstacle -= dt;
		_cooldownEvaluationVisionTerrain -= dt;
		_cooldownSautStrategique -= dt;
		_cooldownVerificationVisionJoueur -= dt;
		_memoireDetectionJoueur -= dt;
		_cooldownReproduction -= dt;
		_cooldownVerificationBarresUI -= dt;
		_cooldownEvaluationEnvironnement -= dt;
		_cooldownVariationAnimation -= dt;
		_cooldownCohesionAnimation -= dt;
		_cooldownImpactChargeJoueur -= dt;
		_cooldownReconfigurationArbreAnimation = Mathf.Max(0f, _cooldownReconfigurationArbreAnimation - dt);
		_verrouMouvementMorsure = Mathf.Max(0f, _verrouMouvementMorsure - dt);
		_tempsIdleErrance = Mathf.Max(0f, _tempsIdleErrance - dt);
		_tempsFuite = Mathf.Max(0f, _tempsFuite - dt);
		_tempsCharge = Mathf.Max(0f, _tempsCharge - dt);
		if (_estEnGestation)
			_tempsGestationRestant = Mathf.Max(0f, _tempsGestationRestant - dt);
		bool enEffortIntense = _etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge;
		RegenererStamina(dt, enEffortIntense);
		GererRegenerationVie(dt);
		GererDegatsFamine(dt);
		if (_etat == EtatBoeuf.Mort)
		{
			if (ActiverProfilagePerfBovin)
				PerfBudgetMonitor.End("Faune/BovinFrame", debutFrameUs);
			return;
		}
		if (_cooldownVerificationBarresUI <= 0f)
		{
			_cooldownVerificationBarresUI = 1.0f;
			AssurerBarresUIDessusTete();
		}
		MettreAJourAffichageFaim3D();
		if (_cooldownEvaluationEnvironnement <= 0f)
		{
			_cooldownEvaluationEnvironnement = Mathf.Max(30f, IntervalleEvaluationEnvironnementSecondes);
			EvaluerAdaptationComportementaleSelonEnvironnement();
		}
		float vitesseHorizActuelle = Mathf.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
		bool contexteCalmePourVariation = _etat != EtatBoeuf.Fuite && _etat != EtatBoeuf.Charge && vitesseHorizActuelle <= 0.18f;
		if (ActiverSelectionEvolutionnaireAnimations && _cooldownVariationAnimation <= 0f && contexteCalmePourVariation)
		{
			AppliquerSelectionAnimationEvolutive(forceReconfigurerArbre: UtiliserAnimationTreeLocomotion);
			_cooldownVariationAnimation = Mathf.Max(2f, IntervalleVariationAnimationSecondes) * _rng.RandfRange(0.72f, 1.28f);
		}

		if (_etat == EtatBoeuf.Fuite)
			AjouterExperience(ExperienceFuiteParSeconde * dt, "fuite");

		if (_cooldownControleSol <= 0f)
		{
			_cooldownControleSol = 0.35f;
			SecuriserPositionSol();
		}

		_dtAccumuleTickCerveau += dt;
		_cooldownTickCerveau -= dt;
		if (_cooldownTickCerveau <= 0f)
		{
			ulong debutCerveauUs = ActiverProfilagePerfBovin ? PerfBudgetMonitor.Begin() : 0UL;
			float dtCerveau = Mathf.Max(0.005f, _dtAccumuleTickCerveau);
			_dtAccumuleTickCerveau = 0f;
			float intervalle = CalculerIntervalleTickCerveau();
			_cooldownTickCerveau = intervalle * _rng.RandfRange(0.92f, 1.08f);
			GererPresenceJoueur();
			GererReproductionEtGestation();
			GererEtatEtCible(dtCerveau);
			if (ActiverProfilagePerfBovin)
				PerfBudgetMonitor.End("Faune/BovinCerveau", debutCerveauUs);
		}

		Vector3 direction = (_cibleCourante - GlobalPosition);
		direction.Y = 0f;
		direction = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Zero;
		_dansEau = EstDansEau();
		if (_dansEau)
			direction = CalculerDirectionNage(direction, dt);
		bool demandeSautStrategique = false;
		if (!_dansEau)
		{
			direction = AjusterDirectionAntiObstacle(direction);
			direction = AdapterStrategieTerrain(direction, dt, ref demandeSautStrategique);
			EvaluerCoincageEtDeblocage(dt, direction, ref demandeSautStrategique);
		}
		if (!demandeSautStrategique && DoitTenterSautEscalade(direction))
			demandeSautStrategique = true;
		_directionDeplacementHorizontale = direction;

		float vitesseMarcheActuelle = VitesseMarche * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
		float vitesseFuiteActuelle = VitesseFuite * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
		bool veutCourir = _etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge;
		bool fatigue = RatioStaminaCourant() <= Mathf.Clamp(SeuilFatigueCourse, 0f, 1f);
		bool staminaSprintOk = SprintAutoriseParStamina();
		bool peutCourir = veutCourir && !fatigue && staminaSprintOk && EssayerDepenserStamina(CoutStaminaCourseParSeconde * dt);
		float seuilBroutageMobile = Mathf.Max(1.0f, RayonMangerHerbe * 0.9f);
		float seuilBroutageMobile2 = seuilBroutageMobile * seuilBroutageMobile;
		bool broutageMobile = _etat == EtatBoeuf.Broutage && GlobalPosition.DistanceSquaredTo(_cibleCourante) > seuilBroutageMobile2;
		float vitesseCible = peutCourir ? vitesseFuiteActuelle : ((_etat == EtatBoeuf.Broutage && !broutageMobile) ? 0f : vitesseMarcheActuelle);
		if (_verrouMouvementMorsure > 0f)
			vitesseCible = 0f;
		Vector3 vHoriz = new Vector3(Velocity.X, 0f, Velocity.Z);
		Vector3 vCible = direction * vitesseCible;
		float facteur = direction == Vector3.Zero ? FreinageHorizontal : AccelerationHorizontale;
		vHoriz = vHoriz.Lerp(vCible, Mathf.Clamp(facteur * dt, 0f, 1f));
		FloorSnapLength = _dansEau ? 0f : Mathf.Max(0.05f, LongueurStepAssist);

		float vy = Velocity.Y;
		if (_dansEau)
		{
			bool staminaNageOk = _staminaCourante > 0.35f;
			AppliquerPhysiqueNatation(dt, ref vHoriz, ref vy, staminaNageOk, _eauIntentionRemonter);
		}
		else
		{
			if (!IsOnFloor())
				vy -= ForceGravite * dt;
			else if (vy < 0f)
				vy = -0.3f;
		}
		if (!_dansEau && _verrouMouvementMorsure <= 0f && demandeSautStrategique && ActiverSautStrategique && IsOnFloor() && _cooldownSautStrategique <= 0f && EssayerDepenserStamina(CoutStaminaSaut))
		{
			float facteurSautEvolutif = Mathf.Lerp(0.9f, 1.2f, _geneAudaceSaut);
			vy = Mathf.Max(vy, ImpulsionSautStrategique * Mathf.Max(1f, MultiplicateurHauteurSaut) * facteurSautEvolutif);
			if (direction.LengthSquared() > 0.0001f)
			{
				// Saut d'escalade avec elan avant: conserve l'intention de progression.
				float impulsionAvant = Mathf.Max(vitesseMarcheActuelle * 0.55f, 2.2f);
				vHoriz += direction.Normalized() * impulsionAvant;
				float vmax = Mathf.Max(vitesseFuiteActuelle, vitesseMarcheActuelle) * 1.08f;
				if (vHoriz.Length() > vmax)
					vHoriz = vHoriz.Normalized() * vmax;
			}
			_cooldownSautStrategique = Mathf.Max(0.1f, CooldownSautStrategique);
			_positionDernierSaut = GlobalPosition;
			_fenetreAnimSautStrategique = 0.38f;
		}

		Velocity = new Vector3(vHoriz.X, vy, vHoriz.Z);
		MoveAndSlide();
		if (ActiverDiagnosticSpawnBovin && _framesDiagnosticSpawnRestantes > 0)
		{
			_framesDiagnosticSpawnRestantes--;
			Vector3 v = Velocity;
			GD.Print($"ZERO-K Faune [DiagSpawn] {Name}: etat={_etat}, onFloor={IsOnFloor()}, vel=({v.X:F2},{v.Y:F2},{v.Z:F2}), pos=({GlobalPosition.X:F2},{GlobalPosition.Y:F2},{GlobalPosition.Z:F2}).");
		}
		if (!_dansEau
			&& _cooldownEnjambementObstacle <= 0f
			&& _verrouMouvementMorsure <= 0f
			&& _etat != EtatBoeuf.Mort)
		{
			bool enjambement = StepAssistService.TryApplyStepAssist(
				this,
				new Vector3(Velocity.X, 0f, Velocity.Z),
				dt,
				HauteurMaxEnjambementObstacle,
				DistanceAvantEnjambementObstacle,
				VitesseMinEnjambementObstacle,
				NormalYMinSolEnjambementObstacle,
				NormalYMaxObstacleEnjambement);
			if (enjambement)
				_cooldownEnjambementObstacle = Mathf.Max(0.01f, CooldownEnjambementObstacleSec);
		}

		float vitesseHoriz = new Vector3(Velocity.X, 0f, Velocity.Z).Length();
		MettreAJourApprentissageNavigation(dt, direction, vitesseHoriz);
		MettreAJourCycleIdleMultiples(dt, vitesseHorizActuelle);
		MettreAJourAnimation(dt, vitesseHoriz);
		if (EstFallbackLocomotionBobSeulement())
			AppliquerLocomotionSquelettiqueProcedural(dt, vitesseHoriz);
		OrienteCorpsVersDirectionDeplacement(dt, vitesseHoriz);
		if (_reconfigurationArbreAnimationEnAttente
			&& _cooldownReconfigurationArbreAnimation <= 0f
			&& UtiliserAnimationTreeLocomotion)
		{
			_reconfigurationArbreAnimationEnAttente = false;
			ConfigurerAnimationTreeFaune();
			_cooldownReconfigurationArbreAnimation = Mathf.Max(0.05f, CooldownReconfigurationAnimationTreeSec);
		}
		if (ActiverProfilagePerfBovin)
		{
			PerfBudgetMonitor.End("Faune/BovinFrame", debutFrameUs);
			if (_cooldownDrainProfilage >= Mathf.Max(0.2f, IntervalleLogProfilageBovinSec))
			{
				_cooldownDrainProfilage = 0f;
				PerfBudgetMonitor.FlushSiEchu("Faune", IntervalleLogProfilageBovinSec);
			}
		}
	}

	private float CalculerIntervalleTickCerveau()
	{
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge || _memoireDetectionJoueur > 0f)
			return 0.05f; // Réactivité maximale sous stress/combat.

		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return 0.18f;

		Vector3 d = _joueur.GlobalPosition - GlobalPosition;
		d.Y = 0f;
		float dist2 = d.LengthSquared();
		if (dist2 <= 20f * 20f) return 0.07f;
		if (dist2 <= 45f * 45f) return 0.11f;
		return 0.18f;
	}

	private void OrienteCorpsVersDirectionDeplacement(float dt, float vitesseHoriz)
	{
		Vector3 dir = Vector3.Zero;
		if (PrefererDirectionCiblePourLOrientation && _directionDeplacementHorizontale.LengthSquared() > 0.0001f)
			dir = _directionDeplacementHorizontale;
		else if (vitesseHoriz >= 0.08f)
			dir = new Vector3(Velocity.X, 0f, Velocity.Z).Normalized();
		if (dir.LengthSquared() < 0.0001f)
			return;
		// Axe -Z du corps vers la direction (convention Godot 3D : avant = -Z local).
		float yawCible = Mathf.Atan2(-dir.X, -dir.Z) + Mathf.DegToRad(CorrectionYawRegardDegres);
		float k = 1f - Mathf.Exp(-Mathf.Max(0.5f, VitesseOrientationCorps) * dt);
		float yaw = Mathf.LerpAngle(Rotation.Y, yawCible, Mathf.Clamp(k, 0f, 1f));
		Rotation = new Vector3(Rotation.X, yaw, Rotation.Z);
	}

	private void MettreAJourAgeEtEvolution(float dt)
	{
		_ageSecondes += dt;
		_cooldownAge -= dt;
		if (_cooldownAge <= 0f)
		{
			_cooldownAge += Mathf.Max(5f, IntervalleCycleAgeSecondes);
			AjouterExperience(ExperienceCycleAge, "vieillissement");
		}
	}

	private void AjouterExperience(float quantite, string typeEvenement)
	{
		if (quantite <= 0f) return;
		_experience += quantite;
		EmitSignal(SignalName.EvolutionEvenement, typeEvenement, quantite, _niveau, _ageSecondes / 3600f);
		if (!AutoriserNiveauxParExperience)
			return;
		while (_experience >= ExperienceParNiveau)
		{
			_experience -= ExperienceParNiveau;
			_niveau++;
			MettreAJourStatsDerivees();
			EvaluerDeblocages();
			EmitSignal(SignalName.EvolutionEvenement, "niveau_plus", 1f, _niveau, _ageSecondes / 3600f);
		}
	}

	private void MettreAJourStatsDerivees()
	{
		_faimMaxActuelle = FaimMax * (ConstitutionActuelle / Mathf.Max(0.1f, ConstitutionBase));
		_faimCourante = Mathf.Clamp(_faimCourante, 0f, _faimMaxActuelle);
		_staminaMaxActuelle = StaminaMax * (ConstitutionActuelle / Mathf.Max(0.1f, ConstitutionBase));
		_staminaCourante = Mathf.Clamp(_staminaCourante, 0f, _staminaMaxActuelle);
		_vieMaxActuelle = VieMax * (ConstitutionActuelle / Mathf.Max(0.1f, ConstitutionBase));
		_vieCourante = Mathf.Clamp(_vieCourante, 0f, _vieMaxActuelle);
	}

	private void EvaluerDeblocages()
	{
		VerifierDeblocage(ref _peutEsquiver, _niveau >= 3, "deblocage_esquive");
		VerifierDeblocage(ref _peutAttaquer, _niveau >= 4, "deblocage_charge");
		VerifierDeblocage(ref _peutSuivre, _niveau >= 5, "deblocage_suivi");
		VerifierDeblocage(ref _peutAider, _niveau >= 7, "deblocage_aide");
	}

	private void VerifierDeblocage(ref bool flag, bool condition, string evenement)
	{
		if (flag || !condition) return;
		flag = true;
		EmitSignal(SignalName.EvolutionEvenement, evenement, 1f, _niveau, _ageSecondes / 3600f);
	}

	private float RatioFaimCourant()
	{
		if (_faimMaxActuelle <= 0.001f)
			return 0f;
		return Mathf.Clamp(_faimCourante / _faimMaxActuelle, 0f, 1f);
	}

	private float RatioStaminaCourant()
	{
		if (_staminaMaxActuelle <= 0.001f)
			return 0f;
		return Mathf.Clamp(_staminaCourante / _staminaMaxActuelle, 0f, 1f);
	}

	private bool SprintAutoriseParStamina()
	{
		float seuilMini = Mathf.Max(0.05f, CoutStaminaCourseParSeconde * 0.05f);
		return _staminaCourante > seuilMini;
	}

	private bool EssayerDepenserStamina(float cout)
	{
		float c = Mathf.Max(0f, cout);
		if (c <= 0f)
			return true;
		if (_staminaCourante < c)
			return false;
		_staminaCourante = Mathf.Max(0f, _staminaCourante - c);
		MettreAJourAffichageFaim3D();
		return true;
	}

	private void RegenererStamina(float dt, bool enEffortIntense)
	{
		if (_staminaCourante >= _staminaMaxActuelle - 0.001f)
			return;
		float regenBase = Mathf.Max(0f, RegenerationStaminaParSeconde) * dt;
		if (regenBase <= 0.0001f)
			return;
		float facteur = enEffortIntense ? 0.25f : 1f;
		float regenPotentielle = regenBase * facteur;
		float manque = Mathf.Max(0f, _staminaMaxActuelle - _staminaCourante);
		float regen = Mathf.Min(manque, regenPotentielle);
		if (regen <= 0.0001f)
			return;

		float coutFaim = regen * Mathf.Max(0f, CoutFaimParPointStaminaRegen);
		if (coutFaim > 0f)
		{
			float ratioPossible = _faimCourante <= 0.0001f ? 0f : Mathf.Clamp(_faimCourante / coutFaim, 0f, 1f);
			regen *= ratioPossible;
			coutFaim *= ratioPossible;
		}
		if (regen <= 0.0001f)
			return;

		_staminaCourante = Mathf.Min(_staminaMaxActuelle, _staminaCourante + regen);
		_faimCourante = Mathf.Max(0f, _faimCourante - coutFaim);
	}

	private void GererRegenerationVie(float dt)
	{
		if (_etat == EtatBoeuf.Mort)
			return;
		if (_faimCourante <= 0.0001f)
			return; // Pas de regen vie en famine totale.
		_cooldownRegenVie -= dt;
		if (_cooldownRegenVie > 0f)
			return;
		_cooldownRegenVie = Mathf.Max(1f, IntervalleRegenVieSecondes);
		if (_vieCourante >= _vieMaxActuelle - 0.001f)
			return;
		float gain = _vieMaxActuelle * Mathf.Clamp(RegenViePourcentageParCycle, 0f, 1f);
		if (gain <= 0.0001f)
			return;
		_vieCourante = Mathf.Min(_vieMaxActuelle, _vieCourante + gain);
		MettreAJourAffichageFaim3D();
	}

	private void GererDegatsFamine(float dt)
	{
		if (_etat == EtatBoeuf.Mort)
			return;
		if (_faimCourante > 0.0001f)
			return;

		float degats = Mathf.Max(0.01f, DegatsVieParSecondeFaimNulle) * dt;
		_vieCourante = Mathf.Max(0f, _vieCourante - degats);
		_flashRougeDegatsRestant = Mathf.Max(_flashRougeDegatsRestant, Mathf.Max(0.05f, DureeFlashRougeDegats));
		AppliquerFlashRougeSurMateriaux(1f);
		MettreAJourAffichageFaim3D();
		if (_vieCourante <= 0.0001f)
			BasculerEnMort();
	}

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

	private bool FaimCritiquePrioritaire() => RatioFaimCourant() <= 0.25f;

	private bool DoitEntrerBroutageSelonSeuils()
	{
		if (FaimCritiquePrioritaire())
			return true;
		return RatioFaimCourant() <= 0.50f || _faimCourante <= SeuilRechercheHerbe;
	}

	private void ForcerEtatBroutageSiBesoin(bool prioriteAbsolue)
	{
		if (_etat != EtatBoeuf.Broutage)
		{
			_etat = EtatBoeuf.Broutage;
			_tempsBroutage = DureeBroutage + _rng.RandfRange(0f, 2f);
			_cooldownMorsure = 0.15f;
			_echecsMorsureConsecutifs = 0;
		}
		else if (prioriteAbsolue)
		{
			_tempsBroutage = Mathf.Max(_tempsBroutage, DureeBroutage * 1.25f);
			_cooldownMorsure = Mathf.Min(_cooldownMorsure, 0.15f);
		}
		if (!TrouverPointHerbeProche(out Vector3 herbe))
			herbe = GlobalPosition;
		_cibleCourante = herbe;
	}

	private bool HerbeDisponibleAutour(Vector3 point, float rayon)
	{
		if (_gestionnaire == null) return false;
		return _gestionnaire.ExisteGazonFauneGlobal(point, rayon);
	}

	private bool TrouverPointHerbeProche(out Vector3 cibleHerbe)
	{
		cibleHerbe = GlobalPosition;
		if (_gestionnaire == null)
			return false;
		if (HerbeDisponibleAutour(GlobalPosition, RayonMangerHerbe))
			return true;

		float rayonMax = Mathf.Max(RayonMangerHerbe + 1f, RayonRechercheHerbeVisible);
		int essais = Mathf.Max(6, EssaisRechercheHerbe);
		for (int i = 0; i < essais; i++)
		{
			float angle = _rng.RandfRange(0f, Mathf.Tau);
			float dist = _rng.RandfRange(RayonMangerHerbe + 0.5f, rayonMax);
			Vector3 cand = GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
			if (!PositionTerrainValide(cand))
				continue;
			if (!HerbeDisponibleAutour(cand, RayonMangerHerbe))
				continue;
			cibleHerbe = new Vector3(cand.X, GlobalPosition.Y, cand.Z);
			return true;
		}
		return false;
	}

	private float AngleVisionActuelDegres()
	{
		float a = AngleVisionBaseDegres + GainAngleVisionParNiveauDegres * Mathf.Max(0, _niveau - 1);
		return Mathf.Clamp(a, Mathf.Min(AngleVisionBaseDegres, AngleVisionMaxDegres), Mathf.Max(AngleVisionBaseDegres, AngleVisionMaxDegres));
	}

	private bool PossedeLigneDeVueSurJoueur()
	{
		if (_joueur == null) return false;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;

		Vector3 origine = GlobalPosition + Vector3.Up * Mathf.Max(0.2f, HauteurYeuxPerception);
		Vector3 cible = _joueur.GlobalPosition + Vector3.Up * 1.0f;
		var q = PhysicsRayQueryParameters3D.Create(origine, cible);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit == null || hit.Count == 0)
			return false;
		if (!hit.ContainsKey("collider"))
			return false;
		var collider = hit["collider"].AsGodotObject();
		return collider == _joueur;
	}

	private bool PeutPercevoirJoueur(float dist, Vector3 versJoueurHoriz)
	{
		if (_joueur == null || dist <= 0.001f)
			return false;

		// Mémoire courte de perception pour éviter clignotement de décision entre scans.
		if (_memoireDetectionJoueur > 0f && _cooldownVerificationVisionJoueur > 0f)
			return true;
		if (_cooldownVerificationVisionJoueur > 0f)
			return false;
		_cooldownVerificationVisionJoueur = Mathf.Max(0.05f, IntervalleVerificationVisionJoueur);

		bool detecte = false;
		if (dist <= Mathf.Max(0.1f, DistanceOuieJoueur))
			detecte = true;

		if (!detecte && UtiliserConeVisionJoueur && dist <= Mathf.Max(DistancePeurJoueur, DistanceVisionMaxJoueur))
		{
			Vector3 fwd = -GlobalTransform.Basis.Z;
			fwd.Y = 0f;
			fwd = fwd.LengthSquared() > 0.0001f ? fwd.Normalized() : Vector3.Forward;
			Vector3 dir = versJoueurHoriz.LengthSquared() > 0.0001f ? versJoueurHoriz.Normalized() : Vector3.Zero;
			float dot = Mathf.Clamp(fwd.Dot(dir), -1f, 1f);
			float angle = Mathf.RadToDeg(Mathf.Acos(dot));
			if (angle <= AngleVisionActuelDegres() * 0.5f && PossedeLigneDeVueSurJoueur())
				detecte = true;
		}

		if (detecte)
			_memoireDetectionJoueur = Mathf.Max(0.15f, MemoireDetectionSecondes);
		return detecte || _memoireDetectionJoueur > 0f;
	}

	private IReadOnlyList<BoeufSauvage> ObtenirPopulationLocale()
	{
		if (_gestionnaireFaune == null || !GodotObject.IsInstanceValid(_gestionnaireFaune))
			_gestionnaireFaune = GetParent() as GestionnaireFauneBoeufs;
		return _gestionnaireFaune?.ObtenirBoeufsActifs();
	}

	private bool TrouverFemelleMenaceeParJoueur(out VacheSauvage femelleMenacee)
	{
		femelleMenacee = null;
		if (_joueur == null) return false;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null) return false;
		float meilleure = float.MaxValue;
		for (int i = 0; i < population.Count; i++)
		{
			if (population[i] is not VacheSauvage f || f == this || !GodotObject.IsInstanceValid(f) || f._etat == EtatBoeuf.Mort)
				continue;
			float dJ = f.GlobalPosition.DistanceTo(_joueur.GlobalPosition);
			if (dJ > DistanceAlerteFemelle)
				continue;
			float dMoi = f.GlobalPosition.DistanceTo(GlobalPosition);
			if (dMoi > RayonRassemblement * 1.2f)
				continue;
			if (dJ < meilleure)
			{
				meilleure = dJ;
				femelleMenacee = f;
			}
		}
		return femelleMenacee != null;
	}

	private void GererReproductionEtGestation()
	{
		if (_etat == EtatBoeuf.Mort || GetParent() == null)
			return;

		MettreAJourMaturiteVeau();
		if (!ActiverReproductionFaune || !EstFemelle || _estVeauActif)
			return;

		if (_estEnGestation)
		{
			if (_tempsGestationRestant <= 0f)
				DonnerNaissance();
			return;
		}
	}

	private void MettreAJourMaturiteVeau()
	{
		if (!_estVeauActif)
			return;
		if (_ageSecondes < Mathf.Max(60f, DureeVeauAvantMaturiteSecondes))
			return;
		_estVeauActif = false;
		AppliquerGeneTailleVisuelleEtPhysique();
		MettreAJourStatsDerivees();
		EmitSignal(SignalName.EvolutionEvenement, "maturite_veau", 1f, _niveau, _ageSecondes / 3600f);
	}

	private void TenterConceptionJournaliereSelective()
	{
		if (_tentativeReproductionJourEffectuee || !EstFemelle || _estVeauActif || _estEnGestation)
			return;
		_tentativeReproductionJourEffectuee = true;
		if (_faimCourante < SeuilRechercheHerbe * 0.85f)
			return;
		if (!TrouverMaleSurvivantPrioritairePourReproduction(out BoeufSauvage male))
			return;
		if (!male.PeutParticiperCommeMalePourReproduction())
			return;
		if (_rng.Randf() > Mathf.Clamp(ChanceConceptionJournaliere, 0f, 1f))
			return;
		CommencerGestationAvec(male);
	}

	private bool PeutParticiperCommeMalePourReproduction()
	{
		if (!EstTaureau || _etat == EtatBoeuf.Mort || _estVeauActif || _tentativeReproductionJourEffectuee)
			return false;
		if (_faimCourante < SeuilRechercheHerbe * 0.8f)
			return false;
		return true;
	}

	private void EvaluerAdaptationComportementaleSelonEnvironnement()
	{
		if (!ActiverEvolutionEnvironnementale || _etat == EtatBoeuf.Mort)
			return;

		float intensite = Mathf.Max(0.001f, IntensiteAdaptationComportementale);
		float ratioStamina = _staminaMaxActuelle > 0.01f ? _staminaCourante / _staminaMaxActuelle : 0f;
		bool estStress = _tempsFuite > 0f || _memoireDetectionJoueur > 0f;
		bool environnementStable = _faimCourante > Mathf.Max(10f, SeuilRechercheHerbe) && ratioStamina > 0.55f && !estStress;
		bool procheTroupe = false;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population != null)
		{
			int voisins = 0;
			float rayon2 = Mathf.Max(4f, RayonRassemblement);
			rayon2 *= rayon2;
			for (int i = 0; i < population.Count; i++)
			{
				BoeufSauvage b = population[i];
				if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
					continue;
				if (GlobalPosition.DistanceSquaredTo(b.GlobalPosition) <= rayon2)
					voisins++;
			}
			procheTroupe = voisins >= 2;
		}

		if (environnementStable)
		{
			_geneConfiance = Mathf.Clamp(_geneConfiance + intensite * 0.7f, 0f, 1f);
			_geneReflexeFuite = Mathf.Clamp(_geneReflexeFuite - intensite * 0.45f, 0f, 1f);
		}
		if (procheTroupe)
		{
			_geneConfiance = Mathf.Clamp(_geneConfiance + intensite * 0.55f, 0f, 1f);
			_geneReflexeAttaque = Mathf.Clamp(_geneReflexeAttaque + intensite * 0.18f, 0f, 1f);
		}
		if (estStress)
		{
			_geneReflexeFuite = Mathf.Clamp(_geneReflexeFuite + intensite * 0.85f, 0f, 1f);
			_geneConfiance = Mathf.Clamp(_geneConfiance - intensite * 0.55f, 0f, 1f);
		}
		if (_etat == EtatBoeuf.Charge)
			_geneReflexeAttaque = Mathf.Clamp(_geneReflexeAttaque + intensite * 0.7f, 0f, 1f);
		if (_etat == EtatBoeuf.Fuite)
			_geneReflexeAttaque = Mathf.Clamp(_geneReflexeAttaque - intensite * 0.35f, 0f, 1f);

		EvaluerDeblocagesAdaptationEnvironnement(environnementStable, procheTroupe, estStress);

		if (environnementStable || procheTroupe || estStress)
			EmitSignal(SignalName.EvolutionEvenement, "adaptation_environnement", intensite, _niveau, _ageSecondes / 3600f);
	}

	private void EvaluerDeblocagesAdaptationEnvironnement(bool environnementStable, bool procheTroupe, bool estStress)
	{
		float scoreCible =
			Mathf.Clamp(_geneConfiance, 0f, 1f) * 0.45f +
			(1f - Mathf.Clamp(_geneReflexeFuite, 0f, 1f)) * 0.35f +
			Mathf.Clamp(_geneReflexeAttaque, 0f, 1f) * 0.20f;
		if (environnementStable)
			scoreCible += 0.08f;
		if (procheTroupe)
			scoreCible += 0.06f;
		if (estStress)
			scoreCible -= 0.10f;

		scoreCible = Mathf.Clamp(scoreCible, 0f, 1f);
		_scoreAdaptationEnvironnement = Mathf.Lerp(_scoreAdaptationEnvironnement, scoreCible, 0.35f);

		VerifierDeblocage(ref _deblocageAnimationContextuelle, _scoreAdaptationEnvironnement >= 0.42f, "deblocage_animation_contextuelle");
		VerifierDeblocage(ref _deblocageStrategieTroupeau, procheTroupe && _scoreAdaptationEnvironnement >= 0.58f, "deblocage_pensee_troupeau");
		VerifierDeblocage(ref _deblocageAffichageTroupeau, _scoreAdaptationEnvironnement >= 0.50f, "deblocage_affichage_troupeau");
	}

	private void CommencerGestationAvec(BoeufSauvage male)
	{
		if (male == null || !GodotObject.IsInstanceValid(male))
			return;
		_estEnGestation = true;
		_tentativeReproductionJourEffectuee = true;
		_maleGestationReference = male;
		_tempsGestationRestant = Mathf.Max(10f, DureeGestationSecondes);
		_cooldownReproduction = Mathf.Max(5f, CooldownReproductionSecondes * 0.35f);
		male._tentativeReproductionJourEffectuee = true;
		male._cooldownReproduction = Mathf.Max(5f, male.CooldownReproductionSecondes * 0.35f);
		AjouterExperience(1.2f, "conception");
		male.AjouterExperience(0.6f, "reproduction");
	}

	private bool TrouverMaleProchePourReproduction(out BoeufSauvage male)
	{
		male = null;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null) return false;
		float meilleure = float.MaxValue;
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (!b.EstTaureau)
				continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
			if (d2 < meilleure && d2 <= 14f * 14f)
			{
				meilleure = d2;
				male = b;
			}
		}
		return male != null;
	}

	private bool TrouverMaleSurvivantPrioritairePourReproduction(out BoeufSauvage male)
	{
		male = null;
		IReadOnlyList<BoeufSauvage> population = ObtenirPopulationLocale();
		if (population == null)
			return false;
		float rayon = Mathf.Max(5f, RayonReproductionJour);
		float rayon2 = rayon * rayon;
		_scratchCandidatsReproduction.Clear();
		for (int i = 0; i < population.Count; i++)
		{
			BoeufSauvage b = population[i];
			if (b == this || b == null || !GodotObject.IsInstanceValid(b) || b._etat == EtatBoeuf.Mort)
				continue;
			if (!b.EstTaureau || b._estVeauActif || b._tentativeReproductionJourEffectuee)
				continue;
			float d2 = GlobalPosition.DistanceSquaredTo(b.GlobalPosition);
			if (d2 > rayon2)
				continue;
			_scratchCandidatsReproduction.Add(b);
		}
		if (_scratchCandidatsReproduction.Count == 0)
			return false;

		_scratchCandidatsReproduction.Sort((a, b) => b._ageSecondes.CompareTo(a._ageSecondes));
		int garder = Mathf.Clamp(MaxCandidatsMalesParAge, 1, _scratchCandidatsReproduction.Count);
		float meilleurScore = float.MinValue;
		for (int i = 0; i < garder; i++)
		{
			BoeufSauvage c = _scratchCandidatsReproduction[i];
			float ageScore = Mathf.Clamp(c._ageSecondes / Mathf.Max(1f, _ageSecondes + 1f), 0f, 2.5f);
			float proxScore = 1f - Mathf.Clamp(GlobalPosition.DistanceTo(c.GlobalPosition) / rayon, 0f, 1f);
			float score = ageScore * 0.72f + proxScore * 0.28f + _rng.RandfRange(0f, 0.08f);
			if (score > meilleurScore)
			{
				meilleurScore = score;
				male = c;
			}
		}
		return male != null;
	}

	private void DonnerNaissance()
	{
		_estEnGestation = false;
		_tempsGestationRestant = 0f;
		_cooldownReproduction = Mathf.Max(10f, CooldownReproductionSecondes);

		bool naissanceMale = _rng.Randf() <= Mathf.Clamp(ProbabiliteNaissanceMale, 0f, 1f);
		string chemin = naissanceMale ? CheminSceneNaissanceMale : CheminSceneNaissanceFemelle;
		if (NaissanceSousFormeVeau)
		{
			string cheminVeau = naissanceMale ? CheminSceneVeauMale : CheminSceneVeauFemelle;
			if (!string.IsNullOrWhiteSpace(cheminVeau) && ResourceLoader.Exists(cheminVeau))
				chemin = cheminVeau;
		}
		if (string.IsNullOrWhiteSpace(chemin) || !ResourceLoader.Exists(chemin))
			return;
		var ps = GD.Load<PackedScene>(chemin);
		Node inst = ps?.Instantiate();
		if (inst is not BoeufSauvage bebe)
		{
			inst?.QueueFree();
			return;
		}

		Vector3 pos = GlobalPosition + new Vector3(_rng.RandfRange(-2.1f, 2.1f), 0f, _rng.RandfRange(-2.1f, 2.1f));
		Vector3 sol = TrouverSolPourNaissance(pos);
		GetParent().AddChild(bebe);
		bebe.GlobalPosition = sol + Vector3.Up * 0.2f;
		bebe.Configurer(_gestionnaire, _joueur, _seedTerrain, _ancreTroupeau);
		BoeufSauvage pere = _maleGestationReference != null && GodotObject.IsInstanceValid(_maleGestationReference)
			? _maleGestationReference
			: null;
		float minTaille = Mathf.Min(TailleGeneMin, TailleGeneMax);
		float maxTaille = Mathf.Max(TailleGeneMin, TailleGeneMax);
		(float geneTailleA, float geneTailleB) = CroisementSBX(
			_geneTaille,
			pere != null ? pere._geneTaille : _geneTaille,
			minTaille, maxTaille, EtaSBX);
		float geneBebe = _rng.Randf() < 0.5f ? geneTailleA : geneTailleB;
		geneBebe = MutationPolynomiale(geneBebe, minTaille, maxTaille, EtaMutationPolynomiale, 1f);
		bebe.DefinirGeneTaille(geneBebe);

		float minVit = Mathf.Min(VitesseGeneMin, VitesseGeneMax);
		float maxVit = Mathf.Max(VitesseGeneMin, VitesseGeneMax);
		(float geneVitA, float geneVitB) = CroisementSBX(
			_geneVitesseDeplacement,
			pere != null ? pere._geneVitesseDeplacement : _geneVitesseDeplacement,
			minVit, maxVit, EtaSBX);
		float geneVitesseBebe = _rng.Randf() < 0.5f ? geneVitA : geneVitB;
		geneVitesseBebe = MutationPolynomiale(geneVitesseBebe, minVit, maxVit, EtaMutationPolynomiale, 1f);
		geneVitesseBebe += _rng.RandfRange(-IntensiteMutationVitesse, IntensiteMutationVitesse);
		_geneVitesseDeplacement = Mathf.Clamp(_geneVitesseDeplacement, minVit, maxVit);
		bebe._geneVitesseDeplacement = Mathf.Clamp(geneVitesseBebe, minVit, maxVit);

		(float genePersA, float genePersB) = CroisementSBX(
			_genePersonnalite,
			pere != null ? pere._genePersonnalite : _genePersonnalite,
			0f, 1f, EtaSBX);
		float genePersBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? genePersA : genePersB, 0f, 1f, EtaMutationPolynomiale, 0.9f);
		bebe._genePersonnalite = Mathf.Clamp(genePersBebe, 0f, 1f);

		(float geneConfA, float geneConfB) = CroisementSBX(
			_geneConfiance,
			pere != null ? pere._geneConfiance : _geneConfiance,
			0f, 1f, EtaSBX);
		float geneConfianceBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? geneConfA : geneConfB, 0f, 1f, EtaMutationPolynomiale, 0.85f);

		(float geneFuiteA, float geneFuiteB) = CroisementSBX(
			_geneReflexeFuite,
			pere != null ? pere._geneReflexeFuite : _geneReflexeFuite,
			0f, 1f, EtaSBX);
		float geneFuiteBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? geneFuiteA : geneFuiteB, 0f, 1f, EtaMutationPolynomiale, 0.85f);

		(float geneAttaqueA, float geneAttaqueB) = CroisementSBX(
			_geneReflexeAttaque,
			pere != null ? pere._geneReflexeAttaque : _geneReflexeAttaque,
			0f, 1f, EtaSBX);
		float geneAttaqueBebe = MutationPolynomiale(_rng.Randf() < 0.5f ? geneAttaqueA : geneAttaqueB, 0f, 1f, EtaMutationPolynomiale, 0.85f);
		bebe.DefinirGenesComportementSocial(geneConfianceBebe, geneFuiteBebe, geneAttaqueBebe);

		(float navPrudenceA, float navPrudenceB) = CroisementSBX(
			_genePrudenceNavigation,
			pere != null ? pere._genePrudenceNavigation : _genePrudenceNavigation,
			0f, 1f, EtaSBX);
		(float navSautA, float navSautB) = CroisementSBX(
			_geneAudaceSaut,
			pere != null ? pere._geneAudaceSaut : _geneAudaceSaut,
			0f, 1f, EtaSBX);
		float genePrudence = MutationPolynomiale(_rng.Randf() < 0.5f ? navPrudenceA : navPrudenceB, 0f, 1f, EtaMutationPolynomiale, 0.8f);
		float geneSaut = MutationPolynomiale(_rng.Randf() < 0.5f ? navSautA : navSautB, 0f, 1f, EtaMutationPolynomiale, 0.8f);
		bebe.DefinirGenesNavigation(genePrudence, geneSaut);
		if (NaissanceSousFormeVeau)
			bebe.ConfigurerCommeVeau();

		_maleGestationReference = null;
		AjouterExperience(2.6f, "naissance");
	}

	private Vector3 TrouverSolPourNaissance(Vector3 approx)
	{
		int x = Mathf.FloorToInt(approx.X);
		int z = Mathf.FloorToInt(approx.Z);
		int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, _seedTerrain);
		Vector3 test = new Vector3(x + 0.5f, h + 60f, z + 0.5f);
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null)
			return approx;
		var q = PhysicsRayQueryParameters3D.Create(test, test + Vector3.Down * 120f);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = world.DirectSpaceState.IntersectRay(q);
		if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
			return (Vector3)hit["position"];
		return approx;
	}

	private void GererPresenceJoueur()
	{
		if (_joueur == null) return;
		if (FaimCritiquePrioritaire())
			return; // Sous 25% de faim: l'animal ignore le joueur et cherche a manger en priorite.
		Vector3 d = _joueur.GlobalPosition - GlobalPosition;
		d.Y = 0f;
		float dist = d.Length();
		if (dist <= 0.001f) return;
		if (!PeutPercevoirJoueur(dist, d))
			return;
		float geneConfiance = Mathf.Clamp(_geneConfiance, 0f, 1f);
		float geneFuite = Mathf.Clamp(_geneReflexeFuite, 0f, 1f);
		float geneAttaque = Mathf.Clamp(_geneReflexeAttaque, 0f, 1f);
		float facteurPersonnalitePeur = Mathf.Lerp(1.35f, 0.7f, _genePersonnalite);
		float facteurConfiancePeur = Mathf.Lerp(1.42f, 0.62f, geneConfiance);
		float facteurFuitePeur = Mathf.Lerp(0.82f, 1.36f, geneFuite);
		float distancePeurEffective = DistancePeurJoueur * facteurPersonnalitePeur * facteurConfiancePeur * facteurFuitePeur;
		float chanceAgressivite = Mathf.Lerp(0.03f, 0.32f, geneAttaque) * Mathf.Lerp(1.08f, 0.62f, geneConfiance);
		chanceAgressivite *= Mathf.Lerp(0.95f, 0.65f, geneFuite);
		chanceAgressivite = Mathf.Clamp(chanceAgressivite, 0.01f, 0.9f);
		float chanceResterCalme = Mathf.Lerp(0.05f, 0.68f, geneConfiance);
		chanceResterCalme *= Mathf.Lerp(1f, 0.58f, geneFuite);
		chanceResterCalme *= Mathf.Lerp(1f, 0.75f, geneAttaque);
		chanceResterCalme = Mathf.Clamp(chanceResterCalme, 0f, 0.85f);

		if (EstTaureau && TaureauProtegeFemelles && TrouverFemelleMenaceeParJoueur(out _))
		{
			if (!EssayerDepenserStamina(CoutStaminaAttaque))
				return;
			_etat = EtatBoeuf.Charge;
			_tempsCharge = Mathf.Max(_tempsCharge, DureeChargeProtection);
			_cibleCourante = _joueur.GlobalPosition;
			EmitSignal(SignalName.EvolutionEvenement, "charge_protection_troupeau", 1f, _niveau, _ageSecondes / 3600f);
			return;
		}

		if (_peutAttaquer && dist < 2.4f && _faimCourante > SeuilRechercheHerbe + 10f && _rng.Randf() < chanceAgressivite)
		{
			if (!EssayerDepenserStamina(CoutStaminaAttaque))
				return;
			_etat = EtatBoeuf.Charge;
			_tempsCharge = 1.2f;
			EmitSignal(SignalName.EvolutionEvenement, "charge_joueur", 1f, _niveau, _ageSecondes / 3600f);
			return;
		}

		if (dist < distancePeurEffective)
		{
			if (_rng.Randf() < chanceResterCalme)
				return;
			_tempsFuite = 3.0f;
			if (_peutEsquiver && _rng.Randf() < 0.22f)
			{
				Vector3 tangent = d.Normalized().Rotated(Vector3.Up, _rng.RandfRange(-Mathf.Pi / 2f, Mathf.Pi / 2f));
				_cibleCourante = GlobalPosition - tangent * _rng.RandfRange(6f, 10f);
				AjouterExperience(ExperienceEsquive, "esquive");
			}
		}
	}

	private void GererEtatEtCible(float dt)
	{
		if (FaimCritiquePrioritaire())
			ForcerEtatBroutageSiBesoin(prioriteAbsolue: true);
		else if (DoitEntrerBroutageSelonSeuils())
			ForcerEtatBroutageSiBesoin(prioriteAbsolue: false);

		if (_etat == EtatBoeuf.Charge && _tempsCharge > 0f && _joueur != null)
		{
			_cibleCourante = _joueur.GlobalPosition;
			if (GlobalPosition.DistanceSquaredTo(_joueur.GlobalPosition) < 1.8f * 1.8f)
			{
				EssayerAppliquerImpactChargeJoueur();
				_tempsFuite = 1.6f; // Impact: le boeuf repart ensuite en fuite.
			}
			return;
		}

		if (_tempsFuite > 0f)
		{
			_etat = EtatBoeuf.Fuite;
			Vector3 fuite = _joueur != null ? (GlobalPosition - _joueur.GlobalPosition) : Vector3.Forward;
			fuite.Y = 0f;
			if (fuite.LengthSquared() < 0.001f) fuite = Vector3.Forward;
			_cibleCourante = GlobalPosition + fuite.Normalized() * _rng.RandfRange(10f, 18f);
			return;
		}

		if (_etat == EtatBoeuf.Broutage)
		{
			_tempsBroutage -= dt;
			_cooldownMorsure -= dt;
			float seuilHerbe = Mathf.Max(1.0f, RayonMangerHerbe * 0.9f);
			if (GlobalPosition.DistanceSquaredTo(_cibleCourante) > seuilHerbe * seuilHerbe)
			{
				// Se déplace vers une zone réellement couverte en mesh herbe.
				if (!HerbeDisponibleAutour(_cibleCourante, RayonMangerHerbe) && TrouverPointHerbeProche(out Vector3 h2))
					_cibleCourante = h2;
			}
			if (_cooldownMorsure <= 0f)
			{
				_cooldownMorsure = 0.85f;
				bool aMange = ConsommerHerbeSousPattes();
				if (!aMange)
				{
					_echecsMorsureConsecutifs++;
					_cooldownMorsure = 0.65f;
					if (TrouverPointHerbeProche(out Vector3 h3))
						_cibleCourante = h3;
					else if (_echecsMorsureConsecutifs >= 3)
					{
						// Évite le spam statique sans herbe: repart chercher ailleurs.
						_etat = EtatBoeuf.Errance;
						ChoisirNouvelleCible(false);
						return;
					}
				}
				else
				{
					_echecsMorsureConsecutifs = 0;
				}
			}
			if (_tempsBroutage <= 0f || _faimCourante >= _faimMaxActuelle - 2f)
			{
				_etat = EtatBoeuf.Errance;
				ChoisirNouvelleCible(false);
			}
			return;
		}

		if (_peutAider && TrouverAllieEnDetresse(out BoeufSauvage allie))
		{
			_etat = EtatBoeuf.Soutien;
			_cibleCourante = allie.GlobalPosition;
			if (GlobalPosition.DistanceSquaredTo(allie.GlobalPosition) < 16f)
				AjouterExperience(0.5f, "aide_allie");
			return;
		}

		if (DoitEntrerBroutageSelonSeuils() && _etat != EtatBoeuf.Fuite)
		{
			ForcerEtatBroutageSiBesoin(prioriteAbsolue: FaimCritiquePrioritaire());
			return;
		}

		if (_peutSuivre && TrouverAllieLePlusProche(out BoeufSauvage proche))
		{
			float d2 = GlobalPosition.DistanceSquaredTo(proche.GlobalPosition);
			float min2 = 11f * 11f;
			float max2 = (RayonRassemblement * 1.1f) * (RayonRassemblement * 1.1f);
			if (d2 > min2 && d2 < max2)
			{
				_etat = EtatBoeuf.Soutien;
				_cibleCourante = proche.GlobalPosition;
				return;
			}
		}

		if (_deblocageStrategieTroupeau && TrouverAllieLePlusProche(out BoeufSauvage procheTroupeau))
		{
			float dTroupe2 = GlobalPosition.DistanceSquaredTo(procheTroupeau.GlobalPosition);
			float minTroupe2 = 7.5f * 7.5f;
			float maxTroupe2 = (RayonRassemblement * 1.35f) * (RayonRassemblement * 1.35f);
			if (dTroupe2 > minTroupe2 && dTroupe2 < maxTroupe2)
			{
				_etat = EtatBoeuf.Soutien;
				_cibleCourante = procheTroupeau.GlobalPosition.Lerp(_ancreTroupeau, 0.35f);
				return;
			}
		}

		_etat = EtatBoeuf.Errance;
		if (_tempsIdleErrance > 0f)
		{
			_cibleCourante = GlobalPosition; // Déclenche l'animation idle le temps de la pause.
			return;
		}

		if (GlobalPosition.DistanceSquaredTo(_cibleCourante) < 1.8f * 1.8f)
		{
			float minIdle = Mathf.Max(0f, DureeIdleErranceMin);
			float maxIdle = Mathf.Max(minIdle, DureeIdleErranceMax);
			_tempsIdleErrance = _rng.RandfRange(minIdle, maxIdle);
			if (_tempsIdleErrance > 0.01f)
			{
				_cibleCourante = GlobalPosition;
				return;
			}
		}

		if (_cooldownChoixCible <= 0f || GlobalPosition.DistanceSquaredTo(_cibleCourante) < 1.8f * 1.8f)
			ChoisirNouvelleCible(false);
	}

	private void EssayerAppliquerImpactChargeJoueur()
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur)) return;
		if (_cooldownImpactChargeJoueur > 0f) return;
		Vector3 dir = _joueur.GlobalPosition - GlobalPosition;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.0001f)
			dir = -GlobalTransform.Basis.Z;
		float impulsion = Mathf.Max(0.1f, ImpulsionChargeSurJoueur);
		if (_joueur is Joueur joueurHumain)
			joueurHumain.AppliquerPousseeBovin(dir, impulsion);
		else
		{
			Vector3 d = dir.Normalized();
			Vector3 v = _joueur.Velocity;
			v.X += d.X * impulsion;
			v.Z += d.Z * impulsion;
			_joueur.Velocity = v;
		}
		_cooldownImpactChargeJoueur = Mathf.Max(0.05f, CooldownImpactChargeJoueur);
		if (ContactChargeCrediblePourAnimation(dir))
			DeclencherAnimationAttaqueChargeVersJoueur();
	}

	/// <summary>Évite ruade / coup de tête animés sans vraie approche (contact crédible).</summary>
	private bool ContactChargeCrediblePourAnimation(Vector3 dirVersJoueurHoriz)
	{
		dirVersJoueurHoriz.Y = 0f;
		float dist = dirVersJoueurHoriz.Length();
		if (dist < 0.0001f)
			return true;
		Vector3 versJ = dirVersJoueurHoriz / dist;
		Vector3 vH = new Vector3(Velocity.X, 0f, Velocity.Z);
		float approche = vH.Dot(versJ);
		if (dist <= 1.15f)
			return true;
		return dist <= 1.82f && approche >= 0.55f;
	}

	/// <summary>Joue <see cref="_clipAttaqueKick"/> (cible derriere / sur le flanc arriere) ou <see cref="_clipAttaqueTete"/> (cible devant).</summary>
	private void DeclencherAnimationAttaqueChargeVersJoueur()
	{
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
			return;
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return;
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
			return;

		float duree = 0.72f;
		Animation animRef = _animationPlayer.GetAnimation(ObtenirStringNameAnimation(clip));
		if (animRef != null)
			duree = Mathf.Clamp(animRef.Length, 0.38f, 2.4f);
		_tempsVerrouAnimationCombat = duree;

		if (_blendLocomotionActif && _playbackEtatFaune != null && _animationTreeFaune != null && _animationTreeFaune.Active)
		{
			bool noeudPresent = (noeud == NomNoeudAttaqueTete && _machineAPorteAttaqueTete) || (noeud == NomNoeudAttaqueKick && _machineAPorteAttaqueKick);
			if (noeudPresent)
			{
				_noeudAnimationCombatVerrou = noeud;
				_playbackEtatFaune.Travel(ObtenirNomEtatAnimation(noeud));
				_etatCourantMachineAnimation = noeud;
				return;
			}
		}

		_noeudAnimationCombatVerrou = "";
		_animationPlayer.Play(ObtenirStringNameAnimation(clip), 0.08f);
	}

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

	private void BasculerEnMort()
	{
		_etat = EtatBoeuf.Mort;
		_vieCourante = 0f;
		_cadavreAttendDepecage = true;
		_cadavreLootDistribue = false;
		_coupsDepecageDagueValides = 0;
		_tempsMort = float.MaxValue;
		Velocity = Vector3.Zero;
		EmitSignal(SignalName.EvolutionEvenement, "mort_faim", 1f, _niveau, _ageSecondes / 3600f);
		if (!string.IsNullOrEmpty(_clipMort) && _animationPlayer != null && _animationPlayer.HasAnimation(_clipMort))
		{
			if (_animationTreeFaune != null)
				_animationTreeFaune.Active = false;
			_animationPlayer.Play(ObtenirStringNameAnimation(_clipMort), 0.12f);
		}
		else if (_playbackEtatFaune != null && _machineAPorteMort)
			_playbackEtatFaune.Travel(NomNoeudMortString);
		else if (!string.IsNullOrEmpty(_clipMort) && _animationPlayer != null)
			_animationPlayer.Play(ObtenirStringNameAnimation(_clipMort), 0.12f);
	}

	private void GererMort(float dt)
	{
		if (_cadavreLootDistribue)
			return;
		if (_cadavreAttendDepecage)
			return;
		_tempsMort -= dt;
		if (_tempsMort <= 0f)
			QueueFree();
	}

	/// <summary>Cadavre encore présent (pas looté).</summary>
	public bool EstCadavreDepecable() => _etat == EtatBoeuf.Mort && !_cadavreLootDistribue;

	/// <summary>Enregistre un coup de dague valide sur le cadavre. Retourne true uniquement au dernier coup requis (déclencher le loot).</summary>
	public bool EnregistrerCoupDepecageDagueValide()
	{
		if (_etat != EtatBoeuf.Mort || !_cadavreAttendDepecage || _cadavreLootDistribue)
			return false;
		int requis = Mathf.Max(3, CoupsDagueRequisPourFinDepecage);
		if (_coupsDepecageDagueValides >= requis)
			return false;
		_coupsDepecageDagueValides++;
		return _coupsDepecageDagueValides == requis;
	}

	/// <summary>Marque le cadavre comme traité et le retire de la scène (après spawn du loot).</summary>
	public void FinaliserCadavreApresDepecage()
	{
		_vieCourante = 0f;
		_cadavreLootDistribue = true;
		_cadavreAttendDepecage = false;
		if (IsInsideTree())
			QueueFree();
	}

	/// <summary>Indique au streaming/persist que cet individu ne doit plus jamais être rechargé.</summary>
	public bool DoitEtreExcluPersistanceFaune() => _cadavreLootDistribue;

	/// <summary>Première texture d’albedo trouvée sur le mesh du bovin (cuir dérivé de la peau).</summary>
	public Texture2D EssayerObtenirTexturePeauPourCuir()
	{
		Node racine = _modeleVisuel != null && GodotObject.IsInstanceValid(_modeleVisuel) ? _modeleVisuel : (Node)this;
		return ChercherPremiereAlbedoTextureRecursif(racine);
	}

	private static Texture2D ChercherPremiereAlbedoTextureRecursif(Node n)
	{
		if (n is MeshInstance3D mi && mi.Mesh != null)
		{
			int nSurf = mi.Mesh.GetSurfaceCount();
			for (int s = 0; s < nSurf; s++)
			{
				Material ov = mi.GetSurfaceOverrideMaterial(s);
				if (ov is BaseMaterial3D bmOv && bmOv.AlbedoTexture != null)
					return bmOv.AlbedoTexture;
			}
			for (int s = 0; s < nSurf; s++)
			{
				Material mSurf = mi.Mesh.SurfaceGetMaterial(s);
				if (mSurf is BaseMaterial3D bmSurf && bmSurf.AlbedoTexture != null)
					return bmSurf.AlbedoTexture;
			}
		}
		foreach (Node enfant in n.GetChildren())
		{
			Texture2D t = ChercherPremiereAlbedoTextureRecursif(enfant);
			if (t != null)
				return t;
		}
		return null;
	}

	/// <summary>Clé pour <see cref="SlotInventaire.GenomeAssemblage"/> : empilement cuir selon la même « peau ».</summary>
	public string ConstruireGenomePeauPourSlotCuir(Texture2D texPeau)
	{
		if (texPeau != null && !string.IsNullOrEmpty(texPeau.ResourcePath))
			return "PEAU:" + texPeau.ResourcePath;
		return EstTaureau ? "PEAU:TAUREAU" : "PEAU:VACHE";
	}

	public bool RecevoirImpactCombat(
		float intensiteImpact,
		Vector3 pointImpactMonde,
		Vector3 directionImpactMonde,
		bool estTranchant,
		bool estPerforant,
		string nomZoneImpact = "",
		ulong sourceId = 0UL)
	{
		if (_etat == EtatBoeuf.Mort || intensiteImpact <= 0.0001f)
			return false;

		double maintenant = Time.GetTicksMsec() / 1000.0;
		if (sourceId != 0UL)
		{
			if (_horodatageDernierDegatParSource.TryGetValue(sourceId, out double dernier)
				&& (maintenant - dernier) < Mathf.Max(0.02f, CooldownDegatsParSourceSecondes))
				return false;
			_horodatageDernierDegatParSource[sourceId] = maintenant;
		}

		float degats = Mathf.Max(DegatsMinImpact, intensiteImpact * Mathf.Max(0.01f, MultiplicateurDegatsImpact));
		float multiplicateurZone = ObtenirMultiplicateurZoneImpact(nomZoneImpact, pointImpactMonde);
		degats *= multiplicateurZone;
		if (estTranchant)
			degats *= 1.12f;
		if (estPerforant)
			degats *= 1.30f;

		float capCoup = Mathf.Max(2f, _vieMaxActuelle * Mathf.Clamp(CapDegatsParImpactRatioVieMax, 0.05f, 0.8f));
		degats = Mathf.Clamp(degats, 0f, capCoup);
		if (degats <= 0.0001f)
			return false;

		_vieCourante = Mathf.Max(0f, _vieCourante - degats);
		JouerCriDegats(degats);
		MettreAJourAffichageFaim3D();
		if (_vieCourante <= 0.0001f)
		{
			BasculerEnMort();
			return true;
		}

		if (_etat != EtatBoeuf.Mort && _etat != EtatBoeuf.Charge)
		{
			_etat = EtatBoeuf.Fuite;
			_tempsFuite = Mathf.Max(_tempsFuite, estPerforant ? 2.4f : 1.35f);
		}
		return true;
	}

	private float ObtenirMultiplicateurZoneImpact(string nomZoneImpact, Vector3 pointImpactMonde)
	{
		string nom = (nomZoneImpact ?? string.Empty).ToLowerInvariant();
		if (nom.Contains("tete"))
			return 1.55f;
		if (nom.Contains("ventre"))
			return 1.25f;

		Vector3 local = ToLocal(pointImpactMonde);
		if (local.Y > 0.95f)
			return 1.45f;
		if (local.Y > 0.32f && local.Y < 0.85f)
			return 1.2f;
		return 1f;
	}

	/// <summary>1) Lecteur sur le squelette 2) Fusion scenes -> <c>locomotion_faune</c> 3) Noms de clips 4) <see cref="AnimationTree"/> ou lecture directe.</summary>
	private void InitialiserAnimations()
	{
		DetruireAnimationTreeFaune();
		_machineAPorteBroutage = false;
		_machineAPorteMort = false;
		_machineAPorteNage = false;
		_machineAPorteSaut = false;
		_machineAPorteSautGalop = false;
		_machineAPorteAttaqueKick = false;
		_machineAPorteAttaqueTete = false;
		_clipIdle = _clipMarche = _clipCourse = _clipTrot = _clipNage = _clipManger = _clipMort = "";
		_clipSaut = _clipSautGalop = _clipAttaqueKick = _clipAttaqueTete = "";

		if (!ResoudreLecteurAnimationPrincipalSurSquelette())
			return;

		ChargerScenesAnimationEtFusionnerSurBibliothequeFaune();
		List<string> tous = CollecterCheminsAnimation(_animationPlayer);
		DiagnosticListeClipsSiDemande(tous);
		ResoudreNomsClipsLocomotionDepuisBibliothequeEtListe(tous);
		InitialiserSelectionEvolutionnaireAnimations(tous);
		_timerCycleIdleSecondes = _rng.RandfRange(IntervalleMinCycleIdleSecondes * 0.35f, IntervalleMaxCycleIdleSecondes);

		if (string.IsNullOrEmpty(_clipIdle))
		{
			GD.PrintErr("ZERO-K Faune : aucun clip d'animation exploitable sur le squelette.");
			_animationPlayer = null;
			return;
		}

		AppliquerBouclesSurClipsLocomotion();
		DemarrerArbreAnimationOuLectureDirecte();
		_squelletteModele = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel != null ? _modeleVisuel : this);
	}

	private bool ResoudreLecteurAnimationPrincipalSurSquelette()
	{
		_animationPlayer = ChoisirMeilleurAnimationPlayer(_modeleVisuel != null ? _modeleVisuel : this);
		if (_animationPlayer == null || CompterClipsAnimation(_animationPlayer) == 0)
			_animationPlayer = ChoisirMeilleurAnimationPlayer(this);

		if (_animationPlayer != null && CompterClipsAnimation(_animationPlayer) > 0)
			return true;

		// GLB « squelette seul » (ex. Tripo) : pas d'AnimationPlayer importé — secours + message explicite.
		if (EssayerCreerLecteurEtFallbackLocomotionVisuelle())
			return true;

		GD.PrintErr("ZERO-K Faune : aucun AnimationPlayer avec clips sous le bovin (pas de nœud Modele ?).");
		return false;
	}

	/// <summary>
	/// Cree un lecteur sur le corps et des clips <c>locomotion_faune</c> minimaux (bob vertical du nœud Modele)
	/// quand le fichier glTF ne contient aucune animation. Pour un vrai cycle de marche, importez un GLB avec clips
	/// ou renseignez <see cref="CheminSceneGltfAnimationsExternesMemeRig"/> / les exports de scenes par action.
	/// </summary>
	private bool EssayerCreerLecteurEtFallbackLocomotionVisuelle()
	{
		if (_modeleVisuel == null)
			return false;

		AnimationPlayer ap = GetNodeOrNull<AnimationPlayer>("AnimationPlayerFauneCorps");
		if (ap == null || !GodotObject.IsInstanceValid(ap))
		{
			ap = new AnimationPlayer { Name = "AnimationPlayerFauneCorps" };
			AddChild(ap);
		}

		_animationPlayer = ap;
		Vector3 p0 = _modeleVisuel.Position;
		bool squelette = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel) != null;
		float m = squelette ? 0.22f : 1f;

		var lib = new AnimationLibrary();
		lib.AddAnimation("Idle", CreerAnimationBobPositionModele(p0, 0.028f * m, 2.6f));
		lib.AddAnimation("Marche", CreerAnimationBobPositionModele(p0, 0.065f * m, 0.52f, 1.15f, 1.00f));
		lib.AddAnimation("Course", CreerAnimationBobPositionModele(p0, 0.11f * m, 0.30f, 1.25f, 1.08f));
		lib.AddAnimation("Broutage", CreerAnimationBobPositionModele(p0, 0.018f * m, 3.1f, 0.45f, 0.35f));
		lib.AddAnimation("Mort", CreerAnimationBobPositionModele(p0, 0.006f * m, 1.35f, 0.12f, 0.08f, false));

		if (_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			_animationPlayer.RemoveAnimationLibrary(NomBibliothequeLocomotionFaune);
		_animationPlayer.AddAnimationLibrary(NomBibliothequeLocomotionFaune, lib);

		GD.Print($"ZERO-K Faune : le mesh n'inclut pas d'animations glTF — fallback local minimal active ({lib.GetAnimationList().Count} clips). " +
			"Pour des clips reels, ajoutez un .glb avec animations ou renseignez CheminSceneGltfAnimationsExternesMemeRig.");
		return CompterClipsAnimation(_animationPlayer) > 0;
	}

	/// <summary>Vrai si seuls les clips <c>locomotion_faune</c> de secours (bob sur Modele) sont utilises — on peut alors animer le squelette en code.</summary>
	private bool EstFallbackLocomotionBobSeulement()
	{
		if (_animationPlayer == null || !_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			return false;
		AnimationLibrary lib = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
		if (lib == null || !lib.HasAnimation("Idle"))
			return false;
		Animation anim = lib.GetAnimation("Idle");
		if (anim == null || anim.GetTrackCount() < 1)
			return false;
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			if (!anim.TrackGetPath(i).ToString().Contains("Modele", StringComparison.Ordinal))
				return false;
		}
		return true;
	}

	/// <summary>Locomotion approximative sur les os (mesh Tripo sans clips glTF).</summary>
	private void AppliquerLocomotionSquelettiqueProcedural(float dt, float vitesseHoriz)
	{
		if (_squelletteModele == null || !GodotObject.IsInstanceValid(_squelletteModele))
			return;

		float rythme = 1f;
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			rythme = 1.5f;
		else if (vitesseHoriz > 0.12f)
			rythme = Mathf.Lerp(1f, 1.38f, Mathf.Clamp(vitesseHoriz / Mathf.Max(0.01f, VitesseMarche), 0f, 1f));
		else if (_etat == EtatBoeuf.Broutage)
			rythme = 0.62f;

		_phaseLocomotionSqueletteProcedurale += dt * Mathf.Tau * 0.88f * rythme;
		float walk = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.12f, VitesseMarche), 0f, 1.85f);
		if (_etat == EtatBoeuf.Broutage)
			walk = 0.2f;
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			walk = Mathf.Max(walk, 1.1f);

		_squelletteModele.ResetBonePoses();
		int n = _squelletteModele.GetBoneCount();
		for (int i = 1; i < n; i++)
		{
			Transform3D rest = _squelletteModele.GetBoneRest(i);
			Quaternion baseR = Quaternion.FromEuler(rest.Basis.Orthonormalized().GetEuler());
			float alt = (i & 1) == 0 ? 1f : -1f;
			float mag = Mathf.DegToRad(6.5f) * walk * alt;
			if (_etat == EtatBoeuf.Broutage)
				mag *= 0.4f;
			float ph = _phaseLocomotionSqueletteProcedurale + i * 0.48f;
			Quaternion swing = Quaternion.FromEuler(new Vector3(
				Mathf.Sin(ph) * mag,
				Mathf.Sin(ph * 0.48f) * mag * 0.3f,
				Mathf.Cos(ph * 0.82f) * mag * 0.22f));
			_squelletteModele.SetBonePoseRotation(i, baseR * swing);
		}
	}

	private static Animation CreerAnimationBobPositionModele(
		Vector3 baseLocal,
		float amplitudeY,
		float duree,
		float pitchMul = 1f,
		float rollMul = 1f,
		bool loop = true)
	{
		float len = Mathf.Max(0.35f, duree);
		var anim = new Animation
		{
			LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None,
			Length = len
		};
		int trPos = anim.AddTrack(Animation.TrackType.Position3D);
		anim.TrackSetPath(trPos, new NodePath("Modele"));
		anim.PositionTrackInsertKey(trPos, 0.0, baseLocal);
		anim.PositionTrackInsertKey(trPos, len * 0.25, baseLocal + new Vector3(0f, amplitudeY * 0.55f, 0f));
		anim.PositionTrackInsertKey(trPos, len * 0.5, baseLocal + new Vector3(0f, amplitudeY, 0f));
		anim.PositionTrackInsertKey(trPos, len * 0.75, baseLocal + new Vector3(0f, amplitudeY * 0.45f, 0f));
		anim.PositionTrackInsertKey(trPos, len, baseLocal);

		// Donne une impression de pas/respiration meme sans squelette.
		int trRot = anim.AddTrack(Animation.TrackType.Rotation3D);
		anim.TrackSetPath(trRot, new NodePath("Modele"));
		Vector3 r0 = Vector3.Zero;
		Vector3 r1 = new Vector3(Mathf.DegToRad(amplitudeY * 85f * pitchMul), 0f, Mathf.DegToRad(amplitudeY * 35f * rollMul));
		Vector3 r2 = new Vector3(Mathf.DegToRad(-amplitudeY * 95f * pitchMul), 0f, Mathf.DegToRad(-amplitudeY * 28f * rollMul));
		anim.RotationTrackInsertKey(trRot, 0.0, Quaternion.FromEuler(r0));
		anim.RotationTrackInsertKey(trRot, len * 0.25, Quaternion.FromEuler(r1));
		anim.RotationTrackInsertKey(trRot, len * 0.5, Quaternion.FromEuler(r0));
		anim.RotationTrackInsertKey(trRot, len * 0.75, Quaternion.FromEuler(r2));
		anim.RotationTrackInsertKey(trRot, len, Quaternion.FromEuler(r0));
		return anim;
	}

	private void ChargerScenesAnimationEtFusionnerSurBibliothequeFaune()
	{
		PreparerLecteurEtBibliothequeLocomotionFaune();
		if (FusionnerAutomatiquementAnimationsDuGlbReference && !string.IsNullOrWhiteSpace(CheminGlbSqueletteReference) && ResourceLoader.Exists(CheminGlbSqueletteReference))
			FusionnerAnimationsRemappeesDepuisSceneReference(CheminGlbSqueletteReference);
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationIdle, "Idle");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationMarche, "Marche");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationCourse, "Course");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationBroutage, "Broutage");
		FusionnerUneSceneAnimationVersBibliothequeFaune(CheminSceneAnimationMort, "Mort");
		FusionnerBibliothequesDepuisGltfExterneMemeRig(); // export ou decouverte auto dans Modeles/Entites/Boeufs/
		FusionnerBibliothequesDepuisDossierAnimationsCompatibles();
	}

	private void DiagnosticListeClipsSiDemande(List<string> tous)
	{
		if (!AfficherDiagnosticClipsUneFois || tous.Count == 0) return;
		if (DiagnosticListeClipsDejaAffichePourProcessus)
			return;
		DiagnosticListeClipsDejaAffichePourProcessus = true;
		_diagnosticClipsAffiche = true;
		GD.Print($"ZERO-K Faune : lecteur {GetPathTo(_animationPlayer)} — {tous.Count} clip(s) : {string.Join(", ", tous)}");
	}

	private void ResoudreNomsClipsLocomotionDepuisBibliothequeEtListe(List<string> tous)
	{
		AppliquerClipsBibliothequeLocomotionFauneEnPriorite();

		string candidatGallopPur = "";
		string candidatCourseGenerique = "";

		foreach (string nomComplet in tous)
		{
			if (EstClipSystemeOuVide(nomComplet)) continue;
			string n = nomComplet.ToLowerInvariant();
			if (string.IsNullOrEmpty(_clipIdle) && !NomClipSembleMort(n) && (n.Contains("idle") || n.Contains("stand") || n.Contains("repos") || n.Contains("survey")))
				_clipIdle = nomComplet;
			if (string.IsNullOrEmpty(_clipMarche) && !NomClipSembleMort(n) && (n.Contains("walk") || n.Contains("marche") || n.Contains("locomotion") || n.Contains("cycle")))
				_clipMarche = nomComplet;
			if (string.IsNullOrEmpty(_clipSautGalop) && NomClipSembleSautGalop(n))
				_clipSautGalop = nomComplet;
			if (string.IsNullOrEmpty(_clipSaut) && n.Contains("jump") && !NomClipSembleSautGalop(n) && !NomClipSembleMort(n))
				_clipSaut = nomComplet;
			if (string.IsNullOrEmpty(_clipAttaqueKick) && !NomClipSembleMort(n) && ResoudreClipSembleAttaqueDerriere(n))
				_clipAttaqueKick = nomComplet;
			if (string.IsNullOrEmpty(_clipAttaqueTete) && !NomClipSembleMort(n) && ResoudreClipSembleAttaqueDevant(n))
				_clipAttaqueTete = nomComplet;
			if (string.IsNullOrEmpty(_clipTrot) && !NomClipSembleMort(n) && (n.Contains("trot") || n.Contains("jog") || n.Contains("lope")))
				_clipTrot = nomComplet;
			if (string.IsNullOrEmpty(_clipNage) && !NomClipSembleMort(n) && (n.Contains("swim") || n.Contains("paddle") || n.Contains("nage")))
				_clipNage = nomComplet;
			if (string.IsNullOrEmpty(_clipManger) && !NomClipSembleMort(n) && (n.Contains("eat") || n.Contains("eating") || n.Contains("graze") || n.Contains("chew") || n.Contains("manger") || n.Contains("browse")))
				_clipManger = nomComplet;
			if (string.IsNullOrEmpty(_clipMort) && (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort")))
				_clipMort = nomComplet;

			bool ressembleCourse = n.Contains("run") || n.Contains("gallop") || n.Contains("course") || n.Contains("charge");
			if (!NomClipSembleMort(n) && ressembleCourse
				&& !NomClipSembleSautGalop(n)
				&& !ResoudreClipSembleAttaqueDevant(n)
				&& !ResoudreClipSembleAttaqueDerriere(n)
				&& !(n.Contains("jump") && !n.Contains("gallop")))
			{
				if (string.IsNullOrEmpty(candidatGallopPur) && n.Contains("gallop") && !NomClipSembleSautGalop(n))
					candidatGallopPur = nomComplet;
				if (string.IsNullOrEmpty(candidatCourseGenerique))
					candidatCourseGenerique = nomComplet;
			}
		}

		if (string.IsNullOrEmpty(_clipCourse))
			_clipCourse = !string.IsNullOrEmpty(candidatGallopPur) ? candidatGallopPur : candidatCourseGenerique;

		if (string.IsNullOrEmpty(_clipMarche) || NomClipSembleMort(_clipMarche))
			_clipMarche = PremierClipLocomotionUtileNonMortel(tous);
		if (string.IsNullOrEmpty(_clipIdle) || NomClipSembleMort(_clipIdle))
			_clipIdle = !string.IsNullOrEmpty(_clipMarche) ? _clipMarche : PremierClipLocomotionUtileNonMortel(tous);
		if (string.IsNullOrEmpty(_clipCourse))
			_clipCourse = _clipMarche;
		if (string.IsNullOrEmpty(_clipTrot))
			_clipTrot = _clipMarche;
		if (string.IsNullOrEmpty(_clipNage))
			_clipNage = _clipCourse;
		if (string.IsNullOrEmpty(_clipManger) || NomClipSembleMort(_clipManger))
		{
			if (!string.IsNullOrEmpty(_clipIdle) && !NomClipSembleMort(_clipIdle))
				_clipManger = _clipIdle;
			else
				_clipManger = !string.IsNullOrEmpty(_clipMarche) ? _clipMarche : PremierClipLocomotionUtileNonMortel(tous);
		}
		if (!string.IsNullOrEmpty(_clipMort) && NomClipSembleMort(_clipManger) && _clipManger == _clipMort)
			_clipManger = !string.IsNullOrEmpty(_clipMarche) ? _clipMarche : _clipIdle;

		// Secours : pack avec un seul clip "kick" / "headbutt" sans mots-cles directionnels.
		if (string.IsNullOrEmpty(_clipAttaqueKick))
		{
			foreach (string nomComplet in tous)
			{
				if (EstClipSystemeOuVide(nomComplet)) continue;
				string n = nomComplet.ToLowerInvariant();
				if (NomClipSembleMort(n) || ResoudreClipSembleAttaqueDevant(n)) continue;
				if (n.Contains("kick") && !n.Contains("walk") && !n.Contains("sidekick"))
				{
					_clipAttaqueKick = nomComplet;
					break;
				}
			}
		}
		if (string.IsNullOrEmpty(_clipAttaqueTete))
		{
			foreach (string nomComplet in tous)
			{
				if (EstClipSystemeOuVide(nomComplet)) continue;
				string n = nomComplet.ToLowerInvariant();
				if (NomClipSembleMort(n)) continue;
				if (n.Contains("headbutt") || (n.Contains("attack") && n.Contains("head")))
				{
					_clipAttaqueTete = nomComplet;
					break;
				}
			}
		}
	}

	private static bool NomClipSembleSautGalop(string n)
	{
		if (string.IsNullOrEmpty(n)) return false;
		return n.Contains("gallop_jump") || n.Contains("gallopjump") || n.Contains("run_jump");
	}

	private static bool ResoudreClipSembleAttaqueDerriere(string n)
	{
		if (n.Contains("headbutt") || (n.Contains("attack") && n.Contains("head")))
			return false;
		return n.Contains("attack_kick") || n.Contains("kick_back") || n.Contains("kick_rear") || n.Contains("rear_kick")
			|| n.Contains("back_kick") || (n.Contains("kick") && (n.Contains("back") || n.Contains("rear") || n.Contains("behind") || n.Contains("derriere")));
	}

	private static bool ResoudreClipSembleAttaqueDevant(string n)
	{
		return n.Contains("headbutt") || n.Contains("attack_head") || n.Contains("attack_headbutt")
			|| (n.Contains("attack") && n.Contains("head")) || n.Contains("coup_de_tete") || n.Contains("ram");
	}

	private void InitialiserSelectionEvolutionnaireAnimations(List<string> tous)
	{
		_poolsAnimationsEvolutives.Clear();
		InitialiserPoolCategorie("idle");
		InitialiserPoolCategorie("walk");
		InitialiserPoolCategorie("run");
		InitialiserPoolCategorie("trot");
		InitialiserPoolCategorie("graze");
		InitialiserPoolCategorie("swim");
		InitialiserPoolCategorie("death");
		InitialiserPoolCategorie("jump");
		InitialiserPoolCategorie("gallop_jump");
		InitialiserPoolCategorie("attack_kick");
		InitialiserPoolCategorie("attack_head");

		AjouterClipAuPool("idle", _clipIdle);
		AjouterClipAuPool("walk", _clipMarche);
		AjouterClipAuPool("run", _clipCourse);
		AjouterClipAuPool("trot", _clipTrot);
		AjouterClipAuPool("graze", _clipManger);
		AjouterClipAuPool("swim", _clipNage);
		AjouterClipAuPool("death", _clipMort);
		AjouterClipAuPool("jump", _clipSaut);
		AjouterClipAuPool("gallop_jump", _clipSautGalop);
		AjouterClipAuPool("attack_kick", _clipAttaqueKick);
		AjouterClipAuPool("attack_head", _clipAttaqueTete);

		if (tous != null)
		{
			foreach (string c in tous)
			{
				if (EstClipSystemeOuVide(c))
					continue;
				string n = c.ToLowerInvariant();
				if (!NomClipSembleMort(n) && (n.Contains("idle") || n.Contains("stand") || n.Contains("repos") || n.Contains("survey")))
					AjouterClipAuPool("idle", c);
				if (!NomClipSembleMort(n) && (n.Contains("walk") || n.Contains("marche") || n.Contains("locomotion") || n.Contains("cycle")))
					AjouterClipAuPool("walk", c);
				bool ressembleCourse = n.Contains("run") || n.Contains("gallop") || n.Contains("course") || n.Contains("charge");
				if (!NomClipSembleMort(n) && ressembleCourse
					&& !NomClipSembleSautGalop(n)
					&& !ResoudreClipSembleAttaqueDevant(n)
					&& !ResoudreClipSembleAttaqueDerriere(n)
					&& !(n.Contains("jump") && !n.Contains("gallop")))
					AjouterClipAuPool("run", c);
				if (!NomClipSembleMort(n) && (n.Contains("trot") || n.Contains("jog") || n.Contains("lope")))
					AjouterClipAuPool("trot", c);
				if (!NomClipSembleMort(n) && (n.Contains("eat") || n.Contains("eating") || n.Contains("graze") || n.Contains("chew") || n.Contains("manger") || n.Contains("browse")))
					AjouterClipAuPool("graze", c);
				if (!NomClipSembleMort(n) && (n.Contains("swim") || n.Contains("paddle") || n.Contains("nage")))
					AjouterClipAuPool("swim", c);
				if (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort"))
					AjouterClipAuPool("death", c);
				if (!NomClipSembleMort(n) && n.Contains("jump") && !NomClipSembleSautGalop(n))
					AjouterClipAuPool("jump", c);
				if (!NomClipSembleMort(n) && NomClipSembleSautGalop(n))
					AjouterClipAuPool("gallop_jump", c);
				if (!NomClipSembleMort(n) && ResoudreClipSembleAttaqueDerriere(n))
					AjouterClipAuPool("attack_kick", c);
				if (!NomClipSembleMort(n) && ResoudreClipSembleAttaqueDevant(n))
					AjouterClipAuPool("attack_head", c);
			}
		}

		ChargerRegistryAnimationsEvolutivesDepuisJson(tous ?? new List<string>());
		RemplirClipsSpeciauxDepuisPoolsSiEncoreVides();
		AppliquerSelectionAnimationEvolutive(forceReconfigurerArbre: false);
		_cooldownVariationAnimation = Mathf.Max(2f, IntervalleVariationAnimationSecondes) * _rng.RandfRange(0.72f, 1.28f);
	}

	private void InitialiserPoolCategorie(string categorie)
	{
		if (!_poolsAnimationsEvolutives.ContainsKey(categorie))
			_poolsAnimationsEvolutives[categorie] = new List<string>();
	}

	private void AjouterClipAuPool(string categorie, string clip)
	{
		if (string.IsNullOrWhiteSpace(clip) || _animationPlayer == null || !_animationPlayer.HasAnimation(clip))
			return;
		InitialiserPoolCategorie(categorie);
		List<string> pool = _poolsAnimationsEvolutives[categorie];
		if (!pool.Contains(clip))
			pool.Add(clip);
	}

	/// <summary>Apres le JSON <see cref="CheminRegistryAnimationsFaune"/>, complete les clips sauts / attaques si la detection par nom seul les a rates.</summary>
	private void RemplirClipsSpeciauxDepuisPoolsSiEncoreVides()
	{
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
			return;

		void ChercherPremierClipValide(string categorie, ref string cible)
		{
			if (!string.IsNullOrEmpty(cible) && _animationPlayer.HasAnimation(cible))
				return;
			if (!_poolsAnimationsEvolutives.TryGetValue(categorie, out List<string> pool) || pool == null)
				return;
			for (int i = 0; i < pool.Count; i++)
			{
				string clip = pool[i];
				if (!string.IsNullOrEmpty(clip) && _animationPlayer.HasAnimation(clip))
				{
					cible = clip;
					return;
				}
			}
		}

		ChercherPremierClipValide("jump", ref _clipSaut);
		ChercherPremierClipValide("gallop_jump", ref _clipSautGalop);
		ChercherPremierClipValide("attack_kick", ref _clipAttaqueKick);
		ChercherPremierClipValide("attack_head", ref _clipAttaqueTete);
	}

	private void ChargerRegistryAnimationsEvolutivesDepuisJson(List<string> tous)
	{
		if (!ActiverSelectionEvolutionnaireAnimations || string.IsNullOrWhiteSpace(CheminRegistryAnimationsFaune))
			return;
		if (!FileAccess.FileExists(CheminRegistryAnimationsFaune))
			return;

		try
		{
			string contenu = FileAccess.GetFileAsString(CheminRegistryAnimationsFaune);
			if (string.IsNullOrWhiteSpace(contenu))
				return;
			using JsonDocument doc = JsonDocument.Parse(contenu);
			if (doc.RootElement.ValueKind != JsonValueKind.Object)
				return;
			if (!doc.RootElement.TryGetProperty("categories", out JsonElement categories) || categories.ValueKind != JsonValueKind.Object)
				return;

			foreach (JsonProperty entree in categories.EnumerateObject())
			{
				string categorie = entree.Name.ToLowerInvariant();
				if (entree.Value.ValueKind != JsonValueKind.Array)
					continue;
				foreach (JsonElement item in entree.Value.EnumerateArray())
				{
					if (item.ValueKind != JsonValueKind.String)
						continue;
					string motif = (item.GetString() ?? "").Trim();
					if (string.IsNullOrEmpty(motif))
						continue;
					string clip = ResoudreNomClipDepuisMotif(motif, tous);
					if (!string.IsNullOrEmpty(clip))
						AjouterClipAuPool(categorie, clip);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K Faune : registre d'animations invalide ({CheminRegistryAnimationsFaune}) : {ex.Message}");
		}
	}

	private static string ResoudreNomClipDepuisMotif(string motif, List<string> tous)
	{
		if (string.IsNullOrWhiteSpace(motif) || tous == null || tous.Count == 0)
			return "";
		for (int i = 0; i < tous.Count; i++)
		{
			if (string.Equals(tous[i], motif, StringComparison.OrdinalIgnoreCase))
				return tous[i];
		}

		string m = motif.ToLowerInvariant();
		string mUnderscore = m.Replace(" ", "_");
		for (int i = 0; i < tous.Count; i++)
		{
			string c = tous[i];
			if (c.EndsWith("/" + motif, StringComparison.OrdinalIgnoreCase))
				return c;
			string n = c.ToLowerInvariant();
			if (n.Contains(m, StringComparison.Ordinal))
				return c;
			if (mUnderscore != m && n.Contains(mUnderscore, StringComparison.Ordinal))
				return c;
		}
		return "";
	}

	private void AppliquerSelectionAnimationEvolutive(bool forceReconfigurerArbre)
	{
		if (!ActiverSelectionEvolutionnaireAnimations || _animationPlayer == null)
			return;

		float stress = (_tempsFuite > 0f || _memoireDetectionJoueur > 0f) ? 1f : 0f;
		float faim = RatioFaimCourant();
		float cohesion = CalculerRatioCohesionTroupeau();
		float intensite = Mathf.Clamp(IntensiteSelectionAnimationEvolutive, 0f, 1f);

		float scoreCalme = Mathf.Clamp(_geneConfiance * 0.55f + faim * 0.30f + cohesion * 0.15f - stress * 0.35f, 0f, 1f);
		float scoreDynamique = Mathf.Clamp(_geneReflexeAttaque * 0.45f + _geneReflexeFuite * 0.35f + stress * 0.30f, 0f, 1f);
		float scoreBroutage = Mathf.Clamp(faim * 0.55f + _geneConfiance * 0.30f - stress * 0.45f, 0f, 1f);
		float scoreNage = Mathf.Clamp(stress * 0.30f + (1f - faim) * 0.25f + _geneReflexeFuite * 0.25f + cohesion * 0.20f, 0f, 1f);

		string ancienIdle = _clipIdle;
		string ancienMarche = _clipMarche;
		string ancienCourse = _clipCourse;
		string ancienTrot = _clipTrot;
		string ancienManger = _clipManger;
		string ancienNage = _clipNage;

		bool idleMultiples = _poolsAnimationsEvolutives.TryGetValue("idle", out List<string> poolIdle) && poolIdle != null && poolIdle.Count >= 2;
		if (!idleMultiples)
			_clipIdle = ChoisirClipDepuisPoolEvolutif("idle", _clipIdle, scoreCalme * intensite + (1f - intensite) * 0.5f);
		_clipMarche = ChoisirClipDepuisPoolEvolutif("walk", _clipMarche, (scoreCalme * 0.45f + scoreDynamique * 0.55f) * intensite + (1f - intensite) * 0.5f);
		_clipCourse = ChoisirClipDepuisPoolEvolutif("run", _clipCourse, scoreDynamique * intensite + (1f - intensite) * 0.5f);
		_clipTrot = ChoisirClipDepuisPoolEvolutif("trot", _clipTrot, (scoreCalme * 0.3f + scoreDynamique * 0.7f) * intensite + (1f - intensite) * 0.5f);
		_clipManger = ChoisirClipDepuisPoolEvolutif("graze", _clipManger, scoreBroutage * intensite + (1f - intensite) * 0.5f);
		_clipNage = ChoisirClipDepuisPoolEvolutif("swim", _clipNage, scoreNage * intensite + (1f - intensite) * 0.5f);
		_clipMort = ChoisirClipDepuisPoolEvolutif("death", _clipMort, 0.5f);

		bool change =
			(!idleMultiples && ancienIdle != _clipIdle) ||
			ancienMarche != _clipMarche ||
			ancienCourse != _clipCourse ||
			ancienTrot != _clipTrot ||
			ancienManger != _clipManger ||
			ancienNage != _clipNage;

		if (!change)
			return;

		AppliquerBouclesSurClipsLocomotion();
		if (forceReconfigurerArbre && UtiliserAnimationTreeLocomotion)
			DemanderReconfigurationAnimationTree();
	}

	private string ChoisirClipDepuisPoolEvolutif(string categorie, string fallback, float score)
	{
		if (!_poolsAnimationsEvolutives.TryGetValue(categorie, out List<string> pool) || pool.Count == 0)
			return fallback;
		if (pool.Count == 1)
			return pool[0];

		float s = Mathf.Clamp(score, 0f, 1f);
		float bruit = _rng.RandfRange(-0.18f, 0.18f);
		s = Mathf.Clamp(s + bruit, 0f, 1f);
		int idx = Mathf.Clamp(Mathf.RoundToInt(s * (pool.Count - 1)), 0, pool.Count - 1);
		return pool[idx];
	}

	/// <summary>Nœud AnimationTree placé dans la scène (éditeur) : nom attendu, typo fréquente, ou premier enfant direct.</summary>
	private AnimationTree TrouverAnimationTreeConfigureDansLaScene()
	{
		AnimationTree t = GetNodeOrNull<AnimationTree>(NomNoeudAnimationTreeFauneEditeur);
		if (t != null && GodotObject.IsInstanceValid(t))
			return t;
		t = GetNodeOrNull<AnimationTree>(NomNoeudAnimationTreeFauTypo);
		if (t != null && GodotObject.IsInstanceValid(t))
			return t;
		foreach (Node c in GetChildren())
		{
			if (c is AnimationTree at && GodotObject.IsInstanceValid(at))
				return at;
		}
		return null;
	}

	private void DemarrerArbreAnimationOuLectureDirecte()
	{
		if (UtiliserAnimationTreeLocomotion)
			ConfigurerAnimationTreeFaune();
		else
		{
			DetruireAnimationTreeFaune();
			_animationPlayer.ProcessMode = ProcessModeEnum.Always;
			_animationPlayer.Active = true;
			_animationPlayer.Play(new StringName(_clipIdle), 0.12f);
		}
	}

	private void DemanderReconfigurationAnimationTree()
	{
		if (!UtiliserAnimationTreeLocomotion)
			return;
		if (_cooldownReconfigurationArbreAnimation <= 0f)
		{
			ConfigurerAnimationTreeFaune();
			_cooldownReconfigurationArbreAnimation = Mathf.Max(0.05f, CooldownReconfigurationAnimationTreeSec);
			_reconfigurationArbreAnimationEnAttente = false;
			return;
		}
		_reconfigurationArbreAnimationEnAttente = true;
	}

	private void ConfigurerAnimationTreeFaune()
	{
		DetruireAnimationTreeFaune();
		if (_animationPlayer == null)
			return;

		AnimationTree arbreEditeur = TrouverAnimationTreeConfigureDansLaScene();
		if (arbreEditeur != null && GodotObject.IsInstanceValid(arbreEditeur))
		{
			_animationTreeFaune = arbreEditeur;
			_animationTreeCreeParScript = false;
			if (_animationTreeFaune.Name != NomNoeudAnimationTreeFauneEditeur)
				_animationTreeFaune.Name = NomNoeudAnimationTreeFauneEditeur;
		}
		else
		{
			_animationTreeFaune = new AnimationTree { Name = NomNoeudAnimationTreeFauneEditeur };
			Node parentArbre = _animationPlayer.GetParent() ?? _modeleVisuel ?? (Node)this;
			parentArbre.AddChild(_animationTreeFaune);
			_animationTreeCreeParScript = true;
		}

		var blend = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
		blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipIdle) }, 0f);
		bool marcheDiff = !string.IsNullOrEmpty(_clipMarche) && _clipMarche != _clipIdle;
		bool courseDiff = !string.IsNullOrEmpty(_clipCourse) && _clipCourse != _clipMarche;
		bool trotDiff = !string.IsNullOrEmpty(_clipTrot) && _clipTrot != _clipMarche && _clipTrot != _clipCourse;
		if (marcheDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipMarche) }, 0.55f);
		if (trotDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipTrot) }, 0.78f);
		if (courseDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipCourse) }, 1f);
		else if (!marcheDiff)
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipIdle) }, 1f);
		else
			blend.AddBlendPoint(new AnimationNodeAnimation { Animation = new StringName(_clipMarche) }, 1f);

		var machine = new AnimationNodeStateMachine();
		machine.AddNode(NomNoeudDeplacement, blend, new Vector2(220f, 120f));

		bool PorteClip(string c) => !string.IsNullOrEmpty(c) && _animationPlayer.HasAnimation(c);

		_machineAPorteBroutage = !string.IsNullOrEmpty(_clipManger) && _clipManger != _clipIdle;
		_machineAPorteMort = !string.IsNullOrEmpty(_clipMort);
		_machineAPorteNage = !string.IsNullOrEmpty(_clipNage) && _clipNage != _clipMarche && _clipNage != _clipCourse;
		_machineAPorteSaut = PorteClip(_clipSaut);
		_machineAPorteSautGalop = PorteClip(_clipSautGalop);
		_machineAPorteAttaqueKick = PorteClip(_clipAttaqueKick);
		_machineAPorteAttaqueTete = PorteClip(_clipAttaqueTete);

		if (_machineAPorteBroutage)
			machine.AddNode(NomNoeudBroutage, new AnimationNodeAnimation { Animation = new StringName(_clipManger) }, new Vector2(460f, 40f));
		if (_machineAPorteMort)
			machine.AddNode(NomNoeudMort, new AnimationNodeAnimation { Animation = new StringName(_clipMort) }, new Vector2(460f, 220f));
		if (_machineAPorteNage)
			machine.AddNode("Nage", new AnimationNodeAnimation { Animation = new StringName(_clipNage) }, new Vector2(460f, 320f));
		if (_machineAPorteSaut)
			machine.AddNode(NomNoeudSaut, new AnimationNodeAnimation { Animation = new StringName(_clipSaut) }, new Vector2(40f, 0f));
		if (_machineAPorteSautGalop)
			machine.AddNode(NomNoeudSautGalop, new AnimationNodeAnimation { Animation = new StringName(_clipSautGalop) }, new Vector2(40f, 72f));
		if (_machineAPorteAttaqueKick)
			machine.AddNode(NomNoeudAttaqueKick, new AnimationNodeAnimation { Animation = new StringName(_clipAttaqueKick) }, new Vector2(680f, 100f));
		if (_machineAPorteAttaqueTete)
			machine.AddNode(NomNoeudAttaqueTete, new AnimationNodeAnimation { Animation = new StringName(_clipAttaqueTete) }, new Vector2(680f, 200f));

		const float xfade = 0.14f;
		var depuisStart = new AnimationNodeStateMachineTransition
		{
			XfadeTime = xfade,
			SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate
		};
		machine.AddTransition("Start", NomNoeudDeplacement, depuisStart);

		var allerBroutage = new AnimationNodeStateMachineTransition { XfadeTime = xfade };
		var quitterBroutage = new AnimationNodeStateMachineTransition { XfadeTime = xfade };
		if (_machineAPorteBroutage)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudBroutage, allerBroutage);
			machine.AddTransition(NomNoeudBroutage, NomNoeudDeplacement, quitterBroutage);
		}
		if (_machineAPorteNage)
		{
			machine.AddTransition(NomNoeudDeplacement, "Nage", new AnimationNodeStateMachineTransition { XfadeTime = 0.12f });
			machine.AddTransition("Nage", NomNoeudDeplacement, new AnimationNodeStateMachineTransition { XfadeTime = 0.12f });
		}

		var xfdSaut = new AnimationNodeStateMachineTransition { XfadeTime = 0.11f };
		var xfdSautRetour = new AnimationNodeStateMachineTransition { XfadeTime = 0.14f };
		if (_machineAPorteSaut)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudSaut, xfdSaut);
			machine.AddTransition(NomNoeudSaut, NomNoeudDeplacement, xfdSautRetour);
		}
		if (_machineAPorteSautGalop)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudSautGalop, xfdSaut);
			machine.AddTransition(NomNoeudSautGalop, NomNoeudDeplacement, xfdSautRetour);
		}
		var xfdAttaque = new AnimationNodeStateMachineTransition { XfadeTime = 0.08f };
		var xfdAttaqueRetour = new AnimationNodeStateMachineTransition { XfadeTime = 0.1f };
		if (_machineAPorteAttaqueKick)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudAttaqueKick, xfdAttaque);
			machine.AddTransition(NomNoeudAttaqueKick, NomNoeudDeplacement, xfdAttaqueRetour);
		}
		if (_machineAPorteAttaqueTete)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudAttaqueTete, xfdAttaque);
			machine.AddTransition(NomNoeudAttaqueTete, NomNoeudDeplacement, xfdAttaqueRetour);
		}

		var versMort = new AnimationNodeStateMachineTransition { XfadeTime = 0.1f, SwitchMode = AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate };
		if (_machineAPorteMort)
		{
			machine.AddTransition(NomNoeudDeplacement, NomNoeudMort, versMort);
			if (_machineAPorteBroutage)
				machine.AddTransition(NomNoeudBroutage, NomNoeudMort, versMort);
			if (_machineAPorteSaut)
				machine.AddTransition(NomNoeudSaut, NomNoeudMort, versMort);
			if (_machineAPorteSautGalop)
				machine.AddTransition(NomNoeudSautGalop, NomNoeudMort, versMort);
			if (_machineAPorteAttaqueKick)
				machine.AddTransition(NomNoeudAttaqueKick, NomNoeudMort, versMort);
			if (_machineAPorteAttaqueTete)
				machine.AddTransition(NomNoeudAttaqueTete, NomNoeudMort, versMort);
		}

		_animationTreeFaune.ProcessMode = ProcessModeEnum.Always;
		_animationTreeFaune.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
		_animationTreeFaune.TreeRoot = machine;
		_animationTreeFaune.AnimPlayer = _animationTreeFaune.GetPathTo(_animationPlayer);
		_animationTreeFaune.Active = false;
		_blendLocomotionActif = false;
		_playbackEtatFaune = null;
		_etatCourantMachineAnimation = "";
		_tentativesLiaisonPlaybackArbre = 0;
		Callable.From(ApresAnimationTreePretFaune).CallDeferred();
	}

	private void ApresAnimationTreePretFaune()
	{
		if (_animationTreeFaune == null || !GodotObject.IsInstanceValid(_animationTreeFaune) || _animationPlayer == null)
			return;

		_animationTreeFaune.Active = true;
		_playbackEtatFaune = ExtrairePlaybackMachineEtatFaune();
		if (_playbackEtatFaune == null)
		{
			_animationTreeFaune.Active = false;
			if (_tentativesLiaisonPlaybackArbre++ > MaxTentativesLiaisonPlaybackArbre)
			{
				GD.PrintErr("ZERO-K Faune : AnimationTree — playback introuvable apres plusieurs frames, bascule lecture directe (desactivez UtiliserAnimationTreeLocomotion si besoin).");
				DetruireAnimationTreeFaune();
				if (!string.IsNullOrEmpty(_clipIdle))
					_animationPlayer.Play(new StringName(_clipIdle), 0.12f);
				return;
			}

			Callable.From(ApresAnimationTreePretFaune).CallDeferred();
			return;
		}

		_tentativesLiaisonPlaybackArbre = 0;
		_blendLocomotionActif = true;
		_playbackEtatFaune.Start(NomNoeudDeplacementString);
		_etatCourantMachineAnimation = NomNoeudDeplacement;
	}

	private AnimationNodeStateMachinePlayback ExtrairePlaybackMachineEtatFaune()
	{
		if (_animationTreeFaune == null) return null;
		Variant v = _animationTreeFaune.Get("parameters/playback");
		if (v.VariantType == Variant.Type.Nil) return null;
		return v.AsGodotObject() as AnimationNodeStateMachinePlayback;
	}

	private void DetruireAnimationTreeFaune()
	{
		if (_animationTreeFaune != null && GodotObject.IsInstanceValid(_animationTreeFaune))
		{
			_animationTreeFaune.Active = false;
			_animationTreeFaune.TreeRoot = null;
			if (_animationTreeCreeParScript)
				_animationTreeFaune.QueueFree();
		}

		_animationTreeFaune = null;
		_playbackEtatFaune = null;
		_etatCourantMachineAnimation = "";
		_blendLocomotionActif = false;
		_animationTreeCreeParScript = false;
		_dernierBlendAnimation = float.NaN;
		_derniereVitesseAnimation = float.NaN;
	}

	private StringName ObtenirStringNameAnimation(string nom)
	{
		if (string.IsNullOrEmpty(nom)) return default;
		if (_cacheStringNameAnimations.TryGetValue(nom, out StringName value))
			return value;
		value = new StringName(nom);
		_cacheStringNameAnimations[nom] = value;
		return value;
	}

	private static StringName ObtenirNomEtatAnimation(string etat)
	{
		return etat switch
		{
			NomNoeudDeplacement => NomNoeudDeplacementString,
			NomNoeudBroutage => NomNoeudBroutageString,
			NomNoeudMort => NomNoeudMortString,
			"Nage" => NomNoeudNageString,
			NomNoeudSaut => NomNoeudSautString,
			NomNoeudSautGalop => NomNoeudSautGalopString,
			NomNoeudAttaqueKick => NomNoeudAttaqueKickString,
			NomNoeudAttaqueTete => NomNoeudAttaqueTeteString,
			_ => new StringName(etat)
		};
	}

	/// <summary>En errance calme, enchaine les clips <c>idle</c> du pool (ex. 5 variantes) sans toucher marche/course.</summary>
	private void MettreAJourCycleIdleMultiples(float dt, float vitesseHoriz)
	{
		if (_animationPlayer == null || !GodotObject.IsInstanceValid(_animationPlayer))
			return;
		if (!_poolsAnimationsEvolutives.TryGetValue("idle", out List<string> pool) || pool == null || pool.Count < 2)
			return;
		bool calme = _etat != EtatBoeuf.Fuite && _etat != EtatBoeuf.Charge && _etat != EtatBoeuf.Broutage
			&& vitesseHoriz <= 0.2f && !_dansEau && _tempsVerrouAnimationCombat <= 0f;
		if (!calme)
			return;
		_timerCycleIdleSecondes -= dt;
		if (_timerCycleIdleSecondes > 0f)
			return;
		float imin = Mathf.Max(1.5f, IntervalleMinCycleIdleSecondes);
		float imax = Mathf.Max(imin + 0.5f, IntervalleMaxCycleIdleSecondes);
		_timerCycleIdleSecondes = _rng.RandfRange(imin, imax);
		_indexCycleIdle = (_indexCycleIdle + 1) % pool.Count;
		string suivant = pool[_indexCycleIdle];
		if (string.IsNullOrEmpty(suivant) || suivant == _clipIdle)
			return;
		_clipIdle = suivant;
		AppliquerBouclesSurClipsLocomotion();
		if (UtiliserAnimationTreeLocomotion)
			DemanderReconfigurationAnimationTree();
	}

	private void MettreAJourAnimation(float dt, float vitesseHoriz)
	{
		if (_animationPlayer == null) return;
		_tempsVerrouAnimationCombat = Mathf.Max(0f, _tempsVerrouAnimationCombat - dt);
		if (_tempsVerrouAnimationCombat <= 0f && !string.IsNullOrEmpty(_noeudAnimationCombatVerrou)
			&& _blendLocomotionActif && _playbackEtatFaune != null && _animationTreeFaune != null && _animationTreeFaune.Active)
		{
			_playbackEtatFaune.Travel(NomNoeudDeplacementString);
			_etatCourantMachineAnimation = NomNoeudDeplacement;
			_noeudAnimationCombatVerrou = "";
		}

		float vitesseMarcheActuelle = VitesseMarche * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
		float vitesseFuiteActuelle = VitesseFuite * MultiplicateurNiveau * (VitesseStatActuelle / Mathf.Max(0.1f, VitesseBase));
		float seuilIdle = 0.12f;
		float seuilMarche = 0.25f;
		bool sautAscendant = !_dansEau && !IsOnFloor()
			&& (_fenetreAnimSautStrategique > 0f || Velocity.Y > 0.42f);
		bool sprintAnime = (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge) && SprintAutoriseParStamina();
		bool clipsSautDedies = _machineAPorteSaut || _machineAPorteSautGalop;

		if (_blendLocomotionActif && _playbackEtatFaune != null && _animationTreeFaune != null && _animationTreeFaune.Active)
		{
			string etatVoulu = NomNoeudDeplacement;
			if (_dansEau && _machineAPorteNage)
				etatVoulu = "Nage";
			else if (!_dansEau && _etat == EtatBoeuf.Broutage && _machineAPorteBroutage)
				etatVoulu = NomNoeudBroutage;
			else if (_tempsVerrouAnimationCombat > 0f && !string.IsNullOrEmpty(_noeudAnimationCombatVerrou))
			{
				if (_noeudAnimationCombatVerrou == NomNoeudAttaqueKick && _machineAPorteAttaqueKick)
					etatVoulu = NomNoeudAttaqueKick;
				else if (_noeudAnimationCombatVerrou == NomNoeudAttaqueTete && _machineAPorteAttaqueTete)
					etatVoulu = NomNoeudAttaqueTete;
			}
			else if (!_dansEau && sautAscendant && sprintAnime && _machineAPorteSautGalop)
				etatVoulu = NomNoeudSautGalop;
			else if (!_dansEau && sautAscendant && _machineAPorteSaut && (!_machineAPorteSautGalop || !sprintAnime))
				etatVoulu = NomNoeudSaut;
			else if (!_dansEau && IsOnFloor() && (_etatCourantMachineAnimation == NomNoeudSaut || _etatCourantMachineAnimation == NomNoeudSautGalop))
				etatVoulu = NomNoeudDeplacement;

			if (etatVoulu != _etatCourantMachineAnimation)
			{
				_playbackEtatFaune.Travel(ObtenirNomEtatAnimation(etatVoulu));
				_etatCourantMachineAnimation = etatVoulu;
			}

			if (etatVoulu == NomNoeudDeplacement && _animationTreeFaune != null)
			{
				float blend = 0f;
				if (sprintAnime)
					blend = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.01f, vitesseFuiteActuelle), 0f, 1f);
				else if (vitesseHoriz > seuilMarche)
					blend = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.01f, vitesseMarcheActuelle) * 0.65f, 0f, 0.95f);
				if (sautAscendant && !clipsSautDedies)
				{
					blend = vitesseHoriz > seuilMarche ? Mathf.Max(blend, 0.80f) : 0f;
				}
				if (IntensiteMicroVivaciteAnimation > 0.0001f && _etat != EtatBoeuf.Fuite && _etat != EtatBoeuf.Charge && vitesseHoriz > seuilIdle && vitesseHoriz < 0.38f)
				{
					float phase = (float)_ageSecondes * 0.85f + (GetInstanceId() & 2047) * 0.0015f;
					blend = Mathf.Clamp(blend + IntensiteMicroVivaciteAnimation * 0.12f * Mathf.Sin(phase), 0f, 0.98f);
				}
				if (float.IsNaN(_dernierBlendAnimation) || Mathf.Abs(_dernierBlendAnimation - blend) > 0.0001f)
				{
					_animationTreeFaune.Set(ParamBlendDeplacement, blend);
					_dernierBlendAnimation = blend;
				}
			}

			float speed = 1f;
			if (_dansEau)
				speed = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, VitesseNageHorizontale), 0.75f, 1.25f);
			else if (etatVoulu == NomNoeudBroutage)
				speed = 0.9f;
			else if (etatVoulu == NomNoeudSautGalop || (etatVoulu == NomNoeudDeplacement && sprintAnime))
				speed = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseFuiteActuelle), 0.85f, 1.75f);
			else if (etatVoulu == NomNoeudSaut)
				speed = vitesseHoriz > seuilMarche ? Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseMarcheActuelle), 0.85f, 1.35f) : 0.92f;
			else if (etatVoulu == NomNoeudAttaqueKick || etatVoulu == NomNoeudAttaqueTete)
				speed = 1f;
			else if (vitesseHoriz > seuilMarche)
				speed = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseMarcheActuelle), 0.8f, 1.45f);
			if (sautAscendant && !clipsSautDedies && etatVoulu == NomNoeudDeplacement)
				speed = vitesseHoriz > seuilMarche ? Mathf.Max(speed, 1.05f) : 0.92f;
			if (IntensiteMicroVivaciteAnimation > 0.0001f && etatVoulu == NomNoeudDeplacement && vitesseHoriz > seuilIdle)
			{
				float phase2 = (float)_ageSecondes * 1.9f + (GetInstanceId() & 1023) * 0.002f;
				speed *= 1f + IntensiteMicroVivaciteAnimation * (0.55f * Mathf.Sin(phase2) + 0.35f * Mathf.Sin(phase2 * 1.7f));
			}
			float vitesseAppliquee = speed * Mathf.Clamp(MultiplicateurVitesseAnimation, 0.2f, 2.0f) * FacteurAnimationContextuelle();
			if (float.IsNaN(_derniereVitesseAnimation) || Mathf.Abs(_derniereVitesseAnimation - vitesseAppliquee) > 0.0001f)
			{
				_animationPlayer.SpeedScale = vitesseAppliquee;
				_derniereVitesseAnimation = vitesseAppliquee;
			}
			return;
		}

		string cible = _clipIdle;
		float speedDirect = 1f;
		if (_dansEau)
		{
			cible = !string.IsNullOrEmpty(_clipNage) ? _clipNage : (!string.IsNullOrEmpty(_clipCourse) ? _clipCourse : (!string.IsNullOrEmpty(_clipMarche) ? _clipMarche : _clipIdle));
			speedDirect = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, VitesseNageHorizontale), 0.75f, 1.25f);
		}
		else if (_etat == EtatBoeuf.Broutage)
		{
			cible = !string.IsNullOrEmpty(_clipManger) ? _clipManger : _clipIdle;
			speedDirect = 0.9f;
		}
		else if ((_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge) && SprintAutoriseParStamina())
		{
			cible = !string.IsNullOrEmpty(_clipCourse) ? _clipCourse : (!string.IsNullOrEmpty(_clipTrot) ? _clipTrot : _clipMarche);
			speedDirect = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseFuiteActuelle), 0.85f, 1.75f);
		}
		else if (sautAscendant)
		{
			bool sautAvecElan = vitesseHoriz > seuilMarche;
			if (sprintAnime && !string.IsNullOrEmpty(_clipSautGalop) && _animationPlayer.HasAnimation(_clipSautGalop))
			{
				cible = _clipSautGalop;
				speedDirect = 1.12f;
			}
			else if (!string.IsNullOrEmpty(_clipSaut) && _animationPlayer.HasAnimation(_clipSaut))
			{
				cible = _clipSaut;
				speedDirect = sautAvecElan ? 1.05f : 0.92f;
			}
			else
			{
				cible = sautAvecElan
					? (!string.IsNullOrEmpty(_clipCourse) ? _clipCourse : (!string.IsNullOrEmpty(_clipTrot) ? _clipTrot : _clipMarche))
					: _clipIdle;
				speedDirect = sautAvecElan ? 1.05f : 0.92f;
			}
		}
		else if (vitesseHoriz > seuilMarche)
		{
			cible = !string.IsNullOrEmpty(_clipTrot) && vitesseHoriz > vitesseMarcheActuelle * 0.74f ? _clipTrot : (!string.IsNullOrEmpty(_clipMarche) ? _clipMarche : _clipIdle);
			speedDirect = Mathf.Clamp(vitesseHoriz / Mathf.Max(0.1f, vitesseMarcheActuelle), 0.8f, 1.45f);
		}

		if (!string.IsNullOrEmpty(cible))
		{
			StringName nom = ObtenirStringNameAnimation(cible);
			if (_animationPlayer.CurrentAnimation != nom || !_animationPlayer.IsPlaying())
				_animationPlayer.Play(nom, 0.16f);
		}
		float vitesseDirecte = speedDirect * Mathf.Clamp(MultiplicateurVitesseAnimation, 0.2f, 2.0f) * FacteurAnimationContextuelle();
		if (float.IsNaN(_derniereVitesseAnimation) || Mathf.Abs(_derniereVitesseAnimation - vitesseDirecte) > 0.0001f)
		{
			_animationPlayer.SpeedScale = vitesseDirecte;
			_derniereVitesseAnimation = vitesseDirecte;
		}
	}

	private void PreparerLecteurEtBibliothequeLocomotionFaune()
	{
		if (_animationPlayer == null) return;
		if (!_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			_animationPlayer.AddAnimationLibrary(NomBibliothequeLocomotionFaune, new AnimationLibrary());
		_animationPlayer.RootNode = new NodePath("..");
		_animationPlayer.ProcessMode = ProcessModeEnum.Always;
		_animationPlayer.Active = true;
	}

	private void FusionnerAnimationsRemappeesDepuisSceneReference(string cheminScene)
	{
		if (_animationPlayer == null || _modeleVisuel == null) return;
		if (string.IsNullOrWhiteSpace(cheminScene) || !ResourceLoader.Exists(cheminScene)) return;

		var sc = GD.Load<PackedScene>(cheminScene);
		Node temp = sc?.Instantiate();
		if (temp == null) return;

		try
		{
			AnimationPlayer apRef = ChoisirMeilleurAnimationPlayer(temp);
			Skeleton3D skRef = TrouverPremierNoeudDeType<Skeleton3D>(temp);
			Skeleton3D skLive = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel);
			if (apRef == null || skRef == null || skLive == null)
				return;

			Node racineRef = apRef.GetParent() ?? temp;
			Node racineLive = _animationPlayer.GetParent() ?? _modeleVisuel;
			string prefixRef = racineRef.GetPathTo(skRef).ToString();
			string prefixLive = racineLive.GetPathTo(skLive).ToString();

			AnimationLibrary libLoc = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
			if (libLoc == null) return;

			foreach (string nomComplet in CollecterCheminsAnimation(apRef))
			{
				if (EstClipSystemeOuVide(nomComplet)) continue;
				string std = DeriverNomStandardClipOuNull(nomComplet);
				if (std == null) continue;
				if (libLoc.HasAnimation(std)) continue;

				Animation source = apRef.GetAnimation(new StringName(nomComplet));
				if (source == null) continue;
				var anim = (Animation)source.Duplicate(true);
				RemapperPrefixSquelette(anim, prefixRef, prefixLive);
				RemapperCheminsParMarqueurSquelette(anim, prefixLive);
				anim.LoopMode = Animation.LoopModeEnum.Linear;
				libLoc.AddAnimation(std, anim);
			}

			GD.Print($"ZERO-K Faune : pistes remappees depuis {cheminScene} vers {NomBibliothequeLocomotionFaune} ({prefixRef} -> {prefixLive}).");
		}
		finally
		{
			temp.QueueFree();
		}
	}

	private void FusionnerUneSceneAnimationVersBibliothequeFaune(string cheminScene, string nomClipStandard)
	{
		if (_animationPlayer == null || _modeleVisuel == null) return;
		if (string.IsNullOrWhiteSpace(cheminScene) || !ResourceLoader.Exists(cheminScene)) return;
		if (!_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune)) return;

		AnimationLibrary libLoc = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
		if (libLoc == null || libLoc.HasAnimation(nomClipStandard)) return;

		var sc = GD.Load<PackedScene>(cheminScene);
		Node temp = sc?.Instantiate();
		if (temp == null) return;

		try
		{
			AnimationPlayer apExt = ChoisirMeilleurAnimationPlayer(temp);
			Skeleton3D skExt = TrouverPremierNoeudDeType<Skeleton3D>(temp);
			Skeleton3D skLive = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel);
			if (apExt == null || skExt == null || skLive == null)
				return;

			Node racineExt = apExt.GetParent() ?? temp;
			Node racineLive = _animationPlayer.GetParent() ?? _modeleVisuel;
			string prefixExt = racineExt.GetPathTo(skExt).ToString();
			string prefixLive = racineLive.GetPathTo(skLive).ToString();

			Animation anim = ExtrairePremiereAnimationDepuisLecteur(apExt);
			if (anim == null) return;

			if (nomClipStandard is "Idle" or "Marche" or "Broutage")
				anim.LoopMode = Animation.LoopModeEnum.Linear;

			RemapperPrefixSquelette(anim, prefixExt, prefixLive);
			RemapperCheminsParMarqueurSquelette(anim, prefixLive);
			libLoc.AddAnimation(nomClipStandard, anim);
			GD.Print($"ZERO-K Faune : scene {cheminScene} -> {NomBibliothequeLocomotionFaune}/{nomClipStandard}.");
		}
		finally
		{
			temp.QueueFree();
		}
	}

	private void AppliquerClipsBibliothequeLocomotionFauneEnPriorite()
	{
		if (_animationPlayer == null || !_animationPlayer.HasAnimationLibrary(NomBibliothequeLocomotionFaune))
			return;
		AnimationLibrary lib = _animationPlayer.GetAnimationLibrary(NomBibliothequeLocomotionFaune);
		if (lib == null) return;

		string Pref(string c) => $"{NomBibliothequeLocomotionFaune}/{c}";
		if (lib.HasAnimation("Idle")) _clipIdle = Pref("Idle");
		if (lib.HasAnimation("Marche")) _clipMarche = Pref("Marche");
		if (lib.HasAnimation("Course")) _clipCourse = Pref("Course");
		if (lib.HasAnimation("Broutage")) _clipManger = Pref("Broutage");
		if (lib.HasAnimation("Mort")) _clipMort = Pref("Mort");
		if (lib.HasAnimation("Walk") && string.IsNullOrEmpty(_clipMarche)) _clipMarche = Pref("Walk");
		if (lib.HasAnimation("Gallop") && string.IsNullOrEmpty(_clipCourse)) _clipCourse = Pref("Gallop");
		if (lib.HasAnimation("Jump") && string.IsNullOrEmpty(_clipSaut)) _clipSaut = Pref("Jump");
		if (lib.HasAnimation("GallopJump") && string.IsNullOrEmpty(_clipSautGalop)) _clipSautGalop = Pref("GallopJump");
		if (lib.HasAnimation("Eating") && string.IsNullOrEmpty(_clipManger)) _clipManger = Pref("Eating");
		if (lib.HasAnimation("AttaqueKick") && string.IsNullOrEmpty(_clipAttaqueKick)) _clipAttaqueKick = Pref("AttaqueKick");
		if (lib.HasAnimation("AttaqueTete") && string.IsNullOrEmpty(_clipAttaqueTete)) _clipAttaqueTete = Pref("AttaqueTete");
	}

	private static T TrouverPremierNoeudDeType<T>(Node racine) where T : Node
	{
		if (racine == null) return null;
		if (racine is T t) return t;
		foreach (Node enfant in racine.GetChildren())
		{
			T trouve = TrouverPremierNoeudDeType<T>(enfant);
			if (trouve != null) return trouve;
		}
		return null;
	}

	private static Animation ExtrairePremiereAnimationDepuisLecteur(AnimationPlayer ap)
	{
		if (ap == null) return null;
		foreach (StringName nomLib in ap.GetAnimationLibraryList())
		{
			AnimationLibrary lib = ap.GetAnimationLibrary(nomLib);
			if (lib == null) continue;
			foreach (StringName nomAnim in lib.GetAnimationList())
			{
				Animation source = lib.GetAnimation(nomAnim);
				if (source != null)
					return (Animation)source.Duplicate(true);
			}
		}

		foreach (StringName nom in ap.GetAnimationList())
		{
			Animation source = ap.GetAnimation(nom);
			if (source != null)
				return (Animation)source.Duplicate(true);
		}

		return null;
	}

	private static void RemapperPrefixSquelette(Animation anim, string prefixeExterne, string prefixeCible)
	{
		if (anim == null || string.IsNullOrEmpty(prefixeExterne) || prefixeCible == null) return;
		if (string.Equals(prefixeExterne, prefixeCible, StringComparison.Ordinal)) return;
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string s = anim.TrackGetPath(i).ToString();
			if (s.StartsWith(prefixeExterne, StringComparison.Ordinal))
				anim.TrackSetPath(i, new NodePath(prefixeCible + s.Substring(prefixeExterne.Length)));
		}
	}

	private static void RemapperCheminsParMarqueurSquelette(Animation anim, string cheminNoeudSqueletteCible)
	{
		if (anim == null || string.IsNullOrEmpty(cheminNoeudSqueletteCible)) return;
		const string marqueur = "Skeleton3D";
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string s = anim.TrackGetPath(i).ToString();
			int idx = s.IndexOf(marqueur, StringComparison.Ordinal);
			if (idx < 0) continue;
			string queue = s.Substring(idx + marqueur.Length);
			anim.TrackSetPath(i, new NodePath(cheminNoeudSqueletteCible + queue));
		}
	}

	private static string DeriverNomStandardClipOuNull(string nomComplet)
	{
		string n = nomComplet.ToLowerInvariant();
		if (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort")) return "Mort";
		if (NomClipSembleSautGalop(n) || (n.Contains("jump") && !n.Contains("gallop")))
			return null;
		if (n.Contains("attack") || n.Contains("headbutt") || (n.Contains("kick") && !n.Contains("walk")))
			return null;
		if (n.Contains("idle") || n.Contains("stand") || n.Contains("repos")) return "Idle";
		if (n.Contains("walk") || n.Contains("marche") || n.Contains("locomotion") || n.Contains("cycle")) return "Marche";
		if (n.Contains("run") || n.Contains("gallop") || n.Contains("course") || n.Contains("charge")) return "Course";
		if (n.Contains("eat") || n.Contains("eating") || n.Contains("graze") || n.Contains("manger") || n.Contains("browse")) return "Broutage";
		return null;
	}

	private void FusionnerBibliothequesDepuisGltfExterneMemeRig()
	{
		string chemin = (CheminSceneGltfAnimationsExternesMemeRig ?? "").Trim();
		if (string.IsNullOrEmpty(chemin))
			chemin = ResoudreCheminAnimationsExternesAutomatique();
		FusionnerBibliothequesDepuisCheminExterne(chemin, "externe_unique");
	}

	private void FusionnerBibliothequesDepuisDossierAnimationsCompatibles()
	{
		if (string.IsNullOrWhiteSpace(DossierAnimationsAnimalesCompatibles))
			return;
		string dossierNorm = DossierAnimationsAnimalesCompatibles.Trim();
		List<string> chemins;
		lock (VerrouCacheBibliothequesAnimExternes)
		{
			if (CacheListeCheminsDossierAnimationsCompatibles != null
				&& string.Equals(DossierListeCheminsCache, dossierNorm, StringComparison.OrdinalIgnoreCase))
				chemins = CacheListeCheminsDossierAnimationsCompatibles;
			else
			{
				chemins = ListerFichiersAnimationsRecursifs(dossierNorm);
				CacheListeCheminsDossierAnimationsCompatibles = chemins;
				DossierListeCheminsCache = dossierNorm;
			}
		}
		int ajoutes = 0;
		foreach (string chemin in chemins)
		{
			if (string.IsNullOrWhiteSpace(chemin))
				continue;
			if (string.Equals(chemin, CheminSceneGltfAnimationsExternesMemeRig, StringComparison.OrdinalIgnoreCase))
				continue;
			if (string.Equals(chemin, CheminGlbSqueletteReference, StringComparison.OrdinalIgnoreCase))
				continue;
			string sourceKey = NettoyerCleLibrairie(System.IO.Path.GetFileNameWithoutExtension(chemin));
			if (string.IsNullOrWhiteSpace(sourceKey))
				sourceKey = $"pool_{ajoutes}";
			FusionnerBibliothequesDepuisCheminExterne(chemin, sourceKey);
			ajoutes++;
		}
		if (ajoutes > 0 && !LogScanDossierAnimationsCompatiblesEffectue)
		{
			LogScanDossierAnimationsCompatiblesEffectue = true;
			GD.Print($"ZERO-K Faune : scan dossier animations compatibles -> {ajoutes} fichier(s) (cache partage entre individus).");
		}
	}

	private static List<string> ListerFichiersAnimationsRecursifs(string dossierRes)
	{
		var resultats = new List<string>();
		var visites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(dossierRes))
			return resultats;

		var pile = new Stack<string>();
		pile.Push(dossierRes);
		while (pile.Count > 0)
		{
			string courant = pile.Pop();
			if (!visites.Add(courant))
				continue;
			DirAccess d = DirAccess.Open(courant);
			if (d == null)
				continue;

			d.ListDirBegin();
			while (true)
			{
				string nom = d.GetNext();
				if (nom == "")
					break;
				if (nom == "." || nom == "..")
					continue;
				string chemin = $"{courant.TrimEnd('/')}/{nom}";
				if (d.CurrentIsDir())
				{
					pile.Push(chemin);
					continue;
				}

				if (nom.EndsWith(".import", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!nom.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) && !nom.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!ResourceLoader.Exists(chemin))
					continue;
				resultats.Add(chemin);
			}
			d.ListDirEnd();
		}

		return resultats;
	}

	private static string NettoyerCleLibrairie(string valeur)
	{
		if (string.IsNullOrWhiteSpace(valeur))
			return "";
		string s = valeur.ToLowerInvariant();
		var chars = new char[s.Length];
		int e = 0;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
			chars[e++] = ok ? c : '_';
		}
		return new string(chars, 0, e).Trim('_');
	}

	private static HashSet<string> ExtraireNomsOsNormalises(Skeleton3D sk)
	{
		var os = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (sk == null)
			return os;
		int n = sk.GetBoneCount();
		for (int i = 0; i < n; i++)
		{
			string nom = sk.GetBoneName(i).ToString().Trim().ToLowerInvariant();
			if (!string.IsNullOrWhiteSpace(nom))
				os.Add(nom);
		}
		return os;
	}

	private static bool AnimationCompatibleAvecSkeleton(Animation anim, HashSet<string> osLive)
	{
		if (anim == null || osLive == null || osLive.Count == 0)
			return true;

		int totalPistesOs = 0;
		int correspondances = 0;
		var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			string p = anim.TrackGetPath(i).ToString();
			int idx = p.IndexOf(':');
			if (idx < 0 || idx + 1 >= p.Length)
				continue;
			string os = p.Substring(idx + 1).Trim().ToLowerInvariant();
			if (string.IsNullOrWhiteSpace(os))
				continue;
			if (!vus.Add(os))
				continue;
			totalPistesOs++;
			if (osLive.Contains(os))
				correspondances++;
		}

		if (totalPistesOs == 0)
			return true;
		return correspondances >= 2;
	}

	private static string ResoudreCheminAnimationsExternesAutomatique()
	{
		foreach (string c in CheminsAnimationExterneAuto)
		{
			if (ResourceLoader.Exists(c))
				return c;
		}

		const string dossier = "res://Modeles/Entites/Boeufs/";
		var d = DirAccess.Open(dossier);
		if (d == null)
			return "";

		d.ListDirBegin();
		while (true)
		{
			string f = d.GetNext();
			if (f == "")
				break;
			if (f == "." || f == "..")
				continue;
			if (f.EndsWith(".import", StringComparison.Ordinal))
				continue;
			string fl = f.ToLowerInvariant();
			if (!(fl.EndsWith(".glb", StringComparison.Ordinal) || fl.EndsWith(".gltf", StringComparison.Ordinal)))
				continue;
			if (fl == "boeufsauvage.glb")
				continue;
			if (!fl.Contains("anim", StringComparison.Ordinal))
				continue;
			string chemin = dossier + f;
			if (ResourceLoader.Exists(chemin))
			{
				d.ListDirEnd();
				return chemin;
			}
		}
		d.ListDirEnd();
		return "";
	}

	private void FusionnerBibliothequesDepuisCheminExterne(string chemin, string sourceKey)
	{
		if (string.IsNullOrEmpty(chemin) || !ResourceLoader.Exists(chemin) || _animationPlayer == null)
			return;
		sourceKey = NettoyerCleLibrairie(sourceKey);
		if (string.IsNullOrWhiteSpace(sourceKey))
			sourceKey = "source";

		Skeleton3D skLivePrecalc = TrouverPremierNoeudDeType<Skeleton3D>(_modeleVisuel != null ? _modeleVisuel : this);
		Node racineLivePrecalc = _animationPlayer.GetParent() ?? (_modeleVisuel ?? (Node)this);
		string prefixLivePrecalc = skLivePrecalc != null ? racineLivePrecalc.GetPathTo(skLivePrecalc).ToString() : "";
		string cleCache = $"{chemin}|{sourceKey}|{prefixLivePrecalc}";

		lock (VerrouCacheBibliothequesAnimExternes)
		{
			if (CacheBibliothequesExternesRemappees.TryGetValue(cleCache, out List<(string libDest, AnimationLibrary lib)> enCache)
				&& enCache != null && enCache.Count > 0)
			{
				foreach ((string libDest, AnimationLibrary lib) in enCache)
				{
					if (lib == null) continue;
					var instLib = (AnimationLibrary)lib.Duplicate(true);
					if (_animationPlayer.HasAnimationLibrary(libDest))
						_animationPlayer.RemoveAnimationLibrary(libDest);
					_animationPlayer.AddAnimationLibrary(libDest, instLib);
				}
				return;
			}
		}

		var ps = GD.Load<PackedScene>(chemin);
		Node inst = ps?.Instantiate();
		if (inst == null)
			return;

		var snapshotPourCache = new List<(string libDest, AnimationLibrary lib)>();
		try
		{
			AnimationPlayer apExt = ChoisirMeilleurAnimationPlayer(inst);
			if (apExt == null)
				return;

			Skeleton3D skExt = TrouverPremierNoeudDeType<Skeleton3D>(inst);
			Skeleton3D skLive = skLivePrecalc;
			HashSet<string> osLive = ExtraireNomsOsNormalises(skLive);
			string prefixExt = "";
			string prefixLive = "";
			if (skExt != null && skLive != null)
			{
				Node racineExt = apExt.GetParent() ?? inst;
				Node racineLive = racineLivePrecalc;
				prefixExt = racineExt.GetPathTo(skExt).ToString();
				prefixLive = racineLive.GetPathTo(skLive).ToString();
			}

			int libsAjoutees = 0;
			foreach (StringName libName in apExt.GetAnimationLibraryList())
			{
				AnimationLibrary source = apExt.GetAnimationLibrary(libName);
				if (source == null) continue;
				var copie = new AnimationLibrary();
				foreach (StringName n in source.GetAnimationList())
				{
					Animation a = source.GetAnimation(n);
					if (a == null) continue;
					var c = (Animation)a.Duplicate(true);
					if (!string.IsNullOrEmpty(prefixExt) && !string.IsNullOrEmpty(prefixLive))
					{
						RemapperPrefixSquelette(c, prefixExt, prefixLive);
						RemapperCheminsParMarqueurSquelette(c, prefixLive);
					}
					if (!AnimationCompatibleAvecSkeleton(c, osLive))
						continue;
					copie.AddAnimation(n.ToString(), c);
				}
				if (copie.GetAnimationList().Count == 0) continue;
				string libDest = $"externe_rig_{sourceKey}_{libName}";
				if (_animationPlayer.HasAnimationLibrary(libDest))
					_animationPlayer.RemoveAnimationLibrary(libDest);
				_animationPlayer.AddAnimationLibrary(libDest, copie);
				snapshotPourCache.Add((libDest, (AnimationLibrary)copie.Duplicate(true)));
				libsAjoutees++;
			}

			// Lecteur avec clips a la racine (sans AnimationLibrary), frequent apres certains exports.
			if (libsAjoutees == 0)
			{
				var libLegacy = new AnimationLibrary();
				foreach (StringName n in apExt.GetAnimationList())
				{
					Animation a = apExt.GetAnimation(n);
					if (a != null)
					{
						var c = (Animation)a.Duplicate(true);
						if (!string.IsNullOrEmpty(prefixExt) && !string.IsNullOrEmpty(prefixLive))
						{
							RemapperPrefixSquelette(c, prefixExt, prefixLive);
							RemapperCheminsParMarqueurSquelette(c, prefixLive);
						}
						if (!AnimationCompatibleAvecSkeleton(c, osLive))
							continue;
						libLegacy.AddAnimation(n.ToString(), c);
					}
				}
				if (libLegacy.GetAnimationList().Count > 0)
				{
					string libDestLegacy = $"externe_rig_{sourceKey}_legacy";
					if (_animationPlayer.HasAnimationLibrary(libDestLegacy))
						_animationPlayer.RemoveAnimationLibrary(libDestLegacy);
					_animationPlayer.AddAnimationLibrary(libDestLegacy, libLegacy);
					snapshotPourCache.Add((libDestLegacy, (AnimationLibrary)libLegacy.Duplicate(true)));
					libsAjoutees++;
				}
			}

			if (snapshotPourCache.Count > 0)
			{
				lock (VerrouCacheBibliothequesAnimExternes)
				{
					if (!CacheBibliothequesExternesRemappees.ContainsKey(cleCache))
						CacheBibliothequesExternesRemappees[cleCache] = snapshotPourCache;
				}
			}

			if (libsAjoutees > 0)
				GD.Print($"ZERO-K Faune : animations externes fusionnees depuis {chemin} ({libsAjoutees} bibliotheque(s)) [cache pour prochains individus].");
		}
		finally
		{
			inst.QueueFree();
		}
	}

	private static bool EstClipSystemeOuVide(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return true;
		string n = nomComplet.ToLowerInvariant();
		return n.Contains("reset") || n.Contains("rest_pose") || n.Contains("t-pose") || n.Contains("tpose")
			|| n.EndsWith("/reset") || n == "reset";
	}

	private static bool NomClipSembleMort(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return false;
		string n = nomComplet.ToLowerInvariant();
		return n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("mort")
			|| n.Contains("ragdoll") || n.Contains("corpse");
	}

	private static bool NomClipSembleCombatOuSaut(string nomComplet)
	{
		if (string.IsNullOrWhiteSpace(nomComplet)) return false;
		string n = nomComplet.ToLowerInvariant();
		if (NomClipSembleSautGalop(n))
			return false;
		return n.Contains("attack") || n.Contains("headbutt") || n.Contains("kick") || n.Contains("jump")
			|| n.Contains("bite") || n.Contains("hit");
	}

	private static string PremierClipLocomotionUtileNonMortel(List<string> tous)
	{
		foreach (string c in tous)
		{
			if (!EstClipSystemeOuVide(c) && !NomClipSembleMort(c))
				return c;
		}
		foreach (string c in tous)
		{
			if (!EstClipSystemeOuVide(c))
				return c;
		}
		return "";
	}

	private void MettreAJourFlashDegatsVisuel(float dt)
	{
		if (_flashRougeDegatsRestant <= 0f) return;
		_flashRougeDegatsRestant = Mathf.Max(0f, _flashRougeDegatsRestant - dt);
		float ratio = Mathf.Clamp(_flashRougeDegatsRestant / Mathf.Max(0.05f, DureeFlashRougeDegats), 0f, 1f);
		AppliquerFlashRougeSurMateriaux(ratio);
	}

	private void AppliquerFlashRougeSurMateriaux(float intensite)
	{
		for (int i = _materiauxPelageInstances.Count - 1; i >= 0; i--)
		{
			ShaderMaterial mat = _materiauxPelageInstances[i];
			if (mat == null || !GodotObject.IsInstanceValid(mat))
			{
				_materiauxPelageInstances.RemoveAt(i);
				continue;
			}
			mat.SetShaderParameter("flash_rouge_degats", Mathf.Clamp(intensite, 0f, 1f));
		}
	}

	private void AppliquerBouclesSurClipsLocomotion()
	{
		if (_animationPlayer == null) return;
		var vus = new HashSet<string>();
		foreach (string chemin in new[] { _clipIdle, _clipMarche, _clipCourse, _clipManger })
		{
			if (string.IsNullOrEmpty(chemin) || !vus.Add(chemin)) continue;
			Animation anim = _animationPlayer.GetAnimation(chemin);
			if (anim == null) continue;
			anim.LoopMode = Animation.LoopModeEnum.Linear;
		}
	}

	private static List<string> CollecterCheminsAnimation(AnimationPlayer ap)
	{
		var liste = new List<string>();
		var vus = new HashSet<string>(StringComparer.Ordinal);
		if (ap == null) return liste;

		foreach (StringName nom in ap.GetAnimationList())
		{
			string s = nom.ToString();
			if (vus.Add(s))
				liste.Add(s);
		}

		foreach (StringName lib in ap.GetAnimationLibraryList())
		{
			AnimationLibrary libObj = ap.GetAnimationLibrary(lib);
			if (libObj == null) continue;
			foreach (StringName anim in libObj.GetAnimationList())
			{
				string s = $"{lib}/{anim}";
				if (vus.Add(s))
					liste.Add(s);
			}
		}

		return liste;
	}

	private static int CompterClipsAnimation(AnimationPlayer ap) => CollecterCheminsAnimation(ap).Count;

	private static AnimationPlayer ChoisirMeilleurAnimationPlayer(Node racine)
	{
		if (racine == null) return null;
		AnimationPlayer meilleur = null;
		int maxScore = -1;

		void Parcourir(Node n)
		{
			if (n is AnimationPlayer ap)
			{
				int score = CompterClipsAnimation(ap);
				// Ignorer les lecteurs vides (ex. nœud ajouté a la main dans l'éditeur sans bibliothèque).
				if (score > 0 && score > maxScore)
				{
					maxScore = score;
					meilleur = ap;
				}
			}

			foreach (Node enfant in n.GetChildren())
				Parcourir(enfant);
		}

		Parcourir(racine);
		return meilleur;
	}

	private void StabiliserMateriauxBoeuf()
	{
		_shaderPelageBoeuf ??= GD.Load<Shader>("res://shaders/BoeufSauvage.gdshader");
		_materiauxPelageInstances.Clear();
		if (_textureDiffuseModele == null && !string.IsNullOrWhiteSpace(CheminTextureDiffuseModele))
		{
			if (_cacheTextureDiffuseBoeuf.TryGetValue(CheminTextureDiffuseModele, out Texture2D texCache))
			{
				_textureDiffuseModele = texCache;
			}
			else if (ResourceLoader.Exists(CheminTextureDiffuseModele))
			{
				_textureDiffuseModele = GD.Load<Texture2D>(CheminTextureDiffuseModele);
				_cacheTextureDiffuseBoeuf[CheminTextureDiffuseModele] = _textureDiffuseModele;
			}
			else
			{
				_cacheTextureDiffuseBoeuf[CheminTextureDiffuseModele] = null;
				if (_cheminsTextureIntrouvablesLoggues.Add(CheminTextureDiffuseModele))
					GD.Print($"ZERO-K Faune : texture diffuse absente ({CheminTextureDiffuseModele}), utilisation des matériaux GLTF natifs.");
			}
		}
		StabiliserMateriauxRecursif(this, _shaderPelageBoeuf, _textureDiffuseModele);
	}

	private void StabiliserMateriauxRecursif(Node node, Shader shaderPelage, Texture2D textureDiffuse)
	{
		if (node is MeshInstance3D mesh)
		{
			if (UtiliserShaderPelageProcedural && shaderPelage != null)
			{
				var mat = new ShaderMaterial
				{
					Shader = shaderPelage
				};
				mat.SetShaderParameter("flash_rouge_degats", Mathf.Clamp(_flashRougeDegatsRestant / Mathf.Max(0.05f, DureeFlashRougeDegats), 0f, 1f));
				mesh.MaterialOverride = mat;
				_materiauxPelageInstances.Add(mat);
			}
			else
			{
				mesh.MaterialOverride = null;
				int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
				bool auMoinsUneSurface = false;
				for (int i = 0; i < surfaces; i++)
				{
					Material source = mesh.GetActiveMaterial(i);
					if (source == null) continue;

					Material dup = (Material)source.Duplicate(true);
					mesh.SetSurfaceOverrideMaterial(i, dup);
					auMoinsUneSurface = true;

					switch (dup)
					{
						case StandardMaterial3D sm:
							RenforcerStandardMateriauBoeuf(sm, textureDiffuse);
							break;
						case BaseMaterial3D bm:
							RenforcerBaseMateriau3DBoeuf(bm, textureDiffuse);
							break;
					}
				}

				if (!auMoinsUneSurface && surfaces > 0)
				{
					mesh.MaterialOverride = new StandardMaterial3D
					{
						AlbedoColor = textureDiffuse != null ? Colors.White : new Color(0.40f, 0.30f, 0.19f, 1f),
						AlbedoTexture = textureDiffuse,
						Roughness = 0.9f,
						Metallic = 0f
					};
				}
			}
		}

		foreach (Node enfant in node.GetChildren())
			StabiliserMateriauxRecursif(enfant, shaderPelage, textureDiffuse);
	}

	private static void RenforcerStandardMateriauBoeuf(StandardMaterial3D sm, Texture2D textureDiffuse)
	{
		sm.Metallic = Mathf.Min(sm.Metallic, 0.35f);
		sm.Roughness = Mathf.Clamp(sm.Roughness, 0.35f, 1f);
		float lum = sm.AlbedoColor.R + sm.AlbedoColor.G + sm.AlbedoColor.B;
		if (lum < 0.06f)
			sm.AlbedoColor = Colors.White;
		if (sm.AlbedoTexture == null && textureDiffuse != null)
			sm.AlbedoTexture = textureDiffuse;
		sm.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
	}

	private static void RenforcerBaseMateriau3DBoeuf(BaseMaterial3D bm, Texture2D textureDiffuse)
	{
		bm.Metallic = Mathf.Min(bm.Metallic, 0.35f);
		bm.Roughness = Mathf.Clamp(bm.Roughness, 0.35f, 1f);
		float lum = bm.AlbedoColor.R + bm.AlbedoColor.G + bm.AlbedoColor.B;
		if (lum < 0.06f)
			bm.AlbedoColor = Colors.White;
		if (bm.AlbedoTexture == null && textureDiffuse != null)
			bm.AlbedoTexture = textureDiffuse;
		bm.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
	}

	private void SecuriserPositionSol()
	{
		if (EssayerTrouverSolParRaycast(GlobalPosition + Vector3.Up * 3f, out Vector3 sol))
		{
			if (sol.DistanceTo(GlobalPosition) > 7f)
				_cibleCourante = _ancreTroupeau;
			return;
		}

		float seuilVide = _joueur != null ? _joueur.GlobalPosition.Y - 80f : 90f;
		if (GlobalPosition.Y < seuilVide)
		{
			int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(
				Mathf.FloorToInt(_ancreTroupeau.X),
				Mathf.FloorToInt(_ancreTroupeau.Z),
				_seedTerrain);
			GlobalPosition = new Vector3(_ancreTroupeau.X, h + 1.2f, _ancreTroupeau.Z);
			Velocity = Vector3.Zero;
			_cibleCourante = _ancreTroupeau;
		}
	}

	private bool EssayerTrouverSolParRaycast(Vector3 debut, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;

		Vector3 fin = debut + Vector3.Down * 40f;
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.HitFromInside = false;
		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || !hit.ContainsKey("position")) return false;
		pointSol = (Vector3)hit["position"];
		return true;
	}
}
