using OpenConquer.Client.Diagnostics;
using OpenConquer.Content;
using OpenConquer.Content.Mesh;

namespace OpenConquer.Client.World;

internal sealed class WorldRoleAssetReader(IClientContentSource source, ClientDiagnostics diagnostics)
{
    private readonly Lock _sync = new();
    private C3RoleCatalog? _catalog;

    internal WorldRoleAssets? Load(uint packedAppearance, WorldRoleSources sources, CancellationToken token)
    {
        lock (_sync)
        {
            token.ThrowIfCancellationRequested();
            if (_catalog is null)
            {
                if (!source.TryOpenRead("ini/c3.wdb", ContentLookupMode.LooseOnly, out Stream? probe))
                {
                    return null;
                }

                probe.Dispose();
                _catalog = C3RoleCatalog.Load(source);
            }

            return WorldRoleAssets.Load(source, _catalog, packedAppearance, sources, diagnostics, token);
        }
    }
}
