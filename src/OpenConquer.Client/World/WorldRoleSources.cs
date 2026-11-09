namespace OpenConquer.Client.World;

internal readonly record struct WorldRoleSources(
    uint BodyModelId, uint ArmetModelId, uint RightWeaponModelId, uint LeftWeaponModelId)
{
    private const uint ModelFamilyScale = 1_000_000;
    private const uint LinkedHairFamily = 0x1D0D8;

    internal static WorldRoleSources Resolve(uint packedAppearance, ushort hairComposite,
        WorldHeroEquipment equipment) =>
        Resolve(packedAppearance, hairComposite, equipment.GetActiveItem(1)?.TypeId ?? 0,
            equipment.GetActiveItem(3)?.TypeId ?? 0, equipment.GetActiveItem(4)?.TypeId ?? 0,
            equipment.GetActiveItem(5)?.TypeId ?? 0);

    internal static WorldRoleSources Resolve(uint packedAppearance, ushort hairComposite,
        uint headwearType, uint armorType, uint rightHandType, uint leftHandType)
    {
        uint bodyModel = packedAppearance % 1000 * ModelFamilyScale;
        uint hairStyle = (uint)hairComposite % 1000;
        uint armetModel = hairStyle % 100 == 0 ? 0 : bodyModel + LinkedHairFamily + hairStyle;

        if (headwearType != 0)
        {
            armetModel = bodyModel + NormalizeItemSource(headwearType);
        }

        if (armorType != 0)
        {
            bodyModel += NormalizeItemSource(armorType);
        }

        (uint rightWeapon, uint leftWeapon) = ResolveWeapons(rightHandType, leftHandType);

        return new WorldRoleSources(bodyModel, armetModel, rightWeapon, leftWeapon);
    }

    private static (uint Right, uint Left) ResolveWeapons(uint rightSource, uint leftSource)
    {
        uint right = 0;
        uint left = NormalizeItemSource(leftSource);

        if (leftSource / 10_000 == 105)
        {
            right = left;
            left = 0;
        }

        uint rightFamily = rightSource / 1_000 % 1_000;
        if (rightFamily is 500 or 370)
        {
            left = NormalizeItemSource(rightSource);
            right = 0;
        }
        else if (rightSource != 0)
        {
            if (rightSource / 10_000 == 105 && left / 1_000 % 1_000 == 500)
            {
                left = 0;
            }

            right = NormalizeItemSource(rightSource);
        }

        return (right, left);
    }

    private static uint NormalizeItemSource(uint itemTypeId) =>
        (itemTypeId % ModelFamilyScale / 10) * 10;
}
