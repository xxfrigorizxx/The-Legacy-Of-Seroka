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

    private ColorRect _overlayEauImmersion;
    private ShaderMaterial _materiauOverlayEauImmersion;
    private CanvasLayer _calqueEauImmersion;
    private float _intensiteFiltreEau;

    private void AssurerOverlayEauImmersion()
    {
        if (_overlayEauImmersion != null && GodotObject.IsInstanceValid(_overlayEauImmersion))
            return;

        // Calque DÉDIÉ (Layer très haut), enfant du joueur : aucune dépendance au HUD (qui pouvait être introuvable
        // ou dont l'overlay finissait 0×0). Un CanvasLayer rend toujours par-dessus la 3D.
        if (_calqueEauImmersion == null || !GodotObject.IsInstanceValid(_calqueEauImmersion))
        {
            // Layer 10 : au-dessus de la 3D, SOUS le HUD (15) pour ne pas teinter la barre d'inventaire.
            _calqueEauImmersion = new CanvasLayer { Name = "CalqueEauImmersion", Layer = 10 };
            AddChild(_calqueEauImmersion);
        }

        _overlayEauImmersion = new ColorRect
        {
            Name = "OverlayEauImmersion",
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _calqueEauImmersion.AddChild(_overlayEauImmersion);
        // Anchors ET offsets en plein écran (une fois dans l'arbre) : garantit une taille = viewport (pas un rect 0×0).
        _overlayEauImmersion.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        GD.Print($"ZERO-K [FiltreEau] overlay créé : taille={_overlayEauImmersion.Size} dansArbre={_overlayEauImmersion.IsInsideTree()} calqueLayer={_calqueEauImmersion.Layer}");

        _materiauOverlayEauImmersion = _overlayEauImmersion.Material as ShaderMaterial;
        if (_materiauOverlayEauImmersion == null)
        {
            var shader = new Shader();
            // Volontairement SANS screen_texture : teinte bleue procedurale = rendu garanti (aucune dependance
            // a la texture d'ecran ni back-buffer copy, qui peuvent silencieusement ne rien afficher).
            shader.Code = @"
shader_type canvas_item;

uniform vec4 teinte_profonde : source_color = vec4(0.09, 0.28, 0.46, 1.0);
uniform float intensite : hint_range(0.0, 1.0) = 0.0;
uniform float temps = 0.0;

void fragment()
{
    // Vignette : bords plus denses (lumiere qui baisse en profondeur).
    float d = distance(SCREEN_UV, vec2(0.5));
    float vig = 0.5 + 0.6 * smoothstep(0.20, 0.98, d);
    // Caustiques discretes : l'eau bouge.
    float caustic = 0.05 * (sin(SCREEN_UV.x * 36.0 + temps * 1.4) + sin(SCREEN_UV.y * 28.0 - temps * 1.1));
    float a = clamp((vig + caustic) * intensite, 0.0, 0.92);
    COLOR = vec4(teinte_profonde.rgb, a);
}
";
            _materiauOverlayEauImmersion = new ShaderMaterial { Shader = shader };
            _overlayEauImmersion.Material = _materiauOverlayEauImmersion;
        }

        _materiauOverlayEauImmersion.SetShaderParameter("intensite", 0f);
    }

    private bool _etatCameraSousLeauPrecedent;
    private ulong _dernierLogFiltreEauMs;

    /// <summary>True si la caméra active (les yeux) est sous l'eau : déclenche le filtre sous-marin.</summary>
    private bool CameraEstSousLeau()
    {
        if (_gestionnaireMonde == null)
            return false;
        Camera3D cam = _camera != null && GodotObject.IsInstanceValid(_camera) ? _camera : _cameraFps;
        if (cam == null || !GodotObject.IsInstanceValid(cam) || !cam.IsInsideTree())
            return false;

        Vector3 p = cam.GlobalPosition;
        // Présence d'eau au niveau des yeux (voxel exact OU voisinage immédiat).
        bool exact = _gestionnaireMonde.EstPointDansEauExact(p);
        bool immerge = _gestionnaireMonde.EstPointImmergeEau(p);
        float surfaceOcean = _gestionnaireMonde.ObtenirNiveauSurfaceEau();
        // Surface de référence : locale si le corps nage (lacs en altitude), sinon le niveau d'océan.
        float surfaceRef = _dernierEtatDansEau ? _derniereSurfaceEau : surfaceOcean;

        // Les YEUX sont sous l'eau seulement si (a) de l'eau est présente ici ET (b) la caméra est NETTEMENT sous
        // la surface RÉELLE. On n'utilise PLUS le voxel « exact » seul comme déclencheur : il devient vrai dès que
        // les yeux entrent dans la cellule d'eau (jusqu'à ~0,6 m AU-DESSUS de la surface marching-cubes), ce qui
        // déclenchait le filtre bien trop tôt (tête encore hors de l'eau).
        bool eauPresente = exact || immerge;
        bool sousLeau = eauPresente && p.Y <= surfaceRef - 0.06f;

        // Diagnostic : transition immédiate + rappel périodique tant qu'on est près de l'eau (à retirer après).
        ulong now = Time.GetTicksMsec();
        bool transition = sousLeau != _etatCameraSousLeauPrecedent;
        bool procheEau = _dernierEtatDansEau || immerge || p.Y <= surfaceOcean + 3f;
        if (transition || (procheEau && now - _dernierLogFiltreEauMs >= 800))
        {
            _etatCameraSousLeauPrecedent = sousLeau;
            _dernierLogFiltreEauMs = now;
            GD.Print($"ZERO-K [FiltreEau] sousLeau={sousLeau} | camY={p.Y:0.00} surfOcean={surfaceOcean:0.00} surfLocale={_derniereSurfaceEau:0.00} exact={exact} immerge={immerge} nageCorps={_dernierEtatDansEau} intensite={_intensiteFiltreEau:0.00}");
        }
        return sousLeau;
    }

    /// <summary>Filtre bleu plein écran quand les yeux passent sous la surface (fondu doux à l'entrée/sortie).</summary>
    private void MettreAJourFiltreEauImmersion(float dt)
    {
        float cible = (!_mortJoueurEnCours && CameraEstSousLeau()) ? 1f : 0f;
        _intensiteFiltreEau = Mathf.MoveToward(_intensiteFiltreEau, cible, dt * 4.5f);

        if (_intensiteFiltreEau <= 0.001f)
        {
            if (_overlayEauImmersion != null && GodotObject.IsInstanceValid(_overlayEauImmersion))
                _overlayEauImmersion.Visible = false;
            return;
        }

        AssurerOverlayEauImmersion();
        if (_overlayEauImmersion == null || !GodotObject.IsInstanceValid(_overlayEauImmersion) || _materiauOverlayEauImmersion == null)
            return;

        _overlayEauImmersion.Visible = true;
        _materiauOverlayEauImmersion.SetShaderParameter("intensite", _intensiteFiltreEau);
        _materiauOverlayEauImmersion.SetShaderParameter("temps", (float)Time.GetTicksMsec() * 0.001f);
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
