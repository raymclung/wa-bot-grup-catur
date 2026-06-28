@echo off
REM ============================================================
REM  Jalankan WA Bot: brain (C#) + gateway (Node) sekaligus.
REM  Membuka DUA jendela terpisah. QR muncul di jendela Gateway.
REM ============================================================
cd /d "%~dp0"

echo Menjalankan brain (C#)...
start "WA Brain (C#)" cmd /k "cd /d "%~dp0brain" && dotnet run"

REM Beri jeda agar brain siap lebih dulu sebelum gateway konek WA.
timeout /t 4 /nobreak >nul

echo Menjalankan gateway (Node) - QR akan muncul di jendelanya...
start "WA Gateway (Node) - SCAN QR DI SINI" cmd /k "cd /d "%~dp0gateway" && npm start"

echo.
echo Dua jendela terbuka:
echo   - "WA Brain (C#)"   : otak moderasi
echo   - "WA Gateway ..."  : koneksi WhatsApp (scan QR di sini)
echo.
echo Tutup jendela ini kapan saja.
