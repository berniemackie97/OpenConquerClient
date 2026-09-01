namespace OpenConquer.Client;

internal static class Program
{
    private static int Main()
    {
        using ClientApplication application = new();

        return application.Run();
    }
}
