# ============================================================
#  Pasang auto-start WA Bot saat server booting (Task Scheduler).
#  Membuat 2 task (berjalan sebagai SYSTEM, saat startup):
#    - "WA Bot Brain"
#    - "WA Bot Gateway"
#  Masing-masing memakai wrapper loop-restart di folder service\.
#
#  JALANKAN PowerShell SEBAGAI ADMINISTRATOR, lalu:
#     powershell -ExecutionPolicy Bypass -File service\install-autostart.ps1
#
#  PENTING: scan QR sekali secara interaktif (npm start) SEBELUM
#  mengandalkan autostart, agar sesi login (gateway\auth) sudah ada.
# ============================================================

$ErrorActionPreference = 'Stop'

# Pastikan dijalankan sebagai Administrator
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host "GAGAL: Jalankan PowerShell SEBAGAI ADMINISTRATOR." -ForegroundColor Red
    exit 1
}

$root = Split-Path -Parent $PSScriptRoot          # folder wa-bot
$service = Join-Path $root 'service'
$brainCmd = Join-Path $service 'run-brain.cmd'
$gwCmd = Join-Path $service 'run-gateway.cmd'

$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)

$brainAction = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c `"$brainCmd`""
$gwAction = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c `"$gwCmd`""

Register-ScheduledTask -TaskName 'WA Bot Brain' -Action $brainAction -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "OK  : Task 'WA Bot Brain' terpasang." -ForegroundColor Green

Register-ScheduledTask -TaskName 'WA Bot Gateway' -Action $gwAction -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "OK  : Task 'WA Bot Gateway' terpasang." -ForegroundColor Green

Write-Host ""
Write-Host "Selesai. Bot akan otomatis jalan setiap server booting." -ForegroundColor Cyan
Write-Host "Uji tanpa reboot:  Start-ScheduledTask -TaskName 'WA Bot Brain'; Start-ScheduledTask -TaskName 'WA Bot Gateway'" -ForegroundColor DarkGray
Write-Host "Lihat status    :  Get-ScheduledTask -TaskName 'WA Bot*'" -ForegroundColor DarkGray
Write-Host "Copot           :  service\uninstall-autostart.ps1" -ForegroundColor DarkGray
