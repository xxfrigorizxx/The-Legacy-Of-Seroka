shader_type spatial;
render_mode cull_back;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform float echelle_triplanar = 7.0;
uniform vec3 couleur_paille = vec3(0.78, 0.68, 0.38);
uniform vec3 couleur_herbe_fondu = vec3(0.55, 0.45, 0.30);

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

	// Brins / pointes : normales raides (épine de paille sortant de la motte)
	float pente = 1.0 - abs(n.y);
	float brin = smoothstep(0.35, 0.82, pente);

	vec3 col = tex.rgb;
	col = mix(col, couleur_herbe_fondu, brin * 0.55);
	col = mix(col, couleur_paille, brin * brin * 0.65);

	// Micro-variation 3D pour casser l'uniformité restante
	float bruit = sin(pos_local.x * 19.0 + pos_local.y * 13.0) * cos(pos_local.z * 17.0);
	col += vec3(bruit * 0.035);

	ALBEDO = col;
	ROUGHNESS = 0.92;
	METALLIC = 0.0;
}
