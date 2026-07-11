namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Identifies one configured provider instance.</summary>
public readonly record struct FileBrowserSourceId
{
    /// <summary>Creates a provider source identifier.</summary>
    public FileBrowserSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>Gets the stable identifier value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>
/// Identifies one occurrence in a browser hierarchy. The opaque value must describe the
/// occurrence, not only its content hash, because the same content can appear at several paths.
/// </summary>
public readonly record struct FileBrowserItemKey
{
    /// <summary>Creates an occurrence key.</summary>
    public FileBrowserItemKey(FileBrowserSourceId sourceId, string value, string? revision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (string.IsNullOrWhiteSpace(sourceId.Value))
        {
            throw new ArgumentException("A source identifier is required.", nameof(sourceId));
        }

        SourceId = sourceId;
        Value = value.Trim();
        Revision = string.IsNullOrWhiteSpace(revision) ? null : revision.Trim();
    }

    /// <summary>Gets the owning provider source.</summary>
    public FileBrowserSourceId SourceId { get; }

    /// <summary>Gets the provider-owned opaque occurrence value.</summary>
    public string Value { get; }

    /// <summary>Gets an optional immutable source revision, such as a resolved IPNS CID.</summary>
    public string? Revision { get; }

    /// <inheritdoc />
    public override string ToString()
        => Revision is null
            ? $"{SourceId}:{Value}"
            : $"{SourceId}:{Value}@{Revision}";
}

/// <summary>Identifies content independently from its occurrence in a hierarchy.</summary>
public sealed record FileBrowserContentIdentity
{
    /// <summary>Creates a content identity.</summary>
    public FileBrowserContentIdentity(string scheme, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Scheme = scheme.Trim().ToLowerInvariant();
        Value = value.Trim();
    }

    /// <summary>Gets the identity scheme, for example <c>cid</c> or <c>sha256</c>.</summary>
    public string Scheme { get; }

    /// <summary>Gets the provider-independent identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Scheme}:{Value}";
}
