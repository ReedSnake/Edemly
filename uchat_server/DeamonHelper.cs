using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

public static class DaemonHelper
{
    [DllImport("libc")]
    private static extern int fork();

    [DllImport("libc")]
    private static extern int setsid();

    [DllImport("libc")]
    private static extern int chdir(string path);

    [DllImport("libc")]
    private static extern int close(int fd);

    public static void Daemonize()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return; 

        int pid = fork();
        if (pid > 0)
        {
            Environment.Exit(0);
        }

        setsid();

        close(0);
        close(1);
        close(2);

        var logStream = new FileStream("/var/log/uchat_server.log", FileMode.Append, FileAccess.Write);
        var writer = new StreamWriter(logStream) { AutoFlush = true };
        Console.SetOut(writer);
        Console.SetError(writer);

        chdir("/");
    }
}

