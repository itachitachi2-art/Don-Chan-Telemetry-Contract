@echo off
setlocal
cd /d "%~dp0"
echo [DonChanZombieRadar] Building...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set ERR=%ERRORLEVEL%
if not "%ERR%"=="0" (
  echo.
  echo Build failed. Exit code: %ERR%
  pause
  exit /b %ERR%
)
echo.
echo Build succeeded.
pause
