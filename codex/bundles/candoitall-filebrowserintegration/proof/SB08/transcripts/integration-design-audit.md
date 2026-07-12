# SB08 Future CanDoItAll Integration Design Audit — SB08-INV-02

- Run label: final source-anchored design audit, 2026-07-11.
- Working directory context only: `bundle://architecture/07-candoitall-integration.md` and `bundle://architecture/08-cache-and-invalidation.md`.
- Command: `audit N004-N009 and R024-R028 against exact source anchors, owners, dependency direction, authorization, cache/revision policy, and CI1-CI13 proof gates`.
ExitCode: 0

Coverage proven in the design, not production:

- Projects: shared filtered card/files source projection; files tab; project-card dialog; deterministic ordered project/subproject/source/revision fingerprint.
- Workbench: project-structure floating browser, include-subprojects scope, folder-node browser action, semantic root resolver, principal authorization, and no reuse of local opener/path existence as authority.
- Processes: process-owned run root policy and separate always-current uncached run-artifact browser.
- Resources: project/external/IPFS source catalog and reauthorized “make resource” flow; current absence of `resource.storage-object` or IPFS connector is an explicit CI11 prerequisite, not hidden.
- remote FTP: provider/native-storage sidecar and module adapter plan without adding FTP to FileTools base packages.
- storage/cache: Infrastructure-native browse sidecars stay FileTools-free; outer adapters map to FileTools; typed backward-compatible settings live in `StorageCatalogRecord.ConfigJson`; Disabled/Memory/Hybrid profiles are optional.
- security: principal-aware re-resolution and bounded opaque server handles precede browse/content/save; unsigned encoded tokens are never authority.
- revision: in-memory aggregate catalog revision ships first; project revision spans filesystem/IPFS/subprojects; durable/shared revision is mandatory before any distributed cache secondary.
- cache keys include runtime/database, driver/binding, semantic scope, query, source revision, and authorization scope, or cached raw data is reauthorized; agent-working filesystem/process paths remain uncached.

The future plan is ordered CI1-CI13 and requires fresh source/CodeAnalytics re-entry because CanDoItAll is under refactoring. No production main implementation is claimed.
