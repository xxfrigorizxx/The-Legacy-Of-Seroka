using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Journal fichier user://logs/ + anti-spam console pour les erreurs répétées (mesh, chunks, persistence).
/// </summary>
public static class JournalErreursZeroK
{
	private sealed class EntreeThrottle
	{
		public int Total;
		public int AfficheConsole;
		public double DernierAffichageSec;
	}

	private static readonly object Verrou = new object();
	private static readonly Dictionary<string, EntreeThrottle> Throttle = new Dictionary<string, EntreeThrottle>(StringComparer.Ordinal);
	private static FileAccess _fichier;
	private static string _cheminJournal;
	private static bool _initialise;

	/// <summary>Max affichages console identiques avant silence (toujours écrit en fichier).</summary>
	public const int MaxAffichagesConsoleParMessage = 2;
	public const double IntervalleMinAffichageConsoleSec = 4.0;

	public static string CheminJournalActif => _cheminJournal ?? "";

	public static void Initialiser()
	{
		if (_initialise || Engine.IsEditorHint())
			return;
		lock (Verrou)
		{
			if (_initialise)
				return;
			try
			{
				string dossier = "user://logs";
				DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dossier));
				string horodatage = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
				_cheminJournal = $"{dossier}/zero_k_{horodatage}.log";
				_fichier = FileAccess.Open(_cheminJournal, FileAccess.ModeFlags.Write);
				if (_fichier != null)
				{
					_fichier.StoreLine($"=== ZERO-K journal {horodatage} ===");
					_fichier.Flush();
				}
				GD.Print($"ZERO-K : journal erreurs -> {ProjectSettings.GlobalizePath(_cheminJournal)}");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"ZERO-K : impossible d'ouvrir le journal erreurs : {ex.Message}");
			}
			_initialise = true;
		}
	}

	public static void Erreur(string message, bool forcerConsole = false)
	{
		if (string.IsNullOrWhiteSpace(message))
			return;
		if (!_initialise)
			Initialiser();

		string ligne = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
		lock (Verrou)
		{
			try
			{
				_fichier?.StoreLine(ligne);
				if (_fichier != null && Throttle.Count % 16 == 0)
					_fichier.Flush();
			}
			catch { /* ignore I/O */ }

			if (!Throttle.TryGetValue(message, out EntreeThrottle entree))
			{
				entree = new EntreeThrottle();
				Throttle[message] = entree;
			}
			entree.Total++;
			double maintenant = Time.GetTicksMsec() / 1000.0;
			bool peutAfficher = forcerConsole
				|| entree.AfficheConsole < MaxAffichagesConsoleParMessage
				|| (maintenant - entree.DernierAffichageSec) >= IntervalleMinAffichageConsoleSec;
			if (!peutAfficher)
				return;
			entree.AfficheConsole++;
			entree.DernierAffichageSec = maintenant;
			string suffixe = entree.Total > entree.AfficheConsole
				? $" (occurrence #{entree.Total}, détail dans le journal)"
				: "";
			GD.PrintErr(message + suffixe);
		}
	}

	public static void Avertissement(string message)
	{
		Erreur("[WARN] " + message);
	}

	public static void Flush()
	{
		lock (Verrou)
		{
			try { _fichier?.Flush(); } catch { /* ignore */ }
		}
	}

	public static string ResumerThrottlePourJournal(int maxLignes = 24)
	{
		lock (Verrou)
		{
			var sb = new StringBuilder(512);
			int n = 0;
			foreach (var kv in Throttle)
			{
				if (kv.Value.Total <= kv.Value.AfficheConsole)
					continue;
				sb.Append(kv.Key).Append(" ×").Append(kv.Value.Total).AppendLine();
				if (++n >= maxLignes)
				{
					sb.AppendLine("…");
					break;
				}
			}
			return sb.Length > 0 ? sb.ToString() : "(aucune erreur throttle)";
		}
	}
}
