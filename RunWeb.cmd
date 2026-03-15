@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RunWeb.ps1" %*
endlocal
