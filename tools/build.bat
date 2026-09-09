@echo off
REM Compile SkadooshConverter.exe a la racine du depot depuis src\*.cs, avec
REM assets\icon.ico pour icone. Aucun SDK requis : le csc.exe livre avec
REM le .NET Framework de Windows suffit.
REM   cmd /c .\tools\build.bat
setlocal
set "ROOT=%~dp0.."
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Compilateur C# introuvable ^(csc.exe^). Le .NET Framework est-il installe ?
    exit /b 1
)

echo Compilation de SkadooshConverter.exe ...
"%CSC%" /nologo /target:winexe /optimize+ ^
  /out:"%ROOT%\SkadooshConverter.exe" ^
  /win32icon:"%ROOT%\assets\icon.ico" ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.IO.Compression.dll ^
  /reference:System.IO.Compression.FileSystem.dll ^
  "%ROOT%\src\*.cs"

if errorlevel 1 (
  echo.
  echo Build FAILED.
  exit /b 1
)
echo Build OK -^> "%ROOT%\SkadooshConverter.exe"
endlocal
