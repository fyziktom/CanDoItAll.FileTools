# SB07 Proof Manifest

Status: **Pass after independent lifecycle, security, architecture, component, headed-browser, and visual repair** (2026-07-11).

## Ownership

- Subbundle: SB07 Basic Viewers, Editing Workflow, and History.
- Requirements: R012-R023.
- Raw notes: N010-N016.
- Semantic contract: `bundle://proof/SB07/semantic-invariants.md`.

## Review sequence

The first apparently working interaction surface was not accepted. Independent review reopened autosave completion because the rendered state could remain `IsSaving`, renderer/history ambiguity, binary edit neutrality, stale object-URL ownership, unsafe Markdown destinations, controlled-mode callback order, reentrant file replacement, content limits, and an oversized component facade. Repairs added a post-transition `SaveCompleted` producer, a latest-wins component bridge, explicit priority/ambiguity, defensive binary changes, serialized and generation-checked object-URL lifecycle, inert Markdown links/images, callback ordering, replacement guards, bounded content, and focused collaborators. The same autosave behavior then passed direct Core tests, rendered component tests, and the headed Sandbox.

- Failing-first/adversarial transcript: `bundle://proof/SB07/transcripts/failing-first.md`.
- Passing semantic test transcript: `bundle://proof/SB07/transcripts/passing-tests.md`.
- Passing Release build/format transcript: `bundle://proof/SB07/transcripts/passing-build-and-format.md`.
- CodeAnalytics transcript: `bundle://proof/SB07/transcripts/codeanalytics.md`.
- Source assertions: `bundle://proof/SB07/transcripts/source-assertions.md`.
- Anti-stub/global-asset audit: `bundle://proof/SB07/transcripts/anti-stub.md`.
- Browser actions/metrics: `bundle://proof/SB07/transcripts/browser-validation.md`.
- Screenshot review: `bundle://proof/SB07/transcripts/visual-review.md`.
- Final source/test/visual hashes: `bundle://proof/SB07/transcripts/final-hashes.md`.

This bundle-only closure did not rerun repository-writing build pipelines. It records the settled implementation/reviewer evidence supplied to the closure task, and it independently performed read-only source, package-hash, screenshot-existence, and integrity checks.

## Commands and results

| Evidence | Result | Transcript |
| --- | --- | --- |
| Interaction scoped Release tests | 175/175: 21 Abstractions, 59 Core, 72 Components, 23 Markdown | `bundle://proof/SB07/transcripts/passing-tests.md` |
| Interaction Release build/format | 0 warnings/errors; format clean | `bundle://proof/SB07/transcripts/passing-build-and-format.md` |
| Core CodeAnalytics | `snap-20260711201550-e44d3e1b`: zero diagnostics, Warning/Error findings, and cycles | `bundle://proof/SB07/transcripts/codeanalytics.md` |
| Components CodeAnalytics | `snap-20260711201456-918bc3d5`: zero diagnostics, Warning/Error findings, and cycles | `bundle://proof/SB07/transcripts/codeanalytics.md` |
| Headed browser and browser-to-interaction bridge | pass; console 0 errors/0 warnings, localhost-only resources, all 18 displayed dynamic requests HTTP 200 | `bundle://proof/SB07/transcripts/browser-validation.md` |

## Portable browser artifacts

- `repo://output/playwright/sb07/interaction-markdown-rendered-1440x900.png`
- `repo://output/playwright/sb07/interaction-edit-preview-720x520.png`
- `repo://output/playwright/sb07/interaction-autosave-clean-720x520.png`
- `repo://output/playwright/sb07/interaction-mermaid-560x360.png`
- `repo://output/playwright/sb07/interaction-binary-limit-480x360.png`
- `repo://output/playwright/sb07/browser-markdown-overlay-390x844.png`

## Changed-file integrity

The frozen source/test/visual integrity set is in `bundle://proof/SB07/transcripts/final-hashes.md`. FileTools was created in an uncommitted transfer worktree, so reliable before-hashes do not exist for newly added SB07 files. Representative frozen source hash: `e3ce6ad93cebb516d0cde2b347725cb5e579be15f5e4a3d99a8e7c7cd4357c7d`.

## Semantic adequacy gate

- Shallow-pass trap: a shell can show editor buttons and count a save callback while completion never reaches rendered state, stale completions mutate a replacement file, Markdown creates navigation/fetch DOM, or object URLs display/revoke the wrong payload.
- Adversarial negative proof: failure/conflict/cancellation/edit-during-save/replaced-runtime/coalesced save tests; equal-priority ambiguity; stale preview; dangerous Markdown/raw HTML; SVG/unknown inert fallback; corrupt image/PDF readiness; overlapping object-URL application; oversized text and binary paths.
- Semantic positive proof: direct/rendered tests and the headed Sandbox prove registered Markdown View/Edit, debounced split preview, bounded undo/redo, awaited manual/automatic persistence, acknowledged clean state, binary limits, image/PDF readiness, inert fallback, Mermaid extension, browser activation, blocked identities, folder navigation, and live read-only filesystem bridging.
- Anti-stub result: no production TODO/FIXME/NotImplemented, fixture branch, service locator, or document-global script/style path was accepted; optional Markdown alone owns Markdig.
- Literal closure: the reusable interaction framework and basic renderers are shipped. Full Diff/Office-class editors and CanDoItAll host integration remain explicit future extensions.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `SaveCompleted` event | `repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCoordinator.cs` and `FileSaveCompletionPublisher.cs` publish only after acknowledgement/rejection/cancellation state transition | `repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/FileInteractionSaveEventBridge.cs` synchronizes the attached runtime's current state | the bridge attaches/detaches per runtime, sequence-gates completion, rejects replacement senders, and disposes subscriptions | Core success/failure/conflict/cancel/throwing-observer tests plus Components autosave/edit-during-save/replaced-runtime/coalesced tests in `repo://tests/CanDoItAll.FileTools.FileInteraction.Core.Tests/FileSaveCoordinatorTests.cs` and `repo://tests/CanDoItAll.FileTools.FileInteraction.Components.Tests/FileInteractionAdvancedInteractionTests.cs` |
| host `SaveRequested` callback | `repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/EventCallbackFileSaveTarget.cs` awaits the host callback | Sandbox `repo://samples/CanDoItAll.FileTools.Sandbox/Demo/SandboxInteractionGateway.cs` authorizes revision-aware persistence | dirty state clears only from the host acknowledgement; failure/conflict stays dirty and retryable | missing callback, host failure, conflict/rebase/overwrite, edit-during-save, and replacement tests in the component suites |
| renderer `ContentChanged` callback | registered editors produce immutable `FileInteractionContentChange` values through the render context | `FileInteractionEditingRuntime` applies bounded content, history, preview, and save policy | bytes and metadata are defensively copied/normalized before the monotonic edit revision is recorded | binary-neutral change, oversize, invalid UTF-8, and detached-editor tests |

## Architecture closure

- `FileInteraction.Components -> FileInteraction.Core -> Abstractions`; optional `FileInteraction.Markdown -> Components/Core` and Markdig; no reverse edge.
- Core and Components focused snapshots have zero diagnostics, Warning/Error findings, or cycles.
- `FileInteraction.razor.cs` is 348 lines after extracting mode, save/preview bridges, render factories, and edit commands; `FileSaveCoordinator.cs` is 342 lines and delegates event publication.
- No runtime partial class or `IServiceProvider` service location was introduced. DI is confined to explicit registration/composition.

## Downstream decision

AC7 passes. SB08 may use the frozen interaction proof for final regression/package closure, but may not claim deferred CanDoItAll module integration has shipped.
