using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Runner de capture vidéo automatique du vrai monde (terrain/faune/ciel/eau) via une caméra cinématique.
/// Scène de test dédiée : aucun impact sur la partie standard.
/// </summary>
public partial class ExplorationAutoMondeRunner : Node3D
{
    private const int FpsCapture = 30;
    private const int DureeTotaleSecondes = 60;
    private const int DureePhase1Secondes = 5;
    private const int DureePhase2Secondes = 25;
    private const int DureePhase3Secondes = 20;
    private const int DureePhase4Secondes = 10;
    private const int FramesTotales = FpsCapture * DureeTotaleSecondes;

    private Node3D _mondeInstance;
    private Gestionnaire_Monde _gestionnaireMonde;
    private Joueur _joueur;
    private Cycle_Solaire _cycleSolaire;
    private Camera3D _cameraCinematique;
    private CanvasLayer _overlay;
    private Label _labelOverlay;

    private int _frameCapture;
    private int _frameAttente;
    private bool _captureDemarree;
    private bool _quitte;
    private float _angleBase;
    private readonly List<float> _frameTimesMs = new();
    private int _spikes18ms;
    private int _spikes33ms;
    private int _spikes50ms;
    private ulong _memoireDebut;

    public override void _Ready()
    {
        PackedScene sceneMonde = GD.Load<PackedScene>("res://monde_zero.tscn");
        if (sceneMonde == null)
        {
            GD.PrintErr("ExplorationAutoMonde: impossible de charger res://monde_zero.tscn");
            GetTree()?.Quit(1);
            return;
        }

        _mondeInstance = sceneMonde.Instantiate<Node3D>();
        _mondeInstance.Name = "MondeZeroInstance";
        AddChild(_mondeInstance);

        _cameraCinematique = new Camera3D
        {
            Name = "CameraCinematique",
            Current = true,
            Near = 0.05f,
            Far = 5000f,
            Fov = 65f
        };
        AddChild(_cameraCinematique);

        _overlay = new CanvasLayer { Name = "OverlayExploration" };
        _labelOverlay = new Label
        {
            Name = "LabelInfo",
            Text = "Exploration auto: attente du monde...",
            Position = new Vector2(16f, 16f)
        };
        _overlay.AddChild(_labelOverlay);
        AddChild(_overlay);

        _gestionnaireMonde = _mondeInstance.GetNodeOrNull<Gestionnaire_Monde>("Gestionnaire_Monde");
        _joueur = _mondeInstance.GetNodeOrNull<Joueur>("Joueur");
        _cycleSolaire = _mondeInstance.GetNodeOrNull<Cycle_Solaire>("CycleSolaire");

        if (_gestionnaireMonde == null || _joueur == null)
        {
            GD.PrintErr("ExplorationAutoMonde: noeuds obligatoires introuvables (Gestionnaire_Monde/Joueur).");
            GetTree()?.Quit(1);
            return;
        }

        // Forcer un midi local pour une lumière stable pendant la capture.
        DateTime utc = DateTime.UtcNow;
        double offsetMidi = 12.0 - (utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0);
        _cycleSolaire?.DefinirDecalageHoraire(offsetMidi);

        Vector3 centre = _joueur.GlobalPosition;
        _cameraCinematique.GlobalPosition = centre + new Vector3(0f, 55f, 55f);
        _cameraCinematique.LookAt(centre, Vector3.Up);

        GD.Print("ExplorationAutoMonde: runner prêt, attente spawn prêt + alignement.");
    }

    public override void _Process(double delta)
    {
        if (_quitte || _cameraCinematique == null || _gestionnaireMonde == null || _joueur == null)
            return;

        bool mondePret = _gestionnaireMonde.EstSpawnPret() && _gestionnaireMonde.EstAlignementSpawnTermine();
        if (!_captureDemarree)
        {
            _frameAttente++;
            Vector3 centreAttente = _joueur.GlobalPosition;
            _cameraCinematique.GlobalPosition = centreAttente + new Vector3(0f, 52f, 48f);
            _cameraCinematique.LookAt(centreAttente, Vector3.Up);

            if (mondePret)
            {
                _joueur.DesactiverCamerasPourCameraExterne();
                _cameraCinematique.Current = true;
                _captureDemarree = true;
                _frameCapture = 0;
                _angleBase = 0f;
                _memoireDebut = (ulong)GC.GetTotalMemory(false);
                _frameTimesMs.Clear();
                _spikes18ms = 0;
                _spikes33ms = 0;
                _spikes50ms = 0;
                if (_labelOverlay != null)
                    _labelOverlay.Text = "Exploration auto: capture en cours (phase 1/4)";
                GD.Print("ExplorationAutoMonde: monde prêt, démarrage trajectoire caméra.");
            }
            return;
        }

        float frameMs = (float)(delta * 1000.0);
        _frameTimesMs.Add(frameMs);
        if (frameMs >= 18f) _spikes18ms++;
        if (frameMs >= 33f) _spikes33ms++;
        if (frameMs >= 50f) _spikes50ms++;

        _frameCapture++;
        MettreAJourTrajectoireCamera(_frameCapture);

        if (_frameCapture >= FramesTotales)
        {
            _quitte = true;
            if (_labelOverlay != null)
                _labelOverlay.Text = "Exploration auto: terminé, fermeture...";
            EcrireRapportMetrics();
            GD.Print("ExplorationAutoMonde: capture terminée.");
            GetTree()?.Quit(0);
        }
    }

    private void MettreAJourTrajectoireCamera(int frame)
    {
        Vector3 centre = _joueur.GlobalPosition;
        int p1Fin = DureePhase1Secondes * FpsCapture;
        int p2Fin = p1Fin + DureePhase2Secondes * FpsCapture;
        int p3Fin = p2Fin + DureePhase3Secondes * FpsCapture;

        if (frame <= p1Fin)
        {
            float t = Mathf.Clamp((float)frame / Mathf.Max(1f, p1Fin), 0f, 1f);
            Vector3 pos = centre + new Vector3(0f, Mathf.Lerp(50f, 44f, t), Mathf.Lerp(46f, 38f, t));
            _cameraCinematique.GlobalPosition = pos;
            _cameraCinematique.LookAt(centre, Vector3.Up);
            MettreTexteOverlay("phase 1/4 - attente/intro");
            return;
        }

        if (frame <= p2Fin)
        {
            float t = Mathf.Clamp((float)(frame - p1Fin) / Mathf.Max(1f, DureePhase2Secondes * FpsCapture), 0f, 1f);
            float angle = _angleBase + t * Mathf.Tau * 1.15f;
            float rayon = 40f;
            float hauteur = 30f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * rayon, hauteur, Mathf.Sin(angle) * rayon);
            _cameraCinematique.GlobalPosition = centre + offset;
            _cameraCinematique.LookAt(centre + new Vector3(0f, 6f, 0f), Vector3.Up);
            MettreTexteOverlay("phase 2/4 - orbite panoramique");
            return;
        }

        if (frame <= p3Fin)
        {
            float t = Mathf.Clamp((float)(frame - p2Fin) / Mathf.Max(1f, DureePhase3Secondes * FpsCapture), 0f, 1f);
            float angle = _angleBase + Mathf.Tau * 1.15f + t * Mathf.Tau * 0.9f;
            float rayon = Mathf.Lerp(32f, 15f, t);
            float hauteur = Mathf.Lerp(22f, 9f, t);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * rayon, hauteur, Mathf.Sin(angle) * rayon);
            _cameraCinematique.GlobalPosition = centre + offset;
            _cameraCinematique.LookAt(centre + new Vector3(0f, 2.5f, 0f), Vector3.Up);
            MettreTexteOverlay("phase 3/4 - descente faune");
            return;
        }

        {
            float t = Mathf.Clamp((float)(frame - p3Fin) / Mathf.Max(1f, DureePhase4Secondes * FpsCapture), 0f, 1f);
            float angle = _angleBase + Mathf.Tau * 2.05f + t * Mathf.Tau * 0.35f;
            float rayon = Mathf.Lerp(15f, 42f, t);
            float hauteur = Mathf.Lerp(10f, 52f, t);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * rayon, hauteur, Mathf.Sin(angle) * rayon);
            _cameraCinematique.GlobalPosition = centre + offset;
            _cameraCinematique.LookAt(centre + new Vector3(0f, 3f, 0f), Vector3.Up);
            MettreTexteOverlay("phase 4/4 - recul final");
        }
    }

    private void MettreTexteOverlay(string phase)
    {
        if (_labelOverlay == null)
            return;
        _labelOverlay.Text = $"Exploration auto: {phase} - frame {_frameCapture}/{FramesTotales}";
    }

    private void EcrireRapportMetrics()
    {
        try
        {
            string chemin = ProjectSettings.GlobalizePath("res://artifacts/exploration_perf_metrics.log");
            Directory.CreateDirectory(Path.GetDirectoryName(chemin) ?? ".");
            var copy = new List<float>(_frameTimesMs);
            copy.Sort();
            float p95 = Percentile(copy, 0.95f);
            float p99 = Percentile(copy, 0.99f);
            float max = copy.Count > 0 ? copy[^1] : 0f;
            float moyenne = 0f;
            for (int i = 0; i < _frameTimesMs.Count; i++)
                moyenne += _frameTimesMs[i];
            if (_frameTimesMs.Count > 0) moyenne /= _frameTimesMs.Count;
            ulong memFin = (ulong)GC.GetTotalMemory(false);

            using var w = new StreamWriter(File.Open(chemin, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read));
            w.WriteLine($"frames_capture={_frameCapture}");
            w.WriteLine($"frames_attente_spawn={_frameAttente}");
            w.WriteLine($"frametime_ms_avg={moyenne:F3}");
            w.WriteLine($"frametime_ms_p95={p95:F3}");
            w.WriteLine($"frametime_ms_p99={p99:F3}");
            w.WriteLine($"frametime_ms_max={max:F3}");
            w.WriteLine($"spikes_ge_18ms={_spikes18ms}");
            w.WriteLine($"spikes_ge_33ms={_spikes33ms}");
            w.WriteLine($"spikes_ge_50ms={_spikes50ms}");
            w.WriteLine($"gc_mem_start_bytes={_memoireDebut}");
            w.WriteLine($"gc_mem_end_bytes={memFin}");
            w.WriteLine($"gc_mem_delta_bytes={(long)memFin - (long)_memoireDebut}");
            w.WriteLine("EXPLORATION_PERF_RESULT=OK");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ExplorationAutoMonde: échec écriture métriques perf: {ex.Message}");
        }
    }

    private static float Percentile(List<float> sorted, float p)
    {
        if (sorted == null || sorted.Count == 0) return 0f;
        float clamped = Mathf.Clamp(p, 0f, 1f);
        int idx = Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * clamped), 0, sorted.Count - 1);
        return sorted[idx];
    }
}
