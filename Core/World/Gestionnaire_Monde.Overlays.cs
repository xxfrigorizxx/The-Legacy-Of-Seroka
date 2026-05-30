using Godot;
using System;

/// <summary>
/// Overlays visuels : repere central, flou perceptif EmerukedesiParotaroma (palier 1) et transition immersive de portail.
/// Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: shaders, phases de transition et logique de declenchement/fondu identiques a l'historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void CreerRepereCentreEcran()
	{
		if (_repereCentreLayer != null && GodotObject.IsInstanceValid(_repereCentreLayer)) return;

		_repereCentreLayer = new CanvasLayer { Layer = 12 };
		AddChild(_repereCentreLayer);

		var root = new Control
		{
			Name = "RepereCentre",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		root.SetAnchorsPreset(Control.LayoutPreset.Center);
		root.CustomMinimumSize = new Vector2(22, 22);
		root.Size = root.CustomMinimumSize;
		root.Position = -root.Size * 0.5f;
		_repereCentreLayer.AddChild(root);

		var h = new ColorRect
		{
			Name = "LigneHorizontale",
			Color = new Color(1f, 1f, 1f, 0.9f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		h.SetAnchorsPreset(Control.LayoutPreset.Center);
		h.CustomMinimumSize = new Vector2(18, 2);
		h.Size = h.CustomMinimumSize;
		h.Position = -h.Size * 0.5f;
		root.AddChild(h);

		var v = new ColorRect
		{
			Name = "LigneVerticale",
			Color = new Color(1f, 1f, 1f, 0.9f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		v.SetAnchorsPreset(Control.LayoutPreset.Center);
		v.CustomMinimumSize = new Vector2(2, 18);
		v.Size = v.CustomMinimumSize;
		v.Position = -v.Size * 0.5f;
		root.AddChild(v);
	}

	private void CreerOverlayEmerukedesiParotaromaStage1()
	{
		if (_overlayEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_overlayEmerukedesiParotaromaStage1))
			return;

		_overlayEmerukedesiParotaromaStage1 = new CanvasLayer { Name = "Overlay_EmerukedesiParotaroma_Stage1", Layer = 49 };
		var rect = new ColorRect
		{
			Name = "EmerukedesiParotaromaRect",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rect.OffsetLeft = 0f;
		rect.OffsetTop = 0f;
		rect.OffsetRight = 0f;
		rect.OffsetBottom = 0f;
		rect.Color = Colors.White;

		var shader = new Shader();
		shader.Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec2 uv = SCREEN_UV;
	vec4 base = texture(screen_tex, uv);
	vec2 p = SCREEN_PIXEL_SIZE * mix(0.0, 5.0, clamp(strength, 0.0, 1.0));

	vec4 blur = base * 0.30;
	blur += texture(screen_tex, uv + vec2( p.x,  0.0)) * 0.14;
	blur += texture(screen_tex, uv + vec2(-p.x,  0.0)) * 0.14;
	blur += texture(screen_tex, uv + vec2( 0.0,  p.y)) * 0.14;
	blur += texture(screen_tex, uv + vec2( 0.0, -p.y)) * 0.14;
	blur += texture(screen_tex, uv + vec2( p.x,  p.y)) * 0.07;
	blur += texture(screen_tex, uv + vec2(-p.x,  p.y)) * 0.07;

	COLOR = mix(base, blur, clamp(strength, 0.0, 1.0));
}";

		_materiauEmerukedesiParotaromaStage1 = new ShaderMaterial { Shader = shader };
		_materiauEmerukedesiParotaromaStage1.SetShaderParameter("strength", 0.0f);
		rect.Material = _materiauEmerukedesiParotaromaStage1;
		_overlayEmerukedesiParotaromaStage1.AddChild(rect);
		_overlayEmerukedesiParotaromaStage1.Visible = false;
		AddChild(_overlayEmerukedesiParotaromaStage1);
	}

	private void AssurerOverlayPortailTransition()
	{
		if (_overlayPortailTransition != null && GodotObject.IsInstanceValid(_overlayPortailTransition)) return;
		_overlayPortailTransition = new CanvasLayer { Name = "Overlay_Portail_Transition", Layer = 51 };
		_rectAssombrissementPortail = new ColorRect
		{
			Name = "RectAssombrissementPortail",
			MouseFilter = Control.MouseFilterEnum.Stop,
			Color = new Color(0f, 0f, 0f, 0f)
		};
		_rectAssombrissementPortail.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_rectAssombrissementPortail.OffsetLeft = 0f;
		_rectAssombrissementPortail.OffsetTop = 0f;
		_rectAssombrissementPortail.OffsetRight = 0f;
		_rectAssombrissementPortail.OffsetBottom = 0f;
		_overlayPortailTransition.AddChild(_rectAssombrissementPortail);
		_rectEffetVitessePortail = new ColorRect
		{
			Name = "RectEffetVitessePortail",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color = new Color(1f, 1f, 1f, 0f),
			Modulate = new Color(1f, 1f, 1f, 0f)
		};
		_rectEffetVitessePortail.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_rectEffetVitessePortail.OffsetLeft = 0f;
		_rectEffetVitessePortail.OffsetTop = 0f;
		_rectEffetVitessePortail.OffsetRight = 0f;
		_rectEffetVitessePortail.OffsetBottom = 0f;
		var shaderVitesse = new Shader();
		shaderVitesse.Code = @"
shader_type canvas_item;
uniform float warp_strength : hint_range(0.0, 1.0) = 0.0;
uniform float line_density : hint_range(40.0, 380.0) = 165.0;
uniform float speed : hint_range(4.0, 90.0) = 38.0;

float hash1(float x) { return fract(sin(x * 127.1) * 43758.5453); }

void fragment()
{
	vec2 uv = UV;
	float rows = max(12.0, line_density);
	float row = floor(uv.y * rows);
	float seed = hash1(row + floor(TIME * 4.0));
	float xCenter = fract(seed + TIME * speed * 0.045);
	float width = mix(0.0018, 0.020, warp_strength);
	float dist = abs(uv.x - xCenter);
	float line = smoothstep(width, 0.0, dist);
	float fadeEdges = smoothstep(0.05, 0.40, uv.x) * smoothstep(1.0, 0.72, uv.x);
	float sparkle = 0.55 + 0.45 * hash1(row * 0.91 + floor(TIME * 18.0));
	float alpha = line * fadeEdges * sparkle * warp_strength;
	COLOR = vec4(vec3(1.0), alpha);
}
";
		_materiauEffetVitessePortail = new ShaderMaterial { Shader = shaderVitesse };
		_materiauEffetVitessePortail.SetShaderParameter("warp_strength", 0.0f);
		_rectEffetVitessePortail.Material = _materiauEffetVitessePortail;
		_overlayPortailTransition.AddChild(_rectEffetVitessePortail);
		_overlayPortailTransition.Visible = false;
		AddChild(_overlayPortailTransition);
	}

	private void CalculerPhasesTransitionPortail(float dureeTotaleSec, out float fadeIn, out float phaseVitesse, out float fadeOut)
	{
		float d = Mathf.Max(0.35f, dureeTotaleSec);
		fadeIn = Mathf.Clamp(d * 0.30f, 0.22f, 1.0f);
		fadeOut = Mathf.Clamp(d * 0.26f, 0.20f, 0.85f);
		phaseVitesse = Mathf.Max(0.10f, d - fadeIn - fadeOut);
	}

	/// <summary>Transition immersive portail : noir progressif, lignes de vitesse blanches, puis éclaircissement.</summary>
	public void AfficherAssombrissementPortailTransition(float dureeTotaleSec)
	{
		AssurerOverlayPortailTransition();
		if (_rectAssombrissementPortail == null || _rectEffetVitessePortail == null) return;
		if (_tweenTransitionPortail != null && GodotObject.IsInstanceValid(_tweenTransitionPortail))
			_tweenTransitionPortail.Kill();
		_overlayPortailTransition.Visible = true;
		_rectAssombrissementPortail.Color = new Color(0f, 0f, 0f, 0f);
		_rectEffetVitessePortail.Modulate = new Color(1f, 1f, 1f, 0f);
		_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", 0.0f);
		CalculerPhasesTransitionPortail(dureeTotaleSec, out float fadeIn, out float phaseVitesse, out float fadeOut);
		float demiVitesse = Mathf.Max(0.05f, phaseVitesse * 0.5f);
		Tween tween = CreateTween();
		_tweenTransitionPortail = tween;
		tween.TweenProperty(_rectAssombrissementPortail, "color", new Color(0f, 0f, 0f, 0.98f), fadeIn);
		tween.Parallel().TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0.14f), fadeIn);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 0.0f, 0.50f, fadeIn);
		tween.TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0.48f), demiVitesse);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 0.50f, 1.00f, demiVitesse);
		tween.TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0.24f), demiVitesse);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 1.00f, 0.35f, demiVitesse);
		tween.TweenProperty(_rectAssombrissementPortail, "color", new Color(0f, 0f, 0f, 0f), fadeOut);
		tween.Parallel().TweenProperty(_rectEffetVitessePortail, "modulate", new Color(1f, 1f, 1f, 0f), fadeOut);
		tween.Parallel().TweenMethod(Callable.From<float>(v =>
		{
			_materiauEffetVitessePortail?.SetShaderParameter("warp_strength", v);
		}), 0.35f, 0.0f, fadeOut);
		tween.TweenCallback(Callable.From(() =>
		{
			if (_overlayPortailTransition != null && GodotObject.IsInstanceValid(_overlayPortailTransition))
				_overlayPortailTransition.Visible = false;
			if (_rectEffetVitessePortail != null && GodotObject.IsInstanceValid(_rectEffetVitessePortail))
				_rectEffetVitessePortail.Modulate = new Color(1f, 1f, 1f, 0f);
		}));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RpcRecevoirAssombrissementPortail(float dureeSec)
	{
		AfficherAssombrissementPortailTransition(dureeSec);
	}

	/// <summary>Serveur : envoie l’effet d’assombrissement au peer cible (et localement si c’est l’hôte).</summary>
	public void DiffuserAssombrissementPortailAuxClients(long peerId, float dureeSec)
	{
		if (Multiplayer.HasMultiplayerPeer())
		{
			if (!Multiplayer.IsServer()) return;
			// Godot interdit RpcId vers soi-même quand CallLocal=false.
			if (peerId != Multiplayer.GetUniqueId())
				RpcId((int)peerId, nameof(RpcRecevoirAssombrissementPortail), dureeSec);
			else
				AfficherAssombrissementPortailTransition(dureeSec);
		}
		else
			AfficherAssombrissementPortailTransition(dureeSec);
	}

	private void ReinitialiserEmerukedesiParotaromaStage1()
	{
		_dernierYRemonteeAbysse = float.NaN;
		_yDepartMonteeAbysse = float.NaN;
		_monteeAbysseContinue = false;
		_secondesSansMonteeAbysse = 0.0;
		_emerukedesiParotaromaStage1Actif = false;
		_emerukedesiParotaromaStage1FonduSortieActif = false;
		_emerukedesiParotaromaStage1TempsFonduRestant = 0.0;
		if (_materiauEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_materiauEmerukedesiParotaromaStage1))
			_materiauEmerukedesiParotaromaStage1.SetShaderParameter("strength", 0.0f);
		if (_overlayEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_overlayEmerukedesiParotaromaStage1))
			_overlayEmerukedesiParotaromaStage1.Visible = false;
	}

	/// <summary>Mise à jour de la manifestation palier 1 de l'<see cref="EmerukedesiParotaroma"/> (remontée en zone négative uniquement).</summary>
	private void MettreAJourEmerukedesiParotaromaStage1(double delta)
	{
		if (_joueur == null || _dimensionLocaleActive != (int)DimensionJeu.Abysse)
		{
			ReinitialiserEmerukedesiParotaromaStage1();
			return;
		}

		float yActuel = _joueur.GlobalPosition.Y;
		if (yActuel >= 0f)
		{
			ReinitialiserEmerukedesiParotaromaStage1();
			_dernierYRemonteeAbysse = yActuel;
			return;
		}

		if (float.IsNaN(_dernierYRemonteeAbysse))
			_dernierYRemonteeAbysse = yActuel;

		float deltaY = yActuel - _dernierYRemonteeAbysse;
		bool remonteeEffective = deltaY > SeuilProgressionMonteeAbysseMetres;
		bool redescenteNette = deltaY < -SeuilRedescenteNetteAbysseMetres;

		if (float.IsNaN(_yDepartMonteeAbysse))
			_yDepartMonteeAbysse = yActuel;

		float intensite = 0f;
		if (remonteeEffective)
		{
			_monteeAbysseContinue = true;
			_secondesSansMonteeAbysse = 0.0;

			bool gainSuffisant = !float.IsNaN(_yDepartMonteeAbysse)
				&& (yActuel - _yDepartMonteeAbysse) >= SeuilDeclenchementRemonteeAbysseMetres;
			if (!_emerukedesiParotaromaStage1Actif && gainSuffisant)
				_emerukedesiParotaromaStage1Actif = true;

			if (_emerukedesiParotaromaStage1Actif)
			{
				_emerukedesiParotaromaStage1FonduSortieActif = false;
				_emerukedesiParotaromaStage1TempsFonduRestant = DureeFonduEmerukedesiParotaromaStage1Sec;
				intensite = 1f;
			}
		}
		else
		{
			_monteeAbysseContinue = false;
			_secondesSansMonteeAbysse += delta;

			if (redescenteNette)
				_yDepartMonteeAbysse = yActuel;

			if (_emerukedesiParotaromaStage1Actif
				&& !_emerukedesiParotaromaStage1FonduSortieActif
				&& _secondesSansMonteeAbysse >= DelaiArretMonteeAvantFonduParotaromaSec)
			{
				_emerukedesiParotaromaStage1FonduSortieActif = true;
				_emerukedesiParotaromaStage1TempsFonduRestant = DureeFonduEmerukedesiParotaromaStage1Sec;
			}

			if (_emerukedesiParotaromaStage1FonduSortieActif)
			{
				_emerukedesiParotaromaStage1TempsFonduRestant = Math.Max(0.0, _emerukedesiParotaromaStage1TempsFonduRestant - delta);
				intensite = (float)Mathf.Clamp((float)(_emerukedesiParotaromaStage1TempsFonduRestant / DureeFonduEmerukedesiParotaromaStage1Sec), 0f, 1f);
				if (_emerukedesiParotaromaStage1TempsFonduRestant <= 0.0)
				{
					_emerukedesiParotaromaStage1FonduSortieActif = false;
					_emerukedesiParotaromaStage1Actif = false;
					_yDepartMonteeAbysse = yActuel;
					intensite = 0f;
				}
			}
			else if (_emerukedesiParotaromaStage1Actif)
			{
				// Pendant le délai anti-yoyo, on conserve l'intensité pleine pour éviter une coupure visuelle brutale.
				intensite = 1f;
			}
		}

		if (_materiauEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_materiauEmerukedesiParotaromaStage1))
			_materiauEmerukedesiParotaromaStage1.SetShaderParameter("strength", intensite);
		if (_overlayEmerukedesiParotaromaStage1 != null && GodotObject.IsInstanceValid(_overlayEmerukedesiParotaromaStage1))
			_overlayEmerukedesiParotaromaStage1.Visible = intensite > 0.001f;
		_dernierYRemonteeAbysse = yActuel;
	}
}
