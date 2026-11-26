@echo off
set DST=%ProgramFiles%\YzControl
mkdir "%DST%" 2>nul
xcopy /E /I /Y /Q /R "%~dp0SatData" "%DST%\SatData\" >nul
xcopy /E /I /Y /Q /R "%~dp0Sat" "%DST%\Sat\" >nul
copy /Y "%~dp0YzControl.exe" "%DST%\" >nul
copy /Y "%~dp0YzControl.exe.config" "%DST%\" >nul
powershell -NoProfile -Command "$w=New-Object -ComObject WScript.Shell;$s=$w.CreateShortcut([Environment]::GetFolderPath('Desktop')+'\YzControl.lnk');$s.TargetPath='%DST%\YzControl.exe';$s.WorkingDirectory='%DST%';$s.Save()"
exit /b 0
