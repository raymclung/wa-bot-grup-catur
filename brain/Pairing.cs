using System;
using System.Collections.Generic;
using System.IO;
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
		public string White = "";       // nama tampilan
		public string Black = "";
		public string WhiteHandle = ""; // handle Lichess (buat rematch + klasemen)
		public string BlackHandle = "";
		public string WhiteLid = "";    // LID WA (buat tag/notif saat rematch)
		public string BlackLid = "";
		public string Url = "";
		public int LimitSec;            // time control (buat rematch)
		public int IncSec;
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

	private static readonly Regex RoundsRx = new Regex("(\\d{1,2})\\s*(ronde|babak|round)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	// Balik string aksi kalau ditangani (pair/start/cancel/...); null kalau bukan perintah pairing.
	public static async Task<string?> TryHandle(AppConfig config, HttpClient http, ILogger logger, string outBase, IncomingMessage msg, string senderNum, string senderPhone)
	{
		if (config.Lci == null || !config.Lci.Enabled || !msg.MentionedBot)
		{
			return null;
		}
		try
		{
		string t = (msg.Text ?? "").ToLowerInvariant();
		string bulkArg = ExtractBulkId(msg.Text ?? "");
		bool isPair = t.Contains("pair") || t.Contains("pasang");
		bool isStart = !isPair && StartRx.IsMatch(t) && (bulkArg.Length > 0 || t.Contains("start") || t.Contains("jam") || t.Contains("clock"));
		bool isCancel = !isPair && CancelRx.IsMatch(t);
		bool isInfo = !isPair && (t.Contains("info") || t.Contains("profil"));
		bool isResult = !isPair && (t.Contains("hasil") || t.Contains("result") || t.Contains("skor") || t.Contains("score"));
		bool isBoards = !isPair && (t.Contains("boards") || t.Contains("daftar board") || t.Contains("papan aktif"));
		bool isHelp = !isPair && (t.Contains("bantuan") || t.Contains("help") || t.Contains("perintah pairing"));
		bool isRematch = !isPair && (t.Contains("rematch") || t.Contains("tukar warna") || t.Contains("main lagi"));
		bool isReset = !isPair && t.Contains("reset") && (t.Contains("klasemen") || t.Contains("standings") || t.Contains("musim"));
		bool isStats = !isPair && (t.Contains("statistik") || t.Contains("rekap") || t.Contains("stats"));
		bool isKlasemen = !isPair && !isReset && (t.Contains("klasemen") || t.Contains("standings"));
		// Turnamen mini. "turnamen" TANPA tag pemain dibiarkan null -> dijawab fitur JADWAL lama.
		int mcount = 0;
		foreach (MentionPair m0 in (msg.Mentions ?? Array.Empty<MentionPair>()))
		{
			if (!string.IsNullOrEmpty(m0.Phone))
			{
				mcount++;
			}
		}
		bool tWord = t.Contains("turnamen") || t.Contains("tournament");
		bool isTournEnd = !isPair && tWord && (t.Contains("selesai") || t.Contains("akhiri") || t.Contains("tutup") || t.Contains("stop") || t.Contains("juara"));
		bool isTournStart = !isPair && tWord && !isTournEnd && mcount >= 2;
		bool isRonde = !isPair && (t.Contains("ronde") || t.Contains("babak"));
		if (!isPair && !isStart && !isCancel && !isInfo && !isResult && !isBoards && !isHelp && !isRematch && !isKlasemen && !isReset && !isStats && !isTournStart && !isTournEnd && !isRonde)
		{
			return null;
		}
		if (!AdminSync.IsAllowed(config, senderNum, senderPhone))
		{
			await Send(http, outBase, msg.Jid, "Perintah pairing khusus admin.", null, logger);
			return "pair-denied";
		}

		// ===== TURNAMEN: MULAI =====  @bot turnamen @A @B @C @D
		if (isTournStart)
		{
			List<PairingTournament.TPlayer> tps = new List<PairingTournament.TPlayer>();
			foreach (MentionPair m in (msg.Mentions ?? Array.Empty<MentionPair>()))
			{
				if (string.IsNullOrEmpty(m.Phone))
				{
					continue;
				}
				LciClient.LookupResult lu = await LciClient.LookupByPhone(config, http, m.Phone, logger);
				if (lu.Found && lu.Verified && lu.Handle.Length > 0)
				{
					tps.Add(new PairingTournament.TPlayer { Handle = lu.Handle, Name = (!string.IsNullOrWhiteSpace(lu.FullName) ? lu.FullName : lu.Handle), Lid = m.Lid, Phone = m.Phone });
				}
			}
			if (tps.Count < 2)
			{
				await Send(http, outBase, msg.Jid, "Butuh minimal 2 pemain terdaftar & terverifikasi. Tag pemainnya ya.", null, logger);
				return "turnamen-need";
			}
			int tl;
			int ti;
			ParseTime(t, out tl, out ti);
			int rounds = 1; // default 1 babak; user bisa override "N ronde"
			Match rm = RoundsRx.Match(t);
			if (rm.Success)
			{
				rounds = int.Parse(rm.Groups[1].Value);
				if (rounds < 1) { rounds = 1; }
				if (rounds > 20) { rounds = 20; }
			}
			PairingStandings.Reset(msg.Jid);
			PairingTournament.StartNew(msg.Jid, tps, tl, ti, rounds);
			await Send(http, outBase, msg.Jid, "🏆 Turnamen dimulai! " + tps.Count + " pemain · " + rounds + " ronde · G" + (tl / 60) + "+" + ti + " unrated. Klasemen direset.", null, logger);
			PairingTournament.TState? stNew = PairingTournament.Get(msg.Jid);
			if (stNew != null)
			{
				await RunRound(config, http, logger, outBase, msg.Jid, stNew);
			}
			return "turnamen-start";
		}

		// ===== TURNAMEN: RONDE BERIKUTNYA =====  @bot ronde
		if (isRonde)
		{
			PairingTournament.TState? st = PairingTournament.Get(msg.Jid);
			if (st == null || !st.Active)
			{
				await Send(http, outBase, msg.Jid, "Tidak ada turnamen aktif. Mulai dulu: @bot turnamen @A @B @C @D", null, logger);
				return "ronde-none";
			}
			await RunRound(config, http, logger, outBase, msg.Jid, st);
			return "ronde";
		}

		// ===== TURNAMEN: SELESAI =====  @bot turnamen selesai
		if (isTournEnd)
		{
			PairingTournament.TState? ste = PairingTournament.Get(msg.Jid);
			if (ste == null)
			{
				await Send(http, outBase, msg.Jid, "Tidak ada turnamen aktif.", null, logger);
				return "turnamen-end-none";
			}
			string standings = PairingStandings.Format(msg.Jid);
			PairingTournament.End(msg.Jid);
			await Send(http, outBase, msg.Jid, "🏁 Turnamen selesai!\n\n" + standings + "\n\nTerima kasih sudah bertanding! 👏", null, logger);
			return "turnamen-end";
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

		// ===== BANTUAN =====  @bot bantuan  -> daftar perintah pairing
		if (isHelp)
		{
			string help = "Perintah pairing (admin):\n"
				+ "• @bot pair @A vs @B [unrated|rated] G5+1 — buat board\n"
				+ "   tambah 'mulai'/'gas' untuk langsung start jam\n"
				+ "   tag 4+ pemain = banyak board sekaligus (A-B, C-D)\n"
				+ "• @bot start [BulkID] — mulai jam board terakhir\n"
				+ "• @bot cancel [BulkID] — batalkan board\n"
				+ "• @bot rematch — ulang 2 pemain terakhir (warna ditukar)\n"
				+ "• @bot hasil [BulkID] — cek skor\n"
				+ "• @bot boards — daftar board aktif\n"
				+ "• @bot klasemen — tabel menang/seri/kalah\n"
				+ "• @bot statistik @A — rekap pribadi pemain\n"
				+ "• @bot reset klasemen — mulai musim baru\n"
				+ "• @bot info @A — info pemain (handle Lichess, verifikasi)\n"
				+ "\nTurnamen mini (Swiss):\n"
				+ "• @bot turnamen @A @B @C @D — mulai turnamen + ronde 1\n"
				+ "• @bot ronde — pair ronde berikutnya (by klasemen)\n"
				+ "• @bot turnamen selesai — umumkan juara & tutup";
			await Send(http, outBase, msg.Jid, help, null, logger);
			return "help";
		}

		// ===== KLASEMEN =====  @bot klasemen  -> tabel M/S/K + poin
		if (isKlasemen)
		{
			await Send(http, outBase, msg.Jid, PairingStandings.Format(msg.Jid), null, logger);
			return "klasemen";
		}

		// ===== RESET KLASEMEN =====  @bot reset klasemen  -> mulai musim baru
		if (isReset)
		{
			bool okReset = PairingStandings.Reset(msg.Jid);
			await Send(http, outBase, msg.Jid, okReset ? "Klasemen grup ini direset. Musim baru dimulai! 🆕" : "Klasemen masih kosong, tidak ada yang direset.", null, logger);
			return "klasemen-reset";
		}

		// ===== STATISTIK =====  @bot statistik @A  -> rekap pribadi pemain
		if (isStats)
		{
			MentionPair? who = null;
			foreach (MentionPair m in (msg.Mentions ?? Array.Empty<MentionPair>()))
			{
				if (!string.IsNullOrEmpty(m.Phone))
				{
					who = m;
					break;
				}
			}
			if (who == null)
			{
				await Send(http, outBase, msg.Jid, "Tag pemainnya. Contoh: @bot statistik @NamaPemain", null, logger);
				return "stats-need";
			}
			LciClient.LookupResult ls = await LciClient.LookupByPhone(config, http, who.Phone, logger);
			if (!ls.Found || ls.Handle.Length == 0)
			{
				await Send(http, outBase, msg.Jid, "@" + who.Lid + " belum terdaftar di LCI.", new string[1] { who.Lid }, logger);
				return "stats-unreg";
			}
			string snm = (!string.IsNullOrWhiteSpace(ls.FullName) ? ls.FullName : ls.Handle);
			await Send(http, outBase, msg.Jid, PairingStandings.FormatPlayer(msg.Jid, ls.Handle, snm), null, logger);
			return "stats";
		}

		// ===== REMATCH =====  @bot rematch  -> pasangkan ulang 2 pemain terakhir, warna ditukar
		if (isRematch)
		{
			Board? last = GetLastBoard(msg.Jid);
			if (last == null || last.WhiteHandle.Length == 0 || last.BlackHandle.Length == 0)
			{
				await Send(http, outBase, msg.Jid, "Belum ada board untuk di-rematch. Buat dulu: @bot pair @A vs @B G5+1", null, logger);
				return "rematch-none";
			}
			// Tukar warna: putih baru = hitam lama.
			LciClient.PairResult prr = await LciClient.Pair(config, http, last.BlackHandle, last.WhiteHandle, last.LimitSec, last.IncSec, last.Rated, config.Lci.DefaultVariant, logger);
			if (!prr.Success || prr.Url.Length == 0)
			{
				await Send(http, outBase, msg.Jid, "Gagal rematch. " + Clip(prr.Message), null, logger);
				return "rematch-fail";
			}
			AddBoard(msg.Jid, prr.BulkId, last.Black, last.White, last.BlackHandle, last.WhiteHandle, last.BlackLid, last.WhiteLid, prr.Url, last.LimitSec, last.IncSec, last.Rated);
			EnsurePoller(config, http, outBase, logger);
			int rmins = last.LimitSec / 60;
			List<string> rtags = new List<string>();
			if (last.BlackLid.Length > 0)
			{
				rtags.Add(last.BlackLid);
			}
			if (last.WhiteLid.Length > 0)
			{
				rtags.Add(last.WhiteLid);
			}
			string rtagLine = "";
			foreach (string l in rtags)
			{
				rtagLine += " @" + l;
			}
			if (rtagLine.Length > 0)
			{
				rtagLine = "\nMain yuk" + rtagLine + "!";
			}
			string rbody = "♟️ Rematch! " + last.Black + " (putih) vs " + last.White + " (hitam) · G" + rmins + "+" + last.IncSec + " " + (last.Rated ? "rated" : "unrated") + "\n" + Invite(prr.Url) + "\nBulkID: " + prr.BulkId + "\nMulai jam: @bot start " + prr.BulkId + "  ·  Batal: @bot cancel " + prr.BulkId + rtagLine;
			await Send(http, outBase, msg.Jid, rbody, rtags.ToArray(), logger);
			return "rematch";
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
		bool startNow = t.Contains("mulai") || t.Contains("langsung") || t.Contains("sekaligus") || t.Contains("gas") || t.Contains(" now");

		// Banyak pemain (4, 6, ...) -> beberapa board sekaligus (pasangan berurutan A-B, C-D, ...).
		if (players.Count > 2)
		{
			StringBuilder mb = new StringBuilder();
			List<string> tagLids = new List<string>();
			int made = 0;
			for (int i = 0; i + 1 < players.Count; i += 2)
			{
				string line = await CreateBoardLine(config, http, logger, msg.Jid, players[i], players[i + 1], limit, inc, rated, startNow);
				mb.Append(line + "\n\n");
				if (line.StartsWith("♟️"))
				{
					made++;
					tagLids.Add(players[i].Lid);
					tagLids.Add(players[i + 1].Lid);
				}
			}
			EnsurePoller(config, http, outBase, logger);
			mb.Append(made + " board dibuat.");
			if (tagLids.Count > 0)
			{
				mb.Append("\nMain yuk");
				foreach (string l in tagLids)
				{
					mb.Append(" @" + l);
				}
				mb.Append("!");
			}
			await Send(http, outBase, msg.Jid, mb.ToString().TrimEnd(), tagLids.ToArray(), logger);
			return "pair-multi";
		}

		// Satu pasangan -> balasan lengkap (ajakan + tag kalau pemain belum terdaftar).
		MentionPair wp = players[0];
		MentionPair bp = players[1];
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
		AddBoard(msg.Jid, pr.BulkId, wName, bName, lw.Handle, lb.Handle, wp.Lid, bp.Lid, pr.Url, limit, inc, rated);
		EnsurePoller(config, http, outBase, logger); // mulai pemantau hasil (auto-umumkan saat game kelar)
		string startedTxt = "";
		if (startNow)
		{
			LciClient.ActionResult sc = await LciClient.StartClocks(config, http, pr.BulkId, logger);
			startedTxt = sc.Success ? "\n⏱️ Jam langsung dimulai. Gas!" : "";
		}
		// Plain text (tanpa italic/markdown) supaya gampang di-copy. Nama lengkap dari LCI (full_name).
		// Tag kedua pemain di baris terakhir -> mereka dapat notifikasi (link tetap bersih di tengah).
		string body = "♟️ Board siap! " + wName + " (putih) vs " + bName + " (hitam) · G" + mins + "+" + inc + " " + ratedTxt + "\n" + Invite(pr.Url) + startedTxt + "\nBulkID: " + pr.BulkId + (startNow ? "" : ("\nMulai jam: @bot start " + pr.BulkId)) + "  ·  Batal: @bot cancel " + pr.BulkId + "\nMain yuk @" + wp.Lid + " @" + bp.Lid + "!";
		await Send(http, outBase, msg.Jid, body, new string[2] { wp.Lid, bp.Lid }, logger);
		return "pair";
		}
		catch (Exception ex)
		{
			logger.LogWarning("PairingCommand error: {Msg}", ex.Message);
			try
			{
				string dir = Path.Combine(Directory.GetCurrentDirectory(), "data");
				Directory.CreateDirectory(dir);
				File.AppendAllText(Path.Combine(dir, "pairing-errors.txt"), DateTime.UtcNow.ToString("o") + "\n" + ex + "\n\n");
			}
			catch
			{
			}
			try
			{
				await Send(http, outBase, msg.Jid, "Aduh, ada error saat proses perintah pairing. Sudah aku catat, coba lagi ya. 🙏", null, logger);
			}
			catch
			{
			}
			return "pair-error";
		}
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
	private static readonly string[] _bulkKw = new string[] { "start", "mulai", "jalankan", "jam", "clock", "clocks", "cancel", "batal", "hapus", "board", "boards", "papan", "game", "pairing", "bulk", "id", "bulkid", "pair", "pasang", "info", "profil", "hasil", "result", "skor", "score", "rated", "unrated", "casual", "latihan", "langsung", "sekaligus", "gas", "now", "please", "tolong", "turnamen", "tournament", "ronde", "babak", "selesai", "klasemen", "statistik", "rematch", "bantuan" };

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

	private static void AddBoard(string jid, string bulkId, string white, string black, string whiteHandle, string blackHandle, string whiteLid, string blackLid, string url, int limitSec, int incSec, bool rated)
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
			list.Add(new Board { BulkId = bulkId, White = white, Black = black, WhiteHandle = whiteHandle, BlackHandle = blackHandle, WhiteLid = whiteLid, BlackLid = blackLid, Url = url, LimitSec = limitSec, IncSec = incSec, Rated = rated });
		}
	}

	private static Board? GetLastBoard(string jid)
	{
		lock (_lock)
		{
			if (_boards.TryGetValue(jid, out List<Board>? list) && list != null && list.Count > 0)
			{
				return list[list.Count - 1];
			}
			return null;
		}
	}

	// Buat satu board untuk multi-pair; balik baris ringkas (diawali ♟️ jika sukses, ⚠️ jika gagal).
	private static async Task<string> CreateBoardLine(AppConfig config, HttpClient http, ILogger logger, string jid, MentionPair wp, MentionPair bp, int limit, int inc, bool rated, bool startNow)
	{
		LciClient.LookupResult lw = await LciClient.LookupByPhone(config, http, wp.Phone, logger);
		if (!lw.Found || !lw.Verified || lw.Handle.Length == 0)
		{
			return "⚠️ @" + wp.Lid + " (putih) belum terdaftar/terverifikasi.";
		}
		LciClient.LookupResult lb = await LciClient.LookupByPhone(config, http, bp.Phone, logger);
		if (!lb.Found || !lb.Verified || lb.Handle.Length == 0)
		{
			return "⚠️ @" + bp.Lid + " (hitam) belum terdaftar/terverifikasi.";
		}
		LciClient.PairResult pr = await LciClient.Pair(config, http, lw.Handle, lb.Handle, limit, inc, rated, config.Lci!.DefaultVariant, logger);
		if (!pr.Success || pr.Url.Length == 0)
		{
			return "⚠️ Gagal buat board. " + Clip(pr.Message);
		}
		string wName = (!string.IsNullOrWhiteSpace(lw.FullName) ? lw.FullName : lw.Handle);
		string bName = (!string.IsNullOrWhiteSpace(lb.FullName) ? lb.FullName : lb.Handle);
		AddBoard(jid, pr.BulkId, wName, bName, lw.Handle, lb.Handle, wp.Lid, bp.Lid, pr.Url, limit, inc, rated);
		if (startNow)
		{
			await LciClient.StartClocks(config, http, pr.BulkId, logger);
		}
		return "♟️ " + wName + " (putih) vs " + bName + " (hitam)\n" + pr.Url + "\nBulkID: " + pr.BulkId;
	}

	// Jalankan satu ronde turnamen: susun pasangan (acak utk ronde 1; by-klasemen utk ronde >1,
	// hindari ulang lawan; bye utk ganjil), buat board (jam langsung jalan), kirim + simpan state.
	private static async Task RunRound(AppConfig config, HttpClient http, ILogger logger, string outBase, string jid, PairingTournament.TState st)
	{
		int newRound = st.Round + 1;
		List<PairingTournament.TPlayer> pool = new List<PairingTournament.TPlayer>(st.Players);
		if (newRound == 1)
		{
			for (int i = pool.Count - 1; i > 0; i--)
			{
				int j = Random.Shared.Next(i + 1);
				PairingTournament.TPlayer tmp = pool[i];
				pool[i] = pool[j];
				pool[j] = tmp;
			}
		}
		else
		{
			pool.Sort((PairingTournament.TPlayer a, PairingTournament.TPlayer b) => PairingStandings.PointsOf(jid, b.Handle).CompareTo(PairingStandings.PointsOf(jid, a.Handle)));
		}
		PairingTournament.TPlayer? bye = null;
		if (pool.Count % 2 == 1)
		{
			bye = pool[pool.Count - 1];
			pool.RemoveAt(pool.Count - 1);
		}
		List<(PairingTournament.TPlayer w, PairingTournament.TPlayer b)> pairs = new List<(PairingTournament.TPlayer, PairingTournament.TPlayer)>();
		while (pool.Count >= 2)
		{
			PairingTournament.TPlayer a = pool[0];
			pool.RemoveAt(0);
			int j = 0;
			while (j < pool.Count && st.Played.Contains(PairingTournament.PairKey(a.Handle, pool[j].Handle)))
			{
				j++;
			}
			if (j >= pool.Count)
			{
				j = 0; // semua sudah pernah lawan -> terima ulang
			}
			PairingTournament.TPlayer bb = pool[j];
			pool.RemoveAt(j);
			if (newRound % 2 == 0)
			{
				pairs.Add((bb, a)); // warna gantian per ronde
			}
			else
			{
				pairs.Add((a, bb));
			}
		}
		StringBuilder sb = new StringBuilder("🏆 Ronde " + newRound + "/" + st.TotalRounds + ":\n\n");
		List<string> tagLids = new List<string>();
		List<string> roundBulks = new List<string>();
		foreach (var p in pairs)
		{
			(string line, string bulkId) = await CreateBoardByHandles(config, http, logger, jid, p.w, p.b, st.LimitSec, st.IncSec);
			sb.Append(line + "\n\n");
			if (bulkId.Length > 0)
			{
				roundBulks.Add(bulkId);
			}
			st.Played.Add(PairingTournament.PairKey(p.w.Handle, p.b.Handle));
			if (!string.IsNullOrEmpty(p.w.Lid))
			{
				tagLids.Add(p.w.Lid);
			}
			if (!string.IsNullOrEmpty(p.b.Lid))
			{
				tagLids.Add(p.b.Lid);
			}
		}
		if (bye != null)
		{
			PairingStandings.RecordBye(jid, bye.Handle, bye.Name); // bye = +1 poin (Swiss)
			sb.Append("⏸️ " + bye.Name + " dapat BYE ronde ini (+1 poin).\n");
		}
		st.Round = newRound;
		st.RoundBulkIds = roundBulks;
		PairingTournament.Update(jid, st);
		EnsurePoller(config, http, outBase, logger);
		if (tagLids.Count > 0)
		{
			sb.Append("\nMain yuk");
			foreach (string l in tagLids)
			{
				sb.Append(" @" + l);
			}
			sb.Append("!");
		}
		await Send(http, outBase, jid, sb.ToString().TrimEnd(), tagLids.ToArray(), logger);
	}

	// Buat board langsung dari handle (turnamen) + start jam. Balik baris ringkas.
	private static async Task<(string line, string bulkId)> CreateBoardByHandles(AppConfig config, HttpClient http, ILogger logger, string jid, PairingTournament.TPlayer w, PairingTournament.TPlayer b, int limit, int inc)
	{
		LciClient.PairResult pr = await LciClient.Pair(config, http, w.Handle, b.Handle, limit, inc, false, config.Lci!.DefaultVariant, logger);
		if (!pr.Success || pr.Url.Length == 0)
		{
			return ("⚠️ " + w.Name + " vs " + b.Name + ": gagal. " + Clip(pr.Message), "");
		}
		AddBoard(jid, pr.BulkId, w.Name, b.Name, w.Handle, b.Handle, w.Lid, b.Lid, pr.Url, limit, inc, false);
		await LciClient.StartClocks(config, http, pr.BulkId, logger); // turnamen: jam langsung jalan
		return ("♟️ " + w.Name + " (P) vs " + b.Name + " (H)\n" + pr.Url, pr.BulkId);
	}

	// Cek tiap turnamen aktif: kalau SEMUA papan ronde berjalan sudah beres -> lanjut ronde
	// berikutnya; kalau sudah ronde terakhir -> umumkan juara & tutup. Dipanggil dari poller.
	private static async Task MaybeAdvanceTournaments(AppConfig config, HttpClient http, ILogger logger, string outBase)
	{
		Dictionary<string, PairingTournament.TState> actives = PairingTournament.AllActive();
		foreach (KeyValuePair<string, PairingTournament.TState> kv in actives)
		{
			string jid = kv.Key;
			PairingTournament.TState st = kv.Value;
			if (st.RoundBulkIds == null || st.RoundBulkIds.Count == 0)
			{
				continue; // belum ada papan terlacak (turnamen lama / belum dipasang)
			}
			if (!AllBoardsDone(jid, st.RoundBulkIds))
			{
				continue; // masih ada game berjalan
			}
			if (st.Round >= st.TotalRounds)
			{
				await FinishTournament(config, http, outBase, jid, logger);
			}
			else
			{
				st.RoundBulkIds = new List<string>(); // cegah pemicu dobel sebelum ronde baru dibuat
				PairingTournament.Update(jid, st);
				await Send(http, outBase, jid, "✅ Ronde " + st.Round + " selesai! Menyiapkan ronde berikutnya...", null, logger);
				await RunRound(config, http, logger, outBase, jid, st);
			}
		}
	}

	private static bool AllBoardsDone(string jid, List<string> bulkIds)
	{
		lock (_lock)
		{
			if (!_boards.TryGetValue(jid, out List<Board>? list) || list == null)
			{
				return false;
			}
			foreach (string bid in bulkIds)
			{
				Board? found = list.Find((Board x) => x.BulkId == bid);
				if (found == null || !found.Done)
				{
					return false;
				}
			}
			return true;
		}
	}

	private static async Task FinishTournament(AppConfig config, HttpClient http, string outBase, string jid, ILogger logger)
	{
		string standings = PairingStandings.Format(jid);
		PairingTournament.End(jid);
		await Send(http, outBase, jid, "🏁 Turnamen selesai!\n\n" + standings + "\n\nTerima kasih sudah bertanding! 👏", null, logger);
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
							if (ri.Games.Count > 0)
							{
								PairingStandings.Record(jid, b.WhiteHandle, b.White, b.BlackHandle, b.Black, ri.Games[0].Score);
							}
							await Send(http, outBase, jid, "♟️ Hasil: " + ri.Summary + "\nKetik @bot klasemen untuk peringkat.", null, logger);
						}
						else if (b.Polls > 240) // ~3 jam tak selesai -> berhenti pantau
						{
							lock (_lock)
							{
								b.Done = true;
							}
						}
					}
					// Auto-lanjut / akhiri turnamen yang semua papan rondenya sudah beres.
					await MaybeAdvanceTournaments(config, http, logger, outBase);
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
