using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenRegistrationF001F002Tests
{
    [Fact]
    public void RegistrationExtension_UsesPinnedProjectDefinedFrames()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "WireGolden", "F001-F002-registration.project-defined.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        Assert.Equal("project-defined", root.GetProperty("classification").GetString());
        Assert.False(root.GetProperty("runtimeCaptured").GetBoolean());
        Assert.Equal("docs/networking/FLOW-001B-account-registration.md", root.GetProperty("sourceSpecification").GetString());
        Assert.Equal("e13bf5724975ec825e64a3609719afedd9b6af41", root.GetProperty("sourceCommit").GetString());
        Assert.Equal(0x0012_34ABu, root.GetProperty("loginSeed").GetUInt32());

        JsonElement requestCase = root.GetProperty("request");
        byte[] expectedRequest = Convert.FromHexString(requestCase.GetProperty("frameHex").GetString()!);
        Assert.Equal(AccountRegistrationRequestF001.PacketType, requestCase.GetProperty("packetId").GetUInt16());
        Assert.Equal("client-to-server", requestCase.GetProperty("direction").GetString());
        Assert.Equal(AccountRegistrationRequestF001.PacketLength, expectedRequest.Length);
        Assert.Equal(expectedRequest.Length, BinaryPrimitives.ReadUInt16LittleEndian(expectedRequest));
        Assert.Equal(AccountRegistrationRequestF001.PacketType, BinaryPrimitives.ReadUInt16LittleEndian(expectedRequest.AsSpan(2)));
        Assert.Equal(requestCase.GetProperty("frameSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(expectedRequest)).ToLowerInvariant());

        byte[] writtenRequest = new byte[AccountRegistrationRequestF001.PacketLength];
        AccountRegistrationRequestF001.Write(writtenRequest, "testacc"u8, Convert.FromHexString("22DB42ACB82F421DAEF13F7A611D5A032F45309A4D0DDC652F45309A4D0DDC65"));
        Assert.Equal(expectedRequest, writtenRequest);

        int count = 0;
        foreach (JsonElement responseCase in root.GetProperty("responses").EnumerateArray())
        {
            byte[] frame = Convert.FromHexString(responseCase.GetProperty("frameHex").GetString()!);
            uint resultCode = responseCase.GetProperty("resultCode").GetUInt32();
            Assert.Equal((uint)count++, resultCode);
            Assert.Equal(AccountRegistrationResponseF002.PacketType, responseCase.GetProperty("packetId").GetUInt16());
            Assert.Equal("server-to-client", responseCase.GetProperty("direction").GetString());
            Assert.Equal(AccountRegistrationResponseF002.FrameLength, frame.Length);
            Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
            Assert.Equal(AccountRegistrationResponseF002.PacketType, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
            Assert.Equal(responseCase.GetProperty("frameSha256").GetString(),
                Convert.ToHexString(SHA256.HashData(frame)).ToLowerInvariant());
            Assert.True(AccountRegistrationResponseF002.TryRead(frame, out AccountRegistrationResult result));
            Assert.Equal((AccountRegistrationResult)resultCode, result);
        }

        Assert.Equal(5, count);
    }
}
