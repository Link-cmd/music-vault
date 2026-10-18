$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
 $bitmap = [Drawing.Bitmap]::new($size,$size)
 $g = [Drawing.Graphics]::FromImage($bitmap)
 $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.ScaleTransform($size/256.0,$size/256.0)
 $g.Clear([Drawing.Color]::Transparent)
 $paper=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FAFAF8'))
 $ink=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#252B27'))
 $accent=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#DC9267'))
 $line=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#8E9B90'),3)
 $g.FillRectangle($ink,12,12,232,232)
 $g.FillRectangle($paper,18,18,220,220)
 $g.FillRectangle($ink,18,18,220,28)
 $g.FillRectangle($paper,28,28,8,8)
 $g.FillRectangle($paper,208,29,19,5)
 $g.FillEllipse($ink,49,61,158,158)
 foreach($diameter in @(130,108,86)) { $g.DrawEllipse($line,128-$diameter/2,140-$diameter/2,$diameter,$diameter) }
 $g.FillEllipse($paper,104,116,48,48)
 $g.FillEllipse($ink,122,134,12,12)
 $g.FillRectangle($accent,197,183,33,44)
 $triangle=[Drawing.PointF[]]@([Drawing.PointF]::new(205,193),[Drawing.PointF]::new(205,217),[Drawing.PointF]::new(222,205))
 $g.FillPolygon($ink,$triangle)
 $memory=[IO.MemoryStream]::new()
 $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
 $frames += ,@($size,$memory.ToArray())
 if($size -eq 256) { $bitmap.Save((Join-Path $PSScriptRoot 'vault-icon.png'),[Drawing.Imaging.ImageFormat]::Png) }
 $memory.Dispose();$g.Dispose();$bitmap.Dispose();$paper.Dispose();$ink.Dispose();$accent.Dispose();$line.Dispose()
}
$stream=[IO.File]::Create((Join-Path $PSScriptRoot 'vault.ico'))
$writer=[IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count
foreach($frame in $frames) {
 $dimension=if($frame[0] -eq 256){0}else{$frame[0]}
 $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
 $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame[1].Length);$writer.Write([uint32]$offset)
 $offset += $frame[1].Length
}
foreach($frame in $frames){$writer.Write([byte[]]$frame[1])}
$writer.Dispose()
