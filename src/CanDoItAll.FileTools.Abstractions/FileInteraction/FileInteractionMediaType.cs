namespace CanDoItAll.FileTools.FileInteraction;

internal static class FileInteractionMediaType
{
    public static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : NormalizeRequired(value);

    public static string NormalizeRequired(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parameterSeparator = value.IndexOf(';');
        var mediaType = (parameterSeparator < 0 ? value : value[..parameterSeparator])
            .Trim()
            .ToLowerInvariant();
        if (mediaType.Length == 0)
        {
            throw new ArgumentException("A media type is required before its parameters.", nameof(value));
        }

        return mediaType;
    }
}
