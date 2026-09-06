namespace OpenConquer.Product.Tool;

internal static class ProductToolHost
{
    private const int SuccessExitCode = 0;
    private const int InvalidArgumentsExitCode = 2;
    private const int OperationFailedExitCode = 1;

    public static int Run(string[] args)
    {
        if (
            !ProductToolCommandLine.TryParse(
                args,
                Environment.CurrentDirectory,
                out ProductStageOptions? options,
                out string? errorMessage
            )
        )
        {
            Console.Error.WriteLine($"OpenConquer.Product.Tool: {errorMessage}");
            Console.Error.WriteLine(ProductToolCommandLine.Usage);
            return InvalidArgumentsExitCode;
        }

        try
        {
            ProductStageOptions stageOptions = options;
            ManagedProductStager.Stage(stageOptions);
            Console.WriteLine(
                $"Staged managed OpenConquer product at '{stageOptions.OutputRootPath}'."
            );
            return SuccessExitCode;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            Console.Error.WriteLine($"OpenConquer.Product.Tool: {exception.Message}");
            return OperationFailedExitCode;
        }
    }
}
