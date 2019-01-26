using Serilog;
using Serilog.Core;
using System;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    /// <summary>
    /// Receives and logs messages from everywhere in the bot.
    /// </summary>
    public class LoggingService : IDisposable
    {
        private readonly Logger logger;
        private readonly LogSeverity logLevel;
        private readonly string[] hardExclusions;


        public LoggingService(PmConfig config)
        {
            logLevel = config.logLevel;
            hardExclusions = config.logExclude;

            const string template = "{Timestamp:HH:mm:ss}|{Level:u3}|{Message}{NewLine}";

            logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: template)
                .WriteTo.RollingFile("logs/{Date}.txt", outputTemplate: template)
                .CreateLogger();
        }


        /// <summary>Logs a message.</summary>
        public void Log(string message, LogSeverity severity, string source = LogSource.Bot)
        {
            if (severity > logLevel || message.ContainsAny(hardExclusions)) return;
            logger.Write(severity.ToSerilog(), $"{source}> {message}");
        }


        /// <summary>Logs an exception. Connection-related exceptions will be treated as warnings,
        /// while any other exception will be treated as an error.</summary>
        public void Exception(string message, Exception e, string source = LogSource.Bot)
        {
            if (e is HttpException || e is TimeoutException)
            {
                Warning($"{message}: {e.GetType()}: {e.Message}", source);
            }
            else
            {
                Error($"{message}: {e}", source);
            }
        }


        /// <summary>Logs a message sent from a discord client.</summary>
        public Task ClientLog(LogMessage log)
        {
            string source = log.Source.Replace("Shard #", "Gateway");

            // To the purposes of this bot, these warnings are expected and thus serve no purpose
            if (log.Severity == LogSeverity.Warning)
            {
                if (log.Source == "Rest")
                {
                    // The library should handle common ratelimits (Note: Most requests use the option RetryRatelimit)
                    // If ignoring them proves to be a bad idea (it hasn't so far), it would be needed to
                    // manually implement ratelimit handling for commands and games.
                    if (log.Message.StartsWith("Rate limit triggered")) return Task.CompletedTask;
                }
                else if (log.Source.StartsWith("Gateway"))
                {
                    // Unknown object events that are irrelevant.
                    // These include user voice events and leaving guilds.
                    if (log.Message.StartsWith("Unknown")) return Task.CompletedTask; // Unknown objects 
                }
            }

            Log($"{log.Message}{log.Exception}", log.Severity, source);
            return Task.CompletedTask;
        }


        /// <summary>Logs a message.</summary>
        public void Debug(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Debug, source);

        /// <summary>Logs a message.</summary>
        public void Verbose(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Verbose, source);

        /// <summary>Logs a message.</summary>
        public void Info(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Info, source);

        /// <summary>Logs a message.</summary>
        public void Warning(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Warning, source);

        /// <summary>Logs a message.</summary>
        public void Error(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Error, source);

        /// <summary>Logs a message.</summary>
        public void Fatal(string message, string source = LogSource.Bot)
            => Log(message, LogSeverity.Critical, source);


        /// <summary>Release all resources used for logging.</summary>
        public void Dispose()
        {
            logger.Dispose();
        }
    }
}
