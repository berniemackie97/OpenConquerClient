using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Tool.Tests;

/// <summary>
/// A disposable directory used to build synthetic source snapshots and content sets.
/// </summary>
internal sealed class TemporarySourceTree : IDisposable
{
    private const uint ProgressBackgroundUid = 0x0561D7F3;
    private const uint MainDialog1Uid = 0xCAE8016F;
    private const uint ProgressHpUid = 0x1311773C;
    private const uint ProgressHpAlternateUid = 0xE8F5223B;
    private const uint ProgressHpHighlightUid = 0xF1020E31;
    private const uint ProgressMpUid = 0xF4284E3C;
    private const uint ProgressMpAlternateUid = 0xAE67606C;
    private const uint ProgressMpHighlightUid = 0xA5D8EB93;
    private const uint ProgressPowerUid = 0x3BEA48E0;
    private const uint ProgressPowerHighlightUid = 0x9B8E4823;
    private const uint ProgressForceUid = 0xF29FAED2;
    private const uint ProgressForceAlternateUid = 0xC3DAFD40;

    public TemporarySourceTree()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "OpenConquer.Content.Tool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath
    {
        get;
    }

    public string ChildPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public void WriteText(string relativePath, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        WriteBytes(relativePath, Encoding.Latin1.GetBytes(contents));
    }

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> contents)
    {
        string filePath = ChildPath(relativePath);
        string directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException($"'{relativePath}' has no parent directory.");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(filePath, contents);
    }

    public void WriteStartupSnapshot(string backgroundFormat = "Data/Main/Logo%d.bmp")
    {
        byte[] progressBackground = CreateSyntheticDds("ProgressBk");
        byte[] mainDialog1 = CreateSyntheticDds("mainDialog1");
        byte[] progressHp = CreateSyntheticDds("ProgressHP");
        byte[] progressHpAlternate = CreateSyntheticDds("ProgressHPA");
        byte[] progressHpHighlight = CreateSyntheticDds("ProgressHPH");
        byte[] progressMp = CreateSyntheticDds("ProgressMP");
        byte[] progressMpAlternate = CreateSyntheticDds("ProgressMPA");
        byte[] progressMpHighlight = CreateSyntheticDds("ProgressMPH");
        byte[] progressPower = CreateSyntheticDds("ProgressPower");
        byte[] progressPowerHighlight = CreateSyntheticDds("ProgressPowerH");
        byte[] progressForce = CreateSyntheticDds("ProgressForce");
        byte[] progressForceAlternate = CreateSyntheticDds("ProgressForceA");

        WriteText("version.dat", "5517");
        WriteText("ini/GameSetUp.ini", "[ScreenMode]\nScreenModeRecord=2\n");
        WriteText("ini/info.ini", $"[DlgLogo]\nBgFormat={backgroundFormat}\n");
        WriteText("ini/package.ini", "data.wdf\nc3.wdf\ndata3.wdf\n");
        WriteText("ani/Control.ani", BuildControlAni());
        WriteText("ani/Magic.ani",
            "[MagicSkillType1000]\nFrameAmount=1\nFrame0=data/main/MagicSkillType1000.dds\n"
            + "[XpSkillType2000]\nFrameAmount=1\nFrame0=data/main/XpSkillType2000.dds\n"
            + "[MagicSkillType1415]\nFrameAmount=1\nFrame0=data/main3/skill38.dds\n"
            + "[MagicOther]\nFrameAmount=1\nFrame0=data/main/MagicOther.dds\n");
        WriteText("ani/ItemMinIcon.Ani",
            "[ItemDefault]\nFrameAmount=1\nFrame0=data/ItemMinIcon/Default.dds\n"
            + "[Item100]\nFrameAmount=1\nFrame0=data/ItemMinIcon/100.dds\n"
            + "[ItemPreview]\nFrameAmount=1\nFrame0=data/ItemMinIcon/Preview.dds\n");
        WriteText("ani/effect.ani",
            "[FireLight]\nFrameAmount=2\nFrame0=data/Pic/FireLight/01.dds\nFrame1=data/Pic/FireLight/02.dds\n"
            + "[RedLight]\nFrameAmount=1\nFrame0=data/Pic/RedLight/01.dds\n"
            + "[BlueLight]\nFrameAmount=1\nFrame0=data/Pic/BlueLight/01.dds\n"
            + "[RoyalBlueLight]\nFrameAmount=1\nFrame0=data/Pic/RoyalBlueLight/01.dds\n"
            + "[YellowLight]\nFrameAmount=1\nFrame0=data/Pic/YellowLight/01.dds\n"
            + "[CustomGlow]\nFrameAmount=1\nFrame0=data/Pic/CustomGlow/01.dds\n");

        WriteBytes("data/main/Logo1.bmp", TestBitmap.CreateTwoByTwo());
        WriteBytes("data/main/Logo2.bmp", TestBitmap.CreateTwoByTwo());

        WriteLooseDds("data/main/MainDialog2.dds", "MainDialog2 loose");
        WriteLooseDds("data/main/ProgressForce2.dds", "ProgressForce2 loose");
        WriteLooseDds("data/main/ProgressForce2a.dds", "ProgressForce2a loose");
        WriteLooseDds("data/main/QueryBtn.dds", "QueryBtn loose");
        WriteLooseDds("data/main/QueryBtnClick.dds", "QueryBtnClick loose");
        WriteLooseDds("data/main/LevWordBtn.dds", "LevWordBtn loose");
        WriteLooseDds("data/main/LevWordBtnClick.dds", "LevWordBtnClick loose");
        WriteLooseDds("data/main/GoodBtn.dds", "GoodBtn loose");
        WriteLooseDds("data/main/GoodBtnClick.dds", "GoodBtnClick loose");
        WriteLooseDds("data/main/SetBtn.dds", "SetBtn loose");
        WriteLooseDds("data/main/SetBtnClick.dds", "SetBtnClick loose");
        WriteLooseDds("data/interface/Style01/Action/MissionBtnNormal.dds", "MissionBtnNormal loose");
        WriteLooseDds("data/interface/Style01/Action/MissionBtnClick.dds", "MissionBtnClick loose");
        WriteLooseDds("data/interface/Style01/Action/MissionBtnEmboss.dds", "MissionBtnEmboss loose");
        WriteLooseDds("data/main/ChatBtn.dds", "ChatBtn loose");
        WriteLooseDds("data/main/ChatBtnClick.dds", "ChatBtnClick loose");
        WriteLooseDds("data/main/GroupBtn.dds", "GroupBtn loose");
        WriteLooseDds("data/main/GroupBtnClick.dds", "GroupBtnClick loose");
        WriteLooseDds("data/main/PkFree.dds", "PkFree loose");
        WriteLooseDds("data/main/PkFreeClick.dds", "PkFreeClick loose");
        WriteLooseDds("data/main/PkSafe.dds", "PkSafe loose");
        WriteLooseDds("data/main/PkSafeClick.dds", "PkSafeClick loose");
        WriteLooseDds("data/main/PkGroup.dds", "PkGroup loose");
        WriteLooseDds("data/main/PkGroupClick.dds", "PkGroupClick loose");
        WriteLooseDds("data/main/PkArre.dds", "PkArre loose");
        WriteLooseDds("data/main/PkArreClick.dds", "PkArreClick loose");
        WriteLooseDds("data/main/OrganiseBtnNormal.dds", "OrganiseBtnNormal loose");
        WriteLooseDds("data/main/OrganiseBtnClick.dds", "OrganiseBtnClick loose");
        WriteLooseDds("data/main/OrganiseBtnUnClick.dds", "OrganiseBtnUnClick loose");
        WriteLooseDds("data/main/OrganiseBtnEmboss.dds", "OrganiseBtnEmboss loose");
        WriteLooseDds("data/main/SkillBtn.dds", "SkillBtn loose");
        WriteLooseDds("data/main/SkillBtnClick.dds", "SkillBtnClick loose");
        WriteLooseDds("data/main/SkillBtnL.dds", "SkillBtnL loose");
        WriteLooseDds("data/main/RunChk1.dds", "RunChk1 loose");
        WriteLooseDds("data/main/RunChk2.dds", "RunChk2 loose");
        WriteLooseDds("data/main/MapChk1.dds", "MapChk1 loose");
        WriteLooseDds("data/main/MapChk2.dds", "MapChk2 loose");
        WriteLooseDds("data/main/ScreenMoveChk1.dds", "ScreenMoveChk1 loose");
        WriteLooseDds("data/main/ScreenMoveChk2.dds", "ScreenMoveChk2 loose");
        WriteLooseDds("data/main/NpcEquip.dds", "NpcEquip loose");
        WriteLooseDds("data/main/NpcEquipClick.dds", "NpcEquipClick loose");

        WriteQuickbarAssets();

        WriteBytes("data.wdf", CreateWdf(
            (ProgressBackgroundUid, progressBackground),
            (MainDialog1Uid, mainDialog1),
            (ProgressHpUid, progressHp),
            (ProgressHpAlternateUid, progressHpAlternate),
            (ProgressHpHighlightUid, progressHpHighlight),
            (ProgressMpUid, progressMp),
            (ProgressMpAlternateUid, progressMpAlternate),
            (ProgressMpHighlightUid, progressMpHighlight),
            (ProgressPowerUid, progressPower),
            (ProgressPowerHighlightUid, progressPowerHighlight),
            (ProgressForceUid, progressForce),
            (ProgressForceAlternateUid, progressForceAlternate)));
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static string BuildControlAni()
    {
        StringBuilder builder = new();

        AppendSection(builder, "Dialog4", "data/main/mainDialog1.dds", "data/main/mainDialog2.dds");
        AppendSection(builder, "Progress40", "data/main/ProgressHP.dds", "data/main/ProgressHPA.dds", "data/main/ProgressHPH.dds");
        AppendSection(builder, "Progress41", "data/main/ProgressMP.dds", "data/main/ProgressMPA.dds", "data/main/ProgressMPH.dds");
        AppendSection(builder, "Progress42", "data/main/ProgressPower.dds", "data/main/ProgressPower.dds", "data/main/ProgressPowerH.dds");
        AppendSection(builder, "Progress45", "data/main/ProgressBk.dds");
        AppendSection(builder, "Progress46", "data/main/ProgressForce.dds", "data/main/ProgressForceA.dds");
        AppendSection(builder, "Progress47", "data/main/ProgressForce2.dds", "data/main/ProgressForce2A.dds");
        AppendSection(builder, "Button40", "data/main/QueryBtn.dds", "data/main/QueryBtnClick.dds");
        AppendSection(builder, "Button410", "data/main/LevWordBtn.dds", "data/main/LevWordBtnClick.dds");
        AppendSection(builder, "Button42", "data/main/GoodBtn.dds", "data/main/GoodBtnClick.dds");
        AppendSection(builder, "Button43", "data/main/SetBtn.dds", "data/main/SetBtnClick.dds");
        AppendSection(builder, "Main3_MissionBtn", "data/interface/Style01/Action/MissionBtnNormal.dds", "data/interface/Style01/Action/MissionBtnClick.dds", "data/interface/Style01/Action/MissionBtnEmboss.dds");
        AppendSection(builder, "Button45", "data/main/ChatBtn.dds", "data/main/ChatBtnClick.dds");
        AppendSection(builder, "Button46", "data/main/GroupBtn.dds", "data/main/GroupBtnClick.dds");
        AppendSection(builder, "Button47", "data/main/PkFree.dds", "data/main/PkFreeClick.dds");
        AppendSection(builder, "Button49", "data/main/PkSafe.dds", "data/main/PkSafeClick.dds");
        AppendSection(builder, "Button48", "data/main/PkGroup.dds", "data/main/PkGroupClick.dds");
        AppendSection(builder, "Button412", "data/main/PkArre.dds", "data/main/PkArreClick.dds");
        AppendSection(builder, "Main3_OrganiseBtn", "data/main/OrganiseBtnNormal.dds", "data/main/OrganiseBtnClick.dds", "data/main/OrganiseBtnUnClick.dds", "data/main/OrganiseBtnEmboss.dds");
        AppendSection(builder, "Button41", "data/main/SkillBtn.dds", "data/main/SkillBtnClick.dds", "data/main/SkillBtnL.dds");
        AppendSection(builder, "Check40", "data/main/RunChk1.dds", "data/main/RunChk2.dds");
        AppendSection(builder, "Check43", "data/main/MapChk2.dds", "data/main/MapChk1.dds");
        AppendSection(builder, "Check46", "data/main/ScreenMoveChk1.dds", "data/main/ScreenMoveChk2.dds");
        AppendSection(builder, "Button411", "data/main/NpcEquip.dds", "data/main/NpcEquipClick.dds");

        AppendSection(builder, "Compose_CoverPic", "data/interface/compose/CoverPic.dds");
        AppendSection(builder, "Swapuse_UsemainbBtn", "data/main/UsemainbBtnNormal.dds");
        AppendSection(builder, "Swapuse_SwapmainbBtn", "data/main/SwapmainbBtnNormal.dds");
        AppendSection(builder, "Equip_AddPic", "data/interface/Style01/Equip/Num/AddPic.dds");

        for (int digit = 0; digit <= 9; digit++)
        {
            AppendSection(builder, $"Main3_Num{digit}Pic", $"data/main/Num{digit}Pic.dds");
            AppendSection(builder, $"Equip_Num{digit}", $"data/interface/Style01/Equip/Num/{digit}.dds");
        }

        AppendSection(builder, "ButtonA1", "data/main/Act1.dds");
        AppendSection(builder, "Action_Dance2Btn", "data/interface/Style01/Action/Dance2BtnNormal.dds");
        AppendSection(builder, "OtherControl", "data/main/UnusedControl.dds");

        return builder.ToString();
    }

    private void WriteQuickbarAssets()
    {
        WriteLooseDds("data/interface/compose/CoverPic.dds", "CoverPic");
        WriteLooseDds("data/main/UsemainbBtnNormal.dds", "UsemainbBtnNormal");
        WriteLooseDds("data/main/SwapmainbBtnNormal.dds", "SwapmainbBtnNormal");
        WriteLooseDds("data/interface/Style01/Equip/Num/AddPic.dds", "Equip AddPic");

        for (int digit = 0; digit <= 9; digit++)
        {
            WriteLooseDds($"data/main/Num{digit}Pic.dds", $"Num{digit}Pic");
            WriteLooseDds($"data/interface/Style01/Equip/Num/{digit}.dds", $"Equip Num {digit}");
        }

        WriteLooseDds("data/main/Act1.dds", "Act1");
        WriteLooseDds("data/interface/Style01/Action/Dance2BtnNormal.dds", "Dance2BtnNormal");
        WriteLooseDds("data/main/MagicSkillType1000.dds", "MagicSkillType1000");
        WriteLooseDds("data/main/XpSkillType2000.dds", "XpSkillType2000");
        WriteLooseDds("data/ItemMinIcon/Default.dds", "ItemDefault");
        WriteLooseDds("data/ItemMinIcon/100.dds", "Item100");
        WriteLooseDds("data/Pic/FireLight/01.dds", "FireLight01");
        WriteLooseDds("data/Pic/FireLight/02.dds", "FireLight02");
        WriteLooseDds("data/Pic/RedLight/01.dds", "RedLight01");
        WriteLooseDds("data/Pic/BlueLight/01.dds", "BlueLight01");
        WriteLooseDds("data/Pic/RoyalBlueLight/01.dds", "RoyalBlueLight01");
        WriteLooseDds("data/Pic/YellowLight/01.dds", "YellowLight01");

        WriteLooseDds("data/main/UnusedControl.dds", "UnusedControl");
        WriteLooseDds("data/main/MagicOther.dds", "MagicOther");
        WriteLooseDds("data/ItemMinIcon/Preview.dds", "ItemPreview");
        WriteLooseDds("data/Pic/CustomGlow/01.dds", "CustomGlow01");
    }

    private void WriteLooseDds(string path, string marker) => WriteBytes(path, CreateSyntheticDds(marker));

    private static void AppendSection(StringBuilder builder, string sectionName, params string[] framePaths)
    {
        builder.Append('[').Append(sectionName).Append("]\nFrameAmount=").Append(framePaths.Length).Append('\n');

        for (int frameIndex = 0; frameIndex < framePaths.Length; frameIndex++)
        {
            builder.Append("Frame").Append(frameIndex).Append('=').Append(framePaths[frameIndex]).Append('\n');
        }
    }

    private static byte[] CreateSyntheticDds(string marker) => Encoding.ASCII.GetBytes($"DDS {marker}");

    private static byte[] CreateWdf(params (uint Uid, byte[] Payload)[] entries)
    {
        const int headerLength = 12;
        const int entryLength = 16;

        (uint Uid, byte[] Payload)[] orderedEntries = entries.OrderBy(static entry => entry.Uid).ToArray();
        int payloadLength = orderedEntries.Sum(static entry => entry.Payload.Length);
        int tableOffset = checked(headerLength + payloadLength);
        byte[] archive = new byte[checked(tableOffset + orderedEntries.Length * entryLength)];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, 0x57444650);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), checked((uint)orderedEntries.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), checked((uint)tableOffset));

        int payloadOffset = headerLength;

        for (int index = 0; index < orderedEntries.Length; index++)
        {
            (uint uid, byte[] payload) = orderedEntries[index];
            payload.CopyTo(archive, payloadOffset);

            Span<byte> encodedEntry = archive.AsSpan(tableOffset + index * entryLength, entryLength);
            BinaryPrimitives.WriteUInt32LittleEndian(encodedEntry, uid);
            BinaryPrimitives.WriteUInt32LittleEndian(encodedEntry[4..], checked((uint)payloadOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(encodedEntry[8..], checked((uint)payload.Length));

            payloadOffset = checked(payloadOffset + payload.Length);
        }

        return archive;
    }
}
