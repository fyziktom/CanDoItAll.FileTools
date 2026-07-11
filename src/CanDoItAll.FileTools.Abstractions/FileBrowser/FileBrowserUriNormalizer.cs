namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Validates navigation targets before provider-controlled values cross into a browser.</summary>
public static class FileBrowserUriNormalizer
{
    /// <summary>
    /// Normalizes an optional HTTP(S) or same-host relative URI and rejects active or ambiguous targets.
    /// </summary>
    public static string? Normalize(string? value, string parameterName = "value")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        var isFragmentReference = candidate.StartsWith('#') && HasValidPercentEscapes(candidate);
        if (candidate.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            || candidate.Contains('\\')
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || !Uri.TryCreate(candidate, UriKind.RelativeOrAbsolute, out var uri)
            || (!isFragmentReference
                && !Uri.IsWellFormedUriString(candidate, UriKind.RelativeOrAbsolute)))
        {
            throw InvalidUri(parameterName);
        }

        if (!uri.IsAbsoluteUri)
        {
            return candidate;
        }

        if ((uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return candidate;
        }

        throw InvalidUri(parameterName);
    }

    private static ArgumentException InvalidUri(string parameterName)
        => new(
            "The value must be a well-formed HTTP(S) URI or a safe same-host relative URI.",
            parameterName);

    private static bool HasValidPercentEscapes(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }
}
