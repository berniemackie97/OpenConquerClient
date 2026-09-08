using System.ComponentModel;
using System.Diagnostics;

namespace OpenConquer.Product.Tool;

/// <summary>Runs .NET SDK commands used while composing a local development product.</summary>
internal sealed class DotNetProcessRunner : IDotNetProcessRunner
{
    public void Run(string workingDirectory, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        string normalizedWorkingDirectory = ProductStagingPathGuard.RequireDirectory(workingDirectory, nameof(workingDirectory));

        if (arguments.Count == 0)
        {
            throw new ArgumentException("A .NET SDK command is required.", nameof(arguments));
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = normalizedWorkingDirectory,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            if (argument is null)
            {
                throw new ArgumentException("A .NET SDK argument must not be null.", nameof(arguments));
            }

            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new()
        {
            StartInfo = startInfo,
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The .NET SDK process could not be started.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("The .NET SDK process could not be started. Ensure the .NET SDK is installed and 'dotnet' is available on PATH.", exception);
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"The .NET SDK command failed with exit code {process.ExitCode}.");
        }
    }
}
