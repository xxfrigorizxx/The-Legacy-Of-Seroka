using Godot;
using System.Collections.Generic;

/// <summary>ADN de la matière organique : régit la physique du bois (flottabilité, combustion, résistance).</summary>
public struct ProfilBotanique
{
	public string Nom;
	public byte ID_Tronc;
	public byte ID_Feuille;

	/// <summary>Densité par rapport à l'eau (1.0). &lt; 1.0 flotte, &gt; 1.0 coule.</summary>
	public float MasseDensite;
	/// <summary>Points de vie du bloc face à une lame.</summary>
	public float ResistanceHache;
	/// <summary>Temps de combustion en secondes par unité de volume.</summary>
	public float Combustibilite;
	/// <summary>Température générée par la combustion (utile pour la forge plus tard).</summary>
	public float ChaleurDegagee;
	/// <summary>0 = Cassant (verre), 1 = Très flexible (Arc/Tressage possible sans casser).</summary>
	public float Flexibilite;
}

/// <summary>Données d'un arbre pour InventaireArbres (stade 0=plançon, 1-3=croissance, 3=mature).</summary>
public struct DonneesArbre
{
	public byte Stage;
	public uint Seed;
}

/// <summary>Générateur L-System pour la botanique. Interprète les règles de Lindenmayer et produit des positions de voxels.</summary>
/// <remarks>Chêne réaliste : tronc épais, branches en spirale, feuillage en dôme. Croissance paramétrée par stade.</remarks>
public static class LSystem_Botanique
{
	public static readonly ProfilBotanique Chene = new ProfilBotanique
	{
		Nom = "Chêne",
		ID_Tronc = 30,
		ID_Feuille = 31,
		MasseDensite = 0.85f,
		ResistanceHache = 200f,
		Combustibilite = 300f,
		ChaleurDegagee = 800f,
		Flexibilite = 0.2f
	};

	public static readonly ProfilBotanique Bouleau = new ProfilBotanique
	{
		Nom = "Bouleau",
		ID_Tronc = 30,
		ID_Feuille = 31,
		MasseDensite = 0.75f,
		ResistanceHache = 150f,
		Combustibilite = 280f,
		ChaleurDegagee = 740f,
		Flexibilite = 0.25f
	};

	public static readonly ProfilBotanique Pin = new ProfilBotanique
	{
		Nom = "Pin",
		ID_Tronc = 30,
		ID_Feuille = 31,
		MasseDensite = 0.68f,
		ResistanceHache = 105f,
		Combustibilite = 330f,
		ChaleurDegagee = 760f,
		Flexibilite = 0.3f
	};

	public static readonly ProfilBotanique Sapin = new ProfilBotanique
	{
		Nom = "Sapin",
		ID_Tronc = 30,
		ID_Feuille = 31,
		MasseDensite = 0.71f,
		ResistanceHache = 118f,
		Combustibilite = 320f,
		ChaleurDegagee = 770f,
		Flexibilite = 0.28f
	};

	public static readonly ProfilBotanique Jungle = new ProfilBotanique
	{
		Nom = "Fromager (Kapokier)",
		ID_Tronc = 30,
		ID_Feuille = 31,
		MasseDensite = 0.35f,   // 📖 FIX CRITIQUE : Extrêmement léger (flotte comme un bouchon)
		ResistanceHache = 60f,  // 📖 FIX CRITIQUE : Bois spongieux, s'abat très rapidement
		Combustibilite = 150f,  // 📖 Brûle comme une allumette
		ChaleurDegagee = 450f,  // 📖 Ne fait pas de bonnes braises
		Flexibilite = 0.10f
	};

	/// <summary>Index du chêne dans TableBotanique.</summary>
	public const byte IndexChene = 0;
	public const byte IndexBouleau = 1; // Nouvelle espece
	public const byte IndexPin = 2; // Nouvelle espece
	public const byte IndexSapin = 3; // Conifere de climat froid modere
	public const byte IndexJungle = 4; // Arbre tropical a couronne haute

	/// <summary>Table des essences. 0=Chêne, 1=Bouleau, 2=Pin, 3=Sapin, 4=Jungle.</summary>
	public static readonly ProfilBotanique[] TableBotanique = { Chene, Bouleau, Pin, Sapin, Jungle };

	/// <summary>Retourne le profil botanique pour l'index d'espèce (0 = chêne). Clamp si hors bornes.</summary>
	public static ProfilBotanique ObtenirProfil(byte indexEspece)
	{
		int i = Mathf.Clamp(indexEspece, 0, TableBotanique.Length - 1);
		return TableBotanique[i];
	}

	/// <summary>Stades de croissance : 0=plançon (1 iter), 1=jeune (2 iter), 2=adulte (3 iter), 3=mature (4 iter).</summary>
	public const int STADE_MAX = 3;

	private struct TortueEtat
	{
		public Vector3 Position;
		public Vector3 Direction;
	}

	private static float RandFromSeed(uint seed, int index)
	{
		uint h = (seed * 73856093u) ^ (uint)(index * 19349663);
		return ((h % 10000) / 10000f);
	}

	/// <summary>Génère la chaîne d'ADN après N itérations.</summary>
	public static string GenererChaine(string axiome, Dictionary<char, string> regles, int iterations)
	{
		string actuel = axiome;
		for (int i = 0; i < iterations; i++)
		{
			string suivant = "";
			foreach (char c in actuel)
			{
				if (regles.ContainsKey(c))
					suivant += regles[c];
				else
					suivant += c;
			}
			actuel = suivant;
		}
		return actuel;
	}

	/// <summary>Chêne organique : angles 360°, 1–4 branches asymétriques, sous-branches possibles. Chaque arbre différent.</summary>
	public static string GenererChaineCheneOrganique(int iterations, uint seed)
	{
		string actuel = "TTTTA";
		string[] dirs8 = { "[+B]", "[+>B]", "[>B]", "[->B]", "[-B]", "[-<B]", "[<B]", "[+<B]" };
		int iterA = 0, iterB = 0, iterb = 0, iterc = 0;
		for (int i = 0; i < iterations; i++)
		{
			string suivant = "";
			foreach (char c in actuel)
			{
				if (c == 'A')
				{
					string branches = "";
					int nb = 2 + (int)(RandFromSeed(seed, iterA * 7) * 4); // 2 à 5 branches, canopée dense
					var indicesUtilises = new HashSet<int>();
					for (int j = 0; j < nb; j++)
					{
						int idx;
						int attempts = 0;
						do { idx = (int)(RandFromSeed(seed, iterA * 7 + j * 5 + attempts) * dirs8.Length) % dirs8.Length; attempts++; }
						while (indicesUtilises.Contains(idx) && attempts < 16);
						indicesUtilises.Add(idx);
						branches += dirs8[idx];
					}
					// Chêne moins étiré: un étage n'ajoute pas toujours de tronc.
					bool pousseTronc = RandFromSeed(seed, 500 + iterA * 13) < 0.72f;
					suivant += branches + (pousseTronc ? "TA" : "A");
					iterA++;
				}
				else if (c == 'B')
				{
					float r = RandFromSeed(seed, iterB * 17 + 100);
					if (r < 0.55f)
					{
						// Sous-branches + feuillage le long : FFL = feuille intermédiaire avant ramifications
						string[] subDirs = { "[+b]", "[-b]", "[>b]", "[<b]", "[+>b]", "[+<b]", "[->b]", "[-<b]" };
						int nSub = 2 + (int)(RandFromSeed(seed, iterB * 17 + 101) * 3);
						var seen = new HashSet<int>();
						string sub = "";
						for (int k = 0; k < nSub; k++)
						{
							int d = (int)(RandFromSeed(seed, iterB * 17 + 102 + k) * subDirs.Length) % subDirs.Length;
							if (!seen.Contains(d)) { seen.Add(d); sub += subDirs[d]; }
						}
						suivant += "FFL" + (sub.Length > 0 ? sub : "[+b][-b]") + "L"; // L intermédiaire + L finale
					}
					else
					{
						// Feuillage direct : 2 à 5 directions + feuillage au centre
						string[] leafDirs = { "[+L]", "[>L]", "[-L]", "[<L]", "[+>L]", "[+<L]", "[->L]", "[-<L]" };
						int nLeaf = 2 + (int)(RandFromSeed(seed, iterB * 17 + 103) * 4);
						var seen = new HashSet<int>();
						string leaf = "";
						for (int k = 0; k < nLeaf; k++)
						{
							int d = (int)(RandFromSeed(seed, iterB * 17 + 104 + k) * leafDirs.Length) % leafDirs.Length;
							if (!seen.Contains(d)) { seen.Add(d); leaf += leafDirs[d]; }
						}
						suivant += "FFL" + (leaf.Length > 0 ? leaf : "L") + "L"; // FFL = feuillage le long + aux extrémités
					}
					iterB++;
				}
				else if (c == 'b')
				{
					// Sous-branche : ajoute des sous-sous-branches (c) pour éviter les chênes "squelettes".
					float r = RandFromSeed(seed, iterb * 19 + 200);
					suivant += r < 0.30f
						? "FF[+b][-b][>c][<c]L"
						: (r < 0.62f
							? "F[+c]F[-c][>b]L"
							: "FF[+c][-c][>c][<c]L");
					iterb++;
				}
				else if (c == 'c')
				{
					// Sous-sous-branche terminale: petite ramification feuillue, sans récursion.
					float r = RandFromSeed(seed, 2600 + iterc * 23);
					suivant += r < 0.5f ? "F[+L][-L]L" : "FF[<L][>L]L";
					iterc++;
				}
				else
					suivant += c;
			}
			actuel = suivant;
		}
		return actuel;
	}

	public static string GenererChaineBouleauOrganique(int iterations, uint seed)
	{
		// Bouleau : tronc dominant mais NON exponentiel, branches fines concentrées vers le haut.
		string actuel = "TA";
		int iterA = 0, iterb = 0, iterc = 0;
		for (int i = 0; i < iterations; i++)
		{
			string suivant = "";
			foreach (char c in actuel)
			{
				if (c == 'A')
				{
					float r = RandFromSeed(seed, 700 + iterA * 11);
					// Tronc pas systématique: limite l'effet étiré.
					bool pousseTronc = RandFromSeed(seed, 760 + iterA * 17) < 0.82f;
					string prefix = pousseTronc ? "T" : "";
					if (r < 0.33f) suivant += prefix + "[+b][<b][-c]A";
					else if (r < 0.66f) suivant += prefix + "[+b][-b][>c]A";
					else suivant += prefix + "[<b][->b][+c]A";
					iterA++;
				}
				else if (c == 'b')
				{
					float r = RandFromSeed(seed, 900 + iterb * 13);
					// Rameaux de bouleau avec sous-sous-branche pour densifier la silhouette.
					suivant += r < 0.35f
						? "FFL[+c][<c][->c]"
						: (r < 0.70f ? "FL[+b][-c][>c]L" : "F[<c][+c]L");
					iterb++;
				}
				else if (c == 'c')
				{
					float r = RandFromSeed(seed, 1700 + iterc * 19);
					suivant += r < 0.5f ? "F[+L][<L]L" : "FF[<L][->L]L";
					iterc++;
				}
				else
					suivant += c;
			}
			actuel = suivant;
		}
		return actuel;
	}

	public static string GenererChainePinOrganique(int iterations, uint seed)
	{
		// Pin conique: tronc central dominant, 8 directions autour du tronc, sous-branches latérales.
		// Plus on est bas dans l'arbre, plus des branches peuvent manquer (cassées/absentes) pour casser la symétrie.
		string axiome = "TTTA";
		int iterb = 0;
		for (int i = 0; i < iterations; i++)
		{
			string next = "";
			foreach (char c in axiome)
			{
				// A = étage de branches autour du tronc (8 directions), avec trous asymétriques.
				if (c == 'A')
				{
					float t = iterations > 1 ? (float)i / (iterations - 1) : 1f; // 0=base, 1=sommet
					string[] dirs8 = { "R", "RR", "RRR", "RRRR", "r", "rr", "rrr", "rrrr" };

					// Bas: déjà 3-4 côtés, puis davantage en montant jusqu'à 7-8.
					int minCible = t < 0.2f ? 3 : (t < 0.45f ? 4 : (t < 0.7f ? 5 : 7));
					int maxCible = t < 0.2f ? 4 : (t < 0.45f ? 6 : (t < 0.7f ? 7 : 8));
					int cible = Mathf.Clamp(minCible + (int)(RandFromSeed(seed, 3600 + i * 17) * (maxCible - minCible + 1)), 2, 8);
					int cibleInter = Mathf.Clamp(Mathf.Max(2, cible - 2), 2, 6);

					// Distribution circulaire (évite l'effet "deux côtés uniquement" sur tout l'arbre).
					int[] ordreCirculaire = { 0, 4, 2, 6, 1, 5, 3, 7 };
					int offset = (int)(RandFromSeed(seed, 3700 + i * 29) * 8f) % 8;
					string couronne = "";
					for (int k = 0; k < cible; k++)
					{
						int idx = ordreCirculaire[(k + offset) % 8];
						// Jitter latéral uniquement (pas de verticale forte) pour garder des branches surtout horizontales.
						couronne += "[" + dirs8[idx] + "j+bcbL]";
					}
					// Etage intermédiaire: comble visuellement entre deux étages principaux.
					int offsetInter = (offset + 1 + (int)(RandFromSeed(seed, 3729 + i * 31) * 3f)) % 8;
					string couronneInter = "";
					for (int k = 0; k < cibleInter; k++)
					{
						int idx = ordreCirculaire[(k + offsetInter) % 8];
						couronneInter += "[" + dirs8[idx] + "j+bcL]";
					}

					next += "T" + couronne + "T" + couronneInter + "A";
				}
				// b = branche principale + sous-branches latérales.
				// Le couple R/r + + incline dans des plans différents => vraies ramifications autour de la branche.
				else if (c == 'b')
				{
					float r = RandFromSeed(seed, 4100 + i * 131 + iterb * 17);
					// Pin: éviter les "bras" interminables -> sous-branches courtes/intermédiaires, très peu de récursion.
					next += r < 0.56f
						? "Fj[R+c]Fj[r+c]cL"
						: (r < 0.82f ? "Fj[RR+c]Fj[rr+c]L" : "Fj[R+F]Fj[r+F]L");
					iterb++;
				}
				else next += c;
			}
			axiome = next;
		}
		return axiome;
	}

	public static string GenererChaineSapinOrganique(int iterations, uint seed)
	{
		// Sapin: conifere plus touffu que le pin, etage quasi continu du bas vers le haut.
		// On démarre plus bas qu'avant pour avoir des branches proches du sol.
		string axiome = "TA";
		int iterb = 0;
		for (int i = 0; i < iterations; i++)
		{
			string next = "";
			foreach (char c in axiome)
			{
				if (c == 'A')
				{
					float t = iterations > 1 ? (float)i / (iterations - 1) : 1f; // 0=base, 1=sommet
					string[] dirs8 = { "R", "RR", "RRR", "RRRR", "r", "rr", "rrr", "rrrr" };
					// Croissance progressive: jeune sapin = moins d'étages/branches, mature = silhouette pleine.
					int bonusMaturite = Mathf.Clamp(iterations - 2, 0, 3);
					int minCible = t < 0.25f ? (3 + bonusMaturite) : (t < 0.7f ? (3 + bonusMaturite) : (2 + bonusMaturite));
					int maxCible = t < 0.25f ? (5 + bonusMaturite) : (t < 0.7f ? (5 + bonusMaturite) : (4 + bonusMaturite));
					int cible = Mathf.Clamp(minCible + (int)(RandFromSeed(seed, 5200 + i * 17) * (maxCible - minCible + 1)), 3, 8);
					int[] ordre = { 0, 4, 2, 6, 1, 5, 3, 7 };
					int offset = (int)(RandFromSeed(seed, 5300 + i * 29) * 8f) % 8;
					string couronne = "";
					for (int k = 0; k < cible; k++)
					{
						int idx = ordre[(k + offset) % 8];
						// Orientation clairement descendante: "jupe" de sapin jusqu'au bas du tronc.
						couronne += "[" + dirs8[idx] + "j--bbcbL]";
					}
					next += "T" + couronne + "A";
				}
				else if (c == 'b')
				{
					float r = RandFromSeed(seed, 6100 + i * 131 + iterb * 19);
					next += r < 0.58f
						? "Fj[R+b]Fj[r+b][+c][-c]L"
						: (r < 0.86f ? "Fj[RR+c]Fj[rr+c]L" : "Fj[R+F][r+F]L");
					iterb++;
				}
				else next += c;
			}
			axiome = next;
		}
		return axiome;
	}

	public static string GenererChaineJungleOrganique(int iterations, uint seed)
	{
		// Arbre de jungle: long tronc nu, canopée concentrée dans le tiers supérieur.
		string axiome = "TTTTTA";
		int iterB = 0;
		int iterb = 0;
		for (int i = 0; i < iterations; i++)
		{
			string next = "";
			foreach (char c in axiome)
			{
				if (c == 'A')
				{
					float t = iterations > 1 ? (float)i / (iterations - 1) : 1f; // 0=bas, 1=sommet
					if (t < 0.55f)
					{
						// Bas et milieu: surtout tronc.
						next += "TTA";
						continue;
					}

					// Haut: couronne très dense, multi-étages, avec sous-branches.
					string[] dirs8 = { "R", "RR", "RRR", "RRRR", "r", "rr", "rrr", "rrrr" };
					int[] ordre = { 0, 4, 2, 6, 1, 5, 3, 7 };
					int cible = t < 0.8f ? 6 : 8;
					int offset = (int)(RandFromSeed(seed, 7800 + i * 17) * 8f) % 8;
					string couronne = "";
					for (int k = 0; k < cible; k++)
					{
						int idx = ordre[(k + offset) % 8];
						couronne += "[" + dirs8[idx] + "jv+BB[+b][-b]L]";
					}
					int offsetInter = (offset + 1 + (int)(RandFromSeed(seed, 7849 + i * 31) * 3f)) % 8;
					string couronneInter = "";
					for (int k = 0; k < Mathf.Max(3, cible - 2); k++)
					{
						int idx = ordre[(k + offsetInter) % 8];
						couronneInter += "[" + dirs8[idx] + "j+BbL]";
					}
					next += "T" + couronne + "T" + couronneInter + "A";
				}
				else if (c == 'B')
				{
					float r = RandFromSeed(seed, 8200 + i * 19 + iterB * 7);
					next += r < 0.40f
						? "FFj[+b][<b][>b]L"
						: (r < 0.75f ? "Fj[+b][-b][>b]L" : "FFj[+b][-b][<b][>b]L");
					iterB++;
				}
				else if (c == 'b')
				{
					float r = RandFromSeed(seed, 8600 + i * 23 + iterb * 11);
					next += r < 0.36f
						? "Fj[+L][<L][>L]L"
						: (r < 0.72f ? "FFj[->L][>L]L" : "Fj[+L][-L][>L]L");
					iterb++;
				}
				else next += c;
			}
			axiome = next;
		}
		return axiome;
	}

	/// <summary>L-system asymétrique : branches non obligatoires de chaque côté. Si branche à gauche, pas forcément à droite.</summary>
	public static string GenererChaineAsymetrique(string axiome, string regleB, int iterations, uint seed)
	{
		string actuel = axiome;
		int iterGlobal = 0;
		for (int i = 0; i < iterations; i++)
		{
			string suivant = "";
			foreach (char c in actuel)
			{
				if (c == 'T')
				{
					string branches = "";
					string[] dirs = { "[+B]", "[>B]", "[-B]", "[<B]" };
					for (int d = 0; d < 4; d++)
						if (RandFromSeed(seed, iterGlobal * 4 + d) < 0.72f) branches += dirs[d];
					if (branches.Length == 0 && RandFromSeed(seed, iterGlobal + 200) > 0.2f)
						branches = dirs[(int)(RandFromSeed(seed, iterGlobal) * 4) % 4];
					// Tronc plus haut avant premières branches (iterGlobal 0-1 = 3-2 segments purs)
					int troncAvantBranches = iterGlobal < 2 ? (3 - iterGlobal) : 1;
					suivant += new string('F', troncAvantBranches) + branches + "T";
					iterGlobal++;
				}
				else if (c == 'B')
					suivant += regleB;
				else
					suivant += c;
			}
			actuel = suivant;
		}
		return actuel;
	}

	private static void TracerLigneVoxel(Vector3 start, Vector3 end, HashSet<Vector3I> bois)
	{
		float dist = start.DistanceTo(end);
		int steps = Mathf.Max(1, Mathf.CeilToInt(dist * 2.0f));
		for (int i = 0; i <= steps; i++)
		{
			Vector3 p = start.Lerp(end, (float)i / steps);
			bois.Add(new Vector3I(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y), Mathf.RoundToInt(p.Z)));
		}
	}

	/// <summary>Chaîne L-System Chêne réaliste : tronc vertical + branches en spirale + feuilles aux extrémités.</summary>
	/// <remarks>T= tronc, B= branche, L= feuille. Angles dérivés de modèles botaniques (chêne pédonculé).</remarks>
	private static string GenererAdnChene(int iterations)
	{
		// Règles : tronc monte + branches latérales en spirale ; chaque branche peut se ramifier
		var regles = new Dictionary<char, string>
		{
			{ 'T', "F[+B][-B][>B][<B]T" },   // Tronc avec 4 branches en croix
			{ 'B', "FF[^L][&L][^L]F[+b][-b]" }, // Branche + feuilles puis sous-branches
			{ 'b', "F[+L][-L]" }               // Ramification secondaire
		};
		return GenererChaine("T", regles, iterations);
	}

	/// <summary>Interprète la chaîne L-System : tronc central NU, branches pleines de feuilles (différenciation géométrique stricte).</summary>
	/// <param name="racine">Position mondiale de la racine.</param>
	/// <param name="stadeCroissance">0 à STADE_MAX — stade+1 pour itérations.</param>
	/// <param name="seed">Graine pour variabilité.</param>
	public static void InterpreterArbre(Vector3I racine, int stadeCroissance, uint seed, out HashSet<Vector3I> bois, out HashSet<Vector3I> feuilles)
	{
		bois = new HashSet<Vector3I>();
		feuilles = new HashSet<Vector3I>();

		// ADN du chêne voxel : tronc 5 blocs purs (nu), apex avec 4 branches pleines de feuilles
		string axiome = "TTTTTA";
		var regles = new Dictionary<char, string>
		{
			{ 'A', "[+B][>B][-B][<B]TTA" },
			{ 'B', "FF[+L][>L][-L][<L]L" }
		};
		int iter = Mathf.Clamp(stadeCroissance + 1, 1, 4);
		string adnFinal = GenererChaine(axiome, regles, iter);

		var pile = new Stack<TortueEtat>();
		var etat = new TortueEtat { Position = new Vector3(racine.X, racine.Y, racine.Z), Direction = Vector3.Up };
		float angle = Mathf.DegToRad(45f);

		bois.Add(racine);

		foreach (char commande in adnFinal)
		{
			switch (commande)
			{
				case 'T':
					// TRONC ABSOLU : montée verticale pure, pas de feuilles
					etat.Position += Vector3.Up;
					bois.Add(new Vector3I(Mathf.RoundToInt(etat.Position.X), Mathf.RoundToInt(etat.Position.Y), Mathf.RoundToInt(etat.Position.Z)));
					break;
				case 'F':
					// BRANCHE : avance dans la direction angulaire
					etat.Position += etat.Direction;
					bois.Add(new Vector3I(Mathf.RoundToInt(etat.Position.X), Mathf.RoundToInt(etat.Position.Y), Mathf.RoundToInt(etat.Position.Z)));
					break;
				case '[':
					pile.Push(etat);
					break;
				case ']':
					etat = pile.Pop();
					break;
				case '+': etat.Direction = etat.Direction.Rotated(Vector3.Right, angle); break;
				case '-': etat.Direction = etat.Direction.Rotated(Vector3.Right, -angle); break;
				case '>': etat.Direction = etat.Direction.Rotated(Vector3.Forward, angle); break;
				case '<': etat.Direction = etat.Direction.Rotated(Vector3.Forward, -angle); break;
				case 'A':
				case 'B':
					break;
				case 'L':
					// CIME : cluster de feuillage massif (SDF sphère losange), pas de bois
					Vector3 centreFeuille = etat.Position + etat.Direction;
					int cx = Mathf.RoundToInt(centreFeuille.X);
					int cy = Mathf.RoundToInt(centreFeuille.Y);
					int cz = Mathf.RoundToInt(centreFeuille.Z);
					for (int dx = -1; dx <= 1; dx++)
						for (int dy = -1; dy <= 1; dy++)
							for (int dz = -1; dz <= 1; dz++)
								if (Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz) <= 2)
								{
									var vf = new Vector3I(cx + dx, cy + dy, cz + dz);
									if (!bois.Contains(vf)) feuilles.Add(vf);
								}
					break;
			}
		}
	}

	/// <summary>Génère un Chêne mature (stade 3) à la position racine — compatibilité.</summary>
	public static void GenererChene(Vector3I racine, out HashSet<Vector3I> bois, out HashSet<Vector3I> feuilles)
	{
		InterpreterArbre(racine, STADE_MAX, (uint)((racine.X * 73856093) ^ (racine.Z * 19349663)), out bois, out feuilles);
	}
}
