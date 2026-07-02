using System.Diagnostics;
using Chess; // Gera.Chess

/// <summary>Wrapper Stockfish (UCI): analisa FEN -> langkah terbaik + evaluasi. Spawn proses per
/// permintaan (sederhana & aman; analisa singkat ~1-2 dtk). Aman gagal: kembalikan null kalau error.</summary>
static class StockfishEngine
{
    static string _exe = "";
    static int _moveTimeMs = 1200;
    public static void Init(string exePath, int moveTimeMs) { _exe = exePath; _moveTimeMs = moveTimeMs > 0 ? moveTimeMs : 1200; }
    public static bool Available => _exe.Length > 0 && File.Exists(_exe);

    public record Result(string Best, string ScoreType, int ScoreVal, string Pv);

    public static Result? Analyze(string fen)
    {
        if (!Available) return null;
        Process? p = null;
        string pv = "";
        try
        {
            p = Process.Start(new ProcessStartInfo(_exe)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return null;
            p.StandardInput.WriteLine("uci");
            p.StandardInput.WriteLine("isready");
            p.StandardInput.WriteLine("position fen " + fen);
            p.StandardInput.WriteLine("go movetime " + _moveTimeMs);
            p.StandardInput.Flush();

            // Watchdog: kalau engine macet (tak pernah kirim bestmove / pipe menggantung), paksa kill
            // setelah movetime+6 dtk -> ReadLine() balik null & loop berhenti (tak menggantung thread).
            var pw = p;
            _ = Task.Run(async () => { try { await Task.Delay(_moveTimeMs + 6000); if (!pw.HasExited) pw.Kill(true); } catch { } });

            string? best = null; string st = "cp"; int sv = 0;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < _moveTimeMs + 5000)
            {
                string? line = p.StandardOutput.ReadLine();
                if (line is null) break;
                if (line.StartsWith("info ") && line.Contains(" score "))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int i = Array.IndexOf(parts, "score");
                    if (i >= 0 && i + 2 < parts.Length) { st = parts[i + 1]; int.TryParse(parts[i + 2], out sv); }
                    int pi = Array.IndexOf(parts, "pv");
                    if (pi >= 0 && pi + 1 < parts.Length) pv = string.Join(" ", parts.Skip(pi + 1)); // simpan PV terdalam
                }
                if (line.StartsWith("bestmove"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) best = parts[1];
                    break;
                }
            }
            try { p.StandardInput.WriteLine("quit"); p.StandardInput.Flush(); } catch { }
            if (!p.WaitForExit(2000)) { try { p.Kill(true); } catch { } }
            return best is null || best == "(none)" ? null : new Result(best, st, sv, pv);
        }
        catch { try { p?.Kill(true); } catch { } return null; }
        finally { p?.Dispose(); }
    }

    /// <summary>Analisa MultiPV: kembalikan N langkah terbaik (urut skor). Result.Best = langkah UCI,
    /// Result.Pv = garis lanjutan. Kosong kalau gagal.</summary>
    public static List<Result> AnalyzeMulti(string fen, int multiPv)
    {
        var outList = new List<Result>();
        if (!Available) return outList;
        Process? p = null;
        try
        {
            p = Process.Start(new ProcessStartInfo(_exe)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return outList;
            p.StandardInput.WriteLine("uci");
            p.StandardInput.WriteLine("isready");
            p.StandardInput.WriteLine("setoption name MultiPV value " + multiPv);
            p.StandardInput.WriteLine("position fen " + fen);
            p.StandardInput.WriteLine("go movetime " + _moveTimeMs);
            p.StandardInput.Flush();
            var pw = p;
            _ = Task.Run(async () => { try { await Task.Delay(_moveTimeMs + 6000); if (!pw.HasExited) pw.Kill(true); } catch { } });

            var best = new Dictionary<int, (string mv, string st, int sv, string pv)>();
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < _moveTimeMs + 5000)
            {
                string? line = p.StandardOutput.ReadLine();
                if (line is null) break;
                if (line.StartsWith("info ") && line.Contains(" multipv ") && line.Contains(" score ") && line.Contains(" pv "))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int mi = Array.IndexOf(parts, "multipv");
                    int si = Array.IndexOf(parts, "score");
                    int pi = Array.IndexOf(parts, "pv");
                    if (mi >= 0 && si + 2 < parts.Length && pi + 1 < parts.Length)
                    {
                        int.TryParse(parts[mi + 1], out int idx);
                        string st = parts[si + 1]; int.TryParse(parts[si + 2], out int sv);
                        string pv = string.Join(" ", parts.Skip(pi + 1));   // keep deepest per index
                        best[idx] = (parts[pi + 1], st, sv, pv);
                    }
                }
                if (line.StartsWith("bestmove")) break;
            }
            try { p.StandardInput.WriteLine("quit"); p.StandardInput.Flush(); } catch { }
            if (!p.WaitForExit(2000)) { try { p.Kill(true); } catch { } }
            foreach (var kv in best.OrderBy(k => k.Key))
                if (kv.Value.mv != "(none)") outList.Add(new Result(kv.Value.mv, kv.Value.st, kv.Value.sv, kv.Value.pv));
            return outList;
        }
        catch { try { p?.Kill(true); } catch { } return outList; }
        finally { p?.Dispose(); }
    }
}

/// <summary>Analisa-dari-gambar yang menunggu jawaban giliran (Putih/Hitam). Key = jid|pengirim. TTL 5 menit.</summary>
static class PendingAnalysis
{
    static readonly object _l = new();
    static readonly Dictionary<string, (string placement, DateTime at)> _m = new();
    public static void Set(string key, string placement) { lock (_l) _m[key] = (placement, DateTime.UtcNow); }
    public static bool Has(string key) { lock (_l) { return _m.TryGetValue(key, out var v) && (DateTime.UtcNow - v.at).TotalMinutes <= 5; } }
    public static string? Take(string key)
    {
        lock (_l)
        {
            if (_m.TryGetValue(key, out var v)) { _m.Remove(key); if ((DateTime.UtcNow - v.at).TotalMinutes <= 5) return v.placement; }
            return null;
        }
    }
}

/// <summary>Analisis posisi catur untuk WhatsApp: terima FEN atau PGN, balas langkah terbaik + evaluasi.</summary>
static class ChessAnalysis
{
    /// <summary>Hasil analisis. Fen = posisi yang dianalisa (untuk render papan).</summary>
    public record Output(string Text, string Fen);

    public static async Task<Output?> Run(string input, AiConfig? ai, HttpClient http, ILogger logger)
    {
        input = (input ?? "").Trim();
        if (input.Length == 0)
            return new Output("Kirim posisi dalam *FEN* atau jalannya game dalam *PGN* setelah perintah. Contoh:\n`!analisa rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1`", "");

        // "Langkah fokus" di belakang FEN (mis. "<FEN> Qd6" / "<FEN> kenapa Qd6") -> kritik langkah itu vs terbaik.
        string? focusMove = null;
        if (LooksLikeFen(input))
        {
            var toks = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (toks.Length > 6)
            {
                for (int i = 6; i < toks.Length; i++)
                    if (LooksLikeMove(toks[i])) { focusMove = toks[i]; break; }
                if (focusMove != null) input = string.Join(" ", toks.Take(6));
            }
        }

        string fen;
        try
        {
            if (LooksLikeFen(input)) { ChessBoard.LoadFromFen(input); fen = input; }
            else { var b = ChessBoard.LoadFromPgn(input); fen = b.ToFen(); }
        }
        catch
        {
            return new Output("Maaf, posisinya tak terbaca. Pastikan *FEN* valid atau *PGN* benar.", "");
        }

        if (focusMove != null)
            return await Critique(fen, focusMove, ai, http, logger);

        if (!StockfishEngine.Available)
            return new Output("Engine analisa belum siap di server.", fen);

        var lines = await Task.Run(() => StockfishEngine.AnalyzeMulti(fen, 3));
        var res = lines.Count > 0 ? lines[0] : await Task.Run(() => StockfishEngine.Analyze(fen));
        if (res is null)
            return new Output("Engine tak merespons. Coba lagi sebentar ya.", fen);

        bool whiteToMove = fen.Contains(" w ");
        string who = whiteToMove ? "Putih" : "Hitam";
        string san = UciToSan(ChessBoard.LoadFromFen(fen), res.Best) ?? Coord(res.Best);
        string eval = EvalText(res.ScoreType, res.ScoreVal, who);
        string pvSan = PvToSan(fen, res.Pv, 6);

        string text = $"\U0001F50D *Analisis posisi* ({who} jalan)\n" +
                      $"Langkah terbaik: *{san}*\n" +
                      $"Evaluasi: {eval}";
        if (pvSan.Length > 0) text += $"\nLanjutan: {pvSan}";
        if (lines.Count > 1)
        {
            var alts = new List<string>();
            for (int i = 1; i < lines.Count && i < 3; i++)
            {
                string s2 = UciToSan(ChessBoard.LoadFromFen(fen), lines[i].Best) ?? Coord(lines[i].Best);
                string ce = lines[i].ScoreType == "mate" ? $"#{Math.Abs(lines[i].ScoreVal)}" : $"{(lines[i].ScoreVal >= 0 ? "+" : "")}{lines[i].ScoreVal / 100.0:0.0}";
                alts.Add($"{s2} ({ce})");
            }
            if (alts.Count > 0) text += "\nAlternatif: " + string.Join(", ", alts);
        }

        // Penjelasan KATA-KATA dari AI (menjelaskan langkah yang SUDAH pasti benar dari engine -> minim ngarang).
        if (ai is { Enabled: true })
        {
            string prompt =
                $"Ini analisis catur. Posisi FEN: {fen}. {who} yang jalan. " +
                $"Langkah TERBAIK menurut engine Stockfish: {san}. Evaluasi: {eval}. " +
                (pvSan.Length > 0 ? $"Lanjutan yang diharapkan: {pvSan}. " : "") +
                "Jelaskan dalam 1-2 kalimat SINGKAT dan natural KENAPA langkah itu bagus: apa yang diincar/diserang, " +
                "petak atau jalur (file/diagonal) yang dibuka atau dikuasai, bidak yang dimenangkan, atau rencananya. " +
                "Langkah terbaik ini SUDAH PASTI benar dari engine - JANGAN usulkan langkah lain, JANGAN ragukan, cukup jelaskan idenya. " +
                "Kalau TIDAK yakin alasan konkretnya, sebut tujuan UMUM saja (menguasai pusat, mengembangkan bidak, mengamankan raja, menyiapkan dorongan pion, menekan bidak lawan) - " +
                "JANGAN mengarang efek spesifik yang belum tentu benar (mis. menyebut diagonal/petak/jalur yang sebenarnya tidak terbuka). " +
                "Kalau skakmat, jelaskan polanya singkat.";
            try
            {
                string? expl = await Ai.Ask(ai, http, prompt, logger);
                if (!string.IsNullOrWhiteSpace(expl)) text += $"\n\U0001F4A1 {expl!.Trim()}";
            }
            catch { }
        }
        return new Output(text, fen);
    }

    static string PvToSan(string fen, string pvUci, int maxPlies)
    {
        try
        {
            var b = ChessBoard.LoadFromFen(fen);
            var list = new List<string>();
            foreach (var u in pvUci.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (list.Count >= maxPlies || u.Length != 4) break;
                var m = new Move(u.Substring(0, 2), u.Substring(2, 2));
                if (!b.Move(m)) break;
                list.Add(m.San);
            }
            return string.Join(" ", list);
        }
        catch { return ""; }
    }

    static bool LooksLikeFen(string s)
    {
        // FEN: bidang papan pakai '/' dan ada giliran ' w '/' b '. PGN tak begitu.
        var first = s.Split(' ')[0];
        return first.Count(c => c == '/') == 7;
    }

    static string Coord(string uci) => uci.Length >= 4 ? uci.Substring(0, 2) + "-" + uci.Substring(2, 2) + (uci.Length > 4 ? "=" + char.ToUpper(uci[4]) : "") : uci;

    static string? UciToSan(ChessBoard board, string uci)
    {
        try
        {
            if (uci.Length != 4) return null;       // promosi (len 5) -> pakai notasi koordinat
            string from = uci.Substring(0, 2), to = uci.Substring(2, 2);
            var move = new Move(from, to);
            if (!board.Move(move)) return null;     // validasi + isi San
            return move.San;
        }
        catch { return null; }
    }

    // Kritik satu langkah spesifik vs langkah terbaik engine (grounded Stockfish, anti-ngarang).
    public static async Task<Output?> Critique(string fen, string userMove, AiConfig? ai, HttpClient http, ILogger logger)
    {
        if (!StockfishEngine.Available)
            return new Output("Engine analisa belum siap di server.", fen);
        bool whiteToMove = fen.Contains(" w ");
        string who = whiteToMove ? "Putih" : "Hitam";

        var best = await Task.Run(() => StockfishEngine.Analyze(fen));
        if (best is null) return new Output("Engine tak merespons. Coba lagi sebentar ya.", fen);
        string bestSan = UciToSan(ChessBoard.LoadFromFen(fen), best.Best) ?? Coord(best.Best);
        string bestEval = EvalText(best.ScoreType, best.ScoreVal, who);

        ChessBoard b;
        try { b = ChessBoard.LoadFromFen(fen); } catch { return new Output("Posisi tak terbaca.", ""); }
        Move chosen = default;
        bool found = false;
        string nu = NormSan(userMove);
        try { foreach (var m in b.Moves()) { if (NormSan(m.San) == nu) { chosen = m; found = true; break; } } } catch { }
        if (!found)
            return new Output($"Langkah *{userMove}* sepertinya tak legal/tak dikenali di posisi ini. Cek lagi ya.\n(Langkah terbaik: *{bestSan}*, eval {bestEval})", fen);

        string userSan = chosen.San;
        bool isBest = NormSan(userSan) == NormSan(bestSan);
        b.Move(chosen);
        string afterFen = b.ToFen();

        string userEval = "?"; string refPv = "";
        var after = await Task.Run(() => StockfishEngine.Analyze(afterFen));
        if (after != null)
        {
            userEval = EvalText(after.ScoreType, -after.ScoreVal, who);  // giliran lawan -> balik tanda ke sudut pandang pemain
            refPv = PvToSan(afterFen, after.Pv, 5);
        }

        string text;
        if (isBest)
            text = $"\U0001F50D *{userSan}* \u2014 itu memang langkah TERBAIK! (eval {bestEval})";
        else
            text = $"\U0001F50D *Analisa langkah* ({who} jalan)\n" +
                   $"Langkahmu *{userSan}*: eval {userEval}\n" +
                   $"Terbaik *{bestSan}*: eval {bestEval}" +
                   (refPv.Length > 0 ? $"\nSetelah {userSan}, lawan: {refPv}" : "");

        if (ai is { Enabled: true })
        {
            string prompt = isBest
                ? $"Posisi catur FEN {fen}, {who} jalan. Pemain memainkan {userSan} dan itu MEMANG langkah terbaik menurut engine (eval {bestEval}). Jelaskan 1-2 kalimat singkat & natural kenapa bagus. Jangan ragukan, jangan usulkan langkah lain."
                : $"Posisi catur FEN {fen}, {who} jalan. Pemain main {userSan} (eval {userEval}), padahal langkah TERBAIK menurut Stockfish adalah {bestSan} (eval {bestEval}). " +
                  (refPv.Length > 0 ? $"Setelah {userSan}, lawan membalas: {refPv}. " : "") +
                  $"Jelaskan 1-2 kalimat SINGKAT & natural kenapa {userSan} kurang baik dibanding {bestSan}, BERDASAR angka & garis engine di atas SAJA. " +
                  $"JANGAN mengarang garis lain. Kalau tak yakin detail, sebut alasan umum (kehilangan tempo/bidak, melemahkan raja, melewatkan taktik/skak). JANGAN menyebut langkah selain {bestSan} sebagai terbaik.";
            try { string? e = await Ai.Ask(ai, http, prompt, logger); if (!string.IsNullOrWhiteSpace(e)) text += $"\n\U0001F4A1 {e!.Trim()}"; } catch { }
        }
        return new Output(text, fen);
    }

    // Versi "TANPA BOCOR" untuk jawaban-salah puzzle di GRUP yang masih aktif: tunjukkan eval langkah pemain +
    // dorong cari yang lebih memaksa, TAPI tak pernah menyebut langkah terbaik/solusi (puzzle tetap utuh untuk yang
    // lain). Engine-only, TANPA AI -> mustahil membocorkan. Codex: panggil ini di handler jawaban-salah puzzle grup.
    public static async Task<string?> CritiqueSafe(string fen, string userMove)
    {
        if (!StockfishEngine.Available) return null;
        bool whiteToMove = fen.Contains(" w ");
        string who = whiteToMove ? "Putih" : "Hitam";
        var best = await Task.Run(() => StockfishEngine.Analyze(fen));
        if (best is null) return null;
        ChessBoard b;
        try { b = ChessBoard.LoadFromFen(fen); } catch { return null; }
        Move chosen = default; bool found = false;
        string nu = NormSan(userMove);
        try { foreach (var m in b.Moves()) { if (NormSan(m.San) == nu) { chosen = m; found = true; break; } } } catch { }
        if (!found) return null;                       // langkah tak legal -> biar hint biasa yang jalan
        string userSan = chosen.San;
        b.Move(chosen);
        var after = await Task.Run(() => StockfishEngine.Analyze(b.ToFen()));
        int Cp(StockfishEngine.Result r) => r.ScoreType == "mate" ? (r.ScoreVal > 0 ? 100000 : -100000) : r.ScoreVal;
        int bestCp = Cp(best);
        int userCp = after != null ? -Cp(after) : 0;   // sudut pandang pemain
        string userEval = after != null ? EvalText(after.ScoreType, -after.ScoreVal, who) : "?";
        bool muchBetter = (bestCp - userCp) >= 150;     // ada langkah >= 1.5 pion lebih kuat
        return $"\U0001F50D Langkahmu *{userSan}*: eval {userEval}.\n" +
               (muchBetter
                   ? "Di posisi ini ada langkah yang jauh lebih kuat \u2014 cari yang memaksa: skak, tangkapan, atau ancaman."
                   : "Lumayan, tapi belum yang paling tepat. Coba langkah yang lebih memaksa.");
    }

    static bool LooksLikeMove(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s == "O-O" || s == "O-O-O" || s == "0-0" || s == "0-0-0") return true;
        string t = s.TrimEnd('+', '#', '!', '?');
        int eq = t.IndexOf('=');
        if (eq > 0) t = t.Substring(0, eq);
        if (t.Length < 2 || t.Length > 6) return false;
        char f = t[t.Length - 2], r = t[t.Length - 1];
        if (f < 'a' || f > 'h' || r < '1' || r > '8') return false;
        char c0 = t[0];
        return c0 == 'K' || c0 == 'Q' || c0 == 'R' || c0 == 'B' || c0 == 'N' || (c0 >= 'a' && c0 <= 'h');
    }

    static string NormSan(string s) => new string((s ?? "").Where(c => !"xX+#!?=".Contains(c)).ToArray()).Replace("0", "O").ToLowerInvariant();

    static string EvalText(string type, int val, string who)
    {
        // Skor dari sudut pandang pihak yang JALAN (mover = who).
        if (type == "mate")
        {
            int n = Math.Abs(val);
            return val > 0 ? $"skakmat dalam {n} untuk {who} \U0001F3C1" : $"{who} akan diskakmat dalam {n} \u26A0\uFE0F";
        }
        double pawns = val / 100.0;
        string mag = Math.Abs(pawns) < 0.3 ? "seimbang" :
                     Math.Abs(pawns) < 1.0 ? "sedikit unggul" :
                     Math.Abs(pawns) < 3.0 ? "unggul" : "menang jelas";
        if (Math.Abs(pawns) < 0.3) return $"kira-kira seimbang ({(pawns >= 0 ? "+" : "")}{pawns:0.0})";
        string side = pawns > 0 ? who : (who == "Putih" ? "Hitam" : "Putih");
        return $"{side} {mag} ({(pawns >= 0 ? "+" : "")}{pawns:0.0})";
    }
}
