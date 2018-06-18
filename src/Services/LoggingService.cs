using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    public class LoggingService
    {
        private readonly DiscordShardedClient client;
        private readonly CommandService commands;

        private string[] logExclude = null;

        private const int WriteAttempts = 1000;
        public const string LogDirectory = "logs/";
        public string LogFile => $"{LogDirectory}{DateTime.Now.ToString("yyyy-MM-dd")}.txt";


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
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);
            if (!File.Exists(LogFile)) File.Create(LogFile).Dispose();

            if (logExclude != null && ((message.Message?.ContainsAny(logExclude) ?? false)
                || (message.Exception?.ToString().ContainsAny(logExclude) ?? false)))
            {
                return Task.CompletedTask;
            }

            string logText = $"{DateTime.Now.ToString("hh:mm:ss")} [{message.Severity}] " +
                             $"{message.Source.Replace("Shard #", "Gateway")}: {message.Message}{message.Exception}";

            for (int i = 0; i <= WriteAttempts; ++i)
            {
                try
                {
                    File.AppendAllTextAsync(LogFile, $"{logText}\n"); //Write the log text to a file
                    break;
                }
                catch (IOException) when (i < WriteAttempts) { Task.Delay(2); }
            }

            return Console.Out.WriteLineAsync(logText); //Write the log text to the console
        }
        public Task Log(LogSeverity severity, string message) => Log(new LogMessage(severity, LogSource.Bot, message));
        public Task Log(LogSeverity severity, string source, string message) => Log(new LogMessage(severity, source, message));
    }
}
