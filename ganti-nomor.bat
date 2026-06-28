@echo off
REM ============================================================
REM  Ganti nomor bot WhatsApp.
REM  Menghapus sesi login lama (gateway/auth) lalu menjalankan
REM  gateway ulang untuk scan QR dengan nomor BARU.
REM
REM  Aturan & config TIDAK terhapus — hanya sesi login.
REM ============================================================
echo === Ganti Nomor Bot WhatsApp ===
echo.
echo PENTING: Tutup dulu jendela Gateway yang sedang jalan (tekan Ctrl+C di sana).
echo.
pause

cd /d "%~dp0gateway"

if exist auth (
  echo Menghapus sesi login lama...
  del /q auth\* 2>nul
  for /d %%D in (auth\*) do rmdir /s /q "%%D" 2>nul
)

echo Sesi lama dihapus. Menjalankan gateway untuk QR baru...
echo Scan QR yang muncul dengan HP nomor BARU, lalu jadikan nomor itu ADMIN grup.
echo.
npm start
