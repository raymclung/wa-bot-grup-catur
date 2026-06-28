# QC Respons Bot — Pedoman Nada & Baseline

Tujuan: nada bot **konsisten, ramah, mengajak — bukan menyalahkan**. Dokumen ini baseline
"suara" bot. Setiap mengubah teks respons, jaga prinsip di bawah & jalankan `node tests/tone-lint.mjs`.

## Prinsip nada
1. **Bantu, bukan menghukum.** "saya bantu rapikan" ✅ — bukan "pesan kamu DIHAPUS karena melanggar" ❌.
2. **Tidak menyalahkan.** "Belum pas" / "belum tepat" ✅ — bukan "SALAH" ❌.
3. **Ucapkan terima kasih / apresiasi.** "terima kasih sudah ikut menjaga grup" ✅.
4. **Mengajak & menyemangati.** "ayo coba lagi", "kamu pasti bisa", "semangat" ✅.
5. **Jelaskan singkat alasannya**, tanpa nada keras/ancaman ("awas", "hukuman", "diperingatkan keras" ❌).
6. **Sapa dengan @nama** saat relevan, agar terasa personal & hangat.

## Baseline teks (per skenario)
> Sumber: `brain/config/config.json` (bisa diubah tanpa rebuild) & beberapa di `brain/Program.cs`.

| Skenario | Sumber | Teks baseline (ringkas) |
|---|---|---|
| **Moderasi (hapus konten)** | config `warningMessage` | "@user, saya bantu rapikan pesan tadi karena terdeteksi {reason}. Terima kasih sudah ikut menjaga grup…" |
| **Anti-flood** | config `floodWarningMessage` | "@user, saya bantu jeda sebentar ya. Beberapa pesan yang sangat berdekatan saya rapikan…" |
| **Probation (anggota baru)** | config `probation.message` | "@user, untuk anggota baru, link/gambar saya tahan sementara… akses terbuka otomatis." |
| **Media forward** | config `mediaModeration.message` | "@user, media yang sering diteruskan saya rapikan dulu… silakan kirim ulang tanpa forward ya." |
| **Sambutan** | config `welcomeMessage` | "Selamat datang @user di {group}! Senang kamu bergabung… selamat belajar, bertanding, bersilaturahmi." |
| **Puzzle benar** | code (`puzzle-correct`) | "✅ *Benar, @user!* 👏 Lawan membalas *…*. Sekarang giliranmu…" |
| **Puzzle belum tepat** | code (`puzzle-wrong`) | "Belum pas, @user." + penjelasan singkat (AI) kenapa kurang tepat (tanpa membocorkan solusi). |
| **Puzzle dorongan** | config `puzzle.tryHarderMessage` | "Semangat, coba satu langkah dulu ya… kalau buntu, solusi muncul otomatis." |
| **Lapor diterima** | code (`lapor`) | "Terima kasih, laporanmu sudah diteruskan ke admin. 🙏" |
| **Juara turnamen** | code | "👏 Selamat untuk para juara! Terima kasih semua yang sudah bertanding 🔥♟️" |

## Kata yang DIHINDARI (di teks balasan ke user)
`salah` (untuk jawaban → pakai "belum pas"), `dilarang`, `melanggar`, `tidak boleh`,
`kamu harus`, `awas`, `hukuman`, `diperingatkan`, `DIHAPUS` (huruf besar/menuduh).
> Catatan: kata seperti "hindari" di `rulesText` boleh (itu aturan, bukan teguran personal) —
> karena itu `tone-lint` TIDAK memeriksa `rulesText`.

## Cara cek
```bash
node tests/tone-lint.mjs
```
Lint membaca field respons di `config.json` & menandai bila ada kata menyalahkan/keras.
Lulus = nada masih sesuai baseline.
