using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;

namespace PacManBot.Services
{
    public class LoggingService
    {
        private readonly DiscordShardedClient client;
        private readonly CommandService commands;

        private string[] logExclude = null;

        public const string LogDirectory = "logs/";
        private string LogFile => $"{LogDirectory}{DateTime.Now.ToString("yyyy-MM-dd")}.txt";


        public LoggingService(DiscordShardedClient client, CommandService commands)
        {
            this.client = client;
            this.commands = commands;

            this.client.Log += Log;
            this.commands.Log += Log;
        }


        public void LoadLogExclude(StorageService storage)
        {
            logExclude = storage.BotContent["logexclude"]?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }


        public Task Log(LogMessage message)
        {
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory); //Create the log directory if it doesn't exist
            if (!File.Exists(LogFile)) File.WriteAllText(LogFile, ""); //Create today's log file if it doesn't exist

            if (logExclude != null && message.Message.ContainsAny(logExclude)) return Task.CompletedTask;

            string logText = $"{DateTime.Now.ToString("hh:mm:ss")} [{message.Severity}] {message.Source}: {message.Exception?.ToString() ?? message.Message}";
            File.AppendAllText(LogFile, $"{logText}\n"); //Write the log text to a file

            return Console.Out.WriteLineAsync(logText); //Write the log text to the console
        }
        public Task Log(LogSeverity severity, string message) => Log(new LogMessage(severity, LogSource.Bot, message));
        public Task Log(LogSeverity severity, string source, string message) => Log(new LogMessage(severity, source, message));
    }
}
