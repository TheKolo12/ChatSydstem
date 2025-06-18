using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatSydstem
{
    public static class ChatLogger
    {
        public static void LogMessage(Player sender, string message)
        {
            string folderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "Configs", "Plugins", "ChatSystem", "TextLog"
            );

            string logFilePath = Path.Combine(folderPath, "log.txt");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Log.Info("DirectoryLog Created");
            }

            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {sender.Nickname} ({sender.UserId}) sent: \"{message}\"{Environment.NewLine}";

            File.AppendAllText(logFilePath, logEntry);
        }
    }
}
