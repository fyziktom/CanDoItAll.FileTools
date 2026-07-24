# FileTools Agent Instructions

## Shared Standards

Follow the reviewed standards in a resolved `CanDoItAll.SharedInfo` clone. This
repository owns its local implementation and the exceptions documented below.

Use `$apply-candoitall-shared-standards` when available. It checks an explicit
`CANDOITALL_SHAREDINFO_ROOT` and nearby sibling locations without assuming a fixed
developer profile or repositories directory.

## Repository Scope

- This repository owns the storage-neutral FileBrowser and FileInteraction contracts,
  runtime coordination, Blazor components, filesystem and Markdown adapters, and the
  desktop file-launching boundary.
- Hosts own authorization, application storage, persistence, platform policy, and
  application-triggered effects. Do not add dependencies on `CanDoItAll.Components` or
  sibling product source projects to shipping FileTools packages.
- Keep production projects in `src`, tests in `tests`, maintained examples in `samples`,
  durable documentation in `docs`, and repository automation in `tools/<area>`.

## Commands

- Restore: `dotnet restore .\CanDoItAll.FileTools.slnx --configfile .\NuGet.config`
- Build: `dotnet build .\CanDoItAll.FileTools.slnx --configuration Release --no-restore`
- Test: `dotnet test .\CanDoItAll.FileTools.slnx --configuration Release --no-build --no-restore`
- Validate formatting: `dotnet format .\CanDoItAll.FileTools.slnx --verify-no-changes --no-restore`
- Build packages: `.\tools\deployment\nugets\Build-NuGets.ps1`
- Validate packages: `.\tools\validation\Test-NuGetPackages.ps1`

## Local Exceptions

- The completed `codex/bundles/candoitall-filebrowserintegration` delivery bundle remains
  at its historical tracked path so its frozen proof links and hashes stay meaningful.
  New active repository-local bundles belong under `.codex/bundles`.
- `README.md` uses canonical GitHub URLs for repository documents because the same file
  is embedded as the NuGet package README, where repository-relative links do not resolve.

## Safety

- Keep sibling repositories read-only unless the user explicitly requests a multi-repo
  change.
- Do not commit generated output, local settings, credentials, runtime state, or package
  artifacts.
- Preserve repository-specific changes that are unrelated to the active task.
