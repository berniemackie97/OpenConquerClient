using System.Reflection;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// The page shown in the centre panel, where <c>play.exe</c> showed <c>ini/Play.dat [Web] web1</c>. It is set at
/// build time (<c>OpenConquerLauncherHomePage</c>) and must be HTTPS. Navigation stays on its host and that host's
/// subdomains; every other link opens in the system browser instead of inside the launcher.
/// </summary>
internal sealed class LauncherHomePage
{
    private const string MetadataKey = "OpenConquerLauncherHomePage";

    private readonly string _host;
    private readonly string _siteDomain;

    public LauncherHomePage(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!IsHttps(address))
        {
            throw new ArgumentException("The launcher home page must be an absolute HTTPS address.", nameof(address));
        }

        Address = address;
        _host = address.IdnHost;
        _siteDomain = _host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? _host[4..] : _host;
    }

    public Uri Address
    {
        get;
    }

    public static LauncherHomePage FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string? configured = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == MetadataKey)?.Value;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out Uri? address))
        {
            throw new InvalidOperationException("The launcher was built without a valid home page address.");
        }

        return new LauncherHomePage(address);
    }

    /// <summary>Whether the panel may navigate to <paramref name="target"/> itself.</summary>
    public bool KeepsInPanel(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsHttps(target))
        {
            return false;
        }

        string host = target.IdnHost;
        return string.Equals(host, _host, StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, _siteDomain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + _siteDomain, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a link leaving the panel may be handed to the system browser.</summary>
    public static bool OpensExternally(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return IsHttps(target);
    }

    private static bool IsHttps(Uri address) =>
        address.IsAbsoluteUri && address.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(address.IdnHost)
        && string.IsNullOrEmpty(address.UserInfo);
}
