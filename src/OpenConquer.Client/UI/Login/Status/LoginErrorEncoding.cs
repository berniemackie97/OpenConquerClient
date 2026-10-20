using System.Runtime.InteropServices;
using System.Text;

namespace OpenConquer.Client.UI.Login.Status;

internal static partial class LoginErrorEncoding
{
    // Flash's ANSI bridge uses the Windows system ACP, not CodePage.ini.
    // On other hosts the English retail profile explicitly selects 1252.
    internal static Encoding ForCurrentHost() => ForCodePage(OperatingSystem.IsWindows() ? checked((int)GetACP()) : 1252);

    internal static Encoding ForCodePage(int codePage) =>
        CodePagesEncodingProvider.Instance.GetEncoding(codePage) ?? Encoding.GetEncoding(codePage);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetACP();
}
