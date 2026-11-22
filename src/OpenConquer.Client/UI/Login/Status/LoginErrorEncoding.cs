using System.Runtime.InteropServices;
using System.Text;

namespace OpenConquer.Client.UI.Login.Status;

internal static partial class LoginErrorEncoding
{
    internal static Encoding ForCurrentHost()
    {
        return ForCodePage(OperatingSystem.IsWindows() ? checked((int)GetACP()) : 1252);
    }

    internal static Encoding ForCodePage(int codePage)
    {
        return CodePagesEncodingProvider.Instance.GetEncoding(codePage) ?? Encoding.GetEncoding(codePage);
    }

    [LibraryImport("kernel32.dll")]
    private static partial uint GetACP();
}
