using OpenConquer.Platform;

namespace OpenConquer.Client;

internal static class ClientApplication
{
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using DesktopWindow window = new();
        window.Run();

        return 0;
    }
}
