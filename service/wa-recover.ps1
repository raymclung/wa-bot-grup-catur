# ============================================================
#  Pulihkan koneksi WhatsApp bot (re-link):
#    1. Arsipkan sesi mati (kalau ada creds) -> gateway masuk mode QR
#    2. Restart gateway -> QR fresh
#    3. Tutup jendela QR Chrome LAMA (profil khusus) -> pastikan cuma SATU
#    4. Buka SATU jendela Chrome khusus QR (tak ganggu Chrome utama)
#  Dipakai manual ATAU otomatis oleh wa-monitor.ps1 saat logout.
#  Jalan manual: powershell -ExecutionPolicy Bypass -File service\wa-recover.ps1
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'
$gw    = "C:\Users\dev8\Documents\wa-bot\gateway"
$qrUrl = "file:///C:/Users/dev8/Documents/wa-bot/gateway/qr.html"
$udd   = "$env:LOCALAPPDATA\wa-qr-chrome"   # profil Chrome TERPISAH khusus QR

# 1) Arsipkan sesi mati kalau masih ada creds (logout) -> biar gateway generate QR
if (Test-Path "$gw\auth\creds.json") {
  $ts = Get-Date -Format 'yyyyMMdd-HHmmss'
  Move-Item "$gw\auth" "$gw\auth-archive-$ts"
  New-Item -ItemType Directory -Force "$gw\auth" | Out-Null
  New-Item -ItemType File -Force "$gw\auth\.gitkeep" | Out-Null
  Write-Host "[wa-recover] sesi mati diarsipkan -> auth-archive-$ts"
} else {
  Write-Host "[wa-recover] auth sudah kosong (mode QR) -> cukup refresh QR"
}

# 2) Restart gateway -> QR fresh
$token = (Get-Content "$gw\config.json" -Raw | ConvertFrom-Json).adminApiToken
try { Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:3211/admin/restart?token=$token&target=gateway" -TimeoutSec 10 | Out-Null } catch {}
$ok = $false
for ($i = 0; $i -lt 15; $i++) {
  Start-Sleep 2
  if ((Test-Path "$gw\qr.png") -and (((Get-Date) - (Get-Item "$gw\qr.png").LastWriteTime).TotalSeconds -lt 12)) { $ok = $true; break }
}
Write-Host ("[wa-recover] QR fresh: " + $(if ($ok) { 'siap' } else { 'BELUM (cek manual)' }))

# 3) Tutup jendela QR Chrome lama (hanya yang pakai profil wa-qr-chrome) -> tak menumpuk
Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'chrome.exe' -and $_.CommandLine -like '*wa-qr-chrome*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep 1

# 4) Buka SATU jendela Chrome khusus QR (profil terpisah -> tak ganggu tab Chrome-mu yang lain)
Start-Process chrome.exe -ArgumentList "--user-data-dir=$udd", "--new-window", "--app=$qrUrl"
Write-Host "[wa-recover] Chrome QR (SATU jendela) dibuka. Scan: WhatsApp > Perangkat Tertaut > Tautkan Perangkat."
