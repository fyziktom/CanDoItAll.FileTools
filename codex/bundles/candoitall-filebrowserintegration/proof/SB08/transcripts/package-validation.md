# SB08 Package Validation — SB08-INV-01

- Run label: final deterministic package proof, 2026-07-11.
- Working directory context only: `C:/repositories/CanDoItAll.FileTools`.
- Command: `scripts/pack-release.ps1 then scripts/validate-packages.ps1 with final expected SHA-256 manifest`.
ExitCode: 0

Output: validated exactly 7 packages and 7 symbol packages. Validation checked project and packed dependency sets/versions, net10 assemblies/XML docs/PDBs, MIT/authors/readme, absolute package-readme links, RCL static-web-asset metadata and isolated CSS, collocated object-URL module, no foreign CanDoItAll dependency/content, and Markdig only in the optional Markdown package.

Final manifest copied from `repo://output/package-validation/package-hashes-final.sha256`:

```text
912b5d73f951a35019377b68c9e6d1a35aa92d39dc7e8c99655bbde506063677 *CanDoItAll.FileTools.Abstractions.0.1.0.nupkg
3fe6fff1568c1763fdf43bb46e30d9048a3f36645e80c7878b9e9596d15e72ec *CanDoItAll.FileTools.Abstractions.0.1.0.snupkg
9e2791f2002b7d264f1a88b2f38deeddc26611f4e99e1fd438bbedbb86a12983 *CanDoItAll.FileTools.FileBrowser.Components.0.1.0.nupkg
ec5fa0a324b5e64be08631fc122f390d2b7c2c128810c381371b76031a750832 *CanDoItAll.FileTools.FileBrowser.Components.0.1.0.snupkg
d279669423330995b927d90169c71bed043bce26b8b7b306cafa5730e74dac7f *CanDoItAll.FileTools.FileBrowser.Core.0.1.0.nupkg
5ffb99bb9170c0bb28806f2a9eef45435a391e52eabcc2b19731a65f88d67b69 *CanDoItAll.FileTools.FileBrowser.Core.0.1.0.snupkg
7a49843c6e8c1da9309da65a9fd56f50b4723e2ec4d60911f33ce66b909f5575 *CanDoItAll.FileTools.FileInteraction.Components.0.1.0.nupkg
98e335d086707003bec2fd7e14a0b80223fcb7d6a7327c854cfca48738fff8b6 *CanDoItAll.FileTools.FileInteraction.Components.0.1.0.snupkg
b0d3dd8535eaaa1ec2b4e945140f088e2e47c0b3d9b60d3c8771dd5412dd6bb2 *CanDoItAll.FileTools.FileInteraction.Core.0.1.0.nupkg
d703032af3b44b0344e6aa07967331d52d1347649eef22f14e6c649eafc74209 *CanDoItAll.FileTools.FileInteraction.Core.0.1.0.snupkg
ea761e86dce7dda4e4c83a87267a8da6fc7117d41899f2fe50409d0e0104a19c *CanDoItAll.FileTools.FileInteraction.Markdown.0.1.0.nupkg
488c2852a6db636cdb2c4e03475a7f6ad0b3f037bf83fa214125d555cebc2f1a *CanDoItAll.FileTools.FileInteraction.Markdown.0.1.0.snupkg
28f17e9117a41ec41923dacf8b8121e6129a622daf3baee85e70d1ac223f3f20 *CanDoItAll.FileTools.Providers.FileSystem.0.1.0.nupkg
da9a583c556fab67c627d8cedf8a283d9520ae4a56d7db85af988e3f6eec2271 *CanDoItAll.FileTools.Providers.FileSystem.0.1.0.snupkg
```
