@echo off
REM Smoke test brain (terisolasi, tak sentuh bot live). Build dulu lalu uji.
cd /d "%~dp0.."
dotnet build brain -nologo -v q
if errorlevel 1 ( echo BUILD GAGAL & exit /b 2 )
node tests\smoke.mjs
