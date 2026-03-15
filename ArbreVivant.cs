using Godot;
using System.Collections.Generic;

/// <summary>Entité 3D d'arbre procédurale (L-System volumétrique). Branches continues, feuillage, croissance temporelle.</summary>
/// <remarks>Hérite de StaticBody3D. Remplaçant des arbres voxels.</remarks>
public partial class ArbreVivant : StaticBody3D
{
	private struct TortueEtat
	{
		public Transform3D Transform;
		public float Epaisseur;
	}

	public int AgeEnJours = 1;
	public float ResistanceActuelle = 50f;
	/// <summary>Graine pour variabilité des angles/longueurs (évite arbres identiques).</summary>
	public uint Seed = 12345;
	private const float CHANCE_CROISSANCE = 0.05f; // 1 chance sur 20 de grandir chaque nuit

	private MeshInstance3D _visuelBois;
	private MeshInstance3D _visuelFeuillage;
	private CollisionShape3D _hitbox;

	private static StandardMaterial3D _cacheMatBois;
	private static StandardMaterial3D _cacheMatFeuilles;

	private static Material ObtenirMaterielBois()
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
			ResistanceActuelle = 50f * AgeEnJours;
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
			ResistanceActuelle = 50f * AgeEnJours;
			GenererMaillageArbre();
		}
	}

	/// <summary>Applique des dégâts (minage avec pierre/silex). Résistance selon épaisseur : branche fine = coupe facile, tronc épais = dur.</summary>
	/// <param name="pointImpactMonde">Point d'impact du rayon (en coordonnées monde).</param>
	/// <param name="degats">Dégâts de base de l'outil.</param>
	/// <returns>True si l'arbre est abattu (branches et bûches tombent au sol).</returns>
	public bool SubirDegats(Vector3 pointImpactMonde, float degats)
	{
		Vector3 hitLocal = GlobalTransform.AffineInverse() * pointImpactMonde;
		float distAxis = Mathf.Sqrt(hitLocal.X * hitLocal.X + hitLocal.Z * hitLocal.Z);
		float hauteurArbre = (0.6f + AgeEnJours * 0.18f) * 6f * (AgeEnJours <= 2 ? 0.5f : 1f);
		float yNorm = Mathf.Clamp(hitLocal.Y / Mathf.Max(0.1f, hauteurArbre), 0f, 1f);
		float epaisseurTronc = 0.2f * (1f - yNorm * 0.6f);
		float epaisseurEstimee = distAxis < 0.2f ? epaisseurTronc : Mathf.Max(0.03f, 0.15f - distAxis * 0.2f);
		float multiplicateur = 0.12f / Mathf.Max(0.03f, epaisseurEstimee);
		float degatsEffectifs = degats * Mathf.Clamp(multiplicateur, 0.5f, 4f);
		ResistanceActuelle -= degatsEffectifs;
		if (ResistanceActuelle <= 0f)
		{
			Vector3 baseArbre = GlobalPosition;
			var gestionnaire = GetParent()?.GetParent() as Gestionnaire_Monde;
			gestionnaire?.DemanderSpawnDebrisArbre(baseArbre, AgeEnJours, Seed);
			QueueFree();
			return true;
		}
		return false;
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

		bool premierSegmentDeBranche = false;
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
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					GenererSegmentBranche(stBois, pStart, tortue.Origin, right, forward, rayonDebut, rayonFin);
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
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					if (premierSegmentDeBranche)
					{
						rayonDebut = epaisseurBase * 1.12f;
						premierSegmentDeBranche = false;
					}
					float coef = (commande == 'b') ? 0.7f : 1f;
					GenererSegmentBranche(stBois, pStart, tortue.Origin, right, forward, rayonDebut * coef, rayonFin * coef);
					epaisseurBase = rayonFin * coef;
					// Feuillage LE LONG des branches : 2–3 clusters par segment (style chêne dense)
					if (pile.Count > 0)
					{
						int hashBase = Mathf.Abs((int)(pStart.X * 7 + pStart.Z * 31 + pStart.Y * 13));
						Transform3D tStart = new Transform3D(tortue.Basis, pStart);
						Transform3D tMid = new Transform3D(tortue.Basis, pStart.Lerp(tortue.Origin, 0.5f));
						Transform3D tEnd = new Transform3D(tortue.Basis, tortue.Origin);
						GenererFeuillagePetit(stFeuilles, tStart, AgeEnJours);
						if (Hash(Seed, hashBase) < 0.85f) GenererFeuillagePetit(stFeuilles, tMid, AgeEnJours);
						GenererFeuillagePetit(stFeuilles, tEnd, AgeEnJours);
					}
					break;
				}
				case '[':
					pile.Push(new TortueEtat { Transform = tortue, Epaisseur = epaisseurBase });
					premierSegmentDeBranche = true;
					break;
				case ']':
				{
					TortueEtat etat = pile.Pop();
					tortue = etat.Transform;
					epaisseurBase = etat.Epaisseur;
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
					// CIME : cluster de feuillage massif (pas de bois)
					GenererFeuillage(stFeuilles, tortue, AgeEnJours);
					break;
			}
		}
		// Couronne au sommet
		GenererFeuillage(stFeuilles, tortue, AgeEnJours);

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

		_hitbox.Shape = meshBois != null && meshBois.GetFaces().Length > 0
			? ItemPhysique.CreerShapeCollisionConvexeRobuste(meshBois)
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
