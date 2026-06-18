using Godot;
using System;

/// <summary>Cycle solaire/lunaire basé sur UtcNow + décalage du fuseau horaire. Contrôle soleil, lune et atmosphère.</summary>
public partial class Cycle_Solaire : Node
{
	/// <summary>Émis à chaque passage à minuit (nouveau jour). Croissance des arbres.</summary>
	[Signal] public delegate void NouveauJourEventHandler();

	[Export] private DirectionalLight3D _soleil;
	[Export] private DirectionalLight3D _lune; // Deuxième lampe (bleutée, ombre activée)
	[Export] private WorldEnvironment _environnement; // Pour brouillard et ambiance
	[Export] private int _renderDistanceBrouillardChunks = 23;
	[Export] private int _tailleChunkBrouillard = 16;
	[Export] private int _avanceBrouillardChunks = 2;
	[Export(PropertyHint.Range, "0.0,1.0,0.001")] private float _energieLuneMinNuit = 0.012f;
	[Export(PropertyHint.Range, "-1.0,2.0,0.001")] private float _energieLuneMaxNuit = -1f; // -1 = reprendre l'énergie configurée sur le nœud Lune.

	/// <summary>Décalage en heures de la dimension actuelle. Monde 1 = 0, Monde 2 = +6, etc.</summary>
	private double _decalageMondeHeures = 0.0;
	private bool _forcerHeureFixeDimension;
	private double _heureFixeDimensionHeures = 12.0;
	/// <summary>Pour détecter le passage minuit (nouveau jour).</summary>
	private double _pourcentageJourneePrecedent = -1.0;
	/// <summary>Vrai pendant le chargement initial du terrain autour du joueur (cache soleil/lune pour éviter le "flash" avant stabilité).</summary>
	private bool _chargementMondeActif;
	/// <summary>Référence jour pour inject brouillard vol. (désactivé en surface — évite luminosité liée à la caméra).</summary>
	private float _injectAmbiantVolBrouillardJour = 0.08f;
	/// <summary>Texture ciel étoilé générée une fois (ProceduralSkyMaterial.SkyCover).</summary>
	private bool _textureEtoilesAppliquee;
	/// <summary>Évite de spammer la console si le matériau de ciel n’est pas procédural (scène modifiée / upgrade moteur).</summary>
	private bool _alerteTypeSkyMaterialEmise;
	/// <summary>Énergie de référence lue sur le nœud Lune (éditeur), utilisée si <see cref="_energieLuneMaxNuit"/> = -1.</summary>
	private float _energieLuneEditeur = 0.055f;
	/// <summary>Évite l'oscillation jour/nuit autour de l'horizon (pompage lumineux).</summary>
	private bool _modeNuitActif = true;
	private float _hauteurSoleilCourante;

	private const float SeuilEntreeModeNuit = -0.03f;
	private const float SeuilSortieModeNuit = 0.02f;
	private const float VitesseLissageLumiere = 4.2f;

	/// <summary>RPC appelé par le Serveur une seule fois quand le joueur spawn ou traverse un portail.</summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void DefinirDecalageHoraire(double heuresDeDecalage)
	{
		_decalageMondeHeures = heuresDeDecalage;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	public void ConfigurerHeureFixeDimension(int forcerHeureFixe, double heureFixe)
	{
		_forcerHeureFixeDimension = forcerHeureFixe != 0;
		_heureFixeDimensionHeures = Mathf.Clamp((float)heureFixe, 0f, 23.99f);
	}

	/// <summary>Permet au gestionnaire de masquer les luminaires célestes pendant le bootstrap des chunks de spawn.</summary>
	public void DefinirChargementMondeActif(bool actif)
	{
		_chargementMondeActif = actif;
		if (_chargementMondeActif)
			AppliquerEtatSansSoleilNiLune();
	}

	/// <summary>Vrai si le soleil est au-dessus de l'horizon (refroidissement céramique au sol).</summary>
	public bool EstJourEnsoleille(float seuilHauteur = 0.12f) =>
		!_chargementMondeActif && !_modeNuitActif && _hauteurSoleilCourante >= seuilHauteur;

	private void AppliquerEtatSansSoleilNiLune()
	{
		if (_soleil != null)
		{
			_soleil.Visible = false;
			_soleil.LightEnergy = 0f;
			_soleil.Set("sky_mode", 1);
		}
		if (_lune != null)
		{
			_lune.Visible = false;
			_lune.LightEnergy = 0f;
			_lune.Set("sky_mode", 1);
			_lune.Set("light_volumetric_fog_energy", 0.0f);
		}
	}

	public override void _Ready()
	{
		// Fallback : résolution des nœuds par chemin si les Exports sont vides
		if (_soleil == null)
		{
			_soleil = GetParent()?.GetNodeOrNull<DirectionalLight3D>("Soleil");
			if (_soleil == null) GD.PrintErr("ZERO-K ALERTE : Nœud 'Soleil' introuvable !");
		}
		if (_lune == null)
		{
			_lune = GetParent()?.GetNodeOrNull<DirectionalLight3D>("Lune");
			if (_lune == null) GD.PrintErr("ZERO-K ALERTE : Nœud 'Lune' introuvable !");
		}
		if (_environnement == null)
		{
			_environnement = GetParent()?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
			if (_environnement == null) GD.PrintErr("ZERO-K ALERTE : Nœud 'WorldEnvironment' introuvable !");
		}
		// Pas de disque lunaire dans le ciel (évite l'effet "deuxième soleil").
		if (_lune != null)
		{
			_energieLuneEditeur = Mathf.Max(0f, _lune.LightEnergy);
			_lune.Set("sky_mode", 1);
			// Empêche tout halo/spot blanc lié au brouillard volumétrique pour la lune.
			_lune.Set("light_volumetric_fog_energy", 0.0f);
		}

		AppliquerDistanceBrouillardProgressive();
		ConfigurerOmbresDirectionnelles();
		if (_environnement?.Environment != null)
		{
			var env = _environnement.Environment;
			// Brouillard volumétrique = calculé le long du regard → désactivé surface (monde_zero).
			if (env.VolumetricFogEnabled)
				_injectAmbiantVolBrouillardJour = Mathf.Max(0.01f, env.VolumetricFogAmbientInject);
		}
		AppliquerTextureEtoilesSiPossible();
		GD.Print("Moteur Thermodynamique : EN LIGNE.");
	}

	/// <summary>Ombres directionnelles soleil/lune (shadow maps natives Godot — PBR standard sur le terrain).</summary>
	private void ConfigurerOmbresDirectionnelles()
	{
		// normalBias modéré : trop élevé = ombres des arbres/roches invisibles sur le sol.
		// Distance courte (200 m) = forte densité de texels = ombres nettes près du joueur.
		AppliquerProfilOmbresDirectionnelles(_soleil, distanceMax: 200f, bias: 0.02f, normalBias: 0.6f);
		AppliquerProfilOmbresDirectionnelles(_lune, distanceMax: 200f, bias: 0.03f, normalBias: 0.7f);
	}

	private static void AppliquerProfilOmbresDirectionnelles(DirectionalLight3D lumiere, float distanceMax, float bias, float normalBias)
	{
		if (lumiere == null)
			return;
		lumiere.ShadowEnabled = true;
		lumiere.ShadowReverseCullFace = false;
		lumiere.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
		lumiere.DirectionalShadowBlendSplits = true;
		lumiere.DirectionalShadowMaxDistance = distanceMax;
		lumiere.ShadowBias = bias;
		lumiere.ShadowNormalBias = normalBias;
		// Pénombre douce (bords d'ombre non crénelés), indépendante de la caméra.
		lumiere.ShadowBlur = 1.0f;
	}

	/// <summary>Sans texture <see cref="ProceduralSkyMaterial.SkyCover"/>, les étoiles du ciel procédural ne s’affichent pas.</summary>
	private void AppliquerTextureEtoilesSiPossible()
	{
		if (_textureEtoilesAppliquee || _environnement?.Environment?.Sky?.SkyMaterial is not ProceduralSkyMaterial skyMat)
			return;
		if (skyMat.SkyCover != null)
		{
			_textureEtoilesAppliquee = true;
			return;
		}
		const int w = 2048;
		const int h = 1024;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(Colors.Black);
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < 14000; i++)
		{
			int x = (int)(rng.Randf() * w);
			int y = (int)(rng.Randf() * h);
			float b = rng.RandfRange(0.25f, 1f);
			img.SetPixel(x, y, new Color(b, b, b, 1f));
		}
		// Quelques étoiles plus grosses (constellations légères)
		for (int i = 0; i < 900; i++)
		{
			int x = (int)(rng.Randf() * (w - 2)) + 1;
			int y = (int)(rng.Randf() * (h - 2)) + 1;
			float b = rng.RandfRange(0.55f, 1f);
			var c = new Color(b, b, b, 1f);
			img.SetPixel(x, y, c);
			// Halos plus marqués sur l’axe vertical (perception « défilant » haut-bas sur la voûte).
			img.SetPixel(x, y - 1, c * 0.58f);
			img.SetPixel(x, y + 1, c * 0.58f);
			img.SetPixel(x - 1, y, c * 0.35f);
			img.SetPixel(x + 1, y, c * 0.35f);
		}
		var tex = ImageTexture.CreateFromImage(img);
		skyMat.SkyCover = tex;
		_textureEtoilesAppliquee = true;
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
		if (_environnement?.Environment == null) return;

		float tailleChunkMetres = Mathf.Max(1f, _tailleChunkBrouillard);
		int chunkDebut = Mathf.Max(1, _renderDistanceBrouillardChunks - _avanceBrouillardChunks);
		float debut = chunkDebut * tailleChunkMetres;
		float fin = (_renderDistanceBrouillardChunks + 2) * tailleChunkMetres; // Laisse une marge pour un fondu doux.

		var env = _environnement.Environment;
		// Godot 4.x : pas de propriété fog_depth_enabled — brouillard profondeur = FogEnabled + FogMode Depth.
		env.FogEnabled = true;
		env.FogMode = Godot.Environment.FogModeEnum.Depth;
		env.FogDepthBegin = debut;
		env.FogDepthEnd = Mathf.Max(debut + tailleChunkMetres, fin);
		env.FogDepthCurve = 1.15f;
	}

	private float CalculerEnergieLuneNuit(float hauteurSoleil)
	{
		float intensiteNuit = Mathf.Clamp(-hauteurSoleil, 0f, 1f);
		float energieMax = _energieLuneMaxNuit >= 0f
			? _energieLuneMaxNuit
			: Mathf.Max(0.02f, _energieLuneEditeur);
		float energieMin = Mathf.Clamp(_energieLuneMinNuit, 0f, energieMax);
		// Courbe douce: la lune commence faiblement après le coucher et monte progressivement.
		float t = Mathf.Pow(intensiteNuit, 0.72f);
		return Mathf.Lerp(energieMin, energieMax, t);
	}

	private static float LisserVers(float courant, float cible, double delta, float vitesse)
	{
		float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, vitesse) * (float)delta);
		return Mathf.Lerp(courant, cible, t);
	}

	public override void _Process(double delta)
	{
		if (!IsInsideTree()) return; // GARROT SPATIAL : le Soleil ne tourne pas si l'univers s'effondre.

		AppliquerTextureEtoilesSiPossible();

		double pourcentageJournee;
		if (_forcerHeureFixeDimension)
		{
			pourcentageJournee = _heureFixeDimensionHeures / 24.0;
		}
		else
		{
			DateTime heureDansCeMonde = DateTime.UtcNow.AddHours(_decalageMondeHeures);
			TimeSpan heureActuelle = heureDansCeMonde.TimeOfDay;
			pourcentageJournee = heureActuelle.TotalHours / 24.0;
		}

		// Détection passage minuit → signal NouveauJour (croissance arbres)
		if (!_forcerHeureFixeDimension && _pourcentageJourneePrecedent >= 0.98 && pourcentageJournee < 0.02)
			EmitSignal("NouveauJour");
		_pourcentageJourneePrecedent = pourcentageJournee;

		// Calcul de l'angle X (Midi = -90°)
		float angleX = 90f - (float)(pourcentageJournee * 360.0);
		// Hauteur : 1 = Zénith (Midi), 0 = Horizon, -1 = Nadir (Minuit)
		float hauteurSoleil = Mathf.Sin(Mathf.DegToRad(-angleX));
		_hauteurSoleilCourante = hauteurSoleil;
		if (_modeNuitActif)
		{
			if (hauteurSoleil >= SeuilSortieModeNuit)
				_modeNuitActif = false;
		}
		else if (hauteurSoleil <= SeuilEntreeModeNuit)
		{
			_modeNuitActif = true;
		}

		// Toujours rafraîchir l’atmosphère / le ciel procédural, même si le nœud Soleil manque (sinon ciel figé noir).
		if (_soleil == null)
		{
			MettreAJourAtmosphereEtCiel(hauteurSoleil, delta);
			return;
		}

		// Rotation céleste : même pendant le chargement (overlay), sinon le ciel procédural / étoiles semblent « figés ».
		_soleil.RotationDegrees = new Vector3(angleX, -30f, 0f);
		if (_lune != null)
			_lune.RotationDegrees = new Vector3(angleX + 180f, -30f, 0f);

		if (_chargementMondeActif)
		{
			// Luminaires masqués (anti-flash spawn), mais le ciel procédural / brouillard suivent l'heure.
			AppliquerEtatSansSoleilNiLune();
			MettreAJourAtmosphereEtCiel(hauteurSoleil, delta);
			return;
		}

		// GD.Print("Heure Universelle Relative : " + heureDansCeMonde.ToString("HH:mm:ss") + " | Angle : " + angleX);
		_soleil.Visible = true;

		// --- GESTION DE LA NUIT ET DE L'ATMOSPHÈRE ---
		// Le soleil s'éteint sous l'horizon, la lune s'allume
		// ProceduralSkyMaterial affiche 1 disque par DirectionalLight → sky_mode=1 (LightOnly) exclut du ciel
		if (_modeNuitActif)
		{
			// Crépuscule/aurore: même en mode nuit on garde une faible projection lumineuse
			// pour éviter la coupure brutale "soleil visible mais monde noir".
			float energieCrepuscule = Mathf.Clamp((hauteurSoleil + 0.12f) * 1.65f, 0f, 0.55f);
			_soleil.LightEnergy = LisserVers(_soleil.LightEnergy, energieCrepuscule, delta, VitesseLissageLumiere);
			// Soleil éteint sous l'horizon : on coupe sa shadow map (Godot rendait 4 splits d'ombre pour une lumière nulle).
			// Aucun effet visuel (une lumière à énergie ~0 ne projette aucune ombre visible), gros gain GPU nocturne.
			_soleil.ShadowEnabled = _soleil.LightEnergy > 0.01f;
			_soleil.Set("sky_mode", hauteurSoleil > 0f ? 0 : 1); // Disque visible seulement quand au-dessus de l'horizon.
			if (_lune != null)
			{
				_lune.Visible = true;
				float energieLuneCible = CalculerEnergieLuneNuit(hauteurSoleil);
				_lune.LightEnergy = LisserVers(_lune.LightEnergy, energieLuneCible, delta, VitesseLissageLumiere);
				// Idem pour la lune quand elle se couche / lune absente : pas de shadow map pour une énergie nulle.
				_lune.ShadowEnabled = _lune.LightEnergy > 0.01f;
				_lune.Set("sky_mode", 1); // LightOnly : pas de disque blanc parasite.
				_lune.Set("light_volumetric_fog_energy", 0.0f);
			}
		}
		else
		{
			// Jour : soleil fort, lune éteinte (sinon elle remplit les ombres du soleil).
			float energieSoleilCible = Mathf.Clamp(hauteurSoleil * 2.4f, 0.25f, 2.35f);
			_soleil.LightEnergy = LisserVers(_soleil.LightEnergy, energieSoleilCible, delta, VitesseLissageLumiere);
			_soleil.ShadowEnabled = true; // Jour : soleil dominant, ombres directionnelles actives.
			_soleil.Set("sky_mode", 0); // LightAndSky (soleil visible via les nodes existants)
			if (_lune != null)
			{
				_lune.Visible = false;
				_lune.ShadowEnabled = false;
				_lune.LightEnergy = 0f;
				_lune.Set("sky_mode", 1); // CRITIQUE : LightOnly = pas de disque dans le ciel
				_lune.Set("light_volumetric_fog_energy", 0.0f);
			}
		}

		MettreAJourAtmosphereEtCiel(hauteurSoleil, delta);
	}

	/// <summary>Ambiance, brouillard et couleurs du <see cref="ProceduralSkyMaterial"/> selon la hauteur du soleil.</summary>
	private void MettreAJourAtmosphereEtCiel(float hauteurSoleil, double delta)
	{
		if (_environnement == null || _environnement.Environment == null)
			return;
		// APISARA : heure fixe + ciel/brouillard portés par la ressource Environment du Gestionnaire_Abysse (assignée au WE racine).
		// Ne pas réécraser chaque frame sinon le ciel « bleu APISARA » est remplacé par l’interpolation jour/nuit standard.
		if (_forcerHeureFixeDimension)
			return;

		var envGlobal = _environnement.Environment;
		// Réparation si un réglage ou une ressource a basculé le fond hors Sky (ciel entièrement noir).
		if (envGlobal.BackgroundMode != Godot.Environment.BGMode.Sky)
			envGlobal.BackgroundMode = Godot.Environment.BGMode.Sky;

		float intensiteJour = Mathf.Clamp(hauteurSoleil + 0.11f, 0f, 1f); // 0 = nuit, 1 = jour
		// Courbe rééquilibrée : nuit plus sombre qu'avant, mais ciel encore lisible.
		float intensiteJourLisse = Mathf.Pow(intensiteJour, 1.35f);
		bool crepuscule = hauteurSoleil > -0.15f && hauteurSoleil < 0.35f; // Lever/coucher
		float intensiteCrepuscule = crepuscule ? 1f - Mathf.Abs(hauteurSoleil - 0.1f) / 0.45f : 0f;

		Color couleurBrouillardJour = new Color(0.6f, 0.7f, 0.8f);
		Color couleurBrouillardNuit = new Color(0.01f, 0.01f, 0.03f);

		// Ambiance ciel = fill léger (pas un second soleil qui efface les ombres directionnelles).
		envGlobal.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
		float ambientEnergyCible = Mathf.Lerp(0.03f, 0.14f, intensiteJourLisse);
		float ambientSkyCible = Mathf.Lerp(0.55f, 0.82f, intensiteJourLisse);
		envGlobal.AmbientLightEnergy = LisserVers(envGlobal.AmbientLightEnergy, ambientEnergyCible, delta, VitesseLissageLumiere);
		envGlobal.AmbientLightSkyContribution = LisserVers(envGlobal.AmbientLightSkyContribution, ambientSkyCible, delta, VitesseLissageLumiere);
		if (envGlobal.VolumetricFogEnabled)
		{
			float fogAmbientInjectCible = Mathf.Lerp(0.015f, _injectAmbiantVolBrouillardJour, intensiteJourLisse);
			envGlobal.VolumetricFogAmbientInject =
				LisserVers(envGlobal.VolumetricFogAmbientInject, fogAmbientInjectCible, delta, VitesseLissageLumiere);
		}

		if (envGlobal.FogEnabled)
		{
			Color cibleFog = couleurBrouillardNuit.Lerp(couleurBrouillardJour, intensiteJour);
			envGlobal.FogLightColor = envGlobal.FogLightColor.Lerp(cibleFog, 1f - Mathf.Exp(-VitesseLissageLumiere * (float)delta));
		}

		// Ciel dynamique : jour (bleu) ↔ crépuscule (orange/rose) ↔ nuit (sombre)
		var sky = envGlobal.Sky;
		if (sky?.SkyMaterial is not ProceduralSkyMaterial skyMat)
		{
			if (!_alerteTypeSkyMaterialEmise)
			{
				_alerteTypeSkyMaterialEmise = true;
				string nomType = sky?.SkyMaterial?.GetType().Name ?? "null";
				GD.PrintErr($"ZERO-K : Sky.SkyMaterial attendu = ProceduralSkyMaterial, reçu = {nomType}. Ciel dynamique non mis à jour.");
			}
			return;
		}

		// Couleurs jour
		Color cielHautJour = new Color(0.38f, 0.5f, 0.65f);   // Bleu ciel
		Color cielHorizonJour = new Color(0.55f, 0.62f, 0.75f);
		Color solHorizonJour = new Color(0.5f, 0.55f, 0.6f);

		// Couleurs crépuscule (lever/coucher)
		Color cielHautCrepuscule = new Color(0.4f, 0.25f, 0.5f);   // Violet/rose
		Color cielHorizonCrepuscule = new Color(0.95f, 0.45f, 0.25f); // Orange
		Color solHorizonCrepuscule = new Color(0.6f, 0.3f, 0.2f);

		// Couleurs nuit (pas du noir TV : garde un dégradé pour le procédural + étoiles).
		Color cielHautNuit = new Color(0.045f, 0.05f, 0.14f);
		Color cielHorizonNuit = new Color(0.06f, 0.065f, 0.18f);
		Color solHorizonNuit = new Color(0.07f, 0.07f, 0.2f);

		// Interpolation : nuit → crépuscule → jour
		Color cielHaut, cielHorizon, solHorizon;
		if (intensiteJour > 0.5f)
		{
			// Jour ou fin de crépuscule
			float t = Mathf.Clamp((intensiteJour - 0.5f) * 2f, 0f, 1f);
			cielHaut = cielHautCrepuscule.Lerp(cielHautJour, t);
			cielHorizon = cielHorizonCrepuscule.Lerp(cielHorizonJour, t);
			solHorizon = solHorizonCrepuscule.Lerp(solHorizonJour, t);
		}
		else if (intensiteCrepuscule > 0f)
		{
			// Crépuscule actif
			cielHaut = cielHautNuit.Lerp(cielHautCrepuscule, intensiteCrepuscule);
			cielHorizon = cielHorizonNuit.Lerp(cielHorizonCrepuscule, intensiteCrepuscule);
			solHorizon = solHorizonNuit.Lerp(solHorizonCrepuscule, intensiteCrepuscule);
		}
		else
		{
			// Nuit pure
			cielHaut = cielHautNuit;
			cielHorizon = cielHorizonNuit;
			solHorizon = solHorizonNuit;
		}

		skyMat.SkyTopColor = cielHaut;
		skyMat.SkyHorizonColor = cielHorizon;
		skyMat.GroundHorizonColor = solHorizon;
		skyMat.GroundBottomColor = solHorizonNuit.Lerp(new Color(0.2f, 0.17f, 0.13f), intensiteJourLisse);
		// Évite le "ciel noir total" la nuit.
		float skyEnergyCible = Mathf.Lerp(0.24f, 1f, intensiteJourLisse);
		float groundEnergyCible = Mathf.Lerp(0.14f, 1f, intensiteJourLisse);
		skyMat.SkyEnergyMultiplier = LisserVers(skyMat.SkyEnergyMultiplier, skyEnergyCible, delta, VitesseLissageLumiere);
		skyMat.GroundEnergyMultiplier = LisserVers(skyMat.GroundEnergyMultiplier, groundEnergyCible, delta, VitesseLissageLumiere);
		// Étoiles : découplées du léger +0.11 sur intensiteJour (sinon ciel noir sans patch d’étoiles).
		float alphaEtoiles = Mathf.Clamp((-0.055f - hauteurSoleil) / 0.36f, 0f, 1f);
		skyMat.SkyCoverModulate = new Color(0.95f, 0.98f, 1f, alphaEtoiles);
	}
}
