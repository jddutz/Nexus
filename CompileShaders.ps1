# CompileShaders.ps1

param(
    [switch]$EnableShaderDebugPrintf
)

$ErrorActionPreference = "Stop"

$workspaceRoot = (git -C $PSScriptRoot rev-parse --show-toplevel 2>$null)

if (-not $workspaceRoot) {
    throw "Unable to locate the Nexus workspace root."
}

$workspaceRoot = $workspaceRoot.Trim()

$shaderDirectory = Join-Path $workspaceRoot "src/Vulkan/Shaders"
$outputDirectory = Join-Path $shaderDirectory "Compiled"
$glslc = Join-Path $env:VULKAN_SDK "Bin/glslc.exe"

if (-not (Test-Path $glslc)) {
    throw "glslc not found at '$glslc'. Ensure VULKAN_SDK is set correctly."
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

Write-Host "Workspace: $workspaceRoot"
Write-Host "Shaders:   $shaderDirectory"
Write-Host "Output:    $outputDirectory"
Write-Host ""

$shaderCompileOptions = @()
if ($EnableShaderDebugPrintf) {
    $shaderCompileOptions += "-DNEXUS_SHADER_DEBUG_PRINTF=1"
}

Get-ChildItem -Path $shaderDirectory -Filter "*.vert" | ForEach-Object {
    $output = Join-Path $outputDirectory "$($_.Name).spv"

    Write-Host "Compiling $($_.Name)..."
    & $glslc @shaderCompileOptions $_.FullName -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to compile '$($_.Name)'."
    }
}

Get-ChildItem -Path $shaderDirectory -Filter "*.frag" | ForEach-Object {
    $output = Join-Path $outputDirectory "$($_.Name).spv"

    Write-Host "Compiling $($_.Name)..."
    & $glslc @shaderCompileOptions $_.FullName -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to compile '$($_.Name)'."
    }
}

Write-Host ""
Write-Host "Shader compilation complete!"