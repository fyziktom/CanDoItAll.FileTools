using System.ComponentModel;
using System.Diagnostics;

namespace CanDoItAll.FileTools.Desktop;

public sealed class DesktopFileLauncher : IDesktopFileLauncher
{
    private readonly IDesktopProcessStarter processStarter;
    private readonly Func<bool> availability;

    public DesktopFileLauncher()
        : this(new SystemDesktopProcessStarter(), IsSupportedInteractiveProcess)
    {
    }

    internal DesktopFileLauncher(
        IDesktopProcessStarter processStarter,
        Func<bool>? availability = null)
    {
        this.processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
        this.availability = availability ?? IsSupportedInteractiveProcess;
    }

    public bool IsAvailable => availability();

    public ValueTask<DesktopFileLaunchResult> LaunchAsync(
        DesktopFileLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable)
        {
            return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.DesktopUnavailable,
                "Desktop file launching is not available in this process."));
        }

        bool isFile = File.Exists(request.TargetPath);
        bool isDirectory = Directory.Exists(request.TargetPath);
        if (!isFile && !isDirectory)
        {
            return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.TargetNotFound,
                "The file or folder does not exist."));
        }

        if (request.ExecutablePath is not null && !File.Exists(request.ExecutablePath))
        {
            return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.ApplicationNotFound,
                "The configured application does not exist."));
        }

        string launchPath = ResolveLaunchPath(request, isFile);
        ProcessStartInfo startInfo = CreateStartInfo(launchPath, request.ExecutablePath);
        try
        {
            if (!processStarter.Start(startInfo))
            {
                return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                    DesktopFileLaunchFailureCode.ProcessStartFailed,
                    "The operating system did not start the requested application."));
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception
                or InvalidOperationException
                or NotSupportedException)
        {
            return ValueTask.FromResult(DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.ProcessStartFailed,
                "The requested application could not be started."));
        }

        return ValueTask.FromResult(DesktopFileLaunchResult.Success(launchPath));
    }

    private static string ResolveLaunchPath(
        DesktopFileLaunchRequest request,
        bool targetIsFile)
    {
        if (request.Operation != DesktopFileLaunchOperation.OpenContainingFolder
            || !targetIsFile)
        {
            return request.TargetPath;
        }

        return Path.GetDirectoryName(request.TargetPath)
            ?? throw new InvalidOperationException("The target file does not have a containing folder.");
    }

    private static ProcessStartInfo CreateStartInfo(
        string launchPath,
        string? executablePath)
    {
        if (executablePath is null)
        {
            return new ProcessStartInfo
            {
                FileName = launchPath,
                UseShellExecute = true
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(launchPath);
        return startInfo;
    }

    private static bool IsSupportedInteractiveProcess()
        => Environment.UserInteractive
            && (OperatingSystem.IsWindows()
                || OperatingSystem.IsLinux()
                || OperatingSystem.IsMacOS());
}

internal interface IDesktopProcessStarter
{
    bool Start(ProcessStartInfo startInfo);
}

internal sealed class SystemDesktopProcessStarter : IDesktopProcessStarter
{
    public bool Start(ProcessStartInfo startInfo)
    {
        using Process? process = Process.Start(startInfo);
        return process is not null;
    }
}
