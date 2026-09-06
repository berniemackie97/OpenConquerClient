using System.IO.Pipes;
using System.Security.Principal;

namespace OpenConquer.Launcher.Instances;

/// <summary>Local activation only. This protocol is not a game-login or client-handoff channel.</summary>
internal static class LauncherActivationProtocol
{
    // Magic, version 1, activate command, two reserved zero bytes. No variable-length fields.
    private static readonly byte[] s_request = [0x4f, 0x43, 0x4c, 0x41, 1, 1, 0, 0];
    public const int RequestLength = 8;
    public const byte Accepted = 1;
    public const byte Unavailable = 2;

    public static bool IsActivationRequest(ReadOnlySpan<byte> request) => request.SequenceEqual(s_request);

    public static async Task<bool> TryActivateAsync(string pipeName, CancellationToken cancellationToken)
    {
        using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Anonymous);
        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await client.WriteAsync(s_request, cancellationToken).ConfigureAwait(false);
            byte[] response = new byte[1];
            await client.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
            return response[0] == Accepted;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
