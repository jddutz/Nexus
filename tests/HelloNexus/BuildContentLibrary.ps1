$nap = Join-Path $PSScriptRoot "..\..\src\AssetPipeline\bin\Debug\net10.0\nap.exe"
$i = Join-Path $PSScriptRoot "..\..\.assets\nap.yaml"
$o = Join-Path $PSScriptRoot ".content"
$nativeLibrary = Join-Path (Split-Path -Parent $nap) "nexus_font_native.dll"

if (-not (Test-Path $nativeLibrary -PathType Leaf)) {
    throw "Missing native font rasterizer '$nativeLibrary'. Build and install src\AssetPipeline\native for the current Windows runtime before running this script."
}

& $nap --output $o clean
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $nap --output $o build --input $i
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }