using Godot;
using System;
using System.Collections.Generic;

public partial class ItemPhysique : RigidBody3D
{
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
