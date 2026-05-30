using Godot;
using System;
using System.Collections.Generic;

public partial class ItemPhysique : RigidBody3D
{
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
}
