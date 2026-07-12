# Scope Inventory

## Transfer inventory

| Source project | Target responsibility | Disposition |
| --- | --- | --- |
| `CanDoItAll.Components.FileBrowser.Core` | Models/provider contracts -> Abstractions; search/session/tree/navigation -> Core | Transfer, namespace/package rename, responsibility slicing |
| `CanDoItAll.Components.FileBrowser.Providers.FileSystem` | Root-confined example adapter | Transfer to optional FileSystem project; depend on Abstractions only where practical |
| `CanDoItAll.Components.FileBrowser.BaseLib` | FileBrowser RCL | Transfer and remove direct-effect actions; add explicit density/chrome modes; eliminate cross-repo source dependency |
| Three FileBrowser test projects | Characterization and boundary proof | Transfer with tests kept near target package boundaries |
| FileBrowser sandbox | Standalone visual/composition proof | Transfer and expand with floating, minimal, interaction, save, error, empty, and unsupported scenarios |

## New FileTools responsibilities

- Storage-neutral identity, items, capabilities, pages, filters, sorts, metadata, content leases, provider interfaces, file revisions, interaction modes, persistence requests, autosave/preview policies, history contracts.
- Browser provider catalog, provider validation, navigation, search strategies, bounded/disabled session state retention, session orchestration.
- Interaction profile catalog and deterministic match scoring.
- Text editing state, bounded history, save scheduling, preview scheduling, dirty/conflict/error state.
- Blazor FileBrowser and FileInteraction shells plus explicitly registered renderer descriptors.
- Basic renderer/editor implementations that do not force Office/IPFS/FTP dependencies.
- Root-confined, symlink-aware local filesystem example provider.

## Out-of-scope production implementation

- CanDoItAll database schema/migrations and driver changes.
- Project-card buttons, tabs, canvas toolbar/floating windows, process-run pages, resource-module UI.
- Production IPFS/FTP adapters in FileTools.
- Full Office, spreadsheet, media editing, collaborative editing, or diff engine.

## Proof inventory

- Unit/negative tests per project.
- Composition smoke resolving catalogs/session/renderer registrations.
- Filesystem traversal/symlink/race/cancellation/read-range tests.
- Component contract tests for host-only file activation and effects.
- Browser screenshots and assertions for desktop, compact floating, low height, and narrow/mobile.
- CodeAnalytics snapshots before/after with project reference and cycle proof.
- Source removal manifest for Components.

