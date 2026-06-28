# ============================================================
#  Redeploy WA Bot (brain + gateway) dengan kode terbaru, sekaligus
#  memasang autostart Task Scheduler (persisten saat reboot).
#
#  JALANKAN SEBAGAI ADMINISTRATOR:
#     powershell -ExecutionPolicy Bypass -File service\redeploy.ps1
#
#  Yang dilakukan:
#   1. Pasang/segarkan scheduled task 'WA Bot Brain' dan 'WA Bot Gateway'
#      (SYSTEM, AtStartup, loop-restart) - lihat install-autostart.ps1.
#   2. Hentikan brain+gateway yang sedang jalan (berdasarkan port 5050 dan 3211)
#      beserta wrapper loop-nya, agar tidak menjalankan kode lama.
#   3. Start ulang lewat scheduled task -> wrapper rebuild (dotnet run -c Release)
#      sehingga kode C#/Node terbaru aktif.
#   4. Verifikasi /health brain dan gateway.
# ============================================================
$ErrorActionPreference = 'Stop'

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Write-Host "GAGAL: jalankan PowerShell SEBAGAI ADMINISTRATOR." -ForegroundColor Red; exit 1 }

# --- 1) Pasang autostart (idempotent) ---
& "$PSScriptRoot\install-autostart.ps1"

# --- 2) Hentikan proses lama berdasarkan port (plus wrapper loop di atasnya) ---
function Stop-TreeByPort([int]$port) {
    $c = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $c) { Write-Host "Port $port : tak ada listener (lewati)." -ForegroundColor DarkGray; return }
    $rootPid = [int]$c.OwningProcess
    $chain = @($rootPid)
    $cur = Get-CimInstance Win32_Process -Filter "ProcessId=$rootPid" -ErrorAction SilentlyContinue
    for ($i = 0; $i -lt 2 -and $cur -and $cur.ParentProcessId; $i++) {
        $par = Get-CimInstance Win32_Process -Filter "ProcessId=$($cur.ParentProcessId)" -ErrorAction SilentlyContinue
        if ($par -and ($par.Name -match 'cmd|dotnet')) { $chain += [int]$par.ProcessId; $cur = $par } else { break }
    }
    foreach ($procId in ($chain | Select-Object -Unique)) {
        try { Stop-Process -Id $procId -Force -ErrorAction Stop; Write-Host "Port $port : hentikan PID $procId." -ForegroundColor Yellow }
        catch { Write-Host "Port $port : gagal hentikan PID $procId ($($_.Exception.Message))." -ForegroundColor Red }
    }
}

Stop-ScheduledTask -TaskName 'WA Bot Brain'   -ErrorAction SilentlyContinue
Stop-ScheduledTask -TaskName 'WA Bot Gateway' -ErrorAction SilentlyContinue
Stop-TreeByPort 5050
Stop-TreeByPort 3211
Start-Sleep -Seconds 4

# --- 3) Start ulang via scheduled task (wrapper rebuild kode terbaru) ---
Start-ScheduledTask -TaskName 'WA Bot Brain'
Start-Sleep -Seconds 3
Start-ScheduledTask -TaskName 'WA Bot Gateway'

# --- 4) Verifikasi ---
Write-Host "Menunggu brain (rebuild Release bisa ~30 dtk)..." -ForegroundColor Cyan
$brainOk = $false
for ($i = 0; $i -lt 90; $i++) {
    try { $h = Invoke-RestMethod 'http://127.0.0.1:5050/health' -TimeoutSec 3; if ($h.ok) { $brainOk = $true; Write-Host ("BRAIN UP: " + ($h | ConvertTo-Json -Compress)) -ForegroundColor Green; break } } catch {}
    Start-Sleep -Seconds 1
}
if (-not $brainOk) { Write-Host "BRAIN belum UP - cek jendela task / log." -ForegroundColor Red }

Write-Host "Menunggu gateway tersambung ke WhatsApp..." -ForegroundColor Cyan
$gwOk = $false
for ($i = 0; $i -lt 60; $i++) {
    try { $g = Invoke-RestMethod 'http://127.0.0.1:3211/health' -TimeoutSec 3; if ($g.connected) { $gwOk = $true; Write-Host ("GATEWAY CONNECTED: " + ($g | ConvertTo-Json -Compress)) -ForegroundColor Green; break } } catch {}
    Start-Sleep -Seconds 1
}
if (-not $gwOk) { Write-Host "GATEWAY belum connected - cek jendela task gateway." -ForegroundColor Red }

Write-Host ""
Write-Host "Selesai. Kode terbaru aktif dan autostart terpasang (jalan otomatis tiap reboot)." -ForegroundColor Cyan
