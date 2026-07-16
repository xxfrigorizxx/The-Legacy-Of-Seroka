using Godot;

/// <summary>
/// Inspection d'un PNJ humain : clic droit en visant un PNJ ouvre un panneau (stats + carnet + inventaire).
/// L'en-tête (titre + Fermer) est stable ; le contenu se RAFRAÎCHIT en continu (timer) pour refléter l'état réel.
/// </summary>
public partial class Joueur : CharacterBody3D
{
	private static readonly string[] NomsBaieInspect =
		{ "rouge", "violette", "orange", "bleue", "jaune", "verte", "noire", "rose", "cyan" };

	private CanvasLayer _coucheInspectionPnj;
	private Label _titreInspectionPnj;
	private VBoxContainer _contenuInspectionPnj;
	private PnjHumain _pnjInspecte;

	public bool InspectionPnjOuverte => _coucheInspectionPnj != null && GodotObject.IsInstanceValid(_coucheInspectionPnj) && _coucheInspectionPnj.Visible;

	private bool EssayerInspecterPnjSousVisee()
	{
		Camera3D cam = _camera != null && GodotObject.IsInstanceValid(_camera) ? _camera : _cameraFps;
		if (cam == null || !GodotObject.IsInstanceValid(cam))
			return false;
		PhysicsDirectSpaceState3D espace = GetWorld3D()?.DirectSpaceState;
		if (espace == null)
			return false;

		Vector3 from = cam.GlobalPosition;
		Vector3 dir = -cam.GlobalTransform.Basis.Z;
		var q = PhysicsRayQueryParameters3D.Create(from, from + dir * 4.5f);
		q.CollideWithBodies = true;
		q.CollideWithAreas = false;
		if (GetRid().IsValid)
			q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = espace.IntersectRay(q);
		if (hit == null || hit.Count == 0 || !hit.ContainsKey("collider"))
			return false;
		PnjHumain pnj = TrouverPnjAncetre(hit["collider"].AsGodotObject() as Node);
		if (pnj == null)
			return false;
		OuvrirInspectionPnj(pnj);
		return true;
	}

	private static PnjHumain TrouverPnjAncetre(Node n)
	{
		while (n != null)
		{
			if (n is PnjHumain p)
				return p;
			n = n.GetParent();
		}
		return null;
	}

	private void OuvrirInspectionPnj(PnjHumain pnj)
	{
		if (pnj == null || !GodotObject.IsInstanceValid(pnj))
			return;
		_pnjInspecte = pnj;
		AssurerUiInspectionPnj();
		RemplirInspectionPnj(pnj);
		_coucheInspectionPnj.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public void FermerInspectionPnj()
	{
		_pnjInspecte = null;
		if (_coucheInspectionPnj != null && GodotObject.IsInstanceValid(_coucheInspectionPnj))
			_coucheInspectionPnj.Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void RafraichirInspectionPnj()
	{
		if (_coucheInspectionPnj == null || !_coucheInspectionPnj.Visible)
			return;
		if (_pnjInspecte == null || !GodotObject.IsInstanceValid(_pnjInspecte))
		{
			FermerInspectionPnj();
			return;
		}
		RemplirInspectionPnj(_pnjInspecte);
	}

	private void AssurerUiInspectionPnj()
	{
		if (_coucheInspectionPnj != null && GodotObject.IsInstanceValid(_coucheInspectionPnj))
			return;

		_coucheInspectionPnj = new CanvasLayer { Name = "InspectionPnjLayer", Layer = 50, Visible = false };
		AddChild(_coucheInspectionPnj);

		var fond = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = Control.MouseFilterEnum.Stop };
		fond.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		fond.GuiInput += (InputEvent e) =>
		{
			if (e is InputEventMouseButton mb && mb.Pressed)
				FermerInspectionPnj();
		};
		_coucheInspectionPnj.AddChild(fond);

		var panneau = new Panel
		{
			AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
			OffsetLeft = -230f, OffsetTop = -260f, OffsetRight = 230f, OffsetBottom = 260f
		};
		_coucheInspectionPnj.AddChild(panneau);

		var marge = new MarginContainer();
		marge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		marge.AddThemeConstantOverride("margin_left", 14);
		marge.AddThemeConstantOverride("margin_right", 14);
		marge.AddThemeConstantOverride("margin_top", 12);
		marge.AddThemeConstantOverride("margin_bottom", 12);
		panneau.AddChild(marge);

		var racine = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		racine.AddThemeConstantOverride("separation", 6);
		marge.AddChild(racine);

		// En-tête STABLE : titre + bouton Fermer (jamais reconstruit -> toujours cliquable).
		var entete = new HBoxContainer();
		_titreInspectionPnj = new Label { Text = "Inspection", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_titreInspectionPnj.AddThemeFontSizeOverride("font_size", 20);
		entete.AddChild(_titreInspectionPnj);
		var btnFermer = new Button { Text = "Fermer (Échap)" };
		btnFermer.Pressed += FermerInspectionPnj;
		entete.AddChild(btnFermer);
		racine.AddChild(entete);
		racine.AddChild(new HSeparator());

		var defilement = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		racine.AddChild(defilement);
		_contenuInspectionPnj = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_contenuInspectionPnj.AddThemeConstantOverride("separation", 4);
		defilement.AddChild(_contenuInspectionPnj);

		// Rafraîchissement live du contenu.
		var timer = new Timer { Name = "TimerRefreshInspectionPnj", WaitTime = 0.3, Autostart = true };
		timer.Timeout += RafraichirInspectionPnj;
		_coucheInspectionPnj.AddChild(timer);
	}

	private void RemplirInspectionPnj(PnjHumain pnj)
	{
		if (_contenuInspectionPnj == null)
			return;
		if (_titreInspectionPnj != null)
			_titreInspectionPnj.Text = $"{pnj.NomPnj}{(pnj.EstRebelle ? " (rebelle)" : "")} — {(pnj.EstGentil ? "Gentil" : "Mechant")} {Mathf.RoundToInt(pnj.RatioAlignement * 100f)}%";

		foreach (Node c in _contenuInspectionPnj.GetChildren())
			c.QueueFree();

		_contenuInspectionPnj.AddChild(new Label
		{
			Text = $"Vie {Mathf.RoundToInt(pnj.RatioVieGlobale() * 100f)}%    Faim {Mathf.RoundToInt(pnj.RatioFaim() * 100f)}%    Stamina {Mathf.RoundToInt(pnj.RatioStamina() * 100f)}%    Int {pnj.Intelligence}"
		});
		_contenuInspectionPnj.AddChild(new Label
		{
			Text = pnj.Societe != null ? $"Société : {pnj.Societe.Nom} ({pnj.RangSociete})" : "Sans société"
		});

		_contenuInspectionPnj.AddChild(new HSeparator());
		_contenuInspectionPnj.AddChild(new Label { Text = "Membres" });
		for (int i = 0; i < pnj.NombreMembres; i++)
			_contenuInspectionPnj.AddChild(new Label { Text = $"   {pnj.NomMembre(i)} : {pnj.PvMembre(i)}/{pnj.PvMembreMax(i)} PV" });

		_contenuInspectionPnj.AddChild(new HSeparator());
		_contenuInspectionPnj.AddChild(new Label { Text = "Carnet du savoir (cerveau)" });
		var carnet = pnj.Carnet;
		if (carnet == null || carnet.Count == 0)
			_contenuInspectionPnj.AddChild(new Label { Text = "   Rien d'appris encore.", Modulate = new Color(0.7f, 0.75f, 0.8f) });
		else
			for (int i = 0; i < carnet.Count; i++)
				_contenuInspectionPnj.AddChild(new Label { Text = $"   • {carnet[i]}" });

		_contenuInspectionPnj.AddChild(new HSeparator());
		_contenuInspectionPnj.AddChild(new Label { Text = "Inventaire (2 mains + 4 craft, pas de sac)" });
		var grille = new GridContainer { Columns = 2 };
		_contenuInspectionPnj.AddChild(grille);
		SlotInventaire[] inv = pnj.Inventaire;
		string[] roles = { "Main D", "Main G", "Craft 1", "Craft 2", "Craft 3", "Craft 4" };
		if (inv != null)
		{
			for (int i = 0; i < inv.Length; i++)
			{
				var cell = new Panel { CustomMinimumSize = new Vector2(196f, 34f) };
				var lbl = new Label
				{
					Text = $"{(i < roles.Length ? roles[i] : "Slot " + i)} : {TexteSlotInspect(inv[i])}",
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				lbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
				cell.AddChild(lbl);
				grille.AddChild(cell);
			}
		}
	}

	private static string TexteSlotInspect(SlotInventaire slot)
	{
		if (slot.EstVide)
			return "—";
		if (slot.ID == Joueur.IdObjetBaie)
		{
			int c = Mathf.Clamp(slot.IndexChimique, 0, NomsBaieInspect.Length - 1);
			return $"Baie {NomsBaieInspect[c]} x{slot.Quantite}";
		}
		return $"#{slot.ID} x{slot.Quantite}";
	}
}
