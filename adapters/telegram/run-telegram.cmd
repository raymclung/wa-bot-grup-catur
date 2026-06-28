@echo off
REM Adapter Telegram — set token lalu jalankan. Loop auto-restart kalau crash.
REM Ganti TOKEN di bawah dengan token dari @BotFather, atau set env TELEGRAM_BOT_TOKEN sebelum menjalankan.

if "%TELEGRAM_BOT_TOKEN%"=="" set TELEGRAM_BOT_TOKEN=GANTI_DENGAN_TOKEN_BOTFATHER
set BRAIN_URL=http://127.0.0.1:5050
set PORT=3310

:loop
node "%~dp0index.js"
echo Adapter berhenti, restart 3 detik...
timeout /t 3 /nobreak >nul
goto loop
