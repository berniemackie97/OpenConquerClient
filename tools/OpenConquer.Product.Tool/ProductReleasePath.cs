using System.Text;

namespace OpenConquer.Product.Tool;

internal static class ProductReleasePath
{
    public const int MaximumLength = 512;

    public static bool IsValid(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaximumLength || path[0] == '/' || path[^1] == '/')
        {
            return false;
        }

        if (path.Any(character => character == '\0' || character == '\\' || char.IsControl(character)))
        {
            return false;
        }

        return path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or "..") &&
            !segment.EndsWith(' ') && !segment.EndsWith('.')) && !Path.IsPathFullyQualified(path);
    }

    public static string PortableIdentity(string path)
    {
        if (!IsValid(path))
        {
            throw new ArgumentException("The release path is invalid.", nameof(path));
        }

        return path.Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
