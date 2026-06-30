using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// ============================================================
//  State turnamen mini per grup: peserta, ronde, pasangan yang sudah main.
//  Disimpan ke data/pairing-tournaments.json (tahan restart brain).
//  Klasemen-nya pakai PairingStandings (di-reset saat turnamen mulai).
// ============================================================

internal static class PairingTournament
{
	private static readonly object _lk = new object();

	private static string DataDir => Path.Combine(Directory.GetCurrentDirectory(), "data");

	private static string FilePath => Path.Combine(DataDir, "pairing-tournaments.json");

	public sealed class TPlayer
	{
		public string Handle { get; set; } = "";
		public string Name { get; set; } = "";
		public string Lid { get; set; } = "";
		public string Phone { get; set; } = "";
	}

	public sealed class TState
	{
		public bool Active { get; set; }
		public int Round { get; set; }
		public int TotalRounds { get; set; } = 1;
		public int LimitSec { get; set; } = 300;
		public int IncSec { get; set; }
		public List<TPlayer> Players { get; set; } = new List<TPlayer>();
		public List<string> Played { get; set; } = new List<string>(); // "handleA|handleB" (terurut)
		public List<string> RoundBulkIds { get; set; } = new List<string>(); // board ronde berjalan (utk auto-lanjut)
	}

	private static Dictionary<string, TState> Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<Dictionary<string, TState>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, TState>();
			}
		}
		catch
		{
		}
		return new Dictionary<string, TState>();
	}

	private static void Save(Dictionary<string, TState> data)
	{
		try
		{
			Directory.CreateDirectory(DataDir);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch
		{
		}
	}

	public static TState? Get(string jid)
	{
		lock (_lk)
		{
			return Load().TryGetValue(jid, out TState? s) ? s : null;
		}
	}

	public static void StartNew(string jid, List<TPlayer> players, int limitSec, int incSec, int totalRounds)
	{
		lock (_lk)
		{
			Dictionary<string, TState> data = Load();
			data[jid] = new TState { Active = true, Round = 0, TotalRounds = totalRounds, LimitSec = limitSec, IncSec = incSec, Players = players, Played = new List<string>(), RoundBulkIds = new List<string>() };
			Save(data);
		}
	}

	// Semua turnamen yang masih aktif (jid -> state). Untuk auto-lanjut ronde.
	public static Dictionary<string, TState> AllActive()
	{
		lock (_lk)
		{
			Dictionary<string, TState> result = new Dictionary<string, TState>();
			foreach (KeyValuePair<string, TState> kv in Load())
			{
				if (kv.Value != null && kv.Value.Active)
				{
					result[kv.Key] = kv.Value;
				}
			}
			return result;
		}
	}

	public static void Update(string jid, TState s)
	{
		lock (_lk)
		{
			Dictionary<string, TState> data = Load();
			data[jid] = s;
			Save(data);
		}
	}

	public static void End(string jid)
	{
		lock (_lk)
		{
			Dictionary<string, TState> data = Load();
			if (data.Remove(jid))
			{
				Save(data);
			}
		}
	}

	// Kunci pasangan (urut alfabet) supaya A-vs-B == B-vs-A.
	public static string PairKey(string a, string b)
	{
		string x = a.ToLowerInvariant();
		string y = b.ToLowerInvariant();
		return (string.CompareOrdinal(x, y) <= 0) ? (x + "|" + y) : (y + "|" + x);
	}
}
