# Build, test, and packaging

The repository is pinned by `global.json` to .NET SDK 10.0.301 with latest-patch roll-forward. Package metadata is centralized in `Directory.Build.props`; `Directory.Build.targets` supplies the repository README to packable projects that do not define a package-specific readme. NuGet versions are centralized in `Directory.Packages.props`.

## Local validation

From the repository root:

```powershell
dotnet restore .\CanDoItAll.FileTools.slnx
dotnet build .\CanDoItAll.FileTools.slnx -c Release --no-restore -warnaserror
dotnet test .\CanDoItAll.FileTools.slnx -c Release --no-build --no-restore
dotnet format .\CanDoItAll.FileTools.slnx --verify-no-changes --no-restore
```

The Sandbox can be built or run separately:

```powershell
dotnet build .\samples\CanDoItAll.FileTools.Sandbox\CanDoItAll.FileTools.Sandbox.csproj -c Release --no-restore -warnaserror
dotnet run --project .\samples\CanDoItAll.FileTools.Sandbox\CanDoItAll.FileTools.Sandbox.csproj -c Release --no-build
```

## Pack all seven libraries

After a Release build, create packages without rebuilding:

```powershell
.\scripts\pack-release.ps1 -Configuration Release -NoBuild -NoRestore
.\scripts\validate-packages.ps1
```

The default package directory is `output/packages/release`; validation writes a sorted SHA-256 manifest to `output/package-validation/package-hashes.sha256`. Both scripts reject output paths outside the repository's `output` directory. Nothing is published.

To pack with a one-off package version or a separate output directory:

```powershell
.\scripts\pack-release.ps1 `
    -Version 0.1.0-ci.42 `
    -OutputDirectory output/packages/ci-42 `
    -NoBuild `
    -NoRestore

.\scripts\validate-packages.ps1 `
    -PackageDirectory output/packages/ci-42 `
    -HashOutput output/package-validation/ci-42.sha256
```

Without `-NoBuild`/`-NoRestore`, `dotnet pack` performs the normal build/restore behavior. The script uses a fixed manifest rather than discovering arbitrary projects, so tests and samples cannot become release packages accidentally.

## Package gates

Validation requires:

- exactly one `.nupkg` and one `.snupkg` for each expected ID;
- matching package ID, assembly, XML documentation, package readme, MIT expression, and CanDoItAll author metadata;
- the approved project-reference and packed dependency graph;
- no CanDoItAll.Components or main-application dependency;
- Markdig only in `CanDoItAll.FileTools.FileInteraction.Markdown`;
- static-web-asset build metadata and at least one asset for each Razor class library;
- SHA-256 for every package and symbol package.

Use `-ExpectedHashesPath` to verify that an exact artifact set still matches a previously recorded manifest, for example after copying it to another location. These are raw package-file integrity hashes. A separate `dotnet pack` invocation can produce different NuGet ZIP metadata even from the same compiled inputs, so this comparison is not presented as reproducible-repack proof.

## CI

`.github/workflows/ci.yml` runs the same restore, Release build, tests, format gate, pack, and package validation on Windows. It has read-only repository permission and does not push or publish packages.
