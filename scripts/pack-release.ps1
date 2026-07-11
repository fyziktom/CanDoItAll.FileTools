[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Configuration = 'Release',

    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = 'output/packages/release',

    [string]$Version,

    [switch]$NoBuild,

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'output'))
$requestedOutput = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

$outputPrefix = $outputRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $requestedOutput.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Package output must be below '$outputRoot'."
}

if (-not [string]::IsNullOrWhiteSpace($Version) -and $Version -notmatch '^[0-9A-Za-z][0-9A-Za-z.+-]*$') {
    throw "Version '$Version' is not a valid package-version argument."
}

. (Join-Path $PSScriptRoot 'package-manifest.ps1')
$packages = @(Get-FileToolsPackageManifest)
if ($packages.Count -ne 7) {
    throw "The release manifest must contain exactly seven packages; found $($packages.Count)."
}

[System.IO.Directory]::CreateDirectory($requestedOutput) | Out-Null

foreach ($package in $packages) {
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $package.Project))
    if (-not [System.IO.File]::Exists($projectPath)) {
        throw "Packable project '$projectPath' does not exist."
    }

    $arguments = @(
        'pack'
        $projectPath
        '--configuration'
        $Configuration
        '--output'
        $requestedOutput
    )
    if ($NoBuild) {
        $arguments += '--no-build'
    }

    if ($NoRestore) {
        $arguments += '--no-restore'
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $arguments += "--property:PackageVersion=$Version"
    }

    Write-Host "Packing $($package.Id)"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$($package.Id)' with exit code $LASTEXITCODE."
    }
}

Write-Host "Packed $($packages.Count) FileTools libraries into '$requestedOutput'."
