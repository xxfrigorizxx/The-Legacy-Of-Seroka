using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	public void BasculerVisibilite()
	{
		EstOuvert = !EstOuvert;
		Visible = EstOuvert;

		if (!EstOuvert)
		{
			ResoudreCurseurAvantFermeture();
			RestituerGrilleAnalyseurAvantFermeture();
			if (_joueurRef != null)
			{
				if (_joueurRef.FourTorchieOuvert != null && GodotObject.IsInstanceValid(_joueurRef.FourTorchieOuvert))
					_joueurRef.FourTorchieOuvert.SynchroniserFourTorchieDepuisGrille();
				_joueurRef.CraftGrille3x3AuTable = false;
				_joueurRef.AtelierPlanTravailOuvert = null;
				_joueurRef.StockageRackBatonsOuvert = false;
				_joueurRef.RackBatonsOuvert = null;
				_joueurRef.StockageCoffreOuvert = false;
				_joueurRef.CoffreOuvert = null;
				_joueurRef.StockageFourTorchieOuvert = false;
				_joueurRef.FourTorchieOuvert = null;
			}
		}

		if (!EstOuvert && _panneauInfobulleSlot != null)
			_panneauInfobulleSlot.Visible = false;

		Input.MouseMode = EstOuvert ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		if (!Engine.IsEditorHint())
			SetProcess(EstOuvert);

		if (EstOuvert)
		{
			CallDeferred(nameof(RemplirParentOuViewport));
			CallDeferred(nameof(AppliquerAncresContenu));
			CallDeferred(nameof(ConnecterClicsInventaire));
			if (!Engine.IsEditorHint())
				AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
			RafraichirMenu();
			CallDeferred(nameof(RafraichirMenu));
		}
	}

	// Cette fonction lit les données du Joueur et les affiche dans l'UI
	public void RafraichirMenu()
	{
		if (_joueurRef == null) return;
		MettreAJourVisibiliteLigneCraftVersusCoffre();
		RafraichirPanneauSanteCorps();
		RafraichirAvatarApercuJoueurCorps();
		ResoudreReferencesSlotsMains();
		if (_lblMainGauche == null) _lblMainGauche = TrouverOuCreerLabel(MainGaucheSlot, "Main G\n[Vide]");
		if (_lblMainDroite == null) _lblMainDroite = TrouverOuCreerLabel(MainDroiteSlot, "Main D\n[Vide]");
		AssurerPreviews3DMains();

		bool visG = _joueurRef.InventaireSlotAunVisuel3D(_joueurRef.MainGauche);
		bool visD = _joueurRef.InventaireSlotAunVisuel3D(_joueurRef.MainDroite);
		bool previewGOk = _vpMenuGauche != null && GodotObject.IsInstanceValid(_vpMenuGauche);
		bool previewDOk = _vpMenuDroite != null && GodotObject.IsInstanceValid(_vpMenuDroite);

		if (previewGOk)
		{
			_vpMenuGauche.Visible = visG;
			if (_meshPreviewMenuG != null)
			{
				ulong emG = EmpreinteSlotPourPreviewMenu(_joueurRef.MainGauche);
				if (emG != _empreinteMainGLast)
				{
					_empreinteMainGLast = emG;
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuG, _joueurRef.MainGauche);
				}
			}
		}
		if (_lblMainGauche != null)
		{
			bool montrerTexteG = !visG || !previewGOk;
			_lblMainGauche.Visible = montrerTexteG;
			string nomG = Atlas_Matiere.ObtenirNomObjet(_joueurRef.MainGauche);
			_lblMainGauche.Text = montrerTexteG
				? (string.IsNullOrEmpty(nomG) ? " " : nomG)
				: " ";
		}
		RafraichirQuantiteSlot(MainGaucheSlot, _joueurRef.MainGauche);
		AppliquerBordureActive(MainGaucheSlot, _joueurRef.MainGaucheEstActive);

		if (previewDOk)
		{
			_vpMenuDroite.Visible = visD;
			if (_meshPreviewMenuD != null)
			{
				ulong emD = EmpreinteSlotPourPreviewMenu(_joueurRef.MainDroite);
				if (emD != _empreinteMainDLast)
				{
					_empreinteMainDLast = emD;
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuD, _joueurRef.MainDroite);
				}
			}
		}
		if (_lblMainDroite != null)
		{
			bool montrerTexteD = !visD || !previewDOk;
			_lblMainDroite.Visible = montrerTexteD;
			string nomD = Atlas_Matiere.ObtenirNomObjet(_joueurRef.MainDroite);
			_lblMainDroite.Text = montrerTexteD
				? (string.IsNullOrEmpty(nomD) ? " " : nomD)
				: " ";
		}
		RafraichirQuantiteSlot(MainDroiteSlot, _joueurRef.MainDroite);
		AppliquerBordureActive(MainDroiteSlot, !_joueurRef.MainGaucheEstActive);

		ResoudreReferencesSlotsMains();
		AssurerPreviews3DMains();
		if (EquipementCorpsSlot != null && _meshPreviewMenuCeinture != null && GodotObject.IsInstanceValid(_meshPreviewMenuCeinture))
		{
			var eqC = _joueurRef.EquipementCeinture;
			bool visC = _joueurRef.InventaireSlotAunVisuel3D(eqC);
			bool vpCOk = _vpMenuCeinture != null && GodotObject.IsInstanceValid(_vpMenuCeinture);
			if (vpCOk)
			{
				_vpMenuCeinture.Visible = visC;
				if (visC)
				{
					ulong emC = EmpreinteSlotPourPreviewMenu(eqC);
					if (emC != _empreinteCeintureLast)
					{
						_empreinteCeintureLast = emC;
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuCeinture, eqC);
					}
				}
				else
				{
					_empreinteCeintureLast = 0UL;
					_meshPreviewMenuCeinture.Mesh = null;
					_meshPreviewMenuCeinture.MaterialOverride = null;
				}
			}
			if (_lblSlotCeinture != null)
			{
				bool montrerTexte = !visC || !vpCOk;
				_lblSlotCeinture.Visible = montrerTexte;
				string nomC = Atlas_Matiere.ObtenirNomObjet(eqC);
				_lblSlotCeinture.Text = string.IsNullOrEmpty(nomC) ? " " : nomC;
			}
			RafraichirQuantiteSlot(EquipementCorpsSlot, eqC);
		}
		if (EquipementSacSlot != null && _meshPreviewMenuSacEquip != null && GodotObject.IsInstanceValid(_meshPreviewMenuSacEquip))
		{
			var eqS = _joueurRef.EquipementSacDos;
			bool visS = _joueurRef.InventaireSlotAunVisuel3D(eqS);
			bool vpSOk = _vpMenuSacEquip != null && GodotObject.IsInstanceValid(_vpMenuSacEquip);
			if (vpSOk)
			{
				_vpMenuSacEquip.Visible = visS;
				if (visS)
				{
					ulong emS = EmpreinteSlotPourPreviewMenu(eqS);
					if (emS != _empreinteSacLast)
					{
						_empreinteSacLast = emS;
						_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuSacEquip, eqS);
					}
				}
				else
				{
					_empreinteSacLast = 0UL;
					_meshPreviewMenuSacEquip.Mesh = null;
					_meshPreviewMenuSacEquip.MaterialOverride = null;
				}
			}
			if (_lblSlotSacEquip != null)
			{
				bool montrerTexte = !visS || !vpSOk;
				_lblSlotSacEquip.Visible = montrerTexte;
				string nomS = Atlas_Matiere.ObtenirNomObjet(eqS);
				_lblSlotSacEquip.Text = string.IsNullOrEmpty(nomS) ? " " : nomS;
			}
			RafraichirQuantiteSlot(EquipementSacSlot, eqS);
		}
		if (CarnetSavoirSlot != null && _meshPreviewMenuCarnet != null && GodotObject.IsInstanceValid(_meshPreviewMenuCarnet))
		{
			var eqK = _joueurRef.EquipementCarnet;
			bool visK = _joueurRef.InventaireSlotAunVisuel3D(eqK);
			bool vpKOk = _vpMenuCarnet != null && GodotObject.IsInstanceValid(_vpMenuCarnet);
			if (vpKOk)
			{
				_vpMenuCarnet.Visible = visK;
				if (visK)
					_joueurRef.SynchroniserPreviewSlotMenu(_meshPreviewMenuCarnet, eqK);
				else
				{
					_meshPreviewMenuCarnet.Mesh = null;
					_meshPreviewMenuCarnet.MaterialOverride = null;
				}
			}
			if (_lblSlotCarnet != null)
			{
				bool montrerTexte = !visK || !vpKOk;
				_lblSlotCarnet.Visible = montrerTexte;
				string nomK = Atlas_Matiere.ObtenirNomObjet(eqK);
				_lblSlotCarnet.Text = string.IsNullOrEmpty(nomK) ? " " : nomK;
			}
			RafraichirQuantiteSlot(CarnetSavoirSlot, eqK);
		}

		AppliquerDispositionGrilleCraft();
		MettreAJourEnteteModeRack();
		if (_ecranBarreCourant == ModeEcranBarreMenu.Analyseur)
			RafraichirPanneauAnalyseur();
		else if (_ecranBarreCourant == ModeEcranBarreMenu.CreatifAdmin)
		{
			_creatifAdminListeSale = true;
			RafraichirPanneauCreatifAdminSiThrottle();
		}
		if (_joueurRef.StockageRackBatonsOuvert && _joueurRef.RackBatonsOuvert != null && GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert))
		{
			if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBatons)
				_joueurRef.SynchroniserVisuelRackBatons(_joueurRef.RackBatonsOuvert);
			else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBuches)
				_joueurRef.SynchroniserVisuelRackBuches(_joueurRef.RackBatonsOuvert);
            else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche)
                _joueurRef.RackBatonsOuvert.SynchroniserCombustiblePitFeuRocheDepuisGrille();
		}

		RafraichirGrillesStockageSacEtCeinture();

		RafraichirCellulesCraft();
		if (_joueurRef.StockageFourTorchieOuvert)
			RafraichirCellulesFourTorchie();
		RafraichirAffichageCurseurSouris();
	}

	/// <summary>Inventaire (Q) : 2×2 visible. Établi (E sur table) : 3×3.</summary>
	private void AppliquerDispositionGrilleCraft()
	{
		ResoudreGrilleAssemblage();
		if (GrilleAssemblage == null || _joueurRef == null) return;
		bool etabli = _joueurRef.CraftGrille3x3AuTable;
		bool pitRoche = _joueurRef.StockageRackBatonsOuvert
			&& _joueurRef.RackBatonsOuvert != null
			&& GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert)
			&& _joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche;
		GrilleAssemblage.Columns = etabli ? 3 : 2;
		for (int i = 0; i < GrilleAssemblage.GetChildCount(); i++)
		{
			if (GrilleAssemblage.GetChild(i) is Control c)
				c.Visible = pitRoche ? i < 3 : (etabli || i < 4);
		}
		if (GrilleAssemblage.GetParent() is Panel cadre)
			cadre.CustomMinimumSize = pitRoche ? new Vector2(240, 96) : (etabli ? new Vector2(240, 240) : new Vector2(168, 168));
	}

	private void AppliquerBordureActive(Panel slot, bool estActif)
	{
		if (Engine.IsEditorHint() || slot == null) return;
		var style = slot.GetThemeStylebox("panel") as StyleBoxFlat;
		if (style != null)
		{
			var nouveauStyle = (StyleBoxFlat)style.Duplicate();
			nouveauStyle.BorderColor = estActif ? new Color(1, 0.9f, 0.2f) : new Color(1, 1, 1, 0.3f);
			slot.AddThemeStyleboxOverride("panel", nouveauStyle);
		}
	}
}
