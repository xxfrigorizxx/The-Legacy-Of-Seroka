using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void AssurerPreviews3DMains()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreReferencesSlotsMains();
		if (MainGaucheSlot == null || MainDroiteSlot == null) return;
		if (_meshPreviewMenuG == null || !GodotObject.IsInstanceValid(_meshPreviewMenuG))
		{
			_meshPreviewMenuG = CreerViewportPreviewDansSlot(MainGaucheSlot, "ViewportMenuMainG", out _vpMenuGauche);
			_meshPreviewMenuD = CreerViewportPreviewDansSlot(MainDroiteSlot, "ViewportMenuMainD", out _vpMenuDroite);
		}
		if (EquipementCorpsSlot != null && (_meshPreviewMenuCeinture == null || !GodotObject.IsInstanceValid(_meshPreviewMenuCeinture)))
		{
			_meshPreviewMenuCeinture = CreerViewportPreviewDansSlot(EquipementCorpsSlot, "ViewportMenuCeinture", out _vpMenuCeinture);
			_lblSlotCeinture = TrouverOuCreerLabel(EquipementCorpsSlot, "Ceinture\n[vide]");
		}
		if (EquipementSacSlot != null && (_meshPreviewMenuSacEquip == null || !GodotObject.IsInstanceValid(_meshPreviewMenuSacEquip)))
		{
			_meshPreviewMenuSacEquip = CreerViewportPreviewDansSlot(EquipementSacSlot, "ViewportMenuSacEquip", out _vpMenuSacEquip);
			_lblSlotSacEquip = TrouverOuCreerLabel(EquipementSacSlot, "Sac\n[vide]");
		}
		if (CarnetSavoirSlot != null && (_meshPreviewMenuCarnet == null || !GodotObject.IsInstanceValid(_meshPreviewMenuCarnet)))
		{
			_meshPreviewMenuCarnet = CreerViewportPreviewDansSlot(CarnetSavoirSlot, "ViewportMenuCarnet", out _vpMenuCarnet);
			_lblSlotCarnet = TrouverOuCreerLabel(CarnetSavoirSlot, "Carnet\n[vide]");
		}
	}

	private static MeshInstance3D CreerViewportPreviewDansSlot(Panel panel, string nomConteneur, out SubViewportContainer holder)
	{
		holder = new SubViewportContainer
		{
			Name = nomConteneur,
			Stretch = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		holder.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.AddChild(holder);
		// Viewport au premier plan : le label de secours reste derrière le modèle 3D.
		panel.MoveChild(holder, panel.GetChildCount() - 1);

		var viewport = new SubViewport
		{
			Size = new Vector2I(72, 72),
			RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible,
			World3D = new World3D(),
			TransparentBg = true
		};
		holder.AddChild(viewport);

		var cam = new Camera3D();
		cam.SetOrthogonal(0.5f, 0.01f, 10f);
		cam.Position = new Vector3(0, 0, 1.2f);
		cam.Current = true;
		viewport.AddChild(cam);

		var meshNode = new MeshInstance3D();
		meshNode.Position = Vector3.Zero;
		meshNode.RotationDegrees = new Vector3(-20, 25, 0);
		viewport.AddChild(meshNode);

		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-45, 30, 0);
		light.Set("sky_mode", 1);
		viewport.AddChild(light);

		return meshNode;
	}

	private void ConnecterClicsInventaire()
	{
		if (Engine.IsEditorHint()) return;
		ResoudreReferencesSlotsMains();
		ResoudreGrilleAssemblage();
		ResoudreSlotResultatCraft();
		void Branche(Panel pan, Control.GuiInputEventHandler fn)
		{
			if (pan == null) return;
			pan.MouseFilter = Control.MouseFilterEnum.Stop;
			pan.GuiInput += fn;
		}
		if (!_clicsMainsConnectes)
		{
			_clicsMainsConnectes = true;
			Branche(MainGaucheSlot, e => TraiterClicInventaire(e, 0));
			Branche(MainDroiteSlot, e => TraiterClicInventaire(e, 1));
		}
		if (!_clicsCraftConnectes && GrilleAssemblage != null)
		{
			_clicsCraftConnectes = true;
			GrilleAssemblage.MouseFilter = Control.MouseFilterEnum.Ignore;
			int n = GrilleAssemblage.GetChildCount();
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				if (GrilleAssemblage.GetChild(i) is Panel cp)
					Branche(cp, e => TraiterClicInventaire(e, 2, idx));
			}
		}
		if (!_clicsSlotResultatCraftConnecte && SlotResultatCraft != null)
		{
			_clicsSlotResultatCraftConnecte = true;
			Branche(SlotResultatCraft, e => TraiterClicInventaire(e, 3));
		}
		ResoudreReferencesSlotsMains();
		if (!_clicsSlotCeintureConnecte && EquipementCorpsSlot != null)
		{
			_clicsSlotCeintureConnecte = true;
			Branche(EquipementCorpsSlot, e => TraiterClicInventaire(e, 4));
		}
		if (!_clicsSlotSacConnecte && EquipementSacSlot != null)
		{
			_clicsSlotSacConnecte = true;
			Branche(EquipementSacSlot, e => TraiterClicInventaire(e, 5));
		}
		if (CarnetSavoirSlot != null && !CarnetSavoirSlot.HasMeta("ClickBound_8"))
		{
			Branche(CarnetSavoirSlot, e => TraiterClicInventaire(e, 8));
			CarnetSavoirSlot.SetMeta("ClickBound_8", true);
		}
		if (!_clicsGrilleSacConnectes && ObtenirGrilleSac() is GridContainer grilleSac)
		{
			_clicsGrilleSacConnectes = true;
			grilleSac.MouseFilter = Control.MouseFilterEnum.Ignore;
			int n = grilleSac.GetChildCount();
			for (int i = 0; i < n; i++)
			{
				int idx = i;
				if (grilleSac.GetChild(i) is Panel cp)
				{
					Branche(cp, e => TraiterClicInventaire(e, 6, idx));
					cp.SetMeta("ClickBound_6", true);
				}
			}
		}
		if (!_clicsGrilleCeintureStockageConnectes && ObtenirGrilleCeintureStockage() is GridContainer grilleCeint)
		{
			_clicsGrilleCeintureStockageConnectes = true;
			grilleCeint.MouseFilter = Control.MouseFilterEnum.Ignore;
			for (int i = 0; i < grilleCeint.GetChildCount(); i++)
			{
				int idx = i;
				if (grilleCeint.GetChild(i) is Panel cp)
				{
					Branche(cp, e => TraiterClicInventaire(e, 7, idx));
					cp.SetMeta("ClickBound_7", true);
				}
			}
		}
		if (!_clicsGrilleCoffreConnectes && ObtenirGrilleCoffreBois() is GridContainer grilleCoffre)
		{
			_clicsGrilleCoffreConnectes = true;
			grilleCoffre.MouseFilter = Control.MouseFilterEnum.Ignore;
			int nC = grilleCoffre.GetChildCount();
			for (int i = 0; i < nC; i++)
			{
				int idx = i;
				if (grilleCoffre.GetChild(i) is Panel cp)
					Branche(cp, e => TraiterClicInventaire(e, 9, idx));
			}
		}
		AssurerPanneauAnalyseur();
		if (!_clicsGrilleAnalyseurConnectes && _slotsAnalyseur != null && _slotsAnalyseur.Length > 0)
		{
			_clicsGrilleAnalyseurConnectes = true;
			for (int i = 0; i < _slotsAnalyseur.Length; i++)
			{
				int idx = i;
				Panel cp = _slotsAnalyseur[i];
				if (cp == null) continue;
				Branche(cp, e => TraiterClicInventaire(e, 10, idx));
			}
		}
		AssurerCapaciteGrillesStockage();
	}

	private void TraiterClicInventaire(InputEvent e, int mode, int craftIdx = -1)
	{
		if (_joueurRef == null) return;
		if (e is not InputEventMouseButton mb || !mb.Pressed)
			return;
		bool clicGauche = mb.ButtonIndex == MouseButton.Left;
		bool clicDroit = mb.ButtonIndex == MouseButton.Right;
		if (!clicGauche && !clicDroit)
			return;

		if (mode == 0)
			InteragirCurseurAvecSlot(ref _joueurRef.MainGauche, clicGauche, clicDroit);
		else if (mode == 1)
			InteragirCurseurAvecSlot(ref _joueurRef.MainDroite, clicGauche, clicDroit);
		else if (mode == 2 && craftIdx >= 0)
		{
			if (!_joueurRef.CraftGrille3x3AuTable && craftIdx >= 4)
				return;
			var g = _joueurRef.ObtenirGrilleCraftAffichee();
			if (g == null || craftIdx >= g.Length)
				return;
            if (_joueurRef.StockageRackBatonsOuvert)
            {
                TraiterClicRackBatons(ref _joueurRef.RefSlotCraft(craftIdx), clicGauche, clicDroit, craftIdx);
                if (_joueurRef.RackBatonsOuvert != null && GodotObject.IsInstanceValid(_joueurRef.RackBatonsOuvert))
                {
                    if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBatons)
                        _joueurRef.SynchroniserVisuelRackBatons(_joueurRef.RackBatonsOuvert);
                    else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetRackBuches)
                        _joueurRef.SynchroniserVisuelRackBuches(_joueurRef.RackBatonsOuvert);
                    else if (_joueurRef.RackBatonsOuvert.ID_Objet == Joueur.IdObjetPitFeuRoche)
                        _joueurRef.RackBatonsOuvert.SynchroniserCombustiblePitFeuRocheDepuisGrille();
                }
                _joueurRef.VerifierRecettes();
                GetViewport()?.SetInputAsHandled();
                _joueurRef.RafraichirHUD();
                RafraichirMenu();
                return;
            }
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotCraft(craftIdx), clicGauche, clicDroit);
			_joueurRef.VerifierRecettes();
		}
		else if (mode == 3)
		{
			if (clicGauche && _curseurMenu.EstVide && !_joueurRef.SlotResultatCraft.EstVide)
			{
				_curseurMenu = _joueurRef.AppliquerBonusMetierTraisageAuResultatCraft(_joueurRef.SlotResultatCraft);
				_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
				_joueurRef.ConsommerIngredientsCraft();
				_joueurRef.VerifierRecettes();
			}
			else
				return;
		}
		else if (mode == 4)
		{
			if (!EchangerCurseurAvecEquipementCeintureSiValide())
				return;
		}
		else if (mode == 5)
		{
			if (!EchangerCurseurAvecEquipementSacSiValide())
				return;
		}
		else if (mode == 6 && craftIdx >= 0)
		{
			int capSac = Joueur.ObtenirCapaciteSacStockage(_joueurRef.EquipementSacDos);
			if (!_joueurRef.ASacEquipe() || craftIdx < 0 || craftIdx >= capSac) return;
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotSac(craftIdx), clicGauche, clicDroit, slotSacStockage: true);
		}
		else if (mode == 7 && craftIdx >= 0)
		{
			int capCeinture = Joueur.ObtenirCapaciteCeintureStockage(_joueurRef.EquipementCeinture);
			if (!_joueurRef.ACeintureSacochesEquipe() || craftIdx < 0 || craftIdx >= capCeinture) return;
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotCeintureStockage(craftIdx), clicGauche, clicDroit, slotCeintureStockage: true, indexSlotCeinture: craftIdx);
		}
		else if (mode == 8)
		{
			if (!EchangerCurseurAvecEquipementCarnetSiValide())
				return;
		}
		else if (mode == 9 && craftIdx >= 0)
		{
			if (!_joueurRef.StockageCoffreOuvert || craftIdx < 0 || craftIdx > 9) return;
			InteragirCurseurAvecSlot(ref _joueurRef.RefSlotCoffreStockage(craftIdx), clicGauche, clicDroit, slotCoffreStockage: true);
			_joueurRef.VerifierRecettes();
		}
		else if (mode == 10 && craftIdx >= 0)
		{
			SlotInventaire[] grilleAnalyse = _joueurRef.ObtenirGrilleAnalyseurActif();
			if (grilleAnalyse == null || craftIdx >= grilleAnalyse.Length)
				return;
			InteragirCurseurAvecSlot(ref grilleAnalyse[craftIdx], clicGauche, clicDroit);
		}
		else
			return;

		GetViewport()?.SetInputAsHandled();
		_joueurRef.RafraichirHUD();
	}

	private int CompterQuantiteTotaleRack()
	{
		if (_joueurRef == null || !_joueurRef.StockageRackBatonsOuvert) return 0;
		return _joueurRef.CompterQuantiteRackOuvert();
	}

	private void DeposerDepuisCurseurVersRack(ref SlotInventaire destination, int quantiteSouhaitee)
	{
		if (_joueurRef == null || _curseurMenu.EstVide || !_joueurRef.EstSlotStockableDansRackOuvert(_curseurMenu)) return;
		int qCur = Joueur.ObtenirQuantiteSlot(_curseurMenu);
		if (qCur <= 0) return;
		int capacite = _joueurRef.ObtenirCapaciteRackOuvert();
		int espaceGlobal = Mathf.Max(0, capacite - CompterQuantiteTotaleRack());
		if (espaceGlobal <= 0) return;

		if (destination.EstVide)
		{
			int move = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), Mathf.Min(espaceGlobal, capacite));
			if (move <= 0) return;
			destination = _curseurMenu;
			destination.Quantite = move;
			if (qCur - move <= 0) _curseurMenu = new SlotInventaire();
			else _curseurMenu.Quantite = qCur - move;
			return;
		}

		if (!Joueur.SontEmpilables(destination, _curseurMenu)) return;
		int qDst = Joueur.ObtenirQuantiteSlot(destination);
		int moveStack = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), Mathf.Min(espaceGlobal, capacite - qDst));
		if (moveStack <= 0) return;
		destination.Quantite = qDst + moveStack;
		if (qCur - moveStack <= 0) _curseurMenu = new SlotInventaire();
		else _curseurMenu.Quantite = qCur - moveStack;
	}

	private void DeposerDepuisCurseurVersSlotSimple(ref SlotInventaire destination, int quantiteSouhaitee)
	{
		if (_curseurMenu.EstVide) return;
		int qCur = Joueur.ObtenirQuantiteSlot(_curseurMenu);
		if (qCur <= 0) return;

		if (destination.EstVide)
		{
			int maxPile = Mathf.Max(1, Joueur.ObtenirPileMax(_curseurMenu));
			int move = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), maxPile);
			if (move <= 0) return;
			destination = _curseurMenu;
			destination.Quantite = move;
			if (qCur - move <= 0) _curseurMenu = new SlotInventaire();
			else _curseurMenu.Quantite = qCur - move;
			return;
		}

		if (!Joueur.SontEmpilables(destination, _curseurMenu)) return;
		int qDst = Joueur.ObtenirQuantiteSlot(destination);
		int maxPileDst = Mathf.Max(1, Joueur.ObtenirPileMax(destination));
		int moveStack = Mathf.Min(Mathf.Min(qCur, quantiteSouhaitee), Mathf.Max(0, maxPileDst - qDst));
		if (moveStack <= 0) return;
		destination.Quantite = qDst + moveStack;
		if (qCur - moveStack <= 0) _curseurMenu = new SlotInventaire();
		else _curseurMenu.Quantite = qCur - moveStack;
	}

	private void TraiterClicRackBatons(ref SlotInventaire slotRack, bool clicGauche, bool clicDroit, int rackIdx)
	{
		bool pitRoche = _joueurRef != null && _joueurRef.RackOuvertEstPitFeuRoche();
		if (pitRoche)
		{
			if (clicGauche)
			{
				if (_curseurMenu.EstVide)
				{
					if (!slotRack.EstVide)
					{
						_curseurMenu = slotRack;
						_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
						slotRack = new SlotInventaire();
					}
				}
				else
				{
					if (_joueurRef.EstSlotSortiePitFeuRoche(rackIdx))
						return;
					if (!_joueurRef.EstSlotStockableDansPitFeuRocheIndex(rackIdx, _curseurMenu))
						return;
					DeposerDepuisCurseurVersSlotSimple(ref slotRack, int.MaxValue);
				}
				return;
			}

			if (clicDroit)
			{
				if (_curseurMenu.EstVide)
				{
					if (!slotRack.EstVide)
					{
						int q = Joueur.ObtenirQuantiteSlot(slotRack);
						_curseurMenu = slotRack;
						_curseurMenu.Quantite = 1;
						if (q <= 1) slotRack = new SlotInventaire();
						else slotRack.Quantite = q - 1;
					}
				}
				else
				{
					if (_joueurRef.EstSlotSortiePitFeuRoche(rackIdx))
						return;
					if (!_joueurRef.EstSlotStockableDansPitFeuRocheIndex(rackIdx, _curseurMenu))
						return;
					DeposerDepuisCurseurVersSlotSimple(ref slotRack, 1);
				}
			}
			return;
		}

		// Rack dédié: capacité globale pilotée par le type de rack ouvert (bâtons/bûches).
		if (clicGauche)
		{
			if (_curseurMenu.EstVide)
			{
				if (!slotRack.EstVide)
				{
					_curseurMenu = slotRack;
					_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
					slotRack = new SlotInventaire();
				}
			}
			else
			{
				DeposerDepuisCurseurVersRack(ref slotRack, int.MaxValue);
			}
			return;
		}

		if (clicDroit)
		{
			if (_curseurMenu.EstVide)
			{
				if (!slotRack.EstVide)
				{
					int q = Joueur.ObtenirQuantiteSlot(slotRack);
					_curseurMenu = slotRack;
					_curseurMenu.Quantite = 1;
					if (q <= 1) slotRack = new SlotInventaire();
					else slotRack.Quantite = q - 1;
				}
			}
			else
			{
				DeposerDepuisCurseurVersRack(ref slotRack, 1);
			}
		}
	}

	private static SlotInventaire CopierSlotUnitaire(SlotInventaire src)
	{
		var s = src;
		s.Quantite = 1;
		return s;
	}

	private static bool PeutEmpiler(SlotInventaire a, SlotInventaire b, int maxPile) => Joueur.SontEmpilables(a, b) && maxPile > 1;

	private int ObtenirMultiplicateurPileSlotSac()
	{
		if (_joueurRef == null || !_joueurRef.ASacEquipe()) return 1;
		return Joueur.ObtenirMultiplicateurPileSac(_joueurRef.EquipementSacDos);
	}

	private int ObtenirMultiplicateurPileSlotCeinture(int indexSlotCeinture)
	{
		if (_joueurRef == null || !_joueurRef.ACeintureSacochesEquipe()) return 1;
		return Joueur.ObtenirMultiplicateurPileCeintureSlot(_joueurRef.EquipementCeinture, indexSlotCeinture);
	}

	private int ObtenirPileMaxContexte(SlotInventaire slot, bool slotSacStockage, bool slotCeintureStockage, int indexSlotCeinture = -1)
	{
		int max = Joueur.ObtenirPileMax(slot);
		if (slotSacStockage) max *= ObtenirMultiplicateurPileSlotSac();
		if (slotCeintureStockage) max *= ObtenirMultiplicateurPileSlotCeinture(indexSlotCeinture);
		return max;
	}

	private void InteragirCurseurAvecSlot(ref SlotInventaire slot, bool clicGauche, bool clicDroit, bool slotSacStockage = false, bool slotCeintureStockage = false, int indexSlotCeinture = -1, bool slotCoffreStockage = false)
	{
		if (clicGauche)
		{
			InteractionClicGauche(ref slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture, slotCoffreStockage);
			return;
		}
		if (clicDroit)
			InteractionClicDroit(ref slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture, slotCoffreStockage);
	}

	private void InteractionClicGauche(ref SlotInventaire slot, bool slotSacStockage, bool slotCeintureStockage, int indexSlotCeinture, bool slotCoffreStockage = false)
	{
		if (slotCoffreStockage && !_curseurMenu.EstVide && Joueur.EstObjetInterditDansCoffre(_curseurMenu))
		{
			if (slot.EstVide)
				return;
			if (!PeutEmpiler(slot, _curseurMenu, ObtenirPileMaxContexte(slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture)))
				return;
		}
		if (_curseurMenu.EstVide)
		{
			_curseurMenu = slot;
			if (!_curseurMenu.EstVide) _curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
			slot = new SlotInventaire();
			return;
		}
		if (slot.EstVide)
		{
			slot = _curseurMenu;
			slot.Quantite = Joueur.ObtenirQuantiteSlot(slot);
			_curseurMenu = new SlotInventaire();
			return;
		}
		int max = ObtenirPileMaxContexte(slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture);
		if (PeutEmpiler(slot, _curseurMenu, max))
		{
			int qDst = Joueur.ObtenirQuantiteSlot(slot);
			int qSrc = Joueur.ObtenirQuantiteSlot(_curseurMenu);
			int place = Mathf.Max(0, max - qDst);
			int depose = Mathf.Min(place, qSrc);
			if (depose > 0)
			{
				slot.Quantite = qDst + depose;
				qSrc -= depose;
				if (qSrc <= 0) _curseurMenu = new SlotInventaire();
				else _curseurMenu.Quantite = qSrc;
				return;
			}
		}
		if (slotCoffreStockage && Joueur.EstObjetInterditDansCoffre(_curseurMenu))
			return;
		var a = _curseurMenu;
		_curseurMenu = slot;
		slot = a;
		_curseurMenu.Quantite = Joueur.ObtenirQuantiteSlot(_curseurMenu);
		slot.Quantite = Joueur.ObtenirQuantiteSlot(slot);
	}

	private void InteractionClicDroit(ref SlotInventaire slot, bool slotSacStockage, bool slotCeintureStockage, int indexSlotCeinture, bool slotCoffreStockage = false)
	{
		if (_curseurMenu.EstVide)
		{
			if (slot.EstVide) return;
			int q = Joueur.ObtenirQuantiteSlot(slot);
			int prendre = Mathf.CeilToInt(q * 0.5f);
			_curseurMenu = slot;
			_curseurMenu.Quantite = prendre;
			int reste = q - prendre;
			if (reste <= 0) slot = new SlotInventaire();
			else slot.Quantite = reste;
			return;
		}
		if (slot.EstVide)
		{
			if (slotCoffreStockage && Joueur.EstObjetInterditDansCoffre(_curseurMenu))
				return;
			slot = CopierSlotUnitaire(_curseurMenu);
			int qSrc = Joueur.ObtenirQuantiteSlot(_curseurMenu) - 1;
			if (qSrc <= 0) _curseurMenu = new SlotInventaire();
			else _curseurMenu.Quantite = qSrc;
			return;
		}
		int max = ObtenirPileMaxContexte(slot, slotSacStockage, slotCeintureStockage, indexSlotCeinture);
		if (!PeutEmpiler(slot, _curseurMenu, max)) return;
		int qDst = Joueur.ObtenirQuantiteSlot(slot);
		if (qDst >= max) return;
		slot.Quantite = qDst + 1;
		int qSrc2 = Joueur.ObtenirQuantiteSlot(_curseurMenu) - 1;
		if (qSrc2 <= 0) _curseurMenu = new SlotInventaire();
		else _curseurMenu.Quantite = qSrc2;
	}

	private void EchangerCurseurAvec(ref SlotInventaire slot)
	{
		var a = _curseurMenu;
		_curseurMenu = slot;
		slot = a;
	}

	/// <summary>Échange curseur ↔ équipement ceinture : ceinture simple (102) ou ceinture à sacoches (104).</summary>
	private bool EchangerCurseurAvecEquipementCeintureSiValide()
	{
		if (_joueurRef == null) return false;
		if (!_curseurMenu.EstVide && _curseurMenu.ID != Joueur.IdObjetCeinturePoches && _curseurMenu.ID != Joueur.IdObjetCeintureSacoches)
		{
			GD.Print("ZERO-K : Ce slot rouge n’accepte que les ceintures.");
			return false;
		}
		SlotInventaire surCeinture = _joueurRef.EquipementCeinture;
		SlotInventaire depuisCurseur = _curseurMenu;
		_joueurRef.AssignerEquipementCeinture(depuisCurseur);
		_curseurMenu = surCeinture;
		return true;
	}

	private bool EchangerCurseurAvecEquipementSacSiValide()
	{
		if (_joueurRef == null) return false;
		if (!_curseurMenu.EstVide && _curseurMenu.ID != Joueur.IdObjetSacTier0)
		{
			GD.Print("ZERO-K : Ce slot n’accepte que le sac tier 0.");
			return false;
		}
		SlotInventaire surSac = _joueurRef.EquipementSacDos;
		SlotInventaire depuisCurseur = _curseurMenu;
		_joueurRef.AssignerEquipementSacDos(depuisCurseur);
		_curseurMenu = surSac;
		return true;
	}

	private bool EchangerCurseurAvecEquipementCarnetSiValide()
	{
		if (_joueurRef == null) return false;
		if (!_curseurMenu.EstVide && _curseurMenu.ID != Joueur.IdObjetCarnetSavoir)
		{
			GD.Print("ZERO-K : Ce slot n'accepte que le carnet du savoir.");
			return false;
		}
		SlotInventaire surCarnet = _joueurRef.EquipementCarnet;
		SlotInventaire depuisCurseur = _curseurMenu;
		_joueurRef.AssignerEquipementCarnet(depuisCurseur);
		_curseurMenu = surCarnet;
		return true;
	}

	private void ResoudreCurseurAvantFermeture()
	{
		if (_joueurRef == null || _curseurMenu.EstVide) return;
		if (_joueurRef.MainGauche.EstVide)
		{
			_joueurRef.MainGauche = _curseurMenu;
			_curseurMenu = new SlotInventaire();
		}
		else if (_joueurRef.MainDroite.EstVide)
		{
			_joueurRef.MainDroite = _curseurMenu;
			_curseurMenu = new SlotInventaire();
		}
		else
		{
			bool place = false;
			if (_joueurRef.StockageCoffreOuvert && _joueurRef.CoffreOuvert != null && GodotObject.IsInstanceValid(_joueurRef.CoffreOuvert)
				&& !Joueur.EstObjetInterditDansCoffre(_curseurMenu))
			{
				for (int i = 0; i < 10; i++)
				{
					ref SlotInventaire sc = ref _joueurRef.RefSlotCoffreStockage(i);
					if (!sc.EstVide) continue;
					sc = _curseurMenu;
					place = true;
					break;
				}
			}
			var g = _joueurRef.ObtenirGrilleCraftAffichee();
			int maxI = _joueurRef.CraftGrille3x3AuTable ? 9 : 4;
			if (!place && g != null)
			{
				for (int i = 0; i < maxI && i < g.Length; i++)
				{
					if (!g[i].EstVide) continue;
					g[i] = _curseurMenu;
					place = true;
					break;
				}
			}
			if (place)
				_curseurMenu = new SlotInventaire();
			else
			{
				if (_joueurRef.MainGaucheEstActive)
					EchangerCurseurAvec(ref _joueurRef.MainGauche);
				else
					EchangerCurseurAvec(ref _joueurRef.MainDroite);
				// Garde l’ancien contenu de la main dans le curseur pour la prochaine ouverture.
			}
		}
		_joueurRef.RafraichirHUD();
	}
}
