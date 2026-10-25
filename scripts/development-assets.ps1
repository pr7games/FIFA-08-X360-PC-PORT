# Original, neutral development placeholders; no EA/Xbox assets are distributed.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot '../launcher-assets'
New-Item -ItemType Directory -Force $dir | Out-Null
foreach($name in @('cover.jpg','fifa08_bg.jpg','ea-fifa-reference.png','fifa08_icon.png')){
 $path=Join-Path $dir $name;if(Test-Path $path){continue}
 $b=New-Object Drawing.Bitmap 960,640;$g=[Drawing.Graphics]::FromImage($b)
 try{$g.Clear([Drawing.Color]::FromArgb(18,18,22));$font=New-Object Drawing.Font 'Segoe UI',38;try{$g.DrawString('PR7 - development build',$font,[Drawing.Brushes]::White,30,260)}finally{$font.Dispose()};$b.Save($path,$(if($name.EndsWith('.jpg')){[Drawing.Imaging.ImageFormat]::Jpeg}else{[Drawing.Imaging.ImageFormat]::Png}))}finally{$g.Dispose();$b.Dispose()}
}
$icon=Join-Path $dir 'icon.ico'
if(!(Test-Path $icon)){$stream=[IO.File]::Create($icon);try{[Drawing.SystemIcons]::Application.Save($stream)}finally{$stream.Dispose()}}
New-Item -ItemType Directory -Force (Join-Path $dir 'ps-buttons') | Out-Null
