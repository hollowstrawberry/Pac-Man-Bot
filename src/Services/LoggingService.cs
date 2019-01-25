using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
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

        private const int WriteAttempts = 10;
        public const string LogDirectory = "logs/";
        public string LogFile => $"{LogDirectory}{DateTime.Now:yyyy-MM-dd}.txt";


        public LoggingService(PmConfig config)
        {
            logExclude = config.logExclude;
        }


        /// <summary>Logs an entry to the console and to disk.</summary>
        public async Task Log(LogSeverity severity, string source, string message)
        {
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);
            if (!File.Exists(LogFile)) File.Create(LogFile).Dispose();

            if (message.ContainsAny(logExclude)) return;

            string logText = $"{DateTime.Now:hh:mm:ss} [{severity}] {source}: {message}\n";

            await Console.Out.WriteAsync(logText);

            for (int i = 0; i <= WriteAttempts; ++i)
            {
                try
                {
                    await File.AppendAllTextAsync(LogFile, logText);
                    break;
                }
                catch (IOException) when (i < WriteAttempts) { await Task.Delay(10); }
            }
        }

        /// <summary>Logs an entry to the console and to disk, using the default source name.</summary>
        public Task Log(LogSeverity severity, string message) => Log(severity, LogSource.Bot, message);

        /// <summary>Logs an entry to the console and to disk, using the default severity.</summary>
        public Task Log(string source, string message) => Log(LogSeverity.Verbose, source, message);

        /// <summary>Logs an entry to the console and to disk, using the default severity and source name.</summary>
        public Task Log(string message) => Log(LogSeverity.Verbose, LogSource.Bot, message);


        /// <summary>Logs an entry to the console and to disk, hooked from a discord client.</summary>
        public Task ClientLog(LogMessage log)
        {
            return Log(log.Severity, log.Source.Replace("Shard #", "Gateway"), $"{log.Message}{log.Exception}");
        }
    }
}
