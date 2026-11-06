using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

/// <summary>Feeds raw 1008/1009 test frames through the production dispatcher and codecs.</summary>
internal static class WorldEquipmentFrames
{
    internal static WorldHeroEquipment Hydrate(IEnumerable<byte[]> packets)
    {
        WorldHeroEquipment equipment = new();
        foreach (byte[] packet in packets)
        {
            equipment.Apply(packet);
        }

        return equipment;
    }

    internal static bool Apply(this WorldHeroEquipment equipment, byte[] packet) =>
        GameWorldPacketDispatcher.Dispatch(packet, new EquipmentOnly(equipment, null));

    internal static void Apply(this WorldHeroModel hero, byte[] packet) =>
        GameWorldPacketDispatcher.Dispatch(packet, new EquipmentOnly(null, hero));

    private sealed class EquipmentOnly(WorldHeroEquipment? equipment, WorldHeroModel? hero) : IGameWorldPacketHandler
    {
        public bool HandleItemRecord(GameItemRecord1008 record)
        {
            hero?.ApplyItemRecord(record);
            return equipment?.Apply(record) ?? false;
        }

        public bool HandleItem(GameItem1009 item)
        {
            hero?.ApplyItem(item);
            return equipment?.Apply(item) ?? false;
        }

        public bool HandleWalk(GameWalk10005 walk) => false;

        public bool HandleAction(GameWorldAction10010 action) => false;

        public bool HandleAttributes(GameUserAttrib10017 attributes) => false;

        public bool HandleWeather(GameWeather1016 weather) => false;

        public bool HandleUnknown(ushort packetType) => false;
    }
}
