using OpenConquer.Content;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Client.World;

/// <summary>The role's decoded parts and attachment tracks, prepared without graphics resources.</summary>
internal sealed record WorldRoleAssets(
    WorldRolePartAssets Body, WorldRolePartAssets? Armet, C3Motion? ArmetAttachment,
    WorldRolePartAssets[] RightWeapon, C3Motion? RightWeaponAttachment,
    WorldRolePartAssets[] LeftWeapon, C3Motion? LeftWeaponAttachment)
{
    internal static ulong InitialBodyMotionKey(uint packedAppearance) =>
        (packedAppearance % 1000UL) * 1_000_000UL + 100UL;

    internal static WorldRoleAssets? Load(IClientContentSource source, uint packedAppearance,
        WorldRoleSources sources, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!source.TryOpenRead("ini/c3.wdb", ContentLookupMode.LooseOnly, out Stream? probe))
        {
            return null;
        }

        probe.Dispose();
        C3RoleCatalog catalog = C3RoleCatalog.Load(source);
        if (!catalog.TryResolve("body", sources.BodyModelId, out IReadOnlyList<C3ResolvedPart> bodies) || bodies.Count != 1)
        {
            throw new InvalidDataException($"The character body model {sources.BodyModelId} is unavailable in the 3D resource catalog.");
        }

        string? motionPath = catalog.TryResolveMotionPath("body", InitialBodyMotionKey(packedAppearance),
            out string resolvedMotion) ? resolvedMotion : null;
        WorldRolePartAssets body = WorldRolePartAssets.Load(source, catalog, bodies[0], token, motionPath);

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
                Console.Error.WriteLine($"OpenConquer: character headwear model {sources.ArmetModelId} is not present in the C3 catalog.");
            }
        }

        (WorldRolePartAssets[] right, C3Motion? rightAttachment) = LoadWeapon(
            source, catalog, body, "r_weapon", "v_r_weapon", sources.RightWeaponModelId, token);
        (WorldRolePartAssets[] left, C3Motion? leftAttachment) = LoadWeapon(
            source, catalog, body, "l_weapon", "v_l_weapon", sources.LeftWeaponModelId, token);
        return new WorldRoleAssets(body, armet, attachment, right, rightAttachment, left, leftAttachment);
    }

    private static (WorldRolePartAssets[] Parts, C3Motion? Attachment) LoadWeapon(
        IClientContentSource source, C3RoleCatalog catalog, WorldRolePartAssets body, string name, string track,
        uint modelId, CancellationToken token)
    {
        if (modelId == 0)
        {
            return ([], null);
        }

        token.ThrowIfCancellationRequested();
        if (!catalog.TryResolve(name, modelId, out IReadOnlyList<C3ResolvedPart> sources))
        {
            Console.Error.WriteLine($"OpenConquer: character {name} model {modelId} is not present in the C3 catalog.");
            return ([], null);
        }

        if (sources.Count == 0 || !body.TryGetAttachment(track, out C3Motion attachment))
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
