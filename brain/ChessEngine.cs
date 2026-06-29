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

        if (!StockfishEngine.Available)
            return new Output("Engine analisa belum siap di server.", fen);

        var res = await Task.Run(() => StockfishEngine.Analyze(fen));
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
