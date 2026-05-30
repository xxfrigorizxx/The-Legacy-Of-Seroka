using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void MettreAJourCameraApercuJoueurCorps(float delta)
	{
		if (_cameraApercuJoueurCorps == null || !GodotObject.IsInstanceValid(_cameraApercuJoueurCorps))
			return;
		if (_avatarApercuJoueurCorps == null || !GodotObject.IsInstanceValid(_avatarApercuJoueurCorps))
			return;
		_ = delta;
		Vector3 cible = _avatarApercuJoueurCorps.GlobalPosition + new Vector3(0f, HauteurCibleCameraApercuJoueurCorps, 0f);
		// Cadrage fixe: la caméra reste devant l'avatar (ne suit plus son axe local),
		// ce qui garantit un rendu "face joueur" au lieu d'un profil persistant.
		Vector3 posCam = cible
			+ new Vector3(DecalageLateralCameraApercuJoueurCorps, HauteurCameraApercuJoueurCorps, DistanceCameraApercuJoueurCorps);
		_cameraApercuJoueurCorps.GlobalPosition = posCam;
		_cameraApercuJoueurCorps.LookAt(cible, Vector3.Up);
	}

	private void AssurerInfobulleInventaire()
	{
		if (_panneauInfobulleSlot != null && GodotObject.IsInstanceValid(_panneauInfobulleSlot)) return;
		_panneauInfobulleSlot = new Panel
		{
			Name = "InfobulleNomSlot",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
			ZIndex = 640
		};
		_lblInfobulleSlot = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top
		};
		_lblInfobulleSlot.AddThemeFontSizeOverride("font_size", 13);
		_lblInfobulleSlot.AddThemeColorOverride("font_outline_color", Colors.Black);
		_lblInfobulleSlot.AddThemeConstantOverride("outline_size", 2);
		_lblInfobulleSlot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lblInfobulleSlot.OffsetLeft = 8;
		_lblInfobulleSlot.OffsetTop = 6;
		_lblInfobulleSlot.OffsetRight = -8;
		_lblInfobulleSlot.OffsetBottom = -6;
		_panneauInfobulleSlot.AddChild(_lblInfobulleSlot);
		AddChild(_panneauInfobulleSlot);
		MoveChild(_panneauInfobulleSlot, GetChildCount() - 1);
	}

	private bool TryObtenirSlotSousControleSouris(Control h, out SlotInventaire slot)
	{
		slot = default;
		if (h == null || _joueurRef == null) return false;
		ResoudreReferencesSlotsMains();
		ResoudreGrilleAssemblage();
		ResoudreSlotResultatCraft();

		if (MainGaucheSlot != null && GodotObject.IsInstanceValid(MainGaucheSlot)
			&& (h == MainGaucheSlot || MainGaucheSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.MainGauche;
			return true;
		}
		if (MainDroiteSlot != null && GodotObject.IsInstanceValid(MainDroiteSlot)
			&& (h == MainDroiteSlot || MainDroiteSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.MainDroite;
			return true;
		}
		if (EquipementCorpsSlot != null && GodotObject.IsInstanceValid(EquipementCorpsSlot)
			&& (h == EquipementCorpsSlot || EquipementCorpsSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.EquipementCeinture;
			return true;
		}
		if (EquipementSacSlot != null && GodotObject.IsInstanceValid(EquipementSacSlot)
			&& (h == EquipementSacSlot || EquipementSacSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.EquipementSacDos;
			return true;
		}
		if (CarnetSavoirSlot != null && GodotObject.IsInstanceValid(CarnetSavoirSlot)
			&& (h == CarnetSavoirSlot || CarnetSavoirSlot.IsAncestorOf(h)))
		{
			slot = _joueurRef.EquipementCarnet;
			return true;
		}
		if (SlotResultatCraft != null && GodotObject.IsInstanceValid(SlotResultatCraft)
			&& (h == SlotResultatCraft || SlotResultatCraft.IsAncestorOf(h)))
		{
			slot = _joueurRef.SlotResultatCraft;
			return true;
		}
		if (!_joueurRef.StockageCoffreOuvert && GrilleAssemblage != null && GodotObject.IsInstanceValid(GrilleAssemblage) && GrilleAssemblage.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == GrilleAssemblage && cur is Panel)
				{
					int idx = cur.GetIndex();
					if (!_joueurRef.CraftGrille3x3AuTable && idx >= 4)
						break;
					var g = _joueurRef.ObtenirGrilleCraftAffichee();
					if (g != null && idx >= 0 && idx < g.Length)
					{
						slot = g[idx];
						return true;
					}
					break;
				}
			}
		}
		if (ObtenirGrilleSac() is GridContainer grilleSac && GodotObject.IsInstanceValid(grilleSac) && grilleSac.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == grilleSac && cur is Panel)
				{
					int idx = cur.GetIndex();
					int capSac = Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos);
					if (!_joueurRef.ASacEquipe() || idx < 0 || idx >= capSac) break;
					slot = _joueurRef.RefSlotSac(idx);
					return true;
				}
			}
		}
		if (ObtenirGrilleCeintureStockage() is GridContainer grilleCeint && GodotObject.IsInstanceValid(grilleCeint) && grilleCeint.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == grilleCeint && cur is Panel)
				{
					int idx = cur.GetIndex();
					int capCeinture = Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture);
					if (!_joueurRef.ACeintureSacochesEquipe() || idx < 0 || idx >= capCeinture) break;
					slot = _joueurRef.RefSlotCeintureStockage(idx);
					return true;
				}
			}
		}
		if (ObtenirGrilleCoffreBois() is GridContainer grilleCoffre && GodotObject.IsInstanceValid(grilleCoffre) && grilleCoffre.IsAncestorOf(h))
		{
			for (Control cur = h; cur != null; cur = cur.GetParent() as Control)
			{
				if (cur.GetParent() == grilleCoffre && cur is Panel)
				{
					int idx = cur.GetIndex();
					if (!_joueurRef.StockageCoffreOuvert || idx < 0 || idx > 9) break;
					slot = _joueurRef.RefSlotCoffreStockage(idx);
					return true;
				}
			}
		}
		return false;
	}

	private void MettreAJourInfobulleSourisInventaire()
	{
		if (Engine.IsEditorHint() || _joueurRef == null)
			return;
		AssurerInfobulleInventaire();
		var vp = GetViewport();
		Control h = vp?.GuiGetHoveredControl();
		if (h == null || !TryObtenirSlotSousControleSouris(h, out SlotInventaire sl) || sl.EstVide)
		{
			if (_panneauInfobulleSlot != null)
				_panneauInfobulleSlot.Visible = false;
			return;
		}
		string nom = Atlas_Matiere.ObtenirNomObjet(sl);
		if (string.IsNullOrEmpty(nom))
		{
			_panneauInfobulleSlot.Visible = false;
			return;
		}
		_lblInfobulleSlot.Text = nom;
		const float maxL = 300f;
		Vector2 ms = _lblInfobulleSlot.GetMinimumSize();
		ms.X = Mathf.Min(Mathf.Max(ms.X, 80f), maxL);
		ms.Y = Mathf.Max(ms.Y, 22f);
		_panneauInfobulleSlot.CustomMinimumSize = ms + new Vector2(16f, 12f);
		_panneauInfobulleSlot.Size = _panneauInfobulleSlot.CustomMinimumSize;
		Vector2 posSouris = GetGlobalMousePosition();
		Rect2 vr = GetViewport().GetVisibleRect();
		Vector2 p = posSouris + new Vector2(14f, 18f);
		if (p.X + _panneauInfobulleSlot.Size.X > vr.Position.X + vr.Size.X)
			p.X = posSouris.X - _panneauInfobulleSlot.Size.X - 10f;
		if (p.Y + _panneauInfobulleSlot.Size.Y > vr.Position.Y + vr.Size.Y)
			p.Y = posSouris.Y - _panneauInfobulleSlot.Size.Y - 10f;
		_panneauInfobulleSlot.GlobalPosition = p;
		_panneauInfobulleSlot.Visible = true;
	}

	private void RepositionnerInfobulleSlotSourisSiVisible()
	{
		if (_panneauInfobulleSlot == null || !_panneauInfobulleSlot.Visible)
			return;
		Vector2 posSouris = GetGlobalMousePosition();
		Rect2 vr = GetViewport().GetVisibleRect();
		Vector2 p = posSouris + new Vector2(14f, 18f);
		if (p.X + _panneauInfobulleSlot.Size.X > vr.Position.X + vr.Size.X)
			p.X = posSouris.X - _panneauInfobulleSlot.Size.X - 10f;
		if (p.Y + _panneauInfobulleSlot.Size.Y > vr.Position.Y + vr.Size.Y)
			p.Y = posSouris.Y - _panneauInfobulleSlot.Size.Y - 10f;
		_panneauInfobulleSlot.GlobalPosition = p;
	}
}
