param([string]$Installer, [switch]$CheckGitHub)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path ([IO.Path]::GetTempPath()) ('FIFA08-Updater-Test-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $project 'launcher-src') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler /nologo /codepage:65001 /target:exe /main:UpdaterTests "/out:$testExe" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll @sources (Join-Path $PSScriptRoot 'UpdaterTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Updater test compilation failed.' }
    $testArgs = @()
    if ($Installer) { $testArgs += (Resolve-Path -LiteralPath $Installer).Path }
    if ($CheckGitHub) { $testArgs += '--github' }
    & $testExe @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Updater tests failed.' }
} finally {
    if (Test-Path -LiteralPath $testExe) { Remove-Item -LiteralPath $testExe -Force }
}
