# C# Testability Plan

## Characterization before transfer

- Preserve existing URI/model/query/provider validation/navigation/search/session/tree/filesystem/UI helper tests.
- Record baseline test totals and failures before semantic changes.
- Update tests intentionally for changed host-action and file-open semantics; never delete a failing assertion without its replacement behavior test.

## Isolated unit tests

| Owner | Direct behaviors |
| --- | --- |
| Abstractions | validation, equality/identity, media/extension normalization, option invariants, content lease disposal |
| Browser Core | catalog, response validation, navigation, search, selection, retention disabled/bounded, invalidation, retry/cancellation/stale completion |
| FileSystem | root confinement, traversal, links, pagination tokens, inaccessible/race entries, metadata, range read, file invocation capability |
| Interaction Core | profile scoring, ambiguity, mode support, edit revision, dirty state, manual/idle/interval/edit-count/text-unit save, dynamic save availability, preview coalescing/metadata pairing, prioritized history selection/branching/bounds |
| Browser Components | parameters, event-up activation/actions, no direct provider URI effects, normal/compact/minimal projections, debounce/disposal |
| Interaction Components | renderer selection, unsupported state, View/Edit switch, text and binary content changes, edit-size rejection, awaited save success/failure/conflict/reentrancy, undo visibility, split preview metadata, latest-wins object URLs, file switch cancellation |
| Markdown adapter | renderer registration, sanitized output policy, edit/preview debounce defaults |

## Required negative tests

- Traversal/symlink escape cannot leave configured root.
- A provider-supplied URI cannot execute without host event handling.
- Disabled retention cannot serve a previously materialized page after mutation.
- A stale async browse/search/preview result cannot overwrite a newer revision.
- Duplicate equally-scored interaction profiles fail deterministically instead of silently selecting one.
- Unsupported Edit/Diff disables the action and never selects a view-only renderer as editor.
- Save handler failure retains dirty state; stale save acknowledgement cannot clear a later edit.
- Undo/redo history is reset on file or base-revision change and cannot cross files.

## Composition smoke

- Default FileTools registrations resolve browser and interaction catalogs without filesystem/Markdown packages.
- Adding FileSystem and Markdown registrations extends catalogs without replacing core services.
- Sandbox starts from FileTools references only.
- Future CanDoItAll composition test will resolve storage browse adapters, semantic scope providers, selected renderers, HybridCache decorator, and revision service.

## Browser proof

- Real headed/maximized first pass and screenshot review.
- Viewports/containers: large desktop, 720x520 dialog, 560x360/480x360 floating, 360px/narrow mobile.
- List and card modes; loading, error, empty, partial, pagination; context menu open state; file activation; interaction View/Edit/split/unsupported/save failure.
- Assert no horizontal overflow, actionable toolbar, readable breadcrumb, visible selection/status, correct overlay layering, and only intended scroll owner.

## Failing-first and proof artifacts

Behavior-changing critical subbundles must capture the same invariant failing before the production change and passing after it. Each manifest lists hashes, command transcripts, source assertions, anti-stub audit, and dependent smoke evidence.
