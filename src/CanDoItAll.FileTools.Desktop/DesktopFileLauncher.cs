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
            cancellationToken.ThrowIfCancellationRequested();
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
        => DesktopSessionAvailability.IsAvailable(
            Environment.UserInteractive,
            DesktopOperatingSystemExtensions.CaptureCurrent(),
            Environment.GetEnvironmentVariable);
}

internal enum DesktopOperatingSystem
{
    Windows,
    Linux,
    MacOs,
    Unsupported
}

internal static class DesktopOperatingSystemExtensions
{
    public static DesktopOperatingSystem CaptureCurrent()
        => OperatingSystem.IsWindows()
            ? DesktopOperatingSystem.Windows
            : OperatingSystem.IsLinux()
                ? DesktopOperatingSystem.Linux
                : OperatingSystem.IsMacOS()
                    ? DesktopOperatingSystem.MacOs
                    : DesktopOperatingSystem.Unsupported;
}

internal static class DesktopSessionAvailability
{
    public static bool IsAvailable(
        bool userInteractive,
        DesktopOperatingSystem operatingSystem,
        Func<string, string?> environmentVariableReader)
    {
        ArgumentNullException.ThrowIfNull(environmentVariableReader);
        if (!userInteractive)
        {
            return false;
        }

        return operatingSystem switch
        {
            DesktopOperatingSystem.Windows => true,
            DesktopOperatingSystem.Linux =>
                HasValue(environmentVariableReader("DISPLAY")) ||
                HasValue(environmentVariableReader("WAYLAND_DISPLAY")),
            DesktopOperatingSystem.MacOs =>
                HasValue(environmentVariableReader("TERM_PROGRAM")) ||
                HasValue(environmentVariableReader("__CFBundleIdentifier")),
            _ => false
        };
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
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
        return IsAccepted(startInfo, process);
    }

    internal static bool IsAccepted(ProcessStartInfo startInfo, Process? process)
        => process is not null || startInfo.UseShellExecute;
}
