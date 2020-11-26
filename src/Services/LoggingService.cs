using System;
using System.Net.WebSockets;
using DSharpPlus.Exceptions;
using Emzi0767.Utilities;
using Microsoft.Extensions.Logging;
using PacManBot.Extensions;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PacManBot.Services
{
    /// <summary>
    /// Receives and logs messages from everywhere in the bot.
    /// </summary>
    public class LoggingService : ILogger, ILoggerFactory, ILoggerProvider, IDisposable
    {
        private readonly Serilog.Core.Logger _logger;
        private readonly LogLevel _minLogLevel;
        private readonly LogLevel _minClientLogLevel;
        private readonly string[] _hardExclusions;


        public LoggingService(PmBotConfig config)
        {
            _minLogLevel = config.logLevel;
            _minClientLogLevel = config.clientLogLevel;
            _hardExclusions = config.logExclude;

            _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: config.logTemplate)
                .WriteTo.File("logs/.txt", rollingInterval: RollingInterval.Day, outputTemplate: config.logTemplate)
                .CreateLogger();
        }


        /// <summary>Logs a message.</summary>
        public void Log(string message, LogLevel logLevel)
        {
            if (logLevel < _minLogLevel || message.ContainsAny(_hardExclusions)) return;
            _logger.Write((LogEventLevel)logLevel, message);
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




        void ILogger.Log<TState>(LogLevel level, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (level < _minClientLogLevel) return;
            var message = state.ToString();

            level = eventId.Name switch
            {
                "Startup" or "CommandsNext" => LogLevel.Trace,
                "ConnectionClose" => LogLevel.Information,
                "RatelimitPreemptive" => LogLevel.Debug,
                "RatelimitHit" => LogLevel.Warning,
                null when level < LogLevel.Warning => LogLevel.Trace,
                _ => level,
            };

            if (exception is not null && level >= LogLevel.Warning) Exception(message, exception);
            else Log(message, level);
        }

        bool ILogger.IsEnabled(LogLevel logLevel) => logLevel >= _minLogLevel;
        IDisposable ILogger.BeginScope<TState>(TState state) => this;

        void ILoggerFactory.AddProvider(ILoggerProvider provider) { }
        ILogger ILoggerFactory.CreateLogger(string categoryName) => this;
        ILogger ILoggerProvider.CreateLogger(string categoryName) => this;

        void IDisposable.Dispose()
        {
            _logger.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
