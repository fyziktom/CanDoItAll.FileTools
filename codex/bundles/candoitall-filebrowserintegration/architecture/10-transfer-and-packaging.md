# Transfer and Packaging Plan

## Transfer sequence

1. Capture Components FileBrowser source/test/docs/script manifest and baseline tests.
2. Scaffold FileTools solution/package policies.
3. Move contracts before implementations and rename namespaces/package IDs.
4. Preserve characterization tests near new owners.
5. Refactor behavior gaps and add new tests.
6. Prove standalone restore/build/test/sandbox/browser/package.
7. Compare source responsibility manifest.
8. Remove FileBrowser ownership and release references from Components.
9. Rebuild/test Components to prove remaining libraries are intact.

## Components files to update/remove

- FileBrowser production projects under `src/`.
- three FileBrowser test projects under `tests/`.
- FileBrowser sandbox under `samples/`.
- `docs/file-browser/**` and `scripts/validate-file-browser-packages.ps1` after their useful content is transferred.
- FileBrowser entries in `CanDoItAll.Components.slnx`, `.github/workflows/ci.yml`, `README.md`, `docs/open-source-release-checklist.md`, and `scripts/pack-release.ps1`.

Keep Components.BaseLib/Common/Mermaid, generic tooltip lifecycle fixes/tests, unrelated samples/assets, user settings, and generated artifacts.

## Package names

- `CanDoItAll.FileTools.Abstractions`
- `CanDoItAll.FileTools.FileBrowser.Core`
- `CanDoItAll.FileTools.FileBrowser.Components`
- `CanDoItAll.FileTools.Providers.FileSystem`
- `CanDoItAll.FileTools.FileInteraction.Core`
- `CanDoItAll.FileTools.FileInteraction.Components`
- `CanDoItAll.FileTools.FileInteraction.Markdown`

## Static asset migration

Update every `_content/CanDoItAll.Components.FileBrowser.BaseLib/...` path to the new package ID. Prefer CSS isolation over the former global `wwwroot/css/file-browser.css`. Collocated modules use new package paths and are imported dynamically.

## Versioning

This is a new repository at `0.1.0` prerelease/stabilization level. Record breaking namespace/package migration in README. Do not publish or push in this run unless separately authorized.

## Rollback

Until Components removal is committed separately, its branch is the source rollback. Do not remove source before all FileTools gates pass. If the post-removal Components build fails, restore only the transfer commit/files with a targeted patch; never reset the unrelated worktree.

