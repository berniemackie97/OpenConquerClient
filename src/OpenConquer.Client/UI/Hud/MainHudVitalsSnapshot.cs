namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudVitalsSnapshot(int CurrentLife, int LifeFollower, int MaxLife, int CurrentMana, int ManaFollower, int MaxMana, int Stamina, int MaxStamina, bool HasExtendedStaminaGauge);
