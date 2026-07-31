# FileInteraction extension guide

FileInteraction starts after a host has authorized and resolved a file. It consumes a `FileInteractionRequest`, an independent `IFileContentSource`, and an immutable component composition. It never depends on a FileBrowser session.

The shell bounds full-content loading with `MaximumContentBytes` (16 MiB by default) and reports an error instead of allocating beyond that limit. The same limit is enforced before accepting every text or binary editor change: an oversized replacement is surfaced but does not replace content, advance the edit revision, or change the existing dirty state; a later valid edit can continue. Keep metadata-only renderers metadata-only. Route larger streaming formats to a separate host-owned viewer until an appropriate bounded streaming contract is added; do not raise the limit without an application memory budget.

## Composition and resolution

Use `FileInteractionComponentBuilder` to register profiles, history factories, and renderer descriptors. `AddBuiltIns` supplies text view/edit, raster-image view, browser-native PDF view, an SVG browser-frame view, and a browser-frame fallback; both frames use an empty `sandbox` capability set. SVG is deliberately excluded from the raster profile. `AddMarkdown` contributes the optional higher-priority Markdown profile and renderer.

Profile resolution considers requested mode/capability and type evidence. Exact media types score above media-type wildcards, exact extensions, and fallback patterns; priority breaks ties at the best match kind. An equal final result is reported as ambiguous rather than selected arbitrarily.

```csharp
services.AddFileInteractionComponents(builder => builder
    .AddBuiltIns()
    .AddMarkdown()
    .AddProfile(myProfile)
    .AddRenderer(myRenderer));
```

Registrations are explicit: an application that uses only images can build a smaller composition instead of calling `AddBuiltIns`.

## Add a renderer

1. Define a `FileInteractionProfileDescriptor` with matching extensions/media types, supported capabilities, priority, and optional autosave/preview/history defaults.
2. Create a Blazor component implementing `IFileInteractionRendererComponent`. Its writable `[Parameter] FileInteractionRenderContext Context` receives request metadata, bounded content, mode, edit revision, and `MaximumContentBytes` so editors can preflight replacements. Text editors can raise the `TextChanged` convenience callback; binary or specialized editors can raise a defensively copied `FileInteractionContentChange` through `ContentChanged`. The shell enforces the limit again before accepting either callback.
3. Add one `FileInteractionRendererDescriptor` per profile/mode. Choose `Text` or `Binary` and declare `MetadataOnly` only when the renderer does not need bytes.
4. Register the profile before building the immutable composition. A renderer that names an unknown profile is rejected.

Custom renderers should keep application effects event-up. They must not directly download, mutate storage, open local paths, or treat content as trusted markup. The built-in browser-native PDF `<object>` is an explicit boundary: embedded links/actions are controlled by the browser/PDF viewer and are not converted into FileInteraction host callbacks. A host that must mediate every PDF action should register/use a different renderer or route PDFs elsewhere. Collocated JavaScript modules and CSS isolation keep type-specific behavior scoped; clean up object URLs, listeners, and module references on replacement/disposal.

`FileObjectView.TargetFrame` is the host-composition seam for decorating the actual `<img>`, `<object>`, or sandboxed `<iframe>`. Its `FileObjectViewTargetFrameContext` supplies the `Kind` and the `TargetContent` fragment. A host renderer can place that fragment inside a zoom/pan component while `FileObjectView` continues to own bounded bytes, object-URL replacement/disposal, loading and error state, and browser-frame security attributes. Render `TargetContent` unchanged and exactly once. SVG adapters must retain `FileObjectViewKind.Browser`; do not replace the sandboxed frame with inline SVG.

## Controlled mode

`FileInteractionRequest.Mode` is the host-authoritative value. When an internal View/Edit control requests a change, the component awaits `ModeChanged` before publishing the resulting state. A controlled host should update its request model and render a new `FileInteractionRequest` carrying the accepted mode. A later parent render with the previous mode restores that mode; set `AllowModeSwitch` to false when the host does not offer in-shell switching.

## Editing and history

Edit mode is available only when the resolved profile and renderer support it. The included bounded text history stores snapshots under both entry and byte limits. A custom `IFileEditHistoryProviderFactory` can select a file-type-specific history provider; returning no provider disables undo/redo.

History factories expose `IFileEditHistoryProviderFactory.Priority`. The highest matching factory wins; equal highest priorities are an explicit ambiguity instead of depending on registration order. The included bounded text factory uses priority -100 as a generic fallback, allowing a file-type-specific factory to override it.

History is scoped to the file handle/base revision. Undo followed by a new edit truncates redo. A file switch, external replacement, or conflict resolution must not carry history into the new revision.

`FileInteractionState` reports lifecycle, edit revision, dirty/saving/conflict state, undo/redo availability, and errors. Hosts should use `StateChanged` for close guards and window chrome instead of inspecting renderer internals.

## Save and autosave

Manual save is the baseline for editable/savable profiles. Autosave can combine interval, idle-after-change, edit-change-count, and cumulative changed text-unit triggers through validated `FileAutoSaveOptions`. `FileAutoSaveTriggers.TextUnitCount` uses the `textUnitCount` threshold and measures changed UTF-16 code units after removing the unchanged prefix/suffix; it is not a Unicode grapheme count. Saves coalesce and at most one persistence attempt runs for an edit session.

`SaveRequested` is an awaited request/response callback. The host reads `FileSaveRequest.Content` through its replayable stream factory, persists using `ExpectedRevision`, and optionally supplies the new revision:

```csharp
private async Task PersistAsync(FileInteractionSaveRequestedEventArgs args)
{
    FileContentRevision persisted = await storage.SaveAsync(
        args.Request.File,
        args.Request.Content,
        args.Request.ExpectedRevision);

    args.SetPersistedRevision(persisted);
}
```

The sample `storage` is a host abstraction, not a FileTools service. Manual and automatic saves use the same callback and both await host persistence completion before being treated as successful. Let persistence exceptions flow back through the callback. A failed/conflicting save stays dirty, and completion for edit revision N cannot clear changes made at N+1.

### Conflict retries

When persistence throws `FileSaveConflictException`, **retry against current revision** updates `ExpectedRevision` to the exception's `ActualRevision` and resubmits the existing local edit snapshot. It does not reload remote content, compute a three-way merge, or reconcile changes. **Retry without revision** clears the expected revision and resubmits that same local snapshot as an overwrite attempt.

The host owns conflict policy and must authorize every retry in `SaveRequested`, especially a request whose `ExpectedRevision` is null. Reject overwrite when the current principal/scope does not allow it. If the product requires reload, comparison, or merge, implement that host workflow explicitly and replace/reopen the interaction with the chosen revision; do not describe the rebase retry as a merge.

## Preview and future Diff

Profiles may enable a debounced split preview beside or below the editor. Preview results carry the edit revision plus the edited media type and encoding; stale work is discarded. Configure longer debounce for expensive renderers. The current built-ins use identity preview content and Markdown renders that content through its view renderer.

`FileInteractionMode.Diff` and the Diff capability are reserved extension points. No built-in Diff renderer is shipped, so hosts must not advertise it until a registered profile/renderer supports it.
