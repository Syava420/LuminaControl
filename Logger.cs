using System;
using System.IO;

namespace LuminaControl;

public static class Logger
{
    private static readonly object LogLock = new();
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LuminaControl_Log.txt");

    public static void Init()
    {
        try
        {
            lock (LogLock)
            {
                File.WriteAllText(LogPath, $"=== LOG STARTED: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n");
            }
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            lock (LogLock)
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
            }
        }
        catch { }
    }
}
