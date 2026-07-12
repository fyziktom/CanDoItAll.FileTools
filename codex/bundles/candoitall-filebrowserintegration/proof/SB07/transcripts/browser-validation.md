# SB07 Final Headed Browser Validation

- Run label: final repaired Sandbox interaction and browser-to-interaction matrix, 2026-07-11.
- Working directory context only: local Sandbox at loopback port 5189; server stopped after capture.
- Command: `Playwright headed interaction scenarios, browser overlay bridge, responsive DOM metrics, console/resource/network inspection`.
ExitCode: 0

## Semantic scenarios

- `SB07-INV-01`: automatic save was awaited and ended at `Persisted sandbox-r101`, `Loaded`, clean edit revision 1, `Saved`, dirty false, saving false. Manual save, failure preserving edits plus retry revision 101, conflict plus rebase revision 103, conflict plus overwrite revision 105, close guards, and the 4 KiB host rejection behaved explicitly.
- `SB07-INV-02`: optional Markdown rendered an H1 through its renderer rather than `TextFileView`; split preview debounced; undo/redo worked; controlled editing remained usable.
- `SB07-INV-03`: Mermaid edit/view updated with zero external scripts; binary neutral content became dirty but exact 524,288-byte limit rejection preserved prior content; image used a blob URL and completed at natural 1x1; PDF used a blob object with no fallback; ZIP/unknown stayed metadata-only with zero object, iframe, or image content.
- Browser bridge: file double-click produced `ItemInvoked` and opened; action surface produced `ActionRequested` and opened; unmapped identity was blocked without overlay; folder navigation stayed inside browser with no overlay; live filesystem content opened as authorized read-only content.

## Responsive and runtime analytics

- 1440x900, 720x520, 560x360, 480x360, and 390x844 passed with no document-level horizontal overflow.
- constrained binary and overlay layouts used their intended internal scroll owners; controls and content remained reachable.
- fresh console contained 2 informational Blazor messages, 0 errors, and 0 warnings.
- resource snapshot contained 10 resources from loopback only and no external origins.
- all 18 displayed dynamic requests returned HTTP 200.
- the previously defective raw-Markdown screenshot was deleted and replaced by the final rendered-Markdown evidence.

## Explicit instrumentation limitation

No independent persistent `pageerror` counter was installed before final teardown. No page-error event or console-error appeared in the available evidence. This is recorded as an instrumentation limitation, not silently converted into a counted zero.
