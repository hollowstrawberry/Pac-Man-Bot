using Serilog;
using Serilog.Core;
using System;
using DSharpPlus;
using PacManBot.Constants;
using PacManBot.Extensions;
using Microsoft.Extensions.Logging;
using DSharpPlus.Exceptions;

namespace PacManBot.Services
{
    /// <summary>
    /// Receives and logs messages from everywhere in the bot.
    /// </summary>
    public class LoggingService : ILogger<DiscordShardedClient>, IDisposable
    {
        public static LoggingService Instance { get; set; }

        private readonly Logger logger;
        private readonly LogLevel minLogLevel;
        private readonly LogLevel minClientLogLevel;
        private readonly string[] hardExclusions;


        public LoggingService(PmBotConfig config)
        {
            minLogLevel = config.logLevel;
            minClientLogLevel = config.clientLogLevel;
            hardExclusions = config.logExclude;

            const string template = "{Timestamp:HH:mm:ss}|{Level:u3}> {Message}{NewLine}";

            logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: template)
                .WriteTo.RollingFile("logs/{Date}.txt", outputTemplate: template)
                .CreateLogger();
        }


        /// <summary>Logs a message. Used by the Discord client.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel < minClientLogLevel) return;
            var message = formatter(state, exception);
            if (message.ContainsAny(hardExclusions)) return;
            logger.Write((Serilog.Events.LogEventLevel)logLevel, message);
        }

        /// <summary>Logs a message.</summary>
        public void Log(string message, LogLevel logLevel)
        {
            if (logLevel < minLogLevel || message.ContainsAny(hardExclusions)) return;
            logger.Write((Serilog.Events.LogEventLevel)logLevel, message);
        }


        /// <summary>Logs an exception. Connection-related exceptions will be treated as warnings,
        /// while more important exceptions will be treated as an error.</summary>
        public void Exception(string message, Exception e)
        {
            if (message != null) message += " - ";

            if (e is ServerErrorException || e is RateLimitException
                || e is TimeoutException || e is OperationCanceledException)
            {
                Warning($"{message}{e.GetType()}: {e.Message}");
            }
            else
            {
                Error($"{message}{e}"); // Full stacktrace
            }
        }


        /// <summary>Logs a message.</summary>
        public void Verbose(string message)
            => Log(message, LogLevel.Debug);

        /// <summary>Logs a message.</summary>
        public void Info(string message)
            => Log(message, LogLevel.Information);

        /// <summary>Logs a message.</summary>
        public void Warning(string message)
            => Log(message, LogLevel.Warning);

        /// <summary>Logs a message.</summary>
        public void Error(string message)
            => Log(message, LogLevel.Error);

        /// <summary>Logs a message.</summary>
        public void Fatal(string message)
            => Log(message, LogLevel.Critical);


        /// <summary>Release all resources used for logging.</summary>
        public void Dispose()
        {
            logger.Dispose();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= minLogLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }
    }
}
