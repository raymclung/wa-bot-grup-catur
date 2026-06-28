@echo off
REM ============================================================
REM  Wrapper headless Ollama server (loop-restart) untuk Task Scheduler (SYSTEM).
REM  Tujuan: Ollama jalan LEPAS dari sesi login RDP (tahan logoff/disconnect),
REM  jadi tidak mati tiap sesi desktop terganggu.
REM
REM  Env penting:
REM   - OLLAMA_MODELS        : folder model (WAJIB, biar SYSTEM tidak "kehilangan" model dev8)
REM   - OLLAMA_HOST          : alamat bind (127.0.0.1:11434 = lokal saja)
REM   - OLLAMA_MAX_LOADED_MODELS=1 : cegah >1 model termuat sekaligus (hemat VRAM, anti-OOM)
REM   - OLLAMA_NUM_PARALLEL=1      : 1 permintaan sekaligus (stabil di 16GB VRAM)
REM   - OLLAMA_KEEP_ALIVE=30m      : model tetap hangat 30 menit
REM ============================================================
set "OLLAMA_HOST=127.0.0.1:11434"
set "OLLAMA_MODELS=C:\Users\dev8\.ollama\models"
set "OLLAMA_MAX_LOADED_MODELS=1"
set "OLLAMA_NUM_PARALLEL=1"
set "OLLAMA_KEEP_ALIVE=24h"

set "OLLAMA_EXE=C:\Users\dev8\AppData\Local\Programs\Ollama\ollama.exe"

:loop
"%OLLAMA_EXE%" serve
echo [%date% %time%] Ollama serve berhenti. Restart dalam 5 detik...
timeout /t 5 /nobreak >nul
goto loop
