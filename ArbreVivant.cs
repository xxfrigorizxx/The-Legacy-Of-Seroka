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

	private static StandardMaterial3D _cacheMatBois;
	private static StandardMaterial3D _cacheMatBoisTriplanar;
	private static StandardMaterial3D _cacheMatBoisBatonChenEPale;
	private static StandardMaterial3D _cacheMatFeuilles;

	public static Material ObtenirMaterielBois()
	{
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
	public static Material ObtenirMaterielBoisTriplanar()
	{
		if (_cacheMatBoisTriplanar != null) return _cacheMatBoisTriplanar;
		ObtenirMaterielBois();
		_cacheMatBoisTriplanar = (StandardMaterial3D)_cacheMatBois.Duplicate();
		_cacheMatBoisTriplanar.Uv1Triplanar = true;
		_cacheMatBoisTriplanar.Uv1WorldTriplanar = true;
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

	private static Material ObtenirMaterielFeuilles()
	{
		if (_cacheMatFeuilles != null) return _cacheMatFeuilles;
		_cacheMatFeuilles = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.2f, 0.55f, 0.15f),
			Roughness = 0.95f,
			Metallic = 0f
		};
		return _cacheMatFeuilles;
	}

	public override void _Ready()
	{
		_visuelBois = new MeshInstance3D { Name = "Bois" };
		_visuelFeuillage = new MeshInstance3D { Name = "Feuillage" };
		_hitbox = new CollisionShape3D { Name = "Hitbox" };

		AddChild(_visuelBois);
		AddChild(_visuelFeuillage);
		AddChild(_hitbox);

		AddToGroup("Arbres");

		GenererMaillageArbre();
	}

	/// <summary>Appelé à minuit par le serveur (arbres dans chunks actifs). 1 chance sur 20 de grandir.</summary>
	public void VieillirUnJour()
	{
		if (GD.Randf() <= CHANCE_CROISSANCE)
		{
			AgeEnJours++;
			ResistanceActuelle = ResistanceMaxPourAge(AgeEnJours);
			GenererMaillageArbre();
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
			GenererMaillageArbre();
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
		// Essence (pour masse / résistance des bûches — aujourd’hui chêne ; même index que LSystem).
		cadavre.SetMeta("IndexBotanique", (int)LSystem_Botanique.IndexChene);
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

	private void GenererMaillageArbre()
	{
		// Chêne organique : variété, branches asymétriques (pas 4 angles fixes), sous-branches
		int iter = Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 6)); // Plus d'itérations = plus de ramification
		string adnFinal = LSystem_Botanique.GenererChaineCheneOrganique(iter, Seed);

		var stBois = new SurfaceTool();
		stBois.Begin(Mesh.PrimitiveType.Triangles);

		var stFeuilles = new SurfaceTool();
		stFeuilles.Begin(Mesh.PrimitiveType.Triangles);

		Stack<TortueEtat> pile = new Stack<TortueEtat>();
		Transform3D tortue = Transform3D.Identity;

		float angle = Mathf.DegToRad(35f + Hash(Seed, 0) * 25f);
		float multEpaisseur = 0.75f + Hash(Seed, 1) * 0.5f;
		float multLongueur = 0.8f + Hash(Seed, 2) * 0.6f;
		float reductionBranche = 0.72f + Hash(Seed, 3) * 0.18f;
		// Bébé arbres (1-2) plus petits ; matures (5+) plus grands
		float scaleAge = AgeEnJours <= 2 ? 0.4f + 0.2f * AgeEnJours : 1f;
		float epaisseurBase = (0.12f + 0.06f * AgeEnJours) * multEpaisseur * scaleAge;
		float longueurSegment = (0.6f + AgeEnJours * 0.18f) * multLongueur * scaleAge;

		_hauteurTroncTotale = 0f;
		_rayonTroncBase = epaisseurBase;
		_rayonTroncSommet = epaisseurBase;
		float sommeLongueurBranche = 0f;
		float sommeEpaisseurBranche = 0f;
		int nbSegmentsBranche = 0;

		bool premierSegmentDeBranche = false;
		bool estCoupe = false;
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
					}
					epaisseurBase = rayonFin;
					break;
				}
				case 'F':
				case 'b':
					// BRANCHE ou sous-branche
				{
					Vector3 pStart = tortue.Origin;
					Vector3 right = tortue.Basis.X.Normalized();
					Vector3 forward = tortue.Basis.Z.Normalized();
					tortue = tortue.TranslatedLocal(new Vector3(0, longueurSegment, 0));
					Vector3 pEnd = tortue.Origin;
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					if (premierSegmentDeBranche)
					{
						rayonDebut = epaisseurBase * 1.12f;
						premierSegmentDeBranche = false;
					}
					float coef = (commande == 'b') ? 0.7f : 1f;
					nbSegmentsBranche++;
					sommeLongueurBranche += longueurSegment;
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
							GenererFeuillagePetit(stFeuilles, tStart, AgeEnJours);
							if (Hash(Seed, hashBase) < 0.85f) GenererFeuillagePetit(stFeuilles, tMid, AgeEnJours);
							GenererFeuillagePetit(stFeuilles, tEnd, AgeEnJours);
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
				case '>': tortue = tortue.RotatedLocal(Vector3.Forward, angle); break;
				case '<': tortue = tortue.RotatedLocal(Vector3.Forward, -angle); break;
				case 'A':
				case 'B':
					break;
				case 'L':
					if (!estCoupe) GenererFeuillage(stFeuilles, tortue, AgeEnJours);
					break;
			}
		}
		_longueurBrancheMoyenne = nbSegmentsBranche > 0 ? sommeLongueurBranche / nbSegmentsBranche : longueurSegment;
		_epaisseurBrancheMoyenne = nbSegmentsBranche > 0 ? sommeEpaisseurBranche / nbSegmentsBranche : (epaisseurBase * 0.7f);

		if (!estCoupe) GenererFeuillage(stFeuilles, tortue, AgeEnJours);

		stBois.GenerateNormals();
		// Pas de GenerateTangents (nécessite UV parfaits, inutile sans normal map)
		Mesh meshBois = stBois.Commit();
		_visuelBois.Mesh = meshBois;
		_visuelBois.MaterialOverride = ObtenirMaterielBois();

		stFeuilles.GenerateNormals();
		_visuelFeuillage.Mesh = stFeuilles.Commit();
		Color vertFeuille = CouleurFeuillesArbre();
		var bruitFeuille = new FastNoiseLite { Seed = (int)(Seed + 5000) };
		bruitFeuille.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		bruitFeuille.Frequency = 0.12f;
		bruitFeuille.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		var texFeuille = new NoiseTexture2D { Width = 64, Height = 64, Noise = bruitFeuille };
		StandardMaterial3D matFeuille = new StandardMaterial3D
		{
			AlbedoColor = vertFeuille,
			AlbedoTexture = texFeuille,
			Roughness = 0.9f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		_visuelFeuillage.MaterialOverride = matFeuille;

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
	private void GenererFeuillagePetit(SurfaceTool st, Transform3D tortue, int age)
	{
		float rayon = 0.55f + age * 0.12f;
		Vector3 centre = tortue.Origin;
		// Plus de points quand le rayon augmente = pas de vide
		int nPoints = Mathf.Max(28, (int)(rayon * 45));
		for (int i = 0; i < nPoints; i++)
		{
			float phi = (float)(i % 8) / 8f * Mathf.Pi * 0.9f;
			float theta = (float)(i / 8) / 3f * Mathf.Tau;
			Vector3 dir = tortue.Basis * new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
			Vector3 pos = centre + dir * rayon;
			// Feuilles ovales (pas carrées) : largeur ≠ hauteur
			float largeur = (0.18f + Hash(Seed, i) * 0.12f);
			float hauteur = (0.28f + Hash(Seed, i + 100) * 0.12f);
			Vector3 right = (Mathf.Abs(dir.Dot(tortue.Basis.X)) < 0.9f ? tortue.Basis.X : tortue.Basis.Z);
			right = (right - dir * dir.Dot(right)).Normalized();
			Vector3 fwd = dir.Cross(right).Normalized();
			Vector3 p0 = pos - right * largeur - fwd * hauteur * 0.5f;
			Vector3 p1 = pos + right * largeur - fwd * hauteur * 0.5f;
			Vector3 p2 = pos + right * largeur + fwd * hauteur * 0.5f;
			Vector3 p3 = pos - right * largeur + fwd * hauteur * 0.5f;
			st.SetNormal(dir); st.SetUV(Vector2.Zero); st.AddVertex(p0);
			st.SetNormal(dir); st.SetUV(Vector2.Zero); st.AddVertex(p1);
			st.SetNormal(dir); st.SetUV(Vector2.Zero); st.AddVertex(p2);
			st.SetNormal(dir); st.SetUV(Vector2.Zero); st.AddVertex(p0);
			st.SetNormal(dir); st.SetUV(Vector2.Zero); st.AddVertex(p2);
			st.SetNormal(dir); st.SetUV(Vector2.Zero); st.AddVertex(p3);
		}
	}

	/// <summary>Sphère de feuillage AAA : densité proportionnelle au rayon (pas de vide), feuilles ovales.</summary>
	private void GenererFeuillage(SurfaceTool st, Transform3D tortue, int age)
	{
		float rayon = 1.0f + age * 0.35f;
		float variante = 0.9f + Hash(Seed, 4) * 0.25f;
		rayon *= variante;
		Vector3 centre = tortue.Origin;
		// Densité proportionnelle au rayon : grosse sphère = plus de quads, pas de vide
		int nRings = Mathf.Clamp((int)(rayon * 10), 12, 22);
		int nPerRing = Mathf.Clamp((int)(rayon * 12), 14, 28);
		int total = 0;
		for (int ring = 1; ring < nRings; ring++)
		{
			float phi = (float)ring / nRings * Mathf.Pi;
			int count = Mathf.Max(4, (int)(nPerRing * Mathf.Sin(phi)));
			float rRing = rayon * (0.5f + 0.5f * Hash(Seed, ring));
			for (int i = 0; i < count; i++)
			{
				float theta = (float)i / count * Mathf.Tau + Hash(Seed, ring * 100 + i) * 0.25f;
				Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
				dir = tortue.Basis * dir;
				Vector3 pos = centre + dir * rRing;
				// Feuilles ovales : largeur et hauteur différentes, forme organique
				float largeur = 0.22f + Hash(Seed, total) * 0.14f;
				float hauteur = 0.32f + Hash(Seed, total + 500) * 0.16f;
				Vector3 up = dir;
				Vector3 right = tortue.Basis.X;
				if (Mathf.Abs(up.Dot(right)) > 0.99f) right = tortue.Basis.Z;
				right = (right - up * up.Dot(right)).Normalized();
				Vector3 fwd = up.Cross(right).Normalized();
				Vector3 halfR = right * largeur;
				Vector3 halfF = fwd * hauteur;
				Vector3 p0 = pos - halfR - halfF;
				Vector3 p1 = pos + halfR - halfF;
				Vector3 p2 = pos + halfR + halfF;
				Vector3 p3 = pos - halfR + halfF;
				Vector3 n = up;
				st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
				st.SetNormal(n); st.SetUV(new Vector2(1, 1)); st.AddVertex(p1);
				st.SetNormal(n); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2);
				st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
				st.SetNormal(n); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2);
				st.SetNormal(n); st.SetUV(new Vector2(0, 0)); st.AddVertex(p3);
				total++;
			}
		}
		// Couche intérieure : comble le vide au centre pour un volume opaque
		for (int ring = 1; ring < nRings - 2; ring++)
		{
			float phi = (float)ring / nRings * Mathf.Pi;
			int count = Mathf.Max(4, (int)(nPerRing * 0.7f * Mathf.Sin(phi)));
			float rInner = rayon * (0.35f + 0.25f * Hash(Seed, ring + 1000));
			for (int i = 0; i < count; i++)
			{
				float theta = (float)i / count * Mathf.Tau + Hash(Seed, (ring + 1000) * 100 + i) * 0.2f;
				Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
				dir = tortue.Basis * dir;
				Vector3 pos = centre + dir * rInner;
				float largeur = 0.18f + Hash(Seed, total + 1000) * 0.1f;
				float hauteur = 0.26f + Hash(Seed, total + 1500) * 0.12f;
				Vector3 up = dir;
				Vector3 right = tortue.Basis.X;
				if (Mathf.Abs(up.Dot(right)) > 0.99f) right = tortue.Basis.Z;
				right = (right - up * up.Dot(right)).Normalized();
				Vector3 fwd = up.Cross(right).Normalized();
				Vector3 halfR = right * largeur;
				Vector3 halfF = fwd * hauteur;
				Vector3 p0 = pos - halfR - halfF;
				Vector3 p1 = pos + halfR - halfF;
				Vector3 p2 = pos + halfR + halfF;
				Vector3 p3 = pos - halfR + halfF;
				Vector3 n = up;
				st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
				st.SetNormal(n); st.SetUV(new Vector2(1, 1)); st.AddVertex(p1);
				st.SetNormal(n); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2);
				st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
				st.SetNormal(n); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2);
				st.SetNormal(n); st.SetUV(new Vector2(0, 0)); st.AddVertex(p3);
				total++;
			}
		}
	}

	private Color CouleurFeuillesArbre()
	{
		float h = Hash(Seed, 10);
		float h2 = Hash(Seed, 11);
		// Verts vibrants (vivant) : peu de rouge/bleu, vert dominant saturé
		float r = 0.1f + h * 0.08f;
		float g = 0.5f + h * 0.35f;
		float b = 0.08f + h2 * 0.12f;
		return new Color(r, g, b);
	}
}
