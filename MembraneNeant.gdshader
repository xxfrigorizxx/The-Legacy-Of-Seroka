shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, unshaded;

uniform vec4 albedo_color : source_color = vec4(0.02, 0.04, 0.12, 0.92);
uniform vec4 emission_color : source_color = vec4(0.04, 0.08, 0.22, 1.0);
uniform float emission_energy : hint_range(0.0, 4.0) = 0.35;
uniform float noise_scale = 2.4;
uniform float noise_speed = 0.55;
uniform float radial_darken : hint_range(0.0, 3.0) = 1.35;
uniform float alpha_edge : hint_range(0.0, 1.0) = 0.35;
uniform float clip_bas_uv : hint_range(0.0, 0.49) = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

void fragment() {
	if (UV.y < clip_bas_uv) discard;
	vec2 uv = UV * noise_scale;
	float t = TIME * noise_speed;
	float n = noise2(uv + vec2(t * 0.31, -t * 0.27));
	n += 0.5 * noise2(uv * 2.7 + vec2(-t * 0.2, t * 0.18));
	vec2 c = UV - vec2(0.5);
	float r = length(c) * 2.0;
	float core = pow(clamp(r, 0.0, 1.0), radial_darken);
	vec3 col = albedo_color.rgb * (0.35 + n * 0.65) * (0.25 + core);
	float a = mix(albedo_color.a, alpha_edge, core);
	ALBEDO = col;
	ALPHA = clamp(a, 0.0, 1.0);
	EMISSION = emission_color.rgb * emission_energy * (0.4 + n * 0.6) * (1.0 - core * 0.5);
}
