using System.Buffers.Binary;
using System.Text;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginAccountResponse1055Tests
{
    [Fact]
    public void SuccessDecodesVerifiedNativeFieldsWithoutAuthorizingHandoff()
    {
        byte[] frame = Response(session: 0x12345678, key: 0x89ABCDEF, port: 5816, additional: 77, host: "127.0.0.1");

        Assert.True(LoginAccountResponse1055.TryRead(frame, out LoginAccountResponse1055 result));
        Assert.True(result.IsSuccess);
        Assert.Equal(0x12345678u, result.SessionToken);
        Assert.Equal(0x89ABCDEFu, result.AuthenticationKeyOrFailureCode);
        Assert.Equal(5816u, result.GamePort);
        Assert.Equal(77u, result.AdditionalSessionField);
        Assert.Equal("127.0.0.1", result.GameHost);
    }

    [Fact]
    public void DiagnosticTextRedactsAuthenticationFieldsAndEndpoint()
    {
        byte[] successful = Response(0x12345678, 0x89ABCDEF, 5816, 77, "127.0.0.1");
        Assert.True(LoginAccountResponse1055.TryRead(successful, out LoginAccountResponse1055 response));
        Assert.Equal("LoginAccountResponse1055 { IsSuccess = True }", response.ToString());
        Assert.Equal("LoginAccountResponse1055 { IsSuccess = True }", $"{response}");

        byte[] failed = Response(0, 20, 5816, 77, "127.0.0.1");
        Assert.True(LoginAccountResponse1055.TryRead(failed, out LoginAccountResponse1055 failure));
        Assert.Equal("LoginAccountResponse1055 { IsSuccess = False }", failure.ToString());
    }

    [Fact]
    public void FailureReadsOnlyTheNativeFailureCodeNotStaleEndpointBytes()
    {
        byte[] frame = Response(session: 0, key: 20, port: 5816, additional: 9, host: "10.1.2.3");

        Assert.True(LoginAccountResponse1055.TryRead(frame, out LoginAccountResponse1055 result));
        Assert.False(result.IsSuccess);
        Assert.Equal(20u, result.AuthenticationKeyOrFailureCode);
        Assert.Equal(0u, result.GamePort);
        Assert.Equal(0u, result.AdditionalSessionField);
        Assert.Null(result.GameHost);
    }

    [Theory]
    [InlineData("127.0.0.1", 0u)]
    [InlineData("127.0.0.1", 65536u)]
    [InlineData("127.1", 5816u)]
    [InlineData("01.2.3.4", 5816u)]
    [InlineData("1.2.3.999", 5816u)]
    [InlineData("example.com", 5816u)]
    [InlineData("", 5816u)]
    public void InvalidSuccessfulHandoffFieldsFailClosed(string host, uint port)
    {
        byte[] frame = Response(session: 1, key: 2, port: port, additional: 3, host: host);
        Assert.False(LoginAccountResponse1055.TryRead(frame, out _));
    }

    [Fact]
    public void RejectsUnterminatedAndNonzeroGarbageAfterIpTerminator()
    {
        byte[] frame = Response(1, 2, 5816, 3, "127.0.0.1");
        frame[^1] = (byte)'x';
        Assert.False(LoginAccountResponse1055.TryRead(frame, out _));

        frame = Response(1, 2, 5816, 3, "127.0.0.1");
        frame.AsSpan(20, 16).Fill((byte)'1');
        Assert.False(LoginAccountResponse1055.TryRead(frame, out _));
    }

    [Fact]
    public void RejectsTruncatedOversizedWrongTypeAndInconsistentLength()
    {
        byte[] frame = Response(1, 2, 5816, 3, "127.0.0.1");
        Assert.False(LoginAccountResponse1055.TryRead(frame.AsSpan()[..35], out _));
        Assert.False(LoginAccountResponse1055.TryRead([.. frame, 0], out _));
        frame[2] = 0;
        Assert.False(LoginAccountResponse1055.TryRead(frame, out _));
        frame = Response(1, 2, 5816, 3, "127.0.0.1");
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 35);
        Assert.False(LoginAccountResponse1055.TryRead(frame, out _));
    }

    private static byte[] Response(uint session, uint key, uint port, uint additional, string host)
    {
        byte[] frame = new byte[LoginAccountResponse1055.FrameLength];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), LoginAccountResponse1055.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), session);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), key);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(12), port);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(16), additional);
        Encoding.ASCII.GetBytes(host.AsSpan(), frame.AsSpan(20));
        return frame;
    }
}
