namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Normalized provider and runtime failure categories.</summary>
public enum FileBrowserErrorCode
{
    NotFound,
    InvalidLocation,
    Unauthorized,
    Forbidden,
    Offline,
    Unavailable,
    Timeout,
    RateLimited,
    Unsupported,
    Conflict,
    StaleCursor,
    CorruptProviderResponse,
    ProviderFailure,
    InvalidOperation
}

/// <summary>A safe error projection suitable for renderers and telemetry.</summary>
public sealed record FileBrowserError
{
    public FileBrowserError(
        FileBrowserErrorCode code,
        string message,
        bool isRetryable = false,
        string? technicalDetail = null,
        string? correlationId = null,
        TimeSpan? retryAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter));
        }

        Code = code;
        Message = message.Trim();
        IsRetryable = isRetryable;
        TechnicalDetail = string.IsNullOrWhiteSpace(technicalDetail) ? null : technicalDetail.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        RetryAfter = retryAfter;
    }

    public FileBrowserErrorCode Code { get; }

    public string Message { get; }

    public bool IsRetryable { get; }

    public string? TechnicalDetail { get; }

    public string? CorrelationId { get; }

    public TimeSpan? RetryAfter { get; }
}

/// <summary>A provider exception carrying a normalized, renderer-safe failure.</summary>
public sealed class FileBrowserProviderException : Exception
{
    public FileBrowserProviderException(FileBrowserError error, Exception? innerException = null)
        : base(error?.Message, innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public FileBrowserError Error { get; }
}
