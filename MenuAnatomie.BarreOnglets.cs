using Godot;
using System;
using System.Collections.Generic;

public partial class MenuAnatomie : Control
{
	private void AppliquerEcranBarre(ModeEcranBarreMenu mode)
	{
		if (Engine.IsEditorHint()) return;
		if (mode == ModeEcranBarreMenu.CreatifAdmin && (_joueurRef == null || !_joueurRef.ModeCreatifActif))
			mode = ModeEcranBarreMenu.Inventaire;
		_ecranBarreCourant = mode;
		AssurerPanneauSauvegarderQuitter();
		AssurerPanneauAnalyseur();
		AssurerPanneauCreatifAdmin();
		if (_corpsHBoxRef != null)
			_corpsHBoxRef.Visible = mode == ModeEcranBarreMenu.Inventaire || mode == ModeEcranBarreMenu.Analyseur;
		if (_panneauAnalyseur != null)
			_panneauAnalyseur.Visible = mode == ModeEcranBarreMenu.Analyseur;
		if (_panneauCreatifAdmin != null)
			_panneauCreatifAdmin.Visible = mode == ModeEcranBarreMenu.CreatifAdmin;
		if (_panneauSauvegarderQuitter != null)
			_panneauSauvegarderQuitter.Visible = mode == ModeEcranBarreMenu.SauvegarderQuitter;
		MettreAJourStyleOngletsBarre();
		if (mode == ModeEcranBarreMenu.Analyseur)
			RafraichirPanneauAnalyseur();
		else if (mode == ModeEcranBarreMenu.CreatifAdmin)
			RafraichirPanneauCreatifAdmin();
		RafraichirAffichageCurseurSouris();
	}

	private void MettreAJourStyleOngletsBarre()
	{
		Color actif = Colors.White;
		Color inactif = new(0.62f, 0.62f, 0.62f);
		if (_ongletInventaireBarre != null)
			_ongletInventaireBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.Inventaire ? actif : inactif;
		if (_ongletFutureStateBarre != null)
			_ongletFutureStateBarre.Modulate = inactif;
		if (_ongletMetierBarre != null)
			_ongletMetierBarre.Modulate = inactif;
		if (_ongletAnalyseurBarre != null)
			_ongletAnalyseurBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.Analyseur ? actif : inactif;
		if (_ongletCreatifBarre != null)
		{
			_ongletCreatifBarre.Visible = _joueurRef != null && _joueurRef.ModeCreatifActif;
			_ongletCreatifBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.CreatifAdmin ? actif : inactif;
		}
		if (_ongletQuitterBarre != null)
			_ongletQuitterBarre.Modulate = _ecranBarreCourant == ModeEcranBarreMenu.SauvegarderQuitter ? actif : inactif;
	}

	private void _OnOngletInventaireBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
	}

	private void _OnOngletQuitterJeuBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.SauvegarderQuitter);
	}

	private void _OnOngletFutureStateBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		_joueurRef?.OuvrirFutureStateDepuisMenu();
	}

	private void _OnOngletMetierBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		_joueurRef?.OuvrirMetiersDepuisMenu();
	}

	private void _OnOngletAnalyseurBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.Analyseur);
	}

	private void _OnOngletCreatifAdminBarre(InputEvent e)
	{
		if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
			return;
		GetViewport()?.SetInputAsHandled();
		AppliquerEcranBarre(ModeEcranBarreMenu.CreatifAdmin);
	}

	public void ForcerOngletInventaire()
	{
		AppliquerEcranBarre(ModeEcranBarreMenu.Inventaire);
	}

	/// <summary>Accroche le bloc faim / énergie du joueur sous les barres de PV (colonne gauche du menu inventaire Q).</summary>
	public void AttacherHudFaimEnergieJoueur(Control widget)
	{
		if (widget == null)
			return;
		AssurerPanneauSanteCorps();
		if (_boiteFaimEnergieExterne == null || !GodotObject.IsInstanceValid(_boiteFaimEnergieExterne))
			return;
		Node parent = widget.GetParent();
		if (parent != null)
			parent.RemoveChild(widget);
		_boiteFaimEnergieExterne.AddChild(widget);
	}
}
