namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Describes the provider-observed version of a source or container.</summary>
public readonly record struct FileBrowserVersionStamp
{
    public FileBrowserVersionStamp(string value, bool isImmutable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
        IsImmutable = isImmutable;
    }

    public string Value { get; }

    public bool IsImmutable { get; }

    public override string ToString() => Value;
}

/// <summary>
/// Optionally exposes a provider's current version without prescribing how a host caches it.
/// </summary>
public interface IFileBrowserVersionProvider
{
    ValueTask<FileBrowserVersionStamp?> GetVersionAsync(
        FileBrowserItemKey? containerKey = null,
        CancellationToken cancellationToken = default);
}

