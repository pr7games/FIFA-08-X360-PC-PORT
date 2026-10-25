@echo off
setlocal
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" exit /b 1
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:"%~dp0..\launcher-assets\icon.ico" /out:"%~dp0FIFA08 editor by pr7.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Xml.dll "%~dp0src\*.cs"
if errorlevel 1 exit /b 1
copy /y "%~dp0FIFA08 editor by pr7.exe" "%~dp0..\FIFA08 editor by pr7.exe" >nul
exit /b %errorlevel%
