namespace OpenConquer.Networking.Authentication;

/// <summary>The realm identity accepted by the OpenConquer catalog and AccountServer.</summary>
internal static class RealmProtocolName
{
    internal const int MaximumLength = 15;

    internal static void Validate(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        ValidateLength(value.Length, parameterName);
        foreach (char character in value)
        {
            ValidateCharacter(character, parameterName);
        }
    }

    internal static void Validate(ReadOnlySpan<byte> value, string parameterName)
    {
        ValidateLength(value.Length, parameterName);
        foreach (byte character in value)
        {
            ValidateCharacter((char)character, parameterName);
        }
    }

    private static void ValidateLength(int length, string parameterName)
    {
        if (length is < 1 or > MaximumLength)
        {
            throw new ArgumentException("A realm protocol name must contain 1 to 15 ASCII bytes.", parameterName);
        }
    }

    private static void ValidateCharacter(char character, string parameterName)
    {
        if (character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '(' or ')' or '-'))
        {
            throw new ArgumentException("A realm protocol name may contain only ASCII letters, digits, underscore, parentheses and hyphen.", parameterName);
        }
    }
}
