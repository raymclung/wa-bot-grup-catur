@echo off
REM ============================================================
REM  Restart bot (brain + gateway) TANPA admin.
REM  Syarat: grant-bot-control.ps1 sudah dijalankan sekali (sebagai admin).
REM  Pakai ini setelah ada perubahan kode, atau kalau bot perlu disegarkan.
REM  Config (kata-kata/FAQ/dll) TIDAK perlu restart - sudah auto-reload.
REM ============================================================
echo Merestart WA Bot Gateway...
powershell -NoProfile -Command "try { Stop-ScheduledTask -TaskName 'WA Bot Gateway' -ErrorAction Stop; Start-Sleep 3; Start-ScheduledTask -TaskName 'WA Bot Gateway' -ErrorAction Stop } catch { Write-Host ('GAGAL: ' + $_.Exception.Message); exit 1 }"
if errorlevel 1 goto fail

echo Merestart WA Bot Brain...
powershell -NoProfile -Command "try { Stop-ScheduledTask -TaskName 'WA Bot Brain' -ErrorAction Stop; Start-Sleep 3; Start-ScheduledTask -TaskName 'WA Bot Brain' -ErrorAction Stop } catch { Write-Host ('GAGAL: ' + $_.Exception.Message); exit 1 }"
if errorlevel 1 goto fail

echo.
echo Selesai. Tunggu ~30-60 detik (brain cold-start), lalu cek bot di grup.
exit /b 0

:fail
echo.
echo GAGAL merestart. Pastikan grant-bot-control.ps1 sudah dijalankan (sebagai admin) dan kedua task ada.
exit /b 1
