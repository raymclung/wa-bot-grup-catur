# ============================================================
#  Launcher anti-duplikat untuk resilience script (alarm + publisher QR).
#  Dipanggil saat login dev8 (via wa-autostart.vbs di Startup) DAN bisa manual.
#  Untuk TIAP script: start hanya kalau BELUM ada prosesnya (cegah duplikat).
#  Catatan: dijalankan sebagai dev8 (interaktif) -> punya izin tulis IIS web root
#  (wa-qr-publish) dan akses desktop (wa-monitor: Chrome/msg saat logout).
#  Jalan manual: powershell -ExecutionPolicy Bypass -File service\wa-autostart.ps1
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'
$svc = 'C:\Users\dev8\Documents\wa-bot\service'

function Ensure-Running($script) {
  # Cocokkan pada nama file .ps1 di command-line proses yang berjalan.
  # Proses launcher ini command-line-nya 'wa-autostart.ps1' -> TIDAK self-match nama target.
  $n = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*$script*" }).Count
  if ($n -eq 0) {
    Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
      '-NoProfile','-ExecutionPolicy','Bypass','-WindowStyle','Hidden','-File',"`"$svc\$script`""
    )
    Write-Host "[wa-autostart] start $script"
  } else {
    Write-Host "[wa-autostart] $script sudah jalan ($n) - lewati"
  }
}

Ensure-Running 'wa-qr-publish.ps1'
Ensure-Running 'wa-monitor.ps1'
