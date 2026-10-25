@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Error: No se encontro el compilador csc.exe de .NET Framework.
    exit /b 1
)

set "PAYLOAD_RESOURCE="
if defined FIFA08_INSTALLER_PAYLOAD set PAYLOAD_RESOURCE=/resource:"%FIFA08_INSTALLER_PAYLOAD%",Fifa08.Payload.zip

echo Compilando Instalador FIFA 08.exe...
"%CSC%" /codepage:65001 /target:winexe /optimize+ /platform:anycpu /out:"%~dp0Instalador FIFA 08.exe" /win32icon:"%~dp0launcher-assets\icon.ico" %PAYLOAD_RESOURCE% /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll "%~dp0installer-src\Program.cs" "%~dp0installer-src\InstallerStyle.cs" "%~dp0installer-src\ModernInstaller.cs" "%~dp0installer-src\InstallerLanguage.cs" "%~dp0launcher-src\PortUi.cs" "%~dp0launcher-src\BrandHeader.cs" "%~dp0launcher-src\DarkScrollHost.cs" "%~dp0launcher-src\GameLanguages.cs"

if %ERRORLEVEL% equ 0 (
    echo Compilacion exitosa: "Instalador FIFA 08.exe" generado.
) else (
    echo Error durante la compilacion del instalador.
    exit /b 1
)

exit /b 0
