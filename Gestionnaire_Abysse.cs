using Godot;
using System.Collections.Generic;

/// <summary>Variante serveur dédiée à APISARA (<see cref="ConstantesDimensionAbysse.Apisara"/>), identifiant technique <see cref="DimensionJeu.Abysse"/>.</summary>
public partial class Gestionnaire_Abysse : Monde_Serveur
{
	private WorldEnvironment _worldEnvironmentAbysse;
	private Godot.Environment _environmentAbysse;
	private Node3D _racineFogVolumesAbysse;
	private readonly List<FogVolume> _fogVolumesStrates = new List<FogVolume>();
	private FogVolume _fogVolumeScellementFond;
	private bool _atmosphereAbysseActive;
	private int _renderDistanceBrouillardChunks = 10;
	private int _tailleChunkBrouillard = 16;
	private int _avanceBrouillardChunks = 2;

	public override void _Ready()
	{
		base._Ready();
		NomDimension = ConstantesDimensionAbysse.Apisara;
		ActiverGenerationAbysse = true;
		InitialiserAtmosphereAbysseNative();
		DefinirAtmosphereAbysseActive(false);
	}

	/// <summary>Ressource d’ambiance partagée (ciel bleu APISARA, brouillard profondeur, volumétrique léger). Assignée au <c>WorldEnvironment</c> racine de la scène pour éviter un second WE en conflit.</summary>
	public Godot.Environment ObtenirEnvironmentAbysse() => _environmentAbysse;

	public void DefinirAtmosphereAbysseActive(bool actif)
	{
		_atmosphereAbysseActive = actif;
		if (actif)
			AppliquerDistanceBrouillardProgressive();
		// Ne pas attacher l’Environment ici : le rendu n’utilise qu’un seul WE actif ; l’assignation se fait sur le nœud racine (voir Gestionnaire_Monde).
		if (_worldEnvironmentAbysse != null && GodotObject.IsInstanceValid(_worldEnvironmentAbysse))
			_worldEnvironmentAbysse.Environment = null;
		if (_racineFogVolumesAbysse != null && GodotObject.IsInstanceValid(_racineFogVolumesAbysse))
			_racineFogVolumesAbysse.Visible = actif;
		for (int i = _fogVolumesStrates.Count - 1; i >= 0; i--)
		{
			FogVolume fog = _fogVolumesStrates[i];
			if (fog == null || !GodotObject.IsInstanceValid(fog))
			{
				_fogVolumesStrates.RemoveAt(i);
				continue;
			}
			fog.Visible = actif;
		}
		if (_fogVolumeScellementFond != null && GodotObject.IsInstanceValid(_fogVolumeScellementFond))
			_fogVolumeScellementFond.Visible = actif;
	}

	public void ConfigurerDistanceBrouillardProgressive(int renderDistanceChunks, int tailleChunk, int avanceChunks = 2)
	{
		_renderDistanceBrouillardChunks = Mathf.Max(1, renderDistanceChunks);
		_tailleChunkBrouillard = Mathf.Max(1, tailleChunk);
		_avanceBrouillardChunks = Mathf.Max(0, avanceChunks);
		AppliquerDistanceBrouillardProgressive();
	}

	private void AppliquerDistanceBrouillardProgressive()
	{
		if (_environmentAbysse == null)
			return;

		float tailleChunkMetres = Mathf.Max(1f, _tailleChunkBrouillard);
		int chunkDebut = Mathf.Max(1, _renderDistanceBrouillardChunks - _avanceBrouillardChunks);
		float debut = chunkDebut * tailleChunkMetres;
		float fin = (_renderDistanceBrouillardChunks + 2) * tailleChunkMetres;

		// Même logique que l'Alpha: brouillard profondeur adossé à la distance de rendu.
		_environmentAbysse.FogEnabled = true;
		_environmentAbysse.FogMode = Environment.FogModeEnum.Depth;
		_environmentAbysse.FogDepthBegin = debut;
		_environmentAbysse.FogDepthEnd = Mathf.Max(debut + tailleChunkMetres, fin);
		_environmentAbysse.FogDepthCurve = 1.15f;
	}

	private void InitialiserAtmosphereAbysseNative()
	{
		_worldEnvironmentAbysse = GetNodeOrNull<WorldEnvironment>("WorldEnvironment_Abysse");
		if (_worldEnvironmentAbysse == null)
		{
			_worldEnvironmentAbysse = new WorldEnvironment { Name = "WorldEnvironment_Abysse" };
			AddChild(_worldEnvironmentAbysse);
		}

		_environmentAbysse = _worldEnvironmentAbysse.Environment ?? new Godot.Environment();
		Sky skyAbysse = _environmentAbysse.Sky ?? new Sky();
		ProceduralSkyMaterial skyMateriauAbysse = skyAbysse.SkyMaterial as ProceduralSkyMaterial ?? new ProceduralSkyMaterial();
		// Ciel APISARA : bleu clair type jour (zénith saturé, horizon plus lumineux). Les strates « nuages » par palier
		// restent inchangées : elles sont des FogVolume dans ConstruireStratesFogVolume (non modifié ici).
		skyMateriauAbysse.SkyTopColor = new Color(0.18f, 0.48f, 0.92f, 1f);
		skyMateriauAbysse.SkyHorizonColor = new Color(0.45f, 0.72f, 0.98f, 1f);
		skyMateriauAbysse.GroundHorizonColor = new Color(0.35f, 0.62f, 0.90f, 1f);
		skyMateriauAbysse.GroundBottomColor = new Color(0.12f, 0.35f, 0.62f, 1f);
		skyMateriauAbysse.EnergyMultiplier = 1.05f;
		skyAbysse.SkyMaterial = skyMateriauAbysse;
		_environmentAbysse.Sky = skyAbysse;
		_environmentAbysse.BackgroundMode = Environment.BGMode.Sky;

		_environmentAbysse.Set("volumetric_fog_enabled", true);
		_environmentAbysse.Set("volumetric_fog_density", 0.0002f);
		_environmentAbysse.Set("volumetric_fog_albedo", new Color(0.42f, 0.54f, 0.66f, 1f));
		_environmentAbysse.Set("volumetric_fog_emission", new Color(0.00f, 0.00f, 0.00f, 1f));
		_environmentAbysse.Set("volumetric_fog_length", 2600f);
		_environmentAbysse.Set("volumetric_fog_detail_spread", 4.0f);
		_environmentAbysse.Set("volumetric_fog_ambient_inject", 0.08f);
		AppliquerDistanceBrouillardProgressive();
		_worldEnvironmentAbysse.Environment = null;
		_worldEnvironmentAbysse.ProcessMode = ProcessModeEnum.Disabled;

		_racineFogVolumesAbysse = GetNodeOrNull<Node3D>("FogVolumes_Abysse");
		if (_racineFogVolumesAbysse == null)
		{
			_racineFogVolumesAbysse = new Node3D { Name = "FogVolumes_Abysse" };
			AddChild(_racineFogVolumesAbysse);
		}
		ConstruireStratesFogVolume();
		ConstruireFogVolumeScellementFond();
	}

	private void ConstruireStratesFogVolume()
	{
		for (int i = 0; i < _fogVolumesStrates.Count; i++)
		{
			if (_fogVolumesStrates[i] != null && GodotObject.IsInstanceValid(_fogVolumesStrates[i]))
				_fogVolumesStrates[i].QueueFree();
		}
		_fogVolumesStrates.Clear();
		if (_racineFogVolumesAbysse == null || !GodotObject.IsInstanceValid(_racineFogVolumesAbysse))
			return;

		float taillePalier = Mathf.Max(1f, ConstantesDimensionAbysse.TaillePalierMetres);
		float profondeurAbsolue = Mathf.Abs(ConstantesDimensionAbysse.FondAbsolu);
		int nombreStratesVersBas = Mathf.Max(1, Mathf.CeilToInt(profondeurAbsolue / taillePalier) + 1);
		const float epaisseurStrateMetres = 12f;
		const float decalageCentreSousPalierMetres = 6f;
		for (int i = 0; i < nombreStratesVersBas; i++)
		{
			float yMonde = -i * taillePalier;
			FogVolume volume = new FogVolume
			{
				Name = $"FogVolumePalier_{i}",
				Visible = _atmosphereAbysseActive
			};
			volume.Set("shape", 0); // Box
			// Rebord du trou: on remonte à une emprise 1000x1000 (rayon 500 sur les axes).
			// Verticalement, la strate est uniquement SOUS le palier: [yPalier, yPalier-12].
			volume.Set("size", new Vector3(1000f, epaisseurStrateMetres, 1000f));
			volume.Position = new Vector3(0f, yMonde - decalageCentreSousPalierMetres, 0f);

			FogMaterial materiau = new FogMaterial();
			materiau.Set("density", 1.9f);
			materiau.Set("albedo", new Color(0.97f, 0.98f, 0.99f, 1f));
			materiau.Set("emission", new Color(0.62f, 0.66f, 0.70f, 1f));
			volume.Material = materiau;

			_racineFogVolumesAbysse.AddChild(volume);
			_fogVolumesStrates.Add(volume);
		}
	}

	private void ConstruireFogVolumeScellementFond()
	{
		if (_racineFogVolumesAbysse == null || !GodotObject.IsInstanceValid(_racineFogVolumesAbysse))
			return;
		if (_fogVolumeScellementFond != null && GodotObject.IsInstanceValid(_fogVolumeScellementFond))
			_fogVolumeScellementFond.QueueFree();

		_fogVolumeScellementFond = new FogVolume
		{
			Name = "FogVolumeScellementFond",
			Visible = _atmosphereAbysseActive
		};
		_fogVolumeScellementFond.Set("shape", 0); // Box
		_fogVolumeScellementFond.Set("size", new Vector3(1000f, 900f, 1000f));
		_fogVolumeScellementFond.Position = new Vector3(0f, -2000f, 0f);

		FogMaterial materiauFond = new FogMaterial();
		materiauFond.Set("density", 20.0f);
		materiauFond.Set("albedo", new Color(0.00f, 0.03f, 0.05f, 1f));
		materiauFond.Set("emission", new Color(0.00f, 0.00f, 0.00f, 1f));
		_fogVolumeScellementFond.Material = materiauFond;

		_racineFogVolumesAbysse.AddChild(_fogVolumeScellementFond);
	}
}
