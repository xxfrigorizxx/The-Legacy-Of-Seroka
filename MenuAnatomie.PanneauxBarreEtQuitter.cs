using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void ConfigurerBarreOngletsJeu()
	{
		if (Engine.IsEditorHint() || _barreOngletsJeuConfiguree) return;
		var barre = GetNodeOrNull<HBoxContainer>(CheminBarreOnglets) ?? FindChild("BarreOnglets", true, false) as HBoxContainer;
		if (barre == null) return;
		_barreOngletsJeuConfiguree = true;
		foreach (Node enfant in barre.GetChildren())
		{
			if (enfant is not Panel pan) continue;
			string nom = pan.Name;
			if (nom == "Onglet0")
			{
				_ongletInventaireBarre = pan;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lInv)
				{
					lInv.Text = "Inventaire";
					lInv.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletInventaireBarre;
			}
			else if (nom == "Onglet1")
			{
				_ongletFutureStateBarre = pan;
				pan.Visible = true;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lFuture)
				{
					lFuture.Text = "Future States";
					lFuture.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletFutureStateBarre;
			}
			else if (nom == "Onglet2")
			{
				_ongletMetierBarre = pan;
				pan.Visible = true;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lMetier)
				{
					lMetier.Text = "Metiers";
					lMetier.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletMetierBarre;
			}
			else if (nom == "Onglet3")
			{
				_ongletAnalyseurBarre = pan;
				pan.Visible = true;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lAna)
				{
					lAna.Text = "Analyseur";
					lAna.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletAnalyseurBarre;
			}
			else if (nom == "Onglet4")
			{
				_ongletCreatifBarre = pan;
				pan.Visible = _joueurRef != null && _joueurRef.ModeCreatifActif;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lCreatif)
				{
					lCreatif.Text = "Creatif/Admin";
					lCreatif.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletCreatifAdminBarre;
			}
			else if (nom == "Onglet11")
			{
				_ongletQuitterBarre = pan;
				pan.MouseFilter = Control.MouseFilterEnum.Stop;
				if (pan.GetNodeOrNull<Label>("Label") is Label lQuit)
				{
					lQuit.Text = "Sauvegarder / Quitter";
					lQuit.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				pan.GuiInput += _OnOngletQuitterJeuBarre;
			}
			else if (nom.ToString().StartsWith("Onglet", StringComparison.Ordinal))
				pan.Visible = false;
		}
		AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
	}

	private Gestionnaire_Monde ObtenirGestionnaireMonde()
	{
		if (_joueurRef == null) return null;
		Node parent = _joueurRef.GetParent();
		return parent?.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
	}

	private void AssurerPanneauSauvegarderQuitter()
	{
		if (Engine.IsEditorHint()) return;
		var vbox = GetNodeOrNull<VBoxContainer>(CheminVBoxPrincipal) ?? FindChild("VBoxPrincipal", true, false) as VBoxContainer;
		if (vbox == null) return;
		_corpsHBoxRef ??= GetNodeOrNull<HBoxContainer>(CheminCorpsHBox) ?? vbox.GetNodeOrNull<HBoxContainer>("CorpsHBox");
		if (_panneauSauvegarderQuitter != null) return;

		_panneauSauvegarderQuitter = new Panel
		{
			Name = "PanneauSauvegarderQuitter",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_panneauSauvegarderQuitter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_panneauSauvegarderQuitter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centre.OffsetLeft = centre.OffsetTop = 8;
		centre.OffsetRight = centre.OffsetBottom = -8;
		_panneauSauvegarderQuitter.AddChild(centre);

		var col = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		col.AddThemeConstantOverride("separation", 16);
		centre.AddChild(col);

		var titre = new Label
		{
			Text = "Sauvegarder ou quitter",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		titre.AddThemeFontSizeOverride("font_size", 18);
		col.AddChild(titre);

		var btnSauve = new Button { Text = "Sauvegarder", CustomMinimumSize = new Vector2(220, 40) };
		btnSauve.Pressed += () => ObtenirGestionnaireMonde()?.SauvegarderManuelDepuisMenu();
		col.AddChild(btnSauve);

		var btnQuit = new Button { Text = "Quitter le jeu", CustomMinimumSize = new Vector2(220, 40) };
		btnQuit.Pressed += () =>
		{
			ObtenirGestionnaireMonde()?.SauvegarderManuelDepuisMenu();
			GetTree().Quit();
		};
		col.AddChild(btnQuit);

		vbox.AddChild(_panneauSauvegarderQuitter);
	}
}
