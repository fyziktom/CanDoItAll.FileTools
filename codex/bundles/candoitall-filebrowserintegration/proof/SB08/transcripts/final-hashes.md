# SB08 Final Bundle and Product Integrity Set — SB08-INV-01 SB08-INV-02 SB08-INV-03

- Run label: read-only final integrity capture after implementation, transfer, visual, report, and red-team freeze, 2026-07-11.
- Working directory context only: bundle and FileTools repository.
- Command: `Get-FileHash selected final bundle and FileTools product/package-manifest files -Algorithm SHA256`.
ExitCode: 0

## FileTools anchors

```text
3efbe6fefd1e72ca4f0c638b6bee0d831c36cd9cb58a4568c0972e3a4d37118e  repo://README.md
04371b4567e0bb34848fb0d4a401688217aee4f9183615203337830ac543d684  repo://CanDoItAll.FileTools.slnx
771865602fe6d3f95fd23b541ec2ac8b3684a2fe3c1dc4fea272e61997a0d5d9  repo://Directory.Build.props
7265ce12a1285e3350650d83216a69130a79243e9c7b04f80fdc8806d5b4c20e  repo://Directory.Packages.props
3ddfe8ebafda49765f2306d0ed7e72458cf7c135711dd3ac236eb9c8d4009bb6  repo://output/package-validation/package-hashes-final.sha256
```

The 14 individual package hashes are in `bundle://proof/SB08/transcripts/package-validation.md`; interaction source/test/visual hashes are in `bundle://proof/SB07/transcripts/final-hashes.md`; Components changed-file hashes are in `bundle://proof/SB08/transcripts/components-cleanup.md`.

## Final bundle anchors

```text
db4ebce1669adfe54a1639d0722ecc243f38650bf08ea810406d91991741333a  bundle://README.md
57a0a8bc81958a83d112e47b9bacdc9c5a685dfe52e728d15baa59a3ee4a36f5  bundle://requirements/01-normalized-requirements.md
ac7360afa658c8d16ceb1e96d4e41a03a6421b66a56541c4d5e76fa68ae8e010  bundle://architecture/07-candoitall-integration.md
b311af0285b1e2aad167257c2c099f1d1eeccb8559282fed8d51fdc78a5d55e7  bundle://architecture/08-cache-and-invalidation.md
704956f18ace836e06ba958ec9e1ec23614ab56977e7089c2524db06665d1032  bundle://plan/01-phase-plan.md
b926da8b6d8beafd6ac1f31b8994fb76af4f3fece48e14c891ebb57164c7e4ab  bundle://plan/architecture-checkpoints.md
2fe6c15b4a17cb4b2aa8d315f5d4cf036842729d352c8c301fc5f84ba7183581  bundle://traceability/01-requirement-traceability.md
09ce7f91eb4c4685e99d50b011bf623291754ed4d8d5d30f436e8ebe926a0933  bundle://traceability/02-input-coverage.md
fbf0abbcb829517ca7d76af483faf4e9f7e26469cf550825d2e41bb69b49d7b2  bundle://reviews/00-bundle-self-review.md
d35ce566f14fa46c5e942a0ddb7a6a217dafb36c59950213c4b9e6fb27d04d4c  bundle://reviews/01-execution-report.md
1bd028958ad88190d34e9616cd71d851e44022de3063b37978544cda7d5d3352  bundle://reviews/csharp-architecture-gate.md
98bca1b88c2d75f02578b7f631280e4d4460944a8307187073b6700f5f347c00  bundle://subbundles/07-basic-viewers-editing-workflow-and-history/README.md
f1bc494ecad5e95fa930510008a50948b4241f2e66da8591b9fe6b9da5500ec1  bundle://subbundles/08-validation-packaging-and-candoitall-integration-design/README.md
dab36635ebc9c6e474cadd16f658d3238a4582f50f38963ef694c9eb9d2cca87  bundle://proof/SB07/manifest.md
017e3820d3073b1197e909643da50aaca2d43ff826bcd0b584580a4e1f3fa2b3  bundle://proof/SB07/semantic-invariants.md
e4da5ac7e77758f4c6e9df7d206895526786e18c5609637f9ee030969cfc8b4f  bundle://proof/SB07/transcripts/browser-validation.md
f7981ed3a091ba545d7720e561029a5e1073414b9af8652675466cf0f8c0c2ae  bundle://proof/SB07/transcripts/visual-review.md
6c141faa5a9362c56f6227f5c74816554d4cfd9ea186bc7e9dab24425afcf689  bundle://proof/SB07/transcripts/final-hashes.md
17ad747b098ad3811fc18fe08e415f176a9ad9f30964c6a7ef56485cfba1494a  bundle://proof/SB08/semantic-invariants.md
405db499492fe58658d2ab135d1c2c98626dc86bd4f2fd7830dbdddc7c5b35b2  bundle://proof/SB08/transcripts/filetools-validation.md
ffa710f89a13389006b2bce8a1b4b6a93c48739df2c1f7d0d4dab0ff63634ff8  bundle://proof/SB08/transcripts/package-validation.md
b264f007deb737bdd8a65e365a6407d2208113e68f51b489b5915cca9fb4472f  bundle://proof/SB08/transcripts/codeanalytics.md
01e5f412dfb3e5c1a533ecde618b2a27581f8377561a89a145b56c9070313116  bundle://proof/SB08/transcripts/components-cleanup.md
8fd2bda05b4bb82c0be1ba3fe2a66ca9d685dd6340059e98960d9bc3b8436f85  bundle://proof/SB08/transcripts/main-readonly.md
798cd59e97194074d90f49fbd5f678af2e85b3851ab0e6b807dc3db73a3e1d3a  bundle://proof/SB08/transcripts/integration-design-audit.md
eb4147e0182c25bc498e2793f2bebd013ec453302fc1365c1b4b9ed682090821  bundle://proof/SB08/transcripts/browser-regression.md
095b54ba504a9b10efd69fbc9510f56b72f2ab008cc86c8b00f7bb38837b2f4c  bundle://proof/SB08/transcripts/red-team.md
```

The final SB08 manifest and validator transcript are excluded because they cite this integrity record and validator reruns. This file excludes its own hash.
