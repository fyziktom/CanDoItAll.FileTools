using System.Text;
using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Providers.FileSystem;

internal sealed record FileSystemContinuationState(
    int Version,
    string SourceId,
    string ParentKey,
    string? Revision,
    string QueryFingerprint,
    string ConsistencyToken,
    int Offset);

/// <summary>Encodes and validates provider-owned offset cursors.</summary>
internal static class FileSystemContinuationTokenCodec
{
    private const int CurrentVersion = 1;
    private const int MaximumEncodedLength = 16 * 1024;

    public static string Encode(
        FileBrowserBrowseRequest request,
        string consistencyToken,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(consistencyToken);
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var state = new FileSystemContinuationState(
            CurrentVersion,
            request.ParentKey.SourceId.Value,
            request.ParentKey.Value,
            request.ParentKey.Revision,
            FileBrowserQueryFingerprint.From(request).Value,
            consistencyToken,
            offset);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(state.Version);
            writer.Write(state.SourceId);
            writer.Write(state.ParentKey);
            writer.Write(state.Revision is not null);
            if (state.Revision is not null)
            {
                writer.Write(state.Revision);
            }

            writer.Write(state.QueryFingerprint);
            writer.Write(state.ConsistencyToken);
            writer.Write(state.Offset);
        }

        return ToBase64Url(stream.ToArray());
    }

    public static int DecodeAndValidate(
        string continuationToken,
        FileBrowserBrowseRequest request,
        string consistencyToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(continuationToken);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(consistencyToken);

        FileSystemContinuationState state;
        try
        {
            if (continuationToken.Length > MaximumEncodedLength)
            {
                throw new FormatException("Oversized continuation token.");
            }

            var normalized = continuationToken.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            var payload = Convert.FromBase64String(normalized);
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var version = reader.ReadInt32();
            var sourceId = reader.ReadString();
            var parentKey = reader.ReadString();
            var revision = reader.ReadBoolean() ? reader.ReadString() : null;
            var queryFingerprint = reader.ReadString();
            var decodedConsistencyToken = reader.ReadString();
            var offset = reader.ReadInt32();
            if (stream.Position != stream.Length)
            {
                throw new FormatException("Trailing continuation-token data.");
            }

            state = new FileSystemContinuationState(
                version,
                sourceId,
                parentKey,
                revision,
                queryFingerprint,
                decodedConsistencyToken,
                offset);
        }
        catch (Exception exception) when (exception is FormatException
                                               or EndOfStreamException
                                               or IOException
                                               or DecoderFallbackException
                                               or OverflowException)
        {
            throw StaleCursor("The filesystem continuation token is invalid.");
        }

        var expectedFingerprint = FileBrowserQueryFingerprint.From(request).Value;
        if (state.Version != CurrentVersion
            || !string.Equals(state.SourceId, request.ParentKey.SourceId.Value, StringComparison.Ordinal)
            || !string.Equals(state.ParentKey, request.ParentKey.Value, StringComparison.Ordinal)
            || !string.Equals(state.Revision, request.ParentKey.Revision, StringComparison.Ordinal)
            || !string.Equals(state.QueryFingerprint, expectedFingerprint, StringComparison.Ordinal)
            || !string.Equals(state.ConsistencyToken, consistencyToken, StringComparison.Ordinal)
            || state.Offset < 0)
        {
            throw StaleCursor("The filesystem continuation token no longer matches this browse query.");
        }

        return state.Offset;
    }

    private static string ToBase64Url(byte[] payload)
        => Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static FileBrowserProviderException StaleCursor(string message)
        => FileSystemProviderErrors.Create(
            FileBrowserErrorCode.StaleCursor,
            message,
            isRetryable: true);
}
