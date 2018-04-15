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
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;

        public const string LogDirectory = "logs/";
        private string LogFile => $"{LogDirectory}{DateTime.Now.ToString("yyyy-MM-dd")}.txt";


        //DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient client, CommandService commands)
        {
            this.client = client;
            this.commands = commands;

            this.client.Log += Log;
            this.commands.Log += Log;
        }


        public Task Log(LogMessage message)
        {
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory); //Create the log directory if it doesn't exist
            if (!File.Exists(LogFile)) File.Create(LogFile).Dispose(); //Create today's log file if it doesn't exist

            string logText = $"{DateTime.Now.ToString("hh:mm:ss")} [{message.Severity}] {message.Source}: {message.Exception?.ToString() ?? message.Message}";
            File.AppendAllText(LogFile, $"{logText}\n"); //Write the log text to a file

            if (message.Severity.ToString() == "Verbose" && message.Source == "Rest") return Task.CompletedTask;
            else return Console.Out.WriteLineAsync(logText); //Write the log text to the console
        }
        public Task Log(LogSeverity severity, string message) => Log(new LogMessage(severity, "Bot", message));
        public Task Log(LogSeverity severity, string source, string message) => Log(new LogMessage(severity, source, message));
    }
}
