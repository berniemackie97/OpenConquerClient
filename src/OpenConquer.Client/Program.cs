namespace OpenConquer.Client;

internal static class Program
{
    private const int InvalidStartupArgumentsExitCode = 2;

    private static int Main(string[] args)
    {
        if (!ClientStartupOptions.TryParse(args, out ClientStartupOptions? options, out string? errorMessage))
        {
            Console.Error.WriteLine($"OpenConquer: {errorMessage}");
            Console.Error.WriteLine("Usage: OpenConquer.Client [--content-root <path>]");

            return InvalidStartupArgumentsExitCode;
        }

        using ClientApplication application = new(options.ContentRootPath);

        return application.Run();
    }
}
