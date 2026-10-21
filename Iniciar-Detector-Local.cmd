@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Start-LocalVision.ps1" -Install
if errorlevel 1 pause