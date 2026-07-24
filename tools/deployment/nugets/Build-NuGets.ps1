[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory,

    [switch]$NoRestore,

    [switch]$NoBuild,

    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z.+-]*$')]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\packages'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$solutions = @(
    Get-ChildItem -LiteralPath $repositoryRoot -File |
        Where-Object { $_.Extension -in @('.sln', '.slnx') } |
        Sort-Object Name
)
if ($solutions.Count -ne 1) {
    throw "Expected one canonical root solution; found $($solutions.Count)."
}

. (Join-Path $PSScriptRoot 'Get-FileToolsPackageManifest.ps1')
$packages = @(Get-FileToolsPackageManifest)
if ($packages.Count -ne 8) {
    throw "The FileTools package manifest must contain exactly eight packages; found $($packages.Count)."
}

$duplicateIds = @($packages | Group-Object Id | Where-Object Count -gt 1)
if ($duplicateIds.Count -ne 0) {
    throw 'The FileTools package manifest contains duplicate package IDs.'
}

$sourceRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src'))
$sourcePrefix = $sourceRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

foreach ($package in $packages) {
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $package.Project))
    if (-not $projectPath.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Packable project '$projectPath' must be below '$sourceRoot'."
    }

    if (-not [System.IO.File]::Exists($projectPath)) {
        throw "Packable project '$projectPath' does not exist."
    }
}

$operation = if ($NoRestore) {
    'Pack'
}
else {
    'Restore and pack'
}

if (-not $PSCmdlet.ShouldProcess(
        $OutputDirectory,
        "$operation $($packages.Count) FileTools NuGet packages from '$($solutions[0].Name)'"
    )) {
    [pscustomobject]@{
        Repository = Split-Path $repositoryRoot -Leaf
        Solution = $solutions[0].Name
        Configuration = $Configuration
        OutputDirectory = $OutputDirectory
        PackageCount = $packages.Count
        Status = 'Preview'
    }
    return
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if (-not $NoRestore) {
    & dotnet restore $solutions[0].FullName --configfile (Join-Path $repositoryRoot 'NuGet.config')
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }
}

foreach ($package in $packages) {
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $package.Project))
    $arguments = @(
        'pack'
        $projectPath
        '--configuration'
        $Configuration
        '--no-restore'
        '--output'
        $OutputDirectory
        '-p:ContinuousIntegrationBuild=true'
    )
    if ($NoBuild) {
        $arguments += '--no-build'
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $arguments += "-p:PackageVersion=$Version"
    }

    Write-Host "Packing $($package.Id)"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for '$($package.Id)' with exit code $LASTEXITCODE."
    }
}

[pscustomobject]@{
    Repository = Split-Path $repositoryRoot -Leaf
    Solution = $solutions[0].Name
    Configuration = $Configuration
    OutputDirectory = $OutputDirectory
    PackageCount = $packages.Count
    Status = 'Succeeded'
}
