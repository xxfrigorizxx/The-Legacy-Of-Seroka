using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Chunk_Client : Node3D
{
	/// <summary>Expose les meshes buisson procéduraux pour les autres systèmes (ex: bloc chutant).</summary>
	public static Mesh ObtenirMeshBuissonProcedural(bool avecBaies, int couleurBaieIndex = 0)
	{
		if (!avecBaies)
		{
			if (_cacheMeshVide == null) _cacheMeshVide = GenererMeshBuissonProcedural(false, 0);
			return _cacheMeshVide;
		}
		int c = Joueur.ClampIndexCouleurBaie(couleurBaieIndex);
		if (_cacheMeshPleinParCouleurCache[c] == null)
			_cacheMeshPleinParCouleurCache[c] = GenererMeshBuissonProcedural(true, c);
		return _cacheMeshPleinParCouleurCache[c];
	}

	/// <summary>Expose le mesh procédural d'aloe vera pour les items inventaire/monde.</summary>
	public static Mesh ObtenirMeshAloeVeraProcedural()
	{
		if (_cacheMeshAloeVera == null) _cacheMeshAloeVera = GenererMeshAloeVeraProcedural();
		return _cacheMeshAloeVera;
	}

	/// <summary>Expose un petit segment d'aloe (objet inventaire), pas la plante complète.</summary>
	public static Mesh ObtenirMeshLamelleAloeObjetProcedural()
	{
		if (_cacheMeshLamelleAloeObjet == null)
		{
			// Objet récolté: priorité au modèle utilisateur.
			_cacheMeshLamelleAloeObjet = ChargerMeshAloeVeraDepuisModeleUtilisateur() ?? GenererMeshLamelleAloeObjetProcedural();
		}
		return _cacheMeshLamelleAloeObjet;
	}

	/// <summary>Buisson procédural unifié : "vide" = feuillage seul, "plein" = feuillage + baies teintées (<paramref name="indexCouleurBaie"/>).</summary>
	private static Mesh GenererMeshBuissonProcedural(bool avecBaies, int indexCouleurBaie = 0)
	{
		int c = avecBaies ? Joueur.ClampIndexCouleurBaie(indexCouleurBaie) : 0;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var rng = new RandomNumberGenerator { Seed = avecBaies ? (uint)(0xB511u + (uint)c * 199u) : 0xB512u };

		int couches = avecBaies ? 10 : 9;
		for (int couche = 0; couche < couches; couche++)
		{
			float t = couches == 1 ? 0f : couche / (float)(couches - 1);
			float hauteur = Mathf.Lerp(5f, 30f, t);
			float compression = 1f - Mathf.Abs(t - 0.52f) * 1.05f;
			float rayon = Mathf.Max(16f, 52f * compression);
			// Densité fortement augmentée (x4-x5) pour un vrai volume de buisson.
			int feuilles = 42 + (couche % 4) * 8;
			for (int i = 0; i < feuilles; i++)
			{
				float angle = (i / (float)feuilles) * Mathf.Tau + rng.RandfRange(-0.22f, 0.22f);
				Vector3 centre = new Vector3(
					Mathf.Cos(angle) * rayon * rng.RandfRange(0.28f, 0.78f),
					hauteur + rng.RandfRange(-2.2f, 2.2f),
					Mathf.Sin(angle) * rayon * rng.RandfRange(0.28f, 0.78f));
				float largeur = rng.RandfRange(17f, 26f);
				float longueur = rng.RandfRange(7f, 12f);
				float inclinaison = rng.RandfRange(-0.03f, 0.06f);
				Color couleurFeuilleBase = new Color(
					0.23f + rng.RandfRange(-0.025f, 0.05f),
					0.52f + rng.RandfRange(-0.06f, 0.08f),
					0.24f + rng.RandfRange(-0.03f, 0.04f),
					1f);
				Color couleurFeuilleSommet = couleurFeuilleBase.Lerp(new Color(0.72f, 0.86f, 0.62f, 1f), 0.28f);
				AjouterFeuillePerforeeDoubleFace(st, centre, largeur, longueur, angle + rng.RandfRange(-0.25f, 0.25f), inclinaison, couleurFeuilleBase, couleurFeuilleSommet);
			}
		}

		// Tige courte + micro-branches: le buisson doit rester "rond", pas ressembler à un mini-arbre.
		Color couleurTigeBas = new Color(0.30f, 0.24f, 0.16f, 1f);
		Color couleurTigeHaut = new Color(0.36f, 0.30f, 0.20f, 1f);
		AjouterCylindreSegment(st, new Vector3(0f, 0f, 0f), new Vector3(0f, 15f, 0f), 4.2f, 2.8f, 6, couleurTigeBas, couleurTigeHaut);
		AjouterCylindreSegment(st, new Vector3(0f, 9f, 0f), new Vector3(4.2f, 15.5f, 2.5f), 1.5f, 0.9f, 5, couleurTigeHaut, couleurTigeHaut);
		AjouterCylindreSegment(st, new Vector3(0f, 8f, 0f), new Vector3(-4.0f, 14.8f, 2.8f), 1.4f, 0.85f, 5, couleurTigeHaut, couleurTigeHaut);
		AjouterCylindreSegment(st, new Vector3(0f, 10f, 0f), new Vector3(2.0f, 15.2f, -4.5f), 1.4f, 0.8f, 5, couleurTigeHaut, couleurTigeHaut);

		if (avecBaies)
		{
			int nbBaies = 8;
			Color couleurBaie = Joueur.ObtenirCouleurAlbedoBaie(c);
			for (int i = 0; i < nbBaies; i++)
			{
				float a = (i / (float)nbBaies) * Mathf.Tau + rng.RandfRange(-0.25f, 0.25f);
				float r = rng.RandfRange(10f, 22f);
				float y = rng.RandfRange(10f, 28f);
				Vector3 centreBaie = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
				AjouterBaieGourmande(st, centreBaie, rng.RandfRange(2.1f, 3.3f), couleurBaie);
			}
		}

		st.GenerateNormals();
		var mesh = st.Commit();
		if (mesh is ArrayMesh am && am.GetSurfaceCount() > 0)
		{
			if (avecBaies && c == 8)
			{
				var matFluo = (StandardMaterial3D)ObtenirMaterielBuissonProcedural().Duplicate();
				matFluo.Emission = new Color(0.08f, 0.48f, 0.58f);
				matFluo.EmissionEnergyMultiplier = 2.5f;
				am.SurfaceSetMaterial(0, matFluo);
			}
			else
				am.SurfaceSetMaterial(0, ObtenirMaterielBuissonProcedural());
		}
		return mesh;
	}

	/// <summary>Plante désertique type aloe vera générée en code (rosette de feuilles épaisses avec ponctuation claire).</summary>
	private static Mesh GenererMeshAloeVeraProcedural()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var rng = new RandomNumberGenerator { Seed = 0xA10E0u };

		Color colBas = new Color(0.22f, 0.55f, 0.31f, 1f);
		Color colSommet = new Color(0.56f, 0.86f, 0.66f, 1f);
		Color colTache = new Color(0.86f, 0.94f, 0.88f, 1f);

		int feuilles = 15;
		for (int i = 0; i < feuilles; i++)
		{
			float ring = i < 8 ? 0f : 1f;
			float t = i / Mathf.Max(1f, feuilles - 1f);
			float angle = (i / (float)feuilles) * Mathf.Tau + (ring > 0f ? 0.16f : 0f) + rng.RandfRange(-0.055f, 0.055f);
			float longueur = ring > 0f ? rng.RandfRange(36f, 50f) : rng.RandfRange(24f, 34f);
			float largeurBase = ring > 0f ? rng.RandfRange(10f, 13f) : rng.RandfRange(8f, 10f);
			float epaisseur = rng.RandfRange(1.6f, 2.5f);
			float inclinaison = ring > 0f ? rng.RandfRange(0.11f, 0.24f) : rng.RandfRange(0.05f, 0.16f);
			float torsion = rng.RandfRange(-0.06f, 0.06f);
			Vector3 centre = new Vector3(Mathf.Cos(angle) * (ring > 0f ? 2.7f : 1.15f), 0.5f + ring * 0.5f, Mathf.Sin(angle) * (ring > 0f ? 2.7f : 1.15f));
			Color feuilleBase = colBas.Lerp(new Color(0.16f, 0.42f, 0.24f, 1f), t * 0.15f);
			Color feuilleSommet = colSommet.Lerp(new Color(0.68f, 0.94f, 0.74f, 1f), t * 0.2f);
			AjouterFeuilleAloeDoubleFace(st, centre, angle, longueur, largeurBase, epaisseur, inclinaison, torsion, feuilleBase, feuilleSommet);

			// Petites ponctuations blanches façon aloe (sans texture externe).
			int nbTaches = ring > 0f ? 7 : 4;
			for (int k = 0; k < nbTaches; k++)
			{
				float tf = (k + 1) / (float)(nbTaches + 1);
				float localL = longueur * tf * (0.72f + rng.RandfRange(-0.07f, 0.08f));
				float decalLat = rng.RandfRange(-0.22f, 0.22f) * largeurBase * (1f - tf * 0.7f);
				Vector3 axe = new Vector3(Mathf.Cos(angle + torsion * tf), inclinaison, Mathf.Sin(angle + torsion * tf)).Normalized();
				Vector3 droite = new Vector3(-axe.Z, 0f, axe.X).Normalized();
				Vector3 posTache = centre + axe * localL + droite * decalLat + Vector3.Up * (0.6f + tf * 0.25f);
				AjouterBilleOctaedre(st, posTache, rng.RandfRange(0.55f, 0.95f), colTache);
			}
		}

		// Coeur visuel : remplit le centre (évite trou/non-texturé) et donne l'effet de rosette compacte.
		AjouterCylindreSegment(st, new Vector3(0f, -0.35f, 0f), new Vector3(0f, 3.3f, 0f), 4.6f, 2.2f, 10, new Color(0.18f, 0.43f, 0.24f, 1f), new Color(0.30f, 0.63f, 0.35f, 1f));
		AjouterBilleOctaedre(st, new Vector3(0f, 2.7f, 0f), 1.7f, new Color(0.36f, 0.70f, 0.42f, 1f));
		for (int c = 0; c < 5; c++)
		{
			float a = (c / 5f) * Mathf.Tau + 0.16f;
			AjouterFeuilleAloeDoubleFace(
				st,
				new Vector3(Mathf.Cos(a) * 0.7f, 0.9f, Mathf.Sin(a) * 0.7f),
				a,
				16f + c * 1.2f,
				5.2f,
				1.25f,
				0.20f,
				0.015f,
				new Color(0.24f, 0.58f, 0.34f, 1f),
				new Color(0.62f, 0.92f, 0.72f, 1f));
		}

		st.GenerateNormals();
		var mesh = st.Commit();
		if (mesh is ArrayMesh am && am.GetSurfaceCount() > 0)
			am.SurfaceSetMaterial(0, ObtenirMaterielBuissonProcedural());
		return mesh;
	}

	private static StandardMaterial3D ConstruireMaterielAloeFallback(int indexPartie)
	{
		// Fallback simple si le GLB n'embarque pas de matériaux.
		if (indexPartie % 3 == 0)
			return new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.58f, 0.34f, 1f), Roughness = 0.78f, Metallic = 0f, VertexColorUseAsAlbedo = true };
		if (indexPartie % 3 == 1)
			return new StandardMaterial3D { AlbedoColor = new Color(0.80f, 0.95f, 0.86f, 1f), Roughness = 0.42f, Metallic = 0f, CullMode = BaseMaterial3D.CullModeEnum.Disabled, VertexColorUseAsAlbedo = true };
		return new StandardMaterial3D { AlbedoColor = new Color(0.17f, 0.30f, 0.18f, 1f), Roughness = 0.88f, Metallic = 0f, VertexColorUseAsAlbedo = true };
	}

	private static Material ConstruireMaterielAloePartie(int indexPartie, Material materiauSource)
	{
		// On force des teintes lisibles même si le GLB n'a pas de texture exploitable en runtime.
		StandardMaterial3D baseMat = materiauSource as StandardMaterial3D;
		StandardMaterial3D mat = baseMat != null
			? (StandardMaterial3D)baseMat.Duplicate()
			: ConstruireMaterielAloeFallback(indexPartie);

		if (indexPartie % 3 == 0)
		{
			mat.AlbedoColor = new Color(0.26f, 0.66f, 0.38f, 1f);
			mat.Roughness = 0.74f;
		}
		else if (indexPartie % 3 == 1)
		{
			mat.AlbedoColor = new Color(0.86f, 0.96f, 0.90f, 1f);
			mat.Roughness = 0.36f;
			mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		}
		else
		{
			mat.AlbedoColor = new Color(0.15f, 0.26f, 0.16f, 1f);
			mat.Roughness = 0.84f;
		}

		mat.Metallic = 0f;
		mat.VertexColorUseAsAlbedo = true;
		return mat;
	}

	private static Mesh NormaliserMeshAloeObjet(Mesh mesh)
	{
		if (mesh == null || mesh.GetSurfaceCount() <= 0)
			return mesh;

		Aabb box = mesh.GetAabb();
		if (box.Size.Y <= 0.0001f)
			return mesh;

		// Cible: item compact (~26 cm) centré sur l'origine pour éviter le "gros modèle" au lancer.
		const float hauteurCible = 0.26f;
		float facteur = hauteurCible / box.Size.Y;
		Vector3 centre = box.Position + box.Size * 0.5f;
		Transform3D xf = Transform3D.Identity.ScaledLocal(Vector3.One * facteur);
		xf.Origin = -centre * facteur;

		var resultat = new ArrayMesh();
		for (int s = 0; s < mesh.GetSurfaceCount(); s++)
		{
			var st = new SurfaceTool();
			st.Begin(Mesh.PrimitiveType.Triangles);
			st.AppendFrom(mesh, s, xf);
			Godot.Collections.Array arrays = st.CommitToArrays();
			resultat.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			resultat.SurfaceSetMaterial(s, mesh.SurfaceGetMaterial(s));
		}
		return resultat;
	}

	private static Mesh ChargerMeshAloeVeraDepuisModeleUtilisateur()
	{
		const string cheminGlb = "res://Modeles/materials/naturelle/aloe_verra.glb";
		PackedScene scene = GD.Load<PackedScene>(cheminGlb);
		if (scene == null)
			return null;

		Node inst = scene.Instantiate();
		if (inst == null)
			return null;

		try
		{
			var parties = new List<(Mesh mesh, MeshInstance3D mi, Transform3D xf)>(4);
			void Collecter(Node n, Transform3D xfParent)
			{
				Transform3D xfCourant = xfParent;
				if (n is Node3D n3)
					xfCourant = xfParent * n3.Transform;
				if (n is MeshInstance3D mi && mi.Mesh != null)
					parties.Add((mi.Mesh, mi, xfCourant));
				for (int i = 0; i < n.GetChildCount(); i++)
					Collecter(n.GetChild(i), xfCourant);
			}
			Collecter(inst, Transform3D.Identity);
			if (parties.Count == 0)
				return null;

			var fusion = new ArrayMesh();
			int surfaceAjoutee = 0;
			for (int p = 0; p < parties.Count; p++)
			{
				(Mesh meshPartie, MeshInstance3D miPartie, Transform3D xfPartie) = parties[p];
				int surfaces = meshPartie.GetSurfaceCount();
				for (int s = 0; s < surfaces; s++)
				{
					var st = new SurfaceTool();
					st.Begin(Mesh.PrimitiveType.Triangles);
					st.AppendFrom(meshPartie, s, xfPartie);
					st.GenerateNormals();
					Godot.Collections.Array arrays = st.CommitToArrays();
					fusion.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
					Material matSource = miPartie.GetActiveMaterial(s) ?? meshPartie.SurfaceGetMaterial(s);
					fusion.SurfaceSetMaterial(surfaceAjoutee, ConstruireMaterielAloePartie(p, matSource));
					surfaceAjoutee++;
				}
			}
			return NormaliserMeshAloeObjet(fusion);
		}
		catch (Exception ex)
		{
			GD.PrintErr("ZERO-K : Chargement aloe_verra.glb échoué, fallback procédural. ", ex.Message);
			return null;
		}
		finally
		{
			inst.QueueFree();
		}
	}

	/// <summary>Petite lamelle d'aloe (gel) utilisée comme item récoltable en inventaire.</summary>
	private static Mesh GenererMeshLamelleAloeObjetProcedural()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		Color colBase = new Color(0.23f, 0.62f, 0.37f, 1f);
		Color colSommet = new Color(0.62f, 0.90f, 0.70f, 1f);
		Color colGel = new Color(0.83f, 0.97f, 0.88f, 1f);

		// Lamelle unique: une seule pointe (pas deux), avec ouverture centrale légère.
		Vector3 baseL = new Vector3(-0.040f, 0.000f, -0.055f);
		Vector3 baseR = new Vector3(0.040f, 0.000f, -0.055f);
		Vector3 midL = new Vector3(-0.030f, 0.012f, 0.000f);
		Vector3 midR = new Vector3(0.030f, 0.012f, 0.000f);
		Vector3 innerL = new Vector3(-0.008f, 0.008f, 0.006f);
		Vector3 innerR = new Vector3(0.008f, 0.008f, 0.006f);
		Vector3 tip = new Vector3(0.000f, 0.020f, 0.105f);

		// Face avant.
		AjouterTriangleCouleurParSommet(st, baseL, colBase, midL, colBase.Lerp(colSommet, 0.55f), innerL, colSommet);
		AjouterTriangleCouleurParSommet(st, baseR, colBase, innerR, colSommet, midR, colBase.Lerp(colSommet, 0.55f));
		AjouterTriangleCouleurParSommet(st, midL, colBase.Lerp(colSommet, 0.55f), tip, colSommet, innerL, colSommet);
		AjouterTriangleCouleurParSommet(st, innerR, colSommet, tip, colSommet, midR, colBase.Lerp(colSommet, 0.55f));
		// Bande de gel au centre.
		AjouterTriangleCouleurParSommet(st, innerL, colGel, innerR, colGel, tip, colGel);

		// Face arrière.
		AjouterTriangleCouleurParSommet(st, innerL, colSommet, midL, colBase.Lerp(colSommet, 0.55f), baseL, colBase);
		AjouterTriangleCouleurParSommet(st, midR, colBase.Lerp(colSommet, 0.55f), innerR, colSommet, baseR, colBase);
		AjouterTriangleCouleurParSommet(st, innerL, colSommet, tip, colSommet, midL, colBase.Lerp(colSommet, 0.55f));
		AjouterTriangleCouleurParSommet(st, midR, colBase.Lerp(colSommet, 0.55f), tip, colSommet, innerR, colSommet);
		AjouterTriangleCouleurParSommet(st, tip, colGel, innerR, colGel, innerL, colGel);

		// Petit volume de base pour éviter l'aspect "papier plat".
		AjouterBilleOctaedre(st, new Vector3(0f, 0.004f, -0.018f), 0.008f, colBase.Lerp(colGel, 0.35f));
		st.GenerateNormals();
		var mesh = st.Commit();
		if (mesh is ArrayMesh am && am.GetSurfaceCount() > 0)
		{
			var mat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.58f, 0.88f, 0.69f, 1f),
				Roughness = 0.56f,
				Metallic = 0f,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			am.SurfaceSetMaterial(0, mat);
		}
		return mesh;
	}

	private static void AjouterFeuilleAloeDoubleFace(SurfaceTool st, Vector3 baseCentre, float angleY, float longueur, float largeurBase, float epaisseur, float inclinaison, float torsion, Color colBase, Color colSommet)
	{
		Vector3 dir = new Vector3(Mathf.Cos(angleY), inclinaison, Mathf.Sin(angleY)).Normalized();
		Vector3 droite = new Vector3(-dir.Z, 0f, dir.X).Normalized();
		float largeurMilieu = largeurBase * 0.55f;
		float largeurSommet = largeurBase * 0.16f;
		Vector3 p0L = baseCentre - droite * largeurBase * 0.5f;
		Vector3 p0R = baseCentre + droite * largeurBase * 0.5f;
		Vector3 centreMilieu = baseCentre + dir * (longueur * 0.48f) + Vector3.Up * (epaisseur * 0.8f);
		Vector3 droiteMilieu = new Vector3(-Mathf.Sin(angleY + torsion), 0f, Mathf.Cos(angleY + torsion)).Normalized();
		Vector3 p1L = centreMilieu - droiteMilieu * largeurMilieu * 0.5f;
		Vector3 p1R = centreMilieu + droiteMilieu * largeurMilieu * 0.5f;
		Vector3 centreSommet = baseCentre + dir * longueur + Vector3.Up * (epaisseur * 1.4f);
		Vector3 droiteSommet = new Vector3(-Mathf.Sin(angleY + torsion * 1.8f), 0f, Mathf.Cos(angleY + torsion * 1.8f)).Normalized();
		Vector3 p2L = centreSommet - droiteSommet * largeurSommet * 0.5f;
		Vector3 p2R = centreSommet + droiteSommet * largeurSommet * 0.5f;

		AjouterTriangleCouleurParSommet(st, p0L, colBase, p0R, colBase, p1R, colBase.Lerp(colSommet, 0.45f));
		AjouterTriangleCouleurParSommet(st, p0L, colBase, p1R, colBase.Lerp(colSommet, 0.45f), p1L, colBase.Lerp(colSommet, 0.45f));
		AjouterTriangleCouleurParSommet(st, p1L, colBase.Lerp(colSommet, 0.45f), p1R, colBase.Lerp(colSommet, 0.45f), p2R, colSommet);
		AjouterTriangleCouleurParSommet(st, p1L, colBase.Lerp(colSommet, 0.45f), p2R, colSommet, p2L, colSommet);

		// Face arrière.
		AjouterTriangleCouleurParSommet(st, p1R, colBase.Lerp(colSommet, 0.45f), p0R, colBase, p0L, colBase);
		AjouterTriangleCouleurParSommet(st, p1L, colBase.Lerp(colSommet, 0.45f), p1R, colBase.Lerp(colSommet, 0.45f), p0L, colBase);
		AjouterTriangleCouleurParSommet(st, p2R, colSommet, p1R, colBase.Lerp(colSommet, 0.45f), p1L, colBase.Lerp(colSommet, 0.45f));
		AjouterTriangleCouleurParSommet(st, p2L, colSommet, p2R, colSommet, p1L, colBase.Lerp(colSommet, 0.45f));
	}

	private static Material ObtenirMaterielBuissonProcedural()
	{
		if (_cacheMaterielBuissonProcedural != null) return _cacheMaterielBuissonProcedural;
		if (_cacheTextureFeuilleBuisson == null)
		{
			var bruitFeuille = new FastNoiseLite { Seed = 34991 };
			bruitFeuille.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			bruitFeuille.Frequency = 0.13f;
			bruitFeuille.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
			bruitFeuille.FractalOctaves = 3;
			_cacheTextureFeuilleBuisson = new NoiseTexture2D
			{
				Width = 96,
				Height = 96,
				Noise = bruitFeuille
			};
		}
		var mat = new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = _cacheTextureFeuilleBuisson,
			Uv1Triplanar = true,
			Uv1WorldTriplanar = false,
			Uv1Scale = new Vector3(0.18f, 0.18f, 0.18f),
			Roughness = 0.95f,
			Metallic = 0f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			EmissionEnabled = true,
			Emission = new Color(0.02f, 0.02f, 0.02f)
		};
		_cacheMaterielBuissonProcedural = mat;
		return mat;
	}

	private static void AjouterFeuillePerforeeDoubleFace(SurfaceTool st, Vector3 centre, float largeur, float longueur, float angleY, float inclinaison, Color couleurBase, Color couleurSommet)
	{
		Vector3 axe = new Vector3(Mathf.Cos(angleY), 0f, Mathf.Sin(angleY));
		Vector3 droite = axe * (largeur * 0.5f);
		Vector3 levee = (Vector3.Up + new Vector3(-axe.Z, 0f, axe.X) * inclinaison).Normalized() * longueur;
		Vector3 pointes = levee * 0.88f;
		Vector3 departL = centre - droite;
		Vector3 departR = centre + droite;
		Vector3 milieuL = centre - droite * 0.38f + levee * 0.46f;
		Vector3 milieuR = centre + droite * 0.38f + levee * 0.46f;
		Vector3 sommetL = centre - droite * 0.16f + pointes;
		Vector3 sommetR = centre + droite * 0.16f + pointes;

		// Deux lobes avec fente centrale : silhouette moins "cube/rectangle" et aspect percé.
		AjouterTriangleCouleurParSommet(st, departL, couleurBase, milieuL, couleurBase.Lerp(couleurSommet, 0.6f), sommetL, couleurSommet);
		AjouterTriangleCouleurParSommet(st, departR, couleurBase, sommetR, couleurSommet, milieuR, couleurBase.Lerp(couleurSommet, 0.6f));
		AjouterTriangleCouleurParSommet(st, sommetL, couleurSommet, milieuL, couleurBase.Lerp(couleurSommet, 0.6f), departL, couleurBase);
		AjouterTriangleCouleurParSommet(st, milieuR, couleurBase.Lerp(couleurSommet, 0.6f), sommetR, couleurSommet, departR, couleurBase);
	}

	private static void AjouterCylindreSegment(SurfaceTool st, Vector3 basePos, Vector3 topPos, float rayonBase, float rayonTop, int cotes, Color couleurBase, Color couleurTop)
	{
		if (cotes < 3) cotes = 3;
		Vector3 axe = (topPos - basePos).Normalized();
		if (axe.LengthSquared() < 0.0001f) return;
		Vector3 tangent = Mathf.Abs(axe.Y) < 0.99f ? axe.Cross(Vector3.Up).Normalized() : axe.Cross(Vector3.Right).Normalized();
		Vector3 bitangent = axe.Cross(tangent).Normalized();

		for (int i = 0; i < cotes; i++)
		{
			float a0 = (i / (float)cotes) * Mathf.Tau;
			float a1 = ((i + 1) / (float)cotes) * Mathf.Tau;
			Vector3 rb0 = tangent * Mathf.Cos(a0) + bitangent * Mathf.Sin(a0);
			Vector3 rb1 = tangent * Mathf.Cos(a1) + bitangent * Mathf.Sin(a1);

			Vector3 b0 = basePos + rb0 * rayonBase;
			Vector3 b1 = basePos + rb1 * rayonBase;
			Vector3 t0 = topPos + rb0 * rayonTop;
			Vector3 t1 = topPos + rb1 * rayonTop;

			AjouterTriangleCouleurParSommet(st, b0, couleurBase, b1, couleurBase, t1, couleurTop);
			AjouterTriangleCouleurParSommet(st, b0, couleurBase, t1, couleurTop, t0, couleurTop);
		}
	}

	private static void AjouterBilleOctaedre(SurfaceTool st, Vector3 centre, float rayon, Color couleur)
	{
		Vector3 haut = centre + Vector3.Up * rayon;
		Vector3 bas = centre - Vector3.Up * rayon;
		Vector3 est = centre + Vector3.Right * rayon;
		Vector3 ouest = centre - Vector3.Right * rayon;
		Vector3 sud = centre + Vector3.Forward * rayon;
		Vector3 nord = centre - Vector3.Forward * rayon;

		AjouterTriangleCouleur(st, haut, est, sud, couleur);
		AjouterTriangleCouleur(st, haut, sud, ouest, couleur);
		AjouterTriangleCouleur(st, haut, ouest, nord, couleur);
		AjouterTriangleCouleur(st, haut, nord, est, couleur);
		AjouterTriangleCouleur(st, bas, sud, est, couleur);
		AjouterTriangleCouleur(st, bas, ouest, sud, couleur);
		AjouterTriangleCouleur(st, bas, nord, ouest, couleur);
		AjouterTriangleCouleur(st, bas, est, nord, couleur);
	}

	/// <summary>Baie avec relief visuel: corps rouge + mini reflet clair légèrement décentré.</summary>
	private static void AjouterBaieGourmande(SurfaceTool st, Vector3 centre, float rayon, Color couleurRouge)
	{
		AjouterBilleOctaedre(st, centre, rayon, couleurRouge);
		Vector3 offsetReflet = new Vector3(rayon * 0.32f, rayon * 0.34f, -rayon * 0.26f);
		Color reflet = couleurRouge.Lerp(new Color(1f, 0.86f, 0.9f, 1f), 0.72f);
		AjouterBilleOctaedre(st, centre + offsetReflet, rayon * 0.30f, reflet);
	}

	private static void AjouterTriangleCouleur(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color couleur)
	{
		st.SetColor(couleur); st.AddVertex(a);
		st.SetColor(couleur); st.AddVertex(b);
		st.SetColor(couleur); st.AddVertex(c);
	}

	private static void AjouterTriangleCouleurParSommet(SurfaceTool st, Vector3 a, Color ca, Vector3 b, Color cb, Vector3 c, Color cc)
	{
		st.SetColor(ca); st.AddVertex(a);
		st.SetColor(cb); st.AddVertex(b);
		st.SetColor(cc); st.AddVertex(c);
	}

	/// <summary>Génère un bouquet de lames fines pour un rendu moins "pics". Normales biaisées vers le ciel pour éclairage unifié.</summary>
	private static Mesh GenererMeshGazonProcedural()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		// FIX CRITIQUE : Création du canal de couleur pour autoriser le MultiMesh à peindre !
		st.SetColor(new Color(1f, 1f, 1f, 1f));

		// Lames plus fines + un peu plus nombreuses : silhouette "tiges" plus naturelle.
		float w = 0.034f;
		float h = 0.145f;

		void CreerLame(Vector3 centre, float angleY)
		{
			Vector3 axe = new Vector3(Mathf.Cos(angleY), 0f, Mathf.Sin(angleY)).Normalized();
			Vector3 lateral = new Vector3(-axe.Z, 0f, axe.X);
			float demiBase = w * 0.5f;
			float demiMilieu = w * 0.28f;
			float demiSommet = w * 0.10f;
			Vector3 p0L = centre - axe * demiBase;
			Vector3 p0R = centre + axe * demiBase;
			Vector3 centreMilieu = centre + new Vector3(0f, h * 0.55f, 0f) + lateral * 0.020f;
			Vector3 p1L = centreMilieu - axe * demiMilieu;
			Vector3 p1R = centreMilieu + axe * demiMilieu;
			Vector3 centreSommet = centre + new Vector3(0f, h, 0f) + lateral * 0.040f;
			Vector3 p2L = centreSommet - axe * demiSommet;
			Vector3 p2R = centreSommet + axe * demiSommet;

			// Normale biaisée vers le ciel (80% Up, 20% plan) : unifie l'éclairage avec le terrain, plus d'effet "X"
			Vector3 normal = (Vector3.Up * 0.8f + lateral * 0.2f).Normalized();

			st.SetNormal(normal); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0L);
			st.SetNormal(normal); st.SetUV(new Vector2(1, 1)); st.AddVertex(p0R);
			st.SetNormal(normal); st.SetUV(new Vector2(1, 0.45f)); st.AddVertex(p1R);
			st.SetNormal(normal); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0L);
			st.SetNormal(normal); st.SetUV(new Vector2(1, 0.45f)); st.AddVertex(p1R);
			st.SetNormal(normal); st.SetUV(new Vector2(0, 0.45f)); st.AddVertex(p1L);
			st.SetNormal(normal); st.SetUV(new Vector2(0, 0.45f)); st.AddVertex(p1L);
			st.SetNormal(normal); st.SetUV(new Vector2(1, 0.45f)); st.AddVertex(p1R);
			st.SetNormal(normal); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2R);
			st.SetNormal(normal); st.SetUV(new Vector2(0, 0.45f)); st.AddVertex(p1L);
			st.SetNormal(normal); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2R);
			st.SetNormal(normal); st.SetUV(new Vector2(0, 0)); st.AddVertex(p2L);
		}

		float rayonTouffe = 0.042f;
		for (int i = 0; i < 6; i++)
		{
			float a = i * (Mathf.Tau / 6f);
			Vector3 centre = new Vector3(Mathf.Cos(a) * rayonTouffe, 0f, Mathf.Sin(a) * rayonTouffe);
			float decalage = (i % 2 == 0) ? 0.16f : -0.11f;
			CreerLame(centre, a + decalage);
		}
		CreerLame(Vector3.Zero, Mathf.Pi * 0.23f);

		st.GenerateTangents();
		return st.Commit();
	}

	private static IEnumerable<Node> ObtenirTousLesNoeuds(Node n)
	{
		yield return n;
		foreach (Node enfant in n.GetChildren())
			foreach (Node descendant in ObtenirTousLesNoeuds(enfant))
				yield return descendant;
	}
}
