namespace OpenConquer.Client.World;

internal readonly record struct WorldRoleSources(uint BodyModelId, uint ArmetModelId)
{
    private const uint ModelFamilyScale = 1_000_000;
    private const uint LinkedHairFamily = 0x1D0D8;

    internal static WorldRoleSources Resolve(uint packedAppearance, ushort hairComposite,
        WorldHeroEquipment equipment)
    {
        uint bodyModel = packedAppearance % 1000 * ModelFamilyScale;
        uint hairStyle = (uint)hairComposite % 1000;
        uint armetModel = hairStyle % 100 == 0 ? 0 : bodyModel + LinkedHairFamily + hairStyle;

        if (equipment.GetActiveItem(1) is { } headwear)
        {
            armetModel = bodyModel + NormalizeItemSource(headwear.TypeId);
        }

        if (equipment.GetActiveItem(3) is { } armor)
        {
            bodyModel += NormalizeItemSource(armor.TypeId);
        }

        return new WorldRoleSources(bodyModel, armetModel);
    }

    private static uint NormalizeItemSource(uint itemTypeId) =>
        (itemTypeId % ModelFamilyScale / 10) * 10;
}
