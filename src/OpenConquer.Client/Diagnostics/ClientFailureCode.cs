using System.Net.Sockets;
using System.Text.Json;

namespace OpenConquer.Client.Diagnostics;

internal enum ClientFailureCode
{
    Unknown = 0,
    FileMissing = 1,
    InvalidData = 2,
    AccessDenied = 3,
    Io = 4,
    Socket = 5,
    Timeout = 6,
    NativeLibrary = 7,
    Unsupported = 8,
    Canceled = 9,
    InvalidState = 10,
    InvalidArgument = 11,
}

internal static class ClientFailure
{
    internal static int Classify(Exception exception) => (int)(exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => ClientFailureCode.FileMissing,
        InvalidDataException or JsonException => ClientFailureCode.InvalidData,
        UnauthorizedAccessException => ClientFailureCode.AccessDenied,
        IOException => ClientFailureCode.Io,
        SocketException => ClientFailureCode.Socket,
        TimeoutException => ClientFailureCode.Timeout,
        DllNotFoundException or EntryPointNotFoundException or BadImageFormatException => ClientFailureCode.NativeLibrary,
        NotSupportedException => ClientFailureCode.Unsupported,
        OperationCanceledException => ClientFailureCode.Canceled,
        InvalidOperationException => ClientFailureCode.InvalidState,
        ArgumentException => ClientFailureCode.InvalidArgument,
        _ => ClientFailureCode.Unknown,
    });
}
