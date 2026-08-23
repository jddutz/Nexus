<#
.SYNOPSIS
    Removes "GameEngine" from folder names and project (.csproj) file names
    under src/ and tests/, and updates the one remaining source comment and
    the tests namespace that still reference the old naming.

.DESCRIPTION
    Namespaces in .cs files already use the short "Nexus.X" form, and
    ProjectReference paths in .csproj files already point at the target
    (post-rename) paths. What's left is physically renaming the folders and
    .csproj files (and their generated bin/obj artifacts, which are deleted
    and will be regenerated on next build) to match.

    Run with -WhatIf to preview the renames without changing anything.

.PARAMETER WhatIf
    Preview the actions without making changes.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# Map: old folder name (under src/) -> new folder name
$srcFolderRenames = [ordered]@{
    'Nexus.GameEngine.Audio'           = 'Nexus.Audio'
    'Nexus.GameEngine.Content'         = 'Nexus.Content'
    'Nexus.GameEngine.Core'            = 'Nexus.Core'
    'Nexus.GameEngine.Graphics.Vulkan' = 'Nexus.Graphics.Vulkan'
    'Nexus.GameEngine.Graphics'        = 'Nexus.Graphics'
    'Nexus.GameEngine.GUI'             = 'Nexus.GUI'
    'Nexus.GameEngine.Input'           = 'Nexus.Input'
    'Nexus.GameEngine.IO'              = 'Nexus.IO'
    'Nexus.GameEngine.Network'         = 'Nexus.Network'
    'Nexus.GameEngine.Physics'         = 'Nexus.Physics'
    'Nexus.GameEngine.Platform'        = 'Nexus.Platform'
    'Nexus.GameEngine.Runtime'         = 'Nexus.Runtime'
    'Nexus.GameEngine.Scenes'          = 'Nexus.Scenes'
    'Nexus.GameEngine.Testing'         = 'Nexus.Testing'
}

# Map: old .csproj file name -> new .csproj file name, keyed by the *new* folder name
$csprojRenames = [ordered]@{
    'Nexus.Audio'           = @{ Old = 'Nexus.GameEngine.Audio.csproj'; New = 'Nexus.Audio.csproj' }
    'Nexus.Content'         = @{ Old = 'Nexus.GameEngine.Content.csproj'; New = 'Nexus.Content.csproj' }
    'Nexus.Core'            = @{ Old = 'Nexus.GameEngine.Core.csproj'; New = 'Nexus.Core.csproj' }
    'Nexus.Graphics.Vulkan' = @{ Old = 'Nexus.GameEngine.Graphics.Vulkan.csproj'; New = 'Nexus.Graphics.Vulkan.csproj' }
    'Nexus.Graphics'        = @{ Old = 'Nexus.GameEngine.Graphics.csproj'; New = 'Nexus.Graphics.csproj' }
    'Nexus.GUI'             = @{ Old = 'Nexus.GameEngine.GUI.csproj'; New = 'Nexus.GUI.csproj' }
    'Nexus.Input'           = @{ Old = 'Nexus.GameEngine.Input.csproj'; New = 'Nexus.Input.csproj' }
    'Nexus.IO'              = @{ Old = 'Nexus.GameEngine.IO.csproj'; New = 'Nexus.IO.csproj' }
    'Nexus.Network'         = @{ Old = 'Nexus.GameEngine.Network.csproj'; New = 'Nexus.Network.csproj' }
    'Nexus.Physics'         = @{ Old = 'Nexus.GameEngine.Physics.csproj'; New = 'Nexus.Physics.csproj' }
    'Nexus.Runtime'         = @{ Old = 'Nexus.GameEngine.Runtime.csproj'; New = 'Nexus.Runtime.csproj' }
    'Nexus.Scenes'          = @{ Old = 'Nexus.GameEngine.SceneGraph.csproj'; New = 'Nexus.Scenes.csproj' }
    'Nexus.Testing'         = @{ Old = 'Nexus.GameEngine.Testing.csproj'; New = 'Nexus.Testing.csproj' }
}

function Remove-BuildArtifacts {
    param([string]$FolderPath)
    foreach ($sub in @('bin', 'obj')) {
        $path = Join-Path $FolderPath $sub
        if (Test-Path $path) {
            if ($PSCmdlet.ShouldProcess($path, 'Remove build artifacts')) {
                Remove-Item -Path $path -Recurse -Force
            }
        }
    }
}

# --- src/ folder + csproj renames ---
$srcRoot = Join-Path $repoRoot 'src'
foreach ($oldName in $srcFolderRenames.Keys) {
    $newName = $srcFolderRenames[$oldName]
    $oldPath = Join-Path $srcRoot $oldName
    $newPath = Join-Path $srcRoot $newName

    if (-not (Test-Path $oldPath)) {
        Write-Warning "Skipping missing folder: $oldPath"
        continue
    }

    Remove-BuildArtifacts -FolderPath $oldPath

    if ($PSCmdlet.ShouldProcess($oldPath, "Rename folder to $newName")) {
        Rename-Item -Path $oldPath -NewName $newName
    }

    if ($csprojRenames.Contains($newName)) {
        $rename = $csprojRenames[$newName]
        $oldCsproj = Join-Path $newPath $rename.Old
        $newCsproj = Join-Path $newPath $rename.New
        if (Test-Path $oldCsproj) {
            if ($PSCmdlet.ShouldProcess($oldCsproj, "Rename project file to $($rename.New)")) {
                Rename-Item -Path $oldCsproj -NewName $rename.New
            }
        }
        elseif ($PSCmdlet.ShouldProcess($newPath, 'WhatIf: project file rename (folder not yet renamed)')) {
            # Under -WhatIf the folder rename above was simulated only, so the
            # csproj still lives under $oldPath - report the intended action.
            Write-Host "What if: Rename '$oldName\$($rename.Old)' to '$($rename.New)'"
        }
    }
}

# --- tests/GameEngineTests -> tests/Tests ---
$testsRoot = Join-Path $repoRoot 'tests'
$oldTestsPath = Join-Path $testsRoot 'GameEngineTests'
$newTestsPath = Join-Path $testsRoot 'Tests'

if (Test-Path $oldTestsPath) {
    Remove-BuildArtifacts -FolderPath $oldTestsPath

    if ($PSCmdlet.ShouldProcess($oldTestsPath, 'Rename folder to Tests')) {
        Rename-Item -Path $oldTestsPath -NewName 'Tests'
    }

    $oldTestsCsproj = Join-Path $newTestsPath 'GameEngineTests.csproj'
    $newTestsCsproj = Join-Path $newTestsPath 'Tests.csproj'
    if (Test-Path $oldTestsCsproj) {
        if ($PSCmdlet.ShouldProcess($oldTestsCsproj, 'Rename project file to Tests.csproj')) {
            Rename-Item -Path $oldTestsCsproj -NewName 'Tests.csproj'
        }
    }

    # Update the namespace declaration in the test source files.
    Get-ChildItem -Path $newTestsPath -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $content = Get-Content -Path $_.FullName -Raw
        if ($content -match 'namespace GameEngineTests;?') {
            $updated = $content -replace 'namespace GameEngineTests;', 'namespace Tests;'
            if ($PSCmdlet.ShouldProcess($_.FullName, 'Update namespace to Tests')) {
                Set-Content -Path $_.FullName -Value $updated -NoNewline
            }
        }
    }
}
else {
    Write-Warning "Skipping missing folder: $oldTestsPath"
}

# --- Fix stray comment referencing the old assembly naming ---
$pipelineManager = Join-Path $srcRoot 'Nexus.Graphics\Pipelines\PipelineManager.cs'
if (Test-Path $pipelineManager) {
    $content = Get-Content -Path $pipelineManager -Raw
    $needle = 'Loads shaders from embedded resources in the GameEngine assembly.'
    if ($content.Contains($needle)) {
        $updated = $content.Replace($needle, 'Loads shaders from embedded resources in the assembly.')
        if ($PSCmdlet.ShouldProcess($pipelineManager, 'Update stray comment')) {
            Set-Content -Path $pipelineManager -Value $updated -NoNewline
        }
    }
}

# --- Update Nexus.slnx references ---
$slnx = Join-Path $repoRoot 'Nexus.slnx'
if (Test-Path $slnx) {
    $content = Get-Content -Path $slnx -Raw
    $updated = $content `
        -replace [regex]::Escape('src/Nexus.Scenes/Nexus.SceneGraph.csproj'), 'src/Nexus.Scenes/Nexus.Scenes.csproj' `
        -replace [regex]::Escape('tests/GameEngineTests/GameEngineTests.csproj'), 'tests/Tests/Tests.csproj'
    if ($updated -ne $content) {
        if ($PSCmdlet.ShouldProcess($slnx, 'Update project paths')) {
            Set-Content -Path $slnx -Value $updated -NoNewline
        }
    }
}

Write-Host "Done. Run 'dotnet build' to verify, then 'git add -A' to stage the renames." -ForegroundColor Green
