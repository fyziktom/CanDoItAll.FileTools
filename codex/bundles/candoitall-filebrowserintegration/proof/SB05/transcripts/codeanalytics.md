# SB05 CodeAnalytics
Command: `CodeAnalytics MCP analyze FileBrowser.Components and query findings, diagnostics, dependencies, and cycles`
ExitCode: 0

- Snapshot: `snap-20260711173023-3ec305d8`.
- Snapshot created: `2026-07-11T17:30:23.6425504+00:00`.
- Scope: `repo://src/CanDoItAll.FileTools.FileBrowser.Components/CanDoItAll.FileTools.FileBrowser.Components.csproj`.
- Closure query time: `2026-07-11T18:11:56Z`.
- Dashboard correlation: `code-analytics_e3b34a5b40cf4e9d8bfc6f001999bf56`.
- Dependencies correlation: `code-analytics_6387ccb84ec543b588671e8359f00627`.
- Findings correlation: `code-analytics_ba7e39f86abe469a914ace05c49f0e16`.

Result:

```text
projects=1
documents=13
types=16
members=147
findings=0
openQuestions=0
hotspots=0
diagnostics=0
cycles=0
```

`FileBrowser.razor.cs` is 362 physical lines in the snapshot, down from the 534-line legacy code-behind. Focused collaborators own interaction dispatch/guard/policy, search debounce, and UI models. Direct `.csproj` inspection confirms RCL references only FileTools Abstractions/Core plus the ASP.NET framework; the single-project snapshot itself did not enumerate external project edges, so the manifest does not misuse it as reference-graph proof.
