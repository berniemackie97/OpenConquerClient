using System.Text;
using System.Text.Json;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Manifest;

public sealed class ContentManifestReaderTests
{
    [Fact]
    public void ReadAcceptsManifestAtMaximumLength()
    {
        using MemoryStream serialized = new();
        ContentManifestWriter.Write(serialized, CreateManifest());

        byte[] payload = new byte[ContentManifestReader.MaximumLength];
        Array.Fill(payload, (byte)' ');
        serialized.ToArray().CopyTo(payload, 0);

        using MemoryStream input = new(payload, writable: false);
        ContentManifest manifest = ContentManifestReader.Read(input);

        Assert.Equal(1, manifest.FileCount);
        Assert.Equal(4, manifest.Length);
    }

    [Fact]
    public void ReadRejectsManifestBeyondMaximumLength()
    {
        byte[] payload = new byte[ContentManifestReader.MaximumLength + 1];
        Array.Fill(payload, (byte)' ');

        using MemoryStream input = new(payload, writable: false);
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ContentManifestReader.Read(input));

        Assert.Contains("exceeds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadRejectsMalformedJsonAsInvalidContent()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes("{\"schemaVersion\":"));
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ContentManifestReader.Read(input));

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    private static ContentManifest CreateManifest() =>
        new("5517", new string('0', 64),
        [
            new ContentManifestEntry("data/a.bin", "data/a.bin", 4, new string('a', 64), "unknown"),
        ]);
}
