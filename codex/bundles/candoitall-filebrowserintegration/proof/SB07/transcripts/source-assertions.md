# SB07 Source Assertions

- Run label: read-only final source review, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Command: `rg and direct project/source inspection across src/CanDoItAll.FileTools.FileInteraction.* and corresponding tests`.
- Exit code: `0`.
ExitCode: 0

Observed final assertions:

- `SaveCompleted` has a production producer in Core and a production consumer/attachment lifecycle in Components; it is not a test-seeded-only event (`SB07-INV-01`).
- `FileInteraction.razor.cs` is 348 lines; mode, edit commands, save/preview event bridges, render factories, and runtime state have focused top-level owners.
- `FileSaveCoordinator.cs` is 342 lines and delegates isolated observer publication to the 35-line `FileSaveCompletionPublisher.cs`.
- renderer and history catalogs apply priority and reject final ties; generic bounded history is a low-priority fallback (`SB07-INV-02`).
- Markdown calls Markdig `DisableHtml()` and then removes every link/image destination while keeping inert labels; Markdown CSS uses Blazor `::deep` isolation.
- raster built-ins name exact PNG/JPEG/GIF/WebP/BMP types; SVG and unknown image types use metadata-only inert fallback.
- `FileObjectView.razor.js` is collocated, stores URL plus attribute ownership, removes `src`/`data` before revoke, and has no global `window.*` API (`SB07-INV-03`).
- no FileTools product references CanDoItAll or `CanDoItAll.Components`; the optional Markdown project alone references Markdig.

Final hashes for the asserted files are in `bundle://proof/SB07/transcripts/final-hashes.md`.
