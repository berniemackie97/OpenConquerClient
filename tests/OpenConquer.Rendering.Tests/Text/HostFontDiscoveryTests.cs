using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class HostFontDiscoveryTests
{
    [Fact]
    public void Constructor_PreservesFontReferencesAndDefaultGuiFont()
    {
        HostFontReference first = new("/fonts/first.ttf", faceIndex: 0);
        HostFontReference second = new("/fonts/second.ttc", faceIndex: 2);
        HostFontReference defaultGuiFont = new("/fonts/default.ttf", faceIndex: 1);

        HostFontDiscovery discovery = new([first, second], defaultGuiFont);

        Assert.Equal(2, discovery.FontReferences.Count);
        Assert.Same(first, discovery.FontReferences[0]);
        Assert.Same(second, discovery.FontReferences[1]);
        Assert.Same(defaultGuiFont, discovery.DefaultGuiFont);
    }

    [Fact]
    public void Constructor_WithoutDefaultGuiFont_PreservesNull()
    {
        HostFontDiscovery discovery = new([]);

        Assert.Empty(discovery.FontReferences);
        Assert.Null(discovery.DefaultGuiFont);
    }

    [Fact]
    public void Constructor_CopiesInputSequence()
    {
        HostFontReference first = new("/fonts/first.ttf");
        HostFontReference second = new("/fonts/second.ttf");
        List<HostFontReference> references = [first];

        HostFontDiscovery discovery = new(references);
        references.Add(second);

        Assert.Single(discovery.FontReferences);
        Assert.Same(first, discovery.FontReferences[0]);
    }

    [Fact]
    public void Constructor_PreservesReferenceOrder()
    {
        HostFontReference first = new("/fonts/z.ttf");
        HostFontReference second = new("/fonts/a.ttf");
        HostFontReference third = new("/fonts/m.ttf");

        HostFontDiscovery discovery = new([first, second, third]);

        Assert.Collection(
            discovery.FontReferences,
            reference => Assert.Same(first, reference),
            reference => Assert.Same(second, reference),
            reference => Assert.Same(third, reference));
    }

    [Fact]
    public void Constructor_DuplicateReferencesArePreserved()
    {
        HostFontReference reference = new("/fonts/example.ttf");

        HostFontDiscovery discovery = new([reference, reference]);

        Assert.Equal(2, discovery.FontReferences.Count);
        Assert.Same(reference, discovery.FontReferences[0]);
        Assert.Same(reference, discovery.FontReferences[1]);
    }

    [Fact]
    public void Constructor_NullFontReferences_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HostFontDiscovery(null!));
    }

    [Fact]
    public void Constructor_NullReference_Throws()
    {
        Assert.Throws<ArgumentException>(() => new HostFontDiscovery([new HostFontReference("/fonts/example.ttf"), null!]));
    }
}
