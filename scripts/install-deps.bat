@echo off
REM Installe les dependances de Skadoosh converter :
REM   - FFmpeg (conversion audio) : telecharge automatiquement dans bin\
REM   - LibreOffice (conversion de documents) : installation manuelle
title Skadoosh converter - dependances
echo.
echo === FFmpeg (conversion audio) ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-ffmpeg.ps1"
echo.
echo === LibreOffice (conversion de documents) ===
if exist "%ProgramFiles%\LibreOffice\program\soffice.exe" (
    echo LibreOffice est deja installe.
) else if exist "%ProgramFiles(x86)%\LibreOffice\program\soffice.exe" (
    echo LibreOffice est deja installe.
) else (
    echo LibreOffice n'est pas installe. Pour convertir des documents,
    echo installez-le gratuitement depuis :  https://fr.libreoffice.org
)
echo.
echo Termine. Vous pouvez fermer cette fenetre et revenir a Skadoosh.
pause
