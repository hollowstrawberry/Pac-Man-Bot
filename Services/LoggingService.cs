using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PacManBot.Services
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;

        private string logDirectory { get; }
        private string logFile => Path.Combine(logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");

        //DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient client, CommandService commands)
        {
            logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

            _client = client;
            _commands = commands;

            _client.Log += Log;
            _commands.Log += Log;
        }


        public Task Log(LogMessage message)
        {
            if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory); //Create the log directory if it doesn't exist
            if (!File.Exists(logFile)) File.Create(logFile).Dispose(); //Create today's log file if it doesn't exist

            string logText = $"{DateTime.UtcNow.ToString("hh:mm:ss")} [{message.Severity}] {message.Source}: {message.Exception?.ToString() ?? message.Message}";
            File.AppendAllText(logFile, $"{logText}\n"); //Write the log text to a file

            if (message.Severity.ToString() == "Verbose" && message.Source == "Rest") return Task.CompletedTask;
            else return Console.Out.WriteLineAsync(logText); //Write the log text to the console
        }
        public Task Log(LogSeverity severity, string message) => Log(new LogMessage(severity, "Bot", message));
        public Task Log(LogSeverity severity, string source, string message) => Log(new LogMessage(severity, source, message));
    }
}
