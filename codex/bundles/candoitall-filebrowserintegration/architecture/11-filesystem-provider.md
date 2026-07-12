# Filesystem Example Provider

## Security boundary

The example provider is root-confined by canonical/path comparison and must run under a restricted OS identity. Path checks are defense in depth, not an operating-system sandbox and not an authorization replacement.

## Link policy

- Root reparse point: reject configuration.
- Child reparse point: either expose as inert `Link` or exclude by option.
- Never call `ResolveLinkTarget`, navigate through, or read content through a reparse point.
- No “follow links” option is shipped because path-based checks cannot eliminate hostile TOCTOU ancestor replacement without handle-relative/platform-specific traversal.

## Disclosure policy

- Browser DisplayPath is root-relative (`.` for root).
- Descriptor metadata/description and public error technical detail never include the configured absolute root or raw BCL exception message.
- The original exception may be retained as InnerException for trusted host logging.
- Absolute-path projection is a future host decorator, not provider output.

## Freshness and content

- No listing/content cache exists in the adapter.
- Each operation resolves/refreshes current filesystem metadata; enumeration objects are not trusted as permanently current.
- Range reads use an async FileStream with sharing compatible with active agents plus a bounded wrapper that cannot read or seek beyond the selected range.
- Lease Length is returned range bytes, as documented in Abstractions.
- A new open after same-path replacement observes the replacement; an existing lease remains the opened handle’s view.

- One internal root-confined content reader backs both browser range reads and the provider's independent `IFileContentSource` implementation. A host may therefore open FileInteraction after browser-session disposal, but only after it has authorized the occurrence and minted a `FileReference` whose value is the canonical root-relative occurrence key.
- Mutable local reads report no `FileContentRevision`; this example provider cannot honestly provide optimistic concurrency across arbitrary external or agent writes. It intentionally supplies no save target. A production host that needs editing must add its own authorized persistence and revision adapter.

## Host effects

Regular files advertise `Open` only as host invocation eligibility. OpenUri/DownloadUri remain null, the provider does not implement action execution, and it never invokes shell/browser/download/copy behavior.
