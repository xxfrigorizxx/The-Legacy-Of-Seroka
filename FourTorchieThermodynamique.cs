using Godot;

/// <summary>Profil thermique d'une unité de combustible pour le four en torchie (tier 1).</summary>
public readonly struct ProfilCombustibleFourTorchie
{
	public readonly float TempMaxC;
	public readonly double DureeSec;
	public readonly float VitesseChauffeBase;
	public readonly bool EncrasseFour;
	public readonly bool GazToxique;
	public readonly bool EstAnomalieAnthracite;
	public readonly string NomDebug;

	public ProfilCombustibleFourTorchie(
		float tempMaxC,
		double dureeSec,
		float vitesseChauffeBase,
		bool encrasseFour = false,
		bool gazToxique = false,
		bool estAnomalieAnthracite = false,
		string nomDebug = "")
	{
		TempMaxC = tempMaxC;
		DureeSec = dureeSec;
		VitesseChauffeBase = vitesseChauffeBase;
		EncrasseFour = encrasseFour;
		GazToxique = gazToxique;
		EstAnomalieAnthracite = estAnomalieAnthracite;
		NomDebug = nomDebug ?? "";
	}
}

/// <summary>Thermodynamique SEROKA du four en torchie : températures réelles (°C), montée/descente progressive.</summary>
public static class FourTorchieThermodynamique
{
	public const float TempAmbianteC = 20f;
	public const float SeuilCuissonMinC = 80f;
	public const float SeuilBrulureCuissonC = 200f;
	public const float SeuilCuissonBolArgileMinC = 500f;
	public const float SeuilCuissonBolArgileMaxC = 700f;
	public const float FourHpMax = 100f;
	public const int FlagSteakBruleIndexChimique = 1;
	public const int FlagBolCeramiqueChaudIndexChimique = 2;
	public const int FlagMouleEtainFonduChaudIndexChimique = 3;
	public const int FlagMouleEtainSolidifieIndexChimique = 4;
	public const float SeuilFonteEtainMinC = 230f;
	public const float SeuilFonteEtainMaxC = 399f;
	public const float SeuilFonteEtainScorieC = 400f;
	public const double DureeFonteEtainSec = 30.0;
	public const double DureeCuissonSteakSec = 45.0;
	public const double DureeCuissonBolArgileSec = 150.0;
	public const double DureeRefroidissementBolCeramiqueSec = 60.0;
	public const float SeuilFacteurBrulureBolChaud = 0.12f;

	/// <summary>Calibré pour que chaque combustible atteigne ~98 % de son plafond avant d'être consumé.</summary>
	private const float FacteurMonteeThermique = 0.17f;

	public static bool EstCombustibleFourTorchie(SlotInventaire s)
	{
		if (s.EstVide)
			return false;
		return s.ID == 30 || s.ID == 32 || s.ID == BlocChutant.ID_BRANCHE
			|| Joueur.EstIdCharbonRecolte(s.ID);
	}

	public static bool ResoudreProfilCombustible(SlotInventaire s, out ProfilCombustibleFourTorchie profil)
	{
		profil = default;
		if (s.EstVide)
			return false;

		if (s.ID == Joueur.IdObjetCharbonAntracite)
		{
			profil = new ProfilCombustibleFourTorchie(1100f, 0d, 48f, estAnomalieAnthracite: true, nomDebug: "Anthracite");
			return true;
		}
		if (s.ID == Joueur.IdObjetCharbonBasseQualite)
		{
			profil = CreerProfilMineral(650f, 180d, gazToxique: true, nom: "Charbon basse qualité");
			return true;
		}
		if (s.ID == Joueur.IdObjetCharbonMoyenneQualite)
		{
			profil = CreerProfilMineral(750f, 240d, nom: "Charbon moyen");
			return true;
		}
		if (s.ID == Joueur.IdObjetCharbonBonneQualite)
		{
			profil = CreerProfilMineral(850f, 300d, nom: "Charbon bonne qualité");
			return true;
		}

		if (s.ID != 30 && s.ID != 32 && s.ID != BlocChutant.ID_BRANCHE)
			return false;

		byte essence = s.IndexBotanique;
		switch (essence)
		{
			case LSystem_Botanique.IndexJungle:
				profil = CreerProfilBois(250f, 20d, "Capotier (kapokier)");
				return true;
			case LSystem_Botanique.IndexSapin:
			case LSystem_Botanique.IndexPin:
				profil = CreerProfilBois(350f, 40d, "Bois résineux", encrasseFour: true);
				return true;
			case LSystem_Botanique.IndexChene:
			case LSystem_Botanique.IndexBouleau:
				profil = CreerProfilBois(200f, 60d, "Bois vert (fraîchement coupé)");
				return true;
			case LSystem_Botanique.IndexBouleauMort:
				profil = CreerProfilBois(450f, 80d, "Bouleau mort");
				return true;
			case LSystem_Botanique.IndexCheneMort:
				profil = CreerProfilBois(550f, 120d, "Chêne mort");
				return true;
			default:
				profil = CreerProfilBois(200f, 60d, "Bois (essence inconnue)");
				return true;
		}
	}

	/// <summary>Plafond thermique effectif (°C) pour un combustible, après malus d'encrassement.</summary>
	public static float ObtenirPlafondThermiqueCombustible(SlotInventaire s, float encrassementMalusC = 0f)
	{
		if (!ResoudreProfilCombustible(s, out ProfilCombustibleFourTorchie profil))
			return 0f;
		return Mathf.Max(TempAmbianteC, profil.TempMaxC - encrassementMalusC);
	}

	private static ProfilCombustibleFourTorchie CreerProfilBois(float tempMax, double dureeSec, string nom, bool encrasseFour = false)
	{
		float vitesse = CalculerVitesseChauffeBase(tempMax, dureeSec);
		return new ProfilCombustibleFourTorchie(tempMax, dureeSec, vitesse, encrasseFour, nomDebug: nom);
	}

	private static ProfilCombustibleFourTorchie CreerProfilMineral(float tempMax, double dureeSec, bool gazToxique = false, string nom = "")
	{
		float vitesse = CalculerVitesseChauffeBase(tempMax, dureeSec);
		return new ProfilCombustibleFourTorchie(tempMax, dureeSec, vitesse, gazToxique: gazToxique, nomDebug: nom);
	}

	private static float CalculerVitesseChauffeBase(float tempMaxC, double dureeSec)
	{
		double d = Mathf.Max(1.0, dureeSec);
		return (tempMaxC - TempAmbianteC) / (float)(d * FacteurMonteeThermique);
	}

	/// <summary>Montée progressive : plus le combustible est puissant, plus ça monte vite ; ralentit près du plafond thermique.</summary>
	public static float CalculerDeltaChauffe(float tempActuelleC, ProfilCombustibleFourTorchie profil, float encrassementMalusC, float dt)
	{
		float plafond = Mathf.Max(TempAmbianteC, profil.TempMaxC - encrassementMalusC);
		if (tempActuelleC >= plafond - 0.05f)
			return 0f;
		float ratio = 1f - Mathf.Clamp(tempActuelleC / Mathf.Max(1f, plafond), 0f, 0.97f);
		return profil.VitesseChauffeBase * ratio * dt;
	}

	/// <summary>Refroidissement progressif vers l'ambiante (plus rapide quand très chaud).</summary>
	public static float CalculerDeltaRefroidissement(float tempActuelleC, float dt)
	{
		float ecart = Mathf.Max(0f, tempActuelleC - TempAmbianteC);
		if (ecart <= 0.01f)
			return 0f;
		float vitesse = 6f + ecart * 0.035f;
		return Mathf.Min(ecart, vitesse * dt);
	}

	public static bool EstSteakBrule(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetSteakCuit && s.IndexChimique == FlagSteakBruleIndexChimique;

	public static bool EstSteakCuitNormal(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetSteakCuit && s.IndexChimique != FlagSteakBruleIndexChimique;

	public static bool EstBolCeramiqueChaud(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetBolCeramique
		&& s.IndexChimique == FlagBolCeramiqueChaudIndexChimique;

	public static bool EstBolCeramiqueRefroidi(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetBolCeramique
		&& s.IndexChimique != FlagBolCeramiqueChaudIndexChimique;

	public static bool EstMouleCeramiqueChaud(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetMouleCeramique
		&& s.IndexChimique == FlagBolCeramiqueChaudIndexChimique;

	public static bool EstMouleCeramiqueRefroidi(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetMouleCeramique
		&& s.IndexChimique != FlagBolCeramiqueChaudIndexChimique;

	public static bool EstObjetArgileCuissableFour(int id) =>
		id == Joueur.IdObjetBolArgile || id == Joueur.IdObjetMouleArgile;

	public static bool EstObjetFonteEtainCuissableFour(int id) =>
		id == Joueur.IdObjetBolCeramiqueEtain;

	public static bool TemperatureDansPlageFonteEtain(float tempC) =>
		tempC >= SeuilFonteEtainMinC && tempC <= SeuilFonteEtainMaxC;

	/// <summary>La fusion démarre dès 230 °C (pas de plafond : au-delà de 400 °C → scorie).</summary>
	public static bool TemperatureSuffisanteFonteEtain(float tempC) =>
		tempC >= SeuilFonteEtainMinC;

	public static bool EstBolEtainFonduChaud(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetBolEtainFonduChaud
		&& s.IndexChimique == FlagBolCeramiqueChaudIndexChimique;

	public static bool EstBolEtainFonduLiquide(SlotInventaire s) =>
		EstBolEtainFonduChaud(s) && ObtenirFacteurChaleurBolEtainFonduSlot(s) > 0.04f;

	public static bool EstBolCeramiqueScorieChaud(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetBolCeramiqueScorie
		&& s.IndexChimique == FlagBolCeramiqueChaudIndexChimique;

	public static bool EstMouleCeramiqueVideRefroidi(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetMouleCeramique
		&& s.IndexChimique == 0;

	public static bool EstMouleEtainFonduChaud(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetMouleCeramique
		&& s.IndexChimique == FlagMouleEtainFonduChaudIndexChimique;

	public static bool EstMouleEtainSolidifie(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetMouleCeramique
		&& s.IndexChimique == FlagMouleEtainSolidifieIndexChimique;

	public static bool EstSlotChamotteFour(SlotInventaire s) =>
		!s.EstVide && s.ID == Joueur.IdObjetChamotte;

	public static bool TemperatureDansPlageCuissonBolArgile(float tempC) =>
		tempC >= SeuilCuissonBolArgileMinC && tempC <= SeuilCuissonBolArgileMaxC;

	public static double ObtenirDureeCuissonFourPourSlot(SlotInventaire s)
	{
		if (!s.EstVide && s.ID == Joueur.IdObjetBolArgile)
			return DureeCuissonBolArgileSec;
		if (!s.EstVide && s.ID == Joueur.IdObjetMouleArgile)
			return DureeCuissonBolArgileSec;
		if (!s.EstVide && s.ID == Joueur.IdObjetSteakCru)
			return DureeCuissonSteakSec;
		if (!s.EstVide && s.ID == Joueur.IdObjetBolCeramiqueEtain)
			return DureeFonteEtainSec;
		return DureeCuissonSteakSec;
	}

	/// <summary>0 = argile froide, 1 = incandescent (progression douce 180 °C → 680 °C).</summary>
	public static float ObtenirFacteurTeinteChauffeBolArgile(float tempC, float progressionCuisson01 = 0f)
	{
		float depuisTemp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(180f, 680f, tempC));
		float depuisCuisson = Mathf.Clamp(progressionCuisson01, 0f, 1f);
		return Mathf.Clamp(Mathf.Max(depuisTemp, depuisCuisson * 0.65f), 0f, 1f);
	}

	/// <summary>1 = céramique chaude, 0 = refroidie (progression linéaire).</summary>
	public static float ObtenirFacteurChaleurBolCeramique(float progressionRefroidissement01)
		=> 1f - Mathf.Clamp(progressionRefroidissement01, 0f, 1f);

	public static bool EstFacteurBolAssezChaudPourBruler(float facteurChaleur)
		=> facteurChaleur >= SeuilFacteurBrulureBolChaud;

	public static float ObtenirFacteurChaleurBolCeramiqueSlot(SlotInventaire s)
	{
		if (s.ID == Joueur.IdObjetBolCeramique)
			return ItemPhysique.ObtenirFacteurChaleurBolCeramiqueDepuisSlot(s);
		if (s.ID == Joueur.IdObjetMouleCeramique)
			return ItemPhysique.ObtenirFacteurChaleurMouleCeramiqueDepuisSlot(s);
		if (s.ID == Joueur.IdObjetBolEtainFonduChaud)
			return ItemPhysique.ObtenirFacteurChaleurBolEtainFonduDepuisSlot(s);
		if (s.ID == Joueur.IdObjetBolCeramiqueScorie)
			return ItemPhysique.ObtenirFacteurChaleurBolScorieDepuisSlot(s);
		return 0f;
	}

	public static float ObtenirFacteurChaleurBolEtainFonduSlot(SlotInventaire s)
		=> ObtenirFacteurChaleurBolCeramiqueSlot(s);

	public static bool EstSlotBolBrulant(SlotInventaire s)
		=> EstFacteurBolAssezChaudPourBruler(ObtenirFacteurChaleurBolCeramiqueSlot(s));
}
