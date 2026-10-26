param(
    [ValidateSet('legacy','native','performance','balanced','quality')][string]$FsrProfile='legacy',
    [ValidateRange(-1,5)][int]$Anisotropy=-1,
    [bool]$KeyboardEnabled=$false,
    [switch]$CaptureOutput,
    [switch]$TraceGpu,
    [bool]$AsyncShaders = $false,
    [bool]$EdramProtection = $true,
    [bool]$AchievementPopup = $true,
    [bool]$AchievementSound = $true,
    [ValidateSet('es','en')][string]$InterfaceLanguage = 'es',
    [switch]$PrepareOnly,
    [switch]$PlanOnly,
    [ValidatePattern('^(?:[a-z]{2}|[a-z]{3}_[a-z]{2})$')][string]$TextLanguage = 'es',
    [ValidatePattern('^(?:original|[a-z]{2}|[a-z]{3}_[a-z]{2})$')][string]$CommentaryLanguage = 'en',
    [ValidateSet('none','fxaa','fxaa_extreme','fsr')][string]$PostEffect = 'none',
    [bool]$VSync = $false,
    [ValidateSet('720','1080','1440','2160','1080_fsr','1440_fsr','2160_fsr')][string]$Resolution = '1080',
    [ValidateSet('window','fullscreen')][string]$DisplayMode = 'fullscreen',
    [bool]$ShowFps = $false,
    [ValidateSet(0,30,60,90,100,120,144,165,200,240,360)][int]$FrameLimit = 60,
    [ValidateSet('compatibility','native')][string]$GraphicsProfile = 'compatibility',
    [ValidateSet('D3D12','Vulkan')][string]$GraphicsApi = 'Vulkan',
    [ValidateSet('original')][string]$BuildVersion = 'original',
    [ValidateSet('full','fast','none')][string]$ReadbackMode = 'fast',
    [ValidateSet('guest','host','original')][string]$PacingMode = 'guest',
    # Accepted for launcher windows that were already open before cleanup.
    # These retired experimental controls do not affect either backend.
    [Parameter(DontShow=$true)][bool]$ReuseFullReadback = $true,
    [Parameter(DontShow=$true)][ValidateRange(0,250)][int]$FenceSpinUs = 0,
    [switch]$NoWait
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'display-config.ps1')
$exe = Join-Path $PSScriptRoot 'native\build\fifa08.exe'
$env:FIFA08_PACING = $PacingMode
$env:FIFA08_GRAPHICS_API = $GraphicsApi
$env:FIFA08_FPS_LIMIT = [string]$FrameLimit
$env:FIFA08_SHOW_FPS = if ($ShowFps) { '1' } else { '0' }
$env:FIFA08_UI_LANGUAGE = $InterfaceLanguage
# Keep the original shared animation/interpolation constant. FPS unlocks are separate.
$env:FIFA08_EXPERIMENTAL_60HZ = '0'
Remove-Item Env:FIFA08_EFFECT_TEST -ErrorAction SilentlyContinue
$assets = Join-Path $PSScriptRoot 'analysis\private\game'
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'GameData\default.xex')) {
    $assets = Join-Path $PSScriptRoot 'GameData'
}
$userData = Join-Path $PSScriptRoot 'analysis\private\native-user'
if ($assets -eq (Join-Path $PSScriptRoot 'GameData')) { $userData = Join-Path $PSScriptRoot 'UserData' }
$cache = Join-Path $PSScriptRoot 'analysis\native-cache'
if ($assets -eq (Join-Path $PSScriptRoot 'GameData')) { $cache = Join-Path $PSScriptRoot 'Cache' }
$log = Join-Path $PSScriptRoot 'analysis\native-run.log'
if ($assets -eq (Join-Path $PSScriptRoot 'GameData')) { $log = Join-Path $PSScriptRoot 'Logs\fifa08.log' }
$frames = Join-Path $PSScriptRoot 'analysis\native-frames.csv'
$cleanRoot=Split-Path $PSScriptRoot -Parent
if ((Split-Path $PSScriptRoot -Leaf) -eq 'bin' -and (Test-Path -LiteralPath (Join-Path $cleanRoot 'game/default.xex'))) {
    $assets=Join-Path $cleanRoot 'game'
    $userData=Join-Path $cleanRoot 'data/user'
    $cache=Join-Path $cleanRoot 'data/cache'
    $log=Join-Path $cleanRoot 'data/logs/fifa08.log'
    $frames=Join-Path $cleanRoot 'data/logs/frames.csv'
}
$env:FIFA08_ACHIEVEMENT_SOUND=Join-Path $PSScriptRoot 'sounds/logro-xbox.mp3'
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
Remove-Item Env:FIFA08_FRAME_LOG,Env:FIFA08_DRAW_LOG -ErrorAction SilentlyContinue
Remove-Item Env:FIFA08_GPU_LOG -ErrorAction SilentlyContinue
if ($TraceGpu) {
    $env:FIFA08_FRAME_LOG = $frames
    $env:FIFA08_DRAW_LOG = Join-Path $PSScriptRoot 'analysis\guest-frame-diagnostics.csv'
    $env:FIFA08_GPU_LOG = Join-Path $PSScriptRoot ('analysis\native-gpu-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.csv')
    Write-Host "Perfil backend: $env:FIFA08_GPU_LOG"
}
if ($CaptureOutput) {
    $env:FIFA08_CAPTURE_OUTPUT = Join-Path $PSScriptRoot 'analysis\native-output.bmp'
} else {
    Remove-Item Env:FIFA08_CAPTURE_OUTPUT -ErrorAction SilentlyContinue
}
if (!(Test-Path -LiteralPath $exe)) { throw 'Falta compilar el ejecutable: build-native.cmd.' }
if (!(Test-Path -LiteralPath (Join-Path $assets 'default.xex'))) {
    throw 'Faltan los recursos extraidos: ejecutar extract-game.cjs con Node.'
}
if (Get-Process -Name 'fifa08','fifa08-mejorado','fifa08-ui','fifa08-anim','fifa08-res','fifa08-fps','fifa08-render','fifa08-performance','fifa08-backend','fifa08-integration','fifa08-cpu' -ErrorAction SilentlyContinue) {
    throw 'Cierra FIFA 08 antes de cambiar la configuracion o abrir otra instancia.'
}
if ($BuildVersion -eq 'original') {
    $backendDirectory = Join-Path (Split-Path $exe) ('Backends\' + $GraphicsApi)
    $manifestPath = Join-Path $backendDirectory 'manifest.json'
    if (!(Test-Path -LiteralPath $manifestPath)) { throw "Falta el backend $GraphicsApi preparado: $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    foreach ($entry in $manifest.Files) {
        if ([IO.Path]::GetFileName($entry.Name) -ne $entry.Name) { throw 'Nombre de componente invalido en el manifiesto.' }
        $component = Join-Path $backendDirectory $entry.Name
        if (!(Test-Path -LiteralPath $component) -or (Get-FileHash -LiteralPath $component -Algorithm SHA256).Hash -ne $entry.SHA256) {
            throw "El componente $($entry.Name) del backend $GraphicsApi no coincide con su manifiesto."
        }
    }
    foreach ($entry in $manifest.Files) {
        Copy-Item -LiteralPath (Join-Path $backendDirectory $entry.Name) -Destination (Join-Path (Split-Path $exe) $entry.Name) -Force
    }
    Write-Host "Backend: $($manifest.Origin)"
}
if ($PrepareOnly) { return }
& (Join-Path $PSScriptRoot 'configurar-idiomas.ps1') -GameData $assets -TextLanguage $TextLanguage -CommentaryLanguage $CommentaryLanguage
# 1080p uses a 960x540 guest grid, rasterized by the GPU at exact 2x.
# 1440p and 4K retain the 720p guest buffers with exact 2x and 3x GPU rendering.
$outputResolution = $Resolution.Replace('_fsr','')
$useSpatialFsr = $Resolution.EndsWith('_fsr') -or $PostEffect -eq 'fsr'
if ($FsrProfile -ne 'legacy') { $useSpatialFsr = $FsrProfile -ne 'native' }
$nativeClarity = -not $useSpatialFsr -and $outputResolution -in @('720','1080')
$internalResolution = $outputResolution
if ($useSpatialFsr) {
    $internalResolution = switch ($outputResolution) {
        '1080' { '720' }
        '1440' { '1080' }
        '2160' { '1440' }
        default { '720' }
    }
}
if ($useSpatialFsr -and $FsrProfile -ne 'legacy') {
    $internalResolution = switch ($FsrProfile) {
        'performance' { '720' }
        'balanced' { '1080' }
        'quality' { if ($outputResolution -eq '1080') { '1080' } else { '1440' } }
    }
}
$displayConfig = Get-Fifa08DisplayConfig -Resolution $outputResolution -DisplayMode $DisplayMode
$env:FIFA08_SELECTED_RESOLUTION = "$($displayConfig.BufferWidth) x $($displayConfig.BufferHeight)"
$env:FIFA08_FSR_OUTPUT_WIDTH = if ($useSpatialFsr) { [string]$displayConfig.BufferWidth } else { '0' }
$env:FIFA08_FSR_OUTPUT_HEIGHT = if ($useSpatialFsr) { [string]$displayConfig.BufferHeight } else { '0' }
$renderConfig = Get-Fifa08DisplayConfig -Resolution $internalResolution -DisplayMode fullscreen
$preset = @($renderConfig.BufferWidth, $renderConfig.BufferHeight, $renderConfig.RenderScale)
$guestWidth = if($internalResolution -eq '1080'){960}else{1280}
$guestHeight = if($internalResolution -eq '1080'){540}else{720}
if($useSpatialFsr -and $FsrProfile -ne 'legacy'){
    # Guest grids use 32-pixel pitches; the renderer aligns each tile pitch separately.
    $table=@{
        '720'=@{performance=@(960,540,1);balanced=@(1088,612,1);quality=@(1152,648,1)}
        '1080'=@{performance=@(960,540,1);balanced=@(1280,720,1);quality=@(1600,900,2)}
        '1440'=@{performance=@(1280,720,1);balanced=@(1664,936,2);quality=@(1920,1080,2)}
        '2160'=@{performance=@(1920,1080,2);balanced=@(2560,1440,2);quality=@(2880,1620,3)}
    }
    $preset=$table[$outputResolution][$FsrProfile]
    $guestWidth=[int]($preset[0]/$preset[2]);$guestHeight=[int]($preset[1]/$preset[2])
    $internalResolution="$($preset[0])x$($preset[1])"
}
# Actual rasterized input before FSR (not the smaller Xbox coordinate grid).
$env:FIFA08_FSR_INPUT_WIDTH=if($useSpatialFsr){[string]$preset[0]}else{'0'}
$env:FIFA08_FSR_INPUT_HEIGHT=if($useSpatialFsr){[string]$preset[1]}else{'0'}
$env:FIFA08_GUEST_WIDTH=[string]$guestWidth
$env:FIFA08_GUEST_HEIGHT=[string]$guestHeight
$env:FIFA08_NATIVE_1080=if($guestWidth -eq 960 -and $guestHeight -eq 540){'1'}else{'0'}
# RCAS sharpens a full-resolution native frame without running EASU.
$env:FIFA08_NATIVE_RCAS=if($useSpatialFsr){'0'}else{'1'}
$fullscreenArgument = if ($DisplayMode -eq 'fullscreen') { '--fullscreen' } else { '--no-fullscreen' }
$windowWidth = $displayConfig.WindowWidth
$windowHeight = $displayConfig.WindowHeight
New-Item -ItemType Directory -Force $userData, $cache | Out-Null
$arguments = @('--game_data_root', ('"' + $assets + '"'),
    '--user_data_root', ('"' + $userData + '"'), '--cache_root', ('"' + $cache + '"'),
    '--log_file', ('"' + $log + '"'), '--log_level', $(if ($TraceGpu) { 'info' } else { 'warning' }),
    ('--user_language=' + [Fifa08Languages]::UserLanguage($(if ($CommentaryLanguage -eq 'original') { $TextLanguage } else { $CommentaryLanguage }))),
    ('--swap_post_effect=' + $(if ($useSpatialFsr -or $nativeClarity -or $PostEffect -eq 'fsr') { 'none' } else { $PostEffect })),
    ('--anisotropic_override=' + $Anisotropy),
    ('--mnk_mode=' + $KeyboardEnabled.ToString().ToLowerInvariant()),
    ('--present_fsr_sharpness_reduction=' + $(if($useSpatialFsr){'0'}elseif($nativeClarity){'0.5'}else{'1'})),
    ('--present_effect=' + $(if ($useSpatialFsr) { 'fsr' } else { 'bilinear' })),
    ('--async_shader_compilation=' + $AsyncShaders.ToString().ToLowerInvariant()),
    ('--edram_ownership_no_wrap=' + $EdramProtection.ToString().ToLowerInvariant()),
    ('--fifa08_achievement_popup=' + $AchievementPopup.ToString().ToLowerInvariant()),
    ('--fifa08_achievement_sound=' + $AchievementSound.ToString().ToLowerInvariant()),
    '--host_coalesce_guest_paints=true',
    ('--gpu_log_sync_costs=' + $TraceGpu.IsPresent.ToString().ToLowerInvariant()),
    '--readback_resolve_skip_unchanged=true',
    '--clear_memory_page_state=true',
    ('--readback_resolve=' + $ReadbackMode),
    ('--window_width=' + $windowWidth), ('--window_height=' + $windowHeight),
    '--video_mode_width=1280', '--video_mode_height=720',
    ('--resolution_scale=' + $preset[2]),
    ('--draw_resolution_scale_x=' + $preset[2]),
    ('--draw_resolution_scale_y=' + $preset[2]),
    $fullscreenArgument,
    # Preserve the guest clock at 60 Hz even for experimental 90/120 caps.
    '--video_mode_refresh_rate=60', ('--vsync=' + $VSync.ToString().ToLowerInvariant()))
if ($KeyboardEnabled) {
    $controlsFile=Join-Path $PSScriptRoot 'launcher-controls.json'
    if (Test-Path -LiteralPath $controlsFile) {
        $bindings=Get-Content -LiteralPath $controlsFile -Raw | ConvertFrom-Json
        foreach($name in @('a','b','x','y','left_trigger','right_trigger','left_shoulder','right_shoulder','start','back','lstick_up','lstick_down','lstick_left','lstick_right','lstick_press','rstick_up','rstick_down','rstick_left','rstick_right','rstick_press','dpad_up','dpad_down','dpad_left','dpad_right')) {
            $key=[string]$bindings.$name
            if ($key -and $key -match '^[A-Za-z0-9+_,]+$') { $arguments += "--keybind_$name=$key" }
        }
    }
}
if ($GraphicsApi -eq 'Vulkan') {
    if ($GraphicsProfile -eq 'compatibility') { $arguments += '--render_target_path_vulkan=fsi' }
    $arguments += @('--host_present_from_non_ui_thread=false',
        '--vulkan_allow_present_mode_mailbox=true',
        '--vulkan_allow_present_mode_immediate=false', '--vulkan_allow_present_mode_fifo_relaxed=true',
        '--readback_memexport=false', '--vulkan_readback_memexport=false',
        '--depth_float24_convert_in_pixel_shader=false',
        '--execute_unclipped_draw_vs_on_cpu=true', '--execute_unclipped_draw_vs_on_cpu_for_psi_render_backend=true',
        '--execute_unclipped_draw_vs_on_cpu_with_scissor=false',
        '--occlusion_query_enable=true',
        # Batch until a frame boundary or a required readback instead of
        # submitting at every primary PM4 buffer boundary.
        '--vulkan_submit_on_primary_buffer_end=false',
        ('--vulkan_log_fence_waits=' + $TraceGpu.IsPresent.ToString().ToLowerInvariant()), ('--vulkan_log_present_timing=' + $TraceGpu.IsPresent.ToString().ToLowerInvariant()))
} else {
    if ($VSync) { $arguments += '--host_present_from_non_ui_thread=false' }
    $arguments += @(('--render_target_path_d3d12=' + $(if ($GraphicsProfile -eq 'compatibility') { 'rov' } else { 'rtv' })),
        '--d3d12_submit_on_primary_buffer_end=false',
        '--d3d12_startup_buffer_recovery=true',
        ('--d3d12_startup_recovery_target_fps=' + $(if ($FrameLimit -gt 0) { $FrameLimit } else { 120 })),
        '--d3d12_paced_present_tearing=true', '--d3d12_allow_variable_refresh_rate_and_tearing=true')
}
Write-Host 'Iniciando FIFA 08...'
Write-Host "Registro: $log"
Write-Host "Version: $BuildVersion; cuadros: $frames"
Write-Host "API grafica solicitada: $GraphicsApi"
Write-Host "Buffer real: $($preset[0]) x $($preset[1]); ventana inicial: $windowWidth x $windowHeight"
if ($PlanOnly) {
    [pscustomobject]@{Executable=$exe; Arguments=$arguments; BufferWidth=$preset[0]; BufferHeight=$preset[1];
        FrameLimit=$FrameLimit; PacingMode=$PacingMode; TextLanguage=$TextLanguage; CommentaryLanguage=$CommentaryLanguage;
        GraphicsApi=$GraphicsApi; VSync=$VSync; PostEffect=$PostEffect; Resolution=$Resolution; InternalResolution=$internalResolution; SpatialFsr=$useSpatialFsr; GameLaunched=$false} | ConvertTo-Json -Depth 4
    return
}
$process = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
if ($NoWait) { return $process }
$process.WaitForExit()
Write-Host "El proceso termino con codigo $($process.ExitCode)."


if ($process.ExitCode -ne 0) { throw "FIFA 08 termino con codigo $($process.ExitCode). Consulta $log" }
