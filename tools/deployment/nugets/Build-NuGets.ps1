[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory,

    [switch]$NoRestore,

    [switch]$NoBuild,

    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z.+-]*$')]
    [string]$Version = '',

    [switch]$CreateRunDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$globalJsonPath = Join-Path $repositoryRoot 'global.json'
if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
    throw "global.json was not found at '$globalJsonPath'."
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$FailureMessage
    )

    Push-Location -LiteralPath $repositoryRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FailureMessage Exit code: $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

$effectiveVersion = $Version.Trim()
if ([string]::IsNullOrWhiteSpace($effectiveVersion)) {
    [xml]$directoryBuildProps = Get-Content -LiteralPath (
        Join-Path $repositoryRoot 'Directory.Build.props'
    ) -Raw
    $versionNode = $directoryBuildProps.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw 'Directory.Build.props must define the committed package Version.'
    }
    $effectiveVersion = $versionNode.InnerText.Trim()
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $outputRoot = Join-Path $repositoryRoot 'artifacts\packages'
    $createRunDirectory = $true
}
elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $outputRoot = $OutputDirectory
    $createRunDirectory = $CreateRunDirectory.IsPresent
}
else {
    $outputRoot = Join-Path $repositoryRoot $OutputDirectory
    $createRunDirectory = $CreateRunDirectory.IsPresent
}
if ($createRunDirectory) {
    $runTimestamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
    $OutputDirectory = Join-Path $outputRoot "${effectiveVersion}_$runTimestamp"
}
else {
    $OutputDirectory = $outputRoot
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
        "$operation $($packages.Count) FileTools NuGet packages at version '$effectiveVersion' from '$($solutions[0].Name)'"
    )) {
    [pscustomobject]@{
        Repository = Split-Path $repositoryRoot -Leaf
        Solution = $solutions[0].Name
        Configuration = $Configuration
        PackageVersion = $effectiveVersion
        OutputDirectory = $OutputDirectory
        PackageCount = $packages.Count
        Status = 'Preview'
    }
    return
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if (-not $NoRestore) {
    $restoreArguments = @(
        'restore'
        $solutions[0].FullName
        '--configfile'
        (Join-Path $repositoryRoot 'NuGet.config')
    )
    Invoke-DotNet `
        -Arguments $restoreArguments `
        -FailureMessage 'dotnet restore failed.'
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

    $arguments += "-p:PackageVersion=$effectiveVersion"

    Write-Host "Packing $($package.Id)"
    Invoke-DotNet `
        -Arguments $arguments `
        -FailureMessage "dotnet pack failed for '$($package.Id)'."
}

[pscustomobject]@{
    Repository = Split-Path $repositoryRoot -Leaf
    Solution = $solutions[0].Name
    Configuration = $Configuration
    PackageVersion = $effectiveVersion
    OutputDirectory = $OutputDirectory
    PackageCount = $packages.Count
    Status = 'Succeeded'
}
