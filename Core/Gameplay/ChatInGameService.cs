using Godot;
using System;

/// <summary>
/// Chat in-game : messages système / squelette (rebonds, analyseur…), saisie joueur (réseau plus tard).
/// Le panneau n’est pas toujours visible : il s’ouvre à l’arrivée d’un message puis se referme après 30 s,
/// ou s’ouvre avec T pour écrire (Entrée envoie, Échap ferme).
/// </summary>
public partial class Joueur
{
    private static Joueur _joueurFilSquelette;
    private CanvasLayer _coucheFilSquelette;
    private Control _racineChat;
    private RichTextLabel _richFilSquelette;
    private LineEdit _ligneSaisieChat;
    private Timer _timerMasquageChatPassif;
    private bool _chatEditionOuverte;
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
        vbox.AddChild(_ligneSaisieChat);

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
        PousserLigneChatHistorique("[Moi] " + t, prefixerSquelette: false);
        GD.Print($"ZERO-K Chat joueur : {t}");
        if (_ligneSaisieChat != null && GodotObject.IsInstanceValid(_ligneSaisieChat))
            _ligneSaisieChat.Text = "";
        _ligneSaisieChat?.CallDeferred(LineEdit.MethodName.GrabFocus);
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
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _timerMasquageChatPassif?.Stop();
        if (_racineChat != null && GodotObject.IsInstanceValid(_racineChat))
            _racineChat.Visible = false;
    }

    /// <summary>À appeler depuis les branches UI qui bloquent le clavier (menu Q, menu K) pour que T ouvre quand même le chat.</summary>
    public bool EssayerOuvrirChatDepuisToucheT(InputEvent @event) => EssayerBasculerChatInGameDepuisInput(@event);
}
