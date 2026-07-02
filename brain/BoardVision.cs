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
                    float scale = S * 0.86f / 45f;       // samakan dgn BoardRenderer (bidak 86% petak, di-tengah)
                    float pad = (S - 45f * scale) / 2f;
                    cv.Translate(pad, pad); cv.Scale(scale); cv.DrawPicture(svg.Picture);
                }
                bl.Add(bmp.Bytes); cl.Add(ch);
            }
            // Template TAMBAHAN Chess.com (raw BGRA S*S*4 .bin), diekstrak dari board asli.
            // Dicocokkan bersama cburnett -> papan Lichess & Chess.com sama-sama kebaca (best match menang).
            string ccDir = Path.Combine(assetsDir, "chesscom");
            if (Directory.Exists(ccDir))
            {
                foreach (var (ch, file) in PieceFiles)
                {
                    string bp = Path.Combine(ccDir, file + ".bin");
                    if (!File.Exists(bp)) continue;
                    byte[] raw; try { raw = File.ReadAllBytes(bp); } catch { continue; }
                    if (raw.Length == S * S * 4) { bl.Add(raw); cl.Add(ch); }
                }
            }
            // Hanya cache kalau aset benar-benar termuat; jangan kunci array KOSONG permanen
            // (mis. path aset salah saat panggil pertama) -> biar panggilan berikutnya coba lagi.
            if (bl.Count > 0) { _pieceBytes = bl.ToArray(); _pieceChars = cl.ToArray(); }
        }
    }

    /// <summary>Gambar papan -> FEN penempatan bidak (rank8..rank1). null kalau gagal.
    /// flip=true kalau Hitam di bawah. Asumsi v1: gambar = papan (di-crop tengah jadi persegi).</summary>

    // Potong margin/border berwarna seragam (mis. border coklat + label koordinat Lichess/render bot)
    // supaya yang tersisa hanya area 8x8 papan. Heuristik: dari tiap sisi, buang baris/kolom yang
    // >85% mirip warna border (diambil dari pojok), berhenti saat menemui pola papan (kotak-kotak).
    static SKBitmap TrimToBoard(SKBitmap src)
    {
        int w=src.Width,h=src.Height; var px=src.Pixels;
        SKColor bc=px[0];
        bool simrow(int y){int same=0;for(int x=0;x<w;x++){var c=px[y*w+x];if(Math.Abs(c.Red-bc.Red)+Math.Abs(c.Green-bc.Green)+Math.Abs(c.Blue-bc.Blue)<48)same++;}return same>w*85/100;}
        bool simcol(int x){int same=0;for(int y=0;y<h;y++){var c=px[y*w+x];if(Math.Abs(c.Red-bc.Red)+Math.Abs(c.Green-bc.Green)+Math.Abs(c.Blue-bc.Blue)<48)same++;}return same>h*85/100;}
        int top=0,bot=h-1,left=0,right=w-1;
        while(top<bot&&simrow(top))top++;
        while(bot>top&&simrow(bot))bot--;
        while(left<right&&simcol(left))left++;
        while(right>left&&simcol(right))right--;
        int bw=right-left+1,bh=bot-top+1;
        if(bw<64||bh<64||(bw==w&&bh==h)) return src; // tak ada border terdeteksi
        var info=new SKImageInfo(bw,bh,SKColorType.Bgra8888,SKAlphaType.Unpremul);
        var outb=new SKBitmap(info);
        using(var cv=new SKCanvas(outb)) cv.DrawBitmap(src,new SKRect(left,top,right+1,bot+1),new SKRect(0,0,bw,bh));
        return outb;
    }

    public static string? RecognizeFen(byte[] img, string assetsDir, bool flip = false)
    {
        EnsurePieces(assetsDir);
        if (_pieceBytes is null || _pieceBytes.Length == 0) return null;
        using var src0 = SKBitmap.Decode(img);
        if (src0 is null) return null;
        using var src = TrimToBoard(src0);

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

    /// <summary>Balik penempatan 180 derajat (papan yang di-screenshot dari sisi Hitam).
    /// a8 &lt;-&gt; h1, dst. Input/output = FEN penempatan (tanpa giliran).</summary>
    public static string FlipPlacement(string placement)
    {
        var full = new System.Text.StringBuilder();
        foreach (var row in placement.Split('/'))
            foreach (char c in row) { if (char.IsDigit(c)) full.Append('.', c - '0'); else full.Append(c); }
        while (full.Length < 64) full.Append('.');
        char[] arr = full.ToString().Substring(0, 64).ToCharArray();
        System.Array.Reverse(arr);   // putar 180 derajat
        var outSb = new System.Text.StringBuilder();
        for (int r = 0; r < 8; r++)
        {
            int empty = 0;
            for (int c = 0; c < 8; c++)
            {
                char ch = arr[r * 8 + c];
                if (ch == '.') empty++;
                else { if (empty > 0) { outSb.Append(empty); empty = 0; } outSb.Append(ch); }
            }
            if (empty > 0) outSb.Append(empty);
            if (r < 7) outSb.Append('/');
        }
        return outSb.ToString();
    }

    /// <summary>Skor arah bidak: rata-rata rank bidak Putih - rata-rata rank bidak Hitam.
    /// Bidak Putih tak bisa mundur, normalnya di bawah (rank kecil) -> skor negatif.
    /// Skor jelas positif = papan kemungkinan terbalik (perspektif Hitam).</summary>
    public static double PawnDirectionScore(string placement)
    {
        string[] rows = placement.Split('/');
        double wSum = 0, bSum = 0; int wN = 0, bN = 0;
        for (int r = 0; r < rows.Length && r < 8; r++)
        {
            int rank = 8 - r;
            foreach (char c in rows[r])
            {
                if (char.IsDigit(c)) continue;
                if (c == 'P') { wSum += rank; wN++; }
                else if (c == 'p') { bSum += rank; bN++; }
            }
        }
        if (wN == 0 || bN == 0) return 0.0;   // tak bisa putuskan
        return (wSum / wN) - (bSum / bN);
    }

    /// <summary>Kenali papan lalu auto-benarkan orientasi via arah bidak.
    /// flipped=true kalau papan dibalik (terdeteksi dari sisi Hitam).</summary>
    public static string RecognizeFenAuto(byte[] img, string assetsDir, out bool flipped)
    {
        flipped = false;
        string placement = RecognizeFen(img, assetsDir, false);
        if (placement == null) return null;
        if (PawnDirectionScore(placement) > 0.5)   // bidak Putih jelas di atas Hitam -> terbalik
        {
            placement = FlipPlacement(placement);
            flipped = true;
        }
        return placement;
    }

    public static void ResetCache() { lock (_lock) { _pieceBytes = null; _pieceChars = null; } }

    static readonly System.Collections.Generic.Dictionary<char, string> _fileFor = new()
    {
        ['K'] = "wK", ['Q'] = "wQ", ['R'] = "wR", ['B'] = "wB", ['N'] = "wN", ['P'] = "wP",
        ['k'] = "bK", ['q'] = "bQ", ['r'] = "bR", ['b'] = "bB", ['n'] = "bN", ['p'] = "bP",
    };

    /// <summary>Ekstrak template bidak dari SATU papan yang posisinya DIKETAHUI (mis. Chess.com neo),
    /// simpan tiap tipe sbg RAW BGRA (S*S*4) .bin ke outDir. known = (row0=rank8, col0=fileA, char bidak).
    /// Latar petak dibuat transparan via soft-mask jarak-warna. Return jumlah template ditulis.</summary>
    public static int ExtractTemplatesFromBoard(byte[] img, string outDir, (int r, int c, char ch)[] known)
    {
        using var src0 = SKBitmap.Decode(img);
        if (src0 is null) return 0;
        using var src = TrimToBoard(src0);
        int side = Math.Min(src.Width, src.Height);
        int ox = (src.Width - side) / 2, oy = (src.Height - side) / 2;
        var info = new SKImageInfo(8 * S, 8 * S, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var board = new SKBitmap(info);
        using (var cv = new SKCanvas(board))
            cv.DrawBitmap(src, new SKRect(ox, oy, ox + side, oy + side), new SKRect(0, 0, 8 * S, 8 * S));
        byte[] bd = board.Bytes; int stride = 8 * S * 4;
        Directory.CreateDirectory(outDir);
        int count = 0;
        foreach (var (r, c, ch) in known)
        {
            if (!_fileFor.TryGetValue(ch, out var fname)) continue;
            int x0 = c * S, y0 = r * S;
            var (br, bg, bb) = AvgCorners(bd, stride, x0, y0);
            byte[] tb = new byte[S * S * 4];
            for (int j = 0; j < S; j++)
            {
                int row = (y0 + j) * stride + x0 * 4;
                int prow = j * S * 4;
                for (int i = 0; i < S; i++)
                {
                    int o = row + i * 4, po = prow + i * 4;
                    int B = bd[o], G = bd[o + 1], R = bd[o + 2];
                    int dist = Math.Abs(B - bb) + Math.Abs(G - bg) + Math.Abs(R - br);
                    int a = dist * 5; if (a > 255) a = 255;   // dekat warna petak -> transparan
                    if (i < 4 || i >= S - 4 || j < 4 || j >= S - 4) a = 0;   // buang tepi: label koordinat a-h/1-8 jangan ter-bake
                    tb[po] = (byte)B; tb[po + 1] = (byte)G; tb[po + 2] = (byte)R; tb[po + 3] = (byte)a;
                }
            }
            try { File.WriteAllBytes(Path.Combine(outDir, fname + ".bin"), tb); count++; } catch { }
        }
        return count;
    }

    static char Classify(byte[] bd, int stride, int x0, int y0)
    {
        var (br, bg, bb) = AvgCorners(bd, stride, x0, y0);
        // KOSONG ditentukan dari JUMLAH TINTA (piksel beda jauh dari latar), bukan MAD ke latar.
        // Penting: bidak PUTIH di petak TERANG kontrasnya rendah (isi putih ~ petak), tapi outline
        // gelap tetap menghasilkan cukup tinta -> tak salah dianggap kosong. Highlight langkah =
        // tint seragam -> tinta ~0 -> tetap kosong dengan benar.
        int ink = InkCount(bd, stride, x0, y0, br, bg, bb, 45);
        int inner = (S - 12) * (S - 12); if (ink < (int)(inner * 0.045)) return '.';
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
        int n = 0, m = 6;   // abaikan tepi -> toleran misalignment/sliver border
        for (int j = m; j < S - m; j++)
        {
            int row = (y0 + j) * stride + x0 * 4;
            for (int i = m; i < S - m; i++)
            {
                int o = row + i * 4;
                if (Math.Abs(bd[o] - bb) > t || Math.Abs(bd[o + 1] - bg) > t || Math.Abs(bd[o + 2] - br) > t) n++;
            }
        }
        return n;
    }

    static (int r, int g, int b) AvgCorners(byte[] bd, int stride, int x0, int y0)
    {
        int[] xs = { 7, S - 8, 7, S - 8 }, ys = { 7, 7, S - 8, S - 8 };
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
        int W = 8 * S, H = 8 * S;
        long best = long.MaxValue;
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                long sum = 0;
                for (int j = 0; j < S; j++)
                {
                    int by = y0 + j + dy; if (by < 0) by = 0; else if (by >= H) by = H - 1;
                    int row = by * stride;
                    int prow = j * S * 4;
                    for (int i = 0; i < S; i++)
                    {
                        int bx = x0 + i + dx; if (bx < 0) bx = 0; else if (bx >= W) bx = W - 1;
                        int o = row + bx * 4, po = prow + i * 4;
                        int pa = piece[po + 3];
                        int eb = (piece[po] * pa + bb * (255 - pa)) / 255;
                        int eg = (piece[po + 1] * pa + bg * (255 - pa)) / 255;
                        int er = (piece[po + 2] * pa + br * (255 - pa)) / 255;
                        sum += Math.Abs(bd[o] - eb) + Math.Abs(bd[o + 1] - eg) + Math.Abs(bd[o + 2] - er);
                    }
                }
                if (sum < best) best = sum;
            }
        }
        return best;
    }
}
