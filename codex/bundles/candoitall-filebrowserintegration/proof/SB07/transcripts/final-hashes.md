# SB07 Final SHA-256 Integrity Set

- Run label: read-only `Get-FileHash -Algorithm SHA256` on the settled FileTools tree, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Command: `Get-FileHash selected final Interaction production, test, Sandbox, and Playwright files -Algorithm SHA256`.
- Exit code: `0`.
ExitCode: 0
- Limitation: these files were created in an uncommitted transfer worktree, so trustworthy pre-SB07 hashes do not exist. Final hashes are recorded; no synthetic before value is claimed. Bundle-record hashes are consolidated by SB08 after final edits.

```text
58d652262b630a035e3616bd1be9f513160c8c83a716dccbcc60a221c73b7f79  repo://src/CanDoItAll.FileTools.FileInteraction.Components/Components/FileInteraction.razor.cs
e33ec1b038a5f6d831f13c7a554cec7dbd147f53846799de0372b815565acd57  repo://src/CanDoItAll.FileTools.FileInteraction.Components/Components/FileObjectView.razor.cs
a4d8acda426a6e4e8573da294519b70ebc38413eb1d5de247b019dbb2e6dc41a  repo://src/CanDoItAll.FileTools.FileInteraction.Components/Components/FileObjectView.razor.js
e5d416408db403af5b8e1c6c51506ae6850d3823d357cac447940ece7c248e2e  repo://src/CanDoItAll.FileTools.FileInteraction.Components/Composition/FileInteractionBuiltIns.cs
efacf8c4557efefdb41768d92a7dd4af11d4ffb493bfbd9cb059840649207e86  repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/FileInteractionEditingRuntime.cs
6f6a4a2093bdf4cfa481dd52192ccd67aa2ab7e04578800bed4a73c91a812cdc  repo://src/CanDoItAll.FileTools.FileInteraction.Components/Models/FileInteractionSaveEventBridge.cs
634365d19852382a435ccf249d23e3d50d260c3c4ecf9bcaf0819da48aa0b7b2  repo://src/CanDoItAll.FileTools.FileInteraction.Core/BoundedTextHistoryProvider.cs
e570abd23bd796db22b961f37e4ab06cd1cb3ece6f1489ab33052134aa4dc8ca  repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileAutoSaveScheduler.cs
2dac4c2c905cc8e9472949fe9a185063d9a48ed4027da5f3ae0d52b8d74c561b  repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileInteractionEditCoordinator.cs
3ff49d68dd63ae1ee98a4d3ba3b395547dfedc674e02688317988214152f33ab  repo://src/CanDoItAll.FileTools.FileInteraction.Core/FilePreviewCoordinator.cs
907eecc63865fc496d9b6a9499074e40cf2bb01f5d18320814288fc11cf856e4  repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCompletionPublisher.cs
e3ce6ad93cebb516d0cde2b347725cb5e579be15f5e4a3d99a8e7c7cd4357c7d  repo://src/CanDoItAll.FileTools.FileInteraction.Core/FileSaveCoordinator.cs
425de5a5fb3366f2cdffc65a342651388a4abfdcbc2bed414f5251b0444ad358  repo://src/CanDoItAll.FileTools.FileInteraction.Markdown/Components/MarkdownFileView.razor
569c6fc17c669374136693b977609e201f3b5b8100244c5cfea392da053f0038  repo://src/CanDoItAll.FileTools.FileInteraction.Markdown/Composition/FileInteractionMarkdownExtensions.cs
8691ec42080712ccf4f36173b0cbf9f3d4d1245604cecbe0e499b4fab48fae9c  repo://src/CanDoItAll.FileTools.FileInteraction.Markdown/Rendering/MarkdownContentRenderer.cs
dc268a13fffe3efe8eb8f634b2ecb612858b64350274df4f5b13e768db348872  repo://tests/CanDoItAll.FileTools.FileInteraction.Core.Tests/FileSaveCoordinatorTests.cs
09bbf00ec8c6282500868957d12b667833ea2b16bffdf19ab0b8dafd892d5d71  repo://tests/CanDoItAll.FileTools.FileInteraction.Components.Tests/FileInteractionAdvancedInteractionTests.cs
0765ccfc209911beca996db9494303230313210e345c5483716f895ac9294950  repo://tests/CanDoItAll.FileTools.FileInteraction.Components.Tests/FileObjectUrlInteropTests.cs
e7b3b9fbacc1a244944e645c46e86f913d4c506c6558eff63750caf3ea1fe4c6  repo://tests/CanDoItAll.FileTools.FileInteraction.Markdown.Tests/MarkdownSecurityAndInteractionTests.cs
77bbe36af8a641e8c8e71115cd06164b6d23ebd275d1d60fa1e513ba0c6f901e  repo://samples/CanDoItAll.FileTools.Sandbox/Components/Pages/InteractionLab.razor
8cea7282f7f6a2dde90494dcbe935423a692633858e9d2e7a944935381ff6d20  repo://samples/CanDoItAll.FileTools.Sandbox/Demo/SandboxInteractionComposition.cs
f8358acaf0ab62a49f75ddbf0062227d37483741e79d9fedc045362917435236  repo://samples/CanDoItAll.FileTools.Sandbox/Demo/SandboxInteractionGateway.cs
```

```text
7d3a591b10d8c1d8511c32ffa9a0a9eb4159a40cd91beb8913e594916104b224  repo://output/playwright/sb07/interaction-markdown-rendered-1440x900.png
46d539a5c99b975ea589d1379a5c750abe208824962ab10c70442267d836def5  repo://output/playwright/sb07/interaction-edit-preview-720x520.png
e2d5ad6a256f1847c07804ece658f7bce675ae1ebdde067c3ddf6fd0e594d99a  repo://output/playwright/sb07/interaction-autosave-clean-720x520.png
49c3d3c75479629dcf06cdf4d8045f6542fc533d3076f71061ba2411a49dfb4c  repo://output/playwright/sb07/interaction-mermaid-560x360.png
ca53801a6b25f516f8291a60f001b9d85bc30ecb68ecf43a40f4db6822bf7f64  repo://output/playwright/sb07/interaction-binary-limit-480x360.png
0b4b82f3eab972549273e826c2b6dbaffc8b131d267ac7e50349bdfab0c3f7c7  repo://output/playwright/sb07/browser-markdown-overlay-390x844.png
```

The transcript intentionally excludes its own hash.
