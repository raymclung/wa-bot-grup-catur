# ============================================================
#  ALARM LOGOUT WhatsApp bot.
#  Pantau /health tiap 45 dtk. Begitu WA logout (sesi mati):
#    - Popup pesan ke sesi login (RDP) via `msg` + beep
#    - Tulis LOGOUT-ALERT.txt (stempel waktu)
#    - Jalankan wa-recover.ps1 OTOMATIS -> QR re-link langsung siap di SATU Chrome
#    - Jaga QR tetap fresh selama belum di-scan
#  Jalan manual: powershell -ExecutionPolicy Bypass -File service\wa-monitor.ps1
#  (Idealnya dijalankan otomatis via Task Scheduler - lihat install-monitor.)
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'
$gw      = "C:\Users\dev8\Documents\wa-bot\gateway"
$recover = "C:\Users\dev8\Documents\wa-bot\service\wa-recover.ps1"
$alert   = "$gw\LOGOUT-ALERT.txt"
$state   = 'unknown'

Write-Host "[wa-monitor] mulai memantau /health tiap 45 detik..."
while ($true) {
  try { $h = Invoke-RestMethod -Uri "http://127.0.0.1:3211/health" -TimeoutSec 5 } catch { $h = $null }
  $ready  = [bool]($h -and $h.ready)
  $logout = [bool]($h -and $h.loggedOut)
  $ts = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')

  if ($ready) {
    if ($state -ne 'ok') { Write-Host "[$ts] WA READY (bot normal)"; Remove-Item $alert -Force -EA SilentlyContinue }
    $state = 'ok'
  }
  else {
    $qrAge = if (Test-Path "$gw\qr.png") { ((Get-Date) - (Get-Item "$gw\qr.png").LastWriteTime).TotalSeconds } else { 9999 }

    # Logout BARU (belum masuk mode linking) -> alarm keras
    if ($logout -and $state -ne 'linking') {
      "[$ts] BOT WHATSAPP LOGOUT (sesi mati). Menyiapkan QR re-link otomatis di Chrome..." | Out-File $alert
      try { msg * "BOT WHATSAPP LOGOUT ($ts). QR re-link sudah disiapkan di Chrome - silakan SCAN. (WhatsApp > Perangkat Tertaut > Tautkan Perangkat)" } catch {}
      try { [console]::beep(880,500); [console]::beep(660,500) } catch {}
      Write-Host "[$ts] !!! LOGOUT terdeteksi -> siapkan QR"
    }

    # Perlu siapkan/refresh QR: saat logout baru, ATAU sedang linking tapi QR sudah basi
    if ($logout -or ($state -eq 'linking' -and $qrAge -gt 150)) {
      & $recover 2>&1 | Out-Null
      "[$ts] QR re-link SIAP di Chrome. Scan: WhatsApp > Perangkat Tertaut > Tautkan Perangkat." | Add-Content $alert
      $state = 'linking'
    }
  }
  Start-Sleep -Seconds 45
}
