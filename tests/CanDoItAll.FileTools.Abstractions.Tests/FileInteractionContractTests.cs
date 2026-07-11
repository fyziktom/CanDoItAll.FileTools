namespace CanDoItAll.FileTools.Abstractions.Tests;

public sealed class FileInteractionContractTests
{
    [Fact]
    public void FileReference_RequiresOpaqueSourceAndValue_AndNormalizesRevision()
    {
        var reference = new FileReference(" project ", " docs/readme ", " r2 ");

        Assert.Equal("project", reference.SourceId);
        Assert.Equal("docs/readme", reference.Value);
        Assert.Equal("r2", reference.Revision);
        Assert.Equal("project:docs/readme@r2", reference.ToString());
        Assert.Throws<ArgumentException>(() => new FileReference(" ", "file"));
        Assert.Throws<ArgumentException>(() => new FileContentRevision(" "));
    }

    [Fact]
    public void Request_NormalizesTypeAndExposesExtension()
    {
        var request = new FileInteractionRequest(
            File(),
            " README.MD ",
            FileInteractionMode.Edit,
            " TEXT/MARKDOWN; Charset=UTF-8 ",
            size: 42,
            contentRevision: new FileContentRevision("r1"));

        Assert.Equal("README.MD", request.FileName);
        Assert.Equal(".md", request.Extension);
        Assert.Equal("text/markdown", request.MediaType);
        Assert.Equal(FileInteractionMode.Edit, request.Mode);
    }

    [Fact]
    public void Profile_NormalizesPatterns_AndReportsModes()
    {
        var profile = new FileInteractionProfileDescriptor(
            " markdown ",
            FileInteractionCapabilities.View
                | FileInteractionCapabilities.Edit
                | FileInteractionCapabilities.Save
                | FileInteractionCapabilities.Preview,
            extensions: ["MD", ".markdown", ".MD"],
            mediaTypes: ["TEXT/MARKDOWN", "text/*"],
            autoSave: new FileAutoSaveOptions(FileAutoSaveTriggers.Idle, idleDelay: TimeSpan.FromSeconds(2)),
            preview: new FilePreviewOptions(true, TimeSpan.FromMilliseconds(250), splitByDefault: true));

        Assert.Equal([".markdown", ".md"], profile.Extensions);
        Assert.Equal(["text/*", "text/markdown"], profile.MediaTypes);
        Assert.True(profile.Supports(FileInteractionMode.View));
        Assert.True(profile.Supports(FileInteractionMode.Edit));
        Assert.False(profile.Supports(FileInteractionMode.Diff));
    }

    [Fact]
    public void Profile_RejectsShallowOrInconsistentCapabilities()
    {
        Assert.Throws<ArgumentException>(() => new FileInteractionProfileDescriptor(
            "none", FileInteractionCapabilities.View));
        Assert.Throws<ArgumentException>(() => new FileInteractionProfileDescriptor(
            "edit", FileInteractionCapabilities.Edit, extensions: [".txt"]));
        Assert.Throws<ArgumentException>(() => new FileInteractionProfileDescriptor(
            "autosave",
            FileInteractionCapabilities.View,
            extensions: [".txt"],
            autoSave: new FileAutoSaveOptions(FileAutoSaveTriggers.Idle, idleDelay: TimeSpan.FromSeconds(1))));
        Assert.Throws<ArgumentException>(() => new FileInteractionProfileDescriptor(
            "preview",
            FileInteractionCapabilities.View,
            extensions: [".txt"],
            preview: new FilePreviewOptions(enabled: true)));
    }

    [Fact]
    public void AutoSave_AllowsCompositeTriggers_AndRejectsMissingOrExtraneousValues()
    {
        var options = new FileAutoSaveOptions(
            FileAutoSaveTriggers.Interval
                | FileAutoSaveTriggers.Idle
                | FileAutoSaveTriggers.ChangeCount
                | FileAutoSaveTriggers.TextUnitCount,
            interval: TimeSpan.FromMinutes(1),
            idleDelay: TimeSpan.FromSeconds(2),
            changeCount: 20,
            textUnitCount: 200);

        Assert.True(options.Enabled);
        Assert.Equal(200, options.TextUnitCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.Interval));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.Idle));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.ChangeCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.ChangeCount,
            changeCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.TextUnitCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.TextUnitCount,
            textUnitCount: 0));
        Assert.Throws<ArgumentException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.None,
            idleDelay: TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentException>(() => new FileAutoSaveOptions(
            FileAutoSaveTriggers.None,
            textUnitCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileAutoSaveOptions(
            (FileAutoSaveTriggers)(1 << 12)));
    }

    [Fact]
    public void PreviewAndHistoryOptions_ValidateDisabledAndEnabledStates()
    {
        Assert.Throws<ArgumentException>(() => new FilePreviewOptions(splitByDefault: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FilePreviewOptions(true, TimeSpan.FromMilliseconds(-1)));
        Assert.Throws<ArgumentException>(() => new FileHistoryOptions(maxEntries: 10, maxBytes: 0));

        var history = new FileHistoryOptions(10, 4096);
        Assert.True(history.Enabled);
        Assert.False(FileHistoryOptions.Disabled.Enabled);
    }

    [Fact]
    public void HistoryState_RejectsNegativeDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileEditHistoryState(false, false, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileEditHistoryState(false, false, 0, -1));
        Assert.Throws<ArgumentException>(() => new FileEditHistoryState(true, false, 0, 0));
        Assert.Throws<ArgumentException>(() => new FileEditHistoryState(false, false, 1, 0));
    }

    [Fact]
    public void EditSnapshot_DefensivelyCopiesMutableInput()
    {
        byte[] bytes = [1, 2, 3];
        var snapshot = new FileEditSnapshot(File(), 1, bytes);

        bytes[0] = 9;

        Assert.Equal((byte)1, snapshot.Content.Span[0]);
    }

    [Fact]
    public async Task ContentLease_DisposesOwnedStreamAtMostOnce()
    {
        var stream = new CountingStream();
        var lease = new FileContentLease(stream, " TEXT/PLAIN ", revision: new FileContentRevision("r1"));

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(1, stream.DisposeCount);
        Assert.Equal("text/plain", lease.MediaType);
        Assert.Equal("r1", lease.Revision?.Value);
    }

    [Fact]
    public async Task SaveContent_IsReplayableAndRequestCarriesExpectedRevision()
    {
        var content = new ReplayableContent([1, 2, 3]);
        var request = new FileSaveRequest(
            File(),
            editRevision: 7,
            content,
            expectedRevision: new FileContentRevision("r1"),
            mediaType: " TEXT/PLAIN ",
            isAutomatic: true);

        await using var first = await request.Content.OpenReadAsync();
        await using var second = await request.Content.OpenReadAsync();

        Assert.NotSame(first, second);
        Assert.Equal(7, request.EditRevision);
        Assert.Equal("r1", request.ExpectedRevision?.Value);
        Assert.Equal("text/plain", request.MediaType);
        Assert.True(request.IsAutomatic);
    }

    [Fact]
    public void SaveConflict_PreservesExpectedAndActualRevisions()
    {
        var exception = new FileSaveConflictException(
            File(),
            new FileContentRevision("r1"),
            new FileContentRevision("r2"));

        Assert.Equal("r1", exception.ExpectedRevision?.Value);
        Assert.Equal("r2", exception.ActualRevision?.Value);
    }

    [Fact]
    public void AbstractionsAssembly_HasNoFrameworkOrApplicationDependencies()
    {
        var forbiddenPrefixes = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Caching",
            "CanDoItAll.Components",
            "CanDoItAll.Infrastructure",
            "CanDoItAll.Modules"
        };
        var references = typeof(FileReference).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? "");

        Assert.DoesNotContain(references, name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static FileReference File() => new("source", "docs/readme");

    private sealed class ReplayableContent(byte[] bytes) : IFileSaveContent
    {
        public long? Length => bytes.Length;

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }

    private sealed class CountingStream : MemoryStream
    {
        public int DisposeCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing && DisposeCount == 0)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }
}
