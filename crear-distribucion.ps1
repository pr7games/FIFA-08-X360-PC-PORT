param(
    [string]$Destination = (Join-Path $PSScriptRoot 'dist\FIFA 08 PC Installer'),
    [string]$InstalledRoot,
    [string]$StandaloneDestination = (Join-Path $PSScriptRoot 'dist\FIFA08-PC-Setup.exe')
)
$ErrorActionPreference = 'Stop'
# InstalledRoot packages the reviewed binaries without copying user data or game assets.
$componentRoot = $PSScriptRoot
$editorRoot = $PSScriptRoot
if ($InstalledRoot) {
    $InstalledRoot = (Resolve-Path -LiteralPath $InstalledRoot).Path
    $componentRoot = Join-Path $InstalledRoot 'bin'
    $editorRoot = $InstalledRoot
} else {
    & (Join-Path $PSScriptRoot 'modding\build.cmd') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del editor.' }
}
& (Join-Path $PSScriptRoot 'build-launcher.cmd') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del launcher.' }
& (Join-Path $PSScriptRoot 'build-installer.cmd') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del instalador.' }
$payload = Join-Path $Destination 'payload'
New-Item -ItemType Directory -Force -Path $Destination,$payload | Out-Null
foreach ($relative in @('FIFA08 editor by pr7.exe','modding\LEEME.txt','modding\THIRD_PARTY_NOTICES.txt')) {
    $target = Join-Path $payload $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    $base = if ($relative -eq 'FIFA08 editor by pr7.exe') { $editorRoot } else { $PSScriptRoot }
    Copy-Item -LiteralPath (Join-Path $base $relative) -Destination $target -Force
}
foreach ($relative in @('NOVEDADES-FIFA08.txt','FIFA09-NOTICE.txt','FIFA 08 Launcher.exe','FIFA 08.exe','probar-fifa08.ps1','display-config.ps1','configurar-idiomas.ps1','launcher-src\GameLanguages.cs','launcher-assets\ea-fifa-reference.png','launcher-assets\cover.jpg','launcher-assets\icon.ico','launcher-assets\fifa08_bg.jpg','launcher-assets\fifa08_icon.png','native\build\fifa08.exe','sounds\logro-xbox.mp3')) {
    $base = if ($relative -in @('FIFA 08 Launcher.exe','FIFA 08.exe','probar-fifa08.ps1','display-config.ps1','configurar-idiomas.ps1','launcher-src\GameLanguages.cs','FIFA09-NOTICE.txt','NOVEDADES-FIFA08.txt')) { $PSScriptRoot } else { $componentRoot }
    $source = Join-Path $base $relative
    if (!(Test-Path -LiteralPath $source)) { throw "Falta el archivo requerido: $relative" }
    $target = Join-Path $payload $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}
Copy-Item -LiteralPath (Join-Path $componentRoot 'launcher-assets\ps-buttons') -Destination (Join-Path $payload 'launcher-assets') -Recurse -Force
foreach ($api in @('D3D12','Vulkan')) {
    $source = Join-Path $componentRoot ('native\build\Backends\' + $api)
    $manifest = Get-Content (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json
    $target = Join-Path $payload ('native\build\Backends\' + $api)
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($entry in $manifest.Files) {
        if ([IO.Path]::GetFileName($entry.Name) -ne $entry.Name -or (Get-FileHash -LiteralPath (Join-Path $source $entry.Name)).Hash -ne $entry.SHA256) { throw "Componente invalido: $api / $($entry.Name)" }
        Copy-Item -LiteralPath (Join-Path $source $entry.Name) -Destination $target -Force
    }
    Copy-Item -LiteralPath (Join-Path $source 'manifest.json') -Destination $target -Force
}
$primary = Get-Content (Join-Path $payload 'native\build\Backends\Vulkan\manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $primary.Files) { Copy-Item -LiteralPath (Join-Path $payload ('native\build\Backends\Vulkan\' + $entry.Name)) -Destination (Join-Path $payload 'native\build') -Force }
$sourceXexHash = if ($InstalledRoot) { (Get-Content -LiteralPath (Join-Path $componentRoot 'port-manifest.json') -Raw | ConvertFrom-Json).SourceDefaultXexSHA256 } else { (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'analysis\private\game\default.xex')).Hash }
@{Version=([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim());SourceDefaultXexSHA256=$sourceXexHash;GameExecutableSHA256=(Get-FileHash -LiteralPath (Join-Path $payload 'native\build\fifa08.exe')).Hash;GameAssetsIncluded=$false} | ConvertTo-Json | Set-Content (Join-Path $payload 'port-manifest.json') -Encoding UTF8
@{GraphicsApi='Vulkan';Resolution='1080';DisplayMode='window';FrameLimit='60';TextLanguage='es';CommentaryLanguage='es';PostEffect='none';VSync=$false;ShowFps=$false} | ConvertTo-Json | Set-Content (Join-Path $payload 'launcher-settings.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Instalador FIFA 08.exe') -Destination $Destination -Force
@'
Ejecuta Instalador FIFA 08.exe junto a la carpeta payload.
Selecciona tu ISO de Xbox 360, carpeta GOD o carpeta extraida con default.xex, y el destino.
El paquete contiene el port y sus componentes; debes aportar los archivos originales del juego.
El launcher permite elegir API, resolucion, ventana, FPS, FXAA, textos, comentarios y VSync.
Launcher e instalador: ES | EN cambia la interfaz en tiempo real y recuerda la eleccion.
El instalador detecta textos y relatos incluidos en la copia compatible; el launcher
permite elegirlos por separado. No descarga ni agrega idiomas ausentes del original.
Launcher and installer: ES | EN switches the interface instantly. Available game
text and commentary languages are detected from your supported original copy.
FIFA08 editor by pr7.exe permite editar esta instalacion; instrucciones en modding/LEEME.txt.
Opciones contiene los controles de avisos y sonido de logros. Los ajustes de desarrollo no se muestran.
F7: logros. F10: menu PC. Alt + Enter: pantalla completa.
Consulta NOVEDADES-FIFA08.txt dentro del payload para la guia y las limitaciones conocidas.
'@ | Set-Content (Join-Path $Destination 'LEEME_INSTALACION.txt') -Encoding UTF8
Write-Host "Paquete preparado: $Destination"

# Also produce the single-file installer from this exact payload.
& (Join-Path $PSScriptRoot "build-standalone-installer.ps1") -Payload $payload -Destination $StandaloneDestination
if (!$?) { throw "Fallo el instalador integrado." }
