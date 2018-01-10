using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PacManBot.Services
{
    public class LoggingService
    {
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;

        private string logDirectory { get; }
        private string logFile => Path.Combine(logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");

        //DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

            this.discord = discord;
            this.commands = commands;

            this.discord.Log += OnLogAsync;
            this.commands.Log += OnLogAsync;
        }


        private Task OnLogAsync(LogMessage message)
        {
            if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory); //Create the log directory if it doesn't exist
            if (!File.Exists(logFile)) File.Create(logFile).Dispose(); //Create today's log file if it doesn't exist

            string logText = $"{DateTime.UtcNow.ToString("hh:mm:ss")} [{message.Severity}] {message.Source}: {message.Exception?.ToString() ?? message.Message}";
            File.AppendAllText(logFile, logText + "\n"); //Write the log text to a file


            if (message.Severity.ToString() == "Verbose" && message.Source == "Rest") return Task.CompletedTask;
            else return Console.Out.WriteLineAsync(logText); //Write the log text to the console
        }
    }
}
