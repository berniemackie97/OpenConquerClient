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

    /// <summary>
    /// A path under this tree that has deliberately not been created.
    /// </summary>
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

    /// <summary>
    /// Writes a synthetic retail-shaped source containing every dependency in the implemented runtime content closure.
    /// </summary>
    /// <remarks>
    /// Control.ani is loose. ProgressBk, Dialog4 frame 0, HP, MP, Progress42, and Progress46 frames are package-backed.
    /// Dialog4 frame 1, ProgressForce2, ProgressForce2a, and action-strip frames are loose synthetic assets.
    /// Historical Server.dat is deliberately absent because it is not runtime content.
    /// </remarks>
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
        WriteText("ani/Control.ani", "[Dialog4]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/mainDialog1.dds\n"
                                     + "Frame1=data/main/mainDialog2.dds\n"
                                     + "[Progress40]\n"
                                     + "FrameAmount=3\n"
                                     + "Frame0=data/main/ProgressHP.dds\n"
                                     + "Frame1=data/main/ProgressHPA.dds\n"
                                     + "Frame2=data/main/ProgressHPH.dds\n"
                                     + "[Progress41]\n"
                                     + "FrameAmount=3\n"
                                     + "Frame0=data/main/ProgressMP.dds\n"
                                     + "Frame1=data/main/ProgressMPA.dds\n"
                                     + "Frame2=data/main/ProgressMPH.dds\n"
                                     + "[Progress42]\n"
                                     + "FrameAmount=3\n"
                                     + "Frame0=data/main/ProgressPower.dds\n"
                                     + "Frame1=data/main/ProgressPower.dds\n"
                                     + "Frame2=data/main/ProgressPowerH.dds\n"
                                     + "[Progress45]\n"
                                     + "FrameAmount=1\n"
                                     + "Frame0=data/main/ProgressBk.dds\n"
                                     + "[Progress46]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/ProgressForce.dds\n"
                                     + "Frame1=data/main/ProgressForceA.dds\n"
                                     + "[Progress47]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/ProgressForce2.dds\n"
                                     + "Frame1=data/main/ProgressForce2A.dds\n"
                                     + "[Button40]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/QueryBtn.dds\n"
                                     + "Frame1=data/main/QueryBtnClick.dds\n"
                                     + "[Button410]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/LevWordBtn.dds\n"
                                     + "Frame1=data/main/LevWordBtnClick.dds\n"
                                     + "[Button42]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/GoodBtn.dds\n"
                                     + "Frame1=data/main/GoodBtnClick.dds\n"
                                     + "[Button43]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/SetBtn.dds\n"
                                     + "Frame1=data/main/SetBtnClick.dds\n"
                                     + "[Main3_MissionBtn]\n"
                                     + "FrameAmount=3\n"
                                     + "Frame0=data/interface/Style01/Action/MissionBtnNormal.dds\n"
                                     + "Frame1=data/interface/Style01/Action/MissionBtnClick.dds\n"
                                     + "Frame2=data/interface/Style01/Action/MissionBtnEmboss.dds\n"
                                     + "[Button45]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/ChatBtn.dds\n"
                                     + "Frame1=data/main/ChatBtnClick.dds\n"
                                     + "[Button46]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/GroupBtn.dds\n"
                                     + "Frame1=data/main/GroupBtnClick.dds\n"
                                     + "[Button47]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/PkFree.dds\n"
                                     + "Frame1=data/main/PkFreeClick.dds\n"
                                     + "[Button49]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/PkSafe.dds\n"
                                     + "Frame1=data/main/PkSafeClick.dds\n"
                                     + "[Button48]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/PkGroup.dds\n"
                                     + "Frame1=data/main/PkGroupClick.dds\n"
                                     + "[Button412]\n"
                                     + "FrameAmount=2\n"
                                     + "Frame0=data/main/PkArre.dds\n"
                                     + "Frame1=data/main/PkArreClick.dds\n"
                                     + "[Main3_OrganiseBtn]\n"
                                     + "FrameAmount=4\n"
                                     + "Frame0=data/main/OrganiseBtnNormal.dds\n"
                                     + "Frame1=data/main/OrganiseBtnClick.dds\n"
                                     + "Frame2=data/main/OrganiseBtnUnClick.dds\n"
                                     + "Frame3=data/main/OrganiseBtnEmboss.dds\n"
                                     + "[Button41]\n"
                                     + "FrameAmount=3\n"
                                     + "Frame0=data/main/SkillBtn.dds\n"
                                     + "Frame1=data/main/SkillBtnClick.dds\n"
                                     + "Frame2=data/main/SkillBtnL.dds\n");

        WriteBytes("data/main/Logo1.bmp", TestBitmap.CreateTwoByTwo());
        WriteBytes("data/main/Logo2.bmp", TestBitmap.CreateTwoByTwo());
        WriteBytes("data/main/MainDialog2.dds", CreateSyntheticDds("MainDialog2 loose"));
        WriteBytes("data/main/ProgressForce2.dds", CreateSyntheticDds("ProgressForce2 loose"));
        WriteBytes("data/main/ProgressForce2a.dds", CreateSyntheticDds("ProgressForce2a loose"));
        WriteBytes("data/main/QueryBtn.dds", CreateSyntheticDds("QueryBtn loose"));
        WriteBytes("data/main/QueryBtnClick.dds", CreateSyntheticDds("QueryBtnClick loose"));
        WriteBytes("data/main/LevWordBtn.dds", CreateSyntheticDds("LevWordBtn loose"));
        WriteBytes("data/main/LevWordBtnClick.dds", CreateSyntheticDds("LevWordBtnClick loose"));
        WriteBytes("data/main/GoodBtn.dds", CreateSyntheticDds("GoodBtn loose"));
        WriteBytes("data/main/GoodBtnClick.dds", CreateSyntheticDds("GoodBtnClick loose"));
        WriteBytes("data/main/SetBtn.dds", CreateSyntheticDds("SetBtn loose"));
        WriteBytes("data/main/SetBtnClick.dds", CreateSyntheticDds("SetBtnClick loose"));
        WriteBytes("data/interface/Style01/Action/MissionBtnNormal.dds", CreateSyntheticDds("MissionBtnNormal loose"));
        WriteBytes("data/interface/Style01/Action/MissionBtnClick.dds", CreateSyntheticDds("MissionBtnClick loose"));
        WriteBytes("data/interface/Style01/Action/MissionBtnEmboss.dds", CreateSyntheticDds("MissionBtnEmboss loose"));
        WriteBytes("data/main/ChatBtn.dds", CreateSyntheticDds("ChatBtn loose"));
        WriteBytes("data/main/ChatBtnClick.dds", CreateSyntheticDds("ChatBtnClick loose"));
        WriteBytes("data/main/GroupBtn.dds", CreateSyntheticDds("GroupBtn loose"));
        WriteBytes("data/main/GroupBtnClick.dds", CreateSyntheticDds("GroupBtnClick loose"));
        WriteBytes("data/main/PkFree.dds", CreateSyntheticDds("PkFree loose"));
        WriteBytes("data/main/PkFreeClick.dds", CreateSyntheticDds("PkFreeClick loose"));
        WriteBytes("data/main/PkSafe.dds", CreateSyntheticDds("PkSafe loose"));
        WriteBytes("data/main/PkSafeClick.dds", CreateSyntheticDds("PkSafeClick loose"));
        WriteBytes("data/main/PkGroup.dds", CreateSyntheticDds("PkGroup loose"));
        WriteBytes("data/main/PkGroupClick.dds", CreateSyntheticDds("PkGroupClick loose"));
        WriteBytes("data/main/PkArre.dds", CreateSyntheticDds("PkArre loose"));
        WriteBytes("data/main/PkArreClick.dds", CreateSyntheticDds("PkArreClick loose"));
        WriteBytes("data/main/OrganiseBtnNormal.dds", CreateSyntheticDds("OrganiseBtnNormal loose"));
        WriteBytes("data/main/OrganiseBtnClick.dds", CreateSyntheticDds("OrganiseBtnClick loose"));
        WriteBytes("data/main/OrganiseBtnUnClick.dds", CreateSyntheticDds("OrganiseBtnUnClick loose"));
        WriteBytes("data/main/OrganiseBtnEmboss.dds", CreateSyntheticDds("OrganiseBtnEmboss loose"));
        WriteBytes("data/main/SkillBtn.dds", CreateSyntheticDds("SkillBtn loose"));
        WriteBytes("data/main/SkillBtnClick.dds", CreateSyntheticDds("SkillBtnClick loose"));
        WriteBytes("data/main/SkillBtnL.dds", CreateSyntheticDds("SkillBtnL loose"));

        WriteBytes("data.wdf", CreateWdf((ProgressBackgroundUid, progressBackground), (MainDialog1Uid, mainDialog1),
            (ProgressHpUid, progressHp), (ProgressHpAlternateUid, progressHpAlternate), (ProgressHpHighlightUid, progressHpHighlight),
            (ProgressMpUid, progressMp), (ProgressMpAlternateUid, progressMpAlternate), (ProgressMpHighlightUid, progressMpHighlight),
            (ProgressPowerUid, progressPower), (ProgressPowerHighlightUid, progressPowerHighlight),
            (ProgressForceUid, progressForce), (ProgressForceAlternateUid, progressForceAlternate)));
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static byte[] CreateSyntheticDds(string marker)
    {
        return Encoding.ASCII.GetBytes($"DDS {marker}");
    }

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
