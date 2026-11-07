using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace OpenConquer.Client.Startup.LoginFlow;

/// <summary>
/// Resolves the local IPv4 address the 5517 client reports in 1037. Native <c>sub_66df97</c> runs
/// <c>gethostbyname(gethostname())</c> when it connects to the account server (0x4AF266) and keeps the last returned
/// address as its raw in_addr dword (0x66E05A); a failed lookup leaves the previous value. Evidence: 20_notes
/// Network/bootstrap-tolerance-cp24d.md.
/// </summary>
internal sealed class LocalAddressResolver(Func<CancellationToken, Task<IPAddress[]>>? lookup = null)
{
    private readonly Func<CancellationToken, Task<IPAddress[]>> _lookup = lookup ??
        (static cancellationToken => Dns.GetHostAddressesAsync(Dns.GetHostName(), AddressFamily.InterNetwork, cancellationToken));

    internal uint Current
    {
        get;
        private set;
    }

    internal async Task<uint> RefreshAsync(CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await _lookup(cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return Current;
        }

        for (int index = 0; index < addresses.Length; index++)
        {
            if (addresses[index].AddressFamily == AddressFamily.InterNetwork)
            {
                Current = BinaryPrimitives.ReadUInt32LittleEndian(addresses[index].GetAddressBytes());
            }
        }

        return Current;
    }
}
