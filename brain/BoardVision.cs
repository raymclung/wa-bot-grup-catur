using SkiaSharp;
using Svg.Skia;

/// <summary>Pengenalan posisi catur dari gambar papan DIGITAL (mis. screenshot Lichess cburnett)
/// -> FEN. Deterministik: render tiap bidak cburnett (transparan) sekali, lalu tiap petak
/// dicocokkan (template matching) terhadap 12 bidak + kosong, dengan warna latar dideteksi
/// PER-PETAK (tahan highlight langkah). Bukan AI -> akurat untuk papan digital bersih.</summary>
static class BoardVision
{
    const int S = 48;  // ukuran tiap petak saat analisa (px)

    static readonly (char ch, string file)[] PieceFiles =
    {
        ('K', "wK"), ('Q', "wQ"), ('R', "wR"), ('B', "wB"), ('N', "wN"), ('P', "wP"),
        ('k', "bK"), ('q', "bQ"), ('r', "bR"), ('b', "bB"), ('n', "bN"), ('p', "bP"),
    };

    static readonly object _lock = new();
    static byte[][]? _pieceBytes;   // tiap: S*S*4 BGRA (latar transparan)
    static char[]? _pieceChars;

    static void EnsurePieces(string assetsDir)
    {
        if (_pieceBytes != null) return;
        lock (_lock)
        {
            if (_pieceBytes != null) return;
            var bl = new List<byte[]>(); var cl = new List<char>();
            foreach (var (ch, file) in PieceFiles)
            {
                string p = Path.Combine(assetsDir, file + ".svg");
                if (!File.Exists(p)) continue;
                var svg = new SKSvg(); try { svg.Load(p); } catch { continue; }
                if (svg.Picture is null) continue;
                using var bmp = new SKBitmap(new SKImageInfo(S, S, SKColorType.Bgra8888, SKAlphaType.Unpremul));
                using (var cv = new SKCanvas(bmp))
                {
                    cv.Clear(SKColors.Transparent);
                    float scale = S / 45f;               // PENUH satu petak (Lichess gambar bidak edge-to-edge)
                    cv.Scale(scale); cv.DrawPicture(svg.Picture);
                }
                bl.Add(bmp.Bytes); cl.Add(ch);
            }
            _pieceBytes = bl.ToArray(); _pieceChars = cl.ToArray();
        }
    }

    /// <summary>Gambar papan -> FEN penempatan bidak (rank8..rank1). null kalau gagal.
    /// flip=true kalau Hitam di bawah. Asumsi v1: gambar = papan (di-crop tengah jadi persegi).</summary>
    public static string? RecognizeFen(byte[] img, string assetsDir, bool flip = false)
    {
        EnsurePieces(assetsDir);
        if (_pieceBytes is null || _pieceBytes.Length == 0) return null;
        using var src = SKBitmap.Decode(img);
        if (src is null) return null;

        // crop tengah jadi persegi, lalu skala ke 8S x 8S (tiap petak = S)
        int side = Math.Min(src.Width, src.Height);
        int ox = (src.Width - side) / 2, oy = (src.Height - side) / 2;
        var info = new SKImageInfo(8 * S, 8 * S, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var board = new SKBitmap(info);
        using (var cv = new SKCanvas(board))
        {
            cv.DrawBitmap(src, new SKRect(ox, oy, ox + side, oy + side), new SKRect(0, 0, 8 * S, 8 * S));
        }
        byte[] bd = board.Bytes; int stride = 8 * S * 4;

        char[,] grid = new char[8, 8];
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                grid[r, c] = Classify(bd, stride, c * S, r * S);

        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < 8; r++)
        {
            int empty = 0;
            for (int c = 0; c < 8; c++)
            {
                int rr = flip ? 7 - r : r, cc = flip ? 7 - c : c;
                char ch = grid[rr, cc];
                if (ch == '.') empty++;
                else { if (empty > 0) { sb.Append(empty); empty = 0; } sb.Append(ch); }
            }
            if (empty > 0) sb.Append(empty);
            if (r < 7) sb.Append('/');
        }
        return sb.ToString();
    }

    /// <summary>Lengkapi FEN penempatan jadi FEN penuh: tambah giliran + hak rokade (disimpulkan dari
    /// posisi raja/benteng di petak asal) + en passant '-'. Dari gambar, giliran tak diketahui -> default Putih.</summary>
    public static string BuildFullFen(string placement, bool white)
    {
        string castle = "";
        var rows = placement.Split('/');
        if (rows.Length == 8)
        {
            string r8 = Expand8(rows[0]);   // rank 8 (a8..h8)
            string r1 = Expand8(rows[7]);   // rank 1 (a1..h1)
            if (r1[4] == 'K') { if (r1[7] == 'R') castle += "K"; if (r1[0] == 'R') castle += "Q"; }
            if (r8[4] == 'k') { if (r8[7] == 'r') castle += "k"; if (r8[0] == 'r') castle += "q"; }
        }
        if (castle.Length == 0) castle = "-";
        return placement + " " + (white ? "w" : "b") + " " + castle + " - 0 1";
    }

    static string Expand8(string row)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in row) { if (char.IsDigit(c)) sb.Append('.', c - '0'); else sb.Append(c); }
        while (sb.Length < 8) sb.Append('.');
        return sb.ToString(0, 8);
    }

    static char Classify(byte[] bd, int stride, int x0, int y0)
    {
        var (br, bg, bb) = AvgCorners(bd, stride, x0, y0);
        // KOSONG ditentukan dari JUMLAH TINTA (piksel beda jauh dari latar), bukan MAD ke latar.
        // Penting: bidak PUTIH di petak TERANG kontrasnya rendah (isi putih ~ petak), tapi outline
        // gelap tetap menghasilkan cukup tinta -> tak salah dianggap kosong. Highlight langkah =
        // tint seragam -> tinta ~0 -> tetap kosong dengan benar.
        int ink = InkCount(bd, stride, x0, y0, br, bg, bb, 45);
        if (ink < (int)(S * S * 0.035)) return '.';
        long best = long.MaxValue; char bestCh = '.';
        for (int i = 0; i < _pieceBytes!.Length; i++)
        {
            long mad = MadPiece(bd, stride, x0, y0, _pieceBytes[i], br, bg, bb);  // offset-toleran
            if (mad < best) { best = mad; bestCh = _pieceChars![i]; }
        }
        return bestCh;
    }

    static int InkCount(byte[] bd, int stride, int x0, int y0, int br, int bg, int bb, int t)
    {
        int n = 0;
        for (int j = 0; j < S; j++)
        {
            int row = (y0 + j) * stride + x0 * 4;
            for (int i = 0; i < S; i++)
            {
                int o = row + i * 4;
                if (Math.Abs(bd[o] - bb) > t || Math.Abs(bd[o + 1] - bg) > t || Math.Abs(bd[o + 2] - br) > t) n++;
            }
        }
        return n;
    }

    static (int r, int g, int b) AvgCorners(byte[] bd, int stride, int x0, int y0)
    {
        int[] xs = { 3, S - 4, 3, S - 4 }, ys = { 3, 3, S - 4, S - 4 };
        long r = 0, g = 0, b = 0;
        for (int k = 0; k < 4; k++)
        {
            int o = (y0 + ys[k]) * stride + (x0 + xs[k]) * 4;
            b += bd[o]; g += bd[o + 1]; r += bd[o + 2];
        }
        return ((int)(r / 4), (int)(g / 4), (int)(b / 4));
    }

    static long MadEmpty(byte[] bd, int stride, int x0, int y0, int br, int bg, int bb)
    {
        long sum = 0;
        for (int j = 0; j < S; j++)
        {
            int row = (y0 + j) * stride + x0 * 4;
            for (int i = 0; i < S; i++)
            {
                int o = row + i * 4;
                sum += Math.Abs(bd[o] - bb) + Math.Abs(bd[o + 1] - bg) + Math.Abs(bd[o + 2] - br);
            }
        }
        return sum;
    }

    static long MadPiece(byte[] bd, int stride, int x0, int y0, byte[] piece, int br, int bg, int bb)
    {
        long sum = 0;
        for (int j = 0; j < S; j++)
        {
            int row = (y0 + j) * stride + x0 * 4;
            int prow = j * S * 4;
            for (int i = 0; i < S; i++)
            {
                int o = row + i * 4, po = prow + i * 4;
                int pa = piece[po + 3];
                int eb = (piece[po] * pa + bb * (255 - pa)) / 255;
                int eg = (piece[po + 1] * pa + bg * (255 - pa)) / 255;
                int er = (piece[po + 2] * pa + br * (255 - pa)) / 255;
                sum += Math.Abs(bd[o] - eb) + Math.Abs(bd[o + 1] - eg) + Math.Abs(bd[o + 2] - er);
            }
        }
        return sum;
    }
}
