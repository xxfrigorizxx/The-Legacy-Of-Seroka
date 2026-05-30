using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Dormance/réveil des corps dynamiques posés + recalage sous-sol. Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: mêmes budgets de traitement et mêmes règles de gel/dégel que l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void MettreAJourDormanceObjetsPoses(float dt)
	{
		if (_joueur == null) return;
		RafraichirCacheDormanceGroupes(dt);
		Vector2I chunkJoueur = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
		int rayon = RayonDormanceObjetsChunks;
		bool useGardeTerrain = UseArchitectureReseau && _mondeClient != null;
		int rayonSecuriteTerrain = Mathf.Clamp(RayonSecuriteTerrainObjetsChunks, 0, 2);

		int budgetTotal = Mathf.Max(16, BudgetDormanceObjetsParCycle);
		int budgetBlocs = Mathf.Max(1, Mathf.RoundToInt(budgetTotal * 0.65f));
		int budgetDyn = Mathf.Max(1, budgetTotal - budgetBlocs);
		int budgetFiletSecurite = ActiverFiletSecuriteObjetsDynamiques
			? Mathf.Clamp(BudgetFiletSecuriteObjetsParCycle, 1, budgetTotal)
			: 0;
		TraiterDormanceGroupe("BlocsPoses", ref _indexDormanceBlocsPoses, budgetBlocs, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ignorerRacks: true, ref budgetFiletSecurite);
		TraiterDormanceGroupe("ObjetsDormantsDynamiques", ref _indexDormanceObjetsDyn, budgetDyn, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ignorerRacks: false, ref budgetFiletSecurite);
	}

	private void TraiterDormanceGroupe(string nomGroupe, ref int indexCurseur, int budget, Vector2I chunkJoueur, int rayon, bool useGardeTerrain, int rayonSecuriteTerrain, bool ignorerRacks, ref int budgetFiletSecurite)
	{
		if (!_cacheRigidBodiesDormance.TryGetValue(nomGroupe, out List<RigidBody3D> noeuds))
		{
			RafraichirCacheDormanceGroupes(0f, force: true);
			if (!_cacheRigidBodiesDormance.TryGetValue(nomGroupe, out noeuds))
				return;
		}
		int total = noeuds.Count;
		if (total == 0) { indexCurseur = 0; return; }
		if (indexCurseur >= total) indexCurseur = 0;
		int iterations = Math.Min(Mathf.Max(1, budget), total);
		for (int i = 0; i < iterations; i++)
		{
			total = noeuds.Count;
			if (total <= 0) { indexCurseur = 0; return; }
			if (indexCurseur >= total) indexCurseur = 0;
			if ((uint)indexCurseur >= (uint)noeuds.Count) break;
			RigidBody3D rb = noeuds[indexCurseur++];
			if (rb == null || !GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree())
			{
				int idxSuppr = Mathf.Clamp(indexCurseur - 1, 0, noeuds.Count - 1);
				if (idxSuppr >= 0 && idxSuppr < noeuds.Count) noeuds.RemoveAt(idxSuppr);
				total = noeuds.Count;
				indexCurseur = Mathf.Clamp(indexCurseur - 1, 0, Math.Max(0, total - 1));
				if (total == 0) { indexCurseur = 0; return; }
				continue;
			}
			if (ignorerRacks && rb is ItemPhysique ip && ItemPhysique.EstMeublePoseStatique(ip.ID_Objet))
				continue;
			AppliquerDormanceRigidBody(rb, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ref budgetFiletSecurite);
		}
	}

	private void AppliquerDormanceRigidBody(RigidBody3D rb, Vector2I chunkJoueur, int rayon, bool useGardeTerrain, int rayonSecuriteTerrain, ref int budgetFiletSecurite)
	{
		Vector2I c = WorldToChunkCoord(rb.GlobalPosition, TailleChunk);
		bool dansRayon = Mathf.Abs(c.X - chunkJoueur.X) <= rayon && Mathf.Abs(c.Y - chunkJoueur.Y) <= rayon;
		bool terrainPret = !useGardeTerrain || _mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, rayonSecuriteTerrain);
		bool itemLegerPetit = ItemPhysique.EstRigidBodyLegerEtPetitReactif(rb);
		bool structureStatique = rb is ItemPhysique ipStatique && ItemPhysique.EstMeublePoseStatique(ipStatique.ID_Objet);

		if (!structureStatique && budgetFiletSecurite > 0
			&& !_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
		{
			budgetFiletSecurite--;
			EssayerRecalerRigidBodySousSol(rb, terrainPret);
		}

		if (itemLegerPetit && _joueur != null && GodotObject.IsInstanceValid(_joueur))
		{
			float dist2 = rb.GlobalPosition.DistanceSquaredTo(_joueur.GlobalPosition);
			if (dist2 <= 6f * 6f)
			{
				if (!terrainPret)
				{
					EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
					return;
				}
				if (_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
					return;
				// Dégeler seulement la dormance — ne pas réveiller un objet déjà au repos sur le sol.
				if (rb.Freeze) rb.Freeze = false;
				return;
			}
		}

		// Priorité gameplay: un objet proche du joueur ne doit jamais rester figé en l'air.
		if (dansRayon)
		{
			if (!terrainPret)
			{
				EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
				return;
			}
			if (_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
				return;
			if (rb.Freeze) rb.Freeze = false;
			return;
		}

		if (itemLegerPetit && terrainPret)
		{
			if (rb.Freeze) rb.Freeze = false;
			return;
		}
		// Lointain : figer seulement si encore actif (évite de casser le repos naturel Sleeping au sol).
		if (!terrainPret || rb.Freeze || !rb.Sleeping)
		{
			if (!terrainPret || rb.LinearVelocity.LengthSquared() > 0.08f || rb.AngularVelocity.LengthSquared() > 0.08f || rb.Freeze)
				FigerRigidBodyDormance(rb);
		}
	}

	private static void FigerRigidBodyDormance(RigidBody3D rb)
	{
		rb.LinearVelocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		rb.Sleeping = true;
		rb.Freeze = true;
	}

	private void EssayerRecalerRigidBodySousSol(RigidBody3D rb, bool terrainPret)
	{
		if (!terrainPret || !GodotObject.IsInstanceValid(rb))
			return;

		// Objet au repos : le filet ne doit pas le téléporter (cause principale du « saut » après pose).
		if (rb.Sleeping
			&& rb.LinearVelocity.LengthSquared() < 0.06f
			&& rb.AngularVelocity.LengthSquared() < 0.06f)
			return;

		Vector3 pos = rb.GlobalPosition;
		PhysicsDirectSpaceState3D espace = rb.GetWorld3D()?.DirectSpaceState;
		if (espace != null)
		{
			var requete = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 0.35f, pos + Vector3.Down * 8f);
			requete.CollisionMask = 1;
			requete.CollideWithAreas = false;
			requete.Exclude = new Godot.Collections.Array<Rid> { rb.GetRid() };
			var impact = espace.IntersectRay(requete);
			if (impact.Count > 0 && impact.ContainsKey("position"))
			{
				float ySol = ((Vector3)impact["position"]).Y;
				float ecart = pos.Y - ySol;
				// Déjà posé sur le mesh collision réel : ne pas remonter vers la hauteur procédurale.
				if (ecart >= -0.3f)
					return;
				// Enfoui sous le mesh seulement : petit recal au contact, sans filet de dégel.
				if (ecart >= -1.5f)
				{
					float yCorrige = ySol + Mathf.Max(0.02f, MargeRemonteeObjetsMetres);
					if (Mathf.Abs(yCorrige - pos.Y) < 0.02f)
						return;
					rb.GlobalPosition = new Vector3(pos.X, yCorrige, pos.Z);
					rb.LinearVelocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					rb.Sleeping = true;
					return;
				}
			}
		}

		// Chute dans le vide (pas de sol raycast) : dernier recours procédural, puis attente collision.
		int x = Mathf.FloorToInt(pos.X);
		int z = Mathf.FloorToInt(pos.Z);
		int h = _dimensionLocaleActive == (int)DimensionJeu.Abysse
			? ApisaraHauteurTerrain.ObtenirHauteurSolMonde(x, z, SeedTerrain)
			: Generateur_Voxel.ObtenirHauteurTerrainMonde(x, z, SeedTerrain);
		float ySurface = h + 1.0f;
		if (pos.Y >= ySurface - 0.6f)
			return;

		float yCorrigeProc = ySurface + Mathf.Max(0.02f, MargeRemonteeObjetsMetres);
		rb.GlobalPosition = new Vector3(pos.X, yCorrigeProc, pos.Z);
		rb.LinearVelocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
	}

	private Vector2I ObtenirCoordonneesChunkJoueur()
	{
		if (_joueur == null) return Vector2I.Zero;
		return WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
	}
}
