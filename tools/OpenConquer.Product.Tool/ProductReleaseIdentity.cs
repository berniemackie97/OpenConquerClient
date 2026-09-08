using System.Globalization;
using System.Security.Cryptography;

namespace OpenConquer.Product.Tool;

internal static class ProductReleaseIdentity
{
    private const int SequenceLength = 20;
    private const int DigestLength = 64;
    private const int GenerationNonceLength = 32;

    public static string Create(ulong releaseSequence, ReadOnlySpan<byte> manifestBytes)
    {
        if (releaseSequence == 0 || manifestBytes.IsEmpty)
        {
            throw new ArgumentException("A release identity requires a sequence and manifest bytes.");
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(manifestBytes, digest);
        return string.Create(CultureInfo.InvariantCulture, $"{releaseSequence:D20}-{Convert.ToHexStringLower(digest)}");
    }

    public static bool IsValid(string? value)
    {
        int canonicalLength = SequenceLength + 1 + DigestLength;
        if (value is null || value.Length is not (SequenceLength + 1 + DigestLength) and not (SequenceLength + 1 + DigestLength + 1 + GenerationNonceLength) || value[SequenceLength] != '-')
        {
            return false;
        }

        for (int index = 0; index < SequenceLength; index++)
        {
            if (value[index] is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        for (int index = SequenceLength + 1; index < value.Length; index++)
        {
            if (index == canonicalLength)
            {
                if (value[index] != '-')
                {
                    return false;
                }

                continue;
            }

            if (value[index] is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return ulong.TryParse(value.AsSpan(0, SequenceLength), NumberStyles.None, CultureInfo.InvariantCulture, out ulong sequence) && sequence > 0;
    }

    public static bool Matches(string releaseId, ulong releaseSequence, ReadOnlySpan<byte> manifestBytes)
    {
        if (!IsValid(releaseId) || releaseSequence == 0 || manifestBytes.IsEmpty)
        {
            return false;
        }

        string canonical = Create(releaseSequence, manifestBytes);
        return releaseId.StartsWith(canonical, StringComparison.Ordinal) && (releaseId.Length == canonical.Length || releaseId.Length == canonical.Length + 33);
    }
}
