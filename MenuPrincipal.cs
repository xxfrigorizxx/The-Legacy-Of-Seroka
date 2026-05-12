using Godot;

/// <summary>Menu principal : charger, quitter, assistant « Nouveau monde » en deux étapes + aperçu 3D.</summary>
public partial class MenuPrincipal : Control
{
	private const string CheminTextureFondAccueil = "res://textures/ui/menu/menu.png";

	private Panel _panelMenuPrincipal;
	private Panel _panelEtapeMonde;
	private Panel _panelEtapePerso;
	private VBoxContainer _vboxPrincipal;
	private LineEdit _nomNouveauMonde;
	private LineEdit _seedNouveauMonde;
	private Label _labelErreurMonde;
	private LineEdit _nomPersonnage;
	private Label _labelRaceCourante;
	private Label _labelErreurPerso;
	private Label _labelBonusRacial;
	private OptionButton _listeMondes;
	private Button _btnNouveauMondeWizard;
	private Button _btnLoadGame;
	private Button _btnQuit;
	private Button _btnSuivantMonde;
	private Button _btnRetourMenuDepuisMonde;
	private Button _btnRacePrecedente;
	private Button _btnRaceSuivante;
	private Button _btnSexePrecedent;
	private Button _btnSexeSuivant;
	private Label _labelSexeCourant;
	private Button _btnRetourDepuisPerso;
	private Button _btnConfirmerEtJouer;
	private ApercuRaceMenu3D _apercu3d;
	private RaceJoueur _raceSelectionnee = RaceJoueur.Humain;
	private SexeJoueur _sexeSelectionne = SexeJoueur.Masculin;

	public override void _Ready()
	{
		_panelMenuPrincipal = GetNode<Panel>("PanelMenuPrincipal");
		_panelEtapeMonde = GetNode<Panel>("PanelEtapeMonde");
		_panelEtapePerso = GetNode<Panel>("PanelEtapePerso");

		_vboxPrincipal = _panelMenuPrincipal.GetNode<VBoxContainer>("VBoxMenuPrincipal");
		_listeMondes = _vboxPrincipal.GetNode<OptionButton>("ListeMondes");
		_btnLoadGame = _vboxPrincipal.GetNode<Button>("BtnLoadGame");
		_btnNouveauMondeWizard = _vboxPrincipal.GetNode<Button>("BtnNouveauMondeWizard");
		_btnQuit = _vboxPrincipal.GetNode<Button>("BtnQuit");
		ConfigurerVisuelsAccueil();

		var vboxMonde = _panelEtapeMonde.GetNode<VBoxContainer>("VBoxEtapeMonde");
		_nomNouveauMonde = vboxMonde.GetNode<LineEdit>("NomNouveauMonde");
		_seedNouveauMonde = vboxMonde.GetNode<LineEdit>("SeedNouveauMonde");
		_labelErreurMonde = vboxMonde.GetNode<Label>("LabelErreurMonde");
		_btnRetourMenuDepuisMonde = vboxMonde.GetNode<Button>("HBoxBtnsMonde/BtnRetourMenuDepuisMonde");
		_btnSuivantMonde = vboxMonde.GetNode<Button>("HBoxBtnsMonde/BtnSuivantMonde");

		var vboxPerso = _panelEtapePerso.GetNode<VBoxContainer>("VBoxEtapePerso");
		_apercu3d = vboxPerso.GetNode<ApercuRaceMenu3D>("SubViewportContainer/SubViewport/ApercuRaceMenu3D");
		_btnRacePrecedente = vboxPerso.GetNode<Button>("HBoxRace/BtnRacePrecedente");
		_btnRaceSuivante = vboxPerso.GetNode<Button>("HBoxRace/BtnRaceSuivante");
		_labelRaceCourante = vboxPerso.GetNode<Label>("HBoxRace/LabelRaceCourante");
		_btnSexePrecedent = vboxPerso.GetNode<Button>("HBoxSexe/BtnSexePrecedent");
		_btnSexeSuivant = vboxPerso.GetNode<Button>("HBoxSexe/BtnSexeSuivant");
		_labelSexeCourant = vboxPerso.GetNode<Label>("HBoxSexe/LabelSexeCourant");
		_labelBonusRacial = vboxPerso.GetNodeOrNull<Label>("LabelBonusRacial");
		if (_labelBonusRacial == null)
		{
			_labelBonusRacial = new Label
			{
				Name = "LabelBonusRacial",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_labelBonusRacial.AddThemeColorOverride("font_color", new Color(0.82f, 0.90f, 1f));
			_labelBonusRacial.AddThemeFontSizeOverride("font_size", 15);
			vboxPerso.AddChild(_labelBonusRacial);
			vboxPerso.MoveChild(_labelBonusRacial, 4);
		}
		_nomPersonnage = vboxPerso.GetNode<LineEdit>("NomPersonnage");
		_labelErreurPerso = vboxPerso.GetNode<Label>("LabelErreurPerso");
		_btnRetourDepuisPerso = vboxPerso.GetNode<Button>("HBoxBtnsPerso/BtnRetourDepuisPerso");
		_btnConfirmerEtJouer = vboxPerso.GetNode<Button>("HBoxBtnsPerso/BtnConfirmerEtJouer");

		_labelErreurMonde.Text = "";
		_labelErreurPerso.Text = "";
		_btnNouveauMondeWizard.Pressed += OuvrirAssistantEtapeMonde;
		_btnSuivantMonde.Pressed += OnSuivantEtapeMonde;
		_btnRetourMenuDepuisMonde.Pressed += OnRetourMenuDepuisEtapeMonde;
		_btnRetourDepuisPerso.Pressed += OnRetourDepuisEtapePerso;
		_btnConfirmerEtJouer.Pressed += OnConfirmerEtJouer;
		_btnRacePrecedente.Pressed += () => ChangerRace(-1);
		_btnRaceSuivante.Pressed += () => ChangerRace(1);
		_btnSexePrecedent.Pressed += () => ChangerSexe(-1);
		_btnSexeSuivant.Pressed += () => ChangerSexe(1);
		_btnLoadGame.Pressed += OnLoadGame;
		_btnQuit.Pressed += () => GetTree().Quit();

		RafraichirListeMondes();
		SelectionnerDernierMondeJoueDansListeSiConnu();
		if (Etat.RecreationPersonnageMemeMondeEnAttente)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			_panelMenuPrincipal.Visible = false;
			_panelEtapeMonde.Visible = false;
			_panelEtapePerso.Visible = true;
			_labelErreurPerso.Text = "";
			_nomPersonnage.Text = "";
			_raceSelectionnee = Etat.RaceJoueurCourante;
			_sexeSelectionne = Etat.SexeJoueurCourante;
			MettreAJourAffichageRace();
			MettreAJourAffichageSexe();
			_apercu3d?.DefinirRaceEtSexe(_raceSelectionnee, _sexeSelectionne);
		}
		else
			AfficherPanneauPrincipal();
	}

	private void ConfigurerVisuelsAccueil()
	{
		Texture2D textureFond = ResourceLoader.Load<Texture2D>(CheminTextureFondAccueil);
		if (textureFond != null)
		{
			ColorRect ancienFondUni = GetNodeOrNull<ColorRect>("ColorRect");
			if (ancienFondUni != null)
				ancienFondUni.Visible = false;

			var fondAccueil = GetNodeOrNull<TextureRect>("FondAccueilTexture");
			if (fondAccueil == null)
			{
				fondAccueil = new TextureRect
				{
					Name = "FondAccueilTexture",
					MouseFilter = MouseFilterEnum.Ignore
				};
				fondAccueil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
				AddChild(fondAccueil);
				MoveChild(fondAccueil, 0);
			}

			fondAccueil.Texture = textureFond;
			fondAccueil.ExpandMode = (TextureRect.ExpandModeEnum)1;
			fondAccueil.StretchMode = (TextureRect.StretchModeEnum)0;
		}
		else
			GD.PushWarning($"MenuPrincipal: texture de fond introuvable ({CheminTextureFondAccueil}).");

	}

	private GameState Etat => GetNode<GameState>("/root/GameState");

	private void AfficherPanneauPrincipal()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_panelMenuPrincipal.Visible = true;
		_panelEtapeMonde.Visible = false;
		_panelEtapePerso.Visible = false;
	}

	private void OuvrirAssistantEtapeMonde()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_labelErreurMonde.Text = "";
		_panelMenuPrincipal.Visible = false;
		_panelEtapeMonde.Visible = true;
		_panelEtapePerso.Visible = false;
	}

	private void OnRetourMenuDepuisEtapeMonde()
	{
		Etat.AnnulerCreationMondeBrouillon();
		AfficherPanneauPrincipal();
	}

	private void OnSuivantEtapeMonde()
	{
		_labelErreurMonde.Text = "";
		if (!Etat.EssayerValiderEtapeMondeNouveau(_nomNouveauMonde.Text, _seedNouveauMonde.Text, out string erreur))
		{
			_labelErreurMonde.Text = erreur ?? "Validation impossible.";
			return;
		}

		_panelEtapeMonde.Visible = false;
		_panelEtapePerso.Visible = true;
		_labelErreurPerso.Text = "";
		_raceSelectionnee = RaceJoueur.Humain;
		_sexeSelectionne = SexeJoueur.Masculin;
		MettreAJourAffichageRace();
		MettreAJourAffichageSexe();
		_apercu3d?.DefinirRaceEtSexe(_raceSelectionnee, _sexeSelectionne);
	}

	private void OnRetourDepuisEtapePerso()
	{
		if (Etat.RecreationPersonnageMemeMondeEnAttente)
		{
			Etat.AnnulerRecreationPersonnageMemeMondeEnAttente();
			AfficherPanneauPrincipal();
			return;
		}
		_panelEtapePerso.Visible = false;
		_panelEtapeMonde.Visible = true;
	}

	private void ChangerRace(int delta)
	{
		int v = (int)_raceSelectionnee + delta;
		while (v < 0) v += 2;
		while (v > 1) v -= 2;
		_raceSelectionnee = (RaceJoueur)v;
		MettreAJourAffichageRace();
		_apercu3d?.DefinirRaceEtSexe(_raceSelectionnee, _sexeSelectionne);
	}

	private void ChangerSexe(int delta)
	{
		int v = (int)_sexeSelectionne + delta;
		while (v < 0) v += 2;
		while (v > 1) v -= 2;
		_sexeSelectionne = (SexeJoueur)v;
		MettreAJourAffichageSexe();
		_apercu3d?.DefinirRaceEtSexe(_raceSelectionnee, _sexeSelectionne);
	}

	private void MettreAJourAffichageRace()
	{
		_labelRaceCourante.Text = _raceSelectionnee == RaceJoueur.Orc ? "Orc" : "Humain";
		if (_labelBonusRacial != null)
		{
			_labelBonusRacial.Text = _raceSelectionnee == RaceJoueur.Orc
				? "Bonus racial Orc : Force élevée, Constitution élevée, Intelligence basse. XP Force x2, XP Constitution x2 (réservé), XP Intelligence x0,5."
				: "Humain : profil neutre. Aucun bonus ou malus racial d'XP.";
		}
	}

	private void MettreAJourAffichageSexe()
	{
		_labelSexeCourant.Text = _sexeSelectionne == SexeJoueur.Feminin ? "Féminin" : "Masculin";
	}

	private void OnConfirmerEtJouer()
	{
		_labelErreurPerso.Text = "";
		if (Etat.RecreationPersonnageMemeMondeEnAttente)
		{
			if (!Etat.EssayerFinaliserRecreationPersonnageSurMondeExistant(_nomPersonnage.Text, _raceSelectionnee, _sexeSelectionne, out string erreurMort))
			{
				_labelErreurPerso.Text = erreurMort ?? "Création impossible.";
				return;
			}
			RafraichirListeMondes();
			SelectionnerDernierMondeJoueDansListeSiConnu();
			GetTree().ChangeSceneToFile("res://monde_zero.tscn");
			return;
		}
		if (!Etat.EssayerFinaliserNouveauMondeAvecPersonnage(_nomPersonnage.Text, _raceSelectionnee, _sexeSelectionne, out string erreur))
		{
			_labelErreurPerso.Text = erreur ?? "Création impossible.";
			return;
		}

		RafraichirListeMondes();
		SelectionnerDernierMondeJoueDansListeSiConnu();
		GetTree().ChangeSceneToFile("res://monde_zero.tscn");
	}

	private void RafraichirListeMondes()
	{
		_listeMondes.Clear();
		_listeMondes.AddItem("-- Choisir un monde --", 0);
		var mondes = Etat.ObtenirListeMondes();
		for (int i = 0; i < mondes.Count; i++)
			_listeMondes.AddItem(mondes[i], i + 1);
	}

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

	private void OnLoadGame()
	{
		int idx = _listeMondes.Selected;
		if (idx <= 0) return;
		string nom = _listeMondes.GetItemText(idx);
		if (!Etat.ChargerMonde(nom)) return;
		GetTree().ChangeSceneToFile("res://monde_zero.tscn");
	}
}
