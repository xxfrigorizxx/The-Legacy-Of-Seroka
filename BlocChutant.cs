using Godot;

public partial class BlocChutant : RigidBody3D
{
	private const byte ID_BUISSON_PLEIN = 10;
	private const byte ID_BUISSON_VIDE = 11;
	private const byte ID_FIBRE_HERBE = 15;
	/// <summary>Bois (bûche) — LSystem Tronc.</summary>
	public const byte ID_BOIS = 30;
	/// <summary>Branche — bois fin, tombe quand on coupe.</summary>
	public const byte ID_BRANCHE = 31;

	/// <summary>Crée un BlocChutant. Le parent doit l'ajouter à la scène, puis définir GlobalPosition immédiatement après.</summary>
	public static BlocChutant Creer(Vector3 positionMonde, byte idMateriau, Material matTerrain)
	{
		var bloc = new BlocChutant();
		bloc.SetMeta("ID_Matiere", (int)idMateriau);
		bloc._ConstruireVisuelEtCollision(idMateriau, matTerrain);
		// GlobalPosition nécessite is_inside_tree() == true : à définir par l'appelant après AddChild().
		return bloc;
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
					float rayon = estBranche ? 0.08f : 0.2f;
					float hauteur = estBranche ? 0.6f : 0.5f;
					meshInstance.Mesh = _ConstruireMeshCylindre(rayon, hauteur);
					var bruitEcorce = new FastNoiseLite { Seed = 4242 };
					bruitEcorce.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
					bruitEcorce.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
					bruitEcorce.Frequency = 0.08f;
					var texEcorce = new NoiseTexture2D { Width = 128, Height = 128, Noise = bruitEcorce };
					meshInstance.MaterialOverride = new StandardMaterial3D
					{
						AlbedoColor = new Color(0.52f, 0.32f, 0.14f),
						AlbedoTexture = texEcorce,
						Roughness = 0.9f,
						Metallic = 0.02f
					};
					meshInstance.Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0); // Cylindre couché (bûche)
				}
				break;
			case ID_BUISSON_PLEIN:
				meshInstance.Mesh = _ExtraireMeshBuisson("res://Modeles/Botanique/Buisson_Plein.glb");
				meshInstance.Scale = new Vector3(0.008f, 0.008f, 0.008f);
				break;
			case ID_BUISSON_VIDE:
				meshInstance.Mesh = _ExtraireMeshBuisson("res://Modeles/Botanique/Buisson_Vide.glb");
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
			default:
				meshInstance.Mesh = _ConstruireMeshCube(idMateriau);
				break;
		}

		// Buissons/bois : garder leur matériau. Fibre : déjà ajouté. Autres (terrain etc.) : override matTerrain.
		if (idMateriau != ID_BUISSON_PLEIN && idMateriau != ID_BUISSON_VIDE && idMateriau != ID_FIBRE_HERBE && idMateriau != ID_BOIS && idMateriau != ID_BRANCHE && matTerrain != null)
			meshInstance.MaterialOverride = (Material)matTerrain.Duplicate();
		if (idMateriau != ID_FIBRE_HERBE)
			AddChild(meshInstance);

		// Collision simple BoxShape3D — jamais ConvexPolygonShape3D (perf potato PC)
		var collision = new CollisionShape3D();
		bool estBuisson = idMateriau == ID_BUISSON_PLEIN || idMateriau == ID_BUISSON_VIDE;
		bool estFibre = idMateriau == ID_FIBRE_HERBE;
		bool estBois = idMateriau == ID_BOIS || idMateriau == ID_BRANCHE;
		if (estFibre)
		{
			collision.Shape = new BoxShape3D { Size = new Vector3(0.1f, 0.4f, 0.1f) };
			collision.Position = new Vector3(0.05f, 0.2f, 0.05f);
		}
		else if (estBois)
		{
			float r = idMateriau == ID_BRANCHE ? 0.1f : 0.25f;
			float h = idMateriau == ID_BRANCHE ? 0.7f : 0.55f;
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
		else
		{
			collision.Shape = new BoxShape3D { Size = Vector3.One };
			collision.Position = new Vector3(0.5f, 0.5f, 0.5f);
		}
		AddChild(collision);
	}

	private static Mesh _ExtraireMeshBuisson(string path)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null) return null;
		Node racine = scene.Instantiate();
		Mesh mesh = _ExtraireMeshRecursif(racine);
		racine.QueueFree();
		return mesh;
	}

	private static Mesh _ExtraireMeshRecursif(Node noeud)
	{
		if (noeud is MeshInstance3D mi && mi.Mesh != null) return mi.Mesh;
		foreach (Node enfant in noeud.GetChildren())
		{
			Mesh m = _ExtraireMeshRecursif(enfant);
			if (m != null) return m;
		}
		return null;
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
