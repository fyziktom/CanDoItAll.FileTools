using System.Security;
using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Providers.FileSystem;

/// <summary>Normalizes BCL filesystem failures without disclosing paths or swallowing cancellation.</summary>
internal static class FileSystemProviderErrors
{
    public static T Execute<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileBrowserProviderException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw Create(
                FileBrowserErrorCode.Forbidden,
                "Access to the requested filesystem location was denied.");
        }
        catch (SecurityException)
        {
            throw Create(
                FileBrowserErrorCode.Forbidden,
                "Access to the requested filesystem location was denied.");
        }
        catch (FileNotFoundException)
        {
            throw Create(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem item no longer exists.");
        }
        catch (DirectoryNotFoundException)
        {
            throw Create(
                FileBrowserErrorCode.NotFound,
                "The requested filesystem directory no longer exists.");
        }
        catch (PathTooLongException)
        {
            throw Create(
                FileBrowserErrorCode.InvalidLocation,
                "The requested filesystem path is too long.");
        }
        catch (ArgumentException)
        {
            throw Create(
                FileBrowserErrorCode.InvalidLocation,
                "The requested filesystem location is invalid.");
        }
        catch (NotSupportedException)
        {
            throw Create(
                FileBrowserErrorCode.Unsupported,
                "The requested filesystem operation is not supported on this platform.");
        }
        catch (IOException)
        {
            throw Create(
                FileBrowserErrorCode.Unavailable,
                "The requested filesystem location is temporarily unavailable.",
                isRetryable: true);
        }
    }

    public static FileBrowserProviderException Create(
        FileBrowserErrorCode code,
        string message,
        bool isRetryable = false)
        => new(new FileBrowserError(code, message, isRetryable));
}
