using System.Runtime.ExceptionServices;

namespace OpenConquer.Launcher;

/// <summary>Collects failures for a single owner while each shutdown stage runs.</summary>
internal sealed class LauncherFailures
{
    private readonly List<Exception> _failures = [];

    public Exception? Failure => _failures.Count switch
    {
        0 => null,
        1 => _failures[0],
        _ => new AggregateException("Launcher shutdown encountered multiple failures.", _failures)
    };

    public void Add(Exception failure)
    {
        if (failure is AggregateException { InnerExceptions.Count: > 0 } aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                Add(inner);
            }
        }
        else if (!_failures.Any(item => ReferenceEquals(item, failure)))
        {
            _failures.Add(failure);
        }
    }

    public void Run(Action stage)
    {
        try
        {
            stage();
        }
        catch (Exception failure)
        {
            Add(failure);
        }
    }

    public async Task RunAsync(Func<Task> stage)
    {
        try
        {
            await stage();
        }
        catch (Exception failure)
        {
            Add(failure);
        }
    }

    public void ThrowIfFailed()
    {
        if (Failure is { } failure)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }
}
