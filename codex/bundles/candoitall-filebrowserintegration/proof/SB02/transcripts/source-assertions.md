# Contract Source Assertions — INV-SB02-CONTRACTS
Command: `inspect Abstractions project and source dependency boundary`
ExitCode: 0

- Date: `2026-07-11`
- Working directory: `C:\repositories\CanDoItAll.FileTools`
- Commands:
  - `rg -n '<(PackageReference|ProjectReference|FrameworkReference)' src\CanDoItAll.FileTools.Abstractions`
  - forbidden searches for ASP.NET, caching, `FileInfo`, `DirectoryInfo`, Components, Infrastructure, Modules, `ComponentBase`, `RenderFragment`, `IServiceProvider`, and `IServiceCollection`.
- Result: no matches.

Assertions:

- Abstractions is BCL-only and has no project/package/framework reference.
- Browser contracts remain descriptive; built-in UI action projection was not moved into Abstractions.
- FileInteraction content access is independent of browser-session lifetime.
- Save content is replayable and host-persisted; no storage driver or callback implementation is embedded.
- Renderer component types and DI are absent.
