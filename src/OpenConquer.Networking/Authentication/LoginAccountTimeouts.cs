namespace OpenConquer.Networking.Authentication;

/// <summary>Separate time limits for the AccountServer connect, seed and response steps.</summary>
internal readonly record struct LoginAccountTimeouts(TimeSpan Connect, TimeSpan Seed)
{
    internal static LoginAccountTimeouts Default { get; } = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

    internal TimeSpan Response { get; init; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        if (Connect <= TimeSpan.Zero || Connect > TimeSpan.FromSeconds(30) ||
            Seed <= TimeSpan.Zero || Seed > TimeSpan.FromSeconds(30) ||
            Response <= TimeSpan.Zero || Response > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(LoginAccountTimeouts),
                "AccountServer phase timeouts must be positive and no greater than 30 seconds.");
        }
    }
}
