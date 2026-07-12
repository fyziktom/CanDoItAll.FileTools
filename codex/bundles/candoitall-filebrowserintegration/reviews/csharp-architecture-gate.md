# C# Architecture Gate

## C# Architecture Gate Result

Status: `Pass with explicit future-integration follow-up`

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| None | Abstractions remains the dependency floor | direct project/package audit, 21 tests, SB01/SB02 manifests | none |
| None | Browser runtime was sliced instead of copied as a monolith | 275-line/33-member session versus 1,315-line/89-member legacy; `snap-20260711162248-cd589ee2`; 132 tests | none |
| None | Browser and interaction UI execute no host storage/file effects | host-event tests, source audits, SB05/SB07 production matrices | none |
| None | FileInteraction policy, rendering, optional Markdown, and filesystem adapter remain separately consumable | project graph, 175 interaction tests, 83 filesystem tests, package validator | none |
| Reviewed residual | broad final snapshot sees generated duplicate types and four cohesive-file complexity warnings | `snap-20260711202431-5dbb5110` | keep focused snapshots/tests as semantic gates; do not call the broad snapshot warning-free |
| Reviewed baseline | two unchanged Components test baselines fail outside cleanup scope | `proof/SB08/transcripts/components-cleanup.md` | repair separately in Components; production WAE builds and package ownership pass |
| Required future phase | CanDoItAll adapters/cache/revision/module UI are design-only | main source diff 0; CI1-CI13 in integration architecture | refresh source/CodeAnalytics at re-entry and implement in a separate run |

### Dependency direction

The shipped product graph is acyclic:

```text
Abstractions
├─ FileBrowser.Core
│  └─ FileBrowser.Components
├─ Providers.FileSystem
└─ FileInteraction.Core
   └─ FileInteraction.Components
      └─ FileInteraction.Markdown (optional Markdig owner)
```

- Abstractions has no project, package, framework, Blazor, filesystem, cache, persistence, storage SDK, or CanDoItAll dependency.
- Browser Core references Abstractions only; Browser Components references Browser Core/Abstractions plus the ASP.NET Core framework.
- filesystem references Abstractions only and is not pulled into browser/interaction base packages.
- Interaction Core references Abstractions only; Components references Core/Abstractions; optional Markdown references the interaction layer and alone owns Markdig.
- no FileTools product references CanDoItAll or `CanDoItAll.Components`.
- the final seven-product snapshot has 743 dependency edges and zero cycles. Focused final Core/Components snapshots have zero diagnostics, Warning/Error findings, or cycles.
- Components did not gain a FileTools dependency; its old FileBrowser project/solution/CI/release ownership was removed after proof.
- the future main direction remains `module/composition adapters -> FileTools`; Infrastructure-native browse sidecars stay FileTools-free, preventing a Foundation-to-UI/domain reverse edge.

### Responsibility check

- `FileBrowserSession` is a thin orchestration facade; generation execution, source transition, browse/search mode, revision retirement, continuation history, loading, navigation, search, selection, actions, and state stores have top-level owners.
- `FileBrowser.razor.cs` remains a rendering/orchestration owner and delegates dispatch freshness, actions, search debounce, and projections; host effects are emitted upward.
- `FileInteraction.razor.cs` is 348 lines after mode, edit command, save/preview bridge, render factory, runtime/binding, and UI-state extraction.
- `FileSaveCoordinator.cs` is 342 lines and delegates failure-isolated observer publication to `FileSaveCompletionPublisher`; autosave scheduling and edit/history/preview policies have separate owners.
- `FileBrowserStateStore.cs` contains the intentionally separate disabled and bounded store implementations; `FileSystemFileBrowserProvider.cs` remains a cohesive simplified provider with a shared root-confined reader. Their broad-snapshot complexity warnings are reviewed, not hidden.
- no broad manager/helper, nested architecture boundary, or duplicate moved responsibility was accepted.

### Construction check

- composition uses explicit builders/catalogs and immutable registrations.
- no production `BuildServiceProvider` call or broad `IServiceProvider` injection exists.
- builders register policy/renderer/history factories; they do not invoke storage/provider I/O.
- built-in text/image/PDF/inert profiles and the optional `.AddMarkdown()` extension are explicit; consumers do not pay for unselected packages.

### Partial-class policy

No new runtime partial class was added. Cohesive Razor code-behind is used only for component lifecycle/render orchestration. Policies, schedulers, catalogs, histories, content loading, object-URL lifecycle, and provider/runtime behavior are top-level testable types.

### Testability proof

- FileTools full result: 433/433 tests — 21 Abstractions, 132 Browser Core, 43 Browser Components, 83 filesystem, 59 Interaction Core, 72 Interaction Components, 23 Markdown.
- isolated negative tests cover validation/lifetimes, retention/invalidation/source transitions, traversal/links/races/ranges, stale callbacks, host-only effects, save failure/conflict/cancellation/replacement, resolver/history ambiguity, stale preview, unsafe Markdown/SVG/unknown/image/PDF, object-URL ownership/readiness, and content limits.
- explicit composition smoke proves base-only, built-in, optional Markdown, custom renderer, registered Diff, and filesystem-content paths without a full CanDoItAll host.
- real Sandbox browser proof is required in addition to unit/component tests; SB05 is closed and SB07 final technical proof is separately recorded.
- package validation proves the compiled dependency/asset result, not only source project declarations.

### Extension seam check

- a new browser source implements neutral provider/content contracts; it does not edit the browser session/RCL.
- a new viewer/editor/history/preview capability registers a profile/renderer/factory; it does not edit the shell switch or require service location.
- Diff is a reserved explicit mode and can be registered without a base diff dependency.
- host save remains an awaited callback, so storage drivers stay in the consuming app.
- future FTP/IPFS/CanDoItAll native sidecars remain outer adapters; no base FileTools package needs those SDKs.

### Pattern-selection conformance

Implementation matches `architecture/03-csharp-pattern-selection-records.md`: Ports and Adapters for storage boundaries, Strategy for retention/save/preview/history, Decorator for future host cache, Registry/Factory for profiles/renderers/history, Observer/EventCallback for host effects, and explicit state coordinators for lifecycle ordering. No service-locator or all-in-one package shortcut replaced those decisions.

### Closure decision

The shipped FileTools architecture and guarded Components ownership removal pass. No architecture blocker remains for bundle closure. CanDoItAll production integration is deliberately not passed by this gate: it is the CI1-CI13 follow-up and must supply fresh graph/source/security/cache/revision/UI proof when the refactoring stabilizes.
