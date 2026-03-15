@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0OneClickDeploy.ps1" %*
endlocal
