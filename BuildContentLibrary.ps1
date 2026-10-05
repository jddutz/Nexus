$nap = Join-Path $PSScriptRoot "src\AssetPipeline\bin\Debug\net10.0\nap.exe"
$i = ".assets\nap.yaml"
$o = ".content"

& $nap --output $o clean
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $nap --output $o build --input $i
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }