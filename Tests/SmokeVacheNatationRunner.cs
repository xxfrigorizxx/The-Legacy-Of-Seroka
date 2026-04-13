using Godot;
using System.IO;
using System.Text;

/// <summary>
/// Smoke test headless : vache en « eau » (drapeau <see cref="BoeufSauvage.ModeSmokeTestForcerDetectionEau"/>),
/// sol sous les pattes, quelques secondes de physique puis rapport dans <c>artifacts/smoke_vache_natation.log</c>.
/// Lancement : <c>godot --path . res://Tests/SmokeVacheNatation.tscn --headless --quit-after 10</c> (ajuster le délai si besoin) ou sans quit-after et la scène quitte via code.
/// </summary>
public partial class SmokeVacheNatationRunner : Node3D
{
	private const int MaxFramesPhysique = 240;
	private int _compteurFrames;
	private bool _termine;
	private VacheSauvage _vache;
	private Gestionnaire_Monde _gestionnaireHorsArbre;
	private CharacterBody3D _joueurDummy;

	public override void _Ready()
	{
		BoeufSauvage.ModeSmokeTestForcerDetectionEau = true;

		var sol = new StaticBody3D { Name = "SolTestSmoke" };
		var forme = new CollisionShape3D();
		forme.Shape = new BoxShape3D { Size = new Vector3(160f, 2f, 160f) };
		sol.AddChild(forme);
		sol.Position = new Vector3(0f, 98f, 0f);
		AddChild(sol);

		_joueurDummy = new CharacterBody3D { Name = "JoueurDummySmoke" };
		var capsule = new CollisionShape3D();
		capsule.Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.6f };
		_joueurDummy.AddChild(capsule);
		_joueurDummy.Position = new Vector3(40f, 101f, 0f);
		AddChild(_joueurDummy);

		_gestionnaireHorsArbre = new Gestionnaire_Monde();

		PackedScene scene = GD.Load<PackedScene>("res://Scenes/Faune/VacheSauvage.tscn");
		_vache = scene.Instantiate<VacheSauvage>();
		_vache.ActiverReproductionFaune = false;
		_vache.ActiverIATerrainAdaptative = false;
		_vache.ActiverEvolutionEnvironnementale = false;
		_vache.UtiliserConeVisionJoueur = false;
		AddChild(_vache);
		_vache.GlobalPosition = new Vector3(0f, 100.5f, 0f);
		_vache.Configurer(_gestionnaireHorsArbre, _joueurDummy, 4242, Vector3.Zero);

		GD.Print("SmokeVacheNatation: démarrage (ModeSmokeTestForcerDetectionEau=true)");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_termine)
			return;
		if (_vache == null || !GodotObject.IsInstanceValid(_vache))
		{
			Terminer(false, "instance vache invalide");
			return;
		}

		_compteurFrames++;
		if (_compteurFrames < MaxFramesPhysique)
			return;

		bool dansEau = _vache.NatationEauDetectee;
		float vy = _vache.Velocity.Y;
		float vhz = new Vector2(_vache.Velocity.X, _vache.Velocity.Z).Length();
		string clip = _vache.DiagnosticAnimationLocomotionCourante;
		bool physiqueOk = dansEau && Mathf.Abs(vy) < 35f && !float.IsNaN(vy) && !float.IsInfinity(vy);
		string detail = dansEau ? $"clip={clip} vy={vy:F3} vhz={vhz:F3}" : "natation non détectée";
		Terminer(physiqueOk, detail);
	}

	private void Terminer(bool succes, string detail)
	{
		if (_termine)
			return;
		_termine = true;
		SetPhysicsProcess(false);

		var sb = new StringBuilder();
		sb.AppendLine($"frames_physique={_compteurFrames}");
		sb.AppendLine($"detail={detail}");
		if (_vache != null && GodotObject.IsInstanceValid(_vache))
		{
			sb.AppendLine($"natation_eau_detectee={(_vache.NatationEauDetectee ? "oui" : "non")}");
			sb.AppendLine($"velocity_y={_vache.Velocity.Y:F4}");
			sb.AppendLine($"velocity_hz={new Vector2(_vache.Velocity.X, _vache.Velocity.Z).Length():F4}");
			sb.AppendLine($"clip_courant={_vache.DiagnosticAnimationLocomotionCourante}");
		}
		sb.AppendLine($"SMOKE_VACHE_NATATION_RESULT={(succes ? "OK" : "ECHEC")}");

		string texte = sb.ToString();
		GD.Print(texte);

		string resGlobal = ProjectSettings.GlobalizePath("res://");
		if (!string.IsNullOrEmpty(resGlobal))
		{
			string racine = resGlobal.TrimEnd('/', '\\');
			string art = Path.Combine(racine, "artifacts");
			Directory.CreateDirectory(art);
			File.WriteAllText(Path.Combine(art, "smoke_vache_natation.log"), texte);
		}

		BoeufSauvage.ModeSmokeTestForcerDetectionEau = false;
		if (_gestionnaireHorsArbre != null && GodotObject.IsInstanceValid(_gestionnaireHorsArbre))
		{
			_gestionnaireHorsArbre.Free();
			_gestionnaireHorsArbre = null;
		}

		if (GetTree() != null)
			GetTree().Quit(succes ? 0 : 1);
	}
}
