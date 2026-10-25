using OpenConquer.Content.Media;

namespace OpenConquer.Content.Tests.Login;

public sealed class EmbeddedMediaTests
{
    [Fact]
    public void EveryEmbeddedResource_UsesItsAssetPathAsItsName()
    {
        string[] names = typeof(EmbeddedMedia).Assembly.GetManifestResourceNames();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.True(name.StartsWith("Assets/Login/", StringComparison.Ordinal) ||
            name.StartsWith("Assets/Role/", StringComparison.Ordinal), name));
        Assert.DoesNotContain(names, name => name.Contains('\\', StringComparison.Ordinal));
    }

    [Fact]
    public void Read_RejectsContentThatDoesNotMatchItsDigest()
    {
        const string path = "Login/Server/Status.rgba.br";
        const string wrongDigest = "0000000000000000000000000000000000000000000000000000000000000000";

        Assert.Throws<InvalidDataException>(() => EmbeddedMedia.Read(path, 72 * 18 * 4, wrongDigest, compressed: true));
    }

    [Fact]
    public void Read_RejectsAWrongDeclaredLength()
    {
        const string path = "Login/Server/Status.rgba.br";
        const string digest = "5FA3C29BE37231F2CB83C54440C737C45BEFFB10694687D40680224BE3A5C7E2";

        Assert.Throws<InvalidDataException>(() => EmbeddedMedia.Read(path, 72 * 18 * 4 + 1, digest, compressed: true));
        Assert.Throws<InvalidDataException>(() => EmbeddedMedia.Read(path, 72 * 18 * 4 - 1, digest, compressed: true));
    }

    [Fact]
    public void Read_RejectsAMissingResource()
    {
        Assert.Throws<InvalidDataException>(() => EmbeddedMedia.Read("Login/Missing.rgba.br", 4, new string('0', 64), compressed: true));
    }
}
