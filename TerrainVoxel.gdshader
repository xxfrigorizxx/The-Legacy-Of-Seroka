shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back;
uniform sampler2DArray texture_array : source_color, filter_linear_mipmap, repeat_enable;
uniform float triplanar_scale = 0.2;
uniform float lod_texture_start = 140.0;
uniform float lod_texture_end = 520.0;
uniform float lod_far_mip = 4.0;
uniform float lod_steps = 8.0;
uniform float lod_step_blend = 0.75;
uniform float lod_start_jitter = 32.0;
uniform float lumiere_ambiante = 0.08;
uniform float douceur_ombre = 0.50;
uniform float neutralisation_sable = 0.48;

varying vec3 uv1_power_normal;
varying vec3 uv1_triplanar_pos;
varying float raw_id;
varying vec3 world_pos;
varying float v_temperature;
varying float v_humidite;

// Fonction mathématique de pseudo-aléatoire très légère
float random(vec2 st) {
	return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
}

// Bruit de Valeur (Value Noise) 2D ultra-rapide pour le GPU
float noise(vec2 st) {
	vec2 i = floor(st);
	vec2 f = fract(st);
	float a = random(i);
	float b = random(i + vec2(1.0, 0.0));
	float c = random(i + vec2(0.0, 1.0));
	float d = random(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void vertex() {
	vec3 normal = abs(NORMAL);
	normal = pow(normal, vec3(4.0));
	uv1_power_normal = normal / dot(normal, vec3(1.0));

	// Récupération de la position globale pour le calcul mathématique
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	uv1_triplanar_pos = world_pos * triplanar_scale;
	// On garde l'ID brut sans l'arrondir ici
	raw_id = COLOR.r * 255.0;
	v_temperature = (COLOR.g * 2.0) - 1.0; // Retour à -1.0 -> 1.0
	v_humidite = (COLOR.b * 2.0) - 1.0;    // Retour à -1.0 -> 1.0
}

void fragment() {
	float cam_dist = distance(CAMERA_POSITION_WORLD, world_pos);
	float jitter = (noise(world_pos.xz * 0.03) - 0.5) * lod_start_jitter;
	float start_d = lod_texture_start + jitter;
	float end_d = lod_texture_end + jitter;
	float lod_t_base = clamp((cam_dist - start_d) / max(1.0, end_d - start_d), 0.0, 1.0);

	// Progression multi-étapes (10-12+) avec fondu entre étapes pour masquer tout "pop".
	float steps = max(2.0, floor(lod_steps + 0.5));
	float idx = lod_t_base * (steps - 1.0);
	float i0 = floor(idx);
	float frac_step = idx - i0;
	float step_soft = smoothstep(0.5 - 0.5 * lod_step_blend, 0.5 + 0.5 * lod_step_blend, frac_step);
	float t0 = i0 / (steps - 1.0);
	float t1 = min(i0 + 1.0, steps - 1.0) / (steps - 1.0);
	float lod_t = mix(t0, t1, step_soft);
	float detail_t = 1.0 - lod_t * 0.50;

	// 1. PERTURBATION DES TRANSITIONS (Bruit à haute fréquence)
	// On crée un chaos de +/- 0.4 sur l'ID aux frontières des matériaux
	float edge_noise = (noise(world_pos.xz * 4.0) - 0.5) * 0.8 * detail_t;
	float material_id = round(raw_id + edge_noise);
	// Limite 31 pour inclure Tronc (30) et Feuille (31) — éviter que le bois devienne Glace (9)
	material_id = clamp(material_id, 0.0, 31.0);

	// Tronc Chêne (ID 30) et Feuilles (ID 31) : couleurs fixes (évite dépassement texture_array limitée à 0-9)
	bool is_bois = (material_id >= 29.5 && material_id < 30.5);
	bool is_feuilles = (material_id >= 30.5);
	float tex_layer = clamp(material_id, 0.0, 9.0);

	// Projection Triplanaire standard
	vec2 uv_x = uv1_triplanar_pos.zy;
	vec2 uv_y = uv1_triplanar_pos.xz;
	vec2 uv_z = uv1_triplanar_pos.xy;
	vec4 tex_x = texture(texture_array, vec3(uv_x, tex_layer));
	vec4 tex_y = texture(texture_array, vec3(uv_y, tex_layer));
	vec4 tex_z = texture(texture_array, vec3(uv_z, tex_layer));
	vec4 tex_triplanar = tex_x * uv1_power_normal.x + tex_y * uv1_power_normal.y + tex_z * uv1_power_normal.z;

	vec2 uv_dom = uv_y;
	float max_axis = uv1_power_normal.y;
	if (uv1_power_normal.x > max_axis) { max_axis = uv1_power_normal.x; uv_dom = uv_x; }
	if (uv1_power_normal.z > max_axis) { uv_dom = uv_z; }
	vec4 tex_lod = textureLod(texture_array, vec3(uv_dom, tex_layer), lod_far_mip);
	float tex_lod_mix = lod_t * 0.38;
	vec4 albedo_tex = mix(tex_triplanar, tex_lod, tex_lod_mix);

	if (is_bois) albedo_tex.rgb = vec3(0.4, 0.28, 0.18);
	if (is_feuilles) albedo_tex.rgb = vec3(0.25, 0.55, 0.22);
	vec3 final_color = albedo_tex.rgb;

	bool est_herbe = (material_id == 1.0);
	bool est_boue = (material_id == 7.0);
	bool est_aride = (material_id == 6.0);
	bool est_sable = (material_id == 3.0);

	// Sable: neutralise la dominante orange vers une teinte plus beige naturelle.
	if (est_sable) {
		vec3 hsv_sable = rgb2hsv(final_color);
		hsv_sable.y = clamp(hsv_sable.y * (1.0 - neutralisation_sable), 0.0, 1.0);
		hsv_sable.x = mix(hsv_sable.x, 0.135, neutralisation_sable * 0.42);
		final_color = hsv2rgb(hsv_sable);
	}

	// Loin: on coupe presque tous les coûts de détail shader.
	if (detail_t > 0.01) {
		// 2. MACRO-VARIATION ANTI-DAMIER (Bruit à basse fréquence)
		// Crée des zones d'ombres et de lumières géantes sur le terrain
		float macro_noise = noise(world_pos.xz * 0.05);
		final_color *= (0.65 + macro_noise * 0.5 * detail_t); // Modulation atténuée au loin

		float facteur_humidite = 0.0;
		if (est_herbe || est_boue || est_aride) {
			// --- MUTATION SÉMANTIQUE (FERTILITÉ & CLIMAT) —---
			float facteur_humidite_macro = clamp((v_humidite + 1.0) * 0.5, 0.0, 1.0);
			// Micro-variation mondiale (pas liée aux chunks) pour casser l'uniformité visuelle.
			float humidite_micro = noise(world_pos.xz * 0.012 + vec2(73.1, -21.7));
			facteur_humidite = clamp(facteur_humidite_macro * 0.82 + humidite_micro * 0.18 * detail_t, 0.0, 1.0);
		}

		// Herbe (ID 1) : palette pilotée par climat (texture base N/B ou colorée).
		if (est_herbe) {
			vec3 base = final_color;
			float h = facteur_humidite;
			float t = clamp((v_temperature + 1.0) * 0.5, 0.0, 1.0);
			float jungle = smoothstep(0.58, 0.92, t) * smoothstep(0.55, 0.95, h);
			float micro = noise(world_pos.xz * 0.08 + vec2(11.3, -6.9)) - 0.5;
			vec3 hsvBase = rgb2hsv(base);
			// Si la texture est N/B (saturation faible), on applique la palette plus fortement.
			float forceNB = 1.0 - smoothstep(0.10, 0.30, hsvBase.y);
			float forcePalette = mix(0.30, 0.82, forceNB);
			// Teintes cibles: sec -> jaune, normal -> vert, jungle -> vert vif.
			float hueBiome = mix(0.16, mix(0.30, 0.36, jungle), h);
			float satBiome = mix(0.36, 0.80, h);
			satBiome = mix(satBiome, satBiome * 1.08, jungle);

			vec3 hsvOut = hsvBase;
			hsvOut.x = mix(hsvBase.x, hueBiome, forcePalette);
			hsvOut.y = clamp(mix(hsvBase.y, satBiome, forcePalette), 0.0, 1.0);
			float vMul = mix(1.05, 0.90, h);
			vMul = mix(vMul, vMul * 0.95, jungle);
			hsvOut.z = clamp(hsvBase.z * vMul + micro * 0.045 * detail_t, 0.0, 1.0);
			vec3 couleurBiome = hsv2rgb(hsvOut);

			// Conserve le relief: la texture pilote encore les hautes/basses fréquences.
			float lum = dot(base, vec3(0.299, 0.587, 0.114));
			float detailLum = clamp((lum - 0.5) * 1.22 + 0.5, 0.0, 1.0);
			vec3 coloreRelief = couleurBiome * (0.72 + detailLum * 0.54);
			final_color = mix(base, coloreRelief, forcePalette);
		}
		// Boue (ID 7) : sec = grisâtre, humide = de plus en plus brun
		if (est_boue) {
			// Logique demandée: humide => plus gris (pas brun foncé).
			vec3 hsv = rgb2hsv(final_color);
			float h = facteur_humidite;
			float micro = noise(world_pos.xz * 0.09 + vec2(-17.2, 8.6)) - 0.5;
			hsv.x = fract(hsv.x + mix(0.012, -0.006, h) + micro * 0.006 * detail_t);
			hsv.y = clamp(hsv.y * mix(0.75, 0.28, h), 0.0, 1.0); // Plus humide => plus désaturé (gris)
			hsv.z = clamp(hsv.z * mix(1.02, 0.72, h), 0.0, 1.0);
			vec3 teinte_boue = hsv2rgb(hsv);
			vec3 gris_humide = vec3(dot(teinte_boue, vec3(0.299, 0.587, 0.114)));
			final_color = mix(teinte_boue, gris_humide, h * 0.55);
		}
		// Terre aride (ID 6) : sec = grisâtre, humide = assombri
		if (est_aride) {
			// Même logique: humide => plus gris, sans virer brun.
			vec3 hsv = rgb2hsv(final_color);
			float h = facteur_humidite;
			float micro = noise(world_pos.xz * 0.07 + vec2(5.4, 14.9)) - 0.5;
			hsv.x = fract(hsv.x + mix(-0.01, 0.0, h) + micro * 0.004 * detail_t);
			hsv.y = clamp(hsv.y * mix(0.62, 0.20, h), 0.0, 1.0);
			hsv.z = clamp(hsv.z * mix(1.08, 0.76, h), 0.0, 1.0);
			vec3 teinte_aride = hsv2rgb(hsv);
			vec3 gris_humide = vec3(dot(teinte_aride, vec3(0.299, 0.587, 0.114)));
			final_color = mix(teinte_aride, gris_humide, h * 0.48);
		}
	}

	ALBEDO = final_color;
	ROUGHNESS = 0.85;
}

void light() {
	float ndotl = max(dot(NORMAL, LIGHT), 0.0);
	float half_lambert = ndotl * 0.5 + 0.5;
	float diffus_lisse = smoothstep(0.18 - douceur_ombre * 0.15, 0.92, half_lambert);
	float diffus = max(lumiere_ambiante, diffus_lisse);

	// Garde une lumière majoritairement neutre pour éviter la dérive orange du sable.
	float lum_light = dot(LIGHT_COLOR, vec3(0.2126, 0.7152, 0.0722));
	vec3 lumiere_rgb = mix(vec3(lum_light), LIGHT_COLOR, 0.22);
	DIFFUSE_LIGHT += ALBEDO * lumiere_rgb * diffus * ATTENUATION;

	// Sol/roche/sable volontairement mats : aucun reflet speculaire.
}
