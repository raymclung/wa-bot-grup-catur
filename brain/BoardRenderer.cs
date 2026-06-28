using System.Security.Cryptography;
using System.Text;
using SkiaSharp;
using Svg.Skia;

/// <summary>Render papan catur dari FEN menjadi PNG (lokal, tanpa layanan luar).
/// Bidak memakai set SVG Lichess "cburnett" (assets/pieces). Hasil di-cache per posisi.</summary>
static class BoardRenderer
{
    const int Square = 80;
    const int Margin = 26;                       // bingkai kayu + ruang koordinat
    const int Size = Square * 8 + Margin * 2;
    const string CacheVersion = "v2-svg";        // ganti -> invalidasi cache lama

    static readonly SKColor Light = new(0xF0, 0xD9, 0xB5);  // krem (lichess brown)
    static readonly SKColor Dark = new(0xB5, 0x88, 0x63);   // cokelat
    static readonly SKColor Frame = new(0x53, 0x39, 0x22);  // bingkai kayu gelap
    static readonly SKColor CoordText = new(0xF1, 0xE4, 0xCE);

    // peta huruf FEN -> nama file svg
    static readonly Dictionary<char, string> PieceFile = new()
    {
        ['K'] = "wK", ['Q'] = "wQ", ['R'] = "wR", ['B'] = "wB", ['N'] = "wN", ['P'] = "wP",
        ['k'] = "bK", ['q'] = "bQ", ['r'] = "bR", ['b'] = "bB", ['n'] = "bN", ['p'] = "bP",
    };

    static readonly object _lock = new();
    static Dictionary<char, SKSvg>? _pieces; // disimpan agar SKPicture tidak ikut di-GC

    static void EnsurePieces(string assetsDir)
    {
        if (_pieces != null) return;
        lock (_lock)
        {
            if (_pieces != null) return;
            var dict = new Dictionary<char, SKSvg>();
            foreach (var (ch, file) in PieceFile)
            {
                string p = Path.Combine(assetsDir, file + ".svg");
                if (!File.Exists(p)) continue;
                var svg = new SKSvg();
                try { svg.Load(p); } catch { continue; }
                if (svg.Picture != null) dict[ch] = svg;
            }
            _pieces = dict;
        }
    }

    /// <summary>Render FEN -> path PNG (cache). flip=true menaruh Hitam di bawah.</summary>
    public static string Render(string fen, bool flip, string cacheDir, string assetsDir)
    {
        Directory.CreateDirectory(cacheDir);
        string key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(CacheVersion + "|" + fen + "|" + flip))).Substring(0, 16);
        string path = Path.Combine(cacheDir, key + ".png");
        if (File.Exists(path)) return path;

        EnsurePieces(assetsDir);

        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        var canvas = surface.Canvas;
        canvas.Clear(Frame);

        using var lightP = new SKPaint { Color = Light, IsAntialias = true };
        using var darkP = new SKPaint { Color = Dark, IsAntialias = true };
        using var coordPaint = new SKPaint { Color = CoordText, IsAntialias = true };
        using var coordFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default, 15);

        // Kotak papan (origin di Margin,Margin)
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                bool isLight = (r + c) % 2 == 0;
                canvas.DrawRect(Margin + c * Square, Margin + r * Square, Square, Square, isLight ? lightP : darkP);
            }

        // Garis tipis pemisah papan/bingkai
        using var edge = new SKPaint { Color = new SKColor(0x2E, 0x20, 0x14), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawRect(Margin - 1, Margin - 1, Square * 8 + 2, Square * 8 + 2, edge);

        // Koordinat di bingkai
        for (int i = 0; i < 8; i++)
        {
            int rankNum = flip ? i + 1 : 8 - i;
            char fileCh = (char)('a' + (flip ? 7 - i : i));
            canvas.DrawText(rankNum.ToString(), Margin / 2f, Margin + i * Square + Square / 2f + 5, SKTextAlign.Center, coordFont, coordPaint);
            canvas.DrawText(fileCh.ToString(), Margin + i * Square + Square / 2f, Size - Margin / 2f + 5, SKTextAlign.Center, coordFont, coordPaint);
        }

        // Bidak (SVG cburnett, viewBox 45x45)
        var pieces = _pieces ?? new();
        float scale = (Square * 0.86f) / 45f;
        float pad = (Square - 45f * scale) / 2f;
        var rows = fen.Split(' ')[0].Split('/'); // rank8..rank1
        for (int rank = 0; rank < 8 && rank < rows.Length; rank++)
        {
            int file = 0;
            foreach (char ch in rows[rank])
            {
                if (char.IsDigit(ch)) { file += ch - '0'; continue; }
                if (!pieces.TryGetValue(ch, out var svg) || svg.Picture is null) { file++; continue; }
                int dr = flip ? 7 - rank : rank;
                int df = flip ? 7 - file : file;
                float x = Margin + df * Square + pad;
                float y = Margin + dr * Square + pad;
                canvas.Save();
                canvas.Translate(x, y);
                canvas.Scale(scale);
                canvas.DrawPicture(svg.Picture);
                canvas.Restore();
                file++;
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(path);
        data.SaveTo(fs);
        return path;
    }
}
