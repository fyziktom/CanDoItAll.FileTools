using System.Collections.ObjectModel;

namespace CanDoItAll.FileTools.FileInteraction;

public enum FileInteractionMode
{
    View,
    Edit,
    Diff
}

[Flags]
public enum FileInteractionCapabilities
{
    None = 0,
    View = 1 << 0,
    Edit = 1 << 1,
    Preview = 1 << 2,
    Save = 1 << 3,
    Undo = 1 << 4,
    Redo = 1 << 5,
    Diff = 1 << 6
}

public enum FilePreviewPlacement
{
    Beside,
    Below
}

/// <summary>Validated preview defaults owned by a file interaction profile.</summary>
public sealed record FilePreviewOptions
{
    public FilePreviewOptions(
        bool enabled = false,
        TimeSpan? debounce = null,
        bool splitByDefault = false,
        FilePreviewPlacement placement = FilePreviewPlacement.Beside)
    {
        var effectiveDebounce = debounce ?? TimeSpan.FromMilliseconds(300);
        if (effectiveDebounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }

        if (!enabled && splitByDefault)
        {
            throw new ArgumentException("Split preview cannot be the default when preview is disabled.", nameof(splitByDefault));
        }

        Enabled = enabled;
        Debounce = effectiveDebounce;
        SplitByDefault = splitByDefault;
        Placement = placement;
    }

    public bool Enabled { get; }

    public TimeSpan Debounce { get; }

    public bool SplitByDefault { get; }

    public FilePreviewPlacement Placement { get; }

    public static FilePreviewOptions Disabled { get; } = new();
}

/// <summary>Describes type matching and behavior defaults without naming a UI component.</summary>
public sealed record FileInteractionProfileDescriptor
{
    public FileInteractionProfileDescriptor(
        string id,
        FileInteractionCapabilities capabilities,
        IEnumerable<string>? extensions = null,
        IEnumerable<string>? mediaTypes = null,
        int priority = 0,
        FileAutoSaveOptions? autoSave = null,
        FilePreviewOptions? preview = null,
        FileHistoryOptions? history = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (capabilities == FileInteractionCapabilities.None)
        {
            throw new ArgumentOutOfRangeException(nameof(capabilities));
        }

        Id = id.Trim();
        Capabilities = capabilities;
        Extensions = NormalizeExtensions(extensions);
        MediaTypes = NormalizeMediaTypes(mediaTypes);
        if (Extensions.Count == 0 && MediaTypes.Count == 0)
        {
            throw new ArgumentException("At least one extension or media type is required.");
        }

        Priority = priority;
        AutoSave = autoSave ?? FileAutoSaveOptions.Disabled;
        Preview = preview ?? FilePreviewOptions.Disabled;
        History = history ?? FileHistoryOptions.Disabled;

        if (Capabilities.HasFlag(FileInteractionCapabilities.Edit)
            && !Capabilities.HasFlag(FileInteractionCapabilities.View))
        {
            throw new ArgumentException("Editable profiles must also support viewing.", nameof(capabilities));
        }

        if (AutoSave.Enabled && !Capabilities.HasFlag(FileInteractionCapabilities.Save))
        {
            throw new ArgumentException("Automatic save requires the Save capability.", nameof(autoSave));
        }

        if (Preview.Enabled && !Capabilities.HasFlag(FileInteractionCapabilities.Preview))
        {
            throw new ArgumentException("Preview options require the Preview capability.", nameof(preview));
        }
    }

    public string Id { get; }

    public FileInteractionCapabilities Capabilities { get; }

    public IReadOnlyList<string> Extensions { get; }

    public IReadOnlyList<string> MediaTypes { get; }

    public int Priority { get; }

    public FileAutoSaveOptions AutoSave { get; }

    public FilePreviewOptions Preview { get; }

    public FileHistoryOptions History { get; }

    public bool Supports(FileInteractionMode mode) => mode switch
    {
        FileInteractionMode.View => Capabilities.HasFlag(FileInteractionCapabilities.View),
        FileInteractionMode.Edit => Capabilities.HasFlag(FileInteractionCapabilities.Edit),
        FileInteractionMode.Diff => Capabilities.HasFlag(FileInteractionCapabilities.Diff),
        _ => false
    };

    private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string>? extensions)
    {
        var values = (extensions ?? [])
            .Select(value => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Extensions cannot contain blank values.", nameof(extensions))
                : value.Trim().ToLowerInvariant())
            .Select(value => value == "*" || value.StartsWith('.') ? value : $".{value}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(values);
    }

    private static IReadOnlyList<string> NormalizeMediaTypes(IEnumerable<string>? mediaTypes)
    {
        var values = (mediaTypes ?? [])
            .Select(value => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Media types cannot contain blank values.", nameof(mediaTypes))
                : FileInteractionMediaType.NormalizeRequired(value))
            .Select(ValidateMediaType)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(values);
    }

    private static string ValidateMediaType(string value)
    {
        var separator = value.IndexOf('/');
        if (separator <= 0
            || separator == value.Length - 1
            || value.Contains(' ')
            || (value.Contains('*') && !value.EndsWith("/*", StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Invalid media type pattern '{value}'.");
        }

        return value;
    }
}

/// <summary>Input used by core resolution and UI shells.</summary>
public sealed record FileInteractionRequest
{
    public FileInteractionRequest(
        FileReference file,
        string fileName,
        FileInteractionMode mode = FileInteractionMode.View,
        string? mediaType = null,
        long? size = null,
        FileContentRevision? contentRevision = null)
    {
        if (string.IsNullOrWhiteSpace(file.SourceId) || string.IsNullOrWhiteSpace(file.Value))
        {
            throw new ArgumentException("A valid file reference is required.", nameof(file));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        File = file;
        FileName = fileName.Trim();
        Mode = mode;
        MediaType = FileInteractionMediaType.NormalizeOptional(mediaType);
        Size = size;
        ContentRevision = contentRevision;
    }

    public FileReference File { get; }

    public string FileName { get; }

    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    public FileInteractionMode Mode { get; }

    public string? MediaType { get; }

    public long? Size { get; }

    public FileContentRevision? ContentRevision { get; }
}
