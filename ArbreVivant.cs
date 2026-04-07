using Godot;
using System;
using System.Collections.Generic;

/// <summary>Entité 3D d'arbre procédurale (L-System volumétrique). Branches continues, feuillage, croissance temporelle.</summary>
/// <remarks>Hérite de StaticBody3D. Remplaçant des arbres voxels.</remarks>
public partial class ArbreVivant : StaticBody3D
{
	private List<Vector3> _coupesLocales = new List<Vector3>();

	private struct TortueEtat
	{
		public Transform3D Transform;
		public float Epaisseur;
		public bool EstCoupe;
	}

	public int AgeEnJours = 1;
	public float ResistanceActuelle = 53.5f;

	/// <summary>PV max du fût vivant : croît linéairement + quadratiquement avec l’âge (grands arbres beaucoup plus tenaces).</summary>
	public static float ResistanceMaxPourAge(int ageEnJours)
	{
		ageEnJours = Mathf.Max(1, ageEnJours);
		return 50f * ageEnJours + 3.5f * ageEnJours * ageEnJours;
	}
	/// <summary>Graine pour variabilité des angles/longueurs (évite arbres identiques).</summary>
	public uint Seed = 12345;
	public byte IndexBotanique = 0;
	private const float CHANCE_CROISSANCE = 0.05f; // 1 chance sur 20 de grandir chaque nuit

	/// <summary>Dimensions réelles du tronc (remplis par GenererMaillageArbre). Utilisées pour la bûche et les bâtons au démembrement.</summary>
	private float _hauteurTroncTotale;
	private float _rayonTroncBase;
	private float _rayonTroncSommet;
	private float _longueurBrancheMoyenne;
	private float _epaisseurBrancheMoyenne;

	private MeshInstance3D _visuelBois;
	private MeshInstance3D _visuelFeuillage;
	private CollisionShape3D _hitbox;
	private Node3D _observationRef;
	private int _lodActuel = -1;
	private bool _maillageInitialGenere;
	private float _attenteGeneration;
	private float _cooldownLod;
	private uint _seedEffectif;
	private float _lodFeuillageActuel = 1f;

	private const float DISTANCE_LOD0 = 130f;
	private const float DISTANCE_LOD1 = 280f;
	private const float DISTANCE_LOD2 = 520f;
	private const float INTERVALLE_MAJ_LOD = 0.55f;

	private static StandardMaterial3D _cacheMatBois;
	private static StandardMaterial3D _cacheMatBoisTriplanar;
	private static StandardMaterial3D _cacheMatBoisPin;
	private static StandardMaterial3D _cacheMatBoisTriplanarPin;
	private static StandardMaterial3D _cacheMatBoisBouleau;
	private static StandardMaterial3D _cacheMatBoisTriplanarBouleau;
	private static StandardMaterial3D _cacheMatBoisBatonChenEPale;
	private static StandardMaterial3D _cacheMatFeuillesCaduc;
	private static StandardMaterial3D _cacheMatFeuillesPin;
	private static Texture2D _cacheTextureFeuilleCaduc;
	private uint SeedForme => _seedEffectif != 0 ? _seedEffectif : Seed;

	public static Material ObtenirMaterielBois(byte indexBotanique = 0)
	{
		if (indexBotanique == LSystem_Botanique.IndexPin)
		{
			if (_cacheMatBoisPin != null) return _cacheMatBoisPin;
			var bruit = new FastNoiseLite { Seed = 999, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.1f };
			var tex = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruit };
			var ramp = new Gradient();
			ramp.AddPoint(0.0f, new Color(0.11f, 0.08f, 0.07f)); // Ecorce sombre
			ramp.AddPoint(1.0f, new Color(0.24f, 0.18f, 0.15f));
			tex.ColorRamp = ramp;
			_cacheMatBoisPin = new StandardMaterial3D
			{
				AlbedoTexture = tex,
				AlbedoColor = new Color(0.26f, 0.19f, 0.15f),
				Roughness = 0.96f
			};
			return _cacheMatBoisPin;
		}

		if (indexBotanique == LSystem_Botanique.IndexBouleau)
		{
			if (_cacheMatBoisBouleau != null) return _cacheMatBoisBouleau;
			var bruit = new FastNoiseLite { Seed = 777, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.08f };
			var tex = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruit };
			var ramp = new Gradient();
			ramp.AddPoint(0.0f, new Color(0.1f, 0.1f, 0.1f));    // Taches noires
			ramp.AddPoint(0.2f, new Color(0.85f, 0.85f, 0.82f));  // Écorce blanche
			ramp.AddPoint(1.0f, new Color(0.92f, 0.92f, 0.90f));
			tex.ColorRamp = ramp;
			_cacheMatBoisBouleau = new StandardMaterial3D { AlbedoTexture = tex, Roughness = 0.85f };
			return _cacheMatBoisBouleau;
		}

		if (_cacheMatBois != null) return _cacheMatBois;
		var bruitEcorce = new FastNoiseLite { Seed = 4242 };
		bruitEcorce.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		bruitEcorce.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		bruitEcorce.Frequency = 0.08f; // Fines stries type écorce
		var texEcorce = new NoiseTexture2D { Width = 128, Height = 128, Noise = bruitEcorce };
		_cacheMatBois = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.52f, 0.32f, 0.14f), // Brun bois chaud
			AlbedoTexture = texEcorce,
			Roughness = 0.9f,
			Metallic = 0.02f
		};
		return _cacheMatBois;
	}

	/// <summary>Même texture que le tronc, triplanar monde (ignore les UV locaux dégénérés) + teinte aubier.</summary>
	public static Material ObtenirMaterielBoisTriplanar(byte indexBotanique = 0)
	{
		if (indexBotanique == LSystem_Botanique.IndexPin)
		{
			if (_cacheMatBoisTriplanarPin != null) return _cacheMatBoisTriplanarPin;
			ObtenirMaterielBois(LSystem_Botanique.IndexPin);
			_cacheMatBoisTriplanarPin = (StandardMaterial3D)_cacheMatBoisPin.Duplicate();
			_cacheMatBoisTriplanarPin.Uv1Triplanar = true;
			_cacheMatBoisTriplanarPin.Uv1WorldTriplanar = false;
			_cacheMatBoisTriplanarPin.Uv1TriplanarSharpness = 2f;
			// Rendu au sol/objets plus proche de l'écorce réelle du pin (moins jaune clair).
			_cacheMatBoisTriplanarPin.AlbedoColor = new Color(0.46f, 0.34f, 0.25f);
			_cacheMatBoisTriplanarPin.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			return _cacheMatBoisTriplanarPin;
		}

		if (indexBotanique == LSystem_Botanique.IndexBouleau)
		{
			if (_cacheMatBoisTriplanarBouleau != null) return _cacheMatBoisTriplanarBouleau;
			ObtenirMaterielBois(LSystem_Botanique.IndexBouleau);
			_cacheMatBoisTriplanarBouleau = (StandardMaterial3D)_cacheMatBoisBouleau.Duplicate();
			_cacheMatBoisTriplanarBouleau.Uv1Triplanar = true;
			_cacheMatBoisTriplanarBouleau.Uv1WorldTriplanar = false;
			_cacheMatBoisTriplanarBouleau.Uv1TriplanarSharpness = 2f;
			_cacheMatBoisTriplanarBouleau.AlbedoColor = new Color(0.82f, 0.78f, 0.65f); // Bois intérieur clair
			_cacheMatBoisTriplanarBouleau.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			return _cacheMatBoisTriplanarBouleau;
		}

		if (_cacheMatBoisTriplanar != null) return _cacheMatBoisTriplanar;
		ObtenirMaterielBois();
		_cacheMatBoisTriplanar = (StandardMaterial3D)_cacheMatBois.Duplicate();
		_cacheMatBoisTriplanar.Uv1Triplanar = true;
		// Mapping triplanar local (et non monde) pour figer la texture sur chaque mesh de bois.
		_cacheMatBoisTriplanar.Uv1WorldTriplanar = false;
		_cacheMatBoisTriplanar.Uv1TriplanarSharpness = 2f;
		_cacheMatBoisTriplanar.AlbedoColor = new Color(0.65f, 0.45f, 0.25f);
		// Désactive le backface culling (éclats / faces de coupe visibles même si winding imparfait).
		_cacheMatBoisTriplanar.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		return _cacheMatBoisTriplanar;
	}

	/// <summary>Bâton de chêne façonné au craft (branche → bâton) : même texture triplanar, albedo plus pâle (aubier / bois travaillé).</summary>
	public static Material ObtenirMaterielBoisTriplanarBatonChenEPale()
	{
		if (_cacheMatBoisBatonChenEPale != null) return _cacheMatBoisBatonChenEPale;
		ObtenirMaterielBoisTriplanar();
		_cacheMatBoisBatonChenEPale = (StandardMaterial3D)_cacheMatBoisTriplanar.Duplicate();
		Color baseC = _cacheMatBoisBatonChenEPale.AlbedoColor;
		_cacheMatBoisBatonChenEPale.AlbedoColor = baseC.Lerp(new Color(0.92f, 0.86f, 0.78f), 0.38f);
		return _cacheMatBoisBatonChenEPale;
	}

	private static Material ObtenirMaterielFeuilles(byte indexBotanique)
	{
		if (indexBotanique == LSystem_Botanique.IndexPin)
		{
			if (_cacheMatFeuillesPin != null) return _cacheMatFeuillesPin;
			_cacheMatFeuillesPin = new StandardMaterial3D
			{
				AlbedoColor = Colors.White,
				VertexColorUseAsAlbedo = true,
				Roughness = 0.93f,
				Metallic = 0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			return _cacheMatFeuillesPin;
		}
		if (_cacheMatFeuillesCaduc != null) return _cacheMatFeuillesCaduc;
		if (_cacheTextureFeuilleCaduc == null)
		{
			var bruitFeuille = new FastNoiseLite { Seed = 21037 };
			bruitFeuille.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			bruitFeuille.Frequency = 0.16f;
			bruitFeuille.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
			bruitFeuille.FractalOctaves = 3;
			_cacheTextureFeuilleCaduc = new NoiseTexture2D
			{
				Width = 96,
				Height = 96,
				Noise = bruitFeuille
			};
		}
		_cacheMatFeuillesCaduc = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = _cacheTextureFeuilleCaduc,
			Roughness = 0.95f,
			Metallic = 0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		return _cacheMatFeuillesCaduc;
	}

	public override void _Ready()
	{
		_visuelBois = new MeshInstance3D { Name = "Bois" };
		_visuelFeuillage = new MeshInstance3D { Name = "Feuillage" };
		_hitbox = new CollisionShape3D { Name = "Hitbox" };
		_visuelFeuillage.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		AddChild(_visuelBois);
		AddChild(_visuelFeuillage);
		AddChild(_hitbox);

		AddToGroup("Arbres");
		uint px = (uint)Mathf.Abs((int)GlobalPosition.X);
		uint py = (uint)Mathf.Abs((int)GlobalPosition.Y);
		uint pz = (uint)Mathf.Abs((int)GlobalPosition.Z);
		uint posHash = (px * 73856093u) ^ (py * 83492791u) ^ (pz * 19349663u);
		_seedEffectif = Seed ^ posHash ^ (uint)(IndexBotanique * 2654435761u);

		// Répartit le coût de spawn sur plusieurs frames (évite le freeze quand une forêt apparaît).
		float dObs = GlobalPosition.DistanceTo(PositionObservation());
		float h = Hash(SeedForme, 15000);
		// Proche joueur: génération immédiate (évite de voir "pop" les feuilles).
		// Moyen: léger étalement. Loin: étalement plus large pour lisser le coût global.
		if (dObs <= 110f) _attenteGeneration = 0f;
		else if (dObs <= 210f) _attenteGeneration = 0.01f + h * 0.10f;
		else _attenteGeneration = 0.10f + h * 0.55f;
		_cooldownLod = Hash(SeedForme, 15100) * INTERVALLE_MAJ_LOD;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (!_maillageInitialGenere)
		{
			_attenteGeneration -= dt;
			if (_attenteGeneration <= 0f)
				RegenererSelonLod(true);
			return;
		}

		_cooldownLod -= dt;
		if (_cooldownLod <= 0f)
		{
			_cooldownLod = INTERVALLE_MAJ_LOD + Hash(SeedForme, 15200) * 0.35f;
			RegenererSelonLod(false);
		}
	}

	/// <summary>Appelé à minuit par le serveur (arbres dans chunks actifs). 1 chance sur 20 de grandir.</summary>
	public void VieillirUnJour()
	{
		if (GD.Randf() <= CHANCE_CROISSANCE)
		{
			AgeEnJours++;
			ResistanceActuelle = ResistanceMaxPourAge(AgeEnJours);
			RegenererSelonLod(true);
		}
	}

	/// <summary>Simule le temps passé hors-ligne quand le chunk est rechargé. Déterministe (seed position).</summary>
	/// <param name="joursEcoules">Jours où le chunk était déchargé.</param>
	/// <param name="posMonde">Position de l'arbre (pour seed déterministe si pas encore dans la scène).</param>
	public void RattraperCroissance(int joursEcoules, Vector3? posMonde = null)
	{
		if (joursEcoules <= 0) return;
		Vector3 pos = posMonde ?? GlobalPosition;
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Mathf.Abs(pos.X) * 73856.0 + Mathf.Abs(pos.Z) * 19349.0 + joursEcoules * 7919);
		int succesCroissance = 0;
		for (int i = 0; i < joursEcoules; i++)
		{
			if (rng.Randf() <= CHANCE_CROISSANCE)
				succesCroissance++;
		}
		if (succesCroissance > 0)
		{
			AgeEnJours += succesCroissance;
			ResistanceActuelle = ResistanceMaxPourAge(AgeEnJours);
			RegenererSelonLod(true);
		}
	}

	/// <summary>Applique des dégâts (minage avec pierre/silex). Loi du Rebond : force sous le seuil = zéro dégât.</summary>
	/// <param name="pointImpactMonde">Point d'impact du rayon (en coordonnées monde).</param>
	/// <param name="directionFrappe">Direction de la frappe (pour faire basculer l'arbre ou la branche).</param>
	/// <param name="forceImpact">Force d'impact cinétique (masse × vitesse × tranchant).</param>
	/// <param name="epaisseurLame">Épaisseur de la lame (détermine si on peut entamer le tronc).</param>
	/// <param name="hachettePrimitive106">Hachette assemblée : contourne la limite « lame fine seulement » sur tronc mature et mord mieux sur gros fût.</param>
	/// <returns>0 = Rebond, 1 = Touché (tronc), 2 = Arbre abattu, 3 = Branche amputée.</returns>
	public int SubirDegats(Vector3 pointImpactMonde, Vector3 directionFrappe, float forceImpact, float epaisseurLame, bool hachettePrimitive106 = false)
	{
		// Jeunes arbres (tier 1–2) : seuil plus bas pour outils taillés / mains nues. Vieux : un peu plus d’inertie requise.
		float seuilRuptureBotanique = AgeEnJours <= 2
			? (20f + AgeEnJours * 12f)
			: (30f + AgeEnJours * 15f + 0.4f * AgeEnJours * AgeEnJours);
		if (hachettePrimitive106)
			seuilRuptureBotanique *= 0.82f;
		if (forceImpact < seuilRuptureBotanique)
			return 0;

		Vector3 hitLocal = GlobalTransform.AffineInverse() * pointImpactMonde;
		float distAxis = Mathf.Sqrt(hitLocal.X * hitLocal.X + hitLocal.Z * hitLocal.Z);
		// Hauteur réelle du tronc (segments « T ») : sinon l’ancienne formule sous-estime y → tronc trop « épais » → rebond sur 2e/3e tiers
		float hTronc = Mathf.Max(0.25f, _hauteurTroncTotale);
		float hNormTronc = Mathf.Clamp(hitLocal.Y / hTronc, 0f, 1f);
		float epaisseurTronc = 0.2f * (1f - hNormTronc * 0.6f) * (AgeEnJours * 0.5f);

		// Les 3 premiers tiers du tronc : zone d’axe élargie pour pouvoir entailler toute la hauteur utile du fût
		float rayonAxe = hNormTronc < 1f / 3f ? 0.52f : (hNormTronc < 2f / 3f ? 0.46f : 0.40f);
		bool estLeTronc = hitLocal.Y <= hTronc * 1.05f && distAxis < rayonAxe;
		float epaisseurEstimee = estLeTronc ? epaisseurTronc : 0.05f;

		// Roc / lame très épaisse : pas de taille fine sur tronc mature — sauf vraie hachette (tranchant + masse).
		if (!hachettePrimitive106 && AgeEnJours >= 3 && epaisseurLame > 0.05f)
			return 0;

		if (!hachettePrimitive106 && epaisseurEstimee > epaisseurLame * 4.0f && epaisseurLame > 0.04f)
			return 0;

		if (estLeTronc)
		{
			float multiplicateur = 0.12f / Mathf.Max(0.01f, epaisseurEstimee);
			if (hachettePrimitive106)
				multiplicateur *= 1.22f;
			ResistanceActuelle -= forceImpact * Mathf.Clamp(multiplicateur, 0.1f, 5f);
			if (ResistanceActuelle <= 0f)
			{
				DeclencherChuteArbre(directionFrappe);
				return 2;
			}
			return 1;
		}
		else
		{
			_coupesLocales.Add(hitLocal);
			GenererMaillageArbre();
			DeclencherChuteBranche(pointImpactMonde, directionFrappe);
			return 3;
		}
	}

	private void DeclencherChuteArbre(Vector3 directionFrappe)
	{
		Transform3D poseArbre = GlobalTransform;
		RigidBody3D cadavre = new RigidBody3D { Name = "ArbreMort" };
		cadavre.Mass = 50f + (AgeEnJours * 80f);
		cadavre.ContinuousCd = true;
		cadavre.CollisionLayer = 1;
		cadavre.CollisionMask = 1;
		cadavre.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.08f };
		cadavre.SetMeta("PV", 60f * AgeEnJours);
		cadavre.SetMeta("Age", AgeEnJours);
		cadavre.SetMeta("HauteurTronc", _hauteurTroncTotale);
		cadavre.SetMeta("RayonTroncBase", _rayonTroncBase);
		cadavre.SetMeta("RayonTroncSommet", _rayonTroncSommet);
		cadavre.SetMeta("LongueurBrancheMoy", _longueurBrancheMoyenne);
		cadavre.SetMeta("EpaisseurBrancheMoy", _epaisseurBrancheMoyenne);
		// Essence de l'arbre (chêne/bouleau) conservée sur le cadavre.
		cadavre.SetMeta("IndexBotanique", (int)IndexBotanique);
		int segmentsTronc = Mathf.Max(1, Mathf.CeilToInt(_hauteurTroncTotale / 1.0f));
		cadavre.SetMeta("SegmentsRestants", segmentsTronc);
		cadavre.SetMeta("SegmentsInitiaux", segmentsTronc);
		cadavre.SetMeta("BranchesRestantes", 2 + AgeEnJours);

		cadavre.AngularDampMode = RigidBody3D.DampMode.Replace;
		cadavre.AngularDamp = 4.0f;
		cadavre.LinearDampMode = RigidBody3D.DampMode.Replace;
		cadavre.LinearDamp = 1.0f;

		MeshInstance3D boisCopy = new MeshInstance3D { Name = "Bois", Mesh = _visuelBois.Mesh, MaterialOverride = _visuelBois.MaterialOverride };
		MeshInstance3D feuillesCopy = new MeshInstance3D { Name = "Feuillage", Mesh = _visuelFeuillage.Mesh, MaterialOverride = _visuelFeuillage.MaterialOverride };
		cadavre.AddChild(boisCopy);
		cadavre.AddChild(feuillesCopy);

		CollisionShape3D hitboxCopy = new CollisionShape3D();
		Mesh meshArbre = _visuelBois.Mesh;

		if (AgeEnJours > 10)
		{
			GD.Print("ZERO-K : Arbre titanesque détecté. Utilisation d'un cylindre de collision pour éviter l'effondrement quantique.");
			var cylindreDeSecours = new CylinderShape3D
			{
				Radius = 0.2f + (AgeEnJours * 0.05f),
				Height = 1.0f + (AgeEnJours * 0.5f)
			};
			hitboxCopy.Shape = cylindreDeSecours;
			hitboxCopy.Position = new Vector3(0, cylindreDeSecours.Height / 2f, 0);
		}
		else
		{
			Shape3D choix = null;
			if (meshArbre != null)
			{
				try
				{
					if (meshArbre.GetFaces().Length > 0)
						choix = meshArbre.CreateConvexShape(true, true);
				}
				catch (Exception ex)
				{
					GD.PrintErr($"ZERO-K : Convex arbre échoué ({ex.Message}). Boîte englobante de secours.");
					choix = null;
				}
			}
			if (choix == null)
				choix = ItemPhysique.CreerShapeCollisionConvexeRobuste(meshArbre);
			hitboxCopy.Shape = choix;
		}

		cadavre.AddChild(hitboxCopy);

		GetParent().AddChild(cadavre);
		cadavre.GlobalTransform = poseArbre;
		cadavre.GlobalPosition += Vector3.Up * 0.24f;
		cadavre.ApplyCentralImpulse(directionFrappe * (40f * AgeEnJours) + Vector3.Up * 20f);

		QueueFree();
	}

	/// <summary>L’impact est souvent dans le volume du feuillage/tronc : repousser depuis la racine puis garder au-dessus du sol.</summary>
	private Vector3 CalculerPositionSpawnBranche(Vector3 pointImpact, Vector3 directionFrappe)
	{
		Vector3 depuisRacine = pointImpact - GlobalPosition;
		if (depuisRacine.LengthSquared() < 1e-4f)
			depuisRacine = directionFrappe.LengthSquared() > 1e-4f ? directionFrappe.Normalized() : Vector3.Up;
		else
			depuisRacine = depuisRacine.Normalized();
		Vector3 candidat = pointImpact + depuisRacine * 0.48f + Vector3.Up * 0.22f;
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return candidat;
		Vector3 haut = new Vector3(candidat.X, candidat.Y + 8f, candidat.Z);
		Vector3 bas = new Vector3(candidat.X, candidat.Y - 25f, candidat.Z);
		var q = PhysicsRayQueryParameters3D.Create(haut, bas);
		q.CollisionMask = 1;
		var hit = space.IntersectRay(q);
		if (hit.Count > 0 && hit.ContainsKey("position"))
		{
			Vector3 sol = hit["position"].AsVector3();
			Vector3 n = hit.ContainsKey("normal") ? hit["normal"].AsVector3().Normalized() : Vector3.Up;
			const float minAuDessus = 0.32f;
			float d = n.Dot(candidat - sol);
			if (d < minAuDessus)
				candidat = sol + n * minAuDessus + depuisRacine * 0.12f;
		}
		return candidat;
	}

	private void DeclencherChuteBranche(Vector3 pointImpact, Vector3 directionFrappe)
	{
		RigidBody3D brancheMorte = new RigidBody3D { Name = "BrancheMorte" };
		float volBr = Mathf.Pi * 0.05f * 0.05f * 0.8f;
		brancheMorte.Mass = Mathf.Max(0.2f, volBr * 500f);
		brancheMorte.ContinuousCd = true;
		brancheMorte.CollisionLayer = 1;
		brancheMorte.CollisionMask = 1;
		brancheMorte.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.78f, Bounce = 0.14f };
		brancheMorte.LinearDampMode = RigidBody3D.DampMode.Replace;
		brancheMorte.LinearDamp = 0.08f;
		brancheMorte.AngularDampMode = RigidBody3D.DampMode.Replace;
		brancheMorte.AngularDamp = 0.35f;

		brancheMorte.AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.05f, Height = 0.8f },
			MaterialOverride = _visuelBois.MaterialOverride,
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0)
		});
		brancheMorte.AddChild(new CollisionShape3D
		{
			Shape = new CylinderShape3D { Radius = 0.05f, Height = 0.8f },
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0)
		});

		GetParent().AddChild(brancheMorte);
		brancheMorte.GlobalPosition = CalculerPositionSpawnBranche(pointImpact, directionFrappe);
		brancheMorte.ApplyCentralImpulse(directionFrappe * 5f);
	}

	private static float Hash(uint seed, int salt)
	{
		uint h = (seed * 73856093u) ^ (uint)(salt * 19349663);
		return ((h % 10000) / 10000f);
	}

	private Vector3 PositionObservation()
	{
		if (_observationRef == null || !GodotObject.IsInstanceValid(_observationRef))
		{
			Node scene = GetTree()?.CurrentScene;
			_observationRef = scene?.GetNodeOrNull<Node3D>("Joueur/Camera3D")
				?? scene?.GetNodeOrNull<Node3D>("Joueur");
		}
		return _observationRef != null && GodotObject.IsInstanceValid(_observationRef)
			? _observationRef.GlobalPosition
			: GlobalPosition;
	}

	private int EvaluerLodDistance(float distance)
	{
		// Hystérésis légère pour éviter les regen en boucle sur les frontières.
		if (_lodActuel == 0)
			return distance <= DISTANCE_LOD0 + 8f ? 0 : (distance < DISTANCE_LOD1 ? 1 : (distance < DISTANCE_LOD2 ? 2 : 2));
		if (_lodActuel == 1)
			return distance < DISTANCE_LOD0 - 5f ? 0 : (distance <= DISTANCE_LOD1 + 10f ? 1 : 2);
		if (_lodActuel == 2)
			return distance < DISTANCE_LOD1 - 8f ? 1 : 2;
		return distance < DISTANCE_LOD0 ? 0 : (distance < DISTANCE_LOD1 ? 1 : 2);
	}

	private void RegenererSelonLod(bool forcer)
	{
		float distance = GlobalPosition.DistanceTo(PositionObservation());
		int lod = EvaluerLodDistance(distance);
		if (!forcer && lod == _lodActuel) return;
		_lodActuel = lod;
		GenererMaillageArbre(_lodActuel);
		_maillageInitialGenere = true;
	}

	private void GenererMaillageArbre(int lodNiveau = 0)
	{
		lodNiveau = Mathf.Clamp(lodNiveau, 0, 2);
		float facteurFeuillesLod = lodNiveau == 0 ? 1f : (lodNiveau == 1 ? 0.62f : 0.20f);
		float facteurAiguillesLod = lodNiveau == 0 ? 1f : (lodNiveau == 1 ? 0.60f : 0.18f);
		_lodFeuillageActuel = facteurFeuillesLod;

		int iter = IndexBotanique switch
		{
			LSystem_Botanique.IndexPin => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 4)),
			LSystem_Botanique.IndexBouleau => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 3)),
			_ => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 3))
		};
		if (lodNiveau >= 1) iter = Mathf.Max(1, iter - 1);

		string adnFinal = "";
		const int maxAdnLen = 18000;
		for (;;)
		{
			adnFinal = IndexBotanique switch
			{
				LSystem_Botanique.IndexPin => LSystem_Botanique.GenererChainePinOrganique(iter, SeedForme),
				LSystem_Botanique.IndexBouleau => LSystem_Botanique.GenererChaineBouleauOrganique(iter, SeedForme),
				_ => LSystem_Botanique.GenererChaineCheneOrganique(iter, SeedForme)
			};
			if (adnFinal.Length <= maxAdnLen || iter <= 2) break;
			iter--;
		}

		var stBois = new SurfaceTool();
		stBois.Begin(Mesh.PrimitiveType.Triangles);

		var stFeuilles = new SurfaceTool();
		stFeuilles.Begin(Mesh.PrimitiveType.Triangles);
		Color couleurFeuillage = CouleurFeuillesArbre();
		// Couleur portée par les vertex + matériau partagé = beaucoup moins de draw overhead.
		stFeuilles.SetColor(couleurFeuillage);

		Stack<TortueEtat> pile = new Stack<TortueEtat>();
		Transform3D tortue = Transform3D.Identity;

		float angleBase = IndexBotanique switch { LSystem_Botanique.IndexPin => 80f, LSystem_Botanique.IndexBouleau => 20f, _ => 35f };
		float angle = Mathf.DegToRad(angleBase + Hash(SeedForme, 0) * 14f);
		float multEpaisseur = 0.75f + Hash(SeedForme, 1) * 0.5f;
		float multLongueur = 0.8f + Hash(SeedForme, 2) * 0.6f;
		float reductionBranche = 0.72f + Hash(SeedForme, 3) * 0.18f;
		// Bébé arbres (1-2) plus petits ; matures (5+) plus grands
		float scaleAge = AgeEnJours <= 2 ? 0.4f + 0.2f * AgeEnJours : 1f;
		float epaisseurBase = (0.12f + 0.06f * AgeEnJours) * multEpaisseur * scaleAge;
		float longueurSegment = (0.6f + AgeEnJours * 0.18f) * multLongueur * scaleAge;
		if (IndexBotanique == LSystem_Botanique.IndexBouleau)
		{
			// Bouleau plus fin, moins "totem", tronc un peu élancé.
			epaisseurBase *= 0.72f;
			// Etages de branches plus rapproches.
			longueurSegment *= 0.56f;
			reductionBranche = 0.78f + Hash(SeedForme, 3) * 0.10f;
		}
		else if (IndexBotanique == LSystem_Botanique.IndexChene)
		{
			// Chêne moins étiré: un peu plus trapu.
			epaisseurBase *= 1.08f;
			longueurSegment *= 0.74f;
			reductionBranche = 0.76f + Hash(SeedForme, 3) * 0.10f;
		}
		else if (IndexBotanique == LSystem_Botanique.IndexPin)
		{
			// Pin: tronc élancé + conicité marquée.
			epaisseurBase *= 0.58f;
			longueurSegment *= 0.52f;
			reductionBranche = 0.86f + Hash(SeedForme, 3) * 0.06f;
		}

		_hauteurTroncTotale = 0f;
		float epaisseurBaseInitiale = epaisseurBase;
		_rayonTroncBase = epaisseurBase;
		_rayonTroncSommet = epaisseurBase;
		float sommeLongueurBranche = 0f;
		float sommeEpaisseurBranche = 0f;
		int nbSegmentsBranche = 0;

		bool premierSegmentDeBranche = false;
		bool estCoupe = false;
		int compteurJitterYaw = 0;
		int compteurJitterPitch = 0;
		foreach (char commande in adnFinal)
		{
			switch (commande)
			{
				case 'T':
					// TRONC ABSOLU : montée verticale pure, pas de feuilles
				{
					Vector3 pStart = tortue.Origin;
					Vector3 right = tortue.Basis.X.Normalized();
					Vector3 forward = tortue.Basis.Z.Normalized();
					tortue = tortue.TranslatedLocal(new Vector3(0, longueurSegment, 0));
					Vector3 pEnd = tortue.Origin;
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					_hauteurTroncTotale += longueurSegment;
					_rayonTroncSommet = rayonFin;
					if (!estCoupe)
					{
						foreach (Vector3 coupe in _coupesLocales)
						{
							if (pStart.DistanceTo(coupe) < 0.8f) { estCoupe = true; break; }
						}
					}
					if (!estCoupe)
					{
						GenererSegmentBranche(stBois, pStart, pEnd, right, forward, rayonDebut, rayonFin);
						// Pin: aiguilles denses sur le haut du tronc pour éviter un aspect nu.
						if (IndexBotanique == LSystem_Botanique.IndexPin && epaisseurBaseInitiale > 0.0001f)
						{
							float ratioTronc = Mathf.Clamp(rayonDebut / epaisseurBaseInitiale, 0f, 1f);
							if (ratioTronc < 0.78f)
								GenererAiguillesPin(stFeuilles, new Transform3D(tortue.Basis, pEnd), AgeEnJours + 2, 0.34f * facteurAiguillesLod);
						}
					}
					epaisseurBase = rayonFin;
					break;
				}
				case 'F':
				case 'b':
				case 'c':
					// BRANCHE ou sous-branche
				{
					Vector3 pStart = tortue.Origin;
					Vector3 right = tortue.Basis.X.Normalized();
					Vector3 forward = tortue.Basis.Z.Normalized();
					float longueurLocale = longueurSegment;
					if (IndexBotanique == LSystem_Botanique.IndexPin && epaisseurBaseInitiale > 0.0001f)
					{
						// Forme conique: branches basses plus longues, hautes plus courtes.
						float ratioHauteur = Mathf.Clamp(epaisseurBase / epaisseurBaseInitiale, 0.25f, 1.0f);
						float facteurCone = 0.24f + 0.20f * Mathf.Pow(ratioHauteur, 1.45f);
						longueurLocale *= facteurCone;
					}
					if (commande == 'b') longueurLocale *= (IndexBotanique == LSystem_Botanique.IndexPin ? 0.74f : 0.86f);
					else if (commande == 'c') longueurLocale *= (IndexBotanique == LSystem_Botanique.IndexPin ? 0.52f : 0.66f);
					tortue = tortue.TranslatedLocal(new Vector3(0, longueurLocale, 0));
					Vector3 pEnd = tortue.Origin;
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					if (premierSegmentDeBranche)
					{
						rayonDebut = epaisseurBase * 1.12f;
						premierSegmentDeBranche = false;
					}
					float coef = commande == 'b' ? 0.7f : (commande == 'c' ? 0.52f : 1f);
					nbSegmentsBranche++;
					sommeLongueurBranche += longueurLocale;
					sommeEpaisseurBranche += (rayonDebut + rayonFin) * 0.5f * coef;
					if (!estCoupe)
					{
						foreach (Vector3 coupe in _coupesLocales)
						{
							if (pStart.DistanceTo(coupe) < 0.8f) { estCoupe = true; break; }
						}
					}
					if (!estCoupe)
					{
						GenererSegmentBranche(stBois, pStart, pEnd, right, forward, rayonDebut * coef, rayonFin * coef);
						if (pile.Count > 0)
						{
							int hashBase = Mathf.Abs((int)(pStart.X * 7 + pStart.Z * 31 + pStart.Y * 13));
							Transform3D tStart = new Transform3D(tortue.Basis, pStart);
							Transform3D tMid = new Transform3D(tortue.Basis, pStart.Lerp(pEnd, 0.5f));
							Transform3D tEnd = new Transform3D(tortue.Basis, pEnd);
							if (IndexBotanique == LSystem_Botanique.IndexPin)
							{
								// Pin: aiguilles denses (pas de larges feuilles).
								GenererAiguillesPin(stFeuilles, tStart, AgeEnJours + 2, 0.58f * facteurAiguillesLod);
								if (Hash(SeedForme, hashBase + 91) < 0.30f) GenererAiguillesPin(stFeuilles, tMid, AgeEnJours + 2, 0.66f * facteurAiguillesLod);
								GenererAiguillesPin(stFeuilles, tEnd, AgeEnJours + 2, 0.78f * facteurAiguillesLod);
							}
							else
							{
								float ratioBranche = epaisseurBaseInitiale > 0.0001f
									? Mathf.Clamp(((rayonDebut + rayonFin) * 0.5f) / epaisseurBaseInitiale, 0.20f, 1f)
									: 0.55f;
								float tailleBase = (0.70f + ratioBranche * 0.95f) * (0.86f + 0.14f * facteurFeuillesLod);
								GenererFeuillagePetit(stFeuilles, tStart, AgeEnJours, tailleBase * 1.10f, facteurFeuillesLod);
								GenererFeuillagePetit(stFeuilles, tMid, AgeEnJours, tailleBase * 0.92f, facteurFeuillesLod);
								GenererFeuillagePetit(stFeuilles, tEnd, AgeEnJours, tailleBase * 0.78f, facteurFeuillesLod);
							}
						}
					}
					epaisseurBase = rayonFin * coef;
					break;
				}
				case '[':
					pile.Push(new TortueEtat { Transform = tortue, Epaisseur = epaisseurBase, EstCoupe = estCoupe });
					premierSegmentDeBranche = true;
					break;
				case ']':
				{
					TortueEtat etat = pile.Pop();
					tortue = etat.Transform;
					epaisseurBase = etat.Epaisseur;
					estCoupe = etat.EstCoupe;
					break;
				}
				case '+': tortue = tortue.RotatedLocal(Vector3.Right, angle); break;
				case '-': tortue = tortue.RotatedLocal(Vector3.Right, -angle); break;
				case 'R': tortue = tortue.RotatedLocal(Vector3.Up, Mathf.Pi / 4f); break;
				case 'r': tortue = tortue.RotatedLocal(Vector3.Up, -Mathf.Pi / 4f); break;
				case 'j':
				{
					// Jitter déterministe pour casser les couronnes parfaitement symétriques.
					float amp = IndexBotanique == LSystem_Botanique.IndexPin ? 18f : 10f;
					float v = Hash(SeedForme, 10000 + compteurJitterYaw++);
					float yaw = Mathf.DegToRad((v * 2f - 1f) * amp);
					tortue = tortue.RotatedLocal(Vector3.Up, yaw);
					break;
				}
				case 'v':
				{
					float amp = IndexBotanique == LSystem_Botanique.IndexPin ? 4f : 6f;
					float v = Hash(SeedForme, 11000 + compteurJitterPitch++);
					float pitch = Mathf.DegToRad((v * 2f - 1f) * amp);
					tortue = tortue.RotatedLocal(Vector3.Right, pitch);
					break;
				}
				case '>': tortue = tortue.RotatedLocal(Vector3.Forward, angle); break;
				case '<': tortue = tortue.RotatedLocal(Vector3.Forward, -angle); break;
				case 'A':
				case 'B':
					break;
				case 'L':
					if (!estCoupe)
					{
						if (IndexBotanique == LSystem_Botanique.IndexPin) GenererAiguillesPin(stFeuilles, tortue, AgeEnJours + 2, 0.88f * facteurAiguillesLod);
						else GenererFeuillage(stFeuilles, tortue, AgeEnJours, 0.95f, facteurFeuillesLod);
					}
					break;
			}
		}
		_longueurBrancheMoyenne = nbSegmentsBranche > 0 ? sommeLongueurBranche / nbSegmentsBranche : longueurSegment;
		_epaisseurBrancheMoyenne = nbSegmentsBranche > 0 ? sommeEpaisseurBranche / nbSegmentsBranche : (epaisseurBase * 0.7f);

		if (!estCoupe)
		{
			// Évite l'effet "sucette" au sommet du bouleau.
			if (IndexBotanique == LSystem_Botanique.IndexBouleau || IndexBotanique == LSystem_Botanique.IndexPin)
				if (IndexBotanique == LSystem_Botanique.IndexPin) GenererAiguillesPin(stFeuilles, tortue, AgeEnJours + 2, 0.95f * facteurAiguillesLod);
				else GenererFeuillagePetit(stFeuilles, tortue, Mathf.Max(1, AgeEnJours - 1), 0.82f, facteurFeuillesLod);
			else GenererFeuillage(stFeuilles, tortue, AgeEnJours, 0.92f, facteurFeuillesLod);
		}

		stBois.GenerateNormals();
		// Pas de GenerateTangents (nécessite UV parfaits, inutile sans normal map)
		Mesh meshBois = stBois.Commit();
		_visuelBois.Mesh = meshBois;
		_visuelBois.MaterialOverride = ObtenirMaterielBois(IndexBotanique);

		stFeuilles.GenerateNormals();
		_visuelFeuillage.Mesh = stFeuilles.Commit();
		_visuelFeuillage.MaterialOverride = ObtenirMaterielFeuilles(IndexBotanique);

		// FIX CRITIQUE : Hitbox exacte pour permettre de marcher sous les branches et viser le tronc.
		_hitbox.Shape = meshBois != null && meshBois.GetFaces().Length > 0
			? meshBois.CreateTrimeshShape()
			: new BoxShape3D { Size = Vector3.One };
	}

	/// <summary>Segment cylindrique avec conicité (rayonStart → rayonEnd) pour transition douce, sans saut.</summary>
	private void GenererSegmentBranche(SurfaceTool st, Vector3 start, Vector3 end, Vector3 right, Vector3 forward, float rayonStart, float rayonEnd)
	{
		const int cotes = 8;
		Vector3[] pStart = new Vector3[cotes];
		Vector3[] pEnd = new Vector3[cotes];
		for (int i = 0; i < cotes; i++)
		{
			float a = (float)i / cotes * Mathf.Tau;
			Vector3 dir = (Mathf.Cos(a) * right + Mathf.Sin(a) * forward);
			pStart[i] = start + dir * rayonStart;
			pEnd[i] = end + dir * rayonEnd;
		}
		for (int i = 0; i < cotes; i++)
		{
			int n = (i + 1) % cotes;
			st.SetUV(new Vector2((float)i / cotes, 0)); st.AddVertex(pStart[i]);
			st.SetUV(new Vector2((float)n / cotes, 0)); st.AddVertex(pStart[n]);
			st.SetUV(new Vector2((float)n / cotes, 1)); st.AddVertex(pEnd[n]);

			st.SetUV(new Vector2((float)i / cotes, 0)); st.AddVertex(pStart[i]);
			st.SetUV(new Vector2((float)n / cotes, 1)); st.AddVertex(pEnd[n]);
			st.SetUV(new Vector2((float)i / cotes, 1)); st.AddVertex(pEnd[i]);
		}
	}

	/// <summary>Cluster de feuillage le long des branches — formes ovales, densité adaptée au rayon.</summary>
	private void GenererFeuillagePetit(SurfaceTool st, Transform3D tortue, int age, float tailleMul = 1f, float lodMul = 1f)
	{
		GenererFeuillesCaducTypePin(st, tortue, age, 0.40f * lodMul, tailleMul);
	}

	/// <summary>Aiguilles de pin: petits quads étroits orientés radialement, très denses.</summary>
	private void GenererAiguillesPin(SurfaceTool st, Transform3D tortue, int age, float densiteMul = 1f)
	{
		Vector3 centre = tortue.Origin;
		Vector3 axe = tortue.Basis.Y.Normalized();
		Vector3 refPerp = tortue.Basis.X.Normalized();
		if (Mathf.Abs(refPerp.Dot(axe)) > 0.95f) refPerp = tortue.Basis.Z.Normalized();
		refPerp = (refPerp - axe * axe.Dot(refPerp)).Normalized();

		// Pin optimisé: moins d'instances, mais aiguilles un peu plus grandes.
		int nPoints = Mathf.Clamp((int)((8 + age * 2) * densiteMul), 5, 14);
		float rayonBranche = 0.060f + Mathf.Clamp(age * 0.008f, 0f, 0.05f);
		float demiLongueurSpan = 0.11f + Mathf.Clamp(age * 0.015f, 0f, 0.10f);
		for (int i = 0; i < nPoints; i++)
		{
			float theta = (float)i / nPoints * Mathf.Tau + Hash(SeedForme, 2200 + i) * 0.35f;
			Vector3 radial = (refPerp.Rotated(axe, theta)).Normalized();
			float offsetAxe = (Hash(SeedForme, 2300 + i) * 2f - 1f) * demiLongueurSpan;
			// Point d'ancrage SUR la branche (évite l'effet "nuage qui flotte").
			Vector3 ancre = centre + axe * offsetAxe + radial * rayonBranche;

			// Aiguilles plus épaisses et longues (x2 env.), sortie depuis l'ancre.
			float largeur = 0.050f + Hash(SeedForme, 2400 + i) * 0.036f;
			float longueur = 0.42f + Hash(SeedForme, 2500 + i) * 0.18f;
			Vector3 dir = (radial * 0.88f - axe * 0.22f).Normalized();
			Vector3 tangent = dir.Cross(axe);
			if (tangent.LengthSquared() < 1e-5f) tangent = dir.Cross(refPerp);
			tangent = tangent.Normalized();

			Vector3 baseG = ancre - tangent * largeur;
			Vector3 baseD = ancre + tangent * largeur;
			Vector3 tipC = ancre + dir * longueur;
			Vector3 n = dir;

			st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(baseG);
			st.SetNormal(n); st.SetUV(new Vector2(1, 1)); st.AddVertex(baseD);
			st.SetNormal(n); st.SetUV(new Vector2(0.5f, 0)); st.AddVertex(tipC);
		}
	}

	/// <summary>Feuilles de chêne/bouleau distribuées comme le pin, mais en lamelles larges (pas d'épines).</summary>
	private void GenererFeuillesCaducTypePin(SurfaceTool st, Transform3D tortue, int age, float densiteMul = 1f, float tailleMul = 1f)
	{
		Vector3 centre = tortue.Origin;
		Vector3 axe = tortue.Basis.Y.Normalized();
		Vector3 refPerp = tortue.Basis.X.Normalized();
		if (Mathf.Abs(refPerp.Dot(axe)) > 0.95f) refPerp = tortue.Basis.Z.Normalized();
		refPerp = (refPerp - axe * axe.Dot(refPerp)).Normalized();
		bool estChene = IndexBotanique == LSystem_Botanique.IndexChene;
		float echelle = Mathf.Clamp(tailleMul, 0.55f, 1.95f);
		float coefLargeur = (estChene ? 1.18f : 1.08f) * echelle;
		float coefLongueur = (estChene ? 0.78f : 0.86f) * echelle;
		float rayonSphere = ((estChene ? 0.44f : 0.38f) * echelle + Mathf.Clamp(age * 0.015f, 0f, 0.10f)) * 1.90f;
		int couches = estChene ? 4 : 3;
		if (_lodFeuillageActuel < 0.70f) couches -= 1;
		if (_lodFeuillageActuel < 0.40f) couches -= 1;
		couches = Mathf.Clamp(couches, 1, 4);
		int pointsBase = Mathf.Clamp((int)((8 + age * 2) * densiteMul * (0.85f + echelle * 0.45f) * 1.35f), 5, 24);

		// Amas sphériques de cartes-feuilles: plus denses et volumétriques.
		for (int couche = 0; couche < couches; couche++)
		{
			int points = Mathf.Clamp((int)(pointsBase * (0.78f + couche * 0.22f)), 5, 20);
			float rayonCouche = rayonSphere * (0.50f + couche * 0.33f);
			float yawBase = Hash(SeedForme, 3200 + couche * 97) * Mathf.Tau;
			for (int i = 0; i < points; i++)
			{
				float t = (i + 0.5f) / points;
				float y = 1f - 2f * t;
				float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
				float theta = i * 2.3999632f + yawBase + Hash(SeedForme, 3300 + couche * 41 + i) * 0.35f;
				Vector3 local = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
				Vector3 dir = (tortue.Basis * local).Normalized();
				Vector3 ancre = centre + dir * rayonCouche;

				float largeur = (0.52f + Hash(SeedForme, 3700 + couche * 113 + i) * 0.22f) * coefLargeur;
				float longueur = (0.40f + Hash(SeedForme, 3800 + couche * 131 + i) * 0.16f) * coefLongueur;
				AjouterTouffeFeuilleTypeBuisson(st, ancre, dir, axe, refPerp, largeur, longueur, 5000 + couche * 257 + i * 31, centre, rayonSphere * 1.12f);

				// Petit amas dans le gros (volume plus naturel) sans le faire partout pour contenir le coût.
				if (_lodFeuillageActuel > 0.75f && Hash(SeedForme, 8600 + couche * 181 + i) > 0.74f)
				{
					Vector3 ancreInterne = ancre - dir * (rayonCouche * 0.24f);
					AjouterTouffeFeuilleTypeBuisson(
						st,
						ancreInterne,
						dir,
						axe,
						refPerp,
						largeur * 0.62f,
						longueur * 0.60f,
						9000 + couche * 293 + i * 47,
						centre,
						rayonSphere * 1.12f
					);
				}
			}
		}
	}

	private void AjouterTouffeFeuilleTypeBuisson(
		SurfaceTool st,
		Vector3 ancre,
		Vector3 dir,
		Vector3 axeRef,
		Vector3 axeFallback,
		float largeur,
		float longueur,
		int saltBase,
		Vector3 centreCouronne,
		float rayonMaxCouronne)
	{
		// 1 à 2 cartes "blob", chacune en double couche (grande + petite).
		float seuilDouble = _lodFeuillageActuel > 0.75f ? 0.58f : (_lodFeuillageActuel > 0.45f ? 0.78f : 0.95f);
		int feuilles = 1; // Optimisation: moitié moins de micro-cartes, compensée par des feuilles plus grandes.
		for (int i = 0; i < feuilles; i++)
		{
			float yaw = Hash(SeedForme, saltBase + 17 + i) * Mathf.Tau;
			float pitch = (Hash(SeedForme, saltBase + 31 + i) * 2f - 1f) * 0.85f;
			Vector3 dirLeaf = dir.Rotated(axeRef, yaw);
			Vector3 sideAxis = dirLeaf.Cross(axeRef);
			if (sideAxis.LengthSquared() < 1e-5f) sideAxis = dirLeaf.Cross(axeFallback);
			if (sideAxis.LengthSquared() < 1e-5f) sideAxis = dirLeaf.Cross(Vector3.Right);
			sideAxis = sideAxis.Normalized();
			dirLeaf = dirLeaf.Rotated(sideAxis, pitch).Normalized();

			Vector3 upAxis = dirLeaf.Cross(sideAxis);
			if (upAxis.LengthSquared() < 1e-5f) upAxis = axeRef;
			upAxis = upAxis.Normalized();

			float w = largeur * (0.90f + Hash(SeedForme, saltBase + 47 + i) * 0.34f);
			float l = longueur * (0.88f + Hash(SeedForme, saltBase + 59 + i) * 0.30f);
			Vector3 centre =
				ancre
				+ sideAxis * ((Hash(SeedForme, saltBase + 71 + i) * 2f - 1f) * 0.07f)
				+ upAxis * ((Hash(SeedForme, saltBase + 79 + i) * 2f - 1f) * 0.06f);

			// Anti-feuilles volantes: clamp strict dans le volume de couronne.
			float demiDiag = Mathf.Max(w * 0.5f, l * 0.74f);
			Vector3 radial = centre - centreCouronne;
			float dist = radial.Length();
			if (dist + demiDiag > rayonMaxCouronne)
			{
				if (dist < 1e-4f) radial = dir;
				float cible = Mathf.Max(0f, rayonMaxCouronne - demiDiag * 0.92f);
				centre = centreCouronne + radial.Normalized() * cible;
			}

			// Couche principale (large) + couche secondaire (plus petite) pour un rendu "balle de feuilles".
			AjouterFeuillePerforeeTypeBuisson(st, centre, dirLeaf, axeRef, axeFallback, w, l);
			bool autoriser2e = _lodFeuillageActuel > 0.78f || (_lodFeuillageActuel > 0.42f && Hash(SeedForme, saltBase + 91 + i) > 0.62f);
			if (autoriser2e)
			{
				Vector3 dirLeaf2 = dirLeaf.Rotated(sideAxis, (Hash(SeedForme, saltBase + 97 + i) * 2f - 1f) * 0.45f).Normalized();
				AjouterFeuillePerforeeTypeBuisson(st, centre, dirLeaf2, sideAxis, axeRef, w * 0.72f, l * 0.66f);
			}
		}
	}

	private static void AjouterFeuillePerforeeTypeBuisson(SurfaceTool st, Vector3 centre, Vector3 dir, Vector3 axeRef, Vector3 axeFallback, float largeur, float longueur)
	{
		Vector3 droite = dir.Cross(axeRef);
		if (droite.LengthSquared() < 1e-5f) droite = dir.Cross(axeFallback);
		if (droite.LengthSquared() < 1e-5f) droite = dir.Cross(Vector3.Right);
		droite = droite.Normalized() * (largeur * 0.5f);
		Vector3 levee = dir * longueur;
		Vector3 pointes = levee * 0.50f;

		Vector3 departL = centre - droite;
		Vector3 departR = centre + droite;
		Vector3 milieuL = centre - droite * 0.62f + levee * 0.24f;
		Vector3 milieuR = centre + droite * 0.62f + levee * 0.24f;
		Vector3 sommetL = centre - droite * 0.24f + pointes;
		Vector3 sommetR = centre + droite * 0.24f + pointes;
		Vector3 sommetC = centre + levee * 0.74f;

		// Face avant
		st.SetNormal(dir); st.SetUV(new Vector2(0, 1)); st.AddVertex(departL);
		st.SetNormal(dir); st.SetUV(new Vector2(0.45f, 0.45f)); st.AddVertex(milieuL);
		st.SetNormal(dir); st.SetUV(new Vector2(0.35f, 0)); st.AddVertex(sommetL);
		st.SetNormal(dir); st.SetUV(new Vector2(1, 1)); st.AddVertex(departR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.65f, 0)); st.AddVertex(sommetR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.55f, 0.45f)); st.AddVertex(milieuR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.45f, 0.45f)); st.AddVertex(milieuL);
		st.SetNormal(dir); st.SetUV(new Vector2(0.55f, 0.45f)); st.AddVertex(milieuR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.50f, 0.02f)); st.AddVertex(sommetC);

		// Pas de face arrière dupliquée: culling désactivé suffit, moitié moins de triangles.
	}

	/// <summary>Sphère de feuillage AAA : densité proportionnelle au rayon (pas de vide), feuilles ovales.</summary>
	private void GenererFeuillage(SurfaceTool st, Transform3D tortue, int age, float tailleMul = 1f, float lodMul = 1f)
	{
		float densite = (0.45f + Mathf.Clamp(age - 2, 0, 4) * 0.08f) * lodMul * 0.5f;
		GenererFeuillesCaducTypePin(st, tortue, age, densite, tailleMul * 2f);
	}

	private Color CouleurFeuillesArbre()
	{
		if (IndexBotanique == LSystem_Botanique.IndexPin) return new Color(0.12f, 0.25f, 0.15f); // Aiguilles sombres
		float h = Hash(SeedForme, 10);
		float h2 = Hash(SeedForme, 11);
		// Feuilles caduques: base verte + légère dérive olive/jaune pour casser le "vert plein".
		float rBase = IndexBotanique == LSystem_Botanique.IndexBouleau ? 0.20f : 0.17f;
		float gBase = IndexBotanique == LSystem_Botanique.IndexBouleau ? 0.56f : 0.52f;
		float bBase = IndexBotanique == LSystem_Botanique.IndexBouleau ? 0.13f : 0.11f;
		float r = rBase + h * 0.12f;
		float g = gBase + h * 0.25f;
		float b = bBase + h2 * 0.10f;
		return new Color(r, g, b);
	}
}
