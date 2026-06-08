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
	public const double DureeCuissonSteakSec = 45.0;
	public const double DureeCuissonBolArgileSec = 150.0;
	public const double DureeRefroidissementBolCeramiqueSec = 60.0;
	public const float SeuilFacteurBrulureBolChaud = 0.12f;

	private const float FacteurMonteeThermique = 0.38f;

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

	public static bool EstSlotVoxelArgileEchecFour(SlotInventaire s) =>
		!s.EstVide && (s.ID == 8 || (Atlas_Matiere.EssayerLireIdVoxelTerrain(s, out int v) && v == 8));

	public static bool TemperatureDansPlageCuissonBolArgile(float tempC) =>
		tempC >= SeuilCuissonBolArgileMinC && tempC <= SeuilCuissonBolArgileMaxC;

	public static double ObtenirDureeCuissonFourPourSlot(SlotInventaire s)
	{
		if (!s.EstVide && s.ID == Joueur.IdObjetBolArgile)
			return DureeCuissonBolArgileSec;
		if (!s.EstVide && s.ID == Joueur.IdObjetSteakCru)
			return DureeCuissonSteakSec;
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
		=> ItemPhysique.ObtenirFacteurChaleurBolCeramiqueDepuisSlot(s);

	public static bool EstSlotBolBrulant(SlotInventaire s)
		=> EstFacteurBolAssezChaudPourBruler(ObtenirFacteurChaleurBolCeramiqueSlot(s));
}
