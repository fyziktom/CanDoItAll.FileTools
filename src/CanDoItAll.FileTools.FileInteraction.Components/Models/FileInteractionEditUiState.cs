namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Owns recoverable edit-validation presentation independently of persistence state.</summary>
internal sealed class FileInteractionEditUiState
{
    public Exception? Error { get; private set; }

    public string? Message => Error switch
    {
        FileInteractionContentTooLargeException exception =>
            $"The edit exceeds the configured {exception.MaximumBytes:N0}-byte interaction limit. The previous content is unchanged.",
        null => null,
        _ => "The editor could not apply this content change. The previous content is unchanged."
    };

    public void SetError(Exception error)
        => Error = error ?? throw new ArgumentNullException(nameof(error));

    public void Reset() => Error = null;
}
