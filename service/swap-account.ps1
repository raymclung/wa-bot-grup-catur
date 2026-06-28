# ============================================================
#  Ganti akun WhatsApp bot (kalau akun lama hangus / diblokir).
#  Alur:
#    1. Backup state saat ini (sesi + config + data).
#    2. Arsipkan sesi WA lama (TIDAK dihapus -> bisa dikembalikan).
#    3. Restart gateway dengan sesi KOSONG -> QR baru dibuat.
#    4. Scan QR dengan NOMOR BARU (WhatsApp -> Perangkat Tertaut).
#
#  Jalankan:  powershell -ExecutionPolicy Bypass -File service\swap-account.ps1
#  Lewati konfirmasi:  ... -File service\swap-account.ps1 -Yes
#  TIDAK butuh admin.
# ============================================================

param([switch]$Yes)

$ErrorActionPreference = 'Stop'
$root  = Split-Path -Parent $PSScriptRoot
$auth  = Join-Path $root 'gateway\auth'
$qrPng = Join-Path $root 'gateway\qr.png'
$qrHtml= Join-Path $root 'gateway\qr.html'
$token = 'wabot-redeploy-2f9k7x'          # = adminApiToken (endpoint lokal)
$gw    = 'http://127.0.0.1:3211'

Write-Host "=== SWAP AKUN WHATSAPP BOT ==="
Write-Host "Akan: backup -> arsipkan sesi lama -> tampilkan QR baru untuk scan NOMOR BARU."
Write-Host "Sesi lama TIDAK dihapus (diarsipkan), jadi bisa dikembalikan."
if (-not $Yes) {
    $c = Read-Host "Lanjut? ketik YA"
    if ($c -ne 'YA') { Write-Host "Dibatalkan."; exit 0 }
}

# 1) Backup
Write-Host "[1/4] Backup state..."
& (Join-Path $PSScriptRoot 'backup.ps1')

# 2) Hentikan gateway lalu arsipkan sesi lama (saat gateway down -> aman)
Write-Host "[2/4] Menghentikan gateway & mengarsipkan sesi lama..."
try { Invoke-RestMethod -Method Post -Uri "$gw/admin/restart?token=$token" -TimeoutSec 8 | Out-Null } catch {}
Start-Sleep -Seconds 3
if (Test-Path $auth) {
    $archive = Join-Path $root ("gateway\auth-archive-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Move-Item -Path $auth -Destination $archive
    Write-Host ("      Sesi lama -> " + $archive)
}
New-Item -ItemType Directory -Force -Path $auth | Out-Null
Remove-Item -Path $qrPng,$qrHtml -ErrorAction SilentlyContinue   # buang QR lama

# 3) Pastikan gateway reload sesi KOSONG (restart sekali lagi) lalu tunggu QR
Write-Host "[3/4] Restart gateway dengan sesi kosong & menunggu QR..."
try { Invoke-RestMethod -Method Post -Uri "$gw/admin/restart?token=$token" -TimeoutSec 8 | Out-Null } catch {}
$ok = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    if (Test-Path $qrPng) { $ok = $true; break }
}

# 4) Instruksi
Write-Host "[4/4] Siap scan."
if ($ok) {
    Write-Host ("      QR  : " + $qrPng + "   (atau buka " + $qrHtml + " di browser)")
} else {
    Write-Host "      QR belum muncul. Cek gateway jalan (wrapper run-gateway) lalu lihat gateway\qr.png." -ForegroundColor Yellow
}
Write-Host ""
Write-Host "SCAN: di HP NOMOR BARU -> WhatsApp -> Perangkat Tertaut -> Tautkan Perangkat -> scan QR di atas."
Write-Host ""
Write-Host "SETELAH TERSAMBUNG:"
Write-Host "  - Tambahkan NOMOR BARU bot ke grup yang dimoderasi, lalu jadikan ADMIN."
Write-Host "  - JID grup di config TIDAK perlu diubah (grupnya sama)."
Write-Host "  - Notifikasi monitor tetap ke nomor admin yang sama (alertJids)."
Write-Host ""
Write-Host "KEMBALIKAN sesi lama (kalau ganti gagal):"
Write-Host "  1. Hentikan gateway."
Write-Host "  2. Hapus isi gateway\auth, lalu salin isi folder gateway\auth-archive-<timestamp> ke gateway\auth."
Write-Host "  3. Restart gateway (tanpa scan QR)."
