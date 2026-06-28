@echo off
REM ============================================================
REM  Wrapper auto-restart untuk GATEWAY (Node/Baileys).
REM  Dijalankan oleh Task Scheduler saat server booting.
REM  Beri jeda agar brain siap dulu, lalu loop-restart jika mati.
REM  Catatan: sesi login (gateway\auth) HARUS sudah ada
REM  (scan QR sekali secara interaktif sebelum mengandalkan autostart).
REM ============================================================
timeout /t 8 /nobreak >nul
cd /d "%~dp0..\gateway"
:loop
node src\index.js
echo [%date% %time%] Gateway berhenti. Restart dalam 5 detik...
timeout /t 5 /nobreak >nul
goto loop
