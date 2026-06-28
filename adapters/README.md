# Adapter Channel — Kontrak (Bot Agnostic)

Brain (C#) **tidak tergantung WhatsApp**. WhatsApp hanyalah salah satu *adapter*.
Untuk menambah channel baru (email, Telegram, dll), tulis sebuah adapter kecil yang
memenuhi **2 arah** kontrak di bawah. Tidak perlu mengubah brain.

```
                 (masuk)                          (keluar)
  Channel  ──►  POST brain /incoming      brain ──►  POST {adapterBase}/send
  (WA/email/...)                                      POST {adapterBase}/send-image
                                                      POST {adapterBase}/delete   (kalau didukung)
```

## 1) MASUK — adapter → brain
`POST http://127.0.0.1:5050/incoming` (Content-Type: application/json)

```json
{
  "channel": "email",                  // WAJIB: nama channel (cocokkan dgn config.channels)
  "jid": "budi@example.com",           // ID percakapan (netral; email=alamat, telegram=chatId)
  "participant": "budi@example.com",   // ID pengirim
  "pushName": "Budi",                  // nama tampil (opsional)
  "text": "!next",                     // isi pesan
  "key": { "id": "<msgId>" },          // referensi pesan (untuk /delete; boleh apa adanya)
  "mentionedBot": false                // true bila bot di-tag (pemicu AI)
}
```
Brain membalas JSON `{ ok, action }` (mis. `command`, `ai`, `moderated`, `unmanaged`).
Catatan: **gating grup hanya untuk `channel="whatsapp"`**. Channel lain dianggap percakapan
langsung → selalu dilayani (tak perlu daftar di `groups`).

## 2) KELUAR — brain → adapter
Brain mengirim balasan ke **base URL adapter channel itu** (dari `config.channels`):

- `POST {adapterBase}/send` → `{ jid, text, mentions? }` — kirim teks.
- `POST {adapterBase}/send-image` → `{ jid, path, caption? }` — kirim gambar (kalau channel mendukung).
- `POST {adapterBase}/delete` → `{ jid, key }` — hapus pesan (hanya dipanggil bila channel `CanDelete`).
- `GET  {adapterBase}/health` → `{ ok: true }` — untuk pemantauan.

Adapter cukup implement yang relevan. Yang tak didukung boleh balas 200 no-op
(mis. email: `/delete` no-op). Brain SUDAH tahu kemampuan tiap channel lewat `ChannelCaps`
(`Caps.Of(channel)`), jadi mis. **email tidak akan pernah dipanggil `/delete`**.

## 3) Daftarkan di brain config
`brain/config/config.json`:
```json
"channels": {
  "email": "http://127.0.0.1:3300"
}
```
Lalu `POST /reload` (atau /admin/restart). Balasan untuk pesan `channel:"email"`
otomatis dirutekan ke `http://127.0.0.1:3300`.

## Kemampuan channel (ChannelCaps) — sudah ada di brain
| channel  | CanDelete | SupportsImage | SupportsMention |
|----------|:---------:|:-------------:|:---------------:|
| whatsapp |    ya     |      ya       |       ya        |
| telegram |    ya     |      ya       |       ya        |
| email    |  tidak    |  ya (lampiran)|     tidak       |

Tambah/ubah di `Caps` (Program.cs) bila perlu.

## Contoh adapter email (garis besar)
1. **IMAP poll** inbox tiap N detik → tiap email baru → `POST /incoming`
   (`channel:"email"`, `jid`=alamat pengirim, `text`=isi email).
2. **HTTP server** kecil: `/send` → kirim email balasan via **SMTP** (nodemailer);
   `/send-image` → email + lampiran; `/delete` → no-op; `/health`.
3. Jalankan sebagai proses sendiri (mirip `gateway/`), beri base URL → daftarkan di `config.channels`.

> Pola WhatsApp ada di `gateway/src/index.js` sebagai referensi lengkap.

## Bukti
Sudah diuji dengan adapter "console" sederhana: pesan `channel:"console"` (bukan grup WA)
diproses brain, dan balasannya dirutekan ke adapter console — **bukan** ke WhatsApp.
Membuktikan brain channel-agnostic.
