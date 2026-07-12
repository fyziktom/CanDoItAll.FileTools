# SB05 Browser Validation
Command: `Playwright headed Sandbox browser scenario matrix and DOM overflow/console assertions`
ExitCode: 0

- Run label: `2026-07-11 repaired headed Playwright matrix`.
- Route: Sandbox `/browser` (also mapped at `/`).
- Host: standalone `CanDoItAll.FileTools.Sandbox` focused floating frame.
- Result: **Pass**.

## Interaction matrix

- switched Healthy/Empty/Partial warning/Retryable error/Live filesystem scenarios;
- switched Standard/Compact/Minimal density and List/Cards projection;
- switched sources, navigated folders, selected items, searched, refreshed, loaded more, and exercised error/retry state;
- double-clicked a file and selected an item action; the Sandbox host event log received the request while the RCL executed no provider effect;
- opened the native action popover and verified readable top-layer content and honest button-group semantics;
- no-source state hid search, refresh, location, and source-dependent toolbar controls;
- browser console after the repaired matrix: `0 errors`, `0 warnings`.

## Viewport and artifact matrix

| Viewport/state | Artifact | Result |
| --- | --- | --- |
| 1440x900 Standard/List | `repo://output/playwright/sb05/repaired-desktop-standard.png` | readable full desktop source/toolbar/table/status layout; no harmful overflow |
| desktop action popover open | `repo://output/playwright/sb05/action-popover.png` | readable, unclipped, above result chrome; ordinary action buttons |
| 720x520 Compact/List live filesystem | `repo://output/playwright/sb05/compact-focus-720x520.png` | compact toolbar wraps intentionally; results own vertical scroll |
| 560x360 Minimal/Cards | `repo://output/playwright/sb05/minimal-cards-focus-560x360.png` | essential search/view/breadcrumb/status remain reachable |
| 480x360 repaired Minimal/Cards | `repo://output/playwright/sb05/repaired-minimal-cards-480x360-final2.png` | final low-height scroll-owner metrics pass |
| 390x360 Minimal/Cards | `repo://output/playwright/sb05/repaired-minimal-cards-390x360.png` | narrow low-height controls/results remain usable |
| 390x844 repaired Minimal/Cards | `repo://output/playwright/sb05/repaired-minimal-cards-390x844-final2.png` | final tall-phone layout readable with load-more/status reachable |

## Final 480x360 DOM/overflow measurements

```text
viewport: 480 x 360
document.body: clientWidth=480, scrollWidth=480, clientHeight=360, scrollHeight=360
.ft-file-browser__card-scroll: clientWidth=450, scrollWidth=450, clientHeight=76, scrollHeight=290
overflow owners: .ft-file-browser__card-scroll only
```

There is no page-level lateral or vertical overflow. The intentionally bounded result region scrolls while source selection, search, recursive option, refresh/view controls, breadcrumb, load-more, item count, and status remain visible.
