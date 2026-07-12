# SB08 Failing-First Transfer Ownership

- Run label: Components ownership inventory before guarded cleanup, 2026-07-11.
- Working directory context only: Components repository at branch `file-explorer`, baseline HEAD `b5e642a7d5f15e7378b7ad8993712dc96632b9a7`.
- Command: `git ls-files legacy FileBrowser production, tests, sample, docs, and release validation paths; verify cleanup precondition`.
ExitCode: 1

Output: exactly 107 tracked legacy FileBrowser ownership files were present before cleanup. This intentionally fails `SB08-INV-01`: FileTools proof existed, but Components still owned production Core/BaseLib/FileSystem projects, tests, sample/docs, and release validation entries. Cleanup was therefore required and was not performed before FileTools gates passed.

The failing-first evidence is the ownership state, not a fabricated compile failure.
