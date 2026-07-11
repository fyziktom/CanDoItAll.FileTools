namespace CanDoItAll.FileTools.FileInteraction;

/// <summary>An opaque host-authorized reference to a file occurrence.</summary>
public readonly record struct FileReference
{
    public FileReference(string sourceId, string value, string? revision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        SourceId = sourceId.Trim();
        Value = value.Trim();
        Revision = string.IsNullOrWhiteSpace(revision) ? null : revision.Trim();
    }

    public string SourceId { get; }

    public string Value { get; }

    public string? Revision { get; }

    public override string ToString()
        => Revision is null ? $"{SourceId}:{Value}" : $"{SourceId}:{Value}@{Revision}";
}

/// <summary>An opaque persisted-content revision used for optimistic concurrency.</summary>
public readonly record struct FileContentRevision
{
    public FileContentRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

