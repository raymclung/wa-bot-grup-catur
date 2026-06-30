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

	// Catatan board per grup (untuk start/cancel/hasil/list + auto-umumkan hasil).
	private sealed class Board
	{
		public string BulkId = "";
		public string White = "";
		public string Black = "";
		public string Url = "";
		public bool Rated;
		public bool Done;   // selesai+diumumkan ATAU dibatalkan -> berhenti dipantau
		public int Polls;
	}

	private static readonly Dictionary<string, List<Board>> _boards = new Dictionary<string, List<Board>>();

	private static volatile bool _pollerStarted = false;

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
		bool isStart = !isPair && StartRx.IsMatch(t) && (bulkArg.Length > 0 || t.Contains("start") || t.Contains("jam") || t.Contains("clock"));
		bool isCancel = !isPair && CancelRx.IsMatch(t);
		bool isInfo = !isPair && (t.Contains("info") || t.Contains("profil"));
		bool isResult = !isPair && (t.Contains("hasil") || t.Contains("result") || t.Contains("skor") || t.Contains("score"));
		bool isBoards = !isPair && (t.Contains("boards") || t.Contains("daftar board") || t.Contains("papan aktif"));
		if (!isPair && !isStart && !isCancel && !isInfo && !isResult && !isBoards)
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
			if (ar.Success)
			{
				MarkDone(msg.Jid, bulkC);
			}
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

		// ===== HASIL =====  @bot hasil [<BulkID>]  -> skor game (board terakhir kalau ID tak disebut)
		if (isResult)
		{
			string bidR = (bulkArg.Length > 0) ? bulkArg : GetLast(msg.Jid);
			if (bidR.Length == 0)
			{
				await Send(http, outBase, msg.Jid, "Belum ada board. Sebutkan BulkID: @bot hasil <BulkID>", null, logger);
				return "result-none";
			}
			LciClient.ResultInfo ri = await LciClient.ResultsParsed(config, http, bidR, logger);
			if (!ri.Ok)
			{
				await Send(http, outBase, msg.Jid, "Board tidak ditemukan / belum ada hasil.", null, logger);
				return "result-missing";
			}
			if (ri.Summary.Length > 0)
			{
				await Send(http, outBase, msg.Jid, "♟️ Hasil:\n" + ri.Summary + (ri.AllFinished ? "" : "\n(sebagian masih berjalan)"), null, logger);
			}
			else
			{
				await Send(http, outBase, msg.Jid, "Game belum selesai. Sabar ya 🙂", null, logger);
			}
			return "result";
		}

		// ===== DAFTAR BOARD =====  @bot boards  -> board aktif di grup ini
		if (isBoards)
		{
			List<Board> snap;
			lock (_lock)
			{
				snap = (_boards.TryGetValue(msg.Jid, out List<Board>? l) && l != null) ? new List<Board>(l) : new List<Board>();
			}
			List<Board> active = snap.FindAll((Board b) => !b.Done);
			if (active.Count == 0)
			{
				await Send(http, outBase, msg.Jid, "Belum ada board aktif di grup ini.", null, logger);
				return "boards-none";
			}
			StringBuilder sbb = new StringBuilder("Board aktif:\n");
			foreach (Board b in active)
			{
				sbb.Append("• " + b.White + " vs " + b.Black + "\n  " + b.Url + "  (" + b.BulkId + ")\n");
			}
			await Send(http, outBase, msg.Jid, sbb.ToString().TrimEnd(), null, logger);
			return "boards";
		}

		// ===== INFO PEMAIN =====  @bot info @A [@B ...]  -> nama + handle Lichess + status verifikasi
		if (isInfo)
		{
			List<MentionPair> who = new List<MentionPair>();
			foreach (MentionPair m in (msg.Mentions ?? Array.Empty<MentionPair>()))
			{
				if (!string.IsNullOrEmpty(m.Phone))
				{
					who.Add(m);
				}
			}
			if (who.Count == 0)
			{
				await Send(http, outBase, msg.Jid, "Tag pemain yang mau dilihat. Contoh: @bot info @NamaPemain", null, logger);
				return "info-need-mention";
			}
			StringBuilder sb = new StringBuilder();
			foreach (MentionPair m in who)
			{
				LciClient.LookupResult lu = await LciClient.LookupByPhone(config, http, m.Phone, logger);
				if (lu.Found)
				{
					string nm = (!string.IsNullOrWhiteSpace(lu.FullName) ? lu.FullName : ("@" + m.Lid));
					sb.Append("• " + nm + " — Lichess: " + (lu.Handle.Length > 0 ? lu.Handle : "-") + (lu.Verified ? " ✓ terverifikasi" : " (belum verifikasi)") + "\n");
				}
				else
				{
					sb.Append("• @" + m.Lid + " — belum terdaftar di LCI (daftar: ligacatur.com/register)\n");
				}
			}
			await Send(http, outBase, msg.Jid, sb.ToString().TrimEnd(), null, logger);
			return "info";
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
		string ratedTxt = (rated ? "rated" : "unrated");
		int mins = limit / 60;
		string wName = (!string.IsNullOrWhiteSpace(lw.FullName) ? lw.FullName : (lw.Handle.Length > 0 ? lw.Handle : ("@" + wp.Lid)));
		string bName = (!string.IsNullOrWhiteSpace(lb.FullName) ? lb.FullName : (lb.Handle.Length > 0 ? lb.Handle : ("@" + bp.Lid)));
		AddBoard(msg.Jid, pr.BulkId, wName, bName, pr.Url, rated);
		EnsurePoller(config, http, outBase, logger); // mulai pemantau hasil (auto-umumkan saat game kelar)
		// Pair + langsung mulai jam kalau diminta ("mulai"/"langsung"/"sekaligus"/"gas").
		bool startNow = t.Contains("mulai") || t.Contains("langsung") || t.Contains("sekaligus") || t.Contains("gas") || t.Contains(" now");
		string startedTxt = "";
		if (startNow)
		{
			LciClient.ActionResult sc = await LciClient.StartClocks(config, http, pr.BulkId, logger);
			startedTxt = sc.Success ? "\n⏱️ Jam langsung dimulai. Gas!" : "";
		}
		// Plain text (tanpa italic/markdown) supaya gampang di-copy. Nama lengkap dari LCI (full_name).
		string body = "♟️ Board siap! " + wName + " (putih) vs " + bName + " (hitam) · G" + mins + "+" + inc + " " + ratedTxt + "\n" + Invite(pr.Url) + startedTxt + "\nBulkID: " + pr.BulkId + (startNow ? "" : ("\nMulai jam: @bot start " + pr.BulkId)) + "  ·  Batal: @bot cancel " + pr.BulkId;
		await Send(http, outBase, msg.Jid, body, null, logger);
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
	private static readonly string[] _bulkKw = new string[] { "start", "mulai", "jalankan", "jam", "clock", "clocks", "cancel", "batal", "hapus", "board", "boards", "papan", "game", "pairing", "bulk", "id", "bulkid", "pair", "pasang", "info", "profil", "hasil", "result", "skor", "score", "rated", "unrated", "casual", "latihan", "langsung", "sekaligus", "gas", "now", "please", "tolong" };

	private static readonly Regex BulkRx = new Regex("^[A-Za-z0-9]{6,16}$", RegexOptions.CultureInvariant);

	private static string ExtractBulkId(string text)
	{
		foreach (string w0 in (text ?? "").Split(new char[] { ' ', '\t', '\n', '\r', ',', '.', ':' }, StringSplitOptions.RemoveEmptyEntries))
		{
			// Buang format WhatsApp (_italic_, *bold*, ~coret~) yang sering nempel saat di-copy.
			string w = w0.Trim('_', '*', '~');
			if (w.StartsWith("@"))
			{
				continue;
			}
			if (Array.IndexOf(_bulkKw, w.ToLowerInvariant()) >= 0)
			{
				continue;
			}
			// BulkID Lichess = alfanumerik ~8 karakter. Ini menyaring "G5+1", "vs", "5", dll.
			if (BulkRx.IsMatch(w))
			{
				return w;
			}
		}
		return "";
	}

	// Ajakan klik link, divariasikan acak (3-5 gaya) biar tidak terdengar robotik. {0} = URL game.
	private static readonly string[] _invites = new string[]
	{
		"{0} <- pemain klik di sini\nPenonton juga boleh klik! Ayuk!",
		"{0} <- klik di sini buat mulai, pemain!\nYang mau nonton, klik juga ya. Seru!",
		"{0} <- pemain masuk lewat link ini\nPenonton dipersilakan merapat, ramaikan!",
		"{0} <- ini papannya, pemain langsung gas\nPenonton boleh ikut nonton bareng!",
		"{0} <- pemain klik di sini\nNonton juga boleh kok, ayo rame-rame!"
	};

	private static string Invite(string url)
	{
		return string.Format(_invites[Random.Shared.Next(_invites.Length)], url);
	}

	private static string GetLast(string jid)
	{
		lock (_lock)
		{
			if (_boards.TryGetValue(jid, out List<Board>? list) && list != null && list.Count > 0)
			{
				return list[list.Count - 1].BulkId;
			}
			return "";
		}
	}

	private static void AddBoard(string jid, string bulkId, string white, string black, string url, bool rated)
	{
		if (string.IsNullOrEmpty(bulkId))
		{
			return;
		}
		lock (_lock)
		{
			if (!_boards.TryGetValue(jid, out List<Board>? list) || list == null)
			{
				list = new List<Board>();
				_boards[jid] = list;
			}
			list.Add(new Board { BulkId = bulkId, White = white, Black = black, Url = url, Rated = rated });
		}
	}

	private static void MarkDone(string jid, string bulkId)
	{
		lock (_lock)
		{
			if (_boards.TryGetValue(jid, out List<Board>? list) && list != null)
			{
				foreach (Board b in list)
				{
					if (b.BulkId == bulkId)
					{
						b.Done = true;
					}
				}
			}
		}
	}

	// Pemantau hasil di latar belakang: tiap 45 dtk cek board yang belum selesai,
	// umumkan skornya ke grup saat game kelar. Dimulai sekali (lazy) saat pairing pertama.
	private static void EnsurePoller(AppConfig config, HttpClient http, string outBase, ILogger logger)
	{
		lock (_lock)
		{
			if (_pollerStarted)
			{
				return;
			}
			_pollerStarted = true;
		}
		_ = Task.Run(async delegate
		{
			while (true)
			{
				try
				{
					await Task.Delay(45000);
					List<(string jid, Board b)> pending = new List<(string, Board)>();
					lock (_lock)
					{
						foreach (KeyValuePair<string, List<Board>> kv in _boards)
						{
							foreach (Board b in kv.Value)
							{
								if (!b.Done)
								{
									pending.Add((kv.Key, b));
								}
							}
						}
					}
					foreach ((string jid, Board b) in pending)
					{
						b.Polls++;
						LciClient.ResultInfo ri = await LciClient.ResultsParsed(config, http, b.BulkId, logger);
						if (ri.Ok && ri.AllFinished && ri.Summary.Length > 0)
						{
							lock (_lock)
							{
								b.Done = true;
							}
							await Send(http, outBase, jid, "♟️ Hasil: " + ri.Summary, null, logger);
						}
						else if (b.Polls > 240) // ~3 jam tak selesai -> berhenti pantau
						{
							lock (_lock)
							{
								b.Done = true;
							}
						}
					}
				}
				catch (Exception ex)
				{
					logger.LogWarning("pair poller: {Msg}", ex.Message);
				}
			}
		});
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
