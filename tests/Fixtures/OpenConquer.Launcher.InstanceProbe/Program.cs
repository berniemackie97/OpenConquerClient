using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.InstanceProbe;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            return 64;
        }

        string pipe;
        try
        {
            pipe = args[2] == "@current-user" ? LauncherInstanceIdentity.ForCurrentUser().PipeName : args[2];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(exception.GetType().Name);
            return 65;
        }
        if (args[0] == "resolve")
        {
            Console.WriteLine(pipe);
            return 0;
        }

        using LauncherProcessLease lease = new(args[1]);
        if (args[0] is "activate" or "wait")
        {
            if (args[0] == "wait")
            {
                Console.WriteLine("checking");
            }

            LauncherInstanceAdmission result = LauncherInstanceStartup.Enter(lease, pipe);
            Console.WriteLine(result);
            return result == LauncherInstanceAdmission.ActivatedExisting ? 0 : 2;
        }

        if (!lease.TryAcquire())
        {
            Console.WriteLine("busy");
            return 3;
        }

        LauncherActivationServer? server = null;
        try
        {
            if (args[0] == "listen")
            {
                server = new LauncherActivationServer(pipe);
                _ = server.RunAsync(_ =>
                {
                    Console.WriteLine("activated");
                    return Task.FromResult(true);
                });
            }
            else if (args[0] != "hold")
            {
                return 64;
            }

            Console.WriteLine("ready");
            Console.ReadLine();
            return 0;
        }
        finally
        {
            server?.StopAsync().GetAwaiter().GetResult();
        }
    }
}
