@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo .NET Framework C# compiler was not found.
  exit /b 1
)
"%CSC%" /nologo /target:winexe /out:E-MU-Control-Desk.exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /resource:E-MU_Control_Desk.html,E-MU_Control_Desk.html /resource:EMU-PROTEUS-2000.jpg,EMU-PROTEUS-2000.jpg /resource:EMU-AUDITY-2000.jpg,EMU-AUDITY-2000.jpg /resource:EMU-VIRTUOSO-2000.jpg,EMU-VIRTUOSO-2000.jpg /resource:EMU-XTREME-LEAD-1.jpg,EMU-XTREME-LEAD-1.jpg /resource:E-MU-TURBOPHATT.jpg,E-MU-TURBOPHATT.jpg /resource:E-MU-Orbit-3.jpg,E-MU-Orbit-3.jpg /resource:E-MU-MOPHATT.jpg,E-MU-MOPHATT.jpg /resource:E-MU-B-3.jpg,E-MU-B-3.jpg Launcher.cs
if errorlevel 1 exit /b 1
echo Built E-MU-Control-Desk.exe
