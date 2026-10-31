using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Tests.World;

public sealed class WorldMapPuzzleValidatorTests
{
    [Fact]
    public void RejectsMissingCatalog()
    {
        using TemporarySourceTree root = new();
        Assert.Throws<FileNotFoundException>(() => WorldMapPuzzleValidator.Validate(root.RootPath, 1010));
    }
}
