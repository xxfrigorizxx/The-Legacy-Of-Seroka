using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Persistance des PNJ humains : sauvegarde/chargement avec le monde (position, sexe, vitaux, inventaire) +
/// « recap » du temps écoulé hors-jeu (la faim baisse selon les heures passées depuis la dernière sauvegarde).
/// v5 : PNJ virtuels hors-chunk, cible migration, intelligence/XP.
/// </summary>
public static class PnjHumainPersistance
{
	private const int Version = 7;
	private const string NomFichier = "pnj_humains.dat";

	private static string CheminFichier()
	{
		string nomMonde = GameState.Instance?.NomMondeActuel;
		if (string.IsNullOrWhiteSpace(nomMonde))
			return null;
		string dossier = ProjectSettings.GlobalizePath($"user://saves/{nomMonde}");
		Directory.CreateDirectory(dossier);
		return Path.Combine(dossier, NomFichier);
	}

	public static void Sauvegarder(Gestionnaire_Monde gm = null, IReadOnlyList<PnjHumainEtatVirtuel> virtuels = null)
	{
		string chemin = CheminFichier();
		if (chemin == null)
			return;
		virtuels ??= PnjHumainContinuiteService.Virtuels;
		try
		{
			var vivants = new List<PnjHumain>();
			foreach (PnjHumain p in PnjHumain.Tous)
				if (p != null && GodotObject.IsInstanceValid(p))
					vivants.Add(p);

			using var w = new BinaryWriter(File.Open(chemin, FileMode.Create));
			w.Write(Version);
			w.Write(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			w.Write(vivants.Count + virtuels.Count);
			foreach (PnjHumain p in vivants)
				EcrireEntreePhysique(w, p);
			foreach (PnjHumainEtatVirtuel v in virtuels)
				EcrireEntreeVirtuelle(w, v);
			EcrireSectionCamps(w);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K PNJ : erreur sauvegarde : {ex.Message}");
		}
	}

	private static void EcrireEntreePhysique(BinaryWriter w, PnjHumain p)
	{
		w.Write(false); // isVirtual
		w.Write((int)p.SexePnj);
		Vector3 pos = p.GlobalPosition;
		w.Write(pos.X); w.Write(pos.Y); w.Write(pos.Z);
		w.Write(p.FaimCourante);
		w.Write(p.StaminaCourante);
		w.Write(p.NombreMembres);
		for (int i = 0; i < p.NombreMembres; i++)
			w.Write(p.PvMembre(i));
		SlotInventaire[] inv = p.Inventaire ?? Array.Empty<SlotInventaire>();
		w.Write(inv.Length);
		for (int i = 0; i < inv.Length; i++)
		{
			w.Write(inv[i].ID);
			w.Write(inv[i].Quantite);
			w.Write(inv[i].IndexChimique);
		}
		var carnet = p.Carnet;
		w.Write(carnet.Count);
		for (int i = 0; i < carnet.Count; i++)
			w.Write(carnet[i] ?? "");
		w.Write(p.NomPnj ?? "");
		w.Write(p.EstRebelle);
		w.Write(p.ActesBons);
		w.Write(p.ActesMauvais);
		w.Write(p.NomSocieteOuVide ?? "");
		Vector2 cible = p.CibleMigrationAbsolueXZ;
		w.Write(cible.X);
		w.Write(cible.Y);
		w.Write(p.EnMigrationVersBiome);
		w.Write(p.EstEnPauseCamp);
		Vector2 camp = p.EstEnPauseCamp ? p.AncreCampXZ : Vector2.Zero;
		w.Write(camp.X);
		w.Write(camp.Y);
		w.Write(p.Intelligence);
		w.Write(p.XpAnalyse);
	}

	private static void EcrireEntreeVirtuelle(BinaryWriter w, PnjHumainEtatVirtuel v)
	{
		w.Write(true); // isVirtual
		w.Write((int)v.Sexe);
		w.Write(v.PosX); w.Write(v.PosY); w.Write(v.PosZ);
		w.Write(v.Faim);
		w.Write(v.Stamina);
		int nbMembres = v.PvMembres?.Length ?? 0;
		w.Write(nbMembres);
		for (int i = 0; i < nbMembres; i++)
			w.Write(v.PvMembres[i]);
		SlotInventaire[] inv = v.Inventaire ?? Array.Empty<SlotInventaire>();
		w.Write(inv.Length);
		for (int i = 0; i < inv.Length; i++)
		{
			w.Write(inv[i].ID);
			w.Write(inv[i].Quantite);
			w.Write(inv[i].IndexChimique);
		}
		w.Write(v.Carnet.Count);
		for (int i = 0; i < v.Carnet.Count; i++)
			w.Write(v.Carnet[i] ?? "");
		w.Write(v.Nom ?? "");
		w.Write(v.Rebelle);
		w.Write(v.ActesBons);
		w.Write(v.ActesMauvais);
		w.Write(v.SocieteNom ?? "");
		w.Write(v.CibleMigrX);
		w.Write(v.CibleMigrZ);
		w.Write(v.ACibleMigration);
		w.Write(v.EnPauseCamp);
		w.Write(v.CampX);
		w.Write(v.CampZ);
		w.Write(v.Intelligence);
		w.Write(v.XpAnalyse);
	}

	private static void EcrireSectionCamps(BinaryWriter w)
	{
		var camps = CollecterCampsPourSauvegarde();
		w.Write(camps.Count);
		foreach (CampPnjDonneesSauvegarde c in camps)
			c.Ecrire(w);
	}

	private static List<CampPnjDonneesSauvegarde> CollecterCampsPourSauvegarde()
	{
		var camps = new List<CampPnjDonneesSauvegarde>();
		var deja = new HashSet<(int x, int z)>();
		foreach (SocietePnj soc in SocietePnj.ToutesPourSauvegarde())
		{
			CampPnjStructure camp = soc.StructureCamp;
			if (camp == null || !camp.EstInitialise)
				continue;
			Vector2 ancre = camp.Ancre;
			int kx = Mathf.RoundToInt(ancre.X), kz = Mathf.RoundToInt(ancre.Y);
			if (!deja.Add((kx, kz)))
				continue;
			camps.Add(CampPnjDonneesSauvegarde.DepuisStructure(camp, soc.Nom, rebelle: false, soc));
		}
		foreach (PnjHumain p in PnjHumain.Tous)
		{
			if (p == null || !GodotObject.IsInstanceValid(p))
				continue;
			CampPnjStructure camp = p.ObtenirCampPersoStructure();
			if (camp == null || !camp.EstInitialise)
				continue;
			Vector2 ancre = camp.Ancre;
			int kx = Mathf.RoundToInt(ancre.X), kz = Mathf.RoundToInt(ancre.Y);
			if (!deja.Add((kx, kz)))
				continue;
			camps.Add(CampPnjDonneesSauvegarde.DepuisStructure(camp, p.NomPnj, rebelle: true, null));
		}
		return camps;
	}

	private sealed class CampPnjDonneesSauvegarde
	{
		public float AncreX, AncreZ;
		public string SocieteNom = "";
		public bool Rebelle;
		public int ObjectifReserve;
		public int BaiesDeposeesReserve;
		public readonly List<(int couleur, int qty)> StockBon = new();
		public readonly List<(int couleur, int qty)> StockMauvais = new();
		public int VisuellesBon;
		public int VisuellesMauvais;

		public static CampPnjDonneesSauvegarde DepuisStructure(CampPnjStructure camp, string nom, bool rebelle, SocietePnj soc)
		{
			var d = new CampPnjDonneesSauvegarde
			{
				AncreX = camp.Ancre.X,
				AncreZ = camp.Ancre.Y,
				SocieteNom = nom ?? "",
				Rebelle = rebelle,
				ObjectifReserve = soc?.ObjectifReserveBaies ?? camp.ObjectifReserveBaies,
				BaiesDeposeesReserve = soc?.BaiesDeposeesReserve ?? camp.BaiesDeposeesReserve
			};
			camp.CopierStocksVers(d.StockBon, d.StockMauvais, out d.VisuellesBon, out d.VisuellesMauvais);
			return d;
		}

		public void Ecrire(BinaryWriter w)
		{
			w.Write(AncreX);
			w.Write(AncreZ);
			w.Write(SocieteNom ?? "");
			w.Write(Rebelle);
			w.Write(ObjectifReserve);
			w.Write(BaiesDeposeesReserve);
			w.Write(StockBon.Count);
			foreach ((int couleur, int qty) in StockBon)
			{
				w.Write(couleur);
				w.Write(qty);
			}
			w.Write(StockMauvais.Count);
			foreach ((int couleur, int qty) in StockMauvais)
			{
				w.Write(couleur);
				w.Write(qty);
			}
			w.Write(VisuellesBon);
			w.Write(VisuellesMauvais);
		}

		public static CampPnjDonneesSauvegarde Lire(BinaryReader r)
		{
			var d = new CampPnjDonneesSauvegarde
			{
				AncreX = r.ReadSingle(),
				AncreZ = r.ReadSingle(),
				SocieteNom = r.ReadString(),
				Rebelle = r.ReadBoolean(),
				ObjectifReserve = r.ReadInt32(),
				BaiesDeposeesReserve = r.ReadInt32()
			};
			int nBon = r.ReadInt32();
			for (int i = 0; i < nBon; i++)
				d.StockBon.Add((r.ReadInt32(), r.ReadInt32()));
			int nMauvais = r.ReadInt32();
			for (int i = 0; i < nMauvais; i++)
				d.StockMauvais.Add((r.ReadInt32(), r.ReadInt32()));
			d.VisuellesBon = r.ReadInt32();
			d.VisuellesMauvais = r.ReadInt32();
			return d;
		}
	}

	public static void Charger(Node parent)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return;
		string chemin = CheminFichier();
		if (chemin == null || !File.Exists(chemin))
			return;
		try
		{
			bool aEtatVivant = false;
			foreach (PnjHumain p in PnjHumain.Tous)
			{
				if (p != null && GodotObject.IsInstanceValid(p))
				{
					aEtatVivant = true;
					break;
				}
			}
			if (aEtatVivant || PnjHumainContinuiteService.Virtuels.Count > 0)
				Sauvegarder();
			PnjHumainContinuiteService.Vider();
			foreach (PnjHumain p in new List<PnjHumain>(PnjHumain.Tous))
				if (p != null && GodotObject.IsInstanceValid(p))
					p.QueueFree();

			using var r = new BinaryReader(File.Open(chemin, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
			int version = r.ReadInt32();
			if (version < 1 || version > Version)
				return;
			long tsSauvegarde = r.ReadInt64();
			long maintenant = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			float secondesEcoulees = Mathf.Max(0f, maintenant - tsSauvegarde);
			float heuresEcoulees = secondesEcoulees / 3600f;
			float drainFaimRecap = PnjHumain.DrainFaimOfflineParHeure * heuresEcoulees;
			int seed = GameState.Instance?.SeedTerrainActuel ?? 19847;

			int count = r.ReadInt32();
			var entrees = new List<(bool virtuel, PnjHumainEtatVirtuel etat)>(count);
			for (int k = 0; k < count; k++)
			{
				if (EssayerLireEntree(r, version, drainFaimRecap, out bool isVirtual, out PnjHumainEtatVirtuel etat))
					entrees.Add((isVirtual, etat));
			}

			var campsSauvegardes = new List<CampPnjDonneesSauvegarde>();
			if (version >= 7 && r.BaseStream.Position < r.BaseStream.Length)
			{
				int nbCamps = r.ReadInt32();
				for (int i = 0; i < nbCamps; i++)
					campsSauvegardes.Add(CampPnjDonneesSauvegarde.Lire(r));
			}

			if (secondesEcoulees > 1f && entrees.Count > 0)
			{
				var etats = new List<PnjHumainEtatVirtuel>();
				foreach ((bool _, PnjHumainEtatVirtuel etat) in entrees)
					etats.Add(etat);
				PnjHumainContinuiteService.SimulerRecapOffline(etats, secondesEcoulees, seed);
			}

			int physiques = 0, virtuels = 0;
			foreach ((bool isVirtual, PnjHumainEtatVirtuel etat) in entrees)
			{
				if (etat.Faim <= 0f)
					continue;
				if (isVirtual)
				{
					PnjHumainContinuiteService.AjouterVirtuel(etat);
					virtuels++;
					continue;
				}
				var pnj = new PnjHumain();
				pnj.Configurer(etat.Sexe);
				parent.AddChild(pnj);
				pnj.RestaurerDepuisVirtuel(etat, seed);
				physiques++;
			}

			AppliquerCampsSauvegardes(campsSauvegardes, seed, parent);

			GD.Print($"ZERO-K PNJ : {physiques} physique(s), {virtuels} virtuel(s) rechargé(s) (~{heuresEcoulees:0.0} h écoulées, recap {secondesEcoulees:0}s).");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"ZERO-K PNJ : erreur chargement : {ex.Message}");
		}
	}

	private static bool EssayerLireEntree(BinaryReader r, int version, float drainFaimRecap, out bool isVirtual, out PnjHumainEtatVirtuel etat)
	{
		isVirtual = false;
		etat = null;
		isVirtual = version >= 5 && r.ReadBoolean();
		var sexe = (SexeJoueur)r.ReadInt32();
		float px = r.ReadSingle(), py = r.ReadSingle(), pz = r.ReadSingle();
		float faim = r.ReadSingle();
		float stamina = r.ReadSingle();
		int nbMembres = r.ReadInt32();
		int[] pvMembres = new int[nbMembres];
		for (int i = 0; i < nbMembres; i++)
			pvMembres[i] = r.ReadInt32();
		int invLen = r.ReadInt32();
		var inv = new SlotInventaire[invLen];
		for (int i = 0; i < invLen; i++)
		{
			inv[i].ID = r.ReadInt32();
			inv[i].Quantite = r.ReadInt32();
			if (version >= 4)
				inv[i].IndexChimique = r.ReadInt32();
		}
		var carnet = new List<string>();
		if (version >= 2)
		{
			int nbCarnet = r.ReadInt32();
			for (int i = 0; i < nbCarnet; i++)
				carnet.Add(r.ReadString());
		}
		string nomPnj = ""; bool rebelle = false; int bons = 0, mauvais = 0; string societeNom = "";
		if (version >= 3)
		{
			nomPnj = r.ReadString();
			rebelle = r.ReadBoolean();
			bons = r.ReadInt32();
			mauvais = r.ReadInt32();
			societeNom = r.ReadString();
		}
		float cibleX = 0f, cibleZ = 0f;
		bool aCible = false;
		bool enCamp = false;
		float campX = 0f, campZ = 0f;
		int intelligence = 10, xpAnalyse = 0;
		if (version >= 5)
		{
			cibleX = r.ReadSingle();
			cibleZ = r.ReadSingle();
			aCible = r.ReadBoolean();
			if (version >= 6)
			{
				enCamp = r.ReadBoolean();
				campX = r.ReadSingle();
				campZ = r.ReadSingle();
			}
			intelligence = r.ReadInt32();
			xpAnalyse = r.ReadInt32();
		}

		faim -= drainFaimRecap;
		if (faim <= 0f)
			faim = 8f;

		etat = new PnjHumainEtatVirtuel
		{
			Sexe = sexe,
			Faim = faim,
			Stamina = stamina,
			Nom = nomPnj,
			Rebelle = rebelle,
			ActesBons = bons,
			ActesMauvais = mauvais,
			SocieteNom = societeNom,
			Intelligence = intelligence,
			XpAnalyse = xpAnalyse,
			PvMembres = pvMembres,
			Inventaire = inv,
			ACibleMigration = aCible
		};
		etat.DefinirPosition(new Vector3(px, py, pz));
		if (aCible)
		{
			etat.CibleMigrX = cibleX;
			etat.CibleMigrZ = cibleZ;
		}
		if (enCamp)
			etat.DefinirCamp(new Vector2(campX, campZ));
		foreach (string l in carnet)
			if (!string.IsNullOrWhiteSpace(l))
				etat.Carnet.Add(l);
		return true;
	}

	private static void AppliquerCampsSauvegardes(List<CampPnjDonneesSauvegarde> camps, int seed, Node parent)
	{
		if (camps == null || camps.Count == 0)
			return;
		Gestionnaire_Monde gm = parent.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde")
			?? parent.FindChild("Gestionnaire_Monde", recursive: true, owned: false) as Gestionnaire_Monde;
		foreach (CampPnjDonneesSauvegarde d in camps)
		{
			Vector2 ancre = new Vector2(d.AncreX, d.AncreZ);
			CampPnjStructure camp = null;
			if (!d.Rebelle && !string.IsNullOrWhiteSpace(d.SocieteNom))
			{
				SocietePnj soc = SocietePnj.TrouverOuCreerParNom(d.SocieteNom);
				if (soc.StructureCamp == null || !soc.StructureCamp.EstInitialise)
					soc.InitialiserStructureCamp(ancre, seed, gm);
				camp = soc.StructureCamp;
				soc.RestaurerObjectifReserveColonie(d.ObjectifReserve, d.BaiesDeposeesReserve);
			}
			else
			{
				foreach (PnjHumain p in PnjHumain.Tous)
				{
					if (p == null || !GodotObject.IsInstanceValid(p))
						continue;
					if (!p.EstEnPauseCamp || p.AncreCampXZ.DistanceTo(ancre) > 1.5f)
						continue;
					if (p.ObtenirCampPersoStructure() == null)
						p.EtablirCampDepuisSauvegarde(ancre);
					camp = p.ObtenirCampPersoStructure();
					break;
				}
			}
			camp?.RestaurerStocksDepuisSauvegarde(
				d.ObjectifReserve,
				d.BaiesDeposeesReserve,
				d.StockBon,
				d.StockMauvais,
				d.VisuellesBon,
				d.VisuellesMauvais);
			camp?.FinaliserRestaurationStock(gm, seed);
		}
	}
}

