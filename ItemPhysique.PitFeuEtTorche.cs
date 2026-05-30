using Godot;
using System;
using System.Collections.Generic;

public partial class ItemPhysique : RigidBody3D
{
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
				LightColor = new Color(1.0f, 0.58f, 0.26f),
				LightEnergy = 2.2f,
				OmniRange = 5.8f,
				Position = new Vector3(0f, 0.28f, 0f),
				Visible = false
			};
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
				Amount = 54,
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
				Amount = 16,
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

		OmniLight3D light = parent.GetNodeOrNull<OmniLight3D>("TorcheLumiere");
		if (light == null)
		{
			light = new OmniLight3D
			{
				Name = "TorcheLumiere",
				LightColor = new Color(1.0f, 0.62f, 0.30f),
				LightEnergy = 1.7f,
				OmniRange = 5.1f,
				Position = new Vector3(0f, 0.90f, 0f)
			};
			parent.AddChild(light);
		}
		light.Visible = true;
	}

	private void ActiverVisuelTorche(bool actif)
	{
		if (ID_Objet != Joueur.IdObjetTorche)
			return;
		if (actif)
			AttacherVisuelFlammeTorche(this);
		_torcheFlamme = GetNodeOrNull<Node3D>("TorcheFlamme");
		_torcheLight = GetNodeOrNull<OmniLight3D>("TorcheLumiere");
		if (_torcheFlamme != null)
		{
			_torcheFlamme.Visible = actif;
			GpuParticles3D flammes = _torcheFlamme.GetNodeOrNull<GpuParticles3D>("TorcheFlammesParticles");
			GpuParticles3D fumee = _torcheFlamme.GetNodeOrNull<GpuParticles3D>("TorcheFumeeParticles");
			if (flammes != null)
			{
				flammes.Emitting = actif;
				flammes.Visible = actif;
			}
			if (fumee != null)
			{
				fumee.Emitting = actif;
				fumee.Visible = actif;
			}
		}
		if (_torcheLight != null)
			_torcheLight.Visible = actif;
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
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		long finMs = _pitFeuRocheResteSec > 0.001d
			? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)Mathf.Round((float)(_pitFeuRocheResteSec * 1000.0))
			: 0L;
		long progressCuissonMs = (long)Mathf.Round((float)Math.Max(0d, _pitFeuRocheProgressCuissonSec * 1000.0));
		GenomeAssemblage = $"PITFEUROCHE:{_pitFeuRocheStockCombustible}:{finMs}:{progressCuissonMs}";
		SetMeta(Joueur.MetaGenomeAssemblage, GenomeAssemblage);
		SetMeta(MetaPitFeuRocheStockCombustible, _pitFeuRocheStockCombustible);
		SetMeta(MetaPitFeuRocheFinCombustionUnixMs, finMs);
		SetMeta(MetaPitFeuRocheProgressCuissonMs, progressCuissonMs);
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
		long finMs = 0L;
		long progressCuissonMs = 0L;
		int stock = 0;
		if (!string.IsNullOrEmpty(GenomeAssemblage) && GenomeAssemblage.StartsWith("PITFEUROCHE:", StringComparison.Ordinal))
		{
			string brut = GenomeAssemblage.Substring("PITFEUROCHE:".Length);
			string[] morceaux = brut.Split(':');
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
			AjouterCombustiblePitFeuRocheDansGrille(_pitFeuRocheStockCombustible, 32);
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
		int nSlots = Mathf.Min(9, GrillePlanTravailAtelier.Length);
		for (int i = 0; i < nSlots; i++)
		{
			SlotInventaire s = GrillePlanTravailAtelier[i];
			if (EstSlotCombustiblePitFeuRoche(s))
				totalCombustible += Joueur.ObtenirQuantiteSlot(s);
			else if (EstSlotCuissonPitFeuRoche(s))
				totalCru += Joueur.ObtenirQuantiteSlot(s);
			else if (EstSlotResultatPitFeuRoche(s))
				totalCuit += Joueur.ObtenirQuantiteSlot(s);
		}

		for (int i = 0; i < nSlots; i++)
			GrillePlanTravailAtelier[i] = new SlotInventaire();

		var combustible = new SlotInventaire { ID = 32, Quantite = 1, IndexBotanique = LSystem_Botanique.IndexChene };
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

	private int AjouterCombustiblePitFeuRocheDansGrille(int quantite, int idCombustible)
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

		if (slot.EstVide)
		{
			slot = new SlotInventaire
			{
				ID = idCombustible,
				Quantite = 0,
				IndexBotanique = LSystem_Botanique.IndexChene
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

	public bool AjouterCombustiblePitFeuRoche(int quantite = 1, int idCombustible = 32)
	{
		if (ID_Objet != Joueur.IdObjetPitFeuRoche)
			return false;
		if (quantite <= 0)
			return false;
		int stockAvant = CompterCombustiblePitFeuRocheDepuisGrille();
		int espace = Mathf.Max(0, 999 - stockAvant);
		if (espace <= 0)
			return false;
		int ajoute = AjouterCombustiblePitFeuRocheDansGrille(Mathf.Min(espace, quantite), idCombustible);
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
		if (!RetirerCombustiblePitFeuRocheDepuisGrille(1))
			return false;
		_pitFeuRocheStockCombustible = CompterCombustiblePitFeuRocheDepuisGrille();
		_pitFeuRocheResteSec = Math.Max(1d, dureeSec);
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
