using System.Buffers;
using System.Buffers.Binary;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginDecodedFrameReaderTests
{
    [Fact]
    public void ReadsConcatenatedFramesAndLeavesTrailingPartialFrameUnconsumed()
    {
        byte[] first = Seed(0x01234567u);
        byte[] second = Seed(0x89ABCDEFu);
        byte[] combined = [.. first, .. second, 8, 0];
        ReadOnlySequence<byte> buffer = new(combined);

        Assert.True(LoginDecodedFrameReader.TryRead(ref buffer, out ReadOnlySequence<byte> frame));
        Assert.Equal(first, frame.ToArray());
        Assert.True(LoginDecodedFrameReader.TryRead(ref buffer, out frame));
        Assert.Equal(second, frame.ToArray());
        Assert.False(LoginDecodedFrameReader.TryRead(ref buffer, out frame));
        Assert.Equal(new byte[] { 8, 0 }, buffer.ToArray());
    }

    [Fact]
    public void IncompleteHeaderAndBodyNeverConsumeInput()
    {
        byte[] seed = Seed(42);
        for (int length = 0; length < seed.Length; length++)
        {
            ReadOnlySequence<byte> buffer = new(seed.AsMemory(0, length));
            Assert.False(LoginDecodedFrameReader.TryRead(ref buffer, out _));
            Assert.Equal(length, buffer.Length);
        }
    }

    [Fact]
    public void SplitHeaderAcrossSequenceSegmentsParsesWithoutCopyingTheFrame()
    {
        byte[] seed = Seed(0x13572468);
        Segment head = new(seed.AsMemory(0, 1));
        Segment tail = head.Append(seed.AsMemory(1, 1)).Append(seed.AsMemory(2, 2)).Append(seed.AsMemory(4));
        ReadOnlySequence<byte> buffer = new(head, 0, tail, tail.Memory.Length);

        Assert.True(LoginDecodedFrameReader.TryRead(ref buffer, out ReadOnlySequence<byte> frame));
        Assert.Equal(seed, frame.ToArray());
        Assert.True(buffer.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1025)]
    [InlineData(65535)]
    public void MalformedLengthsAreRejectedAsSoonAsTheirHeaderArrives(int length)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1059);
        ReadOnlySequence<byte> buffer = new(bytes);

        Assert.Throws<InvalidDataException>(() =>
        {
            ReadOnlySequence<byte> candidate = new(bytes);
            LoginDecodedFrameReader.TryRead(ref candidate, out _);
        });
        Assert.Equal(4, buffer.Length);
    }

    [Fact]
    public void ZeroPacketTypeFailsClosedWithoutConsumingInput()
    {
        byte[] frame = Seed(5);
        frame[2] = frame[3] = 0;
        ReadOnlySequence<byte> buffer = new(frame);

        Assert.Throws<InvalidDataException>(() =>
        {
            ReadOnlySequence<byte> candidate = new(frame);
            LoginDecodedFrameReader.TryRead(ref candidate, out _);
        });
        Assert.Equal(frame, buffer.ToArray());
    }

    [Fact]
    public void MaximumLengthFrameIsAcceptedWithoutAdvancingPastItsBoundary()
    {
        byte[] frame = new byte[LoginDecodedFrameReader.MaximumFrameLength];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1055);
        byte[] combined = [.. frame, 0xCE];
        ReadOnlySequence<byte> buffer = new(combined);

        Assert.True(LoginDecodedFrameReader.TryRead(ref buffer, out ReadOnlySequence<byte> decoded));
        Assert.Equal(frame.Length, decoded.Length);
        Assert.Equal(new byte[] { 0xCE }, buffer.ToArray());
    }

    private static byte[] Seed(uint value)
    {
        byte[] frame = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1059);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), value);
        return frame;
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            Segment result = new(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = result;
            return result;
        }
    }
}
