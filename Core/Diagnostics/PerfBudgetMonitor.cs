using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>Collecte légère des durées (microsecondes) et journal périodique compact par catégorie.</summary>
public static class PerfBudgetMonitor
{
	private sealed class Stat
	{
		public long TotalUs;
		public long MaxUs;
		public int Hits;
	}

	private static readonly object VerrouStats = new object();
	private static readonly Dictionary<string, Stat> Stats = new Dictionary<string, Stat>(StringComparer.Ordinal);
	private static readonly StringBuilder BufferLog = new StringBuilder(512);
	private static double _dernierFlushSec;

	public static ulong Begin()
	{
		return Time.GetTicksUsec();
	}

	public static void End(string cle, ulong debutUs)
	{
		if (string.IsNullOrWhiteSpace(cle) || debutUs == 0UL)
			return;
		ulong maintenantUs = Time.GetTicksUsec();
		long us = (long)Math.Max(0, (double)(maintenantUs >= debutUs ? maintenantUs - debutUs : 0UL));
		if (us < 0L)
			us = 0L;
		lock (VerrouStats)
		{
			if (!Stats.TryGetValue(cle, out Stat s))
			{
				s = new Stat();
				Stats[cle] = s;
			}
			s.TotalUs += us;
			if (us > s.MaxUs)
				s.MaxUs = us;
			s.Hits++;
		}
	}

	public static void FlushSiEchu(string prefixe, float intervalleSecondes, bool force = false)
	{
		if (string.IsNullOrWhiteSpace(prefixe))
			return;
		double maintenant = Time.GetTicksMsec() / 1000.0;
		if (!force && (maintenant - _dernierFlushSec) < Math.Max(0.25f, intervalleSecondes))
			return;
		_dernierFlushSec = maintenant;

		lock (VerrouStats)
		{
			BufferLog.Clear();
			BufferLog.Append("PERF ").Append(prefixe).Append(" -> ");
			int ecrit = 0;
			foreach ((string cle, Stat s) in Stats)
			{
				if (!cle.StartsWith(prefixe, StringComparison.Ordinal))
					continue;
				if (s.Hits <= 0)
					continue;
				if (ecrit > 0)
					BufferLog.Append(" | ");
				double moyenneMs = (s.TotalUs / (double)s.Hits) / 1000.0;
				double maxMs = s.MaxUs / 1000.0;
				string suffixe = cle.Substring(prefixe.Length);
				if (suffixe.StartsWith("/", StringComparison.Ordinal))
					suffixe = suffixe.Substring(1);
				BufferLog
					.Append(suffixe)
					.Append(":avg=")
					.Append(moyenneMs.ToString("F2"))
					.Append("ms,max=")
					.Append(maxMs.ToString("F2"))
					.Append("ms,n=")
					.Append(s.Hits);
				s.TotalUs = 0L;
				s.MaxUs = 0L;
				s.Hits = 0;
				ecrit++;
			}
			if (ecrit > 0)
				GD.Print(BufferLog.ToString());
		}
	}
}
