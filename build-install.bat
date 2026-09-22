@echo off
setlocal
cd /d "%~dp0"
echo [DonChanTelemetryProbe] Build + Install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 goto :fail
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 goto :fail
echo.
echo Build + Install succeeded.
pause
exit /b 0
:fail
echo.
echo Build + Install failed. Exit code: %ERRORLEVEL%
pause
exit /b %ERRORLEVEL%
