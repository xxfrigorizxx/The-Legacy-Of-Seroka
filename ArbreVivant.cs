using Godot;
using System;
using System.Collections.Generic;

/// <summary>Entité 3D d'arbre procédurale (L-System volumétrique). Branches continues, feuillage, croissance temporelle.</summary>
/// <remarks>Hérite de StaticBody3D. Remplaçant des arbres voxels.</remarks>
public partial class ArbreVivant : StaticBody3D
{
	private List<Vector3> _coupesLocales = new List<Vector3>();

	private struct TortueEtat
	{
		public Transform3D Transform;
		public float Epaisseur;
		public bool EstCoupe;
	}

	public int AgeEnJours = 1;
	public float ResistanceActuelle = 53.5f;

	/// <summary>PV max du fût vivant : croît linéairement + quadratiquement avec l’âge (grands arbres beaucoup plus tenaces).</summary>
	public static float ResistanceMaxPourAge(int ageEnJours)
	{
		ageEnJours = Mathf.Max(1, ageEnJours);
		return 50f * ageEnJours + 3.5f * ageEnJours * ageEnJours;
	}
	/// <summary>Graine pour variabilité des angles/longueurs (évite arbres identiques).</summary>
	public uint Seed = 12345;
	public byte IndexBotanique = 0;
	private const float CHANCE_CROISSANCE = 0.05f; // 1 chance sur 20 de grandir chaque nuit

	/// <summary>Dimensions réelles du tronc (remplis par GenererMaillageArbre). Utilisées pour la bûche et les bâtons au démembrement.</summary>
	private float _hauteurTroncTotale;
	private float _rayonTroncBase;
	private float _rayonTroncSommet;
	private float _longueurBrancheMoyenne;
	private float _epaisseurBrancheMoyenne;
	private float _progressionLianesJungle = 1f; // 1 = pleine longueur, diminue après coupe puis repousse.

	private MeshInstance3D _visuelBois;
	private MeshInstance3D _visuelFeuillage;
	private CollisionShape3D _hitbox;
	private Node3D _observationRef;
	private int _lodActuel = -1;
	private bool _maillageInitialGenere;
	private float _attenteGeneration;
	private float _cooldownLod;
	private uint _seedEffectif;
	private float _lodFeuillageActuel = 1f;

	private const float DISTANCE_LOD0 = 130f;
	private const float DISTANCE_LOD1 = 280f;
	private const float DISTANCE_LOD2 = 520f;
	private const float INTERVALLE_MAJ_LOD = 0.75f;
	private const int BUDGET_GENERATION_INIT_PAR_FRAME = 2;
	private static ulong _frameBudgetGenerationInit;
	private static int _resteBudgetGenerationInit;

	private static StandardMaterial3D _cacheMatBois;
	private static StandardMaterial3D _cacheMatBoisTriplanar;
	private static StandardMaterial3D _cacheMatBoisPin;
	private static StandardMaterial3D _cacheMatBoisTriplanarPin;
	private static StandardMaterial3D _cacheMatBoisSapin;
	private static StandardMaterial3D _cacheMatBoisTriplanarSapin;
	private static StandardMaterial3D _cacheMatBoisJungle;
	private static StandardMaterial3D _cacheMatBoisTriplanarJungle;
	private static StandardMaterial3D _cacheMatBoisBouleau;
	private static StandardMaterial3D _cacheMatBoisTriplanarBouleau;
	private static StandardMaterial3D _cacheMatBoisBatonChenEPale;
	private static StandardMaterial3D _cacheMatFeuillesCaduc;
	private static StandardMaterial3D _cacheMatFeuillesPin;
	private static StandardMaterial3D _cacheMatFeuillesJungle;
	private static Texture2D _cacheTextureFeuilleCaduc;
	private static Texture2D _cacheTextureFeuilleJungle;
	private static readonly Color CouleurLianeJungle = new Color(0.18f, 0.34f, 0.16f);
	private uint SeedForme => _seedEffectif != 0 ? _seedEffectif : Seed;

	public static Material ObtenirMaterielBois(byte indexBotanique = 0)
	{
		if (indexBotanique == LSystem_Botanique.IndexPin)
		{
			if (_cacheMatBoisPin != null) return _cacheMatBoisPin;
			var bruit = new FastNoiseLite { Seed = 999, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.1f };
			var tex = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruit };
			var ramp = new Gradient();
			ramp.AddPoint(0.0f, new Color(0.11f, 0.08f, 0.07f)); // Ecorce sombre
			ramp.AddPoint(1.0f, new Color(0.24f, 0.18f, 0.15f));
			tex.ColorRamp = ramp;
			_cacheMatBoisPin = new StandardMaterial3D
			{
				AlbedoTexture = tex,
				AlbedoColor = new Color(0.26f, 0.19f, 0.15f),
				Roughness = 0.96f
			};
			return _cacheMatBoisPin;
		}

		if (indexBotanique == LSystem_Botanique.IndexSapin)
		{
			if (_cacheMatBoisSapin != null) return _cacheMatBoisSapin;
			var bruit = new FastNoiseLite { Seed = 1337, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.09f };
			var tex = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruit };
			var ramp = new Gradient();
			ramp.AddPoint(0.0f, new Color(0.10f, 0.08f, 0.07f));
			ramp.AddPoint(1.0f, new Color(0.27f, 0.21f, 0.17f));
			tex.ColorRamp = ramp;
			_cacheMatBoisSapin = new StandardMaterial3D
			{
				AlbedoTexture = tex,
				AlbedoColor = new Color(0.30f, 0.22f, 0.18f),
				Roughness = 0.95f
			};
			return _cacheMatBoisSapin;
		}

		if (indexBotanique == LSystem_Botanique.IndexJungle)
		{
			if (_cacheMatBoisJungle != null) return _cacheMatBoisJungle;
			var bruit = new FastNoiseLite { Seed = 1717, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.085f };
			var tex = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruit };
			var ramp = new Gradient();
			ramp.AddPoint(0.0f, new Color(0.24f, 0.17f, 0.11f));
			ramp.AddPoint(1.0f, new Color(0.46f, 0.34f, 0.22f));
			tex.ColorRamp = ramp;
			_cacheMatBoisJungle = new StandardMaterial3D
			{
				AlbedoTexture = tex,
				AlbedoColor = new Color(0.50f, 0.37f, 0.24f),
				Roughness = 0.94f
			};
			return _cacheMatBoisJungle;
		}

		if (indexBotanique == LSystem_Botanique.IndexBouleau)
		{
			if (_cacheMatBoisBouleau != null) return _cacheMatBoisBouleau;
			var bruit = new FastNoiseLite { Seed = 777, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.08f };
			var tex = new NoiseTexture2D { Width = 256, Height = 256, Noise = bruit };
			var ramp = new Gradient();
			ramp.AddPoint(0.0f, new Color(0.1f, 0.1f, 0.1f));    // Taches noires
			ramp.AddPoint(0.2f, new Color(0.85f, 0.85f, 0.82f));  // Écorce blanche
			ramp.AddPoint(1.0f, new Color(0.92f, 0.92f, 0.90f));
			tex.ColorRamp = ramp;
			_cacheMatBoisBouleau = new StandardMaterial3D { AlbedoTexture = tex, Roughness = 0.85f };
			return _cacheMatBoisBouleau;
		}

		if (_cacheMatBois != null) return _cacheMatBois;
		var bruitEcorce = new FastNoiseLite { Seed = 4242 };
		bruitEcorce.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		bruitEcorce.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		bruitEcorce.Frequency = 0.08f; // Fines stries type écorce
		var texEcorce = new NoiseTexture2D { Width = 128, Height = 128, Noise = bruitEcorce };
		_cacheMatBois = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.52f, 0.32f, 0.14f), // Brun bois chaud
			AlbedoTexture = texEcorce,
			Roughness = 0.9f,
			Metallic = 0.02f
		};
		return _cacheMatBois;
	}

	/// <summary>Même texture que le tronc, triplanar monde (ignore les UV locaux dégénérés) + teinte aubier.</summary>
	public static Material ObtenirMaterielBoisTriplanar(byte indexBotanique = 0)
	{
		if (indexBotanique == LSystem_Botanique.IndexPin)
		{
			if (_cacheMatBoisTriplanarPin != null) return _cacheMatBoisTriplanarPin;
			ObtenirMaterielBois(LSystem_Botanique.IndexPin);
			_cacheMatBoisTriplanarPin = (StandardMaterial3D)_cacheMatBoisPin.Duplicate();
			_cacheMatBoisTriplanarPin.Uv1Triplanar = true;
			_cacheMatBoisTriplanarPin.Uv1WorldTriplanar = false;
			_cacheMatBoisTriplanarPin.Uv1TriplanarSharpness = 2f;
			// Rendu au sol/objets plus proche de l'écorce réelle du pin (moins jaune clair).
			_cacheMatBoisTriplanarPin.AlbedoColor = new Color(0.46f, 0.34f, 0.25f);
			_cacheMatBoisTriplanarPin.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			return _cacheMatBoisTriplanarPin;
		}

		if (indexBotanique == LSystem_Botanique.IndexSapin)
		{
			if (_cacheMatBoisTriplanarSapin != null) return _cacheMatBoisTriplanarSapin;
			ObtenirMaterielBois(LSystem_Botanique.IndexSapin);
			_cacheMatBoisTriplanarSapin = (StandardMaterial3D)_cacheMatBoisSapin.Duplicate();
			_cacheMatBoisTriplanarSapin.Uv1Triplanar = true;
			_cacheMatBoisTriplanarSapin.Uv1WorldTriplanar = false;
			_cacheMatBoisTriplanarSapin.Uv1TriplanarSharpness = 2f;
			_cacheMatBoisTriplanarSapin.AlbedoColor = new Color(0.52f, 0.40f, 0.32f);
			_cacheMatBoisTriplanarSapin.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			return _cacheMatBoisTriplanarSapin;
		}

		if (indexBotanique == LSystem_Botanique.IndexJungle)
		{
			if (_cacheMatBoisTriplanarJungle != null) return _cacheMatBoisTriplanarJungle;
			ObtenirMaterielBois(LSystem_Botanique.IndexJungle);
			_cacheMatBoisTriplanarJungle = (StandardMaterial3D)_cacheMatBoisJungle.Duplicate();
			_cacheMatBoisTriplanarJungle.Uv1Triplanar = true;
			_cacheMatBoisTriplanarJungle.Uv1WorldTriplanar = false;
			_cacheMatBoisTriplanarJungle.Uv1TriplanarSharpness = 2f;
			_cacheMatBoisTriplanarJungle.AlbedoColor = new Color(0.66f, 0.50f, 0.33f);
			_cacheMatBoisTriplanarJungle.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			return _cacheMatBoisTriplanarJungle;
		}

		if (indexBotanique == LSystem_Botanique.IndexBouleau)
		{
			if (_cacheMatBoisTriplanarBouleau != null) return _cacheMatBoisTriplanarBouleau;
			ObtenirMaterielBois(LSystem_Botanique.IndexBouleau);
			_cacheMatBoisTriplanarBouleau = (StandardMaterial3D)_cacheMatBoisBouleau.Duplicate();
			_cacheMatBoisTriplanarBouleau.Uv1Triplanar = true;
			_cacheMatBoisTriplanarBouleau.Uv1WorldTriplanar = false;
			_cacheMatBoisTriplanarBouleau.Uv1TriplanarSharpness = 2f;
			_cacheMatBoisTriplanarBouleau.AlbedoColor = new Color(0.82f, 0.78f, 0.65f); // Bois intérieur clair
			_cacheMatBoisTriplanarBouleau.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			return _cacheMatBoisTriplanarBouleau;
		}

		if (_cacheMatBoisTriplanar != null) return _cacheMatBoisTriplanar;
		ObtenirMaterielBois();
		_cacheMatBoisTriplanar = (StandardMaterial3D)_cacheMatBois.Duplicate();
		_cacheMatBoisTriplanar.Uv1Triplanar = true;
		// Mapping triplanar local (et non monde) pour figer la texture sur chaque mesh de bois.
		_cacheMatBoisTriplanar.Uv1WorldTriplanar = false;
		_cacheMatBoisTriplanar.Uv1TriplanarSharpness = 2f;
		_cacheMatBoisTriplanar.AlbedoColor = new Color(0.65f, 0.45f, 0.25f);
		// Désactive le backface culling (éclats / faces de coupe visibles même si winding imparfait).
		_cacheMatBoisTriplanar.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		return _cacheMatBoisTriplanar;
	}

	/// <summary>Bâton de chêne façonné au craft (branche → bâton) : même texture triplanar, albedo plus pâle (aubier / bois travaillé).</summary>
	public static Material ObtenirMaterielBoisTriplanarBatonChenEPale()
	{
		if (_cacheMatBoisBatonChenEPale != null) return _cacheMatBoisBatonChenEPale;
		ObtenirMaterielBoisTriplanar();
		_cacheMatBoisBatonChenEPale = (StandardMaterial3D)_cacheMatBoisTriplanar.Duplicate();
		Color baseC = _cacheMatBoisBatonChenEPale.AlbedoColor;
		_cacheMatBoisBatonChenEPale.AlbedoColor = baseC.Lerp(new Color(0.92f, 0.86f, 0.78f), 0.38f);
		return _cacheMatBoisBatonChenEPale;
	}

	private static Material ObtenirMaterielFeuilles(byte indexBotanique)
	{
		if (indexBotanique == LSystem_Botanique.IndexPin || indexBotanique == LSystem_Botanique.IndexSapin)
		{
			if (_cacheMatFeuillesPin != null) return _cacheMatFeuillesPin;
			_cacheMatFeuillesPin = new StandardMaterial3D
			{
				AlbedoColor = Colors.White,
				VertexColorUseAsAlbedo = true,
				Roughness = 0.93f,
				Metallic = 0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			return _cacheMatFeuillesPin;
		}
		if (indexBotanique == LSystem_Botanique.IndexJungle)
		{
			if (_cacheMatFeuillesJungle != null) return _cacheMatFeuillesJungle;
			if (_cacheTextureFeuilleJungle == null)
			{
				var bruitFeuilleJungle = new FastNoiseLite { Seed = 42042 };
				bruitFeuilleJungle.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
				bruitFeuilleJungle.Frequency = 0.13f;
				bruitFeuilleJungle.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
				bruitFeuilleJungle.FractalOctaves = 2;
				var texJungle = new NoiseTexture2D
				{
					Width = 128,
					Height = 128,
					Noise = bruitFeuilleJungle
				};
				var rampJungle = new Gradient();
				rampJungle.AddPoint(0.0f, new Color(0.08f, 0.22f, 0.10f));
				rampJungle.AddPoint(0.55f, new Color(0.18f, 0.45f, 0.20f));
				rampJungle.AddPoint(1.0f, new Color(0.11f, 0.30f, 0.14f));
				texJungle.ColorRamp = rampJungle;
				_cacheTextureFeuilleJungle = texJungle;
			}
			_cacheMatFeuillesJungle = new StandardMaterial3D
			{
				AlbedoColor = Colors.White,
				VertexColorUseAsAlbedo = true,
				AlbedoTexture = _cacheTextureFeuilleJungle,
				Roughness = 0.90f,
				Metallic = 0f,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			return _cacheMatFeuillesJungle;
		}
		if (_cacheMatFeuillesCaduc != null) return _cacheMatFeuillesCaduc;
		if (_cacheTextureFeuilleCaduc == null)
		{
			var bruitFeuille = new FastNoiseLite { Seed = 21037 };
			bruitFeuille.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
			bruitFeuille.Frequency = 0.16f;
			bruitFeuille.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
			bruitFeuille.FractalOctaves = 3;
			_cacheTextureFeuilleCaduc = new NoiseTexture2D
			{
				Width = 96,
				Height = 96,
				Noise = bruitFeuille
			};
		}
		_cacheMatFeuillesCaduc = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			VertexColorUseAsAlbedo = true,
			AlbedoTexture = _cacheTextureFeuilleCaduc,
			Roughness = 0.95f,
			Metallic = 0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		return _cacheMatFeuillesCaduc;
	}

	public override void _Ready()
	{
		_visuelBois = new MeshInstance3D { Name = "Bois" };
		_visuelFeuillage = new MeshInstance3D { Name = "Feuillage" };
		_hitbox = new CollisionShape3D { Name = "Hitbox" };
		_visuelFeuillage.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		AddChild(_visuelBois);
		AddChild(_visuelFeuillage);
		AddChild(_hitbox);

		AddToGroup("Arbres");
		uint px = (uint)Mathf.Abs((int)GlobalPosition.X);
		uint py = (uint)Mathf.Abs((int)GlobalPosition.Y);
		uint pz = (uint)Mathf.Abs((int)GlobalPosition.Z);
		uint posHash = (px * 73856093u) ^ (py * 83492791u) ^ (pz * 19349663u);
		_seedEffectif = Seed ^ posHash ^ (uint)(IndexBotanique * 2654435761u);
		if (IndexBotanique == LSystem_Botanique.IndexJungle)
		{
			// Jeune jungle: lianes courtes, elles se développent avec l'âge.
			float jitter = 0.82f + Hash(SeedForme, 19111) * 0.18f;
			_progressionLianesJungle = ProgressionLianesCibleSelonAge(AgeEnJours) * jitter;
		}

		// Répartit le coût de spawn sur plusieurs frames (évite le freeze quand une forêt apparaît).
		float dObs = GlobalPosition.DistanceTo(PositionObservation());
		float h = Hash(SeedForme, 15000);
		// Proche joueur: quasi immédiat avec léger jitter pour éviter les gros spikes.
		// Moyen/loin: étalement plus large pour lisser le coût global.
		if (dObs <= 110f) _attenteGeneration = h * 0.08f;
		else if (dObs <= 210f) _attenteGeneration = 0.04f + h * 0.22f;
		else _attenteGeneration = 0.35f + h * 2.40f;
		_cooldownLod = Hash(SeedForme, 15100) * INTERVALLE_MAJ_LOD;
		SetProcess(true);
	}

	private static bool ConsommerBudgetGenerationInitiale()
	{
		ulong frame = Engine.GetProcessFrames();
		if (_frameBudgetGenerationInit != frame)
		{
			_frameBudgetGenerationInit = frame;
			int budget = BUDGET_GENERATION_INIT_PAR_FRAME;
			float fps = (float)Engine.GetFramesPerSecond();
			if (fps > 0f)
			{
				if (fps < 42f) budget = 1;
				else if (fps > 95f) budget = BUDGET_GENERATION_INIT_PAR_FRAME + 1;
			}
			_resteBudgetGenerationInit = Mathf.Max(1, budget);
		}
		if (_resteBudgetGenerationInit <= 0) return false;
		_resteBudgetGenerationInit--;
		return true;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (!_maillageInitialGenere)
		{
			_attenteGeneration -= dt;
			if (_attenteGeneration <= 0f)
			{
				// Anti micro-freeze: limite stricte du nombre d'arbres qui génèrent leur 1er maillage par frame.
				if (ConsommerBudgetGenerationInitiale())
					RegenererSelonLod(true);
				else
					_attenteGeneration = 0.02f + Hash(SeedForme, 15050) * 0.05f;
			}
			return;
		}

		_cooldownLod -= dt;
		if (_cooldownLod <= 0f)
		{
			float distance = GlobalPosition.DistanceTo(PositionObservation());
			float intervalleLod = INTERVALLE_MAJ_LOD;
			if (distance > DISTANCE_LOD1) intervalleLod = 1.20f;
			if (distance > DISTANCE_LOD2) intervalleLod = 2.00f;
			// Sapin: optimisation légère sans changer le rendu final (moins de recalculs LOD).
			if (IndexBotanique == LSystem_Botanique.IndexSapin) intervalleLod *= 1.25f;
			_cooldownLod = intervalleLod + Hash(SeedForme, 15200) * 0.45f;
			RegenererSelonLod(false);
		}
	}

	/// <summary>Appelé à minuit par le serveur (arbres dans chunks actifs). 1 chance sur 20 de grandir.</summary>
	public void VieillirUnJour()
	{
		bool aGrandi = GD.Randf() <= CHANCE_CROISSANCE;
		bool lianesOntRepousse = false;
		if (aGrandi)
		{
			AgeEnJours++;
			ResistanceActuelle = ResistanceMaxPourAge(AgeEnJours);
			if (IndexBotanique == LSystem_Botanique.IndexJungle)
			{
				float cible = ProgressionLianesCibleSelonAge(AgeEnJours);
				if (_progressionLianesJungle < cible)
				{
					// Repousse liée à la croissance de l'arbre (même chance/jour, pas de repousse infinie quotidienne).
					_progressionLianesJungle = Mathf.Clamp(_progressionLianesJungle + 0.34f, 0f, cible);
					lianesOntRepousse = true;
				}
			}
		}
		if (aGrandi || lianesOntRepousse)
		{
			RegenererSelonLod(true);
		}
	}

	/// <summary>Simule le temps passé hors-ligne quand le chunk est rechargé. Déterministe (seed position).</summary>
	/// <param name="joursEcoules">Jours où le chunk était déchargé.</param>
	/// <param name="posMonde">Position de l'arbre (pour seed déterministe si pas encore dans la scène).</param>
	public void RattraperCroissance(int joursEcoules, Vector3? posMonde = null)
	{
		if (joursEcoules <= 0) return;
		Vector3 pos = posMonde ?? GlobalPosition;
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Mathf.Abs(pos.X) * 73856.0 + Mathf.Abs(pos.Z) * 19349.0 + joursEcoules * 7919);
		int succesCroissance = 0;
		for (int i = 0; i < joursEcoules; i++)
		{
			if (rng.Randf() <= CHANCE_CROISSANCE)
				succesCroissance++;
		}
		if (succesCroissance > 0)
		{
			AgeEnJours += succesCroissance;
			ResistanceActuelle = ResistanceMaxPourAge(AgeEnJours);
		}
		if (IndexBotanique == LSystem_Botanique.IndexJungle)
		{
			float cible = ProgressionLianesCibleSelonAge(AgeEnJours);
			if (_progressionLianesJungle < cible)
			{
				// Hors-ligne: la repousse suit uniquement les "jours de croissance" tirés ci-dessus.
				if (succesCroissance > 0)
					_progressionLianesJungle = Mathf.Clamp(_progressionLianesJungle + succesCroissance * 0.30f, 0f, cible);
			}
		}
		if (succesCroissance > 0)
			RegenererSelonLod(true);
	}

	private static float ProgressionLianesCibleSelonAge(int age)
	{
		if (age <= 1) return 0.18f;
		if (age == 2) return 0.34f;
		if (age == 3) return 0.52f;
		if (age == 4) return 0.70f;
		if (age == 5) return 0.82f;
		return 1f;
	}

	/// <summary>Coupe une liane de jungle avec la dague. Retourne true si une liane est récoltée.</summary>
	public bool EssayerCouperLiane(Vector3 pointImpactMonde, Vector3 directionFrappe, out Vector3 posSpawnLiane)
	{
		posSpawnLiane = pointImpactMonde;
		if (IndexBotanique != LSystem_Botanique.IndexJungle) return false;
		if (!EstPointCibleLiane(pointImpactMonde)) return false;
		Vector3 hitLocal = GlobalTransform.AffineInverse() * pointImpactMonde;
		float hTronc = Mathf.Max(0.25f, _hauteurTroncTotale);
		float hNorm = Mathf.Clamp(hitLocal.Y / hTronc, 0f, 1f);
		// Les lianes exploitables sont sur la moitié haute/canopée.
		if (hNorm < 0.45f) return false;
		if (ChanceLianesJungleSelonAge(AgeEnJours) * _progressionLianesJungle < 0.06f) return false;

		// Plus de plancher à 0.25: évite la récolte infinie.
		_progressionLianesJungle = Mathf.Clamp(_progressionLianesJungle - 0.34f, 0f, 1f);
		RegenererSelonLod(true);
		posSpawnLiane = pointImpactMonde + directionFrappe.Normalized() * 0.18f + Vector3.Up * 0.12f;
		return true;
	}

	/// <summary>Vrai seulement si l'impact vise la zone volumique de liane (pas le tronc entier).</summary>
	public bool EstPointCibleLiane(Vector3 pointImpactMonde)
	{
		if (IndexBotanique != LSystem_Botanique.IndexJungle) return false;
		if (ChanceLianesJungleSelonAge(AgeEnJours) * _progressionLianesJungle < 0.06f) return false;

		Vector3 hitLocal = GlobalTransform.AffineInverse() * pointImpactMonde;
		float hTronc = Mathf.Max(0.25f, _hauteurTroncTotale);
		float hNorm = hitLocal.Y / hTronc;
		// Zone verticale réaliste des lianes: haut de tronc + canopée.
		if (hNorm < 0.20f || hNorm > 1.45f) return false;

		float distAxis = Mathf.Sqrt(hitLocal.X * hitLocal.X + hitLocal.Z * hitLocal.Z);
		float hClamp = Mathf.Clamp(hNorm, 0f, 1f);
		float rayonTronc = 0.2f * (1f - hClamp * 0.6f) * (AgeEnJours * 0.5f);
		rayonTronc = Mathf.Max(0.05f, rayonTronc);

		// Oblige de viser en dehors du fût, mais pas dans le vide loin de l'arbre.
		bool horsTronc = distAxis > (rayonTronc + 0.16f);
		bool dansVolumeLiane = distAxis < (rayonTronc + 1.35f);
		if (horsTronc && dansVolumeLiane) return true;

		// Fallback gameplay: le raycast tape souvent le collider du tronc (pas la géométrie des lianes).
		// On autorise donc une zone "près du tronc haut" pour ne pas bloquer la récolte au viseur.
		bool zoneVerticaleToleree = hNorm >= 0.35f && hNorm <= 1.30f;
		bool procheTroncTolere = distAxis <= (rayonTronc + 0.55f);
		return zoneVerticaleToleree && procheTroncTolere;
	}

	/// <summary>Applique des dégâts (minage avec pierre/silex). Loi du Rebond : force sous le seuil = zéro dégât.</summary>
	/// <param name="pointImpactMonde">Point d'impact du rayon (en coordonnées monde).</param>
	/// <param name="directionFrappe">Direction de la frappe (pour faire basculer l'arbre ou la branche).</param>
	/// <param name="forceImpact">Force d'impact cinétique (masse × vitesse × tranchant).</param>
	/// <param name="epaisseurLame">Épaisseur de la lame (détermine si on peut entamer le tronc).</param>
	/// <param name="hachettePrimitive106">Hachette assemblée : contourne la limite « lame fine seulement » sur tronc mature et mord mieux sur gros fût.</param>
	/// <returns>0 = Rebond, 1 = Touché (tronc), 2 = Arbre abattu, 3 = Branche amputée.</returns>
	public int SubirDegats(Vector3 pointImpactMonde, Vector3 directionFrappe, float forceImpact, float epaisseurLame, bool hachettePrimitive106 = false)
	{
		ProfilBotanique profil = LSystem_Botanique.ObtenirProfil(IndexBotanique);
		float facteurEssence = Mathf.Clamp(profil.ResistanceHache / 150f, 0.72f, 1.45f);
		// Jeunes arbres (tier 1–2) : seuil plus bas pour outils taillés / mains nues. Vieux : un peu plus d’inertie requise.
		float seuilRuptureBotanique = AgeEnJours <= 2
			? (20f + AgeEnJours * 12f)
			: (30f + AgeEnJours * 15f + 0.4f * AgeEnJours * AgeEnJours);
		seuilRuptureBotanique *= facteurEssence;
		if (AgeEnJours <= 2)
		{
			// Anti hard-lock early game:
			// Tous les jeunes arbres (incluant jungle) restent entaillables tôt.
			float facteurJeune = 1f;
			if (IndexBotanique == LSystem_Botanique.IndexChene) facteurJeune = 0.62f;
			else if (IndexBotanique == LSystem_Botanique.IndexBouleau) facteurJeune = 0.58f;
			else facteurJeune = 0.56f; // pin/sapin/jungle
			seuilRuptureBotanique *= facteurJeune;
		}
		if (hachettePrimitive106)
			seuilRuptureBotanique *= 0.70f;

		Vector3 hitLocal = GlobalTransform.AffineInverse() * pointImpactMonde;
		float distAxis = Mathf.Sqrt(hitLocal.X * hitLocal.X + hitLocal.Z * hitLocal.Z);
		// Hauteur réelle du tronc (segments « T ») : sinon l’ancienne formule sous-estime y → tronc trop « épais » → rebond sur 2e/3e tiers
		float hTronc = Mathf.Max(0.25f, _hauteurTroncTotale);
		float hNormTronc = Mathf.Clamp(hitLocal.Y / hTronc, 0f, 1f);
		float epaisseurTronc = 0.2f * (1f - hNormTronc * 0.6f) * (AgeEnJours * 0.5f);

		// Les 3 premiers tiers du tronc : zone d’axe élargie pour pouvoir entailler toute la hauteur utile du fût
		float rayonAxe = hNormTronc < 1f / 3f ? 0.52f : (hNormTronc < 2f / 3f ? 0.46f : 0.40f);
		bool estLeTronc = hitLocal.Y <= hTronc * 1.05f && distAxis < rayonAxe;
		float epaisseurEstimee = estLeTronc ? epaisseurTronc : 0.05f;

		// Hachette bien orientée : même si l'impact est faible, le tronc prend toujours des copeaux (progression garantie).
		if (hachettePrimitive106 && estLeTronc && forceImpact < seuilRuptureBotanique)
		{
			float pvMaxTheorique = ResistanceMaxPourAge(AgeEnJours);
			float degatsGarantis = Mathf.Max(1.9f, pvMaxTheorique * 0.009f);
			ResistanceActuelle -= degatsGarantis;
			if (ResistanceActuelle <= 0f)
			{
				DeclencherChuteArbre(directionFrappe);
				return 2;
			}
			return 1;
		}

		if (forceImpact < seuilRuptureBotanique)
			return 0;

		// Roc / lame très épaisse : pas de taille fine sur tronc mature — sauf vraie hachette (tranchant + masse).
		if (!hachettePrimitive106 && AgeEnJours >= 3 && epaisseurLame > 0.05f)
			return 0;

		if (!hachettePrimitive106 && epaisseurEstimee > epaisseurLame * 4.0f && epaisseurLame > 0.04f)
			return 0;

		if (estLeTronc)
		{
			float multiplicateur = 0.12f / Mathf.Max(0.01f, epaisseurEstimee);
			if (hachettePrimitive106)
				multiplicateur *= 1.22f;
			float degatsBruts = forceImpact * Mathf.Clamp(multiplicateur, 0.1f, 2.4f);
			// Anti one-shot: chaque coup enlève une portion bornée des PV max, puis l’arbre cède après une vraie série d’entailles.
			float pvMaxTheorique = ResistanceMaxPourAge(AgeEnJours);
			float plafondParCoup = pvMaxTheorique * (hachettePrimitive106 ? 0.20f : 0.14f);
			float plancherParCoup = hachettePrimitive106 ? 4.5f : 3.0f;
			if (!hachettePrimitive106 && AgeEnJours <= 2)
				plancherParCoup = Mathf.Max(plancherParCoup, 4.2f);
			float degats = Mathf.Min(degatsBruts, Mathf.Max(plancherParCoup, plafondParCoup));
			ResistanceActuelle -= degats;
			if (ResistanceActuelle <= 0f)
			{
				DeclencherChuteArbre(directionFrappe);
				return 2;
			}
			return 1;
		}
		else
		{
			_coupesLocales.Add(hitLocal);
			GenererMaillageArbre();
			DeclencherChuteBranche(pointImpactMonde, directionFrappe);
			return 3;
		}
	}

	private void DeclencherChuteArbre(Vector3 directionFrappe)
	{
		Transform3D poseArbre = GlobalTransform;
		RigidBody3D cadavre = new RigidBody3D { Name = "ArbreMort" };
		cadavre.Mass = 50f + (AgeEnJours * 80f);
		cadavre.ContinuousCd = true;
		cadavre.CollisionLayer = 1;
		cadavre.CollisionMask = 1;
		cadavre.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.08f };
		cadavre.SetMeta("PV", 60f * AgeEnJours);
		cadavre.SetMeta("Age", AgeEnJours);
		cadavre.SetMeta("HauteurTronc", _hauteurTroncTotale);
		cadavre.SetMeta("RayonTroncBase", _rayonTroncBase);
		cadavre.SetMeta("RayonTroncSommet", _rayonTroncSommet);
		cadavre.SetMeta("LongueurBrancheMoy", _longueurBrancheMoyenne);
		cadavre.SetMeta("EpaisseurBrancheMoy", _epaisseurBrancheMoyenne);
		// Essence de l'arbre (chêne/bouleau) conservée sur le cadavre.
		cadavre.SetMeta("IndexBotanique", (int)IndexBotanique);
		int segmentsTronc = Mathf.Max(1, Mathf.CeilToInt(_hauteurTroncTotale / 1.0f));
		cadavre.SetMeta("SegmentsRestants", segmentsTronc);
		cadavre.SetMeta("SegmentsInitiaux", segmentsTronc);
		// Standardise l'ébranchage: même les très vieux arbres ne demandent pas des dizaines de frappes "bâton".
		int branchesRestantes = Mathf.Clamp(2 + Mathf.CeilToInt(_hauteurTroncTotale * 0.55f), 3, 10);
		cadavre.SetMeta("BranchesRestantes", branchesRestantes);

		cadavre.AngularDampMode = RigidBody3D.DampMode.Replace;
		cadavre.AngularDamp = 4.0f;
		cadavre.LinearDampMode = RigidBody3D.DampMode.Replace;
		cadavre.LinearDamp = 1.0f;

		MeshInstance3D boisCopy = new MeshInstance3D { Name = "Bois", Mesh = _visuelBois.Mesh, MaterialOverride = _visuelBois.MaterialOverride };
		MeshInstance3D feuillesCopy = new MeshInstance3D { Name = "Feuillage", Mesh = _visuelFeuillage.Mesh, MaterialOverride = _visuelFeuillage.MaterialOverride };
		cadavre.AddChild(boisCopy);
		cadavre.AddChild(feuillesCopy);

		CollisionShape3D hitboxCopy = new CollisionShape3D();
		Mesh meshArbre = _visuelBois.Mesh;

		if (AgeEnJours > 10)
		{
			GD.Print("ZERO-K : Arbre titanesque détecté. Utilisation d'un cylindre de collision pour éviter l'effondrement quantique.");
			var cylindreDeSecours = new CylinderShape3D
			{
				Radius = 0.2f + (AgeEnJours * 0.05f),
				Height = 1.0f + (AgeEnJours * 0.5f)
			};
			hitboxCopy.Shape = cylindreDeSecours;
			hitboxCopy.Position = new Vector3(0, cylindreDeSecours.Height / 2f, 0);
		}
		else
		{
			Shape3D choix = null;
			if (meshArbre != null)
			{
				try
				{
					if (meshArbre.GetFaces().Length > 0)
						choix = meshArbre.CreateConvexShape(true, true);
				}
				catch (Exception ex)
				{
					GD.PrintErr($"ZERO-K : Convex arbre échoué ({ex.Message}). Boîte englobante de secours.");
					choix = null;
				}
			}
			if (choix == null)
				choix = ItemPhysique.CreerShapeCollisionConvexeRobuste(meshArbre);
			hitboxCopy.Shape = choix;
		}

		cadavre.AddChild(hitboxCopy);

		GetParent().AddChild(cadavre);
		cadavre.GlobalTransform = poseArbre;
		cadavre.GlobalPosition += Vector3.Up * 0.24f;
		cadavre.ApplyCentralImpulse(directionFrappe * (40f * AgeEnJours) + Vector3.Up * 20f);

		QueueFree();
	}

	/// <summary>L’impact est souvent dans le volume du feuillage/tronc : repousser depuis la racine puis garder au-dessus du sol.</summary>
	private Vector3 CalculerPositionSpawnBranche(Vector3 pointImpact, Vector3 directionFrappe)
	{
		Vector3 depuisRacine = pointImpact - GlobalPosition;
		if (depuisRacine.LengthSquared() < 1e-4f)
			depuisRacine = directionFrappe.LengthSquared() > 1e-4f ? directionFrappe.Normalized() : Vector3.Up;
		else
			depuisRacine = depuisRacine.Normalized();
		Vector3 candidat = pointImpact + depuisRacine * 0.48f + Vector3.Up * 0.22f;
		var space = GetWorld3D()?.DirectSpaceState;
		if (space == null)
			return candidat;
		Vector3 haut = new Vector3(candidat.X, candidat.Y + 8f, candidat.Z);
		Vector3 bas = new Vector3(candidat.X, candidat.Y - 25f, candidat.Z);
		var q = PhysicsRayQueryParameters3D.Create(haut, bas);
		q.CollisionMask = 1;
		var hit = space.IntersectRay(q);
		if (hit.Count > 0 && hit.ContainsKey("position"))
		{
			Vector3 sol = hit["position"].AsVector3();
			Vector3 n = hit.ContainsKey("normal") ? hit["normal"].AsVector3().Normalized() : Vector3.Up;
			const float minAuDessus = 0.32f;
			float d = n.Dot(candidat - sol);
			if (d < minAuDessus)
				candidat = sol + n * minAuDessus + depuisRacine * 0.12f;
		}
		return candidat;
	}

	private void DeclencherChuteBranche(Vector3 pointImpact, Vector3 directionFrappe)
	{
		RigidBody3D brancheMorte = new RigidBody3D { Name = "BrancheMorte" };
		float volBr = Mathf.Pi * 0.05f * 0.05f * 0.8f;
		brancheMorte.Mass = Mathf.Max(0.2f, volBr * 500f);
		brancheMorte.ContinuousCd = true;
		brancheMorte.CollisionLayer = 1;
		brancheMorte.CollisionMask = 1;
		brancheMorte.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.78f, Bounce = 0.14f };
		brancheMorte.LinearDampMode = RigidBody3D.DampMode.Replace;
		brancheMorte.LinearDamp = 0.08f;
		brancheMorte.AngularDampMode = RigidBody3D.DampMode.Replace;
		brancheMorte.AngularDamp = 0.35f;

		brancheMorte.AddChild(new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.05f, Height = 0.8f },
			MaterialOverride = _visuelBois.MaterialOverride,
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0)
		});
		brancheMorte.AddChild(new CollisionShape3D
		{
			Shape = new CylinderShape3D { Radius = 0.05f, Height = 0.8f },
			Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0)
		});

		GetParent().AddChild(brancheMorte);
		brancheMorte.GlobalPosition = CalculerPositionSpawnBranche(pointImpact, directionFrappe);
		brancheMorte.ApplyCentralImpulse(directionFrappe * 5f);
	}

	private static float Hash(uint seed, int salt)
	{
		uint h = (seed * 73856093u) ^ (uint)(salt * 19349663);
		return ((h % 10000) / 10000f);
	}

	private Vector3 PositionObservation()
	{
		if (_observationRef == null || !GodotObject.IsInstanceValid(_observationRef))
		{
			Node scene = GetTree()?.CurrentScene;
			_observationRef = GetViewport()?.GetCamera3D();
			if (_observationRef == null || !GodotObject.IsInstanceValid(_observationRef))
			{
			_observationRef = scene?.GetNodeOrNull<Node3D>("Joueur/Camera3D")
				?? scene?.GetNodeOrNull<Node3D>("Joueur");
			}
		}
		return _observationRef != null && GodotObject.IsInstanceValid(_observationRef)
			? _observationRef.GlobalPosition
			: GlobalPosition;
	}

	private int EvaluerLodDistance(float distance)
	{
		// Hystérésis légère pour éviter les regen en boucle sur les frontières.
		if (_lodActuel == 0)
			return distance <= DISTANCE_LOD0 + 8f ? 0 : (distance < DISTANCE_LOD1 ? 1 : (distance < DISTANCE_LOD2 ? 2 : 2));
		if (_lodActuel == 1)
			return distance < DISTANCE_LOD0 - 5f ? 0 : (distance <= DISTANCE_LOD1 + 10f ? 1 : 2);
		if (_lodActuel == 2)
			return distance < DISTANCE_LOD1 - 8f ? 1 : 2;
		return distance < DISTANCE_LOD0 ? 0 : (distance < DISTANCE_LOD1 ? 1 : 2);
	}

	private void RegenererSelonLod(bool forcer)
	{
		float distance = GlobalPosition.DistanceTo(PositionObservation());
		int lod = EvaluerLodDistance(distance);
		if (!forcer && lod == _lodActuel) return;
		_lodActuel = lod;
		GenererMaillageArbre(_lodActuel);
		_maillageInitialGenere = true;
	}

	private void GenererMaillageArbre(int lodNiveau = 0)
	{
		lodNiveau = Mathf.Clamp(lodNiveau, 0, 2);
		float facteurFeuillesLod = lodNiveau == 0 ? 1f : (lodNiveau == 1 ? 0.62f : 0.20f);
		float facteurAiguillesLod = lodNiveau == 0 ? 1f : (lodNiveau == 1 ? 0.60f : 0.18f);
		_lodFeuillageActuel = facteurFeuillesLod;

		int iter = IndexBotanique switch
		{
			LSystem_Botanique.IndexPin => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 4)),
			LSystem_Botanique.IndexSapin => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 4)),
			LSystem_Botanique.IndexJungle => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 5)),
			LSystem_Botanique.IndexBouleau => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 3)),
			_ => Mathf.Max(2, Mathf.Clamp(AgeEnJours, 1, 3))
		};
		if (lodNiveau >= 1)
		{
			// Jungle: garder plus de complexité au loin pour éviter l’effet "4 branches".
			if (IndexBotanique == LSystem_Botanique.IndexJungle) iter = Mathf.Max(2, iter - 1);
			else iter = Mathf.Max(1, iter - 1);
		}

		string adnFinal = "";
		const int maxAdnLen = 18000;
		for (;;)
		{
			adnFinal = IndexBotanique switch
			{
				LSystem_Botanique.IndexPin => LSystem_Botanique.GenererChainePinOrganique(iter, SeedForme),
				LSystem_Botanique.IndexSapin => LSystem_Botanique.GenererChaineSapinOrganique(iter, SeedForme),
				LSystem_Botanique.IndexJungle => LSystem_Botanique.GenererChaineJungleOrganique(iter, SeedForme),
				LSystem_Botanique.IndexBouleau => LSystem_Botanique.GenererChaineBouleauOrganique(iter, SeedForme),
				_ => LSystem_Botanique.GenererChaineCheneOrganique(iter, SeedForme)
			};
			if (adnFinal.Length <= maxAdnLen || iter <= 2) break;
			iter--;
		}

		var stBois = new SurfaceTool();
		stBois.Begin(Mesh.PrimitiveType.Triangles);

		var stFeuilles = new SurfaceTool();
		stFeuilles.Begin(Mesh.PrimitiveType.Triangles);
		Color couleurFeuillage = CouleurFeuillesArbre();
		// Couleur portée par les vertex + matériau partagé = beaucoup moins de draw overhead.
		stFeuilles.SetColor(couleurFeuillage);

		Stack<TortueEtat> pile = new Stack<TortueEtat>();
		Transform3D tortue = Transform3D.Identity;

		float angleBase = IndexBotanique switch
		{
			LSystem_Botanique.IndexPin => 80f,
			LSystem_Botanique.IndexSapin => 62f,
			LSystem_Botanique.IndexJungle => 28f,
			LSystem_Botanique.IndexBouleau => 20f,
			_ => 35f
		};
		float angle = Mathf.DegToRad(angleBase + Hash(SeedForme, 0) * 14f);
		float multEpaisseur = 0.75f + Hash(SeedForme, 1) * 0.5f;
		float multLongueur = 0.8f + Hash(SeedForme, 2) * 0.6f;
		float reductionBranche = 0.72f + Hash(SeedForme, 3) * 0.18f;
		// Bébé arbres (1-2) plus petits ; matures (5+) plus grands
		float scaleAge = AgeEnJours <= 2 ? 0.4f + 0.2f * AgeEnJours : 1f;
		float epaisseurBase = (0.12f + 0.06f * AgeEnJours) * multEpaisseur * scaleAge;
		float longueurSegment = (0.6f + AgeEnJours * 0.18f) * multLongueur * scaleAge;
		if (IndexBotanique == LSystem_Botanique.IndexBouleau)
		{
			// Bouleau plus fin, moins "totem", tronc un peu élancé.
			epaisseurBase *= 0.72f;
			// Etages de branches plus rapproches.
			longueurSegment *= 0.56f;
			reductionBranche = 0.78f + Hash(SeedForme, 3) * 0.10f;
		}
		else if (IndexBotanique == LSystem_Botanique.IndexChene)
		{
			// Chêne moins étiré: un peu plus trapu.
			epaisseurBase *= 1.08f;
			longueurSegment *= 0.74f;
			reductionBranche = 0.76f + Hash(SeedForme, 3) * 0.10f;
		}
		else if (IndexBotanique == LSystem_Botanique.IndexPin)
		{
			// Pin: tronc élancé + conicité marquée.
			epaisseurBase *= 0.58f;
			longueurSegment *= 0.52f;
			reductionBranche = 0.86f + Hash(SeedForme, 3) * 0.06f;
		}
		else if (IndexBotanique == LSystem_Botanique.IndexSapin)
		{
			// Sapin: conique mais plus fourni que le pin.
			epaisseurBase *= 0.62f;
			longueurSegment *= 0.50f;
			reductionBranche = 0.83f + Hash(SeedForme, 3) * 0.06f;
		}
		else if (IndexBotanique == LSystem_Botanique.IndexJungle)
		{
			// Jungle: tronc haut et robuste, conicité lente pour éviter l'effet "aiguille".
			epaisseurBase *= 1.24f;
			longueurSegment *= 0.72f;
			reductionBranche = 0.90f + Hash(SeedForme, 3) * 0.04f;
		}

		_hauteurTroncTotale = 0f;
		float epaisseurBaseInitiale = epaisseurBase;
		_rayonTroncBase = epaisseurBase;
		_rayonTroncSommet = epaisseurBase;
		float sommeLongueurBranche = 0f;
		float sommeEpaisseurBranche = 0f;
		int nbSegmentsBranche = 0;

		bool premierSegmentDeBranche = false;
		bool estCoupe = false;
		int compteurJitterYaw = 0;
		int compteurJitterPitch = 0;
		foreach (char commande in adnFinal)
		{
			switch (commande)
			{
				case 'T':
					// TRONC ABSOLU : montée verticale pure, pas de feuilles
				{
					Vector3 pStart = tortue.Origin;
					Vector3 right = tortue.Basis.X.Normalized();
					Vector3 forward = tortue.Basis.Z.Normalized();
					tortue = tortue.TranslatedLocal(new Vector3(0, longueurSegment, 0));
					Vector3 pEnd = tortue.Origin;
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					if (IndexBotanique == LSystem_Botanique.IndexJungle && epaisseurBaseInitiale > 0.0001f)
						rayonFin = Mathf.Max(rayonFin, epaisseurBaseInitiale * 0.19f);
					_hauteurTroncTotale += longueurSegment;
					_rayonTroncSommet = rayonFin;
					if (!estCoupe)
					{
						foreach (Vector3 coupe in _coupesLocales)
						{
							if (pStart.DistanceTo(coupe) < 0.8f) { estCoupe = true; break; }
						}
					}
					if (!estCoupe)
					{
						GenererSegmentBranche(stBois, pStart, pEnd, right, forward, rayonDebut, rayonFin);
						// Conifères: aiguilles sur le haut du tronc pour éviter un aspect nu.
						if ((IndexBotanique == LSystem_Botanique.IndexPin || IndexBotanique == LSystem_Botanique.IndexSapin) && epaisseurBaseInitiale > 0.0001f)
						{
							float ratioTronc = Mathf.Clamp(rayonDebut / epaisseurBaseInitiale, 0f, 1f);
							if (ratioTronc < 0.78f)
							{
								float densite = IndexBotanique == LSystem_Botanique.IndexSapin ? 0.28f : 0.34f;
								GenererAiguillesConifere(stFeuilles, new Transform3D(tortue.Basis, pEnd), AgeEnJours + 2, densite * facteurAiguillesLod, IndexBotanique == LSystem_Botanique.IndexSapin);
							}
						}
					}
					epaisseurBase = rayonFin;
					break;
				}
				case 'F':
				case 'b':
				case 'c':
					// BRANCHE ou sous-branche
				{
					Vector3 pStart = tortue.Origin;
					Vector3 right = tortue.Basis.X.Normalized();
					Vector3 forward = tortue.Basis.Z.Normalized();
					float longueurLocale = longueurSegment;
					if ((IndexBotanique == LSystem_Botanique.IndexPin || IndexBotanique == LSystem_Botanique.IndexSapin) && epaisseurBaseInitiale > 0.0001f)
					{
						// Forme conique: branches basses plus longues, hautes plus courtes.
						float ratioHauteur = Mathf.Clamp(epaisseurBase / epaisseurBaseInitiale, 0.25f, 1.0f);
						float facteurCone = IndexBotanique == LSystem_Botanique.IndexSapin
							? 0.42f + 0.22f * Mathf.Pow(ratioHauteur, 1.18f)
							: 0.24f + 0.20f * Mathf.Pow(ratioHauteur, 1.45f);
						// Sapin: étage bas encore plus long pour approcher du sol.
						if (IndexBotanique == LSystem_Botanique.IndexSapin && ratioHauteur > 0.82f)
							facteurCone *= 1.22f;
						if (IndexBotanique == LSystem_Botanique.IndexSapin && ratioHauteur > 0.93f)
							facteurCone *= 1.16f;
						longueurLocale *= facteurCone;
					}
					if (commande == 'b')
					{
						if (IndexBotanique == LSystem_Botanique.IndexPin) longueurLocale *= 0.74f;
						else if (IndexBotanique == LSystem_Botanique.IndexSapin) longueurLocale *= 0.78f;
						else longueurLocale *= 0.86f;
					}
					else if (commande == 'c')
					{
						if (IndexBotanique == LSystem_Botanique.IndexPin) longueurLocale *= 0.52f;
						else if (IndexBotanique == LSystem_Botanique.IndexSapin) longueurLocale *= 0.56f;
						else longueurLocale *= 0.66f;
					}
					tortue = tortue.TranslatedLocal(new Vector3(0, longueurLocale, 0));
					Vector3 pEnd = tortue.Origin;
					float rayonFin = epaisseurBase * reductionBranche;
					float rayonDebut = epaisseurBase;
					if (premierSegmentDeBranche)
					{
						rayonDebut = epaisseurBase * 1.12f;
						premierSegmentDeBranche = false;
					}
					float coef = commande == 'b' ? 0.7f : (commande == 'c' ? 0.52f : 1f);
					nbSegmentsBranche++;
					sommeLongueurBranche += longueurLocale;
					sommeEpaisseurBranche += (rayonDebut + rayonFin) * 0.5f * coef;
					if (!estCoupe)
					{
						foreach (Vector3 coupe in _coupesLocales)
						{
							if (pStart.DistanceTo(coupe) < 0.8f) { estCoupe = true; break; }
						}
					}
					if (!estCoupe)
					{
						GenererSegmentBranche(stBois, pStart, pEnd, right, forward, rayonDebut * coef, rayonFin * coef);
						if (pile.Count > 0)
						{
							int hashBase = Mathf.Abs((int)(pStart.X * 7 + pStart.Z * 31 + pStart.Y * 13));
							Transform3D tStart = new Transform3D(tortue.Basis, pStart);
							Transform3D tMid = new Transform3D(tortue.Basis, pStart.Lerp(pEnd, 0.5f));
							Transform3D tEnd = new Transform3D(tortue.Basis, pEnd);
							if (IndexBotanique == LSystem_Botanique.IndexPin || IndexBotanique == LSystem_Botanique.IndexSapin)
							{
								// Conifères: aiguilles denses (pas de larges feuilles).
								bool estSapin = IndexBotanique == LSystem_Botanique.IndexSapin;
								GenererAiguillesConifere(stFeuilles, tStart, AgeEnJours + 2, (estSapin ? 0.72f : 0.58f) * facteurAiguillesLod, estSapin);
								if (Hash(SeedForme, hashBase + 91) < (estSapin ? 0.55f : 0.30f))
									GenererAiguillesConifere(stFeuilles, tMid, AgeEnJours + 2, (estSapin ? 0.82f : 0.66f) * facteurAiguillesLod, estSapin);
								GenererAiguillesConifere(stFeuilles, tEnd, AgeEnJours + 2, (estSapin ? 0.92f : 0.78f) * facteurAiguillesLod, estSapin);
							}
							else
							{
								float ratioBranche = epaisseurBaseInitiale > 0.0001f
									? Mathf.Clamp(((rayonDebut + rayonFin) * 0.5f) / epaisseurBaseInitiale, 0.20f, 1f)
									: 0.55f;
								bool canopeeValide = IndexBotanique != LSystem_Botanique.IndexJungle || ratioBranche >= 0.28f;
								if (canopeeValide)
								{
									float tailleBase = (0.70f + ratioBranche * 0.95f) * (0.86f + 0.14f * facteurFeuillesLod);
									if (IndexBotanique == LSystem_Botanique.IndexJungle) tailleBase *= 1.36f;
									GenererFeuillagePetit(stFeuilles, tStart, AgeEnJours, tailleBase * 1.10f, facteurFeuillesLod);
									GenererFeuillagePetit(stFeuilles, tMid, AgeEnJours, tailleBase * 0.92f, facteurFeuillesLod);
									GenererFeuillagePetit(stFeuilles, tEnd, AgeEnJours, tailleBase * 0.78f, facteurFeuillesLod);
									if (IndexBotanique == LSystem_Botanique.IndexJungle)
									{
										float chanceLiane = ChanceLianesJungleSelonAge(AgeEnJours);
										if (Hash(SeedForme, hashBase + 501) < chanceLiane * 0.78f)
											GenererLianesJungle(stFeuilles, tEnd, AgeEnJours, 0.90f + chanceLiane * 0.65f);
										if (Hash(SeedForme, hashBase + 641) < chanceLiane * 0.48f)
											GenererLianesTroncJungle(stFeuilles, tStart, AgeEnJours);
									}
								}
							}
						}
					}
					epaisseurBase = rayonFin * coef;
					break;
				}
				case '[':
					pile.Push(new TortueEtat { Transform = tortue, Epaisseur = epaisseurBase, EstCoupe = estCoupe });
					premierSegmentDeBranche = true;
					break;
				case ']':
				{
					TortueEtat etat = pile.Pop();
					tortue = etat.Transform;
					epaisseurBase = etat.Epaisseur;
					estCoupe = etat.EstCoupe;
					break;
				}
				case '+': tortue = tortue.RotatedLocal(Vector3.Right, angle); break;
				case '-': tortue = tortue.RotatedLocal(Vector3.Right, -angle); break;
				case 'R': tortue = tortue.RotatedLocal(Vector3.Up, Mathf.Pi / 4f); break;
				case 'r': tortue = tortue.RotatedLocal(Vector3.Up, -Mathf.Pi / 4f); break;
				case 'j':
				{
					// Jitter déterministe pour casser les couronnes parfaitement symétriques.
					float amp = IndexBotanique == LSystem_Botanique.IndexPin ? 18f : (IndexBotanique == LSystem_Botanique.IndexSapin ? 12f : 10f);
					float v = Hash(SeedForme, 10000 + compteurJitterYaw++);
					float yaw = Mathf.DegToRad((v * 2f - 1f) * amp);
					tortue = tortue.RotatedLocal(Vector3.Up, yaw);
					break;
				}
				case 'v':
				{
					float amp = IndexBotanique == LSystem_Botanique.IndexPin ? 4f : (IndexBotanique == LSystem_Botanique.IndexSapin ? 5f : 6f);
					float v = Hash(SeedForme, 11000 + compteurJitterPitch++);
					float pitch = Mathf.DegToRad((v * 2f - 1f) * amp);
					tortue = tortue.RotatedLocal(Vector3.Right, pitch);
					break;
				}
				case '>': tortue = tortue.RotatedLocal(Vector3.Forward, angle); break;
				case '<': tortue = tortue.RotatedLocal(Vector3.Forward, -angle); break;
				case 'A':
				case 'B':
					break;
				case 'L':
					if (!estCoupe)
					{
						if (IndexBotanique == LSystem_Botanique.IndexPin || IndexBotanique == LSystem_Botanique.IndexSapin)
							GenererAiguillesConifere(stFeuilles, tortue, AgeEnJours + 2, 0.88f * facteurAiguillesLod, IndexBotanique == LSystem_Botanique.IndexSapin);
						else GenererFeuillage(stFeuilles, tortue, AgeEnJours, 0.95f, facteurFeuillesLod);
					}
					break;
			}
		}
		_longueurBrancheMoyenne = nbSegmentsBranche > 0 ? sommeLongueurBranche / nbSegmentsBranche : longueurSegment;
		_epaisseurBrancheMoyenne = nbSegmentsBranche > 0 ? sommeEpaisseurBranche / nbSegmentsBranche : (epaisseurBase * 0.7f);

		if (!estCoupe)
		{
			// Évite l'effet "sucette" au sommet du bouleau.
			if (IndexBotanique == LSystem_Botanique.IndexBouleau || IndexBotanique == LSystem_Botanique.IndexPin || IndexBotanique == LSystem_Botanique.IndexSapin || IndexBotanique == LSystem_Botanique.IndexJungle)
				if (IndexBotanique == LSystem_Botanique.IndexPin || IndexBotanique == LSystem_Botanique.IndexSapin)
					GenererAiguillesConifere(stFeuilles, tortue, AgeEnJours + 2, 0.95f * facteurAiguillesLod, IndexBotanique == LSystem_Botanique.IndexSapin);
				else
				{
					GenererFeuillagePetit(stFeuilles, tortue, Mathf.Max(1, AgeEnJours - 1), IndexBotanique == LSystem_Botanique.IndexJungle ? 1.32f : 0.82f, facteurFeuillesLod);
					if (IndexBotanique == LSystem_Botanique.IndexJungle)
					{
						float chanceLianeSommet = ChanceLianesJungleSelonAge(AgeEnJours) * 0.85f;
						if (Hash(SeedForme, 19877 + AgeEnJours) < chanceLianeSommet)
							GenererLianesJungle(stFeuilles, tortue, AgeEnJours, 1.05f);
					}
				}
			else GenererFeuillage(stFeuilles, tortue, AgeEnJours, 0.92f, facteurFeuillesLod);
		}

		stBois.GenerateNormals();
		// Pas de GenerateTangents (nécessite UV parfaits, inutile sans normal map)
		Mesh meshBois = stBois.Commit();
		_visuelBois.Mesh = meshBois;
		_visuelBois.MaterialOverride = ObtenirMaterielBois(IndexBotanique);

		stFeuilles.GenerateNormals();
		_visuelFeuillage.Mesh = stFeuilles.Commit();
		_visuelFeuillage.MaterialOverride = ObtenirMaterielFeuilles(IndexBotanique);

		// FIX CRITIQUE : Hitbox exacte pour permettre de marcher sous les branches et viser le tronc.
		_hitbox.Shape = meshBois != null && meshBois.GetFaces().Length > 0
			? meshBois.CreateTrimeshShape()
			: new BoxShape3D { Size = Vector3.One };
	}

	/// <summary>Segment cylindrique avec conicité (rayonStart → rayonEnd) pour transition douce, sans saut.</summary>
	private void GenererSegmentBranche(SurfaceTool st, Vector3 start, Vector3 end, Vector3 right, Vector3 forward, float rayonStart, float rayonEnd)
	{
		const int cotes = 8;
		Vector3[] pStart = new Vector3[cotes];
		Vector3[] pEnd = new Vector3[cotes];
		for (int i = 0; i < cotes; i++)
		{
			float a = (float)i / cotes * Mathf.Tau;
			Vector3 dir = (Mathf.Cos(a) * right + Mathf.Sin(a) * forward);
			pStart[i] = start + dir * rayonStart;
			pEnd[i] = end + dir * rayonEnd;
		}
		for (int i = 0; i < cotes; i++)
		{
			int n = (i + 1) % cotes;
			st.SetUV(new Vector2((float)i / cotes, 0)); st.AddVertex(pStart[i]);
			st.SetUV(new Vector2((float)n / cotes, 0)); st.AddVertex(pStart[n]);
			st.SetUV(new Vector2((float)n / cotes, 1)); st.AddVertex(pEnd[n]);

			st.SetUV(new Vector2((float)i / cotes, 0)); st.AddVertex(pStart[i]);
			st.SetUV(new Vector2((float)n / cotes, 1)); st.AddVertex(pEnd[n]);
			st.SetUV(new Vector2((float)i / cotes, 1)); st.AddVertex(pEnd[i]);
		}
	}

	/// <summary>Cluster de feuillage le long des branches — formes ovales, densité adaptée au rayon.</summary>
	private void GenererFeuillagePetit(SurfaceTool st, Transform3D tortue, int age, float tailleMul = 1f, float lodMul = 1f)
	{
		GenererFeuillesCaducTypePin(st, tortue, age, 0.40f * lodMul, tailleMul);
	}

	/// <summary>Aiguilles de conifère: pin (longues) ou sapin (plus courtes et plus fournies).</summary>
	private void GenererAiguillesConifere(SurfaceTool st, Transform3D tortue, int age, float densiteMul = 1f, bool estSapin = false)
	{
		Vector3 centre = tortue.Origin;
		Vector3 axe = tortue.Basis.Y.Normalized();
		Vector3 refPerp = tortue.Basis.X.Normalized();
		if (Mathf.Abs(refPerp.Dot(axe)) > 0.95f) refPerp = tortue.Basis.Z.Normalized();
		refPerp = (refPerp - axe * axe.Dot(refPerp)).Normalized();

		// Sapin: plus de points, aiguilles plus courtes et plus proches de la branche.
		int maxPoints = estSapin ? 18 : 14;
		int nPoints = Mathf.Clamp((int)((8 + age * 2) * densiteMul), 5, maxPoints);
		float rayonBranche = (estSapin ? 0.050f : 0.060f) + Mathf.Clamp(age * (estSapin ? 0.006f : 0.008f), 0f, estSapin ? 0.04f : 0.05f);
		float demiLongueurSpan = (estSapin ? 0.09f : 0.11f) + Mathf.Clamp(age * (estSapin ? 0.012f : 0.015f), 0f, estSapin ? 0.08f : 0.10f);
		for (int i = 0; i < nPoints; i++)
		{
			float theta = (float)i / nPoints * Mathf.Tau + Hash(SeedForme, 2200 + i) * 0.35f;
			Vector3 radial = (refPerp.Rotated(axe, theta)).Normalized();
			float offsetAxe = (Hash(SeedForme, 2300 + i) * 2f - 1f) * demiLongueurSpan;
			// Point d'ancrage SUR la branche (évite l'effet "nuage qui flotte").
			Vector3 ancre = centre + axe * offsetAxe + radial * rayonBranche;

			float largeur = (estSapin ? 0.038f : 0.050f) + Hash(SeedForme, 2400 + i) * (estSapin ? 0.026f : 0.036f);
			float longueur = (estSapin ? 0.25f : 0.42f) + Hash(SeedForme, 2500 + i) * (estSapin ? 0.12f : 0.18f);
			Vector3 dir = estSapin
				? (radial * 0.90f - axe * 0.12f).Normalized()
				: (radial * 0.88f - axe * 0.22f).Normalized();
			Vector3 tangent = dir.Cross(axe);
			if (tangent.LengthSquared() < 1e-5f) tangent = dir.Cross(refPerp);
			tangent = tangent.Normalized();

			Vector3 baseG = ancre - tangent * largeur;
			Vector3 baseD = ancre + tangent * largeur;
			Vector3 tipC = ancre + dir * longueur;
			Vector3 n = dir;

			st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(baseG);
			st.SetNormal(n); st.SetUV(new Vector2(1, 1)); st.AddVertex(baseD);
			st.SetNormal(n); st.SetUV(new Vector2(0.5f, 0)); st.AddVertex(tipC);
		}
	}

	/// <summary>Probabilité de lianes jungle selon âge: ~0 à 1 an, puis augmente avec le temps.</summary>
	private static float ChanceLianesJungleSelonAge(int age)
	{
		if (age <= 1) return 0.01f;   // quasi aucune à 1 an
		if (age == 2) return 0.18f;
		if (age == 3) return 0.34f;
		if (age == 4) return 0.50f;
		if (age == 5) return 0.62f;
		return Mathf.Clamp(0.62f + (age - 5) * 0.06f, 0.62f, 0.96f);
	}

	/// <summary>Lianes pendantes de canopée pour les arbres de jungle (mesh 3D tubulaire).</summary>
	private void GenererLianesJungle(SurfaceTool st, Transform3D tortue, int age, float densiteMul = 1f)
	{
		if (IndexBotanique != LSystem_Botanique.IndexJungle) return;
		float chance = ChanceLianesJungleSelonAge(age) * _progressionLianesJungle;
		Color couleurFeuillage = CouleurFeuillesArbre();
		st.SetColor(CouleurLianeJungle);

		Vector3 centre = tortue.Origin;
		Vector3 axe = tortue.Basis.Y.Normalized();
		Vector3 refPerp = tortue.Basis.X.Normalized();
		if (Mathf.Abs(refPerp.Dot(axe)) > 0.95f) refPerp = tortue.Basis.Z.Normalized();
		refPerp = (refPerp - axe * axe.Dot(refPerp)).Normalized();

		int n = Mathf.Clamp((int)((1 + age * 0.90f) * densiteMul), 1, 14);
		int nbAjoutees = 0;
		for (int i = 0; i < n; i++)
		{
			if (Hash(SeedForme, 16950 + i + age * 11) > chance) continue;
			float theta = (float)i / Mathf.Max(1, n) * Mathf.Tau + Hash(SeedForme, 17000 + i) * 0.62f;
			Vector3 radial = refPerp.Rotated(axe, theta).Normalized();
			Vector3 ancre = centre + radial * (0.10f + Hash(SeedForme, 17100 + i) * 0.18f);
			float longueur = (0.85f + Hash(SeedForme, 17200 + i) * (1.20f + age * 0.10f)) * Mathf.Lerp(0.30f, 1f, _progressionLianesJungle);
			float rayon = 0.030f + Hash(SeedForme, 17300 + i) * 0.016f;
			bool viserSol = Hash(SeedForme, 17750 + i + age * 9) < 0.62f;
			AjouterLianeTubulaire(st, ancre, longueur, rayon, radial, 17600 + i * 17, viserSol, CouleurLianeJungle);
			nbAjoutees++;
		}

		// Sécurité visuelle: garantit au moins une liane exploitable sur arbres non bébé.
		if (nbAjoutees == 0 && age >= 2 && chance > 0.08f)
		{
			Vector3 radial = refPerp.Rotated(axe, Hash(SeedForme, 18888 + age) * Mathf.Tau).Normalized();
			Vector3 ancre = centre + radial * 0.16f;
			float longueur = (1.10f + Hash(SeedForme, 18911 + age) * 1.35f) * Mathf.Lerp(0.35f, 1f, _progressionLianesJungle);
			float rayon = 0.036f;
			AjouterLianeTubulaire(st, ancre, longueur, rayon, radial, 18977 + age * 17, true, CouleurLianeJungle);
		}
		st.SetColor(couleurFeuillage);
	}

	/// <summary>Liane collée au tronc, plus courte, générée aléatoirement.</summary>
	private void GenererLianesTroncJungle(SurfaceTool st, Transform3D tortue, int age)
	{
		if (IndexBotanique != LSystem_Botanique.IndexJungle) return;
		float chance = ChanceLianesJungleSelonAge(age) * 0.72f * _progressionLianesJungle;
		if (Hash(SeedForme, 17400 + age) > chance) return;
		Color couleurFeuillage = CouleurFeuillesArbre();
		st.SetColor(CouleurLianeJungle);

		Vector3 centre = tortue.Origin;
		Vector3 radial = tortue.Basis.X.Normalized();
		if (radial.LengthSquared() < 1e-5f) radial = Vector3.Right;
		Vector3 ancre = centre + radial * (0.06f + Hash(SeedForme, 17470 + age) * 0.06f);
		float longueur = (0.55f + Hash(SeedForme, 17500 + age) * 0.75f) * Mathf.Lerp(0.30f, 1f, _progressionLianesJungle);
		float rayon = 0.022f + Hash(SeedForme, 17600 + age) * 0.010f;
		AjouterLianeTubulaire(st, ancre, longueur, rayon, radial, 18100 + age * 13, false, CouleurLianeJungle);
		st.SetColor(couleurFeuillage);
	}

	/// <summary>Liane 3D: tube courbé texturé (pas un simple plan).</summary>
	private void AjouterLianeTubulaire(SurfaceTool st, Vector3 ancre, float longueur, float rayonBase, Vector3 radial, int saltBase, bool viserSol, Color couleur)
	{
		// Pousse vers le sol: ajuste la longueur selon un raycast vertical.
		var space = GetWorld3D()?.DirectSpaceState;
		bool toucheSol = false;
		if (space != null)
		{
			Vector3 bas = ancre + Vector3.Down * 220f;
			var q = PhysicsRayQueryParameters3D.Create(ancre, bas);
			q.CollisionMask = 1;
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			var hit = space.IntersectRay(q);
			if (hit.Count > 0 && hit.ContainsKey("position"))
			{
				toucheSol = true;
				Vector3 sol = hit["position"].AsVector3();
				float distSol = Mathf.Max(0.10f, ancre.DistanceTo(sol) - 0.10f);
				if (viserSol)
				{
					// Certaines lianes vont vraiment près du sol.
					float cibleSol = distSol * Mathf.Lerp(0.88f, 0.98f, _progressionLianesJungle);
					longueur = Mathf.Clamp(cibleSol, 0.30f, distSol);
				}
				else
				{
					float cibleSol = distSol * Mathf.Lerp(0.65f, 0.92f, _progressionLianesJungle);
					longueur = Mathf.Clamp(Mathf.Max(longueur, cibleSol), 0.25f, distSol);
				}
			}
		}
		if (viserSol && !toucheSol)
			longueur = Mathf.Max(longueur, 6.5f);
		const int segments = 6;
		Vector3 prev = ancre;
		for (int s = 1; s <= segments; s++)
		{
			float t = (float)s / segments;
			float sway = (Hash(SeedForme, saltBase + s * 3) * 2f - 1f) * 0.06f;
			Vector3 offsetLateral = radial * (Mathf.Sin(t * Mathf.Pi) * 0.08f + sway);
			Vector3 curr = ancre + Vector3.Down * (longueur * t) + offsetLateral;
			float rayon = Mathf.Max(0.0025f, rayonBase * (1f - t * 0.55f));
			AjouterTubeSegmentLiane(st, prev, curr, rayon, 5, couleur);
			prev = curr;
		}
	}

	private static void AjouterTubeSegmentLiane(SurfaceTool st, Vector3 start, Vector3 end, float rayon, int cotes, Color couleur)
	{
		Vector3 axis = end - start;
		float len = axis.Length();
		if (len < 1e-4f) return;
		axis /= len;

		Vector3 upRef = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.92f ? Vector3.Right : Vector3.Up;
		Vector3 right = axis.Cross(upRef).Normalized();
		Vector3 forward = right.Cross(axis).Normalized();

		for (int i = 0; i < cotes; i++)
		{
			int n = (i + 1) % cotes;
			float a0 = (float)i / cotes * Mathf.Tau;
			float a1 = (float)n / cotes * Mathf.Tau;
			Vector3 d0 = (Mathf.Cos(a0) * right + Mathf.Sin(a0) * forward).Normalized();
			Vector3 d1 = (Mathf.Cos(a1) * right + Mathf.Sin(a1) * forward).Normalized();
			Vector3 s0 = start + d0 * rayon;
			Vector3 s1 = start + d1 * rayon;
			Vector3 e0 = end + d0 * rayon;
			Vector3 e1 = end + d1 * rayon;

			st.SetColor(couleur); st.SetNormal(d0); st.SetUV(new Vector2((float)i / cotes, 0)); st.AddVertex(s0);
			st.SetColor(couleur); st.SetNormal(d1); st.SetUV(new Vector2((float)n / cotes, 0)); st.AddVertex(s1);
			st.SetColor(couleur); st.SetNormal(d0); st.SetUV(new Vector2((float)i / cotes, 1)); st.AddVertex(e0);

			st.SetColor(couleur); st.SetNormal(d1); st.SetUV(new Vector2((float)n / cotes, 0)); st.AddVertex(s1);
			st.SetColor(couleur); st.SetNormal(d1); st.SetUV(new Vector2((float)n / cotes, 1)); st.AddVertex(e1);
			st.SetColor(couleur); st.SetNormal(d0); st.SetUV(new Vector2((float)i / cotes, 1)); st.AddVertex(e0);
		}
	}

	/// <summary>Feuilles de chêne/bouleau distribuées comme le pin, mais en lamelles larges (pas d'épines).</summary>
	private void GenererFeuillesCaducTypePin(SurfaceTool st, Transform3D tortue, int age, float densiteMul = 1f, float tailleMul = 1f)
	{
		Vector3 centre = tortue.Origin;
		Vector3 axe = tortue.Basis.Y.Normalized();
		Vector3 refPerp = tortue.Basis.X.Normalized();
		if (Mathf.Abs(refPerp.Dot(axe)) > 0.95f) refPerp = tortue.Basis.Z.Normalized();
		refPerp = (refPerp - axe * axe.Dot(refPerp)).Normalized();
		bool estChene = IndexBotanique == LSystem_Botanique.IndexChene;
		bool estJungle = IndexBotanique == LSystem_Botanique.IndexJungle;
		float maturiteJungle = estJungle ? Mathf.Clamp((age - 1f) / 6f, 0f, 1f) : 1f;
		float echelle = Mathf.Clamp(tailleMul, 0.55f, 1.95f);
		float coefLargeur = (estJungle ? 1.34f : (estChene ? 1.18f : 1.08f)) * echelle * (estJungle ? Mathf.Lerp(0.86f, 1f, maturiteJungle) : 1f);
		float coefLongueur = (estJungle ? 1.12f : (estChene ? 0.78f : 0.86f)) * echelle * (estJungle ? Mathf.Lerp(0.88f, 1f, maturiteJungle) : 1f);
		float rayonSphere = ((estJungle ? 0.44f : (estChene ? 0.44f : 0.38f)) * echelle + Mathf.Clamp(age * 0.015f, 0f, 0.10f)) * 1.90f;
		int couches = estChene ? 4 : 3;
		if (estJungle) couches = age <= 2 ? 2 : (age <= 4 ? 3 : 4);
		if (_lodFeuillageActuel < 0.70f) couches -= 1;
		if (_lodFeuillageActuel < 0.40f) couches -= 1;
		couches = Mathf.Clamp(couches, 1, 4);
		int pointsBase = Mathf.Clamp((int)((8 + age * 2) * densiteMul * (0.85f + echelle * 0.45f) * (estJungle ? 0.72f : 1.35f)), 5, estJungle ? 14 : 24);

		// Amas sphériques de cartes-feuilles: plus denses et volumétriques.
		for (int couche = 0; couche < couches; couche++)
		{
			int points = Mathf.Clamp((int)(pointsBase * (0.78f + couche * 0.22f)), 5, 20);
			float rayonCouche = rayonSphere * (0.50f + couche * 0.33f);
			float yawBase = Hash(SeedForme, 3200 + couche * 97) * Mathf.Tau;
			for (int i = 0; i < points; i++)
			{
				float t = (i + 0.5f) / points;
				float y = 1f - 2f * t;
				float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
				float theta = i * 2.3999632f + yawBase + Hash(SeedForme, 3300 + couche * 41 + i) * 0.35f;
				Vector3 local = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
				Vector3 dir = (tortue.Basis * local).Normalized();
				Vector3 ancre = centre + dir * rayonCouche;

				float largeur = (0.52f + Hash(SeedForme, 3700 + couche * 113 + i) * 0.22f) * coefLargeur;
				float longueur = (0.40f + Hash(SeedForme, 3800 + couche * 131 + i) * 0.16f) * coefLongueur;
				// Jungle utilise la même géométrie que chêne/bouleau (moins "carré/épine"), mais garde sa texture matériau dédiée.
				AjouterTouffeFeuilleTypeBuisson(st, ancre, dir, axe, refPerp, largeur, longueur, 5000 + couche * 257 + i * 31, centre, rayonSphere * 1.12f);

				// Petit amas dans le gros (volume plus naturel) sans le faire partout pour contenir le coût.
				if (_lodFeuillageActuel > 0.75f && Hash(SeedForme, 8600 + couche * 181 + i) > (estJungle ? Mathf.Lerp(0.90f, 0.78f, maturiteJungle) : 0.74f))
				{
					Vector3 ancreInterne = ancre - dir * (rayonCouche * 0.24f);
					AjouterTouffeFeuilleTypeBuisson(st, ancreInterne, dir, axe, refPerp, largeur * 0.62f, longueur * 0.60f, 9000 + couche * 293 + i * 47, centre, rayonSphere * 1.12f);
				}
			}
		}
	}

	private void AjouterTouffeFeuilleJungle(
		SurfaceTool st,
		Vector3 ancre,
		Vector3 dir,
		Vector3 axeRef,
		Vector3 axeFallback,
		float largeur,
		float longueur,
		int saltBase,
		Vector3 centreCouronne,
		float rayonMaxCouronne)
	{
		float yaw = Hash(SeedForme, saltBase + 17) * Mathf.Tau;
		float pitch = (Hash(SeedForme, saltBase + 31) * 2f - 1f) * 0.48f;
		Vector3 dirLeaf = dir.Rotated(axeRef, yaw);
		Vector3 sideAxis = dirLeaf.Cross(axeRef);
		if (sideAxis.LengthSquared() < 1e-5f) sideAxis = dirLeaf.Cross(axeFallback);
		if (sideAxis.LengthSquared() < 1e-5f) sideAxis = dirLeaf.Cross(Vector3.Right);
		sideAxis = sideAxis.Normalized();
		dirLeaf = dirLeaf.Rotated(sideAxis, pitch).Normalized();
		Vector3 centre = ancre + sideAxis * ((Hash(SeedForme, saltBase + 71) * 2f - 1f) * 0.04f);

		float demiDiag = Mathf.Max(largeur * 0.5f, longueur * 0.52f);
		Vector3 radial = centre - centreCouronne;
		float dist = radial.Length();
		if (dist + demiDiag > rayonMaxCouronne)
		{
			if (dist < 1e-4f) radial = dir;
			float cible = Mathf.Max(0f, rayonMaxCouronne - demiDiag * 0.90f);
			centre = centreCouronne + radial.Normalized() * cible;
		}

		AjouterFeuilleJungleLarge(st, centre, dirLeaf, axeRef, axeFallback, largeur, longueur);
		if (_lodFeuillageActuel > 0.45f && Hash(SeedForme, saltBase + 91) > 0.38f)
		{
			Vector3 dirLeaf2 = dirLeaf.Rotated(sideAxis, (Hash(SeedForme, saltBase + 97) * 2f - 1f) * 0.32f).Normalized();
			AjouterFeuilleJungleLarge(st, centre, dirLeaf2, sideAxis, axeRef, largeur * 0.78f, longueur * 0.72f);
		}
	}

	private static void AjouterFeuilleJungleLarge(SurfaceTool st, Vector3 centre, Vector3 dir, Vector3 axeRef, Vector3 axeFallback, float largeur, float longueur)
	{
		Vector3 droite = dir.Cross(axeRef);
		if (droite.LengthSquared() < 1e-5f) droite = dir.Cross(axeFallback);
		if (droite.LengthSquared() < 1e-5f) droite = dir.Cross(Vector3.Right);
		droite = droite.Normalized() * (largeur * 0.5f);
		Vector3 avant = dir * longueur;
		Vector3 p0 = centre - droite;
		Vector3 p1 = centre + droite;
		Vector3 p2 = centre - droite * 0.82f + avant;
		Vector3 p3 = centre + droite * 0.82f + avant;
		Vector3 n = dir;

		st.SetNormal(n); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
		st.SetNormal(n); st.SetUV(new Vector2(1, 1)); st.AddVertex(p1);
		st.SetNormal(n); st.SetUV(new Vector2(0, 0)); st.AddVertex(p2);
		st.SetNormal(n); st.SetUV(new Vector2(1, 1)); st.AddVertex(p1);
		st.SetNormal(n); st.SetUV(new Vector2(1, 0)); st.AddVertex(p3);
		st.SetNormal(n); st.SetUV(new Vector2(0, 0)); st.AddVertex(p2);
	}

	private void AjouterTouffeFeuilleTypeBuisson(
		SurfaceTool st,
		Vector3 ancre,
		Vector3 dir,
		Vector3 axeRef,
		Vector3 axeFallback,
		float largeur,
		float longueur,
		int saltBase,
		Vector3 centreCouronne,
		float rayonMaxCouronne)
	{
		// 1 à 2 cartes "blob", chacune en double couche (grande + petite).
		float seuilDouble = _lodFeuillageActuel > 0.75f ? 0.58f : (_lodFeuillageActuel > 0.45f ? 0.78f : 0.95f);
		int feuilles = 1; // Optimisation: moitié moins de micro-cartes, compensée par des feuilles plus grandes.
		for (int i = 0; i < feuilles; i++)
		{
			float yaw = Hash(SeedForme, saltBase + 17 + i) * Mathf.Tau;
			float pitch = (Hash(SeedForme, saltBase + 31 + i) * 2f - 1f) * 0.85f;
			Vector3 dirLeaf = dir.Rotated(axeRef, yaw);
			Vector3 sideAxis = dirLeaf.Cross(axeRef);
			if (sideAxis.LengthSquared() < 1e-5f) sideAxis = dirLeaf.Cross(axeFallback);
			if (sideAxis.LengthSquared() < 1e-5f) sideAxis = dirLeaf.Cross(Vector3.Right);
			sideAxis = sideAxis.Normalized();
			dirLeaf = dirLeaf.Rotated(sideAxis, pitch).Normalized();

			Vector3 upAxis = dirLeaf.Cross(sideAxis);
			if (upAxis.LengthSquared() < 1e-5f) upAxis = axeRef;
			upAxis = upAxis.Normalized();

			float w = largeur * (0.90f + Hash(SeedForme, saltBase + 47 + i) * 0.34f);
			float l = longueur * (0.88f + Hash(SeedForme, saltBase + 59 + i) * 0.30f);
			Vector3 centre =
				ancre
				+ sideAxis * ((Hash(SeedForme, saltBase + 71 + i) * 2f - 1f) * 0.07f)
				+ upAxis * ((Hash(SeedForme, saltBase + 79 + i) * 2f - 1f) * 0.06f);

			// Anti-feuilles volantes: clamp strict dans le volume de couronne.
			float demiDiag = Mathf.Max(w * 0.5f, l * 0.74f);
			Vector3 radial = centre - centreCouronne;
			float dist = radial.Length();
			if (dist + demiDiag > rayonMaxCouronne)
			{
				if (dist < 1e-4f) radial = dir;
				float cible = Mathf.Max(0f, rayonMaxCouronne - demiDiag * 0.92f);
				centre = centreCouronne + radial.Normalized() * cible;
			}

			// Couche principale (large) + couche secondaire (plus petite) pour un rendu "balle de feuilles".
			AjouterFeuillePerforeeTypeBuisson(st, centre, dirLeaf, axeRef, axeFallback, w, l);
			bool autoriser2e = _lodFeuillageActuel > 0.78f || (_lodFeuillageActuel > 0.42f && Hash(SeedForme, saltBase + 91 + i) > 0.62f);
			if (autoriser2e)
			{
				Vector3 dirLeaf2 = dirLeaf.Rotated(sideAxis, (Hash(SeedForme, saltBase + 97 + i) * 2f - 1f) * 0.45f).Normalized();
				AjouterFeuillePerforeeTypeBuisson(st, centre, dirLeaf2, sideAxis, axeRef, w * 0.72f, l * 0.66f);
			}
		}
	}

	private static void AjouterFeuillePerforeeTypeBuisson(SurfaceTool st, Vector3 centre, Vector3 dir, Vector3 axeRef, Vector3 axeFallback, float largeur, float longueur)
	{
		Vector3 droite = dir.Cross(axeRef);
		if (droite.LengthSquared() < 1e-5f) droite = dir.Cross(axeFallback);
		if (droite.LengthSquared() < 1e-5f) droite = dir.Cross(Vector3.Right);
		droite = droite.Normalized() * (largeur * 0.5f);
		Vector3 levee = dir * longueur;
		Vector3 pointes = levee * 0.50f;

		Vector3 departL = centre - droite;
		Vector3 departR = centre + droite;
		Vector3 milieuL = centre - droite * 0.62f + levee * 0.24f;
		Vector3 milieuR = centre + droite * 0.62f + levee * 0.24f;
		Vector3 sommetL = centre - droite * 0.24f + pointes;
		Vector3 sommetR = centre + droite * 0.24f + pointes;
		Vector3 sommetC = centre + levee * 0.74f;

		// Face avant
		st.SetNormal(dir); st.SetUV(new Vector2(0, 1)); st.AddVertex(departL);
		st.SetNormal(dir); st.SetUV(new Vector2(0.45f, 0.45f)); st.AddVertex(milieuL);
		st.SetNormal(dir); st.SetUV(new Vector2(0.35f, 0)); st.AddVertex(sommetL);
		st.SetNormal(dir); st.SetUV(new Vector2(1, 1)); st.AddVertex(departR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.65f, 0)); st.AddVertex(sommetR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.55f, 0.45f)); st.AddVertex(milieuR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.45f, 0.45f)); st.AddVertex(milieuL);
		st.SetNormal(dir); st.SetUV(new Vector2(0.55f, 0.45f)); st.AddVertex(milieuR);
		st.SetNormal(dir); st.SetUV(new Vector2(0.50f, 0.02f)); st.AddVertex(sommetC);

		// Pas de face arrière dupliquée: culling désactivé suffit, moitié moins de triangles.
	}

	/// <summary>Sphère de feuillage AAA : densité proportionnelle au rayon (pas de vide), feuilles ovales.</summary>
	private void GenererFeuillage(SurfaceTool st, Transform3D tortue, int age, float tailleMul = 1f, float lodMul = 1f)
	{
		float densite = (0.45f + Mathf.Clamp(age - 2, 0, 4) * 0.08f) * lodMul * 0.5f;
		GenererFeuillesCaducTypePin(st, tortue, age, densite, tailleMul * 2f);
	}

	private Color CouleurFeuillesArbre()
	{
		if (IndexBotanique == LSystem_Botanique.IndexPin) return new Color(0.12f, 0.25f, 0.15f); // Aiguilles sombres
		if (IndexBotanique == LSystem_Botanique.IndexSapin) return new Color(0.16f, 0.30f, 0.20f); // Vert froid plus réaliste
		if (IndexBotanique == LSystem_Botanique.IndexJungle) return new Color(0.11f, 0.43f, 0.21f); // Vert tropical saturé
		float h = Hash(SeedForme, 10);
		float h2 = Hash(SeedForme, 11);
		// Feuilles caduques: base verte + légère dérive olive/jaune pour casser le "vert plein".
		float rBase = IndexBotanique == LSystem_Botanique.IndexBouleau ? 0.20f : 0.17f;
		float gBase = IndexBotanique == LSystem_Botanique.IndexBouleau ? 0.56f : 0.52f;
		float bBase = IndexBotanique == LSystem_Botanique.IndexBouleau ? 0.13f : 0.11f;
		float r = rBase + h * 0.12f;
		float g = gBase + h * 0.25f;
		float b = bBase + h2 * 0.10f;
		return new Color(r, g, b);
	}
}
