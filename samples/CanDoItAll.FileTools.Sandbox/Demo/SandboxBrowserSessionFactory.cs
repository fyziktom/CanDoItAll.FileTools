using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Providers.FileSystem;

namespace CanDoItAll.FileTools.Sandbox.Demo;

public sealed class SandboxBrowserSessionFactory(SandboxFileSystemRoot fileSystemRoot)
{
    public IFileBrowserSession Create(SandboxBrowserScenario scenario)
    {
        IReadOnlyList<IFileBrowserProvider> providers = scenario switch
        {
            SandboxBrowserScenario.Healthy =>
            [
                DemoFileBrowserProvider.CreateProjectSource(),
                DemoFileBrowserProvider.CreateSharedSource(),
                fileSystemRoot.CreateProvider("local", "Workspace local")
            ],
            SandboxBrowserScenario.Empty => [DemoFileBrowserProvider.CreateEmptySource()],
            SandboxBrowserScenario.PartialWarning => [DemoFileBrowserProvider.CreateWarningSource()],
            SandboxBrowserScenario.RetryableError => [DemoFileBrowserProvider.CreateRetryableSource()],
            SandboxBrowserScenario.LiveFileSystem => [fileSystemRoot.CreateProvider("filesystem", "Live filesystem")],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        return new FileBrowserSession(
            providers,
            new FileBrowserSessionOptions(pageSize: 8));
    }

    public void MutateLiveFileSystem() => fileSystemRoot.CreateOrUpdateLiveFile();

    public bool TryCreateLiveInteraction(
        FileBrowserItem item,
        out SandboxInteractionSelection? selection)
    {
        ArgumentNullException.ThrowIfNull(item);
        string sourceId = item.Key.SourceId.Value;
        if (item.IsContainer
            || sourceId is not ("local" or "filesystem"))
        {
            selection = null;
            return false;
        }

        // This is the deliberate host authorization bridge: only the two generated-root source IDs
        // are translated from a browser occurrence key into an independent interaction reference.
        FileSystemFileBrowserProvider provider = fileSystemRoot.CreateProvider(sourceId, "Authorized sandbox read");
        var reference = new FileReference(sourceId, item.Key.Value);
        selection = new SandboxInteractionSelection(
            $"live-{sourceId}-{item.Key.Value}",
            item.Name,
            "Fresh read-only content through the filesystem provider's IFileContentSource bridge.",
            new FileInteractionRequest(
                reference,
                item.Name,
                FileInteractionMode.View,
                item.MediaType,
                item.Size),
            provider,
            CanPersist: false,
            CanInjectSaveOutcome: false,
            IsLiveFileSystem: true);
        return true;
    }
}

public sealed class SandboxFileSystemRoot : IDisposable
{
    private readonly string basePath;
    private readonly string rootPath;
    private int disposed;
    private int mutationRevision;

    public SandboxFileSystemRoot()
    {
        basePath = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "CanDoItAll.FileTools.Sandbox"));
        rootPath = Path.Combine(basePath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        Seed();
    }

    public FileSystemFileBrowserProvider CreateProvider(string sourceId, string displayName)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return new FileSystemFileBrowserProvider(new FileSystemFileBrowserOptions(
            new FileBrowserSourceId(sourceId),
            rootPath,
            displayName,
            recommendedPageSize: 8));
    }

    public void CreateOrUpdateLiveFile()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        int revision = Interlocked.Increment(ref mutationRevision);
        File.WriteAllText(
            Path.Combine(rootPath, "live-change.txt"),
            $"Sandbox mutation revision {revision}. Refresh the browser to observe this file.");
    }

    private void Seed()
    {
        string notes = Directory.CreateDirectory(Path.Combine(rootPath, "notes")).FullName;
        string exports = Directory.CreateDirectory(Path.Combine(rootPath, "exports")).FullName;
        string media = Directory.CreateDirectory(Path.Combine(rootPath, "media")).FullName;
        File.WriteAllText(Path.Combine(rootPath, "README.md"), "# Live sandbox\n\nRefresh the browser after external changes.");
        File.WriteAllText(Path.Combine(rootPath, "project.json"), "{ \"name\": \"file-tools-sandbox\" }");
        File.WriteAllText(Path.Combine(notes, "handoff.md"), "# Handoff\n\nThe filesystem provider performs fresh reads.");
        File.WriteAllText(Path.Combine(notes, "todo.txt"), "Validate compact and minimal layouts.");
        File.WriteAllText(Path.Combine(exports, "metrics.csv"), "name,value\nfiles,9\nsources,3\n");
        File.WriteAllBytes(Path.Combine(exports, "report.pdf"), "%PDF-1.4\n%sandbox"u8.ToArray());
        File.WriteAllBytes(Path.Combine(media, "cover.png"), [137, 80, 78, 71, 13, 10, 26, 10]);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        string fullRoot = Path.GetFullPath(rootPath);
        string? parent = Directory.GetParent(fullRoot)?.FullName;
        bool isOwnedRoot = string.Equals(parent, basePath, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(Path.GetFileName(fullRoot), "N", out _);
        if (!isOwnedRoot)
        {
            return;
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(fullRoot);
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(fullRoot, recursive: true);
            }
        }
        catch (DirectoryNotFoundException)
        {
            // The generated sandbox root was already removed.
        }
        catch (IOException)
        {
            // The operating system may still own a short-lived read handle during shutdown.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup for a temporary sandbox fixture.
        }
    }
}
