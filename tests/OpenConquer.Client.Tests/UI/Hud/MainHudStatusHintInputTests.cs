using System.Buffers.Binary;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Content.Regions;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudStatusHintInputTests
{
    private static readonly LogicalRenderSize s_800x600 = new(800, 600);
    private static readonly LogicalRenderSize s_1024x768 = new(1024, 768);

    [Theory]
    [InlineData(800, 600, 459)]
    [InlineData(1024, 768, 627)]
    public void PointerMove_UsesMainDialogLocalCoordinates(int width, int height, int originY)
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = CreateInput(state, new LogicalRenderSize(width, height));

        input.HandlePointerMoved(0, originY + 23);

        Assert.Equal(MainHudStatusHintKind.WalkRun, state.Kind);
        Assert.Equal(1, state.HoveredHotspot);
        Assert.True(state.IsVisible);
    }

    [Fact]
    public void PointerMove_UsesRegionOrderSkillManaLife()
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = CreateInput(state, s_800x600);

        input.HandlePointerMoved(20, 459 + 60);

        Assert.Equal(MainHudStatusHintKind.Life, state.Kind);

        input.HandlePointerMoved(-1, -1);
        input.HandlePointerMoved(60, 459 + 60);

        Assert.Equal(MainHudStatusHintKind.Mana, state.Kind);

        input.HandlePointerMoved(-1, -1);
        input.HandlePointerMoved(5, 459 + 100);

        Assert.Equal(MainHudStatusHintKind.Skill, state.Kind);
    }

    [Fact]
    public void PointerMove_UsesRegionsAfterRectanglesOnInitialEntry()
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = new(
            state,
            MainHudStatusHintLayout.Create(s_800x600),
            CreateRegion(new ClientRegionRectangle(0, 23, 22, 45)),
            CreateRegion(new ClientRegionRectangle(50, 58, 85, 100)),
            CreateRegion(new ClientRegionRectangle(10, 58, 45, 100)),
            alternateLayout: false);

        input.HandlePointerMoved(10, 459 + 30);

        Assert.Equal(MainHudStatusHintKind.Skill, state.Kind);
        Assert.Equal(6, state.HoveredHotspot);
    }

    [Fact]
    public void PointerMove_RetestsOnlyActiveHotspotAndRequiresAnotherMoveToEnterNext()
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = CreateInput(state, s_800x600);

        input.HandlePointerMoved(0, 459 + 23);
        Assert.Equal(MainHudStatusHintKind.WalkRun, state.Kind);

        input.HandlePointerMoved(73, 459 + 23);
        Assert.False(state.IsVisible);
        Assert.Equal(MainHudStatusHintState.NoHoveredHotspot, state.HoveredHotspot);

        input.HandlePointerMoved(73, 459 + 23);
        Assert.Equal(MainHudStatusHintKind.Map, state.Kind);
    }

    [Fact]
    public void PointerMove_UsesMirroredXOnlyForRegions()
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = CreateInput(state, s_800x600, alternateLayout: true);

        input.HandlePointerMoved(780, 459 + 60);

        Assert.Equal(MainHudStatusHintKind.Life, state.Kind);

        input.HandlePointerMoved(-1, -1);
        input.HandlePointerMoved(0, 459 + 23);

        Assert.Equal(MainHudStatusHintKind.WalkRun, state.Kind);
    }

    [Fact]
    public void PointerMove_ZeroRectangleCannotSelectHiddenChatSlot()
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = CreateInput(state, s_1024x768);

        input.HandlePointerMoved(70, 627 + 32);

        Assert.NotEqual(MainHudStatusHintKind.Chat, state.Kind);
    }

    [Fact]
    public void PointerMove_ExitingRegionClearsNativeHintState()
    {
        MainHudStatusHintState state = new();
        MainHudStatusHintInput input = CreateInput(state, s_800x600);

        input.HandlePointerMoved(20, 459 + 60);
        Assert.Equal(MainHudStatusHintKind.Life, state.Kind);

        input.HandlePointerMoved(300, 459 + 70);

        Assert.False(state.IsVisible);
        Assert.Equal(MainHudStatusHintKind.None, state.Kind);
        Assert.Equal(MainHudStatusHintState.NoHoveredHotspot, state.HoveredHotspot);
    }

    private static MainHudStatusHintInput CreateInput(MainHudStatusHintState state, LogicalRenderSize size, bool alternateLayout = false)
    {
        return new MainHudStatusHintInput(
            state,
            MainHudStatusHintLayout.Create(size),
            CreateRegion(new ClientRegionRectangle(0, 90, 92, 141)),
            CreateRegion(new ClientRegionRectangle(47, 58, 84, 90)),
            CreateRegion(new ClientRegionRectangle(10, 58, 47, 90)),
            alternateLayout);
    }

    private static ClientRegionFile CreateRegion(ClientRegionRectangle rectangle)
    {
        byte[] encoded = new byte[sizeof(uint) + 32 + 16];
        Span<byte> payload = encoded.AsSpan(sizeof(uint));

        BinaryPrimitives.WriteUInt32LittleEndian(encoded, 48);
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[4..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[8..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[12..], 16);

        for (int index = 0; index < 2; index++)
        {
            int offset = 16 + index * 16;
            BinaryPrimitives.WriteInt32LittleEndian(payload[offset..], rectangle.Left);
            BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], rectangle.Top);
            BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 8)..], rectangle.Right);
            BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 12)..], rectangle.Bottom);
        }

        return ClientRegionFile.Parse(encoded);
    }
}
