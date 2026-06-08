using Godot;
using System.Collections.Generic;

public partial class ItemPhysique
{
	private const float FourTorchieRayonDegatsExplosionM = 2.75f;
	private const int FourTorchieDegatsExplosionProches = 28;
	private const int FourTorchieDegatsExplosionLointains = 12;
	private const float FourTorchieRayonCratereTerrainM = 2.1f;
	private const float FourTorchieRayonFloreExplosionM = 2.9f;
	private const float FourTorchieRayonArbresExplosionM = 3.25f;
	private const float FourTorchieRayonMeublesExplosionM = 2.9f;

	private void ExecuterExplosionFourTorchie()
	{
		if (ID_Objet != Joueur.IdObjetFourTorchie)
			return;

		Vector3 centre = GlobalPosition + Vector3.Up * (Joueur.TailleFourTorchiePoseMetres * 0.42f);
		AppliquerDestructionEnvironnementExplosionFourTorchie(centre);
		InfligerDegatsExplosionFourTorchieAuJoueur(centre);
		SpawnerEffetExplosionFourTorchie(centre);
		DetruireContenuFourTorchieExplosion();

		GD.Print("SEROKA : Le four en torchie a explosé (anthracite / surchauffe).");
		_fourTorchieAnomalieAnthracite = false;
		_fourTorchieAllume = false;
		MettreAJourVisuelFourTorchie(false);

		Joueur joueur = ObtenirJoueurMonde();
		joueur?.SauvegarderEtatPersistantMonde(GetTree());

		QueueFree();
	}

	private void InfligerDegatsExplosionFourTorchieAuJoueur(Vector3 centreExplosion)
	{
		Joueur joueur = ObtenirJoueurMonde();
		if (joueur == null)
			return;

		float dist = joueur.GlobalPosition.DistanceTo(centreExplosion);
		if (dist > FourTorchieRayonDegatsExplosionM)
			return;

		float ratio = 1f - Mathf.Clamp(dist / FourTorchieRayonDegatsExplosionM, 0f, 1f);
		int degats = Mathf.RoundToInt(Mathf.Lerp(FourTorchieDegatsExplosionLointains, FourTorchieDegatsExplosionProches, ratio));
		joueur.AppliquerDegatsSectionCorps("torse", degats, affecterOs: true);
		joueur.AppliquerDegatsSectionCorps("tete", Mathf.Max(4, degats / 3), affecterOs: false);
		GD.Print($"SEROKA : Explosion du four — vous êtes trop près ({dist:F1} m, -{degats} PV torse).");
	}

	private void AppliquerDestructionEnvironnementExplosionFourTorchie(Vector3 centre)
	{
		Gestionnaire_Monde gm = ObtenirGestionnaireMonde();
		if (gm != null)
		{
			gm.AppliquerDestructionGlobale(centre, FourTorchieRayonCratereTerrainM, forceDegats: 18f);
			gm.AppliquerFauchageGlobal(centre, FourTorchieRayonFloreExplosionM);
		}

		SceneTree arbre = GetTree();
		if (arbre == null)
			return;

		float rayonArbres2 = FourTorchieRayonArbresExplosionM * FourTorchieRayonArbresExplosionM;
		Godot.Collections.Array<Node> arbres = arbre.GetNodesInGroup("Arbres");
		for (int i = 0; i < arbres.Count; i++)
		{
			if (arbres[i] is not ArbreVivant av || !GodotObject.IsInstanceValid(av))
				continue;
			if (av.GlobalPosition.DistanceSquaredTo(centre) > rayonArbres2)
				continue;

			Vector3 dir = av.GlobalPosition - centre;
			if (dir.LengthSquared() < 0.01f)
				dir = Vector3.Up;
			else
				dir = dir.Normalized();
			Vector3 pointImpact = av.GlobalPosition + Vector3.Up * 0.6f;

			for (int coup = 0; coup < 14; coup++)
			{
				int resultat = av.SubirDegats(pointImpact, dir, 900f, 0.02f, hachettePrimitive106: true);
				if (resultat == 2)
					break;
			}
		}

		DetruireMeublesEtObjetsPosesExplosionFourTorchie(centre);
	}

	private void DetruireMeublesEtObjetsPosesExplosionFourTorchie(Vector3 centre)
	{
		SceneTree arbre = GetTree();
		if (arbre == null)
			return;

		float rayon2 = FourTorchieRayonMeublesExplosionM * FourTorchieRayonMeublesExplosionM;
		var aDetruire = new List<Node>();
		Godot.Collections.Array<Node> poses = arbre.GetNodesInGroup("BlocsPoses");
		for (int i = 0; i < poses.Count; i++)
		{
			Node noeud = poses[i];
			if (!GodotObject.IsInstanceValid(noeud))
				continue;

			ItemPhysique item = noeud as ItemPhysique
				?? noeud.GetNodeOrNull<ItemPhysique>("ItemPhysique");
			if (item == null || !GodotObject.IsInstanceValid(item))
				continue;
			if (item == this)
				continue;

			if (item.GlobalPosition.DistanceSquaredTo(centre) > rayon2)
				continue;

			Node racine = noeud.IsInGroup("BlocsPoses") ? noeud : item;
			if (!aDetruire.Contains(racine))
				aDetruire.Add(racine);
		}

		for (int i = 0; i < aDetruire.Count; i++)
		{
			Node n = aDetruire[i];
			if (GodotObject.IsInstanceValid(n))
				n.QueueFree();
		}
	}

	private void DetruireContenuFourTorchieExplosion()
	{
		AssurerGrilleFourTorchie();
		for (int i = 0; i < FourTorchieNbSlots; i++)
			GrilleFourTorchie[i] = new SlotInventaire();
		for (int i = 0; i < FourTorchieNbCuisson; i++)
			_fourTorchieProgressCuissonSec[i] = 0d;
		_fourTorchieResteCombSec = 0d;
		_fourTorchieProfilActifValide = false;
		_fourTorchieTemperature = FourTorchieThermodynamique.TempAmbianteC;
	}

	private static void SpawnerEffetExplosionFourTorchie(Vector3 centreMonde)
	{
		SceneTree arbre = Engine.GetMainLoop() as SceneTree;
		Node parent = arbre?.CurrentScene;
		if (parent == null)
			return;

		var racine = new Node3D { Name = "FourTorchieExplosionEffet" };
		parent.AddChild(racine);
		racine.GlobalPosition = centreMonde;

		var flash = new OmniLight3D
		{
			LightColor = new Color(1f, 0.55f, 0.18f),
			LightEnergy = 6f,
			OmniRange = 14f,
			ShadowEnabled = false
		};
		racine.AddChild(flash);

		var particules = new GpuParticles3D
		{
			Amount = 120,
			OneShot = true,
			Explosiveness = 0.92f,
			Lifetime = 1.35f,
			Emitting = true
		};
		var mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 10, Rings = 8 };
		mesh.Material = new StandardMaterial3D
		{
			AlbedoColor = new Color(1f, 0.42f, 0.08f, 0.85f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.35f, 0.05f),
			EmissionEnergyMultiplier = 2.4f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		particules.DrawPass1 = mesh;
		particules.ProcessMaterial = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, 1f, 0f),
			Spread = 180f,
			InitialVelocityMin = 2.2f,
			InitialVelocityMax = 6.8f,
			Gravity = new Vector3(0f, -5.5f, 0f),
			ScaleMin = 0.35f,
			ScaleMax = 1.6f
		};
		racine.AddChild(particules);

		var fumee = new GpuParticles3D
		{
			Amount = 64,
			OneShot = true,
			Explosiveness = 0.55f,
			Lifetime = 3.8f,
			Emitting = true,
			Position = new Vector3(0f, 0.6f, 0f)
		};
		var meshFumee = new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 8, Rings = 6 };
		meshFumee.Material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.35f, 0.35f, 0.35f, 0.55f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		fumee.DrawPass1 = meshFumee;
		fumee.ProcessMaterial = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, 1f, 0f),
			Spread = 42f,
			InitialVelocityMin = 0.6f,
			InitialVelocityMax = 2.4f,
			Gravity = new Vector3(0f, 0.8f, 0f),
			ScaleMin = 0.8f,
			ScaleMax = 2.8f
		};
		racine.AddChild(fumee);

		var timer = racine.GetTree().CreateTimer(4.5);
		timer.Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(racine))
				racine.QueueFree();
		};
	}
}
