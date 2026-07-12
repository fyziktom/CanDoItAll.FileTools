# SB06 Proof Manifest

Status: **Pass after independent architecture repair** (2026-07-11).

- Semantic contract: `bundle://proof/SB06/semantic-invariants.md`.
- Failing-first/adversarial negative proof: `bundle://proof/SB06/transcripts/failing-first.md`.
- Passing semantic positive proof: `bundle://proof/SB06/transcripts/closure-summary.md`.
- Anti-stub audit: `bundle://proof/SB06/transcripts/closure-summary.md`.
- Portable source anchor: `repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCoordinator.cs`.

## Review sequence

The first implementation passed 35 tests but was deliberately not closed. Independent review found queued persistence after disposal, unstable conflict/autosave semantics, unsafe UI-side history choreography, undrained preview work, and normalization/composition gaps. Those findings were repaired and covered by negative tests before this manifest was emitted.

## Commands and results

- Abstractions focused Release tests: 21 passed, 0 failed/skipped.
- FileInteraction.Core focused Release tests: 47 passed at the original SB06 gate, then 59 passed after final SB07 cross-layer lifecycle hardening.
- Four scoped `dotnet format --verify-no-changes --no-restore` checks: exit 0.
- Final Core CodeAnalytics `snap-20260711201550-e44d3e1b`: zero diagnostics, Warning/Error findings, or cycles; only product edge is Core -> Abstractions. The earlier SB06 snapshot was `snap-20260711154304-e9a43a19`.
- Source audit: no Razor/filesystem/cache/storage/main/UI types, service locator/DI package, Timer, `Task.Run`, partial runtime, TODO, or stub.

## Production behavior artifacts and SHA-256

```text
0e31349431e862cb0b68f1319e436dafae326049cedf16f193ff241624ed30a1  BoundedTextHistoryProvider.cs
1ed7b09e07bed50ba98e85bffe472316c7b1bbd960526c6ab5413a27643b1d1c  FileAutoSaveScheduler.cs
208017726d0c2ed748a0827ba5f99f0df24581a8ece1432dbb78982948f7854c  FileEditSession.cs
24a0c810aced6b2d0a8dc91747f14a3c0a17f55f7caa72c7e72052d5566c6039  FileInteractionCoreBuilder.cs
df351cdefb7ddc82c51f83ac11df2bfc3b0f534ccfcd846e067276dd0ec0d6c3  FileInteractionDelay.cs
27cf2722cab30f603afed0bc0bb23506854e7229e70c767cd419a71bd6938a5a  FileInteractionEditCoordinator.cs
310659931fede3a689ec4776988cf80c7a3e9fc4a39a3574f79a92167a5568b9  FileInteractionProfileCatalog.cs
834163685327619bf1d6ec39af0ee01c3ef19728352cc6f02b8345c43261ebfa  FilePreviewCoordinator.cs
46e0ab13f7b29932f14ffcb98351b05c027f5cc4c1987ef440044c1bc99ea3c9  FileSaveContracts.cs
49091ceee39046d1f67dc6ab43bb7246ba04e0f39d6ea08eb0cf464a3d6e7d20  FileSaveCoordinator.cs
```

Contract hardening in Abstractions rejects unknown autosave bits and canonicalizes MIME parameters; those changes remain BCL-only.

## Composition decision

Core intentionally does not take Microsoft DI. `FileInteractionCoreBuilder` builds immutable explicit catalogs without service location; the optional `IServiceCollection` convenience layer belongs to FileInteraction.Components in SB07.

## Dependent proof

SB07 must prove the component adapter, fake renderer registration, save EventCallback bridge, and actual browser UI. This gate proves UI-neutral policy only.
