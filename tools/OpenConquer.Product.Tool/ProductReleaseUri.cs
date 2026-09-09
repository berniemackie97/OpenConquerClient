namespace OpenConquer.Product.Tool;

internal static class ProductReleaseUri
{
    public const int MaximumLength = 2048;

    public static bool IsValidHttps(Uri? uri)
    {
        return uri is not null && uri.IsAbsoluteUri &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            uri.OriginalString.Length <= MaximumLength && string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
            uri.HostNameType != UriHostNameType.Unknown &&
            !uri.OriginalString.Any(character => character == '\\' ||
                char.IsControl(character));
    }

    public static bool TryParseBase(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength ||
            value.Any(character => character == '\\' || char.IsControl(character)) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) ||
            !IsValidHttps(parsed) ||
            !parsed.AbsolutePath.EndsWith('/'))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
