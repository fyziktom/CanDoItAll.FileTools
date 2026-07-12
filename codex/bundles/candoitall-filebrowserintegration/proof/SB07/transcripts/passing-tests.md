# SB07 Passing Interaction Tests

- Run label: settled final Release interaction test evidence, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Evidence source: final implementation/reviewer execution supplied to bundle closure.
- Command: `dotnet test tests/CanDoItAll.FileTools.Abstractions.Tests -c Release; dotnet test tests/CanDoItAll.FileTools.FileInteraction.Core.Tests -c Release; dotnet test tests/CanDoItAll.FileTools.FileInteraction.Components.Tests -c Release; dotnet test tests/CanDoItAll.FileTools.FileInteraction.Markdown.Tests -c Release`.
- Exit code: `0`.
ExitCode: 0

| Project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Abstractions | 21 | 0 | 0 |
| FileInteraction.Core | 59 | 0 | 0 |
| FileInteraction.Components | 72 | 0 | 0 |
| FileInteraction.Markdown | 23 | 0 | 0 |
| Total interaction scope | 175 | 0 | 0 |

`SB07-INV-01` is bound to `SaveCompleted_SuccessObservesPostAcknowledgementState`, failure/conflict/cancellation/throwing-observer companions, and rendered `TextUnitAutoSave_*`, edit-during-save, replaced-runtime, dynamic-availability, and coalesced-save cases.

`SB07-INV-02` is bound to priority/ambiguity, bounded history, preview coalescing/stale completion, controlled parent mode ordering, explicit unsupported and registered Diff, and detached/reentrant file replacement cases.

`SB07-INV-03` is bound to exact raster/SVG/unknown inert rendering, object-URL overlap/revoke/readiness/corrupt-content cases, optional Markdown dependency composition, and dangerous raw HTML/link/image/autolink rejection.

Representative semantic test names are source-verifiable under `repo://tests/CanDoItAll.FileTools.FileInteraction.Core.Tests/`, `repo://tests/CanDoItAll.FileTools.FileInteraction.Components.Tests/`, and `repo://tests/CanDoItAll.FileTools.FileInteraction.Markdown.Tests/`.
