@echo off
REM ============================================================
REM  Wrapper auto-restart untuk BRAIN (C#).
REM  Dijalankan oleh Task Scheduler saat server booting.
REM  Jika brain berhenti/crash, otomatis dijalankan lagi.
REM  Menggunakan config kanonik di brain\config (lewat dotnet run).
REM ============================================================
cd /d "%~dp0..\brain"
:loop
dotnet run -c Release
echo [%date% %time%] Brain berhenti. Restart dalam 5 detik...
timeout /t 5 /nobreak >nul
goto loop
