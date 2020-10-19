using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DiscordBotsList.Api;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using PacManBot.Commands;

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of Pac-Man Bot and handles its connection to Discord.
    /// </summary>
    public class PmBot
    {
        private readonly PmBotConfig config;
        private readonly IServiceProvider services;

        private readonly DiscordShardedClient client;
        private readonly LoggingService log;
        private readonly GameService games;
        private readonly InputService input;
        private readonly SchedulingService schedule;

        private AuthDiscordBotListApi discordBotList = null;
        private DateTime lastGuildCountUpdate = DateTime.MinValue;
        private int ready = 0;


        public PmBot(PmBotConfig config, IServiceProvider services, DiscordShardedClient client,
            LoggingService log, GameService games, InputService input, SchedulingService schedule)
        {
            this.config = config;
            this.services = services;
            this.client = client;
            this.log = log;
            this.games = games;
            this.input = input;
            this.schedule = schedule;
        }


        /// <summary>Starts the bot and its connection to Discord.</summary>
        public async Task StartAsync()
        {
            await client.UseCommandsNextAsync(new CommandsNextConfiguration
            {
                UseDefaultCommandHandler = false,
                Services = services,
            });
            foreach (var (shard, commands) in await client.GetCommandsNextAsync())
            {
                commands.RegisterCommands(typeof(BaseModule).Assembly);
                commands.SetHelpFormatter<HelpFormatter>();
            }

            await games.LoadGamesAsync();

            client.Ready += OnReady;
            client.ClientErrored += OnClientErrored;
            client.SocketErrored += OnSocketErrored;
            client.GuildCreated += OnJoinedGuild;
            client.GuildDeleted += OnLeftGuild;
            client.ChannelDeleted += OnChannelDeleted;

            schedule.PrepareRestart += StopAsync;

            await client.StartAsync();
            //await client.UpdateStatusAsync(
            //    new DiscordActivity("Booting up...", ActivityType.Playing), UserStatus.Idle, DateTime.Now);

            if (!string.IsNullOrWhiteSpace(config.discordBotListToken))
            {
                discordBotList = new AuthDiscordBotListApi(client.CurrentUser.Id, config.discordBotListToken);
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync()
        {
            await client.UpdateStatusAsync(userStatus: UserStatus.DoNotDisturb); // why not

            foreach (var shard in client.ShardClients.Values)
                input.StopListening(shard);

            schedule.StopTimers();

            client.Ready -= OnReady;
            client.ClientErrored -= OnClientErrored;
            client.SocketErrored -= OnSocketErrored;
            client.GuildCreated -= OnJoinedGuild;
            client.GuildDeleted -= OnLeftGuild;
            client.ChannelDeleted -= OnChannelDeleted;

            await Task.Delay(5_000); // Buffer time to finish up doing whatever

            await client.StopAsync();
        }




        private Task OnClientErrored(DiscordClient shard, ClientErrorEventArgs args)
        {
            log.Exception($"On {args.EventName} handler", args.Exception);
            return Task.CompletedTask;
        }


        private Task OnSocketErrored(DiscordClient shard, SocketErrorEventArgs args)
        {
            log.Exception($"On shard {shard.ShardId}", args.Exception);
            return Task.CompletedTask;
        }


        private Task OnReady(DiscordClient shard, ReadyEventArgs args)
        {
            _ = OnReadyAsync(shard).LogExceptions(log, $"On ready {shard.ShardId}");
            return Task.CompletedTask;
        }


        private async Task OnReadyAsync(DiscordClient shard)
        {
            input.StartListening(shard);

            if (++ready == shard.ShardCount)
            {
                schedule.StartTimers();
                await client.UpdateStatusAsync(
                    new DiscordActivity($"with you!", ActivityType.Playing), UserStatus.Online, DateTime.Now);
                log.Info($"All Shards ready");
            }

            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                    .Split("/").Select(ulong.Parse).ToArray();

                    var channel = await shard.GetChannelAsync(id[0]);
                    if (channel != null)
                    {
                        File.Delete(Files.ManualRestart);
                        var message = await channel.GetMessageAsync(id[1]);
                        if (message != null) await message.ModifyAsync(CustomEmoji.Check);
                        log.Info("Resumed after manual restart");
                    }
                }
                catch (Exception e)
                {
                    log.Exception("After manual restart", e);
                    if (File.Exists(Files.ManualRestart) && !(e is IOException)) File.Delete(Files.ManualRestart);
                }
            }
        }


        private Task OnJoinedGuild(DiscordClient shard, GuildCreateEventArgs args)
        {
            _ = UpdateGuildCountAsync().LogExceptions(log, "While updating guild count");
            return Task.CompletedTask;
        }


        private Task OnLeftGuild(DiscordClient shard, GuildDeleteEventArgs args)
        {
            foreach (var channel in args.Guild.Channels.Keys)
            {
                games.Remove(games.GetForChannel(channel));
            }

            _ = UpdateGuildCountAsync().LogExceptions(log, "While updating guild count");
            return Task.CompletedTask;
        }


        private Task OnChannelDeleted(DiscordClient shard, ChannelDeleteEventArgs args)
        {
            games.Remove(games.GetForChannel(args.Channel.Id));
            return Task.CompletedTask;
        }


        private async Task UpdateGuildCountAsync()
        {
            if (discordBotList == null || (DateTime.Now - lastGuildCountUpdate).TotalMinutes < 30.0) return;

            int guilds = client.ShardClients.Values.Select(x => x.Guilds.Count).Aggregate((a, b) => a + b);

            var recordedGuilds = (await discordBotList.GetBotStatsAsync(client.CurrentUser.Id)).GuildCount;
            if (recordedGuilds < guilds)
            {
                await discordBotList.UpdateStats(guilds, client.ShardClients.Count);
                lastGuildCountUpdate = DateTime.Now;
            }

            log.Info($"Guild count updated to {guilds}");
        }
    }
}
