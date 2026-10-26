@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Error: No se encontro el compilador csc.exe de .NET Framework.
    exit /b 1
)

echo Compilando FIFA 08 Launcher.exe...
"%CSC%" %FIFA08_LAUNCHER_DEFINES% /codepage:65001 /target:winexe /optimize+ /platform:anycpu /out:"%~dp0FIFA 08 Launcher.exe" /win32icon:"%~dp0launcher-assets\icon.ico" /win32manifest:"%~dp0launcher-src\app.manifest" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "%~dp0launcher-src\Updater.cs" "%~dp0launcher-src\Program.cs" "%~dp0launcher-src\ModernLauncher.cs" "%~dp0launcher-src\ModernAchievementCards.cs" "%~dp0launcher-src\PromptMods.cs" "%~dp0launcher-src\LauncherLanguage.cs" "%~dp0launcher-src\LauncherFeatures.cs" "%~dp0launcher-src\GameLanguages.cs" "%~dp0launcher-src\PortUi.cs" "%~dp0launcher-src\BrandHeader.cs" "%~dp0launcher-src\DarkScrollHost.cs"

if %ERRORLEVEL% equ 0 (
    echo Compilacion exitosa: "FIFA 08 Launcher.exe" generado.
    copy /y "%~dp0FIFA 08 Launcher.exe" "%~dp0FIFA 08.exe" >nul
) else (
    echo Error durante la compilacion.
    exit /b 1
)

exit /b 0
