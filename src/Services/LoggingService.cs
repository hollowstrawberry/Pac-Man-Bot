using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    /// <summary>
    /// Receives and logs messages to the console and on disk, from everywhere in the bot.
    /// </summary>
    public class LoggingService
    {
        private readonly string[] logExclude;

        private const int WriteAttempts = 1000;
        public const string LogDirectory = "logs/";
        public string LogFile => $"{LogDirectory}{DateTime.Now:yyyy-MM-dd}.txt";


        public LoggingService(BotConfig config, DiscordShardedClient client, CommandService commands)
        {
            logExclude = config.logExclude;

            client.Log += Log;
            commands.Log += Log;
        }



        /// <summary>Logs an entry to the console and to disk.</summary>
        public Task Log(LogMessage message)
        {
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);
            if (!File.Exists(LogFile)) File.Create(LogFile).Dispose();

            if (logExclude != null &&
                ((message.Message?.ContainsAny(logExclude) ?? false)
                || (message.Exception?.ToString().ContainsAny(logExclude) ?? false)))
            {
                return Task.CompletedTask;
            }

            string logText = $"{DateTime.Now:hh:mm:ss} [{message.Severity}] " +
                             $"{message.Source.Replace("Shard #", "Gateway")}: {message.Message}{message.Exception}";

            for (int i = 0; i <= WriteAttempts; ++i)
            {
                try
                {
                    File.AppendAllTextAsync(LogFile, $"{logText}\n"); // Write the log text to a file
                    break;
                }
                catch (IOException) when (i < WriteAttempts) { Task.Delay(2); }
            }

            return Console.Out.WriteLineAsync(logText); // Write the log text to the console
        }

        /// <summary>Logs an entry to the console and to disk.</summary>
        public Task Log(LogSeverity severity, string message) => Log(new LogMessage(severity, LogSource.Bot, message));

        /// <summary>Logs an entry to the console and to disk.</summary>
        public Task Log(LogSeverity severity, string source, string message) => Log(new LogMessage(severity, source, message));
    }
}
