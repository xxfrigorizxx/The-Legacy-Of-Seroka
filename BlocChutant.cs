using Godot;

public partial class BlocChutant : RigidBody3D
{
	private static float VolumeDepuisShape(Shape3D s)
	{
		switch (s)
		{
			case BoxShape3D b:
				return Mathf.Abs(b.Size.X * b.Size.Y * b.Size.Z);
			case SphereShape3D sp:
				return 4f / 3f * Mathf.Pi * sp.Radius * sp.Radius * sp.Radius;
			case CylinderShape3D cy:
				return Mathf.Pi * cy.Radius * cy.Radius * cy.Height;
			case CapsuleShape3D ca:
			{
				float h = Mathf.Max(0f, ca.Height - 2f * ca.Radius);
				return Mathf.Pi * ca.Radius * ca.Radius * h + 4f / 3f * Mathf.Pi * ca.Radius * ca.Radius * ca.Radius;
			}
			default:
				return 0.125f;
		}
	}

	private float EstimerVolumeCollisionTotale()
	{
		float v = 0f;
		foreach (Node c in GetChildren())
		{
			if (c is CollisionShape3D cs && cs.Shape != null)
				v += VolumeDepuisShape(cs.Shape);
		}
		return Mathf.Max(1e-6f, v);
	}

	public override void _Ready()
	{
		AddToGroup("PersistantsBlocChutant");
		AddToGroup("ObjetsDormantsDynamiques");
		CollisionLayer = 1;
		CollisionMask = 1;
		ContinuousCd = true;
		LinearDampMode = RigidBody3D.DampMode.Replace;
		AngularDampMode = RigidBody3D.DampMode.Replace;

		int mid = 0;
		if (HasMeta("ID_Matiere"))
			mid = GetMeta("ID_Matiere").AsInt32();

		float vol = EstimerVolumeCollisionTotale();
		float densiteKgM3 = 1600f;
		var pm = new PhysicsMaterial { Friction = 0.72f, Bounce = 0.08f };
		float linD = 0.12f;
		float angD = 0.55f;

		switch ((byte)mid)
		{
			case ID_BOIS:
				densiteKgM3 = 520f;
				pm = new PhysicsMaterial { Friction = 0.78f, Bounce = 0.16f };
				linD = 0.07f;
				angD = 0.42f;
				break;
			case ID_BRANCHE:
				densiteKgM3 = 480f;
				pm = new PhysicsMaterial { Friction = 0.76f, Bounce = 0.14f };
				linD = 0.08f;
				angD = 0.38f;
				break;
			case ID_FIBRE_HERBE:
				densiteKgM3 = 120f;
				pm = new PhysicsMaterial { Friction = 0.88f, Bounce = 0.2f };
				linD = 0.45f;
				angD = 1.05f;
				break;
			case ID_BUISSON_PLEIN:
			case ID_BUISSON_VIDE:
				densiteKgM3 = 200f;
				pm = new PhysicsMaterial { Friction = 0.82f, Bounce = 0.1f };
				linD = 0.35f;
				angD = 0.85f;
				break;
			case ID_FEUILLE_ARRACHEE:
				densiteKgM3 = 90f;
				pm = new PhysicsMaterial { Friction = 0.92f, Bounce = 0.06f };
				linD = 0.55f;
				angD = 1.15f;
				break;
			case ID_BAIE:
				densiteKgM3 = 180f;
				pm = new PhysicsMaterial { Friction = 0.82f, Bounce = 0.12f };
				linD = 0.28f;
				angD = 0.52f;
				break;
		}

		Mass = Mathf.Clamp(vol * densiteKgM3, 0.02f, 500f);
		PhysicsMaterialOverride = pm;
		LinearDamp = linD;
		AngularDamp = angD;
	}

	private const byte ID_BUISSON_PLEIN = 10;
	private const byte ID_BUISSON_VIDE = 11;
	private const byte ID_FIBRE_HERBE = 15;
	/// <summary>Bois (bûche) — LSystem Tronc.</summary>
	public const byte ID_BOIS = 30;
	/// <summary>Branche — bois fin, tombe quand on coupe.</summary>
	public const byte ID_BRANCHE = 31;
	/// <summary>Petite baie récoltable.</summary>
	public const byte ID_BAIE = 35;
	/// <summary>Feuillage arraché (même mesh visuel que les feuilles d'arbre, pas de l'herbe).</summary>
	public const byte ID_FEUILLE_ARRACHEE = 34;

	/// <summary>Méta sur ID 31 : branche issue d'un buisson (coupée courte / fine), pas d'un arbre.</summary>
	public const string MetaBrancheTailléeBuisson = "BrancheTailléeBuisson";

	/// <summary>Crée un BlocChutant. Le parent doit l'ajouter à la scène, puis définir GlobalPosition immédiatement après.</summary>
	/// <param name="brancheTailléeBuisson">Si true et <see cref="ID_BRANCHE"/> : mesh court (récolte buisson). Sinon : branche d'arbre (longue).</param>
	public static BlocChutant Creer(Vector3 positionMonde, byte idMateriau, Material matTerrain, bool brancheTailléeBuisson = false)
	{
		var bloc = new BlocChutant();
		bloc.SetMeta("ID_Matiere", (int)idMateriau);
		if (idMateriau == ID_BRANCHE && brancheTailléeBuisson)
			bloc.SetMeta(MetaBrancheTailléeBuisson, true);
		if (idMateriau == ID_FEUILLE_ARRACHEE)
			bloc._ConstruireVisuelFeuillage(null);
		else
			bloc._ConstruireVisuelEtCollision(idMateriau, matTerrain);
		// GlobalPosition nécessite is_inside_tree() == true : à définir par l'appelant après AddChild().
		return bloc;
	}

	/// <summary>Crée un BlocChutant feuillage (même visuel que les feuilles d'arbre). Utiliser quand on arrache le feuillage d'un arbre.</summary>
	public static BlocChutant CreerFeuillageArrache(Vector3 positionMonde, Material matFeuilles, Mesh meshFeuillageSource = null)
	{
		var bloc = new BlocChutant();
		bloc.SetMeta("ID_Matiere", (int)ID_FEUILLE_ARRACHEE);
		bloc._ConstruireVisuelFeuillage(matFeuilles, meshFeuillageSource);
		return bloc;
	}

	private static bool EssayerExtraireTeinteMoyenneFeuillage(Mesh mesh, out Color teinteMoyenne)
	{
		teinteMoyenne = Colors.Black;
		if (mesh is not ArrayMesh arrayMesh || arrayMesh.GetSurfaceCount() <= 0)
			return false;

		Color somme = Colors.Black;
		int echantillons = 0;
		for (int s = 0; s < arrayMesh.GetSurfaceCount(); s++)
		{
			var mdt = new MeshDataTool();
			if (mdt.CreateFromSurface(arrayMesh, s) != Error.Ok)
				continue;
			int nbVerts = mdt.GetVertexCount();
			for (int i = 0; i < nbVerts; i++)
			{
				Color c = mdt.GetVertexColor(i);
				if (c.A <= 0f)
					continue;
				somme += new Color(c.R, c.G, c.B, 1f);
				echantillons++;
			}
		}

		if (echantillons <= 0)
			return false;

		float inv = 1f / echantillons;
		teinteMoyenne = new Color(somme.R * inv, somme.G * inv, somme.B * inv, 1f);
		return true;
	}

	private static bool EstBlancApprox(Color c)
	{
		return Mathf.IsEqualApprox(c.R, 1f)
			&& Mathf.IsEqualApprox(c.G, 1f)
			&& Mathf.IsEqualApprox(c.B, 1f);
	}

	private void _ConstruireVisuelFeuillage(Material matFeuilles, Mesh meshFeuillageSource = null)
	{
		// Petit cluster de feuilles (quads ovales) — même style que le feuillage d'arbre, pas des brins d'herbe.
		Color teinteFallback = new Color(0.2f, 0.55f, 0.15f);
		if (EssayerExtraireTeinteMoyenneFeuillage(meshFeuillageSource, out Color teinteSource))
			teinteFallback = teinteSource;

		Material mat;
		if (matFeuilles is StandardMaterial3D matStd)
		{
			var matStdClone = (StandardMaterial3D)matStd.Duplicate();
			bool dependCouleurVertex = matStdClone.VertexColorUseAsAlbedo;
			if (dependCouleurVertex)
				matStdClone.VertexColorUseAsAlbedo = false;
			if (dependCouleurVertex || (matStdClone.AlbedoTexture == null && EstBlancApprox(matStdClone.AlbedoColor)))
				matStdClone.AlbedoColor = teinteFallback;
			mat = matStdClone;
		}
		else if (matFeuilles != null)
		{
			mat = (Material)matFeuilles.Duplicate();
		}
		else
		{
			mat = new StandardMaterial3D { AlbedoColor = teinteFallback, Roughness = 0.95f, Metallic = 0f };
		}
		float l = 0.18f;
		float w = 0.12f;
		for (int i = 0; i < 3; i++)
		{
			var q = new QuadMesh { Size = new Vector2(w, l) };
			var mi = new MeshInstance3D { Mesh = q, MaterialOverride = mat };
			mi.Position = new Vector3((i - 1) * 0.04f, l * 0.5f, (i % 2) * 0.02f);
			mi.Rotation = new Vector3(0.1f * (i - 1), 0.15f * i, 0.08f * (i - 1));
			AddChild(mi);
		}
		var collision = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.15f, l, 0.15f) }, Position = new Vector3(0, l * 0.5f, 0) };
		AddChild(collision);
	}

	private void _ConstruireVisuelEtCollision(byte idMateriau, Material matTerrain)
	{
		var meshInstance = new MeshInstance3D { Name = "MeshInstance3D" };

		switch (idMateriau)
		{
			case ID_BOIS:
			case ID_BRANCHE:
				{
					bool estBranche = idMateriau == ID_BRANCHE;
					// Branche d'arbre : même silhouette procédurale qu'un bâton (32). Buisson : petit cylindre.
					bool tailléeBuisson = estBranche && HasMeta(MetaBrancheTailléeBuisson) && GetMeta(MetaBrancheTailléeBuisson).AsBool();
					if (estBranche && tailléeBuisson)
					{
						float rayon = 0.0267f;
						float hauteur = 0.2f;
						meshInstance.Mesh = _ConstruireMeshCylindre(rayon, hauteur);
						if (matTerrain != null)
							meshInstance.MaterialOverride = (Material)matTerrain.Duplicate();
						else
							meshInstance.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.52f, 0.32f, 0.14f), Roughness = 0.9f, Metallic = 0.02f };
					}
					else if (estBranche)
					{
						Joueur.CalculerDimensionsBoisPose(32, 0, 2, out float br, out float bl, out _, out _);
						meshInstance.Mesh = Joueur.GenererMeshBoisFendu(br, bl, 0);
						if (matTerrain != null)
							meshInstance.MaterialOverride = (Material)matTerrain.Duplicate();
						else
							meshInstance.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.52f, 0.32f, 0.14f), Roughness = 0.9f, Metallic = 0.02f };
					}
					else
					{
						float rayon = 0.2f;
						float hauteur = 0.5f;
						meshInstance.Mesh = _ConstruireMeshCylindre(rayon, hauteur);
						meshInstance.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.52f, 0.32f, 0.14f), Roughness = 0.9f, Metallic = 0.02f };
					}
					meshInstance.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
				}
				break;
			case ID_BUISSON_PLEIN:
				meshInstance.Mesh = Chunk_Client.ObtenirMeshBuissonProcedural(true);
				meshInstance.Scale = new Vector3(0.008f, 0.008f, 0.008f);
				break;
			case ID_BUISSON_VIDE:
				meshInstance.Mesh = Chunk_Client.ObtenirMeshBuissonProcedural(false);
				meshInstance.Scale = new Vector3(0.008f, 0.008f, 0.008f);
				break;
			case ID_FIBRE_HERBE:
				{
					var matHerbe = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.55f, 0.15f), Roughness = 0.9f };
					float l = 0.38f;
					for (int i = 0; i < 4; i++)
					{
						float x = (i - 1.5f) * 0.02f; float z = (i % 2) * 0.018f - 0.009f;
						var mi = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.01f, Height = l - 0.02f }, MaterialOverride = matHerbe, Position = new Vector3(x, l * 0.5f, z), Rotation = new Vector3(0.05f * (i - 2), 0, 0.04f * (i - 1)) };
						AddChild(mi);
					}
				}
				break;
			case ID_BAIE:
				meshInstance.Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 10, Rings = 6 };
				meshInstance.MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = new Color(0.90f, 0.14f, 0.14f),
					Roughness = 0.34f,
					Metallic = 0f,
					EmissionEnabled = true,
					Emission = new Color(0.05f, 0.01f, 0.01f)
				};
				break;
			default:
				meshInstance.Mesh = _ConstruireMeshCube(idMateriau);
				break;
		}

		// Buissons/bois : garder leur matériau. Fibre : déjà ajouté. Autres (terrain etc.) : override matTerrain.
		if (idMateriau != ID_BUISSON_PLEIN && idMateriau != ID_BUISSON_VIDE && idMateriau != ID_FIBRE_HERBE && idMateriau != ID_BOIS && idMateriau != ID_BRANCHE && idMateriau != ID_BAIE && matTerrain != null)
			meshInstance.MaterialOverride = (Material)matTerrain.Duplicate();
		if (idMateriau != ID_FIBRE_HERBE)
			AddChild(meshInstance);

		// Collision simple BoxShape3D — jamais ConvexPolygonShape3D (perf potato PC)
		var collision = new CollisionShape3D();
		bool estBuisson = idMateriau == ID_BUISSON_PLEIN || idMateriau == ID_BUISSON_VIDE;
		bool estFibre = idMateriau == ID_FIBRE_HERBE;
		bool estBois = idMateriau == ID_BOIS || idMateriau == ID_BRANCHE;
		bool estBaie = idMateriau == ID_BAIE;
		if (estFibre)
		{
			collision.Shape = new BoxShape3D { Size = new Vector3(0.1f, 0.4f, 0.1f) };
			collision.Position = new Vector3(0.05f, 0.2f, 0.05f);
		}
		else if (estBois)
		{
			bool brancheTailléeBuisson = idMateriau == ID_BRANCHE && HasMeta(MetaBrancheTailléeBuisson) && GetMeta(MetaBrancheTailléeBuisson).AsBool();
			float r;
			float h;
			if (idMateriau == ID_BRANCHE && !brancheTailléeBuisson)
			{
				Joueur.CalculerDimensionsBoisPose(32, 0, 2, out r, out h, out _, out _);
			}
			else if (idMateriau == ID_BRANCHE)
			{
				r = 0.0334f;
				h = 0.233f;
			}
			else
			{
				r = 0.25f;
				h = 0.55f;
			}
			collision.Shape = new CylinderShape3D { Radius = r, Height = h };
			collision.Position = new Vector3(0, 0, 0);
			collision.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0);
		}
		else if (estBuisson)
		{
			// Collision fixe 0.25m — indépendante du mesh géant (perf)
			float tailleCollision = 0.25f;
			collision.Shape = new BoxShape3D { Size = new Vector3(tailleCollision, tailleCollision, tailleCollision) };
			collision.Position = new Vector3(tailleCollision * 0.5f, tailleCollision * 0.5f, tailleCollision * 0.5f);
		}
		else if (estBaie)
		{
			collision.Shape = new SphereShape3D { Radius = 0.09f };
			collision.Position = new Vector3(0f, 0f, 0f);
		}
		else
		{
			collision.Shape = new BoxShape3D { Size = Vector3.One };
			collision.Position = new Vector3(0.5f, 0.5f, 0.5f);
		}
		AddChild(collision);
	}

	private static Mesh _ConstruireMeshCylindre(float rayon, float hauteur)
	{
		const int cotes = 12;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		float halfH = hauteur * 0.5f;
		for (int i = 0; i < cotes; i++)
		{
			float a0 = (float)i / cotes * Mathf.Tau;
			float a1 = (float)(i + 1) / cotes * Mathf.Tau;
			Vector3 v0 = new Vector3(Mathf.Cos(a0) * rayon, halfH, Mathf.Sin(a0) * rayon);
			Vector3 v1 = new Vector3(Mathf.Cos(a1) * rayon, halfH, Mathf.Sin(a1) * rayon);
			Vector3 v2 = new Vector3(Mathf.Cos(a0) * rayon, -halfH, Mathf.Sin(a0) * rayon);
			Vector3 v3 = new Vector3(Mathf.Cos(a1) * rayon, -halfH, Mathf.Sin(a1) * rayon);
			Vector3 n = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0, Mathf.Sin((a0 + a1) * 0.5f));
			st.SetNormal(n); st.AddVertex(v0);
			st.SetNormal(n); st.AddVertex(v1);
			st.SetNormal(n); st.AddVertex(v3);
			st.SetNormal(n); st.AddVertex(v0);
			st.SetNormal(n); st.AddVertex(v3);
			st.SetNormal(n); st.AddVertex(v2);
		}
		Vector3 nTop = Vector3.Up, nBot = Vector3.Down;
		for (int i = 0; i < cotes; i++)
		{
			float a0 = (float)i / cotes * Mathf.Tau;
			float a1 = (float)(i + 1) / cotes * Mathf.Tau;
			Vector3 c = new Vector3(0, halfH, 0);
			Vector3 p0 = new Vector3(Mathf.Cos(a0) * rayon, halfH, Mathf.Sin(a0) * rayon);
			Vector3 p1 = new Vector3(Mathf.Cos(a1) * rayon, halfH, Mathf.Sin(a1) * rayon);
			st.SetNormal(nTop); st.AddVertex(c);
			st.SetNormal(nTop); st.AddVertex(p0);
			st.SetNormal(nTop); st.AddVertex(p1);
		}
		for (int i = 0; i < cotes; i++)
		{
			float a0 = (float)i / cotes * Mathf.Tau;
			float a1 = (float)(i + 1) / cotes * Mathf.Tau;
			Vector3 c = new Vector3(0, -halfH, 0);
			Vector3 p0 = new Vector3(Mathf.Cos(a0) * rayon, -halfH, Mathf.Sin(a0) * rayon);
			Vector3 p1 = new Vector3(Mathf.Cos(a1) * rayon, -halfH, Mathf.Sin(a1) * rayon);
			st.SetNormal(nBot); st.AddVertex(c);
			st.SetNormal(nBot); st.AddVertex(p1);
			st.SetNormal(nBot); st.AddVertex(p0);
		}
		st.GenerateNormals();
		return st.Commit();
	}

	private static Mesh _ConstruireMeshCube(byte idMateriau)
	{
		Color couleurId = new Color(idMateriau / 255.0f, 0.0f, 0.0f, 1.0f);
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		Vector3[] verts = {
			new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
			new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1)
		};
		int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 2, 6, 7, 2, 7, 3, 0, 3, 7, 0, 7, 4, 1, 5, 6, 1, 6, 2 };
		for (int i = 0; i < indices.Length; i += 3)
		{
			Vector3 n = (verts[indices[i + 1]] - verts[indices[i]]).Cross(verts[indices[i + 2]] - verts[indices[i]]).Normalized();
			st.SetNormal(n); st.SetColor(couleurId); st.AddVertex(verts[indices[i]]);
			st.SetNormal(n); st.SetColor(couleurId); st.AddVertex(verts[indices[i + 1]]);
			st.SetNormal(n); st.SetColor(couleurId); st.AddVertex(verts[indices[i + 2]]);
		}
		st.GenerateNormals();
		return st.Commit();
	}
}
