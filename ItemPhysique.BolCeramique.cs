using Godot;
using System;

public partial class ItemPhysique
{
	public const string PrefixGenomeBolCeramique = "BOLCERAM:";

	/// <summary>Lit l'état thermique d'un bol céramique (inventaire, four, etc.).</summary>
	public static bool EssayerLireEtatBolCeramiqueSlot(SlotInventaire s, out int indexChimique, out double refroidissementSec)
	{
		indexChimique = s.IndexChimique;
		refroidissementSec = 0d;
		if (s.EstVide || s.ID != Joueur.IdObjetBolCeramique)
			return false;

		string g = s.GenomeAssemblage ?? "";
		if (g.StartsWith(PrefixGenomeBolCeramique, StringComparison.Ordinal))
		{
			string[] m = g.Substring(PrefixGenomeBolCeramique.Length).Split(':');
			if (m.Length >= 1 && int.TryParse(m[0], out int chi))
				indexChimique = chi;
			if (m.Length >= 2 && long.TryParse(m[1], out long progMs))
				refroidissementSec = Math.Max(0d, progMs / 1000.0);
		}
		else if (indexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			refroidissementSec = 0d;

		return true;
	}

	public static void EcrireEtatBolCeramiqueSlot(ref SlotInventaire s, int indexChimique, double refroidissementSec)
	{
		if (s.EstVide || s.ID != Joueur.IdObjetBolCeramique)
			return;
		s.IndexChimique = indexChimique;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, refroidissementSec * 1000.0));
		s.GenomeAssemblage = $"{PrefixGenomeBolCeramique}{indexChimique}:{progMs}";
	}

	public static float ObtenirFacteurChaleurBolCeramiqueDepuisSlot(SlotInventaire s)
	{
		if (!EssayerLireEtatBolCeramiqueSlot(s, out int chi, out double refroidSec))
			return 0f;
		if (chi != FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique)
			return 0f;
		float prog = (float)(refroidSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	public static SlotInventaire CreerSlotBolCeramiqueChaud()
	{
		var slot = new SlotInventaire
		{
			ID = Joueur.IdObjetBolCeramique,
			Quantite = 1,
			IndexChimique = FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
			IndexMorphologique = 0,
			IndexTaille = 0,
			EstUnEclat = false
		};
		EcrireEtatBolCeramiqueSlot(ref slot, FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique, 0d);
		return slot;
	}
	private double _bolCeramiqueRefroidissementSec;
	private double _bolCeramiqueDernierSyncSec = -1d;

	private bool EstBolCeramiqueChaudPose() =>
		ID_Objet == Joueur.IdObjetBolCeramique
		&& IndexChimique == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique;

	/// <summary>0 = froid, 1 = brûlant (céramique chaude ou en refroidissement au soleil).</summary>
	public float ObtenirFacteurBrulureBolCeramique()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramique || !EstBolCeramiqueChaudPose())
			return 0f;
		float prog = (float)(_bolCeramiqueRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		return FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(prog);
	}

	/// <summary>Zone de contact brûlante au sol (pieds du joueur).</summary>
	public bool EssayerObtenirZoneContactChaleurBolMonde(out Vector3 centreMonde, out float rayonMetres)
	{
		centreMonde = Vector3.Zero;
		rayonMetres = 0f;
		float facteur = ObtenirFacteurBrulureBolCeramique();
		if (!FourTorchieThermodynamique.EstFacteurBolAssezChaudPourBruler(facteur))
			return false;
		centreMonde = GlobalPosition + new Vector3(0f, 0.035f, 0f);
		rayonMetres = Mathf.Lerp(0.14f, 0.24f, facteur);
		return true;
	}

	private void InitialiserBolCeramiquePose()
	{
		ChargerEtatBolCeramiqueDepuisGenome();
		MettreAJourVisuelBolCeramiquePose();
	}

	private void TraiterRefroidissementBolCeramiqueAuSoleil(double delta)
	{
		if (!EstBolCeramiqueChaudPose())
			return;

		Cycle_Solaire soleil = GetTree()?.CurrentScene?.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");
		if (soleil == null || !soleil.EstJourEnsoleille())
			return;

		_bolCeramiqueRefroidissementSec += delta;
		MettreAJourVisuelBolCeramiquePose();
		if (_bolCeramiqueRefroidissementSec < FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
		{
			SynchroniserGenomeBolCeramiquePeriodique(delta);
			return;
		}

		IndexChimique = 0;
		_bolCeramiqueRefroidissementSec = 0d;
		MettreAJourVisuelBolCeramiquePose();
		SynchroniserGenomeBolCeramique();
		GD.Print("SEROKA : Bol en céramique refroidi au soleil.");
	}

	private void SynchroniserGenomeBolCeramiquePeriodique(double delta)
	{
		_bolCeramiqueDernierSyncSec -= delta;
		if (_bolCeramiqueDernierSyncSec <= 0d)
		{
			_bolCeramiqueDernierSyncSec = 0.45d;
			SynchroniserGenomeBolCeramique();
		}
	}

	public void SynchroniserGenomeBolCeramique()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramique)
			return;
		long progMs = (long)Mathf.Round((float)Math.Max(0d, _bolCeramiqueRefroidissementSec * 1000.0));
		GenomeAssemblage = $"{PrefixGenomeBolCeramique}{IndexChimique}:{progMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
	}

	private void ChargerEtatBolCeramiqueDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramique)
			return;
		var slot = new SlotInventaire
		{
			ID = ID_Objet,
			IndexChimique = IndexChimique,
			GenomeAssemblage = GenomeAssemblage ?? ""
		};
		if (EssayerLireEtatBolCeramiqueSlot(slot, out int chi, out double refroidSec))
		{
			IndexChimique = chi;
			_bolCeramiqueRefroidissementSec = refroidSec;
		}
	}

	private void MettreAJourVisuelBolCeramiquePose()
	{
		if (ID_Objet != Joueur.IdObjetBolCeramique)
			return;
		var meshRoot = GetNodeOrNull<Node3D>("MeshInstance3D");
		if (meshRoot == null)
			return;
		float progRefroid = (float)(_bolCeramiqueRefroidissementSec / FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec);
		float facteurChaleur = EstBolCeramiqueChaudPose()
			? FourTorchieThermodynamique.ObtenirFacteurChaleurBolCeramique(progRefroid)
			: 0f;
		var slot = new SlotInventaire
		{
			ID = Joueur.IdObjetBolCeramique,
			IndexChimique = IndexChimique
		};
		Joueur.InstancierModeleBolCeramique(meshRoot, slot, 0.42f, false, facteurChaleur);
	}
}
