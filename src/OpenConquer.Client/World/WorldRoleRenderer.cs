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
    private readonly WorldRoleFrameClock _clock = new();

    private WorldRoleRenderer(WorldRolePart body, WorldRolePart? armet, C3Motion? armetAttachment)
    {
        _body = body;
        _armet = armet;
        _armetAttachment = armetAttachment;
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

            return new WorldRoleRenderer(body, armet, attachment);
        }
        catch
        {
            armet?.Dispose();
            body?.Dispose();
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
    }

    public void Dispose()
    {
        _armet?.Dispose();
        _body.Dispose();
    }
}
