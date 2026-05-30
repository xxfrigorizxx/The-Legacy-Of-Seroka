using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Pipeline de spawn (débris, ensemencement roches, stase, dégel dynamique). Partie de <see cref="Monde_Serveur"/>.
/// CONTRAT: budgets de micro-dosage et règles de dormance/réveil identiques à l'historique.
/// </summary>
public partial class Monde_Serveur : Node
{
	private void SpawnBlocChutant(Vector3 pos, byte mat, bool brancheTailléeBuisson = false, byte indexCouleurBaie = 0)
	{
		if (_parentPourBlocsChutants == null) return;
		var matTerrain = MaterielTerrain ?? GD.Load<Material>("res://Manteau_Planetaire.tres");
		var bloc = BlocChutant.Creer(pos, mat, matTerrain, brancheTailléeBuisson, indexCouleurBaie);
		_parentPourBlocsChutants.AddChild(bloc);
		bloc.SetMeta("DimensionId", _dimensionServeurId);
		// Fibres (fauchage) : léger décalage vers le haut pour éviter d’être coincées dans le sol / la collision.
		Vector3 posPose = mat == 15 ? pos + new Vector3(0f, 0.12f, 0f) : pos;
		bloc.GlobalPosition = posPose;
	}

	/// <summary>Spawn branches et bûches qui tombent au sol quand un arbre est coupé.</summary>
	public void SpawnDebrisArbre(Vector3 baseArbre, int ageEnJours, uint seed)
	{
		if (_parentPourBlocsChutants == null) return;
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Mathf.Abs(baseArbre.X) * 73856 + Mathf.Abs(baseArbre.Z) * 19349 + seed);
		int nbBranches = Mathf.Clamp(ageEnJours * 2 + (int)(rng.Randf() * 4), 2, 12);
		int nbBuches = Mathf.Clamp(ageEnJours / 2 + (int)(rng.Randf() * 2), 1, 6);
		float offsetRayon = 0.8f + ageEnJours * 0.1f;
		for (int i = 0; i < nbBranches; i++)
		{
			float angle = (float)i / nbBranches * Mathf.Tau + rng.Randf() * 0.5f;
			float r = offsetRayon * (0.5f + rng.Randf() * 0.5f);
			Vector3 pos = baseArbre + new Vector3(Mathf.Cos(angle) * r, 0.5f + rng.Randf() * 0.3f, Mathf.Sin(angle) * r);
			SpawnBlocChutant(pos, BlocChutant.ID_BRANCHE);
		}
		for (int i = 0; i < nbBuches; i++)
		{
			float angle = (float)i / nbBuches * Mathf.Tau + rng.Randf() * 0.8f;
			float r = offsetRayon * (0.3f + rng.Randf() * 0.4f);
			Vector3 pos = baseArbre + new Vector3(Mathf.Cos(angle) * r, 0.6f + rng.Randf() * 0.4f, Mathf.Sin(angle) * r);
			SpawnBlocChutant(pos, BlocChutant.ID_BOIS);
		}
	}

	private const float NIVEAU_EAU = 103f;  // +1 m
	private const float DECALAGE_SPAWN_VERTICAL = 1.2f; // Légèrement au-dessus du terrain à la génération, tombe quand réveillé
	/// <summary>Rayon en chunks : objets dynamiques gelés se réveillent dans cette zone autour du joueur.</summary>
	private const int RAYON_ACTIVATION_PIERRES_CHUNKS = 5;

	/// <summary>Délai de synchronisation : attend 2 frames physiques, puis enfile sur le tapis roulant (ordre spatial logique). Si onStasePrete est fourni (chunk procédural), on enqueue l'envoi client seulement après la stase → évite LibererRochesChunk à vide.</summary>
	internal async void DeclencherEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk, Action<Vector2I, Chunk_Serveur> onStasePrete = null)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		var positionsFiltrees = CollecterPositionsEnsemencement(chunkCoord, chunk, tailleChunk);
		var aEnfiler = new List<(Vector3 pos, int id, int indexCache, int indexChimique)>();
		foreach (var p in positionsFiltrees)
			aEnfiler.Add((p.pos, p.idMat, p.idxMorph, p.taille));
		MettreRochesEnStase(chunkCoord, aEnfiler);
		onStasePrete?.Invoke(chunkCoord, chunk);
	}

	/// <summary>Pré-crée les pools par matière rocheuse (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>).</summary>
	private void CreerPoolsRochesParTaille()
	{
		if (_parentPourBlocsChutants == null) return;
		int n = 0;
		for (int id = ItemPhysique.IdRocheMatiereMin; id <= ItemPhysique.IdRocheMatiereMax; id++)
		{
			_poolsRochesParTaille[id] = new List<RigidBody3D>();
			for (int i = 0; i < TaillePoolParType; i++)
			{
				var rb = CreerNouvelleRoche(id, 0, 2);
				_poolsRochesParTaille[id].Add(rb);
			}
			n++;
		}
		GD.Print($"ZERO-K : Pools roches par matière créés ({n} x {TaillePoolParType}).");
	}

	/// <summary>Collecte positions, ID matière (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>), morph (-1 = tirage), taille (0–4).</summary>
	private List<(Vector3 pos, int idMat, int idxMorph, int taille)> CollecterPositionsEnsemencement(Vector2I chunkCoord, Chunk_Serveur chunk, float tailleChunk)
	{
		var liste = new List<(Vector3 pos, int idMat, int idxMorph, int taille)>();
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(chunkCoord.X * 73856093 + chunkCoord.Y * 19349663 + SeedTerrain);

		for (float x = 0; x < tailleChunk; x += 3f)
		{
			for (float z = 0; z < tailleChunk; z += 3f)
			{
				if (rng.Randf() > 0.02f) continue;
				int lx = Mathf.Clamp(Mathf.FloorToInt(x), 0, (int)tailleChunk);
				int lz = Mathf.Clamp(Mathf.FloorToInt(z), 0, (int)tailleChunk);
				var (ySurface, idMatiere) = chunk.ObtenirSurfaceEtMateriau(lx, lz);
				if (ySurface < 0) continue;

				Vector3 pointImpact = new Vector3(
					chunkCoord.X * tailleChunk + x + 0.5f,
					ySurface + 0.5f,
					chunkCoord.Y * tailleChunk + z + 0.5f
				);
				Vector3 pointDeSpawnSecurise = pointImpact + new Vector3(0, DECALAGE_SPAWN_VERTICAL, 0);

				if (idMatiere == 3 && pointImpact.Y < NIVEAU_EAU)
				{
					liste.Add((pointDeSpawnSecurise, ItemPhysique.IdRocheMatiereMin + ItemPhysique.IndexChimiqueSilex, -1, 1));
					continue;
				}

				int tailleSpawn = 0;
				float proba = rng.Randf();
				if (idMatiere == 1 || idMatiere == 3) tailleSpawn = 1;
				else if (idMatiere == 7 || idMatiere == 8) tailleSpawn = (proba > 0.4f) ? 1 : 2;
				else if (idMatiere == 5 || idMatiere == 6) tailleSpawn = (proba > 0.5f) ? 1 : 2;
				else if (idMatiere == 2)
				{
					if (proba < 0.40f) tailleSpawn = 1;
					else if (proba < 0.70f) tailleSpawn = 2;
					else if (proba < 0.90f) tailleSpawn = 3;
					else tailleSpawn = 4;
				}

				if (tailleSpawn != 0)
				{
					int chimIdx = rng.RandiRange(0, ItemPhysique.TableGeologique.Length - 1);
					liste.Add((pointDeSpawnSecurise, ItemPhysique.IdRocheMatiereMin + chimIdx, -1, tailleSpawn));
				}
			}
		}
		return liste;
	}

	/// <summary>Chambre de stase : les roches attendent leur sol. Pas de spawn tant que le chunk n'est pas scellé (envoyé).</summary>
	private void MettreRochesEnStase(Vector2I coordChunk, List<(Vector3 pos, int id, int indexCache, int indexChimique)> pierres)
	{
		if (pierres.Count == 0) return;
		pierres.Sort((a, b) =>
		{
			int cmpX = a.pos.X.CompareTo(b.pos.X);
			if (cmpX != 0) return cmpX;
			int cmpZ = a.pos.Z.CompareTo(b.pos.Z);
			if (cmpZ != 0) return cmpZ;
			return a.pos.Y.CompareTo(b.pos.Y);
		});
		_rochesEnStase[coordChunk] = pierres;
	}

	/// <summary>Signal de fondation : chunk scellé (envoyé au client) → on transfère ses roches vers la file de micro-dosage (3 par frame).</summary>
	internal void LibererRochesChunk(Vector2I coordChunk)
	{
		if (!_rochesEnStase.TryGetValue(coordChunk, out var liste)) return;
		foreach (var p in liste)
			_filePierresAInstancier.Enqueue(p);
		_rochesEnStase.Remove(coordChunk);
	}

	/// <summary>Enfile cailloux et silex sur le tapis roulant en ordre spatial logique (X, Z, Y) : terrain cohérent.</summary>
	private void EnfilerPierresSurTapisRoulant(List<(Vector3 pos, int id, int indexCache, int indexChimique)> pierres)
	{
		if (pierres.Count == 0) return;
		pierres.Sort((a, b) =>
		{
			int cmpX = a.pos.X.CompareTo(b.pos.X);
			if (cmpX != 0) return cmpX;
			int cmpZ = a.pos.Z.CompareTo(b.pos.Z);
			if (cmpZ != 0) return cmpZ;
			return a.pos.Y.CompareTo(b.pos.Y);
		});
		foreach (var p in pierres)
			_filePierresAInstancier.Enqueue((p.pos, p.id, p.indexCache, p.indexChimique));
	}

	/// <summary>Roches matière (ID <see cref="ItemPhysique.IdRocheMatiereMin"/>–<see cref="ItemPhysique.IdRocheMatiereMax"/>) : <paramref name="indexCache"/> = morph (-1/-2 = tirage), <paramref name="indexChimique"/> = <see cref="ItemPhysique.IndexTailleRoche"/> (0–4).</summary>
	private void GenererItemPhysique(Vector3 position, int idObjet, int indexCache = -1, int indexChimique = -1)
	{
		if (_parentPourBlocsChutants == null) return;
		ItemPhysique rb = null;
		if (_poolsRochesParTaille.TryGetValue(idObjet, out var pool) && pool.Count > 0)
		{
			rb = pool[pool.Count - 1] as ItemPhysique;
			pool.RemoveAt(pool.Count - 1);
			if (rb != null)
			{
				rb.ID_Objet = idObjet;
				if (indexCache == -2)
					rb.IndexCacheMemoire = GD.RandRange(2, 3);
				else if (indexCache < 0)
					rb.IndexCacheMemoire = GD.RandRange(0, 3);
				else
					rb.IndexCacheMemoire = Mathf.Clamp(indexCache, 0, 3);
				rb.IndexTailleRoche = indexChimique >= 0 ? Mathf.Clamp(indexChimique, 0, 4) : 2;
				rb.IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet);
				rb.ReappliquerApparence();
				rb.Freeze = true; // Stase : ReveillerPierresDansRayon dégèle à 2 chunks (terrain solide)
			}
		}
		else
			rb = CreerNouvelleRoche(idObjet, indexCache, indexChimique);
		try
		{
			_parentPourBlocsChutants.AddChild(rb);
			rb.GlobalPosition = position;
			rb.Freeze = true; // Dormance : gravité seulement à 2 chunks du joueur (évite chute dans le vide)
			rb.SetMeta("DimensionId", _dimensionServeurId);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K CRASH ÉVITÉ : Objet physique échoué à l'instanciation. {ex.Message}");
			rb?.QueueFree();
		}
	}

	/// <summary>Crée une roche neuve (ItemPhysique = RigidBody3D racine). N'est pas ajoutée au parent.</summary>
	private ItemPhysique CreerNouvelleRoche(int idObjet, int indexCache, int indexTailleOuChim)
	{
		int morph;
		if (indexCache == -2)
			morph = GD.RandRange(2, 3);
		else if (indexCache < 0)
			morph = GD.RandRange(0, 3);
		else
			morph = Mathf.Clamp(indexCache, 0, 3);
		int taille = indexTailleOuChim >= 0 ? Mathf.Clamp(indexTailleOuChim, 0, 4) : 2;
		float rayon = ItemPhysique.RayonBaseRochesJoueur(taille);
		var item = new ItemPhysique
		{
			ID_Objet = idObjet,
			IndexCacheMemoire = morph,
			IndexTailleRoche = taille,
			IndexChimique = ItemPhysique.IndexChimiqueDepuisIdRoche(idObjet),
			Name = "ItemPhysique",
			// Morphologie appliquée sur le MeshInstance3D dans ItemPhysique._Ready (Jolt : pas d’échelle non uniforme sur RigidBody3D).
			Scale = Vector3.One
		};
		item.Mass = 1.0f;
		// Friction / amortissement : ItemPhysique._Ready → AppliquerPhysiqueRochePortee (évite conflit avec matériau 0,6).
		item.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = rayon, Height = rayon * 2f } });
		item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = rayon } });
		return item;
	}

	/// <summary>Rayon en unités : pierres gelées se réveillent quand joueur entre (2 chunks = terrain chargé).</summary>
	private float RayonActivationPierres => RAYON_ACTIVATION_PIERRES_CHUNKS * TailleChunk;

	private bool TerrainChargeAutourPosition(Vector3 posMonde)
	{
		Vector2I c = Gestionnaire_Monde.WorldToChunkCoord(posMonde, TailleChunk);
		int rayon = Mathf.Clamp(RayonSecuriteTerrainReveilPierres, 0, 2);
		for (int dx = -rayon; dx <= rayon; dx++)
			for (int dz = -rayon; dz <= rayon; dz++)
				if (!_chunks.ContainsKey(new Vector2I(c.X + dx, c.Y + dz)))
					return false;
		return true;
	}

	/// <summary>Réveille les objets dynamiques dans le rayon, endort les lointains (charge CPU réduite côté serveur).</summary>
	private void ReveillerPierresDansRayon()
	{
		if (_parentPourBlocsChutants == null || _obtenirPositionJoueur == null) return;
		int dimensionActive = _obtenirDimensionActive?.Invoke() ?? _dimensionServeurId;
		Vector3 posJoueur = InvokerPositionJoueurStreaming();
		float rayonCarre = RayonActivationPierres * RayonActivationPierres;
		foreach (Node child in _parentPourBlocsChutants.GetChildren())
		{
			if (child is not RigidBody3D rb) continue;
			// Chaque Monde_Serveur ne doit traiter que ses propres corps : sinon <see cref="TerrainChargeAutourPosition"/>
			// utilise la grille <c>_chunks</c> de cette instance (ex. Alpha) alors que le caillou appartient à Beta → gel permanent en l’air.
			if (rb.HasMeta("DimensionId"))
			{
				if (rb.GetMeta("DimensionId").AsInt32() != _dimensionServeurId)
					continue;
			}
			else if (_dimensionServeurId != (int)DimensionJeu.Alpha && !ActiverGenerationAbysse)
			{
				// Héritage : clones Alpha-like utilisent toujours DimensionId ; Alpha + APISARA peuvent encore avoir d’anciens corps sans balise.
				continue;
			}
			int id = 0;
			if (rb is ItemPhysique item)
				id = item.ID_Objet;
			else if (rb.HasMeta("ID_Matiere"))
				id = rb.GetMeta("ID_Matiere").AsInt32();
			if (!TryGetPositionMonde(rb, out Vector3 posRb)) continue;
			bool structureFixe = id == 200 || id == Joueur.IdObjetTableAnalyseTier1 || id == Joueur.IdObjetRackBatons || id == Joueur.IdObjetRackBuches || id == Joueur.IdObjetCoffreBoisTier0;
			// Le joueur « dimensionnel » n’est pas dans ce monde : on regèle pour éviter des corps actifs fantômes.
			if (dimensionActive != _dimensionServeurId)
			{
				if (!structureFixe)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
				continue;
			}
			float distCarre = posRb.DistanceSquaredTo(posJoueur);
			if (distCarre <= rayonCarre)
			{
				bool terrainPret = TerrainChargeAutourPosition(posRb);
				if (!structureFixe && terrainPret)
				{
					rb.Freeze = false; // Réveiller : gravité + collisions
					rb.Sleeping = false;
				}
				else if (!structureFixe)
				{
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					rb.Freeze = true;
				}
			}
			else
			{
				rb.LinearVelocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				rb.Sleeping = true;
				if (!structureFixe)
					rb.Freeze = true;
			}
		}
	}

	public void ForcerPulseReveilPierres()
	{
		ReveillerPierresDansRayon();
	}

	/// <summary>Évite l’erreur Godot <c>!is_inside_tree()</c> sur GlobalPosition (ex. sauvegarde pendant <c>_ExitTree</c>).</summary>
	private static bool TryGetPositionMonde(Node3D node, out Vector3 worldPos)
	{
		worldPos = default;
		if (node == null) return false;
		if (node.IsInsideTree())
		{
			worldPos = node.GlobalPosition;
			return true;
		}
		if (node.GetParent() is Node3D parent && parent.IsInsideTree())
		{
			worldPos = parent.GlobalTransform * node.Position;
			return true;
		}
		return false;
	}
}
