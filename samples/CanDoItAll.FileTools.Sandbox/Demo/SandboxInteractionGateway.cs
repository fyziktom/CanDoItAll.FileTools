using System.Globalization;
using System.Text;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Sandbox.Demo;

public enum SandboxSaveInjection
{
    None,
    Failure,
    Conflict
}

public sealed record SandboxInteractionSample(
    string Id,
    string Label,
    string FileName,
    string Description,
    string Kind,
    bool IsEditable,
    bool IsAutomaticSave);

public sealed record SandboxInteractionSelection(
    string Id,
    string Label,
    string Description,
    FileInteractionRequest Request,
    IFileContentSource ContentSource,
    bool CanPersist,
    bool CanInjectSaveOutcome,
    bool IsLiveFileSystem = false);

/// <summary>
/// Demo host adapter for interaction reads and saves. It deliberately uses opaque references and
/// is not a FileTools storage implementation.
/// </summary>
public sealed class SandboxInteractionGateway : IFileContentSource
{
    public const int MaximumFileBytes = 512 * 1024;
    public const int MaximumPersistedBytes = 4 * 1024;
    public const string SourceId = "sandbox-memory";

    private static readonly IReadOnlyDictionary<(string Source, string Key), string> BrowserMappings =
        new Dictionary<(string Source, string Key), string>
        {
            [("project", "readme")] = "markdown",
            [("project", "roadmap")] = "mermaid",
            [("project", "solution")] = "text",
            [("project", "settings")] = "autosave",
            [("project", "artifact-report")] = "pdf",
            [("shared", "manual")] = "pdf",
            [("shared", "brand-cover")] = "image",
            [("shared", "template-data")] = "object"
        };

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries;
    private long revisionSequence = 100;

    public SandboxInteractionGateway()
    {
        entries = SeedEntries().ToDictionary(entry => entry.Sample.Id, StringComparer.Ordinal);
        Samples = Array.AsReadOnly(entries.Values
            .Select(entry => entry.Sample)
            .OrderBy(entry => entry.Id == "markdown" ? 0 : 1)
            .ThenBy(entry => entry.Label, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<SandboxInteractionSample> Samples { get; }

    public SandboxInteractionSelection CreateSelection(string sampleId, string? displayFileName = null)
    {
        Entry entry;
        lock (gate)
        {
            entry = GetEntry(sampleId);
            return CreateSelection(entry, displayFileName ?? entry.Sample.FileName);
        }
    }

    public bool TryCreateBrowserSelection(
        FileBrowserItem item,
        out SandboxInteractionSelection? selection)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsContainer
            && BrowserMappings.TryGetValue((item.Key.SourceId.Value, item.Key.Value), out string? sampleId))
        {
            selection = CreateSelection(sampleId, item.Name);
            return true;
        }

        selection = null;
        return false;
    }

    public void ArmSaveOutcome(FileReference file, SandboxSaveInjection injection)
    {
        if (injection == SandboxSaveInjection.None)
        {
            throw new ArgumentOutOfRangeException(nameof(injection));
        }

        lock (gate)
        {
            Entry entry = GetEntry(file);
            entry.NextSave = injection;
        }
    }

    public async ValueTask<FileContentRevision> SaveAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Entry entry;
        lock (gate)
        {
            entry = GetEntry(request.File);
            if (!entry.Sample.IsEditable)
            {
                throw new InvalidOperationException("This sandbox sample is read-only.");
            }
        }

        byte[] content = await ReadBoundedAsync(request.Content, cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(420), cancellationToken).ConfigureAwait(false);

        lock (gate)
        {
            entry = GetEntry(request.File);
            SandboxSaveInjection injection = entry.NextSave;
            entry.NextSave = SandboxSaveInjection.None;
            if (injection == SandboxSaveInjection.Failure)
            {
                throw new IOException("The sandbox host injected a one-shot persistence failure.");
            }

            if (injection == SandboxSaveInjection.Conflict)
            {
                FileContentRevision externalRevision = NextRevision();
                entry.Revision = externalRevision;
                throw new FileSaveConflictException(
                    request.File,
                    request.ExpectedRevision,
                    externalRevision,
                    "The sandbox host injected a one-shot external revision.");
            }

            if (request.ExpectedRevision is { } expected && expected != entry.Revision)
            {
                throw new FileSaveConflictException(
                    request.File,
                    expected,
                    entry.Revision);
            }

            entry.Content = content;
            entry.Revision = NextRevision();
            return entry.Revision;
        }
    }

    public ValueTask<FileContentLease> OpenReadAsync(
        FileContentReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Length > MaximumFileBytes + 1L)
        {
            throw new IOException("The sandbox host rejected an unbounded content request.");
        }

        lock (gate)
        {
            Entry entry = GetEntry(request.File);
            if (request.Offset > entry.Content.LongLength)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "The requested offset is beyond the sample content.");
            }

            int offset = checked((int)request.Offset);
            int available = entry.Content.Length - offset;
            int length = request.Length.HasValue
                ? checked((int)Math.Min(request.Length.Value, available))
                : available;
            var copy = entry.Content.AsSpan(offset, length).ToArray();
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(copy, writable: false),
                entry.MediaType,
                copy.LongLength,
                entry.Revision));
        }
    }

    private SandboxInteractionSelection CreateSelection(Entry entry, string fileName)
        => new(
            entry.Sample.Id,
            entry.Sample.Label,
            entry.Sample.Description,
            new FileInteractionRequest(
                entry.Reference,
                fileName,
                FileInteractionMode.View,
                entry.MediaType,
                entry.Content.LongLength,
                entry.Revision),
            this,
            entry.Sample.IsEditable,
            entry.Sample.IsEditable);

    private Entry GetEntry(string sampleId)
        => entries.TryGetValue(sampleId, out Entry? entry)
            ? entry
            : throw new KeyNotFoundException("The sandbox sample is not authorized.");

    private Entry GetEntry(FileReference file)
    {
        if (!string.Equals(file.SourceId, SourceId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The file reference is outside the sandbox-memory source.");
        }

        Entry? entry = entries.Values.FirstOrDefault(candidate => candidate.Reference == file);
        return entry ?? throw new KeyNotFoundException("The opaque file reference is not authorized.");
    }

    private FileContentRevision NextRevision()
    {
        revisionSequence++;
        return new FileContentRevision(
            $"sandbox-r{revisionSequence.ToString(CultureInfo.InvariantCulture)}");
    }

    private static async ValueTask<byte[]> ReadBoundedAsync(
        IFileSaveContent content,
        CancellationToken cancellationToken)
    {
        if (content.Length > MaximumPersistedBytes)
        {
            throw new IOException("The edited sample exceeds the sandbox persistence limit.");
        }

        await using Stream source = await content.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > MaximumPersistedBytes)
            {
                throw new IOException("The edited sample exceeds the sandbox persistence limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return destination.ToArray();
    }

    private static IEnumerable<Entry> SeedEntries()
    {
        yield return Text(
            "text",
            "Plain text",
            "release-notes.txt",
            "Manual persistence, bounded history, and host-owned retry behavior.",
            "Release gate\r\n- Validate browser integration\r\n- Capture responsive evidence\r\n");
        yield return Text(
            "markdown",
            "Markdown",
            "architecture.md",
            "Optional Markdown renderer with debounced split preview.",
            "# File interaction\n\nThe host supplies **content**, persistence, and policy.\n\n- bounded reads\n- explicit renderers\n- awaited saves\n",
            mediaType: "text/markdown");
        yield return Text(
            "mermaid",
            "Mermaid seam",
            "workflow.mmd",
            "Sandbox-local safe renderer seam without CanDoItAll.Components.",
            "flowchart LR\n    Browser --> Interaction\n    Interaction --> Host\n    Host --> Storage\n",
            mediaType: "text/x-mermaid");
        yield return Text(
            "autosave",
            "Automatic save",
            "scratch.auto",
            "TextUnitCount = 8 with an idle fallback, routed through the awaited host adapter.",
            "Type a short update; the host persists automatically after the configured threshold.",
            isAutomaticSave: true,
            mediaType: "text/x-sandbox-auto");
        yield return Text(
            "bounded",
            "Bounded rejection",
            "oversized-edit.txt",
            "A readable 6 KiB sample whose edits exceed the host's explicit 4 KiB persistence limit.",
            new string('x', 6 * 1024));
        yield return new Entry(
            new SandboxInteractionSample(
                "binary", "Binary / hex", "packet.bin",
                "Neutral ContentChanged editing, history, persistence, and an explicit over-limit rejection probe.",
                "binary", true, false),
            Reference("binary"),
            [0, 1, 2, 16, 32, 64, 127, 128, 254, 255],
            "application/octet-stream",
            new FileContentRevision("sandbox-binary-r1"));
        yield return new Entry(
            new SandboxInteractionSample(
                "image", "Image", "status.png",
                "Built-in bounded image viewer backed by a local object URL.",
                "viewer", false, false),
            Reference("image"),
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="),
            "image/png",
            new FileContentRevision("sandbox-r5"));
        yield return new Entry(
            new SandboxInteractionSample(
                "pdf", "PDF", "brief.pdf",
                "Built-in browser PDF surface using only bounded in-memory content.",
                "viewer", false, false),
            Reference("pdf"),
            CreateMinimalPdf(),
            "application/pdf",
            new FileContentRevision("sandbox-r6"));
        yield return new Entry(
            new SandboxInteractionSample(
                "object", "Unsupported object", "archive.zip",
                "Metadata-only inert fallback; the payload is never embedded or executed.",
                "fallback", false, false),
            Reference("object"),
            [80, 75, 3, 4],
            "application/zip",
            new FileContentRevision("sandbox-r7"));
    }

    private static Entry Text(
        string id,
        string label,
        string fileName,
        string description,
        string content,
        bool isAutomaticSave = false,
        string mediaType = "text/plain")
        => new(
            new SandboxInteractionSample(
                id, label, fileName, description,
                isAutomaticSave ? "autosave" : "editable",
                true,
                isAutomaticSave),
            Reference(id),
            Encoding.UTF8.GetBytes(content),
            mediaType,
            new FileContentRevision($"sandbox-{id}-r1"));

    private static FileReference Reference(string id)
        => new(SourceId, $"opaque-{id}-7f4c2a");

    private static byte[] CreateMinimalPdf()
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 320 180] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            "<< /Length 47 >>\nstream\nBT /F1 18 Tf 44 92 Td (FileTools sandbox) Tj ET\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        int xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            builder.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        builder.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n")
            .Append(xref.ToString(CultureInfo.InvariantCulture))
            .Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private sealed class Entry(
        SandboxInteractionSample sample,
        FileReference reference,
        byte[] content,
        string mediaType,
        FileContentRevision revision)
    {
        public SandboxInteractionSample Sample { get; } = sample;

        public FileReference Reference { get; } = reference;

        public byte[] Content { get; set; } = content;

        public string MediaType { get; } = mediaType;

        public FileContentRevision Revision { get; set; } = revision;

        public SandboxSaveInjection NextSave { get; set; }
    }
}
