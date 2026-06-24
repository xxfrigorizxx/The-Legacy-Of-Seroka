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
        "/INVOCA BOVA",
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
    private bool _messagesRecentsEnAttenteAffichageApresUi;
    private string[] _suggestionsCommandesActives = Array.Empty<string>();
    private int _indexSuggestionCommande = -1;
    private bool _miseAJourTexteSuggestionInterne;
    private const int MaxLignesFilSquelette = 18;
    private const float DelaiMasquageChatPassifSec = 15f;
    private const float DelaiMasquageChatAnalyseurSec = 2f;
    private float _delaiMasquageMessageEnAttenteSec = DelaiMasquageChatPassifSec;

    public bool ChatInGameOuvert() => _chatEditionOuverte;

    public void InitialiserChatInGame()
    {
        if (!EstJoueurLocalPourChat())
            return;
        _joueurFilSquelette = this;
        if (_coucheFilSquelette != null && GodotObject.IsInstanceValid(_coucheFilSquelette))
            return;

        _coucheFilSquelette = new CanvasLayer
        {
            Name = "FilSqueletteBoiteNoire",
            // Sous l'UI joueur, donc jamais bloquant visuellement.
            Layer = 99
        };
        AddChild(_coucheFilSquelette);

        _racineChat = new Control
        {
            Name = "RacineChatInGame",
            Visible = false,
            // Le flux passif ne capte pas les clics.
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        // Zone en haut d'ecran: visible meme quand des panneaux joueur occupent le bas.
        _racineChat.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _racineChat.AnchorTop = 0f;
        _racineChat.AnchorBottom = 0f;
        _racineChat.OffsetLeft = 8f;
        _racineChat.OffsetRight = -8f;
        _racineChat.OffsetTop = 8f;
        _racineChat.OffsetBottom = 260f;
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
            Text = "Chat — T ouvrir | Entree envoyer | Echap fermer | Messages squelette 15 s (analyseur 2 s)",
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
            KeepEditingOnTextSubmit = true,
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
            return;
        if (string.Equals(t, "/ADIUTO", StringComparison.OrdinalIgnoreCase))
        {
            AfficherAideCommandesChat();
            MasquerSuggestionsCommandesChat();
            if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
            {
                _ligneSaisieChat.Text = "";
                _ligneSaisieChat.CaretColumn = 0;
            }
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
            {
                _ligneSaisieChat.Text = "";
                _ligneSaisieChat.CaretColumn = 0;
            }
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
            {
                _ligneSaisieChat.Text = "";
                _ligneSaisieChat.CaretColumn = 0;
            }
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
            {
                _ligneSaisieChat.Text = "";
                _ligneSaisieChat.CaretColumn = 0;
            }
            return;
        }
        if (t.StartsWith("/INVOCA", StringComparison.OrdinalIgnoreCase))
        {
            TraiterCommandeInvocationFaune(t);
            MasquerSuggestionsCommandesChat();
            if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
            {
                _ligneSaisieChat.Text = "";
                _ligneSaisieChat.CaretColumn = 0;
            }
            return;
        }
        PousserLigneChatHistorique("[Moi] " + t, prefixerSquelette: false);
        GD.Print($"ZERO-K Chat joueur : {t}");
        MasquerSuggestionsCommandesChat();
        if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
        {
            _ligneSaisieChat.Text = "";
            _ligneSaisieChat.CaretColumn = 0;
        }
    }

    private void AfficherAideCommandesChat()
    {
        PousserLigneChatHistorique("[Aide] Commandes chat:", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /ADIUTO -> affiche cette aide.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /INVOCA BOVA [n] -> fait apparaitre un troupeau de bovins autour de toi (defaut 6, max 24).", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /MODUSA RUDI 0 -> desactive mode creatif + noclip.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /MODUSA RUDI 1 -> active mode creatif.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /MODUSA RUDI 3 -> active mode creatif + noclip.", prefixerSquelette: false);
        PousserLigneChatHistorique("[Aide] /DIMANASIO APISARA -> transfert vers APISARA.", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO ARAPA -> retour vers {ConstantesDimensions.NomAlpha}.", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO PETA -> transfert vers {ConstantesDimensions.NomBeta} (meme seed que {ConstantesDimensions.NomAlpha}, fuseau +6h).", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO OMEGA -> transfert vers {ConstantesDimensions.NomOmega} (meme seed que {ConstantesDimensions.NomAlpha}, fuseau +12h).", prefixerSquelette: false);
        PousserLigneChatHistorique($"[Aide] /DIMANASIO DERATA -> transfert vers {ConstantesDimensions.NomDelta} (meme seed que {ConstantesDimensions.NomAlpha}, fuseau +18h).", prefixerSquelette: false);
    }

    /// <summary>/INVOCA BOVA [n] : fait apparaitre un troupeau de bovins autour du joueur (debug/creatif, contourne le gate FPS et la restriction plaine).</summary>
    private void TraiterCommandeInvocationFaune(string commande)
    {
        string[] parties = (commande ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool cibleBovins = parties.Length >= 2 && parties[1].StartsWith("BOVA", StringComparison.OrdinalIgnoreCase);
        if (!cibleBovins)
        {
            PousserLigneChatHistorique("[Invoca] Usage : /INVOCA BOVA [nombre] -> fait apparaitre un troupeau de bovins.", prefixerSquelette: false);
            return;
        }
        int taille = 6;
        if (parties.Length >= 3 && int.TryParse(parties[2], out int n))
            taille = Mathf.Clamp(n, 1, 24);

        GestionnaireFauneBoeufs faune = TrouverGestionnaireFauneBoeufs();
        if (faune == null)
        {
            PousserLigneChatHistorique("[Invoca] Erreur : gestionnaire de faune introuvable.", prefixerSquelette: false);
            return;
        }
        int spawnes = faune.ForcerApparitionTroupeauAuJoueur(taille);
        if (spawnes > 0)
            PousserLigneChatHistorique($"[Invoca] Troupeau invoque : {spawnes} bovin(s) autour de toi.", prefixerSquelette: false);
        else
            PousserLigneChatHistorique("[Invoca] Aucun bovin n'a pu apparaitre (sol introuvable, ou dimension sans faune bovine).", prefixerSquelette: false);
    }

    private GestionnaireFauneBoeufs TrouverGestionnaireFauneBoeufs()
    {
        Node scene = GetTree()?.CurrentScene;
        GestionnaireFauneBoeufs direct = scene?.GetNodeOrNull<GestionnaireFauneBoeufs>("GestionnaireFauneBoeufs");
        if (direct != null && GodotObject.IsInstanceValid(direct))
            return direct;
        GestionnaireFauneBoeufs viaParent = GetParent()?.GetNodeOrNull<GestionnaireFauneBoeufs>("GestionnaireFauneBoeufs");
        if (viaParent != null && GodotObject.IsInstanceValid(viaParent))
            return viaParent;
        return scene != null ? TrouverPremierNoeudDeTypeChat<GestionnaireFauneBoeufs>(scene) : null;
    }

    private static T TrouverPremierNoeudDeTypeChat<T>(Node racine) where T : Node
    {
        if (racine is T t)
            return t;
        foreach (Node enfant in racine.GetChildren())
        {
            T trouve = TrouverPremierNoeudDeTypeChat<T>(enfant);
            if (trouve != null)
                return trouve;
        }
        return null;
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

        if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter)
        {
            // Evite que ui_accept (Entrée) retombe vers les handlers gameplay.
            _ligneSaisieChat?.AcceptEvent();
            return;
        }

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

    private void RedemarrerTimerMasquagePassif(float delaiSec = DelaiMasquageChatPassifSec)
    {
        if (_timerMasquageChatPassif == null || !GodotObject.IsInstanceValid(_timerMasquageChatPassif))
            return;
        if (_chatEditionOuverte)
            return;
        _timerMasquageChatPassif.WaitTime = Mathf.Max(0.1f, delaiSec);
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

    private void AfficherPanneauChatPourMessage(float delaiMasquageSec)
    {
        if (_racineChat == null || !GodotObject.IsInstanceValid(_racineChat))
            return;
        bool uiBloquanteOuverte = UiJoueurBloquanteHorsChatOuverte();
        if (!_chatEditionOuverte && uiBloquanteOuverte)
        {
            _messagesRecentsEnAttenteAffichageApresUi = true;
            _delaiMasquageMessageEnAttenteSec = delaiMasquageSec;
            _racineChat.Visible = false;
            _racineChat.MouseFilter = Control.MouseFilterEnum.Ignore;
            return;
        }
        _messagesRecentsEnAttenteAffichageApresUi = false;
        _racineChat.Visible = true;
        if (!_chatEditionOuverte && _ligneSaisieChat != null)
            _ligneSaisieChat.Visible = false;
        // Hors mode saisie : ne pas bloquer les clics du jeu sur le reste de l'ecran.
        _racineChat.MouseFilter = _chatEditionOuverte
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
        RedemarrerTimerMasquagePassif(delaiMasquageSec);
    }

    private void PousserMessageDansFilSquelette(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        if (_racineChat == null || !GodotObject.IsInstanceValid(_racineChat))
            InitialiserChatInGame();
        if (_racineChat == null || !GodotObject.IsInstanceValid(_racineChat))
            return;
        bool estMessageAnalyseur = message.TrimStart().StartsWith("Analyseur :", StringComparison.OrdinalIgnoreCase);
        float delaiMasquage = estMessageAnalyseur ? DelaiMasquageChatAnalyseurSec : DelaiMasquageChatPassifSec;
        AfficherPanneauChatPourMessage(delaiMasquage);
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
        if (j == null || !GodotObject.IsInstanceValid(j) || !j.EstJoueurLocalPourChat())
            j = TrouverJoueurLocalPourChat();
        if (j == null || !GodotObject.IsInstanceValid(j))
            return;
        if (j._racineChat == null || !GodotObject.IsInstanceValid(j._racineChat))
            j.InitialiserChatInGame();
        j.PousserMessageDansFilSquelette(message);
    }

    /// <summary>T ouvre le chat pour écrire. Quand le chat est déjà ouvert à l’édition, T n’est pas consommé (frappe dans le champ).</summary>
    public bool EssayerBasculerChatInGameDepuisInput(InputEvent @event)
    {
        if (@event is not InputEventKey ek || !ek.Pressed || ek.Echo)
            return false;
        bool estT = ek.Keycode == Key.T || ek.PhysicalKeycode == Key.T;
        bool estEntree = ek.Keycode == Key.Enter
            || ek.Keycode == Key.KpEnter
            || ek.PhysicalKeycode == Key.Enter
            || ek.PhysicalKeycode == Key.KpEnter;
        bool modifieur = ek.CtrlPressed || ek.MetaPressed || ek.AltPressed;
        if (_chatEditionOuverte)
        {
            Control focus = GetViewport()?.GuiGetFocusOwner();
            bool focusSurLigneChat = focus == _ligneSaisieChat;

            // T en mode édition: recentre le focus seulement si le champ n'est pas déjà actif.
            if (estT && !modifieur && _ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
            {
                if (focusSurLigneChat)
                    return false; // Laisser la lettre "t" être saisie normalement.
                _ligneSaisieChat.CallDeferred(LineEdit.MethodName.GrabFocus);
                return true;
            }
            // Entrée en mode édition: si le champ n'a pas le focus, on le reprend.
            // Si le champ a déjà le focus, on laisse LineEdit gérer TextSubmitted.
            if (estEntree && !modifieur && _ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
            {
                if (focusSurLigneChat)
                {
                    // Soumission explicite ici pour garantir l'envoi via Entrée dans tous les contextes de propagation input.
                    OnTexteChatSoumis(_ligneSaisieChat.Text);
                    _ligneSaisieChat.AcceptEvent();
                    return true;
                }
                _ligneSaisieChat.CallDeferred(LineEdit.MethodName.GrabFocus);
                return true;
            }
            return false;
        }
        if (UiJoueurBloquanteHorsChatOuverte())
            return false;
        if (SaisieTexteUiEnCours())
            return false;
        // Chat fermé: seule la touche T doit l'ouvrir (pas Entrée).
        if (!estT)
            return false;
        if (modifieur)
            return false;

        OuvrirChatInGame();
        return true;
    }

    public void OuvrirChatInGame()
    {
        if (UiJoueurBloquanteHorsChatOuverte())
            return;
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

    private void MettreAJourVisibiliteChatSelonUiBloquante()
    {
        if (_chatEditionOuverte || _racineChat == null || !GodotObject.IsInstanceValid(_racineChat))
            return;
        bool uiBloquanteOuverte = UiJoueurBloquanteHorsChatOuverte();
        if (uiBloquanteOuverte)
        {
            _racineChat.MouseFilter = Control.MouseFilterEnum.Ignore;
            return;
        }
        if (_messagesRecentsEnAttenteAffichageApresUi)
        {
            _messagesRecentsEnAttenteAffichageApresUi = false;
            _racineChat.Visible = true;
            RedemarrerTimerMasquagePassif(_delaiMasquageMessageEnAttenteSec);
        }
        _racineChat.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private bool EstJoueurLocalPourChat()
    {
        if (!Multiplayer.HasMultiplayerPeer())
            return true;
        return IsMultiplayerAuthority() || GetMultiplayerAuthority() == Multiplayer.GetUniqueId();
    }

    private static Joueur TrouverJoueurLocalPourChat()
    {
        if (Engine.GetMainLoop() is not SceneTree arbre)
            return null;
        return TrouverJoueurLocalPourChatRecursif(arbre.Root);
    }

    private static Joueur TrouverJoueurLocalPourChatRecursif(Node noeud)
    {
        if (noeud is Joueur joueur && joueur.EstJoueurLocalPourChat())
            return joueur;
        foreach (Node enfant in noeud.GetChildren())
        {
            Joueur trouve = TrouverJoueurLocalPourChatRecursif(enfant);
            if (trouve != null)
                return trouve;
        }
        return null;
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
