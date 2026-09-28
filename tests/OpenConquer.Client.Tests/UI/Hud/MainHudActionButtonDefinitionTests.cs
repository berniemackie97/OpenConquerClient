using OpenConquer.Client.UI.Hud;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonDefinitionTests
{
    private static readonly (MainHudActionButtonId Id, string Section, int X, int Y, int Width, int Height, int Frames)[] s_expected =
    [
        (MainHudActionButtonId.Button40, "Button40", 502, 94, 46, 22, 2),
        (MainHudActionButtonId.Button410, "Button410", 702, 119, 46, 22, 2),
        (MainHudActionButtonId.Button42, "Button42", 552, 94, 46, 22, 2),
        (MainHudActionButtonId.Button43, "Button43", 652, 119, 46, 22, 2),
        (MainHudActionButtonId.Main3MissionBtn, "Main3_MissionBtn", 502, 119, 46, 22, 3),
        (MainHudActionButtonId.Button45, "Button45", 552, 119, 46, 22, 2),
        (MainHudActionButtonId.Button46, "Button46", 602, 119, 46, 22, 2),
        (MainHudActionButtonId.Button47, "Button47", 652, 94, 46, 22, 2),
        (MainHudActionButtonId.Main3OrganiseBtn, "Main3_OrganiseBtn", 702, 94, 46, 22, 4),
        (MainHudActionButtonId.Button41, "Button41", 602, 94, 46, 22, 3),
    ];

    [Fact]
    public void NativeDrawOrder_MatchesVerified5517ControlDefinitions()
    {
        ReadOnlySpan<MainHudActionButtonDefinition> actual = MainHudActionButtonDefinitions.NativeDrawOrder;

        Assert.Equal(s_expected.Length, actual.Length);

        for (int index = 0; index < s_expected.Length; index++)
        {
            var expected = s_expected[index];
            MainHudActionButtonDefinition definition = actual[index];

            Assert.Equal(expected.Id, definition.Id);
            Assert.Equal((int)expected.Id, definition.ControlId);
            Assert.Equal(expected.Section, definition.AniSectionName);
            Assert.Equal(expected.X, definition.LocalX);
            Assert.Equal(expected.Y, definition.LocalY);
            Assert.Equal(expected.Width, definition.HitWidth);
            Assert.Equal(expected.Height, definition.HitHeight);
            Assert.Equal(expected.Frames, definition.ExpectedFrameCount);
            Assert.Equal(definition, MainHudActionButtonDefinitions.Get(expected.Id));
        }
    }

    [Fact]
    public void Definitions_UseUniqueNativeControlIds()
    {
        MainHudActionButtonId[] ids = MainHudActionButtonDefinitions.NativeDrawOrder.ToArray().Select(static definition => definition.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void Get_RejectsUnknownNativeControlId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudActionButtonDefinitions.Get((MainHudActionButtonId)0x7FFF));
    }
}
