namespace OpenConquer.Rendering.Text;

/// <summary>
/// Discovers font resources registered with the current host font system.
/// </summary>
internal interface IHostFontSource
{
    /// <summary>
    /// Discovers the host's registered font resources and preferred GUI font family.
    /// </summary>
    HostFontDiscovery Discover();
}
