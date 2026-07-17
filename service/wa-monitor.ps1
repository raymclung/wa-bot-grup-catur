# ============================================================
#  ALARM + NOTIFIKASI PEMULIHAN WhatsApp bot.
#  Pantau /health tiap 45 dtk.
#   - LOGOUT (sesi mati): popup msg + beep + LOGOUT-ALERT.txt + auto wa-recover (QR)
#   - PUTUS non-logout (403/throttle/blip): tunggu (gateway auto-reconnect sendiri)
#   - PULIH (down -> ready): kirim pesan "Bot online kembali" ke GRUP ADMIN
#     supaya admin tahu dari chat grup, bukan cuma cek manual.
#  Jalan manual: powershell -ExecutionPolicy Bypass -File service\wa-monitor.ps1
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'
$gw       = "C:\Users\dev8\Documents\wa-bot\gateway"
$recover  = "C:\Users\dev8\Documents\wa-bot\service\wa-recover.ps1"
$alert    = "$gw\LOGOUT-ALERT.txt"
$adminJid = '120363042435757595@g.us'   # grup admin (Judit Polica WAG)
$state    = 'unknown'
$wasDown  = $false                       # true kalau WA sempat putus -> kirim notif saat pulih

Write-Host "[wa-monitor] mulai memantau /health tiap 45 detik..."
while ($true) {
  try { $h = Invoke-RestMethod -Uri "http://127.0.0.1:3211/health" -TimeoutSec 5 } catch { $h = $null }
  $ready  = [bool]($h -and $h.ready)
  $logout = [bool]($h -and $h.loggedOut)
  $ts = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')

  if ($ready) {
    if ($state -ne 'ok') { Write-Host "[$ts] WA READY (bot normal)"; Remove-Item $alert -Force -EA SilentlyContinue }
    # PULIH dari putus -> kabari grup admin (sekali)
    if ($wasDown) {
      $txt = "Bot sudah online kembali. (pulih otomatis, $ts)"
      try {
        $body = @{ jid = $adminJid; text = $txt } | ConvertTo-Json -Compress
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:3211/send" -Body $body -ContentType 'application/json' -TimeoutSec 20 | Out-Null
        Write-Host "[$ts] Notif pulih terkirim ke grup admin."
      } catch { Write-Host "[$ts] Gagal kirim notif pulih: $($_.Exception.Message)" }
      $wasDown = $false
    }
    $state = 'ok'
  }
  else {
    $wasDown = $true    # tandai sedang putus -> saat ready lagi, kirim notif pulih
    $qrAge = if (Test-Path "$gw\qr.png") { ((Get-Date) - (Get-Item "$gw\qr.png").LastWriteTime).TotalSeconds } else { 9999 }

    # Logout BARU (belum masuk mode linking) -> alarm keras
    if ($logout -and $state -ne 'linking') {
      "[$ts] BOT WHATSAPP LOGOUT (sesi mati). Menyiapkan QR re-link otomatis di Chrome..." | Out-File $alert
      try { msg * "BOT WHATSAPP LOGOUT ($ts). QR re-link sudah disiapkan di Chrome - silakan SCAN. (WhatsApp > Perangkat Tertaut > Tautkan Perangkat)" } catch {}
      try { [console]::beep(880,500); [console]::beep(660,500) } catch {}
      Write-Host "[$ts] !!! LOGOUT terdeteksi -> siapkan QR"
    }

    # Siapkan/refresh QR HANYA saat logout (bukan 403/throttle - itu gateway reconnect sendiri)
    if ($logout -or ($state -eq 'linking' -and $qrAge -gt 150)) {
      & $recover 2>&1 | Out-Null
      "[$ts] QR re-link SIAP di Chrome. Scan: WhatsApp > Perangkat Tertaut > Tautkan Perangkat." | Add-Content $alert
      $state = 'linking'
    }
  }
  Start-Sleep -Seconds 45
}
