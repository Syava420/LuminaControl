using System;
using System.IO;

namespace LuminaControl
{
    public static class Logger
    {
        private static readonly object _logLock = new object();
        private static readonly string LogPath = @"C:\Users\Neuron\Desktop\LuminaControl_Log.txt";

        public static void Init()
        {
            try
            {
                lock (_logLock)
                {
                    File.WriteAllText(LogPath, string.Format("=== LOG STARTED: {0} ===\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                }
            }
            catch {}
        }

        public static void Log(string message)
        {
            try
            {
                lock (_logLock)
                {
                    string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    File.AppendAllText(LogPath, string.Format("[{0}] {1}\r\n", time, message));
                }
            }
            catch {}
        }
    }
}
