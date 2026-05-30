using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void AssurerPanneauAnalyseur()
	{
		if (Engine.IsEditorHint()) return;
		var vbox = GetNodeOrNull<VBoxContainer>(CheminVBoxPrincipal) ?? FindChild("VBoxPrincipal", true, false) as VBoxContainer;
		if (vbox == null) return;
		if (_panneauAnalyseur != null) return;

		_panneauAnalyseur = new Panel
		{
			Name = "PanneauAnalyseurManuel",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_panneauAnalyseur.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_panneauAnalyseur.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		_panneauAnalyseur.CustomMinimumSize = new Vector2(0f, 220f);

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centre.OffsetLeft = centre.OffsetTop = 8;
		centre.OffsetRight = centre.OffsetBottom = -8;
		_panneauAnalyseur.AddChild(centre);

		var col = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(560f, 360f)
		};
		col.AddThemeConstantOverride("separation", 12);
		centre.AddChild(col);
		_colAnalyseurContenu = col;

		_lblAnalyseurTitre = new Label
		{
			Text = "Analyseur manuel",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblAnalyseurTitre.AddThemeFontSizeOverride("font_size", 20);
		col.AddChild(_lblAnalyseurTitre);

		_lblAnalyseurAide = new Label
		{
			Text = "Dépose des objets. L'analyse consomme ce que tu as mis.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblAnalyseurAide.AddThemeFontSizeOverride("font_size", 13);
		col.AddChild(_lblAnalyseurAide);

		_grilleAnalyseur = new GridContainer
		{
			Name = "GrilleAnalyseur",
			Columns = Joueur.CapaciteAnalyseurTableTier1,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_grilleAnalyseur.AddThemeConstantOverride("h_separation", 12);
		_grilleAnalyseur.AddThemeConstantOverride("v_separation", 12);
		col.AddChild(_grilleAnalyseur);

		_slotsAnalyseur = new Panel[Joueur.CapaciteAnalyseurTableTier1];
		for (int i = 0; i < Joueur.CapaciteAnalyseurTableTier1; i++)
		{
			var slot = new Panel
			{
				Name = $"AnalyseurSlot{i}",
				CustomMinimumSize = new Vector2(80f, 80f),
				MouseFilter = Control.MouseFilterEnum.Stop
			};
			_grilleAnalyseur.AddChild(slot);
			_slotsAnalyseur[i] = slot;
		}

		_btnAnalyser = new Button
		{
			Name = "BtnAnalyser",
			Text = "Analyser",
			CustomMinimumSize = new Vector2(220f, 40f)
		};
		_btnAnalyser.Pressed += () =>
		{
			if (_joueurRef == null) return;
			_joueurRef.EssayerAnalyserCrafts(out string msgAnalyse);
			RafraichirLabelChanceAnalyseur();
			if (_lblAnalyseurMessage != null)
				_lblAnalyseurMessage.Text = ConstruireTexteEtatAnalyseur(string.IsNullOrEmpty(msgAnalyse) ? _joueurRef.ObtenirMessageAnalyseurActif() : msgAnalyse);
			GetViewport()?.SetInputAsHandled();
			_joueurRef.RafraichirHUD();
			RafraichirMenu();
		};
		col.AddChild(_btnAnalyser);

		_lblAnalyseurChance = new Label
		{
			Name = "AnalyseurChance",
			Text = "Chance de réussite: --,--%",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_lblAnalyseurChance.AddThemeFontSizeOverride("font_size", 14);
		_lblAnalyseurChance.AddThemeColorOverride("font_color", new Color(0.90f, 0.95f, 1.00f));
		col.AddChild(_lblAnalyseurChance);

		_lblAnalyseurMessage = new Label
		{
			Name = "AnalyseurMessage",
			Text = "Dépose des objets puis clique sur Analyser.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(560f, 80f)
		};
		_lblAnalyseurMessage.AddThemeFontSizeOverride("font_size", 13);
		_lblAnalyseurMessage.AddThemeColorOverride("font_outline_color", Colors.Black);
		_lblAnalyseurMessage.AddThemeConstantOverride("outline_size", 2);
		col.AddChild(_lblAnalyseurMessage);

		vbox.AddChild(_panneauAnalyseur);
	}

	private void AssurerPreviewsAnalyseur()
	{
		if (Engine.IsEditorHint()) return;
		if (_slotsAnalyseur == null || _slotsAnalyseur.Length == 0)
			return;
		if (_vpAnalyseur != null && _vpAnalyseur.Length == _slotsAnalyseur.Length && _vpAnalyseur[0] != null && GodotObject.IsInstanceValid(_vpAnalyseur[0]))
		{
			if (_empreinteAnalyseurLast == null || _empreinteAnalyseurLast.Length != _slotsAnalyseur.Length)
				_empreinteAnalyseurLast = new ulong[_slotsAnalyseur.Length];
			return;
		}
		_vpAnalyseur = new SubViewportContainer[_slotsAnalyseur.Length];
		_meshPreviewAnalyseur = new MeshInstance3D[_slotsAnalyseur.Length];
		_lblAnalyseur = new Label[_slotsAnalyseur.Length];
		for (int i = 0; i < _slotsAnalyseur.Length; i++)
		{
			if (_slotsAnalyseur[i] == null) continue;
			_meshPreviewAnalyseur[i] = CreerViewportPreviewDansSlot(_slotsAnalyseur[i], $"VpAnalyseur{i}", out _vpAnalyseur[i]);
			_lblAnalyseur[i] = TrouverOuCreerLabel(_slotsAnalyseur[i], " ");
		}
		_empreinteAnalyseurLast = new ulong[_slotsAnalyseur.Length];
	}

	private void RafraichirPanneauAnalyseur()
	{
		if (_joueurRef == null) return;
		AssurerPanneauAnalyseur();
		AssurerPreviewsAnalyseur();
		RafraichirLabelChanceAnalyseur();
		int capaciteActive = _joueurRef.ObtenirCapaciteAnalyseurActif();
		SlotInventaire[] grilleAnalyse = _joueurRef.ObtenirGrilleAnalyseurActif();
		bool estTier1 = _joueurRef.AnalyseurTier1Actif;
		if (_grilleAnalyseur != null)
		{
			_grilleAnalyseur.Columns = capaciteActive;
			_grilleAnalyseur.AddThemeConstantOverride("h_separation", estTier1 ? 8 : 12);
			_grilleAnalyseur.AddThemeConstantOverride("v_separation", estTier1 ? 8 : 12);
		}
		if (_colAnalyseurContenu != null && GodotObject.IsInstanceValid(_colAnalyseurContenu))
			_colAnalyseurContenu.CustomMinimumSize = estTier1 ? new Vector2(860f, 380f) : new Vector2(560f, 360f);
		if (_lblAnalyseurTitre != null)
			_lblAnalyseurTitre.Text = estTier1 ? "Table d'analyse tier 1" : "Analyseur manuel";
		if (_lblAnalyseurAide != null)
			_lblAnalyseurAide.Text = estTier1
				? "Dépose des objets. L'analyse T1 consomme ce que tu as mis. Les recettes des tiers inférieurs restent déblocables."
				: "Dépose des objets. L'analyse consomme ce que tu as mis.";
		if (_lblAnalyseurMessage != null)
		{
			_lblAnalyseurMessage.CustomMinimumSize = estTier1 ? new Vector2(860f, 80f) : new Vector2(560f, 80f);
			_lblAnalyseurMessage.Text = ConstruireTexteEtatAnalyseur(string.IsNullOrEmpty(_joueurRef.ObtenirMessageAnalyseurActif())
				? "Dépose des objets puis clique sur Analyser."
				: _joueurRef.ObtenirMessageAnalyseurActif());
		}
		if (_slotsAnalyseur == null) return;
		for (int i = 0; i < _slotsAnalyseur.Length; i++)
		{
			Panel panel = _slotsAnalyseur[i];
			if (panel == null) continue;
			panel.Visible = i < capaciteActive;
			SlotInventaire s = (grilleAnalyse != null && i < grilleAnalyse.Length) ? grilleAnalyse[i] : default;
			bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
			bool vpOk = _vpAnalyseur != null && i < _vpAnalyseur.Length && _vpAnalyseur[i] != null && GodotObject.IsInstanceValid(_vpAnalyseur[i]);
			if (vpOk)
			{
				_vpAnalyseur[i].Visible = panel.Visible && vis;
				if (vis && _meshPreviewAnalyseur != null && i < _meshPreviewAnalyseur.Length && _meshPreviewAnalyseur[i] != null)
				{
					ulong em = EmpreinteSlotPourPreviewMenu(s);
					if (_empreinteAnalyseurLast == null || i >= _empreinteAnalyseurLast.Length || em != _empreinteAnalyseurLast[i])
					{
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewAnalyseur[i], s);
						if (_empreinteAnalyseurLast != null && i < _empreinteAnalyseurLast.Length)
							_empreinteAnalyseurLast[i] = em;
					}
				}
				else if (_meshPreviewAnalyseur != null && i < _meshPreviewAnalyseur.Length && _meshPreviewAnalyseur[i] != null)
				{
					_meshPreviewAnalyseur[i].Mesh = null;
					_meshPreviewAnalyseur[i].MaterialOverride = null;
					if (_empreinteAnalyseurLast != null && i < _empreinteAnalyseurLast.Length)
						_empreinteAnalyseurLast[i] = 0UL;
				}
			}
			if (_lblAnalyseur != null && i < _lblAnalyseur.Length && _lblAnalyseur[i] != null)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(s);
				_lblAnalyseur[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
				_lblAnalyseur[i].Visible = !vis || !vpOk;
			}
			RafraichirQuantiteSlot(panel, s);
		}
	}

	private string ConstruireTexteEtatAnalyseur(string messagePrincipal)
	{
		if (_joueurRef == null)
			return messagePrincipal ?? "";
		string regle = "Règle: base 50% + 0,01% par point d'Intelligence autour de 10.";
		if (string.IsNullOrWhiteSpace(messagePrincipal))
			return regle;
		return $"{regle}\n\n{messagePrincipal}";
	}

	private void RafraichirLabelChanceAnalyseur()
	{
		if (_lblAnalyseurChance == null || _joueurRef == null)
			return;
		float chance = Mathf.Clamp(_joueurRef.ObtenirChanceReussiteAnalyseManuelle() * 100f, 0f, 100f);
		_lblAnalyseurChance.Text = $"Chance de réussite: {chance:F2}%";
	}

	private void RestituerGrilleAnalyseurAvantFermeture()
	{
		if (_joueurRef == null) return;
		SlotInventaire[] grilleAnalyse = _joueurRef.ObtenirGrilleAnalyseurActif();
		if (grilleAnalyse == null) return;
		for (int i = 0; i < grilleAnalyse.Length; i++)
		{
			SlotInventaire s = grilleAnalyse[i];
			if (s.EstVide) continue;
			if (!_joueurRef.EssayerRangerSlotInventaireOuStockage(ref s) && !s.EstVide)
				_joueurRef.DeposerSlotAuSolDepuisMenu(s);
			grilleAnalyse[i] = new SlotInventaire();
		}
		_joueurRef.FermerAnalyseurActif();
	}

	public void OuvrirAnalyseurDepuisMonde(bool tier1, ItemPhysique tableAnalyseTier1)
	{
		if (_joueurRef == null)
			return;
		if (tier1)
			_joueurRef.OuvrirAnalyseurTier1(tableAnalyseTier1);
		else
			_joueurRef.OuvrirAnalyseurManuel();

		if (!EstOuvert)
			BasculerVisibilite();
		AppliquerEcranBarre(ModeEcranBarreMenu.Analyseur);
		RafraichirMenu();
	}
}
