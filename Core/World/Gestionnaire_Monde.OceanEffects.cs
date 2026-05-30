using Godot;

/// <summary>
/// Océan physique + effets d'eau (remous, éclaboussures). Partie de <see cref="Gestionnaire_Monde"/>.
/// CONTRAT: détection d'eau par voxels et effets visuels identiques au comportement historique.
/// </summary>
public partial class Gestionnaire_Monde : Node3D
{
	private void CreerAreaOcean()
	{
		float demiRayon = RayonMondeChunks * TailleChunk;
		float hauteurZone = NiveauEauOcean + 500f; // Couvre jusqu'en profondeur -500
		var ocean = new Area3D { Name = "Ocean_Physique" };
		// IMPORTANT : pas d'effet physique global ici.
		// Les forces eau sont gérées par chaque corps selon son ratio d'immersion.
		ocean.GravitySpaceOverride = Area3D.SpaceOverride.Disabled;
		ocean.Gravity = 0f;
		ocean.GravityDirection = new Vector3(0, -1, 0);
		ocean.GravityPoint = false;
		ocean.LinearDamp = 0f;
		ocean.LinearDampSpaceOverride = Area3D.SpaceOverride.Disabled;
		ocean.AngularDamp = 0f;
		ocean.AngularDampSpaceOverride = Area3D.SpaceOverride.Disabled;
		ocean.Priority = 100; // Priorité haute sur le monde par défaut

		var col = new CollisionShape3D();
		col.Shape = new BoxShape3D { Size = new Vector3(demiRayon * 2f, hauteurZone, demiRayon * 2f) };
		ocean.AddChild(col);
		ocean.Position = new Vector3(0, (NiveauEauOcean - 500f) / 2f, 0); // Centre du volume
		ocean.BodyEntered += SurCorpsEntreOcean;
		ocean.BodyExited += SurCorpsSortOcean;
		AddChild(ocean);
		_oceanPhysique = ocean;
	}

	private void SurCorpsEntreOcean(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return;
		if (!CorpsAuContactEauVoxel(corps)) return;

		ulong id = corps.GetInstanceId();
		if (!_corpsDansOcean.Add(id)) return;

		if (corps is CharacterBody3D or RigidBody3D)
			AssurerEffetRemousSuiviPour(corps, id);

		if (corps is RigidBody3D rb)
		{
			// Seulement un objet qui tombe (vitesse verticale descendante suffisante).
			float vitesseChute = -rb.LinearVelocity.Y;
			if (vitesseChute < 2.0f) return;
			float intensite = Mathf.Clamp(vitesseChute / 18f, 0.35f, 1.35f);
			Vector3 impactSurface = rb.GlobalPosition;
			impactSurface.Y = NiveauEauOcean + 0.04f;
			CreerEclaboussureSurface(impactSurface, intensite);
		}
	}

	private void SurCorpsSortOcean(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return;
		ulong id = corps.GetInstanceId();
		_corpsDansOcean.Remove(id);
		RetirerEffetRemousSuivi(id);
	}

	private void AssurerEffetRemousSuiviPour(Node3D corps, ulong id)
	{
		if (_effetsRemousParCorps.ContainsKey(id)) return;
		AssurerConteneurEffetsEau();
		if (_conteneurEffetsEau == null || !GodotObject.IsInstanceValid(_conteneurEffetsEau)) return;

		var p = new GpuParticles3D
		{
			Name = $"Remous_{id}",
			Amount = 10,
			Lifetime = 0.50f,
			OneShot = false,
			Emitting = false
		};
		var mat = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, 1f, 0f),
			Spread = 24f,
			InitialVelocityMin = 0.18f,
			InitialVelocityMax = 0.58f,
			Gravity = new Vector3(0f, -1.2f, 0f),
			ScaleMin = 0.08f,
			ScaleMax = 0.15f,
			DampingMin = 0.7f,
			DampingMax = 1.2f
		};
		p.ProcessMaterial = mat;
		p.DrawPass1 = new QuadMesh { Size = new Vector2(0.06f, 0.06f) };
		p.MaterialOverride = ObtenirMaterielEclaboussureEau();
		_conteneurEffetsEau.AddChild(p);
		if (p.IsInsideTree())
			p.GlobalPosition = new Vector3(corps.GlobalPosition.X, ObtenirNiveauSurfaceEau(), corps.GlobalPosition.Z);
		_corpsSuiviRemous[id] = corps;
		_effetsRemousParCorps[id] = p;
	}

	private void RetirerEffetRemousSuivi(ulong id)
	{
		_corpsSuiviRemous.Remove(id);
		if (_effetsRemousParCorps.TryGetValue(id, out var p))
		{
			_effetsRemousParCorps.Remove(id);
			if (p != null && GodotObject.IsInstanceValid(p))
				p.QueueFree();
		}
	}

	private void MettreAJourEffetsRemousSuivis()
	{
		if (_effetsRemousParCorps.Count == 0) return;
		float ySurface = ObtenirNiveauSurfaceEau() + 0.03f;
		_tmpRemousASupprimer.Clear();
		foreach (var kv in _effetsRemousParCorps)
		{
			ulong id = kv.Key;
			GpuParticles3D p = kv.Value;
			if (p == null || !GodotObject.IsInstanceValid(p))
			{
				_tmpRemousASupprimer.Add(id);
				continue;
			}
			if (!_corpsSuiviRemous.TryGetValue(id, out Node3D corps) || corps == null || !GodotObject.IsInstanceValid(corps) || !_corpsDansOcean.Contains(id))
			{
				_tmpRemousASupprimer.Add(id);
				continue;
			}
			if (!corps.IsInsideTree() || !p.IsInsideTree())
			{
				_tmpRemousASupprimer.Add(id);
				continue;
			}

			float vitesseHoriz = 0f;
			if (corps is CharacterBody3D cb)
			{
				Vector3 v = cb.Velocity;
				vitesseHoriz = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
			}
			else if (corps is RigidBody3D rb)
			{
				Vector3 v = rb.LinearVelocity;
				vitesseHoriz = Mathf.Sqrt(v.X * v.X + v.Z * v.Z);
			}

			bool auContactEau = CorpsAuContactEauVoxel(corps);
			bool actif = vitesseHoriz > 0.45f && auContactEau;
			p.GlobalPosition = new Vector3(corps.GlobalPosition.X, ySurface, corps.GlobalPosition.Z);
			p.AmountRatio = actif ? Mathf.Clamp((vitesseHoriz - 0.45f) / 3.8f, 0.08f, 0.72f) : 0f;
			p.Emitting = actif;
		}

		for (int i = 0; i < _tmpRemousASupprimer.Count; i++)
			RetirerEffetRemousSuivi(_tmpRemousASupprimer[i]);
	}

	/// <summary>Vérité gameplay : le corps est dans l'eau uniquement si ses voxels de contact détectent l'eau.</summary>
	private bool CorpsAuContactEauVoxel(Node3D corps)
	{
		if (corps == null || !GodotObject.IsInstanceValid(corps)) return false;
		Vector3 pos = corps.GlobalPosition;
		// Échantillons pieds + centre bas pour éviter les faux positifs en grottes sèches sous le niveau de mer.
		return EstPointDansEau(pos + new Vector3(0f, -0.95f, 0f))
			|| EstPointDansEau(pos + new Vector3(0f, -0.55f, 0f))
			|| EstPointDansEau(pos + new Vector3(0f, -0.15f, 0f));
	}

	private StandardMaterial3D ObtenirMaterielEclaboussureEau()
	{
		if (_materielEclaboussureEau != null) return _materielEclaboussureEau;
		_materielEclaboussureEau = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = new Color(0.82f, 0.93f, 1f, 0.82f),
			NoDepthTest = false,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
		};
		return _materielEclaboussureEau;
	}

	private void AssurerConteneurEffetsEau()
	{
		if (_conteneurEffetsEau != null && GodotObject.IsInstanceValid(_conteneurEffetsEau)) return;
		_conteneurEffetsEau = new Node3D { Name = "Effets_Eau" };
		AddChild(_conteneurEffetsEau);
	}

	private void CreerEclaboussureSurface(Vector3 centre, float intensite)
	{
		AssurerConteneurEffetsEau();
		if (_conteneurEffetsEau == null || !GodotObject.IsInstanceValid(_conteneurEffetsEau)) return;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(Engine.GetPhysicsFrames() * 73856093u + (uint)Mathf.Abs((int)centre.X * 19349663) + (uint)Mathf.Abs((int)centre.Z * 83492791));
		int nbGouttes = Mathf.Clamp(Mathf.RoundToInt(10 + 18 * intensite), 10, 34);
		Material mat = ObtenirMaterielEclaboussureEau();

		for (int i = 0; i < nbGouttes; i++)
		{
			float angle = rng.RandfRange(0f, Mathf.Tau);
			float rayon = rng.RandfRange(0.06f, 0.18f + 0.22f * intensite);
			float montee = rng.RandfRange(0.08f, 0.32f + 0.25f * intensite);
			float dureeMontee = rng.RandfRange(0.10f, 0.18f);
			float dureeDescente = rng.RandfRange(0.12f, 0.24f);
			float taille = rng.RandfRange(0.028f, 0.05f + 0.03f * intensite);

			var goutte = new MeshInstance3D
			{
				Mesh = new QuadMesh { Size = new Vector2(taille, taille) },
				MaterialOverride = mat
			};
			_conteneurEffetsEau.AddChild(goutte);
			goutte.GlobalPosition = centre + new Vector3(rng.RandfRange(-0.04f, 0.04f), 0f, rng.RandfRange(-0.04f, 0.04f));

			Vector3 cibleMontee = centre + new Vector3(Mathf.Cos(angle) * rayon * 0.55f, montee, Mathf.Sin(angle) * rayon * 0.55f);
			Vector3 cibleDescente = centre + new Vector3(Mathf.Cos(angle) * rayon, rng.RandfRange(0.0f, 0.03f), Mathf.Sin(angle) * rayon);

			// Tween rattaché à la goutte : évite tweens orphelins sous Gestionnaire_Monde si la scène change avant la fin.
			var tw = goutte.CreateTween();
			tw.TweenProperty(goutte, "global_position", cibleMontee, dureeMontee).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(goutte, "global_position", cibleDescente, dureeDescente).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.Parallel().TweenProperty(goutte, "scale", Vector3.Zero, dureeMontee + dureeDescente).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(goutte))
					goutte.QueueFree();
			};
		}
	}
}
