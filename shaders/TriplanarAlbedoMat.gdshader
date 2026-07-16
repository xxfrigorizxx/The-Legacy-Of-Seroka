shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, blend_mix;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform float echelle_triplanar = 6.0;
uniform vec4 albedo_tinte = vec4(1.0);
uniform float roughness = 0.55;
uniform float metallic = 0.0;

varying vec3 pos_local;
varying vec3 norm_local;

vec4 echantillon_triplanar(sampler2D tex, vec3 p, vec3 n) {
	vec3 w = abs(n);
	w = max(w, vec3(0.0001));
	w /= (w.x + w.y + w.z);
	vec4 tx = texture(tex, p.yz * echelle_triplanar);
	vec4 ty = texture(tex, p.xz * echelle_triplanar);
	vec4 tz = texture(tex, p.xy * echelle_triplanar);
	return tx * w.x + ty * w.y + tz * w.z;
}

void vertex() {
	pos_local = VERTEX;
	norm_local = NORMAL;
}

void fragment() {
	vec3 n = normalize(norm_local);
	vec4 tex = echantillon_triplanar(albedo_tex, pos_local, n);
	ALBEDO = tex.rgb * albedo_tinte.rgb;
	ROUGHNESS = roughness;
	METALLIC = metallic;
}
