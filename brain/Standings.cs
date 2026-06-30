using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

// ============================================================
//  Klasemen pairing per grup: Menang/Seri/Kalah + poin.
//  Disimpan ke data/pairing-standings.json (poin: M=1, S=0.5, K=0).
//  Dicatat otomatis oleh poller hasil di PairingCommand saat game kelar.
// ============================================================

internal static class PairingStandings
{
	private static readonly object _lk = new object();

	private static string DataDir => Path.Combine(Directory.GetCurrentDirectory(), "data");

	private static string FilePath => Path.Combine(DataDir, "pairing-standings.json");

	private sealed class Rec
	{
		public string Name { get; set; } = "";
		public int W { get; set; }
		public int D { get; set; }
		public int L { get; set; }
	}

	// grup -> handle(lowercase) -> Rec
	private static Dictionary<string, Dictionary<string, Rec>> Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Rec>>>(File.ReadAllText(FilePath))
					?? new Dictionary<string, Dictionary<string, Rec>>();
			}
		}
		catch
		{
		}
		return new Dictionary<string, Dictionary<string, Rec>>();
	}

	private static void Save(Dictionary<string, Dictionary<string, Rec>> data)
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

	// Catat satu hasil. score: "1-0" (putih menang) / "0-1" (hitam) / "1/2-1/2" (seri).
	public static void Record(string jid, string wHandle, string wName, string bHandle, string bName, string score)
	{
		if (string.IsNullOrWhiteSpace(score))
		{
			return;
		}
		string wk = (wHandle.Length > 0 ? wHandle : wName).ToLowerInvariant();
		string bk = (bHandle.Length > 0 ? bHandle : bName).ToLowerInvariant();
		if (wk.Length == 0 || bk.Length == 0)
		{
			return;
		}
		lock (_lk)
		{
			Dictionary<string, Dictionary<string, Rec>> data = Load();
			if (!data.TryGetValue(jid, out Dictionary<string, Rec>? g) || g == null)
			{
				g = new Dictionary<string, Rec>();
				data[jid] = g;
			}
			Rec wr = GetRec(g, wk, wName);
			Rec br = GetRec(g, bk, bName);
			if (score.Contains("1/2") || score.Contains("½"))
			{
				wr.D++;
				br.D++;
			}
			else if (score.StartsWith("1-0"))
			{
				wr.W++;
				br.L++;
			}
			else if (score.StartsWith("0-1"))
			{
				wr.L++;
				br.W++;
			}
			else
			{
				return; // skor tak dikenal -> jangan catat / jangan simpan
			}
			Save(data);
		}
	}

	private static Rec GetRec(Dictionary<string, Rec> g, string key, string name)
	{
		if (!g.TryGetValue(key, out Rec? r) || r == null)
		{
			r = new Rec { Name = name };
			g[key] = r;
		}
		if (name.Length > 0)
		{
			r.Name = name; // selalu pakai nama tampilan terbaru
		}
		return r;
	}

	// Tabel klasemen untuk satu grup (string siap kirim). "" -> belum ada.
	public static string Format(string jid)
	{
		lock (_lk)
		{
			Dictionary<string, Dictionary<string, Rec>> data = Load();
			if (!data.TryGetValue(jid, out Dictionary<string, Rec>? g) || g == null || g.Count == 0)
			{
				return "Belum ada hasil tercatat di grup ini.";
			}
			List<Rec> rows = new List<Rec>(g.Values);
			rows.Sort(delegate (Rec a, Rec b)
			{
				double pa = a.W + 0.5 * a.D;
				double pb = b.W + 0.5 * b.D;
				if (pb != pa)
				{
					return pb.CompareTo(pa);
				}
				return b.W.CompareTo(a.W);
			});
			StringBuilder sb = new StringBuilder("🏆 Klasemen:\n");
			int i = 1;
			foreach (Rec r in rows)
			{
				double pts = r.W + 0.5 * r.D;
				sb.Append(i + ". " + r.Name + " — " + Pts(pts) + " poin (" + r.W + "M/" + r.D + "S/" + r.L + "K)\n");
				i++;
			}
			return sb.ToString().TrimEnd();
		}
	}

	private static string Pts(double p)
	{
		return (p == Math.Floor(p)) ? ((int)p).ToString() : p.ToString("0.0", CultureInfo.InvariantCulture);
	}

	// Statistik 1 pemain (peringkat + M/S/K). key = handle Lichess (lowercase).
	public static string FormatPlayer(string jid, string key, string displayName)
	{
		string shown = (displayName.Length > 0) ? displayName : key;
		lock (_lk)
		{
			Dictionary<string, Dictionary<string, Rec>> data = Load();
			if (!data.TryGetValue(jid, out Dictionary<string, Rec>? g) || g == null || g.Count == 0)
			{
				return shown + ": belum ada hasil tercatat.";
			}
			List<KeyValuePair<string, Rec>> rows = new List<KeyValuePair<string, Rec>>(g);
			rows.Sort(delegate (KeyValuePair<string, Rec> a, KeyValuePair<string, Rec> b)
			{
				double pa = a.Value.W + 0.5 * a.Value.D;
				double pb = b.Value.W + 0.5 * b.Value.D;
				if (pb != pa)
				{
					return pb.CompareTo(pa);
				}
				return b.Value.W.CompareTo(a.Value.W);
			});
			int rank = 0;
			Rec? me = null;
			for (int i = 0; i < rows.Count; i++)
			{
				if (rows[i].Key == key.ToLowerInvariant())
				{
					rank = i + 1;
					me = rows[i].Value;
					break;
				}
			}
			if (me == null)
			{
				return shown + ": belum ada hasil tercatat.";
			}
			double pts = me.W + 0.5 * me.D;
			return "📊 " + me.Name + "\nPeringkat #" + rank + " dari " + rows.Count + "\n" + Pts(pts) + " poin — " + me.W + " menang, " + me.D + " seri, " + me.L + " kalah";
		}
	}

	// Poin 1 pemain (W + 0.5*D). 0 kalau belum ada. Buat urutkan peserta turnamen.
	public static double PointsOf(string jid, string key)
	{
		lock (_lk)
		{
			Dictionary<string, Dictionary<string, Rec>> data = Load();
			if (data.TryGetValue(jid, out Dictionary<string, Rec>? g) && g != null
				&& g.TryGetValue(key.ToLowerInvariant(), out Rec? r) && r != null)
			{
				return r.W + 0.5 * r.D;
			}
			return 0.0;
		}
	}

	// Reset klasemen 1 grup (mulai musim baru). true kalau ada yang dihapus.
	public static bool Reset(string jid)
	{
		lock (_lk)
		{
			Dictionary<string, Dictionary<string, Rec>> data = Load();
			if (data.Remove(jid))
			{
				Save(data);
				return true;
			}
			return false;
		}
	}
}
