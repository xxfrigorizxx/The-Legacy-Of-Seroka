shader_type spatial;
// Eau transparente : blend_mix + depth_draw_opaque (ne s'écrit pas dans le depth → la depth texture lue
// ci-dessous = passe opaque = le terrain au fond). cull_back : on ne voit pas l'envers de la nappe.
render_mode blend_mix, depth_draw_opaque, cull_back;

// Teinte de l'eau PEU PROFONDE (bords). Conservé : défini aussi depuis le C#
// (SetShaderParameter("albedo_color", ...)) — ne pas renommer/retirer (erreur runtime sinon).
uniform vec4 albedo_color : source_color = vec4(0.10, 0.30, 0.60, 0.60);
// Teinte de l'eau PROFONDE : devient bleu sombre et opaque avec la profondeur (cache le fond → « vraie eau »).
uniform vec4 couleur_eau_profonde : source_color = vec4(0.02, 0.10, 0.22, 1.0);
// Distance (m) sur laquelle l'eau passe de translucide (bord) à opaque (fond).
uniform float profondeur_opacite = 3.0;
// Plancher d'opacité (bord, eau peu profonde) et opacité en eau profonde.
uniform float alpha_bord = 0.60;
uniform float alpha_fond = 0.96;
// Reflet de surface accru en angle rasant (fresnel).
uniform float force_fresnel = 0.5;
// Vaguelettes : perturbent la normale (spéculaire/fresnel vivant) sans déformer le maillage.
uniform float amplitude_vagues = 0.04;
uniform float vitesse_vagues = 0.25;

// Profondeur de la géométrie OPAQUE derrière l'eau (le terrain, désormais opaque, y figure).
uniform sampler2D texture_profondeur : hint_depth_texture;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// --- Épaisseur d'eau = (fond opaque) - (surface), reconstruite en espace vue. ---
	// Godot 4 (Vulkan/Forward+) : la profondeur du depth buffer est DÉJÀ en [0,1] pour z (NDC Vulkan),
	// donc ndc.z = d (et SURTOUT PAS d*2-1, qui est la convention OpenGL et donnait un fondu faux).
	float d = texture(texture_profondeur, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, d);
	vec4 vue = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	vue.xyz /= vue.w;
	float prof_fond = -vue.z;          // distance caméra -> fond opaque
	float prof_surface = -VERTEX.z;    // distance caméra -> surface (VERTEX = espace vue en fragment)
	float epaisseur = max(0.0, prof_fond - prof_surface);
	float t = clamp(epaisseur / max(0.01, profondeur_opacite), 0.0, 1.0);

	// --- Vaguelettes : normale (espace vue) légèrement perturbée autour de la verticale. ---
	float tm = TIME * vitesse_vagues;
	vec2 p = world_pos.xz;
	float nx = sin(p.x * 0.45 + tm) + 0.6 * sin(p.y * 0.70 - tm * 1.3);
	float nz = sin(p.y * 0.40 - tm) + 0.6 * sin(p.x * 0.65 + tm * 1.1);
	vec3 n_monde = normalize(vec3(amplitude_vagues * nx, 1.0, amplitude_vagues * nz));
	NORMAL = normalize((VIEW_MATRIX * vec4(n_monde, 0.0)).xyz);

	// --- Fresnel : plus opaque/réfléchissant en angle rasant. ---
	float fres = pow(1.0 - clamp(dot(normalize(-VERTEX), NORMAL), 0.0, 1.0), 3.0);

	ALBEDO = mix(albedo_color.rgb, couleur_eau_profonde.rgb, t);
	ALPHA = clamp(mix(alpha_bord, alpha_fond, t) + fres * force_fresnel, 0.0, 1.0);
	ROUGHNESS = 0.04;
	METALLIC = 0.0;
	SPECULAR = 0.6;
}
