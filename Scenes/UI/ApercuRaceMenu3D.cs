using Godot;

/// <summary>Aperçu 3D tournant pour choisir la race et le sexe dans le menu (assistant création).</summary>
public partial class ApercuRaceMenu3D : Node3D
{
	[Export] public float VitesseRotationYRad { get; set; } = 0.65f;

	private Node3D _pivot;
	private Node3D _rigCourant;
	private AnimationPlayer _animationPlayerCourant;

	public override void _Ready()
	{
		_pivot = GetNodeOrNull<Node3D>("Pivot");
		if (_pivot == null)
		{
			_pivot = new Node3D { Name = "Pivot" };
			AddChild(_pivot);
		}
	}

	public override void _Process(double delta)
	{
		if (_pivot != null && GodotObject.IsInstanceValid(_pivot))
			_pivot.RotateY(VitesseRotationYRad * (float)delta);
	}

	/// <summary>Charge le GLB selon race et sexe sous le pivot (même échelle que <see cref="Joueur"/>).</summary>
	public void DefinirRaceEtSexe(RaceJoueur race, SexeJoueur sexe)
	{
		if (_pivot == null) return;
		if (_rigCourant != null && GodotObject.IsInstanceValid(_rigCourant))
		{
			_rigCourant.QueueFree();
			_rigCourant = null;
		}
		_animationPlayerCourant = null;

		string chemin = Joueur.ObtenirCheminGlbCorpsJoueur(race, sexe);
		var sc = GD.Load<PackedScene>(chemin);
		if (sc == null)
		{
			GD.PrintErr($"ZERO-K : Aperçu menu — scène introuvable : {chemin}");
			return;
		}

		_rigCourant = sc.Instantiate<Node3D>();
		_pivot.AddChild(_rigCourant);
		Joueur.AppliquerEchelleRigSelonRace(_rigCourant, race);
		Vector3 man = Vector3.Zero;
		_rigCourant.RotationDegrees = new Vector3(man.X, Joueur.YawRigMixamoVersGodotDeg + man.Y, man.Z);

		_animationPlayerCourant = TrouverPremierAnimationPlayer(_rigCourant);
		EssayerJouerIdle(_animationPlayerCourant);
	}

	private static AnimationPlayer TrouverPremierAnimationPlayer(Node racine)
	{
		if (racine is AnimationPlayer ap) return ap;
		foreach (Node c in racine.GetChildren())
		{
			AnimationPlayer r = TrouverPremierAnimationPlayer(c);
			if (r != null) return r;
		}
		return null;
	}

	private static void EssayerJouerIdle(AnimationPlayer ap)
	{
		if (ap == null) return;
		string[] noms = ap.GetAnimationList();
		if (noms == null || noms.Length == 0) return;
		string choix = noms[0];
		for (int i = 0; i < noms.Length; i++)
		{
			string l = noms[i].ToLowerInvariant();
			if (l.Contains("idle") || l.Contains("attente") || l.Contains("stand") || l.Contains("tpose") || l.Contains("t-pose"))
			{
				choix = noms[i];
				break;
			}
		}
		ap.Play(choix);
	}
}
