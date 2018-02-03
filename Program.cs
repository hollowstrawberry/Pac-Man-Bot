using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PacManBot.Services;
using PacManBot.Constants;


//Made by Samrux for fun
//GitHub repo: https://github.com/Samrux/Pac-Man-Bot


namespace PacManBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private LoggingService _logger;
        private IConfigurationRoot _botConfig;

        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            var configBuilder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile(BotFile.Config); //Add the configuration file
            _botConfig = configBuilder.Build(); //Build the configuration file

            //Client and its configuration
            if (!Int32.TryParse(_botConfig["messagecachesize"], out int cacheSize)) cacheSize = 100;
            var clientConfig = new DiscordSocketConfig { LogLevel = LogSeverity.Verbose, MessageCacheSize = cacheSize, /*WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance*/ }; //Specify websocketprovider to run properly in Windows 7
            _client = new DiscordSocketClient(clientConfig);

            //Prepare services
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(new CommandService(new CommandServiceConfig{ DefaultRunMode = RunMode.Async, LogLevel = LogSeverity.Verbose }))
                .AddSingleton<CommandHandler>()
                .AddSingleton<ReactionHandler>()
                .AddSingleton<LoggingService>()
                .AddSingleton<StartupService>()
                .AddSingleton(_botConfig);

            var provider = services.BuildServiceProvider();

            //Initialize services
            _logger = provider.GetRequiredService<LoggingService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<ReactionHandler>();

            //Events
            _client.Ready += async () => await UpdatePlaying(); //Updates playing message when ready or when changing guild count
            _client.JoinedGuild += async (arg) => await UpdatePlaying();
            _client.LeftGuild += async (arg) => await UpdatePlaying();


            await Task.Delay(-1); //Prevent the application from closing
        }

        private async Task UpdatePlaying()
        {
            int guilds = _client.Guilds.Count;
            await _client.SetGameAsync($"{_botConfig["prefix"]}help | {guilds} guilds");
            await _logger.Log(LogSeverity.Info, $"Updated guilds: {guilds}");
            if (!string.IsNullOrEmpty(_botConfig["httptoken"])) await UpdateServerGuildCount(guilds);
        }

        private Task UpdateServerGuildCount(int count)
        {
            var request = (HttpWebRequest)WebRequest.Create($"https://bots.discord.pw/api/bots/{_client.CurrentUser.Id}/stats");
            request.ContentType = "application/json";
            request.Method = "POST";
            request.Headers.Add("Authorization", _botConfig["httptoken"]);

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write($"{{\n\"server_count\": {count}\n}}");
                streamWriter.Flush();
                streamWriter.Close();
            }

            var response = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                return _logger.Log(LogSeverity.Verbose, $"Sent server count to server. {streamReader.ReadToEnd()}");
            }
        }
    }
}
