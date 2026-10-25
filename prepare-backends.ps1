$ErrorActionPreference = 'Stop'
$build = Join-Path $PSScriptRoot 'native\build'
$source = Join-Path $PSScriptRoot 'tools/rexglue-source/rexglue-sdk-0.10.0/src/ui/vulkan/vulkan_device.cpp'
if (!(Test-Path $source) -or !(Select-String -LiteralPath $source -SimpleMatch 'graphics_queue_count' -Quiet)) { throw 'Reviewed Vulkan queue-isolation source is missing; run bootstrap.' }
foreach ($api in @('D3D12','Vulkan')) {
    $folder = Join-Path $build ('Backends\' + $api)
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    # Copy fresh binaries from build directory if present.
    foreach ($name in @('rexruntime.dll','rexgpu-xenosrd.dll')) {
        $source = Join-Path $build $name
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $folder $name) -Force
        }
    }
    # Each API receives the runtime and renderer compiled together.
    $files = foreach ($name in @('rexruntime.dll','rexgpu-xenosrd.dll')) {
        $component = Join-Path $folder $name
        if (!(Test-Path -LiteralPath $component)) { throw "Falta el componente compilado: $component" }
        [PSCustomObject]@{ Name = $name; SHA256 = (Get-FileHash -LiteralPath $component).Hash }
    }
    [PSCustomObject]@{
        API = $api
        Origin = if ($api -eq 'D3D12') { 'FIFA08 matching SDK D3D12 backend with configurable VSync' } else { 'FIFA08 matching SDK Vulkan backend with isolated presentation queue' }
        Files = @($files)
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $folder 'manifest.json') -Encoding UTF8
}
Write-Output 'Backends preparados: D3D12 configurable y Vulkan con cola independiente.'
