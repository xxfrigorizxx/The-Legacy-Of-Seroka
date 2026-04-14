shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 couleur_base : source_color = vec4(0.24, 0.17, 0.12, 1.0);
uniform vec4 couleur_taches : source_color = vec4(0.78, 0.73, 0.66, 1.0);
uniform float echelle_taches = 0.85;
uniform float rugosite = 0.9;
uniform float speculaire = 0.12;
uniform float lumiere_ambiante = 0.28;
uniform float douceur_ombre = 0.45;
uniform float force_speculaire = 0.18;
uniform float flash_rouge_degats : hint_range(0.0, 1.0) = 0.0;

float bruit_rapide(vec3 p) {
	p = floor(p);
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void fragment() {
	vec3 p = WORLD_POSITION * echelle_taches;
	float n0 = bruit_rapide(p * 2.0);
	float n1 = bruit_rapide(p * 3.3 + vec3(13.1, 2.7, 9.4));
	float masque = smoothstep(0.43, 0.63, mix(n0, n1, 0.45));
	vec3 couleur = mix(couleur_base.rgb, couleur_taches.rgb, masque);

	// Variation subtile pour casser l'uniformite visuelle.
	float salissure = bruit_rapide(p * 7.0 + vec3(4.2, 1.0, 3.3));
	couleur *= 0.88 + 0.18 * salissure;
	couleur = mix(couleur, vec3(1.0, 0.12, 0.10), clamp(flash_rouge_degats, 0.0, 1.0));

	ALBEDO = couleur;
	ROUGHNESS = rugosite;
	SPECULAR = speculaire;
}

void light() {
	float ndotl = max(dot(NORMAL, LIGHT), 0.0);
	float half_lambert = ndotl * 0.5 + 0.5;
	float diffus_lisse = smoothstep(0.25 - douceur_ombre * 0.2, 0.85, half_lambert);
	float diffus = max(lumiere_ambiante, diffus_lisse);

	// Réduit les dominantes trop violentes des lumières colorées.
	vec3 lumiere_rgb = mix(vec3(dot(LIGHT_COLOR, vec3(0.3333))), LIGHT_COLOR, 0.7);
	DIFFUSE_LIGHT += ALBEDO * lumiere_rgb * diffus * ATTENUATION;

	vec3 h = normalize(VIEW + LIGHT);
	float ndoth = max(dot(NORMAL, h), 0.0);
	float spec = pow(ndoth, 20.0) * force_speculaire;
	SPECULAR_LIGHT += lumiere_rgb * spec * ATTENUATION;
}
