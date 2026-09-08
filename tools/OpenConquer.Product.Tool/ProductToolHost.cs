using System.Text.Json;

namespace OpenConquer.Product.Tool;

internal static class ProductToolHost
{
    private const int SuccessExitCode = 0;
    private const int InvalidArgumentsExitCode = 2;
    private const int OperationFailedExitCode = 1;

    public static int Run(string[] args)
    {
        if (!ProductToolCommandLine.TryParse(args, Environment.CurrentDirectory, out ProductToolOptions? options, out string? errorMessage))
        {
            Console.Error.WriteLine($"OpenConquer.Product.Tool: {errorMessage}");
            Console.Error.WriteLine(ProductToolCommandLine.Usage);
            return InvalidArgumentsExitCode;
        }

        try
        {
            switch (options)
            {
                case ReleaseManifestOptions manifestOptions:
                    ProductReleaseManifest.Create(manifestOptions);
                    Console.WriteLine($"Created release manifest at '{manifestOptions.OutputPath}'.");
                    break;
                case ReleaseSignatureOptions signatureOptions:
                    ProductReleaseSignature.Create(signatureOptions);
                    Console.WriteLine($"Created release signature envelope at '{signatureOptions.OutputPath}'.");
                    break;
                case ReleaseTrustOptions trustOptions:
                    ProductReleaseTrust.Create(trustOptions);
                    Console.WriteLine($"Created release trust at '{trustOptions.OutputPath}'.");
                    break;
                case LocalProductOptions localProductOptions:
                    LocalProductBuilder builder = new(new DotNetProcessRunner());
                    LocalProductBuildResult result = builder.Build(localProductOptions, DevelopmentPublisherIdentityPaths.CreateCurrent());
                    Console.WriteLine($"Created local OpenConquer product release '{result.ReleaseVersion}' ({result.TargetRuntime}, sequence {result.ReleaseSequence}) at '{result.ProductPath}'.");
                    break;
                case ProductStageOptions stageOptions:
                    ManagedProductStager.Stage(stageOptions);
                    Console.WriteLine($"Staged managed OpenConquer product at '{stageOptions.OutputRootPath}'.");
                    break;
                default:
                    throw new InvalidOperationException("The product tool received an unsupported operation.");
            }

            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or JsonException or NotSupportedException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"OpenConquer.Product.Tool: {exception.Message}");
            return OperationFailedExitCode;
        }
    }
}
