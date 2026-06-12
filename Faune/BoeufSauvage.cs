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
	/// <summary>Durée réelle (secondes) avant disparition d'un cadavre non dépecé — 24 h par défaut, compte hors ligne.</summary>
	[Export] public float DureeCadavreAvantSuppression = 86400f;
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
	[Export(PropertyHint.Range, "0,100,0.5")] public float ChanceFractureOsChargeJoueurPct = 5f;
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
	/// mort→<see cref="_clipMort"/>, ruade arrière→<see cref="_clipAttaqueKick"/> (<see cref="ClipAttaqueKickCanonique"/>), coup de tête avant→<see cref="_clipAttaqueTete"/> (<see cref="ClipAttaqueTeteCanonique"/>).
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
	[ExportSubgroup("Ancrage au sol (visuel)")]
	/// <summary>Relève le mesh uniquement en course (delta par rapport à la marche), pas au repos.</summary>
	[Export] public bool AutoCompenserEnfoncementClipsLocomotion = true;
	/// <summary>Modifie les pistes Y des GLB à l'import — peut cumuler avec la compensation runtime ; laisser désactivé sauf besoin.</summary>
	[Export] public bool ReequilibrerClipsYAlImport = false;
	/// <summary>Ajustement fin en sprint uniquement (0 = auto depuis les clips).</summary>
	[Export(PropertyHint.Range, "0,0.08,0.005")] public float CompensationSolCourseManuelle = 0f;
	[Export(PropertyHint.Range, "0,0.06,0.005")] public float CompensationSolMarcheManuelle = 0f;
	[Export(PropertyHint.Range, "0.02,0.08,0.005")] public float CompensationSolMaxCourse = 0.045f;
	[Export(PropertyHint.Range, "1,24,0.5")] public float VitesseLissageCompensationSol = 10f;

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
	/// <summary>Horodatage Unix (s) au moment de la mort — persistance du délai 24 h hors ligne.</summary>
	private double _horodatageMortUnixSec;
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
	/// <summary>Noms exacts des clips d'attaque (pack Quaternius / bull).</summary>
	private const string ClipAttaqueKickCanonique = "Attack_Kick";
	private const string ClipAttaqueTeteCanonique = "Attack_Headbutt";
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
	private bool _animationMortDoitEtreFigee;
	private bool _animationMortFigee;
	private const float EpsilonFinAnimationMortSec = 0.02f;
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
	private float _cooldownReengagementChargeJoueur;
	private bool _impactChargeJoueurPlanifie;
	private bool _impactChargeCoupDeTetePlanifie;
	private float _delaiImpactChargePlanifie;
	private Vector3 _pointImpactChargePlanifie;
	private Vector3 _dirImpactChargePlanifie;
	private int _indiceFormeImpactChargePlanifie = -1;
	private const float DelaiDegatsApresDebutAnimationCharge = 0.24f;
	private const float DistanceMaxDeclenchementAttaqueCharge = 2.05f;
	private const float DistanceMaxImpactChargeApresDelai = 2.35f;
	/// <summary>À cette distance, le taureau engage la charge (plus de simple regard).</summary>
	private const float DistanceDeclenchementEngagementCharge = 2.35f;
	/// <summary>Seuil « face à face » : animation d’attaque puis dégâts (pas de RNG).</summary>
	private const float DistanceAttaqueChargeFaceAFace = 2.05f;
	/// <summary>Pas de dégâts de charge si le bovin est trop au-dessus du joueur (saut / écrasement tête).</summary>
	private const float DeltaYMaxDegatsChargeSurJoueur = 0.72f;
	/// <summary>Pause après une charge (réussie ou ratée) pour éviter charge → fuite → charge en boucle.</summary>
	private const float CooldownReengagementChargeJoueurSec = 14f;
	private float _flashRougeDegatsRestant;
	private float _cooldownVariationAnimation;
	private int _signatureContexteAnimation = int.MinValue;
	private float _tempsStableCalmePourClip;
	private EtatBoeuf _etatPourClipsLocomotion = (EtatBoeuf)(-1);
	/// <summary>Stabilité minimale avant de changer de clip (évite les swaps « au hasard »).</summary>
	private const float TempsStabiliteAvantChangementClipSec = 4f;
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
	private float _compensationYIdle;
	private float _compensationYMarche;
	private float _compensationYCourse;
	private float _compensationYTrot;
	private float _compensationYBroutage;
	private float _offsetVisuelSolActuel;
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
		_impactChargeJoueurPlanifie = false;
		_indiceFormeImpactChargePlanifie = -1;
		_delaiImpactChargePlanifie = 0f;
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
		if (GetTree().Paused)
			return;
		ulong debutFrameUs = ActiverProfilagePerfBovin ? PerfBudgetMonitor.Begin() : 0UL;
		_cooldownDrainProfilage += (float)delta;
		float dt = (float)delta;

		if (_etat == EtatBoeuf.Mort)
		{
			MettreAJourAnimationMortEtFigerSiTerminee();
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
		_cooldownReengagementChargeJoueur = Mathf.Max(0f, _cooldownReengagementChargeJoueur - dt);
		MettreAJourImpactChargeJoueurPlanifie(dt);
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
		MettreAJourVariationClipsContextuelle(dt, vitesseHorizActuelle);

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
			bool transfertDimension = _gestionnaire != null && GodotObject.IsInstanceValid(_gestionnaire)
				&& _gestionnaire.EstVerrouSecuriteAbysseActif();
			if (!transfertDimension)
			{
				GererPresenceJoueur();
				GererReproductionEtGestation();
				GererEtatEtCible(dtCerveau);
			}
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
		bool combatChargeActif = _etat == EtatBoeuf.Charge || _impactChargeJoueurPlanifie || _tempsVerrouAnimationCombat > 0.01f;
		if (!_dansEau && !combatChargeActif)
		{
			direction = AjusterDirectionAntiObstacle(direction);
			direction = AdapterStrategieTerrain(direction, dt, ref demandeSautStrategique);
			EvaluerCoincageEtDeblocage(dt, direction, ref demandeSautStrategique);
		}
		if (!combatChargeActif && !demandeSautStrategique && DoitTenterSautEscalade(direction))
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
		if (_etat == EtatBoeuf.Charge && _joueur != null && GodotObject.IsInstanceValid(_joueur))
		{
			Vector3 versJoueurCharge = _joueur.GlobalPosition - GlobalPosition;
			versJoueurCharge.Y = 0f;
			float distCharge = versJoueurCharge.Length();
			if (distCharge <= DistanceDeclenchementEngagementCharge)
			{
				OrienteCorpsVersJoueur(dt);
				if (!_impactChargeJoueurPlanifie && _tempsVerrouAnimationCombat <= 0.01f)
				{
					if (distCharge <= 1.2f)
						vitesseCible = 0f;
					else if (distCharge <= DistanceAttaqueChargeFaceAFace)
						vitesseCible = Mathf.Min(vitesseCible, 0.55f);
				}
			}
		}
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
		MettreAJourCompensationEnfoncementSol(dt);
		if (EstFallbackLocomotionBobSeulement())
			AppliquerLocomotionSquelettiqueProcedural(dt, vitesseHoriz);
		OrienteCorpsVersDirectionDeplacement(dt, vitesseHoriz);
		if (_etat != EtatBoeuf.Mort
			&& _reconfigurationArbreAnimationEnAttente
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

		if (_gestionnaire != null && GodotObject.IsInstanceValid(_gestionnaire) && _gestionnaire.EstVerrouSecuriteAbysseActif())
			return 0.18f;
		if (!EssayerObtenirPositionJoueur(out Vector3 posJoueur))
			return 0.18f;

		Vector3 d = posJoueur - GlobalPosition;
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
		AppliquerYawVersDirectionHorizontale(dir, dt);
	}

	private void OrienteCorpsVersJoueur(float dt)
	{
		if (_joueur == null || !GodotObject.IsInstanceValid(_joueur))
			return;
		Vector3 vers = _joueur.GlobalPosition - GlobalPosition;
		vers.Y = 0f;
		if (vers.LengthSquared() < 0.0001f)
			return;
		AppliquerYawVersDirectionHorizontale(vers.Normalized(), dt);
	}

	private void AppliquerYawVersDirectionHorizontale(Vector3 dir, float dt)
	{
		if (dir.LengthSquared() < 0.0001f)
			return;
		dir = dir.Normalized();
		float yawCible = Mathf.Atan2(-dir.X, -dir.Z) + Mathf.DegToRad(CorrectionYawRegardDegres);
		float k = 1f - Mathf.Exp(-Mathf.Max(0.5f, VitesseOrientationCorps) * dt);
		float yaw = Mathf.LerpAngle(Rotation.Y, yawCible, Mathf.Clamp(k, 0f, 1f));
		Rotation = new Vector3(Rotation.X, yaw, Rotation.Z);
	}










}
