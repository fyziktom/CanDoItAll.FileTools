# SB07 Semantic Invariants

## Invariant SB07-INV-01

- Invariant ID: `SB07-INV-01`
- Source raw note: N013/R015-R016 — persistence must be an awaited host action with manual and automatic strategies.
- Expected behavior: save completion is published after the coordinator reaches acknowledged, failed, conflict, or cancelled current state; the attached component runtime renders that current state and a stale runtime cannot update its replacement.
- Disallowed shallow implementation: firing only an initial `IsSaving` notification, clearing dirty before host acknowledgement, or forwarding a completion from a detached runtime.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first.md` records the headed autosave state remaining `IsSaving`.
- Passing test: `bundle://proof/SB07/transcripts/passing-tests.md` records direct completion and rendered autosave success/failure/conflict/edit/replacement/coalescing cases.
- Changed source files: `repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCoordinator.cs`, `repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCompletionPublisher.cs`, and `repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/FileInteractionSaveEventBridge.cs`; hashes are in `bundle://proof/SB07/transcripts/final-hashes.md`.
- Production assertions: the coordinator publishes from post-transition state; observer failures are isolated; the bridge checks sender/attachment/sequence and then synchronizes current state.
- Red-team negative case: edit while save awaits, replace runtime before completion, coalesce manual and automatic requests, and throw from an observer.
- Downstream dependency check: Sandbox automatic save reaches clean only after its awaited host acknowledgement; SB08 packages the same assemblies.

## Invariant SB07-INV-02

- Invariant ID: `SB07-INV-02`
- Source raw note: N012-N014/R013/R017-R019 — View/Edit, history, split preview, and a future Diff seam must be explicit and race-safe.
- Expected behavior: profile/renderer/history resolution is deterministic; undo/redo is bounded and file/revision scoped; rapid previews coalesce and stale completion cannot publish; unsupported Diff is explicit.
- Disallowed shallow implementation: extension switch statements, equal-priority first-wins behavior, UI-owned history choreography, preview per keystroke, or Diff silently falling back to View.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first.md` records ambiguity/lifecycle and facade findings that reopened the first result.
- Passing test: `bundle://proof/SB07/transcripts/passing-tests.md` records resolver/history/preview/controlled-mode/Diff component cases.
- Changed source files: Core catalogs/coordinators and Components composition/runtime files under `repo://src/CanDoItAll.FileTools.FileInteraction.Core/` and `repo://src/CanDoItAll.FileTools.FileInteraction.Components/`; hashes are in `bundle://proof/SB07/transcripts/final-hashes.md`.
- Production assertions: resolution uses match tier then priority and rejects final ties; history fallback has low priority; preview and render callbacks carry generation/revision identity.
- Red-team negative case: two highest-priority matches, a newer edit finishing before an old preview, and a callback captured before file replacement.
- Downstream dependency check: the headed Markdown edit/split/undo/redo flow and explicit unsupported state pass without optional dependencies leaking into base packages.

## Invariant SB07-INV-03

- Invariant ID: `SB07-INV-03`
- Source raw note: N015-N016/R020-R021 — consumers choose renderers and file-type assets must not conflict with the host.
- Expected behavior: base built-ins cover text, an exact raster allowlist, opt-in native PDF, and inert unknown/SVG; Markdown is optional and strips active destinations; object URLs are owned, latest-wins, readiness-gated, and revoked.
- Disallowed shallow implementation: wildcard `image/*`, inline SVG, raw Markdown HTML/links/images, globally loaded scripts/styles, or a stale object URL remaining visible.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first.md` records the unsafe/stale renderer risks rejected by review.
- Passing test: `bundle://proof/SB07/transcripts/passing-tests.md` records Markdown security, exact raster/SVG/unknown, object-URL overlap/readiness/disposal, and optional package graph tests.
- Changed source files: `repo://src/CanDoItAll.FileTools.FileInteraction.Components/Components/FileObjectView.razor.js`, built-in composition/view files, and `repo://src/CanDoItAll.FileTools.FileInteraction.Markdown/`; hashes are in `bundle://proof/SB07/transcripts/final-hashes.md`.
- Production assertions: collocated JS owns `{url, attributeName}`, removes `src`/`data` before revoke, and CSS is isolated; Markdown disables raw HTML and emits no link/image destination.
- Red-team negative case: hostile SVG, dangerous Markdown navigation or fetch targets, corrupt image and PDF content, and two overlapping URL applications.
- Downstream dependency check: packaged base assemblies have no Markdig edge, the optional package alone contains Markdig, and headed image/PDF/inert/Markdown states pass.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `SaveCompleted` event | `repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCoordinator.cs` / `FileSaveCompletionPublisher.cs` | `repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/FileInteractionSaveEventBridge.cs` | attach, post-transition publish, sequence/current-runtime sync, detach/dispose | direct Core completion tests and rendered stale/replacement/coalescing autosave tests |
| `ContentChanged` event | registered editor receives callback through `repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/FileInteractionRenderContext.cs` | `FileInteractionEditingRuntime` applies bounded edit/history/preview/save | defensive immutable copy then monotonic edit revision | binary-neutral, oversize, invalid UTF-8, detached editor tests |

## Anti-stub and literal closure

`bundle://proof/SB07/transcripts/anti-stub.md` records no production stub/service-locator/global-asset path. The shipped seam is intentionally extensible: full Diff, Office, and host-specific Mermaid remain future optional packages, not pretend built-ins.
