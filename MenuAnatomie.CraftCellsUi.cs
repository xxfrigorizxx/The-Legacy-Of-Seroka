using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private ProgressBar _barreCuissonPitRoche;
	private ProgressBar _barreCombustionPitRoche;
	private float _accumRafraichPitRoche;

	/// <summary>Barres du feu de camp roche : combustion (cellule combustible, slot 0) et cuisson (cellule cuisson, slot 1).</summary>
	private void RafraichirBarresPitFeuRoche()
	{
		if (_joueurRef == null || GrilleAssemblage == null)
			return;
		bool pitRoche = _joueurRef.StockageRackBatonsOuvert
			&& _joueurRef.RackBatonsOuvert != null
			&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
			&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
		float pComb = pitRoche ? _joueurRef.RackBatonsOuvert.ObtenirProgressionCombustionPitFeuRoche() : -1f;
		float pCuis = pitRoche ? _joueurRef.RackBatonsOuvert.ObtenirProgressionCuissonPitFeuRoche() : -1f;
		MajBarrePitRoche(ref _barreCombustionPitRoche, "BarreCombustionPitRoche", 0, pComb, new Color(0.95f, 0.5f, 0.18f));
		MajBarrePitRoche(ref _barreCuissonPitRoche, "BarreCuissonPitRoche", 1, pCuis, new Color(0.45f, 0.8f, 0.35f));
	}

	private void MajBarrePitRoche(ref ProgressBar barre, string nom, int slotIndex, float progress, Color couleurRemplissage)
	{
		Panel panel = (GrilleAssemblage.GetChildCount() > slotIndex)
			? GrilleAssemblage.GetChild(slotIndex) as Panel
			: null;
		if (panel == null || progress < 0f)
		{
			if (barre != null && GodotObject.IsInstanceValid(barre))
				barre.Visible = false;
			return;
		}
		if (barre == null || !GodotObject.IsInstanceValid(barre) || barre.GetParent() != panel)
		{
			if (barre != null && GodotObject.IsInstanceValid(barre))
				barre.QueueFree();
			barre = new ProgressBar
			{
				Name = nom,
				MinValue = 0,
				MaxValue = 100,
				ShowPercentage = false,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			barre.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
			barre.OffsetLeft = 3f;
			barre.OffsetRight = -3f;
			barre.OffsetTop = -11f;
			barre.OffsetBottom = -3f;
			var fond = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.55f) };
			fond.SetCornerRadiusAll(2);
			var remplissage = new StyleBoxFlat { BgColor = couleurRemplissage };
			remplissage.SetCornerRadiusAll(2);
			barre.AddThemeStyleboxOverride("background", fond);
			barre.AddThemeStyleboxOverride("fill", remplissage);
			panel.AddChild(barre);
		}
		barre.Visible = true;
		barre.Value = progress * 100f;
	}

	private void RafraichirCellulesCraft()
	{
		if (_joueurRef == null || GrilleAssemblage == null) return;
		if (_joueurRef.StockageCoffreOuvert)
			RafraichirCellulesCoffre();
		else
		{
			AssurerPreviewsCraft();
			_joueurRef.VerifierRecettes();
			var gCraft = _joueurRef.ObtenirGrilleCraftAffichee();
			bool pitRoche = _joueurRef.StockageRackBatonsOuvert
				&& _joueurRef.RackBatonsOuvert != null
				&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
				&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
			int nActives = _joueurRef.CraftGrille3x3AuTable ? 9 : 4;
			for (int i = 0; i < 9; i++)
			{
				SlotInventaire s = (gCraft != null && i < nActives && i < gCraft.Length) ? gCraft[i] : default;
				bool vis = _joueurRef.InventaireSlotAunVisuel3D(s);
				bool vpOk = _vpCraft != null && i < _vpCraft.Length && _vpCraft[i] != null && GodotObject.IsInstanceValid(_vpCraft[i]);
				if (vpOk)
				{
					_vpCraft[i].Visible = vis;
					if (_meshPreviewCraft != null && i < _meshPreviewCraft.Length && _meshPreviewCraft[i] != null)
					{
						if (vis)
						{
							ulong em = EmpreinteSlotPourPreviewMenu(s);
							if (_empreinteCraftLast == null || i >= _empreinteCraftLast.Length || em != _empreinteCraftLast[i])
							{
								_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewCraft[i], s);
								if (_empreinteCraftLast != null && i < _empreinteCraftLast.Length)
									_empreinteCraftLast[i] = em;
							}
						}
						else
						{
							if (_empreinteCraftLast != null && i < _empreinteCraftLast.Length)
								_empreinteCraftLast[i] = 0UL;
							_meshPreviewCraft[i].Mesh = null;
							_meshPreviewCraft[i].MaterialOverride = null;
						}
					}
				}
				if (_lblCraft != null && i < _lblCraft.Length && _lblCraft[i] != null)
				{
					string nom = Atlas_Matiere.ObtenirNomObjet(s);
					if (pitRoche && s.EstVide)
					{
						if (i == 0) nom = "Combustible";
						else if (i == 1) nom = "Cuisson";
						else if (i == 2) nom = "Resultat";
					}
					_lblCraft[i].Text = string.IsNullOrEmpty(nom) ? " " : nom;
					_lblCraft[i].Visible = !vis || !vpOk;
				}
				if (GrilleAssemblage.GetChild(i) is Panel panelCase)
					RafraichirQuantiteSlot(panelCase, s);
			}
		}

		ResoudreSlotResultatCraft();
		if (SlotResultatCraft != null)
		{
			bool modeRack = _joueurRef.StockageRackBatonsOuvert;
			bool modeCoffre = _joueurRef.StockageCoffreOuvert;
			bool modeFour = _joueurRef.StockageFourTorchieOuvert;
			SlotResultatCraft.Visible = !modeRack && !modeCoffre && !modeFour;
			if (modeRack || modeCoffre || modeFour)
			{
				_empreinteResultatCraftLast = 0UL;
				if (_meshPreviewResultatCraft != null)
				{
					_meshPreviewResultatCraft.Mesh = null;
					_meshPreviewResultatCraft.MaterialOverride = null;
				}
				if (_lblResultatCraft != null)
					_lblResultatCraft.Visible = false;
				RafraichirBarresPitFeuRoche();
				return;
			}

			if (_vpResultatCraft == null && GodotObject.IsInstanceValid(SlotResultatCraft))
			{
				_meshPreviewResultatCraft = CreerViewportPreviewDansSlot(SlotResultatCraft, "VpResultatCraft", out _vpResultatCraft);
				_lblResultatCraft = TrouverOuCreerLabel(SlotResultatCraft, " ");
			}

			var sRes = _joueurRef.SlotResultatCraft;
			bool visRes = _joueurRef.InventaireSlotAunVisuel3D(sRes);
			bool vpResOk = _vpResultatCraft != null && GodotObject.IsInstanceValid(_vpResultatCraft);

			if (vpResOk)
			{
				_vpResultatCraft.Visible = visRes;
				if (visRes && _meshPreviewResultatCraft != null)
				{
					ulong emRes = EmpreinteSlotPourPreviewMenu(sRes);
					if (emRes != _empreinteResultatCraftLast)
					{
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewResultatCraft, sRes);
						_empreinteResultatCraftLast = emRes;
					}
				}
				else if (_meshPreviewResultatCraft != null)
				{
					_empreinteResultatCraftLast = 0UL;
					_meshPreviewResultatCraft.Mesh = null;
					_meshPreviewResultatCraft.MaterialOverride = null;
				}
			}
			if (_lblResultatCraft != null)
			{
				string nomRes = Atlas_Matiere.ObtenirNomObjet(sRes);
				_lblResultatCraft.Text = string.IsNullOrEmpty(nomRes) ? " " : nomRes;
				_lblResultatCraft.Visible = !visRes || !vpResOk;
			}
			RafraichirQuantiteSlot(SlotResultatCraft, sRes);
		}
		RafraichirBarresPitFeuRoche();
	}

	private Label TrouverOuCreerLabel(Panel parent, string texteDefaut)
	{
		if (parent == null) return null;
		var lbl = parent.GetNodeOrNull<Label>("Label") ?? TrouverLabelEnfant(parent);
		if (lbl == null)
		{
			lbl = new Label
			{
				Name = "Label",
				Text = texteDefaut,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			lbl.AddThemeFontSizeOverride("font_size", 12);
			lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
			lbl.AddThemeConstantOverride("outline_size", 3);
			parent.AddChild(lbl);
		}
		// Stop par défaut : le label recouvre le Panel et bloquait GuiInput (craft + mains).
		lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
		return lbl;
	}

	private void MettreAJourEnteteModeRack()
	{
		if (_joueurRef == null) return;
		ResoudreGrilleAssemblage();
		if (GrilleAssemblage?.GetParent() is not Panel cadre) return;

		if (_lblModeRack == null || !GodotObject.IsInstanceValid(_lblModeRack))
		{
			_lblModeRack = cadre.GetNodeOrNull<Label>("LabelModeRack");
			if (_lblModeRack == null)
			{
				_lblModeRack = new Label
				{
					Name = "LabelModeRack",
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				_lblModeRack.SetAnchorsPreset(Control.LayoutPreset.TopWide);
				_lblModeRack.OffsetLeft = 0;
				_lblModeRack.OffsetRight = 0;
				_lblModeRack.OffsetTop = -28;
				_lblModeRack.OffsetBottom = -6;
				_lblModeRack.AddThemeFontSizeOverride("font_size", 14);
				_lblModeRack.AddThemeColorOverride("font_outline_color", Colors.Black);
				_lblModeRack.AddThemeConstantOverride("outline_size", 3);
				cadre.AddChild(_lblModeRack);
			}
		}

		if (_joueurRef.StockageCoffreOuvert)
		{
			_lblModeRack.Text = "Stockage Coffre en bois  [10 emplacements]";
			_lblModeRack.Visible = true;
		}
		else if (_joueurRef.StockageRackBatonsOuvert)
		{
			int total = _joueurRef.CompterQuantiteRackOuvert();
			int cap = _joueurRef.ObtenirCapaciteRackOuvert();
			bool rackBuches = _joueurRef.RackBatonsOuvert != null
				&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
				&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBuches;
            bool pitRoche = _joueurRef.RackBatonsOuvert != null
                && GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
                && _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
            _lblModeRack.Text = pitRoche
                ? $"Pit roche | Combustible [{total}/{cap}] | Cuisson: slot 2 | Resultat: slot 3"
                : (rackBuches
                    ? $"Stockage Rack a buches  [{total}/{cap}]"
                    : $"Stockage Rack a batons  [{total}/{cap}]");
			_lblModeRack.Visible = true;
		}
		else
		{
			_lblModeRack.Visible = false;
		}
	}

	private Label TrouverOuCreerLabelQuantite(Panel parent)
	{
		if (parent == null) return null;
		var lbl = parent.GetNodeOrNull<Label>("QtyLabel");
		if (lbl == null)
		{
			lbl = new Label
			{
				Name = "QtyLabel",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Visible = false
			};
			lbl.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			lbl.OffsetLeft = -52f;
			lbl.OffsetTop = 2f;
			lbl.OffsetRight = -4f;
			lbl.OffsetBottom = 18f;
			lbl.AddThemeFontSizeOverride("font_size", 12);
			lbl.AddThemeColorOverride("font_color", Colors.White);
			lbl.AddThemeColorOverride("font_outline_color", Colors.Black);
			lbl.AddThemeConstantOverride("outline_size", 2);
			lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
			parent.AddChild(lbl);
		}
		return lbl;
	}

	private void RafraichirQuantiteSlot(Panel panel, SlotInventaire slot)
	{
		if (panel == null) return;
		var lbl = TrouverOuCreerLabelQuantite(panel);
		if (lbl == null) return;
		int q = Joueur.ObtenirQuantiteSlot(slot);
		lbl.Visible = !slot.EstVide && q > 1;
		lbl.Text = lbl.Visible ? $"x{q}" : "";
	}

	private Label TrouverLabelEnfant(Node parent)
	{
		if (parent == null) return null;
		foreach (Node enfant in parent.GetChildren())
		{
			if (enfant is Label lbl) return lbl;
		}
		return null;
	}

	private void DesactiverFocusParasite(Node parent)
	{
		if (parent is Control c)
			c.FocusMode = Control.FocusModeEnum.None;
		foreach (Node enfant in parent.GetChildren())
			DesactiverFocusParasite(enfant);
	}
}
