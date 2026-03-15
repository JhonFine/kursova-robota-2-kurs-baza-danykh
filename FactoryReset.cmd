@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FactoryReset.ps1" %*
endlocal
