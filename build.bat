@echo off
REM Compile Skadoosh converter en .exe cliquable avec le compilateur C# integre.
REM Aucun SDK .NET requis - utilise le .NET Framework livre avec Windows.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /out:SkadooshConverter.exe ^
  /win32icon:icon.ico ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  SkadooshConverter.cs

if %ERRORLEVEL%==0 (
  echo.
  echo Build OK -^> SkadooshConverter.exe
) else (
  echo.
  echo Build FAILED.
)
