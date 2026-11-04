using System.Numerics;
using OpenConquer.Content;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client.World;

internal sealed class WorldRoleRenderer : IDisposable
{
    private readonly WorldRolePart _body;
    private readonly WorldRolePart? _armet;
    private readonly C3Motion? _armetAttachment;
    private readonly WorldRolePart[] _rightWeapon;
    private readonly WorldRolePart[] _leftWeapon;
    private readonly C3Motion? _rightWeaponAttachment;
    private readonly C3Motion? _leftWeaponAttachment;
    private readonly WorldRoleFrameClock _clock = new();

    private WorldRoleRenderer(WorldRolePart body, WorldRolePart? armet, C3Motion? armetAttachment,
        WorldRolePart[] rightWeapon, C3Motion? rightWeaponAttachment,
        WorldRolePart[] leftWeapon, C3Motion? leftWeaponAttachment)
    {
        _body = body;
        _armet = armet;
        _armetAttachment = armetAttachment;
        _rightWeapon = rightWeapon;
        _leftWeapon = leftWeapon;
        _rightWeaponAttachment = rightWeaponAttachment;
        _leftWeaponAttachment = leftWeaponAttachment;
    }

    internal static ulong InitialBodyMotionKey(uint packedAppearance) =>
        (packedAppearance % 1000UL) * 1_000_000UL + 100UL;

    internal static WorldRoleRenderer? Load(IClientContentSource source,
        OpenGLGraphicsDevice device, uint packedAppearance, WorldRoleSources sources)
    {
        if (!source.TryOpenRead("ini/c3.wdb", ContentLookupMode.LooseOnly, out Stream? probe))
        {
            return null;
        }

        probe.Dispose();
        C3RoleCatalog catalog = C3RoleCatalog.Load(source);
        C3ResolvedPart bodySource = ResolveSinglePart(catalog, "body", sources.BodyModelId);
        string? motionPath = catalog.TryResolveMotionPath("body", InitialBodyMotionKey(packedAppearance),
            out string resolvedMotion) ? resolvedMotion : null;

        WorldRolePart? body = null;
        WorldRolePart? armet = null;
        WorldRolePart[] rightWeapon = [];
        WorldRolePart[] leftWeapon = [];
        try
        {
            body = WorldRolePart.Load(source, device, catalog, bodySource, motionPath);
            C3Motion? attachment = null;
            if (sources.ArmetModelId != 0)
            {
                if (catalog.TryResolve("armet", sources.ArmetModelId, out IReadOnlyList<C3ResolvedPart> parts))
                {
                    if (parts.Count != 1 || !body.TryGetAttachment("v_armet", out C3Motion linkedMotion))
                    {
                        throw new InvalidDataException("The character headwear has an unsupported attachment layout.");
                    }

                    attachment = linkedMotion;
                    armet = WorldRolePart.Load(source, device, catalog, parts[0]);
                }
                else
                {
                    Console.Error.WriteLine($"OpenConquer: character headwear model {sources.ArmetModelId} is not present in the C3 catalog.");
                }
            }

            (WorldRolePart[] loadedRight, C3Motion? rightAttachment) = LoadWeapon(
                source, device, catalog, body, "r_weapon", "v_r_weapon", sources.RightWeaponModelId);
            rightWeapon = loadedRight;
            (WorldRolePart[] loadedLeft, C3Motion? leftAttachment) = LoadWeapon(
                source, device, catalog, body, "l_weapon", "v_l_weapon", sources.LeftWeaponModelId);
            leftWeapon = loadedLeft;
            return new WorldRoleRenderer(body, armet, attachment,
                rightWeapon, rightAttachment, leftWeapon, leftAttachment);
        }
        catch
        {
            DisposeParts(leftWeapon);
            DisposeParts(rightWeapon);
            armet?.Dispose();
            body?.Dispose();
            throw;
        }
    }

    private static (WorldRolePart[] Parts, C3Motion? Attachment) LoadWeapon(
        IClientContentSource source, OpenGLGraphicsDevice device, C3RoleCatalog catalog,
        WorldRolePart body, string name, string track, uint modelId)
    {
        if (modelId == 0)
        {
            return ([], null);
        }

        if (!catalog.TryResolve(name, modelId, out IReadOnlyList<C3ResolvedPart> sources))
        {
            Console.Error.WriteLine($"OpenConquer: character {name} model {modelId} is not present in the C3 catalog.");
            return ([], null);
        }

        if (sources.Count == 0 || !body.TryGetAttachment(track, out C3Motion attachment))
        {
            throw new InvalidDataException($"The character {name} has an unsupported attachment layout.");
        }

        List<WorldRolePart> parts = new(sources.Count);
        try
        {
            foreach (C3ResolvedPart part in sources)
            {
                parts.Add(WorldRolePart.Load(source, device, catalog, part));
            }

            return ([.. parts], attachment);
        }
        catch
        {
            foreach (WorldRolePart part in parts)
            {
                part.Dispose();
            }

            throw;
        }
    }

    private static C3ResolvedPart ResolveSinglePart(C3RoleCatalog catalog, string name, uint modelId)
    {
        if (!catalog.TryResolve(name, modelId, out IReadOnlyList<C3ResolvedPart> parts) || parts.Count != 1)
        {
            throw new InvalidDataException($"The character {name} model {modelId} is unavailable in the 3D resource catalog.");
        }

        return parts[0];
    }

    internal void Draw(OpenGLRenderer renderer, Matrix4x4 main, Matrix4x4 shadow, uint nowMilliseconds)
    {
        int frame = _clock.GetFrame(nowMilliseconds, _body.FrameCount);
        _body.Draw(renderer, main, frame);
        _body.DrawShadow(renderer, shadow);

        if (_armet is { } armet)
        {
            if (!C3MotionSampler.TrySampleBone(_armetAttachment, 0, frame, out Matrix4x4 linked))
            {
                throw new InvalidDataException("The headwear attachment motion cannot be sampled.");
            }

            armet.Draw(renderer, linked * main, frame);
            armet.DrawShadow(renderer, linked * shadow);
        }

        DrawWeapon(renderer, main, shadow, frame, _rightWeapon, _rightWeaponAttachment);
        DrawWeapon(renderer, main, shadow, frame, _leftWeapon, _leftWeaponAttachment);
    }

    private static void DrawWeapon(OpenGLRenderer renderer, Matrix4x4 main, Matrix4x4 shadow,
        int frame, WorldRolePart[] parts, C3Motion? attachment)
    {
        if (parts.Length == 0)
        {
            return;
        }

        if (!C3MotionSampler.TrySampleBone(attachment, 0, frame, out Matrix4x4 linked))
        {
            throw new InvalidDataException("The character weapon attachment motion cannot be sampled.");
        }

        foreach (WorldRolePart part in parts)
        {
            part.Draw(renderer, linked * main, frame);
            part.DrawShadow(renderer, linked * shadow);
        }
    }

    private static void DisposeParts(WorldRolePart[] parts)
    {
        foreach (WorldRolePart part in parts)
        {
            part.Dispose();
        }
    }

    public void Dispose()
    {
        DisposeParts(_leftWeapon);
        DisposeParts(_rightWeapon);
        _armet?.Dispose();
        _body.Dispose();
    }
}
