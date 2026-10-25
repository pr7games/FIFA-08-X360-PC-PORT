param([switch]$SkipBootstrap,[switch]$SkipPackage)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
foreach($tool in @('python','node','cmake','ninja','clang','clang++')) { if(!(Get-Command $tool -ErrorAction SilentlyContinue)){throw "Missing prerequisite on PATH: $tool"} }
if(!$SkipBootstrap){ & python scripts/bootstrap.py; if($LASTEXITCODE){throw 'Bootstrap failed'} }
& powershell -NoProfile -ExecutionPolicy Bypass -File scripts/development-assets.ps1
if($LASTEXITCODE){throw 'Development assets failed'}
$sdk=Join-Path $PWD 'tools/rexglue-source/rexglue-sdk-0.10.0'
if(!(Test-Path analysis/private/default.xex)){throw 'Place your own supported default.xex at analysis/private/default.xex; see README.'}
$common=@('-G','Ninja','-DCMAKE_C_COMPILER=clang','-DCMAKE_CXX_COMPILER=clang++','-DCMAKE_BUILD_TYPE=RelWithDebInfo','-DREXGLUE_USE_D3D12=ON','-DREXGLUE_USE_VULKAN=ON','-DREXGLUE_ENABLE_TRACY=OFF','-DREXGLUE_ENABLE_PERF_COUNTERS=OFF','-DCMAKE_C_FLAGS=-msse4.1','-DCMAKE_CXX_FLAGS=-msse4.1')
& cmake -S $sdk -B build/sdk @common
if($LASTEXITCODE){throw 'SDK configuration failed'}
& cmake --build build/sdk --target rexglue --parallel 4
if($LASTEXITCODE){throw 'SDK code generator build failed'}
$codegen=Get-ChildItem "$sdk/out" -Filter 'rexglue*.exe' -Recurse | Where-Object {$_.Name -in @('rexglue.exe','rexgluerd.exe')} | Select-Object -First 1
if(!$codegen){throw 'Code generator output not found'}
& $codegen.FullName codegen native/fifa08_manifest.toml
if($LASTEXITCODE){throw 'Code generation failed'}
foreach($name in @('vmx','register-helpers','memory-copy','cpu-waits','resolution','presentation','crowd','unlocks')) {
 & node "patch-generated-$name.cjs";if($LASTEXITCODE){throw "Guest source adaptation failed: $name"}
}
& cmake -S native -B native/build @common "-DREXSDK_DIR=$sdk" '-DREXGLUE_RECOMP_DEBUG_INFO=none' '-DFIFA08_CPU_AVX2=ON' '-DFIFA08_DISPATCH_TELEMETRY=OFF'
if($LASTEXITCODE){throw 'Port configuration failed'}
& cmake --build native/build --parallel 4
if($LASTEXITCODE){throw 'Port build failed'}
& python scripts/check-runtime-imports.py native/build/fifa08.exe
if($LASTEXITCODE){throw 'Release runtime import validation failed'}
& powershell -NoProfile -ExecutionPolicy Bypass -File prepare-backends.ps1
if($LASTEXITCODE){throw 'Backend packaging failed'}
& .\build-launcher.cmd;if($LASTEXITCODE){throw 'Launcher failed'}
& .\modding\build.cmd;if($LASTEXITCODE){throw 'Editor failed'}
& .\build-installer.cmd;if($LASTEXITCODE){throw 'Installer failed'}
if(!$SkipPackage){
 if(!(Test-Path sounds/logro-xbox.mp3)){throw 'Optional custom audio missing: supply your locally licensed MP3, or use -SkipPackage. See README.'}
 & powershell -NoProfile -ExecutionPolicy Bypass -File crear-distribucion.ps1
 if($LASTEXITCODE){throw 'Release packaging failed'}
}
