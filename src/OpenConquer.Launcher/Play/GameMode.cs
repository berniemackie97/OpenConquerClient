namespace OpenConquer.Launcher.Play;

/// <summary>The start modes of <c>play.exe</c>, valued as its <c>ini/GameSetup.ini [GameMode] GameModeRecord</c>.</summary>
internal enum GameMode
{
    /// <summary>"Standard Definition"; <c>NormalTimes</c>.</summary>
    Standard = 0,
    /// <summary>"High Definition"; <c>HighTimes</c>.</summary>
    High = 1,
    /// <summary>"Low Definition"; <c>LowTimes</c>. The only mode the game client distinguishes.</summary>
    Low = 2,
}

/// <summary>How often each start mode has been used: the <c>[GameMode]</c> counters <c>play.exe</c> keeps.</summary>
internal readonly record struct GameModeUsage(int HighTimes, int StandardTimes, int LowTimes)
{
    /// <summary>
    /// The mode shown in its normal art, as <c>CPlayDlg_OnInitDialog</c> picks it: the larger of High and Standard
    /// unless Low is at least as large, then the first of Standard, High, Low with that count.
    /// </summary>
    public GameMode MostUsed
    {
        get
        {
            int highest = Math.Max(HighTimes, StandardTimes) > LowTimes ? Math.Max(HighTimes, StandardTimes) : LowTimes;
            return highest == StandardTimes ? GameMode.Standard : highest == HighTimes ? GameMode.High : GameMode.Low;
        }
    }

    /// <summary>Low Definition asks for confirmation only until it has been used once.</summary>
    public bool LowNeedsConfirmation => LowTimes == 0;

    public GameModeUsage Record(GameMode mode) => mode switch
    {
        GameMode.High => this with { HighTimes = Increment(HighTimes) },
        GameMode.Standard => this with { StandardTimes = Increment(StandardTimes) },
        GameMode.Low => this with { LowTimes = Increment(LowTimes) },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown start mode."),
    };

    private static int Increment(int times) => times == int.MaxValue ? times : times + 1;
}
