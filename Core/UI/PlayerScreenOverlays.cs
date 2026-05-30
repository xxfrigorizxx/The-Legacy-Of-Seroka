using Godot;
using System;
using System.Collections.Generic;

public partial class Joueur
{
    private void AssurerOverlayDegatsRouge()
    {
        if (_overlayDegatsRouge != null && GodotObject.IsInstanceValid(_overlayDegatsRouge))
            return;

        CanvasLayer hudInventaire = GetParent()?.GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (hudInventaire == null)
            return;

        _overlayDegatsRouge = hudInventaire.GetNodeOrNull<ColorRect>("OverlayDegatsRouge");
        if (_overlayDegatsRouge == null)
        {
            _overlayDegatsRouge = new ColorRect
            {
                Name = "OverlayDegatsRouge",
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                ZIndex = 300
            };
            _overlayDegatsRouge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _overlayDegatsRouge.OffsetLeft = 0f;
            _overlayDegatsRouge.OffsetTop = 0f;
            _overlayDegatsRouge.OffsetRight = 0f;
            _overlayDegatsRouge.OffsetBottom = 0f;
            hudInventaire.AddChild(_overlayDegatsRouge);
        }

        _materiauOverlayDegatsRouge = _overlayDegatsRouge.Material as ShaderMaterial;
        if (_materiauOverlayDegatsRouge == null)
        {
            var shader = new Shader();
            shader.Code = @"
shader_type canvas_item;

uniform vec4 edge_color : source_color = vec4(0.82, 0.06, 0.06, 1.0);
uniform float edge_size = 0.13;
uniform float softness = 0.08;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;

void fragment()
{
    vec2 uv = UV;
    float d = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    float border = 1.0 - smoothstep(edge_size, edge_size + softness, d);
    COLOR = vec4(edge_color.rgb, border * intensity);
}
";
            _materiauOverlayDegatsRouge = new ShaderMaterial { Shader = shader };
            _overlayDegatsRouge.Material = _materiauOverlayDegatsRouge;
        }
        _materiauOverlayDegatsRouge.SetShaderParameter("intensity", 0f);
    }

    private void AssurerOverlayVisionTete()
    {
        if (_overlayVisionTete != null && GodotObject.IsInstanceValid(_overlayVisionTete))
            return;

        CanvasLayer hudInventaire = GetParent()?.GetNodeOrNull<CanvasLayer>("Gestionnaire_Monde/HUD_Inventaire");
        if (hudInventaire == null)
            return;

        _overlayVisionTete = hudInventaire.GetNodeOrNull<ColorRect>("OverlayVisionTete");
        if (_overlayVisionTete == null)
        {
            _overlayVisionTete = new ColorRect
            {
                Name = "OverlayVisionTete",
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                ZIndex = 280
            };
            _overlayVisionTete.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            hudInventaire.AddChild(_overlayVisionTete);
        }

        _materiauOverlayVisionTete = _overlayVisionTete.Material as ShaderMaterial;
        if (_materiauOverlayVisionTete == null)
        {
            var shader = new Shader();
            shader.Code = @"
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_strength : hint_range(0.0, 4.0) = 0.0;
uniform float darken_strength : hint_range(0.0, 1.0) = 0.0;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.0;

void fragment()
{
    vec2 uv = SCREEN_UV;
    vec2 px = SCREEN_PIXEL_SIZE * blur_strength * 4.0;
    vec3 col = texture(screen_texture, uv).rgb * 0.2270270270;
    col += texture(screen_texture, uv + vec2(px.x, 0.0)).rgb * 0.1945945946;
    col += texture(screen_texture, uv - vec2(px.x, 0.0)).rgb * 0.1945945946;
    col += texture(screen_texture, uv + vec2(0.0, px.y)).rgb * 0.1945945946;
    col += texture(screen_texture, uv - vec2(0.0, px.y)).rgb * 0.1945945946;
    col += texture(screen_texture, uv + px).rgb * 0.1216216216;
    col += texture(screen_texture, uv - px).rgb * 0.1216216216;
    col += texture(screen_texture, uv + vec2(px.x, -px.y)).rgb * 0.1216216216;
    col += texture(screen_texture, uv + vec2(-px.x, px.y)).rgb * 0.1216216216;

    col *= 1.0 - darken_strength * 0.72;
    float d = distance(uv, vec2(0.5));
    col *= 1.0 - vignette_strength * smoothstep(0.2, 0.92, d);

    float alpha = max(blur_strength * 0.08, darken_strength * 0.35);
    COLOR = vec4(col, alpha);
}
";
            _materiauOverlayVisionTete = new ShaderMaterial { Shader = shader };
            _overlayVisionTete.Material = _materiauOverlayVisionTete;
        }

        _materiauOverlayVisionTete.SetShaderParameter("blur_strength", 0f);
        _materiauOverlayVisionTete.SetShaderParameter("darken_strength", 0f);
        _materiauOverlayVisionTete.SetShaderParameter("vignette_strength", 0f);
    }

    private float ObtenirRatioPvTete() => ObtenirRatioPvSectionCorps(SectionCorpsTete);

    /// <summary>Tête ≤ 50 % PV : vision floue et assombrie (intensité croît quand les PV baissent).</summary>
    private void MettreAJourEffetVisionTete()
    {
        if (_mortJoueurEnCours)
        {
            if (_overlayVisionTete != null && GodotObject.IsInstanceValid(_overlayVisionTete))
                _overlayVisionTete.Visible = false;
            return;
        }

        float ratioTete = ObtenirRatioPvTete();
        float severite = 0f;
        if (ratioTete <= RatioPvSeuilFelureMembre)
            severite = 1f - Mathf.Clamp(ratioTete / RatioPvSeuilFelureMembre, 0f, 1f);

        if (severite <= 0.001f)
        {
            if (_overlayVisionTete != null && GodotObject.IsInstanceValid(_overlayVisionTete))
                _overlayVisionTete.Visible = false;
            return;
        }

        AssurerOverlayVisionTete();
        if (_overlayVisionTete == null || !GodotObject.IsInstanceValid(_overlayVisionTete) || _materiauOverlayVisionTete == null)
            return;

        _overlayVisionTete.Visible = true;
        _materiauOverlayVisionTete.SetShaderParameter("blur_strength", severite * IntensiteMaxFlouVisionTete);
        _materiauOverlayVisionTete.SetShaderParameter("darken_strength", severite * IntensiteMaxObscurcissementVisionTete);
        _materiauOverlayVisionTete.SetShaderParameter("vignette_strength", severite * 0.85f);
    }

    private void JouerFlashDegatsBovin()
    {
        AssurerOverlayDegatsRouge();
        if (_overlayDegatsRouge == null || !GodotObject.IsInstanceValid(_overlayDegatsRouge) || _materiauOverlayDegatsRouge == null)
            return;

        _tweenOverlayDegatsRouge?.Kill();
        _overlayDegatsRouge.Visible = true;
        _materiauOverlayDegatsRouge.SetShaderParameter("intensity", IntensiteMaxFlashDegatsBovin);
        _tweenOverlayDegatsRouge = CreateTween();
        _tweenOverlayDegatsRouge.TweenProperty(_materiauOverlayDegatsRouge, "shader_parameter/intensity", 0f, DureeFlashDegatsBovinSec);
        _tweenOverlayDegatsRouge.Finished += () =>
        {
            if (_overlayDegatsRouge != null && GodotObject.IsInstanceValid(_overlayDegatsRouge))
                _overlayDegatsRouge.Visible = false;
        };
    }
}
