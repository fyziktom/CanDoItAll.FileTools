namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Immutable, value-equal cursor history retained with an accumulated container.</summary>
internal sealed class FileBrowserContinuationHistory : IEquatable<FileBrowserContinuationHistory>
{
    private readonly string[] tokens;

    private FileBrowserContinuationHistory(IEnumerable<string> tokens)
    {
        this.tokens = tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static FileBrowserContinuationHistory Empty { get; } = new([]);

    public IReadOnlyList<string> Tokens => tokens;

    public static FileBrowserContinuationHistory Create(IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new FileBrowserContinuationHistory(tokens);
    }

    public bool Equals(FileBrowserContinuationHistory? other)
        => other is not null && tokens.SequenceEqual(other.tokens, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as FileBrowserContinuationHistory);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (string token in tokens)
        {
            hash.Add(token, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
