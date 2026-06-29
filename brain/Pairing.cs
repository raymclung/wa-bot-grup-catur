using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// ============================================================
//  Perintah PAIRING di WAG (LCI). Hanya saat bot DI-TAG (mentionedBot).
//    @bot pair @A vs @B [unrated|rated] G5+1   -> bikin board + link
//    @bot start                                -> mulai jam board terakhir grup
//    @bot cancel                               -> batalkan board terakhir grup
//  Pemain diambil dari NOMOR MENTION (gateway kirim {lid, phone}, bot dibuang).
//  Khusus admin (AdminSync). LCI lewat LciClient.
// ============================================================

internal static class PairingCommand
{
	private static readonly object _lock = new object();

	private static readonly Dictionary<string, string> _lastBulk = new Dictionary<string, string>();

	private static readonly Regex GpRx = new Regex("g?\\s*(\\d{1,3})\\s*\\+\\s*(\\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex MenitRx = new Regex("(\\d{1,3})\\s*menit", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex IncRx = new Regex("(?:increment|inc|tambahan)\\s*(\\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex RatedRx = new Regex("\\brated\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex StartRx = new Regex("\\b(start|mulai|jalankan)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex CancelRx = new Regex("\\b(cancel|batal|hapus)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	// Balik string aksi kalau ditangani (pair/start/cancel/...); null kalau bukan perintah pairing.
	public static async Task<string?> TryHandle(AppConfig config, HttpClient http, ILogger logger, string outBase, IncomingMessage msg, string senderNum, string senderPhone)
	{
		if (config.Lci == null || !config.Lci.Enabled || !msg.MentionedBot)
		{
			return null;
		}
		string t = (msg.Text ?? "").ToLowerInvariant();
		string bulkArg = ExtractBulkId(msg.Text ?? "");
		bool isPair = t.Contains("pair") || t.Contains("pasang");
		bool isStart = StartRx.IsMatch(t) && (bulkArg.Length > 0 || t.Contains("start") || t.Contains("jam") || t.Contains("clock"));
		bool isCancel = CancelRx.IsMatch(t);
		if (!isPair && !isStart && !isCancel)
		{
			return null;
		}
		if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
		{
			await Send(http, outBase, msg.Jid, "Perintah pairing khusus admin.", null, logger);
			return "pair-denied";
		}

		if (isCancel)
		{
			string bulkC = (bulkArg.Length > 0) ? bulkArg : GetLast(msg.Jid);
			if (bulkC.Length == 0)
			{
				await Send(http, outBase, msg.Jid, "Belum ada board untuk dibatalkan. Sebutkan BulkID: @bot cancel <BulkID>", null, logger);
				return "pair-cancel-none";
			}
			LciClient.ActionResult ar = await LciClient.CancelBoard(config, http, bulkC, logger);
			await Send(http, outBase, msg.Jid, ar.Success ? "Oke, board dibatalkan." : ("Gagal batal board. " + Clip(ar.Message)), null, logger);
			return "pair-cancel";
		}
		if (isStart)
		{
			string bulkS = (bulkArg.Length > 0) ? bulkArg : GetLast(msg.Jid);
			if (bulkS.Length == 0)
			{
				await Send(http, outBase, msg.Jid, "Belum ada board untuk dimulai. Buat dulu: @bot pair @A vs @B G5+1 (atau: @bot start <BulkID>)", null, logger);
				return "pair-start-none";
			}
			LciClient.ActionResult ar2 = await LciClient.StartClocks(config, http, bulkS, logger);
			await Send(http, outBase, msg.Jid, ar2.Success ? "⏱️ Jam dimulai. Selamat bertanding!" : ("Gagal mulai jam. " + Clip(ar2.Message)), null, logger);
			return "pair-start";
		}

		// ===== PAIR =====
		if (string.IsNullOrEmpty(config.WabotToken) || config.WabotToken == "PUT_YOUR_LCI_TOKEN_HERE")
		{
			await Send(http, outBase, msg.Jid, "LCI belum aktif (token belum di-set). Hubungi admin dulu.", null, logger);
			return "pair-no-token";
		}
		List<MentionPair> players = new List<MentionPair>();
		foreach (MentionPair m in (msg.Mentions ?? Array.Empty<MentionPair>()))
		{
			if (!string.IsNullOrEmpty(m.Phone))
			{
				players.Add(m);
			}
		}
		if (players.Count < 2)
		{
			await Send(http, outBase, msg.Jid, "Tag 2 pemain ya (yang nomornya terdaftar). Contoh: @bot pair @A vs @B unrated G5+1", null, logger);
			return "pair-need-2";
		}
		MentionPair wp = players[0];
		MentionPair bp = players[1];
		int limit;
		int inc;
		ParseTime(t, out limit, out inc);
		bool rated = config.Lci.DefaultRated;
		if (t.Contains("unrated") || t.Contains("casual") || t.Contains("latihan") || t.Contains("tanpa rating"))
		{
			rated = false;
		}
		else if (RatedRx.IsMatch(t))
		{
			rated = true;
		}

		LciClient.LookupResult lw = await LciClient.LookupByPhone(config, http, wp.Phone, logger);
		if (!lw.Found || !lw.Verified || lw.Handle.Length == 0)
		{
			await Send(http, outBase, msg.Jid, "Pemain putih (@" + wp.Lid + ") belum terdaftar/terverifikasi di LCI.", new string[1] { wp.Lid }, logger);
			return "pair-white-unverified";
		}
		LciClient.LookupResult lb = await LciClient.LookupByPhone(config, http, bp.Phone, logger);
		if (!lb.Found || !lb.Verified || lb.Handle.Length == 0)
		{
			await Send(http, outBase, msg.Jid, "Pemain hitam (@" + bp.Lid + ") belum terdaftar/terverifikasi di LCI.", new string[1] { bp.Lid }, logger);
			return "pair-black-unverified";
		}
		LciClient.PairResult pr = await LciClient.Pair(config, http, lw.Handle, lb.Handle, limit, inc, rated, config.Lci.DefaultVariant, logger);
		if (!pr.Success || pr.Url.Length == 0)
		{
			await Send(http, outBase, msg.Jid, "Gagal membuat board. " + Clip(pr.Message), null, logger);
			return "pair-fail";
		}
		SetLast(msg.Jid, pr.BulkId);
		string ratedTxt = (rated ? "rated" : "unrated");
		int mins = limit / 60;
		string body = "♟️ Board siap! @" + wp.Lid + " (putih) vs @" + bp.Lid + " (hitam) · G" + mins + "+" + inc + " " + ratedTxt + "\n" + pr.Url + "\nBulkID: " + pr.BulkId + "\n_Mulai jam: @bot start " + pr.BulkId + "  ·  Batal: @bot cancel " + pr.BulkId + "_";
		await Send(http, outBase, msg.Jid, body, new string[2] { wp.Lid, bp.Lid }, logger);
		return "pair";
	}

	private static void ParseTime(string t, out int limitSec, out int incSec)
	{
		Match g = GpRx.Match(t);
		if (g.Success)
		{
			limitSec = int.Parse(g.Groups[1].Value) * 60;
			incSec = int.Parse(g.Groups[2].Value);
			return;
		}
		limitSec = 300;
		incSec = 0;
		Match mn = MenitRx.Match(t);
		if (mn.Success)
		{
			limitSec = int.Parse(mn.Groups[1].Value) * 60;
		}
		Match ic = IncRx.Match(t);
		if (ic.Success)
		{
			incSec = int.Parse(ic.Groups[1].Value);
		}
	}

	private static string Clip(string s)
	{
		s = (s ?? "").Trim();
		return (s.Length > 100) ? s.Substring(0, 100) : s;
	}

	// Ambil BulkID dari teks perintah start/cancel: token pertama yang BUKAN mention (@..),
	// bukan kata kunci, panjang >= 3. "" kalau tak ada (-> pakai board terakhir grup).
	private static readonly string[] _bulkKw = new string[] { "start", "mulai", "jalankan", "jam", "clock", "clocks", "cancel", "batal", "hapus", "board", "papan", "game", "pairing", "bulk", "id", "bulkid" };

	private static string ExtractBulkId(string text)
	{
		foreach (string w in (text ?? "").Split(new char[] { ' ', '\t', '\n', '\r', ',', '.', ':' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (w.StartsWith("@"))
			{
				continue;
			}
			if (Array.IndexOf(_bulkKw, w.ToLowerInvariant()) >= 0)
			{
				continue;
			}
			if (w.Length >= 3)
			{
				return w;
			}
		}
		return "";
	}

	private static string GetLast(string jid)
	{
		lock (_lock)
		{
			return _lastBulk.TryGetValue(jid, out string v) ? v : "";
		}
	}

	private static void SetLast(string jid, string bulk)
	{
		lock (_lock)
		{
			if (!string.IsNullOrEmpty(bulk))
			{
				_lastBulk[jid] = bulk;
			}
		}
	}

	private static async Task Send(HttpClient http, string outBase, string jid, string text, string[]? mentionLids, ILogger logger)
	{
		try
		{
			string[] mentions = Array.Empty<string>();
			if (mentionLids != null)
			{
				List<string> mj = new List<string>();
				foreach (string l in mentionLids)
				{
					if (!string.IsNullOrEmpty(l))
					{
						mj.Add(l + "@lid");
					}
				}
				mentions = mj.ToArray();
			}
			object body = new { jid, text, mentions };
			using StringContent content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
			await http.PostAsync(outBase + "/send", content);
		}
		catch (Exception ex)
		{
			logger.LogWarning("pair send gagal: {Msg}", ex.Message);
		}
	}
}
