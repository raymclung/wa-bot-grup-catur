# Panduan Admin - Bot WhatsApp Liga Catur Indonesia

Panduan singkat memakai Judit Polica di grup WhatsApp.

## 1. Yang Bot Lakukan Otomatis

| Fitur | Penjelasan |
|---|---|
| Hapus pesan terlarang | Judi, spam, undangan grup, dan link platform lawan sesuai aturan grup. |
| Anti-spam beruntun | Pesan terlalu rapat dirapikan. |
| Puzzle harian | Jam 09:00 WIB sulit, jam 13:00 WIB lebih santai. Solusi setelah 3 jam. |
| Anti-report | Jika ada masalah, anggota diarahkan ke `!admin`, bukan report nomor bot. |

Catatan: bot tidak kick anggota. Bot harus menjadi admin grup agar bisa menghapus pesan.

## 2. Perintah Untuk Anggota

| Perintah | Fungsi | Contoh |
|---|---|---|
| `!help` | Menu singkat | `!help` |
| `!rules` | Aturan grup | `!rules` |
| `!next` | Jadwal turnamen | `!next` |
| `!hasil <id>` | Hasil turnamen | `!hasil 9175` |
| `!standings` | Pilih klasemen terbaru | `!standings` |
| `!standings <id>` | Klasemen langsung | `!standings 9175` |
| `!events` | Event chess.college | `!events` |
| `!rating <user>` | Rating Lichess | `!rating DrNykterstein` |
| `!puzzle` | Puzzle on-demand | `!puzzle` |
| `!solusi` | Buka solusi saat waktunya | `!solusi` |
| `!lapor` | Reply pesan, laporkan ke admin | `!lapor spam` |
| `!admin <catatan>` | Panggil admin tanpa reply | `!admin bot salah hapus` |
| `!sleep` | Bot istirahat total | `!sleep` |
| `!tanya <soal>` | Tanya AI catur | `!tanya tips endgame` |

`!wake` hanya untuk admin.

## 3. AI

AI hanya menjawab jika:
- memakai `!tanya <pertanyaan>`
- bot di-tag

Chat pribadi admin boleh dipakai. Nomor lain tetap diabaikan.

## 4. Chat Pribadi Admin

Bot bisa membalas DM secara reaktif.

- gateway: `allowPrivateChat=true`
- brain: `privateChat.enabled=true`
- opsional: isi `privateChat.allowedNumbers`
- kosong berarti pakai admin allowlist

Bot tetap tidak memulai DM duluan.

## 5. Klasemen Dan Hasil

Interaktif:
1. Ketik `!standings`.
2. Bot tampilkan daftar.
3. Balas nomor.

Langsung:
- `!standings 9175`
- `!hasil 9175`

ID turnamen ada di link `TournamentID=`.

## 6. Sebar Pengumuman Admin

Dilakukan di grup Admin.

### Wizard Pilih Grup
1. Ketik `!sebar info`.
2. Tulis isi pesan.
3. Pilih nomor grup, atau ketik `semua`.
4. Bot menyebarkan pesan.

Batalkan dengan `!batal`.

### Teks Langsung
- `!announcement <teks>`
- Alias: `!umumkan <teks>`

Bot langsung kirim ke grup tujuan default.

### Poster / Gambar
Kirim gambar di grup Admin dengan caption diawali `!sebar`.

Contoh:
`!sebar Turnamen malam ini. Detail di poster.`

Catatan anti-ban:
- jangan spam pengumuman berulang
- mention panjang dipecah otomatis per 5 tag
- beri jeda antar pengumuman

## 7. Jika Bot Bermasalah

- Ketik `!admin <kendala>` di grup.
- Jangan report nomor bot.
- Jika bot salah hapus, reply pesan dan ketik `!lapor`.
- Jika bot diam total, hubungi tim teknis.

## 8. Pairing & Turnamen (Liga Catur)

Semua perintah ini **tag bot** (`@bot ...`) dan **khusus admin**. Pemain diambil dari **tag** (`@nama`).

> **Penting:** pemain yang di-tag harus (1) **pernah kirim minimal 1 pesan** di grup — biar bot tahu nomornya, DAN (2) **terdaftar + verifikasi** di ligacatur.com. Yang belum otomatis dilewati + dilaporkan bot.

### Pairing satuan
| Perintah | Fungsi |
|---|---|
| `@bot pair @A vs @B` | Buat 1 game (default unrated, G5+0) |
| `@bot pair @A vs @B rated G5+1` | Atur rated + waktu (G<menit>+<increment>) |
| `@bot pair @A vs @B G3+2 gas` | Tambah `gas`/`mulai` = jam langsung jalan |
| `@bot pair @A @B @C @D` | Banyak board sekaligus (A-B, C-D) |
| `@bot start [BulkID]` | Mulai jam (board terakhir kalau ID kosong) |
| `@bot cancel [BulkID]` | Batalkan board |
| `@bot rematch` | Ulang 2 pemain terakhir, warna ditukar |
| `@bot hasil [BulkID]` | Cek skor game |
| `@bot boards` | Daftar board aktif di grup |
| `@bot info @A` | Info pemain (handle Lichess, status verifikasi) |
| `@bot bantuan` | Daftar semua perintah pairing |

### Klasemen
| Perintah | Fungsi |
|---|---|
| `@bot klasemen` | Tabel Menang/Seri/Kalah + poin (+ Buchholz saat turnamen) |
| `@bot statistik @A` | Rekap pribadi 1 pemain |
| `@bot reset klasemen` | Kosongkan klasemen (mulai musim baru) |

### Turnamen (Swiss mini, otomatis)
| Perintah | Fungsi |
|---|---|
| `@bot turnamen @A @B @C @D` | Mulai turnamen (default 1 babak) |
| `@bot turnamen @A @B @C @D 3 ronde` | Atur jumlah ronde |
| `@bot turnamen status` | Ronde ke berapa, peserta, klasemen |
| `@bot turnamen tambah @X` | Tambah peserta (ikut ronde berikutnya) |
| `@bot turnamen keluar @X` | Keluarkan peserta |
| `@bot ronde` | Pair ronde berikutnya manual |
| `@bot turnamen selesai` | Umumkan juara & tutup |
| `@bot turnamen batal` | Hentikan tanpa umumkan juara |
| `@bot turnamen riwayat` | Daftar turnamen selesai + juaranya |

**Yang otomatis saat turnamen:**
- Hasil game diumumkan sendiri saat selesai
- Ronde berikutnya auto-jalan saat semua papan beres; juara diumumkan di ronde terakhir
- Bye = +1 poin (kalau peserta ganjil)
- Tiebreak Buchholz (poin sama -> urut by kekuatan lawan)
- Reminder kalau game turnamen belum dimulai ~2 menit
- Aman restart: turnamen tetap lanjut walau bot sempat mati

## 9. Cheat Sheet

Anggota:
```
!help
!next
!rules
!puzzle
!standings
!admin <catatan>
!sleep
```

Admin (umum):
```
!wake
!status
!announcement <teks>
!sebar info
!batal
```

Admin (pairing & turnamen, tag bot):
```
@bot pair @A vs @B G5+1 gas
@bot pair @A @B @C @D
@bot boards
@bot hasil
@bot rematch
@bot klasemen
@bot statistik @A
@bot turnamen @A @B @C @D 3 ronde
@bot turnamen status
@bot ronde
@bot turnamen selesai
@bot turnamen riwayat
@bot bantuan
```
