# Failing-First Contract Baseline — INV-SB02-CONTRACTS

- Run label: `SB02-before-contracts`
- Date: `2026-07-11`
- Working directory: `C:\repositories\CanDoItAll.FileTools`
- Evidence: CodeAnalytics snapshot `snap-20260711140220-e81d3243`.
- Result: the Abstractions project contains zero declared domain types; all seven detected types in the solution are generated test/Sandbox entry points.

The intended provider/content/interaction/save/history contracts therefore do not exist before SB02. The passing proof must demonstrate semantic validation/lifetimes as well as file creation.
Command: `CodeAnalytics MCP inspect Abstractions before contract implementation`
ExitCode: 1
