@echo off
setlocal
"C:\Program Files\nodejs\node.exe" "%~dp0make-vslh-fixture.cjs"
if errorlevel 1 exit /b 1
call "%~dp0..\build-tools.cmd"
if errorlevel 1 exit /b 1
"%~dp0..\build\xenon\XenonRecomp\XenonRecomp.exe" "%~dp0input" "%~dp0generated"
if errorlevel 1 exit /b 1
call "%~dp0..\build-tools.cmd" CheckVslh
if errorlevel 1 exit /b 1
"%~dp0..\build\CheckVslh.exe"
exit /b %errorlevel%
