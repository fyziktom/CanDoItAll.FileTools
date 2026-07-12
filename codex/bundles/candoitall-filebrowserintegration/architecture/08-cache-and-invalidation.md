# Cache and Invalidation Architecture

## Two distinct layers

1. FileTools browser-session retention: optional bounded navigation state, process-local to one browser session, with no Microsoft cache dependency.
2. CanDoItAll listing cache: an optional host-side decorator for expensive listings, implemented with .NET `HybridCache` only after native browsing, authorization, and no-cache freshness paths are proven.

The driver/binding policy is authoritative. FileBrowser session retention cannot turn a Disabled host listing policy back into caching.

## Delivery posture: memory now, durable later

Official .NET guidance makes `HybridCache` suitable for the host decorator: it uses memory as primary storage, provides stampede protection, and can later use an `IDistributedCache` secondary. The first CanDoItAll delivery registers memory primary only.

Initial delivery:

- `HybridCache` has no distributed secondary.
- `IFileCatalogRevisionService` and its change sink are singleton, process-local, in-memory services.
- A process restart discards both cached listings and revisions; no project entity timestamp or new persistence table is required.
- The default for every missing/legacy provider cache setting is Disabled.

Future distributed delivery:

- add a durable/shared revision store or reliable cross-node revision event/backplane first;
- include its monotonic version in cache keys;
- prove multi-node invalidation, authorization revision, and database-profile switching;
- only then enable Redis/SQL/Postgres or another `IDistributedCache` secondary.

Do not enable a distributed secondary while revisions remain process-local: another node could select an obsolete distributed entry even though its local revision changed.

## Typed provider/binding settings

Persist a typed `StorageBrowseCacheSettings` inside the provider configuration serialized in `StorageCatalogRecord.ConfigJson`. Do not hide operational cache policy in `MetadataJson`, and do not add a cache-settings table in the first delivery.

Required fields/invariants:

```text
Enabled
Mode = Disabled | Memory | Hybrid
TimeToLive
MaximumTimeToLive
MaximumPageSize
MaximumCachedItems
AllowForceRefresh
ImmutableVersionPolicy
```

- `Enabled == false` normalizes to `Mode == Disabled`.
- `Mode == Memory` uses the HybridCache API with memory primary and no distributed secondary.
- `Mode == Hybrid` requires a deliberately configured distributed secondary plus the durable/shared-revision gate. Until then it fails closed or is rejected by settings validation; it is never silently treated as distributed-ready.
- TTL must be positive and cannot exceed `MaximumTimeToLive`.
- page/item bounds are mandatory; an unbounded provider response is never placed in cache.
- force refresh is an authorized capability, not a query-string bypass available to every caller.
- immutable caching is allowed only when the driver proves that the addressed version is immutable (for example an IPFS CID/DAG version). A mutable MFS path, user label, or ordinary filesystem path cannot opt itself into immutable behavior.
- absent settings deserialize to a valid Disabled object for backward compatibility.

`StorageCatalogRecord.UpdatedAtUtc` is unsuitable as file freshness: catalog/bootstrap reads can refresh it and it represents configuration activity. `Project.UpdatedAtUtc` likewise represents project entity changes, not a mixed file set.

## Policy matrix

| Source | Driver-level cache | Aggregate cache | Session retention | Default |
| --- | --- | --- | --- | --- |
| Managed process-run folder | Disabled | Disabled | Disabled | Re-enumerate on open and explicit refresh |
| Agent working/output/product OS folder | Disabled | Disabled | Disabled | Always current; semantic root authorization required |
| Ordinary attached filesystem folder | Disabled | Optional only through a semantic project/resource aggregate | Disabled | No raw OS-folder listing cache |
| IPFS immutable CID/DAG | Long-lived by proven immutable version | Optional | Bounded | Key includes CID/version; never reuse for MFS |
| IPFS mutable MFS path | Conservative/Disabled | Optional bounded TTL | Disabled/Bounded | Treat as mutable and allow authorized refresh |
| Project composite | Source-specific | Enabled only when configured | Bounded/Disabled by UI | Key includes catalog revision, source-set fingerprint, include-subprojects |
| FTP | Configurable conservative TTL | Optional | Disabled/Bounded | Force refresh and hard maximum TTL |
| Resources composite | Source-specific | Optional | Bounded | Revision includes attachment/source-set/authorization changes |

## Cacheable value boundary

Cache only bounded listing/stat values that are safe at the chosen authorization boundary. Never cache:

- open streams/content leases;
- credentials or provider secrets;
- signed download URLs;
- opaque server handles;
- decoded unsigned storage reference tokens;
- user-specific action grants under a shared key;
- unfiltered cross-principal data that will be returned without a second authorization pass.

Choose one of two explicit models per cache decorator:

1. Authorization-scoped cache: listing output is already filtered, so the key includes a stable authorization-scope fingerprint derived from tenant/principal/grants/authorization revision.
2. Raw provider cache: only safe preauthorization provider facts are cached; every hit is filtered and reauthorized before mapping to a FileTools page or issuing handles.

Do not mix these models in one entry.

## Key design

Never use arbitrary user paths or secrets as raw keys. Canonical bounded data is serialized and hashed:

```text
runtime/database snapshot fingerprint or generation from IDatabaseRuntimeState.GetSnapshot()
source/storage binding stable id
semantic scope id
include-descendants/include-subprojects
deterministic source-set fingerprint
file-catalog revision or proven immutable version
authorization-scope fingerprint (for authorization-scoped entries)
normalized query/filter/sort/metadata/page fingerprint
cache schema version
```

Database/profile switches produce a new namespace and evict current-process entries where possible. A profile name alone is insufficient; use the runtime-state snapshot/generation so switching away and back cannot accidentally select data from the wrong runtime generation.

## In-memory file-catalog revision

The first delivery uses an in-memory record/service shape, not a persisted Project property:

```text
SemanticScopeId
MonotonicRevision
FilesChangedAtUtc
Reason
SourceRevisionSummary
```

Revision producers call a single change sink after the underlying mutation succeeds:

- awaited FileInteraction save acknowledgement;
- storage placement tagged to a ProjectId;
- project-structure file/folder/storage-reference mutation;
- subproject relationship or selected source-set change;
- resource attachment/configuration/promotion change;
- observed agent workspace receipt;
- filesystem watcher/coarse scan where deliberately enabled;
- authorized manual refresh.

Consumers read the current revision when building aggregate keys. A successful bump chooses a new versioned key; best-effort tag/key removal reduces memory but is not the correctness mechanism. Direct OS/FTP writes can bypass events, so maximum TTL and explicit refresh remain mandatory for any mutable cached aggregate.

## Opaque handles and cache interaction

The server handle registry is separate from the listing cache. A cache hit returns descriptive entry data only. Before an activation/download/edit/save response, the host:

1. re-resolves the semantic scope and current entry;
2. repeats principal-aware authorization;
3. issues or resolves an opaque random handle bound to principal/tenant, source/item, operations, revision, and expiry;
4. rejects a stale, expired, cross-principal, profile-mismatched, or authorization-revision-mismatched handle.

`StorageJson.EncodeReferenceToken` is unsigned base64url JSON and can never replace this registry.

## Disabled and force-refresh semantics

- Disabled is a real pass-through decorator: no cache lookup, value factory coalescing, store, or session retention.
- Authorized force refresh bypasses the entry, reads the provider, bumps semantic revision when the observed source set changed (or records an explicit refresh generation), and evicts applicable local tags/keys.
- A force-refresh flag never bypasses authorization and never permits a larger page than configured bounds.
- Cancellation/failure does not publish a partial listing or advance revision.

## Memory and multi-node bounds

.NET memory cache is not automatically size-bounded. Prefer bounded pages and validated HybridCache payload/expiration options. If a dedicated `MemoryCache` fallback is introduced, give it a private singleton and a consistent size unit; do not size-limit the shared DI cache when unrelated entries do not set sizes.

With a future distributed secondary, local primary entries on other nodes are not automatically invalidated by a local tag removal. Durable versioned keys plus shared revision are the correctness mechanism; event distribution/backplane is an eviction accelerator.

## Required proof for future implementation

- legacy/missing `ConfigJson` settings deserialize to Disabled;
- invalid Enabled/Mode, TTL, maximum TTL, page/item, force-refresh, and immutable combinations fail or normalize deterministically;
- Disabled mode observes provider mutation on the next request and does not coalesce through a cache;
- two principals/scopes cannot receive each other's filtered entries or handles;
- raw shared-cache mode reauthorizes every hit;
- database runtime snapshot changes namespace entries;
- successful save/placement/source-set/resource change bumps the in-memory revision after, not before, persistence;
- failed/cancelled mutations do not bump revision;
- restart semantics are honest for memory-only delivery;
- distributed cache cannot be enabled until durable/shared revision proof passes;
- mutable MFS/filesystem/FTP data never receives immutable policy by assertion alone.
