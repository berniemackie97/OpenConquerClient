namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudSelectedSkillState
{
    public string SectionName { get; private set; } = MainHudSelectedSkillDefinition.InitialSectionName;

    public uint ContentId
    {
        get; private set;
    }

    public uint BlockedCover
    {
        get; private set;
    }

    public byte CoverFlag
    {
        get; private set;
    }

    public bool IsImageActive
    {
        get; private set;
    }

    public bool IsCovered => BlockedCover != 0 || CoverFlag != 0;
    public bool IsInputBlocked => BlockedCover != 0;

    public void SetSectionAndContent(string sectionName, uint contentId, uint blockedCover)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        IsImageActive = true;
        BlockedCover = blockedCover;
        SectionName = sectionName;
        ContentId = contentId;
    }

    public void ClearLoadedImage()
    {
        IsImageActive = false;
        BlockedCover = 0;
        ContentId = 0;
    }

    public void SetCoverFlag(byte coverFlag) => CoverFlag = coverFlag;
}
