using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Launcher.Updates;

internal static class ReleaseUriPolicy
{
    public const int MaximumLength = 2048;

    public static bool TryParseHttps(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength ||
            value.Any(character => character == '\\' || char.IsControl(character)) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) || !IsValidHttps(parsed))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool IsValidHttps(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            uri.OriginalString.Length > MaximumLength || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.HostNameType == UriHostNameType.Unknown)
        {
            return false;
        }

        return !uri.OriginalString.Any(character => character == '\\' || char.IsControl(character));
    }

    public static bool EqualsExact(Uri expected, Uri actual) =>
        string.Equals(expected.AbsoluteUri, actual.AbsoluteUri, StringComparison.Ordinal);
}
