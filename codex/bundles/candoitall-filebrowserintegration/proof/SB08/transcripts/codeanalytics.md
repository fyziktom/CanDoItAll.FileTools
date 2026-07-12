# SB08 Final CodeAnalytics — SB08-INV-01 SB08-INV-03

- Run label: final seven-product FileTools snapshot, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Command: `CodeAnalytics MCP analyze seven product projects; query dashboard, diagnostics, findings, dependencies, cycles, hotspots, and open questions`.
ExitCode: 0

Snapshot `snap-20260711202431-5dbb5110` contains 7 projects, 266 types, 1,940 members, and 743 dependency edges. It reports 0 cycles, 0 open questions, and no blocking errors.

Reviewed residuals are reported honestly:

- two DEP0002 warnings are duplicate generated types from Debug `obj` output (`EmbeddedAttribute` and `ValidatableTypeAttribute`), not product dependency cycles;
- four complexity Warning findings identify cohesive files: `FileBrowser.razor.cs` 362 lines, `FileBrowserSessionExecutionCoordinator.cs` 353 lines, `FileBrowserStateStore.cs` 493 lines containing separate disabled/bounded stores, and `FileSystemFileBrowserProvider.cs` 391 lines;
- 65 remaining findings are Info.

Focused semantic snapshots remain clean: Browser Core `snap-20260711162248-cd589ee2`, Browser Components `snap-20260711173023-3ec305d8`, Interaction Core `snap-20260711201550-e44d3e1b`, Interaction Components `snap-20260711201456-918bc3d5`, and filesystem `snap-20260711184248-7131c945`. The broad final snapshot is not falsely described as zero warnings.
