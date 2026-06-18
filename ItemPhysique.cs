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
        || idObjet == Joueur.IdObjetFourTorchie
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

	/// <summary>Butin au sol en repos : mesh + collision pour le raycast E, mais corps figé « statique » (coût Jolt minimal).</summary>
	public bool EstEnReposAuSolOptimise { get; private set; }
	private bool _continuousCdAvantRepos = true;

	/// <summary>Passe en mode décoratif au repos : visible et ramassable, sans simulation dynamique continue.</summary>
	public void PasserEnReposAuSolOptimise()
	{
		if (!GodotObject.IsInstanceValid(this) || EstMeublePoseStatique(ID_Objet))
			return;
		_continuousCdAvantRepos = ContinuousCd;
		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		Sleeping = true;
		Freeze = true;
		FreezeMode = FreezeModeEnum.Static;
		ContinuousCd = false;
		EstEnReposAuSolOptimise = true;
		ActualiserBoiteOcclusionLocale();
		GererOccludeurStatiqueObjet(true);
	}

	/// <summary>Réactive la physique (frappe, poussée joueur, lancer) après un repos optimisé.</summary>
	public void ReveillerPhysiqueAuSol()
	{
		if (!GodotObject.IsInstanceValid(this))
			return;
		if (!Freeze && !EstEnReposAuSolOptimise)
			return;
		Freeze = false;
		Sleeping = false;
		ContinuousCd = _continuousCdAvantRepos;
		EstEnReposAuSolOptimise = false;
		GererOccludeurStatiqueObjet(false);
		ActualiserBoiteOcclusionLocale();
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
	/// <summary>Durée totale (s) de l'unité de combustible en train de brûler — pour la barre de combustion (resteSec / total).</summary>
	private double _pitFeuRocheDureeUniteCouranteSec = DureeCombustionPitFeuSec;
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

	private void ActiverOmbresMeshesEnfants()
	{
		var pile = new System.Collections.Generic.List<Node> { this };
		for (int i = 0; i < pile.Count; i++)
		{
			Node noeud = pile[i];
			if (noeud is MeshInstance3D mi)
				mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
			foreach (Node enfant in noeud.GetChildren())
				pile.Add(enfant);
		}
	}

	public override void _Ready()
	{
		if (IsInGroup("BlocsPoses"))
		{
			Callable.From(NotifierEnregistrementOcclusionObjetPose).CallDeferred();
			Callable.From(ActiverOmbresMeshesEnfants).CallDeferred();
		}

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
		if (ID_Objet == Joueur.IdObjetFourTorchie)
		{
			InitialiserFourTorchiePose();
			return;
		}
		if (ID_Objet == Joueur.IdObjetBolCeramique)
		{
			InitialiserBolCeramiquePose();
			return;
		}
		if (ID_Objet == Joueur.IdObjetMouleCeramique)
		{
			InitialiserMouleCeramiquePose();
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
		if (ID_Objet == Joueur.IdObjetFourTorchie)
		{
			TraiterFourTorchie(delta);
			return;
		}
		if (ID_Objet == Joueur.IdObjetBolCeramique)
		{
			TraiterRefroidissementBolCeramiqueAuSoleil(delta);
			return;
		}
		if (ID_Objet == Joueur.IdObjetMouleCeramique)
		{
			TraiterRefroidissementMouleCeramiqueAuSoleil(delta);
			return;
		}
		if (ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche)
		{
			bool estRoche = ID_Objet == Joueur.IdObjetPitFeuRoche;
			double resteSec = estRoche ? _pitFeuRocheResteSec : _pitFeuResteSec;
			if (resteSec <= 0.001d)
			{
				// Garde-fou : un feu NON allumé ne doit jamais afficher de flammes ni de lumière.
				// L'animation des flammes ci-dessous ne tourne que feu allumé : si un visuel restait actif
				// (re-pose, état résiduel, ordre d'init), les flammes paraissaient « figées » et la lumière restait allumée.
				if ((_pitFlammeCroix != null && GodotObject.IsInstanceValid(_pitFlammeCroix) && _pitFlammeCroix.Visible)
					|| (_pitFlammeLight != null && GodotObject.IsInstanceValid(_pitFlammeLight) && _pitFlammeLight.Visible)
					|| (_pitFlammeParticles != null && GodotObject.IsInstanceValid(_pitFlammeParticles) && _pitFlammeParticles.Visible))
					ActiverVisuelPitFeu(false);
				return;
			}
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
				_pitFlammeLight.LightEnergy = LumiereFeuEnergy - 0.35f + 0.28f * Mathf.Sin(t * 9.6f) + 0.14f * pulseSlow;
			}
			if (resteSec <= 0.001d)
			{
				if (estRoche)
				{
					_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
					byte essenceCombustible = ObtenirEssenceCombustiblePitFeuRoche();
					if (_pitFeuRocheStockCombustible > 0 && RetirerCombustiblePitFeuRocheDepuisGrille(1))
					{
						_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
						resteSec = DureeCombustionPitFeuRochePourEssence(essenceCombustible);
						_pitFeuRocheDureeUniteCouranteSec = resteSec;
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



}
