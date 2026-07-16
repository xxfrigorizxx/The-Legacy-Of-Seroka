using Godot;
using System;
using System.Collections.Generic;

/// <summary>Localisation des PNJ physiques + virtuels (pour /TP HOMINA et debug).</summary>
public static class PnjHumainLocalisationService
{
	public struct InfoPnj
	{
		public string Nom;
		public Vector3 Position;
		public bool Virtuel;
		public bool EnMigration;
		public bool EnCamp;
		public bool EnEvaluationCamp;
		public Vector2 CibleMigration;
	}

	public static List<InfoPnj> ListerTous(Gestionnaire_Monde gm)
	{
		var list = new List<InfoPnj>();
		foreach (PnjHumain p in PnjHumain.Tous)
		{
			if (p == null || !GodotObject.IsInstanceValid(p))
				continue;
			list.Add(new InfoPnj
			{
				Nom = p.NomPnj,
				Position = p.GlobalPosition,
				Virtuel = false,
				EnMigration = p.EnMigrationVersBiome,
				EnCamp = p.EstEnPauseCamp,
				EnEvaluationCamp = p.EstEnEvaluationCamp,
				CibleMigration = p.CibleMigrationAbsolueXZ
			});
		}
		foreach (PnjHumainEtatVirtuel v in PnjHumainContinuiteService.Virtuels)
		{
			if (v == null)
				continue;
			list.Add(new InfoPnj
			{
				Nom = v.Nom,
				Position = v.Position,
				Virtuel = true,
				EnMigration = v.ACibleMigration,
				EnCamp = v.EnPauseCamp,
				EnEvaluationCamp = false,
				CibleMigration = v.CibleMigration
			});
		}
		return list;
	}

	/// <summary>Résout un PNJ par nom partiel, ou le plus proche du joueur si filtre vide.</summary>
	public static bool EssayerResoudreCible(Gestionnaire_Monde gm, string filtreNom, out InfoPnj info)
	{
		info = default;
		if (gm == null || !gm.JoueurReferenceValide())
			return false;
		List<InfoPnj> tous = ListerTous(gm);
		if (tous.Count == 0)
			return false;

		Vector3 joueur = gm.ObtenirPositionJoueurOuSpawn();
		if (!string.IsNullOrWhiteSpace(filtreNom))
		{
			InfoPnj? meilleur = null;
			float meilleurScore = float.MinValue;
			string filtre = filtreNom.Trim();
			foreach (InfoPnj p in tous)
			{
				if (string.IsNullOrEmpty(p.Nom))
					continue;
				if (!p.Nom.Contains(filtre, StringComparison.OrdinalIgnoreCase))
					continue;
				float score = 1000f - joueur.DistanceTo(p.Position);
				if (p.Nom.Equals(filtre, StringComparison.OrdinalIgnoreCase))
					score += 5000f;
				if (score > meilleurScore)
				{
					meilleurScore = score;
					meilleur = p;
				}
			}
			if (meilleur == null)
				return false;
			info = meilleur.Value;
			return true;
		}

		float distMin = float.MaxValue;
		foreach (InfoPnj p in tous)
		{
			float d = joueur.DistanceTo(p.Position);
			if (d >= distMin)
				continue;
			distMin = d;
			info = p;
		}
		return true;
	}
}
