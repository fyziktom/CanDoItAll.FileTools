# SB01 Semantic Invariants

## Validator contract record

- Invariant ID: `INV-SB01-BOUNDARY`
- Source raw note: N001-N003 and R001-R004 require a standalone, lightweight, selectively consumable FileTools boundary.
- Expected behavior: the solution and seven product projects point only inward, and Abstractions has no dependency.
- Disallowed shallow implementation: a renamed copy that still references Components, main CanDoItAll, or all optional renderer/provider packages.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-build.md` proves no project/solution existed.
- Passing test: `bundle://proof/SB01/transcripts/passing-build.md` proves the scaffolded graph builds.
- Changed source files: `repo://CanDoItAll.FileTools.slnx` and product project files listed with hashes in `bundle://proof/SB01/manifest.md`.
- Production assertions: project-reference/package-reference inspection enforces the intended one-way graph.
- Red-team negative case: scan every product project for Components/main references and verify Abstractions has zero references.
- Downstream dependency check: SB02 contracts and every later product compile on this graph.

## INV-SB01-BOUNDARY

- Source raw note: N001/N002/N003/N015 — FileTools must own lightweight selectable parts without main/heavy dependency drag.
- Expected behavior: the standalone solution builds with Abstractions as a zero-dependency leaf and every implementation/UI edge pointing inward.
- Disallowed shallow implementation: a compiling solution whose contracts reference UI/implementation/sibling repositories, or a folder skeleton claimed as feature completion.
- Failing-first proof: `bundle://proof/SB01/transcripts/failing-first-build.md`.
- Passing proof: `bundle://proof/SB01/transcripts/passing-restore.md`, `bundle://proof/SB01/transcripts/passing-build.md`.
- Changed source: project/hash table in `bundle://proof/SB01/manifest.md`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.md`.
- Red-team negative: forbidden namespace/reference searches return no production match; Abstractions reference search returns no match.
- Downstream dependency check: all later product/test/Sandbox assemblies compile against the graph.

## Shallow-pass trap

Compilation alone could pass even if optional packages referenced each other incorrectly or Abstractions acquired ASP.NET. The source and CodeAnalytics reference assertions make that shallow implementation fail.

## Adversarial negative proof

The forbidden-reference audit explicitly searches the production graph for main/Components namespaces and lower-boundary references. An accidental ProjectReference/PackageReference in Abstractions would appear in the transcript and block closure.

## Semantic positive proof

A clean restore and Release build produces every planned assembly and the Sandbox from FileTools alone; CodeAnalytics reports the intended 14-project acyclic graph.

## Anti-stub audit

See `bundle://proof/SB01/transcripts/anti-stub.md`. SB01 claims boundaries only, not unimplemented features.

## Raw-note literal closure

The graph realizes “smaller parts ... without taking all heavy dependencies” at compile time. The actual browser/interaction behavior remains correctly open in later subbundles.
