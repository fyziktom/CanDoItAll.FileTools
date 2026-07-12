# SB05 Source Assertions
Command: `inspect RCL source for host-only dispatch, render freshness, accessibility, and dependency direction`
ExitCode: 0

- Run label: `2026-07-11 read-only closure source audit`.
- Working directory: `C:\repositories\CanDoItAll.FileTools`.
- Exit code: `0` for all assertions below.

## Dependency direction

`repo://src/CanDoItAll.FileTools.FileBrowser.Components/CanDoItAll.FileTools.FileBrowser.Components.csproj` contains only:

```text
FrameworkReference: Microsoft.AspNetCore.App
ProjectReference: CanDoItAll.FileTools.Abstractions
ProjectReference: CanDoItAll.FileTools.FileBrowser.Core
```

No Components.BaseLib or CanDoItAll main reference is present.

## Host-only effect audit

Command:

```powershell
rg -n -i "href\s*=|download\s*=|navigator\.clipboard|window\.open|NavigationManager|IFileBrowserActionProvider|ExecuteActionAsync" src\CanDoItAll.FileTools.FileBrowser.Components -g '!bin/**' -g '!obj/**'
```

Output: `No direct browser/provider effect path found in the RCL.`

`FileBrowserHostBoundaryBehaviorTests` additionally proves awaited `ItemInvoked`/`ActionRequested`, folder-only navigation, and zero session action execution.

## Independent repair assertions

- `FileBrowser.razor.cs` creates select/activate/action callbacks through `FileBrowserInteractionDispatcher` using the currently rendered session/snapshot.
- `FileBrowserInteractionDispatcher` re-resolves the current item and checks `FileBrowserInteractionStamp` around asynchronous work.
- `FileBrowserItemActions.OnParametersSet` invalidates and cancels state when session, item key/object, source, snapshot revision, or disabled state changes.
- list/card primary controls are native `<button type="button">`; browser-generated Space click reaches activatable non-selectable items.
- no-source root uses `has-no-sources` and conditionally omits toolbar/location/search/refresh.
- item action overlay is `popover="auto"`, `role="group"`, and uses ordinary buttons; no `role="menu"`/`role="menuitem"` is present.
- warnings use `@key="warningIndex"`, preserving duplicate identities as separate occurrences.
- `.ft-file-browser__card-scroll` is the result overflow owner in the repaired low-height card layout.

## Asset isolation

```text
wwwrootExists=False
jsCount=0
razorCss=FileBrowser.razor.css
```

The RCL installs no global script or body/document handler.
