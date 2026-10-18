using System.Buffers;
using System.Buffers.Binary;
using OpenConquer.Networking.Game.Cryptography;

namespace OpenConquer.Networking.Game.Framing;

/// <summary>
/// Incremental server-to-client secured frame decoder. Consumes no bytes beyond
/// one complete frame and preserves inbound CFB-64 feedback across fragments.
/// The client owns each returned frame and must dispose it.
/// </summary>
internal sealed class GameSecuredFrameDecoder : IDisposable
{
    private readonly Lock _gate = new();
    private readonly GameSessionCipher _sessionCipher;

    private byte[]? _buffer;
    private int _bufferedLength;
    private int _packetLength;
    private int _expectedWireLength;
    private bool _faulted;
    private bool _disposed;

    internal GameSecuredFrameDecoder(GameSessionCipher sessionCipher)
    {
        _sessionCipher = sessionCipher ?? throw new ArgumentNullException(nameof(sessionCipher));
    }

    internal GameSecuredFrameDecodeResult Decode(ReadOnlySpan<byte> encrypted)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_faulted)
            {
                throw new InvalidOperationException("The secured frame decoder is terminal after a framing failure.");
            }

            if (encrypted.IsEmpty)
            {
                return new GameSecuredFrameDecodeResult(
                    GameSecuredFrameDecodeStatus.NeedMoreData, 0, null);
            }

            byte[] buffer = _buffer ??= ArrayPool<byte>.Shared.Rent(
                GameSecuredFrameProtocol.MaximumWireLength);
            int consumed = 0;

            try
            {
                if (_bufferedLength < GameSecuredFrameProtocol.HeaderLength)
                {
                    int headerBytes = Math.Min(
                        GameSecuredFrameProtocol.HeaderLength - _bufferedLength,
                        encrypted.Length);

                    _sessionCipher.DecryptInbound(
                        encrypted[..headerBytes], buffer.AsSpan(_bufferedLength, headerBytes));
                    _bufferedLength += headerBytes;
                    consumed += headerBytes;

                    if (_bufferedLength < GameSecuredFrameProtocol.HeaderLength)
                    {
                        return new GameSecuredFrameDecodeResult(
                            GameSecuredFrameDecodeStatus.NeedMoreData, consumed, null);
                    }

                    int packetLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
                    if (packetLength is < GameSecuredFrameProtocol.HeaderLength or
                        > GameSecuredFrameProtocol.MaximumPacketLength)
                    {
                        return Fail(GameSecuredFrameDecodeStatus.InvalidPacketLength, consumed);
                    }

                    _packetLength = packetLength;
                    _expectedWireLength = packetLength + GameSecuredFrameProtocol.SignatureLength;
                }

                int toRead = Math.Min(_expectedWireLength - _bufferedLength,
                    encrypted.Length - consumed);
                if (toRead > 0)
                {
                    _sessionCipher.DecryptInbound(encrypted.Slice(consumed, toRead),
                        buffer.AsSpan(_bufferedLength, toRead));
                    _bufferedLength += toRead;
                    consumed += toRead;
                }

                if (_bufferedLength != _expectedWireLength)
                {
                    return new GameSecuredFrameDecodeResult(
                        GameSecuredFrameDecodeStatus.NeedMoreData, consumed, null);
                }

                if (!buffer.AsSpan(_packetLength, GameSecuredFrameProtocol.SignatureLength)
                    .SequenceEqual(GameSecuredFrameProtocol.ServerSignature))
                {
                    return Fail(GameSecuredFrameDecodeStatus.InvalidSignature, consumed);
                }

                GameSecuredInboundFrame frame = new(buffer, _packetLength);
                _buffer = null;
                ResetFrameState();
                return new GameSecuredFrameDecodeResult(
                    GameSecuredFrameDecodeStatus.Success, consumed, frame);
            }
            catch
            {
                _faulted = true;
                ReleaseBuffer();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseBuffer();
        }
    }

    private GameSecuredFrameDecodeResult Fail(GameSecuredFrameDecodeStatus status, int consumed)
    {
        _faulted = true;
        ReleaseBuffer();
        return new GameSecuredFrameDecodeResult(status, consumed, null);
    }

    private void ReleaseBuffer()
    {
        byte[]? buffer = _buffer;
        _buffer = null;
        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        ResetFrameState();
    }

    private void ResetFrameState()
    {
        _bufferedLength = 0;
        _packetLength = 0;
        _expectedWireLength = 0;
    }
}
