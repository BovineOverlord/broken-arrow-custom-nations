@echo off
setlocal
cd /d "%~dp0"
title Custom Nations Installer
echo Custom Nations comes precompiled. No build tools are needed.
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set "INSTALL_RESULT=%ERRORLEVEL%"
echo.
if not "%INSTALL_RESULT%"=="0" echo Installation stopped. Read the error above for the next step.
if not "%INSTALL_RESULT%"=="0" echo Keep the whole extracted download together, including scripts and dist.
pause
exit /b %INSTALL_RESULT%
