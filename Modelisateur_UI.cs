using Godot;
using System;
using System.Collections.Generic;

/// <summary>Espace CAO : clic gauche saisir/poser ; clic droit +15° Y ; molette ±15° X, Maj+molette ±15° Z ; clic droit maintenu + souris : pivot fin (X/Y).</summary>
public partial class Modelisateur_UI : CanvasLayer
{
	public bool EstOuvert { get; private set; }

	/// <summary>Le HUD CAO utilise le clavier (nom du brevet) — le joueur ne doit pas intercepter Q.</summary>
	public bool SaisieTexteEnCours => _inputNomInvention != null && _inputNomInvention.HasFocus();

	private SubViewport _viewportCAO;
	private Node3D _etabliSpatial;
	private Camera3D _cameraCAO;
	private Joueur _joueurRef;

	private SubViewportContainer _containerViewport;
	private HBoxContainer _hboxPiecesEtabli;
	private Panel _zoneDepotHaut;
	private Panel _panelMainG;
	private Panel _panelMainD;
	private MeshInstance3D _meshApercuG;
	private MeshInstance3D _meshApercuD;
	private Label _lblTransit;

	/// <summary>Corps 3D (StaticBody3D ou héritage) suivi par la souris sur le plan de travail.</summary>
	private Node3D _objetEnMainCAO;
	/// <summary>Plan de pose des pièces : normale alignée sur la vue caméra, passe par l’établi (mis à jour à l’orbite).</summary>
	private Plane _planDeTravail = new Plane(new Vector3(0, 0, 1), 0f);

	private float _camOrbiteY;
	private float _camOrbiteX;
	private float _camDistance = 2.5f;

	private const float LongueurRayonCAO = 100f;
	/// <summary>Arcball : amortissement de l’angle entre deux positions sur la sphère virtuelle.</summary>
	private const float SensibiliteArcballCAO = 0.92f;

	private Vector2 _arcballSourisPrecedentCAO;
	/// <summary>Demi-côté max du plan de travail (évite que la pièce parte à l’infini si le rayon est presque parallèle au plan).</summary>
	private const float RayonMaxDeplacementPlanCAO = 2.8f;
	/// <summary>Fibres / corde : plan de travail plus serré (comme la pose « près du corps » en monde).</summary>
	private const float RayonMaxPlanCAOFlexible = 1.15f;

	/// <summary>Objet tenu « en main » dans l’UI CAO avant dépôt sur l’établi.</summary>
	private SlotInventaire _transit;
	/// <summary>Pièce sur l’établi en cours de pivot (clic droit maintenu + mouvement souris).</summary>
	private Node3D _cibleRotationEtabli;
	/// <summary>Ligature scindée : deux segments étirés entre ancres et nœud sous le curseur (clic milieu).</summary>
	private StaticBody3D _tissuSegmentA;
	private StaticBody3D _tissuSegmentB;
	private Vector3 _tissuAncrageA;
	private Vector3 _tissuAncrageB;
	private Vector3 _tissuNoeudCentral;

	// --- Forge / brevet d'assemblage ---
	private Button _btnForger;
	private Panel _panelBrevet;
	private LineEdit _inputNomInvention;
	private string _genomeEnAttente = "";

	/// <summary>Registre local des assemblages reconnus (Phase 3.4) : génome → nom affiché.</summary>
	public static Dictionary<string, string> BaseDeDonneesBrevets = new Dictionary<string, string>();

	private static bool EstObjetAvecVisuelCAO(int id) =>
		id == 10 || id == 11 || id == 12 || id == 15 || id == 16 || id == 17 || id == 20 || id == 30 || id == 32 || id == 34 || id == 100;

	private bool EnTransit => !_transit.EstVide;
	private bool PieceSurCurseur => _objetEnMainCAO != null && GodotObject.IsInstanceValid(_objetEnMainCAO);

	public void Initialiser(Joueur joueur)
	{
		_joueurRef = joueur;
		if (_panelMainG != null)
			RafraichirApercusMains();
	}

	public override void _Ready()
	{
		EstOuvert = false;
		_transit = default;
		Layer = 80;
		Visible = false;

		// Stop : absorbe les clics sur le « vide » — sinon ils traversent vers le monde (minage, lancer, etc.)
		var fond = new ColorRect { Color = new Color(0.05f, 0.05f, 0.08f, 0.95f), MouseFilter = Control.MouseFilterEnum.Stop };
		fond.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(fond);

		var rootVBox = new VBoxContainer();
		rootVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rootVBox.AddThemeConstantOverride("separation", 10);
		AddChild(rootVBox);

		// --- Zone dépôt : clic DROIT pour poser l’objet « en main » sur l’établi ---
		var topBar = new PanelContainer();
		topBar.CustomMinimumSize = new Vector2(0, 120);
		var topMargin = new MarginContainer();
		topMargin.AddThemeConstantOverride("margin_left", 16);
		topMargin.AddThemeConstantOverride("margin_right", 16);
		topMargin.AddThemeConstantOverride("margin_top", 8);
		topMargin.AddThemeConstantOverride("margin_bottom", 8);
		topBar.AddChild(topMargin);
		var topVBox = new VBoxContainer();
		topMargin.AddChild(topVBox);
		topVBox.AddChild(new Label
		{
			Text = "Fixer sur l’établi : clic GAUCHE sur la vue 3D centrale, ou clic DROIT dans la zone verte — fibres / cordes tressées : déplacement plus près du centre ; échelle naturelle si la matière n’est pas élastique (comme en jeu).",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});
		_zoneDepotHaut = new Panel();
		_zoneDepotHaut.CustomMinimumSize = new Vector2(200, 56);
		_zoneDepotHaut.MouseFilter = Control.MouseFilterEnum.Stop;
		_zoneDepotHaut.TooltipText = "Clic droit : fixe la pièce sur l’établi à la position actuelle du curseur (comme un clic gauche sur la vue)";
		var depotStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.15f, 0.22f, 0.18f, 0.95f),
			BorderColor = new Color(0.35f, 0.65f, 0.45f),
			BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2
		};
		_zoneDepotHaut.AddThemeStyleboxOverride("panel", depotStyle);
		var lblDepot = new Label
		{
			Text = "▼ Zone dépôt (clic droit) ▼",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		lblDepot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_zoneDepotHaut.AddChild(lblDepot);
		_zoneDepotHaut.GuiInput += OnZoneDepotGuiInput;
		topVBox.AddChild(_zoneDepotHaut);

		topVBox.AddChild(new Label
		{
			Text = "Aperçus établi — prise aussi possible par clic gauche sur la vue 3D (raycast)",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(0.85f, 0.85f, 0.9f)
		});
		_hboxPiecesEtabli = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		_hboxPiecesEtabli.AddThemeConstantOverride("separation", 12);
		topVBox.AddChild(_hboxPiecesEtabli);
		rootVBox.AddChild(topBar);

		_lblTransit = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_lblTransit.AddThemeFontSizeOverride("font_size", 14);
		rootVBox.AddChild(_lblTransit);

		_containerViewport = new SubViewportContainer { Stretch = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_containerViewport.TooltipText = "Gauche : saisir / poser · Flèches : orbiter la caméra autour de l’établi · Clic droit : +15° Y · Molette : ±15° X · Maj+molette : ±15° Z · Clic droit maintenu + glisser : arcball · Plan de pose suit la vue";
		rootVBox.AddChild(_containerViewport);

		_viewportCAO = new SubViewport { TransparentBg = true };
		_viewportCAO.World3D = new World3D();
		_containerViewport.AddChild(_viewportCAO);

		_viewportCAO.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, 45, 0) });
		_viewportCAO.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(45, -135, 0), LightEnergy = 0.5f });

		_cameraCAO = new Camera3D { Position = new Vector3(0, 0, 2.5f) };
		_viewportCAO.AddChild(_cameraCAO);

		_etabliSpatial = new Node3D { Name = "Etabli" };
		_viewportCAO.AddChild(_etabliSpatial);

		var repere = new MeshInstance3D
		{
			Name = "RepereOrigine",
			Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.05f) },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 0, 0) }
		};
		_etabliSpatial.AddChild(repere);

		_camOrbiteY = 0f;
		_camOrbiteX = 0f;
		_camDistance = 2.5f;
		MettreAJourCameraOrbiteEtPlanCAO();

		// --- Barre basse : aperçus 3D des mains ---
		var bottomBar = new PanelContainer();
		bottomBar.CustomMinimumSize = new Vector2(0, 150);
		var bottomMargin = new MarginContainer();
		bottomMargin.AddThemeConstantOverride("margin_left", 20);
		bottomMargin.AddThemeConstantOverride("margin_right", 20);
		bottomMargin.AddThemeConstantOverride("margin_top", 10);
		bottomMargin.AddThemeConstantOverride("margin_bottom", 14);
		bottomBar.AddChild(bottomMargin);
		var bottomVBox = new VBoxContainer();
		bottomMargin.AddChild(bottomVBox);
		bottomVBox.AddChild(new Label
		{
			Text = "Mains : clic GAUCHE = prendre en main (CAO) · clic DROIT sur une main vide = rendre depuis la main CAO",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		var hInv = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		hInv.AddThemeConstantOverride("separation", 20);
		hInv.AddChild(new Control { CustomMinimumSize = new Vector2(24, 1) });

		_panelMainG = CreerPanelSlotMain(true, out _meshApercuG);
		hInv.AddChild(_panelMainG);

		_panelMainD = CreerPanelSlotMain(false, out _meshApercuD);
		hInv.AddChild(_panelMainD);

		var spacerMilieu = new Control { CustomMinimumSize = new Vector2(32, 1), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		hInv.AddChild(spacerMilieu);

		for (int i = 0; i < 4; i++)
		{
			var sacSlot = new Panel();
			sacSlot.CustomMinimumSize = new Vector2(56, 56);
			sacSlot.TooltipText = "Sac / inventaire étendu (à venir)";
			sacSlot.Modulate = new Color(0.45f, 0.45f, 0.5f, 0.85f);
			var st = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.12f, 0.16f, 0.9f),
				BorderColor = new Color(0.35f, 0.35f, 0.4f),
				BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2
			};
			sacSlot.AddThemeStyleboxOverride("panel", st);
			hInv.AddChild(sacSlot);
		}

		hInv.AddChild(new Control { CustomMinimumSize = new Vector2(24, 1) });
		bottomVBox.AddChild(hInv);

		var hboxForge = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		hboxForge.AddThemeConstantOverride("separation", 12);
		_btnForger = new Button { Text = "FORGER L'ASSEMBLAGE", CustomMinimumSize = new Vector2(300, 60) };
		_btnForger.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0));
		_btnForger.Pressed += ExecuterForge;
		hboxForge.AddChild(_btnForger);
		bottomVBox.AddChild(hboxForge);

		rootVBox.AddChild(bottomBar);

		// --- Popup de baptême (invention inconnue) ---
		_panelBrevet = new Panel { Visible = false };
		_panelBrevet.SetAnchorsPreset(Control.LayoutPreset.Center);
		_panelBrevet.CustomMinimumSize = new Vector2(400, 200);
		_panelBrevet.OffsetLeft = -200f;
		_panelBrevet.OffsetTop = -100f;
		_panelBrevet.OffsetRight = 200f;
		_panelBrevet.OffsetBottom = 100f;
		_panelBrevet.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(_panelBrevet);

		var vboxBrevet = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vboxBrevet.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 20);
		_panelBrevet.AddChild(vboxBrevet);

		vboxBrevet.AddChild(new Label
		{
			Text = "INVENTION INCONNUE\nEntrez le nom de cet outil :",
			HorizontalAlignment = HorizontalAlignment.Center
		});

		_inputNomInvention = new LineEdit { PlaceholderText = "Ex: Hache de Frigorizz" };
		vboxBrevet.AddChild(_inputNomInvention);

		var btnValiderNom = new Button { Text = "GRAVER DANS LA MATRICE" };
		btnValiderNom.Pressed += ValiderBrevet;
		vboxBrevet.AddChild(btnValiderNom);

		MettreAJourLabelTransit();
	}

	private Panel CreerPanelSlotMain(bool mainGauche, out MeshInstance3D meshPreview)
	{
		var panel = new Panel();
		panel.CustomMinimumSize = new Vector2(96, 96);
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		var pStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.14f, 0.18f, 1f),
			BorderColor = new Color(0.4f, 0.4f, 0.48f),
			BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2
		};
		panel.AddThemeStyleboxOverride("panel", pStyle);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 6);
		margin.AddThemeConstantOverride("margin_right", 6);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		panel.AddChild(margin);

		var vpc = new SubViewportContainer { Stretch = true };
		vpc.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddChild(vpc);

		var vp = new SubViewport();
		vp.Size = new Vector2I(80, 80);
		vp.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible;
		vp.World3D = new World3D(); // Isolation entre les deux aperçus mains (CAO)
		vpc.AddChild(vp);

		var cam = new Camera3D();
		cam.SetOrthogonal(0.55f, 0.01f, 10f);
		cam.Position = new Vector3(0, 0, 1.2f);
		vp.AddChild(cam);

		var meshNode = new MeshInstance3D();
		vp.AddChild(meshNode);
		meshPreview = meshNode;

		var light = new DirectionalLight3D { RotationDegrees = new Vector3(-45, 30, 0) };
		light.Set("sky_mode", 1); // LightOnly (comme Joueur) — pas de disque dans le ciel du SubViewport
		vp.AddChild(light);

		bool g = mainGauche;
		panel.GuiInput += e => OnSlotMainGuiInput(e, g);

		return panel;
	}

	private void OnSlotMainGuiInput(InputEvent e, bool mainGauche)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed) return;
		if (mb.ButtonIndex == MouseButton.Left)
			PrendreDepuisMain(mainGauche);
		else if (mb.ButtonIndex == MouseButton.Right)
			RemettreTransitVersMain(mainGauche);
	}

	private void OnZoneDepotGuiInput(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Right) return;
		DeposerTransitSurEtabli();
	}

	public override void _Input(InputEvent @event)
	{
		if (!EstOuvert) return;

		// Saisie du nom de brevet : ne pas laisser la vue 3D / E voler clavier et souris.
		if (SaisieTexteEnCours)
			return;

		Vector2 mouseEcran = GetViewport().GetMousePosition();
		bool surVueCAO = _containerViewport != null && _containerViewport.GetGlobalRect().HasPoint(mouseEcran);
		bool surZoneDepot = _zoneDepotHaut != null && _zoneDepotHaut.GetGlobalRect().HasPoint(mouseEcran);

		// Fin du pivot établi (clic droit relâché)
		if (@event is InputEventMouseButton mbFin && mbFin.ButtonIndex == MouseButton.Right && !mbFin.Pressed)
		{
			_cibleRotationEtabli = null;
			RafraichirBandeauEtabli();
			return;
		}

		if (@event is InputEventMouseButton mbMid && mbMid.ButtonIndex == MouseButton.Middle)
		{
			if (!mbMid.Pressed)
			{
				_tissuSegmentA = null;
				_tissuSegmentB = null;
				GetViewport().SetInputAsHandled();
				return;
			}
			if (surVueCAO && !surZoneDepot && _viewportCAO?.World3D != null && _cameraCAO != null)
			{
				Vector2 vpPos = CoordonneesSourisViewportCAO(mbMid);
				GodotObject hit = IntersectionRaycastVue(vpPos);
				Node3D porteur = hit != null ? ResoudrePorteurDepuisCollider(hit) : null;
				if (porteur != null && porteur.GetParent() == _etabliSpatial
					&& porteur.HasMeta("EstLigature") && porteur.GetMeta("EstLigature").AsBool())
				{
					int idMat = (int)porteur.GetMeta("ID").AsInt32();
					float baseHeight = idMat == 17 ? 0.38f : (idMat == 15 || idMat == 16 ? 0.34f : 0.28f);

					Vector3 up = porteur.GlobalTransform.Basis.Y.Normalized();
					float length = porteur.Scale.Y * baseHeight;
					_tissuAncrageA = porteur.GlobalPosition + up * (length * 0.5f);
					_tissuAncrageB = porteur.GlobalPosition - up * (length * 0.5f);

					Vector3 rayOrig = _cameraCAO.ProjectRayOrigin(vpPos);
					Vector3 rayDir = _cameraCAO.ProjectRayNormal(vpPos);
					var query = PhysicsRayQueryParameters3D.Create(rayOrig, rayOrig + rayDir * 100f);
					var res = _viewportCAO.World3D.DirectSpaceState.IntersectRay(query);
					if (res != null && res.Count > 0 && res.ContainsKey("position"))
						_tissuNoeudCentral = res["position"].AsVector3();
					else
						_tissuNoeudCentral = porteur.GlobalPosition;

					SlotInventaire slot = LireSlotDepuisNoeudPorteur(porteur);
					_tissuSegmentA = FabriquerCorpsCAO(slot);
					_tissuSegmentB = FabriquerCorpsCAO(slot);
					if (_tissuSegmentA == null || _tissuSegmentB == null)
					{
						_tissuSegmentA?.QueueFree();
						_tissuSegmentB?.QueueFree();
						_tissuSegmentA = null;
						_tissuSegmentB = null;
						GetViewport().SetInputAsHandled();
						return;
					}
					_tissuSegmentA.SetMeta("EstLigature", true);
					_tissuSegmentB.SetMeta("EstLigature", true);

					_etabliSpatial.AddChild(_tissuSegmentA);
					_etabliSpatial.AddChild(_tissuSegmentB);
					porteur.QueueFree();

					GD.Print("ZERO-K : Fibre pincée. Bougez la souris pour créer une ondulation (Maj = Hauteur).");
				}
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseMotion mm)
		{
			if (!surVueCAO || surZoneDepot)
				return;

			bool clicDroitMaintenu = Input.IsMouseButtonPressed(MouseButton.Right);

			// Clic milieu : déformation de la corde / fibre scindée (ondulations, U, coude en hauteur avec Shift)
			if (_tissuSegmentA != null && _tissuSegmentB != null
				&& GodotObject.IsInstanceValid(_tissuSegmentA) && GodotObject.IsInstanceValid(_tissuSegmentB)
				&& Input.IsMouseButtonPressed(MouseButton.Middle) && _cameraCAO != null)
			{
				float dx = mm.Relative.X * 0.01f;
				float dy = mm.Relative.Y * 0.01f;

				if (Input.IsKeyPressed(Key.Shift))
					_tissuNoeudCentral += -dy * _cameraCAO.GlobalTransform.Basis.Y.Normalized();
				else
				{
					_tissuNoeudCentral += dx * _cameraCAO.GlobalTransform.Basis.X.Normalized()
						+ dy * _cameraCAO.GlobalTransform.Basis.Z.Normalized();
				}

				void TendreSegment(Node3D segment, Vector3 p1, Vector3 p2)
				{
					Vector3 offset = p2 - p1;
					float dist = offset.Length();
					if (dist < 0.01f) return;
					segment.GlobalPosition = p1 + offset * 0.5f;

					Vector3 yAxis = offset.Normalized();
					Vector3 xAxis = Vector3.Up.Cross(yAxis).Normalized();
					if (xAxis.LengthSquared() < 0.01f)
						xAxis = Vector3.Right.Cross(yAxis).Normalized();
					Vector3 zAxis = xAxis.Cross(yAxis).Normalized();
					segment.GlobalTransform = new Transform3D(new Basis(xAxis, yAxis, zAxis), segment.GlobalPosition);

					int idSeg = (int)segment.GetMeta("ID").AsInt32();
					float bH = idSeg == 17 ? 0.38f : (idSeg == 15 || idSeg == 16 ? 0.34f : 0.28f);
					segment.Scale = new Vector3(1f, dist / bH, 1f);
				}

				TendreSegment(_tissuSegmentA, _tissuAncrageA, _tissuNoeudCentral);
				TendreSegment(_tissuSegmentB, _tissuNoeudCentral, _tissuAncrageB);

				GetViewport().SetInputAsHandled();
				return;
			}

			// Pivot : clic droit maintenu + glissement — arcball (toutes orientations atteignables)
			if (clicDroitMaintenu)
			{
				Vector2 vpCur = CoordonneesSourisViewportCAO(mm);
				if (PieceSurCurseur && EnTransit && _objetEnMainCAO != null)
				{
					AppliquerArcballPivot(_arcballSourisPrecedentCAO, vpCur, _objetEnMainCAO);
					_arcballSourisPrecedentCAO = vpCur;
					GetViewport().SetInputAsHandled();
				}
				else if (_cibleRotationEtabli != null && GodotObject.IsInstanceValid(_cibleRotationEtabli)
					&& _cibleRotationEtabli.GetParent() == _etabliSpatial)
				{
					AppliquerArcballPivot(_arcballSourisPrecedentCAO, vpCur, _cibleRotationEtabli);
					_arcballSourisPrecedentCAO = vpCur;
					GetViewport().SetInputAsHandled();
				}
				return;
			}

			// Déplacement sur le plan seulement si pas en train de pivoter
			if (PieceSurCurseur && _cameraCAO != null && _viewportCAO != null)
			{
				MettreAJourPositionObjetCurseur(CoordonneesSourisViewportCAO(mm));
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
		{
			if (!surVueCAO)
				return;

			Vector2 vpPos = CoordonneesSourisViewportCAO(mouseBtn);

			if (mouseBtn.ButtonIndex == MouseButton.Left)
			{
				_cibleRotationEtabli = null;
				_tissuSegmentA = null;
				_tissuSegmentB = null;
				if (PieceSurCurseur)
					FinaliserPlacementPieceSurEtabli();
				else
					TenterSaisieParRaycast(vpPos);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (!surZoneDepot)
			{
				if (mouseBtn.ButtonIndex == MouseButton.Right)
				{
					if (PieceSurCurseur && EnTransit)
						_cibleRotationEtabli = null;
					else
					{
						GodotObject hit = IntersectionRaycastVue(vpPos);
						Node3D porteur = hit != null ? ResoudrePorteurDepuisCollider(hit) : null;
						_cibleRotationEtabli = porteur != null && porteur.GetParent() == _etabliSpatial ? porteur : null;
					}
					_arcballSourisPrecedentCAO = vpPos;
					AppliquerRotation(1, 15f, vpPos);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
				{
					if (Input.IsKeyPressed(Key.Shift))
						AppliquerRotation(2, 15f, vpPos);
					else
						AppliquerRotation(0, 15f, vpPos);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
				{
					if (Input.IsKeyPressed(Key.Shift))
						AppliquerRotation(2, -15f, vpPos);
					else
						AppliquerRotation(0, -15f, vpPos);
					GetViewport().SetInputAsHandled();
					return;
				}
			}
		}
		else if (@event is InputEvent ie && ie.IsPressed() && !ie.IsEcho())
		{
			// Même action que le monde (« interagir » = E par défaut) : ligature, pas le tressage T.
			if (ie.IsAction("interagir"))
			{
				ExecuterLigature();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	/// <summary>Projection souris → sphère unité (Y écran vers le haut mathématique).</summary>
	private static Vector3 SphereMapArcballCAO(Vector2 pixel, Vector2I viewportSize)
	{
		float r = 0.48f * Mathf.Min(viewportSize.X, viewportSize.Y);
		if (r < 4f) r = 4f;
		Vector2 c = new Vector2(viewportSize.X * 0.5f, viewportSize.Y * 0.5f);
		float dx = (pixel.X - c.X) / r;
		float dy = -(pixel.Y - c.Y) / r;
		float d2 = dx * dx + dy * dy;
		if (d2 > 1f)
		{
			float inv = 1f / Mathf.Sqrt(d2);
			return new Vector3(dx * inv, dy * inv, 0f);
		}
		return new Vector3(dx, dy, Mathf.Sqrt(1f - d2));
	}

	/// <summary>Rotation entre deux échantillons arcball : sous-groupe dense de SO(3), toutes les orientations accessibles par enchaînement.</summary>
	private void AppliquerArcballPivot(Vector2 pixel0, Vector2 pixel1, Node3D noeud)
	{
		if (noeud == null || !GodotObject.IsInstanceValid(noeud) || _cameraCAO == null || _viewportCAO == null) return;

		Vector2I vs = _viewportCAO.Size;
		Vector3 va = SphereMapArcballCAO(pixel0, vs);
		Vector3 vb = SphereMapArcballCAO(pixel1, vs);
		Vector3 axisCam = va.Cross(vb);
		float axisLen = axisCam.Length();
		if (axisLen < 1e-10f) return;
		axisCam /= axisLen;
		float dot = Mathf.Clamp(va.Dot(vb), -1f, 1f);
		float angle = Mathf.Acos(dot) * SensibiliteArcballCAO;
		if (angle < 1e-6f) return;

		Basis b = _cameraCAO.GlobalTransform.Basis;
		Vector3 axisWorld = b.X * axisCam.X + b.Y * axisCam.Y + (-b.Z) * axisCam.Z;
		if (axisWorld.LengthSquared() < 1e-12f) return;
		noeud.GlobalRotate(axisWorld.Normalized(), angle);
	}

	/// <summary>Rotation par crans (locales) : pièce sous le curseur en main, sinon raycast sur l’établi.</summary>
	/// <param name="axe">0 = X, 1 = Y, 2 = Z.</param>
	/// <param name="mousePosViewport">Position souris en pixels du SubViewport CAO (stretch).</param>
	private void AppliquerRotation(int axe, float angleDegres, Vector2 mousePosViewport)
	{
		void Tourner(Node3D obj)
		{
			if (obj == null || !GodotObject.IsInstanceValid(obj)) return;
			float rad = Mathf.DegToRad(angleDegres);
			if (axe == 0) obj.RotateX(rad);
			else if (axe == 1) obj.RotateY(rad);
			else if (axe == 2) obj.RotateZ(rad);
		}

		if (_objetEnMainCAO != null && GodotObject.IsInstanceValid(_objetEnMainCAO))
		{
			Tourner(_objetEnMainCAO);
			return;
		}

		GodotObject hit = IntersectionRaycastVue(mousePosViewport);
		if (hit == null) return;
		Node3D porteur = ResoudrePorteurDepuisCollider(hit);
		if (porteur != null && porteur.GetParent() == _etabliSpatial)
			Tourner(porteur);
	}

	/// <summary>Convertit la position souris d’un InputEvent en coordonnées pixel du SubViewport CAO (stretch inclus).</summary>
	private Vector2 CoordonneesSourisViewportCAO(InputEventMouse evt)
	{
		if (_containerViewport == null || _viewportCAO == null)
			return evt.Position;
		Vector2 local = _containerViewport.GetGlobalTransformWithCanvas().AffineInverse() * evt.GlobalPosition;
		Vector2 cs = _containerViewport.Size;
		Vector2I vps = _viewportCAO.Size;
		if (cs.X > 1e-4f && cs.Y > 1e-4f)
			return new Vector2(local.X * vps.X / cs.X, local.Y * vps.Y / cs.Y);
		return local;
	}

	private GodotObject IntersectionRaycastVue(Vector2 coordonneesViewport)
	{
		if (_viewportCAO?.World3D == null || _cameraCAO == null) return null;
		Vector3 rayOrigin = _cameraCAO.ProjectRayOrigin(coordonneesViewport);
		Vector3 rayNormal = _cameraCAO.ProjectRayNormal(coordonneesViewport);
		var space = _viewportCAO.World3D.DirectSpaceState;
		Vector3 to = rayOrigin + rayNormal * LongueurRayonCAO;
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, to);
		var dict = space.IntersectRay(query);
		if (dict == null || dict.Count == 0) return null;
		return dict["collider"].AsGodotObject();
	}

	private static Node3D ResoudrePorteurDepuisCollider(GodotObject collider)
	{
		if (collider is CollisionShape3D cs && cs.GetParent() is StaticBody3D sb)
			return sb;
		if (collider is StaticBody3D sb2)
			return sb2;
		return null;
	}

	private void TenterSaisieParRaycast(Vector2 vpPos)
	{
		if (PieceSurCurseur || EnTransit) return;
		GodotObject hit = IntersectionRaycastVue(vpPos);
		if (hit == null) return;
		Node3D porteur = ResoudrePorteurDepuisCollider(hit);
		if (porteur == null || porteur.GetParent() != _etabliSpatial) return;
		if (SaisirPorteurSurEtabli(porteur))
			GD.Print("ZERO-K : Composant saisi.");
	}

	private bool SaisirPorteurSurEtabli(Node3D porteur)
	{
		if (_joueurRef == null || porteur == null || !GodotObject.IsInstanceValid(porteur)) return false;
		if (EnTransit || PieceSurCurseur) return false;

		_cibleRotationEtabli = null;
		_tissuSegmentA = null;
		_tissuSegmentB = null;
		_transit = LireSlotDepuisNoeudPorteur(porteur);
		if (_transit.EstVide) return false;

		porteur.GetParent()?.RemoveChild(porteur);
		_viewportCAO.AddChild(porteur);
		_objetEnMainCAO = porteur;

		MettreAJourPositionObjetCurseur();
		Callable.From(() => MettreAJourPositionObjetCurseur()).CallDeferred();

		MettreAJourLabelTransit();
		RafraichirBandeauEtabli();
		return true;
	}

	private static SlotInventaire LireSlotDepuisNoeudPorteur(Node3D porteur)
	{
		var slot = new SlotInventaire();
		slot.ID = porteur.HasMeta("ID") ? (int)porteur.GetMeta("ID").AsInt32() : 0;
		slot.IndexChimique = porteur.HasMeta("IndexChimique") ? (int)porteur.GetMeta("IndexChimique").AsInt32() : 0;
		slot.IndexMorphologique = porteur.HasMeta("IndexMorphologique") ? (int)porteur.GetMeta("IndexMorphologique").AsInt32() : 0;
		slot.NiveauFracture = porteur.HasMeta("NiveauFracture") ? (int)porteur.GetMeta("NiveauFracture").AsInt32() : 0;
		slot.EstUnEclat = porteur.HasMeta("EstUnEclat") && porteur.GetMeta("EstUnEclat").AsBool();
		slot.ScaleEclat = porteur.HasMeta("ScaleEclat") ? porteur.GetMeta("ScaleEclat").AsVector3() : Vector3.One;
		MeshInstance3D vis = TrouverPremierMeshInstance(porteur);
		slot.MeshEclat = slot.EstUnEclat && vis?.Mesh != null ? (Mesh)vis.Mesh.Duplicate() : null;
		slot.IndexBotanique = porteur.HasMeta("IndexBotanique")
			? (byte)Mathf.Clamp((int)porteur.GetMeta("IndexBotanique").AsInt32(), 0, 255)
			: LSystem_Botanique.IndexChene;
		slot.GenomeAssemblage = porteur.HasMeta(Joueur.MetaGenomeAssemblage)
			? porteur.GetMeta(Joueur.MetaGenomeAssemblage).AsString()
			: "";
		return slot;
	}

	private static MeshInstance3D TrouverPremierMeshInstance(Node racine)
	{
		if (racine is MeshInstance3D m && m.Mesh != null) return m;
		foreach (Node c in racine.GetChildren())
		{
			if (c is MeshInstance3D mm && mm.Mesh != null) return mm;
		}
		return null;
	}

	private void MettreAJourPositionObjetCurseur(Vector2? coordonneesViewport = null)
	{
		if (!PieceSurCurseur || _cameraCAO == null || _viewportCAO == null) return;
		Vector2 vpSouris = coordonneesViewport ?? _viewportCAO.GetMousePosition();
		Vector3 origine = _cameraCAO.ProjectRayOrigin(vpSouris);
		Vector3 direction = _cameraCAO.ProjectRayNormal(vpSouris);
		if (!EssayerIntersectionPlanTravail(origine, direction, out Vector3 intersection))
			return;
		float r = RayonMaxDeplacementPlanCAO;
		if (EnTransit && Joueur.EstFlexibleOuCordePourPlanCAO(_transit.ID))
			r = Mathf.Min(r, RayonMaxPlanCAOFlexible);
		Vector3 centre = _etabliSpatial.GlobalPosition;
		Vector3 n = _planDeTravail.Normal;
		Vector3 offset = intersection - centre;
		offset -= n * n.Dot(offset);
		float len = offset.Length();
		if (len > r)
			offset *= r / len;
		_objetEnMainCAO.GlobalPosition = centre + offset;
	}

	/// <summary>Intersection rayon / plan de travail (équation Godot : Normal·P + D = 0).</summary>
	private bool EssayerIntersectionPlanTravail(Vector3 origine, Vector3 direction, out Vector3 intersection)
	{
		intersection = default;
		float denom = _planDeTravail.Normal.Dot(direction);
		if (Mathf.Abs(denom) < 1e-6f) return false;
		// Godot : Normal·P = D sur le plan
		float t = (_planDeTravail.D - _planDeTravail.Normal.Dot(origine)) / denom;
		if (t < 0f) return false;
		intersection = origine + direction * t;
		return true;
	}

	/// <summary>Corps saisissable : StaticBody3D + visuel + hitbox (raycast DirectSpaceState).</summary>
	private static StaticBody3D FabriquerCorpsCAO(SlotInventaire slot)
	{
		Mesh mesh = slot.EstUnEclat ? slot.MeshEclat : Joueur.ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique);
		if (mesh == null) return null;

		var piece = new StaticBody3D();
		var visuel = new MeshInstance3D();
		visuel.Mesh = mesh;
		Joueur.AppliquerMaterielObjet(visuel, slot.ID, slot.IndexChimique, slot.IndexMorphologique, slot.NiveauFracture);
		piece.AddChild(visuel);

		var hitbox = new CollisionShape3D { Shape = ItemPhysique.CreerShapeCollisionConvexeRobuste(mesh) };
		piece.AddChild(hitbox);

		if (slot.ID == 30 || slot.ID == 32)
		{
			Vector3 s = slot.ScaleEclat.LengthSquared() > 1e-6f ? slot.ScaleEclat : Vector3.One;
			piece.Scale = new Vector3(s.X, s.Z, s.X);
			piece.RotationDegrees = new Vector3(90f, 0f, 0f);
		}
		else
			piece.Scale = Joueur.ObtenirEchellePieceFlexibleCAO(slot);

		piece.SetMeta("ID", slot.ID);
		piece.SetMeta("IndexChimique", slot.IndexChimique);
		piece.SetMeta("IndexMorphologique", slot.IndexMorphologique);
		piece.SetMeta("NiveauFracture", slot.NiveauFracture);
		piece.SetMeta("EstUnEclat", slot.EstUnEclat);
		piece.SetMeta("ScaleEclat", slot.ScaleEclat);
		piece.SetMeta("IndexBotanique", (int)slot.IndexBotanique);
		if (!string.IsNullOrEmpty(slot.GenomeAssemblage))
			piece.SetMeta(Joueur.MetaGenomeAssemblage, slot.GenomeAssemblage);

		int hashMeshMeta = 0;
		if (slot.EstUnEclat && slot.MeshEclat != null)
			hashMeshMeta = slot.ID == 100 && !string.IsNullOrEmpty(slot.GenomeAssemblage)
				? Joueur.HashGenomeStable(slot.GenomeAssemblage)
				: slot.MeshEclat.GetHashCode();
		else if (slot.ID == 100 && !string.IsNullOrEmpty(slot.GenomeAssemblage))
			hashMeshMeta = Joueur.HashGenomeStable(slot.GenomeAssemblage);
		piece.SetMeta("HashMesh", hashMeshMeta);

		return piece;
	}

	private StaticBody3D CreerCorpsStatiqueDepuisTransitSlot() => FabriquerCorpsCAO(_transit);

	private void FinaliserPlacementPieceSurEtabli()
	{
		if (_joueurRef == null || !PieceSurCurseur || !EnTransit) return;

		_cibleRotationEtabli = null;
		_tissuSegmentA = null;
		_tissuSegmentB = null;
		Node3D piece = _objetEnMainCAO;
		int idLog = _transit.ID;
		piece.Reparent(_etabliSpatial);
		_objetEnMainCAO = null;
		_transit = default;

		_joueurRef.RafraichirHUD();
		RafraichirApercusMains();
		RafraichirBandeauEtabli();
		MettreAJourLabelTransit();
		GD.Print($"ZERO-K : Composant fixé sur l'établi (ID {idLog}).");
	}

	/// <summary>Fibres / corde au curseur : interagir (E) fige la matière si elle touche ≥2 pièces (pont intégrité).</summary>
	private void ExecuterLigature()
	{
		if (_objetEnMainCAO == null || !GodotObject.IsInstanceValid(_objetEnMainCAO))
		{
			GD.Print("ZERO-K : Vous devez tenir une matière avec votre curseur pour lier.");
			return;
		}

		if (!_objetEnMainCAO.HasMeta("ID"))
		{
			GD.Print("ZERO-K : Objet au curseur invalide pour la ligature.");
			return;
		}

		int idMatiere = (int)_objetEnMainCAO.GetMeta("ID").AsInt32();
		if (idMatiere != 15 && idMatiere != 16 && idMatiere != 17 && idMatiere != 20)
		{
			GD.Print("ZERO-K : Matière trop rigide. Utilisez Corde/Fibres.");
			return;
		}

		Aabb aabbLien = ObtenirAabbGlobale(_objetEnMainCAO).Grow(0.05f);
		var piecesTouchees = new List<Node3D>();

		foreach (Node e in _etabliSpatial.GetChildren())
		{
			if (e is Node3D piece && piece.HasMeta("ID"))
			{
				if (aabbLien.Intersects(ObtenirAabbGlobale(piece)))
					piecesTouchees.Add(piece);
			}
		}

		if (piecesTouchees.Count >= 2)
		{
			_objetEnMainCAO.SetMeta("EstLigature", true);

			Node3D lien = _objetEnMainCAO;
			lien.GetParent()?.RemoveChild(lien);
			_etabliSpatial.AddChild(lien);

			MeshInstance3D mi = TrouverPremierMeshInstance(lien);
			if (mi != null && mi.MaterialOverride != null && mi.MaterialOverride is StandardMaterial3D sm0)
			{
				var mat = (StandardMaterial3D)sm0.Duplicate();
				mat.AlbedoColor = new Color(0f, 1f, 0f);
				mi.MaterialOverride = mat;
			}

			_objetEnMainCAO = null;
			_transit = default;
			_cibleRotationEtabli = null;
			_tissuSegmentA = null;
			_tissuSegmentB = null;

			_joueurRef?.RafraichirHUD();
			RafraichirApercusMains();
			RafraichirBandeauEtabli();
			MettreAJourLabelTransit();

			GD.Print($"ZERO-K : Nœud serré sur {piecesTouchees.Count} pièces !");
		}
		else
		{
			GD.Print("ZERO-K : ÉCHEC. La corde doit toucher au moins 2 composants posés pour les attacher.");
		}
	}

	private void DetruireObjetCurseurCAOIfAny()
	{
		if (_objetEnMainCAO != null && GodotObject.IsInstanceValid(_objetEnMainCAO))
			_objetEnMainCAO.QueueFree();
		_objetEnMainCAO = null;
	}

	private void MettreAJourLabelTransit()
	{
		if (_lblTransit == null) return;
		if (!EnTransit && !PieceSurCurseur)
		{
			_lblTransit.Text = "Vue 3D : clic gauche = saisir / poser · clic droit maintenu = pivot (arcball) · clic milieu sur une ligature verte = scission + ondulation (Shift = hauteur du coude) · E = ligature stricte. Zone verte : clic droit = poser.";
			return;
		}
		_lblTransit.Text = $"Pièce (curseur) ID {_transit.ID} — Clic gauche : poser. Clic droit : pivoter. Clic milieu sur ligature posée : tisser en U / onduler. E : ligature.";
	}

	public override void _Process(double delta)
	{
		if (!EstOuvert || SaisieTexteEnCours || _cameraCAO == null || _etabliSpatial == null)
			return;

		float dt = (float)delta;
		float inputX = Input.GetAxis("ui_left", "ui_right");
		float inputY = Input.GetAxis("ui_down", "ui_up");
		if (inputX != 0f || inputY != 0f)
		{
			_camOrbiteY += inputX * 2.5f * dt;
			_camOrbiteX += inputY * 2.5f * dt;
			_camOrbiteX = Mathf.Clamp(_camOrbiteX, -Mathf.Pi / 2.2f, Mathf.Pi / 2.2f);
			MettreAJourCameraOrbiteEtPlanCAO();
		}
	}

	/// <summary>Caméra sphérique autour de l’établi + plan de pose aligné sur la vue (flèches : orbite).</summary>
	private void MettreAJourCameraOrbiteEtPlanCAO()
	{
		if (_cameraCAO == null || _etabliSpatial == null) return;
		Vector3 cible = _etabliSpatial.GlobalPosition;
		float x = _camDistance * Mathf.Cos(_camOrbiteX) * Mathf.Sin(_camOrbiteY);
		float y = _camDistance * Mathf.Sin(_camOrbiteX);
		float z = _camDistance * Mathf.Cos(_camOrbiteX) * Mathf.Cos(_camOrbiteY);
		_cameraCAO.GlobalPosition = cible + new Vector3(x, y, z);
		_cameraCAO.LookAt(cible, Vector3.Up);
		Vector3 nn = _cameraCAO.GlobalTransform.Basis.Z.Normalized();
		_planDeTravail = new Plane(nn, nn.Dot(cible));
	}

	public void BasculerVisibilite()
	{
		if (EstOuvert)
			RemettreTransitDansMainsSiFermeture();

		EstOuvert = !EstOuvert;
		Visible = EstOuvert;
		Input.MouseMode = EstOuvert ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;

		_joueurRef?.DefinirVisibiliteObjetMainCamera(!EstOuvert);

		if (EstOuvert)
		{
			_cibleRotationEtabli = null;
			_tissuSegmentA = null;
			_tissuSegmentB = null;
			_camOrbiteY = 0f;
			_camOrbiteX = 0f;
			_camDistance = 2.5f;
			MettreAJourCameraOrbiteEtPlanCAO();
			GD.Print("ZERO-K : Entrée dans l'Espace CAO.");
			RafraichirApercusMains();
			RafraichirBandeauEtabli();
			MettreAJourLabelTransit();
		}
		else
		{
			GD.Print("ZERO-K : Fermeture de l'Espace CAO.");
			if (_panelBrevet != null)
				_panelBrevet.Visible = false;
			if (_inputNomInvention != null)
				_inputNomInvention.Text = "";
			_joueurRef?.RafraichirHUD();
		}
	}

	private void RemettreTransitDansMainsSiFermeture()
	{
		if (_joueurRef == null) return;
		_cibleRotationEtabli = null;
		_tissuSegmentA = null;
		_tissuSegmentB = null;
		DetruireObjetCurseurCAOIfAny();
		if (!EnTransit) return;
		if (_joueurRef.MainGauche.EstVide)
			_joueurRef.MainGauche = _transit;
		else if (_joueurRef.MainDroite.EstVide)
			_joueurRef.MainDroite = _transit;
		else
			GD.PrintErr("ZERO-K : Fermeture CAO avec pièce en main CAO et deux mains pleines — pièce perdue (slot).");
		_transit = default;
		_joueurRef.RafraichirHUD();
	}

	private void RafraichirApercusMains()
	{
		if (_joueurRef == null) return;
		AppliquerApercuSlot(_meshApercuG, _joueurRef.MainGauche);
		AppliquerApercuSlot(_meshApercuD, _joueurRef.MainDroite);
	}

	private static void AppliquerApercuSlot(MeshInstance3D meshNode, SlotInventaire slot)
	{
		if (meshNode == null) return;
		if (slot.EstVide || !EstObjetAvecVisuelCAO(slot.ID))
		{
			meshNode.Mesh = null;
			meshNode.MaterialOverride = null;
			return;
		}
		Mesh m = slot.EstUnEclat ? slot.MeshEclat : Joueur.ObtenirMeshDepuisCache(slot.ID, slot.IndexMorphologique);
		meshNode.Mesh = m;
		if (slot.ID == 30 || slot.ID == 32)
		{
			Vector3 s = slot.ScaleEclat.LengthSquared() > 1e-6f ? slot.ScaleEclat : Vector3.One;
			meshNode.Scale = new Vector3(s.X, s.Z, s.X);
			meshNode.RotationDegrees = new Vector3(68f, 18f, 0);
		}
		else
		{
			meshNode.Scale = Vector3.One;
			meshNode.RotationDegrees = Vector3.Zero;
		}
		if (slot.EstUnEclat)
		{
			if (slot.ID is 10 or 11 or 12)
				Joueur.AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
			else if (slot.ID is 30 or 32)
				Joueur.AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, 0, 0);
			else if (slot.ID == 100)
				Joueur.AppliquerMaterielObjet(meshNode, 100, slot.IndexChimique, 0, 0);
			else
				meshNode.MaterialOverride = null;
		}
		else if (m != null)
			Joueur.AppliquerMaterielObjet(meshNode, slot.ID, slot.IndexChimique, slot.ID == 20 ? slot.IndexMorphologique : 0, slot.ID == 20 ? slot.NiveauFracture : 0);
	}

	/// <summary>Copie le slot pour le transit : mesh éclat dupliqué pour ne pas partager une ressource avec l’aperçu / l’établi.</summary>
	private static SlotInventaire DupliquerSlotPourTransit(SlotInventaire s)
	{
		if (s.EstVide) return default;
		SlotInventaire c = s;
		if (s.EstUnEclat && s.MeshEclat != null)
			c.MeshEclat = (Mesh)s.MeshEclat.Duplicate();
		return c;
	}

	private void PrendreDepuisMain(bool mainGauche)
	{
		if (_joueurRef == null) return;
		if (EnTransit || PieceSurCurseur)
		{
			GD.Print("ZERO-K : Vous tenez déjà une pièce avec le curseur — fixez-la sur l’établi ou rendez-la (clic droit sur une main vide).");
			return;
		}
		SlotInventaire slot = mainGauche ? _joueurRef.MainGauche : _joueurRef.MainDroite;
		if (slot.EstVide) return;
		_transit = DupliquerSlotPourTransit(slot);
		_cibleRotationEtabli = null;
		_tissuSegmentA = null;
		_tissuSegmentB = null;

		_objetEnMainCAO = CreerCorpsStatiqueDepuisTransitSlot();
		if (_objetEnMainCAO == null)
		{
			GD.PrintErr("ZERO-K : Mesh introuvable pour la prise en main CAO.");
			_transit = default;
			return;
		}

		if (mainGauche) _joueurRef.MainGauche = default;
		else _joueurRef.MainDroite = default;
		_viewportCAO.AddChild(_objetEnMainCAO);
		MettreAJourPositionObjetCurseur();
		Callable.From(() => MettreAJourPositionObjetCurseur()).CallDeferred();

		_joueurRef.RafraichirHUD();
		RafraichirApercusMains();
		MettreAJourLabelTransit();
		GD.Print($"ZERO-K : Pièce ID {_transit.ID} extraite — suit le curseur ; clic gauche sur la vue ou clic droit zone verte pour fixer.");
	}

	private void RemettreTransitVersMain(bool mainGauche)
	{
		if (_joueurRef == null || !EnTransit) return;
		SlotInventaire cible = mainGauche ? _joueurRef.MainGauche : _joueurRef.MainDroite;
		if (!cible.EstVide)
		{
			GD.Print("ZERO-K : Cette main est déjà pleine.");
			return;
		}
		DetruireObjetCurseurCAOIfAny();
		if (mainGauche) _joueurRef.MainGauche = _transit;
		else _joueurRef.MainDroite = _transit;
		_transit = default;
		_joueurRef.RafraichirHUD();
		RafraichirApercusMains();
		MettreAJourLabelTransit();
	}

	private void DeposerTransitSurEtabli()
	{
		if (_joueurRef == null || !EnTransit) return;

		if (PieceSurCurseur)
		{
			FinaliserPlacementPieceSurEtabli();
			return;
		}

		SlotInventaire slot = _transit;
		StaticBody3D piece = FabriquerCorpsCAO(slot);
		if (piece == null)
		{
			GD.PrintErr("ZERO-K : Corps CAO introuvable pour dépôt.");
			return;
		}

		_etabliSpatial.AddChild(piece);
		_transit = default;

		_joueurRef.RafraichirHUD();
		RafraichirApercusMains();
		RafraichirBandeauEtabli();
		MettreAJourLabelTransit();
		GD.Print($"ZERO-K : Pièce (ID {slot.ID}) déposée sur l'établi.");
	}

	private void RafraichirBandeauEtabli()
	{
		if (_hboxPiecesEtabli == null || _etabliSpatial == null) return;
		foreach (Node c in _hboxPiecesEtabli.GetChildren())
			c.QueueFree();

		foreach (Node n in _etabliSpatial.GetChildren())
		{
			if (n.Name == "RepereOrigine") continue;
			if (n is StaticBody3D sb)
			{
				_hboxPiecesEtabli.AddChild(CreerTuilePieceEtabli(sb));
				continue;
			}
			if (n is MeshInstance3D miLegacy && miLegacy.Mesh != null)
				_hboxPiecesEtabli.AddChild(CreerTuilePieceEtabli(miLegacy));
		}

		if (_hboxPiecesEtabli.GetChildCount() == 0)
		{
			_hboxPiecesEtabli.AddChild(new Label
			{
				Text = "Aucune pièce — prenez un objet en bas (clic gauche), déplacez-le sur la vue, puis fixez (clic gauche sur la vue ou clic droit zone verte).",
				Modulate = new Color(0.7f, 0.7f, 0.75f)
			});
		}
	}

	private Control CreerTuilePieceEtabli(Node3D porteur)
	{
		MeshInstance3D pieceSource = TrouverPremierMeshInstance(porteur);
		int id = porteur.HasMeta("ID") ? (int)porteur.GetMeta("ID").AsInt32() : 0;
		var panel = new Panel();
		panel.CustomMinimumSize = new Vector2(88, 100);
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.18f, 0.22f),
			BorderColor = new Color(0.5f, 0.5f, 0.55f),
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1
		});

		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		panel.AddChild(vbox);

		var vpc = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(72, 72) };
		vbox.AddChild(vpc);
		var vp = new SubViewport { Size = new Vector2I(72, 72), RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
		vpc.AddChild(vp);
		var cam = new Camera3D();
		cam.SetOrthogonal(0.55f, 0.01f, 10f);
		cam.Position = new Vector3(0, 0, 1.1f);
		vp.AddChild(cam);
		if (pieceSource == null || pieceSource.Mesh == null)
		{
			vbox.AddChild(new Label { Text = $"ID {id} (sans mesh)", HorizontalAlignment = HorizontalAlignment.Center });
			return panel;
		}

		// Mesh dupliqué : évite qu’une même ressource soit liée à la scène établi + la tuile (effets bizarres au QueueFree).
		Mesh meshTuile = (Mesh)pieceSource.Mesh.Duplicate();
		var mi = new MeshInstance3D { Mesh = meshTuile };
		mi.Scale = pieceSource.Scale;
		mi.Rotation = pieceSource.Rotation;
		vp.AddChild(mi);
		var lumiereTuile = new DirectionalLight3D { RotationDegrees = new Vector3(-40, 35, 0) };
		lumiereTuile.Set("sky_mode", 1);
		vp.AddChild(lumiereTuile);
		Joueur.AppliquerMaterielObjet(mi, id,
			porteur.HasMeta("IndexChimique") ? (int)porteur.GetMeta("IndexChimique").AsInt32() : 0,
			porteur.HasMeta("IndexMorphologique") ? (int)porteur.GetMeta("IndexMorphologique").AsInt32() : 0,
			porteur.HasMeta("NiveauFracture") ? (int)porteur.GetMeta("NiveauFracture").AsInt32() : 0);

		vbox.AddChild(new Label { Text = $"ID {id}", HorizontalAlignment = HorizontalAlignment.Center });

		Node3D porteurCap = porteur;
		panel.GuiInput += e =>
		{
			if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				PrendrePieceEtabliVersTransit(porteurCap);
		};

		return panel;
	}

	private void PrendrePieceEtabliVersTransit(Node3D porteur)
	{
		if (_joueurRef == null || porteur == null || !GodotObject.IsInstanceValid(porteur)) return;
		if (EnTransit || PieceSurCurseur)
		{
			GD.Print("ZERO-K : Main CAO / curseur déjà occupé.");
			return;
		}

		if (SaisirPorteurSurEtabli(porteur))
			GD.Print($"ZERO-K : Pièce ID {_transit.ID} prise depuis l'établi (tuile) — suit le curseur.");
	}

	/// <summary>Boîte englobante monde du mesh (rotation + échelle du MeshInstance3D, pas seulement du porteur).</summary>
	private Aabb ObtenirAabbGlobale(Node3D piece)
	{
		MeshInstance3D mi = TrouverPremierMeshInstance(piece);
		if (mi == null || mi.Mesh == null)
			return new Aabb(piece.GlobalPosition, Vector3.One * 0.05f);

		Aabb aabb = mi.Mesh.GetAabb();
		Transform3D gt = mi.GlobalTransform;

		Vector3[] corners = new Vector3[8]
		{
			new Vector3(aabb.Position.X, aabb.Position.Y, aabb.Position.Z),
			new Vector3(aabb.Position.X + aabb.Size.X, aabb.Position.Y, aabb.Position.Z),
			new Vector3(aabb.Position.X, aabb.Position.Y + aabb.Size.Y, aabb.Position.Z),
			new Vector3(aabb.Position.X + aabb.Size.X, aabb.Position.Y + aabb.Size.Y, aabb.Position.Z),
			new Vector3(aabb.Position.X, aabb.Position.Y, aabb.Position.Z + aabb.Size.Z),
			new Vector3(aabb.Position.X + aabb.Size.X, aabb.Position.Y, aabb.Position.Z + aabb.Size.Z),
			new Vector3(aabb.Position.X, aabb.Position.Y + aabb.Size.Y, aabb.Position.Z + aabb.Size.Z),
			new Vector3(aabb.Position.X + aabb.Size.X, aabb.Position.Y + aabb.Size.Y, aabb.Position.Z + aabb.Size.Z)
		};

		Vector3 min = gt * corners[0];
		Vector3 max = min;
		for (int i = 1; i < 8; i++)
		{
			Vector3 p = gt * corners[i];
			min = new Vector3(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y), Mathf.Min(min.Z, p.Z));
			max = new Vector3(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y), Mathf.Max(max.Z, p.Z));
		}

		return new Aabb(min, max - min).Grow(0.04f);
	}

	private bool VerifierIntegritePhysique(List<Node3D> pieces)
	{
		if (pieces.Count <= 1)
			return true;

		var aabbs = new List<Aabb>();
		var estLigature = new List<bool>();
		foreach (Node3D p in pieces)
		{
			aabbs.Add(ObtenirAabbGlobale(p));
			estLigature.Add(p.HasMeta("EstLigature") && p.GetMeta("EstLigature").AsBool());
		}

		var connectes = new HashSet<int> { 0 };

		bool changement;
		do
		{
			changement = false;
			for (int i = 0; i < pieces.Count; i++)
			{
				if (connectes.Contains(i))
					continue;

				for (int j = 0; j < pieces.Count; j++)
				{
					if (!connectes.Contains(j))
						continue;

					// Au moins une des deux pièces doit être une ligature : pas de connexion rigide–rigide (ex. bois–roche sans corde).
					if (!estLigature[i] && !estLigature[j])
						continue;

					if (aabbs[i].Intersects(aabbs[j]))
					{
						connectes.Add(i);
						changement = true;
						break;
					}
				}
			}
		} while (changement);

		return connectes.Count == pieces.Count;
	}

	/// <summary>Génome d'assemblage : positions relatives au cœur arrondies à 2 cm, rotations à 15° (tolérance humaine).</summary>
	private string CalculerGenomeAssemblage()
	{
		var pieces = new List<Node3D>();
		foreach (Node e in _etabliSpatial.GetChildren())
		{
			if (e is Node3D nd && nd.HasMeta("ID"))
				pieces.Add(nd);
		}

		if (pieces.Count == 0)
			return "";

		// 1. Pièce maîtresse (manche / centre : bûche 30 ou bâton 32 en priorité)
		Node3D coeur = pieces[0];
		foreach (Node3D p in pieces)
		{
			int idP = (int)p.GetMeta("ID").AsInt32();
			if (idP == 30 || idP == 32)
			{
				coeur = p;
				break;
			}
		}

		// 2. Relatives au cœur
		var listeADN = new List<string>();
		Transform3D invCoeur = coeur.GlobalTransform.AffineInverse();

		foreach (Node3D p in pieces)
		{
			if (p == coeur)
				continue;

			Vector3 posRelative = invCoeur * p.GlobalPosition;
			float rx = Mathf.Round(posRelative.X / 0.02f) * 0.02f;
			float ry = Mathf.Round(posRelative.Y / 0.02f) * 0.02f;
			float rz = Mathf.Round(posRelative.Z / 0.02f) * 0.02f;

			Basis relBasis = invCoeur.Basis * p.GlobalTransform.Basis;
			Vector3 rotRef = relBasis.GetEuler();
			float rotX = Mathf.Round(Mathf.RadToDeg(rotRef.X) / 15f) * 15f;
			float rotY = Mathf.Round(Mathf.RadToDeg(rotRef.Y) / 15f) * 15f;
			float rotZ = Mathf.Round(Mathf.RadToDeg(rotRef.Z) / 15f) * 15f;

			int idPiece = (int)p.GetMeta("ID").AsInt32();
			int nivF = p.HasMeta("NiveauFracture") ? (int)p.GetMeta("NiveauFracture").AsInt32() : 0;
			bool estLien = p.HasMeta("EstLigature") && p.GetMeta("EstLigature").AsBool();
			string type = estLien ? "LIEN" : "COMP";
			string adn = $"[{type}_{idPiece}_F:{nivF}_P:{rx:F2},{ry:F2},{rz:F2}_R:{rotX:F0},{rotY:F0},{rotZ:F0}]";
			listeADN.Add(adn);
		}

		listeADN.Sort();

		int idCoeur = (int)coeur.GetMeta("ID").AsInt32();
		int nivCoeur = coeur.HasMeta("NiveauFracture") ? (int)coeur.GetMeta("NiveauFracture").AsInt32() : 0;
		string genomeFinal = $"COEUR[ID:{idCoeur}_F:{nivCoeur}]_LIES:";
		foreach (string adn in listeADN)
			genomeFinal += adn;

		return genomeFinal;
	}

	private void ExecuterForge()
	{
		var pieces = new List<Node3D>();
		foreach (Node e in _etabliSpatial.GetChildren())
		{
			if (e is Node3D nd && e.HasMeta("ID"))
				pieces.Add(nd);
		}

		if (pieces.Count <= 1)
		{
			GD.Print("ZERO-K : Vous avez besoin d'au moins 2 composants pour forger.");
			return;
		}

		if (!VerifierIntegritePhysique(pieces))
		{
			GD.Print("ZERO-K : LE BAKE EST BLOQUÉ ! Des composants flottent dans le vide ou ne sont pas attachés.");
			return;
		}

		_genomeEnAttente = CalculerGenomeAssemblage();
		GD.Print($"ZERO-K : Séquence générée -> {_genomeEnAttente}");

		if (string.IsNullOrEmpty(_genomeEnAttente))
		{
			GD.PrintErr("ZERO-K : Génome vide — impossible de forger.");
			return;
		}

		if (BaseDeDonneesBrevets.ContainsKey(_genomeEnAttente))
		{
			string nomConnu = BaseDeDonneesBrevets[_genomeEnAttente];
			GD.Print($"ZERO-K : Assemblage reconnu ! Vous avez forgé : {nomConnu}");
			FinaliserBake(nomConnu, _genomeEnAttente);
		}
		else
		{
			_panelBrevet.Visible = true;
			if (_inputNomInvention != null)
			{
				_inputNomInvention.Text = "";
				_inputNomInvention.CallDeferred(LineEdit.MethodName.GrabFocus);
			}
		}
	}

	private void ValiderBrevet()
	{
		if (_inputNomInvention == null) return;
		string nom = _inputNomInvention.Text.Trim();
		if (string.IsNullOrEmpty(nom))
			return;

		if (string.IsNullOrEmpty(_genomeEnAttente))
		{
			_panelBrevet.Visible = false;
			return;
		}

		BaseDeDonneesBrevets[_genomeEnAttente] = nom;
		_panelBrevet.Visible = false;
		_inputNomInvention.Text = "";

		GD.Print($"ZERO-K : NOUVEAU BREVET ! L'univers se souviendra de : {nom}");
		FinaliserBake(nom, _genomeEnAttente);
	}

	/// <summary>Phase 3.5 : fusion des meshes de l’établi en un seul outil (ID 100), stats enregistrées, table vidée, CAO fermée.</summary>
	private void FinaliserBake(string nomOutil, string genomeAssemblage)
	{
		if (_joueurRef == null) return;

		var pieces = new List<Node3D>();
		foreach (Node e in _etabliSpatial.GetChildren())
		{
			if (e is Node3D n && n.HasMeta("ID"))
				pieces.Add(n);
		}

		if (pieces.Count == 0)
			return;

		Node3D coeur = pieces[0];
		foreach (Node3D p in pieces)
		{
			int idP = (int)p.GetMeta("ID").AsInt32();
			if (idP == 30 || idP == 32)
			{
				coeur = p;
				break;
			}
		}

		var meshFinal = new ArrayMesh();
		int surfaceIdx = 0;
		float masseTotale = 0f;
		float meilleurTranchant = 0.1f;
		Vector3? axeTranchantHerite = null;

		Transform3D invCoeur = coeur.GlobalTransform.AffineInverse();

		foreach (Node3D p in pieces)
		{
			int id = (int)p.GetMeta("ID").AsInt32();

			if (id == 100 && p.HasMeta("HashMesh"))
			{
				int hash = (int)p.GetMeta("HashMesh").AsInt32();
				if (hash != 0 && Joueur.RegistreOutilsForges.TryGetValue(hash, out var statsHerites))
				{
					masseTotale += statsHerites.Masse;
					meilleurTranchant = Mathf.Min(meilleurTranchant, statsHerites.EpaisseurLameBase);
					Vector3 dirMonde = (p.GlobalTransform.Basis * statsHerites.AxeTranchantLocal).Normalized();
					axeTranchantHerite = (invCoeur.Basis * dirMonde).Normalized();
				}
			}
			else if (id == 32) masseTotale += 0.8f;
			else if (id == 30) masseTotale += 15f;
			else if (id == 11) { masseTotale += 2f; meilleurTranchant = Mathf.Min(meilleurTranchant, 0.015f); }
			else if (id >= 10 && id <= 14) { masseTotale += 4f; meilleurTranchant = Mathf.Min(meilleurTranchant, 0.04f); }
			else if (id == 15 || id == 16 || id == 17 || id == 20) masseTotale += 0.1f;

			MeshInstance3D mi = TrouverPremierMeshInstance(p);
			if (mi == null || mi.Mesh == null)
				continue;

			// Ligature (corde / fibres) : compte pour la masse, pas de géométrie sur l’outil forgé.
			if (p.HasMeta("EstLigature") && p.GetMeta("EstLigature").AsBool())
				continue;

			Transform3D transRelative = invCoeur * mi.GlobalTransform;
			int surfCount = mi.Mesh.GetSurfaceCount();
			for (int surf = 0; surf < surfCount; surf++)
			{
				var st = new SurfaceTool();
				st.AppendFrom(mi.Mesh, surf, transRelative);
				st.Commit(meshFinal);

				Material mat = mi.MaterialOverride;
				if (mat == null)
					mat = mi.Mesh.SurfaceGetMaterial(surf);
				Material matAssign = mat != null
					? (Material)mat.Duplicate()
					: new StandardMaterial3D { Roughness = 0.85f, AlbedoColor = new Color(0.55f, 0.5f, 0.45f) };
				meshFinal.SurfaceSetMaterial(surfaceIdx, matAssign);
				surfaceIdx++;
			}
		}

		if (surfaceIdx == 0)
		{
			GD.PrintErr("ZERO-K : Bake impossible — aucune surface de mesh fusionnable sur l'établi.");
			return;
		}

		Vector3 axeTranchantFinal = Vector3.Up;

		Node3D tete = null;
		float maxDistY = 0.1f;
		foreach (Node3D p in pieces)
		{
			int pId = (int)p.GetMeta("ID").AsInt32();
			if (pId >= 10 && pId <= 14)
			{
				Transform3D tRel = coeur.GlobalTransform.AffineInverse() * p.GlobalTransform;
				if (Mathf.Abs(tRel.Origin.Y) > maxDistY)
				{
					maxDistY = Mathf.Abs(tRel.Origin.Y);
					tete = p;
				}
			}
		}

		if (tete != null)
		{
			MeshInstance3D miTete = TrouverPremierMeshInstance(tete);
			if (miTete != null && miTete.Mesh != null)
			{
				Transform3D tm = coeur.GlobalTransform.AffineInverse() * miTete.GlobalTransform;
				Aabb abb = miTete.Mesh.GetAabb();
				Vector3 s = abb.Size;
				Vector3 axeLoc = (s.X <= s.Y && s.X <= s.Z) ? Vector3.Right
					: (s.Y <= s.Z ? Vector3.Up : Vector3.Back);
				axeTranchantFinal = (tm.Basis * axeLoc).Normalized();
			}
			else
			{
				Transform3D transRelative = coeur.GlobalTransform.AffineInverse() * tete.GlobalTransform;
				axeTranchantFinal = transRelative.Basis.Y.Normalized();
			}
		}

		if (axeTranchantHerite.HasValue)
			axeTranchantFinal = axeTranchantHerite.Value;

		string genome = genomeAssemblage ?? "";
		int clefRegistre = Joueur.HashGenomeStable(genome);
		if (clefRegistre == 0)
			clefRegistre = meshFinal.GetHashCode();
		Joueur.RegistreOutilsForges[clefRegistre] = new Joueur.StatsOutilForge
		{
			Masse = masseTotale,
			EpaisseurLameBase = meilleurTranchant,
			AxeTranchantLocal = axeTranchantFinal,
			Nom = nomOutil
		};

		var slotBake = new SlotInventaire
		{
			ID = 100,
			EstUnEclat = true,
			MeshEclat = meshFinal,
			ScaleEclat = Vector3.One,
			IndexMorphologique = 0,
			GenomeAssemblage = genome
		};

		if (_joueurRef.MainDroite.EstVide)
			_joueurRef.MainDroite = slotBake;
		else if (_joueurRef.MainGauche.EstVide)
			_joueurRef.MainGauche = slotBake;
		else
		{
			_joueurRef.MainDroite = slotBake;
			GD.Print("ZERO-K : Mains pleines. L'outil précédent a été écrasé/lâché.");
		}

		foreach (Node3D p in pieces)
			p.QueueFree();

		DetruireObjetCurseurCAOIfAny();
		_transit = default;
		_cibleRotationEtabli = null;
		_tissuSegmentA = null;
		_tissuSegmentB = null;

		_joueurRef.RafraichirHUD();
		RafraichirBandeauEtabli();
		MettreAJourLabelTransit();

		BasculerVisibilite();

		GD.Print($"ZERO-K : FORGE TERMINÉE. [{nomOutil}] a été transféré dans vos mains. (Masse: {masseTotale} kg)");
	}
}
