# ============================================================
#  Copot layanan Ollama SYSTEM (kembali ke cara lama / tray app).
#  JALANKAN SEBAGAI ADMINISTRATOR:
#     powershell -ExecutionPolicy Bypass -File "C:\Users\dev8\Documents\wa-bot\service\uninstall-ollama-service.ps1"
#
#  Setelah ini, nyalakan lagi Ollama tray seperti biasa (Start Menu > Ollama),
#  atau aktifkan kembali auto-start tray via Task Manager > Startup bila perlu.
# ============================================================

$ErrorActionPreference = 'Stop'

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    Write-Host "GAGAL: Jalankan PowerShell SEBAGAI ADMINISTRATOR." -ForegroundColor Red
    exit 1
}

# Hentikan task + server SYSTEM
try { Stop-ScheduledTask -TaskName 'Ollama Server' -ErrorAction SilentlyContinue } catch { }
try { Unregister-ScheduledTask -TaskName 'Ollama Server' -Confirm:$false -ErrorAction Stop; Write-Host "OK  : Task 'Ollama Server' dicopot." -ForegroundColor Green }
catch { Write-Host "CATATAN: Task 'Ollama Server' tidak ditemukan (mungkin sudah dicopot)." -ForegroundColor Yellow }

Get-Process ollama -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "OK  : Server SYSTEM dihentikan." -ForegroundColor Green
Write-Host ""
Write-Host "Selesai. Untuk pakai lagi cara lama: buka Ollama dari Start Menu (tray app)." -ForegroundColor Cyan
