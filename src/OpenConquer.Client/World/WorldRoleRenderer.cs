using System.Numerics;
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

    internal static WorldRoleRenderer Create(OpenGLGraphicsDevice device, WorldRoleAssets assets)
    {
        WorldRolePart? body = null;
        WorldRolePart? armet = null;
        WorldRolePart[] rightWeapon = [];
        WorldRolePart[] leftWeapon = [];
        try
        {
            body = WorldRolePart.Create(device, assets.Body);
            if (assets.Armet is { } headwear)
            {
                armet = WorldRolePart.Create(device, headwear);
            }

            rightWeapon = CreateParts(device, assets.RightWeapon);
            leftWeapon = CreateParts(device, assets.LeftWeapon);
            return new WorldRoleRenderer(body, armet, assets.ArmetAttachment,
                rightWeapon, assets.RightWeaponAttachment, leftWeapon, assets.LeftWeaponAttachment);
        }
        catch (Exception failure)
        {
            ClientShutdown cleanup = new(failure);
            ReleaseParts(leftWeapon, rightWeapon, armet, body, cleanup);
            cleanup.ThrowIfFailed();
            throw;
        }
    }

    private static WorldRolePart[] CreateParts(OpenGLGraphicsDevice device, WorldRolePartAssets[] assets)
    {
        List<WorldRolePart> parts = new(assets.Length);
        try
        {
            foreach (WorldRolePartAssets part in assets)
            {
                parts.Add(WorldRolePart.Create(device, part));
            }

            return [.. parts];
        }
        catch (Exception failure)
        {
            ClientShutdown cleanup = new(failure);
            DisposeParts(parts, cleanup);
            cleanup.ThrowIfFailed();
            throw;
        }
    }

    private static void DisposeParts(IReadOnlyList<WorldRolePart> parts, ClientShutdown cleanup)
    {
        foreach (WorldRolePart part in parts)
        {
            cleanup.Run(part.Dispose);
        }
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

    public void Dispose()
    {
        ClientShutdown cleanup = new();
        ReleaseParts(_leftWeapon, _rightWeapon, _armet, _body, cleanup);
        cleanup.ThrowIfFailed();
    }

    private static void ReleaseParts(WorldRolePart[] left, WorldRolePart[] right, WorldRolePart? armet,
        WorldRolePart? body, ClientShutdown cleanup)
    {
        DisposeParts(left, cleanup);
        DisposeParts(right, cleanup);
        cleanup.Run(() => armet?.Dispose());
        cleanup.Run(() => body?.Dispose());
    }
}
