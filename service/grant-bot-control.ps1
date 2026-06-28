# ============================================================
#  SEKALI JALAN (sebagai ADMINISTRATOR).
#  Memberi user 'dev8' izin START/STOP task bot — supaya restart bot
#  ke depannya TIDAK perlu admin lagi.
#
#  JALANKAN:
#    powershell -ExecutionPolicy Bypass -File "C:\Users\dev8\Documents\wa-bot\service\grant-bot-control.ps1"
#
#  Setelah ini, dev8 cukup pakai service\restart-bot.cmd (tanpa admin).
#
#  Catatan keamanan: task berjalan sebagai SYSTEM dan menjalankan file .cmd
#  di folder service\. Karena dev8 bisa start task + mengedit .cmd itu,
#  dev8 efektif bisa menjalankan kode sebagai SYSTEM. Aman selama folder bot
#  hanya dikelola pemilik server. (Ini konsekuensi wajar dari "restart tanpa admin".)
# ============================================================

$ErrorActionPreference = 'Stop'

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Write-Host "GAGAL: Jalankan SEBAGAI ADMINISTRATOR." -ForegroundColor Red; exit 1 }

$user = 'dev8'
$sid = (New-Object System.Security.Principal.NTAccount($user)).Translate([System.Security.Principal.SecurityIdentifier]).Value
# FA = Full Access pada task (jamin Start + Stop). Admin & SYSTEM tetap punya akses (ACE ditambahkan, bukan menimpa).
$ace = "(A;;FA;;;$sid)"
Write-Host "User $user SID: $sid" -ForegroundColor DarkGray

$svc = New-Object -ComObject Schedule.Service
$svc.Connect()
$folder = $svc.GetFolder('\')

$tasks = @('WA Bot Brain', 'WA Bot Gateway', 'Ollama Server')
foreach ($tn in $tasks) {
    $t = $null
    try { $t = $folder.GetTask($tn) } catch { Write-Host "lewati  : '$tn' (belum ada)" -ForegroundColor DarkGray; continue }

    # 4 = DACL_SECURITY_INFORMATION -> kembalikan SDDL bagian D:(...)
    $sddl = $t.GetSecurityDescriptor(4)
    if ($sddl -like "*$sid*") {
        Write-Host "sudah   : '$tn' (izin dev8 sudah ada)" -ForegroundColor DarkGray
        continue
    }
    $newSddl = $sddl + $ace   # tambah ACE ke akhir DACL (ACE digabung berurutan)
    # 0 = tanpa flag tambahan
    $t.SetSecurityDescriptor($newSddl, 0)
    Write-Host "OK      : izin Start/Stop diberikan ke dev8 untuk '$tn'" -ForegroundColor Green
}

Write-Host ""
Write-Host "Selesai. Sekarang dev8 bisa restart bot TANPA admin:" -ForegroundColor Cyan
Write-Host "   service\restart-bot.cmd            (restart brain + gateway)" -ForegroundColor Gray
Write-Host "   atau di PowerShell biasa (dev8):" -ForegroundColor Gray
Write-Host "   Stop-ScheduledTask -TaskName 'WA Bot Gateway'; Start-ScheduledTask -TaskName 'WA Bot Gateway'" -ForegroundColor Gray
