@echo off
setlocal
cd /d "%~dp0"
echo [DonChanTelemetryProbe] Building...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Build failed. Exit code: %RC%
  pause
  exit /b %RC%
)
echo.
echo Build succeeded.
pause
exit /b 0
