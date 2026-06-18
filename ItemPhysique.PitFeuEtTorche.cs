using Godot;
using System;
using System.Collections.Generic;

public partial class ItemPhysique : RigidBody3D
{
	// Lumière torche / feu : portée étendue + atténuation douce (évite coupure radicale).
	private const float LumiereTorcheEnergy = 2.15f;
	private const float LumiereTorchePortee = 8.2f;
	private const float LumiereTorcheAttenuation = 0.26f;
	private const float LumiereFeuEnergy = 2.45f;
	private const float LumiereFeuPortee = 9.5f;
	private const float LumiereFeuAttenuation = 0.26f;

	private static void ConfigurerLumiereTorche(OmniLight3D light)
	{
		if (light == null || !GodotObject.IsInstanceValid(light))
			return;
		light.LightColor = new Color(1.0f, 0.62f, 0.30f);
		light.LightEnergy = LumiereTorcheEnergy;
		light.OmniRange = LumiereTorchePortee;
		light.OmniAttenuation = LumiereTorcheAttenuation;
		light.LightSpecular = 0.12f;
		light.ShadowEnabled = false;
	}

	private static void ConfigurerLumiereFeuCamp(OmniLight3D light)
	{
		if (light == null || !GodotObject.IsInstanceValid(light))
			return;
		light.LightColor = new Color(1.0f, 0.58f, 0.26f);
		light.LightEnergy = LumiereFeuEnergy;
		light.OmniRange = LumiereFeuPortee;
		light.OmniAttenuation = LumiereFeuAttenuation;
		light.LightSpecular = 0.12f;
		light.ShadowEnabled = false;
	}

	private void ActiverVisuelPitFeu(bool actif)
	{
		if (ID_Objet != Joueur.IdObjetPitFeu && ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		if (_pitFlammeCroix == null || !GodotObject.IsInstanceValid(_pitFlammeCroix))
		{
			_pitFlammeCroix = new Node3D
			{
				Name = "PitFeuFlammesCroix",
				Position = PitFlammeCroixBasePosition,
				Visible = false
			};
			StandardMaterial3D matFlamme = CreerMateriauFlammePitTexture();
			for (int i = 0; i < 4; i++)
			{
				var mi = new MeshInstance3D
				{
					Name = $"FlammePlan{i}",
					Mesh = new QuadMesh { Size = new Vector2(0.94f, 0.285f) },
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
				};
				mi.MaterialOverride = matFlamme;
				mi.RotationDegrees = new Vector3(0f, i * 45f, 0f);
				mi.Position = new Vector3(0f, 0.04f + i * 0.010f, 0f);
				_pitFlammeCroix.AddChild(mi);
			}
			AddChild(_pitFlammeCroix);
		}
		if (_pitFlammeParticles == null || !GodotObject.IsInstanceValid(_pitFlammeParticles))
		{
			_pitFlammeParticles = new GpuParticles3D
			{
				Name = "PitFeuFlammes",
				Amount = 84,
				Explosiveness = 0f,
				Lifetime = 0.74,
				OneShot = false,
				Emitting = false,
				Position = PitFlammeParticlesBasePosition
			};
			var meshFlamme = new QuadMesh { Size = new Vector2(0.336f, 0.135f) };
			meshFlamme.Material = CreerMateriauFlammePitTexture();
			_pitFlammeParticles.DrawPass1 = meshFlamme;
			var mat = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 1.35f, 0f),
				InitialVelocityMin = 0.055f,
				InitialVelocityMax = 0.175f,
				ScaleMin = 0.48f,
				ScaleMax = 1.16f,
				ScaleCurve = null
			};
			_pitFlammeParticles.ProcessMaterial = mat;
			AddChild(_pitFlammeParticles);
		}
		if (_pitFumeeParticles == null || !GodotObject.IsInstanceValid(_pitFumeeParticles))
		{
			_pitFumeeParticles = new GpuParticles3D
			{
				Name = "PitFeuFumee",
				Amount = 30,
				Explosiveness = 0f,
				Lifetime = 3.2,
				OneShot = false,
				Emitting = false,
				Position = PitFumeeParticlesBasePosition,
				VisibilityAabb = new Aabb(new Vector3(-1.2f, -0.6f, -1.2f), new Vector3(2.4f, 3.8f, 2.4f))
			};
			var meshFumee = new SphereMesh { Radius = 0.05f, Height = 0.10f, RadialSegments = 8, Rings = 6 };
			meshFumee.Material = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.72f, 0.72f, 0.72f, 0.62f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			_pitFumeeParticles.DrawPass1 = meshFumee;
			var matFumee = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 0.35f, 0f),
				InitialVelocityMin = 0.018f,
				InitialVelocityMax = 0.082f,
				ScaleMin = 0.26f,
				ScaleMax = 0.9f
			};
			_pitFumeeParticles.ProcessMaterial = matFumee;
			AddChild(_pitFumeeParticles);
		}
		if (_pitFlammeLight == null || !GodotObject.IsInstanceValid(_pitFlammeLight))
		{
			_pitFlammeLight = new OmniLight3D
			{
				Name = "PitFeuLumiere",
				Position = new Vector3(0f, 0.28f, 0f),
				Visible = false
			};
			ConfigurerLumiereFeuCamp(_pitFlammeLight);
			AddChild(_pitFlammeLight);
		}
		_pitFlammeCroix.Visible = actif;
		_pitFlammeParticles.Emitting = actif;
		_pitFlammeParticles.Visible = actif;
		_pitFumeeParticles.Emitting = actif;
		_pitFumeeParticles.Visible = actif;
		_pitFlammeLight.Visible = actif;
	}

	private static ImageTexture ObtenirTextureFlammePit()
	{
		if (_textureFlammePitCache != null && GodotObject.IsInstanceValid(_textureFlammePitCache))
			return _textureFlammePitCache;
		const int taille = 256;
		Image img = Image.CreateEmpty(taille, taille, false, Image.Format.Rgba8);
		for (int y = 0; y < taille; y++)
		{
			float v = (float)y / (taille - 1);
			for (int x = 0; x < taille; x++)
			{
				float u = (float)x / (taille - 1);
				float wobble = 0.045f * Mathf.Sin(v * 18.0f + u * 33.0f) + 0.03f * Mathf.Sin(v * 47.0f);
				float langues = 0.018f * Mathf.Sin((u * 9.0f + v * 4.0f) * Mathf.Pi * 2.0f) + 0.012f * Mathf.Sin((u * 17.0f - v * 6.0f) * Mathf.Pi);
				float centre = 1.0f - Mathf.Abs(((u + wobble + langues) - 0.5f) * 2.0f);
				float profil = Mathf.Pow(Mathf.Clamp(centre, 0f, 1f), 1.55f);
				float hauteur = Mathf.Clamp(1.0f - v, 0f, 1f);
				float turbulence = 0.82f + 0.18f * Mathf.Sin(u * 52.0f + v * 29.0f) * (0.5f + 0.5f * hauteur);
				float alpha = Mathf.Clamp(profil * Mathf.Pow(hauteur, 0.46f) * turbulence, 0f, 1f);
				alpha *= 1.0f - Mathf.Clamp(v * v * 0.9f, 0f, 0.9f);
				alpha = Mathf.Clamp(alpha * 1.46f, 0f, 1f);
				float coeur = Mathf.Clamp(1.0f - Mathf.Abs((u - 0.5f) * 5.4f), 0f, 1f) * Mathf.Clamp(1.0f - v * 1.7f, 0f, 1f);
				Color baseC = new Color(1.0f, 0.36f, 0.06f, 1f);
				Color hotC = new Color(1.0f, 0.74f, 0.18f, 1f);
				Color tipC = new Color(1.0f, 0.93f, 0.62f, 1f);
				Color c = baseC.Lerp(hotC, Mathf.Clamp(v * 1.1f, 0f, 1f)).Lerp(tipC, Mathf.Clamp(v * 1.9f - 0.30f, 0f, 1f));
				c = c.Lerp(new Color(1.0f, 0.98f, 0.86f, 1f), coeur * 0.55f);
				c = c.Lerp(new Color(1.0f, 0.9f, 0.42f, 1f), Mathf.Clamp((1.0f - v) * 0.22f, 0f, 0.22f));
				c.A = alpha;
				img.SetPixel(x, y, c);
			}
		}
		_textureFlammePitCache = ImageTexture.CreateFromImage(img);
		return _textureFlammePitCache;
	}

	private static StandardMaterial3D CreerMateriauFlammePitTexture()
	{
		return new StandardMaterial3D
		{
			AlbedoTexture = ObtenirTextureFlammePit(),
			AlbedoColor = new Color(1f, 1f, 1f, 1f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			EmissionEnabled = true,
			Emission = new Color(1f, 0.6f, 0.22f),
			EmissionEnergyMultiplier = 1.9f
		};
	}

	public static void AttacherVisuelFlammeTorche(Node3D parent)
	{
		if (parent == null || !GodotObject.IsInstanceValid(parent))
			return;
		Node3D racine = parent.GetNodeOrNull<Node3D>("TorcheFlamme");
		if (racine == null)
		{
			racine = new Node3D
			{
				Name = "TorcheFlamme",
				Position = new Vector3(0f, 0.86f, 0f)
			};
			StandardMaterial3D mat = CreerMateriauFlammePitTexture();
			for (int i = 0; i < 3; i++)
			{
				var plan = new MeshInstance3D
				{
					Name = $"FlammeTorchePlan{i}",
					Mesh = new QuadMesh { Size = new Vector2(0.20f, 0.32f) },
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
					Position = new Vector3(0f, i * 0.02f, 0f),
					RotationDegrees = new Vector3(0f, i * 60f, 0f),
					MaterialOverride = mat
				};
				racine.AddChild(plan);
			}

			var flammes = new GpuParticles3D
			{
				Name = "TorcheFlammesParticles",
				Amount = 38,
				Explosiveness = 0f,
				Lifetime = 0.68,
				OneShot = false,
				Emitting = true,
				Position = new Vector3(0f, 0.03f, 0f)
			};
			var meshFlamme = new QuadMesh { Size = new Vector2(0.18f, 0.24f) };
			meshFlamme.Material = CreerMateriauFlammePitTexture();
			flammes.DrawPass1 = meshFlamme;
			flammes.ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 1.2f, 0f),
				InitialVelocityMin = 0.045f,
				InitialVelocityMax = 0.15f,
				ScaleMin = 0.42f,
				ScaleMax = 0.98f
			};
			racine.AddChild(flammes);

			var fumee = new GpuParticles3D
			{
				Name = "TorcheFumeeParticles",
				Amount = 10,
				Explosiveness = 0f,
				Lifetime = 2.5,
				OneShot = false,
				Emitting = true,
				Position = new Vector3(0f, 0.12f, 0f),
				VisibilityAabb = new Aabb(new Vector3(-0.8f, -0.4f, -0.8f), new Vector3(1.6f, 2.2f, 1.6f))
			};
			var meshFumee = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 6 };
			meshFumee.Material = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.72f, 0.72f, 0.72f, 0.52f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			fumee.DrawPass1 = meshFumee;
			fumee.ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 0.28f, 0f),
				InitialVelocityMin = 0.012f,
				InitialVelocityMax = 0.06f,
				ScaleMin = 0.22f,
				ScaleMax = 0.66f
			};
			racine.AddChild(fumee);
			parent.AddChild(racine);
		}
		racine.Visible = true;

		// Lumière calée sur la flamme (pas sur la racine du corps) pour éviter le décalage en main / au sol.
		OmniLight3D light = racine.GetNodeOrNull<OmniLight3D>("TorcheLumiere");
		if (light == null)
			light = parent.GetNodeOrNull<OmniLight3D>("TorcheLumiere");
		if (light == null)
		{
			light = new OmniLight3D
			{
				Name = "TorcheLumiere",
				Position = new Vector3(0f, 0.06f, 0f)
			};
			ConfigurerLumiereTorche(light);
			racine.AddChild(light);
		}
		else
		{
			light.Position = new Vector3(0f, 0.06f, 0f);
			if (light.GetParent() != racine)
			{
				light.Reparent(racine);
				light.Position = new Vector3(0f, 0.06f, 0f);
			}
			ConfigurerLumiereTorche(light);
		}
		light.Visible = true;
	}

	private static void AppliquerEtatVisuelTorcheSurNoeud(Node noeud, bool actif, ref Node3D premiereFlamme, ref OmniLight3D premiereLumiere)
	{
		if (noeud == null || !GodotObject.IsInstanceValid(noeud))
			return;
		var pile = new List<Node> { noeud };
		for (int i = 0; i < pile.Count; i++)
		{
			Node courant = pile[i];
			if (courant.Name == "TorcheFlamme" && courant is Node3D flamme)
			{
				flamme.Visible = actif;
				foreach (Node enfant in flamme.GetChildren())
				{
					if (enfant is GpuParticles3D particles)
					{
						particles.Emitting = actif;
						particles.Visible = actif;
					}
				}
				premiereFlamme ??= flamme;
			}
			if (courant.Name == "TorcheLumiere" && courant is OmniLight3D lumiere)
			{
				lumiere.Visible = actif;
				premiereLumiere ??= lumiere;
			}
			foreach (Node enfant in courant.GetChildren())
				pile.Add(enfant);
		}
	}

	private void ActiverVisuelTorche(bool actif)
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		if (actif)
			AttacherVisuelFlammeTorche(this);
		Node3D premiereFlamme = null;
		OmniLight3D premiereLumiere = null;
		AppliquerEtatVisuelTorcheSurNoeud(this, actif, ref premiereFlamme, ref premiereLumiere);
		_torcheFlamme = actif ? premiereFlamme : null;
		_torcheLight = actif ? premiereLumiere : null;
	}

	private void SynchroniserGenomeTorche(bool allumee)
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		GenomeAssemblage = allumee ? "TORCHE:1" : "TORCHE:0";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaTorcheAllumee, allumee);
	}

	public bool EstTorcheAllumee()
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return false;
		if ((GenomeAssemblage ?? "").StartsWith("TORCHE:1", StringComparison.Ordinal))
			return true;
		return HasMeta(MetaTorcheAllumee) && GetMeta(MetaTorcheAllumee).AsBool();
	}

	/// <summary>
	/// Zone de contact brûlante (flamme visible) en coordonnées monde.
	/// Utilisée pour les brûlures "au contact direct" uniquement.
	/// </summary>
	public bool EssayerObtenirZoneContactFlammeMonde(out Vector3 centreMonde, out float rayonMetres)
	{
		centreMonde = Vector3.Zero;
		rayonMetres = 0f;

		if (ID_Objet == Joueur.IdObjetTorche)
		{
			if (!EstTorcheAllumee())
				return false;

			Node3D flamme = _torcheFlamme;
			if (flamme == null || !GodotObject.IsInstanceValid(flamme))
				flamme = GetNodeOrNull<Node3D>("TorcheFlamme");
			centreMonde = flamme != null
				? flamme.GlobalPosition + new Vector3(0f, 0.03f, 0f)
				: GlobalPosition + new Vector3(0f, 0.89f, 0f);
			rayonMetres = 0.15f;
			return true;
		}

		if (ID_Objet == Joueur.IdObjetPitFeu || ID_Objet == Joueur.IdObjetPitFeuRoche)
		{
			if (!EstPitFeuAllume())
				return false;

			Node3D flammePit = _pitFlammeCroix;
			if (flammePit == null || !GodotObject.IsInstanceValid(flammePit))
				flammePit = GetNodeOrNull<Node3D>("PitFeuFlammesCroix");
			centreMonde = flammePit != null
				? flammePit.GlobalPosition + new Vector3(0f, 0.05f, 0f)
				: GlobalPosition + PitFlammeCroixBasePosition + new Vector3(0f, 0.05f, 0f);
			rayonMetres = 0.36f;
			return true;
		}

		return false;
	}

	public bool ActiverTorcheAllumee()
	{
		if (ID_Objet != Joueur.IdObjetTorche || EstTorcheAllumee())
			return false;
		ActiverVisuelTorche(true);
		SynchroniserGenomeTorche(true);
		return true;
	}

	public bool EteindreTorche()
	{
		if (ID_Objet != Joueur.IdObjetTorche || !EstTorcheAllumee())
			return false;
		ActiverVisuelTorche(false);
		SynchroniserGenomeTorche(false);
		return true;
	}

	private void ChargerEtatTorcheDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		bool allumee = (GenomeAssemblage ?? "").StartsWith("TORCHE:1", StringComparison.Ordinal);
		if (!allumee && HasMeta(MetaTorcheAllumee))
			allumee = GetMeta(MetaTorcheAllumee).AsBool();
		ActiverVisuelTorche(allumee);
		SynchroniserGenomeTorche(allumee);
	}

	private void SynchroniserGenomePitFeuDepuisReste()
	{
		if (ID_Objet != Joueur.IdObjetPitFeu)
			return;
		long finMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)Mathf.Round((float)(_pitFeuResteSec * 1000.0));
		GenomeAssemblage = $"PITFEU:{finMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaPitFeuFinCombustionUnixMs, finMs);
	}

	private void SynchroniserGenomePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		AssurerGrillePitFeuRoche3Slots();
		SlotInventaire comb = GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		SlotInventaire cuis = GrillePlanTravailAtelier[PitFeuRocheSlotCuisson];
		SlotInventaire res = GrillePlanTravailAtelier[PitFeuRocheSlotResultat];
		bool combOk = EstSlotCombustiblePitFeuRoche(comb);
		int combQte = combOk ? Joueur.ObtenirQuantiteSlot(comb) : 0;
		_pitFeuRocheStockCombustible = combQte;
		int combId = combOk ? comb.ID : 32;
		byte combEssence = combOk ? comb.IndexBotanique : LSystem_Botanique.IndexChene;
		int crus = EstSlotCuissonPitFeuRoche(cuis) ? Joueur.ObtenirQuantiteSlot(cuis) : 0;
		int cuits = EstSlotResultatPitFeuRoche(res) ? Joueur.ObtenirQuantiteSlot(res) : 0;
		// Provenance du steak (index) : prise du cru en priorité, sinon du cuit (préserve la variante).
		SlotInventaire steakRef = crus > 0 ? cuis : res;
		byte sBot = steakRef.IndexBotanique;
		int sChi = steakRef.IndexChimique;
		int sMor = steakRef.IndexMorphologique;
		long t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long resteMs = (long)Mathf.Round((float)Math.Max(0d, _pitFeuRocheResteSec * 1000.0));
		long dureeMs = (long)Mathf.Round((float)Math.Max(1d, _pitFeuRocheDureeUniteCouranteSec * 1000.0));
		long progMs = (long)Mathf.Round((float)Math.Max(0d, _pitFeuRocheProgressCuissonSec * 1000.0));
		// Format v2 : état complet pour rattrapage hors-ligne (bois+essence, steaks crus/cuits, temps de référence).
		GenomeAssemblage = $"PITFEUROCHE2:{t0}:{combQte}:{combEssence}:{combId}:{resteMs}:{dureeMs}:{progMs}:{crus}:{cuits}:{sBot}:{sChi}:{sMor}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaPitFeuRocheStockCombustible, combQte);
	}

	private void ChargerEtatPitFeuDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetPitFeu)
			return;
		long maintenant = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long finMs = 0L;
		if (!string.IsNullOrEmpty(GenomeAssemblage) && GenomeAssemblage.StartsWith("PITFEU:", StringComparison.Ordinal))
		{
			string brut = GenomeAssemblage.Substring("PITFEU:".Length);
			long.TryParse(brut, out finMs);
		}
		else if (HasMeta(MetaPitFeuFinCombustionUnixMs))
		{
			finMs = GetMeta(MetaPitFeuFinCombustionUnixMs).AsInt64();
		}
		if (finMs > maintenant)
		{
			_pitFeuResteSec = (finMs - maintenant) / 1000.0;
			_pitFeuDernierSyncRestantSec = -1d;
			ActiverVisuelPitFeu(true);
		}
		else
		{
			_pitFeuResteSec = 0d;
			ActiverVisuelPitFeu(false);
		}
	}

	private void ChargerEtatPitFeuRocheDepuisGenome()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		long maintenant = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		string g = GenomeAssemblage ?? "";

		// Format v2 : état complet + rattrapage hors-ligne (combustion + cuisson pendant l'absence).
		if (g.StartsWith("PITFEUROCHE2:", StringComparison.Ordinal))
		{
			string[] m = g.Substring("PITFEUROCHE2:".Length).Split(':');
			if (m.Length >= 12)
			{
				long.TryParse(m[0], out long t0);
				int.TryParse(m[1], out int combQte);
				byte.TryParse(m[2], out byte essence);
				int.TryParse(m[3], out int combId);
				long.TryParse(m[4], out long resteMs);
				long.TryParse(m[5], out long dureeMs);
				long.TryParse(m[6], out long progMs);
				int.TryParse(m[7], out int crus);
				int.TryParse(m[8], out int cuits);
				byte.TryParse(m[9], out byte sBot);
				int.TryParse(m[10], out int sChi);
				int.TryParse(m[11], out int sMor);

				double resteUnite = Math.Max(0d, resteMs / 1000.0);
				double dureeUnite = Math.Max(1d, dureeMs / 1000.0);
				double progress = Math.Max(0d, progMs / 1000.0);
				combQte = Mathf.Max(0, combQte);
				crus = Mathf.Max(0, crus);
				cuits = Mathf.Max(0, cuits);

				int plafondCuits = Mathf.Max(1, Joueur.ObtenirPileMax(
					new SlotInventaire { ID = Joueur.IdObjetSteakCuit, Quantite = 1 }));
				double tempsEcoule = Math.Max(0d, (maintenant - t0) / 1000.0);
				SimulerRattrapagePitFeuRoche(ref combQte, essence, ref resteUnite, dureeUnite,
					ref progress, ref crus, ref cuits, tempsEcoule, plafondCuits);

				RestaurerGrillePitFeuRoche(combQte, combId, essence, crus, cuits, sBot, sChi, sMor);
				_pitFeuRocheStockCombustible = combQte;
				_pitFeuRocheResteSec = resteUnite;
				_pitFeuRocheDureeUniteCouranteSec = dureeUnite;
				_pitFeuRocheProgressCuissonSec = progress;
				_pitFeuRocheDernierSyncRestantSec = -1d;
				ActiverVisuelPitFeu(resteUnite > 0.001d);
				return;
			}
		}

		// Compat ancien format PITFEUROCHE: (stock:finMs:progress) — restauration simple sans steaks.
		long finMs = 0L;
		long progressCuissonMs = 0L;
		int stock = 0;
		if (g.StartsWith("PITFEUROCHE:", StringComparison.Ordinal))
		{
			string[] morceaux = g.Substring("PITFEUROCHE:".Length).Split(':');
			if (morceaux.Length >= 2)
			{
				int.TryParse(morceaux[0], out stock);
				long.TryParse(morceaux[1], out finMs);
				if (morceaux.Length >= 3)
					long.TryParse(morceaux[2], out progressCuissonMs);
			}
		}
		else
		{
			if (HasMeta(MetaPitFeuRocheStockCombustible))
				stock = Mathf.Max(0, GetMeta(MetaPitFeuRocheStockCombustible).AsInt32());
			if (HasMeta(MetaPitFeuRocheFinCombustionUnixMs))
				finMs = GetMeta(MetaPitFeuRocheFinCombustionUnixMs).AsInt64();
			if (HasMeta(MetaPitFeuRocheProgressCuissonMs))
				progressCuissonMs = Math.Max(0L, GetMeta(MetaPitFeuRocheProgressCuissonMs).AsInt64());
		}
		_pitFeuRocheStockCombustible = Mathf.Max(0, stock);
		AssurerGrillePitFeuRoche3Slots();
		if (_pitFeuRocheStockCombustible > 0 && CompterCombustiblePitFeuRocheDepuisGrille() <= 0)
			AjouterCombustiblePitFeuRocheDansGrille(_pitFeuRocheStockCombustible, 32, LSystem_Botanique.IndexChene);
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		_pitFeuRocheProgressCuissonSec = Math.Max(0d, progressCuissonMs / 1000.0);
		if (finMs > maintenant)
		{
			_pitFeuRocheResteSec = (finMs - maintenant) / 1000.0;
			_pitFeuRocheDernierSyncRestantSec = -1d;
			ActiverVisuelPitFeu(true);
		}
		else
		{
			_pitFeuRocheResteSec = 0d;
			ActiverVisuelPitFeu(false);
		}
	}

	/// <summary>
	/// Rattrapage hors-ligne : simule la combustion (bois brûlé unité par unité) ET la cuisson des steaks
	/// pendant <paramref name="tempsEcoule"/> secondes. Le feu brûle son bois en continu (comme en ligne) ;
	/// la cuisson n'avance que tant que le feu brûle. Si le bois s'épuise, le feu s'éteint et la cuisson restante est perdue.
	/// </summary>
	private void SimulerRattrapagePitFeuRoche(ref int comb, byte essence, ref double resteUnite, double dureeUnite,
		ref double progress, ref int crus, ref int cuits, double tempsEcoule, int plafondCuits)
	{
		if (dureeUnite <= 0d)
			dureeUnite = DureeCombustionPitFeuRochePourEssence(essence);
		double dureeCuisson = Math.Max(0.001d, DureeCuissonPitFeuRocheSteakSec);
		double t = tempsEcoule;
		bool allume = resteUnite > 0.0001d;
		int garde = 0;
		while (t > 0.0001d && allume && garde++ < 1000000)
		{
			double dt = Math.Min(t, resteUnite);
			// Cuisson active seulement si un steak cru attend et que le résultat n'est pas plein.
			if (crus > 0 && cuits < plafondCuits)
			{
				progress += dt;
				while (progress >= dureeCuisson && crus > 0 && cuits < plafondCuits)
				{
					progress -= dureeCuisson;
					crus--;
					cuits++;
				}
				if (crus <= 0 || cuits >= plafondCuits)
					progress = 0d;
			}
			else
			{
				progress = 0d;
			}
			resteUnite -= dt;
			t -= dt;
			if (resteUnite <= 0.0001d)
			{
				if (comb > 0)
				{
					comb--;
					resteUnite = dureeUnite; // unité suivante (même essence).
				}
				else
				{
					allume = false;
					resteUnite = 0d; // plus de bois → feu éteint.
				}
			}
		}
	}

	/// <summary>Réécrit les 3 slots du feu roche (combustible+essence, steaks crus, steaks cuits) après rattrapage/chargement.</summary>
	private void RestaurerGrillePitFeuRoche(int combQte, int combId, byte essence, int crus, int cuits, byte sBot, int sChi, int sMor)
	{
		AssurerGrillePitFeuRoche3Slots();
		int idComb = combId == BlocChutant.ID_BRANCHE ? BlocChutant.ID_BRANCHE : 32;
		GrillePlanTravailAtelier[PitFeuRocheSlotCombustible] = combQte > 0
			? new SlotInventaire { ID = idComb, Quantite = combQte, IndexBotanique = essence }
			: new SlotInventaire();
		GrillePlanTravailAtelier[PitFeuRocheSlotCuisson] = crus > 0
			? new SlotInventaire { ID = Joueur.IdObjetSteakCru, Quantite = crus, IndexBotanique = sBot, IndexChimique = sChi, IndexMorphologique = sMor }
			: new SlotInventaire();
		GrillePlanTravailAtelier[PitFeuRocheSlotResultat] = cuits > 0
			? new SlotInventaire { ID = Joueur.IdObjetSteakCuit, Quantite = cuits, IndexBotanique = sBot, IndexChimique = sChi, IndexMorphologique = sMor }
			: new SlotInventaire();
	}

	public bool EstPitFeuAllume()
	{
		if (ID_Objet == Joueur.IdObjetPitFeu)
			return _pitFeuResteSec > 0.001d;
		if (ID_Objet == Joueur.IdObjetPitFeuRoche)
			return _pitFeuRocheResteSec > 0.001d;
		return false;
	}

	public bool ActiverPitFeuAllume(double dureeSec = DureeCombustionPitFeuSec)
	{
		if (ID_Objet != Joueur.IdObjetPitFeu)
			return false;
		_pitFeuResteSec = Math.Max(1d, dureeSec);
		_pitFeuDernierSyncRestantSec = -1d;
		ActiverVisuelPitFeu(true);
		SynchroniserGenomePitFeuDepuisReste();
		return true;
	}

	private static bool EstSlotCombustiblePitFeuRoche(SlotInventaire s)
	{
		return !s.EstVide && (s.ID == 32 || s.ID == BlocChutant.ID_BRANCHE);
	}

	private static bool EstSlotCuissonPitFeuRoche(SlotInventaire s)
	{
		return !s.EstVide && s.ID == Joueur.IdObjetSteakCru;
	}

	/// <summary>Durée de combustion (s) d'UNE unité de combustible selon l'essence de bois (branche/bâton). Repli = <see cref="DureeCombustionPitFeuSec"/>.</summary>
	private static double DureeCombustionPitFeuRochePourEssence(byte essence)
	{
		switch (essence)
		{
			case LSystem_Botanique.IndexChene:
			case LSystem_Botanique.IndexBouleau:
				return 60.0;
			case LSystem_Botanique.IndexPin:
			case LSystem_Botanique.IndexSapin:
				return 40.0;
			case LSystem_Botanique.IndexJungle:
				return 20.0;
			case LSystem_Botanique.IndexCheneMort:
				return 120.0;
			case LSystem_Botanique.IndexBouleauMort:
				return 80.0;
			default:
				return DureeCombustionPitFeuSec;
		}
	}

	/// <summary>
	/// Progression de cuisson du steak en cours (0..1) pour la barre UI, ou -1 si rien ne cuit
	/// (feu éteint, pas de steak cru dans le slot cuisson, ou slot résultat plein).
	/// </summary>
	public float ObtenirProgressionCuissonPitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null)
			return -1f;
		if (_pitFeuRocheResteSec <= 0.001d)
			return -1f;
		AssurerGrillePitFeuRoche3Slots();
		SlotInventaire cuisson = GrillePlanTravailAtelier[PitFeuRocheSlotCuisson];
		if (!EstSlotCuissonPitFeuRoche(cuisson))
			return -1f;
		SlotInventaire resultat = GrillePlanTravailAtelier[PitFeuRocheSlotResultat];
		if (!resultat.EstVide && EstSlotResultatPitFeuRoche(resultat)
			&& Joueur.ObtenirQuantiteSlot(resultat) >= Mathf.Max(1, Joueur.ObtenirPileMax(resultat)))
			return -1f; // Résultat plein : cuisson en pause.
		if (DureeCuissonPitFeuRocheSteakSec <= 0.0)
			return -1f;
		return Mathf.Clamp((float)(_pitFeuRocheProgressCuissonSec / DureeCuissonPitFeuRocheSteakSec), 0f, 1f);
	}

	/// <summary>Progression de combustion de l'unité de bois en cours (1 = pleine, 0 = consumée), ou -1 si le feu est éteint. Pour la barre UI.</summary>
	public float ObtenirProgressionCombustionPitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || _pitFeuRocheResteSec <= 0.001d)
			return -1f;
		double total = Math.Max(_pitFeuRocheDureeUniteCouranteSec, _pitFeuRocheResteSec);
		if (total <= 0.0)
			return -1f;
		return Mathf.Clamp((float)(_pitFeuRocheResteSec / total), 0f, 1f);
	}

	/// <summary>Essence du combustible actuellement dans le slot (pour calculer sa durée avant consommation). Chêne par défaut.</summary>
	private byte ObtenirEssenceCombustiblePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null)
			return LSystem_Botanique.IndexChene;
		AssurerGrillePitFeuRoche3Slots();
		SlotInventaire slot = GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!EstSlotCombustiblePitFeuRoche(slot))
			return LSystem_Botanique.IndexChene;
		return slot.IndexBotanique;
	}

	private static bool EstSlotResultatPitFeuRoche(SlotInventaire s)
	{
		return !s.EstVide && s.ID == Joueur.IdObjetSteakCuit;
	}

	private void AssurerGrillePitFeuRoche3Slots()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		if (GrillePlanTravailAtelier == null || GrillePlanTravailAtelier.Length < 9)
		{
			var ancienne = GrillePlanTravailAtelier;
			GrillePlanTravailAtelier = new SlotInventaire[9];
			if (ancienne != null)
			{
				int n = Mathf.Min(ancienne.Length, GrillePlanTravailAtelier.Length);
				for (int i = 0; i < n; i++)
					GrillePlanTravailAtelier[i] = ancienne[i];
			}
		}

		int totalCombustible = 0;
		int totalCru = 0;
		int totalCuit = 0;
		// Préserver l'ID (branche/bâton) ET l'essence réels du combustible — sinon les branches
		// étaient transformées en bâtons de chêne et les durées par essence étaient perdues.
		int idCombustible = 32;
		byte essenceCombustible = LSystem_Botanique.IndexChene;
		bool combustibleVu = false;
		int nSlots = Mathf.Min(9, GrillePlanTravailAtelier.Length);
		for (int i = 0; i < nSlots; i++)
		{
			SlotInventaire s = GrillePlanTravailAtelier[i];
			if (EstSlotCombustiblePitFeuRoche(s))
			{
				totalCombustible += Joueur.ObtenirQuantiteSlot(s);
				if (!combustibleVu)
				{
					combustibleVu = true;
					idCombustible = s.ID;
					essenceCombustible = s.IndexBotanique;
				}
			}
			else if (EstSlotCuissonPitFeuRoche(s))
				totalCru += Joueur.ObtenirQuantiteSlot(s);
			else if (EstSlotResultatPitFeuRoche(s))
				totalCuit += Joueur.ObtenirQuantiteSlot(s);
		}

		for (int i = 0; i < nSlots; i++)
			GrillePlanTravailAtelier[i] = new SlotInventaire();

		var combustible = new SlotInventaire { ID = idCombustible, Quantite = 1, IndexBotanique = essenceCombustible };
		int maxCombustible = Mathf.Max(1, Joueur.ObtenirPileMax(combustible));
		combustible.Quantite = Mathf.Clamp(totalCombustible, 0, maxCombustible);
		if (combustible.Quantite > 0)
			GrillePlanTravailAtelier[PitFeuRocheSlotCombustible] = combustible;

		var cru = new SlotInventaire { ID = Joueur.IdObjetSteakCru, Quantite = 1 };
		int maxCru = Mathf.Max(1, Joueur.ObtenirPileMax(cru));
		cru.Quantite = Mathf.Clamp(totalCru, 0, maxCru);
		if (cru.Quantite > 0)
			GrillePlanTravailAtelier[PitFeuRocheSlotCuisson] = cru;

		var cuit = new SlotInventaire { ID = Joueur.IdObjetSteakCuit, Quantite = 1 };
		int maxCuit = Mathf.Max(1, Joueur.ObtenirPileMax(cuit));
		cuit.Quantite = Mathf.Clamp(totalCuit, 0, maxCuit);
		if (cuit.Quantite > 0)
			GrillePlanTravailAtelier[PitFeuRocheSlotResultat] = cuit;
	}

	private int CompterCombustiblePitFeuRocheDepuisGrille()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null)
			return 0;
		AssurerGrillePitFeuRoche3Slots();
		SlotInventaire slot = GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!EstSlotCombustiblePitFeuRoche(slot))
			return 0;
		return Mathf.Clamp(Joueur.ObtenirQuantiteSlot(slot), 0, 999);
	}

	private int AjouterCombustiblePitFeuRocheDansGrille(int quantite, int idCombustible, byte essence)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null || quantite <= 0)
			return 0;
		AssurerGrillePitFeuRoche3Slots();
		if (idCombustible != 32 && idCombustible != BlocChutant.ID_BRANCHE)
			idCombustible = 32;
		ref SlotInventaire slot = ref GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!slot.EstVide && !EstSlotCombustiblePitFeuRoche(slot))
			return 0;
		if (!slot.EstVide && slot.ID != idCombustible)
			return 0;
		// Ne pas mélanger deux essences dans la même pile : leurs durées de combustion diffèrent.
		if (!slot.EstVide && slot.IndexBotanique != essence)
			return 0;

		if (slot.EstVide)
		{
			slot = new SlotInventaire
			{
				ID = idCombustible,
				Quantite = 0,
				IndexBotanique = essence
			};
		}
		int maxPile = Mathf.Max(1, Joueur.ObtenirPileMax(slot));
		int q = Joueur.ObtenirQuantiteSlot(slot);
		int depose = Mathf.Min(Mathf.Max(0, maxPile - q), quantite);
		if (depose <= 0)
			return 0;
		slot.Quantite = q + depose;
		return depose;
	}

	private bool RetirerCombustiblePitFeuRocheDepuisGrille(int quantite)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null || quantite <= 0)
			return false;
		AssurerGrillePitFeuRoche3Slots();
		ref SlotInventaire slot = ref GrillePlanTravailAtelier[PitFeuRocheSlotCombustible];
		if (!EstSlotCombustiblePitFeuRoche(slot))
			return false;
		int q = Joueur.ObtenirQuantiteSlot(slot);
		if (q < quantite)
			return false;
		int restant = q - quantite;
		if (restant <= 0) slot = new SlotInventaire();
		else slot.Quantite = restant;
		return true;
	}

	private void ReinitialiserProgressCuissonPitFeuRoche()
	{
		if (_pitFeuRocheProgressCuissonSec <= 0.001d)
		{
			_pitFeuRocheProgressCuissonSec = 0d;
			return;
		}
		_pitFeuRocheProgressCuissonSec = 0d;
		SynchroniserGenomePitFeuRoche();
	}

	private void TraiterCuissonPitFeuRoche(double delta)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche || GrillePlanTravailAtelier == null || _pitFeuRocheResteSec <= 0.001d)
			return;
		AssurerGrillePitFeuRoche3Slots();

		ref SlotInventaire slotCuisson = ref GrillePlanTravailAtelier[PitFeuRocheSlotCuisson];
		ref SlotInventaire slotResultat = ref GrillePlanTravailAtelier[PitFeuRocheSlotResultat];

		if (!EstSlotCuissonPitFeuRoche(slotCuisson))
		{
			ReinitialiserProgressCuissonPitFeuRoche();
			return;
		}

		if (!slotResultat.EstVide)
		{
			if (!EstSlotResultatPitFeuRoche(slotResultat))
			{
				ReinitialiserProgressCuissonPitFeuRoche();
				return;
			}
			int maxPileRes = Mathf.Max(1, Joueur.ObtenirPileMax(slotResultat));
			if (Joueur.ObtenirQuantiteSlot(slotResultat) >= maxPileRes)
			{
				ReinitialiserProgressCuissonPitFeuRoche();
				return;
			}
		}

		_pitFeuRocheProgressCuissonSec += Math.Max(0d, delta);
		bool conversion = false;
		while (_pitFeuRocheProgressCuissonSec >= DureeCuissonPitFeuRocheSteakSec)
		{
			if (!EstSlotCuissonPitFeuRoche(slotCuisson))
			{
				ReinitialiserProgressCuissonPitFeuRoche();
				break;
			}

			var steakCuit = slotCuisson;
			steakCuit.ID = Joueur.IdObjetSteakCuit;
			steakCuit.Quantite = 1;

			if (slotResultat.EstVide)
			{
				slotResultat = steakCuit;
			}
			else
			{
				int maxPileRes = Mathf.Max(1, Joueur.ObtenirPileMax(slotResultat));
				if (!Joueur.SontEmpilables(slotResultat, steakCuit) || Joueur.ObtenirQuantiteSlot(slotResultat) >= maxPileRes)
					break;
				slotResultat.Quantite = Joueur.ObtenirQuantiteSlot(slotResultat) + 1;
			}

			int qCru = Joueur.ObtenirQuantiteSlot(slotCuisson) - 1;
			if (qCru <= 0) slotCuisson = new SlotInventaire();
			else slotCuisson.Quantite = qCru;
			ObtenirJoueurMonde()?.AjouterXpMetier("Cuisinier", 1UL);

			_pitFeuRocheProgressCuissonSec -= DureeCuissonPitFeuRocheSteakSec;
			conversion = true;
		}

		if (conversion)
			SynchroniserGenomePitFeuRoche();
	}

	public int ObtenirStockCombustiblePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return 0;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		return Mathf.Max(0, _pitFeuRocheStockCombustible);
	}

	public bool EstPitFeuRocheAllume()
	{
		return ID_Objet == Joueur.IdObjetPitFeuRoche && _pitFeuRocheResteSec > 0.001d;
	}

	public bool AjouterCombustiblePitFeuRoche(int quantite = 1, int idCombustible = 32, byte essence = LSystem_Botanique.IndexChene)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		if (quantite <= 0)
			return false;
		int stockAvant = CompterCombustiblePitFeuRocheDepuisGrille();
		int espace = Mathf.Max(0, 999 - stockAvant);
		if (espace <= 0)
			return false;
		int ajoute = AjouterCombustiblePitFeuRocheDansGrille(Mathf.Min(espace, quantite), idCombustible, essence);
		if (ajoute <= 0)
			return false;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		SynchroniserGenomePitFeuRoche();
		return true;
	}

	public bool ActiverPitFeuRocheAllume(double dureeSec = DureeCombustionPitFeuSec)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		if (_pitFeuRocheResteSec > 0.001d)
			return true;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		if (_pitFeuRocheStockCombustible <= 0)
			return false;
		byte essenceAllumage = ObtenirEssenceCombustiblePitFeuRoche();
		if (!RetirerCombustiblePitFeuRocheDepuisGrille(1))
			return false;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		// Durée selon l'essence de la branche/bâton consommé (le paramètre dureeSec n'est plus utilisé pour le feu roche).
		_pitFeuRocheResteSec = Math.Max(1d, DureeCombustionPitFeuRochePourEssence(essenceAllumage));
		_pitFeuRocheDureeUniteCouranteSec = _pitFeuRocheResteSec;
		_pitFeuRocheDernierSyncRestantSec = -1d;
		ActiverVisuelPitFeu(true);
		SynchroniserGenomePitFeuRoche();
		return true;
	}

	public bool EteindrePitFeuRoche()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		_pitFeuRocheResteSec = 0d;
		_pitFeuRocheDernierSyncRestantSec = -1d;
		ActiverVisuelPitFeu(false);
		SynchroniserGenomePitFeuRoche();
		return true;
	}

	public void SynchroniserCombustiblePitFeuRocheDepuisGrille()
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return;
		AssurerGrillePitFeuRoche3Slots();
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		SynchroniserGenomePitFeuRoche();
	}
}
