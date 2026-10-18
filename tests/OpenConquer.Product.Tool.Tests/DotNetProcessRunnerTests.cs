namespace OpenConquer.Product.Tool.Tests;

public sealed class DotNetProcessRunnerTests
{
    [Fact]
    public void RunAcceptsSuccessfulDotNetCommand()
    {
        using TemporaryDirectory temporary = new();

        DotNetProcessRunner runner = new();

        runner.Run(temporary.RootPath, ["--version"]);
    }

    [Fact]
    public void RunRejectsFailedDotNetCommand()
    {
        using TemporaryDirectory temporary = new();

        DotNetProcessRunner runner = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => runner.Run(temporary.RootPath, ["definitely-not-an-openconquer-dotnet-command"]));

        Assert.Contains("exit code", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunRejectsEmptyArguments()
    {
        using TemporaryDirectory temporary = new();

        DotNetProcessRunner runner = new();

        Assert.Throws<ArgumentException>(() => runner.Run(temporary.RootPath, []));
    }

    [Fact]
    public void RunRejectsMissingWorkingDirectory()
    {
        using TemporaryDirectory temporary = new();

        DotNetProcessRunner runner = new();

        string missingPath = Path.Combine(temporary.RootPath, "missing");

        Assert.Throws<DirectoryNotFoundException>(() => runner.Run(missingPath, ["--version"]));
    }

    [Fact]
    public void RunRejectsNullArgument()
    {
        using TemporaryDirectory temporary = new();

        DotNetProcessRunner runner = new();

        string?[] arguments = ["--version", null];

        Assert.Throws<ArgumentException>(() => runner.Run(temporary.RootPath, arguments!));
    }
}
