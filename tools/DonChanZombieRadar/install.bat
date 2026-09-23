@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
set ERR=%ERRORLEVEL%
if not "%ERR%"=="0" (
  echo Install failed. Exit code: %ERR%
  pause
  exit /b %ERR%
)
pause
