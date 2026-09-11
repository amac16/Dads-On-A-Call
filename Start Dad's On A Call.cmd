@echo off
setlocal
if exist "%~dp0dist\DadsOnACall.exe" goto launch
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (
    echo Build failed. See the error above.
    pause
    exit /b 1
)
:launch
start "" "%~dp0dist\DadsOnACall.exe"
