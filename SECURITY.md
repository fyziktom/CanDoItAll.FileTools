# Security Policy

## Supported Versions

The latest published version of each package and the current `main` branch receive
security fixes. Older package versions are not guaranteed to receive backports.

## Reporting A Vulnerability

Use [GitHub private vulnerability reporting](https://github.com/fyziktom/CanDoItAll.FileTools/security/advisories/new).
Do not publish exploit details, credentials, private data, or sensitive proof in a public
issue.

Include the affected package or commit, reproduction steps, expected impact, and any safe
mitigation already tested. If private reporting is unavailable, contact the repository
owner privately before sharing technical details.

## Scope

Reports may cover FileBrowser and FileInteraction contracts, components and renderers,
the filesystem provider, desktop launching, package supply-chain metadata, or a way the
library violates a documented host security boundary.

Host authorization policy, host persistence, host-provided storage drivers, browser
behavior, and application deployment are outside this repository's direct control unless
the report demonstrates that FileTools bypasses or misrepresents the documented boundary.
