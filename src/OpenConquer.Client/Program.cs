namespace OpenConquer.Client;

internal static class Program
{
    private const int InvalidStartupArgumentsExitCode = 2;

    private static int Main(string[] args)
    {
        if (
            !ClientStartupOptions.TryParse(
                args,
                out ClientStartupOptions? options,
                out string? errorMessage
            )
        )
        {
            Console.Error.WriteLine($"OpenConquer: {errorMessage}");
            Console.Error.WriteLine(
                $"Usage: OpenConquer.Client [--content-root <path>] "
                    + $"[--presentation <{ClientStartupOptions.PresentationPolicyNames}>]"
            );

            return InvalidStartupArgumentsExitCode;
        }

        ClientApplication application = new(options.ContentRootPath, options.PresentationPolicy);

        int exitCode;

        try
        {
            exitCode = application.Run();
        }
        catch
        {
            try
            {
                application.Dispose();
            }
            catch
            {
                // Preserve the application failure that initiated cleanup.
            }

            throw;
        }

        application.Dispose();

        return exitCode;
    }
}
