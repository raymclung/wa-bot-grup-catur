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

## 8. Cheat Sheet

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

Admin:
```
!wake
!status
!announcement <teks>
!sebar info
!batal
```
