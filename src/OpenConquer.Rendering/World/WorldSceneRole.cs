namespace OpenConquer.Rendering.World;

public readonly record struct WorldSceneRole(uint EntityId, int TileX, int TileY, byte Direction);
/// <summary>
/// A remote role's RoleView pose for the current frame: world-pixel anchor (already including any
/// screen-Y adjustment), rotation in degrees and the stored RoleView elevation.
/// </summary>
public readonly record struct WorldSceneRolePosition(uint EntityId, int WorldX, int WorldY, float RotationDegrees,
    float Elevation);
internal readonly record struct WorldRoleDrawSlot(int SpriteIndex, WorldSceneRole Role);
