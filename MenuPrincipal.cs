using Godot;
using System.Collections.Generic;

/// <summary>Menu principal : nouveau monde (nom + seed optionnelle), charger, quitter.</summary>
public partial class MenuPrincipal : Control
{
	private VBoxContainer _vbox;
	private LineEdit _nomNouveauMonde;
	private LineEdit _seedNouveauMonde;
	private Label _labelErreurCreation;
	private OptionButton _listeMondes;
	private Button _btnNewWorld;
	private Button _btnLoadGame;
	private Button _btnQuit;

	public override void _Ready()
	{
		_vbox = GetNode<VBoxContainer>("VBoxContainer");
		_nomNouveauMonde = _vbox.GetNode<LineEdit>("NomNouveauMonde");
		_seedNouveauMonde = _vbox.GetNode<LineEdit>("SeedNouveauMonde");
		_labelErreurCreation = _vbox.GetNode<Label>("LabelErreurCreation");
		_btnNewWorld = _vbox.GetNode<Button>("BtnNewWorld");
		_btnLoadGame = _vbox.GetNode<Button>("BtnLoadGame");
		_listeMondes = _vbox.GetNode<OptionButton>("ListeMondes");
		_btnQuit = _vbox.GetNode<Button>("BtnQuit");

		_labelErreurCreation.Text = "";
		_btnNewWorld.Pressed += OnNewWorld;
		_btnLoadGame.Pressed += OnLoadGame;
		_btnQuit.Pressed += () => GetTree().Quit();

		RafraichirListeMondes();
		SelectionnerDernierMondeJoueDansListeSiConnu();
	}

	private GameState Etat => GetNode<GameState>("/root/GameState");

	private void RafraichirListeMondes()
	{
		_listeMondes.Clear();
		_listeMondes.AddItem("-- Choisir un monde --", 0);
		var mondes = Etat.ObtenirListeMondes();
		for (int i = 0; i < mondes.Count; i++)
			_listeMondes.AddItem(mondes[i], i + 1);
	}

	/// <summary>Pré-sélectionne le dernier monde enregistré pour éviter de recharger le mauvais dossier par inadvertance.</summary>
	private void SelectionnerDernierMondeJoueDansListeSiConnu()
	{
		if (!Etat.EssayerLireDernierMondeJoueSurDisque(out string dernier)) return;
		for (int i = 1; i < _listeMondes.ItemCount; i++)
		{
			if (_listeMondes.GetItemText(i) == dernier)
			{
				_listeMondes.Select(i);
				return;
			}
		}
	}

	private void OnNewWorld()
	{
		_labelErreurCreation.Text = "";
		if (!Etat.EssayerCreerNouveauMonde(_nomNouveauMonde.Text, _seedNouveauMonde.Text, out string erreur))
		{
			_labelErreurCreation.Text = erreur ?? "Création impossible.";
			return;
		}
		RafraichirListeMondes();
		SelectionnerDernierMondeJoueDansListeSiConnu();
		GetTree().ChangeSceneToFile("res://monde_zero.tscn");
	}

	private void OnLoadGame()
	{
		int idx = _listeMondes.Selected;
		if (idx <= 0) return;
		string nom = _listeMondes.GetItemText(idx);
		if (!Etat.ChargerMonde(nom)) return;
		GetTree().ChangeSceneToFile("res://monde_zero.tscn");
	}
}
