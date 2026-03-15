using Godot;
using System.Collections.Generic;

/// <summary>ADN de la matière organique : régit la physique du bois (flottabilité, résistance à la hache).</summary>
public struct ProfilBotanique
{
	public string Nom;
	public byte ID_Tronc;
	public byte ID_Feuille;
	public float MasseDensite;   // Ex: 0.75 (flotte), 1.2 (coule)
	public float ResistanceHache; // Points de vie du bloc
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
	/// <summary>Le Chêne : Dense, majestueux, branches noueuses.</summary>
	public static readonly ProfilBotanique Chene = new ProfilBotanique
	{
		Nom = "Chêne",
		ID_Tronc = 30,
		ID_Feuille = 31,
		MasseDensite = 0.75f,
		ResistanceHache = 150f
	};

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
		string actuel = "TTTTTA";
		string[] dirs8 = { "[+B]", "[+>B]", "[>B]", "[->B]", "[-B]", "[-<B]", "[<B]", "[+<B]" };
		int iterA = 0, iterB = 0, iterb = 0;
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
					suivant += branches + "TTA";
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
					// Sous-branche : 2–3 rameaux feuillus, feuillage le long
					float r = RandFromSeed(seed, iterb * 19 + 200);
					suivant += r < 0.4f ? "FF[+L][-L][>L]L" : (r < 0.7f ? "FL[+L][-L]L" : "FF[+L]L");
					iterb++;
				}
				else
					suivant += c;
			}
			actuel = suivant;
		}
		return actuel;
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
