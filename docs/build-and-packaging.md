# Build, test, and packaging

The repository is pinned by `global.json` to .NET SDK 10.0.302 with latest-patch
roll-forward. Package metadata is centralized in `Directory.Build.props`;
`Directory.Build.targets` supplies the repository README and license to packable projects
that do not define package-specific files. NuGet versions are centralized in
`Directory.Packages.props`, and restore commands select the repository-owned
`NuGet.config` explicitly so machine-level sources do not affect the result.

## Local validation

From the repository root:

```powershell
dotnet restore .\CanDoItAll.FileTools.slnx --configfile .\NuGet.config
dotnet build .\CanDoItAll.FileTools.slnx -c Release --no-restore -warnaserror
dotnet test .\CanDoItAll.FileTools.slnx -c Release --no-build --no-restore
dotnet format .\CanDoItAll.FileTools.slnx --verify-no-changes --no-restore
```

The Sandbox can be built or run separately:

```powershell
dotnet build .\samples\CanDoItAll.FileTools.Sandbox\CanDoItAll.FileTools.Sandbox.csproj -c Release --no-restore -warnaserror
dotnet run --project .\samples\CanDoItAll.FileTools.Sandbox\CanDoItAll.FileTools.Sandbox.csproj -c Release --no-build
```

## Pack all eight libraries

After a Release build, create packages without rebuilding:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1 -Configuration Release -NoBuild -NoRestore
.\tools\validation\Test-NuGetPackages.ps1
```

The default package directory is `artifacts/packages/<version>_<timestamp>`; validation
writes a sorted SHA-256 manifest to `artifacts/package-validation/package-hashes.sha256`.
The package builder
accepts an absolute or repository-relative output directory as required by the shared
CanDoItAll NuGet adapter contract. The validator confines its generated hash manifest and
validated local package set to the repository's ignored `artifacts` directory. Nothing is
published.

To pack with a one-off package version or a separate output directory:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1 `
    -Version 0.1.0-ci.42 `
    -OutputDirectory artifacts/packages/ci-42 `
    -NoBuild `
    -NoRestore

.\tools\validation\Test-NuGetPackages.ps1 `
    -PackageDirectory artifacts/packages/ci-42 `
    -HashOutput artifacts/package-validation/ci-42.sha256
```

Without `-NoBuild`/`-NoRestore`, the adapter restores the canonical solution and
`dotnet pack` performs its normal build. The adapter supports `-WhatIf`, makes one
approval decision before creating output or invoking .NET, and uses a fixed manifest
rather than discovering arbitrary projects, so tests and samples cannot become release
packages accidentally.

## Package gates

Validation requires:

- exactly one `.nupkg` and one `.snupkg` for each expected ID;
- matching package ID, assembly, XML documentation, package readme, SPDX MIT license
  expression, approved package icon, and CanDoItAll author metadata;
- distinct public project and source-repository URLs plus published Git provenance;
- the package-specific versions selected by each project, including exact internal dependency versions;
- the approved project-reference and packed dependency graph;
- no CanDoItAll.Components or main-application dependency;
- Markdig only in `CanDoItAll.FileTools.FileInteraction.Markdown`;
- static-web-asset build metadata and at least one asset for each Razor class library;
- SHA-256 for every package and symbol package.

Use `-ExpectedHashesPath` to verify that an exact artifact set still matches a previously recorded manifest, for example after copying it to another location. These are raw package-file integrity hashes. A separate `dotnet pack` invocation can produce different NuGet ZIP metadata even from the same compiled inputs, so this comparison is not presented as reproducible-repack proof.

## CI

`.github/workflows/ci.yml` runs the same restore, Release build, tests, format gate, pack, and package validation on Windows. It has read-only repository permission and does not push or publish packages.
