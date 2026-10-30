<#
.SYNOPSIS
Builds a single Nexus.GameEngine NuGet package and writes it to a local feed.
.PARAMETER Version
The NuGet package version. Increment this when publishing updated engine builds.
.PARAMETER OutputDirectory
The local NuGet feed directory. Existing Nexus.GameEngine packages are removed first.
.EXAMPLE
.\publish.ps1 -Version 1.0.1
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $Version = '1.0.0',

    [ValidateNotNullOrEmpty()]
    [string] $OutputDirectory = '..\..\LocalNuGetRepo'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\Runtime\Nexus.Runtime.csproj'
$framework = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.TargetFramework
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "Nexus.GameEngine-$([guid]::NewGuid())"

try {
    $null = New-Item -ItemType Directory -Path $output -Force
    Get-ChildItem -LiteralPath $output -File |
    Where-Object { $_.Name -match '^Nexus\.GameEngine\.\d.*\.(?:nupkg|snupkg)$' } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName }

    $null = New-Item -ItemType Directory -Path $staging
    $publishDirectory = Join-Path $staging 'publish'

    & dotnet publish $project --configuration Release --output $publishDirectory --self-contained false
    if ($LASTEXITCODE -ne 0) {
        throw "Engine build failed with exit code $LASTEXITCODE."
    }

    # Use the resolved runtime graph so bundled engine projects never become package dependencies.
    $dependencyGraph = Get-Content -LiteralPath (Join-Path $publishDirectory 'Nexus.Runtime.deps.json') -Raw |
    ConvertFrom-Json
    $dependencies = foreach ($library in $dependencyGraph.libraries.PSObject.Properties | Sort-Object Name) {
        if ($library.Value.type -eq 'package') {
            $id, $resolvedVersion = $library.Name -split '/', 2
            $escapedId = [System.Security.SecurityElement]::Escape($id)
            $escapedVersion = [System.Security.SecurityElement]::Escape($resolvedVersion)
            "        <dependency id=`"$escapedId`" version=`"$escapedVersion`" />"
        }
    }

    $generator = Join-Path $PSScriptRoot 'codegen\bin\Release\netstandard2.0\Nexus.SourceGenerators.dll'
    if (-not (Test-Path -LiteralPath $generator -PathType Leaf)) {
        throw "Source generator output not found: $generator"
    }

    Copy-Item -LiteralPath $generator -Destination $staging
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $staging

    $escapedPackageVersion = [System.Security.SecurityElement]::Escape($Version)
    $nuspec = Join-Path $staging 'Nexus.GameEngine.nuspec'
    @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>Nexus.GameEngine</id>
    <version>$escapedPackageVersion</version>
    <authors>Nexus</authors>
    <description>Nexus game engine runtime and engine assemblies.</description>
    <readme>README.md</readme>
    <license type="file">LICENSE</license>
    <dependencies>
      <group targetFramework="$framework">
$($dependencies -join [Environment]::NewLine)
      </group>
    </dependencies>
  </metadata>
  <files>
    <file src="publish\Nexus.*.dll" target="lib\$framework" />
    <file src="Nexus.SourceGenerators.dll" target="analyzers\dotnet\cs" />
    <file src="README.md" target="README.md" />
    <file src="LICENSE" target="LICENSE" />
  </files>
</package>
"@ | Set-Content -LiteralPath $nuspec -Encoding UTF8

    & dotnet pack $project --configuration Release --no-build --no-restore --output $output `
        "-p:NuspecFile=$nuspec" "-p:NuspecBasePath=$staging"
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet packaging failed with exit code $LASTEXITCODE."
    }

    Write-Host "Published Nexus.GameEngine $Version to $output"
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
