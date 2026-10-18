namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Native 5517 account-seeded keypad mapping. This is a compatibility transform, not encryption.
/// </summary>
internal static class LoginPasswordKeypadEncoder
{
    internal const int MaximumAccountLength = 32;
    internal const int MaximumPasswordLength = 15;
    internal const int EncodedFieldLength = CredentialRc5.StandardFieldLength;

    private readonly record struct KeypadRow(byte ScanCode, char Shifted, char Plain);

    // Native 48-row keyboard table at 0x936768, preserving first-match order.
    private static readonly KeypadRow[] s_rows =
    [
        new(0x02, '1', '1'), new(0x03, '@', '2'), new(0x04, '3', '3'),
        new(0x05, '4', '4'), new(0x06, '5', '5'), new(0x07, '6', '6'),
        new(0x08, '7', '7'), new(0x09, '8', '8'), new(0x0A, '9', '9'),
        new(0x0B, '0', '0'), new(0x4F, '1', '1'), new(0x50, '2', '2'),
        new(0x51, '3', '3'), new(0x4B, '4', '4'), new(0x4C, '5', '5'),
        new(0x4D, '6', '6'), new(0x47, '7', '7'), new(0x48, '8', '8'),
        new(0x49, '9', '9'), new(0x52, '0', '0'), new(0x1E, 'A', 'a'),
        new(0x30, 'B', 'b'), new(0x2E, 'C', 'c'), new(0x20, 'D', 'd'),
        new(0x12, 'E', 'e'), new(0x21, 'F', 'f'), new(0x22, 'G', 'g'),
        new(0x23, 'H', 'h'), new(0x17, 'I', 'i'), new(0x24, 'J', 'j'),
        new(0x25, 'K', 'k'), new(0x26, 'L', 'l'), new(0x32, 'M', 'm'),
        new(0x31, 'N', 'n'), new(0x18, 'O', 'o'), new(0x19, 'P', 'p'),
        new(0x10, 'Q', 'q'), new(0x13, 'R', 'r'), new(0x1F, 'S', 's'),
        new(0x14, 'T', 't'), new(0x16, 'U', 'u'), new(0x2F, 'V', 'v'),
        new(0x11, 'W', 'w'), new(0x2D, 'X', 'x'), new(0x15, 'Y', 'y'),
        new(0x2C, 'Z', 'z'), new(0x34, '>', '.'), new(0x0C, '_', '-'),
    ];

    /// <summary>
    /// Writes a NUL-padded 32-byte plaintext credential field for seeded RC5.
    /// Rejects unrepresentable input rather than silently truncating a password.
    /// Sources remain caller-owned and are never formatted, logged, or copied to strings.
    /// </summary>
    internal static void Encode(ReadOnlySpan<byte> accountName, ReadOnlySpan<char> password, Span<byte> destination)
    {
        if (destination.Length != EncodedFieldLength)
        {
            throw new ArgumentException("Keypad destination must be exactly 32 bytes.", nameof(destination));
        }

        if (accountName.Length is < 1 or > MaximumAccountLength)
        {
            throw new ArgumentException("Invalid account name length.", nameof(accountName));
        }

        foreach (byte value in accountName)
        {
            if (value is < 0x20 or 0x7F)
            {
                throw new ArgumentException("Account name contains a control byte.", nameof(accountName));
            }
        }

        if (password.Length is < 1 or > MaximumPasswordLength)
        {
            throw new ArgumentException("Invalid password length.", nameof(password));
        }

        // Validate every character before modifying the caller's destination.
        foreach (char character in password)
        {
            if (!TryGetKeyValue(character, out _))
            {
                throw new ArgumentException("Password contains an unsupported native keypad character.", nameof(password));
            }
        }

        Span<byte> salt = stackalloc byte[16];
        Span<byte> values = stackalloc byte[256];
        Span<byte> weights = stackalloc byte[256];
        Span<byte> indexByValue = stackalloc byte[256];
        try
        {
            int signedSum = 0;
            foreach (byte value in accountName)
            {
                signedSum += unchecked((sbyte)value);
            }

            uint state = unchecked((uint)signedSum);
            for (int index = 0; index < salt.Length; index++)
            {
                state = unchecked(state * 0x343FDu + 0x269EC3u);
                salt[index] = (byte)(state >> 16);
            }

            values[0] = 0;
            weights[0] = 0;
            for (int index = 1; index < values.Length; index++)
            {
                values[index] = (byte)index;
                weights[index] = (byte)(index ^ salt[index & 15]);
            }

            // Native descending selection sort uses a strict greater-than comparison.
            // Sorting by another algorithm would alter equal-weight permutation positions.
            for (int index = 1; index < values.Length; index++)
            {
                for (int candidate = index + 1; candidate < values.Length; candidate++)
                {
                    if (weights[index] >= weights[candidate])
                    {
                        continue;
                    }

                    (values[index], values[candidate]) = (values[candidate], values[index]);
                    (weights[index], weights[candidate]) = (weights[candidate], weights[index]);
                }
            }

            indexByValue[0] = 0;
            for (int index = 1; index < values.Length; index++)
            {
                indexByValue[values[index]] = (byte)index;
            }

            destination.Clear();
            for (int index = 0; index < password.Length; index++)
            {
                _ = TryGetKeyValue(password[index], out byte value);
                destination[index] = indexByValue[value];
            }
        }
        finally
        {
            salt.Clear();
            values.Clear();
            weights.Clear();
            indexByValue.Clear();
        }
    }

    private static bool TryGetKeyValue(char character, out byte value)
    {
        foreach (KeypadRow row in s_rows)
        {
            if (row.Shifted == character)
            {
                value = (byte)(row.ScanCode + 0x80);
                return true;
            }

            if (row.Plain == character)
            {
                value = row.ScanCode;
                return true;
            }
        }

        value = 0;
        return false;
    }
}
