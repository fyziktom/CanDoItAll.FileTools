# SB07 Failing-First and Adversarial Review

- Run label: independent interaction lifecycle/architecture/browser review, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Evidence source: implementation/reviewer and headed Sandbox reports supplied to the bundle closure; no source repository command was rerun by this bundle-only task.
- Command: `Playwright headed /interaction automatic-save scenario; inspect host StateChanged completion`.
- Exit code: `1`.
- Output: the awaited automatic save completed in the host, but the rendered/state callback observation remained at `IsSaving`; this failed `SB07-INV-01` and reopened the implementation.
ExitCode: 1

The repair added a Core `SaveCompleted` event emitted after state transition and a latest-wins component bridge. Review then forced the same behavior through success, failure, conflict, cancellation, edit-during-save, replacement, and coalesced manual/automatic paths.

- Command: `Independent C# architecture and renderer lifecycle review of FileInteraction Core/Components/Markdown`.
- Exit code: `1`.
- Output: the initial result was rejected for a concrete Core dependency cycle through a concrete publisher signature, oversized responsibility concentration, ambiguous renderer/history selection, stale object-URL ownership/readiness risks, unsafe Markdown destinations, reentrant replacement/order risks, and incomplete binary/content-limit semantics. These findings cover `SB07-INV-02` and `SB07-INV-03` and were repaired before the passing snapshots/tests.

This is a failing-first gate record, not a fabricated unit-test console log. The exact observable browser failure and reviewer findings are preserved; passing evidence for the repaired behavior is separate.
