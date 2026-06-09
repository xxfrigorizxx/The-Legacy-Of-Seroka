using Godot;
using System;

public partial class ItemPhysique
{
	public const string PrefixGenomeMouleCeramique = "MOULCERAM:";

	public static bool EssayerLireEtatMouleCeramiqueSlot(SlotInventaire s, out int indexChimique, out double refroidissementSec)
	{
		indexChimique = s.IndexChimique;
		refroidissementSec = 0d;
		if (s.EstVide || s.ID != Joueur.IdObjetMouleCeramique)
			return false;

		string g = s.GenomeAssemblage ?? "";
		if (g.StartsWith(PrefixGenomeMouleCeramique, StringComparison.Ordinal))
		{
			string[] m = g.Substring(PrefixGenomeMouleCeramique.Length).Split(':');
			if (m.Length >= 1 && int.TryParse(m[0], out int chi))
				indexChimique = chi;
			if (m.Length >= 2 && long.TryParse(m[1], out long progMs))
				refroidissementSec = Math.Max(0d, progMs / 1000.0);
		}
		else if (indexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			refroidissementSec = 0d;

		return true;
	}

	public static void EcrireEtatMouleCeramiqueSlot(ref SlotInventaire s, int indexChimique, double refroidissementSec)
	{
		if (s.EstVide || s.ID != Joueur.IdObjetMouleCeramique)
			return;
		s.IndexChimique = indexChimique;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, refroidissementSec * 1000.0));
		s.GenomeAssemblage = $"{PrefixGenomeMouleCeramique}{indexChimique}:{progMs}";
	}

	public static float ObtenirFacteurChaleurMouleCeramiqueDepuisSlot(SlotInventaire s)
	{
		if (!EssayerLireEtatMouleCeramiqueSlot(s, out int chi, out double refroidSec))
			return 0f;
		if (chi != FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			return 0f;
		float prog = (float)(refroidSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	public static SlotInventaire CreerSlotMouleCeramiqueChaud()
	{
		var slot = new SlotInventaire
		{
			ID = Joueur.IdObjetMouleCeramique,
			Quantite = 1,
			IndexChimique = FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
			IndexMorphologique = 0,
			IndexTaille = 0,
			EstUnEclat = false
		};
		EcrireEtatMouleCeramiqueSlot(ref slot, FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique, 0d);
		return slot;
	}

	private double _mouleCeramiqueRefroidissementSec;
	private double _mouleCeramiqueDernierSyncSec = -1d;

	private bool EstMouleCeramiqueChaudPose() =>
		ID_Objet == Joueur.IdObjetMouleCeramique
		&& IndexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique;

	public float ObtenirFacteurBrulureMouleCeramique()
	{
		if (ID_Objet != Joueur.IdObjetMouleCeramique || !EstMouleCeramiqueChaudPose())
			return 0f;
		float prog = (float)(_mouleCeramiqueRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	public bool EssayerObtenirZoneContactChaleurMouleMonde(out Vector3 centreMonde, out float rayonMetres)
	{
		centreMonde = Vector3.Zero;
		rayonMetres = 0f;
		float facteur = ObtenirFacteurBrulureMouleCeramique();
		if (!FourTorchieThermodynamique.EstFacteurBolAssezChaudPourBruler(facteur))
			return false;
		centreMonde = GlobalPosition + new Vector3(0f, 0.04f, 0f);
		rayonMetres = Mathf.Lerp(0.16f, 0.26f, facteur);
		return true;
	}

	private void InitialiserMouleCeramiquePose()
	{
		ChargerEtatMouleCeramiqueDepuisGenome();
		MettreAJourVisuelMouleCeramiquePose();
	}

	private void TraiterRefroidissementMouleCeramiqueAuSoleil(double delta)
	{
		if (!EstMouleCeramiqueChaudPose())
			return;

		Cycle_Solaire soleil = GetTree()?.CurrentScene?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (soleil == null || !soleil.EstJourEnsoleille())
			return;

		_mouleCeramiqueRefroidissementSec += delta;
		MettreAJourVisuelMouleCeramiquePose();
		if (_mouleCeramiqueRefroidissementSec < FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
		{
			SynchroniserGenomeMouleCeramiquePeriodique(delta);
			return;
		}

		IndexChimique = 0;
		_mouleCeramiqueRefroidissementSec = 0d;
		MettreAJourVisuelMouleCeramiquePose();
		SynchroniserGenomeMouleCeramique();
		GD.Print("SEROKA : Moule en céramique refroidi au soleil.");
	}

	private void SynchroniserGenomeMouleCeramiquePeriodique(double delta)
	{
		_mouleCeramiqueDernierSyncSec -= delta;
		if (_mouleCeramiqueDernierSyncSec <= 0d)
		{
			_mouleCeramiqueDernierSyncSec = 0.45d;
			SynchroniserGenomeMouleCeramique();
		}
	}

	public void SynchroniserGenomeMouleCeramique()
	{
		if (ID_Objet != Joueur.IdObjetMouleCeramique)
			return;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, _mouleCeramiqueRefroidissementSec * 1000.0));
		GenomeAssemblage = $"{PrefixGenomeMouleCeramique}{IndexChimique}:{progMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
	}

	private void ChargerEtatMouleCeramiqueDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetMouleCeramique)
			return;
		var slot = new SlotInventaire
		{
			ID = ID_Objet,
			IndexChimique = IndexChimique,
			GenomeAssemblage = GenomeAssemblage ?? ""
		};
		if (EssayerLireEtatMouleCeramiqueSlot(slot, out int chi, out double refroidSec))
		{
			IndexChimique = chi;
			_mouleCeramiqueRefroidissementSec = refroidSec;
		}
	}

	private void MettreAJourVisuelMouleCeramiquePose()
	{
		if (ID_Objet != Joueur.IdObjetMouleCeramique)
			return;
		var meshRoot = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (meshRoot == null)
			return;
		float progRefroid = (float)(_mouleCeramiqueRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		float facteurChaleur = EstMouleCeramiqueChaudPose()
			? FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(progRefroid)
			: 0f;
		var slot = new SlotInventaire
		{
			ID = Joueur.IdObjetMouleCeramique,
			IndexChimique = IndexChimique
		};
		Joueur.InstancierModeleMouleCeramique(meshRoot, slot, 0.42f, false, facteurChaleur);
	}
}
