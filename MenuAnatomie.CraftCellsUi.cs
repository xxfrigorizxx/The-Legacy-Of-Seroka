using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
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
			SlotResultatCraft.Visible = !modeRack && !modeCoffre;
			if (modeRack || modeCoffre)
			{
				_empreinteResultatCraftLast = 0UL;
				if (_meshPreviewResultatCraft != null)
				{
					_meshPreviewResultatCraft.Mesh = null;
					_meshPreviewResultatCraft.MaterialOverride = null;
				}
				if (_lblResultatCraft != null)
					_lblResultatCraft.Visible = false;
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
