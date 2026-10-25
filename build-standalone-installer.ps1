param(
    [string]$Payload = (Join-Path $PSScriptRoot 'dist\FIFA 08 PC Installer\payload'),
    [string]$Destination = (Join-Path $PSScriptRoot 'dist\FIFA08-PC-Setup.exe')
)
$ErrorActionPreference = 'Stop'
$Payload = (Resolve-Path -LiteralPath $Payload).Path
foreach ($required in @('native\build\fifa08.exe','FIFA 08 Launcher.exe','port-manifest.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $Payload $required))) { throw "Falta componente: $required" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = Join-Path ([IO.Path]::GetTempPath()) ('fifa08-payload-' + [Guid]::NewGuid().ToString('N') + '.zip')
$previousPayload = $env:FIFA08_INSTALLER_PAYLOAD
try {
    [IO.Compression.ZipFile]::CreateFromDirectory($Payload, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
    $env:FIFA08_INSTALLER_PAYLOAD = $archive
    & (Join-Path $PSScriptRoot 'build-installer.cmd') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Fallo al compilar el instalador integrado.' }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Instalador FIFA 08.exe') -Destination $Destination -Force
    $digest = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash.ToLowerInvariant()
    ($digest + '  ' + [IO.Path]::GetFileName($Destination)) | Set-Content -LiteralPath ($Destination + '.sha256') -Encoding ASCII
    Write-Host "Instalador integrado listo: $Destination"
} finally {
    $env:FIFA08_INSTALLER_PAYLOAD = $previousPayload
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
}
