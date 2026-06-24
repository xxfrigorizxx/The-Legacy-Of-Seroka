using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Dormance/réveil des corps dynamiques posés + recalage sous-sol. Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: mêmes budgets de traitement et mêmes règles de gel/dégel que l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	/// <summary>Gèle en masse les objets posés au repos après chargement (évite 300+ corps Jolt actifs).</summary>
	public void OptimiserPhysiqueObjetsPosesApresChargement()
	{
		if (_joueur == null || !IsInsideTree())
			return;
		int gelés = 0;
		foreach (Node n in GetTree().GetNodesInGroup("BlocsPoses"))
		{
			if (n is not ItemPhysique ip || !GodotObject.IsInstanceValid(ip))
				continue;
			if (ItemPhysique.EstMeublePoseStatique(ip.ID_Objet) || ip.EstEnReposAuSolOptimise)
				continue;
			if (ip.LinearVelocity.LengthSquared() > 0.35f || ip.AngularVelocity.LengthSquared() > 0.35f)
				continue;
			ip.PasserEnReposAuSolOptimise();
			gelés++;
		}
		if (gelés > 0)
			GD.Print($"ZERO-K PERF : {gelés} objet(s) posé(s) passé(s) en repos optimisé après chargement.");
		RafraichirCacheDormanceGroupes(0f, force: true);
		InvaliderCacheLodObjetsAuSol();
	}

	private void MettreAJourDormanceObjetsPoses(float dt)
	{
		if (_joueur == null) return;
		RafraichirCacheDormanceGroupes(dt);
		Vector2I chunkJoueur = WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
		int rayon = RayonDormanceObjetsChunks;
		bool useGardeTerrain = UseArchitectureReseau && _mondeClient != null;
		int rayonSecuriteTerrain = Mathf.Clamp(RayonSecuriteTerrainObjetsChunks, 0, 2);

		int budgetTotal = Mathf.Max(16, BudgetDormanceObjetsParCycle);
		float fps = (float)Engine.GetFramesPerSecond();
		if (fps < 18f)
			budgetTotal = Mathf.Max(10, budgetTotal / 4);
		else if (fps < 30f)
			budgetTotal = Mathf.Max(14, budgetTotal / 2);
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
		int traite = 0;
		int securite = 0;
		int limiteSecurite = Math.Max(iterations * 4, total * 2);
		while (traite < iterations && securite < limiteSecurite)
		{
			securite++;
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
			if (rb is ItemPhysique ipRepos && ipRepos.EstEnReposAuSolOptimise)
				continue;
			if (ignorerRacks && rb is ItemPhysique ip && ItemPhysique.EstMeublePoseStatique(ip.ID_Objet))
				continue;
			traite++;
			AppliquerDormanceRigidBody(rb, chunkJoueur, rayon, useGardeTerrain, rayonSecuriteTerrain, ref budgetFiletSecurite);
		}
	}

	private const float SeuilReposLineaireDormance2 = 0.12f;
	private const float SeuilReposAngulaireDormance2 = 0.16f;

	private static bool EstRigidBodyQuasiImmobile(RigidBody3D rb)
	{
		if (!GodotObject.IsInstanceValid(rb))
			return false;
		if (rb.Sleeping)
			return true;
		// Chute libre encore perceptible : ne pas geler (évite un corps figé en l'air au sommet d'un rebond).
		if (rb.LinearVelocity.Y < -0.55f)
			return false;
		return rb.LinearVelocity.LengthSquared() <= SeuilReposLineaireDormance2
			&& rb.AngularVelocity.LengthSquared() <= SeuilReposAngulaireDormance2;
	}

	/// <summary>Raycast vers le bas : sol ou autre objet posé (même couche 1) — piles d'os / butin.</summary>
	private bool EstRigidBodyAppuyeSurSupport(RigidBody3D rb)
	{
		if (rb == null || !GodotObject.IsInstanceValid(rb) || !rb.IsInsideTree())
			return false;
		if (!EstCollisionTerrainChunkPretPourPoint(rb.GlobalPosition))
			return false;
		if (UseArchitectureReseau && _mondeClient != null
			&& !_mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, 1))
			return false;

		PhysicsDirectSpaceState3D espace = rb.GetWorld3D()?.DirectSpaceState;
		if (espace == null)
			return true;

		Vector3 pos = rb.GlobalPosition;
		var requete = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 0.25f, pos + Vector3.Down * 3.5f);
		requete.CollisionMask = 1;
		requete.CollideWithAreas = false;
		requete.Exclude = new Godot.Collections.Array<Rid> { rb.GetRid() };
		var impact = espace.IntersectRay(requete);
		if (impact.Count == 0 || !impact.ContainsKey("position"))
			return false;

		float ySupport = ((Vector3)impact["position"]).Y;
		float ecart = pos.Y - ySupport;
		return ecart <= 2.5f && ecart >= -0.55f;
	}

	private void AppliquerDormanceRigidBody(RigidBody3D rb, Vector2I chunkJoueur, int rayon, bool useGardeTerrain, int rayonSecuriteTerrain, ref int budgetFiletSecurite)
	{
		if (rb is ItemPhysique ipDejaGele && ipDejaGele.EstEnReposAuSolOptimise)
			return;
		bool terrainPret = !useGardeTerrain || _mondeClient.CollisionTerrainActiveAutourPoint(rb.GlobalPosition, rayonSecuriteTerrain);
		bool structureStatique = rb is ItemPhysique ipStatique && ItemPhysique.EstMeublePoseStatique(ipStatique.ID_Objet);
		if (structureStatique)
			return;

		if (!structureStatique && budgetFiletSecurite > 0
			&& !_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
		{
			budgetFiletSecurite--;
			EssayerRecalerRigidBodySousSol(rb, terrainPret);
		}

		if (!terrainPret)
		{
			EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
			return;
		}
		if (_rigidBodiesAttenteCollisionSolRestauration.Contains(rb))
			return;

		// Pile dense (os, intestins, cuir…) : une fois quasi immobile et appuyé, geler pour stopper la rotation infinie.
		if (!structureStatique && EstRigidBodyQuasiImmobile(rb) && EstRigidBodyAppuyeSurSupport(rb))
		{
			if (EssayerRefusionnerBlocChutantTerrain(rb))
				return;
			if (!rb.Freeze)
				FigerRigidBodyDormance(rb);
			return;
		}

		// En mouvement : dégel dormance uniquement (le contact joueur/outil réveille aussi via ImpactCombat / combat).
		if (rb.Freeze)
		{
			if (rb is ItemPhysique ip)
				ip.ReveillerPhysiqueAuSol();
			else
				rb.Freeze = false;
		}
	}

	private static void FigerRigidBodyDormance(RigidBody3D rb)
	{
		if (rb is ItemPhysique ip)
		{
			ip.PasserEnReposAuSolOptimise();
			return;
		}
		rb.LinearVelocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		rb.Sleeping = true;
		rb.Freeze = true;
		rb.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
		rb.ContinuousCd = false;
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

		// Pas de mesh collision encore (grotte, chunk en chargement) : attendre — ne jamais téléporter
		// vers la surface procédurale (une grotte creusée est volontairement sous la surface du monde).
		EnregistrerRigidBodyRestaurationSolSiCollisionManquante(rb);
	}

	private Vector2I ObtenirCoordonneesChunkJoueur()
	{
		if (_joueur == null) return Vector2I.Zero;
		return WorldToChunkCoord(_joueur.GlobalPosition, TailleChunk);
	}

	private bool EssayerRefusionnerBlocChutantTerrain(RigidBody3D rb)
	{
		if (!UseArchitectureReseau || _mondeServeur == null || rb is not BlocChutant)
			return false;
		if (!rb.HasMeta("ID_Matiere"))
			return false;
		if (rb.HasMeta("DimensionId") && rb.GetMeta("DimensionId").AsInt32() != _mondeServeur.ObtenirDimensionServeurId())
			return false;
		byte mat = (byte)Mathf.Clamp(rb.GetMeta("ID_Matiere").AsInt32(), 0, 255);
		if (!Monde_Serveur.EstMateriauTerrainRefusionnable(mat))
			return false;
		if (!_mondeServeur.PeutRefusionnerMaintenant())
			return false;
		if (!_mondeServeur.RefusionnerVoxelTerrain(rb.GlobalPosition, mat))
			return false;
		rb.QueueFree();
		return true;
	}
}
