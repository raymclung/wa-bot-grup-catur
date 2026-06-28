# ============================================================
#  Copot auto-start WA Bot (hapus task Scheduler).
#  Jalankan PowerShell SEBAGAI ADMINISTRATOR:
#     powershell -ExecutionPolicy Bypass -File service\uninstall-autostart.ps1
# ============================================================

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host "GAGAL: Jalankan PowerShell SEBAGAI ADMINISTRATOR." -ForegroundColor Red
    exit 1
}

foreach ($name in @('WA Bot Brain', 'WA Bot Gateway')) {
    try {
        Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction Stop
        Write-Host "OK  : Task '$name' dihapus." -ForegroundColor Green
    } catch {
        Write-Host "INFO: Task '$name' tidak ditemukan (lewati)." -ForegroundColor DarkYellow
    }
}
