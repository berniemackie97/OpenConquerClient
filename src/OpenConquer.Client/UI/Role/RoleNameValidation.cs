using System.Text;

namespace OpenConquer.Client.UI.Role;

internal enum RoleNameValidationResult
{
    Valid,
    InvalidLength,
    InvalidGlyph,
}

internal static class RoleNameValidation
{
    private static readonly Encoding s_encoding = CodePagesEncodingProvider.Instance.GetEncoding(1252,
        EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) ??
        throw new InvalidOperationException("The native 5517 name code page is unavailable.");

    public static RoleNameValidationResult Validate(string name, Func<Rune, bool> hasGlyphInk, out byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(hasGlyphInk);

        encoded = [];
        try
        {
            encoded = s_encoding.GetBytes(name);
        }
        catch (EncoderFallbackException)
        {
            return RoleNameValidationResult.InvalidGlyph;
        }

        if (encoded.Length is < 4 or > 15)
        {
            return RoleNameValidationResult.InvalidLength;
        }

        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            // Native 5517 accepts a period only when it is neither the first nor the last encoded character.
            if (character == '.' && index > 0 && index < name.Length - 1)
            {
                continue;
            }

            if (!hasGlyphInk(new Rune(character)))
            {
                return RoleNameValidationResult.InvalidGlyph;
            }
        }

        return RoleNameValidationResult.Valid;
    }
}
