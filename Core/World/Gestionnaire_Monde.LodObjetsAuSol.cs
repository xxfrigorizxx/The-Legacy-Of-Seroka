using Godot;
using System.Collections.Generic;

/// <summary>LOD client des objets au sol : moins de draw + moins de Jolt hors rayon d'intérêt.</summary>
public partial class Gestionnaire_Monde : Node3D
{
	[ExportGroup("LOD objets au sol (client)")]
	[Export] public bool ActiverLodObjetsAuSol = true;
	[Export] public float RayonVisuelObjetsAuSolMetres = 40f;
	[Export] public float RayonCollisionObjetsAuSolMetres = 16f;
	[Export] public int BudgetLodObjetsAuSolParPasse = 140;

	private float _cooldownLodObjetsAuSol;
	private int _indexLodObjetsAuSol;
	private readonly List<ItemPhysique> _cacheLodObjetsAuSol = new List<ItemPhysique>(512);
	private float _cooldownRebuildCacheLod;

	private void MettreAJourLodObjetsAuSol(float dt)
	{
		if (!ActiverLodObjetsAuSol || _joueur == null || !IsInsideTree())
			return;
		_cooldownLodObjetsAuSol -= dt;
		if (_cooldownLodObjetsAuSol > 0f)
			return;
		float fps = (float)Engine.GetFramesPerSecond();
		_cooldownLodObjetsAuSol = fps < 22f ? 0.32f : 0.18f;
		AssurerCacheLodObjetsAuSol(dt);
		int n = _cacheLodObjetsAuSol.Count;
		if (n == 0)
			return;
		if (_indexLodObjetsAuSol >= n)
			_indexLodObjetsAuSol = 0;
		Vector3 posJ = _joueur.GlobalPosition;
		float rVis = Mathf.Max(18f, RayonVisuelObjetsAuSolMetres);
		float rCol = Mathf.Max(8f, RayonCollisionObjetsAuSolMetres);
		if (fps < 18f)
		{
			rVis *= 0.72f;
			rCol *= 0.85f;
		}
		float rVis2 = rVis * rVis;
		float rCol2 = rCol * rCol;
		int budget = fps < 18f ? Mathf.Max(48, BudgetLodObjetsAuSolParPasse / 2) : BudgetLodObjetsAuSolParPasse;
		int traite = 0;
		while (traite < budget && traite < n)
		{
			if (_indexLodObjetsAuSol >= n)
				_indexLodObjetsAuSol = 0;
			ItemPhysique ip = _cacheLodObjetsAuSol[_indexLodObjetsAuSol++];
			if (!GodotObject.IsInstanceValid(ip) || !ip.IsInsideTree())
				continue;
			float d2 = ip.GlobalPosition.DistanceSquaredTo(posJ);
			bool procheVisuel = d2 <= rVis2;
			bool procheCollision = d2 <= rCol2;
			ip.AppliquerLodClientObjetAuSol(procheVisuel, procheCollision);
			traite++;
		}
	}

	private void AssurerCacheLodObjetsAuSol(float dt)
	{
		_cooldownRebuildCacheLod -= dt;
		if (_cooldownRebuildCacheLod > 0f && _cacheLodObjetsAuSol.Count > 0)
			return;
		_cooldownRebuildCacheLod = 2.5f;
		_cacheLodObjetsAuSol.Clear();
		foreach (Node n in GetTree().GetNodesInGroup("BlocsPoses"))
		{
			if (n is ItemPhysique ip && GodotObject.IsInstanceValid(ip) && ip.IsInsideTree())
			{
				if (ItemPhysique.EstMeublePoseStatique(ip.ID_Objet))
					continue;
				_cacheLodObjetsAuSol.Add(ip);
			}
		}
		_indexLodObjetsAuSol %= Mathf.Max(1, _cacheLodObjetsAuSol.Count);
	}

	internal void InvaliderCacheLodObjetsAuSol()
	{
		_cooldownRebuildCacheLod = 0f;
		_cacheLodObjetsAuSol.Clear();
		_indexLodObjetsAuSol = 0;
	}
}
