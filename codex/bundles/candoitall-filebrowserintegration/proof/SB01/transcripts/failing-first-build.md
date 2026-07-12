# Failing-First Build — INV-SB01-BOUNDARY

- Run label: `SB01-before-scaffold`
- Date: `2026-07-11`
- Working directory: `C:\repositories\CanDoItAll.FileTools`
- Command: `dotnet build . -c Release`
- Exit code: `1`

```text
MSBUILD : error MSB1003: Specify a project or solution file. The current working directory does not contain a project or solution file.
```

This is the expected failing-first proof: the repository had no compilable product boundary before SB01.
ExitCode: 1
