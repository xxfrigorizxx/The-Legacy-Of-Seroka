using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
	[Flags]
	private enum CategorieAnalyse
	{
		Aucune = 0,
		Fibre = 1 << 0,
		Ligature = 1 << 1,
		Corde = 1 << 2,
		Tissu = 1 << 3,
		Baton = 1 << 4,
		BatonFaconne = 1 << 5,
		BatonEnT = 1 << 6,
		RochePointe = 1 << 7,
		RochePlate = 1 << 8,
		RocheOvale = 1 << 9,
		RocheRonde = 1 << 10,
		BuchePleine = 1 << 11,
		DemiBuche = 1 << 12,
		Pochette = 1 << 13,
		CeinturePoches = 1 << 14
	}

	private sealed class RecetteAnalysable
	{
		public string CleCraft;
		public int IdResultat;
		public CategorieAnalyse Masque;
		public string Titre;
		public string[] LegendeSymboles;
		public string[] PatronCraft;
	}

	public const int CapaciteAnalyseurManuel = 4;
	public SlotInventaire[] GrilleAnalyseurManuel = new SlotInventaire[CapaciteAnalyseurManuel];
	public string MessageAnalyseurManuel = "Depose des objets puis clique sur Analyser.";
	private readonly HashSet<string> _craftsDecouverts = new HashSet<string>(StringComparer.Ordinal);

	private static readonly RecetteAnalysable[] RecettesAnalyseur = new RecetteAnalysable[]
	{
		new RecetteAnalysable
		{
			CleCraft = "corde_tier0",
			IdResultat = 20,
			Masque = CategorieAnalyse.Fibre,
			Titre = "Corde d'herbe",
			LegendeSymboles = new[] { "F = Fibre d'herbe" },
			PatronCraft = new[] { "(F)(F)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "corde_tier2",
			IdResultat = 20,
			Masque = CategorieAnalyse.Corde,
			Titre = "Corde solide",
			LegendeSymboles = new[] { "C = Corde d'herbe" },
			PatronCraft = new[] { "(C)(C)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_21",
			IdResultat = 21,
			Masque = CategorieAnalyse.Ligature,
			Titre = "Tissu",
			LegendeSymboles = new[] { "L = Ligature (corde ou liane)" },
			PatronCraft = new[] { "(L)(L)", "(L)(L)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "baton_faconne",
			IdResultat = 32,
			Masque = CategorieAnalyse.Baton,
			Titre = "Baton faconne",
			LegendeSymboles = new[] { "B = Baton brut" },
			PatronCraft = new[] { "(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_105",
			IdResultat = 105,
			Masque = CategorieAnalyse.RochePointe | CategorieAnalyse.Ligature,
			Titre = "Dague primitive",
			LegendeSymboles = new[] { "R = Petite roche en pointe", "L = Ligature" },
			PatronCraft = new[] { "(R)", "(L)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_106",
			IdResultat = 106,
			Masque = CategorieAnalyse.Baton | CategorieAnalyse.RochePlate | CategorieAnalyse.Ligature,
			Titre = "Hachette primitive",
			LegendeSymboles = new[] { "R = Petite roche plate", "L = Ligature", "B = Baton" },
			PatronCraft = new[] { "(R)(L)", "(  )(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_107",
			IdResultat = IdObjetPellePierreTier0,
			Masque = CategorieAnalyse.BatonFaconne | CategorieAnalyse.RocheOvale | CategorieAnalyse.Ligature,
			Titre = "Pelle pierre tier 0",
			LegendeSymboles = new[] { "B = Baton faconne", "L = Ligature", "R = Petite roche ovale" },
			PatronCraft = new[] { "(  )(B)(  )", "(  )(L)(  )", "(  )(R)(  )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_108",
			IdResultat = IdObjetPiochePierreTier0,
			Masque = CategorieAnalyse.BatonFaconne | CategorieAnalyse.RochePointe | CategorieAnalyse.Ligature,
			Titre = "Pioche pierre tier 0",
			LegendeSymboles = new[] { "R = Petite roche en pointe", "L = Ligature", "B = Baton faconne" },
			PatronCraft = new[] { "(R)(L)(R)", "(  )(B)(  )", "(  )(B)(  )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_111",
			IdResultat = IdObjetLancePierreTier0,
			Masque = CategorieAnalyse.BatonFaconne | CategorieAnalyse.RochePointe | CategorieAnalyse.Ligature,
			Titre = "Lance pierre tier 0",
			LegendeSymboles = new[] { "R = Petite roche en pointe", "L = Ligature", "B = Baton faconne" },
			PatronCraft = new[] { "(  )(L)(R)", "(  )(B)(L)", "(B)(  )(  )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_112",
			IdResultat = IdObjetFauxPierreTier0,
			Masque = CategorieAnalyse.BatonFaconne | CategorieAnalyse.BatonEnT | CategorieAnalyse.RochePointe | CategorieAnalyse.Ligature,
			Titre = "Faux primitive",
			LegendeSymboles = new[] { "R = Petite roche en pointe", "T = Baton faconne en T", "L = Ligature", "B = Baton faconne" },
			PatronCraft = new[] { "(  )(R)(  )", "(T)(L)(T)", "(  )(B)(  )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_102",
			IdResultat = IdObjetCeinturePoches,
			Masque = CategorieAnalyse.Ligature,
			Titre = "Ceinture a poches",
			LegendeSymboles = new[] { "L = Ligature" },
			PatronCraft = new[] { "(L)(L)(L)", "(L)(L)(L)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_109",
			IdResultat = IdObjetRackBatons,
			Masque = CategorieAnalyse.Baton | CategorieAnalyse.Ligature,
			Titre = "Rack a batons",
			LegendeSymboles = new[] { "B = Baton", "L = Ligature" },
			PatronCraft = new[] { "(B)(L)(  )", "(  )(B)(  )", "(  )(L)(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_110",
			IdResultat = IdObjetRackBuches,
			Masque = CategorieAnalyse.DemiBuche | CategorieAnalyse.Ligature,
			Titre = "Rack a buches",
			LegendeSymboles = new[] { "D = Demi-buche", "L = Ligature", "d = Demi-buche courte" },
			PatronCraft = new[] { "(D)(  )(D)", "(D)(  )(D)", "(L)(d)(L)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_103",
			IdResultat = IdObjetPochetteTier0,
			Masque = CategorieAnalyse.Tissu | CategorieAnalyse.Ligature,
			Titre = "Pochette tier 0",
			LegendeSymboles = new[] { "T = Tissu", "L = Ligature" },
			PatronCraft = new[] { "(  )(T)(  )", "(  )(L)(  )", "(  )(T)(  )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_101",
			IdResultat = IdObjetSacTier0,
			Masque = CategorieAnalyse.Pochette | CategorieAnalyse.Ligature,
			Titre = "Sac tier 0",
			LegendeSymboles = new[] { "P = Pochette tier 0", "L = Ligature" },
			PatronCraft = new[] { "(  )(L)(  )", "(  )(P)(  )", "(  )(  )(  )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_104",
			IdResultat = IdObjetCeintureSacoches,
			Masque = CategorieAnalyse.Pochette | CategorieAnalyse.CeinturePoches,
			Titre = "Ceinture a sacoches",
			LegendeSymboles = new[] { "P = Pochette tier 0", "C = Ceinture a poches" },
			PatronCraft = new[] { "(P)(  )(P)", "(  )(C)(  )", "(P)(  )(P)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_113",
			IdResultat = IdObjetCoffreBoisTier0,
			Masque = CategorieAnalyse.DemiBuche | CategorieAnalyse.Baton | CategorieAnalyse.Ligature,
			Titre = "Coffre en bois",
			LegendeSymboles = new[] { "L = Ligature", "B = Baton", "D = Demi-buche standard" },
			PatronCraft = new[] { "(L)(B)(L)", "(D)(L)(D)", "(D)(D)(D)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_200",
			IdResultat = 200,
			Masque = CategorieAnalyse.DemiBuche | CategorieAnalyse.BuchePleine | CategorieAnalyse.RocheRonde | CategorieAnalyse.Corde,
			Titre = "Atelier primitif",
			LegendeSymboles = new[] { "D = Demi-buche", "R = Petite roche ronde", "B = Buche pleine", "C = Corde" },
			PatronCraft = new[] { "(D)(R)", "(B)(C)" }
		}
	};

	private static string FormaterMessageDecouverte(RecetteAnalysable recette)
	{
		string titre = string.IsNullOrWhiteSpace(recette?.Titre) ? "Craft inconnu" : recette.Titre;
		string legende = "";
		if (recette?.LegendeSymboles != null && recette.LegendeSymboles.Length > 0)
			legende = string.Join(", ", recette.LegendeSymboles);
		string patron = "";
		if (recette?.PatronCraft != null && recette.PatronCraft.Length > 0)
			patron = string.Join("\n", recette.PatronCraft);
		if (string.IsNullOrWhiteSpace(legende) || string.IsNullOrWhiteSpace(patron))
		{
			GD.PrintErr($"ZERO-K : Recette analyseur incomplète (clé={recette?.CleCraft ?? "?"}).");
			return $"Vous avez decouvert: {titre}. Le schema de craft est en preparation.";
		}
		return $"Vous avez decouvert: {titre}\nLe craft est: {legende}\nLe craft est\n{patron}";
	}

	private static string CleCraftDepuisResultat(in SlotInventaire resultat)
	{
		if (resultat.ID <= 0) return "";
		if (resultat.ID == 20)
			return resultat.IndexBotanique >= 2 ? "corde_tier2" : "corde_tier0";
		if (resultat.ID == 32 && resultat.IndexChimique == 1)
			return "baton_faconne";
		return $"id_{resultat.ID}";
	}

	public bool EstCraftDebloque(SlotInventaire resultat)
	{
		if (resultat.EstVide) return true;
		string cle = CleCraftDepuisResultat(resultat);
		if (string.IsNullOrEmpty(cle)) return true;
		if (_craftsDecouverts.Contains(cle)) return true;
		return _craftsDecouverts.Contains($"id_{resultat.ID}");
	}

	public bool EstCraftDebloque(int idResultat)
	{
		if (idResultat <= 0) return true;
		return _craftsDecouverts.Contains($"id_{idResultat}");
	}

	public void DebloquerCraft(int idResultat)
	{
		if (idResultat <= 0) return;
		_craftsDecouverts.Add($"id_{idResultat}");
	}

	public void DebloquerCraft(string cleCraft)
	{
		if (string.IsNullOrWhiteSpace(cleCraft)) return;
		_craftsDecouverts.Add(cleCraft);
	}

	public string[] ExporterCraftsDecouverts()
	{
		var data = new string[_craftsDecouverts.Count];
		_craftsDecouverts.CopyTo(data);
		Array.Sort(data, StringComparer.Ordinal);
		return data;
	}

	public void ImporterCraftsDecouverts(IEnumerable<string> keys)
	{
		_craftsDecouverts.Clear();
		if (keys == null) return;
		foreach (string key in keys)
		{
			if (!string.IsNullOrWhiteSpace(key))
				_craftsDecouverts.Add(key);
		}
	}

	private static CategorieAnalyse DeterminerCategoriesAnalyse(in SlotInventaire s)
	{
		if (s.EstVide) return CategorieAnalyse.Aucune;
		CategorieAnalyse c = CategorieAnalyse.Aucune;
		if (s.ID == 15) c |= CategorieAnalyse.Fibre;
		if (s.ID == 16 || s.ID == 20) c |= CategorieAnalyse.Ligature;
		if (s.ID == 20) c |= CategorieAnalyse.Corde;
		if (s.ID == 21) c |= CategorieAnalyse.Tissu;
		if (s.ID == 32)
		{
			c |= CategorieAnalyse.Baton;
			if (s.IndexChimique == 1) c |= CategorieAnalyse.BatonFaconne;
			if (s.IndexMorphologique == 4) c |= CategorieAnalyse.BatonEnT;
		}
		if (s.ID == 30)
		{
			if (s.IndexMorphologique == 1) c |= CategorieAnalyse.DemiBuche;
			if (s.IndexMorphologique == 0) c |= CategorieAnalyse.BuchePleine;
		}
		if (s.ID == IdObjetPochetteTier0) c |= CategorieAnalyse.Pochette;
		if (s.ID == IdObjetCeinturePoches) c |= CategorieAnalyse.CeinturePoches;
		if (ItemPhysique.EstIdRocheMatiere(s.ID))
		{
			if (s.IndexMorphologique == 0) c |= CategorieAnalyse.RocheRonde;
			else if (s.IndexMorphologique == 1) c |= CategorieAnalyse.RochePlate;
			else if (s.IndexMorphologique == 2) c |= CategorieAnalyse.RocheOvale;
			else if (s.IndexMorphologique == 3) c |= CategorieAnalyse.RochePointe;
		}
		return c;
	}

	public bool EssayerAnalyserCrafts(out string message)
	{
		CategorieAnalyse masque = CategorieAnalyse.Aucune;
		bool aDesItems = false;
		for (int i = 0; i < GrilleAnalyseurManuel.Length; i++)
		{
			SlotInventaire s = GrilleAnalyseurManuel[i];
			if (s.EstVide) continue;
			aDesItems = true;
			masque |= DeterminerCategoriesAnalyse(s);
		}

		if (!aDesItems)
		{
			message = "Depose des objets dans l'analyseur.";
			MessageAnalyseurManuel = message;
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		void ConsommerAnalyseur()
		{
			for (int i = 0; i < GrilleAnalyseurManuel.Length; i++)
				GrilleAnalyseurManuel[i] = new SlotInventaire();
		}

		var candidates = new List<RecetteAnalysable>();
		for (int i = 0; i < RecettesAnalyseur.Length; i++)
		{
			RecetteAnalysable r = RecettesAnalyseur[i];
			if ((masque & r.Masque) == r.Masque)
				candidates.Add(r);
		}
		if (candidates.Count == 0)
		{
			ConsommerAnalyseur();
			message = "Aucun craft ne se compose uniquement de ces materiaux.";
			MessageAnalyseurManuel = message;
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		var nonDecouvertes = new List<RecetteAnalysable>();
		for (int i = 0; i < candidates.Count; i++)
		{
			if (!_craftsDecouverts.Contains(candidates[i].CleCraft))
				nonDecouvertes.Add(candidates[i]);
		}
		if (nonDecouvertes.Count == 0)
		{
			ConsommerAnalyseur();
			message = "Rien a decouvrir avec ce que tu as analyse.";
			MessageAnalyseurManuel = message;
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		float pReussite = ObtenirChanceReussiteAnalyseManuelle();
		if (GD.Randf() >= pReussite)
		{
			ConsommerAnalyseur();
			AjouterXpFutureState("Intelligence", 2UL);
			message = "Echec de l'analyse : tes echantillons sont consumes. Tu en retires une lecon (+2 XP Intelligence).";
			MessageAnalyseurManuel = message;
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		int pick = nonDecouvertes.Count == 1 ? 0 : GD.RandRange(0, nonDecouvertes.Count - 1);
		RecetteAnalysable choisie = nonDecouvertes[pick];
		DebloquerCraft(choisie.CleCraft);
		ConsommerAnalyseur();
		AjouterXpFutureState("Intelligence", 1UL);

		message = FormaterMessageDecouverte(choisie) + " (+1 XP Intelligence).";
		MessageAnalyseurManuel = message;
		string titreCourt = string.IsNullOrWhiteSpace(choisie.Titre) ? "nouvelle formule" : choisie.Titre;
		AlerteSqueletteBoiteNoire($"Analyseur : reussite — tu decouvres « {titreCourt} » (+1 XP Intelligence). Ouvre le menu Q pour le schema detaille.");
		return true;
	}

	private static bool EssayerFusionnerSlot(ref SlotInventaire destination, ref SlotInventaire source, int pileMax)
	{
		if (destination.EstVide || source.EstVide) return false;
		if (!Joueur.SontEmpilables(destination, source)) return false;
		int qDst = Joueur.ObtenirQuantiteSlot(destination);
		int qSrc = Joueur.ObtenirQuantiteSlot(source);
		int place = Mathf.Max(0, pileMax - qDst);
		if (place <= 0) return false;
		int move = Mathf.Min(place, qSrc);
		if (move <= 0) return false;
		destination.Quantite = qDst + move;
		qSrc -= move;
		if (qSrc <= 0) source = new SlotInventaire();
		else source.Quantite = qSrc;
		return true;
	}

	private static bool EssayerDeposerDansSlotVide(ref SlotInventaire destination, ref SlotInventaire source)
	{
		if (!destination.EstVide || source.EstVide) return false;
		destination = source;
		destination.Quantite = Joueur.ObtenirQuantiteSlot(destination);
		source = new SlotInventaire();
		return true;
	}

	public bool EssayerRangerSlotInventaireOuStockage(ref SlotInventaire slot)
	{
		if (slot.EstVide) return true;

		EssayerFusionnerSlot(ref MainGauche, ref slot, Joueur.ObtenirPileMax(MainGauche));
		EssayerFusionnerSlot(ref MainDroite, ref slot, Joueur.ObtenirPileMax(MainDroite));
		for (int i = 0; i < GrilleCraftPoche.Length && !slot.EstVide; i++)
			EssayerFusionnerSlot(ref GrilleCraftPoche[i], ref slot, Joueur.ObtenirPileMax(GrilleCraftPoche[i]));

		for (int i = 0; i < GrilleSacStockage.Length && !slot.EstVide; i++)
		{
			int max = Joueur.ObtenirPileMax(GrilleSacStockage[i]);
			if (Joueur.EstSacTier0Liane(EquipementSacDos)) max *= 2;
			EssayerFusionnerSlot(ref GrilleSacStockage[i], ref slot, max);
		}
		for (int i = 0; i < GrilleCeintureStockage.Length && !slot.EstVide; i++)
		{
			int max = Joueur.ObtenirPileMax(GrilleCeintureStockage[i]) * Joueur.ObtenirMultiplicateurPileCeintureSlot(EquipementCeinture, i);
			EssayerFusionnerSlot(ref GrilleCeintureStockage[i], ref slot, max);
		}

		if (!slot.EstVide && EssayerDeposerDansSlotVide(ref MainGauche, ref slot)) return true;
		if (!slot.EstVide && EssayerDeposerDansSlotVide(ref MainDroite, ref slot)) return true;
		for (int i = 0; i < GrilleCraftPoche.Length && !slot.EstVide; i++)
			EssayerDeposerDansSlotVide(ref GrilleCraftPoche[i], ref slot);
		for (int i = 0; i < GrilleSacStockage.Length && !slot.EstVide; i++)
			EssayerDeposerDansSlotVide(ref GrilleSacStockage[i], ref slot);
		for (int i = 0; i < GrilleCeintureStockage.Length && !slot.EstVide; i++)
			EssayerDeposerDansSlotVide(ref GrilleCeintureStockage[i], ref slot);
		return slot.EstVide;
	}

	public void DeposerSlotAuSolDepuisMenu(in SlotInventaire slot)
	{
		if (slot.EstVide) return;
		Vector3 avant = -GlobalTransform.Basis.Z.Normalized();
		Vector3 spawn = GlobalPosition + avant * 1.2f + Vector3.Up * 0.7f;
		Node3D corps = CreerBlocPose(spawn, slot);
		if (corps is RigidBody3D rb)
			rb.ApplyCentralImpulse(avant * 2.6f + Vector3.Up * 1.8f);
	}
}
