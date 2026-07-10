# ============================================================
#  Publikasikan QR re-link bot ke https://dev8.chess.stream/<token>.html
#  supaya bisa di-scan JARAK JAUH lewat URL (tak perlu video call).
#  Jalan sebagai user 'dev8' (punya izin tulis web root) - gateway sendiri
#  jalan sebagai SYSTEM yang TIDAK bisa tulis ke folder IIS.
#  Keamanan: path pakai TOKEN rahasia (tak tertebak); QR hilang saat sudah tertaut.
#  Jalan manual: powershell -ExecutionPolicy Bypass -File service\wa-qr-publish.ps1
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'
$tok      = 'walink-b7bfb37c4786a491'
$webroot  = 'C:\inetpub\Websites\dev8.chess.stream'
$gwqr     = 'C:\Users\dev8\Documents\wa-bot\gateway\qr.png'
$htmlPath = Join-Path $webroot ($tok + '.html')
$pngPath  = Join-Path $webroot ($tok + '.png')

Write-Host "[wa-qr-publish] mulai. URL: https://dev8.chess.stream/$tok.html"
while ($true) {
  try { $h = Invoke-RestMethod -Uri 'http://127.0.0.1:3211/health' -TimeoutSec 4 } catch { $h = $null }
  $ready = [bool]($h -and $h.ready)

  if ($ready) {
    # Sudah tertaut -> JANGAN tampilkan QR live di publik
    Set-Content -Path $htmlPath -Encoding UTF8 -Value '<!doctype html><html><head><meta charset="utf-8"></head><body style="text-align:center;font-family:sans-serif;padding:40px"><h2>Bot sudah tertaut &#10003;</h2><p>Tidak ada QR aktif.</p></body></html>'
    Remove-Item $pngPath -Force -EA SilentlyContinue
  }
  elseif (Test-Path $gwqr) {
    # Mode QR -> salin QR terbaru + halaman auto-refresh (cache-bust supaya selalu gambar terbaru)
    Copy-Item $gwqr $pngPath -Force
    $t = [DateTimeOffset]::Now.ToUnixTimeSeconds()
    $html = @"
<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="refresh" content="12"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Tautkan Bot</title></head>
<body style="text-align:center;font-family:sans-serif;padding:18px;background:#f7f7f7">
<h2>Tautkan Bot (Judit Polica)</h2>
<p>WhatsApp &rarr; Perangkat Tertaut &rarr; Tautkan Perangkat &rarr; scan:</p>
<img src="$tok.png?t=$t" style="max-width:92vw;height:auto;border:1px solid #ccc">
<p style="color:#888">QR auto-refresh ~12 dtk &mdash; scan barcode yang sedang tampil.</p></body></html>
"@
    Set-Content -Path $htmlPath -Encoding UTF8 -Value $html
  }
  Start-Sleep -Seconds 10
}
