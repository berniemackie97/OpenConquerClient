namespace OpenConquer.Content;

internal static class ContentRead
{
    public static byte[] ReadRequiredBytes(IClientContentSource source, string contentPath, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        using Stream stream = source.OpenRequiredRead(contentPath);

        return ReadBytes(stream, contentPath, maximumLength);
    }

    public static byte[] ReadBytes(Stream stream, string contentPath, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        if (!stream.CanRead)
        {
            throw new ArgumentException("Content stream must be readable.", nameof(stream));
        }

        if (stream.CanSeek && stream.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"Client content file '{contentPath}' is {stream.Length} bytes; the limit is {maximumLength} bytes."
            );
        }

        using MemoryStream destination = new(capacity: stream.CanSeek ? checked((int)stream.Length) : 0);
        byte[] buffer = new byte[81920];
        int totalLength = 0;

        while (true)
        {
            int bytesRead = stream.Read(buffer);

            if (bytesRead == 0)
            {
                return destination.ToArray();
            }

            totalLength = checked(totalLength + bytesRead);

            if (totalLength > maximumLength)
            {
                throw new InvalidDataException(
                    $"Client content file '{contentPath}' exceeds the {maximumLength}-byte limit."
                );
            }

            destination.Write(buffer, 0, bytesRead);
        }
    }
}
