# Source Assertions

The final scoped source/dependency audit established:

- FileBrowser.Core references Abstractions only and contains no UI, filesystem-adapter, main CanDoItAll, or storage-driver dependency.
- No package reference, project cycle, diagnostic, runtime partial, service locator, `Timer`, or `Task.Run` orchestration was introduced.
- Async invalidation is public at item, source, and all-state scopes and is backed by source generations rather than cache deletion alone.
- Source transitions, operation execution, browse/search modes, generation retirement, and continuation history have named top-level owners.
- Disabled and bounded retention are explicit strategies; active render state is not misreported as reusable cache state.
- The 275-line session delegates rather than duplicating those owners' algorithms.

Result: **Pass**.
