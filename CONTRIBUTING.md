# Contributing

## Contribution Policy

Issues and focused pull requests are welcome. Open an issue before a large public API,
package-boundary, or compatibility change so the intended ownership and migration path
can be agreed before implementation.

Security reports must follow [SECURITY.md](SECURITY.md) and must not be filed as public
issues.

## Development Setup

1. Install the .NET SDK pinned by `global.json`.
2. Use Windows PowerShell 5.1 or PowerShell 7 for repository packaging tools.
3. Run commands from the repository root.

The tracked `NuGet.config` declares the package sources required by the repository.
Restore commands and repository tools select it explicitly so machine-level sources do
not affect the result. Do not add credentials or machine-specific feeds to that file.

## Validation

Run the main gate:

```powershell
dotnet restore .\CanDoItAll.FileTools.slnx --configfile .\NuGet.config
dotnet build .\CanDoItAll.FileTools.slnx --configuration Release --no-restore -warnaserror
dotnet test .\CanDoItAll.FileTools.slnx --configuration Release --no-build --no-restore
dotnet format .\CanDoItAll.FileTools.slnx --verify-no-changes --no-restore
```

For package or metadata changes, also run:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1 -Configuration Release -NoBuild -NoRestore
.\tools\validation\Test-NuGetPackages.ps1
```

The package build creates local artifacts and never publishes them.

## Architecture Rules

- Preserve the dependency direction documented in
  [docs/package-architecture.md](docs/package-architecture.md).
- Keep authorization, persistence, host storage, and platform effects behind the public
  host boundaries described in
  [docs/host-integration-security.md](docs/host-integration-security.md).
- Shipping projects may reference sibling FileTools source projects, but not sibling
  repositories or `CanDoItAll.Components`.
- Keep generated output and local state out of Git.
- Update documentation when public behavior, package metadata, or package contracts
  change.

## Pull Requests

- Keep changes focused.
- Add or update tests for behavior changes.
- Describe public API, package-version, and migration effects.
- Include the exact validation commands and results.
