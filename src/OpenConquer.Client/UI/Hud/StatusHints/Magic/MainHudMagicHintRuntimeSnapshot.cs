namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal readonly record struct MainHudLearnedMagic(uint Type, uint Level, uint CurrentExperience);

internal sealed class MainHudEquippedMount(ReadOnlySpan<byte> encodedDisplayName, int lineageLevel)
{
    private readonly byte[] _displayName = encodedDisplayName.ToArray();

    public ReadOnlyMemory<byte> EncodedDisplayName => _displayName;

    public int LineageLevel { get; } = lineageLevel;
}

internal readonly record struct MainHudZeroMagicState(byte Mode, bool CanRevive, bool CanRestoreAppearance, bool CanDescend, bool CanTryOn, uint TryoutRemainingSeconds, ReadOnlyMemory<byte> TryoutItemName);

internal sealed class MainHudMagicHintRuntimeSnapshot
{
    private readonly MainHudLearnedMagic[] _learnedMagic;

    public MainHudMagicHintRuntimeSnapshot(IEnumerable<MainHudLearnedMagic> learnedMagic, uint characterLevel, int activeSubprofessionClass, int activeSubprofessionPhase, long packedSubprofessionPhases, MainHudEquippedMount? equippedMount, MainHudZeroMagicState zeroMagic)
    {
        ArgumentNullException.ThrowIfNull(learnedMagic);

        _learnedMagic = learnedMagic.ToArray();
        CharacterLevel = characterLevel;
        ActiveSubprofessionClass = activeSubprofessionClass;
        ActiveSubprofessionPhase = activeSubprofessionPhase;
        PackedSubprofessionPhases = packedSubprofessionPhases;
        EquippedMount = equippedMount;
        ZeroMagic = zeroMagic with
        {
            TryoutItemName = zeroMagic.TryoutItemName.ToArray()
        };
    }

    public uint CharacterLevel
    {
        get;
    }

    public int ActiveSubprofessionClass
    {
        get;
    }

    public int ActiveSubprofessionPhase
    {
        get;
    }

    public long PackedSubprofessionPhases
    {
        get;
    }

    public MainHudEquippedMount? EquippedMount
    {
        get;
    }

    public MainHudZeroMagicState ZeroMagic
    {
        get;
    }

    public bool TryFindLearnedMagic(uint fullType, out MainHudLearnedMagic magic)
    {
        foreach (MainHudLearnedMagic learned in _learnedMagic)
        {
            if (learned.Type == fullType)
            {
                magic = learned;
                return true;
            }
        }

        magic = default;
        return false;
    }

    public bool HasLearnedSubprofession(uint classId)
    {
        if (classId is < 1 or > 18)
        {
            return false;
        }

        long divisor = 1;

        for (uint index = 1; index < classId; index++)
        {
            divisor *= 10;
        }

        return (PackedSubprofessionPhases / divisor) % 10 > 0;
    }
}

internal interface IMainHudMagicHintSource
{
    MainHudMagicHintRuntimeSnapshot? Capture();
}

internal sealed class MainHudMagicHintStateStore : IMainHudMagicHintSource
{
    private MainHudMagicHintRuntimeSnapshot? _snapshot;

    public MainHudMagicHintRuntimeSnapshot? Capture() => Volatile.Read(ref _snapshot);

    public void Publish(MainHudMagicHintRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _snapshot, snapshot);
    }

    public void Clear() => Volatile.Write(ref _snapshot, null);
}
