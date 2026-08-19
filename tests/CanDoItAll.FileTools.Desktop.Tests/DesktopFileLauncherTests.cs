using System.Diagnostics;
using CanDoItAll.FileTools.Desktop;

namespace CanDoItAll.FileTools.Desktop.Tests;

public sealed class DesktopFileLauncherTests : IDisposable
{
    private readonly string testRoot = Path.Combine(
        Path.GetTempPath(),
        "CanDoItAll.FileTools.Desktop.Tests",
        Guid.NewGuid().ToString("N"));

    public DesktopFileLauncherTests()
    {
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void Request_RejectsRelativePathsAndUnknownOperations()
    {
        Assert.Throws<ArgumentException>(() => new DesktopFileLaunchRequest("relative/file.txt"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopFileLaunchRequest(
            testRoot,
            (DesktopFileLaunchOperation)42));
    }

    [Fact]
    public void Request_rejects_foreign_or_ambiguous_absolute_path_syntax()
    {
        string foreignPath = OperatingSystem.IsWindows()
            ? "/var/tmp/report.txt"
            : @"C:\foreign\report.txt";

        Assert.Throws<ArgumentException>(() => new DesktopFileLaunchRequest(foreignPath));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<ArgumentException>(() => new DesktopFileLaunchRequest("//server/share/report.txt"));
            Assert.Throws<ArgumentException>(() => new DesktopFileLaunchRequest(@"\\server\share\report.txt"));
        }
    }

    [Theory]
    [InlineData((int)DesktopOperatingSystem.Windows, true, null, null, null, null, true)]
    [InlineData((int)DesktopOperatingSystem.Windows, false, null, null, null, null, false)]
    [InlineData((int)DesktopOperatingSystem.Linux, true, ":0", null, null, null, true)]
    [InlineData((int)DesktopOperatingSystem.Linux, true, null, "wayland-0", null, null, true)]
    [InlineData((int)DesktopOperatingSystem.Linux, true, null, null, null, null, false)]
    [InlineData((int)DesktopOperatingSystem.MacOs, true, null, null, "Apple_Terminal", null, true)]
    [InlineData((int)DesktopOperatingSystem.MacOs, true, null, null, null, null, false)]
    [InlineData((int)DesktopOperatingSystem.Unsupported, true, ":0", null, null, null, false)]
    public void Desktop_session_availability_is_host_specific_and_fail_closed(
        int operatingSystem,
        bool userInteractive,
        string? display,
        string? waylandDisplay,
        string? termProgram,
        string? bundleIdentifier,
        bool expected)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DISPLAY"] = display,
            ["WAYLAND_DISPLAY"] = waylandDisplay,
            ["TERM_PROGRAM"] = termProgram,
            ["__CFBundleIdentifier"] = bundleIdentifier
        };

        bool available = DesktopSessionAvailability.IsAvailable(
            userInteractive,
            (DesktopOperatingSystem)operatingSystem,
            name => environment[name]);

        Assert.Equal(expected, available);
    }

    [Fact]
    public async Task MissingTarget_ReturnsTypedFailureWithoutStartingProcess()
    {
        var starter = new RecordingProcessStarter();
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);

        DesktopFileLaunchResult result = await launcher.LaunchAsync(
            new DesktopFileLaunchRequest(Path.Combine(testRoot, "missing.xlsx")));

        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.TargetNotFound, result.Failure?.Code);
        Assert.Empty(starter.Starts);
    }

    [Fact]
    public async Task UnavailableDesktop_ReturnsTypedFailureWithoutStartingProcess()
    {
        string target = CreateFile("quarterly report.xlsx");
        var starter = new RecordingProcessStarter();
        var launcher = new DesktopFileLauncher(starter, () => false);

        DesktopFileLaunchResult result = await launcher.LaunchAsync(
            new DesktopFileLaunchRequest(target));

        Assert.False(launcher.IsAvailable);
        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.DesktopUnavailable, result.Failure?.Code);
        Assert.Empty(starter.Starts);
    }

    [Fact]
    public async Task Cancellation_before_desktop_delegation_does_not_start_a_process()
    {
        string target = CreateFile("quarterly report.xlsx");
        var starter = new RecordingProcessStarter();
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await launcher.LaunchAsync(
                new DesktopFileLaunchRequest(target),
                cancellation.Token));

        Assert.Empty(starter.Starts);
    }

    [Fact]
    public async Task Cancellation_during_availability_preflight_does_not_start_a_process()
    {
        string target = CreateFile("quarterly report.xlsx");
        var starter = new RecordingProcessStarter();
        using var cancellation = new CancellationTokenSource();
        var launcher = new DesktopFileLauncher(
            starter,
            () =>
            {
                cancellation.Cancel();
                return true;
            });

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await launcher.LaunchAsync(
                new DesktopFileLaunchRequest(target),
                cancellation.Token));

        Assert.Empty(starter.Starts);
    }

    [Fact]
    public async Task MissingExplicitApplication_ReturnsTypedFailureWithoutFallback()
    {
        string target = CreateFile("quarterly report.xlsx");
        var starter = new RecordingProcessStarter();
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);

        DesktopFileLaunchResult result = await launcher.LaunchAsync(new DesktopFileLaunchRequest(
            target,
            executablePath: Path.Combine(testRoot, "missing app.exe")));

        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.ApplicationNotFound, result.Failure?.Code);
        Assert.Empty(starter.Starts);
    }

    [Fact]
    public async Task SystemDefault_UsesShellExecuteWithExactTargetPath()
    {
        string target = CreateFile("quarterly report.xlsx");
        var starter = new RecordingProcessStarter();
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);

        DesktopFileLaunchResult result = await launcher.LaunchAsync(
            new DesktopFileLaunchRequest(target));

        StartedProcess started = Assert.Single(starter.Starts);
        Assert.True(result.Succeeded);
        Assert.Equal(target, result.LaunchedPath);
        Assert.Equal(target, started.FileName);
        Assert.True(started.UseShellExecute);
        Assert.Empty(started.Arguments);
    }

    [Fact]
    public async Task ExplicitApplication_UsesArgumentListWithoutShellOrPathQuoting()
    {
        string target = CreateFile("quarterly report.xlsx");
        string executable = CreateFile("preferred office app.exe");
        var starter = new RecordingProcessStarter();
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);

        DesktopFileLaunchResult result = await launcher.LaunchAsync(new DesktopFileLaunchRequest(
            target,
            executablePath: executable));

        StartedProcess started = Assert.Single(starter.Starts);
        Assert.True(result.Succeeded);
        Assert.Equal(executable, started.FileName);
        Assert.False(started.UseShellExecute);
        Assert.Equal([target], started.Arguments);
    }

    [Fact]
    public async Task OpenContainingFolder_UsesExactParentForFileAndFolderItselfForFolder()
    {
        string containingFolder = Directory.CreateDirectory(
            Path.Combine(testRoot, "folder with spaces")).FullName;
        string target = Path.Combine(containingFolder, "report.docx");
        File.WriteAllText(target, "test");
        var starter = new RecordingProcessStarter();
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);

        DesktopFileLaunchResult fileResult = await launcher.LaunchAsync(new DesktopFileLaunchRequest(
            target,
            DesktopFileLaunchOperation.OpenContainingFolder));
        DesktopFileLaunchResult folderResult = await launcher.LaunchAsync(new DesktopFileLaunchRequest(
            containingFolder,
            DesktopFileLaunchOperation.OpenContainingFolder));

        Assert.Equal(containingFolder, fileResult.LaunchedPath);
        Assert.Equal(containingFolder, folderResult.LaunchedPath);
        Assert.Equal([containingFolder, containingFolder], starter.Starts.Select(start => start.FileName));
        Assert.All(starter.Starts, start => Assert.True(start.UseShellExecute));
    }

    [Fact]
    public async Task ProcessStartFailure_ReturnsFailureAndDoesNotRetryWithSystemDefault()
    {
        string target = CreateFile("quarterly report.xlsx");
        string executable = CreateFile("preferred office app.exe");
        var starter = new RecordingProcessStarter
        {
            Exception = new InvalidOperationException($"start rejected for {target}")
        };
        DesktopFileLauncher launcher = CreateAvailableLauncher(starter);

        DesktopFileLaunchResult result = await launcher.LaunchAsync(new DesktopFileLaunchRequest(
            target,
            executablePath: executable));

        Assert.False(result.Succeeded);
        Assert.Equal(DesktopFileLaunchFailureCode.ProcessStartFailed, result.Failure?.Code);
        Assert.Equal("The requested application could not be started.", result.Failure?.Message);
        Assert.DoesNotContain(target, result.Failure?.Message, StringComparison.Ordinal);
        StartedProcess started = Assert.Single(starter.Starts);
        Assert.Equal(executable, started.FileName);
        Assert.False(started.UseShellExecute);
    }

    [Fact]
    public void ShellDelegationWithoutProcessHandle_IsAccepted()
    {
        var shellStart = new ProcessStartInfo { UseShellExecute = true };
        var directStart = new ProcessStartInfo { UseShellExecute = false };

        Assert.True(SystemDesktopProcessStarter.IsAccepted(shellStart, null));
        Assert.False(SystemDesktopProcessStarter.IsAccepted(directStart, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private string CreateFile(string name)
    {
        string path = Path.Combine(testRoot, name);
        File.WriteAllText(path, "test");
        return path;
    }

    private static DesktopFileLauncher CreateAvailableLauncher(IDesktopProcessStarter starter)
        => new(starter, () => true);

    private sealed class RecordingProcessStarter : IDesktopProcessStarter
    {
        public List<StartedProcess> Starts { get; } = [];

        public Exception? Exception { get; init; }

        public bool Start(ProcessStartInfo startInfo)
        {
            Starts.Add(new StartedProcess(
                startInfo.FileName,
                startInfo.UseShellExecute,
                startInfo.ArgumentList.ToArray()));
            if (Exception is not null)
            {
                throw Exception;
            }

            return true;
        }
    }

    private sealed record StartedProcess(
        string FileName,
        bool UseShellExecute,
        IReadOnlyList<string> Arguments);
}
