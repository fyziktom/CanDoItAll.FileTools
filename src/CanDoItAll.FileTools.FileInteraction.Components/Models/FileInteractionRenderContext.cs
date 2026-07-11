using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Neutral content and event-up context passed to a registered renderer component.</summary>
public sealed class FileInteractionRenderContext
{
    public FileInteractionRenderContext(
        FileInteractionRequest request,
        FileInteractionMode mode,
        ReadOnlyMemory<byte> content,
        long editRevision,
        string? mediaType = null,
        string? text = null,
        string? encodingName = null,
        EventCallback<string> textChanged = default,
        EventCallback<FileInteractionContentChange> contentChanged = default,
        int maximumContentBytes = int.MaxValue)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        if (editRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(editRevision));
        }

        if (maximumContentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumContentBytes));
        }

        Mode = mode;
        Content = content;
        EditRevision = editRevision;
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        Text = text;
        EncodingName = string.IsNullOrWhiteSpace(encodingName) ? null : encodingName.Trim();
        TextChanged = textChanged;
        ContentChanged = contentChanged;
        MaximumContentBytes = maximumContentBytes;
    }

    public FileInteractionRequest Request { get; }

    public FileInteractionMode Mode { get; }

    public ReadOnlyMemory<byte> Content { get; }

    public long EditRevision { get; }

    public string? MediaType { get; }

    public string? Text { get; }

    public string? EncodingName { get; }

    public EventCallback<string> TextChanged { get; }

    /// <summary>
    /// Neutral edit event for binary and specialized editors. Text editors may use this event or
    /// the <see cref="TextChanged"/> convenience event.
    /// </summary>
    public EventCallback<FileInteractionContentChange> ContentChanged { get; }

    /// <summary>The host's bounded content limit for this interaction surface.</summary>
    public int MaximumContentBytes { get; }

    public bool IsEmpty => Content.IsEmpty;
}

/// <summary>Defensively copied content replacement raised by a registered editor renderer.</summary>
public sealed class FileInteractionContentChange
{
    public FileInteractionContentChange(
        ReadOnlyMemory<byte> content,
        string? mediaType = null,
        string? encodingName = null)
    {
        Content = content.ToArray();
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        EncodingName = string.IsNullOrWhiteSpace(encodingName) ? null : encodingName.Trim();
    }

    public ReadOnlyMemory<byte> Content { get; }

    public string? MediaType { get; }

    public string? EncodingName { get; }
}

/// <summary>Awaited request/response carrier used by the component's host-owned persistence event.</summary>
public sealed class FileInteractionSaveRequestedEventArgs
{
    public FileInteractionSaveRequestedEventArgs(FileSaveRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    public FileSaveRequest Request { get; }

    public FileContentRevision? PersistedRevision { get; private set; }

    public bool HasPersistedRevision { get; private set; }

    /// <summary>Optionally supplies the new optimistic-concurrency revision before the callback completes.</summary>
    public void SetPersistedRevision(FileContentRevision revision)
    {
        if (string.IsNullOrWhiteSpace(revision.Value))
        {
            throw new ArgumentException("A valid persisted revision is required.", nameof(revision));
        }

        PersistedRevision = revision;
        HasPersistedRevision = true;
    }
}
