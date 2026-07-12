# SB05 Final SHA-256 Integrity Set
Command: `Get-FileHash final RCL, tests, Sandbox, and accepted screenshots -Algorithm SHA256`
ExitCode: 0

- Generated read-only from the final FileTools tree and bundle records on `2026-07-11`.
- FileTools was an uncommitted transfer worktree, so trustworthy per-file pre-SB05 hashes were not available. The initial legacy Components baseline is retained in `legacy-baseline-tests.md`; no synthetic before hash is claimed.
- Paths below are portable `repo://` or `bundle://` references.

## RCL production

```text
8b22819a44079e500e89f54186c3182fa5f9e01570da2d256d82db048425a5f5  repo://src/CanDoItAll.FileTools.FileBrowser.Components/_Imports.razor
c614245e0144a94ff3b0908e6099784dc39d176edce91a988f4f2e9c97a90b99  repo://src/CanDoItAll.FileTools.FileBrowser.Components/CanDoItAll.FileTools.FileBrowser.Components.csproj
6ac11b8f7c23e0c0acd2da9518dca759ff4c895fa1438309410c50c27c3a6019  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor
113ee2f0b92b32a460d8ca25b959dea7d41b59bf9e7643e92afaa7c4e9f5b1fe  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor.cs
16444e040023702234bf99f53ca4fd8613f799a7a2f05d0a917297c0fe9f0446  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowser.razor.css
8e17d36745ea42785dad0e065479648d79063425597d93176733044fd04110f6  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserBreadcrumbs.razor
a8fadf4b708b31afabfa998100753a11c3671295beed9951ef47e0ac97ad1856  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserCardView.razor
feb54f38bf276b5df96b3995740e8ca61d452aed62418c3d72ff0b14fb75c0ea  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserItemActions.razor
f8d17c0a3d2f43d284fe206ebe52b10a057878f2599f8681dff714f555ae738e  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserItemActions.razor.cs
59ae7b8284716586da05e5d7680407102473b04e619a891c1ea1b20d250800d1  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserListView.razor
9df42aec608325ea50065a9c5479738c9e843bbb55ab9d8574d68ed0c26072fc  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserSourceNavigation.razor
5ea2628f67afe991cbcd25f1f1d675dcf479582f2376395371e7aaaa5367a3c1  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserStatus.razor
07e111c312ce63666c74c5a71c4c57836bcdbc6c67beff3f2971f8c8cc23ad80  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Components/FileBrowserToolbar.razor
9eb96906db8a9b6720f9fc980af64b090c74efeab13525c08948d73070b2a49d  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionDispatcher.cs
1d11d6b6604abdd3a7bda5c271f28db02eacea1b96dd324be01e2f0f5c6fc345  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionGuard.cs
6118db4ec5a478c763ad3a98724b8b52b5ee0bc1c2c76b6acb05ddc9800e1e8b  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserInteractionPolicy.cs
3d4cea6f39266fddd33945f679b8135ce4486903c01ad26f9a05e2cea7357126  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserSearchDebouncer.cs
470e7ee4beb8291f30d12c4f58785c748e914d19636760a04d80461a6b9da12e  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Models/FileBrowserUiModels.cs
921744ad6453bcd96e2127a1792b06cb55468e713c38fe7841fd9af41dd6b132  repo://src/CanDoItAll.FileTools.FileBrowser.Components/Properties/AssemblyInfo.cs
```

## Component tests

```text
e31570e10ab0c89420dddff3a60b0a4c2c038f73f807e6b64ee0c373da2a3772  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/CanDoItAll.FileTools.FileBrowser.Components.Tests.csproj
1b6afacdb0086d9aaea0d02216234818f63e203c1ad698b94c24ea3fe36f273f  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserComponentContractTests.cs
dbc0911571e1e3919257720da93dda52c0573dba08894311c9dd60844216273a  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserDisplayFormatterTests.cs
8645e09bbf2f4ed621ad93dc2b0634a20d2d8b1a6b12f9b225f2f4ca20dccf72  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserHostBoundaryBehaviorTests.cs
ccbef361bc47138bb075e73157c811354468b7285451b1b0671e749385faf2a3  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserInteractionGuardTests.cs
c6d9019b1fea07df9a7006037f67a5172b64541dd29334b55a349c6bc1527b7c  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserInteractionPolicyTests.cs
6a107a110a0f5cee39a82e38af62db8c8b5d2efb872d0aa07128facd17cc9307  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserRenderedComponentTests.cs
d916a2271032092490c67148ad90dddd6b612464274a5bc211c4bafaca9c29ef  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/FileBrowserSearchDebouncerTests.cs
75c155e6ace0079f16c7402bbf94ccec5c9c9f675fc69a0c2ed020d751ee6530  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/GlobalUsings.cs
ed0c150671c04b492895817327d51641d7d68ee589ff44628d22ac3c4694b52e  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/RenderedFileBrowserTestSession.cs
2dbd9f9d63b6f2afca8385ab3148456279d7f827c777f8a1e8c0d6b717469551  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/TestFileBrowserItemFactory.cs
2eb11d31849a39376baa96547611eb8db1957d613803d87b79161b9140cdf15e  repo://tests/CanDoItAll.FileTools.FileBrowser.Components.Tests/TestPaths.cs
```

## Sandbox behavior owners and final visual artifacts

```text
f28710e7e4ba1dd6130f512b2d0387886ab2cfa5c8b6dc25b73b0b5a2ee5ecbb  repo://samples/CanDoItAll.FileTools.Sandbox/Components/Pages/BrowserLab.razor
337fde8f1f962edc33314fcd0fe0c4fc086ac34cf54832404a83de9e12078662  repo://samples/CanDoItAll.FileTools.Sandbox/Demo/DemoFileBrowserProvider.cs
e8cd8e9c240ab462c669cbf0ffd29908c353d99ede733e1d45982e5799f1eec3  repo://samples/CanDoItAll.FileTools.Sandbox/Demo/SandboxBrowserScenario.cs
1a66bd6111c32ce819f84d521b9e91b43202547904fc42e9ab70521687f0a901  repo://samples/CanDoItAll.FileTools.Sandbox/Demo/SandboxBrowserSessionFactory.cs
64fdee568a7065e89e364f749a95279600f5f09c9d3b55bcc1df17d983741be8  repo://samples/CanDoItAll.FileTools.Sandbox/wwwroot/app.css
54f6d7d87f8aa224f98d70c74d2fb00f6d2e9cc7a69bd3cfe6d07227b05b704b  repo://output/playwright/sb05/repaired-minimal-cards-480x360-final2.png
de05e1c8a77d6ec91071536bb005358e1361fc1e9a5a7b105316e977dfe08f77  repo://output/playwright/sb05/repaired-minimal-cards-390x844-final2.png
```

## Bundle closure records

```text
2113824b137f89218456fe2bf5f025f3660c6dc56fec93f84327629fce04c047  bundle://README.md
b926da8b6d8beafd6ac1f31b8994fb76af4f3fece48e14c891ebb57164c7e4ab  bundle://plan/architecture-checkpoints.md
d69aae8fd97041fe36b6a2a9f31c8ec84af13ee2c0848796ba19fb272014a3b5  bundle://subbundles/05-responsive-filebrowser-component-and-sandbox/README.md
cf539caa406468c4296b892e4227ed4512799dc3ceaf88cd83fefb2d2c1c79aa  bundle://subbundles/07-basic-viewers-editing-workflow-and-history/README.md
403ced4c9f44ee549f27457fb72c7c9c34014250f7687f68e9f3ce0c7195cbe2  bundle://reviews/01-execution-report.md
ba964b43ca5b96d501fe51765e40be6ae62d283feb467024b5788936ceca938e  bundle://reviews/csharp-architecture-gate.md
0c8d3479186f6d1ce30881896955dcc02ffea0c147b2bf23bbead9ce9ac64e09  bundle://traceability/01-requirement-traceability.md
d06c35d14d8bc8cb5c205fa7db164490480f79883955d9e524c197505303fd31  bundle://traceability/02-input-coverage.md
9c598750e8ac8a262a1dcf027598c1ce91a604aa3493c696c3ef2a979ffa9610  bundle://proof/SB05/entry-gate.md
740dbd096e43541564d6611873b479c3ec1e67a9c93ec80c8aaf6a2ddbe9d868  bundle://proof/SB05/manifest.md
2b5407e1991df0c9acf555f60d2d3b47041180a9701dbb0e1d382d155aea396c  bundle://proof/SB05/semantic-invariants.md
6bcfd890eef4a8d2decc74b9ff7d9ee84d1689d7b15d9016a05b9d31ff64d4b3  bundle://proof/SB05/transcripts/failing-first.md
3134e0ae15007fc70bc8a6480c592ba122d748020f98244748c58e1c986ff543  bundle://proof/SB05/transcripts/passing-component-tests.md
3a7328d515d6834b1c8cd521c65bc027db610a7c0ba3cb5a51e81ca1b2df9c4f  bundle://proof/SB05/transcripts/passing-build-and-format.md
7d38c9ead035525eab5301e7aed0446f65b29ce2adba31663c947d9856bdcc6d  bundle://proof/SB05/transcripts/codeanalytics.md
4c3954788f9aefdf1eca7a3e639d6d2906a82d3b4cb73377cacb74df791c5cdd  bundle://proof/SB05/transcripts/source-assertions.md
d2b71a6500fe81d56198e5d321c8b15bc7d58fdd0bee14d29aa6ab781e42a3de  bundle://proof/SB05/transcripts/anti-stub.md
fc7a5f20d8eed16f06cd928ec5c6d78493ad322bce9bcce367e5b6f81359d6be  bundle://proof/SB05/transcripts/browser-validation.md
ec7c46b9e3d673a5a3ad706f1a869f93acf4822a6ae92c11cc3c5a79e086cad2  bundle://proof/SB05/transcripts/visual-review.md
63908fb2b984fc664e0cef899a3e12b438129a04eb7afcd6fc844ffc4621799e  bundle://proof/SB05/transcripts/dependent-smoke.md
```

The hash transcript intentionally excludes its own hash to avoid a self-referential value.
