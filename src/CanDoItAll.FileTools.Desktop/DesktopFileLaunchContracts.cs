namespace CanDoItAll.FileTools.Desktop;

public enum DesktopFileLaunchOperation
{
    Open,
    OpenContainingFolder
}

public enum DesktopFileLaunchFailureCode
{
    DesktopUnavailable,
    TargetNotFound,
    ApplicationNotFound,
    ProcessStartFailed
}

public sealed record DesktopFileLaunchFailure(
    DesktopFileLaunchFailureCode Code,
    string Message);

public sealed record DesktopFileLaunchRequest
{
    public DesktopFileLaunchRequest(
        string targetPath,
        DesktopFileLaunchOperation operation = DesktopFileLaunchOperation.Open,
        string? executablePath = null)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        TargetPath = ValidateAbsolutePath(targetPath, nameof(targetPath));
        Operation = operation;
        ExecutablePath = executablePath is null
            ? null
            : ValidateAbsolutePath(executablePath, nameof(executablePath));
    }

    public string TargetPath { get; }

    public DesktopFileLaunchOperation Operation { get; }

    public string? ExecutablePath { get; }

    private static string ValidateAbsolutePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (DesktopPhysicalPathSyntax.IsForeignOrAmbiguous(path))
        {
            throw new ArgumentException(
                "The path uses syntax that is not valid for this host.",
                parameterName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The path must be fully qualified.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}

internal static class DesktopPhysicalPathSyntax
{
    public static bool IsForeignOrAmbiguous(string path)
    {
        bool windowsDrive =
            path.Length >= 2 &&
            char.IsAsciiLetter(path[0]) &&
            path[1] == ':';
        bool windowsNetworkOrDevice =
            path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal);
        bool uri = path.Contains("://", StringComparison.Ordinal);

        return OperatingSystem.IsWindows()
            ? path.StartsWith("/", StringComparison.Ordinal) || uri
            : windowsDrive || windowsNetworkOrDevice || uri;
    }
}

public sealed record DesktopFileLaunchResult
{
    private DesktopFileLaunchResult(
        string? launchedPath,
        DesktopFileLaunchFailure? failure)
    {
        LaunchedPath = launchedPath;
        Failure = failure;
    }

    public bool Succeeded => Failure is null;

    public string? LaunchedPath { get; }

    public DesktopFileLaunchFailure? Failure { get; }

    public static DesktopFileLaunchResult Success(string launchedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launchedPath);
        return new DesktopFileLaunchResult(launchedPath, null);
    }

    public static DesktopFileLaunchResult Failed(
        DesktopFileLaunchFailureCode code,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new DesktopFileLaunchResult(null, new DesktopFileLaunchFailure(code, message));
    }
}

public interface IDesktopFileLauncher
{
    bool IsAvailable { get; }

    ValueTask<DesktopFileLaunchResult> LaunchAsync(
        DesktopFileLaunchRequest request,
        CancellationToken cancellationToken = default);
}
