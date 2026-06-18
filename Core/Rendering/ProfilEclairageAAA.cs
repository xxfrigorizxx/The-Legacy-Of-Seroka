using Godot;

/// <summary>Profils d'éclairage image (SSAO, SSIL, SDFGI) — qualité AAA avec repli machines faibles.</summary>
public enum QualiteEclairageAaa
{
	Faible = 0,
	Moyen = 1,
	Eleve = 2,
	Ultra = 3
}

public static class ProfilEclairageAAA
{
	/// <summary>
	/// Applique un profil image sur l'Environment monde (surface) : SSAO (contact), SSIL (rebond léger).
	/// SDFGI volontairement OFF (il fuit à travers la roche → grottes faussement éclairées).
	/// N'écrit PAS l'ambiance (AmbientLightEnergy / SkyContribution) : c'est Cycle_Solaire qui la pilote chaque frame.
	/// </summary>
	public static void Appliquer(Environment env, QualiteEclairageAaa qualite)
	{
		if (env == null)
			return;

		// Base commune : tonemap ACES, pas de brouillard volumétrique (calculé le long du regard → instable).
		env.TonemapMode = Environment.ToneMapper.Aces;
		env.TonemapExposure = 1.05f;
		// Pas d'ajustement de contraste : il écrase les zones d'ombre en noir total (terrain noir dehors).
		env.AdjustmentEnabled = false;
		env.VolumetricFogEnabled = false;
		env.GlowEnabled = false;

		// SDFGI off pour toutes les qualités : incompatible avec des grottes voxel sombres.
		env.SdfgiEnabled = false;
		env.SsaoEnabled = false;
		env.SsilEnabled = false;

		switch (qualite)
		{
			case QualiteEclairageAaa.Faible:
				// Aucun effet écran : performances maximales. Ombres directionnelles seules.
				break;

			case QualiteEclairageAaa.Moyen:
				env.SsaoEnabled = true;
				env.SsaoRadius = 1.2f;
				env.SsaoIntensity = 0.85f;
				env.SsaoPower = 1.4f;
				env.SsaoDetail = 0.45f;
				env.SsaoHorizon = 0.06f;
				break;

			case QualiteEclairageAaa.Eleve:
				env.SsaoEnabled = true;
				env.SsilEnabled = true;
				env.SsaoRadius = 1.6f;
				env.SsaoIntensity = 1.05f;
				env.SsaoPower = 1.25f;
				env.SsaoDetail = 0.55f;
				env.SsilRadius = 4.5f;
				env.SsilIntensity = 0.28f;
				env.SsilSharpness = 0.88f;
				break;

			case QualiteEclairageAaa.Ultra:
			default:
				env.SsaoEnabled = true;
				env.SsilEnabled = true;
				env.SsaoRadius = 2.0f;
				env.SsaoIntensity = 1.15f;
				env.SsaoPower = 1.15f;
				env.SsaoDetail = 0.62f;
				env.SsaoHorizon = 0.04f;
				env.SsilRadius = 5.5f;
				env.SsilIntensity = 0.32f;
				env.SsilSharpness = 0.92f;
				break;
		}
	}

	public static QualiteEclairageAaa DepuisPresetGraphique(PresetGraphique preset) => preset switch
	{
		PresetGraphique.Faible => QualiteEclairageAaa.Faible,
		PresetGraphique.Moyen => QualiteEclairageAaa.Moyen,
		PresetGraphique.Eleve => QualiteEclairageAaa.Eleve,
		PresetGraphique.Ultra => QualiteEclairageAaa.Ultra,
		_ => QualiteEclairageAaa.Ultra
	};
}
