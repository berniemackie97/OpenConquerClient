using System.Globalization;
using System.Security.Cryptography;

namespace OpenConquer.Launcher.Installation;

/// <summary>Binds a generation directory name to the exact signed manifest stored inside it.</summary>
internal static class ManagedReleaseId
{
    public static string Create(ulong releaseSequence, ReadOnlySpan<byte> manifestBytes, string? generationNonce = null)
    {
        if (releaseSequence == 0 || manifestBytes.IsEmpty)
        {
            throw new ArgumentException("A release identity requires a sequence and manifest bytes.");
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(manifestBytes, digest);

        string canonical = string.Create(CultureInfo.InvariantCulture, $"{releaseSequence:D20}-{Convert.ToHexStringLower(digest)}");
        if (generationNonce is null)
        {
            return canonical;
        }

        if (generationNonce.Length != 32 || generationNonce.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("The generation nonce is invalid.", nameof(generationNonce));
        }

        return canonical + '-' + generationNonce;
    }

    public static bool Matches(string releaseId, ulong releaseSequence, ReadOnlySpan<byte> manifestBytes)
    {
        if (!ManagedInstallationManifest.IsValidReleaseId(releaseId) || releaseSequence == 0 || manifestBytes.IsEmpty)
        {
            return false;
        }

        string canonical = Create(releaseSequence, manifestBytes);
        return releaseId.StartsWith(canonical, StringComparison.Ordinal) && (releaseId.Length == canonical.Length || releaseId.Length == canonical.Length + 33);
    }
}
