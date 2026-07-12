# UI, JS, CSS, and Layout Architecture

## Component modes

- `Standard`: breadcrumbs, full search/filter/sort, source navigation, list/cards, status.
- `Compact`: condensed row heights/gaps, collapsible secondary filters, shorter labels, status summary.
- `Minimal`: breadcrumb/current location, search toggle, refresh, list-first result surface; optional source rail/status/action columns.
- Density/chrome are explicit parameters/options, not inferred solely from phone width. Container queries remain a fallback for actual available width.

## Floating-window rules

- Exactly one result scroll owner inside the browser.
- Header/chrome remains visible while results scroll when height permits.
- Context menus/overlays render above neighboring window chrome and are tested open.
- No toolbar control is simply clipped; secondary actions collapse into an overflow menu.
- Host supplies the semantic include-descendants label (“Include subprojects” in project scopes).

## CSS isolation

Generated child markup (for example inert Markdown HTML) is reached only through Blazor's `::deep` isolation transform anchored below the component-scoped root.

Each component uses a matching `.razor.css`; the RCL’s isolated bundle is automatically included by the consuming app. Shared variables use a FileTools-prefixed surface at the component root. Avoid required global styles, `body` mutation, generic selectors, and inline style attributes.

## JavaScript isolation

- Use collocated `.razor.js` ES modules imported through `IJSRuntime` from `./_content/{PackageId}/...`.
- No global `window.fileTools` API or script tags in components.
- JS responsibilities are narrow: object URL create/revoke, anchor measurement/position, focus/selection helpers only when browser APIs are necessary.
- Component owns/disposes module/object URLs; server-side disposal tolerates circuit disconnection where applicable.
- File-type-specific modules remain in their renderer package so absent packages load no assets.

## Event conflicts

Pointer/keyboard handling attaches only to component elements. Mermaid or editor-specific mouseover/keyboard behavior is owned by that renderer and uses propagation deliberately; the shell does not register document-wide handlers.

## Visual proof matrix

| Scenario | Required size |
| --- | --- |
| Maximized desktop | available large viewport, at least 1440x900 equivalent |
| Dialog | 720x520 |
| Canvas floating | 560x360 and 480x360 |
| Narrow/mobile | 390x844 and 360px container |

Each pass covers list/cards where applicable, menu open state, loading/error/empty, pagination, selection, and interaction view/edit/split.
