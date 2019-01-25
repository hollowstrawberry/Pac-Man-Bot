using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    /// <summary>
    /// Receives and logs messages from everywhere in the bot.
    /// </summary>
    public class LoggingService
    {
        private readonly LogSeverity logLevel;
        private readonly string[] hardExclusions;

        private const int WriteAttempts = 10;
        public const string LogDirectory = "logs/";
        public string LogFile => $"{LogDirectory}{DateTime.Now:yyyy-MM-dd}.txt";


        public LoggingService(PmConfig config)
        {
            logLevel = config.logLevel;
            hardExclusions = config.logExclude;
        }


        /// <summary>Logs a message.</summary>
        public async Task LogAsync(string message, LogSeverity severity, string source = LogSource.Bot)
        {
            if (severity > logLevel || message.ContainsAny(hardExclusions)) return;

            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);
            if (!File.Exists(LogFile)) File.Create(LogFile).Dispose();

            string text = $"{DateTime.Now:hh:mm:ss}|{severity.ToString().ToUpperInvariant()}|{source}> {message}\n";
            await Console.Out.WriteAsync(text);
            await File.AppendAllTextAsync(LogFile, text);
        }


        /// <summary>Logs a message without caring for synchronization.</summary>
        public async void Log(string message, LogSeverity severity, string source = LogSource.Bot)
        {
            await LogAsync(message, severity, source); // This is bad, but fire-and-forget is better than nothing
        }


        /// <summary>Logs a message sent from a discord client.</summary>
        public Task ClientLog(LogMessage log)
        {
            var source = log.Source.Replace("Shard #", "Gateway");
            var msg = $"{log.Message}{log.Exception}";
            return LogAsync(msg, log.Severity, source);
        }


        /// <summary>Logs a message.</summary>
        public Task DebugAsync(string message, string source = LogSource.Bot)
            => LogAsync(message, LogSeverity.Debug, source);
        /// <summary>Logs a message.</summary>
        public Task VerboseAsync(string message, string source = LogSource.Bot)
            => LogAsync(message, LogSeverity.Verbose, source);
        /// <summary>Logs a message.</summary>
        public Task InfoAsync(string message, string source = LogSource.Bot)
            => LogAsync(message, LogSeverity.Info, source);
        /// <summary>Logs a message.</summary>
        public Task WarningAsync(string message, string source = LogSource.Bot)
            => LogAsync(message, LogSeverity.Warning, source);
        /// <summary>Logs a message.</summary>
        public Task ErrorAsync(string message, string source = LogSource.Bot)
            => LogAsync(message, LogSeverity.Error, source);
        /// <summary>Logs an exception.</summary>
        public Task ErrorAsync(Exception exception, string source = LogSource.Bot)
            => LogAsync(exception.ToString(), LogSeverity.Error, source);
        /// <summary>Logs a message.</summary>
        public Task FatalAsync(string message, string source = LogSource.Bot)
            => LogAsync(message, LogSeverity.Critical, source);

        /// <summary>Logs a message without caring for synchronization.</summary>
        public void Debug(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Debug, source);
        /// <summary>Logs a message without caring for synchronization.</summary>
        public void Verbose(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Verbose, source);
        /// <summary>Logs a message without caring for synchronization.</summary>
        public void Info(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Info, source);
        /// <summary>Logs a message without caring for synchronization.</summary>
        public void Warning(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Warning, source);
        /// <summary>Logs a message without caring for synchronization.</summary>
        public void Error(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Error, source);
        /// <summary>Logs an exception without caring for synchronization.</summary>
        public void Error(Exception exception, string source = LogSource.Bot)
            => Log(exception.ToString(), LogSeverity.Error, source);
    }
}
