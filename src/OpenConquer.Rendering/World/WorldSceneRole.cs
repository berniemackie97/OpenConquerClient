namespace OpenConquer.Rendering.World;

public readonly record struct WorldSceneRole(uint EntityId, int TileX, int TileY, byte Direction);
internal readonly record struct WorldRoleDrawSlot(int SpriteIndex, WorldSceneRole Role);
