namespace OpenConquer.Rendering.World;

public readonly record struct WorldSceneRole(uint EntityId, int TileX, int TileY, byte Direction);
/// <summary>A remote role's interpolated world-pixel anchor and facing for the current frame.</summary>
public readonly record struct WorldSceneRolePosition(uint EntityId, int WorldX, int WorldY, byte Direction);
internal readonly record struct WorldRoleDrawSlot(int SpriteIndex, WorldSceneRole Role);
