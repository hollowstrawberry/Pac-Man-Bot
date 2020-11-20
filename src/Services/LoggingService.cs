using Serilog;
using Serilog.Core;
using System;
using DSharpPlus;
using PacManBot.Extensions;
using Microsoft.Extensions.Logging;
using DSharpPlus.Exceptions;
using Emzi0767.Utilities;
using System.Net.WebSockets;

namespace PacManBot.Services
{
    /// <summary>
    /// Receives and logs messages from everywhere in the bot.
    /// </summary>
    public class LoggingService : ILogger<DiscordShardedClient>, IDisposable
    {
        private readonly Logger _logger;
        private readonly LogLevel _minLogLevel;
        private readonly LogLevel _minClientLogLevel;
        private readonly string[] _hardExclusions;


        public LoggingService(PmBotConfig config)
        {
            _minLogLevel = config.logLevel;
            _minClientLogLevel = config.clientLogLevel;
            _hardExclusions = config.logExclude;

            const string template = "{Timestamp:HH:mm:ss}|{Level:u3}> {Message}{NewLine}";

            _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: template)
                .WriteTo.RollingFile("logs/{Date}.txt", outputTemplate: template)
                .CreateLogger();
        }
        

        /// <summary>Logs a message. Used by the Discord client.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel < _minClientLogLevel) return;
            var message = formatter(state, exception);

            if (message.ContainsAny(_hardExclusions)) return;
            if (logLevel == LogLevel.Critical && message.Contains("reconnecting")) logLevel = LogLevel.Information; // hardcoding this one

            _logger.Write((Serilog.Events.LogEventLevel)logLevel, message);
        }

        /// <summary>Logs a message.</summary>
        public void Log(string message, LogLevel logLevel)
        {
            if (logLevel < _minLogLevel || message.ContainsAny(_hardExclusions)) return;
            _logger.Write((Serilog.Events.LogEventLevel)logLevel, message);
        }


        /// <summary>Logs an exception. Connection-related exceptions will be treated as warnings,
        /// while more important exceptions will be treated as an error.</summary>
        public void Exception(string message, Exception e)
        {
            if (e is AggregateException ae)
            {
                foreach (var ie in ae.InnerExceptions) Exception(message, ie);
                return;
            }

            if (e is ServerErrorException || e is RateLimitException || e is NotFoundException || e is WebSocketException
                || e.GetType().IsGeneric(typeof(AsyncEventTimeoutException<,>)))
            {
                Warning($"{message}{" - ".If(message is not null)}{e.GetType().Name}: {e.Message}");
            }
            else
            {
                Error($"{message}{" - ".If(message is not null)}{e}"); // Full stacktrace
            }
        }


        /// <summary>Logs a message.</summary>
        public void Debug(string message) => Log(message, LogLevel.Debug);

        /// <summary>Logs a message.</summary>
        public void Info(string message) => Log(message, LogLevel.Information);

        /// <summary>Logs a message.</summary>
        public void Warning(string message) => Log(message, LogLevel.Warning);

        /// <summary>Logs a message.</summary>
        public void Error(string message) => Log(message, LogLevel.Error);

        /// <summary>Logs a message.</summary>
        public void Critical(string message) => Log(message, LogLevel.Critical);


        /// <summary>Release all resources used for logging.</summary>
        public void Dispose()
        {
            _logger.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLogLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }
    }
}
