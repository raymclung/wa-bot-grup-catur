# ============================================================
#  Pasang Ollama sebagai layanan headless (Task Scheduler, SYSTEM, AtStartup).
#  Memperbaiki "Ollama sering crash" yang disebabkan Ollama jalan sebagai
#  aplikasi desktop (tray) yang terikat sesi login RDP.
#
#  JALANKAN PowerShell SEBAGAI ADMINISTRATOR, lalu:
#     powershell -ExecutionPolicy Bypass -File "C:\Users\dev8\Documents\wa-bot\service\install-ollama-service.ps1"
#
#  Yang dilakukan skrip:
#   1. Validasi ollama.exe + folder model.
#   2. Hentikan tray app + server lama (bebaskan port 11434).
#   3. Best-effort matikan auto-start tray (biar tidak rebut port saat dev8 login).
#   4. Daftarkan task SYSTEM "Ollama Server" (loop-restart via run-ollama.cmd).
#   5. Jalankan + verifikasi (/api/version + daftar model).
# ============================================================

$ErrorActionPreference = 'Stop'

# --- Pastikan Administrator ---
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host "GAGAL: Jalankan PowerShell SEBAGAI ADMINISTRATOR." -ForegroundColor Red
    exit 1
}

$ollamaExe = "C:\Users\dev8\AppData\Local\Programs\Ollama\ollama.exe"
$modelsDir = "C:\Users\dev8\.ollama\models"
$runCmd = Join-Path $PSScriptRoot 'run-ollama.cmd'

# --- Validasi ---
if (-not (Test-Path $ollamaExe)) { Write-Host "GAGAL: ollama.exe tidak ditemukan di $ollamaExe" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $modelsDir)) { Write-Host "GAGAL: folder model tidak ditemukan di $modelsDir" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $runCmd))    { Write-Host "GAGAL: run-ollama.cmd tidak ditemukan di $runCmd" -ForegroundColor Red; exit 1 }
Write-Host "OK  : ollama.exe & folder model ditemukan." -ForegroundColor Green

# --- Hentikan tray app + server lama (bebaskan 11434) ---
Get-Process 'ollama app','ollama' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Host "OK  : Tray app & server lama dihentikan." -ForegroundColor Green

# --- Best-effort: matikan auto-start tray untuk dev8 ---
$startupLnk = "C:\Users\dev8\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Ollama.lnk"
if (Test-Path $startupLnk) { Remove-Item $startupLnk -Force -ErrorAction SilentlyContinue; Write-Host "OK  : Shortcut startup tray dihapus." -ForegroundColor Green }
try {
    $sid = (New-Object System.Security.Principal.NTAccount('dev8')).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $runKey = "Registry::HKEY_USERS\$sid\Software\Microsoft\Windows\CurrentVersion\Run"
    if (Test-Path $runKey) {
        $props = (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).PSObject.Properties.Name
        foreach ($n in $props) {
            if ($n -match 'Ollama') { Remove-ItemProperty $runKey -Name $n -Force -ErrorAction SilentlyContinue; Write-Host "OK  : Run key '$n' (dev8) dihapus." -ForegroundColor Green }
        }
    }
} catch {
    Write-Host "CATATAN: tidak bisa otomatis matikan auto-start tray (dev8 mungkin tak login)." -ForegroundColor Yellow
    Write-Host "         Matikan manual: Task Manager > Startup > Ollama > Disable." -ForegroundColor Yellow
}

# --- Daftarkan task SYSTEM ---
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
$action = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c `"$runCmd`""

Register-ScheduledTask -TaskName 'Ollama Server' -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "OK  : Task 'Ollama Server' terpasang (SYSTEM, AtStartup)." -ForegroundColor Green

# --- Jalankan sekarang + verifikasi ---
Start-ScheduledTask -TaskName 'Ollama Server'
Write-Host "..  : Menunggu Ollama siap (maks ~40 dtk)..." -ForegroundColor DarkGray
$ok = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:11434/api/version' -TimeoutSec 4
        if ($r.StatusCode -eq 200) { $ok = $true; break }
    } catch { }
}

if ($ok) {
    Write-Host ""
    Write-Host "BERHASIL: Ollama jalan sebagai layanan SYSTEM." -ForegroundColor Cyan
    Write-Host "Versi   : $((Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:11434/api/version').Content)"
    Write-Host "Cek model terbaca (harus muncul daftar, mis. qwen2.5:7b):" -ForegroundColor DarkGray
    $env:OLLAMA_MODELS = $modelsDir
    & $ollamaExe list
} else {
    Write-Host ""
    Write-Host "PERINGATAN: Ollama belum merespons. Cek apakah GPU terpakai saat jalan sebagai SYSTEM." -ForegroundColor Yellow
    Write-Host "Lihat status: Get-ScheduledTask -TaskName 'Ollama Server'" -ForegroundColor DarkGray
    Write-Host "Rollback   : service\uninstall-ollama-service.ps1, lalu nyalakan lagi Ollama tray seperti biasa." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Copot/rollback :  service\uninstall-ollama-service.ps1" -ForegroundColor DarkGray
