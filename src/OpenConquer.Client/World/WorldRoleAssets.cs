using OpenConquer.Client.Diagnostics;
using OpenConquer.Content;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Client.World;

/// <summary>The role's decoded parts and attachment tracks, prepared without graphics resources.</summary>
internal sealed record WorldRoleAssets(
    WorldRolePartAssets Body, WorldRolePartAssets? Armet, C3Motion? ArmetAttachment,
    WorldRolePartAssets[] RightWeapon, C3Motion? RightWeaponAttachment,
    WorldRolePartAssets[] LeftWeapon, C3Motion? LeftWeaponAttachment)
{
    private static readonly ushort[] s_groundActions =
    [
        WorldRolePlayback.GroundWalkFirst, WorldRolePlayback.GroundWalkSecond,
        WorldRolePlayback.GroundRunFirst, WorldRolePlayback.GroundRunSecond
    ];

    internal static ulong InitialBodyMotionKey(uint packedAppearance) =>
        BodyMotionKey(packedAppearance, 100);

    internal static ulong BodyMotionKey(uint packedAppearance, ushort action) =>
        (packedAppearance % 1000UL) * 1_000_000UL + action;

    internal static WorldRoleAssets Load(IClientContentSource source, C3RoleCatalog catalog, uint packedAppearance,
        WorldRoleSources sources, ClientDiagnostics diagnostics, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!catalog.TryResolve("body", sources.BodyModelId, out IReadOnlyList<C3ResolvedPart> bodies) || bodies.Count != 1)
        {
            throw new InvalidDataException($"The character body model {sources.BodyModelId} is unavailable in the 3D resource catalog.");
        }

        string? motionPath = catalog.TryResolveMotionPath("body", InitialBodyMotionKey(packedAppearance),
            out string resolvedMotion) ? resolvedMotion : null;
        Dictionary<ushort, string> groundMotions = [];
        foreach (ushort action in s_groundActions)
        {
            if (catalog.TryResolveMotionPath("body", BodyMotionKey(packedAppearance, action),
                out string path))
            {
                groundMotions.Add(action, path);
            }
        }

        WorldRolePartAssets body = WorldRolePartAssets.Load(source, catalog, bodies[0], token,
            motionPath, groundMotions);

        WorldRolePartAssets? armet = null;
        C3Motion? attachment = null;
        if (sources.ArmetModelId != 0)
        {
            if (catalog.TryResolve("armet", sources.ArmetModelId, out IReadOnlyList<C3ResolvedPart> parts))
            {
                if (parts.Count != 1 || !body.TryGetAttachment("v_armet", out attachment))
                {
                    throw new InvalidDataException("The character headwear has an unsupported attachment layout.");
                }

                armet = WorldRolePartAssets.Load(source, catalog, parts[0], token);
            }
            else
            {
                diagnostics.WorldRoleModelMissing(sources.ArmetModelId, equipmentSlot: 1);
            }
        }

        (WorldRolePartAssets[] right, C3Motion? rightAttachment) = LoadWeapon(
            source, catalog, body, "r_weapon", "v_r_weapon", sources.RightWeaponModelId, 4, diagnostics, token);
        (WorldRolePartAssets[] left, C3Motion? leftAttachment) = LoadWeapon(
            source, catalog, body, "l_weapon", "v_l_weapon", sources.LeftWeaponModelId, 5, diagnostics, token);
        return new WorldRoleAssets(body, armet, attachment, right, rightAttachment, left, leftAttachment);
    }

    private static (WorldRolePartAssets[] Parts, C3Motion? Attachment) LoadWeapon(
        IClientContentSource source, C3RoleCatalog catalog, WorldRolePartAssets body, string name, string track,
        uint modelId, int equipmentSlot, ClientDiagnostics diagnostics, CancellationToken token)
    {
        if (modelId == 0)
        {
            return ([], null);
        }

        token.ThrowIfCancellationRequested();
        if (!catalog.TryResolve(name, modelId, out IReadOnlyList<C3ResolvedPart> sources))
        {
            diagnostics.WorldRoleModelMissing(modelId, equipmentSlot);
            return ([], null);
        }

        if (sources.Count == 0 || !body.TryGetAttachment(track, out C3Motion? attachment))
        {
            throw new InvalidDataException($"The character {name} has an unsupported attachment layout.");
        }

        WorldRolePartAssets[] parts = new WorldRolePartAssets[sources.Count];
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = WorldRolePartAssets.Load(source, catalog, sources[i], token);
        }

        return (parts, attachment);
    }
}
