namespace OpenConquer.Client;

internal static class ClientApplication
{
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using ClientWindow window = new();
        window.Run();

        return 0;
    }
}
