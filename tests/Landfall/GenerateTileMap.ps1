$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$tileSize = 128
$gridSize = 8
$outputPath = Join-Path $PSScriptRoot ".assets\tiles\terrain-tile-map.png"

$hexColors = @(
    "FFFFFF"
    "567D46", "668B4F", "789858", "8AA666", "9BB473", "A8BE7B", "496B3C", "3F754A"
    "5E8B57", "729B62", "83A86D", "92B57A", "A1C187", "527A42", "6D914B", "87965C"
    "654B32", "76563A", "866342", "96704A", "A27A50", "B18A5A", "C19A68", "563E2B"
    "76604A", "8C7155", "A18463", "B39470", "C2A17A", "D0B18B", "E0C39C", "B8A88A"
    "555A5B", "686D6E", "797D7D", "898C8B", "9A9C99", "AAABA7", "B9B9B4", "D0CFC8"
    "81704A", "958052", "A88F59", "BA9F62", "C9AF70", "D8C17F", "E5D194", "A9783B"
    "315C78", "396D8A", "427F9B", "4C91AA", "57A2B8", "68B2C4", "7CC2D0", "244A66"
    "477E9B", "5B91A9", "74A5B8", "8BB9C8", "A1CAD5", "3A7185", "598B94"
)

if ($hexColors.Count -ne $gridSize * $gridSize) {
    throw "Expected $($gridSize * $gridSize) tile colors, found $($hexColors.Count)."
}

if (($hexColors | Select-Object -Unique).Count -ne $hexColors.Count) {
    throw "Tile colors must all be unique."
}

$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$bitmap = [System.Drawing.Bitmap]::new($tileSize * $gridSize, $tileSize * $gridSize)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try {
    for ($index = 0; $index -lt $hexColors.Count; $index++) {
        $color = [System.Drawing.ColorTranslator]::FromHtml("#$($hexColors[$index])")
        $brush = [System.Drawing.SolidBrush]::new($color)
        try {
            $x = ($index % $gridSize) * $tileSize
            $y = [Math]::Floor($index / $gridSize) * $tileSize
            $graphics.FillRectangle($brush, $x, $y, $tileSize, $tileSize)
        }
        finally {
            $brush.Dispose()
        }
    }

    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "Generated $($gridSize * $gridSize) unique tiles at $outputPath"
