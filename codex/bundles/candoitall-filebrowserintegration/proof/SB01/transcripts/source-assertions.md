# Source Assertions — INV-SB01-BOUNDARY
Command: `inspect solution and all product project reference graphs`
ExitCode: 0

- Run label: `SB01-source-audit`
- Date: `2026-07-11`
- Working directory: `C:\repositories\CanDoItAll.FileTools`
- Commands:
  - `rg -n '<ProjectReference' src samples`
  - `rg -n '<(PackageReference|ProjectReference|FrameworkReference)' src/CanDoItAll.FileTools.Abstractions`
  - `rg -n 'CanDoItAll\.(Components|Infrastructure|Modules|Processes|AppComponents)' src samples`
- Exit interpretation: the first command lists only planned inward references; the second and third return no matches.

Assertions:

- Abstractions has no package, project, or framework reference.
- FileBrowser.Core, FileInteraction.Core, and Providers.FileSystem reference Abstractions only.
- Component RCLs reference their domain Core and Abstractions plus the ASP.NET shared framework.
- Optional Markdown references interaction packages only.
- No production/sample project references CanDoItAll Components, Infrastructure, Modules, Processes, or AppComponents.
