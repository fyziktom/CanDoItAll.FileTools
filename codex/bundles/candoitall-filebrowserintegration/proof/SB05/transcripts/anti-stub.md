# SB05 Anti-Stub Audit
Command: `rg RCL production source for TODO, FIXME, NotImplemented, direct effects, and global assets`
ExitCode: 0

- Run label: `2026-07-11 final production audit`.
- Working directory: `C:\repositories\CanDoItAll.FileTools`.
- Command:

```powershell
rg -n -i "TODO|FIXME|NotImplementedException|throw new NotImplemented|fixture-specific|template-only" src\CanDoItAll.FileTools.FileBrowser.Components -g '!bin/**' -g '!obj/**'
```

- Exit code normalized to `0` after the expected no-match result.
- Output: `No anti-stub markers found.`
- Additional audit: no RCL `wwwroot`, no JavaScript files, and one component-scoped `FileBrowser.razor.css`.
- Result: Pass. No template-only behavior or test-only production branch was accepted as SB05 implementation.
