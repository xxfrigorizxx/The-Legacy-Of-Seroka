using Godot;
using System;

public enum PresetGraphique
{
	Faible = 0,
	Moyen = 1,
	Eleve = 2,
	Ultra = 3,
	Personnalise = 4
}

public sealed class GraphicsOptionsData
{
	public PresetGraphique Preset = PresetGraphique.Personnalise;
	public int RenderDistance = 14;
	public int RenderDistanceDetailChunks = 10;
	public int RayonQualiteProcheChunks = 5;
	public int RayonGazonVisibleChunks = 9;
	public int RayonBuissonsVisibleChunks = 16;
	public bool ActiverHorizonLod;
	public int RayonHorizonChunks = 72;
	public float PasHorizonMetres = 20f;
	public bool ActiverCullingCameraChunks = true;
	public float AngleCullingCameraDeg = 135f;
	public int MargeChunksToujoursVisibles = 8;
	public int MaxChunksParFrame = 14;
	public int LODTextureEtapes = 12;
	public bool ProfilLodCinematiqueUltraSmooth = true;
	public bool ModeSurvieFpsAgressif = true;
	public int FpsCibleAutoDiagnostic = 60;
	public int SeuilFpsUrgenceForte = 42;
	public int SeuilFpsUrgenceCritique = 30;
	public int SeuilFpsUrgenceExtreme = 24;
	public int SeuilFpsSortieUrgenceExtreme = 56;

	public GraphicsOptionsData Clone()
	{
		return (GraphicsOptionsData)MemberwiseClone();
	}
}

public static class GraphicsOptionsService
{
	private const string SectionOptions = "graphics";
	private const string CheminConfig = "user://options_graphics.cfg";

	public static GraphicsOptionsData Normaliser(GraphicsOptionsData options)
	{
		if (options == null)
			return new GraphicsOptionsData();

		options.RenderDistance = Mathf.Clamp(options.RenderDistance, 2, 64);
		options.RenderDistanceDetailChunks = Mathf.Clamp(options.RenderDistanceDetailChunks, 2, options.RenderDistance);
		options.RayonQualiteProcheChunks = Mathf.Clamp(options.RayonQualiteProcheChunks, 1, 24);
		options.RayonGazonVisibleChunks = Mathf.Clamp(options.RayonGazonVisibleChunks, 1, 24);
		options.RayonBuissonsVisibleChunks = Mathf.Clamp(options.RayonBuissonsVisibleChunks, 2, 32);
		options.RayonHorizonChunks = Mathf.Clamp(options.RayonHorizonChunks, 24, 240);
		options.PasHorizonMetres = Mathf.Clamp(options.PasHorizonMetres, 12f, 80f);
		options.AngleCullingCameraDeg = Mathf.Clamp(options.AngleCullingCameraDeg, 80f, 175f);
		options.MargeChunksToujoursVisibles = Mathf.Clamp(options.MargeChunksToujoursVisibles, 1, 32);
		options.MaxChunksParFrame = Mathf.Clamp(options.MaxChunksParFrame, 2, 40);
		options.LODTextureEtapes = Mathf.Clamp(options.LODTextureEtapes, 8, 24);
		options.FpsCibleAutoDiagnostic = Mathf.Clamp(options.FpsCibleAutoDiagnostic, 45, 144);
		options.SeuilFpsUrgenceForte = Mathf.Clamp(options.SeuilFpsUrgenceForte, 20, 59);
		options.SeuilFpsUrgenceCritique = Mathf.Clamp(options.SeuilFpsUrgenceCritique, 15, options.SeuilFpsUrgenceForte - 1);
		options.SeuilFpsUrgenceExtreme = Mathf.Clamp(options.SeuilFpsUrgenceExtreme, 10, options.SeuilFpsUrgenceCritique);
		options.SeuilFpsSortieUrgenceExtreme = Mathf.Clamp(options.SeuilFpsSortieUrgenceExtreme, options.SeuilFpsUrgenceForte, 90);
		return options;
	}

	public static GraphicsOptionsData ChargerOuDefaut(GraphicsOptionsData defaut)
	{
		GraphicsOptionsData baseOptions = Normaliser((defaut ?? new GraphicsOptionsData()).Clone());
		var config = new ConfigFile();
		Error err = config.Load(CheminConfig);
		if (err != Error.Ok)
			return baseOptions;

		baseOptions.Preset = (PresetGraphique)(int)config.GetValue(SectionOptions, "preset", (int)baseOptions.Preset);
		baseOptions.RenderDistance = (int)config.GetValue(SectionOptions, "render_distance", baseOptions.RenderDistance);
		baseOptions.RenderDistanceDetailChunks = (int)config.GetValue(SectionOptions, "render_distance_detail", baseOptions.RenderDistanceDetailChunks);
		baseOptions.RayonQualiteProcheChunks = (int)config.GetValue(SectionOptions, "rayon_qualite_proche", baseOptions.RayonQualiteProcheChunks);
		baseOptions.RayonGazonVisibleChunks = (int)config.GetValue(SectionOptions, "rayon_gazon", baseOptions.RayonGazonVisibleChunks);
		baseOptions.RayonBuissonsVisibleChunks = (int)config.GetValue(SectionOptions, "rayon_buissons", baseOptions.RayonBuissonsVisibleChunks);
		baseOptions.ActiverHorizonLod = (bool)config.GetValue(SectionOptions, "horizon_actif", baseOptions.ActiverHorizonLod);
		baseOptions.RayonHorizonChunks = (int)config.GetValue(SectionOptions, "horizon_rayon", baseOptions.RayonHorizonChunks);
		baseOptions.PasHorizonMetres = (float)(double)config.GetValue(SectionOptions, "horizon_pas", baseOptions.PasHorizonMetres);
		baseOptions.ActiverCullingCameraChunks = (bool)config.GetValue(SectionOptions, "culling_actif", baseOptions.ActiverCullingCameraChunks);
		baseOptions.AngleCullingCameraDeg = (float)(double)config.GetValue(SectionOptions, "culling_angle", baseOptions.AngleCullingCameraDeg);
		baseOptions.MargeChunksToujoursVisibles = (int)config.GetValue(SectionOptions, "culling_marge_visible", baseOptions.MargeChunksToujoursVisibles);
		baseOptions.MaxChunksParFrame = (int)config.GetValue(SectionOptions, "max_chunks_par_frame", baseOptions.MaxChunksParFrame);
		baseOptions.LODTextureEtapes = (int)config.GetValue(SectionOptions, "lod_etapes", baseOptions.LODTextureEtapes);
		baseOptions.ProfilLodCinematiqueUltraSmooth = (bool)config.GetValue(SectionOptions, "lod_ultra_smooth", baseOptions.ProfilLodCinematiqueUltraSmooth);
		baseOptions.ModeSurvieFpsAgressif = (bool)config.GetValue(SectionOptions, "fps_survie_agressive", baseOptions.ModeSurvieFpsAgressif);
		baseOptions.FpsCibleAutoDiagnostic = (int)config.GetValue(SectionOptions, "fps_cible", baseOptions.FpsCibleAutoDiagnostic);
		baseOptions.SeuilFpsUrgenceForte = (int)config.GetValue(SectionOptions, "fps_urgence_forte", baseOptions.SeuilFpsUrgenceForte);
		baseOptions.SeuilFpsUrgenceCritique = (int)config.GetValue(SectionOptions, "fps_urgence_critique", baseOptions.SeuilFpsUrgenceCritique);
		baseOptions.SeuilFpsUrgenceExtreme = (int)config.GetValue(SectionOptions, "fps_urgence_extreme", baseOptions.SeuilFpsUrgenceExtreme);
		baseOptions.SeuilFpsSortieUrgenceExtreme = (int)config.GetValue(SectionOptions, "fps_sortie_extreme", baseOptions.SeuilFpsSortieUrgenceExtreme);
		return Normaliser(baseOptions);
	}

	public static void Sauvegarder(GraphicsOptionsData options)
	{
		GraphicsOptionsData safe = Normaliser((options ?? new GraphicsOptionsData()).Clone());
		var config = new ConfigFile();
		config.SetValue(SectionOptions, "preset", (int)safe.Preset);
		config.SetValue(SectionOptions, "render_distance", safe.RenderDistance);
		config.SetValue(SectionOptions, "render_distance_detail", safe.RenderDistanceDetailChunks);
		config.SetValue(SectionOptions, "rayon_qualite_proche", safe.RayonQualiteProcheChunks);
		config.SetValue(SectionOptions, "rayon_gazon", safe.RayonGazonVisibleChunks);
		config.SetValue(SectionOptions, "rayon_buissons", safe.RayonBuissonsVisibleChunks);
		config.SetValue(SectionOptions, "horizon_actif", safe.ActiverHorizonLod);
		config.SetValue(SectionOptions, "horizon_rayon", safe.RayonHorizonChunks);
		config.SetValue(SectionOptions, "horizon_pas", safe.PasHorizonMetres);
		config.SetValue(SectionOptions, "culling_actif", safe.ActiverCullingCameraChunks);
		config.SetValue(SectionOptions, "culling_angle", safe.AngleCullingCameraDeg);
		config.SetValue(SectionOptions, "culling_marge_visible", safe.MargeChunksToujoursVisibles);
		config.SetValue(SectionOptions, "max_chunks_par_frame", safe.MaxChunksParFrame);
		config.SetValue(SectionOptions, "lod_etapes", safe.LODTextureEtapes);
		config.SetValue(SectionOptions, "lod_ultra_smooth", safe.ProfilLodCinematiqueUltraSmooth);
		config.SetValue(SectionOptions, "fps_survie_agressive", safe.ModeSurvieFpsAgressif);
		config.SetValue(SectionOptions, "fps_cible", safe.FpsCibleAutoDiagnostic);
		config.SetValue(SectionOptions, "fps_urgence_forte", safe.SeuilFpsUrgenceForte);
		config.SetValue(SectionOptions, "fps_urgence_critique", safe.SeuilFpsUrgenceCritique);
		config.SetValue(SectionOptions, "fps_urgence_extreme", safe.SeuilFpsUrgenceExtreme);
		config.SetValue(SectionOptions, "fps_sortie_extreme", safe.SeuilFpsSortieUrgenceExtreme);
		Error err = config.Save(CheminConfig);
		if (err != Error.Ok)
			GD.PrintErr($"ZERO-K : Impossible de sauvegarder {CheminConfig} ({err}).");
	}

	public static GraphicsOptionsData ConstruirePreset(PresetGraphique preset, GraphicsOptionsData baseOptions)
	{
		GraphicsOptionsData source = Normaliser((baseOptions ?? new GraphicsOptionsData()).Clone());
		GraphicsOptionsData p = source.Clone();
		p.Preset = preset;
		switch (preset)
		{
			case PresetGraphique.Faible:
				p.RenderDistance = 10;
				p.RenderDistanceDetailChunks = 7;
				p.RayonQualiteProcheChunks = 3;
				p.RayonGazonVisibleChunks = 4;
				p.RayonBuissonsVisibleChunks = 7;
				p.ActiverHorizonLod = false;
				p.RayonHorizonChunks = 40;
				p.PasHorizonMetres = 42f;
				p.ActiverCullingCameraChunks = true;
				p.AngleCullingCameraDeg = 118f;
				p.MargeChunksToujoursVisibles = 5;
				p.MaxChunksParFrame = 8;
				p.LODTextureEtapes = 9;
				p.ProfilLodCinematiqueUltraSmooth = false;
				p.ModeSurvieFpsAgressif = true;
				p.FpsCibleAutoDiagnostic = 58;
				p.SeuilFpsUrgenceForte = 44;
				p.SeuilFpsUrgenceCritique = 32;
				p.SeuilFpsUrgenceExtreme = 25;
				p.SeuilFpsSortieUrgenceExtreme = 55;
				break;
			case PresetGraphique.Moyen:
				p.RenderDistance = 14;
				p.RenderDistanceDetailChunks = 10;
				p.RayonQualiteProcheChunks = 5;
				p.RayonGazonVisibleChunks = 8;
				p.RayonBuissonsVisibleChunks = 12;
				p.ActiverHorizonLod = true;
				p.RayonHorizonChunks = 64;
				p.PasHorizonMetres = 28f;
				p.ActiverCullingCameraChunks = true;
				p.AngleCullingCameraDeg = 132f;
				p.MargeChunksToujoursVisibles = 8;
				p.MaxChunksParFrame = 12;
				p.LODTextureEtapes = 12;
				p.ProfilLodCinematiqueUltraSmooth = true;
				p.ModeSurvieFpsAgressif = true;
				p.FpsCibleAutoDiagnostic = 60;
				p.SeuilFpsUrgenceForte = 42;
				p.SeuilFpsUrgenceCritique = 30;
				p.SeuilFpsUrgenceExtreme = 24;
				p.SeuilFpsSortieUrgenceExtreme = 56;
				break;
			case PresetGraphique.Eleve:
				p.RenderDistance = 18;
				p.RenderDistanceDetailChunks = 13;
				p.RayonQualiteProcheChunks = 7;
				p.RayonGazonVisibleChunks = 10;
				p.RayonBuissonsVisibleChunks = 16;
				p.ActiverHorizonLod = true;
				p.RayonHorizonChunks = 90;
				p.PasHorizonMetres = 20f;
				p.ActiverCullingCameraChunks = true;
				p.AngleCullingCameraDeg = 142f;
				p.MargeChunksToujoursVisibles = 10;
				p.MaxChunksParFrame = 16;
				p.LODTextureEtapes = 14;
				p.ProfilLodCinematiqueUltraSmooth = true;
				p.ModeSurvieFpsAgressif = true;
				p.FpsCibleAutoDiagnostic = 62;
				p.SeuilFpsUrgenceForte = 45;
				p.SeuilFpsUrgenceCritique = 33;
				p.SeuilFpsUrgenceExtreme = 26;
				p.SeuilFpsSortieUrgenceExtreme = 58;
				break;
			case PresetGraphique.Ultra:
				p.RenderDistance = 24;
				p.RenderDistanceDetailChunks = 18;
				p.RayonQualiteProcheChunks = 10;
				p.RayonGazonVisibleChunks = 13;
				p.RayonBuissonsVisibleChunks = 20;
				p.ActiverHorizonLod = true;
				p.RayonHorizonChunks = 120;
				p.PasHorizonMetres = 16f;
				p.ActiverCullingCameraChunks = true;
				p.AngleCullingCameraDeg = 150f;
				p.MargeChunksToujoursVisibles = 12;
				p.MaxChunksParFrame = 20;
				p.LODTextureEtapes = 18;
				p.ProfilLodCinematiqueUltraSmooth = true;
				p.ModeSurvieFpsAgressif = false;
				p.FpsCibleAutoDiagnostic = 60;
				p.SeuilFpsUrgenceForte = 40;
				p.SeuilFpsUrgenceCritique = 28;
				p.SeuilFpsUrgenceExtreme = 22;
				p.SeuilFpsSortieUrgenceExtreme = 54;
				break;
			default:
				p.Preset = PresetGraphique.Personnalise;
				break;
		}
		// À distance de rendu élevée, l’horizon LOD évite le vide noir pendant le streaming (Faible reste souvent sous 18 chunks).
		if (p.RenderDistance >= 18)
			p.ActiverHorizonLod = true;
		return Normaliser(p);
	}

	public static GraphicsOptionsData GenererBaseAutoMateriel(string nomCpu, string nomGpu, GraphicsOptionsData baseOptions)
	{
		PresetGraphique preset = DeterminerPresetMateriel(nomCpu, nomGpu);
		return ConstruirePreset(preset, baseOptions);
	}

	public static PresetGraphique DeterminerPresetMateriel(string nomCpu, string nomGpu)
	{
		string cpu = (nomCpu ?? "").ToLowerInvariant();
		string gpu = (nomGpu ?? "").ToLowerInvariant();
		if (gpu.Contains("4090") || gpu.Contains("4080") || gpu.Contains("7900 xtx") || gpu.Contains("rx 7900") || gpu.Contains("rtx 50"))
			return PresetGraphique.Ultra;
		if (gpu.Contains("4070") || gpu.Contains("3080") || gpu.Contains("3070") || gpu.Contains("7800 xt") || gpu.Contains("6800 xt"))
			return PresetGraphique.Eleve;
		if (gpu.Contains("1060") || gpu.Contains("1660") || gpu.Contains("2060") || gpu.Contains("3060") || gpu.Contains("rx 580") || gpu.Contains("rx 6600"))
			return PresetGraphique.Moyen;
		if (gpu.Contains("1050") || gpu.Contains("960") || gpu.Contains("mx") || gpu.Contains("vega") || gpu.Contains("uhd") || gpu.Contains("radeon(tm) graphics"))
			return PresetGraphique.Faible;
		if (cpu.Contains("i3") || cpu.Contains("ryzen 3") || cpu.Contains("athlon") || cpu.Contains("pentium"))
			return PresetGraphique.Faible;
		if (cpu.Contains("i5") || cpu.Contains("ryzen 5"))
			return PresetGraphique.Moyen;
		if (cpu.Contains("i7") || cpu.Contains("ryzen 7"))
			return PresetGraphique.Eleve;
		if (cpu.Contains("i9") || cpu.Contains("ryzen 9"))
			return PresetGraphique.Ultra;
		return PresetGraphique.Moyen;
	}

	public static GraphicsOptionsData AjusterSelonFps(GraphicsOptionsData courant, float fpsMoyen, float fpsMin)
	{
		GraphicsOptionsData next = Normaliser((courant ?? new GraphicsOptionsData()).Clone());
		bool degrade = fpsMoyen < 52f || fpsMin < 40f;
		bool upgrade = fpsMoyen > 78f && fpsMin > 60f;
		if (!degrade && !upgrade)
			return next;

		if (degrade)
		{
			next.RenderDistance = Mathf.Max(8, next.RenderDistance - 1);
			next.RenderDistanceDetailChunks = Mathf.Max(6, next.RenderDistanceDetailChunks - 1);
			next.RayonGazonVisibleChunks = Mathf.Max(2, next.RayonGazonVisibleChunks - 1);
			next.RayonBuissonsVisibleChunks = Mathf.Max(4, next.RayonBuissonsVisibleChunks - 1);
			next.RayonHorizonChunks = Mathf.Max(32, next.RayonHorizonChunks - 4);
			next.MaxChunksParFrame = Mathf.Max(5, next.MaxChunksParFrame - 1);
			if (fpsMin < 35f)
				next.ActiverHorizonLod = false;
		}
		else if (upgrade)
		{
			next.RenderDistance = Mathf.Min(26, next.RenderDistance + 1);
			next.RenderDistanceDetailChunks = Mathf.Min(next.RenderDistance, next.RenderDistanceDetailChunks + 1);
			next.RayonGazonVisibleChunks = Mathf.Min(16, next.RayonGazonVisibleChunks + 1);
			next.RayonBuissonsVisibleChunks = Mathf.Min(24, next.RayonBuissonsVisibleChunks + 1);
			next.RayonHorizonChunks = Mathf.Min(128, next.RayonHorizonChunks + 4);
			next.MaxChunksParFrame = Mathf.Min(24, next.MaxChunksParFrame + 1);
			next.ActiverHorizonLod = true;
		}

		next.Preset = PresetGraphique.Personnalise;
		return Normaliser(next);
	}
}
