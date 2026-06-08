using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void AssurerPreviewsCraft()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreGrilleAssemblage();
		if (GrilleAssemblage == null) return;
		int nChild = GrilleAssemblage.GetChildCount();
		if (_vpCraft != null && _vpCraft.Length != nChild)
		{
			_vpCraft = null;
			_meshPreviewCraft = null;
			_lblCraft = null;
			_empreinteCraftLast = null;
			_clicsCraftConnectes = false;
			CallDeferred(nameof(ConnecterClicsInventaire));
		}
		if (_vpCraft != null && nChild > 0 && _vpCraft[0] != null && GodotObject.IsInstanceValid(_vpCraft[0]))
		{
			if (_empreinteCraftLast == null || _empreinteCraftLast.Length != nChild)
				_empreinteCraftLast = new ulong[nChild];
			return;
		}
		_meshPreviewCraft = new MeshInstance3D[nChild];
		_vpCraft = new SubViewportContainer[nChild];
		_lblCraft = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (GrilleAssemblage.GetChild(i) is not Panel p) continue;
			_meshPreviewCraft[i] = CreerViewportPreviewDansSlot(p, $"VpCraft{i}", out _vpCraft[i]);
			_lblCraft[i] = TrouverOuCreerLabel(p, " ");
		}
		_empreinteCraftLast = new ulong[nChild];
	}

	private void AssurerPreviewsCoffre()
	{
		if (Engine.IsEditorHint()) return;
		if (ObtenirGrilleCoffreBois() is not GridContainer grille || grille == null) return;
		int nChild = grille.GetChildCount();
		if (_vpCoffre != null && _vpCoffre.Length != nChild)
		{
			_vpCoffre = null;
			_meshPreviewCoffre = null;
			_lblCoffre = null;
			_empreinteCoffreLast = null;
			_clicsGrilleCoffreConnectes = false;
			CallDeferred(nameof(ConnecterClicsInventaire));
		}
		if (_vpCoffre != null && nChild > 0 && _vpCoffre[0] != null && GodotObject.IsInstanceValid(_vpCoffre[0]))
		{
			if (_empreinteCoffreLast == null || _empreinteCoffreLast.Length != nChild)
				_empreinteCoffreLast = new ulong[nChild];
			return;
		}
		_meshPreviewCoffre = new MeshInstance3D[nChild];
		_vpCoffre = new SubViewportContainer[nChild];
		_lblCoffre = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (grille.GetChild(i) is not Panel p) continue;
			_meshPreviewCoffre[i] = CreerViewportPreviewDansSlot(p, $"VpCoffre{i}", out _vpCoffre[i]);
			_lblCoffre[i] = TrouverOuCreerLabel(p, " ");
		}
		_empreinteCoffreLast = new ulong[nChild];
	}

	private void AssurerPreviewsSacStockage()
	{
		if (Engine.IsEditorHint()) return;
		if (ObtenirGrilleSac() is not GridContainer grille || grille == null) return;
		int nChild = grille.GetChildCount();
		if (_vpSacStockage != null && _vpSacStockage.Length != nChild)
		{
			_vpSacStockage = null;
			_meshPreviewSacStockage = null;
			_lblSacStockage = null;
			_empreinteSacStockageLast = null;
		}
		if (_vpSacStockage != null && nChild > 0 && _vpSacStockage[0] != null && GodotObject.IsInstanceValid(_vpSacStockage[0]))
		{
			if (_empreinteSacStockageLast == null || _empreinteSacStockageLast.Length != nChild)
				_empreinteSacStockageLast = new ulong[nChild];
			return;
		}
		_meshPreviewSacStockage = new MeshInstance3D[nChild];
		_vpSacStockage = new SubViewportContainer[nChild];
		_lblSacStockage = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (grille.GetChild(i) is not Panel p) continue;
			_meshPreviewSacStockage[i] = CreerViewportPreviewDansSlot(p, $"VpSacStock{i}", out _vpSacStockage[i]);
			_lblSacStockage[i] = TrouverOuCreerLabel(p, " ");
		}
		_empreinteSacStockageLast = new ulong[nChild];
	}

	private void AssurerPreviewsCeintureStockage()
	{
		if (Engine.IsEditorHint()) return;
		if (ObtenirGrilleCeintureStockage() is not GridContainer grille || grille == null) return;
		int nChild = grille.GetChildCount();
		if (_vpCeintureStockage != null && _vpCeintureStockage.Length != nChild)
		{
			_vpCeintureStockage = null;
			_meshPreviewCeintureStockage = null;
			_lblCeintureStockage = null;
			_empreinteCeintureStockageLast = null;
		}
		if (_vpCeintureStockage != null && nChild > 0 && _vpCeintureStockage[0] != null && GodotObject.IsInstanceValid(_vpCeintureStockage[0]))
		{
			if (_empreinteCeintureStockageLast == null || _empreinteCeintureStockageLast.Length != nChild)
				_empreinteCeintureStockageLast = new ulong[nChild];
			return;
		}
		_meshPreviewCeintureStockage = new MeshInstance3D[nChild];
		_vpCeintureStockage = new SubViewportContainer[nChild];
		_lblCeintureStockage = new Label[nChild];
		for (int i = 0; i < nChild; i++)
		{
			if (grille.GetChild(i) is not Panel p) continue;
			_meshPreviewCeintureStockage[i] = CreerViewportPreviewDansSlot(p, $"VpCeintureStock{i}", out _vpCeintureStockage[i]);
			_lblCeintureStockage[i] = TrouverOuCreerLabel(p, " ");
		}
		_empreinteCeintureStockageLast = new ulong[nChild];
	}

	/// <summary>Modèle 3D dans la case si possible ; texte seulement en secours (objet sans mesh).</summary>
	private void MettreAJourCaseSlotAvecPreview(
		Panel panel,
		SlotInventaire slot,
		SubViewportContainer vp,
		MeshInstance3D meshPreview,
		Label lblFallback,
		ref ulong empreinteSlot)
	{
		if (panel == null || _joueurRef == null) return;
		bool vis3d = _joueurRef.InventaireSlotAunVisuel3D(slot);
		bool vpOk = vp != null && GodotObject.IsInstanceValid(vp);
		if (vpOk)
		{
			vp.Visible = vis3d;
			if (meshPreview != null)
			{
				if (vis3d)
				{
					ulong em = EmpreinteSlotPourPreviewMenu(slot);
					if (em != empreinteSlot)
					{
						empreinteSlot = em;
						_joueurRef.SynchroniserPreviewSlotMenu(meshPreview, slot);
					}
				}
				else
				{
					empreinteSlot = 0UL;
					meshPreview.Mesh = null;
					meshPreview.MaterialOverride = null;
				}
			}
		}
		if (lblFallback != null)
		{
			bool texte = !vis3d || !vpOk;
			lblFallback.Visible = texte;
			if (texte)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(slot);
				lblFallback.Text = slot.EstVide || string.IsNullOrEmpty(nom) ? " " : nom;
			}
		}
		RafraichirQuantiteSlot(panel, slot);
	}

	private void RafraichirGrillesStockageSacEtCeinture()
	{
		if (_joueurRef == null) return;

		if (ObtenirGrilleSac() is GridContainer grilleSac)
		{
			AssurerCapaciteGrillesStockage();
			AssurerPreviewsSacStockage();
			int capSac = Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos);
			bool afficher = _joueurRef.ASacEquipe();
			grilleSac.Visible = afficher;
			grilleSac.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleSac.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			for (int i = 0; i < grilleSac.GetChildCount(); i++)
			{
				if (grilleSac.GetChild(i) is not Panel p) continue;
				bool visCase = afficher && i < capSac;
				p.Visible = visCase;
				var slot = visCase ? _joueurRef.RefSlotSac(i) : new SlotInventaire();
				ulong em = 0UL;
				if (_empreinteSacStockageLast != null && i < _empreinteSacStockageLast.Length)
					em = _empreinteSacStockageLast[i];
				SubViewportContainer vp = _vpSacStockage != null && i < _vpSacStockage.Length ? _vpSacStockage[i] : null;
				MeshInstance3D mesh = _meshPreviewSacStockage != null && i < _meshPreviewSacStockage.Length ? _meshPreviewSacStockage[i] : null;
				Label lbl = _lblSacStockage != null && i < _lblSacStockage.Length ? _lblSacStockage[i] : null;
				MettreAJourCaseSlotAvecPreview(p, slot, vp, mesh, lbl, ref em);
				if (_empreinteSacStockageLast != null && i < _empreinteSacStockageLast.Length)
					_empreinteSacStockageLast[i] = em;
			}
		}

		if (ObtenirGrilleCeintureStockage() is GridContainer grilleCeintSt)
		{
			AssurerCapaciteGrillesStockage();
			AssurerPreviewsCeintureStockage();
			int capCeinture = Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture);
			bool afficherC = _joueurRef.ACeintureSacochesEquipe();
			grilleCeintSt.Visible = afficherC;
			grilleCeintSt.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grilleCeintSt.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			for (int i = 0; i < grilleCeintSt.GetChildCount(); i++)
			{
				if (grilleCeintSt.GetChild(i) is not Panel p) continue;
				bool visCase = afficherC && i < capCeinture;
				p.Visible = visCase;
				var slot = visCase ? _joueurRef.RefSlotCeintureStockage(i) : new SlotInventaire();
				ulong em = 0UL;
				if (_empreinteCeintureStockageLast != null && i < _empreinteCeintureStockageLast.Length)
					em = _empreinteCeintureStockageLast[i];
				SubViewportContainer vp = _vpCeintureStockage != null && i < _vpCeintureStockage.Length ? _vpCeintureStockage[i] : null;
				MeshInstance3D mesh = _meshPreviewCeintureStockage != null && i < _meshPreviewCeintureStockage.Length ? _meshPreviewCeintureStockage[i] : null;
				Label lbl = _lblCeintureStockage != null && i < _lblCeintureStockage.Length ? _lblCeintureStockage[i] : null;
				MettreAJourCaseSlotAvecPreview(p, slot, vp, mesh, lbl, ref em);
				if (_empreinteCeintureStockageLast != null && i < _empreinteCeintureStockageLast.Length)
					_empreinteCeintureStockageLast[i] = em;
			}
		}
	}

	private void RafraichirCellulesCoffre()
	{
		if (_joueurRef == null || !_joueurRef.StockageCoffreOuvert) return;
		if (ObtenirGrilleCoffreBois() is not GridContainer grille || grille == null) return;
		AssurerPreviewsCoffre();
		for (int i = 0; i < 10 && i < grille.GetChildCount(); i++)
		{
			ref SlotInventaire s = ref _joueurRef.RefSlotCoffreStockage(i);
			bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
			bool vpOk = _vpCoffre != null && i < _vpCoffre.Length && _vpCoffre[i] != null && GodotObject.IsInstanceValid(_vpCoffre[i]);
			if (vpOk)
			{
				_vpCoffre[i].Visible = vis;
				if (_meshPreviewCoffre != null && i < _meshPreviewCoffre.Length && _meshPreviewCoffre[i] != null)
				{
					if (vis)
					{
						ulong em = EmpreinteSlotPourPreviewMenu(s);
						if (_empreinteCoffreLast == null || i >= _empreinteCoffreLast.Length || em != _empreinteCoffreLast[i])
						{
							_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewCoffre[i], s);
							if (_empreinteCoffreLast != null && i < _empreinteCoffreLast.Length)
								_empreinteCoffreLast[i] = em;
						}
					}
					else
					{
						if (_empreinteCoffreLast != null && i < _empreinteCoffreLast.Length)
							_empreinteCoffreLast[i] = 0UL;
						_meshPreviewCoffre[i].Mesh = null;
						_meshPreviewCoffre[i].MaterialOverride = null;
					}
				}
			}
			if (_lblCoffre != null && i < _lblCoffre.Length && _lblCoffre[i] != null)
			{
				string nom = Atlas_Matiere.ObtenirNomObjet(s);
				_lblCoffre[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
				_lblCoffre[i].Visible = !vis || !vpOk;
			}
			if (grille.GetChild(i) is Panel panelCase)
				RafraichirQuantiteSlot(panelCase, s);
		}
	}

	private void AssurerApercuFlottantCurseur()
	{
		if (Engine.IsEditorHint() || _conteneurFlottantCurseur != null) return;
		_conteneurFlottantCurseur = new Panel
		{
			Name = "FlottantCurseurInventaire",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(96, 118)
		};
		_conteneurFlottantCurseur.Size = _conteneurFlottantCurseur.CustomMinimumSize;
		_conteneurFlottantCurseur.ZIndex = 512;

		var vbox = new VBoxContainer
		{
			Name = "VBoxCurseur",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = vbox.OffsetTop = 4;
		vbox.OffsetRight = vbox.OffsetBottom = -4;
		_conteneurFlottantCurseur.AddChild(vbox);

		var cadreVp = new Panel
		{
			CustomMinimumSize = new Vector2(88, 88),
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vbox.AddChild(cadreVp);
		_meshCurseurSouris = CreerViewportPreviewDansSlot(cadreVp, "VpCurseurSouris", out _vpCurseurSouris);
		_lblCurseurQuantite = TrouverOuCreerLabelQuantite(cadreVp);

		_lblCurseurSouris = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_lblCurseurSouris.AddThemeFontSizeOverride("font_size", 11);
		_lblCurseurSouris.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		_lblCurseurSouris.AddThemeConstantOverride("outline_size", 2);
		vbox.AddChild(_lblCurseurSouris);

		AddChild(_conteneurFlottantCurseur);
		MoveChild(_conteneurFlottantCurseur, GetChildCount() - 1);
		_conteneurFlottantCurseur.Visible = false;
	}

	private void RafraichirAffichageCurseurSouris()
	{
		if (Engine.IsEditorHint() || _joueurRef == null) return;
		AssurerApercuFlottantCurseur();
		bool ecranAvecCurseurFlottant = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire
			|| _ecranBarreCourant == ModeEcranBarreMenu.Analyseur;
		bool montre = EstOuvert && !_curseurMenu.EstVide && ecranAvecCurseurFlottant;
		_conteneurFlottantCurseur.Visible = montre;
		if (!montre) return;

		bool vis = _joueurRef.InventaireSlotAunVisuel3D(_curseurMenu);
		bool vpOk = _vpCurseurSouris != null && GodotObject.IsInstanceValid(_vpCurseurSouris);
		if (vpOk)
		{
			_vpCurseurSouris.Visible = vis;
			if (vis && _meshCurseurSouris != null)
				_joueurRef.SynchroniserPreviewSlotMenu(_meshCurseurSouris, _curseurMenu);
			else if (_meshCurseurSouris != null)
			{
				_meshCurseurSouris.Mesh = null;
				_meshCurseurSouris.MaterialOverride = null;
			}
		}
		if (_lblCurseurSouris != null)
		{
			string nom = Atlas_Matiere.ObtenirNomObjet(_curseurMenu);
			_lblCurseurSouris.Text = string.IsNullOrEmpty(nom) ? " " : nom;
			_lblCurseurSouris.Visible = !vis || !vpOk;
		}
		if (_lblCurseurQuantite != null)
		{
			int q = Joueur.ObtenirQuantiteSlot(_curseurMenu);
			_lblCurseurQuantite.Visible = q > 1;
			_lblCurseurQuantite.Text = q > 1 ? $"x{q}" : "";
		}
		_conteneurFlottantCurseur.GlobalPosition = GetGlobalMousePosition() - _conteneurFlottantCurseur.Size * 0.5f;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || !EstOuvert)
			return;
		bool ecranInventaire = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire;
		bool ecranAnalyseur = _ecranBarreCourant == ModeEcranBarreMenu.Analyseur;
		if (!ecranInventaire && !ecranAnalyseur)
			return;

		if (ecranInventaire)
		{
			_accumulateurInfobulleInventaire += (float)delta;
			if (_accumulateurInfobulleInventaire >= IntervalleInfobulleInventaireSec)
			{
				_accumulateurInfobulleInventaire = 0f;
				MettreAJourInfobulleSourisInventaire();
			}
			else
				RepositionnerInfobulleSlotSourisSiVisible();

			RafraichirPanneauSanteCorps(inclureAvatar: false);
			RafraichirBarresPitFeuRoche();
			// Feu de camp roche ouvert : rafraîchir le contenu des cellules en continu (steak cuit qui apparaît,
			// stock de combustible qui descend) — sinon le résultat n'apparaissait qu'au prochain clic.
			if (_joueurRef != null && _joueurRef.RackOuvertEstPitFeuRoche())
			{
				_accumRafraichPitRoche += (float)delta;
				if (_accumRafraichPitRoche >= 0.35f)
				{
					_accumRafraichPitRoche = 0f;
					RafraichirCellulesCraft();
				}
			}
			if (_joueurRef != null && _joueurRef.StockageFourTorchieOuvert)
			{
				_accumRafraichFourTorchie += (float)delta;
				if (_accumRafraichFourTorchie >= 0.35f)
				{
					_accumRafraichFourTorchie = 0f;
					RafraichirCellulesFourTorchie();
				}
			}
			_compteurFrameMenuProcess++;
			if ((_compteurFrameMenuProcess & 1) == 0)
			{
				RafraichirAvatarApercuJoueurCorps();
				MettreAJourCameraApercuJoueurCorps((float)delta);
			}
		}

		if (_conteneurFlottantCurseur != null && _conteneurFlottantCurseur.Visible)
		{
			Vector2 demi = _conteneurFlottantCurseur.Size * 0.5f;
			_conteneurFlottantCurseur.GlobalPosition = GetGlobalMousePosition() - demi;
		}
	}
}
