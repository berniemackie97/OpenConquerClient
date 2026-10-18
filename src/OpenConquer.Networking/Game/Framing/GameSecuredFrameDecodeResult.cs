namespace OpenConquer.Networking.Game.Framing;

internal enum GameSecuredFrameDecodeStatus
{
    NeedMoreData = 0,
    Success,
    InvalidPacketLength,
    InvalidSignature,
}

/// <summary>
/// Consumed bytes exclude any following wire frames in the same input span.
/// On success, the caller takes exclusive ownership of Frame until disposal.
/// </summary>
internal readonly record struct GameSecuredFrameDecodeResult(
    GameSecuredFrameDecodeStatus Status, int BytesConsumed, GameSecuredInboundFrame? Frame);
