using Godot;
using System;
using System.Collections.Generic;

public partial class BoeufSauvage : CharacterBody3D
{
	private void MettreAJourFlashDegatsVisuel(float dt)
	{
		if (_flashRougeDegatsRestant <= 0f) return;
		_flashRougeDegatsRestant = Mathf.Max(0f, _flashRougeDegatsRestant - dt);
		float ratio = Mathf.Clamp(_flashRougeDegatsRestant / Mathf.Max(0.05f, DureeFlashRougeDegats), 0f, 1f);
		AppliquerFlashRougeSurMateriaux(ratio);
	}

	private void AppliquerFlashRougeSurMateriaux(float intensite)
	{
		for (int i = _materiauxPelageInstances.Count - 1; i >= 0; i--)
		{
			ShaderMaterial mat = _materiauxPelageInstances[i];
			if (mat == null || !GodotObject.IsInstanceValid(mat))
			{
				_materiauxPelageInstances.RemoveAt(i);
				continue;
			}
			mat.SetShaderParameter("flash_rouge_degats", Mathf.Clamp(intensite, 0f, 1f));
		}
	}

	private void AppliquerBouclesSurClipsLocomotion()
	{
		if (_animationPlayer == null) return;
		var vus = new HashSet<string>();
		foreach (string chemin in new[] { _clipIdle, _clipMarche, _clipCourse, _clipManger })
		{
			if (string.IsNullOrEmpty(chemin) || !vus.Add(chemin)) continue;
			Animation anim = _animationPlayer.GetAnimation(chemin);
			if (anim == null) continue;
			anim.LoopMode = Animation.LoopModeEnum.Linear;
		}
	}

	private static List<string> CollecterCheminsAnimation(AnimationPlayer ap)
	{
		var liste = new List<string>();
		var vus = new HashSet<string>(StringComparer.Ordinal);
		if (ap == null) return liste;

		foreach (StringName nom in ap.GetAnimationList())
		{
			string s = nom.ToString();
			if (vus.Add(s))
				liste.Add(s);
		}

		foreach (StringName lib in ap.GetAnimationLibraryList())
		{
			AnimationLibrary libObj = ap.GetAnimationLibrary(lib);
			if (libObj == null) continue;
			foreach (StringName anim in libObj.GetAnimationList())
			{
				string s = $"{lib}/{anim}";
				if (vus.Add(s))
					liste.Add(s);
			}
		}

		return liste;
	}

	private static int CompterClipsAnimation(AnimationPlayer ap) => CollecterCheminsAnimation(ap).Count;

	private static AnimationPlayer ChoisirMeilleurAnimationPlayer(Node racine)
	{
		if (racine == null) return null;
		AnimationPlayer meilleur = null;
		int maxScore = -1;

		void Parcourir(Node n)
		{
			if (n is AnimationPlayer ap)
			{
				int score = CompterClipsAnimation(ap);
				// Ignorer les lecteurs vides (ex. nœud ajouté a la main dans l'éditeur sans bibliothèque).
				if (score > 0 && score > maxScore)
				{
					maxScore = score;
					meilleur = ap;
				}
			}

			foreach (Node enfant in n.GetChildren())
				Parcourir(enfant);
		}

		Parcourir(racine);
		return meilleur;
	}

	private void StabiliserMateriauxBoeuf()
	{
		_shaderPelageBoeuf ??= GD.Load<Shader>("res://shaders/BoeufSauvage.gdshader");
		_materiauxPelageInstances.Clear();
		if (_textureDiffuseModele == null && !string.IsNullOrWhiteSpace(CheminTextureDiffuseModele))
		{
			if (_cacheTextureDiffuseBoeuf.TryGetValue(CheminTextureDiffuseModele, out Texture2D texCache))
			{
				_textureDiffuseModele = texCache;
			}
			else if (ResourceLoader.Exists(CheminTextureDiffuseModele))
			{
				_textureDiffuseModele = GD.Load<Texture2D>(CheminTextureDiffuseModele);
				_cacheTextureDiffuseBoeuf[CheminTextureDiffuseModele] = _textureDiffuseModele;
			}
			else
			{
				_cacheTextureDiffuseBoeuf[CheminTextureDiffuseModele] = null;
				if (_cheminsTextureIntrouvablesLoggues.Add(CheminTextureDiffuseModele))
					GD.Print($"ZERO-K Faune : texture diffuse absente ({CheminTextureDiffuseModele}), utilisation des matériaux GLTF natifs.");
			}
		}
		StabiliserMateriauxRecursif(this, _shaderPelageBoeuf, _textureDiffuseModele);
	}

	private void StabiliserMateriauxRecursif(Node node, Shader shaderPelage, Texture2D textureDiffuse)
	{
		if (node is MeshInstance3D mesh)
		{
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
			if (UtiliserShaderPelageProcedural && shaderPelage != null)
			{
				var mat = new ShaderMaterial
				{
					Shader = shaderPelage
				};
				mat.SetShaderParameter("flash_rouge_degats", Mathf.Clamp(_flashRougeDegatsRestant / Mathf.Max(0.05f, DureeFlashRougeDegats), 0f, 1f));
				mesh.MaterialOverride = mat;
				_materiauxPelageInstances.Add(mat);
			}
			else
			{
				mesh.MaterialOverride = null;
				int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
				bool auMoinsUneSurface = false;
				for (int i = 0; i < surfaces; i++)
				{
					Material source = mesh.GetActiveMaterial(i);
					if (source == null) continue;

					Material dup = (Material)source.Duplicate(true);
					mesh.SetSurfaceOverrideMaterial(i, dup);
					auMoinsUneSurface = true;

					switch (dup)
					{
						case StandardMaterial3D sm:
							RenforcerStandardMateriauBoeuf(sm, textureDiffuse);
							break;
						case BaseMaterial3D bm:
							RenforcerBaseMateriau3DBoeuf(bm, textureDiffuse);
							break;
					}
				}

				if (!auMoinsUneSurface && surfaces > 0)
				{
					mesh.MaterialOverride = new StandardMaterial3D
					{
						AlbedoColor = textureDiffuse != null ? Colors.White : new Color(0.40f, 0.30f, 0.19f, 1f),
						AlbedoTexture = textureDiffuse,
						Roughness = 0.9f,
						Metallic = 0f
					};
				}
			}
		}

		foreach (Node enfant in node.GetChildren())
			StabiliserMateriauxRecursif(enfant, shaderPelage, textureDiffuse);
	}

	private static void RenforcerStandardMateriauBoeuf(StandardMaterial3D sm, Texture2D textureDiffuse)
	{
		sm.Metallic = Mathf.Min(sm.Metallic, 0.35f);
		sm.Roughness = Mathf.Clamp(sm.Roughness, 0.35f, 1f);
		float lum = sm.AlbedoColor.R + sm.AlbedoColor.G + sm.AlbedoColor.B;
		if (lum < 0.06f)
			sm.AlbedoColor = Colors.White;
		if (sm.AlbedoTexture == null && textureDiffuse != null)
			sm.AlbedoTexture = textureDiffuse;
		sm.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
	}

	private static void RenforcerBaseMateriau3DBoeuf(BaseMaterial3D bm, Texture2D textureDiffuse)
	{
		bm.Metallic = Mathf.Min(bm.Metallic, 0.35f);
		bm.Roughness = Mathf.Clamp(bm.Roughness, 0.35f, 1f);
		float lum = bm.AlbedoColor.R + bm.AlbedoColor.G + bm.AlbedoColor.B;
		if (lum < 0.06f)
			bm.AlbedoColor = Colors.White;
		if (bm.AlbedoTexture == null && textureDiffuse != null)
			bm.AlbedoTexture = textureDiffuse;
		bm.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
	}

	private static bool PisteInfluenceHauteurCorps(string path)
	{
		if (string.IsNullOrEmpty(path))
			return false;
		string p = path.ToLowerInvariant();
		if (p.Contains("foot") || p.Contains("toe") || p.Contains("hoof") || p.Contains("ankle")
			|| p.Contains("knee") || p.Contains("genou") || p.Contains("pied") || p.Contains("sabot")
			|| p.Contains("calf") || p.Contains("shin"))
			return false;
		if (p.Contains("modele"))
			return true;
		return p.Contains("root") || p.Contains("hips") || p.Contains("pelvis")
			|| p.Contains("spine") || p.Contains("armature") || p.Contains("skeleton");
	}

	private static bool NomClipLocomotionRapide(string nomClip)
	{
		if (string.IsNullOrEmpty(nomClip))
			return false;
		string n = nomClip.ToLowerInvariant();
		return n.Contains("run") || n.Contains("gallop") || n.Contains("course")
			|| n.Contains("trot") || n.Contains("lope") || n.Contains("charge");
	}

	private float EstimerEnfoncementDepuisPistesPosition(Animation anim)
	{
		if (anim == null)
			return 0f;
		float minDelta = 0f;
		bool refPose = false;
		float refY = 0f;
		for (int t = 0; t < anim.GetTrackCount(); t++)
		{
			if (anim.TrackGetType(t) != Animation.TrackType.Position3D)
				continue;
			if (!PisteInfluenceHauteurCorps(anim.TrackGetPath(t).ToString()))
				continue;
			int keyCount = anim.TrackGetKeyCount(t);
			if (keyCount <= 0)
				continue;
			float y0 = ((Vector3)anim.TrackGetKeyValue(t, 0)).Y;
			if (!refPose)
			{
				refY = y0;
				refPose = true;
			}
			for (int k = 0; k < keyCount; k++)
			{
				float y = ((Vector3)anim.TrackGetKeyValue(t, k)).Y;
				minDelta = Mathf.Min(minDelta, y - refY);
			}
		}
		return refPose ? Mathf.Max(0f, -minDelta) : 0f;
	}

	private float EstimerEnfoncementClip(string nomClip)
	{
		if (_animationPlayer == null || string.IsNullOrEmpty(nomClip) || !_animationPlayer.HasAnimation(nomClip))
			return 0f;
		Animation anim = _animationPlayer.GetAnimation(nomClip);
		return EstimerEnfoncementDepuisPistesPosition(anim);
	}

	private static void ReequilibrerEnfoncementVerticalClip(Animation anim, string nomClip)
	{
		if (anim == null)
			return;
		float enfoncement = 0f;
		for (int t = 0; t < anim.GetTrackCount(); t++)
		{
			if (anim.TrackGetType(t) != Animation.TrackType.Position3D)
				continue;
			if (!PisteInfluenceHauteurCorps(anim.TrackGetPath(t).ToString()))
				continue;
			int keyCount = anim.TrackGetKeyCount(t);
			if (keyCount <= 0)
				continue;
			float y0 = ((Vector3)anim.TrackGetKeyValue(t, 0)).Y;
			for (int k = 0; k < keyCount; k++)
			{
				float y = ((Vector3)anim.TrackGetKeyValue(t, k)).Y;
				enfoncement = Mathf.Max(enfoncement, y0 - y);
			}
		}
		if (enfoncement < 0.012f)
			return;
		float correction = enfoncement * 0.75f;
		for (int t = 0; t < anim.GetTrackCount(); t++)
		{
			if (anim.TrackGetType(t) != Animation.TrackType.Position3D)
				continue;
			if (!PisteInfluenceHauteurCorps(anim.TrackGetPath(t).ToString()))
				continue;
			int keyCount = anim.TrackGetKeyCount(t);
			for (int k = 0; k < keyCount; k++)
			{
				Vector3 v = (Vector3)anim.TrackGetKeyValue(t, k);
				v.Y += correction;
				anim.TrackSetKeyValue(t, k, v);
			}
		}
	}

	private void AnalyserCompensationsEnfoncementClipsLocomotion()
	{
		_compensationYIdle = 0f;
		_compensationYMarche = CompensationSolMarcheManuelle;
		_compensationYCourse = 0f;
		_compensationYTrot = 0f;
		_compensationYBroutage = 0f;
		if (_animationPlayer == null)
			return;

		float MesurerEnfoncementClip(string clip, float manuel)
		{
			if (!AutoCompenserEnfoncementClipsLocomotion)
				return manuel;
			float est = EstimerEnfoncementClip(clip);
			if (est < 0.01f)
				return manuel;
			return manuel > 0f ? Mathf.Max(manuel, est * 0.55f) : est * 0.55f;
		}

		if (!string.IsNullOrEmpty(_clipIdle))
			_compensationYIdle = MesurerEnfoncementClip(_clipIdle, 0f);
		if (!string.IsNullOrEmpty(_clipMarche))
			_compensationYMarche = MesurerEnfoncementClip(_clipMarche, CompensationSolMarcheManuelle);
		if (!string.IsNullOrEmpty(_clipCourse))
			_compensationYCourse = MesurerEnfoncementClip(_clipCourse, 0f);
		if (!string.IsNullOrEmpty(_clipTrot))
			_compensationYTrot = MesurerEnfoncementClip(_clipTrot, 0f);
		if (!string.IsNullOrEmpty(_clipManger))
			_compensationYBroutage = MesurerEnfoncementClip(_clipManger, 0f);
	}

	private float CalculerCompensationSolCible()
	{
		if (!AutoCompenserEnfoncementClipsLocomotion || _modeleVisuel == null || _etat == EtatBoeuf.Mort)
			return 0f;
		if (_dansEau || _tempsVerrouAnimationCombat > 0.01f || _impactChargeJoueurPlanifie)
			return 0f;
		if (_etat == EtatBoeuf.Broutage)
			return Mathf.Min(_compensationYBroutage, CompensationSolMaxCourse * 0.35f);

		float blend = float.IsNaN(_dernierBlendAnimation) ? 0f : Mathf.Clamp(_dernierBlendAnimation, 0f, 1f);
		if (_etat == EtatBoeuf.Fuite || _etat == EtatBoeuf.Charge)
			blend = Mathf.Max(blend, 0.65f);

		// Marche / idle = référence au sol ; on ne relève qu'en course (delta).
		if (blend < 0.42f)
			return 0f;

		float refSol = Mathf.Min(_compensationYMarche, _compensationYIdle);
		float extraCourse = Mathf.Max(0f, _compensationYCourse - refSol);
		float extraTrot = Mathf.Max(0f, _compensationYTrot - refSol);
		float extra = extraCourse;
		if (blend > 0.55f && extraTrot > 0f)
			extra = Mathf.Lerp(extraCourse, extraTrot, Mathf.Clamp((blend - 0.55f) / 0.45f, 0f, 1f));

		float t = Mathf.Clamp((blend - 0.42f) / 0.58f, 0f, 1f);
		float manuel = CompensationSolCourseManuelle * t;
		return Mathf.Min(extra * t + manuel, CompensationSolMaxCourse);
	}

	private void MettreAJourCompensationEnfoncementSol(float dt)
	{
		if (_modeleVisuel == null || !_geneTailleInitialise)
			return;
		float cible = CalculerCompensationSolCible();
		float lissage = Mathf.Clamp(VitesseLissageCompensationSol * dt, 0f, 1f);
		_offsetVisuelSolActuel = Mathf.Lerp(_offsetVisuelSolActuel, cible, lissage);
		Transform3D baseT = _transformModeleBase;
		baseT.Basis = baseT.Basis.Scaled(Vector3.One * TailleEffective);
		baseT.Origin += Vector3.Up * (_offsetVisuelSolActuel * TailleEffective);
		_modeleVisuel.Transform = baseT;
	}

	private void SecuriserPositionSol()
	{
		if (EssayerTrouverSolParRaycast(GlobalPosition + Vector3.Up * 3f, out Vector3 sol))
		{
			if (sol.DistanceTo(GlobalPosition) > 7f)
				_cibleCourante = _ancreTroupeau;
			return;
		}

		float seuilVide = _joueur != null ? _joueur.GlobalPosition.Y - 80f : 90f;
		if (GlobalPosition.Y < seuilVide)
		{
			int h = Generateur_Voxel.ObtenirHauteurTerrainMonde(
				Mathf.FloorToInt(_ancreTroupeau.X),
				Mathf.FloorToInt(_ancreTroupeau.Z),
				_seedTerrain);
			GlobalPosition = new Vector3(_ancreTroupeau.X, h + 1.2f, _ancreTroupeau.Z);
			Velocity = Vector3.Zero;
			_cibleCourante = _ancreTroupeau;
		}
	}

	private bool EssayerTrouverSolParRaycast(Vector3 debut, out Vector3 pointSol)
	{
		pointSol = Vector3.Zero;
		World3D world = GetWorld3D();
		if (world == null || world.DirectSpaceState == null) return false;

		Vector3 fin = debut + Vector3.Down * 40f;
		var query = PhysicsRayQueryParameters3D.Create(debut, fin);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.HitFromInside = false;
		var hit = world.DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 || !hit.ContainsKey("position")) return false;
		pointSol = (Vector3)hit["position"];
		return true;
	}
}
