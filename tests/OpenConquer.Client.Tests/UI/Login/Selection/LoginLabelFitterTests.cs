using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login.Selection;

public sealed class LoginLabelFitterTests
{
    [Fact]
    public void Candidates_PreserveWholeTextElementsAndExposeTruncation()
    {
        string name = "A\u0301\U0001F600B";

        string[] candidates = LoginLabelFitter.Candidates(name).ToArray();

        Assert.Equal(new[] { name, "A\u0301\U0001F600...", "A\u0301...", "..." }, candidates);
    }

    [Fact]
    public void CoverageGate_RejectsTextElementsThatNeedMultiScalarShaping()
    {
        Assert.True(LoginLabelFitter.HasMultiScalarTextElements("A\u0301"));
        Assert.True(LoginLabelFitter.HasMultiScalarTextElements("\U0001F1FA\U0001F1F8"));
        Assert.False(LoginLabelFitter.HasMultiScalarTextElements("A\U0001F600\u6F22"));
    }

    [Fact]
    public void Candidates_ProduceBoundedSequenceForCatalogMaximum()
    {
        string name = new('A', 48);

        string[] candidates = LoginLabelFitter.Candidates(name).ToArray();

        Assert.Equal(49, candidates.Length);
        Assert.Equal(name, candidates[0]);
        Assert.Equal("...", candidates[^1]);
        Assert.All(candidates, candidate => Assert.DoesNotContain('\uFFFD', candidate));
    }

    [Fact]
    public void Center_PreservesActualNegativeBearingsInsideReferenceCell()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));
        LoginRect cell = layout.RegionTiles[0];

        bool fitted = LoginLabelFitter.TryCenter(cell, 8, inkLeft: -2, inkTop: 4, inkRight: 72, inkBottom: 16, out int x, out int y);

        Assert.True(fitted);
        Assert.Equal(99, x);
        Assert.Equal(191, y);
        Assert.InRange(x - 2, cell.X + 8, cell.X + cell.Width - 8);
        Assert.InRange(x + 72, cell.X + 8, cell.X + cell.Width - 8);
        Assert.InRange(y + 4, cell.Y, cell.Y + cell.Height);
        Assert.InRange(y + 16, cell.Y, cell.Y + cell.Height);
    }

    [Fact]
    public void Center_FitsCompactStageWithinPadding()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(800, 600));
        LoginRect cell = layout.ServerHitAreas[0];

        bool fitted = LoginLabelFitter.TryCenter(cell, 6, 1, 2, 101, 13, out int x, out int y);

        Assert.True(fitted);
        Assert.True(x + 1 >= cell.X + 6);
        Assert.True(x + 101 <= cell.X + cell.Width - 6);
        Assert.True(y + 2 >= cell.Y);
        Assert.True(y + 13 <= cell.Y + cell.Height);
    }

    [Theory]
    [InlineData(8, 0, 0, 200, 12)]
    [InlineData(8, 0, 0, 12, 100)]
    [InlineData(8, 0, 0, 0, 12)]
    [InlineData(8, 0, 0, 12, 0)]
    public void Center_RejectsNonfittingOrEmptyInk(int padding, int left, int top, int right, int bottom)
    {
        LoginRect cell = new(55, 194, 154, 50);

        Assert.False(LoginLabelFitter.TryCenter(cell, padding, left, top, right, bottom, out _, out _));
    }

    [Fact]
    public void Center_RejectsNegativePadding()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginLabelFitter.TryCenter(new LoginRect(0, 0, 154, 50), -1, 0, 0, 5, 5, out _, out _));
    }
}
