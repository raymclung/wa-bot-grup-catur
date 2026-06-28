# Operasi Bot Tanpa Admin — Cheat Sheet

Semua perintah di bawah jalan dari terminal mana pun di server **tanpa hak admin**.
Token restart = `<ADMIN_TOKEN>` (hanya berlaku dari localhost).

## 1) Cek kesehatan
```bash
curl -s http://127.0.0.1:5050/health        # brain  -> {"ok":true,...}
curl -s http://127.0.0.1:3211/health        # gateway -> {"ok":true,"connected":true}
curl -s http://localhost:11434/api/version  # ollama  -> {"version":"..."}
```

## 2) Hot-reload config (TANPA restart)
Pakai setelah mengubah `brain/config/config.json` atau `brain/config/rules.json`.
```bash
curl -s -X POST http://127.0.0.1:5050/reload
```
Berlaku untuk: aturan moderasi, daftar grup, exempt, puzzle, announcer, jam tenang, laporan, FAQ, dll.

## 3) Deploy / restart (TANPA admin) — wrapper loop otomatis rebuild
```bash
# restart brain saja
curl -s -X POST "http://127.0.0.1:5050/admin/restart?token=<ADMIN_TOKEN>&target=brain"

# restart gateway saja
curl -s -X POST "http://127.0.0.1:5050/admin/restart?token=<ADMIN_TOKEN>&target=gateway"

# restart keduanya
curl -s -X POST "http://127.0.0.1:5050/admin/restart?token=<ADMIN_TOKEN>&target=both"
```
Lalu cek lagi `/health` sampai `{"ok":true}` (biasanya ~6 detik untuk brain).

## Kapan pakai yang mana
- Ubah **config/rules** → **#2 reload** (paling cepat, tanpa putus).
- Ubah **kode C# brain** → **#3 restart brain** (wrapper build ulang).
- Ubah **kode gateway** → **#3 restart gateway**.
- Bot aneh / mau segar → **#3 both**.

## Catatan
- Ollama sudah jadi service SYSTEM (tahan logoff/reboot) — tak perlu disentuh.
- Yang BUTUH admin (jarang): pasang/ubah service Windows & autostart (sudah beres),
  serta kill proses lintas-sesi. Operasi harian di atas TIDAK butuh admin.
