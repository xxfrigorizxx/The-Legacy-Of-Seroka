using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
	[Flags]
	private enum CategorieAnalyse : long
	{
		Aucune = 0,
		Fibre = 1L << 0,
		Ligature = 1L << 1,
		Corde = 1L << 2,
		Tissu = 1L << 3,
		Baton = 1L << 4,
		BatonFaconne = 1L << 5,
		BatonEnT = 1L << 6,
		RochePointe = 1L << 7,
		RochePlate = 1L << 8,
		RocheOvale = 1L << 9,
		RocheRonde = 1L << 10,
		BuchePleine = 1L << 11,
		DemiBuche = 1L << 12,
		Pochette = 1L << 13,
		CeinturePoches = 1L << 14,
		/// <summary>Branche d'arbre / de buisson (ID 31) : manche hachette primitive, mais pas substitut du bâton brut (32) pour « bâton façonné » seul.</summary>
		BrancheBrute = 1L << 15,
		/// <summary>Roche voxel brute (ID 2), utilisée pour débloquer la recette de façonnage vers petite roche matière (ID 47).</summary>
		RocheVoxelBrute = 1L << 16,
		Silex = 1L << 17,
		RocheSulfuree = 1L << 18,
		PitFeu = 1L << 19,
		RocheMatiere = 1L << 20,
		/// <summary>Rondin court fendu en 8 (IndexTaille 3, IndexMorphologique 3) — maillet / pilon.</summary>
		MiniBucheHuitieme = 1L << 21,
		DaguePrimitive = 1L << 22,
		MailletBois = 1L << 23,
		BolBois = 1L << 24,
		OsBoeuf = 1L << 25,
		CuirBoeuf = 1L << 26,
		MortierPilonBois = 1L << 27,
		BolEau = 1L << 28,
		VoxelArgile = 1L << 29,
		ArgileHumidifiee = 1L << 30,
		VoxelBoue = 1L << 31,
		Torchie = 1L << 32,
		PinceOs = 1L << 33,
		/// <summary>Rondin court cylindre entier (IndexTaille 3, IndexMorphologique 0) — bol en bois.</summary>
		BucheRondinCourt = 1L << 34,
		/// <summary>Intestin de bœuf brut (118) ou nettoyé (119) — corde d'intestin (distinct de la fibre d'herbe).</summary>
		IntestinBoeuf = 1L << 35,
		/// <summary>Voxel terre aride (ID terrain 6) — ingrédient de la boue (bol d'eau + terre aride).</summary>
		VoxelTerreAride = 1L << 36,
		/// <summary>Bol en céramique (159) — refroidi ou chaud.</summary>
		BolCeramique = 1L << 37,
		/// <summary>Minerai d'étain recolté (166).</summary>
		Etain = 1L << 38
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
	public const int CapaciteAnalyseurTableTier1 = 8;
	public SlotInventaire[] GrilleAnalyseurManuel = new SlotInventaire[CapaciteAnalyseurManuel];
	public SlotInventaire[] GrilleAnalyseurTableTier1 = new SlotInventaire[CapaciteAnalyseurTableTier1];
	public string MessageAnalyseurManuel = "Depose des objets puis clique sur Analyser.";
	public string MessageAnalyseurTableTier1 = "Depose des objets puis clique sur Analyser (table T1).";
	public bool AnalyseurTier1Actif;
	public ItemPhysique TableAnalyseTier1Ouverte;
	private readonly HashSet<string> _craftsDecouverts = new HashSet<string>(StringComparer.Ordinal);

	private static readonly RecetteAnalysable[] RecettesAnalyseurTier0 = new RecetteAnalysable[]
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
			CleCraft = "corde_mixte",
			IdResultat = 20,
			Masque = CategorieAnalyse.Fibre,
			Titre = "Corde mixte (deux fibres)",
			LegendeSymboles = new[]
			{
				"Fg = Fibre gauche (herbe 15, liane 16 ou boyau 17)",
				"Fd = Fibre droite (autre type que Fg)",
				"Disposition : (Fg)(Fd) sur une meme ligne — gauche vers droite"
			},
			PatronCraft = new[] { "(Fg)(Fd)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "corde_mixte_intestin",
			IdResultat = 20,
			Masque = CategorieAnalyse.Fibre,
			Titre = "Corde mixte intestin + fibre",
			LegendeSymboles = new[]
			{
				"I = Intestin (118 brut ou 119 nettoye dans l'eau)",
				"F = Fibre (herbe, liane ou boyau)",
				"Disposition : (I)(F) ou (F)(I) sur une meme ligne — gauche vers droite"
			},
			PatronCraft = new[] { "(I)(F)", "(F)(I)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "corde_intestin",
			IdResultat = 20,
			Masque = CategorieAnalyse.IntestinBoeuf,
			Titre = "Corde d'intestin",
			LegendeSymboles = new[]
			{
				"I = Intestin (118 brut ou 119 nettoye dans l'eau)",
				"Analyseur : un intestin suffit",
				"Craft (poche Q ou atelier) : deux intestins, positions libres sur la grille 2x2"
			},
			PatronCraft = new[] { "(I)(I)" }
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
			LegendeSymboles = new[] { "B = Baton brut (32) ou branche (31) — même formule, essence = bois utilisé" },
			PatronCraft = new[] { "(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "roche_marbre_pointe",
			IdResultat = 47,
			Masque = CategorieAnalyse.RocheVoxelBrute,
			Titre = "Petite roche faconnee en pointe (marbre)",
			LegendeSymboles = new[]
			{
				"R = Roche brute (ID 2)",
				"Resultat = Petite roche marbre en pointe"
			},
			PatronCraft = new[] { "(R)", "(R)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "roche_marbre_plate",
			IdResultat = 47,
			Masque = CategorieAnalyse.RocheVoxelBrute,
			Titre = "Petite roche faconnee plate (marbre)",
			LegendeSymboles = new[]
			{
				"R = Roche brute (ID 2)",
				"Resultat = Petite roche marbre plate"
			},
			PatronCraft = new[] { "(R)(R)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "roche_marbre_ronde",
			IdResultat = 47,
			Masque = CategorieAnalyse.RocheVoxelBrute,
			Titre = "Petite roche faconnee ronde (marbre)",
			LegendeSymboles = new[]
			{
				"R = Roche brute (ID 2)",
				"Resultat = Petite roche marbre ronde"
			},
			PatronCraft = new[] { "(R)(R)", "(R)(R)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "roche_marbre_ovale",
			IdResultat = 47,
			Masque = CategorieAnalyse.RocheVoxelBrute,
			Titre = "Petite roche faconnee ovale (marbre)",
			LegendeSymboles = new[]
			{
				"R = Roche brute (ID 2)",
				"Resultat = Petite roche marbre ovale"
			},
			PatronCraft = new[] { "(R)(R)(R)", "(R)(R)(R) (rotation 2x3/3x2)" }
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
			LegendeSymboles = new[] { "R = Petite roche plate", "L = Ligature", "B = Baton brut (32) ou branche (31)" },
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
			CleCraft = "id_128",
			IdResultat = IdObjetMailletBois,
			Masque = CategorieAnalyse.MiniBucheHuitieme,
			Titre = "Maillet en bois",
			LegendeSymboles = new[] { "mB = Rondin court fendu en 8 (standard → demi → rondin, puis 3 coupes dans le bois)" },
			PatronCraft = new[] { "Établi 3x3 : placer 1 mB seul dans n'importe quelle case." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_129",
			IdResultat = IdObjetBolBois,
			Masque = CategorieAnalyse.BucheRondinCourt | CategorieAnalyse.DaguePrimitive,
			Titre = "Bol en bois",
			LegendeSymboles = new[] { "rB = Rondin court (standard → demi → rondin, coupe transversale)", "D = Dague (non consommée, -2 durabilité)" },
			PatronCraft = new[] { "Grille craft (Q) ou établi 3×3 : 1 rB + 1 D, positions libres." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_130",
			IdResultat = IdObjetMortierPilonBois,
			Masque = CategorieAnalyse.BolBois | CategorieAnalyse.MailletBois,
			Titre = "Mortier avec pilon",
			LegendeSymboles = new[] { "B = Bol en bois", "P = Pilon/Maillet en bois" },
			PatronCraft = new[] { "Établi 3x3 : 1 B + 1 P, positions libres." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_155",
			IdResultat = IdObjetArgileHumidifiee,
			Masque = CategorieAnalyse.BolEau | CategorieAnalyse.VoxelArgile,
			Titre = "Argile humidifiee",
			LegendeSymboles = new[] { "Be = Bol rempli d'eau", "A = Voxel argile (ID terrain 8)" },
			PatronCraft = new[] { "Grille craft : 1 Be + 1 A (le bol est vide apres craft)." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_7",
			IdResultat = 7,
			Masque = CategorieAnalyse.BolEau | CategorieAnalyse.VoxelTerreAride,
			Titre = "Voxel de boue",
			LegendeSymboles = new[] { "Be = Bol rempli d'eau", "Ta = Voxel terre aride (ID terrain 6)" },
			PatronCraft = new[] { "Grille craft : 1 Be + 1 Ta -> 1 voxel boue (le bol est vide apres craft)." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_156",
			IdResultat = IdObjetTorchie,
			Masque = CategorieAnalyse.ArgileHumidifiee | CategorieAnalyse.Fibre | CategorieAnalyse.VoxelBoue,
			Titre = "Torchie",
			LegendeSymboles = new[] { "Ah = Argile humidifiee", "H = Brin d'herbe (fibre)", "B = Voxel boue (ID terrain 7)" },
			PatronCraft = new[] { "Grille craft : 1 Ah + 1 H + 1 B -> 3 torchies." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_157",
			IdResultat = IdObjetFourTorchie,
			Masque = CategorieAnalyse.Torchie,
			Titre = "Four en Torchie",
			LegendeSymboles = new[] { "T = Torchie" },
			PatronCraft = new[] { "( )(T)( )", "(T)(T)(T)", "(T)( )(T)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_158",
			IdResultat = IdObjetBolArgile,
			Masque = CategorieAnalyse.ArgileHumidifiee,
			Titre = "Bol en argile",
			LegendeSymboles = new[] { "A = Argile humidifiee" },
			PatronCraft = new[] { "( )( )( )", "(A)( )(A)", "( )(A)( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_161",
			IdResultat = IdObjetMouleArgile,
			Masque = CategorieAnalyse.ArgileHumidifiee,
			Titre = "Moule en argile",
			LegendeSymboles = new[] { "A = Argile humidifiee" },
			PatronCraft = new[] { "(A)( )(A)", "(A)(A)(A)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_167",
			IdResultat = IdObjetBolCeramiqueEtain,
			Masque = CategorieAnalyse.BolCeramique | CategorieAnalyse.Etain,
			Titre = "Bol en ceramique etain",
			LegendeSymboles = new[] { "Bc = Bol en ceramique (refroidi)", "Et = Minerai d'etain" },
			PatronCraft = new[] { "Grille craft : 1 Bc + 1 Et, positions libres sur la grille 2x2." }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_160",
			IdResultat = IdObjetPinceOs,
			Masque = CategorieAnalyse.OsBoeuf,
			Titre = "Pince en os",
			LegendeSymboles = new[] { "O = Os de boeuf" },
			PatronCraft = new[] { "( )(O)( )", "( )(O)( )", "(O)( )(O)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_131",
			IdResultat = IdObjetTableAnalyseTier1,
			Masque = CategorieAnalyse.MortierPilonBois | CategorieAnalyse.CuirBoeuf | CategorieAnalyse.OsBoeuf | CategorieAnalyse.Ligature,
			Titre = "Table d'analyse tier 1",
			LegendeSymboles = new[] { "MP = Mortier + pilon", "C = Cuir de boeuf", "O = Os de boeuf", "L = Liage (corde/liane/intestin)", "Craft établi: (C)(C)(MP) / (L)(B)(L) / (O)( )(O)" },
			PatronCraft = new[] { "(C)(C)(MP)", "(L)(B)(L)", "(O)( )(O)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_133",
			IdResultat = IdObjetAtelleJambe,
			Masque = CategorieAnalyse.BrancheBrute | CategorieAnalyse.Ligature,
			Titre = "Atelle de jambe",
			LegendeSymboles = new[] { "Br = Branche brute (x6, meme essence)", "L = Ligature (x3, meme variante)" },
			PatronCraft = new[] { "(Br)(L)(Br)", "(Br)(L)(Br)", "(Br)(L)(Br)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_134",
			IdResultat = IdObjetAtelleBras,
			Masque = CategorieAnalyse.BrancheBrute | CategorieAnalyse.Ligature,
			Titre = "Atelle de bras",
			LegendeSymboles = new[] { "Br = Branche brute (x6, meme essence)", "L = Ligature (x3, meme variante)" },
			PatronCraft = new[] { "(Br)(L)(Br)", "(L)(L)(Br)", "(Br)(Br)(Br)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_135",
			IdResultat = IdObjetBandageTier1,
			Masque = CategorieAnalyse.Ligature,
			Titre = "Bandage tier 1",
			LegendeSymboles = new[] { "L = Ligature (corde ou liane, meme variante x3)" },
			PatronCraft = new[] { "(L)(L)", "(L)()" }
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
			LegendeSymboles = new[] { "B = Baton faconne", "L = Corde ou liane" },
			PatronCraft = new[] { "(  )(  )(  )", "(L)(B)(L)", "(B)( )(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_110",
			IdResultat = IdObjetRackBuches,
			Masque = CategorieAnalyse.DemiBuche | CategorieAnalyse.Ligature,
			Titre = "Rack a buches",
			LegendeSymboles = new[] { "D = Demi-buche standard fendue en 2", "L = Ligature (corde ou liane)", "d = Demi-buche courte" },
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
			CleCraft = "id_120",
			IdResultat = IdObjetPitFeu,
			Masque = CategorieAnalyse.BrancheBrute,
			Titre = "Pit a feu",
			LegendeSymboles = new[] { "Br = Branche brute (meme essence)" },
			PatronCraft = new[] { "(Br)(Br)", "(Br)(Br)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_121",
			IdResultat = IdObjetAllumeFeu,
			Masque = CategorieAnalyse.Silex | CategorieAnalyse.RocheSulfuree,
			Titre = "Allume-feu préhistorique",
			LegendeSymboles = new[] { "S = Silex", "P = Marcassite ou pyrite" },
			PatronCraft = new[] { "(S)(P) ou (P)(S)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_145",
			IdResultat = IdObjetTorche,
			Masque = CategorieAnalyse.BrancheBrute | CategorieAnalyse.Tissu,
			Titre = "Torche",
			LegendeSymboles = new[] { "T = Tissu", "Br = Branche brute" },
			PatronCraft = new[] { "(T)", "(Br)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_122",
			IdResultat = IdObjetPitFeuRoche,
			Masque = CategorieAnalyse.RocheMatiere | CategorieAnalyse.PitFeu,
			Titre = "Pit a feu roche",
			LegendeSymboles = new[] { "Pf = Pit a feu", "R = Roches (x8 autour)" },
			PatronCraft = new[] { "(R)(R)(R)", "(R)(Pf)(R)", "(R)(R)(R)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_200",
			IdResultat = 200,
			Masque = CategorieAnalyse.DemiBuche | CategorieAnalyse.BuchePleine | CategorieAnalyse.RocheRonde | CategorieAnalyse.Ligature,
			Titre = "Atelier primitif",
			LegendeSymboles = new[] { "D = Demi-buche courte", "d = Demi-buche courte fendue en 2", "R = Petite roche ronde", "L = Liage" },
			PatronCraft = new[] { "(d)(R)", "(D)(L)" }
		},
		
	};

	// Table T1 : débloque ces recettes (+ RecettesAnalyseurTier0 en fusion). Pas l'analyseur manuel seul.
	private static readonly RecetteAnalysable[] RecettesAnalyseurTier1 = new RecetteAnalysable[]
	{
		new RecetteAnalysable
		{
			CleCraft = "id_147",
			IdResultat = IdObjetTableBoisDecorative,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Table décorative bois",
			LegendeSymboles = new[] { "Table T1 : 1 bâton + 1 demi-bûche fendue en 2 + 1 liage", "Craft atelier : (L)(BF)(L) / (B)( )(B)" },
			PatronCraft = new[] { "(L)(BF)(L)", "(B)( )(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_148",
			IdResultat = IdObjetTableArtisanaTier1,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Table artisanat structures T1",
			LegendeSymboles = new[] { "Table analyse T1 : 1 hachette + 1 pioche + 1 atelier + 1 petite roche ronde + 1 demi-bûche fendue", "Craft atelier : (H)( )(P) / (R)(R)(DB) / ( )(T)( )" },
			PatronCraft = new[] { "(H)( )(P)", "(R)(R)(DB)", "( )(T)( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_132",
			IdResultat = IdObjetHachePierreTier1,
			Masque = CategorieAnalyse.Baton | CategorieAnalyse.RochePlate,
			Titre = "Hache en pierre",
			LegendeSymboles = new[] { "R = Petite roche plate (x2, meme matiere)", "B = Baton (x3, meme essence)" },
			PatronCraft = new[] { "(R)(R)(B)", "( )( )(B)", "( )( )(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_124",
			IdResultat = IdObjetFondationBois,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Fondation bois",
			LegendeSymboles = new[] { "Table T1 : 1 demi-buche standard (buche fendue en 2)", "Craft atelier : grille 3×3 (x9 demi-buches, meme essence)" },
			PatronCraft = new[] { "(D)(D)(D)", "(D)(D)(D)", "(D)(D)(D)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_125",
			IdResultat = IdObjetFondationRoche,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Fondation roche",
			LegendeSymboles = new[] { "Table T1 : 1 roche moyenne (taille 2)", "Craft atelier : grille 3×3 (x9 roches moyennes, meme type)" },
			PatronCraft = new[] { "(R)(R)(R)", "(R)(R)(R)", "(R)(R)(R)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_126",
			IdResultat = IdObjetFondationBoisSoleRoche,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Fondation bois (sole roche)",
			LegendeSymboles = new[]
			{
				"Table T1 : 1 demi-buche standard + 1 roche moyenne",
				"Craft atelier : (D)(D)(D) / (D)(D)(D) / (R)(R)(R)"
			},
			PatronCraft = new[] { "(D)(D)(D)", "(D)(D)(D)", "(R)(R)(R)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_127",
			IdResultat = IdObjetFondationRocheSoleBois,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Fondation roche (sole bois)",
			LegendeSymboles = new[]
			{
				"Table T1 : 1 demi-buche standard + 1 roche moyenne",
				"Craft atelier : (R)(R)(R) / (R)(R)(R) / (D)(D)(D)"
			},
			PatronCraft = new[] { "(R)(R)(R)", "(R)(R)(R)", "(D)(D)(D)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_136",
			IdResultat = IdObjetSolBois,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Plancher bois",
			LegendeSymboles = new[] { "Table T1 : au moins 1 demi-buche standard (fendue en 2)", "Craft atelier : ligne du milieu (D)(D)(D)" },
			PatronCraft = new[] { "( )( )( )", "(D)(D)(D)", "( )( )( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_137",
			IdResultat = IdObjetSolRoche,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Plancher roche",
			LegendeSymboles = new[] { "Table T1 : 1 roche moyenne (taille 2)", "Craft atelier : ligne du haut ou du bas (R)(R)(R)" },
			PatronCraft = new[] { "(R)(R)(R)", "( )( )( )", "( )( )( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_138",
			IdResultat = IdObjetMuretBois,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Muret en bois",
			LegendeSymboles = new[] { "Table T1 : 1 bûche standard (pleine)", "Craft atelier : ligne du milieu (B)(B)(B)" },
			PatronCraft = new[] { "( )( )( )", "(B)(B)(B)", "( )( )( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_140",
			IdResultat = IdObjetMurBois,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Mur bois",
			LegendeSymboles = new[] { "Table T1 : 1 bûche standard (pleine)", "Craft atelier : grille 3x3 pleine (B)(B)(B) / (B)(B)(B) / (B)(B)(B), même essence" },
			PatronCraft = new[] { "(B)(B)(B)", "(B)(B)(B)", "(B)(B)(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_141",
			IdResultat = IdObjetMurBoisFenetre,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Mur bois fenêtré",
			LegendeSymboles = new[] { "Table T1 : 1 fenêtre bois + 1 bûche standard (pleine)", "Craft atelier : (B)(B)(B) / (B)(F)(B) / (B)(B)(B), bûches de même essence" },
			PatronCraft = new[] { "(B)(B)(B)", "(B)(F)(B)", "(B)(B)(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_143",
			IdResultat = IdObjetPorteBois,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Porte bois",
			LegendeSymboles = new[] { "Table T1 : 1 demi-bûche standard fendue en 2", "Craft atelier : ( )(DB)( ) / ( )(DB)( ) / ( )(DB)( ), même essence" },
			PatronCraft = new[] { "( )(DB)( )", "( )(DB)( )", "( )(DB)( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_144",
			IdResultat = IdObjetToitChaume,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Toit chaume",
			LegendeSymboles = new[] { "Table T1 : 1 branche brute + 1 ligature", "Craft atelier : ( )(L)( ) / (L)(Br)(L) / ( )( )( )" },
			PatronCraft = new[] { "( )(L)( )", "(L)(Br)(L)", "( )( )( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_142",
			IdResultat = IdObjetMurBoisCadrePorte,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Mur cadre de porte bois",
			LegendeSymboles = new[] { "Table T1 : 1 bûche standard (pleine)", "Craft atelier : (B)(B)(B) / (B)( )(B) / (B)( )(B), même essence" },
			PatronCraft = new[] { "(B)(B)(B)", "(B)( )(B)", "(B)( )(B)" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_139",
			IdResultat = IdObjetMuretPierre,
			Masque = CategorieAnalyse.Aucune,
			Titre = "Muret en pierre",
			LegendeSymboles = new[] { "Table T1 : 1 roche moyenne (taille 2)", "Craft atelier : ligne du milieu (R)(R)(R)" },
			PatronCraft = new[] { "( )( )( )", "(R)(R)(R)", "( )( )( )" }
		},
		new RecetteAnalysable
		{
			CleCraft = "id_146",
			IdResultat = IdObjetFenetreBois,
			Masque = CategorieAnalyse.DemiBuche | CategorieAnalyse.Ligature | CategorieAnalyse.BrancheBrute,
			Titre = "Fenêtre bois",
			LegendeSymboles = new[] { "DB = Demi-bûche fendue en 2", "L = Liage", "Br = Branche brute" },
			PatronCraft = new[] { "(L)(DB)(L)", "(DB)(Br)(DB)", "(L)(DB)(L)" }
		}
	};

	public int ObtenirCapaciteAnalyseurActif() => AnalyseurTier1Actif ? CapaciteAnalyseurTableTier1 : CapaciteAnalyseurManuel;

	public SlotInventaire[] ObtenirGrilleAnalyseurActif()
	{
		return AnalyseurTier1Actif ? GrilleAnalyseurTableTier1 : GrilleAnalyseurManuel;
	}

	public string ObtenirMessageAnalyseurActif()
	{
		return AnalyseurTier1Actif ? MessageAnalyseurTableTier1 : MessageAnalyseurManuel;
	}

	private void DefinirMessageAnalyseurActif(string message)
	{
		if (AnalyseurTier1Actif)
			MessageAnalyseurTableTier1 = message;
		else
			MessageAnalyseurManuel = message;
	}

	public void OuvrirAnalyseurManuel()
	{
		AnalyseurTier1Actif = false;
		TableAnalyseTier1Ouverte = null;
	}

	public void OuvrirAnalyseurTier1(ItemPhysique table)
	{
		AnalyseurTier1Actif = true;
		TableAnalyseTier1Ouverte = table;
	}

	public void FermerAnalyseurActif()
	{
		AnalyseurTier1Actif = false;
		TableAnalyseTier1Ouverte = null;
	}

	private static RecetteAnalysable[] ObtenirRecettesAnalyseurPourMode(bool tier1Actif)
	{
		if (!tier1Actif || RecettesAnalyseurTier1.Length == 0)
			return RecettesAnalyseurTier0;
		var fusion = new RecetteAnalysable[RecettesAnalyseurTier0.Length + RecettesAnalyseurTier1.Length];
		Array.Copy(RecettesAnalyseurTier0, fusion, RecettesAnalyseurTier0.Length);
		Array.Copy(RecettesAnalyseurTier1, 0, fusion, RecettesAnalyseurTier0.Length, RecettesAnalyseurTier1.Length);
		return fusion;
	}

	/// <summary>Légende des symboles + lignes du patron (grille craft), pour l’analyseur et le fil squelette.</summary>
	private static string FormaterTexteSchemaRecette(RecetteAnalysable recette)
	{
		if (recette == null) return "";
		var blocs = new List<string>(4);
		if (recette.LegendeSymboles != null && recette.LegendeSymboles.Length > 0)
			blocs.Add("Symboles : " + string.Join(", ", recette.LegendeSymboles));
		if (recette.PatronCraft != null && recette.PatronCraft.Length > 0)
			blocs.Add("Disposition (parentheses vides = case libre) :\n" + string.Join("\n", recette.PatronCraft));
		return string.Join("\n", blocs);
	}

	private static string FormaterMessageDecouverte(RecetteAnalysable recette)
	{
		string titre = string.IsNullOrWhiteSpace(recette?.Titre) ? "Craft inconnu" : recette.Titre;
		string schema = FormaterTexteSchemaRecette(recette);
		if (string.IsNullOrWhiteSpace(schema))
		{
			GD.PrintErr($"ZERO-K : Recette analyseur incomplète (clé={recette?.CleCraft ?? "?"}).");
			return $"Vous avez decouvert: {titre}. Le schema de craft est en preparation.";
		}
		return $"Vous avez decouvert: {titre}\n{schema}";
	}

	private static string[] ObtenirEffetsAnalyseBaie(int indexCouleur)
	{
		switch (ClampIndexCouleurBaie(indexCouleur))
		{
			case 0: // rouge
				return new[] { "-5 PV sur une partie du corps aleatoire", "+2 faim" };
			case 1: // violette
				return new[] { "-10 faim", "Affaiblit de moitie le poison de la baie rose en cours" };
			case 2: // orange
				return new[] { "-5 faim", "Saut x2 pendant 5 secondes" };
			case 3: // bleue
				return new[] { "+3 faim", "Degats recus reduits de moitie pendant 3 secondes" };
			case 4: // jaune
				return new[] { "+1 faim" };
			case 5: // verte
				return new[] { "+3 faim" };
			case 6: // noire
				return new[] { "+2 faim", "Vitesse augmentee pendant 5 secondes" };
			case 7: // rose
				return new[] { "Poison: 100 PV sur 24h sur une partie du corps aleatoire" };
			case 8: // cyan fluorescente
				return new[] { "Soigne 5 PV sur la partie du corps la plus endommagee", "+5 faim" };
			default:
				return new[] { "Effet inconnu" };
		}
	}

	/// <summary>Corde 20 issue de deux fibres distinctes (15/16/17), sans intestin.</summary>
	private static bool EstCordeMixteFibresCraft(in SlotInventaire s) =>
		!s.EstVide && s.ID == 20
		&& !EstVarianteCordeIntestinMixe(s)
		&& s.IndexChimique is 15 or 16 or 17
		&& s.IndexMorphologique is 15 or 16 or 17
		&& s.IndexChimique != s.IndexMorphologique;

	/// <summary>Clé de déblocage : pour le bâton façonné, une seule entrée <c>baton_faconne</c> couvre toutes les essences (IndexBotanique porté par le résultat craft).</summary>
	private static string CleCraftDepuisResultat(in SlotInventaire resultat)
	{
		if (resultat.ID <= 0) return "";
		if (resultat.ID == 20)
		{
			if (EstVarianteCordeIntestinMixe(resultat))
				return "corde_mixte_intestin";
			if (EstVarianteIntestinSolide(resultat))
				return "corde_intestin_solide";
			if (EstVarianteIntestin(resultat))
				return "corde_intestin";
			if (EstCordeMixteFibresCraft(resultat))
				return "corde_mixte";
			if (EstVarianteHerbeSolide(resultat) || EstVarianteLiane(resultat))
				return "corde_tier2";
			if (resultat.IndexChimique == 15 && resultat.IndexMorphologique == 15)
				return resultat.IndexBotanique >= 2 ? "corde_tier2" : "corde_tier0";
			return "corde_tier0";
		}
		if (resultat.ID == 32 && resultat.IndexChimique == 1)
			return "baton_faconne";
		if (resultat.ID == 47)
		{
			return resultat.IndexMorphologique switch
			{
				3 => "roche_marbre_pointe",
				1 => "roche_marbre_plate",
				0 => "roche_marbre_ronde",
				2 => "roche_marbre_ovale",
				_ => "id_47"
			};
		}
		return $"id_{resultat.ID}";
	}

	private bool EstFiliereCordeIntestinDebloquee()
	{
		return _craftsDecouverts.Contains("corde_intestin")
			|| _craftsDecouverts.Contains("corde_intestin_solide")
			|| _craftsDecouverts.Contains("corde_mixte_intestin");
	}

	public bool EstCraftDebloque(SlotInventaire resultat)
	{
		if (resultat.EstVide) return true;
		string cle = CleCraftDepuisResultat(resultat);
		if (string.IsNullOrEmpty(cle)) return true;
		if (_craftsDecouverts.Contains(cle)) return true;
		if (_craftsDecouverts.Contains($"id_{resultat.ID}")) return true;
		if (resultat.ID == 20 && EstFiliereCordeIntestinDebloquee()
			&& (EstVarianteIntestin(resultat) || EstVarianteIntestinSolide(resultat) || EstVarianteCordeIntestinMixe(resultat)))
			return true;
		return false;
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
		if (keys != null)
		{
			foreach (string key in keys)
			{
				if (!string.IsNullOrWhiteSpace(key))
					_craftsDecouverts.Add(key);
			}
		}
		// Sauvegardes : « corde_mixte_intestin » débloqué avant l'ajout de corde_intestin.
		if (_craftsDecouverts.Contains("corde_mixte_intestin"))
			_craftsDecouverts.Add("corde_intestin");
	}

	private static void FiltrerCandidatsAnalyseFondationMixte(List<RecetteAnalysable> candidates, SlotInventaire[] grilleAnalyse)
	{
		if (!GrilleAnalyseContientDemiBucheStandard(grilleAnalyse) || !GrilleAnalyseContientRocheMoyenne(grilleAnalyse))
			return;
		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			string cle = candidates[i].CleCraft;
			if (cle is "id_124" or "id_125" or "id_136" or "id_137" or "id_143")
				candidates.RemoveAt(i);
		}
	}

	/// <summary>Demi-bûche standard fendue en 2 (ID 30, morpho 1, taille 1).</summary>
	private static bool EstDemiBucheStandardFendue(in SlotInventaire s) =>
		!s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 1;

	/// <summary>Bûche standard pleine (ID 30, morpho 0, taille 1).</summary>
	private static bool EstBucheStandardPleine(in SlotInventaire s) =>
		!s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 1;

	private static bool EstRocheMoyenneMatiere(in SlotInventaire s) =>
		!s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexTaille == 2;

	private static bool GrilleAnalyseContientDemiBucheStandard(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (EstDemiBucheStandardFendue(grille[i]))
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientRocheMoyenne(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (EstRocheMoyenneMatiere(grille[i]))
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientBucheStandardPleine(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (EstBucheStandardPleine(grille[i]))
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientFenetreBois(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && grille[i].ID == IdObjetFenetreBois)
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientBrancheBrute(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && grille[i].ID == BlocChutant.ID_BRANCHE)
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientLigature(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && (grille[i].ID == 16 || grille[i].ID == 20))
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientBaton(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && grille[i].ID == 32)
				return true;
		}
		return false;
	}

	/// <summary>Demi-bûche courte non fendue valide pour l'atelier primitif : ID 30, morpho 0, taille 2.</summary>
	private static bool GrilleAnalyseContientDemiBucheCourteAtelier(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			SlotInventaire s = grille[i];
			if (!s.EstVide && s.ID == 30 && s.IndexMorphologique == 0 && s.IndexTaille == 2)
				return true;
		}
		return false;
	}

	/// <summary>Demi-bûche fendue en 2 valide pour l'atelier primitif : ID 30, morpho 1, taille 2.</summary>
	private static bool GrilleAnalyseContientDemiBucheFendueEn2Atelier(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			SlotInventaire s = grille[i];
			if (!s.EstVide && s.ID == 30 && s.IndexMorphologique == 1 && s.IndexTaille == 2)
				return true;
		}
		return false;
	}

	/// <summary>Présence d'une paire demi-bûche courte + demi-bûche courte fendue en 2, de même essence.</summary>
	private static bool GrilleAnalyseContientPaireDemiBuchesAtelierMemeEssence(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			SlotInventaire a = grille[i];
			if (a.EstVide || a.ID != 30 || a.IndexMorphologique != 0 || a.IndexTaille != 2)
				continue;
			for (int j = 0; j < grille.Length; j++)
			{
				if (i == j) continue;
				SlotInventaire b = grille[j];
				if (b.EstVide || b.ID != 30 || b.IndexMorphologique != 1 || b.IndexTaille != 2)
					continue;
				if (a.IndexBotanique == b.IndexBotanique)
					return true;
			}
		}
		return false;
	}

	/// <summary>Liage/corde (ID 16 ou 20) utilisé par la recette atelier primitif.</summary>
	private static bool GrilleAnalyseContientLiageAtelier(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && (grille[i].ID == 16 || grille[i].ID == 20))
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientHachettePrimitive(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && grille[i].ID == 106)
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientPiocheTier0(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && grille[i].ID == IdObjetPiochePierreTier0)
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientAtelierPrimitif(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			if (!grille[i].EstVide && grille[i].ID == 200)
				return true;
		}
		return false;
	}

	private static bool GrilleAnalyseContientPetiteRocheRonde(SlotInventaire[] grille)
	{
		if (grille == null) return false;
		for (int i = 0; i < grille.Length; i++)
		{
			SlotInventaire s = grille[i];
			if (!s.EstVide && ItemPhysique.EstIdRocheMatiere(s.ID) && s.IndexMorphologique == 0 && (s.IndexTaille == 0 || s.IndexTaille == 1))
				return true;
		}
		return false;
	}

	/// <summary>Déblocage table T1 : échantillons simples (1 matériau ou mix bois+roche), pas l'union bit à bit seule.</summary>
	private static bool AnalyseurTableT1SatisfaitFondationPlancher(RecetteAnalysable r, SlotInventaire[] grille)
	{
		bool aBois = GrilleAnalyseContientDemiBucheStandard(grille);
		bool aRoche = GrilleAnalyseContientRocheMoyenne(grille);
		bool aBuchePleine = GrilleAnalyseContientBucheStandardPleine(grille);
		bool aFenetreBois = GrilleAnalyseContientFenetreBois(grille);
		bool aBrancheBrute = GrilleAnalyseContientBrancheBrute(grille);
		bool aLigature = GrilleAnalyseContientLigature(grille);
		bool aBaton = GrilleAnalyseContientBaton(grille);
		bool aHachette = GrilleAnalyseContientHachettePrimitive(grille);
		bool aPioche = GrilleAnalyseContientPiocheTier0(grille);
		bool aAtelier = GrilleAnalyseContientAtelierPrimitif(grille);
		bool aPetiteRocheRonde = GrilleAnalyseContientPetiteRocheRonde(grille);
		return r.CleCraft switch
		{
			"id_148" => aHachette && aPioche && aAtelier && aPetiteRocheRonde && aBois,
			"id_147" => aBaton && aBois && aLigature,
			"id_124" => aBois && !aRoche,
			"id_125" => aRoche && !aBois,
			"id_126" or "id_127" => aBois && aRoche,
			"id_136" => aBois,
			"id_137" => aRoche,
			"id_138" => aBuchePleine,
			"id_140" => aBuchePleine,
			"id_141" => aBuchePleine && aFenetreBois,
			"id_143" => aBois,
			"id_144" => aBrancheBrute && aLigature,
			"id_142" => aBuchePleine,
			"id_139" => aRoche,
			_ => false
		};
	}

	private static CategorieAnalyse DeterminerCategoriesAnalyse(in SlotInventaire s)
	{
		if (s.EstVide) return CategorieAnalyse.Aucune;
		CategorieAnalyse c = CategorieAnalyse.Aucune;
		bool estVoxelTerrainMinerai = Atlas_Matiere.EssayerLireIdVoxelTerrain(s, out int idVoxelTerrain)
			&& Atlas_Matiere.EstIdVoxelTerrainMinerai(idVoxelTerrain);
		if (s.ID is 15 or 16 or 17) c |= CategorieAnalyse.Fibre;
		if (s.ID == Joueur.IdObjetIntestinBoeufNettoye || s.ID == Joueur.IdObjetIntestinBoeuf)
			c |= CategorieAnalyse.IntestinBoeuf;
		if (s.ID == 16 || s.ID == 20) c |= CategorieAnalyse.Ligature;
		if (s.ID == 20) c |= CategorieAnalyse.Corde;
		if (s.ID == 21) c |= CategorieAnalyse.Tissu;
		if (s.ID == 32)
		{
			c |= CategorieAnalyse.Baton;
			if (s.IndexChimique == 1) c |= CategorieAnalyse.BatonFaconne;
			if (s.IndexMorphologique == 4) c |= CategorieAnalyse.BatonEnT;
		}
		// Branche (31) : manche pour hachette primitive (masque dédié), pas la catégorie « Baton » seule (sinon une branche seule déclenche « bâton façonné »).
		if (s.ID == BlocChutant.ID_BRANCHE)
			c |= CategorieAnalyse.BrancheBrute;
		if (s.ID == 30)
		{
			if (s.IndexMorphologique == 1) c |= CategorieAnalyse.DemiBuche;
			if (s.IndexMorphologique == 0) c |= CategorieAnalyse.BuchePleine;
			if (s.IndexTaille == 3 && s.IndexMorphologique == 0) c |= CategorieAnalyse.BucheRondinCourt;
			if (s.IndexTaille == 3 && s.IndexMorphologique == 3) c |= CategorieAnalyse.MiniBucheHuitieme;
		}
		if (s.ID == 105) c |= CategorieAnalyse.DaguePrimitive;
		if (s.ID == IdObjetMailletBois) c |= CategorieAnalyse.MailletBois;
		if (s.ID == IdObjetBolBois) c |= CategorieAnalyse.BolBois;
		if (s.ID == IdObjetBolEau) c |= CategorieAnalyse.BolEau;
		if (s.ID == IdObjetArgileHumidifiee) c |= CategorieAnalyse.ArgileHumidifiee;
		if (s.ID == IdObjetBolArgile) c |= CategorieAnalyse.ArgileHumidifiee;
		if (s.ID == IdObjetMouleArgile) c |= CategorieAnalyse.ArgileHumidifiee;
		if (s.ID == IdObjetTorchie) c |= CategorieAnalyse.Torchie;
		if (FourTorchieThermodynamique.EstBolCeramiqueRefroidi(s) || FourTorchieThermodynamique.EstBolCeramiqueChaud(s))
			c |= CategorieAnalyse.BolCeramique;
		if (s.ID == IdObjetBolCeramiqueEtain) c |= CategorieAnalyse.BolCeramique;
		if (EstIdEtainRecolte(s.ID)) c |= CategorieAnalyse.Etain;
		if (Atlas_Matiere.EstSlotVoxelArgile(s)) c |= CategorieAnalyse.VoxelArgile;
		if (Atlas_Matiere.EstSlotVoxelBoue(s)) c |= CategorieAnalyse.VoxelBoue;
		if (Atlas_Matiere.EstSlotVoxelTerreAride(s)) c |= CategorieAnalyse.VoxelTerreAride;
		if (s.ID == IdObjetMortierPilonBois) c |= CategorieAnalyse.MortierPilonBois;
		if (s.ID == IdObjetOsBoeuf) c |= CategorieAnalyse.OsBoeuf;
		if (s.ID == IdObjetPinceOs) c |= CategorieAnalyse.PinceOs;
		if (s.ID == IdObjetCuirBoeuf) c |= CategorieAnalyse.CuirBoeuf;
		if (s.ID == IdObjetPochetteTier0) c |= CategorieAnalyse.Pochette;
		if (s.ID == IdObjetCeinturePoches) c |= CategorieAnalyse.CeinturePoches;
		// Les voxels minerais utilisent l'ID proxy 2 en inventaire; on évite donc de les classer "roche brute".
		if (s.ID == 2 && !estVoxelTerrainMinerai) c |= CategorieAnalyse.RocheVoxelBrute;
		if (ItemPhysique.EstMatiereSilexParIdObjet(s.ID))
			c |= CategorieAnalyse.Silex;
		if (ItemPhysique.EstIdRocheMatiere(s.ID))
		{
			int idxGeo = ItemPhysique.IndexChimiqueDepuisIdRoche(s.ID);
			if (idxGeo == 10 || idxGeo == 11)
				c |= CategorieAnalyse.RocheSulfuree;
		}
		if (s.ID == IdObjetPitFeu)
			c |= CategorieAnalyse.PitFeu;
		if (ItemPhysique.EstIdRocheMatiere(s.ID))
		{
			c |= CategorieAnalyse.RocheMatiere;
			if (s.IndexMorphologique == 0) c |= CategorieAnalyse.RocheRonde;
			else if (s.IndexMorphologique == 1) c |= CategorieAnalyse.RochePlate;
			else if (s.IndexMorphologique == 2) c |= CategorieAnalyse.RocheOvale;
			else if (s.IndexMorphologique == 3) c |= CategorieAnalyse.RochePointe;
		}
		return c;
	}

	/// <summary>Hachette primitive : bâton (32) <b>ou</b> branche (31) + roche plate + ligature. Les autres recettes suivent le masque bit à bit.</summary>
	private static bool AnalyseurUnionSatisfaitRecette(CategorieAnalyse union, RecetteAnalysable r, SlotInventaire[] grilleAnalyse)
	{
		if (r.CleCraft == "id_106")
		{
			bool manche = (union & CategorieAnalyse.Baton) != 0 || (union & CategorieAnalyse.BrancheBrute) != 0;
			bool roche = (union & CategorieAnalyse.RochePlate) != 0;
			bool lig = (union & CategorieAnalyse.Ligature) != 0;
			return manche && roche && lig;
		}
		// Bâton façonné : une seule clé « baton_faconne » pour toutes les essences (chêne, bouleau, …) — bâton brut (32) ou branche (31).
		if (r.CleCraft == "baton_faconne")
			return (union & CategorieAnalyse.Baton) != 0 || (union & CategorieAnalyse.BrancheBrute) != 0;
		if (r.CleCraft == "corde_mixte" && grilleAnalyse != null)
		{
			bool herbe = false, liane = false, boyau = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == 15) herbe = true;
				else if (s.ID == 16) liane = true;
				else if (s.ID == 17) boyau = true;
			}
			int nbFibres = (herbe ? 1 : 0) + (liane ? 1 : 0) + (boyau ? 1 : 0);
			return nbFibres >= 2;
		}
		if (r.CleCraft == "corde_mixte_intestin" && grilleAnalyse != null)
		{
			bool intestin = false, fibre = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (EstIntestinUtilisablePourCraft(s)) intestin = true;
				else if (s.ID is 15 or 16 or 17) fibre = true;
			}
			return intestin && fibre;
		}
		// Atelier primitif (200) : demi-bûche standard + demi-bûche fendue en 2 + petite roche ronde + liage.
		// IMPORTANT : aligne strictement l'analyseur sur la vraie recette de craft (Atlas_Matiere).
		if (r.CleCraft == "id_200")
		{
			if (grilleAnalyse == null) return false;
			bool aPaireBoisMemeEssence = GrilleAnalyseContientPaireDemiBuchesAtelierMemeEssence(grilleAnalyse);
			bool aRocheRonde = GrilleAnalyseContientPetiteRocheRonde(grilleAnalyse);
			bool aLiage = GrilleAnalyseContientLiageAtelier(grilleAnalyse);
			return aPaireBoisMemeEssence && aRocheRonde && aLiage;
		}
		if (r.CleCraft == "corde_intestin" && grilleAnalyse != null)
		{
			int nbIntestins = 0;
			int nbAutres = 0;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (EstIntestinUtilisablePourCraft(s))
					nbIntestins++;
				else
					nbAutres++;
			}
			// Un échantillon suffit à l'analyseur ; le craft exige deux intestins (EvaluerRecette).
			return nbIntestins >= 1 && nbAutres == 0;
		}
		if (r.CleCraft == "id_155" && grilleAnalyse != null)
		{
			bool bolEau = false, argile = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetBolEau) bolEau = true;
				else if (Atlas_Matiere.EstSlotVoxelArgile(s)) argile = true;
				else return false;
			}
			return bolEau && argile;
		}
		if (r.CleCraft == "id_7" && grilleAnalyse != null)
		{
			bool bolEau = false, terreAride = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetBolEau) bolEau = true;
				else if (Atlas_Matiere.EstSlotVoxelTerreAride(s)) terreAride = true;
				else return false;
			}
			return bolEau && terreAride;
		}
		if (r.CleCraft == "id_156" && grilleAnalyse != null)
		{
			bool argileHumid = false, fibreHerbe = false, boue = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetArgileHumidifiee) argileHumid = true;
				else if (Atlas_Matiere.EstSlotBrinHerbe(s)) fibreHerbe = true;
				else if (Atlas_Matiere.EstSlotVoxelBoue(s)) boue = true;
				else return false;
			}
			return argileHumid && fibreHerbe && boue;
		}
		if (r.CleCraft == "id_157" && grilleAnalyse != null)
		{
			bool aTorchie = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetTorchie) aTorchie = true;
				else return false;
			}
			return aTorchie;
		}
		if (r.CleCraft == "id_158" && grilleAnalyse != null)
		{
			bool aArgileHumid = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetArgileHumidifiee) aArgileHumid = true;
				else return false;
			}
			return aArgileHumid;
		}
		if (r.CleCraft == "id_161" && grilleAnalyse != null)
		{
			bool aArgileHumid = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetArgileHumidifiee) aArgileHumid = true;
				else return false;
			}
			return aArgileHumid;
		}
		if (r.CleCraft == "id_167" && grilleAnalyse != null)
		{
			bool bolCer = false, etain = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (FourTorchieThermodynamique.EstBolCeramiqueRefroidi(s)
					|| FourTorchieThermodynamique.EstBolCeramiqueChaud(s))
					bolCer = true;
				else if (EstIdEtainRecolte(s.ID))
					etain = true;
				else return false;
			}
			return bolCer && etain;
		}
		if (r.CleCraft == "id_160" && grilleAnalyse != null)
		{
			bool aOs = false;
			for (int i = 0; i < grilleAnalyse.Length; i++)
			{
				SlotInventaire s = grilleAnalyse[i];
				if (s.EstVide) continue;
				if (s.ID == IdObjetOsBoeuf) aOs = true;
				else return false;
			}
			return aOs;
		}
		if (r.CleCraft is "id_147" or "id_148" or "id_124" or "id_125" or "id_126" or "id_127" or "id_136" or "id_137" or "id_138" or "id_139" or "id_140" or "id_141" or "id_142" or "id_143" or "id_144")
			return grilleAnalyse != null && AnalyseurTableT1SatisfaitFondationPlancher(r, grilleAnalyse);
		return (union & r.Masque) == r.Masque;
	}

	public bool EssayerAnalyserCrafts(out string message)
	{
		SlotInventaire[] grilleAnalyse = ObtenirGrilleAnalyseurActif();
		RecetteAnalysable[] recettesActives = ObtenirRecettesAnalyseurPourMode(AnalyseurTier1Actif);
		CategorieAnalyse masque = CategorieAnalyse.Aucune;
		bool aDesItems = false;
		for (int i = 0; i < grilleAnalyse.Length; i++)
		{
			SlotInventaire s = grilleAnalyse[i];
			if (s.EstVide) continue;
			aDesItems = true;
			masque |= DeterminerCategoriesAnalyse(s);
		}

		if (!aDesItems)
		{
			message = "Depose des objets dans l'analyseur.";
			DefinirMessageAnalyseurActif(message);
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		void ConsommerAnalyseur()
		{
			for (int i = 0; i < grilleAnalyse.Length; i++)
				grilleAnalyse[i] = new SlotInventaire();
		}

		// Analyse mono-baie: ne debloque pas de craft, mais revele un effet de la baie.
		int nbSlotsOccupes = 0;
		SlotInventaire slotUnique = new SlotInventaire();
		for (int i = 0; i < grilleAnalyse.Length; i++)
		{
			if (grilleAnalyse[i].EstVide) continue;
			nbSlotsOccupes++;
			slotUnique = grilleAnalyse[i];
			if (nbSlotsOccupes > 1) break;
		}
		if (nbSlotsOccupes == 1 && slotUnique.ID == IdObjetBaie)
		{
			float pReussiteBaie = ObtenirChanceReussiteAnalyseManuelle();
			if (GD.Randf() >= pReussiteBaie)
			{
				ConsommerAnalyseur();
				ulong xpIntelligenceRecueEchecBaie = AjouterXpFutureStateEtRetourEffectif("Intelligence", 2UL);
				message = $"Echec de l'analyse de baie : echantillon consomme (+{xpIntelligenceRecueEchecBaie} XP Intelligence).";
				DefinirMessageAnalyseurActif(message);
				AlerteSqueletteBoiteNoire("Analyseur : " + message);
				return false;
			}

			int idxBaie = ClampIndexCouleurBaie(slotUnique.IndexChimique);
			string couleur = ObtenirLexemeCouleurBaiePourNomInventaire(idxBaie);
			string[] effetsPossibles = ObtenirEffetsAnalyseBaie(idxBaie);
			int pickEffet = effetsPossibles.Length <= 1 ? 0 : GD.RandRange(0, effetsPossibles.Length - 1);
			string effetRevele = effetsPossibles[pickEffet];
			ConsommerAnalyseur();
			ulong xpIntelligenceRecueSuccesBaie = AjouterXpFutureStateEtRetourEffectif("Intelligence", 1UL);
			message = $"Analyse reussie : baie {couleur} -> {effetRevele} (+{xpIntelligenceRecueSuccesBaie} XP Intelligence).";
			DefinirMessageAnalyseurActif(message);
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return true;
		}

		var candidates = new List<RecetteAnalysable>();
		for (int i = 0; i < recettesActives.Length; i++)
		{
			RecetteAnalysable r = recettesActives[i];
			if (AnalyseurUnionSatisfaitRecette(masque, r, grilleAnalyse))
				candidates.Add(r);
		}
		FiltrerCandidatsAnalyseFondationMixte(candidates, grilleAnalyse);
		if (candidates.Count == 0)
		{
			ConsommerAnalyseur();
			message = "Aucun craft ne se compose uniquement de ces materiaux.";
			DefinirMessageAnalyseurActif(message);
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
			DefinirMessageAnalyseurActif(message);
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		float pReussite = ObtenirChanceReussiteAnalyseManuelle();
		if (GD.Randf() >= pReussite)
		{
			ConsommerAnalyseur();
			ulong xpIntelligenceRecue = AjouterXpFutureStateEtRetourEffectif("Intelligence", 2UL);
			message = $"Echec de l'analyse : tes echantillons sont consumes. Tu en retires une lecon (+{xpIntelligenceRecue} XP Intelligence).";
			DefinirMessageAnalyseurActif(message);
			AlerteSqueletteBoiteNoire("Analyseur : " + message);
			return false;
		}

		int pick = nonDecouvertes.Count == 1 ? 0 : GD.RandRange(0, nonDecouvertes.Count - 1);
		RecetteAnalysable choisie = nonDecouvertes[pick];
		DebloquerCraft(choisie.CleCraft);
		if (choisie.CleCraft == "corde_mixte_intestin")
			DebloquerCraft("corde_intestin");
		ConsommerAnalyseur();
		ulong xpIntelligenceRecueSucces = AjouterXpFutureStateEtRetourEffectif("Intelligence", 1UL);

		message = FormaterMessageDecouverte(choisie) + $" (+{xpIntelligenceRecueSucces} XP Intelligence).";
		DefinirMessageAnalyseurActif(message);
		string titreCourt = string.IsNullOrWhiteSpace(choisie.Titre) ? "nouvelle formule" : choisie.Titre;
		string schemaChat = FormaterTexteSchemaRecette(choisie);
		if (string.IsNullOrEmpty(schemaChat))
			AlerteSqueletteBoiteNoire($"Analyseur : reussite — tu decouvres « {titreCourt} » (+{xpIntelligenceRecueSucces} XP Intelligence). Ouvre le menu Q pour plus de details.");
		else
			AlerteSqueletteBoiteNoire($"Analyseur : reussite — « {titreCourt} » (+{xpIntelligenceRecueSucces} XP Intelligence).\n{schemaChat}");
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
			max *= Joueur.ObtenirMultiplicateurPileSac(EquipementSacDos);
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
