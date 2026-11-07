using OpenConquer.Content;
using OpenConquer.Content.Items;
using OpenConquer.Content.Map;
using OpenConquer.Content.Progression;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.World;

internal sealed record WorldEntryCatalogs(ItemStatCatalog Items, LevelExperienceTable Experience);

/// <summary>Reads and decodes the CPU content required by an admitted world.</summary>
internal static class WorldEntryContent
{
    internal static WorldEntryCatalogs LoadCatalogs(IClientContentSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ItemStatCatalog items = ItemStatCatalog.Load(source);
        cancellationToken.ThrowIfCancellationRequested();
        LevelExperienceTable experience = LevelExperienceTable.Load(source);
        return new WorldEntryCatalogs(items, experience);
    }

    internal static WorldMapScene LoadMap(IClientContentSource source, GameWorldEntrySnapshot entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GameMapCatalog catalog = GameMapCatalog.Load(source);
        cancellationToken.ThrowIfCancellationRequested();
        WorldMapScene scene = WorldMapScene.Load(source, catalog, entry.MapDataId, entry.X, entry.Y,
            unchecked((uint)Environment.TickCount), cancellationToken);
        scene.SetHeroDirection(entry.DirectionIndex);
        return scene;
    }
}
