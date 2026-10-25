# Render dimensions and output window size are separate throughout the launcher.
function Get-Fifa08DisplayConfig {
    param(
        [ValidateSet('720','1080','1440','2160')][string]$Resolution,
        [ValidateSet('window','fullscreen')][string]$DisplayMode
    )
    $presets = @{ '720' = @(1280,720,1); '1080' = @(1920,1080,2);
        '1440' = @(2560,1440,2); '2160' = @(3840,2160,3) }
    $preset = $presets[$Resolution]
    $windowWidth = $preset[0]
    $windowHeight = $preset[1]
    if ($DisplayMode -eq 'window') {
        Add-Type -AssemblyName System.Windows.Forms
        $workArea = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
        $fit = [Math]::Min(1.0, [Math]::Min(($workArea.Width * 0.85 / $windowWidth),
            ([Math]::Max(240, $workArea.Height - 100) * 0.85 / $windowHeight)))
        $windowWidth = [int][Math]::Floor($windowWidth * $fit)
        $windowHeight = [int][Math]::Floor($windowHeight * $fit)
    }
    [pscustomobject]@{
        BufferWidth = $preset[0]; BufferHeight = $preset[1]; RenderScale = $preset[2]
        WindowWidth = $windowWidth; WindowHeight = $windowHeight
    }
}
