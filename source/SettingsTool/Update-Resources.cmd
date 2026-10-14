@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-Resources.ps1" -PackageRoot "%~dp0."
pause
