# SB05 Adversarial Failing-First Review
Command: `independent source, rendered-component, and headed-browser closure review`
ExitCode: 1

- Run label: `2026-07-11 independent RCL/browser closure review`.
- Initial closure decision: **Fail; repair required**.
- Evidence type: independent source/component/browser review before the final repair. This bundle-only recorder did not rerun the pre-repair worktree and does not invent a command exit code.

| Invariant | Defect found before repair | Regression that must fail the shallow implementation |
| --- | --- | --- |
| `SB05-INV-01` | provider/file actions could appear as working UI without proving awaited host-only dispatch | file double-click and action callbacks must be awaited; folder Enter must not invoke host viewer; session action call count stays zero |
| `SB05-INV-02` | callback validity was not bound to the exact render-time session and snapshot, allowing same-key replacement hazards | detached callbacks after session replacement and same-session snapshot replacement do nothing; same-key in-flight action result is discarded |
| `SB05-INV-03` | manual keyboard behavior did not prove native Space for activatable non-selectable items; popup claimed menu semantics without a complete menu keyboard model | Open-only list/card primary controls are native buttons; popover is a `role=group` of ordinary buttons with no menu/menuitem claims |
| `SB05-INV-04` | source-dependent controls remained possible with no source; loaded actions and duplicate warning keys were not replacement-safe | no-source toolbar/location/search/refresh absent; busy snapshot disables action; equal warning identities render twice |
| `SB05-INV-05` | low-height cards could make the page/root and results compete for overflow and clip working chrome | at 480x360 body/root do not overflow and only card results own scrolling |
| `SB05-INV-06` | visual completion alone did not prove the standalone dependency/global-asset boundary | direct reference/source/global asset checks, RCL build/format, and CodeAnalytics must pass |

Final repaired proof is recorded in `passing-component-tests.md`, `source-assertions.md`, `browser-validation.md`, and `visual-review.md`.
