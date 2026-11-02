using Avalonia;

namespace OpenConquerStudio;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: OpenConquerStudio [project-directory]");
            return 2;
        }

        return AppBuilder.Configure<StudioApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
