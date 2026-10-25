using OpenConquer.Content.Flash;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Tests.Flash;

public sealed class RoleMoviesTests
{
    [Fact]
    public void Role_HasSixStageFramesAndItsNamedRootClips()
    {
        FlashMovie role = RoleMovies.LoadRole();

        Assert.Equal((30f, 1024, 768, 6), (role.FrameRate, role.Width, role.Height, role.Root.FrameCount));
        Dictionary<string, FlashPlacement> frame5 = role.Root.DisplayList(5).Where(p => p.Name is not null).ToDictionary(p => p.Name!);
        Assert.Equal(304, frame5["sb"].CharacterId);
        Assert.Equal(312, frame5["createBtn"].CharacterId);
        Assert.Equal((102, 110), (frame5["inGameMC"].Depth, frame5["inGameMC"].CharacterId));
        Assert.Equal(new FlashMatrix(1f, 0f, 0f, 1f, 508f, 376f), frame5["inGameMC"].Matrix);
        Assert.DoesNotContain(role.Root.DisplayList(2), p => p.Name == "returnBtn");
        Assert.Contains(role.Root.DisplayList(3), p => p.Name == "returnBtn" && p.PlacedFrame == 3);
    }

    [Fact]
    public void CharacterInAndOut_PlacesTheTwelvePortraitsInsideMcRole()
    {
        FlashMovie movie = RoleMovies.LoadCharacterInAndOut();

        Assert.Equal(144, movie.Root.FrameCount);
        Assert.Equal(133, movie.Root.Labels["OUT"]);
        FlashPlacement mcRole = Assert.Single(movie.Root.DisplayList(132), p => p.Name == "mcRole");
        FlashSpriteCharacter carousel = Assert.IsType<FlashSpriteCharacter>(movie.Character(mcRole.CharacterId));

        Dictionary<string, FlashPlacement> sons = carousel.Timeline.DisplayList(1).Where(p => p.Name?.StartsWith("son_", StringComparison.Ordinal) == true)
            .ToDictionary(p => p.Name!);
        Assert.Equal(12, sons.Count);
        Assert.Equal((91, -1675.55f), (sons["son_0"].CharacterId, sons["son_0"].Matrix.Tx));
        Assert.Equal((215, 1560.6f), (sons["son_11"].CharacterId, sons["son_11"].Matrix.Tx));
        Assert.Equal(193, sons["son_7"].Depth);
    }

    [Fact]
    public void MorphShapesHaveARasterForEveryRatioTheirTimelinesUse()
    {
        FlashMovie role = RoleMovies.LoadRole();

        FlashMorphCharacter morph = Assert.IsType<FlashMorphCharacter>(role.Character(135));
        Assert.True(morph.RasterByRatio.ContainsKey(0));
        Assert.All(morph.RasterByRatio.Values, raster => Assert.InRange(raster, 0, role.Rasters.Count - 1));
    }

    [Fact]
    public void EveryRasterDecodesToItsDeclaredSizeAndDigest()
    {
        foreach (FlashMovie movie in new[] { RoleMovies.LoadRole(), RoleMovies.LoadCharacterInAndOut() })
        {
            for (int index = 0; index < movie.Rasters.Count; index++)
            {
                RgbaImage image = movie.LoadRaster(index);
                Assert.Equal((movie.Rasters[index].Width, movie.Rasters[index].Height), (image.Width, image.Height));
            }
        }
    }

    [Fact]
    public void EditTextsKeepTheirDeviceFontAndColor()
    {
        FlashEditTextCharacter name = Assert.IsType<FlashEditTextCharacter>(RoleMovies.LoadRole().Character(21));

        Assert.Equal(("Arial", 18f, (byte)240, (byte)192, (byte)166), (name.Font, name.FontHeight, name.Red, name.Green, name.Blue));
        Assert.True(name.ReadOnly);
    }
}
