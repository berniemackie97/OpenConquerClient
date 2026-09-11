using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FreeTypeLibraryTests
{
    [Fact]
    public void Constructor_InitializesFreeTypeLibrary()
    {
        using FreeTypeLibrary library = new();
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        FreeTypeLibrary library = new();

        library.Dispose();
        library.Dispose();
    }
}
