using Godot;
using System;

/// <summary>
/// Chat in-game : messages système / squelette (rebonds, analyseur…), saisie joueur (réseau plus tard).
/// Le panneau n’est pas toujours visible : il s’ouvre à l’arrivée d’un message puis se referme après 30 s,
/// ou s’ouvre avec T pour écrire (Entrée envoie, Échap ferme).
/// </summary>
public partial class Joueur
{
    private static readonly string[] CommandesChatConnues =
    {
        "/ADIUTO",
        "/MODUSA RUDI 0",
        "/MODUSA RUDI 1",
        "/MODUSA RUDI 3",
        "/DIMANASIO APISARA",
        "/DIMANASIO ARAPA",
        "/DIMANASIO PETA",
        "/DIMANASIO OMEGA",
        "/DIMANASIO DERATA"
    };

    private static Joueur _joueurFilSquelette;
    private CanvasLayer _coucheFilSquelette;
    private Control _racineChat;
    private RichTextLabel _richFilSquelette;
    private LineEdit _ligneSaisieChat;
    private Label _labelSuggestionsChat;
    private Timer _timerMasquageChatPassif;
    private bool _chatEditionOuverte;
    private string[] _suggestionsCommandesActives = Array.Empty<string>();
    private int _indexSuggestionCommande = -1;
    private bool _miseAJourTexteSuggestionInterne;
    private const int MaxLignesFilSquelette = 18;
    private const float DelaiMasquageChatPassifSec = 15f;

    public bool ChatInGameOuvert() => _chatEditionOuverte;

    public void InitialiserChatInGame()
    {
        _joueurFilSquelette = this;
        if (_coucheFilSquelette != null && GodotObject.IsInstanceValid(_coucheFilSquelette))
            return;

        _coucheFilSquelette = new CanvasLayer
        {
            Name = "FilSqueletteBoiteNoire",
            Layer = 102
        };
        AddChild(_coucheFilSquelette);

        _racineChat = new Control
        {
            Name = "RacineChatInGame",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _racineChat.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _racineChat.AnchorTop = 1f;
        _racineChat.AnchorBottom = 1f;
        _racineChat.OffsetLeft = 8f;
        _racineChat.OffsetRight = -8f;
        _racineChat.OffsetTop = -260f;
        _racineChat.OffsetBottom = -8f;
        _coucheFilSquelette.AddChild(_racineChat);

        var vbox = new VBoxContainer
        {
            Name = "VBoxChat",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _racineChat.AddChild(vbox);

        var lblAide = new Label
        {
            Text = "Chat — T ouvrir | Entree envoyer | Echap fermer | Messages squelette 15 s",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        lblAide.AddThemeFontSizeOverride("font_size", 11);
        lblAide.AddThemeColorOverride("font_color", new Color(0.75f, 0.8f, 0.9f));
        vbox.AddChild(lblAide);

        var panneauHistorique = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vbox.AddChild(panneauHistorique);

        _richFilSquelette = new RichTextLabel
        {
            Name = "TexteFilChat",
            BbcodeEnabled = false,
            FitContent = false,
            ScrollActive = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _richFilSquelette.AddThemeFontSizeOverride("normal_font_size", 14);
        _richFilSquelette.AddThemeColorOverride("default_color", new Color(0.92f, 0.95f, 1f));
        panneauHistorique.AddChild(_richFilSquelette);

        _ligneSaisieChat = new LineEdit
        {
            Name = "LigneSaisieChat",
            Visible = false,
            PlaceholderText = "Message (solo pour l'instant, pas de reponse)...",
            MaxLength = 256,
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 32)
        };
        _ligneSaisieChat.AddThemeFontSizeOverride("font_size", 14);
        _ligneSaisieChat.TextSubmitted += OnTexteChatSoumis;
        _ligneSaisieChat.TextChanged += OnTexteChatModifie;
        _ligneSaisieChat.GuiInput += OnGuiInputLigneSaisieChat;
        vbox.AddChild(_ligneSaisieChat);

        _labelSuggestionsChat = new Label
        {
            Name = "LabelSuggestionsCommandesChat",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _labelSuggestionsChat.AddThemeFontSizeOverride("font_size", 12);
        _labelSuggestionsChat.AddThemeColorOverride("font_color", new Color(0.74f, 0.88f, 0.95f));
        vbox.AddChild(_labelSuggestionsChat);

        _timerMasquageChatPassif = new Timer
        {
            Name = "TimerMasquageChatPassif",
            OneShot = true,
            WaitTime = DelaiMasquageChatPassifSec,
            ProcessCallback = Timer.TimerProcessCallback.Idle
        };
        _timerMasquageChatPassif.Timeout += OnTimerMasquageChatPassif;
        _coucheFilSquelette.AddChild(_timerMasquageChatPassif);
    }

    private void OnTimerMasquageChatPassif()
    {
        if (_chatEditionOuverte)
            return;
        if (_racineChat != null && GodotObject.IsInstanceValid(_racineChat))
            _racineChat.Visible = false;
    }

    private void OnTexteChatSoumis(string texteBrut)
    {
        string t = texteBrut?.Trim() ?? "";
        if (string.IsNullOrEmpty(t))
        {
            _ligneSaisieChat?.ReleaseFocus();
            return;
        }
        if (string.Equals(t, "/ADIUTO", StringComparison.OrdinalIgnoreCase))
        {
            AfficherAideCommandesChat();
            MasquerSuggestionsCommandesChat();
            if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
                _ligneSaisieChat.Text = "";
            _ligneSaisieChat?.CallDeferred(LineEdit.MethodName.GrabFocus);
            return;
        }
        if (t.StartsWith("/ADAMINISATATORA", StringComparison.OrdinalIgnoreCase))
        {
            bool envoye = _gestionnaireMonde != null && _gestionnaireMonde.EnvoyerCommandeAdminChat(t);
            if (envoye)
                PousserLigneChatHistorique("[Commande bootstrap admin envoyee.]", prefixerSquelette: false);
            else
                PousserLigneChatHistorique("[Erreur] commande admin impossible hors mode reseau serveur.", prefixerSquelette: false);
            MasquerSuggestionsCommandesChat();
            if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
                _ligneSaisieChat.Text = "";
            _ligneSaisieChat?.CallDeferred(LineEdit.MethodName.GrabFocus);
            return;
        }
        if (t.StartsWith("/MODUSA", StringComparison.OrdinalIgnoreCase))
        {
            bool envoye = _gestionnaireMonde != null && _gestionnaireMonde.EnvoyerCommandeAdminChat(t);
            if (envoye)
                PousserLigneChatHistorique("[Commande admin envoyee] " + t, prefixerSquelette: false);
            else
                PousserLigneChatHistorique("[Erreur] commande admin impossible hors mode reseau serveur.", prefixerSquelette: false);
            MasquerSuggestionsCommandesChat();
            if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
                _ligneSaisieChat.Text = "";
            _ligneSaisieChat?.CallDeferred(LineEdit.MethodName.GrabFocus);
            return;
        }
        if (t.StartsWith("/DIMANASIO", StringComparison.OrdinalIgnoreCase))
        {
            bool envoye = _gestionnaireMonde != null && _gestionnaireMonde.EnvoyerCommandeAdminChat(t);
            if (envoye)
                PousserLigneChatHistorique("[Commande dimension envoyee] " + t, prefixerSquelette: false);
            else
                PousserLigneChatHistorique("[Erreur] commande dimension impossible hors mode reseau serveur.", prefixerSquelette: false);
            MasquerSuggestionsCommandesChat();
            if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
                _ligneSaisieChat.Text = "";
            _ligneSaisieChat?.CallDeferred(LineEdit.MethodName.GrabFocus);
            return;
        }
        PousserLigneChatHistorique("[Moi] " + t, prefixerSquelette: false);
        GD.Print($"ZERO-K Chat joueur : {t}");
        MasquerSuggestionsCommandesChat();
        if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
            _ligneSaisieChat.Text = "";
        _ligneSaisieChat?.CallDeferred(LineEdit.MethodName.GrabFocus);
    }

    private void AfficherAideCommandesChat()
    {
        PousserLigneChatHistorique("[Aide] Commandes chat:", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /ADIUTO -> affiche cette aide.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /MODUSA RUDI 0 -> desactive mode creatif + noclip.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /MODUSA RUDI 1 -> active mode creatif.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /MODUSA RUDI 3 -> active mode creatif + noclip.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /DIMANASIO APISARA -> transfert vers APISARA.", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO ARAPA -> retour vers {ConstantesDimensions.NomAlpha}.", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO PETA -> transfert vers {ConstantesDimensions.NomBeta} (meme seed que {ConstantesDimensions.NomAlpha}, fuseau +6h).", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO OMEGA -> transfert vers {ConstantesDimensions.NomOmega} (meme seed que {ConstantesDimensions.NomAlpha}, fuseau +12h).", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO DERATA -> transfert vers {ConstantesDimensions.NomDelta} (meme seed que {ConstantesDimensions.NomAlpha}, fuseau +18h).", prefixerSquelette: false);
    }

    private void OnTexteChatModifie(string texte)
    {
        if (_miseAJourTexteSuggestionInterne)
            return;
        MettreAJourSuggestionsCommandesChat(texte, reinitialiserSelection: true);
    }

    private void OnGuiInputLigneSaisieChat(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode == Key.Up)
        {
            if (EssayerSelectionnerSuggestionCommande(direction: -1))
                _ligneSaisieChat?.AcceptEvent();
            return;
        }

        if (keyEvent.Keycode == Key.Down)
        {
            if (EssayerSelectionnerSuggestionCommande(direction: 1))
                _ligneSaisieChat?.AcceptEvent();
            return;
        }
    }

    private bool EssayerSelectionnerSuggestionCommande(int direction)
    {
        if (_ligneSaisieChat == null || !GodotObject.IsInstanceValid(_ligneSaisieChat))
            return false;

        if (_suggestionsCommandesActives.Length == 0)
            MettreAJourSuggestionsCommandesChat(_ligneSaisieChat.Text, reinitialiserSelection: true);
        if (_suggestionsCommandesActives.Length == 0)
            return false;

        int n = _suggestionsCommandesActives.Length;
        if (_indexSuggestionCommande < 0)
            _indexSuggestionCommande = direction < 0 ? n - 1 : 0;
        else
            _indexSuggestionCommande = (_indexSuggestionCommande + direction + n) % n;

        string suggestion = _suggestionsCommandesActives[_indexSuggestionCommande];
        _miseAJourTexteSuggestionInterne = true;
        _ligneSaisieChat.Text = suggestion;
        _ligneSaisieChat.CaretColumn = _ligneSaisieChat.Text.Length;
        _miseAJourTexteSuggestionInterne = false;

        RafraichirLabelSuggestionsCommandesChat();
        return true;
    }

    private void MettreAJourSuggestionsCommandesChat(string texteCourant, bool reinitialiserSelection)
    {
        if (string.IsNullOrWhiteSpace(texteCourant) || !texteCourant.StartsWith('/'))
        {
            MasquerSuggestionsCommandesChat();
            return;
        }

        string prefixe = texteCourant.Trim();
        int nb = 0;
        for (int i = 0; i < CommandesChatConnues.Length; i++)
        {
            if (CommandesChatConnues[i].StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                nb++;
        }
        if (nb == 0)
        {
            MasquerSuggestionsCommandesChat();
            return;
        }

        _suggestionsCommandesActives = new string[nb];
        int j = 0;
        for (int i = 0; i < CommandesChatConnues.Length; i++)
        {
            if (CommandesChatConnues[i].StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                _suggestionsCommandesActives[j++] = CommandesChatConnues[i];
        }

        if (reinitialiserSelection)
            _indexSuggestionCommande = -1;

        RafraichirLabelSuggestionsCommandesChat();
    }

    private void RafraichirLabelSuggestionsCommandesChat()
    {
        if (_labelSuggestionsChat == null || !GodotObject.IsInstanceValid(_labelSuggestionsChat))
            return;
        if (_suggestionsCommandesActives.Length == 0)
        {
            _labelSuggestionsChat.Visible = false;
            _labelSuggestionsChat.Text = "";
            return;
        }

        _labelSuggestionsChat.Visible = true;
        string texte = "Suggestions commandes (Fleche Haut/Bas puis Entree): ";
        int maxAffichage = Math.Min(_suggestionsCommandesActives.Length, 4);
        for (int i = 0; i < maxAffichage; i++)
        {
            bool selectionnee = i == _indexSuggestionCommande;
            if (i > 0)
                texte += " | ";
            texte += selectionnee ? "[" + _suggestionsCommandesActives[i] + "]" : _suggestionsCommandesActives[i];
        }
        if (_suggestionsCommandesActives.Length > maxAffichage)
            texte += " | ...";

        _labelSuggestionsChat.Text = texte;
    }

    private void MasquerSuggestionsCommandesChat()
    {
        _suggestionsCommandesActives = Array.Empty<string>();
        _indexSuggestionCommande = -1;
        if (_labelSuggestionsChat != null && GodotObject.IsInstanceValid(_labelSuggestionsChat))
        {
            _labelSuggestionsChat.Visible = false;
            _labelSuggestionsChat.Text = "";
        }
    }

    private void RedemarrerTimerMasquagePassif()
    {
        if (_timerMasquageChatPassif == null || !GodotObject.IsInstanceValid(_timerMasquageChatPassif))
            return;
        if (_chatEditionOuverte)
            return;
        _timerMasquageChatPassif.Stop();
        _timerMasquageChatPassif.Start();
    }

    private void PousserLigneChatHistorique(string ligneComplete, bool prefixerSquelette)
    {
        if (_richFilSquelette == null || !GodotObject.IsInstanceValid(_richFilSquelette))
            return;

        string ligne = prefixerSquelette ? "[Squelette] " + ligneComplete.Trim() : ligneComplete.Trim();
        if (string.IsNullOrEmpty(_richFilSquelette.Text))
            _richFilSquelette.Text = ligne;
        else
            _richFilSquelette.Text = _richFilSquelette.Text + "\n" + ligne;

        string[] lignes = _richFilSquelette.Text.Split('\n');
        if (lignes.Length > MaxLignesFilSquelette)
            _richFilSquelette.Text = string.Join("\n", lignes, lignes.Length - MaxLignesFilSquelette, MaxLignesFilSquelette);

        CallDeferred(nameof(DefilerFilSqueletteEnBas));
    }

    private void AfficherPanneauChatPourMessage()
    {
        if (_racineChat == null || !GodotObject.IsInstanceValid(_racineChat))
            return;
        _racineChat.Visible = true;
        if (!_chatEditionOuverte && _ligneSaisieChat != null)
            _ligneSaisieChat.Visible = false;
        // Hors mode saisie : ne pas bloquer les clics du jeu sur le reste de l'ecran.
        _racineChat.MouseFilter = _chatEditionOuverte
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
        RedemarrerTimerMasquagePassif();
    }

    private void PousserMessageDansFilSquelette(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        AfficherPanneauChatPourMessage();
        PousserLigneChatHistorique(message, prefixerSquelette: true);
    }

    private void DefilerFilSqueletteEnBas()
    {
        if (_richFilSquelette == null || !GodotObject.IsInstanceValid(_richFilSquelette))
            return;
        int n = _richFilSquelette.GetLineCount();
        if (n > 0)
            _richFilSquelette.ScrollToLine(n - 1);
    }

    public static void AlerteSqueletteBoiteNoire(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        GD.Print($"ZERO-K Squelette : {message}");
        Joueur j = _joueurFilSquelette;
        if (j != null && GodotObject.IsInstanceValid(j))
            j.PousserMessageDansFilSquelette(message);
    }

    /// <summary>T ouvre le chat pour écrire. Quand le chat est déjà ouvert à l’édition, T n’est pas consommé (frappe dans le champ).</summary>
    public bool EssayerBasculerChatInGameDepuisInput(InputEvent @event)
    {
        if (@event is not InputEventKey ek || !ek.Pressed || ek.Echo)
            return false;
        if (_chatEditionOuverte)
            return false;
        if (UiJoueurBloquanteHorsChatOuverte())
            return false;
        if (SaisieTexteUiEnCours())
            return false;
        bool estT = ek.Keycode == Key.T || ek.PhysicalKeycode == Key.T;
        if (!estT)
            return false;
        if (ek.CtrlPressed || ek.MetaPressed || ek.AltPressed)
            return false;

        OuvrirChatInGame();
        return true;
    }

    public void OuvrirChatInGame()
    {
        if (_racineChat == null || !GodotObject.IsInstanceValid(_racineChat))
            InitialiserChatInGame();
        _chatEditionOuverte = true;
        if (_timerMasquageChatPassif != null)
            _timerMasquageChatPassif.Stop();
        _racineChat.Visible = true;
        _racineChat.MouseFilter = Control.MouseFilterEnum.Stop;
        if (_ligneSaisieChat != null)
        {
            _ligneSaisieChat.Visible = true;
            MettreAJourSuggestionsCommandesChat(_ligneSaisieChat.Text, reinitialiserSelection: true);
            _ligneSaisieChat.CallDeferred(LineEdit.MethodName.GrabFocus);
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void FermerChatInGame()
    {
        _chatEditionOuverte = false;
        if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
        {
            _ligneSaisieChat.Visible = false;
            _ligneSaisieChat.ReleaseFocus();
        }
        MasquerSuggestionsCommandesChat();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _timerMasquageChatPassif?.Stop();
        if (_racineChat != null && GodotObject.IsInstanceValid(_racineChat))
            _racineChat.Visible = false;
    }

    /// <summary>À appeler depuis les branches UI qui bloquent le clavier (menu Q, menu K) pour que T ouvre quand même le chat.</summary>
    public bool EssayerOuvrirChatDepuisToucheT(InputEvent @event) => EssayerBasculerChatInGameDepuisInput(@event);

    private bool UiJoueurBloquanteHorsChatOuverte()
    {
        return (_modelisateur != null && _modelisateur.EstOuvert)
            || (_menuFutureState != null && _menuFutureState.EstOuvert)
            || (_menuAnatomie != null && _menuAnatomie.EstOuvert)
            || CarnetSavoirOuvert();
    }

    private bool SaisieTexteUiEnCours()
    {
        Control focus = GetViewport()?.GuiGetFocusOwner();
        if (focus == null || !GodotObject.IsInstanceValid(focus))
            return false;
        if (focus == _ligneSaisieChat)
            return false;
        if (focus is LineEdit ligne)
            return ligne.Editable;
        if (focus is TextEdit zone)
            return zone.Editable;
        return false;
    }
}
