using Godot;

public partial class ItemPhysique
{
	private Node3D _fourTorchieFlammesMeche;
	private GpuParticles3D _fourTorchieFlammesMecheParticles;
	private GpuParticles3D _fourTorchieFumeeSommet;
	private OmniLight3D _fourTorchieLumiere;
	private bool _fourTorchieAncragesVisuelsResolus;
	private Vector3 _fourTorchieFumeeSommetBaseLocale;

	private void AssurerVisuelFourTorchieCree()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;

		if (_fourTorchieFlammesMeche == null || !GodotObject.IsInstanceValid(_fourTorchieFlammesMeche))
		{
			_fourTorchieFlammesMeche = new Node3D
			{
				Name = "FourTorchieFlammesMeche",
				Visible = false
			};
			StandardMaterial3D matFlamme = CreerMateriauFlammePitTexture();
			for (int i = 0; i < 4; i++)
			{
				var mi = new MeshInstance3D
				{
					Name = $"FlammeMechePlan{i}",
					Mesh = new QuadMesh { Size = new Vector2(0.72f, 0.38f) },
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
				};
				mi.MaterialOverride = matFlamme;
				mi.RotationDegrees = new Vector3(0f, i * 45f, 0f);
				mi.Position = new Vector3(0f, 0.06f + i * 0.012f, 0f);
				_fourTorchieFlammesMeche.AddChild(mi);
			}
			AddChild(_fourTorchieFlammesMeche);
		}

		if (_fourTorchieFlammesMecheParticles == null || !GodotObject.IsInstanceValid(_fourTorchieFlammesMecheParticles))
		{
			_fourTorchieFlammesMecheParticles = new GpuParticles3D
			{
				Name = "FourTorchieFlammesMecheParticles",
				Amount = 96,
				Lifetime = 0.82f,
				OneShot = false,
				Emitting = false,
				Visible = false
			};
			var meshFlamme = new QuadMesh { Size = new Vector2(0.42f, 0.22f) };
			meshFlamme.Material = CreerMateriauFlammePitTexture();
			_fourTorchieFlammesMecheParticles.DrawPass1 = meshFlamme;
			_fourTorchieFlammesMecheParticles.ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 1.2f, 0f),
				InitialVelocityMin = 0.08f,
				InitialVelocityMax = 0.24f,
				ScaleMin = 0.55f,
				ScaleMax = 1.25f
			};
			AddChild(_fourTorchieFlammesMecheParticles);
		}

		if (_fourTorchieFumeeSommet == null || !GodotObject.IsInstanceValid(_fourTorchieFumeeSommet))
		{
			_fourTorchieFumeeSommet = new GpuParticles3D
			{
				Name = "FourTorchieFumeeSommet",
				Amount = 42,
				Lifetime = 4.5f,
				OneShot = false,
				Emitting = false,
				Visible = false,
				VisibilityAabb = new Aabb(new Vector3(-2f, -1f, -2f), new Vector3(4f, 6f, 4f))
			};
			var meshFumee = new SphereMesh { Radius = 0.12f, Height = 0.22f, RadialSegments = 8, Rings = 6 };
			meshFumee.Material = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.68f, 0.68f, 0.68f, 0.58f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};
			_fourTorchieFumeeSommet.DrawPass1 = meshFumee;
			_fourTorchieFumeeSommet.ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, 1f, 0f),
				Gravity = new Vector3(0f, 0.28f, 0f),
				InitialVelocityMin = 0.04f,
				InitialVelocityMax = 0.14f,
				ScaleMin = 0.35f,
				ScaleMax = 1.35f
			};
			AddChild(_fourTorchieFumeeSommet);
		}

		if (_fourTorchieLumiere == null || !GodotObject.IsInstanceValid(_fourTorchieLumiere))
		{
			_fourTorchieLumiere = new OmniLight3D
			{
				Name = "FourTorchieLumiere",
				Visible = false
			};
			ConfigurerLumiereFeuCamp(_fourTorchieLumiere);
			AddChild(_fourTorchieLumiere);
		}

		ResoudreAncragesVisuelsFourTorchie();
	}

	private void ResoudreAncragesVisuelsFourTorchie()
	{
		if (_fourTorchieAncragesVisuelsResolus)
			return;

		Node3D racineModele = GetNodeOrNull<Node3D>("MeshInstance3D/ModeleArme")
			?? GetNodeOrNull<Node3D>("MeshInstance3D");
		if (racineModele == null)
			return;

		MeshInstance3D boisBrule = ChercherMeshFourTorchieRecursif(racineModele, "bois_brul");
		MeshInstance3D scorie = ChercherMeshFourTorchieRecursif(racineModele, "scorie");

		Vector3 posMeche = boisBrule != null
			? PositionMondeVersLocaleFourTorchie(EstimerSommetMeshLocal(boisBrule))
			: new Vector3(0.55f, 0.42f, 0.08f);

		Vector3 posFumee = scorie != null
			? PositionMondeVersLocaleFourTorchie(EstimerSommetMeshLocal(scorie) + Vector3.Up * 0.18f)
			: new Vector3(0f, Joueur.TailleFourTorchiePoseMetres * 0.88f, 0f);

		Vector3 posLumiere = posMeche.Lerp(posFumee, 0.35f);

		if (_fourTorchieFlammesMeche != null && GodotObject.IsInstanceValid(_fourTorchieFlammesMeche))
			_fourTorchieFlammesMeche.Position = posMeche;
		if (_fourTorchieFlammesMecheParticles != null && GodotObject.IsInstanceValid(_fourTorchieFlammesMecheParticles))
			_fourTorchieFlammesMecheParticles.Position = posMeche + new Vector3(0f, 0.05f, 0f);
		if (_fourTorchieFumeeSommet != null && GodotObject.IsInstanceValid(_fourTorchieFumeeSommet))
		{
			_fourTorchieFumeeSommetBaseLocale = posFumee;
			_fourTorchieFumeeSommet.Position = posFumee;
		}
		if (_fourTorchieLumiere != null && GodotObject.IsInstanceValid(_fourTorchieLumiere))
			_fourTorchieLumiere.Position = posLumiere;

		_fourTorchieAncragesVisuelsResolus = boisBrule != null || scorie != null;
	}

	private Vector3 PositionMondeVersLocaleFourTorchie(Vector3 pointMonde)
	{
		Basis inv = GlobalTransform.Basis.Inverse();
		return inv * (pointMonde - GlobalPosition);
	}

	private static Vector3 EstimerSommetMeshLocal(MeshInstance3D mesh)
	{
		if (mesh?.Mesh == null)
			return mesh?.GlobalPosition ?? Vector3.Zero;
		Aabb aabb = mesh.Mesh.GetAabb();
		Vector3 sommetLocal = aabb.Position + new Vector3(aabb.Size.X * 0.5f, aabb.Size.Y, aabb.Size.Z * 0.5f);
		return mesh.GlobalTransform * sommetLocal;
	}

	private static MeshInstance3D ChercherMeshFourTorchieRecursif(Node racine, string fragmentNom)
	{
		if (racine is MeshInstance3D mi && mi.Name.ToString().ToLowerInvariant().Contains(fragmentNom))
			return mi;
		foreach (Node enfant in racine.GetChildren())
		{
			MeshInstance3D trouve = ChercherMeshFourTorchieRecursif(enfant, fragmentNom);
			if (trouve != null)
				return trouve;
		}
		return null;
	}

	private void MettreAJourVisuelFourTorchie(bool? forceActif = null)
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;

		bool bruleCombustible = _fourTorchieAllume && (_fourTorchieResteCombSec > 0.001d || _fourTorchieAnomalieAnthracite);
		bool actif = forceActif ?? EstFourTorchieActif();

		AssurerVisuelFourTorchieCree();
		ResoudreAncragesVisuelsFourTorchie();

		if (_fourTorchieFlammesMeche != null)
			_fourTorchieFlammesMeche.Visible = bruleCombustible;
		if (_fourTorchieFlammesMecheParticles != null)
		{
			_fourTorchieFlammesMecheParticles.Visible = bruleCombustible;
			_fourTorchieFlammesMecheParticles.Emitting = bruleCombustible;
		}
		if (_fourTorchieFumeeSommet != null)
		{
			_fourTorchieFumeeSommet.Visible = actif;
			_fourTorchieFumeeSommet.Emitting = actif;
		}
		if (_fourTorchieLumiere != null)
			_fourTorchieLumiere.Visible = actif;
	}

	private void AnimerVisuelFourTorchie(float dt)
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie || !EstFourTorchieActif())
			return;
		if (_fourTorchieFlammesMeche == null || !GodotObject.IsInstanceValid(_fourTorchieFlammesMeche))
			return;

		float t = (float)Time.GetTicksMsec() * 0.001f;
		float pulseFast = Mathf.Sin(t * 8.4f);
		float pulseSlow = Mathf.Sin(t * 3.5f + 1.1f);
		float swayX = 0.55f * Mathf.Sin(t * 1.5f) + 0.28f * Mathf.Sin(t * 2.7f + 0.6f);
		float swayZ = 0.48f * Mathf.Sin(t * 1.8f + 0.4f) + 0.24f * Mathf.Sin(t * 3.1f + 1.2f);

		_fourTorchieFlammesMeche.Scale = new Vector3(
			0.92f + 0.1f * pulseFast + 0.05f * pulseSlow,
			1.05f + 0.18f * Mathf.Sin(t * 6.8f) + 0.08f * pulseSlow,
			0.92f + 0.08f * Mathf.Sin(t * 7.6f) + 0.04f * pulseSlow);
		_fourTorchieFlammesMeche.RotationDegrees = new Vector3(2.4f * swayX, 0f, 2f * swayZ);

		if (_fourTorchieFlammesMecheParticles != null && GodotObject.IsInstanceValid(_fourTorchieFlammesMecheParticles)
			&& _fourTorchieFlammesMecheParticles.ProcessMaterial is ParticleProcessMaterial matFlamme)
		{
			float gust = 0.5f + 0.5f * Mathf.Sin(t * 2.1f + 0.2f);
			matFlamme.Direction = new Vector3(0.11f * swayX, 1f, 0.11f * swayZ);
			matFlamme.InitialVelocityMin = 0.07f + 0.02f * gust;
			matFlamme.InitialVelocityMax = 0.22f + 0.05f * gust;
		}

		if (_fourTorchieFumeeSommet != null && GodotObject.IsInstanceValid(_fourTorchieFumeeSommet))
			_fourTorchieFumeeSommet.Position = _fourTorchieFumeeSommetBaseLocale
				+ new Vector3(0.015f * swayX, 0.02f * Mathf.Sin(t * 2.4f), 0.015f * swayZ);

		if (_fourTorchieLumiere != null && GodotObject.IsInstanceValid(_fourTorchieLumiere))
			_fourTorchieLumiere.LightEnergy = LumiereFeuEnergy - 0.3f + 0.26f * Mathf.Sin(t * 9.2f) + 0.12f * pulseSlow;
	}
}
