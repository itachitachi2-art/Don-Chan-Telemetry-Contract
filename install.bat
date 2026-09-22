@echo off
setlocal
cd /d "%~dp0"
echo [DonChanTelemetryProbe] Installing to %%APPDATA%%\7DaysToDie\Mods ...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Install failed. Exit code: %RC%
  pause
  exit /b %RC%
)
echo.
echo Install succeeded.
pause
exit /b 0
