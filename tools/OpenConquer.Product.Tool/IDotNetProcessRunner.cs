namespace OpenConquer.Product.Tool;

internal interface IDotNetProcessRunner
{
    void Run(string workingDirectory, IReadOnlyList<string> arguments);
}
