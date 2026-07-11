using System.Buffers;
using System.Text;

namespace CanDoItAll.FileTools.FileInteraction.Components;

public sealed class FileInteractionContentTooLargeException : InvalidOperationException
{
    public FileInteractionContentTooLargeException(long maximumBytes)
        : base($"The file exceeds the configured {maximumBytes:N0}-byte interaction limit.")
    {
        MaximumBytes = maximumBytes;
    }

    public long MaximumBytes { get; }
}

public sealed record FileInteractionLoadedContent(
    ReadOnlyMemory<byte> Content,
    string? MediaType,
    FileContentRevision? Revision);

/// <summary>Reads a bounded content lease without retaining the provider-owned stream.</summary>
public sealed class FileInteractionContentLoader
{
    public async ValueTask<FileInteractionLoadedContent> LoadAsync(
        IFileContentSource source,
        FileInteractionRequest request,
        int maximumBytes,
        CancellationToken cancellationToken = default)
        => await LoadAsync(
            source,
            request,
            maximumBytes,
            static (_, _) => true,
            cancellationToken).ConfigureAwait(false);

    internal async ValueTask<FileInteractionLoadedContent> LoadAsync(
        IFileContentSource source,
        FileInteractionRequest request,
        int maximumBytes,
        Func<string?, FileContentRevision?, bool> shouldReadContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(shouldReadContent);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        await using var lease = await source.OpenReadAsync(
            new FileContentReadRequest(request.File, length: maximumBytes + 1L),
            cancellationToken).ConfigureAwait(false);
        var mediaType = lease.MediaType ?? request.MediaType;
        var revision = lease.Revision ?? request.ContentRevision;
        if (!shouldReadContent(mediaType, revision))
        {
            return new FileInteractionLoadedContent(ReadOnlyMemory<byte>.Empty, mediaType, revision);
        }

        if (lease.Length > maximumBytes)
        {
            throw new FileInteractionContentTooLargeException(maximumBytes);
        }

        var initialCapacity = lease.Length is > 0 and <= int.MaxValue
            ? (int)Math.Min(lease.Length.Value, Math.Min(maximumBytes, 81920))
            : 0;
        using var buffer = new MemoryStream(initialCapacity);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Min(81920, maximumBytes));
        try
        {
            while (true)
            {
                var read = await lease.Stream.ReadAsync(rented, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > maximumBytes)
                {
                    throw new FileInteractionContentTooLargeException(maximumBytes);
                }

                await buffer.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return new FileInteractionLoadedContent(
            buffer.ToArray(),
            mediaType,
            revision);
    }
}

internal sealed class FileInteractionTextBuffer
{
    private FileInteractionTextBuffer(string text, Encoding encoding, bool includePreamble)
    {
        Text = text;
        Encoding = encoding;
        IncludePreamble = includePreamble;
    }

    public string Text { get; }

    public Encoding Encoding { get; }

    public bool IncludePreamble { get; }

    public string EncodingName => Encoding.WebName;

    public byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var payload = Encoding.GetBytes(text);
        if (!IncludePreamble)
        {
            return payload;
        }

        var preamble = Encoding.GetPreamble();
        var result = new byte[preamble.Length + payload.Length];
        preamble.CopyTo(result, 0);
        payload.CopyTo(result, preamble.Length);
        return result;
    }

    public static FileInteractionTextBuffer Decode(ReadOnlyMemory<byte> content)
    {
        var bytes = content.Span;
        Encoding encoding;
        var preambleLength = 0;
        if (bytes.StartsWith(Encoding.UTF8.GetPreamble()))
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
            preambleLength = Encoding.UTF8.GetPreamble().Length;
        }
        else if (bytes.StartsWith(Encoding.Unicode.GetPreamble()))
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
            preambleLength = Encoding.Unicode.GetPreamble().Length;
        }
        else if (bytes.StartsWith(Encoding.BigEndianUnicode.GetPreamble()))
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
            preambleLength = Encoding.BigEndianUnicode.GetPreamble().Length;
        }
        else
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        }

        try
        {
            return new FileInteractionTextBuffer(
                encoding.GetString(bytes[preambleLength..]),
                encoding,
                preambleLength > 0);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The text content is not valid UTF-8 or BOM-qualified UTF-16.", exception);
        }
    }
}
