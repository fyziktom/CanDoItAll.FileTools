# SB01 Artifact-Backed Proof Manifest

- Status: `Completed`
- Owned requirements: R001-R004, R023, R029-R030.
- Owned raw notes: N001-N003, N015, N017.
- Semantic contract: `bundle://proof/SB01/semantic-invariants.md`.

## Changed-file hashes

All listed files were absent before SB01 unless noted.

| File | Before | After SHA-256 |
| --- | --- | --- |
| `repo://CanDoItAll.FileTools.slnx` | absent | `963d8fe6c67f171f6023a44e3fac0104865fcdc05385f5d8bb0010edbd9f2f61` |
| `repo://Directory.Build.props` | absent | `771865602fe6d3f95fd23b541ec2ac8b3684a2fe3c1dc4fea272e61997a0d5d9` |
| `repo://Directory.Packages.props` | absent | `91c9f8ce35172e9c73a7d06e68cb32284429b4000db48bd18bfb88d95f09833e` |
| `repo://global.json` | absent | `b9bc61b695fda5ac7992b1fbf43c13541bd0edb5f749d6f847e91ee1f21bc0ac` |
| `repo://README.md` | initial title only | `f3c51e0285d1c1c2fd3fb4a8d83952dc19c5874a2f0d8be51e74ed0f1014d2f7` |
| `repo://src/CanDoItAll.FileTools.Abstractions/CanDoItAll.FileTools.Abstractions.csproj` | absent | `8765d090d8ada4cde9ec927c0a65c9dc6f35b16932517660f4585c269177fd61` |
| `repo://src/CanDoItAll.FileTools.FileBrowser.Core/CanDoItAll.FileTools.FileBrowser.Core.csproj` | absent | `59e25f9143008c182e12e36e6780773d896b27bff6bf4afa7bf50f248767ab8f` |
| `repo://src/CanDoItAll.FileTools.FileBrowser.Components/CanDoItAll.FileTools.FileBrowser.Components.csproj` | absent | `c614245e0144a94ff3b0908e6099784dc39d176edce91a988f4f2e9c97a90b99` |
| `repo://src/CanDoItAll.FileTools.Providers.FileSystem/CanDoItAll.FileTools.Providers.FileSystem.csproj` | absent | `5647d3462b0a8210aff0c5d0608f096760e0cb59575a4806974646efb9c409a7` |
| `repo://src/CanDoItAll.FileTools.FileInteraction.Core/CanDoItAll.FileTools.FileInteraction.Core.csproj` | absent | `92d532f9cd8825f0b40be6b33a9ed09a9a341fc65ac534581226dbd9222a8d81` |
| `repo://src/CanDoItAll.FileTools.FileInteraction.Components/CanDoItAll.FileTools.FileInteraction.Components.csproj` | absent | `120c2246cc9c7b6a16ce7a4772aac9eacc2f4cb49d7c2dd683237c98328d2943` |
| `repo://src/CanDoItAll.FileTools.FileInteraction.Markdown/CanDoItAll.FileTools.FileInteraction.Markdown.csproj` | absent | `607fd79fb03cc641aeb2928c3e983a6479c7b9ee89af98a90555aefe4e8e9ecb` |

The remaining Sandbox/test project files are covered by the build and CodeAnalytics inventory; their hashes are retained in the command-session output and will be refreshed at final closure.

## Command transcripts

- Failing first: `bundle://proof/SB01/transcripts/failing-first-build.md`.
- Restore: `bundle://proof/SB01/transcripts/passing-restore.md`.
- Build: `bundle://proof/SB01/transcripts/passing-build.md`.
- Source assertions: `bundle://proof/SB01/transcripts/source-assertions.md`.
- Anti-stub: `bundle://proof/SB01/transcripts/anti-stub.md`.
- CodeAnalytics: `bundle://proof/SB01/transcripts/codeanalytics.md`.

## Architecture review

Status: `Pass`.

- Responsibilities are project boundaries rather than partial/nested types.
- Dependency direction matches the target; no cycle.
- Composition is limited to Sandbox.
- Direct test projects exist for extracted owners.
- No service locator, `BuildServiceProvider`, or cross-repository source reference.

## Downstream smoke

All later product/test assemblies and Sandbox compile against the new graph in the same Release build.
