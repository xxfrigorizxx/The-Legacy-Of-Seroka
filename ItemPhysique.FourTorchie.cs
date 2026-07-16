using Godot;
using System;

public partial class ItemPhysique
{
	public const int FourTorchieNbSlots = 9;
	public const int FourTorchieSlotCombustible = 0;
	public const int FourTorchiePremierSlotCuisson = 1;
	public const int FourTorchiePremierSlotResultat = 5;
	public const int FourTorchieNbCuisson = 4;

	private const string PrefixGenomeFourTorchie = "FOURTORCHIE3:";
	private const string SeparateurSlotsFourTorchie = "#S#";
	private const float FourTorchieEncrassementParUniteResineux = 18f;
	private const double FourTorchieDureeMonteeAnthraciteSec = 20.0;

	private float _fourTorchieTemperature = FourTorchieThermodynamique.TempAmbianteC;
	private bool _fourTorchieAllume;
	private float _fourTorchieHp = FourTorchieThermodynamique.FourHpMax;
	private float _fourTorchieEncrassementMalusC;
	private bool _fourTorchieAnomalieAnthracite;
	private double _fourTorchieAnomalieResteSec;
	private double _fourTorchieResteCombSec;
	private double _fourTorchieDureeUniteCouranteSec;
	private ProfilCombustibleFourTorchie _fourTorchieProfilActif;
	private bool _fourTorchieProfilActifValide;
	private readonly double[] _fourTorchieProgressCuissonSec = new double[FourTorchieNbCuisson];
	private readonly bool[] _fourTorchieEtainMarqueScorie = new bool[FourTorchieNbCuisson];
	private double _fourTorchieDernierSyncFourSec = -1d;

	public SlotInventaire[] GrilleFourTorchie = new SlotInventaire[FourTorchieNbSlots];

	public float ObtenirTemperatureFourTorchie() => Mathf.Max(FourTorchieThermodynamique.TempAmbianteC, _fourTorchieTemperature);

	public float ObtenirHpFourTorchie() => Mathf.Clamp(_fourTorchieHp, 0f, FourTorchieThermodynamique.FourHpMax);

	public bool EstFourTorchieAllume() => ID_Objet == Joueur.IdObjetFourTorchie && _fourTorchieAllume;

	public bool EstFourTorchieActif() =>
		ID_Objet == Joueur.IdObjetFourTorchie
		&& (_fourTorchieAllume && (_fourTorchieResteCombSec > 0.001d || _fourTorchieAnomalieAnthracite)
			|| _fourTorchieTemperature > FourTorchieThermodynamique.TempAmbianteC + 8f);

	/// <summary>Plafond thermique du combustible en cours ou du slot combustible (0 si aucun).</summary>
	public float ObtenirPlafondThermiqueActifFourTorchie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return 0f;
		AssurerGrilleFourTorchie();
		if (_fourTorchieProfilActifValide)
			return Mathf.Max(FourTorchieThermodynamique.TempAmbianteC, _fourTorchieProfilActif.TempMaxC - _fourTorchieEncrassementMalusC);
		SlotInventaire comb = GrilleFourTorchie[FourTorchieSlotCombustible];
		if (!EstSlotCombustibleFourTorchie(comb))
			return 0f;
		return FourTorchieThermodynamique.ObtenirPlafondThermiqueCombustible(comb, _fourTorchieEncrassementMalusC);
	}

	public float ObtenirProgressionCombustionFourTorchie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie || !_fourTorchieAllume || _fourTorchieResteCombSec <= 0.001d)
			return -1f;
		if (_fourTorchieDureeUniteCouranteSec <= 0.001d)
			return 0f;
		return Mathf.Clamp((float)(_fourTorchieResteCombSec / _fourTorchieDureeUniteCouranteSec), 0f, 1f);
	}

	public float ObtenirProgressionCuissonFourTorchie(int indexCuisson)
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie || indexCuisson < 0 || indexCuisson >= FourTorchieNbCuisson)
			return -1f;
		AssurerGrilleFourTorchie();
		SlotInventaire cuisson = GrilleFourTorchie[FourTorchiePremierSlotCuisson + indexCuisson];
		if (!EstSlotCuissonFourTorchie(cuisson))
			return -1f;
		double duree = FourTorchieThermodynamique.ObtenirDureeCuissonFourPourSlot(cuisson);
		if (duree <= 0.001d)
			return -1f;
		if ((cuisson.ID == Joueur.IdObjetBolArgile || cuisson.ID == Joueur.IdObjetMouleArgile)
			&& !FourTorchieThermodynamique.TemperatureDansPlageCuissonBolArgile(_fourTorchieTemperature))
			return -1f;
		if (cuisson.ID == Joueur.IdObjetSteakCru
			&& _fourTorchieTemperature < FourTorchieThermodynamique.SeuilCuissonMinC)
			return -1f;
		if (cuisson.ID == Joueur.IdObjetBolCeramiqueEtain
			&& !FourTorchieThermodynamique.TemperatureSuffisanteFonteEtain(_fourTorchieTemperature))
			return -1f;
		return Mathf.Clamp((float)(_fourTorchieProgressCuissonSec[indexCuisson] / duree), 0f, 1f);
	}

	public static bool EstSlotCombustibleFourTorchie(SlotInventaire s) =>
		FourTorchieThermodynamique.EstCombustibleFourTorchie(s);

	public static bool EstObjetCuissableFourTorchie(SlotInventaire s) =>
		!s.EstVide && (s.ID == Joueur.IdObjetSteakCru
			|| FourTorchieThermodynamique.EstObjetArgileCuissableFour(s.ID)
			|| FourTorchieThermodynamique.EstObjetFonteEtainCuissableFour(s.ID));

	public static bool EstSlotCuissonFourTorchie(SlotInventaire s) => EstObjetCuissableFourTorchie(s);

	public static bool EstSlotResultatFourTorchie(SlotInventaire s) =>
		!s.EstVide && (FourTorchieThermodynamique.EstSteakCuitNormal(s)
			|| FourTorchieThermodynamique.EstSteakBrule(s)
			|| FourTorchieThermodynamique.EstBolCeramiqueChaud(s)
			|| FourTorchieThermodynamique.EstBolCeramiqueRefroidi(s)
			|| FourTorchieThermodynamique.EstMouleCeramiqueChaud(s)
			|| FourTorchieThermodynamique.EstMouleCeramiqueRefroidi(s)
			|| FourTorchieThermodynamique.EstMouleEtainFonduChaud(s)
			|| FourTorchieThermodynamique.EstMouleEtainSolidifie(s)
			|| FourTorchieThermodynamique.EstBolEtainFonduChaud(s)
			|| FourTorchieThermodynamique.EstBolCeramiqueScorieChaud(s)
			|| s.ID == Joueur.IdObjetBolEtainSolidifie
			|| (s.ID == Joueur.IdObjetBolCeramiqueScorie && s.IndexChimique == 0)
			|| FourTorchieThermodynamique.EstSlotChamotteFour(s));

	public static bool SontResultatsFourTorchieCompatibles(SlotInventaire cuisson, SlotInventaire resultat)
	{
		if (resultat.EstVide)
			return true;
		if (cuisson.EstVide)
			return EstSlotResultatFourTorchie(resultat);
		if (cuisson.ID == Joueur.IdObjetSteakCru)
			return FourTorchieThermodynamique.EstSteakCuitNormal(resultat)
				|| FourTorchieThermodynamique.EstSteakBrule(resultat);
		if (cuisson.ID == Joueur.IdObjetBolArgile)
			return FourTorchieThermodynamique.EstBolCeramiqueChaud(resultat)
				|| FourTorchieThermodynamique.EstBolCeramiqueRefroidi(resultat)
				|| FourTorchieThermodynamique.EstSlotChamotteFour(resultat);
		if (cuisson.ID == Joueur.IdObjetMouleArgile)
			return FourTorchieThermodynamique.EstMouleCeramiqueChaud(resultat)
				|| FourTorchieThermodynamique.EstMouleCeramiqueRefroidi(resultat)
				|| FourTorchieThermodynamique.EstSlotChamotteFour(resultat);
		if (cuisson.ID == Joueur.IdObjetBolCeramiqueEtain)
			return FourTorchieThermodynamique.EstBolEtainFonduChaud(resultat)
				|| FourTorchieThermodynamique.EstBolCeramiqueScorieChaud(resultat)
				|| resultat.ID == Joueur.IdObjetBolEtainSolidifie
				|| (resultat.ID == Joueur.IdObjetBolCeramiqueScorie && resultat.IndexChimique == 0);
		return false;
	}

	public static bool EstIndexSlotCombustibleFourTorchie(int idx) => idx == FourTorchieSlotCombustible;

	public static bool EstIndexSlotCuissonFourTorchie(int idx) =>
		idx >= FourTorchiePremierSlotCuisson && idx < FourTorchiePremierSlotCuisson + FourTorchieNbCuisson;

	public static bool EstIndexSlotResultatFourTorchie(int idx) =>
		idx >= FourTorchiePremierSlotResultat && idx < FourTorchiePremierSlotResultat + FourTorchieNbCuisson;

	public bool EstSlotStockableDansFourTorchieIndex(int idx, SlotInventaire s)
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie || s.EstVide)
			return false;
		if (EstIndexSlotCombustibleFourTorchie(idx))
			return EstSlotCombustibleFourTorchie(s);
		if (EstIndexSlotCuissonFourTorchie(idx))
			return EstSlotCuissonFourTorchie(s);
		if (EstIndexSlotResultatFourTorchie(idx))
		{
			SlotInventaire slot = GrilleFourTorchie[idx];
			if (slot.EstVide)
				return EstSlotResultatFourTorchie(s);
			return EstSlotResultatFourTorchie(s) && Joueur.SontEmpilables(slot, s);
		}
		return false;
	}

	public void NotifierCombustibleFourTorchieModifie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;
		AssurerGrilleFourTorchie();
		SynchroniserGenomeFourTorchie();
	}

	public bool ActiverFourTorchieAllume()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie || _fourTorchieHp <= 0.001f)
			return false;
		AssurerGrilleFourTorchie();
		RechargerSlotsFourTorchieDepuisGenomeSiBesoin();
		if (_fourTorchieAllume && (_fourTorchieResteCombSec > 0.001d || _fourTorchieAnomalieAnthracite))
		{
			MettreAJourVisuelFourTorchie(true);
			return true;
		}
		if (!EstSlotCombustibleFourTorchie(GrilleFourTorchie[FourTorchieSlotCombustible]))
			return false;
		_fourTorchieAllume = true;
		if (_fourTorchieResteCombSec <= 0.001d && !_fourTorchieAnomalieAnthracite
			&& !EssayerDemarrerProchainCombustibleFourTorchie())
		{
			_fourTorchieAllume = false;
			return false;
		}
		MettreAJourVisuelFourTorchie(true);
		SynchroniserGenomeFourTorchie();
		return true;
	}

	public bool EteindreFourTorchie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return false;
		_fourTorchieAllume = false;
		_fourTorchieResteCombSec = 0d;
		_fourTorchieProfilActifValide = false;
		_fourTorchieAnomalieAnthracite = false;
		_fourTorchieAnomalieResteSec = 0d;
		for (int i = 0; i < FourTorchieNbCuisson; i++)
			_fourTorchieProgressCuissonSec[i] = 0d;
		MettreAJourVisuelFourTorchie(false);
		SynchroniserGenomeFourTorchie();
		return true;
	}

	private void RechargerSlotsFourTorchieDepuisGenomeSiBesoin()
	{
		bool grilleVide = true;
		for (int i = 0; i < FourTorchieNbSlots; i++)
		{
			if (!GrilleFourTorchie[i].EstVide)
			{
				grilleVide = false;
				break;
			}
		}
		if (!grilleVide)
			return;

		string g = GenomeAssemblage ?? "";
		if (!g.Contains(SeparateurSlotsFourTorchie, StringComparison.Ordinal))
			return;
		string[] parties = g.Split(SeparateurSlotsFourTorchie, 2, StringSplitOptions.None);
		if (parties.Length < 2)
			return;
		string[] slotsParts = parties[1].Split('#');
		for (int i = 0; i < FourTorchieNbSlots && i < slotsParts.Length; i++)
			GrilleFourTorchie[i] = DecoderSlotFourTorchie(slotsParts[i]);
	}

	public void SynchroniserFourTorchieDepuisGrille()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;
		AssurerGrilleFourTorchie();
		SynchroniserGenomeFourTorchie();
	}

	private void AssurerGrilleFourTorchie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;
		if (GrilleFourTorchie == null || GrilleFourTorchie.Length < FourTorchieNbSlots)
		{
			var ancienne = GrilleFourTorchie;
			GrilleFourTorchie = new SlotInventaire[FourTorchieNbSlots];
			if (ancienne != null)
			{
				int n = Mathf.Min(ancienne.Length, GrilleFourTorchie.Length);
				for (int i = 0; i < n; i++)
					GrilleFourTorchie[i] = ancienne[i];
			}
		}
	}

	private void InitialiserFourTorchiePose()
	{
		Mass = 28f;
		GravityScale = 0f;
		ResistanceActuelle = 44f;
		Scale = Vector3.One;
		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		Sleeping = true;
		Freeze = true;
		FreezeMode = FreezeModeEnum.Static;
		AssurerGrilleFourTorchie();
		ChargerEtatFourTorchieDepuisGenome();
		MettreAJourVisuelFourTorchie();
	}

	private void TraiterFourTorchie(double delta)
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;
		if (_fourTorchieHp <= 0.001f && !_fourTorchieAnomalieAnthracite)
			return;
		AssurerGrilleFourTorchie();
		float dt = (float)Math.Max(0d, delta);

		if (_fourTorchieAnomalieAnthracite)
		{
			_fourTorchieAnomalieResteSec = Math.Max(0d, _fourTorchieAnomalieResteSec - delta);
			float cible = 1050f;
			float ecart = cible - _fourTorchieTemperature;
			_fourTorchieTemperature += Mathf.Max(3f, ecart * 0.22f) * dt;
			TraiterCuissonFourTorchie(delta);
			if (_fourTorchieAnomalieResteSec <= 0.001d || _fourTorchieTemperature >= 980f)
				ExecuterExplosionFourTorchie();
			SynchroniserGenomeFourTorchiePeriodique(delta);
			MettreAJourVisuelFourTorchie();
			AnimerVisuelFourTorchie(dt);
			return;
		}

		if (_fourTorchieAllume && _fourTorchieResteCombSec > 0.001d)
		{
			if (!_fourTorchieProfilActifValide)
			{
				ref SlotInventaire comb = ref GrilleFourTorchie[FourTorchieSlotCombustible];
				_fourTorchieProfilActifValide = EstSlotCombustibleFourTorchie(comb)
					&& FourTorchieThermodynamique.ResoudreProfilCombustible(comb, out _fourTorchieProfilActif);
			}
			_fourTorchieResteCombSec -= delta;
			if (_fourTorchieProfilActifValide)
			{
				float deltaChauffe = FourTorchieThermodynamique.CalculerDeltaChauffe(
					_fourTorchieTemperature, _fourTorchieProfilActif, _fourTorchieEncrassementMalusC, dt);
				_fourTorchieTemperature += deltaChauffe;
			}
			TraiterCuissonFourTorchie(delta);
			if (_fourTorchieResteCombSec <= 0.001d)
				FinaliserUniteCombustibleFourTorchie();
		}
		else if (!_fourTorchieAllume || _fourTorchieResteCombSec <= 0.001d)
		{
			float deltaFroid = FourTorchieThermodynamique.CalculerDeltaRefroidissement(_fourTorchieTemperature, dt);
			_fourTorchieTemperature = Mathf.Max(FourTorchieThermodynamique.TempAmbianteC, _fourTorchieTemperature - deltaFroid);
		}

		TraiterRefroidissementBolsCeramiqueDansFour(delta);
		SynchroniserGenomeFourTorchiePeriodique(delta);
		MettreAJourVisuelFourTorchie();
		AnimerVisuelFourTorchie(dt);
	}

	private bool FourPeutRefroidirBolsCeramique()
	{
		if (_fourTorchieAnomalieAnthracite)
			return false;
		bool pasDeChauffeActive = !_fourTorchieAllume
			|| _fourTorchieResteCombSec <= 0.001d
			|| !_fourTorchieProfilActifValide;
		if (!pasDeChauffeActive)
			return false;
		return _fourTorchieTemperature <= FourTorchieThermodynamique.TempAmbianteC + 35f;
	}

	private void TraiterRefroidissementBolsCeramiqueDansFour(double delta)
	{
		if (!FourPeutRefroidirBolsCeramique())
			return;

		float facteurAmbiance = 1f - Mathf.Clamp(
			(_fourTorchieTemperature - FourTorchieThermodynamique.TempAmbianteC) / 120f, 0f, 1f);
		double dtBol = delta * facteurAmbiance;
		if (dtBol <= 0.0001d)
			return;

		bool modifie = false;
		for (int i = 0; i < FourTorchieNbSlots; i++)
		{
			ref SlotInventaire slot = ref GrilleFourTorchie[i];
			if (FourTorchieThermodynamique.EstBolCeramiqueChaud(slot))
			{
				EssayerLireEtatBolCeramiqueSlot(slot, out _, out double progSecBol);
				progSecBol += dtBol;
				if (progSecBol >= FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
					EcrireEtatBolCeramiqueSlot(ref slot, 0, 0d);
				else
					EcrireEtatBolCeramiqueSlot(
						ref slot,
						FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
						progSecBol);
				modifie = true;
				continue;
			}

			if (FourTorchieThermodynamique.EstBolEtainFonduChaud(slot))
			{
				EssayerLireEtatBolEtainFonduSlot(slot, out _, out double progEtain);
				progEtain += dtBol;
				if (progEtain >= FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
				{
					slot.ID = Joueur.IdObjetBolEtainSolidifie;
					slot.IndexChimique = 0;
					slot.GenomeAssemblage = "";
				}
				else
					EcrireEtatBolEtainFonduSlot(
						ref slot,
						FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
						progEtain);
				modifie = true;
				continue;
			}

			if (FourTorchieThermodynamique.EstBolCeramiqueScorieChaud(slot))
			{
				EssayerLireEtatBolScorieSlot(slot, out _, out double progScorie);
				progScorie += dtBol;
				if (progScorie >= FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
					EcrireEtatBolScorieSlot(ref slot, 0, 0d);
				else
					EcrireEtatBolScorieSlot(
						ref slot,
						FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
						progScorie);
				modifie = true;
				continue;
			}

			if (!FourTorchieThermodynamique.EstMouleCeramiqueChaud(slot)
				&& !FourTorchieThermodynamique.EstMouleEtainFonduChaud(slot))
				continue;

			if (FourTorchieThermodynamique.EstMouleCeramiqueChaud(slot))
			{
				EssayerLireEtatMouleCeramiqueSlot(slot, out _, out double progSecMoule);
				progSecMoule += dtBol;
				if (progSecMoule >= FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
					EcrireEtatMouleCeramiqueSlot(ref slot, 0, 0d);
				else
					EcrireEtatMouleCeramiqueSlot(
						ref slot,
						FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique,
						progSecMoule);
			}
			else
			{
				EssayerLireEtatMouleCeramiqueSlot(slot, out _, out double progEtainMoule);
				progEtainMoule += dtBol;
				if (progEtainMoule >= FourTorchieThermodynamique.DureeRefroidissementBolCeramiqueSec)
					EcrireEtatMouleCeramiqueSlot(
						ref slot,
						FourTorchieThermodynamique.FlagMouleEtainSolidifieIndexChimique,
						0d);
				else
					EcrireEtatMouleCeramiqueSlot(
						ref slot,
						FourTorchieThermodynamique.FlagMouleEtainFonduChaudIndexChimique,
						progEtainMoule);
			}
			modifie = true;
		}

		if (modifie)
			SynchroniserGenomeFourTorchie();
	}

	private void SynchroniserGenomeFourTorchiePeriodique(double delta)
	{
		_fourTorchieDernierSyncFourSec -= delta;
		if (_fourTorchieDernierSyncFourSec <= 0d)
		{
			_fourTorchieDernierSyncFourSec = 0.45d;
			SynchroniserGenomeFourTorchie();
		}
	}

	private void DeclencherAnomalieAnthracite()
	{
		if (_fourTorchieAnomalieAnthracite)
			return;
		_fourTorchieAnomalieAnthracite = true;
		_fourTorchieAnomalieResteSec = FourTorchieDureeMonteeAnthraciteSec;
		_fourTorchieHp = 0f;
		_fourTorchieAllume = true;
		_fourTorchieResteCombSec = 0d;
		_fourTorchieProfilActifValide = false;
		GD.Print("SEROKA : ANTHRACITE dans le four — atrophie thermique ! Éloignez-vous !");
		MettreAJourVisuelFourTorchie(true);
		SynchroniserGenomeFourTorchie();
	}

	/// <summary>Fin d'une unité de combustible : enchaîne la suivante ou éteint le four sans consommer le stock au repos.</summary>
	private void FinaliserUniteCombustibleFourTorchie()
	{
		if (!_fourTorchieAllume || _fourTorchieAnomalieAnthracite)
			return;
		if (!EssayerDemarrerProchainCombustibleFourTorchie())
			_fourTorchieAllume = false;
	}

	private bool EssayerDemarrerProchainCombustibleFourTorchie()
	{
		if (!_fourTorchieAllume || _fourTorchieAnomalieAnthracite || _fourTorchieHp <= 0.001f)
			return false;

		ref SlotInventaire comb = ref GrilleFourTorchie[FourTorchieSlotCombustible];
		if (!EstSlotCombustibleFourTorchie(comb))
		{
			_fourTorchieResteCombSec = 0d;
			_fourTorchieProfilActifValide = false;
			return false;
		}

		if (comb.ID == Joueur.IdObjetCharbonAntracite)
		{
			DeclencherAnomalieAnthracite();
			int q = Joueur.ObtenirQuantiteSlot(comb);
			if (q <= 1) comb = new SlotInventaire();
			else comb.Quantite = q - 1;
			return true;
		}

		if (!FourTorchieThermodynamique.ResoudreProfilCombustible(comb, out ProfilCombustibleFourTorchie profil))
			return false;

		int qte = Joueur.ObtenirQuantiteSlot(comb);
		if (qte <= 1) comb = new SlotInventaire();
		else comb.Quantite = qte - 1;

		_fourTorchieProfilActif = profil;
		_fourTorchieProfilActifValide = true;
		_fourTorchieResteCombSec = Math.Max(0.5d, profil.DureeSec);
		_fourTorchieDureeUniteCouranteSec = _fourTorchieResteCombSec;

		if (profil.EncrasseFour)
			_fourTorchieEncrassementMalusC = Mathf.Min(120f, _fourTorchieEncrassementMalusC + FourTorchieEncrassementParUniteResineux);
		if (profil.GazToxique)
			GD.Print("SEROKA : Fumée sulfureuse — charbon de mauvaise qualité dans le four.");

		MettreAJourVisuelFourTorchie(true);
		SynchroniserGenomeFourTorchie();
		return true;
	}

	private void TraiterCuissonFourTorchie(double delta)
	{
		bool modifie = false;
		for (int i = 0; i < FourTorchieNbCuisson; i++)
		{
			if (!EssayerAvancerCuissonFourTorchie(i, delta))
				continue;
			modifie = true;
		}
		if (modifie)
			SynchroniserGenomeFourTorchie();
	}

	private bool EssayerAvancerCuissonFourTorchie(int indexCuisson, double delta)
	{
		int slotCuisson = FourTorchiePremierSlotCuisson + indexCuisson;
		int slotResultat = FourTorchiePremierSlotResultat + indexCuisson;
		ref SlotInventaire cuisson = ref GrilleFourTorchie[slotCuisson];
		ref SlotInventaire resultat = ref GrilleFourTorchie[slotResultat];

		if (!EstSlotCuissonFourTorchie(cuisson))
		{
			if (_fourTorchieProgressCuissonSec[indexCuisson] > 0.001d
				|| _fourTorchieEtainMarqueScorie[indexCuisson])
			{
				_fourTorchieProgressCuissonSec[indexCuisson] = 0d;
				_fourTorchieEtainMarqueScorie[indexCuisson] = false;
				return true;
			}
			return false;
		}

		if (!SontResultatsFourTorchieCompatibles(cuisson, resultat))
		{
			_fourTorchieProgressCuissonSec[indexCuisson] = 0d;
			return false;
		}
		if (!resultat.EstVide)
		{
			int maxPile = Mathf.Max(1, Joueur.ObtenirPileMax(resultat));
			if (Joueur.ObtenirQuantiteSlot(resultat) >= maxPile)
			{
				_fourTorchieProgressCuissonSec[indexCuisson] = 0d;
				return false;
			}
		}

		if (FourTorchieThermodynamique.EstObjetArgileCuissableFour(cuisson.ID))
			return EssayerAvancerCuissonArgileFourTorchie(indexCuisson, delta, ref cuisson, ref resultat);
		if (FourTorchieThermodynamique.EstObjetFonteEtainCuissableFour(cuisson.ID))
			return EssayerAvancerCuissonEtainFourTorchie(indexCuisson, delta, ref cuisson, ref resultat);
		return EssayerAvancerCuissonSteakFourTorchie(indexCuisson, delta, ref cuisson, ref resultat);
	}

	private bool EssayerAvancerCuissonSteakFourTorchie(int indexCuisson, double delta, ref SlotInventaire cuisson, ref SlotInventaire resultat)
	{
		if (_fourTorchieTemperature < FourTorchieThermodynamique.SeuilCuissonMinC)
			return false;

		double duree = FourTorchieThermodynamique.DureeCuissonSteakSec;
		_fourTorchieProgressCuissonSec[indexCuisson] += delta;
		bool modifie = false;
		while (_fourTorchieProgressCuissonSec[indexCuisson] >= duree && EstSlotCuissonFourTorchie(cuisson) && cuisson.ID == Joueur.IdObjetSteakCru)
		{
			bool brule = _fourTorchieTemperature > FourTorchieThermodynamique.SeuilBrulureCuissonC;
			var produit = cuisson;
			produit.ID = Joueur.IdObjetSteakCuit;
			produit.Quantite = 1;
			produit.IndexChimique = brule ? FourTorchieThermodynamique.FlagSteakBruleIndexChimique : 0;

			if (!DeposerProduitCuissonFourTorchie(ref resultat, produit))
				break;

			int qCru = Joueur.ObtenirQuantiteSlot(cuisson) - 1;
			if (qCru <= 0) cuisson = new SlotInventaire();
			else cuisson.Quantite = qCru;
			ObtenirJoueurMonde()?.AjouterXpMetier("Cuisinier", 1UL);
			_fourTorchieProgressCuissonSec[indexCuisson] -= duree;
			modifie = true;
		}
		return modifie;
	}

	private bool EssayerAvancerCuissonArgileFourTorchie(int indexCuisson, double delta, ref SlotInventaire cuisson, ref SlotInventaire resultat)
	{
		if (_fourTorchieTemperature > FourTorchieThermodynamique.SeuilCuissonBolArgileMaxC)
		{
			bool modifie = FinaliserEchecArgileFourTorchie(indexCuisson, ref cuisson, ref resultat);
			_fourTorchieProgressCuissonSec[indexCuisson] = 0d;
			return modifie;
		}

		if (!FourTorchieThermodynamique.TemperatureDansPlageCuissonBolArgile(_fourTorchieTemperature))
			return false;

		double duree = FourTorchieThermodynamique.DureeCuissonBolArgileSec;
		_fourTorchieProgressCuissonSec[indexCuisson] += delta;
		bool modifieOk = false;
		while (_fourTorchieProgressCuissonSec[indexCuisson] >= duree
			&& EstSlotCuissonFourTorchie(cuisson)
			&& FourTorchieThermodynamique.EstObjetArgileCuissableFour(cuisson.ID))
		{
			if (_fourTorchieTemperature > FourTorchieThermodynamique.SeuilCuissonBolArgileMaxC)
			{
				modifieOk |= FinaliserEchecArgileFourTorchie(indexCuisson, ref cuisson, ref resultat);
				_fourTorchieProgressCuissonSec[indexCuisson] = 0d;
				break;
			}
			if (!FourTorchieThermodynamique.TemperatureDansPlageCuissonBolArgile(_fourTorchieTemperature))
				break;

			SlotInventaire produit = cuisson.ID == Joueur.IdObjetMouleArgile
				? CreerSlotMouleCeramiqueChaud()
				: CreerSlotBolCeramiqueChaud();

			if (!DeposerProduitCuissonFourTorchie(ref resultat, produit))
				break;

			int qCru = Joueur.ObtenirQuantiteSlot(cuisson) - 1;
			if (qCru <= 0) cuisson = new SlotInventaire();
			else cuisson.Quantite = qCru;
			ObtenirJoueurMonde()?.AjouterXpMetier("Potier", 2UL);
			_fourTorchieProgressCuissonSec[indexCuisson] -= duree;
			modifieOk = true;
		}
		return modifieOk;
	}

	private bool EssayerAvancerCuissonEtainFourTorchie(int indexCuisson, double delta, ref SlotInventaire cuisson, ref SlotInventaire resultat)
	{
		if (_fourTorchieTemperature >= FourTorchieThermodynamique.SeuilFonteEtainScorieC)
			_fourTorchieEtainMarqueScorie[indexCuisson] = true;

		if (_fourTorchieTemperature < FourTorchieThermodynamique.SeuilFonteEtainMinC)
			return false;

		double duree = FourTorchieThermodynamique.DureeFonteEtainSec;
		_fourTorchieProgressCuissonSec[indexCuisson] += delta;
		bool modifie = false;
		while (_fourTorchieProgressCuissonSec[indexCuisson] >= duree
			&& EstSlotCuissonFourTorchie(cuisson)
			&& cuisson.ID == Joueur.IdObjetBolCeramiqueEtain)
		{
			SlotInventaire produit = _fourTorchieEtainMarqueScorie[indexCuisson]
				? CreerSlotBolCeramiqueScorieChaud()
				: CreerSlotBolEtainFonduChaud();

			if (!DeposerProduitCuissonFourTorchie(ref resultat, produit))
				break;

			int qCru = Joueur.ObtenirQuantiteSlot(cuisson) - 1;
			if (qCru <= 0) cuisson = new SlotInventaire();
			else cuisson.Quantite = qCru;
			ObtenirJoueurMonde()?.AjouterXpMetier("Forgeron", 2UL);
			_fourTorchieProgressCuissonSec[indexCuisson] -= duree;
			_fourTorchieEtainMarqueScorie[indexCuisson] = false;
			modifie = true;
		}
		return modifie;
	}

	private static bool DeposerProduitCuissonFourTorchie(ref SlotInventaire resultat, SlotInventaire produit)
	{
		if (resultat.EstVide)
		{
			resultat = produit;
			return true;
		}
		if (!Joueur.SontEmpilables(resultat, produit))
			return false;
		int maxPile = Mathf.Max(1, Joueur.ObtenirPileMax(resultat));
		if (Joueur.ObtenirQuantiteSlot(resultat) >= maxPile)
			return false;
		resultat.Quantite = Joueur.ObtenirQuantiteSlot(resultat) + 1;
		return true;
	}

	private bool FinaliserEchecArgileFourTorchie(int indexCuisson, ref SlotInventaire cuisson, ref SlotInventaire resultat)
	{
		int idCuisson = cuisson.ID;
		var echec = new SlotInventaire { ID = Joueur.IdObjetChamotte, Quantite = 1 };
		if (!DeposerProduitCuissonFourTorchie(ref resultat, echec))
			return false;
		int qCru = Joueur.ObtenirQuantiteSlot(cuisson) - 1;
		if (qCru <= 0) cuisson = new SlotInventaire();
		else cuisson.Quantite = qCru;
		_fourTorchieProgressCuissonSec[indexCuisson] = 0d;
		string libelle = idCuisson == Joueur.IdObjetMouleArgile ? "Moule en argile" : "Bol en argile";
		GD.Print($"SEROKA : {libelle} sur-cuit dans le four — 1 chamotte récupérée.");
		return true;
	}

	public void SynchroniserGenomeFourTorchie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;
		AssurerGrilleFourTorchie();
		long t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long resteMs = (long)Mathf.Round((float)Math.Max(0d, _fourTorchieResteCombSec * 1000.0));
		long dureeMs = (long)Mathf.Round((float)Math.Max(1d, _fourTorchieDureeUniteCouranteSec * 1000.0));
		string progs = string.Join(":",
			((long)Mathf.Round(_fourTorchieProgressCuissonSec[0] * 1000.0)).ToString(),
			((long)Mathf.Round(_fourTorchieProgressCuissonSec[1] * 1000.0)).ToString(),
			((long)Mathf.Round(_fourTorchieProgressCuissonSec[2] * 1000.0)).ToString(),
			((long)Mathf.Round(_fourTorchieProgressCuissonSec[3] * 1000.0)).ToString());
		var slotsEnc = new string[FourTorchieNbSlots];
		for (int i = 0; i < FourTorchieNbSlots; i++)
			slotsEnc[i] = EncoderSlotFourTorchie(GrilleFourTorchie[i]);
		int allume = _fourTorchieAllume ? 1 : 0;
		int anomalie = _fourTorchieAnomalieAnthracite ? 1 : 0;
		GenomeAssemblage = $"{PrefixGenomeFourTorchie}{t0}:{_fourTorchieTemperature:F1}:{resteMs}:{dureeMs}:{allume}:{_fourTorchieHp:F0}:{_fourTorchieEncrassementMalusC:F0}:{anomalie}:{progs}{SeparateurSlotsFourTorchie}{string.Join("#", slotsEnc)}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
	}

	private static string EncoderSlotFourTorchie(SlotInventaire s)
	{
		if (s.EstVide) return "-";
		string baseEnc = $"{s.ID},{Joueur.ObtenirQuantiteSlot(s)},{s.IndexBotanique},{s.IndexChimique},{s.IndexMorphologique}";
		if (!string.IsNullOrEmpty(s.GenomeAssemblage))
			return $"{baseEnc}~{s.GenomeAssemblage}";
		return baseEnc;
	}

	private static SlotInventaire DecoderSlotFourTorchie(string part)
	{
		if (string.IsNullOrEmpty(part) || part == "-")
			return new SlotInventaire();
		string[] partiesGenome = part.Split('~', 2);
		string[] m = partiesGenome[0].Split(',');
		if (m.Length < 2 || !int.TryParse(m[0], out int id) || id <= 0)
			return new SlotInventaire();
		int.TryParse(m[1], out int q);
		byte.TryParse(m.Length > 2 ? m[2] : "0", out byte bot);
		int.TryParse(m.Length > 3 ? m[3] : "0", out int chi);
		int.TryParse(m.Length > 4 ? m[4] : "0", out int mor);
		string genome = partiesGenome.Length > 1 ? partiesGenome[1] : "";
		if (id == Joueur.IdObjetBolCeramique
			&& chi == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique
			&& string.IsNullOrEmpty(genome))
		{
			genome = $"{PrefixGenomeBolCeramique}{chi}:0";
		}
		if (id == Joueur.IdObjetMouleCeramique
			&& chi == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique
			&& string.IsNullOrEmpty(genome))
		{
			genome = $"{PrefixGenomeMouleCeramique}{chi}:0";
		}
		if (id == Joueur.IdObjetBolEtainFonduChaud
			&& chi == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique
			&& string.IsNullOrEmpty(genome))
		{
			genome = $"{PrefixGenomeBolEtainFondu}{chi}:0";
		}
		if (id == Joueur.IdObjetBolCeramiqueScorie
			&& chi == FourTorchieThermodynamique.FlagBolCeramiqueChaudIndexChimique
			&& string.IsNullOrEmpty(genome))
		{
			genome = $"{PrefixGenomeBolScorie}{chi}:0";
		}
		var slotDecode = new SlotInventaire
		{
			ID = id,
			Quantite = Mathf.Max(1, q),
			IndexBotanique = bot,
			IndexChimique = chi,
			IndexMorphologique = mor,
			GenomeAssemblage = genome
		};
		EssayerFinaliserBolEtainFonduRefroidi(ref slotDecode);
		return slotDecode;
	}

	private void ChargerEtatFourTorchieDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;
		AssurerGrilleFourTorchie();
		long maintenant = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		string g = GenomeAssemblage ?? "";

		if (g.StartsWith(PrefixGenomeFourTorchie, StringComparison.Ordinal)
			|| g.StartsWith("FOURTORCHIE2:", StringComparison.Ordinal))
		{
			string prefix = g.StartsWith(PrefixGenomeFourTorchie, StringComparison.Ordinal) ? PrefixGenomeFourTorchie : "FOURTORCHIE2:";
			string payload = g.Substring(prefix.Length);
			string[] partiesSlots = payload.Split(SeparateurSlotsFourTorchie, 2, StringSplitOptions.None);
			string entete = partiesSlots[0];
			string[] m = entete.Split(':');
			if (m.Length >= 7)
			{
				long.TryParse(m[0], out long t0);
				float.TryParse(m[1], System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out float temp);
				long.TryParse(m[2], out long resteMs);
				long.TryParse(m[3], out long dureeMs);
				int offset = 4;
				if (prefix == PrefixGenomeFourTorchie && m.Length >= 11)
				{
					int.TryParse(m[4], out int allume);
					float.TryParse(m[5], System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out float hp);
					float.TryParse(m[6], System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out float encrasse);
					int.TryParse(m[7], out int anomalie);
					_fourTorchieAllume = allume != 0;
					_fourTorchieHp = hp;
					_fourTorchieEncrassementMalusC = encrasse;
					_fourTorchieAnomalieAnthracite = anomalie != 0;
					if (_fourTorchieAnomalieAnthracite)
						_fourTorchieAnomalieResteSec = FourTorchieDureeMonteeAnthraciteSec;
					offset = 8;
				}

				long[] progMs = new long[FourTorchieNbCuisson];
				for (int i = 0; i < FourTorchieNbCuisson && offset + i < m.Length; i++)
					long.TryParse(m[offset + i], out progMs[i]);

				if (partiesSlots.Length > 1)
				{
					string[] slotsParts = partiesSlots[1].Split('#');
					for (int i = 0; i < FourTorchieNbSlots && i < slotsParts.Length; i++)
						GrilleFourTorchie[i] = DecoderSlotFourTorchie(slotsParts[i]);
				}

				double resteUnite = Math.Max(0d, resteMs / 1000.0);
				double dureeUnite = Math.Max(1d, dureeMs / 1000.0);
				for (int i = 0; i < FourTorchieNbCuisson; i++)
					_fourTorchieProgressCuissonSec[i] = Math.Max(0d, progMs[i] / 1000.0);

				_fourTorchieTemperature = temp > 1f ? temp : FourTorchieThermodynamique.TempAmbianteC;
				_fourTorchieDureeUniteCouranteSec = dureeUnite;
				_fourTorchieResteCombSec = resteUnite;

				SlotInventaire comb = GrilleFourTorchie[FourTorchieSlotCombustible];
				_fourTorchieProfilActifValide = EstSlotCombustibleFourTorchie(comb)
					&& FourTorchieThermodynamique.ResoudreProfilCombustible(comb, out _fourTorchieProfilActif);

				double tempsEcoule = Math.Max(0d, (maintenant - t0) / 1000.0);
				if (tempsEcoule > 0.01d)
				{
					if (_fourTorchieAnomalieAnthracite)
						SimulerRattrapageAnomalieAnthracite(tempsEcoule);
					else
						SimulerRattrapageFourTorchie(tempsEcoule);
				}

				NormaliserEtatAllumageFourTorchieApresChargement();

				if (_fourTorchieAnomalieAnthracite && !GodotObject.IsInstanceValid(this))
					return;

				_fourTorchieDernierSyncFourSec = -1d;
				SynchroniserGenomeFourTorchie();
				return;
			}
		}

		_fourTorchieTemperature = FourTorchieThermodynamique.TempAmbianteC;
		_fourTorchieAllume = false;
		_fourTorchieHp = FourTorchieThermodynamique.FourHpMax;
		_fourTorchieResteCombSec = 0d;
		_fourTorchieProfilActifValide = false;
		for (int i = 0; i < FourTorchieNbCuisson; i++)
			_fourTorchieProgressCuissonSec[i] = 0d;
	}

	private void SimulerRattrapageAnomalieAnthracite(double tempsEcoule)
	{
		double t = tempsEcoule;
		int garde = 0;
		while (t > 0.0001d && garde++ < 500000)
		{
			double dt = Math.Min(t, Math.Max(0.05d, _fourTorchieAnomalieResteSec));
			_fourTorchieAnomalieResteSec -= dt;
			float ecart = 1050f - _fourTorchieTemperature;
			_fourTorchieTemperature += Mathf.Max(3f, ecart * 0.22f) * (float)dt;
			if (_fourTorchieTemperature >= FourTorchieThermodynamique.SeuilCuissonMinC)
				TraiterCuissonFourTorchie(dt);
			t -= dt;
			if (_fourTorchieAnomalieResteSec <= 0.001d || _fourTorchieTemperature >= 980f)
			{
				ExecuterExplosionFourTorchie();
				return;
			}
		}
	}

	private void SimulerRattrapageFourTorchie(double tempsEcoule)
	{
		double t = tempsEcoule;
		int garde = 0;
		while (t > 0.0001d && garde++ < 500000 && (_fourTorchieHp > 0.001f || _fourTorchieAnomalieAnthracite))
		{
			if (_fourTorchieAnomalieAnthracite)
			{
				SimulerRattrapageAnomalieAnthracite(t);
				return;
			}

			if (_fourTorchieAllume && _fourTorchieResteCombSec > 0.001d)
			{
				double dt = Math.Min(t, _fourTorchieResteCombSec);
				if (_fourTorchieProfilActifValide)
				{
					float deltaChauffe = FourTorchieThermodynamique.CalculerDeltaChauffe(
						_fourTorchieTemperature, _fourTorchieProfilActif, _fourTorchieEncrassementMalusC, (float)dt);
					_fourTorchieTemperature += deltaChauffe;
				}
				if (_fourTorchieTemperature >= FourTorchieThermodynamique.SeuilCuissonMinC)
					TraiterCuissonFourTorchie(dt);
				_fourTorchieResteCombSec -= dt;
				t -= dt;
				if (_fourTorchieResteCombSec <= 0.001d)
					FinaliserUniteCombustibleFourTorchie();
			}
			else
			{
				double dt = Math.Min(t, 1.0);
				float deltaFroid = FourTorchieThermodynamique.CalculerDeltaRefroidissement(_fourTorchieTemperature, (float)dt);
				_fourTorchieTemperature = Mathf.Max(FourTorchieThermodynamique.TempAmbianteC, _fourTorchieTemperature - deltaFroid);
				TraiterRefroidissementBolsCeramiqueDansFour(dt);
				t -= dt;
			}
		}
	}

	/// <summary>
	/// Corrige l'état « allumé » sans combustion active (ex. four froid laissé en déco avec charbon) :
	/// évite une consommation fantôme au prochain rattrapage hors ligne.
	/// </summary>
	private void NormaliserEtatAllumageFourTorchieApresChargement()
	{
		if (!_fourTorchieAllume || _fourTorchieAnomalieAnthracite || _fourTorchieResteCombSec > 0.001d)
			return;
		_fourTorchieAllume = false;
		_fourTorchieProfilActifValide = false;
	}
}
