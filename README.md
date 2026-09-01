<div align="center">

# 🤖 WA Bot — Moderasi Grup Catur

**Bot moderasi grup WhatsApp berbasis aturan, dengan arsitektur hybrid Node.js + C#.**

[![C#](https://img.shields.io/badge/C%23-.NET%2010-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Node.js](https://img.shields.io/badge/Node.js-Baileys-339933?style=flat-square&logo=nodedotjs&logoColor=white)](https://nodejs.org/)
[![WhatsApp](https://img.shields.io/badge/WhatsApp-Bot-25D366?style=flat-square&logo=whatsapp&logoColor=white)](#)
[![Rule Based](https://img.shields.io/badge/Moderasi-rule--based-FF7A2D?style=flat-square)](#menambah-aturan-baru)
[![No AI](https://img.shields.io/badge/Runtime-tanpa%20AI-6B7280?style=flat-square)](#)

</div>

---

## Ringkasan

Bot moderasi grup WhatsApp **rule-based**, dipakai untuk grup komunitas catur
(Chess Stream / CCL). Arsitekturnya **hybrid**, memisahkan koneksi dari logika:

| Komponen | Teknologi | Perannya |
|---|---|---|
| **`gateway/`** | Node.js + Baileys | Lapisan tipis. Hanya menjaga koneksi WhatsApp dan menjalankan perintah — tidak ada logika moderasi di sini. |
| **`brain/`** | C# (.NET 10) | "Otak" moderasi: membaca aturan dari JSON, memutuskan pelanggaran, lalu memerintahkan gateway menghapus pesan dan mengirim peringatan. |

Pemisahan ini membuat logika moderasi dapat diuji tanpa koneksi WhatsApp,
dan gateway dapat diganti tanpa menyentuh aturan.

> [!NOTE]
> Saat berjalan, bot **tidak memakai token maupun AI apa pun** — murni pencocokan
> pola dengan regex. Ini disengaja: hasilnya dapat diprediksi, murah, dan tidak
> bergantung pada layanan luar.

- **Aturan saat ini:** hapus pesan berisi link/promosi **judi & spam** (`brain/config/rules.json`, bisa ditambah).
- **Aksi:** hapus pesan + kirim peringatan. **Tanpa kick otomatis.**
- **Syarat:** bot harus jadi **admin grup** agar bisa menghapus pesan anggota.

## Alur

```
WhatsApp ⇄ gateway (Node/Baileys) ──POST /incoming──▶ brain (C#)
                  ▲                                        │
                  └──── POST /delete , POST /send ◀────────┘
```

## Cara menjalankan (paling mudah)

Klik dua kali **`start-all.bat`** di folder ini — otomatis membuka dua jendela (brain + gateway). **QR muncul di jendela "WA Gateway"**.

## Cara menjalankan (manual, DUA terminal)

**Terminal 1 — brain (C#):**
```bash
cd <folder-repo>\brain
dotnet run
```
Brain mendengarkan di `http://127.0.0.1:5050`.

**Terminal 2 — gateway (Node):**
```bash
cd <folder-repo>\gateway
npm install
npm start
```
Muncul **QR code** di terminal. Scan dengan WhatsApp **nomor bot** (Setelan â†’ Perangkat Tertaut â†’ Tautkan Perangkat). Sesi tersimpan di `gateway/auth/`, jadi tidak perlu scan ulang tiap start.

> Jalankan **brain dulu**, lalu gateway — supaya pesan pertama yang masuk langsung terkirim ke brain.

## Struktur

```
wa-bot/
├── start-all.bat            jalankan brain + gateway sekaligus
├── ganti-nomor.bat          hapus sesi login & scan QR nomor baru
├── service/                 auto-start saat server reboot
│   ├── run-brain.cmd        wrapper loop-restart brain
│   ├── run-gateway.cmd      wrapper loop-restart gateway
│   ├── install-autostart.ps1
│   └── uninstall-autostart.ps1
├── gateway/                 Node + Baileys (koneksi WA)
│   ├── package.json
│   ├── config.json          port, URL webhook brain
│   ├── auth/                sesi login WA (otomatis, JANGAN di-commit)
│   └── src/index.js         konek + QR + teruskan ke brain + /send /delete
└── brain/                   C# .NET 10 (otak moderasi)
    ├── WaBot.csproj
    ├── Program.cs           webhook /incoming + logika hapus/warning
    ├── config/
    │   ├── config.json      URL gateway + teks peringatan + exemptNumbers
    │   └── rules.json       daftar aturan moderasi (extensible)
    ├── logs/audit.log       catatan setiap pesan yang dihapus (otomatis)
    └── data/warnings.json   hitungan peringatan per anggota (otomatis)
```

## Pengecualian (exempt), log, & ganti nomor

**Nomor yang tidak pernah dimoderasi** (mis. admin grup): tambahkan ke `exemptNumbers` di `brain/config/config.json`:
```json
"exemptNumbers": ["6281234567890", "6289876543210"]
```
Simbol/spasi diabaikan otomatis. Setelah edit, restart brain atau `POST /reload`.

**Log audit** — setiap pesan yang dihapus dicatat ke `brain/logs/audit.log` (waktu, grup, pengirim, aturan, cuplikan teks).

**Riwayat peringatan** disimpan di `brain/data/warnings.json` — tidak hilang saat bot restart.

**Ganti nomor bot:** klik dua kali **`ganti-nomor.bat`** (tutup dulu gateway lama). Aturan & config tetap utuh; hanya sesi login yang dibuang.

## Pesan sambutan (welcome)

Saat ada member baru join, bot otomatis kirim sambutan + aturan. Atur di `brain/config/config.json`:
```json
"welcomeEnabled": true,
"welcomeMessage": "ðŸ‘‹ Selamat datang @user di {group}!\n\nMohon baca aturan grup:\n{rules}\n\n...",
"rulesText": "1. Dilarang judi/spam.\n2. Hormati anggota.\n3. Sesuai topik."
```
Placeholder: `@user` (mention member), `{group}` (nama grup), `{rules}` (isi `rulesText`). Set `welcomeEnabled: false` untuk mematikan. Bot **tidak perlu admin** untuk sekadar mengirim sambutan (hanya butuh admin untuk menghapus pesan).

## Anti-flood

Jika seorang anggota mengirim pesan beruntun terlalu cepat, pesan berlebih otomatis dihapus + diberi peringatan (sekali per cooldown, agar tidak spam). Atur di `brain/config/config.json`:
```json
"floodEnabled": true,
"floodMaxMessages": 6,
"floodWindowSeconds": 8,
"floodWarnCooldownSeconds": 30,
"floodWarningMessage": "ðŸš¦ @user, jangan kirim pesan beruntun terlalu cepat. Peringatan ke-{count}."
```
Artinya: > **6 pesan dalam 8 detik** dari orang yang sama â†’ pesan ke-7 dst. dihapus; peringatan dikirim maksimal sekali tiap 30 detik. Nomor di `exemptNumbers` dikecualikan dari anti-flood juga. Set `floodEnabled: false` untuk mematikan.

> **Aturan bawaan saat ini** (`rules.json`): `judi-keywords`, `judi-url`, `grup-invite` (link `chat.whatsapp.com`). `spam-shortener` tersedia tapi non-aktif.

## Menambah aturan baru

Edit `brain/config/rules.json`, tambahkan objek di array `rules`:

```json
{
  "id": "nama-unik",
  "name": "Deskripsi singkat",
  "reason": "alasan yang muncul di peringatan",
  "enabled": true,
  "flags": "i",
  "pattern": "polaRegexDiSini"
}
```

> Di JSON, setiap `\` pada regex harus ditulis `\\` (mis. `\\s`, `\\/`).
> Set `"enabled": false` untuk mematikan aturan tanpa menghapusnya.
> Muat ulang tanpa restart: `POST http://127.0.0.1:5050/reload`.

## Pengaturan per-grup

Secara default semua grup memakai setelan global. Anda bisa **menimpa per-grup** dan membatasi grup mana yang diurus, di `brain/config/config.json`:

- `manageAllGroups: true` (default) — bot mengurus **semua** grup tempat ia berada.
- `manageAllGroups: false` — bot **hanya** mengurus grup yang terdaftar di `groups`; **diam total** di grup lain.

```json
"manageAllGroups": false,
"groups": {
  "12036xxxxxxxxx@g.us": { "label": "Utama — moderasi penuh" },
  "12036yyyyyyyyy@g.us": {
    "label": "Pengumuman saja",
    "moderationEnabled": false,
    "floodEnabled": false,
    "welcomeEnabled": false,
    "commandsEnabled": false
  },
  "12036zzzzzzzzz@g.us": {
    "label": "Santai",
    "disabledRules": ["grup-invite"],
    "welcomeMessage": "Halo @user ðŸ‘‹ selamat datang!",
    "exemptNumbers": ["628111111111"]
  }
}
```

Field yang **tidak ditulis** ikut setelan global. Override yang tersedia per grup:
`moderationEnabled`, `floodEnabled`, `welcomeEnabled`, `commandsEnabled`, `welcomeMessage`, `rulesText`, `disabledRules` (id aturan yang dimatikan khusus grup ini), `exemptNumbers` (admin khusus grup — ditambahkan ke exempt global).

> **Cara mendapat JID grup:** setiap pesan yang dimoderasi mencatat `grup=<jid>` di `brain/logs/audit.log` (berakhiran `@g.us`). Setelah tahu JID-nya, masukkan ke `groups`.

## Perintah chat (commands)

Anggota/admin bisa ketik perintah di grup (diawali `!`). Diatur di `brain/config/config.json`.

**Perintah umum:**
- `!help` - menu singkat
- `!rules` - aturan grup
- `!info` - status bot singkat
- `!next` - jadwal turnamen
- `!puzzle` - puzzle on-demand
- `!solusi` - solusi puzzle saat waktunya
- `!admin <catatan>` - panggil admin tanpa reply
- `!lapor` - reply pesan bermasalah, teruskan ke admin
- `!sleep` - bot diam total; `!wake` hanya admin

**Puzzle harian:**
- 09:00 WIB: `Pagi Sulit`, rating `2900+`
- 13:00 WIB: `Siang Santai`, rating `1200-1800`
- solusi otomatis setelah 180 menit
- pool puzzle dibuat oleh `brain/data/build_puzzle_pool.py`

**Sebar admin:**
- `!sebar info` - wizard pilih grup
- `!announcement <teks>` - kirim langsung ke target default
- `!umumkan <teks>` - alias `!announcement`
- caption gambar `!sebar ...` - sebar poster
- mention panjang dipecah otomatis per 5 tag oleh gateway

**Perintah data turnamen:**
- `!standings` - pilih turnamen terbaru
- `!standings <id>` - klasemen langsung
- `!pairing <id>` / `!pairings <id>` - pertandingan
- `!jadwal <id>` - jadwal internal

Contoh: `!standings 8990`. Jika `dbConnectionString` kosong, perintah data membalas bahwa data internal belum aktif.

### Mengaktifkan perintah data (DB)
1. Isi `dbConnectionString` di `config.json`, mis.:
   `"Server=NAMA\\INSTANCE;Database=ssch;User Id=...;Password=...;TrustServerCertificate=True"`
   (atau Windows auth: `Server=...;Database=ssch;Integrated Security=True;TrustServerCertificate=True`)
2. Pastikan `dataCommands` menunjuk stored procedure & parameter yang benar.
3. Restart brain atau `POST /reload`.
## Broadcast dari sistem Chess Stream

Sistem turnamen (PairingBot / website) bisa **mengirim pengumuman ke grup WA** (mis. "turnamen mau mulai") dengan memanggil endpoint `/broadcast` di brain — pola yang sama seperti PairingBot memanggil `TournamentDataUpdate.ashx`. **Tidak butuh DB.**

**Aktifkan:** isi `broadcastToken` di `brain/config/config.json` dan (opsional) peta `tournamentGroups`:
```json
"broadcastToken": "rahasia-panjang-acak",
"tournamentGroups": { "8990": "1203630xxxxxxxxx@g.us" }
```

**Cara memanggil** (dari PairingBot/website, fire-and-forget HTTP):
```
POST http://<host-bot>:5050/broadcast
Content-Type: application/json
{ "token": "rahasia-panjang-acak", "tournamentId": 8990, "text": "â™Ÿï¸ Turnamen dimulai 10 menit lagi!" }
```
Atau kirim ke grup tertentu langsung: `{ "token": "...", "jid": "...@g.us", "text": "..." }`.

Respon: `401` token salah, `400` tujuan/teks kurang, `403` jika `broadcastToken` belum diset (endpoint nonaktif).

> **Keamanan/jaringan:** brain saat ini hanya listen di `127.0.0.1` â†’ hanya bisa dipanggil dari **server yang sama**. Jika PairingBot ada di server lain, brain perlu listen di alamat yang reachable + dibatasi firewall (hanya IP Chess Stream) + token. Untuk cara kerja, mencari titik panggil di PairingBot (mis. saat log "Round X Pairings are made" / "Starting clocks"), lihat aturan `pairingbot.md` & `tournament-data-update.md`.

## Auto-start saat server reboot

Agar bot otomatis jalan setiap server booting (tanpa perlu login), pasang task Scheduler. Bot dijalankan sebagai SYSTEM, dengan wrapper yang **otomatis menghidupkan ulang** kalau prosesnya mati.

**Syarat:** scan QR **sekali** secara interaktif dulu (`npm start` â†’ scan) supaya sesi `gateway/auth/` sudah ada.

**Pasang** (PowerShell **sebagai Administrator**):
```powershell
cd <folder-repo>
powershell -ExecutionPolicy Bypass -File service\install-autostart.ps1
```

**Uji tanpa reboot:**
```powershell
Start-ScheduledTask -TaskName 'WA Bot Brain'
Start-ScheduledTask -TaskName 'WA Bot Gateway'
```

**Copot:**
```powershell
powershell -ExecutionPolicy Bypass -File service\uninstall-autostart.ps1
```

> Membuat 2 task: **WA Bot Brain** & **WA Bot Gateway** (trigger: At startup, akun: SYSTEM). Brain pakai config kanonik di `brain/config` (lewat `dotnet run -c Release`); gateway pakai `gateway/src`.

## Catatan risiko

Otomasi WhatsApp tidak resmi (melanggar ToS). Untuk menekan risiko ban:
- Pakai **nomor khusus** bot (anggap sekali pakai — kalau di-ban tinggal ganti & scan ulang).
- Jangan agresif / spam. Mulai dari **satu grup**.
- Config & aturan tetap aman di server walau nomor berganti.


### Chat Pribadi Admin
- Gateway: `allowPrivateChat=true`.
- Brain: `privateChat.enabled=true`.
- Batasi nomor: isi `privateChat.allowedNumbers`.
- Jika kosong, bot memakai admin allowlist.
- Bot hanya membalas DM masuk.

