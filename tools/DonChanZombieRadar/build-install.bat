@echo off
setlocal
cd /d "%~dp0"
echo [DonChanZombieRadar] Building...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo [DonChanZombieRadar] Installing...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 (
  echo Install failed.
  pause
  exit /b 1
)
echo Done.
pause
