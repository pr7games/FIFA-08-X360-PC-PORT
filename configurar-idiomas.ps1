param(
    [Parameter(Mandatory=$true)][string]$GameData,
    [ValidatePattern('^(?:[a-z]{2}|[a-z]{3}_[a-z]{2})$')][string]$TextLanguage = 'es',
    [ValidatePattern('^(?:original|[a-z]{2}|[a-z]{3}_[a-z]{2})$')][string]$CommentaryLanguage = 'en'
)
$ErrorActionPreference = 'Stop'
if (!('Fifa08Languages' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'launcher-src\GameLanguages.cs') }
$original = Join-Path $PSScriptRoot 'analysis\private\locale-original.big'
[Fifa08Languages]::Configure($GameData,$TextLanguage,$CommentaryLanguage,$original)
Write-Host "Textos: $TextLanguage; comentarios: $CommentaryLanguage"
