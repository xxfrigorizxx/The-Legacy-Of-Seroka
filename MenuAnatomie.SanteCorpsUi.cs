using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void AssurerPanneauSanteCorps()
	{
		if (VueJoueurPanel == null || !GodotObject.IsInstanceValid(VueJoueurPanel))
			VueJoueurPanel = GetNodeOrNull<Panel>(CheminVueJoueurPanel) ?? FindChild("VueJoueurPanel", true, false) as Panel;
		if (VueJoueurPanel == null)
			return;

		Panel panneauExistant = VueJoueurPanel.GetNodeOrNull<Panel>("PanneauSanteCorps");
		if (panneauExistant != null && GodotObject.IsInstanceValid(panneauExistant))
		{
			_panneauSanteCorps = panneauExistant;
			if (_barresSanteCorps.Count == 0)
				RehydraterReferencesSanteCorpsDepuisArbre();
			return;
		}

		if (_panneauSanteCorps != null && GodotObject.IsInstanceValid(_panneauSanteCorps))
			return;

		SupprimerPanneauxSanteCorpsOrphelins();
		_barresSanteCorps.Clear();
		_labelsSanteCorps.Clear();
		_labelsEtatOsCorps.Clear();
		_stylesRemplissageSanteCorps.Clear();

		if (VueJoueurPanel.GetNodeOrNull<Label>("Label") is Label labelScene)
			labelScene.Visible = false;

		_panneauSanteCorps = new Panel
		{
			Name = "PanneauSanteCorps",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_panneauSanteCorps.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_panneauSanteCorps.OffsetLeft = 8f;
		_panneauSanteCorps.OffsetTop = 8f;
		_panneauSanteCorps.OffsetRight = -8f;
		_panneauSanteCorps.OffsetBottom = -8f;
		VueJoueurPanel.AddChild(_panneauSanteCorps);

		var marge = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		marge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		marge.AddThemeConstantOverride("margin_left", 10);
		marge.AddThemeConstantOverride("margin_top", 10);
		marge.AddThemeConstantOverride("margin_right", 10);
		marge.AddThemeConstantOverride("margin_bottom", 10);
		_panneauSanteCorps.AddChild(marge);

		var colonne = new VBoxContainer
		{
			Name = "ColonneSanteCorps",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		colonne.AddThemeConstantOverride("separation", 8);
		marge.AddChild(colonne);

		var titre = new Label
		{
			Text = "Anatomie / Points de vie",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		titre.AddThemeFontSizeOverride("font_size", 16);
		titre.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.98f));
		colonne.AddChild(titre);

		_lblSanteGlobaleCorps = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblSanteGlobaleCorps.AddThemeFontSizeOverride("font_size", 13);
		_lblSanteGlobaleCorps.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 0.86f));
		colonne.AddChild(_lblSanteGlobaleCorps);

		_lblForceEtMultiplicateur = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblForceEtMultiplicateur.AddThemeFontSizeOverride("font_size", 12);
		_lblForceEtMultiplicateur.AddThemeColorOverride("font_color", new Color(0.86f, 0.90f, 0.98f));
		colonne.AddChild(_lblForceEtMultiplicateur);

		var cadreApercu = new Panel
		{
			Name = "CadreApercuJoueurCorps",
			CustomMinimumSize = new Vector2(0, 380),
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		cadreApercu.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		colonne.AddChild(cadreApercu);
		AssurerApercuJoueurCorps(cadreApercu);

		_lblPoidsMaxSousApercu = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblPoidsMaxSousApercu.AddThemeFontSizeOverride("font_size", 12);
		_lblPoidsMaxSousApercu.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.78f));
		colonne.AddChild(_lblPoidsMaxSousApercu);

		colonne.AddChild(new HSeparator());

		var scroll = new ScrollContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		colonne.AddChild(scroll);

		var colonneSections = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		colonneSections.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		colonneSections.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(colonneSections);

		CreerLigneSanteCorps(colonneSections, "tete", "Tete");
		CreerLigneSanteCorps(colonneSections, "torse", "Torse");
		CreerLigneSanteCorps(colonneSections, "bras_gauche", "Bras gauche");
		CreerLigneSanteCorps(colonneSections, "bras_droit", "Bras droit");
		CreerLigneSanteCorps(colonneSections, "jambe_gauche", "Jambe gauche");
		CreerLigneSanteCorps(colonneSections, "jambe_droite", "Jambe droite");

		colonne.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
		_boiteFaimEnergieExterne = new VBoxContainer
		{
			Name = "BoiteFaimEnergieExterne",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_boiteFaimEnergieExterne.AddThemeConstantOverride("separation", 6);
		_boiteFaimEnergieExterne.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		colonne.AddChild(_boiteFaimEnergieExterne);
	}

	private void AssurerApercuJoueurCorps(Panel parent)
	{
		if (parent == null || (_vpApercuJoueurCorps != null && GodotObject.IsInstanceValid(_vpApercuJoueurCorps)))
			return;

		var centreApercu = new CenterContainer
		{
			Name = "CentreApercuJoueurCorps",
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		centreApercu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centreApercu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		parent.AddChild(centreApercu);

		_vpApercuJoueurCorps = new SubViewportContainer
		{
			Name = "ApercuJoueurCorpsViewport",
			Stretch = true,
			CustomMinimumSize = new Vector2(250f, 360f),
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_vpApercuJoueurCorps.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		_vpApercuJoueurCorps.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		centreApercu.AddChild(_vpApercuJoueurCorps);

		_svApercuJoueurCorps = new SubViewport
		{
			Size = new Vector2I(300, 460),
			RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
			World3D = new World3D(),
			TransparentBg = true
		};
		_vpApercuJoueurCorps.AddChild(_svApercuJoueurCorps);

		_racineApercuJoueurCorps = new Node3D { Name = "RacineApercuJoueurCorps" };
		_svApercuJoueurCorps.AddChild(_racineApercuJoueurCorps);

		_cameraApercuJoueurCorps = new Camera3D
		{
			Name = "CameraApercuJoueurCorps",
			Fov = 33f,
			Near = 0.02f,
			Far = 180f,
			Current = true
		};
		_svApercuJoueurCorps.AddChild(_cameraApercuJoueurCorps);

		var light = new DirectionalLight3D
		{
			Name = "LightApercuJoueurCorps",
			LightEnergy = 1.25f,
			RotationDegrees = new Vector3(-35f, 40f, 0f)
		};
		// Cette lumière reste confinée au World3D dédié de l'aperçu.
		light.Set("sky_mode", 1); // LightOnly
		light.Set("light_volumetric_fog_energy", 0.0f);
		_svApercuJoueurCorps.AddChild(light);
		var lightRemplissage = new OmniLight3D
		{
			Name = "FillApercuJoueurCorps",
			Position = new Vector3(-0.5f, 1.15f, 1.2f),
			LightEnergy = 0.35f,
			OmniRange = 5.0f
		};
		_svApercuJoueurCorps.AddChild(lightRemplissage);

		RafraichirAvatarApercuJoueurCorps(true);
		MettreAJourCameraApercuJoueurCorps(0f);
	}

	private void RafraichirAvatarApercuJoueurCorps(bool forcerRecreation = false)
	{
		if (_joueurRef == null || _svApercuJoueurCorps == null || !GodotObject.IsInstanceValid(_svApercuJoueurCorps))
			return;
		if (_racineApercuJoueurCorps == null || !GodotObject.IsInstanceValid(_racineApercuJoueurCorps))
			return;

		ulong empreinteCourante = _joueurRef.CalculerEmpreinteAvatarApercuUi();
		bool avatarInvalide = _avatarApercuJoueurCorps == null || !GodotObject.IsInstanceValid(_avatarApercuJoueurCorps);
		bool doitReconstruire = forcerRecreation || avatarInvalide || empreinteCourante != _empreinteAvatarApercuJoueurCorps;
		if (doitReconstruire)
		{
			if (_avatarApercuJoueurCorps != null && GodotObject.IsInstanceValid(_avatarApercuJoueurCorps))
				_avatarApercuJoueurCorps.QueueFree();
			_avatarApercuJoueurCorps = _joueurRef.CreerCloneAvatarApercuUi();
			if (_avatarApercuJoueurCorps != null && GodotObject.IsInstanceValid(_avatarApercuJoueurCorps))
			{
				try
				{
					_racineApercuJoueurCorps.AddChild(_avatarApercuJoueurCorps);
					_empreinteAvatarApercuJoueurCorps = empreinteCourante;
				}
				catch (Exception ex)
				{
					GD.PrintErr($"ZERO-K : Echec AddChild avatar aperçu UI: {ex.Message}");
					if (_avatarApercuJoueurCorps != null && GodotObject.IsInstanceValid(_avatarApercuJoueurCorps))
						_avatarApercuJoueurCorps.Free();
					_avatarApercuJoueurCorps = null;
					_empreinteAvatarApercuJoueurCorps = 0UL;
				}
			}
			else
				_empreinteAvatarApercuJoueurCorps = 0UL;
		}

		if (_avatarApercuJoueurCorps != null && GodotObject.IsInstanceValid(_avatarApercuJoueurCorps))
			_joueurRef.SynchroniserTransformAvatarApercuUi(_avatarApercuJoueurCorps);
	}

	private void CreerLigneSanteCorps(VBoxContainer parent, string cleSection, string nomSection)
	{
		var ligne = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		ligne.AddThemeConstantOverride("separation", 2);
		parent.AddChild(ligne);

		var entete = new Label
		{
			Text = nomSection,
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		entete.AddThemeFontSizeOverride("font_size", 12);
		entete.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.95f));
		ligne.AddChild(entete);

		var rangeeBarres = new HBoxContainer
		{
			Name = "RangeeBarresSanteSection",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0, 16)
		};
		rangeeBarres.AddThemeConstantOverride("separation", 0);
		rangeeBarres.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		ligne.AddChild(rangeeBarres);

		var barre = new ProgressBar
		{
			Name = "BarrePvSection",
			MinValue = 0,
			MaxValue = 100,
			Value = 100,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		barre.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		barre.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		var fondBarre = new StyleBoxFlat
		{
			BgColor = new Color(0.22f, 0.22f, 0.22f, 1f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6
		};
		var remplissage = new StyleBoxFlat
		{
			BgColor = new Color(0.25f, 0.82f, 0.35f, 1f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6
		};
		barre.AddThemeStyleboxOverride("background", fondBarre);
		barre.AddThemeStyleboxOverride("fill", remplissage);
		rangeeBarres.AddChild(barre);

		var segmentBrulure = new ColorRect
		{
			Name = "SegmentBrulureSection",
			Color = new Color(0.80f, 0.15f, 0.15f, 0.95f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
			CustomMinimumSize = new Vector2(0, 16)
		};
		segmentBrulure.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		segmentBrulure.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		rangeeBarres.AddChild(segmentBrulure);

		var lblEtatOs = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		lblEtatOs.AddThemeFontSizeOverride("font_size", 11);
		lblEtatOs.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.86f));
		ligne.AddChild(lblEtatOs);

		var lblInfos = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		lblInfos.AddThemeFontSizeOverride("font_size", 11);
		lblInfos.AddThemeColorOverride("font_color", new Color(0.77f, 0.82f, 0.88f));
		ligne.AddChild(lblInfos);

		_barresSanteCorps[cleSection] = barre;
		_labelsEtatOsCorps[cleSection] = lblEtatOs;
		_labelsSanteCorps[cleSection] = lblInfos;
		_stylesRemplissageSanteCorps[cleSection] = remplissage;
		_segmentsBrulureSanteCorps[cleSection] = segmentBrulure;
	}

	private void SupprimerPanneauxSanteCorpsOrphelins()
	{
		if (VueJoueurPanel == null || !GodotObject.IsInstanceValid(VueJoueurPanel))
			return;
		for (int i = VueJoueurPanel.GetChildCount() - 1; i >= 0; i--)
		{
			if (VueJoueurPanel.GetChild(i) is Panel p && p.Name == "PanneauSanteCorps")
				p.QueueFree();
		}
		_panneauSanteCorps = null;
	}

	private void RehydraterReferencesSanteCorpsDepuisArbre()
	{
		_barresSanteCorps.Clear();
		_labelsSanteCorps.Clear();
		_labelsEtatOsCorps.Clear();
		_stylesRemplissageSanteCorps.Clear();
		_segmentsBrulureSanteCorps.Clear();
		if (_panneauSanteCorps == null || !GodotObject.IsInstanceValid(_panneauSanteCorps))
			return;

		var colonne = _panneauSanteCorps.FindChild("ColonneSanteCorps", true, false) as VBoxContainer;
		if (colonne == null)
			return;

		ScrollContainer scroll = null;
		for (int i = 0; i < colonne.GetChildCount(); i++)
		{
			if (colonne.GetChild(i) is ScrollContainer sc)
			{
				scroll = sc;
				break;
			}
		}
		if (scroll == null || scroll.GetChildCount() == 0)
			return;
		if (scroll.GetChild(0) is not VBoxContainer sections)
			return;

		for (int i = 0; i < ClesSectionsSanteCorpsOrdre.Length && i < sections.GetChildCount(); i++)
		{
			if (sections.GetChild(i) is not VBoxContainer ligne || ligne.GetChildCount() < 4)
				continue;
			string cle = ClesSectionsSanteCorpsOrdre[i];
			if (ligne.GetChild(1) is HBoxContainer rangeeBarres)
			{
				ProgressBar barre = rangeeBarres.GetNodeOrNull<ProgressBar>("BarrePvSection");
				if (barre != null)
				{
					_barresSanteCorps[cle] = barre;
					if (barre.GetThemeStylebox("fill") is StyleBoxFlat styleRemplissage)
						_stylesRemplissageSanteCorps[cle] = styleRemplissage;
				}
				ColorRect segmentBrulure = rangeeBarres.GetNodeOrNull<ColorRect>("SegmentBrulureSection");
				if (segmentBrulure != null)
					_segmentsBrulureSanteCorps[cle] = segmentBrulure;
			}
			else if (ligne.GetChild(1) is ProgressBar barreAncienneVersion)
			{
				_barresSanteCorps[cle] = barreAncienneVersion;
				if (barreAncienneVersion.GetThemeStylebox("fill") is StyleBoxFlat styleRemplissage)
					_stylesRemplissageSanteCorps[cle] = styleRemplissage;
			}
			if (ligne.GetChild(2) is Label lblOs)
				_labelsEtatOsCorps[cle] = lblOs;
			if (ligne.GetChild(3) is Label lblPv)
				_labelsSanteCorps[cle] = lblPv;
		}
	}

	private static string FormaterTextePvSection(Joueur.SectionSanteCorps section)
	{
		float ratioPct = section.PointsVieMax > 0.001f
			? Mathf.Clamp(section.PointsVie / section.PointsVieMax, 0f, 1f) * 100f
			: 0f;
		int pv = Mathf.CeilToInt(section.PointsVie);
		int pvMax = Mathf.CeilToInt(section.PointsVieMax);
		int pvBrulure = Mathf.CeilToInt(Mathf.Max(0f, section.PointsVieBrulureBloquee));
		string suffixeBrulure = pvBrulure > 0 ? $"  |  Brulure max: -{pvBrulure}" : "";
		return $"{pv}/{pvMax} PV ({ratioPct:F0}%)  |  Matiere: {section.Matiere}{suffixeBrulure}";
	}

	private static void ForcerRafraichissementLabelPv(Label lbl, string texte)
	{
		if (lbl == null || !GodotObject.IsInstanceValid(lbl))
			return;
		lbl.Text = texte;
		lbl.QueueRedraw();
	}

	private static Color CouleurSanteDepuisRatio(float ratio)
	{
		if (ratio >= 0.66f) return new Color(0.25f, 0.82f, 0.35f, 1f);
		if (ratio >= 0.33f) return new Color(0.95f, 0.67f, 0.20f, 1f);
		return new Color(0.88f, 0.22f, 0.22f, 1f);
	}

	private static (string symbole, string etat) EvaluerEtatOs(float ratioOs)
	{
		if (ratioOs <= 0.35f)
			return ("[X]", "CASSE");
		if (ratioOs <= 0.70f)
			return ("[!]", "FELURE");
		return ("[OK]", "BON ETAT");
	}

	/// <summary>Rafraîchit immédiatement les PV affichés (menu Q ouvert) — appelé après dégâts / soins.</summary>
	public void RafraichirSanteCorpsImmediate()
	{
		if (!EstOuvert || _joueurRef == null)
			return;
		RafraichirPanneauSanteCorps();
	}

	private void RafraichirPanneauSanteCorps(bool inclureAvatar = true)
	{
		if (_joueurRef == null)
			return;
		AssurerPanneauSanteCorps();
		if (inclureAvatar)
			RafraichirAvatarApercuJoueurCorps();
		if (_barresSanteCorps.Count == 0)
			RehydraterReferencesSanteCorpsDepuisArbre();
		if (_barresSanteCorps.Count == 0)
			return;

		float ratioGlobal = _joueurRef.ObtenirRatioSanteGlobaleCorps();
		if (_lblSanteGlobaleCorps != null)
			_lblSanteGlobaleCorps.Text = $"Sante globale: {(ratioGlobal * 100f):F0}%";
		if (_lblForceEtMultiplicateur != null)
		{
			ulong niveauForce = _joueurRef.ObtenirNiveauFutureState("Force");
			float multiplicateurForce = _joueurRef.ObtenirMultiplicateurCapaciteChargeForce();
			_lblForceEtMultiplicateur.Text = $"Force: niv {niveauForce:N0} | Multiplicateur: x{multiplicateurForce:F4}";
		}
		if (_lblPoidsMaxSousApercu != null)
		{
			float poidsActuelKg = _joueurRef.ObtenirPoidsTotalPorteKg();
			float poidsMaxKg = _joueurRef.ObtenirCapacitePoidsMaxKg();
			_lblPoidsMaxSousApercu.Text = $"Poids: {poidsActuelKg:F2} / {poidsMaxKg:F2} kg";
		}

		IReadOnlyList<Joueur.SectionSanteCorps> sections = _joueurRef.ObtenirEtatSanteCorps();
		for (int i = 0; i < sections.Count; i++)
		{
			Joueur.SectionSanteCorps section = sections[i];
			float ratioSection = section.PointsVieMax > 0 ? section.PointsVie / (float)section.PointsVieMax : 0f;
			float ratioOs = section.IntegriteOsMax > 0 ? section.IntegriteOs / (float)section.IntegriteOsMax : 0f;
			var etatOs = EvaluerEtatOs(ratioOs);
			if (_barresSanteCorps.TryGetValue(section.Cle, out ProgressBar barre))
			{
				float pvMaxBrut = Mathf.Max(0f, section.PointsVieMaxBrut);
				float pvMaxCourant = Mathf.Max(0f, section.PointsVieMax);
				float ratioZoneSoignable = pvMaxBrut > 0.001f ? Mathf.Clamp(pvMaxCourant / pvMaxBrut, 0f, 1f) : 1f;
				float ratioZoneBrulee = pvMaxBrut > 0.001f ? Mathf.Clamp(section.PointsVieBrulureBloquee / pvMaxBrut, 0f, 1f) : 0f;
				if (pvMaxBrut <= 0.001f)
				{
					ratioZoneSoignable = 1f;
					ratioZoneBrulee = 0f;
				}

				barre.MaxValue = Mathf.Max(1, section.PointsVieMax);
				barre.Value = Mathf.Clamp(section.PointsVie, 0, section.PointsVieMax);
				barre.SizeFlagsStretchRatio = Mathf.Max(0.001f, ratioZoneSoignable);
				if (_stylesRemplissageSanteCorps.TryGetValue(section.Cle, out StyleBoxFlat styleRemplissage))
					styleRemplissage.BgColor = CouleurSanteDepuisRatio(ratioSection);
				if (_segmentsBrulureSanteCorps.TryGetValue(section.Cle, out ColorRect segmentBrulure))
				{
					segmentBrulure.SizeFlagsStretchRatio = Mathf.Max(0.001f, ratioZoneBrulee);
					segmentBrulure.Visible = ratioZoneBrulee > 0.001f;
				}
			}
			if (_labelsEtatOsCorps.TryGetValue(section.Cle, out Label lblEtatOs))
			{
				lblEtatOs.Text = $"{etatOs.symbole} {section.Os} (os): {etatOs.etat} ({section.IntegriteOs:F0}/{section.IntegriteOsMax:F0})";
				lblEtatOs.QueueRedraw();
			}
			if (_labelsSanteCorps.TryGetValue(section.Cle, out Label lbl))
				ForcerRafraichissementLabelPv(lbl, FormaterTextePvSection(section));
		}
	}
}
