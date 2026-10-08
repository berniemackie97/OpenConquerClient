namespace OpenConquer.Client.UI.Hud.Quickbar;

internal readonly record struct MainHudQuickbarSlotMetadata(uint AddLevel, uint StackCount, uint OpaqueItemValue, uint InscribedFlag)
{
    public byte OpaqueItemByte => unchecked((byte)OpaqueItemValue);
}

internal readonly record struct MainHudQuickbarSlotSnapshot(byte OccupiedFlag, uint Selector, uint AggregateQuantity, byte CoverFlag, byte ContentKind, byte GlowKind, uint GlowStartTime, uint Payload, uint ContentId, uint ItemUid, MainHudQuickbarSlotMetadata Metadata)
{
    public bool IsOccupied => OccupiedFlag != 0;
}

internal sealed class MainHudQuickbarSlotsState
{
    private const int StorageCapacity = 256;
    private const uint InitialSelector = 3;
    private static readonly MainHudQuickbarSlotMetadata s_initialMetadata = new(0, 0, 1, 0);

    private readonly uint[] _selectors = new uint[StorageCapacity];
    private readonly uint[] _aggregateQuantities = new uint[StorageCapacity];
    private readonly byte[] _occupiedFlags = new byte[StorageCapacity];
    private readonly byte[] _coverFlags = new byte[StorageCapacity];
    private readonly byte[] _contentKinds = new byte[StorageCapacity];
    private readonly byte[] _glowKinds = new byte[StorageCapacity];
    private readonly uint[] _glowStartTimes = new uint[StorageCapacity];
    private readonly uint[] _payloads = new uint[StorageCapacity];
    private readonly uint[] _contentIds = new uint[StorageCapacity];
    private readonly uint[] _itemUids = new uint[StorageCapacity];
    private readonly MainHudQuickbarSlotMetadata[] _metadata = new MainHudQuickbarSlotMetadata[StorageCapacity];
    private uint _insertionRemovalCounter;

    public MainHudQuickbarSlotsState()
    {
        Array.Fill(_selectors, InitialSelector);
        Array.Fill(_metadata, s_initialMetadata);
    }

    public uint InsertionRemovalCounter => _insertionRemovalCounter;

    public MainHudQuickbarSlotSnapshot GetSlot(int slotIndex)
    {
        ValidateStorageIndex(slotIndex);
        return new MainHudQuickbarSlotSnapshot(_occupiedFlags[slotIndex], _selectors[slotIndex], _aggregateQuantities[slotIndex], _coverFlags[slotIndex], _contentKinds[slotIndex], _glowKinds[slotIndex], _glowStartTimes[slotIndex], _payloads[slotIndex], _contentIds[slotIndex], _itemUids[slotIndex], _metadata[slotIndex]);
    }

    public bool IsOccupied(int columnOneBased, int rowOneBased)
    {
        int slotIndex = GetNativeStorageIndex(columnOneBased, rowOneBased);
        return (uint)slotIndex < StorageCapacity && _occupiedFlags[slotIndex] != 0;
    }

    public static int GetVisibleSlotIndex(int columnOneBased, int rowOneBased)
    {
        ValidateVisibleCoordinates(columnOneBased, rowOneBased);
        return GetNativeStorageIndex(columnOneBased, rowOneBased);
    }

    public int Populate(int columnOneBased, int rowOneBased, uint contentId, uint payload, uint selector, byte contentKind, byte glowKind, MainHudQuickbarSlotMetadata metadata)
    {
        ValidateVisibleCoordinates(columnOneBased, rowOneBased);

        int slotIndex = GetNativeStorageIndex(columnOneBased, rowOneBased);

        _occupiedFlags[slotIndex] = 1;
        _contentIds[slotIndex] = contentId;
        _payloads[slotIndex] = payload;
        _selectors[slotIndex] = selector;
        _contentKinds[slotIndex] = contentKind;
        _glowKinds[slotIndex] = glowKind;
        _metadata[slotIndex] = metadata;

        if (glowKind != 0)
        {
            _glowStartTimes[slotIndex] = 0;
        }

        _insertionRemovalCounter = unchecked(_insertionRemovalCounter + 1);
        return slotIndex;
    }

    public void ClearOccupied(int columnOneBased, int rowOneBased)
    {
        int slotIndex = GetNativeStorageIndex(columnOneBased, rowOneBased);

        if ((uint)slotIndex >= StorageCapacity || _occupiedFlags[slotIndex] == 0)
        {
            return;
        }

        _occupiedFlags[slotIndex] = 0;
        _insertionRemovalCounter = unchecked(_insertionRemovalCounter - 1);
    }

    public void ClearAllOccupied()
    {
        Array.Clear(_occupiedFlags);
        _insertionRemovalCounter = 0;
    }

    public void SetItemUid(int slotIndex, uint itemUid)
    {
        ValidateStorageIndex(slotIndex);
        _itemUids[slotIndex] = itemUid;
    }

    public void SetAggregateQuantity(int slotIndex, uint quantity)
    {
        ValidateStorageIndex(slotIndex);
        _aggregateQuantities[slotIndex] = quantity;
    }

    public void SetCoverFlag(int slotIndex, byte coverFlag)
    {
        ValidateStorageIndex(slotIndex);
        _coverFlags[slotIndex] = coverFlag;
    }

    public void SetGlowStartTime(int slotIndex, uint startTime)
    {
        ValidateStorageIndex(slotIndex);
        _glowStartTimes[slotIndex] = startTime;
    }

    private static int GetNativeStorageIndex(int columnOneBased, int rowOneBased) =>
        unchecked(MainHudQuickbarDefinition.ColumnCount * (rowOneBased - 1) + columnOneBased - 1);

    private static void ValidateVisibleCoordinates(int columnOneBased, int rowOneBased)
    {
        if ((uint)(columnOneBased - 1) >= MainHudQuickbarDefinition.ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(columnOneBased), columnOneBased, $"Column must be between 1 and {MainHudQuickbarDefinition.ColumnCount}.");
        }

        if ((uint)(rowOneBased - 1) >= MainHudQuickbarDefinition.RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOneBased), rowOneBased, $"Row must be between 1 and {MainHudQuickbarDefinition.RowCount}.");
        }
    }

    private static void ValidateStorageIndex(int slotIndex)
    {
        if ((uint)slotIndex >= StorageCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, $"Slot index must be between 0 and {StorageCapacity - 1}.");
        }
    }
}
