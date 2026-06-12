shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
/// 0 = quartz ordinaire, 1 = quartz pur (translucide)
uniform float mode_pur = 0.0;
uniform vec4 teinte_base : source_color = vec4(0.96, 0.95, 0.93, 1.0);

varying vec3 pos_local;
varying vec3 norm_local;

float hash21(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * noise2(p);
		p *= 2.08;
		a *= 0.5;
	}
	return v;
}

void vertex() {
	pos_local = VERTEX;
	norm_local = NORMAL;
}

void fragment() {
	vec3 n = normalize(norm_local);
	vec4 tex = texture(albedo_tex, UV);

	// Variation 3D pour casser l'unicolore sur le mesh
	vec2 p3 = pos_local.xz * 5.5 + pos_local.y * 3.2;
	float grain3d = fbm(p3 + UV * 6.0);
	float veins3d = fbm(p3 * 0.65 + grain3d);

	float pur = clamp(mode_pur, 0.0, 1.0);
	float milky = mix(0.78 + grain3d * 0.18, 0.88 + grain3d * 0.10, pur);
	milky *= mix(0.90 + veins3d * 0.12, 0.95 + veins3d * 0.05, pur);

	vec3 col = tex.rgb * teinte_base.rgb * milky;
	col = mix(col, vec3(1.0), grain3d * mix(0.06, 0.14, pur));

	float fresnel = pow(1.0 - max(dot(n, VIEW), 0.0), mix(2.6, 3.8, pur));
	col += vec3(0.12, 0.13, 0.16) * fresnel * mix(0.35, 0.85, pur);

	ALBEDO = col;
	ROUGHNESS = mix(0.34, 0.06, pur) * (1.0 - fresnel * 0.35);
	METALLIC = 0.0;
	SPECULAR = mix(0.42, 0.78, pur);

	float alpha_tex = tex.a;
	float alpha = mix(1.0, alpha_tex * mix(0.32, 0.72, fresnel) + grain3d * 0.05, pur);
	ALPHA = clamp(alpha, 0.0, 1.0);
}
