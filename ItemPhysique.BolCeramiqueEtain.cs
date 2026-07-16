using Godot;
using System;

public partial class ItemPhysique
{
	public const string PrefixGenomeBolEtainFondu = "BOLETAINFONDU:";
	public const string PrefixGenomeBolScorie = "BOLSCORIE:";

	public static bool EssayerLireEtatBolEtainFonduSlot(SlotInventaire s, out int indexChimique, out double refroidissementSec)
	{
		indexChimique = s.IndexChimique;
		refroidissementSec = 0d;
		if (s.EstVide || s.ID != Joueur.IdObjetBolEtainFonduChaud)
			return false;

		string g = s.GenomeAssemblage ?? "";
		if (g.StartsWith(PrefixGenomeBolEtainFondu, StringComparison.Ordinal))
		{
			string[] m = g.Substring(PrefixGenomeBolEtainFondu.Length).Split(':');
			if (m.Length >= 1 && int.TryParse(m[0], out int chi))
				indexChimique = chi;
			if (m.Length >= 2 && long.TryParse(m[1], out long progMs))
				refroidissementSec = Math.Max(0d, progMs / 1000.0);
		}
		else if (indexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			refroidissementSec = 0d;

		return true;
	}

	public static void EcrireEtatBolEtainFonduSlot(ref SlotInventaire s, int indexChimique, double refroidissementSec)
	{
		if (s.EstVide || s.ID != Joueur.IdObjetBolEtainFonduChaud)
			return;
		s.IndexChimique = indexChimique;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, refroidissementSec * 1000.0));
		s.GenomeAssemblage = $"{PrefixGenomeBolEtainFondu}{indexChimique}:{progMs}";
	}

	public static float ObtenirFacteurChaleurBolEtainFonduDepuisSlot(SlotInventaire s)
	{
		if (!EssayerLireEtatBolEtainFonduSlot(s, out int chi, out double refroidSec))
			return 0f;
		if (chi != FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			return 0f;
		float prog = (float)(refroidSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	public static SlotInventaire CreerSlotBolEtainFonduChaud()
	{
		var slot = new SlotInventaire
		{
			ID = Joueur.IdObjetBolEtainFonduChaud,
			Quantite = 1,
			IndexChimique = FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
			IndexMorphologique = 0,
			IndexTaille = 0,
			EstUnEclat = false
		};
		EcrireEtatBolEtainFonduSlot(ref slot, FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique, 0d);
		return slot;
	}

	/// <summary>Bol fondu entièrement refroidi → bol étain solidifié (ID 169).</summary>
	public static bool EssayerFinaliserBolEtainFonduRefroidi(ref SlotInventaire s)
	{
		if (s.EstVide || s.ID != Joueur.IdObjetBolEtainFonduChaud)
			return false;
		if (FourTorchieThermodynamique.ObtenirFacteurChaleurBolEtainFonduSlot(s) > 0.04f)
			return false;
		s.ID = Joueur.IdObjetBolEtainSolidifie;
		s.IndexChimique = 0;
		s.GenomeAssemblage = "";
		return true;
	}

	public static SlotInventaire CreerSlotBolCeramiqueScorieChaud()
	{
		var slot = new SlotInventaire
		{
			ID = Joueur.IdObjetBolCeramiqueScorie,
			Quantite = 1,
			IndexChimique = FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
			IndexMorphologique = 0,
			IndexTaille = 0,
			EstUnEclat = false
		};
		EcrireEtatBolScorieSlot(ref slot, FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique, 0d);
		return slot;
	}

	public static bool EssayerLireEtatBolScorieSlot(SlotInventaire s, out int indexChimique, out double refroidissementSec)
	{
		indexChimique = s.IndexChimique;
		refroidissementSec = 0d;
		if (s.EstVide || s.ID != Joueur.IdObjetBolCeramiqueScorie)
			return false;

		string g = s.GenomeAssemblage ?? "";
		if (g.StartsWith(PrefixGenomeBolScorie, StringComparison.Ordinal))
		{
			string[] m = g.Substring(PrefixGenomeBolScorie.Length).Split(':');
			if (m.Length >= 1 && int.TryParse(m[0], out int chi))
				indexChimique = chi;
			if (m.Length >= 2 && long.TryParse(m[1], out long progMs))
				refroidissementSec = Math.Max(0d, progMs / 1000.0);
		}
		else if (indexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			refroidissementSec = 0d;

		return true;
	}

	public static void EcrireEtatBolScorieSlot(ref SlotInventaire s, int indexChimique, double refroidissementSec)
	{
		if (s.EstVide || s.ID != Joueur.IdObjetBolCeramiqueScorie)
			return;
		s.IndexChimique = indexChimique;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, refroidissementSec * 1000.0));
		s.GenomeAssemblage = $"{PrefixGenomeBolScorie}{indexChimique}:{progMs}";
	}

	public static float ObtenirFacteurChaleurBolScorieDepuisSlot(SlotInventaire s)
	{
		if (!EssayerLireEtatBolScorieSlot(s, out int chi, out double refroidSec))
			return 0f;
		if (chi != FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			return 0f;
		float prog = (float)(refroidSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	private double _bolEtainFonduRefroidissementSec;
	private double _bolEtainFonduDernierSyncSec = -1d;
	private double _bolScorieRefroidissementSec;
	private double _bolScorieDernierSyncSec = -1d;

	private bool EstBolEtainFonduChaudPose() =>
		ID_Objet == Joueur.IdObjetBolEtainFonduChaud
		&& IndexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique;

	private bool EstBolScorieChaudPose() =>
		ID_Objet == Joueur.IdObjetBolCeramiqueScorie
		&& IndexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique;

	public float ObtenirFacteurBrulureBolEtainFondu()
	{
		if (!EstBolEtainFonduChaudPose())
			return 0f;
		float prog = (float)(_bolEtainFonduRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	public float ObtenirFacteurBrulureBolScorie()
	{
		if (!EstBolScorieChaudPose())
			return 0f;
		float prog = (float)(_bolScorieRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	public bool EssayerObtenirZoneContactChaleurBolEtainMonde(out Vector3 centreMonde, out float rayonMetres)
	{
		centreMonde = Vector3.Zero;
		rayonMetres = 0f;
		float facteur = ObtenirFacteurBrulureBolEtainFondu();
		if (!FourTorchieThermodynamique.EstFacteurBolAssezChaudPourBruler(facteur))
			return false;
		centreMonde = GlobalPosition + new Vector3(0f, 0.035f, 0f);
		rayonMetres = Mathf.Lerp(0.14f, 0.24f, facteur);
		return true;
	}

	public bool EssayerObtenirZoneContactChaleurBolScorieMonde(out Vector3 centreMonde, out float rayonMetres)
	{
		centreMonde = Vector3.Zero;
		rayonMetres = 0f;
		float facteur = ObtenirFacteurBrulureBolScorie();
		if (!FourTorchieThermodynamique.EstFacteurBolAssezChaudPourBruler(facteur))
			return false;
		centreMonde = GlobalPosition + new Vector3(0f, 0.035f, 0f);
		rayonMetres = Mathf.Lerp(0.14f, 0.24f, facteur);
		return true;
	}

	private void InitialiserBolEtainFonduPose()
	{
		ChargerEtatBolEtainFonduDepuisGenome();
		MettreAJourVisuelBolEtainFonduPose();
	}

	private void InitialiserBolScoriePose()
	{
		ChargerEtatBolScorieDepuisGenome();
		MettreAJourVisuelBolScoriePose();
	}

	private void TraiterRefroidissementBolEtainFonduAuSoleil(double delta)
	{
		if (!EstBolEtainFonduChaudPose())
			return;

		Cycle_Solaire soleil = GetTree()?.CurrentScene?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (soleil == null || !soleil.EstJourEnsoleille())
			return;

		_bolEtainFonduRefroidissementSec += delta;
		MettreAJourVisuelBolEtainFonduPose();
		if (_bolEtainFonduRefroidissementSec < FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
		{
			SynchroniserGenomeBolEtainFonduPeriodique(delta);
			return;
		}

		ID_Objet = Joueur.IdObjetBolEtainSolidifie;
		IndexChimique = 0;
		GenomeAssemblage = "";
		_bolEtainFonduRefroidissementSec = 0d;
		MettreAJourVisuelBolEtainSolidifiePose();
		SetMeta(Joueur.MetaGenomeAssemblage, "");
		GD.Print("SEROKA : Bol étain fondu solidifié au soleil.");
	}

	private void TraiterRefroidissementBolScorieAuSoleil(double delta)
	{
		if (!EstBolScorieChaudPose())
			return;

		Cycle_Solaire soleil = GetTree()?.CurrentScene?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (soleil == null || !soleil.EstJourEnsoleille())
			return;

		_bolScorieRefroidissementSec += delta;
		MettreAJourVisuelBolScoriePose();
		if (_bolScorieRefroidissementSec < FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
		{
			SynchroniserGenomeBolScoriePeriodique(delta);
			return;
		}

		IndexChimique = 0;
		_bolScorieRefroidissementSec = 0d;
		MettreAJourVisuelBolScoriePose();
		SynchroniserGenomeBolScorie();
		GD.Print("SEROKA : Bol scorie refroidi au soleil.");
	}

	private void SynchroniserGenomeBolEtainFonduPeriodique(double delta)
	{
		_bolEtainFonduDernierSyncSec -= delta;
		if (_bolEtainFonduDernierSyncSec <= 0d)
		{
			_bolEtainFonduDernierSyncSec = 0.45d;
			SynchroniserGenomeBolEtainFondu();
		}
	}

	private void SynchroniserGenomeBolScoriePeriodique(double delta)
	{
		_bolScorieDernierSyncSec -= delta;
		if (_bolScorieDernierSyncSec <= 0d)
		{
			_bolScorieDernierSyncSec = 0.45d;
			SynchroniserGenomeBolScorie();
		}
	}

	public void SynchroniserGenomeBolEtainFondu()
	{
		if (ID_Objet != Joueur.IdObjetBolEtainFonduChaud)
			return;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, _bolEtainFonduRefroidissementSec * 1000.0));
		GenomeAssemblage = $"{PrefixGenomeBolEtainFondu}{IndexChimique}:{progMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
	}

	public void SynchroniserGenomeBolScorie()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramiqueScorie)
			return;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, _bolScorieRefroidissementSec * 1000.0));
		GenomeAssemblage = $"{PrefixGenomeBolScorie}{IndexChimique}:{progMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
	}

	private void ChargerEtatBolEtainFonduDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetBolEtainFonduChaud)
			return;
		var slot = new SlotInventaire
		{
			ID = ID_Objet,
			IndexChimique = IndexChimique,
			GenomeAssemblage = GenomeAssemblage ?? ""
		};
		if (EssayerLireEtatBolEtainFonduSlot(slot, out int chi, out double refroidSec))
		{
			IndexChimique = chi;
			_bolEtainFonduRefroidissementSec = refroidSec;
		}
	}

	private void ChargerEtatBolScorieDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramiqueScorie)
			return;
		var slot = new SlotInventaire
		{
			ID = ID_Objet,
			IndexChimique = IndexChimique,
			GenomeAssemblage = GenomeAssemblage ?? ""
		};
		if (EssayerLireEtatBolScorieSlot(slot, out int chi, out double refroidSec))
		{
			IndexChimique = chi;
			_bolScorieRefroidissementSec = refroidSec;
		}
	}

	private void MettreAJourVisuelBolEtainFonduPose()
	{
		if (ID_Objet != Joueur.IdObjetBolEtainFonduChaud)
			return;
		var meshRoot = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (meshRoot == null)
			return;
		float progRefroid = (float)(_bolEtainFonduRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		float facteurChaleur = EstBolEtainFonduChaudPose()
			? FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(progRefroid)
			: 0f;
		var slot = new SlotInventaire { ID = Joueur.IdObjetBolEtainFonduChaud, IndexChimique = IndexChimique };
		Joueur.InstancierModeleBolEtainFonduChaud(meshRoot, slot, 0.42f, true, facteurChaleur);
	}

	private void MettreAJourVisuelBolEtainSolidifiePose()
	{
		if (ID_Objet != Joueur.IdObjetBolEtainSolidifie)
			return;
		var meshRoot = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (meshRoot == null)
			return;
		var slot = new SlotInventaire { ID = Joueur.IdObjetBolEtainSolidifie };
		Joueur.InstancierModeleBolEtainSolidifie(meshRoot, slot, 0.42f, true);
	}

	private void MettreAJourVisuelBolScoriePose()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramiqueScorie)
			return;
		var meshRoot = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (meshRoot == null)
			return;
		float progRefroid = (float)(_bolScorieRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		float facteurChaleur = EstBolScorieChaudPose()
			? FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(progRefroid)
			: 0f;
		var slot = new SlotInventaire { ID = Joueur.IdObjetBolCeramiqueScorie, IndexChimique = IndexChimique };
		Joueur.InstancierModeleBolCeramiqueScorie(meshRoot, slot, 0.42f, true, facteurChaleur);
	}
}
