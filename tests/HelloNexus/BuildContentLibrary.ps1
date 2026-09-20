$nap = Join-Path $PSScriptRoot "..\..\src\AssetPipeline\bin\Debug\net10.0\nap.exe"
$input = Join-Path $PSScriptRoot "..\..\.assets\nap.yaml"
$output = Join-Path $PSScriptRoot ".content"

& $nap --output $output clean
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $nap --output $output build --input $input
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }