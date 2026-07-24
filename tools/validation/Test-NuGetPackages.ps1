[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$PackageDirectory = 'artifacts/packages',

    [ValidateNotNullOrEmpty()]
    [string]$HashOutput = 'artifacts/package-validation/package-hashes.sha256',

    [string]$ExpectedHashesPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$artifactsPrefix = $artifactsRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

function Resolve-ArtifactPath([string]$Path, [string]$Label) {
    $resolved = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
    }

    if (-not $resolved.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must be below '$artifactsRoot'."
    }

    return $resolved
}

function Assert-EqualSet(
    [string]$Label,
    [AllowEmptyCollection()][string[]]$Actual,
    [AllowEmptyCollection()][string[]]$Expected) {
    $actualValues = @($Actual | Sort-Object -Unique)
    $expectedValues = @($Expected | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $expectedValues -DifferenceObject $actualValues)
    if ($difference.Count -ne 0) {
        $actualText = if ($actualValues.Count -eq 0) { '<none>' } else { $actualValues -join ', ' }
        $expectedText = if ($expectedValues.Count -eq 0) { '<none>' } else { $expectedValues -join ', ' }
        throw "$Label mismatch. Expected: $expectedText. Actual: $actualText."
    }
}

function Get-XmlNodeText([System.Xml.XmlNode]$Root, [string]$XPath) {
    $node = $Root.SelectSingleNode($XPath)
    if ($null -eq $node) {
        return $null
    }

    return $node.InnerText.Trim()
}

function Read-Nuspec([System.IO.Compression.ZipArchive]$Archive, [string]$PackagePath) {
    $entries = @($Archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) })
    if ($entries.Count -ne 1) {
        throw "Package '$PackagePath' must contain exactly one nuspec; found $($entries.Count)."
    }

    $reader = [System.IO.StreamReader]::new($entries[0].Open())
    try {
        return [xml]$reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}

function Assert-ProjectMetadata([pscustomobject]$Package) {
    $projectPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Package.Project))
    if (-not [System.IO.File]::Exists($projectPath)) {
        throw "Project '$projectPath' does not exist."
    }

    [xml]$project = [System.IO.File]::ReadAllText($projectPath)
    $assemblyName = Get-XmlNodeText $project "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='AssemblyName']"
    if ($assemblyName -ne $Package.Assembly) {
        throw "Project '$($Package.Id)' has assembly '$assemblyName'; expected '$($Package.Assembly)'."
    }

    $isPackable = Get-XmlNodeText $project "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='IsPackable']"
    if ($isPackable -ne 'true') {
        throw "Project '$($Package.Id)' must set IsPackable to true."
    }

    $projectDirectory = [System.IO.Path]::GetDirectoryName($projectPath)
    $actualProjectReferences = @(
        $project.SelectNodes("/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='ProjectReference']") |
            ForEach-Object {
                $include = $_.GetAttribute('Include')
                $resolvedReference = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $include))
                if (-not $resolvedReference.StartsWith(
                    ([System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src')) + [System.IO.Path]::DirectorySeparatorChar),
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Project '$($Package.Id)' references a project outside src: '$include'."
                }

                [System.IO.Path]::GetFileNameWithoutExtension($resolvedReference)
            }
    )
    Assert-EqualSet "Project references for $($Package.Id)" $actualProjectReferences $Package.ProjectReferences

    $actualPackageReferences = @(
        $project.SelectNodes("/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='PackageReference']") |
            ForEach-Object { $_.GetAttribute('Include') }
    )
    Assert-EqualSet "Package references for $($Package.Id)" $actualPackageReferences $Package.PackageReferences

    $frameworkReferences = @(
        $project.SelectNodes("/*[local-name()='Project']/*[local-name()='ItemGroup']/*[local-name()='FrameworkReference']") |
            ForEach-Object { $_.GetAttribute('Include') }
    )
    $expectedFrameworkReferences = if ($Package.IsRazorClassLibrary) { @('Microsoft.AspNetCore.App') } else { @() }
    Assert-EqualSet "Framework references for $($Package.Id)" $frameworkReferences $expectedFrameworkReferences

    $projectText = [System.IO.File]::ReadAllText($projectPath)
    if ($projectText -match 'CanDoItAll\.Components' -or $projectText -match 'CanDoItAll(?!\.FileTools)[\\/]') {
        throw "Project '$($Package.Id)' contains a forbidden Components or main-application dependency."
    }
}

. (Join-Path $repositoryRoot 'tools\deployment\nugets\Get-FileToolsPackageManifest.ps1')
$packages = @(Get-FileToolsPackageManifest)
if ($packages.Count -ne 8) {
    throw "The validation manifest must contain exactly eight packages; found $($packages.Count)."
}

$duplicateIds = @($packages | Group-Object Id | Where-Object Count -gt 1)
if ($duplicateIds.Count -ne 0) {
    throw "The validation manifest contains duplicate package IDs."
}

$buildPropsPath = Join-Path $repositoryRoot 'Directory.Build.props'
[xml]$buildProps = [System.IO.File]::ReadAllText($buildPropsPath)
$requiredBuildProperties = [ordered]@{
    TargetFramework = 'net10.0'
    Deterministic = 'true'
    GenerateDocumentationFile = 'true'
    Authors = 'CanDoItAll'
    PackageProjectUrl = 'https://aicandoitall.com'
    RepositoryUrl = 'https://github.com/fyziktom/CanDoItAll.FileTools'
    RepositoryType = 'git'
    PublishRepositoryUrl = 'true'
    IncludeSymbols = 'true'
    SymbolPackageFormat = 'snupkg'
}
foreach ($property in $requiredBuildProperties.GetEnumerator()) {
    $actualValue = Get-XmlNodeText $buildProps "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='$($property.Key)']"
    if ($actualValue -ne $property.Value) {
        throw "Directory.Build.props must set $($property.Key) to '$($property.Value)'; found '$actualValue'."
    }
}

$licenseExpression = Get-XmlNodeText $buildProps "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='PackageLicenseExpression']"
if (-not [string]::IsNullOrWhiteSpace($licenseExpression)) {
    throw "Directory.Build.props must not use PackageLicenseExpression for the modified MIT-derived license."
}

$repositoryLicensePath = Join-Path $repositoryRoot 'LICENSE'
$repositoryLicenseText = [System.IO.File]::ReadAllText($repositoryLicensePath)
if ($repositoryLicenseText -notmatch 'MIT-Derived License with Source Link Requirement' -or
    $repositoryLicenseText -notmatch [regex]::Escape('https://github.com/fyziktom/CanDoItAll.FileTools')) {
    throw "The repository LICENSE does not contain the required source-link contract."
}

$packagePath = Resolve-ArtifactPath $PackageDirectory 'PackageDirectory'
$hashPath = Resolve-ArtifactPath $HashOutput 'HashOutput'
if (-not [System.IO.Directory]::Exists($packagePath)) {
    throw "Package directory '$packagePath' does not exist."
}

$expectedHashLines = $null
if (-not [string]::IsNullOrWhiteSpace($ExpectedHashesPath)) {
    $expectedHashPath = Resolve-ArtifactPath $ExpectedHashesPath 'ExpectedHashesPath'
    if (-not [System.IO.File]::Exists($expectedHashPath)) {
        throw "Expected hash manifest '$expectedHashPath' does not exist."
    }

    $expectedHashLines = @(
        [System.IO.File]::ReadAllLines($expectedHashPath) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

$nupkgs = @(Get-ChildItem -LiteralPath $packagePath -File -Filter '*.nupkg')
$snupkgs = @(Get-ChildItem -LiteralPath $packagePath -File -Filter '*.snupkg')
if ($nupkgs.Count -ne $packages.Count) {
    throw "Expected $($packages.Count) nupkg files in '$packagePath'; found $($nupkgs.Count)."
}

if ($snupkgs.Count -ne $packages.Count) {
    throw "Expected $($packages.Count) snupkg files in '$packagePath'; found $($snupkgs.Count)."
}

$manifestIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($package in $packages) {
    $manifestIds.Add($package.Id) | Out-Null
}

$packageVersions = [System.Collections.Generic.Dictionary[string, string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($nupkg in $nupkgs) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
    try {
        [xml]$nuspec = Read-Nuspec $archive $nupkg.FullName
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) {
            throw "Package '$($nupkg.Name)' has no metadata node."
        }

        $id = Get-XmlNodeText $metadata "*[local-name()='id']"
        $version = Get-XmlNodeText $metadata "*[local-name()='version']"
        if (-not $manifestIds.Contains($id)) {
            throw "Package '$($nupkg.Name)' has unexpected ID '$id'."
        }

        if ([string]::IsNullOrWhiteSpace($version)) {
            throw "Package '$id' has no version."
        }

        if ($packageVersions.ContainsKey($id)) {
            throw "More than one package declares ID '$id'."
        }

        $packageVersions.Add($id, $version)
    }
    finally {
        $archive.Dispose()
    }
}

$seenNupkgs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$seenSnupkgs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($package in $packages) {
    Assert-ProjectMetadata $package

    $matches = @($nupkgs | Where-Object { $_.Name.StartsWith("$($package.Id).", [System.StringComparison]::OrdinalIgnoreCase) })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one nupkg for '$($package.Id)'; found $($matches.Count)."
    }

    $nupkg = $matches[0]
    $archive = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
    try {
        [xml]$nuspec = Read-Nuspec $archive $nupkg.FullName
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) {
            throw "Package '$($nupkg.Name)' has no metadata node."
        }

        $id = Get-XmlNodeText $metadata "*[local-name()='id']"
        $version = Get-XmlNodeText $metadata "*[local-name()='version']"
        $authors = Get-XmlNodeText $metadata "*[local-name()='authors']"
        $license = $metadata.SelectSingleNode("*[local-name()='license']")
        $readme = Get-XmlNodeText $metadata "*[local-name()='readme']"
        $projectUrl = Get-XmlNodeText $metadata "*[local-name()='projectUrl']"
        $repository = $metadata.SelectSingleNode("*[local-name()='repository']")
        if ($id -ne $package.Id) {
            throw "Package '$($nupkg.Name)' has ID '$id'; expected '$($package.Id)'."
        }

        if ([string]::IsNullOrWhiteSpace($version)) {
            throw "Package '$id' has no version."
        }

        if ($version -ne $packageVersions[$id]) {
            throw "Package '$id' changed version while the package set was being inspected."
        }

        if ($authors -ne 'CanDoItAll') {
            throw "Package '$id' must carry CanDoItAll authorship."
        }

        if ($null -eq $license -or
            $license.GetAttribute('type') -ne 'file' -or
            $license.InnerText.Trim() -ne 'LICENSE') {
            throw "Package '$id' must declare the repository LICENSE as a file license."
        }

        if ($readme -ne 'README.md') {
            throw "Package '$id' must declare README.md as its package readme."
        }

        if ([string]::IsNullOrWhiteSpace($projectUrl) -or
            $projectUrl.TrimEnd('/') -ne 'https://aicandoitall.com') {
            throw "Package '$id' must use 'https://aicandoitall.com' as its project URL."
        }

        if ($null -eq $repository -or
            $repository.GetAttribute('type') -ne 'git' -or
            $repository.GetAttribute('url') -ne 'https://github.com/fyziktom/CanDoItAll.FileTools') {
            throw "Package '$id' must publish its canonical Git repository metadata."
        }

        $expectedNupkgName = "$id.$version.nupkg"
        if ($nupkg.Name -cne $expectedNupkgName) {
            throw "Package file '$($nupkg.Name)' must be named '$expectedNupkgName'."
        }

        if (-not $seenNupkgs.Add($nupkg.FullName)) {
            throw "Package '$($nupkg.Name)' matched more than one manifest entry."
        }

        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $assemblyEntry = "lib/net10.0/$($package.Assembly).dll"
        $documentationEntry = "lib/net10.0/$($package.Assembly).xml"
        if ($entryNames -cnotcontains $assemblyEntry -or $entryNames -cnotcontains $documentationEntry) {
            throw "Package '$id' must contain '$assemblyEntry' and '$documentationEntry'."
        }

        if ($entryNames -cnotcontains 'README.md') {
            throw "Package '$id' must contain README.md at the package root."
        }

        if ($entryNames -cnotcontains 'LICENSE') {
            throw "Package '$id' must contain LICENSE at the package root."
        }

        $licenseEntry = $archive.GetEntry('LICENSE')
        $licenseReader = [System.IO.StreamReader]::new($licenseEntry.Open())
        try {
            $licenseText = $licenseReader.ReadToEnd()
            if ($licenseText -cne $repositoryLicenseText) {
                throw "Package '$id' contains a LICENSE that differs from the repository LICENSE."
            }
        }
        finally {
            $licenseReader.Dispose()
        }

        $readmeEntry = $archive.GetEntry('README.md')
        $readmeReader = [System.IO.StreamReader]::new($readmeEntry.Open())
        try {
            $readmeText = $readmeReader.ReadToEnd()
            if ($readmeText -match '\]\((?!https?://|mailto:|#)[^)]+\)') {
                throw "Package '$id' contains a relative README link that will not resolve in package context."
            }
        }
        finally {
            $readmeReader.Dispose()
        }

        $dependencyNodes = @($metadata.SelectNodes("*[local-name()='dependencies']//*[local-name()='dependency']"))
        $dependencyIds = @($dependencyNodes | ForEach-Object { $_.GetAttribute('id') })
        $expectedDependencies = @($package.ProjectReferences) + @($package.PackageReferences)
        Assert-EqualSet "Packed dependencies for $id" $dependencyIds $expectedDependencies
        foreach ($dependencyNode in $dependencyNodes) {
            $dependencyId = $dependencyNode.GetAttribute('id')
            $isInternalDependency = $dependencyId.StartsWith(
                'CanDoItAll.FileTools.',
                [System.StringComparison]::OrdinalIgnoreCase)
            if ($isInternalDependency) {
                if (-not $packageVersions.ContainsKey($dependencyId)) {
                    throw "Package '$id' references internal package '$dependencyId' that is absent from the package set."
                }

                $expectedDependencyVersion = $packageVersions[$dependencyId]
                if ($dependencyNode.GetAttribute('version') -ne $expectedDependencyVersion) {
                    throw "Package '$id' must reference internal dependency '$dependencyId' at version '$expectedDependencyVersion'."
                }
            }
        }

        foreach ($dependencyId in $dependencyIds) {
            $isComponentsDependency = $dependencyId.StartsWith(
                'CanDoItAll.Components',
                [System.StringComparison]::OrdinalIgnoreCase)
            $isForeignCanDoItAllDependency = $dependencyId.StartsWith(
                'CanDoItAll.',
                [System.StringComparison]::OrdinalIgnoreCase) -and -not $dependencyId.StartsWith(
                    'CanDoItAll.FileTools.',
                    [System.StringComparison]::OrdinalIgnoreCase)
            if ($isComponentsDependency -or $isForeignCanDoItAllDependency) {
                throw "Package '$id' contains forbidden dependency '$dependencyId'."
            }

            if ($dependencyId -eq 'Markdig' -and $id -ne 'CanDoItAll.FileTools.FileInteraction.Markdown') {
                throw "Only the Markdown package may depend on Markdig."
            }
        }

        $hasAssetMetadata = $entryNames -ccontains 'build/Microsoft.AspNetCore.StaticWebAssets.props'
        $hasEndpointMetadata = $entryNames -ccontains 'build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props'
        $staticAssets = @($entryNames | Where-Object { $_.StartsWith('staticwebassets/', [System.StringComparison]::Ordinal) -and -not $_.EndsWith('/') })
        $isolatedCssBundles = @($staticAssets | Where-Object { $_.EndsWith('.bundle.scp.css', [System.StringComparison]::Ordinal) })
        if ($package.IsRazorClassLibrary) {
            if (-not $hasAssetMetadata -or -not $hasEndpointMetadata -or $staticAssets.Count -eq 0 -or $isolatedCssBundles.Count -eq 0) {
                throw "Razor class library '$id' is missing packed static-web-asset metadata, isolated CSS, or content."
            }

            $isFileInteractionComponents = $id -eq 'CanDoItAll.FileTools.FileInteraction.Components'
            $hasObjectUrlModule = $entryNames -ccontains 'staticwebassets/Components/FileObjectView.razor.js'
            if ($isFileInteractionComponents -and -not $hasObjectUrlModule) {
                throw "FileInteraction.Components is missing its collocated object-URL module."
            }
        }
        elseif ($hasAssetMetadata -or $hasEndpointMetadata -or $staticAssets.Count -ne 0) {
            throw "Non-RCL package '$id' unexpectedly contains static-web-asset output."
        }

        foreach ($entry in $archive.Entries | Where-Object {
            $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) -or
            $_.FullName.EndsWith('.props', [System.StringComparison]::OrdinalIgnoreCase) -or
            $_.FullName.EndsWith('.targets', [System.StringComparison]::OrdinalIgnoreCase) -or
            ($_.FullName.StartsWith('staticwebassets/', [System.StringComparison]::OrdinalIgnoreCase) -and
                ($_.FullName.EndsWith('.css', [System.StringComparison]::OrdinalIgnoreCase) -or
                    $_.FullName.EndsWith('.js', [System.StringComparison]::OrdinalIgnoreCase) -or
                    $_.FullName.EndsWith('.json', [System.StringComparison]::OrdinalIgnoreCase)))
        }) {
            $reader = [System.IO.StreamReader]::new($entry.Open())
            try {
                $text = $reader.ReadToEnd()
                if ($text -match 'CanDoItAll\.Components') {
                    throw "Package '$id' contains a stale CanDoItAll.Components reference in '$($entry.FullName)'."
                }
            }
            finally {
                $reader.Dispose()
            }
        }

        $expectedSnupkgName = "$id.$version.snupkg"
        $symbolMatches = @($snupkgs | Where-Object { $_.Name -ceq $expectedSnupkgName })
        if ($symbolMatches.Count -ne 1) {
            throw "Expected exactly one symbol package named '$expectedSnupkgName'; found $($symbolMatches.Count)."
        }

        if (-not $seenSnupkgs.Add($symbolMatches[0].FullName)) {
            throw "Symbol package '$expectedSnupkgName' matched more than one manifest entry."
        }

        $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($symbolMatches[0].FullName)
        try {
            $symbolEntry = "lib/net10.0/$($package.Assembly).pdb"
            $symbolEntryNames = @($symbolArchive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
            if ($symbolEntryNames -cnotcontains $symbolEntry) {
                throw "Symbol package '$expectedSnupkgName' must contain '$symbolEntry'."
            }
        }
        finally {
            $symbolArchive.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

if ($seenNupkgs.Count -ne $nupkgs.Count -or $seenSnupkgs.Count -ne $snupkgs.Count) {
    throw "The package directory contains files that are not present in the eight-package manifest."
}

$hashLines = @(
    @($nupkgs) + @($snupkgs) |
        Sort-Object Name |
        ForEach-Object {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash *$($_.Name)"
        }
)

if ($null -ne $expectedHashLines) {
    $hashDifference = @(Compare-Object -ReferenceObject $expectedHashLines -DifferenceObject $hashLines -SyncWindow 0)
    if ($hashDifference.Count -ne 0) {
        throw "Package hashes do not match '$ExpectedHashesPath'."
    }
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($hashPath)) | Out-Null
[System.IO.File]::WriteAllLines($hashPath, $hashLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "Validated $($packages.Count) packages and $($packages.Count) symbol packages."
Write-Host "SHA-256 manifest: $hashPath"
