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

            const string template = "{Timestamp:HH:mm:ss}|{Level:u3}|{Message}{NewLine}";

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
            logger.Write((Serilog.Events.LogEventLevel)logLevel, $"{LogSource.Client}> {message}");
        }

        /// <summary>Logs a message.</summary>
        public void Log(string message, LogLevel logLevel, string source = LogSource.Bot)
        {
            if (logLevel < minLogLevel || message.ContainsAny(hardExclusions)) return;
            logger.Write((Serilog.Events.LogEventLevel)logLevel, $"{source}> {message}");
        }


        /// <summary>Logs an exception. Connection-related exceptions will be treated as warnings,
        /// while more important exceptions will be treated as an error.</summary>
        public void Exception(string message, Exception e, string source = LogSource.Bot)
        {
            if (message != null) message += " - ";

            if (e is ServerErrorException || e is RateLimitException
                || e is TimeoutException || e is OperationCanceledException)
            {
                Warning($"{message}{e.GetType()}: {e.Message}", source);
            }
            else
            {
                Error($"{message}{e}", source); // Full stacktrace
            }
        }


        /// <summary>Logs a message.</summary>
        public void Debug(string message, string source = LogSource.Bot)
            => Log(message, LogLevel.Debug, source);

        /// <summary>Logs a message.</summary>
        public void Verbose(string message, string source = LogSource.Bot)
            => Log(message, LogLevel.Debug, source);

        /// <summary>Logs a message.</summary>
        public void Info(string message, string source = LogSource.Bot)
            => Log(message, LogLevel.Information, source);

        /// <summary>Logs a message.</summary>
        public void Warning(string message, string source = LogSource.Bot)
            => Log(message, LogLevel.Warning, source);

        /// <summary>Logs a message.</summary>
        public void Error(string message, string source = LogSource.Bot)
            => Log(message, LogLevel.Error, source);

        /// <summary>Logs a message.</summary>
        public void Fatal(string message, string source = LogSource.Bot)
            => Log(message, LogLevel.Critical, source);


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
