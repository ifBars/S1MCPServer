using MelonLoader;
using System;

namespace S1MCPServer.Utils
{
    public static class ModLogger 
    {
        public static void Info(string message)
        {
            MelonLogger.Msg(message);
        }

        public static void Debug(string message)
        {
            // Debug logging is always enabled for development
            MelonLogger.Msg($"[DEBUG] {message}");
        }

        public static void Error(string message)
        {
            MelonLogger.Msg($"[ERROR] {message}");
        }

        public static void Error(string message, Exception exception)
        {
            MelonLogger.Error($"{message}: {exception.Message}");
            MelonLogger.Error($"Stack trace: {exception.StackTrace}");
        }
        
        public static void Warn(string message)
        {
            MelonLogger.Warning(message);
        }
    }
}