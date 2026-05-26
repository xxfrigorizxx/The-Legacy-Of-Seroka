using Godot;
using System;
using System.Collections.Generic;

/// <summary>Composition chimique d'une roche. Dicte couleur, rugosité, future résistance et point de fusion.</summary>
public struct ProfilMineral
{
	public string Nom;
	public Color CouleurBase;
	public Color CouleurVeine;
	public Color CouleurTache;
	public float Rugosite;
	public int ResistanceFuture;
}

/// <summary>ADN de l'objet libre : identifie ce que le Raycast du joueur ramasse.
/// Matières rocheuses : IDs <see cref="IdRocheMatiereMin"/>–<see cref="IdRocheMatiereMax"/> (granit=40, …, silex=<c>40+IndexChimiqueSilex</c>). Taille = <see cref="IndexTailleRoche"/>, forme = <see cref="IndexCacheMemoire"/> (0–3).
/// Hérite de RigidBody3D pour ContactMonitor / BodyEntered (physique de rupture).</summary>
public partial class ItemPhysique : RigidBody3D
{
	public const int IdRocheMatiereMin = 40;
	public const int IdRocheMatiereMax = 51;
	/// <summary>Atelier posé (200) : grille 3×3 du plan de travail, indépendante du craft 2×2 de l’inventaire (Q).</summary>
	public SlotInventaire[] GrillePlanTravailAtelier = new SlotInventaire[9];
	/// <summary>Coffre en bois posé (113) : 10 slots persistés avec l’objet (monde + sauvegarde).</summary>
	public SlotInventaire[] GrilleStockageCoffre = new SlotInventaire[10];
	/// <summary>Indice dans <see cref="TableGeologique"/> pour le silex (ID objet = <c>40 + IndexChimiqueSilex</c>).</summary>
	public const int IndexChimiqueSilex = 5;
	/// <summary>Roche créée par le joueur (pose/lancer) : mesh et collision déjà figés, ne pas les remplacer dans _Ready.</summary>
	public const string MetaRocheForgeeParJoueur = "RocheForgeeJoueur";

	public static bool EstIdRocheMatiere(int id) => id >= IdRocheMatiereMin && id <= IdRocheMatiereMax;

	public static int IndexChimiqueDepuisIdRoche(int id) => Mathf.Clamp(id - IdRocheMatiereMin, 0, TableGeologique.Length - 1);

	public static bool EstMatiereSilexParIdObjet(int id) => EstIdRocheMatiere(id) && IndexChimiqueDepuisIdRoche(id) == IndexChimiqueSilex;

	/// <summary>Atelier (200), racks, coffre : corps figé en mode statique au sol — ne pas les traiter comme des objets à « dégeler » après streaming du terrain.</summary>
	public static bool EstMeublePoseStatique(int idObjet) =>
		idObjet == 200
		|| idObjet == Joueur.IdObjetTableBoisDecorative
		|| idObjet == Joueur.IdObjetTableArtisanaTier1
		|| idObjet == Joueur.IdObjetTableAnalyseTier1
		|| idObjet == Joueur.IdObjetRackBatons
		|| idObjet == Joueur.IdObjetRackBuches
		|| idObjet == Joueur.IdObjetCoffreBoisTier0
		|| idObjet == Joueur.IdObjetPitFeu
		|| idObjet == Joueur.IdObjetPitFeuRoche
		|| idObjet == Joueur.IdObjetFondationBois
		|| idObjet == Joueur.IdObjetFondationRoche
		|| idObjet == Joueur.IdObjetFondationBoisSoleRoche
		|| idObjet == Joueur.IdObjetFondationRocheSoleBois
		|| idObjet == Joueur.IdObjetSolBois
		|| idObjet == Joueur.IdObjetSolRoche
		|| idObjet == Joueur.IdObjetMuretBois
		|| idObjet == Joueur.IdObjetMuretPierre
		|| idObjet == Joueur.IdObjetMurBois
		|| idObjet == Joueur.IdObjetMurBoisFenetre
		|| idObjet == Joueur.IdObjetMurBoisCadrePorte
		|| idObjet == Joueur.IdObjetPorteBois
		|| idObjet == Joueur.IdObjetToitChaume
		|| idObjet == Joueur.IdObjetTorche;

	private const float SeuilMasseObjetLegerKg = 35f;
	private const float SeuilHauteurObjetPetitMetres = 0.6f;

	public static bool EstRigidBodyLegerEtPetitReactif(RigidBody3D rb)
	{
		if (rb is ItemPhysique ip)
			return ip.EstObjetLegerEtPetitReactif();
		return false;
	}

	public bool EstObjetLegerEtPetitReactif()
	{
		if (EstMeublePoseStatique(ID_Objet))
			return false;
		return Mass <= SeuilMasseObjetLegerKg && ObtenirHauteurApproxObjetMetres() <= SeuilHauteurObjetPetitMetres;
	}

	private float ObtenirHauteurApproxObjetMetres()
	{
		foreach (Node c in GetChildren())
		{
			if (c is not CollisionShape3D cs || cs.Shape == null)
				continue;
			switch (cs.Shape)
			{
				case BoxShape3D box:
					return Mathf.Max(0.01f, box.Size.Y);
				case SphereShape3D sphere:
					return Mathf.Max(0.01f, sphere.Radius * 2f);
				case CapsuleShape3D capsule:
					return Mathf.Max(0.01f, capsule.Height + capsule.Radius * 2f);
				case CylinderShape3D cylinder:
					return Mathf.Max(0.01f, cylinder.Height);
			}
		}

		foreach (Node c in GetChildren())
		{
			if (c is MeshInstance3D mi && mi.Mesh != null)
			{
				Aabb aabb = mi.Mesh.GetAabb();
				return Mathf.Max(0.01f, aabb.Size.Y * Mathf.Abs(mi.Scale.Y));
			}
		}
		return 1f;
	}

	public static float RayonBaseRochesJoueur(int indexTaille) => indexTaille switch
	{
		0 => 0.08f,
		1 => 0.15f,
		2 => 0.25f,
		3 => 0.40f,
		4 => 0.65f,
		_ => 0.2f
	};

	public static Vector3 EchelleMorphologieRoche(int morph) => morph switch
	{
		1 => new Vector3(1f, 0.4f, 1f),
		2 => new Vector3(1f, 0.7f, 1.4f),
		3 => new Vector3(0.6f, 1.3f, 0.6f),
		_ => Vector3.One
	};

	/// <summary>Boîte alignée sur la sphère déformée du mesh : pas d’échelle non uniforme sur le <see cref="RigidBody3D"/> (Jolt).</summary>
	public static BoxShape3D CreerBoxCollisionRocheMatiere(float rayonSphereBase, Vector3 echelleMorph)
	{
		return new BoxShape3D
		{
			Size = new Vector3(
				rayonSphereBase * 2f * echelleMorph.X,
				rayonSphereBase * 2f * echelleMorph.Y,
				rayonSphereBase * 2f * echelleMorph.Z)
		};
	}

	/// <summary>Morph 0 = sphère (roule) ; 1–3 = boîte épousant le mesh déformé (plate / ovale / pointe).</summary>
	public static Shape3D CreerShapeCollisionRocheMatiere(float rayonSphereBase, int morphologie)
	{
		morphologie = Mathf.Clamp(morphologie, 0, 3);
		if (morphologie == 1 || morphologie == 2 || morphologie == 3)
			return CreerBoxCollisionRocheMatiere(rayonSphereBase, EchelleMorphologieRoche(morphologie));
		return new SphereShape3D { Radius = rayonSphereBase };
	}

	/// <summary>Plus la roche est grosse (index 0–4), plus elle encaisse avant fracture (résistance de base × facteur).</summary>
	public static float FacteurSoliditeRochesParTaille(int indexTailleRoche)
	{
		int t = Mathf.Clamp(indexTailleRoche, 0, 4);
		return 0.68f + t * 0.13f;
	}

	/// <summary>Roche posée : CCD. Ronde (morph 0) = sphère + faible amortissement pour rouler ; déformée = boîte + amortissement plus fort (stabilité).</summary>
	public static void AppliquerPhysiqueRochePortee(ItemPhysique rb)
	{
		if (rb == null || !EstIdRocheMatiere(rb.ID_Objet)) return;
		int m = Mathf.Clamp(rb.IndexCacheMemoire, 0, 3);
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.ContinuousCd = true;
		if (m == 0) // ronde
		{
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.82f, Bounce = 0.06f };
			rb.LinearDamp = 0.2f;
			rb.AngularDamp = 0.35f;
		}
		else if (m == 1) // plate
		{
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.96f, Bounce = 0.04f };
			rb.LinearDamp = 0.4f;
			rb.AngularDamp = 1.05f;
		}
		else if (m == 2) // ovale
		{
			// Ovale : conserve de l'inertie et roule plus naturellement.
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.76f, Bounce = 0.04f };
			rb.LinearDamp = 0.16f;
			rb.AngularDamp = 0.28f;
		}
		else // m == 3, pointe
		{
			// Pointe : peut rouler/tanguer puis se stabiliser, sans arrêt "net" immédiat.
			rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.88f, Bounce = 0.03f };
			rb.LinearDamp = 0.24f;
			rb.AngularDamp = 0.72f;
		}
	}

	/// <summary>Dague posée/lancée : CCD + amortissement pour limiter traverse-sol et vrilles infinies.</summary>
	public static void AppliquerPhysiqueDague105(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != 105) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.22f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.9f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.65f, Bounce = 0.04f };
	}

	/// <summary>Hachette primitive (106) : même esprit que la dague, masse plus élevée, CCD.</summary>
	public static void AppliquerPhysiqueHachette106(ItemPhysique rb)
	{
		if (rb == null || (rb.ID_Objet != 106 && rb.ID_Objet != Joueur.IdObjetHachePierreTier1)) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.2f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.82f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.62f, Bounce = 0.05f };
	}

	/// <summary>Pelle pierre tier0 (107) : physique proche hachette, un peu plus stable au sol.</summary>
	public static void AppliquerPhysiquePelle107(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetPellePierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.24f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.92f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.68f, Bounce = 0.04f };
	}

	/// <summary>Pioche pierre tier0 (108) : outil plus lourd, stabilité proche hachette.</summary>
	public static void AppliquerPhysiquePioche108(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetPiochePierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.22f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.88f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.66f, Bounce = 0.04f };
	}

	/// <summary>Lance pierre tier0 (111) : plus allongée, orientée attaque/lancer.</summary>
	public static void AppliquerPhysiqueLance111(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetLancePierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.18f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.62f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.58f, Bounce = 0.05f };
	}

	/// <summary>Faux primitive (112) : même esprit que la dague, légèrement plus amortie (mesh épée).</summary>
	public static void AppliquerPhysiqueFaux112(ItemPhysique rb)
	{
		if (rb == null || rb.ID_Objet != Joueur.IdObjetFauxPierreTier0) return;
		rb.ContinuousCd = true;
		rb.LinearDampMode = RigidBody3D.DampMode.Replace;
		rb.LinearDamp = 0.23f;
		rb.AngularDampMode = RigidBody3D.DampMode.Replace;
		rb.AngularDamp = 0.88f;
		rb.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.64f, Bounce = 0.045f };
	}

	/// <summary>Table géologique : compositions minérales réelles (couleur, rugosité, future résistance).</summary>
	public static readonly ProfilMineral[] TableGeologique = new ProfilMineral[]
	{
		new ProfilMineral { Nom = "Granit", CouleurBase = new Color(0.4f, 0.4f, 0.4f), CouleurVeine = new Color(0.8f, 0.8f, 0.8f), CouleurTache = new Color(0.1f, 0.1f, 0.1f), Rugosite = 0.9f, ResistanceFuture = 80 },
		new ProfilMineral { Nom = "Basalte", CouleurBase = new Color(0.15f, 0.15f, 0.15f), CouleurVeine = new Color(0.1f, 0.1f, 0.1f), CouleurTache = new Color(0.05f, 0.05f, 0.05f), Rugosite = 0.95f, ResistanceFuture = 90 },
		new ProfilMineral { Nom = "Calcaire", CouleurBase = new Color(0.85f, 0.85f, 0.80f), CouleurVeine = new Color(0.9f, 0.9f, 0.85f), CouleurTache = new Color(0.7f, 0.7f, 0.6f), Rugosite = 1.0f, ResistanceFuture = 20 },
		new ProfilMineral { Nom = "Grès", CouleurBase = new Color(0.6f, 0.4f, 0.2f), CouleurVeine = new Color(0.7f, 0.5f, 0.3f), CouleurTache = new Color(0.4f, 0.2f, 0.1f), Rugosite = 0.98f, ResistanceFuture = 40 },
		new ProfilMineral { Nom = "Schiste", CouleurBase = new Color(0.3f, 0.35f, 0.35f), CouleurVeine = new Color(0.2f, 0.25f, 0.25f), CouleurTache = new Color(0.4f, 0.45f, 0.45f), Rugosite = 0.8f, ResistanceFuture = 30 },
		new ProfilMineral { Nom = "Silex", CouleurBase = new Color(0.12f, 0.12f, 0.14f), CouleurVeine = new Color(0.18f, 0.18f, 0.20f), CouleurTache = new Color(0.02f, 0.02f, 0.03f), Rugosite = 0.5f, ResistanceFuture = 85 },
		new ProfilMineral { Nom = "Quartz", CouleurBase = new Color(0.9f, 0.88f, 0.85f), CouleurVeine = new Color(0.95f, 0.95f, 0.95f), CouleurTache = new Color(0.6f, 0.55f, 0.5f), Rugosite = 0.3f, ResistanceFuture = 70 },
		new ProfilMineral { Nom = "Marbre", CouleurBase = new Color(0.85f, 0.85f, 0.9f), CouleurVeine = new Color(0.7f, 0.7f, 0.75f), CouleurTache = new Color(0.95f, 0.95f, 0.98f), Rugosite = 0.2f, ResistanceFuture = 50 },
		new ProfilMineral { Nom = "Obsidienne", CouleurBase = new Color(0.08f, 0.08f, 0.1f), CouleurVeine = new Color(0.05f, 0.05f, 0.06f), CouleurTache = new Color(0.15f, 0.15f, 0.18f), Rugosite = 0.15f, ResistanceFuture = 75 },
		new ProfilMineral { Nom = "Gneiss", CouleurBase = new Color(0.45f, 0.42f, 0.4f), CouleurVeine = new Color(0.55f, 0.5f, 0.48f), CouleurTache = new Color(0.25f, 0.22f, 0.2f), Rugosite = 0.85f, ResistanceFuture = 65 },
		new ProfilMineral { Nom = "Marcassite", CouleurBase = new Color(0.62f, 0.58f, 0.44f), CouleurVeine = new Color(0.72f, 0.68f, 0.5f), CouleurTache = new Color(0.33f, 0.3f, 0.24f), Rugosite = 0.56f, ResistanceFuture = 58 },
		new ProfilMineral { Nom = "Pyrite", CouleurBase = new Color(0.76f, 0.68f, 0.3f), CouleurVeine = new Color(0.86f, 0.78f, 0.4f), CouleurTache = new Color(0.4f, 0.34f, 0.14f), Rugosite = 0.48f, ResistanceFuture = 62 }
	};

	[Export] public int ID_Objet = 0;
	/// <summary>Sauvegarde de la forme exacte (index dans la banque d'ADN). -1 = tirage aléatoire au spawn.</summary>
	public int IndexCacheMemoire = -1;
	/// <summary>Index dans TableGeologique. -1 = non défini (tirage au spawn).</summary>
	public int IndexChimique = -1;
	/// <summary>Résistance actuelle (dégâts physiques). Initialisée depuis TableGeologique[IndexChimique].ResistanceFuture. À 0 → fracture.</summary>
	public float ResistanceActuelle { get; set; }
	/// <summary>True si cette roche est un éclat créé par fracture (créé à l'instant, jamais remis au pool ni sauvegardé).</summary>
	public bool EstEclatFracture { get; set; }
	/// <summary>Bouclier d'amnésie : si true, _Ready() n'écrase pas le maillage tranché (pas de chargement depuis le cache).</summary>
	public bool EstUnEclat = false;
	/// <summary>Nombre de fractures subies (0 = roche intacte). Au-delà de 5, le fragment devient poudre et disparaît.</summary>
	public int NiveauFracture = 0;
	/// <summary>Essence de bois (0 = chêne). Pour bûche (30) et bâton (32) : propriétés depuis le profil chêne ; en prévision des futurs arbres.</summary>
	public byte IndexBotanique = 0;
	/// <summary>Assemblage CAO identique à <see cref="SlotInventaire.GenomeAssemblage"/> (outil forgé ID 100).</summary>
	public string GenomeAssemblage = "";
	/// <summary>Grosseur pour roches matière (ID <see cref="IdRocheMatiereMin"/>–<see cref="IdRocheMatiereMax"/>) : 0=Mini … 4=Énorme. Aligné sur <see cref="SlotInventaire.IndexTaille"/>.</summary>
	public int IndexTailleRoche = 2;

	/// <summary>Banque d'ADN : accès public pour rendu en main et UI inventaire.</summary>
	public static IReadOnlyList<Mesh> CacheMeshCaillou => _cacheMeshCaillou;
	public static IReadOnlyList<Shape3D> CacheCollisionCaillou => _cacheCollisionCaillou;
	public static IReadOnlyList<Mesh> CacheMeshSilex => _cacheMeshSilex;
	public static IReadOnlyList<Shape3D> CacheCollisionSilex => _cacheCollisionSilex;

	private static readonly List<Mesh> _cacheMeshCaillou = new List<Mesh>();
	private static readonly List<Shape3D> _cacheCollisionCaillou = new List<Shape3D>();
	private static readonly List<Mesh> _cacheMeshSilex = new List<Mesh>();
	private static readonly List<Shape3D> _cacheCollisionSilex = new List<Shape3D>();
	private const int NbVariationsCache = 50;

	/// <summary>Cache des matériaux procéduraux (évite le freeze à la cassure : pas de génération 256×256 à chaque éclat).</summary>
	private static readonly Dictionary<(bool silex, int idx, bool eclat), StandardMaterial3D> _cacheMateriaux = new Dictionary<(bool, int, bool), StandardMaterial3D>();
	private const int MaxPointsContourFragment = 48;

	/// <summary>True si BodyEntered a été connecté par nous (évite "disconnect nonexistent" à la fracture).</summary>
	private bool _surImpactConnecte = false;
	/// <summary>Pendant quelques frames après un lancer : ignore les micro-chocs (joueur / overlap) qui fracturaient dans le vide.</summary>
	private ulong _frameFinGraceImpactLancer = 0;
	private BoeufSauvage _bovinPlante;
	private Vector3 _offsetLocalDansBovinPlante = Vector3.Zero;

	/// <summary>À appeler juste après le spawn au lancer (roche) pour ne pas perdre la pierre au premier contact.</summary>
	public void ActiverGraceImpactAuLancer(int nbFramesPhysiques = 22)
	{
		_frameFinGraceImpactLancer = Engine.GetPhysicsFrames() + (ulong)Mathf.Max(1, nbFramesPhysiques);
	}
	/// <summary>Cache pour flottaison : eau voxel via le gestionnaire (évite la bande Y qui cassait le sol sec).</summary>
	private Gestionnaire_Monde _gestionnaireMondeCache;
	private readonly Vector3[] _echantillonsImmersionObjet = new Vector3[7];
	private double _tempsImmersionIntestin;
	private const double DureeCombustionPitFeuSec = 300.0;
	private const double DureeCuissonPitFeuRocheSteakSec = 60.0;
	private const int PitFeuRocheSlotCombustible = 0;
	private const int PitFeuRocheSlotCuisson = 1;
	private const int PitFeuRocheSlotResultat = 2;
	private const string MetaPitFeuFinCombustionUnixMs = "PitFeuFinCombustionUnixMs";
	private const string MetaPitFeuRocheStockCombustible = "PitFeuRocheStockCombustible";
	private const string MetaPitFeuRocheFinCombustionUnixMs = "PitFeuRocheFinCombustionUnixMs";
	private const string MetaPitFeuRocheProgressCuissonMs = "PitFeuRocheProgressCuissonMs";
	private GpuParticles3D _pitFlammeParticles;
	private Node3D _pitFlammeCroix;
	private GpuParticles3D _pitFumeeParticles;
	private static ImageTexture _textureFlammePitCache;
	private OmniLight3D _pitFlammeLight;
	private static readonly Vector3 PitFlammeCroixBasePosition = new Vector3(0f, 0.105f, 0f);
	private static readonly Vector3 PitFlammeParticlesBasePosition = new Vector3(0f, 0.18f, 0f);
	private static readonly Vector3 PitFumeeParticlesBasePosition = new Vector3(0f, 0.24f, 0f);
	private double _pitFeuResteSec = 0d;
	private double _pitFeuDernierSyncRestantSec = -1d;
	private int _pitFeuRocheStockCombustible = 0;
	private double _pitFeuRocheResteSec = 0d;
	private double _pitFeuRocheDernierSyncRestantSec = -1d;
	private double _pitFeuRocheProgressCuissonSec = 0d;
	private const string MetaTorcheAllumee = "TorcheAllumee";
	private Node3D _torcheFlamme;
	private OmniLight3D _torcheLight;

	private Gestionnaire_Monde ObtenirGestionnaireMonde()
	{
		if (_gestionnaireMondeCache != null && GodotObject.IsInstanceValid(_gestionnaireMondeCache))
			return _gestionnaireMondeCache;

		Node p = GetParent();
		while (p != null)
		{
			if (p is Gestionnaire_Monde gmDirect)
			{
				_gestionnaireMondeCache = gmDirect;
				return gmDirect;
			}
			Gestionnaire_Monde gmEnfant = p.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
			if (gmEnfant != null)
			{
				_gestionnaireMondeCache = gmEnfant;
				return gmEnfant;
			}
			p = p.GetParent();
		}

		Node scene = GetTree()?.CurrentScene;
		if (scene != null)
		{
			if (scene is Gestionnaire_Monde gmScene)
			{
				_gestionnaireMondeCache = gmScene;
				return gmScene;
			}
			Gestionnaire_Monde gmNomme = scene.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
			if (gmNomme != null)
			{
				_gestionnaireMondeCache = gmNomme;
				return gmNomme;
			}
			if (scene.FindChild("Gestionnaire_Monde", recursive: true, owned: false) is Gestionnaire_Monde gmTrouve)
			{
				_gestionnaireMondeCache = gmTrouve;
				return gmTrouve;
			}
		}

		return null;
	}

	private Joueur ObtenirJoueurMonde()
	{
		SceneTree arbre = GetTree();
		Node scene = arbre?.CurrentScene;
		return scene?.GetNodeOrNull<Joueur>("Joueur");
	}

	private void ActiverVisuelPitFeu(bool actif)
	{
		if (ID_Objet != Joueur.IdObjetPitFeu && ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		if (_pitFlammeCroix == null || !GodotObject.IsInstanceValid(_pitFlammeCroix))
		{
			_pitFlammeCroix = new Node3D
			{
				Name = "PitFeuFlammesCroix",
				Position = PitFlammeCroixBasePosition,
				Visible = false
			};
			StandardMaterial3D matFlamme = CreerMateriauFlammePitTexture();
			for (int i = 0; i < 4; i++)
			{
				var mi = new MeshInstance3D
				{
					Name = $"FlammePlan{i}",
					Mesh = new QuadMesh { Size = new Vector2(0.94f, 0.285f) },
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
				};
				mi.MaterialOverride = matFlamme;
				mi.RotationDegrees = new Vector3(0f, i * 45f, 0f);
				mi.Position = new Vector3(0f, 0.04f + i * 0.010f, 0f);
				_pitFlammeCroix.AddChild(mi);
			}
			AddChild(_pitFlammeCroix);
		}
		if (_pitFlammeParticles == null || !GodotObject.IsInstanceValid(_pitFlammeParticles))
		{
			_pitFlammeParticles = new GpuParticles3D
			{
				Name = "PitFeuFlammes",
				Amount = 84,
				Explosiveness = 0f,
				Lifetime = 0.74,
				OneShot = false,
				Emitting = false,
				Position = PitFlammeParticlesBasePosition
			};
			var meshFlamme = new QuadMesh { Size = new Vector2(0.336f, 0.135f) };
			meshFlamme.Material = CreerMateriauFlammePitTexture();
			_pitFlammeParticles.DrawPass1 = meshFlamme;
			var mat = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 1.35f, 0f),
				InitialVelocityMin = 0.055f,
				InitialVelocityMax = 0.175f,
				ScaleMin = 0.48f,
				ScaleMax = 1.16f,
				ScaleCurve = null
			};
			_pitFlammeParticles.ProcessMaterial = mat;
			AddChild(_pitFlammeParticles);
		}
		if (_pitFumeeParticles == null || !GodotObject.IsInstanceValid(_pitFumeeParticles))
		{
			_pitFumeeParticles = new GpuParticles3D
			{
				Name = "PitFeuFumee",
				Amount = 30,
				Explosiveness = 0f,
				Lifetime = 3.2,
				OneShot = false,
				Emitting = false,
				Position = PitFumeeParticlesBasePosition,
				VisibilityAabb = new Aabb(new Vector3(-1.2f, -0.6f, -1.2f), new Vector3(2.4f, 3.8f, 2.4f))
			};
			var meshFumee = new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 8, Rings = 6 };
			meshFumee.Material = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.72f, 0.72f, 0.72f, 0.62f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			_pitFumeeParticles.DrawPass1 = meshFumee;
			var matFumee = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 0.35f, 0f),
				InitialVelocityMin = 0.018f,
				InitialVelocityMax = 0.082f,
				ScaleMin = 0.26f,
				ScaleMax = 0.9f
			};
			_pitFumeeParticles.ProcessMaterial = matFumee;
			AddChild(_pitFumeeParticles);
		}
		if (_pitFlammeLight == null || !GodotObject.IsInstanceValid(_pitFlammeLight))
		{
			_pitFlammeLight = new OmniLight3D
			{
				Name = "PitFeuLumiere",
				LightColor = new Color(1.0f, 0.58f, 0.26f),
				LightEnergy = 2.2f,
				OmniRange = 5.8f,
				Position = new Vector3(0f, 0.28f, 0f),
				Visible = false
			};
			AddChild(_pitFlammeLight);
		}
		_pitFlammeCroix.Visible = actif;
		_pitFlammeParticles.Emitting = actif;
		_pitFlammeParticles.Visible = actif;
		_pitFumeeParticles.Emitting = actif;
		_pitFumeeParticles.Visible = actif;
		_pitFlammeLight.Visible = actif;
	}

	private static ImageTexture ObtenirTextureFlammePit()
	{
		if (_textureFlammePitCache != null && GodotObject.IsInstanceValid(_textureFlammePitCache))
			return _textureFlammePitCache;
		const int taille = 256;
		Image img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
		for (int y = 0; y < taille; y++)
		{
			float v = (float)y / (taille - 1);
			for (int x = 0; x < taille; x++)
			{
				float u = (float)x / (taille - 1);
				float wobble = 0.045f * Mathf.Sin(v * 18.0f + u * 33.0f) + 0.03f * Mathf.Sin(v * 47.0f);
				float langues = 0.018f * Mathf.Sin((u * 9.0f + v * 4.0f) * Mathf.Pi * 2.0f) + 0.012f * Mathf.Sin((u * 17.0f - v * 6.0f) * Mathf.Pi);
				float centre = 1.0f - Mathf.Abs(((u + wobble + langues) - 0.5f) * 2.0f);
				float profil = Mathf.Pow(Mathf.Clamp(centre, 0f, 1f), 1.55f);
				float hauteur = Mathf.Clamp(1.0f - v, 0f, 1f);
				float turbulence = 0.82f + 0.18f * Mathf.Sin(u * 52.0f + v * 29.0f) * (0.5f + 0.5f * hauteur);
				float alpha = Mathf.Clamp(profil * Mathf.Pow(hauteur, 0.46f) * turbulence, 0f, 1f);
				alpha *= 1.0f - Mathf.Clamp(v * v * 0.9f, 0f, 0.9f);
				alpha = Mathf.Clamp(alpha * 1.46f, 0f, 1f);
				float coeur = Mathf.Clamp(1.0f - Mathf.Abs((u - 0.5f) * 5.4f), 0f, 1f) * Mathf.Clamp(1.0f - v * 1.7f, 0f, 1f);
				Color baseC = new Color(1.0f, 0.36f, 0.06f, 1f);
				Color hotC = new Color(1.0f, 0.74f, 0.18f, 1f);
				Color tipC = new Color(1.0f, 0.93f, 0.62f, 1f);
				Color c = baseC.Lerp(hotC, Mathf.Clamp(v * 1.1f, 0f, 1f)).Lerp(tipC, Mathf.Clamp(v * 1.9f - 0.30f, 0f, 1f));
				c = c.Lerp(new Color(1.0f, 0.98f, 0.86f, 1f), coeur * 0.55f);
				c = c.Lerp(new Color(1.0f, 0.9f, 0.42f, 1f), Mathf.Clamp((1.0f - v) * 0.22f, 0f, 0.22f));
				c.A = alpha;
				img.SetPixel(x, y, c);
			}
		}
		_textureFlammePitCache = ImageTexture.CreateFromImage(img);
		return _textureFlammePitCache;
	}

	private static StandardMaterial3D CreerMateriauFlammePitTexture()
	{
		return new StandardMaterial3D
		{
			AlbedoTexture = ObtenirTextureFlammePit(),
			AlbedoColor = new Color(1f, 1f, 1f, 1f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			EmissionEnabled = true,
			Emission = new Color(1f, 0.6f, 0.22f),
			EmissionEnergyMultiplier = 1.9f
		};
	}

	public static void AttacherVisuelFlammeTorche(Node3D parent)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return;
		Node3D racine = parent.GetNodeOrNull<Node3D>("TorcheFlamme");
		if (racine == null)
		{
			racine = new Node3D
			{
				Name = "TorcheFlamme",
				Position = new Vector3(0f, 0.86f, 0f)
			};
			StandardMaterial3D mat = CreerMateriauFlammePitTexture();
			for (int i = 0; i < 3; i++)
			{
				var plan = new MeshInstance3D
				{
					Name = $"FlammeTorchePlan{i}",
					Mesh = new QuadMesh { Size = new Vector2(0.20f, 0.32f) },
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
					Position = new Vector3(0f, i * 0.02f, 0f),
					RotationDegrees = new Vector3(0f, i * 60f, 0f),
					MaterialOverride = mat
				};
				racine.AddChild(plan);
			}

			var flammes = new GpuParticles3D
			{
				Name = "TorcheFlammesParticles",
				Amount = 54,
				Explosiveness = 0f,
				Lifetime = 0.68,
				OneShot = false,
				Emitting = true,
				Position = new Vector3(0f, 0.03f, 0f)
			};
			var meshFlamme = new QuadMesh { Size = new Vector2(0.18f, 0.24f) };
			meshFlamme.Material = CreerMateriauFlammePitTexture();
			flammes.DrawPass1 = meshFlamme;
			flammes.ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 1.2f, 0f),
				InitialVelocityMin = 0.045f,
				InitialVelocityMax = 0.15f,
				ScaleMin = 0.42f,
				ScaleMax = 0.98f
			};
			racine.AddChild(flammes);

			var fumee = new GpuParticles3D
			{
				Name = "TorcheFumeeParticles",
				Amount = 16,
				Explosiveness = 0f,
				Lifetime = 2.5,
				OneShot = false,
				Emitting = true,
				Position = new Vector3(0f, 0.12f, 0f),
				VisibilityAabb = new Aabb(new Vector3(-0.8f, -0.4f, -0.8f), new Vector3(1.6f, 2.2f, 1.6f))
			};
			var meshFumee = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 6 };
			meshFumee.Material = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.72f, 0.72f, 0.72f, 0.52f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			fumee.DrawPass1 = meshFumee;
			fumee.ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 0.28f, 0f),
				InitialVelocityMin = 0.012f,
				InitialVelocityMax = 0.06f,
				ScaleMin = 0.22f,
				ScaleMax = 0.66f
			};
			racine.AddChild(fumee);
			parent.AddChild(racine);
		}
		racine.Visible = true;

		OmniLight3D light = parent.GetNodeOrNull<OmniLight3D>("TorcheLumiere");
		if (light == null)
		{
			light = new OmniLight3D
			{
				Name = "TorcheLumiere",
				LightColor = new Color(1.0f, 0.62f, 0.30f),
				LightEnergy = 1.7f,
				OmniRange = 5.1f,
				Position = new Vector3(0f, 0.90f, 0f)
			};
			parent.AddChild(light);
		}
		light.Visible = true;
	}

	private void ActiverVisuelTorche(bool actif)
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		if (actif)
			AttacherVisuelFlammeTorche(this);
		_torcheFlamme = GetNodeOrNull<Node3D>("TorcheFlamme");
		_torcheLight = GetNodeOrNull<OmniLight3D>("TorcheLumiere");
		if (_torcheFlamme != null)
		{
			_torcheFlamme.Visible = actif;
			GpuParticles3D flammes = _torcheFlamme.GetNodeOrNull<GpuParticles3D>("TorcheFlammesParticles");
			GpuParticles3D fumee = _torcheFlamme.GetNodeOrNull<GpuParticles3D>("TorcheFumeeParticles");
			if (flammes != null)
			{
				flammes.Emitting = actif;
				flammes.Visible = actif;
			}
			if (fumee != null)
			{
				fumee.Emitting = actif;
				fumee.Visible = actif;
			}
		}
		if (_torcheLight != null)
			_torcheLight.Visible = actif;
	}

	private void SynchroniserGenomeTorche(bool allumee)
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		GenomeAssemblage = allumee ? "TORCHE:1" : "TORCHE:0";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaTorcheAllumee, allumee);
	}

	public bool EstTorcheAllumee()
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return false;
		if ((GenomeAssemblage ?? "").StartsWith("TORCHE:1", StringComparison.Ordinal))
			return true;
		return HasMeta(MetaTorcheAllumee) && GetMeta(MetaTorcheAllumee).AsBool();
	}

	public bool ActiverTorcheAllumee()
	{
		if (ID_Objet != Joueur.IdObjetTorche || EstTorcheAllumee())
			return false;
		ActiverVisuelTorche(true);
		SynchroniserGenomeTorche(true);
		return true;
	}

	public bool EteindreTorche()
	{
		if (ID_Objet != Joueur.IdObjetTorche || !EstTorcheAllumee())
			return false;
		ActiverVisuelTorche(false);
		SynchroniserGenomeTorche(false);
		return true;
	}

	private void ChargerEtatTorcheDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		bool allumee = (GenomeAssemblage ?? "").StartsWith("TORCHE:1", StringComparison.Ordinal);
		if (!allumee && HasMeta(MetaTorcheAllumee))
			allumee = GetMeta(MetaTorcheAllumee).AsBool();
		ActiverVisuelTorche(allumee);
		SynchroniserGenomeTorche(allumee);
	}

	private void SynchroniserGenomePitFeuDepuisReste()
	{
		if (ID_Objet != Joueur.IdObjetPitFeu)
			return;
		long finMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)Mathf.Round((float)(_pitFeuResteSec * 1000.0));
		GenomeAssemblage = $"PITFEU:{finMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaPitFeuFinCombustionUnixMs, finMs);
	}

	private void SynchroniserGenomePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		AssurerGrillePitFeuRoche3Slots();
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		long finMs = _pitFeuRocheResteSec > 0.001d
			? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)Mathf.Round((float)(_pitFeuRocheResteSec * 1000.0))
			: 0L;
		long progressCuissonMs = (long)Mathf.Round((float)Math.Max(0d, _pitFeuRocheProgressCuissonSec * 1000.0));
		GenomeAssemblage = $"PITFEUROCHE:{_pitFeuRocheStockCombustible}:{finMs}:{progressCuissonMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaPitFeuRocheStockCombustible, _pitFeuRocheStockCombustible);
		SetMeta(MetaPitFeuRocheFinCombustionUnixMs, finMs);
		SetMeta(MetaPitFeuRocheProgressCuissonMs, progressCuissonMs);
	}

	private void ChargerEtatPitFeuDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetPitFeu)
			return;
		long maintenant = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long finMs = 0L;
		if (!string.IsNullOrEmpty(GenomeAssemblage) && GenomeAssemblage.StartsWith("PITFEU:", StringComparison.Ordinal))
		{
			string brut = GenomeAssemblage.Substring("PITFEU:".Length);
			long.TryParse(brut, out finMs);
		}
		else if (HasMeta(MetaPitFeuFinCombustionUnixMs))
		{
			finMs = GetMeta(MetaPitFeuFinCombustionUnixMs).AsInt64();
		}
		if (finMs > maintenant)
		{
			_pitFeuResteSec = (finMs - maintenant) / 1000.0;
			_pitFeuDernierSyncRestantSec = -1d;
			ActiverVisuelPitFeu(true);
		}
		else
		{
			_pitFeuResteSec = 0d;
			ActiverVisuelPitFeu(false);
		}
	}

	private void ChargerEtatPitFeuRocheDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		long maintenant = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long finMs = 0L;
		long progressCuissonMs = 0L;
		int stock = 0;
		if (!string.IsNullOrEmpty(GenomeAssemblage) && GenomeAssemblage.StartsWith("PITFEUROCHE:", StringComparison.Ordinal))
		{
			string brut = GenomeAssemblage.Substring("PITFEUROCHE:".Length);
			string[] morceaux = brut.Split(':');
			if (morceaux.Length >= 2)
			{
				int.TryParse(morceaux[0], out stock);
				long.TryParse(morceaux[1], out finMs);
				if (morceaux.Length >= 3)
					long.TryParse(morceaux[2], out progressCuissonMs);
			}
		}
		else
		{
			if (HasMeta(MetaPitFeuRocheStockCombustible))
				stock = Mathf.Max(0, GetMeta(MetaPitFeuRocheStockCombustible).AsInt32());
			if (HasMeta(MetaPitFeuRocheFinCombustionUnixMs))
				finMs = GetMeta(MetaPitFeuRocheFinCombustionUnixMs).AsInt64();
			if (HasMeta(MetaPitFeuRocheProgressCuissonMs))
				progressCuissonMs = Math.Max(0L, GetMeta(MetaPitFeuRocheProgressCuissonMs).AsInt64());
		}
		_pitFeuRocheStockCombustible = Mathf.Max(0, stock);
		AssurerGrillePitFeuRoche3Slots();
		if (_pitFeuRocheStockCombustible > 0 && CompterCombustiblePitFeuRocheDepuisGrille() <= 0)
			AjouterCombustiblePitFeuRocheDansGrille(_pitFeuRocheStockCombustible, 32);
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		_pitFeuRocheProgressCuissonSec = Math.Max(0d, progressCuissonMs / 1000.0);
		if (finMs > maintenant)
		{
			_pitFeuRocheResteSec = (finMs - maintenant) / 1000.0;
			_pitFeuRocheDernierSyncRestantSec = -1d;
			ActiverVisuelPitFeu(true);
		}
		else
		{
			_pitFeuRocheResteSec = 0d;
			ActiverVisuelPitFeu(false);
		}
	}

	public bool EstPitFeuAllume()
	{
		if (ID_Objet == Joueur.IdObjetPitFeu)
			return _pitFeuResteSec > 0.001d;
		if (ID_Objet == Joueur.IdObjetPitFeuRoche)
			return _pitFeuRocheResteSec > 0.001d;
		return false;
	}

	public bool ActiverPitFeuAllume(double dureeSec = DureeCombustionPitFeuSec)
	{
		if (ID_Objet != Joueur.IdObjetPitFeu)
			return false;
		_pitFeuResteSec = Math.Max(1d, dureeSec);
		_pitFeuDernierSyncRestantSec = -1d;
		ActiverVisuelPitFeu(true);
		SynchroniserGenomePitFeuDepuisReste();
		return true;
	}

	private static bool EstSlotCombustiblePitFeuRoche(SlotInventaire s)
	{
		return !s.EstVide && (s.ID == 32 || s.ID == BlocChutant.ID_BRANCHE);
	}

	private static bool EstSlotCuissonPitFeuRoche(SlotInventaire s)
	{
		return !s.EstVide && s.ID == Joueur.IdObjetSteakCru;
	}

	private static bool EstSlotResultatPitFeuRoche(SlotInventaire s)
	{
		return !s.EstVide && s.ID == Joueur.IdObjetSteakCuit;
	}

	private void AssurerGrillePitFeuRoche3Slots()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		if (GrillePlanTravailAtelier == null || GrillePlanTravailAtelier.Length < 9)
		{
			var ancienne = GrillePlanTravailAtelier;
			GrillePlanTravailAtelier = new SlotInventaire[9];
			if (ancienne != null)
			{
				int n = Mathf.Min(ancienne.Length, GrillePlanTravailAtelier.Length);
				for (int i = 0; i < n; i++)
					GrillePlanTravailAtelier[i] = ancienne[i];
			}
		}

		int totalCombustible = 0;
		int totalCru = 0;
		int totalCuit = 0;
		int nSlots = Mathf.Min(9, GrillePlanTravailAtelier.Length);
		for (int i = 0; i < nSlots; i++)
		{
			SlotInventaire s = GrillePlanTravailAtelier[i];
			if (EstSlotCombustiblePitFeuRoche(s))
				totalCombustible += Joueur.ObtenirQuantiteSlot(s);
			else if (EstSlotCuissonPitFeuRoche(s))
				totalCru += Joueur.ObtenirQuantiteSlot(s);
			else if (EstSlotResultatPitFeuRoche(s))
				totalCuit += Joueur.ObtenirQuantiteSlot(s);
		}

		for (int i = 0; i < nSlots; i++)
			GrillePlanTravailAtelier[i] = new SlotInventaire();

		var combustible = new SlotInventaire { ID = 32, Quantite = 1, IndexBotanique = LSystem_Botanique.IndexChene };
		int maxCombustible = Mathf.Max(1, Joueur.ObtenirPileMax(combustible));
		combustible.Quantite = Mathf.Clamp(totalCombustible, 0, maxCombustible);
		if (combustible.Quantite > 0)
			GrillePlanTravailAtelier[PitFeuRocheSlotCombustible] = combustible;

		var cru = new SlotInventaire { ID = Joueur.IdObjetSteakCru, Quantite = 1 };
		int maxCru = Mathf.Max(1, Joueur.ObtenirPileMax(cru));
		cru.Quantite = Mathf.Clamp(totalCru, 0, maxCru);
		if (cru.Quantite > 0)
			GrillePlanTravailAtelier[PitFeuRocheSlotCuisson] = cru;

		var cuit = new SlotInventaire { ID = Joueur.IdObjetSteakCuit, Quantite = 1 };
		int maxCuit = Mathf.Max(1, Joueur.ObtenirPileMax(cuit));
		cuit.Quantite = Mathf.Clamp(totalCuit, 0, maxCuit);
		if (cuit.Quantite > 0)
			GrillePlanTravailAtelier[PitFeuRocheSlotResultat] = cuit;
	}

	private int CompterCombustiblePitFeuRocheDepuisGrille()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null)
			return 0;
		AssurerGrillePitFeuRoche3Slots();
		SlotInventaire slot = GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!EstSlotCombustiblePitFeuRoche(slot))
			return 0;
		return Mathf.Clamp(Joueur.ObtenirQuantiteSlot(slot), 0, 999);
	}

	private int AjouterCombustiblePitFeuRocheDansGrille(int quantite, int idCombustible)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null || quantite <= 0)
			return 0;
		AssurerGrillePitFeuRoche3Slots();
		if (idCombustible != 32 && idCombustible != BlocChutant.ID_BRANCHE)
			idCombustible = 32;
		ref SlotInventaire slot = ref GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!slot.EstVide && !EstSlotCombustiblePitFeuRoche(slot))
			return 0;
		if (!slot.EstVide && slot.ID != idCombustible)
			return 0;

		if (slot.EstVide)
		{
			slot = new SlotInventaire
			{
				ID = idCombustible,
				Quantite = 0,
				IndexBotanique = LSystem_Botanique.IndexChene
			};
		}
		int maxPile = Mathf.Max(1, Joueur.ObtenirPileMax(slot));
		int q = Joueur.ObtenirQuantiteSlot(slot);
		int depose = Mathf.Min(Mathf.Max(0, maxPile - q), quantite);
		if (depose <= 0)
			return 0;
		slot.Quantite = q + depose;
		return depose;
	}

	private bool RetirerCombustiblePitFeuRocheDepuisGrille(int quantite)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null || quantite <= 0)
			return false;
		AssurerGrillePitFeuRoche3Slots();
		ref SlotInventaire slot = ref GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!EstSlotCombustiblePitFeuRoche(slot))
			return false;
		int q = Joueur.ObtenirQuantiteSlot(slot);
		if (q < quantite)
			return false;
		int restant = q - quantite;
		if (restant <= 0) slot = new SlotInventaire();
		else slot.Quantite = restant;
		return true;
	}

	private void ReinitialiserProgressCuissonPitFeuRoche()
	{
		if (_pitFeuRocheProgressCuissonSec <= 0.001d)
		{
			_pitFeuRocheProgressCuissonSec = 0d;
			return;
		}
		_pitFeuRocheProgressCuissonSec = 0d;
		SynchroniserGenomePitFeuRoche();
	}

	private void TraiterCuissonPitFeuRoche(double delta)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null || _pitFeuRocheResteSec <= 0.001d)
			return;
		AssurerGrillePitFeuRoche3Slots();

		ref SlotInventaire slotCuisson = ref GrillePlanTravailAtelier[PitFeuRocheSlotCuisson];
		ref SlotInventaire slotResultat = ref GrillePlanTravailAtelier[PitFeuRocheSlotResultat];

		if (!EstSlotCuissonPitFeuRoche(slotCuisson))
		{
			ReinitialiserProgressCuissonPitFeuRoche();
			return;
		}

		if (!slotResultat.EstVide)
		{
			if (!EstSlotResultatPitFeuRoche(slotResultat))
			{
				ReinitialiserProgressCuissonPitFeuRoche();
				return;
			}
			int maxPileRes = Mathf.Max(1, Joueur.ObtenirPileMax(slotResultat));
			if (Joueur.ObtenirQuantiteSlot(slotResultat) >= maxPileRes)
			{
				ReinitialiserProgressCuissonPitFeuRoche();
				return;
			}
		}

		_pitFeuRocheProgressCuissonSec += Math.Max(0d, delta);
		bool conversion = false;
		while (_pitFeuRocheProgressCuissonSec >= DureeCuissonPitFeuRocheSteakSec)
		{
			if (!EstSlotCuissonPitFeuRoche(slotCuisson))
			{
				ReinitialiserProgressCuissonPitFeuRoche();
				break;
			}

			var steakCuit = slotCuisson;
			steakCuit.ID = Joueur.IdObjetSteakCuit;
			steakCuit.Quantite = 1;

			if (slotResultat.EstVide)
			{
				slotResultat = steakCuit;
			}
			else
			{
				int maxPileRes = Mathf.Max(1, Joueur.ObtenirPileMax(slotResultat));
				if (!Joueur.SontEmpilables(slotResultat, steakCuit) || Joueur.ObtenirQuantiteSlot(slotResultat) >= maxPileRes)
					break;
				slotResultat.Quantite = Joueur.ObtenirQuantiteSlot(slotResultat) + 1;
			}

			int qCru = Joueur.ObtenirQuantiteSlot(slotCuisson) - 1;
			if (qCru <= 0) slotCuisson = new SlotInventaire();
			else slotCuisson.Quantite = qCru;
			ObtenirJoueurMonde()?.AjouterXpMetier("Cuisinier", 1UL);

			_pitFeuRocheProgressCuissonSec -= DureeCuissonPitFeuRocheSteakSec;
			conversion = true;
		}

		if (conversion)
			SynchroniserGenomePitFeuRoche();
	}

	public int ObtenirStockCombustiblePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return 0;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		return Mathf.Max(0, _pitFeuRocheStockCombustible);
	}

	public bool EstPitFeuRocheAllume()
	{
		return ID_Objet == Joueur.IdObjetPitFeuRoche && _pitFeuRocheResteSec > 0.001d;
	}

	public bool AjouterCombustiblePitFeuRoche(int quantite = 1, int idCombustible = 32)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		if (quantite <= 0)
			return false;
		int stockAvant = CompterCombustiblePitFeuRocheDepuisGrille();
		int espace = Mathf.Max(0, 999 - stockAvant);
		if (espace <= 0)
			return false;
		int ajoute = AjouterCombustiblePitFeuRocheDansGrille(Mathf.Min(espace, quantite), idCombustible);
		if (ajoute <= 0)
			return false;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		SynchroniserGenomePitFeuRoche();
		return true;
	}

	public bool ActiverPitFeuRocheAllume(double dureeSec = DureeCombustionPitFeuSec)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		if (_pitFeuRocheResteSec > 0.001d)
			return true;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		if (_pitFeuRocheStockCombustible <= 0)
			return false;
		if (!RetirerCombustiblePitFeuRocheDepuisGrille(1))
			return false;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		_pitFeuRocheResteSec = Math.Max(1d, dureeSec);
		_pitFeuRocheDernierSyncRestantSec = -1d;
		ActiverVisuelPitFeu(true);
		SynchroniserGenomePitFeuRoche();
		return true;
	}

	public bool EteindrePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		_pitFeuRocheResteSec = 0d;
		_pitFeuRocheDernierSyncRestantSec = -1d;
		ActiverVisuelPitFeu(false);
		SynchroniserGenomePitFeuRoche();
		return true;
	}

	public void SynchroniserCombustiblePitFeuRocheDepuisGrille()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		AssurerGrillePitFeuRoche3Slots();
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		SynchroniserGenomePitFeuRoche();
	}

	private Vector3 ObtenirDemiExtentsApproxObjet()
	{
		Vector3 tailleMax = Vector3.Zero;
		foreach (Node c in GetChildren())
		{
			if (c is not CollisionShape3D cs || cs.Shape == null)
				continue;
			Vector3 tailleForme = cs.Shape switch
			{
				BoxShape3D box => box.Size,
				SphereShape3D sphere => Vector3.One * (sphere.Radius * 2f),
				CapsuleShape3D capsule => new Vector3(capsule.Radius * 2f, capsule.Height + capsule.Radius * 2f, capsule.Radius * 2f),
				CylinderShape3D cylinder => new Vector3(cylinder.Radius * 2f, cylinder.Height, cylinder.Radius * 2f),
				_ => Vector3.Zero
			};
			if (tailleForme == Vector3.Zero)
				continue;
			Vector3 scaleLocal = new Vector3(Mathf.Abs(cs.Scale.X), Mathf.Abs(cs.Scale.Y), Mathf.Abs(cs.Scale.Z));
			tailleForme *= scaleLocal;
			tailleMax = new Vector3(
				Mathf.Max(tailleMax.X, tailleForme.X),
				Mathf.Max(tailleMax.Y, tailleForme.Y),
				Mathf.Max(tailleMax.Z, tailleForme.Z));
		}

		if (tailleMax.LengthSquared() < 1e-6f)
		{
			foreach (Node c in GetChildren())
			{
				if (c is MeshInstance3D mi && mi.Mesh != null)
				{
					Vector3 tailleMesh = mi.Mesh.GetAabb().Size;
					Vector3 scaleMesh = new Vector3(Mathf.Abs(mi.Scale.X), Mathf.Abs(mi.Scale.Y), Mathf.Abs(mi.Scale.Z));
					tailleMesh *= scaleMesh;
					tailleMax = new Vector3(
						Mathf.Max(tailleMax.X, tailleMesh.X),
						Mathf.Max(tailleMax.Y, tailleMesh.Y),
						Mathf.Max(tailleMax.Z, tailleMesh.Z));
				}
			}
		}

		if (tailleMax.LengthSquared() < 1e-6f)
			tailleMax = new Vector3(0.35f, 0.35f, 0.35f);

		return tailleMax * 0.5f;
	}

	private float CalculerRatioImmersionObjet(Gestionnaire_Monde gm)
	{
		if (gm == null)
			return 0f;
		Vector3 demi = ObtenirDemiExtentsApproxObjet();
		float x = Mathf.Max(0.05f, demi.X * 0.9f);
		float yBas = -Mathf.Max(0.05f, demi.Y * 0.9f);
		float yMilieu = 0f;
		float yHaut = Mathf.Max(0.05f, demi.Y * 0.9f);
		float z = Mathf.Max(0.05f, demi.Z * 0.9f);

		_echantillonsImmersionObjet[0] = new Vector3(0f, yBas, 0f);
		_echantillonsImmersionObjet[1] = new Vector3(0f, yMilieu, 0f);
		_echantillonsImmersionObjet[2] = new Vector3(0f, yHaut, 0f);
		_echantillonsImmersionObjet[3] = new Vector3(x, yMilieu, 0f);
		_echantillonsImmersionObjet[4] = new Vector3(-x, yMilieu, 0f);
		_echantillonsImmersionObjet[5] = new Vector3(0f, yMilieu, z);
		_echantillonsImmersionObjet[6] = new Vector3(0f, yMilieu, -z);
		Transform3D xf = GlobalTransform;
		int pointsImmerges = 0;
		for (int i = 0; i < _echantillonsImmersionObjet.Length; i++)
		{
			if (gm.EstPointImmergeEau(xf * _echantillonsImmersionObjet[i]))
				pointsImmerges++;
		}
		return pointsImmerges / (float)_echantillonsImmersionObjet.Length;
	}

	private void TransformerIntestinEnVersionNettoyee()
	{
		ID_Objet = Joueur.IdObjetIntestinBoeufNettoye;
		// Le ramassage lit souvent ID_Matiere sur les objets "BlocsPoses":
		// il faut synchroniser la meta sinon l'inventaire reçoit encore l'intestin sale (118).
		SetMeta("ID_Matiere", ID_Objet);
		_tempsImmersionIntestin = 0d;
		Node3D meshRoot = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (meshRoot != null)
			Joueur.InstancierModeleIntestinBoeufNettoye(meshRoot, new SlotInventaire { ID = Joueur.IdObjetIntestinBoeufNettoye }, 0.24f);
		GD.Print("ZERO-K : Intestin nettoyé à l'eau (intestin propre).");
	}

	private static MeshInstance3D TrouverPremierMeshInstanceAvecMesh(Node n)
	{
		if (n is MeshInstance3D mi && mi.Mesh != null)
			return mi;
		foreach (Node c in n.GetChildren())
		{
			MeshInstance3D r = TrouverPremierMeshInstanceAvecMesh(c);
			if (r != null) return r;
		}
		return null;
	}

	public override void _Ready()
	{
		// CORRECTION CRITIQUE : Chercher dans THIS, pas dans GetParent()
		MeshInstance3D visuel = null;
		CollisionShape3D hitbox = null;
		foreach (Node child in this.GetChildren())
		{
			if (child is MeshInstance3D mi) visuel = mi;
			else if (child is CollisionShape3D cs) hitbox = cs;
		}

		if ((ID_Objet == 20 || ID_Objet == 21 || ID_Objet == Joueur.IdObjetCeinturePoches || ID_Objet == Joueur.IdObjetCeintureSacoches || ID_Objet == Joueur.IdObjetPochetteTier0 || ID_Objet == Joueur.IdObjetSacTier0 || ID_Objet == Joueur.IdObjetCarnetSavoir) && visuel == null)
			visuel = TrouverPremierMeshInstanceAvecMesh(this);

		if (ID_Objet == 200)
		{
			Mass = 2800f;
			GravityScale = 0f;
			ResistanceActuelle = 80f;
			Scale = Vector3.One;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			return;
		}
		if (ID_Objet == Joueur.IdObjetTableAnalyseTier1)
		{
			Mass = 2400f;
			GravityScale = 0f;
			ResistanceActuelle = 86f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			return;
		}
		if (ID_Objet == Joueur.IdObjetRackBatons || ID_Objet == Joueur.IdObjetRackBuches)
		{
			Mass = 1200f;
			GravityScale = 0f;
			ResistanceActuelle = 65f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			return;
		}
		if (ID_Objet == Joueur.IdObjetCoffreBoisTier0)
		{
			Mass = 42f;
			GravityScale = 0f;
			ResistanceActuelle = 38f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			return;
		}
		if (ID_Objet == Joueur.IdObjetPitFeu)
		{
			Mass = 26f;
			GravityScale = 0f;
			ResistanceActuelle = 42f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			ChargerEtatPitFeuDepuisGenome();
			if (_pitFeuResteSec <= 0.001d)
				ActiverVisuelPitFeu(false);
			return;
		}
		if (ID_Objet == Joueur.IdObjetPitFeuRoche)
		{
			Mass = 34f;
			GravityScale = 0f;
			ResistanceActuelle = 48f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			ChargerEtatPitFeuRocheDepuisGenome();
			if (_pitFeuRocheResteSec <= 0.001d)
				ActiverVisuelPitFeu(false);
			return;
		}
		if (ID_Objet == Joueur.IdObjetFondationBois
			|| ID_Objet == Joueur.IdObjetFondationRoche
			|| ID_Objet == Joueur.IdObjetFondationBoisSoleRoche
			|| ID_Objet == Joueur.IdObjetFondationRocheSoleBois)
		{
			Mass = ID_Objet == Joueur.IdObjetFondationBois ? 38f
				: (ID_Objet == Joueur.IdObjetFondationRoche ? 62f
				: (ID_Objet == Joueur.IdObjetFondationBoisSoleRoche ? 54f : 58f));
			GravityScale = 0f;
			ResistanceActuelle = 90f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			return;
		}
		if (ID_Objet == Joueur.IdObjetMuretBois || ID_Objet == Joueur.IdObjetMuretPierre || ID_Objet == Joueur.IdObjetMurBois || ID_Objet == Joueur.IdObjetMurBoisFenetre || ID_Objet == Joueur.IdObjetMurBoisCadrePorte || ID_Objet == Joueur.IdObjetPorteBois || ID_Objet == Joueur.IdObjetToitChaume)
		{
			Mass = ID_Objet == Joueur.IdObjetPorteBois
				? 18f
				: (ID_Objet == Joueur.IdObjetToitChaume
					? 14f
					: ((ID_Objet == Joueur.IdObjetMurBois || ID_Objet == Joueur.IdObjetMurBoisFenetre || ID_Objet == Joueur.IdObjetMurBoisCadrePorte) ? 24f : 16f));
			GravityScale = 0f;
			ResistanceActuelle = 76f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			return;
		}
		if (ID_Objet == Joueur.IdObjetTorche)
		{
			Mass = 6f;
			GravityScale = 0f;
			ResistanceActuelle = 22f;
			Scale = Vector3.One;
			LinearVelocity = Vector3.Zero;
			AngularVelocity = Vector3.Zero;
			Sleeping = true;
			Freeze = true;
			FreezeMode = FreezeModeEnum.Static;
			ChargerEtatTorcheDepuisGenome();
			return;
		}
		if (ID_Objet == Joueur.IdObjetAllumeFeu)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 10, 11);
			Mass = 0.26f;
			ResistanceActuelle = 24f;
			Scale = Vector3.One;
			return;
		}
		if (ID_Objet == Joueur.IdObjetFenetreBois)
		{
			Mass = 6.5f;
			ResistanceActuelle = 26f;
			Scale = Vector3.One;
			return;
		}

		if (visuel == null || hitbox == null) return;

		if (EstIdRocheMatiere(ID_Objet))
			IndexChimique = IndexChimiqueDepuisIdRoche(ID_Objet);
		else if (IndexChimique == -1)
			IndexChimique = GD.RandRange(0, TableGeologique.Length - 1);

		// LE BOUCLIER : Si c'est un éclat coupé procéduralement, on ne génère RIEN depuis le cache.
		if (EstUnEclat)
		{
			int ch = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			if (EstIdRocheMatiere(ID_Objet))
				visuel.MaterialOverride = CreerMaterielProcedural(EstMatiereSilexParIdObjet(ID_Objet), ch);
			else if (ID_Objet == 30 || ID_Objet == 32 || ID_Objet == BlocChutant.ID_BRANCHE)
				visuel.MaterialOverride = ID_Objet == 32 && IndexChimique == 1 && IndexBotanique == LSystem_Botanique.IndexChene
					? ArbreVivant.ObtenirMaterielBoisTriplanarBatonChenEPale()
					: ArbreVivant.ObtenirMaterielBoisTriplanar(IndexBotanique);
			if (!EstMatiereSilexParIdObjet(ID_Objet) && !_surImpactConnecte)
			{
				ContactMonitor = true;
				MaxContactsReported = 1;
				BodyEntered += SurImpactPhysique;
				_surImpactConnecte = true;
			}
			if (EstIdRocheMatiere(ID_Objet))
				AppliquerPhysiqueRochePortee(this);
			return;
		}

		// Fibre (15), corde (20), tissu (21), ceinture (102), pochette tier 0 (103) : mesh et matériau déjà assignés par Joueur.CreerBlocPose / BlocChutant.
		if (ID_Objet == 15 || ID_Objet == 20 || ID_Objet == 21 || ID_Objet == Joueur.IdObjetCeinturePoches || ID_Objet == Joueur.IdObjetCeintureSacoches || ID_Objet == Joueur.IdObjetPochetteTier0 || ID_Objet == Joueur.IdObjetSacTier0 || ID_Objet == Joueur.IdObjetCarnetSavoir)
		{
			Mass = ID_Objet == 21 ? 0.1f : (ID_Objet == Joueur.IdObjetCeinturePoches ? 0.14f : (ID_Objet == Joueur.IdObjetCeintureSacoches ? 0.18f : (ID_Objet == Joueur.IdObjetPochetteTier0 ? 0.12f : (ID_Objet == Joueur.IdObjetSacTier0 ? 0.16f : (ID_Objet == Joueur.IdObjetCarnetSavoir ? 0.24f : 0.08f)))));
			ResistanceActuelle = 1f;
			return;
		}
		if (ID_Objet == Joueur.IdObjetAtelleJambe || ID_Objet == Joueur.IdObjetAtelleBras)
		{
			Mass = 0.34f;
			ResistanceActuelle = 8f;
			Scale = Vector3.One;
			return;
		}
		if (ID_Objet == Joueur.IdObjetBandageTier1)
		{
			Mass = 0.12f;
			ResistanceActuelle = 4f;
			Scale = Vector3.One;
			return;
		}
		// Dague primitive (105) : GLB multi-surfaces injecté par Joueur.InstancierModeleArme — pas de cache caillou.
		if (ID_Objet == 105)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			Mass = 0.32f;
			ResistanceActuelle = 20f;
			Scale = Vector3.One;
			AppliquerPhysiqueDague105(this);
			return;
		}
		if (ID_Objet == 106 || ID_Objet == Joueur.IdObjetHachePierreTier1)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			Mass = ID_Objet == Joueur.IdObjetHachePierreTier1 ? 0.64f : 0.58f;
			ResistanceActuelle = ID_Objet == Joueur.IdObjetHachePierreTier1 ? 30f : 28f;
			Scale = Vector3.One;
			AppliquerPhysiqueHachette106(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetPellePierreTier0)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			Mass = 0.62f;
			ResistanceActuelle = 30f;
			Scale = Vector3.One;
			AppliquerPhysiquePelle107(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetPiochePierreTier0)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			Mass = 0.66f;
			ResistanceActuelle = 32f;
			Scale = Vector3.One;
			AppliquerPhysiquePioche108(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetFauxPierreTier0)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			Mass = 0.38f;
			ResistanceActuelle = 22f;
			Scale = Vector3.One;
			AppliquerPhysiqueFaux112(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetLancePierreTier0)
		{
			IndexChimique = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
			Mass = 0.60f;
			ResistanceActuelle = 30f;
			Scale = Vector3.One;
			AppliquerPhysiqueLance111(this);
			return;
		}
		// Bûche (30), bâton (32) et branche (31) : propriétés depuis le profil botanique (chêne, pin, …) pour masse / flottaison.
		if (ID_Objet == 30 || ID_Objet == 32 || ID_Objet == BlocChutant.ID_BRANCHE)
		{
			ProfilBotanique p = LSystem_Botanique.ObtenirProfil(IndexBotanique);
			// Bois vert / cellulosique : un peu moins dense que l’eau (1000) pour flotter crédiblement une fois sec/humide jeu.
			float densiteKgM3 = 520f * (p.MasseDensite / 0.85f);
			float volRef, vol;
			Vector3 sc0 = Scale;
			if (sc0.LengthSquared() < 1e-8f) sc0 = Vector3.One;
			if (visuel.Mesh is CylinderMesh cyl)
			{
				float rr = cyl.TopRadius;
				float hh = cyl.Height;
				vol = Mathf.Pi * rr * rr * hh * sc0.X * sc0.Y * sc0.Z;
				volRef = ID_Objet == 30 ? (Mathf.Pi * 0.12f * 0.12f * 0.6f) : (Mathf.Pi * 0.02f * 0.02f * 0.5f);
			}
			else if (visuel.Mesh != null)
			{
				// Bûche/bâton sculpté (ArrayMesh) : volume depuis AABB pour masse cohérente avec la forme
				Vector3 sz = visuel.Mesh.GetAabb().Size;
				vol = Mathf.Abs(sz.X * sz.Y * sz.Z);
				volRef = ID_Objet == 30 ? (Mathf.Pi * 0.12f * 0.12f * 0.6f) : (Mathf.Pi * 0.02f * 0.02f * 0.5f);
			}
			else
			{
				volRef = ID_Objet == 30 ? (Mathf.Pi * 0.12f * 0.12f * 0.6f) : (Mathf.Pi * 0.02f * 0.02f * 0.5f);
				vol = volRef * sc0.X * sc0.Y * sc0.Z;
			}
			if (ID_Objet == 30)
			{
				volRef = Mathf.Pi * 0.12f * 0.12f * 0.6f;
				ResistanceActuelle = p.ResistanceHache * 0.075f * Mathf.Clamp(Mathf.Pow(vol / volRef, 0.35f), 0.28f, 3.2f);
			}
			else
			{
				// Bâton (32) ou branche (31) : même ordre de grandeur de résistance relative au volume.
				volRef = Mathf.Pi * 0.02f * 0.02f * 0.5f;
				ResistanceActuelle = p.ResistanceHache * 0.015f * Mathf.Clamp(Mathf.Pow(vol / volRef, 0.35f), 0.2f, 2.5f);
			}
			Mass = Mathf.Max(0.015f, vol * densiteKgM3);
			PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.18f, Friction = 0.78f };
			ContactMonitor = true;
			MaxContactsReported = 12;
			Scale = Vector3.One;
			return;
		}

		if (EstIdRocheMatiere(ID_Objet))
		{
			IndexTailleRoche = Mathf.Clamp(IndexTailleRoche, 0, 4);
			if (IndexCacheMemoire < 0)
				IndexCacheMemoire = GD.RandRange(0, 3);
			IndexCacheMemoire = Mathf.Clamp(IndexCacheMemoire, 0, 3);

			// Mesh + collision déjà forgés dans Joueur.CreerBlocPose : ne pas remplacer par une sphère cache (sinon casse + hitbox fausse).
			if (HasMeta(MetaRocheForgeeParJoueur) && GetMeta(MetaRocheForgeeParJoueur).AsBool())
			{
				IndexChimique = IndexChimiqueDepuisIdRoche(ID_Objet);
				Scale = Vector3.One;
				visuel.Scale = Vector3.One;
				hitbox.Scale = Vector3.One;
				int idxChimR = IndexChimique;
				Vector3 sz = visuel.Mesh != null ? visuel.Mesh.GetAabb().Size : Vector3.One * 0.2f;
				float vol = Mathf.Max(1e-8f, Mathf.Abs(sz.X * sz.Y * sz.Z));
				Mass = Mathf.Max(0.04f, vol * 2200f);
				ResistanceActuelle = TableGeologique[idxChimR].ResistanceFuture * FacteurSoliditeRochesParTaille(IndexTailleRoche);
				if (!EstMatiereSilexParIdObjet(ID_Objet) && !_surImpactConnecte)
				{
					ContactMonitor = true;
					MaxContactsReported = 1;
					BodyEntered += SurImpactPhysique;
					_surImpactConnecte = true;
				}
				return;
			}

			IndexChimique = IndexChimiqueDepuisIdRoche(ID_Objet);
			float r = RayonBaseRochesJoueur(IndexTailleRoche);
			Vector3 morph = EchelleMorphologieRoche(IndexCacheMemoire);
			Scale = Vector3.One;
			visuel.Scale = morph;
			hitbox.Scale = Vector3.One;
			visuel.Mesh = new SphereMesh { Radius = r, Height = r * 2f };
			hitbox.Shape = CreerShapeCollisionRocheMatiere(r, IndexCacheMemoire);
			AppliquerMateriel(visuel);
			int idxChimR2 = IndexChimiqueDepuisIdRoche(ID_Objet);
			ResistanceActuelle = TableGeologique[idxChimR2].ResistanceFuture * FacteurSoliditeRochesParTaille(IndexTailleRoche);
			float volSph = 4f / 3f * Mathf.Pi * r * r * r;
			Mass = Mathf.Max(0.04f, volSph * 2200f * Mathf.Abs(morph.X * morph.Y * morph.Z));
			if (!EstMatiereSilexParIdObjet(ID_Objet) && !_surImpactConnecte)
			{
				ContactMonitor = true;
				MaxContactsReported = 1;
				BodyEntered += SurImpactPhysique;
				_surImpactConnecte = true;
			}
			RotationDegrees = new Vector3(GD.RandRange(0, 360), GD.RandRange(0, 360), GD.RandRange(0, 360));
			AppliquerPhysiqueRochePortee(this);
			return;
		}

		AppliquerMateriel(visuel);

		// MODIFICATION CRITIQUE : Si IndexCacheMemoire déjà défini (objet relâché par le joueur), on NE TIRE PAS au hasard.
		if (IndexCacheMemoire == -1)
			IndexCacheMemoire = PreparerCacheEtTirerIndex(false);

		// Biomécanique : résistance aux chocs (pour physique de rupture)
		int idxChim = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
		ResistanceActuelle = TableGeologique[idxChim].ResistanceFuture;

		if (!_surImpactConnecte)
		{
			ContactMonitor = true;
			MaxContactsReported = 1;
			BodyEntered += SurImpactPhysique;
			_surImpactConnecte = true;
		}

		int idx = Mathf.Clamp(IndexCacheMemoire, 0, int.MaxValue);
		if (idx < _cacheMeshCaillou.Count)
		{
			visuel.Mesh = _cacheMeshCaillou[idx];
			hitbox.Shape = _cacheCollisionCaillou[idx];
		}
		Scale = Vector3.One;

		RotationDegrees = new Vector3(GD.RandRange(0, 360), GD.RandRange(0, 360), GD.RandRange(0, 360));
	}

	/// <summary>Bûche (30) / bâton (32) : flottent dans l’eau voxel si l’essence est moins dense que l’eau (ex. chêne 0,85).
	/// Pas de flottaison sur terre : évite les forces géantes qui faisaient traverser le sol.</summary>
	public override void _PhysicsProcess(double delta)
	{
		if (_bovinPlante != null)
		{
			if (!GodotObject.IsInstanceValid(_bovinPlante) || !_bovinPlante.IsInsideTree())
			{
				_bovinPlante = null;
				QueueFree();
				return;
			}
			GlobalPosition = _bovinPlante.ToGlobal(_offsetLocalDansBovinPlante);
			return;
		}

		if (ID_Objet == Joueur.IdObjetIntestinBoeuf)
		{
			Gestionnaire_Monde gmIntestin = ObtenirGestionnaireMonde();
			float ratioIntestin = 0f;
			if (gmIntestin != null)
			{
				ratioIntestin = CalculerRatioImmersionObjet(gmIntestin);
				if (ratioIntestin < 0.5f && gmIntestin.EstPointImmergeEau(GlobalPosition))
					ratioIntestin = 0.5f;
			}
			if (ratioIntestin >= 0.5f)
				_tempsImmersionIntestin += delta;
			else
				_tempsImmersionIntestin = 0d;
			if (_tempsImmersionIntestin >= 0.35d)
				TransformerIntestinEnVersionNettoyee();
			return;
		}
		if (ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche)
		{
			bool estRoche = ID_Objet == Joueur.IdObjetPitFeuRoche;
			double resteSec = estRoche ? _pitFeuRocheResteSec : _pitFeuResteSec;
			if (resteSec <= 0.001d)
				return;
			if (estRoche)
				TraiterCuissonPitFeuRoche(delta);
			resteSec -= delta;
			float t = (float)Time.GetTicksMsec() * 0.001f;
			float pulseFast = Mathf.Sin(t * 8.9f);
			float pulseSlow = Mathf.Sin(t * 3.7f + 1.2f);
			float swayX = 0.68f * Mathf.Sin(t * 1.6f) + 0.32f * Mathf.Sin(t * 2.9f + 0.8f);
			float swayZ = 0.62f * Mathf.Sin(t * 1.9f + 0.35f) + 0.38f * Mathf.Sin(t * 3.3f + 1.4f);
			if (_pitFlammeCroix != null && GodotObject.IsInstanceValid(_pitFlammeCroix))
			{
				float ampX = 0.95f + 0.08f * pulseFast + 0.04f * pulseSlow;
				float ampY = 1.08f + 0.16f * Mathf.Sin(t * 7.4f + 0.5f) + 0.08f * pulseSlow;
				float ampZ = 0.95f + 0.075f * Mathf.Sin(t * 8.1f + 0.9f) + 0.035f * pulseSlow;
				_pitFlammeCroix.Scale = new Vector3(ampX, ampY, ampZ);
				_pitFlammeCroix.RotationDegrees = new Vector3(2.7f * swayX, 0f, 2.1f * swayZ);
				_pitFlammeCroix.Position = PitFlammeCroixBasePosition + new Vector3(0.012f * swayX, 0.018f * Mathf.Sin(t * 5.4f), 0.012f * swayZ);
			}
			if (_pitFlammeParticles != null && GodotObject.IsInstanceValid(_pitFlammeParticles))
			{
				float gust = 0.5f + 0.5f * Mathf.Sin(t * 2.25f + 0.3f);
				_pitFlammeParticles.Position = PitFlammeParticlesBasePosition + new Vector3(0.011f * swayX, 0.018f * gust, 0.011f * swayZ);
				if (_pitFlammeParticles.ProcessMaterial is ParticleProcessMaterial matFlamme)
				{
					matFlamme.Direction = new Vector3(0.13f * swayX, 1.0f, 0.13f * swayZ);
					matFlamme.InitialVelocityMin = 0.055f + 0.016f * gust;
					matFlamme.InitialVelocityMax = 0.175f + 0.046f * gust;
				}
			}
			if (_pitFumeeParticles != null && GodotObject.IsInstanceValid(_pitFumeeParticles))
			{
				_pitFumeeParticles.Position = PitFumeeParticlesBasePosition + new Vector3(0.006f * swayX, 0.012f * Mathf.Sin(t * 2.8f + 0.4f), 0.006f * swayZ);
			}
			if (_pitFlammeLight != null && GodotObject.IsInstanceValid(_pitFlammeLight))
			{
				_pitFlammeLight.LightEnergy = 2.05f + 0.28f * Mathf.Sin(t * 9.6f) + 0.14f * pulseSlow;
			}
			if (resteSec <= 0.001d)
			{
				if (estRoche)
				{
					_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
					if (_pitFeuRocheStockCombustible > 0 && RetirerCombustiblePitFeuRocheDepuisGrille(1))
					{
						_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
						resteSec = DureeCombustionPitFeuSec;
					}
					else
					{
						resteSec = 0d;
						ActiverVisuelPitFeu(false);
					}
				}
				else
				{
					ActiverVisuelPitFeu(false);
					QueueFree();
					return;
				}
			}
			if (estRoche)
			{
				_pitFeuRocheResteSec = Math.Max(0d, resteSec);
				if (_pitFeuRocheDernierSyncRestantSec < 0d || Math.Abs(_pitFeuRocheDernierSyncRestantSec - _pitFeuRocheResteSec) >= 1.0d)
				{
					_pitFeuRocheDernierSyncRestantSec = _pitFeuRocheResteSec;
					SynchroniserGenomePitFeuRoche();
				}
			}
			else
			{
				_pitFeuResteSec = Math.Max(0d, resteSec);
				if (_pitFeuDernierSyncRestantSec < 0d || Math.Abs(_pitFeuDernierSyncRestantSec - _pitFeuResteSec) >= 1.0d)
				{
					_pitFeuDernierSyncRestantSec = _pitFeuResteSec;
					SynchroniserGenomePitFeuDepuisReste();
				}
			}
			return;
		}

		// Roches matière : correction dynamique par morphologie (freinage réel, eau, et redressement des plates).
		if (EstIdRocheMatiere(ID_Objet))
		{
			int m = Mathf.Clamp(IndexCacheMemoire, 0, 3);
			Gestionnaire_Monde gmRoche = ObtenirGestionnaireMonde();
			bool dansEau = gmRoche != null && CalculerRatioImmersionObjet(gmRoche) >= 0.5f;
			// Hors eau : on laisse friction/rebond du PhysicsMaterial (moteur) — pas de forces « magiques ».
			if (dansEau)
			{
				ApplyCentralForce(-LinearVelocity * Mass * 2.2f);
				ApplyTorque(-AngularVelocity * Mass * 1.25f);
				return;
			}
			// Roche plate à l’air : léger couple pour retomber sur la face large (stabilité réaliste).
			if (m == 1)
			{
				Vector3 upLocal = GlobalTransform.Basis.Y.Normalized();
				Vector3 axeCorrection = upLocal.Cross(Vector3.Up);
				if (axeCorrection.LengthSquared() > 1e-6f)
					ApplyTorque(axeCorrection * (Mass * 2.2f));
			}
			return;
		}

		if (ID_Objet != 30 && ID_Objet != 32 && ID_Objet != BlocChutant.ID_BRANCHE) return;
		ProfilBotanique profil = LSystem_Botanique.ObtenirProfil(IndexBotanique);
		if (profil.MasseDensite >= 1f) return;

		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm == null) return;
		float ratioImmersion = CalculerRatioImmersionObjet(gm);
		if (ratioImmersion < 0.5f) return;

		const float RHO_EAU = 1000f;
		const float G_EAU = 4f;
		float y = GlobalPosition.Y;
		float niveauBaseEau = gm.ObtenirNiveauSurfaceEau() - 0.35f;

		float rayonEff = ID_Objet == 30 ? 0.12f : 0.02f;
		float longueurEff = ID_Objet == 30 ? 0.6f : 0.5f;
		if (ID_Objet == BlocChutant.ID_BRANCHE)
		{
			rayonEff = 0.04f;
			longueurEff = 0.55f;
		}
		foreach (Node c in GetChildren())
		{
			if (c is MeshInstance3D mi && mi.Mesh is CylinderMesh cy)
			{
				rayonEff = Mathf.Max(rayonEff, cy.TopRadius);
				longueurEff = Mathf.Max(longueurEff, cy.Height);
				break;
			}
		}

		// Centre du corps un peu au-dessus du plan d’eau : cylindre couché → le rayon domine le tirant d’eau visible
		float offsetSurface = Mathf.Clamp(rayonEff * 0.55f + longueurEff * 0.08f, 0.06f, 0.65f);
		float niveauRef = niveauBaseEau + offsetSurface;
		if (y >= niveauRef + 0.35f) return;

		float massePortee = 0f;
		foreach (Node body in GetCollidingBodies())
		{
			if (body is RigidBody3D rb)
				massePortee += rb.Mass;
			else if (body is CharacterBody3D)
				massePortee += 60f;
		}

		float vol = Mathf.Pi * rayonEff * rayonEff * longueurEff;
		float poidsEau = vol * RHO_EAU * 0.0085f;
		float excesFlot = Mathf.Max(0f, poidsEau - Mass * G_EAU * 0.22f);
		ApplyCentralForce(Vector3.Up * excesFlot);

		float enfoncement = Mathf.Clamp(niveauRef - y, 0f, 0.42f);
		float kPoussee = ID_Objet == 30 ? 38f : 48f;
		ApplyCentralForce(Vector3.Up * (enfoncement * Mass * kPoussee));

		if (massePortee > 0f)
			ApplyCentralForce(Vector3.Down * (massePortee * G_EAU));

		float vy = LinearVelocity.Y;
		if (Mathf.Abs(vy) > 0.02f)
			ApplyCentralForce(Vector3.Up * (-vy * Mass * (ID_Objet == 30 ? 8f : 6f)));
		Vector3 vH = LinearVelocity;
		vH.Y = 0f;
		if (vH.LengthSquared() > 0.25f)
			ApplyCentralForce(-vH.Normalized() * Mass * 0.8f * (ID_Objet == 30 ? 1.2f : 1f));
	}

	/// <summary>Seuil de rupture (Loi du Rebond). En dessous de cette force d'impact, dégâts strictement zéro.</summary>
	public float ObtenirSeuilRupture()
	{
		if (EstMatiereSilexParIdObjet(ID_Objet)) return 80f;
		if (EstIdRocheMatiere(ID_Objet)) return 50f;
		if (ID_Objet == 30 || ID_Objet == 32 || ID_Objet == BlocChutant.ID_BRANCHE || ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche || ID_Objet == Joueur.IdObjetTorche || ID_Objet == Joueur.IdObjetFenetreBois || ID_Objet == Joueur.IdObjetTableBoisDecorative || ID_Objet == Joueur.IdObjetTableArtisanaTier1) return 40f; // Bois mort durci
		if (ID_Objet == Joueur.IdObjetAllumeFeu) return 44f;
		return 10f; // Matières souples ou organiques
	}

	/// <summary>Applique les dégâts selon la Loi du Rebond : en dessous du seuil, zéro dégât.</summary>
	/// <returns>0 = Rebond (Zéro dégât), 1 = Endommagé, 2 = Fracturé/Détruit</returns>
	public int SubirDegats(float forceImpact, Vector3 dirVue, Vector3 pointImpact)
	{
		float seuil = ObtenirSeuilRupture();
		if (forceImpact < seuil)
			return 0;

		float degats = forceImpact;
		float capPourcent;
		if (EstIdRocheMatiere(ID_Objet))
		{
			degats *= 0.060f;
			capPourcent = 0.26f;
		}
		else if (ID_Objet == 30 || ID_Objet == 32 || ID_Objet == BlocChutant.ID_BRANCHE || ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche || ID_Objet == Joueur.IdObjetTorche || ID_Objet == Joueur.IdObjetFenetreBois || ID_Objet == Joueur.IdObjetTableBoisDecorative || ID_Objet == Joueur.IdObjetTableArtisanaTier1)
		{
			degats *= 0.080f;
			capPourcent = 0.34f;
		}
		else if (ID_Objet == Joueur.IdObjetAllumeFeu)
		{
			degats *= 0.068f;
			capPourcent = 0.28f;
		}
		else if (EstMatiereSilexParIdObjet(ID_Objet))
		{
			degats *= 0.065f;
			capPourcent = 0.28f;
		}
		else
		{
			degats *= 0.10f;
			capPourcent = 0.36f;
		}
		float capParCoup = Mathf.Max(4f, ResistanceActuelle * capPourcent);
		degats = Mathf.Min(degats, capParCoup);
		ResistanceActuelle -= degats;
		if (ResistanceActuelle <= 0)
		{
			FracturerPublic(dirVue, pointImpact);
			return 2;
		}
		return 1;
	}

	// ----- MOTEUR DE FRACTURE (SurImpactPhysique → Fracturer → SpawnEclatVrai) -----
	/// <summary>Appelé à chaque contact physique. body peut être null (terrain PhysicsServer3D bas-niveau) → traité comme sol.</summary>
	private void SurImpactPhysique(Node body)
	{
		if (EstIdRocheMatiere(ID_Objet) && _frameFinGraceImpactLancer != 0 && Engine.GetPhysicsFrames() < _frameFinGraceImpactLancer)
			return;

		BoeufSauvage boeufTouche = ResoudreBoeufDepuisNoeud(body);
		if (boeufTouche != null)
		{
			float vitesseImpact = LinearVelocity.Length();
			float masseImpact = Mathf.Max(0.01f, Mass);
			float energieImpactCinetique = 0.5f * masseImpact * vitesseImpact * vitesseImpact;
			float impulsion = masseImpact * vitesseImpact;
			float energieImpact = (energieImpactCinetique * 0.44f + impulsion * 3.4f)
				* CoefficientMorphologieImpact()
				* CoefficientMateriauImpactFaune();

			bool tranchant = EstObjetTranchantPourImpactFaune();
			bool perforant = tranchant && vitesseImpact > 3.3f && EstObjetPointeBienAligneeVers(boeufTouche);
			string zone = DeterminerZoneImpactBovin(boeufTouche);

			bool applique = boeufTouche.RecevoirImpactCombat(
				energieImpact,
				GlobalPosition,
				LinearVelocity,
				tranchant,
				perforant,
				zone,
				(ulong)GetInstanceId());

			if (applique)
			{
				LinearVelocity *= 0.4f;
				AngularVelocity *= 0.35f;
				if (perforant && vitesseImpact > 4.9f)
					TenterPlanterDansBovin(boeufTouche);
			}
		}

		// Objets légers/petits: au contact joueur, on les réveille et on les repousse légèrement
		// pour éviter un blocage dur sur de petits objets au sol.
		if (body is CharacterBody3D personnage && EstObjetLegerEtPetitReactif())
		{
			if (Freeze) Freeze = false;
			if (Sleeping) Sleeping = false;
			Vector3 dirPoussee = GlobalPosition - personnage.GlobalPosition;
			dirPoussee.Y = 0f;
			if (dirPoussee.LengthSquared() < 0.0001f)
			{
				dirPoussee = LinearVelocity;
				dirPoussee.Y = 0f;
			}
			if (dirPoussee.LengthSquared() > 0.0001f)
				dirPoussee = dirPoussee.Normalized();
			else
				dirPoussee = Vector3.Forward;

			float impulsionHoriz = Mathf.Clamp(0.8f + Mass * 0.25f, 0.8f, 3.5f);
			ApplyCentralImpulse(dirPoussee * impulsionHoriz + Vector3.Up * 0.18f);
		}

		// 1. Détection du corps fantôme (terrain bas-niveau)
		bool frappeLeSol = (body == null);

		// 2. Calcul de l'énergie cinétique
		float velociteRelative = LinearVelocity.Length();
		if (!frappeLeSol && body is RigidBody3D rigidBody)
			velociteRelative += rigidBody.LinearVelocity.Length();

		float masseCourante = Mathf.Max(0.01f, Mass);
		float energieCinetique = 0.5f * masseCourante * velociteRelative * velociteRelative;
		// Roches : seuil haut + grâce au lancer — évite fracture « dans le vide » au départ.
		float seuilEnergie = EstIdRocheMatiere(ID_Objet) ? 85f : 8f;
		if (energieCinetique < seuilEnergie) return;

		// Choc contre un personnage (sortie de main / frottement) : pas de casse sauf très gros choc.
		if (EstIdRocheMatiere(ID_Objet) && body is CharacterBody3D && energieCinetique < 220f)
			return;

		// 3. Dureté adverse
		float dureteAdverse = 50f;
		if (frappeLeSol)
			dureteAdverse = 80f;
		else if (body is ItemPhysique autreRoche)
		{
			int idxAutre = Mathf.Clamp(autreRoche.IndexChimique, 0, TableGeologique.Length - 1);
			dureteAdverse = TableGeologique[idxAutre].ResistanceFuture;
		}

		// 4. Calcul des dégâts internes
		int idxMoi = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
		float maDurete = TableGeologique[idxMoi].ResistanceFuture;
		float degatsSubis = (Mathf.Sqrt(energieCinetique) * 9.5f * dureteAdverse) / Mathf.Max(1f, maDurete);
		if (EstIdRocheMatiere(ID_Objet))
		{
			degatsSubis *= Mathf.Clamp((energieCinetique - seuilEnergie) / 78f, 0.1f, 1.18f);
			if (body is CharacterBody3D)
				degatsSubis *= 0.12f;
			// Un seul contact ne peut pas vider toute la résistance (lancer violent sur sol dur).
			degatsSubis = Mathf.Min(degatsSubis, Mathf.Max(6f, ResistanceActuelle * 0.38f));
		}
		ResistanceActuelle -= degatsSubis;

		if (!frappeLeSol && EstMatiereSilexParIdObjet(ID_Objet) && dureteAdverse > 70f && energieCinetique > 30f)
			GenererParticulesEtincelle();

		// 5. La fracture : direction de la vélocité (choc réel) + centre du corps → plan de coupe stable (évite plan aléatoire sur roche plate).
		if (ResistanceActuelle <= 0)
		{
			Vector3 v = LinearVelocity;
			Vector3? dirChoc = v.LengthSquared() > 0.04f ? v.Normalized() : (Vector3?)null;
			Fracturer(dirChoc, GlobalPosition);
		}
	}

	private static BoeufSauvage ResoudreBoeufDepuisNoeud(Node body)
	{
		for (Node n = body; n != null; n = n.GetParent())
			if (n is BoeufSauvage b)
				return b;
		return null;
	}

	private bool EstObjetTranchantPourImpactFaune()
	{
		if (ID_Objet == 105 || ID_Objet == 106 || ID_Objet == Joueur.IdObjetHachePierreTier1 || ID_Objet == Joueur.IdObjetPiochePierreTier0 || ID_Objet == Joueur.IdObjetPellePierreTier0 || ID_Objet == Joueur.IdObjetLancePierreTier0 || ID_Objet == Joueur.IdObjetFauxPierreTier0 || ID_Objet == 100)
			return true;
		if (EstUnEclat)
			return true;
		return EstIdRocheMatiere(ID_Objet) && IndexCacheMemoire == 3;
	}

	private float CoefficientMorphologieImpact()
	{
		if (!EstIdRocheMatiere(ID_Objet))
			return 1f;
		int morph = Mathf.Clamp(IndexCacheMemoire, 0, 3);
		return morph switch
		{
			1 => 0.88f, // plate : pénètre moins en lancer
			2 => 1.02f, // ovale : compromis
			3 => 1.16f, // pointe : transfert plus agressif
			_ => 0.97f  // ronde
		};
	}

	private float CoefficientMateriauImpactFaune()
	{
		if (EstMatiereSilexParIdObjet(ID_Objet))
			return 1.16f;
		if (ID_Objet == Joueur.IdObjetLancePierreTier0)
			return 1.22f;
		if (ID_Objet == 106 || ID_Objet == Joueur.IdObjetHachePierreTier1 || ID_Objet == Joueur.IdObjetPiochePierreTier0)
			return 1.08f;
		if (ID_Objet == 105 || ID_Objet == Joueur.IdObjetPellePierreTier0 || ID_Objet == Joueur.IdObjetFauxPierreTier0)
			return 0.96f;
		if (EstIdRocheMatiere(ID_Objet))
			return Mathf.Lerp(0.72f, 1.06f, Mathf.Clamp(IndexTailleRoche / 4f, 0f, 1f));
		return 1f;
	}

	private bool EstObjetPointeBienAligneeVers(BoeufSauvage cible)
	{
		if (cible == null || !GodotObject.IsInstanceValid(cible) || LinearVelocity.LengthSquared() < 0.01f)
			return false;
		Vector3 versCible = (cible.GlobalPosition - GlobalPosition).Normalized();
		Vector3 dirVitesse = LinearVelocity.Normalized();
		Vector3 axePointeA = (-GlobalTransform.Basis.Z).Normalized();
		Vector3 axePointeB = GlobalTransform.Basis.Y.Normalized();
		float alignPointe = Mathf.Max(axePointeA.Dot(dirVitesse), axePointeB.Dot(dirVitesse));
		float trajectoireVersCible = dirVitesse.Dot(versCible);
		return alignPointe > 0.38f && trajectoireVersCible > 0.35f;
	}

	private string DeterminerZoneImpactBovin(BoeufSauvage boeuf)
	{
		if (boeuf == null || !GodotObject.IsInstanceValid(boeuf))
			return "";
		Vector3 local = boeuf.ToLocal(GlobalPosition);
		if (local.Y > 0.95f) return "CollisionShape3D_Tete";
		if (local.Y > 0.32f && local.Y < 0.85f) return "CollisionShape3D_Ventre";
		return "CollisionShape3D";
	}

	private void TenterPlanterDansBovin(BoeufSauvage boeuf)
	{
		if (boeuf == null || !GodotObject.IsInstanceValid(boeuf) || _bovinPlante != null)
			return;
		_bovinPlante = boeuf;
		Vector3 dir = LinearVelocity.LengthSquared() > 0.001f ? LinearVelocity.Normalized() : -boeuf.GlobalTransform.Basis.Z.Normalized();
		_offsetLocalDansBovinPlante = boeuf.ToLocal(GlobalPosition + dir * 0.08f);
		Freeze = true;
		FreezeMode = FreezeModeEnum.Static;
		Sleeping = true;
		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		CollisionLayer = 0u;
		CollisionMask = 0u;
	}

	/// <summary>Appelé depuis l'extérieur (ex: frappe du joueur) pour déclencher la fracture.</summary>
	public void FracturerPublic()
	{
		Fracturer(null, null);
	}

	/// <summary>Fracture la roche avec un plan de coupe aligné sur le regard du joueur (cassure nette, face plate vers le joueur).</summary>
	/// <param name="directionVueMonde">Direction du regard (du joueur vers le point d'impact), en espace monde. Si null, plan aléatoire (choc physique).</param>
	/// <param name="pointImpactMonde">Point d'impact du raycast en espace monde. Si null, le plan passe par le centre local.</param>
	public void FracturerPublic(Vector3? directionVueMonde, Vector3? pointImpactMonde)
	{
		Fracturer(directionVueMonde, pointImpactMonde);
	}

	private void GenererParticulesEtincelle()
	{
		// Placeholder : étincelles (GPUParticles3D ou effet visuel). À brancher sur un asset si besoin.
	}

	private void Fracturer(Vector3? directionVueMonde, Vector3? pointImpactMonde)
	{
		MeshInstance3D monVisuel = null;
		foreach (Node child in this.GetChildren())
		{
			if (child is MeshInstance3D mi) { monVisuel = mi; break; }
		}
		if (monVisuel == null || monVisuel.Mesh == null)
		{
			GD.PrintErr("FRACTURE ÉCHOUÉE : Aucun MeshInstance3D trouvé !");
			QueueFree();
			return;
		}
		Vector3 sm = monVisuel.Scale;
		if (sm.LengthSquared() < 1e-12f) sm = Vector3.One;
		Vector3 echelleTotaleMesh = new Vector3(
			Mathf.Abs(Scale.X * sm.X),
			Mathf.Abs(Scale.Y * sm.Y),
			Mathf.Abs(Scale.Z * sm.Z));
		// Au-delà de 5 fractures : poudre, disparition (plus de fragments)
		if (NiveauFracture > 5)
		{
			QueueFree();
			return;
		}

		// Roches matière : 2 morceaux = palier de taille inférieure, même morphologie (sphère procédurale) — plus de fragments déformés au contour.
		if (EstIdRocheMatiere(ID_Objet))
		{
			FracturerRocheMatiereParPalierTaille(directionVueMonde, pointImpactMonde);
			return;
		}

		Vector3 normaleCoupe;
		if (directionVueMonde.HasValue && directionVueMonde.Value.LengthSquared() > 0.01f)
		{
			Vector3 dirVueLocal = (GlobalTransform.Basis.Inverse() * directionVueMonde.Value).Normalized();
			// Anatomie de la roche : coupe selon l'endroit où on tape (plus au hasard).
			Aabb aabb = monVisuel.Mesh.GetAabb();
			Vector3 size = aabb.Size;
			float ex = size.X * echelleTotaleMesh.X; float ey = size.Y * echelleTotaleMesh.Y; float ez = size.Z * echelleTotaleMesh.Z;
			Vector3 axisMin = ex <= ey && ex <= ez ? Vector3.Right : (ey <= ex && ey <= ez ? Vector3.Up : Vector3.Back);
			Vector3 axisMax = ex >= ey && ex >= ez ? Vector3.Right : (ey >= ex && ey >= ez ? Vector3.Up : Vector3.Back);
			float dotMin = Mathf.Abs(dirVueLocal.Dot(axisMin));
			float dotMax = Mathf.Abs(dirVueLocal.Dot(axisMax));
			// Tape sur l'épaisseur (face plate) → 2 morceaux plus petits, plus épais, ronds. Tape sur le côté mince → 2 morceaux plus minces.
			normaleCoupe = (dotMin >= dotMax) ? axisMin : axisMax;
		}
		else
			normaleCoupe = new Vector3((float)GD.Randf() - 0.5f, (float)GD.Randf() - 0.5f, (float)GD.Randf() - 0.5f).Normalized();

		Vector3 pointSurLePlanLocal = pointImpactMonde.HasValue ? GlobalTransform.AffineInverse() * pointImpactMonde.Value : Vector3.Zero;
		Plane planCoupe = new Plane(normaleCoupe, -normaleCoupe.Dot(pointSurLePlanLocal));

		Vector3 impactPos = pointImpactMonde.HasValue ? pointImpactMonde.Value : (GlobalPosition + Vector3.Up * 0.1f);
		Vector3 normalMonde = GlobalTransform.Basis * normaleCoupe;
		float masseFragment = Mass * 0.5f;

		// Priorité : morceaux préfabriqués variés + triplanar (géométrie propre, plus de pointes/noir)
		if (SpawnChunksPrefabriques(impactPos, normalMonde, masseFragment))
		{
			if (!EstMatiereSilexParIdObjet(ID_Objet) && _surImpactConnecte) { BodyEntered -= SurImpactPhysique; _surImpactConnecte = false; }
			QueueFree(); // LA MÈRE EST DÉTRUITE ICI. AUCUNE EXCEPTION.
			return;
		}

		// Fallback : découpe de la roche exacte au plan
		Material matRoche = monVisuel.MaterialOverride ?? (monVisuel.Mesh.GetSurfaceCount() > 0 ? monVisuel.Mesh.SurfaceGetMaterial(0) : null);
		if (matRoche != null && monVisuel.Mesh is ArrayMesh arrMesh && DecouperMeshEtSpawnerMoities(arrMesh, planCoupe, impactPos, normalMonde, masseFragment, matRoche, echelleTotaleMesh))
		{
			if (!EstMatiereSilexParIdObjet(ID_Objet) && _surImpactConnecte) { BodyEntered -= SurImpactPhysique; _surImpactConnecte = false; }
			QueueFree(); // LA MÈRE EST DÉTRUITE ICI. AUCUNE EXCEPTION.
			return;
		}

		// Fallback : méthode par contour (si découpe mesh échoue). Cuire le scale dans les sommets pour éviter accordéon UV.
		Vector3[] sommetsActuels = monVisuel.Mesh.GetFaces();
		if (sommetsActuels == null || sommetsActuels.Length == 0) { QueueFree(); return; }

		// --- LE BAKE SCALE (CUISSON DE L'ADN) ---
		// Écrase les atomes selon l'échelle corps × mesh (morphologie sur le mesh pour Jolt), pour que l'éclat naisse pur (Scale 1,1,1).
		Vector3 echelleMere = echelleTotaleMesh;
		for (int i = 0; i < sommetsActuels.Length; i++)
		{
			sommetsActuels[i] = new Vector3(
				sommetsActuels[i].X * echelleMere.X,
				sommetsActuels[i].Y * echelleMere.Y,
				sommetsActuels[i].Z * echelleMere.Z
			);
		}
		// ----------------------------------------

		var ptsA = new List<Vector3>();
		var ptsB = new List<Vector3>();
		foreach (Vector3 pt in sommetsActuels)
		{
			float dist = planCoupe.DistanceTo(pt);
			if (dist > 0) ptsA.Add(pt); else ptsB.Add(pt);
			if (Mathf.Abs(dist) < 0.1f) { Vector3 proj = planCoupe.Project(pt); ptsA.Add(proj); ptsB.Add(proj); }
		}
		if (ptsA.Count > MaxPointsContourFragment) ReduirePointsContour(ptsA, MaxPointsContourFragment);
		if (ptsB.Count > MaxPointsContourFragment) ReduirePointsContour(ptsB, MaxPointsContourFragment);
		Vector3[] facesA = ptsA.Count >= 4 ? OrdonnerPointsDansPlan(ptsA, planCoupe) : PointsFallbackFragment(planCoupe, 1);
		Vector3[] facesB = ptsB.Count >= 4 ? OrdonnerPointsDansPlan(ptsB, planCoupe) : PointsFallbackFragment(planCoupe, -1);
		SpawnEclatVrai(facesA, masseFragment, impactPos + (normalMonde * 0.03f), normaleCoupe);
		SpawnEclatVrai(facesB, masseFragment, impactPos - (normalMonde * 0.03f), -normaleCoupe);
		if (!EstMatiereSilexParIdObjet(ID_Objet) && _surImpactConnecte) { BodyEntered -= SurImpactPhysique; _surImpactConnecte = false; }
		QueueFree(); // LA MÈRE EST DÉTRUITE ICI. AUCUNE EXCEPTION. AUCUN RECYCLAGE.
	}

	/// <summary>Fragment procédural après fracture : même ID matière, morpho conservée, taille au palier inférieur.</summary>
	public static ItemPhysique CreerFragmentRocheMatierePourPalier(int idObjet, int indexCacheMemoire, int indexTailleRoche, int niveauFracture, float masseCible)
	{
		var item = new ItemPhysique
		{
			ID_Objet = idObjet,
			IndexCacheMemoire = Mathf.Clamp(indexCacheMemoire, 0, 3),
			IndexTailleRoche = Mathf.Clamp(indexTailleRoche, 0, 4),
			NiveauFracture = niveauFracture,
			Name = "ItemPhysique",
			Scale = Vector3.One,
			EstUnEclat = false,
			EstEclatFracture = false,
			ContinuousCd = true
		};
		item.AddChild(new MeshInstance3D());
		item.AddChild(new CollisionShape3D());
		item.ReappliquerApparence();
		float masseRef = item.Mass;
		item.Mass = Mathf.Max(0.02f, masseCible);
		if (masseRef > 1e-6f)
			item.ResistanceActuelle *= item.Mass / masseRef;
		if (!EstMatiereSilexParIdObjet(idObjet))
		{
			item.ContactMonitor = true;
			item.MaxContactsReported = 1;
			item.BodyEntered += item.SurImpactPhysique;
			item._surImpactConnecte = true;
		}
		return item;
	}

	private void FracturerRocheMatiereParPalierTaille(Vector3? directionVueMonde, Vector3? pointImpactMonde)
	{
		if (_surImpactConnecte) { BodyEntered -= SurImpactPhysique; _surImpactConnecte = false; }
		int nouvelleTaille = Mathf.Max(0, IndexTailleRoche - 1);
		Vector3 n = directionVueMonde.HasValue && directionVueMonde.Value.LengthSquared() > 0.01f
			? directionVueMonde.Value.Normalized()
			: new Vector3((float)GD.Randf() - 0.5f, (float)GD.Randf() - 0.5f, (float)GD.Randf() - 0.5f).Normalized();
		float masseDemi = Mass * 0.5f;
		int nv = NiveauFracture + 1;
		Node p = GetParent();
		if (p == null) { QueueFree(); return; }
		Vector3 centre = GlobalPosition;
		for (int i = 0; i < 2; i++)
		{
			ItemPhysique frag = CreerFragmentRocheMatierePourPalier(ID_Objet, IndexCacheMemoire, nouvelleTaille, nv, masseDemi);
			frag.Name = "ItemPhysique";
			frag.AddToGroup("BlocsPoses");
			frag.SetMeta("ID_Matiere", frag.ID_Objet);
			Vector3 pos = centre + n * (i == 0 ? 0.05f : -0.05f) + Vector3.Up * 0.04f;
			frag.SetMeta("spawn_pos", pos);
			Vector3 imp = (n * (i == 0 ? 0.5f : -0.5f) + Vector3.Up * 0.4f + new Vector3((float)GD.Randf() - 0.5f, 0.1f, (float)GD.Randf() - 0.5f) * 0.25f).Normalized() * 0.9f;
			frag.SetMeta("spawn_impulse", imp);
			frag.TreeEntered += () => AppliquerSpawnEclat(frag);
			p.AddChild(frag);
		}
		QueueFree();
	}

	/// <summary>Spawn 2 morceaux préfabriqués depuis le cache (formes variées) + triplanar. Géométrie propre, plus de pointes ni faces noires. Retourne true si succès.</summary>
	private bool SpawnChunksPrefabriques(Vector3 impactPos, Vector3 normalMonde, float masseFragment)
	{
		// Roches matière (ID <see cref="IdRocheMatiereMin"/>–<see cref="IdRocheMatiereMax"/>) : pas de morceaux génériques du cache — coupe réelle / contour pour garder forme + échelle ADN.
		if (EstIdRocheMatiere(ID_Objet))
			return false;
		bool estSilex = EstMatiereSilexParIdObjet(ID_Objet);
		var cacheMesh = estSilex ? _cacheMeshSilex : _cacheMeshCaillou;
		var cacheCollision = estSilex ? _cacheCollisionSilex : _cacheCollisionCaillou;
		lock (cacheMesh)
		{
			if (cacheMesh.Count < 4) return false; // besoin d'au moins 2 formes "cassées" (2e moitié)
			int idxA = PreparerCacheEtTirerIndex(estSilex, true);
			int idxB = PreparerCacheEtTirerIndex(estSilex, true);
			if (idxA == idxB && cacheMesh.Count > 1) idxB = (idxA + 1) % cacheMesh.Count;
			Mesh meshA = cacheMesh[idxA];
			Mesh meshB = cacheMesh[idxB];
			Shape3D shapeA = idxA < cacheCollision.Count ? cacheCollision[idxA] : null;
			Shape3D shapeB = idxB < cacheCollision.Count ? cacheCollision[idxB] : null;
			Vector3 scaleFragment = new Vector3(0.65f, 0.65f, 0.65f); // fragments plus petits
			for (int i = 0; i < 2; i++)
			{
				Mesh m = (i == 0) ? meshA : meshB;
				Shape3D s = (i == 0) ? shapeA : shapeB;
				ItemPhysique frag = new ItemPhysique();
				frag.EstUnEclat = true;
				frag.EstEclatFracture = true;
				frag.NiveauFracture = NiveauFracture + 1;
				frag.ID_Objet = ID_Objet;
				frag.IndexChimique = IndexChimique;
				frag.IndexTailleRoche = IndexTailleRoche;
				frag.IndexCacheMemoire = Mathf.Clamp(IndexCacheMemoire, 0, 3);
				frag.Mass = masseFragment;
				int idxCh = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
				frag.ResistanceActuelle = TableGeologique[idxCh].ResistanceFuture * (masseFragment / 50f);
				frag.ContinuousCd = true;
				frag.Scale = scaleFragment;
				var visuel = new MeshInstance3D { Mesh = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
				StandardMaterial3D mat = (StandardMaterial3D)CreerMaterielProcedural(estSilex, IndexChimique, pourEclat: false).Duplicate(true);
				mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				mat.Roughness = 0.95f;
				mat.NormalEnabled = false;
				mat.Uv1Triplanar = true;
				mat.Uv1WorldTriplanar = true;
				mat.Uv1Scale = new Vector3(1.2f, 1.2f, 1.2f);
				visuel.MaterialOverride = mat;
				frag.AddChild(visuel);
				frag.AddChild(new CollisionShape3D { Shape = s ?? new BoxShape3D { Size = new Vector3(0.12f, 0.12f, 0.12f) } });
				if (!EstMatiereSilexParIdObjet(frag.ID_Objet)) { frag.ContactMonitor = true; frag.MaxContactsReported = 1; frag.BodyEntered += frag.SurImpactPhysique; frag._surImpactConnecte = true; }
				frag.Name = "ItemPhysique";
				frag.AddToGroup("BlocsPoses");
				frag.SetMeta("ID_Matiere", frag.ID_Objet);
				Vector3 pos = impactPos + (i == 0 ? 1 : -1) * normalMonde * 0.03f;
				frag.SetMeta("spawn_pos", pos);
				frag.SetMeta("spawn_impulse", (normalMonde * (i == 0 ? 0.6f : -0.6f) + new Vector3((float)GD.Randf() - 0.5f, 0.4f, (float)GD.Randf() - 0.5f)).Normalized() * 0.8f);
				frag.TreeEntered += () => AppliquerSpawnEclat(frag);
				GetParent().AddChild(frag);
			}
		}
		return true;
	}

	/// <summary>Découpe le mesh de la roche au plan et crée 2 moitiés (même texture, modèles temporaires). echelleMere = cuisson du scale dans les sommets (évite accordéon).</summary>
	private bool DecouperMeshEtSpawnerMoities(ArrayMesh mesh, Plane plan, Vector3 impactPos, Vector3 normalMonde, float masseFragment, Material matRoche, Vector3 echelleMere)
	{
		if (mesh == null || mesh.GetSurfaceCount() == 0) return false;
		var mdt = new MeshDataTool();
		if (mdt.CreateFromSurface(mesh, 0) != Error.Ok) return false;

		var trisA = new List<(Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 na, Vector3 nb, Vector3 nc)>();
		var trisB = new List<(Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 na, Vector3 nb, Vector3 nc)>();
		var capA = new List<Vector3>();
		var capB = new List<Vector3>();

		for (int f = 0; f < mdt.GetFaceCount(); f++)
		{
			int i0 = mdt.GetFaceVertex(f, 0), i1 = mdt.GetFaceVertex(f, 1), i2 = mdt.GetFaceVertex(f, 2);
			Vector3 v0 = mdt.GetVertex(i0), v1 = mdt.GetVertex(i1), v2 = mdt.GetVertex(i2);
			Vector2 uv0 = mdt.GetVertexUV(i0), uv1 = mdt.GetVertexUV(i1), uv2 = mdt.GetVertexUV(i2);
			Vector3 n0 = mdt.GetVertexNormal(i0), n1 = mdt.GetVertexNormal(i1), n2 = mdt.GetVertexNormal(i2);
			DecouperTriangle(plan, v0, v1, v2, uv0, uv1, uv2, n0, n1, n2, trisA, trisB, capA, capB);
		}

		if (trisA.Count == 0 || trisB.Count == 0) return false;

		// Cuire le scale dans les sommets après la coupe (plan en local, résultats mis à l'échelle)
		AppliquerScaleAuxTriangles(trisA, capA, echelleMere);
		AppliquerScaleAuxTriangles(trisB, capB, echelleMere);

		// Normale du cap : doit pointer vers l'extérieur de chaque moitié pour que la face de coupe soit visible (éviter backface culling = transparence)
		ArrayMesh meshA = ConstruireMeshMoitie(trisA, capA, plan, -1f);  // moitié côté + du plan → cap visible depuis côté -
		ArrayMesh meshB = ConstruireMeshMoitie(trisB, capB, plan, 1f);   // moitié côté - du plan → cap visible depuis côté +
		if (meshA.GetFaces().Length == 0 || meshB.GetFaces().Length == 0) return false;

		// Un seul matériau global par fragment (MaterialOverride dans SpawnMoitieRoche) — pas de SurfaceSetMaterial pour compat inventaire / procédural.
		SpawnMoitieRoche(meshA, impactPos + normalMonde * 0.02f, normalMonde, masseFragment);
		SpawnMoitieRoche(meshB, impactPos - normalMonde * 0.02f, -normalMonde, masseFragment);
		return true;
	}

	private static void AppliquerScaleAuxTriangles(
		List<(Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 na, Vector3 nb, Vector3 nc)> tris,
		List<Vector3> cap, Vector3 echelle)
	{
		for (int i = 0; i < tris.Count; i++)
		{
			var t = tris[i];
			tris[i] = (
				new Vector3(t.a.X * echelle.X, t.a.Y * echelle.Y, t.a.Z * echelle.Z),
				new Vector3(t.b.X * echelle.X, t.b.Y * echelle.Y, t.b.Z * echelle.Z),
				new Vector3(t.c.X * echelle.X, t.c.Y * echelle.Y, t.c.Z * echelle.Z),
				t.uva, t.uvb, t.uvc, t.na, t.nb, t.nc
			);
		}
		for (int i = 0; i < cap.Count; i++)
			cap[i] = new Vector3(cap[i].X * echelle.X, cap[i].Y * echelle.Y, cap[i].Z * echelle.Z);
	}

	private static void DecouperTriangle(Plane plan, Vector3 v0, Vector3 v1, Vector3 v2, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector3 n0, Vector3 n1, Vector3 n2,
		List<(Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector3, Vector3, Vector3)> trisA,
		List<(Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector3, Vector3, Vector3)> trisB,
		List<Vector3> capA, List<Vector3> capB)
	{
		float d0 = plan.DistanceTo(v0), d1 = plan.DistanceTo(v1), d2 = plan.DistanceTo(v2);
		const float eps = 0.0001f;
		if (d0 >= -eps && d1 >= -eps && d2 >= -eps) { trisA.Add((v0, v1, v2, uv0, uv1, uv2, n0, n1, n2)); return; }
		if (d0 <= eps && d1 <= eps && d2 <= eps) { trisB.Add((v0, v1, v2, uv0, uv1, uv2, n0, n1, n2)); return; }
		float t02 = Mathf.Abs(d0 - d2) < eps ? 0.5f : d0 / (d0 - d2);
		float t12 = Mathf.Abs(d1 - d2) < eps ? 0.5f : d1 / (d1 - d2);
		float t01 = Mathf.Abs(d0 - d1) < eps ? 0.5f : d0 / (d0 - d1);
		Vector3 p01 = v0 + t01 * (v1 - v0); Vector2 uv01 = uv0 + t01 * (uv1 - uv0); Vector3 n01 = (n0 + t01 * (n1 - n0)).Normalized();
		Vector3 p02 = v0 + t02 * (v2 - v0); Vector2 uv02 = uv0 + t02 * (uv2 - uv0); Vector3 n02 = (n0 + t02 * (n2 - n0)).Normalized();
		Vector3 p12 = v1 + t12 * (v2 - v1); Vector2 uv12 = uv1 + t12 * (uv2 - uv1); Vector3 n12 = (n1 + t12 * (n2 - n1)).Normalized();
		// v0,v1 côté A, v2 côté B → intersections 0-2 et 1-2
		if (d0 >= -eps && d1 >= -eps)
		{
			trisA.Add((v0, v1, p12, uv0, uv1, uv12, n0, n1, n12)); trisA.Add((v0, p12, p02, uv0, uv12, uv02, n0, n12, n02));
			trisB.Add((v2, p02, p12, uv2, uv02, uv12, n2, n02, n12));
			capA.Add(p02); capA.Add(p12); capB.Add(p02); capB.Add(p12);
		}
		// v0,v2 côté A, v1 côté B → intersections 0-1 et 1-2
		else if (d0 >= -eps && d2 >= -eps)
		{
			trisA.Add((v0, p02, p01, uv0, uv02, uv01, n0, n02, n01)); trisA.Add((v0, p01, v2, uv0, uv01, uv2, n0, n01, n2));
			trisB.Add((v1, p12, p01, uv1, uv12, uv01, n1, n12, n01));
			capA.Add(p01); capA.Add(p02); capB.Add(p01); capB.Add(p12);
		}
		// v1,v2 côté A, v0 côté B → intersections 0-1 et 0-2
		else if (d1 >= -eps && d2 >= -eps)
		{
			trisA.Add((v1, p12, p01, uv1, uv12, uv01, n1, n12, n01)); trisA.Add((v1, p01, v2, uv1, uv01, uv2, n1, n01, n2));
			trisB.Add((v0, p02, p01, uv0, uv02, uv01, n0, n02, n01));
			capA.Add(p01); capA.Add(p12); capB.Add(p01); capB.Add(p02);
		}
		// v0,v1 côté B, v2 côté A
		else if (d0 <= eps && d1 <= eps)
		{
			trisB.Add((v0, v1, p12, uv0, uv1, uv12, n0, n1, n12)); trisB.Add((v0, p12, p02, uv0, uv12, uv02, n0, n12, n02));
			trisA.Add((v2, p02, p12, uv2, uv02, uv12, n2, n02, n12));
			capB.Add(p02); capB.Add(p12); capA.Add(p02); capA.Add(p12);
		}
		// v0,v2 côté B, v1 côté A
		else if (d0 <= eps && d2 <= eps)
		{
			trisB.Add((v0, p02, p01, uv0, uv02, uv01, n0, n02, n01)); trisB.Add((v0, p01, v2, uv0, uv01, uv2, n0, n01, n2));
			trisA.Add((v1, p12, p01, uv1, uv12, uv01, n1, n12, n01));
			capB.Add(p01); capB.Add(p02); capA.Add(p01); capA.Add(p12);
		}
		// v1,v2 côté B, v0 côté A
		else
		{
			trisB.Add((v1, p12, p01, uv1, uv12, uv01, n1, n12, n01)); trisB.Add((v1, p01, v2, uv1, uv01, uv2, n1, n01, n2));
			trisA.Add((v0, p02, p01, uv0, uv02, uv01, n0, n02, n01));
			capB.Add(p01); capB.Add(p12); capA.Add(p01); capA.Add(p02);
		}
	}

	/// <summary>Adoucit le contour du cap vers un polygone régulier (enlève les coins pointus, garde plan et angles droits).</summary>
	private static Vector3[] AdoucirContourCap(Vector3[] cap, Vector3 centrePlan, Vector3 u, Vector3 v, float forceAdoucissement)
	{
		if (cap == null || cap.Length < 4 || forceAdoucissement <= 0f) return cap;
		float rayonMoyen = 0f;
		foreach (Vector3 p in cap) rayonMoyen += (p - centrePlan).Length();
		rayonMoyen /= cap.Length;
		if (rayonMoyen < 0.001f) return cap;
		var result = new Vector3[cap.Length];
		for (int i = 0; i < cap.Length; i++)
		{
			Vector3 d = cap[i] - centrePlan;
			float angle = Mathf.Atan2(d.Dot(v), d.Dot(u));
			// Sommet correspondant du N-gone régulier
			float rRegulier = rayonMoyen;
			Vector3 ptRegulier = centrePlan + (float)Mathf.Cos(angle) * rRegulier * u + (float)Mathf.Sin(angle) * rRegulier * v;
			result[i] = cap[i].Lerp(ptRegulier, forceAdoucissement);
		}
		return result;
	}

	/// <summary>Subdivision récursive pour triangles allongés de la peau (évite étirement texture). a,b,c déjà en espace adouci si applicable.</summary>
	private static void SubdivTriPeau(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 na, Vector3 nb, Vector3 nc, float ratioMax, Vector3[] bordOriginal = null, Vector3[] bordAdouci = null)
	{
		const float eps = 0.0005f;
		float lab = a.DistanceTo(b), lbc = b.DistanceTo(c), lca = c.DistanceTo(a);
		float longest = Mathf.Max(lab, Mathf.Max(lbc, lca));
		float shortest = Mathf.Min(lab, Mathf.Min(lbc, lca));
		if (shortest < eps || longest / shortest <= ratioMax)
		{
			if (a.DistanceSquaredTo(b) >= 0.000001f && b.DistanceSquaredTo(c) >= 0.000001f && c.DistanceSquaredTo(a) >= 0.000001f)
			{
				st.SetNormal(na); st.SetUV(uva); st.AddVertex(a);
				st.SetNormal(nb); st.SetUV(uvb); st.AddVertex(b);
				st.SetNormal(nc); st.SetUV(uvc); st.AddVertex(c);
			}
			return;
		}
		Vector3 m; Vector2 uvm; Vector3 nm;
		if (longest == lab) { m = (a + b) * 0.5f; uvm = (uva + uvb) * 0.5f; nm = (na + nb).Normalized(); SubdivTriPeau(st, a, m, c, uva, uvm, uvc, na, nm, nc, ratioMax, bordOriginal, bordAdouci); SubdivTriPeau(st, m, b, c, uvm, uvb, uvc, nm, nb, nc, ratioMax, bordOriginal, bordAdouci); }
		else if (longest == lbc) { m = (b + c) * 0.5f; uvm = (uvb + uvc) * 0.5f; nm = (nb + nc).Normalized(); SubdivTriPeau(st, a, b, m, uva, uvb, uvm, na, nb, nm, ratioMax, bordOriginal, bordAdouci); SubdivTriPeau(st, a, m, c, uva, uvm, uvc, na, nm, nc, ratioMax, bordOriginal, bordAdouci); }
		else { m = (c + a) * 0.5f; uvm = (uvc + uva) * 0.5f; nm = (nc + na).Normalized(); SubdivTriPeau(st, a, b, m, uva, uvb, uvm, na, nb, nm, ratioMax, bordOriginal, bordAdouci); SubdivTriPeau(st, m, b, c, uvm, uvb, uvc, nm, nb, nc, ratioMax, bordOriginal, bordAdouci); }
	}

	/// <summary>Coque convexe 2D (Graham scan). Retourne les indices des points sur la coque.</summary>
	private static List<int> ConvexHull2D(Vector2[] points)
	{
		if (points == null || points.Length < 3) return null;
		int n = points.Length;
		int leftMost = 0;
		for (int i = 1; i < n; i++)
			if (points[i].X < points[leftMost].X || (Mathf.Abs(points[i].X - points[leftMost].X) < 0.0001f && points[i].Y < points[leftMost].Y))
				leftMost = i;
		var hull = new List<int>();
		int p = leftMost, q;
		do
		{
			hull.Add(p);
			q = (p + 1) % n;
			for (int i = 0; i < n; i++)
			{
				float cross = (points[q].X - points[p].X) * (points[i].Y - points[p].Y) - (points[q].Y - points[p].Y) * (points[i].X - points[p].X);
				if (cross < -0.0001f) q = i;
				else if (Mathf.Abs(cross) < 0.0001f && (points[i] - points[p]).LengthSquared() > (points[q] - points[p]).LengthSquared()) q = i;
			}
			p = q;
		} while (p != leftMost && hull.Count < n);
		return hull.Count >= 3 ? hull : null;
	}

	/// <summary>Ajoute un triangle au cap (évite dégénérés).</summary>
	private static void AjouterTriCap(SurfaceTool st, Vector3 nCap, Vector3[] cap, Func<Vector3, Vector2> UVCap, int i, int j, int k, float eps)
	{
		Vector3 pa = cap[i], pb = cap[j], pc = cap[k];
		if (pa.DistanceSquaredTo(pb) < eps * eps || pb.DistanceSquaredTo(pc) < eps * eps || pc.DistanceSquaredTo(pa) < eps * eps) return;
		float longest = Mathf.Max(pa.DistanceTo(pb), Mathf.Max(pb.DistanceTo(pc), pc.DistanceTo(pa)));
		float shortest = Mathf.Min(pa.DistanceTo(pb), Mathf.Min(pb.DistanceTo(pc), pc.DistanceTo(pa)));
		if (shortest < eps || longest / shortest > 2.5f) return;
		st.SetNormal(nCap); st.SetUV(UVCap(pa)); st.AddVertex(pa);
		st.SetNormal(nCap); st.SetUV(UVCap(pb)); st.AddVertex(pb);
		st.SetNormal(nCap); st.SetUV(UVCap(pc)); st.AddVertex(pc);
	}

	/// <summary>Remplace un sommet par sa version adoucie/coque si c'est un point du bord (cap). Snap au plus proche si coque convexe.</summary>
	private static Vector3 RemplacerSiBord(Vector3 p, Vector3[] bordOriginal, Vector3[] bordAdouci, float eps = 0.001f)
	{
		int best = -1;
		float bestD = float.MaxValue;
		for (int i = 0; i < bordOriginal.Length; i++)
		{
			float d = p.DistanceSquaredTo(bordOriginal[i]);
			if (d < bestD) { bestD = d; best = i; }
		}
		if (best >= 0 && bestD < 0.00025f) return bordAdouci[best]; // snap bord (trop large → dégénérés, trop strict → trous)
		return p;
	}

	/// <summary>Deux surfaces : peau externe (tris) + cassure (cap). UV orthogonales sur le cap + GenerateTangents sur chaque surface pour corriger l'espace tangent (plus d'étirement en étoile).</summary>
	private static ArrayMesh ConstruireMeshMoitie(
		List<(Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 na, Vector3 nb, Vector3 nc)> tris,
		List<Vector3> cap, Plane plan, float signeNormaleCap)
	{
		var mesh = new ArrayMesh();
		var st = new SurfaceTool();

		// Précalcul du cap adouci (peau et cap partagent le même bord)
		Vector3[] bordOriginal = null, bordAdouci = null;
		if (cap.Count >= 3)
		{
			const float eps = 0.0005f;
			var capDedupe = new List<Vector3>();
			foreach (Vector3 p in cap)
			{
				bool tropProche = false;
				foreach (Vector3 q in capDedupe)
					if (p.DistanceSquaredTo(q) < eps * eps) { tropProche = true; break; }
				if (!tropProche) capDedupe.Add(p);
			}
			if (capDedupe.Count >= 3)
			{
				Vector3 centrePlan = -plan.D * plan.Normal;
				Vector3 u = plan.Normal.Cross(Vector3.Up).Normalized();
				if (u.LengthSquared() < 0.01f) u = plan.Normal.Cross(Vector3.Right).Normalized();
				Vector3 v = plan.Normal.Cross(u).Normalized();
				Vector2 centre2D = Vector2.Zero;
				foreach (Vector3 p in capDedupe) { Vector3 d = p - centrePlan; centre2D += new Vector2(d.Dot(u), d.Dot(v)); }
				centre2D /= capDedupe.Count;
				var ordre = new List<int>();
				for (int i = 0; i < capDedupe.Count; i++) ordre.Add(i);
				ordre.Sort((i, j) => {
					Vector3 di = capDedupe[i] - centrePlan, dj = capDedupe[j] - centrePlan;
					return Mathf.Atan2(di.Dot(v) - centre2D.Y, di.Dot(u) - centre2D.X).CompareTo(Mathf.Atan2(dj.Dot(v) - centre2D.Y, dj.Dot(u) - centre2D.X));
				});
				bordOriginal = new Vector3[capDedupe.Count];
				for (int i = 0; i < capDedupe.Count; i++) bordOriginal[i] = capDedupe[ordre[i]];
				bordAdouci = AdoucirContourCap(bordOriginal, centrePlan, u, v, 0.4f);
				// TOUJOURS coque convexe (4+ pts) → forme propre, plus de pointes/noires
				if (bordAdouci.Length >= 4)
				{
					var pts2 = new Vector2[bordAdouci.Length];
					for (int i = 0; i < bordAdouci.Length; i++) pts2[i] = new Vector2((bordAdouci[i] - centrePlan).Dot(u), (bordAdouci[i] - centrePlan).Dot(v));
					var hullIdx = ConvexHull2D(pts2);
					if (hullIdx != null && hullIdx.Count >= 3)
					{
						var hull3D = new Vector3[hullIdx.Count];
						for (int hi = 0; hi < hullIdx.Count; hi++) hull3D[hi] = bordAdouci[hullIdx[hi]];
						bordOriginal = hull3D;
						bordAdouci = hull3D;
					}
				}
			}
		}

		// 1. PEAU EXTERNE — subdivision agressive + sommets du bord adoucis (enlève les coins pointus)
		const float ratioMaxPeau = 2.2f;
		st.Begin(Mesh.PrimitiveType.Triangles);
		foreach (var t in tris)
		{
			Vector3 a = bordAdouci != null ? RemplacerSiBord(t.a, bordOriginal, bordAdouci) : t.a;
			Vector3 b = bordAdouci != null ? RemplacerSiBord(t.b, bordOriginal, bordAdouci) : t.b;
			Vector3 c = bordAdouci != null ? RemplacerSiBord(t.c, bordOriginal, bordAdouci) : t.c;
			float lab = a.DistanceTo(b), lbc = b.DistanceTo(c), lca = c.DistanceTo(a);
			float longest = Mathf.Max(lab, Mathf.Max(lbc, lca));
			float shortest = Mathf.Min(lab, Mathf.Min(lbc, lca));
			if (shortest < 0.0005f || longest / shortest <= ratioMaxPeau)
			{
				if (a.DistanceSquaredTo(b) >= 0.000001f && b.DistanceSquaredTo(c) >= 0.000001f && c.DistanceSquaredTo(a) >= 0.000001f)
				{
					st.SetNormal(t.na); st.SetUV(t.uva); st.AddVertex(a);
					st.SetNormal(t.nb); st.SetUV(t.uvb); st.AddVertex(b);
					st.SetNormal(t.nc); st.SetUV(t.uvc); st.AddVertex(c);
				}
			}
			else
			{
				Vector3 m; Vector2 uvm; Vector3 nm;
				if (longest == lab) { m = (a + b) * 0.5f; uvm = (t.uva + t.uvb) * 0.5f; nm = (t.na + t.nb).Normalized(); SubdivTriPeau(st, a, m, c, t.uva, uvm, t.uvc, t.na, nm, t.nc, ratioMaxPeau, bordOriginal, bordAdouci); SubdivTriPeau(st, m, b, c, uvm, t.uvb, t.uvc, nm, t.nb, t.nc, ratioMaxPeau, bordOriginal, bordAdouci); }
				else if (longest == lbc) { m = (b + c) * 0.5f; uvm = (t.uvb + t.uvc) * 0.5f; nm = (t.nb + t.nc).Normalized(); SubdivTriPeau(st, a, b, m, t.uva, t.uvb, uvm, t.na, t.nb, nm, ratioMaxPeau, bordOriginal, bordAdouci); SubdivTriPeau(st, a, m, c, t.uva, uvm, t.uvc, t.na, nm, t.nc, ratioMaxPeau, bordOriginal, bordAdouci); }
				else { m = (c + a) * 0.5f; uvm = (t.uvc + t.uva) * 0.5f; nm = (t.nc + t.na).Normalized(); SubdivTriPeau(st, a, b, m, t.uva, t.uvb, uvm, t.na, t.nb, nm, ratioMaxPeau, bordOriginal, bordAdouci); SubdivTriPeau(st, m, b, c, uvm, t.uvb, t.uvc, nm, t.nb, t.nc, ratioMaxPeau, bordOriginal, bordAdouci); }
			}
		}
		st.GenerateTangents();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, st.CommitToArrays());

		// 2. CASSURE (CAP) — utilise le bord adouci précalculé (ou recalcule si pas de précalc)
		if (cap.Count >= 3)
		{
			const float eps = 0.0005f;
			Vector3[] capOrdre;
			Vector3 centrePlan = -plan.D * plan.Normal;
			Vector3 u = plan.Normal.Cross(Vector3.Up).Normalized();
			if (u.LengthSquared() < 0.01f) u = plan.Normal.Cross(Vector3.Right).Normalized();
			Vector3 v = plan.Normal.Cross(u).Normalized();
			if (bordAdouci != null && bordAdouci.Length >= 3)
				capOrdre = bordAdouci;
			else
			{
				var capDedupe = new List<Vector3>();
				foreach (Vector3 p in cap)
				{
					bool tropProche = false;
					foreach (Vector3 q in capDedupe)
						if (p.DistanceSquaredTo(q) < eps * eps) { tropProche = true; break; }
					if (!tropProche) capDedupe.Add(p);
				}
				if (capDedupe.Count < 3) capDedupe = new List<Vector3>(cap);
				Vector2 centre2D = Vector2.Zero;
				foreach (Vector3 p in capDedupe) { Vector3 d = p - centrePlan; centre2D += new Vector2(d.Dot(u), d.Dot(v)); }
				centre2D /= capDedupe.Count;
				var ordre = new List<int>();
				for (int i = 0; i < capDedupe.Count; i++) ordre.Add(i);
				ordre.Sort((i, j) => {
					Vector3 di = capDedupe[i] - centrePlan, dj = capDedupe[j] - centrePlan;
					return Mathf.Atan2(di.Dot(v) - centre2D.Y, di.Dot(u) - centre2D.X).CompareTo(Mathf.Atan2(dj.Dot(v) - centre2D.Y, dj.Dot(u) - centre2D.X));
				});
				capOrdre = new Vector3[capDedupe.Count];
				for (int i = 0; i < capDedupe.Count; i++) capOrdre[i] = capDedupe[ordre[i]];
				capOrdre = AdoucirContourCap(capOrdre, centrePlan, u, v, 0.35f);
			}
			// Pas d'enrichissement : coque convexe = forme simple, évite triangles fins → pointes noires

			// CRÉATION DU CAP — UV normalisées [0,1] + projection orthogonale + filtrer triangles fins (réaliste)
			Vector3 nCap = plan.Normal * signeNormaleCap;
			Vector3 axeU = nCap.Cross(Vector3.Up).Normalized();
			if (axeU.LengthSquared() < 0.01f) axeU = nCap.Cross(Vector3.Right).Normalized();
			Vector3 axeV = nCap.Cross(axeU).Normalized();
			// UV [0,1] pour tangentes propres (évite singularités → pointes noires)
			float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
			foreach (Vector3 p in capOrdre) { float pu = p.Dot(axeU), pv = p.Dot(axeV); if (pu < minU) minU = pu; if (pu > maxU) maxU = pu; if (pv < minV) minV = pv; if (pv > maxV) maxV = pv; }
			float rU = Mathf.Max(0.001f, maxU - minU), rV = Mathf.Max(0.001f, maxV - minV);
			Vector2 UVCap(Vector3 p) => new Vector2(Mathf.Clamp((p.Dot(axeU) - minU) / rU, 0f, 1f), Mathf.Clamp((p.Dot(axeV) - minV) / rV, 0f, 1f));

			// Triangulation SANS éventail (évite singularité UV → texture en pointe/étoile)
			int n = capOrdre.Length;
			st.Clear();
			st.Begin(Mesh.PrimitiveType.Triangles);
			if (n == 3)
				AjouterTriCap(st, nCap, capOrdre, UVCap, 0, 1, 2, eps);
			else if (n == 4)
			{
				AjouterTriCap(st, nCap, capOrdre, UVCap, 0, 1, 2, eps);
				AjouterTriCap(st, nCap, capOrdre, UVCap, 0, 2, 3, eps);
			}
			else if (n == 5)
			{
				AjouterTriCap(st, nCap, capOrdre, UVCap, 0, 1, 2, eps);
				AjouterTriCap(st, nCap, capOrdre, UVCap, 0, 2, 4, eps);
				AjouterTriCap(st, nCap, capOrdre, UVCap, 2, 3, 4, eps);
			}
			else if (n >= 6)
			{
				int c = n / 2; // (0,c,n-1) central + régions (0..c) et (c..n-1)
				AjouterTriCap(st, nCap, capOrdre, UVCap, 0, c, n - 1, eps);
				for (int i = 1; i < c; i++) AjouterTriCap(st, nCap, capOrdre, UVCap, 0, i, i + 1, eps);
				for (int i = c + 1; i < n - 1; i++) AjouterTriCap(st, nCap, capOrdre, UVCap, c, i, i + 1, eps);
			}
			st.GenerateTangents(); // OBLIGATOIRE POUR LE TRIPLANAR NORMAL MAP
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, st.CommitToArrays());
		}
		return mesh;
	}

	/// <summary>Spawn une moitié de roche (modèle temporaire). Un seul MaterialOverride procédural (compat inventaire).</summary>
	private void SpawnMoitieRoche(ArrayMesh meshMoitie, Vector3 positionInitiale, Vector3 directionImpulsion, float nouvelleMasse)
	{
		Shape3D shape = CreerShapeCollisionConvexeRobuste(meshMoitie);
		if (shape == null) shape = new BoxShape3D { Size = new Vector3(0.08f, 0.08f, 0.08f) };
		ItemPhysique moitie = new ItemPhysique();
		moitie.EstUnEclat = true;
		moitie.EstEclatFracture = true;
		moitie.NiveauFracture = NiveauFracture + 1;
		moitie.ID_Objet = ID_Objet;
		moitie.IndexChimique = IndexChimique;
		moitie.IndexTailleRoche = IndexTailleRoche;
		moitie.IndexCacheMemoire = Mathf.Clamp(IndexCacheMemoire, 0, 3);
		moitie.Mass = nouvelleMasse;
		int idxCh = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
		moitie.ResistanceActuelle = TableGeologique[idxCh].ResistanceFuture * (nouvelleMasse / 50f);
		moitie.ContinuousCd = true;
		moitie.Scale = Vector3.One; // Atomes cuits dans le mesh, conteneur à 1,1,1

		MeshInstance3D visuel = new MeshInstance3D();
		visuel.Name = "MeshInstance3D";
		visuel.Mesh = meshMoitie;
		visuel.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		// Matériau procédural pour morceaux fracturés : triplanar MONDE (texture cohérente, plus de surfaces noires/blanches unies), très mat.
		StandardMaterial3D matMoitie = (StandardMaterial3D)CreerMaterielProcedural(EstMatiereSilexParIdObjet(ID_Objet), IndexChimique, pourEclat: false).Duplicate(true);
		matMoitie.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		matMoitie.Roughness = 0.95f;
		matMoitie.NormalEnabled = false;
		matMoitie.Uv1Triplanar = true;
		matMoitie.Uv1WorldTriplanar = true;   // Monde = texture basée sur position globale → variation naturelle, pas de facettes unies
		matMoitie.Uv1Scale = new Vector3(1.2f, 1.2f, 1.2f);
		visuel.MaterialOverride = matMoitie;
		moitie.AddChild(visuel);

		CollisionShape3D hitbox = new CollisionShape3D();
		hitbox.Name = "CollisionShape3D";
		hitbox.Shape = shape;
		moitie.AddChild(hitbox);

		if (!EstMatiereSilexParIdObjet(moitie.ID_Objet)) { moitie.ContactMonitor = true; moitie.MaxContactsReported = 1; moitie.BodyEntered += moitie.SurImpactPhysique; moitie._surImpactConnecte = true; }
		moitie.Name = "ItemPhysique";
		moitie.AddToGroup("BlocsPoses");
		moitie.SetMeta("ID_Matiere", moitie.ID_Objet);
		moitie.SetMeta("spawn_pos", positionInitiale);
		moitie.SetMeta("spawn_impulse", directionImpulsion * 0.8f + new Vector3((float)GD.Randf() - 0.5f, 0.3f, (float)GD.Randf() - 0.5f));
		moitie.TreeEntered += () => AppliquerSpawnEclat(moitie);
		GetParent().AddChild(moitie);
	}

	/// <summary>Ordonne les points dans le plan de coupe (angle autour du centre) pour former un vrai contour asymétrique, pas un éventail.</summary>
	private static Vector3[] OrdonnerPointsDansPlan(List<Vector3> points, Plane plan)
	{
		if (points == null || points.Count < 3) return points?.ToArray() ?? System.Array.Empty<Vector3>();
		Vector3 centrePlan = -plan.D * plan.Normal;
		Vector3 u = plan.Normal.Cross(Vector3.Up).Normalized();
		if (u.LengthSquared() < 0.01f) u = plan.Normal.Cross(Vector3.Right).Normalized();
		Vector3 v = plan.Normal.Cross(u).Normalized();
		var withAngle = new List<(Vector3 p3, float angle)>();
		Vector2 sum2D = Vector2.Zero;
		foreach (Vector3 pt in points)
		{
			Vector3 onPlan = plan.Project(pt);
			Vector3 d = onPlan - centrePlan;
			float xu = d.Dot(u);
			float xv = d.Dot(v);
			sum2D += new Vector2(xu, xv);
			withAngle.Add((onPlan, 0f));
		}
		Vector2 centre2D = sum2D / points.Count;
		for (int i = 0; i < points.Count; i++)
		{
			Vector3 onPlan = withAngle[i].p3;
			Vector3 d = onPlan - centrePlan;
			float angle = Mathf.Atan2(d.Dot(v) - centre2D.Y, d.Dot(u) - centre2D.X);
			withAngle[i] = (onPlan, angle);
		}
		withAngle.Sort((a, b) => a.angle.CompareTo(b.angle));
		var result = new Vector3[withAngle.Count];
		for (int i = 0; i < withAngle.Count; i++) result[i] = withAngle[i].p3;
		return result;
	}

	/// <summary>Adoucit le contour d'un éclat vers une forme plus régulière (enlève les coins pointus).</summary>
	private static Vector3[] AdoucirContourEclat(Vector3[] points, Vector3 centre, Vector3 u, Vector3 v, float force)
	{
		if (points == null || points.Length < 4 || force <= 0f) return points;
		float rayonMoyen = 0f;
		foreach (Vector3 p in points) rayonMoyen += (p - centre).Length();
		rayonMoyen /= points.Length;
		if (rayonMoyen < 0.001f) return points;
		var result = new Vector3[points.Length];
		for (int i = 0; i < points.Length; i++)
		{
			Vector3 d = points[i] - centre;
			float angle = Mathf.Atan2(d.Dot(v), d.Dot(u));
			Vector3 ptRegulier = centre + (float)Mathf.Cos(angle) * rayonMoyen * u + (float)Mathf.Sin(angle) * rayonMoyen * v;
			result[i] = points[i].Lerp(ptRegulier, force);
		}
		return result;
	}

	/// <summary>Insère des points sur les arêtes trop longues d'un polygone (évite triangles allongés).</summary>
	private static Vector3[] EnrichirContourPolygone(Vector3[] points, float maxLongueurArete)
	{
		if (points == null || points.Length < 3) return points;
		var enrichi = new List<Vector3>();
		for (int i = 0; i < points.Length; i++)
		{
			enrichi.Add(points[i]);
			Vector3 next = points[(i + 1) % points.Length];
			float dist = points[i].DistanceTo(next);
			if (dist > maxLongueurArete)
			{
				int nSeg = Mathf.Max(1, (int)(dist / maxLongueurArete));
				for (int s = 1; s < nSeg; s++)
					enrichi.Add(points[i].Lerp(next, (float)s / nSeg));
			}
		}
		return enrichi.Count > points.Length ? enrichi.ToArray() : points;
	}

	/// <summary>Quatre points d'un petit quad d'un côté du plan (fallback, plan = cassure passant par l'impact).</summary>
	private static Vector3[] PointsFallbackFragment(Plane plan, int cote)
	{
		Vector3 centrePlan = -plan.D * plan.Normal; // point sur le plan (cassure)
		float d = cote > 0 ? 0.12f : -0.12f;
		Vector3 n = plan.Normal;
		Vector3 u = n.Cross(Vector3.Up).Normalized();
		if (u.LengthSquared() < 0.01f) u = n.Cross(Vector3.Right).Normalized();
		Vector3 v = n.Cross(u).Normalized();
		float s = 0.05f;
		Vector3 basePt = centrePlan + d * n;
		return new Vector3[] { basePt, basePt + s * u, basePt + s * (u + v), basePt + s * v };
	}

	/// <summary>Reconstruit un éclat à partir des points d'une moitié. normaleDeCoupe = normale de la face de cassure (espace local) pour éclater l'effet éventail.</summary>
	private void SpawnEclatVrai(Vector3[] pointsFragment, float nouvelleMasse, Vector3 positionInitiale, Vector3 normaleDeCoupe)
	{
		if (pointsFragment == null || pointsFragment.Length < 4) return;

		// Centre et repère pour adoucissement
		Vector3 centre = Vector3.Zero;
		foreach (Vector3 p in pointsFragment) centre += p;
		centre /= pointsFragment.Length;
		var normalAcc = Vector3.Zero;
		for (int i = 0; i < pointsFragment.Length; i++)
		{
			Vector3 v1 = pointsFragment[i], v2 = pointsFragment[(i + 1) % pointsFragment.Length];
			normalAcc += (v1 - centre).Cross(v2 - centre);
		}
		if (normalAcc.LengthSquared() > 0.0001f)
		{
			Vector3 nPlan = normalAcc.Normalized();
			Vector3 tU = nPlan.Cross(Vector3.Up).Normalized();
			if (tU.LengthSquared() < 0.01f) tU = nPlan.Cross(Vector3.Right).Normalized();
			Vector3 tV = nPlan.Cross(tU).Normalized();
			pointsFragment = AdoucirContourEclat(pointsFragment, centre, tU, tV, 0.3f);
		}
		// Enrichir le contour : arêtes longues → points intermédiaires (évite étirement/rayons sur faces)
		pointsFragment = EnrichirContourPolygone(pointsFragment, 0.012f);

		// Recalcul centre après enrichissement
		centre = Vector3.Zero;
		foreach (Vector3 p in pointsFragment) centre += p;
		centre /= pointsFragment.Length;

		// Normale moyenne du fragment (surface de cassure) + repère tangent pour les UV
		Vector3 normalPlan = Vector3.Zero;
		int n = pointsFragment.Length;
		for (int i = 0; i < n; i++)
		{
			Vector3 v1 = pointsFragment[i];
			Vector3 v2 = pointsFragment[(i + 1) % n];
			normalPlan += (v1 - centre).Cross(v2 - centre);
		}
		if (normalPlan.LengthSquared() < 0.0001f) normalPlan = Vector3.Up;
		normalPlan = normalPlan.Normalized();
		Vector3 tangentU = normalPlan.Cross(Vector3.Up).Normalized();
		if (tangentU.LengthSquared() < 0.01f) tangentU = normalPlan.Cross(Vector3.Right).Normalized();
		Vector3 tangentV = normalPlan.Cross(tangentU).Normalized();
		float maxExtent = 0.01f;
		foreach (Vector3 p in pointsFragment) maxExtent = Mathf.Max(maxExtent, (p - centre).Length());
		float scaleUV = 0.35f / maxExtent;
		// Épaisseur type "caillou" : volume 3D au lieu d'une feuille plate
		float epaisseur = Mathf.Min(0.06f, maxExtent * 0.5f);

		// Triangulation du polygone
		var points2D = new Vector2[pointsFragment.Length];
		for (int i = 0; i < pointsFragment.Length; i++)
		{
			Vector3 d = pointsFragment[i] - centre;
			points2D[i] = new Vector2(d.Dot(tangentU), d.Dot(tangentV));
		}
		int[] indices = Geometry2D.TriangulatePolygon(points2D);

		// Sommets face arrière (extrusion pour donner du volume = caillou, pas papier)
		Vector3[] pointsArriere = new Vector3[n];
		for (int i = 0; i < n; i++)
			pointsArriere[i] = pointsFragment[i] - normalPlan * epaisseur;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		bool aDesSommetsAvecUV = false;

		void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 norm)
		{
			Vector3 cr = (b - a).Cross(c - a);
			if (cr.LengthSquared() < 0.0001f) return;
			Vector3 n = cr.Normalized();
			if (Mathf.Abs(n.Dot(normaleDeCoupe)) > 0.9f) n = normalPlan * Mathf.Sign(n.Dot(normalPlan));
			// Projection orthogonale (évite l'effet d'étoile sur la texture)
			Vector3 axeU = n.Cross(Vector3.Up).Normalized();
			if (axeU.LengthSquared() < 0.01f) axeU = n.Cross(Vector3.Right).Normalized();
			Vector3 axeV = n.Cross(axeU).Normalized();
			st.SetNormal(n); st.SetUV(new Vector2(a.Dot(axeU), a.Dot(axeV))); st.AddVertex(a);
			st.SetNormal(n); st.SetUV(new Vector2(b.Dot(axeU), b.Dot(axeV))); st.AddVertex(b);
			st.SetNormal(n); st.SetUV(new Vector2(c.Dot(axeU), c.Dot(axeV))); st.AddVertex(c);
			aDesSommetsAvecUV = true;
		}

		if (indices != null && indices.Length >= 3)
		{
			// Face avant (cassure)
			for (int t = 0; t + 2 < indices.Length; t += 3)
			{
				int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
				AddTri(pointsFragment[i0], pointsFragment[i1], pointsFragment[i2], normalPlan);
			}
			// Face arrière (sens inverse pour que la normale pointe vers l'extérieur)
			for (int t = 0; t + 2 < indices.Length; t += 3)
			{
				int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
				AddTri(pointsArriere[i0], pointsArriere[i2], pointsArriere[i1], -normalPlan);
			}
			// Bords (quads entre face avant et arrière) = tranche du caillou
			for (int i = 0; i < n; i++)
			{
				int j = (i + 1) % n;
				Vector3 nBord = (pointsFragment[j] - pointsFragment[i]).Cross(pointsArriere[i] - pointsFragment[i]).Normalized();
				if (nBord.LengthSquared() < 0.01f) continue;
				AddTri(pointsFragment[i], pointsFragment[j], pointsArriere[j], nBord);
				AddTri(pointsFragment[i], pointsArriere[j], pointsArriere[i], nBord);
			}
		}
		else
		{
			// Fallback triangle fan + extrusion
			for (int i = 0; i < n; i++)
			{
				Vector3 v0 = centre, v1 = pointsFragment[i], v2 = pointsFragment[(i + 1) % n];
				Vector3 cAr = centre - normalPlan * epaisseur;
				Vector3 ar1 = pointsArriere[i], ar2 = pointsArriere[(i + 1) % n];
				AddTri(v0, v1, v2, normalPlan);
				AddTri(cAr, ar2, ar1, -normalPlan);
				AddTri(v1, v2, ar2, (v2 - v1).Cross(ar1 - v1).Normalized());
				AddTri(v1, ar2, ar1, (v2 - v1).Cross(ar1 - v1).Normalized());
			}
		}
		// GenerateTangents exige des UV ; ne l'appeler que si au moins un triangle a été ajouté (avec SetUV).
		if (aDesSommetsAvecUV)
			st.GenerateTangents();
		ArrayMesh meshFragment = st.Commit();
		// Fallback : si la triangulation n'a rien donné, mesh minimal pour que le fragment apparaisse à l'écran
		if (meshFragment.GetFaces().Length == 0)
			meshFragment = CreerMeshFallbackFragment(centre, normalPlan, tangentU, tangentV);

		ItemPhysique eclat = new ItemPhysique();
		eclat.EstUnEclat = true;
		eclat.EstEclatFracture = true;
		eclat.NiveauFracture = NiveauFracture + 1;
		eclat.ID_Objet = ID_Objet;
		eclat.IndexChimique = IndexChimique;
		eclat.IndexTailleRoche = IndexTailleRoche;
		eclat.IndexCacheMemoire = Mathf.Clamp(IndexCacheMemoire, 0, 3);
		eclat.Mass = nouvelleMasse;
		int idxCh = Mathf.Clamp(IndexChimique, 0, TableGeologique.Length - 1);
		eclat.ResistanceActuelle = TableGeologique[idxCh].ResistanceFuture * (nouvelleMasse / 50f);
		eclat.ContinuousCd = true;

		bool estSilex = EstMatiereSilexParIdObjet(ID_Objet);
		Shape3D shapeCollision = CreerShapeCollisionConvexeRobuste(meshFragment);
		if (shapeCollision == null)
			shapeCollision = new BoxShape3D { Size = new Vector3(0.08f, 0.08f, 0.08f) };
		eclat.Scale = Vector3.One; // Sommets déjà cuits (scale dans les points), plus d'accordéon UV

		MeshInstance3D visuel = new MeshInstance3D();
		visuel.Name = "MeshInstance3D";
		visuel.Mesh = meshFragment;
		visuel.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		// Matériau harmonisé avec moitiés : triplanar MONDE (plus de facettes noires/blanches), UV normalisées.
		StandardMaterial3D materielBase = CreerMaterielProcedural(estSilex, IndexChimique, pourEclat: false);
		StandardMaterial3D materiel = (StandardMaterial3D)materielBase.Duplicate(true);
		materiel.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		materiel.Roughness = 0.95f;
		materiel.NormalEnabled = false;
		materiel.Uv1Triplanar = true;
		materiel.Uv1WorldTriplanar = true;
		materiel.Uv1Scale = new Vector3(1.2f, 1.2f, 1.2f);
		if (meshFragment.GetSurfaceCount() > 0)
			meshFragment.SurfaceSetMaterial(0, materiel);
		visuel.MaterialOverride = materiel;
		eclat.AddChild(visuel);

		CollisionShape3D hitbox = new CollisionShape3D();
		hitbox.Name = "CollisionShape3D";
		hitbox.Shape = shapeCollision;
		eclat.AddChild(hitbox);

		if (!EstMatiereSilexParIdObjet(eclat.ID_Objet))
		{
			eclat.ContactMonitor = true;
			eclat.MaxContactsReported = 1;
			eclat.BodyEntered += eclat.SurImpactPhysique;
			eclat._surImpactConnecte = true;
		}
		eclat.Name = "ItemPhysique";
		eclat.AddToGroup("BlocsPoses");
		eclat.SetMeta("ID_Matiere", eclat.ID_Objet);

		// Position et impulsion stockées pour appliquer une fois dans l'arbre (évite problèmes d'affichage)
		eclat.SetMeta("spawn_pos", positionInitiale);
		Vector3 explosion = new Vector3((float)GD.Randf() - 0.5f, 0.8f, (float)GD.Randf() - 0.5f).Normalized();
		eclat.SetMeta("spawn_impulse", explosion * 1.0f);
		eclat.TreeEntered += () => AppliquerSpawnEclat(eclat);

		GetParent().AddChild(eclat);
	}

	/// <summary>Applique position et impulsion une fois l'éclat dans l'arbre (fragment bien visible à l'écran). Force la mise à jour du matériau pour que la texture roche s'affiche.</summary>
	private static void AppliquerSpawnEclat(ItemPhysique eclat)
	{
		if (!eclat.HasMeta("spawn_pos")) return;
		Vector3 pos = (Vector3)eclat.GetMeta("spawn_pos");
		// Éviter que les fragments passent sous la map : plancher minimal en Y
		const float YMinSpawn = 0.5f;
		if (pos.Y < YMinSpawn) pos.Y = YMinSpawn;
		eclat.GlobalPosition = pos;
		eclat.RemoveMeta("spawn_pos");
		if (eclat.HasMeta("spawn_impulse"))
		{
			eclat.ApplyCentralImpulse((Vector3)eclat.GetMeta("spawn_impulse"));
			eclat.RemoveMeta("spawn_impulse");
		}
		// Forcer la mise à jour du matériau (évite fragment/moitié gris ou texture qui ne se met pas à jour)
		foreach (Node child in eclat.GetChildren())
		{
			if (child is MeshInstance3D mi)
			{
				if (mi.MaterialOverride is StandardMaterial3D matOverride)
				{
					mi.MaterialOverride = matOverride;
					if (mi.Mesh is ArrayMesh arr && arr.GetSurfaceCount() > 0 && arr.SurfaceGetMaterial(0) != matOverride)
						arr.SurfaceSetMaterial(0, matOverride);
				}
				else if (mi.Mesh is ArrayMesh arr2 && arr2.GetSurfaceCount() > 0)
				{
					// Moitié : reforcer l'override à l'entrée dans l'arbre (au cas où le rendu n'avait pas encore pris en compte)
					Material matSurf = arr2.SurfaceGetMaterial(0);
					if (matSurf != null)
						mi.MaterialOverride = (Material)matSurf.Duplicate(true);
				}
				break;
			}
		}
	}

	/// <summary>Quad épais minimal si la triangulation échoue (le fragment apparaît quand même).</summary>
	private static ArrayMesh CreerMeshFallbackFragment(Vector3 centre, Vector3 normalPlan, Vector3 tangentU, Vector3 tangentV)
	{
		float s = 0.06f;
		float e = 0.03f;
		Vector3 ar = centre - normalPlan * e;
		Vector3 v0 = centre + s * tangentU + s * tangentV;
		Vector3 v1 = centre + s * tangentU - s * tangentV;
		Vector3 v2 = centre - s * tangentU - s * tangentV;
		Vector3 v3 = centre - s * tangentU + s * tangentV;
		Vector3 a0 = ar + s * tangentU + s * tangentV;
		Vector3 a1 = ar + s * tangentU - s * tangentV;
		Vector3 a2 = ar - s * tangentU - s * tangentV;
		Vector3 a3 = ar - s * tangentU + s * tangentV;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		Action<Vector3, Vector3, Vector3, Vector3> tri = (a, b, c, n) => {
			st.SetNormal(n); st.AddVertex(a); st.SetNormal(n); st.AddVertex(b); st.SetNormal(n); st.AddVertex(c);
		};
		tri(v0, v1, v2, normalPlan); tri(v0, v2, v3, normalPlan);
		tri(a0, a2, a1, -normalPlan); tri(a0, a3, a2, -normalPlan);
		tri(v0, v3, a3, tangentU); tri(v0, a3, a0, tangentU);
		tri(v1, v0, a0, tangentV); tri(v1, a0, a1, tangentV);
		tri(v2, v1, a1, -tangentU); tri(v2, a1, a2, -tangentU);
		tri(v3, v2, a2, -tangentV); tri(v3, a2, a3, -tangentV);
		return st.Commit();
	}

	/// <summary>Réapplique mesh/collision/matériau après réutilisation depuis un pool (ID_Objet ou IndexCache/Chimique changés).</summary>
	public void ReappliquerApparence()
	{
		MeshInstance3D visuel = null;
		CollisionShape3D hitbox = null;
		foreach (Node child in GetChildren())
		{
			if (child is MeshInstance3D mi) visuel = mi;
			else if (child is CollisionShape3D cs) hitbox = cs;
		}
		if (visuel == null || hitbox == null) return;
		if (ID_Objet == 105)
		{
			AppliquerPhysiqueDague105(this);
			return;
		}
		if (ID_Objet == 106 || ID_Objet == Joueur.IdObjetHachePierreTier1)
		{
			AppliquerPhysiqueHachette106(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetPellePierreTier0)
		{
			AppliquerPhysiquePelle107(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetPiochePierreTier0)
		{
			AppliquerPhysiquePioche108(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetLancePierreTier0)
		{
			AppliquerPhysiqueLance111(this);
			return;
		}
		if (ID_Objet == Joueur.IdObjetFauxPierreTier0)
		{
			AppliquerPhysiqueFaux112(this);
			return;
		}
		if (EstIdRocheMatiere(ID_Objet))
		{
			IndexChimique = IndexChimiqueDepuisIdRoche(ID_Objet);
			IndexTailleRoche = Mathf.Clamp(IndexTailleRoche, 0, 4);
			if (IndexCacheMemoire < 0)
				IndexCacheMemoire = GD.RandRange(0, 3);
			IndexCacheMemoire = Mathf.Clamp(IndexCacheMemoire, 0, 3);
			float r = RayonBaseRochesJoueur(IndexTailleRoche);
			Vector3 morph = EchelleMorphologieRoche(IndexCacheMemoire);
			Scale = Vector3.One;
			visuel.Scale = morph;
			hitbox.Scale = Vector3.One;
			visuel.Mesh = new SphereMesh { Radius = r, Height = r * 2f };
			hitbox.Shape = CreerShapeCollisionRocheMatiere(r, IndexCacheMemoire);
			AppliquerMateriel(visuel);
			int ich = IndexChimiqueDepuisIdRoche(ID_Objet);
			ResistanceActuelle = TableGeologique[ich].ResistanceFuture * FacteurSoliditeRochesParTaille(IndexTailleRoche);
			float vol = 4f / 3f * Mathf.Pi * r * r * r;
			Mass = Mathf.Max(0.04f, vol * 2200f * Mathf.Abs(morph.X * morph.Y * morph.Z));
			AppliquerPhysiqueRochePortee(this);
			return;
		}
		if (IndexChimique < 0) IndexChimique = GD.RandRange(0, TableGeologique.Length - 1);
		AppliquerMateriel(visuel);
		if (IndexCacheMemoire < 0)
		{
			bool formesCassées = (IndexCacheMemoire == -2);
			IndexCacheMemoire = PreparerCacheEtTirerIndex(false, formesCassées);
		}
		int idx = Mathf.Clamp(IndexCacheMemoire, 0, int.MaxValue);
		if (idx < _cacheMeshCaillou.Count) { visuel.Mesh = _cacheMeshCaillou[idx]; hitbox.Shape = _cacheCollisionCaillou[idx]; }
		Scale = Vector3.One;
		visuel.Scale = Vector3.One;
	}

	private int PreparerCacheEtTirerIndex(bool estSilex, bool formesCassées = false)
	{
		if (estSilex)
		{
			lock (_cacheMeshSilex)
			{
				if (_cacheMeshSilex.Count < NbVariationsCache)
					GenererEtMettreEnCache(true);
				int count = _cacheMeshSilex.Count;
				if (count == 0) return 0;
				if (formesCassées && count > 1) return GD.RandRange(count / 2, count - 1);
				return GD.RandRange(0, Mathf.Max(0, (count / 2) - 1));
			}
		}
		lock (_cacheMeshCaillou)
		{
			if (_cacheMeshCaillou.Count < NbVariationsCache)
				GenererEtMettreEnCache(false);
			int count = _cacheMeshCaillou.Count;
			if (count == 0) return 0;
			if (formesCassées && count > 1) return GD.RandRange(count / 2, count - 1);
			return GD.RandRange(0, Mathf.Max(0, (count / 2) - 1));
		}
	}

	private void AppliquerMateriel(MeshInstance3D visuel)
	{
		visuel.MaterialOverride = CreerMaterielProcedural(EstMatiereSilexParIdObjet(ID_Objet), IndexChimique);
	}

	/// <summary>Retourne le mesh du premier MeshInstance3D enfant (pour éclats et ramassage).</summary>
	public Mesh ObtenirMeshVisuel()
	{
		foreach (Node c in GetChildren())
			if (c is MeshInstance3D mi) return mi.Mesh;
		return null;
	}

	/// <summary>Matériau procédural basé sur la chimie réelle (TableGeologique). Taches, veines, rugosité. Mis en cache pour éviter le freeze à la cassure.</summary>
	/// <param name="pourEclat">Si true, désactive le triplanar et utilise les UV du mesh (évite l'effet "pizza" sur les fragments).</param>
	public static StandardMaterial3D CreerMaterielProcedural(bool estSilex, int indexChimique, bool pourEclat = false)
	{
		int idx = Mathf.Clamp(indexChimique, 0, TableGeologique.Length - 1);
		var key = (estSilex, idx, pourEclat);
		lock (_cacheMateriaux)
		{
			if (_cacheMateriaux.TryGetValue(key, out StandardMaterial3D cached))
				return cached;
		}
		var materiel = new StandardMaterial3D();
		// Seed déterministe par minéral : roche et ses éclats ont la même apparence (même type de pierre)
		int seedCouleur = 50000 + idx * 7919;
		int seedRelief = 60000 + idx * 7919;
		var bruitRelief = new FastNoiseLite { Seed = seedRelief };

		ProfilMineral chimie = TableGeologique[idx];
		materiel.Roughness = chimie.Rugosite;

		// 1. Pigmentation : même texture pour roches et éclats (taches, veines)
		var bruitCouleur = new FastNoiseLite
		{
			Seed = seedCouleur,
			NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			Frequency = 0.03f,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm
		};
		var textureCouleur = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruitCouleur };
		var degradeMineral = new Gradient();
		degradeMineral.AddPoint(0f, chimie.CouleurTache);
		degradeMineral.AddPoint(0.5f, chimie.CouleurBase);
		degradeMineral.AddPoint(1f, chimie.CouleurVeine);
		textureCouleur.ColorRamp = degradeMineral;
		materiel.AlbedoTexture = textureCouleur;

		// 2. Micro-relief : même que les roches (éclats = même apparence, forme cassée uniquement)
		var textureRelief = new NoiseTexture2D { Width = 256, Height = 256, GenerateMipmaps = true, AsNormalMap = true };
		if (estSilex)
		{
			materiel.Metallic = 0.2f;
			bruitRelief.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
			bruitRelief.Frequency = 0.08f;
			textureRelief.BumpStrength = 3.0f;
		}
		else
		{
			materiel.Metallic = 0.0f;
			bruitRelief.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			bruitRelief.Frequency = 0.15f;
			textureRelief.BumpStrength = 1.5f;
		}
		textureRelief.Noise = bruitRelief;
		materiel.NormalEnabled = true;
		materiel.NormalTexture = textureRelief;
		if (!pourEclat)
		{
			// Triplanar en espace objet (évite étirement, masque défauts UV plan de coupe) — vital pour objets physiques et inventaire
			materiel.Uv1Triplanar = true;
			materiel.Uv1WorldTriplanar = false;
			materiel.Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f);
			materiel.Uv1TriplanarSharpness = 2.0f;
		}
		// Pour les éclats : pas de triplanar, UV planaire sur la cassure (réduit quadrillage)
		lock (_cacheMateriaux) { _cacheMateriaux[key] = materiel; }
		return materiel;
	}

	/// <summary>Réduit la liste à au plus maxPoints en gardant des points répartis (évite freeze). Garde au moins 4 points.</summary>
	private static void ReduirePointsContour(List<Vector3> points, int maxPoints)
	{
		if (points == null || points.Count <= maxPoints) return;
		int step = Mathf.Max(1, points.Count / Mathf.Max(4, maxPoints));
		var reduced = new List<Vector3>();
		for (int i = 0; i < points.Count && reduced.Count < maxPoints; i += step)
			reduced.Add(points[i]);
		while (reduced.Count < 4 && reduced.Count < points.Count)
			reduced.Add(points[reduced.Count]);
		points.Clear();
		points.AddRange(reduced);
	}

	/// <summary>UV sphériques (fallback).</summary>
	private static Vector2 UVSpherique(Vector3 centre, Vector3 point)
	{
		Vector3 d = (point - centre).Normalized();
		float u = 0.5f + Mathf.Atan2(d.Z, d.X) / (2f * Mathf.Pi);
		float v = 0.5f - Mathf.Asin(Mathf.Clamp(d.Y, -1f, 1f)) / Mathf.Pi;
		return new Vector2(u, v);
	}

	/// <summary>Méta sur ItemPhysique : ScaleEclat inventaire quand le mesh posé est « cuit » (bake) en monde à l’échelle 1.</summary>
	public const string MetaScaleEclatInventaire = "ScaleEclatInventaire";

	/// <summary>Duplique le mesh en multipliant chaque sommet par <paramref name="scale"/> (non uniforme). Le RigidBody peut rester (1,1,1) pour une physique stable.</summary>
	public static ArrayMesh DupliquerMeshBakeEchelle(Mesh mesh, Vector3 scale)
	{
		if (mesh == null) return null;
		if ((scale - Vector3.One).LengthSquared() < 1e-12f) return null;
		Vector3[] faces = mesh.GetFaces();
		if (faces == null || faces.Length < 9) return null;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		for (int i = 0; i < faces.Length; i += 3)
		{
			Vector3 a = new Vector3(faces[i].X * scale.X, faces[i].Y * scale.Y, faces[i].Z * scale.Z);
			Vector3 b = new Vector3(faces[i + 1].X * scale.X, faces[i + 1].Y * scale.Y, faces[i + 1].Z * scale.Z);
			Vector3 c = new Vector3(faces[i + 2].X * scale.X, faces[i + 2].Y * scale.Y, faces[i + 2].Z * scale.Z);
			Vector3 cr = (b - a).Cross(c - a);
			if (cr.LengthSquared() < 1e-12f) continue;
			Vector3 n = cr.Normalized();
			// GenerateTangents() exige des UV (erreur Godot sinon).
			void AddVert(Vector3 v)
			{
				st.SetNormal(n);
				st.SetUV(new Vector2(v.X * 0.5f + v.Z * 0.5f, v.Y * 0.5f));
				st.AddVertex(v);
			}
			AddVert(a);
			AddVert(b);
			AddVert(c);
		}
		st.GenerateTangents();
		ArrayMesh arr = st.Commit();
		return arr != null && arr.GetSurfaceCount() > 0 ? arr : null;
	}

	/// <summary>Crée une shape de collision sans faire échouer Jolt ("initial triangle area too small"). BoxShape3D depuis AABB = toujours valide. Public pour éclats (Joueur).</summary>
	public static Shape3D CreerShapeCollisionConvexeRobuste(Mesh mesh)
	{
		if (mesh == null) return new BoxShape3D { Size = Vector3.One * 0.2f };
		Aabb aabb = mesh.GetAabb();
		Vector3 size = aabb.Size;
		if (size.X < 0.02f) size.X = 0.1f;
		if (size.Y < 0.02f) size.Y = 0.1f;
		if (size.Z < 0.02f) size.Z = 0.1f;
		return new BoxShape3D { Size = size };
	}

	/// <summary>UV en projection planaire sur la surface de cassure : la texture suit les angles du fragment.</summary>
	private static Vector2 UVPlanCassure(Vector3 centre, Vector3 point, Vector3 normalPlan, Vector3 tangentU, Vector3 tangentV, float scaleUV)
	{
		Vector3 d = point - centre;
		float u = d.Dot(tangentU) * scaleUV + 0.5f;
		float v = d.Dot(tangentV) * scaleUV + 0.5f;
		return new Vector2(u, v);
	}

	private void GenererEtMettreEnCache(bool estSilex)
	{
		ArrayMesh arrayMesh;
		float forceDeformation;

		// Sphère peu détaillée pour que Jolt accepte la shape convexe (évite "initial triangle area too small" avec 1988 sommets)
		if (estSilex)
		{
			var primitive = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 12, Rings = 8 };
			arrayMesh = new ArrayMesh();
			arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, primitive.GetMeshArrays());
			forceDeformation = 0.3f;
		}
		else
		{
			var primitive = new SphereMesh { Radius = 0.15f, Height = 0.3f, RadialSegments = 12, Rings = 8 };
			arrayMesh = new ArrayMesh();
			arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, primitive.GetMeshArrays());
			forceDeformation = 0.15f;
		}

		var bruit = new FastNoiseLite();
		bruit.Seed = (int)GD.Randi();
		if (estSilex)
		{
			bruit.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
			bruit.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
			bruit.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
		}
		else
			bruit.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

		var mdt = new MeshDataTool();
		if (mdt.CreateFromSurface(arrayMesh, 0) != Error.Ok) return;

		// Génétique des proportions : vecteur d'écrasement/étirement procédural unique par modèle
		Vector3 adnMorphologique;
		if (!estSilex)
		{
			// CAILLOU : X et Z varient un peu, Y varie énormément (galette 0.3 → patate ronde 1.0)
			adnMorphologique = new Vector3(
				0.7f + (float)GD.Randf() * 0.5f,
				0.3f + (float)GD.Randf() * 0.7f,
				0.7f + (float)GD.Randf() * 0.5f
			);
		}
		else
		{
			// SILEX : étirement sur un axe pour forme de lame ou d'éclat
			adnMorphologique = new Vector3(
				0.6f + (float)GD.Randf() * 0.4f,
				0.6f + (float)GD.Randf() * 0.4f,
				1.0f + (float)GD.Randf() * 0.8f
			);
		}

		for (int i = 0; i < mdt.GetVertexCount(); i++)
		{
			Vector3 pos = mdt.GetVertex(i);
			Vector3 n = mdt.GetVertexNormal(i);
			float b = bruit.GetNoise3D(pos.X * 10f, pos.Y * 10f, pos.Z * 10f);
			Vector3 positionNouvelle = pos + (n * b * forceDeformation);
			// Écrase/étire le sommet selon l'ADN morphologique de ce modèle
			positionNouvelle.X *= adnMorphologique.X;
			positionNouvelle.Y *= adnMorphologique.Y;
			positionNouvelle.Z *= adnMorphologique.Z;
			mdt.SetVertex(i, positionNouvelle);
		}

		// Recalcul des normales (MeshDataTool n'a pas GenerateNormals) : moyenne des normales des faces adjacentes
		for (int i = 0; i < mdt.GetVertexCount(); i++)
		{
			int[] faces = mdt.GetVertexFaces(i);
			Vector3 sum = Vector3.Zero;
			foreach (int faceIdx in faces)
				sum += mdt.GetFaceNormal(faceIdx);
			if (sum.LengthSquared() > 0.0001f)
				mdt.SetVertexNormal(i, sum.Normalized());
		}

		var nouveauMesh = new ArrayMesh();
		mdt.CommitToSurface(nouveauMesh);

		// Hitbox convexe ; Jolt échoue si trop de sommets ou triangles trop petits ("initial triangle area too small")
		Shape3D nouvelleCollision = CreerShapeCollisionConvexeRobuste(nouveauMesh);

		if (estSilex)
		{
			_cacheMeshSilex.Add(nouveauMesh);
			_cacheCollisionSilex.Add(nouvelleCollision);
		}
		else
		{
			_cacheMeshCaillou.Add(nouveauMesh);
			_cacheCollisionCaillou.Add(nouvelleCollision);
		}
	}
}
