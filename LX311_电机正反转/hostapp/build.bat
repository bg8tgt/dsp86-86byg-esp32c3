@echo off
setlocal
rem Build LX311 Host (WinForms, .NET 4.0 API) with the in-box C# compiler.
rem Works on Windows 7 SP1 and later, no Visual Studio required.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. Install .NET Framework 4.x first.
  pause
  exit /b 1
)

if not exist dist mkdir dist

"%CSC%" /nologo /target:winexe /langversion:5 /optimize+ ^
  /win32manifest:app.manifest /win32icon:app.ico ^
  /out:dist\LX311_Host.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\Modbus.cs src\MainForm.cs

if errorlevel 1 (
  echo.
  echo [ERROR] build failed.
  pause
  exit /b 1
)

echo.
echo Build OK: dist\LX311_Host.exe
