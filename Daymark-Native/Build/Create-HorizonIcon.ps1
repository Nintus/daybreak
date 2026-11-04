param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $background = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#7C83FF'))
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $outer = [Drawing.Drawing2D.GraphicsPath]::new()
    $sun = [Drawing.Drawing2D.GraphicsPath]::new()
    $region = $null
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size/64.0,$size/64.0)
        $tile = [Drawing.Drawing2D.GraphicsPath]::new()
        try {
            foreach ($arc in @(@(0,0,90,180),@(44,0,90,270),@(44,44,90,0),@(0,44,90,90))) { $tile.AddArc($arc[0],$arc[1],20,20,$arc[3],$arc[2]) }
            $tile.CloseFigure(); $graphics.FillPath($background,$tile)
        } finally { $tile.Dispose() }
        # Same 64-unit geometry as horizon-mark.svg, inset inside the app tile.
        $graphics.TranslateTransform(6,6); $graphics.ScaleTransform(.8,.8)
        $outer.AddLine(6,4,28,4); $outer.AddBezier(28,4,65,4,65,60,28,60); $outer.AddLine(28,60,6,60); $outer.CloseFigure()
        $sun.AddArc(14,16,36,36,180,180); $sun.CloseFigure()
        $region = [Drawing.Region]::new($outer); $region.Exclude($sun); $region.Exclude([Drawing.RectangleF]::new(6,34,52,2))
        $graphics.FillRegion($white,$region)
        $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        $frames += ,$stream.ToArray()
    } finally {
        if ($region) { $region.Dispose() }; $stream.Dispose(); $outer.Dispose(); $sun.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $background.Dispose(); $white.Dispose()
    }
}
$file = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath)); $writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count; $sizes = @(16,24,32,48,64,128,256)
    for ($i=0; $i -lt $frames.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset); $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
